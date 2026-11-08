// LO QUE LA ROCOLA DE XP LE PREGUNTA A WINDOWS (user32, kernel32, winmm...), CONTESTADO POR LINUX (traduccion de XP, fase 1). En cada
// archivo de XP, la declaracion de la llamada de Windows ([DllImport("user32.dll")] ... extern) se cambia por un metodo con el MISMO
// nombre y los mismos parametros que hace lo equivalente en Linux, casi siempre llamando a algo de aqui. Asi el resto del codigo de XP
// queda igual.
//
// LA LISTA (8-11-2026, port nativo): lo que queda aqui es de otras pistas del port (el foco y el teclado, la pantalla), hasta que lo
// traduzcan:
//   - GetKeyState / GetAsyncKeyState (Form1, AsistenteRocola): las teclas del escritorio (evdev).
//   - GetDeviceCaps(VREFRESH) (D3D9Star): el refresco del modo de la pantalla (el anfitrion).
//   - GetForegroundWindow / SetForegroundWindow / SetFocus (Form1): las ventanas de la rocola.
// Lo demas ya lo llama el codigo de XP directo a Linux (pista windows, Xp/PORT_WINDOWS.md): la memoria, el procesador y el adaptador
// (Nativo/Equipo.cs), fsync (FileStream.Flush(true)), las prioridades (Nativo/PrioridadHilo.cs), el sonido (Nativo/SalidaAlsa.cs), el video
// (Nativo/ReproductorVideo.cs y MedidorVideo.cs), y sin nada que hacer en Linux: timeBeginPeriod/timeEndPeriod, el registro, FindFirstFile y
// los tiempos de kernel32 del diagnostico (su camino de Unix, /proc).
using System;

namespace Rocola
{
    internal static class LlamadasWindows
    {
        // GetKeyState / GetAsyncKeyState: el bit alto (0x8000) si la tecla esta abajo AHORA. Las teclas las lleva el escritorio (evdev).
        internal static short TeclaAbajo(int vk) { return Escritorio.TeclasAbajo[vk & 0xFF] ? unchecked((short)0x8000) : (short)0; }

        // GetDeviceCaps de la pantalla (gdi32): VREFRESH (116), las veces por segundo que refresca; lo dice el anfitrion (KMS: el modo
        // puesto). Lo demas no se pregunta.
        internal static int CapacidadPantalla(int que) { return que == 116 ? Escritorio.RefrescoHz : 0; }

        // La ventana de delante: la activa del escritorio (en Rocola OS no hay otros programas con ventana)
        internal static IntPtr VentanaDelante() { Form f = Form.ActiveForm; return f != null && !f.IsDisposed ? f.Handle : IntPtr.Zero; }
        internal static bool PonDelante(IntPtr h) { Form f = Control.FromHandle(h) as Form; if (f == null || f.IsDisposed) return false; f.Activate(); return true; }
        internal static IntPtr PonFoco(IntPtr h) { Control c = Control.FromHandle(h); if (c != null && !c.IsDisposed) c.Focus(); return IntPtr.Zero; }
    }
}
