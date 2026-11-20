// LA SUSTITUCION INVISIBLE (fase 11, funcion «calidad_spotify»; FASE11_sustitucion.md): LAS REGLAS, sin estado (las prueba la orden
// «rocola-xp --prueba-calidad [mp3...]»). Las tres decisiones del dueno del 7-11-2026 para el programa anterior («A, B y C»), traidas a rocola-xp:
//
//  A  CUANDO TU CANCION SUENA CLARAMENTE PEOR que la version de Spotify (decision del dueno del 20-11-2026, AGENTS.md: Soloist suena en «Alta»,
//     Ogg Vorbis a 160 kbps, y no se puede subir; «Spotify solo se tomara si la version en la rocola es peor»). Con lo que XP YA MIDE de cada
//     cancion -el corte de agudos del repaso (rocola_eq.txt, EqCut) y el pre-eco (rocola_sonoridad.txt, algoritmo 7)- y lo que dice la
//     cabecera del propio MP3 (su tasa: CBR, Xing/Info o VBRI; y su frecuencia):
//       - el corte medido bajo 16 kHz;
//       - menos de 128 kbps en CBR;
//       - grabada a menos de 44,1 kHz.
//     (Desde el 20-11-2026 ya NO cuentan «128 kbps con el corte bajo 17,5 kHz» ni «pre-eco con 192 kbps o menos y el corte bajo 19 kHz»:
//     esas suenan como el Vorbis 160 de Soloist o mejor.)
//     En VBR solo cuenta el corte, y la frecuencia (su tasa media sale de la musica, no de la calidad). SIN MEDIDA NO SE SUSTITUYE: lo que pide el corte y no lo
//     hay, no cuenta.
//  B  LA PAREJA, ESTRICTA: con el ISRC del MP3 (TSRC), la que tenga ESE ISRC; si no lo trae (o no hay), regla 2: el titulo base igual (sin lo de
//     entre parentesis ni lo de tras « - » que no es version), las mismas marcas de version (en vivo, remix, acustica, karaoke, instrumental,
//     demo...: remasterizada o editada para radio es la misma grabacion), el artista principal igual palabra por palabra (sin acentos ni
//     mayusculas: UnicodeXp.FormaD, el programa corre con InvariantGlobalization) y la duracion a 2 s. A igualdad, el disco que se llama como
//     el tuyo y no un recopilatorio.
//  C  LA FORMA: la primera vez que una pareja suena desde Spotify se compara su volumen por tramos de 0,5 s con el de tu MP3 (correlacion de
//     los dB, con un corrimiento de hasta 3 tramos y al menos 40 tramos con musica en los dos): r >= 0,80 la misma (confirmada); r < 0,55 otra
//     grabacion (se apunta y ya no se sustituye); en medio, o con la forma casi plana, se vuelve a mirar la proxima vez.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    // LO QUE SE LEE DEL MP3 (su cabecera y su etiqueta ID3v2): para A y B
    internal struct RocolaOsCalidadMp3
    {
        internal string Isrc, Titulo, Artista, Disco;
        internal int Kbps, Hz, DuracionMs;
        internal bool Vbr, EsMp3;
    }

    internal static partial class RocolaOsCalidad
    {
        internal const float CorteMalHz = 16000f;
        internal const int DuracionTolMs = 2000;
        internal const int DuracionIsrcTolMs = 3000;   // (revision «invisible-y-reglas») por el ISRC, la duracion a 3 s, como el programa anterior (SpotifyMas.cs)
        internal const double TramoSeg = 0.5;
        internal const int LagMax = 3, TramosMin = 40;
        internal const double RMisma = 0.80, ROtra = 0.55;

        // ---------------------------------------------------------------- A
        // corteHz: el corte de agudos medido por el repaso (0 o menos: sin medir); preEco: el repaso midio pre-eco. motivo: para la bitacora.
        internal static bool SuenaPeor(float corteHz, bool preEco, RocolaOsCalidadMp3 i, out string motivo)
        {
            motivo = null;
            bool conCorte = corteHz > 100f;
            if (conCorte && corteHz < CorteMalHz) { motivo = "agudos cortados en " + ((int)corteHz).ToString(CultureInfo.InvariantCulture) + " Hz"; return true; }
            if (!i.EsMp3) return false;   // (lo que no es MP3: solo el corte)
            // (revision «invisible-y-reglas») la frecuencia va ANTES de mirar si es VBR, como en el programa anterior (SustitucionSpotify.cs:
            // «en VBR solo cuenta el corte» era por la TASA, que en VBR sale de la musica; la frecuencia la dice la cabecera igual)
            if (i.Hz > 0 && i.Hz < 44100) { motivo = "grabada a " + i.Hz.ToString(CultureInfo.InvariantCulture) + " Hz"; return true; }
            if (i.Vbr) return false;   // en VBR solo cuenta el corte (y la frecuencia, arriba)
            if (i.Kbps > 0 && i.Kbps < 128) { motivo = i.Kbps.ToString(CultureInfo.InvariantCulture) + " kbps"; return true; }
            return false;
        }

        // LEE EL MP3: la etiqueta ID3v2.3/2.4 (TSRC, TIT2, TPE1, TALB) y, del primer cuadro de audio, la frecuencia, la tasa (Xing/Info o VBRI
        // si las trae) y la duracion. Lee como mucho la etiqueta y 64 KB de audio. Nunca lanza.
        internal static RocolaOsCalidadMp3 LeeMp3(string ruta)
        {
            RocolaOsCalidadMp3 r = new RocolaOsCalidadMp3();
            try
            {
                if (ruta == null || !ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return r;
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long largo = fs.Length;
                    byte[] h = new byte[10]; long audio = 0;
                    if (Lee(fs, h, 10) == 10 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
                    {
                        int major = h[3], flags = h[5];
                        int tam = Sync(h, 6);
                        audio = 10 + tam + ((flags & 0x10) != 0 ? 10 : 0);
                        if ((major == 3 || major == 4) && (flags & 0x80) == 0 && tam > 0 && tam < 64 * 1024 * 1024)
                        {
                            long fin = 10 + tam; byte[] fh = new byte[10];
                            if ((flags & 0x40) != 0 && Lee(fs, fh, 4) == 4) { int es = major >= 4 ? Sync(fh, 0) : Big(fh, 0); fs.Position = major >= 4 ? 10 + es : 14 + es; }
                            while (fs.Position + 10 <= fin && Lee(fs, fh, 10) == 10)
                            {
                                if (fh[0] == 0) break;
                                int ft = major >= 4 ? Sync(fh, 4) : Big(fh, 4);
                                if (ft <= 0 || fs.Position + ft > fin) break;
                                string id = Encoding.ASCII.GetString(fh, 0, 4);
                                if ((id == "TSRC" || id == "TIT2" || id == "TPE1" || id == "TALB") && ft < 4096)
                                {
                                    byte[] b = new byte[ft]; Lee(fs, b, ft);
                                    string v = Texto(b);
                                    if (id == "TSRC") r.Isrc = LimpiaIsrc(v);
                                    else if (id == "TIT2") r.Titulo = v;
                                    else if (id == "TPE1") r.Artista = v;
                                    else r.Disco = v;
                                }
                                else fs.Position += ft;
                            }
                        }
                    }
                    fs.Position = audio; byte[] a = new byte[65536]; int n = Lee(fs, a, a.Length);
                    for (int i = 0; i + 4 <= n; i++)
                    {
                        if (a[i] != 0xFF || (a[i + 1] & 0xE0) != 0xE0) continue;
                        int ver = (a[i + 1] >> 3) & 3, capa = (a[i + 1] >> 1) & 3, bi = a[i + 2] >> 4, si = (a[i + 2] >> 2) & 3, modo = a[i + 3] >> 6;
                        if (ver == 1 || capa != 1 || bi == 0 || bi == 15 || si == 3) continue;   // solo MPEG capa 3
                        bool m1 = ver == 3;
                        int[] tasas = m1 ? new[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 } : new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
                        int[] hz = { 44100, 48000, 32000 };
                        int f = hz[si] >> (ver == 3 ? 0 : ver == 2 ? 1 : 2);
                        r.EsMp3 = true; r.Hz = f; r.Kbps = tasas[bi];
                        int por = m1 ? 1152 : 576;
                        int lado = m1 ? (modo == 3 ? 17 : 32) : (modo == 3 ? 9 : 17);
                        int x = i + 4 + lado;
                        long cuadros = -1, bytes = -1;
                        if (x + 16 <= n && ((a[x] == 'X' && a[x + 1] == 'i' && a[x + 2] == 'n' && a[x + 3] == 'g') || (a[x] == 'I' && a[x + 1] == 'n' && a[x + 2] == 'f' && a[x + 3] == 'o')))
                        {
                            r.Vbr = a[x] == 'X';   // «Xing»: VBR; «Info»: CBR (LAME)
                            int fl = Big(a, x + 4); int p = x + 8;
                            if ((fl & 1) != 0) { cuadros = (uint)Big(a, p); p += 4; }
                            if ((fl & 2) != 0) { bytes = (uint)Big(a, p); p += 4; }
                            if (r.Vbr && cuadros > 0 && bytes > 0) r.Kbps = (int)Math.Round(bytes * 8.0 / (cuadros * (double)por / f) / 1000.0);
                        }
                        else if (i + 36 + 18 <= n && a[i + 36] == 'V' && a[i + 37] == 'B' && a[i + 38] == 'R' && a[i + 39] == 'I')
                        {
                            r.Vbr = true;
                            bytes = (uint)Big(a, i + 36 + 10); cuadros = (uint)Big(a, i + 36 + 14);
                            if (cuadros > 0 && bytes > 0) r.Kbps = (int)Math.Round(bytes * 8.0 / (cuadros * (double)por / f) / 1000.0);
                        }
                        if (cuadros > 0) r.DuracionMs = (int)Math.Min(int.MaxValue, cuadros * (long)por * 1000L / f);
                        else if (r.Kbps > 0) r.DuracionMs = (int)Math.Min(int.MaxValue, (largo - audio - i) * 8L / r.Kbps);   // (CBR sin Info: por el tamano)
                        break;
                    }
                }
            }
            catch { }
            return r;
        }
        private static int Lee(Stream s, byte[] b, int n) { int h = 0, k; while (h < n && (k = s.Read(b, h, n - h)) > 0) h += k; return h; }
        private static int Sync(byte[] b, int o) { return (b[o] & 0x7f) << 21 | (b[o + 1] & 0x7f) << 14 | (b[o + 2] & 0x7f) << 7 | (b[o + 3] & 0x7f); }
        private static int Big(byte[] b, int o) { return b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]; }
        private static string Texto(byte[] b)
        {
            if (b.Length < 2) return "";
            try
            {
                string s;
                switch (b[0])
                {
                    case 1: s = Encoding.Unicode.GetString(b, 1, b.Length - 1).TrimStart('﻿', '￾'); break;
                    case 2: s = Encoding.BigEndianUnicode.GetString(b, 1, b.Length - 1); break;
                    case 3: s = Encoding.UTF8.GetString(b, 1, b.Length - 1); break;
                    default: s = Encoding.Latin1.GetString(b, 1, b.Length - 1); break;
                }
                int z = s.IndexOf('\0');
                return (z >= 0 ? s.Substring(0, z) : s).Trim();
            }
            catch { return ""; }
        }
        // «US-ABC-12-34567» o «usabc1234567» -> «USABC1234567»; null si no tiene la forma de un ISRC (2 letras, 3 alfanumericos, 7 cifras)
        internal static string LimpiaIsrc(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            StringBuilder b = new StringBuilder(12);
            foreach (char c in s) if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) b.Append(char.ToUpperInvariant(c));
            string r = b.ToString();
            if (r.Length != 12 || !char.IsLetter(r[0]) || !char.IsLetter(r[1])) return null;
            for (int i = 5; i < 12; i++) if (!char.IsDigit(r[i])) return null;
            return r;
        }

        // ---------------------------------------------------------------- B
        // SIN ACENTOS NI MAYUSCULAS, solo letras y cifras separadas por un espacio (UnicodeXp.FormaD: la tabla de XP, sin ICU)
        internal static string Normaliza(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string d;
            try { d = UnicodeXp.FormaD(s); } catch { d = s; }
            StringBuilder b = new StringBuilder(d.Length);
            bool esp = true;
            foreach (char ch in d)
            {
                if (ch >= '̀' && ch <= 'ͯ') continue;   // (las marcas que FormaD separa)
                if (char.IsLetterOrDigit(ch)) { b.Append(char.ToLowerInvariant(ch)); esp = false; }
                else if (!esp) { b.Append(' '); esp = true; }
            }
            return b.ToString().Trim();
        }
        // LAS MARCAS DE QUE ES OTRA GRABACION (otra version de la misma cancion). Remasterizada, editada para radio, «version original», mono o
        // estereo no cuentan: es la misma grabacion.
        private static readonly string[][] Marcas = {
            new[] { "vivo", "en vivo", "live", "en directo", "directo", "en concierto", "concierto" },
            new[] { "mezcla", "remix", "rmx", "mix", "extended", "extendida", "club mix" },
            new[] { "acustica", "acustico", "acoustic", "unplugged", "desenchufado" },
            new[] { "karaoke" }, new[] { "instrumental" }, new[] { "demo" }, new[] { "cover" },
            new[] { "acapella", "a capella", "a cappella", "acappella" },
            new[] { "regrabada", "regrabado", "re recorded", "rerecorded", "re grabada", "taylor s version" },
            new[] { "acelerada", "sped up" }, new[] { "lenta", "slowed" },
            new[] { "sinfonica", "sinfonico", "orquesta", "symphonic" },
            new[] { "version banda", "version nortena", "version mariachi", "version salsa", "version cumbia" },
        };
        // las marcas de un titulo (en lo normalizado, palabra por palabra), en orden y sin repetir: «vivo,acustica»...
        internal static string MarcasDe(string titulo)
        {
            string t = " " + Normaliza(titulo) + " ";
            List<string> m = new List<string>();
            foreach (string[] g in Marcas)
                foreach (string w in g)
                    if (t.IndexOf(" " + w + " ", StringComparison.Ordinal) >= 0) { if (!m.Contains(g[0])) m.Add(g[0]); break; }
            m.Sort(StringComparer.Ordinal);
            return string.Join(",", m);
        }
        // las palabras que en una cola « - ...» o entre parentesis dicen que es una version, una remasterizacion o una edicion (se quita del titulo base)
        private static readonly string[] Colas = { "remaster", "remasterizado", "remasterizada", "remastered", "version", "edit", "edicion", "mono", "stereo", "estereo",
            "vivo", "live", "directo", "concierto", "mix", "remix", "acustica", "acustico", "acoustic", "unplugged", "karaoke", "instrumental", "demo", "original", "radio", "single", "album" };
        // EL TITULO BASE: sin lo de entre parentesis o corchetes y sin la cola « - ...» si es de version (o un ano); normalizado
        internal static string TituloBase(string titulo)
        {
            if (string.IsNullOrEmpty(titulo)) return "";
            StringBuilder b = new StringBuilder(titulo.Length);
            int prof = 0;
            foreach (char ch in titulo)
            {
                if (ch == '(' || ch == '[') { prof++; continue; }
                if (ch == ')' || ch == ']') { if (prof > 0) prof--; continue; }
                if (prof == 0) b.Append(ch);
            }
            string s = b.ToString();
            int g = s.IndexOf(" - ", StringComparison.Ordinal);
            if (g > 0)
            {
                string cola = " " + Normaliza(s.Substring(g + 3)) + " ";
                bool deVersion = false;
                foreach (string w in Colas) if (cola.IndexOf(" " + w + " ", StringComparison.Ordinal) >= 0) { deVersion = true; break; }
                if (!deVersion) foreach (string w in cola.Split(' ')) if (w.Length == 4 && (w.StartsWith("19", StringComparison.Ordinal) || w.StartsWith("20", StringComparison.Ordinal)) && int.TryParse(w, out _)) { deVersion = true; break; }
                if (deVersion) s = s.Substring(0, g);
            }
            return Normaliza(s);
        }
        // las partes de un artista («A & B», «A feat. B», «A y B», «A, B», «A x B», «A con B»), normalizadas
        internal static List<string> PartesArtista(string artista)
        {
            List<string> r = new List<string>();
            string a = " " + Normaliza(artista) + " ";
            foreach (string sep in new[] { " feat ", " ft ", " featuring ", " con ", " y ", " x ", " and ", " with " }) a = a.Replace(sep, "|");
            foreach (string p in a.Split('|')) { string n = p.Trim(); if (n.Length > 0 && !r.Contains(n)) r.Add(n); }
            return r;
        }
        // el artista principal del MP3 (el primero) es, palabra por palabra, uno de los de la cancion de Spotify
        internal static bool MismoArtista(string local, List<string> deSpotify)
        {
            if (deSpotify == null || deSpotify.Count == 0) return false;
            string bruto = local ?? "";
            foreach (string sep in new[] { "&", ",", ";", "/" }) bruto = bruto.Replace(sep, " y ");
            List<string> l = PartesArtista(bruto);
            if (l.Count == 0) return false;
            List<string> s = new List<string>();
            foreach (string x in deSpotify) { string n = Normaliza(x); if (n.Length > 0) s.Add(n); }
            if (s.Contains(l[0])) return true;
            return Normaliza(local) == Normaliza(string.Join(" ", deSpotify));   // «A y B» escrito como un solo nombre
        }

        // LA PAREJA: la mejor candidata con las reglas de B, o null. porIsrc: la lista es de la busqueda por el ISRC del MP3.
        internal static RocolaOsCalidadCandidata Elige(RocolaOsCalidadMp3 mp3, string titulo, string artista, string disco, int durMs, List<RocolaOsCalidadCandidata> l, bool porIsrc, out string porQue)
        {
            porQue = null;
            RocolaOsCalidadCandidata mejor = null; int mejorPuntos = int.MinValue, mejorDif = int.MaxValue;
            string base0 = TituloBase(titulo), marcas0 = MarcasDe(titulo), disco0 = Normaliza(disco);
            int vistas = 0;
            foreach (RocolaOsCalidadCandidata c in l)
            {
                if (c == null || string.IsNullOrEmpty(c.Id)) continue;
                vistas++;
                int dif = durMs > 0 && c.DuracionMs > 0 ? Math.Abs(durMs - c.DuracionMs) : int.MaxValue;
                if (porIsrc)
                {
                    if (mp3.Isrc == null || !string.Equals(c.Isrc, mp3.Isrc, StringComparison.Ordinal)) continue;
                    // (revision «invisible-y-reglas») el mismo ISRC Y la duracion a 3 s, como el programa anterior (SpotifyMas.cs: «Spotify a veces
                    // devuelve parecidas»; un ISRC mal puesto en la etiqueta o un MP3 cortado no pasan). Sin una de las dos duraciones, el ISRC basta.
                    if (dif != int.MaxValue && dif > DuracionIsrcTolMs) { porQue = "su ISRC, pero dura distinto"; continue; }
                }
                else
                {
                    if (TituloBase(c.Titulo) != base0 || base0.Length == 0) { porQue = porQue ?? "otro titulo"; continue; }
                    if (MarcasDe(c.Titulo) != marcas0) { porQue = "otra version (" + (MarcasDe(c.Titulo).Length > 0 ? MarcasDe(c.Titulo) : "sin marcas") + ")"; continue; }
                    if (!MismoArtista(artista, c.Artistas)) { porQue = "otro artista"; continue; }
                    if (dif > DuracionTolMs) { porQue = "dura distinto"; continue; }
                }
                int puntos = 0;
                if (disco0.Length > 0 && Normaliza(c.Disco) == disco0) puntos += 2;
                if (string.Equals(c.TipoDisco, "compilation", StringComparison.OrdinalIgnoreCase)) puntos -= 1;
                if (puntos > mejorPuntos || (puntos == mejorPuntos && dif < mejorDif)) { mejor = c; mejorPuntos = puntos; mejorDif = dif; }
            }
            if (mejor == null && porQue == null) porQue = vistas == 0 ? "sin resultados" : "ninguna con su ISRC";
            return mejor;
        }

        // ---------------------------------------------------------------- C
        // La correlacion de la forma: los dB por tramo de tu MP3 (sumas de cuadrados / cuadros) contra los de lo que sono de Spotify, con un
        // corrimiento de hasta LagMax tramos. Solo los tramos con musica en tu MP3 (a menos de 40 dB de su tramo mas fuerte) y con sonido de
        // Spotify. «misma», «otra» o «no se sabe» (menos de TramosMin tramos, o la forma de tu MP3 casi plana: menos de 1,5 dB de vaiven).
        internal static string Compara(double[] eMp3, long[] nMp3, double[] eSp, long[] nSp, out double mejor, out int usados)
        {
            mejor = -1; usados = 0;
            if (eMp3 == null || nMp3 == null || eSp == null || nSp == null) return "no se sabe";
            int L = Math.Min(Math.Min(eMp3.Length, nMp3.Length), Math.Min(eSp.Length, nSp.Length));
            double[] a = new double[L], b = new double[L]; bool[] ha = new bool[L], hb = new bool[L];
            double maxA = double.NegativeInfinity;
            for (int k = 0; k < L; k++)
            {
                if (nMp3[k] > 0 && eMp3[k] > 0) { a[k] = 10 * Math.Log10(eMp3[k] / nMp3[k]); ha[k] = true; if (a[k] > maxA) maxA = a[k]; }
                if (nSp[k] > 0 && eSp[k] > 0) { b[k] = 10 * Math.Log10(eSp[k] / nSp[k]); hb[k] = true; }
            }
            for (int k = 0; k < L; k++) if (ha[k] && a[k] < maxA - 40) ha[k] = false;
            for (int lag = -LagMax; lag <= LagMax; lag++)
            {
                double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0; int m = 0;
                for (int k = 0; k < L; k++)
                {
                    int j = k + lag;
                    if (j < 0 || j >= L || !ha[k] || !hb[j]) continue;
                    sa += a[k]; sb += b[j]; saa += a[k] * a[k]; sbb += b[j] * b[j]; sab += a[k] * b[j]; m++;
                }
                if (m < TramosMin) continue;
                double va = saa - sa * sa / m, vb = sbb - sb * sb / m;
                if (va / m < 2.25 || vb <= 0) continue;
                double r = (sab - sa * sb / m) / Math.Sqrt(va * vb);
                if (r > mejor) { mejor = r; usados = m; }
            }
            if (usados == 0) return "no se sabe";
            if (mejor >= RMisma) return "misma";
            if (mejor < ROtra) return "otra";
            return "no se sabe";
        }

        // ---------------------------------------------------------------- la orden de prueba
        internal static int Prueba(string[] mp3)
        {
            int mal = 0;
            Action<bool, string> ok = (c, que) => { Console.WriteLine((c ? "BIEN  " : "MAL   ") + que); if (!c) mal++; };
            RocolaOsCalidadMp3 cbr96 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 96, Hz = 44100 }, cbr128 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 128, Hz = 44100 };
            RocolaOsCalidadMp3 cbr320 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 320, Hz = 44100 }, vbr = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 100, Hz = 44100, Vbr = true };
            RocolaOsCalidadMp3 cbr192 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 192, Hz = 44100 }, hz32 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 160, Hz = 32000 };
            string mo;
            ok(SuenaPeor(15000, false, cbr320, out mo), "A: corte medido bajo 16 kHz (" + mo + ")");
            ok(SuenaPeor(0, false, cbr96, out mo), "A: 96 kbps CBR, sin medir el corte (" + mo + ")");
            ok(!SuenaPeor(0, false, vbr, out mo), "A: VBR de 100 kbps de media sin corte medido: no");
            ok(SuenaPeor(15500, false, vbr, out mo), "A: VBR con el corte bajo 16 kHz: si");
            ok(SuenaPeor(0, false, hz32, out mo), "A: grabada a 32 kHz (" + mo + ")");
            RocolaOsCalidadMp3 vbr22 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 64, Hz = 22050, Vbr = true }, vbr44 = new RocolaOsCalidadMp3 { EsMp3 = true, Kbps = 90, Hz = 44100, Vbr = true };
            ok(SuenaPeor(0, false, vbr22, out mo), "A: VBR grabada a 22 kHz, sin medir el corte: si, la frecuencia vale tambien en VBR (" + mo + ")");
            ok(!SuenaPeor(16500, true, vbr44, out mo), "A: VBR de 90 kbps de media, con pre-eco y corte 16,5 kHz: no (en VBR la tasa y el pre-eco no cuentan)");
            ok(!SuenaPeor(0, false, new RocolaOsCalidadMp3 { EsMp3 = false, Hz = 22050 }, out mo), "A: lo que no es MP3, sin el corte: no");
            ok(!SuenaPeor(17000, false, cbr128, out mo), "A: 128 kbps y corte 17 kHz: no (20-11-2026: como el Vorbis 160 de Soloist)");
            ok(!SuenaPeor(0, false, cbr128, out mo), "A: 128 kbps sin corte medido: no (sin medida no se sustituye)");
            ok(!SuenaPeor(18000, false, cbr128, out mo), "A: 128 kbps y corte 18 kHz: no");
            ok(!SuenaPeor(18500, true, cbr192, out mo), "A: pre-eco, 192 kbps y corte 18,5 kHz: no (20-11-2026)");
            ok(!SuenaPeor(18500, false, cbr192, out mo), "A: 192 kbps y corte 18,5 kHz sin pre-eco: no");
            ok(!SuenaPeor(19500, true, cbr192, out mo), "A: pre-eco con corte 19,5 kHz: no");
            ok(!SuenaPeor(0, false, cbr320, out mo), "A: 320 kbps sin medir: no");
            ok(MarcasDe("Amor Eterno") == "", "B: sin marcas");
            ok(MarcasDe("Amor Eterno (En Vivo)") == "vivo", "B: (En Vivo)");
            ok(MarcasDe("Amor Eterno - Live at Bellas Artes") == "vivo", "B: - Live at");
            ok(MarcasDe("Amor Eterno - Remasterizado 2015") == "", "B: remasterizada = la misma grabacion");
            ok(MarcasDe("Oliver") == "" && MarcasDe("El Demonio") == "", "B: «Oliver» no es live ni «Demonio» demo");
            ok(MarcasDe("Cancion (Acoustic Version)") == "acustica" && MarcasDe("Canción (Versión Acústica)") == "acustica", "B: acustica (con acentos)");
            ok(MarcasDe("Cancion - Remix") == MarcasDe("Cancion [Club Mix]"), "B: remix = mix");
            ok(TituloBase("Amor Eterno (En Vivo)") == "amor eterno" && TituloBase("Amor Eterno - Remastered 2011") == "amor eterno" && TituloBase("ÁMOR ETERNO") == "amor eterno", "B: titulo base");
            ok(TituloBase("Sin Ti - Con Ella") != TituloBase("Sin Ti"), "B: una cola que no es de version se queda");
            List<string> sa = new List<string> { "Los Ángeles Azules" };
            ok(!MismoArtista("Los Ángeles", sa), "B: «Los Angeles» no es «Los Angeles Azules»");
            ok(MismoArtista("LOS ANGELES AZULES", sa), "B: mismo artista sin acentos ni mayusculas");
            ok(MismoArtista("Los Ángeles Azules feat. Natalia Lafourcade", new List<string> { "Los Ángeles Azules", "Natalia Lafourcade" }), "B: con invitado");
            ok(!MismoArtista("Natalia", new List<string> { "Natalia Lafourcade" }), "B: «Natalia» no es «Natalia Lafourcade»");
            ok(LimpiaIsrc("MX-A12-99-00001") == "MXA129900001" && LimpiaIsrc("hola") == null, "B: el ISRC");
            List<RocolaOsCalidadCandidata> cand = new List<RocolaOsCalidadCandidata>
            {
                new RocolaOsCalidadCandidata { Id = "envivo", Titulo = "Vals Sin Codigo - En Vivo", Artistas = { "Trio Calidad" }, Disco = "Joyas de Prueba", TipoDisco = "album", DuracionMs = 32000 },
                new RocolaOsCalidadCandidata { Id = "otroart", Titulo = "Vals Sin Codigo", Artistas = { "Trio Calidad Azul" }, Disco = "Joyas de Prueba", TipoDisco = "album", DuracionMs = 32000 },
                new RocolaOsCalidadCandidata { Id = "largo", Titulo = "Vals Sin Codigo", Artistas = { "Trio Calidad" }, Disco = "Joyas de Prueba", TipoDisco = "album", DuracionMs = 36000 },
                new RocolaOsCalidadCandidata { Id = "recop", Titulo = "Vals Sin Codigo", Artistas = { "Trio Calidad" }, Disco = "Exitos de Todos", TipoDisco = "compilation", DuracionMs = 32000 },
                new RocolaOsCalidadCandidata { Id = "buena", Titulo = "Vals Sin Código (Remasterizado)", Artistas = { "Trío Calidad" }, Disco = "Joyas de Prueba", TipoDisco = "album", DuracionMs = 31500 },
            };
            string pq;
            RocolaOsCalidadCandidata e = Elige(new RocolaOsCalidadMp3(), "Vals Sin Codigo", "Trio Calidad", "Joyas de Prueba", 32000, cand, false, out pq);
            ok(e != null && e.Id == "buena", "B: regla 2 elige la de su disco, sin el en vivo, el otro artista, la de otra duracion ni el recopilatorio (" + (e != null ? e.Id : "ninguna") + ")");
            cand.RemoveAll(x => x.Id == "buena");
            e = Elige(new RocolaOsCalidadMp3(), "Vals Sin Codigo", "Trio Calidad", "Joyas de Prueba", 32000, cand, false, out pq);
            ok(e != null && e.Id == "recop", "B: sin la de su disco, el recopilatorio vale (" + (e != null ? e.Id : "ninguna") + ")");
            e = Elige(new RocolaOsCalidadMp3(), "Vals Sin Codigo (En Vivo)", "Trio Calidad", "", 32000, cand, false, out pq);
            ok(e != null && e.Id == "envivo", "B: la tuya en vivo casa con la en vivo");
            // B por el ISRC: la duracion a 3 s (revision «invisible-y-reglas»)
            RocolaOsCalidadMp3 conIsrc = new RocolaOsCalidadMp3 { Isrc = "MXA112600001" };
            List<RocolaOsCalidadCandidata> ci = new List<RocolaOsCalidadCandidata>
            {
                new RocolaOsCalidadCandidata { Id = "isrclargo", Titulo = "Otra Cosa", Artistas = { "Otro" }, Isrc = "MXA112600001", DuracionMs = 36000 },
                new RocolaOsCalidadCandidata { Id = "isrcotro", Titulo = "Bolero de Prueba", Artistas = { "Trio Calidad" }, Isrc = "MXA112600002", DuracionMs = 32000 },
            };
            e = Elige(conIsrc, "Bolero de Prueba", "Trio Calidad", "", 32000, ci, true, out pq);
            ok(e == null, "B: por el ISRC, el mismo ISRC pero 4 s mas larga: no (" + (e != null ? e.Id : pq) + ")");
            ci.Add(new RocolaOsCalidadCandidata { Id = "isrcbien", Titulo = "Otro Nombre (Remastered)", Artistas = { "Otro" }, Isrc = "MXA112600001", DuracionMs = 34500 });
            e = Elige(conIsrc, "Bolero de Prueba", "Trio Calidad", "", 32000, ci, true, out pq);
            ok(e != null && e.Id == "isrcbien", "B: por el ISRC, el mismo ISRC a 2,5 s: si, aunque el titulo sea otro (" + (e != null ? e.Id : pq) + ")");
            e = Elige(conIsrc, "Bolero de Prueba", "Trio Calidad", "", 0, ci, true, out pq);
            ok(e != null && (e.Id == "isrcbien" || e.Id == "isrclargo"), "B: por el ISRC sin la duracion del MP3: el ISRC basta");
            // C: la misma forma con otro nivel y algo de diferencia (misma), otra forma (otra), plana (no se sabe), poca (no se sabe)
            int T = 120; Random rr = new Random(7);
            double[] em = new double[T], es = new double[T], eo = new double[T], ep = new double[T]; long[] nm = new long[T], ns = new long[T], np = new long[T];
            for (int k = 0; k < T; k++)
            {
                double t = k * TramoSeg, db = Envolvente(1, t), otro = Envolvente(2, t);
                nm[k] = 22050; ns[k] = 22050; np[k] = k < 30 ? 22050 : 0;
                em[k] = Math.Pow(10, db / 10) * nm[k];
                es[k] = Math.Pow(10, (db + 6 + (rr.NextDouble() - 0.5)) / 10) * ns[k];
                eo[k] = Math.Pow(10, otro / 10) * ns[k];
                ep[k] = 0.01 * nm[k];
            }
            double r; int u;
            string v1 = Compara(em, nm, es, ns, out r, out u); ok(v1 == "misma", "C: la misma grabacion: " + v1 + " (r " + r.ToString("0.00", CultureInfo.InvariantCulture) + ", " + u + " tramos)");
            string v2 = Compara(em, nm, eo, ns, out r, out u); ok(v2 == "otra", "C: otra grabacion: " + v2 + " (r " + r.ToString("0.00", CultureInfo.InvariantCulture) + ")");
            double[] e4 = new double[T]; for (int k = 2; k < T; k++) e4[k] = es[k - 2];
            long[] n4 = new long[T]; for (int k = 2; k < T; k++) n4[k] = 22050;
            string v4 = Compara(em, nm, e4, n4, out r, out u); ok(v4 == "misma", "C: la misma, corrida 1 s: " + v4);
            string v3 = Compara(ep, nm, es, ns, out r, out u); ok(v3 == "no se sabe", "C: tu MP3 plano: " + v3);
            string v5 = Compara(em, nm, es, np, out r, out u); ok(v5 == "no se sabe", "C: solo 30 tramos: " + v5);
            foreach (string f in mp3)
            {
                RocolaOsCalidadMp3 i = LeeMp3(f);
                bool peor = SuenaPeor(0, false, i, out mo);
                Console.WriteLine("  " + Path.GetFileName(f) + ": " + i.Kbps + " kbps" + (i.Vbr ? " (VBR)" : "") + ", " + i.Hz + " Hz, " + i.DuracionMs + " ms, ISRC " + (i.Isrc ?? "-")
                    + ", «" + (i.Titulo ?? "") + "» de «" + (i.Artista ?? "") + "» en «" + (i.Disco ?? "") + "»; suena peor (sin el corte): " + (peor ? "si, " + mo : "no"));
            }
            Console.WriteLine(mal == 0 ? "TODO BIEN" : mal + " MAL");
            return mal == 0 ? 0 : 1;
        }
        // LA ENVOLVENTE DE LAS PRUEBAS (en dB): la 1 y la 2, dos formas distintas. La misma cuenta la usan el Soloist falso (ids «...m1»/«...m2»)
        // y los MP3 inventados de Pruebas/RocolaOs/sustitucion (ffmpeg, filtro volume).
        internal static double Envolvente(int cual, double t)
        {
            if (cual == 2) return -12 + 9 * Math.Sin(2 * Math.PI * t / 6.2 + 2) + 4 * Math.Cos(2 * Math.PI * t / 10.0);
            return -12 + 9 * Math.Sin(2 * Math.PI * t / 14.0) + 4 * Math.Sin(2 * Math.PI * t / 4.6);
        }
    }
}
