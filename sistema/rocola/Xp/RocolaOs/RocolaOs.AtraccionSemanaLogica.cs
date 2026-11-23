// LA CUENTA DE LA SEMANA DEL MODO ATRACCION: la logica pura (Rocola OS; peticion del dueno del 23-11-2026: «en el modo atraccion, cambia los
// avisos [...] luego, las canciones mas pedidas, en vez de ser las mas pedidas de la noche, seran las mas pedidas de la semana»).
// Sin Form1, sin archivos ni reloj: la prueba (Pruebas/RocolaOs/atraccion-semana) compila este archivo solo. Un dia se nombra como lo hace
// XP con la noche (ClaveNoche: hasta las 8 de la manana todavia es el dia de ayer, «AAAA-MM-DD»); la semana es una VENTANA MOVIL: el dia de
// hoy y los 6 anteriores. Archivo rocola_semana.txt: «semana=1» y luego «AAAA-MM-DD|veces|ruta» (de la mas vieja a la mas nueva).
// Lo de Form1 (leer, guardar, enganches) esta en RocolaOs.AtraccionSemana.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rocola
{
    internal sealed class AtraccionSemanaDatos
    {
        internal const int Dias = 7, TopePorDia = 400;   // TopePorDia = NocheTope de XP
        internal const string Cabecera = "semana=1";
        internal readonly Dictionary<string, Dictionary<string, int>> PorDia = new Dictionary<string, Dictionary<string, int>>();
        internal readonly Dictionary<string, long> Orden = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // la ultima pedida de cada ruta
        internal long Seq;
        internal int Version;   // sube con cada cambio (para recordar la suma)

        // el dia (como ClaveNoche de XP) de un momento: antes de las 8 de la manana todavia es el de ayer
        internal static string ClaveDia(DateTime t) { return t.AddHours(-8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        // los dias de la ventana que acaba en «hoy»; vacio si «hoy» no es una fecha
        internal static List<string> Ventana(string hoy)
        {
            List<string> r = new List<string>(Dias);
            DateTime d;
            if (!DateTime.TryParseExact(hoy, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return r;
            for (int i = 0; i < Dias; i++) r.Add(d.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            return r;
        }
        private static bool EnVentana(string dia, string hoy) { return Ventana(hoy).Contains(dia); }

        // suma 1 a la ruta en ese dia (con el tope de rutas distintas por dia); false si no cuenta
        internal bool Cuenta(string ruta, string dia)
        {
            if (string.IsNullOrEmpty(ruta) || string.IsNullOrEmpty(dia)) return false;
            Dictionary<string, int> d;
            if (!PorDia.TryGetValue(dia, out d)) { d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); PorDia[dia] = d; }
            int n; d.TryGetValue(ruta, out n);
            if (n == 0 && d.Count >= TopePorDia) return false;
            d[ruta] = n + 1; Orden[ruta] = ++Seq; Version++;
            return true;
        }

        // quita los dias fuera de la ventana (y el orden de lo que ya no cuenta); true si quito algo
        internal bool Poda(string hoy)
        {
            List<string> v = Ventana(hoy), fuera = new List<string>();
            foreach (string k in PorDia.Keys) if (!v.Contains(k)) fuera.Add(k);
            if (fuera.Count == 0) return false;
            for (int i = 0; i < fuera.Count; i++) PorDia.Remove(fuera[i]);
            List<string> sobra = new List<string>();
            foreach (string r in Orden.Keys) { if (!Esta(r)) sobra.Add(r); }
            for (int i = 0; i < sobra.Count; i++) Orden.Remove(sobra[i]);
            Version++;
            return true;
        }
        private bool Esta(string ruta) { foreach (Dictionary<string, int> d in PorDia.Values) if (d.ContainsKey(ruta)) return true; return false; }

        // la suma de los dias de la ventana
        internal Dictionary<string, int> Suma(string hoy)
        {
            Dictionary<string, int> t = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<string> v = Ventana(hoy);
            foreach (KeyValuePair<string, Dictionary<string, int>> dia in PorDia)
            {
                if (!v.Contains(dia.Key)) continue;
                foreach (KeyValuePair<string, int> kv in dia.Value) { int n; t.TryGetValue(kv.Key, out n); t[kv.Key] = n + kv.Value; }
            }
            return t;
        }

        // «semana=1» y «AAAA-MM-DD|veces|ruta», de la mas vieja a la mas nueva (dentro de un dia, de la pedida hace mas tiempo a la ultima)
        internal string Texto(string hoy)
        {
            List<string> v = Ventana(hoy);
            List<string> dias = new List<string>();
            foreach (string k in PorDia.Keys) if (v.Contains(k)) dias.Add(k);
            dias.Sort(StringComparer.Ordinal);
            StringBuilder sb = new StringBuilder(64);
            sb.Append(Cabecera).Append("\r\n");
            for (int i = 0; i < dias.Count; i++)
            {
                Dictionary<string, int> d = PorDia[dias[i]];
                List<string> ks = new List<string>(d.Keys);
                ks.Sort(delegate (string a, string b)
                {
                    long x, y; Orden.TryGetValue(a, out x); Orden.TryGetValue(b, out y);
                    int c = x.CompareTo(y); return c != 0 ? c : string.CompareOrdinal(a, b);
                });
                for (int j = 0; j < ks.Count; j++)
                    sb.Append(dias[i]).Append('|').Append(d[ks[j]].ToString(CultureInfo.InvariantCulture)).Append('|').Append(ks[j]).Append("\r\n");
            }
            return sb.ToString();
        }

        // lee las lineas de un archivo (sin tocar lo que ya hay si no es el formato); false si no es «semana=1». Los dias fuera de la ventana se descartan
        internal bool Carga(string[] ls, string hoy)
        {
            if (ls == null || ls.Length == 0 || ls[0].Trim() != Cabecera) return false;
            List<string> v = Ventana(hoy);
            for (int i = 1; i < ls.Length; i++)
            {
                string s = ls[i]; if (s == null) continue;
                int b1 = s.IndexOf('|'); if (b1 != 10) continue;
                int b2 = s.IndexOf('|', b1 + 1); if (b2 <= b1 + 1 || b2 >= s.Length - 1) continue;
                string dia = s.Substring(0, b1);
                if (!v.Contains(dia)) continue;
                int n;
                if (!int.TryParse(s.Substring(b1 + 1, b2 - b1 - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) continue;
                string p = s.Substring(b2 + 1);
                Dictionary<string, int> d;
                if (!PorDia.TryGetValue(dia, out d)) { d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); PorDia[dia] = d; }
                if (!d.ContainsKey(p) && d.Count >= TopePorDia) continue;
                d[p] = n; Orden[p] = ++Seq;
            }
            Version++;
            return true;
        }

        // se siembra con lo de hoy (la cuenta de la noche) cuando no hay archivo de la semana; las claves ya vienen de la mas vieja a la mas nueva
        internal void Siembra(string dia, IList<KeyValuePair<string, int>> hoyCuenta)
        {
            if (hoyCuenta == null) return;
            Dictionary<string, int> d;
            if (!PorDia.TryGetValue(dia, out d)) { d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); PorDia[dia] = d; }
            for (int i = 0; i < hoyCuenta.Count; i++)
            {
                if (hoyCuenta[i].Value <= 0 || (!d.ContainsKey(hoyCuenta[i].Key) && d.Count >= TopePorDia)) continue;
                d[hoyCuenta[i].Key] = hoyCuenta[i].Value; Orden[hoyCuenta[i].Key] = ++Seq;
            }
            Version++;
        }
    }
}
