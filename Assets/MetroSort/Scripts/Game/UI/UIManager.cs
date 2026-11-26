using System;
using System.Collections;
using System.Collections.Generic;
using MetroSort.Analytics;
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
        private Button _dailyButton, _winAdButton, _loseUndo, _loseContinue, _loseAd, _loseHome, _removeAdsButton;
        private Text _loseLeft, _losePrice;
        // Coin pack buy buttons by product id: their labels follow the store's own prices (StoreCatalog.PricesChanged).
        private readonly Dictionary<string, Button> _priceButtons = new Dictionary<string, Button>();
        private GameObject _losePriceCoin;
        private Text _loseUndoBadge;
        private Image _loseUndoDot;
        private RectTransform _loseRetry, _loseOffers;
        // Pause card (separate from settings): level, progress, resume / restart / menu and quick sound toggles.
        private GameObject _pause;
        private Text _pauseLevel, _pauseProgress;
        private Button _pauseMusic, _pauseSound;
        private float _musicBeforeMute = 0.6f, _soundBeforeMute = 0.9f;
        private Slider _musicSlider, _soundSlider;
        private Button _resumeButton, _restartButton, _quitButton, _restoreButton, _privacyButton, _creditsButton;
        private GameObject _linksRow, _credits, _languages;
        private readonly Button[] _boosterButtons = new Button[4];
        private readonly Text[] _boosterBadges = new Text[4];
        private readonly Text[] _boosterPrices = new Text[4];
        private readonly Image[] _boosterBadgeDots = new Image[4];
        private readonly GameObject[] _boosterPriceRows = new GameObject[4];
        private readonly CanvasGroup[] _boosterGroups = new CanvasGroup[4];
        private static readonly Color BadgeRed = new Color(0.90f, 0.16f, 0.20f);
        private static readonly Color BadgeGreen = new Color(0.28f, 0.74f, 0.20f);

        /// <summary>The booster's picture: the painted set (Art/booster_*, MetroSort-Media/HUD-Gorsel-Promptlari.md) when present.</summary>
        private static string BoosterIcon(int i)
        {
            string painted = "booster_" + BoosterIcons[i];
            return UiKit.Art(painted) != null ? painted : BoosterIcons[i];
        }
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
        private Button _refillButton, _livesAdButton, _livesPlayButton, _livesUnlimitedButton;
        private Image[] _lifePips;
        private Image _menuInfinity, _noLivesInfinity;
        private int _livesShown = -1;
        private Coroutine _livesFill;
        private readonly Image[] _winStars = new Image[3];
        private readonly FlipBook[] _winStarSpins = new FlipBook[3];
        // Star chest row on the win card (see ProgressionManager.CompleteLevel).
        private RectTransform _chestIcon;
        private Image[] _chestStars;
        private Text _chestNote, _chestPrize;
        private ChestIdle _chestIdle;
        private Image _winChip;
        private Button _winHome, _playButton;
        private static readonly Color HardRed = new Color(0.90f, 0.22f, 0.24f);
        private static readonly Color SuperPurple = new Color(0.58f, 0.32f, 0.88f);
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
            BuildLanguages();
            BuildCredits();
            BuildPause();
            BuildShop();
            BuildLevels();
            BuildWin();
            BuildLose();
            BuildMoreGames();
            BuildNoLives();
            BuildStarterOffer();
            BuildToast();

            GameServices s = app.Services;
            app.States.Changed += OnStateChanged;
            StoreCatalog.PricesChanged += Refresh;
            s.Economy.Changed += Refresh;
            s.Boosters.Changed += Refresh;
            s.Lives.Changed += RefreshLives;
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
            StoreCatalog.PricesChanged -= Refresh;
            s.Economy.Changed -= Refresh;
            s.Boosters.Changed -= Refresh;
            s.Store.Changed -= Refresh;
            s.Achievements.Unlocked -= OnAchievement;
            _app.Game.BoardChanged -= Refresh;
            _app.Game.LevelWon -= ShowWin;
            _app.Game.LevelLost -= ShowLose;
            _app.Game.Message -= Toast;
            if (_app.MoreGames != null) _app.MoreGames.Changed -= RefreshMoreGames;
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
                Text title = UiKit.MakeText(plate.transform, Application.productName.ToUpperInvariant(), 116, Color.white);
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
            _playButton = play;

            Button levels = Btn(menu, "Levels", Palette.UiPrimary, OpenLevels, 300f, 130f);
            UiKit.Place((RectTransform)levels.transform, 0.5f, 0f, -330f, 150f, 300f, 130f);
            Button shop = Btn(menu, "Shop", Palette.UiPrimary, OpenShop, 300f, 130f);
            UiKit.Place((RectTransform)shop.transform, 0.5f, 0f, 0f, 150f, 300f, 130f);
            _dailyButton = Btn(menu, "Daily Gift", new Color(0.96f, 0.62f, 0.20f), OnDaily, 300f, 130f);
            UiKit.Place((RectTransform)_dailyButton.transform, 0.5f, 0f, 330f, 150f, 300f, 130f);
            // The studio's other games: a quiet pill under the three buttons, shown once there is a game to offer.
            _moreGamesButton = Btn(menu, "Our Games", new Color(0.10f, 0.18f, 0.42f), OpenMoreGames, 520f, 88f, 36);
            UiKit.Place((RectTransform)_moreGamesButton.transform, 0.5f, 0f, 0f, 40f, 520f, 88f);
            _moreGamesIcon = UiKit.MakeImage(_moreGamesButton.transform, "GameIcon", null, Color.white);
            _moreGamesIcon.preserveAspect = true;
            UiKit.Place(_moreGamesIcon.rectTransform, 0f, 0.5f, 18f, 4f, 62f, 62f);
            UiKit.Stretch(UiKit.LabelOf(_moreGamesButton).rectTransform, 92f, 12f, 24f, 6f);
            _moreGamesButton.gameObject.SetActive(false);
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

        /// <summary>
        /// Behind the HUD, over the station: a soft vignette that darkens the corners and draws the eye to the
        /// middle, and a few faint sunbeams slanting in from the top left that sway very slowly.
        /// </summary>
        private void BuildAtmosphere(RectTransform hud)
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.55f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.2f, r));
                    px[y * N + x] = new Color32(10, 14, 40, (byte)(255f * 0.62f * a));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var vignette = UiKit.MakeRect("Vignette", hud).gameObject.AddComponent<RawImage>();
            vignette.texture = tex;
            vignette.raycastTarget = false;
            UiKit.Stretch(vignette.rectTransform, -60f, -60f, -60f, -60f);

            // Sunbeams: long soft bars, brighter in the middle of their width, fading along their length.
            var beam = new Texture2D(16, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Beam" };
            var bp = new Color32[16 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 16; x++)
                {
                    float across = 1f - Mathf.Abs((x + 0.5f) / 16f * 2f - 1f);
                    float along = Mathf.Clamp01((y + 0.5f) / 64f);
                    bp[y * 16 + x] = new Color32(255, 246, 220, (byte)(255f * across * across * along * along));
                }
            beam.SetPixels32(bp);
            beam.Apply();
            float[] xs = { -380f, -170f, 60f };
            float[] widths = { 150f, 90f, 120f };
            for (int i = 0; i < xs.Length; i++)
            {
                var ray = UiKit.MakeRect("Sunbeam", hud).gameObject.AddComponent<RawImage>();
                ray.texture = beam;
                ray.raycastTarget = false;
                ray.color = new Color(1f, 1f, 1f, 0.17f);
                RectTransform rt = UiKit.Place(ray.rectTransform, 0.5f, 1f, xs[i], 40f, widths[i], 1500f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 28f);
                var sway = ray.gameObject.AddComponent<Sway2D>();
                sway.Phase = i * 1.7f;
            }
        }

        private void BuildHud()
        {
            RectTransform hud = UiKit.Stretch(UiKit.MakeRect("Hud", _root));
            _hud = hud.gameObject;
            BuildAtmosphere(hud);

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
            // A glassy highlight along the top of the fill, and a darker band along its foot, for a rounded tube.
            Image shine = UiKit.MakeImage(fill.transform, "Shine", UiPremium.Pill, new Color(1f, 1f, 1f, 0.32f));
            RectTransform shineRect = shine.rectTransform;
            shineRect.anchorMin = new Vector2(0f, 0.52f);
            shineRect.anchorMax = new Vector2(1f, 1f);
            shineRect.offsetMin = new Vector2(9f, 0f);
            shineRect.offsetMax = new Vector2(-9f, -5f);
            Image foot = UiKit.MakeImage(fill.transform, "Foot", UiPremium.Pill, new Color(0.05f, 0.35f, 0.08f, 0.35f));
            RectTransform footRect = foot.rectTransform;
            footRect.anchorMin = new Vector2(0f, 0f);
            footRect.anchorMax = new Vector2(1f, 0.32f);
            footRect.offsetMin = new Vector2(6f, 2f);
            footRect.offsetMax = new Vector2(-6f, 0f);
            // Who is counted: a passenger badge at the left end of the bar.
            Image riderDisc = UiKit.MakeImage(track, "RiderBadge", UiPremium.Round, UiPremium.Blue);
            RectTransform riderRect = UiKit.Place(riderDisc.rectTransform, 0f, 0.5f, -30f, 2f, 66f, 66f);
            riderRect.pivot = new Vector2(0.5f, 0.5f);
            Image rider = UiKit.MakeImage(riderDisc.transform, "Rider", UiPremium.Rider, Color.white);
            UiKit.Stretch(rider.rectTransform, 12f, 14f, 12f, 10f);
            rider.preserveAspect = true;
            _hudProgress = UiPremium.Heading(track, "", 30, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Stretch(_hudProgress.rectTransform, 10f, 4f, 10f, 2f);
            _hudProgress.resizeTextForBestFit = true;
            _hudProgress.resizeTextMinSize = 16;
            _hudProgress.resizeTextMaxSize = 30;
            _hudCoins = CoinPill(bar, 1f, 0.5f, -20f, 0f);
            // No shop shortcut during a level: the plus would only crowd the counter on the bar.
            Transform hudPlus = _hudCoins.transform.parent.Find("RoundButton");
            if (hudPlus != null) hudPlus.gameObject.SetActive(false);
            // A smaller counter than the menu's, kept well inside the bar: the pill used to keep its menu size and
            // run out over the bar's gold rim on the right.
            var coinHolder = (RectTransform)_hudCoins.transform.parent;
            UiKit.Place(coinHolder, 1f, 0.5f, -36f, 0f, 230f, 86f);
            Transform coinPill = coinHolder.Find("Pill");
            if (coinPill != null) UiKit.Place((RectTransform)coinPill, 0.5f, 0.5f, 14f, 0f, 196f, 66f);
            UiKit.Stretch(_hudCoins.rectTransform, 86f, 8f, 20f, 4f);
            BuildScheduleBar(hud);

            // Boosters: four chunky buttons on a gold-rimmed navy tray that matches the top bar. Each big icon pokes
            // out over its button's top edge; a red badge in the corner counts the charges, and once they run out a
            // green plus and the coin price show instead. A booster that can't be used now dims, keeping its colour.
            RectTransform tray = UiKit.Place(UiKit.MakeRect("BoosterTray", hud), 0.5f, 0f, 0f, 14f, 1044f, 206f);
            Image trayRim = UiKit.MakeImage(tray, "Rim", UiPremium.Pill, new Color(1f, 0.74f, 0.20f));
            UiKit.Stretch(trayRim.rectTransform);
            trayRim.pixelsPerUnitMultiplier = 0.75f;
            var trayShadow = trayRim.gameObject.AddComponent<Shadow>();
            trayShadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.45f);
            trayShadow.effectDistance = new Vector2(0f, -8f);
            Image trayBoard = UiKit.MakeImage(tray, "Board", UiPremium.Pill, new Color(0.10f, 0.17f, 0.38f, 0.97f));
            UiKit.Stretch(trayBoard.rectTransform, 7f, 7f, 7f, 7f);
            trayBoard.pixelsPerUnitMultiplier = 0.75f;
            Action[] actions = { () => _app.Game.UseUndo(), () => _app.Game.UseHint(), () => _app.Game.UseExtraPlatform(), () => _app.Game.UseShuffle() };
            for (int i = 0; i < 4; i++)
            {
                Action action = actions[i];
                // No tile behind the booster: the painted icon stands straight on the tray. The touch area is an
                // invisible box the size of the slot, and the whole booster sinks a little when pressed.
                Image hit = UiKit.MakeImage(tray, "Button_" + BoosterNames[i], null, new Color(0f, 0f, 0f, 0f), true);
                UiKit.Place(hit.rectTransform, 0.5f, 0f, (i - 1.5f) * 250f, 18f, 230f, 174f);
                // Booster buttons play their own success/failure sounds, so no generic click here.
                var b = hit.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => action());
                hit.gameObject.AddComponent<PressScale>();
                _boosterGroups[i] = hit.gameObject.AddComponent<CanvasGroup>();
                Image icon = UiKit.AddIcon(b.transform, BoosterIcon(i), 168f, 0.5f, 0.5f, 0f, 14f);
                var iconShadow = icon.gameObject.AddComponent<Shadow>();
                iconShadow.effectColor = new Color(0f, 0f, 0.08f, 0.35f);
                iconShadow.effectDistance = new Vector2(0f, -6f);

                // Price row along the bottom, shown only when no charges are left.
                RectTransform price = UiKit.Place(UiKit.MakeRect("Price", b.transform), 0.5f, 0f, 0f, 12f, 150f, 46f);
                Image pricePill = UiKit.MakeImage(price, "Pill", UiPremium.Pill, new Color(0.03f, 0.06f, 0.18f, 0.6f));
                UiKit.Stretch(pricePill.rectTransform);
                UiKit.AddIcon(price, "coin", 42f, 0f, 0.5f, 26f, 0f);
                _boosterPrices[i] = UiPremium.Heading(price, "", 30, new Color(1f, 0.93f, 0.55f), new Color(0.22f, 0.10f, 0f, 0.9f));
                UiKit.Stretch(_boosterPrices[i].rectTransform, 50f, 2f, 12f, 0f);
                _boosterPriceRows[i] = price.gameObject;

                // Badge on a white ring at the icon's top-right shoulder: the count, or a plus.
                Image ring = UiKit.MakeImage(b.transform, "BadgeRing", UiKit.Circle, Color.white);
                UiKit.Place(ring.rectTransform, 0.5f, 0.5f, 74f, 62f, 68f, 68f);
                var ringShadow = ring.gameObject.AddComponent<Shadow>();
                ringShadow.effectColor = new Color(0f, 0f, 0.08f, 0.35f);
                ringShadow.effectDistance = new Vector2(0f, -4f);
                Image dot = UiKit.MakeImage(ring.transform, "Badge", UiKit.Circle, BadgeRed);
                UiKit.Stretch(dot.rectTransform, 6f, 6f, 6f, 6f);
                _boosterBadgeDots[i] = dot;
                _boosterBadges[i] = UiPremium.Heading(dot.transform, "", 34, Color.white, new Color(0.35f, 0.02f, 0.04f, 0.95f));
                UiKit.Stretch(_boosterBadges[i].rectTransform, 0f, 3f, 0f, 0f);
                _boosterButtons[i] = b;
            }

            Image banner = UiKit.MakeImage(hud, "Tutorial", UiKit.Card, Color.white);
            UiKit.Place(banner.rectTransform, 0.5f, 0f, 0f, 250f, 990f, 116f);
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

        // Where the fingertip is in Art/icon_tap.png (256 px, measured): the hand is pivoted on it.
        private static readonly Vector2 FingerTip = new Vector2(80f / 256f, 1f - 73f / 256f);
        private const float HandTilt = 20f;
        private RectTransform _pointerHand, _pointerRipple;
        private Image _pointerRippleImage;

        /// <summary>
        /// The next thing to touch (a tutorial step or a paid hint): a cartoon hand whose fingertip taps it, with a
        /// ring rippling out on every tap. The hand trails down and to the right, so the passenger it points at stays
        /// in view. (It used to be a yellow disc over the passenger's chest, which hid who was meant.)
        /// </summary>
        private void BuildPointer(Transform canvas)
        {
            _pointer = UiKit.Place(UiKit.MakeRect("Pointer", canvas), 0.5f, 0.5f, 0f, 0f, 10f, 10f);
            _pointerRippleImage = UiKit.MakeImage(_pointer, "Ripple", UiPremium.Ring, new Color(1f, 0.93f, 0.45f, 0f));
            _pointerRipple = UiKit.Place(_pointerRippleImage.rectTransform, 0.5f, 0.5f, 0f, 0f, 150f, 150f);
            Sprite art = UiKit.Art("icon_tap");
            Image hand = UiKit.MakeImage(_pointer, "Hand", art != null ? art : UiKit.Circle, art != null ? Color.white : UiPremium.Gold);
            hand.preserveAspect = true;
            _pointerHand = hand.rectTransform;
            _pointerHand.anchorMin = _pointerHand.anchorMax = new Vector2(0.5f, 0.5f);
            _pointerHand.pivot = art != null ? FingerTip : new Vector2(0.5f, 0.5f);
            _pointerHand.sizeDelta = new Vector2(210f, 210f);
            _pointerHand.anchoredPosition = Vector2.zero;
            // Tilted so the arm comes in from the side rather than over the passenger queued behind.
            _pointerHand.localRotation = Quaternion.Euler(0f, 0f, HandTilt);
            var shadow = hand.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.05f, 0.15f, 0.30f);
            shadow.effectDistance = new Vector2(6f, -8f);
            _pointer.gameObject.SetActive(false);
        }

        /// <summary>One tap every 0.9 s: the hand glides in, presses (the ring starts), and lifts away again.</summary>
        private void AnimatePointer()
        {
            if (GameSettings.ReducedMotion)
            {
                _pointerHand.anchoredPosition = Vector2.zero;
                _pointerHand.localScale = Vector3.one;
                _pointerRippleImage.color = new Color(1f, 0.93f, 0.45f, 0.75f);
                _pointerRipple.localScale = Vector3.one * 0.8f;
                return;
            }
            float t = Time.unscaledTime / 0.9f;
            float phase = t - Mathf.Floor(t);
            float press = phase < 0.40f ? Ease.Smooth(phase / 0.40f) : phase < 0.55f ? 1f : 1f - Ease.Smooth((phase - 0.55f) / 0.45f);
            // Back along the arm (down and to the side, following the tilt) when lifted.
            Vector2 arm = Quaternion.Euler(0f, 0f, HandTilt) * new Vector2(1f, -1f).normalized;
            _pointerHand.anchoredPosition = arm * 48f * (1f - press);
            _pointerHand.localScale = Vector3.one * (1f - 0.08f * press);
            float ripple = phase >= 0.42f ? (phase - 0.42f) / 0.58f : -1f;
            _pointerRippleImage.color = new Color(1f, 0.93f, 0.45f, ripple < 0f ? 0f : 0.95f * (1f - ripple));
            _pointerRipple.localScale = Vector3.one * (ripple < 0f ? 0.3f : 0.3f + 0.9f * ripple);
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
            if (GameSettings.ReducedMotion || !HasRewardBox(prefix) || !isActiveAndEnabled)
            {
                _app.Audio.Play(Sfx.Coins);
                Toast(message);
                return;
            }
            StartCoroutine(RevealRoutine(prefix, message, coins));
        }

        // The painted reward box stands with its base here (canvas units from the centre) and is drawn 1.25x its
        // pixel size, about 640 units wide.
        private const float BoxFloor = -140f, BoxScale = 1.25f;

        private static void ShowBoxState(Image art, Sprite state)
        {
            art.sprite = state;
            art.rectTransform.sizeDelta = new Vector2(state.rect.width, state.rect.height) * BoxScale;
        }

        /// <summary>Stars (and, out of a gift, confetti) burst from the box's mouth, fan out, and fall as they fade.</summary>
        private IEnumerator Sparkles(Transform parent, Vector2 at, bool confetti)
        {
            Sprite star = UiKit.Art("star") ?? UiKit.Icon("star");
            Color[] paper = { new Color(1f, 0.82f, 0.22f), new Color(1f, 0.42f, 0.55f), new Color(0.30f, 0.76f, 1f), new Color(0.52f, 0.92f, 0.40f) };
            int count = confetti ? 28 : 18;
            var bits = new List<Image>();
            var velocity = new List<Vector2>();
            var spin = new List<float>();
            for (int i = 0; i < count; i++)
            {
                bool isStar = star != null && (!confetti || i % 3 == 0);
                Image img = isStar
                    ? UiKit.MakeImage(parent, "Sparkle", star, new Color(1f, 0.97f, 0.82f, 1f))
                    : UiKit.MakeImage(parent, "Confetti", UiKit.Rounded, paper[i % paper.Length]);
                img.preserveAspect = isStar;
                float size = isStar ? UnityEngine.Random.Range(36f, 72f) : UnityEngine.Random.Range(14f, 22f);
                UiKit.Place(img.rectTransform, 0.5f, 0.5f, at.x, at.y, size, isStar ? size : size * 1.7f);
                float angle = UnityEngine.Random.Range(25f, 155f) * Mathf.Deg2Rad;
                bits.Add(img);
                velocity.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(700f, 1350f));
                spin.Add(UnityEngine.Random.Range(-540f, 540f));
            }
            const float life = 1.2f;
            for (float t = 0f; t < life; t += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                for (int i = 0; i < bits.Count; i++)
                {
                    Image img = bits[i];
                    if (img == null) continue; // the reveal was closed early
                    velocity[i] = velocity[i] * Mathf.Exp(-2.4f * dt) + new Vector2(0f, -1500f * dt);
                    RectTransform rt = img.rectTransform;
                    rt.anchoredPosition += velocity[i] * dt;
                    rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + spin[i] * dt);
                    Color c = img.color;
                    c.a = 1f - Mathf.Clamp01((t - 0.5f) / (life - 0.5f));
                    img.color = c;
                }
                yield return null;
            }
            foreach (Image img in bits)
                if (img != null) Destroy(img.gameObject);
        }

        /// <summary>True when a reward box can be shown opening: its painted states or the older flip-book.</summary>
        private static bool HasRewardBox(string prefix)
        {
            return (UiKit.Art(prefix + "_0") != null && UiKit.Art(prefix + "_2") != null) || UiKit.Art(prefix + "_open") != null;
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
            // The box: the three painted states (Art/<prefix>_0 shut, _1 bursting open, _2 open and full, cut from
            // MetroSort-Media/Gorseller/<prefix>-states.png) when they are there, else the older rendered flip-book.
            Sprite shut = UiKit.Art(prefix + "_0"), bursting = UiKit.Art(prefix + "_1"), full = UiKit.Art(prefix + "_2");
            bool painted = shut != null && bursting != null && full != null;
            FlipBook box = null;
            RectTransform b, boxRoot = null;
            Image boxArt = null;
            if (painted)
            {
                // Standing on a soft shadow and scaled from its base, so the swaps between states stay planted.
                boxRoot = UiKit.Place(UiKit.MakeRect("Box", dim.transform), 0.5f, 0.5f, 0f, BoxFloor, 10f, 10f);
                Image shadow = UiKit.MakeImage(boxRoot, "Shadow", UiKit.Circle, new Color(0f, 0f, 0.06f, 0.38f));
                UiKit.Place(shadow.rectTransform, 0.5f, 0.5f, 0f, 8f, 540f, 70f);
                boxArt = UiKit.MakeImage(boxRoot, "Art", shut, Color.white);
                RectTransform art = boxArt.rectTransform;
                art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
                art.pivot = new Vector2(0.5f, 0f);
                art.anchoredPosition = Vector2.zero;
                ShowBoxState(boxArt, shut);
                // The coins leave from the open box's mouth.
                b = UiKit.Place(UiKit.MakeRect("Mouth", dim.transform), 0.5f, 0.5f, 0f, BoxFloor + 300f, 10f, 10f);
            }
            else
            {
                box = UiKit.MakeFlipBook(dim.transform, prefix + "_open", 700f);
                b = (RectTransform)box.transform;
                b.anchoredPosition = new Vector2(0f, 160f);
            }
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

            _app.Audio.Play(Sfx.ChestOpen);
            float t0 = Time.unscaledTime;
            if (painted)
            {
                // Drop onto its shadow, then rattle harder and harder while light starts to leak out.
                const float land = 0.35f, rattle = 0.7f;
                while (Time.unscaledTime - t0 < land + rattle)
                {
                    if (dim == null) yield break;
                    float t = Time.unscaledTime - t0;
                    dim.color = new Color(0.03f, 0.05f, 0.14f, Mathf.Min(1f, t * 5f) * 0.86f);
                    if (t < land)
                    {
                        float k = t / land;
                        boxRoot.anchoredPosition = new Vector2(0f, BoxFloor + 220f * (1f - Ease.OutCubic(k)));
                        boxRoot.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Ease.OutBack(k));
                    }
                    else
                    {
                        float k = (t - land) / rattle, wave = Mathf.Sin(k * Mathf.PI * 14f);
                        boxRoot.anchoredPosition = new Vector2(wave * 6f * k, BoxFloor);
                        boxRoot.localRotation = Quaternion.Euler(0f, 0f, wave * (2f + 8f * k));
                        float squash = 0.05f * k * Mathf.Sin(k * Mathf.PI * 14f + 1.3f);
                        boxRoot.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                        if (rays != null) rays.color = new Color(1f, 0.92f, 0.6f, 0.4f * k);
                    }
                    yield return null;
                }
                boxRoot.anchoredPosition = new Vector2(0f, BoxFloor);
                boxRoot.localRotation = Quaternion.identity;
                ShowBoxState(boxArt, bursting);
                StartCoroutine(Sparkles(dim.transform, b.anchoredPosition, prefix == "gift"));
            }
            else
            {
                // Drop in, then the box rattles and bursts (the burst is frame 10 of 24 in the sheet).
                const float play = 1.5f, burstAt = play * 10f / 24f;
                box.PlayOnce(play);
                while (Time.unscaledTime - t0 < burstAt)
                {
                    if (dim == null) yield break;
                    float t = Time.unscaledTime - t0;
                    dim.color = new Color(0.03f, 0.05f, 0.14f, Mathf.Min(1f, t * 5f) * 0.86f);
                    b.localScale = Vector3.one * Ease.OutBack(Mathf.Min(1f, t / 0.3f));
                    yield return null;
                }
            }

            // Burst: flash, rays, the amount, the conductor. The painted box springs open, squashed by the blast,
            // then stretching up and settling, as the bursting state gives way to the open, overflowing one.
            _app.Audio.Play(Sfx.Coins);
            Haptics.Success();
            if (guide != null) guide.sprite = UiKit.Art("conductor_cheer") ?? guide.sprite;
            const float burst = 0.7f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / burst)
            {
                if (dim == null) yield break;
                float s = t * burst;
                float fade = Mathf.Clamp01(1f - s / 0.3f);
                flash.color = new Color(1f, 0.98f, 0.9f, 0.9f * fade * fade);
                if (rays != null)
                {
                    rays.color = new Color(1f, 0.92f, 0.6f, Mathf.Lerp(painted ? 0.4f : 0f, 0.95f, Mathf.Min(1f, s * 4f)));
                    rays.rectTransform.localScale = Vector3.one * (0.8f + 0.2f * Ease.OutBack(Mathf.Min(1f, s * 3f)));
                }
                amount.color = new Color(1f, 1f, 1f, Mathf.Min(1f, s * 4f));
                amount.rectTransform.localScale = Vector3.one * Ease.OutBack(Mathf.Min(1f, s * 2.4f));
                label.color = new Color(1f, 1f, 1f, Mathf.Min(1f, s * 2f));
                if (guide != null) guide.color = new Color(1f, 1f, 1f, Mathf.Min(1f, s * 3f));
                if (painted)
                {
                    if (s > 0.09f && boxArt.sprite != full) ShowBoxState(boxArt, full);
                    float spring = Mathf.Exp(-6f * s) * Mathf.Cos(s * Mathf.PI * 2f * 2.6f);
                    boxRoot.localScale = new Vector3(1f + 0.12f * spring, 1f - 0.16f * spring, 1f);
                }
                else b.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (painted && boxRoot != null) boxRoot.localScale = Vector3.one;
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
            _musicSlider = SliderRow(body, "Music", data.MusicVolume, v => { data.MusicVolume = v; _app.Audio.MusicVolume = v; }, ref y);
            _soundSlider = SliderRow(body, "Sound", data.SfxVolume, v => { data.SfxVolume = v; _app.Audio.SfxVolume = v; }, ref y);
            _assistSwitch = SwitchRow(body, "Assisted taps", () => { data.AssistedMode = !data.AssistedMode; ApplySettings(); }, ref y);
            _motionSwitch = SwitchRow(body, "Reduced motion", () => { data.ReducedMotion = !data.ReducedMotion; ApplySettings(); }, ref y);
            _fastSwitch = SwitchRow(body, "Fast trains", () => { data.FastTrains = !data.FastTrains; ApplySettings(); }, ref y);

            // Language: the row's button names the current language and opens the list of them all.
            Image lang = UiPremium.MakeTile(body, "LanguageRow", 0.5f, 1f, 0f, y, 872f, 118f);
            float langLeft = RowIconFor(lang.transform, "Language");
            Text langLabel = UiKit.MakeText(lang.transform, "Language", 42, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Stretch(langLabel.rectTransform, langLeft, 10f, 300f, 4f);
            Button langButton = Btn(lang.transform, "", UiPremium.Blue, OpenLanguages, 230f, 88f, 36);
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

            // Under it: the privacy policy (or, where the law asks for it, the ad consent choices) and the credits.
            RectTransform links = UiKit.Place(UiKit.MakeRect("Links", body), 0.5f, 1f, 0f, y - 176f, 860f, 100f);
            _linksRow = links.gameObject;
            _privacyButton = Btn(links, "Privacy", UiPremium.DeepBlue, OpenPrivacy, 415f, 100f, 38);
            UiKit.Place((RectTransform)_privacyButton.transform, 0f, 0.5f, 0f, 0f, 415f, 100f);
            _creditsButton = Btn(links, "Credits", UiPremium.DeepBlue, () => _credits.SetActive(true), 415f, 100f, 38);
            UiKit.Place((RectTransform)_creditsButton.transform, 1f, 0.5f, 0f, 0f, 415f, 100f);
        }

        /// <summary>The studio's privacy page for the current language; empty when none is configured.</summary>
        private string PrivacyPage
        {
            get
            {
                MonetizationConfig money = _app.Services.Monetization;
                return money != null ? money.PrivacyPolicyFor(Loc.Language) : "";
            }
        }

        /// <summary>
        /// Where the ad consent law applies (EEA, UK), the consent choices can be changed here, as Google requires;
        /// their form links the privacy policy. Everywhere else the button opens the policy page itself.
        /// </summary>
        private void OpenPrivacy()
        {
            var ads = _app.Services.Ads;
            if (ads.PrivacyOptionsRequired)
            {
                ads.ShowPrivacyOptions(error => { if (error != null) Toast("Privacy options are not available right now"); });
                return;
            }
            string page = PrivacyPage;
            if (!string.IsNullOrEmpty(page)) Application.OpenURL(page);
        }

        // ------------------------------------------------------------------ language picker

        /// <summary>
        /// Every language the game speaks, each named in itself (so a player can always find their own); the
        /// current one is green. Picking one saves it and rebuilds the UI in it.
        /// </summary>
        private void BuildLanguages()
        {
            RectTransform body;
            string[] languages = Loc.Languages;
            _languages = UiPremium.Modal(_root, "Languages", "Language", 760f, 190f + languages.Length * 124f, () => { _app.Audio.Play(Sfx.Click); CloseLanguages(); }, out body);
            for (int i = 0; i < languages.Length; i++)
            {
                string language = languages[i];
                bool current = language == Loc.Language;
                Button b = Btn(body, "", current ? UiPremium.Green : UiPremium.Blue, () => PickLanguage(language), 560f, 104f, 44);
                UiKit.Place((RectTransform)b.transform, 0.5f, 1f, 0f, -6f - i * 124f, 560f, 104f);
                // Set after creation: labels made through UiKit are translated, a language's own name must not be.
                UiKit.LabelOf(b).text = Loc.NativeName(language);
            }
        }

        // One card at a time: the taller settings card would show its ribbon above the language list.
        private void OpenLanguages()
        {
            _settings.SetActive(false);
            _languages.SetActive(true);
        }

        private void CloseLanguages()
        {
            _languages.SetActive(false);
            _settings.SetActive(true);
        }

        private void PickLanguage(string language)
        {
            if (language == Loc.Language)
            {
                CloseLanguages();
                return;
            }
            // The UI is rebuilt in the new language and reopens on the settings card.
            _languages.SetActive(false);
            SaveData data = _app.Services.Save.Data;
            data.Language = language;
            _app.Services.Save.Save();
            _app.ChangeLanguage(language);
        }

        // ------------------------------------------------------------------ credits

        private void BuildCredits()
        {
            RectTransform body;
            // As wide as the settings panel it opens over, so that panel's close button does not peek out beside this one.
            _credits = UiPremium.Modal(_root, "Credits", "Credits", 940f, 1300f, () => { _app.Audio.Play(Sfx.Click); _credits.SetActive(false); }, out body);
            Image sheet = UiPremium.MakeTile(body, "Sheet", 0.5f, 0.5f, 0f, 0f, 820f, 1120f);
            UiKit.Stretch(sheet.rectTransform, 4f, 6f, 4f, 4f);
            Text text = UiKit.MakeText(sheet.transform, "", 30, UiPremium.Ink, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Stretch(text.rectTransform, 28f, 24f, 28f, 24f);
            text.lineSpacing = 0.9f;
            text.supportRichText = true;
            text.text = CreditsText();
        }

        /// <summary>
        /// Who made what. Every sound is either the game's own (synthesised or made for it) or Kenney's CC0 set, which
        /// needs no credit but gets one; the CC BY train recordings were dropped on 2026-11-21, so nothing here is a
        /// licence requirement except the font and SDK lines (their full texts travel in Resources/Fonts and
        /// Resources/Legal). Add a line before shipping any CC BY asset.
        /// </summary>
        private static string CreditsText()
        {
            const string heading = "<size=36><color=#B23A2B>{0}</color></size>";
            var lines = new[]
            {
                "<size=50><color=#1D3F86>" + Application.productName + "</color></size>",
                Loc.T("A StagFunny game"),
                "",
                string.Format(heading, Loc.T("Sound effects")),
                "Kenney (kenney.nl), CC0",
                "",
                string.Format(heading, Loc.T("Font")),
                "Baloo 2, The Baloo 2 Project Authors",
                "SIL Open Font License 1.1",
                "",
                string.Format(heading, Loc.T("Software")),
                "Google Mobile Ads Unity plugin, Apache 2.0",
                "RevenueCat Purchases SDK, MIT",
                Loc.T("Made with Unity"),
            };
            return string.Join("\n", lines);
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

        private Slider SliderRow(RectTransform body, string label, float value, Action<float> onChanged, ref float y)
        {
            Image tile = UiPremium.MakeTile(body, label + "Row", 0.5f, 1f, 0f, y, 872f, 118f);
            float left = RowIconFor(tile.transform, label);
            Text text = UiKit.MakeText(tile.transform, label, 42, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(text.rectTransform, 0f, 0.5f, left, 4f, 200f, 90f);
            float width = 872f - left - 200f - 50f;
            Slider slider = UiKit.MakeSlider(tile.transform, value, v => onChanged(v), width);
            UiKit.Place((RectTransform)slider.transform, 1f, 0.5f, -36f, 4f, width, 70f);
            y -= 136f;
            return slider;
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



        // The shop: the coin balance stays at the top, everything under it scrolls. LayoutShop stacks the starter
        // offer (only while it runs) over the rest: bundles, coin packs, boosters and Remove Ads.
        private const float ShopWidth = 932f;
        private const float StarterSectionHeight = 470f;
        private const float BundleHeight = 236f;
        private static readonly Color StarterBoard = new Color(0.36f, 0.15f, 0.60f);
        private static readonly Color FeaturedBoard = new Color(0.13f, 0.22f, 0.50f);
        private RectTransform _shopContent, _shopStarter, _shopRest;
        private ScrollRect _shopScroll;
        private float _shopRestHeight;
        private Text _shopStarterTimer;

        private void BuildShop()
        {
            RectTransform body;
            _shop = UiPremium.Modal(_root, "Shop", "Shop", 1000f, 1560f, () => { _app.Audio.Play(Sfx.Click); _shop.SetActive(false); }, out body);

            // Coin balance under the ribbon.
            _shopCoins = CoinPill(body, 0.5f, 1f, 0f, 0f);
            Transform plus = _shopCoins.transform.parent.Find("RoundButton");
            if (plus != null) plus.gameObject.SetActive(false);

            // The scrolling list under it; the transparent viewport takes drags that start between the cards.
            Image viewport = UiKit.MakeImage(body, "Viewport", null, new Color(0f, 0f, 0f, 0f), true);
            UiKit.Stretch(viewport.rectTransform, 0f, 0f, 0f, 104f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _shopContent = UiKit.MakeRect("Content", viewport.transform);
            _shopContent.anchorMin = new Vector2(0f, 1f);
            _shopContent.anchorMax = new Vector2(1f, 1f);
            _shopContent.pivot = new Vector2(0.5f, 1f);
            _shopContent.anchoredPosition = Vector2.zero;
            _shopContent.sizeDelta = Vector2.zero;
            _shopScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _shopScroll.viewport = viewport.rectTransform;
            _shopScroll.content = _shopContent;
            _shopScroll.horizontal = false;
            _shopScroll.vertical = true;
            _shopScroll.movementType = ScrollRect.MovementType.Elastic;
            _shopScroll.scrollSensitivity = 40f;

            _shopStarter = ShopSection("Starter", StarterSectionHeight);
            ShopStarter(_shopStarter);
            _shopRest = ShopSection("Offers", 0f);
            _shopRestHeight = BuildShopOffers(_shopRest);
            _shopRest.sizeDelta = new Vector2(ShopWidth, _shopRestHeight);
            LayoutShop();
            Mascot((RectTransform)body.parent, "thumbs", 220f, -390f, -14f);
        }

        private RectTransform ShopSection(string name, float height)
        {
            return UiKit.Place(UiKit.MakeRect(name, _shopContent), 0.5f, 1f, 0f, 0f, ShopWidth, height);
        }

        /// <summary>Shows the starter offer while it runs, stacks the rest under it and scrolls back to the top.</summary>
        private void LayoutShop()
        {
            if (_shopContent == null) return;
            bool starter = _app.Services.Store.StarterAvailable(Now());
            _shopStarter.gameObject.SetActive(starter);
            float top = starter ? StarterSectionHeight : 0f;
            _shopRest.anchoredPosition = new Vector2(0f, -top);
            _shopContent.sizeDelta = new Vector2(0f, top + _shopRestHeight);
            _shopScroll.StopMovement();
            _shopContent.anchoredPosition = Vector2.zero;
        }

        /// <summary>Bundles, coin packs, boosters and Remove Ads, top to bottom. Returns the height they take.</summary>
        private float BuildShopOffers(RectTransform rest)
        {
            GameServices s = _app.Services;
            var bundles = new List<StoreProduct>();
            var packs = new List<StoreProduct>();
            StoreProduct removeAds = null;
            foreach (StoreProduct product in StoreCatalog.Products)
            {
                if (product.OncePerPlayer) continue;              // the starter pack has a section of its own
                if (product.IsBundle) bundles.Add(product);
                else if (product.Consumable) packs.Add(product);
                else removeAds = product;
            }
            float y = 0f;

            // Bundles, the biggest first: it sets the scale the others are judged by.
            if (bundles.Count > 0)
            {
                y = ShopHeading(rest, "Bundles", y);
                bundles.Sort((a, b) => b.Coins.CompareTo(a.Coins));
                for (int i = 0; i < bundles.Count; i++)
                {
                    string flag = i == 0 ? "BEST VALUE" : (i == 1 && bundles.Count > 2 ? "MOST POPULAR" : null);
                    y += flag != null ? 46f : 24f;             // flags ride over the card's top edge
                    ShopBundle(rest, bundles[i], y, flag);
                    y += BundleHeight;
                }
                y += 34f;
            }

            // Coin packs: the biggest one featured across the top, the others sharing a row with the free coins.
            packs.Sort((a, b) => b.Coins.CompareTo(a.Coins));
            y = ShopHeading(rest, "Coins", y);
            if (packs.Count > 0)
            {
                y += 46f;
                ShopFeatured(rest, packs[0], packs, y);
                y += 330f;
            }
            int cards = Mathf.Max(1, packs.Count);
            float cardW = (ShopWidth - (cards - 1) * 22f) / cards;
            y += 52f;
            for (int i = 1; i < packs.Count; i++) ShopPack(rest, packs[i], -466f + cardW * 0.5f + (i - 1) * (cardW + 22f), y, cardW);
            ShopFreeCoins(rest, -466f + cardW * 0.5f + (cards - 1) * (cardW + 22f), y, cardW);
            y += 330f + 34f;

            // Boosters: each on a disc of its own colour, with the owned count and a coin price.
            y = ShopHeading(rest, "Boosters", y);
            y += 22f;
            for (int i = 0; i < 4; i++)
            {
                var type = (BoosterType)i;
                float x = -351f + i * 234f;
                Image card = UiPremium.MakeTile(rest, "Booster" + i, 0.5f, 1f, x, -y, 216f, 316f);
                Image disc = UiKit.MakeImage(card.transform, "Disc", UiPremium.Round, BoosterColors[i]);
                UiKit.Place(disc.rectTransform, 0.5f, 1f, 0f, -18f, 140f, 140f);
                UiKit.AddIcon(disc.transform, BoosterIcon(i), 100f, 0.5f, 0.5f, 0f, 4f);
                Text name = UiKit.MakeText(card.transform, BoosterNames[i], 30, UiPremium.Ink);
                UiKit.Place(name.rectTransform, 0.5f, 1f, 0f, -164f, 200f, 44f);
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
            y += 316f + 34f;

            // Remove ads.
            if (removeAds != null)
            {
                StoreProduct p = removeAds;
                Image tile = UiPremium.MakeTile(rest, "RemoveAds", 0.5f, 1f, 0f, -y, ShopWidth, 150f);
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
                y += 150f;
            }
            return y + 40f;
        }

        /// <summary>A section heading at y (measured down from the top of parent). Returns the y under it.</summary>
        private static float ShopHeading(RectTransform parent, string label, float y)
        {
            Text head = UiPremium.Heading(parent, label, 44, Color.white, UiPremium.DeepBlue);
            UiKit.Place(head.rectTransform, 0.5f, 1f, 0f, -y, 600f, 60f);
            return y + 60f;
        }

        /// <summary>A gold-rimmed card on a deep board with a soft sheen across its top and turning rays behind its art.</summary>
        private static RectTransform GoldCard(RectTransform parent, string name, float y, float w, float h, Color board, float raysX)
        {
            RectTransform card = UiKit.Place(UiKit.MakeRect(name, parent), 0.5f, 1f, 0f, -y, w, h);
            Image rim = UiKit.MakeImage(card, "Rim", UiKit.Rounded, new Color(1f, 0.76f, 0.22f));
            UiKit.Stretch(rim.rectTransform);
            rim.pixelsPerUnitMultiplier = 0.5f;
            var drop = rim.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0.02f, 0.06f, 0.20f, 0.4f);
            drop.effectDistance = new Vector2(0f, -8f);
            Image inner = UiKit.MakeImage(card, "Board", UiKit.Rounded, board);
            UiKit.Stretch(inner.rectTransform, 9f, 9f, 9f, 9f);
            inner.pixelsPerUnitMultiplier = 0.55f;
            Image sheen = UiKit.MakeImage(card, "Sheen", UiKit.Rounded, new Color(0.45f, 0.62f, 1f, 0.30f));
            UiKit.Place(sheen.rectTransform, 0.5f, 1f, 0f, -9f, w - 18f, 150f);
            sheen.pixelsPerUnitMultiplier = 0.55f;
            UiPremium.Rays(card, 560f, raysX, 0f, 0.85f);
            return card;
        }

        /// <summary>The featured coin pack: a wide gold-rimmed navy card, the art on turning rays, the value flag.</summary>
        private void ShopFeatured(RectTransform parent, StoreProduct p, List<StoreProduct> packs, float y)
        {
            RectTransform card = GoldCard(parent, "Featured", y, ShopWidth, 330f, FeaturedBoard, -262f);
            Sprite bag = UiKit.Art("coin_bag");
            Image art = UiKit.MakeImage(card, "Art", bag != null ? bag : UiKit.Icon("chest"), Color.white);
            art.preserveAspect = true;
            UiKit.Place(art.rectTransform, 0.5f, 0.5f, -262f, 4f, 290f, 290f);
            art.gameObject.AddComponent<Bob>().Height = 7f;
            Text title = UiPremium.Heading(card, p.Title, 42, Color.white, UiPremium.DeepBlue);
            UiKit.Place(title.rectTransform, 0.5f, 1f, 150f, -34f, 560f, 56f);
            UiKit.AddIcon(card, "coin", 92f, 0.5f, 0.5f, 6f, 20f);
            Text amount = UiPremium.Heading(card, "+" + p.Coins, 96, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 0.5f, 222f, 20f, 330f, 110f);
            amount.alignment = TextAnchor.MiddleLeft;
            Button buy = UiPremium.PriceButton(card, p.PriceLabel, UiPremium.Green, () => BuyProduct(p), 330f, 110f, false);
            UiKit.Place((RectTransform)buy.transform, 0.5f, 0f, 150f, 30f, 330f, 110f);
            _priceButtons[p.Id] = buy;
            ShineSweep.Add(buy, 0.4f);
            // The bundles carry BEST VALUE now; the coin pack keeps how much more it gives than the smallest one.
            int bonus = ValueBonus(p, packs);
            if (bonus >= 5) UiPremium.Flag(card, Loc.F("+{0}% more", bonus), UiPremium.Green, 1f, 1f, -26f, 26f, 340f);
        }

        /// <summary>How many percent more coins per money the pack gives than the smallest one (0 if unknown).</summary>
        private static int ValueBonus(StoreProduct p, List<StoreProduct> packs)
        {
            StoreProduct smallest = packs[packs.Count - 1];
            float price, basePrice;
            if (smallest == p || !TryPrice(p.PriceLabel, out price) || !TryPrice(smallest.PriceLabel, out basePrice)) return 0;
            if (price <= 0f || basePrice <= 0f || smallest.Coins <= 0) return 0;
            float ratio = (p.Coins / price) / (smallest.Coins / basePrice);
            return Mathf.RoundToInt((ratio - 1f) * 20f) * 5;
        }

        private static bool TryPrice(string label, out float price)
        {
            var digits = new System.Text.StringBuilder();
            foreach (char c in label ?? "") if (char.IsDigit(c) || c == '.' || c == ',') digits.Append(c == ',' ? '.' : c);
            return float.TryParse(digits.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out price);
        }

        private void ShopPack(RectTransform parent, StoreProduct p, float x, float y, float w)
        {
            // The name rides on a flag over the top edge, so art, amount and button each get their own band.
            Image card = UiPremium.MakeTile(parent, "Pack", 0.5f, 1f, x, -y, w, 330f);
            UiPremium.Rays(card.transform, 300f, 0f, 68f, 0.6f);
            Image art = UiKit.MakeImage(card.transform, "Art", UiKit.Icon("coins"), Color.white);
            art.preserveAspect = true;
            UiKit.Place(art.rectTransform, 0.5f, 0.5f, 0f, 68f, 140f, 130f);
            art.gameObject.AddComponent<Bob>().Height = 4f;
            UiPremium.Flag(card.transform, p.Title, UiPremium.Blue, 0.5f, 1f, 0f, 26f, w - 70f);
            Text amount = UiPremium.Heading(card.transform, "+" + p.Coins, 58, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 0f, 0f, 108f, w, 62f);
            Button buy = UiPremium.PriceButton(card.transform, p.PriceLabel, UiPremium.Green, () => BuyProduct(p), w - 60f, 92f, false);
            UiKit.Place((RectTransform)buy.transform, 0.5f, 0f, 0f, 16f, w - 60f, 92f);
            _priceButtons[p.Id] = buy;
        }

        private void ShopFreeCoins(RectTransform parent, float x, float y, float w)
        {
            Image card = UiPremium.MakeTile(parent, "FreeCoins", 0.5f, 1f, x, -y, w, 330f);
            UiPremium.Rays(card.transform, 300f, 0f, 68f, 0.6f);
            Image art = UiKit.AddIcon(card.transform, UiKit.Art("video_coins") != null ? "video_coins" : "gift", 140f, 0.5f, 0.5f, 0f, 68f);
            art.gameObject.AddComponent<Bob>().Height = 4f;
            UiPremium.Flag(card.transform, "FREE", UiPremium.Green, 0.5f, 1f, 0f, 26f, w - 70f);
            Text amount = UiPremium.Heading(card.transform, "+" + _app.Services.Config.RewardedAdCoins, 58, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 0f, 0f, 108f, w, 62f);
            Button watch = Btn(card.transform, "Watch", UiPremium.Orange, WatchAdForCoins, w - 60f, 92f, 44);
            UiKit.Place((RectTransform)watch.transform, 0.5f, 0f, 0f, 16f, w - 60f, 92f);
        }

        /// <summary>A bundle row: its art on turning rays, the name, the coins, what else it holds, and the price.</summary>
        private void ShopBundle(RectTransform parent, StoreProduct p, float y, string flag)
        {
            Image card = UiPremium.MakeTile(parent, "Bundle", 0.5f, 1f, 0f, -y, ShopWidth, BundleHeight);
            UiPremium.Rays(card.transform, 300f, -346f, 0f, 0.6f);
            Sprite sprite = PackArt(p);
            Image art = UiKit.MakeImage(card.transform, "Art", sprite != null ? sprite : UiKit.Icon("coins"), Color.white);
            art.preserveAspect = true;
            UiKit.Place(art.rectTransform, 0.5f, 0.5f, -346f, 0f, 190f, 190f);
            art.gameObject.AddComponent<Bob>().Height = 4f;
            Text title = UiKit.MakeText(card.transform, p.Title, 38, UiPremium.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(title.rectTransform, 0f, 1f, 226f, -18f, 380f, 52f);
            UiKit.OneLine(title, 38, 22);
            UiKit.AddIcon(card.transform, "coin", 64f, 0.5f, 0.5f, -208f, 12f);
            Text amount = UiPremium.Heading(card.transform, "+" + p.Coins, 62, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 0.5f, -14f, 12f, 320f, 72f);
            amount.alignment = TextAnchor.MiddleLeft;
            BundleStrip(card.transform, p, -206f, -66f, 62f, 80f);
            Button buy = UiPremium.PriceButton(card.transform, p.PriceLabel, UiPremium.Green, () => BuyProduct(p), 250f, 112f, false);
            UiKit.Place((RectTransform)buy.transform, 1f, 0.5f, -24f, -6f, 250f, 112f);
            _priceButtons[p.Id] = buy;
            if (flag != null) UiPremium.Flag(card.transform, flag, flag == "BEST VALUE" ? UiPremium.Red : UiPremium.Orange, 1f, 1f, -24f, 26f, 300f);
        }

        /// <summary>
        /// The picture of a pack, each a little richer than the one before (MetroSort-Media/Magaza-Paket-Promptu.md):
        /// a gift box, a suitcase, the conductor's satchel, the station master's wagon. Null for other products.
        /// </summary>
        private static Sprite PackArt(StoreProduct p)
        {
            switch (p.Id)
            {
                case StoreCatalog.StarterPack: return UiKit.Art("pack_starter");
                case StoreCatalog.BundleSmall: return UiKit.Art("pack_traveller");
                case StoreCatalog.BundleMedium: return UiKit.Art("pack_conductor");
                case StoreCatalog.BundleLarge: return UiKit.Art("pack_station");
                default: return null;
            }
        }

        /// <summary>
        /// What a pack holds besides coins, in a row whose first item is centred at (x, y) in parent: each booster
        /// on its coloured disc with an "xN" badge, then a heart with the infinity sign and the hours of unlimited lives.
        /// </summary>
        private static void BundleStrip(Transform parent, StoreProduct p, float x, float y, float size, float step)
        {
            for (int i = 0; i < 4; i++)
            {
                int n = p.Boosters != null && i < p.Boosters.Length ? p.Boosters[i] : 0;
                if (n <= 0) continue;
                Image disc = UiKit.MakeImage(parent, "Booster" + i, UiPremium.Round, BoosterColors[i]);
                UiKit.Place(disc.rectTransform, 0.5f, 0.5f, x, y, size, size);
                UiKit.AddIcon(disc.transform, BoosterIcon(i), size * 0.74f, 0.5f, 0.5f, 0f, size * 0.03f);
                CountBadge(disc.transform, "x" + n, size);
                x += step;
            }
            if (p.UnlimitedLivesMinutes > 0)
            {
                Image heart = UiKit.AddIcon(parent, "heart", size * 1.1f, 0.5f, 0.5f, x, y);
                UiKit.AddIcon(heart.transform, "infinity", size * 0.62f, 0.5f, 0.5f, 0f, size * 0.05f);
                CountBadge(heart.transform, Loc.F("{0} h", (p.UnlimitedLivesMinutes + 59) / 60), size);
            }
        }

        /// <summary>A small red pill hanging off the bottom-right corner of an item: "x6", "3 h".</summary>
        private static void CountBadge(Transform parent, string label, float size)
        {
            Image badge = UiKit.MakeImage(parent, "Count", UiPremium.Pill, UiPremium.Red);
            UiKit.Place(badge.rectTransform, 1f, 0f, size * 0.24f, -size * 0.12f, size * 0.82f, size * 0.48f);
            int font = Mathf.RoundToInt(size * 0.32f);
            Text t = UiKit.MakeText(badge.transform, label, font, Color.white);
            UiKit.Stretch(t.rectTransform, 6f, 2f, 6f, 0f);
            UiKit.OneLine(t, font, 12);
            UiKit.Emboss(t, new Color(0.45f, 0.04f, 0.04f, 0.9f));
        }

        /// <summary>The starter offer's card at the top of the shop: purple and gold, with its countdown.</summary>
        private void ShopStarter(RectTransform section)
        {
            StoreProduct p = StoreCatalog.Find(StoreCatalog.StarterPack);
            if (p == null) return;
            RectTransform card = GoldCard(section, "StarterCard", 40f, ShopWidth, 400f, StarterBoard, -262f);
            Sprite pack = PackArt(p);
            Image art = UiKit.MakeImage(card, "Art", pack != null ? pack : UiKit.Icon("gift"), Color.white);
            art.preserveAspect = true;
            UiKit.Place(art.rectTransform, 0.5f, 0.5f, -262f, -4f, 280f, 280f);
            art.gameObject.AddComponent<Bob>().Height = 7f;
            UiPremium.Flag(card, "ONE-TIME OFFER", UiPremium.Red, 0f, 1f, 26f, 26f, 360f);
            Image pill = UiKit.MakeImage(card, "Timer", UiPremium.Pill, new Color(0.05f, 0.10f, 0.30f, 0.92f));
            UiKit.Place(pill.rectTransform, 1f, 1f, -26f, -24f, 330f, 62f);
            _shopStarterTimer = UiPremium.Heading(pill.transform, "", 32, Color.white, UiPremium.DeepBlue);
            UiKit.Stretch(_shopStarterTimer.rectTransform, 16f, 2f, 16f, 2f);
            UiKit.OneLine(_shopStarterTimer, 32, 18);
            Text title = UiPremium.Heading(card, p.Title, 46, Color.white, UiPremium.DeepBlue);
            UiKit.Place(title.rectTransform, 0.5f, 0.5f, 165f, 86f, 520f, 56f);
            UiKit.OneLine(title, 46, 26);
            UiKit.AddIcon(card, "coin", 76f, 0.5f, 0.5f, 12f, 20f);
            Text amount = UiPremium.Heading(card, "+" + p.Coins, 78, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 0.5f, 216f, 20f, 320f, 90f);
            amount.alignment = TextAnchor.MiddleLeft;
            BundleStrip(card, p, -46f, -54f, 66f, 84f);
            Button buy = UiPremium.PriceButton(card, p.PriceLabel, UiPremium.Green, () => BuyProduct(p), 440f, 90f, false);
            UiKit.Place((RectTransform)buy.transform, 0.5f, 0.5f, 165f, -150f, 440f, 90f);
            _priceButtons[p.Id] = buy;
            ShineSweep.Add(buy, 0.2f);
        }

        private void BuyProduct(StoreProduct p, Action<bool> after = null)
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
                if (ok && p.OncePerPlayer) LayoutShop();
                if (after != null) after(ok);
            });
        }

        // ------------------------------------------------------------------ starter offer popup

        private GameObject _starterPopup;
        private Text _starterPopupTimer;
        private Action _starterThen;

        /// <summary>The starter pack, presented once when its offer starts (between levels or on the menu).</summary>
        private void BuildStarterOffer()
        {
            StoreProduct p = StoreCatalog.Find(StoreCatalog.StarterPack);
            if (p == null) return;
            RectTransform body;
            _starterPopup = UiPremium.Modal(_root, "StarterOffer", "Starter Pack", 900f, 1130f, () => { _app.Audio.Play(Sfx.Click); CloseStarterPopup(); }, out body);
            UiPremium.Rays(body, 640f, 0f, 0f, 0.8f);
            RectTransform rays = (RectTransform)body.Find("Rays");
            if (rays != null) { rays.anchorMin = rays.anchorMax = new Vector2(0.5f, 1f); rays.anchoredPosition = new Vector2(0f, -230f); }
            Sprite pack = PackArt(p);
            Image art = UiKit.MakeImage(body, "Art", pack != null ? pack : UiKit.Icon("gift"), Color.white);
            art.preserveAspect = true;
            UiKit.Place(art.rectTransform, 0.5f, 1f, 0f, -230f, 320f, 320f);
            art.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            art.gameObject.AddComponent<Bob>().Height = 8f;
            UiPremium.Flag(body, "ONE-TIME OFFER", UiPremium.Red, 0.5f, 1f, 0f, -24f, 420f);
            UiKit.AddIcon(body, "coin", 100f, 0.5f, 1f, -150f, -452f);
            Text amount = UiPremium.Heading(body, "+" + p.Coins, 110, UiPremium.Gold, new Color(0.48f, 0.24f, 0.02f, 1f));
            UiKit.Place(amount.rectTransform, 0.5f, 1f, 90f, -410f, 380f, 120f);
            amount.alignment = TextAnchor.MiddleLeft;
            RectTransform strip = UiKit.Place(UiKit.MakeRect("Contents", body), 0.5f, 1f, 0f, -560f, 640f, 120f);
            BundleStrip(strip, p, -256f, 0f, 104f, 128f);
            Image pill = UiKit.MakeImage(body, "Timer", UiPremium.Pill, new Color(0.07f, 0.16f, 0.40f, 0.92f));
            UiKit.Place(pill.rectTransform, 0.5f, 1f, 0f, -724f, 560f, 76f);
            _starterPopupTimer = UiPremium.Heading(pill.transform, "", 40, Color.white, UiPremium.DeepBlue);
            UiKit.Stretch(_starterPopupTimer.rectTransform, 20f, 4f, 20f, 4f);
            UiKit.OneLine(_starterPopupTimer, 40, 22);
            Button buy = UiPremium.PriceButton(body, p.PriceLabel, UiPremium.Green, () => BuyProduct(p, ok =>
            {
                // Bought: the reward plays over the screen underneath; the player goes on from there.
                if (!ok) return;
                _starterThen = null;
                _starterPopup.SetActive(false);
            }), 620f, 130f, false);
            UiKit.Place((RectTransform)buy.transform, 0.5f, 1f, 0f, -834f, 620f, 130f);
            _priceButtons[p.Id + "#popup"] = buy;
            ShineSweep.Add(buy, 0.3f);
        }

        /// <summary>
        /// Presents the starter offer the first time it is due (the store starts its 48 hours then). then runs when
        /// the popup is closed without buying. False when nothing was shown.
        /// </summary>
        private bool OpenStarterOfferIfDue(Action then)
        {
            if (_starterPopup == null || _starterPopup.activeSelf || !_app.Services.Store.TryStartStarterOffer(Now())) return false;
            _app.Services.Analytics.Track(AnalyticsEvents.StarterOfferShown, new Dictionary<string, object> { { "level", _app.Services.Save.Data.HighestUnlocked } });
            _starterThen = then;
            RefreshStarter();
            _starterPopup.SetActive(true);
            _app.Audio.Play(Sfx.Achievement);
            return true;
        }

        private void CloseStarterPopup()
        {
            if (_starterPopup != null) _starterPopup.SetActive(false);
            Action then = _starterThen;
            _starterThen = null;
            if (then != null) then();
        }

        /// <summary>The starter offer's countdowns; once it is over its card leaves the shop and its popup closes.</summary>
        private void RefreshStarter()
        {
            if (_app == null || _shopContent == null) return;
            StoreManager store = _app.Services.Store;
            long now = Now();
            bool available = store.StarterAvailable(now);
            // Until the offer is presented (at its level) the pack simply sits in the shop, with no countdown.
            bool running = store.StarterTimerRunning;
            string left = Loc.F("Ends in {0}", Clock(store.StarterSecondsLeft(now)));
            foreach (Text timer in new[] { _shopStarterTimer, _starterPopupTimer })
            {
                if (timer == null) continue;
                timer.text = left;
                timer.transform.parent.gameObject.SetActive(running);
            }
            if (_shopStarter.gameObject.activeSelf != available) LayoutShop();
            if (!available && _starterPopup != null && _starterPopup.activeSelf) CloseStarterPopup();
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
            // The reward: a navy chip with the turning coin and the amount in gold.
            RectTransform reward = UiKit.MakeRect("Reward", content);
            reward.sizeDelta = new Vector2(780f, 96f);
            _winChip = UiKit.MakeImage(reward, "Chip", UiPremium.Pill, new Color(0.10f, 0.18f, 0.42f, 0.95f));
            UiKit.Place(_winChip.rectTransform, 0.5f, 0.5f, 0f, 0f, 340f, 90f);
            _winCoins = UiPremium.Heading(reward, "", 60, UiPremium.Gold, new Color(0.40f, 0.18f, 0.0f, 1f));
            UiKit.Place(_winCoins.rectTransform, 0.5f, 0.5f, 34f, 2f, 600f, 90f);
            _winCoins.horizontalOverflow = HorizontalWrapMode.Overflow;
            _winCoinSpin = UiKit.MakeFlipBook(reward, "coin_spin", 96f);
            if (_winCoinSpin != null) _winCoinSpin.Loop = true;

            // Star chest: every new star of a level flies into a slot; a full row opens the chest.
            RectTransform chestRow = UiKit.MakeRect("StarChest", content);
            chestRow.sizeDelta = new Vector2(780f, 212f);
            Image well = UiKit.MakeImage(chestRow, "Well", UiPremium.Well, Color.white);
            UiKit.Stretch(well.rectTransform);
            // Soft turning rays behind the chest, as behind the offers in the shop.
            RectTransform raysHolder = UiKit.Place(UiKit.MakeRect("Glow", chestRow), 0f, 0.5f, 118f, 6f, 10f, 10f);
            raysHolder.pivot = new Vector2(0.5f, 0.5f);
            UiPremium.Rays(raysHolder, 250f, 0f, 0f, 0.7f);
            Sprite chestArt = UiKit.Art("chest");
            Image chestIcon = UiKit.MakeImage(chestRow, "Chest", chestArt != null ? chestArt : UiKit.Icon("gift"), Color.white);
            chestIcon.preserveAspect = true;
            _chestIcon = UiKit.Place(chestIcon.rectTransform, 0f, 0.5f, 118f, 10f, 196f, 196f);
            _chestIcon.pivot = new Vector2(0.5f, 0.5f);
            _chestIdle = chestIcon.gameObject.AddComponent<ChestIdle>();
            Text chestTitle = UiPremium.Heading(chestRow, "Star Chest", 38, new Color(1f, 0.90f, 0.50f), new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Place(chestTitle.rectTransform, 0f, 1f, 236f, -12f, 330f, 52f);
            chestTitle.alignment = TextAnchor.MiddleLeft;
            _chestPrize = UiPremium.Heading(chestRow, "", 38, UiPremium.Gold, new Color(0.40f, 0.18f, 0.0f, 1f));
            UiKit.Place(_chestPrize.rectTransform, 1f, 1f, -26f, -12f, 120f, 52f);
            _chestPrize.alignment = TextAnchor.MiddleRight;
            UiKit.AddIcon(chestRow, "coin", 56f, 1f, 1f, -138f, -37f);
            _chestNote = UiKit.MakeText(chestRow, "", 28, new Color(0.90f, 0.95f, 1f), TextAnchor.MiddleLeft, FontStyle.Normal);
            UiKit.Place(_chestNote.rectTransform, 0f, 1f, 236f, -64f, 520f, 40f);
            UiKit.Emboss(_chestNote, new Color(0.03f, 0.08f, 0.24f, 0.7f));
            int slots = Mathf.Clamp(_app.Services.Config.StarChestStars, 1, 12);
            _chestStars = new Image[slots];
            float slotW = 520f / slots;
            for (int i = 0; i < slots; i++)
            {
                Image hole = UiKit.MakeImage(chestRow, "Slot" + i, UiKit.Circle, new Color(0.03f, 0.07f, 0.22f, 0.55f));
                hole.type = Image.Type.Simple;
                UiKit.Place(hole.rectTransform, 0f, 0f, 236f + slotW * (i + 0.5f), 54f, slotW - 6f, slotW - 6f).pivot = new Vector2(0.5f, 0.5f);
                Image star = UiKit.AddIcon(hole.transform, "star_off", slotW + 4f, 0.5f, 0.5f, 0f, 2f);
                star.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _chestStars[i] = star;
            }

            // Continue is the big call to action; home and the coin video share the row under it.
            Button next = Row(content, "Continue", Palette.UiGreen, OnContinue, 150f, 62);
            ShineSweep.Add(next, 1.0f);
            // The call to action breathes gently, so the eye lands on it first.
            next.GetComponent<PressScale>().Breathe = 0.035f;
            RectTransform actions = UiKit.MakeRect("Actions", content);
            actions.sizeDelta = new Vector2(780f, 124f);
            _winHome = UiPremium.MakeRoundButton(actions, UiPremium.Round, UiPremium.Blue, () => { _app.Audio.Play(Sfx.Click); _app.GoToMenu(); }, 122f);
            _winHome.name = "Button_Home";
            UiPremium.House(_winHome.transform, 76f, HomeDoor);
            _winAdButton = Btn(actions, "+" + _app.Services.Config.RewardedAdCoins, UiPremium.Orange, () =>
            {
                _winAdButton.gameObject.SetActive(false);
                LayoutWinActions();
                WatchAdForCoins();
            }, 630f, 118f, 60);
            RowIcon(_winAdButton, UiKit.Art("video_coins") != null ? "video_coins" : "gift");
            RewardAmount(_winAdButton, 630f);
            LayoutWinActions();

            // One of the studio's other games, on every fifth level only: icon, a line and a small Get button.
            _winPromo = UiKit.MakeRect("MoreGame", content);
            _winPromo.sizeDelta = new Vector2(780f, 118f);
            Image promoPaper = UiKit.MakeImage(_winPromo, "Paper", UiKit.Rounded, Color.white);
            UiKit.Stretch(promoPaper.rectTransform);
            _winPromoIcon = UiKit.MakeImage(_winPromo, "Icon", null, Color.white);
            _winPromoIcon.preserveAspect = true;
            UiKit.Place(_winPromoIcon.rectTransform, 0f, 0.5f, 14f, 0f, 94f, 94f);
            _winPromoText = UiPremium.Heading(_winPromo, "", 36, new Color(0.09f, 0.16f, 0.38f), new Color(1f, 1f, 1f, 0f));
            UiKit.Place(_winPromoText.rectTransform, 0f, 0.5f, 124f, 0f, 440f, 90f);
            _winPromoText.alignment = TextAnchor.MiddleLeft;
            UiKit.OneLine(_winPromoText, 36, 20);
            Button promoGet = Btn(_winPromo, "Get", Palette.UiGreen, () => OpenStore(_winPromoGame), 180f, 84f, 38);
            UiKit.Place((RectTransform)promoGet.transform, 1f, 0.5f, -16f, 0f, 180f, 84f);
            _winPromo.gameObject.SetActive(false);
        }

        /// <summary>The door of the white house on the blue home buttons: a deeper blue than the button.</summary>
        private static readonly Color HomeDoor = new Color(0.10f, 0.33f, 0.74f);

        /// <summary>
        /// Turns a video button's label into the reward itself, "+40" with a coin after it, centred in the space
        /// right of the video icon (as reward buttons do in the big puzzle games).
        /// </summary>
        private static void RewardAmount(Button button, float width)
        {
            Text amount = UiKit.LabelOf(button);
            amount.resizeTextForBestFit = false;
            amount.horizontalOverflow = HorizontalWrapMode.Overflow;
            amount.alignment = TextAnchor.MiddleLeft;
            const float coin = 66f, gap = 6f, iconSpace = 112f, rightPad = 18f;
            float textWidth = amount.preferredWidth;
            float left = iconSpace + (width - rightPad - iconSpace - (textWidth + gap + coin)) * 0.5f;
            UiKit.Place(amount.rectTransform, 0f, 0.5f, left, 5f, textWidth + 4f, 100f);
            Image icon = UiKit.AddIcon(button.transform, "coin", coin, 0f, 0.5f, left + textWidth + gap + coin * 0.5f, 5f);
            var shadow = icon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.45f, 0.20f, 0.02f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -4f);
        }

        /// <summary>Recolours a button made by UiKit.MakeButton, its label's outline included.</summary>
        private static void TintButton(Button b, Color c)
        {
            var img = b.targetGraphic as Image;
            if (img == null || img.color == c) return;
            img.color = c;
            Text label = UiKit.LabelOf(b);
            if (label == null) return;
            var ink = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.9f);
            foreach (Shadow fx in label.GetComponents<Shadow>())
                fx.effectColor = fx is Outline ? ink : new Color(ink.r, ink.g, ink.b, 0.55f);
        }

        /// <summary>Home sits left of the coin video, or alone in the middle once the video is gone.</summary>
        private void LayoutWinActions()
        {
            bool ad = _winAdButton.gameObject.activeSelf;
            UiKit.Place((RectTransform)_winHome.transform, ad ? 0f : 0.5f, 0.5f, 0f, 0f, 122f, 122f);
            UiKit.Place((RectTransform)_winAdButton.transform, 1f, 0.5f, 0f, 0f, 630f, 118f);
        }

        // ------------------------------------------------------------------ the studio's other games

        private Button _moreGamesButton;
        private Image _moreGamesIcon;
        private GameObject _moreGames;
        private RectTransform _moreGamesBody, _winPromo;
        private Image _winPromoIcon;
        private Text _winPromoText;
        private StudioGame _winPromoGame;

        private void BuildMoreGames()
        {
            _moreGames = UiPremium.Modal(_root, "MoreGames", "Our Games", 1000f, 1240f, () => { _app.Audio.Play(Sfx.Click); _moreGames.SetActive(false); }, out _moreGamesBody);
            _moreGames.SetActive(false);
            _app.MoreGames.Changed += RefreshMoreGames;
            RefreshMoreGames();
        }

        private void OpenMoreGames()
        {
            RefreshMoreGames();
            _moreGames.SetActive(true);
        }

        private void OpenStore(StudioGame game)
        {
            if (game == null || string.IsNullOrEmpty(game.StoreUrl)) return;
            _app.Services.Analytics.Track(AnalyticsEvents.MoreGamesOpened, new Dictionary<string, object> { { "game", game.Slug } });
            Application.OpenURL(game.StoreUrl);
        }

        /// <summary>Rebuilds the shelf (the list is short) and shows or hides the menu button.</summary>
        private void RefreshMoreGames()
        {
            if (_moreGamesBody == null) return;
            List<StudioGame> games = _app.MoreGames.Games;
            _moreGamesButton.gameObject.SetActive(games.Count > 0);
            if (games.Count > 0)
            {
                _moreGamesIcon.sprite = games[0].Icon;
                _moreGamesIcon.enabled = games[0].Icon != null;
            }
            for (int i = _moreGamesBody.childCount - 1; i >= 0; i--) Destroy(_moreGamesBody.GetChild(i).gameObject);
            // The card is as tall as its list.
            ((RectTransform)_moreGamesBody.parent).sizeDelta = new Vector2(1000f, 196f + Mathf.Clamp(games.Count, 1, 4) * 262f);
            Color navy = new Color(0.09f, 0.16f, 0.38f), clear = new Color(1f, 1f, 1f, 0f);
            for (int i = 0; i < games.Count && i < 4; i++)
            {
                StudioGame game = games[i];
                RectTransform row = UiKit.Place(UiKit.MakeRect("Game_" + game.Slug, _moreGamesBody), 0.5f, 1f, 0f, -18f - i * 262f, 900f, 240f);
                Image paper = UiKit.MakeImage(row, "Paper", UiKit.Rounded, Color.white);
                UiKit.Stretch(paper.rectTransform);
                var shadow = paper.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.02f, 0.10f, 0.34f, 0.30f);
                shadow.effectDistance = new Vector2(0f, -8f);
                Image icon = UiKit.MakeImage(row, "Icon", game.Icon, Color.white);
                icon.preserveAspect = true;
                icon.enabled = game.Icon != null;
                UiKit.Place(icon.rectTransform, 0f, 0.5f, 26f, 0f, 188f, 188f);
                Text name = UiPremium.Heading(row, game.LocalName, 50, navy, clear);
                UiKit.Place(name.rectTransform, 0f, 0.5f, 238f, 52f, 400f, 68f);
                name.alignment = TextAnchor.MiddleLeft;
                UiKit.OneLine(name, 50, 26);
                Text tagline = UiKit.MakeText(row, game.LocalTagline, 30, new Color(0.36f, 0.43f, 0.58f), TextAnchor.UpperLeft, FontStyle.Normal);
                UiKit.Place(tagline.rectTransform, 0f, 0.5f, 238f, -40f, 400f, 96f);
                tagline.resizeTextForBestFit = true;
                tagline.resizeTextMinSize = 20;
                tagline.resizeTextMaxSize = 30;
                Button get = Btn(row, "Get", Palette.UiGreen, () => OpenStore(game), 220f, 104f, 44);
                UiKit.Place((RectTransform)get.transform, 1f, 0.5f, -26f, 0f, 220f, 104f);
            }
        }

        /// <summary>On every fifth level the win screen offers one of the other games, a different one each time.</summary>
        private void ShowWinPromo(int levelId)
        {
            List<StudioGame> games = _app.MoreGames.Games;
            bool show = games.Count > 0 && levelId >= 5 && levelId % 5 == 0;
            _winPromo.gameObject.SetActive(show);
            if (!show) return;
            _winPromoGame = games[(levelId / 5) % games.Count];
            _winPromoIcon.sprite = _winPromoGame.Icon;
            _winPromoIcon.enabled = _winPromoGame.Icon != null;
            _winPromoText.text = Loc.F("Try {0} too", _winPromoGame.LocalName);
        }

        private void BuildLose()
        {
            RectTransform content;
            _lose = UiKit.Modal(_root, "LevelFailed", "", out content);
            // The title rides on the red ribbon over the card's top edge, like every other popup.
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 56, 118, 44);
            layout.spacing = 24f;
            Image ribbon = UiPremium.Ribbon(content, "", 760f, 0f, 22f);
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _loseTitle = ribbon.GetComponentInChildren<Text>();
            UiKit.OneLine(_loseTitle, 56, 30); // smaller than the ribbon's 70: "İstasyon Tıkandı" still reads large
            Color navy = new Color(0.09f, 0.16f, 0.38f);
            Color clear = new Color(1f, 1f, 1f, 0f);

            // What happened, on one calm white card: the sad conductor, how many are still waiting, and why.
            RectTransform hero = UiKit.MakeRect("Hero", content);
            hero.sizeDelta = new Vector2(788f, 300f);
            Image paper = UiKit.MakeImage(hero, "Paper", UiKit.Rounded, Color.white);
            UiKit.Stretch(paper.rectTransform);
            var paperShadow = paper.gameObject.AddComponent<Shadow>();
            paperShadow.effectColor = new Color(0.02f, 0.10f, 0.34f, 0.30f);
            paperShadow.effectDistance = new Vector2(0f, -8f);
            Sprite sad = UiKit.Art("conductor_sad");
            if (sad != null)
            {
                Image guide = UiKit.MakeImage(hero, "Conductor", sad, Color.white);
                guide.preserveAspect = true;
                UiKit.Place(guide.rectTransform, 0f, 0.5f, 22f, 0f, 262f, 262f);
                guide.gameObject.AddComponent<Bob>().Height = 4f;
            }
            _loseLeft = UiPremium.Heading(hero, "", 124, UiPremium.Red, clear);
            UiKit.Place(_loseLeft.rectTransform, 0f, 0.5f, 296f, 58f, 470f, 132f);
            Text waitingLabel = UiPremium.Heading(hero, "passengers left", 42, navy, clear);
            UiKit.Place(waitingLabel.rectTransform, 0f, 0.5f, 296f, -30f, 470f, 56f);
            UiKit.OneLine(waitingLabel, 42, 22);
            _loseReason = UiKit.MakeText(hero, "", 28, new Color(0.36f, 0.43f, 0.58f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Place(_loseReason.rectTransform, 0f, 0.5f, 296f, -96f, 470f, 68f);
            _loseReason.resizeTextForBestFit = true;
            _loseReason.resizeTextMinSize = 18;
            _loseReason.resizeTextMaxSize = 28;

            // Two ways to carry on from here with one more platform, side by side: pay for it, or watch a video.
            _loseOffers = UiKit.MakeRect("Offers", content);
            _loseOffers.sizeDelta = new Vector2(788f, 236f);
            _loseContinue = Btn(_loseOffers, "Continue", Palette.UiGreen, () => _app.Game.UseExtraPlatform(), 384f, 236f, 50);
            UiKit.Place((RectTransform)_loseContinue.transform, 0f, 0.5f, 0f, 0f, 384f, 236f);
            ShineSweep.Add(_loseContinue, 0.6f);
            _loseContinue.GetComponent<PressScale>().Breathe = 0.03f;
            UiKit.AddIcon(_loseContinue.transform, BoosterIcon(2), 112f, 0.5f, 1f, 0f, -68f);
            UiKit.Stretch(UiKit.LabelOf(_loseContinue).rectTransform, 12f, 74f, 12f, 116f);
            // What it costs, on a light chip: the platforms owned, or the coin price.
            RectTransform price = UiKit.Place(UiKit.MakeRect("Price", _loseContinue.transform), 0.5f, 0f, 0f, 20f, 190f, 56f);
            Image chip = UiKit.MakeImage(price, "Chip", UiPremium.Pill, new Color(1f, 1f, 1f, 0.96f));
            UiKit.Stretch(chip.rectTransform);
            _losePriceCoin = UiKit.AddIcon(price, "coin", 46f, 0f, 0.5f, 12f, 0f).gameObject;
            _losePrice = UiPremium.Heading(price, "", 36, new Color(0.10f, 0.42f, 0.10f), clear);
            UiKit.Stretch(_losePrice.rectTransform, 58f, 2f, 14f, 2f);

            // A red play badge says "video" (the coin art would promise coins).
            _loseAd = Btn(_loseOffers, "Watch", UiPremium.Orange, ContinueWithAd, 384f, 236f, 50);
            UiKit.Place((RectTransform)_loseAd.transform, 1f, 0.5f, 0f, 0f, 384f, 236f);
            Image video = UiKit.MakeImage(_loseAd.transform, "Video", UiPremium.Round, new Color(0.92f, 0.20f, 0.22f));
            UiKit.Place(video.rectTransform, 0.5f, 1f, 0f, -20f, 96f, 96f);
            UiKit.AddIcon(video.transform, "play", 48f, 0.5f, 0.5f, 4f, 2f);
            UiKit.Stretch(UiKit.LabelOf(_loseAd).rectTransform, 12f, 74f, 12f, 116f);
            RectTransform free = UiKit.Place(UiKit.MakeRect("Free", _loseAd.transform), 0.5f, 0f, 0f, 20f, 190f, 56f);
            Image freeChip = UiKit.MakeImage(free, "Chip", UiPremium.Pill, new Color(1f, 1f, 1f, 0.96f));
            UiKit.Stretch(freeChip.rectTransform);
            Text freeText = UiPremium.Heading(free, "Free", 34, new Color(0.78f, 0.36f, 0.02f), clear);
            UiKit.Stretch(freeText.rectTransform, 12f, 2f, 12f, 2f);
            UiKit.OneLine(freeText, 34, 18);

            // Last row: home, undo the last move (round, with its count) and Try Again.
            RectTransform actions = UiKit.MakeRect("Actions", content);
            actions.sizeDelta = new Vector2(788f, 124f);
            _loseHome = UiPremium.MakeRoundButton(actions, UiPremium.Round, UiPremium.Blue, () => { _app.Audio.Play(Sfx.Click); SpendLife(_app.GoToMenu); }, 122f);
            _loseHome.name = "Button_Home";
            UiKit.Place((RectTransform)_loseHome.transform, 0f, 0.5f, 0f, 0f, 122f, 122f);
            UiPremium.House(_loseHome.transform, 76f, HomeDoor);
            _loseUndo = UiPremium.MakeRoundButton(actions, UiPremium.Round, BoosterColors[0], () => _app.Game.UseUndo(), 122f);
            _loseUndo.name = "Button_Undo";
            UiKit.Place((RectTransform)_loseUndo.transform, 0f, 0.5f, 142f, 0f, 122f, 122f);
            UiKit.AddIcon(_loseUndo.transform, BoosterIcon(0), 90f, 0.5f, 0.5f, 0f, 4f);
            Image undoRing = UiKit.MakeImage(_loseUndo.transform, "BadgeRing", UiKit.Circle, Color.white);
            UiKit.Place(undoRing.rectTransform, 0.5f, 0.5f, 46f, 44f, 54f, 54f);
            _loseUndoDot = UiKit.MakeImage(undoRing.transform, "Badge", UiKit.Circle, BadgeRed);
            UiKit.Stretch(_loseUndoDot.rectTransform, 5f, 5f, 5f, 5f);
            _loseUndoBadge = UiPremium.Heading(_loseUndoDot.transform, "", 28, Color.white, new Color(0.35f, 0.02f, 0.04f, 0.95f));
            UiKit.Stretch(_loseUndoBadge.rectTransform, 0f, 2f, 0f, 0f);
            Button retry = Btn(actions, "Try Again", Palette.UiPrimary, () => SpendLife(RestartChecked), 498f, 118f, 50);
            _loseRetry = UiKit.Place((RectTransform)retry.transform, 1f, 0.5f, 0f, 0f, 498f, 118f);
            UiKit.Stretch(UiKit.LabelOf(retry).rectTransform, 20f, 16f, 118f, 6f);
            LifeCostTag(retry.transform);
            // Going home costs a life as well: one quiet line instead of a heart badge on every button.
            Text lifeNote = UiKit.MakeText(content, "Leaving the level also costs a life", 24, new Color(0.88f, 0.93f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Emboss(lifeNote, new Color(0.03f, 0.08f, 0.24f, 0.6f));
            lifeNote.rectTransform.sizeDelta = new Vector2(780f, 32f);
        }

        /// <summary>A rewarded video pays for the extra platform, which is then used straight away.</summary>
        private void ContinueWithAd()
        {
            _app.Services.AdsCoordinator.RequestRewarded(() => _app.Services.Boosters.Grant(BoosterType.ExtraPlatform, 1), ok =>
            {
                if (ok) _app.Game.UseExtraPlatform();
                else Toast("No ad available right now");
            });
        }

        private void BuildToast()
        {
            Image toast = UiKit.MakeImage(_root, "Toast", UiKit.Rounded, new Color(0.13f, 0.16f, 0.24f, 0.94f));
            UiKit.Place(toast.rectTransform, 0.5f, 0f, 0f, 330f, 900f, 90f);
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
            if (state != AppState.Paused && _pause != null && _pause.activeSelf) _pause.SetActive(false);
            if (state != AppState.Playing && state != AppState.Paused && _intro != null)
            {
                Destroy(_intro);
                _intro = null;
            }
            Refresh();
            // Back on the menu, the starter offer shows the first time it is due.
            if (state == AppState.Menu) OpenStarterOfferIfDue(null);
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
            // Hard levels say so before you start: the label names it and the Play button turns red (purple for
            // Super Hard), as players of the genre expect.
            if (next.Tier > 0)
            {
                _menuLevel.text = Loc.F("Level {0}  -  {1}", next.Id, Loc.T(next.Tier >= 2 ? "Super Hard Level" : "Hard Level"));
                _menuLevel.color = next.Tier >= 2 ? SuperPurple : HardRed;
            }
            else
            {
                _menuLevel.text = Loc.F("Level {0}  -  {1}", next.Id, Loc.LevelName(next.Name));
                _menuLevel.color = Palette.UiInk;
            }
            if (_playButton != null) TintButton(_playButton, next.Tier >= 2 ? SuperPurple : (next.Tier == 1 ? HardRed : Palette.UiGreen));
            RefreshDaily();
            RefreshLives();

            for (int i = 0; i < 4; i++)
            {
                var type = (BoosterType)i;
                int count = s.Boosters.Count(type);
                // Owned charges are counted on the red badge; when none are left a green plus and the coin price
                // show, so paid use is never a surprise.
                _boosterBadges[i].text = count > 0 ? count.ToString() : "+";
                _boosterBadgeDots[i].color = count > 0 ? BadgeRed : BadgeGreen;
                _boosterPriceRows[i].SetActive(count <= 0);
                _boosterPrices[i].text = s.Boosters.Cost(type).ToString();
                _shopBoosterCounts[i].text = count.ToString();
            }
            UiKit.LabelOf(_removeAdsButton).text = s.Store.AdsRemoved ? Loc.T("Done") : StoreCatalog.Find(StoreCatalog.RemoveAds).PriceLabel;
            foreach (KeyValuePair<string, Button> pb in _priceButtons)
            {
                // "id#popup": a second button for the same product (the starter offer's popup)
                StoreProduct product = StoreCatalog.Find(pb.Key.Split('#')[0]);
                if (product != null) UiKit.LabelOf(pb.Value).text = product.PriceLabel;
            }
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
            for (int i = 0; i < 4; i++) _boosterGroups[i].alpha = _boosterButtons[i].interactable ? 1f : 0.55f;

            int undoCount = s.Boosters.Count(BoosterType.Undo), platformCount = s.Boosters.Count(BoosterType.ExtraPlatform);
            _loseUndoBadge.text = undoCount > 0 ? undoCount.ToString() : "+";
            _loseUndoDot.color = undoCount > 0 ? BadgeRed : BadgeGreen;
            bool canContinue = game.Board.LoseReason == LoseReason.Deadlock && game.Board.CanAddExtraPlatform;
            bool canWatch = canContinue && s.AdsCoordinator.IsRewardedReady;
            _loseOffers.gameObject.SetActive(canContinue);
            _losePrice.text = platformCount > 0 ? "x" + platformCount : s.Boosters.Cost(BoosterType.ExtraPlatform).ToString();
            _losePriceCoin.SetActive(platformCount <= 0);
            UiKit.Stretch(_losePrice.rectTransform, platformCount > 0 ? 14f : 58f, 2f, 14f, 2f);
            _loseAd.gameObject.SetActive(canWatch);
            // With no video on offer the paid Continue takes the whole row.
            ((RectTransform)_loseContinue.transform).sizeDelta = new Vector2(canWatch ? 384f : 788f, 236f);
            _loseUndo.gameObject.SetActive(game.Board.CanUndo);
            // Without the undo button Try Again takes its place in the row.
            _loseRetry.sizeDelta = new Vector2(game.Board.CanUndo ? 498f : 640f, 118f);
            _loseLeft.text = Mathf.Max(0, game.Board.State.Total - game.Board.State.Boarded).ToString();
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
            _languageLabel.text = Loc.NativeName(Loc.Language);
            _app.Services.Save.Save();
        }

        // ------------------------------------------------------------------ actions

        private void OnPlay() { StartLevelChecked(_app.CurrentLevelId); }

        private void OnPause()
        {
            // Pausing is refused once the final move is in: the win/fail screen is about to appear.
            if (_app.States.Current != AppState.Playing || _app.Game.Board == null || _app.Game.Board.Status != GameStatus.Playing) return;
            _app.States.Set(AppState.Paused);
            OpenPause();
        }

        public void OpenSettings(bool inGame)
        {
            _settingsInGame = inGame;
            // Resume, restart and menu live on the pause card now; settings is only settings.
            _settingsGameRow.SetActive(false);
            var panel = (RectTransform)_settings.transform.Find("Panel");
            // On the menu the panel also holds restore, privacy and credits.
            if (panel != null) panel.sizeDelta = new Vector2(940f, inGame ? 1150f : 1290f);
            _restoreButton.gameObject.SetActive(!inGame);
            _linksRow.SetActive(!inGame);
            bool privacy = _app.Services.Ads.PrivacyOptionsRequired || !string.IsNullOrEmpty(PrivacyPage);
            _privacyButton.gameObject.SetActive(privacy);
            UiKit.Place((RectTransform)_creditsButton.transform, privacy ? 1f : 0.5f, 0.5f, 0f, 0f, 415f, 100f);
            SaveData data = _app.Services.Save.Data;
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(data.MusicVolume);
            if (_soundSlider != null) _soundSlider.SetValueWithoutNotify(data.SfxVolume);
            ApplySettings();
            _settings.SetActive(true);
        }

        private void CloseSettings()
        {
            _settings.SetActive(false);
            _app.Services.Save.Save();
            // Opened from the pause card: back to it, the level stays paused.
            if (_settingsInGame && _app.States.Current == AppState.Paused) OpenPause();
        }

        // ------------------------------------------------------------------ pause card

        private void BuildPause()
        {
            RectTransform body;
            _pause = UiPremium.Modal(_root, "Paused", "Paused", 880f, 790f, () => { _app.Audio.Play(Sfx.Click); ResumeFromPause(); }, out body);
            _pauseLevel = UiPremium.Heading(body, "", 56, Color.white, UiPremium.DeepBlue);
            UiKit.Place(_pauseLevel.rectTransform, 0.5f, 1f, 0f, -6f, 720f, 70f);
            _pauseProgress = UiKit.MakeText(body, "", 34, new Color(0.90f, 0.95f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Emboss(_pauseProgress, new Color(0.03f, 0.08f, 0.24f, 0.7f));
            UiKit.Place(_pauseProgress.rectTransform, 0.5f, 1f, 0f, -78f, 720f, 46f);
            Button resume = Btn(body, "Resume", UiPremium.Green, ResumeFromPause, 640f, 150f, 62);
            UiKit.Place((RectTransform)resume.transform, 0.5f, 1f, 0f, -150f, 640f, 150f);
            ShineSweep.Add(resume, 0.6f);
            Button restart = Btn(body, "Restart", UiPremium.Orange, () => { _pause.SetActive(false); LeaveLevel(RestartChecked); }, 380f, 116f, 40);
            UiKit.Place((RectTransform)restart.transform, 0.5f, 1f, -200f, -326f, 380f, 116f);
            Button menu = Btn(body, "Menu", UiPremium.Red, () => { _pause.SetActive(false); LeaveLevel(_app.GoToMenu); }, 380f, 116f, 40);
            UiKit.Place((RectTransform)menu.transform, 0.5f, 1f, 200f, -326f, 380f, 116f);
            // Quick toggles: music, sound, and the full settings.
            _pauseMusic = PauseToggle(body, "icon_music", -170f, ToggleMusic);
            _pauseSound = PauseToggle(body, "icon_sound", 0f, ToggleSound);
            Button gear = UiPremium.MakeRoundButton(body, UiPremium.Round, UiPremium.Blue, () =>
            {
                _app.Audio.Play(Sfx.Click);
                _pause.SetActive(false);
                OpenSettings(true);
            }, 128f);
            UiKit.Place((RectTransform)gear.transform, 0.5f, 1f, 170f, -478f, 128f, 128f);
            UiKit.AddIcon(gear.transform, "gear", 84f, 0.5f, 0.5f, 0f, 3f);
        }

        private Button PauseToggle(RectTransform body, string icon, float x, Action onClick)
        {
            Button b = UiPremium.MakeRoundButton(body, UiPremium.Round, UiPremium.Blue, () => onClick(), 128f);
            UiKit.Place((RectTransform)b.transform, 0.5f, 1f, x, -478f, 128f, 128f);
            if (UiKit.Art(icon) != null) UiKit.AddIcon(b.transform, icon, 92f, 0.5f, 0.5f, 0f, 3f);
            return b;
        }

        private void OpenPause()
        {
            GameController game = _app.Game;
            if (game.Level != null) _pauseLevel.text = Loc.F("Level {0}", game.Level.Id);
            if (game.Board != null) _pauseProgress.text = Loc.F("Boarded {0} / {1}", game.Board.State.Boarded, game.Board.State.Total);
            RefreshPauseToggles();
            _pause.SetActive(true);
        }

        private void ResumeFromPause()
        {
            _pause.SetActive(false);
            if (_app.States.Current == AppState.Paused) _app.States.Set(AppState.Playing);
        }

        private void ToggleMusic()
        {
            _app.Audio.Play(Sfx.Click);
            SaveData d = _app.Services.Save.Data;
            if (d.MusicVolume > 0.01f) { _musicBeforeMute = d.MusicVolume; d.MusicVolume = 0f; }
            else d.MusicVolume = _musicBeforeMute > 0.01f ? _musicBeforeMute : 0.6f;
            _app.Audio.MusicVolume = d.MusicVolume;
            _app.Services.Save.Save();
            RefreshPauseToggles();
        }

        private void ToggleSound()
        {
            SaveData d = _app.Services.Save.Data;
            if (d.SfxVolume > 0.01f) { _soundBeforeMute = d.SfxVolume; d.SfxVolume = 0f; }
            else d.SfxVolume = _soundBeforeMute > 0.01f ? _soundBeforeMute : 0.9f;
            _app.Audio.SfxVolume = d.SfxVolume;
            _app.Audio.Play(Sfx.Click);
            _app.Services.Save.Save();
            RefreshPauseToggles();
        }

        /// <summary>A muted toggle turns grey with a dimmed icon.</summary>
        private void RefreshPauseToggles()
        {
            SaveData d = _app.Services.Save.Data;
            SetToggleLook(_pauseMusic, d.MusicVolume > 0.01f);
            SetToggleLook(_pauseSound, d.SfxVolume > 0.01f);
        }

        private static void SetToggleLook(Button b, bool on)
        {
            ((Image)b.targetGraphic).color = on ? UiPremium.Blue : new Color(0.55f, 0.60f, 0.70f);
            foreach (Image icon in b.GetComponentsInChildren<Image>(true))
                if (icon != b.targetGraphic) icon.color = new Color(1f, 1f, 1f, on ? 1f : 0.45f);
        }

        private void OpenShop()
        {
            _app.Services.Analytics.Track(AnalyticsEvents.ShopOpened, new Dictionary<string, object> { { "coins", _app.Services.Economy.Coins } });
            // Seen here first, the starter offer's 48 hours start now (its popup then never comes).
            if (_app.Services.Store.TryStartStarterOffer(Now()))
                _app.Services.Analytics.Track(AnalyticsEvents.StarterOfferShown, new Dictionary<string, object> { { "level", _app.Services.Save.Data.HighestUnlocked } });
            Refresh();
            RefreshStarter();
            LayoutShop();
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
                int tier = s.Levels.Get(id).Tier;
                Color fill = !unlocked ? new Color(0.62f, 0.66f, 0.74f) : (here ? UiPremium.Gold : (tier >= 2 ? new Color(0.88f, 0.80f, 1f) : (tier == 1 ? new Color(1f, 0.82f, 0.82f) : Color.white)));
                // A ring in the line colour around each station, like a metro map; red and purple mark the
                // Hard and Super Hard stations.
                Color ringColor = tier >= 2 ? SuperPurple : (tier == 1 ? HardRed : line);
                Image ring = UiKit.MakeImage(_levelsGrid, "Ring" + id, UiPremium.Round, unlocked ? ringColor : new Color(0.48f, 0.52f, 0.60f));
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
                if (tier > 0)
                {
                    // A small "!" (Hard) or "!!" (Super Hard) badge, shown on locked stations too.
                    Image mark = UiKit.MakeImage(ring.transform, "Hard", UiPremium.Round, tier >= 2 ? SuperPurple : HardRed);
                    UiKit.Place(mark.rectTransform, 1f, 1f, 8f, 8f, 56f, 56f);
                    Text bang = UiPremium.Heading(mark.transform, tier >= 2 ? "!!" : "!", 34, Color.white, new Color(0.30f, 0.02f, 0.06f, 0.9f));
                    UiKit.Stretch(bang.rectTransform, 0f, 4f, 0f, 0f);
                }
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
            const float BandWidth = 900f, BandY = -250f, GoalY = -410f;
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
            int tier = _app.Game.Level != null ? _app.Game.Level.Tier : 0;
            if (tier > 0)
                UiPremium.Flag(band.transform, tier >= 2 ? "Super Hard Level" : "Hard Level", tier >= 2 ? SuperPurple : HardRed, 0.5f, 1f, 0f, 6f, 420f);

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
            local.x = Mathf.Clamp(local.x, -300f, 300f) * 0.5f;
            local.y = -60f;
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
                holder.anchoredPosition = local + new Vector2(0f, 50f * Ease.OutCubic(t));
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
            _menuInfinity = UiKit.AddIcon(holder, "infinity", 70f, 0f, 0.5f, 46f, 6f);
            _menuInfinity.gameObject.SetActive(false);
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
            bool unlimited = lives.IsUnlimited(now);
            string clock = unlimited ? Clock(lives.UnlimitedSecondsLeft(now)) : count >= lives.Max ? Loc.T("Full") : Clock(lives.SecondsToNext(now));
            _menuLives.text = count.ToString();
            _menuLives.gameObject.SetActive(!unlimited);
            if (_menuInfinity != null) _menuInfinity.gameObject.SetActive(unlimited);
            _menuLivesTimer.text = clock;
            if (_noLives != null && _noLives.activeSelf)
            {
                bool full = count >= lives.Max;
                _noLivesCount.gameObject.SetActive(!unlimited);
                _noLivesInfinity.gameObject.SetActive(unlimited);
                _noLivesTimer.text = unlimited ? Loc.F("Unlimited lives: {0}", clock)
                    : full ? Loc.T("All lives are full!") : Loc.F("Next life in {0}", clock);
                bool offers = !full && !unlimited;
                _refillButton.gameObject.SetActive(offers);
                _livesUnlimitedButton.gameObject.SetActive(offers);
                _livesAdButton.gameObject.SetActive(offers);
                _livesPlayButton.gameObject.SetActive(!offers);
                foreach (Image pip in _lifePips) pip.gameObject.SetActive(!unlimited);
                ShowLifeCount(count);
            }
        }

        /// <summary>Starts a level when there is a life to play it with; otherwise offers ways to get one.</summary>
        private void StartLevelChecked(int levelId)
        {
            if (_app.Services.Lives.CanPlay(Now())) _app.Game.StartLevel(levelId);
            else OpenNoLives();
        }

        private void RestartChecked()
        {
            if (_app.Services.Lives.CanPlay(Now())) _app.Game.Restart();
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
            _noLives = UiPremium.Modal(_root, "Lives", "Lives", 900f, 1080f, () => { _app.Audio.Play(Sfx.Click); _noLives.SetActive(false); }, out body);
            UiPremium.Rays(body, 620f, 0f, 0f, 0.75f);
            RectTransform rays = (RectTransform)body.Find("Rays");
            if (rays != null) { rays.anchorMin = rays.anchorMax = new Vector2(0.5f, 1f); rays.anchoredPosition = new Vector2(0f, -195f); }
            // The big glossy heart with the count on it.
            FlipBook heart = UiKit.MakeFlipBook(body, "heart_spin", 320f);
            RectTransform heartRect = heart != null ? (RectTransform)heart.transform : UiKit.AddIcon(body, "heart", 320f).rectTransform;
            heartRect.anchorMin = heartRect.anchorMax = new Vector2(0.5f, 1f);
            heartRect.anchoredPosition = new Vector2(0f, -195f);
            if (heart != null) heart.Interval = 2.5f;
            heartRect.gameObject.AddComponent<Beat>();
            _noLivesCount = UiPremium.Heading(heartRect, "0", 120, Color.white, new Color(0.36f, 0.0f, 0.05f, 0.95f));
            UiKit.Stretch(_noLivesCount.rectTransform, 0f, 30f, 0f, 0f);
            _noLivesInfinity = UiKit.AddIcon(heartRect, "infinity", 170f, 0.5f, 0.5f, 0f, 12f);
            _noLivesInfinity.gameObject.SetActive(false);

            // One small heart per life: full ones glossy, spent ones as hollow outlines.
            int max = _app.Services.Lives.Max;
            _lifePips = new Image[max];
            const float Pip = 74f, PipGap = 14f;
            for (int i = 0; i < max; i++)
            {
                float x = (i - (max - 1) * 0.5f) * (Pip + PipGap);
                _lifePips[i] = UiKit.AddIcon(body, "heart", Pip, 0.5f, 1f, x, -405f);
            }

            // Status: "All lives are full!", the countdown to the next life, or unlimited time left, on a dark pill.
            Image status = UiKit.MakeImage(body, "Status", UiPremium.Pill, new Color(0.07f, 0.16f, 0.40f, 0.92f));
            UiKit.Place(status.rectTransform, 0.5f, 1f, 0f, -495f, 660f, 80f);
            _noLivesTimer = UiPremium.Heading(status.transform, "", 42, Color.white, UiPremium.DeepBlue);
            UiKit.Stretch(_noLivesTimer.rectTransform, 20f, 4f, 20f, 4f);
            _noLivesTimer.resizeTextForBestFit = true;
            _noLivesTimer.resizeTextMinSize = 24;
            _noLivesTimer.resizeTextMaxSize = 42;

            GameServices s = _app.Services;
            _refillButton = UiPremium.PriceButton(body, Loc.F("Refill  {0}", s.Lives.RefillCost), UiPremium.Green, () =>
            {
                if (s.Lives.RefillWithCoins(Now())) { _app.Audio.Play(Sfx.Coins); Toast("Lives refilled!"); }
                else { _app.Audio.Play(Sfx.Invalid); Toast("Not enough coins"); }
                Refresh();
            }, 620f, 112f, true);
            UiKit.Place((RectTransform)_refillButton.transform, 0.5f, 1f, 0f, -606f, 620f, 112f);
            // The best offer: an hour of unlimited lives, in purple with the infinity sign.
            _livesUnlimitedButton = UiPremium.PriceButton(body, Loc.F("1 h unlimited  {0}", s.Lives.UnlimitedCost), new Color(0.58f, 0.32f, 0.92f), () =>
            {
                if (s.Lives.BuyUnlimited(Now())) { _app.Audio.Play(Sfx.Achievement); Toast("Unlimited lives started!"); }
                else { _app.Audio.Play(Sfx.Invalid); Toast("Not enough coins"); }
                Refresh();
            }, 620f, 104f, true);
            UiKit.Place((RectTransform)_livesUnlimitedButton.transform, 0.5f, 1f, 0f, -724f, 620f, 104f);
            Image inf = UiKit.AddIcon(_livesUnlimitedButton.transform, "infinity", 96f, 1f, 0.5f, -70f, 4f);
            inf.name = "Infinity";
            UiKit.Stretch(UiKit.LabelOf(_livesUnlimitedButton).rectTransform, 104f * 0.82f, 14f, 130f, 6f);
            Image best = UiPremium.Flag(_livesUnlimitedButton.transform, Loc.T("BEST"), UiPremium.Red, 1f, 1f, -40f, 30f, 150f);
            best.rectTransform.pivot = new Vector2(1f, 0.5f);
            _livesAdButton = Btn(body, "Watch an ad: +1 life", UiPremium.Orange, () =>
            {
                s.AdsCoordinator.RequestRewarded(() => s.Lives.Add(1, Now()), ok =>
                {
                    if (ok) { _app.Audio.Play(Sfx.Coins); Toast("+1 life"); }
                    else Toast("No ad available right now");
                    Refresh();
                });
            }, 620f, 96f, 38);
            UiKit.Place((RectTransform)_livesAdButton.transform, 0.5f, 1f, 0f, -836f, 620f, 96f);
            UiKit.AddIcon(_livesAdButton.transform, "heart", 72f, 0f, 0.5f, 58f, 4f);
            UiKit.Stretch(UiKit.LabelOf(_livesAdButton).rectTransform, 104f, 8f, 20f, 6f);
            // With every life there (or unlimited lives running), the popup just invites the player back in.
            _livesPlayButton = Btn(body, "PLAY", UiPremium.Green, () =>
            {
                _noLives.SetActive(false);
                StartLevelChecked(_app.CurrentLevelId);
            }, 620f, 136f, 60);
            UiKit.Place((RectTransform)_livesPlayButton.transform, 0.5f, 1f, 0f, -660f, 620f, 136f);
        }

        /// <summary>
        /// Shows the lives count on the popup. When it goes up (refill, ad, a life coming back) the new hearts pop
        /// in one after another with a sparkle and the number counts up with them.
        /// </summary>
        private void ShowLifeCount(int count)
        {
            if (_lifePips == null) return;
            if (_livesShown < 0 || count <= _livesShown || !_noLives.activeInHierarchy)
            {
                if (_livesFill != null) { StopCoroutine(_livesFill); _livesFill = null; }
                _livesShown = count;
                _noLivesCount.text = count.ToString();
                for (int i = 0; i < _lifePips.Length; i++) SetPip(i, i < count);
                return;
            }
            if (_livesFill != null) return;
            _livesFill = StartCoroutine(FillLives(_livesShown, count));
        }

        private void SetPip(int i, bool full)
        {
            _lifePips[i].sprite = UiKit.Icon(full ? "heart" : "heart_empty");
            _lifePips[i].color = Color.white;
            _lifePips[i].rectTransform.localScale = Vector3.one;
        }

        private IEnumerator FillLives(int from, int to)
        {
            for (int i = from; i < to && i < _lifePips.Length; i++)
            {
                SetPip(i, true);
                _livesShown = i + 1;
                _noLivesCount.text = _livesShown.ToString();
                _app.Audio.Play(Sfx.Star);
                Haptics.Tap();
                RectTransform pip = _lifePips[i].rectTransform;
                RectTransform big = (RectTransform)_noLivesCount.transform.parent;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.28f)
                {
                    pip.localScale = Vector3.one * (GameSettings.ReducedMotion ? 1f : Ease.OutBack(t) * (1f + 0.25f * Mathf.Sin(t * Mathf.PI)));
                    big.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
                    yield return null;
                }
                pip.localScale = Vector3.one;
                big.localScale = Vector3.one;
            }
            _livesFill = null;
        }

        private void OpenNoLives()
        {
            if (!_app.Services.Lives.CanPlay(Now()))
                _app.Services.Analytics.Track(AnalyticsEvents.LivesEmpty, new Dictionary<string, object> { { "level", _app.CurrentLevelId } });
            _livesShown = -1;
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
            bool counting = (_menu != null && _menu.activeInHierarchy) || (_noLives != null && _noLives.activeInHierarchy)
                || (_shop != null && _shop.activeInHierarchy) || (_starterPopup != null && _starterPopup.activeInHierarchy);
            if (!counting || Time.unscaledTime < _dailyTick) return;
            _dailyTick = Time.unscaledTime + 1f;
            RefreshDaily();
            RefreshLives();
            RefreshStarter();
        }

        private void OnDaily()
        {
            int granted = _app.Services.Economy.ClaimDaily(EconomyManager.TodayNumber(DateTime.UtcNow));
            if (granted <= 0) return;
            _app.Services.Analytics.Track(AnalyticsEvents.DailyGiftClaimed, new Dictionary<string, object> { { "coins", granted } });
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
            Action next = () =>
            {
                if (_app.Services.Levels.Has(finished + 1)) StartLevelChecked(finished + 1);
                else
                {
                    _app.GoToMenu();
                    Toast("You cleared every station. More are on the way!");
                }
            };
            // The first time the starter offer is due it shows here, in place of this break's ad; closing it goes on.
            if (OpenStarterOfferIfDue(next)) return;
            // Interstitials are only ever considered here, between levels, never during a puzzle.
            _app.Services.AdsCoordinator.OnLevelTransition(finished, next);
        }

        // ------------------------------------------------------------------ results

        private void ShowWin(LevelResult result)
        {
            _winCoins.text = "+" + result.CoinsAwarded;
            // The chip fits the amount; the turning coin sits just left of it.
            float textW = _winCoins.preferredWidth;
            _winChip.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(textW + 170f, 260f, 700f), 90f);
            _winCoins.rectTransform.anchoredPosition = new Vector2(34f, 2f);
            if (_winCoinSpin != null) ((RectTransform)_winCoinSpin.transform).anchoredPosition = new Vector2(34f - textW * 0.5f - 58f, 2f);
            foreach (GameObject stray in _flying) if (stray != null) Destroy(stray);
            _flying.Clear();
            _winAdButton.gameObject.SetActive(_app.Services.AdsCoordinator.IsRewardedReady);
            LayoutWinActions();
            ShowWinPromo(result.LevelId);
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
            for (int i = 0; i < _chestStars.Length; i++)
            {
                bool on = i < stars;
                _chestStars[i].sprite = UiKit.Icon(on ? "star" : "star_off");
                _chestStars[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                _chestStars[i].rectTransform.localScale = Vector3.one;
            }
            _chestPrize.text = "+" + _app.Services.Config.StarChestCoins;
            _chestNote.text = stars >= size ? Loc.T("Ready to open!") : Loc.F("{0} more stars to open", size - stars);
            _chestIdle.Ready = stars >= size;
            _chestIdle.enabled = true;
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
                int slot = Mathf.Clamp(shown, 0, _chestStars.Length - 1);
                _app.Audio.Play(Sfx.Select);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
                {
                    if (rt == null) yield break;
                    Vector3 to = _chestStars[slot].rectTransform.position;
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
                RectTransform landed = _chestStars[slot].rectTransform;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.26f)
                {
                    landed.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(t * Mathf.PI) * (1f - 0.5f * t));
                    yield return null;
                }
                landed.localScale = Vector3.one;
            }
            if (result.ChestCoins <= 0) yield break;
            if (_app.States.Current != AppState.LevelComplete) { _coinDisplay = -1; Refresh(); yield break; }

            // Full: the chest rattles, then bursts open in the reveal.
            _chestIdle.enabled = false;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
            {
                _chestIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * 12f * (1f - t * 0.5f));
                _chestIcon.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            _chestIcon.localRotation = Quaternion.identity;
            _chestIcon.localScale = Vector3.one;
            string message = Loc.T("Star chest opened!");
            if (!HasRewardBox("chest"))
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
            // One short line: the offers below say what can be done about it.
            _loseReason.text = Loc.T(reason == LoseReason.OutOfMoves
                ? "The move limit ran out before everyone boarded."
                : (_app.Game.Board != null && _app.Game.Board.State.HasSchedule
                    ? "The bench is full and no front passenger has a train."
                    : "No passenger can reach a platform."));
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

        // ------------------------------------------------------------------ bench notice

        private RectTransform _benchNotice;
        private CanvasGroup _benchNoticeGroup;
        private Text _benchNoticeText;
        private float _benchNoticeStart = -10f;
        private const float BenchNoticeTime = 2.0f;

        /// <summary>
        /// A refused tap explains itself where the player is looking: a red speech plate pops up over the bench.
        /// Without a bench in the level the message falls back to the toast.
        /// </summary>
        public void BenchNotice(string message)
        {
            if (!_app.Station.HasBench) { Toast(message); return; }
            if (_benchNotice == null)
            {
                _benchNotice = UiKit.MakeRect("BenchNotice", _labelLayer);
                UiKit.Place(_benchNotice, 0.5f, 0.5f, 0f, 0f, 700f, 104f);
                _benchNoticeGroup = _benchNotice.gameObject.AddComponent<CanvasGroup>();
                _benchNoticeGroup.blocksRaycasts = false;
                _benchNoticeGroup.interactable = false;
                Image rim = UiKit.MakeImage(_benchNotice, "Rim", UiPremium.Pill, Color.white);
                UiKit.Stretch(rim.rectTransform);
                var shadow = rim.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.25f, 0.02f, 0.05f, 0.45f);
                shadow.effectDistance = new Vector2(0f, -7f);
                Image plate = UiKit.MakeImage(_benchNotice, "Plate", UiPremium.Pill, new Color(0.90f, 0.20f, 0.26f));
                UiKit.Stretch(plate.rectTransform, 6f, 6f, 6f, 6f);
                Image badge = UiKit.MakeImage(_benchNotice, "Badge", UiKit.Circle, Color.white);
                UiKit.Place(badge.rectTransform, 0f, 0.5f, 16f, 0f, 72f, 72f);
                Text mark = UiPremium.Heading(badge.transform, "!", 54, new Color(0.90f, 0.20f, 0.26f), new Color(1f, 1f, 1f, 0f));
                UiKit.Stretch(mark.rectTransform, 0f, 2f, 0f, 0f);
                _benchNoticeText = UiPremium.Heading(_benchNotice, "", 36, Color.white, new Color(0.42f, 0.03f, 0.07f, 0.95f));
                UiKit.Stretch(_benchNoticeText.rectTransform, 100f, 8f, 28f, 8f);
                UiKit.OneLine(_benchNoticeText, 36, 20);
            }
            _benchNoticeText.text = Loc.T(message);
            _benchNoticeStart = Time.unscaledTime;
            _benchNotice.gameObject.SetActive(true);
        }

        private void AnimateBenchNotice()
        {
            if (_benchNotice == null || !_benchNotice.gameObject.activeSelf) return;
            float t = Time.unscaledTime - _benchNoticeStart;
            Vector2 local;
            if (t > BenchNoticeTime || _app.States.Current != AppState.Playing || !WorldToCanvas(_app.Station.BenchAnchor, out local))
            {
                _benchNotice.gameObject.SetActive(false);
                return;
            }
            // Pops in with a little overshoot, wobbles once like a head shake, then fades.
            float pop = Mathf.Clamp01(t / 0.22f);
            float scale = GameSettings.ReducedMotion ? 1f : 1f + 0.18f * Mathf.Sin(pop * Mathf.PI) * (1f - pop) + (pop - 1f) * 0.4f * (1f - pop);
            float wobble = GameSettings.ReducedMotion || t > 0.5f ? 0f : Mathf.Sin(t * 42f) * 9f * (1f - t / 0.5f);
            _benchNotice.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -170f, 170f) + wobble, local.y);
            _benchNotice.localScale = Vector3.one * Mathf.Max(0.2f, pop < 1f ? Mathf.Lerp(0.6f, 1f, pop) * scale : 1f);
            _benchNoticeGroup.alpha = Mathf.Clamp01((BenchNoticeTime - t) / 0.3f);
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
            AnimateBenchNotice();

            Vector2 local;
            if (_pointerOn && WorldToCanvas(_pointerWorld, out local))
            {
                _pointer.anchoredPosition = local;
                // The arm reaches in from the nearer screen edge, never across the other queue.
                _pointer.localScale = new Vector3(local.x < 0f ? -1f : 1f, 1f, 1f);
                AnimatePointer();
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
            // Long queues: a badge with how many more wait behind the last passenger on screen.
            int tails = 0;
            if (_labelLayer.gameObject.activeSelf)
            {
                StationView station = _app.Station;
                for (int q = 0; q < station.TailLaneCount; q++)
                {
                    string caption = station.TailLabel(q);
                    if (caption == null || !WorldToCanvas(station.TailAnchor(q), out local)) continue;
                    Text badge = tails < _tailBadges.Count ? _tailBadges[tails] : NewTailBadge();
                    tails++;
                    GameObject plate = badge.transform.parent.gameObject;
                    if (!plate.activeSelf) plate.SetActive(true);
                    if (badge.text != caption) badge.text = caption;
                    ((RectTransform)plate.transform).anchoredPosition = local;
                }
            }
            for (int i = tails; i < _tailBadges.Count; i++)
            {
                GameObject plate = _tailBadges[i].transform.parent.gameObject;
                if (plate.activeSelf) plate.SetActive(false);
            }
            for (int i = used; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);
        }

        private readonly List<Text> _tailBadges = new List<Text>();

        /// <summary>A small dark pill ("+5") that sits at the back of a long queue.</summary>
        private Text NewTailBadge()
        {
            Image plate = UiKit.MakeImage(_labelLayer, "QueueTail", UiPremium.Pill, new Color(0.10f, 0.17f, 0.38f, 0.92f));
            UiKit.Place(plate.rectTransform, 0.5f, 0.5f, 0f, 0f, 104f, 58f);
            var shadow = plate.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -4f);
            Text text = UiPremium.Heading(plate.transform, "", 36, Color.white, new Color(0.03f, 0.06f, 0.20f, 0.95f));
            UiKit.Stretch(text.rectTransform, 6f, 2f, 6f, 4f);
            text.raycastTarget = false;
            plate.raycastTarget = false;
            _tailBadges.Add(text);
            return text;
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
