using MetroSort.Economy;
using MetroSort.Persistence;
using NUnit.Framework;

namespace MetroSort.Tests
{
    public class LivesTests
    {
        private EconomyConfig _config;
        private InMemoryFileStore _store;
        private SaveManager _save;
        private EconomyManager _economy;
        private LivesManager _lives;
        private const long T0 = 1_800_000_000;

        [SetUp]
        public void SetUp()
        {
            _config = new EconomyConfig();
            _store = new InMemoryFileStore();
            _save = new SaveManager(_store, _config);
            _save.Load();
            _economy = new EconomyManager(_save, _config);
            _lives = new LivesManager(_save, _economy, _config);
        }

        [Test]
        public void NewPlayer_StartsFull()
        {
            Assert.AreEqual(_config.MaxLives, _lives.Count(T0));
            Assert.AreEqual(0, _lives.SecondsToNext(T0));
        }

        [Test]
        public void Losing_TakesOneAndStartsTheTimer()
        {
            Assert.IsTrue(_lives.Lose(T0));
            Assert.AreEqual(_config.MaxLives - 1, _lives.Count(T0));
            Assert.AreEqual(_config.LifeRefillSeconds, _lives.SecondsToNext(T0));
            Assert.AreEqual(_config.LifeRefillSeconds - 100, _lives.SecondsToNext(T0 + 100));
        }

        [Test]
        public void Lives_ComeBackOneAtATime_AndStopAtTheMaximum()
        {
            for (int i = 0; i < 3; i++) _lives.Lose(T0);
            long step = _config.LifeRefillSeconds;
            Assert.AreEqual(_config.MaxLives - 3, _lives.Count(T0 + step - 1));
            Assert.AreEqual(_config.MaxLives - 2, _lives.Count(T0 + step));
            // The second life is due one full step after the first, not after the moment we looked.
            Assert.AreEqual(step, _lives.SecondsToNext(T0 + step));
            Assert.AreEqual(_config.MaxLives, _lives.Count(T0 + step * 10));
            Assert.AreEqual(0, _lives.SecondsToNext(T0 + step * 10));
        }

        [Test]
        public void NoLivesLeft_CannotLoseMore()
        {
            for (int i = 0; i < _config.MaxLives; i++) Assert.IsTrue(_lives.Lose(T0));
            Assert.AreEqual(0, _lives.Count(T0));
            Assert.IsFalse(_lives.Lose(T0));
        }

        [Test]
        public void Refill_CostsCoins_AndFillsEveryLife()
        {
            _lives.Lose(T0);
            _lives.Lose(T0);
            int coins = _economy.Coins;
            Assert.IsTrue(_lives.RefillWithCoins(T0));
            Assert.AreEqual(_config.MaxLives, _lives.Count(T0));
            Assert.AreEqual(coins - _config.LivesRefillCost, _economy.Coins);
            Assert.IsFalse(_lives.RefillWithCoins(T0), "a full set needs no refill");
        }

        [Test]
        public void Refill_WithoutEnoughCoins_ChangesNothing()
        {
            _lives.Lose(T0);
            _economy.TrySpend(_economy.Coins);
            Assert.IsFalse(_lives.RefillWithCoins(T0));
            Assert.AreEqual(_config.MaxLives - 1, _lives.Count(T0));
        }

        [Test]
        public void ClockSetBack_DoesNotHandOutLives()
        {
            _lives.Lose(T0);
            Assert.AreEqual(_config.MaxLives - 1, _lives.Count(T0 - 100000));
        }

        [Test]
        public void Lives_SurviveASaveAndReload()
        {
            _lives.Lose(T0);
            _lives.Lose(T0 + 60);
            var reloaded = new SaveManager(_store, _config);
            reloaded.Load();
            var lives = new LivesManager(reloaded, new EconomyManager(reloaded, _config), _config);
            Assert.AreEqual(_config.MaxLives - 2, lives.Count(T0 + 61));
            Assert.AreEqual(_config.LifeRefillSeconds - 61, lives.SecondsToNext(T0 + 61));
        }
    }
}
