// EL PUENTE DEL GRUPO «VISTAS-B», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). La segunda mitad de
// Form1.Vistas.cs (HeaderView: el encabezado de la seccion; PastillaView: las capsulas de los creditos y de la cola; GeneroView: la pastilla
// del genero; ProgressView: la barra de avance; SongListView: la lista de canciones de un disco y sus hornos) pasa a dibujar con SkiaSharp
// directo y las funciones nativas (Xp/Nativo). Donde la llama, o donde llama ella a, codigo que todavia dibuja con la capa que imita a GDI+
// (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda:
// cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_vistas-b.md dice
// cual es cual).
//   - Las firmas de XP de RenderContent (con un Graphics) de las cinco vistas, para sus OnPaint (WinForms), DrawCtrlOverlay (Form1.Video.cs),
//     EnSeco (Form1.Portadas.cs, PrecargaPaginaGpu.cs) y, la de la capsula, RenderContentGpu (DiscoVivoGpu.cs): el lienzo del Graphics (con
//     su matriz y su recorte) y su estado; despues, lo que XP le deja puesto al Graphics (el suavizado AntiAlias, con las mismas condiciones).
//   - PastillaView.Ancho con una FontFamily (Form1.cs, Form1.Datos.cs): la familia por su nombre.
//   (AnchoTexto de Form1.Ajustes.cs y SueltaBitmaps de Form1.Pared.cs, ya traducidas, se llaman directo: se quitaron AnchoTextoVistasB,
//   AnchoTextoPantallaViva -la de la Letra- y SueltaMapasVistasB; port nativo, grupo directas-vistas, PORT_directas-vistas.md.)
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ---- LAS FIRMAS DE XP DE RenderContent (con un Graphics). Cada una pasa a la traducida el lienzo del Graphics y lo que pesa de su
        // estado, y despues le pone al Graphics lo que XP le deja puesto: SmoothingMode = AntiAlias, si la vista llego a ponerlo (las mismas
        // cuentas de sus primeras lineas). Lo demas que las vistas cambian (el filtro de las portaditas, los Save/Restore) lo devuelven ellas.
        private partial class HeaderView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio);
                if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
        }
        private partial class PastillaView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio);
                if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
#if ROCOLA_GPU
            // RenderContentGpu (DiscoVivoGpu.cs: pone la bandera de la llegada y llama a RenderContent), para DrawCtrlOverlay (Form1.Video.cs)
            public void RenderContentGpu(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContentGpu(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio);
                if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
#endif
            // Ancho con la FontFamily de la letra del Form (Form1.cs, Form1.Datos.cs): la familia por su nombre; la nula es la generica sin
            // remates, como la toma new Font(null, ...) de la capa
            public static int Ancho(string etiq, string valor, int alto, FontFamily fam, bool conPortadas)
            { return Ancho(etiq, valor, alto, fam != null ? fam.Name : Letras.SinRemates, conPortadas); }
        }
        private partial class GeneroView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio);
                if (!(Width < 6 || Height < 6 || texto.Length == 0)) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
        }
        private partial class ProgressView
        {
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Copia, e.Medio);
                g.SmoothingMode = SmoothingMode.AntiAlias;   // (lo pone antes de mirar su medida)
            }
        }
        private partial class SongListView
        {
            // (no le cambia nada al Graphics: copia la pagina horneada, que pinta PintaPagina en su propio lienzo)
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Copia, e.Filtro, e.Medio);
            }
        }
    }
}
