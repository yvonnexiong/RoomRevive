#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RoomRevive.SplatEditorBridge.EditorTools
{
    /// <summary>
    /// Keeps the splat-editor HTTP server (HTML_Editor/editor_server.py on :8766) running in the
    /// EDITOR, independent of Play mode. This is what lets the OnValidate / Initial-Product push reach
    /// the browser editor without entering Play — and it means the server no longer dies when you exit
    /// Play (GameManager used to start it on Play and kill it on stop).
    ///
    /// • On editor load it starts the server if the port isn't already open (toggle via the menu).
    /// • Menu: Tools/RoomRevive/Splat Editor Server/… to Start+Open, Stop, or toggle Auto-Start.
    /// • Uses 127.0.0.1 (not localhost) to dodge the IPv4/IPv6 resolution quirk in some browsers.
    /// </summary>
    [InitializeOnLoad]
    public static class SplatEditorServerLauncher
    {
        const int    Port         = 8766;
        const string Page         = "pink_to_marble_editor.html";
        const string AutoStartKey = "RoomRevive.SplatEditorServer.AutoStart";
        const string PidKey       = "RoomRevive.SplatEditorServer.Pid"; // SessionState: survives domain reloads

        static SplatEditorServerLauncher()
        {
            // Defer past the compile/serialization tick, then start if nothing is listening yet.
            EditorApplication.delayCall += () =>
            {
                if (AutoStart && !IsPortOpen())
                    StartServer(openBrowser: false);
            };
        }

        static bool AutoStart
        {
            get => EditorPrefs.GetBool(AutoStartKey, true);
            set => EditorPrefs.SetBool(AutoStartKey, value);
        }

        static string RepoRoot      => Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
        static string HtmlEditorDir => Path.Combine(RepoRoot, "HTML_Editor");
        static string ServerScript  => Path.Combine(HtmlEditorDir, "editor_server.py");
        static string Url           => $"http://127.0.0.1:{Port}/{Page}";

        static bool IsPortOpen()
        {
            try
            {
                using var c = new TcpClient();
                var ar = c.BeginConnect("127.0.0.1", Port, null, null);
                if (ar.AsyncWaitHandle.WaitOne(300)) { c.EndConnect(ar); return true; }
                return false;
            }
            catch { return false; }
        }

        [MenuItem("Tools/RoomRevive/Splat Editor Server/Start + Open in Chrome")]
        public static void StartMenu() => StartServer(openBrowser: true);

        static void StartServer(bool openBrowser)
        {
            if (IsPortOpen())
            {
                if (openBrowser) OpenBrowser();
                else Debug.Log($"[SplatEditorServer] Already running on :{Port}.");
                return;
            }
            if (!File.Exists(ServerScript))
            {
                Debug.LogWarning($"[SplatEditorServer] editor_server.py not found at {ServerScript}");
                return;
            }
            // 'py' (the Windows Python launcher) is preferred; fall back to 'python'.
            if (!TryStart("py") && !TryStart("python"))
            {
                Debug.LogWarning("[SplatEditorServer] Could not start — neither 'py' nor 'python' is on PATH.");
                return;
            }
            Debug.Log($"[SplatEditorServer] Started on :{Port}.");
            if (openBrowser) OpenBrowser();
        }

        static bool TryStart(string exe)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName         = exe,
                    Arguments        = $"\"{ServerScript}\" {Port}",
                    WorkingDirectory = HtmlEditorDir,
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                });
                if (p == null) return false;
                SessionState.SetInt(PidKey, p.Id);
                return true;
            }
            catch { return false; }
        }

        static void OpenBrowser()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "cmd.exe",
                    Arguments       = $"/c start chrome \"{Url}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
            }
            catch (Exception e) { Debug.LogWarning($"[SplatEditorServer] Could not open Chrome: {e.Message}"); }
        }

        [MenuItem("Tools/RoomRevive/Splat Editor Server/Stop")]
        public static void StopServer()
        {
            int pid = SessionState.GetInt(PidKey, -1);
            if (pid <= 0)
            {
                Debug.LogWarning("[SplatEditorServer] No tracked server this session — if one is running, stop it manually.");
                return;
            }
            try
            {
                // /T kills the child python the 'py' launcher spawned, too.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill", Arguments = $"/PID {pid} /T /F",
                    UseShellExecute = false, CreateNoWindow = true,
                });
                Debug.Log($"[SplatEditorServer] Stopped (pid {pid} + children).");
            }
            catch (Exception e) { Debug.LogWarning($"[SplatEditorServer] Could not stop pid {pid}: {e.Message}"); }
            SessionState.EraseInt(PidKey);
        }

        [MenuItem("Tools/RoomRevive/Splat Editor Server/Toggle Auto-Start (currently varies)")]
        public static void ToggleAutoStart()
        {
            AutoStart = !AutoStart;
            Debug.Log($"[SplatEditorServer] Auto-start is now {(AutoStart ? "ON" : "OFF")}.");
        }
    }
}
#endif
