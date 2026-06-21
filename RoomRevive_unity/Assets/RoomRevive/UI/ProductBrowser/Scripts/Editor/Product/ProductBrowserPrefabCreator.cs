#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Builds the entire ProductBrowser prefab hierarchy from scratch.
    /// Auto-runs on script reload when prefabs are missing.
    ///
    /// Visual reference: VariantCarouselUI (roomrevive_expanded_v9_bigger_type)
    ///   • Card: CSS radial-gradient baked to a PNG texture (#D4D9E5 → #B5BCD0 → #9CA4BC).
    ///   • All other rounded elements: per-radius 64×64 anti-aliased sprites saved to disk.
    ///   • Image area: inner/1.15 aspect ratio, Mask component for rounded clipping.
    ///   • Text block: VerticalLayoutGroup with brand (14f), name (24f), desc (17f).
    ///   • CTA: 60px tall, 18f font, 14px radius.
    ///   • Close: 72px full circle (36px radius), × glyph in Dark.
    ///   • Arrows: siblings of Card, x = cardWidth/2 + 80, full circle (40px), 1.5× scale.
    ///
    /// Menu: Tools → RoomRevive → Product Browser → …
    /// </summary>
    [InitializeOnLoad]
    public static class ProductBrowserPrefabCreator
    {
        // ── Auto-build on script reload ───────────────────────────────────────
        static ProductBrowserPrefabCreator()
        {
            EditorApplication.delayCall += AutoCreateIfMissing;
        }

        static void AutoCreateIfMissing()
        {
            bool missing = !AssetDatabase.LoadAssetAtPath<GameObject>(BrowserUIPrefabPath)
                        || !AssetDatabase.LoadAssetAtPath<GameObject>(DiscoverPrefabPath)
                        || !AssetDatabase.LoadAssetAtPath<GameObject>(SwapPrefabPath);
            if (missing)
            {
                Debug.Log("[ProductBrowserPrefabCreator] Prefabs missing — auto-building now.");
                CreateAll();
            }
        }

        // ── Paths ─────────────────────────────────────────────────────────────
        const string BaseFolder    = "Assets/RoomRevive/UI/ProductBrowser";
        const string DataFolder    = BaseFolder + "/Data/Product";
        const string PrefabFolder  = BaseFolder + "/Prefabs/Product";
        const string SpritesFolder = BaseFolder + "/GeneratedSprites/Product";

        const string BrowserUIPrefabPath = PrefabFolder + "/ProductBrowserUI.prefab";
        const string DiscoverPrefabPath  = PrefabFolder + "/ProductDiscoverPanel.prefab";
        const string SwapPrefabPath      = PrefabFolder + "/ProductSwapPanel.prefab";

        const string FridgeCategoryPath  = DataFolder + "/Category_Fridges.asset";
        const string CabinetCategoryPath = DataFolder + "/Category_Cabinets.asset";
        const string LightsCategoryPath  = DataFolder + "/Category_Lights.asset";

        // ── Design tokens — matches VariantCarouselUI exactly ────────────────
        static readonly Color Dark        = Hex(0x3A4055);
        static readonly Color Muted       = Hex(0x6B7388);
        static readonly Color CtaText     = Hex(0xE6E9F0);
        static readonly Color ImageBg     = new Color(0.227f, 0.251f, 0.333f, 0.18f);
        static readonly Color DotOff      = new Color(0.227f, 0.251f, 0.333f, 0.25f);

        // ── Swap panel layout ─────────────────────────────────────────────────
        const float CardW        = 540f;
        const float CardPad      = 28f;
        const float ThumbSize    = 68f;    // small product image, pinned top-right (kept small so text below it clears)
        const float ArrowOffset  = 80f;
        const float ArrowSize    = 92f;    // a little bigger
        const float ArrowScale   = 1.5f;
        const float CtaH         = 60f;
        const float DotSize      = 8f;
        const float DotSpacing   = 8f;
        const float DotsRowH     = 38f;
        const int   MaxDots      = 5;    // cap; more products → sliding window + chevrons
        // TextBlock VLG: brand-row + name + subtitle + headline + desc + chips + divider
        const float TextBlockH   = 275f;
        const float PriceRowH    = 40f;

        // Font sizes
        const float BrandFont    = 14f;
        const float NameFont     = 28f;
        const float SubtitleFont = 14f;
        const float HeadlineFont = 22f;
        const float DescFont     = 16f;
        const float ChipFont     = 13f;
        const float FromFont     = 13f;
        const float PriceFont    = 26f;
        const float CtaFont      = 18f;

        // MinH per text row (used with LayoutElement inside VLG)
        const float BrandMinH      = 22f;
        const float NameMinH       = 40f;
        const float SubtitleMinH   = 24f;
        const float HeadlineMinH   = 56f;
        const float DescMinH       = 46f;
        const float ChipsRowH      = 34f;
        const float ChipH          = 30f;
        const float DividerH       = 2f;
        const float TextRowSpacing = 8f;

        // Corner radii
        const float RadiusCard   = 30f;
        const float RadiusImage  = 18f;
        const float RadiusCta    = 14f;   // also used for spec-chip pills
        const float RadiusDot    = 4f;
        // Close + brand dot reuse the full-circle "r40" sprite (ArrowSize * 0.5f = 40f).

        // ── Card aesthetics (pic-2 match) ─────────────────────────────────────
        static readonly Color CardColor   = new Color(0.710f, 0.737f, 0.816f, 0.985f); // lavender, mostly opaque (pic-2 midtone)
        static readonly Color ShadowColor = new Color(0.08f, 0.09f, 0.14f, 0.22f); // soft drop shadow
        const float ShadowMargin = 110f;  // how far the soft shadow falls off past the card
        const float ShadowDrop   = 14f;   // downward offset so it reads as a cast shadow
        const float RowSpacing   = 10f;   // vertical gap between card content rows
        static readonly Color ArrowBgColor = new Color(Dark.r, Dark.g, Dark.b, 0.5f); // glassy translucent nav circle

        // ── Discover panel layout — compact, no image ─────────────────────────
        const float DiscoverW          = 480f;
        const float DiscoverPad        = 28f;
        const float DiscoverHeadlineH  = 20f;
        const float DiscoverBodyH      = 56f;
        const float DiscoverCtaH       = 72f;
        const float DiscoverCtaFont    = 20f;
        const float RadiusCtaDiscover  = 24f;  // big rounded but not pill

        // ── Computed (static readonly, not const, because they use division) ──
        static readonly float SwapInner = CardW - CardPad * 2f;           // 484
        // The product image is now a small top-right thumbnail (out of the vertical flow),
        // so the card height is: pad + text block + gap + price + gap + cta + gap + dots + pad.
        static readonly float SwapCardH =
            CardPad + TextBlockH + 14f + PriceRowH + 14f + CtaH + 16f + DotsRowH + CardPad;
        static readonly float SwapRootW = CardW + (ArrowOffset + ArrowSize) * 2f; // 860

        static readonly float DiscoverInner  = DiscoverW - DiscoverPad * 2f;         // 424
        // cardH = pad + headline + gap + body + gap + cta + pad
        static readonly float DiscoverCardH  =
            DiscoverPad + DiscoverHeadlineH + 12f + DiscoverBodyH + 20f + DiscoverCtaH + DiscoverPad;

        // ── Menu items ────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Product Browser/Create Default Assets And Prefabs")]
        public static void CreateAll()
        {
            EnsureFolders();

            ProductCategoryData fridges  = LoadOrCreateCategory(FridgeCategoryPath,  "fridges",  "Fridges",  ProductSwapType.FridgeGameObject, new Color(0.96f, 0.65f, 0.14f));
            ProductCategoryData cabinets = LoadOrCreateCategory(CabinetCategoryPath, "cabinets", "Cabinets", ProductSwapType.CabinetSplat,     new Color(0.55f, 0.78f, 0.56f));
            ProductCategoryData lights   = LoadOrCreateCategory(LightsCategoryPath,  "lights",   "Lights",   ProductSwapType.LightingOnly,      new Color(0.90f, 0.75f, 0.40f));
            AssetDatabase.SaveAssets();

            BuildDiscoverPrefab();
            BuildSwapPrefab();
            BuildBrowserUIPrefab(fridges, cabinets, lights);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(BrowserUIPrefabPath);
            if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }
            Debug.Log("[ProductBrowserPrefabCreator] Done — all prefabs created.");
        }

        [MenuItem("Tools/RoomRevive/Product Browser/Rebuild (Destructive)")]
        public static void RebuildWithConfirmation()
        {
            if (EditorUtility.DisplayDialog(
                "Rebuild Product Browser — Destructive",
                "Regenerates all three prefabs from code. Manual edits WILL be lost.\n\n" +
                "Run Export Snapshot first to save current state.",
                "Rebuild", "Cancel"))
                CreateAll();
        }

        [MenuItem("Tools/RoomRevive/Product Browser/Sync (Non-Destructive)")]
        public static void SyncOnly()
        {
            EnsureFolders();
            LoadOrCreateCategory(FridgeCategoryPath,  "fridges",  "Fridges",  ProductSwapType.FridgeGameObject, new Color(0.96f, 0.65f, 0.14f));
            LoadOrCreateCategory(CabinetCategoryPath, "cabinets", "Cabinets", ProductSwapType.CabinetSplat,     new Color(0.55f, 0.78f, 0.56f));
            LoadOrCreateCategory(LightsCategoryPath,  "lights",   "Lights",   ProductSwapType.LightingOnly,      new Color(0.90f, 0.75f, 0.40f));

            GameObject main = AssetDatabase.LoadAssetAtPath<GameObject>(BrowserUIPrefabPath);
            if (main != null)
            {
                GameObject c = PrefabUtility.LoadPrefabContents(BrowserUIPrefabPath);
                try   { ProductBrowserPrefabBinder.BindContentsInternal(c); PrefabUtility.SaveAsPrefabAsset(c, BrowserUIPrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(c); }
                Debug.Log("[ProductBrowserPrefabCreator] Sync complete.");
            }
            else { Debug.Log("[ProductBrowserPrefabCreator] No prefab found — run Create All."); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── Scene-safe swap-panel rebuild ─────────────────────────────────────
        //   Regenerates ONLY ProductSwapPanel.prefab (the layout), then re-wires the
        //   prev/next/close buttons inside ProductBrowserUI.prefab non-destructively.
        //   Unlike "Rebuild (Destructive)" this preserves ProductBrowserUI's root
        //   fileIDs, so scene instances (FridgeBrowserUI / CabinetBrowserUI) keep their
        //   assigned categories and objectVariants[] arrays.

        [MenuItem("Tools/RoomRevive/Product Browser/Rebuild Swap Panel (Safe)")]
        public static void RebuildSwapPanelOnly()
        {
            EnsureFolders();
            BuildSwapPrefab();

            GameObject browser = AssetDatabase.LoadAssetAtPath<GameObject>(BrowserUIPrefabPath);
            if (browser == null)
            {
                Debug.LogWarning("[ProductBrowserPrefabCreator] Swap panel rebuilt, but ProductBrowserUI.prefab " +
                                 "was not found — run 'Create Default Assets And Prefabs' to generate it.");
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(BrowserUIPrefabPath);
            try
            {
                RewireSwapButtons(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, BrowserUIPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProductBrowserPrefabCreator] Swap panel rebuilt; ProductBrowserUI re-wired (scene instances preserved).");
        }

        /// <summary>
        /// Re-establishes the swap-panel button wiring on a loaded ProductBrowserUI contents root.
        /// Clears before adding so re-runs never produce duplicate (double-fire) listeners.
        /// </summary>
        static void RewireSwapButtons(GameObject browserRoot)
        {
            ProductBrowserController controller = browserRoot.GetComponent<ProductBrowserController>();
            ProductBrowserView       view       = browserRoot.GetComponent<ProductBrowserView>();
            ProductSwapView          sv         = browserRoot.GetComponentInChildren<ProductSwapView>(true);

            if (controller == null || sv == null)
            {
                Debug.LogWarning("[ProductBrowserPrefabCreator] Re-wire skipped — controller or swap view missing.");
                return;
            }

            if (view != null && view.swapPanel == null) view.swapPanel = sv;

            if (sv.prevButton != null)
            {
                ClearPersistentListeners(sv.prevButton.onClick);
                UnityEventTools.AddPersistentListener(sv.prevButton.onClick,
                    new UnityEngine.Events.UnityAction(controller.SelectPrevious));
            }
            if (sv.nextButton != null)
            {
                ClearPersistentListeners(sv.nextButton.onClick);
                UnityEventTools.AddPersistentListener(sv.nextButton.onClick,
                    new UnityEngine.Events.UnityAction(controller.SelectNext));
            }

            Button closeBtn = sv.transform.Find("Card/CloseButton")?.GetComponent<Button>();
            if (closeBtn != null)
            {
                ClearPersistentListeners(closeBtn.onClick);
                UnityEventTools.AddPersistentListener(closeBtn.onClick,
                    new UnityEngine.Events.UnityAction(controller.Close));
            }

            FavoriteButton favBtn = sv.transform.Find("Card/SelectButton")?.GetComponent<FavoriteButton>();
            if (favBtn != null) controller.favoriteButton = favBtn;

            EditorUtility.SetDirty(browserRoot);
        }

        static void ClearPersistentListeners(UnityEngine.Events.UnityEventBase evt)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(evt, i);
        }

        // ── Discover panel ────────────────────────────────────────────────────
        //   Compact teaser — no image, category headline + subline + big CTA.
        //   All text comes from ProductCategoryData (one asset per category).

        static void BuildDiscoverPrefab()
        {
            float inner  = DiscoverInner;  // 424
            float cardH  = DiscoverCardH;  // ~236

            // Force-rebake the gradient sprite so proportions match the new compact card.
            string discoverSpritePath = SpritesFolder + "/CardBackground_Discover.png";
            AssetDatabase.DeleteAsset(discoverSpritePath);

            Sprite cardSprite = GetOrBakeGradientSprite((int)DiscoverW, (int)cardH, "Discover");
            Sprite rCta       = GetOrBakeRoundedSprite(RadiusCtaDiscover, "r24");

            GameObject root = NewRoot("ProductDiscoverPanel", DiscoverW, cardH);
            ProductDiscoverView view = root.AddComponent<ProductDiscoverView>();

            // Card background
            Image cardImg = root.AddComponent<Image>();
            cardImg.color = Color.white; cardImg.sprite = cardSprite;
            cardImg.type  = Image.Type.Simple; cardImg.preserveAspect = false;
            cardImg.raycastTarget = true;

            float y = -DiscoverPad;

            // ── Headline ──────────────────────────────────────────────────────
            // Small, muted, letter-spaced — e.g. "Found a fridge recommendation for you"
            TextMeshProUGUI headlineTmp = MakeText(root.transform, "HeadlineLabel",
                "Found a recommendation for you",
                12f, Muted,
                new Vector2(inner, DiscoverHeadlineH),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y));
            headlineTmp.fontStyle          = FontStyles.Bold;
            headlineTmp.characterSpacing   = 8f;
            headlineTmp.textWrappingMode   = TextWrappingModes.Normal;
            headlineTmp.alignment          = TextAlignmentOptions.Center;
            headlineTmp.raycastTarget      = false;
            view.headlineLabel             = headlineTmp;

            y -= DiscoverHeadlineH + 12f;

            // ── Body ──────────────────────────────────────────────────────────
            // Larger, dark, centered — e.g. "Clean lines that let your kitchen breathe."
            TextMeshProUGUI bodyTmp = MakeText(root.transform, "BodyLabel",
                "Clean lines that let your kitchen breathe.",
                18f, Dark,
                new Vector2(inner, DiscoverBodyH),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y));
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.alignment        = TextAlignmentOptions.Center;
            bodyTmp.raycastTarget    = false;
            view.bodyLabel           = bodyTmp;

            // ── Discover CTA — anchored to bottom ────────────────────────────
            GameObject cta = Child("DiscoverButton", root.transform);
            RectTransform ctaRT = cta.GetComponent<RectTransform>();
            ctaRT.anchorMin = new Vector2(0.5f, 0f); ctaRT.anchorMax = new Vector2(0.5f, 0f);
            ctaRT.pivot     = new Vector2(0.5f, 0f);
            ctaRT.sizeDelta = new Vector2(inner, DiscoverCtaH);
            ctaRT.anchoredPosition = new Vector2(0f, DiscoverPad);
            Image ctaBg = cta.AddComponent<Image>();
            ctaBg.color = Dark; ctaBg.sprite = rCta; ctaBg.type = Image.Type.Sliced;
            Button ctaBtn = cta.AddComponent<Button>(); ctaBtn.targetGraphic = ctaBg;
            ctaBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            TextMeshProUGUI ctaLbl = MakeText(cta.transform, "Label", "Discover", DiscoverCtaFont, CtaText,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            StretchFull(ctaLbl.gameObject);
            ctaLbl.characterSpacing = 4f; ctaLbl.alignment = TextAlignmentOptions.Center;
            ctaLbl.raycastTarget = false;

            SavePrefab(root, DiscoverPrefabPath);
        }

        // ── Swap panel ────────────────────────────────────────────────────────
        //   Full expanded product card — exact VariantCarouselUI layout.

        static void BuildSwapPrefab()
        {
            float cardH  = SwapCardH;   // nominal height for root + shadow sizing (card itself is content-sized)

            Sprite r14        = GetOrBakeRoundedSprite(RadiusCta,   "r14");
            Sprite r4         = GetOrBakeRoundedSprite(RadiusDot,    "r4");
            Sprite r40        = GetOrBakeRoundedSprite(ArrowSize * 0.5f, "r40"); // full circle (close + brand dot)
            Sprite shadow     = GetOrBakeSoftShadowSprite("card_soft", 256, 60f, 60f);
            Sprite chevron    = GetOrBakeChevronSprite("v1", 64, 5.5f);

            // Root wider than card to give arrows room
            GameObject root = NewRoot("ProductSwapPanel", SwapRootW, cardH);
            ProductSwapView view = root.AddComponent<ProductSwapView>();

            // ── Soft drop shadow — sibling behind the card so it floats above the room ──
            GameObject shadowGO = Child("CardShadow", root.transform);
            RectTransform shRT = shadowGO.GetComponent<RectTransform>();
            shRT.anchorMin = shRT.anchorMax = shRT.pivot = new Vector2(0.5f, 0.5f);
            shRT.sizeDelta = new Vector2(CardW + ShadowMargin, cardH + ShadowMargin);
            shRT.anchoredPosition = new Vector2(0f, -ShadowDrop);
            Image shImg = shadowGO.AddComponent<Image>();
            shImg.sprite = shadow; shImg.type = Image.Type.Simple;   // stretched soft blob = diffuse glow, no hard frame
            shImg.color = ShadowColor; shImg.raycastTarget = false;

            // ── Card — content-sized (VLG + ContentSizeFitter) so spacing stays tight no matter which
            //    rows are present; 9-sliced rounded sprite gives crisp corners at any height. ──
            GameObject card = Child("Card", root.transform);
            RectTransform cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(CardW, cardH);   // width fixed; height driven by ContentSizeFitter
            cardRT.anchoredPosition = Vector2.zero;

            // Flat lavender, rounded via 9-sliced sprite (reliable corners; no baked-gradient null issues).
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = CardColor; cardImg.sprite = r40; cardImg.type = Image.Type.Sliced;
            cardImg.preserveAspect = false; cardImg.raycastTarget = true;

            VerticalLayoutGroup cardVlg = card.AddComponent<VerticalLayoutGroup>();
            cardVlg.padding = new RectOffset((int)CardPad, (int)CardPad, (int)CardPad, (int)CardPad);
            cardVlg.spacing = RowSpacing;
            cardVlg.childAlignment = TextAnchor.UpperLeft;
            cardVlg.childControlWidth = true;  cardVlg.childForceExpandWidth  = true;
            cardVlg.childControlHeight = true; cardVlg.childForceExpandHeight = false;

            ContentSizeFitter cardCsf = card.AddComponent<ContentSizeFitter>();
            cardCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // (No product image — removed per design; the card is text-only.)
            // (No close button — the panel is dismissed via gaze-exit / navigation.)
            // Content rows below are children of the card's VerticalLayoutGroup, in display order.

            // Brand row — accent dot + brand label
            GameObject brandRow = Child("BrandRow", card.transform);
            HorizontalLayoutGroup brandHlg = brandRow.AddComponent<HorizontalLayoutGroup>();
            brandHlg.spacing = 8f; brandHlg.childAlignment = TextAnchor.MiddleLeft;
            brandHlg.childControlWidth = true;  brandHlg.childForceExpandWidth  = false;
            brandHlg.childControlHeight = true; brandHlg.childForceExpandHeight = false;
            AddMinH(brandRow, BrandMinH);

            GameObject dotGO = Child("BrandDot", brandRow.transform);
            dotGO.GetComponent<RectTransform>().sizeDelta = new Vector2(12f, 12f);
            LayoutElement dotLe = dotGO.AddComponent<LayoutElement>();
            dotLe.minWidth = dotLe.preferredWidth = 12f; dotLe.minHeight = dotLe.preferredHeight = 12f;
            Image dotImg2 = dotGO.AddComponent<Image>();
            dotImg2.sprite = r40; dotImg2.type = Image.Type.Simple; dotImg2.raycastTarget = false;
            dotImg2.color = new Color(0.96f, 0.65f, 0.14f, 1f); // default accent; view tints per category
            view.brandDot = dotImg2;

            TextMeshProUGUI brandTmp = MakeText(brandRow.transform, "BrandLabel", "BRAND", BrandFont, Muted,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            brandTmp.fontStyle = FontStyles.Bold; brandTmp.characterSpacing = 12f;
            brandTmp.alignment = TextAlignmentOptions.Left; brandTmp.raycastTarget = false;
            view.brandLabel = brandTmp;

            // Product name — large, light/elegant weight (matches the old card), Dark
            TextMeshProUGUI nameTmp = MakeText(card.transform, "ProductNameLabel", "Product Name", NameFont, Dark,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            nameTmp.fontStyle = FontStyles.Normal;
            nameTmp.textWrappingMode = TextWrappingModes.Normal;
            nameTmp.alignment = TextAlignmentOptions.Left; nameTmp.raycastTarget = false;
            AddMinH(nameTmp.gameObject, NameMinH);
            view.productNameLabel = nameTmp;

            // Subtitle — small, Muted
            TextMeshProUGUI subTmp = MakeText(card.transform, "SubtitleLabel", "Subtitle line", SubtitleFont, Muted,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            subTmp.textWrappingMode = TextWrappingModes.Normal;
            subTmp.alignment = TextAlignmentOptions.Left; subTmp.raycastTarget = false;
            AddMinH(subTmp.gameObject, SubtitleMinH);
            view.subtitleLabel = subTmp;

            // Headline (feature line) — larger, Dark
            TextMeshProUGUI headTmp = MakeText(card.transform, "HeadlineLabel", "Headline feature line", HeadlineFont, Dark,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            headTmp.textWrappingMode = TextWrappingModes.Normal;
            headTmp.alignment = TextAlignmentOptions.Left; headTmp.raycastTarget = false;
            AddMinH(headTmp.gameObject, HeadlineMinH);
            view.headlineLabel = headTmp;

            // Description — Muted body
            TextMeshProUGUI descTmp = MakeText(card.transform, "DescriptionLabel", "Short product description.", DescFont, Muted,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            descTmp.textWrappingMode = TextWrappingModes.Normal;
            descTmp.alignment = TextAlignmentOptions.Left; descTmp.raycastTarget = false;
            AddMinH(descTmp.gameObject, DescMinH);
            view.shortDescriptionLabel = descTmp;

            // Spec chips — horizontal row of pills, populated from ProductData.specs
            GameObject chipsGO = Child("SpecChips", card.transform);
            HorizontalLayoutGroup chipsHlg = chipsGO.AddComponent<HorizontalLayoutGroup>();
            chipsHlg.spacing = 8f; chipsHlg.childAlignment = TextAnchor.MiddleLeft;
            chipsHlg.childControlWidth = true;  chipsHlg.childForceExpandWidth  = false;
            chipsHlg.childControlHeight = true; chipsHlg.childForceExpandHeight = false;
            AddMinH(chipsGO, ChipsRowH);
            view.specChipsContainer = chipsGO.transform;

            for (int i = 0; i < 4; i++)
            {
                GameObject chip = Child($"Chip{i}", chipsGO.transform);
                Image chipBg = chip.AddComponent<Image>();
                chipBg.color = new Color(0.227f, 0.251f, 0.333f, 0.10f);
                chipBg.sprite = r14; chipBg.type = Image.Type.Sliced; chipBg.raycastTarget = false;
                HorizontalLayoutGroup chipHlg = chip.AddComponent<HorizontalLayoutGroup>();
                chipHlg.padding = new RectOffset(14, 14, 4, 4); chipHlg.childAlignment = TextAnchor.MiddleCenter;
                chipHlg.childControlWidth = true;  chipHlg.childForceExpandWidth  = false;
                chipHlg.childControlHeight = true; chipHlg.childForceExpandHeight = false;
                ContentSizeFitter csf = chip.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
                LayoutElement chipLe = chip.AddComponent<LayoutElement>();
                chipLe.minHeight = chipLe.preferredHeight = ChipH;
                TextMeshProUGUI chipLbl = MakeText(chip.transform, "Label", $"Spec {i + 1}", ChipFont, Dark,
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                chipLbl.alignment = TextAlignmentOptions.Center; chipLbl.raycastTarget = false;
                if (i > 0) chip.SetActive(false); // placeholders; ProductSwapView enables per spec count
            }

            // Divider — thin line under the content
            GameObject divGO = Child("Divider", card.transform);
            Image divImg = divGO.AddComponent<Image>();
            divImg.color = new Color(Dark.r, Dark.g, Dark.b, 0.15f); divImg.raycastTarget = false;
            AddMinH(divGO, DividerH);

            // ── Price row — "From" (left) + value (right) ──
            GameObject priceRow = Child("PriceRow", card.transform);
            Image priceRowBg = priceRow.AddComponent<Image>(); priceRowBg.color = Color.clear; priceRowBg.raycastTarget = false;
            AddMinH(priceRow, PriceRowH);
            view.priceRow = priceRow;

            TextMeshProUGUI fromTmp = MakeText(priceRow.transform, "FromLabel", "From", FromFont, Muted,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            RectTransform fromRT = fromTmp.rectTransform;
            fromRT.anchorMin = new Vector2(0f, 0f); fromRT.anchorMax = new Vector2(0.5f, 1f);
            fromRT.offsetMin = new Vector2(4f, 0f); fromRT.offsetMax = Vector2.zero;
            fromTmp.alignment = TextAlignmentOptions.Left; fromTmp.raycastTarget = false;
            view.fromLabel = fromTmp;

            TextMeshProUGUI priceTmp = MakeText(priceRow.transform, "PriceLabel", "$0", PriceFont, Dark,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            RectTransform priceRT = priceTmp.rectTransform;
            priceRT.anchorMin = new Vector2(0.4f, 0f); priceRT.anchorMax = new Vector2(1f, 1f);
            priceRT.offsetMin = Vector2.zero; priceRT.offsetMax = new Vector2(-4f, 0f);
            priceTmp.fontStyle = FontStyles.Bold; priceTmp.alignment = TextAlignmentOptions.Right; priceTmp.raycastTarget = false;
            view.priceLabel = priceTmp;

            // ── Select CTA (favorite) ──
            GameObject ctaGO = Child("SelectButton", card.transform);
            AddMinH(ctaGO, CtaH);
            Image ctaBg = ctaGO.AddComponent<Image>();
            ctaBg.color = Dark; ctaBg.sprite = r14; ctaBg.type = Image.Type.Sliced;
            Button ctaBtn = ctaGO.AddComponent<Button>(); ctaBtn.targetGraphic = ctaBg;
            ctaBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            TextMeshProUGUI ctaLbl = MakeText(ctaGO.transform, "Label", "Add to favorites", CtaFont, CtaText,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            StretchFull(ctaLbl.gameObject);
            ctaLbl.characterSpacing = 2f; ctaLbl.alignment = TextAlignmentOptions.Center;
            ctaLbl.raycastTarget = false;
            // Favorite toggle lives on the SelectButton itself, not on the view.
            FavoriteButton fav = ctaGO.AddComponent<FavoriteButton>();
            fav.button = ctaBtn;
            fav.label  = ctaLbl;
            fav.notFavoritedColor = Dark;
            fav.favoritedColor    = new Color(0.22f, 0.55f, 0.38f, 1f);

            // ── Dots row — [‹ chevron] [up to 10 dots] [› chevron] ──
            // The chevrons sit either side of the dot pool and the view toggles them when there
            // are more products than fit in MaxDots (sliding window, active dot centered).
            GameObject dotsRow = Child("DotsContainer", card.transform);
            AddMinH(dotsRow, DotsRowH);
            HorizontalLayoutGroup rowHlg = dotsRow.AddComponent<HorizontalLayoutGroup>();
            rowHlg.childAlignment = TextAnchor.MiddleCenter; rowHlg.spacing = DotSpacing;
            rowHlg.childControlWidth = true;  rowHlg.childControlHeight = false;
            rowHlg.childForceExpandWidth = false; rowHlg.childForceExpandHeight = false;

            GameObject prevChev = MakeDotChevron(dotsRow.transform, "DotsPrevChevron", chevron, pointLeft: true);
            view.dotsPrevChevron = prevChev;

            GameObject dotsGO = Child("Dots", dotsRow.transform);
            HorizontalLayoutGroup hlg = dotsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = DotSpacing;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            ContentSizeFitter dotsCsf = dotsGO.AddComponent<ContentSizeFitter>();
            dotsCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            dotsCsf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
            view.dotsContainer = dotsGO.transform;

            for (int i = 0; i < MaxDots; i++)
            {
                GameObject dot = Child($"Dot{i}", dotsGO.transform);
                dot.GetComponent<RectTransform>().sizeDelta = new Vector2(DotSize, DotSize);
                LayoutElement le = dot.AddComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = DotSize; le.minHeight = le.preferredHeight = DotSize;
                Image dotImg = dot.AddComponent<Image>();
                dotImg.color = i == 0 ? Dark : DotOff;
                dotImg.raycastTarget = false;
                dotImg.sprite = r4; dotImg.type = Image.Type.Sliced;
                if (i >= 3) dot.SetActive(false); // default preview shows a few; view reveals the rest at runtime
            }

            GameObject nextChev = MakeDotChevron(dotsRow.transform, "DotsNextChevron", chevron, pointLeft: false);
            view.dotsNextChevron = nextChev;

            // Assign dot color references so ProductSwapView.RefreshDots can set correct colors
            view.dotActiveColor   = Dark;
            view.dotInactiveColor = DotOff;
            view.maxDots          = MaxDots;

            // ── Prev / Next arrows — siblings of Card, outside it ─────────────
            // x = cardWidth/2 + arrowOffset = 270 + 80 = 350
            float arrowX = CardW * 0.5f + ArrowOffset;

            // Prev
            GameObject prevBtn = Child("PrevButton", root.transform);
            RectTransform prevRT = prevBtn.GetComponent<RectTransform>();
            prevRT.anchorMin = prevRT.anchorMax = prevRT.pivot = new Vector2(0.5f, 0.5f);
            prevRT.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            prevRT.anchoredPosition = new Vector2(-arrowX, 0f);
            prevRT.localScale = Vector3.one * ArrowScale;
            Image prevBg = prevBtn.AddComponent<Image>();
            prevBg.color = ArrowBgColor;   // glassy translucent
            prevBg.sprite = r40; prevBg.type = Image.Type.Simple;
            Button prevButton = prevBtn.AddComponent<Button>(); prevButton.targetGraphic = prevBg;
            prevButton.navigation = new Navigation { mode = Navigation.Mode.None };
            view.prevButton = prevButton;
            MakeArrowIcon(prevBtn.transform, chevron, pointLeft: true);

            // Next
            GameObject nextBtn = Child("NextButton", root.transform);
            RectTransform nextRT = nextBtn.GetComponent<RectTransform>();
            nextRT.anchorMin = nextRT.anchorMax = nextRT.pivot = new Vector2(0.5f, 0.5f);
            nextRT.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            nextRT.anchoredPosition = new Vector2(arrowX, 0f);
            nextRT.localScale = Vector3.one * ArrowScale;
            Image nextBg = nextBtn.AddComponent<Image>();
            nextBg.color = ArrowBgColor;   // glassy translucent
            nextBg.sprite = r40; nextBg.type = Image.Type.Simple;
            Button nextButton = nextBtn.AddComponent<Button>(); nextButton.targetGraphic = nextBg;
            nextButton.navigation = new Navigation { mode = Navigation.Mode.None };
            view.nextButton = nextButton;
            MakeArrowIcon(nextBtn.transform, chevron, pointLeft: false);

            SavePrefab(root, SwapPrefabPath);
        }

        // ── Root ProductBrowserUI prefab ──────────────────────────────────────

        static void BuildBrowserUIPrefab(ProductCategoryData fridges, ProductCategoryData cabinets, ProductCategoryData lights)
        {
            GameObject root = new GameObject("ProductBrowserUI", typeof(RectTransform));
            RectTransform rootRT = root.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(SwapRootW, SwapCardH);
            rootRT.pivot = Vector2.one * 0.5f;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CanvasGroup>();
            root.GetComponent<RectTransform>().localScale = Vector3.one * 0.001f;

            root.AddComponent<RoomRevive.IntentSelector.HeadFollowWorldUI>();
            root.AddComponent<RoomRevive.IntentSelector.MetaWorldSpaceCanvasSetup>();

            ProductBrowserController controller = root.AddComponent<ProductBrowserController>();
            ProductBrowserView        view       = root.AddComponent<ProductBrowserView>();
            ProductVariantRouter      varRouter  = root.AddComponent<ProductVariantRouter>();
            ProductVisibilityRouter   visRouter  = root.AddComponent<ProductVisibilityRouter>();

            controller.view = view;

            // Nest child panels as prefab instances.
            GameObject discoverAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DiscoverPrefabPath);
            GameObject swapAsset     = AssetDatabase.LoadAssetAtPath<GameObject>(SwapPrefabPath);

            if (discoverAsset != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(discoverAsset, root.transform);
                inst.SetActive(false);
                view.discoverPanel = inst.GetComponent<ProductDiscoverView>();

                // Wire "Explore options" → OpenSwap
                Button ctaBtn = inst.transform.Find("ExploreButton")?.GetComponent<Button>();
                if (ctaBtn != null)
                    UnityEventTools.AddPersistentListener(ctaBtn.onClick,
                        new UnityEngine.Events.UnityAction(controller.OpenSwap));
            }

            if (swapAsset != null)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(swapAsset, root.transform);
                inst.SetActive(false);
                view.swapPanel = inst.GetComponent<ProductSwapView>();

                ProductSwapView sv = inst.GetComponent<ProductSwapView>();

                // Wire Prev / Next arrows
                if (sv?.prevButton != null)
                    UnityEventTools.AddPersistentListener(sv.prevButton.onClick,
                        new UnityEngine.Events.UnityAction(controller.SelectPrevious));
                if (sv?.nextButton != null)
                    UnityEventTools.AddPersistentListener(sv.nextButton.onClick,
                        new UnityEngine.Events.UnityAction(controller.SelectNext));

                // FavoriteButton fires onClicked; the controller subscribes at runtime and drives FavoritesManager.
                FavoriteButton favBtn = inst.transform.Find("Card/SelectButton")?.GetComponent<FavoriteButton>();
                if (favBtn != null) controller.favoriteButton = favBtn;

                Button closeBtn = inst.transform.Find("Card/CloseButton")?.GetComponent<Button>();
                if (closeBtn != null)
                    UnityEventTools.AddPersistentListener(closeBtn.onClick,
                        new UnityEngine.Events.UnityAction(controller.Close));
            }

            // Wire controller → routers
            varRouter.controller = controller;
            visRouter.controller = controller;
            UnityEventTools.AddPersistentListener(controller.onProductConfirmed,
                new UnityEngine.Events.UnityAction<int>(varRouter.ForwardConfirm));
            UnityEventTools.AddPersistentListener(controller.onProductChanged,
                new UnityEngine.Events.UnityAction<ProductData>(varRouter.ForwardProductChanged));
            UnityEventTools.AddPersistentListener(controller.onClosed,
                new UnityEngine.Events.UnityAction(varRouter.ForwardClosed));

            SavePrefab(root, BrowserUIPrefabPath);
        }

        // ── Sprite baking ─────────────────────────────────────────────────────

        /// <summary>
        /// Bakes CSS radial-gradient(ellipse at 35% 25%, #D4D9E5 0%, #B5BCD0 65%, #9CA4BC 100%)
        /// plus rounded-corner alpha into a 256px-tall texture proportional to the card.
        /// Saved to disk so it only bakes once; subsequent runs load the existing asset.
        /// </summary>
        static Sprite GetOrBakeGradientSprite(int cardW, int cardH, string key, bool forceRebake = false)
        {
            string path     = $"{SpritesFolder}/CardBackground_{key}.png";
            if (!forceRebake)
            {
                Sprite cached = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (cached != null) return cached;
            }

            const int texH = 256;
            int texW  = Mathf.Max(1, Mathf.RoundToInt(texH * (float)cardW / cardH));
            float rPx = RadiusCard / cardH * texH;

            // Gradient stops — deepened slightly for a richer, less washed-out card (pic-2 feel)
            var c0 = new Color(0.750f, 0.775f, 0.840f); // ~#BFC6D6  0% (clearly lavender, not white)
            var c1 = new Color(0.655f, 0.688f, 0.785f); // ~#A7AFC8 65%
            var c2 = new Color(0.555f, 0.588f, 0.700f); // ~#8E96B3 100%

            // CSS center: 35% left, 25% top → Unity UV y is bottom-up, so cy = 0.75
            const float cx = 0.35f, cyCSS = 0.25f;
            float cy = 1f - cyCSS;
            float rx = Mathf.Max(cx, 1f - cx);        // 0.65 (farthest-corner)
            float ry = Mathf.Max(cyCSS, 1f - cyCSS);  // 0.75

            Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[texW * texH];

            for (int py = 0; py < texH; py++)
            for (int px2 = 0; px2 < texW; px2++)
            {
                float u  = (px2 + 0.5f) / texW;
                float v  = (py  + 0.5f) / texH;
                float du = (u - cx) / rx;
                float dv = (v - cy) / ry;
                float t  = Mathf.Sqrt(du * du + dv * dv);

                Color col = t <= 0.65f
                    ? Color.Lerp(c0, c1, t / 0.65f)
                    : Color.Lerp(c1, c2, Mathf.Clamp01((t - 0.65f) / 0.35f));

                float ex = Mathf.Max(rPx - (px2 + 0.5f), (px2 + 0.5f) - (texW - rPx), 0f);
                float ey = Mathf.Max(rPx - (py  + 0.5f), (py  + 0.5f) - (texH - rPx), 0f);
                float a  = Mathf.Clamp01(rPx - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);

                pixels[py * texW + px2] = new Color32(
                    (byte)(col.r * 255 + 0.5f),
                    (byte)(col.g * 255 + 0.5f),
                    (byte)(col.b * 255 + 0.5f),
                    (byte)(a    * 255 + 0.5f));
            }
            tex.SetPixels32(pixels); tex.Apply();

            WriteTextureToDisk(tex, path);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType      = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled    = false;
                ti.filterMode       = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Bakes a 64×64 anti-aliased rounded rectangle sprite for the given corner radius.
        /// Saved to disk with a radius-keyed filename; loaded on subsequent runs.
        /// Border is set to (r, r, r, r) so Unity can 9-slice it correctly.
        /// </summary>
        static Sprite GetOrBakeRoundedSprite(float radius, string key)
        {
            string path     = $"{SpritesFolder}/RoundedSprite_{key}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 64;
            float r = Mathf.Clamp(radius, 0f, size * 0.5f);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            for (int py = 0; py < size; py++)
            for (int px2 = 0; px2 < size; px2++)
            {
                float fcx = px2 + 0.5f, fcy = py + 0.5f;
                float dx = Mathf.Max(r - fcx, fcx - (size - r), 0f);
                float dy = Mathf.Max(r - fcy, fcy - (size - r), 0f);
                float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[py * size + px2] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels); tex.Apply();

            WriteTextureToDisk(tex, path);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType      = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spriteBorder     = new Vector4(r, r, r, r);
                ti.mipmapEnabled    = false;
                ti.filterMode       = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Bakes a soft (feathered) rounded-rectangle sprite for use as a drop shadow.
        /// White with an alpha that is solid inside the rounded rect and fades to 0 over
        /// <paramref name="feather"/> px outside it. 9-sliced (border = radius + feather) so it
        /// scales to any card size while keeping the soft corners. Tint via the Image color.
        /// </summary>
        static Sprite GetOrBakeSoftShadowSprite(string key, int size, float radius, float feather)
        {
            string path     = $"{SpritesFolder}/Shadow_{key}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            float half      = size * 0.5f;
            float innerHalf = Mathf.Max(0f, half - radius - feather); // half-extent of the straight region

            for (int py = 0; py < size; py++)
            for (int px2 = 0; px2 < size; px2++)
            {
                float dx = Mathf.Max(Mathf.Abs(px2 + 0.5f - half) - innerHalf, 0f);
                float dy = Mathf.Max(Mathf.Abs(py  + 0.5f - half) - innerHalf, 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius; // signed distance to the rounded edge
                float a = Mathf.Clamp01(1f - dist / feather);
                a = a * a * (3f - 2f * a);                            // smootherstep falloff
                pixels[py * size + px2] = new Color32(255, 255, 255, (byte)(a * 255 + 0.5f));
            }
            tex.SetPixels32(pixels); tex.Apply();

            WriteTextureToDisk(tex, path);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                float b = radius + feather;
                ti.textureType      = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spriteBorder     = new Vector4(b, b, b, b);
                ti.mipmapEnabled    = false;
                ti.filterMode       = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Bakes a smooth, round-capped ">" chevron (two capsule strokes meeting at a right-side apex).
        /// White; tint via the Image color. Use as-is for "next", flip localScale.x = -1 for "prev".
        /// </summary>
        static Sprite GetOrBakeChevronSprite(string key, int size, float thickness)
        {
            string path     = $"{SpritesFolder}/Chevron_{key}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            // ">" : top-left → apex (right) → bottom-left (pixel space, y up). Centroid ≈ centre.
            Vector2 a = new Vector2(size * 0.38f, size * 0.72f);
            Vector2 b = new Vector2(size * 0.62f, size * 0.50f);
            Vector2 c = new Vector2(size * 0.38f, size * 0.28f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c));
                float alpha = Mathf.Clamp01(thickness - d + 0.5f); // round caps come free from the capsule distance
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255 + 0.5f));
            }
            tex.SetPixels32(pixels); tex.Apply();

            WriteTextureToDisk(tex, path);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType      = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled    = false;
                ti.filterMode       = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a, ap = p - a;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(Vector2.Dot(ab, ab), 1e-4f));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Adds a chevron arrow icon to a nav button. Falls back to a text glyph if the
        /// sprite isn't loadable yet (first bake of a session returns null until the import settles).</summary>
        static void MakeArrowIcon(Transform parent, Sprite chevron, bool pointLeft)
        {
            if (chevron != null)
            {
                GameObject icon = Child("ArrowIcon", parent);
                RectTransform rt = icon.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(ArrowSize * 0.46f, ArrowSize * 0.46f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = new Vector3(pointLeft ? -1f : 1f, 1f, 1f);
                Image img = icon.AddComponent<Image>();
                img.sprite = chevron; img.type = Image.Type.Simple; img.preserveAspect = true;
                img.color = CtaText; img.raycastTarget = false;
            }
            else
            {
                TextMeshProUGUI lbl = MakeText(parent, "ArrowLabel", pointLeft ? "‹" : "›",
                    ArrowSize * 0.45f, CtaText, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                StretchFull(lbl.gameObject); lbl.alignment = TextAlignmentOptions.Center; lbl.raycastTarget = false;
            }
        }

        /// <summary>Small muted chevron used at the ends of the dot row to flag more products. Returns the GameObject (toggled by the view).</summary>
        static GameObject MakeDotChevron(Transform parent, string name, Sprite chevron, bool pointLeft)
        {
            const float size = 14f;
            GameObject go = Child(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = size; le.minHeight = le.preferredHeight = size;
            go.GetComponent<RectTransform>().localScale = new Vector3(pointLeft ? -1f : 1f, 1f, 1f);
            Image img = go.AddComponent<Image>();
            img.sprite = chevron; img.type = Image.Type.Simple; img.preserveAspect = true;
            img.color = new Color(Dark.r, Dark.g, Dark.b, 0.55f); img.raycastTarget = false;
            go.SetActive(false); // shown by the view only when there are more products
            return go;
        }

        static void WriteTextureToDisk(Texture2D tex, string assetPath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        }

        // ── ScriptableObject helpers ──────────────────────────────────────────

        static ProductCategoryData LoadOrCreateCategory(string path, string id, string displayName,
                                                          ProductSwapType swapType, Color accent)
        {
            ProductCategoryData ex = AssetDatabase.LoadAssetAtPath<ProductCategoryData>(path);
            if (ex != null) return ex;
            ProductCategoryData cat = ScriptableObject.CreateInstance<ProductCategoryData>();
            cat.id = id; cat.displayName = displayName; cat.swapType = swapType; cat.accentColor = accent;
            AssetDatabase.CreateAsset(cat, path);
            return cat;
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        static GameObject NewRoot(string name, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h); rt.pivot = Vector2.one * 0.5f;
            return go;
        }

        static GameObject Child(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetRect(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, string text,
                                         float size, Color color,
                                         Vector2 sizeDelta, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            GameObject go = Child(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            if (sizeDelta != Vector2.zero) rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color;
            tmp.enableAutoSizing = false; tmp.raycastTarget = false;
            return tmp;
        }

        static void AddMinH(GameObject go, float h)
        {
            LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
        }

        static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            Object.DestroyImmediate(root);
            if (ok) Debug.Log($"[ProductBrowserPrefabCreator] Saved: {path}");
            else    Debug.LogError($"[ProductBrowserPrefabCreator] FAILED: {path}");
        }

        // ── Folder setup ──────────────────────────────────────────────────────

        static void EnsureFolders()
        {
            EnsureFolder(BaseFolder); EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder); EnsureFolder(SpritesFolder);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >>  8) & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f);
    }
}
#endif
