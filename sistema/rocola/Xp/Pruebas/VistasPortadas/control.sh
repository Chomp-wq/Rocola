#!/bin/bash
# control.sh: controles negativos del arnes del grupo «portadas»: compila VistasPortadas con una copia de esta traduccion con UN fallo puesto
# a proposito (sed sobre una linea) y cuenta cuantas lineas de la salida cambian frente a la referencia (vistas-ref.tsv de arnes.sh con
# GUARDA=1, o la que haya en PORTADAS). Cada control tiene que dar lineas distintas: si diera 0, el arnes no veria ese fallo.
set -u
S=${PORTADAS:-/root/rocola-trabajo/port/portadas}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
[ -f $S/vistas-ref.tsv ] || { echo "falta $S/vistas-ref.tsv (corre arnes.sh)"; exit 1; }
prueba() {  # nombre archivo patron sustitucion
  rm -rf $S/mutante && mkdir -p $S/mutante/src $S/mutante/proy
  (cd "$(git -C $X rev-parse --show-toplevel)" && git archive HEAD sistema/rocola/Xp/Codigo sistema/rocola/Xp/Plataforma sistema/rocola/Xp/Nativo sistema/rocola/Consola/Pantalla/Gl.cs sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs | tar -x -C $S/mutante/src)
  local f=$S/mutante/src/sistema/rocola/Xp/$2
  grep -q -- "$3" "$f" || { echo "$1: NO ESTA el patron"; return; }
  sed -i "s|$3|$4|" "$f"
  cp $X/Pruebas/VistasPortadas/VistasPortadas.csproj $X/Pruebas/VistasPortadas/Portadas.cs $S/mutante/proy/
  (cd $S/mutante/proy && dotnet build -c Release -nologo -v q -p:Fuente=$S/mutante/src/sistema/rocola/Xp -o $S/mutante/bin 2>&1 | grep -E " error " | head -3)
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 dotnet $S/mutante/bin/VistasPortadas.dll $S/mutante/datos > $S/mutante/salida.tsv 2> /dev/null; echo $? > $S/mutante/codigo)
  echo "$1: $(diff $S/vistas-ref.tsv $S/mutante/salida.tsv | grep -c '^[<>]') lineas distintas o que faltan de $(wc -l < $S/vistas-ref.tsv) (salida del programa: $(cat $S/mutante/codigo))"
}
prueba "sin el medio pixel de la miniatura" Codigo/Form1.Portadas.cs "bool medio = true;" "bool medio = false;"
prueba "sin el espejo del borde" Codigo/Form1.Portadas.cs "Envoltura borde = Envoltura.Espejo;" "Envoltura borde = Envoltura.Transparente;"
prueba "el filtro sin prefiltrar en la miniatura" Codigo/Form1.Portadas.cs "Filtro filtro = Filtro.BilinealAlta;   // filtro original" "Filtro filtro = Filtro.Bilineal;   // filtro original"
prueba "la previa sin prefiltrar" Codigo/CaratulasAdaptativas.cs "Filtro filtro = Filtro.BilinealAlta;" "Filtro filtro = Filtro.Bilineal;"
prueba "fuenteOpaca con las GIF de paleta" Codigo/Form1.Portadas.cs "\&\& formatoSrc != FormatoImagen.Paleta8 " "\&\& true "
prueba "MiniaturaOpaca premultiplicando" Codigo/Form1.Portadas.cs "if ((uint)fila\[x\] < 0xFF000000u) return false;" "if ((uint)fila[x] < 0xFE000000u) return false;"
prueba "InteriorOpaco sin el formato" Codigo/DibujoLigeroGpu.cs "if (!alfaOPaleta) return true;" "if (false) return true;"
prueba "teselas sin la fila de abajo" Codigo/DibujoLigeroGpu.cs "int ja = (y == 0) ? 0 : (y - 1) / lado, jb = Math.Min(ty - 1, (y + 1) / lado);" "int ja = (y == 0) ? 0 : (y - 1) / lado, jb = Math.Min(ty - 1, y / lado);"
prueba "copia de la UI con un punto menos" Codigo/CaratulasAdaptativas.cs "Marshal.Copy(bytes, y \* Side \* 4, new IntPtr(scan.ToInt64() + y \* (long)paso), Side \* 4);" "Marshal.Copy(bytes, y * Side * 4, new IntPtr(scan.ToInt64() + y * (long)paso), Side * 4 - 4);"
prueba "guardar el JPEG con otra calidad" Codigo/Form1.Biblioteca.cs "Imagenes.GuardaJpeg(thumb, tmp, (int)calidad);" "Imagenes.GuardaJpeg(thumb, tmp, (int)calidad - 1);"
# (cache-portadas, 12-11-2026) la identidad de la cache y la portada soltada
prueba "la cache da una copia (sin identidad)" Codigo/Form1.Portadas.cs "if (!coverCache.TryGetValue(key, out img)) return false;" "if (!coverCache.TryGetValue(key, out img)) return false; if (img != null) img = img.Copy();"
prueba "reemplazar no manda la de antes a la papelera" Codigo/Form1.Biblioteca.cs "if (previous != null \&\& !object.ReferenceEquals(previous, img)) coverTrash.Add(previous);" "if (false) coverTrash.Add(previous);"
prueba "la portada soltada sin comprobar (MapaVivo)" Nativo/MapaVivo.cs "if (Soltado(b)) throw new ArgumentException(\"Parameter is not valid.\"); return b.Width;" "return b.Width;"
rm -rf $S/mutante
