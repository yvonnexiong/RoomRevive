using UnityEngine;

public class FridgeController : MonoBehaviour
{
    // ── References ─────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Left door transform (pivot/hinge, not just the mesh).")]
    public Transform leftDoor;

    [Tooltip("Right door transform (pivot/hinge, not just the mesh).")]
    public Transform rightDoor;

    [Tooltip("Refrigerator / freezer drawer transform.")]
    public Transform refrigerator;

    // ── Animation ──────────────────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("How fast doors and the drawer animate to their target.")]
    public float animationSpeed = 4f;

    // ── Cooldown ───────────────────────────────────────────────────────────
    [Header("Cooldown")]
    [Tooltip("Seconds before the same part can be toggled again.")]
    public float cooldown = 2f;

    // ── Closed / Open positions ────────────────────────────────────────────
    // Left door:  closed = -90  →  open = 0   (Z axis)
    // Right door: closed =  90  →  open = 0   (Z axis)
    // Refrigerator: closed = -0.474  →  open = -0.85  (Y position)
    [Header("Refrigerator Positions")]
    public float refrigeratorClosedY = -0.474f;
    public float refrigeratorOpenY   = -0.85f;

    // ── Current animated values (visible in Inspector for debugging) ────────
    [Header("Current Values (runtime)")]
    [Range(-90f, 0f)]  public float leftDoorAngle  = -90f;   // -90 = closed, 0 = open
    [Range(0f,  90f)]  public float rightDoorAngle =  90f;   //  90 = closed, 0 = open
    [Range(-0.85f, -0.474f)] public float refrigeratorY = -0.474f;

    // ── Interaction ────────────────────────────────────────────────────────
    [Header("Interaction")]
    public bool debugLogs = true;

    // ── Internal state ─────────────────────────────────────────────────────
    private bool _leftDoorOpen;
    private bool _rightDoorOpen;
    private bool _refrigeratorOpen;

    private float _leftCooldown;
    private float _rightCooldown;
    private float _fridgeCooldown;

    private float _targetLeftAngle;
    private float _targetRightAngle;
    private float _targetFridgeY;

    // Preserve X and Y of initial euler so we only control Z
    private Vector3 _leftDoorInitialEuler;
    private Vector3 _rightDoorInitialEuler;
    private bool _initialized;

    // ──────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Initialize();
    }

    void Initialize()
    {
        if (_initialized) return;

        // Cache initial eulers to preserve X and Y when we set Z
        if (leftDoor  != null) _leftDoorInitialEuler  = leftDoor.localEulerAngles;
        if (rightDoor != null) _rightDoorInitialEuler = rightDoor.localEulerAngles;

        // Both doors start closed
        _targetLeftAngle  = -90f;
        _targetRightAngle =  90f;
        _targetFridgeY    = refrigeratorClosedY;

        leftDoorAngle  = -90f;
        rightDoorAngle =  90f;
        refrigeratorY  = refrigeratorClosedY;

        _initialized = true;
    }

    void Update()
    {
        TickCooldowns();
        AnimateValues();
        Apply();
    }

    void TickCooldowns()
    {
        if (_leftCooldown   > 0f) _leftCooldown   -= Time.deltaTime;
        if (_rightCooldown  > 0f) _rightCooldown  -= Time.deltaTime;
        if (_fridgeCooldown > 0f) _fridgeCooldown -= Time.deltaTime;
    }

    void AnimateValues()
    {
        float t = Time.deltaTime * animationSpeed;

        // Lerp FROM current value TOWARD target
        leftDoorAngle  = Mathf.Lerp(leftDoorAngle,  _targetLeftAngle,  t);
        rightDoorAngle = Mathf.Lerp(rightDoorAngle, _targetRightAngle, t);
        refrigeratorY  = Mathf.Lerp(refrigeratorY,  _targetFridgeY,    t);
    }

    // ── Toggle API ─────────────────────────────────────────────────────────

    public bool ToggleLeftDoor()
    {
        if (_leftCooldown > 0f)
        {
            if (debugLogs) Debug.Log($"[FridgeController] Left door on cooldown ({_leftCooldown:F2}s left).");
            return false;
        }

        _leftDoorOpen    = !_leftDoorOpen;
        _targetLeftAngle = _leftDoorOpen ? 0f : -90f;   // open = 0, closed = -90
        _leftCooldown    = cooldown;

        if (debugLogs) Debug.Log($"[FridgeController] Left Door → {(_leftDoorOpen ? "OPEN" : "CLOSED")}");
        return true;
    }

    public bool ToggleRightDoor()
    {
        if (_rightCooldown > 0f)
        {
            if (debugLogs) Debug.Log($"[FridgeController] Right door on cooldown ({_rightCooldown:F2}s left).");
            return false;
        }

        _rightDoorOpen    = !_rightDoorOpen;
        _targetRightAngle = _rightDoorOpen ? 0f : 90f;  // open = 0, closed = 90
        _rightCooldown    = cooldown;

        if (debugLogs) Debug.Log($"[FridgeController] Right Door → {(_rightDoorOpen ? "OPEN" : "CLOSED")}");
        return true;
    }

    public bool ToggleRefrigerator()
    {
        if (_fridgeCooldown > 0f)
        {
            if (debugLogs) Debug.Log($"[FridgeController] Refrigerator on cooldown ({_fridgeCooldown:F2}s left).");
            return false;
        }

        _refrigeratorOpen = !_refrigeratorOpen;
        _targetFridgeY    = _refrigeratorOpen ? refrigeratorOpenY : refrigeratorClosedY;
        _fridgeCooldown   = cooldown;

        if (debugLogs) Debug.Log($"[FridgeController] Refrigerator → {(_refrigeratorOpen ? "OPEN" : "CLOSED")}");
        return true;
    }

    // ── UnityEvent-friendly wrappers (usable with Interactable Event Wrapper)
    public void ToggleLeftDoorFromInteraction()    => ToggleLeftDoor();
    public void ToggleRightDoorFromInteraction()   => ToggleRightDoor();
    public void ToggleRefrigeratorFromInteraction() => ToggleRefrigerator();

    // ── State queries ──────────────────────────────────────────────────────
    public bool IsLeftDoorOpen()          => _leftDoorOpen;
    public bool IsRightDoorOpen()         => _rightDoorOpen;
    public bool IsRefrigeratorOpen()      => _refrigeratorOpen;
    public bool IsLeftDoorOnCooldown()    => _leftCooldown   > 0f;
    public bool IsRightDoorOnCooldown()   => _rightCooldown  > 0f;
    public bool IsRefrigeratorOnCooldown() => _fridgeCooldown > 0f;

    // ── Apply transforms ───────────────────────────────────────────────────

    public void Apply()
    {
        ApplyLeftDoor();
        ApplyRightDoor();
        ApplyRefrigerator();
    }

    public void ApplyLeftDoor()
    {
        if (leftDoor == null) return;
        // Preserve X and Y from initial, SET Z directly (not offset)
        // Closed: Z = -90  |  Open: Z = 0
        Vector3 euler = _leftDoorInitialEuler;
        euler.z = leftDoorAngle;
        leftDoor.localEulerAngles = euler;
    }

    public void ApplyRightDoor()
    {
        if (rightDoor == null) return;
        // Preserve X and Y from initial, SET Z directly (not offset)
        // Closed: Z = 90  |  Open: Z = 0
        Vector3 euler = _rightDoorInitialEuler;
        euler.z = rightDoorAngle;
        rightDoor.localEulerAngles = euler;
    }

    public void ApplyRefrigerator()
    {
        if (refrigerator == null) return;
        Vector3 pos = refrigerator.localPosition;
        pos.y = Mathf.Clamp(refrigeratorY, refrigeratorOpenY, refrigeratorClosedY);
        refrigerator.localPosition = pos;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Initialize();
        Apply();
    }
#endif
}
