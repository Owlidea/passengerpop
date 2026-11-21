using MetroSort.Economy;
using MetroSort.Persistence;
using NUnit.Framework;

namespace MetroSort.Tests
{
    public class UnlimitedLivesTests
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
        public void WhileUnlimited_LosingALevelCostsNothing()
        {
            _lives.GrantUnlimited(600, T0);
            Assert.IsTrue(_lives.IsUnlimited(T0 + 10));
            Assert.IsFalse(_lives.Lose(T0 + 10));
            Assert.AreEqual(_lives.Max, _lives.Count(T0 + 10));
            // After it runs out, a loss costs a life again.
            Assert.IsFalse(_lives.IsUnlimited(T0 + 600));
            Assert.IsTrue(_lives.Lose(T0 + 600));
            Assert.AreEqual(_lives.Max - 1, _lives.Count(T0 + 600));
        }

        [Test]
        public void Unlimited_LetsPlayWithNoLivesLeft_AndStacks()
        {
            for (int i = 0; i < _lives.Max; i++) _lives.Lose(T0);
            Assert.IsFalse(_lives.CanPlay(T0));
            _lives.GrantUnlimited(300, T0);
            _lives.GrantUnlimited(300, T0 + 100);
            Assert.IsTrue(_lives.CanPlay(T0 + 100));
            Assert.AreEqual(500, _lives.UnlimitedSecondsLeft(T0 + 100));
        }

        [Test]
        public void BuyingUnlimited_SpendsCoins_AndIsSaved()
        {
            _economy.AddCoins(_config.UnlimitedLivesCost);
            int before = _economy.Coins;
            Assert.IsTrue(_lives.BuyUnlimited(T0));
            Assert.AreEqual(before - _config.UnlimitedLivesCost, _economy.Coins);
            var reloaded = new SaveManager(_store, _config);
            reloaded.Load();
            Assert.AreEqual(T0 + _config.UnlimitedLivesSeconds, reloaded.Data.UnlimitedLivesUntil);
        }

        [Test]
        public void BuyingUnlimited_WithoutCoins_Fails()
        {
            _economy.TrySpend(_economy.Coins);
            Assert.IsFalse(_lives.BuyUnlimited(T0));
            Assert.IsFalse(_lives.IsUnlimited(T0));
        }
    }
}
