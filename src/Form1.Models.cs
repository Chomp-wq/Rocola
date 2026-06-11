using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        private class PlayItem
        {
            public bool IsVideo;
            public Track Song;
            public string VideoPath;
            public Album Disc;

            public string PathKey()
            {
                if (IsVideo)
                {
                    return VideoPath ?? "";
                }
                if (Song == null || Song.Path == null)
                {
                    return "";
                }
                return Song.Path;
            }

            public string Display()
            {
                if (IsVideo)
                {
                    string name = (Disc != null && !string.IsNullOrEmpty(Disc.Artist)) ? Disc.Artist : "";
                    try
                    {
                        if (name.Length == 0)
                        {
                            name = System.IO.Path.GetFileNameWithoutExtension(VideoPath);
                        }
                    }
                    catch
                    {
                    }
                    return "► Video:  " + name;
                }
                if (Song == null)
                {
                    return "";
                }
                return Song.Artist + " - " + Song.Title;
            }
        }

        private class VideoItem
        {
            public string Path;

            public VideoItem(string p)
            {
                Path = p;
            }

            public override string ToString()
            {
                try
                {
                    return System.IO.Path.GetFileNameWithoutExtension(Path);
                }
                catch
                {
                    return Path;
                }
            }
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
        }

        private class Track
        {
            public string Path;
            public string Artist;
            public string Title;
            public string Album;
            public string Genre;

            private static readonly string[] Id3v1Genres = new string[80]
            {
                "Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop", "Jazz", "Metal",
                "New Age", "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae", "Rock", "Techno", "Industrial",
                "Alternative", "Ska", "Death Metal", "Pranks", "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz+Funk",
                "Fusion", "Trance", "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip", "Gospel", "Noise",
                "AlternRock", "Bass", "Soul", "Punk", "Space", "Meditative", "Instrumental Pop", "Instrumental Rock", "Ethnic", "Gothic",
                "Darkwave", "Techno-Industrial", "Electronic", "Pop-Folk", "Eurodance", "Dream", "Southern Rock", "Comedy", "Cult", "Gangsta",
                "Top 40", "Christian Rap", "Pop/Funk", "Jungle", "Native American", "Cabaret", "New Wave", "Psychadelic", "Rave", "Showtunes",
                "Trailer", "Lo-Fi", "Tribal", "Acid Punk", "Acid Jazz", "Polka", "Retro", "Musical", "Rock & Roll", "Hard Rock"
            };

            private bool tagsLoaded;

            public override string ToString()
            {
                return Title;
            }

            private static string NormalizeGenre(string raw)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    return "";
                }
                raw = raw.Trim();
                if (raw.Length > 0 && raw[0] == '(')
                {
                    int close = raw.IndexOf(')');
                    int id;
                    if (close > 1 && int.TryParse(raw.Substring(1, close - 1), out id) && id >= 0 && id < Id3v1Genres.Length)
                    {
                        string rest = raw.Substring(close + 1).Trim();
                        if (rest.Length > 0)
                        {
                            return rest;
                        }
                        return Id3v1Genres[id];
                    }
                }
                int id2;
                if (int.TryParse(raw, out id2) && id2 >= 0 && id2 < Id3v1Genres.Length)
                {
                    return Id3v1Genres[id2];
                }
                return raw;
            }

            public static Track FromFile(string path)
            {
                Track track = new Track();
                track.Path = path;
                track.Artist = "";
                track.Title = "";
                track.Album = "";
                track.Genre = "";
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                string left;
                string right;
                if (SplitArtist(fileName, out left, out right) && !AllDigits(left))
                {
                    track.Artist = left;
                    track.Title = right;
                }
                else
                {
                    track.Title = fileName;
                }
                if (string.IsNullOrEmpty(track.Artist))
                {
                    string dir = System.IO.Path.GetDirectoryName(path);
                    string folder = (dir != null) ? LeafName(dir) : "Desconocido";
                    string folderArtist;
                    string ignored;
                    if (SplitArtist(folder, out folderArtist, out ignored))
                    {
                        track.Artist = folderArtist;
                    }
                    else
                    {
                        track.Artist = folder;
                    }
                }
                return track;
            }

            public void EnsureTags()
            {
                if (tagsLoaded)
                {
                    return;
                }
                tagsLoaded = true;
                try
                {
                    if (System.IO.Path.GetExtension(Path).ToLowerInvariant() == ".mp3")
                    {
                        ReadId3(Path, this);
                    }
                }
                catch
                {
                }
            }

            private static void ReadId3(string path, Track t)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[10];
                    if (fs.Read(header, 0, 10) == 10 && header[0] == 'I' && header[1] == 'D' && header[2] == '3')
                    {
                        int major = header[3];
                        int size = ((header[6] & 0x7F) << 21) | ((header[7] & 0x7F) << 14) | ((header[8] & 0x7F) << 7) | (header[9] & 0x7F);
                        byte[] tag = new byte[size];
                        int read = 0;
                        int n;
                        while (read < size && (n = fs.Read(tag, read, size - read)) > 0)
                        {
                            read += n;
                        }
                        ParseId3v2(tag, read, major, t);
                    }
                }
                if (string.IsNullOrEmpty(t.Title) || string.IsNullOrEmpty(t.Artist) || string.IsNullOrEmpty(t.Album) || string.IsNullOrEmpty(t.Genre))
                {
                    ReadId3v1(path, t);
                }
            }

            private static void ParseId3v2(byte[] tag, int len, int major, Track t)
            {
                int pos = 0;
                while (pos + 10 <= len)
                {
                    string id = Encoding.ASCII.GetString(tag, pos, 4);
                    if (id[0] == '\0')
                    {
                        break;
                    }
                    bool valid = true;
                    for (int i = 0; i < 4; i++)
                    {
                        char c = id[i];
                        if ((c < 'A' || c > 'Z') && (c < '0' || c > '9'))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid)
                    {
                        break;
                    }
                    int frameSize = (major < 4)
                        ? ((tag[pos + 4] << 24) | (tag[pos + 5] << 16) | (tag[pos + 6] << 8) | tag[pos + 7])
                        : (((tag[pos + 4] & 0x7F) << 21) | ((tag[pos + 5] & 0x7F) << 14) | ((tag[pos + 6] & 0x7F) << 7) | (tag[pos + 7] & 0x7F));
                    int dataStart = pos + 10;
                    if (frameSize <= 0 || dataStart + frameSize > len)
                    {
                        break;
                    }
                    if (id == "TIT2" || id == "TPE1" || id == "TALB" || id == "TCON")
                    {
                        string text = DecodeText(tag, dataStart, frameSize);
                        if (id == "TIT2" && string.IsNullOrEmpty(t.Title))
                        {
                            t.Title = text;
                        }
                        else if (id == "TPE1" && string.IsNullOrEmpty(t.Artist))
                        {
                            t.Artist = text;
                        }
                        else if (id == "TALB" && string.IsNullOrEmpty(t.Album))
                        {
                            t.Album = text;
                        }
                        else if (id == "TCON" && string.IsNullOrEmpty(t.Genre))
                        {
                            t.Genre = NormalizeGenre(text);
                        }
                    }
                    pos = dataStart + frameSize;
                }
            }

            private static string DecodeText(byte[] b, int start, int size)
            {
                if (size < 1)
                {
                    return "";
                }
                byte enc = b[start];
                int pos = start + 1;
                int len = size - 1;
                string text;
                if (enc == 1)
                {
                    if (len >= 2 && b[pos] == 0xFF && b[pos + 1] == 0xFE)
                    {
                        text = Encoding.Unicode.GetString(b, pos + 2, len - 2);
                    }
                    else if (len >= 2 && b[pos] == 0xFE && b[pos + 1] == 0xFF)
                    {
                        text = Encoding.BigEndianUnicode.GetString(b, pos + 2, len - 2);
                    }
                    else
                    {
                        text = Encoding.Unicode.GetString(b, pos, len);
                    }
                }
                else if (enc == 2)
                {
                    text = Encoding.BigEndianUnicode.GetString(b, pos, len);
                }
                else if (enc == 3)
                {
                    text = Encoding.UTF8.GetString(b, pos, len);
                }
                else
                {
                    text = Encoding.GetEncoding(28591).GetString(b, pos, len);
                }
                int zero = text.IndexOf('\0');
                if (zero >= 0)
                {
                    text = text.Substring(0, zero);
                }
                return text.Trim();
            }

            private static void ReadId3v1(string path, Track t)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 128)
                    {
                        return;
                    }
                    fs.Seek(-128L, SeekOrigin.End);
                    byte[] tag = new byte[128];
                    if (fs.Read(tag, 0, 128) == 128 && tag[0] == 'T' && tag[1] == 'A' && tag[2] == 'G')
                    {
                        Encoding latin1 = Encoding.GetEncoding(28591);
                        char[] trimChars = new char[2] { '\0', ' ' };
                        string title = latin1.GetString(tag, 3, 30).Trim(trimChars);
                        string artist = latin1.GetString(tag, 33, 30).Trim(trimChars);
                        string album = latin1.GetString(tag, 63, 30).Trim(trimChars);
                        if (string.IsNullOrEmpty(t.Title) && title.Length > 0)
                        {
                            t.Title = title;
                        }
                        if (string.IsNullOrEmpty(t.Artist) && artist.Length > 0)
                        {
                            t.Artist = artist;
                        }
                        if (string.IsNullOrEmpty(t.Album) && album.Length > 0)
                        {
                            t.Album = album;
                        }
                        int genre = tag[127];
                        if (string.IsNullOrEmpty(t.Genre) && genre >= 0 && genre < Id3v1Genres.Length)
                        {
                            t.Genre = Id3v1Genres[genre];
                        }
                    }
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateAudio(string root)
        {
            return SafeEnumerateByExt(root, AudioExt);
        }

        private static IEnumerable<string> SafeEnumerateVideo(string root)
        {
            return SafeEnumerateByExt(root, VideoExt);
        }

        private static IEnumerable<string> SafeEnumerateByExt(string root, string[] extensions)
        {
            Stack<string> dirs = new Stack<string>();
            dirs.Push(root);
            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                string[] files = null;
                try
                {
                    files = Directory.GetFiles(dir);
                }
                catch
                {
                }
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (Array.IndexOf(extensions, ext) >= 0)
                        {
                            yield return file;
                        }
                    }
                }
                string[] subDirs = null;
                try
                {
                    subDirs = Directory.GetDirectories(dir);
                }
                catch
                {
                }
                if (subDirs != null)
                {
                    foreach (string sub in subDirs)
                    {
                        dirs.Push(sub);
                    }
                }
            }
        }
    }
}
