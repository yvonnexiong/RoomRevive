using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using TMPro;

namespace RoomRevive.Onboarding
{
    // Self-contained ReadyUI controller.
    // On enable: immediately reads onboarding_selection.json if it exists,
    // then watches for file changes so it stays live without FlowController.
    public class OnboardingReadyController : MonoBehaviour
    {
        [SerializeField] CanvasGroup[]     _rowGroups;   // one per product row
        [SerializeField] TextMeshProUGUI[] _productTmps; // right-side name TMP per row
        [SerializeField] TextMeshProUGUI   _noteTmp;

        [Tooltip("Relative to repo root. Must match OnboardingBridge.selectionRelativePath.")]
        [SerializeField] string _selectionRelativePath = "Onboarding/onboarding_selection.json";

        [Tooltip("Seconds to wait for selection data before falling back to placeholder names.")]
        [SerializeField] float _dataTimeoutSeconds = 4f;

        // Application.dataPath = …/RoomRevive_unity/Assets — go up twice to reach repo root
        string SelectionPath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../..", _selectionRelativePath));

        FileSystemWatcher _watcher;
        readonly object   _lock = new();
        string            _pendingJson;
        bool              _hasPending;
        bool              _dataReady;

        void OnEnable()
        {
            _dataReady  = false;
            _hasPending = false;
            StopAllCoroutines();
            StartCoroutine(RunSequence());
            StartWatching();

            // If file already exists, bind immediately (e.g. replaying ReadyUI standalone)
            if (File.Exists(SelectionPath))
                QueueJson(File.ReadAllText(SelectionPath));
        }

        void OnDisable()
        {
            StopAllCoroutines();
            _watcher?.Dispose();
            _watcher = null;
        }

        void Update()
        {
            string json = null;
            lock (_lock)
            {
                if (_hasPending) { json = _pendingJson; _hasPending = false; }
            }
            if (json != null) ApplyJson(json);
        }

        public void Setup(CanvasGroup[] rowGroups, TextMeshProUGUI[] productTmps, TextMeshProUGUI noteTmp)
        {
            _rowGroups   = rowGroups;
            _productTmps = productTmps;
            _noteTmp     = noteTmp;
        }

        void StartWatching()
        {
            _watcher?.Dispose();
            string dir  = Path.GetDirectoryName(SelectionPath);
            string file = Path.GetFileName(SelectionPath);
            if (!Directory.Exists(dir)) return;

            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
        }

        void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                Thread.Sleep(50);
                QueueJson(File.ReadAllText(SelectionPath));
            }
            catch { }
        }

        void QueueJson(string json)
        {
            lock (_lock) { _pendingJson = json; _hasPending = true; }
        }

        void ApplyJson(string json)
        {
            try
            {
                var result = JsonUtility.FromJson<SelectionResult>(json);
                if (result?.rows == null) return;
                BindData(result.rows);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReadyUI] Failed to parse selection JSON: {e.Message}");
            }
        }

        public void BindData(SelectionRow[] rows)
        {
            if (_productTmps != null)
                for (int i = 0; i < rows.Length && i < _productTmps.Length; i++)
                    if (_productTmps[i] && !string.IsNullOrEmpty(rows[i].name))
                        _productTmps[i].text = rows[i].name;
            _dataReady = true;
        }

        IEnumerator RunSequence()
        {
            foreach (var cg in _rowGroups)
                if (cg) cg.alpha = 0f;

            yield return null;

            if (_noteTmp) StartCoroutine(AnimateDots());

            float elapsed = 0f;
            while (!_dataReady && elapsed < _dataTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < _rowGroups.Length; i++)
            {
                StartCoroutine(FadeRow(i));
                if (i < _rowGroups.Length - 1)
                    yield return new WaitForSeconds(0.07f);
            }
        }

        IEnumerator FadeRow(int i)
        {
            var cg = _rowGroups[i];
            if (cg == null) yield break;
            float dur = 0.4f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = EaseOut(Mathf.Clamp01(t / dur));
                yield return null;
            }
            cg.alpha = 1f;
        }

        IEnumerator AnimateDots()
        {
            string base_ = "Transforming your kitchen";
            string[] states = { base_ + " .", base_ + " ..", base_ + " ..." };
            int idx = 0;
            while (!_dataReady)
            {
                if (_noteTmp) _noteTmp.text = states[idx % 3];
                idx++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
