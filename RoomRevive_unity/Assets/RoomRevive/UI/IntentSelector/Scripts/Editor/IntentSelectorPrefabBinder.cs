using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RoomRevive.IntentSelector.EditorTools
{
    /// <summary>
    /// Inspector helpers that find or add components on the IntentSelectorUI prefab root
    /// and wire serialized references by child-name convention. Safe to run repeatedly:
    /// it never overwrites a manually assigned reference.
    /// </summary>
    public static class IntentSelectorPrefabBinder
    {
        public const string ViewRootName = "ViewRoot";
        public const string HeaderPillName = "HeaderPill";
        public const string CardsContainerName = "CardsContainer";
        public const string CardTemplateName = "CardTemplate";

        public const string VisualName = "Visual";
        public const string ImageAreaName = "ImageArea";
        public const string IconContentName = "IconContent";
        public const string TitleTextName = "TitleText";
        public const string SubtitleTextName = "SubtitleText";
        public const string StateOverlayName = "StateOverlay";
        public const string HitAreaName = "HitArea";

        [MenuItem("Tools/RoomRevive/Intent Selector/Rebind Intent Selector Prefab")]
        public static void RebindMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Rebind Intent Selector",
                    "Select an IntentSelectorUI prefab root (or instance) in the Hierarchy/Project, then run this command again.",
                    "OK");
                return;
            }
            RebindRoot(selected);
        }

        [MenuItem("Tools/RoomRevive/Intent Selector/Validate Intent Selector Prefab")]
        public static void ValidateMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;
            ValidateRoot(selected);
        }

        public static void RebindRoot(GameObject root)
        {
            if (root == null) return;

            Undo.RegisterFullObjectHierarchyUndo(root, "Rebind Intent Selector Prefab");

            EnsureComponent<Canvas>(root);
            EnsureComponent<CanvasScaler>(root);
            EnsureComponent<GraphicRaycaster>(root);
            EnsureComponent<CanvasGroup>(root);

            MetaWorldSpaceCanvasSetup meta = EnsureComponent<MetaWorldSpaceCanvasSetup>(root);
            HeadFollowWorldUI head = EnsureComponent<HeadFollowWorldUI>(root);
            IntentSelectorController controller = EnsureComponent<IntentSelectorController>(root);
            IntentSelectorView view = EnsureComponent<IntentSelectorView>(root);
            IntentUnityEventRouter eventRouter = EnsureComponent<IntentUnityEventRouter>(root);
            IntentAudioRouter audioRouter = EnsureComponent<IntentAudioRouter>(root);
            IntentVisibilityRouter visibilityRouter = EnsureComponent<IntentVisibilityRouter>(root);

            Transform viewRoot = FindOrNull(root.transform, ViewRootName);
            Transform cardsContainer = viewRoot != null ? FindOrNull(viewRoot, CardsContainerName) : FindOrNull(root.transform, CardsContainerName);
            Transform cardTemplate = cardsContainer != null ? FindOrNull(cardsContainer, CardTemplateName) : null;

            if (view.cardsContainer == null && cardsContainer != null)
                view.cardsContainer = cardsContainer as RectTransform;

            if (view.cardTemplate == null && cardTemplate != null)
            {
                IntentCardView cardView = cardTemplate.GetComponent<IntentCardView>();
                if (cardView == null) cardView = cardTemplate.gameObject.AddComponent<IntentCardView>();
                view.cardTemplate = cardView;
            }

            if (cardTemplate != null)
                BindCardTemplate(cardTemplate);

            // Header pill: wire the IntentSelectorHeaderView if a pill exists.
            Transform headerPill = viewRoot != null ? FindOrNull(viewRoot, HeaderPillName) : FindOrNull(root.transform, HeaderPillName);
            if (headerPill != null)
                BindHeaderPill(headerPill);

            // Card instances under CardsContainer: back-fill IntentCardView.stateData by
            // matching each card prefab's name to a sibling IntentState_*.asset under Data/.
            if (cardsContainer != null)
                BindNestedCardInstances(cardsContainer);

            if (controller.view == null) controller.view = view;

            EditorUtility.SetDirty(root);
            if (view != null) EditorUtility.SetDirty(view);
            if (controller != null) EditorUtility.SetDirty(controller);

            Debug.Log($"[IntentSelectorPrefabBinder] Rebound {root.name}.", root);
        }

        public static void BindCardTemplate(Transform cardTemplate)
        {
            if (cardTemplate == null) return;

            IntentCardView card = cardTemplate.GetComponent<IntentCardView>();
            if (card == null) card = cardTemplate.gameObject.AddComponent<IntentCardView>();

            IntentCardPointerProxy proxy = cardTemplate.GetComponent<IntentCardPointerProxy>();
            if (proxy == null) proxy = cardTemplate.gameObject.AddComponent<IntentCardPointerProxy>();
            if (proxy.card == null) proxy.card = card;

            Transform visual = FindOrNull(cardTemplate, VisualName);
            if (card.visual == null && visual != null) card.visual = visual as RectTransform;

            Transform imageArea = visual != null ? FindOrNull(visual, ImageAreaName) : FindOrNull(cardTemplate, ImageAreaName);
            if (card.imageArea == null && imageArea != null) card.imageArea = imageArea.GetComponent<Image>();

            Transform iconContent = visual != null ? FindOrNull(visual, IconContentName) : FindOrNull(cardTemplate, IconContentName);
            if (card.iconImage == null && iconContent != null) card.iconImage = iconContent.GetComponent<Image>();

            Transform titleText = visual != null ? FindOrNull(visual, TitleTextName) : FindOrNull(cardTemplate, TitleTextName);
            if (card.titleText == null && titleText != null) card.titleText = titleText.GetComponent<TextMeshProUGUI>();

            Transform subtitleText = visual != null ? FindOrNull(visual, SubtitleTextName) : FindOrNull(cardTemplate, SubtitleTextName);
            if (card.subtitleText == null && subtitleText != null) card.subtitleText = subtitleText.GetComponent<TextMeshProUGUI>();

            Transform stateOverlay = visual != null ? FindOrNull(visual, StateOverlayName) : FindOrNull(cardTemplate, StateOverlayName);
            if (card.stateOverlay == null && stateOverlay != null) card.stateOverlay = stateOverlay.GetComponent<Image>();

            if (card.hitImage == null) card.hitImage = cardTemplate.GetComponent<Image>();
            if (card.button == null) card.button = cardTemplate.GetComponent<Button>();

            EditorUtility.SetDirty(card);
            EditorUtility.SetDirty(proxy);
        }

        /// <summary>
        /// Ensure HeaderPill has an IntentSelectorHeaderView wired to its Title/Subtitle children
        /// and pointing at IntentSelectorPanelData.asset (if it exists).
        /// </summary>
        public static void BindHeaderPill(Transform headerPill)
        {
            if (headerPill == null) return;

            IntentSelectorHeaderView headerView = headerPill.GetComponent<IntentSelectorHeaderView>();
            if (headerView == null) headerView = headerPill.gameObject.AddComponent<IntentSelectorHeaderView>();

            // Wire TMP refs by child name.
            if (headerView.titleText == null)
            {
                Transform t = FindOrNull(headerPill, "Title");
                if (t != null) headerView.titleText = t.GetComponent<TMPro.TextMeshProUGUI>();
            }
            if (headerView.subtitleText == null)
            {
                Transform s = FindOrNull(headerPill, "Subtitle");
                if (s != null) headerView.subtitleText = s.GetComponent<TMPro.TextMeshProUGUI>();
            }

            // Wire panel data if the canonical asset exists.
            if (headerView.panelData == null)
            {
                IntentSelectorPanelData panelData = AssetDatabase.LoadAssetAtPath<IntentSelectorPanelData>(
                    "Assets/RoomRevive/UI/IntentSelector/Data/IntentSelectorPanelData.asset");
                if (panelData != null) headerView.panelData = panelData;
            }

            EditorUtility.SetDirty(headerView);
        }

        /// <summary>
        /// For each card GameObject under CardsContainer, if its IntentCardView.stateData is null,
        /// match the card prefab's source asset filename ("Card_Calm.prefab") to an
        /// IntentState_*.asset by id substring (e.g. "Calm" → IntentState_Calm.asset).
        /// </summary>
        public static void BindNestedCardInstances(Transform cardsContainer)
        {
            if (cardsContainer == null) return;

            for (int i = 0; i < cardsContainer.childCount; i++)
            {
                GameObject child = cardsContainer.GetChild(i).gameObject;
                if (child.name == CardTemplateName) continue;

                IntentCardView cardView = child.GetComponent<IntentCardView>();
                if (cardView == null) continue;
                if (cardView.stateData != null) continue;

                // Try to resolve the source prefab and pull the state name from its filename.
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(child);
                string sourcePath = sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : null;
                string baseName = string.IsNullOrEmpty(sourcePath) ? child.name : System.IO.Path.GetFileNameWithoutExtension(sourcePath);

                // "Card_Calm" → "Calm". "Card_00_calm" → "calm".
                string id = baseName;
                if (id.StartsWith("Card_", System.StringComparison.OrdinalIgnoreCase))
                    id = id.Substring(5);

                // Strip leading numeric prefix like "00_".
                int firstUnderscore = id.IndexOf('_');
                if (firstUnderscore >= 0 && int.TryParse(id.Substring(0, firstUnderscore), out _))
                    id = id.Substring(firstUnderscore + 1);

                IntentStateData found = FindStateByIdOrName(id);
                if (found != null)
                {
                    cardView.stateData = found;
                    EditorUtility.SetDirty(cardView);
                }
            }
        }

        static IntentStateData FindStateByIdOrName(string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName)) return null;

            string[] guids = AssetDatabase.FindAssets("t:IntentStateData", new[] { "Assets/RoomRevive/UI/IntentSelector/Data" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                IntentStateData state = AssetDatabase.LoadAssetAtPath<IntentStateData>(path);
                if (state == null) continue;

                if (string.Equals(state.id, idOrName, System.StringComparison.OrdinalIgnoreCase)) return state;
                if (state.name.IndexOf(idOrName, System.StringComparison.OrdinalIgnoreCase) >= 0) return state;
                if (!string.IsNullOrEmpty(state.displayName) &&
                    state.displayName.IndexOf(idOrName, System.StringComparison.OrdinalIgnoreCase) >= 0) return state;
            }
            return null;
        }

        public static bool ValidateRoot(GameObject root)
        {
            if (root == null) return false;

            bool ok = true;
            ok &= Require<MetaWorldSpaceCanvasSetup>(root);
            ok &= Require<IntentSelectorController>(root);
            ok &= Require<IntentSelectorView>(root);

            IntentSelectorView view = root.GetComponent<IntentSelectorView>();
            if (view != null)
            {
                if (view.cardsContainer == null) { ok = false; Debug.LogWarning("[Validate] IntentSelectorView.cardsContainer is null.", root); }
                if (view.cardTemplate == null) { ok = false; Debug.LogWarning("[Validate] IntentSelectorView.cardTemplate is null.", root); }
            }

            IntentSelectorController controller = root.GetComponent<IntentSelectorController>();
            if (controller != null)
            {
                if (controller.catalog == null) { ok = false; Debug.LogWarning("[Validate] IntentSelectorController.catalog is null.", root); }
                if (controller.view == null) { ok = false; Debug.LogWarning("[Validate] IntentSelectorController.view is null.", root); }
            }

            if (ok) Debug.Log($"[Validate] {root.name} looks good.", root);
            return ok;
        }

        static bool Require<T>(GameObject root) where T : Component
        {
            if (root.GetComponent<T>() != null) return true;
            Debug.LogWarning($"[Validate] Missing {typeof(T).Name} on {root.name}.", root);
            return false;
        }

        public static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c != null) return c;
            return Undo.AddComponent<T>(go);
        }

        public static Transform FindOrNull(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName)) return null;
            return FindRecursive(parent, childName);
        }

        static Transform FindRecursive(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == childName) return c;
                Transform deeper = FindRecursive(c, childName);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
