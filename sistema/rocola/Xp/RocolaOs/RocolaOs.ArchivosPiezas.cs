// ROCOLA OS (fase 11): LAS DOS PIEZAS PROPIAS DEL GESTOR DE ARCHIVOS DE CTRL+F4 (FASE11_navegador-archivos.md): la lista de una carpeta y el
// editor de texto. XP no tiene ni la una ni la otra en sus hojas (Ctrl+F2 usa una ListBox blanca de Windows), asi que se pintan como las
// piezas de Ctrl+F3 (las tarjetas, los botones planos y los deslizadores de Form1.Ajustes.cs): el fondo azul oscuro de la tarjeta, el
// contorno redondo con su borde, el renglon elegido en el color de acento, la letra de la hoja (y la de ancho fijo de XP, Courier New, en
// el editor), con el teclado y el raton de la hoja nativa (Nativo/Pieza.cs: la caja, el foco, el tabulador, el clic y el doble clic).
// Las teclas del editor se traducen aqui, con la distribucion de EE. UU. (la de la tabla de PantallaRocola.CaracterDe, completa con los
// signos: lo que hace falta para escribir una llave), y no pasan a CaracterDe (SuppressKeyPress).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // ======================================================================= LA LISTA DE UNA CARPETA
    internal sealed class RocolaOsListaArchivos : IPieza, IDisposable
    {
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        internal List<RocolaOsEntrada> Renglones = new List<RocolaOsEntrada>();
        private int sel = -1, top;
        internal const int Alto = 24, Cabecera = 26;
        internal Color Acento = Color.FromArgb(150, 205, 255);
        internal string TituloFecha = "Fecha";   // (la papelera: «Se tiró el»)
        internal Letra Letra, LetraChica;
        internal event Action Elegido;     // cambio el elegido
        internal event Action Abre;        // doble clic o Intro
        internal event Action Sube;        // Retroceso
        internal RocolaOsListaArchivos() { caja = new CajaPieza(this, 0, 0); caja.RepintaConMedida = true; caja.Seleccionable = true; caja.Transparente = true; caja.BackColor = Color.Transparent; }
        public void Dispose() { caja.Desecha(null); }
        internal void SetBounds(int x, int y, int w, int h) { caja.SetBounds(x, y, w, h); }
        internal bool Focus() { return caja.Focus(); }
        internal int Elegida { get { return sel; } set { sel = Math.Max(-1, Math.Min(value, Renglones.Count - 1)); AVista(); caja.Invalidate(); } }
        internal RocolaOsEntrada LaElegida { get { return sel >= 0 && sel < Renglones.Count ? Renglones[sel] : null; } }
        internal void Pon(List<RocolaOsEntrada> l, int elegida) { Renglones = l ?? new List<RocolaOsEntrada>(); top = 0; sel = -1; Elegida = elegida; }
        private int Visibles { get { return Math.Max(1, (caja.Bounds.Height - Cabecera - 4) / Alto); } }
        private void AVista()
        {
            if (sel < 0) { top = 0; return; }
            if (sel < top) top = sel;
            if (sel >= top + Visibles) top = sel - Visibles + 1;
            top = Math.Max(0, Math.Min(top, Math.Max(0, Renglones.Count - Visibles)));
        }
        private string busca = ""; private long ultimaLetra;
        private void Elige(int i) { int a = sel; Elegida = i; if (a != sel) { Action h = Elegido; if (h != null) h(); } }

        bool IPieza.QuiereTecla(Tecla k)
        {
            Tecla c = k & Tecla.KeyCode;
            return (k & (Tecla.Control | Tecla.Alt)) == 0 && (c == Tecla.Up || c == Tecla.Down || c == Tecla.PageUp || c == Tecla.PageDown || c == Tecla.Home || c == Tecla.End || c == Tecla.Enter || c == Tecla.Back);
        }
        void IPieza.TeclaAbajo(PulsoTecla e)
        {
            if (!caja.Enabled || (e.KeyData & (Tecla.Control | Tecla.Alt)) != 0) return;
            int n = Renglones.Count; if (n == 0 && e.KeyCode != Tecla.Back) return;
            switch (e.KeyCode)
            {
                case Tecla.Up: Elige(sel <= 0 ? 0 : sel - 1); break;
                case Tecla.Down: Elige(sel < 0 ? 0 : Math.Min(n - 1, sel + 1)); break;
                case Tecla.PageUp: Elige(Math.Max(0, sel - Visibles)); break;
                case Tecla.PageDown: Elige(Math.Min(n - 1, Math.Max(0, sel) + Visibles)); break;
                case Tecla.Home: Elige(0); break;
                case Tecla.End: Elige(n - 1); break;
                case Tecla.Enter: { Action h = Abre; if (h != null && sel >= 0) h(); } break;
                case Tecla.Back: { Action h = Sube; if (h != null) h(); } break;
                default:
                    {
                        // ESCRIBIR EL PRINCIPIO DEL NOMBRE salta a el (como el Explorador de Windows): lo escrito se junta mientras no pase 1,5 s
                        char ch = RocolaOsEditor.CaracterUs(e.KeyCode, (e.KeyData & Tecla.Shift) != 0);
                        if (ch == '\0' || n == 0) return;
                        long ahora = Environment.TickCount64;
                        if (ahora - ultimaLetra > 1500) busca = "";
                        ultimaLetra = ahora; busca += ch;
                        for (int i = 0; i < n; i++)
                            if (!Renglones[i].Subir && Renglones[i].Nombre.StartsWith(busca, StringComparison.OrdinalIgnoreCase)) { Elige(i); break; }
                        break;
                    }
            }
            e.SuppressKeyPress = true;
        }
        private int RenglonEn(int y) { int i = (y - Cabecera - 2) / Alto; return y < Cabecera + 2 ? -1 : top + i; }
        void IPieza.RatonAbajo(Raton e)
        {
            caja.Focus();
            if (e.Button != BotonRaton.Left) return;
            int i = RenglonEn(e.Y);
            if (i >= 0 && i < Renglones.Count) Elige(i);
        }
        void IPieza.DobleClic(Raton e) { int i = RenglonEn(e.Y); if (i >= 0 && i < Renglones.Count && i == sel) { Action h = Abre; if (h != null) h(); } }
        void IPieza.Rueda(Raton e)
        {
            e.Handled = true;
            top = Math.Max(0, Math.Min(top - Math.Sign(e.Delta) * 3, Math.Max(0, Renglones.Count - Visibles)));
            caja.Invalidate();
        }
        void IPieza.AlRecibirFoco() { caja.Invalidate(); }
        void IPieza.AlPerderFoco() { caja.Invalidate(); }

        void IPieza.PintaContenido(SKCanvas g)
        {
            Rectangle b = caja.Bounds; int w = b.Width, h = b.Height;
            AjusteLetra aj = AjusteLetra.ClearTypeAjustada;
            bool foco = caja.Focused;
            RocolaOsPinta.Caja(g, new Rectangle(0, 0, w - 1, h - 1), Color.FromArgb(22, 28, 56), foco ? Color.FromArgb(110, 140, 210) : Color.FromArgb(46, 58, 104), foco ? 2f : 1f);
            Letra f = Letra ?? caja.Font, fc = LetraChica ?? f;
            int cTam = w - 300, cFecha = w - 166;
            Color tenue = Color.FromArgb(138, 149, 182);
            RocolaOsPinta.Texto(g, "Nombre", fc, tenue, 40, 6, aj);
            RocolaOsPinta.TextoDerecha(g, "Tamaño", fc, tenue, cFecha - 24, 6, aj);
            RocolaOsPinta.Texto(g, TituloFecha, fc, tenue, cFecha, 6, aj);
            PinturaXp.Rellena(g, new Rectangle(10, Cabecera, w - 20, 1), Color.FromArgb(46, 58, 104));
            if (Renglones.Count == 0) { RocolaOsPinta.Texto(g, "(vacía)", f, tenue, 40, Cabecera + 8, aj); return; }
            int vis = Visibles;
            for (int k = 0; k < vis && top + k < Renglones.Count; k++)
            {
                int i = top + k; RocolaOsEntrada en = Renglones[i];
                int y = Cabecera + 2 + k * Alto;
                if (i == sel)
                {
                    Color rel = foco ? Color.FromArgb(28, 62, 104) : Color.FromArgb(30, 38, 70);
                    RocolaOsPinta.Caja(g, new Rectangle(4, y, w - 20, Alto - 1), rel, foco ? Acento : Color.FromArgb(60, 72, 118), 1f);
                }
                RocolaOsPinta.Icono(g, 14, y + 4, en);
                Color c = en.Subir ? tenue : Color.FromArgb(222, 230, 250);
                string nom = en.Subir ? ".. (subir)" : en.Nombre + (en.Carpeta ? "/" : "") + (en.Enlace ? "  (enlace)" : "");
                RocolaOsPinta.Texto(g, RocolaOsPinta.Recorta(nom, f, cTam - 60, aj), f, c, 40, y + 3, aj);
                if (!en.Carpeta) RocolaOsPinta.TextoDerecha(g, RocolaOsArchivos.Medida(en.Tamano), f, c, cFecha - 24, y + 3, aj);
                if (!en.Subir) RocolaOsPinta.Texto(g, en.Fecha.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), f, tenue, cFecha, y + 3, aj);
            }
            // la barrita de donde se esta (sin flechas: la rueda, las teclas y el clic)
            if (Renglones.Count > vis)
            {
                int alto = h - Cabecera - 8;
                int pul = Math.Max(20, alto * vis / Renglones.Count);
                int py = Cabecera + 4 + (alto - pul) * top / Math.Max(1, Renglones.Count - vis);
                RocolaOsPinta.Caja(g, new Rectangle(w - 12, py, 6, pul), Color.FromArgb(70, 84, 132), Color.FromArgb(70, 84, 132), 1f);
            }
        }
    }

    // ======================================================================= EL EDITOR DE TEXTO
    internal sealed class RocolaOsEditor : IPieza, IDisposable
    {
        private readonly CajaPieza caja;
        CajaPieza IPieza.Caja { get { return caja; } }
        private List<string> lin = new List<string> { "" };
        private int fil, col, top, izq;   // el cursor; el primer renglon y la primera columna que se ven
        internal bool UnRenglon;           // (renombrar y archivos nuevos: Intro no parte el renglon)
        internal bool SoloLeer;
        internal bool Tapado;              // las llaves: cada renglon con sus 3 primeros y 3 ultimos
        internal bool Cambiado;
        internal int Tope = RocolaOsArchivos.MaxTexto;
        internal Letra Letra;
        internal event Action Intro;       // (UnRenglon) Intro
        internal event Action Guarda;      // Ctrl+S
        internal event Action Cambia;      // el texto cambio
        internal RocolaOsEditor() { caja = new CajaPieza(this, 0, 0); caja.RepintaConMedida = true; caja.Seleccionable = true; caja.Transparente = true; caja.BackColor = Color.Transparent; }
        public void Dispose() { caja.Desecha(null); }
        internal void SetBounds(int x, int y, int w, int h) { caja.SetBounds(x, y, w, h); }
        internal bool Focus() { return caja.Focus(); }
        internal int TabIndex { set { caja.TabIndex = value; } }
        internal string Texto
        {
            get { return string.Join("\n", lin); }
            set { lin = new List<string>((value ?? "").Replace("\r\n", "\n").Split('\n')); fil = 0; col = 0; top = 0; izq = 0; Cambiado = false; caja.Invalidate(); }
        }
        internal void AlFinal() { fil = lin.Count - 1; col = lin[fil].Length; AVista(); caja.Invalidate(); }
        internal int Renglones { get { return lin.Count; } }
        internal int Fila { get { return fil; } }
        internal int Columna { get { return col; } }
        private int Largo() { int n = 0; foreach (string s in lin) n += Encoding.UTF8.GetByteCount(s) + 1; return n; }

        // ---- medidas (letra de ancho fijo) ----
        private float anchoLetra = -1;
        private float AnchoLetra()
        {
            if (anchoLetra > 0) return anchoLetra;
            Letra f = Letra ?? caja.Font;
            SizeF a = TextoGdiPlus.Mide(new string('M', 21), f, AjusteLetra.ClearTypeAjustada, new SizeF(0, 0), FormatoTexto.SinFormato);
            SizeF b = TextoGdiPlus.Mide("M", f, AjusteLetra.ClearTypeAjustada, new SizeF(0, 0), FormatoTexto.SinFormato);
            anchoLetra = Math.Max(1f, (a.Width - b.Width) / 20f);
            return anchoLetra;
        }
        private int AltoRenglon { get { Letra f = Letra ?? caja.Font; return Math.Max(12, (int)Math.Ceiling(f.AltoRenglon) + 2); } }
        private const int Margen = 10;
        private int Visibles { get { return UnRenglon ? 1 : Math.Max(1, (caja.Bounds.Height - 2 * Margen) / AltoRenglon); } }
        private int ColsVisibles { get { return Math.Max(4, (int)((caja.Bounds.Width - 2 * Margen - 10) / AnchoLetra())); } }
        private void AVista()
        {
            if (fil < top) top = fil;
            if (fil >= top + Visibles) top = fil - Visibles + 1;
            if (col < izq) izq = Math.Max(0, col - 8);
            if (col >= izq + ColsVisibles) izq = col - ColsVisibles + 8;
            if (izq < 0) izq = 0;
        }

        // ---- las teclas ----
        bool IPieza.QuiereTecla(Tecla k)
        {
            if ((k & Tecla.Alt) != 0) return false;
            Tecla c = k & Tecla.KeyCode;
            if (c == Tecla.Tab || c == Tecla.Escape) return false;
            if (c == Tecla.Enter) return !UnRenglon;
            return true;   // las flechas, Inicio, Fin, Retroceso, Suprimir... y lo que se escribe
        }
        // EL CARACTER DE UNA TECLA, distribucion de EE. UU. (la de PantallaRocola.CaracterDe con todos sus signos)
        internal static char CaracterUs(Tecla k, bool may)
        {
            if (k >= Tecla.A && k <= Tecla.Z) return (char)((may ? 'A' : 'a') + (k - Tecla.A));
            if (k >= Tecla.D0 && k <= Tecla.D9) return may ? ")!@#$%^&*("[k - Tecla.D0] : (char)('0' + (k - Tecla.D0));
            if (k >= Tecla.NumPad0 && k <= Tecla.NumPad9) return (char)('0' + (k - Tecla.NumPad0));
            switch (k)
            {
                case Tecla.Space: return ' ';
                case Tecla.OemMinus: return may ? '_' : '-';
                case Tecla.Oemplus: return may ? '+' : '=';
                case Tecla.OemOpenBrackets: return may ? '{' : '[';
                case Tecla.OemCloseBrackets: return may ? '}' : ']';
                case Tecla.OemPipe: case Tecla.OemBackslash: return may ? '|' : '\\';
                case Tecla.OemSemicolon: return may ? ':' : ';';
                case Tecla.OemQuotes: return may ? '"' : '\'';
                case Tecla.Oemcomma: return may ? '<' : ',';
                case Tecla.OemPeriod: return may ? '>' : '.';
                case Tecla.OemQuestion: return may ? '?' : '/';
                case Tecla.Oemtilde: return may ? '~' : '`';
                case Tecla.Multiply: return '*';
                case Tecla.Add: return '+';
                case Tecla.Subtract: return '-';
                case Tecla.Divide: return '/';
                case Tecla.Decimal: return '.';
            }
            return '\0';
        }
        void IPieza.TeclaAbajo(PulsoTecla e)
        {
            Tecla k = e.KeyCode; bool ctrl = (e.KeyData & Tecla.Control) != 0, may = (e.KeyData & Tecla.Shift) != 0;
            if ((e.KeyData & Tecla.Alt) != 0) return;
            bool hecho = true;
            if (ctrl && k == Tecla.S) { Action g = Guarda; if (g != null) g(); }
            else if (k == Tecla.Left) { if (col > 0) col--; else if (fil > 0) { fil--; col = lin[fil].Length; } }
            else if (k == Tecla.Right) { if (col < lin[fil].Length) col++; else if (fil < lin.Count - 1) { fil++; col = 0; } }
            else if (k == Tecla.Up) { if (fil > 0) { fil--; col = Math.Min(col, lin[fil].Length); } }
            else if (k == Tecla.Down) { if (fil < lin.Count - 1) { fil++; col = Math.Min(col, lin[fil].Length); } }
            else if (k == Tecla.Home) { if (ctrl) fil = 0; col = 0; }
            else if (k == Tecla.End) { if (ctrl) fil = lin.Count - 1; col = lin[fil].Length; }
            else if (k == Tecla.PageUp) { fil = Math.Max(0, fil - Visibles); col = Math.Min(col, lin[fil].Length); }
            else if (k == Tecla.PageDown) { fil = Math.Min(lin.Count - 1, fil + Visibles); col = Math.Min(col, lin[fil].Length); }
            else if (SoloLeer || ctrl) hecho = ctrl && k != Tecla.ControlKey;
            else if (k == Tecla.Back)
            {
                if (col > 0) { lin[fil] = lin[fil].Remove(col - 1, 1); col--; Cambio(); }
                else if (fil > 0) { col = lin[fil - 1].Length; lin[fil - 1] += lin[fil]; lin.RemoveAt(fil); fil--; Cambio(); }
            }
            else if (k == Tecla.Delete)
            {
                if (col < lin[fil].Length) { lin[fil] = lin[fil].Remove(col, 1); Cambio(); }
                else if (fil < lin.Count - 1) { lin[fil] += lin[fil + 1]; lin.RemoveAt(fil + 1); Cambio(); }
            }
            else if (k == Tecla.Enter)
            {
                if (UnRenglon) { Action h = Intro; if (h != null) h(); }
                else if (Largo() < Tope) { string r = lin[fil].Substring(col); lin[fil] = lin[fil].Substring(0, col); lin.Insert(fil + 1, r); fil++; col = 0; Cambio(); }
            }
            else
            {
                char ch = CaracterUs(k, may);
                if (ch != '\0') { if (Largo() + Encoding.UTF8.GetByteCount(ch.ToString()) <= Tope) { lin[fil] = lin[fil].Insert(col, ch.ToString()); col++; Cambio(); } }
                else hecho = false;
            }
            if (!hecho) return;
            e.SuppressKeyPress = true;   // (y no pasa a CaracterDe)
            AVista(); caja.Invalidate();
        }
        private void Cambio() { Cambiado = true; Action h = Cambia; if (h != null) h(); }
        void IPieza.RatonAbajo(Raton e)
        {
            caja.Focus();
            if (e.Button != BotonRaton.Left) return;
            int f = UnRenglon ? 0 : top + Math.Max(0, (e.Y - Margen) / AltoRenglon);
            fil = Math.Max(0, Math.Min(lin.Count - 1, f));
            col = Math.Max(0, Math.Min(lin[fil].Length, izq + (int)Math.Round((e.X - Margen) / AnchoLetra())));
            caja.Invalidate();
        }
        void IPieza.Rueda(Raton e)
        {
            e.Handled = true;
            if (UnRenglon) return;
            top = Math.Max(0, Math.Min(top - Math.Sign(e.Delta) * 3, Math.Max(0, lin.Count - Visibles)));
            caja.Invalidate();
        }
        void IPieza.AlRecibirFoco() { caja.Invalidate(); }
        void IPieza.AlPerderFoco() { caja.Invalidate(); }

        void IPieza.PintaContenido(SKCanvas g)
        {
            Rectangle b = caja.Bounds; int w = b.Width, h = b.Height;
            bool foco = caja.Focused;
            RocolaOsPinta.Caja(g, new Rectangle(0, 0, w - 1, h - 1), Color.FromArgb(14, 18, 40), foco ? Color.FromArgb(110, 140, 210) : Color.FromArgb(46, 58, 104), foco ? 2f : 1f);
            Letra f = Letra ?? caja.Font;
            AjusteLetra aj = AjusteLetra.ClearTypeAjustada;
            float aw = AnchoLetra(); int ar = AltoRenglon;
            int y0 = UnRenglon ? (h - ar) / 2 : Margen;
            g.Save();
            g.ClipRect(new SKRect(4, 2, w - 4, h - 2));
            Color tinta = SoloLeer ? Color.FromArgb(190, 198, 222) : Color.FromArgb(232, 238, 255);
            int cols = ColsVisibles + 2;
            for (int k = 0; k < Visibles && top + k < lin.Count; k++)
            {
                string s = lin[top + k];
                if (Tapado) s = RocolaOsArchivos.Tapa(s);
                if (izq > 0) s = izq < s.Length ? s.Substring(izq) : "";
                if (s.Length > cols) s = s.Substring(0, cols);
                RocolaOsPinta.Texto(g, s, f, tinta, Margen, y0 + k * ar, aj);
            }
            g.Restore();
            if (foco && !SoloLeer && fil >= top && fil < top + Visibles)
            {
                float x = Margen + (col - izq) * aw + 2;
                PinturaXp.Rellena(g, new RectangleF(x, y0 + (fil - top) * ar + 1, 2, ar - 2), Color.FromArgb(150, 205, 255));
            }
        }
    }

    // ======================================================================= LO QUE PINTAN LAS DOS
    internal static class RocolaOsPinta
    {
        // el contorno redondo de las piezas de Ctrl+F3 (radio 9, como el boton plano)
        internal static void Caja(SKCanvas g, Rectangle r, Color relleno, Color borde, float ancho)
        {
            using (SKPaint b = PinturaXp.Pincel(relleno))
            using (SKPaint p = PinturaXp.Pluma(borde, ancho))
            using (SKPath pa = new SKPath())
            {
                pa.AddRoundRect(new SKRect(r.X + 0.5f, r.Y + 0.5f, r.Right + 0.5f, r.Bottom + 0.5f), 9, 9);
                Pixel.Rellena(g, pa, b, true); Pixel.Traza(g, pa, p, true);
            }
        }
        internal static void Texto(SKCanvas g, string s, Letra f, Color c, float x, float y, AjusteLetra aj)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (SKPaint t = PinturaXp.Pincel(c)) TextoGdiPlus.Escribe(g, s, f, aj, t, new RectangleF(x, y, 0, 0), FormatoTexto.SinFormato);
        }
        internal static void TextoDerecha(SKCanvas g, string s, Letra f, Color c, float xDer, float y, AjusteLetra aj)
        {
            SizeF m = TextoGdiPlus.Mide(s, f, aj, new SizeF(0, 0), FormatoTexto.SinFormato);
            Texto(g, s, f, c, xDer - m.Width, y, aj);
        }
        internal static string Recorta(string s, Letra f, float ancho, AjusteLetra aj)
        {
            if (TextoGdiPlus.Mide(s, f, aj, new SizeF(0, 0), FormatoTexto.SinFormato).Width <= ancho) return s;
            int lo = 0, hi = s.Length;
            while (lo < hi) { int m = (lo + hi + 1) / 2; if (TextoGdiPlus.Mide(s.Substring(0, m) + "...", f, aj, new SizeF(0, 0), FormatoTexto.SinFormato).Width <= ancho) lo = m; else hi = m - 1; }
            return s.Substring(0, lo) + "...";
        }
        // una carpeta (amarilla), un archivo (una hoja) o una llave (un circulo con su vastago), dibujados
        internal static void Icono(SKCanvas g, int x, int y, RocolaOsEntrada e)
        {
            if (e.Subir) { PinturaXp.Linea(g, x + 8, y + 3, x + 8, y + 14, Color.FromArgb(138, 149, 182), 2f, true); PinturaXp.Linea(g, x + 3, y + 8, x + 8, y + 3, Color.FromArgb(138, 149, 182), 2f, true); PinturaXp.Linea(g, x + 13, y + 8, x + 8, y + 3, Color.FromArgb(138, 149, 182), 2f, true); return; }
            if (e.Carpeta)
            {
                Caja(g, new Rectangle(x, y + 1, 7, 4), Color.FromArgb(230, 186, 90), Color.FromArgb(230, 186, 90), 1f);
                Caja(g, new Rectangle(x, y + 4, 17, 11), Color.FromArgb(240, 200, 100), Color.FromArgb(200, 160, 70), 1f);
                return;
            }
            if (e.Llave)
            {
                using (SKPaint p = PinturaXp.Pluma(Color.FromArgb(255, 196, 96), 2f)) { p.IsAntialias = true; g.DrawCircle(x + 5, y + 8, 4, p); }
                PinturaXp.Linea(g, x + 9, y + 8, x + 17, y + 8, Color.FromArgb(255, 196, 96), 2f, true);
                PinturaXp.Linea(g, x + 14, y + 8, x + 14, y + 12, Color.FromArgb(255, 196, 96), 2f, true);
                return;
            }
            Caja(g, new Rectangle(x + 2, y, 12, 16), Color.FromArgb(200, 210, 235), Color.FromArgb(150, 160, 190), 1f);
            PinturaXp.Linea(g, x + 5, y + 6, x + 11, y + 6, Color.FromArgb(120, 130, 160), 1f, false);
            PinturaXp.Linea(g, x + 5, y + 9, x + 11, y + 9, Color.FromArgb(120, 130, 160), 1f, false);
        }
    }
}
