// TIPOS DE LA ROCOLA QUE LA CADENA NECESITA, sacados tal cual de XP: Track (Form1.Vistas.cs), IAudioSource (Form1.Sonido.cs), Album
// (Form1.Buscador.cs) y PlayItem (Form1.Video.cs).
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // ==========  ETIQUETAS (ID3)  ==========
        private class Track
        {
            public string Path;
            public string Artist;
            public string Title;
            public string Album;
            public string Genre;

            // En la lista del disco se muestra EXACTAMENTE el nombre del archivo (sin extension), tal
            // cual esta en la carpeta: cero adivinanzas. Title/Artist se siguen usando por dentro para
            // "Sonando:", el color por genero y el buscador.
            public override string ToString()
            {
                try { return System.IO.Path.GetFileNameWithoutExtension(Path); }
                catch { return Title; }
            }

            private static readonly string[] Id3v1Genres = {
                "Blues","Classic Rock","Country","Dance","Disco","Funk","Grunge","Hip-Hop","Jazz","Metal",
                "New Age","Oldies","Other","Pop","R&B","Rap","Reggae","Rock","Techno","Industrial",
                "Alternative","Ska","Death Metal","Pranks","Soundtrack","Euro-Techno","Ambient","Trip-Hop","Vocal","Jazz+Funk",
                "Fusion","Trance","Classical","Instrumental","Acid","House","Game","Sound Clip","Gospel","Noise",
                "AlternRock","Bass","Soul","Punk","Space","Meditative","Instrumental Pop","Instrumental Rock","Ethnic","Gothic",
                "Darkwave","Techno-Industrial","Electronic","Pop-Folk","Eurodance","Dream","Southern Rock","Comedy","Cult","Gangsta",
                "Top 40","Christian Rap","Pop/Funk","Jungle","Native American","Cabaret","New Wave","Psychadelic","Rave","Showtunes",
                "Trailer","Lo-Fi","Tribal","Acid Punk","Acid Jazz","Polka","Retro","Musical","Rock & Roll","Hard Rock"
            };

            private static string NormalizeGenre(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return "";
                raw = raw.Trim();
                if (raw.Length > 0 && raw[0] == '(')
                {
                    int close = raw.IndexOf(')');
                    if (close > 1)
                    {
                        string num = raw.Substring(1, close - 1);
                        int n;
                        if (int.TryParse(num, out n) && n >= 0 && n < Id3v1Genres.Length)
                        {
                            string rest = raw.Substring(close + 1).Trim();
                            if (rest.Length > 0) return rest;
                            return Id3v1Genres[n];
                        }
                    }
                }
                int num2;
                if (int.TryParse(raw, out num2) && num2 >= 0 && num2 < Id3v1Genres.Length) return Id3v1Genres[num2];
                return raw;
            }

            // RAPIDO: no abre el archivo. Titulo/artista salen del nombre y la carpeta.
            // (Con +100k canciones, abrir cada MP3 para leer ID3 era lo que tardaba.)
            public static Track FromFile(string path)
            {
                Track t = new Track();
                t.Path = Pool(path); t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                string fa, ft;
                bool split = SplitArtist(name, out fa, out ft);
                if (split && AllDigits(fa))
                {
                    // "01 - X - Y": quitar el numero de pista y reanalizar -> artista X, titulo Y.
                    string fa2, ft2;
                    if (SplitArtist(ft, out fa2, out ft2) && !AllDigits(fa2)) { t.Artist = fa2; t.Title = ft2; }
                    else t.Title = name;   // "01 - Titulo": igual que siempre
                }
                else if (split) { t.Artist = fa; t.Title = ft; }
                else { t.Title = name; }
                if (string.IsNullOrEmpty(t.Artist))
                {
                    // El artista que sale de la CARPETA es el mismo para todas las canciones de ella (y llegan seguidas): se saca una vez por
                    // carpeta y las demas comparten la misma cadena, en vez de armar cada vez GetDirectoryName, LeafName y los pedazos de
                    // SplitArtist (varios objetos por cancion, y uno retenido por cada una). Solo si el comienzo de la ruta, hasta el ultimo
                    // separador, es letra por letra el de la carpeta recordada: GetDirectoryName depende de ese comienzo y de nada mas.
                    int corte = path.LastIndexOfAny(SepChars);
                    ArtistaDeCarpeta ultima = ultimaCarpeta;
                    if (corte > 0 && ultima != null && ultima.Corte == corte && string.CompareOrdinal(path, 0, ultima.Prefijo, 0, corte) == 0)
                        t.Artist = ultima.Artista;
                    else
                    {
                        string d = System.IO.Path.GetDirectoryName(path);
                        string leaf = (d != null) ? LeafName(d) : "Desconocido";
                        string da, dn;
                        if (SplitArtist(leaf, out da, out dn)) t.Artist = da; else t.Artist = leaf;
                        if (corte > 0) ultimaCarpeta = new ArtistaDeCarpeta(path.Substring(0, corte), t.Artist);
                    }
                }
                return t;
            }
            private sealed class ArtistaDeCarpeta
            {
                internal readonly string Prefijo, Artista; internal readonly int Corte;
                internal ArtistaDeCarpeta(string prefijo, string artista) { Prefijo = prefijo; Artista = artista; Corte = prefijo.Length; }
            }
            private static volatile ArtistaDeCarpeta ultimaCarpeta;   // una sola referencia: leer y cambiar es atomico entre hilos

            // EL GENERO A SOLAS, LEYENDO LO MENOS POSIBLE.
            //
            // La etiqueta de texto de un MP3 puede llevar la CARATULA dentro y medir megas. Leerla entera para
            // sacar UNA palabra es tirar el disco a la basura, y el paseo de fondo hacia eso con TODAS las
            // canciones de la biblioteca: con veinte mil archivos son gigas de lectura inutil.
            //
            // La etiqueta va por MARCOS, y cada marco dice de antemano cuanto mide. Aqui se leen solo las diez
            // letras de cabecera de cada marco y se SALTAN los que no interesan -la caratula, sobre todo- sin
            // llegar a leerlos. De un archivo con caratula de 300 KB se leen unos cientos de bytes. Si la
            // etiqueta larga no trae genero, se prueba la corta del final (128 bytes); y si tampoco, se queda
            // sin genero, que es decoracion, en vez de leerse el archivo entero.
            private static int SyncSafe4(byte[] b, int p)
            {
                if (b == null || p < 0 || p + 4 > b.Length) return -1;
                if ((b[p] | b[p + 1] | b[p + 2] | b[p + 3]) >= 128) return -1;
                return (b[p] << 21) | (b[p + 1] << 14) | (b[p + 2] << 7) | b[p + 3];
            }
            private static int BigEndian4(byte[] b, int p)
            {
                if (b == null || p < 0 || p + 4 > b.Length) return -1;
                uint v = ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3];
                return v > int.MaxValue ? -1 : (int)v;
            }
            private static byte[] DeUnsync(byte[] b, int start, int size)
            {
                if (b == null || start < 0 || size < 0 || start + size > b.Length) return new byte[0];
                byte[] d = new byte[size]; int n = 0, end = start + size;
                for (int i = start; i < end; i++)
                {
                    byte v = b[i]; d[n++] = v;
                    if (v == 0xff && i + 1 < end && b[i + 1] == 0x00) i++;
                }
                if (n == d.Length) return d;
                byte[] exact = new byte[n]; Array.Copy(d, 0, exact, 0, n); return exact;
            }


            // Una sola lectura acotada para la metadata del corpus; no cargar portada ni adivinar etiquetas.
            public static void SoloArtistaGenero(string path, out string artista, out string genero)
            {
                Track t = new Track();
                t.Path = path; t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                artista = ""; genero = "";
                try
                {
                    if (System.IO.Path.GetExtension(path).ToLowerInvariant() != ".mp3") return;
                    ReadId3(path, t);
                }
                catch { }
                artista = (t.Artist ?? "").Trim();
                genero = (t.Genre ?? "").Trim();
            }

            public static string SoloGenero(string path)
            {
                Track t = new Track();
                t.Path = path; t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                try
                {
                    if (System.IO.Path.GetExtension(path).ToLowerInvariant() != ".mp3") return "";
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                        ReadGenreStream(fs, t);
                }
                catch { }
                return t.Genre ?? "";
            }

            private static void ReadGenreStream(Stream fs, Track t)
            {
                // Si v2 esta truncada/corrupta, intentar v1 sin abrir otro archivo.
                try
                {
                    fs.Position = 0;
                    byte[] cab = new byte[10];
                    if (ReadFully(fs, cab, 0, 10) == 10 && cab[0] == (byte)'I' && cab[1] == (byte)'D' && cab[2] == (byte)'3'
                        && cab[3] >= 3 && cab[3] <= 4)   // 2.2 y versiones futuras tienen otra forma
                    {
                        int major = cab[3];
                        // En 2.3 la des-sincronizacion cubre el cuerpo entero y cambia sus posiciones.
                        // Es un caso raro: se usa el lector completo, que la revierte antes de separar marcos.
                        if (major <= 3 && (cab[5] & 0x80) != 0)
                        {
                            ReadId3Stream(fs, t);
                            return;
                        }
                        long size = SyncSafe4(cab, 6);
                        long fin = 10 + size;
                        if (size <= 0 || fin > fs.Length) throw new InvalidDataException("ID3 truncado");
                        if ((cab[5] & 0x40) != 0)
                        {
                            byte[] eh = new byte[4]; if (ReadFully(fs, eh, 0, 4) != 4) throw new EndOfStreamException();
                            int es = (major >= 4) ? SyncSafe4(eh, 0) : BigEndian4(eh, 0);
                            long resta = (major >= 4) ? es - 4L : es;
                            if (es < (major >= 4 ? 6 : 0) || resta < 0 || fs.Position + resta > fin) throw new InvalidDataException("ID3 extendido invalido");
                            fs.Position += resta;
                        }
                        byte[] fh = new byte[10];
                        while (fs.Position + 10 <= fin)
                        {
                            if (ReadFully(fs, fh, 0, 10) != 10) break;
                            if (fh[0] == 0) break;   // relleno: se acabaron los marcos
                            int fsz;
                            if (major >= 4) fsz = SyncSafe4(fh, 4);
                            else fsz = BigEndian4(fh, 4);
                            if (fsz <= 0 || fs.Position + fsz > fin) break;
                            bool esGenero = (fh[0] == (byte)'T' && fh[1] == (byte)'C' && fh[2] == (byte)'O' && fh[3] == (byte)'N');
                            if (esGenero && fsz <= 4096)
                            {
                                // Se le pasa el marco entero (cabecera incluida) al mismo lector de siempre,
                                // asi el genero se interpreta exactamente igual que cuando se lee todo.
                                byte[] uno = new byte[10 + fsz];
                                Array.Copy(fh, 0, uno, 0, 10);
                                int leidos = 0, r;
                                while (leidos < fsz && (r = fs.Read(uno, 10 + leidos, fsz - leidos)) > 0) leidos += r;
                                ParseId3v2(uno, 10 + leidos, major, cab[5] & 0x80, t);
                                if (!string.IsNullOrEmpty(t.Genre)) break;
                            }
                            else fs.Position += fsz;   // la caratula y lo demas: ni se tocan
                        }
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(t.Genre)) { try { ReadId3v1Stream(fs, t); } catch { } }
            }

            // Lee el ID3 bajo demanda (una sola vez), sobre todo para el GENERO (color de fondo).
            private bool tagsLoaded = false;
            public bool TagsLoaded { get { return tagsLoaded; } }
            public void EnsureTags()
            {
                if (tagsLoaded) return;
                tagsLoaded = true;
                if (TomaEtiquetaLeida()) return;   // ya leida por adelantado, y sigue valiendo (ver abajo)
                try { if (System.IO.Path.GetExtension(Path).ToLowerInvariant() == ".mp3") ReadId3(Path, this); }
                catch { }
            }

            // LA ETIQUETA DE LA SIGUIENTE, LEIDA POR ADELANTADO (29/09). Leerla al empezar la cancion era abrir el MP3 y
            // leer su cabecera en el hilo de la pantalla, justo al cambiar de cancion: con un disco mecanico ocupado, o con
            // el antivirus mirando cada archivo que se abre, un tiron. Ahora la lectura por adelantado de la siguiente
            // (PrecalientaSiguiente) la lee en su hilo, de los MISMOS bytes que acaba de leer, sobre una COPIA de la cancion
            // hecha en la pantalla (CopiaEtiqueta), y la deja aqui. Al empezar, EnsureTags la pasa a la cancion si:
            //   - es para esta cancion (el mismo objeto) y la cancion sigue como cuando se copio, y
            //   - el archivo no cambio desde que se leyo: tamano y fechas (AudioFileSignature), sin abrirlo ni leerlo.
            // Si no, se lee como siempre. El resultado es el mismo campo por campo: el mismo lector sobre los mismos bytes.
            // Hay UN solo hueco (la siguiente es una): no se agranda cada cancion de la biblioteca.
            private static readonly object etiquetaCerrojo = new object();
            private static Track etiquetaDe, etiquetaAntes, etiquetaLeida;
            private static string etiquetaFirma;
            // En la pantalla: la cancion tal como esta (solo lo que la etiqueta puede cambiar).
            public Track CopiaEtiqueta()
            {
                Track c = new Track();
                c.Path = Path; c.Artist = Artist; c.Title = Title; c.Album = Album; c.Genre = Genre;
                return c;
            }
            // En el hilo de fondo: lo que EnsureTags dejaria en la cancion, leido sobre una copia de 'antes', de 'datos'
            // (el archivo entero ya en memoria) o, si no los hay, del archivo. No toca ninguna cancion de la biblioteca.
            public static Track LeeEtiquetaAparte(Track antes, byte[] datos)
            {
                Track c = antes.CopiaEtiqueta();
                try
                {
                    if (System.IO.Path.GetExtension(c.Path).ToLowerInvariant() == ".mp3")
                    {
                        if (datos != null) { using (MemoryStream ms = new MemoryStream(datos, false)) ReadId3Stream(ms, c); }
                        else ReadId3(c.Path, c);
                    }
                }
                catch { }
                return c;
            }
            // Y la deja lista para 'cancion', con la firma que tenia el archivo ANTES de leerlo.
            public static void PonEtiquetaLeida(Track cancion, Track antes, Track leida, string firma)
            {
                if (cancion == null || antes == null || leida == null || firma == null) return;
                lock (etiquetaCerrojo) { etiquetaDe = cancion; etiquetaAntes = antes; etiquetaLeida = leida; etiquetaFirma = firma; }
            }
            private bool TomaEtiquetaLeida()
            {
                Track antes, leida; string firma;
                lock (etiquetaCerrojo)
                {
                    if (!object.ReferenceEquals(etiquetaDe, this)) return false;
                    antes = etiquetaAntes; leida = etiquetaLeida; firma = etiquetaFirma;
                    etiquetaDe = null; etiquetaAntes = null; etiquetaLeida = null; etiquetaFirma = null;
                }
                if (antes == null || leida == null || firma == null) return false;
                if (!string.Equals(antes.Path, Path, StringComparison.Ordinal) || antes.Artist != Artist || antes.Title != Title
                    || antes.Album != Album || antes.Genre != Genre) return false;
                if (firma != Form1.AudioFileSignature(Path)) return false;   // el archivo cambio: se lee el de ahora
                Artist = leida.Artist; Title = leida.Title; Album = leida.Album; Genre = leida.Genre;
                return true;
            }

            private static void ReadId3(string path, Track t)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    ReadId3Stream(fs, t);
            }

            // Cabecera y etiqueta v1 usan el mismo handle; no reabrir el MP3 al cambiar cancion.
            private static void ReadId3Stream(Stream fs, Track t)
            {
                fs.Position = 0;
                byte[] header = new byte[10];
                if (ReadFully(fs, header, 0, 10) == 10 && header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3'
                    && header[3] >= 3 && header[3] <= 4)
                {
                    int major = header[3];
                    int size = SyncSafe4(header, 6);
                    if (size > 0 && size <= 33554432)   // tope 32MB: un encabezado CORRUPTO puede declarar hasta 256MB y ese new byte[] tumbaria la RAM de la rocola; se cae al ID3v1
                    {
                        int flags = header[5];
                        if (major <= 3 && (flags & 0x80) != 0)
                        {
                            // En 2.3 el escape global cambia posiciones: conservar el lector completo.
                            byte[] tag = new byte[size];
                            int read = ReadFully(fs, tag, 0, size);
                            tag = DeUnsync(tag, 0, read); read = tag.Length; flags &= ~0x80;
                            ParseId3v2(tag, read, major, flags, t);
                        }
                        else ReadId3Frames(fs, size, major, flags, t);
                    }
                }
                if (string.IsNullOrEmpty(t.Title) || string.IsNullOrEmpty(t.Artist) || string.IsNullOrEmpty(t.Album) || string.IsNullOrEmpty(t.Genre))
                    ReadId3v1Stream(fs, t);
            }

            // Lee solo los marcos de texto. Saltar APIC evita leer megabytes de portada al pulsar.
            // El parser existente interpreta los textos/flags; esta rutina solo recorre el archivo.
            private static void ReadId3Frames(Stream fs, int size, int major, int flags, Track t)
            {
                long end = Math.Min(fs.Length, fs.Position + size);
                if ((flags & 0x40) != 0)
                {
                    byte[] ext = new byte[4];
                    if (end - fs.Position < 4 || ReadFully(fs, ext, 0, 4) != 4) return;
                    int es = major >= 4 ? SyncSafe4(ext, 0) : BigEndian4(ext, 0);
                    long remaining = major >= 4 ? es - 4L : es;
                    if (es < (major >= 4 ? 6 : 0) || remaining < 0 || remaining > end - fs.Position) return;
                    fs.Seek(remaining, SeekOrigin.Current);
                }
                byte[] header = new byte[10];
                while (end - fs.Position >= 10)
                {
                    if (ReadFully(fs, header, 0, 10) != 10) return;
                    bool valid = true;
                    for (int i = 0; i < 4; i++)
                        if (!((header[i] >= 'A' && header[i] <= 'Z') || (header[i] >= '0' && header[i] <= '9'))) { valid = false; break; }
                    if (!valid) return;
                    int frameSize = major >= 4 ? SyncSafe4(header, 4) : BigEndian4(header, 4);
                    if (frameSize <= 0 || frameSize > end - fs.Position) return;
                    string id = Encoding.ASCII.GetString(header, 0, 4);
                    if (id == "TIT2" || id == "TPE1" || id == "TALB" || id == "TCON")
                    {
                        byte[] frame = new byte[10 + frameSize];
                        Array.Copy(header, frame, 10);
                        if (ReadFully(fs, frame, 10, frameSize) != frameSize) return;
                        ParseId3v2(frame, frame.Length, major, flags & 0x80, t);
                    }
                    else fs.Seek(frameSize, SeekOrigin.Current);
                }
            }

            private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
            {
                int done = 0, read;
                while (done < count && (read = stream.Read(buffer, offset + done, count - done)) > 0) done += read;
                return done;
            }

            private static void ParseId3v2(byte[] tag, int len, int major, Track t)
            { ParseId3v2(tag, len, major, 0, t); }

            private static void ParseId3v2(byte[] tag, int len, int major, int tagFlags, Track t)
            {
                int pos = 0;
                if ((tagFlags & 0x40) != 0)
                {
                    if (len < 4) return;
                    int es = (major >= 4) ? SyncSafe4(tag, 0) : BigEndian4(tag, 0);
                    int skip = (major >= 4) ? es : (es < 0 ? -1 : es + 4);
                    if (es < (major >= 4 ? 6 : 0) || skip < 4 || skip > len) return;
                    pos = skip;
                }
                while (pos + 10 <= len)
                {
                    string id = Encoding.ASCII.GetString(tag, pos, 4);
                    if (id[0] == '\0') break;
                    bool valid = true;
                    for (int i = 0; i < 4; i++) { char c = id[i]; if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) { valid = false; break; } }
                    if (!valid) break;
                    int frameSize;
                    if (major >= 4) frameSize = SyncSafe4(tag, pos + 4);
                    else frameSize = BigEndian4(tag, pos + 4);
                    int startPos = pos + 10;
                    if (frameSize <= 0 || startPos + frameSize > len) break;
                    if (id == "TIT2" || id == "TPE1" || id == "TALB" || id == "TCON")
                    {
                        int formatFlags = tag[pos + 9];
                        bool compressed = major >= 4 ? (formatFlags & 0x08) != 0 : (formatFlags & 0x80) != 0;
                        bool encrypted = major >= 4 ? (formatFlags & 0x04) != 0 : (formatFlags & 0x40) != 0;
                        byte[] body = tag; int bs = startPos, bn = frameSize;
                        if (major >= 4 && (((tagFlags & 0x80) != 0) || (formatFlags & 0x02) != 0))
                        { body = DeUnsync(tag, startPos, frameSize); bs = 0; bn = body.Length; }
                        if (major >= 4)
                        {
                            if ((formatFlags & 0x40) != 0) { bs++; bn--; }       // identidad de grupo
                            if ((formatFlags & 0x01) != 0) { bs += 4; bn -= 4; } // indicador de longitud
                        }
                        else if ((formatFlags & 0x20) != 0) { bs++; bn--; }
                        string val = (!compressed && !encrypted && bn > 0 && bs >= 0 && bs + bn <= body.Length)
                            ? DecodeText(body, bs, bn) : "";
                        if (val != null) val = val.Trim();
                        bool has = !string.IsNullOrEmpty(val);
                        // El ID3 MANDA sobre titulo/artista: el nombre de archivo es solo una adivinanza
                        // (en discos de varios artistas suele venir al reves, o sin titulo siquiera).
                        if (id == "TIT2" && has) t.Title = val;
                        else if (id == "TPE1" && has) t.Artist = val;
                        else if (id == "TALB" && has && string.IsNullOrEmpty(t.Album)) t.Album = val;
                        else if (id == "TCON" && has && string.IsNullOrEmpty(t.Genre)) t.Genre = NormalizeGenre(val);
                    }
                    pos = startPos + frameSize;
                }
            }

            private static string DecodeText(byte[] b, int start, int size)
            {
                if (size < 1) return "";
                byte enc = b[start];
                int s = start + 1, n = size - 1;
                string result;
                switch (enc)
                {
                    case 1:
                        if (n >= 2 && b[s] == 0xFF && b[s + 1] == 0xFE) result = Encoding.Unicode.GetString(b, s + 2, n - 2);
                        else if (n >= 2 && b[s] == 0xFE && b[s + 1] == 0xFF) result = Encoding.BigEndianUnicode.GetString(b, s + 2, n - 2);
                        else result = Encoding.Unicode.GetString(b, s, n);
                        break;
                    case 2: result = Encoding.BigEndianUnicode.GetString(b, s, n); break;
                    case 3: result = Encoding.UTF8.GetString(b, s, n); break;
                    default: result = Encoding.GetEncoding(28591).GetString(b, s, n); break;
                }
                int z = result.IndexOf('\0');
                if (z >= 0) result = result.Substring(0, z);
                return result.Trim();
            }

            private static void ReadId3v1Stream(Stream fs, Track t)
            {
                if (fs.Length < 128) return;
                fs.Seek(-128, SeekOrigin.End);
                byte[] b = new byte[128];
                if (ReadFully(fs, b, 0, 128) != 128) return;
                if (!(b[0] == (byte)'T' && b[1] == (byte)'A' && b[2] == (byte)'G')) return;
                Encoding latin = Encoding.GetEncoding(28591);
                char[] tc = new char[] { '\0', ' ' };
                string title = latin.GetString(b, 3, 30).Trim(tc);
                string artist = latin.GetString(b, 33, 30).Trim(tc);
                string album = latin.GetString(b, 63, 30).Trim(tc);
                if (string.IsNullOrEmpty(t.Title) && title.Length > 0) t.Title = title;
                if (string.IsNullOrEmpty(t.Artist) && artist.Length > 0) t.Artist = artist;
                if (string.IsNullOrEmpty(t.Album) && album.Length > 0) t.Album = album;
                int gi = b[127];
                if (string.IsNullOrEmpty(t.Genre) && gi >= 0 && gi < Id3v1Genres.Length) t.Genre = Id3v1Genres[gi];
            }
        }
        // Fuente de audio para reproducir: expone lo que la rocola necesita (muestras float para el tap + medicion,
        // volumen, y tiempo actual/total para la barra de progreso). Dos implementaciones: la NORMAL (AudioFileReader,
        // cualquier formato) y la TOLERANTE (MP3 con frecuencia mezclada que NAudio rechazaria).
        private interface IAudioSource : NAudio.Wave.ISampleProvider, IDisposable
        {
            float Volume { set; }
            TimeSpan CurrentTime { get; }
            TimeSpan TotalTime { get; }
            void Salta(TimeSpan donde);   // retomar a media cancion (volvio el aparato de sonido; SalidaSonido.cs)
        }
        private class Album
        {
            public string Folder;
            public string Name;
            public string Artist;
            public char Letter;
            public string CoverPath;
            public List<Track> Tracks = new List<Track>();
            public List<string> VideoFiles = new List<string>();
            public bool IsTop = false;             // disco virtual de "lo mas sonado" por genero
            public bool TopIsVideo = false;        // (TOP) este disco virtual contiene VIDEOS, no canciones
            public string KeyCache, KeyFrom;       // clave de la casilla ya armada (ver CellKey): se pide en cada cuadro
            public string KeyQ, KeyQDe;            // y su variante con el palomito (ver ClaveCasilla), tambien armada una vez
            public List<string> CycleCovers;       // (TOP) caratulas distintas para ciclar en el cubo seleccionado
            public List<string> TrackCovers;       // (TOP) caratula de origen por cada cancion (alineado con Tracks)
            public List<string> TrackInfo;         // (TOP) "Artista - Album" de origen por cada cancion
            public List<Album> TrackAlbums;        // (TOP) disco de origen por cada cancion (para "sonando")
        }
        // Un elemento de la cola: una cancion O un video. Disc = disco para el cubo de abajo.
        private class PlayItem
        {
            public bool IsVideo;
            public Track Song;
            public string VideoPath;
            public Album Disc;
            public string PathKey()
            {
                if (IsVideo) return VideoPath ?? "";
                return (Song != null && Song.Path != null) ? Song.Path : "";
            }
            public string Display()
            {
                if (IsVideo)
                {
                    // Mostrar "album - nombre del video": el disco (carpeta) MAS el nombre del archivo.
                    string disc = (Disc != null && !string.IsNullOrEmpty(Disc.Artist)) ? Disc.Artist : "";
                    string file = "";
                    try { file = System.IO.Path.GetFileNameWithoutExtension(VideoPath); } catch { }
                    string n;
                    if (disc.Length > 0 && file.Length > 0) n = disc + " - " + file;
                    else if (file.Length > 0) n = file;
                    else n = disc;
                    return "\u25BA Video:  " + n;
                }
                return (Song != null) ? (Song.Artist + " - " + Song.Title) : "";
            }

            // Lo mismo, pero PARTIDO: arriba lo que suena y debajo de quien es. La franja de abajo los pone en
            // dos renglones distintos, con distinto tamano y distinto color, asi que no vale con una sola
            // cadena "Artista - Titulo" cortada por el guion (hay titulos con guiones).
            public void TituloArtista(out string titulo, out string artista)
            {
                if (IsVideo)
                {
                    string disc = (Disc != null && !string.IsNullOrEmpty(Disc.Artist)) ? Disc.Artist : "";
                    string file = "";
                    try { file = System.IO.Path.GetFileNameWithoutExtension(VideoPath); } catch { }
                    titulo = (file.Length > 0) ? file : disc;
                    artista = "\u25BA Video" + ((disc.Length > 0 && file.Length > 0) ? ("  -  " + disc) : "");
                    return;
                }
                titulo = (Song != null && !string.IsNullOrEmpty(Song.Title)) ? Song.Title : "";
                artista = (Song != null && !string.IsNullOrEmpty(Song.Artist)) ? Song.Artist : "";
            }
        }
    }
}
