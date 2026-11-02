// LAS TECLAS Y LOS BOTONES. En XP los botones de la rocola (y el monedero) llegan como teclas de Windows. En Rocola OS llegan por evdev
// (/dev/input/event*: teclados y placas de botones USB). Aqui se leen, se traducen al codigo de Windows (la misma tabla que
// sistema/rocola/Motor/Botones.cs, asi rocola_teclas.txt del bar vale tal cual) y se entregan al escritorio como teclas, con su
// repeticion al dejarlas apretadas (como Windows).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Rocola.Linux
{
    internal static unsafe class Teclado
    {
        private static readonly Dictionary<int, int> aWindows = Tabla();
        private static Dictionary<int, int> Tabla()
        {
            Dictionary<int, int> t = new Dictionary<int, int>();
            string fila1 = "QWERTYUIOP", fila2 = "ASDFGHJKL", fila3 = "ZXCVBNM";
            for (int i = 0; i < fila1.Length; i++) t[16 + i] = fila1[i];
            for (int i = 0; i < fila2.Length; i++) t[30 + i] = fila2[i];
            for (int i = 0; i < fila3.Length; i++) t[44 + i] = fila3[i];
            t[2] = '1'; t[3] = '2'; t[4] = '3'; t[5] = '4'; t[6] = '5'; t[7] = '6'; t[8] = '7'; t[9] = '8'; t[10] = '9'; t[11] = '0';
            t[1] = 0x1B; t[14] = 0x08; t[15] = 0x09; t[28] = 0x0D; t[96] = 0x0D; t[57] = 0x20;
            t[29] = 0x11; t[97] = 0x11; t[42] = 0x10; t[54] = 0x10; t[56] = 0x12; t[100] = 0x12; t[58] = 0x14;
            t[103] = 0x26; t[108] = 0x28; t[105] = 0x25; t[106] = 0x27;
            t[102] = 0x24; t[107] = 0x23; t[104] = 0x21; t[109] = 0x22; t[110] = 0x2D; t[111] = 0x2E;
            for (int i = 0; i < 10; i++) t[59 + i] = 0x70 + i;
            t[87] = 0x7A; t[88] = 0x7B;
            t[82] = 0x60; t[79] = 0x61; t[80] = 0x62; t[81] = 0x63; t[75] = 0x64; t[76] = 0x65; t[77] = 0x66; t[71] = 0x67; t[72] = 0x68; t[73] = 0x69;
            t[55] = 0x6A; t[78] = 0x6B; t[74] = 0x6D; t[83] = 0x6E; t[98] = 0x6F;
            t[12] = 0xBD; t[13] = 0xBB; t[26] = 0xDB; t[27] = 0xDD; t[39] = 0xBA; t[40] = 0xDE; t[41] = 0xC0; t[43] = 0xDC; t[51] = 0xBC; t[52] = 0xBE; t[53] = 0xBF;
            return t;
        }
        [DllImport("libc", SetLastError = true)] private static extern int open(string ruta, int banderas);
        [DllImport("libc")] private static extern IntPtr read(int fd, byte* b, IntPtr n);
        [DllImport("libc")] private static extern int ioctl(int fd, ulong pedido, byte* datos);

        internal static void Inicia()
        {
            if (Environment.GetEnvironmentVariable("ROCOLA_SIN_TECLADO") == "1") return;
            try
            {
                if (!Directory.Exists("/dev/input")) return;
                foreach (string ruta in Directory.GetFiles("/dev/input", "event*"))
                {
                    int fd = open(ruta, 0 | 0x80000);
                    if (fd < 0) continue;
                    // solo los que dan teclas (EV_KEY)
                    byte* bits = stackalloc byte[8];
                    if (ioctl(fd, 0x80084520UL /* EVIOCGBIT(0, 8) */, bits) < 0 || (bits[0] & (1 << 1)) == 0) continue;
                    int esta = fd;
                    new Thread(delegate () { Lee(esta); }) { IsBackground = true, Name = "Teclas " + Path.GetFileName(ruta) }.Start();
                }
            }
            catch (Exception ex) { Console.Error.WriteLine("[Rocola OS] teclas: " + ex.Message); }
        }
        // struct input_event (64 bits): tiempo (16), tipo (2), codigo (2), valor (4).
        private static void Lee(int fd)
        {
            byte* ev = stackalloc byte[24];
            while (true)
            {
                long n = (long)read(fd, ev, (IntPtr)24);
                if (n < 24) { if (n <= 0) return; continue; }
                ushort tipo = *(ushort*)(ev + 16), codigo = *(ushort*)(ev + 18); int valor = *(int*)(ev + 20);
                if (tipo != 1) continue;   // EV_KEY
                int vk; if (!aWindows.TryGetValue(codigo, out vk)) continue;
                bool abajo = valor != 0;   // 1 bajar, 2 repeticion, 0 soltar
                Keys k = (Keys)vk;
                Escritorio.Entrada(delegate { Escritorio.Tecla(k, abajo); });
            }
        }
    }

    // PARA PROBAR SIN NADIE DELANTE: ROCOLA_GUION="2:Right 3.5:Return" (segundos:tecla) aprieta teclas; ROCOLA_CAPTURA=ruta.ppm con
    // ROCOLA_CAPTURA_SEG=15 guarda la pantalla a esa hora y sale (solo con ROCOLA_PANTALLA=fuera).
    internal static class Pruebas
    {
        private static volatile bool capturar;
        internal static void Inicia()
        {
            string guion = Environment.GetEnvironmentVariable("ROCOLA_GUION");
            string captura = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA");
            if (string.IsNullOrEmpty(guion) && string.IsNullOrEmpty(captura)) return;
            System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
            if (!string.IsNullOrEmpty(guion))
                new Thread(delegate ()
                {
                    foreach (string paso in guion.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int dos = paso.IndexOf(':'); if (dos < 0) continue;
                        double s; if (!double.TryParse(paso.Substring(0, dos), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s)) continue;
                        Keys k = Keys.None;
                        foreach (string parte in paso.Substring(dos + 1).Split('+'))
                        {
                            Keys una; if (Enum.TryParse(parte, true, out una)) k |= una;
                        }
                        while (reloj.Elapsed.TotalSeconds < s) Thread.Sleep(10);
                        Keys tecla = k & Keys.KeyCode; bool ctrl = (k & Keys.Control) != 0, may = (k & Keys.Shift) != 0;
                        Escritorio.Entrada(delegate
                        {
                            if (ctrl) Escritorio.Tecla(Keys.ControlKey, true);
                            if (may) Escritorio.Tecla(Keys.ShiftKey, true);
                            Escritorio.Tecla(tecla, true); Escritorio.Tecla(tecla, false);
                            if (may) Escritorio.Tecla(Keys.ShiftKey, false);
                            if (ctrl) Escritorio.Tecla(Keys.ControlKey, false);
                        });
                    }
                }) { IsBackground = true, Name = "Guion de teclas" }.Start();
            if (!string.IsNullOrEmpty(captura))
            {
                double seg = 15; double.TryParse(Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_SEG") ?? "15", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seg);
                new Thread(delegate () { while (reloj.Elapsed.TotalSeconds < seg) Thread.Sleep(20); capturar = true; Escritorio.Despierta(); }) { IsBackground = true }.Start();
                Pantalla.TrasPresentar = delegate
                {
                    if (!capturar) return;
                    capturar = false;
                    Pantalla.Captura(captura);
                    Console.Error.WriteLine("[Rocola OS] captura: " + captura + " (" + Pantalla.Cuadros + " cuadros, " + Gl.Ordenes + " ordenes)");
                    Environment.Exit(0);
                };
            }
        }
    }
}
