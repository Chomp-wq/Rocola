// Lo esencial en pocas palabras: si la rocola esta en linea, si la actualizacion ya esta, los errores... (para el tablero y la terminal).
using System;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace RocolaCasa
{
    internal static class Resumen
    {
        internal static string S(JsonNode n) { try { return n == null ? "" : n.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : n.ToJsonString(); } catch { return ""; } }
        private static int I(string s) { return int.TryParse((s ?? "").Trim(), out int n) ? n : -1; }

        // Cuando escribio la rocola por ultima vez (null si no se sabe).
        internal static DateTimeOffset? Visto(JsonNode estado)
        {
            if (estado == null) return null;
            string f = S(estado["fecha"]), z = S(estado["zona"]);
            if (DateTimeOffset.TryParseExact(f + " " + (z.Length == 5 ? z.Substring(0, 3) + ":" + z.Substring(3) : "+00:00"), "yyyy-MM-dd HH:mm:ss zzz",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset d)) return d;
            return null;
        }
        private static JsonObject V(string nivel, string titulo, string texto) { return new JsonObject { ["nivel"] = nivel, ["titulo"] = titulo, ["texto"] = texto }; }
        internal static string Hace(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return "hace un momento";
            if (t.TotalMinutes < 60) return "hace " + (int)t.TotalMinutes + " min";
            if (t.TotalHours < 48) return "hace " + (int)t.TotalHours + " h " + t.Minutes + " min";
            return "hace " + (int)t.TotalDays + " dias";
        }

        internal static JsonArray Veredictos(JsonObject r)
        {
            JsonArray l = new JsonArray();
            JsonNode e = r["estado"];
            // EN LINEA: escribe al menos una vez por hora
            if (r["buzon_ok"]?.GetValue<bool>() != true) l.Add((JsonNode)V("mal", "Buzon", "No se puede leer el buzon: " + S(r["buzon_error"]) + ". Revisa Ajustes."));
            DateTimeOffset? visto = Visto(e);
            if (visto == null) l.Add((JsonNode)V("aviso", "Conexion", "La rocola aun no ha escrito en el buzon (le falta el sistema con el buzon o su llave rocola_buzon.txt)."));
            else
            {
                TimeSpan t = DateTimeOffset.Now - visto.Value;
                if (t.TotalMinutes <= 70) l.Add((JsonNode)V("ok", "Conexion", "En linea: escribio " + Hace(t) + "."));
                else l.Add((JsonNode)V("mal", "Conexion", "Sin noticias desde " + Hace(t) + ": apagada, sin internet o el buzon se detuvo. Prueba «Despertar»."));
            }
            JsonNode ver = r["versiones"];
            int ultP = I(S(ver?["programa"]?["n"])), ultS = I(S(ver?["sistema"]?["n"]));
            if (e != null)
            {
                int p = I(S(e["versiones"]?["programa"])), pn = I(S(e["versiones"]?["programa_nueva"])), s = I(S(e["versiones"]?["sistema"]));
                string malas = S(e["versiones"]?["programa_malas"]);
                if (ultP < 0) l.Add((JsonNode)V("aviso", "Programa", "Tiene la version " + p + " (no se pudo ver la ultima publicada)."));
                else if (p >= ultP) l.Add((JsonNode)V("ok", "Programa", "Al dia: version " + p + "."));
                else if (pn == ultP) l.Add((JsonNode)V("aviso", "Programa", "La " + ultP + " ya esta bajada; se pone sola cuando la rocola este libre (tiene la " + p + "). Para ponerla ya: Ordenes -> reiniciar el programa ya."));
                else if ((" " + malas + " ").Contains(" " + ultP + " ")) l.Add((JsonNode)V("mal", "Programa", "La " + ultP + " se aparto como mala (no arranco en la rocola). Sigue con la " + p + ". Revisa los errores y rocola.txt."));
                else l.Add((JsonNode)V("aviso", "Programa", "Tiene la " + p + "; la ultima es la " + ultP + ". La buscara en su revision (cada hora) o al momento con «Buscar actualizacion»."));
                if (ultS >= 0 && s >= 0)
                {
                    string prueba = S(e["versiones"]?["sistema_en_prueba"]), pend = S(e["versiones"]?["reinicio_pendiente"]);
                    if (s >= ultS) l.Add((JsonNode)V("ok", "Sistema", "Al dia: sistema " + s + (prueba == "si" ? " (en prueba: se confirma a los 3 min)" : "") + "."));
                    else if (pend.Contains("sistema")) l.Add((JsonNode)V("aviso", "Sistema", "El sistema " + ultS + " ya esta bajado: se reinicia cuando la rocola este libre (tiene el " + s + ")."));
                    else l.Add((JsonNode)V("aviso", "Sistema", "Tiene el sistema " + s + "; el ultimo es el " + ultS + ". Se baja en su revision (cada hora) o con «Buscar actualizacion»."));
                }
                string act = S(e["actualizacion"]?["estado"]);
                if (act.Length > 0) l.Add((JsonNode)V(act.Contains("no ") || act.Contains("falta") || act.Contains("sin internet") ? "aviso" : "ok", "Actualizacion", act));
                if (S(e["rocola"]?["corriendo"]) != "si") l.Add((JsonNode)V("mal", "Rocola", "El programa de la rocola no esta corriendo (¿se esta reiniciando o se cae al arrancar?). Mira Registros -> rocola.txt."));
                int nerr = I(S(e["errores"]?["total_lineas"]));
                if (nerr > 0) l.Add((JsonNode)V("aviso", "Errores", nerr + " lineas en rocola_error.txt. Mira la pestaña Errores."));
                int temp = I(S(e["equipo"]?["temperatura_c"]));
                if (temp >= 75) l.Add((JsonNode)V("mal", "Equipo", "Temperatura alta: " + temp + " °C."));
            }
            // las compilaciones en curso o fallidas
            if (r["compilaciones"] is JsonArray comp)
                foreach (string nombre in new[] { "Programa", "Sistema" })
                    foreach (JsonNode c in comp)
                    {
                        if (!S(c["nombre"]).StartsWith(nombre)) continue;
                        string est = S(c["estado"]), res = S(c["resultado"]);
                        if (est != "completed") l.Add((JsonNode)V("aviso", "Compilacion", S(c["nombre"]) + " #" + S(c["numero"]) + ": en curso."));
                        else if (res != "success") l.Add((JsonNode)V("mal", "Compilacion", S(c["nombre"]) + " #" + S(c["numero"]) + ": termino mal (" + res + ")."));
                        break;   // solo la ultima de cada una
                    }
            return l;
        }

        internal static string Texto(JsonObject r)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ROCOLA EN CASA ").Append(Program.Version).Append('\n');
            if (r["veredictos"] is JsonArray v)
                foreach (JsonNode x in v)
                {
                    string n = S(x["nivel"]);
                    sb.Append(n == "ok" ? "  [ok]    " : n == "mal" ? "  [MAL]   " : "  [ojo]   ").Append(S(x["titulo"])).Append(": ").Append(S(x["texto"])).Append('\n');
                }
            JsonNode e = r["estado"];
            if (e != null)
            {
                sb.Append("\nLo que pasa ahora:\n").Append(Sangra(S(e["rocola"]?["vivo"])));
                sb.Append("\nEquipo: carga ").Append(S(e["equipo"]?["carga"])).Append(", ").Append(S(e["equipo"]?["temperatura_c"])).Append(" °C, memoria ").Append(S(e["equipo"]?["memoria"]))
                  .Append(", datos ").Append(S(e["equipo"]?["disco_datos"])).Append(", disco ").Append(S(e["equipo"]?["disco_rocola"])).Append('\n');
                sb.Append("Red: ").Append(S(e["red"]?["ip"])).Append("  MAC ").Append(S(e["red"]?["mac"])).Append("  Wake-on: ").Append(S(e["red"]?["wake_on_lan"])).Append('\n');
                string err = S(e["errores"]?["ultimos"]);
                if (err.Length > 0) { sb.Append("\nUltimos errores:\n"); string[] ls = err.Split('\n'); for (int i = Math.Max(0, ls.Length - 12); i < ls.Length; i++) sb.Append("  ").Append(ls[i]).Append('\n'); }
            }
            return sb.ToString();
        }
        private static string Sangra(string s) { StringBuilder b = new StringBuilder(); foreach (string l in (s ?? "").Split('\n')) if (l.Length > 0) b.Append("  ").Append(l).Append('\n'); return b.ToString(); }
    }
}
