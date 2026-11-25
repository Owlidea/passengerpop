using MetroSort.Analytics;
using MetroSort.Persistence;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetroSort.Game
{
    /// <summary>
    /// GameBootstrap: creates the whole game from code when play starts, in whatever scene is open.
    /// Nothing in a scene references scripts, so the project cannot break through missing references.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public GameServices Services { get; private set; }
        public GameStateManager States { get; private set; }
        public ObjectPoolManager Pool { get; private set; }
        public AudioManager Audio { get; private set; }
        public StationView Station { get; private set; }
        public AnimationCoordinator Anim { get; private set; }
        public GameController Game { get; private set; }
        public TutorialManager Tutorial { get; private set; }
        public UIManager UI { get; private set; }
        /// <summary>The studio's other games, for the "Our Games" shelf.</summary>
        public MoreGames MoreGames { get; private set; }
        public InputController Input { get; private set; }

        /// <summary>The level the Play button starts: the furthest unlocked one that exists.</summary>
        public int CurrentLevelId
        {
            get { return Mathf.Clamp(Services.Progression.HighestUnlocked, 1, Services.Levels.Count); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Instance == null) Create(null);
        }

        /// <summary>Builds a fresh game. Tests pass an in-memory store; the real game passes null.</summary>
        public static GameApp Create(IFileStore store)
        {
            if (Instance != null) DestroyImmediate(Instance.gameObject);
            var go = new GameObject("MetroSort");
            DontDestroyOnLoad(go);
            var app = go.AddComponent<GameApp>();
            app.Boot(store);
            return app;
        }

        private T AddChild<T>(string childName) where T : Component
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private void Boot(IFileStore store)
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            Services = GameServices.Create(store);
            SaveData data = Services.Save.Data;
            GameSettings.AssistedMode = data.AssistedMode;
            GameSettings.ReducedMotion = data.ReducedMotion;
            GameSettings.FastTrains = data.FastTrains;
            Loc.Init(data.Language);
            States = new GameStateManager();

            Camera cam = SetupCameraAndLight();
            Pool = AddChild<ObjectPoolManager>("Pool");
            Pool.Prewarm(PassengerView.PoolKey, PassengerView.CreateObject, 24);
            Pool.Prewarm(TrainView.PoolKey, TrainView.CreateObject, 2);

            Audio = AddChild<AudioManager>("Audio");
            Audio.Init(data.MusicVolume, data.SfxVolume);
            States.Changed += state => Audio.SetMusic(state == AppState.Menu || state == AppState.Boot);
            Station = AddChild<StationView>("Station");
            Station.Init(cam, Pool);
            Anim = AddChild<AnimationCoordinator>("Animation");
            Anim.Init(Station, Audio, Pool);
            Game = AddChild<GameController>("Game");
            Game.Init(this);
            Tutorial = AddChild<TutorialManager>("Tutorial");
            if (MoreGames == null) MoreGames = AddChild<MoreGames>("MoreGames");
            Tutorial.Init(this);
            UI = AddChild<UIManager>("UI");
            UI.Init(this);
            Input = AddChild<InputController>("Input");
            Input.Init(this);

            Services.Analytics.Track(AnalyticsEvents.GameLaunched, null);
            GoToMenu();
            // The real game opens on the StagFunny studio intro (tests skip it). The menu is built underneath; the
            // music waits for the intro, and so do the ads, so their consent form never covers the logo.
            if (store == null && BrandIntro.Play(transform, data.ReducedMotion, AfterIntro)) Audio.MusicHeld = true;
            else AfterIntro();
        }

        private void AfterIntro()
        {
            Audio.MusicHeld = false;
            Services.Ads.Initialize();
            if (Services.Save.RecoveredFromBackup) UI.Toast("Your progress was restored from a backup");
        }

        private Camera SetupCameraAndLight()
        {
            // Take over rendering from whatever the open scene contains.
            foreach (Camera existing in FindObjectsByType<Camera>()) existing.gameObject.SetActive(false);
            foreach (Light existing in FindObjectsByType<Light>()) existing.enabled = false;
            foreach (AudioListener existing in FindObjectsByType<AudioListener>()) existing.enabled = false;

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Floor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -28f, 0f);
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.38f;

            // Three-tone ambient: cool light from above, warm bounce from the floor.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.80f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.68f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.62f, 0.54f, 0.48f);
            RenderPolish.Apply(cam, transform);
            QualitySettings.shadowDistance = 70f;
            return cam;
        }

        public void GoToMenu()
        {
            States.Set(AppState.Menu);
            Game.ShowBackdrop(CurrentLevelId);
            UI.SetTutorialText(null);
            UI.Refresh();
        }

        private bool _rebuildUi;

        /// <summary>Switches language. The UI is rebuilt on the next frame, outside the button's own click event.</summary>
        public void ChangeLanguage(string language)
        {
            Loc.Init(language);
            _rebuildUi = true;
        }

        private void Update()
        {
            if (!_rebuildUi) return;
            _rebuildUi = false;
            bool inGame = States.Current == AppState.Paused;
            UI.Unsubscribe();
            DestroyImmediate(UI.gameObject);
            UI = AddChild<UIManager>("UI");
            UI.Init(this);
            if (Game.Board != null)
            {
                Station.RefreshPlatforms(Game.Board.State);
                Tutorial.RefreshText();
            }
            // The switch is made from the settings panel, so put the player back there.
            UI.OpenSettings(inGame);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Services != null) Services.Save.Save();
        }

        private void OnApplicationQuit()
        {
            if (Services != null) Services.Save.Save();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
