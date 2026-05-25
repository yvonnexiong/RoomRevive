using System;
using UnityEngine;
using UnityEngine.Events;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Shows and hides scene GameObjects in response to browser state changes.
    ///
    /// Two binding lists:
    ///   <see cref="stateBindings"/>   — per-browser-state visibility (Hidden/Discover/Swap).
    ///   <see cref="productBindings"/> — per-product-index visibility (shown when that product is confirmed).
    ///
    /// Add this as a sibling of <see cref="ProductBrowserController"/> on the
    /// ProductBrowserUI root, or on any scene GameObject.
    /// </summary>
    public class ProductVisibilityRouter : MonoBehaviour
    {
        [Header("Controller")]
        [Tooltip("Controller to listen to. Auto-found in scene if empty.")]
        public ProductBrowserController controller;

        [Header("State Visibility")]
        [Tooltip("GameObjects shown/hidden based on which browser state is active.")]
        public BrowserStateBinding[] stateBindings;

        [Header("Product Visibility")]
        [Tooltip("GameObjects shown/hidden when a specific product index is confirmed.")]
        public ProductIndexBinding[] productBindings;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        void Awake() => ResolveController();

        void OnEnable()
        {
            ResolveController();
            if (controller != null)
            {
                controller.onDiscoverOpened.AddListener(OnDiscoverOpened);
                controller.onSwapOpened.AddListener(OnSwapOpened);
                controller.onClosed.AddListener(OnClosed);
                controller.onProductConfirmed.AddListener(OnProductConfirmed);
            }
        }

        void OnDisable()
        {
            if (controller != null)
            {
                controller.onDiscoverOpened.RemoveListener(OnDiscoverOpened);
                controller.onSwapOpened.RemoveListener(OnSwapOpened);
                controller.onClosed.RemoveListener(OnClosed);
                controller.onProductConfirmed.RemoveListener(OnProductConfirmed);
            }
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        void OnDiscoverOpened(ProductData _) => ApplyStateBindings(ProductBrowserState.Discover);
        void OnSwapOpened(ProductData _)     => ApplyStateBindings(ProductBrowserState.Swap);
        void OnClosed()                      { /* browser GO disabled — no state to apply */ }

        void OnProductConfirmed(int index)
        {
            if (productBindings == null) return;
            foreach (ProductIndexBinding binding in productBindings)
            {
                if (binding == null) continue;
                bool match = binding.productIndex == index;
                SetObjects(binding.objectsToShow, match);
            }
        }

        // ── Internals ────────────────────────────────────────────────────────

        void ApplyStateBindings(ProductBrowserState state)
        {
            if (stateBindings == null) return;
            foreach (BrowserStateBinding binding in stateBindings)
            {
                if (binding == null) continue;
                bool match = binding.state == state;
                SetObjects(binding.objectsToShow, match);
            }
        }

        static void SetObjects(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (GameObject go in objects)
            {
                if (go != null) go.SetActive(active);
            }
        }

        void ResolveController()
        {
            if (controller != null) return;
            controller = GetComponent<ProductBrowserController>();
            if (controller == null)
#if UNITY_2022_2_OR_NEWER
                controller = FindFirstObjectByType<ProductBrowserController>();
#else
                controller = FindObjectOfType<ProductBrowserController>();
#endif
        }
    }

    // ── Binding data classes ─────────────────────────────────────────────────

    [Serializable]
    public class BrowserStateBinding
    {
        [Tooltip("Which browser state triggers this binding.")]
        public ProductBrowserState state;
        [Tooltip("GameObjects to enable when this state is active (all others in this binding are disabled).")]
        public GameObject[] objectsToShow;
    }

    [Serializable]
    public class ProductIndexBinding
    {
        [Tooltip("Catalog index of the product that triggers this binding.")]
        public int productIndex;
        [Tooltip("GameObjects to enable when this product index is confirmed.")]
        public GameObject[] objectsToShow;
    }
}
