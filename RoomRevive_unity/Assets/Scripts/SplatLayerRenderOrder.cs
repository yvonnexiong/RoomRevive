using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

using UnityComponent = UnityEngine.Component;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SplatLayerRenderOrder : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera baseCamera;
    [SerializeField] private Camera topSplatCamera;

    [Header("Layers")]
    [Tooltip("The layer of the splat that should render on TOP.")]
    [SerializeField] private LayerMask topSplatLayer;

    [Tooltip("Everything the base camera should normally render.")]
    [SerializeField] private LayerMask baseCameraRenderMask = ~0;

    [Header("Settings")]
    [SerializeField] private bool removeTopSplatLayerFromBaseCamera = true;

    [Tooltip("Higher value means the top splat camera renders later.")]
    [SerializeField] private float topCameraDepthOffset = 10f;

    [Tooltip("If true, this script creates the top splat camera automatically.")]
    [SerializeField] private bool autoCreateTopCamera = true;

    private const string TopCameraName = "Top Splat Render Camera";

    private void Reset()
    {
        baseCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (baseCamera == null)
            baseCamera = GetComponent<Camera>();

        if (baseCamera == null)
            return;

        if (topSplatCamera == null && autoCreateTopCamera)
            topSplatCamera = GetOrCreateTopCamera();

        if (topSplatCamera == null)
            return;

        int topMask = topSplatLayer.value;

        if (removeTopSplatLayerFromBaseCamera)
            baseCamera.cullingMask = baseCameraRenderMask.value & ~topMask;
        else
            baseCamera.cullingMask = baseCameraRenderMask.value;

        CopyCameraSettings(baseCamera, topSplatCamera);

        topSplatCamera.cullingMask = topMask;

        // This makes the second camera keep the first camera's color,
        // but clear depth so the top splat can draw over it.
        topSplatCamera.clearFlags = CameraClearFlags.Depth;

        topSplatCamera.depth = baseCamera.depth + topCameraDepthOffset;
        topSplatCamera.enabled = true;

        TrySetupURPCameraStack(baseCamera, topSplatCamera);
    }

    private Camera GetOrCreateTopCamera()
    {
        Transform existing = transform.Find(TopCameraName);

        if (existing != null)
        {
            Camera existingCamera = existing.GetComponent<Camera>();

            if (existingCamera != null)
                return existingCamera;
        }

        GameObject cameraObject = new GameObject(TopCameraName);
        cameraObject.transform.SetParent(transform, false);

        Camera newCamera = cameraObject.AddComponent<Camera>();
        return newCamera;
    }

    private void CopyCameraSettings(Camera source, Camera target)
    {
        target.CopyFrom(source);

        target.transform.position = source.transform.position;
        target.transform.rotation = source.transform.rotation;
        target.transform.localScale = source.transform.localScale;

        target.targetDisplay = source.targetDisplay;
        target.targetTexture = source.targetTexture;

        target.cullingMask = topSplatLayer.value;
        target.clearFlags = CameraClearFlags.Depth;
        target.depth = source.depth + topCameraDepthOffset;
    }

    private void TrySetupURPCameraStack(Camera baseCam, Camera overlayCam)
    {
        Type urpCameraDataType = Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"
        );

        if (urpCameraDataType == null)
            return;

        UnityComponent baseData = GetOrAddComponent(baseCam.gameObject, urpCameraDataType);
        UnityComponent overlayData = GetOrAddComponent(overlayCam.gameObject, urpCameraDataType);

        if (baseData == null || overlayData == null)
            return;

        PropertyInfo renderTypeProperty = urpCameraDataType.GetProperty("renderType");

        if (renderTypeProperty != null)
        {
            object baseRenderType = Enum.Parse(renderTypeProperty.PropertyType, "Base");
            object overlayRenderType = Enum.Parse(renderTypeProperty.PropertyType, "Overlay");

            renderTypeProperty.SetValue(baseData, baseRenderType);
            renderTypeProperty.SetValue(overlayData, overlayRenderType);
        }

        PropertyInfo cameraStackProperty = urpCameraDataType.GetProperty("cameraStack");

        if (cameraStackProperty == null)
            return;

        IList cameraStack = cameraStackProperty.GetValue(baseData) as IList;

        if (cameraStack == null)
            return;

        if (!cameraStack.Contains(overlayCam))
            cameraStack.Add(overlayCam);
    }

    private UnityComponent GetOrAddComponent(GameObject targetObject, Type componentType)
    {
        UnityComponent component = targetObject.GetComponent(componentType);

        if (component == null)
            component = targetObject.AddComponent(componentType);

        return component;
    }
}