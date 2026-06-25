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

import http.server, socketserver, json, os, sys, threading

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8766
ROOT = os.path.dirname(os.path.abspath(__file__))
os.chdir(ROOT)  # serve this folder regardless of where we're launched from

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
            with _lock:
                _command["id"] += 1
                _command["cab"] = cab
                _command["wt"] = wt
                cid = _command["id"]
            self._json({"ok": True, "id": cid, "cab": cab, "wt": wt})
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
