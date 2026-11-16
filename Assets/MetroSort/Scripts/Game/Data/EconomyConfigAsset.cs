using MetroSort.Economy;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Designer-editable economy numbers. Create one at Assets/MetroSort/Resources/EconomyConfig.asset
    /// (the editor setup script does this automatically); without it the code defaults are used.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Metro Sort/Economy Config")]
    public sealed class EconomyConfigAsset : ScriptableObject
    {
        public int startingCoins = 150;
        [Tooltip("Undo, Hint, Extra Platform, Shuffle")]
        public int[] startingBoosters = { 5, 3, 1, 1 };
        [Tooltip("Undo, Hint, Extra Platform, Shuffle")]
        public int[] boosterCosts = { 15, 30, 80, 50 };
        public int coinsPerLevel = 20;
        public int coinsPerStar = 5;
        public int replayCoins = 5;
        public int dailyReward = 60;
        public int rewardedAdCoins = 40;
        public int interstitialFreeUntilLevel = 10;
        public int interstitialEveryNLevels = 3;

        public EconomyConfig ToConfig()
        {
            var c = new EconomyConfig
            {
                StartingCoins = startingCoins,
                CoinsPerLevel = coinsPerLevel,
                CoinsPerStar = coinsPerStar,
                ReplayCoins = replayCoins,
                DailyReward = dailyReward,
                RewardedAdCoins = rewardedAdCoins,
                InterstitialFreeUntilLevel = interstitialFreeUntilLevel,
                InterstitialEveryNLevels = Mathf.Max(1, interstitialEveryNLevels)
            };
            if (startingBoosters != null && startingBoosters.Length == 4) c.StartingBoosters = (int[])startingBoosters.Clone();
            if (boosterCosts != null && boosterCosts.Length == 4) c.BoosterCosts = (int[])boosterCosts.Clone();
            return c;
        }
    }
}
