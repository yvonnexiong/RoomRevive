using UnityEngine;

namespace RoomRevive.IntentSelector
{
    [CreateAssetMenu(menuName = "RoomRevive/Intent Selector/Selector Theme", fileName = "IntentSelectorTheme")]
    public class IntentSelectorTheme : ScriptableObject
    {
        [Header("Scale")]
        [Range(0.5f, 1.0f)] public float normalScale = 0.93f;
        [Range(0.8f, 1.2f)] public float hoverScale = 1.00f;
        [Range(0.9f, 1.3f)] public float selectedScale = 1.08f;
        public float scaleLerpSpeed = 14f;

        [Header("Grey-out Overlay (non-selected cards when a selection exists)")]
        public Color greyedOutOverlayColor = new Color(0f, 0f, 0f, 1f);
        [Range(0f, 1f)] public float greyedOutOverlayAlpha = 0.55f;
        [Range(0f, 1f)] public float greyedOutHoveredAlpha = 0.30f;
        [Range(0f, 1f)] public float greyedOutPressedAlpha = 0.15f;
        public bool reduceGreyOnHoveredUnselectedCard = true;

        [Header("Pre-selection Overlay (no card selected yet)")]
        public Color preSelectionHoverOverlayColor = new Color(1f, 1f, 1f, 1f);
        [Range(0f, 1f)] public float hoverOverlayAlpha = 0.10f;
        [Range(0f, 1f)] public float pressedOverlayAlpha = 0.18f;
        public float overlayLerpSpeed = 14f;
    }
}
