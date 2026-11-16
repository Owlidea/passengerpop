using System;
using System.Collections;
using System.Collections.Generic;
using MetroSort.Boosters;
using MetroSort.Economy;
using MetroSort.Levels;
using MetroSort.Persistence;
using MetroSort.Purchases;
using MetroSort.Puzzle;
using UnityEngine;
using UnityEngine.UI;

namespace MetroSort.Game
{
    /// <summary>
    /// Builds and drives every screen: main menu, gameplay HUD, settings/pause, level complete, level failed,
    /// shop and level select. Portrait-first, reference resolution 1080x1920, laid out inside the safe area.
    /// It only reads game state and forwards button presses; it contains no puzzle rules.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private static readonly string[] BoosterNames = { "Undo", "Hint", "+Platform", "Shuffle" };
        private static readonly Color[] BoosterColors =
        {
            new Color(0.22f, 0.62f, 0.95f), new Color(0.96f, 0.62f, 0.20f), new Color(0.30f, 0.78f, 0.47f), new Color(0.62f, 0.42f, 0.86f)
        };

        private GameApp _app;
        private RectTransform _canvasRect, _root, _labelLayer, _pointer;
        private GameObject _menu, _hud, _settings, _win, _lose, _shop, _levels, _tutorialBanner;
        private Text _menuCoins, _menuLevel, _hudCoins, _hudTitle, _hudProgress, _tutorialText, _toastText;
        private Text _winCoins, _loseTitle, _loseReason, _shopCoins;
        private Button _dailyButton, _winAdButton, _loseUndo, _losePlatform, _removeAdsButton;
        private Button _assistToggle, _motionToggle, _fastToggle, _languageToggle, _resumeButton, _restartButton, _quitButton, _restoreButton, _closeSettingsButton;
        private readonly Button[] _boosterButtons = new Button[4];
        private readonly Text[] _boosterBadges = new Text[4];
        private readonly Button[] _shopBoosterButtons = new Button[4];
        private readonly Image[] _winStars = new Image[3];
        private readonly List<Text> _labels = new List<Text>();
        private CanvasGroup _toastGroup;
        private RectTransform _levelsGrid;
        private Text _levelsPageLabel;
        private Button _levelsPrev, _levelsNext;
        private int _levelsPage;
        private const int LevelsPerPage = Chapters.Size;
        private float _toastUntil, _secretTime;
        private int _secretTaps;
        private bool _pointerOn, _settingsInGame;
        private Vector3 _pointerWorld;

        // ------------------------------------------------------------------ construction

        public void Init(GameApp app)
        {
            _app = app;
            Canvas canvas = UiKit.CreateCanvas(transform);
            _canvasRect = (RectTransform)canvas.transform;
            // Catches taps and drags on the station; sits behind every other UI element.
            Image catcher = UiKit.MakeImage(canvas.transform, "InputCatcher", null, new Color(0f, 0f, 0f, 0f), true);
            UiKit.Stretch(catcher.rectTransform);
            catcher.gameObject.AddComponent<InputCatcher>().App = app;
            _labelLayer = UiKit.Stretch(UiKit.MakeRect("WorldLabels", canvas.transform));
            _root = UiKit.Stretch(UiKit.MakeRect("SafeArea", canvas.transform));
            _root.gameObject.AddComponent<SafeArea>();

            BuildHud();
            BuildMenu();
            BuildPointer(canvas.transform);
            BuildSettings();
            BuildShop();
            BuildLevels();
            BuildWin();
            BuildLose();
            BuildToast();

            GameServices s = app.Services;
            app.States.Changed += OnStateChanged;
            s.Economy.Changed += Refresh;
            s.Boosters.Changed += Refresh;
            s.Store.Changed += Refresh;
            s.Achievements.Unlocked += OnAchievement;
            app.Game.BoardChanged += Refresh;
            app.Game.LevelWon += ShowWin;
            app.Game.LevelLost += ShowLose;
            app.Game.Message += Toast;
            OnStateChanged(app.States.Current);
        }

        private void OnAchievement(AchievementDefinition def)
        {
            _app.Audio.Play(Sfx.Achievement);
            Toast(Loc.F("Achievement: {0}  +{1} coins", Loc.T(def.Title), def.Reward));
        }

        /// <summary>Detaches from game events; call before destroying this UI (for example to rebuild it in another language).</summary>
        public void Unsubscribe()
        {
            GameServices s = _app.Services;
            _app.States.Changed -= OnStateChanged;
            s.Economy.Changed -= Refresh;
            s.Boosters.Changed -= Refresh;
            s.Store.Changed -= Refresh;
            s.Achievements.Unlocked -= OnAchievement;
            _app.Game.BoardChanged -= Refresh;
            _app.Game.LevelWon -= ShowWin;
            _app.Game.LevelLost -= ShowLose;
            _app.Game.Message -= Toast;
        }

        private Button Btn(Transform parent, string label, Color color, Action onClick, float w, float h, int font = 46)
        {
            return UiKit.MakeButton(parent, label, color, () => { _app.Audio.Play(Sfx.Click); onClick(); }, w, h, font);
        }

        private Text CoinPill(Transform parent, float ax, float ay, float x, float y)
        {
            Image pill = UiKit.MakeImage(parent, "Coins", UiKit.Rounded, new Color(1f, 1f, 1f, 0.95f));
            UiKit.Place(pill.rectTransform, ax, ay, x, y, 250f, 92f);
            Image coin = UiKit.MakeImage(pill.transform, "Coin", UiKit.Circle, Palette.Gold);
            UiKit.Place(coin.rectTransform, 0f, 0.5f, 16f, 0f, 60f, 60f);
            Text text = UiKit.MakeText(pill.transform, "0", 44, Palette.UiInk, TextAnchor.MiddleRight);
            UiKit.Stretch(text.rectTransform, 84f, 0f, 24f, 0f);
            return text;
        }

        private void BuildMenu()
        {
            RectTransform menu = UiKit.Stretch(UiKit.MakeRect("Menu", _root));
            _menu = menu.gameObject;

            Image plate = UiKit.MakeImage(menu, "TitlePlate", UiKit.Rounded, new Color(1f, 1f, 1f, 0.94f));
            UiKit.Place(plate.rectTransform, 0.5f, 1f, 0f, -190f, 880f, 300f);
            // Tester shortcut: seven quick taps on the title unlock every level. Remove before a public release.
            plate.raycastTarget = true;
            var secret = plate.gameObject.AddComponent<Button>();
            secret.transition = Selectable.Transition.None;
            secret.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - _secretTime > 1.5f) _secretTaps = 0;
                _secretTime = Time.unscaledTime;
                if (++_secretTaps < 7) return;
                _secretTaps = 0;
                _app.Services.Save.Data.HighestUnlocked = 9999;
                _app.Services.Save.Save();
                _app.Audio.Play(Sfx.Achievement);
                Refresh();
                Toast("All levels unlocked for testing");
            });
            Text title = UiKit.MakeText(plate.transform, "METRO SORT", 108, Palette.UiInk);
            UiKit.Stretch(title.rectTransform, 20f, 90f, 20f, 20f);
            for (int i = 0; i < Palette.Lines.Length; i++)
            {
                Image dot = UiKit.MakeImage(plate.transform, "Line" + i, UiKit.Circle, Palette.Lines[i]);
                UiKit.Place(dot.rectTransform, 0.5f, 0f, (i - 2.5f) * 76f, 62f, 46f, 46f);
                dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            Button settings = Btn(menu, "Settings", Palette.UiInk, () => OpenSettings(false), 250f, 92f, 38);
            UiKit.Place((RectTransform)settings.transform, 0f, 1f, 30f, -40f, 250f, 92f);
            _menuCoins = CoinPill(menu, 1f, 1f, -30f, -40f);

            Image levelPill = UiKit.MakeImage(menu, "LevelPill", UiKit.Rounded, new Color(1f, 1f, 1f, 0.94f));
            UiKit.Place(levelPill.rectTransform, 0.5f, 0f, 0f, 545f, 760f, 84f);
            _menuLevel = UiKit.MakeText(levelPill.transform, "", 40, Palette.UiInk);
            UiKit.Stretch(_menuLevel.rectTransform, 16f, 0f, 16f, 0f);

            Button play = Btn(menu, "PLAY", Palette.UiGreen, OnPlay, 640f, 190f, 92);
            UiKit.Place((RectTransform)play.transform, 0.5f, 0f, 0f, 330f, 640f, 190f);

            Button levels = Btn(menu, "Levels", Palette.UiPrimary, OpenLevels, 300f, 130f);
            UiKit.Place((RectTransform)levels.transform, 0.5f, 0f, -330f, 150f, 300f, 130f);
            Button shop = Btn(menu, "Shop", Palette.UiPrimary, OpenShop, 300f, 130f);
            UiKit.Place((RectTransform)shop.transform, 0.5f, 0f, 0f, 150f, 300f, 130f);
            _dailyButton = Btn(menu, "Daily Gift", new Color(0.96f, 0.62f, 0.20f), OnDaily, 300f, 130f);
            UiKit.Place((RectTransform)_dailyButton.transform, 0.5f, 0f, 330f, 150f, 300f, 130f);
        }

        private void BuildHud()
        {
            RectTransform hud = UiKit.Stretch(UiKit.MakeRect("Hud", _root));
            _hud = hud.gameObject;

            Image bar = UiKit.MakeImage(hud, "TopBar", UiKit.Rounded, new Color(1f, 1f, 1f, 0.94f));
            UiKit.Place(bar.rectTransform, 0.5f, 1f, 0f, -30f, 1020f, 170f);
            Button pause = Btn(bar.transform, "II", Palette.UiInk, OnPause, 110f, 110f, 54);
            UiKit.Place((RectTransform)pause.transform, 0f, 0.5f, 26f, 0f, 110f, 110f);
            _hudTitle = UiKit.MakeText(bar.transform, "", 50, Palette.UiInk);
            UiKit.Place(_hudTitle.rectTransform, 0.5f, 1f, -20f, -14f, 560f, 72f);
            _hudProgress = UiKit.MakeText(bar.transform, "", 36, new Color(0.38f, 0.42f, 0.52f));
            UiKit.Place(_hudProgress.rectTransform, 0.5f, 0f, -20f, 16f, 600f, 60f);
            _hudCoins = CoinPill(bar.transform, 1f, 0.5f, -20f, 0f);
            ((RectTransform)_hudCoins.transform.parent).sizeDelta = new Vector2(210f, 86f);

            Action[] actions = { () => _app.Game.UseUndo(), () => _app.Game.UseHint(), () => _app.Game.UseExtraPlatform(), () => _app.Game.UseShuffle() };
            for (int i = 0; i < 4; i++)
            {
                Action action = actions[i];
                // Booster buttons play their own success/failure sounds, so no generic click here.
                Button b = UiKit.MakeButton(hud, BoosterNames[i], BoosterColors[i], () => action(), 235f, 170f, 42);
                UiKit.Place((RectTransform)b.transform, 0.5f, 0f, (i - 1.5f) * 255f, 40f, 235f, 170f);
                UiKit.Stretch(UiKit.LabelOf(b).rectTransform, 10f, 66f, 10f, 12f);
                Image badge = UiKit.MakeImage(b.transform, "Badge", UiKit.Rounded, new Color(0f, 0f, 0f, 0.22f));
                UiKit.Stretch(badge.rectTransform, 22f, 14f, 22f, 106f);
                _boosterBadges[i] = UiKit.MakeText(badge.transform, "", 32, Color.white);
                UiKit.Stretch(_boosterBadges[i].rectTransform);
                _boosterButtons[i] = b;
            }

            Image banner = UiKit.MakeImage(hud, "Tutorial", UiKit.Rounded, new Color(1f, 1f, 1f, 0.96f));
            UiKit.Place(banner.rectTransform, 0.5f, 0f, 0f, 232f, 990f, 116f);
            _tutorialText = UiKit.MakeText(banner.transform, "", 38, Palette.UiInk);
            UiKit.Stretch(_tutorialText.rectTransform, 26f, 8f, 26f, 8f);
            _tutorialText.resizeTextForBestFit = true;
            _tutorialText.resizeTextMinSize = 22;
            _tutorialText.resizeTextMaxSize = 38;
            _tutorialBanner = banner.gameObject;
            _tutorialBanner.SetActive(false);
        }

        private void BuildPointer(Transform canvas)
        {
            Image ring = UiKit.MakeImage(canvas, "Pointer", UiKit.Circle, new Color(1f, 0.86f, 0.25f, 0.85f));
            _pointer = UiKit.Place(ring.rectTransform, 0.5f, 0.5f, 0f, 0f, 96f, 96f);
            Image core = UiKit.MakeImage(ring.transform, "Core", UiKit.Circle, Color.white);
            UiKit.Place(core.rectTransform, 0.5f, 0.5f, 0f, 0f, 40f, 40f);
            _pointer.gameObject.SetActive(false);
        }

        private Button Row(RectTransform content, string label, Color color, Action onClick, float height = 112f, int font = 42)
        {
            return Btn(content, label, color, onClick, 780f, height, font);
        }

        private void BuildSettings()
        {
            RectTransform content;
            _settings = UiKit.Modal(_root, "Settings", "Settings", out content);
            SaveData data = _app.Services.Save.Data;
            AddSlider(content, "Music", data.MusicVolume, v => { data.MusicVolume = v; _app.Audio.MusicVolume = v; });
            AddSlider(content, "Sound", data.SfxVolume, v => { data.SfxVolume = v; _app.Audio.SfxVolume = v; });
            _assistToggle = Row(content, "", Palette.UiPrimary, () => { data.AssistedMode = !data.AssistedMode; ApplySettings(); }, 100f, 38);
            _motionToggle = Row(content, "", Palette.UiPrimary, () => { data.ReducedMotion = !data.ReducedMotion; ApplySettings(); }, 100f, 38);
            _fastToggle = Row(content, "", Palette.UiPrimary, () => { data.FastTrains = !data.FastTrains; ApplySettings(); }, 100f, 38);
            _languageToggle = Row(content, "", Palette.UiPrimary, () =>
            {
                data.Language = Loc.Language == Loc.Turkish ? Loc.English : Loc.Turkish;
                _app.Services.Save.Save();
                _app.ChangeLanguage(data.Language);
            }, 100f, 38);
            _resumeButton = Row(content, "Resume", Palette.UiGreen, CloseSettings, 130f, 52);
            _restartButton = Row(content, "Restart Level", Palette.UiInk, () => { _settings.SetActive(false); _app.Game.Restart(); });
            _quitButton = Row(content, "Back to Menu", Palette.UiRed, () => { _settings.SetActive(false); _app.GoToMenu(); });
            _restoreButton = Row(content, "Restore Purchases", Palette.UiInk, () => _app.Services.Store.Restore(ok => Toast(ok ? "Purchases restored" : "Nothing to restore")));
            _closeSettingsButton = Row(content, "Close", Palette.UiGreen, CloseSettings, 130f, 52);
        }

        private void AddSlider(RectTransform content, string label, float value, Action<float> onChanged)
        {
            RectTransform row = UiKit.MakeRect(label + "Row", content);
            row.sizeDelta = new Vector2(780f, 84f);
            Text text = UiKit.MakeText(row, label, 42, Palette.UiInk, TextAnchor.MiddleLeft);
            UiKit.Place(text.rectTransform, 0f, 0.5f, 0f, 0f, 200f, 80f);
            Slider slider = UiKit.MakeSlider(row, value, v => onChanged(v), 560f);
            UiKit.Place((RectTransform)slider.transform, 1f, 0.5f, 0f, 0f, 560f, 70f);
        }

        private void BuildShop()
        {
            RectTransform content;
            _shop = UiKit.Modal(_root, "Shop", "Shop", out content);
            _shopCoins = UiKit.MakeText(content, "", 46, Palette.UiInk);
            _shopCoins.rectTransform.sizeDelta = new Vector2(780f, 64f);
            GameServices s = _app.Services;
            for (int i = 0; i < 4; i++)
            {
                var type = (BoosterType)i;
                _shopBoosterButtons[i] = Row(content, "", BoosterColors[i], () =>
                {
                    if (s.Boosters.Buy(type)) _app.Audio.Play(Sfx.Coins);
                    else Toast("Not enough coins");
                }, 100f, 38);
            }
            Row(content, Loc.F("Watch an ad: +{0} coins (optional)", s.Config.RewardedAdCoins), new Color(0.96f, 0.62f, 0.20f), WatchAdForCoins, 100f, 38);
            foreach (StoreProduct product in StoreCatalog.Products)
            {
                StoreProduct p = product;
                string label = p.Consumable ? Loc.T(p.Title) + "  +" + p.Coins + "   " + p.PriceLabel : Loc.T(p.Title) + "   " + p.PriceLabel;
                Button b = Row(content, label, Palette.UiInk, () => s.Store.Buy(p.Id, ok =>
                {
                    if (ok) _app.Audio.Play(Sfx.Coins);
                    Toast(ok ? Loc.F("Thank you! {0} added", Loc.T(p.Title)) : "Purchase was not completed");
                }), 100f, 38);
                if (!p.Consumable) _removeAdsButton = b;
            }
            Row(content, "Close", Palette.UiGreen, () => _shop.SetActive(false), 120f, 50);
        }

        private void BuildLevels()
        {
            RectTransform content;
            _levels = UiKit.Modal(_root, "Levels", "Levels", out content);
            _levelsGrid = UiKit.MakeRect("Grid", content);
            var grid = _levelsGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(140f, 140f);
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            _levelsGrid.sizeDelta = new Vector2(780f, 4 * 154f);

            // Page controls: 36 levels per page.
            RectTransform pager = UiKit.MakeRect("Pager", content);
            pager.sizeDelta = new Vector2(780f, 104f);
            _levelsPrev = Btn(pager, "<", Palette.UiPrimary, () => ShowLevelsPage(_levelsPage - 1), 180f, 100f, 60);
            UiKit.Place((RectTransform)_levelsPrev.transform, 0f, 0.5f, 0f, 0f, 180f, 100f);
            _levelsNext = Btn(pager, ">", Palette.UiPrimary, () => ShowLevelsPage(_levelsPage + 1), 180f, 100f, 60);
            UiKit.Place((RectTransform)_levelsNext.transform, 1f, 0.5f, 0f, 0f, 180f, 100f);
            _levelsPageLabel = UiKit.MakeText(pager, "", 40, Palette.UiInk);
            UiKit.Place(_levelsPageLabel.rectTransform, 0.5f, 0.5f, 0f, 0f, 400f, 96f);
            _levelsPageLabel.resizeTextForBestFit = true;
            _levelsPageLabel.resizeTextMinSize = 22;
            _levelsPageLabel.resizeTextMaxSize = 40;
            Row(content, "Close", Palette.UiGreen, () => _levels.SetActive(false), 120f, 50);
        }

        private void BuildWin()
        {
            RectTransform content;
            _win = UiKit.Modal(_root, "LevelComplete", "Level Complete!", out content);
            RectTransform stars = UiKit.MakeRect("Stars", content);
            stars.sizeDelta = new Vector2(780f, 180f);
            for (int i = 0; i < 3; i++)
            {
                _winStars[i] = UiKit.MakeImage(stars, "Star" + i, UiKit.Star, Palette.Gold);
                UiKit.Place(_winStars[i].rectTransform, 0.5f, 0.5f, (i - 1) * 200f, i == 1 ? 14f : 0f, 170f, 170f);
            }
            _winCoins = UiKit.MakeText(content, "", 52, Palette.UiInk);
            _winCoins.rectTransform.sizeDelta = new Vector2(780f, 76f);
            Row(content, "Continue", Palette.UiGreen, OnContinue, 150f, 60);
            _winAdButton = Row(content, Loc.F("Watch an ad: +{0} coins (optional)", _app.Services.Config.RewardedAdCoins), new Color(0.96f, 0.62f, 0.20f), () =>
            {
                _winAdButton.gameObject.SetActive(false);
                WatchAdForCoins();
            }, 100f, 36);
            Row(content, "Menu", Palette.UiInk, () => _app.GoToMenu(), 100f, 40);
        }

        private void BuildLose()
        {
            RectTransform content;
            _lose = UiKit.Modal(_root, "LevelFailed", "", out content);
            _loseTitle = UiKit.MakeText(content, "", 66, Palette.UiInk);
            _loseTitle.rectTransform.sizeDelta = new Vector2(800f, 96f);
            _loseReason = UiKit.MakeText(content, "", 40, new Color(0.38f, 0.42f, 0.52f), TextAnchor.MiddleCenter, FontStyle.Normal);
            _loseReason.rectTransform.sizeDelta = new Vector2(780f, 130f);
            _loseUndo = UiKit.MakeButton(content, "", BoosterColors[0], () => _app.Game.UseUndo(), 780f, 120f, 42);
            _losePlatform = UiKit.MakeButton(content, "", BoosterColors[2], () => _app.Game.UseExtraPlatform(), 780f, 120f, 42);
            Row(content, "Try Again", Palette.UiGreen, () => _app.Game.Restart(), 140f, 56);
            Row(content, "Menu", Palette.UiInk, () => _app.GoToMenu(), 100f, 40);
        }

        private void BuildToast()
        {
            Image toast = UiKit.MakeImage(_root, "Toast", UiKit.Rounded, new Color(0.13f, 0.16f, 0.24f, 0.94f));
            UiKit.Place(toast.rectTransform, 0.5f, 1f, 0f, -225f, 940f, 96f);
            _toastText = UiKit.MakeText(toast.transform, "", 36, Color.white);
            UiKit.Stretch(_toastText.rectTransform, 24f, 6f, 24f, 6f);
            _toastText.resizeTextForBestFit = true;
            _toastText.resizeTextMinSize = 20;
            _toastText.resizeTextMaxSize = 36;
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;
        }

        // ------------------------------------------------------------------ state

        private void OnStateChanged(AppState state)
        {
            bool inLevel = state != AppState.Menu && state != AppState.Boot;
            _menu.SetActive(state == AppState.Menu);
            _hud.SetActive(inLevel);
            _win.SetActive(state == AppState.LevelComplete);
            _lose.SetActive(state == AppState.LevelFailed);
            if (state == AppState.Playing || (state != AppState.Paused && _settingsInGame)) _settings.SetActive(false);
            if (state != AppState.Menu)
            {
                _shop.SetActive(false);
                _levels.SetActive(false);
            }
            _labelLayer.gameObject.SetActive(inLevel);
            Refresh();
        }

        /// <summary>Re-reads everything shown on screen. Cheap enough to call on any change.</summary>
        public void Refresh()
        {
            if (_app == null || _menu == null) return;
            GameServices s = _app.Services;
            string coins = s.Economy.Coins.ToString();
            _menuCoins.text = coins;
            _hudCoins.text = coins;
            _shopCoins.text = Loc.F("You have {0} coins", coins);

            int current = _app.CurrentLevelId;
            LevelData next = s.Levels.Get(current);
            _menuLevel.text = Loc.F("Level {0}  -  {1}", next.Id, Loc.LevelName(next.Name));
            _dailyButton.gameObject.SetActive(s.Economy.CanClaimDaily(EconomyManager.TodayNumber(DateTime.UtcNow)));

            for (int i = 0; i < 4; i++)
            {
                var type = (BoosterType)i;
                int count = s.Boosters.Count(type);
                // Owned charges are shown as a count; otherwise the coin price is shown so paid use is never a surprise.
                _boosterBadges[i].text = count > 0 ? "x" + count : Loc.F("{0} coins", s.Boosters.Cost(type));
                _boosterBadges[i].color = count > 0 ? Color.white : new Color(1f, 0.92f, 0.55f);
                UiKit.LabelOf(_shopBoosterButtons[i]).text = Loc.F("{0}  (you have {1})   buy 1 for {2}", Loc.T(BoosterNames[i]), count, s.Boosters.Cost(type));
            }
            UiKit.LabelOf(_removeAdsButton).text = s.Store.AdsRemoved ? Loc.T("Ads removed - thank you!") : Loc.F("Remove Ads   {0}", StoreCatalog.Find(StoreCatalog.RemoveAds).PriceLabel);
            _removeAdsButton.interactable = !s.Store.AdsRemoved;

            GameController game = _app.Game;
            if (game.Board == null) return;
            BoardState state = game.Board.State;
            _hudTitle.text = Loc.F("Level {0}", game.Level.Id);
            string progress = Loc.F("Boarded {0} / {1}", state.Boarded, state.Total);
            if (state.StageCount > 1) progress += Loc.F("   Wave {0}/{1}", state.Stage + 1, state.StageCount);
            if (state.MoveLimit > 0) progress += Loc.F("   Moves left {0}", state.MovesRemaining);
            _hudProgress.text = progress;

            bool playing = _app.States.Current == AppState.Playing && !game.IsThinking;
            _boosterButtons[0].interactable = playing && game.Board.CanUndo && !_app.Tutorial.IsGuiding;
            _boosterButtons[1].interactable = playing;
            _boosterButtons[2].interactable = playing && game.Board.CanAddExtraPlatform;
            _boosterButtons[3].interactable = playing && !_app.Tutorial.IsGuiding;

            int undoCount = s.Boosters.Count(BoosterType.Undo), platformCount = s.Boosters.Count(BoosterType.ExtraPlatform);
            UiKit.LabelOf(_loseUndo).text = Loc.F("Undo last move  ({0})", undoCount > 0 ? "x" + undoCount : Loc.F("{0} coins", s.Boosters.Cost(BoosterType.Undo)));
            UiKit.LabelOf(_losePlatform).text = Loc.F("Add a platform  ({0})", platformCount > 0 ? "x" + platformCount : Loc.F("{0} coins", s.Boosters.Cost(BoosterType.ExtraPlatform)));
            _loseUndo.gameObject.SetActive(game.Board.CanUndo);
            _losePlatform.gameObject.SetActive(game.Board.LoseReason == LoseReason.Deadlock && game.Board.CanAddExtraPlatform);
        }

        private void ApplySettings()
        {
            SaveData data = _app.Services.Save.Data;
            GameSettings.AssistedMode = data.AssistedMode;
            GameSettings.ReducedMotion = data.ReducedMotion;
            GameSettings.FastTrains = data.FastTrains;
            UiKit.LabelOf(_assistToggle).text = Loc.F("Assisted taps (auto-pick platform): {0}", Loc.T(data.AssistedMode ? "ON" : "OFF"));
            UiKit.LabelOf(_motionToggle).text = Loc.F("Reduced motion: {0}", Loc.T(data.ReducedMotion ? "ON" : "OFF"));
            UiKit.LabelOf(_fastToggle).text = Loc.F("Fast trains: {0}", Loc.T(data.FastTrains ? "ON" : "OFF"));
            UiKit.LabelOf(_languageToggle).text = Loc.F("Language: {0}", Loc.Language == Loc.Turkish ? "Türkçe" : "English");
            _app.Services.Save.Save();
        }

        // ------------------------------------------------------------------ actions

        private void OnPlay() { _app.Game.StartLevel(_app.CurrentLevelId); }

        private void OnPause()
        {
            // Pausing is refused once the final move is in: the win/fail screen is about to appear.
            if (_app.States.Current != AppState.Playing || _app.Game.Board == null || _app.Game.Board.Status != GameStatus.Playing) return;
            _app.States.Set(AppState.Paused);
            OpenSettings(true);
        }

        public void OpenSettings(bool inGame)
        {
            _settingsInGame = inGame;
            _resumeButton.gameObject.SetActive(inGame);
            _restartButton.gameObject.SetActive(inGame);
            _quitButton.gameObject.SetActive(inGame);
            _restoreButton.gameObject.SetActive(!inGame);
            _closeSettingsButton.gameObject.SetActive(!inGame);
            ApplySettings();
            _settings.SetActive(true);
        }

        private void CloseSettings()
        {
            _settings.SetActive(false);
            _app.Services.Save.Save();
            if (_settingsInGame && _app.States.Current == AppState.Paused) _app.States.Set(AppState.Playing);
        }

        private void OpenShop()
        {
            Refresh();
            _shop.SetActive(true);
        }

        public void OpenLevels()
        {
            // Open on the page that contains the level the player is on.
            ShowLevelsPage((_app.CurrentLevelId - 1) / LevelsPerPage);
            _levels.SetActive(true);
        }

        private void ShowLevelsPage(int page)
        {
            GameServices s = _app.Services;
            int count = s.Levels.Count;
            int pages = Mathf.Max(1, (count + LevelsPerPage - 1) / LevelsPerPage);
            _levelsPage = Mathf.Clamp(page, 0, pages - 1);
            for (int i = _levelsGrid.childCount - 1; i >= 0; i--) Destroy(_levelsGrid.GetChild(i).gameObject);
            int first = _levelsPage * LevelsPerPage + 1;
            int last = Mathf.Min(count, first + LevelsPerPage - 1);
            for (int id = first; id <= last; id++)
            {
                int levelId = id;
                bool unlocked = s.Progression.IsUnlocked(id);
                int stars = s.Progression.GetStars(id);
                Color color = !unlocked ? new Color(0.70f, 0.73f, 0.78f) : (stars > 0 ? Palette.UiGreen : Palette.UiPrimary);
                string label = id + "\n" + (stars > 0 ? new string('*', stars) : (unlocked ? " " : Loc.T("locked")));
                Button b = Btn(_levelsGrid, label, color, () => { _levels.SetActive(false); _app.Game.StartLevel(levelId); }, 140f, 140f, 38);
                b.interactable = unlocked;
            }
            // One page per chapter (metro line).
            _levelsPageLabel.text = Loc.F("{0}  ({1}-{2})", Loc.T(Chapters.Names[Mathf.Min(_levelsPage, Chapters.Names.Length - 1)]), first, last);
            _levelsPrev.interactable = _levelsPage > 0;
            _levelsNext.interactable = _levelsPage < pages - 1;
        }

        private void OnDaily()
        {
            int granted = _app.Services.Economy.ClaimDaily(EconomyManager.TodayNumber(DateTime.UtcNow));
            if (granted <= 0) return;
            _app.Audio.Play(Sfx.Coins);
            Toast(Loc.F("Daily gift: +{0} coins", granted));
        }

        private void WatchAdForCoins()
        {
            _app.Services.AdsCoordinator.RequestRewardedCoins(ok =>
            {
                if (ok) _app.Audio.Play(Sfx.Coins);
                Toast(ok ? Loc.F("+{0} coins", _app.Services.Config.RewardedAdCoins) : "No ad available right now");
            });
        }

        private void OnContinue()
        {
            int finished = _app.Game.Level.Id;
            // Interstitials are only ever considered here, between levels, never during a puzzle.
            _app.Services.AdsCoordinator.OnLevelTransition(finished, () =>
            {
                if (_app.Services.Levels.Has(finished + 1)) _app.Game.StartLevel(finished + 1);
                else
                {
                    _app.GoToMenu();
                    Toast("You cleared every station. More are on the way!");
                }
            });
        }

        // ------------------------------------------------------------------ results

        private void ShowWin(LevelResult result)
        {
            _winCoins.text = Loc.F("+{0} coins", result.CoinsAwarded);
            _winAdButton.gameObject.SetActive(_app.Services.AdsCoordinator.IsRewardedReady);
            StopAllCoroutines();
            StartCoroutine(PopStars(result.Stars));
        }

        private IEnumerator PopStars(int earned)
        {
            for (int i = 0; i < 3; i++)
            {
                _winStars[i].color = i < earned ? Palette.Gold : new Color(0.84f, 0.86f, 0.90f);
                _winStars[i].rectTransform.localScale = GameSettings.ReducedMotion ? Vector3.one : Vector3.zero;
            }
            if (GameSettings.ReducedMotion) yield break;
            for (int i = 0; i < 3; i++)
            {
                if (i < earned) _app.Audio.Play(Sfx.Coins);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
                {
                    _winStars[i].rectTransform.localScale = Vector3.one * Ease.OutBack(t);
                    yield return null;
                }
                _winStars[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void ShowLose(LoseReason reason)
        {
            _loseTitle.text = Loc.T(reason == LoseReason.OutOfMoves ? "Out of Moves" : "Station Jammed");
            _loseReason.text = Loc.T(reason == LoseReason.OutOfMoves
                ? "The move limit ran out before everyone boarded."
                : "No passenger can reach a platform. Undo a move or try again.");
            Refresh();
        }

        // ------------------------------------------------------------------ small widgets

        public void SetTutorialText(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            _tutorialBanner.SetActive(show);
            if (show) _tutorialText.text = Loc.T(text);
        }

        public void SetPointer(bool on, Vector3 world)
        {
            _pointerOn = on;
            _pointerWorld = world;
            if (_pointer.gameObject.activeSelf != on) _pointer.gameObject.SetActive(on);
        }

        public void Toast(string message)
        {
            _toastText.text = Loc.T(message);
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        private bool WorldToCanvas(Vector3 world, out Vector2 local)
        {
            Vector3 screen = _app.Station.Cam.WorldToScreenPoint(world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local);
        }

        private void LateUpdate()
        {
            if (_app == null) return;
            float remaining = _toastUntil - Time.unscaledTime;
            _toastGroup.alpha = Mathf.Clamp01(remaining / 0.3f);

            Vector2 local;
            if (_pointerOn && WorldToCanvas(_pointerWorld, out local))
            {
                float bob = GameSettings.ReducedMotion ? 0f : Mathf.Sin(Time.unscaledTime * 6f);
                _pointer.anchoredPosition = local + new Vector2(0f, bob * 10f);
                _pointer.localScale = Vector3.one * (1f + bob * 0.08f);
            }

            // Platform captions (lock condition, uses left, express) follow their platforms.
            int used = 0;
            if (_labelLayer.gameObject.activeSelf)
            {
                List<PlatformView> platforms = _app.Station.Platforms;
                for (int i = 0; i < platforms.Count; i++)
                {
                    string caption = platforms[i].Label;
                    if (string.IsNullOrEmpty(caption) || !WorldToCanvas(platforms[i].LabelAnchor, out local)) continue;
                    Text label = used < _labels.Count ? _labels[used] : NewLabel();
                    used++;
                    if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
                    if (label.text != caption) label.text = caption;
                    label.rectTransform.anchoredPosition = local;
                }
            }
            for (int i = used; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);
        }

        private Text NewLabel()
        {
            Text label = UiKit.MakeText(_labelLayer, "", 28, Palette.UiInk);
            UiKit.Place(label.rectTransform, 0.5f, 0.5f, 0f, 0f, 320f, 80f);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            _labels.Add(label);
            return label;
        }
    }
}
