using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;

namespace RoomRevive
{
    /// <summary>
    /// Simple world-space "Loading" overlay: a rounded panel with a ring of pulsing dots and an
    /// animated "Loading…" label. Self-builds its UI, fades in/out, and can head-follow the camera.
    /// Call Show()/Hide() (or wire the UnityEvents) to toggle it.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class LoadingUI : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private float worldScale = 0.001f;
        [SerializeField] private Vector2 canvasSize = new Vector2(520f, 360f);
        [SerializeField] private Camera eventCamera;

        [Header("Content")]
        [SerializeField] private string label = "Loading cabinet";
        [SerializeField, Range(4, 16)] private int dotCount = 8;
        [Tooltip("Loading-ring rotation speed, in revolutions per second. Negative reverses direction.")]
        [SerializeField] private float spinSpeed = 1.1f;
        [SerializeField] private Color accent = Hex(0x5E97B8);
        [SerializeField] private Color textColor = Hex(0x3A4055);
        [SerializeField] private Color panelColor = new Color(0.71f, 0.737f, 0.816f, 0.985f);

        [Header("Behaviour")]
        [Tooltip("Show automatically when the component starts (Play mode).")]
        [SerializeField] private bool showOnStart = false;
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Head Follow")]
        [SerializeField] private bool followUserHead = true;
        [SerializeField] private float distance = 1.4f;
        [SerializeField] private float rightOffset = 0f;
        [SerializeField] private float upOffset = -0.05f;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Auto Rebuild")]
        [SerializeField] private bool autoRebuildInEditor = true;

        Canvas _canvas;
        CanvasScaler _scaler;
        CanvasGroup _group;
        Transform _cam;
        GameObject _root;
        RectTransform _dotsRoot;
        TextMeshProUGUI _labelText;
        readonly List<UIImage> _dots = new List<UIImage>();
        readonly List<Object> _generated = new List<Object>();
        Sprite _circle;
        float _spin;
        float _dotTime;
        bool _visible;
        float _targetAlpha;

#if UNITY_EDITOR
        bool _rebuildQueued;
#endif

        void Awake()
        {
            GrabComponents();
            SetupCanvas();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            SetupCameraReference();
            SetVisibleImmediate(showOnStart);
        }

        void OnDestroy() => ClearGenerated();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || !autoRebuildInEditor || _rebuildQueued) return;
            _rebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (this == null) return;
                GrabComponents();
                SetupCanvas();
                Rebuild();
                SetVisibleImmediate(true); // preview in edit mode
            };
        }
#endif

        [ContextMenu("Rebuild UI")]
        public void Rebuild()
        {
#if UNITY_EDITOR
            // Never build UI on a prefab ASSET — SetParent is disabled there ("would corrupt data").
            // Editing happens on scene instances / in Prefab Mode (a valid preview scene), where it's fine.
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            ClearGenerated();
            GrabComponents();
            SetupCanvas();
            _circle = CreateCircleSprite();

            _root = MakeUI("Generated_LoadingUI", transform);
            // Don't serialize the generated UI into the scene — it's rebuilt on load, so saving it just
            // accumulates stray copies.
            _root.hideFlags = HideFlags.DontSave;
            Stretch(_root);

            // Panel
            GameObject panel = MakeUI("Panel", _root.transform);
            RectTransform pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = Vector2.one * 0.5f;
            pr.sizeDelta = new Vector2(canvasSize.x * 0.78f, canvasSize.y * 0.78f);
            UIImage panelImg = panel.AddComponent<UIImage>();
            panelImg.sprite = _circle;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = panelColor;
            panelImg.raycastTarget = false;

            // Spinner dots
            GameObject dots = MakeUI("Dots", panel.transform);
            _dotsRoot = dots.GetComponent<RectTransform>();
            _dotsRoot.anchorMin = _dotsRoot.anchorMax = _dotsRoot.pivot = Vector2.one * 0.5f;
            _dotsRoot.anchoredPosition = new Vector2(0f, 34f);
            _dotsRoot.sizeDelta = new Vector2(140f, 140f);

            _dots.Clear();
            float radius = 56f;
            for (int i = 0; i < dotCount; i++)
            {
                float ang = (i / (float)dotCount) * Mathf.PI * 2f;
                GameObject dot = MakeUI("Dot" + i, _dotsRoot);
                RectTransform dr = dot.GetComponent<RectTransform>();
                dr.anchorMin = dr.anchorMax = dr.pivot = Vector2.one * 0.5f;
                dr.sizeDelta = Vector2.one * 16f;
                dr.anchoredPosition = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
                UIImage img = dot.AddComponent<UIImage>();
                img.sprite = _circle;
                img.color = accent;
                img.raycastTarget = false;
                _dots.Add(img);
            }

            // Label
            GameObject labelGO = MakeUI("Label", panel.transform);
            RectTransform lr = labelGO.GetComponent<RectTransform>();
            lr.anchorMin = lr.anchorMax = lr.pivot = Vector2.one * 0.5f;
            lr.anchoredPosition = new Vector2(0f, -78f);
            lr.sizeDelta = new Vector2(canvasSize.x * 0.7f, 60f);
            _labelText = labelGO.AddComponent<TextMeshProUGUI>();
            _labelText.text = label;
            _labelText.fontSize = 34f;
            _labelText.fontStyle = FontStyles.Bold;
            _labelText.color = textColor;
            _labelText.alignment = TextAlignmentOptions.Center;
            _labelText.raycastTarget = false;
            if (fontAsset != null) _labelText.font = fontAsset;
        }

        void Update()
        {
            // Fade
            if (_group != null && !Mathf.Approximately(_group.alpha, _targetAlpha))
            {
                float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
                _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, step);
                bool on = _group.alpha > 0.01f;
                _group.blocksRaycasts = _group.interactable = on;
            }

            // Animate the spinner every frame at runtime (independent of visibility) so the dots are
            // always turning — no need to be shown first. Skip when nothing is built yet.
            if (_dotsRoot == null) return;

            // Rotate the whole ring so the dots orbit the center. All dots share one solid color.
            _spin += Time.unscaledDeltaTime * spinSpeed;
            _dotTime += Time.unscaledDeltaTime;
            _dotsRoot.localRotation = Quaternion.Euler(0f, 0f, -_spin * 360f);

            if (_labelText != null)
            {
                int n = (int)(_dotTime * 2f) % 4;
                _labelText.text = label + new string('.', n);
            }
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !followUserHead) return;
            if (_group == null || _group.alpha < 0.01f) return;
            if (_cam == null) SetupCameraReference();
            if (_cam == null) return;

            transform.position = _cam.position + _cam.forward * distance
                + _cam.right * rightOffset + _cam.up * upOffset;
            transform.rotation = Quaternion.LookRotation(_cam.forward);
        }

        // ── Public API ──────────────────────────────────────────────
        public bool IsVisible => _visible;

        [ContextMenu("Show")]
        public void Show()
        {
            if (_root == null) Rebuild();
            _visible = true;
            _targetAlpha = 1f;
        }

        [ContextMenu("Hide")]
        public void Hide()
        {
            _visible = false;
            _targetAlpha = 0f;
        }

        public void SetVisible(bool visible)
        {
            if (visible) Show(); else Hide();
        }

        public void SetLabel(string text)
        {
            label = text;
            if (_labelText != null) _labelText.text = text;
        }

        void SetVisibleImmediate(bool visible)
        {
            if (_root == null) Rebuild();
            _visible = visible;
            _targetAlpha = visible ? 1f : 0f;
            if (_group != null)
            {
                _group.alpha = _targetAlpha;
                _group.blocksRaycasts = _group.interactable = visible;
            }
        }

        // ── Setup helpers ───────────────────────────────────────────
        void GrabComponents()
        {
            _canvas = GetComponent<Canvas>();
            _scaler = GetComponent<CanvasScaler>();
            _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
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
                rect.sizeDelta = new Vector2(Mathf.Max(320f, canvasSize.x), Mathf.Max(240f, canvasSize.y));
                rect.pivot = Vector2.one * 0.5f;
            }
            if (_scaler != null)
                _scaler.dynamicPixelsPerUnit = 10f;
        }

        void SetupCameraReference()
        {
            GameObject eye = GameObject.Find("CenterEyeAnchor");
            _cam = eye != null ? eye.transform : (Camera.main != null ? Camera.main.transform : null);
        }

        GameObject MakeUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            return go;
        }

        static void Stretch(GameObject go)
        {
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void ClearGenerated()
        {
            if (_root != null) DestroyObj(_root);
            Transform existing = transform.Find("Generated_LoadingUI");
            if (existing != null) DestroyObj(existing.gameObject);
            foreach (Object o in _generated) if (o != null) DestroyObj(o);
            _generated.Clear();
            _dots.Clear();
            _root = null; _circle = null; _labelText = null; _dotsRoot = null;
        }

        Sprite CreateCircleSprite()
        {
            const int size = 64; const float r = 30f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "LoadingCircle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float ex = Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (size - r), 0f);
                float ey = Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (size - r), 0f);
                float a = Mathf.Clamp01(r - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px); tex.Apply();
            Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            s.name = "LoadingCircle";
            _generated.Add(tex); _generated.Add(s);
            return s;
        }

        static void DestroyObj(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
