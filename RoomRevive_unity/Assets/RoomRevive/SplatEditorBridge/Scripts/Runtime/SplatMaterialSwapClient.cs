// SplatMaterialSwapClient.cs
// Unity → browser-editor trigger (the reverse direction of LiveSplatLoader).
//
// A local Python server (default http://localhost:8766) drives the browser "PINK→MARBLE"
// splat editor. POSTing a swap makes the editor apply a cabinet + worktop material to the
// Gaussian-splat kitchen, auto-save the result to a .spz, which LiveSplatLoader then
// hot-reloads into the GaussianSplatRenderer. So:
//
//     SplatMaterialSwapClient.SwapMaterials(...)  →  POST /api/swap
//        → editor polls (~1 s) → re-encodes ~1.9M splats → writes .spz
//        → LiveSplatLoader watches the file → renderer updates   (total ~1–3 s)
//
// REQUIREMENTS (assumed already set up — not this script's job):
//   • The Python server is running on baseUrl.
//   • The browser editor tab is OPEN (it does the actual rendering) and its auto-save is linked.
//
// This is only the HTTP client. It never blocks the main thread (UnityWebRequest coroutine),
// and it never throws on network failure — connection-refused / non-200 surface as warnings.

using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RoomRevive.SplatEditorBridge
{
    [DisallowMultipleComponent]
    public class SplatMaterialSwapClient : MonoBehaviour
    {
        [Header("Server")]
        [Tooltip("Base URL of the local splat-editor server. Localhost only — no auth, no CORS. " +
                 "Use 127.0.0.1 (not localhost) to avoid the IPv4/IPv6 resolution quirk.")]
        public string baseUrl = "http://127.0.0.1:8766";

        [Header("Debug")]
        [Tooltip("Log every request/response. Warnings are always logged regardless.")]
        public bool debugLogs = true;

        [Tooltip("After each successful POST, GET /api/command to confirm the server actually stored " +
                 "the swap (id incremented, cab/wt match). The single best signal for 'did it register?'.")]
        public bool verifyAfterSwap = true;

        [Header("Status (read-only)")]
        [Tooltip("Human-readable result of the most recent request.")]
        public string lastStatus = "idle";
        [Tooltip("Total swap POSTs issued this session.")]
        public int requestCount;
        [Tooltip("What the last POST sent.")]
        public string lastSentCab = "";
        public string lastSentWt = "";
        [Tooltip("id returned by the last successful POST (-1 = none yet).")]
        public int lastPostId = -1;
        [Tooltip("Server's current command after the last read-back (id / cab / wt).")]
        public string lastServerCommand = "";

        [Header("Test (▶ play mode) — use the ⋮ context menu")]
        [Tooltip("Cabinet image filename used by 'Test Swap'. Get exact names from GET /api/materials.")]
        public string testCab = "407_Milano_walnut_reproduction.jpg";
        [Tooltip("Worktop image filename used by 'Test Swap'.")]
        public string testWt = "235_Venato_Bianco_marble_reproduction.jpg";

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Requests a material swap on the live editor. Either argument may be null/empty to
        /// leave that region unchanged. Non-blocking; the visible result lands in Unity in ~1–3 s
        /// (editor poll + re-encode + LiveSplatLoader reload).
        /// </summary>
        public void SwapMaterials(string cabFilename, string wtFilename)
        {
            bool hasCab = !string.IsNullOrEmpty(cabFilename);
            bool hasWt  = !string.IsNullOrEmpty(wtFilename);
            if (!hasCab && !hasWt)
            {
                Debug.LogWarning("[SplatSwap] SwapMaterials called with no cab and no wt — nothing to do.", this);
                return;
            }

            // Build the JSON by hand so we can OMIT a field entirely when it's null
            // (the API treats an omitted field as "leave that region unchanged").
            var sb = new StringBuilder("{");
            if (hasCab) sb.Append("\"cab\":\"").Append(EscapeJson(cabFilename)).Append('"');
            if (hasCab && hasWt) sb.Append(',');
            if (hasWt)  sb.Append("\"wt\":\"").Append(EscapeJson(wtFilename)).Append('"');
            sb.Append('}');

            lastSentCab = hasCab ? cabFilename : "(unchanged)";
            lastSentWt  = hasWt  ? wtFilename  : "(unchanged)";

            if (!EnsureCanRun()) return;
            StartCoroutine(PostSwap(sb.ToString()));
        }

        /// <summary>
        /// Fetches the valid cab/worktop filenames from GET /api/materials and hands them back
        /// (e.g. to populate a UI). On failure the callback is invoked with two empty arrays.
        /// </summary>
        public void FetchMaterials(Action<string[], string[]> onResult)
        {
            if (!EnsureCanRun())
            {
                onResult?.Invoke(Array.Empty<string>(), Array.Empty<string>());
                return;
            }
            StartCoroutine(GetMaterials(onResult));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Edit-mode swap: POSTs /api/swap without Play mode or a coroutine, so it works from OnValidate
        /// (e.g. scrubbing a preview index). Fire-and-forget on a background thread — never blocks the
        /// editor and stays silent if the server is down / the editor tab is closed.
        /// </summary>
        public static void SwapMaterialsEditor(string baseUrl, string cab, string wt)
        {
            bool hasCab = !string.IsNullOrEmpty(cab), hasWt = !string.IsNullOrEmpty(wt);
            if (!hasCab && !hasWt) return;

            var sb = new StringBuilder("{");
            if (hasCab) sb.Append("\"cab\":\"").Append(EscapeJson(cab)).Append('"');
            if (hasCab && hasWt) sb.Append(',');
            if (hasWt)  sb.Append("\"wt\":\"").Append(EscapeJson(wt)).Append('"');
            sb.Append('}');

            string url = (string.IsNullOrEmpty(baseUrl) ? "http://127.0.0.1:8766" : baseUrl).TrimEnd('/') + "/api/swap";
            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.Timeout = 4000;
                    using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                    using (var resp = (System.Net.HttpWebResponse)req.GetResponse()) { /* ignore body */ }
                }
                catch { /* server down / editor tab closed — silent in edit mode */ }
            });
        }
#endif

        // ── Coroutines ─────────────────────────────────────────────────────────

        System.Collections.IEnumerator PostSwap(string json)
        {
            string url = Url("/api/swap");
            int n = ++requestCount;
            if (debugLogs) Debug.Log($"[SplatSwap] #{n} POST {url}  {json}", this);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                lastStatus = $"#{n} FAILED: {Describe(req)}";
                Debug.LogWarning($"[SplatSwap] Swap #{n} failed: {Describe(req)}. " +
                                 "Is the Python server running and the editor tab open?", this);
                yield break;
            }

            SwapResponse resp = ParseJson<SwapResponse>(req.downloadHandler.text);
            if (resp != null && resp.ok)
            {
                lastPostId = resp.id;
                lastStatus = $"#{n} queued ✓ id={resp.id} (cab={lastSentCab}, wt={lastSentWt})";
                if (debugLogs) Debug.Log($"[SplatSwap] {lastStatus}", this);
                if (verifyAfterSwap) yield return GetCommand(resp.id);
            }
            else
            {
                lastStatus = $"#{n} non-ok: '{req.downloadHandler.text}'";
                Debug.LogWarning($"[SplatSwap] Server returned non-ok response: '{req.downloadHandler.text}'", this);
            }
        }

        // Reads back GET /api/command to confirm the server registered the swap we just POSTed.
        // expectedId < 0 means "just report whatever is there" (used by the Ping context menu).
        System.Collections.IEnumerator GetCommand(int expectedId)
        {
            using var req = UnityWebRequest.Get(Url("/api/command"));
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                lastServerCommand = $"read-back failed: {Describe(req)}";
                Debug.LogWarning($"[SplatSwap] {lastServerCommand}", this);
                yield break;
            }

            CommandResponse cmd = ParseJson<CommandResponse>(req.downloadHandler.text);
            if (cmd == null)
            {
                lastServerCommand = $"unparseable: '{req.downloadHandler.text}'";
                yield break;
            }

            lastServerCommand = $"id={cmd.id}, cab={cmd.cab ?? "(null)"}, wt={cmd.wt ?? "(null)"}";

            if (expectedId >= 0 && cmd.id != expectedId)
                Debug.LogWarning($"[SplatSwap] Read-back mismatch: server id={cmd.id} but we expected {expectedId}. " +
                                 "A later request may have overtaken this one, or the server didn't store it.", this);
            else if (debugLogs)
                Debug.Log($"[SplatSwap] Server confirms command → {lastServerCommand}", this);
        }

        System.Collections.IEnumerator GetMaterials(Action<string[], string[]> onResult)
        {
            string url = Url("/api/materials");
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SplatSwap] FetchMaterials failed: {Describe(req)}.", this);
                onResult?.Invoke(Array.Empty<string>(), Array.Empty<string>());
                yield break;
            }

            MaterialsResponse resp = ParseJson<MaterialsResponse>(req.downloadHandler.text);
            string[] cab = resp?.cab ?? Array.Empty<string>();
            string[] wt  = resp?.wt  ?? Array.Empty<string>();
            if (debugLogs) Debug.Log($"[SplatSwap] Materials: {cab.Length} cab, {wt.Length} wt", this);
            onResult?.Invoke(cab, wt);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        bool EnsureCanRun()
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning("[SplatSwap] baseUrl is empty.", this);
                return false;
            }
            // Coroutines only run on an active+enabled behaviour in Play mode.
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SplatSwap] Requests only run in Play mode (UnityWebRequest coroutine).", this);
                return false;
            }
            return true;
        }

        string Url(string path) => baseUrl.TrimEnd('/') + path;

        static string Describe(UnityWebRequest req) =>
            $"HTTP {req.responseCode} — {req.error}";

        static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static T ParseJson<T>(string text) where T : class
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return JsonUtility.FromJson<T>(text); }
            catch (Exception e)
            {
                Debug.LogWarning($"[SplatSwap] Could not parse response '{text}': {e.Message}");
                return null;
            }
        }

        // ── Context-menu tests (Play mode) ─────────────────────────────────────

        [ContextMenu("Test Swap (testCab + testWt)")]
        void ContextTestSwap() => SwapMaterials(testCab, testWt);

        [ContextMenu("Fetch Materials (log names)")]
        void ContextFetchMaterials() =>
            FetchMaterials((cab, wt) =>
                Debug.Log($"[SplatSwap] cab=[{string.Join(", ", cab)}]\nwt=[{string.Join(", ", wt)}]", this));

        [ContextMenu("Ping Server (GET /api/command)")]
        void ContextPing()
        {
            if (!EnsureCanRun()) return;
            StartCoroutine(GetCommand(-1));
        }

        // ── DTOs (JsonUtility) ─────────────────────────────────────────────────
        [Serializable] class SwapResponse      { public bool ok; public int id; }
        [Serializable] class MaterialsResponse { public string[] cab; public string[] wt; }
        [Serializable] class CommandResponse   { public int id; public string cab; public string wt; }
    }
}
