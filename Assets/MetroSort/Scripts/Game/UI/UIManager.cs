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
        private static readonly string[] BoosterIcons = { "undo", "hint", "platform", "shuffle" };
        private static readonly Color[] BoosterColors =
        {
            new Color(0.22f, 0.62f, 0.95f), new Color(0.96f, 0.62f, 0.20f), new Color(0.30f, 0.78f, 0.47f), new Color(0.62f, 0.42f, 0.86f)
        };

        private GameApp _app;
        private RectTransform _canvasRect, _root, _labelLayer, _pointer;
        private GameObject _menuBackdrop, _menu, _hud, _settings, _win, _lose, _shop, _levels, _tutorialBanner;
        private Text _menuCoins, _menuLevel, _hudCoins, _hudTitle, _hudProgress, _tutorialText, _toastText;
        private Text _winCoins, _loseTitle, _loseReason, _shopCoins;
        private Button _dailyButton, _winAdButton, _loseUndo, _losePlatform, _removeAdsButton;
        private Button _resumeButton, _restartButton, _quitButton, _restoreButton;
        private readonly Button[] _boosterButtons = new Button[4];
        private readonly Text[] _boosterBadges = new Text[4];
        private readonly Button[] _shopBoosterButtons = new Button[4];
        private readonly Text[] _shopBoosterCounts = new Text[4];
        private ToggleSwitch _assistSwitch, _motionSwitch, _fastSwitch;
        private Text _languageLabel;
        private GameObject _settingsGameRow;
        private RectTransform _hudFill, _hudTrack;
        private float _hudFillTarget = 40f;
        // The boarded count on the HUD trails the puzzle: it goes up when the riders of a departing train land on
        // the bar (CollectBoarded), so the number and the action on the platform tell the same story.
        private int _boardedShown = -1, _boardedTarget, _collecting;
        private float _boardedChanged;
        private BoardState _progressState;
        private object _progressBoard;
        private Text _menuLives, _menuLivesTimer, _noLivesTimer, _noLivesCount;
        private GameObject _noLives;
        private Button _refillButton;
        private readonly Image[] _winStars = new Image[3];
        private readonly FlipBook[] _winStarSpins = new FlipBook[3];
        // Star chest row on the win card (see ProgressionManager.CompleteLevel).
        private RectTransform _chestFill, _chestIcon, _chestTrack;
        private Text _chestText;
        private const float ChestFillMin = 44f, ChestFillMax = 592f;
        private FlipBook _winCoinSpin;
        private readonly List<Text> _labels = new List<Text>();
        private CanvasGroup _toastGroup;
        private RectTransform _levelsGrid;
        private Text _levelsPageLabel, _levelsBannerName;
        private Image _levelsBanner;
        private Button _levelsPrev, _levelsNext;
        private int _levelsPage;
        private const int LevelsPerPage = Chapters.Size;
        private const int ChapterPictures = 35;
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
            BuildNoLives();
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

        /// <summary>The coin counter: a dark glassy pill with a big coin on its left and a green plus that opens the shop.</summary>
        private Text CoinPill(Transform parent, float ax, float ay, float x, float y)
        {
            RectTransform holder = UiKit.Place(UiKit.MakeRect("Coins", parent), ax, ay, x, y, 300f, 100f);
            Image pill = UiKit.MakeImage(holder, "Pill", UiPremium.Pill, new Color(0.10f, 0.20f, 0.45f, 0.92f));
            UiKit.Place(pill.rectTransform, 0.5f, 0.5f, 10f, 0f, 260f, 76f);
            Image coin = UiKit.AddIcon(holder, "coin", 104f, 0f, 0.5f, 40f, 2f);
            coin.gameObject.AddComponent<Bob>().Height = 3f;
            Text text = UiKit.MakeText(holder, "0", 46, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform, 86f, 8f, 66f, 4f);
            // Never wrap a number onto two lines; shrink it instead.
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = 46;
            UiKit.Emboss(text, new Color(0.03f, 0.08f, 0.22f, 0.95f));
            Button plus = UiPremium.MakeRoundButton(holder, UiPremium.Round, UiPremium.Green, () => { _app.Audio.Play(Sfx.Click); OpenShop(); }, 70f);
            UiKit.Place((RectTransform)plus.transform, 1f, 0.5f, -6f, 0f, 70f, 70f);
            Text p = UiKit.MakeText(plus.transform, "+", 58, Color.white);
            UiKit.Stretch(p.rectTransform, 0f, 6f, 0f, 0f);
            UiKit.Emboss(p, new Color(0.08f, 0.32f, 0.06f, 0.95f));
            return text;
        }

        private void BuildMenu()
        {
            RectTransform menu = UiKit.Stretch(UiKit.MakeRect("Menu", _root));
            _menu = menu.gameObject;

            // Key art fills the whole screen (not just the safe area) behind the menu, cropped to cover.
            Sprite keyArt = UiKit.Art("keyart");
            if (keyArt != null)
            {
                Image backdrop = UiKit.MakeImage(_root.parent, "MenuBackdrop", keyArt, Color.white);
                UiKit.Stretch(backdrop.rectTransform);
                var cover = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = keyArt.rect.width / keyArt.rect.height;
                backdrop.transform.SetSiblingIndex(_root.GetSiblingIndex());
                _menuBackdrop = backdrop.gameObject;
            }

            Sprite logo = UiKit.Art("logo");
            Image plate;
            if (logo != null)
            {
                plate = UiKit.MakeImage(menu, "TitlePlate", logo, Color.white);
                plate.preserveAspect = true;
                UiKit.Place(plate.rectTransform, 0.5f, 1f, 0f, -150f, 800f, 800f * logo.rect.height / logo.rect.width);
                plate.gameObject.AddComponent<Bob>();
            }
            else
            {
                plate = UiKit.MakeImage(menu, "TitlePlate", UiKit.ButtonSprite, Palette.UiPrimary);
                plate.pixelsPerUnitMultiplier = 0.6f;
                UiKit.Place(plate.rectTransform, 0.5f, 1f, 0f, -190f, 880f, 300f);
                Text title = UiKit.MakeText(plate.transform, "METRO SORT", 116, Color.white);
                UiKit.Stretch(title.rectTransform, 20f, 104f, 20f, 16f);
                UiKit.Emboss(title, new Color(0.06f, 0.20f, 0.46f, 0.95f));
                title.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
                Image strip = UiKit.MakeImage(plate.transform, "LineStrip", UiKit.Rounded, Color.white);
                UiKit.Place(strip.rectTransform, 0.5f, 0f, 0f, 44f, 520f, 76f);
                for (int i = 0; i < Palette.Lines.Length; i++)
                {
                    Image dot = UiKit.MakeImage(plate.transform, "Line" + i, UiKit.Circle, Palette.Lines[i]);
                    UiKit.Place(dot.rectTransform, 0.5f, 0f, (i - 2.5f) * 76f, 82f, 46f, 46f);
                    dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                }
            }
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

            Button settings = UiPremium.MakeRoundButton(menu, UiPremium.Round, UiPremium.Blue, () => { _app.Audio.Play(Sfx.Click); OpenSettings(false); }, 116f);
            settings.name = "Button_Settings";
            UiKit.Place((RectTransform)settings.transform, 0f, 1f, 30f, -34f, 116f, 116f);
            UiKit.AddIcon(settings.transform, "gear", 78f, 0.5f, 0.5f, 0f, 3f);
            _menuCoins = CoinPill(menu, 1f, 1f, -30f, -40f);
            BuildLivesPill(menu);

            Image levelPill = UiKit.MakeImage(menu, "LevelPill", UiKit.Card, Color.white);
            levelPill.pixelsPerUnitMultiplier = 1.5f;
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
            Button[] small = { levels, shop, _dailyButton };
            string[] smallIcons = { "levels", "shop", "gift" };
            for (int i = 0; i < small.Length; i++)
            {
                // Dedicated menu artwork (Resources/Art/menu_*) wins over the shared icon when it has been supplied.
                string menuArt = "menu_" + (i == 2 ? "daily" : smallIcons[i]);
                UiKit.AddIcon(small[i].transform, UiKit.Art(menuArt) != null ? menuArt : smallIcons[i], 104f, 0f, 0.5f, 62f, 6f);
                Text label = UiKit.LabelOf(small[i]);
                UiKit.Stretch(label.rectTransform, 112f, 16f, 14f, 6f);
                // Two-word labels wrap here; keep them small enough that both lines stay inside the button.
                label.resizeTextMaxSize = 36;
            }
        }

        private void BuildHud()
        {
            RectTransform hud = UiKit.Stretch(UiKit.MakeRect("Hud", _root));
            _hud = hud.gameObject;

            // Top bar: the same gold-rimmed navy board as the timetable below it.
            RectTransform bar = UiKit.Place(UiKit.MakeRect("TopBar", hud), 0.5f, 1f, 0f, -26f, 1024f, 158f);
            Image barRim = UiKit.MakeImage(bar, "Rim", UiPremium.Pill, new Color(1f, 0.74f, 0.20f));
            UiKit.Stretch(barRim.rectTransform);
            barRim.pixelsPerUnitMultiplier = 0.7f;
            var barShadow = barRim.gameObject.AddComponent<Shadow>();
            barShadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.45f);
            barShadow.effectDistance = new Vector2(0f, -8f);
            Image barBoard = UiKit.MakeImage(bar, "Board", UiPremium.Pill, new Color(0.10f, 0.17f, 0.38f, 0.97f));
            UiKit.Stretch(barBoard.rectTransform, 7f, 7f, 7f, 7f);
            barBoard.pixelsPerUnitMultiplier = 0.7f;
            Button pause = UiPremium.MakeRoundButton(bar, UiPremium.Round, UiPremium.Blue, () => { _app.Audio.Play(Sfx.Click); OnPause(); }, 118f);
            pause.name = "Button_Pause";
            UiKit.Place((RectTransform)pause.transform, 0f, 0.5f, 22f, 2f, 118f, 118f);
            UiKit.AddIcon(pause.transform, "pause", 74f, 0.5f, 0.5f, 0f, 3f);
            _hudTitle = UiPremium.Heading(bar, "", 48, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Place(_hudTitle.rectTransform, 0.5f, 1f, -40f, -10f, 520f, 66f);
            // Boarding progress: a gold bar filling as passengers board, the count written on it.
            RectTransform track = UiKit.Place(UiKit.MakeRect("Progress", bar), 0.5f, 0f, -40f, 22f, 480f, 46f);
            _hudTrack = track;
            Image trackBack = UiKit.MakeImage(track, "Track", UiPremium.Pill, new Color(0.03f, 0.06f, 0.16f, 0.95f));
            UiKit.Stretch(trackBack.rectTransform);
            Image fill = UiKit.MakeImage(track, "Fill", UiPremium.Pill, new Color(0.40f, 0.86f, 0.30f));
            _hudFill = UiKit.Place(fill.rectTransform, 0f, 0.5f, 4f, 0f, 40f, 38f);
            _hudProgress = UiPremium.Heading(track, "", 30, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Stretch(_hudProgress.rectTransform, 10f, 4f, 10f, 2f);
            _hudProgress.resizeTextForBestFit = true;
            _hudProgress.resizeTextMinSize = 16;
            _hudProgress.resizeTextMaxSize = 30;
            _hudCoins = CoinPill(bar, 1f, 0.5f, -20f, 0f);
            // No shop shortcut during a level: the plus would only crowd the counter on the bar.
            Transform hudPlus = _hudCoins.transform.parent.Find("RoundButton");
            if (hudPlus != null) hudPlus.gameObject.SetActive(false);
            UiKit.Stretch(_hudCoins.rectTransform, 86f, 8f, 20f, 4f);
            ((RectTransform)_hudCoins.transform.parent).sizeDelta = new Vector2(210f, 86f);
            BuildScheduleBar(hud);

            Action[] actions = { () => _app.Game.UseUndo(), () => _app.Game.UseHint(), () => _app.Game.UseExtraPlatform(), () => _app.Game.UseShuffle() };
            for (int i = 0; i < 4; i++)
            {
                Action action = actions[i];
                // Booster buttons play their own success/failure sounds, so no generic click here.
                Button b = UiKit.MakeButton(hud, BoosterNames[i], BoosterColors[i], () => action(), 235f, 170f, 42);
                UiKit.Place((RectTransform)b.transform, 0.5f, 0f, (i - 1.5f) * 255f, 40f, 235f, 170f);
                // The icon says what the booster is; its name stays on the object for tools and tests.
                UiKit.LabelOf(b).gameObject.SetActive(false);
                UiKit.AddIcon(b.transform, BoosterIcons[i], 112f, 0.5f, 0.5f, 0f, 26f);
                Image badge = UiKit.MakeImage(b.transform, "Badge", UiKit.Rounded, new Color(0f, 0f, 0f, 0.26f));
                UiKit.Stretch(badge.rectTransform, 22f, 20f, 22f, 104f);
                _boosterBadges[i] = UiKit.MakeText(badge.transform, "", 32, Color.white);
                UiKit.Stretch(_boosterBadges[i].rectTransform);
                _boosterButtons[i] = b;
            }

            Image banner = UiKit.MakeImage(hud, "Tutorial", UiKit.Card, Color.white);
            UiKit.Place(banner.rectTransform, 0.5f, 0f, 0f, 232f, 990f, 116f);
            _tutorialText = UiKit.MakeText(banner.transform, "", 38, Palette.UiInk);
            Sprite guide = UiKit.Art("conductor_point");
            if (guide != null)
            {
                Image img = UiKit.MakeImage(banner.transform, "Conductor", guide, Color.white);
                img.preserveAspect = true;
                UiKit.Place(img.rectTransform, 0f, 0f, 6f, 8f, 180f, 180f);
            }
            UiKit.Stretch(_tutorialText.rectTransform, guide != null ? 190f : 26f, 8f, 26f, 8f);
            _tutorialText.resizeTextForBestFit = true;
            _tutorialText.resizeTextMinSize = 22;
            _tutorialText.resizeTextMaxSize = 38;
            _tutorialBanner = banner.gameObject;
            _tutorialBanner.SetActive(false);
        }

        private NextTrainsStrip _nextTrains;

        private void BuildScheduleBar(RectTransform hud)
        {
            _nextTrains = NextTrainsStrip.Make(hud);
        }

        private void RefreshSchedule(BoardState state)
        {
            if (_nextTrains != null) _nextTrains.Show(state);
        }

        private void BuildPointer(Transform canvas)
        {
            Image ring = UiKit.MakeImage(canvas, "Pointer", UiKit.Circle, new Color(1f, 0.86f, 0.25f, 0.85f));
            _pointer = UiKit.Place(ring.rectTransform, 0.5f, 0.5f, 0f, 0f, 96f, 96f);
            Image core = UiKit.MakeImage(ring.transform, "Core", UiKit.Circle, Color.white);
            UiKit.Place(core.rectTransform, 0.5f, 0.5f, 0f, 0f, 40f, 40f);
            _pointer.gameObject.SetActive(false);
        }

        /// <summary>Puts artwork at the left end of a wide row button and moves its label over.</summary>
        private static void RowIcon(Button row, string icon)
        {
            if (UiKit.Icon(icon) == null) return;
            float h = ((RectTransform)row.transform).sizeDelta.y;
            UiKit.AddIcon(row.transform, icon, h * 0.86f, 0f, 0.5f, 62f, 5f);
            UiKit.Stretch(UiKit.LabelOf(row).rectTransform, 112f, Mathf.Min(16f, h * 0.12f), 14f, 6f);
        }

        /// <summary>The conductor standing on top of a modal card. Returns null when the artwork is missing.</summary>
        private static Image Mascot(RectTransform card, string pose, float size, float x, float y)
        {
            Sprite sprite = UiKit.Art("conductor_" + pose);
            if (sprite == null) return null;
            Image img = UiKit.MakeImage(card, "Conductor", sprite, Color.white);
            img.preserveAspect = true;
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            img.gameObject.AddComponent<Bob>().Height = 6f;
            return img;
        }

        private bool _hadHint;

        /// <summary>The conductor slides up from the bottom corner for a moment (a hint was found).</summary>
        private void MascotPop(string pose)
        {
            Sprite sprite = UiKit.Art("conductor_" + pose);
            if (sprite == null || GameSettings.ReducedMotion || !isActiveAndEnabled) return;
            _app.Audio.Play(Sfx.MascotPop);
            StartCoroutine(MascotPopRoutine(sprite));
        }

        private IEnumerator MascotPopRoutine(Sprite sprite)
        {
            Image img = UiKit.MakeImage(_root, "ConductorPop", sprite, Color.white);
            img.preserveAspect = true;
            _flying.Add(img.gameObject);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(300f, 300f);
            const float hidden = -320f, shown = 250f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.30f)
            {
                if (img == null) yield break;
                rt.anchoredPosition = new Vector2(12f, Mathf.LerpUnclamped(hidden, shown, Ease.OutBack(t)));
                yield return null;
            }
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                if (img == null) yield break;
                rt.anchoredPosition = new Vector2(12f, shown + Mathf.Sin(t * 6f) * 6f);
                yield return null;
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
            {
                if (img == null) yield break;
                rt.anchoredPosition = new Vector2(12f, Mathf.Lerp(shown, hidden, Ease.InCubic(t)));
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        /// <summary>
        /// A reward opens in the middle of the screen: the 3D box (gift_open / chest_open, Tools/art/rewardbox.py)
        /// wobbles, bursts open in a flash of light with rays behind it, the amount pops up, and the coins fly one
        /// by one into the coin counter, which counts up as they land. Falls back to a toast.
        /// </summary>
        private void RevealReward(string prefix, string message, int coins)
        {
            if (GameSettings.ReducedMotion || UiKit.Art(prefix + "_open") == null || !isActiveAndEnabled)
            {
                _app.Audio.Play(Sfx.Coins);
                Toast(message);
                return;
            }
            StartCoroutine(RevealRoutine(prefix, message, coins));
        }

        // While a reward counts up, the coin counters show this instead of the balance (-1: show the balance).
        private int _coinDisplay = -1;

        private Text VisibleCoinCounter()
        {
            if (_shop != null && _shop.activeSelf) return _shopCoins;
            if (_menu != null && _menu.activeSelf) return _menuCoins;
            return _hudCoins;
        }

        private IEnumerator RevealRoutine(string prefix, string message, int coins)
        {
            int balance = _app.Services.Economy.Coins;
            _coinDisplay = Mathf.Max(0, balance - coins);
            Refresh();

            Image dim = UiKit.MakeImage(_root, "RewardReveal", null, new Color(0.03f, 0.05f, 0.14f, 0f), true);
            UiKit.Stretch(dim.rectTransform, -40f, -40f, -40f, -40f);
            _flying.Add(dim.gameObject);
            Sprite raysArt = UiKit.Art("rays");
            Image rays = null;
            if (raysArt != null)
            {
                rays = UiKit.MakeImage(dim.transform, "Rays", raysArt, new Color(1f, 0.92f, 0.6f, 0f));
                rays.preserveAspect = true;
                UiKit.Place(rays.rectTransform, 0.5f, 0.5f, 0f, 160f, 1100f, 1100f);
                rays.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rays.gameObject.AddComponent<Spin>().DegreesPerSecond = 18f;
            }
            FlipBook box = UiKit.MakeFlipBook(dim.transform, prefix + "_open", 700f);
            RectTransform b = (RectTransform)box.transform;
            b.anchoredPosition = new Vector2(0f, 160f);
            Text amount = UiPremium.Heading(dim.transform, "+" + coins, 120, UiPremium.Gold, new Color(0.45f, 0.20f, 0.0f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 0.5f, 0f, -250f, 800f, 150f);
            amount.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            amount.color = new Color(1f, 1f, 1f, 0f);
            Text label = UiKit.MakeText(dim.transform, message, 48, new Color(1f, 1f, 1f, 0f));
            UiKit.Place(label.rectTransform, 0.5f, 0.5f, 0f, -370f, 960f, 90f);
            UiKit.Emboss(label, new Color(0.02f, 0.05f, 0.16f, 0.9f));
            Image guide = null;
            Sprite onlooker = UiKit.Art("conductor_surprised");
            if (onlooker != null)
            {
                guide = UiKit.MakeImage(dim.transform, "Conductor", onlooker, new Color(1f, 1f, 1f, 0f));
                guide.preserveAspect = true;
                UiKit.Place(guide.rectTransform, 0.5f, 0.5f, 330f, -40f, 300f, 300f);
            }
            Image flash = UiKit.MakeImage(dim.transform, "Flash", null, new Color(1f, 0.98f, 0.9f, 0f));
            UiKit.Stretch(flash.rectTransform);

            // Drop in, then the box rattles and bursts (the burst is frame 10 of 24 in the sheet).
            const float play = 1.5f, burstAt = play * 10f / 24f;
            _app.Audio.Play(Sfx.ChestOpen);
            box.PlayOnce(play);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < burstAt)
            {
                if (dim == null) yield break;
                float t = Time.unscaledTime - t0;
                dim.color = new Color(0.03f, 0.05f, 0.14f, Mathf.Min(1f, t * 5f) * 0.86f);
                b.localScale = Vector3.one * Ease.OutBack(Mathf.Min(1f, t / 0.3f));
                yield return null;
            }

            // Burst: flash, rays, the amount, the conductor.
            _app.Audio.Play(Sfx.Coins);
            Haptics.Success();
            if (guide != null) guide.sprite = UiKit.Art("conductor_cheer") ?? guide.sprite;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                if (dim == null) yield break;
                flash.color = new Color(1f, 0.98f, 0.9f, 0.85f * (1f - t) * (1f - t));
                if (rays != null) rays.color = new Color(1f, 0.92f, 0.6f, Mathf.Min(1f, t * 2f) * 0.9f);
                amount.color = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 3f));
                amount.rectTransform.localScale = Vector3.one * Ease.OutBack(Mathf.Min(1f, t * 1.6f));
                label.color = new Color(1f, 1f, 1f, t);
                if (guide != null) guide.color = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 2f));
                b.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.35f);
            if (dim == null) yield break;

            // The coins fly into the counter one by one and it counts up as they land.
            Text counter = VisibleCoinCounter();
            RectTransform counterPill = counter != null ? (RectTransform)counter.transform.parent : null;
            int flyers = coins > 0 ? Mathf.Clamp(coins / 10, 6, 14) : 0;
            int landed = 0, given = 0;
            Vector2 from = b.anchoredPosition;
            Vector2 to = counter != null ? (Vector2)dim.rectTransform.InverseTransformPoint(counter.transform.parent.position) : from;
            var flying = new List<RectTransform>();
            for (int i = 0; i < flyers; i++)
            {
                Image c = UiKit.AddIcon(dim.transform, "coin", 96f, 0.5f, 0.5f, from.x, from.y);
                flying.Add(c.rectTransform);
            }
            float start = Time.unscaledTime;
            const float flight = 0.6f, stagger = 0.07f;
            while (landed < flyers)
            {
                if (dim == null) { _coinDisplay = -1; Refresh(); yield break; }
                float now = Time.unscaledTime - start;
                // Let the counter show through as the coins head for it.
                dim.color = new Color(0.03f, 0.05f, 0.14f, Mathf.Lerp(0.86f, 0.35f, Mathf.Clamp01(now / 0.4f)));
                for (int i = 0; i < flying.Count; i++)
                {
                    RectTransform c = flying[i];
                    if (c == null) continue;
                    float k = (now - i * stagger) / flight;
                    if (k < 0f) continue;
                    if (k >= 1f)
                    {
                        Destroy(c.gameObject);
                        flying[i] = null;
                        landed++;
                        int share = landed == flyers ? coins - given : coins / flyers;
                        given += share;
                        _coinDisplay += share;
                        if (counter != null) counter.text = _coinDisplay.ToString();
                        if (counterPill != null) counterPill.localScale = Vector3.one * 1.15f;
                        _app.Audio.Play(Sfx.CoinTick);
                        continue;
                    }
                    // A curved path: out to the side and up, then into the counter, shrinking as it goes.
                    float e = k * k * (3f - 2f * k);
                    Vector2 control = from + new Vector2((i % 2 == 0 ? -1f : 1f) * (160f + 30f * (i % 4)), 220f);
                    Vector2 p = Vector2.Lerp(Vector2.Lerp(from, control, e), Vector2.Lerp(control, to, e), e);
                    c.anchoredPosition = p;
                    c.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.6f, e);
                    c.localRotation = Quaternion.Euler(0f, 0f, k * 360f * (i % 2 == 0 ? 1f : -1f));
                }
                if (counterPill != null) counterPill.localScale = Vector3.Lerp(counterPill.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
                yield return null;
            }
            _coinDisplay = -1;
            Refresh();
            if (counterPill != null) counterPill.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.6f);
            if (dim == null) yield break;
            var group = dim.gameObject.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                if (dim == null) yield break;
                group.alpha = 1f - t;
                yield return null;
            }
            if (dim != null) Destroy(dim.gameObject);
        }

        private Button Row(RectTransform content, string label, Color color, Action onClick, float height = 112f, int font = 42)
        {
            return Btn(content, label, color, onClick, 780f, height, font);
        }

        private void BuildSettings()
        {
            RectTransform body;
            _settings = UiPremium.Modal(_root, "Settings", "Settings", 940f, 1300f, () => { _app.Audio.Play(Sfx.Click); CloseSettings(); }, out body);
            SaveData data = _app.Services.Save.Data;
            float y = -10f;
            SliderRow(body, "Music", data.MusicVolume, v => { data.MusicVolume = v; _app.Audio.MusicVolume = v; }, ref y);
            SliderRow(body, "Sound", data.SfxVolume, v => { data.SfxVolume = v; _app.Audio.SfxVolume = v; }, ref y);
            _assistSwitch = SwitchRow(body, "Assisted taps", () => { data.AssistedMode = !data.AssistedMode; ApplySettings(); }, ref y);
            _motionSwitch = SwitchRow(body, "Reduced motion", () => { data.ReducedMotion = !data.ReducedMotion; ApplySettings(); }, ref y);
            _fastSwitch = SwitchRow(body, "Fast trains", () => { data.FastTrains = !data.FastTrains; ApplySettings(); }, ref y);

            // Language: the row's button names the current language and switches to the other one.
            Image lang = UiPremium.MakeTile(body, "LanguageRow", 0.5f, 1f, 0f, y, 872f, 118f);
            float langLeft = RowIconFor(lang.transform, "Language");
            Text langLabel = UiKit.MakeText(lang.transform, "Language", 42, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Stretch(langLabel.rectTransform, langLeft, 10f, 300f, 4f);
            Button langButton = Btn(lang.transform, "", UiPremium.Blue, () =>
            {
                data.Language = Loc.Language == Loc.Turkish ? Loc.English : Loc.Turkish;
                _app.Services.Save.Save();
                _app.ChangeLanguage(data.Language);
            }, 230f, 88f, 36);
            UiKit.Place((RectTransform)langButton.transform, 1f, 0.5f, -28f, 4f, 230f, 88f);
            _languageLabel = UiKit.LabelOf(langButton);
            y -= 136f;

            // In a level: resume, restart and leave. On the menu: restore purchases.
            RectTransform game = UiKit.Place(UiKit.MakeRect("GameButtons", body), 0.5f, 1f, 0f, y - 10f, 872f, 260f);
            _settingsGameRow = game.gameObject;
            _resumeButton = Btn(game, "Resume", UiPremium.Green, CloseSettings, 600f, 130f, 56);
            UiKit.Place((RectTransform)_resumeButton.transform, 0.5f, 1f, 0f, 0f, 600f, 130f);
            _restartButton = Btn(game, "Restart Level", UiPremium.Orange, () => { _settings.SetActive(false); LeaveLevel(RestartChecked); }, 420f, 110f, 40);
            UiKit.Place((RectTransform)_restartButton.transform, 0f, 0f, 0f, 0f, 420f, 110f);
            _quitButton = Btn(game, "Back to Menu", UiPremium.Red, () => { _settings.SetActive(false); LeaveLevel(_app.GoToMenu); }, 420f, 110f, 40);
            UiKit.Place((RectTransform)_quitButton.transform, 1f, 0f, 0f, 0f, 420f, 110f);
            _restoreButton = Btn(body, "Restore Purchases", UiPremium.Blue, () => _app.Services.Store.Restore(ok => Toast(ok ? "Purchases restored" : "Nothing to restore")), 620f, 110f, 40);
            UiKit.Place((RectTransform)_restoreButton.transform, 0.5f, 1f, 0f, y - 40f, 620f, 110f);
        }

        // Settings rows: label on the left, behind its icon when the artwork is there.
        private static readonly Dictionary<string, string> SettingIcons = new Dictionary<string, string>
        {
            { "Music", "icon_music" }, { "Sound", "icon_sound" }, { "Assisted taps", "icon_tap" },
            { "Reduced motion", "icon_motion" }, { "Fast trains", "icon_fast" }, { "Language", "icon_language" },
        };

        /// <summary>Adds the row's icon and returns where its label should start.</summary>
        private static float RowIconFor(Transform tile, string label)
        {
            string icon;
            if (!SettingIcons.TryGetValue(label, out icon) || UiKit.Art(icon) == null) return 36f;
            UiKit.AddIcon(tile, icon, 92f, 0f, 0.5f, 72f, 6f);
            return 128f;
        }

        private void SliderRow(RectTransform body, string label, float value, Action<float> onChanged, ref float y)
        {
            Image tile = UiPremium.MakeTile(body, label + "Row", 0.5f, 1f, 0f, y, 872f, 118f);
            float left = RowIconFor(tile.transform, label);
            Text text = UiKit.MakeText(tile.transform, label, 42, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(text.rectTransform, 0f, 0.5f, left, 4f, 200f, 90f);
            float width = 872f - left - 200f - 50f;
            Slider slider = UiKit.MakeSlider(tile.transform, value, v => onChanged(v), width);
            UiKit.Place((RectTransform)slider.transform, 1f, 0.5f, -36f, 4f, width, 70f);
            y -= 136f;
        }

        private ToggleSwitch SwitchRow(RectTransform body, string label, Action onClick, ref float y)
        {
            Image tile = UiPremium.MakeTile(body, label + "Row", 0.5f, 1f, 0f, y, 872f, 118f);
            float left = RowIconFor(tile.transform, label);
            Text text = UiKit.MakeText(tile.transform, label, 42, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Stretch(text.rectTransform, left, 10f, 230f, 4f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = 42;
            ToggleSwitch sw = ToggleSwitch.Make(tile.transform, () => { _app.Audio.Play(Sfx.Click); onClick(); });
            UiKit.Place((RectTransform)sw.transform, 1f, 0.5f, -36f, 4f, 170f, 84f);
            y -= 136f;
            return sw;
        }



        private void BuildShop()
        {
            RectTransform body;
            _shop = UiPremium.Modal(_root, "Shop", "Shop", 1000f, 1560f, () => { _app.Audio.Play(Sfx.Click); _shop.SetActive(false); }, out body);
            GameServices s = _app.Services;

            // Coin balance under the ribbon.
            _shopCoins = CoinPill(body, 0.5f, 1f, 0f, 0f);
            Transform plus = _shopCoins.transform.parent.Find("RoundButton");
            if (plus != null) plus.gameObject.SetActive(false);

            // Coin packs: big cards with the artwork on turning rays, the amount and a green price button.
            var packs = new List<StoreProduct>();
            StoreProduct removeAds = null;
            foreach (StoreProduct product in StoreCatalog.Products)
            {
                if (product.Consumable) packs.Add(product);
                else removeAds = product;
            }
            int biggest = 0;
            foreach (StoreProduct p in packs) biggest = Mathf.Max(biggest, p.Coins);
            float cardW = (932f - (packs.Count - 1) * 22f) / Mathf.Max(1, packs.Count);
            for (int i = 0; i < packs.Count; i++)
            {
                StoreProduct p = packs[i];
                float x = -466f + cardW * 0.5f + i * (cardW + 22f);
                Image card = UiPremium.MakeTile(body, "Pack", 0.5f, 1f, x, -116f, cardW, 520f);
                bool best = p.Coins >= biggest;
                UiPremium.Rays(card.transform, 440f, 0f, 70f, best ? 0.85f : 0.6f);
                Image art = UiKit.MakeImage(card.transform, "Art", UiKit.Icon(best ? "chest" : "coins"), Color.white);
                art.preserveAspect = true;
                UiKit.Place(art.rectTransform, 0.5f, 0.5f, 0f, 70f, 250f, 250f);
                art.gameObject.AddComponent<Bob>().Height = 6f;
                Text title = UiKit.MakeText(card.transform, p.Title, 36, UiPremium.Ink);
                UiKit.Place(title.rectTransform, 0.5f, 1f, 0f, -30f, cardW - 40f, 50f);
                Text amount = UiPremium.Heading(card.transform, "+" + p.Coins, 74, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
                UiKit.Place(amount.rectTransform, 0.5f, 0f, 0f, 150f, cardW, 90f);
                Button buy = UiPremium.PriceButton(card.transform, p.PriceLabel, UiPremium.Green, () => BuyProduct(p), cardW - 70f, 108f, false);
                UiKit.Place((RectTransform)buy.transform, 0.5f, 0f, 0f, 26f, cardW - 70f, 108f);
                if (best) UiPremium.Flag(card.transform, "BEST VALUE", UiPremium.Red, 0.5f, 1f, 0f, 22f, 300f);
            }

            // Free coins for watching an ad.
            Image free = UiPremium.MakeTile(body, "FreeCoins", 0.5f, 1f, 0f, -664f, 932f, 150f);
            Image gift = UiKit.AddIcon(free.transform, UiKit.Art("video_coins") != null ? "video_coins" : "gift", 136f, 0f, 0.5f, 90f, 6f);
            gift.gameObject.AddComponent<Bob>().Height = 4f;
            Text freeTitle = UiKit.MakeText(free.transform, Loc.F("Free +{0} coins", s.Config.RewardedAdCoins), 44, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Stretch(freeTitle.rectTransform, 176f, 62f, 290f, 14f);
            Text freeSub = UiKit.MakeText(free.transform, "Watch a short video", 30, new Color(0.55f, 0.38f, 0.22f), TextAnchor.MiddleLeft);
            UiKit.Stretch(freeSub.rectTransform, 176f, 18f, 290f, 82f);
            Button watch = Btn(free.transform, "Watch", UiPremium.Orange, WatchAdForCoins, 240f, 100f, 44);
            UiKit.Place((RectTransform)watch.transform, 1f, 0.5f, -26f, 4f, 240f, 100f);

            // Boosters: four small cards with the owned count and a coin price.
            Text boosterHead = UiPremium.Heading(body, "Boosters", 44, Color.white, UiPremium.DeepBlue);
            UiKit.Place(boosterHead.rectTransform, 0.5f, 1f, 0f, -834f, 600f, 60f);
            for (int i = 0; i < 4; i++)
            {
                var type = (BoosterType)i;
                float x = -351f + i * 234f;
                Image card = UiPremium.MakeTile(body, "Booster" + i, 0.5f, 1f, x, -902f, 216f, 316f);
                UiKit.AddIcon(card.transform, BoosterIcons[i], 124f, 0.5f, 1f, 0f, -78f);
                Text name = UiKit.MakeText(card.transform, BoosterNames[i], 30, UiPremium.Ink);
                UiKit.Place(name.rectTransform, 0.5f, 1f, 0f, -150f, 200f, 44f);
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 30;
                Image badge = UiKit.MakeImage(card.transform, "Count", UiPremium.Round, UiPremium.Red);
                UiKit.Place(badge.rectTransform, 1f, 1f, 14f, 14f, 70f, 70f);
                _shopBoosterCounts[i] = UiKit.MakeText(badge.transform, "", 32, Color.white);
                UiKit.Stretch(_shopBoosterCounts[i].rectTransform, 0f, 4f, 0f, 0f);
                UiKit.Emboss(_shopBoosterCounts[i], new Color(0.45f, 0.04f, 0.04f, 0.9f));
                _shopBoosterButtons[i] = UiPremium.PriceButton(card.transform, s.Boosters.Cost(type).ToString(), UiPremium.Green, () =>
                {
                    if (s.Boosters.Buy(type)) _app.Audio.Play(Sfx.Coins);
                    else { _app.Audio.Play(Sfx.Invalid); Toast("Not enough coins"); }
                }, 180f, 84f, true);
                UiKit.Place((RectTransform)_shopBoosterButtons[i].transform, 0.5f, 0f, 0f, 20f, 180f, 84f);
            }

            // Remove ads.
            if (removeAds != null)
            {
                StoreProduct p = removeAds;
                Image tile = UiPremium.MakeTile(body, "RemoveAds", 0.5f, 1f, 0f, -1244f, 932f, 150f);
                if (UiKit.Art("no_ads") != null) UiKit.AddIcon(tile.transform, "no_ads", 136f, 0f, 0.5f, 86f, 4f);
                else
                {
                    Image noAds = UiKit.MakeImage(tile.transform, "NoAds", UiPremium.Round, UiPremium.Red);
                    UiKit.Place(noAds.rectTransform, 0f, 0.5f, 30f, 4f, 112f, 112f);
                    Text ad = UiKit.MakeText(noAds.transform, "AD", 40, Color.white);
                    UiKit.Stretch(ad.rectTransform, 0f, 6f, 0f, 0f);
                    UiKit.Emboss(ad, new Color(0.45f, 0.04f, 0.04f, 0.9f));
                }
                Text t = UiKit.MakeText(tile.transform, p.Title, 44, UiPremium.Ink, TextAnchor.MiddleLeft);
                UiKit.Stretch(t.rectTransform, 170f, 62f, 290f, 14f);
                Text sub = UiKit.MakeText(tile.transform, "Play without ad breaks", 30, new Color(0.55f, 0.38f, 0.22f), TextAnchor.MiddleLeft);
                UiKit.Stretch(sub.rectTransform, 170f, 18f, 290f, 82f);
                _removeAdsButton = UiPremium.PriceButton(tile.transform, p.PriceLabel, UiPremium.Green, () => BuyProduct(p), 240f, 100f, false);
                UiKit.Place((RectTransform)_removeAdsButton.transform, 1f, 0.5f, -26f, 4f, 240f, 100f);
            }
            Mascot((RectTransform)body.parent, "thumbs", 220f, -390f, -14f);
        }

        private void BuyProduct(StoreProduct p)
        {
            _app.Audio.Play(Sfx.Click);
            _app.Services.Store.Buy(p.Id, ok =>
            {
                if (ok && p.Consumable) RevealReward("chest", Loc.F("Thank you! {0} added", Loc.T(p.Title)), p.Coins);
                else
                {
                    if (ok) _app.Audio.Play(Sfx.Coins);
                    Toast(ok ? Loc.F("Thank you! {0} added", Loc.T(p.Title)) : "Purchase was not completed");
                }
            });
        }

        private void BuildLevels()
        {
            RectTransform body;
            _levels = UiPremium.Modal(_root, "Levels", "Levels", 1000f, 1600f, () => { _app.Audio.Play(Sfx.Click); _levels.SetActive(false); }, out body);
            // The line's station painting, with its name on a dark band, and round arrows to change line.
            Image frame = UiKit.MakeImage(body, "Banner", UiKit.Rounded, Color.white);
            UiKit.Place(frame.rectTransform, 0.5f, 1f, 0f, -6f, 932f, 300f);
            frame.pixelsPerUnitMultiplier = 0.6f;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _levelsBanner = UiKit.MakeImage(frame.transform, "Picture", UiKit.Art("chapter_0"), Color.white);
            UiKit.Stretch(_levelsBanner.rectTransform);
            Image band = UiKit.MakeImage(frame.transform, "NameBand", null, new Color(0.03f, 0.08f, 0.22f, 0.62f));
            UiKit.Place(band.rectTransform, 0.5f, 0f, 0f, 0f, 932f, 90f);
            _levelsBannerName = UiPremium.Heading(band.transform, "", 52, Color.white, new Color(0.02f, 0.05f, 0.16f, 0.95f));
            UiKit.Stretch(_levelsBannerName.rectTransform, 20f, 6f, 20f, 0f);
            _levelsPrev = UiPremium.MakeRoundButton(body, UiPremium.Round, UiPremium.Blue, () => { _app.Audio.Play(Sfx.Click); ShowLevelsPage(_levelsPage - 1); }, 110f);
            UiKit.Place((RectTransform)_levelsPrev.transform, 0f, 1f, -18f, -100f, 110f, 110f);
            Text prev = UiPremium.Heading(_levelsPrev.transform, "<", 64, Color.white, UiPremium.DeepBlue);
            UiKit.Stretch(prev.rectTransform, 0f, 8f, 6f, 0f);
            _levelsNext = UiPremium.MakeRoundButton(body, UiPremium.Round, UiPremium.Blue, () => { _app.Audio.Play(Sfx.Click); ShowLevelsPage(_levelsPage + 1); }, 110f);
            UiKit.Place((RectTransform)_levelsNext.transform, 1f, 1f, 18f, -100f, 110f, 110f);
            Text next = UiPremium.Heading(_levelsNext.transform, ">", 64, Color.white, UiPremium.DeepBlue);
            UiKit.Stretch(next.rectTransform, 6f, 8f, 0f, 0f);

            // The map: one metro line snaking through twenty stations.
            _levelsGrid = UiKit.Place(UiKit.MakeRect("Map", body), 0.5f, 1f, 0f, -330f, 932f, 1080f);
            Image well = UiKit.MakeImage(_levelsGrid, "Well", UiPremium.Well, Color.white);
            UiKit.Stretch(well.rectTransform);
            Sprite mapArt = UiKit.Art("map_bg");
            if (mapArt != null)
            {
                // A soft painted city under the line, clipped to the well's rounded corners.
                Image clip = UiKit.MakeImage(well.transform, "MapClip", UiKit.Rounded, Color.white);
                UiKit.Stretch(clip.rectTransform, 6f, 6f, 6f, 6f);
                clip.pixelsPerUnitMultiplier = 0.5f;
                clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                Image map = UiKit.MakeImage(clip.transform, "Map", mapArt, new Color(1f, 1f, 1f, 0.92f));
                UiKit.Stretch(map.rectTransform);
                var cover = map.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = mapArt.rect.width / mapArt.rect.height;
            }
            _levelsPageLabel = UiKit.MakeText(body, "", 34, new Color(1f, 1f, 1f, 0.85f));
            UiKit.Place(_levelsPageLabel.rectTransform, 0.5f, 0f, 0f, -8f, 600f, 50f);
            Mascot((RectTransform)body.parent, "think", 210f, -390f, -14f);
        }

        // Station spots on the level map: four rows of five, the line running left to right, then back.
        private static Vector2 MapSpot(int index)
        {
            int row = index / 5, col = index % 5;
            if (row % 2 == 1) col = 4 - col;
            return new Vector2(-352f + col * 176f, -130f - row * 268f);
        }

        private void BuildWin()
        {
            RectTransform content;
            Sprite ribbon = UiKit.Art("ribbon");
            _win = UiKit.Modal(_root, "LevelComplete", ribbon != null ? "" : "Level Complete!", out content);
            Mascot(content, "cheer", 330f, 0f, 50f);
            if (ribbon != null)
            {
                // The ribbon is wider than the card and overlaps its top edge; the conductor stands behind it.
                RectTransform slot = UiKit.MakeRect("Title", content);
                slot.sizeDelta = new Vector2(780f, 130f);
                Image band = UiKit.MakeImage(slot, "Ribbon", ribbon, Color.white);
                band.preserveAspect = true;
                float bandWidth = 980f, bandHeight = bandWidth * ribbon.rect.height / ribbon.rect.width;
                UiKit.Place(band.rectTransform, 0.5f, 0.5f, 0f, 40f, bandWidth, bandHeight);
                Text t = UiKit.MakeText(band.transform, "Level Complete!", 62, Color.white);
                UiKit.Place(t.rectTransform, 0.5f, 0.5f, 0f, bandHeight * 0.13f, bandWidth * 0.60f, 110f);
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 30;
                t.resizeTextMaxSize = 62;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiKit.Emboss(t, new Color(0.50f, 0.06f, 0.10f, 0.9f));
            }
            RectTransform stars = UiKit.MakeRect("Stars", content);
            stars.sizeDelta = new Vector2(780f, 200f);
            Sprite rays = UiKit.Art("rays");
            if (rays != null)
            {
                Image glow = UiKit.MakeImage(stars, "Rays", rays, new Color(1f, 0.86f, 0.35f, 0.9f));
                UiKit.Place(glow.rectTransform, 0.5f, 0.5f, 0f, 10f, 560f, 560f);
                glow.gameObject.AddComponent<Spin>();
            }
            for (int i = 0; i < 3; i++)
            {
                _winStars[i] = UiKit.MakeImage(stars, "Star" + i, UiKit.Icon("star"), Color.white);
                _winStars[i].preserveAspect = true;
                float starSize = i == 1 ? 240f : 200f;
                UiKit.Place(_winStars[i].rectTransform, 0.5f, 0.5f, (i - 1) * 215f, i == 1 ? 22f : 0f, starSize, starSize);
                // An earned star is the 3D one, turning as it lands; the flat picture stays for empty slots.
                _winStarSpins[i] = UiKit.MakeFlipBook(_winStars[i].transform, "star_spin", starSize * 1.15f);
                if (_winStarSpins[i] != null) _winStarSpins[i].Interval = 3.5f + i * 0.6f;
            }
            _winCoins = UiPremium.Heading(content, "", 56, UiPremium.Gold, new Color(0.40f, 0.18f, 0.0f, 1f));
            _winCoins.rectTransform.sizeDelta = new Vector2(780f, 80f);
            _winCoinSpin = UiKit.MakeFlipBook(_winCoins.transform, "coin_spin", 84f);
            if (_winCoinSpin != null) _winCoinSpin.Loop = true;

            // Star chest: the new stars of a level fly into this bar; when it is full the chest opens.
            RectTransform chestRow = UiKit.MakeRect("StarChest", content);
            chestRow.sizeDelta = new Vector2(780f, 116f);
            Text chestLabel = UiPremium.Heading(chestRow, Loc.T("Star Chest"), 30, new Color(1f, 0.90f, 0.50f), new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Place(chestLabel.rectTransform, 0f, 1f, 30f, 0f, 520f, 42f);
            chestLabel.alignment = TextAnchor.MiddleLeft;
            _chestTrack = UiKit.Place(UiKit.MakeRect("Track", chestRow), 0f, 0f, 20f, 8f, 600f, 54f);
            Image chestBack = UiKit.MakeImage(_chestTrack, "Back", UiPremium.Pill, new Color(0.03f, 0.06f, 0.16f, 0.95f));
            UiKit.Stretch(chestBack.rectTransform);
            Image chestFill = UiKit.MakeImage(_chestTrack, "Fill", UiPremium.Pill, new Color(1f, 0.76f, 0.18f));
            _chestFill = UiKit.Place(chestFill.rectTransform, 0f, 0.5f, 4f, 0f, ChestFillMin, 46f);
            Image chestStar = UiKit.AddIcon(_chestTrack, "star", 70f, 0f, 0.5f, -8f, 2f);
            chestStar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _chestText = UiPremium.Heading(_chestTrack, "", 32, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Stretch(_chestText.rectTransform, 30f, 2f, 10f, 2f);
            Sprite chestArt = UiKit.Art("chest");
            Image chestIcon = UiKit.MakeImage(chestRow, "Chest", chestArt != null ? chestArt : UiKit.Icon("gift"), Color.white);
            chestIcon.preserveAspect = true;
            _chestIcon = UiKit.Place(chestIcon.rectTransform, 1f, 0f, -70f, 34f, 150f, 150f);
            _chestIcon.pivot = new Vector2(0.5f, 0.5f);
            Row(content, "Continue", Palette.UiGreen, OnContinue, 150f, 60);
            _winAdButton = Row(content, Loc.F("Watch: +{0} coins", _app.Services.Config.RewardedAdCoins), UiPremium.Orange, () =>
            {
                _winAdButton.gameObject.SetActive(false);
                WatchAdForCoins();
            }, 110f, 44);
            RowIcon(_winAdButton, UiKit.Art("video_coins") != null ? "video_coins" : "gift");
            Row(content, "Menu", new Color(0.10f, 0.17f, 0.38f), () => _app.GoToMenu(), 100f, 40);
        }

        private void BuildLose()
        {
            RectTransform content;
            _lose = UiKit.Modal(_root, "LevelFailed", "", out content);
            Mascot(content, "sad", 300f, 0f, -24f);
            _loseTitle = UiPremium.Heading(content, "", 66, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            _loseTitle.rectTransform.sizeDelta = new Vector2(800f, 96f);
            _loseReason = UiKit.MakeText(content, "", 40, new Color(0.90f, 0.95f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Emboss(_loseReason, new Color(0.03f, 0.08f, 0.24f, 0.7f));
            _loseReason.rectTransform.sizeDelta = new Vector2(780f, 130f);
            _loseUndo = UiKit.MakeButton(content, "", BoosterColors[0], () => _app.Game.UseUndo(), 780f, 120f, 42);
            _losePlatform = UiKit.MakeButton(content, "", BoosterColors[2], () => _app.Game.UseExtraPlatform(), 780f, 120f, 42);
            Button retry = Row(content, "Try Again", Palette.UiGreen, () => SpendLife(RestartChecked), 140f, 56);
            LifeCostTag(retry.transform);
            Button menuButton = Row(content, "Menu", new Color(0.10f, 0.17f, 0.38f), () => SpendLife(_app.GoToMenu), 100f, 40);
            LifeCostTag(menuButton.transform);
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
            if (_menuBackdrop != null) _menuBackdrop.SetActive(state == AppState.Menu);
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
            if (state != AppState.Playing && state != AppState.Paused && _intro != null)
            {
                Destroy(_intro);
                _intro = null;
            }
            Refresh();
        }

        /// <summary>Re-reads everything shown on screen. Cheap enough to call on any change.</summary>
        public void Refresh()
        {
            if (_app == null || _menu == null) return;
            // The conductor pops up with a bright idea whenever a hint has just been found.
            bool hinting = _app.Game != null && _app.Game.HasHint;
            if (hinting && !_hadHint) MascotPop("idea");
            _hadHint = hinting;
            GameServices s = _app.Services;
            string coins = (_coinDisplay >= 0 ? _coinDisplay : s.Economy.Coins).ToString();
            _menuCoins.text = coins;
            _hudCoins.text = coins;
            _shopCoins.text = coins;

            int current = _app.CurrentLevelId;
            LevelData next = s.Levels.Get(current);
            _menuLevel.text = Loc.F("Level {0}  -  {1}", next.Id, Loc.LevelName(next.Name));
            RefreshDaily();
            RefreshLives();

            for (int i = 0; i < 4; i++)
            {
                var type = (BoosterType)i;
                int count = s.Boosters.Count(type);
                // Owned charges are shown as a count; otherwise the coin price is shown so paid use is never a surprise.
                _boosterBadges[i].text = count > 0 ? "x" + count : Loc.F("{0} coins", s.Boosters.Cost(type));
                _boosterBadges[i].color = count > 0 ? Color.white : new Color(1f, 0.92f, 0.55f);
                _shopBoosterCounts[i].text = count.ToString();
            }
            UiKit.LabelOf(_removeAdsButton).text = s.Store.AdsRemoved ? Loc.T("Done") : StoreCatalog.Find(StoreCatalog.RemoveAds).PriceLabel;
            _removeAdsButton.interactable = !s.Store.AdsRemoved;

            GameController game = _app.Game;
            if (game.Board == null) { RefreshSchedule(null); return; }
            BoardState state = game.Board.State;
            _hudTitle.text = Loc.F("Level {0}", game.Level.Id);
            RefreshSchedule(state);
            // A new level, a restart or an undo shows the true count at once; boarding catches up as riders land.
            bool newBoard = !ReferenceEquals(game.Board, _progressBoard);
            if (_boardedShown < 0 || state.Boarded < _boardedShown || newBoard) _boardedShown = state.Boarded;
            if (state.Boarded != _boardedTarget) _boardedChanged = Time.unscaledTime;
            _boardedTarget = state.Boarded;
            _progressBoard = game.Board;
            _progressState = state;
            ShowProgress();
            if (newBoard && _app.States.Current == AppState.Playing) ShowLevelIntro(game.Level.Id, state);

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
            _assistSwitch.Set(data.AssistedMode, "ON", "OFF");
            _motionSwitch.Set(data.ReducedMotion, "ON", "OFF");
            _fastSwitch.Set(data.FastTrains, "ON", "OFF");
            _languageLabel.text = Loc.Language == Loc.Turkish ? "Türkçe" : "English";
            _app.Services.Save.Save();
        }

        // ------------------------------------------------------------------ actions

        private void OnPlay() { StartLevelChecked(_app.CurrentLevelId); }

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
            _settingsGameRow.SetActive(inGame);
            // On the menu there is only one button under the rows, so the panel is shorter.
            var panel = (RectTransform)_settings.transform.Find("Panel");
            if (panel != null) panel.sizeDelta = new Vector2(940f, inGame ? 1300f : 1150f);
            _restoreButton.gameObject.SetActive(!inGame);
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
            for (int i = _levelsGrid.childCount - 1; i >= 1; i--) Destroy(_levelsGrid.GetChild(i).gameObject);
            int first = _levelsPage * LevelsPerPage + 1;
            int last = Mathf.Min(count, first + LevelsPerPage - 1);
            Color line = Palette.Lines[_levelsPage % Palette.Lines.Length];
            int current = _app.CurrentLevelId;

            // Track first, under the stations: a white-edged band in the line's colour.
            RectTransform track = UiKit.Stretch(UiKit.MakeRect("Track", _levelsGrid));
            for (int i = 0; i + first < last; i++)
            {
                Vector2 a = MapSpot(i), b = MapSpot(i + 1);
                bool done = s.Progression.IsUnlocked(first + i + 1);
                foreach (bool edge in new[] { true, false })
                {
                    Image seg = UiKit.MakeImage(track, "Segment", UiKit.Rounded, edge ? Color.white : (done ? line : new Color(0.55f, 0.62f, 0.74f)));
                    float w = edge ? 40f : 24f;
                    Vector2 mid = (a + b) * 0.5f;
                    float len = (b - a).magnitude + w;
                    seg.rectTransform.anchorMin = seg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    seg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    seg.rectTransform.anchoredPosition = mid;
                    seg.rectTransform.sizeDelta = Mathf.Abs(b.y - a.y) > 1f ? new Vector2(w, len) : new Vector2(len, w);
                    seg.pixelsPerUnitMultiplier = 2f;
                }
            }

            for (int id = first; id <= last; id++)
            {
                int levelId = id;
                Vector2 at = MapSpot(id - first);
                bool unlocked = s.Progression.IsUnlocked(id);
                int stars = s.Progression.GetStars(id);
                bool here = id == current;
                float size = here ? 150f : 124f;
                Color fill = !unlocked ? new Color(0.62f, 0.66f, 0.74f) : (here ? UiPremium.Gold : Color.white);
                // A ring in the line colour around each station, like a metro map.
                Image ring = UiKit.MakeImage(_levelsGrid, "Ring" + id, UiPremium.Round, unlocked ? line : new Color(0.48f, 0.52f, 0.60f));
                UiKit.Place(ring.rectTransform, 0.5f, 1f, at.x, at.y + size * 0.5f, size + 22f, size + 22f);
                ring.rectTransform.pivot = new Vector2(0.5f, 1f);
                ring.rectTransform.anchoredPosition = new Vector2(at.x, at.y + (size + 22f) * 0.5f);
                Button b = UiPremium.MakeRoundButton(ring.transform, UiPremium.Round, fill, () =>
                {
                    _app.Audio.Play(Sfx.Click);
                    _levels.SetActive(false);
                    StartLevelChecked(levelId);
                }, size);
                UiKit.Place((RectTransform)b.transform, 0.5f, 0.5f, 0f, 0f, size, size);
                b.interactable = unlocked;
                if (!unlocked)
                {
                    UiKit.AddIcon(b.transform, "lock", size * 0.62f, 0.5f, 0.5f, 0f, 4f);
                    continue;
                }
                Text number = UiPremium.Heading(b.transform, id.ToString(), here ? 56 : 46, here ? Color.white : UiPremium.DeepBlue,
                    here ? new Color(0.55f, 0.28f, 0.02f, 0.95f) : new Color(1f, 1f, 1f, 0.6f));
                UiKit.Stretch(number.rectTransform, 4f, 8f, 4f, 0f);
                number.resizeTextForBestFit = true;
                number.resizeTextMinSize = 24;
                number.resizeTextMaxSize = here ? 56 : 46;
                if (here) ring.gameObject.AddComponent<Bob>().Height = 6f;
                for (int k = 0; k < 3; k++)
                {
                    UiKit.AddIcon(ring.transform, k < stars ? "star" : "star_off", 46f, 0.5f, 0f, (k - 1) * 42f, -14f + (k == 1 ? -6f : 0f));
                }
            }

            string lineName = Loc.T(Chapters.Names[Mathf.Min(_levelsPage, Chapters.Names.Length - 1)]);
            // Each line shows its own station; pages past the last line reuse the paintings in turn.
            int painting = Chapters.PaintingFor(_levelsPage);
            _levelsBanner.sprite = UiKit.Art("chapter_" + (painting >= 0 ? painting : _levelsPage % ChapterPictures));
            _levelsBannerName.text = lineName;
            _levelsPageLabel.text = first + " - " + last;
            _levelsPrev.interactable = _levelsPage > 0;
            _levelsNext.interactable = _levelsPage < pages - 1;
        }

        // ------------------------------------------------------------------ level intro

        private GameObject _intro;

        /// <summary>
        /// A level opens with a ribbon "Level N" dropping in over the station and the goal under it; the goal then
        /// flies up into the boarding bar. Taps go straight through to the game, so playing at once just plays.
        /// </summary>
        private void ShowLevelIntro(int levelId, BoardState state)
        {
            if (_intro != null) Destroy(_intro);
            _intro = null;
            if (GameSettings.ReducedMotion || !isActiveAndEnabled || _hudTrack == null) return;
            StartCoroutine(IntroRoutine(levelId, state));
        }

        private IEnumerator IntroRoutine(int levelId, BoardState state)
        {
            yield return null;   // the tutorial decides on the level's first frame whether it guides this level
            if (_app.Tutorial.IsGuiding || _app.States.Current != AppState.Playing) yield break;
            RectTransform root = UiKit.Stretch(UiKit.MakeRect("LevelIntro", _root));
            _intro = root.gameObject;
            _flying.Add(_intro);

            Sprite ribbonArt = UiKit.Art("ribbon");
            const float BandWidth = 900f, BandY = 330f, GoalY = 170f;
            float bandHeight = ribbonArt != null ? BandWidth * ribbonArt.rect.height / ribbonArt.rect.width : 150f;
            Image band = UiKit.MakeImage(root, "Ribbon", ribbonArt != null ? ribbonArt : UiPremium.Tag, ribbonArt != null ? Color.white : UiPremium.Red);
            band.preserveAspect = ribbonArt != null;
            RectTransform bandRt = UiKit.Place(band.rectTransform, 0.5f, 0.5f, 0f, BandY, BandWidth, bandHeight);
            Text title = UiPremium.Heading(band.transform, Loc.F("Level {0}", levelId), 72, Color.white, new Color(0.50f, 0.06f, 0.10f, 0.95f));
            UiKit.Place(title.rectTransform, 0.5f, 0.5f, 0f, ribbonArt != null ? bandHeight * 0.13f : 0f, BandWidth * 0.62f, 110f);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 30;
            title.resizeTextMaxSize = 72;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;

            // The goal: a passenger token and how many have to board (and the move limit, if any).
            RectTransform goal = UiKit.Place(UiKit.MakeRect("Goal", root), 0.5f, 0.5f, 0f, GoalY, 580f, 116f);
            Image rim = UiKit.MakeImage(goal, "Rim", UiPremium.Pill, new Color(1f, 0.74f, 0.20f));
            UiKit.Stretch(rim.rectTransform);
            var rimShadow = rim.gameObject.AddComponent<Shadow>();
            rimShadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.4f);
            rimShadow.effectDistance = new Vector2(0f, -6f);
            Image plate = UiKit.MakeImage(goal, "Board", UiPremium.Pill, new Color(0.10f, 0.17f, 0.38f, 0.97f));
            UiKit.Stretch(plate.rectTransform, 6f, 6f, 6f, 6f);
            Image token = UiKit.MakeImage(goal, "Token", UiPremium.Round, UiPremium.Green);
            UiKit.Place(token.rectTransform, 0f, 0.5f, 18f, 0f, 86f, 86f);
            Image person = UiKit.MakeImage(token.transform, "Person", UiPremium.Rider, Color.white);
            UiKit.Stretch(person.rectTransform, 14f, 12f, 14f, 16f);
            string goalText = Loc.F("{0} passengers", state.Total);
            if (state.MoveLimit > 0) goalText += "  ·  " + Loc.F("{0} moves", state.MoveLimit);
            Text goalLabel = UiPremium.Heading(goal, goalText, 46, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Stretch(goalLabel.rectTransform, 116f, 8f, 30f, 6f);
            goalLabel.resizeTextForBestFit = true;
            goalLabel.resizeTextMinSize = 24;
            goalLabel.resizeTextMaxSize = 46;
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            var bandGroup = band.gameObject.AddComponent<CanvasGroup>();
            var goalGroup = goal.gameObject.AddComponent<CanvasGroup>();
            goal.localScale = Vector3.zero;

            // In: the ribbon drops and bounces, the goal pops under it.
            _app.Audio.Play(Sfx.MascotPop);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                if (root == null) yield break;
                bandRt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(BandY + 520f, BandY, Ease.OutBack(Mathf.Min(1f, t / 0.7f))));
                goal.localScale = Vector3.one * Ease.OutBack(Mathf.Clamp01((t - 0.35f) / 0.65f));
                yield return null;
            }
            for (float t = 0f; t < 1.0f; t += Time.unscaledDeltaTime)
            {
                if (root == null) yield break;
                yield return null;
            }

            // Out: the ribbon floats up and fades, the goal flies into the boarding bar.
            Vector2 goalStart = goal.anchoredPosition;
            _app.Audio.Play(Sfx.Select);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
            {
                if (root == null || _hudTrack == null) yield break;
                Vector2 end = root.InverseTransformPoint(_hudTrack.TransformPoint(_hudTrack.rect.center));
                float e = Ease.Smooth(t);
                goal.anchoredPosition = Vector2.Lerp(goalStart, end, e * e);
                goal.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, e);
                goalGroup.alpha = 1f - Mathf.Clamp01((t - 0.8f) / 0.2f);
                bandRt.anchoredPosition = new Vector2(0f, BandY + 140f * e);
                bandGroup.alpha = 1f - Mathf.Clamp01(t / 0.6f);
                yield return null;
            }
            if (root != null) Destroy(root.gameObject);
            _intro = null;
            _app.Audio.Play(Sfx.CoinTick);
            yield return BumpTrack();
        }

        // ------------------------------------------------------------------ boarded counter

        private void ShowProgress()
        {
            BoardState state = _progressState;
            if (state == null || _hudProgress == null) return;
            int shown = Mathf.Clamp(_boardedShown, 0, state.Total);
            _hudFillTarget = Mathf.Lerp(40f, 472f, state.Total > 0 ? (float)shown / state.Total : 0f);
            string progress = Loc.F("Boarded {0} / {1}", shown, state.Total);
            if (state.StageCount > 1) progress += Loc.F("   Wave {0}/{1}", state.Stage + 1, state.StageCount);
            if (state.MoveLimit > 0) progress += Loc.F("   Moves left {0}", state.MovesRemaining);
            _hudProgress.text = progress;
        }

        private void AddBoarded(int count)
        {
            _boardedShown = Mathf.Min(_boardedShown + count, _boardedTarget);
            ShowProgress();
        }

        /// <summary>
        /// The riders of a departing train fly from it to the boarding bar as little line-coloured tokens; each one
        /// that lands adds one to the count and bumps the bar.
        /// </summary>
        public void CollectBoarded(Vector3 world, int count, int line)
        {
            if (count <= 0) return;
            if (!isActiveAndEnabled || _hudTrack == null || !_hudTrack.gameObject.activeInHierarchy || GameSettings.ReducedMotion
                || _app.Station == null || _app.Station.Cam == null)
            {
                AddBoarded(count);
                return;
            }
            StartCoroutine(CollectRoutine(world, count, line));
        }

        private IEnumerator CollectRoutine(Vector3 world, int count, int line)
        {
            _collecting++;
            Vector2 start;
            Vector3 screen = _app.Station.Cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, UiCamera, out start);
            Color color = Palette.Line(line);
            var tokens = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                Image disc = UiKit.MakeImage(_root, "Rider", UiPremium.Round, color);
                tokens[i] = UiKit.Place(disc.rectTransform, 0.5f, 0.5f, start.x, start.y, 74f, 74f);
                tokens[i].pivot = new Vector2(0.5f, 0.5f);
                Image person = UiKit.MakeImage(disc.transform, "Person", UiPremium.Rider, Color.white);
                UiKit.Stretch(person.rectTransform, 12f, 10f, 12f, 14f);
                var shadow = disc.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.35f);
                shadow.effectDistance = new Vector2(0f, -4f);
                tokens[i].localScale = Vector3.zero;
                _flying.Add(disc.gameObject);
            }
            const float Stagger = 0.09f, Flight = 0.62f;
            float total = Stagger * (count - 1) + Flight;
            int landed = 0;
            for (float t = 0f; landed < count; t += Time.unscaledDeltaTime)
            {
                // The bar's leading edge moves as tokens land, so aim at where it is now.
                Vector3 edge = _hudFill.TransformPoint(new Vector3(_hudFill.rect.xMax, 0f, 0f));
                Vector2 end = _root.InverseTransformPoint(edge);
                for (int i = 0; i < count; i++)
                {
                    RectTransform rt = tokens[i];
                    if (rt == null) continue;
                    float k = (t - i * Stagger) / Flight;
                    if (k <= 0f) continue;
                    if (k >= 1f)
                    {
                        Destroy(rt.gameObject);
                        tokens[i] = null;
                        landed++;
                        AddBoarded(1);
                        _app.Audio.Play(Sfx.CoinTick);
                        StartCoroutine(BumpTrack());
                        continue;
                    }
                    // Out of the train with a little sideways spread, then a curve up to the bar.
                    float spread = (i - (count - 1) * 0.5f) * 70f;
                    Vector2 control = new Vector2(Mathf.Lerp(start.x, end.x, 0.25f) + spread, Mathf.Max(start.y, end.y) - 60f + 220f);
                    float e = Ease.Smooth(k);
                    Vector2 a = Vector2.Lerp(start, control, e), b = Vector2.Lerp(control, end, e);
                    rt.anchoredPosition = Vector2.Lerp(a, b, e);
                    float pop = Mathf.Min(1f, k / 0.18f);
                    rt.localScale = Vector3.one * (Ease.OutBack(pop) * Mathf.Lerp(1.1f, 0.55f, k * k));
                }
                if (t > total + 1f) break;   // never hang on a lost token
                yield return null;
            }
            for (int i = 0; i < count; i++)
                if (tokens[i] != null) { Destroy(tokens[i].gameObject); AddBoarded(1); }
            _collecting--;
        }

        private IEnumerator BumpTrack()
        {
            if (_hudTrack == null) yield break;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
            {
                if (_hudTrack == null) yield break;
                _hudTrack.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (_hudTrack != null) _hudTrack.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ praise

        /// <summary>A big gold word that pops up over a spot in the station, then rises and fades.</summary>
        public void Praise(string word, Vector3 world, int level)
        {
            if (!isActiveAndEnabled || _app.Station == null || _app.Station.Cam == null) return;
            StartCoroutine(PraiseRoutine(word, world, level));
        }

        private IEnumerator PraiseRoutine(string word, Vector3 world, int level)
        {
            Vector3 screen = _app.Station.Cam.WorldToScreenPoint(world);
            if (screen.z < 0f) yield break;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, UiCamera, out local);
            local.x = Mathf.Clamp(local.x, -340f, 340f);
            RectTransform holder = UiKit.Place(UiKit.MakeRect("Praise", _root), 0.5f, 0.5f, local.x, local.y, 600f, 160f);
            _flying.Add(holder.gameObject);
            Color[] fills = { new Color(1f, 0.92f, 0.45f), new Color(1f, 0.80f, 0.25f), new Color(1f, 0.62f, 0.30f), new Color(1f, 0.45f, 0.62f) };
            Text text = UiPremium.Heading(holder, word, 74 + level * 8, fills[Mathf.Clamp(level, 0, fills.Length - 1)], new Color(0.40f, 0.12f, 0.02f, 1f));
            UiKit.Stretch(text.rectTransform);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            if (level >= 1) _app.Audio.Play(Sfx.Star);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.1f)
            {
                if (holder == null) yield break;
                float pop = GameSettings.ReducedMotion ? 1f : Ease.OutBack(Mathf.Min(1f, t / 0.22f));
                holder.localScale = Vector3.one * pop;
                holder.localRotation = Quaternion.Euler(0f, 0f, (level % 2 == 0 ? -1f : 1f) * 6f * (1f - Mathf.Min(1f, t / 0.3f)));
                holder.anchoredPosition = local + new Vector2(0f, 90f * Ease.OutCubic(t));
                Color c = text.color;
                c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                text.color = c;
                outline.effectColor = new Color(1f, 1f, 1f, 0.9f * c.a);
                yield return null;
            }
            if (holder != null) Destroy(holder.gameObject);
        }

        // ------------------------------------------------------------------ lives

        private long Now() { return LivesManager.Now(DateTime.UtcNow); }

        private static string Clock(long seconds)
        {
            return seconds >= 3600 ? string.Format("{0}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60)
                : string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        }

        /// <summary>The lives counter on the menu: a turning heart with the count on it and the refill clock beside it.</summary>
        private void BuildLivesPill(RectTransform menu)
        {
            RectTransform holder = UiKit.Place(UiKit.MakeRect("Lives", menu), 0f, 1f, 164f, -40f, 290f, 100f);
            Image pill = UiKit.MakeImage(holder, "Pill", UiPremium.Pill, new Color(0.10f, 0.20f, 0.45f, 0.92f));
            UiKit.Place(pill.rectTransform, 0.5f, 0.5f, 14f, 0f, 250f, 76f);
            FlipBook heart = UiKit.MakeFlipBook(holder, "heart_spin", 132f);
            if (heart != null)
            {
                ((RectTransform)heart.transform).anchorMin = ((RectTransform)heart.transform).anchorMax = new Vector2(0f, 0.5f);
                ((RectTransform)heart.transform).anchoredPosition = new Vector2(46f, 2f);
                heart.Interval = 4f;
            }
            else UiKit.AddIcon(holder, "heart", 120f, 0f, 0.5f, 46f, 2f);
            // The count sits in the middle of the heart.
            _menuLives = UiPremium.Heading(holder, "5", 46, Color.white, new Color(0.40f, 0.0f, 0.04f, 0.95f));
            UiKit.Place(_menuLives.rectTransform, 0f, 0.5f, 46f, 8f, 90f, 80f);
            _menuLives.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _menuLivesTimer = UiKit.MakeText(holder, "", 36, Color.white);
            UiKit.Stretch(_menuLivesTimer.rectTransform, 110f, 8f, 16f, 4f);
            _menuLivesTimer.resizeTextForBestFit = true;
            _menuLivesTimer.resizeTextMinSize = 20;
            _menuLivesTimer.resizeTextMaxSize = 36;
            UiKit.Emboss(_menuLivesTimer, new Color(0.03f, 0.08f, 0.22f, 0.95f));
            // Tapping the counter shows the lives popup (refill, ad, countdown).
            var tap = pill.gameObject.AddComponent<Button>();
            pill.raycastTarget = true;
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => { _app.Audio.Play(Sfx.Click); OpenNoLives(); });
        }

        private void RefreshLives()
        {
            if (_menuLives == null) return;
            LivesManager lives = _app.Services.Lives;
            long now = Now();
            int count = lives.Count(now);
            string clock = count >= lives.Max ? Loc.T("Full") : Clock(lives.SecondsToNext(now));
            _menuLives.text = count.ToString();
            _menuLivesTimer.text = clock;
            if (_noLives != null && _noLives.activeSelf)
            {
                _noLivesCount.text = count.ToString();
                _noLivesTimer.text = count >= lives.Max ? Loc.T("All lives are full!") : Loc.F("Next life in {0}", clock);
                _refillButton.interactable = count < lives.Max;
            }
        }

        /// <summary>Starts a level when there is a life to play it with; otherwise offers ways to get one.</summary>
        private void StartLevelChecked(int levelId)
        {
            if (_app.Services.Lives.Count(Now()) > 0) _app.Game.StartLevel(levelId);
            else OpenNoLives();
        }

        private void RestartChecked()
        {
            if (_app.Services.Lives.Count(Now()) > 0) _app.Game.Restart();
            else
            {
                _app.GoToMenu();
                OpenNoLives();
            }
        }

        /// <summary>Leaving a level part-way costs a life once the player has made a move, as giving up would.</summary>
        private void LeaveLevel(Action next)
        {
            GameController game = _app.Game;
            bool started = game.Board != null && (game.Board.CanUndo || game.Board.State.Boarded > 0);
            if (started) SpendLife(next);
            else next();
        }

        /// <summary>Takes a life with the breaking-heart animation, then carries on.</summary>
        private void SpendLife(Action next)
        {
            if (_app.Services.Lives.Lose(Now())) StartCoroutine(HeartBreak());
            next();
            Refresh();
        }

        private IEnumerator HeartBreak()
        {
            _app.Audio.Play(Sfx.LevelFailed);
            RectTransform holder = UiKit.Place(UiKit.MakeRect("HeartBreak", _root), 0.5f, 0.5f, 0f, 120f, 420f, 420f);
            _flying.Add(holder.gameObject);
            FlipBook flip = UiKit.MakeFlipBook(holder, "heart_break", 420f);
            if (flip != null) flip.PlayOnce(1.1f);
            Text minus = UiPremium.Heading(holder, "-1", 96, Color.white, new Color(0.45f, 0.0f, 0.05f, 0.95f));
            UiKit.Place(minus.rectTransform, 0.5f, 0.5f, 0f, -230f, 300f, 120f);
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
            {
                if (holder == null) yield break;
                minus.rectTransform.anchoredPosition = new Vector2(0f, -230f + t * 60f);
                minus.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.6f - t * 1.2f));
                yield return null;
            }
            if (holder != null) Destroy(holder.gameObject);
        }

        /// <summary>A small "-1 heart" badge on buttons that cost a life.</summary>
        private static void LifeCostTag(Transform button)
        {
            RectTransform tag = UiKit.Place(UiKit.MakeRect("LifeCost", button), 1f, 0.5f, -70f, 4f, 120f, 80f);
            Image heart = UiKit.AddIcon(tag, "heart", 70f, 0.5f, 0.5f, 18f, 0f);
            Text t = UiPremium.Heading(heart.transform, "-1", 30, Color.white, new Color(0.40f, 0.0f, 0.04f, 0.95f));
            UiKit.Stretch(t.rectTransform, 0f, 6f, 0f, 0f);
        }

        /// <summary>Lives popup: the count on a big turning heart, the refill clock, a coin refill and a rewarded ad.</summary>
        private void BuildNoLives()
        {
            RectTransform body;
            _noLives = UiPremium.Modal(_root, "Lives", "Lives", 900f, 920f, () => { _app.Audio.Play(Sfx.Click); _noLives.SetActive(false); }, out body);
            UiPremium.Rays(body, 560f, 0f, -190f + 0f, 0.7f);
            RectTransform rays = (RectTransform)body.Find("Rays");
            if (rays != null) { rays.anchorMin = rays.anchorMax = new Vector2(0.5f, 1f); rays.anchoredPosition = new Vector2(0f, -200f); }
            FlipBook heart = UiKit.MakeFlipBook(body, "heart_spin", 300f);
            RectTransform heartRect = heart != null ? (RectTransform)heart.transform : UiKit.AddIcon(body, "heart", 300f).rectTransform;
            heartRect.anchorMin = heartRect.anchorMax = new Vector2(0.5f, 1f);
            heartRect.anchoredPosition = new Vector2(0f, -200f);
            if (heart != null) heart.Interval = 2.5f;
            _noLivesCount = UiPremium.Heading(heartRect, "0", 110, Color.white, new Color(0.40f, 0.0f, 0.04f, 0.95f));
            UiKit.Stretch(_noLivesCount.rectTransform, 0f, 20f, 0f, 0f);
            _noLivesTimer = UiPremium.Heading(body, "", 48, Color.white, UiPremium.DeepBlue);
            UiKit.Place(_noLivesTimer.rectTransform, 0.5f, 1f, 0f, -380f, 800f, 70f);

            GameServices s = _app.Services;
            _refillButton = UiPremium.PriceButton(body, Loc.F("Refill  {0}", s.Lives.RefillCost), UiPremium.Green, () =>
            {
                if (s.Lives.RefillWithCoins(Now())) { _app.Audio.Play(Sfx.Coins); Toast("Lives refilled!"); }
                else { _app.Audio.Play(Sfx.Invalid); Toast("Not enough coins"); }
                Refresh();
            }, 620f, 130f, true);
            UiKit.Place((RectTransform)_refillButton.transform, 0.5f, 1f, 0f, -490f, 620f, 130f);
            Button ad = Btn(body, "Watch an ad: +1 life", UiPremium.Orange, () =>
            {
                s.AdsCoordinator.RequestRewarded(() => s.Lives.Add(1, Now()), ok =>
                {
                    if (ok) { _app.Audio.Play(Sfx.Coins); Toast("+1 life"); }
                    else Toast("No ad available right now");
                    Refresh();
                });
            }, 620f, 120f, 40);
            UiKit.Place((RectTransform)ad.transform, 0.5f, 1f, 0f, -650f, 620f, 120f);
        }

        private void OpenNoLives()
        {
            _noLives.SetActive(true);
            _dailyTick = 0f;
            RefreshLives();
        }

        private float _dailyTick;

        /// <summary>
        /// The daily gift stays on the menu: bright and gently bobbing while there is a gift to collect, dimmed
        /// with a countdown to the next one once it has been collected.
        /// </summary>
        private void RefreshDaily()
        {
            if (_dailyButton == null) return;
            DateTime now = DateTime.UtcNow;
            bool ready = _app.Services.Economy.CanClaimDaily(EconomyManager.TodayNumber(now));
            _dailyButton.interactable = ready;
            Text label = UiKit.LabelOf(_dailyButton);
            if (ready) label.text = Loc.T("Daily Gift");
            else
            {
                TimeSpan left = now.Date.AddDays(1) - now;
                label.text = Loc.F("Next gift\n{0:00}:{1:00}:{2:00}", (int)left.TotalHours, left.Minutes, left.Seconds);
            }
            var bob = _dailyButton.GetComponent<Bob>();
            if (bob == null) bob = _dailyButton.gameObject.AddComponent<Bob>();
            bob.Height = 4f;
            bob.enabled = ready;
        }

        private void Update()
        {
            // Safety net: if the riders never arrive (the train waited, the level was left), show the true count.
            if (_boardedShown >= 0 && _boardedShown < _boardedTarget && _collecting == 0 && Time.unscaledTime - _boardedChanged > 6f)
            {
                _boardedShown = _boardedTarget;
                ShowProgress();
            }
            if (_hudFill != null && Mathf.Abs(_hudFill.sizeDelta.x - _hudFillTarget) > 0.5f)
                _hudFill.sizeDelta = new Vector2(Mathf.Lerp(_hudFill.sizeDelta.x, _hudFillTarget, Time.unscaledDeltaTime * 8f), _hudFill.sizeDelta.y);
            // The countdown on the daily gift ticks once a second while the menu is showing.
            bool counting = (_menu != null && _menu.activeInHierarchy) || (_noLives != null && _noLives.activeInHierarchy);
            if (!counting || Time.unscaledTime < _dailyTick) return;
            _dailyTick = Time.unscaledTime + 1f;
            RefreshDaily();
            RefreshLives();
        }

        private void OnDaily()
        {
            int granted = _app.Services.Economy.ClaimDaily(EconomyManager.TodayNumber(DateTime.UtcNow));
            if (granted <= 0) return;
            RevealReward("gift", Loc.F("Daily gift: +{0} coins", granted), granted);
        }

        private void WatchAdForCoins()
        {
            _app.Services.AdsCoordinator.RequestRewardedCoins(ok =>
            {
                if (ok) RevealReward("gift", Loc.F("+{0} coins", _app.Services.Config.RewardedAdCoins), _app.Services.Config.RewardedAdCoins);
                else Toast("No ad available right now");
            });
        }

        private void OnContinue()
        {
            int finished = _app.Game.Level.Id;
            // Interstitials are only ever considered here, between levels, never during a puzzle.
            _app.Services.AdsCoordinator.OnLevelTransition(finished, () =>
            {
                if (_app.Services.Levels.Has(finished + 1)) StartLevelChecked(finished + 1);
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
            if (_winCoinSpin != null)
            {
                // The turning coin sits just left of the reward text, whatever its length.
                ((RectTransform)_winCoinSpin.transform).anchoredPosition = new Vector2(-_winCoins.preferredWidth * 0.5f - 56f, 4f);
            }
            foreach (GameObject stray in _flying) if (stray != null) Destroy(stray);
            _flying.Clear();
            _winAdButton.gameObject.SetActive(_app.Services.AdsCoordinator.IsRewardedReady);
            StopAllCoroutines();
            // Stopped coroutines leave no one to finish the boarded count or the bar bump: settle them here.
            _collecting = 0;
            _boardedShown = _boardedTarget;
            ShowProgress();
            if (_hudTrack != null) _hudTrack.localScale = Vector3.one;
            // A star chest about to open has already paid: hold its coins back on the counter until it opens.
            _coinDisplay = result.ChestCoins > 0 && !GameSettings.ReducedMotion ? Mathf.Max(0, _app.Services.Economy.Coins - result.ChestCoins) : -1;
            Refresh();
            ShowChest(result.ChestBefore);
            StartCoroutine(PopStars(result));
        }

        private int ChestSize { get { return Mathf.Max(1, _app.Services.Config.StarChestStars); } }

        private void ShowChest(int stars)
        {
            int size = ChestSize;
            stars = Mathf.Clamp(stars, 0, size);
            _chestText.text = stars + "/" + size;
            _chestFill.sizeDelta = new Vector2(Mathf.Lerp(ChestFillMin, ChestFillMax, (float)stars / size), _chestFill.sizeDelta.y);
            _chestIcon.localRotation = Quaternion.identity;
            _chestIcon.localScale = Vector3.one;
        }

        /// <summary>
        /// The level's new stars leave the big win stars one by one and fly into the star chest bar. A full bar
        /// shakes the chest, which then opens in the reward reveal and pays its coins.
        /// </summary>
        private IEnumerator FillChest(LevelResult result)
        {
            int size = ChestSize;
            int shown = result.ChestBefore;
            float unit = _root.lossyScale.x;
            for (int k = 0; k < result.NewStars; k++)
            {
                // Moved on already (Continue, Menu): the coins are paid, just show the true balance.
                if (_app.States.Current != AppState.LevelComplete) { _coinDisplay = -1; Refresh(); yield break; }
                int source = Mathf.Clamp(result.Stars - result.NewStars + k, 0, 2);
                Vector3 from = _winStars[source].rectTransform.position;
                Image star = Floating("star", from, 96f);
                RectTransform rt = star.rectTransform;
                Vector3 control = from + new Vector3(160f, 160f, 0f) * unit;
                _app.Audio.Play(Sfx.Select);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
                {
                    if (rt == null) yield break;
                    Vector3 to = _chestFill.TransformPoint(new Vector3(_chestFill.rect.xMax, 0f, 0f));
                    float e = Ease.Smooth(t);
                    rt.position = Vector3.Lerp(Vector3.Lerp(from, control, e), Vector3.Lerp(control, to, e), e);
                    rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, e);
                    rt.localRotation = Quaternion.Euler(0f, 0f, -300f * e);
                    yield return null;
                }
                if (rt != null) Destroy(rt.gameObject);
                shown++;
                ShowChest(Mathf.Min(shown, size));
                _app.Audio.Play(Sfx.CoinTick);
                Haptics.Tap();
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
                {
                    _chestTrack.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
                    yield return null;
                }
                _chestTrack.localScale = Vector3.one;
            }
            if (result.ChestCoins <= 0) yield break;
            if (_app.States.Current != AppState.LevelComplete) { _coinDisplay = -1; Refresh(); yield break; }

            // Full: the chest rattles, then bursts open in the reveal.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                _chestIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * 12f * (1f - t * 0.5f));
                _chestIcon.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            _chestIcon.localRotation = Quaternion.identity;
            _chestIcon.localScale = Vector3.one;
            string message = Loc.T("Star chest opened!");
            if (UiKit.Art("chest_open") == null)
            {
                _app.Audio.Play(Sfx.Coins);
                Toast(message);
                _coinDisplay = -1;
                Refresh();
            }
            else yield return RevealRoutine("chest", message, result.ChestCoins);
            ShowChest(shown - size);
        }

        private IEnumerator PopStars(LevelResult result)
        {
            int earned = result.Stars;
            for (int i = 0; i < 3; i++)
            {
                bool spin = i < earned && _winStarSpins[i] != null;
                _winStars[i].sprite = UiKit.Icon(i < earned ? "star" : "star_off");
                // Image.enabled rather than SetActive: the 3D star is a child of the flat one.
                _winStars[i].enabled = !spin;
                if (_winStarSpins[i] != null) _winStarSpins[i].gameObject.SetActive(spin);
                _winStars[i].rectTransform.localScale = GameSettings.ReducedMotion ? Vector3.one : Vector3.zero;
            }
            if (GameSettings.ReducedMotion)
            {
                // No flights: show where the chest ended up and say so if it opened.
                int after = result.ChestBefore + result.NewStars;
                ShowChest(result.ChestCoins > 0 ? after - ChestSize : after);
                if (result.ChestCoins > 0) { _app.Audio.Play(Sfx.Coins); Toast(Loc.T("Star chest opened!")); }
                yield break;
            }
            for (int i = 0; i < 3; i++)
            {
                if (i < earned)
                {
                    _app.Audio.Play(Sfx.Star);
                    _app.Audio.Play(Sfx.StarStamp);
                    if (_winStarSpins[i] != null) _winStarSpins[i].Spin(2f);
                    StartCoroutine(StarBurst(_winStars[i].rectTransform.position));
                }
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
                {
                    _winStars[i].rectTransform.localScale = Vector3.one * Ease.OutBack(t);
                    yield return null;
                }
                _winStars[i].rectTransform.localScale = Vector3.one;
            }
            if (result.NewStars > 0) yield return FillChest(result);
            yield return FlyCoins();
        }

        private readonly System.Collections.Generic.List<GameObject> _flying = new System.Collections.Generic.List<GameObject>();

        private Image Floating(string icon, Vector3 position, float size)
        {
            Image img = UiKit.AddIcon(_root.parent, icon, size);
            img.rectTransform.position = position;
            _flying.Add(img.gameObject);
            return img;
        }

        /// <summary>A coin for the reward flight: the tumbling 3D coin when its sheet exists, else the flat icon.</summary>
        private RectTransform FloatingCoin(Vector3 position, float size)
        {
            FlipBook spin = UiKit.MakeFlipBook(_root.parent, "coin_spin", size * 1.15f);
            if (spin == null) return Floating("coin", position, size).rectTransform;
            spin.Loop = true;
            spin.TurnsPerSecond = UnityEngine.Random.Range(1.6f, 2.4f);
            var rt = (RectTransform)spin.transform;
            rt.position = position;
            _flying.Add(spin.gameObject);
            return rt;
        }

        /// <summary>Little stars fly out of a win star as it lands.</summary>
        private IEnumerator StarBurst(Vector3 centre)
        {
            const int count = 8;
            float unit = _root.lossyScale.x;
            var pieces = new Image[count];
            var dirs = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) / count * Mathf.PI * 2f;
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (150f + (i % 2) * 60f) * unit;
                pieces[i] = Floating("star", centre, 46f);
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
            {
                float e = Ease.OutCubic(t);
                for (int i = 0; i < count; i++)
                {
                    if (pieces[i] == null) yield break;
                    pieces[i].rectTransform.position = centre + (Vector3)(dirs[i] * e);
                    pieces[i].rectTransform.localScale = Vector3.one * (1f - t);
                    pieces[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 180f);
                }
                yield return null;
            }
            foreach (Image piece in pieces) if (piece != null) Destroy(piece.gameObject);
        }

        /// <summary>The level reward flies from the win card up to the coin counter.</summary>
        private IEnumerator FlyCoins()
        {
            const int count = 9;
            float unit = _root.lossyScale.x;
            Vector3 from = _winCoins.rectTransform.position;
            RectTransform pill = (RectTransform)_hudCoins.transform.parent;
            Vector3 to = pill.position + new Vector3(-pill.rect.width * 0.5f * unit, 0f, 0f);
            _app.Audio.Play(Sfx.Coins);
            for (int i = 0; i < count; i++) StartCoroutine(FlyCoin(from, to, i * 0.06f, unit));
            yield return new WaitForSecondsRealtime(0.55f + count * 0.06f);
        }

        private IEnumerator FlyCoin(Vector3 from, Vector3 to, float delay, float unit)
        {
            RectTransform coinRect = FloatingCoin(from, 78f);
            GameObject coin = coinRect.gameObject;
            coinRect.localScale = Vector3.zero;
            Vector3 scatter = from + new Vector3(UnityEngine.Random.Range(-170f, 170f), UnityEngine.Random.Range(-60f, 110f), 0f) * unit;
            yield return new WaitForSecondsRealtime(delay);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
            {
                if (coin == null) yield break;
                coinRect.position = Vector3.LerpUnclamped(from, scatter, Ease.OutCubic(t));
                coinRect.localScale = Vector3.one * Ease.OutBack(t);
                yield return null;
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.42f)
            {
                if (coin == null) yield break;
                float e = Ease.InCubic(t);
                coinRect.position = Vector3.LerpUnclamped(scatter, to, e);
                coinRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
                yield return null;
            }
            if (coin == null) yield break;
            Destroy(coin.gameObject);
            _app.Audio.Play(Sfx.CoinTick);
            Haptics.Tap();
            RectTransform pill = (RectTransform)_hudCoins.transform.parent;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
            {
                pill.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.12f);
                yield return null;
            }
            pill.localScale = Vector3.one;
        }

        private void ShowLose(LoseReason reason)
        {
            _loseTitle.text = Loc.T(reason == LoseReason.OutOfMoves ? "Out of Moves" : "Station Jammed");
            _loseReason.text = Loc.T(reason == LoseReason.OutOfMoves
                ? "The move limit ran out before everyone boarded."
                : (_app.Game.Board != null && _app.Game.Board.State.HasSchedule
                    ? "The bench is full and no front passenger has a train. Undo a move or try again."
                    : "No passenger can reach a platform. Undo a move or try again."));
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

        /// <summary>The camera the UI is drawn with: none for the usual overlay canvas.</summary>
        private Camera UiCamera
        {
            get
            {
                Canvas canvas = _canvasRect != null ? _canvasRect.GetComponent<Canvas>() : null;
                return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            }
        }

        private bool WorldToCanvas(Vector3 world, out Vector2 local)
        {
            Vector3 screen = _app.Station.Cam.WorldToScreenPoint(world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, UiCamera, out local);
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
            if (_labelLayer.gameObject.activeSelf)
            {
                StationView station = _app.Station;
                for (int q = 0; q < station.GateLaneCount; q++)
                {
                    string caption = station.GateLabel(q);
                    if (caption == null || !WorldToCanvas(station.GateAnchor(q), out local)) continue;
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
