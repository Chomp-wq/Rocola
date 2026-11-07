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
        // Si el archivo esta danado o falta (un apagon al guardarlo), se usa la copia de antes (.bak, la deja el guardado seguro).
        // Por orden: el archivo, su copia de antes (.bak) y la copia de fuera de lo aprendido (el disco de la musica).
        public void Configura(string rocolaTeclas, string copia = null)
        {
            if (rocolaTeclas != null && (LeeTeclas(rocolaTeclas) || LeeTeclas(rocolaTeclas + ".bak")))
            {
                // la copia de fuera, si aun no esta (una rocola configurada antes de que la hubiera)
                try { if (copia != null && !System.IO.File.Exists(copia) && System.IO.File.Exists(rocolaTeclas)) System.IO.File.Copy(rocolaTeclas, copia); } catch { }
                return;
            }
            if (copia != null && LeeTeclas(copia))
            {
                try { if (rocolaTeclas != null) { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(rocolaTeclas)); System.IO.File.Copy(copia, rocolaTeclas, true); } } catch { }   // vuelve a lo aprendido
                return;
            }
            PorDefecto();
        }
        private void PorDefecto()
        {
            funciones.Clear(); DeArchivo = false;
            funciones[0x26] = Boton.Arriba; funciones[0x28] = Boton.Abajo; funciones[0x25] = Boton.Izquierda; funciones[0x27] = Boton.Derecha;
            funciones[0x0D] = Boton.Poner; funciones[0x08] = Boton.Regresar; funciones['5'] = Boton.Moneda; funciones['S'] = Boton.Saltar;
        }
        private bool LeeTeclas(string rocolaTeclas)
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
            if (vale && vistas.Count == 8) { foreach (KeyValuePair<int, Boton> kv in leidas) funciones[kv.Key] = kv.Value; DeArchivo = true; return true; }
            return false;
        }
        public Boton FuncionDe(int codigoWindows) { Boton b; return funciones.TryGetValue(codigoWindows, out b) ? b : Boton.Ninguno; }

        // ---- la lectura (evdev) ----
        private const int EV_KEY = 1;
        private readonly List<Thread> hilos = new List<Thread>();
        private readonly List<FileStream> abiertos = new List<FileStream>();
        private volatile bool parar;
        public event Action<Boton, int> Pulsado;   // la funcion (Ninguno si la tecla no es de la rocola) y el codigo de Windows
        // Lo mismo, con lo que estaba apretado EN ESE MOMENTO (REGRESAR mantenido): quien atiende la pulsacion llega despues, y para entonces
        // REGRESAR ya se pudo haber soltado (un combo rapido).
        public event Action<Boton, int, bool> PulsadoConRegresar;
        // LO QUE ESTA APRETADO AHORA (para los combos de XP: REGRESAR mantenido + IZQ/DER cambia de letra, + ARRIBA abre el buscador).
        private readonly HashSet<int> apretadas = new HashSet<int>();
        public bool TeclaApretada(int vk) { lock (apretadas) return apretadas.Contains(vk); }
        public bool Apretado(Boton f)
        {
            lock (apretadas) foreach (int vk in apretadas) if (FuncionDe(vk) == f) return true;
            return false;
        }

        // EL RATON (7-11-2026, en el A4 no se podia usar): lo que llega de un raton (o una pantalla tactil) se entrega aparte, ya juntado por
        // cada SYN_REPORT. Tipo 1: se movio (a, b = dx, dy en cuentas del raton); 2: la rueda (a = +1 arriba, -1 abajo); 3: un boton
        // bajado (a = 1 izquierdo o toque, 2 derecho, 3 el de en medio); 4: posicion absoluta (a, b de 0 a 65535: pantalla tactil, tableta).
        public event Action<int, int, int> Raton;
        private const int EV_SYN = 0, EV_REL = 2, EV_ABS = 3, BTN_LEFT = 272, BTN_RIGHT = 273, BTN_MIDDLE = 274, BTN_TOUCH = 330;

        // Abre todos los /dev/input/event* que se dejen leer y escucha cada uno en su hilo. Devuelve cuantos abrio. Y LOS QUE SE CONECTEN
        // DESPUES (7-11-2026: un raton o un teclado enchufados con la rocola ya prendida no se leian nunca): se mira cada 2 s.
        private readonly HashSet<string> rutas = new HashSet<string>();
        private string carpetaEntrada;
        public int Escucha(string carpeta = "/dev/input")
        {
            carpetaEntrada = carpeta;
            int n = AbreNuevos();
            Thread vig = new Thread(delegate () { while (!parar) { Thread.Sleep(2000); try { AbreNuevos(); } catch { } } }) { IsBackground = true, Name = "botones nuevos" };
            vig.Start();
            return n;
        }
        private int AbreNuevos()
        {
            string[] dev;
            try { dev = Directory.GetFiles(carpetaEntrada, "event*"); } catch { return 0; }
            int n = 0;
            foreach (string d in dev)
            {
                lock (rutas) if (rutas.Contains(d)) continue;
                FileStream f;
                try { f = new FileStream(d, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, false); } catch { continue; }
                lock (rutas) { rutas.Add(d); abiertos.Add(f); }
                n++;
                string ruta = d;
                Thread h = new Thread(delegate () { try { Lee(f, ruta); } finally { lock (rutas) { rutas.Remove(ruta); abiertos.Remove(f); } try { f.Dispose(); } catch { } } }) { IsBackground = true, Name = "botones " + Path.GetFileName(d) };
                lock (hilos) hilos.Add(h);
                h.Start();
            }
            return n;
        }
        // LOS LIMITES DE UN EJE ABSOLUTO (EVIOCGABS: struct input_absinfo, 6 int: valor, minimo, maximo, ...)
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        private static extern int ioctl(IntPtr fd, uint pedido, int[] info);
        private static bool Limites(FileStream f, int eje, out int min, out int max)
        {
            min = 0; max = 0;
            try
            {
                int[] info = new int[6];
                if (ioctl(f.SafeFileHandle.DangerousGetHandle(), 0x80184540u + (uint)eje, info) < 0) return false;
                min = info[1]; max = info[2]; return max > min;
            }
            catch { return false; }
        }

        // struct input_event de 64 bits: tiempo (16 bytes), tipo (2), codigo (2), valor (4) = 24 bytes.
        private void Lee(FileStream f, string ruta)
        {
            byte[] e = new byte[24];
            int dx = 0, dy = 0, ax = -1, ay = -1, xmin = 0, xmax = 0, ymin = 0, ymax = 0; bool conAbs = false, absVisto = false;
            while (!parar)
            {
                int n = 0;
                try { while (n < 24) { int k = f.Read(e, n, 24 - n); if (k <= 0) return; n += k; } }
                catch { return; }   // se desconecto el aparato
                int tipo = BitConverter.ToUInt16(e, 16), codigo = BitConverter.ToUInt16(e, 18), valor = BitConverter.ToInt32(e, 20);
                if (tipo == EV_REL)
                {
                    if (codigo == 0) dx += valor; else if (codigo == 1) dy += valor;
                    else if (codigo == 8 && valor != 0) Avisa(2, valor > 0 ? 1 : -1, 0);   // REL_WHEEL (sin acumular: cada muesca cuenta)
                    continue;
                }
                if (tipo == EV_ABS && (codigo == 0 || codigo == 1))   // ABS_X, ABS_Y
                {
                    if (!absVisto) { absVisto = true; conAbs = Limites(f, 0, out xmin, out xmax) & Limites(f, 1, out ymin, out ymax); }
                    if (conAbs) { if (codigo == 0) ax = valor; else ay = valor; }
                    continue;
                }
                if (tipo == EV_SYN && codigo == 0)
                {
                    if (dx != 0 || dy != 0) { Avisa(1, dx, dy); dx = dy = 0; }
                    if (conAbs && (ax >= 0 || ay >= 0))
                    {
                        Avisa(4, ax < 0 ? -1 : (int)((ax - xmin) * 65535L / (xmax - xmin)), ay < 0 ? -1 : (int)((ay - ymin) * 65535L / (ymax - ymin)));
                        ax = ay = -1;
                    }
                    continue;
                }
                if (tipo != EV_KEY) continue;
                if (codigo == BTN_LEFT || codigo == BTN_RIGHT || codigo == BTN_MIDDLE || codigo == BTN_TOUCH)
                {
                    if (valor == 1) Avisa(3, codigo == BTN_RIGHT ? 2 : codigo == BTN_MIDDLE ? 3 : 1, 0);
                    continue;   // (los botones del raton no son teclas de la rocola)
                }
                if (valor == 0) { lock (apretadas) apretadas.Remove(CodigoWindows(codigo)); continue; }   // al soltar: ya no esta apretado
                if (valor != 1) continue;   // cuenta solo al bajar (1), no al repetirse (2)
                lock (apretadas) apretadas.Add(CodigoWindows(codigo));
                Entrega(codigo);
            }
        }
        private void Avisa(int tipo, int a, int b) { Action<int, int, int> r = Raton; if (r != null) r(tipo, a, b); }
        public void AvisaRaton(int tipo, int a, int b) { Avisa(tipo, a, b); }   // (pruebas)
        // Lo que se leyo de un aparato (o de una prueba): codigo de Linux.
        public void Entrega(int codigoLinux)
        {
            int vk = CodigoWindows(codigoLinux);
            Action<Boton, int> a = Pulsado;
            if (a != null) a(FuncionDe(vk), vk);
            Action<Boton, int, bool> b = PulsadoConRegresar;
            if (b != null) b(FuncionDe(vk), vk, FuncionDe(vk) != Boton.Regresar && Apretado(Boton.Regresar));
        }

        public void Dispose()
        {
            parar = true;
            List<FileStream> l; lock (rutas) l = new List<FileStream>(abiertos);
            foreach (FileStream f in l) { try { f.Dispose(); } catch { } }
        }
    }
}
