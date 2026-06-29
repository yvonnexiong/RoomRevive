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

        string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string AnswersPath    => Path.GetFullPath(Path.Combine(ProjectRoot, answersRelativePath));
        string SelectionPath  => Path.GetFullPath(Path.Combine(ProjectRoot, selectionRelativePath));

        FileSystemWatcher _watcher;
        readonly object   _lock = new object();
        string  _pendingJson;
        bool    _hasPending;

        /// <summary>
        /// Call this after the user completes the questionnaire.
        /// Writes answers.json and starts watching for the selection result.
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
            StartWatching();
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
}
