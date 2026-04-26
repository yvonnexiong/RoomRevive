using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

/// <summary>
/// Manages the logic, selection, and visual states for manually built Intent Cards.
/// </summary>
public class IntentSwapManager : MonoBehaviour
{
    [System.Serializable]
    public class CardReference
    {
        [Tooltip("The data associated with this specific card.")]
        public IntentData data;

        [Tooltip("The ISDK Event Wrapper attached to this card.")]
        public InteractableUnityEventWrapper eventWrapper;

        [Header("Visual Elements to Animate")]
        [Tooltip("The parent object of the card's visual elements (used for scaling).")]
        public Transform visualRoot;
        public Image borderImage;
        public Image labelBackgroundImage;
        public GameObject checkmark;
    }

    // ── References ───────────────────────────────────────────────────────────
    [Header("Manually Assigned Cards")]
    [Tooltip("Add your manually built cards here and assign their UI components.")]
    public List<CardReference> cards = new List<CardReference>();

    // ── Visual Settings ──────────────────────────────────────────────────────
    [Header("Card Scale")]
    [Range(0.80f, 0.99f)] public float deselectedScale = 0.93f;
    [Range(1.00f, 1.25f)] public float selectedScale = 1.08f;

    [Header("Colors")]
    public Color borderColorSelected = new Color(1f, 1f, 1f, 0.85f);
    public Color borderColorUnselected = new Color(0.1f, 0.15f, 0.2f, 1f);

    public Color labelBgSelected = new Color(1f, 1f, 1f, 0.25f);
    public Color labelBgUnselected = new Color(0.15f, 0.2f, 0.28f, 0.55f);

    [Header("Debug")]
    public bool debugLogs = true;

    // ── Runtime State ────────────────────────────────────────────────────────
    private int _selectedIndex = -1;

    void Start()
    {
        // Ensure all cards start in the correct unselected visual state
        RefreshVisuals();
    }

    void Update()
    {
        // Optional: Keep basic keyboard testing for the editor
        if (Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveSelection(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveSelection(1);
            if (Input.GetKeyDown(KeyCode.Return)) ConfirmSelection();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public Inspector Methods (Called from UnityEventWrapper)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from the 'WhenHover' UnityEvent. 
    /// Pass the InteractableUnityEventWrapper component into the parameter slot.
    /// </summary>
    public void OnCardHovered(InteractableUnityEventWrapper wrapper)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].eventWrapper == wrapper)
            {
                if (debugLogs) Debug.Log($"<color=cyan><b>[IntentSwapManager]</b></color> Hover registered on Card {i}: {cards[i].data?.title}");
                break;
            }
        }
    }

    /// <summary>
    /// Call this from the 'WhenSelect' UnityEvent. 
    /// Pass the InteractableUnityEventWrapper component into the parameter slot.
    /// </summary>
    public void OnCardSelected(InteractableUnityEventWrapper wrapper)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].eventWrapper == wrapper)
            {
                ApplySelection(i);
                if (debugLogs) Debug.Log($"<color=yellow><b>[IntentSwapManager]</b></color> Select registered on Card {i}: {cards[i].data?.title}");
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Selection Logic & Visuals
    // ─────────────────────────────────────────────────────────────────────────

    private void MoveSelection(int direction)
    {
        if (cards.Count == 0) return;

        int next = Mathf.Clamp(_selectedIndex + direction, 0, cards.Count - 1);
        ApplySelection(next);
    }

    private void ApplySelection(int index)
    {
        if (index < 0 || index >= cards.Count) return;

        _selectedIndex = index;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            bool isSelected = (i == _selectedIndex);

            if (card.visualRoot != null)
            {
                card.visualRoot.localScale = Vector3.one * (isSelected ? selectedScale : deselectedScale);
            }

            if (card.borderImage != null)
            {
                card.borderImage.color = isSelected ? borderColorSelected : borderColorUnselected;
            }

            if (card.labelBackgroundImage != null)
            {
                card.labelBackgroundImage.color = isSelected ? labelBgSelected : labelBgUnselected;
            }

            if (card.checkmark != null)
            {
                card.checkmark.SetActive(isSelected);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Confirmation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from a standard Unity UI Button (like a "Next" button).
    /// </summary>
    public void ConfirmSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= cards.Count)
        {
            if (debugLogs) Debug.LogWarning("[IntentSwapManager] Cannot confirm. No card is currently selected.");
            return;
        }

        var chosen = cards[_selectedIndex].data;
        string title = chosen != null ? chosen.title : "Unknown";
        Debug.Log($"<color=green><b>[IntentSwapManager]</b></color> ✅ Intent confirmed: \"{title}\"");
    }
}