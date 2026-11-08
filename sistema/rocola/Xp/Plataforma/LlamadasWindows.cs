// LO QUE LA ROCOLA DE XP LE PREGUNTA A WINDOWS (user32, kernel32, winmm...), CONTESTADO POR LINUX (traduccion de XP, fase 1). En cada
// archivo de XP, la declaracion de la llamada de Windows ([DllImport("user32.dll")] ... extern) se cambia por un metodo con el MISMO
// nombre y los mismos parametros que hace lo equivalente en Linux, casi siempre llamando a algo de aqui. Asi el resto del codigo de XP
// queda igual.
//
// LA LISTA (revisada el 7-11-2026: cada llamada de XP a Windows y lo que contesta en Linux)
//  Con su version de Linux:
//   - GetKeyState / GetAsyncKeyState (Form1, AsistenteRocola): las teclas del escritorio (evdev).
//   - GlobalMemoryStatusEx (Form1, CancionEnMemoria, MedidasBitacora, DiagnosticoCompleto): /proc/meminfo; y las direcciones del programa
//     (direcciones_libres de la bitacora), de /proc/self/status.
//   - FlushFileBuffers (Form1.Datos, DiarioTablas, Form1.Ia): fsync.
//   - GetDeviceCaps(VREFRESH) (D3D9Star): el refresco del modo de la pantalla (el anfitrion).
//   - EnumDisplayDevices (DiagnosticoCompleto): el adaptador, de OpenGL y de /sys/class/drm.
//   - SetPriorityClass (Program, por Process.PriorityClass): el nice de todos los hilos del proceso.
//   - GetForegroundWindow / SetForegroundWindow / SetFocus (Form1): las ventanas de la rocola.
//   - WMPlayer.OCX (Form1.Biblioteca, Form1.Video): el reproductor de libmpv (VideoMpv.cs).
//   - waveOut* (SalidaPropia) y el volumen (VolumenWindows): ALSA (SonidoAlsa.cs, amixer). Direct3D 9: OpenGL ES 2 (Direct3DaGL.cs).
//   - GetSystemTimes, OpenThread/GetThreadTimes, GetCurrentThreadId (DiagnosticoCompleto): XP ya lee /proc en Linux (HilosRocola.EsUnix:
//     syscall gettid, /proc/stat, /proc/self/task/N/stat), asi que esas declaraciones de Windows no se llaman nunca aqui.
//  Que en Linux no hacen falta (devuelven un valor fijo o nada, a proposito):
//   - timeBeginPeriod / timeEndPeriod (Form1): en XP piden el reloj de 1 ms (sin eso, Sleep y los temporizadores van de 15,6 en 15,6 ms).
//     Linux ya despierta a tiempo (holgura de 50 microsegundos; medido con rocola-xp --prueba-aot en la maquina de pruebas, cargada:
//     Sleep(1) = 1,1 a 1,2 ms de media), no hay nada que pedir.
//   - GetWindowThreadProcessId / AttachThreadInput / mouse_event (Form1) y ShowWindow / SetForegroundWindow (Program): para quitarle el
//     foco a otro programa o traer al frente la otra rocola. En Rocola OS no hay otros programas con ventana.
//   - GetDC / ReleaseDC (D3D9Star): solo para preguntar el refresco (arriba).
//   - DwmGetCompositionTimingInfo (RelojPantallaGpu, dwmapi.dll): Windows XP no tiene DWM (es de Vista), asi que en el A4 tampoco
//     contesta y XP sigue con su reloj; en Linux falla igual (no hay dwmapi) y hace lo mismo que en el A4.
//   - El registro de Windows (advapi32 en Form1.Ajustes; Microsoft.Win32.Registry en Form1 y DiagnosticoCompleto): las salidas de sonido
//     de Vista (XP no las tiene: lista vacia, como en el A4) y el nombre y los MHz del procesador. En Linux no hay registro: el nombre lo
//     lee XP de /proc/cpuinfo (el mismo texto del procesador); los MHz no salen en el diagnostico (en el A4 con XP, si).
//   - FindFirstFileW (RecorridoRapido): XP solo lo usa con .NET 2 (Environment.Version < 4); con .NET 10 recorre las carpetas con .NET.
using System;
using System.Diagnostics.CodeAnalysis;
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

        // GlobalMemoryStatusEx, las direcciones del programa (ullTotalVirtual, ullAvailVirtual): en XP, los 2 GB de un programa de 32 bits y
        // lo que le queda libre (la bitacora lo apunta en cada parada de la pantalla: direcciones_libres). En Linux de 64 bits, la mitad de
        // abajo del espacio de direcciones (47 bits, 128 TB: el A4 no tiene paginas de 5 niveles) menos lo que el programa ya tiene apartado
        // (VmSize de /proc/self/status, que cuenta tambien lo reservado sin usar, como Windows). Sin /proc, todo libre (lo de antes).
        internal static bool Direcciones(out ulong total, out ulong libres)
        {
            total = 1UL << 47; libres = total;
            try
            {
                foreach (string l in File.ReadAllLines("/proc/self/status"))
                    if (l.StartsWith("VmSize:")) { ulong v = Kb(l); libres = v < total ? total - v : 0; return true; }
            }
            catch { }
            return false;
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

        // EnumDisplayDevices (user32): el adaptador de la pantalla principal. En XP, la cadena del controlador de la tarjeta (la que se ve en
        // el Administrador de dispositivos); en Linux, el nombre que da OpenGL (GL_RENDERER, el de Mesa: «AMD ARUBA (DRM 2.50.0 / 6.12...)»
        // con r600, «llvmpipe (...)» sin tarjeta) y, entre parentesis, el controlador del nucleo con el numero PCI de la tarjeta
        // (/sys/class/drm). OpenGL solo se pregunta desde el hilo que tiene el contexto (el de la pantalla, que es el de las ventanas de XP) y lo
        // que dice se recuerda. Si no se sabe nada, false (XP escribe «el sistema no dijo el nombre del adaptador», como antes).
        [DllImport("libEGL.so.1")] private static extern IntPtr eglGetCurrentContext();
        private static string rendererGl;
        internal static bool Adaptador(out string nombre, out string conector, out string id)
        {
            string gl = rendererGl;
            if (gl == null)
            {
                try
                {
                    if (eglGetCurrentContext() != IntPtr.Zero)
                    {
                        string r = Rocola.Pantalla.GL.Texto(Rocola.Pantalla.GL.RENDERER);
                        if (!string.IsNullOrEmpty(r)) rendererGl = gl = r.Trim();
                    }
                }
                catch { }
            }
            string controlador, pci;
            TarjetaDrm("/sys/class/drm", out controlador, out pci, out conector);
            id = pci.Length > 0 ? "PCI " + pci : "";
            string drm = controlador.Length > 0 ? controlador + (pci.Length > 0 ? ", PCI " + pci : "") : "";
            nombre = gl ?? "";
            if (drm.Length > 0) nombre = nombre.Length > 0 ? nombre + " (" + drm + ")" : drm;
            return nombre.Length > 0;
        }

        // La tarjeta de /sys/class/drm (o de otra carpeta igual, para las pruebas): cardN con su controlador (DRIVER de device/uevent) y su
        // numero PCI (PCI_ID), y el primer conector enchufado (cardN-HDMI-A-1 con status «connected»). Si hay varias, la que tiene algo
        // enchufado.
        internal static bool TarjetaDrm(string raiz, out string controlador, out string pci, out string conector)
        {
            controlador = pci = conector = "";
            try
            {
                if (!Directory.Exists(raiz)) return false;
                string[] todo = Directory.GetFileSystemEntries(raiz);
                Array.Sort(todo, StringComparer.Ordinal);
                bool hallada = false;
                foreach (string ruta in todo)
                {
                    string n = Path.GetFileName(ruta);
                    if (n.Length < 5 || !n.StartsWith("card") || n.IndexOf('-') >= 0) continue;
                    bool numero = true; for (int i = 4; i < n.Length; i++) if (n[i] < '0' || n[i] > '9') numero = false;
                    if (!numero) continue;
                    string drv = "", id = "", con = "";
                    try
                    {
                        foreach (string l in File.ReadAllLines(Path.Combine(ruta, "device", "uevent")))
                        {
                            if (l.StartsWith("DRIVER=")) drv = l.Substring(7).Trim();
                            else if (l.StartsWith("PCI_ID=")) id = l.Substring(7).Trim();
                        }
                    }
                    catch { }
                    foreach (string c in todo)
                    {
                        string cn = Path.GetFileName(c);
                        if (!cn.StartsWith(n + "-")) continue;
                        try { if (File.ReadAllText(Path.Combine(c, "status")).Trim() == "connected") { con = cn.Substring(n.Length + 1); break; } }
                        catch { }
                    }
                    if (drv.Length == 0 && id.Length == 0) continue;
                    if (!hallada || (conector.Length == 0 && con.Length > 0)) { controlador = drv; pci = id; conector = con; hallada = true; }
                }
                return hallada;
            }
            catch { return false; }
        }

        // SetPriorityClass (XP: Process.PriorityClass = AboveNormal, en Program.PrioridadDeLaRocola). En Windows la clase de prioridad es de
        // TODO el proceso: sube o baja la base de todos sus hilos, los de ahora y los que nazcan, y cada uno conserva su diferencia. En Linux la
        // prioridad (el nice) es de cada hilo, y .NET la traduce con setpriority del pid, que solo cambia el hilo principal: los que ya
        // existian (los de .NET y los de Mesa, que el anfitrion abre antes del Main de XP) se quedaban en 0 (medido: llvmpipe-0..3 en nice 0
        // con el principal en -6). Aqui el principal va al nice de la clase, con la misma tabla que .NET (tiempo real -19, alta -11, por
        // encima de lo normal -6, normal 0, por debajo de lo normal 10, inactiva 19), y cada uno de los demas hilos de /proc/self/task se
        // mueve lo mismo que el (el de la cache de sombreadores de Mesa, que Mesa pone en 19 a proposito, queda en 13); los que nazcan despues
        // lo heredan del hilo que los crea. Subir la prioridad (nice negativo) pide root o CAP_SYS_NICE (la rocola de Rocola OS corre como
        // root): si el principal no se puede cambiar, lanza Win32Exception como .NET y XP sigue como estaba. (Diferencia que queda: en Linux
        // los programas que se lanzan heredan el nice del hilo que los lanza; en Windows arrancan en la normal.)
        [DllImport("libc", SetLastError = true)] private static extern int setpriority(int cual, uint quien, int nice);
        private const int PrioProceso = 0;   // PRIO_PROCESS: con el numero de un hilo, ese hilo
        internal static int NiceDelHilo(uint tid)   // (campo 19 de /proc/self/task/N/stat; int.MinValue si no se pudo)
        {
            try
            {
                string st = File.ReadAllText("/proc/self/task/" + tid + "/stat");
                string[] p = st.Substring(st.LastIndexOf(')') + 2).Split(' ');
                return int.Parse(p[16]);
            }
            catch { return int.MinValue; }
        }
        internal static int NiceDe(System.Diagnostics.ProcessPriorityClass clase)
        {
            switch (clase)
            {
                case System.Diagnostics.ProcessPriorityClass.RealTime: return -19;
                case System.Diagnostics.ProcessPriorityClass.High: return -11;
                case System.Diagnostics.ProcessPriorityClass.AboveNormal: return -6;
                case System.Diagnostics.ProcessPriorityClass.BelowNormal: return 10;
                case System.Diagnostics.ProcessPriorityClass.Idle: return 19;
                default: return 0;
            }
        }
        internal static void PrioridadDelProceso(System.Diagnostics.ProcessPriorityClass clase)
        {
            int nice = NiceDe(clase);
            uint yo = (uint)Environment.ProcessId;
            int antes = NiceDelHilo(yo);
            if (setpriority(PrioProceso, yo, nice) != 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
            if (antes == int.MinValue || antes == nice) return;
            string[] hilos;
            try { hilos = Directory.GetDirectories("/proc/self/task"); } catch { return; }
            foreach (string h in hilos)
            {
                uint tid; int n;
                if (!uint.TryParse(Path.GetFileName(h), out tid) || tid == yo || (n = NiceDelHilo(tid)) == int.MinValue) continue;   // (uno que ya acabo)
                setpriority(PrioProceso, tid, Math.Max(-20, Math.Min(19, n + nice - antes)));
            }
        }

        // Type.GetTypeFromProgID del reproductor de Windows Media (el medidor de duraciones de los videos): el de libmpv (VideoMpv.cs). Con su
        // constructor sin parametros marcado para NativeAOT: XP lo crea con Activator.CreateInstance (Form1.MideDuracionVideo).
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
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
