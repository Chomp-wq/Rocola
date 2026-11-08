// LO QUE LA TARJETA LE PIDE A LA PANTALLA (port nativo de XP): la unica costura entre el dispositivo de la tarjeta y el escritorio de
// hoy (Plataforma: Anfitrion, Escritorio, el control del panel). Cuando la pista de las ventanas cambie el escritorio por su bucle y su
// compositor, solo cambia este archivo.
//   - Presenta: lo que en XP hacia Present (Direct3D 9 ponia el fondo en la ventana): el fondo del dispositivo (su textura) se le da al
//     control de la ventana y el anfitrion compone la pantalla con el y la pone (KMS: el cambio de pagina). Sin ventana (las pruebas),
//     glFlush.
//   - Generacion y Tocada: OpenGL es uno para todos (el compositor, el video de libmpv, la flecha, cada dispositivo); quien lo toca sube
//     la generacion y el dispositivo, al empezar su cuadro, vuelve a poner su estado si cambio (InitStates).
//   - RefrescoHz: las veces por segundo que refresca la pantalla (lo que XP preguntaba con GetDeviceCaps(VREFRESH)): el del modo de KMS
//     o 60 fuera de pantalla.
//   - AhoraMs: el reloj del escritorio (para la traza de los dibujos, ROCOLA_TRAZA_DIBUJOS).
#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal static class PantallaTarjeta
    {
        internal static int Generacion;
        internal static void Tocada() { unchecked { Generacion++; } }
        internal static int RefrescoHz { get { return Escritorio.RefrescoHz; } }
        internal static double AhoraMs { get { return Escritorio.AhoraMs; } }

        internal static void Presenta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera)
        {
            Control control = Control.FromHandle(ventana);
            if (control == null) { GlMas.Flush(); return; }
            control.TexturaGl = textura; control.AnchoGl = ancho; control.AltoGl = alto;
            Escritorio.SinEspera = sinEspera;
            Escritorio.PresentaNativa(control);
            Tocada();
        }
        // el dispositivo se suelta: el control ya no tiene fondo que componer
        internal static void Olvida(IntPtr ventana)
        {
            Control control = Control.FromHandle(ventana);
            if (control != null) control.TexturaGl = 0;
        }
    }
}
#endif
