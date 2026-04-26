using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Meta XR/Intent Card Data", fileName = "IntentCardData")]
public class MetaRayIntentCardData : ScriptableObject
{
    [Header("Text")]
    public string title = "Intent";

    [TextArea(2, 4)]
    public string subtitle = "Short description.";

    [Header("Visual")]
    public Sprite cardImage;

    [Header("Splat Action")]
    [Tooltip("AutoByIndex means: card 0 = Calm, card 1 = Host, card 2 = Fast.")]
    public MetaRayIntentCardMenu.SplatCardAction splatAction =
        MetaRayIntentCardMenu.SplatCardAction.AutoByIndex;

    [Header("Fallback")]
    public bool useCustomFallbackColor;
    public Color fallbackColor = new Color(0.14f, 0.17f, 0.24f, 1f);

    [Header("Events")]
    public UnityEvent onSelected;
    public UnityEvent onConfirmed;
}
