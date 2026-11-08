// EL VOLCADO DE LAS TEXTURAS QUE SE SUBEN A LA TARJETA (pruebas del port nativo, tanda 2, grupo «frontera», 8-11-2026; no es de XP).
//
// Con ROCOLA_VOLCADO_TEXTURAS=carpeta, cada zona de 64x64 o mas que la rocola sube a una textura se guarda en esa carpeta en PNG, como la
// guardaba la capa de la transicion (Direct3DaGL.cs, UnlockRect y Vuelca, de la referencia congelada aef2211): los puntos tal como se suben
// (BGRA sin premultiplicar, lo que leia LockBits Format32bppArgb), con el mismo nombre (NNNN_T.Ts_WxH_nID_enX,Y_BWxBH.png). Los recorridos
// (Pruebas/Recorridos, VOLCADO=1 y HUELLAS=1) comparan asi, textura por textura, lo que sube la traduccion con lo que subia la referencia.
// La traduccion de la tarjeta (tanda 1) quito Direct3DaGL.cs y con ella el volcado; vuelve aqui, en las dos subidas de XP (SubeBitmapBorde
// de D3D9Star.cs y ActualizaRegionBitmap de TexturasCompatiblesGpu.cs), despues de un glTexSubImage2D que salio bien:
//     ... else if (VolcadoTexturas.Vuelca) VolcadoTexturas.Apunta(...);   (pruebas: ROCOLA_VOLCADO_TEXTURAS, Nativo/VolcadoTexturas.cs)
// Sin la variable, 'Vuelca' es un static readonly falso y no hace nada.
#if ROCOLA_GPU
using System;
using System.IO;
using SkiaSharp;

namespace Rocola
{
    internal static unsafe class VolcadoTexturas
    {
        private static readonly string carpeta = Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_TEXTURAS");
        internal static readonly bool Vuelca = carpeta != null;
        private static int volcadas;

        // la zona (x, y, ancho, alto) de 'textura' que se acaba de subir desde 'datos' (filas de ancho * 4 bytes): si mide 64x64 o mas, a PNG
        internal static void Apunta(uint textura, int x, int y, int ancho, int alto, IntPtr datos)
        {
            if (carpeta == null || ancho < 64 || alto < 64 || datos == IntPtr.Zero) return;
            int tw, th;
            if (!TexturasGl.Medida(textura, out tw, out th)) return;
            try
            {
                using (SKBitmap b = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Bgra8888, SKAlphaType.Unpremul)))
                {
                    Buffer.MemoryCopy((void*)datos, (void*)b.GetPixels(), (long)ancho * alto * 4, (long)ancho * alto * 4);
                    using (SKData d = b.Encode(SKEncodedImageFormat.Png, 100))
                    using (FileStream f = File.Create(Path.Combine(carpeta, (++volcadas).ToString("0000") + "_" + (PantallaTarjeta.AhoraMs / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s_" + tw + "x" + th + "_n" + textura + "_en" + x + "," + y + "_" + ancho + "x" + alto + ".png")))
                        d.SaveTo(f);
                }
            }
            catch { }
        }
    }
}
#endif
