#!/bin/bash
# instrumenta-volcado.sh FUENTE DESTINO: una copia del programa (FUENTE: una carpeta sistema/rocola, o un commit de git) en DESTINO, con el
# volcado de las texturas que se suben (ROCOLA_VOLCADO_TEXTURAS, el VOLCADO=1 de Pruebas/Recorridos) y compilada; deja en DESTINO/prog lo
# que corre.sh necesita. Solo para las pruebas: la copia no es el programa.
#
# Por que (grupo «suena» de la tanda 2, 8-11-2026): el volcado de las texturas estaba en Plataforma/Direct3DaGL.cs (la capa que imitaba a
# Direct3D, que la pista de la tarjeta quito en la tanda 1) y con el no se fue a ningun otro sitio: desde b609319, VOLCADO=1 no vuelca nada
# y los recorridos solo comparan las capas de Dibujo2D. Las texturas de la referencia congelada (cmp-ref y cmp-ref2) si estan. Esta copia
# pone el mismo volcado donde ahora se suben los puntos (TexturasGl.Sube, lo que era UnlockRect): los mismos puntos (los que XP escribio en
# la memoria de la textura, B G R A sin premultiplicar), el mismo PNG de Skia, el mismo nombre y la misma regla (zonas de 64x64 o mas;
# ROCOLA_VOLCADO_MINIMO=n la cambia, para ver tambien las chicas, que la referencia no tiene). Comprobado: la copia de b609319 sale con
# las texturas quietas de la referencia (PORT_suena.md).
set -eu
FUENTE=$1; DESTINO=$2
RAIZ=$(cd "$(dirname "$0")/../../../../.." && pwd)
rm -rf "$DESTINO"; mkdir -p "$DESTINO/sistema/rocola"
if [ -d "$FUENTE" ]; then
  (cd "$FUENTE" && tar -cf - --exclude=bin --exclude=obj --exclude=Pruebas Xp/Codigo Xp/Plataforma Xp/Nativo Xp/RocolaXp.csproj Consola/Pantalla/Gl.cs Consola/Pantalla/Kms.cs Motor/Botones.cs) | tar -xf - -C "$DESTINO/sistema/rocola"
else
  git -C "$RAIZ" archive "$FUENTE" sistema/rocola/Xp/Codigo sistema/rocola/Xp/Plataforma sistema/rocola/Xp/Nativo sistema/rocola/Xp/RocolaXp.csproj \
    sistema/rocola/Consola/Pantalla/Gl.cs sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs | tar -xf - -C "$DESTINO"
fi
python3 - "$DESTINO/sistema/rocola/Xp/Nativo/TexturasGl.cs" <<'FIN'
import sys
p = sys.argv[1]; s = open(p, encoding='utf-8').read()
sube = "            GL.TexSubImage2D(GL.TEXTURE_2D, 0, x, y, ancho, alto, GL.RGBA, GL.UNSIGNED_BYTE, (void*)datos);\n"
assert s.count(sube) == 1, "TexturasGl.Sube cambio: revisar el volcado"
s = s.replace(sube, sube + "            if (volcado != null && ancho >= volcadoMinimo && alto >= volcadoMinimo) Vuelca(textura, i, x, y, ancho, alto, datos);\n")
metodo = '''
        // (pruebas, copia instrumentada) ROCOLA_VOLCADO_TEXTURAS=carpeta: cada textura que se sube, en PNG, como la volcaba Direct3DaGL.cs
        private static readonly string volcado = Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_TEXTURAS");
        private static readonly int volcadoMinimo = int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_MINIMO"), out int m) ? m : 64;
        private static int volcadas;
        private static readonly System.Diagnostics.Stopwatch relojVolcado = System.Diagnostics.Stopwatch.StartNew();
        private static void Vuelca(uint textura, Info i, int x, int y, int ancho, int alto, IntPtr datos)
        {
            try
            {
                using (SkiaSharp.SKBitmap b = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(ancho, alto, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul)))
                {
                    Buffer.MemoryCopy((void*)datos, (void*)b.GetPixels(), (long)ancho * alto * 4, (long)ancho * alto * 4);
                    using (SkiaSharp.SKData d = b.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                    using (System.IO.FileStream f = System.IO.File.Create(System.IO.Path.Combine(volcado, (++volcadas).ToString("0000") + "_" + (relojVolcado.ElapsedMilliseconds / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s_" + i.Ancho + "x" + i.Alto + "_n" + textura + "_en" + x + "," + y + "_" + ancho + "x" + alto + ".png")))
                        d.SaveTo(f);
                }
            }
            catch { }
        }
'''
marca = "        internal static bool Medida(uint textura, out int ancho, out int alto)\n"
assert s.count(marca) == 1
s = s.replace(marca, metodo + "\n" + marca)
open(p, 'w', encoding='utf-8').write(s)
FIN
cd "$DESTINO/sistema/rocola/Xp"
dotnet build -c Release -nologo -v q 2>&1 | grep -E "error|Warn|Error" || true
B=bin/Release/net10.0
mkdir -p "$DESTINO/prog/runtimes"
cp -p $B/*.dll $B/*.json $B/rocola-xp $B/*.pdb "$DESTINO/prog/"; cp -rp $B/runtimes/linux-x64 "$DESTINO/prog/runtimes/"
rm -rf bin obj
echo "listo: $DESTINO/prog"
