using TMPro;
using UnityEngine;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Drives the IntentSelector header pill from an <see cref="IntentSelectorPanelData"/> SO.
    /// Updates in OnValidate so changes to the SO are visible immediately in Prefab Mode.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class IntentSelectorHeaderView : MonoBehaviour
    {
        [Header("Authoring SO")]
        [Tooltip("Edit this asset to change the header title and subtitle. The prefab refreshes automatically.")]
        public IntentSelectorPanelData panelData;

        [Header("Wired references (set by binder/prefab)")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;

        void Reset() { Apply(); }
        void Awake() { Apply(); }
        void OnEnable() { Apply(); }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Defer so we don't mutate TMP_Text from inside OnValidate while Unity is still serializing.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Apply();
            };
        }
#endif

        /// <summary>
        /// Push the SO's strings into the wired TMP children.
        ///
        /// IMPORTANT: data only. Does NOT touch font colour, font size, alignment, or
        /// any other styling. Those are authored on the prefab and must survive SO edits.
        /// </summary>
        public void Apply()
        {
            if (panelData == null) return;

            if (titleText != null) titleText.text = panelData.headerTitle;
            if (subtitleText != null) subtitleText.text = panelData.headerSubtitle;
        }

        /// <summary>
        /// Public refresh entry point used by <see cref="IntentSelectorPanelData"/>.OnValidate
        /// to push designer edits from the SO side into every referencing view.
        /// </summary>
        public void RefreshContent()
        {
            Apply();

#if UNITY_EDITOR
            if (titleText != null) UnityEditor.EditorUtility.SetDirty(titleText);
            if (subtitleText != null) UnityEditor.EditorUtility.SetDirty(subtitleText);
#endif
        }
    }
}
