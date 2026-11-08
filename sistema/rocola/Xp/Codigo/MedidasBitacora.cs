using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Rocola
{
    // ============================== LO QUE TARDA, EN LA BITACORA ==============================
    //
    // Tres esperas que se notan en el bar y que no se pueden medir fuera de su equipo (su disco, su procesador, su
    // Windows): cuanto espera la ventana a las tablas al arrancar, cuanto tarda la biblioteca en estar lista (y dentro,
    // recorrer las carpetas para saber si la guardada sigue valiendo) y cuanto tarda cada cancion en empezar a sonar.
    // Con "diagnostico=1" en la configuracion quedan en rocola_diagnostico.txt; sin eso no se escribe nada. Tomar los
    // tiempos no cuesta nada (dos lecturas del reloj por cosa) y no cambia lo que hace la rocola.
    public partial class Form1
    {
        private int bitacoraArranque = Environment.TickCount;   // cuando nacio la ventana
        private int bitacoraBiblioteca, bitacoraFirmaMs = -1, bitacoraCancion;
        private bool bitacoraBibliotecaDicha;

        private static int Desde(int tick) { return unchecked(Environment.TickCount - tick); }

        // Al publicar las tablas del arranque.
        private void ApuntaArranque(int leerMs, int esperaMs)
        {
            if (!diagOn) return;
            Diag("arranque: tablas leidas en " + leerMs + " ms (" + loudCache.Count + " canciones con volumen); la ventana espero "
                 + esperaMs + " ms; " + Desde(bitacoraArranque) + " ms desde que nacio la ventana");
        }
        // Al terminar de armar la biblioteca.
        private void ApuntaBiblioteca(bool deCache)
        {
            if (!diagOn) return;
            Diag("biblioteca: lista en " + Desde(bitacoraBiblioteca) + " ms (" + (deCache ? "la guardada" : "recorriendo el disco") + "); "
                 + (bitacoraFirmaMs >= 0 ? "comprobar la guardada (recorrer las carpetas) " + bitacoraFirmaMs + " ms; " : "")
                 + tracks.Count + " canciones, " + albums.Count + " discos"
                 + (bitacoraBibliotecaDicha ? "" : "; " + Desde(bitacoraArranque) + " ms desde que nacio la ventana"));
            bitacoraBibliotecaDicha = true;
        }
        // Cuando la cancion ya suena.
        private void ApuntaCancion(int abiertaMs)
        {
            if (!diagOn) return;
            Diag("cancion: abierta en " + abiertaMs + " ms, sonando en " + Desde(bitacoraCancion) + " ms (en memoria hasta ahora: "
                 + EnMemoriaDelAdelanto + " de la lectura por adelantado, " + EnMemoriaLeidas + " leidas al abrir)" + DiagReservaTexto());
        }
    }

    // ============================== EL VIGIA DE LA PANTALLA ==============================
    //
    // Cuanto tarda la pantalla en atender, medido siempre y dentro de la propia rocola (antes solo se podia medir desde
    // fuera, con un banco de pruebas). Un hilo aparte le da un toque cada tanto (BeginInvoke) y cronometra cuanto tarda
    // en atenderlo. Si pasa del umbral, apunta cuanto duro y QUE estaba haciendo la pantalla: eso lo marcan las entradas
    // grandes al empezar (una tecla, el latido, un cuadro de la tarjeta, empezar una cancion, publicar la biblioteca, el
    // arranque), con una cadena y un reloj, nada mas. Si la marca empezo mas o menos cuando empezo la parada, es eso lo
    // que la paro ("en:"); si empezo mucho antes, la pantalla ya lo habia terminado y la paro otra cosa que vino despues,
    // sin marca ("despues de:"). Y si la pantalla lleva diez segundos sin atender, lo dice sin esperar a que vuelva.
    internal sealed class VigiaPantalla
    {
        private static volatile string hace;   // lo ultimo que empezo la pantalla (lo marca ella misma)
        private static int haceTick;
        // Y las ultimas ocho marcas con su hora fina (24-10-2026): el diagnostico completo dice, de cada tiron, que marco la pantalla en el
        // hueco antes del cuadro. Solo la pantalla marca, asi que no hace falta cerrojo; son dos escrituras en arreglos ya hechos.
        private static readonly string[] marcas = new string[8];
        private static readonly long[] marcasTs = new long[8];
        private static int marcaN;
        internal static void Hace(string que)
        {
            haceTick = Environment.TickCount; hace = que;
            int i = marcaN & 7; marcas[i] = que; marcasTs[i] = System.Diagnostics.Stopwatch.GetTimestamp(); marcaN++;
        }
        // La ultima marca con hora entre 'desdeTs' y 'hastaTs' (marcas del reloj fino) que no sea 'salvo', o null.
        internal static string MarcaEntre(long desdeTs, long hastaTs, string salvo, out long cuandoTs)
        {
            cuandoTs = 0;
            for (int k = 1; k <= 8 && k <= marcaN; k++)
            {
                int i = (marcaN - k) & 7; long t = marcasTs[i];
                if (t > hastaTs || string.Equals(marcas[i], salvo)) continue;
                if (t < desdeTs) return null;
                cuandoTs = t; return marcas[i];
            }
            return null;
        }
        internal const string MarcaCuadro = "un cuadro de la tarjeta";   // la que pone cada cuadro al empezar (no es del hueco)
        internal static string QueHace(int llevaMs)
        {
            string q = hace;
            if (q == null) return "sin nada marcado";
            int desde = unchecked(Environment.TickCount - haceTick);
            return (desde <= llevaMs + 60 ? "en: " : "despues de: ") + q + " (empezo hace " + desde + " ms)";
        }

        internal const int SigueParadaMs = 10000;   // sin atender tanto: se dice ya, sin esperar a que vuelva
        private readonly Control pantalla;
        private readonly int umbral, paso;
        private readonly Action<int, string> avisa;
        private volatile bool parar;
        private Thread hilo;

        // 'avisa' corre en el hilo del vigia: tiene que ser barato y no tocar la pantalla.
        internal VigiaPantalla(Control pantalla, int umbralMs, int pasoMs, Action<int, string> avisa)
        { this.pantalla = pantalla; umbral = umbralMs; paso = pasoMs; this.avisa = avisa; }

        internal void Arranca()
        {
            if (hilo != null) return;
            hilo = new Thread(PrioridadHilo.Con(Bucle));   // EN LINUX: (new Thread(X)) nace en la prioridad normal del programa, como en Windows: en Linux heredaria la del hilo que lo crea (Nativo/PrioridadHilo.cs)
            hilo.IsBackground = true; hilo.Name = "vigia de la pantalla";
            hilo.Start();
        }
        internal void Para() { parar = true; }

        private void Bucle()
        {
            HilosRocola.Nombra("vigia de la pantalla");
            ManualResetEvent atendido = new ManualResetEvent(false);
            MethodInvoker toque = delegate { atendido.Set(); };
            while (!parar)
            {
                Thread.Sleep(paso);
                if (parar) break;
                atendido.Reset();
                int t0 = Environment.TickCount;
                try
                {
                    if (pantalla.IsDisposed || !pantalla.IsHandleCreated) continue;
                    pantalla.BeginInvoke(toque);
                }
                catch { continue; }   // cerrandose: no hay a quien preguntar
                string que = null; bool dichoQueSigue = false, cerrada = false;
                while (!atendido.WaitOne(25, false))
                {
                    if (parar) return;
                    if (pantalla.IsDisposed) { cerrada = true; break; }
                    int lleva = unchecked(Environment.TickCount - t0);
                    if (que == null && lleva >= umbral) que = QueHace(lleva);
                    if (!dichoQueSigue && lleva >= SigueParadaMs) { dichoQueSigue = true; try { avisa(lleva, que + " (y sigue parada)"); } catch { } }
                }
                if (cerrada) break;
                int total = unchecked(Environment.TickCount - t0);
                if (total < umbral) continue;
                if (que == null) que = QueHace(total);
                try { avisa(total, que); } catch { }
            }
        }
    }

    public partial class Form1
    {
        private VigiaPantalla vigiaPantalla;
        internal const int ParadaDiagMs = 150, ParadaErrorMs = 500;   // a la bitacora (con diagnostico=1) / a los errores, siempre
        private int paradaErrorTick, paradasSinApuntar;
        private bool paradaApuntada;
        private int paradaGc0, paradaGc1, paradaGc2; private bool paradaGcVisto;   // contadores del recolector en el apunte anterior

        // EN QUE ESTADO ESTABA EL EQUIPO CUANDO LA PANTALLA SE PARO (19-10-2026). Una parada de cientos de milisegundos en el A4 con XP
        // pudo ser el recolector de basura, un hilo de fondo, la memoria o el propio dibujo, y la linea de antes solo decia que
        // hacia la pantalla. Esto agrega lo que se puede mirar sin molestar: cuantas recolecciones hubo desde la parada anterior, la
        // memoria que usa el programa y cuanta RAM fisica y de direcciones le queda al sistema. Un contador del recolector
        // coincidente NO prueba la causa ni dice cuanto duro: solo sirve para descartar o sospechar. Se calcula en el hilo del vigia
        // y solo cuando hay una parada que apuntar (rara): la pantalla no paga nada.
        internal string ContextoDeParada()
        {
            try
            {
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                string gc = !paradaGcVisto ? "gc_totales=" + g0 + "/" + g1 + "/" + g2
                                          : "gc_desde_el_apunte_anterior=" + (g0 - paradaGc0) + "/" + (g1 - paradaGc1) + "/" + (g2 - paradaGc2);
                paradaGc0 = g0; paradaGc1 = g1; paradaGc2 = g2; paradaGcVisto = true;
                string mem = "";
                try
                {
                    System.Diagnostics.Process yo = System.Diagnostics.Process.GetCurrentProcess();
                    try { mem = "; trabajo=" + (yo.WorkingSet64 >> 20) + " MB, privada=" + (yo.PrivateMemorySize64 >> 20) + " MB, monton_gestionado=" + (GC.GetTotalMemory(false) >> 20) + " MB, hilos=" + yo.Threads.Count; }
                    finally { yo.Dispose(); }
                }
                catch { }
                string sis = "";
                try
                {
                    ulong totalPhys, availPhys, totalVirtual, availVirtual; uint memoryLoad;   // EN LINUX: (MemStatusEx y su Length) lo que se pregunta
                    if (Equipo.Memoria(out totalPhys, out availPhys, out memoryLoad))   // EN LINUX: (GlobalMemoryStatusEx) /proc/meminfo
                    {
                        Equipo.Direcciones(out totalVirtual, out availVirtual);   // EN LINUX: (ullTotalVirtual, ullAvailVirtual) /proc/self/status
                        sis = "; ram_libre=" + (availPhys >> 20) + " de " + (totalPhys >> 20) + " MB (carga " + memoryLoad + " %), direcciones_libres=" + (availVirtual >> 20) + " MB";
                    }
                }
                catch { }
                return "; " + gc + mem + sis;
            }
            catch { return ""; }
        }

        private void ArrancaVigia()
        {
            if (vigiaPantalla != null) return;
            // Un toque cada 25 ms: una parada se mide con 25 ms de margen como mucho (con 100 se escapaban paradas de 200 que
            // empezaban justo antes de un toque). Son unos 40 mensajes por segundo: nada para la pantalla.
            vigiaPantalla = new VigiaPantalla(this, ParadaDiagMs, 25, ApuntaParada);
            vigiaPantalla.Arranca();
        }

        // En el hilo del vigia. Diag y LogError solo dejan la linea en la cola de la bitacora: no esperan al disco.
        private void ApuntaParada(int ms, string que)
        {
            string contexto = ms >= ParadaDiagMs ? ContextoDeParada() : "";
            if (ms >= ParadaDiagMs) Diag("pantalla parada " + ms + " ms, " + que + contexto);
            if (ms < ParadaErrorMs) return;
            // Al archivo de errores siempre, pero sin inundarlo: como mucho una cada diez segundos, diciendo cuantas se callaron.
            int ahora = Environment.TickCount;
            if (paradaApuntada && unchecked(ahora - paradaErrorTick) < 10000) { paradasSinApuntar++; return; }
            string callo = paradasSinApuntar > 0 ? " (y " + paradasSinApuntar + " mas de " + ParadaErrorMs + " ms desde el apunte anterior)" : "";
            paradaApuntada = true; paradaErrorTick = ahora; paradasSinApuntar = 0;
            LogError("Pantalla parada " + ms + " ms, " + que + callo
                + "; repaso_pendiente=" + System.Threading.Thread.VolatileRead(ref preScanLeft)
                + "; miniaturas_pendientes=" + minisPendientes + "; biblioteca_construyendose=" + libBuilding + contexto, null);
        }
    }

    // ============================== LA BITACORA ESCRIBE DETRAS DE LA PANTALLA ==============================
    //
    // rocola_diagnostico.txt (con "diagnostico=1"), rocola_error.txt y rocola_incompatibles.txt se escribian en el hilo que
    // apuntaba, muchas veces el de la pantalla: abrir el archivo, mirar cuanto mide y pegarle la linea. Con el diagnostico
    // encendido son varias lineas por cancion, y en un disco mecanico ocupado cada una puede tardar decenas de milisegundos:
    // la bitacora que sirve para cazar los tirones los provocaba. Ahora quien apunta deja la linea -con su hora de ahora-
    // en una cola y sigue; un hilo de fondo las escribe en el mismo orden, de a tandas. Lo que dice cada archivo, su tope de
    // tamano y el sello de version no cambian (el sello lo saca el hilo que escribe: mirar la fecha del programa tambien es
    // ir al disco). Si el archivo esta ocupado un momento -un antivirus- se reintenta en vez de perder la linea. Al cerrar y
    // ante un fallo que puede terminar el proceso se escribe todo ahi mismo (Vacia).
    internal static class Bitacora
    {
        private sealed class Apunte { public string Ruta, Cabeza, Cuerpo; public long Tope; public bool ConSello; }
        private static readonly object cerrojoCola = new object();    // la cola
        private static readonly object cerrojoDisco = new object();   // quien escribe: uno a la vez
        private static readonly Queue<Apunte> cola = new Queue<Apunte>();
        private static bool trabajando;          // hay un hilo de fondo escribiendo o por empezar
        private static int descartados;          // lo que no cupo en la cola
        private const int MaximoEnCola = 4000;   // si el disco no da abasto, la cola no crece sin limite
        private static string sello;             // "  (rocola del ...)", sacado una vez en el hilo que escribe

        // Una linea tal cual (el diagnostico). Tope: por encima de ese tamano el archivo empieza de nuevo (0: sin tope).
        internal static void Apunta(string ruta, long tope, string texto) { Mete(ruta, tope, texto, null, false); }
        // Un apunte de error: la cabeza, el sello de version y el cuerpo, como siempre.
        internal static void ApuntaConSello(string ruta, long tope, string cabeza, string cuerpo) { Mete(ruta, tope, cabeza, cuerpo, true); }

        private static void Mete(string ruta, long tope, string cabeza, string cuerpo, bool conSello)
        {
            if (string.IsNullOrEmpty(ruta)) return;
            Apunte a = new Apunte(); a.Ruta = ruta; a.Tope = tope; a.Cabeza = cabeza ?? ""; a.Cuerpo = cuerpo; a.ConSello = conSello;
            lock (cerrojoCola)
            {
                if (cola.Count >= MaximoEnCola) { descartados++; return; }
                cola.Enqueue(a);
                if (trabajando) return;
                trabajando = true;
            }
            bool encolado = false;
            try { encolado = ThreadPool.QueueUserWorkItem(Trabaja); } catch { }
            if (!encolado) lock (cerrojoCola) trabajando = false;   // sin hilos (al cerrar): queda en la cola para Vacia
        }

        private static void Trabaja(object nada)
        {
            while (true)
            {
                try { lock (cerrojoDisco) EscribeLoPendiente(); } catch { }
                lock (cerrojoCola) { if (cola.Count == 0) { trabajando = false; return; } }
            }
        }

        // Escribe ahora, en este hilo, todo lo pendiente: al cerrar, ante un fallo grave y en las pruebas. Espera como mucho
        // 'ms' a que acabe lo que estuviera escribiendo el hilo de fondo.
        internal static bool Vacia(int ms)
        {
            if (!Monitor.TryEnter(cerrojoDisco, ms)) return false;
            try { EscribeLoPendiente(); }
            catch { }
            finally { Monitor.Exit(cerrojoDisco); }
            return true;
        }

        // Con cerrojoDisco tomado. Lo seguido que va al mismo archivo sale de una sola vez.
        private static void EscribeLoPendiente()
        {
            Apunte[] tanda; int perdidos;
            lock (cerrojoCola)
            {
                if (cola.Count == 0) return;
                tanda = cola.ToArray(); cola.Clear();
                perdidos = descartados; descartados = 0;
            }
            if (sello == null)
            {
                string s = "";
                try { s = Form1.DeCuandoEs(); } catch { }
                sello = s.Length > 0 ? "  (rocola del " + s + ")" : "";
            }
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < tanda.Length)
            {
                string ruta = tanda[i].Ruta; long tope = tanda[i].Tope;
                sb.Length = 0;
                if (perdidos > 0) { sb.Append("(" + perdidos + " apuntes sin escribir: el disco no daba abasto)\r\n"); perdidos = 0; }
                for (; i < tanda.Length && string.Equals(tanda[i].Ruta, ruta, StringComparison.OrdinalIgnoreCase); i++)
                {
                    Apunte a = tanda[i];
                    sb.Append(a.Cabeza);
                    if (a.ConSello) sb.Append(sello);
                    if (a.Cuerpo != null) sb.Append(a.Cuerpo);
                }
                Pega(ruta, tope, sb.ToString());
            }
        }

        // El tope y el pegado de siempre (UTF-8 sin marca). Si el archivo esta ocupado, se reintenta durante un segundo.
        private static void Pega(string ruta, long tope, string texto)
        {
            if (tope > 0) { try { FileInfo fi = new FileInfo(ruta); if (fi.Exists && fi.Length > tope) fi.Delete(); } catch { } }
            for (int intento = 0; ; intento++)
            {
                try { File.AppendAllText(ruta, texto); return; }
                catch (DirectoryNotFoundException) { return; }
                catch (PathTooLongException) { return; }
                catch (IOException) { if (intento >= 20) return; Thread.Sleep(50); }
                catch { return; }
            }
        }
    }
}
