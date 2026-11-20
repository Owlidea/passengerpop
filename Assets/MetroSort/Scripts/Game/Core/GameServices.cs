using System;
using System.Collections.Generic;
using System.IO;
using MetroSort.Ads;
using MetroSort.Analytics;
using MetroSort.Boosters;
using MetroSort.Economy;
using MetroSort.Levels;
using MetroSort.Persistence;
using MetroSort.Purchases;
using UnityEngine;

namespace MetroSort.Game
{
    public enum AppState { Boot, Menu, Playing, Paused, LevelComplete, LevelFailed }

    /// <summary>Single source of truth for which screen/phase the app is in.</summary>
    public sealed class GameStateManager
    {
        public AppState Current { get; private set; }
        public event Action<AppState> Changed;

        public void Set(AppState state)
        {
            if (Current == state) return;
            Current = state;
            if (Changed != null) Changed(state);
        }
    }

    /// <summary>Stores save files under Application.persistentDataPath, writing through a temp file.</summary>
    public sealed class UnityFileStore : IFileStore
    {
        private readonly string _root;

        public UnityFileStore() { _root = Application.persistentDataPath; }

        private string PathFor(string name) { return Path.Combine(_root, name); }
        public bool Exists(string name) { return File.Exists(PathFor(name)); }
        public string Read(string name) { return File.ReadAllText(PathFor(name)); }
        public void Delete(string name) { if (Exists(name)) File.Delete(PathFor(name)); }

        public void Write(string name, string text)
        {
            string path = PathFor(name);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

    /// <summary>Loads every level definition from Resources/Levels. One JSON file per level, no scenes.</summary>
    public sealed class LevelManager
    {
        private readonly List<LevelData> _levels = new List<LevelData>();

        public int Count { get { return _levels.Count; } }

        public void Load()
        {
            _levels.Clear();
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Levels"))
            {
                try { _levels.Add(LevelData.FromJson(asset.text)); }
                catch (Exception e) { Debug.LogError("Metro Sort: could not read level '" + asset.name + "': " + e.Message); }
            }
            _levels.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (_levels.Count == 0)
            {
                Debug.LogWarning("Metro Sort: no level files found in Resources/Levels, using the built-in intro level.");
                var intro = new LevelData { Id = 1, Name = "Three of a Kind", Group = "Intro", Par = 9 };
                intro.Stages.Add(new[] { "GBR", "BRG", "GBR" });
                for (int i = 0; i < 3; i++) intro.Platforms.Add(new PlatformConfig());
                _levels.Add(intro);
            }
        }

        public bool Has(int id) { return id >= 1 && id <= _levels.Count; }

        public LevelData Get(int id)
        {
            return _levels[Mathf.Clamp(id, 1, _levels.Count) - 1];
        }
    }

    /// <summary>Logs events to the console in development builds; swap for a real provider at release.</summary>
    public sealed class DebugLogAnalyticsService : IAnalyticsService
    {
        public void Track(string eventName, Dictionary<string, object> parameters)
        {
            if (!Debug.isDebugBuild) return;
            var sb = new System.Text.StringBuilder("[analytics] ").Append(eventName);
            if (parameters != null)
                foreach (KeyValuePair<string, object> kv in parameters) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
        }
    }

    /// <summary>Composition root for everything that is not a scene object.</summary>
    public sealed class GameServices
    {
        public EconomyConfig Config;
        public SaveManager Save;
        public EconomyManager Economy;
        public LivesManager Lives;
        public ProgressionManager Progression;
        public BoosterManager Boosters;
        public AchievementManager Achievements;
        public IAnalyticsService Analytics;
        public IAdsService Ads;
        public AdsCoordinator AdsCoordinator;
        public IPurchaseService Purchases;
        public StoreManager Store;
        public LevelManager Levels;

        /// <summary>Pass a custom file store (tests) or null for the real one.</summary>
        public static GameServices Create(IFileStore store)
        {
            var s = new GameServices();
            var asset = Resources.Load<EconomyConfigAsset>("EconomyConfig");
            s.Config = asset != null ? asset.ToConfig() : new EconomyConfig();
            s.Save = new SaveManager(store ?? new UnityFileStore(), s.Config);
            s.Save.Load();
            s.Economy = new EconomyManager(s.Save, s.Config);
            s.Lives = new LivesManager(s.Save, s.Economy, s.Config);
            s.Progression = new ProgressionManager(s.Save, s.Economy);
            s.Boosters = new BoosterManager(s.Save, s.Economy);
            s.Achievements = new AchievementManager(s.Save, s.Economy);
            s.Analytics = new DebugLogAnalyticsService();
            // Development stand-ins. Replace with real SDK adapters once store accounts and ad units exist.
            s.Ads = new MockAdsService();
            s.Ads.Initialize();
            s.AdsCoordinator = new AdsCoordinator(s.Ads, s.Economy, s.Save, s.Analytics);
            s.Purchases = new MockPurchaseService();
            s.Purchases.Initialize();
            s.Store = new StoreManager(s.Purchases, new AcceptAllReceiptValidator(), s.Economy, s.Save, s.Analytics);
            s.Levels = new LevelManager();
            s.Levels.Load();
            return s;
        }
    }
}
