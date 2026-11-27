// EL CIELO VIVO DE ROCOLA OS: EL NUCLEO (decision del dueno del 20-11-2026, «El cielo de la rocola, otra vez vivo»: «haz todo!»; plan en
// PLAN_CIELO_VIVO.md; como se usa: FASE11_cielo-nucleo.md). Solo Rocola OS (ROCOLA_OS), solo la tarjeta grafica, encima del cielo de XP.
//
// CUATRO FUNCIONES DEL ANDAMIO, cada una con TODAS sus claves de rocola_os.txt (§2; los grupos de despues solo las leen):
//   cielo    (dia y noche y lo comun)  cielo_sol DN1, sol DN2, luna_camino DN3 (0 = A, la de XP), estrellas_anochecer DN4, horizonte_hora DN5,
//                                      nubes DN6, ciudad_bar (0 automatica por internet; 1 a 41, una de RocolaOsCiudades)
//   fiestas                            aurora_fiesta FI1, horizonte_fiesta FI2, fugaz_fiesta FI3, fuegos FI4, escaparate_fiesta FI5, fechas_propias FI6
//   clima    (su clave es CL1)         lluvia CL2, tormenta CL3, neblina CL4, clima_escaparate CL5
//   vidrio                             vidrio_ficha VI1, vidrio_franja VI2, vidrio_pastillas VI3, vidrio_lista VI4, vidrio_luz VI5, vidrio_buscador VI6
// De fabrica todo encendido salvo luna_camino (0), ciudad_bar (0) y VI1, VI2, VI4 y VI6 (apagadas hasta medirlas en el A4). Una funcion
// apagada (cielo=0...) apaga todo lo suyo. Las claves de las ideas cambian en vivo (el latido las vuelve a mirar cada segundo).
//
// EL ESTADO (§3), sin dibujo: la hora del cielo (la real, ROCOLA_CIELO_FECHA o «Ver como se veria»), el lugar (ciudad_bar, ROCOLA_CIUDAD o el de
// internet), el sol y la luna, la fase del dia (pesos de las paletas de §5, sin saltos), el clima (RocolaOs.CieloClima.cs), el calendario
// (RocolaOs.CieloFiestas.cs) y los interruptores, en UNA FOTO INMUTABLE (RocolaOsCieloFoto) que el latido (1 s, en la pantalla) cambia entera;
// el cuadro (gancho g120, en la pantalla) solo interpola lo de la foto anterior a la nueva en 1 s y lleva la calma (regla 5) y la escena: sin
// cerrojos, sin red ni disco y sin memoria nueva por cuadro.
//
// SIN ROCOLA_OS (o con las cuatro funciones apagadas) no se registra nada que trabaje: ni hilo, ni latido, ni archivos, y cada gancho del cielo
// devuelve lo de XP (RocolaOs.CieloEnchufes.cs).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    // LA FOTO DEL CIELO: lo que el latido calcula cada segundo (inmutable: el que la lee no necesita cerrojo; una nueva cada latido)
    internal sealed class RocolaOsCieloFoto
    {
        internal int Version;                 // sube con cada foto
        internal DateTime Local;              // la hora del cielo (local del sistema: la del bar)
        internal DateTime Utc;
        internal bool VerComo;                // «Ver como se veria» puesto (hora, fecha o clima forzados 30 s)
        internal bool Salto;                  // esta foto salta (empieza o acaba «Ver como se veria»): el cuadro la funde en 1,5 s y no en 1 s
        internal double Lat, Lon; internal string Lugar = ""; internal int Ciudad;   // Ciudad: ciudad_bar (0 automatica)
        internal double SolAlt, SolAz;        // grados (la altura del centro, sin refraccion)
        internal bool SolSube;                // por la manana (la altura crece)
        internal double LunaAlt, LunaAz, LunaFase, LunaLuz;   // fase 0 nueva, 0,5 llena; luz 0 a 1 (lo iluminado)
        internal readonly float[] Pesos = new float[RocolaOsCieloDia.Fases];   // el peso de cada paleta de §5 (suman 1; [0] la noche de XP)
        internal string Fase = "noche";       // la de mas peso (para la bitacora y vivo.txt)
        internal float Noche;                 // = Pesos[0]: 1 = la noche de XP exacta
        internal float Estrellas = 1f, Nebulosa = 1f, Velo;   // §5, ya mezclados por los pesos (con la noche: 1, 1 y 0)
        internal uint Raya;                   // el color de la raya del horizonte de las fases de dia (RGB; sin la noche: esa es la de XP)
        internal RocolaOsClima Clima = RocolaOsClima.Despejado0;   // el clima que vale ahora (despejado si no se sabe o ya caduco)
        internal bool ClimaSabido;            // si hay un clima de verdad (vigente, o forzado)
        internal RocolaOsCalendarioHoy Hoy = RocolaOsCalendarioHoy.Nada;
        // LAS IDEAS, ya miradas (su funcion encendida y su clave): lo que lee cada grupo para saber si pinta
        internal bool DN1, DN2, DN3B, DN4, DN5, DN6, FI1, FI2, FI3, FI4, FI5, FI6, CL1, CL2, CL3, CL4, CL5, VI1, VI2, VI3, VI4, VI5, VI6;
        internal bool Cielo, Fiestas, ClimaVa, Vidrio;   // las cuatro funciones
    }

    // LA ESCENA DE ESTE CUADRO (la pone el gancho g120 al empezar cada cuadro de la tarjeta, antes del cielo; solo en la pantalla)
    internal struct RocolaOsCieloEscena
    {
        internal int Ancho, Alto;
        internal float Horizonte;      // la raya del horizonte de XP (y en pixeles: donde para la aurora; sube con el reposo)
        internal float EstadoArriba;   // donde empieza la franja de abajo (StatusTopY)
        internal int Modo;             // el de XP (SetScene): 0 nada, 1 la pared, 2 el buscador, 3 el disco abierto, 4 la atraccion (el escaparate)
        internal bool Pared;           // la rejilla de portadas a la vista (ShowAlbum)
        internal float Reposo;         // el reposo con musica (el estado extendido «AHORA SUENA»): 0 a 1
        internal double Ms, Dt;        // el reloj fino de la tarjeta (ms) y lo que paso desde el cuadro anterior (s, acotado a 0,25)
        internal float Calma;          // regla 5: 1 normal; 0,35 mientras alguien elige (baja en 0,4 s, vuelve en 3 s)
        internal bool Eligiendo;       // alguien elige ahora (un boton hace menos de 15 s, o el buscador, la ayuda, la estrella, Ctrl+F3 o Ctrl+F4)
    }

    internal static partial class RocolaOsCielo
    {
        internal const string Funcion = "cielo", FuncionFiestas = "fiestas", FuncionClima = "clima", FuncionVidrio = "vidrio";

        // ---------- el registro (sin trabajo: corre antes del Main de XP) ----------
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = Funcion,
                Que = "el cielo vivo: dia y noche por la hora del sol de la ciudad del bar (DN1-DN6) y lo comun del cielo",
                DeFabrica = true,   // (27-11-2026, version del cielo: encendido de fabrica, decision del 20-11-2026)
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.SiNo("cielo_sol", true, true, "DN1: el cielo de XP sigue al sol (alba, amanecer, manana, mediodia, tarde dorada, atardecer, anochecer)"),
                    RocolaOsClave.SiNo("sol", true, true, "DN2: el sol cruza el cielo, siempre detras de todo"),
                    RocolaOsClave.SiNo("luna_camino", false, true, "DN3: la luna en su camino real (1) o en su sitio de XP con su fase real (0)"),
                    RocolaOsClave.SiNo("estrellas_anochecer", true, true, "DN4: las estrellas se encienden de una en una al anochecer"),
                    RocolaOsClave.SiNo("horizonte_hora", true, true, "DN5: la raya del horizonte con la luz de la hora"),
                    RocolaOsClave.SiNo("nubes", true, true, "DN6: nubes pintadas con la luz del sol o de la luna"),
                    RocolaOsClave.Entero("ciudad_bar", 0, 0, RocolaOsCiudades.Maxima, true, "la ciudad del bar: 0 automatica por internet (una vez por semana), o una de la lista (1 a 41)"),
                },
                Arranca = f => Arranca(f),
                Latido = f => Late(f),
                Vivo = (f, sb) => Vivo(sb),
                AlCerrar = (f, razon) => GuardaReloj(),
                ParaAlCerrar = razon => ParaHilo(),
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = FuncionFiestas,
                Que = "las fiestas en el cielo (FI1-FI6), sin internet",
                DeFabrica = true,   // (27-11-2026, version del cielo: encendido de fabrica, decision del 20-11-2026)
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.SiNo("aurora_fiesta", true, true, "FI1: la aurora de XP con los colores de la fecha"),
                    RocolaOsClave.SiNo("horizonte_fiesta", true, true, "FI2: una fila baja de adornos sobre la raya del horizonte"),
                    RocolaOsClave.SiNo("fugaz_fiesta", true, true, "FI3: la fugaz dorada de XP con la estela de la fecha"),
                    RocolaOsClave.SiNo("fuegos", true, true, "FI4: fuegos artificiales en el Grito y en Ano Nuevo"),
                    RocolaOsClave.SiNo("escaparate_fiesta", true, true, "FI5: papel picado y el mensaje de la fecha en el escaparate"),
                    RocolaOsClave.SiNo("fechas_propias", true, true, "FI6: tus fechas (cumpleanos y aniversarios de rocola_fechas.txt)"),
                },
                Arranca = f => Arranca(f),
                Latido = f => Late(f),
                AlCerrar = (f, razon) => GuardaReloj(),
                ParaAlCerrar = razon => ParaHilo(),
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = FuncionClima,
                Que = "CL1: las nubes de afuera (Open-Meteo cada 15 min, gratis y sin llave) y lo demas del clima (CL2-CL5)",
                DeFabrica = true,   // (27-11-2026, version del cielo: encendido de fabrica, decision del 20-11-2026)
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.SiNo("lluvia", true, true, "CL2: lluvia detras de la musica, con el viento real"),
                    RocolaOsClave.SiNo("tormenta", true, true, "CL3: relampagos dentro de las nubes, sin destellos"),
                    RocolaOsClave.SiNo("neblina", true, true, "CL4: neblina en una franja sobre el horizonte"),
                    RocolaOsClave.SiNo("clima_escaparate", true, true, "CL5: la pastilla del clima y un mensaje en el escaparate"),
                },
                Arranca = f => Arranca(f),
                Latido = f => Late(f),
                AlCerrar = (f, razon) => GuardaReloj(),
                ParaAlCerrar = razon => ParaHilo(),
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = FuncionVidrio,
                Que = "las piezas de XP de vidrio (VI1-VI6), en su sitio",
                DeFabrica = true,   // (27-11-2026, version del cielo: encendido de fabrica, decision del 20-11-2026)
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.SiNo("vidrio_ficha", false, true, "VI1: la ficha de vidrio (desenfoca lo de detras: apagada hasta medirla en el A4)"),
                    RocolaOsClave.SiNo("vidrio_franja", false, true, "VI2: la franja «AHORA SUENA» de vidrio (apagada hasta medirla en el A4)"),
                    RocolaOsClave.SiNo("vidrio_pastillas", true, true, "VI3: las pastillas de vidrio (BUSCAR, AYUDA, letras, En cola, creditos)"),
                    RocolaOsClave.SiNo("vidrio_lista", false, true, "VI4: la lista del disco de vidrio tenida por la portada (apagada hasta medirla en el A4)"),
                    RocolaOsClave.SiNo("vidrio_luz", true, true, "VI5: el filo de luz de los vidrios sigue al cielo"),
                    RocolaOsClave.SiNo("vidrio_buscador", false, true, "VI6: el buscador, los avisos y las ventanas del dueno de vidrio (apagada hasta medirla en el A4)"),
                },
                Arranca = f => Arranca(f),
                Latido = f => Late(f),
                AlCerrar = (f, razon) => GuardaReloj(),
                ParaAlCerrar = razon => ParaHilo(),
            });
        }

        // ---------- lo que se lee desde cualquier hilo ----------
        private static RocolaOsCieloFoto foto;            // la de ahora (null: el cielo vivo no va; los ganchos dan lo de XP)
        private static RocolaOsCieloFoto fotoAntes;       // la anterior (el cuadro interpola de esta a la de ahora)
        private static double fotoDesdeMs, fotoDuraMs = 1000;
        internal static RocolaOsCieloFoto Foto { get { return Volatile.Read(ref foto); } }
        // si el cielo vivo va en esta sesion (ROCOLA_OS y alguna de las cuatro funciones encendida, ya arrancada)
        internal static bool Va { get { return Volatile.Read(ref foto) != null && !NucleoCaido; } }   // (NucleoCaido: RocolaOs.CieloEnchufes.cs)

        // ---------- el arranque (en la pantalla, antes de ensenarse la ventana) ----------
        private static bool arrancado;
        private static DateTime inicioReal;
        private static DateTime? fechaPrueba;
        private static void Arranca(Form1 r)
        {
            if (arrancado) return;   // (las cuatro funciones lo llaman: una vez)
            arrancado = true;
            inicioReal = DateTime.Now;
            string e = Environment.GetEnvironmentVariable("ROCOLA_CIELO_FECHA");
            DateTime fp;
            if (!string.IsNullOrEmpty(e))
            {
                if (DateTime.TryParse(e.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out fp)) fechaPrueba = DateTime.SpecifyKind(fp, DateTimeKind.Local);
                else RocolaOs.Apunta("cielo", "ROCOLA_CIELO_FECHA no vale («" + e + "»): se usa la hora de verdad");
            }
            ArrancaHilo();
            RocolaOsCieloFoto f = HazFoto(false);
            Volatile.Write(ref fotoAntes, f);
            Volatile.Write(ref foto, f);
            fotoDesdeMs = -1;
            RocolaOs.Apunta("cielo", "arranca: " + Resumen(f) + (fechaPrueba.HasValue ? " (ROCOLA_CIELO_FECHA)" : ""));
            fasesApuntada = f.Fase; lugarApuntado = f.Lugar;
        }

        // ---------- el latido (1 s, en la pantalla: rapido, sin disco ni red) ----------
        private static long ultimoLatido = -1;
        private static volatile bool otraFoto;
        private static string fasesApuntada, climaApuntado, fiestasApuntadas, lugarApuntado;
        internal static void OtraFoto() { otraFoto = true; }
        private static void Late(Form1 r)
        {
            long n = RocolaOs.Latidos;
            if (n == ultimoLatido) return;   // (una vez por latido aunque lo llamen las cuatro funciones)
            ultimoLatido = n;
            otraFoto = false;
            MiraElBuzon();
            bool salto = RevisaVerComo(r);
            PonFoto(HazFoto(salto));
        }
        private static void PonFoto(RocolaOsCieloFoto f)
        {
            RocolaOsCieloFoto antes = Volatile.Read(ref foto);
            Volatile.Write(ref fotoAntes, antes == null ? f : LoQueSeVe(antes));
            Volatile.Write(ref foto, f);
            fotoDesdeMs = -1;   // (el cuadro siguiente toma la hora de la tarjeta)
            fotoDuraMs = f.Salto ? 1500 : 1000;
            if (f.Fase != fasesApuntada) { RocolaOs.Apunta("cielo", "fase del dia: " + f.Fase + " (sol a " + f.SolAlt.ToString("0.0", CultureInfo.InvariantCulture) + " grados)"); fasesApuntada = f.Fase; }
            string cl = f.ClimaSabido ? f.Clima.Nombre + (f.Clima.Forzado ? " (forzado)" : " (de afuera, medido " + f.Clima.Medida.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC)") : "sin saber (despejado)";
            if (cl != climaApuntado) { RocolaOs.Apunta("cielo", "el clima del cielo: " + cl); climaApuntado = cl; }
            if (f.Lugar != lugarApuntado) { if (lugarApuntado != null) RocolaOs.Apunta("cielo", "el lugar del bar: " + f.Lugar + " (" + f.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + f.Lon.ToString("0.00", CultureInfo.InvariantCulture) + ")"); lugarApuntado = f.Lugar; }
            string fi = f.Hoy.Nombres;
            if (fi != fiestasApuntadas) { RocolaOs.Apunta("cielo", "lo de hoy (" + f.Local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "): " + fi); fiestasApuntadas = fi; }
        }

        // LO QUE SE VE AHORA (lo del ultimo cuadro, interpolado): de ahi parte la mezcla hacia la foto nueva, asi una foto que llega antes de que
        // acabara la mezcla anterior no da escalon
        private static RocolaOsCieloFoto LoQueSeVe(RocolaOsCieloFoto antes)
        {
            if (msAntes < 0) return antes;
            RocolaOsCieloFoto v = new RocolaOsCieloFoto();
            Array.Copy(PesosCuadro, v.Pesos, v.Pesos.Length);
            v.Noche = NocheCuadro; v.Estrellas = EstrellasCuadro; v.Nebulosa = NebulosaCuadro; v.Velo = VeloCuadro;
            v.Raya = ((uint)Math.Max(0, Math.Min(255, (int)(RayaR + 0.5f))) << 16) | ((uint)Math.Max(0, Math.Min(255, (int)(RayaG + 0.5f))) << 8) | (uint)Math.Max(0, Math.Min(255, (int)(RayaB + 0.5f)));
            v.Salto = antes.Salto;
            return v;
        }

        // LA HORA DEL CIELO: «Ver como se veria», ROCOLA_CIELO_FECHA (corre desde ahi con el reloj) o la de verdad
        internal static DateTime AhoraLocal()
        {
            if (verHasta > 0 && verLocal.HasValue) return verLocal.Value + (DateTime.Now - verDesde);
            if (fechaPrueba.HasValue) return fechaPrueba.Value + (DateTime.Now - inicioReal);
            return DateTime.Now;
        }
        // LA CIUDAD ELEGIDA (ciudad_bar=): 0 automatica
        internal static int CiudadElegida { get { int c = RocolaOs.AjusteEntero("ciudad_bar"); return c < 0 || c > RocolaOsCiudades.Maxima ? 0 : c; } }
        // EL LUGAR DEL BAR: ROCOLA_CIUDAD (pruebas), la ciudad elegida, la de internet, o la Ciudad de Mexico mientras no se sepa
        internal static void LugarDelBar(out double lat, out double lon, out string nombre)
        {
            RocolaOsLugar p = CiudadForzada;
            if (p != null) { lat = p.Lat; lon = p.Lon; nombre = p.Ciudad; return; }
            int c = CiudadElegida;
            if (c > 0) { lat = RocolaOsCiudades.Lat[c]; lon = RocolaOsCiudades.Lon[c]; nombre = RocolaOsCiudades.Nombres[c]; return; }
            RocolaOsLugar g = Lugar;
            if (g != null) { lat = g.Lat; lon = g.Lon; nombre = (g.Ciudad.Length > 0 ? g.Ciudad : "?") + " (por internet)"; return; }
            lat = RocolaOsCiudades.Lat[0]; lon = RocolaOsCiudades.Lon[0]; nombre = "Ciudad de México (aún no se sabe el lugar)";
        }

        private static int version;
        // HACE UNA FOTO (en la pantalla: cuentas y lo que el hilo ya dejo en memoria)
        internal static RocolaOsCieloFoto HazFoto(bool salto)
        {
            RocolaOsCieloFoto f = new RocolaOsCieloFoto();
            f.Version = ++version;
            f.Salto = salto;
            f.Local = DateTime.SpecifyKind(AhoraLocal(), DateTimeKind.Local);
            f.Utc = f.Local.ToUniversalTime();
            f.VerComo = verHasta > 0;
            f.Ciudad = CiudadElegida;
            LugarDelBar(out f.Lat, out f.Lon, out f.Lugar);
            RocolaOsAstro.Sol(f.Utc, f.Lat, f.Lon, out f.SolAlt, out f.SolAz);
            double a2, z2; RocolaOsAstro.Sol(f.Utc.AddMinutes(10), f.Lat, f.Lon, out a2, out z2);
            f.SolSube = a2 > f.SolAlt;
            RocolaOsAstro.Luna(f.Utc, f.Lat, f.Lon, out f.LunaAlt, out f.LunaAz, out f.LunaFase);
            f.LunaLuz = RocolaOsAstro.Iluminada(f.LunaFase);
            RocolaOsCieloDia.Pesos(f.SolAlt, f.SolSube, f.Pesos);
            RocolaOsCieloDia.Mezcla(f);
            // las ideas
            f.Cielo = RocolaOs.Encendida(Funcion); f.Fiestas = RocolaOs.Encendida(FuncionFiestas);
            f.ClimaVa = RocolaOs.Encendida(FuncionClima); f.Vidrio = RocolaOs.Encendida(FuncionVidrio);
            f.DN1 = f.Cielo && RocolaOs.AjusteSiNo("cielo_sol"); f.DN2 = f.Cielo && RocolaOs.AjusteSiNo("sol"); f.DN3B = f.Cielo && RocolaOs.AjusteSiNo("luna_camino");
            f.DN4 = f.Cielo && RocolaOs.AjusteSiNo("estrellas_anochecer"); f.DN5 = f.Cielo && RocolaOs.AjusteSiNo("horizonte_hora"); f.DN6 = f.Cielo && RocolaOs.AjusteSiNo("nubes");
            f.FI1 = f.Fiestas && RocolaOs.AjusteSiNo("aurora_fiesta"); f.FI2 = f.Fiestas && RocolaOs.AjusteSiNo("horizonte_fiesta");
            f.FI3 = f.Fiestas && RocolaOs.AjusteSiNo("fugaz_fiesta"); f.FI4 = f.Fiestas && RocolaOs.AjusteSiNo("fuegos");
            f.FI5 = f.Fiestas && RocolaOs.AjusteSiNo("escaparate_fiesta"); f.FI6 = f.Fiestas && RocolaOs.AjusteSiNo("fechas_propias");
            f.CL1 = f.ClimaVa; f.CL2 = f.ClimaVa && RocolaOs.AjusteSiNo("lluvia"); f.CL3 = f.ClimaVa && RocolaOs.AjusteSiNo("tormenta");
            f.CL4 = f.ClimaVa && RocolaOs.AjusteSiNo("neblina"); f.CL5 = f.ClimaVa && RocolaOs.AjusteSiNo("clima_escaparate");
            f.VI1 = f.Vidrio && RocolaOs.AjusteSiNo("vidrio_ficha"); f.VI2 = f.Vidrio && RocolaOs.AjusteSiNo("vidrio_franja"); f.VI3 = f.Vidrio && RocolaOs.AjusteSiNo("vidrio_pastillas");
            f.VI4 = f.Vidrio && RocolaOs.AjusteSiNo("vidrio_lista"); f.VI5 = f.Vidrio && RocolaOs.AjusteSiNo("vidrio_luz"); f.VI6 = f.Vidrio && RocolaOs.AjusteSiNo("vidrio_buscador");
            // el clima: el de «Ver como se veria», el de ROCOLA_CLIMA o el de afuera si aun vale (3 h); si no, despejado
            RocolaOsClima c = verHasta > 0 && verClima != null ? verClima : (climaForzado ?? ClimaGuardado);
            if (c != null && c.Vigente(DateTime.UtcNow)) { f.Clima = c; f.ClimaSabido = true; }
            // el calendario (lo de hoy, con rocola_fechas.txt ya leido por el hilo)
            f.Hoy = RocolaOsCalendario.De(f.Local, Fechas);
            return f;
        }
        internal static string Resumen(RocolaOsCieloFoto f)
        {
            return "hora " + f.Local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "; lugar " + f.Lugar + " (" + f.Lat.ToString("0.00", CultureInfo.InvariantCulture)
                + ", " + f.Lon.ToString("0.00", CultureInfo.InvariantCulture) + "); sol " + f.SolAlt.ToString("0.0", CultureInfo.InvariantCulture) + " grados ("
                + (f.SolSube ? "sube" : "baja") + "); fase " + f.Fase + "; luna " + f.LunaFase.ToString("0.00", CultureInfo.InvariantCulture) + "; clima "
                + (f.ClimaSabido ? f.Clima.Nombre : "sin saber (despejado)") + "; fiestas " + f.Hoy.Nombres;
        }

        // ---------- vivo.txt ----------
        private static void Vivo(StringBuilder sb)
        {
            RocolaOsCieloFoto f = Foto;
            if (f == null) return;
            sb.Append("cielo=fase ").Append(f.Fase).Append(" sol ").Append(f.SolAlt.ToString("0.0", CultureInfo.InvariantCulture))
              .Append(" lugar ").Append(f.Lugar.Replace('\n', ' ')).Append(" clima ").Append(f.ClimaSabido ? f.Clima.Nombre : "sin_saber")
              .Append(" fiestas ").Append(f.Hoy.Nombres).Append(" calma ").Append(escena.Calma.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(verHasta > 0 ? " ver_como" : "").Append('\n');
        }

        // ================================================================ EL CUADRO (gancho g120: en la pantalla, al empezar cada cuadro)
        internal static RocolaOsCieloEscena escena;                       // la de este cuadro (solo la pantalla la escribe)
        internal static RocolaOsCieloEscena Escena { get { return escena; } }
        private static GpuStarfield estrellasPrincipal;                   // el campo de la rocola (no el de la ventana de prueba F12)
        internal static GpuStarfield Principal { get { return estrellasPrincipal; } }
        // lo de este cuadro, interpolado de la foto anterior a la de ahora (sin memoria nueva: arreglos fijos)
        internal static readonly float[] PesosCuadro = new float[RocolaOsCieloDia.Fases];
        internal static float NocheCuadro = 1f, EstrellasCuadro = 1f, NebulosaCuadro = 1f, VeloCuadro, RayaR, RayaG, RayaB;
        private static float horizonteApuntado = -1000f; private static double horizonteApuntadoMs = -1e9;
        private static double calmaPaso = 1;   // 1 normal, 0 calmado del todo (se suaviza al dar la calma)
        private static double msAntes = -1;
        internal static void Cuadro(GpuStarfield estrellas, int ancho, int alto, float horizonte, float estadoArriba, int modo, bool pared, float reposo, double ms)
        {
            RocolaOsCieloFoto f = Volatile.Read(ref foto);
            if (f == null) return;
            estrellasPrincipal = estrellas;
            double dt = msAntes < 0 ? 0 : Math.Max(0, Math.Min(0.25, (ms - msAntes) / 1000.0));
            msAntes = ms;
            escena.Ancho = ancho; escena.Alto = alto; escena.Horizonte = horizonte; escena.EstadoArriba = estadoArriba; escena.Modo = modo; escena.Pared = pared;
            escena.Reposo = reposo; escena.Ms = ms; escena.Dt = dt;
            // la calma (regla 5): baja a 0,35 en 0,4 s y vuelve a 1 en 3 s
            Form1 r = RocolaOs.LaRocola;
            bool eligiendo = r != null && r.RocolaOsCieloEligiendo();
            if (r != null && verHasta > 0 && !verDesdeLaHoja && r.RocolaOsUltimaTecla() != verTecla) QuitaVerComo("se toco un boton");
            escena.Calma = PasoCalma(ref calmaPaso, eligiendo, dt);
            if (calmaLog != null && ms - calmaLogMs >= 100) { calmaLogMs = ms; calmaLog.Append((ms / 1000).ToString("0.00", CultureInfo.InvariantCulture)).Append(' ').Append(escena.Calma.ToString("0.000", CultureInfo.InvariantCulture)).Append(eligiendo ? " elige\n" : "\n"); }
            escena.Eligiendo = eligiendo;
            // lo de la foto, interpolado en 1 s (1,5 s si salta)
            if (fotoDesdeMs < 0) fotoDesdeMs = ms;
            RocolaOsCieloFoto a = Volatile.Read(ref fotoAntes) ?? f;
            float k = (float)Math.Max(0, Math.Min(1, (ms - fotoDesdeMs) / fotoDuraMs));
            for (int i = 0; i < PesosCuadro.Length; i++) PesosCuadro[i] = a.Pesos[i] + (f.Pesos[i] - a.Pesos[i]) * k;
            NocheCuadro = PesosCuadro[0];
            if (f.Pesos[0] >= 1f && k >= 1f) NocheCuadro = 1f;
            EstrellasCuadro = a.Estrellas + (f.Estrellas - a.Estrellas) * k;
            NebulosaCuadro = a.Nebulosa + (f.Nebulosa - a.Nebulosa) * k;
            VeloCuadro = a.Velo + (f.Velo - a.Velo) * k;
            RocolaOsCieloDia.RayaMezcla(a.Raya, f.Raya, k, out RayaR, out RayaG, out RayaB);
            RocolaOsCieloDia.AlCuadro(f);
            RelojCuadro(ms);
            // (para la bitacora: el primero y despues como mucho uno por hora; el reposo con musica lo mueve cada vez que entra o sale)
            if (Math.Abs(horizonte - horizonteApuntado) > 20f && ms - horizonteApuntadoMs > 3600000)
            { horizonteApuntado = horizonte; horizonteApuntadoMs = ms; RocolaOs.Apunta("cielo", "el horizonte de la escena: " + horizonte.ToString("0", CultureInfo.InvariantCulture) + " de " + alto + " px (modo " + modo + ")"); }
        }
        internal const double CalmaMinima = 0.35, CalmaBajaS = 0.4, CalmaSubeS = 3.0;
        // UN PASO DE LA CALMA (regla 5): 'paso' va de 1 (normal) a 0 (calmado) en 0,4 s mientras alguien elige y vuelve en 3 s; lo que da sigue una
        // curva suave de 1 a 0,35. Pura (la prueba la recorre).
        internal static float PasoCalma(ref double paso, bool eligiendo, double dt)
        {
            paso += eligiendo ? -dt / CalmaBajaS : dt / CalmaSubeS;
            if (paso < 0) paso = 0; else if (paso > 1) paso = 1;
            double s = paso * paso * (3 - 2 * paso);
            return (float)(CalmaMinima + (1 - CalmaMinima) * s);
        }
        internal const int EligiendoSegundos = 15;
        // Ctrl+F4 (y lo que abra otra hoja del dueno de Rocola OS) lo dice aqui mientras esta abierta: cuenta como «alguien elige» (regla 5)
        internal static int HojasAbiertas;

        // ---------- «Ver como se veria» (§3): fecha, hora y clima forzados 30 s; no se guarda ----------
        private static long verHasta;          // (TickCount64; 0 = no)
        private static DateTime? verLocal; private static DateTime verDesde; private static RocolaOsClima verClima; private static DateTime verTecla; private static bool verDesdeLaHoja;
        internal const int VerComoSegundos = 30;
        // LO LLAMA la hoja «Imagen» de Ctrl+F4 (y el buzon, abajo): en la pantalla. local: la hora y fecha que se quiere ver (null: la de ahora);
        // clima: un tipo (RocolaOsClima.TipoDe; null o «»: el de ahora). Se quita solo a los 30 s, al tocar un boton (que no sea de la hoja) o con
        // QuitaVerComo (al cerrar la hoja). desdeLaHoja: las teclas no lo quitan (la hoja lleva las suyas; ella llama QuitaVerComo con las de
        // fuera y al cerrarse). Devuelve lo que se puso, para la hoja o el buzon.
        internal static string VerComo(DateTime? local, string clima) { return VerComo(local, clima, false); }
        internal static string VerComo(DateTime? local, string clima, bool desdeLaHoja)
        {
            if (!Va) return "mal: el cielo vivo no va (ROCOLA_OS o las cuatro funciones apagadas)";
            RocolaOsClima c = string.IsNullOrWhiteSpace(clima) ? null : RocolaOsClima.Forzar(clima);
            if (!string.IsNullOrWhiteSpace(clima) && c == null) return "mal: el clima «" + clima + "» no es ninguno (despejado, poco, nublado, neblina, llovizna, lluvia, aguacero, tormenta, nieve)";
            verLocal = local.HasValue ? DateTime.SpecifyKind(local.Value, DateTimeKind.Local) : (DateTime?)null;
            verDesde = DateTime.Now; verClima = c;
            Form1 r = RocolaOs.LaRocola;
            verTecla = r != null ? r.RocolaOsUltimaTecla() : DateTime.MinValue;
            verDesdeLaHoja = desdeLaHoja;
            verHasta = Environment.TickCount64 + VerComoSegundos * 1000L;
            RocolaOsCieloFoto f = HazFoto(true);
            PonFoto(f);
            string que = (verLocal.HasValue ? verLocal.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "la hora de ahora")
                + (c != null ? " con " + c.Nombre : "");
            RocolaOs.Apunta("cielo", "ver como se veria: " + que + " (30 s): " + Resumen(f));
            return "bien: " + que + " por 30 s";
        }
        internal static void QuitaVerComo(string por)
        {
            if (verHasta == 0) return;
            verHasta = 0; verLocal = null; verClima = null;
            PonFoto(HazFoto(true));
            RocolaOs.Apunta("cielo", "ver como se veria: se quita (" + por + ")");
        }
        private static bool RevisaVerComo(Form1 r)
        {
            if (verHasta > 0 && Environment.TickCount64 >= verHasta) { verHasta = 0; verLocal = null; verClima = null; RocolaOs.Apunta("cielo", "ver como se veria: se acaban los 30 s"); return true; }
            return false;
        }
        // EL BUZON (mientras no esta la hoja): /run/rocola-cielo-ver con «AAAA-MM-DD HH:MM», «HH:MM», «;clima», «AAAA-MM-DD HH:MM;clima» o «quitar».
        // Lo lee el latido (/run esta en memoria), lo borra y deja la respuesta en /run/rocola-cielo-ver.respuesta.
        private static void MiraElBuzon()
        {
            string ruta = RocolaOs.Run("rocola-cielo-ver");
            if (!File.Exists(ruta)) return;
            string pedido = "", resp;
            try { pedido = File.ReadAllText(ruta).Trim(); File.Delete(ruta); } catch (Exception ex) { RocolaOs.Falla("cielo", "leer " + ruta, ex); return; }
            resp = VerComoDeTexto(pedido);
            try { File.WriteAllText(ruta + ".respuesta", resp + "\n"); } catch { }
        }
        internal static string VerComoDeTexto(string pedido)
        {
            pedido = (pedido ?? "").Trim();
            if (pedido == "quitar") { QuitaVerComo("el buzon"); return "bien: se quito"; }
            string fecha = pedido, clima = null;
            int p = pedido.IndexOf(';');
            if (p >= 0) { fecha = pedido.Substring(0, p).Trim(); clima = pedido.Substring(p + 1).Trim(); }
            DateTime? local = null;
            if (fecha.Length > 0)
            {
                DateTime d; TimeSpan h;
                if (RocolaOsFechasArchivo.Hora(fecha, out h)) local = AhoraLocal().Date + h;
                else if (DateTime.TryParse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out d)) local = d;
                else return "mal: «" + fecha + "» no es una fecha (AAAA-MM-DD HH:MM) ni una hora (HH:MM)";
            }
            return VerComo(local, clima);
        }

        // ---------- el reloj de los cuadros (pruebas: ROCOLA_CIELO_RELOJ=archivo; lo que tardo el trabajo de cada cuadro) ----------
        private static string relojArchivo; private static bool relojLeido;
        private static double[] relojMs; private static int relojN; private static double relojDesde = -1;
        private static void RelojCuadro(double ms)
        {
            if (!relojLeido)
            {
                relojLeido = true; relojArchivo = Environment.GetEnvironmentVariable("ROCOLA_CIELO_RELOJ"); if (!string.IsNullOrEmpty(relojArchivo)) relojMs = new double[200000];
                calmaArchivo = Environment.GetEnvironmentVariable("ROCOLA_CIELO_CALMA"); if (!string.IsNullOrEmpty(calmaArchivo)) calmaLog = new StringBuilder(1 << 16);
            }
        }
        // (pruebas: ROCOLA_CIELO_CALMA=archivo) la calma cada 0,1 s («segundos calma [elige]»), al cerrar
        private static string calmaArchivo; private static StringBuilder calmaLog; private static double calmaLogMs = -1e9;
        // (lo llama el gancho con lo que tardo el cuadro anterior; desde los 8 s, para no medir el arranque)
        private static double relojEscritoMs;
        internal static void TomaReloj(double trabajoMs, double ms)
        {
            if (!relojLeido) RelojCuadro(ms);
            if (relojMs == null) return;
            if (ms - relojEscritoMs >= 5000 && relojN > 0) { relojEscritoMs = ms; EscribeReloj(); }   // (cada 5 s: con las funciones apagadas no hay AlCerrar)
            if (relojDesde < 0) relojDesde = ms;
            if (ms - relojDesde < 8000 || relojN >= relojMs.Length || !(trabajoMs > 0)) return;
            relojMs[relojN++] = trabajoMs;
        }
        private static bool relojGuardado;
        internal static void GuardaReloj()
        {
            if (calmaLog != null && !relojGuardado) try { File.WriteAllText(calmaArchivo, calmaLog.ToString()); } catch { }
            if (relojMs == null || relojN == 0 || relojGuardado) { relojGuardado = true; return; }
            relojGuardado = true;
            EscribeReloj();
        }
        private static void EscribeReloj()
        {
            double[] v = new double[relojN]; Array.Copy(relojMs, v, relojN); Array.Sort(v);
            Func<double, double> p = q => v[Math.Min(v.Length - 1, (int)Math.Floor(q * (v.Length - 1) + 0.5))];
            string s = "cuadros=" + v.Length + " p50=" + p(0.5).ToString("0.00", CultureInfo.InvariantCulture) + " p95=" + p(0.95).ToString("0.00", CultureInfo.InvariantCulture)
                + " p99=" + p(0.99).ToString("0.00", CultureInfo.InvariantCulture) + " max=" + v[v.Length - 1].ToString("0.00", CultureInfo.InvariantCulture) + "\n";
            try { File.WriteAllText(relojArchivo, s); } catch { }
        }
    }

    public partial class Form1
    {
        // LO QUE SABE XP DE SI ALGUIEN ELIGE (regla 5): un boton hace menos de 15 s (lastInput: teclas y raton), o el buscador, la estrella (la
        // ayuda), Ctrl+F3 u otra hoja del dueno de Rocola OS (Ctrl+F4) abiertos. En la pantalla.
        internal bool RocolaOsCieloEligiendo()
        {
            return (DateTime.Now - lastInput).TotalSeconds < RocolaOsCielo.EligiendoSegundos || searchOpen || ajustesAbiertos || AsistenteVisible
                || Volatile.Read(ref RocolaOsCielo.HojasAbiertas) > 0;
        }
        internal DateTime RocolaOsUltimaTecla() { return lastInput; }
    }
}
