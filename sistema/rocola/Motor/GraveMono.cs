// EL GRAVE EN MONO: por debajo de ~120 Hz, lo mismo por todas las bocinas
//
// En el billar hay cuatro bocinas en el gabinete y tres repartidas por el local, y no se sabe cual lleva el canal
// izquierdo y cual el derecho. En estereo cada bocina da SU canal. Si una grabacion trae el bajo cargado a un lado
// -pasa en algunas grabaciones viejas- o el grave de un canal al reves que el del otro (en contrafase), quien esta
// junto a la bocina que no lo lleva se queda sin bajo, y donde se juntan las dos el grave se cancela. Por debajo de
// ~120 Hz el oido apenas distingue de donde viene el sonido: en un bar, ahi el estereo no aporta y si estorba. Asi
// que ahi se pone el mismo grave en los dos canales. Por encima, todo sigue tal cual.
//
// COMO (EN FASE LINEAL desde el 24-10-2026; decision del dueno: "lo que sea mejor para la calidad del sonido").
//   medio = (L + R) / 2 y lado = (L - R) / 2.
//   El LADO pierde su grave con un paso-bajo FIR SIMETRICO (de fase cero): lado sin grave = lado - pasobajo(lado). Un seno
//   cardinal con ventana de Kaiser: -6 dB en el corte (como el cruce de antes), una transicion de un corte de ancho y 60 dB por
//   debajo; por encima de 1,6 veces el corte, el lado sale entero (0,01 dB).
//   El MEDIO no pasa por NADA: sale bit a bit como entra.
//   L = medio + lado y R = medio - lado.
// ANTES era el cruce Linkwitz-Riley de cuarto orden: el lado por su paso-alto y el medio por un paso-todo, para que siguieran en
// fase. El paso-todo no cambia el nivel de ninguna frecuencia, pero GIRA LA FASE del grave, y en una onda aplanada (un
// masterizado moderno, con el bombo pegado al techo) eso SUBE LOS PICOS: medido, de 1,5 a 2,7 dB con 3 a 12 dB de limitador de
// masterizado, que el limitador de la rocola tenia que volver a quitar (en la cadena de fabrica: mas de 1 dB el 46 % del tiempo
// y hasta 3,8 dB, frente al 32 % y 1,9 dB sin el). En fase lineal los picos del medio no se mueven.
// COMO SE HACE: el filtro mide 2K + 1 coeficientes (1333 a 44,1 kHz con el corte en 120 Hz), asi que para sacar un cuadro hacen
// falta los K que vienen detras. La etapa LEE POR DELANTE (como el DSEE): lo que sale va en su sitio, sin retardo. Trabaja por
// bloques de B cuadros con una ventana de M = B + 2K (una FFT de 4096 a 44,1 kHz): convolucion por FFT, solapar y guardar. Cuesta
// lo que una pasada del DSEE. El estrecho se aplica al armar cada bloque (unos 60 ms antes de oirse); el subgrave, en cada lectura.
// En el cambio a la pista siguiente del mismo disco (Sigue) la siguiente empieza con los ultimos K cuadros de la anterior. La
// anterior acaba sin conocer la siguiente: en sus ultimos 15 ms deja pasar algo del grave del LADO (nada del medio). Lo que le falto
// quitar en su ultimo cuadro, la siguiente lo lleva a cero en 25 ms (EL EMPALME): sin escalon en el cambio.
//
// UN CANAL AL REVES. Si una grabacion trae un canal con la polaridad invertida (un defecto de copia), su grave va
// entero en el lado: sumarlo a mono lo borraria del todo. Por eso se vigila, en la banda del grave, cuanto hay en el
// medio y cuanto en el lado (energia de los ultimos ~0,3 s). Si el lado tiene mas del doble que el medio (el grave de
// los dos canales va claramente al reves), ese grave del lado no se tira: se devuelve, en fase, a los dos canales
// (el grave que sale es el del canal izquierdo). Entra en medio segundo y se quita cuando el medio vuelve a tener
// mas que el lado. En una grabacion normal (el bajo en el centro, abierto a un lado o distinto en cada canal) esto
// nunca se activa.
//
// Lo que no se toca: un archivo de un canal (ya es mono; desde el 24-10-2026 los mono se abren como estereo de dos canales
// iguales, Form1.Sonido.cs) y uno de mas de dos. Y lo mono de un estereo sale tal cual, bit a bit.
//
// CUADROS ENTEROS. Para saber medio y lado hacen falta los dos canales del mismo instante, asi que siempre se
// entregan cuadros enteros: si el origen entrega uno partido, se le sigue pidiendo hasta completarlo (como hace el
// limitador). Si alguien pide una sola muestra, se hace el cuadro entero aparte y la otra mitad sale en la lectura
// siguiente.
//
// DISCO CONTINUO. Entre una pista y la siguiente que sigue sin fundido (la del mismo disco), la etapa de la que entra
// sigue con lo de la que acabo (Sigue): sus ultimos cuadros, la vigilancia y los mandos, y el empalme de arriba. Con fundido no
// se hereda nada: la anterior acaba en silencio y la siguiente empieza de cero, como siempre.
// EL CEBADO (24-10-2026). La siguiente trae al empezar los cuadros de los retardos de delante (el realce, el des-recorte, el
// siseo), que el relevo tira. Antes pasaban por los filtros heredados: para ellos eran un hueco de 0,7 a 2,3 ms entre las dos
// pistas, y con el paso-todo de antes eso daba un golpe. Medido en la cadena entera con la version publicada (pruebas/
// cadenaentera.cs, la musica a 0,3 de pico): un escalon de 0,33 en el cambio. Ahora el cebado pasa tal cual, sin entrar en los
// filtros (Cebado, lo pone ArmaCadena), y lo mismo en las demas etapas que heredan (el local, la presencia, el realce, el bajo
// sintetico y el limitador): 0,0002.
//
// "grave_mono=" en la configuracion: el corte en Hz (de fabrica 120; 0 lo quita). Medido en pruebas/gravemono.cs.
//
// LAS BOCINAS DEL BILLAR (fotos del 9 y 10 de octubre de 2026). En el gabinete, dos medios-graves de 6,5" (GVT
// SPB2327, 8 ohm) y dos tweeters (Mitzu TW-4700, 4 ohm) con un amplificador de dos canales con entradas de microfono.
// Fuera, tres cajas de dos o tres vias con conos de unos 12" y puertos, cada una en una zona del local: junto al arco,
// en la sala del fondo y en el entrepiso. No hay subwoofer. Con eso, esta etapa hace dos cosas mas (Ajustes de abajo):
//  - EL SUBGRAVE FUERA: por debajo de ~30 Hz ninguna de esas bocinas da nada que se oiga, y por debajo de la sintonia
//    de su puerto el cono se queda sin freno: ese grave solo gasta amplificador y mueve los conos de mas. La curva de un
//    paso-alto Butterworth de cuarto orden (24 dB/octava) en los dos canales: a 30 Hz baja 3 dB, a 50 Hz una decima
//    de dB, y a 20 Hz ya quita 13 dB. "subgrave=" en Hz (de fabrica 30; 0 lo quita; entre 15 y 60).
//    EN FASE LINEAL desde el 24-10-2026. Antes era el Butterworth de verdad (dos bicuadradas), que gira la fase del grave:
//    a 50 Hz corria el bombo 96 grados y a 80 Hz 57. Como el paso-todo del cruce de antes, eso SUBIA LOS PICOS de lo aplastado,
//    y el limitador los volvia a quitar: en la cadena de fabrica con el subgrave puesto, un masterizado de 9 dB pasaba mas de 1
//    dB por el limitador el 39 % del tiempo (hasta 4,3 dB) y una cancion aplastada el 90 % (hasta 4,9 dB); con el modo automatico
//    del local en billar o en bocinas chicas, que lo encienden, del 21 al 72 %. Ahora se le quita a la musica lo que esa curva
//    quitaria (1 - |Butterworth|, en fase cero: un FIR simetrico de 3,5 periodos del corte a cada lado, 117 ms a 30 Hz, por
//    FFT, como el lado) y el grave queda en su sitio: 3 %, 7 % y del 1 al 3 %, sin pasar de 2,7 dB. La curva es la misma con
//    0,3 dB (a 15 Hz quita 22 dB en vez de 24). Con el cruce puesto el lado ya va sin grave, asi que basta con quitarselo al
//    medio: los dos canales pierden lo mismo y lo mono sigue mono bit a bit. APAGADO pasa tal cual y no lee por delante (solo
//    guarda lo ultimo que salio); al encenderlo lee por delante 255 ms (de una vez) y desde ahi va por bloques; al apagarlo,
//    acabada la rampa, lo leido por delante sale tal cual y vuelve a pasar tal cual. Cuesta unos 0,2 us por cuadro encendido
//    (Mono, esta maquina; el doble sin el cruce) y 7 ns apagado.
//    A MENOS RESOLUCION desde el 27-10-2026 (sD, GraveDeDiezmado): lo que se quita se calcula con la ventana diezmada 16 veces y se
//    devuelve a la tasa entera; sale igual a menos de -85 dB del calculo entero y cuesta de 2 a 4 veces menos (a 30 Hz, de unos 11 a
//    unos 3 ms por segundo de musica en Mono).
//  - EL ESTEREO MAS ESTRECHO: cada zona oye sobre todo SU bocina, o sea un solo canal. Un instrumento grabado todo
//    a un lado desaparecia en la zona del otro. El lado (L - R) por encima del corte se multiplica por el ancho: al
//    60 % lo que va todo a la izquierda sale por la derecha 12 dB mas bajo, en vez de nada. Lo que va al centro (casi
//    todo: voz, bajo, bombo) no cambia ni de nivel. "ancho_estereo=" en % (de fabrica 60; 100 lo deja como viene).
// Son para ESE montaje (bocinas repartidas por zonas, sin subwoofer): en otro sitio, con un par de bocinas y alguien
// enfrente, convienen el estereo entero y todo el grave. Por eso cada una lleva su interruptor en Ctrl+F3 -> Sonido,
// apagado de fabrica ("estereo_estrecho=" y "corta_subgrave=", 1/0), y se oye al instante en la cancion que suena.
// "subgrave=" y "ancho_estereo=" fijan cuanto. Apagados la etapa sale bit a bit como antes (pruebas/subgrave.cs).
//
// SOLO SI HACE FALTA (S3, 13-10-2026; "estereo_auto=", apagado de fabrica; necesita el estereo estrecho encendido). Las
// mezclas modernas ya llevan casi todo al centro y no hace falta estrecharlas; las de los 60 y 70, con la bateria a un
// lado y la voz al otro, si. Se vigila, por encima del corte, la energia del lado frente a la del medio (unos 3 s): si
// el lado pasa del 60 % del medio (paneo extremo), se estrecha, entrando en un segundo; si baja del 35 %, vuelve a como
// viene. En medio no cambia (para que no vaya y venga). Con el automatico apagado, todo sale bit a bit como antes.
//
// SIN CHASQUIDO AL TOCAR EL MANDO (P6, 23-10-2026). Encender o apagar el estereo estrecho o el subgrave con la cancion sonando (desde
// Ctrl+F3, o al cambiar el tipo de local del modo automatico) antes cambiaba el ancho o el filtro de golpe: un escalon en la onda, que
// se oia como un "tic" (medido: el salto entre dos muestras seguidas llegaba a 6 y 8 dB sobre lo normal con el estereo y a 16 dB al
// quitar el subgrave). Ahora el ancho que se aplica sigue al pedido en una rampa de 25 ms, y el subgrave entra y sale mezclando lo
// filtrado con lo que entra en esos 25 ms (al apagarlo el filtro sigue hasta acabar la mezcla). Fuera de esos 25 ms la cuenta es
// la de siempre, bit a bit: en estado estable lo unico que hay de mas es una comparacion por cuadro.
using System;

namespace Rocola
{
    public partial class Form1
    {
        // El corte del grave en mono, en Hz ("grave_mono=" en config; 0 = apagado). Se lee al armar cada cancion.
        private static float graveMonoHz = GraveMono.HzDeFabrica;
        // El ancho del estereo (0..1; "ancho_estereo=" en %) y el corte del subgrave ("subgrave=" en Hz; 0 = sin corte).
        private static float anchoEstereo = GraveMono.AnchoDeFabrica, subgraveHz = GraveMono.SubDeFabrica;
        // Los interruptores de Ctrl+F3 -> Sonido (apagados de fabrica: son para las bocinas del billar).
        private static bool estereoEstrecho, cortaSubgrave;
        // S3 (13-10-2026): el estereo estrecho solo en las canciones que lo necesitan ("estereo_auto=", apagado de fabrica).
        private static bool estereoAuto;
        private static float AnchoEstereo(string texto)
        {
            float v;
            if (!float.TryParse(texto.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                || float.IsNaN(v) || float.IsInfinity(v)) return anchoEstereo;
            return Clampf(v, 0f, 100f) / 100f;
        }
        private static float SubgraveHz(string texto)
        {
            float v;
            if (!float.TryParse(texto.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                || float.IsNaN(v) || float.IsInfinity(v)) return subgraveHz;
            return v <= 0f ? 0f : Clampf(v, GraveMono.SubMinimo, GraveMono.SubMaximo);
        }
        private static float GraveMonoHz(string texto)
        {
            float v;
            if (!float.TryParse(texto.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                || float.IsNaN(v) || float.IsInfinity(v)) return graveMonoHz;
            return v <= 0f ? 0f : Clampf(v, GraveMono.HzMinimo, GraveMono.HzMaximo);
        }

        private sealed class GraveMono : NAudio.Wave.ISampleProvider
        {
            internal const float HzDeFabrica = 120f, HzMinimo = 40f, HzMaximo = 250f;
            internal const double VigilaSeg = 0.3, VueltaSeg = 0.5;   // ventana de la vigilancia y lo que tarda en dar la vuelta
            internal const float AnchoDeFabrica = .6f, SubDeFabrica = 30f, SubMinimo = 15f, SubMaximo = 60f;
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly bool activo;                 // hay algo que hacer (si no, pasa tal cual)
            private readonly bool cruce;                  // estereo, con un corte que tenga sentido a esta frecuencia
            private readonly bool ajustable;              // armada con ancho/subgrave (los interruptores la cambian en vivo)
            private readonly int tasa, canales;
            private readonly float hz, ancho, subHz;
            // Los interruptores: los cambia la ventana de ajustes en cualquier momento; se leen al empezar cada lectura.
            private volatile bool anchoOn, subOn, autoOn;
            // S3: la energia del lado y del medio por encima del corte, y cuanto se estrecha ahora (0 = como viene).
            internal const double AutoSeg = 3.0, AutoEntra = 0.6, AutoSale = 0.35, AutoRampaSeg = 1.0, AutoPiso = 1e-7;
            private readonly double aAuto, pasoAuto;
            private double eaM, eaS, kAuto, metaAuto;
            internal volatile float AnchoAhora = 1f;   // el ancho que sale ahora (lo ensena Ctrl+F3: S5)
            // EL SUBGRAVE EN FASE LINEAL (ver la cabecera). sK: medio filtro; sM: la FFT; sB: cuadros por bloque (sM = sB + 2 sK).
            internal const double SubPeriodos = 3.5;      // medio filtro, en periodos del corte: 117 ms a 30 Hz
            private readonly bool sub;                    // hay subgrave posible (filtro hecho)
            private readonly int sK, sM, sB, sCh;
            // Con el grave en mono (cruce) lo de debajo del corte ya es lo mismo en los dos canales (el lado sale sin grave): basta con
            // quitarselo al medio, y a los dos canales lo mismo. Media cuenta, y lo mono sigue siendo mono bit a bit.
            private readonly bool sMedio;
            private readonly float[] sH;                  // lo que se quita, en frecuencia (real: fase cero), de 0 a sM/2
            private readonly float[] sL, sR;              // la ventana: L y R de los cuadros [p - sK, p + sB + sK), p = el primero del bloque
            private readonly float[] sLr, sLi, sRr, sRi;  // la FFT de trabajo de cada canal (sM/2); despues, lo que se quita (pares en r, impares en i)
            private readonly float[] sAnillo;             // apagado: los ultimos sK cuadros que salieron (intercalados), para encenderlo ya
            private readonly AudioFftPlan sPlan, sPlanTodo;   // la FFT de sM/2 y las tablas de giro de sM
            // A MENOS RESOLUCION (27-10-2026): lo que se quita no pasa de unas 4 veces el corte (a 4 veces ya es -100 dB), asi que se calcula
            // con la ventana diezmada sD veces (a unos 2,8 kHz): un paso-bajo de fase cero (sG, Kaiser de 90 dB, mitad sLg) la diezma, la FFT
            // de sMd hace el filtro de siempre a esa tasa (sHd) y el mismo paso-bajo la devuelve a la tasa entera. Con sD = 1 (tasas bajas),
            // la FFT de la ventana entera como antes.
            internal const int DiezmaMinimoHz = 2400;
            internal const double DiezmaAtenuacionDb = 90.0;
            private readonly int sD, sLg, sC0, sNd, sMd;
            private readonly float[] sG, sHd, sDr, sDi;
            private readonly AudioFftPlan sPlanD;
            private int sModo;                            // 0: tal cual; 1: por bloques; 2: vaciando, tal cual, lo leido por delante
            private int sA;                               // donde escribe el anillo (en muestras)
            private int sT, sSalen, sVac, sVacN, sUnionN; // el cuadro que toca del bloque y cuantos salen; lo que queda por vaciar
            private long sLeidos, sEmitidos;              // cuadros leidos y entregados desde que se encendio
            private bool sPrimero, sAcabo, sFinHecho, sFinFiltra, sUnion, sMedia;
            private double sEl, sEr;                      // el empalme: lo que la anterior no llego a quitar en el primer cuadro (L, R)
            private float sOtra;                          // la otra mitad de un cuadro entregado a medias
            // EL EMPALME con la pista anterior del mismo disco (los dos filtros). La anterior acabo sin conocer esta: en su ultimo cuadro le
            // falto quitar lo que de esta ya caia en su ventana. Esa diferencia, la del primer cuadro de esta, se lleva a cero en 25 ms:
            // sin escalon, y sin dejar pasar mas que eso.
            private readonly int nUnion;
            private bool unionLado;
            private readonly double aVigila, aEnergia, pasoVuelta;
            private double vm1, vm2, vs1, vs2, em, es;    // vigilancia: grave del medio y del lado, y su energia
            // EL FILTRO EN FASE LINEAL (ver la cabecera). K: medio filtro; M: la FFT; B: cuadros por bloque (M = B + 2K).
            internal const double AtenuacionDb = 60.0;
            private readonly int K, M, B;
            private readonly float[] hRe, hIm;            // el filtro en frecuencia, de 0 a M/2 (compartido entre canciones de la misma tasa y corte)
            private readonly float[] wl, wr;              // la ventana: L y R de los cuadros [p - K, p + B + K), p = el primero del bloque
            private readonly float[] fr, fi;              // la FFT de trabajo (M/2: el lado es real, ver GraveDelLado); despues, el grave del lado
            private readonly float[] lee;                 // lo que se pide al origen (intercalado)
            private readonly float[] cola;                // el bloque ya hecho (intercalado) y cuanto queda por entregar
            private int colaI, colaN;
            private long leidos, emitidos;                // cuadros leidos del origen y entregados
            private bool primerBloque = true, finHecho;
            private readonly float[] finL, finR;          // los ultimos K cuadros de la cancion (para la siguiente del mismo disco)
            private readonly AudioFftPlan plan, planTodo;   // la FFT de M/2 y las tablas de giro de M
            private double vuelta, metaVuelta;            // cuanto grave del lado se devuelve en fase (0 o 1, en rampa)
            private readonly float[] sale = new float[2]; // un cuadro ya hecho que se entrego a medias
            private int saleN;
            private int limpia;                           // cuadros desde la ultima limpieza de restos diminutos
            private bool empezo, acabo;
            private volatile GraveMono anterior;          // la pista que acaba justo antes, sin fundido (disco continuo)
            // EL CEBADO (lo pone ArmaCadena): los cuadros que traen al empezar los retardos de delante (el realce, el des-recorte, el
            // siseo). El relevo de un disco continuo los tira; si pasaran por los filtros heredados serian un hueco entre las dos pistas.
            internal int Cebado;
            private int cebadoPorPasar;                   // muestras del cebado que aun pasan tal cual (solo tras heredar)
        // P6: el ancho que se aplica ahora (sigue al pedido en RampaSeg) y la mezcla del subgrave (1 = filtrado, 0 = como entra).
        internal const double RampaSeg = 0.025;
        private double anRampa, subMezcla;
        private bool anListo, subListo;
        private readonly double pasoAncho, pasoSub;

            // Como siempre: solo el grave en mono (sin tocar el ancho ni el subgrave).
            public GraveMono(NAudio.Wave.ISampleProvider s, float corteHz) : this(s, corteHz, 1f, 0f, false) { }

            // Con el ancho y el subgrave encendidos (las pruebas).
            public GraveMono(NAudio.Wave.ISampleProvider s, float corteHz, float anchoEstereo, float subgraveHz)
                : this(s, corteHz, anchoEstereo, subgraveHz, true, true) { }

            public GraveMono(NAudio.Wave.ISampleProvider s, float corteHz, float anchoEstereo, float subgraveHz, bool conAncho, bool conSubgrave)
                : this(s, corteHz, anchoEstereo, subgraveHz, true) { anchoOn = conAncho; subOn = conSubgrave; }

            private GraveMono(NAudio.Wave.ISampleProvider s, float corteHz, float anchoEstereo, float subgraveHz, bool ajusta)
            {
                src = s;
                tasa = s.WaveFormat.SampleRate;
                canales = s.WaveFormat.Channels;
                hz = corteHz;
                ancho = (anchoEstereo >= 0f && anchoEstereo <= 1f) ? anchoEstereo : 1f;
                subHz = subgraveHz;
                cruce = canales == 2 && tasa >= 8000 && corteHz > 0f && corteHz < 0.2f * tasa;
                sub = (canales == 1 || canales == 2) && tasa >= 8000 && subgraveHz > 0f && subgraveHz < 0.1f * tasa;
                // Armada para los interruptores, se queda en la cadena aunque esten apagados: asi encenderlos se oye ya.
                ajustable = ajusta && (canales == 1 || canales == 2) && tasa >= 8000;
                activo = cruce || ajustable;
                aAuto = 1.0 - Math.Exp(-1.0 / (AutoSeg * Math.Max(1, tasa)));
                pasoAuto = 1.0 / (AutoRampaSeg * Math.Max(1, tasa));
                pasoSub = 1.0 / (RampaSeg * Math.Max(1, tasa));
                pasoAncho = Math.Max(1e-9, 1.0 - ancho) / (RampaSeg * Math.Max(1, tasa));   // de ancho entero a ancho estrecho (o al reves) en 25 ms
                nUnion = Math.Max(1, (int)Math.Round(RampaSeg * tasa));
                if (sub)
                {   // el largo (3,5 periodos del corte a cada lado), la FFT (el bloque, al menos medio filtro) y la ventana
                    sCh = canales;
                    sD = 1; while (tasa / (sD * 2) >= DiezmaMinimoHz) sD <<= 1;
                    if (sD >= 4)
                    {   // el paso-bajo: plano hasta 4 veces el corte y sin nada desde la tasa diezmada menos eso (lo que se doblaria hasta ahi)
                        double fsd = (double)tasa / sD, banda = 4.0 * subgraveHz, dw = 2.0 * Math.PI * (fsd - 2.0 * banda) / tasa;
                        sLg = (int)Math.Ceiling((DiezmaAtenuacionDb - 8.0) / (2.285 * dw) / 2.0);
                        int kd = Math.Max(4, (int)Math.Round(SubPeriodos * fsd / subgraveHz));
                        sK = kd * sD + 2 * sLg + sD; sK = (sK + sD - 1) / sD * sD;
                        int ms = 4096; while (ms < 2 * sK + sK / 2) ms <<= 1;
                        sM = ms; sB = sM - 2 * sK;
                        sC0 = (sLg + sD - 1) / sD * sD;
                        sNd = (sM - 1 - sLg - sC0) / sD + 1;
                        int md = 64; while (md < sNd) md <<= 1;
                        sMd = md;
                        sG = PasoBajoDiezma(sD, sLg);
                        sHd = FiltroSub(fsd, subgraveHz, kd, sMd);
                        sPlanD = GetAudioFftPlan(sMd);
                        sDr = new float[sMd]; sDi = new float[sMd];
                    }
                    else
                    {
                        sD = 1;
                        sK = Math.Max(16, (int)Math.Round(SubPeriodos * tasa / subgraveHz));
                        int ms = 4096; while (ms < 2 * sK + sK / 2) ms <<= 1;
                        sM = ms; sB = sM - 2 * sK;
                        sH = FiltroSub(tasa, subgraveHz, sK, sM);
                        sPlan = GetAudioFftPlan(sM / 2); sPlanTodo = GetAudioFftPlan(sM);
                    }
                    sMedio = cruce;
                    sL = new float[sM]; sLr = new float[sM / 2]; sLi = new float[sM / 2];
                    if (canales == 2) sR = new float[sM];
                    if (canales == 2 && !sMedio) { sRr = new float[sM / 2]; sRi = new float[sM / 2]; }
                    sAnillo = new float[sK * canales];
                }
                if (!cruce) return;
                double w0 = 2.0 * Math.PI * corteHz / tasa;
                aVigila = 1.0 - Math.Exp(-w0);
                aEnergia = 1.0 - Math.Exp(-1.0 / (VigilaSeg * tasa));
                pasoVuelta = 1.0 / (VueltaSeg * tasa);
                // El largo del filtro (Kaiser: transicion de 'corteHz' Hz y AtenuacionDb), la FFT (al menos cuatro medios filtros, para
                // que el bloque no sea mas corto que el filtro) y el bloque.
                K = ((int)Math.Ceiling((AtenuacionDb - 8.0) / (2.285 * w0)) + 1) / 2;
                int m = 2048; while (m < 4 * K) m <<= 1;
                M = m; B = M - 2 * K;
                FiltroEnFrecuencia(tasa, corteHz, K, M, out hRe, out hIm);
                plan = GetAudioFftPlan(M / 2); planTodo = GetAudioFftPlan(M);
                wl = new float[M]; wr = new float[M]; fr = new float[M / 2]; fi = new float[M / 2];
                lee = new float[2 * (B + K)]; cola = new float[2 * B];
                finL = new float[K]; finR = new float[K];
            }

            // El paso-bajo del lado, en frecuencia: seno cardinal (corte a -6 dB) con ventana de Kaiser, normalizado a 1 en el grave,
            // los 2K + 1 coeficientes al principio de M ceros y su FFT. Se guarda el ultimo: cancion tras cancion es el mismo.
            private static readonly object filtroCerrojo = new object();
            private static int filtroTasa, filtroK, filtroM; private static float filtroHz; private static float[] filtroRe, filtroIm;
            private static void FiltroEnFrecuencia(int tasa, float hz, int K, int M, out float[] re, out float[] im)
            {
                lock (filtroCerrojo)
                    if (filtroRe != null && filtroTasa == tasa && filtroHz == hz && filtroK == K && filtroM == M) { re = filtroRe; im = filtroIm; return; }
                double beta = 0.1102 * (AtenuacionDb - 8.7), fc = hz / (double)tasa, i0b = BesselI0(beta), suma = 0.0;
                double[] h = new double[2 * K + 1];
                for (int j = 0; j <= 2 * K; j++)
                {
                    int i = j - K; double x = 2.0 * fc * i, u = (double)i / K;
                    double sinc = i == 0 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
                    h[j] = 2.0 * fc * sinc * BesselI0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - u * u))) / i0b;
                    suma += h[j];
                }
                float[] fr0 = new float[M], fi0 = new float[M];
                for (int j = 0; j <= 2 * K; j++) fr0[j] = (float)(h[j] / suma);
                Fft(fr0, fi0, GetAudioFftPlan(M), false);
                // Solo hace falta de 0 a M/2: el filtro es real y su espectro, simetrico.
                float[] r = new float[M / 2 + 1], q = new float[M / 2 + 1];
                for (int j = 0; j <= M / 2; j++)
                {
                    float a = fr0[j], b = fi0[j];
                    r[j] = (a < 1e-15f && a > -1e-15f) ? 0f : a; q[j] = (b < 1e-15f && b > -1e-15f) ? 0f : b;
                }
                lock (filtroCerrojo) { filtroTasa = tasa; filtroHz = hz; filtroK = K; filtroM = M; filtroRe = r; filtroIm = q; }
                re = r; im = q;
            }
            // LO QUE QUITA EL SUBGRAVE, en frecuencia: 1 - |Butterworth de cuarto orden| (la curva de siempre), en fase cero. Su respuesta
            // al impulso sale de la transformada inversa de esa curva (con 8 veces el largo del filtro: lo que se repite de tanto en tanto
            // ya no pesa; y hasta 12 veces el corte, donde la curva ya es 1e-9); se le pone la ventana de Kaiser de 2K + 1, se ajusta a 1 en
            // el grave y se pasa a la FFT de M, centrada en 0 (asi el espectro es real). Se guarda el ultimo: cancion tras cancion es el mismo.
            private static readonly object subCerrojo = new object();
            private static int subK, subM; private static double subTasa; private static float subFc; private static float[] subFiltro;
            private static float[] FiltroSub(double tasa, float hz, int K, int M)
            {
                lock (subCerrojo)
                    if (subFiltro != null && subTasa == tasa && subFc == hz && subK == K && subM == M) return subFiltro;
                int N = 8 * (2 * K + 1);
                double dw = 2.0 * Math.PI / N, wc = 2.0 * Math.PI * hz / tasa;
                int Q = Math.Min(N / 2, (int)Math.Ceiling(12.0 * wc / dw));
                double[] g = new double[K + 1];
                for (int q = 0; q <= Q; q++)
                {
                    double w = q * dw, G = q == 0 ? 0.5 : 1.0 - 1.0 / Math.Sqrt(1.0 + Math.Pow(wc / w, 8));
                    double cw = Math.Cos(w), sw = Math.Sin(w), c = 1.0, sn = 0.0;   // cos(w n) por giros: el error crece como n * 1e-16
                    for (int n = 0; n <= K; n++) { g[n] += G * c; double c2 = c * cw - sn * sw; sn = sn * cw + c * sw; c = c2; }
                }
                double beta = 0.1102 * (AtenuacionDb - 8.7), i0b = BesselI0(beta), suma = 0.0;
                double[] h = new double[K + 1];
                for (int n = 0; n <= K; n++)
                {
                    double u = (double)n / K;
                    h[n] = g[n] * BesselI0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - u * u))) / i0b;
                    suma += n == 0 ? h[n] : 2.0 * h[n];
                }
                float[] re = new float[M], im = new float[M];
                for (int n = 0; n <= K; n++) { re[n] = (float)(h[n] / suma); if (n > 0) re[M - n] = re[n]; }
                Fft(re, im, GetAudioFftPlan(M), false);
                float[] r = new float[M / 2 + 1];
                for (int k = 0; k <= M / 2; k++) { float a = re[k]; r[k] = (a < 1e-15f && a > -1e-15f) ? 0f : a; }
                lock (subCerrojo) { subTasa = tasa; subFc = hz; subK = K; subM = M; subFiltro = r; }
                return r;
            }
            // EL GRAVE DEL LADO DE TODA LA VENTANA (solapar y guardar). El lado es real: sus M muestras van de dos en dos a una FFT
            // compleja de M/2 (las pares en la parte real, las impares en la imaginaria), se separan sus dos mitades, se pasa por el
            // filtro de 0 a M/2 y se vuelven a juntar para la FFT inversa de M/2. Lo mismo que la FFT de M entera, con la mitad de
            // trabajo. Queda en fr (cuadros pares) y fi (impares). Desde 'hasta' la ventana cuenta como ceros (el empalme: solo la anterior).
            private void GraveDelLado(int hasta)
            {
                int h = M >> 1;
                float[] zr = fr, zi = fi, a = wl, b = wr, hr = hRe, hi = hIm, wc = planTodo.Cos, ws = planTodo.Sin;
                for (int n = 0, j = 0; n < h; n++, j += 2)
                {
                    float s0 = j < hasta ? 0.5f * (a[j] - b[j]) : 0f, s1 = j + 1 < hasta ? 0.5f * (a[j + 1] - b[j + 1]) : 0f;
                    if (s0 < 1e-30f && s0 > -1e-30f) s0 = 0f;
                    if (s1 < 1e-30f && s1 > -1e-30f) s1 = 0f;
                    zr[n] = s0; zi[n] = s1;
                }
                Fft(zr, zi, plan, false);
                // k = 0 (y M/2): X[0] = re + im, X[M/2] = re - im, los dos reales; el filtro ahi tambien es real.
                {
                    double x0 = (zr[0] + zi[0]) * (double)hr[0], xh = (zr[0] - zi[0]) * (double)hr[h];
                    zr[0] = (float)(0.5 * (x0 + xh)); zi[0] = (float)(0.5 * (x0 - xh));
                }
                for (int k = 1; k <= h / 2; k++)
                {
                    int j = h - k;
                    double akr = zr[k], aki = zi[k], ajr = zr[j], aji = zi[j];
                    // Las dos mitades (pares e impares) y X[k] = par + W^k impar, X[j] igual; W^k = cos - i sen (tablas de M).
                    double ekr = 0.5 * (akr + ajr), eki = 0.5 * (aki - aji), okr = 0.5 * (aki + aji), oki = -0.5 * (akr - ajr);
                    double ejr = 0.5 * (ajr + akr), eji = 0.5 * (aji - aki), ojr = 0.5 * (aji + aki), oji = -0.5 * (ajr - akr);
                    double ck = wc[k], sk = ws[k], cj = wc[j], sj = ws[j];
                    double xkr = ekr + (okr * ck + oki * sk), xki = eki + (oki * ck - okr * sk);
                    double xjr = ejr + (ojr * cj + oji * sj), xji = eji + (oji * cj - ojr * sj);
                    // Por el filtro.
                    double ykr = xkr * hr[k] - xki * hi[k], yki = xkr * hi[k] + xki * hr[k];
                    double yjr = xjr * hr[j] - xji * hi[j], yji = xjr * hi[j] + xji * hr[j];
                    // Y de vuelta a una FFT de M/2: Z'[k] = E' + i O', con E' = (Y[k] + conj Y[j]) / 2 y O' = (Y[k] - conj Y[j]) W^-k / 2.
                    double e1r = 0.5 * (ykr + yjr), e1i = 0.5 * (yki - yji), d1r = 0.5 * (ykr - yjr), d1i = 0.5 * (yki + yji);
                    double o1r = d1r * ck - d1i * sk, o1i = d1r * sk + d1i * ck;
                    double e2r = 0.5 * (yjr + ykr), e2i = 0.5 * (yji - yki), d2r = 0.5 * (yjr - ykr), d2i = 0.5 * (yji + yki);
                    double o2r = d2r * cj - d2i * sj, o2i = d2r * sj + d2i * cj;
                    zr[k] = (float)(e1r - o1i); zi[k] = (float)(e1i + o1r);
                    zr[j] = (float)(e2r - o2i); zi[j] = (float)(e2i + o2r);
                }
                Fft(zr, zi, plan, true);
            }
            private static double BesselI0(double x)
            {
                double s = 1.0, t = 1.0;
                for (int k = 1; k < 200; k++) { double v = x / (2.0 * k); t *= v * v; s += t; if (t < 1e-16 * s) break; }
                return s;
            }
            // FFT compleja de base 2 sobre r + i m, en su sitio (la del DSEE, con las tablas compartidas). La inversa divide entre n.
            private static void Fft(float[] r, float[] m, AudioFftPlan p, bool inversa)
            {
                int n = r.Length;
                int[] br = p.Reverse;
                for (int i = 0; i < n; i++)
                {
                    int j = br[i];
                    if (j > i) { float t = r[i]; r[i] = r[j]; r[j] = t; t = m[i]; m[i] = m[j]; m[j] = t; }
                }
                float[] tc = p.Cos, ts = inversa ? p.Sin : p.SinNeg;
                for (int size = 2; size <= n; size <<= 1)
                {
                    int half = size >> 1, tstep = n / size;
                    for (int i = 0; i < n; i += size)
                        for (int j = i, k = 0; j < i + half; j++, k += tstep)
                        {
                            float wr = tc[k], wi = ts[k];
                            int b = j + half;
                            float rb = r[b], mb = m[b];
                            float tr = rb * wr - mb * wi, ti = rb * wi + mb * wr;
                            r[b] = r[j] - tr; m[b] = m[j] - ti;
                            r[j] += tr; m[j] += ti;
                        }
                }
                if (inversa) { float sc = 1f / n; for (int i = 0; i < n; i++) { r[i] *= sc; m[i] *= sc; } }
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            internal bool Activo { get { return activo; } }
            internal float Ancho { get { return (ajustable && anchoOn) ? ancho : 1f; } }
            // Los interruptores de Ctrl+F3, en vivo: el hilo del sonido los toma en su siguiente lectura.
            internal void Ajusta(bool conAncho, bool conSubgrave)
            {
                if (!ajustable) return;
                anchoOn = conAncho; subOn = conSubgrave;
            }
            // S3: el estrecho, solo en las canciones que lo necesitan (necesita el estrecho encendido).
            internal void SoloSiHaceFalta(bool on) { if (ajustable) autoOn = on; }
            // Cuanto estrechar en este cuadro (S3), con las energias de lado y medio por encima del corte.
            private double PasoAuto(double lado2, double medio2)
            {
                eaS += (lado2 - eaS) * aAuto; eaM += (medio2 - eaM) * aAuto;
                if (eaM > AutoPiso || eaS > AutoPiso)
                {
                    double r = eaS / Math.Max(eaM, 1e-30);
                    if (r > AutoEntra) metaAuto = 1.0; else if (r < AutoSale) metaAuto = 0.0;
                }
                if (kAuto != metaAuto) kAuto = kAuto < metaAuto ? Math.Min(metaAuto, kAuto + pasoAuto) : Math.Max(metaAuto, kAuto - pasoAuto);
                return 1.0 - (1.0 - ancho) * kAuto;
            }
            // ¿Hace falta el subgrave? Encendido, o apagandose (la mezcla aun no llego a 0). Al empezar la cancion, la mezcla ya es la del mando.
            private bool SubQuiere()
            {
                if (!subListo) { subListo = true; subMezcla = subOn ? 1.0 : 0.0; }
                return subOn || subMezcla != 0.0;
            }

            // La siguiente pista del mismo disco sigue con los filtros de esta. Lo llama la pantalla al preparar el
            // relevo; se toma en la primera lectura, en el hilo del sonido, cuando esta ya acabo.
            internal void Sigue(GraveMono previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }

            private void Hereda()
            {
                GraveMono a = anterior; anterior = null;
                if (a == null || !a.activo || !a.acabo || a.tasa != tasa || a.hz != hz || a.canales != canales
                    || a.ancho != ancho || a.subHz != subHz || a.ajustable != ajustable) return;
                if (cruce && a.finHecho && a.K == K)
                {   // lo de antes de la primera muestra; el grave del lado empalma con el que salio al final de la anterior
                    Array.Copy(a.finL, 0, wl, 0, K); Array.Copy(a.finR, 0, wr, 0, K); unionLado = true;
                }
                if (sub && a.sFinHecho && a.sK == sK && a.sCh == sCh)
                {   // el subgrave: los ultimos cuadros de la anterior, y si los filtro sin conocer esta, el empalme
                    float[] o = a.sAnillo; int n = o.Length, j = a.sA;
                    for (int i = 0; i < n; i++) { sAnillo[i] = o[j]; if (++j == n) j = 0; }
                    sA = 0; sUnion = a.sFinFiltra;
                }
                vm1 = a.vm1; vm2 = a.vm2; vs1 = a.vs1; vs2 = a.vs2; em = a.em; es = a.es;
                vuelta = a.vuelta; metaVuelta = a.metaVuelta;
                eaM = a.eaM; eaS = a.eaS; kAuto = a.kAuto; metaAuto = a.metaAuto;
                anRampa = a.anRampa; anListo = a.anListo; subMezcla = a.subMezcla; subListo = a.subListo;
                cebadoPorPasar = Math.Max(0, Cebado) * canales;
            }

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (count == 0) return 0;   // no consumir el relevo de filtros hasta la primera lectura de audio
                if (!activo) return src.Read(buffer, offset, count);
                if (!empezo) { empezo = true; Hereda(); }
                if (cebadoPorPasar > 0)
                {   // tras la anterior del mismo disco, el cebado pasa tal cual, sin entrar en los filtros: siguen justo donde acabo su musica
                    int n = src.Read(buffer, offset, Math.Min(count, cebadoPorPasar));
                    if (n > 0)
                    {
                        cebadoPorPasar -= n;
                        if (n < count && cebadoPorPasar == 0) n += sub ? SubLee(buffer, offset + n, count - n) : Interior(buffer, offset + n, count - n);
                        return n;
                    }
                    cebadoPorPasar = 0;
                }
                return sub ? SubLee(buffer, offset, count) : Interior(buffer, offset, count);
            }

            // Lo de antes del subgrave: el grave en mono y el estereo estrecho (un canal: tal cual).
            private int Interior(float[] buffer, int offset, int count)
            {
                if (canales == 1)
                {
                    if (acabo) return 0;
                    int n1 = src.Read(buffer, offset, count);
                    if (n1 <= 0) { acabo = true; return 0; }
                    return n1;
                }
                if (cruce) return LeeEnFaseLineal(buffer, offset, count);
                int hechos = 0;
                // La otra mitad de un cuadro ya hecho, si la lectura anterior pidio un numero impar.
                if (saleN > 0 && count > 0) { buffer[offset] = sale[1]; saleN = 0; hechos = 1; }
                int pares = (count - hechos) & ~1;
                if (pares > 0 && !acabo)
                {
                    int n = Enteros(buffer, offset + hechos, pares);
                    Cuadros(buffer, offset + hechos, n >> 1);
                    hechos += n;
                }
                // Queda sitio para una sola muestra: se hace el cuadro entero aparte y sale su primera mitad.
                if (count - hechos == 1 && !acabo && Enteros(sale, 0, 2) == 2)
                {
                    Cuadros(sale, 0, 1);
                    buffer[offset + hechos++] = sale[0]; saleN = 1;
                }
                return hechos;
            }

            // Lee del origen hasta 'quiero' muestras (par), siempre cuadros enteros: si llega uno partido, se sigue
            // leyendo hasta completarlo. Solo al final puede quedar medio cuadro, que no se puede completar: se descarta.
            private int Enteros(float[] b, int o, int quiero)
            {
                int tiene = 0;
                while (tiene < quiero)
                {
                    int r = src.Read(b, o + tiene, quiero - tiene);
                    if (r <= 0) { acabo = true; break; }
                    tiene += r;
                    if ((tiene & 1) == 0) break;
                }
                return tiene & ~1;
            }

            // Sin cruce (grave_mono=0 o un corte sin sentido): solo el estereo estrecho y el subgrave, cuadro a cuadro.
            private void Cuadros(float[] b, int i, int cuadros) { SinCruce(b, i, cuadros); }

            // CON CRUCE, EN FASE LINEAL: se entrega lo que quede del bloque hecho y, si se acaba, se hace el siguiente. La cola va muestra
            // a muestra, asi que da igual como se pida (una sola muestra, impares) y nunca se parte nada.
            private int LeeEnFaseLineal(float[] buffer, int offset, int count)
            {
                int hechos = 0;
                while (hechos < count)
                {
                    if (colaN == 0 && !Bloque()) break;
                    int k = Math.Min(colaN, count - hechos), d = offset + hechos, q = colaI;
                    for (int i = 0; i < k; i++) buffer[d + i] = cola[q + i];   // a mano: 'buffer' puede ser la vista flotante de un byte[]
                    colaI += k; colaN -= k; hechos += k;
                }
                return hechos;
            }

            // UN BLOQUE: se lee lo que falta de la ventana (el primero, B + K cuadros: lee por delante), el grave del lado de toda la
            // ventana sale de la FFT, y los B cuadros del medio de la ventana se arman: L = medio + lado sin grave (por el ancho), R =
            // medio - lado sin grave; con un canal al reves, mas el grave del lado de vuelta en fase. Despues, el subgrave. Al final de la
            // cancion lo que falta de la ventana son ceros y sale solo lo que se leyo.
            private bool Bloque()
            {
                if (acabo && emitidos >= leidos) return false;
                int desde = primerBloque ? K : 2 * K, quiere = primerBloque ? B + K : B, tiene = 0;
                while (tiene < 2 * quiere && !acabo) { int r = Enteros(lee, tiene, 2 * quiere - tiene); if (r <= 0) break; tiene += r; }
                int got = tiene >> 1;
                float[] wl = this.wl, wr = this.wr;
                for (int f = 0, k = 0; f < got; f++, k += 2) { wl[desde + f] = lee[k]; wr[desde + f] = lee[k + 1]; }
                for (int f = got; f < quiere; f++) { wl[desde + f] = 0f; wr[desde + f] = 0f; }
                primerBloque = false;
                long p = emitidos;
                leidos += got;
                if (acabo && !finHecho)
                {   // los ultimos K cuadros de la cancion, por si la siguiente del mismo disco sigue sin fundido
                    finHecho = true;
                    for (int k = 0; k < K; k++)
                    {
                        long n = leidos - K + k; int w = (int)(n - p + K);
                        finL[k] = (n < 0 || w < 0) ? 0f : wl[w]; finR[k] = (n < 0 || w < 0) ? 0f : wr[w];
                    }
                }
                int salen = (int)Math.Min((long)B, leidos - p);
                if (salen <= 0) return false;
                // El grave del lado: la ventana entera por el paso-bajo. Despues, el del cuadro p + t es fr/fi en 2K + t (pares en fr,
                // impares en fi). Tras la pista anterior del mismo disco, primero el de solo la anterior (con eso acabo ella): el empalme.
                int nu = 0; double eu = 0.0;
                if (unionLado)
                {
                    unionLado = false; nu = Math.Min(nUnion, salen);
                    GraveDelLado(K); double solo = fr[K];   // el cuadro p esta en 2K: par
                    GraveDelLado(M); eu = fr[K] - solo;
                }
                else GraveDelLado(M);
                float[] r0 = fr, i0 = fi;
                double iu = 1.0 / nUnion;
                double an = (ajustable && anchoOn) ? ancho : 1.0;
                bool auto = ajustable && anchoOn && autoOn && ancho != 1f;
                double av = aVigila, ae = aEnergia, pv = pasoVuelta;
                double vm1 = this.vm1, vm2 = this.vm2, vs1 = this.vs1, vs2 = this.vs2, em = this.em, es = this.es;
                double vuelta = this.vuelta, meta = this.metaVuelta;
                double ar = anRampa, pa = pasoAncho; bool anOk = anListo;
                float[] c = cola;
                for (int t = 0, w = K, q = 0, g = 2 * K; t < salen; t++, w++, q += 2, g++)
                {
                    double l = wl[w], rr = wr[w];
                    double m = 0.5 * (l + rr), s = 0.5 * (l - rr);
                    double lo = (g & 1) == 0 ? r0[g >> 1] : i0[g >> 1];               // el grave del lado
                    if (t < nu) lo -= (1.0 - t * iu) * eu;                            // (el empalme)
                    double h2 = s - lo;                                               // y lo de encima del corte
                    // La vigilancia: el grave del medio y el del lado (dos polos en el corte) y su energia.
                    vm1 += (m - vm1) * av; vm2 += (vm1 - vm2) * av; em += (vm2 * vm2 - em) * ae;
                    vs1 += (s - vs1) * av; vs2 += (vs1 - vs2) * av; es += (vs2 * vs2 - es) * ae;
                    // Con grave de verdad (por encima del piso, -70 dB, como S3): en el silencio, con los restos diminutos del realce, se
                    // quedaba decidiendo "canal al reves" y empezaba a devolver el grave del lado, que en fase lineal lleva el eco previo
                    // de lo que viene. Por debajo del piso se queda como estaba.
                    if (es > AutoPiso || em > AutoPiso) { if (es > 2.0 * em) meta = 1.0; else if (es < em) meta = 0.0; }
                    if (vuelta != meta) { vuelta = vuelta < meta ? Math.Min(meta, vuelta + pv) : Math.Max(meta, vuelta - pv); }
                    double anF = an;
                    if (auto) { double mh = m - vm2; anF = PasoAuto(h2 * h2, mh * mh); }   // S3: solo si hace falta
                    if (!anOk) { ar = anF; anOk = true; }                            // P6: el ancho que se aplica sigue al pedido en 25 ms
                    else if (ar != anF) ar = ar < anF ? Math.Min(anF, ar + pa) : Math.Max(anF, ar - pa);
                    double lado = h2 * ar;                                           // el ancho (1 = como viene)
                    double pm = m;
                    if (vuelta != 0.0) pm += vuelta * lo;                             // el grave del lado, de vuelta y en fase
                    c[q] = (float)(pm + lado); c[q + 1] = (float)(pm - lado);
                    // En el silencio, los restos diminutos a cero (Desnormales.cs): cada 64 cuadros, no en cada uno.
                    if (++limpia == Desnormales.Cada)
                    {
                        limpia = 0;
                        vm1 = Desnormales.Limpia(vm1); vm2 = Desnormales.Limpia(vm2); vs1 = Desnormales.Limpia(vs1); vs2 = Desnormales.Limpia(vs2);
                        em = Desnormales.Limpia(em); es = Desnormales.Limpia(es);
                    }
                }
                this.vm1 = vm1; this.vm2 = vm2; this.vs1 = vs1; this.vs2 = vs2; this.em = em; this.es = es;
                this.vuelta = vuelta; this.metaVuelta = meta;
                anRampa = ar; anListo = anOk;
                AnchoAhora = anOk ? (float)ar : (auto ? (float)(1.0 - (1.0 - ancho) * kAuto) : (float)an);
                // La ventana avanza un bloque: lo de sus ultimos 2K cuadros pasa al principio.
                Array.Copy(wl, B, wl, 0, 2 * K); Array.Copy(wr, B, wr, 0, 2 * K);
                emitidos += salen; colaI = 0; colaN = 2 * salen;
                return true;
            }

            // Sin corte del grave en mono (grave_mono=0): medio y lado sin cruce, y solo si el estereo va estrechado.
            private void SinCruce(float[] b, int i, int cuadros)
            {
                bool quiere = anchoOn && ancho != 1f;
                if (!quiere && !anListo) { anRampa = 1.0; anListo = true; }   // arranca con el ancho entero: encenderlo en vivo despues va en rampa
                if (quiere || (anListo && anRampa != 1.0))   // (o aun vuelve al ancho entero, en la rampa de 25 ms)
                {
                    double an = quiere ? ancho : 1.0;
                    bool auto = quiere && autoOn;
                    double ar = anRampa, pa = pasoAncho; bool anOk = anListo;
                    for (int f = 0, k = i; f < cuadros; f++, k += 2)
                    {
                        double l = b[k], r = b[k + 1];
                        double m = 0.5 * (l + r), s0 = 0.5 * (l - r);
                        double anF = auto ? PasoAuto(s0 * s0, m * m) : an;   // S3: sin cruce, con toda la banda
                        if (!anOk) { ar = anF; anOk = true; }
                        else if (ar != anF) ar = ar < anF ? Math.Min(anF, ar + pa) : Math.Max(anF, ar - pa);
                        double s = s0 * ar;
                        b[k] = (float)(m + s); b[k + 1] = (float)(m - s);
                    }
                    anRampa = ar; anListo = anOk;
                    AnchoAhora = anOk ? (float)ar : (auto ? (float)(1.0 - (1.0 - ancho) * kAuto) : (float)an);
                }
                else AnchoAhora = 1f;
            }

            // EL SUBGRAVE EN FASE LINEAL: lo que sale es lo que entra menos su grave de debajo del corte (el paso-bajo de fase cero), por la
            // mezcla del mando. Apagado (modo 0) pasa tal cual y solo se guarda lo ultimo que salio; al encenderlo se entra en bloques (modo
            // 1): se lee por delante, como el grave del lado; al apagarlo, acabada la rampa y el bloque, lo leido por delante sale tal cual
            // (modo 2) y se vuelve al modo 0. Lo que sale se arma en cada lectura, con el mando de ese momento.
            private int SubLee(float[] buffer, int offset, int count)
            {
                int hechos = 0, ch = sCh;
                if (sMedia) { buffer[offset] = sOtra; sMedia = false; hechos = 1; }
                while (hechos < count)
                {
                    if (sModo == 0)
                    {
                        bool quiere = SubQuiere();
                        if (quiere && !sAcabo && sA % ch == 0) { Entra(); continue; }
                        int n = Interior(buffer, offset + hechos, quiere ? 1 : count - hechos);   // con un cuadro a medias, primero se completa
                        if (n <= 0) { if (!sAcabo) { sAcabo = true; sFinHecho = true; sFinFiltra = false; } break; }
                        Anillo(buffer, offset + hechos, n);
                        sUnion = false;
                        hechos += n;
                        continue;
                    }
                    if (sModo == 2)
                    {
                        if (sVacN == 0) { sModo = 0; continue; }
                        int w = sVac++; sVacN--;
                        float a = sL[w]; buffer[offset + hechos++] = a; Anillo1(a);
                        if (ch == 2)
                        {
                            float b = sR[w]; Anillo1(b);
                            if (hechos < count) buffer[offset + hechos++] = b; else { sOtra = b; sMedia = true; }
                        }
                        continue;
                    }
                    if (sT >= sSalen)
                    {
                        if (sSalen > 0 && subMezcla == 0.0 && !subOn) { Sale(); continue; }
                        if (!SubBloque()) { if (!sFinFiltra && sAcabo) sFinFiltra = subMezcla != 0.0; break; }
                        continue;
                    }
                    double meta = subOn ? 1.0 : 0.0, mz = subMezcla, ps = pasoSub, iu = 1.0 / nUnion;
                    int t = sT, fin = sSalen, nu = sUnionN, k0 = sK;
                    float[] xl = sL, xr = sR, lr = sLr, li = sLi, rr = sRr, ri = sRi;
                    double el = sEl, er = sEr;
                    while (t < fin && hechos < count)
                    {
                        int w = k0 + t;
                        if (mz != meta) mz = mz < meta ? Math.Min(meta, mz + ps) : Math.Max(meta, mz - ps);
                        float a, b = 0f;
                        if (mz == 0.0) { a = xl[w]; if (ch == 2) b = xr[w]; }
                        else
                        {
                            bool par = (w & 1) == 0; int v = w >> 1;
                            double ga = par ? lr[v] : li[v], gb = rr == null ? ga : (par ? rr[v] : ri[v]);
                            if (t < nu) { double u = 1.0 - t * iu; ga -= u * el; gb -= u * er; }   // el empalme
                            a = (float)(xl[w] - mz * ga); if (ch == 2) b = (float)(xr[w] - mz * gb);
                        }
                        buffer[offset + hechos++] = a;
                        if (ch == 2) { if (hechos < count) buffer[offset + hechos++] = b; else { sOtra = b; sMedia = true; } }
                        t++;
                    }
                    sT = t; subMezcla = mz;
                }
                return hechos;
            }

            // Lo que salio tal cual se guarda en el anillo (los ultimos sK cuadros), a mano: 'buffer' puede ser la vista flotante de un byte[].
            private void Anillo(float[] b, int o, int n)
            {
                float[] r = sAnillo; int a = sA, len = r.Length;
                for (int i = 0; i < n; i++) { r[a] = b[o + i]; if (++a == len) a = 0; }
                sA = a;
            }
            private void Anillo1(float v) { sAnillo[sA] = v; if (++sA == sAnillo.Length) sA = 0; }

            // Encenderlo: la ventana empieza con lo ultimo que salio, y el primer bloque lee por delante.
            private void Entra()
            {
                float[] r = sAnillo; int a = sA, len = r.Length, ch = sCh;
                for (int f = 0; f < sK; f++) { sL[f] = r[a]; if (ch == 2) sR[f] = r[a + 1]; a += ch; if (a == len) a = 0; }
                sModo = 1; sPrimero = true; sLeidos = 0; sEmitidos = 0; sT = 0; sSalen = 0;
            }

            // Apagado del todo, al acabar un bloque: lo leido por delante sale tal cual y el anillo vuelve a llevar lo ultimo que salio.
            private void Sale()
            {
                int s0 = sSalen, ch = sCh, a = 0;
                for (int f = 0; f < sK; f++) { sAnillo[a] = sL[s0 + f]; if (ch == 2) sAnillo[a + 1] = sR[s0 + f]; a += ch; }
                sA = 0;
                sVac = sK + s0; sVacN = (int)Math.Max(0L, Math.Min((long)sK, sLeidos - (sEmitidos + s0)));
                sModo = 2; sT = 0; sSalen = 0;
            }

            // UN BLOQUE DEL SUBGRAVE: la ventana avanza, se lee lo que falta (el primero, sB + sK cuadros) y sale de la FFT lo que se quita a
            // toda la ventana. Al final de la cancion lo que falta son ceros y sale solo lo que se leyo.
            private bool SubBloque()
            {
                int K = sK, B = sB, ch = sCh;
                if (!sPrimero && sSalen > 0)
                {
                    sEmitidos += sSalen; sSalen = 0; sT = 0;
                    Array.Copy(sL, B, sL, 0, 2 * K); if (ch == 2) Array.Copy(sR, B, sR, 0, 2 * K);
                }
                if (sAcabo && sEmitidos >= sLeidos) return false;
                int desde = sPrimero ? K : 2 * K, quiere = sPrimero ? B + K : B;
                int got = sAcabo ? 0 : SubLeeDelInterior(desde, quiere);
                for (int f = got; f < quiere; f++) { sL[desde + f] = 0f; if (ch == 2) sR[desde + f] = 0f; }
                sPrimero = false;
                long p = sEmitidos;
                sLeidos += got;
                if (sAcabo && !sFinHecho)
                {   // los ultimos sK cuadros de la cancion, por si la siguiente del mismo disco sigue sin fundido
                    sFinHecho = true;
                    for (int k = 0, a = 0; k < K; k++, a += ch)
                    {
                        int w = (int)(sLeidos - K + k - p + K);
                        sAnillo[a] = sL[w]; if (ch == 2) sAnillo[a + 1] = sR[w];
                    }
                    sA = 0;
                }
                int salen = (int)Math.Min((long)B, sLeidos - p);
                if (salen <= 0) return false;
                sUnionN = 0;
                if (sUnion)
                {   // tras la pista anterior del mismo disco, que acabo filtrada sin conocer esta: primero lo de solo ella, para el empalme
                    sUnion = false; sUnionN = Math.Min(nUnion, salen);
                    bool par = (K & 1) == 0; int v = K >> 1;   // el primer cuadro, en K
                    LoQueSeQuita(K);
                    double l0 = par ? sLr[v] : sLi[v], r0 = sRr == null ? l0 : (par ? sRr[v] : sRi[v]);
                    LoQueSeQuita(sM);
                    sEl = (par ? sLr[v] : sLi[v]) - l0; sEr = sRr == null ? sEl : (par ? sRr[v] : sRi[v]) - r0;
                }
                else LoQueSeQuita(sM);
                sT = 0; sSalen = salen;
                return true;
            }

            // Lee del interior 'quiere' cuadros enteros a la ventana desde 'desde', por trozos que caben en sLr (aun libre).
            private int SubLeeDelInterior(int desde, int quiere)
            {
                int ch = sCh, hechos = 0, cabe = sLr.Length / ch;
                float[] t = sLr;
                while (hechos < quiere && !sAcabo)
                {
                    int pide = Math.Min(quiere - hechos, cabe) * ch, tiene = 0;
                    while (tiene < pide) { int r = Interior(t, tiene, pide - tiene); if (r <= 0) { sAcabo = true; break; } tiene += r; }
                    int f = tiene / ch, d = desde + hechos;
                    if (ch == 2) for (int i = 0, k = 0; i < f; i++, k += 2) { sL[d + i] = t[k]; sR[d + i] = t[k + 1]; }
                    else for (int i = 0; i < f; i++) sL[d + i] = t[i];
                    hechos += f;
                }
                return hechos;
            }

            // Lo que se quita a la ventana (desde 'hasta', ceros): al medio (con cruce), o a cada canal por su lado.
            private void LoQueSeQuita(int hasta)
            {
                if (sD > 1)
                {
                    if (sMedio) { GraveDeDiezmado(sL, sR, hasta, sLr, sLi); return; }
                    GraveDeDiezmado(sL, null, hasta, sLr, sLi);
                    if (sR != null) GraveDeDiezmado(sR, null, hasta, sRr, sRi);
                    return;
                }
                if (sMedio) { GraveDe(sL, sR, hasta, sLr, sLi); return; }
                GraveDe(sL, null, hasta, sLr, sLi);
                if (sR != null) GraveDe(sR, null, hasta, sRr, sRi);
            }
            // El paso-bajo de la diezma (y de la vuelta): seno cardinal con el corte en la mitad de la tasa diezmada y ventana de Kaiser de
            // 2 sLg + 1, con suma 1 (en la vuelta se multiplica por sD: cada fase suma 1/sD). Se guarda el ultimo.
            private static readonly object diezmaCerrojo = new object();
            private static int diezmaD, diezmaL; private static float[] diezmaG;
            private static float[] PasoBajoDiezma(int D, int L)
            {
                lock (diezmaCerrojo) if (diezmaG != null && diezmaD == D && diezmaL == L) return diezmaG;
                double beta = 0.1102 * (DiezmaAtenuacionDb - 8.7), i0b = BesselI0(beta), fc = 0.5 / D, suma = 0.0;
                double[] h = new double[2 * L + 1];
                for (int j = 0; j <= 2 * L; j++)
                {
                    int i = j - L; double x = 2.0 * fc * i, u = (double)i / L;
                    double sinc = i == 0 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
                    h[j] = sinc * BesselI0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - u * u))) / i0b;
                    suma += h[j];
                }
                float[] g = new float[2 * L + 1];
                for (int j = 0; j <= 2 * L; j++) { double v = h[j] / suma; g[j] = (v < 1e-12 && v > -1e-12) ? 0f : (float)v; }   // los ceros del seno cardinal, ceros de verdad
                lock (diezmaCerrojo) { diezmaD = D; diezmaL = L; diezmaG = g; }
                return g;
            }
            // LO QUE SE QUITA, A MENOS RESOLUCION: la ventana (o el medio de dos) se diezma con el paso-bajo en los puntos sC0 + sD j (siempre
            // los mismos frente al principio del bloque: sB y sK son multiplos de sD), se filtra a la tasa diezmada con la FFT de sMd y se
            // vuelve a la tasa entera en los cuadros del bloque [sK, sK + sB). Queda donde lo deja GraveDe: zr (pares) y zi (impares). Desde
            // 'hasta' la ventana cuenta como ceros (el empalme). Con lo mono, el medio sale igual que cada canal: la misma cuenta.
            private void GraveDeDiezmado(float[] x, float[] y, int hasta, float[] zr, float[] zi)
            {
                int D = sD, L = sLg, c0 = sC0, nd = sNd, md = sMd;
                float[] g = sG, H = sHd, ar = sDr, ai = sDi;
                for (int j = 0, c = c0; j < nd; j++, c += D)
                {
                    int i0 = c - L, i1 = Math.Min(c + L, hasta - 1);
                    double acc = 0.0;
                    if (y == null) for (int i = i0; i <= i1; i++) acc += g[i - i0] * x[i];
                    else for (int i = i0; i <= i1; i++) acc += g[i - i0] * (0.5f * (x[i] + y[i]));
                    ar[j] = (float)acc; ai[j] = 0f;
                }
                for (int j = nd; j < md; j++) { ar[j] = 0f; ai[j] = 0f; }
                Fft(ar, ai, sPlanD, false);
                int h = md >> 1;
                for (int k = 0; k < md; k++) { float q = H[k <= h ? k : md - k]; ar[k] *= q; ai[k] *= q; }
                Fft(ar, ai, sPlanD, true);
                int desde = sK, hastaW = sK + sB;
                for (int w = desde; w < hastaW; w++)
                {
                    int jlo = (w - L - c0 + D - 1) / D, jhi = (w + L - c0) / D;
                    double acc = 0.0;
                    for (int j = jlo, c = c0 + jlo * D; j <= jhi; j++, c += D) acc += ar[j] * g[w - c + L];
                    float v = (float)(acc * D);
                    if (v < 1e-30f && v > -1e-30f) v = 0f;
                    if ((w & 1) == 0) zr[w >> 1] = v; else zi[w >> 1] = v;
                }
            }
            // Lo de un canal (x) o del medio de dos (x + y)/2, siempre con las mismas cuentas: si los dos canales traen lo mismo (lo mono),
            // salen iguales bit a bit. Como el grave del lado (GraveDelLado): las M muestras van de dos en dos a una FFT compleja de M/2, se
            // separan sus dos mitades, se pasan por el filtro (real: fase cero) y se vuelven a juntar para la inversa. Queda en zr (cuadros
            // pares) y zi (impares).
            private void GraveDe(float[] x, float[] y, int hasta, float[] zr, float[] zi)
            {
                int h = sM >> 1;
                float[] H = sH, wc = sPlanTodo.Cos, ws = sPlanTodo.Sin;
                for (int n = 0, j = 0; n < h; n++, j += 2)
                {
                    float s0 = j < hasta ? (y == null ? x[j] : 0.5f * (x[j] + y[j])) : 0f;
                    float s1 = j + 1 < hasta ? (y == null ? x[j + 1] : 0.5f * (x[j + 1] + y[j + 1])) : 0f;
                    if (s0 < 1e-30f && s0 > -1e-30f) s0 = 0f;
                    if (s1 < 1e-30f && s1 > -1e-30f) s1 = 0f;
                    zr[n] = s0; zi[n] = s1;
                }
                Fft(zr, zi, sPlan, false);
                {
                    double x0 = (zr[0] + zi[0]) * (double)H[0], xh = (zr[0] - zi[0]) * (double)H[h];
                    zr[0] = (float)(0.5 * (x0 + xh)); zi[0] = (float)(0.5 * (x0 - xh));
                }
                for (int k = 1; k <= h / 2; k++)
                {
                    int j = h - k;
                    double akr = zr[k], aki = zi[k], ajr = zr[j], aji = zi[j];
                    double ekr = 0.5 * (akr + ajr), eki = 0.5 * (aki - aji), okr = 0.5 * (aki + aji), oki = -0.5 * (akr - ajr);
                    double ejr = 0.5 * (ajr + akr), eji = 0.5 * (aji - aki), ojr = 0.5 * (aji + aki), oji = -0.5 * (ajr - akr);
                    double ck = wc[k], sk = ws[k], cj = wc[j], sj = ws[j], gk = H[k], gj = H[j];
                    double ykr = (ekr + (okr * ck + oki * sk)) * gk, yki = (eki + (oki * ck - okr * sk)) * gk;
                    double yjr = (ejr + (ojr * cj + oji * sj)) * gj, yji = (eji + (oji * cj - ojr * sj)) * gj;
                    double e1r = 0.5 * (ykr + yjr), e1i = 0.5 * (yki - yji), d1r = 0.5 * (ykr - yjr), d1i = 0.5 * (yki + yji);
                    double o1r = d1r * ck - d1i * sk, o1i = d1r * sk + d1i * ck;
                    double e2r = 0.5 * (yjr + ykr), e2i = 0.5 * (yji - yki), d2r = 0.5 * (yjr - ykr), d2i = 0.5 * (yji + yki);
                    double o2r = d2r * cj - d2i * sj, o2i = d2r * sj + d2i * cj;
                    zr[k] = (float)(e1r - o1i); zi[k] = (float)(e1i + o1r);
                    zr[j] = (float)(e2r - o2i); zi[j] = (float)(e2i + o2r);
                }
                Fft(zr, zi, sPlan, true);
            }
        }
    }
}
