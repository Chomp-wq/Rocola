#!/bin/bash
# control.sh: controles negativos del arnes del grupo «portadas»: compila VistasPortadas con una copia de esta traduccion con UN fallo puesto
# a proposito (sed sobre una linea) y cuenta cuantas lineas de la salida cambian frente a la referencia (vistas-ref.tsv de arnes.sh con
# GUARDA=1, o la que haya en PORTADAS). Cada control tiene que dar lineas distintas: si diera 0, el arnes no veria ese fallo.
set -u
S=${PORTADAS:-/root/rocola-trabajo/port/portadas}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
[ -f $S/vistas-ref.tsv ] || { echo "falta $S/vistas-ref.tsv (corre arnes.sh)"; exit 1; }
prueba() {  # nombre archivo patron sustitucion
  rm -rf $S/mutante && mkdir -p $S/mutante/src $S/mutante/proy
  (cd "$(git -C $X rev-parse --show-toplevel)" && git archive HEAD sistema/rocola/Xp/Codigo sistema/rocola/Xp/Plataforma sistema/rocola/Xp/Nativo sistema/rocola/Consola/Pantalla/Gl.cs sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs | tar -x -C $S/mutante/src)
  local f=$S/mutante/src/sistema/rocola/Xp/$2
  grep -q -- "$3" "$f" || { echo "$1: NO ESTA el patron"; return; }
  sed -i "s|$3|$4|" "$f"
  cp $X/Pruebas/VistasPortadas/VistasPortadas.csproj $X/Pruebas/VistasPortadas/Portadas.cs $S/mutante/proy/
  (cd $S/mutante/proy && dotnet build -c Release -nologo -v q -p:Fuente=$S/mutante/src/sistema/rocola/Xp -o $S/mutante/bin 2>&1 | grep -E " error " | head -3)
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 dotnet $S/mutante/bin/VistasPortadas.dll $S/mutante/datos > $S/mutante/salida.tsv 2> /dev/null)
  echo "$1: $(diff $S/vistas-ref.tsv $S/mutante/salida.tsv | grep -c '^>') lineas distintas de $(wc -l < $S/vistas-ref.tsv)"
}
prueba "sin el medio pixel de la miniatura" Codigo/Form1.Portadas.cs "bool medio = true;" "bool medio = false;"
prueba "sin el espejo del borde" Codigo/Form1.Portadas.cs "Envoltura borde = Envoltura.Espejo;" "Envoltura borde = Envoltura.Transparente;"
prueba "el filtro sin prefiltrar en la miniatura" Codigo/Form1.Portadas.cs "Filtro filtro = Filtro.BilinealAlta;   // filtro original" "Filtro filtro = Filtro.Bilineal;   // filtro original"
prueba "la previa sin prefiltrar" Codigo/CaratulasAdaptativas.cs "Filtro filtro = Filtro.BilinealAlta;" "Filtro filtro = Filtro.Bilineal;"
prueba "fuenteOpaca con las GIF de paleta" Codigo/Form1.Portadas.cs "\&\& formatoSrc != FormatoImagen.Paleta8 " "\&\& true "
prueba "MiniaturaOpaca premultiplicando" Codigo/Form1.Portadas.cs "if ((uint)fila\[x\] < 0xFF000000u) return false;" "if ((uint)fila[x] < 0xFE000000u) return false;"
prueba "InteriorOpaco sin el formato" Plataforma/Puente.portadas.cs "(f \& Rocola.Dibujo2D.PixelFormat.Indexed) != 0" "false"
prueba "teselas sin la fila de abajo" Codigo/DibujoLigeroGpu.cs "int ja = (y == 0) ? 0 : (y - 1) / lado, jb = Math.Min(ty - 1, (y + 1) / lado);" "int ja = (y == 0) ? 0 : (y - 1) / lado, jb = Math.Min(ty - 1, y / lado);"
prueba "copia de la UI con un punto menos" Codigo/CaratulasAdaptativas.cs "Marshal.Copy(bytes, y \* Side \* 4, new IntPtr(scan.ToInt64() + y \* (long)paso), Side \* 4);" "Marshal.Copy(bytes, y * Side * 4, new IntPtr(scan.ToInt64() + y * (long)paso), Side * 4 - 4);"
prueba "guardar el JPEG con otra calidad" Codigo/Form1.Biblioteca.cs "Imagenes.GuardaJpeg(thumb, tmp, (int)calidad);" "Imagenes.GuardaJpeg(thumb, tmp, (int)calidad - 1);"
rm -rf $S/mutante
