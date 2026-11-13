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
// 6) LA CAJA NEGRA DEL SONIDO (22 de octubre de 2026). Los cortes que el hilo de la musica no alcanzo a tapar (BombaSonido.Huecos)
//    y los avisos de la salida que llegaron tarde no se apuntaban en ningun sitio: en el bar, "a veces se corta" no dejaba
//    rastro. Ahora, al acabar o saltar una cancion con algo que contar, se apunta UNA linea «[Cortes de sonido] ...» en
//    rocola_error.txt (cuantos cortes, cuantos avisos tardios y el maximo, y la latencia abierta); y cada cambio del nivel de
//    latencia del perfil de la salida («[Latencia de audio] ...», PerfilLatenciaAudio) tambien. Sin nada que contar, no se
//    escribe nada. Se escribe por la bitacora de fondo (MedidasBitacora.cs): la musica y la pantalla no esperan al disco.
//
// 5) LA CANCION DE LA MEDIDA, CALLADA DESDE DENTRO. La medida de rendimiento pone una cancion muda y antes se
//    callaba bajandole el volumen a la SALIDA. Ese volumen no es de la rocola: en WASAPI es el volumen general
//    de Windows y en WaveOut (Vista o posterior) el de la rocola en el mezclador de Windows, y no se devolvia.
//    Ahora se calla al final de la cadena y no se toca ningun volumen de Windows.

using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    // Solo latencia, nunca cambia PCM, frecuencia, canales ni efectos. Perfil de
    // sesion; una bajada por apertura tras 30 s reales sin picos. No reabre salida.
    //
    // SIN SUBIR Y BAJAR CADA 30 s (24-10-2026). El rocola_error.txt de la noche del 23 al 24 traia unas cien lineas «[Latencia de
    // audio]»: bajaba de 1000 a 750 ms, a los pocos segundos «atasco de 15 a 20 ms» y vuelta a 1000. Esos atascos eran del hilo de
    // la musica, que lee trozos de 25 ms: pasarse del 60 % del trozo (15 ms) contaba como atasco, y en XP un solo tic del reloj del
    // sistema son 15,6 ms. Pero ese hilo tiene 200 ms de colchon (BombaSonido): 15 ms no le hacen nada. Ahora su atasco se mide contra
    // el colchon (pasar de la mitad, 100 ms). Y la salida clasica ya no baja de 750 ms: la rellena el hilo de la pantalla, y el mismo
    // archivo trae paradas de la pantalla de 500 a 766 ms (publicar la biblioteca); con 500 ms de colchon, una de esas se oiria.
    internal sealed class PerfilLatenciaAudio
    {
        private readonly object cerrojo = new object();
        private readonly int[] niveles;
        private int nivel;
        private long desde;
        private double musicaEstable;
        // Los cambios de nivel para la caja negra (se anotan en numeros, sin reservar nada: Observa corre en la musica).
        internal const int MaxCambios = 8;
        internal const int CausaBaja = 0, CausaAtasco = 1, CausaFallo = 2;
        private readonly int[] cambioDe = new int[MaxCambios], cambioA = new int[MaxCambios], cambioCausa = new int[MaxCambios], cambioValor = new int[MaxCambios];
        private int nCambios, perdidosCambios;
        private volatile int hayCambios;
        internal PerfilLatenciaAudio(bool clasica) { niveles = clasica ? new int[] { 1000, 750 } : new int[] { 300, 225, 150 }; }
        internal int Sugerida { get { lock (cerrojo) return niveles[nivel]; } }
        internal bool HayCambios { get { return hayCambios != 0; } }
        private void Anota(int de, int a, int causa, int valor)   // con el cerrojo
        {
            if (de == a) return;
            if (nCambios >= MaxCambios) { perdidosCambios++; return; }
            cambioDe[nCambios] = niveles[de]; cambioA[nCambios] = niveles[a]; cambioCausa[nCambios] = causa; cambioValor[nCambios] = valor; nCambios++;
            hayCambios = 1;
        }
        // Los cambios de nivel desde la ultima vez, en texto para rocola_error.txt (null si no hubo ninguno). Lo llama la pantalla.
        internal string TomaCambios()
        {
            lock (cerrojo)
            {
                if (nCambios == 0 && perdidosCambios == 0) { hayCambios = 0; return null; }
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < nCambios; i++)
                {
                    if (i > 0) sb.Append("; ");
                    bool sube = cambioA[i] > cambioDe[i];
                    sb.Append(sube ? "sube" : "baja").Append(" el margen de ").Append(cambioDe[i]).Append(" a ").Append(cambioA[i]).Append(" ms");
                    if (cambioCausa[i] == CausaBaja) sb.Append(" (30 s de musica sin atascos)");
                    else if (cambioCausa[i] == CausaAtasco) sb.Append(" (atasco de ").Append(cambioValor[i]).Append(" ms)");
                    else sb.Append(" (fallo o corte de la salida)");
                }
                if (perdidosCambios > 0) sb.Append("; y ").Append(perdidosCambios).Append(" cambios mas sin anotar");
                nCambios = 0; perdidosCambios = 0; hayCambios = 0;
                return sb.ToString();
            }
        }
        internal void Fallo() { lock (cerrojo) { Anota(nivel, 0, CausaFallo, 0); nivel = 0; desde = 0; musicaEstable = 0; } }
        internal void Observa(long ahora, double musicaMs, double costoMs, double atrasoMs, int abierta, bool consumidor)
        {
            lock (cerrojo)
            {
                if (musicaMs <= 0) return;
                // El hilo de la musica (no consumidor) tiene su colchon: su atasco es pasarse de la mitad de el, no del 60 % del trozo.
                double topeCosto = consumidor ? Math.Max(10, musicaMs * .6) : Math.Max(musicaMs, BombaSonido.ColchonMs * .5);
                if (costoMs > topeCosto || atrasoMs > abierta * .25)
                { Anota(nivel, 0, CausaAtasco, (int)Math.Max(costoMs, atrasoMs)); nivel = 0; desde = ahora; musicaEstable = 0; return; }
                if (!consumidor || niveles[nivel] != abierta) return;
                if (desde == 0) desde = ahora;
                musicaEstable += musicaMs;
                double transcurrido = (ahora - desde) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (transcurrido >= 30000 && musicaEstable >= 30000 && nivel + 1 < niveles.Length)
                { Anota(nivel, nivel + 1, CausaBaja, 0); nivel++; desde = 0; musicaEstable = 0; }
            }
        }
    }

    internal sealed class FuenteLatenciaAudio : ISampleProvider
    {
        private readonly ISampleProvider src;
        private readonly PerfilLatenciaAudio perfil;
        private readonly int latencia;
        private readonly bool consumidor;
        private readonly BombaSonido bomba;
        private long ultimo, huecos;
        private double duracionPrevia;
        private volatile int atrasos, maxAtrasoMs;   // avisos de la salida que llegaron tarde (mas de un cuarto de la latencia) y el peor
        internal int Atrasos { get { return atrasos; } }
        internal int MaxAtrasoMs { get { return maxAtrasoMs; } }
        internal FuenteLatenciaAudio(ISampleProvider s, PerfilLatenciaAudio p, int ms, bool consume)
        { src = s; perfil = p; latencia = ms; consumidor = consume; bomba = s as BombaSonido; }
        public WaveFormat WaveFormat { get { return src.WaveFormat; } }
        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count)
        {
            long antes = System.Diagnostics.Stopwatch.GetTimestamp();
            int n;
            try { n = src.Read(buffer, offset, count); }
            catch { perfil.Fallo(); throw; }
            long ahora = System.Diagnostics.Stopwatch.GetTimestamp();
            double ms = n * 1000.0 / Math.Max(1, WaveFormat.SampleRate) / Math.Max(1, WaveFormat.Channels);
            double costo = (ahora - antes) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double atraso = consumidor && ultimo != 0 ? Math.Max(0, (antes - ultimo) * 1000.0 / System.Diagnostics.Stopwatch.Frequency - duracionPrevia) : 0;
            if (consumidor && atraso > latencia * .25) { atrasos++; if (atraso > maxAtrasoMs) maxAtrasoMs = (int)Math.Min(atraso, 600000.0); }
            if (bomba != null && (bomba.SinMusicaTemporal || bomba.Huecos != huecos))
            { huecos = bomba.Huecos; perfil.Fallo(); return n; }
            perfil.Observa(ahora, ms, costo, atraso, latencia, consumidor);
            ultimo = antes; duracionPrevia = ms;
            return n;
        }
    }

    public partial class Form1   // EN LINUX: (XP) «: Form». La rocola no hereda de nada: es la duena de su ventana, que le abre la pantalla nativa; lo que XP usaba de Form, con sus nombres, esta en Nativo/Form1.Ventana.cs
    {
        private System.Collections.Generic.Dictionary<string, PerfilLatenciaAudio> perfilesLatencia;
        private PerfilLatenciaAudio perfilLatencia;
        private int latenciaAbiertaMs;
        private bool latenciaClasica;
        private int PreparaLatenciaSalida(string backend, ISampleProvider muestras)
        {
            if (perfilesLatencia == null) perfilesLatencia = new System.Collections.Generic.Dictionary<string, PerfilLatenciaAudio>();
            bool clasica = backend == "waveout";
            string clave = backend + ":" + muestras.WaveFormat.SampleRate + ":" + muestras.WaveFormat.Channels;
            PerfilLatenciaAudio p;
            if (!perfilesLatencia.TryGetValue(clave, out p))
            {
                if (perfilesLatencia.Count >= 8) perfilesLatencia.Clear();
                perfilesLatencia[clave] = p = new PerfilLatenciaAudio(clasica);
            }
            perfilLatencia = p; latenciaClasica = clasica; latenciaAbiertaMs = p.Sugerida;
            return latenciaAbiertaMs;
        }
        private ISampleProvider MideLatenciaSalida(ISampleProvider muestras, bool consumidor)
        {
            if (perfilLatencia == null) return muestras;
            FuenteLatenciaAudio m = new FuenteLatenciaAudio(muestras, perfilLatencia, latenciaAbiertaMs, consumidor);
            if (consumidor) { medidorSalida = m; atrasosApuntados = 0; }
            return m;
        }

        // LA CAJA NEGRA DEL SONIDO (ver 6 arriba).
        private FuenteLatenciaAudio medidorSalida;      // el medidor del lado de la salida (el que ve los avisos tardios)
        private long cortesApuntados;                    // cortes de la bomba ya apuntados (de la salida abierta)
        private int atrasosApuntados;                    // avisos tardios ya apuntados
        // El texto de «[Cortes de sonido]» de lo ocurrido desde la ultima vez, o null si no hubo nada que contar.
        internal static string TextoCortes(string cancion, long cortes, int avisosTardios, int maxAtrasoMs, int latenciaMs)
        {
            if (cortes <= 0 && avisosTardios <= 0) return null;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[Cortes de sonido] ");
            if (!string.IsNullOrEmpty(cancion)) sb.Append('"').Append(cancion.Length > 80 ? cancion.Substring(0, 80) + "..." : cancion).Append("\": ");
            if (cortes > 0) sb.Append(cortes).Append(cortes == 1 ? " corte" : " cortes").Append(" del hilo de la musica (silencio entregado)");
            if (cortes > 0 && avisosTardios > 0) sb.Append(", ");
            if (avisosTardios > 0) sb.Append(avisosTardios).Append(avisosTardios == 1 ? " aviso tardio" : " avisos tardios").Append(" de la salida (el peor, ").Append(maxAtrasoMs).Append(" ms)");
            sb.Append("; latencia abierta ").Append(latenciaMs > 0 ? latenciaMs.ToString() : "?").Append(" ms");
            return sb.ToString();
        }
        // Al acabar o saltar una cancion: lo que paso mientras sono. Una sola linea, solo si hubo algo.
        private void ApuntaCortesDeSonido()
        {
            try
            {
                BombaSonido b = bomba; FuenteLatenciaAudio m = medidorSalida;
                long cortes = b != null ? b.Huecos : 0, nuevosCortes = cortes - cortesApuntados;
                int atrasos = m != null ? m.Atrasos : 0, maxAtraso = m != null ? m.MaxAtrasoMs : 0, nuevosAtrasos = atrasos - atrasosApuntados;
                if (b != null) cortesApuntados = cortes;
                if (m != null) { atrasosApuntados = atrasos; }
                string cancion = nowPlaying != null && nowPlaying.Song != null ? (nowPlaying.Song.Title ?? nowPlaying.Song.Path) : null;
                string t = TextoCortes(cancion, nuevosCortes, nuevosAtrasos, maxAtraso, latenciaAbiertaMs);   // el peor, de la salida abierta
                if (EsMudaDiag(nowPlaying)) return;   // la cancion muda del diagnostico no es una cancion real: nada a la caja negra
                if (t != null) ApuntaCajaNegra(t);
                ApuntaLatencia();
            }
            catch { }
        }
        // Los cambios de nivel de la latencia, si hubo (el reloj de la cancion lo mira cada 500 ms: solo un entero).
        private void ApuntaLatencia()
        {
            PerfilLatenciaAudio p = perfilLatencia;
            if (p == null || !p.HayCambios) return;
            string c = p.TomaCambios();
            if (c != null) ApuntaCajaNegra("[Latencia de audio] " + c);
        }
        private void ApuntaCajaNegra(string linea)
        {
            try { Bitacora.ApuntaConSello(RutaDatos("rocola_error.txt"), 262144, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + linea, "\r\n"); }
            catch { }
        }
        private int RetardoVisualSalida()
        { return latenciaAbiertaMs <= 0 ? 850 : (int)(latenciaAbiertaMs * (latenciaClasica ? .85 : .8)); }
        // ------------------------------------------------------------------ 1) EL FORMATO
        internal Func<IWavePlayer> fabricaSalidaClasica = null;   // solo la ponen las pruebas (null = WaveOut de verdad)
        private string formatoSalida = "";                 // "float32" o "PCM16+TPDF": lo que recibe la salida clasica
        private bool flotanteRechazado;                    // en esta sesion el sistema ya rechazo la coma flotante

        // Windows Vista o posterior: alli la salida clasica pasa por el motor de audio, que mezcla en coma flotante.
        private static bool FlotanteAdmitido()
        {
            // EN LINUX: (Environment.OSVersion: Win32NT y Version.Major >= 6) lo que decidia en el A4, con Windows XP (5.1): no. La tarjeta recibe
            return false;   // 16 bits con dither, como en el bar (en Linux la version seria la del nucleo, 6.x, y diria que si).
        }

        // T5 (SalidaPropia.cs): la salida con su propio hilo, salvo que en esta sesion no haya abierto o haya fallado por si misma.
        private bool salidaPropiaFallo;
        private IWavePlayer CreaWaveOut()
        {
            if (fabricaSalidaClasica != null) return fabricaSalidaClasica();
            if (!salidaPropiaFallo)
            {
                WaveOutPropio p = new WaveOutPropio();
                p.DesiredLatency = latenciaAbiertaMs > 0 ? latenciaAbiertaMs : 1000;
                p.NumberOfBuffers = p.DesiredLatency / 50;   // los mismos bloques de 50 ms
                return p;
            }
            // WaveOut con callback por VENTANA, no WaveOutEvent: el de eventos llama a WaitHandle.WaitOne(Int32),
            // que NO existe en .NET 3.5 sin Service Pack 1 (el de la rockola de XP) y truena. Este no lo usa.
            WaveOutPropio wo = new WaveOutPropio(); wo.LaDeSiempre = true;   // EN LINUX: (new WaveOut(): el WaveOut de NAudio, winmm con aviso por ventana) no hay otro camino a la tarjeta que ALSA: la de / siempre es otra salida propia nueva, con la misma latencia y los mismos bloques de 50 ms
            // El audio se rellena desde el bucle de mensajes de la interfaz; con 1 SEGUNDO de colchon la musica
            // aguanta cualquier atoron (paginas, caratulas, GC, video) sin cortarse.
            wo.DesiredLatency = latenciaAbiertaMs > 0 ? latenciaAbiertaMs : 1000;
            // BLOQUES CORTOS: cada vez que se vacia un bloque la interfaz se para a descifrar el MP3 y a pasarlo
            // por la cadena. Con 20 bloques de 50 ms se hace el mismo trabajo en pausas cuatro veces mas cortas
            // que con 5 de 200 ms, y caben de sobra dentro de un cuadro. El colchon sigue siendo de 1 segundo.
            wo.NumberOfBuffers = wo.DesiredLatency / 50;
            return wo;
        }

        // Abre la salida clasica con las muestras de la cadena y la deja en 'output' (con su aviso de parada).
        // 'flotante': probar primero coma flotante. Si el sistema la rechaza, 16 bits con dither TPDF. Un fallo
        // del APARATO no se confunde con un rechazo de formato: se relanza tal cual.
        private void AbreSalidaClasica(ISampleProvider muestras, bool flotante)
        {
            PreparaLatenciaSalida("waveout", muestras);
            muestras = ConBomba(muestras);   // la cadena la pasa el hilo de la musica; la pantalla solo copia (BombaSonido.cs)
            muestras = MideLatenciaSalida(muestras, true);
            Exception rechazo = null;
            if (flotante && !flotanteRechazado)
            {
                try { IniciaSalida(new SampleToWaveProvider(muestras)); formatoSalida = "float32"; return; }
                catch (Exception ex)
                {
                    if (EsFalloDelDispositivo(ex)) throw;
                    rechazo = ex;
                }
            }
            IniciaSalida(new DitherWaveProvider16(muestras)); formatoSalida = "PCM16+TPDF";
            if (rechazo != null)
            {
                // Los 16 bits si abrieron: era el formato. Se recuerda para no volver a probar en esta sesion.
                flotanteRechazado = true;
                LogError("La salida de sonido no admite coma flotante: sigue en 16 bits con dither", rechazo);
            }
        }

        // Abre la salida clasica con 'prov' y la deja en 'output' (con su aviso de parada). La propia (T5) y, si no abre por algo que no
        // es el aparato ni el formato, la de siempre durante esta sesion. Si no abre ninguna, la excepcion sale como antes.
        private void IniciaSalida(IWaveProvider prov)
        {
            IWavePlayer w = CreaWaveOut();
            output = w; w.PlaybackStopped += Output_PlaybackStopped;
            try { w.Init(prov); return; }
            catch (Exception ex)
            {
                w.PlaybackStopped -= Output_PlaybackStopped; output = null;
                try { w.Dispose(); } catch { }
                if (!EsLaPropia(w) || EsFalloDelDispositivo(ex) || EsFormatoRechazado(ex)) throw;   // EN LINUX: (w is WaveOutPropio) EsLaPropia
                salidaPropiaFallo = true;
                LogError("La salida de sonido con hilo propio no abrio: sigue la de siempre en esta sesion", ex);
            }
            IWavePlayer v = CreaWaveOut();
            output = v; v.PlaybackStopped += Output_PlaybackStopped;
            try { v.Init(prov); }
            catch
            {
                v.PlaybackStopped -= Output_PlaybackStopped; output = null;
                try { v.Dispose(); } catch { }
                throw;
            }
        } private static bool EsLaPropia(object salida) { WaveOutPropio p = salida as WaveOutPropio; return p != null && !p.LaDeSiempre; }   // EN LINUX: ¿es la salida con hilo propio (T5) y no la de siempre? En XP, «is WaveOutPropio» (la de siempre era el WaveOut de NAudio); / aqui las dos son WaveOutPropio sobre ALSA y la de siempre lleva LaDeSiempre.
        private static bool EsFormatoRechazado(Exception ex)
        {
            // EN LINUX: (NAudio.MmException con MmResult.WaveBadFormat) ErrorAlsa de la tarjeta que no acepto el formato, los canales o la
            ErrorAlsa al = ex as ErrorAlsa; return al != null && al.Formato;   // frecuencia (snd_pcm_hw_params_set_* con EINVAL; Nativo/SalidaAlsa.cs)
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
                ErrorAlsa mm = e as ErrorAlsa;   // EN LINUX: (NAudio.MmException y su MmResult, el error de winmm) ErrorAlsa y su numero de ALSA (Nativo/SalidaAlsa.cs), con / los que dicen lo mismo
                if (mm != null)
                {
                    switch (mm.Codigo)
                    {
                        case -ErrorAlsa.ENOENT:           // no hay dispositivo con ese numero (desenchufado)   // EN LINUX: (BadDeviceId) no hay tarjeta con ese nombre
                        case -ErrorAlsa.ENXIO:            // EN LINUX: (BadDeviceId) ni ese numero de tarjeta
                        case -ErrorAlsa.ENODEV:           // EN LINUX: (BadDeviceId e InvalidHandle) la tarjeta ya no esta (se desenchufo, tambien con la salida abierta)
                        // EN LINUX: (NotEnabled, controlador deshabilitado) no puede darse: en Linux un controlador quitado es una tarjeta que no esta (ENOENT)
                        case -ErrorAlsa.EBUSY:            // lo tiene otro programa en exclusiva   // EN LINUX: (AlreadyAllocated) la tiene otro programa (ALSA directo: de uno solo)
                        case -ErrorAlsa.EBADFD: case -ErrorAlsa.EIO: case ErrorAlsa.SinAlsa:   // el que estaba abierto ya no existe   // EN LINUX: (InvalidHandle) la abierta quedo inservible / EN LINUX: (InvalidHandle) la tarjeta dejo de contestar o de pedir sonido / no hay controlador   // EN LINUX: (NoDriver) no esta ALSA (libasound) / EN LINUX: (NoDriverCallback) no puede darse: ALSA no avisa por el controlador
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
        private Reloj reintentoSalida;   // EN LINUX: Reloj de la pantalla nativa (Nativo/Reloj.cs; en XP, Timer de WinForms: SetTimer y WM_TIMER en el hilo de la ventana)
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
            try
            {
                double leido = r.CurrentTime.TotalSeconds, total = r.TotalTime.TotalSeconds;
                double pendientes;
                double oido;
                if (PosicionPorReloj(out oido)) return LimitaPosicion(oido, total);
                if (cadenaSonando == null && PendientePorReloj(out pendientes, out oido)) return LimitaPosicion(leido - pendientes, total); // fuente sintetica sin cadena
                return PosicionOida(leido, total, RetrasoAudible());
            }
            catch { return 0; }
        }
        private double retardoFinalReloj;
        private static double LimitaPosicion(double p, double total)
        { return double.IsNaN(p) || p < 0 ? 0 : (total > 0 && p > total ? total : p); }
        private bool PosicionPorReloj(out double segundos)
        {
            segundos = 0;
            CadenaCancion c = cadenaSonando; Encadenado s = secuencia;
            double pendientes, oido;
            if (c == null || s == null || !PendientePorReloj(out pendientes, out oido)) return false;
            double salida = oido
                            - (double)c.CuadroComienzo / Math.Max(1, s.WaveFormat.SampleRate)
                            - (double)c.CebadoInicial / Math.Max(1, c.Lector.WaveFormat.SampleRate);
            salida = Math.Max(0, salida);
            FuenteSinSilencios silencios = c.Lector as FuenteSinSilencios;
            segundos = silencios != null ? silencios.MusicaOida(salida) : c.InicioMusica + salida;
            return true;
        }

        // GetPosition existe en WaveOut de XP. No se abre otro aparato ni se exige una API moderna.
        // El reloj cuenta lo entregado al dispositivo; la secuencia cuenta tambien el audio aun en la bomba.
        // Si hubo huecos sinteticos o el driver no puede devolver su reloj, se conserva el respaldo anterior.
        private IWavePosition relojSalida;
        private long relojAnterior, relojVueltas;
        private bool relojReiniciado;
        private long AmpliaRelojSalida(IWavePosition salida, long bytes)
        {
            if (!object.ReferenceEquals(relojSalida, salida))
            { relojSalida = salida; relojAnterior = 0; relojVueltas = 0; relojReiniciado = false; }
            if (bytes < 0 || relojReiniciado) return -1;
            if (!latenciaClasica && !(salida is WaveOutPropio && ((WaveOutPropio)salida).LaDeSiempre)) return bytes; // WASAPI ya da 64 bits   // EN LINUX: (salida is WaveOut) la de siempre: una WaveOutPropio con LaDeSiempre
            if (bytes > uint.MaxValue) return -1;
            if (bytes < relojAnterior)
            {
                if (relojAnterior - bytes > 0x80000000L) relojVueltas += 0x100000000L;
                else { relojReiniciado = true; return -1; } // reset, no inventar audio oido
            }
            relojAnterior = bytes;
            return relojVueltas + bytes;
        }

        private bool PendientePorReloj(out double segundos, out double oido)
        {
            segundos = 0; oido = 0;
            Encadenado s = secuencia; IWavePosition p = output as IWavePosition;
            if (s == null || p == null || (bomba != null && bomba.Huecos > 0)) return false;
            try
            {
                WaveFormat f = p.OutputWaveFormat;
                if (f == null || f.AverageBytesPerSecond <= 0) return false;
                // Una sola instantanea de lo entregado: Read puede avanzar incluso durante GetPosition.
                long cuadros = s.CuadrosEntregados;
                long bytes = AmpliaRelojSalida(p, p.GetPosition());
                if (bytes < 0) return false;
                double entregado = (double)cuadros / Math.Max(1, s.WaveFormat.SampleRate);
                // Los consumidores usan ESTE reloj directamente, nunca otro CuadrosEntregados - pendientes.
                oido = (double)bytes / f.AverageBytesPerSecond - retardoFinalReloj;
                segundos = entregado - oido;
                return segundos >= 0 && segundos <= Math.Max(5.0, RetrasoAudible() / 1000.0 + 2.0);
            }
            catch { return false; }
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
            if (t == null || audioOpeningForMeasurement || EsMudaDiag(nowPlaying)) return false;   // la muda del diagnostico no se reintenta (sonaria sin Callada)
            if (!SigueEsperando(nowPlaying, aMedias)) return false;
            if (perfilLatencia != null) perfilLatencia.Fallo();
            if (reintentos == 0)
                LogError("Salida de sonido" + (formatoSalida.Length > 0 ? " (" + formatoSalida + ")" : "") + ": fallo el dispositivo; se espera y se retoma la misma cancion", ex);
            DisposePlaybackNow();
            reintentoCancion = t; reintentoDesde = desde;
            int espera = EsperaReintento(reintentos);
            reintentos++;
            if (reintentoSalida == null) { reintentoSalida = new Reloj(); reintentoSalida.Tick += ReintentoSalida_Tick; }   // EN LINUX: Reloj de la pantalla nativa (WM_TIMER)
            reintentoSalida.Interval = espera;
            reintentoSalida.Start();
            // Mientras se reintenta sigue puesto (cada reintento lo renueva); cuando el aparato vuelve, se va solo.
            EstadoPasajero("Sin salida de sonido: la musica sigue en cuanto vuelva el aparato", espera + EstadoPasajeroMs);
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
                return SalidaAlsa.Tarjeta();   // EN LINUX: (el id del aparato predeterminado de WASAPI: MMDeviceEnumerator.GetDefaultAudioEndpoint(Render, Console).ID) la / tarjeta de la rocola en ALSA. No se llega: FlotanteAdmitido es lo de XP (no), y en XP aqui no se pregunta.
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
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
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
