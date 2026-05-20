using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Pure visual card. Holds references to its prefab children and applies
    /// state-driven visuals. Does not perform scene side effects.
    ///
    /// Authoring: assign <see cref="stateData"/> in the prefab. The card's
    /// text/image/icon refresh in OnValidate so designer SO edits are visible
    /// immediately in Prefab Mode. At runtime, IntentSelectorView.Bind() may
    /// override <see cref="stateData"/> with the state for this card's slot.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class IntentCardView : MonoBehaviour
    {
        [Header("Authoring SO (drives card content)")]
        [Tooltip("ScriptableObject that decides the title, subtitle, image, icon. Edit the asset and the prefab refreshes. Runtime Bind() may swap this.")]
        public IntentStateData stateData;

        [Header("Prefab Wiring")]
        public RectTransform visual;
        public Image hitImage;
        public Image imageArea;
        public Image iconImage;
        public Image stateOverlay;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;
        public Button button;

        public int Index { get; private set; } = -1;
        public IntentStateData State => stateData;

        IntentSelectorController _controller;

        float _targetScale = 1f;
        float _currentScale = 1f;
        Color _targetOverlayColor = Color.clear;
        Color _currentOverlayColor = Color.clear;

        void Reset() { ApplyStateContent(); }
        void Awake() { ApplyStateContent(); }
        void OnEnable() { ApplyStateContent(); }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Defer to avoid mutating UI graphs from inside Unity's OnValidate window.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ApplyStateContent();
            };
        }
#endif

        public void Bind(IntentStateData state, int index, IntentSelectorController controller)
        {
            stateData = state;
            Index = index;
            _controller = controller;

            ApplyStateContent();
        }

        /// <summary>
        /// Push content from the SO into the wired children.
        ///
        /// IMPORTANT: this method only updates DATA — text strings and sprite references.
        /// It does NOT change colours, font sizes, enabled state, preserveAspect, RectTransform
        /// values, or any other styling. Styling is authored on the prefab in Prefab Mode and
        /// must survive SO edits. If you need conditional styling at runtime, do it from the
        /// controller or a router, not here.
        ///
        /// Public so <see cref="IntentStateData"/>.OnValidate can call it from the SO side
        /// when designer edits happen on the asset itself.
        /// </summary>
        public void RefreshContent()
        {
            ApplyStateContent();

#if UNITY_EDITOR
            // Mark affected components dirty so the editor repaints and the change persists.
            if (titleText != null) UnityEditor.EditorUtility.SetDirty(titleText);
            if (subtitleText != null) UnityEditor.EditorUtility.SetDirty(subtitleText);
            if (imageArea != null) UnityEditor.EditorUtility.SetDirty(imageArea);
            if (iconImage != null) UnityEditor.EditorUtility.SetDirty(iconImage);
#endif
        }

        void ApplyStateContent()
        {
            if (stateData == null) return;

            if (titleText != null) titleText.text = stateData.displayName;
            if (subtitleText != null) subtitleText.text = stateData.subtitle;

            if (imageArea != null && stateData.cardImage != null)
                imageArea.sprite = stateData.cardImage;

            if (iconImage != null && stateData.icon != null)
                iconImage.sprite = stateData.icon;
        }

public void SetVisualState(bool hasSelection, bool selected, bool hovered, bool pressed, IntentSelectorTheme theme, bool instant)
        {
            if (theme == null) return;

            _targetScale = selected ? theme.selectedScale : hovered ? theme.hoverScale : theme.normalScale;

            _targetOverlayColor = ComputeOverlayColor(hasSelection, selected, hovered, pressed, theme);

            if (instant)
            {
                _currentScale = _targetScale;
                _currentOverlayColor = _targetOverlayColor;
                ApplyVisualImmediate();
            }
        }

        public void Animate(float deltaTime, IntentSelectorTheme theme)
        {
            if (theme == null) return;

            float scaleT = 1f - Mathf.Exp(-theme.scaleLerpSpeed * deltaTime);
            float overlayT = 1f - Mathf.Exp(-theme.overlayLerpSpeed * deltaTime);

            _currentScale = Mathf.Lerp(_currentScale, _targetScale, scaleT);
            _currentOverlayColor = Color.Lerp(_currentOverlayColor, _targetOverlayColor, overlayT);

            ApplyVisualImmediate();
        }

        void ApplyVisualImmediate()
        {
            if (visual != null) visual.localScale = Vector3.one * _currentScale;
            if (stateOverlay != null) stateOverlay.color = _currentOverlayColor;
        }

        static Color ComputeOverlayColor(bool hasSelection, bool selected, bool hovered, bool pressed, IntentSelectorTheme theme)
        {
            if (!hasSelection)
            {
                // No selection: white hover/press overlay on this card if hovered/pressed.
                if (pressed)
                {
                    Color c = theme.preSelectionHoverOverlayColor;
                    c.a = theme.pressedOverlayAlpha;
                    return c;
                }

                if (hovered)
                {
                    Color c = theme.preSelectionHoverOverlayColor;
                    c.a = theme.hoverOverlayAlpha;
                    return c;
                }

                return Transparent(theme.preSelectionHoverOverlayColor);
            }

            // A card is selected.
            if (selected)
                return Transparent(theme.greyedOutOverlayColor);

            // Non-selected card: grey overlay; lighten on hover/press if configured.
            Color grey = theme.greyedOutOverlayColor;
            float alpha = theme.greyedOutOverlayAlpha;

            if (theme.reduceGreyOnHoveredUnselectedCard)
            {
                if (pressed) alpha = theme.greyedOutPressedAlpha;
                else if (hovered) alpha = theme.greyedOutHoveredAlpha;
            }

            grey.a = alpha;
            return grey;
        }

        static Color Transparent(Color c) => new Color(c.r, c.g, c.b, 0f);

        // ── Pointer proxy forwarding ────────────────────────────────────────

        public void NotifyPointerEnter() => _controller?.NotifyHover(Index, true);
        public void NotifyPointerExit() => _controller?.NotifyHover(Index, false);
        public void NotifyPointerDown() => _controller?.NotifyPress(Index, true);
        public void NotifyPointerUp() => _controller?.NotifyPress(Index, false);
        public void NotifyPointerClick() => _controller?.NotifyClick(Index);
    }
}
