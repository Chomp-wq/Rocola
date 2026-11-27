#!/bin/sh
# LA PRUEBA DEL CATALOGO, sin red (sistema/catalogo/LEEME.md): arma el catalogo con la copia chica inventada (inventa.py) y comprueba
# el formato (valida), lo que debe salir (comprueba.py), que dos corridas dan los mismos bytes, que es igual a
# rocola_catalogo_prueba.tsv.gz (el que usa el grupo de la pared), --solo-cc0, los topes, que «valida» atrapa archivos malos y que
# «extrae» saca de un tar lo mismo. ACTUALIZA=1 sh prueba.sh rehace rocola_catalogo_prueba.tsv.gz (decirlo al grupo de la pared).
set -eu
AQUI=$(cd "$(dirname "$0")" && pwd)
ARMA="$AQUI/../arma_catalogo.py"
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT
PY=${PYTHON:-python3}
export PYTHONDONTWRITEBYTECODE=1

paso() { printf '== %s\n' "$*"; }
arma() {   # arma TABLAS SALIDA [opciones]
	d=$1; o=$2; shift 2
	"$PY" "$ARMA" arma --tablas "$d" --cache "$T/tablas/cache" --sin-red --hoy 2026-11-15 --corrida 1 --salida "$o" "$@" \
		2> "$o.registro" || { cat "$o.registro"; exit 1; }
}

paso "copia inventada"
"$PY" "$AQUI/inventa.py" "$T/tablas"

paso "armar dos veces: los mismos bytes"
arma "$T/tablas" "$T/a"
arma "$T/tablas" "$T/b"
cmp "$T/a/rocola_catalogo.tsv.gz" "$T/b/rocola_catalogo.tsv.gz"
cmp "$T/a/rocola_catalogo.txt" "$T/b/rocola_catalogo.txt"
python3 - "$T/a/rocola_catalogo.tsv.gz" <<'EOF'
import sys
d = open(sys.argv[1], 'rb').read()
assert d[4:8] == b'\0\0\0\0', 'gzip con fecha'
assert d[3] & 0x08 == 0, 'gzip con nombre de archivo'
EOF
grep -q "^sha256=$(sha256sum "$T/a/rocola_catalogo.tsv.gz" | cut -d' ' -f1)\$" "$T/a/rocola_catalogo.txt"
grep -q "^bytes=$(wc -c < "$T/a/rocola_catalogo.tsv.gz")\$" "$T/a/rocola_catalogo.txt"
grep -q '^version=20261114\.1$' "$T/a/rocola_catalogo.txt"

paso "igual a rocola_catalogo_prueba.tsv.gz"
if [ "${ACTUALIZA:-}" = 1 ]; then
	cp "$T/a/rocola_catalogo.tsv.gz" "$AQUI/rocola_catalogo_prueba.tsv.gz"
	echo "  rehecho: $AQUI/rocola_catalogo_prueba.tsv.gz"
fi
cmp "$T/a/rocola_catalogo.tsv.gz" "$AQUI/rocola_catalogo_prueba.tsv.gz"

paso "valida y comprueba"
"$PY" "$ARMA" valida "$T/a/rocola_catalogo.tsv.gz"
"$PY" "$AQUI/comprueba.py" normal "$T/a/rocola_catalogo.tsv.gz" "$T/tablas"

paso "--solo-cc0 (sin las etiquetas de MusicBrainz)"
arma "$T/tablas" "$T/cc0" --solo-cc0
"$PY" "$ARMA" valida "$T/cc0/rocola_catalogo.tsv.gz"
"$PY" "$AQUI/comprueba.py" cc0 "$T/cc0/rocola_catalogo.tsv.gz" "$T/tablas"
grep -q 'por etiquetas de MusicBrainz (m): 0' "$T/cc0/rocola_catalogo_informe.txt"

paso "topes (12 discos por letra: 4 por genero)"
arma "$T/tablas" "$T/tope" --tope 12
"$PY" "$ARMA" valida --tope 12 "$T/tope/rocola_catalogo.tsv.gz"
"$PY" "$AQUI/comprueba.py" tope "$T/tope/rocola_catalogo.tsv.gz" "$T/tablas"
if "$PY" "$ARMA" valida --tope 11 "$T/tope/rocola_catalogo.tsv.gz" > /dev/null; then echo "valida no vio el tope"; exit 1; fi

paso "valida atrapa archivos malos"
zcat "$T/a/rocola_catalogo.tsv.gz" > "$T/bien.tsv"
malo() {   # malo NOMBRE PROGRAMA_DE_PYTHON (recibe las lineas en «l» y las deja cambiadas)
	"$PY" - "$T/bien.tsv" "$T/malo.tsv.gz" "$2" <<'EOF'
import gzip, sys
l = open(sys.argv[1], encoding='utf-8').read().split('\n')[:-1]
exec(sys.argv[3])
with gzip.open(sys.argv[2], 'wb') as f:
    f.write(('\n'.join(l) + '\n').encode('utf-8'))
EOF
	if "$PY" "$ARMA" valida "$T/malo.tsv.gz" > "$T/malo.txt"; then echo "valida no vio: $1"; exit 1; fi
	echo "  atrapado: $1 ($(head -1 "$T/malo.txt"))"
}
malo "fama que sube" "i=[k for k,x in enumerate(l) if x.startswith('A\t')][1]; c=l[i].split('\t'); c[3]='1000000'; l[i]='\t'.join(c)"
malo "letra equivocada" "i=[k for k,x in enumerate(l) if x.startswith('A\t')][3]; c=l[i].split('\t'); c[1]='Zz'+c[1]; l[i]='\t'.join(c)"
malo "genero que no es de XP" "i=[k for k,x in enumerate(l) if x.startswith('A\t')][2]; c=l[i].split('\t'); c[2]='Otros'; l[i]='\t'.join(c)"
malo "cuenta de la letra" "i=[k for k,x in enumerate(l) if x.startswith('D\t')][5]; del l[i]"
malo "letra que falta" "l=[x for x in l if not x.startswith('L\tQ\t')]"
malo "MBID malo" "i=[k for k,x in enumerate(l) if x.startswith('D\t')][0]; c=l[i].split('\t'); c[4]='no-es-mbid'; l[i]='\t'.join(c)"
malo "siete discos" "i=[k for k,x in enumerate(l) if x.startswith('D\t')][0]; l[i+1:i+1]=[l[i]]*6"
malo "marcas malas (columna 7)" "i=[k for k,x in enumerate(l) if x.startswith('D\t')][0]; l[i]=l[i]+'\tz'"
malo "marcas fuera de orden" "i=[k for k,x in enumerate(l) if x.startswith('D\t')][0]; l[i]=l[i]+'\tjs'"
malo "cabecera" "l[0]=l[0].replace('\t1\t','\t2\t',1)"

paso "extrae: de un tar sale lo mismo"
"$PY" - "$T/tablas" "$T/copia.tar" <<'EOF'
import os, sys, tarfile, io
src, out = sys.argv[1], sys.argv[2]
with tarfile.open(out, 'w', format=tarfile.USTAR_FORMAT) as t:
    def pon(nombre, datos):
        i = tarfile.TarInfo(nombre); i.size = len(datos); i.mtime = 0; t.addfile(i, io.BytesIO(datos))
    pon('TIMESTAMP', open(os.path.join(src, 'TIMESTAMP'), 'rb').read())
    pon('mbdump/track', b'1\tno se usa\n')            # una tabla que no se usa: se salta
    for n in sorted(os.listdir(src)):
        p = os.path.join(src, n)
        if os.path.isfile(p) and n != 'TIMESTAMP':
            pon('mbdump/' + n, open(p, 'rb').read())
EOF
"$PY" "$ARMA" extrae --salida "$T/extraidas" < "$T/copia.tar" 2> "$T/extrae.registro" || { cat "$T/extrae.registro"; exit 1; }
test ! -e "$T/extraidas/track"
test "$(grep -c . "$T/extraidas/url")" -lt "$(grep -c . "$T/tablas/url")"   # url: solo las de Spotify
arma "$T/extraidas" "$T/x"
cmp "$T/a/rocola_catalogo.tsv.gz" "$T/x/rocola_catalogo.tsv.gz"

paso "numeros"
zcat "$T/a/rocola_catalogo.tsv.gz" | head -1
sed -n '3,12p' "$T/a/rocola_catalogo_informe.txt"
echo "PRUEBA DEL CATALOGO: BIEN"
