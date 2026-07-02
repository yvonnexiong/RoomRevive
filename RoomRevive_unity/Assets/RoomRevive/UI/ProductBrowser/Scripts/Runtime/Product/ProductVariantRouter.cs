using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Listens to <see cref="ProductBrowserController"/> events and applies scene side effects.
    ///
    /// Model3D mode:      enables one GameObject from <see cref="objectVariants"/> per product index.
    /// GaussianSplat mode: swaps the asset on <see cref="splatRenderer"/> directly — no SplatManager enum needed.
    /// </summary>
    public class ProductVariantRouter : MonoBehaviour
    {
        [Header("Controller")]
        [Tooltip("Controller to listen to. Auto-found in scene if empty.")]
        public ProductBrowserController controller;

        [Header("Object Swap")]
        [Tooltip("Model3D — enables/disables scene GameObjects.\nGaussianSplat — swaps GaussianSplatAsset on a renderer.")]
        public RouterSwapMode swapMode = RouterSwapMode.Model3D;

        // ── Model3D fields ───────────────────────────────────────────────────

        [FormerlySerializedAs("fridgeVariants")]
        [Tooltip("Scene GameObjects in the same order as the catalog. Index 0 here = index 0 in the catalog.")]
        public GameObject[] objectVariants;

        [FormerlySerializedAs("hideFridgesWhenBrowserClosed")]
        [Tooltip("If true, all variant objects are hidden while the browser is closed.")]
        public bool hideObjectsWhenBrowserClosed = true;

        [FormerlySerializedAs("previewFridgeOnProductChange")]
        [Tooltip("If true, the active object is swapped as soon as the product changes (not just on confirm).")]
        public bool previewObjectOnProductChange = true;

        // ── GaussianSplat fields ─────────────────────────────────────────────

        [Tooltip("The GaussianSplatRenderer on this Cabinet UI that should receive the asset swap.")]
        public GaussianSplatRenderer splatRenderer;

        [Tooltip("If true, the splat asset is swapped as soon as the product changes (not just on confirm).")]
        public bool previewSplatOnProductChange = true;

        [Header("Lighting UnityEvents")]
        [Tooltip("Fired when any variant is confirmed. Wire light-change logic here.")]
        public ProductIndexEvent onLightingVariantSelected = new ProductIndexEvent();

        // ── Unity lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            ResolveDependencies();
            RefreshCurrentModelVisibility();
        }

        void OnEnable()
        {
            ResolveDependencies();
            RefreshCurrentModelVisibility();
        }

        // ── Public forwarding (editor preview + persistent listeners) ────────

        public void ForwardConfirm(int index)                  => OnProductConfirmed(index);
        public void ForwardProductChanged(ProductData product) => OnProductChanged(product);
        public void ForwardClosed()                            => OnBrowserClosed();

        /// <summary>
        /// Reapplies the selected 3D model while respecting the controller's
        /// disableCurrent3DModel override.
        /// </summary>
        public void RefreshCurrentModelVisibility()
        {
            if (swapMode != RouterSwapMode.Model3D)
            {
                return;
            }

            int index = controller != null
                ? Mathf.Max(0, controller.SelectedIndex)
                : 0;

            ApplyObjectSwap(index);
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        void OnProductConfirmed(int index)
        {
            switch (swapMode)
            {
                case RouterSwapMode.Model3D:      ApplyObjectSwap(index);        break;
                case RouterSwapMode.GaussianSplat: ApplyGaussianSplatSwap(index); break;
            }
            onLightingVariantSelected?.Invoke(index);
        }

        void OnProductChanged(ProductData product)
        {
            int idx = controller?.SelectedIndex ?? 0;
            if (swapMode == RouterSwapMode.Model3D      && previewObjectOnProductChange) ApplyObjectSwap(idx);
            if (swapMode == RouterSwapMode.GaussianSplat && previewSplatOnProductChange)  ApplyGaussianSplatSwap(idx);
        }

        void OnBrowserClosed()
        {
            if (swapMode == RouterSwapMode.Model3D && hideObjectsWhenBrowserClosed)
                SetAllObjectsActive(false);
        }

        // ── Swap implementations ─────────────────────────────────────────────

        void ApplyObjectSwap(int index)
        {
            if (objectVariants == null || objectVariants.Length == 0) return;

            bool disableSelectedModel =
                controller != null && controller.disableCurrent3DModel;

            for (int i = 0; i < objectVariants.Length; i++)
                if (objectVariants[i] != null)
                    objectVariants[i].SetActive(!disableSelectedModel && i == index);
        }

        void ApplyGaussianSplatSwap(int index)
        {
            ProductData product = controller?.ActiveCategory?.catalog?.GetProduct(index);
            if (product == null)
            {
                Debug.LogWarning($"[ProductVariantRouter] No product at index {index} in active catalog.", this);
                return;
            }
            if (product.splatAsset == null)
            {
                Debug.LogWarning($"[ProductVariantRouter] ProductData '{product.name}' has no splatAsset assigned.", this);
                return;
            }

            SplatManager sm = SplatManager.Instance;
#if UNITY_EDITOR
            if (sm == null)
#if UNITY_2022_2_OR_NEWER
                sm = FindFirstObjectByType<SplatManager>();
#else
                sm = FindObjectOfType<SplatManager>();
#endif
#endif
            if (sm == null)
            {
                Debug.LogWarning("[ProductVariantRouter] GaussianSplat swap: SplatManager not found.", this);
                return;
            }

            sm.SetAsset(product.splatAsset, splatRenderer);
        }

        void SetAllObjectsActive(bool active)
        {
            if (objectVariants == null) return;
            foreach (GameObject go in objectVariants)
                if (go != null) go.SetActive(active);
        }

        // ── Dependency resolution ─────────────────────────────────────────────

        void ResolveDependencies()
        {
            if (controller == null) controller = GetComponent<ProductBrowserController>();
            if (controller == null)
#if UNITY_2022_2_OR_NEWER
                controller = FindFirstObjectByType<ProductBrowserController>();
#else
                controller = FindObjectOfType<ProductBrowserController>();
#endif
        }
    }

    public enum RouterSwapMode
    {
        [InspectorName("3D Model")]    Model3D,
        [InspectorName("Gaussian Splat")] GaussianSplat
    }
}
