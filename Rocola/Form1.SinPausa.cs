// LA CONTINUIDAD ENTRE CANCIONES: la siguiente entra sin pausa por la misma salida
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. El mecanismo de audio (la secuencia que
// alimenta la salida y la medida de la cancion que entra) esta en SinPausa.cs; aqui va lo que decide la
// rocola: cuando se prepara la siguiente, y que pasa en la cola y en la pantalla cuando de verdad suena.
//
// Lo propio de cada rama (la IA en vivo y el DSEE de HD, el colchon de la salida) va en unos pocos metodos
// de Form1.Sonido.cs con el mismo nombre en las dos: ArmaCadena, IaMejoradaPara, DseeDe, ExcitePara,
// AlEntrarPorRelevo y RetrasoAudible. Este archivo es igual en Modest y en HD.
//
// LO QUE NO CAMBIA. Los creditos se cobran al encolar y se devuelven si una cancion falla, como siempre; el
// relevo solo adelanta el momento en que suena la siguiente. La cola y "lo que suena" cambian cuando la
// siguiente EMPIEZA A OIRSE (el colchon de la salida lo retrasa), no cuando empieza a leerse. Si no hay
// siguiente preparada, o es un video, o viene en otro formato, se acaba como siempre: se cierra la salida
// y PlayNext abre la siguiente. El fundido de salida de siempre se conserva, salvo entre una pista y la que
// le sigue en el MISMO disco, que es donde un disco continuo tiene que seguir continuo.

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1 : Form
    {
        // LA CADENA DE UNA CANCION ya armada, sin la salida: lo que se lee y los controles en vivo de ESA cancion.
        private sealed class CadenaCancion
        {
            internal Track Cancion;
            internal PlayItem Item;              // el elemento de la cola con el que suena
            internal IAudioSource Lector;
            internal FuenteMedida Fuente;
            internal ISampleProvider Salida;     // lo que se lee (hasta el fundido; sin la salida)
            internal LevelTap Tap;
            internal SpectralTap Sbr;
            internal DeepBassProvider Bajo;
            internal GraveMono Mono;             // el grave en mono (GraveMono.cs)
            internal SalaLocal Sala;             // claridad, partes suaves, balance, graves y agudos del local (SalaLocal.cs)
            internal PresenciaSala Presencia;    // el compresor suave de la sala ruidosa (PresenciaSala.cs)
            internal QuitaSiseo Siseo;           // el quitasiseo, si esta encendido (Siseo.cs)
            internal ArreglaGrabacion Arreglo;   // el canal mudo y la continua (ArreglosGrabacion.cs)
            internal QuitaZumbido Zumbido;       // las muescas del zumbido de la corriente, si la cancion lo trae (T6, Zumbido.cs)
            internal DeclipProvider Declip;      // el des-recorte, si esta cancion lo lleva
            internal LimitadorPicoReal Limitador; // el limitador de pico real (lo que baja lo ensena Ctrl+F3: S5)
            internal FadeProvider Fundido;
            internal RemuestreoContinuo Conversion;   // el convertidor a la frecuencia de la salida, si la lleva (ConvierteSalida)
            internal LimitadorPicoReal LimiteSalida;  // y el limitador de detras
            internal int Retardo;                // cuadros de cebado: se descartan si entra por relevo
            internal string MedidaRuta, EqRuta;  // que se aprende oyendola (null = nada)
            internal float BajoFactor, BajoBase;
            internal bool IaMejorada;            // suena la version de Apollo (solo HD)
            internal double InicioMusica;
            internal long CuadroComienzo;
            internal int CebadoInicial;
        }
        private static void SellaInicioReloj(CadenaCancion c)
        { c.InicioMusica = c.Lector.CurrentTime.TotalSeconds; c.CuadroComienzo = 0; c.CebadoInicial = c.Retardo; }

        private Encadenado secuencia;            // lo que alimenta la salida abierta
        private CadenaCancion cadenaSonando;     // la de lo que la pantalla da por sonando
        private CadenaCancion cadenaPreparada;   // la siguiente: lista, o entrando y aun sin recoger
        private LevelTap tapMedido;              // de que cancion es la medida en vivo de los campos de medida
        // Se prepara cuando a la que suena le quedan 12 s: tiempo de sobra en una maquina lenta, y lo bastante
        // tarde para que la siguiente ya este medida (el calentador la mide mientras espera en la cola).
        private const double PreparaAntesSeg = 12.0;

        // LA SIGUIENTE CANCION PASA ANTES QUE LAS CARATULAS CERCA DEL FINAL (22 de octubre de 2026). El calentador de la
        // siguiente (PrecalientaSiguiente) espera 1,5 s a que la cancion que empieza tenga el disco, cede el paso mientras
        // haya caratulas en camino y en cada trozo vuelve a mirar. Es lo correcto con minutos por delante, pero si la
        // siguiente se pidio tarde (la cola estaba vacia y entra una moneda a 40 s del final) o hay muchas caratulas, la
        // cancion puede llegar sin estar leida ni abierta. Cuando a la que suena le quedan SiguientePrioridadSeg (45 s) o
        // menos, el calentador se salta esas esperas y abre la siguiente ya. Con mas tiempo, todo igual que antes. No cambia
        // el sonido: solo el orden en que se usa el disco. El reloj de la cancion apunta lo que queda cada 500 ms.
        internal const int SiguientePrioridadSeg = 45;
        private volatile int quedanMs;   // lo que le queda a lo que suena, en ms MAS UNO; 0 = no se sabe (nada suena o aun no hubo tic)
        internal static bool SiguienteEsUrgente(int quedanMilisegundos) { return quedanMilisegundos <= SiguientePrioridadSeg * 1000; }
        private bool SiguienteUrgente() { int q = quedanMs; return q > 0 && SiguienteEsUrgente(q - 1); }
        private void ApuntaQuedan(TimeSpan actual, TimeSpan total)
        {
            double q = (total - actual).TotalMilliseconds;
            quedanMs = (total.TotalSeconds > 0 && !double.IsNaN(q)) ? (int)Math.Max(0.0, Math.Min(q, 2000000000.0)) + 1 : 0;
        }

        // Lo llama el temporizador de la barra de progreso (dos veces por segundo).
        private void PreparaSiguienteSinPausa()
        {
            Encadenado s = secuencia;
            if (s == null || cadenaPreparada != null || cadenaSonando == null) return;
            if (nowPlaying == null || nowPlaying.IsVideo || videoOn || audioOpeningForMeasurement || output == null) return;
            if (s.Terminado || s.HayRelevoPendiente || queue.Count == 0) return;
            PlayItem sig = queue[0];
            if (sig == null || sig.IsVideo || sig.Song == null || sig.Song.Path == null) return;
            IAudioSource r = reader;
            if (r == null || lookahead == null) return;
            double quedan;
            try { quedan = (r.TotalTime - r.CurrentTime).TotalSeconds; } catch { return; }
            if (quedan > PreparaAntesSeg) return;
            // Si Windows cambio de aparato de sonido, no se encadena: la salida se cierra al acabar esta y la
            // siguiente se abre en el aparato nuevo (SalidaSonido.cs).
            if (!MismaSalida()) return;
            // Se eligio otra frecuencia para la tarjeta (A4) y esta salida va a la de antes: la siguiente abre la salida
            // de nuevo, ya a la elegida, en vez de seguir encadenada a la vieja.
            if (frecuenciaSalida > 0 && s.WaveFormat.SampleRate != frecuenciaSalida) return;
            // El lector de la siguiente lo abre por delante el calentador (MiraPorDelante). Si aun no esta, o
            // no conserva la disposicion conocida o excede trabajo de conversion, queda para PlayNext.
            string ruta = RutaQueSonaria(sig.Song.Path);
            if (ruta == null || !lookahead.Cumple(ruta, delegate(IAudioSource l) { return Encadenado.MismoFormato(l.WaveFormat, s.WaveFormat) || RemuestreoContinuo.SePuede(l.WaveFormat, s.WaveFormat); }))
                return;
            double[] tablaConversion = null;
            if (!lookahead.Cumple(ruta, delegate(IAudioSource l) {
                if (Encadenado.MismoFormato(l.WaveFormat, s.WaveFormat)) return true;
                tablaConversion = RemuestreoContinuo.CapturaTabla(l.WaveFormat, s.WaveFormat);
                return tablaConversion != null;
            }))
                return; // un solo calentador acotado; el temporizador vuelve cuando la tabla ya esta hecha
            bool iaMejorada = IaMejoradaPara(sig.Song.Path);
            IAudioSource lector = lookahead.Toma(ruta);
            if (lector == null) return;
            CadenaCancion c;
            try
            {
                lector.Volume = FixedVolume;
                c = ArmaCadena(sig.Song, lector, iaMejorada, 0);
                c.Item = sig;
                SellaInicioReloj(c); c.CebadoInicial = 0; // Encadenado descarta el cebado de los relevos
                if (!Encadenado.MismoFormato(c.Salida.WaveFormat, s.WaveFormat))
                {
                    // Se quita el cebado a la tasa del origen; despues se convierte y se vuelve a limitar.
                    // Otras disposiciones nunca se mezclan: se abre su salida al terminar, como siempre.
                    ConvierteSalida(c, s.WaveFormat, tablaConversion);
                }
            }
            catch (Exception ex)
            {
                LogError("Relevo sin pausa: no se pudo preparar " + System.IO.Path.GetFileName(sig.Song.Path) + " (sonara por el camino de siempre)", ex);
                try { lector.Dispose(); } catch { }
                return;
            }
            if (!s.Prepara(c.Salida, c.Retardo, c)) { try { lector.Dispose(); } catch { } return; }
            cadenaPreparada = c;
            // PISTA SIGUIENTE DEL MISMO DISCO: el fundido de salida de la que suena se quita, si aun no empezo,
            // para que un disco continuo siga continuo. Entre canciones sueltas queda el fundido de siempre.
            bool entera = cadenaSonando.Fundido == null;   // sin fundido ("fundido=0"), la que suena acaba entera
            if (SiguienteDelDisco(nowPlaying, sig) && cadenaSonando.Fundido != null) entera = cadenaSonando.Fundido.Quita();
            // Y la siguiente, sin su fundido de entrada: la onda sigue sin corte (Form1.Sonido.cs, FadeProvider).
            if (entera && SiguienteDelDisco(nowPlaying, sig) && c.Fundido != null) c.Fundido.SinEntrada();
            // Si acaba entera, la siguiente sigue con la memoria de sus filtros: el cambio no se oye (SigueA).
            if (entera && lector.WaveFormat.SampleRate == cadenaSonando.Lector.WaveFormat.SampleRate) SigueA(c, cadenaSonando);
        }

        // La siguiente ya suena. Lo mira el temporizador de la animacion (cada 42 ms): el relevo se recoge
        // cuando ya se oye (ha pasado el colchon de la salida). Si la salida para antes, lo recoge RecogeAlParar.
        private void RevisaRelevo(int ahora)
        {
            Encadenado s = secuencia;
            if (s == null) return;
            double pendientes, segundosOidos;
            Encadenado.Relevo r = PendientePorReloj(out pendientes, out segundosOidos)
                ? s.TomaRelevoPorCuadro(Math.Max(0L, (long)Math.Floor(segundosOidos * s.WaveFormat.SampleRate)))
                : s.TomaRelevo(ahora, RetrasoAudible());
            if (r == null) return;
            CadenaCancion b = r.Ficha as CadenaCancion;
            if (b != null) { b.CuadroComienzo = r.Cuadro; CompletaRelevo(b); }
        }

        // Lo mismo que hace PlayNext al pasar a la siguiente, menos cerrar y abrir la salida: la cancion que
        // entra sale de la cola, pasa a "sonando" y la pantalla se entera. Nada de creditos.
        private void CompletaRelevo(CadenaCancion b)
        {
            PasaRelevo(b);          // el sonido, la medida y la cola (pruebas/sinpausa.cs lo ejecuta tal cual)
            RefrescaTrasRelevo();   // la pantalla, igual que al empezar una cancion por PlayNext
        }

        private void PasaRelevo(CadenaCancion b)
        {
            ApuntaCortesDeSonido();   // la que acaba: si tuvo cortes, una linea en rocola_error.txt (caja negra, SalidaSonido.cs)
            CadenaCancion a = cadenaSonando;
            cadenaSonando = b;
            if (object.ReferenceEquals(cadenaPreparada, b)) cadenaPreparada = null;
            // 1) LA QUE ACABO. Su medida la guardo el hilo del sonido en el primer bloque de la nueva; si la nueva
            //    no llego a dar ni un bloque (archivo vacio) sigue en los campos de siempre. Y su lector se suelta:
            //    el hilo del sonido ya no lo lee.
            // Releva publica la ficha antes del primer Read de la nueva. Si ese Read tarda,
            // el relevo de UI puede coincidir con su primer bloque: consulta y cierre deben
            // ser atomicos respecto a PublicaBloqueDe. Lectores, cola y UI quedan fuera.
            lock (audioDatosLock)
            {
                MedidaCancion m = b.Tap != null ? b.Tap.Medida : null;
                if (m != null && m.CierreAnterior.Listo) CierraMedida(m.CierreAnterior, eqMeasCut, eqMeasCutLo);
                else { FinalizeLoudMeasure(); FinalizeEqMeasure(); InstalaMedida(b); }
            }
            IAudioSource viejo = reader;
            reader = b.Lector;
            if (viejo != null && !object.ReferenceEquals(viejo, b.Lector)) { try { viejo.Dispose(); } catch { } }
            if (a != null && a.Lector != null && !object.ReferenceEquals(a.Lector, viejo) && !object.ReferenceEquals(a.Lector, b.Lector))
            { try { a.Lector.Dispose(); } catch { } }
            // El corte de agudos lo medira el DSEE de la nueva: no se le arrastra el de la anterior.
            eqMeasCut = 0f; eqMeasCutLo = 0f;
            curTap = b.Tap; curSbr = b.Sbr; curBass = b.Bajo;
            bassSongFactor = b.BajoFactor; bassAutoBase = b.BajoBase;
            AlEntrarPorRelevo(b);
            playFailStreak = 0;   // la anterior termino bien
            // 2) LA COLA, como en PlayNext. La que entra es la primera de la cola: la cola solo cambia su primera
            //    por PlayNext, que antes suelta todo esto. Si aun asi no lo fuera, se retira de donde este.
            PlayItem item = b.Item;
            if (queue.Count > 0 && object.ReferenceEquals(queue[0], item)) queue.RemoveAt(0);
            else if (item != null) queue.Remove(item);
            sonoAntes = nowPlaying;   // lo que sono justo antes (para el nivel por disco, NivelDisco.cs)
            nowPlaying = item;
            quedanMs = 0;   // la que entra aun no ha dicho cuanto le queda (el reloj lo apunta en su primer tic)
            RefrescaCola();
            SaveState();   // estado vivo: cambio lo que suena / la cola (para reanudar tras un corte de luz)
        }

        private void RefrescaTrasRelevo()
        {
            ApuntaGeneroTrasRelevo();
            if (nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null) ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
            LayoutControls(); this.Invalidate();
            UpdateNowPlayingUI();
            PrecalientaSiguiente();   // la que viene detras de esta
        }

        // Lo que PlayNext apunta al empezar una cancion: su genero, si el archivo lo trae (color de fondo, discos TOP).
        private void ApuntaGeneroTrasRelevo()
        {
            if (nowPlaying == null || nowPlaying.IsVideo || nowPlaying.Song == null) return;
            nowPlaying.Song.EnsureTags();         // lee ID3 ahora (genero -> color de fondo)
            if (nowPlaying.Song.Path != null && !string.IsNullOrEmpty(nowPlaying.Song.Genre))
            { songGenre[nowPlaying.Song.Path] = GenreLabel(nowPlaying.Song.Genre); statsDirty = true; topDirty = true; }
        }

        // AL PARAR LA SALIDA con la secuencia en marcha (Output_PlaybackStopped, antes de atender el aviso).
        // Si acabo bien, o fallo al LEER (la secuencia lo atrapo y lo apunto), la que ya habia entrado es la que
        // acaba de sonar: pasa a "sonando" sin tocar la pantalla (detras viene PlayNext, que la rehace). El fallo
        // de lectura se devuelve como aviso, UNA vez, y es de la cancion que sonaba al fallar. Si lo que fallo es
        // la SALIDA (un DAC desenchufado, el controlador), la que entraba aun no se oia: no se da por sonada,
        // sigue primera en la cola y PlayNext la empieza desde el principio, como antes del relevo.
        private StoppedEventArgs RecogeAlParar(StoppedEventArgs e)
        {
            Encadenado s = secuencia;
            if (s == null) return e;
            Exception deLaSalida = e != null ? e.Exception : null;
            object quien; Exception fallo;
            bool falloAlLeer = s.TomaFallo(out quien, out fallo);
            double pendientes, segundosOidos;
            Encadenado.Relevo oido = deLaSalida != null && !falloAlLeer && PendientePorReloj(out pendientes, out segundosOidos)
                ? s.TomaRelevoPorCuadro(Math.Max(0L, (long)Math.Floor(segundosOidos * s.WaveFormat.SampleRate))) : null;
            if (deLaSalida == null || falloAlLeer || oido != null)
            {
                Encadenado.Relevo r = oido ?? s.TomaRelevo(Environment.TickCount, int.MinValue);
                CadenaCancion b = r != null ? r.Ficha as CadenaCancion : null;
                if (b != null) { b.CuadroComienzo = r.Cuadro; PasaRelevo(b); ApuntaGeneroTrasRelevo(); }
            }
            return (falloAlLeer && deLaSalida == null) ? new StoppedEventArgs(fallo) : e;
        }

        // Al parar la salida (saltar, cerrar, fallo): la siguiente preparada se suelta, y si ya habia entrado
        // sin que la pantalla se enterara, la medida que el hilo del sonido guardo de la anterior se cierra.
        // Bajo audioDatosLock y con la publicacion ya invalidada. El lector se devuelve al cierre de la bomba:
        // el hilo puede seguir dentro del ultimo Read y debe conservarlo hasta salir.
        private IAudioSource SueltaSinPausa()
        {
            CadenaCancion p = cadenaPreparada;
            cadenaPreparada = null;
            if (p != null)
            {
                MedidaCancion m = p.Tap != null ? p.Tap.Medida : null;
                if (m != null && m.CierreAnterior.Listo) CierraMedida(m.CierreAnterior, eqMeasCut, eqMeasCutLo);
            }
            secuencia = null; cadenaSonando = null; tapMedido = null;
            return p != null ? p.Lector : null;
        }

        // LA MEDIDA EN VIVO EN EL RELEVO. La llama el LevelTap de cada cancion antes de medir su bloque, en el
        // hilo del sonido. Con una sola cancion por salida no hace nada (la medida ya es suya). En el primer bloque
        // de la que entra por relevo, guarda lo medido de la anterior (en memoria ya reservada) y estrena lo suyo.
        private void MedidaDe(LevelTap tap)
        {
            if (object.ReferenceEquals(tap, tapMedido)) return;
            MedidaCancion nueva = tap.Medida;
            if (nueva == null) return;   // un LevelTap armado aparte (las pruebas) mide como siempre
            MedidaCancion.Cierre c = nueva.CierreAnterior;
            c.Ruta = curMeasurePath; c.SumSq = curSumSq; c.Muestras = curSamples; c.Tasa = kwRate; c.Canales = kwCanales;
            c.Fuente = curFuenteMedida;
            c.EqRuta = eqMeasPath; c.EqSumas = eqMeasSum; c.EqN = eqMeasN; c.EqTasa = eqMeasRate;
            curMeasurePath = nueva.Ruta; curSumSq = 0; curSamples = 0; kwRate = 0;   // el K-weighting empieza de cero
            curFuenteMedida = nueva.Fuente as FuenteMedida;
            eqMeasPath = nueva.EqRuta; eqMeasSum = nueva.EqSumas; eqMeasN = 0; eqMeasRate = 0; eqPendN = 0;
            tapMedido = tap;
            c.Listo = true;   // lo ultimo: la pantalla solo lo cierra con todo copiado
        }

        // La nueva no dio ni un bloque: su medida se pone aqui (la salida ya no lee).
        private void InstalaMedida(CadenaCancion c)
        {
            curFuenteMedida = c.Fuente;
            curMeasurePath = c.MedidaRuta; curSumSq = 0; curSamples = 0; kwRate = 0;
            eqMeasPath = c.EqRuta; eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0; eqPendN = 0;
            tapMedido = c.Tap;
        }

        // Cierra lo medido de una cancion que acabo por relevo: lo mismo que FinalizeLoudMeasure y
        // FinalizeEqMeasure con los campos, pero con lo que se guardo. Una sola vez.
        private void CierraMedida(MedidaCancion.Cierre c, float corte, float corteBajo)
        {
            if (c == null || !c.Listo) return;
            c.Listo = false;
            CierraSonoridadDeFuente(c.Ruta, c.SumSq, c.Muestras, c.Tasa, c.Canales, c.Fuente as FuenteMedida);
            CierraHuellaDeFuente(c.EqRuta, c.EqSumas, c.EqN, c.EqTasa, corte, corteBajo, c.Fuente as FuenteMedida);
        }

        // DISCO CONTINUO: la que entra ('nueva') sigue con la memoria de los filtros de la que acaba ('vieja'): el
        // grave en mono y el local. Solo cuando la vieja acaba entera, sin fundido (si no,
        // su memoria es la de una musica que ya no se oye). Sin esto, esos filtros empezarian de cero justo en el
        // cambio, y el del grave en mono deja un golpe en el grave que se oye. Lo toman en su primera lectura.
        private static void SigueA(CadenaCancion nueva, CadenaCancion vieja)
        {
            if (nueva == null || vieja == null) return;
            if (nueva.Tap != null) nueva.Tap.Sigue(vieja.Tap);
            if (nueva.Mono != null) nueva.Mono.Sigue(vieja.Mono);
            if (nueva.Sala != null) nueva.Sala.Sigue(vieja.Sala);
            if (nueva.Presencia != null) nueva.Presencia.Sigue(vieja.Presencia);
            if (nueva.Bajo != null) nueva.Bajo.Sigue(vieja.Bajo);
            if (nueva.Limitador != null) nueva.Limitador.Sigue(vieja.Limitador);
            if (nueva.Siseo != null) nueva.Siseo.Sigue(vieja.Siseo);
            if (nueva.Arreglo != null) nueva.Arreglo.Sigue(vieja.Arreglo);
            if (nueva.Zumbido != null && vieja.Zumbido != null) nueva.Zumbido.Sigue(vieja.Zumbido);
            // Convertidas a la frecuencia de la salida: el convertidor y su limitador, tambien (SinPausa.cs).
            if (nueva.Conversion != null && vieja.Conversion != null) nueva.Conversion.Sigue(vieja.Conversion);
            if (nueva.LimiteSalida != null && vieja.LimiteSalida != null) nueva.LimiteSalida.Sigue(vieja.LimiteSalida);
        }
        // La cadena, a otra frecuencia (la de la salida): el convertidor quita el cebado a la tasa del origen y un limitador cuida los
        // picos que la conversion pueda dejar. El cebado que queda es el de ese limitador.
        private static void ConvierteSalida(CadenaCancion c, NAudio.Wave.WaveFormat a, double[] tabla)
        {
            RemuestreoContinuo r = new RemuestreoContinuo(c.Salida, a, c.Retardo, tabla);
            LimitadorPicoReal lim = new LimitadorPicoReal(r, TechoPicoReal, 3.0);
            c.Salida = lim; c.Retardo = lim.DelayFrames; c.Conversion = r; c.LimiteSalida = lim;
        }

        // ¿'b' es la pista que sigue a 'a' en el MISMO disco? (un disco TOP junta canciones de discos distintos)
        private static bool SiguienteDelDisco(PlayItem a, PlayItem b)
        {
            if (a == null || b == null || a.IsVideo || b.IsVideo || a.Song == null || b.Song == null) return false;
            Album d = a.Disc;
            if (d == null || !object.ReferenceEquals(d, b.Disc) || d.IsTop || d.Tracks == null) return false;
            int i = d.Tracks.IndexOf(a.Song);
            return i >= 0 && i + 1 < d.Tracks.Count && object.ReferenceEquals(d.Tracks[i + 1], b.Song);
        }

        // Los ajustes en vivo de Ctrl+F3 tambien le llegan a la siguiente ya preparada: si aun no
        // suena, empieza con lo nuevo (sin rampa); si ya entro, lo toma con la rampa de siempre.
        private void AplicaAPreparada()
        {
            CadenaCancion p = cadenaPreparada;
            if (p == null || p.Cancion == null || p.Tap == null) return;
            p.Tap.Gain = GananciaDe(p.Cancion);
            p.Tap.Excite = ExcitePara(p);
            p.Tap.Master = MasterEfectivo();
            p.Tap.EqDb = EqCorrFor(p.Cancion.Path);
            if (p.Bajo != null) p.Bajo.Amount = BassAmountPara(p.BajoFactor, p.BajoBase);
            if (p.Sbr != null) p.Sbr.Active = DseeDe(p);
            AplicaSinMejoras(p);   // "Escuchar sin mejoras" (S4, LoQueSuena.cs): la siguiente tambien, si esta puesto
        }
    }
}
