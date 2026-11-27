#!/usr/bin/env python3
# Un servidor de archivos para la escena «cuelga» (revision 27-11-2026): da la carpeta del catalogo como la version «catalogo» de GitHub, pero la
# PRIMERA parte del paquete de portadas (rocola_portadas_01.pak) la deja a la mitad: manda las cabeceras con su tamano entero, la mitad de los
# bytes y se queda callado (una conexion que se cuelga). Uso: cuelga.py PUERTO CARPETA
import http.server, os, socketserver, sys, time


class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def do_GET(self):
        ruta = self.path.split('?')[0]
        pref = '/catalogo/'
        f = os.path.join(sys.argv[2], ruta[len(pref):]) if ruta.startswith(pref) else None
        if not f or '..' in ruta or not os.path.isfile(f):
            self.send_response(404); self.send_header('Content-Length', '0'); self.end_headers(); return
        b = open(f, 'rb').read()
        self.send_response(200)
        self.send_header('Content-Length', str(len(b)))
        self.end_headers()
        if ruta.endswith('rocola_portadas_01.pak'):
            self.wfile.write(b[:len(b) // 2]); self.wfile.flush()
            time.sleep(300)
        else:
            self.wfile.write(b)


class S(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True


S(('127.0.0.1', int(sys.argv[1])), H).serve_forever()
