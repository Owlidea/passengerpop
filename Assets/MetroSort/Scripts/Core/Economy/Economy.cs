using System;
using System.Collections.Generic;
using MetroSort.Persistence;

namespace MetroSort.Economy
{
    /// <summary>
    /// All tunable economy numbers in one place. The Unity layer can override these from a ScriptableObject
    /// (Resources/EconomyConfig.asset; keep the two in step).
    /// Retuned 2026-11-26 so that coins matter: a first clear pays about 17 plus the star chest (about 33 a level
    /// in all, it was about 58) and a continue costs 150 (was 80). Against the declared difficulty curve an
    /// attentive player stays in credit until about level 200 and then needs ads, waiting for lives or a pack;
    /// before, they piled up about 16000 spare coins by level 700 and had no reason to buy.
    /// </summary>
    public sealed class EconomyConfig
    {
        public int StartingCoins = 200;
        /// <summary>Indexed by BoosterType: Undo, Hint, ExtraPlatform, Shuffle.</summary>
        public int[] StartingBoosters = { 5, 3, 1, 1 };
        public int[] BoosterCosts = { 40, 60, 150, 100 };
        public int CoinsPerLevel = 10;
        public int CoinsPerStar = 3;
        public int ReplayCoins = 3;
        public int DailyReward = 50;
        public int RewardedAdCoins = 30;
        /// <summary>No interstitials at or below this level (tutorial and early game stay ad-free).</summary>
        public int InterstitialFreeUntilLevel = 10;
        public int InterstitialEveryNLevels = 3;
        /// <summary>Lives: the most a player can hold, how long one takes to come back, and the coin price of a full refill.</summary>
        public int MaxLives = 5;
        public int LifeRefillSeconds = 30 * 60;
        public int LivesRefillCost = 200;
        /// <summary>Unlimited lives: a stretch of time in which a lost level costs nothing (offer price in coins).</summary>
        public int UnlimitedLivesSeconds = 60 * 60;
        public int UnlimitedLivesCost = 500;

        // Star chest: every new star a level earns goes into the chest; it opens when full.
        public int StarChestStars = 10;
        public int StarChestCoins = 60;

        // First clear of a Hard / Super Hard level pays this on top.
        public int HardLevelBonus = 10;
        public int SuperHardLevelBonus = 20;
    }

    public sealed class EconomyManager
    {
        private readonly SaveManager _save;
        private readonly EconomyConfig _config;

        public event Action Changed;

        public EconomyManager(SaveManager save, EconomyConfig config)
        {
            _save = save;
            _config = config;
        }

        public EconomyConfig Config { get { return _config; } }
        public int Coins { get { return _save.Data.Coins; } }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _save.Data.Coins += amount;
            _save.Save();
            if (Changed != null) Changed();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || _save.Data.Coins < amount) return false;
            _save.Data.Coins -= amount;
            _save.Save();
            if (Changed != null) Changed();
            return true;
        }

        public static int TodayNumber(DateTime utcNow)
        {
            return (int)(utcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
        }

        public bool CanClaimDaily(int today) { return today > _save.Data.LastDailyClaimDay; }

        /// <summary>Returns the coins granted, or 0 when today's reward was already claimed.</summary>
        public int ClaimDaily(int today)
        {
            if (!CanClaimDaily(today)) return 0;
            _save.Data.LastDailyClaimDay = today;
            AddCoins(_config.DailyReward);
            return _config.DailyReward;
        }
    }

    public sealed class LevelResult
    {
        public int LevelId;
        public int Stars;
        public int CoinsAwarded;
        public bool FirstClear;
        /// <summary>True when this attempt had already been rewarded; nothing was granted this time.</summary>
        public bool Duplicate;
        /// <summary>Stars that beat the level's best so far; they go into the star chest.</summary>
        public int NewStars;
        /// <summary>Star chest progress before this level, and the coins it paid if it filled (0 otherwise).</summary>
        public int ChestBefore;
        public int ChestCoins;
    }

    /// <summary>
    /// Level unlocks, stars and completion rewards. Each attempt gets a token; a token can be redeemed once,
    /// so duplicate "level complete" callbacks from the presentation layer can never pay out twice.
    /// </summary>
    public sealed class ProgressionManager
    {
        private readonly SaveManager _save;
        private readonly EconomyManager _economy;
        private int _nextToken = 1;
        private int _activeToken;
        private int _activeLevel;
        private LevelResult _activeResult;

        public event Action Changed;

        public ProgressionManager(SaveManager save, EconomyManager economy)
        {
            _save = save;
            _economy = economy;
        }

        public int HighestUnlocked { get { return _save.Data.HighestUnlocked; } }

        public int GetStars(int levelId)
        {
            int s;
            return _save.Data.Stars.TryGetValue(levelId, out s) ? s : 0;
        }

        public bool IsCompleted(int levelId) { return GetStars(levelId) > 0; }
        public bool IsUnlocked(int levelId) { return levelId <= _save.Data.HighestUnlocked; }

        public int BeginAttempt(int levelId)
        {
            _activeToken = _nextToken++;
            _activeLevel = levelId;
            _activeResult = null;
            return _activeToken;
        }

        /// <summary>3 stars: solved in par moves with no boosters. One star is lost for extra moves, one for using help.</summary>
        public static int ComputeStars(int movesUsed, int par, int assistsUsed)
        {
            int stars = 3;
            if (par > 0 && movesUsed > par) stars--;
            if (assistsUsed > 0) stars--;
            return Math.Max(1, stars);
        }

        public LevelResult CompleteLevel(int attemptToken, int stars, int tier = 0)
        {
            if (attemptToken != _activeToken || _activeToken == 0)
                return new LevelResult { LevelId = _activeLevel, Duplicate = true };
            if (_activeResult != null)
                return new LevelResult { LevelId = _activeLevel, Stars = _activeResult.Stars, FirstClear = false, Duplicate = true };

            stars = Math.Max(1, Math.Min(3, stars));
            EconomyConfig cfg = _economy.Config;
            int previous = GetStars(_activeLevel);
            bool first = previous == 0;
            int bonus = tier >= 2 ? cfg.SuperHardLevelBonus : (tier == 1 ? cfg.HardLevelBonus : 0);
            int coins = first ? cfg.CoinsPerLevel + cfg.CoinsPerStar * stars + bonus : cfg.ReplayCoins;

            if (stars > previous) _save.Data.Stars[_activeLevel] = stars;
            if (_activeLevel >= _save.Data.HighestUnlocked) _save.Data.HighestUnlocked = _activeLevel + 1;
            // Only stars that beat the best so far fill the chest, so replaying an easy level earns nothing.
            int newStars = Math.Max(0, stars - previous);
            int chestBefore = _save.Data.ChestStars;
            int chestCoins = 0;
            _save.Data.ChestStars += newStars;
            if (cfg.StarChestStars > 0 && _save.Data.ChestStars >= cfg.StarChestStars)
            {
                _save.Data.ChestStars -= cfg.StarChestStars;
                chestCoins = cfg.StarChestCoins;
            }
            _activeResult = new LevelResult
            {
                LevelId = _activeLevel, Stars = stars, CoinsAwarded = coins, FirstClear = first,
                NewStars = newStars, ChestBefore = chestBefore, ChestCoins = chestCoins
            };
            _economy.AddCoins(coins + chestCoins); // also saves
            if (Changed != null) Changed();
            return _activeResult;
        }

        public bool IsTutorialDone(string id) { return _save.Data.TutorialsDone.Contains(id); }

        public void MarkTutorialDone(string id)
        {
            if (string.IsNullOrEmpty(id) || _save.Data.TutorialsDone.Contains(id)) return;
            _save.Data.TutorialsDone.Add(id);
            _save.Save();
        }
    }

    public sealed class AchievementDefinition
    {
        public string Id;
        public string Title;
        public int Reward;

        public AchievementDefinition(string id, string title, int reward)
        {
            Id = id;
            Title = title;
            Reward = reward;
        }
    }

    public sealed class AchievementManager
    {
        public static readonly AchievementDefinition[] All =
        {
            new AchievementDefinition("first_train", "First Departure", 20),
            new AchievementDefinition("level_5", "Commuter", 30),
            new AchievementDefinition("level_15", "Dispatcher", 60),
            new AchievementDefinition("level_30", "Station Master", 150),
            new AchievementDefinition("three_stars_10", "Perfect Timetable", 100)
        };

        private readonly SaveManager _save;
        private readonly EconomyManager _economy;

        public event Action<AchievementDefinition> Unlocked;

        public AchievementManager(SaveManager save, EconomyManager economy)
        {
            _save = save;
            _economy = economy;
        }

        public bool IsUnlocked(string id) { return _save.Data.Achievements.Contains(id); }

        public bool Unlock(string id)
        {
            if (IsUnlocked(id)) return false;
            AchievementDefinition def = null;
            foreach (AchievementDefinition d in All) if (d.Id == id) def = d;
            if (def == null) return false;
            _save.Data.Achievements.Add(id);
            _economy.AddCoins(def.Reward);
            if (Unlocked != null) Unlocked(def);
            return true;
        }

        /// <summary>Call after a level is completed to unlock anything newly earned.</summary>
        public void EvaluateProgress(int levelId, Dictionary<int, int> stars)
        {
            Unlock("first_train");
            if (levelId >= 5) Unlock("level_5");
            if (levelId >= 15) Unlock("level_15");
            if (levelId >= 30) Unlock("level_30");
            int perfect = 0;
            foreach (KeyValuePair<int, int> kv in stars) if (kv.Value >= 3) perfect++;
            if (perfect >= 10) Unlock("three_stars_10");
        }
    }
}
