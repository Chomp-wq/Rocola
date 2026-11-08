// LAS TECLAS EN ROCOLA OS (traduccion de XP, fase 1): lo que en XP le llega a la ventana desde Windows (WM_KEYDOWN / WM_KEYUP) cuando se
// aprieta un boton de la rocola (las placas de botones USB son teclados) o una tecla. Se leen por evdev (/dev/input/event*, tambien los
// que se conecten despues: se mira cada 2 s), se pasan al codigo de tecla de Windows (la tabla de Motor/Botones.cs: asi rocola_teclas.txt
// del bar vale tal cual) y se entregan en el hilo de la pantalla al escritorio (Escritorio.Tecla), que hace lo que haria Windows.
// LA REPETICION AL DEJARLO APRETADO es la de Windows XP de fabrica (no la del nucleo, que va mas rapido): la ultima tecla apretada se
// repite a los 500 ms y despues unas 30 veces por segundo (KeyboardDelay 1, KeyboardSpeed 31), como WM_KEYDOWN repetidos.
//   ROCOLA_PULSA=S:VK[,S:VK...]  (pruebas) aprieta y suelta la tecla VK (codigo de Windows) a los S segundos de arrancar; S:VK~MS la
//   deja apretada MS milisegundos, con la repeticion de XP (para la guia del rojo mantenido; otra tecla entre medias va con ella apretada).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Rocola
{
    internal static class TeclasLinux
    {
        internal const int RetrasoMs = 500, RepeticionMs = 33;
        private const int EV_KEY = 1;
        private static readonly HashSet<string> abiertos = new HashSet<string>();
        private static readonly object candado = new object();
        private static int repiteVk; private static double repiteEn;   // la tecla que se repite y cuando
        private static readonly AutoResetEvent senalRepite = new AutoResetEvent(false);
        private static bool arrancado;

        internal static void Arranca(string carpeta)
        {
            if (arrancado) return;
            arrancado = true;
            AbreNuevos(carpeta);
            new Thread(delegate () { while (true) { Thread.Sleep(2000); try { AbreNuevos(carpeta); } catch { } } }) { IsBackground = true, Name = "teclas nuevas" }.Start();
            ArrancaRepeticion();
        }
        private static bool repeticionArrancada;
        private static void ArrancaRepeticion()
        {
            lock (candado) { if (repeticionArrancada) return; repeticionArrancada = true; }
            new Thread(Repite) { IsBackground = true, Name = "teclas: repeticion" }.Start();
        }

        // ---- pruebas: teclas y raton a su hora ----
        // S:VK (apretar y soltar), S:VK1+VK2 (VK1 apretada mientras se aprieta VK2: 17+114 = Ctrl+F3), S:raton:X:Y (llevar la flecha),
        // S:clic:X:Y (llevarla y hacer clic con el izquierdo), S:rueda:N (N muescas).
        internal static void Programadas(string lista)
        {
            if (string.IsNullOrEmpty(lista)) return;
            List<KeyValuePair<double, string>> l = new List<KeyValuePair<double, string>>();
            foreach (string e in lista.Split(','))
            {
                int k = e.IndexOf(':'); double s;
                if (k > 0 && double.TryParse(e.Substring(0, k), NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                {
                    string v = e.Substring(k + 1);
                    int m = v.IndexOf('~'), ms;
                    if (m > 0 && int.TryParse(v.Substring(m + 1), out ms))   // S:VK~MS: apretarla y soltarla MS despues
                    {
                        l.Add(new KeyValuePair<double, string>(s * 1000.0, "baja:" + v.Substring(0, m)));
                        l.Add(new KeyValuePair<double, string>(s * 1000.0 + ms, "suelta:" + v.Substring(0, m)));
                    }
                    else l.Add(new KeyValuePair<double, string>(s * 1000.0, v));
                }
            }
            l.Sort((a, b) => a.Key.CompareTo(b.Key));
            new Thread(delegate ()
            {
                foreach (KeyValuePair<double, string> p in l)
                {
                    double espera = p.Key - Escritorio.AhoraMs;
                    if (espera > 0) Thread.Sleep((int)espera);
                    string[] q = p.Value.Split(':');
                    if ((q[0] == "raton" || q[0] == "clic") && q.Length == 3)
                    {
                        int x = int.Parse(q[1]), y = int.Parse(q[2]);
                        Escritorio.Entrada(delegate { Escritorio.RatonEn(x, y); });
                        if (q[0] == "clic")
                        {
                            Thread.Sleep(30); Escritorio.Entrada(delegate { Escritorio.RatonBoton(MouseButtons.Left, true); });
                            Thread.Sleep(60); Escritorio.Entrada(delegate { Escritorio.RatonBoton(MouseButtons.Left, false); });
                        }
                        continue;
                    }
                    if (q[0] == "rueda" && q.Length == 2) { int m = int.Parse(q[1]); Escritorio.Entrada(delegate { Escritorio.RatonRueda(m); }); continue; }
                    if ((q[0] == "baja" || q[0] == "suelta") && q.Length == 2)
                    {
                        // mantenida: llega como una tecla de verdad (WM_KEYDOWN, y repetida a los 500 ms como en XP, hasta soltarla)
                        int vk; if (!int.TryParse(q[1], out vk)) continue;
                        bool abajo = q[0] == "baja"; Keys tk = (Keys)vk;
                        ArrancaRepeticion();
                        lock (candado)
                        {
                            Escritorio.Entrada(delegate { Escritorio.Tecla(tk, abajo); });   // (dentro del candado: ver Lee)
                            if (abajo) { repiteVk = vk; repiteEn = Escritorio.AhoraMs + RetrasoMs; senalRepite.Set(); }
                            else if (vk == repiteVk) repiteVk = 0;
                        }
                        continue;
                    }
                    string[] teclas = p.Value.Split('+');
                    Keys mod = teclas.Length == 2 ? (Keys)int.Parse(teclas[0]) : Keys.None;
                    Keys t = (Keys)int.Parse(teclas[teclas.Length - 1]);
                    if (mod != Keys.None) { Escritorio.Entrada(delegate { Escritorio.Tecla(mod, true); }); Thread.Sleep(30); }
                    Escritorio.Entrada(delegate { Escritorio.Tecla(t, true); });
                    Thread.Sleep(60);
                    Escritorio.Entrada(delegate { Escritorio.Tecla(t, false); });
                    if (mod != Keys.None) { Thread.Sleep(30); Escritorio.Entrada(delegate { Escritorio.Tecla(mod, false); }); }
                }
            }) { IsBackground = true, Name = "teclas de prueba" }.Start();
        }

        private static void AbreNuevos(string carpeta)
        {
            string[] dev;
            try { dev = Directory.GetFiles(carpeta, "event*"); } catch { return; }
            foreach (string d in dev)
            {
                lock (candado) if (abiertos.Contains(d)) continue;
                FileStream f;
                try { f = new FileStream(d, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, false); } catch { continue; }
                lock (candado) abiertos.Add(d);
                string ruta = d;
                new Thread(delegate () { try { Lee(f); } finally { lock (candado) abiertos.Remove(ruta); try { f.Dispose(); } catch { } } }) { IsBackground = true, Name = "teclas " + Path.GetFileName(d) }.Start();
            }
        }

        // EL RATON (y las pantallas tactiles): movimiento, rueda y botones, juntados por cada SYN_REPORT, al escritorio (Escritorio.Raton*).
        private const int EV_SYN = 0, EV_REL = 2, EV_ABS = 3, BTN_LEFT = 0x110, BTN_RIGHT = 0x111, BTN_MIDDLE = 0x112, BTN_TOUCH = 0x14A;
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern int ioctl(IntPtr fd, uint pedido, int[] info);
        private static bool Limites(FileStream f, int eje, out int min, out int max)
        {
            min = max = 0;
            try { int[] i = new int[6]; if (ioctl(f.SafeFileHandle.DangerousGetHandle(), 0x80184540u + (uint)eje, i) < 0) return false; min = i[1]; max = i[2]; return max > min; }   // EVIOCGABS
            catch { return false; }
        }

        // struct input_event de 64 bits: tiempo (16 bytes), tipo (2), codigo (2), valor (4) = 24 bytes
        private static void Lee(FileStream f)
        {
            byte[] e = new byte[24];
            int dx = 0, dy = 0, ax = -1, ay = -1, xmin = 0, xmax = 0, ymin = 0, ymax = 0; bool absVisto = false, conAbs = false;
            while (true)
            {
                int n = 0;
                try { while (n < 24) { int k = f.Read(e, n, 24 - n); if (k <= 0) return; n += k; } }
                catch { return; }   // se desconecto
                int tipo = BitConverter.ToUInt16(e, 16), codigo = BitConverter.ToUInt16(e, 18), valor = BitConverter.ToInt32(e, 20);
                if (tipo == EV_REL)
                {
                    if (codigo == 0) dx += valor; else if (codigo == 1) dy += valor;
                    else if (codigo == 8 && valor != 0) { int m = valor; Escritorio.Entrada(delegate { Escritorio.RatonRueda(m); }); }   // REL_WHEEL
                    continue;
                }
                if (tipo == EV_ABS && (codigo == 0 || codigo == 1))
                {
                    if (!absVisto) { absVisto = true; conAbs = Limites(f, 0, out xmin, out xmax) & Limites(f, 1, out ymin, out ymax); }
                    if (conAbs) { if (codigo == 0) ax = valor; else ay = valor; }
                    continue;
                }
                if (tipo == EV_SYN && codigo == 0)
                {
                    if (dx != 0 || dy != 0) { int mx = dx, my = dy; dx = dy = 0; Escritorio.Entrada(delegate { Escritorio.RatonMueve(mx, my); }); }
                    if (conAbs && (ax >= 0 || ay >= 0))
                    {
                        int px = ax, py = ay, x0 = xmin, x1 = xmax, y0 = ymin, y1 = ymax; ax = ay = -1;
                        Escritorio.Entrada(delegate
                        {
                            System.Drawing.Size p = Escritorio.Pantalla;
                            Escritorio.RatonEn(px < 0 ? Escritorio.PosicionRaton.X : (int)((px - x0) * (long)(p.Width - 1) / (x1 - x0)), py < 0 ? Escritorio.PosicionRaton.Y : (int)((py - y0) * (long)(p.Height - 1) / (y1 - y0)));
                        });
                    }
                    continue;
                }
                if (tipo != EV_KEY || valor == 2) continue;   // (la repeticion del nucleo no: la hace Repite, como XP)
                if (codigo == BTN_LEFT || codigo == BTN_RIGHT || codigo == BTN_MIDDLE || codigo == BTN_TOUCH)
                {
                    MouseButtons b = codigo == BTN_RIGHT ? MouseButtons.Right : codigo == BTN_MIDDLE ? MouseButtons.Middle : MouseButtons.Left;
                    bool baja = valor == 1;
                    Escritorio.Entrada(delegate { Escritorio.RatonBoton(b, baja); });
                    continue;
                }
                if (codigo >= 0x110 && codigo < 0x160) continue;   // otros botones de raton y de mando (no son teclas)
                int vk = Botones.CodigoWindows(codigo);
                if (vk == 0) continue;
                bool abajo = valor == 1;
                Keys t = (Keys)vk;
                lock (candado)
                {
                    // (port nativo: la tecla se entrega dentro del candado de la repeticion, asi una repeticion nunca llega despues de
                    // soltarla, como en Windows; antes podia colarse detras del soltar y contar como otra pulsacion)
                    Escritorio.Entrada(delegate { Escritorio.Tecla(t, abajo); });
                    bool modificador = vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x14;
                    if (abajo && !modificador) { repiteVk = vk; repiteEn = Escritorio.AhoraMs + RetrasoMs; senalRepite.Set(); }
                    else if (!abajo && vk == repiteVk) repiteVk = 0;
                }
            }
        }

        private static void Repite()
        {
            while (true)
            {
                int vk; double en;
                lock (candado) { vk = repiteVk; en = repiteEn; }
                if (vk == 0) { senalRepite.WaitOne(); continue; }
                double falta = en - Escritorio.AhoraMs;
                if (falta > 0) { senalRepite.WaitOne((int)Math.Ceiling(falta)); continue; }
                lock (candado)
                {
                    if (repiteVk != vk || repiteEn != en) continue;
                    repiteEn = en + RepeticionMs;
                    if (repiteEn < Escritorio.AhoraMs) repiteEn = Escritorio.AhoraMs + RepeticionMs;   // (sin rafagas si se atraso)
                    // (dentro del candado: si en este momento se suelta la tecla, el soltar va despues y la repeticion ya no sale)
                    Keys t = (Keys)vk;
                    Escritorio.Entrada(delegate { Escritorio.Tecla(t, true); });
                }
            }
        }
    }
}
