// SPOTIFY EN LA ROCOLA (fase 11, grupo spotify-xp): LA FUENTE DE SONIDO, una cancion de Spotify para la cadena de XP. Documento:
// FASE11_spotify-xp.md.
//
// Va DENTRO de Form1 porque IAudioSource es privada de Form1 (Codigo/Form1.Sonido.cs:1679). La abre OpenAudio de XP para una ruta
// «spotify:track:ID» (gancho g13: Codigo/Form1.Sonido.cs:1456) y la cadena de XP la trata como un archivo mas: el nivelado de las no medidas,
// el local, el billar, el limitador y la salida (R7). Lo que XP le hace a un ARCHIVO no va (el relleno del codificador, saltar silencios,
// el sello del archivo): OpenAudio devuelve esta fuente antes de todo eso.
//
// Del programa anterior (sistema/rocola/Motor/Spotify.cs, FuenteSpotify y SpotifyEnVivo, 6 y 7-11-2026) se COPIA la idea, no el codigo ni
// el enlace: el hilo del sonido toma lo que haya sin esperar nunca y rellena con silencio lo que no llego (la red que tarda); la cancion se
// da por acabada al llegar a su final de verdad (aqui: la frontera que marca el hilo de Spotify al ver que Soloist paso a otra, o su
// duracion), y si se atasca a media cancion se acaba. Lo nuevo: el sonido llega por el anillo (I1) y no por una tuberia, y el ritmo lo
// marca esta fuente (la tarjeta de la rocola).
//
// EL ORDEN DE UNA CANCION (todo en el hilo del sonido, sin esperar):
//   1. al abrirse no hace nada (XP la puede abrir antes de tiempo; aqui no se pide nada a Soloist)
//   2. la primera lectura: tira lo viejo del anillo, crea su peticion y la deja marcada (RocolaOsSpotify.Pide); el hilo de Spotify manda
//      activate (si hace falta), shuffle y repeat apagados y play con su uri, y apunta donde iba el anillo (EscritoAlPedir)
//   3. cuando Soloist dice que toca la suya (fase «Suena»), desde EscritoAlPedir lo que llega es suyo: se copia; lo que falta, silencio
//   4. acaba al llegar a su frontera o a su duracion (R4); o, sin sonar en 15 s, con el credito devuelto
//   5. al acabar devuelve lo ultimo (o 0: fin de la cancion para XP) y deja la peticion «acabada»: el hilo de Spotify la remata (pause para
//      que Soloist no siga solo, el credito si toca y la bitacora). A XP nunca le lanza un error: si falla, el credito se devuelve por el
//      camino de XP (RefundCredit y su mismo aviso, RocolaOsSpotifyDevuelve) y XP sigue con la siguiente como con una cancion que termino.
using System;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        private sealed class RocolaOsFuenteSpotify : IAudioSource
        {
            internal const int NoEmpiezaMs = 15000;      // R4: no llega a sonar en 15 s desde el play
            internal const int SeguridadMs = 30000;      // (por si el hilo de Spotify no contestara: 30 s sin nada, se acaba)
            internal const double PocoSonidoSeg = 10.0;   // R4: un fallo con menos de 10 s de sonido devuelve el credito
            // (revision «sonido-hilos-y-fallos») EL ARRANQUE SIN HUECO. Al abrirse la salida, el hilo de la musica de XP (BombaSonido) lee de
            // golpe su primer segundo y su colchon (ArranqueMs + ColchonMs) y despues va al paso de la tarjeta. El anillo solo guarda lo que
            // Soloist deja por delante (su tlength, unos 250 ms): si Soloist empieza DURANTE esa lectura de golpe, el anillo se seca y queda un
            // hueco de silencio dentro del primer segundo de la cancion (medido con el Soloist falso: 100 ms a los 0,8 s). Por eso la cancion
            // no empieza a leerse del anillo hasta que la fuente ya entrego eso (en silencio); con Soloist tardando lo de siempre en empezar
            // (la red) no cambia nada. Lo que llega mientras se queda en el anillo: no se pierde nada.
            internal const int ArranqueSinHuecoMs = BombaSonido.ArranqueMs + BombaSonido.ColchonMs + 100;
            private readonly string uri, titulo;
            private readonly int duracionCatalogoMs;
            private readonly NAudio.Wave.WaveFormat formato;
            private readonly int hz;
            private volatile float volumen = 1f;
            private RocolaOsSpotifyPeticion p;
            private bool acabada, saltado;
            private long inicioMs;
            private long cuadrosEntregados;     // lo que se le dio a XP (sonido y silencio): CurrentTime
            private long cuadrosSonido;         // lo que de verdad llego de Soloist
            private long inicioCancion = -1;    // el cuadro del anillo donde empieza (EscritoAlPedir)
            private long silencioAntes;         // cuadros de silencio antes del primer sonido (para TotalTime)
            private long ultimoSonidoMs;
            // (conversion de frecuencia, si el anillo trae otra que la declarada: revision «sonido-hilos-y-fallos»; antes se perdia un cuadro
            // en cada lectura -un chasquido cada 20 a 100 ms-: ahora lo que no se uso se queda para la siguiente)
            private float[] tmp;                // los cuadros del anillo que aun hacen falta (los primeros «tmpN»)
            private int tmpN;
            private double fase;                // la posicion fraccionaria dentro de tmp
            private int contado;                // (1 mientras cuenta en LectoresAbiertos; Interlocked: lo sueltan Acaba y Dispose)

            internal RocolaOsFuenteSpotify(string uri, string titulo, int duracionMs)
            {
                this.uri = uri; this.titulo = titulo; duracionCatalogoMs = duracionMs;
                RocolaOsSpotifyAnillo a = RocolaOsSpotify.Anillo;
                int f = a != null ? a.Frecuencia : 0;
                // la del anillo; sin flujo (0: Soloist aun no abrio el suyo, o lo cierra entre canciones), la ULTIMA que tuvo (revision: asi la
                // declarada es la de verdad y XP la convierte como con un archivo); sin ninguna, 44,1 kHz (la de Spotify)
                if (!(f >= 8000 && f <= 192000)) f = Volatile.Read(ref RocolaOsSpotify.UltimaFrecuencia);
                hz = (f >= 8000 && f <= 192000) ? f : 44100;
                formato = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(hz, 2);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return formato; } }
            public float Volume { set { volumen = value; } }
            public TimeSpan CurrentTime { get { return TimeSpan.FromSeconds(Interlocked.Read(ref cuadrosEntregados) / (double)hz); } }
            // LO QUE DURA: 0 hasta que suena (asi XP no le pone fundido de salida: su silencio de espera al principio lo correria antes de tiempo;
            // la cancion de Spotify trae su propio final); despues, su duracion mas lo que espero en silencio (para la barra de lo que suena)
            public TimeSpan TotalTime
            {
                get
                {
                    RocolaOsSpotifyPeticion q = p;
                    int ms = q != null && Volatile.Read(ref q.DuracionMs) > 0 ? Volatile.Read(ref q.DuracionMs) : duracionCatalogoMs;
                    if (Interlocked.Read(ref cuadrosSonido) == 0 || ms <= 0) return TimeSpan.Zero;
                    return TimeSpan.FromSeconds(ms / 1000.0 + Interlocked.Read(ref silencioAntes) / (double)hz);
                }
            }
            public void Salta(TimeSpan donde) { }   // (retomar a media cancion no se puede: Spotify empieza desde el principio)

            public int Read(float[] buffer, int offset, int count) { return Read(buffer.AsSpan(offset, count)); }
            public int Read(Span<float> d)
            {
                if (acabada) return 0;
                RocolaOsSpotifyAnillo a = RocolaOsSpotify.Anillo;
                long ahora = Environment.TickCount64;
                if (p == null)
                {   // LA PRIMERA LECTURA: ahora si se le pide a Soloist
                    p = new RocolaOsSpotifyPeticion(uri, titulo, duracionCatalogoMs);
                    inicioMs = ultimoSonidoMs = ahora;
                    if (a != null) a.Tira();
                    if (Interlocked.Exchange(ref contado, 1) == 0) Interlocked.Increment(ref RocolaOsSpotify.LectoresAbiertos);
                    RocolaOsSpotify.Pide(p);
                }
                int pedidos = d.Length / 2;
                int n = 0;
                // NADA SE OYE HASTA QUE SOLOIST DICE QUE TOCA ESTA (fase «Suena», la pone el hilo de Spotify): lo que llegue antes al anillo se
                // queda ahi (sin perderse) y, si en ese rato Soloist toco otra cosa, el hilo de Spotify mueve el ancla y eso no se oye (revision:
                // antes se oia todo lo que llegaba desde el play, tambien lo que pusiera un celular en la espera). La fase se lee ANTES que el ancla.
                int fasePedida = Volatile.Read(ref p.Fase);
                long ep = fasePedida >= RocolaOsSpotifyPeticion.Suena ? Interlocked.Read(ref p.EscritoAlPedir) : -1;
                long frontera = Interlocked.Read(ref p.Frontera);
                bool porDuracion = false;
                if (ep >= 0 && a != null && Interlocked.Read(ref cuadrosSonido) == 0 && Interlocked.Read(ref cuadrosEntregados) < (long)hz * ArranqueSinHuecoMs / 1000)
                    a.SaltaVaciado();   // (el arranque sin hueco: aun no; los vaciados si se saltan, como esperando)
                else if (ep >= 0 && a != null)
                {
                    if (!saltado) { a.SaltaA(ep); inicioCancion = ep; saltado = true; }
                    // donde acaba: su frontera (Soloist paso a otra) o su duracion desde que empezo, lo primero
                    long hasta = frontera;
                    int durMs = Volatile.Read(ref p.DuracionMs);
                    if (durMs > 0)
                    {
                        long porDur = inicioCancion + (long)Math.Round(durMs / 1000.0 * FrecuenciaAnillo(a));
                        if (porDur < hasta) { hasta = porDur; porDuracion = true; }
                    }
                    long leidoDespues; bool vaciado;
                    // un vaciado a media cancion (ya sonando) es que Soloist empezo otra cosa (un celular, un salto): ahi acaba, sin oirla
                    bool yaSuena = Interlocked.Read(ref cuadrosSonido) > hz / 2;
                    n = TomaConvertida(a, d, pedidos, hasta, yaSuena, out leidoDespues, out vaciado);
                    if (vaciado && yaSuena) { RocolaOsSpotify.Apunta("vaciado a media cancion en " + uri + ": acaba ahi"); return Acaba(d, 0, RocolaOsSpotifyPeticion.OtraCancion); }
                    if (n > 0)
                    {
                        if (Interlocked.Read(ref cuadrosSonido) == 0) Interlocked.Exchange(ref silencioAntes, Interlocked.Read(ref cuadrosEntregados));
                        Interlocked.Add(ref cuadrosSonido, n);
                        ultimoSonidoMs = ahora;
                    }
                    if (n < pedidos && leidoDespues >= hasta && hasta != long.MaxValue)
                        return Acaba(d, n, porDuracion && hasta != frontera ? RocolaOsSpotifyPeticion.Termino : Volatile.Read(ref p.Motivo));
                }
                else if (Volatile.Read(ref p.Motivo) != 0 && frontera != long.MaxValue)
                    return Acaba(d, 0, Volatile.Read(ref p.Motivo));   // (se acabo antes de mandar play: la rocola se cierra, sin sesion...)
                else if (a != null && fasePedida == RocolaOsSpotifyPeticion.Mandada) a.SaltaVaciado();   // (esperando: sin consumir, pero el vaciado si se salta)
                // sin sonar en 15 s desde que se pidio: se acaba y el credito vuelve (R4); y la seguridad de 30 s sin nada
                if (Interlocked.Read(ref cuadrosSonido) == 0 && ahora - inicioMs > NoEmpiezaMs) return Acaba(d, n, RocolaOsSpotifyPeticion.NoEmpezo);
                if (ahora - ultimoSonidoMs > SeguridadMs) return Acaba(d, n, RocolaOsSpotifyPeticion.SinSonido);
                // lo que no llego, en silencio (la red que tarda); la cancion sigue
                if (n * 2 < d.Length) d.Slice(n * 2).Clear();
                Aplica(d.Slice(0, n * 2));
                Interlocked.Add(ref cuadrosEntregados, pedidos);
                return pedidos * 2;
            }
            private static int FrecuenciaAnillo(RocolaOsSpotifyAnillo a) { int f = a.Frecuencia; return f >= 8000 && f <= 192000 ? f : 44100; }
            // LO QUE HAY EN EL ANILLO, a la frecuencia que se declaro: igual (lo normal) o convertido con una interpolacion lineal (si el anillo
            // trae otra que la declarada al abrirse: Soloist abrio su flujo despues, o lo cambio). La conversion guarda entre lecturas los
            // cuadros que aun hacen falta (tmp/tmpN) y la posicion (fase): la onda sigue sin saltos (revision: antes se perdia un cuadro por
            // lectura, un chasquido cada 20 a 100 ms en toda la cancion).
            private int TomaConvertida(RocolaOsSpotifyAnillo a, Span<float> d, int pedidos, long hasta, bool paraEnVaciado, out long leidoDespues, out bool vaciado)
            {
                int fa = FrecuenciaAnillo(a);
                if (fa == hz) { tmpN = 0; fase = 0; return a.Toma(d.Slice(0, pedidos * 2), hasta, paraEnVaciado, out leidoDespues, out vaciado); }
                double paso = fa / (double)hz;
                // la salida j esta en x = fase + j*paso y necesita los cuadros floor(x) y floor(x)+1
                int necesito = (int)Math.Floor(fase + (pedidos - 1) * paso) + 2;
                if (tmp == null || tmp.Length < necesito * 2) { float[] t2 = new float[necesito * 2 + 64]; if (tmp != null) Array.Copy(tmp, t2, tmpN * 2); tmp = t2; }
                leidoDespues = 0; vaciado = false;
                if (tmpN < necesito)
                {
                    int k = a.Toma(tmp.AsSpan(tmpN * 2, (necesito - tmpN) * 2), hasta, paraEnVaciado, out leidoDespues, out vaciado);
                    if (vaciado && paraEnVaciado) { tmpN = 0; fase = 0; return 0; }
                    if (vaciado) { Array.Copy(tmp, tmpN * 2, tmp, 0, k * 2); tmpN = 0; fase = 0; }   // (lo de antes del vaciado no se oye: solo lo nuevo)
                    tmpN += k;
                }
                else leidoDespues = a.Leido;
                int hechos = 0;
                double x = fase;
                while (hechos < pedidos)
                {
                    int i = (int)x;
                    if (i + 1 >= tmpN) break;
                    float t = (float)(x - i);
                    float l0 = tmp[i * 2], r0 = tmp[i * 2 + 1];
                    d[hechos * 2] = l0 + (tmp[(i + 1) * 2] - l0) * t;
                    d[hechos * 2 + 1] = r0 + (tmp[(i + 1) * 2 + 1] - r0) * t;
                    hechos++; x += paso;
                }
                // se quitan los cuadros que ya no hacen falta (los de antes de floor(x)); lo demas se queda para la siguiente lectura
                int quita = Math.Min((int)x, tmpN);
                if (quita > 0) { Array.Copy(tmp, quita * 2, tmp, 0, (tmpN - quita) * 2); tmpN -= quita; }
                fase = x - quita;
                return hechos;
            }
            private void Aplica(Span<float> d)
            {
                float v = volumen;
                if (v != 1f) for (int i = 0; i < d.Length; i++) d[i] *= v;
            }
            // SE ACABO: lo ultimo que llego y la peticion «acabada» (el hilo de Spotify la remata). Un fallo con poco sonido devuelve el credito.
            private int Acaba(Span<float> d, int n, int motivo)
            {
                acabada = true;
                double seg = Interlocked.Read(ref cuadrosSonido) / (double)hz;
                int durMs = Volatile.Read(ref p.DuracionMs);
                bool natural = motivo == RocolaOsSpotifyPeticion.Termino
                    || ((motivo == RocolaOsSpotifyPeticion.OtraCancion || motivo == RocolaOsSpotifyPeticion.Parada) && durMs > 0 && seg * 1000 >= durMs - 5000);
                if (motivo == RocolaOsSpotifyPeticion.SinMotivo) motivo = RocolaOsSpotifyPeticion.Termino;
                bool devolver = motivo == RocolaOsSpotifyPeticion.NoEmpezo || (!natural && motivo != RocolaOsSpotifyPeticion.Cierre && seg < PocoSonidoSeg);
                p.FinMotivo = natural ? RocolaOsSpotifyPeticion.Termino : motivo;
                p.Devolver = devolver;
                p.SegundosSonados = seg;
                Suelta();
                Volatile.Write(ref p.Fase, RocolaOsSpotifyPeticion.Acabada);
                Aplica(d.Slice(0, n * 2));
                Interlocked.Add(ref cuadrosEntregados, n);
                return n * 2;
            }
            // (atomico: si Acaba y Dispose se cruzaran, LectoresAbiertos no puede quedar en -1; con -1 el lector_presente ya no subiria nunca y
            // ninguna de Spotify volveria a sonar)
            private void Suelta() { if (Interlocked.Exchange(ref contado, 0) == 1) Interlocked.Decrement(ref RocolaOsSpotify.LectoresAbiertos); }
            // XP la cierra (acabo, SALTAR, otra cancion, la rocola se cierra): si aun sonaba, la rocola la corto (sin devolver: como una de XP saltada)
            public void Dispose()
            {
                RocolaOsSpotifyPeticion q = p;
                if (q != null && !acabada)
                {
                    acabada = true;
                    q.FinMotivo = RocolaOsSpotify.CerrandoLaRocola ? RocolaOsSpotifyPeticion.Cierre : RocolaOsSpotifyPeticion.Cortada;
                    q.SegundosSonados = Interlocked.Read(ref cuadrosSonido) / (double)hz;
                    q.Devolver = false;
                    Suelta();
                    Volatile.Write(ref q.Fase, RocolaOsSpotifyPeticion.Acabada);
                }
            }
        }
    }
}
