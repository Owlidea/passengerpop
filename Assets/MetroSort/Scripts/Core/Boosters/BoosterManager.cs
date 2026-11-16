using System;
using MetroSort.Economy;
using MetroSort.Persistence;

namespace MetroSort.Boosters
{
    public enum BoosterType { Undo = 0, Hint = 1, ExtraPlatform = 2, Shuffle = 3 }

    public enum BoosterUseResult { Used, NotAffordable, NoEffect }

    /// <summary>
    /// Booster inventory and payment. A booster is paid for (from inventory first, then coins) only after its
    /// effect reports success, so a shuffle with no valid arrangement or an undo with no history costs nothing.
    /// </summary>
    public sealed class BoosterManager
    {
        private readonly SaveManager _save;
        private readonly EconomyManager _economy;

        public event Action Changed;

        public BoosterManager(SaveManager save, EconomyManager economy)
        {
            _save = save;
            _economy = economy;
        }

        public int Count(BoosterType type) { return _save.Data.Boosters[(int)type]; }
        public int Cost(BoosterType type) { return _economy.Config.BoosterCosts[(int)type]; }
        public bool CanPay(BoosterType type) { return Count(type) > 0 || _economy.Coins >= Cost(type); }

        public BoosterUseResult TryUse(BoosterType type, Func<bool> effect)
        {
            if (!CanPay(type)) return BoosterUseResult.NotAffordable;
            if (effect == null || !effect()) return BoosterUseResult.NoEffect;
            Pay(type);
            return BoosterUseResult.Used;
        }

        /// <summary>Charges for a booster whose effect has already been applied successfully.</summary>
        public bool Pay(BoosterType type)
        {
            if (Count(type) > 0)
            {
                _save.Data.Boosters[(int)type]--;
                _save.Save();
            }
            else if (!_economy.TrySpend(Cost(type)))
            {
                return false;
            }
            if (Changed != null) Changed();
            return true;
        }

        public void Grant(BoosterType type, int amount)
        {
            if (amount <= 0) return;
            _save.Data.Boosters[(int)type] += amount;
            _save.Save();
            if (Changed != null) Changed();
        }

        /// <summary>Buys one charge with coins. Returns false when the player cannot afford it.</summary>
        public bool Buy(BoosterType type)
        {
            if (!_economy.TrySpend(Cost(type))) return false;
            Grant(type, 1);
            return true;
        }
    }
}
