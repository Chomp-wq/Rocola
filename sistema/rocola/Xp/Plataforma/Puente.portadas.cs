// EL PUENTE DEL GRUPO «PORTADAS», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Portadas.cs (leer,
// reducir y copiar las miniaturas), Form1.Biblioteca.cs (guardarlas), CaratulasAdaptativas.cs (las previas de 32x32), DibujoLigeroGpu.cs (las
// teselas y si una portada es opaca por dentro) y RespuestaGpu.cs (la subida con repuesto) ya trabajan con SKBitmap y las funciones nativas
// (Xp/Nativo); donde los llama codigo que todavia pasa un Bitmap de la capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el
// puente va del lado de lo que no esta traducido, y los que llaman no cambian. Son delgadas: le pasan a la traducida los MISMOS puntos
// (PuenteDibujo.Mapa: sin copia y sin hacerse dueno) y, donde la traducida necesita saber con que formato tenia XP el mapa, ese dato. Nada
// de esto queda: cada una se borra cuando se traduzca el archivo que la llama, y todas al final, con Dibujo2D.cs y Puente.cs
// (PORT_portadas.md dice cual es cual).
//   - SaveThumb con un Bitmap: la copia por guardar del calentador de portadas (Form1.cs, DecodeCoverParaCalentar);
//   - TeselasEnPasos con un Bitmap: la imagen de la interfaz (GetUi y GetUiBot de D3D9Star.cs; la hace Form1.Video.cs);
//   - InteriorOpaco con un Bitmap: la portada entera de la casilla (CaratulasSeparadasGpu.cs, cell.FullCover); el formato del Bitmap dice si
//     trae canal alfa o paleta (un SKBitmap no lo guarda);
//   - SubeReponiendo con un Bitmap: la interfaz y las piezas de los buscadores (BuscadorLigeroGpu.cs, SubeUi; BuscadorPortadasGpu.cs, SubePieza).
using System;

namespace Rocola
{
    public partial class Form1
    {
        // ---- SaveThumb, para Form1.cs (el calentador guarda la copia que le dio DecodeCoverParaCalentar): los mismos puntos
        private static void SaveThumb(Rocola.Dibujo2D.Bitmap thumb, string cacheFile) { SaveThumb(PuenteDibujo.Mapa(thumb), cacheFile); }
    }

#if ROCOLA_GPU
    internal sealed partial class GpuPanel
    {
        // ---- TeselasEnPasos, para D3D9Star.cs (la interfaz de la capa: GetUi, GetUiBot): los mismos puntos; la identidad que compara
        // TrabajoTeselas es la de esos puntos, que es la misma mientras sea el mismo Bitmap
        private TeselasVisibles TeselasEnPasos(ref TrabajoTeselas trabajo, Rocola.Dibujo2D.Bitmap b, int w, int h, double subidaMs)
        { return TeselasEnPasos(ref trabajo, PuenteDibujo.Mapa(b), w, h, subidaMs); }

        // ---- InteriorOpaco, para CaratulasSeparadasGpu.cs (la portada entera de la casilla, un Bitmap de la capa): los mismos puntos y lo que
        // XP miraba de su formato (Image.IsAlphaPixelFormat o de paleta)
        internal static bool InteriorOpaco(Rocola.Dibujo2D.Bitmap b)
        {
            if (b == null) return false;
            Rocola.Dibujo2D.PixelFormat f = b.PixelFormat;
            return InteriorOpaco(PuenteDibujo.Mapa(b), Rocola.Dibujo2D.Image.IsAlphaPixelFormat(f) || (f & Rocola.Dibujo2D.PixelFormat.Indexed) != 0);
        }

        // ---- SubeReponiendo, para BuscadorLigeroGpu.cs y BuscadorPortadasGpu.cs (la interfaz y las piezas, Bitmap de la capa): los mismos puntos
        private IntPtr SubeReponiendo(IntPtr tex, ref int bw, ref int bh, Rocola.Dibujo2D.Bitmap bmp, out float u, out float v)
        { return SubeReponiendo(tex, ref bw, ref bh, PuenteDibujo.Mapa(bmp), out u, out v); }
    }
#endif
}
