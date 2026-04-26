using UnityEngine;

/// <summary>
/// Attach to a hand GameObject that has an OVRHand component.
/// Shoots a ray from the hand tip, highlights hovered fridge parts,
/// and toggles them open/closed on a pinch.
/// </summary>
public class FridgeInteractor : MonoBehaviour
{
    // ── References ─────────────────────────────────────────────────────────
    [Header("Hand")]
    [Tooltip("The OVRHand on this hand (used for pinch detection).")]
    public OVRHand hand;

    [Tooltip("The transform to shoot the ray from (e.g. index finger tip or wrist).")]
    public Transform rayOrigin;

    [Header("Fridge")]
    public FridgeController fridgeController;

    // ── Ray ────────────────────────────────────────────────────────────────
    [Header("Ray")]
    public float rayDistance = 3f;
    public LayerMask interactableLayer = -1;

    // ── Materials ──────────────────────────────────────────────────────────
    [Header("Materials")]
    [Tooltip("Applied to the hovered fridge part while pointing at it.")]
    public Material hoverMaterial;

    // ── Private state ──────────────────────────────────────────────────────

    // Hover
    private Transform    _hoveredPart;
    private Renderer[]   _hoveredRenderers;
    private Material[][] _originalMaterials;

    // Pinch
    private bool _wasPinching;

    // ── Debug ──────────────────────────────────────────────────────────────
    [Header("Debug")]
    [Tooltip("Draws the ray in the Scene view and logs key events to Console.")]
    public bool debugMode = true;

    // ──────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (hand == null)
            Debug.LogError("[FridgeInteractor] OVRHand is NOT assigned. Pinch detection will not work.", this);
        else
            Debug.Log($"[FridgeInteractor] OVRHand assigned: {hand.name} | IsTracked: {hand.IsTracked}", this);

        if (rayOrigin == null)
            Debug.LogError("[FridgeInteractor] Ray Origin is NOT assigned. Raycasting will not work.", this);
        else
            Debug.Log($"[FridgeInteractor] Ray Origin: {rayOrigin.name}", this);

        if (fridgeController == null)
            Debug.LogError("[FridgeInteractor] FridgeController is NOT assigned.", this);

        if (interactableLayer == 0)
            Debug.LogWarning("[FridgeInteractor] Interactable Layer Mask is set to Nothing — ray will never hit anything.", this);
    }

    void Update()
    {
        HandleRaycast();
        HandlePinch();
    }

    // ── Raycast ────────────────────────────────────────────────────────────

    void HandleRaycast()
    {
        if (rayOrigin == null || fridgeController == null) return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

        // Draw the ray in Scene view while in Play Mode
        if (debugMode)
            Debug.DrawRay(ray.origin, ray.direction * rayDistance, Color.cyan);

        Transform newPart = null;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableLayer))
        {
            if (debugMode)
                Debug.Log($"[FridgeInteractor] Ray hit: '{hit.collider.name}' on layer '{LayerMask.LayerToName(hit.collider.gameObject.layer)}'");

            newPart = GetFridgePart(hit.transform);

            if (newPart == null && debugMode)
                Debug.Log($"[FridgeInteractor] Hit object '{hit.collider.name}' is not a registered fridge part. Check that Left Door / Right Door / Refrigerator references are set on FridgeController.");
        }

        if (newPart != _hoveredPart)
        {
            ClearHighlight();
            _hoveredPart = newPart;

            if (_hoveredPart != null)
            {
                if (debugMode)
                    Debug.Log($"[FridgeInteractor] Now hovering: {_hoveredPart.name}");
                ApplyHighlight(_hoveredPart);
            }
            else if (debugMode)
            {
                Debug.Log("[FridgeInteractor] No longer hovering any fridge part.");
            }
        }
    }

    Transform GetFridgePart(Transform hit)
    {
        if (IsPartOrChild(hit, fridgeController.leftDoor))    return fridgeController.leftDoor;
        if (IsPartOrChild(hit, fridgeController.rightDoor))   return fridgeController.rightDoor;
        if (IsPartOrChild(hit, fridgeController.refrigerator)) return fridgeController.refrigerator;
        return null;
    }

    bool IsPartOrChild(Transform hit, Transform part)
    {
        if (part == null) return false;
        return hit == part || hit.IsChildOf(part);
    }

    // ── Highlight ──────────────────────────────────────────────────────────

    void ApplyHighlight(Transform part)
    {
        if (hoverMaterial == null)
        {
            if (debugMode) Debug.LogWarning("[FridgeInteractor] Hover Material is not assigned — no highlight will show.");
            return;
        }

        _hoveredRenderers  = part.GetComponentsInChildren<Renderer>();
        _originalMaterials = new Material[_hoveredRenderers.Length][];

        for (int i = 0; i < _hoveredRenderers.Length; i++)
        {
            _originalMaterials[i] = _hoveredRenderers[i].sharedMaterials;
            var hoverMats = new Material[_originalMaterials[i].Length];
            for (int j = 0; j < hoverMats.Length; j++)
                hoverMats[j] = hoverMaterial;
            _hoveredRenderers[i].sharedMaterials = hoverMats;
        }
    }

    void ClearHighlight()
    {
        if (_hoveredRenderers == null) return;

        for (int i = 0; i < _hoveredRenderers.Length; i++)
        {
            if (_hoveredRenderers[i] != null && _originalMaterials[i] != null)
                _hoveredRenderers[i].sharedMaterials = _originalMaterials[i];
        }

        _hoveredRenderers  = null;
        _originalMaterials = null;
    }

    // ── Pinch ──────────────────────────────────────────────────────────────

    void HandlePinch()
    {
        if (hand == null)
        {
            if (debugMode) Debug.LogWarning("[FridgeInteractor] Cannot check pinch — OVRHand is null.");
            return;
        }

        if (!hand.IsTracked)
        {
            if (debugMode) Debug.LogWarning($"[FridgeInteractor] Hand '{hand.name}' is not tracked. Make sure hand tracking is enabled and the hand is visible.");
            return;
        }

        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (debugMode && isPinching != _wasPinching)
            Debug.Log($"[FridgeInteractor] Pinch state changed → isPinching: {isPinching} | Hovered part: {(_hoveredPart != null ? _hoveredPart.name : "NONE")}");

        // Rising edge only — pinch down, not pinch up
        if (isPinching && !_wasPinching)
        {
            if (_hoveredPart == null)
            {
                if (debugMode) Debug.LogWarning("[FridgeInteractor] Pinch detected but no fridge part is hovered.");
            }
            else
            {
                TogglePart(_hoveredPart);
            }
        }

        _wasPinching = isPinching;
    }

    void TogglePart(Transform part)
    {
        if (fridgeController == null) return;

        if (part == fridgeController.leftDoor)
            fridgeController.ToggleLeftDoor();
        else if (part == fridgeController.rightDoor)
            fridgeController.ToggleRightDoor();
        else if (part == fridgeController.refrigerator)
            fridgeController.ToggleRefrigerator();
    }
}
