using UnityEngine;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Scene-resident singleton that persists per-product favorite flags via PlayerPrefs.
    ///
    /// Drop one of these on a GameObject in the scene (e.g. on the ProductBrowserUI root or its
    /// own "_FavoritesManager" GO). Replaces the old static <c>ProductFavorites</c> helper so
    /// callers can locate it through <see cref="Instance"/> rather than depending on a static type.
    ///
    /// Key format: "RR_Fav_{product.id}" — falls back to the SO asset name when id is blank.
    /// Data survives scene reloads and play-mode restarts on the same machine.
    /// </summary>
    [DisallowMultipleComponent]
    public class FavoritesManager : MonoBehaviour
    {
        public static FavoritesManager Instance { get; private set; }

        const string Prefix = "RR_Fav_";

        [Header("Debug")]
        [Tooltip("Log a line every time a favorite is toggled.")]
        public bool debugLogs = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────

        public bool IsFavorited(ProductData product)
        {
            if (product == null) return false;
            return PlayerPrefs.GetInt(Key(product), 0) == 1;
        }

        public void SetFavorited(ProductData product, bool favorited)
        {
            if (product == null) return;
            if (favorited) PlayerPrefs.SetInt(Key(product), 1);
            else           PlayerPrefs.DeleteKey(Key(product));
            PlayerPrefs.Save();

            if (debugLogs)
                Debug.Log($"[FavoritesManager] {(favorited ? "★" : "☆")} {SafeName(product)}", this);
        }

        public bool Toggle(ProductData product)
        {
            bool next = !IsFavorited(product);
            SetFavorited(product, next);
            return next;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static string Key(ProductData product) =>
            Prefix + (string.IsNullOrEmpty(product.id) ? product.name : product.id);

        static string SafeName(ProductData p) =>
            !string.IsNullOrEmpty(p.productName) ? p.productName : p.name;
    }
}
