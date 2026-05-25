#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="ProductSwapView"/>.
    ///
    /// When a <see cref="ProductBrowserController"/> is found in the parent hierarchy,
    /// the Preview Category is locked to the controller's resolved initial category
    /// (instead of being independently editable) and Preview Index becomes a catalog-driven popup.
    /// </summary>
    [CustomEditor(typeof(ProductSwapView))]
    public class ProductSwapViewEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ProductSwapView view = (ProductSwapView)target;

            ProductBrowserController controller = view.GetComponentInParent<ProductBrowserController>(true);
            ProductCategoryData resolvedCat = controller != null
                ? (controller.initialCategory ?? controller.fridgesCategory ?? controller.cabinetsCategory ?? controller.lightsCategory)
                : null;

            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true); // skip m_Script

            while (prop.NextVisible(false))
            {
                if (prop.name == "previewCategory")
                {
                    DrawLockedOrFreeCategory(prop, resolvedCat, controller);
                }
                else if (prop.name == "previewIndex")
                {
                    ProductCategoryData cat = resolvedCat ?? view.previewCategory;
                    DrawPreviewIndexField(prop, cat, view, resolvedCat != null);
                }
                else
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }

            serializedObject.ApplyModifiedProperties();

            // Keep previewCategory in sync with the controller so OnValidate sees the right value.
            if (resolvedCat != null && view.previewCategory != resolvedCat)
            {
                Undo.RecordObject(view, "Sync Preview Category");
                view.previewCategory = resolvedCat;
                EditorUtility.SetDirty(view);
            }
        }

        void DrawLockedOrFreeCategory(SerializedProperty prop, ProductCategoryData resolvedCat, ProductBrowserController controller)
        {
            if (resolvedCat != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        new GUIContent("Preview Category", $"Sourced from ProductBrowserController on \"{controller.gameObject.name}\". Edit Initial Category there."),
                        resolvedCat,
                        typeof(ProductCategoryData),
                        false);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(prop);
            }
        }

        void DrawPreviewIndexField(SerializedProperty indexProp, ProductCategoryData cat, ProductSwapView view, bool isLocked)
        {
            if (cat?.catalog == null || cat.catalog.Count == 0)
            {
                EditorGUILayout.PropertyField(indexProp);
                return;
            }

            int count = cat.catalog.Count;
            string[] options = new string[count];
            for (int i = 0; i < count; i++)
            {
                ProductData p = cat.catalog.GetProduct(i);
                options[i] = p != null ? $"{i + 1}. {p.productName}" : $"{i}: (empty)";
            }

            int current = Mathf.Clamp(indexProp.intValue, 0, count - 1);

            GUIContent label = new GUIContent(
                "Preview Index",
                isLocked
                    ? $"Catalog: {cat.displayName} ({count} items) — sourced from ProductBrowserController"
                    : $"Catalog: {cat.displayName} ({count} items)");

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(label, current, options);
            if (EditorGUI.EndChangeCheck())
            {
                indexProp.intValue = selected;
                serializedObject.ApplyModifiedProperties();
                if (!Application.isPlaying)
                    view.SendMessage("OnValidate", null, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
#endif
