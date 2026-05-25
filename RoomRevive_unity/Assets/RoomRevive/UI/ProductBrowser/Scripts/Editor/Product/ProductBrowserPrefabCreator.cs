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
        static readonly Color CloseCircle = new Color(0.227f, 0.251f, 0.333f, 0.10f);

        // ── Swap panel layout ─────────────────────────────────────────────────
        const float CardW        = 540f;
        const float CardPad      = 28f;
        const float CloseSize    = 72f;
        const float ArrowOffset  = 80f;
        const float ArrowSize    = 80f;
        const float ArrowScale   = 1.5f;
        const float CtaH         = 60f;
        const float DotSize      = 8f;
        const float DotSpacing   = 8f;
        const float DotsRowH     = 38f;
        const float TextBlockH   = 110f;   // brand + name + desc
        const float PriceRowH    = 22f;

        // Font sizes
        const float BrandFont    = 14f;
        const float NameFont     = 24f;
        const float DescFont     = 17f;
        const float CtaFont      = 18f;

        // MinH per text row (used with LayoutElement inside VLG)
        const float BrandMinH    = 22f;
        const float NameMinH     = 32f;
        const float DescMinH     = 26f;

        // Corner radii
        const float RadiusCard   = 28f;
        const float RadiusImage  = 18f;
        const float RadiusCta    = 14f;
        const float RadiusClose  = 36f;   // = CloseSize * 0.5 → full circle
        const float RadiusDot    = 4f;
        // Arrow: ArrowSize * 0.5f = 40f → full circle (saved as "r40")

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
        static readonly float SwapImgH  = (CardW - CardPad * 2f) / 1.15f; // ~420.87
        // cardH = pad + close + gap + img + gap + text + gap + price + gap + cta + gap + dots + pad
        static readonly float SwapCardH =
            CardPad + CloseSize + 14f + (CardW - CardPad * 2f) / 1.15f + 22f
            + TextBlockH + 14f + PriceRowH + 10f + CtaH + 22f + DotsRowH + CardPad;
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
            float inner  = SwapInner;  // 484
            float imgH   = SwapImgH;   // ~420.87
            float cardH  = SwapCardH;  // ~861

            // Bake sprites (saved to disk, loaded on subsequent runs)
            Sprite cardSprite = GetOrBakeGradientSprite((int)CardW, (int)cardH, "Swap");
            Sprite r18        = GetOrBakeRoundedSprite(RadiusImage, "r18");
            Sprite r14        = GetOrBakeRoundedSprite(RadiusCta,   "r14");
            Sprite r36        = GetOrBakeRoundedSprite(RadiusClose,  "r36");
            Sprite r4         = GetOrBakeRoundedSprite(RadiusDot,    "r4");
            Sprite r40        = GetOrBakeRoundedSprite(ArrowSize * 0.5f, "r40"); // full circle

            // Root wider than card to give arrows room
            GameObject root = NewRoot("ProductSwapPanel", SwapRootW, cardH);
            ProductSwapView view = root.AddComponent<ProductSwapView>();

            // ── Card ──────────────────────────────────────────────────────────
            GameObject card = Child("Card", root.transform);
            RectTransform cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(CardW, cardH);
            cardRT.anchoredPosition = Vector2.zero;

            // Gradient + rounded-corner background — Image.Type.Simple (corners baked into alpha)
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = Color.white; cardImg.sprite = cardSprite;
            cardImg.type = Image.Type.Simple; cardImg.preserveAspect = false;
            cardImg.raycastTarget = true;

            float y = -CardPad;

            // ── Close button — top-right, absolute ────────────────────────────
            GameObject closeGO = Child("CloseButton", card.transform);
            RectTransform clRT = closeGO.GetComponent<RectTransform>();
            clRT.anchorMin = clRT.anchorMax = new Vector2(1f, 1f);
            clRT.pivot     = new Vector2(1f, 1f);
            clRT.sizeDelta = new Vector2(CloseSize, CloseSize);
            clRT.anchoredPosition = new Vector2(-CardPad, -CardPad);
            Image clBg = closeGO.AddComponent<Image>();
            clBg.color = CloseCircle; clBg.sprite = r36; clBg.type = Image.Type.Simple;
            Button clBtn = closeGO.AddComponent<Button>(); clBtn.targetGraphic = clBg;
            clBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            TextMeshProUGUI clLbl = MakeText(closeGO.transform, "Label", "×",
                CloseSize * 0.45f, Dark, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            StretchFull(clLbl.gameObject);
            clLbl.alignment = TextAlignmentOptions.Center; clLbl.raycastTarget = false;

            // ── Category label — top-left ─────────────────────────────────────
            // Narrow enough to not overlap close button
            float catW = CardW - CardPad * 2f - CloseSize - 4f;
            TextMeshProUGUI catTmp = MakeText(card.transform, "CategoryLabel", "FRIDGES", 11f, Muted,
                new Vector2(catW, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(CardPad, y));
            catTmp.fontStyle = FontStyles.Bold; catTmp.characterSpacing = 20f;
            catTmp.alignment = TextAlignmentOptions.Left; catTmp.raycastTarget = false;
            view.categoryLabel = catTmp;

            y -= CloseSize + 14f;

            // ── Image area ────────────────────────────────────────────────────
            // Rounded rect background + Mask component clips the product photo.
            GameObject imgObj = Child("ImageArea", card.transform);
            RectTransform imgRT = imgObj.GetComponent<RectTransform>();
            imgRT.anchorMin = imgRT.anchorMax = new Vector2(0.5f, 1f);
            imgRT.pivot     = new Vector2(0.5f, 1f);
            imgRT.sizeDelta = new Vector2(inner, imgH);
            imgRT.anchoredPosition = new Vector2(0f, y);
            Image imgAreaImg = imgObj.AddComponent<Image>();
            imgAreaImg.color = ImageBg; imgAreaImg.raycastTarget = false;
            imgAreaImg.sprite = r18; imgAreaImg.type = Image.Type.Sliced;
            Mask imgMask = imgObj.AddComponent<Mask>(); imgMask.showMaskGraphic = true;

            // Product photo — stretched inside mask, hidden until sprite assigned at runtime
            GameObject photoGO = Child("ProductPhoto", imgObj.transform);
            StretchFull(photoGO);
            Image photoImg = photoGO.AddComponent<Image>();
            photoImg.type = Image.Type.Simple; photoImg.preserveAspect = false;
            photoImg.color = Color.white; photoImg.raycastTarget = false;
            photoImg.gameObject.SetActive(false);
            view.productImage = photoImg;

            y -= imgH + 22f;

            // ── Text block — VerticalLayoutGroup ──────────────────────────────
            // Children: brand, product name, description.
            // LayoutElement.minHeight on each child controls row height.
            GameObject tbGO = Child("TextBlock", card.transform);
            RectTransform tbRT = tbGO.GetComponent<RectTransform>();
            tbRT.anchorMin = tbRT.anchorMax = new Vector2(0.5f, 1f);
            tbRT.pivot     = new Vector2(0.5f, 1f);
            tbRT.sizeDelta = new Vector2(inner, TextBlockH);
            tbRT.anchoredPosition = new Vector2(0f, y);
            VerticalLayoutGroup vlg = tbGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(4, 4, 0, 0);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;  vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            // Brand — 14f, Muted, all-caps tracking
            TextMeshProUGUI brandTmp = MakeText(tbGO.transform, "BrandLabel", "BRAND", BrandFont, Muted,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            brandTmp.fontStyle = FontStyles.Bold; brandTmp.characterSpacing = 20f;
            brandTmp.alignment = TextAlignmentOptions.Left; brandTmp.raycastTarget = false;
            AddMinH(brandTmp.gameObject, BrandMinH);
            view.brandLabel = brandTmp;

            // Product name — 24f, Dark
            TextMeshProUGUI nameTmp = MakeText(tbGO.transform, "ProductNameLabel", "Product Name", NameFont, Dark,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            nameTmp.textWrappingMode = TextWrappingModes.Normal;
            nameTmp.alignment = TextAlignmentOptions.Left; nameTmp.raycastTarget = false;
            AddMinH(nameTmp.gameObject, NameMinH);
            view.productNameLabel = nameTmp;

            // Description — 17f, Muted
            TextMeshProUGUI descTmp = MakeText(tbGO.transform, "DescriptionLabel", "Short product description.", DescFont, Muted,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            descTmp.textWrappingMode = TextWrappingModes.Normal;
            descTmp.alignment = TextAlignmentOptions.Left; descTmp.raycastTarget = false;
            AddMinH(descTmp.gameObject, DescMinH);
            view.shortDescriptionLabel = descTmp;

            y -= TextBlockH + 14f;

            // ── Price row — absolute, below text block ────────────────────────
            GameObject priceRow = Child("PriceRow", card.transform);
            SetRect(priceRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(inner, PriceRowH), new Vector2(0f, y));
            Image priceRowBg = priceRow.AddComponent<Image>(); priceRowBg.color = Color.clear; priceRowBg.raycastTarget = false;
            view.priceRow = priceRow;
            TextMeshProUGUI priceTmp = MakeText(priceRow.transform, "PriceLabel", "From $0", BrandFont, Dark,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            StretchFull(priceTmp.gameObject); priceTmp.fontStyle = FontStyles.Bold;
            priceTmp.alignment = TextAlignmentOptions.Left; priceTmp.raycastTarget = false;
            view.priceLabel = priceTmp;

            y -= PriceRowH + 10f;

            // ── Select CTA ────────────────────────────────────────────────────
            GameObject ctaGO = Child("SelectButton", card.transform);
            RectTransform ctaRT2 = ctaGO.GetComponent<RectTransform>();
            ctaRT2.anchorMin = ctaRT2.anchorMax = new Vector2(0.5f, 1f);
            ctaRT2.pivot     = new Vector2(0.5f, 1f);
            ctaRT2.sizeDelta = new Vector2(inner, CtaH);
            ctaRT2.anchoredPosition = new Vector2(0f, y);
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

            y -= CtaH + 22f;

            // ── Dots row ──────────────────────────────────────────────────────
            GameObject dotsGO = Child("DotsContainer", card.transform);
            RectTransform dotsRT = dotsGO.GetComponent<RectTransform>();
            dotsRT.anchorMin = dotsRT.anchorMax = new Vector2(0.5f, 1f);
            dotsRT.pivot     = new Vector2(0.5f, 1f);
            dotsRT.sizeDelta = new Vector2(inner, DotsRowH);
            dotsRT.anchoredPosition = new Vector2(0f, y);
            HorizontalLayoutGroup hlg = dotsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = DotSpacing;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            view.dotsContainer = dotsGO.transform;

            for (int i = 0; i < 3; i++)
            {
                GameObject dot = Child($"Dot{i}", dotsGO.transform);
                dot.GetComponent<RectTransform>().sizeDelta = new Vector2(DotSize, DotSize);
                LayoutElement le = dot.AddComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = DotSize; le.minHeight = le.preferredHeight = DotSize;
                Image dotImg = dot.AddComponent<Image>();
                dotImg.color = i == 0 ? Dark : DotOff;
                dotImg.raycastTarget = false;
                dotImg.sprite = r4; dotImg.type = Image.Type.Sliced;
            }

            // Assign dot color references so ProductSwapView.RefreshDots can set correct colors
            view.dotActiveColor   = Dark;
            view.dotInactiveColor = DotOff;

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
            prevBg.color = new Color(Dark.r, Dark.g, Dark.b, 0.90f);
            prevBg.sprite = r40; prevBg.type = Image.Type.Simple;
            Button prevButton = prevBtn.AddComponent<Button>(); prevButton.targetGraphic = prevBg;
            prevButton.navigation = new Navigation { mode = Navigation.Mode.None };
            view.prevButton = prevButton;
            TextMeshProUGUI prevLbl = MakeText(prevBtn.transform, "ArrowLabel", "‹",
                ArrowSize * 0.45f, CtaText, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            StretchFull(prevLbl.gameObject); prevLbl.alignment = TextAlignmentOptions.Center;
            prevLbl.raycastTarget = false;

            // Next
            GameObject nextBtn = Child("NextButton", root.transform);
            RectTransform nextRT = nextBtn.GetComponent<RectTransform>();
            nextRT.anchorMin = nextRT.anchorMax = nextRT.pivot = new Vector2(0.5f, 0.5f);
            nextRT.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            nextRT.anchoredPosition = new Vector2(arrowX, 0f);
            nextRT.localScale = Vector3.one * ArrowScale;
            Image nextBg = nextBtn.AddComponent<Image>();
            nextBg.color = new Color(Dark.r, Dark.g, Dark.b, 0.90f);
            nextBg.sprite = r40; nextBg.type = Image.Type.Simple;
            Button nextButton = nextBtn.AddComponent<Button>(); nextButton.targetGraphic = nextBg;
            nextButton.navigation = new Navigation { mode = Navigation.Mode.None };
            view.nextButton = nextButton;
            TextMeshProUGUI nextLbl = MakeText(nextBtn.transform, "ArrowLabel", "›",
                ArrowSize * 0.45f, CtaText, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            StretchFull(nextLbl.gameObject); nextLbl.alignment = TextAlignmentOptions.Center;
            nextLbl.raycastTarget = false;

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
        static Sprite GetOrBakeGradientSprite(int cardW, int cardH, string key)
        {
            string path     = $"{SpritesFolder}/CardBackground_{key}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int texH = 256;
            int texW  = Mathf.Max(1, Mathf.RoundToInt(texH * (float)cardW / cardH));
            float rPx = RadiusCard / cardH * texH;

            // Gradient stops
            var c0 = new Color(0.831f, 0.851f, 0.898f); // #D4D9E5  0%
            var c1 = new Color(0.710f, 0.737f, 0.816f); // #B5BCD0 65%
            var c2 = new Color(0.612f, 0.643f, 0.737f); // #9CA4BC 100%

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
            AssetDatabase.ImportAsset(path);

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
            AssetDatabase.ImportAsset(path);

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
