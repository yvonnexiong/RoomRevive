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
        const string SpritePath          = "Assets/Onboarding/Sprites/RoundedRect.png";
        const string ProgBarSpritePath   = "Assets/Onboarding/Sprites/ProgBar.png";
        const string BgGradientPath      = "Assets/Onboarding/Sprites/BgGradient.png";
        const string CheckmarkSpritePath = "Assets/Onboarding/Sprites/Checkmark.png";
        const string ReadyPrefabPath     = "Assets/Onboarding/Prefabs/ReadyUI.prefab";
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
            GenerateCheckmarkSprite();
            CreateThemeAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("[Onboarding] Phase 0 done — sprites and theme asset ready.");
        }

        static void GenerateRoundedRectSprite()
        {
            const int size = 64, radius = 16;
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

        // White checkmark on transparent background, 32×32 px.
        // Two anti-aliased line segments meeting at the bottom of the tick.
        static void GenerateCheckmarkSprite()
        {
            const int   size = 32;
            const float hw   = 2.0f; // half stroke width in pixels
            const float aa   = 0.9f; // anti-alias falloff

            // Checkmark points in pixel coords (Y = 0 at bottom of texture)
            // Short left arm: upper-left → bottom corner
            // Long right arm: bottom corner → upper-right
            float ax = 6f,  ay = 19f; // left tip
            float bx = 13f, by = 8f;  // bottom corner (the "V" dip)
            float cx = 27f, cy = 24f; // right tip

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d  = Mathf.Min(DistToSeg(px, py, ax, ay, bx, by),
                                     DistToSeg(px, py, bx, by, cx, cy));
                float a  = Mathf.Clamp01((hw + aa - d) / aa);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255 + 0.5f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(CheckmarkSpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(CheckmarkSpritePath);
            imp.textureType         = TextureImporterType.Sprite;
            imp.spriteImportMode    = SpriteImportMode.Single;
            imp.spriteBorder        = Vector4.zero; // no 9-slice — fixed icon
            imp.filterMode          = FilterMode.Bilinear;
            imp.alphaIsTransparency = true;
            imp.textureCompression  = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        // Signed distance from point (px,py) to the nearest point on segment (ax,ay)→(bx,by)
        static float DistToSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float lenSq = dx * dx + dy * dy;
            float t  = lenSq > 0f ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq) : 0f;
            float qx = ax + t * dx - px, qy = ay + t * dy - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
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

        // ── Phase 8a — Review page ("Personalizing your dream kitchen") ─────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 8a — Build Review Page (static)")]
        static void Phase8a()
        {
            var roundRect  = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var bgGradient = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            var checkmark  = AssetDatabase.LoadAssetAtPath<Sprite>(CheckmarkSpritePath);
            if (roundRect == null) { Debug.LogError("[Onboarding] Run Phase 0 first."); return; }
            if (checkmark == null) Debug.LogWarning("[Onboarding] Checkmark sprite missing — re-run Phase 0.");
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { Debug.LogError("[Onboarding] Run Phase 1 first."); return; }

            var existing = root.transform.Find("ReviewPanel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            BuildReviewPage(root.transform, roundRect, bgGradient ?? roundRect, checkmark);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[Onboarding] Phase 8a — ReviewPanel built. Enable it in the Hierarchy to inspect.");
        }

        // ── Phase 8b — Standalone ReadyUI canvas ─────────────────────────────

        [MenuItem("Tools/RoomRevive/Onboarding/Phase 8b — Build Ready Page (static)")]
        static void Phase8b()
        {
            var roundRect  = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var bgGradient = AssetDatabase.LoadAssetAtPath<Sprite>(BgGradientPath);
            if (roundRect == null) { Debug.LogError("[Onboarding] Run Phase 0 first."); return; }
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // ── Build a standalone World Space canvas (same dims as OnboardingFlowUI) ──
            EnsureFolder("Assets/Onboarding/Prefabs");
            var root   = new GameObject("ReadyUI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<GraphicRaycaster>();
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(480f, 780f);
            root.transform.localScale = Vector3.one * 0.001f;

            BuildReadyPage(root.transform, roundRect, bgGradient ?? roundRect);

            PrefabUtility.SaveAsPrefabAsset(root, ReadyPrefabPath);
            Object.DestroyImmediate(root);

            // ── Strip any leftover ReadyPanel from OnboardingFlowUI ───────────
            var flowRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (flowRoot != null)
            {
                var stale = flowRoot.transform.Find("ReadyPanel");
                if (stale != null)
                {
                    Object.DestroyImmediate(stale.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(flowRoot, PrefabPath);
                }
                PrefabUtility.UnloadPrefabContents(flowRoot);
            }

            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ReadyPrefabPath);
            Debug.Log("[Onboarding] Phase 8b — ReadyUI.prefab built as standalone canvas. " +
                      "Add it to the scene at the same position as OnboardingFlowUI, " +
                      "then drag it into the FlowController's 'Ready UI' field.");
        }

        // ── Review page builder ───────────────────────────────────────────────

        static void BuildReviewPage(Transform root, Sprite roundRect, Sprite bgGradient, Sprite checkmark)
        {
            var panel = MakeRT("ReviewPanel", root);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.SetActive(false);

            // childControlHeight = true so the VLG sizes children in one coherent pass.
            // ContentSizeFitter inside a non-controlling VLG runs AFTER positioning,
            // causing children to be placed at wrong Y before their heights are known.
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(20, 20, 16, 16);

            // Banner: fixed height via LE so the outer VLG can position it correctly.
            // ContentSizeFitter removed — it runs after VLG positioning and causes overlap.
            // Height = top pad(14) + title ~2 lines(62) + spacing(4) + subtitle(15) + bot pad(10) ≈ 105; +11 buffer.
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            LE(banner.rectTransform, preferredHeight: 116f);
            var bannerVlg = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVlg.childAlignment       = TextAnchor.UpperCenter;
            bannerVlg.childControlWidth    = true;
            bannerVlg.childControlHeight   = true;
            bannerVlg.childForceExpandWidth  = true;
            bannerVlg.childForceExpandHeight = false;
            bannerVlg.spacing = 4f;
            bannerVlg.padding = new RectOffset(16, 16, 14, 10);

            var titleTmp = MakeTMP("Title", banner.rectTransform,
                "Personalizing your dream kitchen",
                26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);
            titleTmp.enableWordWrapping = true;

            var subtitleTmp = MakeTMP("Subtitle", banner.rectTransform,
                "Matching your choices to the perfect pieces",
                12f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);

            // Rows container: fixed height = 4 rows×56 + progbar×5 + 4 gaps×10 = 269px.
            // No NavBar below — ReviewPanel is animation-only, no user action.
            // ContentSizeFitter removed for same reason as Banner above.
            var rowsRT = MakeRT("RowsArea", panel);
            LE(rowsRT, preferredHeight: 269f);
            var rowsVlg = rowsRT.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsVlg.childAlignment       = TextAnchor.UpperLeft;
            rowsVlg.childControlWidth    = true;
            rowsVlg.childControlHeight   = true;
            rowsVlg.childForceExpandWidth  = true;
            rowsVlg.childForceExpandHeight = false;
            rowsVlg.spacing = 10f;

            // Category labels (matching HTML `order` array)
            string[] cats = { "STYLE", "PALETTE", "COOKING FOR", "INVESTMENT" };
            var rowValueTmps    = new TextMeshProUGUI[4];
            var rowGroups       = new CanvasGroup[4];
            var rowRTs          = new RectTransform[4];
            var checkGroups     = new CanvasGroup[4];
            var checkTransforms = new Transform[4];

            for (int i = 0; i < 4; i++)
            {
                // Row card: CardInner background, horizontal layout
                var row = MakeImage($"Row{i + 1}", rowsRT, roundRect, CardInner);
                LE(row.rectTransform, preferredHeight: 56f);

                var rowCG = row.gameObject.AddComponent<CanvasGroup>();
                rowCG.alpha = 0f;
                rowGroups[i] = rowCG;
                rowRTs[i]    = row.rectTransform;

                var rowHlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowHlg.childAlignment        = TextAnchor.MiddleLeft;
                rowHlg.childControlWidth     = true;
                rowHlg.childControlHeight    = true;
                rowHlg.childForceExpandWidth  = false;
                rowHlg.childForceExpandHeight = false;
                rowHlg.spacing  = 12f;
                rowHlg.padding  = new RectOffset(16, 16, 13, 13);

                // Category label — fixed 96 px, small caps feel via all-caps text
                var catTmp = MakeTMP("Category", row.rectTransform,
                    cats[i], 11f, FontStyles.Bold, InkSecondary, TextAlignmentOptions.Left);
                catTmp.enableWordWrapping = false;
                var catLE = catTmp.gameObject.AddComponent<LayoutElement>();
                catLE.minWidth = 96f; catLE.preferredWidth = 96f;

                // Value — fills remaining width
                var valTmp = MakeTMP("Value", row.rectTransform,
                    "—", 15f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Left);
                valTmp.enableWordWrapping = false;
                valTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                rowValueTmps[i] = valTmp;

                // Checkmark circle (InkPrimary pill, 9-sliced for roundness at 22 px)
                var ck = MakeImage("Check", row.rectTransform, roundRect, InkPrimary);
                var ckLE = ck.gameObject.AddComponent<LayoutElement>();
                ckLE.minWidth = 22f; ckLE.minHeight = 22f;
                ckLE.preferredWidth = 22f; ckLE.preferredHeight = 22f;

                var ckCG = ck.gameObject.AddComponent<CanvasGroup>();
                ckCG.alpha = 0f;
                ck.transform.localScale = Vector3.one * 0.4f;
                checkGroups[i]     = ckCG;
                checkTransforms[i] = ck.transform;

                // Checkmark sprite — white icon inset 4 px inside the circle
                if (checkmark != null)
                {
                    var ckMark = MakeImage("CheckMark", ck.rectTransform, checkmark, Color.white);
                    ckMark.type = Image.Type.Simple;
                    ckMark.preserveAspect = false;
                    var ckMarkRT = ckMark.rectTransform;
                    ckMarkRT.anchorMin = Vector2.zero;
                    ckMarkRT.anchorMax = Vector2.one;
                    ckMarkRT.offsetMin = new Vector2(4f, 4f);
                    ckMarkRT.offsetMax = new Vector2(-4f, -4f);
                }
            }

            // Progress fill bar — 2px rounded corners using the dedicated ProgBar sprite.
            // Fill uses Sliced type + anchorMax.x animation so corners stay round at all fill levels.
            var progBar = AssetDatabase.LoadAssetAtPath<Sprite>(ProgBarSpritePath);

            var progWrapper = MakeRT("ProgressBar", rowsRT);
            progWrapper.gameObject.AddComponent<LayoutElement>().preferredHeight = 5f;

            var progBg = MakeImage("ProgressBg", progWrapper, progBar,
                new Color(SurfaceDeep.r, SurfaceDeep.g, SurfaceDeep.b, 0.30f));
            StretchFill(progBg);
            progBg.type = Image.Type.Sliced;

            var progFill = MakeImage("ProgressFill", progWrapper, progBar, InkPrimary);
            progFill.type = Image.Type.Sliced;
            // Left-anchored: animate anchorMax.x 0→1 to reveal fill while keeping sliced corners
            var pfRT = progFill.rectTransform;
            pfRT.anchorMin = Vector2.zero;
            pfRT.anchorMax = new Vector2(0f, 1f);
            pfRT.offsetMin = Vector2.zero;
            pfRT.offsetMax = Vector2.zero;

            // No NavBar — user takes no action during the review animation.

            // Wire controller
            var ctrl = panel.gameObject.AddComponent<OnboardingReviewController>();
            ctrl.Setup(pfRT, titleTmp, subtitleTmp,
                rowGroups, checkGroups, checkTransforms, rowValueTmps);
        }

        // ── Ready page builder ────────────────────────────────────────────────

        static void BuildReadyPage(Transform root, Sprite roundRect, Sprite bgGradient)
        {
            var panel = MakeRT("ReadyPanel", root);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            // Active by default — this is the only panel in its own canvas.

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(20, 20, 16, 16);

            // Banner
            var banner = MakeImage("Banner", panel, roundRect,
                new Color(SurfaceLight.r, SurfaceLight.g, SurfaceLight.b, 0.80f));
            var bannerVlg = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            bannerVlg.childAlignment       = TextAnchor.UpperCenter;
            bannerVlg.childControlWidth    = true;
            bannerVlg.childControlHeight   = true;
            bannerVlg.childForceExpandWidth  = true;
            bannerVlg.childForceExpandHeight = false;
            bannerVlg.spacing = 4f;
            bannerVlg.padding = new RectOffset(16, 16, 14, 14);
            banner.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            MakeTMP("Title", banner.rectTransform,
                "Your kitchen is ready",
                26f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);

            MakeTMP("Subtitle", banner.rectTransform,
                "Here’s what we’ll bring into your room",
                12f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Center);

            // Changes card
            var card = MakeImage("ChangesCard", panel, roundRect, CardInner);
            var cardVlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardVlg.childAlignment       = TextAnchor.UpperLeft;
            cardVlg.childControlWidth    = true;
            cardVlg.childControlHeight   = true;
            cardVlg.childForceExpandWidth  = true;
            cardVlg.childForceExpandHeight = false;
            cardVlg.spacing = 0f;
            cardVlg.padding = new RectOffset(18, 18, 18, 16);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            // Eyebrow
            var eyebrow = MakeTMP("Eyebrow", card.rectTransform,
                "IN THIS ROOM", 11f, FontStyles.Bold, InkSecondary, TextAlignmentOptions.Left);
            eyebrow.characterSpacing = 60f; // approximates CSS letter-spacing:.06em at 11px
            LE(eyebrow.rectTransform, preferredHeight: 28f);

            // 7 product rows — category (left) + product name (right)
            (string cat, string product)[] items =
            {
                ("Kitchen",        "TOUCH 337"),
                ("Fridge",         "KDN 4174 E Active"),
                ("Cooktop",        "CS 7612 FL"),
                ("Hood",           "DA 1260"),
                ("Microwave",      "M 2224 SC"),
                ("Dishwasher",     "G 5540 SCU SL Active"),
                ("Coffee machine", "CM 5310 Silence"),
            };

            var rowGroups = new CanvasGroup[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                // 1px separator above every row except the first (matches CSS border-top)
                if (i > 0)
                {
                    var sep = MakeRT($"Sep{i}", card.rectTransform);
                    sep.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
                    var sepImg = sep.gameObject.AddComponent<Image>();
                    sepImg.color = new Color(InkPrimary.r, InkPrimary.g, InkPrimary.b, 0.10f);
                }

                var row = MakeRT($"ProductRow{i + 1}", card.rectTransform);
                LE(row, preferredHeight: 40f);

                var rowCG = row.gameObject.AddComponent<CanvasGroup>();
                rowCG.alpha = 0f;
                rowGroups[i] = rowCG;

                var rowHlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowHlg.childAlignment        = TextAnchor.MiddleLeft;
                rowHlg.childControlWidth     = true;
                rowHlg.childControlHeight    = true;
                rowHlg.childForceExpandWidth  = false;
                rowHlg.childForceExpandHeight = true;
                rowHlg.spacing  = 12f;
                rowHlg.padding  = new RectOffset(0, 0, 10, 10);

                var catTmp = MakeTMP("Category", row,
                    items[i].cat, 13f, FontStyles.Normal, InkSecondary, TextAlignmentOptions.Left);
                catTmp.enableWordWrapping = false;

                var valTmp = MakeTMP("Product", row,
                    items[i].product, 13f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Right);
                valTmp.enableWordWrapping = false;
                valTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }

            // "Transforming your kitchen ..." note — dots animated by controller
            var note = MakeTMP("Note", panel,
                "Transforming your kitchen .",
                16f, FontStyles.Bold, InkPrimary, TextAlignmentOptions.Center);
            note.rectTransform.sizeDelta = new Vector2(0f, 50f);

            var ctrl = panel.gameObject.AddComponent<OnboardingReadyController>();
            ctrl.Setup(rowGroups, note);
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
