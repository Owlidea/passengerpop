using System;
using System.Collections.Generic;
using MetroSort.Economy;
using MetroSort.Utilities;

namespace MetroSort.Persistence
{
    /// <summary>Everything that must survive an app restart. Bump CurrentVersion and extend Migrate when it changes.</summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public int HighestUnlocked = 1;
        public Dictionary<int, int> Stars = new Dictionary<int, int>();
        public int Coins;
        public int[] Boosters = new int[4];
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.9f;
        public bool AssistedMode = true;
        public bool ReducedMotion;
        public bool FastTrains;
        public List<string> TutorialsDone = new List<string>();
        public List<string> Achievements = new List<string>();
        public int LastDailyClaimDay = -1;
        public bool RemoveAds;
        public List<string> ProcessedTransactions = new List<string>();
        public int LevelsSinceInterstitial;

        public static SaveData CreateNew(EconomyConfig config)
        {
            var d = new SaveData();
            d.Coins = config.StartingCoins;
            Array.Copy(config.StartingBoosters, d.Boosters, Math.Min(4, config.StartingBoosters.Length));
            return d;
        }

        public string ToJson()
        {
            var root = new Dictionary<string, object>();
            root["version"] = Version;
            root["highestUnlocked"] = HighestUnlocked;
            var stars = new Dictionary<string, object>();
            foreach (KeyValuePair<int, int> kv in Stars)
                stars[kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = kv.Value;
            root["stars"] = stars;
            root["coins"] = Coins;
            root["boosters"] = new List<object> { Boosters[0], Boosters[1], Boosters[2], Boosters[3] };
            root["musicVolume"] = MusicVolume;
            root["sfxVolume"] = SfxVolume;
            root["assistedMode"] = AssistedMode;
            root["reducedMotion"] = ReducedMotion;
            root["fastTrains"] = FastTrains;
            root["tutorialsDone"] = ToObjects(TutorialsDone);
            root["achievements"] = ToObjects(Achievements);
            root["lastDailyClaimDay"] = LastDailyClaimDay;
            root["removeAds"] = RemoveAds;
            root["processedTransactions"] = ToObjects(ProcessedTransactions);
            root["levelsSinceInterstitial"] = LevelsSinceInterstitial;
            return MiniJson.Serialize(root);
        }

        /// <summary>Throws FormatException when the text is not a usable save.</summary>
        public static SaveData FromJson(string json)
        {
            var root = JsonRead.AsDict(MiniJson.Parse(json));
            if (root == null || !root.ContainsKey("version")) throw new FormatException("Not a Metro Sort save file");
            var d = new SaveData();
            d.Version = JsonRead.Int(root, "version", 1);
            d.HighestUnlocked = Math.Max(1, JsonRead.Int(root, "highestUnlocked", 1));
            var stars = JsonRead.Dict(root, "stars");
            if (stars != null)
            {
                foreach (KeyValuePair<string, object> kv in stars)
                {
                    int id;
                    if (int.TryParse(kv.Key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id) && kv.Value is double)
                        d.Stars[id] = Math.Max(0, Math.Min(3, (int)(double)kv.Value));
                }
            }
            d.Coins = Math.Max(0, JsonRead.Int(root, "coins", 0));
            List<object> boosters = JsonRead.List(root, "boosters");
            for (int i = 0; i < 4 && i < boosters.Count; i++)
                if (boosters[i] is double) d.Boosters[i] = Math.Max(0, (int)(double)boosters[i]);
            d.MusicVolume = Clamp01(JsonRead.Float(root, "musicVolume", 0.6f));
            d.SfxVolume = Clamp01(JsonRead.Float(root, "sfxVolume", 0.9f));
            d.AssistedMode = JsonRead.Bool(root, "assistedMode", true);
            d.ReducedMotion = JsonRead.Bool(root, "reducedMotion", false);
            d.FastTrains = JsonRead.Bool(root, "fastTrains", false);
            d.TutorialsDone = ToStrings(JsonRead.List(root, "tutorialsDone"));
            d.Achievements = ToStrings(JsonRead.List(root, "achievements"));
            d.LastDailyClaimDay = JsonRead.Int(root, "lastDailyClaimDay", -1);
            d.RemoveAds = JsonRead.Bool(root, "removeAds", false);
            d.ProcessedTransactions = ToStrings(JsonRead.List(root, "processedTransactions"));
            d.LevelsSinceInterstitial = JsonRead.Int(root, "levelsSinceInterstitial", 0);
            Migrate(d);
            return d;
        }

        private static void Migrate(SaveData d)
        {
            // v1 had no shuffle booster slot or reduced-motion flag; defaults already cover both.
            if (d.Version > CurrentVersion) throw new FormatException("Save file is from a newer version of the game");
            d.Version = CurrentVersion;
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        private static List<object> ToObjects(List<string> list)
        {
            var o = new List<object>(list.Count);
            foreach (string s in list) o.Add(s);
            return o;
        }

        private static List<string> ToStrings(List<object> list)
        {
            var o = new List<string>(list.Count);
            foreach (object s in list) if (s is string) o.Add((string)s);
            return o;
        }
    }

    /// <summary>Minimal storage abstraction: local files today, could be cloud-backed later.</summary>
    public interface IFileStore
    {
        bool Exists(string name);
        string Read(string name);
        void Write(string name, string text);
        void Delete(string name);
    }

    public sealed class InMemoryFileStore : IFileStore
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public bool FailWrites;

        public bool Exists(string name) { return Files.ContainsKey(name); }
        public string Read(string name) { return Files[name]; }
        public void Delete(string name) { Files.Remove(name); }
        public void Write(string name, string text)
        {
            if (FailWrites) throw new System.IO.IOException("Simulated write failure");
            Files[name] = text;
        }
    }

    /// <summary>Hook for a future cloud sync provider; not used by the local MVP.</summary>
    public interface ICloudSaveProvider
    {
        void Upload(string json, Action<bool> onDone);
        void Download(Action<bool, string> onDone);
    }

    /// <summary>
    /// Loads and saves SaveData with recovery: a corrupt main file falls back to the last good backup,
    /// and only if both are unusable does the player start fresh.
    /// </summary>
    public sealed class SaveManager
    {
        public const string FileName = "metrosort_save.json";
        public const string BackupName = "metrosort_save.bak.json";

        private readonly IFileStore _store;
        private readonly EconomyConfig _config;
        private string _lastGoodJson;

        public SaveData Data { get; private set; }
        public bool RecoveredFromBackup { get; private set; }
        public bool StartedFresh { get; private set; }
        public string LastError { get; private set; }

        public SaveManager(IFileStore store, EconomyConfig config)
        {
            _store = store;
            _config = config;
            Data = SaveData.CreateNew(config);
        }

        public void Load()
        {
            RecoveredFromBackup = false;
            StartedFresh = false;
            SaveData main = TryRead(FileName);
            if (main != null) { Data = main; return; }
            SaveData backup = TryRead(BackupName);
            if (backup != null)
            {
                Data = backup;
                RecoveredFromBackup = true;
                return;
            }
            Data = SaveData.CreateNew(_config);
            StartedFresh = true;
        }

        public bool Save()
        {
            try
            {
                string json = Data.ToJson();
                if (_lastGoodJson != null) _store.Write(BackupName, _lastGoodJson);
                _store.Write(FileName, json);
                _lastGoodJson = json;
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        public void ResetAll()
        {
            Data = SaveData.CreateNew(_config);
            Save();
        }

        private SaveData TryRead(string name)
        {
            try
            {
                if (!_store.Exists(name)) return null;
                string json = _store.Read(name);
                SaveData d = SaveData.FromJson(json);
                _lastGoodJson = json;
                return d;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return null;
            }
        }
    }
}
