using UnityEngine;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Root view that owns the Discover and Swap panel references and switches
    /// between them based on <see cref="ProductBrowserState"/>.
    ///
    /// Lives on the same GameObject as <see cref="ProductBrowserController"/>.
    /// Does not build hierarchy — panels are pre-placed in the scene as children.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProductBrowserView : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("The compact teaser card. Pre-placed in scene, disabled by default.")]
        public ProductDiscoverView discoverPanel;

        [Tooltip("The full variant swap panel. Pre-placed in scene, disabled by default.")]
        public ProductSwapView swapPanel;

        [Header("Fade")]
        [Tooltip("Seconds for panel cross-fade transitions.")]
        public float fadeDuration = 0.2f;

        // ── Public API called by the controller ──────────────────────────────

        /// <summary>
        /// Show the correct panel for <paramref name="state"/> and bind product data into it.
        /// </summary>
        public void SetState(ProductBrowserState state, ProductCategoryData category, int productIndex, bool instant)
        {
            switch (state)
            {
                case ProductBrowserState.Discover:
                    SetPanelActive(swapPanel, false);
                    BindAndShow(discoverPanel, category, productIndex);
                    break;

                case ProductBrowserState.Swap:
                    SetPanelActive(discoverPanel, false);
                    BindAndShow(swapPanel, category, productIndex);
                    break;
            }
        }

        // ── Internals ────────────────────────────────────────────────────────

        void BindAndShow(IProductPanel panel, ProductCategoryData category, int productIndex)
        {
            if (panel == null) return;
            panel.Bind(category, productIndex);
            SetPanelActive(panel, true);
        }

        void SetPanelActive(IProductPanel panel, bool active)
        {
            if (panel == null) return;
            var mb = panel as MonoBehaviour;
            if (mb != null) mb.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// Common binding interface shared by Discover and Swap panels.
    /// Implemented by <see cref="ProductDiscoverView"/> and <see cref="ProductSwapView"/>.
    /// </summary>
    public interface IProductPanel
    {
        void Bind(ProductCategoryData category, int productIndex);
    }
}
