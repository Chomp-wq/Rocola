// EL CIELO SEGUN LA HORA, LA FECHA Y EL CLIMA (decision del 7-11-2026: «Implementa todo [...] incrementa la cantidad de decoracion por
// fechas [...] y que tambien tome los datos del clima del internet»). Solo Rocola OS. Aqui va lo que no es dibujo: los interruptores,
// donde esta el bar, la altura del sol y de la luna (cuentas de bolsillo, de sobra para el cielo dibujado), el calendario de fiestas y
// lluvias de estrellas, y el clima de afuera (Open-Meteo: gratuito y sin llave, por curl, cada 15 minutos y solo con el interruptor).
// La ubicacion: la ciudad elegida en Ctrl+F3 o, en «Automatica», la de la conexion a internet (ipapi.co, una vez por semana).
// El dibujo esta en Consola/Pantalla/CieloHora.cs.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        // ---- los interruptores (Ctrl+F3 -> Fondo), encendidos de fabrica ----
        public bool CieloHora = true;          // H1 "cielo_hora=": los colores del cielo segun la hora (el sol)
        public bool SolHora = true;            // H2 "sol_hora=": el sol se pone y sale
        public bool EstrellasAnochecer = true; // H3 "estrellas_anochecer=": las estrellas se encienden al oscurecer
        public bool LunaArco = true;           // H4 "luna_arco=": la luna cruza el cielo a sus horas reales
        public bool LunaSinDisco = true;       // H7 "luna_sin_disco=": su lado oscuro deja ver el cielo
        public bool AuroraHora = true;         // H5 "aurora_hora=": la aurora toma el color de la hora
        public bool MedianocheCielo = true;    // H6 "medianoche=": fugaces y destello a las 12
        public bool FiestasCielo = true;       // F1 "fiestas_cielo=": las fiestas del ano
        public bool LluviasEstrellas = true;   // F2 "lluvias_estrellas=": en sus noches reales
        public bool LunaLlenaCielo = true;     // F3 "luna_llena=": la luna llena mas grande y con halo
        public bool Constelaciones = true;     // O1 "constelaciones=": se dibujan de vez en cuando
        public bool NubesCielo = true;         // O2 "nubes_cielo=": unas nubes dibujadas
        public bool ClimaCielo = true;         // O3 "clima_cielo=": el clima de afuera (internet)
        public int CiudadBar;                  // "ciudad_bar=": 0 automatica (por internet); si no, una de Ciudades

        // ---- la ciudad ----
        public static readonly string[] Ciudades = { "Automática (por internet)", "Acapulco", "Aguascalientes", "Campeche", "Cancún", "Chetumal", "Chihuahua",
            "Chilpancingo", "Ciudad de México", "Ciudad Juárez", "Ciudad Victoria", "Colima", "Cuernavaca", "Culiacán", "Durango", "Guadalajara",
            "Guanajuato", "Hermosillo", "La Paz", "León", "Mazatlán", "Mérida", "Mexicali", "Monterrey", "Morelia", "Oaxaca", "Pachuca", "Puebla",
            "Querétaro", "Saltillo", "San Luis Potosí", "Tampico", "Tepic", "Tijuana", "Tlaxcala", "Toluca", "Torreón", "Tuxtla Gutiérrez",
            "Veracruz", "Villahermosa", "Xalapa", "Zacatecas" };
        private static readonly double[] CiudadLat = { 19.43, 16.85, 21.88, 19.85, 21.16, 18.50, 28.63, 17.55, 19.43, 31.69, 23.74, 19.24, 18.92, 24.80, 24.02, 20.67,
            21.02, 29.07, 24.14, 21.12, 23.25, 20.97, 32.62, 25.69, 19.70, 17.07, 20.10, 19.04, 20.59, 25.42, 22.15, 22.25, 21.50, 32.51, 19.32, 19.29,
            25.54, 16.75, 19.17, 17.99, 19.54, 22.77 };
        private static readonly double[] CiudadLon = { -99.13, -99.88, -102.29, -90.53, -86.85, -88.30, -106.07, -99.50, -99.13, -106.42, -99.15, -103.72, -99.22,
            -107.39, -104.66, -103.35, -101.26, -110.96, -110.31, -101.68, -106.41, -89.62, -115.45, -100.32, -101.19, -96.72, -98.75, -98.20, -100.39,
            -101.00, -100.98, -97.86, -104.89, -117.04, -98.24, -99.66, -103.41, -93.12, -96.13, -92.93, -96.91, -102.58 };
        private double lugarLat = double.NaN, lugarLon = double.NaN; private string lugarNombre = "";
        // Donde esta el bar: la ciudad elegida, la de internet, o la Ciudad de Mexico mientras no se sepa.
        public double LatBar { get { int c = CiudadBar; if (c > 0 && c < Ciudades.Length) return CiudadLat[c]; return double.IsNaN(lugarLat) ? CiudadLat[0] : lugarLat; } }
        public double LonBar { get { int c = CiudadBar; if (c > 0 && c < Ciudades.Length) return CiudadLon[c]; return double.IsNaN(lugarLon) ? CiudadLon[0] : lugarLon; } }
        public string LugarBar
        {
            get
            {
                int c = CiudadBar; if (c > 0 && c < Ciudades.Length) return Ciudades[c];
                return lugarNombre.Length > 0 ? lugarNombre + " (por internet)" : "aún no se sabe (Ciudad de México mientras)";
            }
        }

        // ---- la hora del cielo (ROCOLA_CIELO_FECHA=2026-11-02T21:30: para probar; corre desde ahi con el reloj) ----
        private static readonly DateTime inicioReal = DateTime.Now;
        private static readonly DateTime? cieloFijo = LeeFechaPrueba();
        private static DateTime? LeeFechaPrueba()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_CIELO_FECHA");
            DateTime f;
            if (!string.IsNullOrEmpty(e) && DateTime.TryParse(e, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out f)) return f;
            return null;
        }
        public DateTime AhoraCielo { get { return cieloFijo.HasValue ? cieloFijo.Value + (DateTime.Now - inicioReal) : DateTime.Now; } }

        // ---- el clima ----
        public enum TipoClima { Desconocido, Despejado, PocoNublado, Nublado, Neblina, Llovizna, Lluvia, LluviaFuerte, Tormenta, Nieve }
        private volatile int climaTipo; private float climaNubes, climaViento, climaTemp = float.NaN; private DateTime climaCuando;
        private string climaFallo = ""; private Thread hiloClima;
        public TipoClima Clima { get { return ClimaCielo ? (TipoClima)climaTipo : TipoClima.Desconocido; } }
        public float ClimaNubes { get { return climaNubes; } }   // 0..1
        public float ClimaViento { get { return climaViento; } } // km/h
        public static string NombreClima(TipoClima c)
        {
            switch (c)
            {
                case TipoClima.Despejado: return "despejado"; case TipoClima.PocoNublado: return "algo nublado"; case TipoClima.Nublado: return "nublado";
                case TipoClima.Neblina: return "neblina"; case TipoClima.Llovizna: return "llovizna"; case TipoClima.Lluvia: return "lluvia";
                case TipoClima.LluviaFuerte: return "lluvia fuerte"; case TipoClima.Tormenta: return "tormenta"; case TipoClima.Nieve: return "nieve";
                default: return "sin saber";
            }
        }
        // Para la hoja: lo que dice el clima y de donde.
        public string EstadoClima
        {
            get
            {
                if (!ClimaCielo) return "El clima de afuera está apagado.";
                string forzado = Environment.GetEnvironmentVariable("ROCOLA_CLIMA");
                if (!string.IsNullOrEmpty(forzado)) return "Clima de prueba: " + NombreClima(Clima) + ".";
                if (climaCuando == default(DateTime)) return "Aún no hay clima" + (climaFallo.Length > 0 ? " (" + climaFallo + ")" : " (se pide a internet al arrancar)") + ". Lugar: " + LugarBar + ".";
                int min = (int)(DateTime.Now - climaCuando).TotalMinutes;
                return "Afuera: " + NombreClima(Clima) + (float.IsNaN(climaTemp) ? "" : ", " + Math.Round(climaTemp) + " °C") + " en " + LugarBar + " (hace " + Math.Max(0, min) + " min).";
            }
        }

        // La pantalla lo llama en cada cuadro: arranca el hilo del clima la primera vez que hace falta (no cuesta nada si ya va).
        public void ClimaAlDia()
        {
            if (!ClimaCielo || hiloClima != null) return;
            string forzado = Environment.GetEnvironmentVariable("ROCOLA_CLIMA");
            if (!string.IsNullOrEmpty(forzado)) { climaTipo = (int)ClimaDePrueba(forzado); climaNubes = climaTipo >= (int)TipoClima.Nublado ? 0.95f : 0.4f; climaViento = 12; hiloClima = Thread.CurrentThread; return; }
            LeeClimaGuardado();
            hiloClima = new Thread(BucleClima) { IsBackground = true, Name = "clima", Priority = ThreadPriority.Lowest };
            hiloClima.Start();
        }
        private static TipoClima ClimaDePrueba(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "despejado": return TipoClima.Despejado; case "poco": return TipoClima.PocoNublado; case "nublado": return TipoClima.Nublado;
                case "neblina": return TipoClima.Neblina; case "llovizna": return TipoClima.Llovizna; case "lluvia": return TipoClima.Lluvia;
                case "fuerte": return TipoClima.LluviaFuerte; case "tormenta": return TipoClima.Tormenta; case "nieve": return TipoClima.Nieve;
                default: return TipoClima.Desconocido;
            }
        }
        private string ArchivoClima { get { return RutaDatos("rocola_clima.txt"); } }
        private string ArchivoLugar { get { return RutaDatos("rocola_clima_lugar.txt"); } }
        // El ultimo clima guardado vale 3 horas (asi un reinicio sin internet no se queda en blanco).
        private void LeeClimaGuardado()
        {
            try
            {
                if (File.Exists(ArchivoLugar))
                {
                    string[] p = File.ReadAllText(ArchivoLugar).Split('\t');
                    if (p.Length >= 3) { lugarLat = double.Parse(p[0], CultureInfo.InvariantCulture); lugarLon = double.Parse(p[1], CultureInfo.InvariantCulture); lugarNombre = p[2].Trim(); }
                }
                if (!File.Exists(ArchivoClima)) return;
                string[] c = File.ReadAllText(ArchivoClima).Split('\t');
                if (c.Length < 5) return;
                DateTime cuando = DateTime.FromBinary(long.Parse(c[0], CultureInfo.InvariantCulture));
                if ((DateTime.Now - cuando).TotalHours > 3) return;
                climaTipo = int.Parse(c[1], CultureInfo.InvariantCulture); climaNubes = float.Parse(c[2], CultureInfo.InvariantCulture);
                climaViento = float.Parse(c[3], CultureInfo.InvariantCulture); climaTemp = float.Parse(c[4], CultureInfo.InvariantCulture); climaCuando = cuando;
            }
            catch { }
        }
        private void BucleClima()
        {
            if (Environment.GetEnvironmentVariable("ROCOLA_CLIMA_YA") == null) Thread.Sleep(20000);   // que arranque primero lo demas (ROCOLA_CLIMA_YA: para probar)
            while (true)
            {
                int espera = 15 * 60;
                try
                {
                    if (ClimaCielo)
                    {
                        if (CiudadBar == 0) BuscaLugar();
                        if (!PideClima()) espera = climaCuando == default(DateTime) ? 120 : 5 * 60;
                    }
                }
                catch (Exception ex) { climaFallo = ex.Message; espera = 5 * 60; }
                Thread.Sleep(espera * 1000);
            }
        }
        // Donde esta la conexion (aproximado: la ciudad del proveedor de internet), una vez por semana.
        private DateTime lugarBuscado;
        private void BuscaLugar()
        {
            try { if (File.Exists(ArchivoLugar) && (DateTime.Now - File.GetLastWriteTime(ArchivoLugar)).TotalDays < 7 && !double.IsNaN(lugarLat)) return; } catch { }
            if ((DateTime.Now - lugarBuscado).TotalHours < 1) return;
            lugarBuscado = DateTime.Now;
            string propia = Environment.GetEnvironmentVariable("ROCOLA_UBICA_API");
            string[] apis = propia != null ? new[] { propia } : new[] { "https://ipapi.co/json/", "https://ipwho.is/", "https://get.geojs.io/v1/ip/geo.json" };
            bool hallado = false;
            foreach (string api in apis)
            {
                string o = CurlClima(api);
                if (o == null) continue;
                try
                {
                    using (JsonDocument d = JsonDocument.Parse(o))
                    {
                        JsonElement r = d.RootElement, ci; double la, lo;
                        if (!Coord(r, "latitude", out la) || !Coord(r, "longitude", out lo)) continue;
                        lugarLat = la; lugarLon = lo;
                        lugarNombre = r.TryGetProperty("city", out ci) && ci.ValueKind == JsonValueKind.String ? ci.GetString() : "";
                        hallado = true; break;
                    }
                }
                catch { }
            }
            if (!hallado) return;
            try { File.WriteAllText(ArchivoLugar, lugarLat.ToString(CultureInfo.InvariantCulture) + "\t" + lugarLon.ToString(CultureInfo.InvariantCulture) + "\t" + lugarNombre.Replace("\t", " ")); } catch { }
        }
        // Un numero que viene como numero o como texto (get.geojs.io los manda entre comillas).
        private static bool Coord(JsonElement r, string nombre, out double v)
        {
            v = 0; JsonElement e;
            if (!r.TryGetProperty(nombre, out e)) return false;
            if (e.ValueKind == JsonValueKind.Number) { v = e.GetDouble(); return true; }
            return e.ValueKind == JsonValueKind.String && double.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
        private bool PideClima()
        {
            string baseUrl = Environment.GetEnvironmentVariable("ROCOLA_CLIMA_API") ?? "https://api.open-meteo.com";
            string url = baseUrl + "/v1/forecast?latitude=" + LatBar.ToString("0.00", CultureInfo.InvariantCulture) + "&longitude=" + LonBar.ToString("0.00", CultureInfo.InvariantCulture)
                + "&current=weather_code,cloud_cover,wind_speed_10m,temperature_2m";
            string o = CurlClima(url);
            if (o == null) { climaFallo = "sin internet o no contestó"; return false; }
            using (JsonDocument d = JsonDocument.Parse(o))
            {
                JsonElement cur;
                if (!d.RootElement.TryGetProperty("current", out cur)) { climaFallo = "respuesta rara"; return false; }
                int codigo = Num(cur, "weather_code", 0) is double dc ? (int)dc : 0;
                float nubes = (float)(Num(cur, "cloud_cover", 0) ?? 0) / 100f, viento = (float)(Num(cur, "wind_speed_10m", 0) ?? 0);
                double? temp = Num(cur, "temperature_2m", double.NaN);
                climaTipo = (int)DeCodigo(codigo, nubes); climaNubes = Math.Max(0f, Math.Min(1f, nubes)); climaViento = viento;
                climaTemp = temp.HasValue ? (float)temp.Value : float.NaN; climaCuando = DateTime.Now; climaFallo = "";
            }
            try
            {
                File.WriteAllText(ArchivoClima, climaCuando.ToBinary().ToString(CultureInfo.InvariantCulture) + "\t" + climaTipo + "\t" + climaNubes.ToString(CultureInfo.InvariantCulture)
                    + "\t" + climaViento.ToString(CultureInfo.InvariantCulture) + "\t" + (float.IsNaN(climaTemp) ? "NaN" : climaTemp.ToString(CultureInfo.InvariantCulture)));
            }
            catch { }
            return true;
        }
        private static double? Num(JsonElement e, string nombre, double siNo)
        {
            JsonElement v;
            if (e.TryGetProperty(nombre, out v) && v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            return double.IsNaN(siNo) ? (double?)null : siNo;
        }
        // Los codigos de la OMM que da Open-Meteo.
        internal static TipoClima DeCodigo(int c, float nubes)
        {
            if (c >= 95) return TipoClima.Tormenta;
            if (c == 71 || c == 73 || c == 75 || c == 77 || c == 85 || c == 86) return TipoClima.Nieve;
            if (c == 65 || c == 82 || c == 67) return TipoClima.LluviaFuerte;
            if (c == 61 || c == 63 || c == 66 || c == 80 || c == 81) return TipoClima.Lluvia;
            if (c >= 51 && c <= 57) return TipoClima.Llovizna;
            if (c == 45 || c == 48) return TipoClima.Neblina;
            if (c == 3 || nubes >= 0.75f) return TipoClima.Nublado;
            if (c == 1 || c == 2 || nubes >= 0.3f) return TipoClima.PocoNublado;
            return TipoClima.Despejado;
        }
        private static string CurlClima(string url)
        {
            try
            {
                ProcessStartInfo pi = new ProcessStartInfo("curl") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string a in new[] { "-s", "-f", "--max-time", "10", "-A", "rocola", url }) pi.ArgumentList.Add(a);
                using (Process p = Process.Start(pi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(12000)) { try { p.Kill(); } catch { } return null; }
                    return p.ExitCode == 0 && o.Length > 0 ? o : null;
                }
            }
            catch { return null; }
        }
    }

    // ---- EL SOL Y LA LUNA (cuentas de bolsillo: un par de decimas de grado, de sobra para el cielo dibujado) ----
    public static class Astro
    {
        private const double G = Math.PI / 180;
        private static double Dias(DateTime utc) { return (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays; }
        private static void AltAz(double ra, double dec, double d, double lat, double lon, out double alt, out double az)
        {
            double lst = ((280.46061837 + 360.98564736629 * d + lon) % 360 + 360) % 360 * G, H = lst - ra, la = lat * G;
            double sa = Math.Sin(dec) * Math.Sin(la) + Math.Cos(dec) * Math.Cos(la) * Math.Cos(H);
            alt = Math.Asin(Math.Max(-1, Math.Min(1, sa))) / G;
            az = (Math.Atan2(-Math.Cos(dec) * Math.Sin(H), Math.Sin(dec) * Math.Cos(la) - Math.Cos(dec) * Math.Sin(la) * Math.Cos(H)) / G + 360) % 360;
        }
        private static double LongitudSol(double d)
        {
            double g = (357.529 + 0.98560028 * d) * G, q = 280.459 + 0.98564736 * d;
            return (q + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * G;
        }
        // La altura y el rumbo del sol (grados; rumbo desde el norte hacia el este).
        public static void Sol(DateTime utc, double lat, double lon, out double alt, out double az)
        {
            double d = Dias(utc), eps = (23.439 - 0.00000036 * d) * G, ls = LongitudSol(d);
            double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(ls), Math.Cos(ls)), dec = Math.Asin(Math.Sin(eps) * Math.Sin(ls));
            AltAz(ra, dec, d, lat, lon, out alt, out az);
        }
        // La de la luna, y su fase (0 nueva, 0,5 llena).
        public static void Luna(DateTime utc, double lat, double lon, out double alt, out double az, out double fase)
        {
            double d = Dias(utc), eps = (23.439 - 0.00000036 * d) * G;
            double L = 218.316 + 13.176396 * d, M = (134.963 + 13.064993 * d) * G, F = (93.272 + 13.229350 * d) * G;
            double lm = (L + 6.289 * Math.Sin(M)) * G, bm = 5.128 * Math.Sin(F) * G;
            double ra = Math.Atan2(Math.Sin(lm) * Math.Cos(eps) - Math.Tan(bm) * Math.Sin(eps), Math.Cos(lm));
            double dec = Math.Asin(Math.Sin(bm) * Math.Cos(eps) + Math.Cos(bm) * Math.Sin(eps) * Math.Sin(lm));
            AltAz(ra, dec, d, lat, lon, out alt, out az);
            double e = (lm - LongitudSol(d)) / G;
            fase = ((e % 360) + 360) % 360 / 360.0;
        }
    }

    // ---- EL CALENDARIO DEL CIELO: las fiestas (F1) y las lluvias de estrellas (F2) ----
    [Flags]
    public enum Fiesta
    {
        Ninguna = 0, AnioNuevo = 1, Reyes = 2, SanValentin = 4, Jacarandas = 8, DiaDelNino = 16, Madres = 32, Patrias = 64, Halloween = 128,
        Muertos = 256, Guadalupe = 512, Navidad = 1024, Nochebuena = 2048, Grito = 4096, FinDeAnio = 8192,
        Candelaria = 1 << 14, Bandera = 1 << 15, Mujer = 1 << 16, SanPatricio = 1 << 17, Primavera = 1 << 18, CincoDeMayo = 1 << 19,
        DiaDelPadre = 1 << 20, Revolucion = 1 << 21, SantaCecilia = 1 << 22, Posadas = 1 << 23, MesPatrio = 1 << 24
    }
    public static class CalendarioCielo
    {
        public static Fiesta De(DateTime t)
        {
            int m = t.Month, d = t.Day; double h = t.Hour + t.Minute / 60.0; Fiesta f = Fiesta.Ninguna;
            if ((m == 12 && d == 31 && h >= 23.9) || (m == 1 && d == 1 && h < 0.75)) f |= Fiesta.FinDeAnio;   // la cuenta y los fuegos
            if ((m == 12 && d == 31) || (m == 1 && d == 1)) f |= Fiesta.AnioNuevo;
            if (m == 1 && (d == 5 || d == 6)) f |= Fiesta.Reyes;
            if (m == 2 && (d == 13 || d == 14)) f |= Fiesta.SanValentin;
            if ((m == 3 && d >= 15) || (m == 4 && d <= 15)) f |= Fiesta.Jacarandas;
            if (m == 4 && d == 30) f |= Fiesta.DiaDelNino;
            if (m == 5 && d == 10) f |= Fiesta.Madres;
            if (m == 9 && d >= 13 && d <= 16) f |= Fiesta.Patrias;
            else if (m == 9) f |= Fiesta.MesPatrio;                                     // el resto de septiembre, mas sencillo
            if (m == 2 && d == 2) f |= Fiesta.Candelaria;
            if ((m == 2 && d == 24) || (m == 9 && (d == 15 || d == 16)) || (m == 11 && d == 20)) f |= Fiesta.Bandera;
            if (m == 3 && d == 8) f |= Fiesta.Mujer;
            if (m == 3 && d == 17) f |= Fiesta.SanPatricio;
            if (m == 3 && d >= 19 && d <= 23) f |= Fiesta.Primavera;
            if (m == 5 && d == 5) f |= Fiesta.CincoDeMayo;
            if (m == 6 && t.DayOfWeek == DayOfWeek.Sunday && d >= 15 && d <= 21) f |= Fiesta.DiaDelPadre;   // el tercer domingo de junio
            if (m == 11 && d == 20) f |= Fiesta.Revolucion;
            if (m == 11 && d == 22) f |= Fiesta.SantaCecilia;                                             // la de los musicos (y el mariachi)
            if (m == 12 && d >= 16 && d <= 24) f |= Fiesta.Posadas;
            if ((m == 9 && d == 15 && h >= 22.5) || (m == 9 && d == 16 && h < 0.75)) f |= Fiesta.Grito;
            if (m == 10 && d == 31) f |= Fiesta.Halloween;
            if ((m == 10 && d >= 28) || (m == 11 && d <= 2)) f |= Fiesta.Muertos;
            if (m == 12 && d == 12) f |= Fiesta.Guadalupe;
            if ((m == 12 && d >= 13 && d <= 30) || (m == 1 && d <= 4)) f |= Fiesta.Navidad;
            if ((m == 12 && d == 24 && h >= 18) || (m == 12 && d == 25)) f |= Fiesta.Nochebuena;
            return f;
        }
        // Las lluvias de estrellas del ano: mes, dia del maximo (un dia antes y uno despues tambien), nombre y de donde salen en la pantalla.
        private static readonly int[] LluviaMes = { 1, 4, 5, 8, 10, 10, 11, 12, 12 }, LluviaDia = { 3, 22, 6, 12, 8, 21, 17, 14, 22 };
        private static readonly string[] LluviaNombre = { "Cuadrántidas", "Líridas", "Eta Acuáridas", "Perseidas", "Dracónidas", "Oriónidas", "Leónidas", "Gemínidas", "Úrsidas" };
        private static readonly float[] LluviaX = { 0.78f, 0.62f, 0.18f, 0.86f, 0.82f, 0.24f, 0.14f, 0.30f, 0.92f }, LluviaY = { 0.04f, 0.10f, 0.34f, 0.04f, 0.0f, 0.16f, 0.10f, 0.06f, 0.0f };
        // La de esta noche (la noche cuenta hasta el amanecer: de madrugada vale la del dia de antes), o null.
        public static string LluviaDeEstrellas(DateTime t, out float rx, out float ry)
        {
            DateTime noche = t.Hour < 12 ? t.AddDays(-1) : t;
            for (int i = 0; i < LluviaMes.Length; i++)
            {
                DateTime pico = new DateTime(noche.Year, LluviaMes[i], LluviaDia[i]);
                if (Math.Abs((noche.Date - pico).TotalDays) <= 1) { rx = LluviaX[i]; ry = LluviaY[i]; return LluviaNombre[i]; }
            }
            rx = ry = 0; return null;
        }
    }
}
