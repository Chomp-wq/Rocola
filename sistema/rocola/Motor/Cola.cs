// LA COLA Y EL RELEVO SIN PAUSA (en XP, Form1.SinPausa.cs: PreparaSiguienteSinPausa, RevisaRelevo y PasaRelevo; Form1.Video.cs:
// PlayNext y DisposePlaybackNow), sin la pantalla. Las reglas son las de XP: la siguiente se prepara cuando a la que suena le quedan
// 12 s y entra en la MISMA lectura de la salida (Encadenado, SinPausa.cs), sin hueco; si es la pista siguiente del mismo disco, sin
// fundido y con la memoria de los filtros de la anterior (SigueA, HeredaLocal). Lo que se aprende de cada una se cierra al relevo.
// Diferencia con XP: sin el calentador que abre la siguiente por delante (se abre aqui). Con otra frecuencia, se convierte como en XP.
using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        private Dictionary<string, Track> pistaDeRuta;   // de la biblioteca leida (LeeBiblioteca): la cancion y su disco
        private Dictionary<string, Album> discoDeRuta;
        private PlayItem sinEncadenar;                    // la siguiente que no se puede encadenar (otra frecuencia): no se reintenta
        private int ultimoMuestreo;

        private void IndexaBiblioteca()
        {
            pistaDeRuta = new Dictionary<string, Track>(StringComparer.Ordinal);
            discoDeRuta = new Dictionary<string, Album>(StringComparer.Ordinal);
            foreach (Album a in albums) foreach (Track t in a.Tracks) { pistaDeRuta[t.Path] = t; discoDeRuta[t.Path] = a; }
        }

        // A la cola. Si es de la biblioteca leida, con su disco (para el disco continuo).
        public void Encola(string ruta)
        {
            PlayItem p = new PlayItem();
            Track t = null; Album d = null;
            if (pistaDeRuta != null) { pistaDeRuta.TryGetValue(ruta, out t); discoDeRuta.TryGetValue(ruta, out d); }
            if (t == null) { t = new Track(); t.Path = ruta; }
            p.Song = t; p.Disc = d;
            queue.Add(p);
        }
        public int EnCola { get { return queue.Count; } }

        // LOS VIDEOS. En la cola van como en XP (un PlayItem de video); suenan fuera de la cadena de sonido, con su propio volumen
        // (video_vol=, 0 a 100, como el de XP). Si la primera de la cola es un video y no suena nada: se saca y se devuelve su ruta.
        public string TomaVideo()
        {
            if (nowPlaying != null || queue.Count == 0 || !queue[0].IsVideo) return null;
            PlayItem it = queue[0]; queue.RemoveAt(0);
            sonoAntes = nowPlaying; nowPlaying = it;
            SaveState();
            return it.VideoPath;
        }
        public void AcaboElVideo() { nowPlaying = null; SaveState(); }
        // LO PAGADO QUE NO SONO (4-11-2026, decision del dueno): una cancion que no se puede abrir (archivo danado o que ya no esta) o
        // un video que no arranca devuelve su credito, con aviso. En XP se apuntaba y se saltaba, y el credito se perdia. La que no
        // abre queda como danada (no se puede volver a pedir mientras el archivo no cambie). Lo que la tarjeta no deja sonar no pasa
        // por aqui: vuelve a la cola (AcaboPorFallo).
        public void DevuelveCredito(string porque)
        {
            if (credits < int.MaxValue) credits++;
            UpdateCredits(); SaveState();
            Avisa(porque + ": se devuelve el crédito");
        }
        // EL VOLUMEN DE UN VIDEO EN ROCOLA OS (3-11-2026; en XP, VideoVolumeFor de Form1.Video.cs), como factor de la amplitud del archivo.
        // En XP el reproductor de Windows solo sabia BAJAR (0 a 100): por eso la perilla dejaba hueco (85) y un video flojo se quedaba
        // flojo. Aqui mpv puede subir, y el sonido de los videos se mide (AudioDeVideo.cs): uno medido, con el nivelado encendido, va al
        // mismo nivel que las canciones sin medir (nivel_objetivo=, «Videos y canciones aun sin medir»), entre -16 y +12 dB como en XP,
        // y la perilla ya no hace falta (en XP, el base que elegia el programa con bastantes medidos: aqui, sin tope, el 100). Sin medir,
        // o sin nivelado, la perilla. Los picos los cuida el limitador que mpv lleva delante (Controlador: alimiter al techo de la cadena).
        public double VolumenVideo(string ruta)
        {
            float rms;
            if (normalizeOn && ruta != null && loudCache.TryGetValue(ruta, out rms) && rms > 0.0001f)
                return Math.Max(0.15, Math.Min(4.0, normTarget / rms));
            return videoVol / 100.0;
        }
        public float TechoVideos { get { return TechoPicoReal; } }
        public bool SonandoVideo { get { PlayItem p = nowPlaying; return p != null && p.IsVideo; } }
        public string Sonando { get { PlayItem p = nowPlaying; return p != null && p.Song != null ? p.Song.Path : null; } }
        // Cuanto lleva la que suena (0 a 1) y su disco (para la pantalla).
        public float SonandoProgreso
        {
            get
            {
                CadenaCancion c = cadenaSonando;
                try { double t = c != null ? c.Lector.TotalTime.TotalSeconds : 0; return t > 0 ? (float)Math.Min(1.0, c.Lector.CurrentTime.TotalSeconds / t) : 0f; }
                catch { return 0f; }
            }
        }
        // Las portadas de las siguientes de la cola (las cajitas de «En cola»); null donde no hay.
        public List<string> PortadasEnCola(int n)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < queue.Count && r.Count < n; i++) r.Add(queue[i].Disc != null ? queue[i].Disc.CoverPath : null);
            return r;
        }
        public string SonandoTitulo { get { PlayItem p = nowPlaying; return p == null ? null : p.IsVideo ? Path.GetFileNameWithoutExtension(p.VideoPath) : p.Song != null ? p.Song.ToString() : null; } }
        public string SonandoDisco { get { PlayItem p = nowPlaying; return p != null && p.Disc != null ? (string.IsNullOrEmpty(p.Disc.Name) ? p.Disc.Artist : p.Disc.Artist + " - " + p.Disc.Name) : null; } }
        public string SonandoPortada { get { PlayItem p = nowPlaying; return p != null && p.Disc != null ? p.Disc.CoverPath : null; } }
        public float SonandoGananciaDb { get { CadenaCancion c = cadenaSonando; return c != null && c.Tap != null ? (float)(20.0 * Math.Log10(Math.Max(1e-9f, c.Tap.Gain))) : 0f; } }

        // EMPIEZA LA SIGUIENTE DE LA COLA por el camino de siempre (como PlayNext: salida nueva). Lo que devuelve es lo que la salida
        // tiene que leer; null si la cola se acabo. Una que no se puede abrir se apunta y se salta, como en XP.
        private PlayItem esperandoConversion; private int esperaDesde;
        private int discoFueraDesde; internal const int DiscoFueraMaxMs = 10 * 60 * 1000;
        public ISampleProvider EmpiezaSiguiente()
        {
            while (queue.Count > 0)
            {
                PlayItem it = queue[0];
                if (it.IsVideo) return null;   // los videos los toca quien toca (TomaVideo)
                // UNA DE SPOTIFY (Spotify.cs): la pide al programa de Spotify y suena por la cadena de siempre; si no se puede, se devuelve su credito
                if (EsItemSpotify(it))
                {
                    queue.RemoveAt(0);
                    ISampleProvider sp = EmpiezaSpotify(it);
                    if (sp != null) return sp;
                    DevuelveCredito("Spotify: no se pudo tocar «" + it.Song.Title + "»" + (SpotifyListo ? "" : " (sin conexión con Spotify)"));
                    continue;
                }
                // UNA TUYA CON LOS AGUDOS CORTADOS Y SU VERSION EN SPOTIFY (SP2, SpotifyMas.cs): suena desde Spotify sin que nada cambie en la
                // pantalla; si Spotify no puede, la tuya, como siempre
                string sid = IdParaSustituir(it);
                if (sid != null)
                {
                    queue.RemoveAt(0);
                    ISampleProvider sp = EmpiezaSpotify(it, sid, PistaDeSustituta(it, sid), true);
                    if (sp != null) { try { EmpiezaVerificacion(it, sid); } catch { } return sp; }   // C: la primera vez, se comprueba con el sonido
                    sinSpotify.Add(it); queue.Insert(0, it);
                }
                // UN «MP3» QUE NO LO ES Y SE ESTA CONVIRTIENDO (Conversiones.cs): espera al principio de la cola hasta EsperaMaxMs y suena;
                // si tarda mas, se devuelve el credito y la conversion sigue para la proxima vez.
                if (it.Song != null && Conversiones.Convirtiendo(it.Song.Path))
                {
                    if (!object.ReferenceEquals(esperandoConversion, it)) { esperandoConversion = it; esperaDesde = Environment.TickCount; Avisa("Preparando «" + it.Song.ToString() + "»... un momento"); }
                    if (unchecked(Environment.TickCount - esperaDesde) < Conversiones.EsperaMaxMs) return null;   // se vuelve a mirar en el siguiente ciclo
                    queue.RemoveAt(0); esperandoConversion = null; SaveState();
                    DevuelveCredito("«" + it.Song.ToString() + "» tarda en prepararse (estará lista para la próxima)");
                    continue;
                }
                if (object.ReferenceEquals(esperandoConversion, it)) esperandoConversion = null;
                // EL DISCO DE LA MUSICA DESCONECTADO (4-11-2026): como al arrancar (RestoreState), la cola espera a que vuelva en vez de
                // fallar cancion por cancion (cada una devolveria su credito y quedaria como danada). Si no vuelve en 10 min, como antes.
                if (it.Song != null && RaizConfiguradaAusente(it.Song.Path) != null)
                {
                    if (discoFueraDesde == 0) { discoFueraDesde = Environment.TickCount | 1; Avisa("Esperando el disco de música: la cola se conserva"); LogError("Disco de musica desconectado: la cola espera", null); }
                    if (unchecked(Environment.TickCount - discoFueraDesde) < DiscoFueraMaxMs) return null;
                }
                else if (discoFueraDesde != 0) { discoFueraDesde = 0; Avisa("Volvió el disco de música"); }
                queue.RemoveAt(0);
                try
                {
                    CadenaCancion cad = EmpiezaItem(it);
                    secuencia = new Encadenado(cad.Salida, cad);
                    sinEncadenar = null;
                    pulso = new MedidorPulso(secuencia);   // el pulso para la pantalla (no toca el sonido: Pulso.cs)
                    return pulso;
                }
                catch (Exception ex)
                {
                    LogError("No se pudo tocar " + System.IO.Path.GetFileName(it.Song.Path), ex); nowPlaying = null;
                    NoEsMp3Exception nm = ex as NoEsMp3Exception;
                    if (nm != null && nm.Convirtiendose) { queue.Insert(0, it); continue; }   // recien pedida su conversion: a esperarla (arriba)
                    try { danadas[it.Song.Path] = AudioFileSignature(it.Song.Path); } catch { }   // no se vuelve a cobrar (Pedido.Danada)
                    DevuelveCredito("No se pudo tocar «" + it.Song.ToString() + "»" + (ex is NotSupportedException ? " (" + ex.Message + ")" : ""));
                }
            }
            return null;
        }

        // Lo de StartOpenedTrack de XP (Form1.Sonido.cs): la cadena y, en los campos de siempre, lo que se aprende oyendola.
        private CadenaCancion EmpiezaItem(PlayItem item)
        {
            if (cadenaSonando != null) throw new InvalidOperationException("ya suena otra");
            IAudioSource lector = OpenAudio(item.Song.Path);
            try
            {
                lector.Volume = 1f;
                sonoAntes = nowPlaying; nowPlaying = item;
                if (item != null && !item.IsVideo && item.Song != null) try { item.Song.EnsureTags(); } catch { }   // su genero, al empezarla (como XP)
                CadenaCancion cad = ArmaCadena(item.Song, lector, false, 0.0);
                SellaInicioReloj(cad);
                cad.Item = item;
                lock (audioDatosLock)
                {
                    curMeasurePath = cad.MedidaRuta; curSumSq = 0; curSamples = 0;
                    curFuenteMedida = cad.Fuente;
                    eqMeasPath = cad.EqRuta;
                    eqMeasCut = 0f;   // el acantilado es de ESTA cancion: se vuelve a medir
                    eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                    kwRate = 0;   // cada cancion mide desde cero
                    tapMedido = cad.Tap;
                }
                curSbr = cad.Sbr; curTap = cad.Tap; curBass = cad.Bajo;
                bassSongFactor = cad.BajoFactor; bassAutoBase = cad.BajoBase;
                cadenaSonando = cad; cadenaPreparada = null;
                return cad;
            }
            catch { lector.Dispose(); throw; }
        }

        // LO QUE EN XP HACEN LOS RELOJES DE LA PANTALLA. Llamarlo a menudo (cada 50 a 500 ms) mientras suena: muestrea lo que hizo cada
        // etapa (cada medio segundo), prepara la siguiente cuando toca y recoge el relevo cuando ya se oye. 'cuadrosOidos' = cuadros de la
        // salida que ya sonaron (lo escrito menos lo que la tarjeta aun tiene por delante).
        public void Atiende(long cuadrosOidos)
        {
            int ahora = Environment.TickCount;
            if (unchecked(ahora - ultimoMuestreo) >= 500) { ultimoMuestreo = ahora; MuestreaEtapas(cadenaSonando); }
            PreparaSiguiente();
            Encadenado s = secuencia;
            if (s == null) return;
            Encadenado.Relevo r = s.TomaRelevoPorCuadro(Math.Max(0L, cuadrosOidos));
            CadenaCancion b = r != null ? r.Ficha as CadenaCancion : null;
            if (b != null) { b.CuadroComienzo = r.Cuadro; PasaRelevo(b); }
        }

        // Como PreparaSiguienteSinPausa de XP, sin el calentador ni la conversion de frecuencia.
        private void PreparaSiguiente()
        {
            Encadenado s = secuencia;
            if (s == null || cadenaPreparada != null || cadenaSonando == null) return;
            if (EsItemSpotify(nowPlaying) || cadenaSonando.Lector is FuenteSpotify) return;   // Spotify no lleva relevo (Spotify.cs): cada una abre su salida
            if (s.Terminado || s.HayRelevoPendiente || queue.Count == 0) return;
            PlayItem sig = queue[0];
            if (sig == null || sig.IsVideo || sig.Song == null || sig.Song.Path == null || object.ReferenceEquals(sig, sinEncadenar)) return;
            if (EsItemSpotify(sig) || SeSustituye(sig)) return;   // (la de Spotify, y la tuya que suena desde Spotify, entran por el camino de siempre)
            double quedan;
            try { quedan = (cadenaSonando.Lector.TotalTime - cadenaSonando.Lector.CurrentTime).TotalSeconds; } catch { return; }
            if (quedan > PreparaAntesSeg) return;
            IAudioSource lector;
            try { lector = OpenAudio(sig.Song.Path); }
            catch { sinEncadenar = sig; return; }   // entrara por el camino de siempre, que la apunta y la salta
            CadenaCancion c;
            try
            {
                lector.Volume = 1f;
                // OTRA FRECUENCIA (48000 tras 44100...): se convierte a la de la salida con el convertidor de XP (RemuestreoContinuo, con su
                // limitador detras), como su relevo sin pausa; si no se puede (otra disposicion, demasiado trabajo), por el camino de siempre.
                double[] tabla = null;
                if (!Encadenado.MismoFormato(lector.WaveFormat, s.WaveFormat))
                {
                    tabla = RemuestreoContinuo.CapturaTabla(lector.WaveFormat, s.WaveFormat);
                    if (tabla == null)
                    {
                        lector.Dispose();
                        if (!RemuestreoContinuo.SePuede(lector.WaveFormat, s.WaveFormat)) sinEncadenar = sig;   // no se puede: por el camino de siempre
                        return;   // la tabla se esta preparando en segundo plano: se vuelve a mirar en el siguiente ciclo
                    }
                }
                c = ArmaCadena(sig.Song, lector, false, 0);
                c.Item = sig;
                SellaInicioReloj(c); c.CebadoInicial = 0;   // Encadenado descarta el cebado de los relevos
                if (!Encadenado.MismoFormato(c.Salida.WaveFormat, s.WaveFormat)) ConvierteSalida(c, s.WaveFormat, tabla);
            }
            catch (Exception ex)
            {
                LogError("Relevo sin pausa: no se pudo preparar " + System.IO.Path.GetFileName(sig.Song.Path) + " (sonara por el camino de siempre)", ex);
                try { lector.Dispose(); } catch { }
                sinEncadenar = sig;
                return;
            }
            if (!s.Prepara(c.Salida, c.Retardo, c)) { try { lector.Dispose(); } catch { } return; }
            cadenaPreparada = c;
            // PISTA SIGUIENTE DEL MISMO DISCO: sin fundido de salida (si aun no empezo) ni de entrada, y con la memoria de los filtros.
            bool entera = cadenaSonando.Fundido == null;
            if (SiguienteDelDisco(nowPlaying, sig) && cadenaSonando.Fundido != null) entera = cadenaSonando.Fundido.Quita();
            if (entera && SiguienteDelDisco(nowPlaying, sig) && c.Fundido != null) c.Fundido.SinEntrada();
            if (entera && lector.WaveFormat.SampleRate == cadenaSonando.Lector.WaveFormat.SampleRate) { SigueA(c, cadenaSonando); HeredaLocal(c, cadenaSonando); }
        }

        // Como PasaRelevo de XP, sin la pantalla: la que acabo cierra lo aprendido, la que entra pasa a sonar y sale de la cola.
        private void PasaRelevo(CadenaCancion b)
        {
            CadenaCancion a = cadenaSonando;
            AprendeParejo(a);   // la que acabo, si sono entera en el billar (VolumenParejo.cs)
            cadenaSonando = b;
            if (object.ReferenceEquals(cadenaPreparada, b)) cadenaPreparada = null;
            lock (audioDatosLock)
            {
                MedidaCancion m = b.Tap != null ? b.Tap.Medida : null;
                if (m != null && m.CierreAnterior.Listo) CierraMedida(m.CierreAnterior, eqMeasCut, eqMeasCutLo);
                else { FinalizeLoudMeasure(); FinalizeEqMeasure(); InstalaMedida(b); }
            }
            if (a != null && a.Lector != null && !object.ReferenceEquals(a.Lector, b.Lector)) { try { a.Lector.Dispose(); } catch { } }
            eqMeasCut = 0f; eqMeasCutLo = 0f;
            curTap = b.Tap; curSbr = b.Sbr; curBass = b.Bajo;
            bassSongFactor = b.BajoFactor; bassAutoBase = b.BajoBase;
            PlayItem item = b.Item;
            if (queue.Count > 0 && object.ReferenceEquals(queue[0], item)) queue.RemoveAt(0);
            else if (item != null) queue.Remove(item);
            sonoAntes = nowPlaying;
            nowPlaying = item;
            if (item != null && !item.IsVideo && item.Song != null) try { item.Song.EnsureTags(); } catch { }   // su genero, al empezarla (como XP)
        }

        // LA SALIDA SE ACABO (o se paro): como RecogeAlParar y DisposePlaybackNow de XP. Si la siguiente ya habia entrado, pasa a sonar
        // y se cierra; la preparada que no entro vuelve a la cola; lo de la que sonaba se aprende y se guarda.
        // SIN TARJETA, LA COLA ESPERA (Rocola OS, 4-11-2026). Si la salida no abre o se cae a media cancion (la tarjeta desconectada, un
        // error de E/S), lo que sonaba no se da por oido: se cierra como siempre y vuelve al principio de la cola, para sonar entera cuando la
        // tarjeta vuelva (como al volver la luz). Antes, cada intento fallido se comia una cancion pagada: en segundos, la cola entera.
        public void AcaboPorFallo()
        {
            PlayItem p = CierraLaSalida();   // la que de verdad sonaba (con el relevo ya recogido), no la de antes
            if (p != null && !p.IsVideo && p.Song != null && (queue.Count == 0 || !object.ReferenceEquals(queue[0], p)))
            {
                queue.Insert(0, p); RefrescaCola();
            }
            SaveState();
        }
        public void AcaboLaSalida() { CierraLaSalida(); }
        // Devuelve la que sonaba al final (si la siguiente ya habia entrado por el relevo, esa).
        private PlayItem CierraLaSalida()
        {
            Encadenado s = secuencia;
            if (s != null)
            {
                // (7-11-2026) un fallo al leer a media cancion (archivo danado por la mitad): se acaba como siempre, pero se apunta de cual y que
                // paso (como XP, RecogeAlParar); antes se quedaba guardado sin que nadie lo recogiera
                object quien; Exception fallo;
                if (s.TomaFallo(out quien, out fallo))
                {
                    CadenaCancion fc = quien as CadenaCancion;
                    string cual = fc != null && fc.Cancion != null ? System.IO.Path.GetFileName(fc.Cancion.Path) : "la que sonaba";
                    LogError("Se corto al leer " + cual, fallo);
                }
                Encadenado.Relevo r = s.TomaRelevo(Environment.TickCount, int.MinValue);
                CadenaCancion b = r != null ? r.Ficha as CadenaCancion : null;
                if (b != null) { b.CuadroComienzo = r.Cuadro; PasaRelevo(b); }
                CadenaCancion p = cadenaPreparada;
                if (p != null && s.Cancela(p)) { try { p.Lector.Dispose(); } catch { } }
            }
            cadenaPreparada = null;
            InvalidaPublicacionAudio();
            lock (audioDatosLock)
            {
                CadenaCancion c = cadenaSonando;
                if (c != null) AprendeParejo(c);
                FinalizeLoudMeasure();
                FinalizeEqMeasure();
                if (c != null && c.Lector != null) { try { c.Lector.Dispose(); } catch { } }
                cadenaSonando = null; secuencia = null; tapMedido = null; pulso = null;
            }
            curTap = null; curSbr = null; curBass = null;
            if (EsItemSpotify(nowPlaying) || (spotifySustituida != null && object.ReferenceEquals(nowPlaying, spotifySustituida))) TerminaSpotify();   // que el programa de Spotify calle (Spotify.cs)
            PlayItem ultima = nowPlaying;
            nowPlaying = null;
            return ultima;
        }
    }
}
