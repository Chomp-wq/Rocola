// ARMAR LA CADENA DE UNA CANCION (Rocola/Form1.Sonido.cs: ArmaCadena) y lo que lleva cada cancion (Rocola/Form1.SinPausa.cs:
// CadenaCancion), copiados para el motor de Rocola OS. El orden y las reglas son los de XP.
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
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
            internal SuavizaEses Eses;           // suavizar las eses (T9, Eses.cs)
            internal RetumbeDinamico Retumbe;    // el retumbe dinamico (RetumbeDinamico.cs)
            internal AtaqueClaro Ataque;         // el ataque mas claro (AtaqueClaro.cs)
            internal AsperezaDinamica Aspereza;  // los agudos asperos dinamicos (AsperezaDinamica.cs)
            internal BomboSinHundir Hundir;      // que el bombo no hunda la voz (BomboSinHundir.cs)
            internal MideMargen Margen;          // en el billar: lo que le llega al limitador, para el volumen parejo (VolumenParejo.cs)
            internal QuitaSiseo Siseo;           // el quitasiseo, si esta encendido (Siseo.cs)
            internal ArreglaGrabacion Arreglo;   // el canal mudo y la continua (ArreglosGrabacion.cs)
            internal QuitaZumbido Zumbido;
            internal QuitaPreEco PreEco;         // el pre-eco de los MP3, si esta cancion lo trae y el interruptor esta puesto (R2, PreEco.cs)       // las muescas del zumbido de la corriente, si la cancion lo trae (T6, Zumbido.cs)
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
            // Lo que el modo automatico del local decidio para ESTA cadena (LocalAuto.cs, L1): se fija la primera vez y no cambia a media
            // cancion; en un disco continuo la siguiente lo hereda (HeredaLocal), asi no hay escalon en el cambio de pista.
            internal bool AutoFijo, AutoPresencia, AutoSuaves;
            internal readonly ActividadEtapas Actividad = new ActividadEtapas();   // lo que hizo cada etapa (D1, BitacoraBillar.cs)
        }
        // LA CADENA DE UNA CANCION, del lector al fundido de salida (sin la salida). La usan el arranque de
        // siempre (StartOpenedTrack) y el relevo sin pausa (Form1.SinPausa.cs), asi que las dos suenan igual.
        // Solo ARMA: no toca lo que suena ahora (ni los campos de medida ni los controles en vivo).
        // 'saltadoSeg': la cancion se retoma desde ahi (volvio el aparato de sonido; SalidaSonido.cs); 0 = entera.
        private CadenaCancion ArmaCadena(Track t, IAudioSource source, bool iaMejorada, double saltadoSeg)
        {
            // La comparacion a ciegas se acaba antes de armar otra cancion: si no, se armaria con lo del tramo que suene (CompararCiegas.cs).
            if (ciegoActivo) TerminaCiego("Se acaba la cancion: comparacion cortada. ");
            CadenaCancion c = new CadenaCancion();
            c.Cancion = t; c.Lector = source; c.IaMejorada = iaMejorada;
            OlvidaSiEsOtroArchivo(t.Path, source);   // en esa ruta hay hoy otro archivo: lo aprendido del de antes no vale
            c.Fuente = FuenteDeLector(t.Path, source);
            if (c.Fuente != null && !c.Fuente.Igual(CapturaFuente(t.Path))) OlvidaMedidas(t.Path);
            // NIVELADO: si esta cancion ya se oyo antes, se aplica la ganancia que la lleva al nivel comun; si es
            // nueva, va a su volumen original y se APRENDE su sonoridad en esta pasada (para nivelarla la proxima vez).
            // De una cancion retomada a medias no se aprende nada: seria la medida de un trozo.
            bool entera = saltadoSeg <= 0 && !iaMejorada;
            c.MedidaRuta = (entera && normalizeOn && !durCache.ContainsKey(t.Path)) ? t.Path : null;   // ya pre-analizada = medida COMPLETA y fiable: no se vuelve a medir (y se ahorra el K-weighting)
            c.EqRuta = (entera && EqMideHuella && !HasStereoEq(t.Path)) ? t.Path : null;   // actualiza huellas antiguas sin borrar la biblioteca
            // NIVEL 2 (reconstructor espectral): solo se INSERTA en maquinas que cumplen requisitos; con
            // Active=false es un pase directo (costo ~cero). Ctrl+F3 lo alterna EN VIVO para comparar A/B.
            FuenteOriginal original = new FuenteOriginal(source, this);
            NAudio.Wave.ISampleProvider chain = original;
            int audioTailDelay = 0;   // estos retardos se vacian: el fundido debe abarcar tambien su cola
            // LOS ARREGLOS DE LA GRABACION (S1 y S2, ArreglosGrabacion.cs): un canal mudo por los dos y la continua fuera. Lo
            // primero, para que nada de lo de detras trabaje con el defecto. Sin retardo; sin el defecto, sale bit a bit.
            c.Arreglo = new ArreglaGrabacion(chain, CanalMudoEf, QuitaContinuaEf); chain = c.Arreglo;
            // EL ZUMBIDO DE LA CORRIENTE (T6, Zumbido.cs): solo en las canciones en que el repaso lo midio. Va siempre que lo traen, con el
            // interruptor decidiendo si actua (asi se enciende y se apaga en vivo); apagado, pasa tal cual. Sin retardo.
            int zumbidoHz = ZumbidoDe(t.Path);
            if (zumbidoHz > 0) { c.Zumbido = new QuitaZumbido(chain, zumbidoHz, QuitaZumbidoEf); chain = c.Zumbido; }
            // EL PRE-ECO (R2, PreEco.cs): con su interruptor y solo en las canciones en que el repaso lo midio. Retrasa 64 ms (a la cola del
            // fundido). Sin el interruptor o sin pre-eco medido ni se pone: sale bit a bit como antes.
            if (quitaPreEco && PreEcoDe(t.Path))
            {
                QuitaPreEco pe = new QuitaPreEco(chain); chain = pe; audioTailDelay += pe.DelayFrames; c.PreEco = pe;
            }
            // DES-RECORTE: repara picos aplastados de origen (despues de medir original, antes de DSEE). Ya sabemos, cancion
            // por cancion, si de verdad viene recortada; en las que NO -que son la mayoria- ni se pone en la
            // cadena: se ahorra su trabajo y su retardo de ~1.5 ms. Si aun no esta medida, se pone por si acaso.
            if (declipOn && DeclipVale(t.Path))
            {
                DeclipProvider dc = new DeclipProvider(chain); chain = dc; audioTailDelay += dc.DelayFrames; c.Declip = dc;
            }
            // QUITAR EL SISEO (I2, Siseo.cs): con su interruptor, antes del DSEE y del realce (que si no le darian brillo al
            // siseo). Retrasa 11,6 ms, que se suman a la cola que el fundido tiene que abarcar.
            if (quitaSiseo && !localAuto)   // en el modo billar no (CadenaBillar.cs)
            {
                QuitaSiseo qs = new QuitaSiseo(chain); chain = qs; audioTailDelay += qs.DelayFrames; c.Siseo = qs;
            }
            if (sbrAllowed)
            {
                c.Sbr = new SpectralTap(chain);
                c.Sbr.ConectaOriginal(original, audioTailDelay);
                c.Sbr.Red = ModeloDseePara(t);   // I3: el modelo de su genero, si se pidio y lo hay (Form1.Ia.cs)
                c.Sbr.Active = DseeEfectivo; chain = c.Sbr;
            }
            LevelTap tap = new LevelTap(chain, this);
            tap.Cebado = audioTailDelay;   // lo que llega de cebado (des-recorte, siseo): en un disco continuo no entra en el realce heredado (Realce.cs)
            original.Tap = tap; tap.MideOriginal = true;
            tap.Gain = GananciaDe(t);   // la del disco si suena en racha con otra de su disco (NivelDisco.cs); si no, la suya
            tap.Excite = ExciteAmountFor(t.Path);   // realce de agudos: mas a las dañadas, menos a las que ya traen su aire
            tap.Master = MasterEfectivo();  // SUBIR TODO (lo mismo a todas, sin pasar del techo de la biblioteca: SubirTodo.cs)
            tap.EqDb = EqCorrFor(t.Path);      // EQ automatico: correccion aprendida (null la 1a vez que suena)
            audioTailDelay += tap.DelayFrames;   // realce 2x: retiene 31 cuadros y los entrega al final (Realce.cs)
            c.Tap = tap; tap.Medida = new MedidaCancion(c.MedidaRuta, c.EqRuta);   // lo que aprende oyendola (ver MedidaDe)
            tap.Medida.Fuente = c.Fuente;
            // GRAVE EN MONO: por debajo del corte, el mismo grave por todas las bocinas. Y, con sus interruptores de Ctrl+F3,
            // para las bocinas del billar: sin el subgrave que ninguna reproduce y con el estereo mas estrecho (GraveMono.cs).
            AjusteLocal loc = LocalDe(c);   // los mandos tal cual, o lo que decide el modo automatico del local para esta cancion, fijado en ella (LocalAuto.cs)
            // Con «Escuchar sin mejoras» puesto la cancion NACE sin lo del local (sin rampa de entrada que lo desvanezca); el subgrave y el estereo se quedan.
            if (sinMejoras) { loc.Claridad = false; loc.Suaves = false; loc.Presencia = false; loc.Graves = 0; loc.Agudos = 0; loc.Retumbe = false; loc.Ataque = false; loc.Aspereza = false; }   // el bombo se queda: es de la familia del limitador
            c.Mono = new GraveMono(tap, graveMonoHz, anchoEstereo, SubgraveEf, loc.Estrecho, loc.Subgrave);   // en el billar, a 40 Hz (CadenaBillar.cs)
            c.Mono.SoloSiHaceFalta(loc.EstrechoSoloSiHaceFalta);   // y solo si hace falta (S3)
            // Lo que llega de cebado (los retardos de hasta aqui): en un disco continuo pasa sin tocar los filtros heredados (GraveMono.cs).
            c.Mono.Cebado = audioTailDelay;
            // EL LOCAL (SalaLocal.cs): claridad, partes suaves y balance, con sus mandos de Ctrl+F3 (apagados de fabrica:
            // apagados no tocan ni una muestra). Delante del bajo psicoacustico y del limitador, que cuida los picos.
            float nivelLocal = NivelLocalDe(t, tap.Gain);   // en el billar, el nivel de ESTA cancion; si no, normTarget (CadenaBillar.cs)
            c.Sala = new SalaLocal(c.Mono, tap, nivelLocal, loc.Claridad, loc.Suaves, balanceDb, loc.Graves, loc.Agudos);
            c.Sala.Cebado = audioTailDelay;
            // RETUMBE DINAMICO (RetumbeDinamico.cs, 27-10-2026): detras del EQ automatico y la claridad (que ya cortan ese rango: su corte se descuenta
            // del presupuesto) y delante del compresor. Apagado (de fabrica) no toca ni una muestra.
            c.Retumbe = new RetumbeDinamico(c.Sala, loc.Retumbe, loc.Claridad, RetumbeDinamico.CorteEstaticoDb(tap.EqDb, loc.Claridad, c.Sala.WaveFormat.SampleRate));
            c.Retumbe.Cebado = audioTailDelay;
            // PRESENCIA EN SALA RUIDOSA (PresenciaSala.cs): un compresor suave detras del local; apagado (de fabrica) no toca ni una muestra.
            c.Presencia = new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto);
            if (!presenciaSinGrave) c.Presencia.SinGrave = false;   // solo durante "Presencia: nueva o de antes" (CompararCiegas.cs)
            c.Presencia.Cebado = audioTailDelay;
            // ATAQUE MAS CLARO (AtaqueClaro.cs, 27-10-2026): detras del compresor y delante de las eses. Apagado (de fabrica) no toca ni una muestra.
            c.Ataque = new AtaqueClaro(c.Presencia, loc.Ataque);
            c.Ataque.Cebado = audioTailDelay;
            // SUAVIZAR LAS ESES (T9, Eses.cs): detras de lo que sube los agudos, delante del bajo y del limitador. Apagada (de fabrica) ni se
            // calcula; encendida, fuera de las "s" sale bit a bit.
            // AGUDOS ASPEROS DINAMICOS (AsperezaDinamica.cs, 27-10-2026): detras del ataque (que puede levantar un platillo) y delante de las eses
            // (que van mas arriba). Apagado (de fabrica) no toca ni una muestra.
            c.Aspereza = new AsperezaDinamica(c.Ataque, loc.Aspereza);
            c.Aspereza.Cebado = audioTailDelay;
            c.Eses = new SuavizaEses(c.Aspereza, loc.Eses, localAuto);   // en el billar, con la referencia por encima de 1 kHz (CadenaBillar.cs)
            c.Eses.Cebado = audioTailDelay;
            // BAJO PSICOACUSTICO: despues de nivelar/ecualizar y ANTES del limitador, que es quien controla
            // los picos que anaden los armonicos.
            c.BajoFactor = BassFactorFor(t.Path);   // cuanto le toca a ESTA cancion (1 = la perilla tal cual)
            c.BajoBase = BajoAuto(t.Path);          // y de cuanto se parte, si el bajo va en automatico
            c.Bajo = new DeepBassProvider(c.Eses, BassAmountPara(c.BajoFactor, c.BajoBase));
            c.Bajo.Cebado = audioTailDelay;
            // QUE EL BOMBO NO HUNDA LA VOZ (BomboSinHundir.cs, 27-10-2026): el bajo con su propio margen, pegado al limitador. Apagado (de fabrica) no toca
            // ni una muestra; encendido, mientras nada pase del techo, tampoco.
            c.Hundir = new BomboSinHundir(c.Bajo, loc.Bombo);
            c.Hundir.Cebado = audioTailDelay;
            // LIMITADOR DE PICO REAL: techo con margen para el error de estimacion entre
            // muestras (ver PicoReal.cs). Detras solo quedan el fundido (que solo baja) y el paso a 16 bits
            // con su dither de un bit.
            // EN EL BILLAR, lo que le llega al limitador se mide para el volumen parejo (VolumenParejo.cs). Pasa tal cual.
            NAudio.Wave.ISampleProvider alLimitador = c.Hundir;
            if (localAuto) { c.Margen = new MideMargen(c.Hundir, TechoPicoReal, saltadoSeg < 0.5, ParejoAplicado(t), SegundosDe(t.Path)); alLimitador = c.Margen; }
            LimitadorPicoReal lim = new LimitadorPicoReal(alLimitador, TechoPicoReal, 3.0, localAuto);   // en el billar, respira menos (PicoReal.cs)
            lim.Cebado = audioTailDelay;   // disco continuo: el cebado de delante no entra en el limitador heredado (PicoReal.cs)
            audioTailDelay += lim.DelayFrames; c.Limitador = lim;
            NAudio.Wave.ISampleProvider outChain = lim;
            if (crossfadeSec > 0.01f)
            {
                // CUANTO DURA. Se pregunta primero a lo que el pre-analisis dejo apuntado, que es de
                // donde lo saca tambien el turno de la cola. Preguntarselo AL ARCHIVO obliga a
                // recorrerlo entero cuando no trae la cuenta escrita, que es lo normal: 3.87 ms y
                // nueve megas de disco en una cancion de cuatro minutos, y aqui eso caeria en el hilo
                // de la pantalla, en el instante en que el cliente acaba de soltar el boton. El camino
                // largo se deja solo para las canciones de las que aun no se sabe nada.
                long tf = 0;
                try
                {
                    int hz = source.WaveFormat.SampleRate;
                    float yaSe = SegundosDe(t.Path);
                    FuenteSinSilencios sinSilencios = source as FuenteSinSilencios;
                    // LO APUNTADO TIENE QUE SER DE ESTE ARCHIVO. Se apunta por ruta, y en esa ruta puede haber hoy otro
                    // archivo (la cancion completa en lugar de una descarga cortada, otra version con el mismo nombre). Con
                    // la duracion del de antes, el fundido bajaba a cero donde acababa aquel y el resto de la cancion pagada
                    // sonaba en SILENCIO. El lector ya esta abierto y sabe lo que dura sin leer nada (el MP3 se indexo al
                    // abrirlo): si no cuadra con lo apuntado (un segundo de margen), manda el archivo.
                    double delArchivo = 0;
                    try { delArchivo = ((sinSilencios != null) ? sinSilencios.Interior : source).TotalTime.TotalSeconds; } catch { }   // si no lo sabe, como antes
                    if (yaSe > 0.01f && delArchivo > 0 && Math.Abs(delArchivo - yaSe) > 1.0) yaSe = 0f;
                    tf = (yaSe > 0.01f) ? (long)(yaSe * hz) : (long)(delArchivo * hz);
                    FuenteSinRelleno sinRelleno = ((sinSilencios != null) ? sinSilencios.Interior : source) as FuenteSinRelleno;
                    if (sinRelleno != null && saltadoSeg <= 0) tf = sinRelleno.CuadrosMusica;   // sin el relleno acaba antes: el fundido tambien
                    // Sin los silencios largos: lo que queda por sonar desde donde va (el principio mudo ya se salto al
                    // abrirla, y una retomada ya movio la cuenta), asi el fundido cae en el margen mudo del final.
                    if (sinSilencios != null) tf = sinSilencios.Salen(tf);
                    else tf -= (long)(saltadoSeg * hz);   // retomada: el fundido cuenta desde donde se retomo
                }
                catch { }
                if (tf > 0) tf += audioTailDelay;
                c.Fundido = new FadeProvider(outChain, tf, crossfadeSec, audioTailDelay,
                                             saltadoSeg > 0 ? FadeProvider.EntradaRetomadaSeg : FadeProvider.EntradaSeg);   // fundido suave de entrada/salida
                outChain = c.Fundido;
            }
            else
            {
                // SIN FUNDIDO DE SALIDA ("fundido=0"): la entrada de 5 ms (o de 0,3 s si se retoma) no depende de el. Antes, sin fundido
                // tampoco habia entrada y una cancion que empieza en su pico arrancaba de golpe. Sin total, no hay fundido de salida.
                c.Fundido = new FadeProvider(outChain, 0, 0, audioTailDelay,
                                             saltadoSeg > 0 ? FadeProvider.EntradaRetomadaSeg : FadeProvider.EntradaSeg);
                outChain = c.Fundido;
            }
            c.Retardo = audioTailDelay; c.Salida = outChain;
            AplicaSinMejoras(c);   // «Escuchar sin mejoras» (LoQueSuena.cs): una cancion armada mientras dura (el relevo, o la siguiente) nace ya sin ellas; si no, sale como siempre
            return c;
        }
    }
}
