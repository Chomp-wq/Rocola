// LOS BOTONES DE LA ROCOLA EN LINUX. En XP los botones (y el monedero) llegan como teclas de Windows y se configuran con el asistente
// (rocola_teclas.txt: arriba=, abajo=, izquierda=, derecha=, poner=, regresar=, moneda=, saltar=, con el codigo de tecla de Windows).
// En Rocola OS llegan por evdev (/dev/input/event*: los teclados y las placas de botones USB, que se presentan como teclado). Aqui se
// leen, se traducen al codigo de Windows (asi rocola_teclas.txt del bar vale tal cual) y se convierten en las ocho funciones.
// Un boton cuenta al bajar (no al soltar ni al repetirse por estar apretado: en XP, la moneda y ROJO+ARRIBA tambien, una vez por pulso).
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Rocola
{
    public enum Boton { Ninguno, Arriba, Abajo, Izquierda, Derecha, Poner, Regresar, Moneda, Saltar }

    public sealed class Botones : IDisposable
    {
        // Codigo de tecla de Linux (linux/input-event-codes.h) -> codigo de tecla de Windows (VK_*), el que guarda rocola_teclas.txt.
        private static readonly Dictionary<int, int> aWindows = Tabla();
        private static Dictionary<int, int> Tabla()
        {
            Dictionary<int, int> t = new Dictionary<int, int>();
            string fila1 = "QWERTYUIOP", fila2 = "ASDFGHJKL", fila3 = "ZXCVBNM";
            for (int i = 0; i < fila1.Length; i++) t[16 + i] = fila1[i];   // KEY_Q = 16
            for (int i = 0; i < fila2.Length; i++) t[30 + i] = fila2[i];   // KEY_A = 30
            for (int i = 0; i < fila3.Length; i++) t[44 + i] = fila3[i];   // KEY_Z = 44
            t[2] = '1'; t[3] = '2'; t[4] = '3'; t[5] = '4'; t[6] = '5'; t[7] = '6'; t[8] = '7'; t[9] = '8'; t[10] = '9'; t[11] = '0';
            t[1] = 0x1B;  /* ESC */          t[14] = 0x08; /* BACKSPACE */    t[15] = 0x09; /* TAB */
            t[28] = 0x0D; /* ENTER */        t[96] = 0x0D; /* KP ENTER */     t[57] = 0x20; /* ESPACIO */
            t[29] = 0x11; t[97] = 0x11;      /* CTRL */                       t[42] = 0x10; t[54] = 0x10; /* SHIFT */
            t[56] = 0x12; t[100] = 0x12;     /* ALT */                        t[58] = 0x14; /* MAYUS */
            t[103] = 0x26; t[108] = 0x28; t[105] = 0x25; t[106] = 0x27;   // flechas: arriba, abajo, izquierda, derecha
            t[102] = 0x24; t[107] = 0x23; t[104] = 0x21; t[109] = 0x22;   // inicio, fin, re pag, av pag
            t[110] = 0x2D; t[111] = 0x2E;                                  // insertar, suprimir
            for (int i = 0; i < 10; i++) t[59 + i] = 0x70 + i;             // F1..F10
            t[87] = 0x7A; t[88] = 0x7B;                                    // F11, F12
            // teclado numerico
            t[82] = 0x60; t[79] = 0x61; t[80] = 0x62; t[81] = 0x63; t[75] = 0x64; t[76] = 0x65; t[77] = 0x66; t[71] = 0x67; t[72] = 0x68; t[73] = 0x69;
            t[55] = 0x6A; t[78] = 0x6B; t[74] = 0x6D; t[83] = 0x6E; t[98] = 0x6F;
            // signos (teclado de EE. UU.; los VK_OEM de Windows)
            t[12] = 0xBD; t[13] = 0xBB; t[26] = 0xDB; t[27] = 0xDD; t[39] = 0xBA; t[40] = 0xDE; t[41] = 0xC0; t[43] = 0xDC; t[51] = 0xBC; t[52] = 0xBE; t[53] = 0xBF;
            return t;
        }
        public static int CodigoWindows(int codigoLinux) { int v; return aWindows.TryGetValue(codigoLinux, out v) ? v : 0; }

        // La configuracion: codigo de Windows -> funcion. Sin rocola_teclas.txt (o incompleto, o con un boton repetido, como en XP), la de
        // un teclado: flechas, Enter (poner), Retroceso (regresar), 5 (moneda) y S (saltar).
        private readonly Dictionary<int, Boton> funciones = new Dictionary<int, Boton>();
        public bool DeArchivo { get; private set; }
        public void Configura(string rocolaTeclas)
        {
            funciones.Clear(); DeArchivo = false;
            Dictionary<string, Boton> nombres = new Dictionary<string, Boton> {
                { "arriba", Boton.Arriba }, { "abajo", Boton.Abajo }, { "izquierda", Boton.Izquierda }, { "derecha", Boton.Derecha },
                { "poner", Boton.Poner }, { "regresar", Boton.Regresar }, { "moneda", Boton.Moneda }, { "saltar", Boton.Saltar } };
            Dictionary<int, Boton> leidas = new Dictionary<int, Boton>();
            HashSet<Boton> vistas = new HashSet<Boton>();
            bool vale = true;
            try
            {
                if (rocolaTeclas != null && File.Exists(rocolaTeclas))
                    foreach (string linea in File.ReadAllLines(rocolaTeclas))
                    {
                        string s = linea.Trim(); int eq = s.IndexOf('='); if (eq <= 0) continue;
                        Boton b; int val;
                        if (!nombres.TryGetValue(s.Substring(0, eq).Trim().ToLowerInvariant(), out b)) continue;
                        if (!int.TryParse(s.Substring(eq + 1).Trim(), out val) || val <= 0 || val > 0xFF) continue;   // como LoadKeys de XP
                        if (leidas.ContainsKey(val)) vale = false;   // dos funciones en el mismo boton: no vale (como XP)
                        leidas[val] = b; vistas.Add(b);
                    }
            }
            catch { vale = false; }
            if (vale && vistas.Count == 8) { foreach (KeyValuePair<int, Boton> kv in leidas) funciones[kv.Key] = kv.Value; DeArchivo = true; return; }
            funciones[0x26] = Boton.Arriba; funciones[0x28] = Boton.Abajo; funciones[0x25] = Boton.Izquierda; funciones[0x27] = Boton.Derecha;
            funciones[0x0D] = Boton.Poner; funciones[0x08] = Boton.Regresar; funciones['5'] = Boton.Moneda; funciones['S'] = Boton.Saltar;
        }
        public Boton FuncionDe(int codigoWindows) { Boton b; return funciones.TryGetValue(codigoWindows, out b) ? b : Boton.Ninguno; }

        // ---- la lectura (evdev) ----
        private const int EV_KEY = 1;
        private readonly List<Thread> hilos = new List<Thread>();
        private readonly List<FileStream> abiertos = new List<FileStream>();
        private volatile bool parar;
        public event Action<Boton, int> Pulsado;   // la funcion (Ninguno si la tecla no es de la rocola) y el codigo de Windows
        // LO QUE ESTA APRETADO AHORA (para los combos de XP: REGRESAR mantenido + IZQ/DER cambia de letra, + ARRIBA abre el buscador).
        private readonly HashSet<int> apretadas = new HashSet<int>();
        public bool Apretado(Boton f)
        {
            lock (apretadas) foreach (int vk in apretadas) if (FuncionDe(vk) == f) return true;
            return false;
        }

        // Abre todos los /dev/input/event* que se dejen leer y escucha cada uno en su hilo. Devuelve cuantos abrio.
        public int Escucha(string carpeta = "/dev/input")
        {
            string[] dev;
            try { dev = Directory.GetFiles(carpeta, "event*"); } catch { return 0; }
            foreach (string d in dev)
            {
                FileStream f;
                try { f = new FileStream(d, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, false); } catch { continue; }
                abiertos.Add(f);
                Thread h = new Thread(delegate () { Lee(f); }) { IsBackground = true, Name = "botones " + Path.GetFileName(d) };
                hilos.Add(h); h.Start();
            }
            return abiertos.Count;
        }

        // struct input_event de 64 bits: tiempo (16 bytes), tipo (2), codigo (2), valor (4) = 24 bytes.
        private void Lee(FileStream f)
        {
            byte[] e = new byte[24];
            while (!parar)
            {
                int n = 0;
                try { while (n < 24) { int k = f.Read(e, n, 24 - n); if (k <= 0) return; n += k; } }
                catch { return; }   // se desconecto el aparato
                int tipo = BitConverter.ToUInt16(e, 16), codigo = BitConverter.ToUInt16(e, 18), valor = BitConverter.ToInt32(e, 20);
                if (tipo != EV_KEY) continue;
                if (valor == 0) { lock (apretadas) apretadas.Remove(CodigoWindows(codigo)); continue; }   // al soltar: ya no esta apretado
                if (valor != 1) continue;   // cuenta solo al bajar (1), no al repetirse (2)
                lock (apretadas) apretadas.Add(CodigoWindows(codigo));
                Entrega(codigo);
            }
        }
        // Lo que se leyo de un aparato (o de una prueba): codigo de Linux.
        public void Entrega(int codigoLinux)
        {
            int vk = CodigoWindows(codigoLinux);
            Action<Boton, int> a = Pulsado;
            if (a != null) a(FuncionDe(vk), vk);
        }

        public void Dispose()
        {
            parar = true;
            foreach (FileStream f in abiertos) { try { f.Dispose(); } catch { } }
        }
    }
}
