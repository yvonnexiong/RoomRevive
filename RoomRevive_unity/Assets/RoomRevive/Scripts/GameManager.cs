using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RoomRevive
{
    public enum GamePhase
    {
        Welcome,    // welcomeObjects visible
        Alignment,  // alignmentObjects visible
        Browsing,   // browsingObjects (+ fridgesObjects) visible
    }

    /// <summary>
    /// Orchestrates the game flow through Welcome → Alignment → Browsing.
    /// Each phase owns a list of GameObjects; entering a phase enables that phase's list and
    /// disables the others. Set <see cref="startPhase"/> in the inspector to jump to a phase on play.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Start Phase")]
        [Tooltip("Which phase the game starts in. Use Browsing to skip Welcome and Alignment during iteration.")]
        public GamePhase startPhase = GamePhase.Welcome;

        [Tooltip("ON  = the editor live-previews the Start Phase via OnValidate (and on scene load) — " +
                 "convenient, but it OVERRIDES the enabled state you saved, so reloading re-applies the phase.\n" +
                 "OFF = the scene loads exactly as saved; preview manually with the ⋮ → 'Apply Start Phase (Preview)'.")]
        public bool autoApplyInEditor = false;

        [Header("Welcome")]
        [Tooltip("All GameObjects enabled during the Welcome phase.")]
        public List<GameObject> welcomeObjects = new List<GameObject>();

        [Header("Alignment")]
        [Tooltip("All GameObjects enabled during the Alignment phase.")]
        public List<GameObject> alignmentObjects = new List<GameObject>();

        [Header("Browsing")]
        [Tooltip("All GameObjects enabled during the Browsing phase.")]
        public List<GameObject> browsingObjects = new List<GameObject>();

        [Header("Fridges")]
        [Tooltip("Enabled together with the Browsing phase.")]
        public List<GameObject> fridgesObjects = new List<GameObject>();

        [Header("Staggered Enable")]
        [Tooltip("Enabled FIRST when its phase starts, before the rest of that phase's objects " +
                 "(e.g. SplatPivot — the splat root the others depend on / are parented under).")]
        public GameObject enableFirst;

        [Tooltip("Seconds to wait after enabling 'Enable First' before enabling the rest of the phase's " +
                 "objects. Lets the splats load/initialize first. Play mode only; the editor applies instantly.")]
        public float enableDelay = 0.5f;

        Coroutine _staggerRoutine;

        [Header("Dev Server (Editor only)")]
        [Tooltip("When on, entering Play mode in the EDITOR starts a local web server for the folder below " +
                 "and opens it in Chrome — so you can watch the splat editor while the game runs. " +
                 "Does nothing in a built player.")]
        public bool startServerOnPlay = false;

        [Tooltip("Folder to serve, relative to the repo root (the folder that contains RoomRevive_unity). " +
                 "Absolute paths also work. e.g. 'SplatSelector'.")]
        public string serverWorkingDirectory = "SplatSelector";

        [Tooltip("Command run (from the working directory) to start the server. First token is the executable.")]
        public string serverCommand = "python -m http.server 8000";

        [Tooltip("URL opened in Chrome after the server starts.")]
        public string browserUrl = "http://localhost:8000/pink_to_marble_editor.html";

#if UNITY_EDITOR
        System.Diagnostics.Process _devServer;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            WelcomeUI.OnScanRequested         += OnWelcomeConfirmed;
            AlignmentUIController.OnConfirmed += OnAlignmentComplete;
        }

        void OnDisable()
        {
            WelcomeUI.OnScanRequested         -= OnWelcomeConfirmed;
            AlignmentUIController.OnConfirmed -= OnAlignmentComplete;
#if UNITY_EDITOR
            StopDevServer();
#endif
        }

        void Start()
        {
            EnterPhase(startPhase);
#if UNITY_EDITOR
            if (startServerOnPlay) StartDevServer();
#endif
        }

#if UNITY_EDITOR
        void OnApplicationQuit() => StopDevServer();

        void StartDevServer()
        {
            try
            {
                // dataPath = .../RoomRevive_unity/Assets → repo root is two levels up.
                string repoRoot = System.IO.Directory.GetParent(
                    System.IO.Directory.GetParent(Application.dataPath).FullName).FullName;
                string wd = System.IO.Path.IsPathRooted(serverWorkingDirectory)
                    ? serverWorkingDirectory
                    : System.IO.Path.Combine(repoRoot, serverWorkingDirectory);

                if (!System.IO.Directory.Exists(wd))
                {
                    Debug.LogWarning($"[GameManager] Dev server folder not found: {wd}", this);
                    return;
                }

                // Split "exe arg arg" → run the exe directly so killing _devServer also kills it.
                string cmd = serverCommand.Trim();
                int sp = cmd.IndexOf(' ');
                string exe  = sp < 0 ? cmd : cmd.Substring(0, sp);
                string args = sp < 0 ? "" : cmd.Substring(sp + 1);

                _devServer = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = wd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                Debug.Log($"[GameManager] Dev server started: '{cmd}' (cwd {wd})", this);

                if (!string.IsNullOrEmpty(browserUrl))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c start chrome \"{browserUrl}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameManager] Failed to start dev server: {e.Message}", this);
            }
        }

        void StopDevServer()
        {
            try { if (_devServer != null && !_devServer.HasExited) _devServer.Kill(); }
            catch { /* already gone */ }
            _devServer = null;
        }
#endif

#if UNITY_EDITOR
        // Auto-apply is OPT-IN via autoApplyInEditor. OnValidate also fires on scene load, so when
        // it's on, reloading re-applies the phase (overriding your saved enabled state); when off, the
        // scene stays as saved and you preview on demand with the context menu below.
        void OnValidate()
        {
            if (!autoApplyInEditor) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EnterPhase(startPhase);
            };
        }

        [ContextMenu("Apply Start Phase (Preview)")]
        void ApplyStartPhasePreview() => EnterPhase(startPhase);
#endif

        // ── Phase transitions ─────────────────────────────────────────────────

        // Reused so an object that appears in multiple phase lists (e.g. SplatPivot in both
        // Alignment and Browsing) is enabled whenever ANY active-phase list contains it — instead
        // of a later list clobbering it back off.
        readonly HashSet<GameObject> _shouldBeOn = new HashSet<GameObject>();

        void EnterPhase(GamePhase phase)
        {
            if (_staggerRoutine != null) { StopCoroutine(_staggerRoutine); _staggerRoutine = null; }

            // 1) Collect the objects that should be ON for this phase.
            _shouldBeOn.Clear();
            switch (phase)
            {
                case GamePhase.Welcome:   Mark(welcomeObjects);                       break;
                case GamePhase.Alignment: Mark(alignmentObjects);                     break;
                case GamePhase.Browsing:  Mark(browsingObjects); Mark(fridgesObjects); break;
            }

            // 2) Splat root (enableFirst) is STICKY: on for any non-Welcome phase, off only in Welcome.
            //    Once enabled it stays enabled across Alignment↔Browsing, so it never reloads.
            bool welcome     = phase == GamePhase.Welcome;
            bool splatWasOn  = enableFirst != null && enableFirst.activeSelf;
            bool splatOnNow  = enableFirst != null && !welcome;
            if (enableFirst != null && enableFirst.activeSelf != splatOnNow)
                enableFirst.SetActive(splatOnNow);

            // 3) Turn the OTHER objects off (never touch enableFirst here — it's managed above).
            DisableAllExceptSplat(welcomeObjects);
            DisableAllExceptSplat(alignmentObjects);
            DisableAllExceptSplat(browsingObjects);
            DisableAllExceptSplat(fridgesObjects);

            // 4) Enable this phase's objects. Stagger ONLY on the first splat enable (off→on), so the
            //    splats get time to load; if the splat is already up (Alignment↔Browsing), enable now.
            bool firstSplatEnable = splatOnNow && !splatWasOn;
            if (Application.isPlaying && firstSplatEnable && enableDelay > 0f)
                _staggerRoutine = StartCoroutine(EnableRestAfterDelay());
            else
                EnableRest();
        }

        void OnWelcomeConfirmed()  => EnterPhase(GamePhase.Alignment);
        void OnAlignmentComplete() => EnterPhase(GamePhase.Browsing);

        /// <summary>Public entry point so other scripts (e.g. StartupController buttons) can drive the flow.</summary>
        public void GoToPhase(GamePhase phase) => EnterPhase(phase);

        // Parameterless wrappers so they appear in a Button's OnClick dropdown (UnityEvent persistent
        // calls don't support enum parameters, so GoToPhase(GamePhase) is hidden there).
        public void GoToWelcome()   => EnterPhase(GamePhase.Welcome);
        public void GoToAlignment() => EnterPhase(GamePhase.Alignment);
        public void GoToBrowsing()  => EnterPhase(GamePhase.Browsing);

        IEnumerator EnableRestAfterDelay()
        {
            yield return new WaitForSeconds(enableDelay);
            EnableRest();
            _staggerRoutine = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        void Mark(List<GameObject> objects)
        {
            if (objects == null) return;
            foreach (var go in objects)
                if (go != null) _shouldBeOn.Add(go);
        }

        void DisableAllExceptSplat(List<GameObject> objects)
        {
            if (objects == null) return;
            foreach (var go in objects)
                // Never disable the splat root or THIS manager's own GameObject — if the manager were
                // listed in a phase (mis-config), disabling itself would kill the whole flow.
                if (go != null && go != enableFirst && go != gameObject) go.SetActive(false);
        }

        // Turn on every object that belongs to the active phase (enableFirst is managed separately).
        void EnableRest()
        {
            EnableOn(welcomeObjects);
            EnableOn(alignmentObjects);
            EnableOn(browsingObjects);
            EnableOn(fridgesObjects);
        }

        void EnableOn(List<GameObject> objects)
        {
            if (objects == null) return;
            foreach (var go in objects)
                if (go != null && go != enableFirst && _shouldBeOn.Contains(go)) go.SetActive(true);
        }
    }
}
