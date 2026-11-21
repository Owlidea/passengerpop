using System;
using MetroSort.Persistence;

namespace MetroSort.Economy
{
    /// <summary>
    /// Lives, as in most puzzle games: a level that is lost (or abandoned part-way) costs one, and they come back
    /// one at a time on a timer up to a maximum. Starting a level needs at least one. All times are UTC unix
    /// seconds passed in by the caller, so the rules are testable and immune to the device clock zone.
    /// </summary>
    public sealed class LivesManager
    {
        private readonly SaveManager _save;
        private readonly EconomyManager _economy;
        private readonly EconomyConfig _config;

        public event Action Changed;

        public LivesManager(SaveManager save, EconomyManager economy, EconomyConfig config)
        {
            _save = save;
            _economy = economy;
            _config = config;
            if (_save.Data.Lives < 0 || _save.Data.Lives > config.MaxLives) _save.Data.Lives = config.MaxLives;
        }

        public int Max { get { return _config.MaxLives; } }
        public int RefillCost { get { return _config.LivesRefillCost; } }

        public static long Now(DateTime utcNow)
        {
            return (long)(utcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>Lives available now, after adding the ones that have come back since the last look.</summary>
        public int Count(long now)
        {
            Regenerate(now);
            return _save.Data.Lives;
        }

        public bool IsFull(long now) { return Count(now) >= Max; }

        public int UnlimitedCost { get { return _config.UnlimitedLivesCost; } }
        public int UnlimitedSeconds { get { return _config.UnlimitedLivesSeconds; } }

        /// <summary>True while unlimited lives are running: losing a level costs nothing.</summary>
        public bool IsUnlimited(long now) { return _save.Data.UnlimitedLivesUntil > now; }

        public long UnlimitedSecondsLeft(long now) { return Math.Max(0, _save.Data.UnlimitedLivesUntil - now); }

        /// <summary>A level can be started with a life in hand or during unlimited lives.</summary>
        public bool CanPlay(long now) { return IsUnlimited(now) || Count(now) > 0; }

        /// <summary>Adds unlimited time; it stacks on any that is still running.</summary>
        public void GrantUnlimited(long seconds, long now)
        {
            if (seconds <= 0) return;
            SaveData d = _save.Data;
            d.UnlimitedLivesUntil = Math.Max(d.UnlimitedLivesUntil, now) + seconds;
            _save.Save();
            Notify();
        }

        /// <summary>Buys the configured stretch of unlimited lives with coins. False when the coins are not there.</summary>
        public bool BuyUnlimited(long now)
        {
            if (!_economy.TrySpend(_config.UnlimitedLivesCost)) return false;
            GrantUnlimited(_config.UnlimitedLivesSeconds, now);
            return true;
        }

        /// <summary>Seconds until the next life comes back; 0 when full.</summary>
        public long SecondsToNext(long now)
        {
            if (Count(now) >= Max) return 0;
            long due = _save.Data.LivesTimerStart + _config.LifeRefillSeconds;
            return Math.Max(0, due - now);
        }

        /// <summary>Takes one life (a lost or abandoned level). Returns false when there was none to take.</summary>
        public bool Lose(long now)
        {
            Regenerate(now);
            SaveData d = _save.Data;
            if (IsUnlimited(now) || d.Lives <= 0) return false;
            // The refill clock starts when the first life goes missing.
            if (d.Lives >= Max) d.LivesTimerStart = now;
            d.Lives--;
            _save.Save();
            Notify();
            return true;
        }

        /// <summary>Gives lives back (a watched ad), never above the maximum.</summary>
        public void Add(int amount, long now)
        {
            if (amount <= 0) return;
            Regenerate(now);
            SaveData d = _save.Data;
            d.Lives = Math.Min(Max, d.Lives + amount);
            _save.Save();
            Notify();
        }

        /// <summary>Fills every life for coins. False when already full or the coins are not there.</summary>
        public bool RefillWithCoins(long now)
        {
            if (Count(now) >= Max || !_economy.TrySpend(_config.LivesRefillCost)) return false;
            _save.Data.Lives = Max;
            _save.Save();
            Notify();
            return true;
        }

        private void Regenerate(long now)
        {
            SaveData d = _save.Data;
            if (d.Lives >= Max) return;
            long step = Math.Max(1, _config.LifeRefillSeconds);
            // A clock set back leaves the timer where it was rather than handing out lives.
            if (now < d.LivesTimerStart) { d.LivesTimerStart = now; return; }
            long gained = (now - d.LivesTimerStart) / step;
            if (gained <= 0) return;
            d.Lives = (int)Math.Min(Max, d.Lives + gained);
            d.LivesTimerStart += gained * step;
            _save.Save();
            Notify();
        }

        private void Notify()
        {
            if (Changed != null) Changed();
        }
    }
}
