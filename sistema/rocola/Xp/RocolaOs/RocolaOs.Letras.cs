// BUSCAR UNA CANCION POR SU LETRA EN EL BUSCADOR DE XP (fase 11, grupo letras; FASE11_letras.md). Decision del dueno del 7-11-2026 («L1 y
// L3») hecha otra vez encima de rocola-xp (la del programa anterior, sistema/rocola/Motor/BuscaLetras.cs, es la referencia de la logica):
//   L3  LA LETRA DE TUS MP3 (funcion «letras_mp3», ENCENDIDA de fabrica, automatica): un repaso de fondo (RocolaOs.Hilo, nice 19, unos 20 ms
//       por archivo) lee el marco USLT del ID3v2 de cada MP3 de la biblioteca, UNA vez por archivo (y otra si cambia su tamano o su fecha), y
//       guarda SOLO SUS PALABRAS SUELTAS, normalizadas como las normaliza el buscador de XP (MotorBusqueda.Normaliza: sin acentos por
//       UnicodeXp, nunca string.Normalize), distintas y EN ORDEN ALFABETICO (no se puede rehacer la letra con ellas), sin las vacias ni las
//       de una letra: rocola_letras_mp3.txt de la carpeta de datos («ruta TAB tamano TAB fecha TAB palabras»). NUNCA la letra.
//       En el buscador: una frase de 4 palabras o mas que tenga 3 de cada 4 de sus palabras (sin las vacias) en la letra de una cancion la
//       encuentra, DEBAJO de todo lo de XP (lo de XP manda y sale en su orden; lo de aqui va al final, sin repetir lo que XP ya dio).
//   L1  GENIUS (funcion «busca_letra», APAGADA de fabrica: encenderla es decision del dueno): RocolaOs.LetrasGenius.cs.
// LOS GANCHOS (RocolaOs/GANCHOS.tsv):
//   g114 Codigo/Form1.Biblioteca.cs, RecomputeResults: «CuentaResultados(query, ...)»: antes de contar, RocolaOsLetrasResultados mete lo de
//        Genius delante y lo de la letra de tus MP3 detras en la misma lista de XP (res), y devuelve cuantos metio (la casilla los cuenta).
//        Va ANTES del gancho g40 de Spotify, que mezcla lo suyo sobre esta misma lista.
//   g115 Codigo/Form1.Biblioteca.cs, ChooseSearchResult: «MarkSearchActivity();»: elegir la tarjeta «NO ESTA EN LA ROCOLA» de Genius solo
//        dice que no esta (el aviso del buscador de XP); lo demas, lo de XP (tus canciones) o lo de Spotify (g41, antes).
// SIN ROCOLA_OS (o con las dos funciones apagadas): RocolaOsLetrasResultados devuelve 0 sin tocar res ni leer nada, y RocolaOsLetrasElige
// devuelve false: el buscador de XP tal cual. Reglas de la fase 11: en la pantalla solo el latido, los ganchos y los encargos «RocolaOs:...»;
// los archivos y la red, en los hilos propios.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    // EL REPASO DE L3 Y SU INDICE (fuera de Form1: lo usa su hilo). El indice se publica entero (palabra -> rutas) y la pantalla solo lo lee.
    internal static class RocolaOsLetrasMp3
    {
        internal const string Archivo = "rocola_letras_mp3.txt";
        internal const int PalabrasDeFrase = 4;         // desde cuantas palabras escritas se busca por la letra
        internal const int PalabrasQueCuentan = 3;      // y cuantas de ellas no vacias, como minimo, para la letra de tus MP3
        internal const int PausaMs = 20;                // entre archivo y archivo
        private sealed class Entrada { internal long Tam, Fecha; internal string[] Palabras; }
        private static readonly Dictionary<string, Entrada> porRuta = new Dictionary<string, Entrada>(StringComparer.Ordinal);   // (solo su hilo)
        private static Dictionary<string, string[]> indice;      // palabra -> rutas (publicado entero; null = aun nada)
        private static List<string> pendiente;                   // las rutas de la biblioteca que dio la pantalla (la ultima foto)
        private static readonly object cerrojo = new object();
        private static readonly AutoResetEvent despierta = new AutoResetEvent(false);
        private static Thread hilo;
        private static volatile bool parar;
        private static bool leido;
        internal static int Mirados, ConLetra, EnElIndice, Palabras;   // (para vivo.txt y las pruebas)
        internal static volatile bool Listo;                            // el primer repaso acabo
        private static readonly bool SinPausa = Environment.GetEnvironmentVariable("ROCOLA_LETRAS_SIN_PAUSA") == "1";   // (pruebas)

        internal static Dictionary<string, string[]> Indice { get { return Volatile.Read(ref indice); } }
        // (revision) las palabras del indice en orden (ordinal): la ultima a medio escribir se busca por mitades, no recorriendo todo el indice
        private static string[] claves;

        // LA PANTALLA DA LA FOTO de las rutas de los MP3 de la biblioteca (copias: el hilo no toca nada de XP)
        internal static void Repasa(List<string> rutas)
        {
            lock (cerrojo)
            {
                pendiente = rutas;
                if (hilo == null) hilo = RocolaOs.Hilo("letras_mp3", Bucle);
            }
            despierta.Set();
        }
        internal static void Para() { parar = true; despierta.Set(); }

        private static void Bucle()
        {
            while (!parar)
            {
                List<string> rutas;
                lock (cerrojo) { rutas = pendiente; pendiente = null; }
                if (rutas == null) { despierta.WaitOne(60000); continue; }
                if (!leido) { leido = true; Lee(); }
                Pasa(rutas);
                if (parar) return;
                Arma(rutas);
                Listo = true;
            }
        }

        private static string Ruta { get { return RocolaOs.Datos(Archivo); } }
        private static void Lee()
        {
            int lineas = 0;
            try
            {
                if (!File.Exists(Ruta)) return;
                foreach (string l in File.ReadAllLines(Ruta, Encoding.UTF8))
                {
                    lineas++;
                    string[] c = l.Split('\t');
                    if (c.Length < 4) continue;
                    Entrada x = new Entrada();
                    long.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Tam);
                    long.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Fecha);
                    x.Palabras = c[3].Length == 0 ? null : c[3].Split(' ');
                    porRuta[c[0]] = x;
                }
                // SE COMPACTA: el repaso solo anade renglones, y cada MP3 que cambia deja el suyo de antes. Si sobra mas de un tercio (y al
                // menos 200), se reescribe con el ultimo de cada archivo (sin mirar si existe: con el disco de la musica sin montar se perderia todo)
                if (lineas - porRuta.Count >= 200 && lineas > porRuta.Count * 3 / 2)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (KeyValuePair<string, Entrada> kv in porRuta) Renglon(sb, kv.Key, kv.Value);
                    string t = Ruta + ".nuevo";
                    File.WriteAllText(t, sb.ToString(), new UTF8Encoding(false));
                    File.Move(t, Ruta, true);
                    RocolaOs.Apunta("letras_mp3", "rocola_letras_mp3.txt compactado: " + lineas.ToString(CultureInfo.InvariantCulture) + " renglones a " + porRuta.Count.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) { RocolaOs.Falla("letras_mp3", "leer " + Archivo, ex); }
        }
        private static void Renglon(StringBuilder sb, string ruta, Entrada x)
        {
            sb.Append(ruta.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(x.Tam.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(x.Fecha.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(x.Palabras == null ? "" : string.Join(" ", x.Palabras)).Append('\n');
        }
        // EL REPASO: los MP3 que aun no se miraron (o cambiaron), uno cada 20 ms
        private static void Pasa(List<string> rutas)
        {
            StringBuilder nuevas = new StringBuilder();
            int vistas = 0, conLetra = 0;
            foreach (string r in rutas)
            {
                if (parar) break;
                long tam, fecha;
                try { FileInfo fi = new FileInfo(r); if (!fi.Exists) continue; tam = fi.Length; fecha = fi.LastWriteTimeUtc.Ticks; }
                catch { continue; }
                Entrada ya;
                if (porRuta.TryGetValue(r, out ya) && ya.Tam == tam && ya.Fecha == fecha) continue;
                string letra = null;
                try { letra = LetraDelMp3(r); } catch { }
                Entrada x = new Entrada { Tam = tam, Fecha = fecha, Palabras = PalabrasDe(letra) };
                letra = null;
                porRuta[r] = x;
                Renglon(nuevas, r, x);
                vistas++; if (x.Palabras != null) conLetra++;
                if (nuevas.Length > 64 * 1024) Escribe(nuevas);
                if (!SinPausa) Thread.Sleep(PausaMs);
            }
            Escribe(nuevas);
            Interlocked.Add(ref Mirados, vistas); Interlocked.Add(ref ConLetra, conLetra);
            if (vistas > 0) RocolaOs.Apunta("letras_mp3", vistas.ToString(CultureInfo.InvariantCulture) + " MP3 mirados, " + conLetra.ToString(CultureInfo.InvariantCulture) + " con letra (solo sus palabras sueltas)");
        }
        private static void Escribe(StringBuilder sb)
        {
            if (sb.Length == 0) return;
            try { File.AppendAllText(Ruta, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { RocolaOs.Falla("letras_mp3", "escribir " + Archivo, ex); }
            sb.Clear();
        }
        // EL INDICE: palabra -> las rutas de la biblioteca de hoy con esa palabra en su letra
        private static void Arma(List<string> rutas)
        {
            Dictionary<string, List<string>> d = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            int canciones = 0;
            foreach (string r in rutas)
            {
                Entrada x;
                if (!porRuta.TryGetValue(r, out x) || x.Palabras == null) continue;
                canciones++;
                foreach (string w in x.Palabras)
                {
                    List<string> l;
                    if (!d.TryGetValue(w, out l)) { l = new List<string>(2); d[w] = l; }
                    l.Add(r);
                }
            }
            Dictionary<string, string[]> i = new Dictionary<string, string[]>(d.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> kv in d) i[kv.Key] = kv.Value.ToArray();
            string[] cs = new string[i.Count]; i.Keys.CopyTo(cs, 0); Array.Sort(cs, StringComparer.Ordinal);
            Volatile.Write(ref claves, cs);   // (antes que el indice: si se leen a destiempo, solo una palabra que aun no esta: TryGetValue)
            EnElIndice = canciones; Palabras = i.Count;
            Volatile.Write(ref indice, i.Count > 0 ? i : null);
        }

        // ---------- las palabras ----------
        // las que no dicen nada (no cuentan ni en la letra ni en la frase)
        private static readonly HashSet<string> vacias = new HashSet<string>(StringComparer.Ordinal)
        {
            "el", "la", "los", "las", "un", "una", "unos", "unas", "lo", "de", "del", "al", "a", "y", "e", "o", "u", "en", "con", "por", "para",
            "que", "se", "me", "te", "le", "les", "nos", "mi", "mis", "tu", "tus", "su", "sus", "es", "no", "si", "ya", "pero", "mas", "muy",
            "the", "of", "and", "to", "in", "on", "is", "it", "my", "you", "your", "an", "at", "be", "for", "or", "oh", "ah", "eh", "yeah",
        };
        internal static bool EsVacia(string w) { return w.Length < 2 || vacias.Contains(w); }
        // las palabras distintas de una letra, normalizadas como las del buscador de XP, en orden alfabetico (null si no es una letra)
        internal static string[] PalabrasDe(string letra)
        {
            if (string.IsNullOrWhiteSpace(letra)) return null;
            string n = MotorBusqueda.Normaliza(letra);
            SortedSet<string> s = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string w in n.Split(' ')) if (!EsVacia(w) && w.Length <= 40) s.Add(w);
            if (s.Count < 5) return null;   // (no es una letra: un comentario corto)
            string[] a = new string[s.Count];
            s.CopyTo(a);
            return a;
        }
        // las palabras de lo escrito que cuentan (sin repetir), o null si no es una frase de 4 palabras o mas
        internal static List<string> DeLaFrase(string normalizada)
        {
            if (string.IsNullOrEmpty(normalizada)) return null;
            string[] ws = normalizada.Split(' ');
            if (ws.Length < PalabrasDeFrase) return null;
            List<string> t = new List<string>(ws.Length);
            foreach (string w in ws) if (!EsVacia(w) && !t.Contains(w)) t.Add(w);
            return t.Count >= 2 ? t : null;
        }
        // LAS RUTAS QUE TIENEN 3 DE CADA 4 PALABRAS DE LA FRASE (la ultima puede ir a medio escribir: la que empiece asi), con cuantas tienen
        internal static List<KeyValuePair<string, int>> Busca(List<string> toks)
        {
            Dictionary<string, string[]> d = Indice;
            List<KeyValuePair<string, int>> r = new List<KeyValuePair<string, int>>();
            // (revision) al menos 3 palabras que cuentan: «los tigres del norte» (tigres+norte) pedia 2 de 2 en cualquier lado de la letra
            if (d == null || toks == null || toks.Count < PalabrasQueCuentan) return r;
            int hacen = Math.Max(2, (toks.Count * 3 + 3) / 4);
            Dictionary<string, int> cuenta = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int t = 0; t < toks.Count; t++)
            {
                HashSet<string> estas = new HashSet<string>(StringComparer.Ordinal);
                string[] l;
                if (d.TryGetValue(toks[t], out l)) foreach (string x in l) estas.Add(x);
                else if (t == toks.Count - 1 && toks[t].Length >= 3)
                {
                    string[] cs = Volatile.Read(ref claves);
                    if (cs != null)
                    {
                        int k = Array.BinarySearch(cs, toks[t], StringComparer.Ordinal);
                        if (k < 0) k = ~k;
                        for (; k < cs.Length && cs[k].StartsWith(toks[t], StringComparison.Ordinal); k++)
                            if (d.TryGetValue(cs[k], out l)) foreach (string x in l) estas.Add(x);
                    }
                }
                foreach (string x in estas) { int c; cuenta.TryGetValue(x, out c); cuenta[x] = c + 1; }
            }
            foreach (KeyValuePair<string, int> kv in cuenta) if (kv.Value >= hacen) r.Add(kv);
            r.Sort((a, b) => b.Value != a.Value ? b.Value - a.Value : string.CompareOrdinal(a.Key, b.Key));
            return r;
        }

        // ---------- el marco USLT ----------
        // LA LETRA QUE TRAE EL MP3 (USLT del ID3v2.3 o 2.4; la de BuscaLetras.cs del programa anterior), o null. Solo la lee: no se guarda.
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
                    long es = major >= 4 ? ((eh[0] & 0x7f) << 21) | ((eh[1] & 0x7f) << 14) | ((eh[2] & 0x7f) << 7) | (eh[3] & 0x7f) : ((long)eh[0] << 24) | ((long)eh[1] << 16) | ((long)eh[2] << 8) | eh[3];
                    fs.Position += major >= 4 ? es - 4 : es;
                }
                byte[] fh = new byte[10];
                while (fs.Position + 10 <= fin)
                {
                    if (fs.Read(fh, 0, 10) != 10 || fh[0] == 0) return null;
                    long fsz = major >= 4 ? ((fh[4] & 0x7f) << 21) | ((fh[5] & 0x7f) << 14) | ((fh[6] & 0x7f) << 7) | (fh[7] & 0x7f) : ((long)fh[4] << 24) | ((long)fh[5] << 16) | ((long)fh[6] << 8) | fh[7];
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
                        byte enc = b[s]; s += 4;   // la codificacion y el idioma (3 letras)
                        if (enc == 1 || enc == 2) { while (s + 1 < b.Length && !(b[s] == 0 && b[s + 1] == 0)) s += 2; s += 2; }   // la descripcion
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
    }

    public partial class Form1
    {
        private const int RocolaOsLetrasMaxL3 = 20;     // cuantas de la letra de tus MP3 se anaden, como mucho
        private int rocolaOsLetrasCuenta = -1;          // cuantas habia en el indice del buscador la ultima vez que se dio la foto
        private long rocolaOsLetrasFotoMs;
        private bool rocolaOsLetrasArmando;              // se vio la biblioteca armandose
        private Dictionary<string, SearchItem> rocolaOsLetrasPorRuta;   // ruta -> su cancion del indice de XP (se rehace con cada foto)
        private static readonly bool RocolaOsLetrasMuestra = Environment.GetEnvironmentVariable("ROCOLA_LETRAS_MUESTRA") == "1";   // (pruebas: los resultados, por la salida)
        private string rocolaOsLetrasMostrado;
        // cuantas metio g114 en la lista de XP de la ultima consulta (para que la cuenta de g40, con el modo Spotify, las sume tambien)
        private int rocolaOsLetrasMetidas;

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraLetras()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "letras_mp3",
                Que = "buscar una cancion por su letra: las palabras sueltas de la letra de tus MP3 (L3)",
                DeFabrica = true,
                Latido = f => f.RocolaOsLetrasLatido(),
                Vivo = (f, sb) => sb.Append("letras_mp3=").Append(RocolaOsLetrasMp3.Listo ? "listo" : "repasando").Append(' ')
                    .Append(RocolaOsLetrasMp3.EnElIndice.ToString(CultureInfo.InvariantCulture)).Append(" con letra, ")
                    .Append(RocolaOsLetrasMp3.Palabras.ToString(CultureInfo.InvariantCulture)).Append(" palabras\n"),
                ParaAlCerrar = r => RocolaOsLetrasMp3.Para(),
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "busca_letra",
                Que = "buscar una cancion por su letra en Genius (L1; necesita rocola_genius.txt)",
                DeFabrica = false,   // (decision del dueno: encenderla es suya)
                Arranca = f => RocolaOsGenius.Arranca(),
                Vivo = (f, sb) => sb.Append("busca_letra=").Append(RocolaOsGenius.Estado).Append(" peticiones=").Append(RocolaOsGenius.Peticiones.ToString(CultureInfo.InvariantCulture)).Append('\n'),
                ParaAlCerrar = r => RocolaOsGenius.Para(),
            });
        }

        // EL LATIDO (1 s): cuando la biblioteca termina de armarse (o cambia lo que hay), la foto de las rutas de los MP3 al repaso; y una vez
        // por hora otra vez (lo cambiado se vuelve a leer; lo demas solo se mira su tamano y su fecha)
        private void RocolaOsLetrasLatido()
        {
            if (RocolaOsLetrasMuestra && searchOpen) RocolaOsLetrasEnsena();
            if (libBuilding) { rocolaOsLetrasArmando = true; return; }
            long ahora = Environment.TickCount64;
            if (!rocolaOsLetrasArmando && searchIndex.Count == rocolaOsLetrasCuenta && ahora - rocolaOsLetrasFotoMs < 3600000) return;
            rocolaOsLetrasArmando = false; rocolaOsLetrasCuenta = searchIndex.Count; rocolaOsLetrasFotoMs = ahora;
            rocolaOsLetrasPorRuta = null;
            List<string> rutas = new List<string>();
            HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < searchIndex.Count; i++)
            {
                SearchItem si = searchIndex[i];
                if (si == null || si.Kind != 2 || si.TrackRef == null || si.TrackRef.Path == null) continue;
                string p = si.TrackRef.Path;
                if (p.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && vistas.Add(p)) rutas.Add(p);
            }
            RocolaOsLetrasMp3.Repasa(rutas);
        }

        // g114 (Form1.Biblioteca.cs, RecomputeResults): LO DE LA LETRA EN LA LISTA DE XP, antes de contarla. Devuelve cuantos se metieron.
        private int RocolaOsLetrasResultados(string consulta, List<SearchItem> res)
        {
            rocolaOsLetrasMetidas = 0;
            if (!RocolaOs.Activo || res == null) return 0;
            bool l3 = RocolaOs.Encendida("letras_mp3"), l1 = RocolaOs.Encendida("busca_letra");
            if (!l3 && !l1) return 0;
            string q = MotorBusqueda.Normaliza(consulta);
            List<string> toks = RocolaOsLetrasMp3.DeLaFrase(q);
            if (toks == null) { if (l1) RocolaOsGenius.Mirando(null); return 0; }   // (menos de 4 palabras: lo de XP tal cual; y Genius no pregunta lo de antes)
            int n = 0;
            if (l1) n += RocolaOsGeniusDelante(q, res);
            if (l3) n += RocolaOsLetrasDeTusMp3(toks, res);
            rocolaOsLetrasMetidas = n;
            return n;
        }
        private int RocolaOsLetrasDeTusMp3(List<string> toks, List<SearchItem> res)
        {
            List<KeyValuePair<string, int>> hay = RocolaOsLetrasMp3.Busca(toks);
            if (hay.Count == 0) return 0;
            if (rocolaOsLetrasPorRuta == null)
            {
                Dictionary<string, SearchItem> m = new Dictionary<string, SearchItem>(StringComparer.Ordinal);
                for (int i = 0; i < searchIndex.Count; i++)
                {
                    SearchItem si = searchIndex[i];
                    if (si != null && si.Kind == 2 && si.TrackRef != null && si.TrackRef.Path != null && !m.ContainsKey(si.TrackRef.Path)) m[si.TrackRef.Path] = si;
                }
                rocolaOsLetrasPorRuta = m;
            }
            HashSet<SearchItem> ya = new HashSet<SearchItem>(res);
            int n = 0;
            foreach (KeyValuePair<string, int> kv in hay)
            {
                if (n >= RocolaOsLetrasMaxL3) break;
                SearchItem si;
                if (!rocolaOsLetrasPorRuta.TryGetValue(kv.Key, out si) || !ya.Add(si)) continue;
                res.Add(si); n++;
            }
            return n;
        }

        // g115 (Form1.Biblioteca.cs, ChooseSearchResult): la tarjeta «NO ESTA EN LA ROCOLA» solo lo dice. true = ya lo atendio.
        private bool RocolaOsLetrasElige(SearchItem it)
        {
            if (it == null || !RocolaOs.Activo || !RocolaOsGenius.EsNoEsta(it.AlbumRef)) return false;
            MarkSearchActivity();
            ShowSearchToast("Esa no esta en la rocola", false);
            return true;
        }

        // (pruebas, con ROCOLA_LETRAS_MUESTRA=1) LO QUE ENSENA EL BUSCADOR, por la salida, cuando cambia
        private void RocolaOsLetrasEnsena()
        {
            List<SearchItem> r = searchOverlay != null ? searchOverlay.Results : null;
            StringBuilder sb = new StringBuilder();
            sb.Append("BUSCADOR «").Append(searchOverlay != null ? searchOverlay.Query : "").Append("»");
#if ROCOLA_GPU
            if (searchOverlay != null) sb.Append(" (").Append(searchOverlay.TotalResultados.ToString(CultureInfo.InvariantCulture)).Append(')');
#endif
            if (r != null)
                for (int i = 0; i < r.Count && i < 60; i++)
                {
                    SearchItem it = r[i];
                    if (it == null) continue;
                    string que = RocolaOsGenius.EsNoEsta(it.AlbumRef) ? "noesta" : it.TrackRef != null && it.TrackRef.Path != null && it.TrackRef.Path.StartsWith("spotify:", StringComparison.Ordinal) ? "spotify" : it.Kind.ToString(CultureInfo.InvariantCulture);
                    sb.Append(" | ").Append(que).Append(':').Append(it.Display);
                }
            string s = sb.ToString();
            if (s == rocolaOsLetrasMostrado) return;
            rocolaOsLetrasMostrado = s;
            Console.WriteLine(s);
        }
    }
}
