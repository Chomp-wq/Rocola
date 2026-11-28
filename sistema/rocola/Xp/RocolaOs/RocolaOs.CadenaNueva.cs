// LAS CADENAS DE SONIDO NUEVAS: EL MARCO (plan aprobado por el dueno el 27-11-2026, CADENA_NUEVA.md; lo hecho, CADENA_NUEVA_A.md).
// «Vamos a disenar una nueva cadena de audio [...] Las nuevas tendras como meta exprimir al maximo el formato mp3, que suenen lo mejor posible y
// con volumen constante, no quiero ninguna tecnica de procesamiento si esta hace que el volumen fluctue en tiempo real». Conviven con la de hoy:
// el dueno elige cual suena y solo la reemplazan si ganan a ciegas.
//
// LAS FUNCIONES DE ROCOLA OS:
//   cadena_nueva  (28-11-2026, el dueno: «Elimina la cadena de sonido anterior y tambien el modo billar anterior, yo se que esta cadena de sonido
//                 nueva sera mejor»; CADENA_NUEVA.md, «La cadena vieja borrada») SIEMPRE: con ROCOLA_OS=1 es la unica cadena (la A; con el modo
//                 billar guardado, la B) y su clave «cadena_nueva=» se ignora (Siempre, RocolaOs.cs); con ROCOLA_OS=lista (pruebas), si la nombra.
//                 Sus claves: nivel_lufs (T, -18 de fabrica), techo_dbtp (C, -1), agudos_mp3 (el DSEE y el relleno de huecos, apagado) y
//                 tono_parejo (el EQ automatico fijo por cancion, apagado), todas desde la siguiente cancion (Ctrl+F4 -> Cadena). Al arrancar,
//                 los datos de la cadena vieja que ya nadie lee van a la papelera de Ctrl+F4 (RocolaOs.CadenaVieja.cs).
//   medida_nueva  (encendida de fabrica: solo mide, y solo si la cadena nueva suena o alguien la pide) la tabla rocola_medida_nueva.txt, la
//                 medida al entrar a la cola y el repaso de fondo (RocolaOs.MedidaTabla.cs).
//
// LOS GANCHOS (g460-g479; el manifiesto, RocolaOs/GANCHOS.tsv):
//   g460  Codigo/Form1.Sonido.cs, ArmaCadena: con la cadena nueva, la cadena de esta cancion la arma RocolaOsCadenaNueva (aqui) y XP no; sin
//         ella (o si algo falla al armarla), null y la de XP tal cual. Lo usan el arranque de siempre y el relevo sin pausa.
//   g461  Codigo/Form1.SinPausa.cs, SigueA: el disco continuo entre dos cadenas nuevas lo hace el marco (cada pieza con la suya); entre una
//         nueva y una de XP no se hereda nada (empiezan de cero, como con fundido).
//   g462  Codigo/Form1.Sonido.cs, OpenAudio: con la cadena nueva, un MP3 se abre en coma flotante (RocolaOs.CadenaNuevaMp3.cs) con su sin
//         huecos y saltar los silencios largos de XP por fuera; si no se puede, null y lo de XP.
// Sin ROCOLA_OS o con la cadena nueva apagada, los tres devuelven lo de XP: bit a bit como hoy.
//
// LA CADENA A (§2): leer en coma flotante y sin huecos -> dos canales -> [la entrada: el pulso] -> el zumbido (QuitaZumbido de XP, si se
// midio) -> los arreglos fijos -> [agudos_mp3: el DSEE; el relleno de huecos va en el descompresor] -> [tono_parejo] -> los graves al centro
// -> [la medida en vivo, si no se midio antes] -> el ecualizador del dueno (memoria normal, sin preganancia) -> LA GANANCIA x la noche -> el
// corte del grave de noche y los graves de noche de XP -> el fundido de XP -> la salida de siempre (o la nueva del otro agente).
// LA B (§3): lo mismo hasta los arreglos; despues RocolaOsCadenaB.Arma (las piezas del billar; mientras no esten, de paso) -> el ecualizador
// del dueno (memoria del billar) -> la ganancia (medida con la B) -> la noche (sus graves de 120 Hz y su volumen; su corte de 80 Hz no: va
// dentro del paso alto de las piezas del billar) -> el fundido -> la salida.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static class RocolaOsCadenasNuevas
    {
        internal const string Nombre = "cadena_nueva", Medida = "medida_nueva";
        internal const string ClaveNivel = "nivel_lufs", ClaveTecho = "techo_dbtp", ClaveAgudos = "agudos_mp3", ClaveTono = "tono_parejo";
        internal const float GravesCentroHz = 120f;
        internal const double GananciaMaxDb = 20.0, GananciaMinDb = -40.0;

        // ¿SUENA LA NUEVA? Con ROCOLA_OS=1, siempre (28-11-2026: es la unica); con una lista (pruebas), si la nombra. (Sin ROCOLA_OS, hasta la
        // fase 2 de «La cadena vieja borrada», CADENA_NUEVA.md, la de XP.)
        internal static bool Suena { get { return RocolaOs.Activo && RocolaOs.Encendida(Nombre); } }
        internal static double T { get { int v = RocolaOs.AjusteEntero(ClaveNivel); return v >= -30 && v <= -6 ? v : -18; } }
        internal static double C { get { int v = RocolaOs.AjusteEntero(ClaveTecho); return v >= -20 && v <= 0 ? v : -1; } }
        internal static bool Agudos { get { return RocolaOs.AjusteSiNo(ClaveAgudos); } }
        internal static bool Tono { get { return RocolaOs.AjusteSiNo(ClaveTono); } }
        internal static long Armadas, Fallos, Flotantes;
        internal static string Ultima = "";
        internal static void Apunta(string que) { RocolaOs.Apunta(Nombre, que); }
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraCadenaNueva()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsCadenasNuevas.Nombre,
                Que = "la cadena de sonido nueva, la unica (la A; con el modo billar, la B); sus ajustes en Ctrl+F4 -> Cadena",
                DeFabrica = true, Siempre = true,
                Arranca = f => RocolaOsCadenaVieja.Arranca(),
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.Entero(RocolaOsCadenasNuevas.ClaveNivel, -18, -30, -6, true, "cadena nueva: la sonoridad a la que va cada cancion (LUFS, BS.1770); desde la siguiente cancion"),
                    RocolaOsClave.Entero(RocolaOsCadenasNuevas.ClaveTecho, -1, -20, 0, true, "cadena nueva: el techo del pico real (dBTP); las que no caben quedan mas bajas; desde la siguiente cancion"),
                    RocolaOsClave.SiNo(RocolaOsCadenasNuevas.ClaveAgudos, false, true, "cadena nueva (A): los agudos y los huecos del MP3 (el DSEE y el relleno de huecos R1); decide la prueba ciega"),
                    RocolaOsClave.SiNo(RocolaOsCadenasNuevas.ClaveTono, false, true, "cadena nueva (A): el tono parejo por cancion (el EQ automatico fijo, suave, +-3 dB); decide la prueba ciega"),
                },
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsCadenasNuevas.Medida,
                Que = "la medida nueva de cada cancion para la cadena nueva (BS.1770 y pico real, rocola_medida_nueva.txt); solo trabaja si la cadena nueva suena",
                DeFabrica = true,
                Arranca = f => RocolaOsMedidaTrabajo.Arranca(),
                Latido = f => f.RocolaOsLatidoMedida(),
                Vivo = (f, sb) => f.RocolaOsVivoCadenaNueva(sb),
                ParaAlCerrar = r => RocolaOsMedidaTrabajo.Para(),
            });
            RocolaOsOrdenes.Si("--prueba-cadena-nueva", RocolaOsCadenaNuevaPrueba.Corre);
        }

        // LA CADENA YA ARMADA (lo que el marco necesita de cada cancion, ademas de la CadenaCancion de XP)
        private sealed class RocolaOsArmada
        {
            internal RocolaOsReceta Receta;
            internal QuitaZumbido Zumbido;
            internal GraveMono NocheCorte;
            internal SalaLocal NocheGraves;
            internal RocolaOsPiezaEntrada Entrada;
            internal RocolaOsMedidorVivo Vivo;
            internal RocolaOsGananciaPieza Ganancia;
            internal RocolaOsEqDuenoPieza EqDueno;
            internal string Fuente = "";   // como se abrio (coma flotante, sin huecos...)
        }
        private static readonly ConditionalWeakTable<CadenaCancion, RocolaOsArmada> rocolaOsArmadas = new ConditionalWeakTable<CadenaCancion, RocolaOsArmada>();
        private static RocolaOsArmada RocolaOsArmadaDe(CadenaCancion c) { RocolaOsArmada a; return c != null && rocolaOsArmadas.TryGetValue(c, out a) ? a : null; }

        // ---------------------------------------------------------------- g462: el MP3 en coma flotante
        private IAudioSource RocolaOsCadenaNuevaAbre(string path, string original, FuenteMedida inicio)
        {
            if (!RocolaOsCadenasNuevas.Suena || path == null || !path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return null;
            RocolaOsFuenteFlotante f = null;
            try
            {
                bool agudos = RocolaOsCadenasNuevas.Agudos && !LocalAutoAGuardar;   // (solo la A)
                f = new RocolaOsFuenteFlotante(path, agudos, true);
                IAudioSource s = SellaFuenteAudio(original, inicio, ComoEstereo(f));
                string como;
                s = RocolaOsSinHuecos(path, s, out como);
                Interlocked.Increment(ref RocolaOsCadenasNuevas.Flotantes);
                s = SinSilencios(path, s);
                rocolaOsComoSeAbrio.AddOrUpdate(s, RocolaOsDescompresorFlotante.Motor + ", sin huecos: " + como + (f.ConRelleno ? ", con el relleno de huecos" : ""));
                return s;
            }
            catch (Exception ex)
            {
                if (f != null) { try { f.Dispose(); } catch { } }
                RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "abrir en coma flotante " + Path.GetFileName(path) + " (suena con el lector de XP)", ex);
                return null;
            }
        }
        // como se abrio cada lector de la cadena nueva (el mismo objeto que llega a ArmaCadena); uno que no este aqui lo abrio XP (16 bits)
        private static readonly ConditionalWeakTable<IAudioSource, string> rocolaOsComoSeAbrio = new ConditionalWeakTable<IAudioSource, string>();

        // ---------------------------------------------------------------- g461: el disco continuo
        private static bool RocolaOsCadenaNuevaSigue(CadenaCancion nueva, CadenaCancion vieja)
        {
            RocolaOsArmada an = RocolaOsArmadaDe(nueva), av = RocolaOsArmadaDe(vieja);
            if (an == null && av == null) return false;   // las dos de XP: lo de XP
            if (an == null || av == null) return true;    // una de cada: nada se hereda
            try
            {
                List<IRocolaOsPieza> pn = an.Receta.Piezas, pv = av.Receta.Piezas;
                RocolaOsSiguePiezas(pn, pv);
                if (an.Zumbido != null && av.Zumbido != null) an.Zumbido.Sigue(av.Zumbido);
                if (an.NocheCorte != null && av.NocheCorte != null) an.NocheCorte.Sigue(av.NocheCorte);
                if (an.NocheGraves != null && av.NocheGraves != null) an.NocheGraves.Sigue(av.NocheGraves);
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "el disco continuo", ex); }
            return true;
        }

        // LA PREPARADA SE RETIRA SIN EMPEZAR (gancho g463, SiguienteConElInterruptor de SinSilencios.cs, antes de soltar su lector): la que suena ya no
        // lee su principio para el disco continuo exacto del billar (RocolaOsBillar2Tramo.Retira; si ya lo leyo, la siguiente que se arme empalma)
        private static void RocolaOsCadenaNuevaNoSigue(CadenaCancion p)
        {
            RocolaOsArmada a = RocolaOsArmadaDe(p); if (a == null) return;
            try { foreach (IRocolaOsPieza x in a.Receta.Piezas) { RocolaOsBillar2Tramo t = x as RocolaOsBillar2Tramo; if (t != null) t.Retira(); } }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "retirar la siguiente", ex); }
        }

        // cada pieza de la nueva con la de la vieja del MISMO tipo y el mismo orden entre las de su tipo (una medida en vivo o un tono parejo de mas en
        // una de las dos no descoloca a las demas). Al preparar el relevo (antes de que la vieja acabe): las piezas del billar lo usan para el
        // disco continuo exacto (CADENA_NUEVA.md §8.2); las del marco lo toman en su primera lectura.
        internal static void RocolaOsSiguePiezas(List<IRocolaOsPieza> nuevas, List<IRocolaOsPieza> viejas)
        {
            Dictionary<Type, int> usadas = new Dictionary<Type, int>();
            foreach (IRocolaOsPieza p in nuevas)
            {
                Type t = p.GetType(); int k; usadas.TryGetValue(t, out k); usadas[t] = k + 1;
                int vistos = 0;
                foreach (IRocolaOsPieza v in viejas) if (v.GetType() == t && vistos++ == k) { p.Sigue(v); break; }
            }
        }

        // ---------------------------------------------------------------- g460: la cadena de esta cancion
        private CadenaCancion RocolaOsCadenaNueva(Track t, IAudioSource source, bool iaMejorada, double saltadoSeg)
        {
            if (!RocolaOsCadenasNuevas.Suena || t == null || source == null || t.Path == null) return null;
            try
            {
                if (source.WaveFormat.Channels != 2 || source.WaveFormat.SampleRate < 8000) return null;   // (mas de dos canales: la de XP)
                return RocolaOsArmaNueva(t, source, saltadoSeg);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref RocolaOsCadenasNuevas.Fallos);
                RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "armar la cadena nueva de " + Path.GetFileName(t.Path) + " (suena con la de hoy)", ex);
                return null;
            }
        }

        private CadenaCancion RocolaOsArmaNueva(Track t, IAudioSource source, double saltadoSeg)
        {
            Track original = t;
            t = RocolaOsCalidadCancion(t, source);   // la sustitucion invisible: la cadena con la ruta de Spotify (lo que se ve, tu cancion)
            bool sombra = !ReferenceEquals(t, original);
            CadenaCancion c = new CadenaCancion();
            c.Cancion = t; c.Lector = source; c.IaMejorada = false;
            OlvidaSiEsOtroArchivo(t.Path, source);
            c.Fuente = FuenteDeLector(t.Path, source);
            if (c.Fuente != null && !c.Fuente.Igual(CapturaFuente(t.Path))) OlvidaMedidas(t.Path);
            c.MedidaRuta = null; c.EqRuta = null;   // la cadena nueva no le ensena nada a las tablas de XP (tiene la suya)
            // SPOTIFY: sin el nivelado en vivo de los primeros 30 s (lo que mide para las tablas de XP sigue)
            RocolaOsSpotifySinNivelarEnVivo(source);

            RocolaOsReceta r = new RocolaOsReceta();
            r.Cadena = LocalAutoAGuardar ? 'B' : 'A';
            r.Ruta = t.Path; r.Tasa = source.WaveFormat.SampleRate;
            r.Medida = RocolaOsMedidaTabla.Valida(t.Path, r.Cadena);
            if (r.Medida != null && r.Medida.Alg == RocolaOsMedidaNueva.Algoritmo)
            {
                r.CanalMudo = r.Medida.CanalMudo; r.Invertido = r.Medida.Invertido; r.ContinuaL = r.Medida.ContinuaL; r.ContinuaR = r.Medida.ContinuaR;
                r.ZumbidoHz = r.Medida.ZumbidoHz;
            }
            else
            {
                RocolaOsMedidaNueva soloArreglos = r.Medida;   // (Alg 0: medida en vivo con arreglos por poner: se ponen y se vuelve a medir)
                r.Medida = null;
                if (soloArreglos != null) { r.CanalMudo = soloArreglos.CanalMudo; r.Invertido = soloArreglos.Invertido; r.ContinuaL = soloArreglos.ContinuaL; r.ContinuaR = soloArreglos.ContinuaR; }
                r.ZumbidoHz = sombra ? 0 : ZumbidoDe(t.Path);
            }
            if (r.Cadena == 'A')
            {
                r.Agudos = RocolaOsCadenasNuevas.Agudos;
                if (RocolaOsCadenasNuevas.Tono) r.TonoDb = RocolaOsTonoPedido(sombra ? original.Path : t.Path);
            }
            r.GananciaDb = RocolaOsGananciaNueva(r, t, original, sombra);

            RocolaOsArmada arm = new RocolaOsArmada { Receta = r };
            string ca;
            bool flotante = rocolaOsComoSeAbrio.TryGetValue(source, out ca);
            if (flotante) arm.Fuente = ca;
            else arm.Fuente = source is RocolaOsFuenteSpotify ? "Spotify (rocola-pulso, coma flotante)" : source is RocolaOsFuenteCalidad ? "la version de Spotify (coma flotante)" : "el lector de XP (16 bits: se abrio antes de elegir la nueva)";
            ISampleProvider x = source;
            bool nocheEnB = false;
            // (un MP3 que abrio el lector de XP, a 16 bits y recortado, no se mide en vivo: su pico real saldria corto; lo mide la cola o el repaso)
            bool medirEnVivo = r.Medida == null && saltadoSeg <= 0 && (flotante || !t.Path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
            arm.Entrada = r.Anota(new RocolaOsPiezaEntrada(x, this, medirEnVivo)); x = arm.Entrada;
            if (r.ZumbidoHz > 0) { arm.Zumbido = new QuitaZumbido(x, r.ZumbidoHz, true); x = arm.Zumbido; }
            RocolaOsArreglosFijos af = r.Anota(new RocolaOsArreglosFijos(x, r)); x = af;
            if (r.Cadena == 'A')
            {
                if (r.Agudos && sbrAllowed)
                {
                    try
                    {
                        SpectralTap sbr = new SpectralTap(x);
                        sbr.Red = ModeloDseePara(t);
                        sbr.Active = true;
                        c.Sbr = sbr; x = sbr;
                    }
                    catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "el DSEE de agudos_mp3 (sin el)", ex); }
                }
                if (r.TonoDb != null) x = r.Anota(new RocolaOsTonoParejo(x, TonoParejoGanancias(r.TonoDb, r.Tasa)));
                x = r.Anota(new RocolaOsGravesCentro(x, RocolaOsCadenasNuevas.GravesCentroHz));
            }
            else
            {
                ISampleProvider b = null;
                Func<RocolaOsReceta, ISampleProvider, ISampleProvider> armaB = RocolaOsCadenaB.Arma;
                if (armaB != null)
                {
                    try { b = armaB(r, x); }
                    catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Nombre, "las piezas de la B (van de paso)", ex); b = null; }
                }
                x = b ?? r.Anota(new RocolaOsPiezaDePaso(x));
                nocheEnB = b != null;   // (las piezas del billar llevan el corte de noche dentro de su paso alto: CADENA_NUEVA.md §8.2)
            }
            if (medirEnVivo)
            {
                arm.Vivo = r.Anota(new RocolaOsMedidorVivo(x)); x = arm.Vivo;
                RocolaOsMedidaVivoPrepara(arm, t.Path, source, sombra);
            }
            arm.EqDueno = r.Anota(new RocolaOsEqDuenoPieza(x, this, r.Cadena == 'B', EqDuenoMedios(r.Cadena == 'B'))); x = arm.EqDueno;
            arm.Ganancia = r.Anota(new RocolaOsGananciaPieza(x, this, r.GananciaDb)); x = arm.Ganancia;
            // LA NOCHE (§2: FactorNoche, en la ganancia, y sus filtros de graves de hoy): el corte del grave (NV2) y los graves de noche (N1)
            float corteNoche = corteNocheRelevo >= 0f ? corteNocheRelevo : NocheCorteHz();
            if (corteNoche > 0f && !nocheEnB) { arm.NocheCorte = GraveMono.DeNoche(x, corteNoche); c.Noche = arm.NocheCorte; x = arm.NocheCorte; }
            arm.NocheGraves = new SalaLocal(x, null, 0.26f, false, false, 0, 0, 0); x = arm.NocheGraves;
            // EL FUNDIDO DE XP (el fundido= del dueno; la entrada de 5 ms), con la misma cuenta de lo que dura
            c.Fundido = RocolaOsFundido(x, source, t, saltadoSeg);
            c.Salida = c.Fundido; c.Retardo = 0;
            rocolaOsArmadas.AddOrUpdate(c, arm);
            Interlocked.Increment(ref RocolaOsCadenasNuevas.Armadas);
            string linea = r.Cadena + " " + Path.GetFileName(sombra ? original.Path : t.Path) + (sombra ? " (desde Spotify)" : "") + ": ganancia "
                + r.GananciaDb.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " dB (" + r.Como + ")"
                + (r.Medida != null ? "; " + r.Medida.Resumen() : "; sin medir: se mide mientras suena")
                + (af.Nada ? "" : "; arreglos: " + (r.CanalMudo >= 0 ? "canal mudo " : "") + (r.Invertido ? "invertido " : "") + (r.ContinuaL != 0f || r.ContinuaR != 0f ? "continua" : ""))
                + (r.ZumbidoHz > 0 ? "; zumbido " + r.ZumbidoHz + " Hz" : "")
                + (r.Agudos ? "; agudos_mp3" + (c.Sbr != null ? " (DSEE)" : "") : "") + (r.TonoDb != null ? "; tono parejo" : "")
                + "; " + arm.Fuente;
            RocolaOsCadenasNuevas.Ultima = linea;
            RocolaOsCadenasNuevas.Apunta(linea);
            return c;
        }

        // el fundido de salida y la entrada de XP (ArmaCadena), con lo que dura la musica
        private FadeProvider RocolaOsFundido(ISampleProvider x, IAudioSource source, Track t, double saltadoSeg)
        {
            double entrada = saltadoSeg > 0 ? FadeProvider.EntradaRetomadaSeg : FadeProvider.EntradaSeg;
            if (!(crossfadeSec > 0.01f)) return new FadeProvider(x, 0, 0, 0, entrada);
            long tf = 0;
            try
            {
                int hz = source.WaveFormat.SampleRate;
                float yaSe = SegundosDe(t.Path);
                FuenteSinSilencios sinSilencios = source as FuenteSinSilencios;
                double delArchivo = 0;
                try { delArchivo = ((sinSilencios != null) ? sinSilencios.Interior : source).TotalTime.TotalSeconds; } catch { }
                if (yaSe > 0.01f && delArchivo > 0 && Math.Abs(delArchivo - yaSe) > 1.0) yaSe = 0f;
                tf = (yaSe > 0.01f) ? (long)(yaSe * hz) : (long)(delArchivo * hz);
                FuenteSinRelleno sinRelleno = ((sinSilencios != null) ? sinSilencios.Interior : source) as FuenteSinRelleno;
                if (sinRelleno != null && saltadoSeg <= 0) tf = sinRelleno.CuadrosMusica;
                if (sinSilencios != null) tf = sinSilencios.Salen(tf);
                else tf -= (long)(saltadoSeg * hz);
            }
            catch { }
            return new FadeProvider(x, tf, crossfadeSec, 0, entrada);
        }

        // Spotify en la cadena nueva: el nivelado en vivo de los primeros 30 s (spotify_nivela) no se aplica; su medida para las tablas de XP sigue
        private void RocolaOsSpotifySinNivelarEnVivo(IAudioSource source)
        {
            RocolaOsFuenteSpotify f = source as RocolaOsFuenteSpotify;
            if (f == null || f.Nivela == null || !f.Nivela.Vivo) return;
            RocolaOsSpotifyNivelador n = f.Nivela;
            f.Nivela = new RocolaOsSpotifyNivelador(n.Id, f.WaveFormat.SampleRate, n.ArranqueDb, double.NaN, true);
        }

        // LO QUE SUENA (para la hoja y vivo.txt): la receta de la cancion que suena, si es de la cadena nueva
        internal RocolaOsReceta RocolaOsRecetaSonando { get { RocolaOsArmada a = RocolaOsArmadaDe(cadenaSonando); return a != null ? a.Receta : null; } }
        internal string RocolaOsFuenteSonando { get { RocolaOsArmada a = RocolaOsArmadaDe(cadenaSonando); return a != null ? a.Fuente : null; } }
        private void RocolaOsVivoCadenaNueva(StringBuilder sb)
        {
            RocolaOsReceta r = RocolaOsRecetaSonando;
            sb.Append("cadena_nueva=").Append(RocolaOsCadenasNuevas.Suena ? "suena" : "no").Append(" armadas=").Append(Interlocked.Read(ref RocolaOsCadenasNuevas.Armadas).ToString(CultureInfo.InvariantCulture))
              .Append(" fallos=").Append(Interlocked.Read(ref RocolaOsCadenasNuevas.Fallos).ToString(CultureInfo.InvariantCulture))
              .Append(" flotantes=").Append(Interlocked.Read(ref RocolaOsCadenasNuevas.Flotantes).ToString(CultureInfo.InvariantCulture));
            if (r != null) sb.Append(" sonando=").Append(r.Cadena).Append(" ganancia_db=").Append(r.GananciaDb.ToString("0.00", CultureInfo.InvariantCulture)).Append(" medida=").Append(r.Medida != null ? "si" : "no");
            sb.Append('\n');
            sb.Append("medida_nueva medidas=").Append(RocolaOsMedidaTabla.Cuantas.ToString(CultureInfo.InvariantCulture))
              .Append(" nuevas=").Append(Interlocked.Read(ref RocolaOsMedidaTrabajo.Hechas).ToString(CultureInfo.InvariantCulture))
              .Append(" en_vivo=").Append(Interlocked.Read(ref RocolaOsMedidaTrabajo.EnVivo).ToString(CultureInfo.InvariantCulture))
              .Append(" cola=").Append(RocolaOsMedidaTrabajo.EnCola.ToString(CultureInfo.InvariantCulture))
              .Append(" fondo=").Append(RocolaOsMedidaTrabajo.Pendientes.ToString(CultureInfo.InvariantCulture))
              .Append(" ultima_ms=").Append(RocolaOsMedidaTrabajo.UltimaMs.ToString("0", CultureInfo.InvariantCulture)).Append('\n');
        }
    }
}
