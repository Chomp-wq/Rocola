// ROCOLA OS: LA SALIDA NUEVA DE SONIDO (cadena de audio nueva, aprobada por el dueno el 27-11-2026: CADENA_NUEVA.md §4 y las decisiones D10
// -la salida nueva tambien para la cadena de hoy-, D11 -los videos por la rocola-, D12 -medir el convertidor- y D13 -botones de 1 dB-; lo
// hecho, como y lo probado: CADENA_NUEVA_salida.md). La funcion «salida_nueva» de rocola_os.txt, APAGADA de fabrica: el dueno la enciende
// desde Ctrl+F4 -> Sonido con la prueba de «¿Se oye bien?» de 15 s, que vuelve sola a lo de antes si nadie contesta (RocolaOs.Salida.cs).
//
// CON ELLA (todo lo demas, XP tal cual: sin ROCOLA_OS o con salida_nueva=0 ningun gancho cambia nada, bit a bit):
//   - LA TARJETA SIEMPRE ABIERTA por hw: (sin plughw), elegida por su nombre (la USB primero en automatico), a 24 bits y 44 100 Hz fijos, con
//     ceros cuando no hay nada y 0,5 s de ceros al abrir (el motor: RocolaOs.SalidaNuevaMotor.cs; la tarjeta: RocolaOs.SalidaNuevaTarjetas.cs).
//     Lo que XP abre y cierra (la salida de cada cancion, la de Spotify, la retomada, la del diagnostico) pasa a ser una FUENTE del motor
//     (RocolaOs.SalidaNuevaCliente.cs): Nativo/SalidaAlsa.cs, al abrir, le pregunta a SalidaAlsa.RocolaOsAbre (que pone esta funcion).
//   - La cadena entrega coma flotante (gancho g440: AbreSalidaClasica con flotante; sin el dither de 16 bits de XP) a 44,1 kHz (ganchos
//     g441-g443: como frecuencia_salida=44100, con el convertidor sinc de XP y su relevo sin pausa); lo que llegue a otra frecuencia (la
//     primera vez, mientras se calienta la tabla del convertidor; el sonido de un video) lo convierte la fuente con el mismo sinc.
//   - EL VOLUMEN DEL DUENO DIGITAL en dB (volumen_db=, de -60 a 0, y volumen_mudo=; se guardan y valen al volver a arrancar): los botones de
//     atras (Plataforma/VolumenTeclas.cs: VolumenTeclas.Desvio) mueven 1 dB por toque, y apretados unas 10 veces por segundo, como antes;
//     el control de la tarjeta se pone una vez en 0 dB y no se toca mas.
//   - LA PROTECCION en 0 dBFS y la UNICA cuantizacion (el motor).
//   - LOS VIDEOS POR LA ROCOLA (RocolaOs.SalidaNuevaVideo.cs): mpv deja su sonido en una tuberia que lee la rocola y sale por el mismo flujo,
//     con su ganancia fija medida (sonoridad integrada con ffmpeg, una vez, de fondo) y el modo noche como la musica.
//   - EN LA HOJA SONIDO DE CTRL+F4: la tarjeta, el formato y la frecuencia abiertos, el control por hardware, el volumen del dueno, la
//     proteccion y los videos (RocolaOs.SalidaNuevaHoja.cs), y la herramienta para MEDIR EL CONVERTIDOR (RocolaOs.SalidaNuevaMide.cs; tambien
//     «rocola-xp --mide-convertidor» desde la consola de mantenimiento).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NAudio.Wave;

namespace Rocola
{
    internal static partial class SalidaNueva
    {
        internal const string ClaveVolumen = "volumen_db", ClaveMudo = "volumen_mudo", ClaveBufer = "salida_nueva_bufer_ms", ClaveVideos = "salida_nueva_videos",
            ClaveVideoLufs = "salida_nueva_video_lufs";
        // La salida nueva va en esta sesion: la funcion encendida y el motor no se rindio
        internal static bool Va { get { return Arrancada && !Rendida; } }

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = NombreFuncion,
                Que = "la salida nueva de sonido: la tarjeta por hw: y por su nombre, siempre abierta, a 24 bits y 44,1 kHz, con el volumen del dueno digital y los videos por la rocola",
                DeFabrica = false,
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.Entero(ClaveVolumen, 0, VolumenMinDb, VolumenMaxDb, true, "el volumen del dueno con la salida nueva, en dB (los botones de atras: 1 dB por toque)"),
                    RocolaOsClave.SiNo(ClaveMudo, false, true, "el silencio del dueno con la salida nueva (la tecla de silencio)"),
                    RocolaOsClave.Entero(ClaveBufer, 200, 60, 500, false, "el bufer de la tarjeta con la salida nueva, en ms (lo que tarda en oirse un cambio)"),
                    RocolaOsClave.SiNo(ClaveVideos, true, false, "el sonido de los videos por la rocola (1) o mpv directo a la tarjeta, que la salida nueva le suelta mientras dura el video (0)"),
                    RocolaOsClave.Entero(ClaveVideoLufs, -18, -30, -6, false, "la sonoridad a la que suenan los videos medidos con la salida nueva (LUFS); sin cambiarla, la de la cadena que suena"),
                },
                Arranca = f => ArrancaFuncion(f),
                Latido = f => LatidoFuncion(f),
                Vivo = (f, sb) => Renglones(sb),
                AlCambiar = (f, clave) => { if (clave == ClaveVolumen || clave == ClaveMudo) PonVolumen(RocolaOs.AjusteEntero(ClaveVolumen), RocolaOs.AjusteSiNo(ClaveMudo)); },
            });
            RocolaOsOrdenes.Si("--mide-convertidor", SalidaNuevaMide.Orden);
        }

        // AL ARRANCAR (hilo de la pantalla, antes de ensenarse la ventana): el motor (esperando como mucho 2 s a que abra la tarjeta) y los
        // enchufes de Nativo/ y Plataforma/
        private static void ArrancaFuncion(Form1 f)
        {
            BuferMs = Math.Max(60, Math.Min(500, RocolaOs.AjusteEntero(ClaveBufer)));
            PeriodoMs = BuferMs >= 120 ? 20 : 10;
            PonVolumen(RocolaOs.AjusteEntero(ClaveVolumen), RocolaOs.AjusteSiNo(ClaveMudo));
            string fijo = null; try { fijo = Environment.GetEnvironmentVariable("ROCOLA_SALIDA_NUEVA_PCM"); } catch { }
            if (!string.IsNullOrEmpty(fijo))
            {
                PcmFijo = fijo;
                int t; string ft = null; try { ft = Environment.GetEnvironmentVariable("ROCOLA_SALIDA_NUEVA_TARJETA"); } catch { }
                if (int.TryParse(ft, NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) TarjetaFija = t;
            }
            string gp = null; try { gp = Environment.GetEnvironmentVariable("ROCOLA_SALIDA_NUEVA_GANANCIA_DB"); } catch { }
            double gdb;
            if (double.TryParse(gp, NumberStyles.Float, CultureInfo.InvariantCulture, out gdb)) GananciaPrueba = (float)Math.Pow(10.0, gdb / 20.0);
            // las tablas del convertidor que mas se usan, de fondo (la primera cancion de 48 kHz ya sale convertida)
            CalientaTablas();
            ultimoRenglon = Environment.TickCount64;
            RocolaOs.Apunta(NombreFuncion, "arranca (bufer " + BuferMs + " ms, periodo " + PeriodoMs + " ms; volumen del dueno " + VolumenDb + " dB" + (Mudo ? ", en silencio" : "") + ")");
            bool abrio = Arranca(2000);
            if (Rendida) return;
            if (!abrio) RocolaOs.Apunta(NombreFuncion, "la tarjeta aun no abre (" + Motivo + "): las canciones esperan a que abra");
            SalidaAlsa.RocolaOsAbre = (fmt, bloque, bloques) => Abre("cancion", fmt, bloque, bloques);
            VolumenTeclas.Desvio = Teclas;
            f.RocolaOsSalidaNuevaArrancaVideo();
        }
        private static readonly int[] tasasQueSeCalientan = { 48000, 32000, 22050, 24000 };
        private static int calentadas;
        private static void CalientaTablas()
        {
            while (calentadas < tasasQueSeCalientan.Length)
            {
                double[] t = RemuestreoContinuo.CapturaTabla(WaveFormat.CreateIeeeFloatWaveFormat(tasasQueSeCalientan[calentadas], 2), WaveFormat.CreateIeeeFloatWaveFormat(Tasa, 2));
                if (t == null) return;   // (se esta preparando de fondo: la siguiente, en otro latido)
                calentadas++;
            }
        }
        private static long ultimoRenglon;
        private static void LatidoFuncion(Form1 f)
        {
            if (!Va) return;
            CalientaTablas();
            f.RocolaOsSalidaNuevaLatidoVideo();
            // la proteccion (si actuo desde la ultima vez) va a rocola_error.txt de XP tambien: ahi mira el dueno
            if (ProteccionVeces != proteccionVecesErr) { proteccionVecesErr = ProteccionVeces; RocolaOs.Apunta(NombreFuncion, "proteccion: " + ProteccionVeces + " veces, " + ProteccionMuestras + " muestras"); }
            long ahora = Environment.TickCount64;
            if (ahora - ultimoRenglon > 3600000) { ultimoRenglon = ahora; RocolaOs.Apunta(NombreFuncion, "cada hora: " + Resumen()); }
        }
        private static long proteccionVecesErr;

        // LOS BOTONES DE VOLUMEN (Plataforma/VolumenTeclas.cs los junta en su hilo): pasos de 1 dB; el silencio, con su tecla. Subir quita el
        // silencio (como «2%+ unmute» de antes); bajar no. Se guarda (rocola_os.txt) y vale al volver a arrancar.
        private static bool Teclas(int pasos, bool silencio)
        {
            if (!Va) return false;
            int db = VolumenDb; bool m = Mudo;
            if (pasos != 0) { db = Math.Max(VolumenMinDb, Math.Min(VolumenMaxDb, db + pasos)); if (pasos > 0) m = false; }
            if (silencio) m = !m;
            PonVolumen(db, m);
            RocolaOs.PonAjuste(ClaveVolumen, db.ToString(CultureInfo.InvariantCulture));
            RocolaOs.PonAjuste(ClaveMudo, m ? "1" : "0");
            return true;
        }
        internal static void CambiaVolumen(int pasos, bool silencio) { Teclas(pasos, silencio); }

        // ---------- lo que se ve ----------
        internal static string TextoFormato()
        {
            if (FormatoAbierto == 0) return "—";
            string hz = TasaAbierta % 1000 == 0 ? (TasaAbierta / 1000).ToString(CultureInfo.InvariantCulture) : (TasaAbierta / 1000.0).ToString("0.0##", CultureInfo.InvariantCulture).Replace('.', ',');
            return SalidaNuevaPcm.NombreFormato(FormatoAbierto) + " (" + SalidaNuevaPcm.BitsUtiles(FormatoAbierto) + " bits) a " + hz + " kHz";
        }
        internal static string Resumen()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Rendida ? "rendida (" + Motivo + ")" : Abierta ? "abierta " + PcmNombre : Caida ? "sin tarjeta (" + Motivo + ")" : "abriendo");
            sb.Append("; ").Append(TextoFormato()).Append("; aperturas ").Append(Aperturas).Append(", caidas ").Append(Caidas).Append(", xruns ").Append(Xruns);
            sb.Append("; volumen ").Append(VolumenDb).Append(" dB").Append(Mudo ? " (silencio)" : "");
            sb.Append("; proteccion ").Append(ProteccionVeces).Append(" veces / ").Append(ProteccionMuestras).Append(" muestras");
            sb.Append("; fuentes ").Append(FuentesVivas);
            return sb.ToString();
        }
        // renglones de /run/rocola-vivo.txt
        private static void Renglones(StringBuilder sb)
        {
            sb.Append("salida_nueva=").Append(Rendida ? "rendida" : Abierta ? "abierta" : Caida ? "sin-tarjeta" : "abriendo")
              .Append(" pcm=").Append(PcmNombre.Length > 0 ? PcmNombre : "-")
              .Append(" formato=").Append(FormatoAbierto != 0 ? SalidaNuevaPcm.NombreFormato(FormatoAbierto) : "-")
              .Append(" hz=").Append(TasaAbierta.ToString(CultureInfo.InvariantCulture))
              .Append(" aperturas=").Append(Aperturas.ToString(CultureInfo.InvariantCulture))
              .Append(" caidas=").Append(Caidas.ToString(CultureInfo.InvariantCulture))
              .Append(" xruns=").Append(Xruns.ToString(CultureInfo.InvariantCulture))
              .Append(" volumen_db=").Append(VolumenDb.ToString(CultureInfo.InvariantCulture))
              .Append(" mudo=").Append(Mudo ? "1" : "0")
              .Append(" proteccion_veces=").Append(ProteccionVeces.ToString(CultureInfo.InvariantCulture))
              .Append(" proteccion_muestras=").Append(ProteccionMuestras.ToString(CultureInfo.InvariantCulture))
              .Append(" fuentes=").Append(FuentesVivas.ToString(CultureInfo.InvariantCulture))
              .Append(" videos=").Append(VideosPorLaRocola ? "por-la-rocola" : "mpv-directo")
              .Append('\n');
        }
        internal static volatile bool VideosPorLaRocola;
    }

    public partial class Form1
    {
        // ================================================================== LOS GANCHOS DE Codigo/
        // g440 (Form1.Sonido.cs, StartOpenedTrack): con la salida nueva la cadena entrega coma flotante (la unica cuantizacion es la del motor,
        // a 24 bits); sin ella, lo de XP (FlotanteAdmitido: en XP, no: 16 bits con dither TPDF)
        private static bool RocolaOsSalidaNuevaFlotante(bool deXp) { return SalidaNueva.Va || deXp; }
        // g441-g443 (Form1.Sonido.cs, AFrecuenciaDeLaTarjeta y CalientaFrecuenciaSalida; Form1.SinPausa.cs, el relevo): con la salida nueva la
        // cadena va a 44 100 Hz, como con frecuencia_salida=44100 (el sinc de XP y su relevo sin pausa); sin ella, lo guardado
        private static int RocolaOsSalidaNuevaHz(int deXp) { return SalidaNueva.Va ? SalidaNueva.Tasa : deXp; }
    }
}
