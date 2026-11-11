// EL PUENTE DEL GRUPO «REPOSO», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 8-11-2026). Los archivos del grupo
// (TextoMovilGpu.cs: las cintas del texto que se desliza y el texto que se mueve; FranjaLigeraGpu.cs: el recuadro de la franja de abajo;
// EstadoReposoGpu.cs: el panel del estado extendido y su reloj) pasan a dibujar con SkiaSharp directo y las funciones nativas (Xp/Nativo);
// cada uno, al traducirse, pone aqui su parte: donde lo llama o lo recibe codigo que todavia dibuja con la capa que imita a GDI+
// (Dibujo2D.cs), pasa por ella. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto
// queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_reposo.md
// dice cual es cual). En cada parte:
//   - las firmas de XP de lo que llaman los archivos sin traducir: son delgadas, pasan a la traducida los mismos puntos (PuenteDibujo.Mapa),
//     la misma Letra, el color o el shader de cada pincel, el MISMO FormatoTexto y el lienzo con su estado;
//   - lo que la traducida le pide a lo que no lo esta (pinta con un Graphics, una Font, un Brush y un StringFormat): un Graphics sobre el
//     mismo lienzo con el estado del que llama.
#if ROCOLA_GPU
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // ================================================================ TextoMovilGpu.cs
    public partial class Form1
    {
        // ---- LAS FIRMAS DE XP: TextoConCinta (BuscadorPortadasGpu.cs, EstiloGpu.cs), PintaTextoMovil (Form1.Vistas.cs, SuenaVivoGpu.cs,
        // EstadoReposoGpu.cs) y PintaArtistaCompacto (EstiloGpu.cs; Form1.Video.cs ya la llama directo). El lienzo del Graphics y su estado (el
        // modo de letra; para pintar, tambien la copia y el medio pixel), la Letra de la Font, el pincel en un SKPaint (su color o su shader, con
        // el medio pixel del Graphics; un pincel nulo no pinta, como en GDI+) y el MISMO FormatoTexto. Ninguna le cambia el estado al Graphics.
        // (La de CintaVistaPrevia se quito al traducirse Form1.Buscador.cs, grupo buscador: DrawPreviewInfo la llama directo.)
        internal static bool TextoConCinta(Graphics g, string text, Font f, Color color, RectangleF rect, StringFormat sf)
        { return TextoConCinta(PuenteDibujo.Lienzo(g), PuenteDibujo.Estado(g).Letra, text, PuenteDibujo.Letra(f), color, rect, PuenteDibujo.Formato(sf)); }
        internal static void PintaTextoMovil(Graphics g, string text, Font f, Brush brush, RectangleF rect, StringFormat sf)
        {
            EstadoLienzo e = PuenteDibujo.Estado(g);
            SKPaint p = brush != null ? PuenteDibujo.Pincel(brush, e.Medio) : null;
            try { PintaTextoMovil(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio, text, PuenteDibujo.Letra(f), p, rect, PuenteDibujo.Formato(sf)); }
            finally { PuenteDibujo.SueltaPincelReposo(p); }
        }
        private void PintaArtistaCompacto(Graphics g)
        { EstadoLienzo e = PuenteDibujo.Estado(g); PintaArtistaCompacto(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio); }

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA
        // Un Graphics sobre el mismo lienzo con el estado del que llama: el filtro, el de fabrica (los que pasan por aqui solo escriben y trazan,
        // no pintan imagenes); la matriz del HDC, la de un mapa de bits (la identidad: quien llama pinta en un mapa, el panel de reposo o la
        // franja). (Tambien lo usa la parte de EstadoReposoGpu.cs, abajo.)
        private static Graphics GraphicsReposo(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio)
        { return PuenteDibujo.Graphics(g, new EstadoLienzo(suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity)); }
        // PintaRenglonLejos (EstiloGpu.cs), para PintaArtistaCompacto: la letra de la etiqueta (una Font de WinForms) pasa tal cual. Solo escribe:
        // el suavizado no lo usa (el que tiene quien llama en XP, AntiAlias). (DrawLabelOverlay, de Form1.Video.cs, ya esta traducida: se la
        // llama directo; su puente se quito con el grupo video-form1.)
        private static void PintaRenglonLejosReposo(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, string texto, Font f, Color c, RectangleF r, Alinea al)
        {
            using (Graphics gg = GraphicsReposo(g, true, ajuste, copia, medio))
                PintaRenglonLejos(gg, texto, f, c, r, (StringAlignment)(int)al);
        }
    }
    internal static partial class PuenteDibujo
    {
        // suelta un pincel hecho con Pincel(brush, medio): su shader y su efecto de trazo, y el pincel (lo que hace la capa al pintar)
        internal static void SueltaPincelReposo(SKPaint p)
        {
            if (p == null) return;
            SKShader s = p.Shader; p.Shader = null; if (s != null) s.Dispose();
            SKPathEffect e = p.PathEffect; p.PathEffect = null; if (e != null) e.Dispose();
            p.Dispose();
        }
    }

    // ================================================================ FranjaLigeraGpu.cs
    // (La firma de XP de EmpiezaRecuadro, para EnsureUiBotOverlay de Form1.Video.cs, se quito con el grupo video-form1: Form1.Video.cs ya
    // llama directo a la traducida.)

    // ================================================================ EstadoReposoGpu.cs
    public partial class Form1
    {
        // ---- LAS FIRMAS DE XP QUE LLAMA PrecargaPaginaGpu.cs (la precarga del panel de reposo y su reloj)
        // Los mismos puntos (PuenteDibujo.Mapa). El mapa que devuelve: si es el que se dio, el MISMO Bitmap (como XP, que devuelve el mismo
        // objeto); si la traducida hizo uno nuevo, un Bitmap que se hace su dueno (quien llama lo suelta con su using) con el formato con que XP
        // lo hacia (new Bitmap(w, h, Format32bppPArgb)).
        internal static Bitmap RenderIdlePanelEn(Bitmap bitmap, int w, int h, string title, string artist, string time,
            string[] qt, string[] qa, Image[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen,
            ReposoGrande extra, bool opaque, out int progressStart)
        {
            SKBitmap[] mapas = null;
            if (covers != null) { mapas = new SKBitmap[covers.Length]; for (int i = 0; i < covers.Length; i++) mapas[i] = PuenteDibujo.Mapa(covers[i]); }
            SKBitmap dado = PuenteDibujo.Mapa(bitmap);
            SKBitmap hecho = RenderIdlePanelEn(dado, w, h, title, artist, time, qt, qa, mapas, count, accent, transparent, badgeKind, dozen, extra, opaque, out progressStart);
            return bitmap != null && ReferenceEquals(hecho, dado) ? bitmap : PuenteDibujo.Entrega(hecho, PixelFormat.Format32bppPArgb);
        }
        internal static void CopiaRegionReposo(Bitmap source, Rectangle area, Bitmap target, Point position)
        { CopiaRegionReposo(PuenteDibujo.Mapa(source), area, PuenteDibujo.Mapa(target), position); }
        internal static int UpdateIdleClock(Bitmap panel, Bitmap underlay, string time)
        { return UpdateIdleClock(PuenteDibujo.Mapa(panel), PuenteDibujo.Mapa(underlay), time); }
        internal static int UpdateIdleClockRegion(Bitmap panel, Bitmap underlay, string time, int previousRight, out Rectangle dirty)
        { return UpdateIdleClockRegion(PuenteDibujo.Mapa(panel), PuenteDibujo.Mapa(underlay), time, previousRight, out dirty); }

        // ---- FuenteTituloReposo, PARA SuenaVivoGpu.cs (el titulo que se desliza): la Font de la letra del titulo con los mismos datos (familia
        // como la resuelve GDI+, tamano en pixeles y negrita). La traducida (LetraTituloReposo) la da como Letra; la de en medio se suelta.
        internal static Font FuenteTituloReposo(int w, int h)
        { using (Rocola.Nativo.Letra l = LetraTituloReposo(w, h)) return PuenteDibujo.Font(l); }

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA: PintaTituloGrande y PintaMinutosCola (SuenaVivoGpu.cs), con un Graphics sobre
        // el mismo lienzo (GraphicsReposo, arriba), las Font de las Letra (los mismos datos), un SolidBrush del color de cada pincel y el MISMO
        // FormatoTexto. La firma es la que tienen ya traducidas (grupo «suena»): al juntar, la llamada pierde el «Reposo» y estas dos se borran.
        private static void PintaTituloGrandeReposo(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, ReposoGrande x, string title, string artist,
            Rocola.Nativo.Letra big, Rocola.Nativo.Letra normal, Rocola.Nativo.Letra small, SKPaint white, SKPaint color, FormatoTexto line, int tx, int tw)
        {
            using (Graphics gg = GraphicsReposo(g, suave, ajuste, copia, medio))
            using (Font fb = PuenteDibujo.Font(big)) using (Font fn = PuenteDibujo.Font(normal)) using (Font fs = PuenteDibujo.Font(small))
            using (Brush bw = PuenteDibujo.BrochaReposo(white)) using (Brush bc = PuenteDibujo.BrochaReposo(color))
                PintaTituloGrande(gg, x, title, artist, fb, fn, fs, bw, bc, PuenteDibujo.StringFormat(line), tx, tw);
        }
        private static void PintaMinutosColaReposo(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, string minutos, string artista,
            Rocola.Nativo.Letra f, SKPaint tenue, bool estrecha, float x, float y, float w, float h, FormatoTexto line)
        {
            using (Graphics gg = GraphicsReposo(g, suave, ajuste, copia, medio))
            using (Font ff = PuenteDibujo.Font(f)) using (Brush bt = PuenteDibujo.BrochaReposo(tenue))
                PintaMinutosCola(gg, minutos, artista, ff, bt, estrecha, x, y, w, h, PuenteDibujo.StringFormat(line));
        }
    }
    internal static partial class PuenteDibujo
    {
        // un pincel de un color (SKPaint sin shader) de un archivo traducido para uno que no lo esta y lo pide como Brush: el SolidBrush de ese
        // color (el mismo ARGB). XP solo pasa por aqui pinceles de un color (EstadoReposoGpu.cs: white, color y muted, todos SolidBrush); uno
        // con shader no tiene vuelta, y se dice en vez de pintar otra cosa.
        internal static Brush BrochaReposo(SKPaint p)
        {
            if (p == null) return null;
            if (p.Shader != null) throw new NotSupportedException("PuenteDibujo.BrochaReposo: solo pinceles de un color");
            return new SolidBrush(ColorSk.DeSk(p.Color));
        }
    }
}
#endif
