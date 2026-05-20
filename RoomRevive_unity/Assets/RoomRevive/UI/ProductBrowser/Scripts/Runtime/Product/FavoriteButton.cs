using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// View-only favorite toggle button. Fires <see cref="onClicked"/> on click or Enter
    /// and exposes <see cref="SetFavorited"/> so an external owner (the
    /// <see cref="ProductBrowserController"/>) can drive the label + color.
    ///
    /// No FavoritesManager / PlayerPrefs knowledge lives here — the owner decides
    /// what a click means and pushes the resulting visual state back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class FavoriteButton : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Auto-grabbed from this GameObject if empty.")]
        public Button button;
        [Tooltip("Label that flips between 'Add to favorites' and 'Favorited'. Auto-grabbed from children if empty.")]
        public TextMeshProUGUI label;

        [Header("Colors")]
        public Color notFavoritedColor = new Color(0.227f, 0.251f, 0.333f, 1f);
        public Color favoritedColor    = new Color(0.22f,  0.55f,  0.38f,  1f);

        [Header("Labels")]
        public string notFavoritedText = "Add to favorites";
        public string favoritedText    = "Favorited";

        [Header("Input")]
        [Tooltip("If true, Enter / KeypadEnter also fires the click event while this button is enabled.")]
        public bool listenForEnterKey = true;

        [Header("Events")]
        [Tooltip("Fired on every click / Enter — no matter what. The owner decides what to do.")]
        public UnityEvent onClicked = new UnityEvent();

        [Tooltip("Minimum seconds between fires. Suppresses double-fires from duplicate listeners (e.g. inspector + runtime).")]
        public float fireCooldown = 0.1f;
        float _lastFireTime = -1f;

        void Reset()
        {
            button = GetComponent<Button>();
            if (button != null) label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (button != null) button.onClick.AddListener(Fire);
        }

        void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Fire);
        }

        void Update()
        {
            if (!listenForEnterKey) return;
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
                Fire();
        }

        public void Fire()
        {
            float now = Time.unscaledTime;
            if (now - _lastFireTime < fireCooldown) return;
            _lastFireTime = now;

            Debug.Log("Ok");
            onClicked?.Invoke();
        }

        /// <summary>
        /// Drives the visual state. Owner calls this whenever the favorite flag changes
        /// (on click, on product change, on enable, etc.).
        /// </summary>
        public void SetFavorited(bool favorited)
        {
            if (label != null) label.text = favorited ? favoritedText : notFavoritedText;

            if (button != null && button.targetGraphic is Image img)
            {
                img.color = favorited ? favoritedColor : notFavoritedColor;
                button.targetGraphic.CrossFadeColor(Color.white, 0f, true, false);
            }
        }
    }
}
