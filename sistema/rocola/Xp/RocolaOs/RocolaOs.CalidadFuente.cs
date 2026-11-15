// LA SUSTITUCION INVISIBLE (fase 11, funcion «calidad_spotify»; FASE11_sustitucion.md): LA FUENTE. Una cancion TUYA que suena desde Spotify:
// envuelve la fuente de Spotify del nucleo (RocolaOs.SpotifyFuente.cs: la peticion a Soloist, el anillo, lo que acaba) y le pone lo propio de
// la sustitucion. Va dentro de Form1 porque IAudioSource es privada de Form1. La crea RocolaOsCalidadAbre (desde g13) en el hilo que abre; la
// lee el hilo del sonido: no espera nunca, no toma cerrojos y no reserva memoria al leer (todo se reserva al crearla).
//   - EL CREDITO: es el de tu cancion (ya cobrado por XP): la de Spotify NUNCA lo devuelve (SinDevolver).
//   - LOS 6 S: si Spotify no empieza a sonar en 6 s desde la primera lectura, o si acaba por un fallo (se corto, Soloist se cayo, otra cosa
//     a media cancion), entrega silencio y encarga a la pantalla volver a tu MP3 (RocolaOsCalidadVuelve, el camino de XP que retoma una
//     cancion). Si ya no le toca (se salto), solo acaba.
//   - LO QUE DURA (lo que ve XP): 0 hasta la primera lectura (asi ArmaCadena no le pone fundido de salida: la de Spotify trae su final); mientras
//     espera, lo que dura tu MP3 (la barra no salta y el relevo sin pausa no se prepara antes de tiempo); sonando, lo que dice la de Spotify.
//   - EL ATASCO (revision «sonido-cuota-y-fallos»): ya sonando, 5 s seguidos entregados sin que llegue nada de Soloist (la red que se cae a
//     media cancion) es un fallo y vuelve tu MP3 desde donde iba. El nucleo solo lo ve con Soloist en «playing» (5 s) o sin WebSocket (10
//     s); con Soloist en «buffering» (lo que hace al quedarse sin red) esperaba 30 s de silencio (SeguridadMs). Se cuenta en lo entregado, no
//     en el reloj: si la salida no lee, no cuenta. A 5 s o menos de lo que dura en Spotify, se da por acabada (sigue la siguiente).
//   - V1: lo que llega de Spotify (antes de la cadena) pasa por el medidor del repaso (MedidorBs1770) si la pareja aun no tiene medida.
//   - C: su volumen por tramos de 0,5 s, para compararlo con la forma de tu MP3 (que mide otro hilo) la primera vez.
using System;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        private sealed class RocolaOsFuenteCalidad : IAudioSource
        {
            private const int Esperando = 0, Sonando = 1, Volviendo = 2, Acabo = 3;
            internal readonly string Ruta, Uri;
            internal readonly RocolaOsCalidad.Pareja Par;
            internal Track Sombra;                      // la copia de tu cancion con la ruta de Spotify (la pone g61)
            private readonly Form1 rocola;
            private readonly RocolaOsFuenteSpotify sp;
            private readonly int hz;
            private readonly MedidorBs1770 medidor;     // V1 (null si la pareja ya esta medida)
            private readonly double[] e; private readonly long[] n; private readonly long porTramo;   // C
            private readonly RocolaOsCalidad.Forma forma;   // la de tu MP3 (null si la forma ya se decidio)
            internal const int AtascoMs = 5000;
            private int estado;
            private long t0 = -1, sonidoAntes, sinLlegar;   // sinLlegar: cuadros entregados en silencio desde lo ultimo que llego (ya sonando)
            private volatile bool abandona;
            private int resultado;                      // (1: ya se mando el resultado)

            internal RocolaOsFuenteCalidad(Form1 r, string ruta, RocolaOsCalidad.Pareja p)
            {
                rocola = r; Ruta = ruta; Par = p; Uri = p.Uri;
                sp = new RocolaOsFuenteSpotify(Uri, System.IO.Path.GetFileNameWithoutExtension(ruta), p.DurSpMs);
                sp.SinDevolver = true;
                hz = sp.WaveFormat.SampleRate;
                if (!p.ConMedida) { try { medidor = new MedidorBs1770(hz, new double[] { 1.0, 1.0 }); } catch { medidor = null; } }
                if (p.Verif == 0)
                {
                    int ms = Math.Max(p.DurSpMs, p.DurMp3Ms);
                    int T = (int)Math.Min(4000, Math.Max(80, ms / 1000.0 / RocolaOsCalidad.TramoSeg + 2 * RocolaOsCalidad.LagMax + 20));
                    e = new double[T]; n = new long[T];
                    porTramo = Math.Max(1, (long)(hz * RocolaOsCalidad.TramoSeg));
                    forma = new RocolaOsCalidad.Forma();
                    RocolaOsCalidad.Forma fm = forma; string rr = ruta;
                    RocolaOs.Hilo("calidad-forma", delegate { RocolaOsCalidadMideForma(rr, fm); });
                }
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return sp.WaveFormat; } }
            public float Volume { set { sp.Volume = value; } }
            public TimeSpan CurrentTime { get { return sp.CurrentTime; } }
            public TimeSpan TotalTime
            {
                get
                {
                    if (Volatile.Read(ref t0) < 0) return TimeSpan.Zero;
                    if (sp.CuadrosSonido > 0) return sp.TotalTime;
                    int ms = Par.DurMp3Ms > 0 ? Par.DurMp3Ms : Par.DurSpMs;
                    return TimeSpan.FromMilliseconds(Math.Max(0, ms));
                }
            }
            public void Salta(TimeSpan donde) { }   // (no la abre una retomada: RocolaOsCalidadAbre la manda a tu MP3)

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs), como las de XP
            public int Read(float[] buf, int off, int count)
            {
                if (estado == Acabo) return 0;
                if (estado == Volviendo)
                {
                    if (abandona) { estado = Acabo; return 0; }
                    Array.Clear(buf, off, count);
                    return count;
                }
                long ahora = Environment.TickCount64;
                if (t0 < 0) Volatile.Write(ref t0, ahora);
                int r = sp.Read(buf, off, count);
                long s = sp.CuadrosSonido;
                int nuevos = (int)Math.Max(0, Math.Min(r / 2, s - sonidoAntes));
                if (nuevos > 0) { Mide(buf, off, nuevos); sonidoAntes += nuevos; sinLlegar = 0; if (estado == Esperando) estado = Sonando; }
                else if (estado == Sonando) sinLlegar += Math.Max(0, r / 2);
                if (sp.Terminada)
                {
                    if (sp.FinMotivo == RocolaOsSpotifyPeticion.Termino) { estado = Acabo; Resultado(true); return r; }
                    Vuelve(s / (double)hz, RocolaOsSpotifyPeticion.Motivos[Math.Max(0, Math.Min(RocolaOsSpotifyPeticion.Motivos.Length - 1, sp.FinMotivo))]);
                    if (r < count) Array.Clear(buf, off + r, count - r);
                    return count;
                }
                if (estado == Sonando && sinLlegar > (long)hz * AtascoMs / 1000)
                {
                    sp.Dispose();   // (Cortada, sin devolver: el nucleo la remata con su pausa)
                    if (CercaDelFinal())
                    {   // a 5 s o menos de su final: se da por acabada (XP sigue con la siguiente), sin volver al MP3 por unos segundos
                        estado = Acabo; Resultado(false);
                        RocolaOsCalidad.Apunta("la de Spotify de " + System.IO.Path.GetFileName(Ruta) + " se atasco a menos de 5 s del final: se da por acabada");
                        return 0;
                    }
                    Vuelve(sonidoAntes / (double)hz, "Spotify se atasco " + (AtascoMs / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture) + " s a media cancion");
                    Array.Clear(buf, off, count);
                    return count;
                }
                if (s == 0 && ahora - t0 > RocolaOsCalidad.EsperaMs)
                {
                    sp.Dispose();   // (Cortada, sin devolver: el nucleo la remata con su pausa)
                    Vuelve(0, "Spotify no empezo a sonar en 6 s");
                    Array.Clear(buf, off, count);
                    return count;
                }
                return r;
            }
            private bool CercaDelFinal()
            {
                int ms = Par.DurSpMs > 0 ? Par.DurSpMs : Par.DurMp3Ms;
                return ms > 0 && sonidoAntes * 1000.0 / hz >= ms - 5000;
            }
            // lo que llego de Spotify (al principio del bloque: la fuente del nucleo pone su sonido primero y el silencio despues)
            private void Mide(float[] buf, int off, int cuadros)
            {
                if (medidor != null) { try { medidor.Procesa(buf, off, cuadros * 2); } catch { } }
                if (e == null) return;
                for (int i = 0; i < cuadros; i++)
                {
                    long k = (sonidoAntes + i) / porTramo;
                    if (k >= e.Length) break;
                    float x = 0.5f * (buf[off + i * 2] + buf[off + i * 2 + 1]);
                    e[k] += x * x; n[k]++;
                }
            }
            private void Vuelve(double sonoSeg, string motivo)
            {
                estado = Volviendo;
                Resultado(false);
                RocolaOsFuenteCalidad yo = this; double s = sonoSeg; string m = motivo;
                if (!RocolaOs.Encarga("calidad-vuelve", delegate { rocola.RocolaOsCalidadVuelve(yo, s, m); })) abandona = true;
            }
            internal void Abandona() { abandona = true; }
            private void Resultado(bool natural)
            {
                if (Interlocked.Exchange(ref resultado, 1) != 0) return;
                if (sonidoAntes <= 0) return;
                RocolaOsCalidad.Encola(new RocolaOsCalidad.Resultado
                {
                    Ruta = Ruta, Id = Par.Id, Natural = natural, SonidoSeg = sonidoAntes / (double)hz, Hz = hz, Medidor = medidor, E = e, N = n, Forma = forma,
                    Hasta = Environment.TickCount64 + 120000,
                });
            }
            // XP la cierra (acabo, SALTAR, la rocola se cierra, o tu MP3 entra en su lugar)
            public void Dispose()
            {
                if (estado == Esperando || estado == Sonando) Resultado(false);
                estado = Acabo;
                sp.Dispose();
            }
        }
    }
}
