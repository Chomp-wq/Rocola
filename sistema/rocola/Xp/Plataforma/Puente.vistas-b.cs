// EL PUENTE DEL GRUPO «VISTAS-B», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). La segunda mitad de
// Form1.Vistas.cs (HeaderView: el encabezado de la seccion; PastillaView: las capsulas de los creditos y de la cola; GeneroView: la pastilla
// del genero; ProgressView: la barra de avance; SongListView: la lista de canciones de un disco y sus hornos) dibuja con SkiaSharp directo y
// las funciones nativas (Xp/Nativo). Donde la llama codigo que todavia no esta traducido, pasa por aqui. La regla: el puente va del lado de lo
// que no esta traducido, y los que llaman no cambian. Nada de esto queda: se borra en cuanto se traduzca el archivo que lo necesita, y todo al
// final, con Dibujo2D.cs y Puente.cs (PORT_vistas-b.md dice cual es cual).
//   - PastillaView.Ancho con una FontFamily (Form1.cs, Form1.Datos.cs): la familia por su nombre. Se va con la Font del Form (oleada 7).
//   (Las firmas de XP de RenderContent con un Graphics de las cinco vistas -y RenderContentGpu(Graphics) de la capsula-, que pedian sus OnPaint,
//   se quitaron con la oleada 5, grupo vistas-disco: las vistas ya no heredan de Control y su Pinta llama directo a la traducida;
//   DrawCtrlOverlay, Form1.Video.cs, y EnSeco, Form1.Portadas.cs y PrecargaPaginaGpu.cs, ya la llamaban directo. PORT_vistas-disco.md.)
//   (AnchoTexto de Form1.Ajustes.cs y SueltaBitmaps de Form1.Pared.cs, ya traducidas, se llaman directo: se quitaron AnchoTextoVistasB,
//   AnchoTextoPantallaViva -la de la Letra- y SueltaMapasVistasB; port nativo, grupo directas-vistas, PORT_directas-vistas.md.)
using Rocola.Dibujo2D;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
        private partial class PastillaView
        {
            // Ancho con la FontFamily de la letra del Form (Form1.cs, Form1.Datos.cs): la familia por su nombre; la nula es la generica sin
            // remates, como la toma new Font(null, ...) de la capa
            public static int Ancho(string etiq, string valor, int alto, FontFamily fam, bool conPortadas)
            { return Ancho(etiq, valor, alto, fam != null ? fam.Name : Letras.SinRemates, conPortadas); }
        }
    }
}
