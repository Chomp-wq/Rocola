// EL CIELO VIVO DE ROCOLA OS, NUCLEO: EL CLIMA DE AFUERA, EL LUGAR POR INTERNET Y rocola_fechas.txt (PLAN_CIELO_VIVO.md §2 y §3). Todo lo
// que espera (red y disco) va en UN hilo de fondo (RocolaOs.Hilo «cielo», nice 19), nunca en el de la pantalla: el hilo pone lo que sabe en
// tres referencias que se cambian enteras (Guardado, Lugar, Fechas) y el latido las lee para la foto del cielo.
//   - EL CLIMA (CL1, la clave «clima»): Open-Meteo (gratis y sin llave) cada 15 minutos; al cambiar de ciudad, al momento. El ultimo se guarda
//     en rocola_clima.txt y vale 3 horas: sin internet, a las 3 h el cielo vuelve a lo normal (despejado).
//   - EL LUGAR (con ciudad_bar=0): ipapi.co, ipwho.is o get.geojs.io, una vez por semana (rocola_clima_lugar.txt); sin internet, el ultimo; sin
//     ninguno, la Ciudad de Mexico.
//   - rocola_fechas.txt: se vuelve a leer cuando cambia (RocolaOs.CieloFiestas.cs).
// Para las pruebas: ROCOLA_CLIMA (un tipo, o «tipo,nubes,lluvia,viento,direccion,temperatura»: sin red, no caduca), ROCOLA_CLIMA_API y
// ROCOLA_LUGAR_API (servidores falsos), ROCOLA_CIUDAD («lat,lon»: el lugar sin red) y ROCOLA_CLIMA_YA (sin la espera de 20 s del principio).
// Nunca llaves: ni Open-Meteo ni los del lugar las piden. Solo los escribe el programa.
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    internal enum RocolaOsClimaTipo { SinSaber = 0, Despejado, PocoNublado, Nublado, Neblina, Llovizna, Lluvia, Aguacero, Tormenta, Nieve }

    // UN CLIMA (inmutable)
    internal sealed class RocolaOsClima
    {
        internal readonly RocolaOsClimaTipo Tipo;
        internal readonly int Codigo;            // el de la OMM que da Open-Meteo (-1: forzado o sin saber)
        internal readonly float Nubes;           // 0 a 1
        internal readonly float Lluvia;          // mm/h (lluvia, chubascos y nieve)
        internal readonly float Viento;          // km/h
        internal readonly float Direccion;       // grados, de donde viene (0 norte, 90 este)
        internal readonly float Temperatura;     // grados C (NaN: no se sabe)
        internal readonly float Visibilidad;     // metros (NaN: no se sabe)
        internal readonly bool DeDia;            // lo que dice Open-Meteo (is_day)
        internal readonly DateTime Medida;       // cuando se midio (UTC)
        internal readonly bool Forzado;          // ROCOLA_CLIMA o «Ver como se veria»: no caduca
        internal const double ValeHoras = 3;
        internal static readonly RocolaOsClima Despejado0 = new RocolaOsClima(RocolaOsClimaTipo.Despejado, -1, 0f, 0f, 0f, 0f, float.NaN, float.NaN, true, DateTime.MinValue, false);
        internal RocolaOsClima(RocolaOsClimaTipo tipo, int codigo, float nubes, float lluvia, float viento, float direccion, float temperatura, float visibilidad,
                               bool deDia, DateTime medida, bool forzado)
        {
            Tipo = tipo; Codigo = codigo; Nubes = Math.Max(0f, Math.Min(1f, nubes)); Lluvia = Math.Max(0f, lluvia); Viento = Math.Max(0f, viento);
            Direccion = direccion; Temperatura = temperatura; Visibilidad = visibilidad; DeDia = deDia; Medida = medida; Forzado = forzado;
        }
        // si todavia vale (3 h desde la medida; el forzado siempre)
        internal bool Vigente(DateTime utcAhora) { return Forzado || (Tipo != RocolaOsClimaTipo.SinSaber && utcAhora >= Medida.AddMinutes(-10) && (utcAhora - Medida).TotalHours <= ValeHoras); }
        internal string Nombre { get { return NombreDe(Tipo); } }
        internal static string NombreDe(RocolaOsClimaTipo t)
        {
            switch (t)
            {
                case RocolaOsClimaTipo.Despejado: return "despejado"; case RocolaOsClimaTipo.PocoNublado: return "poco nublado"; case RocolaOsClimaTipo.Nublado: return "nublado";
                case RocolaOsClimaTipo.Neblina: return "neblina"; case RocolaOsClimaTipo.Llovizna: return "llovizna"; case RocolaOsClimaTipo.Lluvia: return "lluvia";
                case RocolaOsClimaTipo.Aguacero: return "aguacero"; case RocolaOsClimaTipo.Tormenta: return "tormenta"; case RocolaOsClimaTipo.Nieve: return "nieve";
                default: return "sin saber";
            }
        }
        // LOS CODIGOS DE LA OMM que da Open-Meteo (como el programa anterior, Motor/CieloClima.cs DeCodigo, con «aguacero» por «lluvia fuerte»)
        internal static RocolaOsClimaTipo DeCodigo(int c, float nubes)
        {
            if (c >= 95) return RocolaOsClimaTipo.Tormenta;
            if (c == 71 || c == 73 || c == 75 || c == 77 || c == 85 || c == 86) return RocolaOsClimaTipo.Nieve;
            if (c == 65 || c == 82 || c == 67) return RocolaOsClimaTipo.Aguacero;
            if (c == 61 || c == 63 || c == 66 || c == 80 || c == 81) return RocolaOsClimaTipo.Lluvia;
            if (c >= 51 && c <= 57) return RocolaOsClimaTipo.Llovizna;
            if (c == 45 || c == 48) return RocolaOsClimaTipo.Neblina;
            if (c == 3 || nubes >= 0.75f) return RocolaOsClimaTipo.Nublado;
            if (c == 1 || c == 2 || nubes >= 0.3f) return RocolaOsClimaTipo.PocoNublado;
            return RocolaOsClimaTipo.Despejado;
        }
        // UN TIPO POR SU NOMBRE (ROCOLA_CLIMA, «Ver como se veria», el buzon): despejado, poco, nublado, neblina, llovizna, lluvia, aguacero (o
        // fuerte), tormenta, nieve. SinSaber si no es ninguno.
        internal static RocolaOsClimaTipo TipoDe(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant().Replace(' ', '_'))
            {
                case "despejado": return RocolaOsClimaTipo.Despejado;
                case "poco": case "poco_nublado": case "pocas_nubes": return RocolaOsClimaTipo.PocoNublado;
                case "nublado": return RocolaOsClimaTipo.Nublado;
                case "neblina": case "niebla": return RocolaOsClimaTipo.Neblina;
                case "llovizna": return RocolaOsClimaTipo.Llovizna;
                case "lluvia": return RocolaOsClimaTipo.Lluvia;
                case "aguacero": case "fuerte": return RocolaOsClimaTipo.Aguacero;
                case "tormenta": return RocolaOsClimaTipo.Tormenta;
                case "nieve": return RocolaOsClimaTipo.Nieve;
                default: return RocolaOsClimaTipo.SinSaber;
            }
        }
        // UN CLIMA FORZADO: «tipo» o «tipo,nubes(%),lluvia(mm/h),viento(km/h),direccion(grados),temperatura(C)»; null si no vale
        internal static RocolaOsClima Forzar(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            string[] p = s.Split(',');
            RocolaOsClimaTipo t = TipoDe(p[0]);
            if (t == RocolaOsClimaTipo.SinSaber) return null;
            float nubes = NubesDe(t), lluvia = LluviaDe(t), viento = 12f, dir = 270f, temp = 18f;
            float v;
            if (p.Length > 1 && Num(p[1], out v)) nubes = v / 100f;
            if (p.Length > 2 && Num(p[2], out v)) lluvia = v;
            if (p.Length > 3 && Num(p[3], out v)) viento = v;
            if (p.Length > 4 && Num(p[4], out v)) dir = v;
            if (p.Length > 5 && Num(p[5], out v)) temp = v;
            return new RocolaOsClima(t, -1, nubes, lluvia, viento, dir, temp, t == RocolaOsClimaTipo.Neblina ? 800f : float.NaN, true, DateTime.UtcNow, true);
        }
        private static bool Num(string s, out float v) { return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !float.IsNaN(v) && !float.IsInfinity(v); }
        private static float NubesDe(RocolaOsClimaTipo t)
        {
            switch (t)
            {
                case RocolaOsClimaTipo.Despejado: return 0.05f; case RocolaOsClimaTipo.PocoNublado: return 0.4f; case RocolaOsClimaTipo.Neblina: return 0.6f;
                case RocolaOsClimaTipo.Llovizna: return 0.85f; case RocolaOsClimaTipo.Nieve: return 0.9f; case RocolaOsClimaTipo.Nublado: case RocolaOsClimaTipo.Lluvia: return 0.95f;
                default: return 1f;
            }
        }
        private static float LluviaDe(RocolaOsClimaTipo t)
        {
            switch (t)
            {
                case RocolaOsClimaTipo.Llovizna: return 0.3f; case RocolaOsClimaTipo.Lluvia: return 2f; case RocolaOsClimaTipo.Aguacero: return 8f;
                case RocolaOsClimaTipo.Tormenta: return 10f; case RocolaOsClimaTipo.Nieve: return 1f; default: return 0f;
            }
        }

        // ---------- rocola_clima.txt: «clave=valor» por renglon ----------
        internal string Texto(double lat, double lon)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# el ultimo clima de afuera (Open-Meteo); lo escribe rocola-xp y vale 3 horas\n");
            sb.Append("medida=").Append(Medida.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("codigo=").Append(Codigo.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("nubes=").Append(((int)Math.Round(Nubes * 100)).ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("lluvia=").Append(F(Lluvia)).Append('\n');
            sb.Append("viento=").Append(F(Viento)).Append('\n');
            sb.Append("direccion=").Append(F(Direccion)).Append('\n');
            sb.Append("temperatura=").Append(F(Temperatura)).Append('\n');
            sb.Append("visibilidad=").Append(F(Visibilidad)).Append('\n');
            sb.Append("de_dia=").Append(DeDia ? "1" : "0").Append('\n');
            sb.Append("lat=").Append(lat.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("lon=").Append(lon.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }
        private static string F(float v) { return float.IsNaN(v) ? "NaN" : v.ToString("0.##", CultureInfo.InvariantCulture); }
        // el texto de rocola_clima.txt (lo de antes de ahora); null si no vale
        internal static RocolaOsClima DeTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            DateTime medida = DateTime.MinValue; int codigo = -1; float nubes = float.NaN, lluvia = 0, viento = 0, dir = 0, temp = float.NaN, vis = float.NaN; bool dia = true;
            foreach (string r in texto.Split('\n'))
            {
                string l = r.Trim(); int i = l.IndexOf('=');
                if (l.Length == 0 || l[0] == '#' || i <= 0) continue;
                string k = l.Substring(0, i), v = l.Substring(i + 1).Trim(); float f;
                switch (k)
                {
                    case "medida": if (!DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out medida)) medida = DateTime.MinValue; break;
                    case "codigo": { int c; codigo = int.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out c) ? c : -1; } break;   // (si no se lee, el archivo no vale: nada de «0 = despejado»)
                    case "nubes": if (Num(v, out f)) nubes = f / 100f; break;
                    case "lluvia": if (Num(v, out f)) lluvia = f; break;
                    case "viento": if (Num(v, out f)) viento = f; break;
                    case "direccion": if (Num(v, out f)) dir = f; break;
                    case "temperatura": if (Num(v, out f)) temp = f; break;
                    case "visibilidad": if (Num(v, out f)) vis = f; break;
                    case "de_dia": dia = v != "0"; break;
                }
            }
            if (medida == DateTime.MinValue || codigo < 0 || float.IsNaN(nubes)) return null;
            return new RocolaOsClima(DeCodigo(codigo, nubes), codigo, nubes, lluvia, viento, dir, temp, vis, dia, DateTime.SpecifyKind(medida, DateTimeKind.Utc), false);
        }
        // LA RESPUESTA DE OPEN-METEO (/v1/forecast?...&current=...); null si no trae lo necesario
        internal static RocolaOsClima DeOpenMeteo(JsonElement raiz, DateTime utcAhora)
        {
            JsonElement cur;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("current", out cur) || cur.ValueKind != JsonValueKind.Object) return null;
            double codigo = Leer(cur, "weather_code", double.NaN);
            if (double.IsNaN(codigo)) return null;
            float nubes = (float)(Leer(cur, "cloud_cover", 0) / 100.0);
            double lluvia = Math.Max(Leer(cur, "precipitation", 0), Leer(cur, "rain", 0) + Leer(cur, "showers", 0)) + Leer(cur, "snowfall", 0);
            return new RocolaOsClima(DeCodigo((int)codigo, nubes), (int)codigo, nubes, (float)lluvia, (float)Leer(cur, "wind_speed_10m", 0), (float)Leer(cur, "wind_direction_10m", 0),
                (float)Leer(cur, "temperature_2m", double.NaN), (float)Leer(cur, "visibility", double.NaN), Leer(cur, "is_day", 1) != 0, utcAhora, false);
        }
        internal static double Leer(JsonElement e, string nombre, double siNo)
        {
            JsonElement v; double d;
            if (!e.TryGetProperty(nombre, out v)) return siNo;
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;   // (get.geojs.io: entre comillas)
            return siNo;
        }
    }

    // EL LUGAR POR INTERNET (inmutable)
    internal sealed class RocolaOsLugar
    {
        internal readonly double Lat, Lon; internal readonly string Ciudad; internal readonly DateTime Cuando;   // (UTC)
        internal RocolaOsLugar(double lat, double lon, string ciudad, DateTime cuando) { Lat = lat; Lon = lon; Ciudad = ciudad ?? ""; Cuando = cuando; }
        internal bool Viejo(DateTime utcAhora) { return (utcAhora - Cuando).TotalDays >= 7; }
        internal string Texto()
        {
            return "# el lugar del bar por internet (una vez por semana); lo escribe rocola-xp\nlat=" + Lat.ToString("0.0000", CultureInfo.InvariantCulture)
                + "\nlon=" + Lon.ToString("0.0000", CultureInfo.InvariantCulture) + "\nciudad=" + Ciudad.Replace('\n', ' ').Replace('\r', ' ')
                + "\nfecha=" + Cuando.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\n";
        }
        internal static RocolaOsLugar DeTexto(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            double lat = double.NaN, lon = double.NaN; string ciudad = ""; DateTime cuando = DateTime.MinValue;
            foreach (string r in texto.Split('\n'))
            {
                string l = r.Trim(); int i = l.IndexOf('=');
                if (l.Length == 0 || l[0] == '#' || i <= 0) continue;
                string k = l.Substring(0, i), v = l.Substring(i + 1).Trim();
                double d;   // (TryParse que falla deja 0 en el out: un lat=0 valido no sale de ahi; si no se lee, NaN y el archivo no vale)
                if (k == "lat") lat = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.NaN;
                else if (k == "lon") lon = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : double.NaN;
                else if (k == "ciudad") ciudad = v;
                else if (k == "fecha") DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out cuando);
            }
            if (!Vale(lat, lon)) return null;
            return new RocolaOsLugar(lat, lon, ciudad, DateTime.SpecifyKind(cuando, DateTimeKind.Utc));
        }
        internal static bool Vale(double lat, double lon) { return !double.IsNaN(lat) && !double.IsNaN(lon) && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180 && !(lat == 0 && lon == 0); }
        // la respuesta de ipapi.co, ipwho.is o get.geojs.io (latitude/longitude, como numero o texto; city)
        internal static RocolaOsLugar DeJson(JsonElement r, DateTime utcAhora)
        {
            if (r.ValueKind != JsonValueKind.Object) return null;
            double la = RocolaOsClima.Leer(r, "latitude", double.NaN), lo = RocolaOsClima.Leer(r, "longitude", double.NaN);
            if (!Vale(la, lo)) return null;
            JsonElement c; string ciudad = r.TryGetProperty("city", out c) && c.ValueKind == JsonValueKind.String ? c.GetString() : "";
            return new RocolaOsLugar(la, lo, ciudad, utcAhora);
        }
    }

    // EL HILO DEL CIELO
    internal static partial class RocolaOsCielo
    {
        internal const string ArchivoClima = "rocola_clima.txt", ArchivoLugar = "rocola_clima_lugar.txt", ArchivoFechas = "rocola_fechas.txt";
        internal const int ClimaCadaMin = 15;
        private static RocolaOsClima climaGuardado;          // el ultimo de Open-Meteo (o el de rocola_clima.txt): lo lee el latido
        private static RocolaOsClima climaForzado;           // ROCOLA_CLIMA
        private static RocolaOsLugar lugar;                  // el lugar por internet
        private static RocolaOsFechasArchivo fechas = RocolaOsFechasArchivo.Vacio;
        private static volatile string climaFallo = "";      // lo ultimo que fallo al pedir el clima (sin secretos: no hay)
        private static long climaPedidos, climaBien, lugarPedidos;
        private static volatile bool paraHilo;
        private static Thread hilo;
        internal static RocolaOsClima ClimaGuardado { get { return Volatile.Read(ref climaGuardado); } }
        internal static RocolaOsLugar Lugar { get { return Volatile.Read(ref lugar); } }
        internal static RocolaOsFechasArchivo Fechas { get { return Volatile.Read(ref fechas); } }
        internal static long ClimaPedidos { get { return Interlocked.Read(ref climaPedidos); } }
        internal static long LugarPedidos { get { return Interlocked.Read(ref lugarPedidos); } }

        // ROCOLA_CIUDAD=«lat,lon» (pruebas): el lugar sin red; null sin la variable o si no vale
        private static bool ciudadLeida; private static RocolaOsLugar ciudadForzada;
        internal static RocolaOsLugar CiudadForzada
        {
            get
            {
                if (ciudadLeida) return ciudadForzada;
                string v = Environment.GetEnvironmentVariable("ROCOLA_CIUDAD");
                if (!string.IsNullOrEmpty(v))
                {
                    string[] p = v.Split(','); double la, lo;
                    if (p.Length >= 2 && double.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out la)
                        && double.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lo) && RocolaOsLugar.Vale(la, lo))
                        ciudadForzada = new RocolaOsLugar(la, lo, p.Length >= 3 ? p[2].Trim() : "ROCOLA_CIUDAD", DateTime.UtcNow);
                }
                ciudadLeida = true;
                return ciudadForzada;
            }
        }

        // ARRANCA EL HILO (desde Arranca, en la pantalla): lee lo guardado y se pone a pedir. Una vez.
        private static void ArrancaHilo()
        {
            if (hilo != null) return;
            climaForzado = RocolaOsClima.Forzar(Environment.GetEnvironmentVariable("ROCOLA_CLIMA"));
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROCOLA_CLIMA")) && climaForzado == null)
                RocolaOs.Apunta("cielo", "ROCOLA_CLIMA no vale («" + Environment.GetEnvironmentVariable("ROCOLA_CLIMA") + "»): se usa el clima de afuera");
            hilo = RocolaOs.Hilo("cielo", Bucle);
        }
        internal static void ParaHilo() { paraHilo = true; }

        private static void Bucle()
        {
            LeeGuardados();
            int espera = Environment.GetEnvironmentVariable("ROCOLA_CLIMA_YA") != null ? 0 : 20;   // que arranque primero lo demas
            for (int s = 0; s < espera * 10 && !paraHilo; s++) { Thread.Sleep(100); RevisaFechas(); }
            DateTime proximoClima = DateTime.MinValue, ultimoLugar = DateTime.MinValue;
            double laAntes = double.NaN, loAntes = double.NaN;
            while (!paraHilo)
            {
                try
                {
                    RevisaFechas();
                    DateTime ahora = DateTime.UtcNow;
                    bool cielo = FuncionVa("cielo"), clima = FuncionVa("clima");
                    // el lugar: solo si alguna idea lo usa (el sol y la luna de DN1-DN6, el filo de luz VI5, el clima), con ciudad_bar=0 y sin
                    // ROCOLA_CIUDAD; una vez por semana, como mucho un intento por hora
                    if ((clima || (cielo && UsaElSol())) && CiudadElegida == 0 && CiudadForzada == null && (Lugar == null || Lugar.Viejo(ahora)) && (ahora - ultimoLugar).TotalHours >= 1)
                    {
                        ultimoLugar = ahora;
                        BuscaLugar();
                    }
                    double la, lo; string n; LugarDelBar(out la, out lo, out n);
                    bool movido = !double.IsNaN(laAntes) && (Math.Abs(la - laAntes) > 0.01 || Math.Abs(lo - loAntes) > 0.01);
                    if (clima && climaForzado == null && LugarSabido && (ahora >= proximoClima || movido))   // (sin saber el lugar no se pide el clima de la Ciudad de Mexico)
                    {
                        bool ok = PideClima(la, lo);
                        proximoClima = DateTime.UtcNow.AddMinutes(ok ? ClimaCadaMin : (ClimaGuardado == null ? 2 : 5));
                    }
                    laAntes = la; loAntes = lo;
                }
                catch (Exception ex) { RocolaOs.Falla("cielo", "hilo del clima", ex); }
                for (int s = 0; s < 100 && !paraHilo; s++) { Thread.Sleep(100); if (s % 20 == 19) RevisaFechas(); }
            }
        }
        // si una funcion del cielo va (desde el hilo: Encendida no lee nada despues de la primera vez)
        private static bool FuncionVa(string funcion) { return RocolaOs.Encendida(funcion); }
        // si alguna idea encendida necesita saber donde esta el bar por el sol o la luna: DN1-DN6 (la fase de la luna de DN3 A no; su camino, DN3 B,
        // si) y VI5 (el filo de luz sigue al cielo)
        private static bool UsaElSol()
        {
            if (FuncionVa(RocolaOsCielo.Funcion))
                foreach (string c in new[] { "cielo_sol", "sol", "luna_camino", "estrellas_anochecer", "horizonte_hora", "nubes" })
                    if (RocolaOs.AjusteSiNo(c)) return true;
            return FuncionVa(RocolaOsCielo.FuncionVidrio) && RocolaOs.AjusteSiNo("vidrio_luz");
        }

        private static void LeeGuardados()
        {
            try
            {
                string[] l = Form1.RocolaOsLeeLineas(RocolaOs.Datos(ArchivoLugar));
                RocolaOsLugar g = l != null ? RocolaOsLugar.DeTexto(string.Join("\n", l)) : null;
                if (g != null) { Volatile.Write(ref lugar, g); RocolaOs.Apunta("cielo", "el lugar guardado: " + Describe(g)); }
            }
            catch (Exception ex) { RocolaOs.Falla("cielo", "leer " + ArchivoLugar, ex); }
            try
            {
                string[] l = Form1.RocolaOsLeeLineas(RocolaOs.Datos(ArchivoClima));
                RocolaOsClima c = l != null ? RocolaOsClima.DeTexto(string.Join("\n", l)) : null;
                if (c != null)
                {
                    bool vale = c.Vigente(DateTime.UtcNow);
                    if (vale) Volatile.Write(ref climaGuardado, c);
                    RocolaOs.Apunta("cielo", "el clima guardado: " + c.Nombre + " de " + c.Medida.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC ("
                        + (vale ? "vale" : "ya es viejo: no se usa") + ")");
                }
            }
            catch (Exception ex) { RocolaOs.Falla("cielo", "leer " + ArchivoClima, ex); }
        }
        private static string Describe(RocolaOsLugar g) { return (g.Ciudad.Length > 0 ? g.Ciudad + " " : "") + "(" + g.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + g.Lon.ToString("0.00", CultureInfo.InvariantCulture) + ")"; }

        // rocola_fechas.txt: se vuelve a leer si cambio (su fecha o su largo)
        private static DateTime fechasHora = DateTime.MinValue; private static long fechasLargo = -2;
        private static void RevisaFechas()
        {
            try
            {
                string ruta = RocolaOs.Datos(ArchivoFechas);
                FileInfo fi = new FileInfo(ruta);
                DateTime h = fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue; long n = fi.Exists ? fi.Length : -1;
                if (h == fechasHora && n == fechasLargo) return;
                fechasHora = h; fechasLargo = n;
                RocolaOsFechasArchivo f = fi.Exists ? RocolaOsFechasArchivo.Lee(File.ReadAllText(ruta, Encoding.UTF8)) : RocolaOsFechasArchivo.Vacio;
                Volatile.Write(ref fechas, f);
                if (fi.Exists)
                    RocolaOs.Apunta("cielo", ArchivoFechas + ": " + f.Renglones.Length + " fechas" + (f.Malos.Length > 0 ? "; no valen: " + string.Join(" | ", f.Malos) : ""));
                OtraFoto();
            }
            catch (Exception ex) { RocolaOs.Falla("cielo", "leer " + ArchivoFechas, ex); }
        }

        // ---------- la red ----------
        private static HttpClient http, httpLocal;
        private static HttpClient Http(string url)
        {
            bool local = url.StartsWith("http://127.0.0.1", StringComparison.Ordinal) || url.StartsWith("http://localhost", StringComparison.Ordinal);
            HttpClient c = local ? httpLocal : http;
            if (c != null) return c;
            SocketsHttpHandler h = new SocketsHttpHandler { UseProxy = !local, PooledConnectionLifetime = TimeSpan.FromMinutes(5), ConnectTimeout = TimeSpan.FromSeconds(8), AutomaticDecompression = DecompressionMethods.All };
            c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 256 * 1024 };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("rocola-xp/1");
            if (local) httpLocal = c; else http = c;
            return c;
        }
        // UN GET: el JSON o null (y por que)
        private static JsonDocument Pide(string url, out string error)
        {
            error = null;
            try
            {
                using (HttpRequestMessage q = new HttpRequestMessage(HttpMethod.Get, url))
                using (HttpResponseMessage r = Http(url).Send(q))
                {
                    if ((int)r.StatusCode != 200) { error = "contesto " + ((int)r.StatusCode).ToString(CultureInfo.InvariantCulture); return null; }
                    using (Stream s = r.Content.ReadAsStream()) return JsonDocument.Parse(s);
                }
            }
            catch (Exception ex) { error = "sin red o respuesta rara (" + ex.GetType().Name + ")"; return null; }
        }
        private static void BuscaLugar()
        {
            string propia = Environment.GetEnvironmentVariable("ROCOLA_LUGAR_API");
            string[] apis = !string.IsNullOrEmpty(propia) ? new[] { propia } : new[] { "https://ipapi.co/json/", "https://ipwho.is/", "https://get.geojs.io/v1/ip/geo.json" };
            string errores = "";
            foreach (string api in apis)
            {
                Interlocked.Increment(ref lugarPedidos);
                string e;
                using (JsonDocument d = Pide(api, out e))
                {
                    RocolaOsLugar g = d != null ? RocolaOsLugar.DeJson(d.RootElement, DateTime.UtcNow) : null;
                    if (g == null) { errores += (errores.Length > 0 ? "; " : "") + Host(api) + ": " + (e ?? "sin lugar en la respuesta"); continue; }
                    Volatile.Write(ref lugar, g);
                    try { Form1.RocolaOsEscribeSeguro(RocolaOs.Datos(ArchivoLugar), g.Texto()); } catch (Exception ex) { RocolaOs.Falla("cielo", "guardar " + ArchivoLugar, ex); }
                    RocolaOs.Apunta("cielo", "el lugar por internet (" + Host(api) + "): " + Describe(g));
                    OtraFoto();
                    return;
                }
            }
            RocolaOs.Apunta("cielo", "no se supo el lugar por internet (" + errores + "); vale " + (Lugar != null ? "el ultimo, " + Describe(Lugar) : "la Ciudad de Mexico"));
        }
        private static string Host(string url) { try { return new Uri(url).Host; } catch { return url; } }
        private static bool PideClima(double la, double lo)
        {
            string b = Environment.GetEnvironmentVariable("ROCOLA_CLIMA_API");
            string baseUrl = string.IsNullOrEmpty(b) ? "https://api.open-meteo.com" : b.TrimEnd('/');
            string url = baseUrl + "/v1/forecast?latitude=" + la.ToString("0.00", CultureInfo.InvariantCulture) + "&longitude=" + lo.ToString("0.00", CultureInfo.InvariantCulture)
                + "&current=weather_code,cloud_cover,precipitation,rain,showers,snowfall,wind_speed_10m,wind_direction_10m,temperature_2m,visibility,is_day";
            Interlocked.Increment(ref climaPedidos);
            string e;
            RocolaOsClima c;
            using (JsonDocument d = Pide(url, out e)) c = d != null ? RocolaOsClima.DeOpenMeteo(d.RootElement, DateTime.UtcNow) : null;
            if (c == null)
            {
                string f = e ?? "respuesta sin clima";
                if (f != climaFallo) RocolaOs.Apunta("cielo", "no se pudo pedir el clima: " + f + (ClimaGuardado != null ? " (vale el ultimo hasta las 3 h)" : ""));
                climaFallo = f;
                return false;
            }
            climaFallo = "";
            Interlocked.Increment(ref climaBien);
            RocolaOsClima antes = ClimaGuardado;
            Volatile.Write(ref climaGuardado, c);
            try { Form1.RocolaOsEscribeSeguro(RocolaOs.Datos(ArchivoClima), c.Texto(la, lo)); } catch (Exception ex) { RocolaOs.Falla("cielo", "guardar " + ArchivoClima, ex); }
            if (antes == null || antes.Tipo != c.Tipo)
                RocolaOs.Apunta("cielo", "el clima de afuera: " + c.Nombre + " (codigo " + c.Codigo + ", nubes " + (int)Math.Round(c.Nubes * 100) + " %, "
                    + c.Temperatura.ToString("0", CultureInfo.InvariantCulture) + " C)");
            OtraFoto();
            return true;
        }
    }
}
