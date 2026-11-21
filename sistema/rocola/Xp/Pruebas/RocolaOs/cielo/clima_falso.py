#!/usr/bin/env python3
# UN OPEN-METEO Y UN «LUGAR POR INTERNET» FALSOS para la prueba del cielo (nunca la red de verdad). Sirve en 127.0.0.1:PUERTO:
#   /v1/forecast?...   el clima: el JSON de --clima (o un 500 con --falla)
#   /lugar             el lugar: {"city": ..., "latitude": ..., "longitude": ...} (como ipapi.co)
# y apunta cada peticion (la ruta con su consulta) en --registro. Escribe «listo» en --listo al empezar.
#   clima_falso.py PUERTO --registro R --listo L [--codigo 63] [--nubes 90] [--ciudad Monterrey --lat 25.69 --lon -100.32] [--falla]
import argparse, json, sys
from http.server import BaseHTTPRequestHandler, HTTPServer
a = argparse.ArgumentParser()
a.add_argument('puerto', type=int); a.add_argument('--registro'); a.add_argument('--listo')
a.add_argument('--codigo', type=int, default=63); a.add_argument('--nubes', type=int, default=90)
a.add_argument('--ciudad', default='Monterrey'); a.add_argument('--lat', type=float, default=25.6866); a.add_argument('--lon', type=float, default=-100.3161)
a.add_argument('--falla', action='store_true')
o = a.parse_args()
class H(BaseHTTPRequestHandler):
    def log_message(self, *x): pass
    def do_GET(self):
        with open(o.registro, 'a') as f: f.write(self.path + '\n')
        if self.path.startswith('/v1/forecast'):
            if o.falla: self.send_response(500); self.end_headers(); return
            cuerpo = {'latitude': o.lat, 'longitude': o.lon, 'current': {'time': '2026-11-20T13:00', 'interval': 900, 'weather_code': o.codigo,
                      'cloud_cover': o.nubes, 'precipitation': 2.1, 'rain': 2.0, 'showers': 0.1, 'snowfall': 0, 'wind_speed_10m': 18.5,
                      'wind_direction_10m': 240, 'temperature_2m': 16.4, 'visibility': 9000, 'is_day': 1}}
        elif self.path.startswith('/lugar'):
            cuerpo = {'ip': '203.0.113.7', 'city': o.ciudad, 'latitude': o.lat, 'longitude': o.lon}
        else:
            self.send_response(404); self.end_headers(); return
        b = json.dumps(cuerpo).encode()
        self.send_response(200); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(b))); self.end_headers(); self.wfile.write(b)
s = HTTPServer(('127.0.0.1', o.puerto), H)
open(o.listo, 'w').write('listo\n')
s.serve_forever()
