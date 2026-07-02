// LiveSplatLoader.cs
// Live bridge between the browser "PINK→MARBLE" editor and Unity.
//
// The editor's "Auto-save copy to file…" rewrites a .spz every time you swap a material.
// This component watches that file and hot-reloads it into a GaussianSplatRenderer, so the
// material changes appear in Unity instantly — in Play mode AND in the Editor.
//
// SETUP
//   1. Put this on (or assign) a GameObject that has a GaussianSplatRenderer.
//   2. In the browser editor (Combined tab) click "Auto-save copy to file…" and save to the
//      path in 'spzFilePath' below — default: <project>/LiveSplat/kitchen-copy.spz.
//      Keep BOTH the editor and this field pointing at the SAME file.
//   3. That's it. Swap materials in the editor → Unity updates within ~1 s.
//
// Notes
//   * The file lives OUTSIDE Assets/ on purpose, so Unity never tries to import it.
//   * Each reload re-encodes ~1.9M splats (Morton + pack + GPU upload), so expect a brief
//     hitch on each save. That's the same work Unity does when importing an .spz.

using System;
using System.IO;
using System.Threading;
using UnityEngine;
using GaussianSplatting.Runtime;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LiveSplatLoader : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Path to the .spz the browser editor auto-saves, relative to the Unity project root (one folder above Assets/). E.g. 'LiveSplat/kitchen-copy.spz'. Pick the same file in the editor's 'Choose file…' dialog.")]
    public string spzRelativePath = "LiveSplat/kitchen-copy.spz";

    // Resolved at runtime so the path works on any machine regardless of where the repo is cloned.
    string spzFilePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", spzRelativePath));

    [Tooltip("Renderer to load into. Leave empty to use a GaussianSplatRenderer on this GameObject.")]
    public GaussianSplatRenderer targetRenderer;

    [Header("Transition")]
    [Tooltip("When on, each new splat is handed to SplatManager.SwapToSpz() to drive the old→new reveal " +
             "across SplatRenderer 1 (current/old) and SplatRenderer 2 (incoming new), instead of loading " +
             "into Target Renderer above. Disable the standalone display renderer when using this.")]
    public bool driveTransition = false;

    [Header("Watching")]
    [Tooltip("Reload automatically whenever the file changes on disk.")]
    public bool autoReload = true;

    [Tooltip("Wait this long (seconds) after the last change before loading — lets the browser finish writing the file.")]
    public float settleSeconds = 0.35f;

    [Tooltip("Fallback poll interval (seconds), in case OS file-change events are missed.")]
    public float pollSeconds = 0.5f;

    [Header("Status (read-only)")]
    public int loadedSplatCount;
    public string lastStatus = "not loaded";

    GaussianSplatRenderer m_Renderer;
    FileSystemWatcher m_Watcher;
    volatile bool m_Touched;     // set from watcher thread / poll when the file changes
    bool m_Pending;              // main thread: a load is queued (debouncing)
    double m_LoadAt;             // main thread: time to actually load
    double m_NextPoll;
    long m_LastLen = -1;
    DateTime m_LastWriteUtc = DateTime.MinValue;

    static double Now =>
#if UNITY_EDITOR
        UnityEditor.EditorApplication.timeSinceStartup;
#else
        Time.realtimeSinceStartup;
#endif

    void OnEnable()
    {
        ResolveRenderer();
        SetupWatcher();
        m_Touched = true; // queue an initial load
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += EditorTick; // drive the watcher in Edit mode too
#endif
    }

    void OnDisable()
    {
        DisposeWatcher();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

    void DisposeWatcher()
    {
        try { if (m_Watcher != null) { m_Watcher.EnableRaisingEvents = false; m_Watcher.Dispose(); } } catch { }
        m_Watcher = null;
    }

#if UNITY_EDITOR
    // Runs in the Editor whenever the component is loaded or a field changes in the inspector.
    // Lets you edit 'spzFilePath' (or just re-add the component) and have it re-point the watcher
    // and hot-reload — all without entering Play mode. OnValidate can fire during serialization,
    // so we never touch the GPU / file watcher directly here; we defer to the next editor tick.
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;                 // disabled → OnEnable handles it when enabled
        UnityEditor.EditorApplication.delayCall -= OnValidateDeferred;
        UnityEditor.EditorApplication.delayCall += OnValidateDeferred;
    }

    void OnValidateDeferred()
    {
        UnityEditor.EditorApplication.delayCall -= OnValidateDeferred;
        if (this == null || !isActiveAndEnabled) return; // component/object may have been destroyed meanwhile

        ResolveRenderer();
        DisposeWatcher();
        SetupWatcher();                                  // re-point at the (possibly new) folder/file
        m_LastLen = -1;                                  // forget cached size/time so the file reads as "new"
        m_LastWriteUtc = DateTime.MinValue;
        m_Touched = true;                                // queue a reload on the next editor tick
    }
#endif

    void ResolveRenderer()
    {
        m_Renderer = targetRenderer != null ? targetRenderer : GetComponent<GaussianSplatRenderer>();
        if (m_Renderer == null)
            Debug.LogWarning("[LiveSplatLoader] No GaussianSplatRenderer found — assign one in the inspector.");
    }

    void SetupWatcher()
    {
        try
        {
            var dir = Path.GetDirectoryName(spzFilePath);
            var file = Path.GetFileName(spzFilePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                Debug.LogWarning($"[LiveSplatLoader] folder not found: '{dir}' — using polling only.");
                return;
            }
            m_Watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            FileSystemEventHandler onChange = (_, __) => m_Touched = true;
            m_Watcher.Changed += onChange;
            m_Watcher.Created += onChange;
            m_Watcher.Renamed += (_, __) => m_Touched = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LiveSplatLoader] file watcher failed ({e.Message}); using polling only.");
        }
    }

#if UNITY_EDITOR
    void EditorTick() { if (!Application.isPlaying) Tick(); }
#endif
    void Update() { if (Application.isPlaying) Tick(); }

    void Tick()
    {
        if (!autoReload) return;
        double now = Now;

        // poll fallback (also catches changes if the OS event was missed)
        if (now >= m_NextPoll)
        {
            m_NextPoll = now + Mathf.Max(0.1f, pollSeconds);
            if (FileChangedOnDisk()) m_Touched = true;
        }

        // debounce: every change pushes the load time out, so we only load once writes settle
        if (m_Touched)
        {
            m_Touched = false;
            m_Pending = true;
            m_LoadAt = now + Mathf.Max(0f, settleSeconds);
        }

        if (m_Pending && now >= m_LoadAt)
        {
            m_Pending = false;
            ReloadNow();
        }
    }

    bool FileChangedOnDisk()
    {
        try
        {
            var fi = new FileInfo(spzFilePath);
            if (!fi.Exists) return false;
            return fi.Length != m_LastLen || fi.LastWriteTimeUtc != m_LastWriteUtc;
        }
        catch { return false; }
    }

    [ContextMenu("Reload now")]
    public void ReloadNow()
    {
        if (m_Renderer == null) ResolveRenderer();
        if (m_Renderer == null && !driveTransition) return;

        try
        {
            var fi = new FileInfo(spzFilePath);
            if (!fi.Exists) { lastStatus = "file not found: " + spzFilePath; return; }

            byte[] bytes = ReadShared(spzFilePath, 8); // the browser may still be writing — retry with a shared read
            if (bytes == null || bytes.Length < 16) { m_Touched = true; return; } // not ready; try again next tick

            RuntimeSplatData rsd = RuntimeSplatProcessing.ProcessSPZBytes(bytes); // gzip NGSP → Morton-ordered, packed GPU buffers

            // Either drive the old→new reveal via SplatManager (renderers 1 & 2) or load into our own renderer.
            SplatManager sm = driveTransition ? SplatManager.GetOrFindInstance() : null;
            if (sm != null) sm.SwapToSpz(rsd);
            else            m_Renderer.LoadFromRuntimeData(rsd);

            m_LastLen = fi.Length;
            m_LastWriteUtc = fi.LastWriteTimeUtc;
            loadedSplatCount = rsd.splatCount;
            lastStatus = $"loaded {rsd.splatCount:N0} splats @ {DateTime.Now:HH:mm:ss}";

#if UNITY_EDITOR
            // Edit mode swaps the GPU buffers but doesn't auto-repaint, so the Scene/Game view keeps
            // showing the stale frame until you nudge it. Force a repaint so the change shows at once.
            if (!Application.isPlaying)
            {
                UnityEditor.SceneView.RepaintAll();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }
        catch (Exception e)
        {
            lastStatus = "load failed: " + e.Message;
            Debug.LogWarning($"[LiveSplatLoader] {lastStatus}");
            m_Touched = true; // probably a mid-write/partial file — retry shortly
        }
    }

    // Read the whole file allowing the writer to keep its own handle open (FileShare.ReadWrite),
    // retrying briefly in case the browser is mid-write.
    static byte[] ReadShared(string path, int tries)
    {
        for (int i = 0; i < tries; i++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long len = fs.Length;
                if (len > 0)
                {
                    var buf = new byte[len];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = fs.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read == buf.Length) return buf;
                }
            }
            catch { /* locked / mid-write */ }
            Thread.Sleep(100);
        }
        return null;
    }
}
