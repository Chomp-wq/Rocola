using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // Resultado completo del escaneo construido en el hilo de fondo.
        // Antes, el armado de discos (busqueda de portadas en disco), la
        // seccion de videos (recorrido completo de carpetas) y el indice de
        // busqueda se hacian en el hilo de UI al terminar el escaneo, lo que
        // congelaba la interfaz con bibliotecas grandes.
        private class ScanOutcome
        {
            public List<Track> Tracks;
            public List<Album> Albums;
            public List<Album> VideoAlbums;
            public Dictionary<char, List<Album>> ByLetter;
            public List<SearchItem> Index;
        }

        private static readonly List<Album> EmptyAlbums = new List<Album>();

        private void Letter_Refresh()
        {
            letterBar.Current = currentLetter;
            letterBar.Invalidate();
        }

        private void LoadMusicFolder()
        {
            if (Directory.Exists(MusicFolder))
            {
                StartScan(MusicFolder);
            }
            else
            {
                lblStatus.Text = "No encontre una carpeta 'musica' junto al programa. Crea una carpeta llamada musica al lado del .exe.";
            }
        }

        private void StartScan(string root)
        {
            tracks.Clear();
            albums.Clear();
            byLetter.Clear();
            letterAvailable.Clear();
            searchIndex.Clear();
            BackToAlbums();
            lblStatus.Text = "Buscando musica en: " + root + " ...";
            scanner = new BackgroundWorker();
            scanner.WorkerReportsProgress = true;
            scanner.DoWork += Scanner_DoWork;
            scanner.ProgressChanged += Scanner_ProgressChanged;
            scanner.RunWorkerCompleted += Scanner_RunWorkerCompleted;
            scanner.RunWorkerAsync(root);
        }

        private void Scanner_DoWork(object sender, DoWorkEventArgs e)
        {
            string root = (string)e.Argument;
            ScanOutcome outcome = new ScanOutcome();
            outcome.Tracks = new List<Track>();
            foreach (string file in SafeEnumerateAudio(root))
            {
                outcome.Tracks.Add(Track.FromFile(file));
                if (outcome.Tracks.Count % 50 == 0)
                {
                    scanner.ReportProgress(0, outcome.Tracks.Count);
                }
            }
            outcome.Albums = BuildAlbums(outcome.Tracks, out outcome.ByLetter);
            outcome.VideoAlbums = BuildVideoSection(root, outcome.Albums);
            outcome.Index = BuildSearchIndex(outcome.Albums);
            e.Result = outcome;
        }

        private void Scanner_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Text = "Encontradas " + (int)e.UserState + " canciones...";
        }

        private void Scanner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                lblStatus.Text = "Error al escanear: " + e.Error.Message;
                return;
            }
            ScanOutcome outcome = (ScanOutcome)e.Result;
            tracks.AddRange(outcome.Tracks);
            albums = outcome.Albums;
            byLetter = outcome.ByLetter;
            videoAlbums = outcome.VideoAlbums;
            searchIndex.Clear();
            searchIndex.AddRange(outcome.Index);
            letterAvailable.Clear();
            foreach (KeyValuePair<char, List<Album>> pair in byLetter)
            {
                if (pair.Value.Count > 0)
                {
                    letterAvailable.Add(pair.Key);
                }
            }
            if (videoAlbums != null && videoAlbums.Count > 0)
            {
                letterAvailable.Add(VideoMark);
            }
            string order = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
            currentLetter = '#';
            for (int i = 0; i < order.Length; i++)
            {
                if (letterAvailable.Contains(order[i]))
                {
                    currentLetter = order[i];
                    break;
                }
            }
            page = 0;
            selAlbumIndex = 0;
            RefreshAlbums();
            if (albums.Count == 0)
            {
                lblStatus.Text = "No encontre MP3 en esa carpeta: " + MusicFolder;
            }
            else
            {
                lblStatus.Text = "";
            }
        }

        private static List<Album> BuildAlbums(List<Track> trackList, out Dictionary<char, List<Album>> byLetterMap)
        {
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < trackList.Count; i++)
            {
                Track track = trackList[i];
                string folder = Path.GetDirectoryName(track.Path);
                if (folder == null)
                {
                    folder = "";
                }
                Album album;
                if (!byFolder.TryGetValue(folder, out album))
                {
                    album = new Album();
                    album.Folder = folder;
                    byFolder[folder] = album;
                }
                album.Tracks.Add(track);
            }
            List<Album> result = new List<Album>();
            foreach (KeyValuePair<string, Album> pair in byFolder)
            {
                Album album = pair.Value;
                string leaf = LeafName(album.Folder);
                string artist;
                string name;
                if (SplitArtist(leaf, out artist, out name))
                {
                    album.Artist = artist;
                    album.Name = name;
                }
                else
                {
                    album.Artist = leaf;
                    album.Name = "";
                }
                album.Letter = FirstLetter(album.Artist);
                album.CoverPath = FindCover(album.Folder);
                album.Tracks.Sort(delegate(Track a, Track b)
                {
                    return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                });
                result.Add(album);
            }
            result.Sort(delegate(Album a, Album b)
            {
                int cmp = string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase);
                return (cmp != 0) ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            byLetterMap = new Dictionary<char, List<Album>>();
            for (int i = 0; i < result.Count; i++)
            {
                Album album = result[i];
                List<Album> list;
                if (!byLetterMap.TryGetValue(album.Letter, out list))
                {
                    list = new List<Album>();
                    byLetterMap[album.Letter] = list;
                }
                list.Add(album);
            }
            return result;
        }

        private static string LeafName(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return "Video";
            }
            string trimmed = folder.TrimEnd('\\', '/');
            string name = Path.GetFileName(trimmed);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return trimmed;
        }

        private static bool SplitArtist(string s, out string left, out string right)
        {
            left = (s ?? "").Trim();
            right = "";
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            int idx = s.IndexOf(" - ", StringComparison.Ordinal);
            int sepLen = 3;
            if (idx < 0)
            {
                idx = s.IndexOf('-');
                sepLen = 1;
            }
            if (idx <= 0)
            {
                return false;
            }
            left = s.Substring(0, idx).Trim();
            right = s.Substring(idx + sepLen).Trim();
            return left.Length > 0;
        }

        private static bool AllDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != ' ' && (c < '0' || c > '9'))
                {
                    return false;
                }
            }
            return true;
        }

        private static string AlbumLabel(Album a)
        {
            if (a == null)
            {
                return "";
            }
            if (a.Name == null || a.Name.Length <= 0)
            {
                return a.Artist;
            }
            return a.Artist + " - " + a.Name;
        }

        private static List<Album> BuildVideoSection(string root, List<Album> albumList)
        {
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albumList.Count; i++)
            {
                albumList[i].VideoFiles.Clear();
                byFolder[albumList[i].Folder] = albumList[i];
            }
            Dictionary<string, Album> videoOnly = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in SafeEnumerateVideo(root))
            {
                string folder = Path.GetDirectoryName(file);
                if (folder == null)
                {
                    folder = "";
                }
                Album album;
                if (byFolder.TryGetValue(folder, out album))
                {
                    album.VideoFiles.Add(file);
                    continue;
                }
                if (videoOnly.TryGetValue(folder, out album))
                {
                    album.VideoFiles.Add(file);
                    continue;
                }
                album = new Album();
                album.Folder = folder;
                album.Artist = LeafName(folder);
                album.Name = "";
                album.Letter = FirstLetter(album.Artist);
                album.CoverPath = FindCover(folder);
                album.VideoFiles.Add(file);
                videoOnly[folder] = album;
            }
            List<Album> result = new List<Album>();
            for (int i = 0; i < albumList.Count; i++)
            {
                if (albumList[i].VideoFiles.Count > 0)
                {
                    result.Add(albumList[i]);
                }
            }
            foreach (KeyValuePair<string, Album> pair in videoOnly)
            {
                result.Add(pair.Value);
            }
            result.Sort(delegate(Album a, Album b)
            {
                return string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase);
            });
            for (int i = 0; i < result.Count; i++)
            {
                result[i].VideoFiles.Sort(StringComparer.OrdinalIgnoreCase);
            }
            return result;
        }

        private static List<SearchItem> BuildSearchIndex(List<Album> albumList)
        {
            List<SearchItem> index = new List<SearchItem>();
            Dictionary<string, List<Album>> byArtist = new Dictionary<string, List<Album>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albumList.Count; i++)
            {
                Album album = albumList[i];
                if (!string.IsNullOrEmpty(album.Artist))
                {
                    List<Album> list;
                    if (!byArtist.TryGetValue(album.Artist, out list))
                    {
                        list = new List<Album>();
                        byArtist[album.Artist] = list;
                    }
                    list.Add(album);
                }
            }
            HashSet<string> seenArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albumList.Count; i++)
            {
                Album album = albumList[i];
                if (!string.IsNullOrEmpty(album.Artist) && seenArtists.Add(album.Artist))
                {
                    SearchItem artistItem = new SearchItem();
                    artistItem.Kind = 0;
                    artistItem.Display = album.Artist;
                    artistItem.Key = Normalize(album.Artist);
                    artistItem.AlbumRef = album;
                    byArtist.TryGetValue(album.Artist, out artistItem.Albums);
                    index.Add(artistItem);
                }
                SearchItem albumItem = new SearchItem();
                albumItem.Kind = 1;
                albumItem.Display = album.Artist + " - " + album.Name;
                albumItem.Key = Normalize(albumItem.Display);
                albumItem.AlbumRef = album;
                index.Add(albumItem);
                for (int j = 0; j < album.Tracks.Count; j++)
                {
                    Track track = album.Tracks[j];
                    SearchItem trackItem = new SearchItem();
                    trackItem.Kind = 2;
                    trackItem.Display = track.Artist + " - " + track.Title;
                    trackItem.Key = Normalize(trackItem.Display);
                    trackItem.TrackRef = track;
                    trackItem.AlbumRef = album;
                    index.Add(trackItem);
                }
            }
            return index;
        }

        private static string MostCommon(List<Track> ts, int field, string folder)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            string best = "";
            int bestCount = 0;
            for (int i = 0; i < ts.Count; i++)
            {
                string value = (field == 0) ? ts[i].Artist : ts[i].Album;
                if (!string.IsNullOrEmpty(value))
                {
                    int count;
                    counts.TryGetValue(value, out count);
                    count = (counts[value] = count + 1);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        best = value;
                    }
                }
            }
            if (best.Length > 0)
            {
                return best;
            }
            if (!string.IsNullOrEmpty(folder))
            {
                return new DirectoryInfo(folder).Name;
            }
            if (field != 0)
            {
                return "Album";
            }
            return "Desconocido";
        }

        private static char FirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return '#';
            }
            string trimmed = s.Trim();
            if (trimmed.Length == 0)
            {
                return '#';
            }
            char c = char.ToUpperInvariant(trimmed[0]);
            string decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            for (int i = 0; i < decomposed.Length; i++)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(decomposed[i]) != UnicodeCategory.NonSpacingMark)
                {
                    c = char.ToUpperInvariant(decomposed[i]);
                    break;
                }
            }
            if (c >= 'A' && c <= 'Z')
            {
                return c;
            }
            return '#';
        }

        private static string FindCover(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }
            try
            {
                for (int i = 0; i < CoverNames.Length; i++)
                {
                    string candidate = Path.Combine(folder, CoverNames[i]);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                string[] files = Directory.GetFiles(folder);
                for (int i = 0; i < files.Length; i++)
                {
                    string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                    if (Array.IndexOf(ImgExt, ext) >= 0)
                    {
                        return files[i];
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private List<Album> CurrentList()
        {
            if (currentLetter == VideoMark)
            {
                return videoAlbums ?? EmptyAlbums;
            }
            List<Album> list;
            if (!byLetter.TryGetValue(currentLetter, out list))
            {
                return EmptyAlbums;
            }
            return list;
        }

        private void RefreshAlbums()
        {
            List<Album> list = CurrentList();
            int pages = Math.Max(1, (list.Count + PageSize - 1) / PageSize);
            if (page < 0)
            {
                page = 0;
            }
            if (page >= pages)
            {
                page = pages - 1;
            }
            if (selAlbumIndex < 0)
            {
                selAlbumIndex = 0;
            }
            if (selAlbumIndex >= list.Count)
            {
                selAlbumIndex = Math.Max(0, list.Count - 1);
            }
            string sig = currentLetter + "|" + page;
            if (sig != lastPageSig)
            {
                lastPageSig = sig;
                coverGen++;
            }
            List<Album> pageList = new List<Album>();
            int start = page * PageSize;
            for (int i = start; i < start + PageSize && i < list.Count; i++)
            {
                pageList.Add(list[i]);
            }
            albumView.Page = pageList;
            albumView.Selected = selAlbumIndex - start;
            albumView.SelAngle = 0.0;
            albumView.CancelHandoff();
            albumView.Invalidate();
            string header = (currentLetter == VideoMark) ? "VIDEOS" : ("Letra " + currentLetter);
            lblHeader.Text = header + "    -    pagina " + (page + 1) + " de " + pages;
            Letter_Refresh();
        }

        private Image LoadCover(Album a)
        {
            if (a == null || a.CoverPath == null)
            {
                return null;
            }
            lock (coverLock)
            {
                Image cached;
                if (coverCache.TryGetValue(a.CoverPath, out cached))
                {
                    return cached;
                }
            }
            Image img = DecodeCover(a.CoverPath);
            lock (coverLock)
            {
                CachePut(a.CoverPath, img);
                return img;
            }
        }

        private Image LoadCoverAsync(Album a)
        {
            if (a == null || a.CoverPath == null)
            {
                return null;
            }
            string key = a.CoverPath;
            lock (coverLock)
            {
                Image cached;
                if (coverCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
                if (coverLoading.Contains(key))
                {
                    return null;
                }
                coverLoading.Add(key);
            }
            int gen = coverGen;
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (gen != coverGen)
                {
                    lock (coverLock)
                    {
                        coverLoading.Remove(key);
                        return;
                    }
                }
                Image img = DecodeCover(key);
                lock (coverLock)
                {
                    CachePut(key, img);
                    coverLoading.Remove(key);
                }
                try
                {
                    if (albumView != null && albumView.IsHandleCreated)
                    {
                        albumView.BeginInvoke((MethodInvoker)delegate
                        {
                            albumView.RefreshCover(key);
                        });
                    }
                }
                catch
                {
                }
            });
            return null;
        }

        private static string ThumbCachePath(string coverPath)
        {
            if (thumbDir == null)
            {
                string dir = Path.Combine(Path.GetTempPath(), "rocola_thumbs");
                try
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
                catch
                {
                }
                thumbDir = dir;
            }
            ulong hash = Fnv64(coverPath.ToLowerInvariant());
            return Path.Combine(thumbDir, hash.ToString("x16") + ".jpg");
        }

        private static ulong Fnv64(string s)
        {
            ulong hash = 1469598103934665603uL;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 1099511628211L;
            }
            return hash;
        }

        private static void SaveThumb(Bitmap thumb, string cacheFile)
        {
            try
            {
                if (jpegEnc == null)
                {
                    ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
                    foreach (ImageCodecInfo enc in encoders)
                    {
                        if (enc.MimeType == "image/jpeg")
                        {
                            jpegEnc = enc;
                            break;
                        }
                    }
                }
                if (jpegEnc == null)
                {
                    thumb.Save(cacheFile);
                    return;
                }
                using (EncoderParameters parms = new EncoderParameters(1))
                {
                    parms.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 82L);
                    thumb.Save(cacheFile, jpegEnc, parms);
                }
            }
            catch
            {
            }
        }

        private static Image DecodeCover(string path)
        {
            string cacheFile = null;
            try
            {
                cacheFile = ThumbCachePath(path);
            }
            catch
            {
            }
            if (cacheFile != null)
            {
                try
                {
                    if (File.Exists(cacheFile))
                    {
                        using (FileStream fs = new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read, 32768))
                        using (Image src = Image.FromStream(fs, false, false))
                        {
                            Bitmap thumb = new Bitmap(256, 256, PixelFormat.Format32bppPArgb);
                            using (Graphics g = Graphics.FromImage(thumb))
                            {
                                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                                g.DrawImage(src, 0, 0, 256, 256);
                            }
                            return thumb;
                        }
                    }
                }
                catch
                {
                }
            }
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
                using (Image src = Image.FromStream(fs, false, false))
                {
                    Bitmap thumb = new Bitmap(256, 256, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(src, 0, 0, 256, 256);
                    }
                    if (cacheFile != null)
                    {
                        SaveThumb(thumb, cacheFile);
                    }
                    return thumb;
                }
            }
            catch
            {
                return null;
            }
        }

        private void CachePut(string key, Image img)
        {
            if (coverCache.ContainsKey(key))
            {
                coverCache[key] = img;
                return;
            }
            coverCache[key] = img;
            coverOrder.Enqueue(key);
            while (coverOrder.Count > CoverCacheMax)
            {
                string evicted = coverOrder.Dequeue();
                Image old;
                if (coverCache.TryGetValue(evicted, out old))
                {
                    coverCache.Remove(evicted);
                    if (old != null)
                    {
                        coverTrash.Add(old);
                    }
                }
            }
        }

        private void DrainCoverTrash()
        {
            // Lectura sin lock: si justo se agrega algo, se recoge en el
            // siguiente tick (40 ms despues). Evita tomar el lock 25 veces
            // por segundo cuando no hay nada que liberar.
            if (coverTrash.Count == 0)
            {
                return;
            }
            List<Image> doomed;
            lock (coverLock)
            {
                if (coverTrash.Count == 0)
                {
                    return;
                }
                doomed = new List<Image>(coverTrash);
                coverTrash.Clear();
            }
            Image inMini = (miniCube != null) ? miniCube.Cover : null;
            Image inCube = (cubeBox != null) ? cubeBox.Cover : null;
            for (int i = 0; i < doomed.Count; i++)
            {
                Image img = doomed[i];
                if (img != null && img != inMini && img != inCube)
                {
                    try
                    {
                        img.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void SetSelectedLocal(int local, int dir)
        {
            albumView.MoveSelectionTo(local, dir);
            albumView.Invalidate();
        }

        private int PageDiscCount()
        {
            int count = CurrentList().Count - page * PageSize;
            if (count < 0)
            {
                count = 0;
            }
            if (count > PageSize)
            {
                count = PageSize;
            }
            return count;
        }

        private void MoveInPage(int delta)
        {
            int count = PageDiscCount();
            if (count != 0)
            {
                int local = selAlbumIndex - page * PageSize + delta;
                if (local < 0)
                {
                    local = 0;
                }
                if (local >= count)
                {
                    local = count - 1;
                }
                selAlbumIndex = page * PageSize + local;
                SetSelectedLocal(local, delta);
            }
        }

        private void NextPage()
        {
            int count = CurrentList().Count;
            int pages = Math.Max(1, (count + PageSize - 1) / PageSize);
            if (page < pages - 1)
            {
                page++;
                selAlbumIndex = page * PageSize;
                RefreshAlbums();
            }
            else if (GotoLetter(1))
            {
                page = 0;
                selAlbumIndex = 0;
                RefreshAlbums();
            }
        }

        private void PrevPage()
        {
            if (page > 0)
            {
                page--;
                selAlbumIndex = page * PageSize;
                RefreshAlbums();
            }
            else if (GotoLetter(-1))
            {
                int count = CurrentList().Count;
                int pages = Math.Max(1, (count + PageSize - 1) / PageSize);
                page = pages - 1;
                selAlbumIndex = page * PageSize;
                RefreshAlbums();
            }
        }

        private bool GotoLetter(int dir)
        {
            string order = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
            int idx = order.IndexOf(currentLetter);
            if (idx < 0)
            {
                idx = 0;
            }
            for (int i = idx + dir; i >= 0 && i < order.Length; i += dir)
            {
                if (letterAvailable.Contains(order[i]))
                {
                    currentLetter = order[i];
                    return true;
                }
            }
            return false;
        }

        private void ChangeLetter(int dir)
        {
            if (GotoLetter(dir))
            {
                page = 0;
                selAlbumIndex = 0;
                RefreshAlbums();
            }
        }

        private void EnterAlbum(Album a)
        {
            currentAlbum = a;
            browsingVideo = currentLetter == VideoMark;
            lblAlbum.Text = browsingVideo ? (AlbumLabel(a) + "   (video)") : AlbumLabel(a);
            songItems.Clear();
            if (browsingVideo)
            {
                for (int i = 0; i < a.VideoFiles.Count; i++)
                {
                    songItems.Add(new VideoItem(a.VideoFiles[i]));
                }
            }
            else
            {
                for (int i = 0; i < a.Tracks.Count; i++)
                {
                    songItems.Add(a.Tracks[i]);
                }
            }
            songSel = 0;
            FillSongPage();
            cubeBox.Cover = LoadCover(a);
            cubeBox.Angle = 0.0;
            albumView.Visible = false;
            letterBar.Visible = false;
            lblHeader.Visible = false;
            panelSongs.Visible = true;
            Focus();
        }

        private void BackToAlbums()
        {
            panelSongs.Visible = false;
            albumView.Visible = true;
            letterBar.Visible = true;
            lblHeader.Visible = true;
            Focus();
        }

        private void NavigateToAlbum(Album a)
        {
            if (a != null)
            {
                currentLetter = a.Letter;
                int idx = CurrentList().IndexOf(a);
                if (idx < 0)
                {
                    idx = 0;
                }
                selAlbumIndex = idx;
                page = idx / PageSize;
                if (panelSongs.Visible)
                {
                    BackToAlbums();
                }
                RefreshAlbums();
            }
        }

        private void MoveSong(int delta)
        {
            if (songItems.Count != 0)
            {
                songSel += delta;
                if (songSel < 0)
                {
                    songSel = 0;
                }
                if (songSel >= songItems.Count)
                {
                    songSel = songItems.Count - 1;
                }
                FillSongPage();
            }
        }

        private int SongRowsPerPage()
        {
            int itemH = Math.Max(1, lstSongs.ItemHeight);
            return Math.Max(1, lstSongs.ClientSize.Height / itemH);
        }

        private void FillSongPage()
        {
            if (songItems.Count == 0)
            {
                lstSongs.Items.Clear();
                if (songPager != null)
                {
                    songPager.Visible = false;
                }
                return;
            }
            if (songSel < 0)
            {
                songSel = 0;
            }
            if (songSel >= songItems.Count)
            {
                songSel = songItems.Count - 1;
            }
            int rowsPerPage = SongRowsPerPage();
            int pages = (songItems.Count + rowsPerPage - 1) / rowsPerPage;
            int songPage = songSel / rowsPerPage;
            int start = songPage * rowsPerPage;
            fillingSongs = true;
            lstSongs.BeginUpdate();
            lstSongs.Items.Clear();
            for (int i = start; i < start + rowsPerPage && i < songItems.Count; i++)
            {
                lstSongs.Items.Add(songItems[i]);
            }
            lstSongs.EndUpdate();
            lstSongs.SelectedIndex = songSel - start;
            fillingSongs = false;
            if (songPager != null)
            {
                songPager.Page = songPage;
                songPager.Pages = pages;
                songPager.Visible = pages > 1;
                songPager.Invalidate();
            }
        }

        private void LstSongs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!fillingSongs && lstSongs.SelectedIndex >= 0)
            {
                int rowsPerPage = SongRowsPerPage();
                int songPage = (songPager != null && songPager.Pages > 1) ? songPager.Page : 0;
                songSel = songPage * rowsPerPage + lstSongs.SelectedIndex;
                if (songSel >= songItems.Count)
                {
                    songSel = Math.Max(0, songItems.Count - 1);
                }
            }
        }

        private void EnqueueSelectedSong()
        {
            if (songSel >= 0 && songSel < songItems.Count)
            {
                object item = songItems[songSel];
                VideoItem videoItem = item as VideoItem;
                if (videoItem != null)
                {
                    AddVideoToQueue(videoItem.Path, currentAlbum);
                }
                else
                {
                    AddSong((Track)item, currentAlbum);
                }
            }
        }
    }
}
