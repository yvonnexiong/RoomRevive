using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SetupManager aligns GameObject 1 so that GameObject 2 fits exactly into the same
/// world position and rotation as GameObject 3.
///
/// Typical setup:
/// - gameObject1 = the root object you want to move/rotate.
/// - gameObject2 = a reference/marker object currently attached to or positioned relative to gameObject1.
/// - gameObject3 = the target object/marker that gameObject2 should match.
///
/// Runtime B-button search:
/// - Press Right Controller B.
/// - Finds the object named gameObject3NameToFind.
/// - Searches inside that object for a child named targetChildNameInsideFoundObject, default "CEILING".
/// - Assigns that child as gameObject3.
/// - Applies alignment.
/// </summary>
[DisallowMultipleComponent]
public class SetupManager : MonoBehaviour
{
    [Header("Main References")]
    [Tooltip("The root GameObject that should be moved and rotated.")]
    public GameObject gameObject1;

    [Tooltip("The reference object/marker that should be aligned to GameObject 3.")]
    public GameObject gameObject2;

    [Tooltip("The target object/marker that GameObject 2 should fit onto.")]
    public GameObject gameObject3;

    [Header("Auto Find GameObject 3")]
    [FormerlySerializedAs("findGameObject1ByNameOnStart")]
    [Tooltip("If true, the script will still search automatically on Start after a delay. Default is false because B button is now used.")]
    public bool findGameObject3ByNameOnStart = false;

    [FormerlySerializedAs("gameObject1NameToFind")]
    [Tooltip("The scene object name to search for. The script will then search inside this object for a child named CEILING.")]
    public string gameObject3NameToFind = "";

    [Tooltip("The child name inside the found object that should become GameObject 3.")]
    public string targetChildNameInsideFoundObject = "CEILING";

    [Tooltip("How many seconds after Play starts before searching for the object, if Find GameObject 3 By Name On Start is enabled.")]
    public float findDelaySeconds = 1f;

    [Tooltip("If true, inactive scene objects and inactive children can also be found.")]
    public bool includeInactiveObjects = true;

    [FormerlySerializedAs("alignAfterGameObject1IsFound")]
    [Tooltip("If true, alignment is applied immediately after GameObject 3 is found.")]
    public bool alignAfterGameObject3IsFound = true;

    [Tooltip("If true, and the CEILING child is not found, the found parent object will be assigned as GameObject 3 instead.")]
    public bool useFoundParentIfChildIsMissing = false;

    [Header("Right Controller B Button")]
    [Tooltip("If true, pressing B on the right controller will find GameObject 3 and apply alignment.")]
    public bool triggerFindAndAlignWithRightBButton = true;

    [Tooltip("If true, pressing B will only find GameObject 3 but not apply alignment.")]
    public bool onlyFindTargetOnBButton = false;

    [Tooltip("If true, logs when the B button is pressed.")]
    public bool logBButtonPress = true;

    [Header("Update Alignment")]
    [Tooltip("If true, the alignment runs every Update.")]
    public bool runAlignmentInUpdate = false;

    [Tooltip("If true, Update alignment also runs in the editor while not playing.")]
    public bool runUpdateAlignmentInEditor = false;

    [Header("Apply Settings")]
    public bool applyPosition = true;
    public bool applyRotation = true;

    [Header("Measured Result")]
    public Vector3 calculatedGameObject1Position;
    public Vector3 calculatedGameObject1EulerRotation;
    public Vector3 positionToAdd;
    public Vector3 rotationToAddEuler;

    private Quaternion calculatedGameObject1Rotation;
    private Quaternion rotationToAdd;

    private bool runAlignmentOnceNextUpdate = false;

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (findGameObject3ByNameOnStart)
        {
            Invoke(nameof(FindGameObject3ByNameDelayed), findDelaySeconds);
        }
    }

    private void Update()
    {
        CheckRightControllerBButton();

        if (!Application.isPlaying && !runUpdateAlignmentInEditor)
            return;

        if (runAlignmentInUpdate || runAlignmentOnceNextUpdate)
        {
            ApplyAlignment(false);
            runAlignmentOnceNextUpdate = false;
        }
    }

    private void CheckRightControllerBButton()
    {
        if (!Application.isPlaying)
            return;

        if (!triggerFindAndAlignWithRightBButton)
            return;

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            if (logBButtonPress)
            {
                Debug.Log($"{nameof(SetupManager)}: Right controller B button pressed.", this);
            }

            FindGameObject3ByNameNow();

            if (!onlyFindTargetOnBButton && gameObject3 != null)
            {
                ApplyAlignment(true);
            }
        }
    }

    /// <summary>
    /// Call this method from another script or from a UnityEvent/Button
    /// to start running the position/rotation alignment every Update.
    /// </summary>
    public void StartRunningAlignmentInUpdate()
    {
        runAlignmentInUpdate = true;
    }

    /// <summary>
    /// Call this method to stop running the alignment every Update.
    /// </summary>
    public void StopRunningAlignmentInUpdate()
    {
        runAlignmentInUpdate = false;
    }

    /// <summary>
    /// Call this if you only want the alignment to run once during the next Update.
    /// </summary>
    public void RunAlignmentOnceInNextUpdate()
    {
        runAlignmentOnceNextUpdate = true;
    }

    /// <summary>
    /// Call this if you want to instantly apply the alignment now.
    /// </summary>
    public void RunAlignmentNow()
    {
        ApplyAlignment(true);
    }

    /// <summary>
    /// Call this from another script if you want the same behavior as pressing B.
    /// </summary>
    public void FindTargetAndAlignNow()
    {
        FindGameObject3ByNameNow();

        if (gameObject3 != null)
        {
            ApplyAlignment(true);
        }
    }

    private void FindGameObject3ByNameDelayed()
    {
        FindGameObject3ByNameNow();

        if (gameObject3 != null && alignAfterGameObject3IsFound)
        {
            ApplyAlignment(true);
        }
    }

    [ContextMenu("Find GameObject 3 By Name Now")]
    public void FindGameObject3ByNameNow()
    {
        if (string.IsNullOrWhiteSpace(gameObject3NameToFind))
        {
            Debug.LogWarning($"{nameof(SetupManager)}: No GameObject 3 parent name has been set.", this);
            return;
        }

        GameObject foundParentObject = FindSceneGameObjectByName(gameObject3NameToFind, includeInactiveObjects);

        if (foundParentObject == null)
        {
            Debug.LogWarning($"{nameof(SetupManager)}: Could not find a GameObject named '{gameObject3NameToFind}' in the scene.", this);
            return;
        }

        GameObject foundChildObject = FindChildGameObjectByName(
            foundParentObject,
            targetChildNameInsideFoundObject,
            includeInactiveObjects
        );

        if (foundChildObject != null)
        {
            gameObject3 = foundChildObject;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            Debug.Log(
                $"{nameof(SetupManager)}: Found parent '{foundParentObject.name}', then assigned child '{foundChildObject.name}' as GameObject 3.",
                this
            );

            return;
        }

        if (useFoundParentIfChildIsMissing)
        {
            gameObject3 = foundParentObject;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            Debug.LogWarning(
                $"{nameof(SetupManager)}: Found parent '{foundParentObject.name}', but no child named '{targetChildNameInsideFoundObject}' was found. Assigned parent as GameObject 3 instead.",
                this
            );

            return;
        }

        Debug.LogWarning(
            $"{nameof(SetupManager)}: Found parent '{foundParentObject.name}', but could not find a child named '{targetChildNameInsideFoundObject}'. GameObject 3 was not changed.",
            this
        );
    }

    [ContextMenu("Calculate Needed Offset")]
    public void CalculateNeededOffset()
    {
        if (!TryCalculateAlignment())
            return;

        Debug.Log(
            $"{nameof(SetupManager)} calculated alignment:\n" +
            $"Position to add: {positionToAdd}\n" +
            $"Rotation to add: {rotationToAddEuler}\n" +
            $"New GameObject 1 position: {calculatedGameObject1Position}\n" +
            $"New GameObject 1 rotation: {calculatedGameObject1EulerRotation}",
            this
        );
    }

    [ContextMenu("Apply Alignment")]
    public void ApplyAlignmentFromContextMenu()
    {
        ApplyAlignment(true);
    }

    public void ApplyAlignment(bool logResult)
    {
        if (!TryCalculateAlignment())
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(gameObject1.transform, "Apply SetupManager Alignment");
        }
#endif

        if (applyPosition)
        {
            gameObject1.transform.position = calculatedGameObject1Position;
        }

        if (applyRotation)
        {
            gameObject1.transform.rotation = calculatedGameObject1Rotation;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(gameObject1.transform);
        }
#endif

        if (logResult)
        {
            Debug.Log($"{nameof(SetupManager)}: Alignment applied. GameObject 2 now fits onto GameObject 3.", this);
        }
    }

    private bool TryCalculateAlignment()
    {
        if (gameObject1 == null)
        {
            Debug.LogWarning($"{nameof(SetupManager)}: GameObject 1 is missing.", this);
            return false;
        }

        if (gameObject2 == null)
        {
            Debug.LogWarning($"{nameof(SetupManager)}: GameObject 2 is missing.", this);
            return false;
        }

        if (gameObject3 == null)
        {
            Debug.LogWarning($"{nameof(SetupManager)}: GameObject 3 is missing.", this);
            return false;
        }

        Transform root = gameObject1.transform;
        Transform source = gameObject2.transform;
        Transform target = gameObject3.transform;

        Quaternion rootToSourceRotation = Quaternion.Inverse(root.rotation) * source.rotation;
        Vector3 rootToSourcePosition = Quaternion.Inverse(root.rotation) * (source.position - root.position);

        calculatedGameObject1Rotation = target.rotation * Quaternion.Inverse(rootToSourceRotation);
        calculatedGameObject1Position = target.position - calculatedGameObject1Rotation * rootToSourcePosition;

        rotationToAdd = calculatedGameObject1Rotation * Quaternion.Inverse(root.rotation);
        positionToAdd = calculatedGameObject1Position - root.position;

        calculatedGameObject1EulerRotation = calculatedGameObject1Rotation.eulerAngles;
        rotationToAddEuler = NormalizeEuler(rotationToAdd.eulerAngles);

        return true;
    }

    private GameObject FindSceneGameObjectByName(string objectName, bool includeInactive)
    {
        if (!includeInactive)
        {
            return GameObject.Find(objectName);
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.name != objectName)
                continue;

            if (!obj.scene.IsValid())
                continue;

            if (obj.hideFlags != HideFlags.None)
                continue;

            Scene scene = obj.scene;

            if (!scene.isLoaded)
                continue;

            return obj;
        }

        return null;
    }

    private GameObject FindChildGameObjectByName(GameObject parentObject, string childName, bool includeInactive)
    {
        if (parentObject == null)
            return null;

        if (string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] childTransforms = parentObject.GetComponentsInChildren<Transform>(includeInactive);

        foreach (Transform childTransform in childTransforms)
        {
            if (childTransform == parentObject.transform)
                continue;

            if (childTransform.name == childName)
            {
                return childTransform.gameObject;
            }
        }

        return null;
    }

    private Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z)
        );
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle > 180f)
            angle -= 360f;

        if (angle < -180f)
            angle += 360f;

        return angle;
    }
}