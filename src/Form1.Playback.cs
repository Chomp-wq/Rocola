using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1
    {
        private void ShowToast(string text)
        {
            ShowToast(text, Color.FromArgb(40, 165, 90));
        }

        private void ShowToast(string text, Color back)
        {
            int w = 380;
            int h = 48;
            lblToast.Text = text;
            lblToast.BackColor = back;
            lblToast.SetBounds((ClientSize.Width - w) / 2, 80, w, h);
            try
            {
                using (GraphicsPath path = RoundRect(new Rectangle(0, 0, w, h), 16))
                {
                    lblToast.Region = new Region(path);
                }
            }
            catch
            {
            }
            lblToast.Visible = true;
            lblToast.BringToFront();
            toastTimer.Stop();
            toastTimer.Start();
        }

        private AddResult AddItem(PlayItem it)
        {
            if (it == null)
            {
                return AddResult.Repeat;
            }
            if (credits <= 0)
            {
                return AddResult.NoCredit;
            }
            PlayItem last = (queue.Count > 0) ? queue[queue.Count - 1] : nowPlaying;
            if (last != null && string.Equals(last.PathKey(), it.PathKey(), StringComparison.OrdinalIgnoreCase))
            {
                return AddResult.Repeat;
            }
            queue.Add(it);
            credits--;
            UpdateCredits();
            if (nowPlaying == null)
            {
                PlayNext();
            }
            else
            {
                UpdateNowPlayingUI();
            }
            return AddResult.Added;
        }

        private void AddSong(Track t, Album disc)
        {
            if (t != null)
            {
                PlayItem item = new PlayItem();
                item.IsVideo = false;
                item.Song = t;
                item.Disc = disc;
                switch (AddItem(item))
                {
                    case AddResult.Added:
                        ShowToast("Agregada a la cola");
                        break;
                    case AddResult.NoCredit:
                        ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35));
                        break;
                    default:
                        ShowToast("Esa ya va seguida", Color.FromArgb(200, 130, 35));
                        break;
                }
            }
        }

        private void AddVideoToQueue(string path, Album disc)
        {
            if (!string.IsNullOrEmpty(path))
            {
                PlayItem item = new PlayItem();
                item.IsVideo = true;
                item.VideoPath = path;
                item.Disc = disc;
                switch (AddItem(item))
                {
                    case AddResult.Added:
                        ShowToast("Video agregado a la cola");
                        break;
                    case AddResult.NoCredit:
                        ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35));
                        break;
                    default:
                        ShowToast("Ese video ya va seguido", Color.FromArgb(200, 130, 35));
                        break;
                }
            }
        }

        private void AddCoin()
        {
            credits += CreditsPerCoin;
            UpdateCredits();
            ShowToast("+" + CreditsPerCoin + " creditos", Color.FromArgb(40, 165, 90));
        }

        private void UpdateCredits()
        {
            if (lblCredits != null)
            {
                if (credits <= 0)
                {
                    lblCredits.Text = "Inserta una moneda";
                    lblCredits.ForeColor = Color.FromArgb(255, 205, 90);
                }
                else
                {
                    lblCredits.Text = "Creditos:  " + credits;
                    lblCredits.ForeColor = Color.FromArgb(120, 240, 140);
                }
                lblCredits.Invalidate();
            }
        }

        private void PlayNext()
        {
            DisposePlaybackNow();
            StopVideoPlayback();
            if (queue.Count == 0)
            {
                nowPlaying = null;
                progress.Value = 0.0;
                lblTime.Text = "0:00 / 0:00";
                ApplyGenreBackground(null);
                LayoutControls();
                Invalidate();
                UpdateNowPlayingUI();
                return;
            }
            nowPlaying = queue[0];
            queue.RemoveAt(0);
            if (nowPlaying.IsVideo)
            {
                bgAccentTarget = DefaultAccent;
                StartVideoItem(nowPlaying.VideoPath);
            }
            else
            {
                nowPlaying.Song.EnsureTags();
                ApplyGenreBackground(nowPlaying.Song);
                PlayTrack(nowPlaying.Song);
            }
            LayoutControls();
            Invalidate();
            UpdateNowPlayingUI();
        }

        private void SkipSong()
        {
            if (nowPlaying != null)
            {
                PlayNext();
            }
        }

        private void EnterVideoFull()
        {
            if (!videoFull && videoOn && video != null)
            {
                videoFull = true;
                LayoutControls();
                video.BringToFront();
                if (lblAttract != null)
                {
                    lblAttract.Visible = true;
                    lblAttract.BringToFront();
                }
            }
        }

        private void ExitVideoFull()
        {
            if (videoFull)
            {
                videoFull = false;
                if (lblAttract != null)
                {
                    lblAttract.Visible = false;
                }
                LayoutControls();
                Invalidate();
            }
        }

        private void StartVideoItem(string path)
        {
            try
            {
                if (video == null)
                {
                    video = new VideoHost();
                    video.TabStop = false;
                    Controls.Add(video);
                    IntPtr forceHandle = video.Handle;
                }
                video.Visible = !searchOpen;
                object ocx = video.GetOcxObj();
                if (ocx == null)
                {
                    throw new Exception("control WMP nulo");
                }
                Set(ocx, "uiMode", "none");
                Set(ocx, "stretchToFit", true);
                object settings = Get(ocx, "settings");
                if (settings != null)
                {
                    Set(settings, "autoStart", true);
                    Set(settings, "volume", 100);
                }
                Set(ocx, "URL", path);
                videoOn = true;
                video.BringToFront();
                videoPoll.Start();
            }
            catch (Exception ex)
            {
                videoOn = false;
                try
                {
                    if (video != null)
                    {
                        video.Visible = false;
                    }
                }
                catch
                {
                }
                ShowToast("No se pudo abrir el video", Color.FromArgb(200, 80, 70));
                lblStatus.Text = "Video (WMP): " + ex.Message;
            }
        }

        private void StopVideoPlayback()
        {
            videoPoll.Stop();
            try
            {
                if (video != null)
                {
                    object ocx = video.GetOcxObj();
                    if (ocx != null)
                    {
                        object controls = Get(ocx, "controls");
                        if (controls != null)
                        {
                            Call(controls, "stop");
                        }
                        Set(ocx, "URL", "");
                    }
                    video.Visible = false;
                }
            }
            catch
            {
            }
            videoOn = false;
            if (videoFull)
            {
                ExitVideoFull();
            }
        }

        private void VideoPoll_Tick(object sender, EventArgs e)
        {
            if (!videoOn || video == null)
            {
                videoPoll.Stop();
                return;
            }
            try
            {
                object ocx = video.GetOcxObj();
                if (ocx != null)
                {
                    object state = Get(ocx, "playState");
                    if (((state != null) ? Convert.ToInt32(state) : (-1)) == 8)
                    {
                        PlayNext();
                    }
                }
            }
            catch
            {
            }
        }

        private static object Get(object o, string prop)
        {
            return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null);
        }

        private static void Set(object o, string prop, object val)
        {
            o.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, o, new object[1] { val });
        }

        private static object Call(object o, string method, params object[] args)
        {
            return o.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, o, args);
        }

        private Album AlbumOfTrack(Track t)
        {
            if (t == null)
            {
                return null;
            }
            string folder = null;
            try
            {
                folder = Path.GetDirectoryName(t.Path);
            }
            catch
            {
            }
            if (folder == null)
            {
                return null;
            }
            for (int i = 0; i < albums.Count; i++)
            {
                if (string.Equals(albums[i].Folder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    return albums[i];
                }
            }
            return null;
        }

        private void ApplyGenreBackground(Track t)
        {
            Color accent = (bgAccentTarget = ColorForTrack(t));
            Color panelBack = Shade(accent, 0.7);
            Color listBack = Shade(accent, 0.76);
            panelSongs.BackColor = panelBack;
            if (songPager != null)
            {
                songPager.BackColor = panelBack;
                songPager.Invalidate();
            }
            cubeBox.BackColor = Color.Transparent;
            lstSongs.BackColor = listBack;
            lstSongs.Invalidate();
        }

        private void PlayTrack(Track t)
        {
            try
            {
                reader = new AudioFileReader(t.Path);
                reader.Volume = FixedVolume;
                WaveOut waveOut = new WaveOut();
                waveOut.DesiredLatency = 400;
                waveOut.NumberOfBuffers = 4;
                output = waveOut;
                output.PlaybackStopped += Output_PlaybackStopped;
                output.Init(new SampleToWaveProvider16(reader));
                output.Play();
                progressTimer.Start();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "No se pudo reproducir " + Path.GetFileName(t.Path) + " (" + ex.Message + ")";
                LogError("PlayTrack " + Path.GetFileName(t.Path), ex);
                DisposePlaybackNow();
                PlayNext();
            }
        }

        private void LogError(string context, Exception ex)
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "rocola_error.txt");
                string contents = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]\r\n" + ((ex != null) ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n";
                File.AppendAllText(path, contents);
            }
            catch
            {
            }
        }

        private void Output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                lblStatus.Text = "Aviso al reproducir: " + e.Exception.Message;
                LogError("PlaybackStopped", e.Exception);
                playFailStreak++;
                if (playFailStreak >= 3)
                {
                    DisposePlaybackNow();
                    nowPlaying = null;
                    lblStatus.Text = "No se pudo reproducir. Detalle en rocola_error.txt (junto al programa)";
                    UpdateNowPlayingUI();
                    return;
                }
            }
            else
            {
                playFailStreak = 0;
            }
            PlayNext();
        }

        private void DisposePlaybackNow()
        {
            progressTimer.Stop();
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                try
                {
                    output.Stop();
                }
                catch
                {
                }
                try
                {
                    output.Dispose();
                }
                catch
                {
                }
                output = null;
            }
            if (reader != null)
            {
                try
                {
                    reader.Dispose();
                }
                catch
                {
                }
                reader = null;
            }
        }

        private void UpdateNowPlayingUI()
        {
            lblNow.Text = (nowPlaying != null) ? ("Reproduciendo:  " + nowPlaying.Display()) : "Nada reproduciendose";
            lblNext.Text = (queue.Count > 0) ? ("Siguiente:  " + queue[0].Display()) : "Siguiente:  -";
            lblCount.Text = "En cola:  " + queue.Count;
            if (miniCube != null)
            {
                if (nowPlaying != null)
                {
                    miniCube.Cover = LoadCover(nowPlaying.Disc);
                    miniCube.Visible = true;
                }
                else
                {
                    miniCube.Cover = null;
                    miniCube.Visible = false;
                }
                miniCube.Invalidate();
            }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (reader != null)
            {
                TimeSpan current = reader.CurrentTime;
                TimeSpan total = reader.TotalTime;
                if (total.TotalSeconds > 0.0)
                {
                    progress.Value = current.TotalSeconds / total.TotalSeconds;
                }
                lblTime.Text = Fmt(current) + " / " + Fmt(total);
            }
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            phase += 0.045;
            DrainCoverTrash();
            if (bgAccent.R != bgAccentTarget.R || bgAccent.G != bgAccentTarget.G || bgAccent.B != bgAccentTarget.B)
            {
                bgAccent = LerpColor(bgAccent, bgAccentTarget, 0.16);
                if (Math.Abs(bgAccent.R - bgAccentTarget.R) <= 2 && Math.Abs(bgAccent.G - bgAccentTarget.G) <= 2 && Math.Abs(bgAccent.B - bgAccentTarget.B) <= 2)
                {
                    bgAccent = bgAccentTarget;
                }
                if (!searchOpen)
                {
                    Invalidate();
                }
            }
            albumView.BgAccent = bgAccent;
            if (searchOpen)
            {
                if ((DateTime.Now - searchLastActivity).TotalSeconds >= 60.0)
                {
                    CloseSearch();
                    return;
                }
                searchOverlay.Accent = bgAccent;
                searchOverlay.Phase = phase;
                searchOverlay.CubeAngle += 0.075;
                searchOverlay.Invalidate();
                return;
            }
            if (videoOn && !videoFull && (DateTime.Now - lastInput).TotalSeconds > 8.0)
            {
                EnterVideoFull();
            }
            if (videoFull)
            {
                if (lblAttract != null)
                {
                    double pulse = 0.5 + 0.5 * Math.Sin(phase * 4.0);
                    lblAttract.ForeColor = Color.FromArgb((int)(140.0 + 115.0 * pulse), (int)(110.0 + 125.0 * pulse), (int)(0.0 + 60.0 * pulse));
                }
                return;
            }
            if (letterBar.Visible)
            {
                letterBar.Phase = phase;
                letterBar.Invalidate();
            }
            if (albumView.Visible)
            {
                albumView.StepSpin();
                albumView.Phase = phase;
                albumView.Invalidate();
            }
            if (panelSongs.Visible)
            {
                panelSongs.Accent = bgAccent;
                panelSongs.Phase = phase;
                panelSongs.Invalidate();
                lblAlbum.Invalidate();
                cubeBox.Phase = phase;
                cubeBox.Angle += 0.06;
                cubeBox.Invalidate();
            }
            if (miniCube.Visible)
            {
                miniCube.Phase = phase;
                miniCube.Angle += 0.06;
                miniCube.Invalidate();
            }
        }

        private static string Fmt(TimeSpan t)
        {
            return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DisposePlaybackNow();
            base.OnFormClosing(e);
        }
    }
}
