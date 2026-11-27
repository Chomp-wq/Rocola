#!/usr/bin/env python3
# Lo que escribe `rocola-xp --prueba-cielo` (lineas SOL, FIESTA y PADRE), contra lo de fuera: las salidas y puestas del sol contra el algoritmo
# de la NOAA (astral 3.2, en America/Mexico_City), a +-3 min; el calendario de PLAN_CIELO_VIVO.md §6 en 2026 dia por dia; y el tercer domingo de
# junio de 2025 a 2028 (los de verdad). Sale con 0 si todo cuadra.
#   sol_y_calendario.py SALIDA.txt        (PYTHONPATH con astral)
import sys, datetime
from zoneinfo import ZoneInfo
from astral import Observer
from astral.sun import sun
tz = ZoneInfo('America/Mexico_City')
mal = 0; peor = 0.0; n = 0
fiestas = {}; padres = {}
for l in open(sys.argv[1], encoding='utf-8'):
    p = l.split()
    if l.startswith('SOL '):
        _, c, f, la, lo, _, sale, _, pone = p
        d = datetime.date.fromisoformat(f)
        s = sun(Observer(float(la), float(lo)), date=d, tzinfo=tz)
        for nom, mio, ref in (('sale', sale, s['sunrise']), ('pone', pone, s['sunset'])):
            h = datetime.datetime.combine(d, datetime.time.fromisoformat(mio), tzinfo=tz)
            dif = (h - ref).total_seconds() / 60; peor = max(peor, abs(dif)); n += 1
            if abs(dif) > 3: mal += 1; print(f'MAL sol {c} {f} {nom}: {mio} y la NOAA {ref:%H:%M:%S} ({dif:+.1f} min)')
    elif l.startswith('FIESTA '):
        fiestas[p[1]] = ' '.join(p[2:])
    elif l.startswith('PADRE '):
        padres[p[1]] = p[2]
print(f'el sol: {n} salidas y puestas (cdmx y monterrey, 7 fechas), lo mas lejos de la NOAA: {peor:.2f} min')
esperado = {'2026-01-01': 'anio_nuevo', '2026-01-06': 'reyes', '2026-02-02': 'candelaria', '2026-02-14': 'amor', '2026-02-24': 'bandera',
            '2026-03-08': 'mujer', '2026-04-30': 'nino', '2026-05-10': 'madres', '2026-06-21': 'padre', '2026-09-15': 'independencia',
            '2026-09-16': 'independencia', '2026-10-31': 'muertos', '2026-11-01': 'muertos', '2026-11-02': 'muertos', '2026-11-20': 'revolucion',
            '2026-11-22': 'santa_cecilia'}
# diciembre (27-11-2026): Guadalupana solo el 12, posadas del 16 al 23, Navidad todos los demas dias
for d in range(1, 32): esperado[f'2026-12-{d}'] = 'guadalupana' if d == 12 else ('posadas' if 16 <= d <= 23 else 'navidad')
for k in sorted(set(esperado) | set(fiestas)):
    if esperado.get(k) != fiestas.get(k): mal += 1; print(f'MAL calendario {k}: «{fiestas.get(k)}» y §6 dice «{esperado.get(k)}»')
print(f'el calendario de 2026: {len(fiestas)} dias con fiesta, {"iguales a §6" if all(esperado.get(k) == fiestas.get(k) for k in set(esperado) | set(fiestas)) else "DISTINTOS"}')
padre = {'2025': '2025-06-15', '2026': '2026-06-21', '2027': '2027-06-20', '2028': '2028-06-18'}
for a, d in padre.items():
    if padres.get(a) != d: mal += 1; print(f'MAL dia del padre {a}: {padres.get(a)} (es {d})')
print('el tercer domingo de junio de 2025 a 2028: ' + ', '.join(padres.get(a, '?') for a in sorted(padre)))
sys.exit(1 if mal else 0)
