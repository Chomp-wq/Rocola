# comun.sh -- lo que comparten los guiones de la tarjeta (se lee con «.»). Se pueden cambiar con el entorno:
#   TARJETA      la carpeta de trabajo (fuera del repositorio): cintas/, salidas/, registros/, trazas/, control/, reproductor/ (el
#                reproductor compilado) y ref/ (una copia de bin/Release/net10.0 de la referencia congelada, con sus datos de prueba)
#   REFERENCIA   la copia congelada con capas (de ella se toman los datos de prueba, sin tocarla)
TARJETA=${TARJETA:-/root/rocola-trabajo/port/tarjeta}
REFERENCIA=${REFERENCIA:-/root/rocola-trabajo/referencia-xp}
guiones=$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)
arbol=$(cd "$guiones/../../../.." && pwd)   # sistema/rocola de este arbol
