using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Authoring DATA for the IntentSelector header pill. Strings only — colours,
    /// font sizes, alignment, and other styling are authored on the prefab and
    /// must not be overwritten by SO edits.
    ///
    /// When edited in the inspector, this SO finds every IntentSelectorHeaderView
    /// that references it (in open scenes and the current Prefab Stage) and pushes
    /// the new content so the change is visible immediately.
    /// </summary>
    [CreateAssetMenu(menuName = "RoomRevive/Intent Selector/Panel Data", fileName = "IntentSelectorPanelData")]
    public class IntentSelectorPanelData : ScriptableObject
    {
        public string headerTitle = "Choose how you want to live";

        [TextArea(1, 3)]
        public string headerSubtitle = "Pick the feeling. We'll shape your kitchen around it.";

#if UNITY_EDITOR
        void OnValidate()
        {
            EditorApplication.delayCall += RefreshReferencingViews;
        }

        void RefreshReferencingViews()
        {
            if (this == null) return;

#if UNITY_2022_2_OR_NEWER
            IntentSelectorHeaderView[] sceneViews = UnityEngine.Object.FindObjectsByType<IntentSelectorHeaderView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            IntentSelectorHeaderView[] sceneViews = UnityEngine.Object.FindObjectsOfType<IntentSelectorHeaderView>(true);
#endif
            for (int i = 0; i < sceneViews.Length; i++)
            {
                IntentSelectorHeaderView v = sceneViews[i];
                if (v != null && v.panelData == this) v.RefreshContent();
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                IntentSelectorHeaderView[] stageViews = stage.prefabContentsRoot.GetComponentsInChildren<IntentSelectorHeaderView>(true);
                for (int i = 0; i < stageViews.Length; i++)
                {
                    IntentSelectorHeaderView v = stageViews[i];
                    if (v != null && v.panelData == this) v.RefreshContent();
                }
            }

            SceneView.RepaintAll();
        }
#endif
    }
}
