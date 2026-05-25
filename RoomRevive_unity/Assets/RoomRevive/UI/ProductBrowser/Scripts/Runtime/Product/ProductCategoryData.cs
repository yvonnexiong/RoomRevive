using UnityEngine;

namespace RoomRevive.ProductBrowser
{
    /// <summary>
    /// Configuration asset for one browsable category — Fridges, Cabinets, or Lights.
    ///
    /// Holds the product catalog and describes how a selected variant maps to a scene
    /// side effect (swap type). The actual side-effect execution lives in the routers
    /// so the controller stays scene-agnostic.
    ///
    /// Create via: Assets → Create → RoomRevive / Product Browser / Product Category
    /// </summary>
    [CreateAssetMenu(menuName = "RoomRevive/Product Browser/Product Category", fileName = "Category_New")]
    public class ProductCategoryData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by routers and logs, e.g. 'fridges', 'cabinets', 'lights'.")]
        public string id;

        [Tooltip("Display label shown as the panel header when this category is open.")]
        public string displayName;

        [Header("Catalog")]
        [Tooltip("Products available for this category.")]
        public ProductCatalog catalog;

        [Header("Swap Type")]
        [Tooltip("How a selected product variant maps to a scene change. Routers read this to decide what action to take.")]
        public ProductSwapType swapType = ProductSwapType.FridgeGameObject;

        [Header("Theme")]
        [Tooltip("Accent color used for CTA buttons and dot indicators in this category's panels.")]
        public Color accentColor = new Color(0.96f, 0.65f, 0.14f, 1f);

        [Header("Discover Panel")]
        [Tooltip("Teaser headline shown on the Discover card before the user taps 'Explore options'.")]
        [TextArea(1, 3)]
        public string discoverHeadline;

        [Tooltip("Supporting sentence shown below the headline on the Discover card.")]
        [TextArea(1, 3)]
        public string discoverSubline;
    }

    /// <summary>
    /// Determines which router handles the variant-selected side effect for a category.
    /// Routers switch on this value to apply the correct scene change.
    /// </summary>
    public enum ProductSwapType
    {
        /// <summary>Toggle fridge GameObjects — variant 0/1/2 maps to fridgeVariants[] on ProductVariantRouter.</summary>
        FridgeGameObject,

        /// <summary>Call SplatManager to swap to a different Gaussian Splat room preset.</summary>
        CabinetSplat,

        /// <summary>Light-only category — no geometry swap. Routers can use UnityEvents for light scene effects.</summary>
        LightingOnly
    }
}
