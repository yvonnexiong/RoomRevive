using System;
using UnityEngine;

namespace RoomRevive.Onboarding
{
    [Serializable]
    public struct OnboardingOptionData
    {
        [Tooltip("Display label on the card or row")]
        public string label;

        [Tooltip("Secondary line below label — leave empty if none (caption area shrinks automatically)")]
        public string subtitle;

        [Tooltip("Exact value emitted to Selection Core — case and space sensitive")]
        public string value;

        [Tooltip("Asset name inside Assets/Onboarding/Images/ without extension. Empty for text rows.")]
        public string imageName;

        public bool HasSubtitle => !string.IsNullOrEmpty(subtitle);
        public bool IsImageCard => !string.IsNullOrEmpty(imageName);
    }
}
