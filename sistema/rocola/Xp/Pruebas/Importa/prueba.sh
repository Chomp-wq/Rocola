#!/bin/bash
# LA PRUEBA DEL IMPORTADOR (`rocola-xp --importa-xp`, Nativo/ImportaXp.cs) Y DE LO GRANDE FUERA DE LO APRENDIDO (Nativo/DatosGrandes.cs),
# con datos INVENTADOS: unos discos falsos con musica generada (tonos, silencio y ruido, ffmpeg) y la carpeta de datos de una rocola de XP
# falsa (mundo.py: lo que midio rocola-xp de esa musica, con las rutas de Windows). Hace falta ser root (unshare y mount: la carpeta de XP
# se monta en solo lectura mientras se importa, como en Rocola OS), ffmpeg y python3.
#   bash prueba.sh PROGRAMA [importa usa material]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT, bin/Release/net10.0) o aot:CARPETA (NativeAOT, con el ejecutable rocola-xp)
#   W: la carpeta de trabajo (por defecto /root/rocola-trabajo/act/xp-datos/importa); se rehace entera en cada corrida
# importa: la importacion tabla por tabla, la carpeta de XP sin tocar, la segunda llamada que no hace nada, la copia del lanzador de antes,
#          la re-importacion de otra carpeta (.antes-xp y la configuracion juntada), el disco que cambia de nombre y los fallos; y (revision)
#          un apagon a media importacion y a media re-importacion (con strace, si esta) y rocola-xp abierta antes de importar sin la musica.
# usa:     rocola-xp abierto fuera de pantalla con lo importado: no vuelve a medir lo medido por XP (solo la cancion nueva), los botones
#          importados valen, y al sonar una cancion importada su sello (rocola_fuentes.txt) vale tal cual (con el sello de Windows no).
# material: el material para entrenar la IA, las miniaturas y el respaldo: sin ROCOLA_IA_DATOS en la carpeta de datos (como XP); con ella, alli; con
#          menos de 256 MB libres alli, la cosecha no junta nada; y si no se puede escribir alli, a la carpeta de datos.
# Al final: «TODO BIEN» o cuantas MAL. Cada comprobacion dice BIEN o MAL en una linea.
set -u
G=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$G/../../../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [importa usa material]}"; shift
partes="${*:-importa usa material}"
W=${W:-/root/rocola-trabajo/act/xp-datos/importa}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
D=$W/discos; XPC=$D/sda1/Rocola/Release
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
hay() { [ -e "$1" ]; }
nohay() { [ ! -e "$1" ]; }
igual() { cmp -s "$1" "$2"; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
lineas() { [ "$(wc -l < "$1")" = "$2" ]; }
sinwindows() { ! grep -qE '[A-Za-z]:\\' "$1"; }
# los discos falsos en solo lectura (como monta rocola-discos los otros discos) y el importador dentro: $1 destino, $2 la carpeta (o el
# archivo que la dice), lo demas los discos. Deja en $W/sale.txt y $W/err.txt lo que dijo, y en $rc como salio.
importa() {
	local dst="$1"; shift
	# (revision) con CORTE=N, un apagon a media importacion: strace mata el programa en seco al ir a cambiar de nombre el archivo N (ese
	# cambio no se hace); lo que quede escrito es lo que dejaria un apagon en ese punto
	local pre=(); [ -n "${CORTE:-}" ] && pre=(strace -f -qq -o /dev/null -e trace=rename -e "inject=rename:signal=KILL:when=$CORTE")
	unshare -m -- sh -c 'd=$1; shift; mount --bind -o ro "$d" "$d" && exec "$@"' sh "$D" "${pre[@]}" env ROCOLA_DATOS="$dst" "${RX[@]}" --importa-xp "$@" > "$W/sale.txt" 2> "$W/err.txt"
	rc=$?
}
# lo que hay en una carpeta, con su contenido y su fecha (para ver que nadie la toco)
foto() { (cd "$1" && find . -printf '%P %y %s %T@\n' | sort && find . -type f -print0 | sort -z | xargs -0 -r sha1sum) ; }
# solo el contenido (sin las fechas ni la marca, que lleva la de la importacion): dos importaciones que acaban igual
contenido() { (cd "$1" && find . -type f ! -name rocola_importado_xp.txt -print0 | sort -z | xargs -0 -r sha1sum) ; }
# rocola-xp fuera de pantalla con los datos de $1 hasta que se cumpla $3 (o $2 segundos); despues SENAL (TERM de fabrica). Lo demas: VAR=valor.
corre() {
	local datos="$1" max="$2" cond="$3"; shift 3
	( cd "$datos" && exec env ROCOLA_DATOS="$datos" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 "$@" "${RX[@]}" ) > "$datos.salida.txt" 2>&1 &
	local pid=$! t=0
	while [ $t -lt $((max * 2)) ] && kill -0 $pid 2> /dev/null; do eval "$cond" && break; sleep 0.5; t=$((t + 1)); done
	kill -${SENAL:-TERM} $pid 2> /dev/null
	t=0; while kill -0 $pid 2> /dev/null && [ $t -lt 60 ]; do sleep 0.5; t=$((t + 1)); done
	kill -9 $pid 2> /dev/null; wait $pid 2> /dev/null
}
# al acabar (tambien si se corta): ninguna rocola de esta prueba queda abierta (las que se abrieron con sus datos en $W)
quedan() { local p; for p in $(pgrep -f rocola-xp); do tr '\0' '\n' < /proc/$p/environ 2> /dev/null | grep -q "^ROCOLA_DATOS=$W/" && echo $p; done; }
limpia() { local p; for p in $(quedan); do echo "(quedaba abierta una rocola de la prueba: $p)"; kill -9 $p; done; }
trap limpia EXIT
gen() { mkdir -p "$(dirname "$1")"; ffmpeg -hide_banner -loglevel error -y -f lavfi -i "$2" -t "$3" -ac 2 -ar 44100 ${5:--c:a libmp3lame -b:a 128k} -metadata title="$4" "$1"; }
# los ticks de .NET de la fecha de escritura y de la «creacion» que .NET da en Linux (la mas vieja de escritura y cambio)
ticks() { python3 -c 'import os,sys; s=os.stat(sys.argv[1]); t=lambda n: 621355968000000000 + n // 100; print(t(s.st_mtime_ns), t(min(s.st_mtime_ns, s.st_ctime_ns)))' "$1"; }

for d in "$D" "$W/prep" "$W/run" "$W/destino1" "$W/destino2" "$W/destino3" "$W/destino4" "$W/destino5" "$W/destino6" "$W/destino7" "$W/destino8" "$W/usa" "$W/usa2" "$W/mat"; do rm -rf "$d"; done
rm -f "$W"/*.salida.txt; mkdir -p "$W/run"

# ================= EL MUNDO =================
echo "=== el mundo (discos falsos y la carpeta de una rocola de XP falsa)"
M=$D/sda1/Rocola/musica
gen "$M/Los Inventados/Disco Uno/01 Tono La.mp3" "sine=frequency=440" 6 "Tono La"
gen "$M/Los Inventados/Disco Uno/02 Tono Mi.mp3" "sine=frequency=330" 6 "Tono Mi"
gen "$M/Los Inventados/Disco Uno/03 Canción Ñandú.mp3" "sine=frequency=660" 6 "Cancion Nandu"   # con acentos (en XP, en mayusculas)
gen "$M/Banda Ficticia/Primer Disco/01 Pulso.mp3" "sine=frequency=220" 6 "Pulso"
gen "$D/sdb1/Musica Extra/Grupo Prueba/Disco Dos/01 Silencio.mp3" "anullsrc=r=44100:cl=stereo" 6 "Silencio"
gen "$D/sdb1/Musica Extra/Grupo Prueba/Disco Dos/02 Tono Alto.mp3" "sine=frequency=1000" 6 "Tono Alto"
# la fecha de los archivos: la de cuando se grabaron (en XP la copia conserva la de escritura)
find "$D" -name '*.mp3' -exec touch -m -d '2019-05-01 10:00:00.1234567' {} +
# una copia parcial de la musica de D: en otro disco (sdc1, que va PRIMERO en la lista): D: no se confunde con el
mkdir -p "$D/sdc1/Musica Extra/Grupo Prueba/Disco Dos"; cp -p "$D/sdb1/Musica Extra/Grupo Prueba/Disco Dos/01 Silencio.mp3" "$D/sdc1/Musica Extra/Grupo Prueba/Disco Dos/"
# lo que mide rocola-xp de esa musica (lo mismo que habria medido XP): el repaso de las cinco
mkdir -p "$W/prep"; printf 'diagnostico=1\r\ndir=%s\r\ndir=%s\r\n' "$M" "$D/sdb1/Musica Extra" > "$W/prep/rocola_config.txt"
printf 'arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=77\r\nsaltar=83\r\n' > "$W/prep/rocola_teclas.txt"
corre "$W/prep" 90 '[ "$(grep -c "|/" "$W/prep/rocola_sonoridad.txt" 2> /dev/null)" = 6 ] && [ "$(grep -c "|/" "$W/prep/rocola_fuentes.txt" 2> /dev/null)" = 6 ]'
si "rocola-xp midio las seis canciones inventadas (lo que mediria XP)" lineas "$W/prep/rocola_volumen.txt" 6
python3 "$G/mundo.py" "$D" "$W/prep" "$XPC" "$REPO" > /dev/null
# la cancion nueva: llega despues (XP no la midio)
gen "$M/Banda Ficticia/Primer Disco/02 Nueva.mp3" "sine=frequency=550" 6 "Nueva"
echo "$XPC" > "$W/run/rocola-datos-xp"   # como lo deja rocola-discos
DISCOS=("$D/sdc1" "$D/sda1" "$D/sdb1")
LA=$M/Los\ Inventados/Disco\ Uno/01\ Tono\ La.mp3; SIL=$D/sdb1/Musica\ Extra/Grupo\ Prueba/Disco\ Dos/01\ Silencio.mp3

if [[ " $partes " == *" importa "* ]]; then
	echo "=== importa"
	foto "$D/sda1/Rocola" > "$W/foto-antes.txt"   # la carpeta de XP y la musica de su disco
	T=$W/destino1
	importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
	si "la primera importacion sale con 0" [ $rc = 0 ]
	si "y lo dice en una linea" contiene "$W/sale.txt" "importado de $XPC a $T"
	si "nada en la salida de errores" [ ! -s "$W/err.txt" ]
	for t in rocola_sonoridad.txt rocola_volumen.txt rocola_eq.txt rocola_fuentes.txt rocola_duracion.txt rocola_afondo.txt rocola_ia_hechas.txt rocola_stats.txt rocola_billar_nivel.txt rocola_config.txt; do
		si "$t traido, sin ninguna ruta de Windows" sinwindows "$T/$t"
		si "$t: la cancion que ya no esta, fuera" nocontiene "$T/$t" "No Esta"
	done
	si "rocola_sonoridad.txt: la cabecera y las seis" lineas "$T/rocola_sonoridad.txt" 7
	si "rocola_volumen.txt: las seis (sin la ultima linea a medias)" lineas "$T/rocola_volumen.txt" 6
	si "rocola_volumen.txt: la linea a medias no esta" nocontiene "$T/rocola_volumen.txt" "0.0123"
	si "rocola_eq.txt: las seis" lineas "$T/rocola_eq.txt" 6
	si "rocola_fuentes.txt: la cabecera y las seis" lineas "$T/rocola_fuentes.txt" 7
	si "rocola_duracion.txt: las seis" lineas "$T/rocola_duracion.txt" 6
	si "la de otras mayusculas («LOS INVENTADOS\\disco uno\\01 tono la.mp3») con su nombre del disco" contiene "$T/rocola_volumen.txt" "|$LA"
	si "con acentos y otras mayusculas («03 CANCIÓN ÑANDÚ.mp3») tambien" contiene "$T/rocola_volumen.txt" "|$M/Los Inventados/Disco Uno/03 Canción Ñandú.mp3"
	si "D: va al disco con su musica (sdb1), no a la copia parcial de sdc1, que va primero" contiene "$T/rocola_volumen.txt" "|$SIL"
	si "y nada de sdc1" nocontiene "$T/rocola_volumen.txt" "$D/sdc1/"
	si "la marca dice C: -> sda1" contiene "$T/rocola_importado_xp.txt" "unidad C: -> $D/sda1 ("
	si "la marca dice D: -> sdb1" contiene "$T/rocola_importado_xp.txt" "unidad D: -> $D/sdb1 ("
	# el sello: la «creada» de Linux (la de .NET en Linux), la escrita de siempre
	ok=1
	while IFS='|' read -r n c e h1 h2 r; do
		r=${r%$'\r'}; [ -z "$r" ] && continue
		set -- $(ticks "$r"); [ "$c" = "$2" ] && [ "$e" = "$1" ] && [ "$n" = "$(stat -c %s "$r")" ] || { ok=0; echo "      sello: $n|$c|$e de $r (Linux: $1 $2)"; }
	done < <(tail -n +2 "$T/rocola_fuentes.txt")
	si "rocola_fuentes.txt: el largo y la escrita de XP, la creada que vera rocola-xp en Linux" [ $ok = 1 ]
	si "y lo dice la marca" contiene "$T/rocola_importado_xp.txt" "6 con la fecha de creacion de Linux"
	si "rocola_afondo.txt: dos y la que no esta fuera" lineas "$T/rocola_afondo.txt" 2
	si "rocola_ia_hechas.txt: los niveles conservados" contiene "$T/rocola_ia_hechas.txt" "2|$LA"
	si "rocola_stats.txt: los totales" contiene "$T/rocola_stats.txt" "monedas=12"
	si "rocola_stats.txt: los artistas tal cual" contiene "$T/rocola_stats.txt" "4|Los Inventados"
	si "rocola_stats.txt: el genero de la cancion" contiene "$T/rocola_stats.txt" "Cumbia|$LA"
	si "rocola_billar_nivel.txt: las dos que estan" lineas "$T/rocola_billar_nivel.txt" 2
	si "rocola_billar_bitacora.txt: tal cual (solo nombres de archivo)" igual "$T/rocola_billar_bitacora.txt" "$XPC/rocola_billar_bitacora.txt"
	si "rocola_teclas.txt: tal cual" igual "$T/rocola_teclas.txt" "$XPC/rocola_teclas.txt"
	si "rocola_config.txt: dir=D:\\MUSICA EXTRA traducida a su carpeta" contiene "$T/rocola_config.txt" "dir=$D/sdb1/Musica Extra"
	si "rocola_config.txt: la carpeta que no esta (E:\\Nada), fuera" nocontiene "$T/rocola_config.txt" "Nada"
	si "rocola_config.txt: respaldo_dir= quitada" nocontiene "$T/rocola_config.txt" "respaldo_dir="
	si "rocola_config.txt: lo demas tal cual" contiene "$T/rocola_config.txt" "noche_pct=80"
	si "el modelo de IAS/, tal cual" igual "$T/IAS/rocola_ia.bin" "$XPC/IAS/rocola_ia.bin"
	si "y su copia anterior" igual "$T/IAS/rocola_ia.bin.anterior-1" "$XPC/IAS/rocola_ia.bin.anterior-1"
	si "el suelto «Rocola_EQ_IA.bin», como rocola_eq_ia.bin (rocola-xp lo pasa a IAS/)" igual "$T/rocola_eq_ia.bin" "$XPC/Rocola_EQ_IA.bin"
	for n in rocola_estado.txt rocola_noche.txt rocola_error.txt rocola_calidad.txt rocola_incompatibles.txt rocola_biblioteca.txt rocola_letras.txt rocola_diagnostico.txt rocola_diagnostico_2026-11-01_1200.txt rocola_miniaturas respaldo rocola_ia_datos.bin rocola_ia_datos.idx; do
		si "no se trae $n" nohay "$T/$n"
	done
	si "la marca: de que carpeta" contiene "$T/rocola_importado_xp.txt" "carpeta=$XPC"
	si "la marca: la primera linea como la del programa anterior" contiene "$T/rocola_importado_xp.txt" "Importado de la rocola de XP ($XPC) el "
	si "la marca: donde esta dentro de su disco" contiene "$T/rocola_importado_xp.txt" "relativa=Rocola/Release"
	si "sin la carpeta de paso ni la de en curso" bash -c '[ ! -e "$1/.importa-xp.tmp" ] && [ ! -e "$1/.importa-xp.en-curso" ]' sh "$T"
	foto "$T" > "$W/foto-destino1.txt"; contenido "$T" > "$W/contenido-destino1.txt"
	# (revision) UN APAGON A MEDIA IMPORTACION: muere al ir a poner el 3.er archivo en su sitio (dos ya puestos, sin la marca); la siguiente
	# llamada acaba lo que faltaba y todo queda igual que una importacion sin cortes
	if command -v strace > /dev/null; then
		T=$W/destino6
		CORTE=3 importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
		si "(apagon a media importacion) muere sin dejar la marca" bash -c '[ $1 != 0 ] && [ ! -e "$2/rocola_importado_xp.txt" ] && [ -e "$2/.importa-xp.en-curso" ]' sh $rc "$T"
		importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
		contenido "$T" > "$W/contenido-destino6.txt"
		si "(apagon a media importacion) la siguiente acaba: lo mismo que sin el corte, con la marca y sin restos" bash -c '[ $1 = 0 ] && cmp -s "$2" "$3" && [ -e "$4/rocola_importado_xp.txt" ] && [ ! -e "$4/.importa-xp.en-curso" ] && [ ! -e "$4/.importa-xp.tmp" ]' sh $rc "$W/contenido-destino1.txt" "$W/contenido-destino6.txt" "$T"
		T=$W/destino1
	else echo "(sin strace: no se prueba el apagon a media importacion)"; fi
	importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
	si "la segunda llamada sale con 0" [ $rc = 0 ]
	si "y dice que ya estaba" contiene "$W/sale.txt" "ya importado de $XPC"
	foto "$T" > "$W/foto-destino1b.txt"
	si "y no toca nada (ni las fechas)" igual "$W/foto-destino1.txt" "$W/foto-destino1b.txt"

	# la copia del lanzador de antes (rocola_de_xp.txt): los botones y la configuracion copiados tal cual de XP y las carpetas de la musica
	T=$W/destino2; mkdir -p "$T"
	cp "$XPC/rocola_teclas.txt" "$XPC/rocola_config.txt" "$T/"; printf 'rocola_teclas.txt de %s\nrocola_config.txt de %s\nmirado %s\n' "$XPC" "$XPC" "$XPC" > "$T/rocola_de_xp.txt"
	printf 'dir=%s\r\n' "$M" >> "$T/rocola_config.txt"
	importa "$T" "$XPC" "${DISCOS[@]}"
	si "con la copia del lanzador de antes: sale con 0" [ $rc = 0 ]
	si "la configuracion, traducida" contiene "$T/rocola_config.txt" "dir=$D/sdb1/Musica Extra"
	si "sin rutas de Windows" sinwindows "$T/rocola_config.txt"
	si "con la carpeta que apunto el lanzador" contiene "$T/rocola_config.txt" "dir=$M"
	si "la copia del lanzador, en .antes-xp" contiene "$T/rocola_config.txt.antes-xp" 'dir=D:\MUSICA EXTRA'
	si "los botones, iguales: sin .antes-xp" nohay "$T/rocola_teclas.txt.antes-xp"

	# OTRA CARPETA DE XP (la buena es otra): todo otra vez, lo de antes en .antes-xp y la configuracion juntada
	T=$W/destino1; X2=$D/sdb1/Rocola\ Nueva/Release; mkdir -p "$X2"
	grep -v 'Tono Alto' "$XPC/rocola_volumen.txt" | head -2 > "$X2/rocola_volumen.txt"
	sed 's/moneda=77/moneda=78/' "$XPC/rocola_teclas.txt" > "$X2/rocola_teclas.txt"
	printf 'gpu=1\r\ndiagnostico=0\r\nclave_nueva=1\r\ndir=D:\\Musica Extra\r\n' > "$X2/rocola_config.txt"
	printf 'clave_solo_de_rocola_xp=7\r\n' >> "$T/rocola_config.txt"
	cp "$T/rocola_volumen.txt" "$W/volumen-de-antes.txt"; cp "$T/rocola_config.txt" "$W/config-de-antes.txt"; cp "$T/rocola_sonoridad.txt" "$W/sonoridad-de-antes.txt"
	rm -rf "$W/destino7"; cp -a "$T" "$W/destino7"   # (revision) la misma, para el apagon a media re-importacion (abajo)
	echo "$X2" > "$W/run/rocola-datos-xp"
	importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
	si "otra carpeta de XP: sale con 0" [ $rc = 0 ]
	si "y lo dice" contiene "$W/sale.txt" "importado otra vez de $X2"
	si "la marca dice la carpeta nueva" contiene "$T/rocola_importado_xp.txt" "carpeta=$X2"
	si "y de cual venia" contiene "$T/rocola_importado_xp.txt" "antes era de $XPC"
	si "rocola_volumen.txt: el de antes, en .antes-xp" igual "$T/rocola_volumen.txt.antes-xp" "$W/volumen-de-antes.txt"
	si "rocola_volumen.txt: el de la carpeta nueva (dos)" lineas "$T/rocola_volumen.txt" 2
	si "rocola_sonoridad.txt (no esta en la nueva): se queda, sin .antes-xp" bash -c 'cmp -s "$1/rocola_sonoridad.txt" "$2" && [ ! -e "$1/rocola_sonoridad.txt.antes-xp" ]' sh "$T" "$W/sonoridad-de-antes.txt"
	si "rocola_teclas.txt: el de la nueva" contiene "$T/rocola_teclas.txt" "moneda=78"
	si "y el de antes en .antes-xp" contiene "$T/rocola_teclas.txt.antes-xp" "moneda=77"
	si "la configuracion: manda la nueva" contiene "$T/rocola_config.txt" "diagnostico=0"
	si "con lo nuevo" contiene "$T/rocola_config.txt" "clave_nueva=1"
	si "y las claves que solo tenia la de antes" contiene "$T/rocola_config.txt" "clave_solo_de_rocola_xp=7"
	si "sin repetir una clave" bash -c '[ "$(grep -c "^diagnostico=" "$1")" = 1 ]' sh "$T/rocola_config.txt"
	si "las carpetas de musica, sin repetir" bash -c '[ "$(grep -c "^dir=" "$1")" = 1 ]' sh "$T/rocola_config.txt"
	si "la configuracion de antes, en .antes-xp" igual "$T/rocola_config.txt.antes-xp" "$W/config-de-antes.txt"
	importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
	si "y otra vez con la nueva: nada que hacer" bash -c '[ $1 = 0 ] && grep -qF "ya importado" "$2"' sh $rc "$W/sale.txt"
	# (revision) UN APAGON A MEDIA RE-IMPORTACION: con lo de antes ya en .antes-xp y un archivo nuevo puesto, muere al ir a poner el 2.o; la
	# marca sigue diciendo la carpeta de antes. La siguiente acaba: lo mismo que la re-importacion sin cortes, tambien los .antes-xp (no se
	# vuelven a copiar encima con lo ya importado: .importa-xp.en-curso)
	if command -v strace > /dev/null; then
		contenido "$T" > "$W/contenido-reimportado.txt"
		CORTE=2 importa "$W/destino7" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
		si "(apagon a media re-importacion) muere y la marca sigue diciendo la de antes" bash -c '[ $1 != 0 ] && tr -d "\r" < "$3/rocola_importado_xp.txt" | grep -qxF "carpeta=$2"' sh $rc "$XPC" "$W/destino7"
		importa "$W/destino7" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
		contenido "$W/destino7" > "$W/contenido-destino7.txt"
		si "(apagon a media re-importacion) la siguiente acaba: lo mismo que sin el corte (y los .antes-xp con lo de antes)" bash -c '[ $1 = 0 ] && cmp -s "$2" "$3" && tr -d "\r" < "$4/rocola_importado_xp.txt" | grep -qxF "carpeta=$5"' sh $rc "$W/contenido-reimportado.txt" "$W/contenido-destino7.txt" "$W/destino7" "$X2"
	else echo "(sin strace: no se prueba el apagon a media re-importacion)"; fi

	# EL DISCO CAMBIA DE NOMBRE (ayer sda1, hoy sde1): la misma carpeta, no se re-importa
	T=$W/destino3; echo "$XPC" > "$W/run/rocola-datos-xp"
	importa "$T" "$W/run/rocola-datos-xp" "${DISCOS[@]}"
	si "(otro destino, importado de sda1)" [ $rc = 0 ]
	mv "$D/sda1" "$D/sde1"; echo "$D/sde1/Rocola/Release" > "$W/run/rocola-datos-xp"
	importa "$T" "$W/run/rocola-datos-xp" "$D/sdc1" "$D/sde1" "$D/sdb1"
	si "el disco que cambio de nombre: sale con 0" [ $rc = 0 ]
	si "y dice que es la misma carpeta" contiene "$W/sale.txt" "la misma carpeta de XP, ahora en $D/sde1/Rocola/Release"
	si "sin re-importar (nada en .antes-xp)" bash -c '! ls "$1"/*.antes-xp > /dev/null 2>&1' sh "$T"
	si "la marca apunta donde esta ahora" contiene "$T/rocola_importado_xp.txt" "carpeta=$D/sde1/Rocola/Release"
	mv "$D/sde1" "$D/sda1"; echo "$XPC" > "$W/run/rocola-datos-xp"

	# LOS FALLOS (no dejan nada)
	T=$W/destino4
	unshare -m -- sh -c 'd=$1; shift; mount --bind -o ro "$d" "$d" && exec "$@"' sh "$D" env -u ROCOLA_DATOS "${RX[@]}" --importa-xp "$XPC" > "$W/sale.txt" 2> "$W/err.txt"; rc=$?
	si "sin ROCOLA_DATOS: sale con 2 y una linea en la salida de errores" bash -c '[ $1 = 2 ] && [ "$(wc -l < "$2")" = 1 ]' sh $rc "$W/err.txt"
	importa "$T" "$W/no-existe"
	si "sin la carpeta de XP: sale con 3" bash -c '[ $1 = 3 ] && [ "$(wc -l < "$2")" = 1 ] && [ ! -e "$3" ]' sh $rc "$W/err.txt" "$T"
	mkdir -p "$W/vacio"
	importa "$T" "$XPC" "$W/vacio"
	si "sin los discos de la musica: sale con 4" bash -c '[ $1 = 4 ] && [ "$(wc -l < "$2")" = 1 ]' sh $rc "$W/err.txt"
	si "y no deja nada (ni la marca: se intentara otra vez)" bash -c '[ -z "$(ls -A "$1")" ]' sh "$T"
	T=$W/destino5; mkdir -p "$T"; chattr +i "$T" 2> /dev/null && inmutable=1 || inmutable=0   # una carpeta donde no se puede crear nada
	if [ $inmutable = 1 ]; then
		importa "$T" "$XPC" "${DISCOS[@]}"
		chattr -i "$T"
		si "no se puede escribir en el destino: sale con 4, una linea en la salida de errores, y no deja nada" bash -c '[ $1 = 4 ] && [ "$(wc -l < "$3")" = 1 ] && [ -z "$(ls -A "$2")" ]' sh $rc "$T" "$W/err.txt"
	fi
	# (revision) ROCOLA-XP ABIERTA ANTES DE LA PRIMERA IMPORTACION, SIN LA MUSICA (la importacion salio con 4 porque no estaba el disco, y el
	# lanzador abre la rocola igual): deja rocola_volumen.txt vacio, que no debe tapar para siempre la tabla de XP. Una tabla con alguna
	# cancion suya si se queda (no se toca). Se cierra con SIGUSR1, como los reinicios propios de Rocola OS.
	T=$W/destino8; mkdir -p "$T"
	printf 'arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=77\r\nsaltar=83\r\n' > "$T/rocola_teclas.txt"
	SENAL=USR1 corre "$T" 10 'false'
	si "(rocola-xp sin musica antes de importar) deja rocola_volumen.txt vacio" bash -c '[ -e "$1/rocola_volumen.txt" ] && [ ! -s "$1/rocola_volumen.txt" ]' sh "$T"
	printf '1.5|/otra/cancion/de/rocola-xp.mp3\r\n' > "$T/rocola_billar_nivel.txt"   # y una tabla con una cancion suya
	importa "$T" "$XPC" "${DISCOS[@]}"
	si "(rocola-xp sin musica antes de importar) la importacion sale con 0" [ $rc = 0 ]
	si "la tabla vacia no tapa la de XP: rocola_volumen.txt con sus seis, sin .antes-xp" bash -c '[ "$(wc -l < "$1/rocola_volumen.txt")" = 6 ] && [ ! -e "$1/rocola_volumen.txt.antes-xp" ]' sh "$T"
	si "y lo dice la marca" contiene "$T/rocola_importado_xp.txt" "vacias (de rocola-xp, sin ninguna cancion), reemplazadas: rocola_volumen.txt"
	si "la tabla con una cancion de rocola-xp no se toca" bash -c 'grep -qF "/otra/cancion/de/rocola-xp.mp3" "$1/rocola_billar_nivel.txt" && grep -qF "no se tocan: rocola_billar_nivel.txt" "$1/rocola_importado_xp.txt"' sh "$T"
	# LA CARPETA DE DATOS ES LA DE XP («datos=xp»): nada que importar
	importa "$XPC" "$XPC" "${DISCOS[@]}"
	si "con la carpeta de XP como destino: sale con 0 sin tocar nada" bash -c '[ $1 = 0 ] && grep -qF "nada que importar" "$2"' sh $rc "$W/sale.txt"
	foto "$D/sda1/Rocola" > "$W/foto-despues.txt"
	si "LA CARPETA DE XP (y su musica) SIN TOCAR: el mismo contenido y las mismas fechas (y se importo con los discos en solo lectura)" igual "$W/foto-antes.txt" "$W/foto-despues.txt"
	rm -rf "$D/sdb1/Rocola Nueva"
fi

if [[ " $partes " == *" usa "* ]]; then
	echo "=== usa (rocola-xp con lo importado)"
	T=$W/usa
	importa "$T" "$XPC" "${DISCOS[@]}"
	si "importado" [ $rc = 0 ]
	printf 'dir=%s\r\n' "$M" >> "$T/rocola_config.txt"   # lo que apunta el lanzador (la musica que encontro rocola-discos)
	cp "$T/rocola_sonoridad.txt" "$W/usa-sonoridad.txt"; cp "$T/rocola_fuentes.txt" "$W/usa-fuentes.txt"; cp "$T/rocola_volumen.txt" "$W/usa-volumen.txt"
	corre "$T" 40 'grep -q "^.*repaso:" "$T/rocola_diagnostico.txt" 2> /dev/null && [ -s "$W/usa-estado.txt" ] && grep -q "02 Nueva" "$T/rocola_sonoridad.txt" 2> /dev/null' ROCOLA_PULSA="7:77~300" ROCOLA_ESTADO_EN="9:$W/usa-estado.txt"
	si "lee lo importado (las seis canciones con volumen)" contiene "$T/rocola_diagnostico.txt" "(6 canciones con volumen)"
	si "el repaso solo tiene la cancion nueva" contiene "$T/rocola_diagnostico.txt" "repaso: 1 archivos"
	si "la mide (la nueva entra en las tablas)" contiene "$T/rocola_sonoridad.txt" "02 Nueva.mp3"
	si "las importadas no se vuelven a medir (sus lineas, igual)" bash -c '[ "$(grep -v "02 Nueva" "$1" | grep -c .)" = "$(grep -c . "$2")" ] && [ "$(grep -v "02 Nueva" "$3" | grep -c .)" = "$(grep -c . "$4")" ]' sh "$T/rocola_sonoridad.txt" "$W/usa-sonoridad.txt" "$T/rocola_volumen.txt" "$W/usa-volumen.txt"
	si "los botones importados valen (la moneda de rocola_teclas.txt, la 77, da creditos)" bash -c 'grep -qE "^creditos=[1-9]" "$1"' sh "$W/usa-estado.txt"
	si "sin abrir el asistente de los botones" bash -c 'grep -q "^asistente_botones=0" "$1"' sh "$W/usa-estado.txt"
	si "rocola_error.txt sin «Otro archivo» ni «Guardado cortado»" bash -c '! grep -qE "Otro archivo|Guardado cortado" "$1" 2> /dev/null' sh "$T/rocola_error.txt"
	# Al sonar una importada se mira su sello: con el de Linux vale tal cual (rocola_fuentes.txt no cambia); con el de Windows (la creada de
	# XP) se adopta y se vuelve a medir (se apunta otra vez con el sello de hoy): lo que el importador evita.
	for caso in linux windows; do
		T=$W/usa2; rm -rf "$T"; importa "$T" "$XPC" "${DISCOS[@]}"; printf 'dir=%s\r\n' "$M" >> "$T/rocola_config.txt"
		if [ $caso = windows ]; then
			python3 - "$T/rocola_fuentes.txt" "$XPC/rocola_fuentes.txt" <<'PY'
import sys
t, xp = sys.argv[1], sys.argv[2]
cre = {}
for l in open(xp, encoding="utf-8").read().splitlines()[1:]:
    p = l.split("|", 5)
    if len(p) == 6: cre[p[5].rsplit("\\", 1)[-1].lower()] = p[1]
sale = []
for l in open(t, encoding="utf-8").read().splitlines():
    p = l.split("|", 5)
    if len(p) == 6 and p[5].rsplit("/", 1)[-1].lower() in cre: p[1] = cre[p[5].rsplit("/", 1)[-1].lower()]
    sale.append("|".join(p))
open(t, "w", encoding="utf-8", newline="").write("\r\n".join(sale) + "\r\n")
PY
		fi
		cp "$T/rocola_fuentes.txt" "$W/usa2-fuentes.txt"
		corre "$T" 40 'grep -q "cancion: abierta" "$T/rocola_diagnostico.txt" 2> /dev/null && sleep 3' ROCOLA_PULSA="7:77~300,10:13,11.5:13"
		# suena la primera del primer disco («01 Pulso», importada): su cuenta en rocola_stats.txt pasa de 2 a 3
		si "($caso) suena «01 Pulso», una importada (su cuenta pasa de 2 a 3)" grep -q "^3|.*01 Pulso.mp3" "$T/rocola_stats.txt"
		antes=$(grep "01 Pulso" "$W/usa2-fuentes.txt"); despues=$(grep "01 Pulso" "$T/rocola_fuentes.txt" | tail -1)
		if [ $caso = linux ]; then si "con el sello que puso el importador, su medida vale tal cual (rocola_fuentes.txt no la vuelve a apuntar)" [ "$antes" = "$despues" ]
		else si "con el sello de Windows, rocola-xp la adopta y le pone el sello de hoy (y la volveria a medir): lo que evita el importador" bash -c '[ "$1" != "$2" ] && [ "${1#*|*|}" = "${2#*|*|}" ]' sh "$antes" "$despues"; fi
	done
fi

if [[ " $partes " == *" material "* ]]; then
	echo "=== material (lo grande: el material de la IA y las miniaturas)"
	R="$W/mat/musica/Ruido Blanco/Disco Ruido"
	for i in 1 2 3; do gen "$R/0$i Ruido $i.wav" "anoisesrc=color=white:seed=$i:amplitude=0.3" 12 "Ruido $i" "-c:a pcm_s16le"; done
	listo() { [ "$(grep -c "|/" "$1/rocola_sonoridad.txt" 2> /dev/null)" = 3 ]; }
	for caso in sin con poco nopuede; do
		T=$W/mat/$caso; I=$W/mat/ia-$caso; mkdir -p "$T" "$I"
		printf 'arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=77\r\nsaltar=83\r\n' > "$T/rocola_teclas.txt"
		printf 'diagnostico=1\r\ndir=%s\r\n' "$W/mat/musica" > "$T/rocola_config.txt"
		case $caso in
			sin) corre "$T" 60 'listo "$T"' ;;
			con) corre "$T" 60 'listo "$T"' ROCOLA_IA_DATOS="$I" ;;
			poco) # un sitio con 200 MB libres (menos de los 256 que pide la cosecha)
				( unshare -m -- bash -c 'mount -t tmpfs -o size=200m tmpfs "$1" || exit 9; shift; T=$1; RXS=$2; shift 2; ( cd "$T" && exec env ROCOLA_DATOS="$T" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_IA_DATOS="$0" $RXS ) > "$T.salida.txt" 2>&1 & p=$!; for i in $(seq 1 120); do [ "$(grep -c "|/" "$T/rocola_sonoridad.txt" 2> /dev/null)" = 3 ] && break; sleep 0.5; done; ls -la "$0" > "$T.ia.txt"; kill -TERM $p; wait $p' "$I" "$I" "$T" "${RX[*]}" ) ;;
			nopuede) # ROCOLA_IA_DATOS en un sitio de solo lectura: a la carpeta de datos
				( unshare -m -- bash -c 'mount --bind -o ro "$0" "$0" || exit 9; T=$1; RXS=$2; ( cd "$T" && exec env ROCOLA_DATOS="$T" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_IA_DATOS="$0" $RXS ) > "$T.salida.txt" 2>&1 & p=$!; for i in $(seq 1 120); do [ "$(grep -c "|/" "$T/rocola_sonoridad.txt" 2> /dev/null)" = 3 ] && break; sleep 0.5; done; kill -TERM $p; wait $p' "$I" "$T" "${RX[*]}" ) ;;
		esac
		si "($caso) el repaso midio las tres" listo "$T"
		case $caso in
			sin) si "(sin ROCOLA_IA_DATOS) el material en la carpeta de datos, como XP" bash -c '[ "$(stat -c %s "$1/rocola_ia_datos.bin" 2> /dev/null || echo 0)" -gt 16 ] && [ -s "$1/rocola_ia_datos.idx" ]' sh "$T"
			     si "(sin ROCOLA_IA_DATOS) las miniaturas en la carpeta de datos" hay "$T/rocola_miniaturas"
			     si "(sin ROCOLA_IA_DATOS) el respaldo en la carpeta de datos" hay "$T/respaldo" ;;
			con) si "(con ROCOLA_IA_DATOS) el material alli" bash -c '[ "$(stat -c %s "$1/rocola_ia_datos.bin" 2> /dev/null || echo 0)" -gt 16 ] && [ -s "$1/rocola_ia_datos.idx" ] && [ -s "$1/rocola_ia_datos.generos.tsv" ]' sh "$I"
			     si "(con ROCOLA_IA_DATOS) y no en la carpeta de datos" bash -c '! ls "$1"/rocola_ia_datos* > /dev/null 2>&1' sh "$T"
			     si "(con ROCOLA_IA_DATOS) de que canciones se saco, en la carpeta de datos (rocola_ia_hechas.txt, como XP)" bash -c '[ -s "$1/rocola_ia_hechas.txt" ] && [ ! -e "$2/rocola_ia_hechas.txt" ]' sh "$T" "$I"
			     si "(con ROCOLA_IA_DATOS) las miniaturas alli" bash -c '[ -d "$1/rocola_miniaturas" ] && [ ! -e "$2/rocola_miniaturas" ]' sh "$I" "$T"
			     si "(con ROCOLA_IA_DATOS) el respaldo de lo aprendido alli (con sus tablas)" bash -c 'ls "$1"/respaldo/*/rocola_config.txt > /dev/null 2>&1 && [ ! -e "$2/respaldo" ]' sh "$I" "$T" ;;
			poco) si "(menos de 256 MB libres) la cosecha no junta nada" bash -c '! grep -q rocola_ia_datos "$1" && [ ! -e "$2/rocola_ia_hechas.txt" ] && ! ls "$2"/rocola_ia_datos* > /dev/null 2>&1' sh "$T.ia.txt" "$T"
			      si "(menos de 256 MB libres) lo demas si (las miniaturas y el respaldo alli)" bash -c 'grep -q rocola_miniaturas "$1" && grep -q respaldo "$1"' sh "$T.ia.txt" ;;
			nopuede) si "(ROCOLA_IA_DATOS de solo lectura) el material, las miniaturas y el respaldo, a la carpeta de datos" bash -c '[ "$(stat -c %s "$1/rocola_ia_datos.bin" 2> /dev/null || echo 0)" -gt 16 ] && [ -d "$1/rocola_miniaturas" ] && [ -d "$1/respaldo" ]' sh "$T"
			         si "(ROCOLA_IA_DATOS de solo lectura) y lo dice en la salida de errores" contiene "$T.salida.txt" "no se puede usar ROCOLA_IA_DATOS=$I" ;;
		esac
	done
fi

si "ninguna rocola de la prueba quedo abierta" [ -z "$(quedan)" ]
echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
