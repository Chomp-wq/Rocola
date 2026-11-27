#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""UN COVER ART ARCHIVE FALSO para la prueba de arma_portadas.py (nada de la red de verdad).
Uso: falso_caa.py PUERTO REGISTRO [--flaky MBID] [--mal MBID]
  GET /release-group/MBID/front-250   -> 302 a /img/MBID.jpg (como el de verdad, que redirige), salvo:
      MBID acabado en «0»      404 (sin portada)
      el de --flaky            500 las dos primeras veces y despues lo normal (el reintento)
      el de --mal              200 pero no es un JPEG
  GET /img/MBID.jpg            -> un JPEG de 8x8 valido con un comentario de relleno de ~12 KB y el MBID dentro (distinto en cada uno)
Cada peticion a /release-group se apunta en REGISTRO (una linea: el MBID). Crea REGISTRO.listo al abrir el puerto."""
import base64, http.server, socketserver, sys, struct, threading

JPEG = base64.b64decode(
    '/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDABALDA4MChAODQ4SERATGCgaGBYWGDEjJR0oOjM9PDkzODdASFxOQERXRTc4UG1RV19iZ2hnPk1xeXBkeFxlZ2P/'
    '2wBDARESEhgVGC8aGi9jQjhCY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2P/wAARCAAIAAgDASIAAhEBAxEB/8QA'
    'HwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2Jy'
    'ggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLD'
    'xMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3'
    'AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6'
    'goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwCjRRRX'
    'nn0R/9k=')
puerto, registro = int(sys.argv[1]), sys.argv[2]
flaky = sys.argv[sys.argv.index('--flaky') + 1] if '--flaky' in sys.argv else None
mal = sys.argv[sys.argv.index('--mal') + 1] if '--mal' in sys.argv else None
cerrojo = threading.Lock()
fallos = {'n': 0}


def jpeg(mbid):
    relleno = (mbid.encode() * 400)[:12000]
    com = b'\xff\xfe' + struct.pack('>H', len(relleno) + 2) + relleno      # un comentario COM: JPEG valido, distinto por MBID
    return JPEG[:2] + com + JPEG[2:]


class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def responde(self, cod, cuerpo=b'', tipo='text/plain', extra=None):
        self.send_response(cod)
        self.send_header('Content-Type', tipo)
        self.send_header('Content-Length', str(len(cuerpo)))
        for k, v in (extra or {}).items():
            self.send_header(k, v)
        self.end_headers()
        self.wfile.write(cuerpo)

    def do_GET(self):
        p = self.path.split('?')[0].split('/')
        if len(p) >= 4 and p[1] == 'release-group':
            mbid = p[2]
            with cerrojo:
                with open(registro, 'a') as f:
                    f.write(mbid + '\n')
                if mbid == flaky and fallos['n'] < 2:
                    fallos['n'] += 1
                    return self.responde(500)
            if mbid.endswith('0'):
                return self.responde(404)
            if mbid == mal:
                return self.responde(200, b'no soy un jpeg', 'image/jpeg')
            return self.responde(302, b'', 'text/plain', {'Location': '/img/%s.jpg' % mbid})
        if len(p) >= 3 and p[1] == 'img':
            return self.responde(200, jpeg(p[2].rsplit('.', 1)[0]), 'image/jpeg')
        self.responde(404)


class S(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True
    request_queue_size = 256


s = S(('127.0.0.1', puerto), H)
open(registro + '.listo', 'w').close()
s.serve_forever()
