// LA CAPA DEL VIDEO ENTRE LAS VENTANAS DE XP QUE AUN QUEDAN (port nativo, temporal: se va con Ventanas.*.cs).
//
// El reproductor de video ya es nativo (Nativo/ReproductorVideo.cs: libmpv, sin control ActiveX ni COM) y el codigo de XP lo llama directo.
// Pero la pantalla todavia la arma el escritorio que imita a WinForms (Ventanas.Escritorio.cs: las capas salen del orden de los controles
// de cada ventana) y en XP el video era un control mas de la ventana de la rocola: su orden frente al panel de la tarjeta, lblAttract,
// lblConfig y lblGpu lo decidian los BringToFront de XP. Para que ese orden sea EL MISMO mientras tanto, la capa del video ocupa un hueco
// en la lista de controles de la ventana (HuecoDelVideo, un control «Nativa» como el GpuPanel: el anfitrion pinta ahi la textura del video)
// y se mueve con ella: Pon (sitio y si se ve), AlFrente (BringToFront) y Quita (Controls.Remove). El codigo de XP no ve este hueco.
// Cuando la pantalla sea nativa (la traduccion de las ventanas), su lista de lo que va encima del panel (video, lblAttract, lblConfig,
// lblGpu) toma el lugar de esto y este archivo se borra.
using System.Drawing;

namespace Rocola
{
    internal sealed class VideoEnVentanas : IPantallaDelVideo
    {
        public ILugarDelVideo Lugar(object ventana)
        {
            HuecoDelVideo h = new HuecoDelVideo();
            Control dueno = ventana as Control;
            if (dueno != null) dueno.Controls.Add(h);   // al fondo de la lista, como Controls.Add del control del WMP en XP
            return h;
        }
        public void Despierta() { Escritorio.Despierta(); }
        public void OpenGlTocado() { PantallaTarjeta.Tocada(); }   // lo puesto en OpenGL ya no es lo del dispositivo de la tarjeta

        private sealed class HuecoDelVideo : Control, ILugarDelVideo
        {
            internal HuecoDelVideo() { Nativa = true; TexturaGlRgba = true; TabStop = false; }
            public void Pon(Rectangle sitio, bool visible)
            {
                if (Bounds != sitio) SetBounds(sitio.X, sitio.Y, sitio.Width, sitio.Height);
                if (VisiblePropio != visible) Visible = visible;
            }
            public void AlFrente() { BringToFront(); }
            public void Textura(uint textura, int ancho, int alto) { TexturaGl = textura; AnchoGl = ancho; AltoGl = alto; TexturaGlRgba = true; }
            public void Quita()
            {
                Control p = Parent;
                if (p != null) p.Controls.Remove(this);
                TexturaGl = 0;
                Dispose();
            }
        }
    }
}
