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

        // ── Phase 3 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 3 — Create Question Assets")]
        static void Phase3()
        {
            EnsureFolder("Assets/Onboarding/Data");

            CreateQuestionAsset("Assets/Onboarding/Data/Q1_Style.asset",
                prompt:       "Which style do you prefer?",
                stepLabel:    "1 of 4",
                imageCards:   true,
                new OnboardingOptionData { label = "Clean & Uncluttered", subtitle = "Modern",       value = "modern",                  imageName = "kitchen_style_1" },
                new OnboardingOptionData { label = "Bold & Dramatic",      subtitle = "Designer",     value = "designer",                imageName = "kitchen_style_2" },
                new OnboardingOptionData { label = "Warm & Cozy",          subtitle = "Cottage",      value = "cottage style",           imageName = "kitchen_style_3" },
                new OnboardingOptionData { label = "Calm & Natural",       subtitle = "Scandinavian", value = "natural & scandinavian",  imageName = "kitchen_style_4" }
            );

            // Q2: no subtitles — caption area will be shorter (44 px vs 56 px)
            CreateQuestionAsset("Assets/Onboarding/Data/Q2_Palette.asset",
                prompt:       "Which colours feel most like home?",
                stepLabel:    "2 of 4",
                imageCards:   true,
                new OnboardingOptionData { label = "Light & Airy",       subtitle = "", value = "light", imageName = "swatches_stack_1" },
                new OnboardingOptionData { label = "Dark & Moody",       subtitle = "", value = "dark",  imageName = "swatches_stack_2" },
                new OnboardingOptionData { label = "Warm Wood Tones",    subtitle = "", value = "wood",  imageName = "swatches_stack_3" },
                new OnboardingOptionData { label = "Colourful & Playful",subtitle = "", value = "bold",  imageName = "swatches_stack_4" }
            );

            CreateQuestionAsset("Assets/Onboarding/Data/Q3_Household.asset",
                prompt:       "How many are you usually cooking for?",
                stepLabel:    "3 of 4",
                imageCards:   false,
                new OnboardingOptionData { label = "1–2 people", subtitle = "", value = "compact"  },
                new OnboardingOptionData { label = "3–4 people", subtitle = "", value = "standard" },
                new OnboardingOptionData { label = "5+ people",  subtitle = "", value = "host"     }
            );

            CreateQuestionAsset("Assets/Onboarding/Data/Q4_Investment.asset",
                prompt:       "How much would you like to invest?",
                stepLabel:    "4 of 4",
                imageCards:   false,
                new OnboardingOptionData { label = "Essential", subtitle = "Affordable",        value = "Essential" },
                new OnboardingOptionData { label = "Signature", subtitle = "Mid-range",         value = "Signature" },
                new OnboardingOptionData { label = "Premium",   subtitle = "High-end",          value = "Premium"   },
                new OnboardingOptionData { label = "Show All",  subtitle = "Explore everything",value = "any"       }
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Onboarding] Phase 3 done — Q1–Q4 question assets created/updated.");
        }

        static void CreateQuestionAsset(string path, string prompt, string stepLabel,
            bool imageCards, params OnboardingOptionData[] options)
        {
            // Always recreate so values stay in sync with this script
            AssetDatabase.DeleteAsset(path);
            var asset = ScriptableObject.CreateInstance<OnboardingQuestionData>();
            asset.prompt       = prompt;
            asset.stepLabel    = stepLabel;
            asset.useImageCards = imageCards;
            asset.options      = new System.Collections.Generic.List<OnboardingOptionData>(options);
            AssetDatabase.CreateAsset(asset, path);
        }

        // ── Phase 4 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 4 — Build Q2 Page")]
        static void Phase4()
        {
            var roundRect    = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var progBarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProgBarSpritePath) ?? roundRect;
            var bgGradient   = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            if (roundRect == null) { Debug.LogError("[Onboarding] Run Phase 0 first."); return; }
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var q2Data = AssetDatabase.LoadAssetAtPath<OnboardingQuestionData>("Assets/Onboarding/Data/Q2_Palette.asset");
            if (q2Data == null) { Debug.LogError("[Onboarding] Run Phase 3 first to create Q2_Palette.asset."); return; }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[Onboarding] Run Phase 1 first to create the prefab."); return; }

            // Rename existing Panel → Q1Panel so sibling naming is unambiguous
            var q1Panel = root.transform.Find("Panel");
            if (q1Panel != null) q1Panel.name = "Q1Panel";

            // Remove old Q2Panel if rebuilding
            var old2 = root.transform.Find("Q2Panel");
            if (old2 != null) Object.DestroyImmediate(old2.gameObject);

            // Build Q2 page — identical layout to Q1; caption height auto-shrinks to
            // 44 px because Q2 options have no subtitles (handled in BuildImageCard)
            BuildImagePage(root.transform, "Q2Panel", q2Data,
                roundRect, progBarSprite, bgGradient,
                activeSegments: 2, isFirstPage: false, hidden: true);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[Onboarding] Phase 4 done — Q2Panel added (hidden). Enable it in the Inspector to test.");
        }

        // Builds a complete image-card question page as a child of parent.
        // activeSegments: how many progress segments are lit (1 = Q1, 2 = Q2, …)
        // hidden: SetActive(false) after building so it waits for Phase 7 navigation
        static void BuildImagePage(Transform parent, string panelName,
            OnboardingQuestionData data, Sprite roundRect, Sprite progBarSprite,
            Sprite bgGradient, int activeSegments, bool isFirstPage, bool hidden)
        {
            var panel = MakeRT(panelName, parent);
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

            // Background (ignoreLayout so VL skips it)
            if (bgGradient != null)
            {
                var bg = MakeImage("Background", panel, bgGradient, Color.white);
                bg.type           = Image.Type.Simple;
                bg.preserveAspect = false;
                bg.raycastTarget  = false;
                bg.rectTransform.anchorMin = Vector2.zero;
                bg.rectTransform.anchorMax = Vector2.one;
                bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
                bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            // Progress bar
            var progressWrapper = MakeRT("ProgressBar", panel);
            LE(progressWrapper, preferredHeight: 4f);
            var segments = MakeRT("Segments", progressWrapper);
            segments.anchorMin = Vector2.zero;
            segments.anchorMax = Vector2.one;
            segments.offsetMin = segments.offsetMax = Vector2.zero;
            var pHL = segments.gameObject.AddComponent<HorizontalLayoutGroup>();
            pHL.spacing              = 6f;
            pHL.childForceExpandWidth  = true;
            pHL.childForceExpandHeight = true;
            pHL.childControlWidth      = true;
            pHL.childControlHeight     = true;
            for (int i = 0; i < 4; i++)
            {
                bool active = i < activeSegments;
                var seg = MakeImage($"Seg{i + 1}", segments, progBarSprite,
                    active ? InkPrimary : new Color(InkPrimary.r, InkPrimary.g, InkPrimary.b, 0.25f));
                seg.type = Image.Type.Sliced;
            }

            // Banner
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            banner.type = Image.Type.Sliced;
            // No fixed banner height — flexes so 2-line prompts wrap without clipping
            var bannerVL = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVL.padding              = new RectOffset(16, 16, 14, 10);
            bannerVL.spacing              = 4f;
            bannerVL.childAlignment       = TextAnchor.MiddleCenter;
            bannerVL.childForceExpandWidth  = true;
            bannerVL.childForceExpandHeight = false;
            bannerVL.childControlWidth      = true;
            bannerVL.childControlHeight     = true;
            // No fixed title height — TMP reports its wrapped preferred height to the VLG
            var titleTMP = MakeTMP("Title", banner.transform, data.prompt,
                26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);
            var subTMP = MakeTMP("Subtitle", banner.transform, data.stepLabel,
                12f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);
            LE(subTMP.rectTransform, preferredHeight: 18f);

            // Card grid (2×2)
            // Cell height keeps photo square (211×211): 211 photo + captionH + 4 inset
            bool anySubtitle  = data.options.Exists(o => o.HasSubtitle);
            float pgCaptionH  = anySubtitle ? 56f : 44f;
            float pgCellH     = 211f + pgCaptionH + 4f; // = 271 with subtitle, 259 without
            float pgGridH     = pgCellH * 2f + 12f;     // two rows + gap
            var gridRT = MakeRT("CardGrid", panel);
            LE(gridRT, preferredHeight: pgGridH);
            var grid             = gridRT.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(215f, pgCellH);
            grid.spacing         = new Vector2(12f, 12f);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment  = TextAnchor.UpperLeft;

            var cardViews = new OnboardingOptionCardView[data.options.Count];
            var values    = new string[data.options.Count];
            for (int i = 0; i < data.options.Count; i++)
            {
                var opt    = data.options[i];
                cardViews[i] = BuildImageCard(gridRT, roundRect, opt.imageName, opt.label, opt.subtitle);
                values[i]    = opt.value;
            }

            // Nav bar
            var navRT = MakeRT("NavBar", panel);
            LE(navRT, preferredHeight: 44f);

            var backBtn = BuildButton(navRT, roundRect, "BackButton", "Back",
                SurfaceLight, InkPrimary, alpha: 1f);
            backBtn.anchorMin        = new Vector2(0f, 0.5f);
            backBtn.anchorMax        = new Vector2(0f, 0.5f);
            backBtn.pivot            = new Vector2(0f, 0.5f);
            backBtn.anchoredPosition = Vector2.zero;
            backBtn.sizeDelta        = new Vector2(120f, 44f);
            var backCG = backBtn.gameObject.AddComponent<CanvasGroup>();

            var nextBtn = BuildButton(navRT, roundRect, "NextButton", "Next",
                InkPrimary, BtnText, alpha: 1f);
            nextBtn.anchorMin = new Vector2(0f, 0.5f);
            nextBtn.anchorMax = new Vector2(1f, 0.5f);
            nextBtn.pivot     = new Vector2(1f, 0.5f);
            nextBtn.offsetMin = new Vector2(132f, -22f);
            nextBtn.offsetMax = new Vector2(0f,   22f);
            var nextCG = nextBtn.gameObject.AddComponent<CanvasGroup>();
            nextCG.alpha = 0.45f;

            // Controller
            var controller = panel.gameObject.AddComponent<OnboardingImagePageController>();
            controller.Setup(cardViews, values,
                nextBtn.GetComponent<Button>(), nextBtn, nextCG, backCG, isFirstPage);

            if (hidden) panel.gameObject.SetActive(false);
        }

        // ── Phase 7 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 7 — Wire Navigation")]
        static void Phase7()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[Onboarding] Run Phase 1 first."); return; }

            // Ensure Q1Panel name is correct (in case Phase 1 was run before this fix)
            var oldPanel = root.transform.Find("Panel");
            if (oldPanel != null) oldPanel.name = "Q1Panel";

            // Drop FlowController on the root Canvas object — recreate if already present
            var existing = root.GetComponent<OnboardingFlowController>();
            if (existing != null) Object.DestroyImmediate(existing);
            root.AddComponent<OnboardingFlowController>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[Onboarding] Phase 7 done — OnboardingFlowController added to root. Press Play to test full flow.");
        }

        // ── Phase 8 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 8 — Build Review Page")]
        static void Phase8()
        {
            var roundRect  = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var bgGradient = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            if (roundRect == null) { Debug.LogError("[Onboarding] Run Phase 0 first."); return; }
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[Onboarding] Run Phase 1 first."); return; }

            var existing = root.transform.Find("ReviewPanel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            BuildReviewPage(root.transform, roundRect, bgGradient ?? roundRect);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[Onboarding] Phase 8 done — ReviewPanel added. Complete Q4 to test the review animation.");
        }

        static void BuildReviewPage(Transform root, Sprite roundRect, Sprite bgGradient)
        {
            var panel = MakeRT("ReviewPanel", root);
            panel.gameObject.SetActive(false);

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(20, 20, 16, 16);

            // Background (ignoreLayout, stretch-fill)
            var bg = MakeImage("Background", panel, bgGradient, Color.white);
            StretchFill(bg);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Progress fill bar (single fill image, not segmented)
            var progWrapper = MakeRT("ProgressWrapper", panel);
            progWrapper.gameObject.AddComponent<LayoutElement>().preferredHeight = 6f;

            var progBg = MakeImage("ProgressBg", progWrapper, roundRect,
                new Color(InkPrimary.r, InkPrimary.g, InkPrimary.b, 0.25f));
            StretchFill(progBg);

            var progFill = MakeImage("ProgressFill", progWrapper, roundRect, InkPrimary);
            StretchFill(progFill);
            progFill.type       = Image.Type.Filled;
            progFill.fillMethod = Image.FillMethod.Horizontal;
            progFill.fillAmount = 0f;

            // Banner
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            var bannerVlg = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVlg.childAlignment       = TextAnchor.UpperCenter;
            bannerVlg.childControlWidth    = true;
            bannerVlg.childControlHeight   = true;
            bannerVlg.childForceExpandWidth  = true;
            bannerVlg.childForceExpandHeight = false;
            bannerVlg.spacing = 6f;
            bannerVlg.padding = new RectOffset(16, 16, 14, 10);
            banner.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleTmp = MakeTMP("Title", banner.rectTransform,
                "Personalizing your dream kitchen",
                26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);

            var dotsTmp = MakeTMP("Dots", banner.rectTransform, "",
                18f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);

            var subtitleTmp = MakeTMP("Subtitle", banner.rectTransform,
                "Got it — finding your kitchen",
                12f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);
            subtitleTmp.gameObject.SetActive(false);

            // Summary card — rows reveal staggered on entry
            var card = MakeImage("SummaryCard", panel, roundRect, CardInner);
            var cardVlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardVlg.childAlignment       = TextAnchor.UpperLeft;
            cardVlg.childControlWidth    = true;
            cardVlg.childControlHeight   = false;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.spacing = 10f;
            cardVlg.padding = new RectOffset(20, 20, 16, 16);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            string[] categories = { "Style", "Colour", "Household", "Investment" };
            var rowGroups    = new CanvasGroup[4];
            var rowValueTmps = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                var row = MakeRT($"Row{i + 1}", card.rectTransform);
                LE(row, preferredHeight: 44f);
                var cg = row.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                rowGroups[i] = cg;

                var rowVlg = row.gameObject.AddComponent<VerticalLayoutGroup>();
                rowVlg.childAlignment       = TextAnchor.MiddleLeft;
                rowVlg.childControlWidth    = true;
                rowVlg.childControlHeight   = true;
                rowVlg.childForceExpandWidth  = true;
                rowVlg.childForceExpandHeight = false;
                rowVlg.spacing = 2f;
                rowVlg.padding = new RectOffset(4, 0, 0, 0);

                MakeTMP("Category", row, categories[i],
                    11f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Left);

                var val = MakeTMP("Value", row, "—",
                    15f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Left);
                rowValueTmps[i] = val;
            }

            var ctrl = panel.gameObject.AddComponent<OnboardingReviewController>();
            ctrl.Setup(progFill, titleTmp, dotsTmp, subtitleTmp, rowGroups, rowValueTmps);
        }

        // ── Phase 6 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 6 — Build Q4 Page")]
        static void Phase6()
        {
            var roundRect     = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var progBarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProgBarSpritePath) ?? roundRect;
            var bgGradient    = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            if (roundRect == null) { Debug.LogError("[Onboarding] Run Phase 0 first."); return; }
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var q4Data = AssetDatabase.LoadAssetAtPath<OnboardingQuestionData>("Assets/Onboarding/Data/Q4_Investment.asset");
            if (q4Data == null) { Debug.LogError("[Onboarding] Run Phase 3 first."); return; }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[Onboarding] Run Phase 1 first."); return; }

            var old4 = root.transform.Find("Q4Panel");
            if (old4 != null) Object.DestroyImmediate(old4.gameObject);

            BuildTextRowPage(root.transform, "Q4Panel", q4Data,
                roundRect, progBarSprite, bgGradient,
                activeSegments: 4, isFirstPage: false,
                nextLabel: "See my kitchen", hidden: true);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[Onboarding] Phase 6 done — Q4Panel added (hidden). CTA reads 'See my kitchen'.");
        }

        // ── Phase 5 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 5 — Build Q3 Page")]
        static void Phase5()
        {
            var roundRect     = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var progBarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProgBarSpritePath) ?? roundRect;
            var bgGradient    = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            if (roundRect == null) { Debug.LogError("[Onboarding] Run Phase 0 first."); return; }
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var q3Data = AssetDatabase.LoadAssetAtPath<OnboardingQuestionData>("Assets/Onboarding/Data/Q3_Household.asset");
            if (q3Data == null) { Debug.LogError("[Onboarding] Run Phase 3 first."); return; }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[Onboarding] Run Phase 1 first."); return; }

            var old3 = root.transform.Find("Q3Panel");
            if (old3 != null) Object.DestroyImmediate(old3.gameObject);

            BuildTextRowPage(root.transform, "Q3Panel", q3Data,
                roundRect, progBarSprite, bgGradient,
                activeSegments: 3, isFirstPage: false, nextLabel: "Next", hidden: true);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[Onboarding] Phase 5 done — Q3Panel added (hidden).");
        }

        // Builds a complete text-row question page (Q3 / Q4).
        // nextLabel: defaults to "Next"; pass "See my kitchen" for Q4.
        static void BuildTextRowPage(Transform parent, string panelName,
            OnboardingQuestionData data, Sprite roundRect, Sprite progBarSprite,
            Sprite bgGradient, int activeSegments, bool isFirstPage,
            string nextLabel, bool hidden)
        {
            var panel = MakeRT(panelName, parent);
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

            // Background
            if (bgGradient != null)
            {
                var bg = MakeImage("Background", panel, bgGradient, Color.white);
                bg.type           = Image.Type.Simple;
                bg.preserveAspect = false;
                bg.raycastTarget  = false;
                bg.rectTransform.anchorMin = Vector2.zero;
                bg.rectTransform.anchorMax = Vector2.one;
                bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
                bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            // Progress bar
            var progressWrapper = MakeRT("ProgressBar", panel);
            LE(progressWrapper, preferredHeight: 4f);
            var segments = MakeRT("Segments", progressWrapper);
            segments.anchorMin = Vector2.zero;
            segments.anchorMax = Vector2.one;
            segments.offsetMin = segments.offsetMax = Vector2.zero;
            var pHL = segments.gameObject.AddComponent<HorizontalLayoutGroup>();
            pHL.spacing              = 6f;
            pHL.childForceExpandWidth  = true;
            pHL.childForceExpandHeight = true;
            pHL.childControlWidth      = true;
            pHL.childControlHeight     = true;
            for (int i = 0; i < 4; i++)
            {
                bool active = i < activeSegments;
                var seg = MakeImage($"Seg{i + 1}", segments, progBarSprite,
                    active ? InkPrimary : new Color(InkPrimary.r, InkPrimary.g, InkPrimary.b, 0.25f));
                seg.type = Image.Type.Sliced;
            }

            // Banner (flexible height — wraps if prompt is long)
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            banner.type = Image.Type.Sliced;
            var bannerVL = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVL.padding              = new RectOffset(16, 16, 14, 10);
            bannerVL.spacing              = 4f;
            bannerVL.childAlignment       = TextAnchor.MiddleCenter;
            bannerVL.childForceExpandWidth  = true;
            bannerVL.childForceExpandHeight = false;
            bannerVL.childControlWidth      = true;
            bannerVL.childControlHeight     = true;
            MakeTMP("Title", banner.transform, data.prompt,
                26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);
            var subTMP = MakeTMP("Subtitle", banner.transform, data.stepLabel,
                12f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);
            LE(subTMP.rectTransform, preferredHeight: 18f);

            // Row list
            float rowH     = 56f;
            float rowGap   = 10f;
            float rowListH = data.options.Count * rowH +
                             Mathf.Max(0, data.options.Count - 1) * rowGap;
            var rowListRT = MakeRT("RowList", panel);
            LE(rowListRT, preferredHeight: rowListH);
            var rowListVL = rowListRT.gameObject.AddComponent<VerticalLayoutGroup>();
            rowListVL.spacing              = rowGap;
            rowListVL.childAlignment       = TextAnchor.UpperCenter;
            rowListVL.childForceExpandWidth  = true;
            rowListVL.childForceExpandHeight = false;
            rowListVL.childControlWidth      = true;
            rowListVL.childControlHeight     = true;

            var rowViews = new OnboardingTextRowView[data.options.Count];
            var values   = new string[data.options.Count];
            for (int i = 0; i < data.options.Count; i++)
            {
                var opt    = data.options[i];
                rowViews[i] = BuildTextRow(rowListRT, roundRect, opt.label, opt.subtitle);
                values[i]   = opt.value;
            }

            // Nav bar
            var navRT = MakeRT("NavBar", panel);
            LE(navRT, preferredHeight: 44f);

            var backBtn = BuildButton(navRT, roundRect, "BackButton", "Back",
                SurfaceLight, InkPrimary, alpha: 1f);
            backBtn.anchorMin        = new Vector2(0f, 0.5f);
            backBtn.anchorMax        = new Vector2(0f, 0.5f);
            backBtn.pivot            = new Vector2(0f, 0.5f);
            backBtn.anchoredPosition = Vector2.zero;
            backBtn.sizeDelta        = new Vector2(120f, 44f);
            var backCG = backBtn.gameObject.AddComponent<CanvasGroup>();

            var nextBtn = BuildButton(navRT, roundRect, "NextButton", nextLabel,
                InkPrimary, BtnText, alpha: 1f);
            nextBtn.anchorMin = new Vector2(0f, 0.5f);
            nextBtn.anchorMax = new Vector2(1f, 0.5f);
            nextBtn.pivot     = new Vector2(1f, 0.5f);
            nextBtn.offsetMin = new Vector2(132f, -22f);
            nextBtn.offsetMax = new Vector2(0f,   22f);
            var nextCG = nextBtn.gameObject.AddComponent<CanvasGroup>();
            nextCG.alpha = 0.45f;

            // Controller
            var controller = panel.gameObject.AddComponent<OnboardingTextPageController>();
            controller.Setup(rowViews, values,
                nextBtn.GetComponent<Button>(), nextCG, backCG, isFirstPage, nextLabel);

            if (hidden) panel.gameObject.SetActive(false);
        }

        // ── Text row builder ──────────────────────────────────────────────────
        // Structure: RowWrapper (56 px LE)
        //   Ring    — roundRect, CardInner / InkPrimary selected, stretch fill
        //   RowBody — roundRect, CardInner, inset 2 px, Mask+Button
        //     Content — stretch fill minus 20 px horizontal padding, VLG centred
        //       Label TMP (15 px Bold)
        //       Sub TMP   (12 px Normal, hidden if empty)

        static OnboardingTextRowView BuildTextRow(RectTransform parent, Sprite roundRect,
            string label, string subtitle)
        {
            var row = MakeRT(label.Split(' ')[0].TrimEnd('–', '-') + "Row", parent);
            LE(row, preferredHeight: 56f);

            var ring = MakeImage("Ring", row, roundRect, CardInner);
            ring.type = Image.Type.Sliced;
            ring.rectTransform.anchorMin = Vector2.zero;
            ring.rectTransform.anchorMax = Vector2.one;
            ring.rectTransform.offsetMin = Vector2.zero;
            ring.rectTransform.offsetMax = Vector2.zero;

            var rowBody = MakeImage("RowBody", row, roundRect, CardInner);
            rowBody.type = Image.Type.Sliced;
            rowBody.rectTransform.anchorMin = Vector2.zero;
            rowBody.rectTransform.anchorMax = Vector2.one;
            rowBody.rectTransform.offsetMin = new Vector2(2f, 2f);
            rowBody.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var rowMask = rowBody.gameObject.AddComponent<Mask>();
            rowMask.showMaskGraphic = true;
            rowBody.gameObject.AddComponent<Button>();

            // Content area: fills RowBody with 20 px left/right inset; VLG centres vertically
            var content = MakeRT("Content", rowBody.transform);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(20f, 0f);
            content.offsetMax = new Vector2(-20f, 0f);
            var contentVL = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentVL.spacing              = 2f;
            contentVL.childAlignment       = TextAnchor.MiddleCenter;
            contentVL.childForceExpandWidth  = true;
            contentVL.childForceExpandHeight = false;
            contentVL.childControlWidth      = true;
            contentVL.childControlHeight     = true;

            var lTMP = MakeTMP("Label", content, label, 15f, FontStyles.Bold,
                InkPrimary, TextAlignmentOptions.Center);
            LE(lTMP.rectTransform, preferredHeight: 22f);

            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            var sTMP = MakeTMP("Sub", content, subtitle, 12f, FontStyles.Normal,
                InkSecondary, TextAlignmentOptions.Center);
            LE(sTMP.rectTransform, preferredHeight: 17f);
            sTMP.gameObject.SetActive(hasSubtitle);

            var view = row.gameObject.AddComponent<OnboardingTextRowView>();
            view.Init(ring, rowBody, lTMP, sTMP);

            var proxy = rowBody.gameObject.AddComponent<OnboardingTextRowInteractionProxy>();
            proxy.Init(view);

            return view;
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

            // ── Q1 Panel ──────────────────────────────────────────────────────
            var panel = MakeRT("Q1Panel", root.transform);
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
                    i == 0 ? InkPrimary : new Color(InkPrimary.r, InkPrimary.g, InkPrimary.b, 0.25f));
                seg.type = Image.Type.Sliced;
            }

            // ── Banner ────────────────────────────────────────────────────────
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            banner.type = Image.Type.Sliced;
            // No fixed banner height — flexes with prompt length
            var bannerVL = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVL.padding              = new RectOffset(16, 16, 14, 10);
            bannerVL.spacing              = 4f;
            bannerVL.childAlignment       = TextAnchor.MiddleCenter;
            bannerVL.childForceExpandWidth  = true;
            bannerVL.childForceExpandHeight = false;
            bannerVL.childControlWidth      = true;
            bannerVL.childControlHeight     = true;

            // No fixed title height — wraps to 2 lines if prompt is long
            MakeTMP("Title", banner.transform,
                "Which style do you prefer?", 26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);

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
                nextBtn.GetComponent<Button>(), nextBtn, nextCG, backCG,
                values: new[] { "modern", "designer", "cottage style", "natural & scandinavian" });

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

            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            // 56 px with subtitle (label 22 + sub 17 + pad 10+12 − spacing rounding)
            // 44 px without (label 22 + pad 10+12 only)
            float captionH = hasSubtitle ? 56f : 44f;

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
            sTMP.gameObject.SetActive(hasSubtitle);

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
