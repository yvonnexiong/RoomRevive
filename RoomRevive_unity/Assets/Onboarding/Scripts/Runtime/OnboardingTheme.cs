using UnityEngine;
using TMPro;

namespace RoomRevive.Onboarding
{
    [CreateAssetMenu(menuName = "RoomRevive/Onboarding/Theme", fileName = "OnboardingTheme")]
    public class OnboardingTheme : ScriptableObject
    {
        [Header("Surface")]
        public Color surfaceBase  = new Color(0.710f, 0.737f, 0.816f, 1f); // #B5BCD0
        public Color surfaceLight = new Color(0.831f, 0.851f, 0.898f, 1f); // #D4D9E5
        public Color surfaceDeep  = new Color(0.486f, 0.522f, 0.627f, 1f); // #7C85A0
        public Color cardInner    = new Color(0.780f, 0.804f, 0.867f, 1f); // #C7CDDD

        [Header("Ink")]
        public Color inkPrimary   = new Color(0.227f, 0.251f, 0.333f, 1f); // #3A4055
        public Color inkSecondary = new Color(0.420f, 0.451f, 0.533f, 1f); // #6B7388

        [Header("Accent")]
        public Color teal         = new Color(0.482f, 0.659f, 0.769f, 1f); // #7BA8C4
        public Color amber        = new Color(0.902f, 0.647f, 0.376f, 1f); // #E6A560

        [Header("Typography")]
        [Tooltip("Alan Sans — import from Google Fonts and assign here. Falls back to TMP default if null.")]
        public TMP_FontAsset font;
        public float fontSizeTitle   = 22f;
        public float fontSizeBody    = 14f;
        public float fontSizeCaption = 11f;
        public float fontSizeButton  = 14f;

        [Header("Shape")]
        public float panelRadius  = 14f;
        public float cardRadius   = 10f;
        public float buttonRadius = 8f;
    }
}
