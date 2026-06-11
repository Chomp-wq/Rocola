using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1 : Form
    {
        private enum AddResult
        {
            Added,
            NoCredit,
            Repeat
        }

        private const float FixedVolume = 1f;
        private const int Cols = 6;
        private const int Rows = 3;
        private const int PageSize = 18;
        private const uint MOUSEEVENTF_LEFTDOWN = 2u;
        private const uint MOUSEEVENTF_LEFTUP = 4u;

        private bool timerResRaised;

        private static readonly Color DefaultAccent = Color.FromArgb(45, 95, 205);

        private LetterBar letterBar;
        private AlbumView albumView;
        private Label lblHeader;
        private Label lblStatus;
        private Label lblNow;
        private Label lblNext;
        private Label lblCount;
        private Label lblTime;
        private Label lblAlbum;
        private Label lblToast;
        private Label lblCredits;
        private HelpBar helpBar;
        private BgPanel panelSongs;
        private ListBox lstSongs;
        private CubeBox cubeBox;
        private CubeBox miniCube;
        private ProgressView progress;
        private SearchOverlay searchOverlay;
        private Timer progressTimer;
        private Timer animTimer;
        private Timer toastTimer;
        private Timer focusTimer;
        private DateTime focusGuardUntil = DateTime.MinValue;
        private IWavePlayer output;
        private AudioFileReader reader;
        private int playFailStreak;
        private SongPagerBar songPager;
        private List<object> songItems = new List<object>();
        private int songSel;
        private bool fillingSongs;
        private readonly List<PlayItem> queue = new List<PlayItem>();
        private PlayItem nowPlaying;
        private readonly List<Track> tracks = new List<Track>();
        private List<Album> albums = new List<Album>();
        private List<Album> videoAlbums = new List<Album>();
        private Dictionary<char, List<Album>> byLetter = new Dictionary<char, List<Album>>();
        private readonly Dictionary<string, Image> coverCache = new Dictionary<string, Image>();
        private readonly Queue<string> coverOrder = new Queue<string>();
        private readonly HashSet<string> coverLoading = new HashSet<string>();
        private readonly List<Image> coverTrash = new List<Image>();
        private volatile int coverGen;
        private string lastPageSig;
        private readonly object coverLock = new object();
        private const int CoverCacheMax = 220;
        private readonly HashSet<char> letterAvailable = new HashSet<char>();
        private readonly List<SearchItem> searchIndex = new List<SearchItem>();
        private char currentLetter = '#';
        private const char VideoMark = '►';
        private int page;
        private int selAlbumIndex;
        private Album currentAlbum;
        private bool browsingVideo;
        private BackgroundWorker scanner;
        private double phase;
        private bool searchOpen;
        private DateTime searchLastActivity = DateTime.Now;
        private string lastQuery;
        private List<SearchItem> lastMatches;
        private int credits;
        private const int CreditsPerCoin = 4;
        private Keys keyUp = Keys.Up;
        private Keys keyDown = Keys.Down;
        private Keys keyLeft = Keys.Left;
        private Keys keyRight = Keys.Right;
        private Keys keyOK = Keys.Enter;
        private Keys keyBack = Keys.Escape;
        private Keys keyCoin = Keys.Insert;
        private Keys keySkip = Keys.ShiftKey;
        private bool configMode;
        private bool needConfig;
        private int configStep;
        private readonly Keys[] configCap = new Keys[8];
        private Label lblConfig;

        private static readonly string[] ConfigSteps = new string[8]
        {
            "ARRIBA   (subir / pagina anterior)",
            "ABAJO   (bajar / pagina siguiente)",
            "IZQUIERDA   (disco anterior)",
            "DERECHA   (disco siguiente)",
            "PONER / ACEPTAR   (elegir, poner cancion)",
            "REGRESAR   (atras / salir)",
            "MONEDA   (credito)",
            "SALTAR   (saltar la cancion actual)"
        };

        private Color bgAccent = DefaultAccent;
        private Color bgAccentTarget = DefaultAccent;
        private VideoHost video;
        private Timer videoPoll;
        private bool videoOn;
        private bool videoFull;
        private DateTime lastInput = DateTime.Now;
        private Label lblAttract;

        private static readonly string[] AudioExt = new string[2] { ".mp3", ".wav" };

        private static readonly string[] VideoExt = new string[13]
        {
            ".mp4", ".avi", ".wmv", ".mpg", ".mpeg", ".mov", ".mkv", ".flv", ".m4v", ".vob",
            ".divx", ".3gp", ".asf"
        };

        private static readonly string[] CoverNames = new string[7] { "portada.jpg", "portada.jpeg", "portada.png", "cover.jpg", "cover.png", "folder.jpg", "front.jpg" };

        private static readonly string[] ImgExt = new string[4] { ".jpg", ".jpeg", ".png", ".bmp" };

        private static string thumbDir;
        private static ImageCodecInfo jpegEnc;

        private IContainer components;

        // Gradiente de fondo del formulario cacheado (se recrea solo cuando
        // cambia el tamano o el color de acento, no en cada repintado).
        private LinearGradientBrush formBgBrush;
        private int formBgW = -1;
        private int formBgH = -1;
        private int formBgAccent;

        private string ConfigFile
        {
            get { return Path.Combine(Application.StartupPath, "rocola_config.txt"); }
        }

        private string MusicFolder
        {
            get
            {
                try
                {
                    string dir = Application.StartupPath;
                    for (int i = 0; i < 4; i++)
                    {
                        if (dir == null)
                        {
                            break;
                        }
                        string candidate = Path.Combine(dir, "musica");
                        if (Directory.Exists(candidate))
                        {
                            return candidate;
                        }
                        DirectoryInfo parent = Directory.GetParent(dir);
                        dir = (parent != null) ? parent.FullName : null;
                    }
                }
                catch
                {
                }
                return Path.Combine(Application.StartupPath, "musica");
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr procId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private static bool KeyHeld(Keys k)
        {
            return (GetKeyState((int)(k & Keys.KeyCode)) & 0x8000) != 0;
        }

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint p);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint p);

        public Form1()
        {
            DoubleBuffered = true;
            try
            {
                if (timeBeginPeriod(1u) == 0)
                {
                    timerResRaised = true;
                }
            }
            catch
            {
            }
            try
            {
                int workerThreads;
                int completionPortThreads;
                ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);
                ThreadPool.SetMinThreads(Math.Max(workerThreads, 3), completionPortThreads);
            }
            catch
            {
            }
            KeyPreview = true;
            KeyPress += Form_KeyPress;
            BuildUi();
            needConfig = !LoadKeys();
            progressTimer = new Timer();
            progressTimer.Interval = 500;
            progressTimer.Tick += ProgressTimer_Tick;
            animTimer = new Timer();
            animTimer.Interval = 40;
            animTimer.Tick += AnimTimer_Tick;
            animTimer.Start();
            toastTimer = new Timer();
            toastTimer.Interval = 1300;
            toastTimer.Tick += delegate
            {
                toastTimer.Stop();
                lblToast.Visible = false;
                if (searchOverlay != null)
                {
                    searchOverlay.ToastOn = false;
                    searchOverlay.Invalidate();
                }
            };
            videoPoll = new Timer();
            videoPoll.Interval = 400;
            videoPoll.Tick += VideoPoll_Tick;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                if (timerResRaised)
                {
                    timeEndPeriod(1u);
                    timerResRaised = false;
                }
            }
            catch
            {
            }
            base.OnFormClosed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutControls();
            LoadMusicFolder();
            try
            {
                Cursor.Hide();
                Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 1, Screen.PrimaryScreen.Bounds.Bottom - 1);
            }
            catch
            {
            }
            ForceFocus();
            focusGuardUntil = DateTime.Now.AddSeconds(120.0);
            if (focusTimer == null)
            {
                focusTimer = new Timer();
                focusTimer.Interval = 1500;
                focusTimer.Tick += FocusTimer_Tick;
            }
            focusTimer.Start();
            if (needConfig)
            {
                StartConfig();
            }
        }

        private void ForceFocus()
        {
            try
            {
                if (GetForegroundWindow() == Handle)
                {
                    Focus();
                    return;
                }
                IntPtr foreground = GetForegroundWindow();
                uint foreThread = (foreground != IntPtr.Zero) ? GetWindowThreadProcessId(foreground, IntPtr.Zero) : 0u;
                uint thisThread = GetCurrentThreadId();
                bool attached = false;
                if (foreThread != 0 && foreThread != thisThread)
                {
                    try
                    {
                        attached = AttachThreadInput(thisThread, foreThread, true);
                    }
                    catch
                    {
                    }
                }
                TopMost = true;
                SetForegroundWindow(Handle);
                Activate();
                BringToFront();
                TopMost = false;
                SetFocus(Handle);
                Focus();
                if (attached)
                {
                    try
                    {
                        AttachThreadInput(thisThread, foreThread, false);
                    }
                    catch
                    {
                    }
                }
                if (GetForegroundWindow() != Handle)
                {
                    Rectangle bounds = Screen.PrimaryScreen.Bounds;
                    Cursor.Position = new Point(bounds.Right - 2, bounds.Bottom - 2);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0u, 0u, 0u, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0u, 0u, 0u, UIntPtr.Zero);
                }
            }
            catch
            {
            }
        }

        private void FocusTimer_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now > focusGuardUntil)
            {
                focusTimer.Stop();
            }
            else if (GetForegroundWindow() != Handle)
            {
                ForceFocus();
            }
        }

        private void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!searchOpen)
            {
                return;
            }
            char c = e.KeyChar;
            switch (c)
            {
                case '\b':
                    searchOverlay.Backspace();
                    MarkSearchActivity();
                    e.Handled = true;
                    return;
                case '\n':
                case '\r':
                case '\u001b':
                    e.Handled = true;
                    return;
            }
            if (!char.IsControl(c))
            {
                searchOverlay.TypeChar(c);
                MarkSearchActivity();
                e.Handled = true;
            }
        }

        private void SaveMusicFolder(string path)
        {
            try
            {
                File.WriteAllText(ConfigFile, path);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "No pude guardar la configuracion: " + ex.Message;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            if (w < 2 || h < 2)
            {
                base.OnPaintBackground(e);
                return;
            }
            if (formBgBrush == null || formBgW != w || formBgH != h || formBgAccent != bgAccent.ToArgb())
            {
                if (formBgBrush != null)
                {
                    formBgBrush.Dispose();
                }
                formBgBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), Shade(bgAccent, 0.34), Shade(bgAccent, 0.8), LinearGradientMode.Vertical);
                formBgW = w;
                formBgH = h;
                formBgAccent = bgAccent.ToArgb();
            }
            g.FillRectangle(formBgBrush, 0, 0, w, h);
        }

        private static Color Shade(Color c, double towardBlack)
        {
            if (towardBlack < 0.0)
            {
                towardBlack = 0.0;
            }
            if (towardBlack > 1.0)
            {
                towardBlack = 1.0;
            }
            double k = 1.0 - towardBlack;
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        private static Color Lighten(Color c, double t)
        {
            if (t < 0.0)
            {
                t = 0.0;
            }
            if (t > 1.0)
            {
                t = 1.0;
            }
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));
        }

        private static Color LerpColor(Color a, Color b, double t)
        {
            if (t < 0.0)
            {
                t = 0.0;
            }
            if (t > 1.0)
            {
                t = 1.0;
            }
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Height)
            {
                d = r.Height;
            }
            if (d > r.Width)
            {
                d = r.Width;
            }
            if (d < 2)
            {
                d = 2;
            }
            path.AddArc(r.X, r.Y, d, d, 180f, 90f);
            path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            if (d > r.Width)
            {
                d = r.Width;
            }
            if (d > r.Height)
            {
                d = r.Height;
            }
            if (d < 1)
            {
                d = 1;
            }
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180f, 90f);
            path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            string text = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(text[i]) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(text[i]);
                }
            }
            return sb.ToString();
        }

        private static bool Has(string s, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (s.Contains(keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static Color ColorForTrack(Track t)
        {
            if (t == null)
            {
                return DefaultAccent;
            }
            string artist = (t.Artist != null) ? t.Artist.ToLowerInvariant() : "";
            if (artist.Contains("bts") || artist.Contains("bangtan"))
            {
                return Color.FromArgb(149, 86, 220);
            }
            return GenreToColor(t.Genre);
        }

        private static Color GenreToColor(string genre)
        {
            if (string.IsNullOrEmpty(genre))
            {
                return DefaultAccent;
            }
            string s = genre.ToLowerInvariant();
            if (Has(s, "k-pop", "kpop", "j-pop", "jpop", "anime", "idol"))
            {
                return Color.FromArgb(149, 86, 220);
            }
            if (Has(s, "metal", "thrash", "death", "heavy", "hardcore", "grindcore"))
            {
                return Color.FromArgb(205, 45, 55);
            }
            if (Has(s, "punk"))
            {
                return Color.FromArgb(232, 80, 58);
            }
            if (Has(s, "gothic", "goth", "darkwave"))
            {
                return Color.FromArgb(112, 58, 150);
            }
            if (Has(s, "industrial"))
            {
                return Color.FromArgb(150, 60, 70);
            }
            if (Has(s, "reggaeton", "reggaet", "perreo"))
            {
                return Color.FromArgb(150, 70, 210);
            }
            if (Has(s, "hip", "rap", "trap"))
            {
                return Color.FromArgb(135, 60, 205);
            }
            if (Has(s, "salsa", "mereng", "bachata", "tropical"))
            {
                return Color.FromArgb(235, 95, 35);
            }
            if (Has(s, "cumbia", "banda", "norte", "ranch", "mariach", "regional", "tejano", "grupera", "corrido", "duranguense", "sonidero"))
            {
                return Color.FromArgb(225, 120, 30);
            }
            if (Has(s, "bolero", "tango", "ranchera"))
            {
                return Color.FromArgb(195, 95, 70);
            }
            if (Has(s, "flamenco", "rumba", "copla"))
            {
                return Color.FromArgb(210, 110, 55);
            }
            if (Has(s, "latin", "latino", "espan"))
            {
                return Color.FromArgb(225, 130, 40);
            }
            if (Has(s, "reggae", "ska", "dub"))
            {
                return Color.FromArgb(40, 175, 85);
            }
            if (Has(s, "drum", "bass", "jungle", "breakbeat", "garage"))
            {
                return Color.FromArgb(20, 155, 160);
            }
            if (Has(s, "electro", "techno", "house", "trance", "edm", "synth", "dance", "club", "dubstep", "rave", "hardstyle"))
            {
                return Color.FromArgb(30, 175, 195);
            }
            if (Has(s, "ambient", "chill", "lo-fi", "lofi", "downtempo", "new age", "meditat"))
            {
                return Color.FromArgb(70, 165, 175);
            }
            if (Has(s, "jazz", "swing", "bebop"))
            {
                return Color.FromArgb(215, 165, 45);
            }
            if (Has(s, "blues", "soul", "funk", "r&b", "rhythm", "motown"))
            {
                return Color.FromArgb(205, 145, 50);
            }
            if (Has(s, "gospel", "christian", "worship", "cristiana"))
            {
                return Color.FromArgb(210, 180, 75);
            }
            if (Has(s, "classic", "orchestr", "piano", "opera", "symphon", "baroque", "instrumental"))
            {
                return Color.FromArgb(95, 95, 205);
            }
            if (Has(s, "soundtrack", "score", "cinematic", "banda sonora"))
            {
                return Color.FromArgb(110, 120, 190);
            }
            if (Has(s, "country", "bluegrass", "americana"))
            {
                return Color.FromArgb(170, 130, 55);
            }
            if (Has(s, "folk", "acoustic", "celt"))
            {
                return Color.FromArgb(150, 140, 70);
            }
            if (Has(s, "disco"))
            {
                return Color.FromArgb(212, 70, 160);
            }
            if (Has(s, "oldies", "50s", "60s", "doo"))
            {
                return Color.FromArgb(192, 120, 90);
            }
            if (Has(s, "world", "ethnic", "african", "arab", "india", "asian"))
            {
                return Color.FromArgb(180, 110, 60);
            }
            if (Has(s, "vocal", "capella", "capela"))
            {
                return Color.FromArgb(180, 150, 120);
            }
            if (Has(s, "comedy", "spoken", "podcast"))
            {
                return Color.FromArgb(120, 130, 150);
            }
            if (Has(s, "child", "kids", "infantil"))
            {
                return Color.FromArgb(235, 180, 60);
            }
            if (Has(s, "pop"))
            {
                return Color.FromArgb(215, 60, 140);
            }
            if (Has(s, "rock", "grunge", "alternative", "indie"))
            {
                return Color.FromArgb(215, 90, 40);
            }
            return DefaultAccent;
        }

        private Font Bold(float size)
        {
            return new Font(Font.FontFamily, size, FontStyle.Bold);
        }

        private RoundButton MakeButton(string text, float fontSize)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Font = Bold(fontSize);
            return button;
        }

        private void TransLabel(Label l, float size)
        {
            l.BackColor = Color.Transparent;
            l.ForeColor = Color.White;
            l.Font = Bold(size);
        }

        private void BuildUi()
        {
            Text = "Rocola";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 780);
            MinimumSize = new Size(820, 600);
            BackColor = Color.FromArgb(10, 14, 40);
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            lblStatus = new Label();
            TransLabel(lblStatus, 9.5f);
            lblStatus.Text = "Cargando musica...";
            helpBar = new HelpBar();
            lblAttract = new Label();
            lblAttract.AutoSize = false;
            lblAttract.TextAlign = ContentAlignment.MiddleCenter;
            lblAttract.BackColor = Color.FromArgb(10, 12, 30);
            lblAttract.ForeColor = Color.FromArgb(250, 230, 50);
            lblAttract.Font = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
            lblAttract.Text = "¡ PRESIONA UN BOTON PARA USAR LA ROCOLA !";
            lblAttract.Visible = false;
            letterBar = new LetterBar();
            letterBar.Available = letterAvailable;
            letterBar.OnPick = delegate(char c)
            {
                if (c != VideoMark || (videoAlbums != null && videoAlbums.Count != 0))
                {
                    currentLetter = c;
                    page = 0;
                    selAlbumIndex = 0;
                    letterBar.Current = c;
                    RefreshAlbums();
                    Focus();
                }
            };
            lblHeader = new Label();
            TransLabel(lblHeader, 12.5f);
            albumView = new AlbumView();
            albumView.GetCover = LoadCoverAsync;
            albumView.OnActivate = delegate(int local)
            {
                List<Album> list = CurrentList();
                int idx = page * PageSize + local;
                if (idx >= 0 && idx < list.Count)
                {
                    selAlbumIndex = idx;
                    albumView.Selected = local;
                    EnterAlbum(list[idx]);
                }
            };
            panelSongs = new BgPanel();
            panelSongs.BackColor = Shade(DefaultAccent, 0.7);
            panelSongs.Visible = false;
            lblAlbum = new Label();
            lblAlbum.BackColor = Color.Transparent;
            lblAlbum.ForeColor = Color.White;
            lblAlbum.Font = Bold(16f);
            cubeBox = new CubeBox();
            cubeBox.BackColor = Color.Transparent;
            lstSongs = new ListBox();
            lstSongs.BackColor = Shade(DefaultAccent, 0.76);
            lstSongs.ForeColor = Color.White;
            lstSongs.BorderStyle = BorderStyle.None;
            lstSongs.Font = Bold(13f);
            lstSongs.IntegralHeight = false;
            lstSongs.MultiColumn = false;
            lstSongs.DrawMode = DrawMode.OwnerDrawFixed;
            lstSongs.ItemHeight = 30;
            lstSongs.DrawItem += LstSongs_DrawItem;
            lstSongs.DoubleClick += delegate
            {
                EnqueueSelectedSong();
            };
            lstSongs.SelectedIndexChanged += LstSongs_SelectedIndexChanged;
            songPager = new SongPagerBar();
            songPager.Visible = false;
            songPager.BackColor = Shade(DefaultAccent, 0.7);
            panelSongs.Controls.Add(lblAlbum);
            panelSongs.Controls.Add(cubeBox);
            panelSongs.Controls.Add(lstSongs);
            panelSongs.Controls.Add(songPager);
            lblNow = new Label();
            TransLabel(lblNow, 13f);
            lblNow.Text = "Nada reproduciendose";
            lblNow.AutoEllipsis = true;
            lblNext = new Label();
            TransLabel(lblNext, 11f);
            lblNext.Text = "Siguiente:  -";
            lblNext.AutoEllipsis = true;
            lblCount = new Label();
            TransLabel(lblCount, 11f);
            lblCount.TextAlign = ContentAlignment.MiddleLeft;
            lblCount.Text = "En cola:  0";
            lblCredits = new Label();
            TransLabel(lblCredits, 14f);
            lblCredits.TextAlign = ContentAlignment.MiddleLeft;
            lblCredits.Text = "Inserta una moneda";
            lblCredits.ForeColor = Color.FromArgb(255, 205, 90);
            miniCube = new CubeBox();
            miniCube.BackColor = Color.Transparent;
            miniCube.Visible = false;
            progress = new ProgressView();
            lblTime = new Label();
            TransLabel(lblTime, 10.5f);
            lblTime.Text = "0:00 / 0:00";
            lblToast = new Label();
            lblToast.TextAlign = ContentAlignment.MiddleCenter;
            lblToast.BackColor = Color.FromArgb(40, 165, 90);
            lblToast.ForeColor = Color.White;
            lblToast.Font = Bold(14f);
            lblToast.Visible = false;
            searchOverlay = new SearchOverlay();
            searchOverlay.Visible = false;
            searchOverlay.GetCover = LoadCover;
            searchOverlay.OnQueryChanged = RecomputeResults;
            searchOverlay.OnChoose = ChooseSearchResult;
            searchOverlay.OnClose = CloseSearch;
            Controls.Add(lblStatus);
            Controls.Add(helpBar);
            Controls.Add(lblCredits);
            Controls.Add(letterBar);
            Controls.Add(lblHeader);
            Controls.Add(albumView);
            Controls.Add(panelSongs);
            Controls.Add(lblNow);
            Controls.Add(lblNext);
            Controls.Add(lblCount);
            Controls.Add(progress);
            Controls.Add(lblTime);
            Controls.Add(miniCube);
            Controls.Add(lblAttract);
            Controls.Add(lblToast);
            Controls.Add(searchOverlay);
            lblConfig = new Label();
            lblConfig.AutoSize = false;
            lblConfig.TextAlign = ContentAlignment.MiddleCenter;
            lblConfig.BackColor = Color.FromArgb(8, 10, 26);
            lblConfig.ForeColor = Color.FromArgb(245, 248, 255);
            lblConfig.Font = new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Bold);
            lblConfig.Text = "";
            lblConfig.Visible = false;
            Controls.Add(lblConfig);
            lblConfig.BringToFront();
        }

        private void LstSongs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstSongs.Items.Count)
            {
                return;
            }
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = e.Bounds;
            g.FillRectangle(Gdi.Brush(lstSongs.BackColor), bounds);
            if ((e.State & DrawItemState.Selected) != 0)
            {
                Rectangle r = new Rectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8, bounds.Height - 6);
                using (GraphicsPath path = RoundedPath(r, 9))
                {
                    g.FillPath(Gdi.Brush(Color.FromArgb(150, Lighten(bgAccent, 0.15))), path);
                    g.DrawPath(Gdi.Pen(Color.FromArgb(180, Lighten(bgAccent, 0.45)), 1.4f), path);
                }
            }
            string text = lstSongs.Items[e.Index].ToString();
            Rectangle textRect = new Rectangle(bounds.X + 16, bounds.Y, bounds.Width - 22, bounds.Height);
            TextRenderer.DrawText(g, text, lstSongs.Font, textRect, Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void LayoutControls()
        {
            if (albumView == null)
            {
                return;
            }
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int margin = 14;
            if (searchOverlay != null)
            {
                searchOverlay.Bounds = new Rectangle(0, 0, w, h);
            }
            if (videoFull && video != null)
            {
                int bannerH = 46;
                lblAttract.SetBounds(0, 0, w, bannerH);
                video.SetBounds(0, bannerH, w, h - bannerH);
                video.BringToFront();
                lblAttract.BringToFront();
                if (helpBar != null)
                {
                    helpBar.Visible = false;
                }
                return;
            }
            if (helpBar != null)
            {
                helpBar.Visible = true;
            }
            if (lblAttract != null)
            {
                lblAttract.Visible = false;
            }
            int videoX = videoOn ? ((int)(w * 0.5)) : (w - margin);
            int leftEdge = videoOn ? (videoX - 12) : (w - margin);
            int leftW = Math.Max(220, leftEdge - margin);
            int rightLimit = w - margin;
            int helpX = margin;
            int helpW = Math.Max(120, Math.Min(580, rightLimit - helpX));
            if (helpBar != null)
            {
                helpBar.SetBounds(helpX, 8, helpW, 34);
            }
            int statusX = helpX + helpW + 12;
            lblStatus.SetBounds(statusX, 15, Math.Max(40, rightLimit - statusX), 22);
            if (lblConfig != null)
            {
                lblConfig.SetBounds(0, 0, w, h);
            }
            int lettersY = 48;
            int lettersH = 40;
            letterBar.SetBounds(margin, lettersY, leftW, lettersH);
            int headerY = lettersY + lettersH + 10;
            lblHeader.SetBounds(margin, headerY + 3, Math.Max(50, leftW), 28);
            int miniSize = 64;
            int progressH = 16;
            int progressY = h - margin - progressH;
            int nextY = progressY - 26;
            int nowY = nextY - 26;
            int infoTop = nowY;
            int infoH = progressY + progressH - infoTop;
            int creditsW = 200;
            int creditsX = w - margin - creditsW;
            int miniX = creditsX - 10 - miniSize;
            int miniY = infoTop + (infoH - miniSize) / 2;
            if (miniY < infoTop)
            {
                miniY = infoTop;
            }
            miniCube.SetBounds(miniX, miniY, miniSize, miniSize);
            if (lblCredits != null)
            {
                lblCredits.SetBounds(creditsX, infoTop + 2, creditsW, 28);
            }
            lblCount.SetBounds(creditsX, infoTop + 34, creditsW, 22);
            int textLimit = miniX - 14;
            int textW = Math.Max(120, textLimit - margin);
            int timeW = 150;
            progress.SetBounds(margin, progressY, Math.Max(50, textW - timeW - 10), progressH);
            lblTime.SetBounds(margin + textW - timeW, progressY - 1, timeW, 24);
            lblNext.SetBounds(margin, nextY, textW, 22);
            lblNow.SetBounds(margin, nowY, textW, 24);
            int gridY = headerY + 44;
            int gridBottom = nowY - 12;
            int gridH = Math.Max(120, gridBottom - gridY);
            albumView.SetBounds(margin, gridY, leftW, gridH);
            panelSongs.SetBounds(margin, gridY, leftW, gridH);
            if (video != null)
            {
                int vx = videoX;
                int vy = lettersY;
                int vw = Math.Max(120, w - margin - vx);
                int vh = Math.Max(90, gridBottom - vy);
                video.SetBounds(vx, vy, vw, vh);
            }
            LayoutSongsPanel();
        }

        private void LayoutSongsPanel()
        {
            int w = panelSongs.ClientSize.Width;
            int h = panelSongs.ClientSize.Height;
            lblAlbum.SetBounds(8, 8, Math.Max(50, w - 16), 32);
            int cubeSize = Math.Min(h - 80, (int)(w * 0.4));
            if (cubeSize < 80)
            {
                cubeSize = 80;
            }
            cubeBox.SetBounds(12, 52, cubeSize, cubeSize);
            int listX = cubeSize + 30;
            int listW = Math.Max(60, w - cubeSize - 42);
            if (songPager != null)
            {
                songPager.SetBounds(listX, 52, listW, 24);
            }
            lstSongs.SetBounds(listX, 80, listW, Math.Max(60, h - 88));
            if (panelSongs.Visible && songItems.Count > 0 && lstSongs.ItemHeight > 0)
            {
                FillSongPage();
            }
        }

        private static float CtrlDot(Graphics g, float x, int cy, int d, Color c)
        {
            g.FillEllipse(Gdi.Brush(c), x, cy - d / 2f, d, d);
            g.DrawEllipse(Gdi.Pen(Color.FromArgb(130, 255, 255, 255), 1.4f), x, cy - d / 2f, d, d);
            return x + d;
        }

        private static float CtrlTxt(Graphics g, Font f, string s, float x, int cy, Color c)
        {
            SizeF size = g.MeasureString(s, f);
            g.DrawString(s, f, Gdi.Brush(c), x, cy - size.Height / 2f);
            return x + size.Width;
        }

        private static void CtrlTri(Graphics g, Color c, PointF a, PointF b, PointF d)
        {
            g.FillPolygon(Gdi.Brush(c), new PointF[3] { a, b, d });
        }

        private static float CtrlPad(Graphics g, float x, int cy, int sz)
        {
            Color cRed = Color.FromArgb(228, 72, 72);
            Color cYellow = Color.FromArgb(242, 206, 72);
            float cx = x + sz / 2f;
            float arrow = sz * 0.34f;
            float half = arrow / 1.4f;
            CtrlTri(g, cRed, new PointF(cx, cy - sz / 2f), new PointF(cx - half, cy - sz / 2f + arrow), new PointF(cx + half, cy - sz / 2f + arrow));
            CtrlTri(g, cRed, new PointF(cx, cy + sz / 2f), new PointF(cx - half, cy + sz / 2f - arrow), new PointF(cx + half, cy + sz / 2f - arrow));
            CtrlTri(g, cYellow, new PointF(x, cy), new PointF(x + arrow, cy - half), new PointF(x + arrow, cy + half));
            CtrlTri(g, cYellow, new PointF(x + sz, cy), new PointF(x + sz - arrow, cy - half), new PointF(x + sz - arrow, cy + half));
            return x + sz;
        }

        private static float CtrlDirLeft(Graphics g, float x, int cy, int sz, Color c)
        {
            PointF[] points = new PointF[3]
            {
                new PointF(x, cy),
                new PointF(x + sz, cy - sz * 0.5f),
                new PointF(x + sz, cy + sz * 0.5f)
            };
            g.FillPolygon(Gdi.Brush(c), points);
            g.DrawPolygon(Gdi.Pen(Color.FromArgb(130, 255, 255, 255), 1.4f), points);
            return x + sz;
        }

        private static float CtrlDirRight(Graphics g, float x, int cy, int sz, Color c)
        {
            PointF[] points = new PointF[3]
            {
                new PointF(x + sz, cy),
                new PointF(x, cy - sz * 0.5f),
                new PointF(x, cy + sz * 0.5f)
            };
            g.FillPolygon(Gdi.Brush(c), points);
            g.DrawPolygon(Gdi.Pen(Color.FromArgb(130, 255, 255, 255), 1.4f), points);
            return x + sz;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Text = "Form1";
        }
    }
}
