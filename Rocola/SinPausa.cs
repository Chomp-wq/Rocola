using System;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // ==============================================================================================
    //  UNA CANCION DETRAS DE OTRA, SIN PAUSA, POR LA MISMA SALIDA
    //
    //  Antes, al acabar una cancion se cerraba la salida de audio, se abria la siguiente y se volvia a abrir
    //  la salida con su colchon: entre cancion y cancion quedaba un silencio que no estaba en la musica.
    //  Ahora la salida se queda abierta. Cuando la cadena de la que suena se acaba -con sus colas ya
    //  entregadas: realce, limitador, fundido-, en la MISMA lectura se sigue con la cadena de la siguiente,
    //  armada de antemano. Tasas distintas pueden conservar esta salida mediante sinc, solo con la MISMA
    //  disposicion conocida de altavoces y dentro del presupuesto de trabajo; formatos iguales van directos.
    //  Otros layouts/canales, disposiciones desconocidas, o una siguiente aun no lista terminan como siempre
    //  y reabren apropiadamente su salida. Nunca se mezclan canales ni se inventa un upmix.
    //
    //  La cadena nueva arranca con su CEBADO: el limitador, el realce y el des-recorte retrasan la senal y
    //  sus primeros cuadros son silencio (el realce, a lo sumo su pre-oscilacion, por debajo de 1e-6). Se
    //  descarta, para que la primera muestra de la siguiente vaya justo detras de la ultima de la anterior.
    //  Nada se mezcla ni se solapa: es un relevo, no un fundido cruzado.
    //
    //  La cola, el letrero y los creditos NO se tocan aqui. Este objeto solo apunta QUE cadena empezo, en
    //  que cuadro y cuando; la pantalla lo recoge por reloj del dispositivo cuando esta disponible, o por
    //  el colchon estimado como respaldo XP. Un fallo al leer tampoco revienta la salida: se apunta y acaba
    //  acabar, para que la pantalla lo atienda UNA sola vez.
    //
    //  Hilos: Read va en el hilo del sonido; Prepara, Cancela y Toma* en el de la pantalla. Lo compartido
    //  pasa por un candado corto. Leer no reserva memoria: lo que hace falta se reserva al preparar.
    // ==============================================================================================
    // Conversion SOLO para relevar una pista de otra tasa con la misma disposicion de altavoces.
    // Tabla de fases y ventana acotadas, sin COM ni API nueva: tambien .NET 3.5/XP.
    // El sinc centrado usa como mucho 96 cuadros futuros (multiplicados al bajar tasa); no agrega cebado.
    // No mezcla canales, no altera pistas del mismo formato y entrega ceil(entrada * salida / entradaHz).
    internal sealed class RemuestreoContinuo : ISampleProvider
    {
        private const int Fases = 512, Bloque = 4096;
        private readonly ISampleProvider src;
        private readonly WaveFormat formato;
        private readonly int tasaIn, tasaOut, ch, mitad, taps, capacidad;
        private readonly float[] anillo, entrada, cuadro;
        private readonly double[] sumas;
        private readonly double[] filtro;
        private long leidos, emitidos, porSaltar;
        private bool fin;
        private int canalCuadro;
        private static readonly object tablaCerrojo = new object();
        private static int tablaIn, tablaOut;
        private static double[] tablaLista;
        private static bool calentando;

        internal static bool TablaPreparada(WaveFormat a, WaveFormat b)
        { return CapturaTabla(a, b) != null; }

        // La referencia se captura bajo el mismo candado que publica el calentador.
        // Otro par puede sustituir la cache despues; esta tabla ya capturada sigue siendo valida.
        internal static double[] CapturaTabla(WaveFormat a, WaveFormat b)
        {
            if (!SePuede(a, b)) return null;
            lock (tablaCerrojo)
            {
                if (tablaLista != null && tablaIn == a.SampleRate && tablaOut == b.SampleRate) return tablaLista;
                if (calentando) return null;
                calentando = true;
            }
            int hzIn = a.SampleRate, hzOut = b.SampleRate;
            try
            {
                if (ThreadPool.QueueUserWorkItem(delegate
                {
                    double[] tabla = null;
                    ThreadPriority prioridad = Thread.CurrentThread.Priority;
                    try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; tabla = CreaFiltro(hzIn, hzOut); }
                    catch { } // si no se pudo preparar, no se encadena ni se corta la musica actual
                    finally
                    {
                        try { Thread.CurrentThread.Priority = prioridad; } catch { }
                        lock (tablaCerrojo) { if (tabla != null) { tablaIn = hzIn; tablaOut = hzOut; tablaLista = tabla; } calentando = false; }
                    }
                })) return null;
            }
            catch { }
            lock (tablaCerrojo) calentando = false;
            return null;
        }

        internal static bool MismoLayout(WaveFormat a, WaveFormat b)
        {
            if (a == null || b == null || a.Channels != b.Channels) return false;
            int ma = FormatoAudio.Mascara(a), mb = FormatoAudio.Mascara(b);
            return ma != 0 && ma == mb;
        }
        internal static bool SePuede(WaveFormat a, WaveFormat b)
        {
            return MismoLayout(a, b) && a.SampleRate >= 8000 && b.SampleRate >= 8000
                && a.SampleRate <= 192000 && b.SampleRate <= 192000 && a.Channels <= 8
                && a.SampleRate <= b.SampleRate * 4L && b.SampleRate <= a.SampleRate * 4L
                && DentroPresupuesto(a, b, Environment.ProcessorCount);
        }
        internal static long PresupuestoTrabajo(int procesadores)
        { return 40000000L * Math.Max(1, procesadores / 2); }
        internal static bool DentroPresupuesto(WaveFormat a, WaveFormat b, int procesadores)
        {
            if (a == null || b == null || a.SampleRate <= 0 || b.SampleRate <= 0 || b.Channels <= 0) return false;
            // Trabajo SOLO de conversion: 40M tap*canal/s en 2C/2T; crece con los procesadores disponibles.
            // No son porcentajes de CPU garantizados ni frena las pistas que van directas, sin convertir.
            long trabajo = (long)Math.Ceiling(96.0 * Math.Max(1.0, (double)a.SampleRate / b.SampleRate))
                           * 2 * b.SampleRate * b.Channels;
            return trabajo <= PresupuestoTrabajo(procesadores);
        }
        internal RemuestreoContinuo(ISampleProvider fuente, WaveFormat destino, int descartaCuadros)
            : this(fuente, destino, descartaCuadros, null, false) { }

        // Camino de produccion: solo usa la tabla capturada ANTES de tomar el lector.
        // Nunca calcula los coeficientes en la UI si otra precarga reemplazo la cache.
        internal RemuestreoContinuo(ISampleProvider fuente, WaveFormat destino, int descartaCuadros, double[] tablaPreparada)
            : this(fuente, destino, descartaCuadros, tablaPreparada, true) { }

        private RemuestreoContinuo(ISampleProvider fuente, WaveFormat destino, int descartaCuadros, double[] tablaPreparada, bool soloPreparada)
        {
            if (fuente == null || !SePuede(fuente.WaveFormat, destino) || descartaCuadros < 0)
                throw new ArgumentException("Formatos no compatibles para continuidad");
            src = fuente; tasaIn = fuente.WaveFormat.SampleRate; tasaOut = destino.SampleRate; ch = destino.Channels;
            formato = FormatoAudio.Flotante(tasaOut, ch, FormatoAudio.Mascara(destino));
            porSaltar = (long)descartaCuadros * ch;
            mitad = (int)Math.Ceiling(96.0 * Math.Max(1.0, (double)tasaIn / tasaOut)); taps = mitad * 2;
            if (soloPreparada && (tablaPreparada == null || tablaPreparada.Length != (Fases + 1) * taps))
                throw new ArgumentException("Falta la tabla de continuidad ya preparada");
            capacidad = Bloque * 2 + taps + 8;
            anillo = new float[capacidad * ch]; entrada = new float[Bloque * ch]; cuadro = new float[ch]; sumas = new double[ch];
            canalCuadro = ch;
            filtro = tablaPreparada;
            if (!soloPreparada)
            {
                lock (tablaCerrojo) if (tablaIn == tasaIn && tablaOut == tasaOut) filtro = tablaLista;
                if (filtro == null) filtro = CreaFiltro(tasaIn, tasaOut); // constructor directo de fixtures, nunca usado por la UI
            }
        }
        private static double[] CreaFiltro(int tasaIn, int tasaOut)
        {
            int mitad = (int)Math.Ceiling(96.0 * Math.Max(1.0, (double)tasaIn / tasaOut)), taps = mitad * 2;
            double[] filtro = new double[(Fases + 1) * taps];
            double corte = 0.47 * Math.Min(1.0, (double)tasaOut / tasaIn);
            for (int fase = 0; fase <= Fases; fase++)
            {
                double fr = (double)fase / Fases, suma = 0;
                for (int j = 0; j < taps; j++)
                {
                    double dt = fr - (j - mitad + 1), u = dt / mitad;
                    double h = 0;
                    if (Math.Abs(u) <= 1)
                    {
                        double a = 2 * corte * dt;
                        double sinc = Math.Abs(a) < 1e-12 ? 1 : Math.Sin(Math.PI * a) / (Math.PI * a);
                        double w = 0.35875 + 0.48829 * Math.Cos(Math.PI * u) + 0.14128 * Math.Cos(2 * Math.PI * u)
                                   + 0.01168 * Math.Cos(3 * Math.PI * u);
                        h = 2 * corte * sinc * w;
                    }
                    filtro[fase * taps + j] = h; suma += h;
                }
                for (int j = 0; j < taps; j++) filtro[fase * taps + j] /= suma;
            }
            return filtro;
        }
        public WaveFormat WaveFormat { get { return formato; } }
        private void Llena(long hasta)
        {
            while (!fin && leidos <= hasta)
            {
                while (porSaltar > 0)
                {
                    int n = src.Read(entrada, 0, (int)Math.Min(porSaltar, entrada.Length));
                    if (n <= 0) { fin = true; return; }
                    porSaltar -= n;
                }
                int got = 0;
                while (got < entrada.Length)
                {
                    int n = src.Read(entrada, got, entrada.Length - got);
                    if (n <= 0) { fin = true; break; }
                    got += n;
                    if (got % ch == 0) break;
                }
                if (got % ch != 0) throw new InvalidOperationException("El origen termino a mitad de un cuadro");
                int frames = got / ch;
                for (int i = 0; i < frames; i++)
                {
                    int p = (int)((leidos + i) % capacidad) * ch;
                    for (int c = 0; c < ch; c++) anillo[p + c] = entrada[i * ch + c];
                }
                leidos += frames;
            }
        }
        private bool Siguiente()
        {
            long num = emitidos * tasaIn, centro = num / tasaOut;
            Llena(centro + mitad);
            long total = (leidos * tasaOut + tasaIn - 1) / tasaIn;
            if (fin && emitidos >= total) return false;
            double fase = (double)(num % tasaOut) * Fases / tasaOut;
            int p0 = (int)fase; double fr = fase - p0;
            int desde = (int)Math.Max(0L, mitad - 1L - centro);
            int hasta = (int)Math.Min((long)taps, leidos - centro + mitad - 1L);
            long primero = centro + desde - mitad + 1;
            if (primero < leidos - capacidad) throw new InvalidOperationException("Ventana de remuestreo fuera del anillo");
            int ring = (int)(primero % capacidad) * ch;
            int h0 = p0 * taps, h1 = h0 + taps;
            // Una fase/interpolacion por tap para todos los canales; un modulo por cuadro, no por tap.
            if (ch == 2)
            {
                double izquierda = 0, derecha = 0;
                for (int j = desde; j < hasta; j++)
                {
                    double h = filtro[h0 + j]; h += (filtro[h1 + j] - h) * fr;
                    izquierda += anillo[ring] * h; derecha += anillo[ring + 1] * h;
                    ring += 2; if (ring == anillo.Length) ring = 0;
                }
                cuadro[0] = (float)izquierda; cuadro[1] = (float)derecha;
            }
            else
            {
                for (int c = 0; c < ch; c++) sumas[c] = 0;
                for (int j = desde; j < hasta; j++)
                {
                    double h = filtro[h0 + j]; h += (filtro[h1 + j] - h) * fr;
                    for (int c = 0; c < ch; c++) sumas[c] += anillo[ring + c] * h;
                    ring += ch; if (ring == anillo.Length) ring = 0;
                }
                for (int c = 0; c < ch; c++) cuadro[c] = (float)sumas[c];
            }
            emitidos++; canalCuadro = 0;
            return true;
        }
        public int Read(float[] b, int o, int n)
        {
            if (b == null || o < 0 || n < 0 || o > b.Length - n) throw new ArgumentOutOfRangeException();
            int hechos = 0;
            while (hechos < n)
            {
                if (canalCuadro == ch && !Siguiente()) break;
                b[o + hechos++] = cuadro[canalCuadro++];
            }
            return hechos;
        }
    }

    internal sealed class Encadenado : ISampleProvider
    {
        internal sealed class Relevo
        {
            internal object Ficha;       // la cancion que empezo (lo que se paso a Prepara)
            internal long Cuadro;        // cuadro de la salida con el que empezo
            internal int Tick;           // Environment.TickCount al empezar a leerla
        }

        private readonly object cerrojo = new object();
        private readonly WaveFormat formato;
        private readonly int ch;
        private ISampleProvider actual;       // solo lo cambia el hilo del sonido (y el constructor)
        private object fichaActual;
        private ISampleProvider siguiente;
        private object fichaSiguiente;
        private int recorteSiguiente;         // cuadros de cebado que se descartan al empezar la siguiente
        private Relevo relevoSiguiente;       // reservado al preparar
        private float[] basuraSiguiente;      // donde se tira el cebado (reservado al preparar)
        private Relevo relevoPendiente;       // empezo y la pantalla aun no lo recogio
        private Exception fallo;
        private object fichaFallo;
        private bool terminado;
        private long muestras;                // muestras entregadas en total
        private int rellenoPendiente;         // silencio que falta para cerrar un cuadro a medias

        internal Encadenado(ISampleProvider primera, object ficha)
        {
            if (primera == null) throw new ArgumentNullException("primera");
            formato = primera.WaveFormat;
            ch = Math.Max(1, formato.Channels);
            actual = primera; fichaActual = ficha;
        }

        public WaveFormat WaveFormat { get { return formato; } }
        internal object FichaActual { get { lock (cerrojo) return fichaActual; } }
        internal long CuadrosEntregados { get { return Interlocked.Read(ref muestras) / ch; } }
        internal bool Terminado { get { lock (cerrojo) return terminado; } }
        internal bool HayPreparada { get { lock (cerrojo) return siguiente != null; } }
        internal bool HayRelevoPendiente { get { lock (cerrojo) return relevoPendiente != null; } }

        // Mismo formato de muestras: la salida no cambia de frecuencia ni de canales a media lectura.
        internal static bool MismoFormato(WaveFormat a, WaveFormat b)
        {
            return RemuestreoContinuo.MismoLayout(a, b) && a.SampleRate == b.SampleRate
                && a.Encoding == b.Encoding && a.BitsPerSample == b.BitsPerSample;
        }

        // Deja lista la siguiente. false si no se puede: otro formato, ya hay una preparada, la anterior aun
        // no se recogio o la secuencia ya termino (entonces va por el camino de siempre).
        internal bool Prepara(ISampleProvider cadena, int recorteCuadros, object ficha)
        {
            if (cadena == null || ficha == null || recorteCuadros < 0 || !MismoFormato(cadena.WaveFormat, formato)) return false;
            float[] basura = recorteCuadros > 0 ? new float[Math.Min(recorteCuadros, 4096) * ch] : null;
            Relevo r = new Relevo();
            r.Ficha = ficha;
            lock (cerrojo)
            {
                if (terminado || siguiente != null || relevoPendiente != null) return false;
                siguiente = cadena; fichaSiguiente = ficha; recorteSiguiente = recorteCuadros;
                relevoSiguiente = r; basuraSiguiente = basura;
                return true;
            }
        }

        // Retira la preparada si aun no empezo. false si ya empezo (o no era esa): entonces suena.
        internal bool Cancela(object ficha)
        {
            lock (cerrojo)
            {
                if (siguiente == null || !object.ReferenceEquals(fichaSiguiente, ficha)) return false;
                siguiente = null; fichaSiguiente = null; relevoSiguiente = null; basuraSiguiente = null;
                return true;
            }
        }

        // El relevo que ya lleva 'espera' ms empezado (lo que tarda en oirse), o null. Se entrega una vez.
        internal Relevo TomaRelevo(int ahora, int espera)
        {
            lock (cerrojo)
            {
                Relevo r = relevoPendiente;
                if (r == null || unchecked(ahora - r.Tick) < espera) return null;
                relevoPendiente = null;
                return r;
            }
        }

        internal Relevo TomaRelevoPorCuadro(long cuadroOido)
        {
            lock (cerrojo)
            {
                Relevo r = relevoPendiente;
                if (r == null || cuadroOido < r.Cuadro) return null;
                relevoPendiente = null; return r;
            }
        }

        // El fallo de lectura, si lo hubo: de que cancion fue y que paso. Se entrega una vez.
        internal bool TomaFallo(out object ficha, out Exception error)
        {
            lock (cerrojo)
            {
                ficha = fichaFallo; error = fallo;
                fichaFallo = null; fallo = null;
                return error != null;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int hechos = 0;
            // Un cuadro que quedo a medias en la lectura anterior se cierra con silencio antes de seguir.
            while (rellenoPendiente > 0 && hechos < count) { buffer[offset + hechos++] = 0f; rellenoPendiente--; }
            while (hechos < count)
            {
                ISampleProvider a = actual;
                if (a == null) break;
                int n;
                try { n = a.Read(buffer, offset + hechos, count - hechos); }
                catch (Exception ex) { Falla(ex); break; }
                if (n > 0) { hechos += n; continue; }
                // Se acabo la que sonaba. Los canales tienen que seguir alineados: si quedo un cuadro a
                // medias (una cadena no deberia dejarlo, pero no se da por hecho), se cierra con silencio.
                int suelto = (int)((muestras + hechos) % ch);
                if (suelto != 0)
                {
                    int falta = ch - suelto;
                    while (falta > 0 && hechos < count) { buffer[offset + hechos++] = 0f; falta--; }
                    if (falta > 0) { rellenoPendiente = falta; break; }
                }
                if (!Releva(hechos)) break;
            }
            if (hechos > 0) Interlocked.Add(ref muestras, hechos);
            return hechos;
        }

        // Pasa a la preparada (si la hay) y descarta su cebado. false si no habia: la secuencia termina.
        private bool Releva(int hechos)
        {
            ISampleProvider nueva; int recorte; float[] basura;
            lock (cerrojo)
            {
                if (siguiente == null) { terminado = true; actual = null; return false; }
                Relevo r = relevoSiguiente;
                r.Cuadro = (muestras + hechos) / ch;
                r.Tick = Environment.TickCount;
                nueva = siguiente; recorte = recorteSiguiente; basura = basuraSiguiente;
                actual = nueva; fichaActual = fichaSiguiente;
                siguiente = null; fichaSiguiente = null; relevoSiguiente = null; basuraSiguiente = null;
                relevoPendiente = r;
            }
            long porTirar = (long)recorte * ch;
            while (porTirar > 0)
            {
                int n;
                try { n = nueva.Read(basura, 0, (int)Math.Min(porTirar, basura.Length)); }
                catch (Exception ex) { Falla(ex); return false; }
                if (n <= 0) break;   // se acabo en el cebado (archivo vacio): la siguiente lectura lo cierra
                porTirar -= n;
            }
            return true;
        }

        private void Falla(Exception ex)
        {
            lock (cerrojo)
            {
                fallo = ex; fichaFallo = fichaActual;
                actual = null; terminado = true;
                siguiente = null; fichaSiguiente = null; relevoSiguiente = null; basuraSiguiente = null;
            }
        }
    }

    // ==============================================================================================
    //  LA MEDIDA EN VIVO DE UNA CANCION QUE ENTRA POR RELEVO
    //
    //  La sonoridad y la huella tonal se aprenden oyendo, bloque a bloque, en campos de la rocola que son de
    //  "la cancion que se esta leyendo". Con dos canciones seguidas por la misma salida, la de detras empieza
    //  a leerse en el hilo del sonido antes de que la pantalla se entere. Lo que cada una debe medir va aqui,
    //  preparado de antemano (con su memoria ya reservada): en el PRIMER bloque de la nueva se guarda lo medido
    //  de la anterior en 'Cierre' y se estrena lo de esta. La pantalla cierra 'Cierre' cuando llega el relevo.
    // ==============================================================================================
    internal sealed class MedidaCancion
    {
        internal sealed class Cierre
        {
            internal volatile bool Listo;         // lo lleno el hilo del sonido: ya se puede cerrar
            internal object Fuente;
            internal string Ruta; internal double SumSq; internal long Muestras; internal int Tasa, Canales;
            internal string EqRuta; internal double[] EqSumas; internal long EqN; internal int EqTasa;
        }
        internal readonly string Ruta;            // sonoridad que se aprende (null = ninguna)
        internal object Fuente;
        internal readonly string EqRuta;          // huella tonal que se aprende (null = ninguna)
        internal readonly double[] EqSumas = new double[8];
        internal readonly Cierre CierreAnterior = new Cierre();
        internal MedidaCancion(string ruta, string eqRuta) { Ruta = ruta; EqRuta = eqRuta; }
    }
}
