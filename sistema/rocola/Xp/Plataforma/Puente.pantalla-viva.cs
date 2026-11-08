// EL PUENTE DEL GRUPO «PANTALLA-VIVA», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 8-11-2026). Los archivos del grupo
// (PantallaVivaGpu.cs: el letrero de la portada elegida, la ficha rapida del disco y el color de la portada enfocada; DiscoVivoGpu.cs: el
// boleto que vuela a la cola, la pastilla de SONANDO con el hueco del ecualizador y las medidas de la lista grande y de los minutos) pasan a
// dibujar con SkiaSharp directo y las funciones nativas (Xp/Nativo); cada uno, al traducirse, pone aqui su parte: donde lo llama o donde llama
// el a codigo que todavia dibuja con la capa que imita a GDI+ (Dibujo2D.cs), pasa por ella. La regla: el puente va del lado de lo que no esta
// traducido, y los que llaman no cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al
// final, con Dibujo2D.cs y Puente.cs (PORT_pantalla-viva.md dice cual es cual). En cada parte:
//   - las firmas de XP de lo que llaman los archivos sin traducir: son delgadas, pasan a la traducida los mismos puntos (PuenteDibujo.Mapa),
//     la familia por su nombre, el lienzo del Graphics (con su matriz y su recorte) y su estado; ninguna traducida le cambia el estado al
//     Graphics, asi que no hay nada que devolverle;
//   - lo que la traducida le pide a lo que no lo esta (AnchoTexto de Form1.Ajustes.cs mide con un Graphics, o sin el, y una Font) y lo que
//     la traducida le da a quien aun lo pide con un tipo de la capa (Redondo, con su GraphicsPath).
#if ROCOLA_GPU
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // ================================================================ PantallaVivaGpu.cs
    public partial class Form1
    {
        // ---- LAS FIRMAS DE XP PARA PrecargaPaginaGpu.cs (case 4: la pastilla de "12 canciones" y la ficha rapida, para calentar): los mismos
        // puntos (PuenteDibujo.Mapa) y la familia por su nombre
        internal static string PintaFoco(Bitmap bmp, string txt, string corto, FontFamily fam)
        { return PintaFoco(PuenteDibujo.Mapa(bmp), txt, corto, PuenteDibujo.FamiliaPantallaViva(fam)); }
        internal static void PintaFicha(Bitmap bmp, Image tapa, string artista, string disco, string resumen, string genero, Color colGenero,
            int enCola, bool pedidas, string[] filas, string[] durs, int[] veces, int total, FontFamily fam)
        {
            PintaFicha(PuenteDibujo.Mapa(bmp), PuenteDibujo.Mapa(tapa), artista, disco, resumen, genero, colGenero, enCola, pedidas, filas, durs, veces, total,
                PuenteDibujo.FamiliaPantallaViva(fam));
        }

        // ---- Redondo CON SU GraphicsPath, PARA LOS QUE AUN LO PIDEN ASI: SuenaVivoGpu.cs (traducido: pinta el SKPath del GraphicsPath,
        // PuenteDibujo.Ruta) y AtraccionGpu.cs. La traducida es RutaRedonda (da un SKPath; no puede ser una sobrecarga de esta, que solo cambiaria
        // en lo que devuelve); el GraphicsPath lleva ESE mismo SKPath (los mismos puntos, el mismo relleno par-impar y la figura cerrada, como
        // tras CloseFigure), que se suelta al soltarlo. Se va cuando los dos llamen a RutaRedonda.
        private static GraphicsPath Redondo(RectangleF r, float radio) { return PuenteDibujo.TrazoPantallaViva(RutaRedonda(r, radio)); }
    }
    internal static partial class PuenteDibujo
    {
        // el nombre de la familia de una FontFamily de la capa (FamiliaUi de Form1.Ajustes.cs, sin traducir, da una), que es lo que pide una Letra;
        // la nula es la generica sin remates, como la toma new Font(null, ...) de la capa
        internal static string FamiliaPantallaViva(FontFamily f) { return f != null ? f.Name : Letras.SinRemates; }
        // un GraphicsPath de la capa con ESE SKPath (no una copia): para darle a quien aun lo pide asi el trazo que hizo una traducida. La figura
        // queda cerrada (lo siguiente que se le anada empieza aparte, como tras CloseFigure); al soltar el GraphicsPath se suelta el SKPath
        internal static GraphicsPath TrazoPantallaViva(SKPath p)
        {
            GraphicsPath gp = new GraphicsPath();
            gp.P.Dispose(); gp.P = p;
            return gp;
        }
    }

    // ================================================================ DiscoVivoGpu.cs
    public partial class Form1
    {
        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA: AnchoTexto (Form1.Ajustes.cs)
        // con un Graphics: solo mide (MeasureString con su modo de letra), asi que basta uno sobre el mismo lienzo con ese modo; la Font es la que
        // da el que llama (la de la lista, un control de WinForms, o fCola de Form1.Vistas.cs), tal cual
        private static int AnchoTextoPantallaViva(SKCanvas g, AjusteLetra ajuste, string t, Font f)
        {
            using (Graphics gg = PuenteDibujo.GraphicsPantallaViva(g, ajuste)) return AnchoTexto(gg, t, f);
        }
        // sin Graphics (TamanoBoleto): la Font de esa Letra (la misma familia, tamano, unidad y estilo), soltada al acabar
        private static int AnchoTextoPantallaViva(string t, Rocola.Nativo.Letra l)
        {
            using (Font f = PuenteDibujo.Font(l)) return AnchoTexto(t, f);
        }

        private partial class SongListView
        {
            // ---- LAS FIRMAS DE XP PARA Form1.Vistas.cs (PintaPagina, la pagina horneada de la lista): el lienzo del Graphics y su estado
            private int AnchoDuracion(Graphics g) { return AnchoDuracion(PuenteDibujo.Lienzo(g), PuenteDibujo.Estado(g).Letra); }
            private string PlanFilaGpu(Graphics g, int idx, int pos, bool enCola, bool suena, Rectangle r, int durW, float xTexto, int hCola, ref string gen)
            { return PlanFilaGpu(PuenteDibujo.Lienzo(g), PuenteDibujo.Estado(g).Letra, idx, pos, enCola, suena, r, durW, xTexto, hCola, ref gen); }
            private void PlanPaginaGpu(Graphics g, int rpp, int n, int w, float xTexto, int hCola)
            { PlanPaginaGpu(PuenteDibujo.Lienzo(g), PuenteDibujo.Estado(g).Letra, rpp, n, w, xTexto, hCola); }
            private int PastillaSonandoEq(Graphics g, int idx, ref float xMarca, float cy, int hCola)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                return PastillaSonandoEq(PuenteDibujo.Lienzo(g), e.Letra, e.Filtro, e.Copia, e.Medio, idx, ref xMarca, cy, hCola);
            }
        }
    }
    internal static partial class PuenteDibujo
    {
        // un Graphics sobre el lienzo de un archivo traducido, para que mida uno que no lo esta (AnchoTexto: MeasureString, que solo mira el modo de
        // letra); lo demas, lo de fabrica de un Graphics de un mapa de bits. No suelta el lienzo ni le cambia nada.
        internal static Graphics GraphicsPantallaViva(SKCanvas c, AjusteLetra ajuste)
        { return Graphics(c, new EstadoLienzo(false, ajuste, false, Filtro.Bilineal, false, SKMatrix.Identity)); }
    }
}
#endif
