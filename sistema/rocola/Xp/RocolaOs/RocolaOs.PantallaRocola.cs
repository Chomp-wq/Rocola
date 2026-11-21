// ROCOLA OS (fase 11, la hoja Pantalla de Ctrl+F4): LA PANTALLA DE LA ROCOLA CAMBIA DE MEDIDA con la rocola andando (otro modo del monitor:
// RocolaOs/RocolaOs.Pantalla.cs, CambiaModoPantalla). Lo que en XP hace Windows al cambiar la resolucion (WM_DISPLAYCHANGE): la medida de la
// pantalla (Screen.PrimaryScreen.Bounds y el refresco de GetDeviceCaps), la ventana maximizada que toma la pantalla nueva (su OnResize: XP se
// reacomoda con LayoutControls, como con cualquier otra resolucion) y las ventanas que quedarian fuera, otra vez dentro (aqui las hojas se
// centran: todas las de la rocola son dialogos centrados en su duena, que es la pantalla entera). Solo lo llama Rocola OS: XP nunca cambia
// de modo, y sin ROCOLA_OS nada llama aqui.
using System;
using System.Drawing;

namespace Rocola
{
    internal static partial class PantallaRocola
    {
        internal static void RocolaOsCambioDeMedida(int ancho, int alto, int hz)
        {
            Ancho = ancho; Alto = alto;
            RefrescoHz = hz >= 24 && hz <= 400 ? hz : 60;
            if (Flecha.X >= ancho || Flecha.Y >= alto) FlechaEn(Math.Min(Flecha.X, ancho - 1), Math.Min(Flecha.Y, alto - 1));
            VentanaNativa w = ventana;
            if (w != null && w.Creada && !w.Desechada) w.Medida = new Size(ancho, alto);   // (la de la rocola va sin borde y maximizada)
            foreach (Hoja h in Hoja.Abiertas)
                if (!h.IsDisposed && h.Visible) h.Mueve(new Point(Math.Max(0, (ancho - h.Width) / 2), Math.Max(0, (alto - h.Height) / 2)));
            HayQueComponer();
        }
    }
}
