// EL PUENTE DEL GRUPO «ASISTENTE», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 9-11-2026). Los archivos del grupo
// (PulidoGpu.cs: el degradado tramado de los paneles; AyudaCasillaGpu.cs: la casilla de AYUDA; AsistenteRocola.cs y AsistenteGuiaRocola.cs: los
// globos de la estrella y los botones dibujados; AsistenteEstrellaGpu.cs: la subida del globo) pasan a dibujar con SkiaSharp directo y las
// funciones nativas (Xp/Nativo); cada uno, al traducirse, pone aqui su parte: donde lo llama o lo recibe codigo que todavia dibuja con la capa
// que imita a GDI+ (Dibujo2D.cs), pasa por ella. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada
// de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs
// (PORT_asistente.md dice cual es cual).
#if ROCOLA_GPU
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // ================================================================ PulidoGpu.cs
    public partial class Form1
    {
        // ---- LA FIRMA DE XP DE DegradadoV (devuelve un Brush de GDI+), PARA QUIEN TODAVIA LO PIDE ASI: EstadoReposoGpu.cs y AcabadoGpu.cs (ya
        // traducidos, lo pasan a un SKPaint con PuenteDibujo.Pincel(b, false)). La traducida da el shader del pincel, y un shader no tiene vuelta
        // a un pincel de GDI+ (lo mismo que BrochaReposo y BrochaSuena con los pinceles con shader): aqui va el camino de XP (PulidoGpu.cs,
        // 142-177) con los pinceles de GDI+, sobre el MISMO guardado de cuatro mapas (degradados, degradadoTurno y su cerrojo) y con las MISMAS
        // cuentas (DegradadosGuardados, DegradadosReusados) que la traducida: los dos caminos se reparten los mapas como en XP. El TextureBrush
        // pinta un mapa del guardado prestado (PuenteDibujo.Bitmap: el dueno es el guardado, como el Bitmap de XP), y el de los tamanos de mas de
        // 2048, uno que se suelta al volver (como en XP y en la capa). Se borra cuando esos dos llamen a la traducida, DegradadoV(r, c0, c1,
        // false), que da el shader con el medio pixel de fabrica de su lienzo.
        internal static Rocola.Dibujo2D.Brush DegradadoV(Rectangle r, Color c0, Color c1)
        {
            if (!InterfazTramada || r.Height < 2 || r.Width < 1) return new LinearGradientBrush(r, c0, c1, 90f);
            if (r.Height > 2048) { using (Rocola.Dibujo2D.Bitmap b = PuenteDibujo.Entrega(HorneaMapaDegradado(r.Height, c0, c1))) return CopiaDegradadoAsistente(b, r.Location); }
            lock (degradadoCerrojo)
            {
                int a = c0.ToArgb(), b = c1.ToArgb();
                for (int i = 0; i < degradados.Length; i++)
                {
                    DegradadoGuardado d = degradados[i];
                    if (d == null || d.Alto != r.Height || d.Inicio != a || d.Fin != b) continue;
                    TextureBrush copia = CopiaDegradadoAsistente(PuenteDibujo.Bitmap(d.Mapa, PixelFormat.Format32bppArgb), r.Location);
                    DegradadosReusados++; return copia;
                }
                SKBitmap fuente = HorneaMapaDegradado(r.Height, c0, c1);
                TextureBrush nuevo;
                try { nuevo = CopiaDegradadoAsistente(PuenteDibujo.Bitmap(fuente, PixelFormat.Format32bppArgb), r.Location); }
                catch { fuente.Dispose(); throw; }
                DegradadoGuardado anterior = degradados[degradadoTurno];
                degradados[degradadoTurno] = new DegradadoGuardado { Alto = r.Height, Inicio = a, Fin = b, Mapa = fuente };
                degradadoTurno = (degradadoTurno + 1) % degradados.Length;
                if (anterior != null) anterior.Mapa.Dispose();
                DegradadosGuardados++; return nuevo;
            }
        }
        // el CopiaDegradado de XP con el pincel de GDI+ (el de la traducida da el shader)
        private static TextureBrush CopiaDegradadoAsistente(Rocola.Dibujo2D.Bitmap fuente, Point origen)
        {
            TextureBrush copia = new TextureBrush(fuente, WrapMode.Tile);
            try { copia.TranslateTransform(origen.X, origen.Y); return copia; }
            catch { copia.Dispose(); throw; }
        }
    }
}
#endif
