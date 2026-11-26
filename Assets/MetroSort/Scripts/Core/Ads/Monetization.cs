using System;
using System.Collections.Generic;
using MetroSort.Analytics;
using MetroSort.Boosters;
using MetroSort.Economy;
using MetroSort.Persistence;

namespace MetroSort.Ads
{
    public enum AdResult { Rewarded, Skipped, Failed, Unavailable }

    /// <summary>Implement this once per ad network. Gameplay never references a concrete SDK.</summary>
    public interface IAdsService
    {
        void Initialize();
        bool IsRewardedAdReady();
        void ShowRewardedAd(Action<AdResult> onFinished);
        bool IsInterstitialReady();
        void ShowInterstitialAd(Action onClosed);
        /// <summary>True where players must be able to reopen their ad consent choices (EEA, UK, ...).</summary>
        bool PrivacyOptionsRequired { get; }
        /// <summary>Reopens the consent choices; onClosed gets an error message, or null when all went well.</summary>
        void ShowPrivacyOptions(Action<string> onClosed);
    }

    /// <summary>Development stand-in. Completes immediately with a configurable outcome.</summary>
    public sealed class MockAdsService : IAdsService
    {
        public bool RewardedReady = true;
        public bool InterstitialReady = true;
        public AdResult NextRewardedResult = AdResult.Rewarded;
        public int RewardedShown;
        public int InterstitialsShown;

        public void Initialize() { }
        public bool IsRewardedAdReady() { return RewardedReady; }
        public bool IsInterstitialReady() { return InterstitialReady; }

        public void ShowRewardedAd(Action<AdResult> onFinished)
        {
            if (!RewardedReady)
            {
                if (onFinished != null) onFinished(AdResult.Unavailable);
                return;
            }
            RewardedShown++;
            if (onFinished != null) onFinished(NextRewardedResult);
        }

        public void ShowInterstitialAd(Action onClosed)
        {
            if (InterstitialReady) InterstitialsShown++;
            if (onClosed != null) onClosed();
        }

        public bool PrivacyOptionsRequired { get { return false; } }

        public void ShowPrivacyOptions(Action<string> onClosed)
        {
            if (onClosed != null) onClosed(null);
        }
    }

    /// <summary>Applies the game's ad rules on top of whichever IAdsService is installed.</summary>
    public sealed class AdsCoordinator
    {
        private readonly IAdsService _ads;
        private readonly EconomyManager _economy;
        private readonly SaveManager _save;
        private readonly IAnalyticsService _analytics;

        public AdsCoordinator(IAdsService ads, EconomyManager economy, SaveManager save, IAnalyticsService analytics)
        {
            _ads = ads;
            _economy = economy;
            _save = save;
            _analytics = analytics;
        }

        public bool IsRewardedReady { get { return _ads.IsRewardedAdReady(); } }

        /// <summary>Coins are granted only inside the Rewarded callback, never for merely requesting an ad.</summary>
        public void RequestRewardedCoins(Action<bool> onDone)
        {
            RequestRewarded(() => _economy.AddCoins(_economy.Config.RewardedAdCoins), onDone);
        }

        /// <summary>Shows a rewarded ad and runs grant once if it was watched to the end (a life, coins, ...).</summary>
        public void RequestRewarded(Action grant, Action<bool> onDone)
        {
            _analytics.Track(AnalyticsEvents.RewardedRequested, null);
            if (!_ads.IsRewardedAdReady())
            {
                if (onDone != null) onDone(false);
                return;
            }
            bool handled = false;
            _ads.ShowRewardedAd(result =>
            {
                if (handled) return; // an SDK firing its callback twice must not pay twice
                handled = true;
                bool rewarded = result == AdResult.Rewarded;
                if (rewarded)
                {
                    if (grant != null) grant();
                    _analytics.Track(AnalyticsEvents.RewardedCompleted, null);
                }
                if (onDone != null) onDone(rewarded);
            });
        }

        /// <summary>
        /// Interstitials are only considered between levels: never in the ad-free early levels, never for
        /// players who bought Remove Ads, and at most once every N completed levels.
        /// </summary>
        public bool ShouldShowInterstitial(int completedLevelId)
        {
            EconomyConfig cfg = _economy.Config;
            if (_save.Data.RemoveAds) return false;
            if (completedLevelId <= cfg.InterstitialFreeUntilLevel) return false;
            return _save.Data.LevelsSinceInterstitial + 1 >= cfg.InterstitialEveryNLevels;
        }

        public void OnLevelTransition(int completedLevelId, Action onContinue)
        {
            if (ShouldShowInterstitial(completedLevelId) && _ads.IsInterstitialReady())
            {
                _save.Data.LevelsSinceInterstitial = 0;
                _save.Save();
                _analytics.Track(AnalyticsEvents.InterstitialShown, new Dictionary<string, object> { { "level", completedLevelId } });
                _ads.ShowInterstitialAd(onContinue);
                return;
            }
            if (!_save.Data.RemoveAds && completedLevelId > _economy.Config.InterstitialFreeUntilLevel)
            {
                _save.Data.LevelsSinceInterstitial++;
                _save.Save();
            }
            if (onContinue != null) onContinue();
        }
    }
}

namespace MetroSort.Purchases
{
    public sealed class PurchaseResult
    {
        public bool Success;
        public string ProductId;
        public string TransactionId;
        public string Receipt;
        public string Error;
    }

    public interface IPurchaseService
    {
        void Initialize();
        void Purchase(string productId, Action<PurchaseResult> onFinished);
        /// <summary>Reports every previously bought non-consumable product id.</summary>
        void RestorePurchases(Action<List<string>> onFinished);
    }

    /// <summary>Plug a server or store receipt check in here before going live.</summary>
    public interface IReceiptValidator
    {
        bool IsValid(PurchaseResult result);
    }

    public sealed class AcceptAllReceiptValidator : IReceiptValidator
    {
        public bool IsValid(PurchaseResult result) { return true; }
    }

    public sealed class MockPurchaseService : IPurchaseService
    {
        public bool NextSucceeds = true;
        public string ForcedTransactionId;
        public readonly List<string> OwnedNonConsumables = new List<string>();
        private int _counter;

        public void Initialize() { }

        public void Purchase(string productId, Action<PurchaseResult> onFinished)
        {
            var r = new PurchaseResult { ProductId = productId, Success = NextSucceeds };
            if (NextSucceeds)
            {
                r.TransactionId = ForcedTransactionId ?? "mock-" + (++_counter);
                r.Receipt = "mock-receipt";
                if (productId == StoreCatalog.RemoveAds && !OwnedNonConsumables.Contains(productId)) OwnedNonConsumables.Add(productId);
            }
            else
            {
                r.Error = "Purchase cancelled";
            }
            if (onFinished != null) onFinished(r);
        }

        public void RestorePurchases(Action<List<string>> onFinished)
        {
            if (onFinished != null) onFinished(new List<string>(OwnedNonConsumables));
        }
    }

    public sealed class StoreProduct
    {
        public string Id;
        public string Title;
        public string PriceLabel;
        public int Coins;
        public bool Consumable;
        /// <summary>Boosters in the pack, indexed by BoosterType (Undo, Hint, ExtraPlatform, Shuffle); null for none.</summary>
        public int[] Boosters;
        /// <summary>Unlimited lives in the pack, in minutes; they stack on any still running.</summary>
        public int UnlimitedLivesMinutes;
        /// <summary>Sold once per player (the starter pack): the shop stops offering it once bought.</summary>
        public bool OncePerPlayer;

        /// <summary>A bundle: coins together with boosters or unlimited lives (plain coin packs are not).</summary>
        public bool IsBundle { get { return Boosters != null || UnlimitedLivesMinutes > 0; } }
    }

    public static class StoreCatalog
    {
        public const string CoinsSmall = "com.metrosort.coins.small";
        public const string CoinsLarge = "com.metrosort.coins.large";
        public const string RemoveAds = "com.metrosort.removeads";
        public const string StarterPack = "com.metrosort.starter";
        public const string BundleSmall = "com.metrosort.bundle.small";
        public const string BundleMedium = "com.metrosort.bundle.medium";
        public const string BundleLarge = "com.metrosort.bundle.large";

        // Coin packs kept low on purpose (2026-11-21); bundles added 2026-11-26 and priced for a new game the same
        // day: $0.99 to $9.99, half the first ladder ($1.99 to $19.99), contents kept. The labels are fallbacks:
        // on a device the store's own prices, in the player's currency, replace them (RevenueCatPurchaseService);
        // set the real prices in Google Play Console and App Store Connect. In coin terms (boosters at their shop
        // price, an hour of unlimited lives at 500; economy of 2026-11-26) the one-time starter pack is the best
        // deal, about 2780 coins per dollar against 505 for the smallest coin pack; the bundles give 1590 to 2320,
        // more the bigger they are.
        public static readonly StoreProduct[] Products =
        {
            new StoreProduct { Id = CoinsSmall, Title = "Pocket of Coins", PriceLabel = "$0.99", Coins = 500, Consumable = true },
            new StoreProduct { Id = CoinsLarge, Title = "Vault of Coins", PriceLabel = "$2.99", Coins = 1800, Consumable = true },
            new StoreProduct { Id = RemoveAds, Title = "Remove Ads", PriceLabel = "$1.99", Coins = 0, Consumable = false },
            new StoreProduct { Id = StarterPack, Title = "Starter Pack", PriceLabel = "$0.99", Coins = 1200, Consumable = true,
                Boosters = new[] { 3, 3, 3, 3 }, UnlimitedLivesMinutes = 60, OncePerPlayer = true },
            new StoreProduct { Id = BundleSmall, Title = "Traveller Bundle", PriceLabel = "$2.99", Coins = 3200, Consumable = true,
                Boosters = new[] { 3, 3, 3, 3 }, UnlimitedLivesMinutes = 60 },
            new StoreProduct { Id = BundleMedium, Title = "Conductor Bundle", PriceLabel = "$4.99", Coins = 7000, Consumable = true,
                Boosters = new[] { 6, 6, 6, 6 }, UnlimitedLivesMinutes = 180 },
            new StoreProduct { Id = BundleLarge, Title = "Station Master Bundle", PriceLabel = "$9.99", Coins = 15000, Consumable = true,
                Boosters = new[] { 12, 12, 12, 12 }, UnlimitedLivesMinutes = 480 }
        };

        /// <summary>Raised when the store's prices have replaced the fallback labels.</summary>
        public static event Action PricesChanged;

        public static void NotifyPricesChanged()
        {
            if (PricesChanged != null) PricesChanged();
        }

        public static StoreProduct Find(string id)
        {
            foreach (StoreProduct p in Products) if (p.Id == id) return p;
            return null;
        }
    }

    /// <summary>Turns purchase results into entitlements exactly once per transaction.</summary>
    public sealed class StoreManager
    {
        /// <summary>
        /// The starter pack sits in the shop from the first level (App Review must be able to find it). Once the
        /// player reaches this level it is presented once, and from then on it stays only ...
        /// </summary>
        public const int StarterOfferLevel = 10;
        /// <summary>... this long.</summary>
        public const long StarterOfferSeconds = 48L * 60 * 60;

        private readonly IPurchaseService _service;
        private readonly IReceiptValidator _validator;
        private readonly EconomyManager _economy;
        private readonly SaveManager _save;
        private readonly IAnalyticsService _analytics;
        private readonly BoosterManager _boosters;
        private readonly LivesManager _lives;
        private readonly Func<long> _clock;

        public event Action Changed;

        /// <summary>boosters and lives receive what bundles hold; clock gives UTC unix seconds (the device clock by default).</summary>
        public StoreManager(IPurchaseService service, IReceiptValidator validator, EconomyManager economy, SaveManager save, IAnalyticsService analytics,
            BoosterManager boosters = null, LivesManager lives = null, Func<long> clock = null)
        {
            _service = service;
            _validator = validator;
            _economy = economy;
            _save = save;
            _analytics = analytics;
            _boosters = boosters;
            _lives = lives;
            _clock = clock ?? (() => LivesManager.Now(DateTime.UtcNow));
        }

        public bool AdsRemoved { get { return _save.Data.RemoveAds; } }

        public bool StarterBought { get { return _save.Data.OneTimePurchases.Contains(StoreCatalog.StarterPack); } }

        /// <summary>The starter pack is on sale: not bought yet, and its 48 hours (if they have started) not over.</summary>
        public bool StarterAvailable(long now)
        {
            if (StarterBought) return false;
            long start = _save.Data.StarterOfferStart;
            return start == 0 || now < start + StarterOfferSeconds;
        }

        /// <summary>The 48 hours are running (the offer has been presented): the game shows its countdown.</summary>
        public bool StarterTimerRunning { get { return _save.Data.StarterOfferStart != 0; } }

        /// <summary>Time left on the starter offer (all of it until the offer is presented).</summary>
        public long StarterSecondsLeft(long now)
        {
            long start = _save.Data.StarterOfferStart;
            return start == 0 ? StarterOfferSeconds : Math.Max(0, start + StarterOfferSeconds - now);
        }

        /// <summary>
        /// Presents the starter offer the first time it is due (its level reached, not bought): starts its 48 hours
        /// and returns true. False ever after, and before the level.
        /// </summary>
        public bool TryStartStarterOffer(long now)
        {
            if (_save.Data.StarterOfferStart != 0 || _save.Data.HighestUnlocked < StarterOfferLevel || !StarterAvailable(now)) return false;
            _save.Data.StarterOfferStart = now;
            _save.Save();
            if (Changed != null) Changed();
            return true;
        }

        public void Buy(string productId, Action<bool> onDone)
        {
            StoreProduct product = StoreCatalog.Find(productId);
            // A once-per-player pack is never charged twice.
            if (product != null && product.OncePerPlayer && _save.Data.OneTimePurchases.Contains(product.Id))
            {
                if (onDone != null) onDone(false);
                return;
            }
            _service.Purchase(productId, result =>
            {
                bool granted = Grant(result);
                if (onDone != null) onDone(granted);
            });
        }

        /// <summary>Returns true only when something new was granted.</summary>
        public bool Grant(PurchaseResult result)
        {
            if (result == null || !result.Success || string.IsNullOrEmpty(result.TransactionId)) return false;
            StoreProduct product = StoreCatalog.Find(result.ProductId);
            if (product == null) return false;
            if (!_validator.IsValid(result)) return false;
            if (_save.Data.ProcessedTransactions.Contains(result.TransactionId)) return false;

            _save.Data.ProcessedTransactions.Add(result.TransactionId);
            if (product.Consumable)
            {
                _economy.AddCoins(product.Coins);
                if (product.Boosters != null && _boosters != null)
                    for (int i = 0; i < product.Boosters.Length && i < 4; i++)
                        if (product.Boosters[i] > 0) _boosters.Grant((BoosterType)i, product.Boosters[i]);
                if (product.UnlimitedLivesMinutes > 0 && _lives != null) _lives.GrantUnlimited(product.UnlimitedLivesMinutes * 60L, _clock());
                if (product.OncePerPlayer && !_save.Data.OneTimePurchases.Contains(product.Id)) _save.Data.OneTimePurchases.Add(product.Id);
            }
            else if (product.Id == StoreCatalog.RemoveAds) _save.Data.RemoveAds = true;
            _save.Save();
            var p = new Dictionary<string, object>();
            p["product"] = product.Id;
            _analytics.Track(AnalyticsEvents.PurchaseCompleted, p);
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>The store says a non-consumable is owned (bought on another device, or before a reinstall).</summary>
        public void MarkOwned(string productId)
        {
            if (productId != StoreCatalog.RemoveAds || _save.Data.RemoveAds) return;
            _save.Data.RemoveAds = true;
            _save.Save();
            if (Changed != null) Changed();
        }

        public void Restore(Action<bool> onDone)
        {
            _service.RestorePurchases(owned =>
            {
                bool any = false;
                if (owned != null && owned.Contains(StoreCatalog.RemoveAds) && !_save.Data.RemoveAds)
                {
                    _save.Data.RemoveAds = true;
                    _save.Save();
                    any = true;
                    if (Changed != null) Changed();
                }
                if (onDone != null) onDone(any);
            });
        }
    }
}

namespace MetroSort.Analytics
{
    public static class AnalyticsEvents
    {
        public const string GameLaunched = "game_launched";
        public const string TutorialStarted = "tutorial_started";
        public const string TutorialCompleted = "tutorial_completed";
        public const string LevelStarted = "level_started";
        public const string LevelCompleted = "level_completed";
        public const string LevelFailed = "level_failed";
        public const string LevelRestarted = "level_restarted";
        public const string BoosterUsed = "booster_used";
        public const string RewardedRequested = "rewarded_ad_requested";
        public const string RewardedCompleted = "rewarded_ad_completed";
        public const string PurchaseCompleted = "iap_completed";
        public const string InterstitialShown = "interstitial_shown";
        public const string ShopOpened = "shop_opened";
        public const string StarterOfferShown = "starter_offer_shown";
        public const string LivesEmpty = "lives_empty";
        public const string DailyGiftClaimed = "daily_gift_claimed";
        public const string MoreGamesOpened = "more_games_open";
    }

    /// <summary>Event sink. Implementations must never throw and must tolerate being offline.</summary>
    public interface IAnalyticsService
    {
        void Track(string eventName, Dictionary<string, object> parameters);
    }

    public sealed class NoOpAnalyticsService : IAnalyticsService
    {
        public void Track(string eventName, Dictionary<string, object> parameters) { }
    }

    /// <summary>Keeps events in memory; handy for tests and for an on-device debug overlay.</summary>
    public sealed class RecordingAnalyticsService : IAnalyticsService
    {
        public readonly List<string> Events = new List<string>();
        public void Track(string eventName, Dictionary<string, object> parameters) { Events.Add(eventName); }
    }
}
