// El servidor del tablero: un HTTP minimo SOLO en 127.0.0.1 (nadie mas en la red lo ve). Sin dependencias ni permisos de administrador
// (en Windows, un TcpListener no necesita reservar la direccion como HttpListener).
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace RocolaCasa
{
    internal sealed class Servidor
    {
        private Config cfg;
        private TcpListener oye;
        private readonly string clave = Guid.NewGuid().ToString("N");   // contra paginas de otros sitios que quieran mandar ordenes (CSRF)
        internal Servidor(Config cfg) { this.cfg = cfg; }

        internal int Arranca()
        {
            for (int p = cfg.Puerto; p < cfg.Puerto + 20; p++)
            {
                try { oye = new TcpListener(IPAddress.Loopback, p); oye.Start(); return p; }
                catch (SocketException) { oye = null; }
            }
            oye = new TcpListener(IPAddress.Loopback, 0); oye.Start();
            return ((IPEndPoint)oye.LocalEndpoint).Port;
        }
        internal async Task Atiende()
        {
            while (true)
            {
                TcpClient c = await oye.AcceptTcpClientAsync();
                _ = Task.Run(() => Uno(c));
            }
        }

        private async Task Uno(TcpClient c)
        {
            using (c)
            using (NetworkStream ns = c.GetStream())
            {
                try
                {
                    ns.ReadTimeout = 30000;
                    // la cabecera
                    MemoryStream cab = new MemoryStream(); uint ult = 0;
                    while (ult != 0x0D0A0D0A)   // hasta la linea en blanco (\r\n\r\n)
                    {
                        int b = ns.ReadByte(); if (b < 0) return;
                        cab.WriteByte((byte)b); ult = (ult << 8) | (uint)b;
                        if (cab.Length > 65536) return;
                    }
                    string[] lineas = Encoding.ASCII.GetString(cab.ToArray()).Split("\r\n");
                    string[] pr = lineas[0].Split(' ');
                    if (pr.Length < 2) return;
                    string metodo = pr[0], ruta = pr[1];
                    Dictionary<string, string> h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < lineas.Length; i++) { int k = lineas[i].IndexOf(':'); if (k > 0) h[lineas[i].Substring(0, k).Trim()] = lineas[i].Substring(k + 1).Trim(); }
                    string cuerpo = "";
                    if (h.TryGetValue("Content-Length", out string cl) && int.TryParse(cl, out int largo) && largo > 0 && largo < 4_000_000)
                    {
                        byte[] buf = new byte[largo]; int leidos = 0;
                        while (leidos < largo) { int n = await ns.ReadAsync(buf, leidos, largo - leidos); if (n <= 0) break; leidos += n; }
                        cuerpo = Encoding.UTF8.GetString(buf, 0, leidos);
                    }
                    // solo desde este mismo tablero: el Host tiene que ser 127.0.0.1 o localhost, y las ordenes llevan la clave de esta sesion
                    string host = h.TryGetValue("Host", out string hv) ? hv : "";
                    if (!host.StartsWith("127.0.0.1:") && !host.StartsWith("localhost:")) { await Responde(ns, 403, "text/plain", "no"); return; }
                    if (metodo == "POST" && (!h.TryGetValue("X-Clave", out string cv) || cv != clave)) { await Responde(ns, 403, "text/plain", "falta la clave de la sesion"); return; }
                    await Ruta(ns, metodo, ruta, cuerpo);
                }
                catch (Exception ex)
                {
                    try { await Responde(ns, 500, "application/json", new JsonObject { ["error"] = ex.Message }.ToJsonString()); } catch { }
                }
            }
        }

        private static string Param(string ruta, string k)
        {
            int q = ruta.IndexOf('?'); if (q < 0) return null;
            foreach (string par in ruta.Substring(q + 1).Split('&'))
            {
                int i = par.IndexOf('=');
                if (i > 0 && par.Substring(0, i) == k) return Uri.UnescapeDataString(par.Substring(i + 1).Replace('+', ' '));
            }
            return null;
        }
        private static string Camino(string ruta) { int q = ruta.IndexOf('?'); return q < 0 ? ruta : ruta.Substring(0, q); }
        private static string Ok(object o = null) { JsonObject r = new JsonObject { ["ok"] = true }; if (o is string s) r["texto"] = s; return r.ToJsonString(); }
        private static string Mal(string e) { return new JsonObject { ["ok"] = false, ["error"] = e }.ToJsonString(); }

        private async Task Ruta(NetworkStream ns, string metodo, string ruta, string cuerpo)
        {
            string c = Camino(ruta);
            Buzon b = new Buzon(cfg);
            JsonNode j = null; if (cuerpo.Length > 0) try { j = JsonNode.Parse(cuerpo); } catch { }
            switch (c)
            {
                case "/":
                    {
                        string html;
                        using (Stream s = typeof(Servidor).Assembly.GetManifestResourceStream("tablero.html"))
                        using (StreamReader r = new StreamReader(s)) html = r.ReadToEnd();
                        await Responde(ns, 200, "text/html; charset=utf-8", html.Replace("__CLAVE__", clave));
                        return;
                    }
                case "/api/todo":
                    if (!cfg.Lista) { await Responde(ns, 200, "application/json", new JsonObject { ["config"] = cfg.Publica(), ["sin_llave"] = true }.ToJsonString()); return; }
                    await Responde(ns, 200, "application/json", (await b.Todo()).ToJsonString());
                    return;
                case "/api/registros":
                    { string t = await b.Registros(); await Responde(ns, 200, "application/json", t ?? "{}"); return; }
                case "/api/archivo":
                    {
                        string n = Param(ruta, "n") ?? "";
                        if (n.Contains('/') || n.Contains("..")) { await Responde(ns, 400, "text/plain", "nombre no valido"); return; }
                        string t = await b.ArchivoSubido(n);
                        await Responde(ns, t != null ? 200 : 404, "text/plain; charset=utf-8", t ?? "Aun no esta en el buzon: pidelo con «Traer» y espera un minuto.");
                        return;
                    }
                case "/api/guarda":   // un archivo del buzon, a la carpeta RocolaCasa de esta computadora
                    {
                        string n = j?["nombre"]?.GetValue<string>() ?? "";
                        if (n.Length == 0 || n.Contains('/') || n.Contains('\\') || n.Contains("..")) { await Responde(ns, 200, "application/json", Mal("nombre no valido")); return; }
                        string t = n == "registros.json" ? await b.Registros() : n == "estado.json" ? await new GitHub(cfg.Llave).Archivo(cfg.Buzon, "rocola/estado.json") : await b.ArchivoSubido(n);
                        if (t == null) { await Responde(ns, 200, "application/json", Mal("no esta en el buzon")); return; }
                        Directory.CreateDirectory(Config.Descargas);
                        string dest = Path.Combine(Config.Descargas, DateTime.Now.ToString("yyyy-MM-dd_HHmm") + "_" + n);
                        await File.WriteAllTextAsync(dest, t);
                        await Responde(ns, 200, "application/json", Ok("Guardado en " + dest));
                        return;
                    }
                case "/api/orden":
                    {
                        string o = j?["orden"]?.GetValue<string>() ?? "", a = j?["arg"]?.GetValue<string>() ?? "";
                        string id = await b.Ordena(o, a);
                        await Responde(ns, 200, "application/json", id != null ? new JsonObject { ["ok"] = true, ["id"] = id }.ToJsonString() : Mal(b.UltimoError));
                        return;
                    }
                case "/api/compila":
                    {
                        string e = await b.Compila(j?["cual"]?.GetValue<string>() ?? "programa");
                        await Responde(ns, 200, "application/json", e == null ? Ok("Compilacion pedida a GitHub") : Mal(e));
                        return;
                    }
                case "/api/despierta":
                    {
                        string mac = j?["mac"]?.GetValue<string>(); if (string.IsNullOrWhiteSpace(mac)) mac = cfg.Mac;
                        string e = Buzon.Despierta(cfg.WolHost, cfg.WolPuerto, mac);
                        await Responde(ns, 200, "application/json", e == null ? Ok("Paquete magico mandado a " + cfg.WolHost + ":" + cfg.WolPuerto) : Mal(e));
                        return;
                    }
                case "/api/comprueba":
                    await Responde(ns, 200, "application/json", (await b.Comprueba()).ToJsonString());
                    return;
                case "/api/config":
                    if (metodo == "POST" && j != null)
                    {
                        string llave = j["llave"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(llave)) cfg.Llave = llave.Trim();
                        cfg.Repo = Config.Txt(j, "repo", cfg.Repo).Trim(); cfg.Rama = Config.Txt(j, "rama", cfg.Rama).Trim(); cfg.Buzon = Config.Txt(j, "buzon", cfg.Buzon).Trim();
                        cfg.WolHost = Config.Txt(j, "wol_host", cfg.WolHost).Trim(); cfg.Mac = Config.Txt(j, "mac", cfg.Mac).Trim();
                        cfg.WolPuerto = Config.Num(j, "wol_puerto", cfg.WolPuerto);
                        cfg.Guarda();
                        await Responde(ns, 200, "application/json", Ok("Guardado en " + Config.Archivo));
                        return;
                    }
                    await Responde(ns, 200, "application/json", cfg.Publica().ToJsonString());
                    return;
                case "/api/abre-carpeta":
                    Directory.CreateDirectory(Config.Descargas);
                    Program.AbreNavegador(Config.Descargas);
                    await Responde(ns, 200, "application/json", Ok(Config.Descargas));
                    return;
            }
            await Responde(ns, 404, "text/plain", "no hay nada aqui");
        }

        private static async Task Responde(NetworkStream ns, int codigo, string tipo, string cuerpo)
        {
            byte[] b = Encoding.UTF8.GetBytes(cuerpo);
            string cab = "HTTP/1.1 " + codigo + " " + (codigo == 200 ? "OK" : "Error") + "\r\nContent-Type: " + tipo + "\r\nContent-Length: " + b.Length
                + "\r\nCache-Control: no-store\r\nX-Frame-Options: DENY\r\nConnection: close\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(cab);
            await ns.WriteAsync(h, 0, h.Length);
            await ns.WriteAsync(b, 0, b.Length);
        }
    }
}
