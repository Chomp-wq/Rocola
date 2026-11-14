#!/usr/bin/env python3
# UN GITHUB DE MENTIRA para probar rocola-buzon y rocola-actualiza sin internet ni llaves (lo arranca prueba.sh, fuera de la raiz de prueba;
# los guiones de dentro lo llaman con ROCOLA_API=http://127.0.0.1:PUERTO). Lee y escribe en la carpeta que se le da (dentro de la raiz de
# prueba, /gh):
#   ordenes.json      las ordenes de «Rocola en casa» (casa/ordenes.json del buzon)
#   repo/RUTA         lo que la rocola sube al buzon (PUT .../contents/RUTA): rocola/estado.json, rocola/respuestas.json...
#   releases.json     las versiones del repositorio de la rocola (GET .../releases); assets/ID, sus archivos
#   api.log           cada peticion (METODO RUTA)
#   uso: gh_falso.py CARPETA ARCHIVO_DEL_PUERTO
import base64, hashlib, json, os, sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

D = sys.argv[1]

class G(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def apunta(self):
        with open(os.path.join(D, "api.log"), "a") as f:
            f.write(self.command + " " + self.path + "\n")

    def manda(self, codigo, cuerpo=None, cab=None):
        b = b"" if cuerpo is None else (cuerpo if isinstance(cuerpo, bytes) else json.dumps(cuerpo).encode())
        self.send_response(codigo)
        for k, v in (cab or {}).items():
            self.send_header(k, v)
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        self.apunta()
        p = self.path.split("?")[0]
        if p.endswith("/contents/casa/ordenes.json"):
            f = os.path.join(D, "ordenes.json")
            if not os.path.exists(f):
                return self.manda(404, {"message": "Not Found"})
            t = open(f, "rb").read()
            et = '"' + hashlib.sha1(t).hexdigest() + '"'
            if self.headers.get("If-None-Match") == et:
                return self.manda(304)
            return self.manda(200, {"content": base64.b64encode(t).decode(), "sha": et.strip('"')}, {"ETag": et})
        if "/contents/" in p:
            r = p.split("/contents/", 1)[1]
            f = os.path.join(D, "repo", r)
            if not os.path.isfile(f):
                return self.manda(404, {"message": "Not Found"})
            t = open(f, "rb").read()
            return self.manda(200, {"content": base64.b64encode(t).decode(), "sha": hashlib.sha1(t).hexdigest()})
        if p.endswith("/releases"):
            f = os.path.join(D, "releases.json")
            return self.manda(200, open(f, "rb").read() if os.path.exists(f) else b"[]")
        if "/releases/assets/" in p:
            f = os.path.join(D, "assets", p.rsplit("/", 1)[1])
            return self.manda(200, open(f, "rb").read()) if os.path.exists(f) else self.manda(404)
        if p.count("/") == 3 and p.startswith("/repos/"):
            return self.manda(200, {"full_name": p[7:]})
        return self.manda(404, {"message": "Not Found"})

    def do_PUT(self):
        self.apunta()
        p = self.path.split("?")[0]
        n = int(self.headers.get("Content-Length", "0"))
        j = json.loads(self.rfile.read(n) or b"{}")
        if "/contents/" not in p:
            return self.manda(404)
        r = p.split("/contents/", 1)[1]
        f = os.path.join(D, "repo", r)
        os.makedirs(os.path.dirname(f), exist_ok=True)
        t = base64.b64decode(j.get("content", ""))
        open(f, "wb").write(t)
        return self.manda(201, {"content": {"sha": hashlib.sha1(t).hexdigest()}})

s = ThreadingHTTPServer(("127.0.0.1", 0), G)
open(sys.argv[2], "w").write(str(s.server_address[1]))
s.serve_forever()
