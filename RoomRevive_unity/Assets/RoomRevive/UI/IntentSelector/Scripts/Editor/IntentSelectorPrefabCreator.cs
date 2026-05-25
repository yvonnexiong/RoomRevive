using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.IntentSelector.EditorTools
{
    /// <summary>
    /// One-shot menu that scaffolds the entire IntentSelector system: folders,
    /// default ScriptableObjects, baked sprite assets, and a prefab that visually
    /// matches the legacy IntentCardSelectorUI look (header pill + gradient cards
    /// + rounded label strip + icon slot, same colors and typography).
    /// Safe to re-run — existing data assets are reused; the prefab is overwritten.
    /// </summary>
    public static class IntentSelectorPrefabCreator
    {
        const string BaseFolder = "Assets/RoomRevive/UI/IntentSelector";
        const string DataFolder = BaseFolder + "/Data";
        const string PrefabFolder = BaseFolder + "/Prefabs";
        const string CardPrefabFolder = PrefabFolder + "/Cards";

        const string ThemePath = DataFolder + "/IntentSelectorTheme.asset";
        const string CatalogPath = DataFolder + "/IntentStateCatalog.asset";
        const string CalmPath = DataFolder + "/IntentState_Calm.asset";
        const string HostPath = DataFolder + "/IntentState_Host.asset";
        const string FastPath = DataFolder + "/IntentState_Fast.asset";
        const string PanelDataPath = DataFolder + "/IntentSelectorPanelData.asset";

        const string PrefabPath = PrefabFolder + "/IntentSelectorUI.prefab";

        // Original layout tokens from the legacy IntentCardSelectorUI.
        const float CardWidth = 260f;
        const float CardGap = 24f;
        const float PanelPadH = 56f;
        const float PanelPadSide = 48f;
        const float ImageAspect = 1.1f;
        const float LabelAreaH = 86f;
        const float CardRadius = 8f;
        const float IconSize = 28f;
        const float LabelPadH = 16f;
        const float HeaderPillH = 22f + 36f + 10f + 22f + 22f; // 112

        // Original design tokens.
        static readonly Color HeaderBg = new Color(0x73 / 255f, 0x7E / 255f, 0x9C / 255f, 195f / 255f);
        static readonly Color LabelBg = new Color(0x73 / 255f, 0x7E / 255f, 0x9C / 255f, 0.55f);
        static readonly Color IconBorder = Hex(0x737E9C);
        static readonly Color CardText = Color.white;
        static readonly Color CardTextDim = new Color(1f, 1f, 1f, 0.85f);

        // ───────────────────────────────────────────────────────────────────
        // Public entry points (three modes — see docs/AI_EDIT_PROTOCOL.md)
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Non-destructive: creates default assets if missing, fixes empty references,
        /// regenerates per-state card prefabs and the main prefab from scratch.
        /// Manual edits to the main prefab or card prefabs ARE NOT preserved.
        /// </summary>
        [MenuItem("Tools/RoomRevive/Intent Selector/Rebuild (Destructive)")]
        public static void RebuildWithConfirmation()
        {
            string existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ? "exists" : "absent";
            bool proceed = EditorUtility.DisplayDialog(
                "Rebuild IntentSelector — Destructive",
                $"This will regenerate IntentSelectorUI.prefab and the card prefabs under Prefabs/Cards/ from code.\n\n" +
                $"Current main prefab: {existing}\n\n" +
                "Manual layout/colour/font edits made in Prefab Mode will be LOST.\n\n" +
                "Tip: run Export Snapshot first if you want a record of the current state.",
                "Rebuild (destructive)", "Cancel");

            if (!proceed) return;
            CreateAll();
        }

        /// <summary>
        /// Sync: ensure default assets exist, fill missing references on existing prefab,
        /// re-run the binder so child-name conventions stay correct. Does not overwrite
        /// existing component values or the prefab hierarchy.
        /// </summary>
        [MenuItem("Tools/RoomRevive/Intent Selector/Sync (Non-destructive)")]
        public static void SyncOnly()
        {
            EnsureFolder(BaseFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(CardPrefabFolder);

            // Bake sprites + default SOs if missing — these are additive only.
            IntentSelectorSpriteFactory.GetOrCreateRoundedMask();
            IntentSelectorSpriteFactory.GetOrCreateCalmGradient();
            IntentSelectorSpriteFactory.GetOrCreateHostGradient();
            IntentSelectorSpriteFactory.GetOrCreateFastGradient();
            LoadOrCreate<IntentSelectorTheme>(ThemePath);
            LoadOrCreate<IntentSelectorPanelData>(PanelDataPath);
            LoadOrCreate<IntentStateData>(CalmPath, s => { s.id = "calm"; s.displayName = "Calm & Unwind"; s.startsSelectedByDefault = true; });
            LoadOrCreate<IntentStateData>(HostPath, s => { s.id = "host"; s.displayName = "Host & Gather"; });
            LoadOrCreate<IntentStateData>(FastPath, s => { s.id = "fast"; s.displayName = "Fast & Focused"; });
            LoadOrCreate<IntentStateCatalog>(CatalogPath, c => { c.states = new System.Collections.Generic.List<IntentStateData>(); });

            // Back-fill stateData on each standalone Card_*.prefab asset under Prefabs/Cards/,
            // matching the prefab filename to an IntentState_*.asset.
            BackfillStateDataOnCardPrefabs();

            // Bind references on the existing main prefab via the binder, in-place.
            GameObject mainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (mainPrefab != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    IntentSelectorPrefabBinder.RebindRoot(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                Debug.Log($"[IntentSelectorPrefabCreator] Sync complete. Existing prefab structure preserved: {PrefabPath}");
            }
            else
            {
                Debug.Log("[IntentSelectorPrefabCreator] No existing main prefab. Run Rebuild to create one.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/RoomRevive/Intent Selector/Create Default Assets And Prefab")]
        public static void CreateAll()
        {
            EnsureFolder(BaseFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(CardPrefabFolder);

            // Sprite assets first — state SOs reference them.
            Sprite roundedMask = IntentSelectorSpriteFactory.GetOrCreateRoundedMask();
            Sprite calmGradient = IntentSelectorSpriteFactory.GetOrCreateCalmGradient();
            Sprite hostGradient = IntentSelectorSpriteFactory.GetOrCreateHostGradient();
            Sprite fastGradient = IntentSelectorSpriteFactory.GetOrCreateFastGradient();

            IntentSelectorTheme theme = LoadOrCreate<IntentSelectorTheme>(ThemePath);
            IntentSelectorPanelData panelData = LoadOrCreate<IntentSelectorPanelData>(PanelDataPath);

            IntentStateData calm = LoadOrCreate<IntentStateData>(CalmPath, s => {
                s.id = "calm";
                s.displayName = "Calm & Unwind";
                s.subtitle = "I want to relax and take my time";
                s.startsSelectedByDefault = true;
            });
            if (calm.cardImage == null) { calm.cardImage = calmGradient; EditorUtility.SetDirty(calm); }

            IntentStateData host = LoadOrCreate<IntentStateData>(HostPath, s => {
                s.id = "host";
                s.displayName = "Host & Gather";
                s.subtitle = "I want to gather, host, and share";
                s.showProductUI = true;
                s.showCabinetUI = true;
                s.showFridges = true;
                s.showCabinets = true;
            });
            if (host.cardImage == null) { host.cardImage = hostGradient; EditorUtility.SetDirty(host); }

            IntentStateData fast = LoadOrCreate<IntentStateData>(FastPath, s => {
                s.id = "fast";
                s.displayName = "Fast & Focused";
                s.subtitle = "I want it simple and efficient";
            });
            if (fast.cardImage == null) { fast.cardImage = fastGradient; EditorUtility.SetDirty(fast); }

            IntentStateCatalog catalog = LoadOrCreate<IntentStateCatalog>(CatalogPath, c => {
                if (c.states == null) c.states = new System.Collections.Generic.List<IntentStateData>();
                if (c.states.Count == 0) { c.states.Add(calm); c.states.Add(host); c.states.Add(fast); }
            });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            // Bake a standalone .prefab per state. Existing prefab assignments are preserved.
            float imageH = CardWidth / ImageAspect;
            float cardH = imageH + LabelAreaH;
            AssignCardPrefab(calm, "Card_Calm", roundedMask, imageH, cardH);
            AssignCardPrefab(host, "Card_Host", roundedMask, imageH, cardH);
            AssignCardPrefab(fast, "Card_Fast", roundedMask, imageH, cardH);
            AssetDatabase.SaveAssets();

            GameObject prefabRoot = BuildPrefabRoot(catalog, theme, roundedMask, panelData);

            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
                Debug.Log($"[IntentSelectorPrefabCreator] Wrote prefab: {PrefabPath}", saved);
            }
            finally
            {
                Object.DestroyImmediate(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/RoomRevive/Intent Selector/Clear Generated Cards (Selected Prefab Instance)")]
        public static void ClearGeneratedCards()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null) return;
            IntentSelectorView view = sel.GetComponentInChildren<IntentSelectorView>(true);
            if (view == null) return;
            Undo.RegisterFullObjectHierarchyUndo(sel, "Clear Generated Cards");
            view.ClearGeneratedCards();
            EditorUtility.SetDirty(view);
        }

        // ── Per-state card prefab baking ────────────────────────────────────

        /// <summary>
        /// Build a standalone card prefab for one state (idempotent), then assign it to
        /// state.cardPrefab. Existing prefab assets at the target path are overwritten so
        /// re-running keeps card visuals in sync with code, but the IntentStateData reference
        /// is only assigned when missing — manual swaps in the inspector survive.
        /// </summary>
        /// <summary>
        /// Walk Prefabs/Cards/Card_*.prefab; for each, if its IntentCardView.stateData is null,
        /// match the filename ("Card_Calm.prefab" → "Calm") to an IntentState_*.asset and assign it.
        /// Never overwrites a non-null reference.
        /// </summary>
        static void BackfillStateDataOnCardPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(CardPrefabFolder)) return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { CardPrefabFolder });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    IntentCardView cardView = contents.GetComponent<IntentCardView>();
                    if (cardView == null) continue;
                    if (cardView.stateData != null) continue;

                    string baseName = System.IO.Path.GetFileNameWithoutExtension(path); // "Card_Calm"
                    string id = baseName.StartsWith("Card_", System.StringComparison.OrdinalIgnoreCase)
                        ? baseName.Substring(5)
                        : baseName;

                    IntentStateData state = ResolveStateByName(id);
                    if (state == null) continue;

                    cardView.stateData = state;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    Debug.Log($"[Sync] Wired {state.name} → {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        static IntentStateData ResolveStateByName(string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName)) return null;
            string[] guids = AssetDatabase.FindAssets("t:IntentStateData", new[] { DataFolder });
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

        static void AssignCardPrefab(IntentStateData state, string prefabName, Sprite roundedMask, float imageH, float cardH)
        {
            if (state == null) return;

            string path = CardPrefabFolder + "/" + prefabName + ".prefab";

            // Build a fresh scene root from the same CardTemplate factory.
            GameObject parent = new GameObject("__CardBakeRoot__", typeof(RectTransform));
            try
            {
                GameObject card = BuildCardTemplate(parent.transform, roundedMask, imageH, cardH);
                card.name = prefabName;

                // Pre-seed visible content so the prefab looks right when opened in Prefab Mode
                // (runtime Bind() will still overwrite from the SO).
                IntentCardView view = card.GetComponent<IntentCardView>();
                if (view != null)
                {
                    // Assign the SO so OnValidate refreshes the card automatically
                    // whenever a designer edits the state asset.
                    view.stateData = state;

                    // Pre-seed visible CONTENT only — text strings and sprite refs —
                    // so the prefab looks correct when opened in Prefab Mode.
                    // Do NOT set colours, sizes, alignment, or enabled flags here:
                    // those belong to the prefab's authored styling.
                    if (view.titleText != null) view.titleText.text = state.displayName;
                    if (view.subtitleText != null) view.subtitleText.text = state.subtitle;
                    if (view.imageArea != null && state.cardImage != null) view.imageArea.sprite = state.cardImage;
                    if (view.iconImage != null && state.icon != null) view.iconImage.sprite = state.icon;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(card, path);

                if (state.cardPrefab == null && saved != null)
                {
                    IntentCardView savedView = saved.GetComponent<IntentCardView>();
                    if (savedView != null)
                    {
                        state.cardPrefab = savedView;
                        EditorUtility.SetDirty(state);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // ── Prefab construction ─────────────────────────────────────────────

        static GameObject BuildPrefabRoot(IntentStateCatalog catalog, IntentSelectorTheme theme, Sprite roundedMask, IntentSelectorPanelData panelData)
        {
            // Match the original panel sizing exactly.
            float imageH = CardWidth / ImageAspect;
            float cardH = imageH + LabelAreaH;
            float panelW = CardWidth * 3f + CardGap * 2f + PanelPadSide * 2f;
            float panelH = PanelPadH + HeaderPillH + 40f + cardH + PanelPadH;

            GameObject root = new GameObject("IntentSelectorUI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));

            RectTransform rootRT = (RectTransform)root.transform;
            rootRT.sizeDelta = new Vector2(panelW + 40f, panelH + 40f);
            rootRT.pivot = Vector2.one * 0.5f;

            MetaWorldSpaceCanvasSetup meta = root.AddComponent<MetaWorldSpaceCanvasSetup>();
            HeadFollowWorldUI head = root.AddComponent<HeadFollowWorldUI>();
            IntentSelectorController controller = root.AddComponent<IntentSelectorController>();
            IntentSelectorView view = root.AddComponent<IntentSelectorView>();
            IntentUnityEventRouter eventRouter = root.AddComponent<IntentUnityEventRouter>();
            IntentAudioRouter audioRouter = root.AddComponent<IntentAudioRouter>();
            IntentVisibilityRouter visibilityRouter = root.AddComponent<IntentVisibilityRouter>();

            controller.catalog = catalog;
            controller.theme = theme;
            controller.view = view;

            // Ray surface child (Meta SDK fills it at Awake via MetaWorldSpaceCanvasSetup).
            GameObject raySurface = NewUIChild(root.transform, "ISDK_RayInteractionSurface");
            StretchFull(raySurface);

            // ViewRoot covers the panel area.
            GameObject viewRoot = NewUIChild(root.transform, IntentSelectorPrefabBinder.ViewRootName);
            RectTransform viewRootRT = (RectTransform)viewRoot.transform;
            viewRootRT.anchorMin = viewRootRT.anchorMax = viewRootRT.pivot = new Vector2(0.5f, 0.5f);
            viewRootRT.sizeDelta = new Vector2(panelW, panelH);
            viewRootRT.anchoredPosition = Vector2.zero;

            // ── Header pill ──────────────────────────────────────────────────
            float pillW = panelW - PanelPadSide * 2f;

            GameObject pill = NewUIChild(viewRoot.transform, IntentSelectorPrefabBinder.HeaderPillName);
            RectTransform pillRT = (RectTransform)pill.transform;
            pillRT.anchorMin = pillRT.anchorMax = new Vector2(0.5f, 1f);
            pillRT.pivot = new Vector2(0.5f, 1f);
            pillRT.sizeDelta = new Vector2(pillW, HeaderPillH);
            pillRT.anchoredPosition = new Vector2(0f, -PanelPadH);

            Image pillBg = pill.AddComponent<Image>();
            pillBg.sprite = roundedMask;
            pillBg.type = Image.Type.Sliced;
            pillBg.color = HeaderBg;
            pillBg.raycastTarget = false;

            float pillInnerW = pillW - 56f * 2f;

            TextMeshProUGUI pillTitle = AddAbsText(pill.transform, "Title", "Choose how you want to live",
                new Vector2(pillInnerW, 40f), new Vector2(0f, -22f), 30f, Color.white);
            pillTitle.fontStyle = FontStyles.Bold;
            pillTitle.alignment = TextAlignmentOptions.Center;
            pillTitle.enableWordWrapping = false;

            TextMeshProUGUI pillSub = AddAbsText(pill.transform, "Subtitle", "Pick the feeling. We'll shape your kitchen around it.",
                new Vector2(pillInnerW, 24f), new Vector2(0f, -22f - 36f - 10f), 14f, new Color(1f, 0.4117647f, 0.7058824f, 1f));
            pillSub.alignment = TextAlignmentOptions.Center;
            pillSub.enableWordWrapping = true;

            // Header view component — drives title/subtitle from a ScriptableObject so designers
            // can edit text and colors in the SO and see it in Prefab Mode immediately.
            IntentSelectorHeaderView headerView = pill.AddComponent<IntentSelectorHeaderView>();
            headerView.panelData = panelData;
            headerView.titleText = pillTitle;
            headerView.subtitleText = pillSub;

            // ── Cards container ──────────────────────────────────────────────
            float totalCardsW = CardWidth * 3f + CardGap * 2f;

            GameObject cardsContainer = NewUIChild(viewRoot.transform, IntentSelectorPrefabBinder.CardsContainerName);
            RectTransform cardsRT = (RectTransform)cardsContainer.transform;
            cardsRT.anchorMin = cardsRT.anchorMax = new Vector2(0.5f, 1f);
            cardsRT.pivot = new Vector2(0.5f, 1f);
            cardsRT.sizeDelta = new Vector2(totalCardsW, cardH);
            cardsRT.anchoredPosition = new Vector2(0f, -PanelPadH - HeaderPillH - 40f);

            HorizontalLayoutGroup hlg = cardsContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = CardGap;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // CardTemplate stays in the prefab as a hidden fallback for states that
            // don't author their own card prefab. Inactive so it never appears at runtime.
            GameObject cardTemplate = BuildCardTemplate(cardsContainer.transform, roundedMask, imageH, cardH);
            cardTemplate.SetActive(false);
            IntentCardView templateCardView = cardTemplate.GetComponent<IntentCardView>();
            view.cardsContainer = cardsRT;
            view.cardTemplate = templateCardView;

            // Bake each state's card as a NESTED PREFAB INSTANCE inside CardsContainer.
            // This makes the cards visible and editable when IntentSelectorUI.prefab is
            // opened in Prefab Mode — each card stays linked to its source prefab asset.
            view.cardViews.Clear();
            view.instantiateCardsFromCatalog = false; // runtime: just bind these existing children
            view.hideTemplateAtRuntime = false;       // template already inactive in prefab

            if (catalog != null && catalog.states != null)
            {
                for (int i = 0; i < catalog.states.Count; i++)
                {
                    IntentStateData state = catalog.states[i];
                    if (state == null || state.cardPrefab == null) continue;

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        state.cardPrefab.gameObject, cardsContainer.transform);

                    if (instance == null) continue;

                    instance.name = $"Card_{i:00}_{(string.IsNullOrEmpty(state.id) ? "state" : state.id)}";
                    instance.SetActive(true);

                    IntentCardView cv = instance.GetComponent<IntentCardView>();
                    if (cv != null) view.cardViews.Add(cv);
                }
            }

            // Wire controller → routers (persistent listeners — all on same prefab).
            UnityEventTools.AddPersistentListener(controller.onStateSelected,
                new UnityEngine.Events.UnityAction<IntentStateData>(eventRouter.RouteSelected));
            UnityEventTools.AddPersistentListener(controller.onStateConfirmed,
                new UnityEngine.Events.UnityAction<IntentStateData>(eventRouter.RouteConfirmed));
            UnityEventTools.AddPersistentListener(controller.onStateSelected,
                new UnityEngine.Events.UnityAction<IntentStateData>(audioRouter.RouteSelected));
            UnityEventTools.AddPersistentListener(controller.onStateSelected,
                new UnityEngine.Events.UnityAction<IntentStateData>(visibilityRouter.RouteSelected));

            // Seed router bindings for the default states.
            if (catalog != null && catalog.states != null)
            {
                eventRouter.bindings.Clear();
                for (int i = 0; i < catalog.states.Count; i++)
                {
                    eventRouter.bindings.Add(new IntentUnityEventRouter.IntentStateUnityEventBinding
                    {
                        state = catalog.states[i]
                    });
                }
            }

            return root;
        }

        static GameObject BuildCardTemplate(Transform parent, Sprite roundedMask, float imageH, float cardH)
        {
            GameObject card = NewUIChild(parent, IntentSelectorPrefabBinder.CardTemplateName);
            RectTransform cardRT = (RectTransform)card.transform;
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 1f);
            cardRT.pivot = new Vector2(0.5f, 1f);
            cardRT.sizeDelta = new Vector2(CardWidth, cardH);

            LayoutElement le = card.AddComponent<LayoutElement>();
            le.minWidth = CardWidth; le.preferredWidth = CardWidth; le.flexibleWidth = 0f;
            le.minHeight = cardH; le.preferredHeight = cardH; le.flexibleHeight = 0f;

            // Hit area on the root (invisible — keeps ray hit stable while Visual scales).
            Image hit = card.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            hit.raycastTarget = true;

            Button button = card.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            IntentCardView cardView = card.AddComponent<IntentCardView>();
            cardView.hitImage = hit;
            cardView.button = button;

            IntentCardPointerProxy proxy = card.AddComponent<IntentCardPointerProxy>();
            proxy.card = cardView;

            // Visual — scales independently of hit area.
            GameObject visual = NewUIChild(card.transform, IntentSelectorPrefabBinder.VisualName);
            RectTransform visualRT = (RectTransform)visual.transform;
            visualRT.anchorMin = visualRT.anchorMax = new Vector2(0.5f, 1f);
            visualRT.pivot = new Vector2(0.5f, 1f);
            visualRT.sizeDelta = new Vector2(CardWidth, cardH);
            visualRT.anchoredPosition = Vector2.zero;
            cardView.visual = visualRT;

            // Mask image (rounded sprite) — clips ImageArea + LabelArea to rounded corners.
            Image maskImage = visual.AddComponent<Image>();
            maskImage.sprite = roundedMask;
            maskImage.type = Image.Type.Sliced;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;

            Mask mask = visual.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // ImageArea — top, holds the card gradient/photo.
            GameObject imageArea = NewUIChild(visual.transform, IntentSelectorPrefabBinder.ImageAreaName);
            RectTransform imageRT = (RectTransform)imageArea.transform;
            imageRT.anchorMin = imageRT.anchorMax = new Vector2(0.5f, 1f);
            imageRT.pivot = new Vector2(0.5f, 1f);
            imageRT.sizeDelta = new Vector2(CardWidth, imageH);
            imageRT.anchoredPosition = Vector2.zero;

            Image imageImg = imageArea.AddComponent<Image>();
            imageImg.color = Color.white;
            imageImg.raycastTarget = false;
            imageImg.type = Image.Type.Simple;
            imageImg.preserveAspect = false;
            cardView.imageArea = imageImg;

            // LabelArea — bottom strip with translucent slate background.
            GameObject labelArea = NewUIChild(visual.transform, "LabelArea");
            RectTransform labelRT = (RectTransform)labelArea.transform;
            labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 1f);
            labelRT.pivot = new Vector2(0.5f, 1f);
            labelRT.sizeDelta = new Vector2(CardWidth, LabelAreaH);
            labelRT.anchoredPosition = new Vector2(0f, -imageH);

            Image labelBg = labelArea.AddComponent<Image>();
            labelBg.color = LabelBg;
            labelBg.raycastTarget = false;

            // Icon circle in label top-left (matches original placement).
            GameObject iconCircle = NewUIChild(labelArea.transform, "IconCircle");
            RectTransform iconCircleRT = (RectTransform)iconCircle.transform;
            iconCircleRT.anchorMin = iconCircleRT.anchorMax = new Vector2(0f, 1f);
            iconCircleRT.pivot = new Vector2(0f, 1f);
            iconCircleRT.sizeDelta = new Vector2(IconSize, IconSize);
            iconCircleRT.anchoredPosition = new Vector2(LabelPadH, -LabelPadH);

            Image iconCircleImg = iconCircle.AddComponent<Image>();
            iconCircleImg.sprite = roundedMask;
            iconCircleImg.type = Image.Type.Sliced;
            iconCircleImg.color = IconBorder;
            iconCircleImg.raycastTarget = false;
            iconCircleImg.enabled = false; // matches original (kept off, shown only if user enables)

            GameObject iconContent = NewUIChild(iconCircle.transform, IntentSelectorPrefabBinder.IconContentName);
            StretchFull(iconContent);
            Image iconImg = iconContent.AddComponent<Image>();
            iconImg.color = new Color(0, 0, 0, 0); // hidden until a state assigns its icon
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            cardView.iconImage = iconImg;

            // Title — 17 Bold white, top-right of icon.
            float titleX = 35f;
            float titleW = CardWidth - titleX - LabelPadH;
            TextMeshProUGUI title = AddAbsText(labelArea.transform, IntentSelectorPrefabBinder.TitleTextName, "Title",
                new Vector2(titleW, IconSize), new Vector2(titleX, -LabelPadH), 17f, CardText);
            title.fontStyle = FontStyles.Bold;
            title.verticalAlignment = VerticalAlignmentOptions.Middle;
            title.overflowMode = TextOverflowModes.Ellipsis;
            cardView.titleText = title;

            // Subtitle — 12, dim white, wraps under the icon row.
            float innerW = CardWidth - LabelPadH * 2f;
            TextMeshProUGUI subtitle = AddAbsText(labelArea.transform, IntentSelectorPrefabBinder.SubtitleTextName, "Subtitle",
                new Vector2(innerW, 26f), new Vector2(LabelPadH + IconSize + 10f - 10f, -LabelPadH - IconSize - 6f), 12f, CardTextDim);
            subtitle.enableWordWrapping = true;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            cardView.subtitleText = subtitle;

            // StateOverlay — full-stretch overlay used for grey-out / hover / press.
            GameObject overlay = NewUIChild(visual.transform, IntentSelectorPrefabBinder.StateOverlayName);
            RectTransform overlayRT = (RectTransform)overlay.transform;
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            Image overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(1f, 1f, 1f, 0f);
            overlayImg.raycastTarget = false;
            cardView.stateOverlay = overlayImg;

            return card;
        }

        // ── Asset helpers ───────────────────────────────────────────────────

        static T LoadOrCreate<T>(string path) where T : ScriptableObject => LoadOrCreate<T>(path, null);

        static T LoadOrCreate<T>(string path, System.Action<T> initIfNew) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            initIfNew?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static GameObject NewUIChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Creates a TMP text positioned with absolute anchored coordinates relative to the parent's
        /// top-center (pivot 0.5, 1) — same convention as the legacy AbsText helper.
        /// </summary>
        static TextMeshProUGUI AddAbsText(Transform parent, string objectName, string text,
            Vector2 size, Vector2 anchoredPosition, float fontSize, Color color)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }
}
