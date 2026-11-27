#!/bin/bash
# LA PRUEBA DE LA CADENA DE SONIDO NUEVA (la A y el marco comun; CADENA_NUEVA.md, CADENA_NUEVA_A.md; RocolaOs/RocolaOs.CadenaNueva*.cs y
# RocolaOs.Medida*.cs). Senales INVENTADAS (senales.py: «musica» sintetica, ruido rosa, senos) codificadas aqui con ffmpeg (LAME); nada se oyo:
# son cuentas. La mayoria con la orden de prueba del programa (--prueba-cadena-nueva: RocolaOs.CadenaNuevaPrueba.cs); las escenas, con la
# rocola de verdad fuera de pantalla y la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c), y la de Spotify con el Soloist falso de
# spotify-xp. Lo que sono se mide con el ebur128 de ffmpeg (otra implementacion de BS.1770) y analiza.py.
#   bash prueba.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   partes (de fabrica todas): decodifica sinhuecos medidor mide ganancia constante disco arreglos costo escena billar hoy sinros spotify
#   W (/root/rocola-trabajo/fase11/cadena-nueva/prueba; se rehace), DATOS0 (/root/rocola-trabajo/port/ventanas/datos0, se copia),
#   FALSO (el Soloist falso compilado de spotify-xp, /root/rocola-trabajo/fase11/spotify-xp/falso; se compila si falta)
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SP="$G/../spotify-xp"
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-decodifica sinhuecos medidor mide ganancia constante disco arreglos costo escena billar hoy sinros spotify}"
W=${W:-/root/rocola-trabajo/fase11/cadena-nueva/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
FALSO=${FALSO:-/root/rocola-trabajo/fase11/spotify-xp/falso}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
py() { python3 -c "import sys; sys.exit(0 if ($1) else 1)"; }
num() { sed -n "s/.* $2=\([^ ]*\).*/\1/p" "$1" | head -1; }   # num ARCHIVO CLAVE (la primera linea que la tenga)
lin() { grep "^$2 $3 " "$1" | head -1; }                          # lin ARCHIVO PARTE NOMBRE
de() { echo "$1" | sed -n "s/.* $2=\([^ ]*\).*/\1/p"; }            # de LINEA CLAVE
cn() { "${RX[@]}" --prueba-cadena-nueva "$@" 2>&1 | grep -v '^Pantalla: '; }
# el ebur128 de ffmpeg de un WAV (o de un trozo de sonado.raw): «I TP»
ebur() { ffmpeg -nostats -hide_banner "${@:2}" -i "$1" -af ebur128=peak=true -f null - 2>&1 | awk '/Integrated loudness:/{f=1} f && /I:/{i=$2} /True peak:/{g=1} g && /Peak:/{p=$2} END{print i, p}'; }
export ROCOLA_PANTALLA=fuera:64x64

# ---------- las senales y los MP3 ----------
M=$W/mp3; mkdir -p "$M"
python3 "$G/senales.py" "$M" > /dev/null || exit 1
enc() { ffmpeg -hide_banner -loglevel error -y "$@" || exit 1; }
enc -i "$M/fuerte.wav" -c:a libmp3lame -b:a 192k "$M/fuerte.mp3"
enc -i "$M/fuerte.wav" -c:a libmp3lame -q:a 2 "$M/fuerte_vbr.mp3"
enc -i "$M/medio.wav" -c:a libmp3lame -b:a 192k "$M/medio.mp3"
enc -i "$M/medio.wav" -ar 22050 "$M/medio22.wav"; enc -i "$M/medio22.wav" -c:a libmp3lame -b:a 64k "$M/mpeg2.mp3"
enc -i "$M/medio.wav" -ar 11025 "$M/medio11.wav"; enc -i "$M/medio11.wav" -c:a libmp3lame -b:a 32k "$M/mpeg25.mp3"
python3 -I "$G/marcas.py" "$M/fuerte.mp3" "$M/itunes.mp3" "$M/vbri.mp3" > "$W/marcas.txt" || exit 1
python3 -I "$G/marcas.py" "$M/mpeg2.mp3" "$M/itunes2.mp3" "$M/vbri2.mp3" >> "$W/marcas.txt" || exit 1
enc -i "$M/disco.wav" -t 15.3 -c:a libmp3lame -b:a 192k "$M/disco1.mp3"; enc -ss 15.3 -i "$M/disco.wav" -c:a libmp3lame -b:a 192k "$M/disco2.mp3"
enc -i "$M/rango.wav" -c:a libmp3lame -b:a 192k "$M/rango.mp3"
# ruido rosa MONO: el MISMO trozo de 2 s repetido a 0, +10 y -10 dB (la escena de la ganancia constante: con una cadena que no depende del
# nivel, cada escalon sale con la misma ganancia aunque la cadena no sea plana, como la del billar)
python3 - "$M/ruido.wav" <<'EOP'
import sys, wave, numpy as np
fs=44100; m=2*fs; rng=np.random.default_rng(5)
w=rng.standard_normal(m); f=np.fft.rfftfreq(m,1/fs); f[0]=1; X=np.fft.rfft(w)/np.sqrt(f); X[f<30]=0; w=np.fft.irfft(X,m); w=w/np.max(np.abs(w))*0.12
y=np.concatenate([w*(1.0 if k%3==0 else (3.162 if k%3==1 else 0.3162)) for k in range(7)])
y=np.clip(y,-1,1); s=np.round(y*32767).astype('<i2')
with wave.open(sys.argv[1],'wb') as wf: wf.setnchannels(1); wf.setsampwidth(2); wf.setframerate(fs); wf.writeframes(s.tobytes())
EOP
enc -i "$M/ruido.wav" -c:a libmp3lame -b:a 192k "$M/ruido.mp3"
enc -i "$M/ruido.mp3" -f f32le -ac 2 "$M/ruido.dec.f32"
enc -i "$M/disco.wav" -t 12 -c:a libmp3lame -b:a 192k "$M/corto.mp3"; enc -ss 12 -t 12 -i "$M/disco.wav" -c:a libmp3lame -b:a 192k "$M/corto1b.mp3"
enc -i "$M/corto2.wav" -c:a libmp3lame -b:a 192k "$M/corto2.mp3"

# ---------------------------------------------------------------- decodifica
if [[ " $partes " == *" decodifica "* ]]; then
  echo "== decodifica (el de hoy, 16 bits, frente al nuevo, coma flotante)"
  cn decodifica "$M/fuerte.mp3" "$M/fuerte_vbr.mp3" "$M/medio.mp3" > "$W/decodifica.txt"; sed 's/^/   /' "$W/decodifica.txt"
  for f in fuerte fuerte_vbr; do l=$(lin "$W/decodifica.txt" DECODIFICA $f.mp3)
    si "decodifica $f: en coma flotante pasa de 0 dBFS ($(de "$l" pico_f_db) dBFS, $(de "$l" sobre_0dbfs_f) muestras) y nada se recorta; hoy, al tope ($(de "$l" al_tope_16))" py "$(de "$l" pico_f_db) > 1.0 and $(de "$l" sobre_0dbfs_f) > 100 and $(de "$l" al_tope_16) > 100 and $(de "$l" pico_16_db) <= 0.0"
  done
  l=$(lin "$W/decodifica.txt" DECODIFICA medio.mp3)
  si "decodifica medio (sin pasar de 0 dBFS): las mismas muestras y solo el redondeo de 16 bits de diferencia ($(de "$l" dif_rel_db) dB)" py "$(de "$l" muestras_16) == $(de "$l" muestras_f) and $(de "$l" dif_rel_db) < -75"
  si "decodifica: con libmpg123 en coma flotante" bash -c "grep -q 'motor=libmpg123_(coma_flotante)' '$W/decodifica.txt'"
fi

# ---------------------------------------------------------------- sinhuecos
if [[ " $partes " == *" sinhuecos "* ]]; then
  echo "== sinhuecos (las tres marcas, MPEG-1, 2 y 2.5, frente al WAV original)"
  sed 's/^/   /' "$W/marcas.txt"
  { cn sinhuecos "$M/fuerte.wav" "$M/fuerte.mp3" "$M/fuerte_vbr.mp3" "$M/itunes.mp3" "$M/vbri.mp3"; cn sinhuecos "$M/medio22.wav" "$M/mpeg2.mp3" "$M/itunes2.mp3" "$M/vbri2.mp3"; cn sinhuecos "$M/medio11.wav" "$M/mpeg25.mp3"; } > "$W/sinhuecos.txt"
  sed 's/^/   /' "$W/sinhuecos.txt"
  for f in fuerte:LAME:1 fuerte_vbr:LAME:1 itunes:iTunSMPB:1 mpeg2:LAME:2 itunes2:iTunSMPB:2 mpeg25:LAME:25; do n=${f%%:*}; r=${f#*:}; mk=${r%%:*}; v=${r#*:}; l=$(lin "$W/sinhuecos.txt" SINHUECOS $n.mp3)
    si "sinhuecos $n ($mk, MPEG-$v): empieza en su muestra y acaba en la suya (desfase $(de "$l" desfase), sobra $(de "$l" sobra))" py "'$(de "$l" marca)' == '$mk' and '$(de "$l" version)' == '$v' and $(de "$l" desfase) == 0 and $(de "$l" sobra) == 0"
  done
  for n in vbri vbri2; do l=$(lin "$W/sinhuecos.txt" SINHUECOS $n.mp3)
    si "sinhuecos $n (VBRI, sin relleno): empieza en su muestra; el final, el relleno de menos el del descompresor ($(de "$l" sobra))" py "'$(de "$l" marca)' == 'VBRI' and $(de "$l" desfase) == 0 and 0 <= $(de "$l" sobra) < 1152"
  done
fi

# ---------------------------------------------------------------- medidor
if [[ " $partes " == *" medidor "* ]]; then
  echo "== medidor (senales conocidas; el ruido rosa contra MedidorBs1770 de XP)"
  cn medidor > "$W/medidor.txt"; sed 's/^/   /' "$W/medidor.txt"
  l=$(lin "$W/medidor.txt" MEDIDOR seno997); si "medidor: un seno de 997 Hz a -20 dBFS en los dos canales da -20,00 LUFS ($(de "$l" lufs))" py "abs($(de "$l" lufs) + 20.0) <= 0.02"
  l=$(lin "$W/medidor.txt" MEDIDOR fs4); si "medidor: el seno en fs/4 a 45 grados: pico de muestra -9,03 y pico real -6,02 ($(de "$l" pico) y $(de "$l" tp))" py "abs($(de "$l" pico) + 9.03) <= 0.02 and abs($(de "$l" tp) + 6.02) <= 0.05"
  l=$(lin "$W/medidor.txt" MEDIDOR 11k); si "medidor: un seno de 11 kHz a -1 dBFS: pico real -1,00 a 0,05 dB ($(de "$l" tp))" py "abs($(de "$l" tp) + 1.0) <= 0.05"
  for t in 44100 48000; do l=$(lin "$W/medidor.txt" MEDIDOR rosa$t)
    si "medidor: ruido rosa a $t Hz con escalones: la integrada y el LRA de XP ($(de "$l" lufs)/$(de "$l" xp) LUFS, $(de "$l" lra)/$(de "$l" lra_xp) LU)" py "abs($(de "$l" lufs) - $(de "$l" xp)) <= 0.01 and abs($(de "$l" lra) - $(de "$l" lra_xp)) <= 0.15 and $(de "$l" tp) >= $(de "$l" pico)"
  done
  si "medidor: el silencio sale mudo" bash -c "grep -q 'silencio muda=1' '$W/medidor.txt'"
fi

# ---------------------------------------------------------------- mide y ganancia (con el ebur128 de ffmpeg)
if [[ " $partes " == *" mide "* || " $partes " == *" ganancia "* ]]; then
  mkdir -p "$W/sale"
  cn mide "$M/fuerte.mp3" "$M/medio.mp3" "$M/rango.mp3" "$M/mpeg2.mp3" > "$W/mide.txt"
  ROCOLA_CN_SALIDA="$W/sale" cn ganancia "$M/fuerte.mp3" "$M/medio.mp3" "$M/rango.mp3" "$M/mpeg2.mp3" > "$W/ganancia.txt"
fi
if [[ " $partes " == *" mide "* ]]; then
  echo "== mide (la cancion entera en coma flotante por la cadena; lo que sale, tambien con el ebur128 de ffmpeg)"
  sed 's/^/   /' "$W/mide.txt"
  for f in fuerte medio rango mpeg2; do l=$(lin "$W/mide.txt" MIDE $f.mp3)
    si "mide $f: cancion entera, renglon de 19 campos con 30 tercios ($(de "$l" veces_tiempo_real) veces el tiempo real)" py "$(de "$l" segundos) > 10 and '$(de "$l" tercios)' == '30' and '$(de "$l" renglon_campos)' == '19'"
    g=$(lin "$W/ganancia.txt" GANANCIA $f.mp3); e=$(ebur "$W/sale/$f.sale.wav")
    si "mide $f: lo que sale, por el medidor y por ffmpeg: $(de "$g" sale_lufs)/${e% *} LUFS y $(de "$g" sale_tp)/${e#* } dBTP" py "abs($(de "$g" sale_lufs) - (${e% *})) <= 0.15 and abs($(de "$g" sale_tp) - (${e#* })) <= 0.35"
  done
fi
if [[ " $partes " == *" ganancia "* ]]; then
  echo "== ganancia (g = min(T - L, C - TP), T = -18 LUFS, C = -1 dBTP)"
  sed 's/^/   /' "$W/ganancia.txt"
  a=$(lin "$W/ganancia.txt" GANANCIA fuerte.mp3); b=$(lin "$W/ganancia.txt" GANANCIA medio.mp3); c=$(lin "$W/ganancia.txt" GANANCIA rango.mp3); d=$(lin "$W/ganancia.txt" GANANCIA mpeg2.mp3)
  si "ganancia: la fuerte ($(de "$a" lufs) LUFS) y la media ($(de "$b" lufs)) salen a -18 a 0,5 LU entre ellas ($(de "$a" sale_lufs) y $(de "$b" sale_lufs))" py "abs($(de "$a" sale_lufs) - $(de "$b" sale_lufs)) <= 0.5 and abs($(de "$a" sale_lufs) + 18) <= 0.5 and abs($(de "$b" sale_lufs) + 18) <= 0.5"
  si "ganancia: y la de MPEG-2 tambien ($(de "$d" sale_lufs))" py "abs($(de "$d" sale_lufs) + 18) <= 0.5"
  si "ganancia: ninguna pasa el techo de -1 dBTP ($(de "$a" sale_tp), $(de "$b" sale_tp), $(de "$c" sale_tp), $(de "$d" sale_tp))" py "max($(de "$a" sale_tp), $(de "$b" sale_tp), $(de "$c" sale_tp), $(de "$d" sale_tp)) <= -0.9"
  si "ganancia: la de mucho rango no cabe y queda mas baja ($(de "$c" sale_lufs) LUFS) con el pico real en el techo ($(de "$c" sale_tp))" py "'$(de "$c" cabe)' == '0' and $(de "$c" sale_lufs) < -18.5 and abs($(de "$c" sale_tp) + 1.0) <= 0.1"
fi

# ---------------------------------------------------------------- constante
if [[ " $partes " == *" constante "* ]]; then
  echo "== constante (ruido rosa mono con escalones de +-10 dB: la ganancia por bloque de 10 ms)"
  cn constante > "$W/constante.txt"; sed 's/^/   /' "$W/constante.txt"
  l=$(lin "$W/constante.txt" CONSTANTE nueva); si "constante: la cadena nueva, la misma ganancia en todos los bloques (vaiven $(de "$l" vaiven_db) dB)" py "$(de "$l" vaiven_db) <= 0.05 and $(de "$l" bloques) > 2000"
  l=$(lin "$W/constante.txt" CONSTANTE hoy); si "constante: la de hoy (su limitador) si la mueve (vaiven $(de "$l" vaiven_db) dB)" py "$(de "$l" vaiven_db) >= 1.0"
fi

# ---------------------------------------------------------------- disco
if [[ " $partes " == *" disco "* ]]; then
  echo "== disco (dos pistas seguidas de un disco continuo, frente a las dos juntas por una sola cadena)"
  cn disco "$M/disco1.mp3" "$M/disco2.mp3" "$M/disco.wav" > "$W/disco.txt"; sed 's/^/   /' "$W/disco.txt"
  l=$(lin "$W/disco.txt" DISCO "union=$(num "$W/disco.txt" union)")
  si "disco: sin hueco (las dos pistas miden lo que el WAV entero: $(num "$W/disco.txt" largo)/$(num "$W/disco.txt" largo_wav))" py "$(num "$W/disco.txt" largo) == $(num "$W/disco.txt" largo_wav)"
  si "disco: fuera de la union, lo mismo que una sola cadena ($(num "$W/disco.txt" err_resto_db) dB)" py "$(num "$W/disco.txt" err_resto_db) <= -150"
  si "disco: en la union solo el empalme del grave del lado, sin escalon (el mayor, $(num "$W/disco.txt" err_union_max_db) dB del nivel)" py "$(num "$W/disco.txt" err_union_max_db) <= -20"
fi

# ---------------------------------------------------------------- arreglos
if [[ " $partes " == *" arreglos "* ]]; then
  echo "== arreglos (canal invertido, canal mudo, continua: se detectan y se arreglan)"
  cn arreglos "$W/arreglos" > "$W/arreglos.txt"; sed 's/^/   /' "$W/arreglos.txt"
  l=$(lin "$W/arreglos.txt" ARREGLOS invertido); si "arreglos: el canal invertido se ve y sale volteado (correlacion del grave $(de "$l" sale_correlacion))" py "'$(de "$l" invertido)' == '1' and $(de "$l" sale_correlacion) > 0.9"
  l=$(lin "$W/arreglos.txt" ARREGLOS mudo); si "arreglos: el canal mudo se ve y se le copia el otro ($(de "$l" sale_izq_db)/$(de "$l" sale_der_db) dB)" py "'$(de "$l" mudo)' == '0' and abs($(de "$l" sale_izq_db) - $(de "$l" sale_der_db)) < 0.1"
  l=$(lin "$W/arreglos.txt" ARREGLOS continua); si "arreglos: la continua se ve ($(de "$l" continuaL)/$(de "$l" continuaR)) y se quita ($(de "$l" sale_continuaL)/$(de "$l" sale_continuaR))" py "abs($(de "$l" continuaL) - 0.02) < 0.003 and abs($(de "$l" continuaR) + 0.01) < 0.003 and abs($(de "$l" sale_continuaL)) < 0.0005 and abs($(de "$l" sale_continuaR)) < 0.0005"
  l=$(lin "$W/arreglos.txt" ARREGLOS sano); si "arreglos: la sana, ninguno" py "'$(de "$l" mudo)' == '-1' and '$(de "$l" invertido)' == '0' and $(de "$l" continuaL) == 0 and $(de "$l" continuaR) == 0"
fi

# ---------------------------------------------------------------- costo
if [[ " $partes " == *" costo "* ]]; then
  echo "== costo (tiempo de procesador del hilo por segundo de musica, la mejor de 5 vueltas)"
  cn costo "$M/medio.mp3" > "$W/costo.txt"; cn costo "$M/fuerte_vbr.mp3" >> "$W/costo.txt"; sed 's/^/   /' "$W/costo.txt"
  for f in medio fuerte_vbr; do l=$(lin "$W/costo.txt" COSTO $f.mp3)
    si "costo $f: la cadena nueva cuesta menos que la de hoy ($(de "$l" nueva_ms_por_s) frente a $(de "$l" hoy_ms_por_s) ms por segundo de musica, decodificar incluido)" py "$(de "$l" nueva_ms_por_s) < $(de "$l" hoy_ms_por_s)"
  done
fi

# ---------------------------------------------------------------- las escenas con la rocola de verdad
AL=$W/alsa; mkdir -p "$AL"
gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
datos() {   # un disco con dos pistas seguidas (la media partida en dos, 12 s cada una), otro con la fuerte (12 s) y otro con el ruido de los
            # escalones (14 s); sin el modo noche (el reloj)
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d/musica/Grupo Prueba/Disco Prueba" "$d/musica/Grupo Fuerte/Disco Fuerte" "$d/musica/Otro Grupo/Ruido"
  cp "$DATOS0/rocola_teclas.txt" "$d/"; printf 'noche=0\r\n' > "$d/rocola_config.txt"
  cp "$M/corto.mp3" "$d/musica/Grupo Prueba/Disco Prueba/01 media.mp3"; cp "$M/corto1b.mp3" "$d/musica/Grupo Prueba/Disco Prueba/02 media.mp3"
  cp "$M/corto2.mp3" "$d/musica/Grupo Fuerte/Disco Fuerte/01 fuerte.mp3"
  cp "$M/ruido.mp3" "$d/musica/Otro Grupo/Ruido/01 ruido.mp3"
  echo "$d"
}
# escena NOMBRE DATOS SEGUNDOS [VAR=valor...]: cuatro monedas, a los 16 s las cuatro canciones (el doble calidad-prueba), la foto de vivo.txt a
# los 50, Ctrl+F4 a los 68 s con la pestana Cadena (Tab x4, Intro) y su captura a los 71,5 s, y a los SEGUNDOS el cierre (SIGTERM)
escena() {
  local n="$1" d="$2" seg="$3"; shift 3
  local o="$W/$n"; mkdir -p "$o/run/rocola-xp/spotify"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
      ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="7:67,7.5:67,8:67,8.5:67,68:17+115,69:9,69.2:9,69.4:9,69.6:9,70.2:13,74:27" \
      ROCOLA_CAPTURA="71.5:$o/cadena.png" ROCOLA_CAPTURA_SALE=0 ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 \
      "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  sleep 16; printf 'pon Disco Prueba/01 media.mp3\npon Disco Prueba/02 media.mp3\npon 01 fuerte.mp3\npon 01 ruido.mp3\n' >> "$o/run/rocola-xp/spotify/prueba-xp"
  sleep 34; cp "$o/run/rocola-os-prueba-vivo.txt" "$o/vivo.txt" 2>/dev/null
  sleep $((seg - 50)); kill -TERM $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
}
trozos() {   # trozos ESCENA: el ebur128 de ffmpeg de lo que sono (las dos del disco juntas y cada una, la fuerte) y la ganancia por bloque del ruido
  local o="$W/$1" i; i=$(python3 "$G/analiza.py" inicio "$o/sonado.raw" | sed -n 's/.* segundo=\([^ ]*\).*/\1/p')
  t() { python3 -c "print('%.4f' % ($i + $1))"; }
  echo "inicio $i" > "$o/trozos.txt"
  echo "disco $(ebur "$o/sonado.raw" -f s16le -ar 44100 -ac 2 -ss "$(t 0.5)" -t 23)" >> "$o/trozos.txt"
  echo "media1 $(ebur "$o/sonado.raw" -f s16le -ar 44100 -ac 2 -ss "$(t 0.5)" -t 11)" >> "$o/trozos.txt"
  echo "media2 $(ebur "$o/sonado.raw" -f s16le -ar 44100 -ac 2 -ss "$(t 12.5)" -t 11)" >> "$o/trozos.txt"
  echo "fuerte $(ebur "$o/sonado.raw" -f s16le -ar 44100 -ac 2 -ss "$(t 24.5)" -t 11)" >> "$o/trozos.txt"
  python3 "$G/analiza.py" constante "$o/sonado.raw" "$M/ruido.dec.f32" 0.5 13 "$(t 36)" > "$o/constante.txt" 2>&1
  sed 's/^/   /' "$o/trozos.txt" "$o/constante.txt"
}
val() { awk -v k="$2" -v c="$3" '$1==k {print $c}' "$W/$1/trozos.txt"; }
if [[ " $partes " == *" escena "* ]]; then
  echo "== escena (la rocola de verdad con la cadena nueva: medida antes de sonar, a -18 LUFS, ganancia constante, la hoja Cadena)"
  d=$(datos nueva); escena nueva "$d" 78 ROCOLA_OS=cadena_nueva,medida_nueva,calidad-prueba,prueba-andamio
  o=$W/nueva; b="$d/rocola_os_bitacora.txt"
  grep -F '[cadena_nueva]' "$b" 2>/dev/null | sed 's/^/   bitacora: /' | cut -c1-260; grep -F '[medida_nueva]' "$b" 2>/dev/null | sed 's/^/   bitacora: /' | cut -c1-200
  grep -E '^cadena_nueva|^medida_nueva' "$o/vivo.txt" 2>/dev/null | sed 's/^/   vivo: /'
  trozos nueva
  si "escena nueva: sale con 0" test "$(cat $o/salio)" = 0
  si "escena nueva: el repaso de fondo midio las cuatro (rocola_medida_nueva.txt: cabecera y cuatro renglones de la A)" bash -c "[ \$(grep -c '^A	' '$d/rocola_medida_nueva.txt') = 4 ] && head -1 '$d/rocola_medida_nueva.txt' | grep -qx 'ROCOLA-MEDIDA-NUEVA v1'"
  si "escena nueva: la tabla solo trae la ruta y numeros (19 campos)" bash -c "awk -F'\t' 'NR>1 && NF != 19 {bad=1} END {exit bad}' '$d/rocola_medida_nueva.txt'"
  si "escena nueva: las cuatro sonaron por la cadena A con su medida (la bitacora), las dos del disco con una sola ganancia, la del disco" bash -c "[ \$(grep -F '[cadena_nueva]' '$b' | grep -c '] A .*: ganancia ') = 4 ] && ! grep -F '[cadena_nueva]' '$b' | grep -q 'sin medir' && [ \$(grep -F '[cadena_nueva]' '$b' | grep -c 'media.mp3: ganancia .* (el disco (2 de 2 pistas medidas)') = 2 ] && [ \$(grep -F '[cadena_nueva]' '$b' | grep -c 'medida ') = 2 ]"
  si "escena nueva: abiertas en coma flotante y sin huecos con LAME" bash -c "grep -F '[cadena_nueva]' '$b' | grep -q 'libmpg123 (coma flotante), sin huecos: LAME'"
  si "escena nueva: vivo.txt dice que suena la nueva" bash -c "grep -q '^cadena_nueva=suena armadas=[1-9]' '$o/vivo.txt'"
  si "escena nueva: el disco entero y la fuerte sonaron a -18 LUFS a 0,5 LU ($(val nueva disco 2) y $(val nueva fuerte 2), ebur128 de ffmpeg)" py "abs($(val nueva disco 2) + 18) <= 0.5 and abs($(val nueva fuerte 2) + 18) <= 0.5"
  si "escena nueva: sin pasar el techo de -1 dBTP ($(val nueva media1 3), $(val nueva media2 3) y $(val nueva fuerte 3))" py "max($(val nueva media1 3), $(val nueva media2 3), $(val nueva fuerte 3)) <= -0.7"
  si "escena nueva: el ruido de los escalones, ganancia constante por bloque de 10 ms ($(num "$o/constante.txt" vaiven_db) dB de vaiven; escalones $(num "$o/constante.txt" escalones_db))" py "$(num "$o/constante.txt" vaiven_db) <= 0.05 and $(num "$o/constante.txt" bloques) > 1000 and $(num "$o/constante.txt" escalones_db) <= 0.05"
  si "escena nueva: la hoja Cadena de Ctrl+F4 se arma (captura) y ningun fallo de la cadena nueva en rocola_error.txt" bash -c "[ -s '$o/cadena.png' ] && ! grep -q 'Rocola OS: cadena_nueva\|Rocola OS: medida_nueva\|la hojita Cadena' '$d/rocola_error.txt' 2>/dev/null"
fi
if [[ " $partes " == *" billar "* ]]; then
  echo "== billar (la misma escena con el modo billar guardado: la B entera, medida despues de sus piezas)"
  d=$(datos billar); printf 'noche=0\r\nlocal_auto=1\r\n' > "$d/rocola_config.txt"
  escena billar "$d" 78 ROCOLA_OS=cadena_nueva,medida_nueva,calidad-prueba,prueba-andamio
  o=$W/billar; b="$d/rocola_os_bitacora.txt"
  grep -F '[cadena_nueva]' "$b" 2>/dev/null | grep '] B ' | sed 's/^/   bitacora: /' | cut -c1-300
  trozos billar
  si "escena billar: sale con 0" test "$(cat $o/salio)" = 0
  si "escena billar: medidas con la B y la huella de sus piezas (cuatro renglones B, ninguno A)" bash -c "[ \$(grep -c '^B	' '$d/rocola_medida_nueva.txt') = 4 ] && ! grep -q '^A	' '$d/rocola_medida_nueva.txt' && ! grep '^B	' '$d/rocola_medida_nueva.txt' | cut -f7 | grep -qx -- '-\|sin-piezas'"
  si "escena billar: las cuatro por la B con su medida, las del disco con la del disco" bash -c "[ \$(grep -F '[cadena_nueva]' '$b' | grep -c '] B .*: ganancia ') = 4 ] && ! grep -F '[cadena_nueva]' '$b' | grep -q 'sin medir' && [ \$(grep -F '[cadena_nueva]' '$b' | grep -c 'media.mp3: ganancia .* (el disco (2 de 2') = 2 ]"
  si "escena billar: el disco entero y la fuerte a -18 LUFS a 0,5 LU despues de las piezas del billar ($(val billar disco 2) y $(val billar fuerte 2))" py "abs($(val billar disco 2) + 18) <= 0.5 and abs($(val billar fuerte 2) + 18) <= 0.5"
  si "escena billar: sin pasar el techo de -1 dBTP ($(val billar media1 3), $(val billar media2 3) y $(val billar fuerte 3))" py "max($(val billar media1 3), $(val billar media2 3), $(val billar fuerte 3)) <= -0.7"
  si "escena billar: el ruido de los escalones de +-10 dB sale con la misma ganancia en cada escalon (las medianas a $(num "$o/constante.txt" escalones_db) dB; cada bloque varia lo que la curva del billar le hace a su tono)" py "$(num "$o/constante.txt" escalones_db) <= 0.05 and $(num "$o/constante.txt" escalones) >= 5"
  si "escena billar: ningun fallo de la cadena nueva ni de las piezas del billar en rocola_error.txt" bash -c "! grep -q 'Rocola OS: cadena_nueva\|Rocola OS: medida_nueva\|Rocola OS: billar_nuevo\|la hojita' '$d/rocola_error.txt' 2>/dev/null"
fi
if [[ " $partes " == *" hoy "* ]]; then
  echo "== hoy (la misma escena con la cadena de hoy: ni se mide ni se arma la nueva; el ruido de los escalones con lo que mueve el nivel)"
  d=$(datos hoy); escena hoy "$d" 62 ROCOLA_OS=medida_nueva,calidad-prueba,prueba-andamio
  o=$W/hoy; b="$d/rocola_os_bitacora.txt"
  trozos hoy
  si "escena hoy: sale con 0" test "$(cat $o/salio)" = 0
  si "escena hoy: ni tabla nueva ni cadena A (la de hoy suena tal cual)" bash -c "[ ! -e '$d/rocola_medida_nueva.txt' ] && ! grep -F '[cadena_nueva]' '$b' 2>/dev/null | grep -q ': ganancia '"
  si "escena hoy: vivo.txt dice que no suena la nueva" bash -c "grep -q '^cadena_nueva=no armadas=0' '$o/vivo.txt'"
  si "escena hoy: el ruido de los escalones, la de hoy mueve la ganancia ($(num "$o/constante.txt" vaiven_db) dB de vaiven; entre escalones $(num "$o/constante.txt" escalones_db) dB)" py "$(num "$o/constante.txt" vaiven_db) > 0.5 and $(num "$o/constante.txt" escalones_db) > 0.5"
fi
if [[ " $partes " == *" sinros "* ]]; then
  echo "== sinros (sin ROCOLA_OS: nada de la cadena nueva)"
  d=$(datos sinros); escena sinros "$d" 52 env -u ROCOLA_OS
  si "escena sin ROCOLA_OS: sale con 0, sin rocola_medida_nueva.txt ni bitacora de Rocola OS" bash -c "[ \$(cat $W/sinros/salio) = 0 ] && [ ! -e '$d/rocola_medida_nueva.txt' ] && [ ! -e '$d/rocola_os_bitacora.txt' ]"
fi

# ---------------------------------------------------------------- spotify (el Soloist falso de spotify-xp)
if [[ " $partes " == *" spotify "* ]]; then
  echo "== spotify (la primera vez fija y estimada, sin el nivelado en vivo; se mide entera; la segunda, con su medida)"
  if [ ! -f "$FALSO/soloist-falso.dll" ] || ! cmp -s "$SP/SoloistFalso/Program.cs" "$FALSO/Program.cs"; then
    echo "(compilando el Soloist falso en $FALSO)"
    mkdir -p "$FALSO"; flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$SP/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
      -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$W/falso-build.txt" 2>&1 || { cat "$W/falso-build.txt"; exit 1; }
    cp "$SP/SoloistFalso/Program.cs" "$FALSO/Program.cs"
  fi
  P="Prueba|Artista inventado|Disco inventado"
  sp() {   # sp NOMBRE DATOS SEGUNDOS: una de Spotify (440 Hz, 34 s) pedida a los 9 s
    local n="$1" d="$2" seg="$3"; local o="$W/$n"; mkdir -p "$o/run/rocola-xp/spotify" "$o/soloist"
    printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
    ( cd "$d" && exec env ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj \
        ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="7:67" ROCOLA_CAPTURA_SALE=0 \
        ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$SP/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" SOLOIST_FALSO_LOG="$o/falso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" \
        ROCOLA_OS=spotify,spotify-prueba,spotify_nivela,cadena_nueva,medida_nueva "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
    local p=$!
    sleep 9; echo "pide spotify:track:f440d034q30|$P|34000" >> "$o/run/rocola-xp/spotify/prueba-pide"
    sleep $((seg - 9)); kill -TERM $p 2>/dev/null
    ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
    wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
    python3 -c "import sys; sys.path.insert(0, sys.argv[1]); from niveles import niveles; [print('%.2f %.3f' % q) for q in niveles(sys.argv[2], sys.argv[3], 440)]" "$SP" "$o/sonado.raw" "$o/registro.txt" > "$o/niveles.txt" 2>/dev/null
  }
  rangos() { python3 - "$1" <<'EOP'
import sys
L=[l.split() for l in open(sys.argv[1]).read().splitlines() if l.strip()]
v=[(float(a),float(b)) for a,b in L]
if not v: print("ini=-99 fin=-99 salto=99"); sys.exit()
t0=v[0][0]; ini=[b for a,b in v if t0+3<=a<=t0+5]; fin=[b for a,b in v if t0+26<=a<=t0+32]; mid=[b for a,b in v if t0+3<=a<=t0+32]
med=lambda x: sorted(x)[len(x)//2] if x else -99
print("ini=%.2f fin=%.2f salto=%.3f" % (med(ini), med(fin), max(abs(mid[i+1]-mid[i]) for i in range(len(mid)-1)) if len(mid) > 1 else 99))
EOP
  }
  d=$(datos sp1); rm -rf "$d/musica"; mkdir -p "$d/musica"
  { echo "ROCOLA-SPOTIFY-NIVELES v1"; for i in $(seq 10 34); do printf 'semilla%s\t-1\t-2\t-4\t-10\t1700000000\n' $i; done; } > "$d/rocola_spotify_niveles.txt"
  sp sp1 "$d" 56; o=$W/sp1; b="$d/rocola_os_bitacora.txt"
  grep -F '[cadena_nueva]' "$b" 2>/dev/null | grep 'spotify:' | sed 's/^/   bitacora: /' | cut -c1-260
  r1=$(rangos "$o/niveles.txt"); echo "   primera vez: $r1"
  si "spotify 1.a vez: sale con 0" test "$(cat $o/salio)" = 0
  si "spotify 1.a vez: por la cadena A con la ganancia fija estimada (de las 25 medidas por XP)" bash -c "grep -F '[cadena_nueva]' '$b' | grep -q 'spotify:track:f440d034q30: ganancia .*Spotify la primera vez: fija, estimada (mediana de 25 medidas por XP'"
  si "spotify 1.a vez: sin el nivelado en vivo: el nivel no sube ni da brincos ($r1)" py "abs($(echo "$r1" | sed 's/.*fin=\([^ ]*\).*/\1/') - ($(echo "$r1" | sed 's/ini=\([^ ]*\).*/\1/'))) <= 0.5 and $(echo "$r1" | sed 's/.*salto=//') <= 0.3"
  si "spotify 1.a vez: sono entera y se midio en vivo (rocola_medida_nueva.txt)" bash -c "grep -q '^A	spotify:track:f440d034q30	' '$d/rocola_medida_nueva.txt'"
  d2="$W/datos-sp2"; rm -rf "$d2"; cp -r "$d" "$d2"; rm -f "$d2/rocola_estado.txt"
  sp sp2 "$d2" 56; o=$W/sp2; b="$d2/rocola_os_bitacora.txt"
  grep -F '[cadena_nueva]' "$b" 2>/dev/null | grep 'spotify:' | sed 's/^/   bitacora: /' | cut -c1-260
  r2=$(rangos "$o/niveles.txt"); echo "   segunda vez: $r2"
  si "spotify 2.a vez: con su medida (la bitacora)" bash -c "grep -F '[cadena_nueva]' '$b' | grep -q 'spotify:track:f440d034q30: ganancia .*(medida '"
  si "spotify 2.a vez: sin brincos y sin nivelado en vivo: el nivel no se mueve ($r2)" py "$(echo "$r2" | sed 's/.*salto=//') <= 0.3 and abs($(echo "$r2" | sed 's/.*fin=\([^ ]*\).*/\1/') - ($(echo "$r2" | sed 's/ini=\([^ ]*\).*/\1/'))) <= 0.5"
  si "spotify 2.a vez: mas fuerte que la 1.a (la estimada de las medidas por XP era -10 LKFS; el tono del falso, mucho mas bajo)" py "$(echo "$r2" | sed 's/ini=\([^ ]*\).*/\1/') > $(echo "$r1" | sed 's/ini=\([^ ]*\).*/\1/') + 10"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
