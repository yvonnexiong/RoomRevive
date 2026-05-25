using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

/// <summary>
/// Spawns a world-space canvas with two buttons (LEFT / RIGHT) that translate a target
/// box along the X axis. All Meta Interaction SDK ray-interaction components are wired
/// up procedurally so the hand ray from <c>OVRInteractionComprehensive</c> can click them.
///
/// You can either:
///   • Assign <see cref="targetBox"/> in the inspector, OR
///   • Leave it null and the panel will look for a GameObject named
///     <see cref="fallbackBoxName"/> (default: "MovableBox"); if none exists it will
///     create a cube primitive at <see cref="autoBoxPosition"/>.
/// </summary>
public class BoxMoverPanel : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Box to move. If null, panel will GameObject.Find(fallbackBoxName) or create a cube.")]
    public Transform targetBox;
    public string fallbackBoxName = "MovableBox";
    public Vector3 autoBoxPosition = new Vector3(0f, 1.4f, 1.5f);
    public Vector3 autoBoxScale    = new Vector3(0.3f, 0.3f, 0.3f);

    [Header("Movement")]
    [Tooltip("World-space distance to move per click along the local X axis.")]
    public float stepDistance = 0.25f;

    [Header("Canvas placement")]
    public Vector3 canvasPosition = new Vector3(0f, 1.1f, 2f);
    public float canvasScale = 0.005f;
    public Vector2 canvasSize = new Vector2(700f, 220f);

    // --- runtime refs ---
    Canvas canvas;
    Button leftButton, rightButton;

    void Start()
    {
        if (Application.isPlaying)
            SpawnIfNeeded();
    }

    [ContextMenu("Spawn UI")]
    public void SpawnUI()
    {
        if (IsSpawned())
        {
            Debug.Log("[BoxMoverPanel] UI already spawned — skipping. Use 'Clear' first to rebuild.", this);
            return;
        }
        Build();
    }

    [ContextMenu("Clear Spawned UI")]
    public void ClearSpawned()
    {
        var existing = transform.Find("BoxMoverPanel_Canvas");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        canvas = null;
        leftButton = null;
        rightButton = null;
    }

    public bool IsSpawned() => transform.Find("BoxMoverPanel_Canvas") != null;

    void SpawnIfNeeded() { if (!IsSpawned()) Build(); }

    // ---------------- Build ----------------

    void Build()
    {
        ResolveOrCreateTarget();
        EnsureEventSystem();

        // ---- Canvas (world space) ----
        var canvasGO = new GameObject("BoxMoverPanel_Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRT = (RectTransform)canvasGO.transform;
        canvasRT.sizeDelta = canvasSize;
        canvasRT.position = canvasPosition;
        canvasRT.localScale = Vector3.one * canvasScale;

        // ---- Left button ----
        leftButton = MakeButton(canvasRT, "LeftButton",
            anchoredPos: new Vector2(-160f, 0f),
            size: new Vector2(280f, 160f),
            label: "<  LEFT");
        leftButton.onClick.AddListener(MoveLeft);

        // ---- Right button ----
        rightButton = MakeButton(canvasRT, "RightButton",
            anchoredPos: new Vector2(160f, 0f),
            size: new Vector2(280f, 160f),
            label: "RIGHT  >");
        rightButton.onClick.AddListener(MoveRight);

        // ---- Meta SDK ray interaction stack ----
        AddRayInteraction(canvasRT, canvas);
    }

    Button MakeButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label)
    {
        var btnGO = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var rt = (RectTransform)btnGO.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        btnGO.GetComponent<Image>().color = new Color(0.15f, 0.6f, 1f, 1f);

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
        colors.pressedColor     = new Color(0.6f, 0.8f, 1f, 1f);
        btn.colors = colors;

        // label
        var textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGO.transform.SetParent(btnGO.transform, false);
        var trt = (RectTransform)textGO.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = textGO.GetComponent<Text>();
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 48;
        t.fontStyle = FontStyle.Bold;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return btn;
    }

    void AddRayInteraction(RectTransform canvasRT, Canvas canvasRef)
    {
        var rayGO = new GameObject("ISDK_RayCanvasInteraction", typeof(RectTransform));
        rayGO.transform.SetParent(canvasRT, false);
        var rayRT = (RectTransform)rayGO.transform;
        rayRT.anchorMin = Vector2.zero; rayRT.anchorMax = Vector2.one;
        rayRT.offsetMin = Vector2.zero; rayRT.offsetMax = Vector2.zero;

        var pointableCanvas = rayGO.AddComponent<PointableCanvas>();
        pointableCanvas.InjectAllPointableCanvas(canvasRef);

        var planeSurface = rayGO.AddComponent<PlaneSurface>();
        planeSurface.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward, false);

        var rayInteractable = rayGO.AddComponent<RayInteractable>();
        rayInteractable.InjectAllRayInteractable(planeSurface);

        var layout = rayGO.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
    }

    void EnsureEventSystem()
    {
        var es = FindObjectOfType<EventSystem>();
        if (es == null)
        {
            var esGO = new GameObject("EventSystem", typeof(EventSystem));
            es = esGO.GetComponent<EventSystem>();
        }
        if (es.GetComponent<PointableCanvasModule>() == null)
            es.gameObject.AddComponent<PointableCanvasModule>();
    }

    void ResolveOrCreateTarget()
    {
        if (targetBox != null) return;

        if (!string.IsNullOrEmpty(fallbackBoxName))
        {
            var found = GameObject.Find(fallbackBoxName);
            if (found != null) { targetBox = found.transform; return; }
        }

        // Create a cube primitive as a fallback so the buttons always have something to move.
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = string.IsNullOrEmpty(fallbackBoxName) ? "MovableBox" : fallbackBoxName;
        box.transform.position = autoBoxPosition;
        box.transform.localScale = autoBoxScale;
        targetBox = box.transform;
    }

    // ---------------- Movement handlers ----------------

    public void MoveLeft()  => Move(-stepDistance);
    public void MoveRight() => Move(+stepDistance);

    void Move(float dx)
    {
        if (targetBox == null) ResolveOrCreateTarget();
        if (targetBox == null) return;
        targetBox.position += new Vector3(dx, 0f, 0f);
    }
}
