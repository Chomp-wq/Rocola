// EL VIDEO EN LA PANTALLA NATIVA (port nativo, tanda 4, grupo pantalla-nativa: W2): lo que el reproductor de video (ReproductorVideo.cs,
// libmpv) le pide a quien compone la pantalla (IPantallaDelVideo). En XP el video era un control mas de la ventana de la rocola (el del
// WMP, con Controls.Add al fondo de la lista): aqui su capa es una capa de textura de la lista de la ventana (PantallaRocola.Vistas.cs),
// que se mueve con ella: Pon (sitio y si se ve), AlFrente (BringToFront) y Quita (Controls.Remove); su orden frente al panel de la tarjeta
// y los rotulos de encima lo deciden los BringToFront de XP, como alli.
// TRANSICION: mientras la ventana de la rocola sea una ventana de la capa, su video va en el hueco de la capa (Plataforma/VideoEnVentanas.cs,
// por PantallaRocola.LaCapa), entre los controles que quedan, que es como se ve hoy.
using System.Drawing;

namespace Rocola
{
    internal sealed class VideoEnPantalla : IPantallaDelVideo
    {
        public ILugarDelVideo Lugar(object ventana)
        {
            PantallaRocola.ILaCapa c = PantallaRocola.LaCapa;
            if (c != null && c.EsSuya(ventana)) return c.LugarDelVideo(ventana);
            LugarNativo l = new LugarNativo();
            PantallaRocola.AgregaCapa(l.Capa);   // al fondo de la lista, como Controls.Add del control del WMP en XP
            return l;
        }
        public void Despierta() { PantallaRocola.Despierta(); }
        public void OpenGlTocado() { PantallaTarjeta.Tocada(); }   // lo puesto en OpenGL ya no es lo del dispositivo de la tarjeta

        private sealed class LugarNativo : ILugarDelVideo
        {
            internal readonly CapaDeTextura Capa = new CapaDeTextura { Rgba = true, Visible = true, Nombre = "Video" };
            public void Pon(Rectangle sitio, bool visible)
            {
                if (Capa.Sitio == sitio && Capa.Visible == visible) return;
                Capa.Sitio = sitio; Capa.Visible = visible;
                PantallaRocola.CambioVentana();
            }
            public void AlFrente() { PantallaRocola.VistaAlFrente(Capa); }
            public void Textura(uint textura, int ancho, int alto) { Capa.Textura = textura; Capa.AnchoGl = ancho; Capa.AltoGl = alto; Capa.Rgba = true; }
            public void Quita() { PantallaRocola.QuitaVista(Capa); Capa.Textura = 0; }
        }
    }
}
