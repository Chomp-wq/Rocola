// LA MUSICA EN SU PROPIO HILO (las dos ramas; 28 de septiembre de 2026).
//
// La salida clasica (WaveOut con aviso por VENTANA: ver SalidaSonido.cs) pide la musica desde el bucle de mensajes
// de la pantalla. Cada 50 ms la pantalla se paraba en seco a descomprimir el MP3 y a pasar el bloque por toda la
// cadena (nivelado, EQ, realce, DSEE, grave, limitador...). Medido con todo encendido: 1,8 ms de media por bloque y
// picos de 7 ms en un procesador de servidor; en el equipo del bar, varias veces eso. Un cuadro dura 16,7 ms: un solo
// bloque podia comerse un cuadro y se veia como un saltito en la estrella y en las animaciones. Y es justo lo que pasa
// en los equipos de dos nucleos, que son los que usan esta salida (Modest siempre; HD cuando no es un equipo "HD").
//
// Aqui la cadena la lee UN HILO PROPIO, que va llenando un colchon corto (200 ms). Cuando la salida pide un bloque,
// la pantalla solo COPIA lo que ya esta hecho. El trabajo es el mismo, pero lo hace el otro nucleo. La salida, su
// colchon de 1 s y sus bloques de 50 ms no cambian, ni una muestra de lo que suena (mismo orden, mismas cuentas).
//
// LO QUE HAY QUE CUIDAR:
//  - Lo que se oye va ~160 ms mas tarde de lo que se lee: el pulso, la aurora y el relevo sin pausa lo suman
//    (RetrasoAudible). Los cambios de Ctrl+F3 llegan ese poco mas tarde; saltar o parar es inmediato.
//  - Si el hilo se atrasa (un disco que tarda en despertar), la pantalla lo espera hasta 600 ms: la salida aun
//    tiene casi un segundo en su colchon y no se corta. Si ni asi llega, entrega silencio en vez de colgarse, y no
//    vuelve a esperar hasta que llegue musica. Nunca devuelve 0 si no se acabo: eso pararia la salida.
//  - El final y los fallos llegan a la salida como antes: 0 cuando se acabo lo que habia, y la excepcion de la
//    cadena sale de la lectura de la salida (la atiende NAudio, que avisa con PlaybackStopped).
//  - 'buffer' puede ser la vista flotante que NAudio pone sobre su byte[]: se copia muestra a muestra, nunca con
//    Array.Copy ni Array.Clear (ver Realce.cs).
//  - Al parar, el hilo se detiene y se espera ANTES de soltar el lector (DisposePlaybackNow): nadie lee un archivo
//    cerrado, y las medidas de la cancion se cierran con el audio de verdad quieto.
// Es una optimizacion que no cambia ni el sonido ni lo que se ve: siempre activa y sin interruptor.
using System;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class BombaSonido : ISampleProvider, IDisposable
    {
        internal const int ColchonMs = 200;     // lo que el hilo deja hecho por delante
        internal const int TrozoMs = 25;        // lo que lee de una vez
        internal const int EsperaMaxMs = 600;   // lo mas que la pantalla espera a un hilo atrasado

        private readonly ISampleProvider src;
        private readonly object cerrojo = new object();
        private readonly float[] anillo, trozo;
        private int ini, lleno;                  // el colchon: donde empieza y cuanto tiene (muestras)
        private readonly int trozoN;
        private Thread hilo;
        private bool parar, fin, atascada;
        private Exception fallo;
        private long huecos;                     // bloques que salieron en silencio por un hilo atrasado
        private int hiloLector;                  // el hilo que lee la cadena (para las pruebas)
        internal readonly int AdelantoMs;        // cuanto antes de oirse se lee cada trozo, de media

        internal BombaSonido(ISampleProvider s)
        {
            if (s == null) throw new ArgumentNullException("s");
            src = s;
            WaveFormat f = s.WaveFormat;
            int ch = Math.Max(1, f.Channels), hz = Math.Max(8000, f.SampleRate);
            int cuadrosTrozo = Math.Max(64, hz * TrozoMs / 1000);
            int cuadrosAnillo = Math.Max(cuadrosTrozo * 4, hz * ColchonMs / 1000);
            trozoN = cuadrosTrozo * ch;
            anillo = new float[cuadrosAnillo * ch];
            trozo = new float[trozoN];
            // El hilo lee un trozo en cuanto cabe: cada trozo entra con el colchon entre medio lleno de un trozo y lleno.
            AdelantoMs = (int)((cuadrosAnillo - cuadrosTrozo * 3L / 2) * 1000 / hz);
        }

        public WaveFormat WaveFormat { get { return src.WaveFormat; } }
        internal long Huecos { get { lock (cerrojo) return huecos; } }
        internal int HiloLector { get { lock (cerrojo) return hiloLector; } }
        internal bool Viva { get { Thread h; lock (cerrojo) h = hilo; return h != null && h.IsAlive; } }

        public int Read(float[] buffer, int offset, int count)
        {
            lock (cerrojo)
            {
                if (hilo == null && !parar) Arranca();
                int hechos = 0;
                int limite = Environment.TickCount + EsperaMaxMs;
                while (hechos < count)
                {
                    if (lleno > 0)
                    {
                        int k = Math.Min(lleno, count - hechos), n = anillo.Length;
                        for (int i = 0; i < k; i++) { buffer[offset + hechos + i] = anillo[ini]; if (++ini == n) ini = 0; }
                        lleno -= k; hechos += k;
                        Monitor.PulseAll(cerrojo);   // hay sitio: que el hilo siga
                        continue;
                    }
                    if (fin || parar || atascada) break;
                    int queda = unchecked(limite - Environment.TickCount);
                    if (queda <= 0) { atascada = true; huecos++; break; }
                    Monitor.Wait(cerrojo, queda);
                }
                if (hechos < count && !fin && !parar)
                {
                    // El hilo va atrasado: silencio en lo que falta, nunca 0 (la salida lo tomaria por el final).
                    for (int i = hechos; i < count; i++) buffer[offset + i] = 0f;
                    return count;
                }
                if (hechos == 0 && fallo != null) { Exception e = fallo; fallo = null; throw new InvalidOperationException(e.Message, e); }
                return hechos;
            }
        }

        private void Arranca()
        {
            Thread h = new Thread(Bombea);
            h.IsBackground = true;
            h.Name = "Rocola: la musica";
            try { h.Priority = ThreadPriority.AboveNormal; } catch { }   // por delante de la pantalla: la musica no se puede cortar
            hilo = h;
            h.Start();
        }

        private void Bombea()
        {
            lock (cerrojo) hiloLector = Thread.CurrentThread.ManagedThreadId;
            try
            {
                while (true)
                {
                    lock (cerrojo)
                    {
                        while (!parar && anillo.Length - lleno < trozoN) Monitor.Wait(cerrojo);
                        if (parar) return;
                    }
                    int n = src.Read(trozo, 0, trozoN);   // fuera del cerrojo: la pantalla puede ir copiando mientras
                    lock (cerrojo)
                    {
                        if (parar) return;
                        if (n <= 0) { fin = true; Monitor.PulseAll(cerrojo); return; }
                        int len = anillo.Length, p = ini + lleno; if (p >= len) p -= len;
                        for (int i = 0; i < n; i++) { anillo[p] = trozo[i]; if (++p == len) p = 0; }
                        lleno += n; atascada = false;
                        Monitor.PulseAll(cerrojo);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (cerrojo) { fallo = ex; fin = true; Monitor.PulseAll(cerrojo); }
            }
        }

        // Para el hilo y lo espera (como mucho 1 s: si se quedo colgado en el disco, se sigue sin el).
        public void Dispose()
        {
            Thread h;
            lock (cerrojo) { parar = true; Monitor.PulseAll(cerrojo); h = hilo; }
            if (h != null && h != Thread.CurrentThread) { try { h.Join(1000); } catch { } }
        }
    }

    public partial class Form1
    {
        private BombaSonido bomba;   // la de la salida clasica que suena (null con WASAPI o sin musica)

        // La salida clasica lee de aqui: la cadena la pasa el hilo de la musica (BombaSonido).
        private ISampleProvider ConBomba(ISampleProvider cadena)
        {
            SueltaBomba();
            bomba = new BombaSonido(cadena);
            return bomba;
        }
        private void SueltaBomba()
        {
            BombaSonido b = bomba; bomba = null;
            if (b != null) { try { b.Dispose(); } catch { } }
        }
        // Lo que la bomba lee por delante de lo que ya pidio la salida (0 sin bomba).
        private int AdelantoBomba() { BombaSonido b = bomba; return (b != null) ? b.AdelantoMs : 0; }
    }
}
