using System;
using System.Collections;
using System.Collections.Generic;
using RoomRevive.IntentSelector;
using RoomRevive.ProductBrowser;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomRevive
{
    /// <summary>
    /// World-space onboarding flow based on the RoomRevive XR onboarding reference.
    /// Arrow keys change the focused answer, Enter confirms it, and Escape/Backspace goes back.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class WelcomeUI : MonoBehaviour
    {
        [Serializable]
        public class OnboardingAnswers
        {
            public string style;
            public string palette;
            public string householdSize;
            public string investmentTier;

            public OnboardingAnswers Clone()
            {
                return new OnboardingAnswers
                {
                    style = style,
                    palette = palette,
                    householdSize = householdSize,
                    investmentTier = investmentTier,
                };
            }
        }

        [Serializable]
        class OptionData
        {
            public string value;
            public string label;
            public string hint;
            public string resourceName;

            public OptionData(string value, string label, string hint = "", string resourceName = "")
            {
                this.value = value;
                this.label = label;
                this.hint = hint;
                this.resourceName = resourceName;
            }
        }

        [Serializable]
        class QuestionData
        {
            public string kicker;
            public string title;
            public OptionData[] options;

            public QuestionData(string kicker, string title, params OptionData[] options)
            {
                this.kicker = kicker;
                this.title = title;
                this.options = options;
            }
        }

        static readonly Color Dark = Hex(0x3A4055);
        static readonly Color Muted = Hex(0x6B7388);
        static readonly Color CtaText = Hex(0xE6E9F0);
        static readonly Color Panel = new Color(0.710f, 0.737f, 0.816f, 0.985f);
        static readonly Color PanelLight = Hex(0xD4D9E5);
        static readonly Color White = Hex(0xFFFFFF);
        static readonly Color Accent = Hex(0x5E97B8);
        static readonly Color Disabled = new Color(0.91f, 0.92f, 0.95f, 0.78f);

        const string ResourceRoot = "RoomReviveOnboarding/";

        const string StyleKey = "RoomRevive.Onboarding.Style";
        const string PaletteKey = "RoomRevive.Onboarding.Palette";
        const string HouseholdKey = "RoomRevive.Onboarding.Household";
        const string InvestmentKey = "RoomRevive.Onboarding.Investment";

        static readonly QuestionData[] Questions =
        {
            new QuestionData(
                "QUESTION 1 OF 4",
                "Which style do you prefer?",
                new OptionData("modern", "Modern", "Clean, handleless lines", "StyleModern"),
                new OptionData("designer", "Designer", "Bold and statement", "StyleDesigner"),
                new OptionData("cottage style", "Cottage", "Warm and framed", "StyleCottage"),
                new OptionData("natural & scandinavian", "Scandinavian", "Calm and natural", "StyleScandinavian")),
            new QuestionData(
                "QUESTION 2 OF 4",
                "Which palette do you prefer?",
                new OptionData("light", "Light & airy", "Bright, neutral", "PaletteLight"),
                new OptionData("dark", "Dark & moody", "Deep, matte", "PaletteDark"),
                new OptionData("wood", "Warm wood", "Natural grain", "PaletteWood"),
                new OptionData("bold", "Bold accent", "Colour-forward", "PaletteBold")),
            new QuestionData(
                "QUESTION 3 OF 4",
                "How many are you usually cooking for?",
                new OptionData("compact", "1–2 people"),
                new OptionData("standard", "3–4 people"),
                new OptionData("host", "5 or more")),
            new QuestionData(
                "QUESTION 4 OF 4",
                "How much would you like to invest?",
                new OptionData("Essential", "Essential", "Affordable"),
                new OptionData("Signature", "Signature", "Mid-range"),
                new OptionData("Premium", "Premium", "High-end"),
                new OptionData("any", "Show me all", "Explore everything")),
        };

        /// <summary>Raised when the final Explore the room action is confirmed.</summary>
        public static Action OnScanRequested;

        /// <summary>Raised after all four answers have been selected and saved.</summary>
        public static Action<OnboardingAnswers> OnAnswersCompleted;

        [Header("Canvas")]
        [SerializeField] public float worldScale = 0.001f;
        [Tooltip("Logical layout size. The layout groups adapt when this value changes.")]
        [SerializeField] public Vector2 canvasSize = new Vector2(1200f, 720f);
        [SerializeField] public Camera eventCamera;

        [Header("Head Follow")]
        [SerializeField] public float distance = 1.4f;
        [SerializeField] public float rightOffset;
        [SerializeField] public float upOffset = -0.1f;

        [Header("Branding")]
        [Tooltip("Optional RoomRevive logo shown on the welcome screen.")]
        [SerializeField] public Sprite logoSprite;

        [Header("Background")]
        [Tooltip("If true, a full-canvas background is drawn behind every step (welcome, questions, result).")]
        [SerializeField] public bool showBackground = true;
        [Tooltip("Optional background image. When empty, backgroundColor is used as a flat fill.")]
        [SerializeField] public Sprite backgroundSprite;
        [Tooltip("Background fill color (also the tint applied to backgroundSprite).")]
        [SerializeField] public Color backgroundColor = new Color(0.86f, 0.88f, 0.92f, 1f);

        [Header("Question Images (optional overrides)")]
        [Tooltip("Leave these empty to use the images imported from the HTML reference.")]
        [SerializeField] Sprite[] questionImageOverrides = new Sprite[8];

        [Header("Font")]
        [Tooltip("Fallback font. When a Product Browser panel is present, onboarding uses its font automatically.")]
        [SerializeField] public TMP_FontAsset fontAsset;

        [Header("Keyboard — New Input System")]
        [SerializeField] bool keyboardNavigation = true;
        [SerializeField] bool allowKeyboardBack = true;

        [Header("Auto Rebuild")]
        [SerializeField] public bool autoRebuildInEditor = true;

        Canvas _canvas;
        CanvasScaler _scaler;
        CanvasGroup _canvasGroup;
        Transform _cameraTransform;
        Coroutine _fade;
        GameObject _root;
        GameObject _content;
        GameObject _background;
        Sprite _roundedSprite;
        TMP_FontAsset _resolvedFontAsset;
        readonly List<Texture2D> _generatedTextures = new List<Texture2D>();
        readonly List<Sprite> _generatedSprites = new List<Sprite>();

        InputAction _navigateAction;
        InputAction _submitAction;
        InputAction _backAction;

        readonly OnboardingAnswers _answers = new OnboardingAnswers();
        int _step;
        int _selectedIndex;
        bool _hasStarted;
        bool _completionRaised;

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        public OnboardingAnswers CurrentAnswers => _answers.Clone();
        public int CurrentStep => _step;

        void Awake()
        {
            GrabComponents();
            SetupCanvas();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            SetupInputActions();

            if (_hasStarted)
                RestartFlow(false);
        }

        void Start()
        {
            if (!Application.isPlaying) return;

            GameObject eye = GameObject.Find("CenterEyeAnchor");
            _cameraTransform = eye != null ? eye.transform : Camera.main?.transform;
            _hasStarted = true;
            RestartFlow(false);
        }

        void OnDisable()
        {
            TearDownInputActions();
        }

        void OnDestroy()
        {
            TearDownInputActions();
            ClearGeneratedUI();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || _cameraTransform == null) return;
            if (_canvasGroup == null || _canvasGroup.alpha < 0.01f) return;

            transform.position = _cameraTransform.position
                + _cameraTransform.forward * distance
                + _cameraTransform.right * rightOffset
                + _cameraTransform.up * upOffset;
            transform.rotation = Quaternion.LookRotation(_cameraTransform.forward);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || !autoRebuildInEditor || _rebuildQueued) return;
            _rebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (this == null) return;
                GrabComponents();
                SetupCanvas();
                _step = 0;
                _selectedIndex = 0;
                Rebuild();
            };
        }
#endif

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
        {
#if UNITY_EDITOR
            // Never build UI on a prefab ASSET — SetParent is disabled there ("would corrupt data").
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            ClearGeneratedUI();
            GrabComponents();
            SetupCanvas();
            ResolveProductBrowserFont();

            _roundedSprite = CreateRoundedSprite();

            _root = MakeUIObject("Generated_WelcomeUI", transform);
            Stretch(_root);

            // Persistent full-canvas background behind every step (welcome, all questions, result).
            // Created first so it renders underneath _content.
            BuildBackground(_root.transform);

            _content = MakeUIObject("Content", _root.transform);
            Stretch(_content);
            RenderCurrentStep();

            MetaWorldSpaceCanvasSetup raySetup = GetComponent<MetaWorldSpaceCanvasSetup>();
            if (raySetup != null)
                raySetup.Configure();
        }

        public void Show()
        {
            if (_root == null) Rebuild();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(true));
        }

        public void Hide()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(false));
        }

        public void RestartOnboarding()
        {
            RestartFlow(true);
        }

        IEnumerator FadeRoutine(bool fadeIn)
        {
            if (_canvasGroup == null) yield break;
            if (fadeIn)
                _canvasGroup.interactable = _canvasGroup.blocksRaycasts = true;

            float start = _canvasGroup.alpha;
            float end = fadeIn ? 1f : 0f;
            float time = 0f;
            while (time < 1f)
            {
                time = Mathf.Min(time + Time.deltaTime / 0.25f, 1f);
                _canvasGroup.alpha = Mathf.Lerp(start, end, time);
                yield return null;
            }

            if (!fadeIn)
                _canvasGroup.interactable = _canvasGroup.blocksRaycasts = false;
        }

        void SetupInputActions()
        {
            TearDownInputActions();
            if (!keyboardNavigation) return;

            _navigateAction = new InputAction("Onboarding Navigate", InputActionType.Value);
            _navigateAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _navigateAction.performed += OnNavigatePerformed;

            _submitAction = new InputAction("Onboarding Submit", InputActionType.Button);
            _submitAction.AddBinding("<Keyboard>/enter");
            _submitAction.AddBinding("<Keyboard>/numpadEnter");
            _submitAction.performed += OnSubmitPerformed;

            _backAction = new InputAction("Onboarding Back", InputActionType.Button);
            _backAction.AddBinding("<Keyboard>/escape");
            _backAction.AddBinding("<Keyboard>/backspace");
            _backAction.performed += OnBackPerformed;

            _navigateAction.Enable();
            _submitAction.Enable();
            _backAction.Enable();
        }

        void TearDownInputActions()
        {
            if (_navigateAction != null)
            {
                _navigateAction.performed -= OnNavigatePerformed;
                _navigateAction.Dispose();
                _navigateAction = null;
            }

            if (_submitAction != null)
            {
                _submitAction.performed -= OnSubmitPerformed;
                _submitAction.Dispose();
                _submitAction = null;
            }

            if (_backAction != null)
            {
                _backAction.performed -= OnBackPerformed;
                _backAction.Dispose();
                _backAction = null;
            }
        }

        void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            Vector2 direction = context.ReadValue<Vector2>();
            if (direction.sqrMagnitude < 0.25f) return;

            int delta;
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                delta = direction.x > 0f ? 1 : -1;
            else
                delta = direction.y > 0f ? -1 : 1;

            MoveSelection(delta);
        }

        void OnSubmitPerformed(InputAction.CallbackContext context)
        {
            SubmitSelection();
        }

        void OnBackPerformed(InputAction.CallbackContext context)
        {
            if (allowKeyboardBack)
                GoBack();
        }

        void MoveSelection(int delta)
        {
            int count = GetSelectableCount();
            if (count <= 1) return;

            int next = _selectedIndex;
            for (int i = 0; i < count; i++)
            {
                next = (next + delta + count) % count;
                if (IsSelectable(next))
                {
                    _selectedIndex = next;
                    RenderCurrentStep();
                    return;
                }
            }
        }

        void SubmitSelection()
        {
            if (_step == 0)
            {
                SetStep(1);
                return;
            }

            if (_step >= 1 && _step <= Questions.Length)
            {
                PickOption(_selectedIndex);
                return;
            }

            if (_step == Questions.Length + 1)
            {
                if (_selectedIndex == 0)
                    OnScanRequested?.Invoke();
                else
                    RestartFlow(true);
            }
        }

        void PickOption(int optionIndex)
        {
            if (_step < 1 || _step > Questions.Length) return;
            if (!IsSelectable(optionIndex)) return;

            QuestionData question = Questions[_step - 1];
            if (optionIndex < 0 || optionIndex >= question.options.Length) return;

            SetAnswer(_step - 1, question.options[optionIndex].value);
            SetStep(_step + 1);
        }

        void GoBack()
        {
            if (_step <= 0) return;
            SetStep(_step - 1);
        }

        void SetStep(int step)
        {
            _step = Mathf.Clamp(step, 0, Questions.Length + 1);
            _selectedIndex = GetSelectedAnswerIndex();
            if (_selectedIndex < 0 || !IsSelectable(_selectedIndex))
                _selectedIndex = FindFirstSelectable();

            if (_step == Questions.Length + 1)
                CompleteAnswers();

            RenderCurrentStep();
        }

        void RestartFlow(bool clearSavedAnswers)
        {
            _answers.style = null;
            _answers.palette = null;
            _answers.householdSize = null;
            _answers.investmentTier = null;
            _completionRaised = false;
            _step = 0;
            _selectedIndex = 0;

            if (clearSavedAnswers)
            {
                PlayerPrefs.DeleteKey(StyleKey);
                PlayerPrefs.DeleteKey(PaletteKey);
                PlayerPrefs.DeleteKey(HouseholdKey);
                PlayerPrefs.DeleteKey(InvestmentKey);
                PlayerPrefs.Save();
            }

            Rebuild();
        }

        void CompleteAnswers()
        {
            PlayerPrefs.SetString(StyleKey, _answers.style ?? string.Empty);
            PlayerPrefs.SetString(PaletteKey, _answers.palette ?? string.Empty);
            PlayerPrefs.SetString(HouseholdKey, _answers.householdSize ?? string.Empty);
            PlayerPrefs.SetString(InvestmentKey, _answers.investmentTier ?? string.Empty);
            PlayerPrefs.Save();

            if (_completionRaised) return;
            _completionRaised = true;
            OnAnswersCompleted?.Invoke(_answers.Clone());
        }

        int GetSelectableCount()
        {
            if (_step >= 1 && _step <= Questions.Length)
                return Questions[_step - 1].options.Length;
            if (_step == Questions.Length + 1)
                return 2;
            return 1;
        }

        bool IsSelectable(int index)
        {
            if (index < 0 || index >= GetSelectableCount()) return false;

            // The reference catalog only has Light and Wood palettes for Scandinavian kitchens.
            if (_step == 2 && _answers.style == "natural & scandinavian")
            {
                string value = Questions[1].options[index].value;
                if (value == "dark" || value == "bold")
                    return false;
            }

            return true;
        }

        int FindFirstSelectable()
        {
            for (int i = 0; i < GetSelectableCount(); i++)
                if (IsSelectable(i)) return i;
            return 0;
        }

        int GetSelectedAnswerIndex()
        {
            if (_step < 1 || _step > Questions.Length)
                return 0;

            string answer = GetAnswer(_step - 1);
            OptionData[] options = Questions[_step - 1].options;
            for (int i = 0; i < options.Length; i++)
                if (options[i].value == answer) return i;
            return -1;
        }

        string GetAnswer(int questionIndex)
        {
            switch (questionIndex)
            {
                case 0: return _answers.style;
                case 1: return _answers.palette;
                case 2: return _answers.householdSize;
                case 3: return _answers.investmentTier;
                default: return null;
            }
        }

        void SetAnswer(int questionIndex, string value)
        {
            switch (questionIndex)
            {
                case 0:
                    _answers.style = value;
                    if (value == "natural & scandinavian" &&
                        (_answers.palette == "dark" || _answers.palette == "bold"))
                        _answers.palette = null;
                    break;
                case 1: _answers.palette = value; break;
                case 2: _answers.householdSize = value; break;
                case 3: _answers.investmentTier = value; break;
            }
        }

        void RenderCurrentStep()
        {
            if (_content == null) return;
            ClearChildren(_content.transform);

            // Background only shows on the first (welcome) page.
            if (_background != null)
                _background.SetActive(_step == 0);

            if (_step == 0)
                BuildWelcome(_content.transform);
            else if (_step <= Questions.Length)
                BuildQuestion(_content.transform, Questions[_step - 1]);
            else
                BuildResult(_content.transform);

            Canvas.ForceUpdateCanvases();
            RectTransform rootRect = _root != null ? _root.GetComponent<RectTransform>() : null;
            if (rootRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        void BuildWelcome(Transform parent)
        {
            GameObject panel = CreateResponsivePanel("WelcomePanel", parent, 760f, 440f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout, new RectOffset(48, 48, 34, 34), 12f, TextAnchor.MiddleCenter);

            AddFlexibleSpacer(panel.transform, "TopSpace", 1f);

            if (logoSprite != null)
            {
                GameObject logo = MakeUIObject("RoomReviveLogo", panel.transform);
                LayoutElement logoLayout = logo.AddComponent<LayoutElement>();
                logoLayout.minWidth = 200f;
                logoLayout.preferredWidth = 360f;
                logoLayout.minHeight = 100f;
                logoLayout.preferredHeight = 170f;
                UIImage image = logo.AddComponent<UIImage>();
                image.sprite = logoSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            CreateLayoutText("Title", panel.transform, "Welcome to RoomRevive", 48f, FontStyles.Bold,
                Dark, 76f, TextAlignmentOptions.Center, 0f, 680f);

            AddFlexibleSpacer(panel.transform, "ButtonSpace", 0.6f);
            CreateLayoutActionButton(panel.transform, "StartButton", "Start", 230f, 58f,
                true, () => SetStep(1));
            AddFlexibleSpacer(panel.transform, "BottomSpace", 1f);
        }

        void BuildQuestion(Transform parent, QuestionData question)
        {
            GameObject page = MakeUIObject("QuestionPage", parent);
            Stretch(page);
            VerticalLayoutGroup pageLayout = page.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(pageLayout, new RectOffset(42, 42, 24, 26), 10f, TextAnchor.UpperCenter);
            pageLayout.childForceExpandWidth = true;

            CreateLayoutText("Kicker", page.transform, question.kicker, 13f, FontStyles.Bold, Muted,
                28f, TextAlignmentOptions.Center, 2.5f);
            CreateLayoutText("Title", page.transform, question.title, 36f, FontStyles.Bold, Dark,
                58f, TextAlignmentOptions.Center);

            bool imageQuestion = _step <= 2;
            AddFlexibleSpacer(page.transform, "TopCardSpace", 1f);

            GameObject optionRow = MakeUIObject("OptionRow", page.transform);
            LayoutElement rowLayout = optionRow.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = imageQuestion ? 360f : 120f;
            rowLayout.minHeight = imageQuestion ? 200f : 100f;
            HorizontalLayoutGroup rowGroup = optionRow.AddComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = imageQuestion ? 20f : 24f;
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = imageQuestion;
            rowGroup.childForceExpandHeight = imageQuestion;

            for (int i = 0; i < question.options.Length; i++)
            {
                int captured = i;
                BuildOptionCard(optionRow.transform, question.options[i], captured, imageQuestion, IsSelectable(i),
                    () => PickOption(captured));
            }

            AddFlexibleSpacer(page.transform, "BottomCardSpace", 1f);
            BuildQuestionFooter(page.transform);
        }

        void BuildOptionCard(Transform parent, OptionData option, int optionIndex,
            bool showImage, bool available, UnityEngine.Events.UnityAction onClick)
        {
            bool selected = optionIndex == _selectedIndex;
            GameObject card = MakeUIObject("Option_" + option.label, parent);
            LayoutElement cardLayout = card.AddComponent<LayoutElement>();
            cardLayout.minWidth = showImage ? 90f : 160f;
            cardLayout.preferredWidth = showImage ? 244f : 220f;
            cardLayout.flexibleWidth = showImage ? 1f : 0f;
            cardLayout.minHeight = showImage ? 0f : 90f;
            cardLayout.preferredHeight = showImage ? 0f : 110f;
            cardLayout.flexibleHeight = showImage ? 1f : 0f;

            UIImage border = card.AddComponent<UIImage>();
            border.sprite = _roundedSprite;
            border.type = Image.Type.Sliced;
            border.color = selected ? Dark : Panel;
            border.raycastTarget = available;

            VerticalLayoutGroup borderLayout = card.AddComponent<VerticalLayoutGroup>();
            int inset = selected ? 5 : 0;
            ConfigureVerticalLayout(borderLayout, new RectOffset(inset, inset, inset, inset), 0f, TextAnchor.MiddleCenter);
            borderLayout.childForceExpandWidth = true;
            borderLayout.childForceExpandHeight = true;

            GameObject inner = MakeUIObject("Inner", card.transform);
            LayoutElement innerLayoutElement = inner.AddComponent<LayoutElement>();
            innerLayoutElement.flexibleWidth = 1f;
            innerLayoutElement.flexibleHeight = 1f;
            UIImage fill = inner.AddComponent<UIImage>();
            fill.sprite = _roundedSprite;
            fill.type = Image.Type.Sliced;
            fill.color = Panel;
            fill.raycastTarget = false;
            Mask mask = inner.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            VerticalLayoutGroup innerLayout = inner.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(innerLayout, new RectOffset(0, 0, 0, 0), 0f, TextAnchor.MiddleCenter);
            innerLayout.childForceExpandWidth = true;
            innerLayout.childForceExpandHeight = false;

            if (showImage)
            {
                GameObject imageGO = MakeUIObject("Preview", inner.transform);
                LayoutElement imageLayout = imageGO.AddComponent<LayoutElement>();
                imageLayout.minHeight = 90f;
                imageLayout.preferredHeight = 240f;
                imageLayout.flexibleHeight = 1f;
                AddQuestionImage(imageGO, option, (_step - 1) * 4 + optionIndex);

                GameObject labelArea = MakeUIObject("LabelArea", inner.transform);
                LayoutElement labelLayout = labelArea.AddComponent<LayoutElement>();
                labelLayout.preferredHeight = 82f;
                VerticalLayoutGroup labelGroup = labelArea.AddComponent<VerticalLayoutGroup>();
                ConfigureVerticalLayout(labelGroup, new RectOffset(17, 17, 9, 11), 2f, TextAnchor.MiddleLeft);
                labelGroup.childForceExpandWidth = true;

                CreateLayoutText("Label", labelArea.transform, option.label, 19f, FontStyles.Bold, Muted,
                    30f, TextAlignmentOptions.Left);
                CreateLayoutText("Hint", labelArea.transform, option.hint, 13f, FontStyles.Normal, Muted,
                    24f, TextAlignmentOptions.Left);
            }
            else
            {
                AddFlexibleSpacer(inner.transform, "TopSpace", 1f);
                CreateLayoutText("Label", inner.transform, option.label, 22f, FontStyles.Bold, Muted,
                    40f, TextAlignmentOptions.Center);
                if (option.hint.Length > 0)
                {
                    CreateLayoutText("Hint", inner.transform, option.hint, 14f, FontStyles.Normal, Muted,
                        28f, TextAlignmentOptions.Center);
                }
                AddFlexibleSpacer(inner.transform, "BottomSpace", 1f);
            }

            if (selected && available)
            {
                GameObject badge = MakeUIObject("SelectedBadge", card.transform);
                LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
                badgeLayout.ignoreLayout = true;
                RectTransform badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = Vector2.one;
                badgeRect.sizeDelta = new Vector2(36f, 36f);
                badgeRect.anchoredPosition = new Vector2(-18f, -18f);
                UIImage badgeImage = badge.AddComponent<UIImage>();
                badgeImage.sprite = _roundedSprite;
                badgeImage.type = Image.Type.Sliced;
                badgeImage.color = PanelLight;
                badgeImage.raycastTarget = false;

                Texture2D selectedIcon = Resources.Load<Texture2D>(ResourceRoot + "SelectedCheck");
                if (selectedIcon != null)
                {
                    GameObject icon = MakeUIObject("CheckIcon", badge.transform);
                    Place(icon, new Vector2(24f, 24f), Vector2.zero);
                    RawImage iconImage = icon.AddComponent<RawImage>();
                    iconImage.texture = selectedIcon;
                    iconImage.raycastTarget = false;
                }
                else
                {
                    CreateText("Check", badge.transform, "✓", 21f, FontStyles.Bold, Dark,
                        new Vector2(36f, 36f), Vector2.zero, TextAlignmentOptions.Center);
                }
            }

            if (!available)
            {
                GameObject overlay = MakeUIObject("Unavailable", card.transform);
                LayoutElement overlayLayout = overlay.AddComponent<LayoutElement>();
                overlayLayout.ignoreLayout = true;
                Stretch(overlay);
                UIImage overlayImage = overlay.AddComponent<UIImage>();
                overlayImage.sprite = _roundedSprite;
                overlayImage.type = Image.Type.Sliced;
                overlayImage.color = Disabled;
                overlayImage.raycastTarget = true;
                TextMeshProUGUI unavailableLabel = CreateText(
                    "UnavailableLabel", overlay.transform, "Not available with this style", 12f,
                    FontStyles.Bold, Muted, new Vector2(210f, 40f),
                    Vector2.zero, TextAlignmentOptions.Center);
                RectTransform unavailableRect = unavailableLabel.rectTransform;
                unavailableRect.anchorMin = new Vector2(0.5f, 0f);
                unavailableRect.anchorMax = new Vector2(0.5f, 0f);
                unavailableRect.pivot = new Vector2(0.5f, 0f);
                unavailableRect.anchoredPosition = new Vector2(0f, 12f);
            }

            Button button = card.AddComponent<Button>();
            button.targetGraphic = border;
            button.interactable = available;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);

            if (available)
            {
                EventTrigger trigger = card.AddComponent<EventTrigger>();
                AddTrigger(trigger, EventTriggerType.PointerEnter, _ => SelectWithoutConfirm(optionIndex));
            }
        }

        void AddQuestionImage(GameObject imageGO, OptionData option, int flatIndex)
        {
            Sprite overrideSprite = null;
            if (questionImageOverrides != null && flatIndex >= 0 && flatIndex < questionImageOverrides.Length)
                overrideSprite = questionImageOverrides[flatIndex];

            if (overrideSprite != null)
            {
                UIImage image = imageGO.AddComponent<UIImage>();
                image.sprite = overrideSprite;
                image.preserveAspect = false;
                image.raycastTarget = false;
                return;
            }

            Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + option.resourceName);
            if (texture != null)
            {
                RawImage raw = imageGO.AddComponent<RawImage>();
                raw.texture = texture;
                raw.uvRect = new Rect(0f, 0f, 1f, 1f);
                raw.raycastTarget = false;
            }
            else
            {
                UIImage placeholder = imageGO.AddComponent<UIImage>();
                placeholder.color = PanelLight;
                placeholder.raycastTarget = false;
                CreateText("MissingImage", imageGO.transform, option.label, 15f, FontStyles.Bold, Muted,
                    new Vector2(180f, 30f), Vector2.zero, TextAlignmentOptions.Center);
            }
        }

        void SelectWithoutConfirm(int index)
        {
            if (_selectedIndex == index || !IsSelectable(index)) return;
            _selectedIndex = index;
            RenderCurrentStep();
        }

        void BuildQuestionFooter(Transform parent)
        {
            GameObject footer = MakeUIObject("QuestionFooter", parent);
            LayoutElement footerLayout = footer.AddComponent<LayoutElement>();
            footerLayout.preferredHeight = 42f;
            footerLayout.minHeight = 42f;
            HorizontalLayoutGroup footerGroup = footer.AddComponent<HorizontalLayoutGroup>();
            footerGroup.spacing = 12f;
            footerGroup.childAlignment = TextAnchor.MiddleCenter;
            footerGroup.childControlWidth = true;
            footerGroup.childControlHeight = true;
            footerGroup.childForceExpandWidth = false;
            footerGroup.childForceExpandHeight = false;

            CreateLayoutSmallButton(footer.transform, "BackButton", "‹  Back", 116f, 42f,
                GoBack, false, 14f);
            AddFlexibleSpacer(footer.transform, "LeftFooterSpace", 1f);

            GameObject progress = MakeUIObject("Dots", footer.transform);
            LayoutElement progressLayout = progress.AddComponent<LayoutElement>();
            progressLayout.minHeight = 38f;
            progressLayout.preferredHeight = 38f;
            HorizontalLayoutGroup progressGroup = progress.AddComponent<HorizontalLayoutGroup>();
            progressGroup.spacing = 8f;
            progressGroup.childAlignment = TextAnchor.MiddleCenter;
            progressGroup.childControlWidth = true;
            progressGroup.childControlHeight = true;
            progressGroup.childForceExpandWidth = false;
            progressGroup.childForceExpandHeight = false;
            ContentSizeFitter progressFitter = progress.AddComponent<ContentSizeFitter>();
            progressFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            progressFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            for (int i = 0; i < Questions.Length; i++)
            {
                bool current = i + 1 == _step;
                GameObject dot = MakeUIObject("Dot" + i, progress.transform);
                LayoutElement dotLayout = dot.AddComponent<LayoutElement>();
                dotLayout.minWidth = 8f;
                dotLayout.preferredWidth = 8f;
                dotLayout.minHeight = 8f;
                dotLayout.preferredHeight = 8f;
                UIImage image = dot.AddComponent<UIImage>();
                image.sprite = _roundedSprite;
                image.type = Image.Type.Sliced;
                image.color = current ? Dark : new Color(Dark.r, Dark.g, Dark.b, 0.25f);
                image.raycastTarget = false;
            }

            AddFlexibleSpacer(footer.transform, "RightFooterSpace", 1f);
            GameObject balance = MakeUIObject("BackButtonBalance", footer.transform);
            LayoutElement balanceLayout = balance.AddComponent<LayoutElement>();
            balanceLayout.minWidth = 116f;
            balanceLayout.preferredWidth = 116f;
            balanceLayout.minHeight = 42f;
            balanceLayout.preferredHeight = 42f;
        }

        void BuildResult(Transform parent)
        {
            GameObject panel = CreateResponsivePanel("ResultPanel", parent, 700f, 500f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout, new RectOffset(44, 44, 30, 28), 10f, TextAnchor.MiddleCenter);

            AddFlexibleSpacer(panel.transform, "TopSpace", 1f);

            GameObject badge = MakeUIObject("CompleteBadge", panel.transform);
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.preferredWidth = 100f;
            badgeLayout.preferredHeight = 100f;
            UIImage badgeImage = badge.AddComponent<UIImage>();
            badgeImage.sprite = _roundedSprite;
            badgeImage.type = Image.Type.Sliced;
            badgeImage.color = Accent;
            badgeImage.raycastTarget = false;

            Texture2D completionIcon = Resources.Load<Texture2D>(ResourceRoot + "SelectedCheck");
            if (completionIcon != null)
            {
                GameObject icon = MakeUIObject("CheckIcon", badge.transform);
                Place(icon, new Vector2(58f, 58f), Vector2.zero);
                RawImage iconImage = icon.AddComponent<RawImage>();
                iconImage.texture = completionIcon;
                iconImage.raycastTarget = false;
            }
            else
            {
                CreateText("Check", badge.transform, "✓", 50f, FontStyles.Bold, Dark,
                    new Vector2(100f, 100f), Vector2.zero, TextAlignmentOptions.Center);
            }

            CreateLayoutText("Title", panel.transform, "You're all set.", 46f, FontStyles.Bold, Dark,
                64f, TextAlignmentOptions.Center, 0f, 620f);
            CreateLayoutText("Body", panel.transform,
                "Your answers are saved.\nDiscover recommendations put together just for you.",
                20f, FontStyles.Normal, Muted, 82f, TextAlignmentOptions.Center, 0f, 610f);

            AddFlexibleSpacer(panel.transform, "ButtonSpace", 0.4f);
            CreateLayoutActionButton(panel.transform, "ExploreButton", "Explore the room", 260f, 58f,
                _selectedIndex == 0, () => OnScanRequested?.Invoke());
            CreateLayoutSmallButton(panel.transform, "StartOverButton", "Start over", 150f, 40f,
                () => RestartFlow(true), _selectedIndex == 1);
            AddFlexibleSpacer(panel.transform, "BottomSpace", 1f);
        }

        void BuildBackground(Transform parent)
        {
            _background = null;
            if (!showBackground) return;

            GameObject bg = MakeUIObject("Background", parent);
            _background = bg;
            Stretch(bg);
            UIImage image = bg.AddComponent<UIImage>();
            if (backgroundSprite != null)
            {
                image.sprite = backgroundSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
            }
            else
            {
                image.sprite = _roundedSprite;
                image.type = Image.Type.Sliced;
            }
            image.color = backgroundColor;
            // Block raycasts so the background catches stray pointer events instead of passthrough,
            // but it sits under every interactive control so it never steals their clicks.
            image.raycastTarget = true;
        }

        GameObject CreateResponsivePanel(string name, Transform parent,
            float preferredWidth, float preferredHeight)
        {
            GameObject page = MakeUIObject(name + "Page", parent);
            Stretch(page);
            VerticalLayoutGroup pageLayout = page.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(pageLayout, new RectOffset(200, 200, 100, 100), 0f, TextAnchor.MiddleCenter);
            pageLayout.childForceExpandWidth = true;
            pageLayout.childForceExpandHeight = true;

            GameObject centerRow = MakeUIObject("CenterRow", page.transform);
            LayoutElement rowLayout = centerRow.AddComponent<LayoutElement>();
            rowLayout.minHeight = Mathf.Min(360f, preferredHeight);
            rowLayout.preferredHeight = preferredHeight;
            rowLayout.flexibleHeight = 6f;
            HorizontalLayoutGroup rowGroup = centerRow.AddComponent<HorizontalLayoutGroup>();
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = true;
            rowGroup.childForceExpandHeight = true;

            GameObject panel = MakeUIObject(name, centerRow.transform);
            LayoutElement panelLayout = panel.AddComponent<LayoutElement>();
            panelLayout.minWidth = Mathf.Min(480f, preferredWidth);
            panelLayout.preferredWidth = preferredWidth;
            panelLayout.flexibleWidth = 4f;
            panelLayout.minHeight = Mathf.Min(360f, preferredHeight);
            panelLayout.preferredHeight = preferredHeight;
            panelLayout.flexibleHeight = 1f;
            UIImage image = panel.AddComponent<UIImage>();
            image.sprite = _roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Panel;
            image.raycastTarget = false;
            Shadow shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.09f, 0.14f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -14f);

            return panel;
        }

        void CreateLayoutActionButton(Transform parent, string name, string label,
            float preferredWidth, float preferredHeight, bool selected,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGO = MakeUIObject(name, parent);
            LayoutElement buttonLayout = buttonGO.AddComponent<LayoutElement>();
            buttonLayout.minWidth = preferredWidth;
            buttonLayout.preferredWidth = preferredWidth;
            buttonLayout.minHeight = preferredHeight;
            buttonLayout.preferredHeight = preferredHeight;
            buttonLayout.flexibleWidth = 0f;
            buttonLayout.flexibleHeight = 0f;
            UIImage image = buttonGO.AddComponent<UIImage>();
            image.sprite = _roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = selected ? Dark : new Color(Dark.r, Dark.g, Dark.b, 0.72f);

            Button button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);

            CreateText("Label", buttonGO.transform, label, 17f, FontStyles.Bold, CtaText,
                new Vector2(preferredWidth, preferredHeight), Vector2.zero, TextAlignmentOptions.Center);
        }

        void CreateLayoutSmallButton(Transform parent, string name, string label,
            float preferredWidth, float preferredHeight, UnityEngine.Events.UnityAction onClick,
            bool selected = false, float fontSize = 14f)
        {
            GameObject buttonGO = MakeUIObject(name, parent);
            LayoutElement buttonLayout = buttonGO.AddComponent<LayoutElement>();
            buttonLayout.minWidth = preferredWidth;
            buttonLayout.preferredWidth = preferredWidth;
            buttonLayout.minHeight = preferredHeight;
            buttonLayout.preferredHeight = preferredHeight;
            buttonLayout.flexibleWidth = 0f;
            buttonLayout.flexibleHeight = 0f;
            UIImage image = buttonGO.AddComponent<UIImage>();
            image.sprite = _roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = selected ? Dark : new Color(Dark.r, Dark.g, Dark.b, 0.5f);

            Button button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);

            CreateText("Label", buttonGO.transform, label, fontSize, FontStyles.Bold,
                CtaText, new Vector2(preferredWidth, preferredHeight),
                Vector2.zero, TextAlignmentOptions.Center);
        }

        TextMeshProUGUI CreateLayoutText(string name, Transform parent, string text, float size,
            FontStyles style, Color color, float preferredHeight, TextAlignmentOptions alignment,
            float characterSpacing = 0f, float preferredWidth = -1f)
        {
            GameObject go = MakeUIObject(name, parent);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(18f, preferredHeight * 0.65f);
            layout.preferredHeight = preferredHeight;
            if (preferredWidth > 0f)
            {
                layout.minWidth = preferredWidth * 0.55f;
                layout.preferredWidth = preferredWidth;
            }
            else
            {
                layout.flexibleWidth = 1f;
            }

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontSizeMax = size;
            tmp.fontSizeMin = Mathf.Max(10f, size * 0.68f);
            tmp.enableAutoSizing = true;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.characterSpacing = characterSpacing;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            if (_resolvedFontAsset != null) tmp.font = _resolvedFontAsset;
            return tmp;
        }

        static void ConfigureVerticalLayout(VerticalLayoutGroup layout, RectOffset padding,
            float spacing, TextAnchor alignment)
        {
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        GameObject AddFlexibleSpacer(Transform parent, string name, float flexibility)
        {
            GameObject spacer = MakeUIObject(name, parent);
            LayoutElement layout = spacer.AddComponent<LayoutElement>();
            layout.flexibleWidth = flexibility;
            layout.flexibleHeight = flexibility;
            return spacer;
        }

        TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
            FontStyles style, Color color, Vector2 rectSize, Vector2 position,
            TextAlignmentOptions alignment, float characterSpacing = 0f)
        {
            GameObject go = MakeUIObject(name, parent);
            Place(go, rectSize, position);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.characterSpacing = characterSpacing;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (_resolvedFontAsset != null) tmp.font = _resolvedFontAsset;
            return tmp;
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        void GrabComponents()
        {
            _canvas = GetComponent<Canvas>();
            _scaler = GetComponent<CanvasScaler>();
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        void ResolveProductBrowserFont()
        {
            _resolvedFontAsset = null;

            ProductSwapView[] productViews = FindObjectsByType<ProductSwapView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < productViews.Length; i++)
            {
                ProductSwapView view = productViews[i];
                if (view == null || !view.gameObject.scene.IsValid()) continue;

                TMP_FontAsset productFont =
                    view.productNameLabel != null ? view.productNameLabel.font :
                    view.headlineLabel != null ? view.headlineLabel.font :
                    view.shortDescriptionLabel != null ? view.shortDescriptionLabel.font : null;

                if (productFont != null)
                {
                    _resolvedFontAsset = productFont;
                    break;
                }
            }

            if (_resolvedFontAsset == null)
            {
                _resolvedFontAsset = Resources.Load<TMP_FontAsset>(
                    "Fonts & Materials/LiberationSans SDF");
            }

            if (_resolvedFontAsset == null)
                _resolvedFontAsset = fontAsset;
        }

        void SetupCanvas()
        {
            if (_canvas == null) return;
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = eventCamera != null ? eventCamera : Camera.main;
            transform.localScale = Vector3.one * worldScale;

            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(
                    Mathf.Max(640f, canvasSize.x),
                    Mathf.Max(420f, canvasSize.y));
                rect.pivot = Vector2.one * 0.5f;
            }

            if (_scaler != null)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _scaler.referencePixelsPerUnit = 100f;
                _scaler.dynamicPixelsPerUnit = 10f;
            }
        }

        void ClearGeneratedUI()
        {
            if (_root != null)
            {
                DestroyObject(_root);
                _root = null;
                _content = null;
            }

            Transform existing = transform.Find("Generated_WelcomeUI");
            if (existing != null)
                DestroyObject(existing.gameObject);

            foreach (Sprite sprite in _generatedSprites)
                if (sprite != null) DestroyObject(sprite);
            foreach (Texture2D texture in _generatedTextures)
                if (texture != null) DestroyObject(texture);

            _generatedSprites.Clear();
            _generatedTextures.Clear();
            _roundedSprite = null;
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyObject(parent.GetChild(i).gameObject);
        }

        GameObject MakeUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            return go;
        }

        static void Place(GameObject go, Vector2 size, Vector2 position)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        static void Stretch(GameObject go)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        Sprite CreateRoundedSprite()
        {
            const int textureSize = 64;
            const float radius = 30f;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "OnboardingRoundedRect";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[textureSize * textureSize];

            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float edgeX = Mathf.Max(radius - px, px - (textureSize - radius), 0f);
                float edgeY = Mathf.Max(radius - py, py - (textureSize - radius), 0f);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(edgeX * edgeX + edgeY * edgeY) + 0.5f);
                pixels[y * textureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize),
                Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "OnboardingRoundedRect";
            _generatedTextures.Add(texture);
            _generatedSprites.Add(sprite);
            return sprite;
        }

        static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        static Color Hex(uint rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f);
        }
    }
}
