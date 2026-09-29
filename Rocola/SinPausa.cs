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
    //  armada de antemano. Si no hay siguiente lista, o no es del mismo formato (frecuencia y canales), se
    //  acaba como siempre y la salida se cierra: el camino de siempre queda de respaldo.
    //
    //  La cadena nueva arranca con su CEBADO: el limitador, el realce y el des-recorte retrasan la senal y
    //  sus primeros cuadros son silencio (el realce, a lo sumo su pre-oscilacion, por debajo de 1e-6). Se
    //  descarta, para que la primera muestra de la siguiente vaya justo detras de la ultima de la anterior.
    //  Nada se mezcla ni se solapa: es un relevo, no un fundido cruzado.
    //
    //  La cola, el letrero y los creditos NO se tocan aqui. Este objeto solo apunta QUE cadena empezo, en
    //  que cuadro y cuando; la pantalla lo recoge cuando de verdad suena (el colchon de la salida lo
    //  retrasa). Un fallo al leer tampoco revienta la salida: se apunta de quien fue y se termina como al
    //  acabar, para que la pantalla lo atienda UNA sola vez.
    //
    //  Hilos: Read va en el hilo del sonido; Prepara, Cancela y Toma* en el de la pantalla. Lo compartido
    //  pasa por un candado corto. Leer no reserva memoria: lo que hace falta se reserva al preparar.
    // ==============================================================================================
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
            return a != null && b != null && a.SampleRate == b.SampleRate && a.Channels == b.Channels
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
