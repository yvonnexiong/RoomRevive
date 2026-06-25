using UnityEngine;
using RoomRevive.SplatEditorBridge;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Bridges the cabinet product browser to the live splat editor: when the selected kitchen
    /// changes, it asks the editor to swap the cabinet-front + worktop materials via
    /// <see cref="SplatMaterialSwapClient"/>.
    ///
    /// The exact editor filenames live on each kitchen's <see cref="ProductData"/>
    /// (<c>splatCabMaterial</c> / <c>splatWtMaterial</c>), baked there by the catalog sync from the
    /// kitchen's design-element references. This router only forwards them — no catalog parsing at runtime.
    ///
    /// Setup: put this on the Cabinet <c>ProductBrowserUI</c> (next to the controller) and assign /
    /// auto-find a <see cref="SplatMaterialSwapClient"/>. Harmless on the fridge UI — fridge products
    /// have empty splat-material fields, so nothing is sent.
    /// </summary>
    [DisallowMultipleComponent]
    public class CabinetMaterialSwapRouter : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Controller to listen to. Auto-found on this GameObject if empty.")]
        public ProductBrowserController controller;

        [Tooltip("HTTP client that talks to the splat editor. Auto-found on this GameObject / scene if empty.")]
        public SplatMaterialSwapClient swapClient;

        [Header("Behavior")]
        [Tooltip("Swap as soon as the displayed product changes (preview). If false, only an explicit " +
                 "SwapForSelected() call triggers a swap — wire that to the CTA / confirm instead.")]
        public bool swapOnProductChange = true;

        [Tooltip("Log each swap request.")]
        public bool debugLogs = true;

        [Header("Status (read-only)")]
        [Tooltip("Number of onProductChanged events received since enable.")]
        public int productChangeCount;
        [Tooltip("Number of swaps actually forwarded to the editor.")]
        public int swapCount;
        [Tooltip("Most recent product + materials handled.")]
        public string lastStatus = "idle";

        void OnEnable()
        {
            ResolveRefs();
            if (controller != null)
            {
                controller.onProductChanged.AddListener(OnProductChanged);
                if (debugLogs) Debug.Log("[CabinetMaterialSwapRouter] Subscribed to onProductChanged.", this);
            }
            else
            {
                Debug.LogWarning("[CabinetMaterialSwapRouter] No ProductBrowserController found — swaps won't fire.", this);
            }
        }

        void OnDisable()
        {
            if (controller != null) controller.onProductChanged.RemoveListener(OnProductChanged);
        }

        void ResolveRefs()
        {
            if (controller == null) controller = GetComponent<ProductBrowserController>();
            if (swapClient == null) swapClient = GetComponent<SplatMaterialSwapClient>();
            if (swapClient == null)
#if UNITY_2022_2_OR_NEWER
                swapClient = FindFirstObjectByType<SplatMaterialSwapClient>();
#else
                swapClient = FindObjectOfType<SplatMaterialSwapClient>();
#endif
        }

        void OnProductChanged(ProductData product)
        {
            productChangeCount++;
            if (debugLogs)
                Debug.Log($"[CabinetMaterialSwapRouter] onProductChanged #{productChangeCount} → {SafeName(product)} " +
                          $"(cab='{product?.splatCabMaterial}', wt='{product?.splatWtMaterial}')", this);
            if (swapOnProductChange) ApplySwap(product);
        }

        /// <summary>Swaps materials for the controller's currently selected product (wire to a button / confirm).</summary>
        public void SwapForSelected() =>
            ApplySwap(controller != null ? controller.SelectedProduct : null);

        void ApplySwap(ProductData product)
        {
            // The client only issues requests in Play mode; skip silently otherwise so editor
            // navigation doesn't spam warnings.
            if (!Application.isPlaying || product == null) return;

            string cab = product.splatCabMaterial;
            string wt  = product.splatWtMaterial;
            if (string.IsNullOrEmpty(cab) && string.IsNullOrEmpty(wt))
            {
                lastStatus = $"skipped {SafeName(product)} — no splat materials (not a kitchen?)";
                return; // not a kitchen / nothing to swap
            }

            if (swapClient == null) ResolveRefs();
            if (swapClient == null)
            {
                lastStatus = "no SplatMaterialSwapClient";
                Debug.LogWarning("[CabinetMaterialSwapRouter] No SplatMaterialSwapClient found — add one to the scene.", this);
                return;
            }

            swapCount++;
            lastStatus = $"#{swapCount} {SafeName(product)} → cab='{cab}', wt='{wt}'";
            if (debugLogs) Debug.Log($"[CabinetMaterialSwapRouter] {lastStatus}", this);

            swapClient.SwapMaterials(cab, wt);
        }

        static string SafeName(ProductData p) =>
            p == null ? "<null>" : (!string.IsNullOrEmpty(p.productName) ? p.productName : p.name);
    }
}
