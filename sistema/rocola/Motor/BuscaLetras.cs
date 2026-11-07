// BUSCAR UNA CANCION POR SU LETRA (decision del dueno del 7-11-2026: «L1 y L3»). Solo Rocola OS. Se busca con una frase (4 palabras o mas)
// en el buscador de siempre:
//  L3  LA LETRA METIDA EN TUS MP3 (automatico): un repaso de fondo, una vez por archivo, lee la letra que algunos MP3 traen dentro (el
//      marco USLT del ID3) y guarda solo sus PALABRAS sueltas, como suenan (rocola_letras_mp3.txt: ruta, tamano, fecha y las palabras; no
//      la letra). Una frase cuyas palabras esten casi todas en la letra de una cancion la encuentra (debajo de lo exacto).
//  L1  LA LETRA EN INTERNET (busca_letra=, encendido; necesita la llave de Genius en rocola_genius.txt): al dejar de escribir una frase,
//      se pregunta a Genius (api.genius.com/search) de que canciones es. Si la tienes, sale TU cancion; si no y Spotify esta encendido, la
//      de Spotify; si no, una tarjeta que dice cual es («no esta en la rocola»). Nada se guarda; va con curl (como las actualizaciones).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        public bool BuscaLetra = true;   // «busca_letra=» L1 (sin la llave de Genius no hace nada)
        public const int PalabrasDeFrase = 4;   // desde cuantas palabras escritas se busca por la letra
        internal static string ArchivoGenius { get { return Path.Combine(CarpetaDatos, "rocola_genius.txt"); } }
        public bool HayLlaveGenius { get { try { return File.Exists(ArchivoGenius) && new FileInfo(ArchivoGenius).Length > 10; } catch { return false; } } }
        // Cuantas palabras tiene lo escrito (para saber si es una frase).
        public static int Palabras(string texto)
        {
            int n = 0; bool en = false;
            foreach (char c in texto ?? "") { if (char.IsLetterOrDigit(c)) { if (!en) n++; en = true; } else en = false; }
            return n;
        }

        // ================= L3: LA LETRA DE TUS MP3 =================
        private sealed class LetraMp3 { public long Tam, Fecha; public string[] Palabras; }
        private readonly Dictionary<string, LetraMp3> letrasMp3 = new Dictionary<string, LetraMp3>(StringComparer.Ordinal);
        private Dictionary<string, List<SearchItem>> palabrasLetra;   // palabra (como suena) -> canciones con esa palabra en su letra
        private Thread repasoLetras;
        private string ArchivoLetrasMp3 { get { return Path.Combine(CarpetaDatos, "rocola_letras_mp3.txt"); } }
        private static readonly bool SinPausaLetras = Environment.GetEnvironmentVariable("ROCOLA_LETRAS_SIN_PAUSA") == "1";   // (pruebas)
        public void EsperaLetrasMp3() { Thread t = repasoLetras; if (t != null) t.Join(); }   // (pruebas: --busca)

        private void LeeLetrasMp3()
        {
            try
            {
                if (!File.Exists(ArchivoLetrasMp3)) return;
                string[] lineas = File.ReadAllLines(ArchivoLetrasMp3);
                foreach (string l in lineas)
                {
                    string[] c = l.Split('\t');
                    if (c.Length < 4) continue;
                    LetraMp3 x = new LetraMp3();
                    long.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Tam);
                    long.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Fecha);
                    x.Palabras = c[3].Length == 0 ? null : c[3].Split(' ');
                    lock (letrasMp3) letrasMp3[c[0]] = x;
                }
                // SE COMPACTA (7-11-2026): el repaso solo anade lineas, asi que cada MP3 que cambia deja la suya de antes. Si sobra mas
                // de un tercio (y al menos 200 lineas), se reescribe con la ultima de cada archivo (no se mira si existe: con el disco de
                // la musica aun sin montar se perderia todo). Se escribe aparte y se cambia de un golpe (sin perder el archivo si falla).
                int vale; lock (letrasMp3) vale = letrasMp3.Count;
                if (lineas.Length - vale >= 200 && lineas.Length > vale * 3 / 2)
                {
                    StringBuilder sb = new StringBuilder();
                    lock (letrasMp3)
                        foreach (KeyValuePair<string, LetraMp3> kv in letrasMp3)
                        {
                            sb.Append(kv.Key).Append('\t').Append(kv.Value.Tam.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(kv.Value.Fecha.ToString(CultureInfo.InvariantCulture))
                              .Append('\t').Append(kv.Value.Palabras == null ? "" : string.Join(" ", kv.Value.Palabras)).Append('\n');
                        }
                    string tmp = ArchivoLetrasMp3 + ".nuevo";
                    File.WriteAllText(tmp, sb.ToString());
                    File.Move(tmp, ArchivoLetrasMp3, true);
                }
            }
            catch (Exception ex) { LogError("rocola_letras_mp3.txt", ex); }
        }
        // El repaso: los MP3 de la biblioteca que aun no se miraron (o cambiaron), uno cada 20 ms, con prioridad baja.
        private void ArrancaLetrasMp3()
        {
            if (repasoLetras != null && repasoLetras.IsAlive) return;
            repasoLetras = new Thread(delegate ()
            {
                try
                {
                    if (letrasMp3.Count == 0) LeeLetrasMp3();
                    List<string> rutas = new List<string>();
                    lock (tracks) foreach (Track t in tracks) if (t.Path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) rutas.Add(t.Path);
                    StringBuilder nuevas = new StringBuilder(); int conLetra = 0, vistas = 0;
                    foreach (string r in rutas)
                    {
                        if (cerrandoEntreno) return;
                        FileInfo fi; try { fi = new FileInfo(r); if (!fi.Exists) continue; } catch { continue; }
                        long tam = fi.Length, fecha = fi.LastWriteTimeUtc.Ticks;
                        LetraMp3 ya; lock (letrasMp3) letrasMp3.TryGetValue(r, out ya);
                        if (ya != null && ya.Tam == tam && ya.Fecha == fecha) continue;
                        string letra = null;
                        try { letra = LetraDelMp3(r); } catch { }
                        string[] pal = PalabrasDeLetra(letra);
                        LetraMp3 x = new LetraMp3 { Tam = tam, Fecha = fecha, Palabras = pal };
                        lock (letrasMp3) letrasMp3[r] = x;
                        nuevas.Append(r.Replace('\t', ' ')).Append('\t').Append(tam.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(fecha.ToString(CultureInfo.InvariantCulture))
                              .Append('\t').Append(pal == null ? "" : string.Join(" ", pal)).Append('\n');
                        vistas++; if (pal != null) conLetra++;
                        if (nuevas.Length > 64 * 1024) { try { File.AppendAllText(ArchivoLetrasMp3, nuevas.ToString()); } catch { } nuevas.Clear(); }
                        if (!SinPausaLetras) Thread.Sleep(20);
                        while (PausaRepaso && !cerrandoEntreno) Thread.Sleep(1000);   // (mientras suena un video, como el repaso)
                    }
                    if (nuevas.Length > 0) try { File.AppendAllText(ArchivoLetrasMp3, nuevas.ToString()); } catch { }
                    if (vistas > 0) Console.WriteLine("Letras de los MP3: " + vistas + " mirados, " + conLetra + " con letra");
                    IndiceLetras();
                }
                catch (Exception ex) { LogError("Letras de los MP3", ex); }
            }) { IsBackground = true, Name = "letras de los MP3", Priority = ThreadPriority.Lowest };
            repasoLetras.Start();
        }
        // Las palabras distintas de una letra, como suenan (sin las vacias ni las de una letra).
        private static string[] PalabrasDeLetra(string letra)
        {
            if (string.IsNullOrWhiteSpace(letra)) return null;
            string f = Fonetica.De(MotorBusqueda.Normaliza(letra));
            HashSet<string> s = new HashSet<string>(StringComparer.Ordinal);
            foreach (string w in f.Split(' ')) if (w.Length >= 2 && !Fonetica.EsVacia(w)) s.Add(w);
            if (s.Count < 5) return null;   // (no es una letra: un comentario corto)
            string[] a = new string[s.Count]; s.CopyTo(a);
            return a;
        }
        // El indice de las letras: palabra -> canciones (las del indice del buscador con esa ruta).
        private void IndiceLetras(bool desdeElIndice = false)
        {
            if (libBuilding && !desdeElIndice) return;   // (mientras se arma el indice, lo rehace el mismo al acabar: IndicePalabras)
            List<SearchItem> idx; try { idx = new List<SearchItem>(searchIndex); } catch { return; }
            Dictionary<string, List<SearchItem>> d = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal);
            foreach (SearchItem si in idx)
            {
                if (si.Kind != 2 || si.TrackRef == null) continue;
                LetraMp3 x; lock (letrasMp3) letrasMp3.TryGetValue(si.TrackRef.Path, out x);
                if (x == null || x.Palabras == null) continue;
                foreach (string w in x.Palabras) { if (!d.TryGetValue(w, out List<SearchItem> l)) { l = new List<SearchItem>(2); d[w] = l; } l.Add(si); }
            }
            palabrasLetra = d.Count > 0 ? d : null;
        }
        // En el buscador: una frase cuyas palabras estan casi todas (3 de cada 4) en la letra de una cancion. Puntos 650.
        private void BuscaEnLetrasMp3(string q, List<SearchItem> matches)
        {
            Dictionary<string, List<SearchItem>> d = palabrasLetra;
            if (d == null || Palabras(q) < PalabrasDeFrase) return;
            List<string> toks = new List<string>();
            foreach (string w in Fonetica.De(q).Split(' ')) if (w.Length >= 2 && !Fonetica.EsVacia(w) && !toks.Contains(w)) toks.Add(w);
            if (toks.Count < 2) return;
            int hacen = Math.Max(2, (toks.Count * 3 + 3) / 4), sello = selloBusqueda;
            Dictionary<SearchItem, int> cuenta = new Dictionary<SearchItem, int>();
            for (int t = 0; t < toks.Count; t++)
            {
                if (!d.TryGetValue(toks[t], out List<SearchItem> l))
                {   // la ultima palabra puede ir a medio escribir: la que empiece asi
                    if (t != toks.Count - 1 || toks[t].Length < 3) continue;
                    l = null;
                    foreach (KeyValuePair<string, List<SearchItem>> kv in d) if (kv.Key.StartsWith(toks[t], StringComparison.Ordinal)) { l = kv.Value; break; }
                    if (l == null) continue;
                }
                foreach (SearchItem si in l) { cuenta.TryGetValue(si, out int c); cuenta[si] = c + 1; }
            }
            foreach (KeyValuePair<SearchItem, int> kv in cuenta)
            {
                if (kv.Value < hacen) continue;
                SearchItem si = kv.Key;
                int sc = 600 + 50 * kv.Value / toks.Count + Popular(si);
                if (si.Marca == sello) { if (sc > si.Score) si.Score = sc; continue; }   // (ya estaba: que suba)
                si.Score = sc; si.Marca = sello; matches.Add(si);
            }
        }
        // La letra que trae el MP3 (marco USLT del ID3v2.3 o 2.4), o null.
        internal static string LetraDelMp3(string ruta)
        {
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
            {
                byte[] cab = new byte[10];
                if (fs.Read(cab, 0, 10) != 10 || cab[0] != 'I' || cab[1] != 'D' || cab[2] != '3' || cab[3] < 3 || cab[3] > 4) return null;
                int major = cab[3]; bool unsync = (cab[5] & 0x80) != 0;
                long size = ((cab[6] & 0x7f) << 21) | ((cab[7] & 0x7f) << 14) | ((cab[8] & 0x7f) << 7) | (cab[9] & 0x7f);
                long fin = Math.Min(fs.Length, 10 + size);
                if ((cab[5] & 0x40) != 0)
                {
                    byte[] eh = new byte[4]; if (fs.Read(eh, 0, 4) != 4) return null;
                    long es = major >= 4 ? ((eh[0] & 0x7f) << 21) | ((eh[1] & 0x7f) << 14) | ((eh[2] & 0x7f) << 7) | (eh[3] & 0x7f) : (eh[0] << 24) | (eh[1] << 16) | (eh[2] << 8) | eh[3];
                    fs.Position += major >= 4 ? es - 4 : es;
                }
                byte[] fh = new byte[10];
                while (fs.Position + 10 <= fin)
                {
                    if (fs.Read(fh, 0, 10) != 10 || fh[0] == 0) return null;
                    long fsz = major >= 4 ? ((fh[4] & 0x7f) << 21) | ((fh[5] & 0x7f) << 14) | ((fh[6] & 0x7f) << 7) | (fh[7] & 0x7f) : ((long)fh[4] << 24) | (fh[5] << 16) | (fh[6] << 8) | fh[7];
                    if (fsz <= 0 || fs.Position + fsz > fin) return null;
                    if (fh[0] == 'U' && fh[1] == 'S' && fh[2] == 'L' && fh[3] == 'T' && fsz <= 256 * 1024)
                    {
                        byte[] b = new byte[fsz]; int leidos = 0, r;
                        while (leidos < fsz && (r = fs.Read(b, leidos, (int)fsz - leidos)) > 0) leidos += r;
                        bool comprimido = major >= 4 ? (fh[9] & 0x0C) != 0 : (fh[9] & 0xC0) != 0;
                        if (comprimido) return null;
                        if (unsync || (major >= 4 && (fh[9] & 0x02) != 0)) b = SinUnsync(b);
                        int s = 0;
                        if (major >= 4 && (fh[9] & 0x01) != 0) s += 4;   // indicador de longitud
                        if (b.Length < s + 5) return null;
                        byte enc = b[s]; s += 4;   // codificacion y el idioma (3 letras)
                        // la descripcion, hasta su terminador
                        if (enc == 1 || enc == 2) { while (s + 1 < b.Length && !(b[s] == 0 && b[s + 1] == 0)) s += 2; s += 2; }
                        else { while (s < b.Length && b[s] != 0) s++; s++; }
                        if (s >= b.Length) return null;
                        int n = b.Length - s;
                        switch (enc)
                        {
                            case 1:
                                if (n >= 2 && b[s] == 0xFF && b[s + 1] == 0xFE) return Encoding.Unicode.GetString(b, s + 2, n - 2);
                                if (n >= 2 && b[s] == 0xFE && b[s + 1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, s + 2, n - 2);
                                return Encoding.Unicode.GetString(b, s, n);
                            case 2: return Encoding.BigEndianUnicode.GetString(b, s, n);
                            case 3: return Encoding.UTF8.GetString(b, s, n);
                            default: return Encoding.Latin1.GetString(b, s, n);
                        }
                    }
                    fs.Position += fsz;
                }
            }
            return null;
        }
        private static byte[] SinUnsync(byte[] b)
        {
            List<byte> r = new List<byte>(b.Length);
            for (int i = 0; i < b.Length; i++) { r.Add(b[i]); if (b[i] == 0xFF && i + 1 < b.Length && b[i + 1] == 0x00) i++; }
            return r.ToArray();
        }

        // ================= L1: LA LETRA EN INTERNET (Genius) =================
        private string letraPedida, letraHecha;
        private List<Resultado> letraRes;
        private bool letraBuscando;
        private int letraDesde;
        // Lo escrito: se buscara cuando se deje de escribir 0,8 s (la pantalla pregunta con ResultadosPorLetra).
        public void PideBusquedaLetra(string texto)
        {
            lock (letrasMp3) { letraPedida = (texto ?? "").Trim(); letraDesde = Environment.TickCount; }
        }
        // Lo que dice Genius de esa frase, ya llevado a tu biblioteca o a Spotify (null si aun no esta o no toca).
        public List<Resultado> ResultadosPorLetra(string texto)
        {
            string t = (texto ?? "").Trim();
            if (!BuscaLetra || Palabras(t) < PalabrasDeFrase || !HayLlaveGenius) return null;
            lock (letrasMp3)
            {
                if (t == letraPedida && t != letraHecha && !letraBuscando && unchecked(Environment.TickCount - letraDesde) >= 800)
                {
                    letraBuscando = true;
                    List<SearchItem> foto = libBuilding ? new List<SearchItem>() : new List<SearchItem>(searchIndex);   // (aqui, en el hilo de la pantalla: dueno del indice)
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        List<Resultado> r = null;
                        try { r = BuscaLetraEnLinea(t, foto); } catch (Exception ex) { LogError("Buscar por la letra", ex); }
                        lock (letrasMp3) { letraRes = r ?? new List<Resultado>(); letraHecha = t; letraBuscando = false; }
                    });
                }
                return t == letraHecha ? letraRes : null;
            }
        }
        private List<Resultado> BuscaLetraEnLinea(string frase, List<SearchItem> foto)
        {
            List<Resultado> r = new List<Resultado>();
            string llave = File.ReadAllText(ArchivoGenius).Trim();
            if (llave.Length < 10) return r;
            string api = Environment.GetEnvironmentVariable("ROCOLA_GENIUS_API") is string e && e.Length > 0 ? e : "https://api.genius.com";   // (pruebas: un Genius simulado)
            string cuerpo = Curl(api + "/search?per_page=5&q=" + Uri.EscapeDataString(frase), llave);
            if (cuerpo == null) return r;
            HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
            using (JsonDocument d = JsonDocument.Parse(cuerpo))
            {
                if (!d.RootElement.TryGetProperty("response", out JsonElement resp) || !resp.TryGetProperty("hits", out JsonElement hits)) return r;
                foreach (JsonElement h in hits.EnumerateArray())
                {
                    if (r.Count >= 3) break;
                    if (!h.TryGetProperty("result", out JsonElement res)) continue;
                    string titulo = SpotifyEnVivo.Texto(res, "title"), artista = res.TryGetProperty("primary_artist", out JsonElement pa) ? SpotifyEnVivo.Texto(pa, "name") : "";
                    if (titulo.Length == 0 || !vistas.Add(Fonetica.De(MotorBusqueda.Normaliza(SinParentesis(titulo))) + "|" + artista)) continue;   // («Querida» y «Querida (En Vivo)»: una)
                    // ¿la tienes? la cancion de tu biblioteca con ese titulo (como suena, con una falta) y ese artista
                    SearchItem tuya = TuCancion(foto, titulo, artista);
                    if (tuya != null)
                    {
                        Track tr = tuya.TrackRef;
                        if (!vistas.Add("ruta|" + tr.Path)) continue;   // (la misma cancion tuya por dos de Genius: la de estudio y la en vivo)
                        r.Add(new Resultado { Tipo = 2, Ruta = tr.Path, Texto = TituloSinPista(string.IsNullOrEmpty(tr.Title) ? titulo : tr.Title), Detalle = "Por la letra · " + (tuya.AlbumRef != null ? tuya.AlbumRef.Artist : artista), Carpeta = tuya.AlbumRef != null ? tuya.AlbumRef.Folder : null, Portada = tuya.AlbumRef != null ? tuya.AlbumRef.CoverPath : null });
                        continue;
                    }
                    // si no, la de Spotify (con el modo encendido)
                    Resultado sp = SpotifyPorTitulo(titulo, artista);
                    if (sp != null) { if (vistas.Add("ruta|" + sp.Ruta)) r.Add(sp); continue; }
                    r.Add(new Resultado { Tipo = 5, Texto = titulo, Detalle = "Por la letra · " + artista + " · no está en la rocola" });
                }
            }
            return r;
        }
        private static string SinParentesis(string s)
        {
            StringBuilder b = new StringBuilder(); int p = 0;
            foreach (char c in s) { if (c == '(' || c == '[') p++; else if (c == ')' || c == ']') { if (p > 0) p--; } else if (p == 0) b.Append(c); }
            return b.ToString();
        }
        private static SearchItem TuCancion(List<SearchItem> foto, string titulo, string artista)
        {
            string ft = Fonetica.SinEspacios(Fonetica.De(MotorBusqueda.Normaliza(SinParentesis(titulo))));
            string[] fa = Fonetica.De(MotorBusqueda.Normaliza(artista)).Split(' ');
            if (ft.Length < 2) return null;
            int tol = Fonetica.Tolerancia(ft.Length);
            SearchItem mejor = null; int mejorPts = 0;
            foreach (SearchItem si in foto)
            {
                if (si.Kind != 2 || si.TrackRef == null || si.Key == null || si.TitleLength <= 0) continue;
                string tk = Fonetica.SinEspacios(Fonetica.De(SinParentesis(si.Key.Substring(si.TitleAt, si.TitleLength))));
                if (tk.Length == 0 || Math.Abs(tk.Length - ft.Length) > tol) continue;
                int dt = tk == ft ? 0 : Fonetica.Distancia(ft, ft.Length, tk, tk.Length, tol);
                if (dt > tol) continue;
                int pts = 10 - dt * 3;
                if (si.Palabras != null) foreach (string w in fa) if (w.Length >= 2 && !Fonetica.EsVacia(w) && Array.IndexOf(si.Palabras, w) >= 0) { pts += 10; break; }   // el artista tambien
                if (pts > mejorPts) { mejorPts = pts; mejor = si; }
            }
            return mejorPts >= 15 || (mejorPts >= 10 && fa.Length == 0) ? mejor : null;   // titulo y artista (o el titulo exacto si no hay artista)
        }
        private Resultado SpotifyPorTitulo(string titulo, string artista)
        {
            SpotifyEnVivo s = spotify;
            if (!SpotifyListo || s == null) return null;
            using (JsonDocument d = Api(s, "/v1/search?type=track&limit=1&q=" + Q("track:" + SinParentesis(titulo) + " artist:" + artista)))
                if (d != null && d.RootElement.TryGetProperty("tracks", out JsonElement tr) && tr.TryGetProperty("items", out JsonElement ti))
                    foreach (JsonElement t in ti.EnumerateArray())
                    {
                        string ruta = ApuntaPista(t, null, null, null); if (ruta == null) continue;
                        PistaSpotify p; lock (spotifyCerrojo) p = spotifyPistas[ruta];
                        // que de verdad sea esa (Spotify a veces da otra parecida): el titulo, como suena, con poca falta
                        string a = Fonetica.SinEspacios(Fonetica.De(MotorBusqueda.Normaliza(SinParentesis(titulo)))), b = Fonetica.SinEspacios(Fonetica.De(MotorBusqueda.Normaliza(SinParentesis(p.Titulo))));
                        int tol = Fonetica.Tolerancia(a.Length);
                        if (a.Length == 0 || (a != b && Fonetica.Distancia(a, a.Length, b, b.Length, tol) > tol)) return null;
                        Resultado x = new Resultado { Tipo = 2, Ruta = ruta, Texto = p.Titulo, Detalle = "Por la letra · Spotify · " + p.Artista, Carpeta = p.DiscoCarpeta };
                        if (p.PortadaUrl != null) x.Portada = BajaPortada(s, p.PortadaUrl, p.DiscoCarpeta ?? p.Id);
                        return x;
                    }
            return null;
        }
        // Una consulta por curl (el programa no lleva su propia pila de internet; curl ya esta para las actualizaciones). null si falla.
        private static string Curl(string url, string llave)
        {
            ProcessStartInfo pi = new ProcessStartInfo("curl") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (string a in new[] { "-s", "-f", "--max-time", "8", "-K", "-", url }) pi.ArgumentList.Add(a);   // (la llave por la entrada: no se ve en la lista de procesos)
            using (Process p = Process.Start(pi))
            {
                p.StandardInput.WriteLine("header = \"Authorization: Bearer " + llave.Replace("\"", "") + "\"");
                p.StandardInput.WriteLine("header = \"User-Agent: rocola\"");
                p.StandardInput.Close();
                string o = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return null; }
                return p.ExitCode == 0 && o.Length > 0 ? o : null;
            }
        }
    }
}
