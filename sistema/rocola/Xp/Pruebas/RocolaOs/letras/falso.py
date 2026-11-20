#!/usr/bin/env python3
# falso.py PUERTO CARPETA DATOS.json : GENIUS Y LRCLIB FALSOS en 127.0.0.1 (solo para Pruebas/RocolaOs/letras/prueba.sh; datos inventados).
#   GET /search?q=..&per_page=..   (Genius) con «Authorization: Bearer LLAVE»: los «hits» de la primera frase de DATOS.genius contenida en q
#                                  (sin mayusculas); otra llave: 401; sin llave: 401
#   GET /api/get?track_name&artist_name&album_name&duration   (LRCLIB): lo de DATOS.lrclib["titulo|artista"] ({synced, plain}); si no, 404
# Escribe CARPETA/peticiones.txt (una linea por peticion: «ruta consulta-decodificada | ua=USER-AGENT | auth=bien|mal|no») y CARPETA/listo.
import json, os, sys, urllib.parse
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
puerto, carpeta, datos = int(sys.argv[1]), sys.argv[2], json.load(open(sys.argv[3], encoding="utf-8"))
os.makedirs(carpeta, exist_ok=True)
reg = open(os.path.join(carpeta, "peticiones.txt"), "a", encoding="utf-8")
class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def manda(self, cod, obj):
        b = json.dumps(obj).encode("utf-8")
        self.send_response(cod); self.send_header("Content-Type", "application/json"); self.send_header("Content-Length", str(len(b))); self.end_headers(); self.wfile.write(b)
    def do_GET(self):
        u = urllib.parse.urlparse(self.path); q = urllib.parse.parse_qs(u.query)
        a = self.headers.get("Authorization")
        auth = "no" if a is None else ("bien" if a == "Bearer " + datos.get("llave", "") else "mal")
        reg.write("%s %s | ua=%s | auth=%s\n" % (u.path, urllib.parse.unquote(u.query), self.headers.get("User-Agent", ""), auth)); reg.flush()
        if u.path == "/search":
            if auth != "bien": return self.manda(401, {"meta": {"status": 401}})
            t = (q.get("q", [""])[0]).lower()
            hits = []
            for frase, hs in datos.get("genius", {}).items():
                if frase in t:
                    hits = [{"type": "song", "result": {"title": h[0], "primary_artist": {"name": h[1]}}} for h in hs]; break
            return self.manda(200, {"meta": {"status": 200}, "response": {"hits": hits}})
        if u.path == "/api/get":
            k = q.get("track_name", [""])[0] + "|" + q.get("artist_name", [""])[0]
            x = datos.get("lrclib", {}).get(k)
            if x is None: return self.manda(404, {"code": 404, "name": "TrackNotFound", "message": "Failed to find specified track"})
            return self.manda(200, {"id": 1, "trackName": k.split("|")[0], "artistName": k.split("|")[1], "instrumental": False,
                                    "plainLyrics": x.get("plain"), "syncedLyrics": x.get("synced")})
        self.manda(404, {})
s = ThreadingHTTPServer(("127.0.0.1", puerto), H)
open(os.path.join(carpeta, "listo"), "w").close()
s.serve_forever()
