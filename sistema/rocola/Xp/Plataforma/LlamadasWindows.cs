// LO QUE LA ROCOLA DE XP LE PREGUNTA A WINDOWS (user32, kernel32, winmm...), CONTESTADO POR LINUX (traduccion de XP, fase 1). En cada
// archivo de XP, la declaracion de la llamada de Windows ([DllImport("user32.dll")] ... extern) se cambia por un metodo con el MISMO
// nombre y los mismos parametros que hace lo equivalente en Linux, casi siempre llamando a algo de aqui. Asi el resto del codigo de XP
// queda igual.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static class LlamadasWindows
    {
        // GetKeyState / GetAsyncKeyState: el bit alto (0x8000) si la tecla esta abajo AHORA. Las teclas las lleva el escritorio (evdev).
        internal static short TeclaAbajo(int vk) { return Escritorio.TeclasAbajo[vk & 0xFF] ? unchecked((short)0x8000) : (short)0; }

        // GlobalMemoryStatusEx: la memoria del equipo, de /proc/meminfo (MemTotal y MemAvailable, en kB). El uso, en % como Windows.
        internal static bool Memoria(out ulong total, out ulong disponible, out uint usoPct)
        {
            total = disponible = 0; usoPct = 0;
            try
            {
                foreach (string l in File.ReadAllLines("/proc/meminfo"))
                {
                    if (l.StartsWith("MemTotal:")) total = Kb(l);
                    else if (l.StartsWith("MemAvailable:")) disponible = Kb(l);
                }
                if (total == 0) return false;
                usoPct = (uint)(100 - disponible * 100 / total);
                return true;
            }
            catch { return false; }
        }
        private static ulong Kb(string l)
        {
            string[] p = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            ulong v; return p.Length >= 2 && ulong.TryParse(p[1], out v) ? v * 1024UL : 0;
        }

        // FlushFileBuffers: que lo escrito llegue al disco de verdad (fsync del descriptor)
        [DllImport("libc", SetLastError = true)] private static extern int fsync(int fd);
        internal static bool AlDisco(IntPtr manejador)
        {
            try { return fsync(manejador.ToInt32()) == 0; } catch { return false; }
        }

        // GetDeviceCaps de la pantalla (gdi32): VREFRESH (116), las veces por segundo que refresca; lo dice el anfitrion (KMS: el modo
        // puesto). Lo demas no se pregunta.
        internal static int CapacidadPantalla(int que) { return que == 116 ? Escritorio.RefrescoHz : 0; }

        // Type.GetTypeFromProgID del reproductor de Windows Media (el medidor de duraciones de los videos): el de libmpv (VideoMpv.cs)
        internal static Type TipoDeProgId(string progId)
        {
            if (progId != null && progId.StartsWith("WMPlayer.OCX", StringComparison.OrdinalIgnoreCase)) return typeof(ReproductorWmp);
            return null;
        }

        // La ventana de delante: la activa del escritorio (en Rocola OS no hay otros programas con ventana)
        internal static IntPtr VentanaDelante() { Form f = Form.ActiveForm; return f != null && !f.IsDisposed ? f.Handle : IntPtr.Zero; }
        internal static bool PonDelante(IntPtr h) { Form f = Control.FromHandle(h) as Form; if (f == null || f.IsDisposed) return false; f.Activate(); return true; }
        internal static IntPtr PonFoco(IntPtr h) { Control c = Control.FromHandle(h); if (c != null && !c.IsDisposed) c.Focus(); return IntPtr.Zero; }
    }
}
