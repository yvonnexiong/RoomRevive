using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Owns the browser state (Hidden / Discover / Swap) and the current product selection.
    /// Has no UI hierarchy references and no scene side-effect logic — those live in
    /// <see cref="ProductBrowserView"/> and the routers.
    ///
    /// Typical scene setup:
    ///   ProductBrowserUI (this + ProductBrowserView)
    ///     ├── DiscoverPanel  (ProductDiscoverView)
    ///     └── SwapPanel      (ProductSwapView)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProductBrowserController : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("Root view that owns the Discover and Swap panels. Auto-found on same GameObject if empty.")]
        public ProductBrowserView view;

        [Tooltip("Favorite toggle on the Swap panel. Auto-found inside the swap panel if empty. The controller subscribes to its onClicked event and drives FavoritesManager + the button's visual state.")]
        public FavoriteButton favoriteButton;

        [Header("Categories")]
        [Tooltip("Hotspots call OpenDiscover(fridgesCategory) / OpenDiscover(cabinetsCategory) etc.")]
        public ProductCategoryData fridgesCategory;
        public ProductCategoryData cabinetsCategory;
        public ProductCategoryData lightsCategory;

        [Header("Initial State")]
        [Tooltip("Which panel is visible when the scene starts.\n• None — both panels hidden.\n• Discover — shows the Discover card.\n• Swap — jumps straight to the product browser.")]
        public ProductBrowserState initialState = ProductBrowserState.Discover;
        [Tooltip("Category used for the initial state. Falls back through fridges → cabinets → lights if null.")]
        public ProductCategoryData initialCategory;
        [Tooltip("Index of the product shown on start. The custom inspector replaces this with a catalog-driven dropdown.")]
        public int initialProductIndex = 0;

        [Header("Behavior")]
        [Tooltip("If true, navigating to a product already shown re-fires all selection events.")]
        public bool reselectSameProductInvokesEvents = false;

        [Tooltip("If true, products wrap around at the first/last position.")]
        public bool wrapAroundProducts = false;

        [Header("Keyboard Debug (Editor / PC)")]
        public bool keyboardDebug = true;

        [Header("Events — Browser State")]
        [Tooltip("Fired when the Discover panel opens. Passes the first product in the catalog.")]
        public ProductDataEvent onDiscoverOpened = new ProductDataEvent();

        [Tooltip("Fired when the Swap panel opens. Passes the currently selected product.")]
        public ProductDataEvent onSwapOpened = new ProductDataEvent();

        [Tooltip("Fired when the browser closes (any state → Hidden).")]
        public UnityEvent onClosed = new UnityEvent();

        [Header("Events — Product Selection")]
        [Tooltip("Fired when a product changes (navigation or explicit open-with-product).")]
        public ProductDataEvent onProductChanged = new ProductDataEvent();

        [Tooltip("Fired when the user confirms a product (taps the CTA on the Swap panel).")]
        public ProductIndexEvent onProductConfirmed = new ProductIndexEvent();

        [Header("Debug")]
        public bool debugLogs = true;

        // ── State ────────────────────────────────────────────────────────────

        ProductBrowserState _state = ProductBrowserState.Discover;
        ProductCategoryData _activeCategory;
        int _selectedIndex = -1;
        ProductData _selectedProduct;

        public ProductBrowserState CurrentState => _state;
        public ProductCategoryData ActiveCategory => _activeCategory;
        public int SelectedIndex => _selectedIndex;
        public ProductData SelectedProduct => _selectedProduct;

        // ── Static events for cross-scene listeners ──────────────────────────

        /// <summary>Fired globally whenever a product is confirmed. Index = variant index confirmed.</summary>
        public static Action<ProductCategoryData, int> OnProductConfirmedGlobal;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            if (view == null) view = GetComponent<ProductBrowserView>();
            if (favoriteButton == null && view?.swapPanel != null)
                favoriteButton = view.swapPanel.GetComponentInChildren<FavoriteButton>(true);

            Button cta = view?.discoverPanel?.ctaButton;
            if (cta != null)
            {
                cta.onClick.RemoveListener(OpenSwap);
                cta.onClick.AddListener(OpenSwap);
            }
        }

        void OnEnable()
        {
            if (favoriteButton != null) favoriteButton.onClicked.AddListener(OnFavoriteButtonClicked);
        }

        void OnDisable()
        {
            if (favoriteButton != null) favoriteButton.onClicked.RemoveListener(OnFavoriteButtonClicked);
        }

        void OnFavoriteButtonClicked()
        {
            if (_selectedProduct == null) return;

            if (FavoritesManager.Instance == null)
            {
                Debug.LogWarning("[ProductBrowser] No FavoritesManager in scene — favorite not persisted.", this);
                return;
            }

            bool nowFavorited = FavoritesManager.Instance.Toggle(_selectedProduct);
            favoriteButton?.SetFavorited(nowFavorited);

            if (debugLogs)
                Debug.Log($"[ProductBrowser] Favorite {(nowFavorited ? "★" : "☆")} {SafeName(_selectedProduct)}", this);
        }

        void PushFavoriteStateToButton()
        {
            if (favoriteButton == null) return;
            bool fav = Application.isPlaying
                       && FavoritesManager.Instance != null
                       && _selectedProduct != null
                       && FavoritesManager.Instance.IsFavorited(_selectedProduct);
            favoriteButton.SetFavorited(fav);
        }

        void Start()
        {
            StartCoroutine(InitDeferred());
        }

        IEnumerator InitDeferred()
        {
            yield return null;
            ApplyInitialState();
            PushFavoriteStateToButton();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ApplyInitialState();
            };
        }
#endif

        void ApplyInitialState()
        {
            if (view == null) view = GetComponent<ProductBrowserView>();

            ProductCategoryData cat = initialCategory ?? fridgesCategory ?? cabinetsCategory ?? lightsCategory;
            int count = cat?.catalog != null ? cat.catalog.Count : 0;
            int index = count > 0 ? Mathf.Clamp(initialProductIndex, 0, count - 1) : 0;

            _activeCategory = cat;
            SetProductIndex(index, fireEvents: false);

            TransitionTo(initialState);

            if (_selectedProduct != null)
            {
                onProductChanged?.Invoke(_selectedProduct);
#if UNITY_EDITOR
                // In edit mode, runtime AddListener registrations are cleared after domain reload,
                // so the event above won't reach the router. Call it directly instead.
                if (!Application.isPlaying)
                {
                    var router = GetComponent<ProductVariantRouter>()
                                 ?? GetComponentInChildren<ProductVariantRouter>(true);
                    router?.ForwardProductChanged(_selectedProduct);
                }
#endif
            }
        }

        void Update()
        {
            if (keyboardDebug) HandleKeyboardDebug();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Opens the Discover panel for the given category, showing the first (or default) product.
        /// Call this from a hotspot when the user taps it.
        /// </summary>
        public void OpenDiscover(ProductCategoryData category)
        {
            if (category == null)
            {
                Debug.LogWarning("[ProductBrowser] OpenDiscover called with null category.", this);
                return;
            }

            _activeCategory = category;

            int defaultIndex = category.catalog != null ? category.catalog.GetDefaultIndex() : 0;
            defaultIndex = Mathf.Max(0, defaultIndex);

            SetProductIndex(defaultIndex, fireEvents: false);
            TransitionTo(ProductBrowserState.Discover);

            if (debugLogs)
                Debug.Log($"[ProductBrowser] Discover opened — category: {category.displayName}", this);

            if (_selectedProduct != null)
                onDiscoverOpened?.Invoke(_selectedProduct);
        }

        /// <summary>
        /// Transitions from Discover to the Swap panel.
        /// Called by the "Explore options" CTA button on the Discover panel.
        /// </summary>
        public void OpenSwap()
        {
            if (_activeCategory == null)
            {
                Debug.LogWarning("[ProductBrowser] OpenSwap called but no active category set.", this);
                return;
            }

            TransitionTo(ProductBrowserState.Swap);

            if (debugLogs)
                Debug.Log($"[ProductBrowser] Swap opened — product: {SafeName(_selectedProduct)}", this);

            if (_selectedProduct != null)
                onSwapOpened?.Invoke(_selectedProduct);
        }

        /// <summary>
        /// From Swap → returns to Discover.
        /// From Discover → disables this GameObject entirely.
        /// </summary>
        public void Close()
        {
            if (_state == ProductBrowserState.Swap && _activeCategory != null)
            {
                TransitionTo(ProductBrowserState.Discover);
                if (debugLogs)
                    Debug.Log("[ProductBrowser] Swap closed → Discover.", this);
                return;
            }

            onClosed?.Invoke();
            if (debugLogs)
                Debug.Log("[ProductBrowser] Closed.", this);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Navigate to a specific product by catalog index (used by Prev/Next buttons).
        /// </summary>
        public void SelectIndex(int index)
        {
            if (_activeCategory?.catalog == null) return;

            SetProductIndex(index, fireEvents: true);
            RefreshView();
        }

        public void SelectPrevious()
        {
            if (_activeCategory?.catalog == null) return;
            int count = _activeCategory.catalog.Count;
            int next = _selectedIndex - 1;
            if (next < 0) next = wrapAroundProducts ? count - 1 : 0;
            SelectIndex(next);
        }

        public void SelectNext()
        {
            if (_activeCategory?.catalog == null) return;
            int count = _activeCategory.catalog.Count;
            int next = _selectedIndex + 1;
            if (next >= count) next = wrapAroundProducts ? 0 : count - 1;
            SelectIndex(next);
        }

        /// <summary>
        /// Confirms the current product selection — fires the variant/visibility router events.
        /// The favorite toggle is owned by <see cref="FavoriteButton"/> on the Swap panel's
        /// SelectButton, so this method no longer touches <see cref="FavoritesManager"/>.
        /// </summary>
        public void ConfirmSelection()
        {
            if (_selectedIndex < 0 || _selectedProduct == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[ProductBrowser] ConfirmSelection called with no active product.", this);
                return;
            }

            if (debugLogs)
                Debug.Log($"<color=lime>[ProductBrowser]</color> Confirmed index {_selectedIndex}: {SafeName(_selectedProduct)}", this);

            onProductConfirmed?.Invoke(_selectedIndex);
            OnProductConfirmedGlobal?.Invoke(_activeCategory, _selectedIndex);
        }

        // ── Internals ────────────────────────────────────────────────────────

        void SetProductIndex(int index, bool fireEvents)
        {
            if (_activeCategory?.catalog == null) return;

            index = Mathf.Clamp(index, 0, _activeCategory.catalog.Count - 1);
            ProductData product = _activeCategory.catalog.GetProduct(index);

            bool changed = index != _selectedIndex;
            _selectedIndex = index;
            _selectedProduct = product;

            if (changed) PushFavoriteStateToButton();

            if ((!changed && !reselectSameProductInvokesEvents) || !fireEvents) return;

            if (debugLogs)
                Debug.Log($"[ProductBrowser] Product changed to index {index}: {SafeName(product)}", this);

            onProductChanged?.Invoke(product);

            if (product?.onSelectedAssetEvent != null)
                product.onSelectedAssetEvent.Invoke();
        }

        void TransitionTo(ProductBrowserState newState)
        {
            _state = newState;
            RefreshView();
        }

        void RefreshView()
        {
            if (view == null) return;
            view.SetState(_state, _activeCategory, _selectedIndex, instant: false);
        }

        void HandleKeyboardDebug()
        {
            if (KeyInput.GetKeyDown(KeyCode.LeftArrow))  SelectPrevious();
            if (KeyInput.GetKeyDown(KeyCode.RightArrow)) SelectNext();
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter)) ConfirmSelection();
            if (KeyInput.GetKeyDown(KeyCode.D) && _state == ProductBrowserState.Discover) OpenSwap();
            if (KeyInput.GetKeyDown(KeyCode.Escape)) Close();
        }

        static string SafeName(ProductData p) =>
            p == null ? "<null>" : (!string.IsNullOrEmpty(p.productName) ? p.productName : p.name);
    }

    // ── Supporting types ─────────────────────────────────────────────────────

    public enum ProductBrowserState
    {
        Discover,
        Swap
    }

    [Serializable] public class ProductDataEvent : UnityEvent<ProductData> { }
    [Serializable] public class ProductIndexEvent : UnityEvent<int> { }
}
