#!/usr/bin/env python3
# EL SERVIDOR DE MENTIRA para probar rocola-soloist y «spotify» sin internet: hace de soloist-builds.spotifycdn.com (sirve los .tar.gz con
# su Last-Modified y contesta 304 a If-Modified-Since, como el de verdad), de apresolve.spotify.com (/apresolve) y del WebSocket de Soloist
# (cualquier otra ruta: 426, como un WebSocket al que se le habla en HTTP). Lo arranca prueba.sh fuera de la raiz de prueba; los guiones
# de dentro le hablan en 127.0.0.1.
#   uso: servidor.py CARPETA ARCHIVO_DEL_PUERTO
#   CARPETA/.modo: «caido» -> 503 a todo; «lento N» -> espera N s antes de contestar; sin el archivo, normal
#   CARPETA/peticiones.log: cada peticion (METODO RUTA y si traia If-Modified-Since)
import email.utils, os, sys, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

D = sys.argv[1]


class S(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def contesta(self, cuerpo):
        try:
            modo = open(os.path.join(D, ".modo")).read().split()
        except OSError:
            modo = []
        with open(os.path.join(D, "peticiones.log"), "a") as f:
            f.write("%s %s%s\n" % (self.command, self.path, " (If-Modified-Since)" if self.headers.get("If-Modified-Since") else ""))
        if modo[:1] == ["lento"]:
            time.sleep(float(modo[1]))
        if modo[:1] == ["caido"]:
            return self.manda(503, b"caido\n")
        ruta = self.path.split("?")[0]
        if ruta == "/apresolve":
            return self.manda(200, b'{"accesspoint":["ap.falso:4070"]}\n', cuerpo=cuerpo)
        f = os.path.join(D, ruta.lstrip("/"))
        if ruta.endswith(".tar.gz") and os.path.isfile(f):
            mt = int(os.path.getmtime(f))
            ims = self.headers.get("If-Modified-Since")
            if ims:
                try:
                    if mt <= email.utils.parsedate_to_datetime(ims).timestamp():
                        return self.manda(304, b"", cab={"Last-Modified": email.utils.formatdate(mt, usegmt=True)}, cuerpo=False)
                except (TypeError, ValueError):
                    pass
            return self.manda(200, open(f, "rb").read(), cab={"Last-Modified": email.utils.formatdate(mt, usegmt=True)}, cuerpo=cuerpo)
        if ruta.endswith(".tar.gz"):
            return self.manda(404, b"no esta\n", cuerpo=cuerpo)
        return self.manda(426, b"Upgrade Required\n", cuerpo=cuerpo)   # (el WebSocket de Soloist, hablado en HTTP)

    def manda(self, codigo, b, cab=None, cuerpo=True):
        self.send_response(codigo)
        for k, v in (cab or {}).items():
            self.send_header(k, v)
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        if cuerpo:
            self.wfile.write(b)

    def do_GET(self):
        self.contesta(True)

    def do_HEAD(self):
        self.contesta(False)


s = ThreadingHTTPServer(("127.0.0.1", 0), S)
with open(sys.argv[2] + ".n", "w") as f:
    f.write(str(s.server_address[1]))
os.rename(sys.argv[2] + ".n", sys.argv[2])
s.serve_forever()
