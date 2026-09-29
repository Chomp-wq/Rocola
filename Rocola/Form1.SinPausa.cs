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
            internal CompensaNoche Noche;        // graves y agudos que devuelve el modo noche (ModoNoche.cs)
            internal GraveMono Mono;             // el grave en mono (GraveMono.cs)
            internal FadeProvider Fundido;
            internal int Retardo;                // cuadros de cebado: se descartan si entra por relevo
            internal string MedidaRuta, EqRuta;  // que se aprende oyendola (null = nada)
            internal float BajoFactor, BajoBase;
            internal bool IaMejorada;            // suena la version de Apollo (solo HD)
        }

        private Encadenado secuencia;            // lo que alimenta la salida abierta
        private CadenaCancion cadenaSonando;     // la de lo que la pantalla da por sonando
        private CadenaCancion cadenaPreparada;   // la siguiente: lista, o entrando y aun sin recoger
        private LevelTap tapMedido;              // de que cancion es la medida en vivo de los campos de medida
        // Se prepara cuando a la que suena le quedan 12 s: tiempo de sobra en una maquina lenta, y lo bastante
        // tarde para que la siguiente ya este medida (el calentador la mide mientras espera en la cola).
        private const double PreparaAntesSeg = 12.0;

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
            // El lector de la siguiente lo abre por delante el calentador (MiraPorDelante). Si aun no esta, o
            // viene en otra frecuencia o con otros canales, no se toca: la abrira el camino de siempre.
            string ruta = RutaQueSonaria(sig.Song.Path);
            if (ruta == null || !lookahead.Cumple(ruta, delegate(IAudioSource l) { return Encadenado.MismoFormato(l.WaveFormat, s.WaveFormat); }))
                return;
            bool iaMejorada = IaMejoradaPara(sig.Song.Path);
            IAudioSource lector = lookahead.Toma(ruta);
            if (lector == null) return;
            CadenaCancion c;
            try
            {
                lector.Volume = FixedVolume;
                c = ArmaCadena(sig.Song, lector, iaMejorada, 0);
                c.Item = sig;
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
            // Si acaba entera, la siguiente sigue con la memoria de sus filtros: el cambio no se oye (SigueA).
            if (entera) SigueA(c, cadenaSonando);
        }

        // La siguiente ya suena. Lo mira el temporizador de la animacion (cada 42 ms): el relevo se recoge
        // cuando ya se oye (ha pasado el colchon de la salida). Si la salida para antes, lo recoge RecogeAlParar.
        private void RevisaRelevo(int ahora)
        {
            Encadenado s = secuencia;
            if (s == null) return;
            Encadenado.Relevo r = s.TomaRelevo(ahora, RetrasoAudible());
            if (r == null) return;
            CadenaCancion b = r.Ficha as CadenaCancion;
            if (b != null) CompletaRelevo(b);
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
            CadenaCancion a = cadenaSonando;
            cadenaSonando = b;
            if (object.ReferenceEquals(cadenaPreparada, b)) cadenaPreparada = null;
            // 1) LA QUE ACABO. Su medida la guardo el hilo del sonido en el primer bloque de la nueva; si la nueva
            //    no llego a dar ni un bloque (archivo vacio) sigue en los campos de siempre. Y su lector se suelta:
            //    el hilo del sonido ya no lo lee.
            // Releva publica la ficha antes del primer Read de la nueva. Si ese Read tarda,
            // el relevo de UI puede coincidir con su primer bloque: consulta y cierre deben
            // ser atomicos respecto a PublicaBloqueDe. Lectores, cola y UI quedan fuera.
            lock (levelLock)
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
            curTap = b.Tap; curSbr = b.Sbr; curBass = b.Bajo; curNoche = b.Noche;
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
            if (deLaSalida == null || falloAlLeer)
            {
                Encadenado.Relevo r = s.TomaRelevo(Environment.TickCount, int.MinValue);
                CadenaCancion b = r != null ? r.Ficha as CadenaCancion : null;
                if (b != null) { PasaRelevo(b); ApuntaGeneroTrasRelevo(); }
            }
            return (falloAlLeer && deLaSalida == null) ? new StoppedEventArgs(fallo) : e;
        }

        // Al parar la salida (saltar, cerrar, fallo): la siguiente preparada se suelta, y si ya habia entrado
        // sin que la pantalla se enterara, la medida que el hilo del sonido guardo de la anterior se cierra.
        // Bajo levelLock y con la publicacion ya invalidada. El lector se devuelve al cierre de la bomba:
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

        // DISCO CONTINUO: la que entra ('nueva') sigue con la memoria de los filtros de la que acaba ('vieja'): la
        // compensacion del modo noche y el grave en mono. Solo cuando la vieja acaba entera, sin fundido (si no,
        // su memoria es la de una musica que ya no se oye). Sin esto, esos filtros empezarian de cero justo en el
        // cambio, y el del grave en mono deja un golpe en el grave que se oye. Lo toman en su primera lectura.
        private static void SigueA(CadenaCancion nueva, CadenaCancion vieja)
        {
            if (nueva == null || vieja == null) return;
            if (nueva.Noche != null) nueva.Noche.Sigue(vieja.Noche);
            if (nueva.Mono != null) nueva.Mono.Sigue(vieja.Mono);
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

        // Los ajustes en vivo de Ctrl+F3 y el modo noche tambien le llegan a la siguiente ya preparada: si aun no
        // suena, empieza con lo nuevo (sin rampa); si ya entro, lo toma con la rampa de siempre.
        private void AplicaAPreparada()
        {
            CadenaCancion p = cadenaPreparada;
            if (p == null || p.Cancion == null || p.Tap == null) return;
            p.Tap.Gain = GananciaDe(p.Cancion);
            p.Tap.Excite = ExcitePara(p);
            p.Tap.Master = MasterEfectivo() * FactorNoche();
            p.Tap.EqDb = EqCorrFor(p.Cancion.Path);
            if (p.Bajo != null) p.Bajo.Amount = BassAmountPara(p.BajoFactor, p.BajoBase);
            if (p.Noche != null) p.Noche.Db = NocheCompensaDb();
            if (p.Sbr != null) p.Sbr.Active = DseeDe(p);
        }
    }
}
