// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using RoomRevive;
using RoomRevive.ProductBrowser;
using UnityEngine;

namespace GaussianSplatting.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GaussianArcCutoutManager : MonoBehaviour
    {
        public static GaussianArcCutoutManager Instance { get; private set; }

        public enum ManagerUpdateMode
        {
            Update,
            LateUpdate,
            ManualOnly
        }
        //Ok

        public enum ObjectVisibilityDisableMode
        {
            DisableGameObject,
            DisableRenderersOnly
        }

        [Serializable]
        public class ArcCutoutBinding
        {
            [Header("Cutout")]
            public string name;
            public GaussianCutout cutout;
            public bool active = true;

            [Header("Fire From")]
            [Tooltip("The arc cutout will be positioned from this transform. The transform's local +Z is the forward direction.")]
            public Transform fireFrom;

            [Tooltip("If true and fireFrom is missing, the manager defaultFireFrom will be used.")]
            public bool useDefaultFireFrom = true;

            public bool followFireFrom = true;
            public bool followPosition = true;
            public bool followRotation = true;

            [Tooltip("Local position offset from the fireFrom transform.")]
            public Vector3 localPositionOffset = Vector3.zero;

            [Tooltip("Local rotation offset. If useFixedCutoutRotation is enabled, this is added after fixedConstantRotation.")]
            public Vector3 localEulerOffset = Vector3.zero;

            [Header("Arc Settings")]
            public bool forceArcType = true;

            public bool overrideSweepDirection = false;
            public GaussianCutout.ArcSweepDirection sweepDirection =
                GaussianCutout.ArcSweepDirection.LeftToRight;

            public bool overrideInvert = false;
            public bool invert = false;

            public bool zeroAngleDisablesArc = true;
        }

        private struct DisableArcData
        {
            public Vector3 center;
            public Vector3 up;
            public Vector3 forward;
            public float radius;
            public float fullArcAngleDeg;
            public float filledArcAngleDeg;
            public bool valid;
        }

        [Header("3D Arc Slider Input")]
        [Tooltip("Optional: assign the MetaGrabbableArcSliderAxisConstraint from the grabbed ball. If assigned, this manager reads its value01 and uses it as reveal01.")]
        public MetaGrabbableArcSliderAxisConstraint arcSliderValueSource;

        [Header("Reveal Control")]
        [Tooltip("If enabled, the manager uses reveal01 instead of revealAngleDeg.")]
        public bool useNormalizedReveal = false;

        [Range(0f, 1f)]
        public float reveal01 = 0.5f;

        [Range(0f, 360f)]
        public float revealAngleDeg = 180f;

        [Header("Angle Limits")]
        public float minRevealAngleDeg = 0f;
        public float maxRevealAngleDeg = 360f;

        [Header("Snapping")]
        public bool snapToStartAndEnd = true;

        [Tooltip("If the reveal angle is within this many degrees of 0 or 360, it snaps to 0 or 360.")]
        [Range(0f, 45f)]
        public float snapThresholdDeg = 15f;

        [Header("Fire From Defaults")]
        [Tooltip("Used when a binding has no fireFrom assigned and useDefaultFireFrom is enabled.")]
        public Transform defaultFireFrom;

        [Header("Fixed Cutout Rotation")]
        [Tooltip("If enabled, all cutouts use fixedConstantRotation as their world rotation instead of fireFrom.rotation.")]
        public bool useFixedCutoutRotation = true;

        [Tooltip("Fixed world rotation for all cutouts.")]
        public Vector3 fixedConstantRotation = Vector3.zero;

        [Tooltip("If enabled, each binding's localEulerOffset is added after fixedConstantRotation.")]
        public bool addBindingLocalEulerOffsetToFixedRotation = true;

        [Header("Manager-Decided 3D Slider Arc Frame")]
        [Tooltip("If enabled, the 3D grabbable sphere reads its arc frame from this manager.")]
        public bool drive3DSliderArcFrame = true;

        [Tooltip("If enabled, the 3D slider arc ignores X and Z rotation from defaultFireFrom and only uses its Y rotation.")]
        public bool sliderArcUseOnlyDefaultFireFromYRotation = true;

        [Tooltip("Extra world-space height offset for the slider arc center.")]
        public float sliderArcWorldHeightOffset = 0f;

        [Tooltip("Extra Y rotation added on top of defaultFireFrom Y rotation.")]
        public float sliderArcYawOffsetDeg = 0f;

        [Header("Master Slider / Cutout GameObject Transform")]
        [Tooltip("Every GameObject in cutoutGameObjects will be placed at defaultFireFrom.position.")]
        public bool placeCutoutGameObjectsAtDefaultFireFromPosition = true;

        [Tooltip("Every GameObject in cutoutGameObjects will rotate from defaultFireFrom.rotation + masterYRotationOffsetDeg.")]
        public bool rotateCutoutGameObjectsFromDefaultFireFrom = true;

        [Tooltip("Adds this many degrees around Y on top of defaultFireFrom.rotation for every GameObject in cutoutGameObjects.")]
        public float masterYRotationOffsetDeg = 0f;

        [Tooltip("Optional local position offset from defaultFireFrom. Leave at zero if the GameObjects should sit exactly at defaultFireFrom.position.")]
        public Vector3 cutoutGameObjectLocalPositionOffset = Vector3.zero;

        [Tooltip("Extra rotation added after defaultFireFrom.rotation and masterYRotationOffsetDeg. Use this if your slider/cutout GameObjects face the wrong way.")]
        public Vector3 cutoutGameObjectExtraEulerOffset = Vector3.zero;

        [Tooltip("Drag the slider / cutout / cutoff GameObjects here. These GameObjects will receive the defaultFireFrom position and master Y rotation offset.")]
        public List<GameObject> cutoutGameObjects = new List<GameObject>();

        [Header("Update")]
        public ManagerUpdateMode updateMode = ManagerUpdateMode.LateUpdate;

        [Tooltip("Apply while not in Play Mode. Useful when positioning cutouts in the editor.")]
        public bool applyInEditMode = true;

        [Tooltip("Apply continuously. If disabled, call ApplyNow() manually.")]
        public bool applyContinuously = true;

        [Header("Cutting / Disabling Guards")]
        [Tooltip("If disabled, the manager will not apply Gaussian cutouts or disable/hide GameObjects while in the Unity Editor. Slider/cutout visual GameObjects can still be positioned.")]
        public bool allowCuttingAndDisablingInEditMode = true;

        [Tooltip("If disabled, the manager will not apply Gaussian cutouts or disable/hide GameObjects while the app is running. Slider/cutout visual GameObjects can still be positioned.")]
        public bool allowCuttingAndDisablingAtRuntime = true;

        [Header("Slider Synchronization")]
        [Tooltip("Keep every linked BeforeAfterSlider synchronized with this manager's resolved reveal value.")]
        public bool syncLinkedSliders = true;

        [Header("Cutouts")]
        public List<ArcCutoutBinding> arcCutouts = new List<ArcCutoutBinding>();

        [Header("GameObject Visibility")]
        [Tooltip("When enabled, the listed GameObjects are disabled while the resolved reveal angle is below disableBelowAngleDeg.")]
        public bool disableGameObjectsBelowAngle = false;

        [Range(0f, 360f)]
        public float disableBelowAngleDeg = 0f;

        [Tooltip("When enabled, listed GameObjects are hidden when their transform position is inside the current arc cutout area.")]
        public bool disableGameObjectsInsideCutoutArea = true;

        [Tooltip("Choose whether to disable the full GameObject or only its Renderer visuals.")]
        public ObjectVisibilityDisableMode objectVisibilityDisableMode =
            ObjectVisibilityDisableMode.DisableRenderersOnly;

        [Tooltip("If true, child Renderers are also found when children are inactive.")]
        public bool includeInactiveRenderersWhenDisablingVisuals = true;

        [Tooltip("If enabled and arcSliderValueSource is assigned, the object-disable arc uses the exact same center/radius/angle/yaw math as the pink 3D slider arc.")]
        public bool matchObjectDisableArcTo3DSliderArc = true;

        [Tooltip("False = starts left and grows clockwise to the right. True = starts right and grows counterclockwise to the left.")]
        public bool reverseObjectDisableCutoutDirection = false;

        [Tooltip("Extra degrees added to the cutout-area test, so objects near the edge do not flicker.")]
        [Range(0f, 45f)]
        public float cutoutAreaVisibilityPaddingDeg = 2f;

        [Tooltip("If true, m_Invert on the GaussianCutout also inverts the object disable area.")]
        public bool respectCutoutInvertForObjectVisibility = true;

        [Tooltip("GameObjects controlled by the reveal angle and by inside-cutout-area visibility. Keep this manager on a separate GameObject so it remains able to re-enable them.")]
        public List<GameObject> gameObjectsToDisableBelowAngle = new List<GameObject>();

        [Header("Object Disable Arc Gizmos")]
        public bool drawObjectDisableCutoutArea = true;

        [Tooltip("Draws the full matched arc shape. This should match the pink slider arc.")]
        public bool drawFullMatchedArc = true;

        [Tooltip("Draws only the value-filled area that is used to hide objects.")]
        public bool drawFilledDisableArea = true;

        public bool drawObjectDisableTransformTestPoints = true;

        [Tooltip("Used only when no arcSliderValueSource is assigned. When the slider source is assigned, its radius is used instead.")]
        [Min(0.05f)]
        public float fallbackObjectDisableCutoutAreaDebugRadius = 2f;

        [Range(4, 128)]
        public int objectDisableCutoutAreaDebugSegments = 64;

        public Color fullMatchedArcGizmoColor =
            new Color(1f, 0f, 1f, 1f);

        public Color filledDisableAreaGizmoColor =
            new Color(1f, 0.35f, 0f, 1f);

        [Min(0.005f)]
        public float objectDisableTransformTestPointRadius = 0.05f;

        public Color objectInsideDisableAreaColor = Color.red;
        public Color objectOutsideDisableAreaColor = Color.green;

        [Header("Product Browser Visibility")]
        [Tooltip("Product Browser Controllers whose currently selected 3D models should follow the same below-angle visibility rule.")]
        public List<ProductBrowserController> productBrowserControllers =
            new List<ProductBrowserController>();

        [Tooltip("Automatically keeps productBrowserControllers populated with every Product Browser Controller in the loaded scene, including inactive ones.")]
        public bool autoFindProductBrowserControllers = true;

        private readonly Dictionary<Renderer, bool> originalRendererEnabledStates =
            new Dictionary<Renderer, bool>();

        private readonly Dictionary<GameObject, bool> originalGameObjectActiveStates =
            new Dictionary<GameObject, bool>();

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "Only one GaussianArcCutoutManager should be active at a time.",
                    this
                );
                return;
            }

            Instance = this;

            if (autoFindProductBrowserControllers)
            {
                RefreshProductBrowserControllerReferences();
            }

            ReadReveal01FromArcSliderValueSource();
            SyncLinkedSliderValues();

            if (Application.isPlaying || applyInEditMode)
            {
                ApplyNow();
            }
            else
            {
                ApplyDefaultFireFromTransformToCutoutGameObjects();
                EnforceCuttingAndDisablingGuardIfNeeded();
            }
        }

        private void OnDisable()
        {
            RestoreAllTrackedRendererStates();
            RestoreAllTrackedGameObjectActiveStates();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static GaussianArcCutoutManager GetOrFindInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GaussianArcCutoutManager manager =
                FindFirstObjectByType<GaussianArcCutoutManager>();

            if (manager != null)
            {
                Instance = manager;
            }

            return manager;
        }

        private void Update()
        {
            if (updateMode != ManagerUpdateMode.Update)
            {
                return;
            }

            Tick();
        }

        private void LateUpdate()
        {
            if (updateMode != ManagerUpdateMode.LateUpdate)
            {
                return;
            }

            Tick();
        }

        private void OnValidate()
        {
            minRevealAngleDeg = Mathf.Clamp(minRevealAngleDeg, 0f, 360f);
            maxRevealAngleDeg = Mathf.Clamp(maxRevealAngleDeg, 0f, 360f);

            if (maxRevealAngleDeg < minRevealAngleDeg)
            {
                maxRevealAngleDeg = minRevealAngleDeg;
            }

            reveal01 = Mathf.Clamp01(reveal01);

            revealAngleDeg = Mathf.Clamp(
                revealAngleDeg,
                minRevealAngleDeg,
                maxRevealAngleDeg
            );

            disableBelowAngleDeg = Mathf.Clamp(disableBelowAngleDeg, 0f, 360f);

            cutoutAreaVisibilityPaddingDeg =
                Mathf.Clamp(cutoutAreaVisibilityPaddingDeg, 0f, 45f);

            fallbackObjectDisableCutoutAreaDebugRadius =
                Mathf.Max(0.05f, fallbackObjectDisableCutoutAreaDebugRadius);

            objectDisableCutoutAreaDebugSegments =
                Mathf.Clamp(objectDisableCutoutAreaDebugSegments, 4, 128);

            objectDisableTransformTestPointRadius =
                Mathf.Max(0.005f, objectDisableTransformTestPointRadius);

            if (autoFindProductBrowserControllers)
            {
                RefreshProductBrowserControllerReferences();
            }

            ReadReveal01FromArcSliderValueSource();
            SyncLinkedSliderValues();

            if (Application.isPlaying || applyInEditMode)
            {
                ApplyNow();
            }
            else
            {
                ApplyDefaultFireFromTransformToCutoutGameObjects();
                EnforceCuttingAndDisablingGuardIfNeeded();
            }
        }

        private void Tick()
        {
            if (!applyContinuously)
            {
                return;
            }

            if (!Application.isPlaying && !applyInEditMode)
            {
                EnforceCuttingAndDisablingGuardIfNeeded();
                return;
            }

            ApplyNow();
        }

        public void SetRevealAngle(float angleDeg)
        {
            useNormalizedReveal = false;

            revealAngleDeg = Mathf.Clamp(
                angleDeg,
                minRevealAngleDeg,
                maxRevealAngleDeg
            );

            ApplyNow();
            SyncLinkedSliderValues();
        }

        public void SetReveal01(float value)
        {
            useNormalizedReveal = true;

            reveal01 = Mathf.Clamp01(value);

            ApplyNow();
            SyncLinkedSliderValues();
        }

        public void SetMasterYRotationOffset(float yRotationDeg)
        {
            masterYRotationOffsetDeg = yRotationDeg;
            ApplyNow();
        }

        public void AddMasterYRotationOffset(float deltaDeg)
        {
            masterYRotationOffsetDeg += deltaDeg;
            ApplyNow();
        }

        public void HideAll()
        {
            SetRevealAngle(0f);
        }

        public void RevealAll()
        {
            SetRevealAngle(360f);
        }

        [ContextMenu("Refresh Product Browser Controller References")]
        public void RefreshProductBrowserControllerReferences()
        {
            ProductBrowserController[] controllers =
                FindObjectsByType<ProductBrowserController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            productBrowserControllers.Clear();

            for (int i = 0; i < controllers.Length; i++)
            {
                ProductBrowserController controller = controllers[i];

                if (controller != null && controller.gameObject.scene.IsValid())
                {
                    productBrowserControllers.Add(controller);
                }
            }
        }

        public void ApplyNow()
        {
            ReadReveal01FromArcSliderValueSource();

            float sharedRevealAngle = GetSharedRevealAngle();
            bool allowCuttingAndDisabling = ShouldApplyCuttingAndDisablingNow();

            if (allowCuttingAndDisabling)
            {
                for (int i = 0; i < arcCutouts.Count; i++)
                {
                    ApplyBinding(arcCutouts[i], sharedRevealAngle);
                }
            }
            else
            {
                DisableAllManagedCutouts();
            }

            ApplyDefaultFireFromTransformToCutoutGameObjects();

            if (allowCuttingAndDisabling)
            {
                ApplyGameObjectVisibility(sharedRevealAngle);
            }
            else
            {
                RestoreAllManagedVisibility();
            }
        }

        private bool ShouldApplyCuttingAndDisablingNow()
        {
            if (Application.isPlaying)
            {
                return allowCuttingAndDisablingAtRuntime;
            }

            return allowCuttingAndDisablingInEditMode;
        }

        private void EnforceCuttingAndDisablingGuardIfNeeded()
        {
            if (ShouldApplyCuttingAndDisablingNow())
            {
                return;
            }

            DisableAllManagedCutouts();
            RestoreAllManagedVisibility();
        }

        private void DisableAllManagedCutouts()
        {
            if (arcCutouts == null)
            {
                return;
            }

            for (int i = 0; i < arcCutouts.Count; i++)
            {
                ArcCutoutBinding binding = arcCutouts[i];

                if (binding == null || binding.cutout == null)
                {
                    continue;
                }

                if (binding.cutout.enabled)
                {
                    binding.cutout.enabled = false;
                }
            }
        }

        private void RestoreAllManagedVisibility()
        {
            SetProductBrowserModelsDisabled(false);
            RestoreAllTrackedRendererStates();
            RestoreAllTrackedGameObjectActiveStates();
        }

        public float GetNormalizedRevealValue()
        {
            ReadReveal01FromArcSliderValueSource();

            float range = maxRevealAngleDeg - minRevealAngleDeg;

            if (range <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                (GetSharedRevealAngle() - minRevealAngleDeg) / range
            );
        }

        public void ReadReveal01FromArcSliderValueSource()
        {
            if (arcSliderValueSource == null)
            {
                return;
            }

            useNormalizedReveal = true;
            reveal01 = Mathf.Clamp01(arcSliderValueSource.value01);
        }

        public bool TryGetManagerDecidedSliderArcFrame(
            out Vector3 centerPosition,
            out Quaternion centerRotation,
            out Vector3 up,
            out Vector3 forward,
            out Vector3 right
        )
        {
            centerPosition = transform.position;
            centerRotation = Quaternion.identity;
            up = Vector3.up;
            forward = Vector3.forward;
            right = Vector3.right;

            if (!drive3DSliderArcFrame)
            {
                return false;
            }

            if (defaultFireFrom == null)
            {
                return false;
            }

            centerPosition =
                defaultFireFrom.TransformPoint(cutoutGameObjectLocalPositionOffset) +
                Vector3.up * sliderArcWorldHeightOffset;

            if (sliderArcUseOnlyDefaultFireFromYRotation)
            {
                float defaultFireFromY = defaultFireFrom.eulerAngles.y;

                centerRotation =
                    Quaternion.Euler(
                        0f,
                        defaultFireFromY + sliderArcYawOffsetDeg,
                        0f
                    );

                up = Vector3.up;
                forward = centerRotation * Vector3.forward;
                right = centerRotation * Vector3.right;

                return true;
            }

            centerRotation =
                defaultFireFrom.rotation *
                Quaternion.Euler(0f, sliderArcYawOffsetDeg, 0f);

            up = centerRotation * Vector3.up;
            forward = centerRotation * Vector3.forward;
            right = centerRotation * Vector3.right;

            return true;
        }

        [ContextMenu("Synchronize Linked Slider Values")]
        public void SyncLinkedSliderValues()
        {
            if (!syncLinkedSliders)
            {
                return;
            }

            float normalizedValue = GetNormalizedRevealValue();

            BeforeAfterSlider[] sliders = FindObjectsByType<BeforeAfterSlider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < sliders.Length; i++)
            {
                BeforeAfterSlider linkedSlider = sliders[i];

                if (linkedSlider != null &&
                    linkedSlider.gameObject.scene.IsValid() &&
                    linkedSlider.UsesCutoutManager(this))
                {
                    linkedSlider.SetValueFromCutoutManager(normalizedValue);
                }
            }
        }

        private void ApplyDefaultFireFromTransformToCutoutGameObjects()
        {
            if (defaultFireFrom == null)
            {
                return;
            }

            Vector3 targetPosition =
                defaultFireFrom.TransformPoint(cutoutGameObjectLocalPositionOffset);

            Quaternion targetRotation;

            if (sliderArcUseOnlyDefaultFireFromYRotation)
            {
                targetRotation =
                    Quaternion.Euler(
                        0f,
                        defaultFireFrom.eulerAngles.y + masterYRotationOffsetDeg,
                        0f
                    ) *
                    Quaternion.Euler(cutoutGameObjectExtraEulerOffset);
            }
            else
            {
                targetRotation =
                    defaultFireFrom.rotation *
                    Quaternion.Euler(0f, masterYRotationOffsetDeg, 0f) *
                    Quaternion.Euler(cutoutGameObjectExtraEulerOffset);
            }

            for (int i = 0; i < cutoutGameObjects.Count; i++)
            {
                GameObject target = cutoutGameObjects[i];

                if (target == null)
                {
                    continue;
                }

                if (target == gameObject)
                {
                    continue;
                }

                Transform targetTransform = target.transform;

                if (placeCutoutGameObjectsAtDefaultFireFromPosition &&
                    rotateCutoutGameObjectsFromDefaultFireFrom)
                {
                    targetTransform.SetPositionAndRotation(
                        targetPosition,
                        targetRotation
                    );
                }
                else if (placeCutoutGameObjectsAtDefaultFireFromPosition)
                {
                    targetTransform.position = targetPosition;
                }
                else if (rotateCutoutGameObjectsFromDefaultFireFrom)
                {
                    targetTransform.rotation = targetRotation;
                }
            }
        }

        private void SetProductBrowserModelsDisabled(bool shouldBeDisabled)
        {
            for (int i = 0; i < productBrowserControllers.Count; i++)
            {
                ProductBrowserController controller = productBrowserControllers[i];

                if (controller != null)
                {
                    controller.SetCurrent3DModelDisabled(shouldBeDisabled);
                }
            }
        }

        private void ApplyGameObjectVisibility(float sharedRevealAngle)
        {
            bool shouldDisableProductModels =
                disableGameObjectsBelowAngle &&
                sharedRevealAngle < disableBelowAngleDeg;

            SetProductBrowserModelsDisabled(shouldDisableProductModels);

            bool shouldUseListVisibility =
                disableGameObjectsBelowAngle ||
                disableGameObjectsInsideCutoutArea;

            if (!shouldUseListVisibility)
            {
                return;
            }

            for (int i = 0; i < gameObjectsToDisableBelowAngle.Count; i++)
            {
                GameObject target = gameObjectsToDisableBelowAngle[i];

                if (target == null)
                {
                    continue;
                }

                if (target == gameObject || transform.IsChildOf(target.transform))
                {
                    continue;
                }

                bool shouldBeVisible = !true;

                if (disableGameObjectsBelowAngle &&
                    sharedRevealAngle < disableBelowAngleDeg)
                {
                    shouldBeVisible = !false;
                }

                if (disableGameObjectsInsideCutoutArea &&
                    IsGameObjectTransformInsideCurrentCutoutArea(
                        target,
                        sharedRevealAngle
                    ))
                {
                    shouldBeVisible = !false;
                }

                ApplyVisibilityToTarget(target, shouldBeVisible);
            }
        }

        private void ApplyVisibilityToTarget(GameObject target, bool shouldBeVisible)
        {
            if (target == null)
            {
                return;
            }

            if (objectVisibilityDisableMode == ObjectVisibilityDisableMode.DisableGameObject)
            {
                if (!originalGameObjectActiveStates.ContainsKey(target))
                {
                    originalGameObjectActiveStates.Add(target, target.activeSelf);
                }

                if (target.activeSelf != shouldBeVisible)
                {
                    target.SetActive(shouldBeVisible);
                }

                return;
            }

            SetTargetRenderersVisible(target, shouldBeVisible);
        }

        private void SetTargetRenderersVisible(GameObject target, bool shouldBeVisible)
        {
            if (target == null)
            {
                return;
            }

            Renderer[] renderers =
                target.GetComponentsInChildren<Renderer>(
                    includeInactiveRenderersWhenDisablingVisuals
                );

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (!originalRendererEnabledStates.ContainsKey(renderer))
                {
                    originalRendererEnabledStates.Add(renderer, renderer.enabled);
                }

                bool originalEnabled = originalRendererEnabledStates[renderer];
                bool targetEnabled = shouldBeVisible && originalEnabled;

                if (renderer.enabled != targetEnabled)
                {
                    renderer.enabled = targetEnabled;
                }
            }
        }

        private void RestoreAllTrackedRendererStates()
        {
            foreach (KeyValuePair<Renderer, bool> pair in originalRendererEnabledStates)
            {
                Renderer renderer = pair.Key;

                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = pair.Value;
            }

            originalRendererEnabledStates.Clear();
        }

        private void RestoreAllTrackedGameObjectActiveStates()
        {
            foreach (KeyValuePair<GameObject, bool> pair in originalGameObjectActiveStates)
            {
                GameObject target = pair.Key;

                if (target == null)
                {
                    continue;
                }

                if (target.activeSelf != pair.Value)
                {
                    target.SetActive(pair.Value);
                }
            }

            originalGameObjectActiveStates.Clear();
        }

        private bool IsGameObjectTransformInsideCurrentCutoutArea(
            GameObject target,
            float sharedRevealAngle
        )
        {
            if (target == null)
            {
                return false;
            }

            float normalizedValue = GetNormalizedDisableValue(sharedRevealAngle);

            if (normalizedValue <= 0.0001f)
            {
                return false;
            }

            Vector3 objectPosition = target.transform.position;

            for (int i = 0; i < arcCutouts.Count; i++)
            {
                ArcCutoutBinding binding = arcCutouts[i];

                if (binding == null || binding.cutout == null)
                {
                    continue;
                }

                if (!binding.active)
                {
                    continue;
                }

                if (IsPointInsideMatchedDisableArc(
                        objectPosition,
                        binding,
                        sharedRevealAngle
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointInsideMatchedDisableArc(
            Vector3 worldPoint,
            ArcCutoutBinding binding,
            float sharedRevealAngle
        )
        {
            DisableArcData arcData =
                GetDisableArcData(binding, sharedRevealAngle);

            if (!arcData.valid)
            {
                return false;
            }

            if (arcData.filledArcAngleDeg <= 0.01f)
            {
                return false;
            }

            if (arcData.filledArcAngleDeg >= arcData.fullArcAngleDeg - 0.01f)
            {
                //return !ShouldInvertCutoutArea(binding.cutout);
            }

            Vector3 toPoint =
                Vector3.ProjectOnPlane(worldPoint - arcData.center, arcData.up);

            if (toPoint.sqrMagnitude <= 0.000001f)
            {
                return true;
            }

            Vector3 pointDirection = toPoint.normalized;

            float signedAngle =
                Vector3.SignedAngle(arcData.forward, pointDirection, arcData.up);

            float halfFullAngle = arcData.fullArcAngleDeg * 0.5f;

            bool insideFullArc =
                signedAngle >= -halfFullAngle &&
                signedAngle <= halfFullAngle;

            if (!insideFullArc)
            {
                //return ShouldInvertCutoutArea(binding.cutout);
            }

            float startAngle;
            float endAngle;

            if (reverseObjectDisableCutoutDirection)
            {
                startAngle = halfFullAngle - arcData.filledArcAngleDeg;
                endAngle = halfFullAngle;
            }
            else
            {
                startAngle = -halfFullAngle;
                endAngle = -halfFullAngle + arcData.filledArcAngleDeg;
            }

            bool insideFilledArc =
                signedAngle >= startAngle &&
                signedAngle <= endAngle;

            /*if (ShouldInvertCutoutArea(binding.cutout))
            {
                insideFilledArc = !insideFilledArc;
            }*/

            return insideFilledArc;
        }

        private DisableArcData GetDisableArcData(
            ArcCutoutBinding binding,
            float sharedRevealAngle
        )
        {
            DisableArcData data = new DisableArcData
            {
                center = Vector3.zero,
                up = Vector3.up,
                forward = Vector3.forward,
                radius = fallbackObjectDisableCutoutAreaDebugRadius,
                fullArcAngleDeg = 360f,
                filledArcAngleDeg = 0f,
                valid = false
            };

            float normalizedValue = GetNormalizedDisableValue(sharedRevealAngle);

            if (matchObjectDisableArcTo3DSliderArc &&
                arcSliderValueSource != null &&
                arcSliderValueSource.constraintMode ==
                MetaGrabbableArcSliderAxisConstraint.ConstraintMode.ArcAroundCenter)
            {
                if (TryGetArcFrameFromArcSliderValueSource(
                        out Vector3 center,
                        out Vector3 up,
                        out Vector3 forward
                    ))
                {
                    data.center = center;
                    data.up = up.normalized;
                    data.forward = forward.normalized;
                    data.radius = Mathf.Max(0.01f, arcSliderValueSource.radius);
                    data.fullArcAngleDeg = Mathf.Clamp(
                        arcSliderValueSource.arcAngleDeg,
                        1f,
                        360f
                    );

                    data.filledArcAngleDeg =
                        Mathf.Clamp(
                            normalizedValue * data.fullArcAngleDeg,
                            0f,
                            data.fullArcAngleDeg
                        );

                    if (data.filledArcAngleDeg > 0.01f)
                    {
                        data.filledArcAngleDeg =
                            Mathf.Clamp(
                                data.filledArcAngleDeg +
                                cutoutAreaVisibilityPaddingDeg,
                                0f,
                                data.fullArcAngleDeg
                            );
                    }

                    data.valid = true;
                    return data;
                }
            }

            if (binding == null || binding.cutout == null)
            {
                return data;
            }

            Transform cutoutTransform = binding.cutout.transform;

            Vector3 fallbackUp = Vector3.up;

            Vector3 fallbackForward =
                Vector3.ProjectOnPlane(cutoutTransform.forward, fallbackUp);

            if (fallbackForward.sqrMagnitude <= 0.000001f)
            {
                fallbackForward = Vector3.forward;
            }

            fallbackForward.Normalize();

            data.center = cutoutTransform.position;
            data.up = fallbackUp;
            data.forward = fallbackForward;
            data.radius = fallbackObjectDisableCutoutAreaDebugRadius;
            data.fullArcAngleDeg = Mathf.Clamp(maxRevealAngleDeg, 1f, 360f);

            data.filledArcAngleDeg =
                Mathf.Clamp(
                    normalizedValue * data.fullArcAngleDeg,
                    0f,
                    data.fullArcAngleDeg
                );

            if (data.filledArcAngleDeg > 0.01f)
            {
                data.filledArcAngleDeg =
                    Mathf.Clamp(
                        data.filledArcAngleDeg +
                        cutoutAreaVisibilityPaddingDeg,
                        0f,
                        data.fullArcAngleDeg
                    );
            }

            data.valid = true;
            return data;
        }

        private bool TryGetArcFrameFromArcSliderValueSource(
            out Vector3 center,
            out Vector3 up,
            out Vector3 forward
        )
        {
            center = Vector3.zero;
            up = Vector3.up;
            forward = Vector3.forward;

            if (arcSliderValueSource == null)
            {
                return false;
            }

            Transform centerTransform =
                GetCenterTransformForArcSliderValueSource();

            if (centerTransform == null)
            {
                return false;
            }

            up = GetSafeSliderUpAxis();

            if (arcSliderValueSource.forceHorizontalPlane &&
                arcSliderValueSource.useHorizontalLocalOffset)
            {
                Vector3 horizontalForward =
                    Vector3.ProjectOnPlane(centerTransform.forward, up);

                if (horizontalForward.sqrMagnitude <= 0.000001f)
                {
                    horizontalForward = Vector3.forward;
                }

                horizontalForward.Normalize();

                Vector3 horizontalRight =
                    Vector3.ProjectOnPlane(centerTransform.right, up);

                if (horizontalRight.sqrMagnitude <= 0.000001f)
                {
                    horizontalRight = Vector3.Cross(up, horizontalForward);
                }

                horizontalRight.Normalize();

                center =
                    centerTransform.position +
                    horizontalRight * arcSliderValueSource.centerLocalOffset.x +
                    up * arcSliderValueSource.centerLocalOffset.y +
                    horizontalForward * arcSliderValueSource.centerLocalOffset.z +
                    up * arcSliderValueSource.worldHeightOffset;

                forward = horizontalForward;
            }
            else
            {
                center =
                    centerTransform.TransformPoint(
                        arcSliderValueSource.centerLocalOffset
                    ) +
                    up * arcSliderValueSource.worldHeightOffset;

                Quaternion centerRotation = centerTransform.rotation;

                forward =
                    Vector3.ProjectOnPlane(
                        centerRotation * Vector3.forward,
                        up
                    );

                if (forward.sqrMagnitude <= 0.000001f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();
            }

            forward =
                Quaternion.AngleAxis(
                    arcSliderValueSource.yawOffsetDeg,
                    up
                ) *
                forward;

            forward = Vector3.ProjectOnPlane(forward, up);

            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            return true;
        }

        private Transform GetCenterTransformForArcSliderValueSource()
        {
            if (arcSliderValueSource == null)
            {
                return null;
            }

            if (arcSliderValueSource.centerMode ==
                MetaGrabbableArcSliderAxisConstraint.CenterMode.ManualCenter)
            {
                return arcSliderValueSource.manualCenter;
            }

            if (arcSliderValueSource.centerMode ==
                MetaGrabbableArcSliderAxisConstraint.CenterMode.CutoutManagerDefaultFireFrom)
            {
                if (defaultFireFrom != null)
                {
                    return defaultFireFrom;
                }

                if (arcSliderValueSource.cutoutManager != null &&
                    arcSliderValueSource.cutoutManager.defaultFireFrom != null)
                {
                    return arcSliderValueSource.cutoutManager.defaultFireFrom;
                }
            }

            if (arcSliderValueSource.centerMode ==
                MetaGrabbableArcSliderAxisConstraint.CenterMode.MainCamera)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                {
                    return mainCamera.transform;
                }
            }

            if (arcSliderValueSource.manualCenter != null)
            {
                return arcSliderValueSource.manualCenter;
            }

            if (defaultFireFrom != null)
            {
                return defaultFireFrom;
            }

            Camera fallbackCamera = Camera.main;

            if (fallbackCamera != null)
            {
                return fallbackCamera.transform;
            }

            return null;
        }

        private Vector3 GetSafeSliderUpAxis()
        {
            if (arcSliderValueSource == null)
            {
                return Vector3.up;
            }

            if (arcSliderValueSource.horizontalUpAxis.sqrMagnitude <= 0.000001f)
            {
                return Vector3.up;
            }

            return arcSliderValueSource.horizontalUpAxis.normalized;
        }

        private Vector3 GetMatchedArcPoint(
            DisableArcData arcData,
            float angleDeg
        )
        {
            Vector3 direction =
                Quaternion.AngleAxis(angleDeg, arcData.up) *
                arcData.forward;

            direction =
                Vector3.ProjectOnPlane(direction, arcData.up);

            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = arcData.forward;
            }

            return arcData.center + direction.normalized * arcData.radius;
        }

        private float GetNormalizedDisableValue(float sharedRevealAngle)
        {
            float range = maxRevealAngleDeg - minRevealAngleDeg;

            if (range <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                (sharedRevealAngle - minRevealAngleDeg) / range
            );
        }
        /*
        private bool ShouldInvertCutoutArea(GaussianCutout cutout)
        {
            if (!respectCutoutInvertForObjectVisibility)
            {
                return false;
            }

            if (cutout == null)
            {
                return false;
            }

            return cutout.m_Invert;
        }*/

        private float GetSharedRevealAngle()
        {
            float angle;

            if (useNormalizedReveal)
            {
                angle = Mathf.Lerp(
                    minRevealAngleDeg,
                    maxRevealAngleDeg,
                    reveal01
                );
            }
            else
            {
                angle = revealAngleDeg;
            }

            angle = Mathf.Clamp(
                angle,
                minRevealAngleDeg,
                maxRevealAngleDeg
            );

            if (snapToStartAndEnd)
            {
                angle = SnapAngleToStartOrEnd(angle);
            }

            return angle;
        }

        private float SnapAngleToStartOrEnd(float angle)
        {
            if (Mathf.Abs(angle - minRevealAngleDeg) <= snapThresholdDeg)
            {
                return minRevealAngleDeg;
            }

            if (Mathf.Abs(angle - maxRevealAngleDeg) <= snapThresholdDeg)
            {
                return maxRevealAngleDeg;
            }

            return angle;
        }

        private Quaternion GetCutoutTargetRotation(
            Transform fireFrom,
            ArcCutoutBinding binding
        )
        {
            if (useFixedCutoutRotation)
            {
                Quaternion fixedRotation =
                    Quaternion.Euler(fixedConstantRotation);

                if (addBindingLocalEulerOffsetToFixedRotation)
                {
                    fixedRotation *= Quaternion.Euler(binding.localEulerOffset);
                }

                return fixedRotation;
            }

            return fireFrom.rotation * Quaternion.Euler(binding.localEulerOffset);
        }

        private void ApplyBinding(ArcCutoutBinding binding, float sharedRevealAngle)
        {
            if (binding == null || binding.cutout == null)
            {
                return;
            }

            GaussianCutout cutout = binding.cutout;

            cutout.enabled = binding.active;

            if (binding.forceArcType)
            {
                cutout.m_Type = GaussianCutout.Type.Arc;
            }

            cutout.m_RevealAngleDeg = sharedRevealAngle;
            cutout.m_ZeroAngleDisablesArc = binding.zeroAngleDisablesArc;

            if (binding.overrideSweepDirection)
            {
                cutout.m_ArcSweepDirection = binding.sweepDirection;
            }

            if (binding.overrideInvert)
            {
                cutout.m_Invert = binding.invert;
            }

            if (!binding.followFireFrom)
            {
                return;
            }

            Transform fireFrom = binding.fireFrom;

            if (fireFrom == null && binding.useDefaultFireFrom)
            {
                fireFrom = defaultFireFrom;
            }

            if (fireFrom == null)
            {
                return;
            }

            Transform cutoutTransform = cutout.transform;

            Vector3 targetPosition =
                fireFrom.TransformPoint(binding.localPositionOffset);

            Quaternion targetRotation =
                GetCutoutTargetRotation(fireFrom, binding);

            if (binding.followPosition && binding.followRotation)
            {
                cutoutTransform.SetPositionAndRotation(
                    targetPosition,
                    targetRotation
                );
            }
            else if (binding.followPosition)
            {
                cutoutTransform.position = targetPosition;
            }
            else if (binding.followRotation)
            {
                cutoutTransform.rotation = targetRotation;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawObjectDisableCutoutArea)
            {
                return;
            }

            float sharedRevealAngle = GetSharedRevealAngle();

            DrawObjectDisableCutoutAreaGizmos(sharedRevealAngle);
            DrawObjectDisableTransformPointGizmos(sharedRevealAngle);
        }

        private void DrawObjectDisableCutoutAreaGizmos(float sharedRevealAngle)
        {
            if (arcCutouts == null)
            {
                return;
            }

            for (int i = 0; i < arcCutouts.Count; i++)
            {
                ArcCutoutBinding binding = arcCutouts[i];

                if (binding == null || binding.cutout == null || !binding.active)
                {
                    continue;
                }

                DisableArcData arcData =
                    GetDisableArcData(binding, sharedRevealAngle);

                if (!arcData.valid)
                {
                    continue;
                }

                if (drawFullMatchedArc)
                {
                    DrawFullMatchedArcGizmo(arcData);
                }

                if (drawFilledDisableArea)
                {
                    DrawFilledDisableAreaGizmo(arcData);
                }
            }
        }

        private void DrawFullMatchedArcGizmo(DisableArcData arcData)
        {
            float halfFullAngle = arcData.fullArcAngleDeg * 0.5f;

            int segments = Mathf.Max(
                4,
                Mathf.RoundToInt(
                    objectDisableCutoutAreaDebugSegments *
                    (arcData.fullArcAngleDeg / 360f)
                )
            );

            Gizmos.color = fullMatchedArcGizmoColor;
            Gizmos.DrawWireSphere(arcData.center, 0.035f);

            Vector3 previousPoint =
                GetMatchedArcPoint(arcData, -halfFullAngle);

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float currentAngle =
                    Mathf.Lerp(-halfFullAngle, halfFullAngle, t);

                Vector3 point =
                    GetMatchedArcPoint(arcData, currentAngle);

                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }

        private void DrawFilledDisableAreaGizmo(DisableArcData arcData)
        {
            if (arcData.filledArcAngleDeg <= 0.01f)
            {
                return;
            }

            float halfFullAngle = arcData.fullArcAngleDeg * 0.5f;

            float startAngle;
            float endAngle;

            if (reverseObjectDisableCutoutDirection)
            {
                startAngle = halfFullAngle - arcData.filledArcAngleDeg;
                endAngle = halfFullAngle;
            }
            else
            {
                startAngle = -halfFullAngle;
                endAngle = -halfFullAngle + arcData.filledArcAngleDeg;
            }

            int segments = Mathf.Max(
                2,
                Mathf.RoundToInt(
                    objectDisableCutoutAreaDebugSegments *
                    (arcData.filledArcAngleDeg / 360f)
                )
            );

            Gizmos.color = filledDisableAreaGizmoColor;

            Vector3 startPoint =
                GetMatchedArcPoint(arcData, startAngle);

            Gizmos.DrawLine(arcData.center, startPoint);

            Vector3 previousPoint = startPoint;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

                Vector3 point =
                    GetMatchedArcPoint(arcData, currentAngle);

                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Gizmos.DrawLine(previousPoint, arcData.center);
        }

        private void DrawObjectDisableTransformPointGizmos(float sharedRevealAngle)
        {
            if (!drawObjectDisableTransformTestPoints)
            {
                return;
            }

            if (gameObjectsToDisableBelowAngle == null)
            {
                return;
            }

            for (int i = 0; i < gameObjectsToDisableBelowAngle.Count; i++)
            {
                GameObject target = gameObjectsToDisableBelowAngle[i];

                if (target == null)
                {
                    continue;
                }

                bool inside =
                    IsGameObjectTransformInsideCurrentCutoutArea(
                        target,
                        sharedRevealAngle
                    );

                Gizmos.color = inside
                    ? objectInsideDisableAreaColor
                    : objectOutsideDisableAreaColor;

                Gizmos.DrawSphere(
                    target.transform.position,
                    objectDisableTransformTestPointRadius
                );
            }
        }
#endif
    }
}