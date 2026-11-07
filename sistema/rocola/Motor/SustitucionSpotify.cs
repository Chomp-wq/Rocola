// SP2 MAS PRECISO (decision del dueno del 7-11-2026: «A, B y C»). Solo Rocola OS con el modo Spotify encendido.
//  A  SUENA PEOR, CON MAS DATOS: antes solo contaba el corte de agudos (< 16 kHz). Ahora tambien lo que dice el propio MP3 (su tasa media,
//     de la cabecera: CBR, Xing/Info o VBRI; y su frecuencia) y el pre-eco medido por el repaso (algoritmo 7). Una de 128 kbps con el
//     corte de LAME (~16-17,5 kHz) ya cuenta; una de 320 kbps o un WAV/FLAC con los agudos enteros, no. (SuenaPeor)
//  B  LA PAREJA, ESTRICTA: si tu MP3 trae su ISRC (el numero de la grabacion, marco TSRC) se busca por ese numero y solo vale la que lo
//     tenga igual. Si no: el titulo sin lo de entre parentesis y tras « - » debe ser IGUAL, las marcas de version (en vivo, remix,
//     acustica, karaoke, instrumental, demo...) iguales en las dos, el artista principal igual palabra por palabra (no «contiene»),
//     la duracion a 2 s y, a igualdad, el disco que se llama como el tuyo y no un recopilatorio. (ReglaPareja = 2; las parejas de antes
//     se vuelven a buscar, primero las que se sustituirian.)
//  C  LA PRIMERA VEZ, SE COMPRUEBA CON EL SONIDO: mientras suena la version de Spotify se apunta su volumen por tramos (la energia que ya
//     mide la cadena) y se compara con la forma de tu MP3 (240 tramos, MideForma): correlacion con un corrimiento de hasta 3 tramos.
//     >= 0,80: confirmada; < 0,55: es otra grabacion y esa cancion ya no se sustituye (vuelve tu MP3); en medio o con poca musica para
//     decidir, se vuelve a mirar la proxima vez. Nada se graba: solo numeros de volumen, en memoria.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        internal const int ReglaPareja = 2;   // las parejas hechas con las reglas de B

        // ---------------- A: LO QUE DICE EL PROPIO MP3 ----------------
        internal struct InfoMp3 { public string Isrc; public int Kbps, Hz; public bool Vbr; }   // (Vbr: su tasa media depende de la musica, no de la calidad)
        private readonly Dictionary<string, InfoMp3> infoMp3 = new Dictionary<string, InfoMp3>(StringComparer.OrdinalIgnoreCase);
        private InfoMp3 InfoDe(string ruta)
        {
            InfoMp3 i;
            lock (infoMp3) if (infoMp3.TryGetValue(ruta, out i)) return i;
            i = LeeInfoMp3(ruta);
            lock (infoMp3) { if (infoMp3.Count > 50000) infoMp3.Clear(); infoMp3[ruta] = i; }
            return i;
        }
        // Lee el ISRC (TSRC de ID3v2.3/2.4) y, del primer cuadro de audio, la frecuencia y la tasa media (Xing/Info o VBRI si es VBR).
        internal static InfoMp3 LeeInfoMp3(string ruta)
        {
            InfoMp3 r = new InfoMp3();
            try
            {
                if (!ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return r;
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] h = new byte[10]; long audio = 0;
                    if (Lee(fs, h, 10) == 10 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
                    {
                        int major = h[3], flags = h[5];
                        int tam = (h[6] & 0x7f) << 21 | (h[7] & 0x7f) << 14 | (h[8] & 0x7f) << 7 | (h[9] & 0x7f);
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
                                if (fh[0] == 'T' && fh[1] == 'S' && fh[2] == 'R' && fh[3] == 'C' && ft < 200)
                                {
                                    byte[] b = new byte[ft]; Lee(fs, b, ft);
                                    r.Isrc = LimpiaIsrc(Texto(b));
                                }
                                else fs.Position += ft;
                            }
                        }
                    }
                    // el primer cuadro de audio (hasta 64 KB despues de la etiqueta)
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
                        r.Hz = f; r.Kbps = tasas[bi];
                        int por = m1 ? 1152 : 576;
                        int lado = m1 ? (modo == 3 ? 17 : 32) : (modo == 3 ? 9 : 17);
                        int x = i + 4 + lado;
                        if (x + 16 <= n && ((a[x] == 'X' && a[x + 1] == 'i' && a[x + 2] == 'n' && a[x + 3] == 'g') || (a[x] == 'I' && a[x + 1] == 'n' && a[x + 2] == 'f' && a[x + 3] == 'o')))
                        {
                            r.Vbr = a[x] == 'X';   // «Xing»: VBR; «Info»: CBR (LAME)
                            int fl = Big(a, x + 4); int p = x + 8; long cuadros = -1, bytes = -1;
                            if ((fl & 1) != 0) { cuadros = (uint)Big(a, p); p += 4; }
                            if ((fl & 2) != 0) { bytes = (uint)Big(a, p); p += 4; }
                            if (cuadros > 0 && bytes > 0) r.Kbps = (int)Math.Round(bytes * 8.0 / (cuadros * (double)por / f) / 1000.0);
                        }
                        else if (i + 36 + 18 <= n && a[i + 36] == 'V' && a[i + 37] == 'B' && a[i + 38] == 'R' && a[i + 39] == 'I')
                        {
                            r.Vbr = true;
                            long bytes = (uint)Big(a, i + 36 + 10), cuadros = (uint)Big(a, i + 36 + 14);
                            if (cuadros > 0 && bytes > 0) r.Kbps = (int)Math.Round(bytes * 8.0 / (cuadros * (double)por / f) / 1000.0);
                        }
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
                switch (b[0])
                {
                    case 1: return Encoding.Unicode.GetString(b, 1, b.Length - 1).TrimStart('﻿', '￾');
                    case 2: return Encoding.BigEndianUnicode.GetString(b, 1, b.Length - 1);
                    case 3: return Encoding.UTF8.GetString(b, 1, b.Length - 1);
                    default: return Encoding.Latin1.GetString(b, 1, b.Length - 1);
                }
            }
            catch { return ""; }
        }
        // «US-ABC-12-34567» o «usabc1234567» -> «USABC1234567»; null si no tiene la forma de un ISRC (2 letras, 3 alfanumericos, 7 cifras)
        internal static string LimpiaIsrc(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            StringBuilder b = new StringBuilder(12);
            foreach (char c in s) if (char.IsLetterOrDigit(c)) b.Append(char.ToUpperInvariant(c));
            string r = b.ToString();
            if (r.Length != 12 || !char.IsLetter(r[0]) || !char.IsLetter(r[1])) return null;
            for (int i = 5; i < 12; i++) if (!char.IsDigit(r[i])) return null;
            return r;
        }

        // Si tu cancion suena peor que la version de Spotify (Ogg Vorbis a 320 kbps, con los agudos hasta ~20 kHz). 'motivo', para la bitacora.
        internal bool SuenaPeor(string ruta, out string motivo)
        {
            motivo = null;
            float corte = 0f;
            try { float[] c; if (eqCache.TryGetValue(ruta, out c) && c != null && c.Length > EqCut) corte = c[EqCut]; } catch { }
            bool conCorte = corte > 100f;
            InfoMp3 i = InfoDe(ruta);
            bool preEco = false; try { preEco = PreEcoDe(ruta); } catch { }
            if (conCorte && corte < CorteDanada) { motivo = "agudos cortados en " + (int)corte + " Hz"; return true; }
            if (i.Vbr) i.Kbps = 0;   // en un VBR la tasa media sale de la musica (una tranquila en la mejor calidad da ~110 kbps): solo cuenta el corte
            if (i.Kbps > 0 && i.Kbps < 128) { motivo = i.Kbps + " kbps"; return true; }
            if (i.Hz > 0 && i.Hz < 44100) { motivo = "grabada a " + i.Hz + " Hz"; return true; }
            if (i.Kbps > 0 && i.Kbps <= 128 && (!conCorte || corte < 17500f)) { motivo = i.Kbps + " kbps y agudos hasta " + (conCorte ? (int)corte + " Hz" : "?"); return true; }
            if (preEco && i.Kbps > 0 && i.Kbps <= 192 && conCorte && corte < 19000f) { motivo = "pre-eco, " + i.Kbps + " kbps"; return true; }
            return false;
        }

        // ---------------- B: LAS REGLAS DE LA PAREJA ----------------
        // Las marcas de que es OTRA grabacion (otra version de la misma cancion). Remasterizada, editada para radio o «version original» no
        // cuentan: es la misma grabacion.
        private static readonly string[] MarcasVersion = {
            "en vivo", "live", "en directo", "directo", "en concierto", "concierto", "remix", "rmx", " mix", "mezcla", "acustic", "acoustic",
            "unplugged", "desenchufado", "karaoke", "instrumental", "demo", "cover", "sped up", "slowed", "reverb", "regrabad", "re-grabad",
            "re-record", "rerecord", "taylor s version", "extended", "extendida", "version sinfonica", "sinfonico", "orquesta", "a capella",
            "acapella", "banda sinfonica", "mariachi version", "version banda", "version norteña", "version nortena", "version salsa", "version cumbia" };
        // Las marcas de un titulo (en minusculas y sin acentos, con lo de entre parentesis y tras « - »).
        internal static string MarcasDe(string titulo)
        {
            if (string.IsNullOrEmpty(titulo)) return "";
            string t = " " + Acentos.Quita(titulo).ToLowerInvariant().Replace('\'', ' ').Replace('(', ' ').Replace(')', ' ').Replace('[', ' ').Replace(']', ' ') + " ";
            List<string> m = new List<string>();
            foreach (string w in MarcasVersion)
            {
                string c = w.StartsWith(" ") ? w : w;
                int k = t.IndexOf(c, StringComparison.Ordinal);
                if (k < 0) continue;
                // palabra entera al principio (que «live» no case con «olive», ni «demo» con «demonio»)
                char antes = k > 0 ? t[k - 1] : ' ';
                if (!c.StartsWith(" ") && char.IsLetterOrDigit(antes)) continue;
                if (c == "live" || c == "demo" || c == "cover" || c == "directo" || c == "concierto")
                { int f = k + c.Length; if (f < t.Length && char.IsLetterOrDigit(t[f])) continue; }
                string clave = c.Trim();
                if (clave == "en vivo" || clave == "live" || clave == "en directo" || clave == "directo" || clave == "en concierto" || clave == "concierto") clave = "vivo";
                else if (clave == "acustic" || clave == "acoustic" || clave == "unplugged" || clave == "desenchufado") clave = "acustica";
                else if (clave == "remix" || clave == "rmx" || clave == "mix" || clave == "mezcla" || clave == "extended" || clave == "extendida") clave = "mezcla";
                else if (clave.StartsWith("re-") || clave.StartsWith("regrabad") || clave.StartsWith("rerecord") || clave == "taylor s version") clave = "regrabada";
                else if (clave == "a capella" || clave == "acapella") clave = "acapella";
                if (!m.Contains(clave)) m.Add(clave);
            }
            m.Sort(StringComparer.Ordinal);
            return string.Join(",", m);
        }
        // Las partes del artista («A & B», «A feat. B», «A y B», «A, B», «A x B», «A con B»), normalizadas.
        internal static List<string> PartesArtista(string artista)
        {
            List<string> r = new List<string>();
            if (string.IsNullOrEmpty(artista)) return r;
            string a = " " + Acentos.Quita(artista).ToLowerInvariant() + " ";
            foreach (string sep in new[] { " feat. ", " feat ", " ft. ", " ft ", " featuring ", " con ", " y ", " & ", " x ", ", ", " / ", ";" }) a = a.Replace(sep, "|");
            foreach (string p in a.Split('|')) { string n = Normaliza(p); if (n.Length > 0 && !r.Contains(n)) r.Add(n); }
            return r;
        }
        // El artista principal (el primero) igual a alguno de los de Spotify, palabra por palabra.
        internal static bool MismoArtista(string local, List<string> deSpotify)
        {
            List<string> l = PartesArtista(local);
            if (l.Count == 0 || deSpotify.Count == 0) return false;
            if (deSpotify.Contains(l[0])) return true;
            return Normaliza(local) == string.Join(" ", deSpotify);   // «A y B» escrito como un solo nombre
        }

        // ---------------- C: COMPROBAR CON EL SONIDO ----------------
        private string verifRuta, verifId; private PlayItem verifItem; private double[] verifE; private long[] verifN;
        private float verifTramoS; private bool verifHecha; private volatile bool verifMidiendo; private long verifCuadros;
        // Al empezar a sonar una tuya desde Spotify (SP2): empieza a apuntar su volumen y pide la forma de tu MP3.
        private void EmpiezaVerificacion(PlayItem it, string id)
        {
            verifRuta = null; verifItem = null;
            Pareja p = ParejaDe(it.Song.Path);
            if (p == null || p.Verif != 0) return;   // ya decidida
            float seg = SegundosDe(it.Song.Path); if (!(seg > 15f)) return;
            verifRuta = it.Song.Path; verifId = id; verifItem = it; verifHecha = false;
            lock (audioDatosLock) { verifE = new double[TramosForma]; verifN = new long[TramosForma]; verifTramoS = seg / TramosForma; verifCuadros = 0; verifMidiendo = true; }
            FormaDe(verifRuta);   // (en su hilo: la forma de tu MP3)
        }
        // Cada vuelta (AtiendeSpotify): la energia de lo que se leyo desde la ultima vez, al tramo donde va.
        private void SigueVerificacion()
        {
            if (verifRuta == null) return;
            bool sigue = object.ReferenceEquals(nowPlaying, verifItem) && object.ReferenceEquals(spotifySustituida, verifItem);
            if (sigue) { FormaDe(verifRuta); return; }   // (si otra la estaba midiendo, que se pida de nuevo)
            // acabo (o se quito): se decide con lo apuntado
            string ruta = verifRuta; verifRuta = null; verifItem = null;
            lock (audioDatosLock) verifMidiendo = false;
            float[] forma = FormaDe(ruta);
            if (forma == null) return;   // aun sin la forma de tu MP3: la proxima vez
            double r; int usados;
            string veredicto = Compara(forma, verifE, verifN, out r, out usados);
            Pareja p = ParejaDe(ruta);
            SpotifyEnVivo sp = spotify;
            if (sp != null) sp.Apunta("comprobada con el sonido: " + Path.GetFileName(ruta) + " -> " + veredicto + " (r " + r.ToString("0.00", CultureInfo.InvariantCulture) + ", " + usados + " tramos; con sonido " + Cuenta(verifN) + ", con forma " + CuentaForma(forma) + ")");
            if (p == null || p.Id != verifId) return;
            if (veredicto == "misma") { p.Verif = 1; ApuntaPareja(ruta, p); }
            else if (veredicto == "otra") { p.Verif = -1; ApuntaPareja(ruta, p); }
        }
        // En el hilo del sonido (MideBloqueOriginal, con audioDatosLock): el volumen de lo que entra a la cadena, por tramos contados
        // en muestras desde el principio (como MideForma: el promedio de los canales, al cuadrado).
        private void VerifMide(float[] b, int o, int n, int tasa, int ch)
        {
            double[] e = verifE; long[] cu = verifN; if (e == null || cu == null || ch < 1 || tasa <= 0) return;
            long porTramo = Math.Max(1, (long)(verifTramoS * tasa));
            int cuadros = n / ch; double suma = 0;
            for (int i = 0; i < cuadros; i++) { float x = 0; for (int c = 0; c < ch; c++) x += b[o + i * ch + c]; x /= ch; suma += x * x; }
            int k = (int)(verifCuadros / porTramo);
            if (k >= 0 && k < e.Length) { e[k] += suma; cu[k] += cuadros; }
            verifCuadros += cuadros;
        }
        private static int Cuenta(long[] n) { int c = 0; foreach (long x in n) if (x > 0) c++; return c; }
        private static int CuentaForma(float[] f) { int c = 0; foreach (float x in f) if (x > 0.05f) c++; return c; }
        // «misma», «otra» o «no se sabe» (poca musica o la forma muy plana para decidir).
        internal static string Compara(float[] forma, double[] e, long[] n, out double mejor, out int usados)
        {
            mejor = -1; usados = 0;
            int L = Math.Min(forma.Length, e.Length);
            double[] a = new double[L], b = new double[L]; bool[] hay = new bool[L];
            for (int k = 0; k < L; k++)
            {
                if (n[k] <= 0 || forma[k] <= 0.05f) continue;   // sin dato o casi silencio en tu MP3
                a[k] = (forma[k] - 1f) * 36.0;                   // la forma va de -36 dB (0) a lo mas fuerte (1)
                b[k] = 10 * Math.Log10(Math.Max(1e-12, e[k] / n[k]));
                hay[k] = true;
            }
            for (int lag = -3; lag <= 3; lag++)
            {
                double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0; int m = 0;
                for (int k = 0; k < L; k++)
                {
                    int j = k + lag; if (j < 0 || j >= L || !hay[k] || !hay[j]) continue;
                    // los tramos del principio y el final de lo que sono pueden estar a medias: fuera
                    sa += a[k]; sb += b[j]; saa += a[k] * a[k]; sbb += b[j] * b[j]; sab += a[k] * b[j]; m++;
                }
                if (m < 40) continue;
                double va = saa - sa * sa / m, vb = sbb - sb * sb / m;
                if (va / m < 2.25 || vb <= 0) continue;   // la forma de tu MP3 casi plana (menos de 1,5 dB de vaiven): no se puede decidir
                double r = (sab - sa * sb / m) / Math.Sqrt(va * vb);
                if (r > mejor) { mejor = r; usados = m; }
            }
            if (usados == 0) return "no se sabe";
            if (mejor >= 0.80) return "misma";
            if (mejor < 0.55) return "otra";
            return "no se sabe";
        }

        // (pruebas: --prueba-sp2 [mp3...]) las reglas de B y la comparacion de C con casos inventados, y lo que se lee de cada MP3
        public static int PruebaSustitucion(string[] mp3)
        {
            int mal = 0;
            Action<bool, string> ok = (c, que) => { Console.WriteLine((c ? "  bien  " : "  MAL   ") + que); if (!c) mal++; };
            ok(MarcasDe("Amor Eterno") == "", "sin marcas");
            ok(MarcasDe("Amor Eterno (En Vivo)") == "vivo", "(En Vivo)");
            ok(MarcasDe("Amor Eterno - Live at Bellas Artes") == "vivo", "- Live at");
            ok(MarcasDe("Amor Eterno - Remasterizado 2015") == "", "remasterizada = la misma grabacion");
            ok(MarcasDe("Oliver") == "", "«olive» no es «live»");
            ok(MarcasDe("El Demonio") == "", "«demonio» no es «demo»");
            ok(MarcasDe("Cancion (Acoustic Version)") == "acustica", "acustica");
            ok(MarcasDe("Cancion - Remix") == MarcasDe("Cancion [Club Mix]"), "remix = mix");
            ok(MarcasDe("Cancion (Radio Edit)") == "", "radio edit = la misma");
            List<string> sa = new List<string> { Normaliza("Los Angeles Azules") };
            ok(!MismoArtista("Los Ángeles", sa), "«Los Angeles» no es «Los Angeles Azules»");
            ok(MismoArtista("Los Ángeles Azules", sa), "mismo artista con acento");
            ok(MismoArtista("Los Ángeles Azules feat. Natalia Lafourcade", new List<string> { Normaliza("Los Angeles Azules"), Normaliza("Natalia Lafourcade") }), "con invitado");
            ok(MismoArtista("Natalia Lafourcade", new List<string> { Normaliza("Los Angeles Azules"), Normaliza("Natalia Lafourcade") }), "etiquetada con la invitada: es uno de los artistas de la grabacion");
            ok(!MismoArtista("Natalia", new List<string> { Normaliza("Natalia Lafourcade") }), "«Natalia» no es «Natalia Lafourcade»");
            ok(LimpiaIsrc("MX-A12-99-00001") == "MXA129900001", "ISRC con guiones");
            ok(LimpiaIsrc("hola") == null, "ISRC invalido");
            // C: la misma forma con ruido y otro nivel (misma), una con otra estructura (otra), una plana (no se sabe)
            Random r = new Random(7);
            float[] forma = new float[TramosForma]; double[] e = new double[TramosForma], e2 = new double[TramosForma], e3 = new double[TramosForma]; long[] n = new long[TramosForma];
            for (int k = 0; k < TramosForma; k++)
            {
                double db = -12 + 9 * Math.Sin(k / 7.0) + 4 * Math.Sin(k / 2.3);   // la cancion: estrofas y coros
                forma[k] = (float)Math.Max(0.06, 1 + db / 36.0);
                n[k] = k < 150 ? 44100 : 0;   // sonaron 150 tramos
                e[k] = Math.Pow(10, (db + 5 + (r.NextDouble() - 0.5) * 2) / 10) * n[k];   // otro volumen (Spotify) y algo de diferencia
                double otro = -12 + 9 * Math.Sin(k / 3.1 + 2) + 4 * Math.Cos(k / 5.0);
                e2[k] = Math.Pow(10, otro / 10) * n[k];
            }
            double rr; int u;
            string v1 = Compara(forma, e, n, out rr, out u); ok(v1 == "misma", "la misma grabacion: " + v1 + " (r " + rr.ToString("0.00", CultureInfo.InvariantCulture) + ")");
            string v2 = Compara(forma, e2, n, out rr, out u); ok(v2 == "otra", "otra grabacion: " + v2 + " (r " + rr.ToString("0.00", CultureInfo.InvariantCulture) + ")");
            // corrida 2 tramos (la medida va un poco por delante)
            double[] e4 = new double[TramosForma]; for (int k = 2; k < TramosForma; k++) e4[k] = e[k - 2];
            string v4 = Compara(forma, e4, n, out rr, out u); ok(v4 == "misma", "la misma, corrida 2 tramos: " + v4);
            float[] plana = new float[TramosForma]; for (int k = 0; k < TramosForma; k++) plana[k] = 0.8f;
            string v3 = Compara(plana, e, n, out rr, out u); ok(v3 == "no se sabe", "forma plana: " + v3);
            long[] poco = new long[TramosForma]; for (int k = 0; k < 20; k++) poco[k] = 44100;
            string v5 = Compara(forma, e, poco, out rr, out u); ok(v5 == "no se sabe", "solo 20 tramos: " + v5);
            foreach (string f in mp3) { InfoMp3 i = LeeInfoMp3(f); Console.WriteLine("  " + Path.GetFileName(f) + ": " + i.Kbps + " kbps" + (i.Vbr ? " (VBR)" : "") + ", " + i.Hz + " Hz, ISRC " + (i.Isrc ?? "-")); }
            Console.WriteLine(mal == 0 ? "todo bien" : mal + " MAL");
            return mal;
        }
    }
}
