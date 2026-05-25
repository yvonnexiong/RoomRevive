using System;
using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoomRevive.IntentSelector
{
    /// <summary>
    /// Configures world-space Canvas + CanvasScaler + GraphicRaycaster,
    /// PointableCanvas, an ISDK_RayInteractionSurface child with
    /// PlaneSurface + BoundsClipper + RectTransformBoundsClipperDriver +
    /// ClippedPlaneSurface + RayInteractable, and ensures an EventSystem
    /// with PointableCanvasModule exists.
    ///
    /// Does NOT build the visual card UI. That lives on the prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class MetaWorldSpaceCanvasSetup : MonoBehaviour
    {
        [Header("Canvas")]
        public Camera eventCamera;
        public float worldScale = 0.001f;

        [Header("Meta Ray Interaction")]
        public bool autoCreateEventSystem = true;
        public PlaneSurface.NormalFacing raySurfaceFacing = PlaneSurface.NormalFacing.Backward;

        [Header("When to Configure")]
        public bool configureOnAwake = true;
        public bool configureInEditorFromContextMenuOnly = true;

        [Header("Debug")]
        public bool debugLogs = false;

        Canvas _canvas;
        CanvasScaler _canvasScaler;
        GraphicRaycaster _graphicRaycaster;
        PointableCanvas _pointableCanvas;
        RayInteractable _rayInteractable;
        GameObject _raySurfaceRoot;

        public RayInteractable CanvasRayInteractable => _rayInteractable;

        void Awake()
        {
            if (configureOnAwake) Configure();
        }

        [ContextMenu("Configure")]
        public void Configure()
        {
            GrabComponents();
            ConfigureCanvas();
            EnsurePointableCanvas();
            EnsureRayInteractionSurface();
            EnsureEventSystemAndPointableModule();
        }

        void GrabComponents()
        {
            _canvas = GetComponent<Canvas>();
            _canvasScaler = GetComponent<CanvasScaler>();
            _graphicRaycaster = GetComponent<GraphicRaycaster>();
        }

        void ConfigureCanvas()
        {
            if (_canvas == null) return;

            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = eventCamera != null ? eventCamera : Camera.main;

            transform.localScale = Vector3.one * worldScale;

            if (_canvasScaler != null)
            {
                _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _canvasScaler.referencePixelsPerUnit = 100f;
                _canvasScaler.dynamicPixelsPerUnit = 10f;
            }

            if (_graphicRaycaster != null)
            {
                _graphicRaycaster.ignoreReversedGraphics = true;
                _graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            }
        }

        public void EnsurePointableCanvas()
        {
            _pointableCanvas = GetComponent<PointableCanvas>();

            if (_pointableCanvas == null)
                _pointableCanvas = gameObject.AddComponent<PointableCanvas>();

            if (_canvas != null)
                _pointableCanvas.InjectAllPointableCanvas(_canvas);
        }

        public void EnsureRayInteractionSurface()
        {
            Transform existing = transform.Find("ISDK_RayInteractionSurface");
            if (existing != null)
                _raySurfaceRoot = existing.gameObject;
            else
                _raySurfaceRoot = MakeUIChild("ISDK_RayInteractionSurface", transform);

            StretchFull(_raySurfaceRoot);

            LayoutElement layoutElement = AddOrGet<LayoutElement>(_raySurfaceRoot);
            layoutElement.ignoreLayout = true;

            PlaneSurface planeSurface = AddOrGet<PlaneSurface>(_raySurfaceRoot);
            planeSurface.Facing = raySurfaceFacing;

            BoundsClipper boundsClipper = AddOrGet<BoundsClipper>(_raySurfaceRoot);

            RectTransformBoundsClipperDriver clipperDriver = AddOrGet<RectTransformBoundsClipperDriver>(_raySurfaceRoot);
            TryConfigureRectTransformBoundsClipperDriver(clipperDriver, boundsClipper);

            ClippedPlaneSurface clippedPlaneSurface = AddOrGet<ClippedPlaneSurface>(_raySurfaceRoot);
            clippedPlaneSurface.InjectAllClippedPlaneSurface(planeSurface, new List<IBoundsClipper> { boundsClipper });

            _rayInteractable = AddOrGet<RayInteractable>(_raySurfaceRoot);
            _rayInteractable.InjectAllRayInteractable(clippedPlaneSurface);

            if (_pointableCanvas != null)
                _rayInteractable.InjectOptionalPointableElement(_pointableCanvas);

            _rayInteractable.InjectOptionalSelectSurface(planeSurface);
        }

        public void EnsureEventSystemAndPointableModule()
        {
            if (!autoCreateEventSystem) return;

            EventSystem eventSystem = FindAny<EventSystem>();

            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem - Meta Pointable Canvas");
                eventSystem = eventSystemGO.AddComponent<EventSystem>();

                if (debugLogs) Debug.Log("[MetaWorldSpaceCanvasSetup] Created EventSystem.", this);
            }

            if (eventSystem.GetComponent<PointableCanvasModule>() == null)
            {
                eventSystem.gameObject.AddComponent<PointableCanvasModule>();
                if (debugLogs) Debug.Log("[MetaWorldSpaceCanvasSetup] Added PointableCanvasModule.", this);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        static GameObject MakeUIChild(string n, Transform parent)
        {
            GameObject go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent != null ? parent.gameObject.layer : 5;
            return go;
        }

        static void StretchFull(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static T AddOrGet<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static T FindAny<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        static void TryConfigureRectTransformBoundsClipperDriver(
            RectTransformBoundsClipperDriver driver,
            BoundsClipper boundsClipper)
        {
            if (driver == null || boundsClipper == null) return;

            RectTransform rectTransform = driver.GetComponent<RectTransform>();
            Type driverType = typeof(RectTransformBoundsClipperDriver);

            MethodInfo[] methods = driverType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (MethodInfo method in methods)
            {
                if (!method.Name.StartsWith("Inject", StringComparison.Ordinal)) continue;

                ParameterInfo[] parameters = method.GetParameters();
                object[] args = new object[parameters.Length];
                bool canUseMethod = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type p = parameters[i].ParameterType;
                    if (p.IsAssignableFrom(typeof(BoundsClipper))) args[i] = boundsClipper;
                    else if (p.IsAssignableFrom(typeof(RectTransform))) args[i] = rectTransform;
                    else if (p.IsAssignableFrom(typeof(Transform))) args[i] = rectTransform;
                    else { canUseMethod = false; break; }
                }

                if (!canUseMethod) continue;

                try { method.Invoke(driver, args); break; }
                catch { /* version-specific differences fall through to field fallback. */ }
            }

            SetFieldIfExists(driver, "_boundsClipper", boundsClipper);
            SetFieldIfExists(driver, "_rectTransform", rectTransform);

            MethodInfo resizeMethod = driverType.GetMethod(
                "Resize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            try { resizeMethod?.Invoke(driver, null); } catch { /* non-fatal */ }
        }

        static void SetFieldIfExists(object target, string fieldName, object value)
        {
            if (target == null) return;

            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null) return;

            try { field.SetValue(target, value); }
            catch { /* ignore */ }
        }
    }
}
