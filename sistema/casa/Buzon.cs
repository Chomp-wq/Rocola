// La configuracion, GitHub y el buzon de la rocola (lo que lee y escribe Rocola en casa).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace RocolaCasa
{
    // ---- la configuracion (en la carpeta del usuario; la llave solo la lee el usuario) ----
    internal sealed class Config
    {
        public string Llave = "", Repo = "Chomp-wq/Rocola", Rama = "claude/modest-carson-fa2v2n", Buzon = "Chomp-wq/rocola-buzon", WolHost = "", Mac = "";
        public int WolPuerto = 9, Puerto = 8765;
        public bool Lista { get { return Llave.Length > 10; } }

        internal static string Carpeta
        {
            get
            {
                string b = OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                    : (Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is string x && x.Length > 0 ? x : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"));
                return Path.Combine(b, OperatingSystem.IsWindows() ? "RocolaCasa" : "rocola-casa");
            }
        }
        internal static string Archivo { get { return Path.Combine(Carpeta, "config.json"); } }
        internal static string Descargas { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RocolaCasa"); } }

        internal static Config Lee()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(Archivo)) return c;
                JsonNode n = JsonNode.Parse(File.ReadAllText(Archivo));
                c.Llave = Txt(n, "llave", c.Llave); c.Repo = Txt(n, "repo", c.Repo); c.Rama = Txt(n, "rama", c.Rama); c.Buzon = Txt(n, "buzon", c.Buzon);
                c.WolHost = Txt(n, "wol_host", c.WolHost); c.Mac = Txt(n, "mac", c.Mac);
                c.WolPuerto = Num(n, "wol_puerto", c.WolPuerto); c.Puerto = Num(n, "puerto", c.Puerto);
            }
            catch { }
            return c;
        }
        internal void Guarda()
        {
            Directory.CreateDirectory(Carpeta);
            JsonObject o = new JsonObject { ["llave"] = Llave, ["repo"] = Repo, ["rama"] = Rama, ["buzon"] = Buzon, ["wol_host"] = WolHost, ["wol_puerto"] = WolPuerto, ["mac"] = Mac, ["puerto"] = Puerto };
            File.WriteAllText(Archivo, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows()) try { File.SetUnixFileMode(Archivo, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
        }
        internal JsonObject Publica()   // lo que ve el tablero (la llave, solo si esta)
        {
            return new JsonObject { ["llave_puesta"] = Lista, ["repo"] = Repo, ["rama"] = Rama, ["buzon"] = Buzon, ["wol_host"] = WolHost, ["wol_puerto"] = WolPuerto, ["mac"] = Mac,
                ["archivo"] = Archivo, ["descargas"] = Descargas, ["version"] = Program.Version };
        }
        internal static string Txt(JsonNode n, string k, string def) { try { JsonNode v = n?[k]; return v != null ? v.GetValue<string>() ?? def : def; } catch { return def; } }
        internal static int Num(JsonNode n, string k, int def) { try { JsonNode v = n?[k]; return v != null ? v.GetValue<int>() : def; } catch { try { return int.Parse(n[k].GetValue<string>()); } catch { return def; } } }
    }

    // ---- GitHub ----
    internal sealed class GitHub
    {
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly string llave;
        internal GitHub(string llave) { this.llave = llave; }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string etag, string cuerpo)> recordado = new System.Collections.Concurrent.ConcurrentDictionary<string, (string, string)>();
        // (para probarlo sin GitHub: ROCOLA_CASA_API=http://127.0.0.1:PUERTO)
        private static readonly string Base = Environment.GetEnvironmentVariable("ROCOLA_CASA_API") is string b && b.Length > 0 ? b.TrimEnd('/') : "https://api.github.com";

        internal async Task<(int codigo, string cuerpo)> Pide(HttpMethod m, string ruta, string json = null, string acepta = "application/vnd.github+json")
        {
            using (HttpRequestMessage r = new HttpRequestMessage(m, Base + ruta))
            {
                r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", llave);
                r.Headers.Accept.ParseAdd(acepta);
                r.Headers.UserAgent.ParseAdd("rocola-casa/" + Program.Version);
                r.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                if (json != null) r.Content = new StringContent(json, Encoding.UTF8, "application/json");
                // OPTIMIZACION: lo leido se recuerda con su ETag y se pregunta «¿cambio?» (If-None-Match): si no, GitHub contesta 304 sin
                // cuerpo y sin gastar consultas del limite por hora (el tablero pregunta lo mismo cada 30 s).
                string clave = m == HttpMethod.Get ? acepta + " " + ruta : null;
                (string etag, string cuerpo) ya = default;
                if (clave != null && recordado.TryGetValue(clave, out ya)) r.Headers.TryAddWithoutValidation("If-None-Match", ya.etag);
                try
                {
                    using (HttpResponseMessage s = await http.SendAsync(r))
                    {
                        if (clave != null && (int)s.StatusCode == 304 && ya.cuerpo != null) return (200, ya.cuerpo);
                        string b = await s.Content.ReadAsStringAsync();
                        if (clave != null)
                        {
                            if ((int)s.StatusCode == 200 && s.Headers.ETag != null && b.Length < 8_000_000) recordado[clave] = (s.Headers.ETag.ToString(), b);
                            else recordado.TryRemove(clave, out _);
                        }
                        return ((int)s.StatusCode, b);
                    }
                }
                catch (Exception ex) { return (0, ex.Message); }
            }
        }
        internal async Task<JsonNode> Json(string ruta)
        {
            var (c, b) = await Pide(HttpMethod.Get, ruta);
            if (c != 200) return null;
            try { return JsonNode.Parse(b); } catch { return null; }
        }
        // Un archivo de un repositorio, tal cual (null si no esta).
        internal async Task<string> Archivo(string repo, string ruta)
        {
            var (c, b) = await Pide(HttpMethod.Get, "/repos/" + repo + "/contents/" + Uri.EscapeDataString(ruta).Replace("%2F", "/"), null, "application/vnd.github.raw+json");   // (sin truco contra la cache: el ETag dice si cambio)
            return c == 200 ? b : null;
        }
        internal async Task<(string texto, string sha)> ArchivoConSha(string repo, string ruta)
        {
            var (c, b) = await Pide(HttpMethod.Get, "/repos/" + repo + "/contents/" + ruta);
            if (c != 200) return (null, null);
            JsonNode n = JsonNode.Parse(b);
            string sha = n["sha"]?.GetValue<string>();
            string cont = n["content"]?.GetValue<string>() ?? "";
            try { return (Encoding.UTF8.GetString(Convert.FromBase64String(cont.Replace("\n", ""))), sha); } catch { return (null, sha); }
        }
        internal async Task<(bool ok, string error)> Escribe(string repo, string ruta, string texto, string mensaje)
        {
            for (int intento = 0; intento < 3; intento++)
            {
                var (_, sha) = await ArchivoConSha(repo, ruta);
                JsonObject o = new JsonObject { ["message"] = mensaje, ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(texto)) };
                if (sha != null) o["sha"] = sha;
                var (c, b) = await Pide(HttpMethod.Put, "/repos/" + repo + "/contents/" + ruta, o.ToJsonString());
                if (c == 200 || c == 201) return (true, null);
                if (c != 409 && c != 422) return (false, Error(c, b));
            }
            return (false, "el archivo cambio mientras se escribia: intentalo otra vez");
        }
        internal static string Error(int c, string b)
        {
            switch (c)
            {
                case 0: return "sin internet (" + b + ")";
                case 401: return "la llave no vale (caduco o esta mal copiada)";
                case 403: return "la llave no tiene permiso para esto";
                case 404: return "no se encontro (¿el repositorio existe y la llave lo incluye?)";
                default: return "GitHub contesto " + c;
            }
        }
    }

    // ---- el buzon y todo lo que ve el tablero ----
    internal sealed class Buzon
    {
        private readonly Config cfg;
        private readonly GitHub gh;
        internal string UltimoError = "";
        internal Buzon(Config cfg) { this.cfg = cfg; gh = new GitHub(cfg.Llave); }

        internal static readonly string[] Ordenes = { "ping", "informe", "registros", "archivo", "buscar_actualizacion", "diagnostico", "reiniciar_programa", "reiniciar_equipo", "apagar", "cancelar", "llave_genius" };

        // Deja una orden en casa/ordenes.json (se guardan las ultimas 40). Devuelve su id, o null.
        internal async Task<string> Ordena(string orden, string arg)
        {
            if (Array.IndexOf(Ordenes, orden) < 0) { UltimoError = "orden desconocida: " + orden; return null; }
            var (texto, _) = await gh.ArchivoConSha(cfg.Buzon, "casa/ordenes.json");
            JsonArray l;
            try { l = texto != null ? (JsonNode.Parse(texto) as JsonArray ?? new JsonArray()) : new JsonArray(); } catch { l = new JsonArray(); }
            string id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            l.Add((JsonNode)new JsonObject { ["id"] = id, ["orden"] = orden, ["arg"] = arg ?? "", ["fecha"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), ["desde"] = Environment.MachineName });
            while (l.Count > 40) l.RemoveAt(0);
            var (ok, err) = await gh.Escribe(cfg.Buzon, "casa/ordenes.json", l.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), "orden: " + orden + (string.IsNullOrEmpty(arg) ? "" : " " + arg));
            if (!ok) { UltimoError = err; return null; }
            return id;
        }

        // Todo lo que muestra el tablero, de una vez.
        internal async Task<JsonObject> Todo()
        {
            JsonObject r = new JsonObject { ["config"] = cfg.Publica(), ["ahora"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) };
            Task<string> tEstado = gh.Archivo(cfg.Buzon, "rocola/estado.json");
            Task<string> tResp = gh.Archivo(cfg.Buzon, "rocola/respuestas.json");
            Task<string> tOrd = gh.Archivo(cfg.Buzon, "casa/ordenes.json");
            Task<JsonNode> tRel = gh.Json("/repos/" + cfg.Repo + "/releases?per_page=60");
            Task<JsonNode> tRuns = gh.Json("/repos/" + cfg.Repo + "/actions/runs?per_page=15&event=workflow_dispatch");
            Task<JsonNode> tArch = gh.Json("/repos/" + cfg.Buzon + "/contents/rocola/archivos");
            Task<(int, string)> tRepo = gh.Pide(HttpMethod.Get, "/repos/" + cfg.Buzon);
            await Task.WhenAll(tEstado, tResp, tOrd, tRel, tRuns, tArch, tRepo);
            var (cb, bb) = tRepo.Result;
            r["buzon_ok"] = cb == 200; r["buzon_error"] = cb == 200 ? "" : GitHub.Error(cb, bb);
            JsonNode estado = null; try { if (tEstado.Result != null) estado = JsonNode.Parse(tEstado.Result); } catch { }
            r["estado"] = estado;
            try { r["respuestas"] = tResp.Result != null ? JsonNode.Parse(tResp.Result) : new JsonArray(); } catch { r["respuestas"] = new JsonArray(); }
            try { r["ordenes"] = tOrd.Result != null ? JsonNode.Parse(tOrd.Result) : new JsonArray(); } catch { r["ordenes"] = new JsonArray(); }
            // las versiones publicadas (la ultima de cada tipo)
            JsonObject ver = new JsonObject();
            if (tRel.Result is JsonArray rels)
                foreach (string tipo in new[] { "programa", "sistema", "casa" })
                {
                    int mejor = -1; JsonNode cual = null;
                    foreach (JsonNode x in rels)
                    {
                        string tag = x?["tag_name"]?.GetValue<string>() ?? "";
                        if (!tag.StartsWith(tipo + "-")) continue;
                        if (int.TryParse(tag.Substring(tipo.Length + 1), out int n) && n > mejor) { mejor = n; cual = x; }
                    }
                    if (cual != null)
                        ver[tipo] = new JsonObject { ["n"] = mejor, ["fecha"] = cual["published_at"]?.GetValue<string>(), ["titulo"] = cual["name"]?.GetValue<string>(), ["url"] = cual["html_url"]?.GetValue<string>(),
                            ["notas"] = cual["body"]?.GetValue<string>() };
                }
            else r["repo_error"] = "no se pudieron leer las versiones de " + cfg.Repo;
            r["versiones"] = ver;
            JsonArray comp = new JsonArray();
            if (tRuns.Result?["workflow_runs"] is JsonArray runs)
                foreach (JsonNode x in runs)
                    comp.Add((JsonNode)new JsonObject { ["nombre"] = x["name"]?.GetValue<string>(), ["numero"] = x["run_number"]?.GetValue<int>(), ["estado"] = x["status"]?.GetValue<string>(),
                        ["resultado"] = x["conclusion"]?.GetValue<string>(), ["fecha"] = x["created_at"]?.GetValue<string>(), ["actualizado"] = x["updated_at"]?.GetValue<string>(),
                        ["url"] = x["html_url"]?.GetValue<string>(), ["commit"] = (x["head_sha"]?.GetValue<string>() ?? "").Length >= 7 ? x["head_sha"].GetValue<string>().Substring(0, 7) : "",
                        ["mensaje"] = PrimeraLinea(x["head_commit"]?["message"]?.GetValue<string>()) });
            r["compilaciones"] = comp;
            JsonArray arch = new JsonArray();
            if (tArch.Result is JsonArray al) foreach (JsonNode x in al) arch.Add((JsonNode)new JsonObject { ["nombre"] = x["name"]?.GetValue<string>(), ["tamano"] = x["size"]?.GetValue<long>() });
            r["archivos"] = arch;
            r["veredictos"] = Resumen.Veredictos(r);
            return r;
        }
        private static string PrimeraLinea(string s) { if (s == null) return ""; int i = s.IndexOf('\n'); return i >= 0 ? s.Substring(0, i) : s; }

        internal Task<string> Registros() { return gh.Archivo(cfg.Buzon, "rocola/registros.json"); }
        internal Task<string> ArchivoSubido(string nombre) { return gh.Archivo(cfg.Buzon, "rocola/archivos/" + nombre); }

        // Compilar en GitHub (Actions -> Run workflow): «programa» (unos 5 min) o «sistema» (cerca de una hora).
        internal async Task<string> Compila(string cual)
        {
            string wf = cual == "sistema" ? "rocola-sistema.yml" : cual == "casa" ? "rocola-casa.yml" : "rocola-programa.yml";
            var (c2, b2) = await gh.Pide(HttpMethod.Post, "/repos/" + cfg.Repo + "/actions/workflows/" + wf + "/dispatches", new JsonObject { ["ref"] = cfg.Rama }.ToJsonString());
            return c2 == 204 ? null : GitHub.Error(c2, b2);
        }

        // Comprobar la llave: si alcanza el repositorio de la rocola y el buzon, y con que permisos.
        internal async Task<JsonObject> Comprueba()
        {
            JsonObject r = new JsonObject();
            foreach (var (k, repo) in new[] { ("rocola", cfg.Repo), ("buzon", cfg.Buzon) })
            {
                var (c, b) = await gh.Pide(HttpMethod.Get, "/repos/" + repo);
                string que = c == 200 ? "se puede leer" : GitHub.Error(c, b);
                if (c == 200)
                    try
                    {
                        JsonNode p = JsonNode.Parse(b)["permissions"];
                        if (p != null) que += p["push"]?.GetValue<bool>() == true ? " y escribir" : " (solo leer)";
                    }
                    catch { }
                r[k] = repo + ": " + que;
            }
            var (ca, ba) = await gh.Pide(HttpMethod.Get, "/repos/" + cfg.Repo + "/actions/workflows");
            r["compilar"] = ca == 200 ? "se ven las compilaciones" : "no se ven las compilaciones: " + GitHub.Error(ca, ba);
            return r;
        }

        // El encendido remoto: el «paquete magico» (6 x FF y 16 veces la MAC) por UDP a la direccion que reenvia el router del bar.
        internal static string Despierta(string host, int puerto, string mac)
        {
            string m = (mac ?? "").Replace(":", "").Replace("-", "").Trim();
            if (m.Length != 12) return "falta la MAC de la rocola (sale en el estado, o ponla en Ajustes)";
            if (string.IsNullOrWhiteSpace(host)) return "falta la direccion del router del bar (Ajustes: un nombre DDNS o su IP publica)";
            byte[] b = new byte[6], p = new byte[102];
            for (int i = 0; i < 6; i++) b[i] = Convert.ToByte(m.Substring(2 * i, 2), 16);
            for (int i = 0; i < 6; i++) p[i] = 0xFF;
            for (int i = 1; i <= 16; i++) Array.Copy(b, 0, p, 6 * i, 6);
            try
            {
                using (UdpClient u = new UdpClient())
                {
                    u.EnableBroadcast = true;
                    for (int i = 0; i < 3; i++) u.Send(p, p.Length, host.Trim(), puerto);
                }
                return null;
            }
            catch (Exception ex) { return "no se pudo mandar: " + ex.Message; }
        }
    }
}
