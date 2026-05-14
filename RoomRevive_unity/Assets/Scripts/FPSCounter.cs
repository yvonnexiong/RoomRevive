using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private string previewText = "-- FPS";

    private TMP_Text fpsText;
    private float timer;
    private int frameCount;

    void Awake()
    {
        fpsText = GetComponentInChildren<TMP_Text>();
    }

    void Update()
    {
        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= 0.5f)
        {
            fpsText.text = $"{Mathf.RoundToInt(frameCount / timer)} FPS";
            frameCount = 0;
            timer = 0f;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var text = GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.text = previewText;
    }
#endif
}
