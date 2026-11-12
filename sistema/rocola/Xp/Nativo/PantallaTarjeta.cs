// LO QUE LA TARJETA LE PIDE A LA PANTALLA (port nativo de XP): la unica costura entre el dispositivo de la tarjeta y la pantalla. Desde la
// tanda 4 (grupo pantalla-nativa, W2) no conoce las ventanas de la capa: se lo pide todo a la pantalla nativa (PantallaRocola.Vistas.cs),
// que mientras dure la transicion se lo pasa a la capa (el panel de la tarjeta aun es un control de la capa).
//   - Presenta: lo que en XP hacia Present (Direct3D 9 ponia el fondo en la ventana): el fondo del dispositivo (su textura) se le da a la
//     capa de la tarjeta de esa ventana y la pantalla compone con el y la pone (KMS: el cambio de pagina). Sin ventana (las pruebas),
//     glFlush.
//   - Generacion y Tocada: OpenGL es uno para todos (el compositor, el video de libmpv, la flecha, cada dispositivo); quien lo toca sube
//     la generacion y el dispositivo, al empezar su cuadro, vuelve a poner su estado si cambio (InitStates).
//   - RefrescoHz: las veces por segundo que refresca la pantalla (lo que XP preguntaba con GetDeviceCaps(VREFRESH)): el del modo de KMS
//     o 60 fuera de pantalla.
//   - AhoraMs: el reloj de la pantalla (para la traza de los dibujos, ROCOLA_TRAZA_DIBUJOS).
#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal static class PantallaTarjeta
    {
        internal static int Generacion;
        internal static void Tocada() { unchecked { Generacion++; } }
        internal static int RefrescoHz { get { return PantallaRocola.RefrescoHz; } }
        internal static double AhoraMs { get { return PantallaRocola.AhoraMs; } }

        internal static void Presenta(IntPtr ventana, uint textura, int ancho, int alto, bool sinEspera)
        {
            if (PantallaRocola.PresentaTarjeta(ventana, textura, ancho, alto, sinEspera)) Tocada();   // (sin ventana, solo glFlush: OpenGL sigue siendo lo del dispositivo)
        }
        // el dispositivo se suelta: la ventana ya no tiene fondo que componer
        internal static void Olvida(IntPtr ventana) { PantallaRocola.OlvidaTarjeta(ventana); }
    }
}
#endif
