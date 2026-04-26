using UnityEngine;

/// <summary>
/// ScriptableObject representing a single lifestyle/intent card.
/// Create via: Assets → Create → RoomRevive → Intent Data
/// </summary>
[CreateAssetMenu(fileName = "NewIntent", menuName = "RoomRevive/Intent Data")]
public class IntentData : ScriptableObject
{
    [Tooltip("Card title shown in bold — e.g. 'Calm & Unwind'")]
    public string title = "Intent Title";

    [Tooltip("Card subtitle — e.g. 'I want to relax and take my time'")]
    public string subtitle = "A short description";

    [Tooltip("Full-bleed card background image (portrait ratio works best, ~3:4)")]
    public Sprite cardImage;
}
