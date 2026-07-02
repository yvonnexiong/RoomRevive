# editor_server.py — static file server for the PINK→MARBLE editor PLUS a tiny swap API.
#
# Serves this folder (so http://localhost:8766/pink_to_marble_editor.html works exactly as before),
# and adds an HTTP API so an external app (e.g. a Unity project) can request a material swap:
#
#   POST /api/swap      body JSON {"cab":"<cabinet image filename>","wt":"<worktop image filename>"}
#                       -> stores the latest command, returns {"ok":true,"id":<n>}
#                       (either field may be omitted/null to leave that region unchanged)
#   GET  /api/command   -> {"id":<n>,"cab":...,"wt":...}  (the browser polls this ~1x/sec)
#   GET  /api/materials -> {"cab":[...filenames],"wt":[...filenames]}  (discover valid names)
#
# The browser editor (which must be open in a tab) does the actual work: on a new command id it
# applies the two materials, which re-saves the linked .spz, which LiveSplatLoader hot-reloads in Unity.
#
# Run:  python editor_server.py 8766

import http.server, socketserver, json, os, sys, threading, shutil, re
from urllib.parse import urlparse, parse_qs

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8766
ROOT = os.path.dirname(os.path.abspath(__file__))
os.chdir(ROOT)  # serve this folder regardless of where we're launched from

# Live splat (what LiveSplatLoader watches / the browser auto-saves) and the "previous selection"
# backup. Paths are relative to this file so they work on any machine / clone location.
_LIVE_SPZ   = os.path.normpath(os.path.join(ROOT, "..", "RoomRevive_unity", "LiveSplat", "kitchen-copy.spz"))
_BACKUP_SPZ = os.path.normpath(os.path.join(ROOT, "..", "RoomRevive_unity", "LiveSplat", "kitchenLastSelected.spz"))
# Persisted editor session (mask + both channels' settings + camera) — written by "Save session",
# read back on load so the exact setup auto-restores. Plain JSON next to this server.
_SESSION_FILE = os.path.join(ROOT, "editor_session.json")
# Where the batch pre-render saves each combo's baked .spz (so runtime loads it without the server).
_PRERENDER_DIR = os.path.join(ROOT, "Prerendered Cabinets")

def save_prerender(key, data):
    """Write a baked combo .spz to Prerendered Cabinets/<key>.spz. Returns the filename."""
    safe = re.sub(r'[^a-zA-Z0-9._-]+', '_', key or '').strip('_') or 'combo'
    os.makedirs(_PRERENDER_DIR, exist_ok=True)
    path = os.path.join(_PRERENDER_DIR, safe + '.spz')
    with open(path, 'wb') as f:
        f.write(data)
    print(f"[prerender] saved {safe}.spz ({len(data)} bytes)")
    return safe + '.spz'

def backup_live_spz():
    """Copy the current live splat to the 'last selected' backup BEFORE a new swap overwrites it,
    so kitchenLastSelected.spz always holds the state from just before the latest change."""
    try:
        if os.path.exists(_LIVE_SPZ):
            shutil.copy2(_LIVE_SPZ, _BACKUP_SPZ)
            print(f"[backup] {os.path.basename(_LIVE_SPZ)} -> {os.path.basename(_BACKUP_SPZ)}")
        else:
            print(f"[backup] live splat not found, skipped: {_LIVE_SPZ}")
    except Exception as e:
        print(f"[backup] failed: {e}")

_lock = threading.Lock()
_command = {"id": 0, "cab": None, "wt": None}  # latest swap request (id increments on each POST)

def list_materials(folder):
    d = os.path.join(ROOT, folder)
    if not os.path.isdir(d):
        return []
    exts = (".jpg", ".jpeg", ".png", ".webp", ".avif")
    return sorted(f for f in os.listdir(d)
                  if f.lower().endswith(exts) and not f.startswith(".") and "_atlas" not in f.lower())

class Handler(http.server.SimpleHTTPRequestHandler):
    def _json(self, obj, code=200):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")  # allow non-browser / cross-origin callers
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        path = self.path.split("?", 1)[0]
        if path == "/api/command":
            with _lock:
                self._json(dict(_command))
            return
        if path == "/api/materials":
            self._json({"cab": list_materials("CabinetMaterials"),
                        "wt": list_materials("WorktopMaterials")})
            return
        if path == "/api/session":
            try:
                if os.path.exists(_SESSION_FILE):
                    with open(_SESSION_FILE, "r", encoding="utf-8") as f:
                        self._json({"ok": True, "session": json.load(f)})
                else:
                    self._json({"ok": True, "session": None})
            except Exception as e:
                self._json({"ok": False, "error": str(e)}, 500)
            return
        return super().do_GET()  # normal static file serving

    def do_POST(self):
        path = self.path.split("?", 1)[0]
        if path == "/api/swap":
            try:
                n = int(self.headers.get("Content-Length", 0) or 0)
                raw = self.rfile.read(n) if n else b"{}"
                data = json.loads(raw.decode("utf-8") or "{}")
            except Exception as e:
                self._json({"ok": False, "error": "bad JSON: " + str(e)}, 400)
                return
            cab = data.get("cab")
            wt = data.get("wt")
            # Snapshot the current live splat as "last selected" before this swap is applied.
            backup_live_spz()
            with _lock:
                _command["id"] += 1
                _command["cab"] = cab
                _command["wt"] = wt
                cid = _command["id"]
            self._json({"ok": True, "id": cid, "cab": cab, "wt": wt})
            return
        if path == "/api/save_prerender":
            try:
                key = (parse_qs(urlparse(self.path).query).get("key") or [""])[0]
                n = int(self.headers.get("Content-Length", 0) or 0)
                raw = self.rfile.read(n) if n else b""
                if not raw:
                    self._json({"ok": False, "error": "empty body"}, 400)
                    return
                name = save_prerender(key, raw)
                self._json({"ok": True, "file": name, "bytes": len(raw)})
            except Exception as e:
                self._json({"ok": False, "error": str(e)}, 500)
            return
        if path == "/api/session":
            try:
                n = int(self.headers.get("Content-Length", 0) or 0)
                raw = self.rfile.read(n) if n else b"{}"
                data = json.loads(raw.decode("utf-8") or "{}")   # validate it's JSON
                with open(_SESSION_FILE, "w", encoding="utf-8") as f:
                    json.dump(data, f, indent=1)
                print(f"[session] saved -> {os.path.basename(_SESSION_FILE)}")
                self._json({"ok": True, "path": os.path.basename(_SESSION_FILE)})
            except Exception as e:
                self._json({"ok": False, "error": str(e)}, 400)
            return
        self._json({"ok": False, "error": "unknown endpoint"}, 404)

    def log_message(self, *args):
        pass  # keep the console quiet

class ThreadingServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True
    allow_reuse_address = True

if __name__ == "__main__":
    with ThreadingServer(("", PORT), Handler) as httpd:
        print(f"Editor + swap API on http://localhost:{PORT}  (root={ROOT})")
        httpd.serve_forever()
