using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace RoomRevive.IntentSelector
{
    [CreateAssetMenu(menuName = "RoomRevive/Intent Selector/Intent State", fileName = "IntentState")]
    public class IntentStateData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id for this state — used by routers, logs, analytics. Should be unique within a catalog.")]
        public string id;

        [Tooltip("Title shown on the card.")]
        public string displayName;

        [TextArea(1, 3)]
        [Tooltip("Subtitle shown under the title.")]
        public string subtitle;

        [Header("Visual")]
        public Sprite cardImage;
        public Sprite icon;
        public Color accentColor = Color.white;

        [Header("Card Prefab (optional)")]
        [Tooltip("Per-state card prefab. If assigned, IntentSelectorView clones this when building the cards row. If null, the view falls back to its shared cardTemplate.")]
        public IntentCardView cardPrefab;

        [Header("Gaussian Splat")]
        [Tooltip("Splat asset loaded by SplatManager when this intent is selected.")]
        public GaussianSplatAsset splatAsset;

        [Header("Audio")]
        [Tooltip("Atmosphere music played by IntentAudioRouter when this state is selected. Optional.")]
        public AudioClip atmosphereMusic;

        [Header("Selection")]
        [Tooltip("If true, IntentStateCatalog.GetDefaultState() returns this state first.")]
        public bool startsSelectedByDefault;

        [Header("Visibility Flags")]
        public bool showProductUI;
        public bool showCabinetUI;
        public bool showFridges;
        public bool showCabinets;

        [Header("Optional Events")]
        [Tooltip("Asset-level UnityEvent invoked when this state is selected. NOTE: asset events cannot reference scene objects. Use IntentUnityEventRouter on the scene prefab for scene wiring.")]
        public UnityEvent onSelectedAssetEvent;

#if UNITY_EDITOR
        /// <summary>
        /// When the asset is edited in the inspector, push the new content to every
        /// <see cref="IntentCardView"/> in open scenes and the current Prefab Stage that
        /// references this SO. Defers via delayCall so we don't mutate UI graphs from
        /// inside Unity's OnValidate serialization window.
        /// </summary>
        void OnValidate()
        {
            EditorApplication.delayCall += RefreshReferencingViews;
        }

        void RefreshReferencingViews()
        {
            if (this == null) return;

            // Live scene instances (including inactive).
#if UNITY_2022_2_OR_NEWER
            IntentCardView[] sceneCards = UnityEngine.Object.FindObjectsByType<IntentCardView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            IntentCardView[] sceneCards = UnityEngine.Object.FindObjectsOfType<IntentCardView>(true);
#endif
            for (int i = 0; i < sceneCards.Length; i++)
            {
                IntentCardView card = sceneCards[i];
                if (card != null && card.stateData == this) card.RefreshContent();
            }

            // Cards inside the currently-open Prefab Stage.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                IntentCardView[] stageCards = stage.prefabContentsRoot.GetComponentsInChildren<IntentCardView>(true);
                for (int i = 0; i < stageCards.Length; i++)
                {
                    IntentCardView card = stageCards[i];
                    if (card != null && card.stateData == this) card.RefreshContent();
                }
            }

            SceneView.RepaintAll();
        }
#endif
    }
}
