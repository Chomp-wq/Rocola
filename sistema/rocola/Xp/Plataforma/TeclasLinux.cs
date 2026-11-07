// LAS TECLAS EN ROCOLA OS (traduccion de XP, fase 1): lo que en XP le llega a la ventana desde Windows (WM_KEYDOWN / WM_KEYUP) cuando se
// aprieta un boton de la rocola (las placas de botones USB son teclados) o una tecla. Se leen por evdev (/dev/input/event*, tambien los
// que se conecten despues: se mira cada 2 s), se pasan al codigo de tecla de Windows (la tabla de Motor/Botones.cs: asi rocola_teclas.txt
// del bar vale tal cual) y se entregan en el hilo de la pantalla al escritorio (Escritorio.Tecla), que hace lo que haria Windows.
// LA REPETICION AL DEJARLO APRETADO es la de Windows XP de fabrica (no la del nucleo, que va mas rapido): la ultima tecla apretada se
// repite a los 500 ms y despues unas 30 veces por segundo (KeyboardDelay 1, KeyboardSpeed 31), como WM_KEYDOWN repetidos.
//   ROCOLA_PULSA=S:VK[,S:VK...]  (pruebas) aprieta y suelta la tecla VK (codigo de Windows) a los S segundos de arrancar.
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
            new Thread(Repite) { IsBackground = true, Name = "teclas: repeticion" }.Start();
        }

        // ---- pruebas: teclas a su hora ----
        internal static void Programadas(string lista)
        {
            if (string.IsNullOrEmpty(lista)) return;
            List<KeyValuePair<double, int>> l = new List<KeyValuePair<double, int>>();
            foreach (string e in lista.Split(','))
            {
                int k = e.IndexOf(':'); double s; int vk;
                if (k > 0 && double.TryParse(e.Substring(0, k), NumberStyles.Float, CultureInfo.InvariantCulture, out s) && int.TryParse(e.Substring(k + 1), out vk))
                    l.Add(new KeyValuePair<double, int>(s * 1000.0, vk));
            }
            l.Sort((a, b) => a.Key.CompareTo(b.Key));
            new Thread(delegate ()
            {
                foreach (KeyValuePair<double, int> p in l)
                {
                    double espera = p.Key - Escritorio.AhoraMs;
                    if (espera > 0) Thread.Sleep((int)espera);
                    Keys t = (Keys)p.Value;
                    Escritorio.Entrada(delegate { Escritorio.Tecla(t, true); });
                    Thread.Sleep(60);
                    Escritorio.Entrada(delegate { Escritorio.Tecla(t, false); });
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

        // struct input_event de 64 bits: tiempo (16 bytes), tipo (2), codigo (2), valor (4) = 24 bytes
        private static void Lee(FileStream f)
        {
            byte[] e = new byte[24];
            while (true)
            {
                int n = 0;
                try { while (n < 24) { int k = f.Read(e, n, 24 - n); if (k <= 0) return; n += k; } }
                catch { return; }   // se desconecto
                int tipo = BitConverter.ToUInt16(e, 16), codigo = BitConverter.ToUInt16(e, 18), valor = BitConverter.ToInt32(e, 20);
                if (tipo != EV_KEY || valor == 2) continue;   // (la repeticion del nucleo no: la hace Repite, como XP)
                if (codigo >= 0x110 && codigo < 0x160) continue;   // botones de raton y de mando (no son teclas)
                int vk = Botones.CodigoWindows(codigo);
                if (vk == 0) continue;
                bool abajo = valor == 1;
                Keys t = (Keys)vk;
                Escritorio.Entrada(delegate { Escritorio.Tecla(t, abajo); });
                lock (candado)
                {
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
                }
                Keys t = (Keys)vk;
                Escritorio.Entrada(delegate { Escritorio.Tecla(t, true); });
            }
        }
    }
}
