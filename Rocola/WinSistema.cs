// ====================================================================================================
//  LAS DOS COSAS QUE EL DIBUJO LE PREGUNTA AL SISTEMA
// ----------------------------------------------------------------------------------------------------
//  El fondo por tarjeta grafica (Rocola/D3D9Star.cs) necesita saber dos cosas que no son suyas:
//
//      cuantas veces por segundo refresca la pantalla   -> para no dibujar cuadros que nadie va a ver
//      si hay algo esperando a que lo atiendan          -> para dibujar en los ratos muertos y salir
//                                                          en cuanto llegue una tecla
//
//  Las dos se preguntan distinto en cada sistema, asi que viven aqui y no en el archivo del dibujo.
//  Ese archivo pasa a ser EL MISMO en esta rama y en la de Linux, que es lo que se buscaba: las dos
//  mil lineas del dibujo -las que de verdad importan- se cambian en un sitio y se llevan de una copia.
//  (En la rama de Windows XP el dibujo sigue en un solo archivo con todo dentro, a proposito: esa es
//  la rocola que esta en el bar y no se toca.)
//
//  El codigo de aqui es EL MISMO de siempre, solo que mudado: GetDC + GetDeviceCaps para el refresco,
//  y PeekMessage para los mensajes.
//
//  Todo dentro de #if ROCOLA_GPU, que es lo unico que lo usa. Con el simbolo apagado compila vacio.
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static class Sistema
    {
        // ---------- cuantas veces por segundo refresca la pantalla ----------
        // Windows contesta 0 o 1 cuando no lo sabe (pantalla vieja, escritorio remoto, maquina virtual);
        // quien llama toma 60 en ese caso, que es lo de siempre.
        private const int VREFRESH = 116;                 // el numero con el que GetDeviceCaps lo pregunta
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);

        public static int RefrescoPantallaHz()
        {
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) return 0;
                int hz = GetDeviceCaps(dc, VREFRESH);
                ReleaseDC(IntPtr.Zero, dc);
                return hz;
            }
            catch { return 0; }
        }

        // ---------- hay algo esperando a que lo atiendan? ----------
        // El fondo por tarjeta dibuja en los ratos muertos: mientras no haya nada que atender sigue
        // sacando cuadros, y en cuanto llega algo -una tecla, un temporizador- sale a atenderlo. Se
        // pregunta con PeekMessage sin sacar el mensaje de la cola (PM_NOREMOVE = 0).
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage { public IntPtr Hwnd; public uint Msg; public IntPtr W; public IntPtr L; public uint Time; public Point Pt; }
        [DllImport("user32.dll")] private static extern int PeekMessage(out NativeMessage msg, IntPtr hWnd, uint min, uint max, uint remove);

        public static bool SinNadaQueAtender()
        {
            try { NativeMessage m; return PeekMessage(out m, IntPtr.Zero, 0, 0, 0) == 0; }
            catch { return false; }   // si no se pudo preguntar, mejor salir a atender que quedarse dentro
        }
    }
}
#endif
