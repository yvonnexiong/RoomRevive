using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Reflection;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MRUKRoomBaker : MonoBehaviour
{
    [Header("Settings")]
    public GameObject sourceRoomRoot;
    public string newRoomName = "SavedMRUKRoom";
    public Material fallbackMaterial;

    [ContextMenu("Bake and Save Room")]
    public void BakeAndSave()
    {
#if UNITY_EDITOR
        if (sourceRoomRoot == null)
            sourceRoomRoot = this.gameObject;

        Debug.Log("[MRUK Baker] Starting MRUK room bake...");

        GameObject newRoot = new GameObject(newRoomName);

        if (sourceRoomRoot.GetComponent<MRUKRoom>() != null)
            newRoot.AddComponent<MRUKRoom>();

        ProcessChildren(sourceRoomRoot.transform, newRoot.transform);

        SaveToPrefab(newRoot);

        DestroyImmediate(newRoot);
#else
        Debug.LogWarning("MRUKRoomBaker: BakeAndSave only works in the Unity Editor, not on Quest/Android builds.");
#endif
    }

    private void ProcessChildren(Transform sourceParent, Transform destParent)
    {
        foreach (Transform sourceChild in sourceParent)
        {
            GameObject newChild = new GameObject(sourceChild.name);
            newChild.transform.SetParent(destParent);

            // Keep the same local transform relative to the MRUK room root.
            newChild.transform.localPosition = sourceChild.localPosition;
            newChild.transform.localRotation = sourceChild.localRotation;
            newChild.transform.localScale = Vector3.one;

            MRUKAnchor sourceAnchor = sourceChild.GetComponent<MRUKAnchor>();

            if (sourceAnchor != null)
            {
                MRUKAnchor newAnchor = newChild.AddComponent<MRUKAnchor>();
                SetLabel(newAnchor, sourceAnchor.Label);

                MeshFilter mf = sourceChild.GetComponentInChildren<MeshFilter>();
                Bounds bakedBounds = new Bounds(Vector3.zero, Vector3.one);

                if (mf != null && mf.sharedMesh != null)
                {
                    bakedBounds = CalculateTightLocalBounds(
                        newChild.transform,
                        mf.transform,
                        mf.sharedMesh
                    );

                    CreateVisualCube(
                        newChild,
                        bakedBounds,
                        IsPlaneLabel(sourceAnchor.Label)
                    );
                }

                if (IsPlaneLabel(sourceAnchor.Label))
                {
                    Rect rect = new Rect(
                        bakedBounds.min.x,
                        bakedBounds.min.y,
                        bakedBounds.size.x,
                        bakedBounds.size.y
                    );

                    SetInternalProperty(newAnchor, "PlaneRect", rect);
                }
                else
                {
                    SetInternalProperty(newAnchor, "VolumeBounds", bakedBounds);
                }
            }

            if (sourceChild.childCount > 0)
                ProcessChildren(sourceChild, newChild.transform);
        }
    }

    private Bounds CalculateTightLocalBounds(
        Transform targetAnchor,
        Transform sourceObjectTransform,
        Mesh mesh
    )
    {
        Bounds meshBounds = mesh.bounds;

        Vector3 min = meshBounds.min;
        Vector3 max = meshBounds.max;

        Vector3[] corners = new Vector3[8];

        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(max.x, min.y, min.z);
        corners[2] = new Vector3(min.x, max.y, min.z);
        corners[3] = new Vector3(max.x, max.y, min.z);
        corners[4] = new Vector3(min.x, min.y, max.z);
        corners[5] = new Vector3(max.x, min.y, max.z);
        corners[6] = new Vector3(min.x, max.y, max.z);
        corners[7] = new Vector3(max.x, max.y, max.z);

        Bounds tightBounds = new Bounds();

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 worldPoint = sourceObjectTransform.TransformPoint(corners[i]);
            Vector3 localPoint = targetAnchor.InverseTransformPoint(worldPoint);

            if (i == 0)
                tightBounds = new Bounds(localPoint, Vector3.zero);
            else
                tightBounds.Encapsulate(localPoint);
        }

        return tightBounds;
    }

    private void CreateVisualCube(GameObject parentAnchor, Bounds tightBounds, bool isPlane)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);

        cube.name = "Visual_Cube";
        cube.transform.SetParent(parentAnchor.transform);

        cube.transform.localPosition = tightBounds.center;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = tightBounds.size;

        Renderer renderer = cube.GetComponent<Renderer>();

        if (renderer != null && fallbackMaterial != null)
            renderer.sharedMaterial = fallbackMaterial;

        if (isPlane)
        {
            Vector3 scale = cube.transform.localScale;

            // Make plane anchors thin so walls/floor/ceiling are readable as surfaces.
            if (scale.z > scale.x && scale.z > scale.y)
                scale.z = 0.02f;
            else if (scale.y < 0.1f)
                scale.y = 0.02f;
            else
                scale.z = 0.02f;

            cube.transform.localScale = scale;
        }
    }

    private void SetInternalProperty(object obj, string propertyName, object value)
    {
        PropertyInfo property = typeof(MRUKAnchor).GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance
        );

        if (property != null)
            property.SetValue(obj, value);

#if UNITY_EDITOR
        EditorUtility.SetDirty(obj as UnityEngine.Object);
#endif
    }

    private void SaveToPrefab(GameObject obj)
    {
#if UNITY_EDITOR
        const string folderPath = "Assets/RoomPrefabs";

        if (!Directory.Exists(folderPath))
            AssetDatabase.CreateFolder("Assets", "RoomPrefabs");

        string localPath = AssetDatabase.GenerateUniqueAssetPath(
            folderPath + "/" + obj.name + ".prefab"
        );

        PrefabUtility.SaveAsPrefabAsset(obj, localPath);

        Debug.Log($"<b>[MRUK Baker]</b> SUCCESS! Saved to: {localPath}");
#endif
    }

    private bool IsPlaneLabel(MRUKAnchor.SceneLabels label)
    {
        return (label & (
            MRUKAnchor.SceneLabels.WALL_FACE |
            MRUKAnchor.SceneLabels.FLOOR |
            MRUKAnchor.SceneLabels.CEILING |
            MRUKAnchor.SceneLabels.WINDOW_FRAME |
            MRUKAnchor.SceneLabels.DOOR_FRAME
        )) != 0;
    }

    private void SetLabel(MRUKAnchor anchor, MRUKAnchor.SceneLabels label)
    {
        PropertyInfo property = typeof(MRUKAnchor).GetProperty(
            "Label",
            BindingFlags.Public | BindingFlags.Instance
        );

        if (property != null)
            property.SetValue(anchor, label);

#if UNITY_EDITOR
        EditorUtility.SetDirty(anchor);
#endif
    }
}