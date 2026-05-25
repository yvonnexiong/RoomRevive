#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoomRevive.ProductBrowser
{
    [CustomEditor(typeof(ProductVariantRouter))]
    [CanEditMultipleObjects]
    public class ProductVariantRouterEditor : Editor
    {
        private SerializedProperty controller;
        private SerializedProperty swapMode;

        private SerializedProperty objectVariants;
        private SerializedProperty hideObjectsWhenBrowserClosed;
        private SerializedProperty previewObjectOnProductChange;

        private SerializedProperty splatRenderer;
        private SerializedProperty previewSplatOnProductChange;

        private SerializedProperty onLightingVariantSelected;

        private void OnEnable()
        {
            controller                   = serializedObject.FindProperty(nameof(ProductVariantRouter.controller));
            swapMode                     = serializedObject.FindProperty(nameof(ProductVariantRouter.swapMode));

            objectVariants               = serializedObject.FindProperty(nameof(ProductVariantRouter.objectVariants));
            hideObjectsWhenBrowserClosed = serializedObject.FindProperty(nameof(ProductVariantRouter.hideObjectsWhenBrowserClosed));
            previewObjectOnProductChange = serializedObject.FindProperty(nameof(ProductVariantRouter.previewObjectOnProductChange));

            splatRenderer                = serializedObject.FindProperty(nameof(ProductVariantRouter.splatRenderer));
            previewSplatOnProductChange  = serializedObject.FindProperty(nameof(ProductVariantRouter.previewSplatOnProductChange));

            onLightingVariantSelected    = serializedObject.FindProperty(nameof(ProductVariantRouter.onLightingVariantSelected));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(controller);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Object Swap", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(swapMode);

            EditorGUILayout.Space(8);

            if (swapMode.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(
                    "Multiple ProductVariantRouter components selected with different Swap Mode values.",
                    MessageType.Info);
            }
            else
            {
                switch ((RouterSwapMode)swapMode.enumValueIndex)
                {
                    case RouterSwapMode.Model3D:
                        EditorGUILayout.LabelField("3D Model References", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(objectVariants, true);
                        EditorGUILayout.PropertyField(hideObjectsWhenBrowserClosed);
                        EditorGUILayout.PropertyField(previewObjectOnProductChange);
                        break;

                    case RouterSwapMode.GaussianSplat:
                        EditorGUILayout.PropertyField(splatRenderer);
                        EditorGUILayout.HelpBox("Splat assets are read from each ProductData in the controller's active catalog. Assign splatAsset on each ProductData asset directly.", MessageType.None);
                        EditorGUILayout.PropertyField(previewSplatOnProductChange);
                        break;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Lighting UnityEvents", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(onLightingVariantSelected);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
