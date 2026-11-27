// EL CIELO VIVO DE ROCOLA OS: LA OBSERVACION REAL DEL AEROPUERTO MAS CERCANO (METAR) Y LAS COORDENADAS EXACTAS DEL BAR (FASE11_cielo-clima.md §9).
// Sin dibujo: traduce un METAR de aviationweather.gov (gratis, sin llave) al mismo «estado» (RocolaOsClima) que da Open-Meteo, para que el cielo
// no cambie de interfaz. Open-Meteo es un PRONOSTICO de modelo (rejillas de 9 a 25 km); un METAR es lo que un observador o un sensor midio en el
// aeropuerto hace unos minutos. REGLA: con un METAR de menos de 90 min de una estacion a 40 km o menos, MANDA para lo de ahora (nubes, lluvia,
// tormenta, niebla, temperatura, viento); Open-Meteo rellena lo que el METAR no trae (direccion con viento variable, visibilidad, el viento si falta)
// y es el clima cuando no hay estacion cerca o el METAR es viejo.
// A internet solo van coordenadas (un cuadro de unos 100 km alrededor del bar para buscar la estacion) o el id de la estacion: nada mas.
// Para las pruebas: ROCOLA_METAR_API (servidor falso; sin ella, con ROCOLA_CLIMA_API puesta NO se pide ningun METAR: las pruebas de antes no tocan la red).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    // LO QUE DICE UN METAR, ya leido (lo que no trae queda como null)
    internal sealed class RocolaOsMetarObs
    {
        internal string Id = ""; internal double Lat, Lon; internal DateTime Cuando;   // (UTC)
        internal int Codigo = 0;           // el de la OMM (0, 1, 2, 3, 45, 51-57, 61-67, 71-77, 80-82, 85-86, 95)
        internal float Nubes;              // 0 a 1
        internal float Lluvia;             // mm/h (lluvia, chubascos y nieve)
        internal double? VientoKmh, Direccion, Temp, VisibilidadM;
        internal string Tiempo = "";       // el wxString tal cual (para la bitacora)
        internal int EdadMin(DateTime utcAhora) { return (int)Math.Round((utcAhora - Cuando).TotalMinutes); }
        internal bool Fresco(DateTime utcAhora) { double m = (utcAhora - Cuando).TotalMinutes; return m >= -10 && m <= RocolaOsMetar.FrescoMin; }

        // EL CLIMA que sale de el: lo que falta se toma de Open-Meteo (om, si aun vale y es de hace 90 min o menos); si tampoco, valores neutros.
        internal RocolaOsClima AClima(RocolaOsClima om, DateTime utcAhora, float km)
        {
            bool hayOm = om != null && !om.Forzado && om.Vigente(utcAhora) && (utcAhora - om.Medida).TotalMinutes <= RocolaOsMetar.FrescoMin;
            float viento = VientoKmh.HasValue ? (float)VientoKmh.Value : hayOm ? om.Viento : 0f;
            float dir = Direccion.HasValue ? (float)Direccion.Value : hayOm ? om.Direccion : 270f;
            float temp = Temp.HasValue ? (float)Temp.Value : hayOm ? om.Temperatura : float.NaN;
            float vis = VisibilidadM.HasValue ? (float)VisibilidadM.Value : hayOm ? om.Visibilidad : float.NaN;
            double alt, az; RocolaOsAstro.Sol(Cuando, Lat, Lon, out alt, out az);
            return new RocolaOsClima(RocolaOsClima.DeCodigo(Codigo, Nubes), Codigo, Nubes, Lluvia, viento, dir, temp, vis, alt > -0.83, Cuando, false, "metar", Id, km);
        }
    }

    internal static class RocolaOsMetar
    {
        internal const double FrescoMin = 90;       // un METAR de mas de 90 min ya no manda
        internal const double CercaKm = 40;         // la estacion tiene que estar a 40 km o menos del bar
        internal const double CuadroGrados = 0.45;  // el cuadro de busqueda: +-0,45 grados de latitud (50 km) y lo mismo en distancia de longitud

        // LA DISTANCIA en km entre dos puntos (haversine)
        internal static double Km(double la1, double lo1, double la2, double lo2)
        {
            const double G = Math.PI / 180, R = 6371.0;
            double dla = (la2 - la1) * G, dlo = (lo2 - lo1) * G;
            double a = Math.Sin(dla / 2) * Math.Sin(dla / 2) + Math.Cos(la1 * G) * Math.Cos(la2 * G) * Math.Sin(dlo / 2) * Math.Sin(dlo / 2);
            return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }
        // EL CUADRO «lat0,lon0,lat1,lon1» alrededor del bar (el orden que pide la API, comprobado con la de verdad)
        internal static string Cuadro(double la, double lo)
        {
            double dla = CuadroGrados, dlo = CuadroGrados / Math.Max(0.2, Math.Cos(la * Math.PI / 180));
            Func<double, string> f = v => v.ToString("0.####", CultureInfo.InvariantCulture);
            return f(Math.Max(-90, la - dla)) + "," + f(Math.Max(-180, lo - dlo)) + "," + f(Math.Min(90, la + dla)) + "," + f(Math.Min(180, lo + dlo));
        }

        // LA NUBOSIDAD de una cobertura de capa (octavos: FEW 1-2, SCT 3-4, BKN 5-7, OVC 8; VV cielo cubierto por niebla); -1 si no es una cobertura
        internal static float Cobertura(string c)
        {
            switch ((c ?? "").Trim().ToUpperInvariant())
            {
                case "FEW": return 0.19f; case "SCT": return 0.44f; case "BKN": return 0.75f; case "OVC": case "VV": return 1f;
                case "CLR": case "SKC": case "NSC": case "NCD": case "CAVOK": return 0f;
                default: return -1f;
            }
        }

        // EL TIEMPO PRESENTE (wxString, como «-RA», «+TSRA BR», «FZFG», «VCSH»): el codigo de la OMM y la lluvia en mm/h; codigo -1 si no dice nada que
        // cuente (HZ, FU, DU...). Primero la tormenta, luego lo que cae (nieve, lluvia helada, chubasco, lluvia, llovizna), luego niebla y neblina.
        internal static int CodigoDeTiempo(string wx, out float lluvia)
        {
            lluvia = 0f;
            if (string.IsNullOrWhiteSpace(wx)) return -1;
            bool ts = false, sn = false, ra = false, dz = false, fz = false, sh = false, fg = false, br = false, up = false, gr = false;
            int inten = 0;   // 1 ligera, 2 moderada, 3 fuerte (la de lo que cae)
            bool fgFz = false;
            foreach (string t0 in wx.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = t0.ToUpperInvariant();
                if (t.StartsWith("VC", StringComparison.Ordinal) || t.StartsWith("RE", StringComparison.Ordinal)) continue;   // cerca del aeropuerto o hace un rato: no
                int i = t[0] == '+' ? 3 : t[0] == '-' ? 1 : 2;
                string core = t.TrimStart('+', '-');
                if (core.Length < 2 || core.Length % 2 != 0) continue;
                bool ok = true; List<string> ch = new List<string>();
                for (int k = 0; k < core.Length; k += 2) { string c = core.Substring(k, 2); if (Array.IndexOf(Conocidos, c) < 0) { ok = false; break; } ch.Add(c); }
                if (!ok) continue;
                bool tFz = ch.Contains("FZ"), tSh = ch.Contains("SH"), tMi = ch.Contains("MI") || ch.Contains("PR");
                if (ch.Contains("TS")) ts = true;
                if (ch.Contains("GR")) gr = true;
                if (ch.Contains("SN") || ch.Contains("SG") || ch.Contains("IC") || ch.Contains("PL")) { sn = true; inten = Math.Max(inten, i); }
                if (ch.Contains("RA")) { ra = true; inten = Math.Max(inten, i); if (tFz) fz = true; if (tSh) sh = true; }
                if (ch.Contains("DZ")) { dz = true; if (!ra) inten = Math.Max(inten, i); if (tFz) fz = true; }
                if (ch.Contains("UP")) { up = true; inten = Math.Max(inten, 1); }
                if (ch.Contains("FG") && !tMi) { fg = true; if (tFz) fgFz = true; }
                if (ch.Contains("BR")) br = true;
                if (tSh && ch.Contains("SN")) sh = true;
            }
            if (ts) { lluvia = 10f; return gr ? (inten == 3 ? 99 : 96) : 95; }
            if (sn) { lluvia = inten == 3 ? 3f : inten == 1 ? 0.5f : 1.5f; return sh ? (inten == 3 ? 86 : 85) : inten == 3 ? 75 : inten == 1 ? 71 : 73; }
            if (ra && fz) { lluvia = inten == 3 ? 4f : 1f; return inten == 3 ? 67 : 66; }
            if (ra && sh) { lluvia = inten == 3 ? 10f : inten == 1 ? 1.5f : 5f; return inten == 3 ? 82 : inten == 1 ? 80 : 81; }
            if (ra) { lluvia = inten == 3 ? 8f : inten == 1 ? 0.5f : 2.5f; return inten == 3 ? 65 : inten == 1 ? 61 : 63; }
            if (dz) { lluvia = inten == 3 ? 0.6f : inten == 1 ? 0.2f : 0.4f; return fz ? (inten == 3 ? 57 : 56) : inten == 3 ? 55 : inten == 1 ? 51 : 53; }
            if (up) { lluvia = 0.5f; return 61; }
            if (fg) return fgFz ? 48 : 45;
            if (br) return 45;
            return -1;
        }
        private static readonly string[] Conocidos = { "MI", "BC", "PR", "DR", "BL", "SH", "TS", "FZ", "DZ", "RA", "SN", "SG", "IC", "PL", "GR", "GS", "UP", "BR", "FG", "FU", "VA", "DU", "SA", "HZ", "PO", "SQ", "FC", "SS", "DS" };

        // UN REPORTE DE LA API (un objeto del arreglo JSON de /api/data/metar?format=json); null si no trae lo necesario (la estacion y la hora)
        internal static RocolaOsMetarObs Lee(JsonElement r)
        {
            if (r.ValueKind != JsonValueKind.Object) return null;
            JsonElement v;
            string id = r.TryGetProperty("icaoId", out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            double obs = RocolaOsClima.Leer(r, "obsTime", double.NaN);
            if (string.IsNullOrEmpty(id) || double.IsNaN(obs) || obs <= 0) return null;
            RocolaOsMetarObs o = new RocolaOsMetarObs();
            o.Id = id.Trim().ToUpperInvariant();
            o.Cuando = DateTime.SpecifyKind(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(obs), DateTimeKind.Utc);
            o.Lat = RocolaOsClima.Leer(r, "lat", double.NaN); o.Lon = RocolaOsClima.Leer(r, "lon", double.NaN);
            // las nubes: la capa que mas cubre (ninguna capa = despejado: un METAR sin nubes dice CLR, SKC, NSC o CAVOK y la API manda «clouds» vacio o «CLR»)
            float nubes = 0f; bool dijo = false;
            if (r.TryGetProperty("clouds", out v) && v.ValueKind == JsonValueKind.Array)
                foreach (JsonElement c in v.EnumerateArray())
                {
                    string cov = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("cover", out JsonElement cv) && cv.ValueKind == JsonValueKind.String ? cv.GetString() : null;
                    float f = Cobertura(cov); if (f >= 0f) { dijo = true; nubes = Math.Max(nubes, f); }
                }
            if (!dijo && r.TryGetProperty("cover", out v) && v.ValueKind == JsonValueKind.String) { float f = Cobertura(v.GetString()); if (f > 0f) nubes = f; }
            o.Nubes = nubes;
            // el tiempo presente
            string wx = r.TryGetProperty("wxString", out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
            o.Tiempo = wx ?? "";
            float ll; int cod = CodigoDeTiempo(wx, out ll);
            if (cod < 0) cod = nubes >= 0.75f ? 3 : nubes >= 0.3f ? 2 : nubes >= 0.1f ? 1 : 0;
            o.Codigo = cod; o.Lluvia = ll;
            // viento (nudos a km/h; «VRB» o falta: no se sabe la direccion), temperatura y visibilidad (millas terrestres; «10+» es 10 o mas)
            double w = RocolaOsClima.Leer(r, "wspd", double.NaN); if (!double.IsNaN(w)) o.VientoKmh = w * 1.852;
            if (r.TryGetProperty("wdir", out v) && v.ValueKind == JsonValueKind.Number && !(w == 0)) o.Direccion = v.GetDouble();
            double t = RocolaOsClima.Leer(r, "temp", double.NaN); if (!double.IsNaN(t)) o.Temp = t;
            if (r.TryGetProperty("visib", out v))
            {
                if (v.ValueKind == JsonValueKind.Number) o.VisibilidadM = v.GetDouble() * 1609.34;
                else if (v.ValueKind == JsonValueKind.String)
                {
                    string s = v.GetString().Trim(); double d;
                    if (double.TryParse(s.TrimEnd('+'), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) o.VisibilidadM = d * 1609.34;
                }
            }
            return o;
        }

        // LA RESPUESTA ENTERA (un arreglo, o nada): todos los reportes que valen
        internal static List<RocolaOsMetarObs> LeeTodos(JsonElement raiz)
        {
            List<RocolaOsMetarObs> l = new List<RocolaOsMetarObs>();
            if (raiz.ValueKind != JsonValueKind.Array) return l;
            foreach (JsonElement e in raiz.EnumerateArray()) { RocolaOsMetarObs o = Lee(e); if (o != null) l.Add(o); }
            return l;
        }
        // EL MAS NUEVO de una estacion (id vacio: de cualquiera)
        internal static RocolaOsMetarObs Ultimo(List<RocolaOsMetarObs> l, string id)
        {
            RocolaOsMetarObs m = null;
            foreach (RocolaOsMetarObs o in l) if ((id.Length == 0 || o.Id == id) && (m == null || o.Cuando > m.Cuando)) m = o;
            return m;
        }
        // LA ESTACION MAS CERCANA (a CercaKm o menos) que tenga un METAR fresco; devuelve ese reporte (null si ninguna)
        internal static RocolaOsMetarObs LaMasCercana(List<RocolaOsMetarObs> l, double la, double lo, DateTime utcAhora, out double km)
        {
            RocolaOsMetarObs mejor = null; km = 0;
            HashSet<string> vistas = new HashSet<string>();
            foreach (RocolaOsMetarObs o in l)
            {
                if (!vistas.Add(o.Id) || double.IsNaN(o.Lat) || double.IsNaN(o.Lon)) continue;
                RocolaOsMetarObs u = Ultimo(l, o.Id);
                if (u == null || !u.Fresco(utcAhora)) continue;
                double k = Km(la, lo, o.Lat, o.Lon);
                if (k <= CercaKm && (mejor == null || k < km)) { mejor = u; km = k; }
            }
            return mejor;
        }
        // LAS COORDENADAS de la hoja: «20.6597» (o «20,6597»); false si no son un numero entre -max y max
        internal static bool Coordenada(string s, double max, out double v)
        {
            v = 0;
            if (s == null) return false;
            s = s.Trim().Replace(',', '.');
            if (s.Length == 0 || s.Length > 14) return false;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.IsNaN(v) && !double.IsInfinity(v) && Math.Abs(v) <= max;
        }
        // «lat, lon» pegado de un mapa («20.6597, -103.3496»): las dos de una vez
        internal static bool Pareja(string s, out double la, out double lo)
        {
            la = lo = 0;
            if (s == null) return false;
            string[] p = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return p.Length == 2 && Coordenada(p[0], 90, out la) && Coordenada(p[1], 180, out lo);
        }

        // ---------- rocola_clima_estacion.txt: la estacion que se recuerda una semana ----------
        internal sealed class Estacion
        {
            internal string Id = ""; internal double Lat, Lon, BarLat, BarLon; internal DateTime Buscada;   // (UTC); Id vacio = «no habia ninguna cerca»
            internal string Texto()
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                return "# la estacion de METAR mas cercana al bar (la busca y la escribe rocola-xp; se vuelve a buscar cada semana)\nid=" + Id + "\nlat=" + Lat.ToString("0.0000", ci)
                    + "\nlon=" + Lon.ToString("0.0000", ci) + "\nbar_lat=" + BarLat.ToString("0.0000", ci) + "\nbar_lon=" + BarLon.ToString("0.0000", ci)
                    + "\nbuscada=" + Buscada.ToString("yyyy-MM-ddTHH:mm:ssZ", ci) + "\n";
            }
            internal static Estacion DeTexto(string t)
            {
                if (string.IsNullOrEmpty(t)) return null;
                Estacion e = new Estacion(); bool b = false; double d;
                foreach (string r in t.Split('\n'))
                {
                    string l = r.Trim(); int i = l.IndexOf('=');
                    if (l.Length == 0 || l[0] == '#' || i <= 0) continue;
                    string k = l.Substring(0, i), v = l.Substring(i + 1).Trim();
                    switch (k)
                    {
                        case "id": e.Id = v.ToUpperInvariant(); break;
                        case "lat": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) e.Lat = d; break;
                        case "lon": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) e.Lon = d; break;
                        case "bar_lat": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) e.BarLat = d; break;
                        case "bar_lon": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) e.BarLon = d; break;
                        case "buscada": { DateTime q; if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out q)) { e.Buscada = DateTime.SpecifyKind(q, DateTimeKind.Utc); b = true; } } break;
                    }
                }
                return b ? e : null;
            }
            // si hay que volver a buscar: una semana (6 h si no habia ninguna cerca), el bar se movio mas de ~1 km, o la estacion ya queda a mas de 40 km
            internal bool Vieja(DateTime utcAhora, double la, double lo)
            {
                if (Id.Length == 0) return (utcAhora - Buscada).TotalHours >= 6 || Math.Abs(la - BarLat) > 0.01 || Math.Abs(lo - BarLon) > 0.01;
                return (utcAhora - Buscada).TotalDays >= 7 || Math.Abs(la - BarLat) > 0.01 || Math.Abs(lo - BarLon) > 0.01 || Km(la, lo, Lat, Lon) > CercaKm;
            }
        }
    }

    internal static partial class RocolaOsCielo
    {
        internal const string ArchivoEstacion = "rocola_clima_estacion.txt";
        private static RocolaOsMetar.Estacion estacion;               // la recordada
        private static int metarSinFresco;                            // intentos seguidos sin un METAR fresco de la estacion recordada
        private static string metarFallo = "";
        private static RocolaOsClima climaOm;                         // el ultimo de Open-Meteo (el relleno del METAR)
        private static long metarPedidos;
        internal static long MetarPedidos { get { return Interlocked.Read(ref metarPedidos); } }

        // LAS COORDENADAS EXACTAS DEL BAR (bar_lat= y bar_lon= de rocola_os.txt): las dos y validas, o no hay
        internal static bool CoordenadasDelBar(out double lat, out double lon)
        {
            lat = lon = 0;
            return RocolaOsMetar.Coordenada(RocolaOs.Ajuste("bar_lat"), 90, out lat) && RocolaOsMetar.Coordenada(RocolaOs.Ajuste("bar_lon"), 180, out lon) && !(lat == 0 && lon == 0)
                && (RocolaOs.Ajuste("bar_lat") ?? "").Trim().Length > 0 && (RocolaOs.Ajuste("bar_lon") ?? "").Trim().Length > 0;
        }
        // DE DONDE SALE EL LUGAR, para la hoja
        internal static string FuenteLugar()
        {
            double la, lo;
            if (CiudadForzada != null) return "variable de prueba ROCOLA_CIUDAD";
            if (CoordenadasDelBar(out la, out lo)) return "coordenadas del bar";
            int c = CiudadElegida;
            if (c > 0) return "ciudad de la lista: " + RocolaOsCiudades.Nombres[c] + " (el centro de la ciudad)";
            RocolaOsLugar g = Lugar;
            if (g != null) return "por internet: " + (g.Ciudad.Length > 0 ? g.Ciudad : "?") + " (aproximado)";
            return "todavía no se sabe (se usa la Ciudad de México)";
        }
        // DE DONDE SALE EL CLIMA QUE SE VE y que tan viejo es, para la hoja
        internal static string FuenteClima(RocolaOsClima c, DateTime utcAhora)
        {
            if (c == null) return "Todavía no se sabe el clima de afuera.";
            if (c.Forzado) return "Forzado (prueba o «Ver cómo se vería»).";
            int min = Math.Max(0, (int)Math.Round((utcAhora - c.Medida).TotalMinutes));
            string hace = min < 90 ? min + " min" : (min / 60) + " h" + (min % 60 >= 30 ? " y media" : "");
            if (c.Fuente == "metar")
            {
                string de = "el aeropuerto " + c.Estacion + " (" + c.Km.ToString("0", CultureInfo.InvariantCulture) + " km)";
                return min <= RocolaOsMetar.FrescoMin ? "Observado en " + de + " hace " + hace + "." : "Sin internet: el último, observado en " + de + " hace " + hace + ".";
            }
            if (min <= 40)
            {
                double la, lo;
                return "Pronóstico de Open-Meteo para " + (CoordenadasDelBar(out la, out lo) ? "tus coordenadas" : "la ciudad del bar") + " (no hay aeropuerto cerca con observación reciente).";
            }
            return "Sin internet: el de hace " + hace + ".";
        }

        // LEE LA ESTACION GUARDADA (al arrancar el hilo)
        private static void LeeEstacionGuardada()
        {
            try
            {
                string[] l = Form1.RocolaOsLeeLineas(RocolaOs.Datos(ArchivoEstacion));
                RocolaOsMetar.Estacion e = l != null ? RocolaOsMetar.Estacion.DeTexto(string.Join("\n", l)) : null;
                if (e != null) { estacion = e; RocolaOs.Apunta("cielo", "la estacion de METAR guardada: " + (e.Id.Length > 0 ? e.Id : "ninguna cerca")); }
            }
            catch (Exception ex) { RocolaOs.Falla("cielo", "leer " + ArchivoEstacion, ex); }
        }
        private static string BaseMetar()
        {
            string m = Environment.GetEnvironmentVariable("ROCOLA_METAR_API");
            if (!string.IsNullOrEmpty(m)) return m.ToLowerInvariant() == "off" ? null : m.TrimEnd('/');
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROCOLA_CLIMA_API")) ? "https://aviationweather.gov" : null;   // (un Open-Meteo falso: sin METAR)
        }
        private static void GuardaEstacion(RocolaOsMetar.Estacion e)
        {
            estacion = e;
            try { Form1.RocolaOsEscribeSeguro(RocolaOs.Datos(ArchivoEstacion), e.Texto()); } catch (Exception ex) { RocolaOs.Falla("cielo", "guardar " + ArchivoEstacion, ex); }
        }
        // EL METAR FRESCO DE LA ESTACION CERCANA, o null (sin estacion, sin red, viejo). km: su distancia al bar.
        private static RocolaOsMetarObs PideMetar(double la, double lo, out float km)
        {
            km = 0;
            string b = BaseMetar();
            if (b == null) return null;
            DateTime ahora = DateTime.UtcNow; string e;
            if (estacion != null && !estacion.Vieja(ahora, la, lo) && estacion.Id.Length == 0) return null;   // «ninguna cerca»: no se vuelve a buscar hasta dentro de 6 h
            if (estacion != null && estacion.Id.Length > 0 && !estacion.Vieja(ahora, la, lo))
            {
                Interlocked.Increment(ref metarPedidos);
                List<RocolaOsMetarObs> l = Bajan(b + "/api/data/metar?ids=" + Uri.EscapeDataString(estacion.Id) + "&format=json&hours=2", out e);
                RocolaOsMetarObs u = l != null ? RocolaOsMetar.Ultimo(l, estacion.Id) : null;
                if (u != null && u.Fresco(ahora)) { metarSinFresco = 0; metarFallo = ""; km = (float)RocolaOsMetar.Km(la, lo, estacion.Lat, estacion.Lon); return u; }
                if (l == null) { FallaMetar("METAR de " + estacion.Id + ": " + e); return null; }   // (sin red: no se cambia de estacion)
                FallaMetar("METAR de " + estacion.Id + ": " + (u == null ? "sin reportes en 2 h" : "el ultimo es de hace " + u.EdadMin(ahora) + " min (no vale)"));
                if (++metarSinFresco < 3) return null;
                metarSinFresco = 0;   // 3 veces seguidas sin nada fresco: se busca otra
            }
            // LA BUSQUEDA: el cuadro alrededor del bar
            Interlocked.Increment(ref metarPedidos);
            List<RocolaOsMetarObs> todos = Bajan(b + "/api/data/metar?bbox=" + RocolaOsMetar.Cuadro(la, lo) + "&format=json&hours=2", out e);
            if (todos == null) { FallaMetar("busqueda de estaciones: " + e); return null; }
            double k; RocolaOsMetarObs m = RocolaOsMetar.LaMasCercana(todos, la, lo, ahora, out k);
            RocolaOsMetar.Estacion n = new RocolaOsMetar.Estacion { Id = m != null ? m.Id : "", Lat = m != null ? m.Lat : 0, Lon = m != null ? m.Lon : 0, BarLat = la, BarLon = lo, Buscada = ahora };
            GuardaEstacion(n);
            metarFallo = "";
            RocolaOs.Apunta("cielo", m != null ? "la estacion de METAR mas cercana: " + m.Id + " a " + k.ToString("0.#", CultureInfo.InvariantCulture) + " km" : "ninguna estacion de METAR fresca a " + RocolaOsMetar.CercaKm + " km o menos del bar");
            km = (float)k;
            return m;
        }
        private static void FallaMetar(string f)
        {
            if (f != metarFallo) RocolaOs.Apunta("cielo", "no se pudo pedir el METAR: " + f);
            metarFallo = f;
        }
        // un GET del arreglo de METAR (204 = ninguno: arreglo vacio); null y por que si falla
        private static List<RocolaOsMetarObs> Bajan(string url, out string error)
        {
            error = null;
            try
            {
                using (System.Net.Http.HttpRequestMessage q = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url))
                using (System.Net.Http.HttpResponseMessage r = Http(url).Send(q))
                {
                    int c = (int)r.StatusCode;
                    if (c == 204) return new List<RocolaOsMetarObs>();
                    if (c != 200) { error = "contesto " + c; return null; }
                    using (Stream s = r.Content.ReadAsStream())
                    using (JsonDocument d = JsonDocument.Parse(s)) return RocolaOsMetar.LeeTodos(d.RootElement);
                }
            }
            catch (Exception ex) { error = "sin red o respuesta rara (" + ex.GetType().Name + ")"; return null; }
        }
    }
}
