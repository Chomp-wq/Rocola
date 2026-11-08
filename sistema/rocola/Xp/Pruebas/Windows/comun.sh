# LO COMUN DE LAS HERRAMIENTAS DE LA PISTA WINDOWS (port nativo de XP; ver Xp/PORT_WINDOWS.md). Se carga con «. comun.sh».
#   W         la carpeta de trabajo, fuera del repositorio (programas copiados, corridas, la tarjeta de prueba compilada)
#   ROCOLA    sistema/rocola de esta rama (de donde sale el programa «mio»)
#   REF       la referencia congelada (aef2211, con capas): nunca se toca, solo se copia
#   DATOSXP   los datos de prueba (musica/, rocola_*.txt, rocola_teclas.txt, rocola_miniaturas/)
AQUI=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
W=${W:-/root/rocola-trabajo/port/windows}
ROCOLA=${ROCOLA:-$(cd "$AQUI/../../.." && pwd)}
REF=${REF:-/root/rocola-trabajo/referencia-xp/sistema/rocola}
DATOSXP=${DATOSXP:-/home/user/Rocola/sistema/rocola/Xp/bin/Release/net10.0}
mkdir -p "$W/runs"
# la tarjeta de prueba con reloj (alsa/reloj.c), compilada una vez en W/alsa
reloj() {
  mkdir -p "$W/alsa"
  if [ ! -f "$W/alsa/libasound_module_pcm_reloj.so" ] || [ "$AQUI/alsa/reloj.c" -nt "$W/alsa/libasound_module_pcm_reloj.so" ]; then
    gcc -shared -fPIC -DPIC -O2 -o "$W/alsa/libasound_module_pcm_reloj.so" "$AQUI/alsa/reloj.c" -lasound || exit 1
  fi
}
