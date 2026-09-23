// LA SALIDA AL DISPOSITIVO: el formato que se le entrega a Windows, y que hacer cuando falla el APARATO
//
// Parte de Form1 (partial): igual en Modest y en HD. Lo propio de HD (WASAPI) sigue en Form1.Sonido.cs.
//
// 1) EL FORMATO. La salida clasica (WaveOut) recibia siempre 16 bits con dither TPDF. En Windows Vista y
//    posteriores eso no llega asi al aparato: el motor de audio de Windows mezcla en coma flotante y despues
//    convierte al formato del dispositivo. Entregarle 16 bits obligaba a redondear y a meter el ruido del
//    dither para nada. Ahora, en Vista o posterior, se le entrega coma flotante de 32 bits tal cual sale de la
//    cadena: ni redondeo ni dither en la rocola. Si el sistema la rechaza, se vuelve a los 16 bits con dither
//    de siempre, que es tambien lo que sigue usando XP (alli no se puede probar que su mezclador la admita).
//    El dither va, como siempre, una sola vez y al final.
//
// 2) CUANDO FALLA EL APARATO, NO LA CANCION. Antes, si la salida no abria (el DAC o la tarjeta desenchufados,
//    el dispositivo tomado por otro programa), la cancion se daba por rota: credito devuelto, apuntada como
//    incompatible y a la siguiente... que fallaba igual. En segundos se vaciaba la cola entera, y al volver el
//    aparato la rocola seguia callada. Ahora un fallo DEL DISPOSITIVO no toca la cancion, ni la cola, ni los
//    creditos: se espera y se vuelve a intentar la MISMA cancion, desde donde iba, hasta que el aparato vuelva.
//    Un fallo de formato o del archivo sigue como siempre (credito devuelto y a la siguiente). Si la salida se
//    cae una y otra vez A MEDIA cancion, a la sexta se da por fallida como antes, para no quedarse en un bucle.
//
// 3) SI LA SALIDA SE QUEDA MUDA sin avisar (un controlador que deja de pedir sonido), la barra de progreso lo
//    nota: 10 s sin avanzar con la salida en marcha cuentan como un fallo del dispositivo.
//
// 4) CAMBIO DE DISPOSITIVO. Con la continuidad sin pausas la salida puede quedar abierta cancion tras cancion.
//    Si Windows cambia de aparato predeterminado (se enchufa otro), la siguiente no se encadena: la salida se
//    cierra al acabar la que suena y la siguiente se abre en el aparato nuevo, como antes de la continuidad.
//
// 5) LA CANCION DE LA MEDIDA, CALLADA DESDE DENTRO. La medida de rendimiento pone una cancion muda y antes se
//    callaba bajandole el volumen a la SALIDA. Ese volumen no es de la rocola: en WASAPI es el volumen general
//    de Windows y en WaveOut (Vista o posterior) el de la rocola en el mezclador de Windows, y no se devolvia.
//    Ahora se calla al final de la cadena y no se toca ningun volumen de Windows.

using System;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
        // ------------------------------------------------------------------ 1) EL FORMATO
        internal Func<IWavePlayer> fabricaSalidaClasica = null;   // solo la ponen las pruebas (null = WaveOut de verdad)
        private string formatoSalida = "";                 // "float32" o "PCM16+TPDF": lo que recibe la salida clasica
        private bool flotanteRechazado;                    // en esta sesion el sistema ya rechazo la coma flotante

        // Windows Vista o posterior: alli la salida clasica pasa por el motor de audio, que mezcla en coma flotante.
        private static bool FlotanteAdmitido()
        {
            OperatingSystem so = Environment.OSVersion;
            return so.Platform == PlatformID.Win32NT && so.Version.Major >= 6;
        }

        private IWavePlayer CreaWaveOut()
        {
            if (fabricaSalidaClasica != null) return fabricaSalidaClasica();
            // WaveOut con callback por VENTANA, no WaveOutEvent: el de eventos llama a WaitHandle.WaitOne(Int32),
            // que NO existe en .NET 3.5 sin Service Pack 1 (el de la rockola de XP) y truena. Este no lo usa.
            WaveOut wo = new WaveOut();
            // El audio se rellena desde el bucle de mensajes de la interfaz; con 1 SEGUNDO de colchon la musica
            // aguanta cualquier atoron (paginas, caratulas, GC, video) sin cortarse.
            wo.DesiredLatency = 1000;
            // BLOQUES CORTOS: cada vez que se vacia un bloque la interfaz se para a descifrar el MP3 y a pasarlo
            // por la cadena. Con 20 bloques de 50 ms se hace el mismo trabajo en pausas cuatro veces mas cortas
            // que con 5 de 200 ms, y caben de sobra dentro de un cuadro. El colchon sigue siendo de 1 segundo.
            wo.NumberOfBuffers = 20;
            return wo;
        }

        // Abre la salida clasica con las muestras de la cadena y la deja en 'output' (con su aviso de parada).
        // 'flotante': probar primero coma flotante. Si el sistema la rechaza, 16 bits con dither TPDF. Un fallo
        // del APARATO no se confunde con un rechazo de formato: se relanza tal cual.
        private void AbreSalidaClasica(ISampleProvider muestras, bool flotante)
        {
            Exception rechazo = null;
            if (flotante && !flotanteRechazado)
            {
                IWavePlayer wf = CreaWaveOut();
                output = wf; wf.PlaybackStopped += Output_PlaybackStopped;
                try { wf.Init(new SampleToWaveProvider(muestras)); formatoSalida = "float32"; return; }
                catch (Exception ex)
                {
                    wf.PlaybackStopped -= Output_PlaybackStopped; output = null;
                    try { wf.Dispose(); } catch { }
                    if (EsFalloDelDispositivo(ex)) throw;
                    rechazo = ex;
                }
            }
            IWavePlayer w16 = CreaWaveOut();
            output = w16; w16.PlaybackStopped += Output_PlaybackStopped;
            try { w16.Init(new DitherWaveProvider16(muestras)); formatoSalida = "PCM16+TPDF"; }
            catch
            {
                w16.PlaybackStopped -= Output_PlaybackStopped; output = null;
                try { w16.Dispose(); } catch { }
                throw;
            }
            if (rechazo != null)
            {
                // Los 16 bits si abrieron: era el formato. Se recuerda para no volver a probar en esta sesion.
                flotanteRechazado = true;
                LogError("La salida de sonido no admite coma flotante: sigue en 16 bits con dither", rechazo);
            }
        }

        // ------------------------------------------------------------------ 2) CUANDO FALLA EL APARATO
        // La salida se quedo muda: lo apunta el vigia (3).
        private sealed class SalidaMudaException : Exception
        {
            public SalidaMudaException() : base("la salida de sonido dejo de pedir musica") { }
        }

        // ¿El fallo es del dispositivo (no de la cancion ni de su formato)?
        private static bool EsFalloDelDispositivo(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is SalidaMudaException) return true;
                NAudio.MmException mm = e as NAudio.MmException;
                if (mm != null)
                {
                    switch (mm.Result)
                    {
                        case NAudio.MmResult.BadDeviceId:        // no hay dispositivo con ese numero (desenchufado)
                        case NAudio.MmResult.NotEnabled:         // controlador deshabilitado
                        case NAudio.MmResult.AlreadyAllocated:   // lo tiene otro programa en exclusiva
                        case NAudio.MmResult.InvalidHandle:      // el que estaba abierto ya no existe
                        case NAudio.MmResult.NoDriver:           // no hay controlador
                        case NAudio.MmResult.NoDriverCallback:
                            return true;
                    }
                    return false;                                // formato rechazado u otro: es de la cancion
                }
                System.Runtime.InteropServices.COMException com = e as System.Runtime.InteropServices.COMException;
                if (com != null)
                {
                    switch (unchecked((uint)com.ErrorCode))
                    {
                        case 0x88890004u:   // AUDCLNT_E_DEVICE_INVALIDATED: se quito el aparato
                        case 0x8889000Au:   // AUDCLNT_E_DEVICE_IN_USE: tomado en exclusiva por otro programa
                        case 0x8889000Fu:   // AUDCLNT_E_ENDPOINT_CREATE_FAILED
                        case 0x88890010u:   // AUDCLNT_E_SERVICE_NOT_RUNNING: el servicio de audio de Windows
                        case 0x88890026u:   // AUDCLNT_E_RESOURCES_INVALIDATED
                        case 0x80070490u:   // E_NOTFOUND: no hay aparato de salida predeterminado
                            return true;
                    }
                    return false;
                }
            }
            return false;
        }

        private const int MaxCaidasAMedias = 5;   // caidas seguidas a media cancion del mismo elemento de la cola
        private Timer reintentoSalida;
        private Track reintentoCancion;           // la cancion que espera a que vuelva el aparato
        private double reintentoDesde;            // desde donde se retoma (s)
        private int reintentos;                   // intentos seguidos sin salida
        private double reanudarEn;                // lo toma StartOpenedTrack: salta ahi antes de armar la cadena (s)
        private double inicioSeg;                 // desde donde arranco la cancion que suena (s)
        private PlayItem caidaItem; private int caidasAMedias;

        // Cuanto esperar antes del intento n: el primero casi enseguida (un tropiezo de un instante), luego 2 s y,
        // desde el tercero, cada 10 s mientras no vuelva el aparato.
        private static int EsperaReintento(int n) { return n <= 0 ? 500 : (n < 3 ? 2000 : 10000); }

        // Desde donde retomar: lo leido menos lo que aun estaba en el colchon de la salida sin oirse.
        private static double PosicionOida(double leidoSeg, double totalSeg, int colchonMs)
        {
            double p = leidoSeg - colchonMs / 1000.0;
            if (p < 0 || double.IsNaN(p)) p = 0;
            if (totalSeg > 0 && p > totalSeg) p = totalSeg;
            return p;
        }
        private double PosicionActual()
        {
            IAudioSource r = reader;
            if (r == null) return 0;
            try { return PosicionOida(r.CurrentTime.TotalSeconds, r.TotalTime.TotalSeconds, RetrasoAudible()); }
            catch { return 0; }
        }
        private double TomaReanudar() { double d = reanudarEn; reanudarEn = 0; inicioSeg = d > 0 ? d : 0; return inicioSeg; }

        // ¿Se sigue esperando al aparato? false = ya se cayo demasiadas veces a media cancion: que se trate como
        // un fallo de la cancion, como antes. Solo cuenta las caidas A MEDIAS del mismo elemento de la cola.
        private bool SigueEsperando(PlayItem item, bool aMedias)
        {
            if (!aMedias) return true;
            if (!object.ReferenceEquals(item, caidaItem)) { caidaItem = item; caidasAMedias = 0; }
            return ++caidasAMedias <= MaxCaidasAMedias;
        }

        // El APARATO fallo (al abrir la salida o a media cancion). true = se espera y se retoma la misma cancion desde
        // 'desde'; false = el que llama la trata como un fallo de la cancion (credito devuelto, siguiente).
        private bool SalidaCaida(Track t, double desde, Exception ex, bool aMedias)
        {
            if (t == null || audioOpeningForMeasurement) return false;
            if (!SigueEsperando(nowPlaying, aMedias)) return false;
            if (reintentos == 0)
                LogError("Salida de sonido" + (formatoSalida.Length > 0 ? " (" + formatoSalida + ")" : "") + ": fallo el dispositivo; se espera y se retoma la misma cancion", ex);
            DisposePlaybackNow();
            reintentoCancion = t; reintentoDesde = desde;
            int espera = EsperaReintento(reintentos);
            reintentos++;
            if (reintentoSalida == null) { reintentoSalida = new Timer(); reintentoSalida.Tick += ReintentoSalida_Tick; }
            reintentoSalida.Interval = espera;
            reintentoSalida.Start();
            lblStatus.Text = "Sin salida de sonido: la musica sigue en cuanto vuelva el aparato";
            if (reintentos == 3) ShowToast("Sin salida de sonido: la cola se conserva", System.Drawing.Color.FromArgb(200, 130, 35));
            return true;
        }

        private void ReintentoSalida_Tick(object sender, EventArgs e)
        {
            reintentoSalida.Stop();
            Track t = reintentoCancion; reintentoCancion = null;
            // Solo si sigue tocandole a ESA cancion (saltar o un video lo cancelan).
            if (t == null || videoOn || nowPlaying == null || nowPlaying.IsVideo || !object.ReferenceEquals(nowPlaying.Song, t)) return;
            reanudarEn = reintentoDesde;
            PlayTrack(t);
        }

        private void CancelaReintentoSalida()
        {
            reintentoCancion = null;
            if (reintentoSalida != null) reintentoSalida.Stop();
            vigiaQuietos = 0; vigiaLeido = -1;
        }

        // La salida abrio y arranco: el aparato esta. Se apunta en cual (4) y se olvida la cuenta de intentos.
        private void SalidaArranco(string idAparato)
        {
            reintentos = 0;
            idSalidaAbierta = idAparato;
        }

        // ------------------------------------------------------------------ 3) EL VIGIA
        private const int TicsSalidaMuda = 20;    // 10 s, con la barra de progreso (cada 500 ms)
        private double vigiaLeido = -1;
        private int vigiaQuietos;

        // true si la salida esta en marcha y lleva 10 s sin pedir musica. Lo mira la barra de progreso.
        private bool SalidaMuda()
        {
            IAudioSource r = reader; IWavePlayer o = output;
            if (r == null || o == null || audioOpeningForMeasurement || videoOn) { vigiaQuietos = 0; vigiaLeido = -1; return false; }
            double p;
            try { p = r.CurrentTime.TotalSeconds; } catch { return false; }
            if (p != vigiaLeido) { vigiaLeido = p; vigiaQuietos = 0; return false; }
            PlaybackState st;
            try { st = o.PlaybackState; } catch { return false; }
            if (st != PlaybackState.Playing) { vigiaQuietos = 0; return false; }
            if (++vigiaQuietos < TicsSalidaMuda) return false;
            vigiaQuietos = 0;
            return true;
        }

        // ------------------------------------------------------------------ 4) CAMBIO DE DISPOSITIVO
        internal Func<string> idSalidaActual = null;     // solo la ponen las pruebas (null = preguntar a Windows)
        private string idSalidaAbierta;           // el aparato en el que se abrio la salida (null = no se sabe)

        // El aparato de salida predeterminado ahora mismo (Vista o posterior; en XP no se pregunta: null).
        private string IdSalidaPorDefecto()
        {
            if (idSalidaActual != null) return idSalidaActual();
            if (!FlotanteAdmitido()) return null;
            try
            {
                using (NAudio.CoreAudioApi.MMDeviceEnumerator en = new NAudio.CoreAudioApi.MMDeviceEnumerator())
                using (NAudio.CoreAudioApi.MMDevice d = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console))
                    return d.ID;
            }
            catch { return null; }
        }

        // ¿Sigue siendo el mismo aparato en el que se abrio la salida? Si no se sabe, se da por bueno.
        private bool MismaSalida()
        {
            string abierta = idSalidaAbierta;
            if (abierta == null) return true;
            string ahora = IdSalidaPorDefecto();
            return ahora == null || string.Equals(ahora, abierta, StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------ 5) LA CANCION DE LA MEDIDA, CALLADA
        // La medida de rendimiento pone una cancion para cargar la maquina de verdad, y MUDA. Se calla aqui, al final
        // de la cadena: se descomprime y se procesa todo igual, y a la salida solo le llegan ceros. NUNCA con el
        // volumen de la salida: en WASAPI es el volumen general de Windows (callaria el equipo entero) y en WaveOut,
        // desde Vista, el de la rocola en el mezclador de Windows, que se queda guardado. Ninguno se devolvia al
        // acabar la medida, asi que la musica de despues podia salir callada. En exclusivo, ademas, no callaria.
        private ISampleProvider CallaSiEsMedida(ISampleProvider s) { return audioOpeningForMeasurement ? new Callada(s) : s; }

        private sealed class Callada : ISampleProvider
        {
            private readonly ISampleProvider src;
            public Callada(ISampleProvider s) { src = s; }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                // Muestra a muestra: 'buffer' puede ser la vista flotante que NAudio pone sobre su byte[], donde
                // Array.Clear borraria en otro sitio (ver Realce.cs).
                for (int i = 0; i < n; i++) buffer[offset + i] = 0f;
                return n;
            }
        }
    }
}
