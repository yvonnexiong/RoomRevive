using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
#endif

public class MRUKRoomToFBXExporter2 : MonoBehaviour
{
    [Tooltip("Drag the room prefab or GameObject you want to export here.")]
    public GameObject targetPrefab;

    [Tooltip("Root folder where a new sub-folder will be created for this export.")]
    public string exportFolder = "Assets/Exports";

    [Tooltip("Override the sub-folder name. Leave blank to use the prefab/GameObject name.")]
    public string exportFileName = "";

#if UNITY_EDITOR
    public void ExportToFBX()
    {
        GameObject source = targetPrefab != null ? targetPrefab : gameObject;

        // Resolve export folder to absolute path
        string resolvedFolder = exportFolder;
        if (!Path.IsPathRooted(resolvedFolder))
            resolvedFolder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", resolvedFolder));

        // Create a sub-folder named after the GameObject / override
        string subFolderName = string.IsNullOrEmpty(exportFileName)
            ? SanitizeName(source.name)
            : SanitizeName(exportFileName);

        string outputFolder = Path.Combine(resolvedFolder, subFolderName);

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        // Convert to project-relative for ModelExporter (must start with "Assets/")
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string relativeOutputFolder = outputFolder;
        if (outputFolder.StartsWith(projectRoot))
            relativeOutputFolder = outputFolder
                .Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, '/');

        // If the source is a prefab asset, instantiate it temporarily
        bool wasInstantiated = false;
        if (!source.scene.IsValid())
        {
            source = Instantiate(source);
            wasInstantiated = true;
        }

        int exported = 0;
        int failed = 0;

        try
        {
            Transform[] children = source.GetComponentsInChildren<Transform>(true);

            // Track used names to avoid file collisions
            System.Collections.Generic.Dictionary<string, int> nameCount =
                new System.Collections.Generic.Dictionary<string, int>();

            foreach (Transform t in children)
            {
                if (t == source.transform)
                    continue;

                GameObject childExport = BuildSingleObject(t, source.transform);
                if (childExport == null)
                    continue;

                // Unique filename
                string baseName = SanitizeName(t.name);
                if (nameCount.ContainsKey(baseName))
                {
                    nameCount[baseName]++;
                    baseName = baseName + "_" + nameCount[baseName];
                }
                else
                {
                    nameCount[baseName] = 0;
                }

                string savePath = Path.Combine(relativeOutputFolder, baseName + ".fbx")
                                     .Replace('\\', '/');

                try
                {
                    // Force BINARY FBX — World Labs and most runtime importers
                    // reject ASCII FBX files (Unity's default).
                    ExportModelOptions exportOptions = new ExportModelOptions
                    {
                        ExportFormat = ExportFormat.Binary
                    };
                    ModelExporter.ExportObject(savePath, childExport, exportOptions);
                    exported++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MRUK Exporter] Failed to export '{t.name}': {ex.Message}");
                    failed++;
                }
                finally
                {
                    DestroyImmediate(childExport);
                }
            }
        }
        finally
        {
            if (wasInstantiated)
                DestroyImmediate(source);

            AssetDatabase.Refresh();
        }

        string summary = $"Exported {exported} FBX file(s) to:\n{outputFolder}";
        if (failed > 0)
            summary += $"\n\n{failed} object(s) failed — check the Console for details.";

        Debug.Log($"[MRUK Exporter] {summary}");
        EditorUtility.DisplayDialog("Export Complete", summary, "OK");
    }

    // Exports only the room SHELL (walls + floor + ceiling/roof) as a
    // single combined FBX. Each matching child is world-baked the same
    // way as the per-object exporter, then all pieces are merged into
    // one Mesh with per-piece submeshes (so original materials survive).
    public void ExportStructureToFBX()
    {
        GameObject source = targetPrefab != null ? targetPrefab : gameObject;

        // Resolve export folder to absolute path
        string resolvedFolder = exportFolder;
        if (!Path.IsPathRooted(resolvedFolder))
            resolvedFolder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", resolvedFolder));

        string subFolderName = string.IsNullOrEmpty(exportFileName)
            ? SanitizeName(source.name)
            : SanitizeName(exportFileName);

        string outputFolder = Path.Combine(resolvedFolder, subFolderName);
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string relativeOutputFolder = outputFolder;
        if (outputFolder.StartsWith(projectRoot))
            relativeOutputFolder = outputFolder
                .Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, '/');

        bool wasInstantiated = false;
        if (!source.scene.IsValid())
        {
            source = Instantiate(source);
            wasInstantiated = true;
        }

        GameObject combinedRoot = null;
        System.Collections.Generic.List<GameObject> tempObjects =
            new System.Collections.Generic.List<GameObject>();

        try
        {
            Transform[] children = source.GetComponentsInChildren<Transform>(true);

            System.Collections.Generic.List<CombineInstance> combines =
                new System.Collections.Generic.List<CombineInstance>();
            System.Collections.Generic.List<Material> materials =
                new System.Collections.Generic.List<Material>();

            foreach (Transform t in children)
            {
                if (t == source.transform) continue;
                if (!IsStructureElement(t.name)) continue;

                GameObject baked = BuildSingleObject(t, source.transform);
                if (baked == null) continue;
                tempObjects.Add(baked);

                MeshFilter mf = baked.GetComponent<MeshFilter>();
                MeshRenderer mr = baked.GetComponent<MeshRenderer>();
                if (mf == null || mf.sharedMesh == null) continue;

                Mesh m = mf.sharedMesh;
                for (int si = 0; si < m.subMeshCount; si++)
                {
                    CombineInstance ci = new CombineInstance
                    {
                        mesh = m,
                        subMeshIndex = si,
                        transform = Matrix4x4.identity  // vertices already world-baked
                    };
                    combines.Add(ci);

                    Material mat =
                        (mr != null && mr.sharedMaterials != null &&
                         si < mr.sharedMaterials.Length && mr.sharedMaterials[si] != null)
                        ? mr.sharedMaterials[si]
                        : GetDefaultMaterial();
                    materials.Add(mat);
                }
            }

            if (combines.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Structure Found",
                    "No meshes whose names contain WALL, FLOOR, CEILING, or ROOF were found under the source.",
                    "OK");
                return;
            }

            Mesh combinedMesh = new Mesh { name = subFolderName + "_Structure" };
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combines.ToArray(),
                                       mergeSubMeshes: false,
                                       useMatrices: true);

            combinedRoot = new GameObject(subFolderName + "_Structure");
            combinedRoot.transform.position = Vector3.zero;
            combinedRoot.transform.rotation = Quaternion.identity;
            combinedRoot.transform.localScale = Vector3.one;
            combinedRoot.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            MeshRenderer combinedMr = combinedRoot.AddComponent<MeshRenderer>();
            combinedMr.sharedMaterials = materials.ToArray();

            string savePath = Path.Combine(relativeOutputFolder, "Structure.fbx")
                                  .Replace('\\', '/');

            ExportModelOptions exportOptions = new ExportModelOptions
            {
                ExportFormat = ExportFormat.Binary
            };

            ModelExporter.ExportObject(savePath, combinedRoot, exportOptions);

            string absolutePath = Path.Combine(outputFolder, "Structure.fbx");
            string summary =
                $"Exported combined structure ({combines.Count} submesh(es)) to:\n{absolutePath}";
            Debug.Log($"[MRUK Exporter] {summary}");
            EditorUtility.DisplayDialog("Export Complete", summary, "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MRUK Exporter] Structure export failed: {ex.Message}");
            EditorUtility.DisplayDialog("Export Failed",
                "Structure export failed — see Console for details.", "OK");
        }
        finally
        {
            foreach (GameObject go in tempObjects)
                if (go != null) DestroyImmediate(go);

            if (combinedRoot != null) DestroyImmediate(combinedRoot);
            if (wasInstantiated) DestroyImmediate(source);

            AssetDatabase.Refresh();
        }
    }

    // True for any Transform whose name identifies a structural surface
    // (wall / floor / ceiling / roof). Case-insensitive, substring match.
    private static bool IsStructureElement(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToUpperInvariant();
        return n.Contains("WALL")
            || n.Contains("FLOOR")
            || n.Contains("CEILING")
            || n.Contains("ROOF");
    }

    // Builds a single export GameObject for one Transform.
    // The GameObject sits at world origin (0,0,0) with identity transform,
    // and the MESH VERTICES are baked into coordinates RELATIVE TO THE
    // SOURCE ROOT's POSITION AND ROTATION only — the parent's SCALE is
    // preserved. That way the pieces keep their real-world size while the
    // room's position/rotation offset is cancelled, so uploaded FBXes line
    // up at origin in World Labs.
    private static GameObject BuildSingleObject(Transform t, Transform sourceRoot)
    {
        // Build a matrix containing only the parent's position + rotation
        // (scale = 1). Inverting this cancels position/rotation but leaves
        // any scale baked into t.localToWorldMatrix intact.
        Matrix4x4 parentPosRot = Matrix4x4.TRS(
            sourceRoot.position,
            sourceRoot.rotation,
            Vector3.one
        );
        Matrix4x4 world = parentPosRot.inverse * t.localToWorldMatrix;

        // 1. MeshFilter
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            GameObject go = CreateOriginGameObject(t.name);
            go.AddComponent<MeshFilter>().sharedMesh =
                BakeVerticesToWorld(mf.sharedMesh, world, t.name + "_Baked");
            MeshRenderer srcMr = t.GetComponent<MeshRenderer>();
            MeshRenderer newMr = go.AddComponent<MeshRenderer>();
            newMr.sharedMaterials =
                (srcMr != null && srcMr.sharedMaterials?.Length > 0)
                ? srcMr.sharedMaterials : new[] { GetDefaultMaterial() };
            return go;
        }

        // 2. SkinnedMeshRenderer
        SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            // BakeMesh returns geometry in the renderer's local space;
            // we then bake that into world space so the FBX is self-contained.
            Mesh localBaked = new Mesh { name = t.name + "_SkinBaked" };
            smr.BakeMesh(localBaked);
            Mesh worldBaked = BakeVerticesToWorld(localBaked, world, t.name + "_Baked");
            Object.DestroyImmediate(localBaked);

            GameObject go = CreateOriginGameObject(t.name);
            go.AddComponent<MeshFilter>().sharedMesh = worldBaked;
            MeshRenderer newMr = go.AddComponent<MeshRenderer>();
            newMr.sharedMaterials =
                (smr.sharedMaterials?.Length > 0)
                ? smr.sharedMaterials : new[] { GetDefaultMaterial() };
            return go;
        }

        // 3. MeshCollider
        MeshCollider mc = t.GetComponent<MeshCollider>();
        if (mc != null && mc.sharedMesh != null)
        {
            GameObject go = CreateOriginGameObject(t.name);
            go.AddComponent<MeshFilter>().sharedMesh =
                BakeVerticesToWorld(mc.sharedMesh, world, t.name + "_Baked");
            go.AddComponent<MeshRenderer>().sharedMaterial = GetDefaultMaterial();
            return go;
        }

        // 4. BoxCollider blockout — build a cube mesh and bake it into world space.
        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box != null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;

            // Unity's primitive cube is 1×1×1 centered at origin, so apply
            // the BoxCollider's center + size locally, then the transform's
            // world matrix.
            Matrix4x4 boxMatrix =
                world * Matrix4x4.TRS(box.center, Quaternion.identity, box.size);

            Mesh baked = BakeVerticesToWorld(cubeMesh, boxMatrix, t.name + "_Baked");
            Object.DestroyImmediate(temp);

            GameObject go = CreateOriginGameObject(t.name);
            go.AddComponent<MeshFilter>().sharedMesh = baked;
            go.AddComponent<MeshRenderer>().sharedMaterial = GetDefaultMaterial();
            return go;
        }

        return null; // nothing exportable
    }

    // GameObject with identity transform at world origin.
    private static GameObject CreateOriginGameObject(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    // Produces a NEW mesh whose vertices/normals/tangents are transformed
    // by the given world matrix. The source mesh is untouched.
    private static Mesh BakeVerticesToWorld(Mesh src, Matrix4x4 worldMatrix, string name)
    {
        Mesh baked = Object.Instantiate(src);
        baked.name = name;

        Vector3[] verts = baked.vertices;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = worldMatrix.MultiplyPoint3x4(verts[i]);
        baked.vertices = verts;

        // Normals use the inverse transpose of the world matrix so non-uniform
        // scale is handled correctly.
        Vector3[] normals = baked.normals;
        if (normals != null && normals.Length == verts.Length)
        {
            Matrix4x4 normalMatrix = worldMatrix.inverse.transpose;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            baked.normals = normals;
        }

        Vector4[] tangents = baked.tangents;
        if (tangents != null && tangents.Length == verts.Length)
        {
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tan = worldMatrix.MultiplyVector(
                    new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                // Preserve handedness in .w
                tangents[i] = new Vector4(tan.x, tan.y, tan.z, tangents[i].w);
            }
            baked.tangents = tangents;
        }

        baked.RecalculateBounds();
        return baked;
    }

    private static Material GetDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("[MRUK Exporter] No suitable shader found.");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        return new Material(shader) { name = "Export_Default_Material" };
    }

    // Strip characters that are invalid in file/folder names
    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");
        return name;
    }
#endif
}
