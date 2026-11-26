using MetroSort.Ads;
using MetroSort.Analytics;
using MetroSort.Boosters;
using MetroSort.Economy;
using MetroSort.Persistence;
using MetroSort.Purchases;
using NUnit.Framework;

namespace MetroSort.Tests
{
    public class ServiceTests
    {
        private EconomyConfig _config;
        private InMemoryFileStore _store;
        private SaveManager _save;
        private EconomyManager _economy;
        private ProgressionManager _progression;
        private BoosterManager _boosters;
        private RecordingAnalyticsService _analytics;

        [SetUp]
        public void SetUp()
        {
            _config = new EconomyConfig();
            _store = new InMemoryFileStore();
            _save = new SaveManager(_store, _config);
            _save.Load();
            _economy = new EconomyManager(_save, _config);
            _progression = new ProgressionManager(_save, _economy);
            _boosters = new BoosterManager(_save, _economy);
            _analytics = new RecordingAnalyticsService();
        }

        [Test]
        public void NewPlayer_GetsStartingBalance()
        {
            Assert.IsTrue(_save.StartedFresh);
            Assert.AreEqual(_config.StartingCoins, _economy.Coins);
            Assert.AreEqual(_config.StartingBoosters[(int)BoosterType.Undo], _boosters.Count(BoosterType.Undo));
            Assert.AreEqual(1, _progression.HighestUnlocked);
        }

        [Test]
        public void SaveLoad_RestoresProgress()
        {
            int token = _progression.BeginAttempt(1);
            _progression.CompleteLevel(token, 3);
            _save.Data.MusicVolume = 0.25f;
            _save.Data.ReducedMotion = true;
            _progression.MarkTutorialDone("select_move");
            _boosters.Grant(BoosterType.Shuffle, 2);
            int coins = _economy.Coins;
            Assert.IsTrue(_save.Save());

            var reloaded = new SaveManager(_store, _config);
            reloaded.Load();
            Assert.IsFalse(reloaded.StartedFresh);
            Assert.AreEqual(2, reloaded.Data.HighestUnlocked);
            Assert.AreEqual(3, reloaded.Data.Stars[1]);
            Assert.AreEqual(coins, reloaded.Data.Coins);
            Assert.AreEqual(0.25f, reloaded.Data.MusicVolume, 0.0001f);
            Assert.IsTrue(reloaded.Data.ReducedMotion);
            Assert.Contains("select_move", reloaded.Data.TutorialsDone);
            Assert.AreEqual(_config.StartingBoosters[3] + 2, reloaded.Data.Boosters[3]);
            Assert.AreEqual(SaveData.CurrentVersion, reloaded.Data.Version);
        }

        [Test]
        public void CorruptSave_FallsBackToBackup_ThenToFreshStart()
        {
            _economy.AddCoins(10);   // first save
            _economy.AddCoins(10);   // second save writes the first one as backup
            _store.Files[SaveManager.FileName] = "{ this is not json";
            var recovered = new SaveManager(_store, _config);
            recovered.Load();
            Assert.IsTrue(recovered.RecoveredFromBackup);
            Assert.AreEqual(_config.StartingCoins + 10, recovered.Data.Coins);

            _store.Files[SaveManager.BackupName] = "garbage";
            var fresh = new SaveManager(_store, _config);
            fresh.Load();
            Assert.IsTrue(fresh.StartedFresh);
            Assert.AreEqual(_config.StartingCoins, fresh.Data.Coins);
        }

        [Test]
        public void WriteFailure_DoesNotThrow_AndKeepsDataInMemory()
        {
            _store.FailWrites = true;
            _economy.AddCoins(25);
            Assert.IsNotNull(_save.LastError);
            Assert.AreEqual(_config.StartingCoins + 25, _economy.Coins);
        }

        [Test]
        public void DuplicateCompletionCallbacks_RewardOnlyOnce()
        {
            int before = _economy.Coins;
            int token = _progression.BeginAttempt(1);
            LevelResult first = _progression.CompleteLevel(token, 3);
            LevelResult second = _progression.CompleteLevel(token, 3);
            LevelResult stale = _progression.CompleteLevel(token - 1, 3);
            Assert.IsFalse(first.Duplicate);
            Assert.IsTrue(first.FirstClear);
            Assert.IsTrue(second.Duplicate);
            Assert.IsTrue(stale.Duplicate);
            Assert.AreEqual(before + first.CoinsAwarded, _economy.Coins);
            Assert.AreEqual(_config.CoinsPerLevel + 3 * _config.CoinsPerStar, first.CoinsAwarded);
            Assert.AreEqual(2, _progression.HighestUnlocked);
        }

        [Test]
        public void HardLevels_PayTheirBonusOnFirstClearOnly()
        {
            int token = _progression.BeginAttempt(1);
            LevelResult first = _progression.CompleteLevel(token, 3, 2);
            Assert.AreEqual(_config.CoinsPerLevel + 3 * _config.CoinsPerStar + _config.SuperHardLevelBonus, first.CoinsAwarded);
            token = _progression.BeginAttempt(1);
            LevelResult replay = _progression.CompleteLevel(token, 3, 2);
            Assert.AreEqual(_config.ReplayCoins, replay.CoinsAwarded);
        }

        [Test]
        public void StarChest_OpensWhenFull_AndKeepsTheRest()
        {
            _save.Data.ChestStars = 8;
            int before = _economy.Coins;
            int token = _progression.BeginAttempt(1);
            LevelResult result = _progression.CompleteLevel(token, 3);
            Assert.AreEqual(3, result.NewStars);
            Assert.AreEqual(8, result.ChestBefore);
            Assert.AreEqual(_config.StarChestCoins, result.ChestCoins);
            Assert.AreEqual(1, _save.Data.ChestStars);
            Assert.AreEqual(before + result.CoinsAwarded + result.ChestCoins, _economy.Coins);
            // Replaying for the same stars adds nothing to the chest.
            token = _progression.BeginAttempt(1);
            LevelResult again = _progression.CompleteLevel(token, 3);
            Assert.AreEqual(0, again.NewStars);
            Assert.AreEqual(1, _save.Data.ChestStars);
        }

        [Test]
        public void Replay_PaysSmallReward_AndNeverLowersStars()
        {
            _progression.CompleteLevel(_progression.BeginAttempt(1), 3);
            LevelResult replay = _progression.CompleteLevel(_progression.BeginAttempt(1), 1);
            Assert.IsFalse(replay.FirstClear);
            Assert.AreEqual(_config.ReplayCoins, replay.CoinsAwarded);
            Assert.AreEqual(3, _progression.GetStars(1));
            Assert.AreEqual(2, _progression.HighestUnlocked);
        }

        [Test]
        public void Stars_DropForExtraMovesAndAssists()
        {
            Assert.AreEqual(3, ProgressionManager.ComputeStars(9, 9, 0));
            Assert.AreEqual(2, ProgressionManager.ComputeStars(11, 9, 0));
            Assert.AreEqual(2, ProgressionManager.ComputeStars(9, 9, 2));
            Assert.AreEqual(1, ProgressionManager.ComputeStars(12, 9, 1));
        }

        [Test]
        public void Booster_UsesInventoryFirst_ThenCoins_AndNeverChargesWithoutEffect()
        {
            int coins = _economy.Coins;
            int shuffles = _boosters.Count(BoosterType.Shuffle);
            Assert.AreEqual(BoosterUseResult.NoEffect, _boosters.TryUse(BoosterType.Shuffle, () => false));
            Assert.AreEqual(shuffles, _boosters.Count(BoosterType.Shuffle));
            Assert.AreEqual(coins, _economy.Coins);

            for (int i = 0; i < shuffles; i++)
                Assert.AreEqual(BoosterUseResult.Used, _boosters.TryUse(BoosterType.Shuffle, () => true));
            Assert.AreEqual(0, _boosters.Count(BoosterType.Shuffle));
            Assert.AreEqual(coins, _economy.Coins);

            Assert.AreEqual(BoosterUseResult.Used, _boosters.TryUse(BoosterType.Shuffle, () => true));
            Assert.AreEqual(coins - _boosters.Cost(BoosterType.Shuffle), _economy.Coins);

            _economy.TrySpend(_economy.Coins);
            bool ran = false;
            Assert.AreEqual(BoosterUseResult.NotAffordable, _boosters.TryUse(BoosterType.Shuffle, () => { ran = true; return true; }));
            Assert.IsFalse(ran, "an unaffordable booster must not apply its effect");
        }

        [Test]
        public void DailyReward_OncePerDay()
        {
            int before = _economy.Coins;
            Assert.AreEqual(_config.DailyReward, _economy.ClaimDaily(20000));
            Assert.AreEqual(0, _economy.ClaimDaily(20000));
            Assert.AreEqual(before + _config.DailyReward, _economy.Coins);
            Assert.AreEqual(_config.DailyReward, _economy.ClaimDaily(20001));
        }

        [Test]
        public void FailedPurchase_GrantsNothing()
        {
            var service = new MockPurchaseService { NextSucceeds = false };
            var store = new StoreManager(service, new AcceptAllReceiptValidator(), _economy, _save, _analytics);
            int before = _economy.Coins;
            bool granted = true;
            store.Buy(StoreCatalog.CoinsSmall, ok => granted = ok);
            Assert.IsFalse(granted);
            Assert.AreEqual(before, _economy.Coins);
            Assert.IsFalse(_analytics.Events.Contains(AnalyticsEvents.PurchaseCompleted));
        }

        [Test]
        public void ConsumablePurchase_IsGrantedOncePerTransaction()
        {
            var service = new MockPurchaseService { ForcedTransactionId = "tx-1" };
            var store = new StoreManager(service, new AcceptAllReceiptValidator(), _economy, _save, _analytics);
            int before = _economy.Coins;
            bool first = false, second = true;
            store.Buy(StoreCatalog.CoinsSmall, ok => first = ok);
            store.Buy(StoreCatalog.CoinsSmall, ok => second = ok); // same transaction delivered again
            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(before + StoreCatalog.Find(StoreCatalog.CoinsSmall).Coins, _economy.Coins);
        }

        [Test]
        public void Bundle_GrantsCoinsBoostersAndUnlimitedLives()
        {
            const long now = 1_000_000;
            var lives = new LivesManager(_save, _economy, _config);
            var store = new StoreManager(new MockPurchaseService(), new AcceptAllReceiptValidator(), _economy, _save, _analytics,
                _boosters, lives, () => now);
            StoreProduct bundle = StoreCatalog.Find(StoreCatalog.BundleMedium);
            Assert.IsTrue(bundle.IsBundle);
            int coins = _economy.Coins;
            var boosters = new int[4];
            for (int i = 0; i < 4; i++) boosters[i] = _boosters.Count((BoosterType)i);
            bool granted = false;
            store.Buy(bundle.Id, ok => granted = ok);
            Assert.IsTrue(granted);
            Assert.AreEqual(coins + bundle.Coins, _economy.Coins);
            for (int i = 0; i < 4; i++) Assert.AreEqual(boosters[i] + bundle.Boosters[i], _boosters.Count((BoosterType)i));
            Assert.IsTrue(lives.IsUnlimited(now));
            Assert.AreEqual(bundle.UnlimitedLivesMinutes * 60L, lives.UnlimitedSecondsLeft(now));
        }

        [Test]
        public void StarterPack_IsSoldOnlyOnce()
        {
            var store = new StoreManager(new MockPurchaseService(), new AcceptAllReceiptValidator(), _economy, _save, _analytics,
                _boosters, new LivesManager(_save, _economy, _config), () => 1_000_000);
            int coins = _economy.Coins;
            bool first = false, second = true;
            store.Buy(StoreCatalog.StarterPack, ok => first = ok);
            store.Buy(StoreCatalog.StarterPack, ok => second = ok);
            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.IsTrue(store.StarterBought);
            Assert.AreEqual(coins + StoreCatalog.Find(StoreCatalog.StarterPack).Coins, _economy.Coins);
        }

        [Test]
        public void StarterOffer_SitsInTheShopFromTheStart_RunsFortyEightHoursFromItsLevel_AndEndsWhenBought()
        {
            var store = new StoreManager(new MockPurchaseService(), new AcceptAllReceiptValidator(), _economy, _save, _analytics,
                _boosters, new LivesManager(_save, _economy, _config), () => 1_000_000);
            const long t0 = 1_000_000;
            // On sale from the first level (App Review must find it), with no countdown and no popup yet.
            _save.Data.HighestUnlocked = 1;
            Assert.IsTrue(store.StarterAvailable(t0));
            Assert.IsTrue(store.StarterAvailable(t0 + 10 * StoreManager.StarterOfferSeconds), "no clock before its level");
            Assert.IsFalse(store.StarterTimerRunning);
            Assert.IsFalse(store.TryStartStarterOffer(t0));
            _save.Data.HighestUnlocked = StoreManager.StarterOfferLevel;
            Assert.IsTrue(store.StarterAvailable(t0));
            Assert.IsTrue(store.TryStartStarterOffer(t0));
            Assert.IsTrue(store.StarterTimerRunning);
            Assert.IsFalse(store.TryStartStarterOffer(t0 + 5), "the offer is presented only once");
            Assert.AreEqual(StoreManager.StarterOfferSeconds - 100, store.StarterSecondsLeft(t0 + 100));
            Assert.IsTrue(store.StarterAvailable(t0 + StoreManager.StarterOfferSeconds - 1));
            Assert.IsFalse(store.StarterAvailable(t0 + StoreManager.StarterOfferSeconds));

            // Reloaded from disk, the clock is still running; buying ends the offer.
            _save.Data.StarterOfferStart = t0;
            _save.Save();
            SaveData reloaded = SaveData.FromJson(_save.Data.ToJson());
            Assert.AreEqual(t0, reloaded.StarterOfferStart);
            store.Buy(StoreCatalog.StarterPack, ok => { });
            Assert.IsFalse(store.StarterAvailable(t0 + 10));
            Assert.Contains(StoreCatalog.StarterPack, SaveData.FromJson(_save.Data.ToJson()).OneTimePurchases);
        }

        [Test]
        public void RemoveAds_CanBeRestored_AndStopsInterstitials()
        {
            var service = new MockPurchaseService();
            service.OwnedNonConsumables.Add(StoreCatalog.RemoveAds);
            var store = new StoreManager(service, new AcceptAllReceiptValidator(), _economy, _save, _analytics);
            var ads = new AdsCoordinator(new MockAdsService(), _economy, _save, _analytics);
            _save.Data.LevelsSinceInterstitial = 99;
            Assert.IsTrue(ads.ShouldShowInterstitial(20));
            bool restored = false;
            store.Restore(ok => restored = ok);
            Assert.IsTrue(restored);
            Assert.IsTrue(store.AdsRemoved);
            Assert.IsFalse(ads.ShouldShowInterstitial(20));
        }

        [Test]
        public void UnavailableOrSkippedAd_GrantsNothing_RewardedAdGrantsOnce()
        {
            var mock = new MockAdsService { RewardedReady = false };
            var ads = new AdsCoordinator(mock, _economy, _save, _analytics);
            int before = _economy.Coins;
            bool result = true;
            ads.RequestRewardedCoins(ok => result = ok);
            Assert.IsFalse(result);
            Assert.AreEqual(before, _economy.Coins);
            Assert.IsTrue(_analytics.Events.Contains(AnalyticsEvents.RewardedRequested));

            mock.RewardedReady = true;
            mock.NextRewardedResult = AdResult.Skipped;
            ads.RequestRewardedCoins(ok => result = ok);
            Assert.IsFalse(result);
            Assert.AreEqual(before, _economy.Coins);

            mock.NextRewardedResult = AdResult.Rewarded;
            ads.RequestRewardedCoins(ok => result = ok);
            Assert.IsTrue(result);
            Assert.AreEqual(before + _config.RewardedAdCoins, _economy.Coins);
        }

        [Test]
        public void Interstitials_NeverShowInEarlyLevels()
        {
            var mock = new MockAdsService();
            var ads = new AdsCoordinator(mock, _economy, _save, _analytics);
            for (int level = 1; level <= _config.InterstitialFreeUntilLevel; level++)
            {
                bool continued = false;
                ads.OnLevelTransition(level, () => continued = true);
                Assert.IsTrue(continued);
            }
            Assert.AreEqual(0, mock.InterstitialsShown);
            int next = _config.InterstitialFreeUntilLevel + 1;
            for (int i = 0; i < _config.InterstitialEveryNLevels; i++) ads.OnLevelTransition(next + i, null);
            Assert.AreEqual(1, mock.InterstitialsShown);
        }

        [Test]
        public void Achievements_UnlockOnce()
        {
            var achievements = new AchievementManager(_save, _economy);
            int before = _economy.Coins;
            Assert.IsTrue(achievements.Unlock("first_train"));
            Assert.IsFalse(achievements.Unlock("first_train"));
            Assert.IsFalse(achievements.Unlock("does_not_exist"));
            Assert.AreEqual(before + 20, _economy.Coins);
        }
    }
}
