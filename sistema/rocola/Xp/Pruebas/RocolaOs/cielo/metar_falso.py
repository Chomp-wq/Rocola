#!/usr/bin/env python3
# UN aviationweather.gov, UN OPEN-METEO Y UN «LUGAR POR INTERNET» FALSOS para la parte «metar» de la prueba del cielo (nunca la red de verdad). Sirve en 127.0.0.1:PUERTO:
#   /api/data/metar?bbox=lat0,lon0,lat1,lon1&format=json&hours=2   los reportes de --metar dentro del cuadro (o 204 sin ninguno)
#   /api/data/metar?ids=XXXX&format=json&hours=2                    los de esa estacion
#   /v1/forecast?...                                                 el Open-Meteo de siempre (--codigo, --nubes; --falla da 500)
#   /lugar                                                           el «lugar por internet» (ciudad falsa lejos del bar, para ver que no se pide)
# --metar es un archivo JSON con una lista de reportes con los campos de la API de verdad; «obsTime» se da en MINUTOS DE EDAD (30 = hace 30 min) y el
# servidor lo vuelve la hora de verdad (segundos de epoca). --metar-falla da 500. Cada peticion (ruta con su consulta) se apunta en --registro.
#   metar_falso.py PUERTO --registro R --listo L [--metar M.json] [--codigo 63 --nubes 90] [--falla] [--metar-falla]
import argparse, json, time, urllib.parse
from http.server import BaseHTTPRequestHandler, HTTPServer
a = argparse.ArgumentParser()
a.add_argument('puerto', type=int); a.add_argument('--registro'); a.add_argument('--listo'); a.add_argument('--metar')
a.add_argument('--codigo', type=int, default=63); a.add_argument('--nubes', type=int, default=90)
a.add_argument('--falla', action='store_true'); a.add_argument('--metar-falla', action='store_true')
o = a.parse_args()
reportes = json.load(open(o.metar)) if o.metar else []
class H(BaseHTTPRequestHandler):
    def log_message(self, *x): pass
    def manda(self, cuerpo, codigo=200):
        b = json.dumps(cuerpo).encode()
        self.send_response(codigo); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(b))); self.end_headers(); self.wfile.write(b)
    def do_GET(self):
        with open(o.registro, 'a') as f: f.write(self.path + '\n')
        u = urllib.parse.urlparse(self.path); q = urllib.parse.parse_qs(u.query)
        if u.path == '/api/data/metar':
            if o.metar_falla: self.send_response(500); self.end_headers(); return
            sel = []
            if 'ids' in q: sel = [r for r in reportes if r['icaoId'] in q['ids'][0].split(',')]
            elif 'bbox' in q:
                la0, lo0, la1, lo1 = [float(x) for x in q['bbox'][0].split(',')]
                sel = [r for r in reportes if la0 <= r['lat'] <= la1 and lo0 <= r['lon'] <= lo1]
            if not sel: self.send_response(204); self.end_headers(); return
            ahora = time.time(); salida = []
            for r in sel:
                r = dict(r); r['obsTime'] = int(ahora - r['obsTime'] * 60); salida.append(r)
            self.manda(salida)
        elif u.path == '/v1/forecast':
            if o.falla: self.send_response(500); self.end_headers(); return
            self.manda({'latitude': 0, 'longitude': 0, 'current': {'time': '2026-11-20T13:00', 'interval': 900, 'weather_code': o.codigo,
                      'cloud_cover': o.nubes, 'precipitation': 2.1, 'rain': 2.0, 'showers': 0.1, 'snowfall': 0, 'wind_speed_10m': 18.5,
                      'wind_direction_10m': 240, 'temperature_2m': 25.4, 'visibility': 9000, 'is_day': 1}})
        elif u.path == '/lugar':
            self.manda({'ip': '203.0.113.7', 'city': 'Monterrey', 'latitude': 25.6866, 'longitude': -100.3161})
        else:
            self.send_response(404); self.end_headers()
s = HTTPServer(('127.0.0.1', o.puerto), H)
open(o.listo, 'w').write('listo\n')
s.serve_forever()
