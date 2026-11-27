#!/bin/sh
# LA PRUEBA DEL PAQUETE DE PORTADAS, sin red (arma_portadas.py; sistema/catalogo/LEEME.md, «Las portadas por adelantado»): arma el paquete con el
# catalogo chico de la prueba (rocola_catalogo_prueba.tsv.gz, inventado) y un Cover Art Archive FALSO (falso_caa.py: 302 como el de verdad, 404,
# un 500 que se reintenta y un cuerpo que no es JPEG). Comprueba: que entran los discos con portada y sin marcas (ni los de artistas de cine,
# karaoke, infantil o hablado), del artista mas famoso al menos; el tope de discos y de MB; las partes (varias, con un tope de 1 MB); el formato
# (valida); que otra corrida con --cache no pide nada; que «valida» atrapa una parte tocada; y que una corrida nueva borra las partes que sobran.
set -eu
AQUI=$(cd "$(dirname "$0")" && pwd)
PY=${PYTHON:-python3}
export PYTHONDONTWRITEBYTECODE=1
T=$(mktemp -d)
FALSO_PID=
trap 'rm -rf "$T"; [ -z "$FALSO_PID" ] || kill "$FALSO_PID" 2> /dev/null || true' EXIT
CAT="$AQUI/rocola_catalogo_prueba.tsv.gz"
paso() { printf '== %s\n' "$*"; }
PUERTO=$((30000 + $$ % 20000))

paso "lo que debe entrar (de las lineas del catalogo)"
"$PY" - "$CAT" "$T/esperados.txt" "$T/orden.txt" <<'EOF'
import gzip, re, sys
fuera_gen = {'Soundtrack', 'Karaoke', 'Infantil', 'Hablado'}
arts, cur = [], None
with gzip.open(sys.argv[1], 'rt', encoding='utf-8') as f:
    for l in f:
        c = l.rstrip('\n').split('\t')
        if c[0] == 'A':
            cur = [int(c[3]), len(arts), c[2], []]
            arts.append(cur)
        elif c[0] == 'D' and cur is not None:
            cur[3].append(c)
bien, orden = [], []
for fama, n, gen, ds in sorted(arts, key=lambda a: (-a[0], a[1])):
    for c in ds:
        if gen not in fuera_gen and c[5] == '1' and not (len(c) > 6 and c[6]):
            bien.append(c[4])
            orden.append(c[4])
open(sys.argv[2], 'w').write('\n'.join(sorted(set(bien))) + '\n')
open(sys.argv[3], 'w').write('\n'.join(orden) + '\n')
print('entrarian %d discos' % len(set(bien)))
EOF
N=$(wc -l < "$T/esperados.txt")
test "$N" -gt 300
"$PY" "$AQUI/../arma_portadas.py" lista --catalogo "$CAT" | grep -q "^$N discos van a la pared"

paso "el Cover Art Archive falso"
FLAKY=$(sed -n 5p "$T/orden.txt"); MAL=$(sed -n 7p "$T/orden.txt")
case "$FLAKY" in *0) FLAKY=$(sed -n 6p "$T/orden.txt");; esac
case "$MAL" in *0) MAL=$(sed -n 8p "$T/orden.txt");; esac
"$PY" "$AQUI/falso_caa.py" "$PUERTO" "$T/caa.txt" --flaky "$FLAKY" --mal "$MAL" &
FALSO_PID=$!
for i in $(seq 1 50); do [ -f "$T/caa.txt.listo" ] && break; sleep 0.1; done
: > "$T/caa.txt"
BASE="http://127.0.0.1:$PUERTO"
printf 'version=20261114.1\n' > "$T/rocola_catalogo.txt"

paso "armar (partes de 1 MB, con cache)"
"$PY" "$AQUI/../arma_portadas.py" arma --catalogo "$CAT" --txt "$T/rocola_catalogo.txt" --salida "$T/s" --cache "$T/cache" --base "$BASE" \
  --max-discos 100000 --parte-mb 1 --hilos 8 --reintentos 3 2> "$T/arma.txt" || { cat "$T/arma.txt"; exit 1; }
tail -2 "$T/arma.txt"
"$PY" "$AQUI/../arma_portadas.py" valida --salida "$T/s"
grep -q '^formato=1$' "$T/s/rocola_portadas.txt"
grep -q '^catalogo=20261114.1$' "$T/s/rocola_portadas.txt"
PARTES=$(sed -n 's/^partes=//p' "$T/s/rocola_portadas.txt")
test "$PARTES" -ge 3
test "$(ls "$T/s"/rocola_portadas_*.pak | wc -l)" = "$PARTES"

paso "el contenido: solo lo que va a la pared, en el orden de fama; el que fallo y se reintento esta; el que no era JPEG, no"
"$PY" - "$T/s" "$T/esperados.txt" "$T/orden.txt" "$FLAKY" "$MAL" "$T/caa.txt" <<'EOF'
import struct, sys, os, re
d, esperados, orden, flaky, mal, registro = sys.argv[1:7]
esperados = set(open(esperados).read().split())
orden = open(orden).read().split()
en, secuencia = {}, []
for n in sorted(x for x in os.listdir(d) if x.endswith('.pak')):
    b = open(os.path.join(d, n), 'rb').read()
    k = struct.unpack('<I', b[4:8])[0]
    for i in range(k):
        mb = b[8 + 24 * i:24 + 24 * i].hex()
        mbid = '%s-%s-%s-%s-%s' % (mb[:8], mb[8:12], mb[12:16], mb[16:20], mb[20:])
        off, ln = struct.unpack('<II', b[24 + 24 * i:32 + 24 * i])
        en[mbid] = b[off:off + ln]
        secuencia.append(mbid)
assert set(en) <= esperados, 'entro algo que no va a la pared: %s' % sorted(set(en) - esperados)[:3]
sin = [m for m in orden if m.endswith('0')]            # los que el Cover Art Archive falso da 404
assert not any(m in en for m in sin), 'entro uno sin portada'
assert flaky in en, 'el del 500 reintentado no esta'
assert mal not in en, 'entro un cuerpo que no era JPEG'
assert all(b[:2] == b'\xff\xd8' and m.encode() in b for m, b in en.items()), 'una portada que no es su JPEG'
esperados_en_orden = [m for m in orden if m in en]
assert secuencia == esperados_en_orden, 'el orden no es el de fama'
vistos = open(registro).read().split()
assert vistos.count(flaky) >= 3, 'el del 500 se pidio %d veces' % vistos.count(flaky)
print('%d portadas en el orden de fama; %d sin portada saltadas; el reintento si; el que no era JPEG, no' % (len(en), len(sin)))
EOF

paso "otra corrida con la cache: ni una peticion mas"
VISTAS=$(wc -l < "$T/caa.txt")
"$PY" "$AQUI/../arma_portadas.py" arma --catalogo "$CAT" --txt "$T/rocola_catalogo.txt" --salida "$T/s2" --cache "$T/cache" --base "$BASE" \
  --max-discos 100000 --parte-mb 1 --hilos 8 --reintentos 3 2> "$T/arma2.txt" || { cat "$T/arma2.txt"; exit 1; }
test "$(wc -l < "$T/caa.txt")" = "$VISTAS"
cmp "$T/s/rocola_portadas.txt" "$T/s2/rocola_portadas.txt"

paso "los topes: 40 discos; 1 MB"
"$PY" "$AQUI/../arma_portadas.py" arma --catalogo "$CAT" --salida "$T/t1" --cache "$T/cache" --base "$BASE" --max-discos 40 2> /dev/null
grep -q '^discos=[0-9]*$' "$T/t1/rocola_portadas.txt"
test "$(sed -n 's/^discos=//p' "$T/t1/rocola_portadas.txt")" -le 40
"$PY" "$AQUI/../arma_portadas.py" valida --salida "$T/t1"
"$PY" "$AQUI/../arma_portadas.py" arma --catalogo "$CAT" --salida "$T/t2" --cache "$T/cache" --base "$BASE" --max-mb 1 --parte-mb 1 2> /dev/null
test "$(sed -n 's/^bytes=//p' "$T/t2/rocola_portadas.txt")" -le 1100000
"$PY" "$AQUI/../arma_portadas.py" valida --salida "$T/t2"

paso "valida atrapa lo tocado"
cp -r "$T/s" "$T/m"
P1=$(ls "$T/m"/rocola_portadas_*.pak | head -1)
"$PY" - "$P1" <<'EOF'
import sys
b = bytearray(open(sys.argv[1], 'rb').read()); b[-5] ^= 0xFF; open(sys.argv[1], 'wb').write(b)
EOF
if "$PY" "$AQUI/../arma_portadas.py" valida --salida "$T/m" 2> "$T/m.txt"; then echo "valida no vio la parte tocada"; exit 1; fi
echo "  atrapado: $(head -1 "$T/m.txt")"
rm "$P1"
if "$PY" "$AQUI/../arma_portadas.py" valida --salida "$T/m" 2> "$T/m.txt"; then echo "valida no vio la parte que falta"; exit 1; fi
echo "  atrapado: $(head -1 "$T/m.txt")"

paso "una corrida nueva borra las partes que sobran"
cp "$T/s/rocola_portadas_01.pak" "$T/t1/rocola_portadas_09.pak"
"$PY" "$AQUI/../arma_portadas.py" arma --catalogo "$CAT" --salida "$T/t1" --cache "$T/cache" --base "$BASE" --max-discos 40 2> /dev/null
test ! -e "$T/t1/rocola_portadas_09.pak"

paso "sin portadas no se publica"
printf '#rocola-catalogo\t1\t2026-11-14\t0\t0\n' | gzip -n > "$T/vacio.tsv.gz"
if "$PY" "$AQUI/../arma_portadas.py" arma --catalogo "$T/vacio.tsv.gz" --salida "$T/v" --base "$BASE" 2> "$T/v.txt"; then echo "armo un paquete vacio"; exit 1; fi
echo "  atrapado: $(tail -1 "$T/v.txt")"
echo "PRUEBA DEL PAQUETE DE PORTADAS: BIEN"
