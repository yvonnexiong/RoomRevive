using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using RoomRevive.Onboarding;

namespace RoomRevive.Onboarding.Editor
{
    /// <summary>
    /// Builds the onboarding UI prefab in two steps:
    ///   Phase 0 — generates the shared rounded-rect sprite + OnboardingTheme asset
    ///   Phase 1 — builds the Q1 visual shell (static, no logic, one page only)
    ///
    /// Run these menus in order, then open the prefab in Prefab Mode to tweak visuals.
    /// Assign Alan Sans to OnboardingTheme.font in the Inspector once it's imported.
    /// </summary>
    public static class OnboardingPrefabCreator
    {
        const string SpritePath        = "Assets/Onboarding/Sprites/RoundedRect.png";
        const string ProgBarSpritePath = "Assets/Onboarding/Sprites/ProgBar.png";
        const string BgGradientPath    = "Assets/Onboarding/Sprites/BgGradient.png";
        const string ThemePath         = "Assets/Onboarding/Data/OnboardingTheme.asset";
        const string PrefabPath        = "Assets/Onboarding/Prefabs/OnboardingFlowUI.prefab";
        const string ImagesRoot        = "Assets/Onboarding/Images";
        const string FontPath          = "Assets/RoomRevive/Font/AlanSans-VariableFont_wght SDF.asset";

        static TMP_FontAsset s_font;

        // Matches OnboardingTheme defaults — used here so Phase 1 works before font is assigned
        static readonly Color SurfaceBase  = Hex(0xB5BCD0);
        static readonly Color SurfaceLight = Hex(0xD4D9E5);
        static readonly Color SurfaceDeep  = Hex(0x7C85A0);
        static readonly Color CardInner    = Hex(0xC7CDDD);
        static readonly Color InkPrimary   = Hex(0x3A4055);
        static readonly Color InkSecondary = Hex(0x6B7388);
        static readonly Color BtnText      = Hex(0xE6E9F0); // WelcomeUI button text

        // ── Phase 0 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 0 — Generate Sprite + Theme")]
        static void Phase0()
        {
            EnsureFolder("Assets/Onboarding/Sprites");
            GenerateRoundedRectSprite();
            GenerateProgBarSprite();
            GenerateBgGradientSprite();
            CreateThemeAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("[Onboarding] Phase 0 done — sprites and theme asset ready.");
        }

        static void GenerateRoundedRectSprite()
        {
            const int size = 64, radius = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px  = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + .5f - size * .5f) - (size * .5f - radius));
                float dy = Mathf.Max(0f, Mathf.Abs(y + .5f - size * .5f) - (size * .5f - radius));
                bool inside = dx * dx + dy * dy <= radius * radius;
                px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(SpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder     = new Vector4(radius, radius, radius, radius); // 9-slice
            imp.filterMode       = FilterMode.Bilinear;
            imp.SaveAndReimport();
        }

        // 12 × 4 px pill, 2-px radius — perfect for a 4px-tall progress segment.
        // 9-slice border = 2 keeps the rounded ends fixed; center stretches horizontally.
        static void GenerateProgBarSprite()
        {
            const int w = 12, h = 4, r = 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px  = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + .5f - w * .5f) - (w * .5f - r));
                float dy = Mathf.Max(0f, Mathf.Abs(y + .5f - h * .5f) - (h * .5f - r));
                bool inside = dx * dx + dy * dy <= (float)(r * r);
                px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(ProgBarSpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(ProgBarSpritePath);
            imp.textureType            = TextureImporterType.Sprite;
            imp.spriteImportMode       = SpriteImportMode.Single;
            imp.spriteBorder           = new Vector4(r, r, r, r);
            imp.filterMode             = FilterMode.Point; // pixel-perfect at tiny size
            imp.textureCompression     = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency    = true;
            imp.SaveAndReimport();
        }

        // Radial gradient matching WelcomeUI: #D4D9E5 → #B5BCD0 → #9CA4BC
        // Center at (35%, 25% from top), corner radius 24 px in texture space.
        static void GenerateBgGradientSprite()
        {
            const int w = 256, h = 416; // matches 480:780 aspect
            const float rPx = 24f;

            var c0 = new Color(0.831f, 0.851f, 0.898f); // #D4D9E5
            var c1 = new Color(0.710f, 0.737f, 0.816f); // #B5BCD0
            var c2 = new Color(0.612f, 0.643f, 0.737f); // #9CA4BC

            const float cx = 0.35f, cyCSS = 0.25f;
            float cy = 1f - cyCSS;
            float rx = Mathf.Max(cx, 1f - cx);
            float ry = Mathf.Max(cyCSS, 1f - cyCSS);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px  = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u   = (x + 0.5f) / w;
                float v   = (y + 0.5f) / h;
                float ddx = (u - cx) / rx;
                float ddy = (v - cy) / ry;
                float t   = Mathf.Clamp01(Mathf.Sqrt(ddx * ddx + ddy * ddy));

                Color col = t <= 0.65f
                    ? Color.Lerp(c0, c1, t / 0.65f)
                    : Color.Lerp(c1, c2, (t - 0.65f) / 0.35f);

                float ex = Mathf.Max(rPx - (x + 0.5f), (x + 0.5f) - (w - rPx), 0f);
                float ey = Mathf.Max(rPx - (y + 0.5f), (y + 0.5f) - (h - rPx), 0f);
                float a  = Mathf.Clamp01(rPx - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);

                px[y * w + x] = new Color32(
                    (byte)(col.r * 255 + 0.5f), (byte)(col.g * 255 + 0.5f),
                    (byte)(col.b * 255 + 0.5f), (byte)(a * 255 + 0.5f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(BgGradientPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(BgGradientPath);
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder     = Vector4.zero;
            imp.filterMode       = FilterMode.Bilinear;
            imp.SaveAndReimport();
        }

        static void CreateThemeAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<OnboardingTheme>(ThemePath) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<OnboardingTheme>(), ThemePath);
        }

        // ── Phase 1 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 1 — Build Q1 Shell")]
        static void Phase1()
        {
            var roundRect = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (roundRect == null)
            {
                Debug.LogError("[Onboarding] Run Phase 0 first to generate the sprite.");
                return;
            }
            var progBarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProgBarSpritePath) ?? roundRect;
            var bgGradient    = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            if (bgGradient == null) Debug.LogWarning("[Onboarding] BgGradient sprite not found — run Phase 0 first.");
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_font == null) Debug.LogWarning("[Onboarding] Alan Sans font not found at " + FontPath);
            EnsureFolder("Assets/Onboarding/Prefabs");

            // ── Canvas (World Space, 480 × 700 units, 1 unit = 1mm) ──────────
            var root   = new GameObject("OnboardingFlowUI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<GraphicRaycaster>();
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(480f, 780f);
            root.transform.localScale = Vector3.one * 0.001f;

            // ── Panel ─────────────────────────────────────────────────────────
            var panel = MakeRT("Panel", root.transform);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding              = new RectOffset(20, 20, 16, 16);
            vl.spacing              = 12f;
            vl.childAlignment       = TextAnchor.UpperCenter;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth      = true;
            vl.childControlHeight     = true;

            // Background gradient (ignoreLayout so VL skips it; renders behind all content)
            if (bgGradient != null)
            {
                var bg = MakeImage("Background", panel, bgGradient, Color.white);
                bg.type             = Image.Type.Simple;
                bg.preserveAspect   = false;
                bg.raycastTarget    = false;
                bg.rectTransform.anchorMin = Vector2.zero;
                bg.rectTransform.anchorMax = Vector2.one;
                bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
                bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            // ── Progress bar ─────────────────────────────────────────────────
            // Wrapper owns the LayoutElement so the parent VL sees exactly 4px.
            // The HLG lives one level inside and stretch-fills the wrapper —
            // this prevents HLG's own ILayoutElement from overriding our height.
            var progressWrapper = MakeRT("ProgressBar", panel);
            LE(progressWrapper, preferredHeight: 4f);
            var progressBar = MakeRT("Segments", progressWrapper);
            progressBar.anchorMin = Vector2.zero;
            progressBar.anchorMax = Vector2.one;
            progressBar.offsetMin = progressBar.offsetMax = Vector2.zero;
            var pHL = progressBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            pHL.spacing              = 6f;
            pHL.childForceExpandWidth  = true;
            pHL.childForceExpandHeight = true;
            pHL.childControlWidth      = true;
            pHL.childControlHeight     = true;
            for (int i = 0; i < 4; i++)
            {
                var seg  = MakeImage($"Seg{i + 1}", progressBar, progBarSprite,
                    i == 0 ? SurfaceDeep : new Color(SurfaceDeep.r, SurfaceDeep.g, SurfaceDeep.b, 0.35f));
                seg.type = Image.Type.Sliced;
            }

            // ── Banner ────────────────────────────────────────────────────────
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            LE(banner.rectTransform, preferredHeight: 84f);
            var bannerVL = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVL.padding              = new RectOffset(16, 16, 14, 10);
            bannerVL.spacing              = 4f;
            bannerVL.childAlignment       = TextAnchor.MiddleCenter;
            bannerVL.childForceExpandWidth  = true;
            bannerVL.childForceExpandHeight = false;
            bannerVL.childControlWidth      = true;
            bannerVL.childControlHeight     = true;

            var titleTMP = MakeTMP("Title", banner.transform,
                "Which style do you prefer?", 26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);
            LE(titleTMP.rectTransform, preferredHeight: 34f);

            var subTMP = MakeTMP("Subtitle", banner.transform,
                "1 of 4", 12f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);
            LE(subTMP.rectTransform, preferredHeight: 18f);

            // ── Card grid (2×2, Q1 Style) ─────────────────────────────────────
            var gridRT = MakeRT("CardGrid", panel);
            LE(gridRT, preferredHeight: 554f);
            var grid            = gridRT.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize       = new Vector2(215f, 271f);
            grid.spacing        = new Vector2(12f, 12f);
            grid.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;

            (string label, string sub, string img)[] cards =
            {
                ("Clean & Uncluttered", "Modern",        "kitchen_style_1"),
                ("Bold & Dramatic",     "Designer",      "kitchen_style_2"),
                ("Warm & Cozy",         "Cottage",       "kitchen_style_3"),
                ("Calm & Natural",      "Scandinavian",  "kitchen_style_4"),
            };
            var cardViews = new OnboardingOptionCardView[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                var (label, sub, img) = cards[i];
                cardViews[i] = BuildImageCard(gridRT, roundRect, img, label, sub);
            }

            // ── Nav bar ────────────────────────────────────────────────────────
            var navRT = MakeRT("NavBar", panel);
            LE(navRT, preferredHeight: 44f);

            var backBtn = BuildButton(navRT, roundRect, "BackButton", "Back",
                SurfaceLight, InkPrimary, alpha: 1f);
            backBtn.anchorMin        = new Vector2(0, .5f);
            backBtn.anchorMax        = new Vector2(0, .5f);
            backBtn.pivot            = new Vector2(0, .5f);
            backBtn.anchoredPosition = Vector2.zero;
            backBtn.sizeDelta        = new Vector2(120f, 44f);
            var backCG = backBtn.gameObject.AddComponent<CanvasGroup>();

            // Next button matches WelcomeUI: InkPrimary (#3A4055) bg + BtnText (#E6E9F0)
            var nextBtn = BuildButton(navRT, roundRect, "NextButton", "Next",
                InkPrimary, BtnText, alpha: 1f);
            nextBtn.anchorMin = new Vector2(0f, 0.5f);
            nextBtn.anchorMax = new Vector2(1f, 0.5f);
            nextBtn.pivot     = new Vector2(1f, 0.5f);
            nextBtn.offsetMin = new Vector2(132f, -22f); // 120 back + 12 gap, -half height
            nextBtn.offsetMax = new Vector2(0f,   22f);  // flush right, +half height
            var nextCG = nextBtn.gameObject.AddComponent<CanvasGroup>();
            nextCG.alpha = 0.45f; // dimmed until a card is selected

            // ── Controller ────────────────────────────────────────────────────
            var controller = panel.gameObject.AddComponent<OnboardingQ1Controller>();
            controller.Setup(cardViews,
                nextBtn.GetComponent<Button>(), nextBtn, nextCG, backCG);

            // ── Save ──────────────────────────────────────────────────────────
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"[Onboarding] Phase 1 done — prefab at {PrefabPath}. Open in Prefab Mode to tweak.");
        }

        // ── Card builder ──────────────────────────────────────────────────────
        // Structure: CellWrapper (GridLayout child)
        //   Ring    — roundRect Image, CardInner normally / Teal when selected
        //   CardBody — roundRect Image, inset 2 px, Mask+Button; children clipped
        //     Photo
        //     Caption → Label + Sub TMPs

        static OnboardingOptionCardView BuildImageCard(RectTransform parent, Sprite roundRect,
            string imgName, string label, string subtitle)
        {
            // CellWrapper — the GridLayout child
            var cell = MakeRT(label.Split(' ')[0] + "Card", parent);

            // Ring: full-cell, CardInner = invisible when not selected, Teal when selected
            var ring = MakeImage("Ring", cell, roundRect, CardInner);
            ring.type = Image.Type.Sliced;
            ring.rectTransform.anchorMin = Vector2.zero;
            ring.rectTransform.anchorMax = Vector2.one;
            ring.rectTransform.offsetMin = Vector2.zero;
            ring.rectTransform.offsetMax = Vector2.zero;

            // CardBody: inset 2 px so ring peeks out on selection
            var cardBody = MakeImage("CardBody", cell, roundRect, CardInner);
            cardBody.type = Image.Type.Sliced;
            cardBody.rectTransform.anchorMin = Vector2.zero;
            cardBody.rectTransform.anchorMax = Vector2.one;
            cardBody.rectTransform.offsetMin = new Vector2(2f, 2f);
            cardBody.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var cardMask = cardBody.gameObject.AddComponent<Mask>();
            cardMask.showMaskGraphic = true;
            cardBody.gameObject.AddComponent<Button>();

            const float captionH = 56f;

            // Photo: fills CardBody except bottom captionH pixels
            var photoRT = MakeRT("Photo", cardBody.transform);
            photoRT.anchorMin = Vector2.zero;
            photoRT.anchorMax = Vector2.one;
            photoRT.offsetMin = new Vector2(0f, captionH);
            photoRT.offsetMax = Vector2.zero;
            var photoImg = photoRT.gameObject.AddComponent<Image>();
            photoImg.preserveAspect = false;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ImagesRoot}/{imgName}.png");
            if (sprite != null) photoImg.sprite = sprite;
            photoImg.color = Color.white;

            // Caption: anchored to bottom captionH pixels of CardBody
            var captionColor = new Color(CardInner.r - 0.04f, CardInner.g - 0.04f, CardInner.b - 0.04f, 1f);
            var captionImg   = MakeImage("Caption", cardBody.transform, null, captionColor);
            captionImg.rectTransform.anchorMin = Vector2.zero;
            captionImg.rectTransform.anchorMax = new Vector2(1f, 0f);
            captionImg.rectTransform.offsetMin = Vector2.zero;
            captionImg.rectTransform.offsetMax = new Vector2(0f, captionH);
            var captionVL = captionImg.gameObject.AddComponent<VerticalLayoutGroup>();
            captionVL.padding              = new RectOffset(14, 14, 10, 12);
            captionVL.spacing              = 2f;
            captionVL.childAlignment       = TextAnchor.UpperLeft;
            captionVL.childForceExpandWidth  = true;
            captionVL.childForceExpandHeight = false;
            captionVL.childControlWidth      = true;
            captionVL.childControlHeight     = true;

            var lTMP = MakeTMP("Label", captionImg.transform, label, 15f, FontStyles.Bold,
                InkPrimary, TextAlignmentOptions.Left);
            LE(lTMP.rectTransform, preferredHeight: 22f);

            var sTMP = MakeTMP("Sub", captionImg.transform, subtitle, 12f, FontStyles.Normal,
                InkSecondary, TextAlignmentOptions.Left);
            LE(sTMP.rectTransform, preferredHeight: 17f);

            var view = cell.gameObject.AddComponent<OnboardingOptionCardView>();
            view.Init(ring, cardBody, captionImg, lTMP, sTMP);

            var proxy = cardBody.gameObject.AddComponent<OnboardingCardInteractionProxy>();
            proxy.Init(view);

            return view;
        }

        // ── Button builder ────────────────────────────────────────────────────

        static RectTransform BuildButton(RectTransform parent, Sprite roundRect,
            string name, string text, Color bg, Color fg, float alpha = 1f)
        {
            // Image stays white so ColorBlock colors render exactly as specified
            var btn  = MakeImage(name, parent, roundRect, Color.white);
            btn.type = Image.Type.Sliced;

            var baseColor = new Color(bg.r, bg.g, bg.b, alpha);
            var button    = btn.gameObject.AddComponent<Button>();
            button.colors = new ColorBlock
            {
                normalColor      = baseColor,
                highlightedColor = LightenColor(baseColor, 0.12f),
                pressedColor     = LightenColor(baseColor, 0.12f),
                selectedColor    = baseColor,
                disabledColor    = baseColor, // CanvasGroup handles dimming
                colorMultiplier  = 1f,
                fadeDuration     = 0f         // instant, no animation
            };

            var lTMP = MakeTMP("Label", btn.transform, text, 14f, FontStyles.Bold,
                fg, TextAlignmentOptions.Center);
            lTMP.rectTransform.anchorMin = Vector2.zero;
            lTMP.rectTransform.anchorMax = Vector2.one;
            lTMP.rectTransform.offsetMin = Vector2.zero;
            lTMP.rectTransform.offsetMax = Vector2.zero;

            return btn.rectTransform;
        }

        static Color LightenColor(Color c, float amount) => new Color(
            Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount),
            Mathf.Clamp01(c.b + amount), c.a);

        // ── Low-level helpers ─────────────────────────────────────────────────

        static RectTransform MakeRT(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt  = MakeRT(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type   = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color  = color;
            return img;
        }

        static TextMeshProUGUI MakeTMP(string name, Transform parent, string text,
            float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var rt  = MakeRT(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (s_font != null) tmp.font = s_font;
            tmp.text               = text;
            tmp.fontSize           = size;
            tmp.fontStyle          = style;
            tmp.color              = color;
            tmp.alignment          = align;
            tmp.enableWordWrapping = true;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            return tmp;
        }

        // Stretch to fill parent
        static void StretchFill(Image img)
        {
            var rt      = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Add / update LayoutElement
        static void LE(RectTransform rt, float preferredHeight = -1f,
            float preferredWidth = -1f, float flexibleHeight = -1f)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>()
                  ?? rt.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
            if (flexibleHeight  >= 0) le.flexibleHeight  = flexibleHeight;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts   = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static Color Hex(uint h) => new Color(
            ((h >> 16) & 0xFF) / 255f,
            ((h >>  8) & 0xFF) / 255f,
            ( h        & 0xFF) / 255f, 1f);
    }
}
