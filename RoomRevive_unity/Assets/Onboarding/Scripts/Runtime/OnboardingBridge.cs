using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace RoomRevive.Onboarding
{
    /// <summary>
    /// File-based handoff between the Unity XR questionnaire and the HTML editor.
    ///
    /// Write path:  Unity writes the 4 answers → HTML editor picks them up and runs selection.
    /// Read path:   HTML editor writes the chosen selection JSON → Unity fires OnSelectionReceived.
    ///
    /// Both paths use project-relative paths (one folder above Assets/) so they work on any machine.
    /// </summary>
    public class OnboardingBridge : MonoBehaviour
    {
        [Tooltip("Relative to project root. HTML editor reads this file to get the four answers.")]
        public string answersRelativePath = "Onboarding/onboarding_answers.json";

        [Tooltip("Relative to project root. HTML editor writes this file when the user confirms a selection.")]
        public string selectionRelativePath = "Onboarding/onboarding_selection.json";

        [Header("Events")]
        [Tooltip("Fired on the main thread when the HTML editor writes a new selection. Payload is the raw JSON string.")]
        public UnityEvent<string> onSelectionReceived;

        // Application.dataPath = …/RoomRevive_unity/Assets — go up twice to reach repo root
        string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        string AnswersPath    => Path.GetFullPath(Path.Combine(ProjectRoot, answersRelativePath));
        string SelectionPath  => Path.GetFullPath(Path.Combine(ProjectRoot, selectionRelativePath));

        FileSystemWatcher _watcher;
        readonly object   _lock = new object();
        string  _pendingJson;
        bool    _hasPending;

        /// <summary>
        /// Call this after the user completes the questionnaire.
        /// Writes answers.json, spawns the selection core, then watches for the result.
        /// </summary>
        public void SubmitAnswers(string style, string tone, string household, string budget)
        {
            var answers = new OnboardingAnswers
            {
                style     = style,
                tone      = tone,
                household = household,
                budget    = budget
            };

            WriteJson(AnswersPath, JsonUtility.ToJson(answers, prettyPrint: true));
            Debug.Log($"[OnboardingBridge] Answers written → {AnswersPath}");
            StartWatching();
            RunSelectionCore();
        }

        void RunSelectionCore()
        {
            try
            {
                string cliPath = Path.GetFullPath(
                    Path.Combine(ProjectRoot, "Onboarding/track2_selection_core/cli.js"));

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = "node",
                    Arguments              = $"\"{cliPath}\"",
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                var proc = new System.Diagnostics.Process { StartInfo = psi };
                proc.OutputDataReceived += (_, e) => { if (e.Data != null) Debug.Log($"[SelectionCore] {e.Data}"); };
                proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) Debug.LogWarning($"[SelectionCore] {e.Data}"); };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                Debug.Log($"[OnboardingBridge] Selection core launched (node pid {proc.Id})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[OnboardingBridge] Could not launch node — is Node.js installed? {e.Message}\n" +
                                 $"Fallback: run 'node Onboarding/track2_selection_core/cli.js' manually.");
            }
        }

        void StartWatching()
        {
            _watcher?.Dispose();

            string dir  = Path.GetDirectoryName(SelectionPath);
            string file = Path.GetFileName(SelectionPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter       = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
        }

        void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Small delay so the writer has finished flushing
                System.Threading.Thread.Sleep(50);
                string json = File.ReadAllText(SelectionPath);
                lock (_lock) { _pendingJson = json; _hasPending = true; }
            }
            catch { /* file still being written — next event will retry */ }
        }

        void Update()
        {
            string json = null;
            lock (_lock)
            {
                if (_hasPending) { json = _pendingJson; _hasPending = false; }
            }
            if (json != null) onSelectionReceived?.Invoke(json);
        }

        void OnDestroy() => _watcher?.Dispose();

        static void WriteJson(string path, string json)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
        }
    }

    [Serializable]
    public class OnboardingAnswers
    {
        public string style;
        public string tone;
        public string household;
        public string budget;
    }

    // One row in the ReadyUI "In this room" card.
    // Track 2 writes this into onboarding_selection.json so Unity never needs catalog.json.
    [Serializable]
    public class SelectionRow
    {
        public string category; // display label, e.g. "Kitchen", "Fridge"
        public string name;     // resolved product name from catalog, e.g. "TOUCH 337"
        public string id;       // catalog id — preserved for Track 3 / visualizer
    }

    // Root object Unity parses from onboarding_selection.json.
    [Serializable]
    public class SelectionResult
    {
        public string         intent;  // e.g. "Fast & Focused"
        public string         tagline; // e.g. "bright, efficient — in, fed, and out"
        public SelectionRow[] rows;    // 7 rows in display order
    }
}
