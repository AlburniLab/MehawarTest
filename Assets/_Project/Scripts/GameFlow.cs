#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Game-flow state machine + greybox UI: main menu -> campaign select -> level 1
    /// ("Il Risveglio") -> level complete -> replay / back to menu. Death/respawn stays inside
    /// the level (PlayerHealth). SINGLE-SCENE choice: the level lives under a disposable
    /// LevelRoot built on demand by GreyboxBootstrap — no scene assets, no Build Settings,
    /// consistent with the all-from-code pipeline. UI is runtime UGUI, keyboard/gamepad
    /// navigable (EventSystem + InputSystemUIInputModule, one selectable focused per panel).
    /// All on-screen text is Italian (project rule); code stays English.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [Header("UI Style")]
        [Tooltip("Reference resolution for the runtime canvas scaler.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1280f, 720f);

        private static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.11f, 0.92f);
        private static readonly Color TitleColor = new Color(0.95f, 0.85f, 0.60f);
        private static readonly Color TextColor = new Color(0.85f, 0.85f, 0.88f);
        private static readonly Color MutedColor = new Color(0.62f, 0.62f, 0.68f);

        private GreyboxBootstrap _builder = null!;
        private Font _font = null!;
        private GameObject _mainMenu = null!;
        private GameObject _campaignSelect = null!;
        private GameObject _levelComplete = null!;
        private GameObject _pauseMenu = null!;
        private Selectable _mainFirst = null!;
        private Selectable _campaignFirst = null!;
        private Selectable _pauseFirst = null!;
        private Button _continueButton = null!;
        private Button _replayButton = null!;
        private Text _completeSubtitle = null!;
        private GameObject? _levelRoot;
        private PlayerInputHub? _playerInput;
        private PlayerControls? _activeControls;   // cached for pause-action unsubscribe
        private LevelCatalog.LevelInfo? _currentInfo;
        private bool _debugRun;                    // out-of-sequence run: no "Prosegui"

        /// <summary>True while the pause menu owns the screen (also used by runtime tests).</summary>
        public bool IsPaused => PauseManager.IsPaused;

        /// <summary>Called by GreyboxBootstrap right after creating this component.</summary>
        public void Initialize(GreyboxBootstrap builder) => _builder = builder;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateEventSystem();
            Transform canvas = CreateCanvas();
            BuildMainMenu(canvas);
            BuildCampaignSelect(canvas);
            BuildLevelComplete(canvas);
            BuildPauseMenu(canvas);
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            // Play-mode exit safety: never leave the clock frozen.
            DetachPauseInput();
            PauseManager.ForceReset();
        }

        // ---------------------------------------------------------------- flow

        private void ShowMainMenu()
        {
            _mainMenu.SetActive(true);
            _campaignSelect.SetActive(false);
            _levelComplete.SetActive(false);
            _pauseMenu.SetActive(false);
            Select(_mainFirst);
        }

        private void ShowCampaignSelect()
        {
            _mainMenu.SetActive(false);
            _campaignSelect.SetActive(true);
            _levelComplete.SetActive(false);
            Select(_campaignFirst);
        }

        /// <summary>Start a level by campaign + 1-based index (LevelCatalog). Public: also the
        /// direct-access API for tests (e.g. StartLevel(ViaRomana, 2) = Il Passo Conteso).</summary>
        public void StartLevel(Campaign campaign, int levelIndex)
            => LaunchLevel(campaign, levelIndex, LevelCatalog.Get(campaign, levelIndex), false);

        /// <summary>Debug access: run ANY builder with ANY avatar, outside the campaign sequence
        /// (dual-moveset playtests). No "Prosegui" on completion; Rigioca replays the same run.</summary>
        public void StartLevelDebug(Campaign campaign, int builderId, string displayName)
            => LaunchLevel(campaign, GameState.CurrentLevel, new LevelCatalog.LevelInfo(displayName, builderId), true);

        private void LaunchLevel(Campaign campaign, int levelIndex, LevelCatalog.LevelInfo info, bool debugRun)
        {
            GameState.Campaign = campaign;
            GameState.CurrentLevel = levelIndex;
            _currentInfo = info;
            _debugRun = debugRun;

            _mainMenu.SetActive(false);
            _campaignSelect.SetActive(false);
            _levelComplete.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);   // UI focus off while playing
            DetachPauseInput();                                // never double-subscribe across runs
            if (_levelRoot != null)
                Destroy(_levelRoot);                           // starting over a running level must not stack roots
            _levelRoot = _builder.BuildLevel(info.BuilderId, OnGoalReached);
            _playerInput = _levelRoot.GetComponentInChildren<PlayerInputHub>();

            // Pause toggle comes from the dedicated UI action map (Esc / gamepad Start),
            // which stays enabled in pause while the gameplay map is switched off.
            if (_playerInput != null)
            {
                _activeControls = _playerInput.Controls;
                _activeControls.UI.Pause.performed += OnPausePerformed;
            }
            Debug.Log($"[GameFlow] Avvio {(debugRun ? "[TEST] " : "")}«{info.Name}» — {GameState.CampaignName} ({GameState.AvatarName}).");
        }

        private void OnGoalReached()
        {
            Debug.Log("[GameFlow] Livello completato!");
            DetachPauseInput();
            if (_levelRoot != null)
                Destroy(_levelRoot);
            _levelRoot = null;
            _playerInput = null;

            string levelName = _currentInfo != null ? _currentInfo.Name : "?";
            _completeSubtitle.text = $"{levelName} — {GameState.CampaignName} ({GameState.AvatarName})";

            bool hasNext = !_debugRun && LevelCatalog.HasLevel(GameState.Campaign, GameState.CurrentLevel + 1);
            _continueButton.gameObject.SetActive(hasNext);
            _levelComplete.SetActive(true);
            Select(hasNext ? _continueButton : _replayButton);
        }

        private static void Select(Selectable target)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        // ------------------------------------------------------------- pause

        private void OnPausePerformed(InputAction.CallbackContext ctx) => TogglePause();

        private void DetachPauseInput()
        {
            if (_activeControls != null)
                _activeControls.UI.Pause.performed -= OnPausePerformed;
            _activeControls = null;
        }

        /// <summary>Toggle pause (UI/Pause action; public so runtime tests can drive it).</summary>
        public void TogglePause()
        {
            if (_levelRoot == null)
                return;   // pause is only meaningful while a level is running
            if (PauseManager.IsPaused)
                ResumeGame();
            else
                PauseGame();
        }

        private void PauseGame()
        {
            PauseManager.SetPaused(true);            // saves the live timeScale (even a hitstop's)
            _playerInput?.SetGameplayEnabled(false); // gameplay map OFF: nothing leaks under the panel
            _pauseMenu.SetActive(true);
            Select(_pauseFirst);
        }

        private void ResumeGame()
        {
            _pauseMenu.SetActive(false);
            PauseManager.SetPaused(false);           // restores the SAVED timeScale
            _playerInput?.SetGameplayEnabled(true);
            EventSystem.current.SetSelectedGameObject(null);
        }

        private void RestartLevel()
        {
            // EXACTLY the death/respawn path (resource burned, kills kept): resume, then die.
            ResumeGame();
            PlayerHealth? health = _levelRoot != null ? _levelRoot.GetComponentInChildren<PlayerHealth>() : null;
            health?.Kill();
            Debug.Log("[GameFlow] Ricomincia livello: applicata la regola morte/respawn.");
        }

        private void ReturnToMenu()
        {
            ResumeGame();                            // restore timeScale/input state first
            DetachPauseInput();
            if (_levelRoot != null)
                Destroy(_levelRoot);
            _levelRoot = null;
            _playerInput = null;
            Debug.Log("[GameFlow] Livello abbandonato — ritorno al menu.");
            ShowMainMenu();
        }

        private void QuitGame()
        {
            Debug.Log("[GameFlow] Esci richiesto (no-op nell'editor).");
            Application.Quit();
        }

        // ------------------------------------------------------------ UI shell

        private static void CreateEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();   // default actions: arrows/dpad + submit/cancel
        }

        private Transform CreateCanvas()
        {
            var go = new GameObject("FlowCanvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        private void BuildMainMenu(Transform canvas)
        {
            (GameObject panel, Transform column) = CreatePanel(canvas, "MainMenu");
            _mainMenu = panel;
            CreateText(column, "MEHAWAR", 64, TitleColor, FontStyle.Bold);
            CreateText(column, "Prototipo greybox — Atto I", 22, MutedColor, FontStyle.Italic);
            CreateSpacer(column, 24f);
            _mainFirst = CreateButton(column, "Nuova partita", null, ShowCampaignSelect);
            CreateButton(column, "[TEST] Passo Conteso — Cesare", "Percorso reale: Via Romana liv. 2 (Favore/Bastione).",
                () => StartLevel(Campaign.ViaRomana, 2));
            CreateButton(column, "[TEST] Passo Conteso — Lucius", "Fuori sequenza, per il test dual-moveset (Sete di sangue/Furia).",
                () => StartLevelDebug(Campaign.ViaOscura, LevelCatalog.PassoContesoBuilder, "Il Passo Conteso"));
            CreateButton(column, "Esci", null, QuitGame);
        }

        private void ReplayCurrent()
        {
            if (_currentInfo == null)
                return;
            LaunchLevel(GameState.Campaign, GameState.CurrentLevel, _currentInfo, _debugRun);
        }

        private void BuildCampaignSelect(Transform canvas)
        {
            (GameObject panel, Transform column) = CreatePanel(canvas, "CampaignSelect");
            _campaignSelect = panel;
            CreateText(column, "Scegli la tua via", 44, TitleColor, FontStyle.Bold);
            CreateSpacer(column, 16f);
            _campaignFirst = CreateButton(column, "Via Oscura",
                "Lucius, ospite mortale di Tiamat. Aggressione pura: la Sete di Sangue\ncresce colpendo e scatena la Furia. Rischia tutto, sempre.",
                () => StartLevel(Campaign.ViaOscura, 1));
            CreateButton(column, "Via Romana",
                "Cesare, campione mortale di Marduk. Colpi lenti, pesanti e lunghi: il Favore\ncresce colpendo senza farsi colpire e arma il Bastione, che para e respinge.",
                () => StartLevel(Campaign.ViaRomana, 1));
            CreateSpacer(column, 12f);
            CreateButton(column, "Indietro", null, ShowMainMenu);
        }

        private void BuildPauseMenu(Transform canvas)
        {
            (GameObject panel, Transform column) = CreatePanel(canvas, "PauseMenu");
            _pauseMenu = panel;
            CreateText(column, "PAUSA", 48, TitleColor, FontStyle.Bold);
            CreateSpacer(column, 20f);
            _pauseFirst = CreateButton(column, "Riprendi", null, ResumeGame);
            CreateButton(column, "Ricomincia livello",
                "Torni all'inizio come dopo una morte: risorsa azzerata, nemici uccisi restano uccisi.",
                RestartLevel);
            CreateButton(column, "Torna al menu", null, ReturnToMenu);
        }

        private void BuildLevelComplete(Transform canvas)
        {
            (GameObject panel, Transform column) = CreatePanel(canvas, "LevelComplete");
            _levelComplete = panel;
            CreateText(column, "Livello completato", 48, TitleColor, FontStyle.Bold);
            _completeSubtitle = CreateText(column, "", 24, TextColor, FontStyle.Normal);
            CreateSpacer(column, 20f);
            _continueButton = CreateButton(column, "Prosegui", null,
                () => StartLevel(GameState.Campaign, GameState.CurrentLevel + 1));
            _replayButton = CreateButton(column, "Rigioca", null, ReplayCurrent);
            CreateButton(column, "Torna al menu", null, ShowMainMenu);
        }

        // -------------------------------------------------------- UI elements

        private (GameObject panel, Transform column) CreatePanel(Transform canvas, string name)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(canvas, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = PanelBg;

            var column = new GameObject("Column");
            column.transform.SetParent(panel.transform, false);
            var colRect = column.AddComponent<RectTransform>();
            colRect.anchorMin = colRect.anchorMax = new Vector2(0.5f, 0.5f);
            colRect.pivot = new Vector2(0.5f, 0.5f);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = column.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            panel.SetActive(false);
            return (panel, column.transform);
        }

        private Text CreateText(Transform parent, string content, int size, Color color, FontStyle style)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            rect.sizeDelta = new Vector2(760f, size * 1.4f);
            return text;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(1f, height);
        }

        private Button CreateButton(Transform parent, string label, string? description, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button." + label);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(640f, description == null ? 58f : 110f);
            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock cb = button.colors;
            cb.normalColor = new Color(0.20f, 0.20f, 0.25f);
            cb.highlightedColor = new Color(0.33f, 0.31f, 0.30f);
            cb.selectedColor = new Color(0.55f, 0.40f, 0.20f);   // warm: readable keyboard focus
            cb.pressedColor = new Color(0.75f, 0.55f, 0.25f);
            cb.fadeDuration = 0.08f;
            button.colors = cb;
            button.onClick.AddListener(onClick);

            Text title = CreateText(go.transform, label, 28, TextColor, FontStyle.Bold);
            var titleRect = title.GetComponent<RectTransform>();
            if (description == null)
            {
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
            }
            else
            {
                titleRect.anchorMin = new Vector2(0f, 0.60f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
                Text desc = CreateText(go.transform, description, 17, MutedColor, FontStyle.Normal);
                var descRect = desc.GetComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0f, 0f);
                descRect.anchorMax = new Vector2(1f, 0.60f);
                descRect.offsetMin = descRect.offsetMax = Vector2.zero;
            }
            return button;
        }
    }
}
