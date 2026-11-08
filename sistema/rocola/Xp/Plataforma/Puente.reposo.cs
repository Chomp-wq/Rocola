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
        // ---- LAS FIRMAS DE XP: TextoConCinta (BuscadorPortadasGpu.cs, EstiloGpu.cs), CintaVistaPrevia (Form1.Buscador.cs), PintaTextoMovil
        // (Form1.Vistas.cs, SuenaVivoGpu.cs, EstadoReposoGpu.cs) y PintaArtistaCompacto (Form1.Video.cs, EstiloGpu.cs). El lienzo del Graphics y
        // su estado (el modo de letra; para pintar, tambien la copia y el medio pixel), la Letra de la Font, el pincel en un SKPaint (su color o
        // su shader, con el medio pixel del Graphics; un pincel nulo no pinta, como en GDI+) y el MISMO FormatoTexto. Ninguna le cambia el
        // estado al Graphics.
        internal static bool TextoConCinta(Graphics g, string text, Font f, Color color, RectangleF rect, StringFormat sf)
        { return TextoConCinta(PuenteDibujo.Lienzo(g), PuenteDibujo.Estado(g).Letra, text, PuenteDibujo.Letra(f), color, rect, PuenteDibujo.Formato(sf)); }
        internal static bool CintaVistaPrevia(Graphics g, string text, Font font, Color color, Rectangle rect)
        { return CintaVistaPrevia(PuenteDibujo.Lienzo(g), PuenteDibujo.Estado(g).Letra, text, PuenteDibujo.Letra(font), color, rect); }
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
        // PintaRenglonLejos (EstiloGpu.cs) y DrawLabelOverlay (Form1.Video.cs), para PintaArtistaCompacto: la letra de la etiqueta (una Font de
        // WinForms) pasa tal cual. Solo escriben: el suavizado no lo usan (el que tiene quien llama en XP, AntiAlias).
        private static void PintaRenglonLejosReposo(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, string texto, Font f, Color c, RectangleF r, Alinea al)
        {
            using (Graphics gg = GraphicsReposo(g, true, ajuste, copia, medio))
                PintaRenglonLejos(gg, texto, f, c, r, (StringAlignment)(int)al);
        }
        private static void DrawLabelOverlayReposo(SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, Label l, Color color, RectangleF caja)
        {
            using (Graphics gg = GraphicsReposo(g, true, ajuste, copia, medio))
                DrawLabelOverlay(gg, l, color, caja);
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
}
#endif
