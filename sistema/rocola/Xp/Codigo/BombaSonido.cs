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
//  - Al parar se invalida la publicacion de esta cancion y se pide terminar al hilo, sin esperar al disco.
//    El propio hilo libera los lectores despues de salir de su ultimo Read; nunca se cierra un lector en uso.
//  - EL ARRANQUE, SIN PARAR LA PANTALLA (29 de septiembre). Al arrancar, WaveOut llena sus 20 bloques de una vez: pide
//    1 s de musica seguido. Con el colchon de 200 ms, la pantalla se quedaba esperando a que el hilo descomprimiera y
//    procesara ese segundo entero (medido: 44 ms en un procesador de servidor, con 20.000 canciones en las tablas;
//    varias veces eso en el equipo del bar), y se veia como un tiron en cada cancion que se ponia. Ahora el hilo
//    empieza en cuanto se arma la salida, deja ese primer segundo hecho en un anillo mas grande y avisa
//    (CuandoEsteLista); la salida arranca entonces y solo copia. Mientras, la pantalla sigue. Despues del arranque, el
//    colchon vuelve a ser de 200 ms, como siempre. Lo que suena no cambia ni una muestra.
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
        internal const int ArranqueMs = 1000;   // lo que la salida clasica llena de una vez al arrancar (20 bloques de 50 ms)

        private readonly ISampleProvider src;
        private readonly object cerrojo = new object();
        private readonly float[] anillo, trozo;
        private int ini, lleno;                  // el colchon: donde empieza y cuanto tiene (muestras)
        private readonly int trozoN;
        private readonly int colchonN, arranqueN;   // lo que se deja hecho sonando, y lo que hace falta para arrancar
        private bool salidaLeyo;                    // la salida ya pidio musica: desde ahi, solo el colchon
        private Action alEstarLista;                // el aviso del primer segundo hecho (una vez)
        private Thread hilo;
        private bool lecturaTerminada, liberacionPedida;
        private Action alParar;
        private bool parar, fin, atascada;
        private Exception fallo;
        private long huecos;                     // bloques que salieron en silencio por un hilo atrasado
        private int hiloLector;                  // el hilo que lee la cadena (para las pruebas)
        internal readonly int AdelantoMs;        // cuanto antes de oirse se lee cada trozo, de media
        internal readonly int ArranqueObjetivoMs, EsperaLecturaMs;

        internal BombaSonido(ISampleProvider s) : this(s, ArranqueMs, EsperaMaxMs) { }
        internal BombaSonido(ISampleProvider s, int arranqueMs, int esperaMs)
        {
            if (s == null) throw new ArgumentNullException("s");
            src = s;
            ArranqueObjetivoMs = Math.Max(ColchonMs, Math.Min(ArranqueMs, arranqueMs));
            EsperaLecturaMs = Math.Max(100, Math.Min(EsperaMaxMs, esperaMs));
            WaveFormat f = s.WaveFormat;
            int ch = Math.Max(1, f.Channels), hz = Math.Max(8000, f.SampleRate);
            int cuadrosTrozo = Math.Max(64, hz * TrozoMs / 1000);
            int cuadrosColchon = Math.Max(cuadrosTrozo * 4, hz * ColchonMs / 1000);
            int cuadrosArranque = hz * ArranqueObjetivoMs / 1000;
            trozoN = cuadrosTrozo * ch;
            colchonN = cuadrosColchon * ch;
            arranqueN = cuadrosArranque * ch;
            // El anillo cabe el primer segundo (y dos trozos de holgura); sonando se usa solo el colchon.
            anillo = new float[Math.Max(cuadrosColchon, cuadrosArranque + 2 * cuadrosTrozo) * ch];
            trozo = new float[trozoN];
            // El hilo lee un trozo en cuanto cabe: cada trozo entra con el colchon entre medio lleno de un trozo y lleno.
            AdelantoMs = (int)((cuadrosColchon - cuadrosTrozo * 3L / 2) * 1000 / hz);
        }

        public WaveFormat WaveFormat { get { return src.WaveFormat; } }
        internal long Huecos { get { lock (cerrojo) return huecos; } }
        internal bool SinMusicaTemporal { get { lock (cerrojo) return atascada; } }
        internal int HiloLector { get { lock (cerrojo) return hiloLector; } }
        internal bool Viva { get { Thread h; lock (cerrojo) h = hilo; return h != null && h.IsAlive; } }
        internal bool Lista { get { lock (cerrojo) return fin || lleno >= arranqueN; } }   // el primer segundo ya esta hecho
        internal int Hecho { get { lock (cerrojo) return lleno; } }                         // muestras en el anillo (pruebas)

        // EL AVISO DEL ARRANQUE. Pone el hilo a trabajar YA (sin esperar a que la salida pida) y avisa, una vez y desde el
        // hilo de la musica, cuando el primer segundo esta hecho, o antes si la cancion se acabo o fallo: entonces la
        // salida arranca y solo copia. Si ya lo esta (o la salida ya empezo a pedir), avisa aqui mismo. El aviso no debe
        // lanzar: corre en el hilo de la musica.
        internal void CuandoEsteLista(Action lista)
        {
            if (lista == null) return;
            bool ya;
            lock (cerrojo)
            {
                if (parar) return;
                if (hilo == null) Arranca();
                ya = fin || salidaLeyo || lleno >= arranqueN;
                if (!ya) alEstarLista = lista;
            }
            if (ya) lista();
        }
        // Bajo el cerrojo: el aviso, si ya toca (una sola vez).
        private Action TomaAviso(bool acabo)
        {
            if (alEstarLista == null || (!acabo && lleno < arranqueN)) return null;
            Action a = alEstarLista; alEstarLista = null;
            return a;
        }
        // Hasta donde llena el hilo: el primer segundo mientras la salida no ha pedido nada; despues, el colchon.
        private int Hasta() { return salidaLeyo ? colchonN : anillo.Length; }

        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count)
        {
            lock (cerrojo)
            {
                if (parar) return 0;
                if (hilo == null && !parar) Arranca();
                salidaLeyo = true; alEstarLista = null;   // la salida ya arranco: desde aqui, el colchon de siempre
                int hechos = 0;
                int limite = Environment.TickCount + EsperaLecturaMs;
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
            Thread h = new Thread(PrioridadHilo.Con(Bombea));   // EN LINUX: (new Thread(X)) con la prioridad que XP le pone antes de Start: en Linux .NET no la aplica (Nativo/PrioridadHilo.cs)
            h.IsBackground = true;
            h.Name = "Rocola: la musica";
            try { PrioridadHilo.Pon(h, ThreadPriority.AboveNormal); } catch { }   // por delante de la pantalla: la musica no se puede cortar   // EN LINUX: (h.Priority = ...) la prioridad, apuntada para ponerla al arrancar (con NativeAOT, .NET no la devuelve: Nativo/PrioridadHilo.cs)
            hilo = h;
            h.Start();
        }

        private void Bombea()
        {
            lock (cerrojo) hiloLector = Thread.CurrentThread.ManagedThreadId;
            HilosRocola.Nombra("audio (hilo de la musica)");   // para el reparto por hilo del diagnostico completo
            try
            {
                while (true)
                {
                    lock (cerrojo)
                    {
                        while (!parar && Hasta() - lleno < trozoN) Monitor.Wait(cerrojo);
                        if (parar) return;
                    }
                    int n = src.Read(trozo, 0, trozoN);   // fuera del cerrojo: la pantalla puede ir copiando mientras
                    Action lista;
                    lock (cerrojo)
                    {
                        if (parar) return;
                        if (n <= 0) { fin = true; lista = TomaAviso(true); Monitor.PulseAll(cerrojo); }
                        else
                        {
                            int len = anillo.Length, p = ini + lleno; if (p >= len) p -= len;
                            for (int i = 0; i < n; i++) { anillo[p] = trozo[i]; if (++p == len) p = 0; }
                            lleno += n; atascada = false;
                            lista = TomaAviso(false);
                            Monitor.PulseAll(cerrojo);
                        }
                    }
                    if (lista != null) lista();   // fuera del cerrojo
                    if (n <= 0) return;
                }
            }
            catch (Exception ex)
            {
                Action lista;
                lock (cerrojo) { fallo = parar ? null : ex; fin = true; lista = parar ? null : TomaAviso(true); Monitor.PulseAll(cerrojo); }
                if (lista != null) lista();   // la salida arranca igual: el fallo le llega al leer, como antes
            }
            finally
            {
                Action libera;
                lock (cerrojo) { lecturaTerminada = true; libera = alParar; alParar = null; Monitor.PulseAll(cerrojo); }
                Libera(libera);   // el ultimo Read ya termino: sus recursos pueden cerrarse sin competir con el lector
            }
        }

        private static void Libera(Action libera) { if (libera != null) try { libera(); } catch { } }

        // Cancelacion inmediata. Si Read sigue esperando al disco, conserva sus lectores hasta su finally.
        // Si ya termino (o nunca empezo), libera en segundo plano: ni un Dispose lento detiene la pantalla.
        internal void CuandoPare(Action libera)
        {
            Action ya = null;
            lock (cerrojo)
            {
                parar = true; lleno = 0; alEstarLista = null;
                if (libera != null && !liberacionPedida)
                {
                    liberacionPedida = true;
                    if (hilo == null || lecturaTerminada) ya = libera; else alParar = libera;
                }
                Monitor.PulseAll(cerrojo);
            }
            if (ya != null)
            {
                Action pendiente = ya;
                try { if (ThreadPool.QueueUserWorkItem(delegate { Libera(pendiente); })) return; } catch { }
                Libera(pendiente);   // si el runtime no admite mas trabajos, conserva al menos la propiedad segura
            }
        }
        public void Dispose() { CuandoPare(null); }
    }

    public partial class Form1
    {
        private BombaSonido bomba;   // la de la salida clasica que suena (null con WASAPI o sin musica)
        private long publicacionAudio;

        // Calculo de audio separado de la cola visual: la UI no espera filtros por muestra.
        // Orden unico si se necesitan ambas puertas: audioDatosLock -> levelLock.
        private readonly object audioDatosLock = new object();
        // La misma puerta permite cerrar su medida
        // e invalidarla antes de empezar otra cancion: un bloque de un lector lento no contamina la nueva.
        private void PublicaBloqueDe(LevelTap tap, float[] buffer, int offset, int count, int rate, int canales)
        {
            lock (audioDatosLock)
            {
                if (tap.Publicacion != publicacionAudio) return;
                MedidaDe(tap);
                ReportAudioBlock(buffer, offset, count, rate, canales);
            }
        }
        private void InvalidaPublicacionAudio() { lock (audioDatosLock) { unchecked { publicacionAudio++; } } }
        private static void LiberaLectoresAudio(IAudioSource actual, IAudioSource preparada)
        {
            if (actual != null) try { actual.Dispose(); } catch { }
            if (preparada != null && !object.ReferenceEquals(actual, preparada)) try { preparada.Dispose(); } catch { }
        }
        private static void SueltaLectoresAudio(BombaSonido anterior, IAudioSource actual, IAudioSource preparada)
        {
            if (anterior != null) anterior.CuandoPare(delegate { LiberaLectoresAudio(actual, preparada); });
            else LiberaLectoresAudio(actual, preparada);   // WASAPI ya detuvo su propio lector con output.Dispose
        }

        // La salida clasica lee de aqui: la cadena la pasa el hilo de la musica (BombaSonido).
        private ISampleProvider ConBomba(ISampleProvider cadena)
        {
            SueltaBomba();
            cortesApuntados = 0;   // salida nueva, cuenta nueva (caja negra, SalidaSonido.cs)
            int ms = latenciaAbiertaMs > 0 ? latenciaAbiertaMs : BombaSonido.ArranqueMs;
            bomba = new BombaSonido(MideLatenciaSalida(cadena, false), ms, ms * 3 / 5);
            return bomba;
        }
        private void SueltaBomba()
        {
            BombaSonido b = bomba; bomba = null;
            if (b != null) { try { b.Dispose(); } catch { } }
        }
        // Lo que la bomba lee por delante de lo que ya pidio la salida (0 sin bomba).
        private int AdelantoBomba() { BombaSonido b = bomba; return (b != null) ? b.AdelantoMs : 0; }

        // EL ARRANQUE DE LA SALIDA CLASICA, SIN PARAR LA PANTALLA. En vez de llamar a Play ya (que espera a que el hilo
        // de la musica haga el primer segundo entero), se le pide al hilo que avise cuando lo tenga, y 'arranca' se hace
        // entonces en el hilo de la pantalla. Si mientras tanto se salto o se paro la cancion (otra salida u otra bomba),
        // no se hace nada. Sin ventana a la que volver (y sin prueba que lo pida) devuelve false: se arranca como antes.
        internal Action<Action> vuelveALaPantalla = null;   // solo las pruebas (null = BeginInvoke); EN LINUX: Action (en XP, MethodInvoker de WinForms: el mismo delegado sin argumentos)
        private bool ArrancaCuandoEsteLista(Action arranca)   // EN LINUX: Action (en XP, MethodInvoker de WinForms: el mismo delegado sin argumentos)
        {
            BombaSonido b = bomba; IWavePlayer o = output;
            Action<Action> vuelve = vuelveALaPantalla;   // EN LINUX: Action (en XP, MethodInvoker de WinForms: el mismo delegado sin argumentos)
            if (b == null || o == null || arranca == null || (vuelve == null && !IsHandleCreated)) return false;
            b.CuandoEsteLista(delegate
            {
                Action enLaPantalla = delegate   // EN LINUX: Action (en XP, MethodInvoker de WinForms: el mismo delegado sin argumentos)
                {
                    if (!object.ReferenceEquals(b, bomba) || !object.ReferenceEquals(o, output)) return;   // se salto o se paro mientras
                    arranca();
                };
                if (vuelve != null) { vuelve(enLaPantalla); return; }
                try { PantallaRocola.Encola(this, enLaPantalla); }   // EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)
                catch (InvalidOperationException) { }   // la ventana ya se cerro: no queda nada que arrancar
            });
            return true;
        }
        // Lo que hace StartOpenedTrack al arrancar la salida clasica, cuando se arranca aqui: el primer segundo ya esta
        // hecho, asi que Play solo copia. Los mismos pasos y en el mismo orden.
        private void SalidaClasicaLista(Track t, double desde, int abiertaMs)
        {
            try
            {
                try { output.Play(); }
                catch (Exception pex) { if (EsFalloDelDispositivo(pex) && SalidaCaida(t, desde, pex, false)) return; throw; }
                SalidaArranco(IdSalidaPorDefecto());   // el aparato esta: en cual (SalidaSonido.cs)
                progressTimer.Start();
                UpdateNowPlayingUI();
                PrecalientaSiguiente();
                ApuntaCancion(abiertaMs);
            }
            catch (Exception ex) { AudioStartFailed(t, ex); }
        }
    }
}
