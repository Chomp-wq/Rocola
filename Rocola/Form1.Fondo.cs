// EL FONDO: el cielo, las nebulosas, la luna, las fugaces y el aire
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
        // ================ LOS EFECTOS QUE SE PUEDEN BAJAR (Ctrl+F3 -> Imagen y rendimiento) ================
        // Cuatro cosas del cielo que cuestan y se pueden quitar cuando un equipo no da para todo. De fabrica
        // estan todas puestas y la rocola se ve EXACTAMENTE como se veia antes de que existieran: solo al
        // bajarlas cambia algo. Las decide el operador, o se las propone el medidor de la misma ventana con
        // lo que haya MEDIDO en ese equipo -nunca con una tabla de "equipos buenos y malos" escrita a mano-.
        // Van por NIVEL -alto, medio o bajo-, no por si/no. Entre "nieblas si" y "nieblas no" hay sitio para
        // "menos nieblas", y ese sitio es media rocola que no hay que afear para ganar los mismos cuadros.
        // Con todo en ALTO (lo de fabrica) la rocola se ve EXACTAMENTE igual que antes de que esto existiera.
        internal const int NivAlto = 2, NivMedio = 1, NivBajo = 0;
        private float estrellasDens = 1f;   // "estrellas=" en config: cuantas estrellas, de 10 a 100 (%)
        private int fugacesNiv = NivAlto;   // "fugaces=" : 2 dos cada 7-16 s;  1 una cada 15-30 s;  0 ninguna
        private int nieblasNiv = NivAlto;   // "nieblas=" : 2 nebulosa + velo;  1 solo la nebulosa;  0 nada
        private int relevoNiv = NivAlto;    // "patron="  : 2 patron nuevo cada minuto;  1 cada tres;  0 nunca
        private int animNiv = NivAlto;      // "animacion=": el zoom al entrar/salir de un disco (ver TransMs)
        // ALIGERAR EL HOJEO ("hojeo_ligero="). Mientras la pagina se desliza, las casillas van a media
        // resolucion -la cuarta parte de bytes a la tarjeta- y no se rehacen porque llegue su portada.
        // Encendido de fabrica: no se ve, porque solo pasa mientras la pagina esta en movimiento.
        private bool hojeoLigero = true;
        private bool progresoNeon = true; // Ctrl+F3: tubo luminoso, sin temporizador adicional.
        private bool muroReposo = true;
        private bool nebulosasCine = true; // Solo GPU; no altera los colores del disco ni del genero.
        private bool vinetaSuave = true;
        private bool haloSeleccion = true;
        private bool estrellasFinas = true;
        private bool ondasSuaves = true;
        private bool progresoFluido = true;
        private bool estadoExpandido = true;
        private bool neonIntenso = true;
        private bool estadoTransparente = true;
        private bool cancionesTransparentes = true;
        private bool cancionesCristal = true;
        private bool colaDoce = true;
        private bool glassCapas = true, coloresCache = true, lunaActiva = true;
        private bool controlesReposo = true, ondasUnidas = true;
        private bool lunaRealista = true, haloLunar = true, polvoLunar = true, lunaProtagonista = true;
        private bool respuestaInmediata = true, transicionesTacto = true, cachePortadasGpu = true;
        private bool buscadorCache = true;
        private bool cieloProfundo = true;
        // CUANTO SE NOTA EL DETALLE NATURAL. El mapa de nubes cubre casi toda la pantalla, y a fuerza
        // entera la bruma ancha tapa el cielo en vez de adornarlo. Bajando esto se va la bruma y quedan
        // los picos, que es donde esta la forma de la nube: sale MAS definida, no solo mas oscura. El
        // tope del alfa NO se toca a proposito, justo por eso.
        //
        // Y es un mando aparte de Form1.NebFuerza, que es const y la comparten las dos paredes -la de
        // tarjeta grafica y la horneada- para que la maquina del bar y la de repuesto pinten igual. Esto
        // es solo del detalle natural, que es solo de tarjeta grafica.
        internal const float NebNaturalDef = 0.55f;
        internal static float nebNaturalFuerza = NebNaturalDef;   // "cielo_profundo_fuerza=" en config, 0..1
        internal static float FuerzaNatural(string txt)
        {
            int v;
            if (!int.TryParse((txt ?? "").Trim(), System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out v)) return NebNaturalDef;
            if (v < 0) v = 0; else if (v > 100) v = 100;
            return v / 100f;
        }
        private bool auroraBandas = true;
        private float muroAlpha = 1f;
        internal static float PasoMuro(float actual, bool reposo)
        {
            float destino = reposo ? 0.10f : 1f;
            float valor = actual + (destino - actual) * (reposo ? 0.08f : 0.5f);
            return Math.Abs(valor - destino) < 0.005f ? destino : valor;
        }
        // PRESENCIA de la aurora en pantalla, de 0 a 1. Antes se dibujaba o no se dibujaba segun si la banda
        // mas alta pasaba de un pelo: en un pasaje bajito (una intro, un silencio, el final de un tema) las
        // bandas caen a cero, la aurora DESAPARECIA de golpe y al volver la musica APARECIA de golpe. Eso era
        // el parpadeo. Ahora hay un mando suave: entra en algo mas de un segundo y se va en unos tres, asi que
        // ni aparece ni desaparece nunca de un cuadro para otro.
        //
        // Y NUNCA LLEGA A CERO. Sin musica la aurora no se apaga: se queda EN REPOSO, baja y tenue, con su
        // ondeo lento (el mismo AurOndaAlto de siempre, que es lo unico que la mueve cuando no hay bandas que
        // seguir) y respirando muy despacio, una vuelta cada medio minuto. Asi el cielo nunca se queda vacio:
        // ni en silencio, ni con un video puesto, ni en el modo atraccion.
        private const float AurReposo = 0.44f;       // cuanta aurora queda cuando no suena nada
        private const float AurReposoOnda = 0.20f;   // cuanto respira ese reposo (+-20%)
        private const float AurReposoPaso = 0.0088f; // radianes por vuelta de reloj: una respiracion cada ~30 s
#if ROCOLA_GPU
        private float auroraPres;
        private float auroraFase;                    // fase de esa respiracion (se lleva a mano: no depende del reloj del sistema)
#endif
        // ¿Hace falta analizar el espectro? Es el 90% del coste del analisis de audio, asi que cuando nadie
        // lo va a mirar (compilado sin GPU, o el usuario puso espectro=0) ni se calcula y el hilo de audio respira.
#if ROCOLA_GPU
        private bool EspectroActivo { get { return auroraAmount > 0.004f; } }
#else
        private bool EspectroActivo { get { return false; } }
#endif
#if ROCOLA_GPU
        private float audioLevel = 0f;                // nivel GLOBAL (max de las 3 bandas): lo pide la tarjeta grafica
#endif
        private int playFailStreak = 0;   // fallos seguidos al reproducir (para no ciclar si NAudio falla)
        // ---- NIVELADO DE VOLUMEN (que todas las canciones suenen PAREJO) ----
        // Cada cancion viene masterizada a distinto volumen. Aqui se mide la sonoridad (RMS integrado) de cada
        // cancion MIENTRAS suena (aprovechando la medicion que ya se hace para el beat) y se guarda por ruta en
        // rocola_volumen.txt. En reproducciones siguientes se aplica una ganancia para llevarla a un nivel objetivo
        // COMUN, con recorte suave de picos. Asi, tras oirse una vez, cada cancion suena al mismo volumen que las
        // demas. La PRIMERA vez que se oye una cancion nueva va a su volumen original (aun no hay medida): se
        // aprende en esa pasada y ya queda pareja para siempre. Se puede apagar con normalizar=0 en rocola_config.txt.
        private bool normalizeOn = true;                                                     // nivelado activo (por defecto si)
        private string curMeasurePath = null;                                                // cancion que se esta MIDIENDO ahora (null = no medir)
        private LevelTap curTap;                                                              // tap de la cancion actual (para cambiarle la ganancia en vivo desde Ctrl+F3)
        private double curSumSq = 0; private long curSamples = 0;                             // acumuladores del RMS integrado de la cancion actual (los llena el hilo de audio)
        // ----- EQ AUTOMATICO (correctivo y suave): cada cancion aprende su HUELLA TONAL (8 bandas anchas,
        // en dB relativos a su propia media) la primera vez que suena; desde la siguiente se corrige hacia
        // el promedio de la biblioteca, con tope de +-4 dB por banda y una perilla de fuerza en Ctrl+F3.
        // Corrige lo grosero (lodo en 200-400 Hz, canciones apagadas o chillonas) SIN uniformar el estilo. -----
        // 8 bandas centradas (biquads RBJ, el estandar de los EQ graficos: se portan bien hasta Nyquist,
        // donde los filtros simples de un polo se desmoronan). Medir usa BANDPASS de 0 dB con Q=2 (campana
        // angosta = poco sangrado entre bandas); corregir usa PEAKING con Q=1.1 (campana musical, ancha).
        internal static readonly float[] EqCentros = { 50f, 158f, 400f, 1000f, 2500f, 5600f, 10000f, 15800f };
        internal static float[] EqBpf(float fc, int rate)
        {
            double w = 2.0 * Math.PI * fc / Math.Max(8000, rate);
            if (w >= Math.PI * 0.95) return null;                       // el centro no cabe en este samplerate
            double al = Math.Sin(w) / 4.0, cw = Math.Cos(w), a0 = 1.0 + al;   // Q = 2
            return new float[] { (float)(al / a0), 0f, (float)(-al / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al) / a0) };
        }
        internal static float[] EqPeak(float fc, int rate, float dB)
        {
            double w = 2.0 * Math.PI * fc / Math.Max(8000, rate);
            if (w >= Math.PI * 0.95 || (dB > -0.1f && dB < 0.1f)) return null;   // sin correccion = ni se crea (identidad exacta)
            double A = Math.Pow(10.0, dB / 40.0), al = Math.Sin(w) / 2.2, cw = Math.Cos(w);   // Q = 1.1
            double a0 = 1.0 + al / A;
            return new float[] { (float)((1.0 + al * A) / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al * A) / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al / A) / a0) };
        }
        // Biquads del K-weighting (BS.1770 / LUFS): un high-shelf que realza agudos (+4 dB) y un high-pass
        // que quita sub-graves. Con ellos, la sonoridad se mide como la OYE el humano. RBJ, a0-normalizado.
        internal static float[] EqShelfHi(float f0, int rate, float dB, float Q)
        {
            double w = 2.0 * Math.PI * f0 / Math.Max(8000, rate);
            double A = Math.Pow(10.0, dB / 40.0), cw = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), tsa = 2.0 * Math.Sqrt(A) * al;
            double a0 = (A + 1) - (A - 1) * cw + tsa;
            return new float[] {
                (float)(A * ((A + 1) + (A - 1) * cw + tsa) / a0),
                (float)(-2.0 * A * ((A - 1) + (A + 1) * cw) / a0),
                (float)(A * ((A + 1) + (A - 1) * cw - tsa) / a0),
                (float)(2.0 * ((A - 1) - (A + 1) * cw) / a0),
                (float)(((A + 1) - (A - 1) * cw - tsa) / a0) };
        }
        internal static float[] EqHighPass(float f0, int rate, float Q)
        {
            double w = 2.0 * Math.PI * f0 / Math.Max(8000, rate);
            double cw = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), a0 = 1.0 + al;
            return new float[] {
                (float)(((1 + cw) / 2.0) / a0), (float)((-(1 + cw)) / a0), (float)(((1 + cw) / 2.0) / a0),
                (float)((-2.0 * cw) / a0), (float)((1 - al) / a0) };
        }
        private float[] kwCoef1, kwCoef2; private float[] kwSt; private int kwRate = 0;   // K-weighting del nivelado (2 biquads/canal, 8 estados)
        // FICHA de cada cancion: 8 bandas de huella tonal en dB (relativas a la mediana de su cuerpo) y tres
        // datos de CALIDAD que salen del mismo analisis y antes se tiraban:
        //   [8] corte de ARRIBA (Hz): donde el codec le talo los agudos. Distingue una cancion sana de una
        //       dañada; decide si sirve para fijar el objetivo del EQ y que bandas tiene sentido corregirle.
        //   [9] RECORTE (0..1): que parte de la cancion viene con los picos aplastados desde la masterizacion.
        //   [10] corte de ABAJO (Hz): donde empieza a haber grave de verdad. Una grabacion vieja cortada en
        //       80 Hz necesita mas ayuda del bajo psicoacustico que una que baja a 30.
        // 0 = todavia no se sabe. Ninguno cuesta un analisis aparte: salen del recorrido que ya se hacia.
        private const int EqLen = 11, EqCut = 8, EqClip = 9, EqCutLo = 10;
        private const long EqMinFrames = 882000;   // ~20 s de musica: con menos, la huella no es confiable
        // "SUBIR EL PISO": las canciones BAJITAS se SUBEN hacia un volumen objetivo; las que ya suenan FUERTE
        // NO se bajan (se prioriza subir las flojas, como pidio el operador). Ajustable desde la interfaz (Ctrl+F3).
        //   ganancia = (objetivo / rms) ^ fuerza , recortada a [1 , tope]   (fuerza 0..1; nunca por debajo de 1 = no baja)
        private float normTarget = 0.26f;                                                     // volumen OBJETIVO (RMS lineal) al que se suben las bajitas; mas alto = todo mas fuerte
        private float normStrength = 1.0f;                                                    // FUERZA del nivelado 0..1 (cuanto se acerca al objetivo; 0 = no toca nada)
        // REALCE DE AGUDOS (estilo DSEE de Sony, en su version honesta): el MP3 recorta el "aire" (agudos
        // altos) y eso ya NO se puede recuperar; lo que SI se puede es re-inventarlo, que es lo que hacen el
        // propio DSEE y los EXCITERS ARMONICOS clasicos (las cajas Aphex/BBE de los equipos de bar): tomar la
        // banda alta que sobrevivio (~5.5 kHz+), saturarla SUAVE (eso genera armonicos nuevos mas arriba) y
        // mezclar una pizca de vuelta. Se aplica en LevelTap tras nivelar; perilla en Ctrl+F3; "realce=" en config.
        private float excAmount = 0.35f;                                                      // intensidad del realce 0..1 (0 = apagado); 0.35 = sutil
        private DeepBassProvider curBass;   // el de la cancion que suena (para mover la perilla en vivo)
        private float masterDb = 0f;         // SUBIR TODO: dB extra para ABSOLUTAMENTE todas las canciones (0..12; barra en Ctrl+F3, "subir_db=" en config)
        private float MasterLin() { return (float)Math.Pow(10.0, masterDb / 20.0); }   // dB -> ganancia lineal
        private static bool relevoOn = true;         // "relevo=" en config
        // LA AURORA SE CALMA MAS QUE LAS ESTRELLAS. Es lo mas grande y lo que mas se mueve de toda la pantalla,
        // asi que mientras alguien esta intentando LEER una lista de canciones es justo lo que mas estorba -mas
        // todavia a las horas en que un cliente ya lleva unas cuantas encima-. Las estrellas bajan al 45%; la
        // aurora bajaba solo al 65%, que casi no se notaba. Ahora baja al 35% Y ademas se DIFUMINA: su filo de
        // neon y la raya del horizonte, que es lo afilado y lo que tira de la vista, se van casi del todo y
        // queda una mancha de color suave. En reposo vuelve entera, con el mismo fundido de siempre.
        private const float AurNavLevel = 0.35f;    // cuanto queda del relleno mientras se navega
        private const float AurNavFilo = 0.22f;     // y cuanto queda del filo de neon (mucho menos: eso es difuminar)
        // LA LUNA. Encendida de fabrica. Es un adorno de gusto -no una mejora que a todo el mundo le sirva-
        // y por eso se puede apagar, como los otros seis de la pestana DETALLES.
        private static bool lunaOn = true;           // "luna=" en config

        private Color bgAccent = DefaultAccent;

        // Cielo del fondo: casi negro con un toque del color del genero (arranca oscuro como el modo
        // atraccion, pero cambia de tinte segun lo que suena).
        // EL COLOR DE LAS NEBULOSAS: el del genero llevado a medias con un violeta azulado. El del genero a
        // secas es demasiado terroso para una nebulosa; mezclado, se sigue notando de que color va la cancion
        // pero parece cielo. La misma cuenta que hace la version por CPU.
        private static Color ColorNebulosa(Color accent)
        {
            return Color.FromArgb(255, (accent.R + 140) / 2, (accent.G + 120) / 2, (accent.B + 255) / 2);
        }

        // ================== LAS DOS NEBULOSAS: EL GENERO POR UN LADO, LA CARATULA POR OTRO ==================
        //
        // La capa de nebulosas es la unica cosa grande de la pantalla donde los 33 colores de los generos
        // llegan enteros: medido, 33 de 33 se distinguen ahi, contra 10 de 33 en el medio del cielo y 4 de 33
        // arriba. Y estaba QUIETA, y de un solo color.
        //
        // Ahora son dos, y se dibujan una encima de otra desplazandose despacio en sentidos contrarios: una
        // lleva el color del GENERO y la otra el de la CARATULA. Donde se encima una con otra sale la mezcla
        // de las dos, y como las dos se mueven, la mezcla se mueve. Los dos colores se ven por separado y
        // juntos a la vez.
        //
        // LO QUE LA CARATULA NO DECIDE NUNCA: el brillo ni la viveza. De ella se toma SOLO de que color es
        // (ver TinteConPortada). Por eso ninguna portada puede estropear la pantalla -ni una casi blanca
        // lavandola, ni una negra apagandola, ni una de neon quemandola- y no hay ningun tope que se pueda
        // quedar corto: sale de como esta hecho.
        private const float NebPaso = 0.00654f;        // radianes por cuadro: a 24 cuadros/s, una vuelta cada ~40 s
        // Y LA VIA LACTEA TAMBIEN SE MUEVE, PERO MUCHO MAS DESPACIO QUE NADA.
        //
        // Era lo unico clavado en un cielo donde todo lo demas se desliza: las estrellas van a la deriva,
        // las dos nebulosas se cruzan en sentidos contrarios, y la banda se quedaba pegada a la esquina.
        // Este archivo ya dice, hablando del polvo, que una parte quieta mientras el resto se mueve "se ve
        // peor que si no se moviera nada"; aqui habia quedado el mismo caso sin arreglar.
        //
        // Una vuelta cada TRES MINUTOS, contra los cuarenta segundos de las nebulosas: es lo que esta mas
        // lejos, y lo que esta mas lejos se mueve menos. Es la misma idea que la deriva por tamano de las
        // estrellas, llevada hasta el fondo del todo.
        private const float HazPaso = 0.001454f;       // radianes por cuadro: a 24 cuadros/s, una vuelta cada ~3 min
        private float hazFase = 0f;

        // ===================== EL RUIDO QUE DESHACE LAS FRANJAS DEL CIELO =====================
        //
        // EL CIELO SE PINTABA EN UNAS TREINTA FRANJAS PLANAS, y eso es lo que lo hacia parecer un degradado
        // de computadora en vez de aire. La cuenta es facil de ver: el degradado va de tres paradas y cada
        // canal recorre muy poco camino. Sin musica y en una pantalla de 1080 de alto, el rojo va de 3 a 16
        // y de 16 a 37, o sea que cambia UN NIVEL DE 255 CADA TREINTA PIXELES. Con un genero que tire a azul
        // llega a un nivel cada setenta.
        //
        // Y el ojo es justo bueno para eso: un canto recto entre dos tonos casi iguales se ve muchisimo mas
        // de lo que la diferencia deberia dejar (se llama banda de Mach, y es cosa del ojo, no de la
        // pantalla). Por eso no basta con que el degradado sea "correcto": SALE bien y SE VE mal.
        //
        // Lo que lo deshace es ruido de un nivel. No tapa el escalon -sigue estando- sino que le quita la
        // linea recta, y sin linea recta el ojo ya no lo engancha. Es lo mismo que hacen los filtros de video
        // cuando le echan grano a un cielo.
        //
        // COMO SE APLICA, y por que asi: encima va un dibujito de 64x64 repetido, mitad puntos blancos y
        // mitad negros, los dos con alfa 2 de 255. Sobre un cielo oscuro el blanco sube dos niveles y el
        // negro no baja casi ninguno, o sea que en la practica es "sube dos o no subas": ruido de mas o menos
        // un nivel alrededor de una subida de uno, que no se ve. Se probo hacerlo pixel a pixel con la cuenta
        // exacta y son dos copias de ocho megas mas el recorrido, contra UN relleno: no compensa por un nivel.
        internal const int RuidoLado = 64;
        internal const int RuidoAlfa = 2;    // de 255: sobre este cielo son dos niveles arriba o ninguno
        internal static Bitmap BakeRuido()
        {
            Bitmap b = new Bitmap(RuidoLado, RuidoLado, PixelFormat.Format32bppArgb);
            Random r = new Random(20260903);   // fijo: el mismo grano siempre, no uno distinto por arranque
            Color cl = Color.FromArgb(RuidoAlfa, 255, 255, 255), os = Color.FromArgb(RuidoAlfa, 0, 0, 0);
            for (int y = 0; y < RuidoLado; y++)
                for (int x = 0; x < RuidoLado; x++)
                    b.SetPixel(x, y, (r.Next(2) == 0) ? cl : os);
            return b;
        }

        // EL MISMO GRANO, EN GRANDE, PARA LA PARED DE TARJETA GRAFICA. Alli no se puede repetir un dibujito
        // al vuelo -las texturas van en modo "pegado al borde", no en modo repetir- asi que hay que subirle
        // uno ya grande y estamparlo en unos pocos trozos. Se arma repitiendo el de 64 con UN relleno, no
        // pixel a pixel: un millon de pixeles de uno en uno tardaria segundos al arrancar.
        internal static Bitmap BakeRuidoGrande(int lado)
        {
            Bitmap chico = BakeRuido();
            Bitmap b = new Bitmap(lado, lado, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            using (TextureBrush tb = new TextureBrush(chico, WrapMode.Tile))
            {
                g.CompositingMode = CompositingMode.SourceCopy;   // se COPIA el grano con su alfa, no se mezcla
                g.FillRectangle(tb, 0, 0, lado, lado);
            }
            chico.Dispose();
            return b;
        }

        // ===================== LA LUNA =====================
        //
        // No habia ninguna, y es lo unico que un cielo de noche tiene y este no tenia.
        //
        // Va BAJA y FUERA DEL CENTRO a proposito: en el centro le pelearia a la pared de discos, que es lo
        // que la gente esta mirando. Abajo a un lado acompana el resplandor del genero en vez de competirle.
        //
        // Y SE MUEVE CON EL CIELO, no aparte: lleva el mismo reloj lento que la via lactea, o sea una vuelta
        // cada tres minutos y sin salirse nunca de su sitio. Se penso en darle la deriva de las estrellas
        // -que es lo que hace una luna de verdad- y no vale: esa deriva DA LA VUELTA por el borde, y una
        // estrella que reaparece al otro lado no la nota nadie porque es un punto, pero una luna saltando de
        // un canto al otro cada minuto y medio se ve, y se ve mal.
        //
        // El dibujo se hornea UNA vez -son unos 180 pixeles de lado- y luego es un pegado chico por cuadro,
        // no una pasada de pantalla. Por eso esta es la unica de las mejoras de esta tanda que cabe TAMBIEN
        // en la pared sin tarjeta grafica.
        // Los sitios y los tamanos son internos porque las DOS paredes tienen que poner la luna en el mismo
        // sitio y del mismo tamano, igual que pasa con la curva del aire y con el corte del cielo.
        internal const float LunaX = 0.795f;      // donde se pone, en parte del ancho
        internal const float LunaY = 0.605f;      // ... y del alto: abajo, donde ya hay resplandor
        private const float LunaRadio = 0.026f;   // radio del disco, en parte del ALTO de la pantalla
        private const float LunaHalo = 3.2f;      // hasta donde llega el resplandor, en radios del disco
        private const float LunaMengua = 0.42f;   // cuanto se la come la sombra (0 = llena; 1 = no queda nada)
        internal const float LunaMenea = 0.030f;  // cuanto se pasea, en parte de pantalla (el mismo reloj que la via lactea)

        // El dibujo de la luna al tamano que le toca en esta pantalla. Devuelve un cuadro con el halo, el
        // disco, sus manchas y la sombra de la fase ya recortada.
        internal static Bitmap BakeLuna(int altoPantalla)
        {
            float rad = altoPantalla * LunaRadio; if (rad < 6f) rad = 6f;
            int lado = (int)(rad * LunaHalo * 2f) | 1;   // impar: asi tiene un pixel de centro exacto
            Bitmap b = new Bitmap(lado, lado, PixelFormat.Format32bppArgb);
            float c = lado / 2f;
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                // EL HALO. Una luna sin halo es un circulo pegado encima; con el, el aire de alrededor se ve
                // encendido y parece que la luz sale de ella. Va en dos tramos para que caiga en curva y no
                // en linea recta, que es lo que lo delata como un degradado.
                float rh = lado / 2f;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(c - rh, c - rh, rh * 2, rh * 2);
                    using (PathGradientBrush pb = new PathGradientBrush(gp))
                    {
                        pb.CenterPoint = new PointF(c, c);
                        pb.CenterColor = Color.FromArgb(96, 226, 232, 246);
                        pb.SurroundColors = new Color[] { Color.FromArgb(0, 226, 232, 246) };
                        Blend bl = new Blend(4);
                        bl.Factors = new float[] { 0f, 0.06f, 0.30f, 1f };
                        bl.Positions = new float[] { 0f, 0.42f, 0.72f, 1f };
                        pb.Blend = bl;
                        g.FillEllipse(pb, c - rh, c - rh, rh * 2, rh * 2);
                    }
                }
                // EL DISCO, aparte, para poder recortarle la fase sin llevarse el halo por delante.
                int ld = (int)(rad * 2f) + 4;
                using (Bitmap disco = new Bitmap(ld, ld, PixelFormat.Format32bppArgb))
                {
                    float cd = ld / 2f;
                    using (Graphics dg = Graphics.FromImage(disco))
                    {
                        dg.SmoothingMode = SmoothingMode.AntiAlias;
                        dg.Clear(Color.Transparent);
                        // Blanco con un dejo calido, y un poco mas apagada por el canto: una esfera iluminada
                        // de lado no es un circulo de color liso.
                        using (GraphicsPath gp = new GraphicsPath())
                        {
                            gp.AddEllipse(cd - rad, cd - rad, rad * 2, rad * 2);
                            using (PathGradientBrush pb = new PathGradientBrush(gp))
                            {
                                pb.CenterPoint = new PointF(cd - rad * 0.28f, cd - rad * 0.28f);
                                pb.CenterColor = Color.FromArgb(255, 252, 250, 240);
                                pb.SurroundColors = new Color[] { Color.FromArgb(255, 206, 206, 202) };
                                dg.FillEllipse(pb, cd - rad, cd - rad, rad * 2, rad * 2);
                            }
                        }
                        // LOS MARES. Son las manchas oscuras que se ven a simple vista, y son las que hacen
                        // que se lea "luna" y no "bola". Fijas, no al azar: la luna siempre ensena la misma cara.
                        float[] mx = { -0.30f, 0.12f, -0.05f, 0.34f, -0.42f };
                        float[] my = { -0.34f, -0.18f, 0.22f, 0.30f, 0.18f };
                        float[] mr = { 0.30f, 0.22f, 0.26f, 0.15f, 0.13f };
                        int[] ma = { 26, 20, 22, 15, 13 };
                        for (int i = 0; i < mx.Length; i++)
                            using (GraphicsPath gp = new GraphicsPath())
                            {
                                float r2 = rad * mr[i], px = cd + rad * mx[i], py = cd + rad * my[i];
                                gp.AddEllipse(px - r2, py - r2, r2 * 2, r2 * 2);
                                using (PathGradientBrush pb = new PathGradientBrush(gp))
                                {
                                    pb.CenterColor = Color.FromArgb(ma[i], 120, 124, 140);
                                    pb.SurroundColors = new Color[] { Color.FromArgb(0, 120, 124, 140) };
                                    dg.FillEllipse(pb, px - r2, py - r2, r2 * 2, r2 * 2);
                                }
                            }
                        // LA FASE: se BORRA un circulo corrido, no se pinta negro encima. Pintar negro dejaria
                        // una media luna negra sobre el cielo -que ahi no hay nada, hay cielo- y se veria.
                        if (LunaMengua > 0.01f)
                        {
                            CompositingMode ocm = dg.CompositingMode;
                            dg.CompositingMode = CompositingMode.SourceCopy;
                            using (SolidBrush nada = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                            {
                                float des = rad * (0.55f + 1.30f * LunaMengua);
                                dg.FillEllipse(nada, cd - des - rad, cd - rad * 1.02f, rad * 2.04f, rad * 2.04f);
                            }
                            dg.CompositingMode = ocm;
                        }
                    }
                    g.DrawImageUnscaled(disco, (int)(c - cd), (int)(c - cd));
                }
            }
            return b;
        }

        // EL TINTE DEL AIRE, para la luna. La misma curva que apaga y enrojece las estrellas de abajo
        // (AireDelCielo): una luna pegada al horizonte se apaga y se pone naranja, y es de lo que mas se
        // acuerda cualquiera que haya visto una. Devuelve los tres factores de 0 a 1.
        internal static void TinteDelAire(float y, float alto, out float fr, out float fg, out float fb)
        {
            float aire = AireDelCielo(y, alto);
            fr = 1f - AireApaga * aire;
            float c = AireCalor * aire;
            fg = fr * (1f - 0.35f * c);
            fb = fr * (1f - 0.75f * c);
        }
        // CUANTO SE TIENEN QUE NOTAR. El dibujo de las nebulosas esta hecho de manchas de alfa 4 y 7 de 255:
        // un velo suavisimo. A esa fuerza los dos colores estan y se cruzan, pero casi no se ven, asi que
        // todo esto no se notaria. Se dibujaron las cuatro fuerzas y se eligio mirando; esta es cuatro veces
        // la de antes: las nubes de color se leen claras y cada disco tine la pantalla de lo suyo, pero
        // siguen siendo nubes de fondo y no le pelean a las portadas ni al texto.
        //
        // Va aqui, fuera del trozo de tarjeta grafica, porque las dos paredes -la de tarjeta y la horneada-
        // tienen que usar la MISMA. Con dos numeros, el dia que alguien toque uno la maquina del bar y la de
        // repuesto pintarian fondos distintos y nadie se enteraria hasta verlas juntas.
        internal const float NebFuerza = 2.4f;
        private const float NebGiroSolo = 45f;         // grados que se le gira al genero cuando la portada no dice nada

        internal static void ColoresNebulosa(Color genero, Color port1, Color port2, float fase,
                                             out Color deGenero, out Color dePortada)
        {
            deGenero = ColorNebulosa(genero);
            Color cp;
            if (port1.A != 0 && port2.A != 0)
            {
                // LA PORTADA TIENE DOS COLORES: la nebulosa va cruzando de uno al otro, muy despacio. Este es
                // el caso que antes no pintaba nada -una portada media roja y media verde daba un promedio
                // que no se parecia a ninguna de las dos y la rocola se callaba-, y ahora es el que mas luce.
                float t = 0.5f + 0.5f * (float)Math.Sin(fase);
                cp = TinteConPortada(genero, MezclaDeTono(port1, port2, t));
            }
            else if (port1.A != 0) cp = TinteConPortada(genero, port1);
            else cp = CompaneroDelGenero(genero);   // sin portada que aporte, el genero se acompana solo
            dePortada = ColorNebulosa(cp);
        }

        // SIN PORTADA QUE APORTE -blanco y negro, o el interruptor de Ctrl+F3 apagado- las dos nebulosas
        // saldrian del mismo color y no habria mezcla que ver. Entonces el segundo sale del propio genero,
        // girandole el tono.
        //
        // Pero hay dos generos CASI GRISES a los que girarles el tono no les hace nada, y esta medido:
        // Hablado (viveza 0.04) se movia dos niveles de 255 y Vocal (0.25) poco mas. A esos se les mueve el
        // brillo y la temperatura, que es lo unico que un gris tiene.
        private static Color CompaneroDelGenero(Color genero)
        {
            int mx = Math.Max(genero.R, Math.Max(genero.G, genero.B));
            int mn = Math.Min(genero.R, Math.Min(genero.G, genero.B));
            float viveza = (mx <= 0) ? 0f : (mx - mn) / (float)mx;
            if (viveza >= TonoMinViveza) return GiraTono(genero, NebGiroSolo, 1f);
            // Gris: se le da un lado FRIO y un poco mas claro. Sobre un cielo azul oscuro eso si se lee.
            int r = genero.R * 82 / 100, g = genero.G * 92 / 100, b = genero.B * 125 / 100;
            if (b > 255) b = 255;
            return Color.FromArgb(255, r, g, b);
        }

        // Mezcla DE TONO entre dos colores, por el camino corto de la rueda. Mezclarlos numero a numero
        // llevaria al gris por en medio: entre un rojo y un verde pasaria por un marron.
        private static Color MezclaDeTono(Color a, Color b, float t)
        {
            float d = TonoDe(b) - TonoDe(a);
            while (d > 180f) d -= 360f; while (d < -180f) d += 360f;
            return GiraTono(a, d * t, 1f);
        }

        private static Color SkyColor(Color accent)
        {
            // ARRIBA del cielo: casi negro (apenas un dejo del genero). Se oscurece hacia el cenit.
            int r = 3 + accent.R / 22, g = 4 + accent.G / 22, b = 8 + accent.B / 18;
            if (r > 22) r = 22; if (g > 22) g = 22; if (b > 30) b = 30;
            return Color.FromArgb(255, r, g, b);
        }

        // Color INFERIOR del degradado del cielo: mas tenido al genero y un poco mas claro que el de arriba.
        private static Color SkyColor2(Color accent)
        {
            // ABAJO del cielo: resplandor de horizonte. Sin musica = azul default; con musica = el color del
            // genero normalizado a un brillo visible, con un leve piso azul para que siga sintiendose cielo.
            int mx = accent.R; if (accent.G > mx) mx = accent.G; if (accent.B > mx) mx = accent.B;
            int r, g, b;
            if (mx < 10) { r = 50; g = 88; b = 188; }
            else
            {
                r = accent.R * 165 / mx; g = accent.G * 165 / mx; b = accent.B * 165 / mx;
                r = (r * 3 + 50) / 4; g = (g * 3 + 88) / 4; b = (b * 3 + 188) / 4;
            }
            if (r > 205) r = 205; if (g > 205) g = 205; if (b > 220) b = 220;
            r = r * SkyDark / 100; g = g * SkyDark / 100; b = b * SkyDark / 100;   // ESTETICA: cielo un poco mas oscuro (el azul se veia muy brillante)
            return Color.FromArgb(255, r, g, b);
        }

        // Factor de OSCURIDAD del cielo (100 = como antes; menor = mas oscuro). Solo brillo, conserva el tinte.
        private const int SkyDark = 75;

        // ================== LAS TRES PERILLAS DEL FONDO ==================
        //
        // Hasta ahora los colores del cielo eran numeros fijos, y quien queria otra cosa no tenia como
        // pedirla. Estas tres son las que de verdad cambian como se ve la pared, y viven en la pestaña
        // "Colores del fondo" de Ctrl+F3.
        //
        // TODAS pasan por una funcion que las acota, igual que la fuerza de la respiracion: en el archivo de
        // configuracion se puede escribir cualquier cosa, y un disparate aqui no puede dejar la pantalla
        // inservible en un bar a las dos de la manana.
        //
        // Y viven AQUI, fuera del trozo de tarjeta grafica, porque las dos paredes -la de tarjeta y la
        // horneada- tienen que usar las mismas. Con dos juegos de numeros, la maquina del bar y la de
        // repuesto pintarian fondos distintos y nadie se enteraria hasta verlas juntas.

        // CUANTO MANDA LA PORTADA en el tono del fondo. 0 = manda el genero y la portada no pinta nada;
        // 1 = manda la portada entera, que es como estaba.
        //
        // AQUI ESTABA LA QUEJA: con la portada al mando entero, una cancion de Pop con una carátula azul
        // pintaba la pantalla AZUL, y el rosa del Pop no aparecia por ningun lado. De fabrica se queda a
        // MEDIAS: se reconoce el genero y se sigue notando que cada disco es distinto.
        private static float portadaManda = 0.5f;    // "portada_manda=" en config
        internal static float PesoPortada(string txt) { return LeePorCiento(txt, 0.5f); }

        // CUANTO AZUL lleva el medio del cielo. 1 = como estaba; 0 = el medio se queda casi negro y el color
        // del genero llega mucho mas arriba.
        private static float cieloAzul = 1f;         // "cielo_azul=" en config
        internal static float AzulCielo(string txt) { return LeePorCiento(txt, 1f); }

        // CUANTO NEGRO hay arriba: donde esta el corte del negro al azul, como parte del alto de la pantalla.
        // Mas negro = el azul empieza mas abajo. Se acota entre un cuarto y dos tercios: fuera de ahi deja de
        // parecer un cielo.
        private const float CieloCorteDef = 0.42f;
        private static float cieloCorte = CieloCorteDef;   // "cielo_negro=" en config
        // El corte, ya acotado, tal y como lo piden las dos paredes.
        internal static float CorteDelCielo { get { return cieloCorte; } }

        // ===================== CUANTO AIRE HAY DELANTE DE CADA ESTRELLA =====================
        //
        // Un cielo de verdad no se ve igual arriba que abajo. Mirando hacia arriba se atraviesa el minimo
        // de atmosfera; mirando al horizonte, la misma mirada cruza muchisimo mas aire, y eso le hace tres
        // cosas a las estrellas de ahi abajo: las APAGA, las tira a NARANJA y las hace CENTELLEAR mas.
        //
        // La pared las pintaba igual de blancas y de brillantes pegadas al techo que pegadas al suelo, y
        // esa es la razon principal de que el campo se leyera como una textura lisa y no como un espacio
        // con fondo. De paso arregla otra cosa: ahi abajo viven el resplandor del genero y la aurora, y
        // hasta ahora las estrellas les peleaban el sitio.
        //
        // Devuelve 0 arriba del todo y 1 abajo del todo. El exponente es lo que concentra el efecto en la
        // franja de abajo en vez de repartirlo por igual: a media altura el aire va por un quinto, no por
        // la mitad.
        //
        // Y VIVE AQUI, fuera del trozo de tarjeta grafica, porque las dos paredes -la de tarjeta y la
        // horneada- tienen que usar exactamente la misma curva, igual que ya pasa con el corte del cielo
        // y con la fuerza de las nebulosas. Con dos copias, la maquina del bar y la de repuesto pintarian
        // cielos distintos y nadie se enteraria hasta verlas juntas.
        private const float AireCurva = 2.2f;
        internal static float AireDelCielo(float y, float alto)
        {
            if (!(alto > 0f)) return 0f;
            float u = y / alto;
            if (!(u > 0f)) return 0f;   // el "!" atrapa tambien el NaN, que en una comparacion normal se colaria
            if (u > 1f) u = 1f;
            return (float)Math.Pow(u, AireCurva);
        }

        // CUANTO SE NOTA, QUE ES COSA DE GUSTO Y POR ESO ES UNA PERILLA.
        //
        // Son tres numeros -cuanto se apaga una estrella pegada al suelo, cuanto enrojece y cuanto mas
        // centellea- pero NO se eligen por separado. Van juntos porque son la misma cosa: cuanto aire hay
        // delante. Un cielo con las estrellas de abajo apagadas pero igual de blancas no es "medio efecto",
        // es un cielo raro. Asi que la perilla es UNA, de cuatro pasos, y cada paso lleva sus tres numeros.
        //
        // Y los cuatro pasos se eligieron MIRANDO: dibujando el cielo a PNG con las cuatro combinaciones y
        // comparandolas de dos en dos, igual que se hizo con las nebulosas. A ojo no se elige esto.
        private static readonly float[] AireApagaPaso     = { 0f, 0.35f, 0.55f, 0.75f };   // brillo que PIERDE la de abajo
        private static readonly float[] AireCalorPaso     = { 0f, 0.35f, 0.55f, 0.72f };   // cuanto enrojece
        private static readonly float[] AireCentelleoPaso = { 0f, 0.50f, 0.90f, 1.40f };   // cuanto MAS centellea
        internal const int AirePasos = 4;
        private const int AirePasoDef = 2;                 // de fabrica, el tercero: se nota y no llama la atencion
        private static int airePaso = AirePasoDef;         // "cielo_aire=" en config
        // Acotada como todas las demas: en el archivo de configuracion se puede escribir cualquier cosa, y un
        // disparate ahi no puede dejar la pantalla inservible en un bar a las dos de la manana.
        internal static int PasoDelAire(string txt)
        {
            int v;
            if (txt == null) return AirePasoDef;
            if (!int.TryParse(txt.Trim(), System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out v)) return AirePasoDef;
            if (v < 0) return 0;
            if (v > AirePasos - 1) return AirePasos - 1;
            return v;
        }
        internal static int AirePaso { get { return airePaso; } }
        internal static float AireApaga { get { return AireApagaPaso[airePaso]; } }
        internal static float AireCalor { get { return AireCalorPaso[airePaso]; } }
        internal static float AireCentelleo { get { return AireCentelleoPaso[airePaso]; } }

        // ===================== DE DONDE SALE UNA FUGAZ Y HACIA DONDE VA =====================
        //
        // TODAS LAS FUGACES ERAN LA MISMA FUGAZ. Salian del sexto de arriba a la izquierda de la pantalla y
        // caian hacia abajo-derecha con un angulo de entre 19 y 33 grados; nunca hacia el otro lado, nunca
        // desde otro sitio. Y cae una cada 7-16 segundos, o sea que quien mira la rocola un minuto ve cinco
        // identicas y deja de mirar.
        //
        // Ahora el angulo barre de lado a lado -de 32 a 148 grados- pero SIEMPRE cayendo, que es lo que hace
        // una de verdad. Se probo dejar que subieran y no vale: abajo esta la pared de discos, y una fugaz
        // que sale de detras de los discos hacia arriba parece un cohete de feria, no un cielo.
        //
        // Y NACE FUERA, PEGADA AL BORDE POR DONDE ENTRA. Antes nacia dentro, asi que con direcciones libres
        // la mitad se saldria de la pantalla en dos palmos sin que diera tiempo a verla. El borde por donde
        // entra sale de su propia direccion: si va mas de lado que de bajada, entra por el costado; si va
        // mas de bajada, por arriba.
        //
        // UNA DE CADA OCHO ES DE LAS GORDAS: lenta, gruesa, con estela larga y brillante, de las que duran
        // un segundo y medio y hacen que alguien levante la vista. Sin esto todas duran lo mismo y el cielo
        // no tiene sorpresas, solo repeticion.
        //
        // Y VIVE AQUI, fuera del trozo de tarjeta grafica, porque las dos paredes tienen que tirar las
        // mismas fugaces: igual que la curva del aire y el corte del cielo. Con dos copias, la maquina del
        // bar y la de repuesto tendrian cielos distintos y no se sabria hasta verlas juntas.
        internal const float FugazMargen = 40f;    // cuanto fuera de la pantalla nace
        internal const float FugazBorra = 130f;    // y a cuanto de fuera se la da por ida (mas que el margen: si no, moriria al nacer)
        internal static void TiraFugaz(Random r, float w, float h,
                                       out float x, out float y, out float vx, out float vy,
                                       out float vida, out float grosor, out float cola, out bool gorda)
        {
            gorda = (r.Next(8) == 0);
            // De 32 a 148 grados: barre de abajo-derecha a abajo-izquierda pasando por la vertical.
            double a = 0.18 * Math.PI + r.NextDouble() * 0.64 * Math.PI;
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);   // dy > 0 siempre: en pantalla, abajo
            float vel = gorda ? (w + h) * (0.16f + (float)r.NextDouble() * 0.10f)
                              : (w + h) * (0.40f + (float)r.NextDouble() * 0.45f);
            vx = dx * vel; vy = dy * vel;
            vida = gorda ? 1.8f + (float)r.NextDouble() * 1.0f : 0.9f + (float)r.NextDouble() * 0.8f;
            grosor = gorda ? 2.6f + (float)r.NextDouble() * 1.2f : 0.9f + (float)r.NextDouble() * 1.5f;
            cola = gorda ? 0.16f + (float)r.NextDouble() * 0.08f : 0.07f + (float)r.NextDouble() * 0.10f;
            if (Math.Abs(dx) > dy)
            {
                // Va mas de lado que de bajada: entra por el costado contrario, y por la mitad de arriba
                // para que le quede pantalla por delante.
                x = (dx > 0f) ? -FugazMargen : w + FugazMargen;
                y = (float)(r.NextDouble() * h * 0.55);
            }
            else
            {
                x = (float)(r.NextDouble() * w);   // va mas de bajada: entra por arriba
                y = -FugazMargen;
            }
        }

        // AL TOCAR UNA PERILLA, QUE SE VEA YA. La ventana de Ctrl+F3 no tapa la pared entera -mide 960 de
        // ancho y la pantalla del bar 1920-, asi que se elige mirando: se toca y el fondo cambia al instante.
        //
        // Sin tarjeta grafica el cielo va horneado a un mapa de bits que se guarda, y hay que decirle que ya
        // no vale; con tarjeta grafica los colores se le pasan al panel en cada cuadro y basta con repintar.
        private void RepintaFondo()
        {
            if (nowPlaying != null && !nowPlaying.IsVideo)
                ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
            if (formStars != null) formStars.TiraCieloCache();
            this.Invalidate();
            if (albumView != null) albumView.Invalidate();
        }

        // EN MEDIO del cielo: el "azulado default". Casi independiente del genero (apenas un dejo) para que,
        // como pediste, despues del color del genero de abajo siga el azul antes de oscurecer al negro de arriba.
        private static Color SkyColorMid(Color accent)
        {
            // La parte AZUL es la que lleva la perilla: la del genero se queda como esta. Con la perilla a
            // cero el medio del cielo casi no tiene azul propio y el color de la cancion llega mucho mas
            // arriba; con ella a tope es el azul de siempre.
            int r = (int)(22 * cieloAzul) + accent.R / 10, g = (int)(50 * cieloAzul) + accent.G / 8, b = (int)(120 * cieloAzul) + accent.B / 7;
            if (r > 60) r = 60; if (g > 105) g = 105; if (b > 205) b = 205;
            r = r * SkyDark / 100; g = g * SkyDark / 100; b = b * SkyDark / 100;   // ESTETICA: mitad del cielo mas oscura
            return Color.FromArgb(255, r, g, b);
        }

        // Sube o baja el brillo de un color sin tocar su tono. La usa la respiracion del cielo: es lo
        // unico que la pared hace por su cuenta, asi que va por un solo sitio.
        internal static Color EscalaBrillo(Color c, float f)
        {
            if (f < 0f) f = 0f;
            int r = (int)(c.R * f + 0.5f), g = (int)(c.G * f + 0.5f), b = (int)(c.B * f + 0.5f);
            if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
            return Color.FromArgb(c.A, r, g, b);
        }

        private static Color Lighten(Color c, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));
        }

        // UN PASO DE UN COLOR HACIA OTRO, QUE DE VERDAD LLEGA.
        //
        // Esto se usa para deslizar el color del fondo de una cancion a la siguiente: en cada cuadro se
        // acerca un 16% a su destino. Con la cuenta de antes -que tiraba los decimales- un canal que tenia
        // que SUBIR se quedaba clavado: subir el 16% de una diferencia de seis son 0.96, y al tirar los
        // decimales eso es cero. O sea que el color no llegaba NUNCA a su destino, se quedaba a seis.
        //
        // Y eso no era solo un color un pelin distinto. El programa se pregunta "sigue cambiando el color?"
        // comparando el actual con el destino, y la respuesta pasaba a ser QUE SI PARA SIEMPRE. Con esa
        // respuesta, la rocola repinta el fondo entero, la rejilla de discos y el interior del disco en CADA
        // cuadro en vez de en uno de cada tres. O sea que despues de la primera cancion se quedaba, de por
        // vida, haciendo el triple de trabajo del previsto, y cuanto mas grande la pantalla peor.
        //
        // Ahora el paso es de un punto como minimo y nunca se pasa de largo, asi que el deslizamiento acaba
        // -en unos veinticinco cuadros, un segundo- y el programa vuelve a su ritmo normal.
        private static int Hacia(int de, int a, double t)
        {
            int d = a - de;
            if (d == 0) return de;
            int abs = (d > 0) ? d : -d;
            int paso = (int)(abs * t + 0.5);
            if (paso < 1) paso = 1;
            if (paso > abs) paso = abs;
            return de + ((d > 0) ? paso : -paso);
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d < 2) d = 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ATAJO (rendimiento): casi todos los titulos son letras inglesas, numeros y signos corrientes.
        // Para esos, descomponer el texto en letra+tilde y preguntar por la categoria Unicode de cada
        // caracter es trabajo tirado: por debajo de 128 no hay NINGUN caracter que sea marca de acento, y
        // bajar a minusculas ahi es exactamente pasar la A-Z a la a-z. Asi que primero se mira si el texto
        // es de esos -una pasada, sin reservar memoria- y si lo es se baja a minusculas a mano; y si encima
        // ya venia en minusculas se devuelve el mismo texto, sin copiar nada.
        // El texto con acentos, enes o alfabetos raros sigue por el camino largo de siempre, igual que antes.
        // Armar el indice del buscador de una biblioteca de 120 mil canciones baja de 362 a 173 ms por esto.
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int n = s.Length, i = 0;
            bool hayMayus = false;
            for (; i < n; i++) { char c = s[i]; if (c >= (char)128) break; if (c >= 'A' && c <= 'Z') hayMayus = true; }
            if (i == n)
            {
                if (!hayMayus) return s;
                char[] b = new char[n];
                for (int k = 0; k < n; k++) { char c = s[k]; b[k] = (c >= 'A' && c <= 'Z') ? (char)(c + 32) : c; }
                return new string(b);
            }
            string formd = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(formd.Length);
            for (int k = 0; k < formd.Length; k++)
                if (CharUnicodeInfo.GetUnicodeCategory(formd[k]) != UnicodeCategory.NonSpacingMark) sb.Append(formd[k]);
            return sb.ToString();
        }
        // Solo texto/mapa, nunca medidas GDI: fuente, DPI, ancho y tema siguen midiendose al dibujar.
        // Por hilo para no introducir candados en el render. Limites de entradas y payload, sin Intern.
        [ThreadStatic] private static CacheTramos cacheTramos;
        private const int TramosMax = 128, TramosBytesMax = 65536;
        private static long BytesTramo(string texto, TextoTramo dato)
        {
            return 2L * texto.Length + 2L * dato.Limpio.Length + 4L * dato.Mapa.Length;
        }
        private static TextoTramo PreparaTramo(string texto)
        {
            CacheTramos cache = cacheTramos;
            TextoTramo dato;
            if (cache != null && cache.Textos.TryGetValue(texto, out dato)) return dato;
            string formd = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(formd.Length);
            int[] mapa = new int[formd.Length];
            // El mapa dice, por cada letra ya limpia, de que letra del texto original salio. Se avanza sobre la
            // forma descompuesta y se cuenta cuantos caracteres ORIGINALES se llevan consumidos.
            // Al descomponer, cada letra del original se abre en UNA base y cero o mas tildes. Las tildes son
            // de la base anterior, asi que solo las bases gastan un caracter del original.
            int orig = -1, j = 0;
            for (int i = 0; i < formd.Length; i++)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(formd[i]) == UnicodeCategory.NonSpacingMark) continue;
                orig++;
                mapa[j] = orig; sb.Append(formd[i]); j++;
            }
            dato = new TextoTramo(); dato.Limpio = sb.ToString(); dato.Mapa = mapa;
            long bytes = BytesTramo(texto, dato);
            if (bytes <= TramosBytesMax)
            {
                if (cache == null) cacheTramos = cache = new CacheTramos();
                while (cache.Textos.Count >= TramosMax || cache.Bytes + bytes > TramosBytesMax)
                {
                    string vieja = cache.Orden.Dequeue();
                    TextoTramo anterior = cache.Textos[vieja];
                    cache.Bytes -= (int)BytesTramo(vieja, anterior);
                    cache.Textos.Remove(vieja);
                }
                cache.Textos.Add(texto, dato); cache.Orden.Enqueue(texto); cache.Bytes += (int)bytes;
            }
            return dato;
        }

        private static bool Has(string s, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++) if (s.Contains(keys[i])) return true;
            return false;
        }

        // EL COLOR DE LO QUE SUENA: excepciones por artista (BTS = morado) y, si no, el genero.
        //
        // Aqui estaba el fallo del fondo. Esto miraba SOLO la etiqueta de genero del archivo (su ID3), y
        // muchisimos MP3 no traen ninguna. Entonces devolvia el azul de "no se de que es esto" aunque la
        // rocola SI supiera el genero por otro lado: la pastilla decia "Tropical", sacado del disco, del
        // artista o del nombre de la carpeta, y el fondo se quedaba azul. Ahora se pregunta por la misma
        // cascada que responde en la lista, en el buscador y en la franja de abajo, asi que las cuatro dicen
        // lo mismo. Deja de ser estatico porque esa cascada necesita lo que la rocola ha ido aprendiendo.
        private Color ColorDeLoQueSuena(Track t, Album org)
        {
            // Y encima, EL TONO DE LA PORTADA. Dos cumbias seguidas pintaban exactamente la misma
            // pantalla naranja aunque una fuera un disco rojo y la otra uno verde.
            Color portada = ColorDePortadaDe(org);
            if (t != null && GeneroDeCancion(t, org).Length == 0 && fondoPortadaOn && portadaManda > 0f)
            {
                // Sin genero no se mezcla con azul. Si el promedio se cancela, usa un pico real.
                Color uno, dos;
                ColoresDePortadaDe(org, out uno, out dos);
                return TonoSoloPortada(portada.A != 0 ? portada : uno);
            }
            Color genero = GeneroDeLoQueSuena(t, org);
            return EsArtistaMorado(t) ? genero : TinteConPortada(genero, portada);
        }

        // EL COLOR DEL GENERO A SECAS, sin el tinte de la portada. Antes esto no se guardaba en ningun lado:
        // solo quedaba la mezcla de los dos. Hace falta suelto porque una de las dos nebulosas lleva EL
        // GENERO y la otra LA CARATULA, y para eso hay que poder decir cual es cual.
        private static bool EsArtistaMorado(Track t)
        {
            string a = t != null && t.Artist != null ? t.Artist.ToLowerInvariant() : "";
            return a.Contains("bts") || a.Contains("bangtan");
        }

        // ---------- EL COLOR QUE LE PONE LA PORTADA AL FONDO ----------
        //
        // LA PORTADA ELIGE EL TONO. LA ROCOLA MANDA EN TODO LO DEMAS.
        //
        // De la portada se toma SOLO de que color es -rojo, azul, verde-. Lo oscuro y lo vivo que sale a
        // la pantalla lo sigue decidiendo el genero, con el mismo brillo y la misma viveza que ya tenia.
        // Asi NINGUNA portada puede estropear la pantalla: ni una casi blanca lavandola, ni una negra
        // apagandola, ni una de neon quemandola. No hay topes de seguridad que ajustar ni recortes que
        // se puedan quedar cortos: sale de como esta hecho, porque el brillo y la viveza NO SE TOCAN.
        //
        // Y el resto de la pantalla no se entera de nada: el genero sigue mandando en la etiqueta de la
        // pastilla (que usa ColorDeEtiqueta directo), asi que la palabra "Tropical" conserva su color.
        internal static Color TinteConPortada(Color genero, Color portada)
        {
            if (portada.A == 0) return genero;                     // la portada no sabe de que color es
            int mx = Math.Max(genero.R, Math.Max(genero.G, genero.B));
            int mn = Math.Min(genero.R, Math.Min(genero.G, genero.B));
            if (mx - mn < 2) return genero;                        // un gris no tiene tono que girar
            // Y NO SE VA ENTERO AL TONO DE LA PORTADA: se queda donde diga la perilla, de fabrica a medias.
            // Antes iba entero, y por eso una cancion de Pop con carátula azul pintaba la pantalla azul sin
            // rastro del rosa del Pop. Se va por el camino CORTO de la rueda (ver MezclaDeTono): a mitad de
            // camino, dar la vuelta larga te deja en el color de enfrente.
            return MezclaDeTono(genero, portada, portadaManda);
        }

        // Conserva brillo/viveza seguros; de la portada solo toma el tono, sin azul mezclado.
        private static Color TonoSoloPortada(Color portada)
        {
            return portada.A == 0 ? DefaultAccent : MezclaDeTono(DefaultAccent, portada, 1f);
        }

        private static void NebulosasSinGenero(Color port1, Color port2, float fase, ref Color a, ref Color b)
        {
            if (!fondoPortadaOn || portadaManda <= 0f || port1.A == 0) return;
            Color cp = port2.A == 0 ? port1 : MezclaDeTono(port1, port2, 0.5f + 0.5f * (float)Math.Sin(fase));
            a = b = ColorNebulosa(TonoSoloPortada(cp));
        }
        internal static readonly Color SonAmbar = Color.FromArgb(255, 198, 104);

        // TONO de un color, en grados de la rueda (0 = rojo, 120 = verde, 240 = azul).
        private static float TonoDe(Color c)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float d = mx - mn;
            if (d < 0.0001f) return 0f;
            float h;
            if (mx == r) h = 60f * (((g - b) / d) % 6f);
            else if (mx == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            while (h < 0f) h += 360f; while (h >= 360f) h -= 360f;
            return h;
        }

        // Gira el TONO de un color (grados en la rueda de color) y le escala el brillo, dejando la VIVEZA
        // como estaba. Vive AQUI FUERA, y no dentro del trozo de tarjeta grafica donde estaba, porque
        // ahora tambien la usa el fondo que toma el tono de la portada, y eso pasa con tarjeta y sin ella.
        private static Color GiraTono(Color c, float grados, float brillo)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float v = mx, d = mx - mn;
            float sat = (mx <= 0.0001f) ? 0f : d / mx;
            float hue = 0f;
            if (d > 0.0001f)
            {
                if (mx == r) hue = 60f * (((g - b) / d) % 6f);
                else if (mx == g) hue = 60f * ((b - r) / d + 2f);
                else hue = 60f * ((r - g) / d + 4f);
            }
            hue += grados; while (hue < 0f) hue += 360f; while (hue >= 360f) hue -= 360f;
            v *= brillo; if (v > 1f) v = 1f;
            float cc = v * sat, x = cc * (1f - Math.Abs((hue / 60f) % 2f - 1f)), m = v - cc;
            float rr, gg, bb;
            if (hue < 60f) { rr = cc; gg = x; bb = 0f; }
            else if (hue < 120f) { rr = x; gg = cc; bb = 0f; }
            else if (hue < 180f) { rr = 0f; gg = cc; bb = x; }
            else if (hue < 240f) { rr = 0f; gg = x; bb = cc; }
            else if (hue < 300f) { rr = x; gg = 0f; bb = cc; }
            else { rr = cc; gg = 0f; bb = x; }
            return Color.FromArgb((int)((rr + m) * 255f + 0.5f), (int)((gg + m) * 255f + 0.5f), (int)((bb + m) * 255f + 0.5f));
        }

        private const int MuestraLado = 32;          // como mucho, 32x32 puntos de la portada
        private const float TonoMinViveza = 0.22f;   // por debajo de esto es un gris, no un color
        private const float TonoMinBrillo = 0.14f;   // y por debajo de esto es negro
        private const float TonoMinAcuerdo = 0.35f;  // cuanto tienen que coincidir los colores para que uno mande

        // DE QUE COLOR ES UNA PORTADA. Devuelve un color del que solo importa el TONO; si la portada no
        // tiene ninguno -blanco y negro, o un revoltijo donde no manda nadie- devuelve A=0, "no se sabe",
        // y entonces manda el genero como hasta ahora.
        //
        // Se muestrea con paso fijo: como mucho 32x32 puntos, sea la portada de 300 pixeles de lado o de
        // 1400. Asi lo que cuesta NO depende del tamaño de la imagen, que es lo que haria que un disco con
        // una portada enorme trabara la rocola justo al empezar la cancion.
        //
        // Y se descarta lo que no es color: los puntos casi grises y los casi negros. Casi todas las
        // portadas traen marco oscuro y letras blancas, y contandolos el promedio se iria siempre al gris.
        //
        // El promedio de tonos se hace con seno y coseno, no sumando grados: el rojo esta en 0 y en 360 a
        // la vez, asi que sumando, dos rojos darian verde.
        private static Color ColorDePortada(Bitmap b)
        {
            Color uno, dos;
            return ColoresDePortada(b, out uno, out dos);
        }

        // Y LOS COLORES -EN PLURAL- DE UNA PORTADA. Del mismo recorrido salen las dos cosas.
        //
        // El promedio de arriba tiene un limite que se nota: cuando una portada trae DOS colores fuertes
        // -media roja y media verde- el promedio apunta a un sitio que no se parece a ninguno de los dos, y
        // entonces la funcion se calla y manda el genero. O sea que las portadas mas vistosas eran justo las
        // que no pintaban nada. Aqui, ademas del promedio, se sacan los DOS tonos que mas mandan, que es lo
        // que las nebulosas necesitan para tener con que mezclarse.
        //
        // Se hace repartiendo los tonos en CASILLAS de la rueda de color en vez de promediarlos todos juntos.
        // Una casilla no es un tono exacto -son 15 grados-, asi que del pico se saca el promedio de lo que
        // cayo DENTRO de el y de sus dos vecinas: asi el color que sale es el de verdad y no el del centro
        // de la casilla.
        //
        // Es el MISMO recorrido de 32x32 puntos de siempre: no cuesta una pasada mas, y como esto se apunta
        // por disco (ver ColorDePortadaDe) se hace una sola vez por portada.
        private const int TonoCasillas = 24;         // la rueda de color repartida en casillas de 15 grados
        private const float TonoSegundoLejos = 40f;  // dos tonos mas juntos que esto son el mismo color
        private const float TonoSegundoManda = 0.25f; // y el segundo tiene que pesar al menos esto del primero
        private static Color ColoresDePortada(Bitmap b, out Color uno, out Color dos)
        {
            uno = Color.FromArgb(0, 0, 0, 0); dos = Color.FromArgb(0, 0, 0, 0);
            if (b == null) return Color.FromArgb(0, 0, 0, 0);
            int w = b.Width, h = b.Height;
            if (w < 1 || h < 1) return Color.FromArgb(0, 0, 0, 0);
            int px = (w + MuestraLado - 1) / MuestraLado; if (px < 1) px = 1;
            int py = (h + MuestraLado - 1) / MuestraLado; if (py < 1) py = 1;
            double sx = 0, sy = 0, peso = 0;
            double[] cajaPeso = new double[TonoCasillas];
            double[] cajaX = new double[TonoCasillas], cajaY = new double[TonoCasillas];
            for (int y = py / 2; y < h; y += py)
                for (int x = px / 2; x < w; x += px)
                {
                    Color c;
                    try { c = b.GetPixel(x, y); } catch { return Color.FromArgb(0, 0, 0, 0); }
                    float r = c.R / 255f, g = c.G / 255f, bl = c.B / 255f;
                    float mx = Math.Max(r, Math.Max(g, bl)), mn = Math.Min(r, Math.Min(g, bl));
                    if (mx < TonoMinBrillo) continue;              // negro: no dice de que color es el disco
                    float d = mx - mn, sat = d / mx;
                    if (sat < TonoMinViveza) continue;             // gris: tampoco
                    float hue;
                    if (mx == r) hue = 60f * (((g - bl) / d) % 6f);
                    else if (mx == g) hue = 60f * ((bl - r) / d + 2f);
                    else hue = 60f * ((r - g) / d + 4f);
                    if (hue < 0f) hue += 360f;
                    // Pesa lo VIVO y lo CLARO: un rojo encendido manda mas que un beige que apenas pasa el
                    // corte. Si no, el fondo de una portada casi apagada decidiria por encima de su dibujo.
                    double pes = sat * mx * (c.A / 255.0);   // el RGB invisible no decide el fondo
                    double rad = hue * Math.PI / 180.0;
                    double cx = Math.Cos(rad) * pes, cy = Math.Sin(rad) * pes;
                    sx += cx; sy += cy; peso += pes;
                    int k = (int)(hue * TonoCasillas / 360f); if (k >= TonoCasillas) k = TonoCasillas - 1;
                    cajaPeso[k] += pes; cajaX[k] += cx; cajaY[k] += cy;
                }
            if (peso <= 0.0001) return Color.FromArgb(0, 0, 0, 0);
            PicosDeTono(cajaPeso, cajaX, cajaY, peso, out uno, out dos);
            // Y SI LOS COLORES NO SE PONEN DE ACUERDO tampoco se sabe. Una portada con de todo -media roja
            // y media verde- daria un promedio que apunta a cualquier lado y no se parece a ninguna de las
            // dos. Cuando eso pasa, mejor callarse y dejar mandar al genero.
            //
            // Esto vale para el color que tine EL CIELO, que es uno solo y tiene que ser de fiar. Los dos
            // picos de arriba NO pasan por aqui a proposito: son justo para ese caso, dos colores que se
            // mezclan en las nebulosas en vez de un promedio que no es ninguno de los dos.
            double acuerdo = Math.Sqrt(sx * sx + sy * sy) / peso;
            if (acuerdo < TonoMinAcuerdo) return Color.FromArgb(0, 0, 0, 0);
            double ang = Math.Atan2(sy, sx) * 180.0 / Math.PI; if (ang < 0) ang += 360.0;
            return GiraTono(Color.FromArgb(255, 0, 0), (float)ang, 1f);   // un color cualquiera con ESE tono
        }

        // Los dos tonos que mas mandan, sacados de las casillas. El primero es la casilla mas pesada; el
        // segundo, la mas pesada de las que estan LEJOS de la primera -si estuvieran juntas serian el mismo
        // color y no habria mezcla que ver- y ademas tiene que pesar lo suyo, que si no seria una esquinita
        // de la portada decidiendo la mitad de la pantalla.
        private static void PicosDeTono(double[] cajaPeso, double[] cajaX, double[] cajaY, double peso,
                                        out Color uno, out Color dos)
        {
            uno = Color.FromArgb(0, 0, 0, 0); dos = Color.FromArgb(0, 0, 0, 0);
            int p1 = -1;
            for (int k = 0; k < TonoCasillas; k++) if (p1 < 0 || cajaPeso[k] > cajaPeso[p1]) p1 = k;
            if (p1 < 0 || cajaPeso[p1] <= 0.0001) return;
            float t1 = TonoDeCasilla(cajaX, cajaY, p1);
            uno = GiraTono(Color.FromArgb(255, 0, 0), t1, 1f);
            int p2 = -1;
            for (int k = 0; k < TonoCasillas; k++)
            {
                if (cajaPeso[k] <= 0.0001) continue;
                if (DifDeTono(TonoDeCasilla(cajaX, cajaY, k), t1) < TonoSegundoLejos) continue;
                if (p2 < 0 || cajaPeso[k] > cajaPeso[p2]) p2 = k;
            }
            if (p2 < 0 || cajaPeso[p2] < cajaPeso[p1] * TonoSegundoManda) return;
            dos = GiraTono(Color.FromArgb(255, 0, 0), TonoDeCasilla(cajaX, cajaY, p2), 1f);
        }

        // El tono de una casilla y sus dos vecinas, promediado en circulo: el pico cae donde de verdad esta
        // el color y no en el centro del cajon.
        private static float TonoDeCasilla(double[] cajaX, double[] cajaY, int k)
        {
            int a = (k + TonoCasillas - 1) % TonoCasillas, b = (k + 1) % TonoCasillas;
            double x = cajaX[a] + cajaX[k] + cajaX[b], y = cajaY[a] + cajaY[k] + cajaY[b];
            double ang = Math.Atan2(y, x) * 180.0 / Math.PI; if (ang < 0) ang += 360.0;
            return (float)ang;
        }

        // Cuanto se separan dos tonos, por el camino corto de la rueda (de 0 a 180).
        private static float DifDeTono(float a, float b)
        {
            float d = a - b; while (d < 0f) d += 360f; while (d >= 360f) d -= 360f;
            return (d > 180f) ? 360f - d : d;
        }

        // El color de la portada de un disco, APUNTADO. Recorrer un mapa de bits cuesta, y esto se pregunta
        // cada vez que empieza una cancion; por disco solo hace falta hacerlo una vez.
        //
        // Si la portada aun no esta cargada NO se apunta nada: se vuelve a preguntar cuando llegue (lo hace
        // el aviso de portada cargada, que ya existia para el cubito de abajo).
        private struct ColoresDisco { public Color Prom, Uno, Dos; }   // el promedio (tine el cielo) y los dos picos (las nebulosas)
        private readonly Dictionary<string, ColoresDisco> colorPortada = new Dictionary<string, ColoresDisco>(StringComparer.OrdinalIgnoreCase);
        private static bool fondoPortadaOn = true;   // "fondo_portada=" en config
        private static readonly Color SinColor = Color.FromArgb(0, 0, 0, 0);
        private Color ColorDePortadaDe(Album a)
        {
            if (!fondoPortadaOn || a == null) return SinColor;
            string k = a.CoverPath;
            if (string.IsNullOrEmpty(k)) return SinColor;
            ColoresDisco c;
            if (colorPortada.TryGetValue(k, out c)) return c.Prom;
            Bitmap b = LoadCover(a) as Bitmap;
            if (b == null) return SinColor;
            c.Prom = ColoresDePortada(b, out c.Uno, out c.Dos);
            colorPortada[k] = c;
            return c.Prom;
        }

        // Los dos colores de la portada de un disco. Quien MIDE y APUNTA sigue siendo el de arriba -aqui solo
        // se lee lo apuntado-, para que no haya dos sitios recorriendo la imagen.
        private void ColoresDePortadaDe(Album a, out Color uno, out Color dos)
        {
            uno = SinColor; dos = SinColor;
            ColorDePortadaDe(a);                     // si no estaba medido, lo mide y lo apunta
            if (!fondoPortadaOn || a == null) return;
            string k = a.CoverPath;
            if (string.IsNullOrEmpty(k)) return;
            ColoresDisco c;
            if (colorPortada.TryGetValue(k, out c)) { uno = c.Uno; dos = c.Dos; }
        }

        // Etiqueta canonica del genero para agrupar los discos TOP (mismas familias que GenreToColor).
        private static string GenreLabel(string genre)
        {
            if (string.IsNullOrEmpty(genre)) return "Otros";
            string g = genre.ToLowerInvariant();
            // KARAOKE manda sobre todo lo demas: una cumbia de karaoke es karaoke, no cumbia. Y NAVIDAD va
            // detras por lo mismo (una "cumbia navidena" se guarda por navidad, que es cuando se pone).
            if (Has(g, "karaoke")) return "Karaoke";
            if (Has(g, "navidad", "navide", "villancico", "christmas", "xmas")) return "Navidad";
            if (Has(g, "k-pop", "kpop", "j-pop", "jpop", "anime", "idol")) return "K-Pop";
            if (Has(g, "metal", "thrash", "death", "heavy", "hardcore", "grindcore")) return "Metal";
            if (Has(g, "punk")) return "Punk";
            if (Has(g, "gothic", "goth", "gotic", "g\u00f3tic", "darkwave")) return "Gotico";
            if (Has(g, "industrial")) return "Industrial";
            if (Has(g, "reggaeton", "reggaet", "reguet", "perreo", "dembow")) return "Reggaeton";
            if (Has(g, "hip", "rap", "trap")) return "Hip-Hop";
            if (Has(g, "salsa", "mereng", "bachata", "tropical", "vallenato", "guarach", "mambo", "chachacha",
                       "cha cha", "danzon", "danz\u00f3n", "guaguanco", "timba", "marimba", "son cubano")) return "Tropical";
            if (Has(g, "cumbia", "banda", "norte", "ranch", "mariach", "regional", "tejan", "grupe", "corrido",
                       "duranguense", "sonidero", "quebradita", "sierre", "huapango", "huasteco", "zapateado")) return "Regional";
            if (Has(g, "bolero", "tango", "ranchera", "balada", "romantic", "romant", "rom\u00e1nt", "vals", "serenata")) return "Bolero";
            if (Has(g, "flamenco", "rumba", "copla")) return "Flamenco";
            if (Has(g, "latin", "latino", "espan", "samba", "sertanejo", "pagode", "forro")) return "Latino";
            if (Has(g, "reggae", "ska", "dub")) return "Reggae";
            if (Has(g, "drum", "bass", "dnb", "d&b", "jungle", "breakbeat", "garage")) return "DnB";
            if (Has(g, "electro", "electr\u00f3n", "techno", "tecno", "house", "trance", "edm", "synth", "dance", "club", "dubstep", "rave", "hardstyle", "tribal")) return "Electronica";
            if (Has(g, "ambient", "chill", "lo-fi", "lofi", "downtempo", "new age", "meditat")) return "Chill";
            if (Has(g, "jazz", "swing", "bebop", "bossa")) return "Jazz";
            if (Has(g, "blues", "soul", "funk", "r&b", "rhythm", "motown")) return "Soul";
            if (Has(g, "gospel", "christian", "worship", "cristian", "alabanza")) return "Cristiana";
            if (Has(g, "classic", "clasic", "cl\u00e1sic", "orchestr", "orquest", "piano", "opera", "\u00f3pera", "symphon", "baroque", "instrumental")) return "Clasica";
            if (Has(g, "soundtrack", "score", "cinematic", "banda sonora")) return "Soundtrack";
            if (Has(g, "country", "bluegrass", "americana")) return "Country";
            if (Has(g, "folk", "acoustic", "celt")) return "Folk";
            if (Has(g, "disco")) return "Disco";
            if (Has(g, "oldies", "50s", "60s", "doo")) return "Oldies";
            if (Has(g, "world", "ethnic", "african", "arab", "india", "asian")) return "World";
            if (Has(g, "vocal", "capella", "capela")) return "Vocal";
            if (Has(g, "comedy", "spoken", "podcast", "hablado", "narracion", "narraci\u00f3n", "audiolibro")) return "Hablado";
            if (Has(g, "child", "kids", "infantil")) return "Infantil";
            if (Has(g, "pop")) return "Pop";
            if (Has(g, "rock", "grunge", "alternative", "indie")) return "Rock";
            return "Otros";
        }

        // EL COLOR DE CADA GENERO. Esta es LA tabla: no hay otra (GenreToColor pregunta aqui).
        //
        // Cada genero se queda en la familia de color que le toca -lo regional en naranjas, lo electronico en
        // turquesas, lo clasico en azules- pero repartidos DENTRO de la familia para que no se confundan entre
        // si. Antes habia parejas que eran el mismo color: Regional 225,120,30 y Latino 225,130,40 no se
        // distinguian de ninguna manera, ni Reggaeton de Hip-Hop. Medido con la distancia "redmean" (la que se
        // parece a lo que ve el ojo, no la de RGB a secas), la pareja mas parecida pasa de 25 a 53, y eso con
        // dos generos MAS en la tabla.
        private static Color ColorDeEtiqueta(string etiqueta)
        {
            switch (etiqueta)
            {
                case "Karaoke": return Color.FromArgb(70, 150, 245); // azul de escenario
                case "Navidad": return Color.FromArgb(125, 200, 70); // verde pino claro
                case "K-Pop": return Color.FromArgb(175, 100, 235); // lila claro
                case "Metal": return Color.FromArgb(215, 40, 45); // rojo sangre
                case "Punk": return Color.FromArgb(245, 85, 30); // naranja encendido
                case "Gotico": return Color.FromArgb(100, 55, 150); // violeta oscuro
                case "Industrial": return Color.FromArgb(145, 65, 80); // granate apagado
                case "Reggaeton": return Color.FromArgb(195, 60, 190); // magenta
                case "Hip-Hop": return Color.FromArgb(125, 70, 215); // violeta azulado
                case "Tropical": return Color.FromArgb(255, 130, 40); // naranja mango
                case "Regional": return Color.FromArgb(215, 120, 25); // naranja tierra
                case "Bolero": return Color.FromArgb(175, 80, 85); // vino rosado
                case "Flamenco": return Color.FromArgb(200, 130, 80); // arena tostada
                case "Latino": return Color.FromArgb(190, 85, 45); // terracota
                case "Reggae": return Color.FromArgb(40, 175, 85); // verde bandera
                case "DnB": return Color.FromArgb(15, 145, 150); // turquesa oscuro
                case "Electronica": return Color.FromArgb(35, 195, 210); // cian brillante
                case "Chill": return Color.FromArgb(95, 175, 165); // verde agua
                case "Jazz": return Color.FromArgb(225, 160, 40); // ambar
                case "Soul": return Color.FromArgb(185, 130, 45); // mostaza
                case "Cristiana": return Color.FromArgb(205, 195, 100); // oro palido
                case "Clasica": return Color.FromArgb(90, 100, 215); // azul indigo
                case "Soundtrack": return Color.FromArgb(110, 125, 200); // azul acero
                case "Country": return Color.FromArgb(140, 120, 85); // caqui
                case "Folk": return Color.FromArgb(120, 150, 90); // verde oliva
                case "Disco": return Color.FromArgb(225, 75, 175); // rosa neon
                case "Oldies": return Color.FromArgb(170, 115, 105); // rosa palo
                case "World": return Color.FromArgb(160, 95, 45); // marron
                case "Vocal": return Color.FromArgb(200, 175, 150); // crema
                case "Hablado": return Color.FromArgb(135, 140, 140); // gris
                case "Infantil": return Color.FromArgb(255, 190, 55); // amarillo vivo
                case "Pop": return Color.FromArgb(235, 55, 110); // rosa fuerte
                case "Rock": return Color.FromArgb(225, 95, 75); // coral
                default: return DefaultAccent;
            }
        }

        // EL COLOR DE UN GENERO CRUDO (el que trae el ID3). Antes esto era una segunda lista de palabras,
        // copia de la de GenreLabel, y las dos ya se habian desparejado: una cancion marcada "Balada" salia
        // con el color del Bolero en un sitio y con el azul de por defecto en otro. Ahora se traduce a
        // etiqueta y se le pide su color, asi que hay UN SOLO sitio donde vive el color de cada genero y no
        // se pueden volver a desparejar.
        private static Color GenreToColor(string genre) { return ColorDeEtiqueta(GenreLabel(genre)); }

        private Font Bold(float size) { return new Font(this.Font.FontFamily, size, FontStyle.Bold); }

        private void TransLabel(Label l, float size)
        {
            l.BackColor = Color.Transparent;
            l.ForeColor = Color.White;
            l.Font = Bold(size);
        }

        // Campo de estrellas reutilizable: cielo + estrellas titilantes (mas brillantes) + estrellas
        // fugaces ocasionales. Lo usan el fondo de navegacion (teñido por genero) y el modo atraccion
        // (cielo negro y estrellas multicolor). El titileo es LENTO a proposito para verse suave aun a 8fps.
        private class Starfield
        {
            public bool Varied = false;   // true = colores variados (atraccion); false = blanco->tinte (genero)
            public float Density = 1f;    // multiplicador de cantidad de estrellas (el fondo del form usa menos)
            // MENOS ESTRELLAS DETRAS DE LAS LETRAS.
            //
            // La rocola tiene dos franjas de texto que no se mueven: el abecedario de arriba y la barra de
            // estado de abajo. El cielo pasa por detras de las dos, y una estrella justo detras de una letra
            // le quita contraste: se lee peor de lejos, que es como se lee en un bar.
            //
            // Con esto encendido, una estrella que caiga en una de esas dos franjas se vuelve a tirar: casi
            // siempre acaba en otro sitio, y las pocas que insisten se quedan. Asi las franjas quedan mas
            // despejadas SIN quedarse vacias -un hueco negro se notaria mas que las estrellas- y sin perder
            // ni una del cielo: se mueven, no se pierden.
            //
            // EL AZAR DE VOLVER A TIRAR VA APARTE, y esto importa mas de lo que parece. Si se tirara del
            // mismo azar que todo lo demas, cada tiro de mas correria la fila entera y saldria OTRO cielo
            // distinto: otras estrellas, otros brillos, otros colores. Con un azar propio, el cielo es
            // exactamente el mismo con esto encendido o apagado, y lo unico que cambia es DONDE caen las
            // pocas que estorbaban. Se vio en la bateria: al hacerlo mal, el cielo perdia 13 de 56.
            public bool QuietBands = false;
            // Que parte de la pantalla ocupan esas franjas. Son de sobra a proposito: cubren el texto y un
            // poco mas, para que ninguna estrella quede pegada al filo de una letra.
            private const float FranjaArriba = 0.13f, FranjaAbajo = 0.88f;
            // Cuantas veces se vuelve a tirar. Con una sola, de cada cuatro estrellas que caian detras de
            // las letras se queda una: bastantes menos, pero no cero.
            private const int TirosFranja = 1;
            private static bool EnFranja(float y) { return y < FranjaArriba || y > FranjaAbajo; }
            public float Dim = 1f;        // atenuacion del starfield (1 = normal; ~0.45 mientras se navega con musica). Solo estrellas/brillos, no el cielo

            // QUE SE DIBUJA. Solo hay dos cosas en el cielo, las mismas por CPU y por tarjeta grafica para
            // que se vea igual por los dos caminos: la ESTRELLA de cinco puntas -las medianas y las grandes-
            // y el PUNTO redondo del polvo del fondo, que mide dos o tres pixeles y ahi una silueta no seria
            // una silueta, seria una mancha con pelusa. Quien es quien lo dice 'Kind', que ya lo decia.
            internal const int FormaEstrella = 0, FormaPunto = 1;
            // Bri: lo brillante que es, de 0 a 255, en una escala CONTINUA. Antes solo habia tres escalones
            // -grande, mediana, polvo- y por eso el cielo se veia hecho de tres cosas repetidas; un cielo de
            // verdad tiene un punado de estrellas fuertes y cientos que apenas se adivinan.
            // Rayos: si lleva las cuatro rayitas cruzadas. Las llevan MUY pocas (ver Ensure).
            private struct Star { public float X, Y; public int Size, Kind; public byte R, G, B, Bri; public bool Rayos; public float Tint, Off, Spd; public byte Band; }   // Band: 0=graves,1=medios,2=agudos
            private struct Shoot { public bool On; public float X, Y, VX, VY; public float Life, MaxLife, Wdt, Tail; }
            private Star[] stars;
            private int sw = -1, sh = -1;
            private SolidBrush sb;
            private Pen sp;
            // ESTETICA (glow): sprites de resplandor radial pre-horneados UNA sola vez y COMPARTIDOS por todas
            // las instancias. Se estampan detras del nucleo de cada estrella: fuerte en la grande, tenue en la
            // chica, nada en los puntos -> "brillan de verdad" sin contaminar visualmente.
            private static Bitmap[] glowPal;   // ESTETICA/RENDIMIENTO: un sprite de glow PRE-HORNEADO por color (sin matriz por estrella)
            private static readonly Color[] glowCols = {
                Color.White,                          // 0 blanco
                Color.FromArgb(255, 120, 120),        // 1 rojo
                Color.FromArgb(255, 180, 110),        // 2 naranja
                Color.FromArgb(255, 225, 140),        // 3 dorado
                Color.FromArgb(150, 255, 180),        // 4 verde
                Color.FromArgb(150, 235, 255),        // 5 cian
                Color.FromArgb(150, 180, 255),        // 6 azul
                Color.FromArgb(225, 165, 255)         // 7 violeta/rosa
            };
            private static readonly object glowLock = new object();
            private readonly PointF[] spk = new PointF[4];   // buffer reusable de las rayitas (sin asignar por estrella)
            private Shoot[] shoots = new Shoot[2];
            private int nextShoot = 0, lastTick = 0;
            // TITILEO por CUADROS HORNEADOS: K capas con el MISMO patron de estrellas a distinto brillo.
            // Rotarlas (una copia por cuadro) reproduce el titileo SIN recalcular ~120 estrellas por cuadro:
            // recupera el efecto animado al costo de una copia (no de recalculo) -> fluido en el CPU lento.
            private const int TwK = 8;
            private Bitmap[] layers;
            private int lw = -1, lh = -1;
            private readonly Random rnd = new Random();
            // Cache del cuadro: el starfield se dibuja a un bitmap y solo se reconstruye cada ~110ms
            // (para el titileo). Asi cada Draw es un blit barato aunque lo pidan muchas veces seguidas
            // los controles transparentes encima (etiquetas de estado, lblAlbum, cubeBox). Antes se
            // redibujaban ~89 rectangulos en cada repintado transparente -> tormenta -> traba/crash.
            private Bitmap cache;
            private int cw = -1, ch = -1, cSky = 0, cTint = 0, lastBuild = 0;
            private int cSky2 = 0;                                        // color inferior del degradado del cache
            private int sBucket = int.MinValue, lBucket = int.MinValue;   // "minuto" del patron actual (cambia cada 60s)
            // PANTALLA COMPLETA como UNA SOLA PIEZA: el campo de estrellas se hornea al tamano de TODA la pantalla
            // (SkyFullW x SkyFullH) y cada fondo (franjas del form, rejilla de discos, interior del disco) pinta
            // solo su VENTANA (offset SkyOffsetX/Y). Asi las estrellas cruzan los bordes de las secciones sin
            // cortarse ni desalinearse: el patron, el cielo y la densidad son continuos entre secciones.
            public int SkyOffsetX = 0, SkyOffsetY = 0, SkyFullW = 0, SkyFullH = 0;
            private int FullW(int w) { return SkyFullW > w ? SkyFullW : w; }   // ancho de pantalla completa (>= la ventana); si no se fijo, la ventana ES la pantalla
            private int FullH(int h) { return SkyFullH > h ? SkyFullH : h; }
            public Color SkyMid = Color.Empty;                            // color INTERMEDIO (azul noche); si Empty -> 2 paradas (punto medio)
            public Color Acento = Color.Empty;                            // el color del GENERO que suena: es con el que se tinen las nebulosas
            // LAS DOS NEBULOSAS (ver ColoresNebulosa): la del genero y la de la caratula. Aqui NO se mueven
            // de sitio -el cielo va horneado a un mapa de bits que se guarda, y moverlas seria rehacerlo en
            // cada cuadro, que es de 6 a 27 ms segun la pantalla-, pero si se cruzan sus COLORES. Si vienen
            // vacias se dibuja una sola con el acento, como se hacia siempre.
            public Color NebUno = Color.Empty, NebDos = Color.Empty;
            public float HazFase = 0f;     // reloj lento del cielo (una vuelta cada ~3 min): pasea la luna
            public bool ConLuna = true;    // perilla de Ctrl+F3
            private LinearGradientBrush skyBrush;                         // pincel de cielo en DEGRADADO (cacheado, ligado al genero)
            private int gbW = -1, gbOff = -99999, gbFull = -1, gbTop = 0, gbMid = 0, gbBot = 0;
            private Bitmap skyCache;                                       // RENDIMIENTO: cielo (degradado) HORNEADO a bitmap; se copia por cuadro en vez de rellenar el degradado cada vez
            private int scW = -1, scH = -1;
            private int fscTop = 0, fscMid = 0, fscBot = 0;               // colores con los que se horneo el cielo de pantalla completa (se re-hornea solo al cambiar genero/tamano)
            // TIRA EL CIELO GUARDADO. Lo llama Ctrl+F3 al tocar una perilla del fondo: el corte del negro al
            // azul no esta en la firma de arriba -no es un color- y sin esto el cielo se quedaria como estaba
            // hasta que cambiara la cancion.
            public void TiraCieloCache() { fscTop = 0; fscMid = 0; fscBot = 0; }
            // DERIVA con la musica (igual que la version GPU): al sonar, el campo entero se desliza LENTO en diagonal
            // (abajo-derecha) y se envuelve por los bordes. Barato: en la vista de discos solo desplaza el blit de la
            // capa de estrellas (toroidal); en el buscador desplaza cada estrella. Solo avanza en repintados COMPLETOS.
            private int driftLastMs; private float driftEnv, driftX, driftY;
            private const float DriftVX = 18f, DriftVY = 9f;             // px/seg (derecha, abajo): lento, diagonal 2:1 (como GPU)

            // Avanza la deriva segun el RELOJ. Se llama UNA vez por latido de la animacion, desde el form, y no
            // desde el dibujado: si se avanzara al dibujar, la deriva iria al ritmo al que toque rehacer las
            // cosas -que no es constante- y el fondo se veria a tirones aunque la cuenta fuera correcta.
            public void AvanzaDeriva(float pulse)
            {
                int now = Environment.TickCount;
                float dt = (driftLastMs == 0) ? 0f : (now - driftLastMs) / 1000f;
                driftLastMs = now;
                if (dt < 0f) dt = 0f; else if (dt > 0.2f) dt = 0.2f;   // acota saltos (tras un hueco largo, p.ej. saliendo de GPU)
                float tgt = (pulse > 0.02f) ? 1f : 0f;
                driftEnv += (tgt - driftEnv) * (tgt > driftEnv ? Math.Min(1f, dt * 2.5f) : Math.Min(1f, dt * 0.6f));   // ataque medio, caida lenta
                int w = (lw > 0) ? lw : 0, h = (lh > 0) ? lh : 0;
                driftX += driftEnv * DriftVX * dt; if (w > 0) driftX %= w;   // env>=0 -> solo crece; modulo -> [0,w)
                driftY += driftEnv * DriftVY * dt; if (h > 0) driftY %= h;
            }
            private void UpdateDrift(int w, int h, float pulse) { AvanzaDeriva(pulse); }

            // Pincel de cielo en degradado vertical CONTINUO: el degradado abarca TODA la pantalla (SkyFullH)
            // y se desplaza segun donde esta este fondo (SkyOffsetY), para que las secciones no se noten
            // divididas (todo se ve como una sola pieza). Cacheado: se recrea solo al cambiar tamano/posicion/colores.
            private void EnsureSkyBrush(int w, int h, Color top, Color bottom)
            {
                int off = SkyOffsetY;
                int full = (SkyFullH > h) ? SkyFullH : h;
                Color mid = (SkyMid.A == 0)
                    ? Color.FromArgb(255, (top.R + bottom.R) / 2, (top.G + bottom.G) / 2, (top.B + bottom.B) / 2)   // sin mid: punto medio (= 2 paradas)
                    : SkyMid;
                int t = top.ToArgb(), m = mid.ToArgb(), b = bottom.ToArgb();
                if (skyBrush != null && gbW == w && gbOff == off && gbFull == full && gbTop == t && gbMid == m && gbBot == b) return;
                if (skyBrush != null) { skyBrush.Dispose(); skyBrush = null; }
                LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, -off, w, full + 1), top, bottom, LinearGradientMode.Vertical);
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { top, mid, bottom };          // negro arriba -> azul noche en medio -> resplandor/azul abajo
                cb.Positions = new float[] { 0f, CorteDelCielo, 1f };  // donde acaba el negro y empieza el azul (perilla "cuanto negro")
                lgb.InterpolationColors = cb;
                skyBrush = lgb;
                gbW = w; gbOff = off; gbFull = full; gbTop = t; gbMid = m; gbBot = b;
            }

            // Hornea un sprite de resplandor por color (una vez). Caro de crear, baratisimo de estampar despues.
            private static void EnsureGlow()
            {
                if (glowPal != null) return;
                lock (glowLock)
                {
                    if (glowPal != null) return;
                    Bitmap[] g2 = new Bitmap[glowCols.Length];
                    for (int i = 0; i < glowCols.Length; i++) g2[i] = BakeGlow(48, 185, glowCols[i]);   // glow FUERTE por color
                    glowPal = g2;
                }
            }

            // Glow del MISMO COLOR que la estrella: elige el sprite pre-horneado mas cercano (sin matriz por estrella).
            private static Bitmap GlowFor(int r, int g, int b)
            {
                Bitmap[] gp = glowPal;
                if (gp == null) return null;
                int best = 0, bd = int.MaxValue;
                for (int i = 0; i < glowCols.Length; i++)
                {
                    int dr = r - glowCols[i].R, dg = g - glowCols[i].G, db = b - glowCols[i].B;
                    int d = dr * dr + dg * dg + db * db;
                    if (d < bd) { bd = d; best = i; }
                }
                return gp[best];
            }

            private static Bitmap BakeGlow(int size, int peakAlpha, Color color)
            {
                Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                using (GraphicsPath gp = new GraphicsPath())
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    gp.AddEllipse(0, 0, size, size);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterPoint = new PointF(size / 2f, size / 2f);
                        pgb.CenterColor = Color.FromArgb(peakAlpha, color.R, color.G, color.B);   // nucleo del color de la estrella
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, color.R, color.G, color.B) };   // se desvanece a transparente
                        g.FillEllipse(pgb, 0, 0, size, size);
                    }
                }
                return bmp;
            }

            // LAS CUATRO RAYITAS CRUZADAS de las estrellas mas brillantes. No las hace la estrella: las hace
            // el diafragma de la camara, y son justo lo que hace que una foto del cielo parezca una foto.
            //
            // Las lleva un punado y nada mas -las que marca Ensure-, porque repartidas por todas el cielo
            // parece purpurina. Tres rombos encajados de menos a mas cortos y mas opacos: sale una raya que
            // se afila hacia fuera sin tener que degradar nada.
            private void DrawRayos(Graphics g, SolidBrush b, float px, float py, float R, Color col, float brillo, int kind)
            {
                // La MEDIANA lleva las rayitas mas cortas y bastante mas tenues que la grande. Esa diferencia
                // es la que evita el efecto purpurina: si todas las llevaran igual de marcadas, sesenta
                // estrellas con cruz serian sesenta chispas y no un cielo. Asi la mediana se lee como una
                // estrella pequena de verdad y la grande sigue mandando en la escena.
                float largo = R * ((kind == 2) ? 4.2f : 3.0f), ancho = R * 0.22f;
                int tope = (kind == 2) ? 120 : 62;
                for (int paso = 0; paso < 3; paso++)
                {
                    float L = largo * (1f - paso * 0.30f), A = ancho * (1f + paso * 1.1f);
                    int alfa = (int)((34 + paso * 26) * brillo); if (alfa > tope) alfa = tope; if (alfa < 1) continue;
                    b.Color = Color.FromArgb(alfa, col);
                    spk[0] = new PointF(px - L, py); spk[1] = new PointF(px, py - A); spk[2] = new PointF(px + L, py); spk[3] = new PointF(px, py + A);
                    g.FillPolygon(b, spk);
                    spk[0] = new PointF(px, py - L); spk[1] = new PointF(px + A, py); spk[2] = new PointF(px, py + L); spk[3] = new PointF(px - A, py);
                    g.FillPolygon(b, spk);
                }
            }
            // El NUCLEO: chiquito y tirando a blanco. Es lo que hace que una estrella "queme" en vez de
            // parecer una pelusa: el color vive en el halo, y el centro casi siempre se ve blanco.
            private void DrawNucleo(Graphics g, SolidBrush b, float px, float py, float R, int cr, int cg, int cb, int a)
            {
                int nr = cr + 70; if (nr > 255) nr = 255;
                int ng = cg + 70; if (ng > 255) ng = 255;
                int nb = cb + 70; if (nb > 255) nb = 255;
                b.Color = Color.FromArgb(a, nr, ng, nb);
                float d = R * 1.1f; if (d < 1.2f) d = 1.2f;
                g.FillEllipse(b, px - d * 0.5f, py - d * 0.5f, d, d);
            }

            // CUANTO HALO lleva cada estrella, y no es el mismo para todas. Las GRANDES son las
            // protagonistas del cielo y con el halo generoso se leen como luces de verdad en vez de como
            // pegatinas. A las MEDIANAS ese mismo halo las convertia en pompas: un disco borroso de quince
            // pixeles con un punto blanco dentro, que es justo lo que se veia mal. Ahora lo llevan apretado,
            // y lo que las hace estrella son sus rayitas, no el borron. (El polvo del fondo no lleva halo:
            // va como punto de un pixel y punto.) Los numeros salieron de dibujar el cielo y mirarlo.
            private static float HaloDe(int kind) { return (kind == 2) ? 1.15f : 0.60f; }

            private void Ensure(int w, int h)
            {
                int bucket = 0;   // patron FIJO: regenerarlo cada minuto obligaba a re-hornear ~120 estrellas (glow+destello) -> tiron periodico en la rocola
                if (stars != null && sw == w && sh == h && sBucket == bucket) return;
                sw = w; sh = h; sBucket = bucket;
                Random r = new Random((20260620 + bucket * 40503) & 0x7FFFFFFF);   // seed FIJO (bucket=0): el patron ya no rota por minuto (regenerarlo daba un tiron periodico)
                Random rFranja = new Random(0x5A5A3C3C);   // azar APARTE, solo para apartar estrellas de las franjas de texto (ver QuietBands)
                // CUANTAS. Cuatro veces mas que antes, y casi todas diminutas: un cielo de verdad tiene un
                // punado de estrellas fuertes y cientos que apenas se adivinan, y eso es lo que da sensacion
                // de fondo. No cuesta lo que parece: el POLVO -tres cuartas partes- se hornea UNA vez en el
                // cielo y no entra en las ocho capas de titileo (ver BakeStarFrame), asi que lo que se hornea
                // ocho veces sigue siendo mas o menos lo mismo que antes.
                int n = (int)(((double)(w * h) / 1700) * Density);
                int lo = (int)(300 * Density); if (lo < 60) lo = 60;
                if (n < lo) n = lo; if (n > 1400) n = 1400;

                // DE QUE COLOR ES UNA ESTRELLA. Las de verdad van de blanco-azulado a blanco, amarillo y
                // naranja: no hay estrellas verdes ni violetas, y la inmensa mayoria de las que se ven son
                // casi blancas. Con la paleta de rotulador de antes el cielo parecia confeti.
                Color[] palTemp = {
                    Color.FromArgb(155, 176, 255), Color.FromArgb(170, 191, 255),   // 0-1  azul-blanco (las mas calientes)
                    Color.FromArgb(202, 215, 255), Color.FromArgb(228, 235, 255),   // 2-3  blanco-azul
                    Color.FromArgb(248, 247, 255), Color.FromArgb(255, 255, 250),   // 4-5  blancas
                    Color.FromArgb(255, 244, 234), Color.FromArgb(255, 237, 205),   // 6-7  blanco-amarillo (como el Sol)
                    Color.FromArgb(255, 218, 165), Color.FromArgb(255, 199, 142),   // 8-9  naranjas
                    Color.FromArgb(255, 178, 128)                                    // 10   naranja hondo
                };

                stars = new Star[n];
                // DONDE SE PONEN.
                //
                // Un cielo de verdad no esta espolvoreado con regularidad de papel pintado: tiene grumos,
                // parejas y zonas mas vacias. Por eso una parte cae alrededor de unos pocos centros y el
                // resto suelta por cualquier sitio. Pero eso tenia DOS fallos, y los dos se veian, sobre
                // todo en pantalla ancha:
                //
                // 1) LAS HILERAS DE LOS BORDES. La que caia fuera de la pantalla se PEGABA al borde en vez
                //    de volver a tirarse. Como los grumos son campanas, muchas caian fuera... y acababan
                //    todas en la misma linea: medido, el 15% del cielo formaba cuatro hileras pegadas a los
                //    cantos. Ahora la que cae fuera se vuelve a tirar, y si tras varios intentos sigue
                //    fuera se pone suelta en cualquier sitio.
                //
                // 2) LOS GRUMOS ESTIRADOS. El tamano del grumo iba en fraccion de la pantalla, asi que en
                //    una pantalla ancha salia estirado a lo ancho: en 2560x1080 el cielo se repartia el
                //    doble de ancho que de alto, o sea bandas horizontales en vez de grumos. Ahora el radio
                //    se mide en PIXELES y el grumo sale redondo se ponga la pantalla como se ponga.
                //
                // Y ademas hay menos estrellas atadas a los grumos que antes, para que el cielo quede
                // repartido de forma pareja y los grumos se noten sin amontonar.
                int nc = 10 + r.Next(5);
                float[] ccx = new float[nc], ccy = new float[nc], ccr = new float[nc];
                for (int c = 0; c < nc; c++)
                {
                    // Los centros no se pegan al canto: un grumo con el centro en el borde deja media
                    // campana fuera y vuelve a amontonar todo en esa orilla.
                    ccx[c] = 0.10f + (float)r.NextDouble() * 0.80f;
                    ccy[c] = 0.10f + (float)r.NextDouble() * 0.80f;
                    ccr[c] = h * (0.10f + (float)r.NextDouble() * 0.14f);   // RADIO EN PIXELES
                }
                int grandes = 0;
                for (int i = 0; i < n; i++)
                {
                    Star s = new Star();
                    float px = 0f, py = 0f;
                    for (int intento = 0; ; intento++)
                    {
                        if (intento < 6 && r.Next(100) < 25)
                        {
                            int c = r.Next(nc);
                            // El mismo radio en pixeles en los dos ejes -> el grumo sale redondo.
                            px = ccx[c] + Gauss(r) * ccr[c] / w;
                            py = ccy[c] + Gauss(r) * ccr[c] / h;
                        }
                        else { px = (float)r.NextDouble(); py = (float)r.NextDouble(); }
                        if (px >= 0.004f && px <= 0.996f && py >= 0.004f && py <= 0.996f) break;
                    }
                    // Si cayo detras de una de las dos franjas de texto, se vuelve a tirar CON EL AZAR DE AL
                    // LADO, que no corre la fila del azar principal (ver QuietBands).
                    if (QuietBands && EnFranja(py))
                    {
                        for (int t = 0; t < TirosFranja; t++)
                        {
                            float nx = (float)rFranja.NextDouble(), ny = (float)rFranja.NextDouble();
                            if (nx < 0.004f || nx > 0.996f || ny < 0.004f || ny > 0.996f) continue;
                            px = nx; py = ny;
                            if (!EnFranja(py)) break;
                        }
                    }
                    s.X = px; s.Y = py;

                    // LO BRILLANTE QUE ES, con una curva bien inclinada: casi todas debilisimas, unas pocas
                    // fuertes. De aqui salen el tamano, el brillo y a que grupo va (polvo / mediana / grande).
                    double m = Math.Pow(r.NextDouble(), 4.6);
                    // DE QUE GRUPO ES. El corte de las medianas sube de 0.34 a 0.55: antes una de cada seis
                    // estrellas era "mediana" -unas noventa en pantalla- y eran justo las que se veian como
                    // pompas. Ahora son la mitad, y las que dejan de serlo pasan a ser polvo de un pixel,
                    // que es como se ve de verdad la inmensa mayoria del cielo.
                    s.Kind = (m > 0.80) ? 2 : (m > 0.55) ? 1 : 0;
                    // Y la mediana ademas ENCOGE (era 4-5, ahora 3): lo que la hace estrella son sus
                    // rayitas, no su bulto. El polvo se queda en un pixel clavado.
                    s.Size = (s.Kind == 2) ? (5 + (int)(m * 4)) : (s.Kind == 1) ? 3 : 1;
                    // TODA la que se vea lleva rayitas. Antes las llevaban diez o doce y las otras noventa se
                    // quedaban de bola redonda: una bola redonda solo pasa por estrella si es diminuta, y
                    // esas no lo eran. Con las rayitas escaladas por tamano (ver DrawRayos) el cielo entero
                    // se lee como estrellas en vez de como puntos gordos.
                    s.Rayos = (s.Kind != 0);
                    // El suelo del brillo no puede ser tan bajo: con 0.16 la mitad del cielo quedaba en alfa
                    // 40 sobre un punto de un pixel, o sea invisible, y el cielo se veia mas vacio que antes
                    // teniendo el doble de estrellas.
                    float bri = (float)(0.26 + m * 0.74);
                    s.Bri = (byte)(bri * 255f);
                    if (s.Kind == 2) grandes++;
                    // A QUE FRECUENCIA late: las grandes con los graves (bombo), las medianas entre medios y
                    // agudos. El polvo no late (ni reacciona ni titila: vive horneado en el cielo).
                    s.Band = (s.Kind == 2) ? (byte)0 : (byte)(1 + r.Next(2));

                    // El color por temperatura, con la curva de siempre: casi todas blancas. Y una parte del
                    // polvo se tine hacia el genero que suena, que es lo que ata el cielo con la musica.
                    int q = r.Next(100);
                    int idx = (q < 14) ? r.Next(2) : (q < 34) ? 2 + r.Next(2) : (q < 68) ? 4 + r.Next(2)
                            : (q < 88) ? 6 + r.Next(2) : (q < 97) ? 8 + r.Next(2) : 10;
                    Color ct = palTemp[idx];
                    s.R = ct.R; s.G = ct.G; s.B = ct.B; s.Tint = 0f;
                    if (!Varied && s.Kind == 0 && r.Next(100) < 22)
                    { s.R = 255; s.G = 255; s.B = 255; s.Tint = 0.45f + (float)r.NextDouble() * 0.55f; }

                    s.Off = (float)(r.NextDouble() * Math.PI * 2);
                    s.Spd = 0.6f + (float)r.NextDouble() * 1.6f;   // titileo LENTO
                    stars[i] = s;
                }
                if (sb == null) sb = new SolidBrush(Color.White);
                if (sp == null) sp = new Pen(Color.White, 2.2f);
            }

            // Un numero al azar con forma de campana (Box-Muller): sirve para que las estrellas caigan
            // ALREDEDOR de un centro, apinandose cerca y ralas lejos, que es como se agrupan de verdad.
            private static float Gauss(Random r)
            {
                double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
                return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            }

            // Patron FIJO (sin fundido): el campo de estrellas ya no cambia cada minuto. Antes el fundido +
            // la regeneracion provocaban un tiron periodico (re-hornear ~120 estrellas) y 3s de repintados
            // extra por minuto, costoso en el CPU lento de la rocola. Se mantiene 1 (sin atenuar) siempre.
            private static float StarFade() { return 1f; }

            public void Draw(Graphics g, int w, int h, Color skyTop, Color skyBottom, Color tint, double phase, bool shooting, float pulse)
            {
                if (w < 4 || h < 4) return;
                Ensure(w, h);
                int now = Environment.TickCount;
                int dt = now - lastBuild;
                bool needNew = cache == null || cw != w || ch != h;
                // En repintados PARCIALES (p.ej. solo la casilla del cubo que gira encima) NO reconstruimos
                // el cuadro entero: basta copiar el cache. Asi el +framerate (~24fps) no desperdicia CPU
                // redibujando ~120 estrellas para una franja minima; el fondo completo se refresca en los
                // repintados completos (cuando el recorte cubre toda la vista: buscador y atraccion cada cuadro).
                RectangleF clip = g.ClipBounds;
                bool partial = !needNew && (clip.Width < w - 1 || clip.Height < h - 1);
                bool rebuild = needNew
                    || (!partial && (cSky != skyTop.ToArgb() || cSky2 != skyBottom.ToArgb() || cTint != tint.ToArgb() || dt >= RefrescoMs || dt < 0));
                if (rebuild)
                {
                    if (needNew)
                    {
                        if (cache != null) cache.Dispose();
                        cache = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                        cw = w; ch = h;
                    }
                    using (Graphics cg = Graphics.FromImage(cache))
                        RenderInto(cg, w, h, skyTop, skyBottom, tint, phase, pulse);
                    cSky = skyTop.ToArgb(); cSky2 = skyBottom.ToArgb(); cTint = tint.ToArgb(); lastBuild = now;
                }
                g.DrawImageUnscaled(cache, 0, 0);
                // LAS FUGACES VAN AQUI, EN VIVO, no dentro de lo que se hornea. Una fugaz cruza la pantalla en
                // un segundo: si viajara dentro del horneado avanzaria a saltos, uno por cada vez que se rehace
                // el cielo. Fuera, va fluida aunque el cielo se rehaga de tarde en tarde, que es justo lo que
                // permite rehacerlo de tarde en tarde. (Es la misma division que usa DrawReactive.)
                if (shooting) DrawShoots(g, w, h);
            }

            // CADA CUANTO SE REHACE EL CIELO cuando se dibuja en vivo (o sea, en la pantalla de atraccion:
            // el resto del programa copia capas ya horneadas). Rehacerlo es dibujar las seiscientas estrellas
            // una a una, y a 24 veces por segundo eso era la mayor parte del gasto de la pantalla que MAS
            // tiempo pasa encendida en un bar: la de reposo. Lo unico que se pierde rehaciendolo menos a
            // menudo es que las estrellas titilan mas despacio, y 200 ms es exactamente el ritmo al que
            // titilan en todo el resto del programa (ver CurrentStarLayerIndex), asi que ademas va parejo.
            public int RefrescoMs = 40;

            // Dibuja un cuadro completo del starfield (cielo + estrellas + fugaces) sobre el bitmap cache.
            private void RenderInto(Graphics g, int w, int h, Color skyTop, Color skyBottom, Color tint, double phase, float pulse)
            {
                EnsureSkyBrush(w, h, skyTop, skyBottom);
                g.FillRectangle(skyBrush, 0, 0, w, h);
                pulse *= Dim;                       // atenuado mientras se navega: el beat tambien baja
                double boost = pulse * 0.62;        // el audio sube el brillo de las estrellas (medianas/grandes)
                int armUp = (int)(pulse * 7.0);     // y agranda las puntas del destello con el beat
                float fade = StarFade() * Dim;      // fundido out/in del patron (cada minuto) x atenuacion al navegar
                EnsureGlow();                       // ESTETICA: sprites de resplandor (horneados una vez)
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.SmoothingMode = SmoothingMode.AntiAlias;   // ESTETICA: el sparkle se ve liso (una sola pieza)
                float gfade = fade < 0f ? 0f : (fade > 1f ? 1f : fade);   // el glow encoge a 0 en el parpadeo del patron (sync con los nucleos)
                UpdateDrift(w, h, pulse);           // deriva con la musica (RenderInto solo corre en reconstruccion completa, ~24fps)
                int dx = (int)driftX, dy = (int)driftY;
                for (int i = 0; i < stars.Length; i++)
                {
                    Star s = stars[i];
                    bool reactive = (s.Kind != 0);   // BEAT (audio) + glow SOLO en medianas (1) y grandes (2); los puntos NO
                    double tw = (0.70 + 0.42 * (0.5 + 0.5 * Math.Sin(phase * s.Spd + s.Off)) + (reactive ? boost : 0.0)) * fade;
                    int a = (int)(tw * s.Bri); if (a > 255) a = 255; if (a < 0) a = 0;
                    int cr, cg, cb;
                    if (s.Tint > 0f) { cr = (int)(255 + (tint.R - 255) * s.Tint); cg = (int)(255 + (tint.G - 255) * s.Tint); cb = (int)(255 + (tint.B - 255) * s.Tint); }
                    else { cr = s.R; cg = s.G; cb = s.B; }
                    int px = (int)(s.X * w) + dx; if (px >= w) px -= w;   // + deriva con la musica (envuelve)
                    int py = (int)(s.Y * h) + dy; if (py >= h) py -= h;
                    // UN PUNTO DE LUZ: halo del mismo color detras y un nucleo chiquito tirando a blanco. El
                    // beat agranda el halo solo en medianas/grandes; el polvo va como punto fijo, sin halo.
                    float bri = s.Bri / 255f;
                    if (reactive)
                    {
                        int gd = (int)((s.Size * 3 + 6 + (int)(pulse * 10)) * gfade * HaloDe(s.Kind));   // el beat agranda el glow y el fundido lo encoge
                        Bitmap gb = GlowFor(cr, cg, cb);
                        if (gb != null && gd > 0) g.DrawImage(gb, new Rectangle(px - gd / 2, py - gd / 2, gd, gd));
                        if (s.Rayos) DrawRayos(g, sb, px, py, s.Size + armUp, Color.FromArgb(cr, cg, cb), bri * (float)tw, s.Kind);
                        DrawNucleo(g, sb, px, py, s.Size, cr, cg, cb, a);
                    }
                    else { sb.Color = Color.FromArgb(a, cr, cg, cb); float ds = s.Size < 1 ? 1 : s.Size; g.FillEllipse(sb, px - ds / 2f, py - ds / 2f, ds, ds); }
                }
            }

            // Fondo del formulario (franjas): cielo cacheado + estrellas TITILANTES por cuadros horneados.
            public void DrawCached(Graphics g, int w, int h, Color skyTop, Color skyBottom)
            {
                if (w < 4 || h < 4) return;
                EnsureLayers(w, h);          // arma el patron de estrellas: el cielo necesita el polvo ya repartido
                EnsureSkyCache(w, h, skyTop, skyBottom);
                BlitSkyAndStars(g, w, h);
            }

            // Una pasada de la nebulosa: se estira su dibujo pequeno y se le pone el color con una matriz.
            private void PintaNebulosa(Graphics sg, int FW, int FH, Color c, float alfa)
            {
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix00 = c.R / 255f; cm.Matrix11 = c.G / 255f; cm.Matrix22 = c.B / 255f; cm.Matrix33 = alfa;
                    ia.SetColorMatrix(cm);
                    sg.DrawImage(nebMask, new Rectangle(0, 0, FW, FH), 0, 0, nebMask.Width, nebMask.Height, GraphicsUnit.Pixel, ia);
                }
            }

            // LA NEBLINA DE LA VIA LACTEA Y LAS NEBULOSAS.
            //
            // Son manchas grandes y muy suaves, o sea justo lo que se puede dibujar CHIQUITO y estirar sin
            // que se note: se hornean a un CUARTO de tamano (en 1024x768, 256x192 = 190 KB cada una) y se
            // estiran al copiarlas. Dibujarlas a tamano real serian trescientas manchas de gradiente radial
            // en cada cambio de cancion; asi son doscientas manchas UNA vez, al arrancar, y luego una copia.
            private Bitmap hazeMask, nebMask;
            private Bitmap hazeFull;

            // La Via Lactea no cambia de color: escalarla solo al cambiar geometria.
            // SourceCopy conserva la transparencia; la composicion final sigue siendo SourceOver.
            private void PintaViaLactea(Graphics g, int w, int h)
            {
                if (hazeMask == null) return;
                if (hazeFull == null || hazeFull.Width != w || hazeFull.Height != h)
                {
                    Bitmap next = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    try
                    {
                        using (Graphics ng = Graphics.FromImage(next))
                        {
                            ng.CompositingMode = CompositingMode.SourceCopy;
                            ng.InterpolationMode = InterpolationMode.Bilinear;
                            ng.DrawImage(hazeMask, new Rectangle(0, 0, w, h), 0, 0, hazeMask.Width, hazeMask.Height, GraphicsUnit.Pixel);
                        }
                    }
                    catch { next.Dispose(); throw; }
                    if (hazeFull != null) hazeFull.Dispose();
                    hazeFull = next;
                }
                g.DrawImage(hazeFull, new Rectangle(0, 0, w, h));
            }
            private int nblW = -1, nblH = -1;
            // El dibujito del grano: 64x64, uno para toda la rocola. Se hace una vez y se repite al estamparlo.
            private static Bitmap ruidoTeja;
            private static void EnsureRuido() { if (ruidoTeja == null) ruidoTeja = BakeRuido(); }

            // LA LUNA. El dibujo se hornea una vez por tamano de pantalla (ver Form1.BakeLuna) y luego esto
            // es UN pegado de unos 180 pixeles de lado por cuadro, que es lo unico de esta tanda que cabe en
            // una maquina sin tarjeta grafica.
            //
            // EL TINTE DEL AIRE VA HORNEADO, NO APLICADO AL PEGAR. Ahi arriba, en CapaAtenuada, esta escrito
            // por que: pegar con una matriz de color obliga a la libreria de dibujo a pasar por CADA pixel y
            // cuesta VEINTE VECES lo que pegar el dibujo tal cual. Se hizo primero de la forma facil y se
            // midio: la luna costaba 0.33 ms por cuadro -mas que armar las mil cuatrocientas estrellas de la
            // otra pared entera- por tenir treinta mil pixeles en cada cuadro.
            //
            // Y no hace falta: la luna casi no cambia de altura, asi que se hornea TENIDA una vez y se reusa
            // cuadro tras cuadro. Es la misma cuenta, hecha una vez en vez de veinticuatro veces por segundo.
            // La clave lleva la altura en escalones de cuatro pixeles: por menos que eso el tinte no cambia
            // ni un nivel de 255.
            private Bitmap luna; private int lunaDe = -1;
            private Bitmap lunaTenida; private int lunaTenidaKey = -1;
            private void PintaLuna(Graphics g, int FW, int FH, int offX, int offY)
            {
                if (!ConLuna || FH < 64) return;
                if (luna == null || lunaDe != FH)
                {
                    if (luna != null) luna.Dispose();
                    if (lunaTenida != null) { lunaTenida.Dispose(); lunaTenida = null; }
                    lunaTenidaKey = -1;
                    luna = BakeLuna(FH); lunaDe = FH;
                }
                // Se pasea con el mismo reloj lento de la via lactea, y acotada: nunca se va de su sitio.
                float mx = FW * LunaMenea, my = FH * LunaMenea;
                float cx = FW * LunaX + (float)Math.Sin(HazFase) * mx;
                float cy = FH * LunaY + (float)Math.Cos(HazFase) * my;

                int key = ((int)cy) / 4;
                if (lunaTenida == null || lunaTenidaKey != key)
                {
                    if (lunaTenida == null)
                        lunaTenida = new Bitmap(luna.Width, luna.Height, PixelFormat.Format32bppPArgb);
                    float fr, fg, fb;
                    TinteDelAire(cy, FH, out fr, out fg, out fb);
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix00 = fr; cm.Matrix11 = fg; cm.Matrix22 = fb;
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        using (Graphics lg = Graphics.FromImage(lunaTenida))
                        {
                            lg.CompositingMode = CompositingMode.SourceCopy;   // copia exacta, sin mezclar con lo que hubiera
                            lg.DrawImage(luna, new Rectangle(0, 0, luna.Width, luna.Height),
                                         0, 0, luna.Width, luna.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                    lunaTenidaKey = key;
                }
                g.DrawImageUnscaled(lunaTenida, (int)(cx - luna.Width / 2f) - offX, (int)(cy - luna.Height / 2f) - offY);
            }
            private void EnsureNieblas(int FW, int FH)
            {
                int mw = FW / 4, mh = FH / 4;
                if (mw < 8) mw = 8; if (mh < 8) mh = 8;
                if (hazeMask != null && nblW == mw && nblH == mh) return;
                if (hazeFull != null) { hazeFull.Dispose(); hazeFull = null; }
                if (hazeMask != null) { hazeMask.Dispose(); hazeMask = null; }
                if (nebMask != null) { nebMask.Dispose(); nebMask = null; }
                nblW = mw; nblH = mh;
                Random r = new Random(20260620);   // fijo: la misma via lactea siempre, no una distinta por arranque

                hazeMask = new Bitmap(mw, mh, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(hazeMask))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    // La banda cruza en diagonal, de abajo-izquierda a arriba-derecha, y se hace de muchas
                    // manchas tenues encimadas: asi tiene grumos y bordes irregulares en vez de ser una franja.
                    float x0 = -mw * 0.08f, y0 = mh * 0.86f, x1 = mw * 1.08f, y1 = mh * 0.06f;
                    for (int i = 0; i < 240; i++)
                    {
                        float t = (float)r.NextDouble();
                        float ancho = mh * (0.075f + 0.045f * (float)Math.Sin(t * 3.1));
                        float x = x0 + (x1 - x0) * t + Gauss(r) * ancho * 0.55f;
                        float y = y0 + (y1 - y0) * t + Gauss(r) * ancho;
                        // Y SE APAGA HACIA ABAJO. Abajo del todo el cielo lleva el resplandor del horizonte,
                        // y una neblina blanca encima de el no se lee como via lactea: se lee como una mancha
                        // gris. Ademas es lo que pasa de verdad, que las luces del pueblo se la comen.
                        float fy = 1f - y / mh; if (fy < 0f) fy = 0f; if (fy > 1f) fy = 1f;
                        int al = (int)(6f * (0.10f + 0.90f * fy * fy)); if (al < 1) continue;
                        Mancha(g, x, y, mh * (0.05f + (float)r.NextDouble() * 0.10f), Color.FromArgb(214, 226, 255), al);
                    }
                    // Y las VETAS DE POLVO que la cruzan: en el cielo de verdad son nubes que tapan lo de
                    // detras, y son las que hacen que la via lactea se lea como algo con volumen.
                    for (int i = 0; i < 24; i++)
                    {
                        float t = (float)r.NextDouble();
                        float x = x0 + (x1 - x0) * t + Gauss(r) * mh * 0.05f;
                        float y = y0 + (y1 - y0) * t + Gauss(r) * mh * 0.055f;
                        Mancha(g, x, y, mh * (0.04f + (float)r.NextDouble() * 0.085f), Color.FromArgb(8, 10, 26), 34);
                    }
                }

                nebMask = new Bitmap(mw, mh, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(nebMask))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    // Van en la MITAD DE ARRIBA del cielo a proposito: abajo el degradado ya lleva el
                    // resplandor del genero, y una nebulosa del mismo color encima de el no se lee como una
                    // nebulosa, se lee como suciedad.
                    for (int k = 0; k < 3; k++)
                    {
                        float cx = mw * (0.14f + (float)r.NextDouble() * 0.72f);
                        float cy = mh * (0.10f + (float)r.NextDouble() * 0.42f);
                        for (int i = 0; i < 20; i++)
                            Mancha(g, cx + Gauss(r) * mw * 0.075f, cy + Gauss(r) * mh * 0.08f,
                                   mh * (0.07f + (float)r.NextDouble() * 0.14f), Color.White, AlfaNeb(4));
                        Mancha(g, cx, cy, mh * 0.07f, Color.White, AlfaNeb(7));   // el corazon, un pelin mas encendido
                    }
                }
            }

            // El alfa de una mancha de nebulosa a la fuerza elegida (ver NebFuerza). La via lactea NO pasa
            // por aqui: esa es blanca y de siempre, y no tiene nada que ver con el color del genero.
            private static int AlfaNeb(int alfa)
            {
                int a = (int)(alfa * NebFuerza + 0.5f);
                return (a > 255) ? 255 : a;
            }

            // Una mancha de luz redonda que se desvanece hacia fuera: la pieza con la que estan hechas la
            // neblina y las nebulosas.
            private static void Mancha(Graphics g, float cx, float cy, float rad, Color c, int alfaCentro)
            {
                if (rad < 1f) rad = 1f;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(cx - rad, cy - rad, rad * 2, rad * 2);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterPoint = new PointF(cx, cy);
                        pgb.CenterColor = Color.FromArgb(alfaCentro, c);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                        g.FillEllipse(pgb, cx - rad, cy - rad, rad * 2, rad * 2);
                    }
                }
            }

            // Hornea el cielo (degradado) al tamano de la PANTALLA COMPLETA (FullW x FullH) y lo reutiliza. Cada
            // fondo copia solo su ventana (en BlitSkyAndStars, con offset), asi el degradado es CONTINUO entre las
            // secciones sin costuras. Copiar un bitmap es mas barato que rellenar un degradado de 3 paradas por
            // cuadro (clave en alta resolucion). Se re-hornea solo al cambiar tamano de pantalla o colores (genero).
            private int saltados;   // cuantos cuadros seguidos se ha dejado el cielo como estaba (ver abajo)
            private void EnsureSkyCache(int w, int h, Color top, Color bottom)
            {
                int FW = FullW(w), FH = FullH(h);
                Color mid = (SkyMid.A == 0)
                    ? Color.FromArgb(255, (top.R + bottom.R) / 2, (top.G + bottom.G) / 2, (top.B + bottom.B) / 2)   // sin mid: punto medio
                    : SkyMid;
                Color acento = Acento;
                // El acento entra en la firma: si no, al cambiar de genero el cielo no se re-hornearia y las
                // nebulosas se quedarian del color de la cancion anterior. Y los dos colores de las nebulosas
                // tambien, por lo mismo: son los que se van cruzando.
                int t = top.ToArgb(), m = mid.ToArgb(), b = bottom.ToArgb() ^ (acento.A == 0 ? 0 : acento.ToArgb() * 31);
                t ^= (NebUno.A == 0 ? 0 : NebUno.ToArgb() * 7) ^ (NebDos.A == 0 ? 0 : NebDos.ToArgb() * 13);
                if (skyCache != null && scW == FW && scH == FH && fscTop == t && fscMid == m && fscBot == b) { saltados = 0; return; }
                // EL CIELO NO SE REHORNEA EN CADA CUADRO DEL DESLIZAMIENTO.
                //
                // Al cambiar de cancion, el color del genero se desliza del anterior al nuevo en unos
                // veinticinco cuadros. Cada uno es un color distinto, y con un color distinto habia que
                // rehornear el cielo ENTERO -el degradado a pantalla completa, la via lactea y las nebulosas
                // estiradas desde su dibujo pequeno, y el polvo-. Eso son 6 ms a 1024x768, 16 a 1920x1080 y
                // 27 a 2560x1440... por cuadro. En pantalla grande, cada cambio de cancion pedia casi medio
                // segundo de trabajo de mas justo cuando la rocola tiene que arrancar la cancion siguiente.
                //
                // Se rehornea uno de cada cuatro. Durante ese segundo el FONDO se desliza a saltitos de
                // cuatro cuadros -en un degradado liso y oscuro no se nota- y todo lo demas (pastillas,
                // bordes, letreros) sigue deslizandose cuadro a cuadro. Y lo importante: NO se redondea
                // ningun color. En cuanto el deslizamiento para, el siguiente horneado es con el color
                // EXACTO, asi que el cielo acaba siendo exactamente el que le toca a esa cancion.
                // (Se probo redondear el color a escalones en vez de esto: se veia. El degradado lleva
                // colores muy oscuros y ahi un escalon de nada es un cambio grande en proporcion.)
                if (skyCache != null && scW == FW && scH == FH && saltados < 3) { saltados++; return; }
                saltados = 0;
                if (skyCache == null || scW != FW || scH != FH)
                {
                    if (skyCache != null) skyCache.Dispose();
                    skyCache = new Bitmap(FW, FH, PixelFormat.Format32bppPArgb);
                    scW = FW; scH = FH;
                }
                EnsureNieblas(FW, FH);
                using (Graphics sg = Graphics.FromImage(skyCache))
                {
                    using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, FW, FH + 1), top, bottom, LinearGradientMode.Vertical))
                    {
                        ColorBlend cb = new ColorBlend(3);
                        cb.Colors = new Color[] { top, mid, bottom };          // negro arriba -> azul noche en medio -> resplandor/genero abajo
                        cb.Positions = new float[] { 0f, CorteDelCielo, 1f };  // ver la perilla "cuanto negro"
                        lgb.InterpolationColors = cb;
                        sg.FillRectangle(lgb, 0, 0, FW, FH);
                    }
                    sg.InterpolationMode = InterpolationMode.Bilinear;
                    // LAS NEBULOSAS, del color de lo que suena. Su dibujo esta horneado en blanco a un cuarto
                    // de tamano y aqui solo se estira y se tine con una matriz de color: al cambiar de cancion
                    // esto es UNA copia, no volver a dibujar ochenta manchas.
                    if (nebMask != null)
                    {
                        // Con el color del GENERO, no con el suelo del degradado: ese es un marron oscuro ya
                        // mezclado con el cielo, y tenir con el solo ensuciaba.
                        // Y el color se lleva a medias con un violeta azulado. El del genero a secas es
                        // demasiado terroso para una nebulosa; mezclado, se sigue notando de que color va la
                        // cancion pero parece cielo.
                        //
                        // Y SON DOS: la del genero y la de la caratula, una encima de otra y cada una a poco
                        // mas de media tinta, para que donde se encimen salga la mezcla de los dos colores. Es
                        // la misma idea que en el camino de tarjeta grafica; lo que alli ademas se mueve de
                        // sitio, aqui no puede: el cielo va horneado.
                        if (NebUno.A != 0 && NebDos.A != 0)
                        {
                            PintaNebulosa(sg, FW, FH, NebUno, 1f);
                            PintaNebulosa(sg, FW, FH, NebDos, 1f);
                        }
                        else
                        {
                            Color a0 = (acento.A == 0) ? bottom : acento;
                            PintaNebulosa(sg, FW, FH, ColorNebulosa(a0), 1f);
                        }
                    }
                    // LA VIA LACTEA: la banda de neblina, tambien horneada a un cuarto y estirada. Va del
                    // mismo blanco azulado siempre, que es de lo que esta hecha de verdad.
                    PintaViaLactea(sg, FW, FH);
                    // EL POLVO YA NO VA AQUI: ver BakeStarFrame. Horneado en el cielo se quedaba clavado
                    // mientras el resto del campo se desliza, y eso -mas de la mitad de las estrellas quietas
                    // y el resto moviendose- se ve peor que si no se moviera nada.
                    //
                    // Y AL FINAL, EL GRANO que deshace las franjas del degradado (ver BakeRuido). Va el
                    // ULTIMO a proposito: tiene que caer encima del degradado, de la via lactea y de las
                    // nebulosas, que son las tres cosas que hacen bandas. Y aqui sale gratis: es un relleno
                    // que se hace una vez con el cielo, no algo que haya que repetir en cada cuadro.
                    EnsureRuido();
                    if (ruidoTeja != null)
                        using (TextureBrush tb = new TextureBrush(ruidoTeja, WrapMode.Tile))
                            sg.FillRectangle(tb, 0, 0, FW, FH);
                }
                fscTop = t; fscMid = m; fscBot = b;
            }

            // Pinta la VENTANA de este fondo dentro del campo de PANTALLA COMPLETA: cielo OPACO por copia directa
            // (SourceCopy, rapido) + la capa de estrellas del cuadro de titileo ACTUAL encima (SourceOver). Ambos
            // bitmaps son de pantalla completa; se dibujan corridos por (-SkyOffsetX, -SkyOffsetY) y el recorte del
            // pintor deja ver solo su seccion. Como todas las secciones leen el MISMO bitmap a la MISMA posicion de
            // pantalla, las estrellas cruzan los bordes sin cortarse ni desalinearse (todo es UNA SOLA PIEZA).
            // 'advance'=false (repintado PARCIAL: la casilla del cubo que gira) reusa la capa del ultimo repintado
            // completo: si avanzara por reloj, la casilla mostraria un cuadro de titileo distinto al del resto.
            private int shownLayer = -1;   // capa mostrada en el ultimo repintado completo
            private void BlitSkyAndStars(Graphics g, int w, int h) { BlitSkyAndStars(g, w, h, true); }
            private void BlitSkyAndStars(Graphics g, int w, int h, bool advance)
            {
                int offX = SkyOffsetX, offY = SkyOffsetY;
                CompositingMode ocm = g.CompositingMode;
                g.CompositingMode = CompositingMode.SourceCopy;   // cielo opaco: copia directa (sin mezclar alfa)
                g.DrawImageUnscaled(skyCache, -offX, -offY);      // la ventana [offX,offY] cae en (0,0); el recorte del pintor limita el area
                g.CompositingMode = CompositingMode.SourceOver;   // estrellas con transparencia encima
                if (advance || shownLayer < 0 || layers == null || shownLayer >= layers.Length) shownLayer = CurrentStarLayerIndex();
                Bitmap sl = (layers != null && shownLayer >= 0 && shownLayer < layers.Length) ? layers[shownLayer] : null;
                if (sl != null) BlitEstrellas(g, sl, w, h, offX, offY);
                // LA LUNA VA DESPUES DE LAS ESTRELLAS porque esta muchisimo mas cerca que ellas: una
                // estrella por delante de la luna se ve, y se ve mal.
                PintaLuna(g, FullW(w), FullH(h), offX, offY);
                // AQUI IRIA EL VELO DEL HORIZONTE, Y NO ESTA. QUEDA ESCRITO POR QUE.
                //
                // Con tarjeta grafica cada estrella se apaga y se enrojece segun a que altura de la pantalla
                // cae -es lo que hace el aire de verdad, y es lo que le da fondo al cielo- (ver BuildStarVerts,
                // al final de D3D9Star.cs). Ahi sale gratis: la cuenta se hace estrella por estrella dentro de
                // un bucle que ya existia.
                //
                // Aqui no se puede hacer igual. Las estrellas van HORNEADAS en ocho capas que se DESLIZAN, asi
                // que un valor atado a la altura viajaria con la estrella en vez de quedarse quieto en la
                // pantalla, que es justo lo contrario de lo que hace el aire: el aire esta donde esta.
                //
                // La otra forma es un VELO -un degradado del color del propio cielo, con alfa creciendo hacia
                // abajo, pegado encima de las estrellas-. SE ESCRIBIO Y SE MIDIO, horneado a un mapa de bits
                // y pegado de una copia, que es lo mas barato que hay:
                //
                //     a 1024x768   pegar el fondo pasa de 0.55 a 1.10 ms por cuadro
                //     a 1920x1080  pasa de 1.50 a 2.90 ms por cuadro
                //
                // O sea que DUPLICA lo que cuesta pintar el fondo, y en la maquina del bar -que es cuatro o
                // cinco veces mas lenta, y que es justamente la que no tiene tarjeta grafica- eso son sesenta
                // milisegundos de cada mil. No es un efecto que valga ese precio.
                //
                // Y no hay forma barata: el velo tapa el 90% de la pantalla (mas arriba el alfa no llega ni a
                // un nivel de 255), asi que son casi dos millones de pixeles mezclando alfa en cada cuadro.
                // Recortarlo a la franja de abajo ahorra la mitad y deja un canto que se ve.
                g.CompositingMode = ocm;
            }

            // LAS ESTRELLAS, CORRIDAS POR LA DERIVA Y ENVOLVIENDO POR LOS BORDES.
            //
            // El campo de estrellas esta horneado al tamano de la pantalla completa y cada vista copia SU ventana.
            // Al sonar musica el campo entero se desliza despacio en diagonal, asi que la ventana que hay que
            // copiar se corre con el; cuando esa ventana se sale por un borde, lo que falta se toma del otro
            // lado, que es lo que hace que el campo parezca infinito y no tenga principio ni final.
            //
            // Se copian los MISMOS pixeles que antes -como mucho en cuatro trozos en vez de uno, y solo cuando
            // la ventana cae justo encima de la costura-, asi que no cuesta mas que antes.
            private void BlitEstrellas(Graphics g, Bitmap sl, int w, int h, int offX, int offY)
            {
                int FW = sl.Width, FH = sl.Height;
                if (FW < 1 || FH < 1) return;
                int sx = (int)((offX - (int)driftX) % FW); if (sx < 0) sx += FW;
                int sy = (int)((offY - (int)driftY) % FH); if (sy < 0) sy += FH;
                int w1 = FW - sx; if (w1 > w) w1 = w;
                int h1 = FH - sy; if (h1 > h) h1 = h;
                if (Dim < 0.985f) sl = CapaAtenuada(sl);   // atenuado mientras se navega con musica: ver abajo
                Trozo(g, sl, 0, 0, w1, h1, sx, sy);
                if (w1 < w) Trozo(g, sl, w1, 0, w - w1, h1, 0, sy);
                if (h1 < h) Trozo(g, sl, 0, h1, w1, h - h1, sx, 0);
                if (w1 < w && h1 < h) Trozo(g, sl, w1, h1, w - w1, h - h1, 0, 0);
            }
            private void Trozo(Graphics g, Bitmap sl, int dx, int dy, int dw, int dh, int sx, int sy)
            {
                if (dw < 1 || dh < 1) return;
                g.DrawImage(sl, new Rectangle(dx, dy, dw, dh), sx, sy, dw, dh, GraphicsUnit.Pixel);
            }

            // LA COPIA ATENUADA, HORNEADA APARTE.
            //
            // Mientras alguien esta eligiendo cancion con musica sonando, el fondo se atenua para que no le
            // pelee a las portadas. Atenuar AL PEGAR obliga a la libreria de dibujo a pasar por CADA pixel
            // aplicando una matriz de color, y eso cuesta VEINTE VECES lo que copiar la capa tal cual: 5.24 ms
            // contra 0.27 en 1024x768. O sea que la rocola se ponia veinte veces mas pesada justo en el momento
            // en que tiene que ir mas suelta, y en la maquina del bar eso es medio nucleo tirado.
            //
            // La capa de titileo solo cambia cinco veces por segundo y la atenuacion se queda quieta en cuanto
            // termina de bajar, asi que la copia atenuada se hornea UNA vez y se reusa en los cuadros que vienen
            // detras. Se ve exactamente igual: es la misma cuenta, hecha una vez en vez de veinticuatro.
            private Bitmap capaAtenuada; private Bitmap capaAtenuadaDe; private int capaAtenuadaKey = -1;
            private Bitmap CapaAtenuada(Bitmap sl)
            {
                int key = (int)(Dim * 50f);   // mismos escalones que la matriz: no se rehace por un pelo de nada
                if (capaAtenuada != null && capaAtenuadaDe == sl && capaAtenuadaKey == key
                    && capaAtenuada.Width == sl.Width && capaAtenuada.Height == sl.Height) return capaAtenuada;
                if (capaAtenuada == null || capaAtenuada.Width != sl.Width || capaAtenuada.Height != sl.Height)
                {
                    if (capaAtenuada != null) capaAtenuada.Dispose();
                    capaAtenuada = new Bitmap(sl.Width, sl.Height, PixelFormat.Format32bppPArgb);
                }
                EnsureDimIA();
                using (Graphics cg = Graphics.FromImage(capaAtenuada))
                {
                    cg.CompositingMode = CompositingMode.SourceCopy;   // copia exacta, sin mezclar con lo que hubiera
                    cg.DrawImage(sl, new Rectangle(0, 0, sl.Width, sl.Height), 0, 0, sl.Width, sl.Height, GraphicsUnit.Pixel, dimIA);
                }
                capaAtenuadaDe = sl; capaAtenuadaKey = key;
                return capaAtenuada;
            }

            // Atributos de imagen con el alfa de la atenuacion (cacheados; la matriz se rehace solo si Dim cambio de escalon)
            private ImageAttributes dimIA; private int dimIAKey = -1;
            private void EnsureDimIA()
            {
                int key = (int)(Dim * 50f);   // escalones de 0.02: sin rehacer la matriz cada cuadro durante la transicion
                if (dimIA != null && dimIAKey == key) return;
                if (dimIA == null) dimIA = new ImageAttributes();
                ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = Dim;
                dimIA.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                dimIAKey = key;
            }

            // Indice del cuadro de titileo segun el reloj (rota ~5 veces/seg). Los armonicos son ENTEROS, asi
            // que el set entero cicla y el titileo se repite suave y continuo, sin recalcular nada por cuadro.
            private int CurrentStarLayerIndex()
            {
                if (layers == null || layers.Length == 0) return -1;
                int idx = (Environment.TickCount / 200) % layers.Length;
                if (idx < 0) idx += layers.Length;
                return idx;
            }

            // Vista de discos (VENTANA del campo COMPARTIDO de pantalla completa): cielo cacheado + estrellas
            // titilantes + LATIDO con la musica. El cielo y las estrellas salen del MISMO bitmap de pantalla
            // completa que pinta el fondo del form, corridos por (SkyOffsetX/Y): por eso las estrellas cruzan
            // los bordes de las secciones sin costura. El latido y las fugaces solo en repintado COMPLETO (en el
            // parcial de la casilla del cubo se reusa la capa: no relatir ni relanzar fugaces por una franja).
            // pLo/pMid/pHi = pulso de graves/medios/agudos (0..1). Cada estrella late con SU banda (ver DrawBeatFlare).
            public void DrawReactive(Graphics g, int w, int h, Color skyTop, Color skyBottom, float pLo, float pMid, float pHi)
            {
                if (w < 4 || h < 4) return;
                EnsureLayers(w, h);          // arma el patron de estrellas: el cielo necesita el polvo ya repartido
                EnsureSkyCache(w, h, skyTop, skyBottom);
                RectangleF clip = g.ClipBounds;
                bool full = (clip.Width >= w - 1 && clip.Height >= h - 1);   // repintado COMPLETO (no la casilla del cubo)
                BlitSkyAndStars(g, w, h, full);
                float pMax = Math.Max(pLo, Math.Max(pMid, pHi));
                // EL LATIDO, TAMBIEN EN LOS REPINTADOS PARCIALES. Antes solo se dibujaba en los completos, y
                // eso significaba que el trozo repintado -la casilla del cubo que gira, y ahora la casilla que
                // se elige- se quedaba SIN los halos de las estrellas hasta el siguiente cuadro completo: los
                // halos parpadeaban ahi dentro. Cuesta casi nada porque DrawBeatFlare descarta de un vistazo
                // las estrellas que caen fuera del recorte, y en un repintado parcial son casi todas.
                if (pMax * Dim > 0.04f) DrawBeatFlare(g, w, h, pLo * Dim, pMid * Dim, pHi * Dim);   // latido por banda (atenuado al navegar)
                if (full) DrawShoots(g, w, h);
            }

            // LATIDO con la musica: a las estrellas medianas/grandes (las que llevan glow) les estampa un glow
            // EXTRA que crece con la energia del audio. Es barato: ~40 sprites pre-horneados, no recalculo ni
            // pasada de pantalla completa. Del MISMO color (sprite por color). Solo en repintado completo. Las
            // estrellas se posicionan en coordenadas de PANTALLA COMPLETA menos el offset de la ventana; las que
            // caen fuera se saltan (el resto las recorta el clip del pintor).
            // Cada estrella late con SU banda (grandes=graves, medianas=medios/agudos): recibe los 3 pulsos y usa el
            // de su Band. Las estrellas cuya banda NO esta sonando no reciben halo -> se VE la separacion de beats.
            private void DrawBeatFlare(Graphics g, int w, int h, float pLo, float pMid, float pHi)
            {
                if (stars == null) return;
                int FW = FullW(w), FH = FullH(h), offX = SkyOffsetX, offY = SkyOffsetY;
                // Se mira contra el RECORTE, no contra la ventana entera: en un repintado parcial eso descarta
                // casi las seiscientas estrellas con una resta, sin llegar a dibujar nada.
                RectangleF rec = g.ClipBounds;
                for (int i = 0; i < stars.Length; i++)
                {
                    Star s = stars[i];
                    if (s.Kind == 0) continue;   // los puntos no reaccionan
                    float pulse = (s.Band == 0) ? pLo : (s.Band == 1) ? pMid : pHi;   // el pulso de SU frecuencia
                    if (pulse <= 0.02f) continue;   // su banda no suena ahora: sin halo (asi se nota la separacion)
                    int gd = s.Size * 2 + 4 + (int)(pulse * 16f);   // el halo crece con el golpe de su banda
                    int px = (int)(s.X * FW) + (int)driftX - offX; if (px >= FW) px -= FW;   // la estrella se movio: su halo con ella
                    int py = (int)(s.Y * FH) + (int)driftY - offY; if (py >= FH) py -= FH;
                    if (px + gd < rec.Left || px - gd > rec.Right || py + gd < rec.Top || py - gd > rec.Bottom) continue;   // fuera del recorte: se salta
                    int cr, cg, cb;
                    if (s.Tint > 0f) { cr = 255; cg = 255; cb = 255; } else { cr = s.R; cg = s.G; cb = s.B; }
                    Bitmap gb = GlowFor(cr, cg, cb);
                    if (gb == null) continue;
                    g.DrawImage(gb, new Rectangle(px - gd / 2, py - gd / 2, gd, gd));
                }
            }

            // Hornea los TwK cuadros de titileo UNA vez (al arrancar / al cambiar de tamano de PANTALLA). Se hornean
            // al tamano de la pantalla COMPLETA (FullW x FullH), no al de la ventana: asi un solo juego de capas
            // sirve a todas las secciones (fondo del form, rejilla, interior del disco) y cada una copia su ventana.
            // El patron es fijo; lo unico que cambia entre cuadros es el brillo de cada estrella -> al rotarlos titila.
            private void EnsureLayers(int w, int h)
            {
                int FW = FullW(w), FH = FullH(h);
                Ensure(FW, FH);
                if (layers != null && lw == FW && lh == FH && lBucket == sBucket) return;
                if (layers != null) { for (int k = 0; k < layers.Length; k++) if (layers[k] != null) layers[k].Dispose(); }
                if (capaAtenuada != null) { capaAtenuada.Dispose(); capaAtenuada = null; }   // apuntaba a una capa que ya no existe
                capaAtenuadaDe = null; capaAtenuadaKey = -1;
                layers = new Bitmap[TwK];
                lw = FW; lh = FH; lBucket = sBucket;
                EnsureGlow();
                for (int j = 0; j < TwK; j++) layers[j] = BakeStarFrame(FW, FH, j);
            }

            // Un cuadro de titileo: el patron con el brillo de cada estrella en la fase j (armonicos enteros
            // -> el set cicla exacto). Solo se ejecuta al hornear, no por cuadro, asi que puede ser detallado.
            private Bitmap BakeStarFrame(int w, int h, int j)
            {
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics lg = Graphics.FromImage(bmp))
                using (SolidBrush b = new SolidBrush(Color.White))
                using (ImageAttributes ia = new ImageAttributes())
                {
                    lg.Clear(Color.Transparent);
                    lg.InterpolationMode = InterpolationMode.Bilinear;
                    lg.SmoothingMode = SmoothingMode.AntiAlias;
                    ColorMatrix cm = new ColorMatrix();
                    bool polvo = false;
                    double ph = 2.0 * Math.PI * j / TwK;   // fase de este cuadro
                    for (int i = 0; i < stars.Length; i++)
                    {
                        Star s = stars[i];
                        // EL POLVO SI ENTRA AQUI, y antes no. Vivia horneado en el bitmap del cielo -una sola
                        // vez en vez de ocho- pero el cielo no se desliza: se queda pegado a la pantalla. Con la
                        // deriva andando, eso dejaba mas de la mitad de las estrellas clavadas mientras el resto
                        // se movia. Aqui se hornea con las demas y el campo entero se desliza como una pieza.
                        // No cuesta nada en marcha: son rectangulitos de un pixel y solo se hornean al arrancar
                        // o al cambiar de tamano de pantalla, no por cuadro.
                        if (s.Kind == 0)
                        {
                            if (!polvo) { lg.SmoothingMode = SmoothingMode.None; polvo = true; }
                            int cr0, cg0, cb0;
                            if (s.Tint > 0f) { cr0 = 255; cg0 = 255; cb0 = 255; } else { cr0 = s.R; cg0 = s.G; cb0 = s.B; }
                            b.Color = Color.FromArgb(s.Bri, cr0, cg0, cb0);
                            int d0 = s.Size < 1 ? 1 : (s.Size > 2 ? 2 : s.Size);
                            lg.FillRectangle(b, (int)(s.X * w) - d0 / 2, (int)(s.Y * h) - d0 / 2, d0, d0);
                            continue;
                        }
                        if (polvo) { lg.SmoothingMode = SmoothingMode.AntiAlias; polvo = false; }
                        int harm = 1 + (i % 2);   // armonico ENTERO (1 o 2: bien muestreado con TwK=8) -> el set cicla exacto
                        double tw = 0.62 + 0.38 * (0.5 + 0.5 * Math.Sin(ph * harm + s.Off));   // titileo 0.62..1.00
                        int a = (int)(tw * s.Bri); if (a > 255) a = 255; if (a < 0) a = 0;
                        int px = (int)(s.X * w), py = (int)(s.Y * h);
                        int cr2, cg2, cb2;
                        if (s.Tint > 0f) { cr2 = 255; cg2 = 255; cb2 = 255; }   // tenidas -> blancas
                        else { cr2 = s.R; cg2 = s.G; cb2 = s.B; }
                        int gd = (int)((s.Size * 3 + 6) * HaloDe(s.Kind));
                        Bitmap gb = GlowFor(cr2, cg2, cb2);   // glow del MISMO COLOR (sprite pre-horneado)
                        if (gb != null)
                        {
                            Rectangle gr = new Rectangle(px - gd / 2, py - gd / 2, gd, gd);
                            if (a >= 250) lg.DrawImage(gb, gr);
                            else   // recursos locales reutilizados; misma matriz y mismo orden de dibujo
                            {
                                cm.Matrix33 = (float)tw;
                                ia.SetColorMatrix(cm);
                                lg.DrawImage(gb, gr, 0, 0, gb.Width, gb.Height, GraphicsUnit.Pixel, ia);
                            }
                        }
                        if (s.Rayos) DrawRayos(lg, b, px, py, s.Size, Color.FromArgb(cr2, cg2, cb2), (s.Bri / 255f) * (float)tw, s.Kind);
                        DrawNucleo(lg, b, px, py, s.Size, cr2, cg2, cb2, a);
                    }
                }
                return bmp;
            }

            private void DrawShoots(Graphics g, int w, int h)
            {
                int now = Environment.TickCount;
                float dt = (lastTick == 0) ? 0f : (now - lastTick) / 1000f;
                if (dt < 0f) dt = 0f; if (dt > 0.12f) dt = 0.12f;   // acotar saltos
                lastTick = now;

                if (now - nextShoot >= 0)   // RESTA, no >=: sobrevive el desbordamiento de TickCount (~25 dias encendida)
                {
                    nextShoot = now + 7000 + rnd.Next(9000);   // una fugaz cada 7-16s
                    for (int i = 0; i < shoots.Length; i++)
                        if (!shoots[i].On)
                        {
                            Shoot ns = new Shoot();
                            ns.On = true;
                            // De donde sale y hacia donde va lo dice Form1, para que las dos paredes tiren
                            // las mismas fugaces (ver TiraFugaz).
                            bool gorda;
                            TiraFugaz(rnd, w, h, out ns.X, out ns.Y, out ns.VX, out ns.VY,
                                      out ns.MaxLife, out ns.Wdt, out ns.Tail, out gorda);
                            ns.Life = ns.MaxLife;
                            shoots[i] = ns;
                            break;
                        }
                }
                for (int i = 0; i < shoots.Length; i++)
                {
                    if (!shoots[i].On) continue;
                    Shoot s = shoots[i];
                    float k = s.Life / s.MaxLife; if (k < 0f) k = 0f;
                    int la = (int)(230 * k * Dim);   // atenuada mientras se navega con musica
                    float tx = s.X - s.VX * s.Tail, ty = s.Y - s.VY * s.Tail;   // estela: las gordas la traen larga
                    sp.Color = Color.FromArgb(la, 255, 255, 255);
                    sp.Width = s.Wdt;
                    g.DrawLine(sp, tx, ty, s.X, s.Y);
                    sb.Color = Color.FromArgb(Math.Min(255, la + 25), 255, 255, 255);
                    int cab = 1 + (int)s.Wdt;
                    g.FillRectangle(sb, (int)s.X - cab / 2, (int)s.Y - cab / 2, cab, cab);
                    s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt;
                    // POR LOS CUATRO LADOS, y no es un detalle: antes solo podian irse por la derecha o por
                    // abajo porque solo podian ir hacia alla. Con la direccion suelta, una que se vaya por la
                    // izquierda seguiria viva, invisible y ocupando una de las dos ranuras hasta que se le
                    // acabara la vida, y mientras tanto no saldria ninguna otra.
                    if (s.Life <= 0f || s.X > w + FugazBorra || s.X < -FugazBorra
                                     || s.Y > h + FugazBorra || s.Y < -FugazBorra) s.On = false;
                    shoots[i] = s;
                }
            }
        }

        // ==========  VISTA DE DISCOS (fondo animado estilo PS2 + cubos)  ==========
        // Panel con fondo estrellado (reactivo al genero), para el modo "dentro del disco"
        private class BgPanel : Panel
        {
            public Color Accent = DefaultAccent;
            public float BgDim = 1f;   // atenuacion del starfield mientras se navega con musica (el form la anima; igual que la rejilla)
            public BgPanel()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }
            protected override void OnPaintBackground(PaintEventArgs e) { /* todo en OnPaint */ }
            public Starfield Sky;   // COMPARTIDO con el fondo del form: este panel pinta su VENTANA del mismo campo (una sola pieza)
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width, h = Height;
                if (w < 4 || h < 4) return;

                // VENTANA del campo COMPARTIDO de pantalla completa: cielo + estrellas salen del mismo bitmap que
                // pinta el fondo del form, corridos por (Left, Top). Cacheado: solo se copia (rapido) y va fluido,
                // y las estrellas cruzan el borde con las franjas de arriba/abajo sin cortarse.
                if (Sky == null) return;
                Sky.SkyOffsetX = this.Left; Sky.SkyOffsetY = this.Top;
                Sky.SkyFullW = (this.Parent != null) ? this.Parent.ClientSize.Width : w;
                Sky.SkyFullH = (this.Parent != null) ? this.Parent.ClientSize.Height : h;
                Sky.SkyMid = SkyColorMid(Accent); Sky.Acento = Accent;
                Sky.Dim = BgDim;   // en sincronia con la rejilla y las franjas (una sola pieza)
                Sky.DrawCached(g, w, h, SkyColor(Accent), SkyColor2(Accent));   // cielo en degradado CONTINUO; un cubo transparente girando encima
            }

        }
    }
}
