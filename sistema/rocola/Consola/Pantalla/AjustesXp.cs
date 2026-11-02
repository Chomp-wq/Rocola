// LOS AJUSTES DEL DUENO (Ctrl+F3) CON EL ASPECTO DE LA VENTANA DE XP (Form1.Ajustes.cs), dibujados aqui con OpenGL ES directo: la
// ventana con su filo azul y la barra de titulo («Rocola - Sonido · Esta rocola es del ...»), el titulo de la pagina con sus
// pestanas a la derecha (Colores, Detalles, Sonido, Imagen, Fondo, Pantalla: las que aqui no tienen nada van apagadas), las tarjetas
// por grupo con su rayita de color y su titulo en mayusculas, los interruptores verdes, los botones y abajo «Listo».
// Se sigue manejando con los botones de la rocola (ARRIBA/ABAJO eligen, IZQ/DER o PONER cambian, REGRESAR guarda y sale): lo elegido
// va resaltado y la pagina se corre para que siempre se vea.
using System;
using System.Collections.Generic;
using System.IO;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private static readonly string[] PestanasXp = { "Colores", "Detalles", "Sonido", "Imagen", "Fondo", "Pantalla" };
        private float ajusteCorre;   // lo que la pagina va corrida (suave)
        private string ajustePagina = "";
        private string selloRocola;

        private static string SubtituloPagina(string pg)
        {
            if (pg == "Sonido") return "Todo se oye al instante en la cancion que suena.";
            if (pg == "Pantalla") return "Lo que se ve en la rocola. Cada cosa con su interruptor.";
            if (pg == "Colores") return "El color del cielo con cada cancion: cuanto manda la portada, cuanto azul y cuanto negro. Se ve al instante.";
            return "Al pedir canciones, el fondo y la barra, internet, el diagnostico y los botones de la rocola.";
        }
        private static Color4 ColorGrupo(string g)
        {
            switch (g)
            {
                case "El billar": case "Afinar con el celular": return Color4.Hex(0xFFB84D);
                case "El local": case "Voz, golpe y retumbe": return Color4.Hex(0x5AAEFF);
                case "Lo que suena ahora": case "Entre canciones": case "Videos": return Color4.Hex(0x58D888);
                case "Volumen al limite": return Color4.Hex(0x8CC8FF);
                case "Al pedir canciones": return Color4.Hex(0x96DCB4);   // los de la hoja Detalles de XP (cVida y cFon)
                case "El fondo y la barra": return Color4.Hex(0x8CCDFF);
                case "Tu biblioteca": return Color4.Hex(0xFFA850);
                case "Dejar como de fabrica": return Color4.Hex(0xF06460);
                case "Mejoras con IA": return Color4.Hex(0xC08CFF);
                default: return Color4.Hex(0x8CB4FF);
            }
        }

        private void Ajustes(Controlador c, float W, float H)
        {
            IList<Form1.Ajuste> l = c.ListaAjustes;
            int n = c.AjustesTotal, sel = c.AjusteSel;
            string pagina = sel < l.Count ? Form1.PaginaDe(l[sel].Grupo) : "Detalles";
            if (pagina != ajustePagina) { ajustePagina = pagina; ajusteCorre = 0; }
            if (selloRocola == null)
            {
                try { string exe = Environment.ProcessPath; selloRocola = exe != null ? File.GetLastWriteTime(exe).ToString("dd/MM/yyyy HH:mm") : ""; } catch { selloRocola = ""; }
            }
            Letra fTit = Ui(13.3f * k), fNom = Ui(13.3f * k), fChica = UiFina(11 * k), fPag = Ui(20 * k), fPest = Ui(13.3f * k), fVal = Ui(14.6f * k);
            Color4 tenue = Color4.Hex(0x9AA6C8), blancoX = Color4.Hex(0xF2F5FF);

            // LA VENTANA: el filo azul, la barra de titulo y el fondo
            float wx = 30 * k, wy = 2 * k, ww = W - 60 * k, wh = H - 4 * k;
            d.Rect(0, 0, W, H, new Color4(0.02f, 0.03f, 0.08f, 0.55f));
            d.Rect(wx - 3 * k, wy - 2 * k, ww + 6 * k, wh + 4 * k, Color4.Hex(0x2456E6));
            d.Degradado(wx, wy, ww, wh, Color4.Hex(0x10172E), Color4.Hex(0x0C1226));
            d.DegradadoH(wx, wy, ww, 26 * k, Color4.Hex(0x2456E6), Color4.Hex(0x1E44C8));
            string titulo = "Rocola - " + pagina + (selloRocola.Length > 0 ? "   ·   Esta rocola es del " + selloRocola : "");
            fTit.Escribe(d, fTit.Ajusta(titulo, ww - 20 * k), wx + 10 * k, wy + 13 * k - fTit.Tam * 0.62f, Blanco);

            // el titulo de la pagina, su subtitulo y las pestanas
            float px = wx + 28 * k;
            fPag.Escribe(d, pagina, px, wy + 52 * k - fPag.Tam * 0.62f, Blanco);
            fChica.Escribe(d, SubtituloPagina(pagina), px + 2 * k, wy + 80 * k - fChica.Tam * 0.62f, tenue);
            float tw = 100 * k, th = 30 * k, tg = 6 * k, tx = wx + ww - 22 * k - PestanasXp.Length * tw - (PestanasXp.Length - 1) * tg, ty = wy + 36 * k;
            foreach (string pe in PestanasXp)
            {
                bool esta = pe == pagina, hay = Array.IndexOf(Form1.PaginasAjustes, pe) >= 0;
                d.Redondo(tx, ty, tw, th, 7 * k, esta ? Color4.Hex(0x274A9E) : Color4.Hex(0x1A2240, 0.9f));
                d.Contorno(tx, ty, tw, th, 7 * k, esta ? 1.6f * k : 1f * k, esta ? Color4.Hex(0x8CB4FF) : Color4.Hex(0x3A4570));
                fPest.EscribeCentrado(d, pe, tx + tw / 2, ty + th / 2 - fPest.Tam * 0.62f, esta ? Blanco : hay ? Color4.Hex(0xAAB4D4) : Color4.Hex(0x5A6488));
                tx += tw + tg;
            }

            // ABAJO: el recuadro de lo que suena (o de la prueba, internet, afinar) y la barra con «Listo»
            float by = wy + wh - 58 * k, bh = 38 * k;
            string[] lq = (c.LoQueSuena ?? "").Split('\n');
            bool recuadro = pagina != "Pantalla";   // en la Pantalla no dice nada de lo que se ve: mas sitio para las tarjetas
            float lin = 16 * k, ra = recuadro ? 34 * k + Math.Min(6, lq.Length) * lin : 0, ry = by - 12 * k - ra;
            if (recuadro) Tarjeta(px - 8 * k, ry, ww - 40 * k, ra, c.RecuadroTitulo, Color4.Hex(0x58D888), fTit);
            for (int j = 0; recuadro && j < lq.Length && j < 6; j++) fChica.Escribe(d, fChica.Ajusta(lq[j], ww - 80 * k), px + 10 * k, ry + 32 * k + j * lin, j == 0 ? blancoX : tenue);
            fChica.Escribe(d, "ARRIBA/ABAJO: elegir     IZQ/DER o PONER: cambiar     REGRESAR: guardar y salir", px, by + bh / 2 - fChica.Tam * 0.62f, tenue);
            float lw = 140 * k, lx = wx + ww - 22 * k - lw;
            d.Redondo(lx, by, lw, bh, 8 * k, Color4.Hex(0x1F6B3A));
            d.Contorno(lx, by, lw, bh, 8 * k, 1.4f * k, Color4.Hex(0x58D888));
            fNom.EscribeCentrado(d, "Listo", lx + lw / 2, by + bh / 2 - fNom.Tam * 0.62f, Blanco);

            // LAS TARJETAS de la pagina, en dos columnas (cada una a la columna mas corta), con lo elegido siempre a la vista
            float cy0 = wy + 98 * k, cy1 = ry - 10 * k, colW = (ww - 40 * k - 16 * k) / 2, fila = 30 * k, cab = 36 * k;
            List<int> idx = new List<int>();
            for (int i = 0; i < l.Count; i++) if (Form1.PaginaDe(l[i].Grupo) == pagina) idx.Add(i);
            bool conBotones = pagina == "Detalles";
            // agrupa
            List<string> grupos = new List<string>(); Dictionary<string, List<int>> porGrupo = new Dictionary<string, List<int>>();
            foreach (int i in idx) { string g = l[i].Grupo; List<int> li; if (!porGrupo.TryGetValue(g, out li)) { li = new List<int>(); porGrupo[g] = li; grupos.Add(g); } li.Add(i); }
            if (conBotones) { grupos.Add("Los botones"); porGrupo["Los botones"] = new List<int> { n - 1 }; }
            // TU BIBLIOTECA (la tarjeta de XP): solo informa, no se elige; despues del volumen
            List<string> bib = pagina == "Sonido" ? c.motor.TextoBiblioteca() : null;
            if (bib != null && !porGrupo.ContainsKey("Tu biblioteca")) { int pv = grupos.IndexOf("Volumen al limite"); grupos.Insert(pv >= 0 ? pv + 1 : 0, "Tu biblioteca"); porGrupo["Tu biblioteca"] = new List<int>(); }
            // un grupo muy largo (la Pantalla) se parte en dos tarjetas, una por columna
            for (int gi = 0; gi < grupos.Count; gi++)
            {
                List<int> li = porGrupo[grupos[gi]];
                if (li.Count <= 12) continue;
                int mitad = (li.Count + 1) / 2; string g2 = grupos[gi] + "\u0001";
                porGrupo[g2] = li.GetRange(mitad, li.Count - mitad); porGrupo[grupos[gi]] = li.GetRange(0, mitad);
                grupos.Insert(gi + 1, g2); gi++;
            }
            // el alto de cada renglon: el de siempre, mas su nota (hasta tres renglones) y su barra
            Dictionary<int, float> altoDe = new Dictionary<int, float>(); Dictionary<int, List<string>> notaDe = new Dictionary<int, List<string>>();
            foreach (string g in grupos) foreach (int i in porGrupo[g])
            {
                float h = fila;
                if (i < l.Count)
                {
                    string nt = l[i].Explicacion;
                    if (!string.IsNullOrEmpty(nt))
                    {
                        string tv = l[i].Texto ?? ""; float sangria = tv == "SÍ" || tv == "NO" ? 50 * k : 0;
                        List<string> ln = Envuelve(nt, fChica, colW - 36 * k - sangria);
                        if (ln.Count > 3) { ln[2] = fChica.Ajusta(ln[2] + " " + string.Join(" ", ln.GetRange(3, ln.Count - 3)), colW - 36 * k - sangria); ln.RemoveRange(3, ln.Count - 3); }
                        notaDe[i] = ln; h += ln.Count * 14 * k;
                    }
                    if (l[i].Posicion.HasValue) h += 24 * k;
                }
                altoDe[i] = h;
            }
            // reparte en columnas
            float[] alto = new float[2]; var lugar = new List<KeyValuePair<string, float[]>>();
            float selY = 0, selH = fila;
            foreach (string g in grupos)
            {
                int col = alto[0] <= alto[1] ? 0 : 1;
                float h = cab + 12 * k, acum = g == "Tu biblioteca" && bib != null ? bib.Count * 18 * k + 4 * k : 0;
                foreach (int i in porGrupo[g]) { if (i == sel) { selY = alto[col] + cab + acum; selH = altoDe[i]; } acum += altoDe[i]; }
                h += acum;
                lugar.Add(new KeyValuePair<string, float[]>(g, new float[] { col, alto[col], h }));
                alto[col] += h + 14 * k;
            }
            float total = Math.Max(alto[0], alto[1]), vis = cy1 - cy0;
            float quiere = total <= vis ? 0 : Math.Max(0, Math.Min(total - vis, selY + selH / 2 - vis / 2));
            ajusteCorre += (quiere - ajusteCorre) * 0.35f; if (Math.Abs(quiere - ajusteCorre) < 0.5f) ajusteCorre = quiere;
            d.Recorta(wx, cy0, ww, vis);
            foreach (var lg in lugar)
            {
                string g = lg.Key; float cx = px - 8 * k + lg.Value[0] * (colW + 16 * k), y = cy0 + lg.Value[1] - ajusteCorre, h = lg.Value[2];
                if (y > cy1 || y + h < cy0) continue;
                Tarjeta(cx, y, colW, h, g.TrimEnd('\u0001').ToUpperInvariant() + (g.EndsWith("\u0001") ? " (SIGUE)" : ""), ColorGrupo(g.TrimEnd('\u0001')), fTit);
                List<int> filas = porGrupo[g];
                float yy = y + cab;
                if (g == "Tu biblioteca" && bib != null)
                {
                    for (int b = 0; b < bib.Count; b++) (b == 0 ? fNom : fChica).Escribe(d, (b == 0 ? fNom : fChica).Ajusta(bib[b], colW - 36 * k), cx + 18 * k, yy + b * 18 * k + 2 * k, b == 0 ? Color4.Hex(0xC6D6F4) : Color4.Hex(0x8A96BA));
                    yy += bib.Count * 18 * k + 4 * k;
                }
                for (int f = 0; f < filas.Count; f++)
                {
                    int i = filas[f]; bool es = i == sel; float hf = altoDe[i];
                    if (es)
                    {
                        d.Redondo(cx + 8 * k, yy - 1 * k, colW - 16 * k, hf - 2 * k, 7 * k, Color4.Hex(0x24346A, 0.95f));
                        d.Contorno(cx + 8 * k, yy - 1 * k, colW - 16 * k, hf - 2 * k, 7 * k, 1.4f * k, Color4.Hex(0x8CB4FF));
                    }
                    if (i == n - 1) { Boton(cx + 18 * k, yy + 2 * k, colW - 36 * k, fila - 6 * k, "Configurar los botones de la rocola", es, fNom); yy += hf; continue; }
                    Fila(l[i], cx + 18 * k, yy, colW - 36 * k, fila, es, fNom, fChica, fVal);
                    // la nota gris (como las de XP) y, si lleva, la barra
                    float ny = yy + fila - 4 * k; List<string> nl;
                    if (notaDe.TryGetValue(i, out nl))
                    {
                        string tv = l[i].Texto ?? ""; float sangria = tv == "SÍ" || tv == "NO" ? 50 * k : 0;
                        foreach (string r in nl) { fChica.Escribe(d, r, cx + 18 * k + sangria, ny, Color4.Hex(0x8A96BA)); ny += 14 * k; }
                    }
                    int? pos = l[i].Posicion;
                    if (pos.HasValue) Deslizador(cx + 22 * k, ny + 10 * k, colW - 44 * k, pos.Value / 100f, ColorGrupo(g.TrimEnd('\u0001')), l[i].Motivo != null);
                    yy += hf;
                }
            }
            d.SinRecorte();
            // una barrita que dice donde va la pagina, si no cabe entera
            if (total > vis)
            {
                float bx = wx + ww - 12 * k, bh2 = Math.Max(20 * k, vis * vis / total), bp = cy0 + (vis - bh2) * (ajusteCorre / (total - vis));
                d.Redondo(bx, cy0, 4 * k, vis, 2 * k, Color4.Hex(0x1E2648));
                d.Redondo(bx, bp, 4 * k, bh2, 2 * k, Color4.Hex(0x8CB4FF));
            }
        }

        // EL DESLIZADOR de XP: la pista, lo lleno del color de la tarjeta y la perilla blanca con su filo.
        private void Deslizador(float x, float cy, float w, float v, Color4 col, bool gris)
        {
            v = Math.Max(0f, Math.Min(1f, v));
            float hp = 6 * k, r = 8 * k;
            d.Redondo(x, cy - hp / 2, w, hp, hp / 2, Color4.Hex(0x252C4C));
            Color4 c = gris ? Color4.Hex(0x4A5070) : col;
            if (v > 0) d.Redondo(x, cy - hp / 2, Math.Max(hp, w * v), hp, hp / 2, c.ConAlfa(0.85f));
            float kx = x + w * v;
            Circulo(kx, cy, r + 1.5f * k, c);
            Circulo(kx, cy, r - 1 * k, gris ? Color4.Hex(0xA0A6B8) : Color4.Hex(0xF4F7FF));
        }

        // Una tarjeta de grupo (los recuadros de la ventana de XP): fondo, filo, la rayita de color y el titulo.
        private void Tarjeta(float x, float y, float w, float h, string titulo, Color4 col, Letra f)
        {
            d.Redondo(x, y, w, h, 10 * k, Color4.Hex(0x141B36, 0.96f));
            d.Contorno(x, y, w, h, 10 * k, 1f * k, Color4.Hex(0x2C3762));
            d.Redondo(x + 14 * k, y + 13 * k, 4 * k, 16 * k, 2 * k, col);
            f.Escribe(d, titulo, x + 26 * k, y + 21 * k - f.Tam * 0.62f, Color4.Hex(0xEEF2FF));
        }

        // Un renglon: el interruptor (SI/NO), el boton (EMPEZAR, PARAR...) o el nombre con su valor a la derecha; en gris con su motivo.
        private void Fila(Form1.Ajuste a, float x, float y, float w, float fila, bool sel, Letra fNom, Letra fChica, Letra fVal)
        {
            string mot = a.Motivo, v = a.Texto ?? "";
            bool gris = mot != null, si = v == "SÍ", no = v == "NO";
            Color4 tinta = gris ? Color4.Hex(0x6A7290) : Color4.Hex(0xF2F5FF);
            float cy = y + fila / 2 - 1 * k;
            if (si || no)
            {
                // EL INTERRUPTOR de XP: una pildora verde con la bolita a la derecha, o gris con la bolita a la izquierda
                float sw = 40 * k, sh = 20 * k, sx = x;
                Color4 fondo = si ? (gris ? Color4.Hex(0x3E6A50) : Color4.Hex(0x58D888)) : Color4.Hex(0x2A3150);
                d.Redondo(sx, cy - sh / 2, sw, sh, sh / 2, fondo);
                if (no) d.Contorno(sx, cy - sh / 2, sw, sh, sh / 2, 1f * k, Color4.Hex(0x4A5480));
                float bxx = si ? sx + sw - sh / 2 : sx + sh / 2;
                Circulo(bxx, cy, sh / 2 - 3 * k, gris ? Color4.Hex(0xA0A6B8) : Blanco);
                float nx = sx + sw + 10 * k, nw = x + w - nx - (gris ? 0 : 0);
                string nom = fNom.Ajusta(a.Nombre, nw);
                fNom.Escribe(d, nom, nx, cy - fNom.Tam * 0.62f, tinta);
                if (gris && fNom.Mide(nom) + fChica.Mide(mot) + 16 * k < nw) fChica.Escribe(d, "(" + mot + ")", nx + fNom.Mide(nom) + 10 * k, cy - fChica.Tam * 0.62f, Color4.Hex(0x7A82A0));
                return;
            }
            bool boton = a.Herramienta && (v == "EMPEZAR" || v == "PARAR" || v.StartsWith("BUSCAR") || v.StartsWith("CORRER") || v.StartsWith("TOCAR") || v.StartsWith("PONER") || v == "");
            if (boton)
            {
                string et = a.Nombre + (v.Length > 0 && v != "EMPEZAR" ? "  ·  " + v : "");
                float bw = Math.Min(w, fNom.Mide(et) + 40 * k);
                Boton(x, y + 2 * k, bw, fila - 6 * k, et, sel, fNom, gris);
                return;
            }
            // el nombre a la izquierda y el valor a la derecha, en azul (como «Al limite» de XP); con flechitas si esta elegido
            float vw = fVal.Mide(v), maxV = w * 0.45f;
            string vv = vw > maxV ? fVal.Ajusta(v, maxV) : v; vw = fVal.Mide(vv);
            fNom.Escribe(d, fNom.Ajusta(a.Nombre, w - vw - 30 * k), x, cy - fNom.Tam * 0.62f, tinta);
            Color4 cv = gris ? Color4.Hex(0x6A7290) : Color4.Hex(0x6FB4FF);
            fVal.Escribe(d, vv, x + w - vw, cy - fVal.Tam * 0.62f, cv);
            if (sel && !gris)
            {
                float q = 5 * k, ax = x + w - vw - 14 * k;
                d.Triangulo(ax + q, cy - q, ax + q, cy + q, ax, cy, cv);
            }
        }

        // Un boton de la ventana de XP: rectangulo azul oscuro con filo, el texto centrado en negrita.
        private void Boton(float x, float y, float w, float h, string texto, bool sel, Letra f, bool gris = false)
        {
            d.Redondo(x, y, w, h, 7 * k, sel ? Color4.Hex(0x2E4C9A) : Color4.Hex(0x1C2850));
            d.Contorno(x, y, w, h, 7 * k, sel ? 1.6f * k : 1f * k, sel ? Color4.Hex(0x9CC4FF) : Color4.Hex(0x4A5C98));
            f.EscribeCentrado(d, f.Ajusta(texto, w - 16 * k), x + w / 2, y + h / 2 - f.Tam * 0.62f, gris ? Color4.Hex(0x6A7290) : Blanco);
        }
    }
}
