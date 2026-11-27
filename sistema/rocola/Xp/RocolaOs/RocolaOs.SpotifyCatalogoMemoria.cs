// LA MEMORIA EN DISCO DE LO QUE CONTESTA SPOTIFY (Rocola OS, modo Spotify; mejora que el dueno eligio el 27-11-2026: «recordar en disco lo que
// contesto Spotify [...] para abrir mas rapido y gastar menos cuota tambien tras apagar»; FASE11_paredes-buscador.md, ultima seccion).
//
// Hasta hoy lo contestado vivia solo en memoria (RocolaOs.SpotifyCatalogoApi.cs: busquedas, discos y paginas con tope): al apagar la rocola, lo
// pedido otra vez (una busqueda, un disco, la pared de un artista) volvia a gastar cuota de la API web (modo desarrollo de 2026) y a esperar la
// red. Ahora cada contestacion de ESTOS cuatro tipos tambien se guarda en un archivo de texto chico, y el hilo de la API la lee del disco antes
// de preguntar a Spotify:
//   busqueda   lo que contesta /search (canciones, discos y artistas, 10 de cada)    caduca a las 12 h
//   pared_todos   una pagina de «Todos los discos de...» (10 discos)                  12 h
//   pared_canciones   una pagina de «Canciones de ARTISTA» (10 canciones)            2 dias
//   pared_artista  una pagina de los discos de un artista (10 discos)                 3 dias
//   disco      un disco de Spotify con sus canciones                                  30 dias
// (un disco casi no cambia; las busquedas y lo de un artista sí, poco). Vencido pero sin red (o con Spotify frenando: 429), se sirve igual
// (hasta 7 veces la caducidad): mejor lo de ayer que nada. Lo de la CUENTA (tus listas, favoritas, lo mas escuchado, lo reciente), las parejas de
// la sustitucion y las llaves NUNCA se guardan aqui: solo datos publicos de la API (ids, nombres, numeros, direcciones de portadas).
//
// DONDE: en los datos grandes de la rocola (Nativo/DatosGrandes.cs: ROCOLA_IA_DATOS, el disco de la musica) en «rocola_spotify_cache/»,
// NUNCA en /rocola/datos (512 MB); sin ese disco aparte (o con menos de 256 MB libres) la memoria en disco no se usa y todo sigue como antes.
// TOPE: 48 MB y 6000 archivos; al arrancar el hilo de la API (y cada 6 h) se borran los vencidos del todo (mas de 7 veces su caducidad) y, si
// pasa del tope, los mas viejos primero. La fecha de lo guardado es la del archivo.
// FORMATO: texto, una linea por cosa, campos con tabuladores (los tabuladores y saltos dentro de un campo van escapados); la primera linea
// lleva la clave exacta (si el nombre del archivo, un hash de la clave, chocara con otra, no se usa). Un archivo que no se entiende se borra.
// La hoja «Spotify» de Ctrl+F4 muestra cuanto se acerto, cuanto pesa y puede vaciarla.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static class RocolaOsSpotifyMemoria
    {
        internal const string Subcarpeta = "rocola_spotify_cache";
        internal const long TopeBytes = 48L * 1024 * 1024;
        internal const int TopeArchivos = 6000;
        internal const int VencidoVecesMax = 7;       // servido aun vencido, hasta tantas veces su caducidad (sin red o frenada)
        private const long LimpiaCadaMs = 6L * 3600 * 1000;
        internal const string TBusqueda = "busqueda", TDisco = "disco";

        // cuanto vale lo guardado de cada tipo (horas)
        internal static double CaducaHoras(string tipo)
        {
            switch (tipo)
            {
                case TBusqueda: return 12;
                case RocolaOsSpotifyCatalogoApi.ParedTodos: return 12;
                case RocolaOsSpotifyCatalogoApi.ParedCanciones: return 48;
                case RocolaOsSpotifyCatalogoApi.ParedArtista: return 72;
                case TDisco: return 24 * 30;
                default: return 0;
            }
        }

        private static readonly object cerrojo = new object();
        private static long aciertos, aciertosVencidos, fallos, guardadas, borradas;
        private static long totalBytes = -1; private static int totalArchivos = -1;   // (-1: aun sin sumar)
        private static long ultimaLimpieza = long.MinValue / 2;
        private static bool avisadoSinDisco;

        // la carpeta, o null si no se usa (sin disco aparte o sin sitio)
        private static string Carpeta()
        {
            if (!DatosGrandes.EnDiscoAparte || DatosGrandes.SinSitio())
            {
                lock (cerrojo) if (!avisadoSinDisco) { avisadoSinDisco = true; RocolaOs.Apunta("spotify_catalogo", "memoria en disco: " + (DatosGrandes.EnDiscoAparte ? "poco sitio en el disco de la rocola" : "sin disco aparte (ROCOLA_IA_DATOS)") + ": no se guarda lo que contesta Spotify"); }
                return null;
            }
            return DatosGrandes.Ruta(Subcarpeta);
        }
        internal static bool Activa { get { return DatosGrandes.EnDiscoAparte; } }

        private static string Archivo(string carpeta, string tipo, string clave)
        {
            byte[] h;
            using (SHA1 sha = SHA1.Create()) h = sha.ComputeHash(Encoding.UTF8.GetBytes(clave));
            StringBuilder sb = new StringBuilder(tipo.Length + 22).Append(tipo).Append('-');
            for (int i = 0; i < 10; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
            return Path.Combine(carpeta, sb.ToString() + ".txt");
        }

        // ---------- escribir y leer lineas ----------
        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { '\\', '\t', '\n', '\r' }) < 0) return s;
            return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        private static string Des(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n);
            }
            return sb.ToString();
        }
        private static string NuloSiVacio(string s) { return string.IsNullOrEmpty(s) ? null : s; }
        private static string I(int n) { return n.ToString(CultureInfo.InvariantCulture); }

        private static void LineaCancion(StringBuilder sb, RocolaOsSpotifyCatalogoCancion c)
        {
            sb.Append("c\t").Append(Esc(c.Id)).Append('\t').Append(Esc(c.Uri)).Append('\t').Append(Esc(c.Titulo)).Append('\t').Append(Esc(c.Artista)).Append('\t')
              .Append(Esc(c.ArtistaIds != null ? string.Join("|", c.ArtistaIds) : "")).Append('\t').Append(Esc(c.Disco)).Append('\t').Append(Esc(c.DiscoId)).Append('\t')
              .Append(Esc(c.DiscoUri)).Append('\t').Append(Esc(c.PortadaUrl)).Append('\t').Append(I(c.DuracionMs)).Append('\t').Append(I(c.Numero)).Append('\n');
        }
        private static void LineaDisco(StringBuilder sb, string marca, RocolaOsSpotifyCatalogoDisco d)
        {
            sb.Append(marca).Append('\t').Append(Esc(d.Id)).Append('\t').Append(Esc(d.Uri)).Append('\t').Append(Esc(d.Nombre)).Append('\t').Append(Esc(d.Artista)).Append('\t')
              .Append(Esc(d.PortadaUrl)).Append('\t').Append(Esc(d.Tipo)).Append('\t').Append(I(d.NumCanciones)).Append('\n');
        }
        private static void LineaArtista(StringBuilder sb, RocolaOsSpotifyCatalogoArtista a)
        {
            sb.Append("a\t").Append(Esc(a.Id)).Append('\t').Append(Esc(a.Uri)).Append('\t').Append(Esc(a.Nombre)).Append('\t').Append(Esc(a.PortadaUrl)).Append('\n');
        }
        private static RocolaOsSpotifyCatalogoCancion LeeCancion(string[] f)
        {
            if (f.Length < 12) return null;
            int dur, num;
            int.TryParse(f[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out dur); int.TryParse(f[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out num);
            string ids = f[5];
            return new RocolaOsSpotifyCatalogoCancion
            {
                Id = f[1], Uri = f[2], Titulo = f[3], Artista = f[4], ArtistaIds = ids.Length > 0 ? ids.Split('|') : null, Disco = f[6], DiscoId = NuloSiVacio(f[7]),
                DiscoUri = NuloSiVacio(f[8]), PortadaUrl = NuloSiVacio(f[9]), DuracionMs = dur, Numero = num
            };
        }
        private static RocolaOsSpotifyCatalogoDisco LeeDisco(string[] f)
        {
            if (f.Length < 8 || f[1].Length == 0) return null;
            int n; int.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
            return new RocolaOsSpotifyCatalogoDisco { Id = f[1], Uri = f[2], Nombre = f[3], Artista = f[4], PortadaUrl = NuloSiVacio(f[5]), Tipo = f[6].Length > 0 ? f[6] : "album", NumCanciones = n };
        }

        // guarda (temporal y cambio de nombre) la entrada de un tipo y una clave
        private static void Guarda(string tipo, string clave, string cuerpo)
        {
            try
            {
                string carpeta = Carpeta();
                if (carpeta == null) return;
                Directory.CreateDirectory(carpeta);
                string ruta = Archivo(carpeta, tipo, clave), t = ruta + ".n";
                // (27-11-2026, revision) linea de cierre «fin<TAB>numero de lineas»: un archivo truncado por un apagon tras el cambio de nombre (sin fsync)
                // no la trae y Lee lo descarta; con ella sabe tambien que no falto ninguna linea
                int lineas = 0;
                for (int k = 0; k < cuerpo.Length; k++) if (cuerpo[k] == '\n') lineas++;
                string texto = "v1\t" + Esc(clave) + "\n" + cuerpo + "fin\t" + I(lineas) + "\n";
                long antes = File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
                File.WriteAllText(t, texto, new UTF8Encoding(false));
                File.Move(t, ruta, true);
                lock (cerrojo)
                {
                    guardadas++;
                    if (totalBytes >= 0) { totalBytes += texto.Length - antes; if (antes == 0) totalArchivos++; }
                }
            }
            catch (Exception ex) { RocolaOs.Apunta("spotify_catalogo", "memoria en disco: no se pudo guardar (" + ex.GetType().Name + ")"); }
        }
        // las lineas de la entrada (sin la de la clave), o null: sin archivo, vencido (y no «viejo»), de otra clave o que no se entiende
        private static List<string[]> Lee(string tipo, string clave, bool viejo)
        {
            try
            {
                string carpeta = Carpeta();
                if (carpeta == null) return null;
                string ruta = Archivo(carpeta, tipo, clave);
                FileInfo fi = new FileInfo(ruta);
                if (!fi.Exists) { lock (cerrojo) fallos++; return null; }
                double horas = (DateTime.UtcNow - fi.LastWriteTimeUtc).TotalHours, vale = CaducaHoras(tipo);
                if (horas < 0) horas = 0;
                if (horas > vale && (!viejo || horas > vale * VencidoVecesMax)) { lock (cerrojo) fallos++; return null; }
                string[] ls = File.ReadAllLines(ruta, Encoding.UTF8);
                if (ls.Length < 1 || ls[0] != "v1\t" + Esc(clave)) { lock (cerrojo) fallos++; if (ls.Length < 1 || !ls[0].StartsWith("v1\t", StringComparison.Ordinal)) TryBorra(ruta); return null; }
                // el cierre: ultima linea «fin<TAB>N» con N = las lineas de datos; sin el (archivo viejo o truncado) o con otra cuenta, no vale y se borra
                int cuerpoN = ls.Length - 2, cierre;
                if (ls.Length < 2 || !ls[ls.Length - 1].StartsWith("fin\t", StringComparison.Ordinal)
                    || !int.TryParse(ls[ls.Length - 1].Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out cierre) || cierre != cuerpoN)
                { lock (cerrojo) fallos++; TryBorra(ruta); return null; }
                List<string[]> r = new List<string[]>(ls.Length);
                for (int i = 1; i < ls.Length - 1; i++)
                {
                    if (ls[i].Length == 0) continue;
                    string[] f = ls[i].Split('\t');
                    for (int k = 0; k < f.Length; k++) f[k] = Des(f[k]);
                    r.Add(f);
                }
                lock (cerrojo) { if (horas > vale) aciertosVencidos++; else aciertos++; }
                return r;
            }
            catch (Exception ex) { RocolaOs.Apunta("spotify_catalogo", "memoria en disco: no se pudo leer (" + ex.GetType().Name + ")"); return null; }
        }
        private static void TryBorra(string ruta) { try { File.Delete(ruta); } catch { } }

        // ---------- las cuatro clases de cosas ----------
        internal static bool TieneBusqueda(string clave)
        {
            try
            {
                if (!DatosGrandes.EnDiscoAparte) return false;
                string carpeta = DatosGrandes.Ruta(Subcarpeta);
                FileInfo fi = new FileInfo(Archivo(carpeta, TBusqueda, clave));
                return fi.Exists && (DateTime.UtcNow - fi.LastWriteTimeUtc).TotalHours <= CaducaHoras(TBusqueda);
            }
            catch { return false; }
        }
        internal static void GuardaBusqueda(string clave, RocolaOsSpotifyCatalogoBusqueda b)
        {
            if (b == null || !DatosGrandes.EnDiscoAparte) return;
            StringBuilder sb = new StringBuilder();
            foreach (RocolaOsSpotifyCatalogoCancion c in b.Canciones) LineaCancion(sb, c);
            foreach (RocolaOsSpotifyCatalogoDisco d in b.Discos) LineaDisco(sb, "d", d);
            foreach (RocolaOsSpotifyCatalogoArtista a in b.Artistas) LineaArtista(sb, a);
            Guarda(TBusqueda, clave, sb.ToString());
        }
        internal static RocolaOsSpotifyCatalogoBusqueda Busqueda(string clave, string consulta, bool viejo)
        {
            List<string[]> ls = Lee(TBusqueda, clave, viejo);
            if (ls == null) return null;
            RocolaOsSpotifyCatalogoBusqueda b = new RocolaOsSpotifyCatalogoBusqueda { Consulta = consulta };
            foreach (string[] f in ls)
            {
                if (f[0] == "c") { RocolaOsSpotifyCatalogoCancion c = LeeCancion(f); if (c != null) b.Canciones.Add(c); }
                else if (f[0] == "d") { RocolaOsSpotifyCatalogoDisco d = LeeDisco(f); if (d != null) b.Discos.Add(d); }
                else if (f[0] == "a" && f.Length >= 5 && f[1].Length > 0) b.Artistas.Add(new RocolaOsSpotifyCatalogoArtista { Id = f[1], Uri = f[2], Nombre = f[3], PortadaUrl = NuloSiVacio(f[4]) });
            }
            return b;
        }
        internal static void GuardaDisco(string id, RocolaOsSpotifyCatalogoDisco d)
        {
            if (d == null || d.Canciones == null || d.Canciones.Count == 0 || !DatosGrandes.EnDiscoAparte) return;
            StringBuilder sb = new StringBuilder();
            LineaDisco(sb, "D", d);
            foreach (RocolaOsSpotifyCatalogoCancion c in d.Canciones) LineaCancion(sb, c);
            Guarda(TDisco, id, sb.ToString());
        }
        internal static RocolaOsSpotifyCatalogoDisco Disco(string id, bool viejo)
        {
            List<string[]> ls = Lee(TDisco, id, viejo);
            if (ls == null || ls.Count == 0 || ls[0][0] != "D") return null;
            RocolaOsSpotifyCatalogoDisco d = LeeDisco(ls[0]);
            if (d == null) return null;
            d.Canciones = new List<RocolaOsSpotifyCatalogoCancion>();
            for (int i = 1; i < ls.Count; i++) if (ls[i][0] == "c") { RocolaOsSpotifyCatalogoCancion c = LeeCancion(ls[i]); if (c != null) d.Canciones.Add(c); }
            return d.Canciones.Count > 0 ? d : null;
        }
        // una pagina de pared: «clave» es la de la memoria («tipo\ndesde\nde»)
        private static string TipoDePagina(string clave) { int i = clave.IndexOf('\n'); return i < 0 ? "" : clave.Substring(0, i); }
        internal static void GuardaPagina(string clave, RocolaOsSpotifyCatalogoPagina pg)
        {
            if (pg == null || !DatosGrandes.EnDiscoAparte) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("p\t").Append(I(pg.Offset)).Append('\t').Append(I(pg.Total)).Append('\t').Append(I(pg.Crudos)).Append('\n');
            foreach (RocolaOsSpotifyCatalogoDisco d in pg.Discos) LineaDisco(sb, "d", d);
            foreach (RocolaOsSpotifyCatalogoCancion c in pg.Canciones) LineaCancion(sb, c);
            Guarda(TipoDePagina(clave), clave, sb.ToString());
        }
        internal static RocolaOsSpotifyCatalogoPagina Pagina(string clave, bool viejo)
        {
            List<string[]> ls = Lee(TipoDePagina(clave), clave, viejo);
            if (ls == null || ls.Count == 0 || ls[0][0] != "p" || ls[0].Length < 4) return null;
            RocolaOsSpotifyCatalogoPagina pg = new RocolaOsSpotifyCatalogoPagina();
            int o, t, cr;
            int.TryParse(ls[0][1], NumberStyles.Integer, CultureInfo.InvariantCulture, out o); int.TryParse(ls[0][2], NumberStyles.Integer, CultureInfo.InvariantCulture, out t);
            int.TryParse(ls[0][3], NumberStyles.Integer, CultureInfo.InvariantCulture, out cr);
            pg.Offset = o; pg.Total = t; pg.Crudos = cr;
            for (int i = 1; i < ls.Count; i++)
            {
                if (ls[i][0] == "d") { RocolaOsSpotifyCatalogoDisco d = LeeDisco(ls[i]); if (d != null) pg.Discos.Add(d); }
                else if (ls[i][0] == "c") { RocolaOsSpotifyCatalogoCancion c = LeeCancion(ls[i]); if (c != null) pg.Canciones.Add(c); }
            }
            return pg;
        }

        // ---------- limpiar y vaciar ----------
        // (en el hilo de la API, al arrancar y cada 6 h) borra los vencidos del todo y, si pasa del tope, los mas viejos
        internal static void Limpia()
        {
            long ahora = Environment.TickCount64;
            lock (cerrojo) { if (ahora - ultimaLimpieza < LimpiaCadaMs) return; ultimaLimpieza = ahora; }
            try
            {
                if (!DatosGrandes.EnDiscoAparte) return;
                string carpeta = DatosGrandes.Ruta(Subcarpeta);
                if (!Directory.Exists(carpeta)) { lock (cerrojo) { totalBytes = 0; totalArchivos = 0; } return; }
                List<FileInfo> l = new List<FileInfo>();
                long suma = 0; int quitadas = 0;
                DateTime ya = DateTime.UtcNow;
                foreach (FileInfo fi in new DirectoryInfo(carpeta).GetFiles())
                {
                    string n = fi.Name; int g = n.IndexOf('-');
                    double vale = g > 0 ? CaducaHoras(n.Substring(0, g)) : 0;
                    bool temporal = n.EndsWith(".n", StringComparison.Ordinal) && (ya - fi.LastWriteTimeUtc).TotalMinutes > 10;
                    if (vale <= 0 || temporal || (ya - fi.LastWriteTimeUtc).TotalHours > vale * VencidoVecesMax) { TryBorra(fi.FullName); quitadas++; continue; }
                    l.Add(fi); suma += fi.Length;
                }
                if (suma > TopeBytes || l.Count > TopeArchivos)
                {
                    l.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                    int i = 0;
                    while (i < l.Count && (suma > TopeBytes * 9 / 10 || l.Count - i > TopeArchivos * 9 / 10)) { suma -= l[i].Length; TryBorra(l[i].FullName); quitadas++; i++; }
                    l.RemoveRange(0, i);
                }
                lock (cerrojo) { totalBytes = suma; totalArchivos = l.Count; borradas += quitadas; }
                if (quitadas > 0) RocolaOs.Apunta("spotify_catalogo", "memoria en disco: se borraron " + quitadas + " archivos vencidos o de mas (quedan " + l.Count + ", " + (suma / 1024) + " KB)");
            }
            catch (Exception ex) { RocolaOs.Apunta("spotify_catalogo", "memoria en disco: no se pudo limpiar (" + ex.GetType().Name + ")"); }
        }
        // «Vaciar la cache» de la hoja: todo lo guardado (nada mas); devuelve cuantos archivos se borraron
        internal static int Vacia()
        {
            int n = 0;
            try
            {
                if (!DatosGrandes.EnDiscoAparte) return 0;
                string carpeta = DatosGrandes.Ruta(Subcarpeta);
                if (Directory.Exists(carpeta))
                    foreach (FileInfo fi in new DirectoryInfo(carpeta).GetFiles()) { try { fi.Delete(); n++; } catch { } }
                lock (cerrojo) { totalBytes = 0; totalArchivos = 0; borradas += n; }
                RocolaOs.Apunta("spotify_catalogo", "memoria en disco: vaciada desde la hoja Spotify (" + n + " archivos)");
            }
            catch (Exception ex) { RocolaOs.Apunta("spotify_catalogo", "memoria en disco: no se pudo vaciar (" + ex.GetType().Name + ")"); }
            return n;
        }

        // ---------- lo que se sabe (el estado del buzon y la hoja) ----------
        internal static void Numeros(out long aciertosDisco, out long vencidos, out long sinNada, out long escritas, out long bytes, out int archivos)
        {
            lock (cerrojo) { aciertosDisco = aciertos; vencidos = aciertosVencidos; sinNada = fallos; escritas = guardadas; bytes = totalBytes; archivos = totalArchivos; }
            if (bytes < 0 || archivos < 0)
            {   // (aun sin sumar: una vez, sin borrar nada; barato: unos cientos de archivos chicos)
                try
                {
                    long b = 0; int n = 0;
                    if (DatosGrandes.EnDiscoAparte)
                    {
                        string carpeta = DatosGrandes.Ruta(Subcarpeta);
                        if (Directory.Exists(carpeta)) foreach (FileInfo fi in new DirectoryInfo(carpeta).GetFiles()) { b += fi.Length; n++; }
                    }
                    lock (cerrojo) { totalBytes = b; totalArchivos = n; }
                    bytes = b; archivos = n;
                }
                catch { bytes = 0; archivos = 0; }
            }
        }
        internal static string TextoEstado()
        {
            long a, v, f, g, b; int n;
            Numeros(out a, out v, out f, out g, out b, out n);
            StringBuilder sb = new StringBuilder();
            sb.Append("cache_disco=").Append(DatosGrandes.EnDiscoAparte ? "si" : "no").Append('\n');
            sb.Append("cache_aciertos=").Append(a.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("cache_vencidas_servidas=").Append(v.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("cache_sin_nada=").Append(f.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("cache_guardadas=").Append(g.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("cache_archivos=").Append(n.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("cache_kb=").Append((b / 1024).ToString(CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }
    }
}
