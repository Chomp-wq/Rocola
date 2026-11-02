// EL WINDOWS DE MENTIRA. La rocola de XP llama a Windows (user32, kernel32, winmm, gdi32, d3d9...) con DllImport. En Rocola OS esas
// llamadas se resuelven contra el propio programa: las que hacen falta estan aqui (y Direct3DCreate9 en D3d9.cs) exportadas con su
// nombre de Windows; las que no estan dan EntryPointNotFoundException, igual que en Mono, donde la rocola ya sabe seguir sin ellas.
// Ademas se pone lo que la rocola deja enchufar (la salida de sonido por ALSA, el video) y se abre la pantalla, antes de que arranque
// (el inicializador del modulo corre antes que Program.Main de XP).
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Rocola.Linux
{
    internal static unsafe class Enlaces
    {
        [ModuleInitializer]
        internal static void Inicia()
        {
            NativeLibrary.SetDllImportResolver(typeof(Enlaces).Assembly, Resuelve);
            try { NativeLibrary.SetDllImportResolver(typeof(NAudio.Wave.WaveFormat).Assembly, Resuelve); } catch { }
            WaveOutPropio.Sistema = delegate { return new SalidaAlsa(); };
            DescompresorMp3.Crea = delegate (NAudio.Wave.WaveFormat f) { return new Mp3EnteroNLayer(f); };
            try { Pantalla.Inicia(); }
            catch (Exception ex) { Console.Error.WriteLine("[Rocola OS] sin pantalla grafica: " + ex.Message); }
            Teclado.Inicia();
            Pruebas.Inicia();
        }
        private static IntPtr Resuelve(string nombre, Assembly a, DllImportSearchPath? ruta)
        {
            string n = nombre.ToLowerInvariant();
            if (n.EndsWith(".dll")) n = n.Substring(0, n.Length - 4);
            switch (n)
            {
                case "d3d9": case "user32": case "kernel32": case "winmm": case "gdi32": case "dwmapi": case "advapi32": case "msacm32": case "shell32": case "ole32":
                    return NativeLibrary.GetMainProgramHandle();
            }
            return IntPtr.Zero;
        }

        // ---------------- user32 ----------------
        [UnmanagedCallersOnly(EntryPoint = "PeekMessage")] private static int PeekMessage(void* m, IntPtr h, uint min, uint max, uint quita) { return Escritorio.HayMensajes() ? 1 : 0; }
        [UnmanagedCallersOnly(EntryPoint = "MsgWaitForMultipleObjectsEx")]
        private static uint MsgWaitForMultipleObjectsEx(uint n, IntPtr asas, uint ms, uint mascara, uint banderas) { return Escritorio.EsperaMensajes((int)Math.Min(ms, 1000u)) ? n : 258u; }
        [UnmanagedCallersOnly(EntryPoint = "GetDC")] private static IntPtr GetDC(IntPtr h) { return (IntPtr)1; }
        [UnmanagedCallersOnly(EntryPoint = "ReleaseDC")] private static int ReleaseDC(IntPtr h, IntPtr dc) { return 1; }
        [UnmanagedCallersOnly(EntryPoint = "GetForegroundWindow")] private static IntPtr GetForegroundWindow() { Form f = Form.ActiveForm; return f != null && f.IsHandleCreated ? f.Handle : IntPtr.Zero; }
        [UnmanagedCallersOnly(EntryPoint = "SetForegroundWindow")] private static int SetForegroundWindow(IntPtr h) { return 1; }
        [UnmanagedCallersOnly(EntryPoint = "GetWindowThreadProcessId")] private static uint GetWindowThreadProcessId(IntPtr h, IntPtr proceso) { return HiloActual(); }
        [UnmanagedCallersOnly(EntryPoint = "AttachThreadInput")] private static int AttachThreadInput(uint a, uint b, int si) { return 1; }
        [UnmanagedCallersOnly(EntryPoint = "SetFocus")] private static IntPtr SetFocus(IntPtr h) { return h; }
        [UnmanagedCallersOnly(EntryPoint = "ShowWindow")] private static int ShowWindow(IntPtr h, int como) { return 1; }
        [UnmanagedCallersOnly(EntryPoint = "GetKeyState")] private static short GetKeyState(int vk) { return vk >= 0 && vk < 256 && Escritorio.TeclasAbajo[vk] ? unchecked((short)0x8000) : (short)0; }
        [UnmanagedCallersOnly(EntryPoint = "GetAsyncKeyState")] private static short GetAsyncKeyState(int vk) { return vk >= 0 && vk < 256 && Escritorio.TeclasAbajo[vk] ? unchecked((short)0x8000) : (short)0; }
        [UnmanagedCallersOnly(EntryPoint = "mouse_event")] private static void mouse_event(uint a, uint b, uint c, uint d, UIntPtr e) { }

        // ---------------- gdi32 ----------------
        [UnmanagedCallersOnly(EntryPoint = "GetDeviceCaps")]
        private static int GetDeviceCaps(IntPtr dc, int i)
        {
            switch (i)
            {
                case 8: return Escritorio.Pantalla.Width;    // HORZRES
                case 10: return Escritorio.Pantalla.Height;  // VERTRES
                case 12: return 32;                          // BITSPIXEL
                case 88: case 90: return 96;                 // LOGPIXELSX/Y
                case 116: return Refresco;                   // VREFRESH
                default: return 0;
            }
        }
        internal static int Refresco = 60;

        // ---------------- kernel32 ----------------
        [UnmanagedCallersOnly(EntryPoint = "RtlMoveMemory")] private static void RtlMoveMemory(IntPtr a, IntPtr de, UIntPtr n) { Buffer.MemoryCopy((void*)de, (void*)a, (long)n, (long)n); }
        [UnmanagedCallersOnly(EntryPoint = "GetCurrentThreadId")] private static uint GetCurrentThreadId() { return HiloActual(); }
        [DllImport("libc", EntryPoint = "syscall")] private static extern long Syscall(long n);
        private static uint HiloActual() { try { return (uint)Syscall(186); } catch { return (uint)Environment.CurrentManagedThreadId; } }   // gettid
        [DllImport("libc", EntryPoint = "fsync")] private static extern int Fsync(int fd);
        [UnmanagedCallersOnly(EntryPoint = "FlushFileBuffers")] private static int FlushFileBuffers(IntPtr h) { return Fsync((int)(long)h) == 0 ? 1 : 0; }
        [UnmanagedCallersOnly(EntryPoint = "GlobalMemoryStatusEx")]
        private static int GlobalMemoryStatusEx(byte* m)
        {
            try
            {
                long total = 0, libre = 0, swapT = 0, swapL = 0;
                foreach (string l in File.ReadAllLines("/proc/meminfo"))
                {
                    string[] p = l.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 2) continue;
                    long v; if (!long.TryParse(p[1], out v)) continue; v *= 1024;
                    if (p[0] == "MemTotal") total = v; else if (p[0] == "MemAvailable") libre = v; else if (p[0] == "SwapTotal") swapT = v; else if (p[0] == "SwapFree") swapL = v;
                }
                *(uint*)(m + 4) = total > 0 ? (uint)(100 - libre * 100 / total) : 0;
                *(ulong*)(m + 8) = (ulong)total; *(ulong*)(m + 16) = (ulong)libre;
                *(ulong*)(m + 24) = (ulong)(total + swapT); *(ulong*)(m + 32) = (ulong)(libre + swapL);
                *(ulong*)(m + 40) = 0x7FFE0000UL; *(ulong*)(m + 48) = 0x7FFE0000UL; *(ulong*)(m + 56) = 0;
                return 1;
            }
            catch { return 0; }
        }

        // ---------------- winmm ----------------
        [UnmanagedCallersOnly(EntryPoint = "timeBeginPeriod")] private static uint timeBeginPeriod(uint p) { return 0; }
        [UnmanagedCallersOnly(EntryPoint = "timeEndPeriod")] private static uint timeEndPeriod(uint p) { return 0; }
    }
}
