#!/usr/bin/env python3
# Escribe un reporte de METAR falso con la forma de la API de aviationweather.gov (obsTime en MINUTOS DE EDAD; metar_falso.py lo vuelve la hora de verdad).
#   metar_json.py ARCHIVO ID LAT LON EDAD_MIN WX NUBES(json) TEMP VIS WSPD
import json, sys
f, id_, la, lo, edad, wx, nubes, temp, vis, w = sys.argv[1:11]
r = {"icaoId": id_, "obsTime": int(edad), "temp": float(temp), "dewp": 10, "wdir": 150, "wspd": int(w), "visib": (float(vis) if vis != "10+" else "10+"), "altim": 1023.5,
     "wxString": (wx or None), "metarType": "METAR", "lat": float(la), "lon": float(lo), "name": "Aeropuerto falso", "clouds": json.loads(nubes)}
json.dump([r], open(f, "w"))
