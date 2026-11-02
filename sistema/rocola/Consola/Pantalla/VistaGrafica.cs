// LA ROCOLA EN LA PANTALLA (Rocola OS, etapa 2, primera version grafica): por KMS y OpenGL ES 2, sin escritorio. Arriba los creditos y
// las letras; en medio la pared de discos (8 caratulas por pagina, la elegida resaltada) o, dentro de un disco, su caratula y sus
// canciones; abajo lo que suena, con su caratula y su avance. Esta escalada a la pantalla (pensada para 1024x768, la del A4). Es la
// primera: funcional y clara; el cielo, los cubos y lo demas de la de XP vienen despues, uno a uno.
using System;
using System.Collections.Generic;
using System.IO;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica : IVista
    {
        private readonly ISuperficie sup;
        private readonly Dibujo d;
        private readonly Letra chica, media, grande, enorme;
        private readonly Letra micro, mini, nChica, nMedia, nGrande, nEnorme, grandeNegrita, nMediaFina, tChica, tMedia, tGrande, tTexto;   // las de la pared al estilo de XP (las negritas, de la letra en negrita)
        private readonly Caratulas caratulas;
        private readonly float k;   // escala frente a 768 de alto
        private string antes;
        private int ultimo;
        public bool LeeTerminal { get { return false; } }
        public bool Anima { get { return true; } }
        private readonly System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        private readonly float[] estrellas;   // x, y (0..1), tamano, fase, velocidad: el cielo de siempre (fijo, como el patron de XP)
        private float pGraves, pMedios, pAgudos;   // el pulso de la musica, suavizado (sube rapido, baja despacio)
        private int focoDisco = -1; private string focoCarpeta; private double focoDesde;
        public string Descripcion { get { return sup.Descripcion; } }

        // Fondo y colores (noche azul con dorado, como la de XP).
        private static readonly Color4 Fondo = Color4.Hex(0x0A1230), Panel = Color4.Hex(0x16224A), PanelClaro = Color4.Hex(0x22336A),
            Dorado = Color4.Hex(0xFFCD5A), Blanco = new Color4(1, 1, 1), Gris = Color4.Hex(0xA8B4D8), Verde = Color4.Hex(0x78F08C);

        internal VistaGrafica(ISuperficie superficie, string letraTtf)
        {
            sup = superficie;
            d = new Dibujo(sup.Ancho, sup.Alto);
            k = sup.Alto / 768f;
            byte[] ttf = File.ReadAllBytes(letraTtf);
            chica = new Letra(d, ttf, 18 * k); media = new Letra(d, ttf, 24 * k); grande = new Letra(d, ttf, 32 * k); enorme = new Letra(d, ttf, 44 * k);
            // LAS LETRAS DE LA ROCOLA DE XP: Microsoft Sans Serif (la de Windows; GDI+ la engorda para la negrita) en casi todo y Trebuchet MS
            // en la ayuda, la atraccion y el reposo. Rocola OS las copia del disco de XP (rocola-discos); si no estan, Arial o DejaVu.
            bool sint;
            byte[] ui = Letras.Lee(Letras.Ui, out sint) ?? ttf, uiN = Letras.Lee(Letras.UiNegrita, out sint) ?? ui;
            bool uiSint = sint; uiNegrita = uiN; uiNegritaSint = sint; uiNormal = ui;
            byte[] tr = Letras.Lee(Letras.Trebuchet, out sint) ?? ui, trN = Letras.Lee(Letras.TrebuchetNegrita, out sint) ?? uiN;
            bool trSint = sint && trN == uiN && uiSint; treNormal = tr; treNegrita = trN; treSint = trSint;
            micro = new Letra(d, ui, 10.5f * k, false, true); mini = new Letra(d, uiN, 13.3f * k, uiSint, true);
            nChica = new Letra(d, uiN, 14.5f * k, uiSint, true); nMedia = new Letra(d, uiN, 18.5f * k, uiSint, true); nGrande = new Letra(d, uiN, 25 * k, uiSint, true); nEnorme = new Letra(d, uiN, 28 * k, uiSint, true);
            grandeNegrita = new Letra(d, trN, 36 * k, trSint, true); nMediaFina = new Letra(d, tr, 19 * k, false, true);
            tChica = new Letra(d, trN, 15 * k, trSint, true); tMedia = new Letra(d, trN, 20 * k, trSint, true); tGrande = new Letra(d, trN, 27 * k, trSint, true); tTexto = new Letra(d, tr, 17 * k, false, true);
            caratulas = new Caratulas(d, (int)(256 * k));
            Random r = new Random(5); estrellas = new float[260 * 5];
            for (int i = 0; i < 260; i++)
            {
                estrellas[5 * i] = (float)r.NextDouble(); estrellas[5 * i + 1] = (float)Math.Pow(r.NextDouble(), 1.3) * 0.85f;
                estrellas[5 * i + 2] = (float)(0.6 + 2.2 * Math.Pow(r.NextDouble(), 3)); estrellas[5 * i + 3] = (float)(r.NextDouble() * 6.283);
                estrellas[5 * i + 4] = (float)(0.6 + r.NextDouble() * 2.2);
            }
        }

        // EL CIELO LEGACY (el fondo de XP, en lo esencial): la noche en degradado, estrellas que titilan y la aurora que ondula sobre el
        // horizonte. Se mueve despacio: no distrae de las portadas.
        private void Pulso(Controlador c)
        {
            float g = 0, m = 0, a = 0; long oido = c.CuadrosOidos;
            if (oido >= 0) c.motor.Pulso(oido, out g, out m, out a);
            pGraves = g > pGraves ? pGraves + (g - pGraves) * 0.6f : pGraves * 0.90f;
            pMedios = m > pMedios ? pMedios + (m - pMedios) * 0.5f : pMedios * 0.93f;
            pAgudos = a > pAgudos ? pAgudos + (a - pAgudos) * 0.6f : pAgudos * 0.88f;
        }
        // La letra: la de ROCOLA_LETRA, si no la DejaVu Sans del sistema (Rocola OS la lleva), o la primera que se encuentre.
        internal static string BuscaLetra()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_LETRA");
            if (!string.IsNullOrEmpty(e) && File.Exists(e)) return e;
            foreach (string r in new[] { "/usr/share/fonts/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/rocola/letra.ttf" })
                if (File.Exists(r)) return r;
            foreach (string dir in new[] { "/usr/share/fonts", "/usr/local/share/fonts" })
                try { foreach (string f in Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories)) return f; } catch { }
            throw new FileNotFoundException("no hay ninguna letra .ttf (ROCOLA_LETRA)");
        }

        public void Dibuja(Controlador c)
        {
            Form1 m = c.motor;
            // Se redibuja si algo cambio, y dos veces por segundo mientras suena (el avance).
            string firma = (c.AjusteSel >= 0 ? "A" + c.AjusteSel + "|" + FirmaAjustes(c) + "|" + c.RecuadroTitulo + c.LoQueSuena : "") + (c.Buscando ? "B" + c.Escrito + "|" + c.FilaTecla + "," + c.ColTecla + "," + c.EnResultados + "," + c.SelResultado + "|" + c.Resultados.Count + (c.Resultados.Count > 0 ? c.Resultados[0].Tipo.ToString() : "") : "") + c.AsistentePaso + "|" + c.AsistenteAviso + "|" + c.letra + "|" + c.cursorDisco + "|" + c.cursorCancion + "|" + (c.dentro != null ? c.dentro.Carpeta : "") + "|" + m.Creditos + "|" + m.EnCola + "|" + m.Sonando + "|" + c.AvisoVigente;
            int ahora = Environment.TickCount;
            if (firma == antes && unchecked(ahora - ultimo) < 33) return;   // el cielo se mueve: unos 30 cuadros por segundo
            antes = firma; ultimo = ahora;
            float W = sup.Ancho, H = sup.Alto;
            d.Empieza(Fondo);
            Pulso(c);
            ColorDelCielo(c);
            Cielo(W, H, reloj.Elapsed.TotalSeconds);
            if (reposoAlfa <= 0f) EstrellaDelCielo(c, W, H, 0f);   // el astro principal de XP, detras de todo (en el reposo, tras el velo)
            if (!(c.Atraccion && c.AtraccionActual != null) && !c.Buscando) Cabecera(c, W);   // en la atraccion y en el buscador, como en XP, sin cabecera
            float abajo = Horizonte(H);   // donde empieza lo que suena (bajo la linea de luz, como en XP)
            if (c.AsistentePaso >= 0) { Asistente(c, W, H); d.Termina(); sup.Presenta(); Fluidez.Cuadro(); return; }
            if (c.Atraccion && c.AtraccionActual != null) { Atraccion(c, W, H); if (c.DiagEscena >= 0) chica.EscribeCentrado(d, c.DiagTexto, W / 2, H - 40 * k, Blanco); d.Termina(); sup.Presenta(); Fluidez.Cuadro(); return; }
            atraccionAntes = false;
            if (c.AjusteSel >= 0) { Ajustes(c, W, H); AvisoEncima(c, W * 1.45f, 130 * k); d.Termina(); sup.Presenta(); Fluidez.Cuadro(); return; }
            // EL REPOSO (muro_reposo= y estado_expandido= de XP): la pared se aparta (el cielo vuelve encima) y lo que suena crece.
            double tr = reloj.Elapsed.TotalSeconds, dtr = Math.Max(0, Math.Min(0.1, tr - reposoT)); reposoT = tr;
            reposoAlfa = Math.Max(0f, Math.Min(1f, reposoAlfa + (float)(c.Reposo ? dtr / 0.6 : -dtr / 0.35)));
            float e = reposoAlfa * reposoAlfa * (3 - 2 * reposoAlfa);
            if (c.Buscando && c.ConPortadas) BuscadorPortadas(c, W, abajo);
            else if (c.Buscando) BuscadorPortadas(c, W, abajo);
            else if (e < 1f) { if (c.dentro == null) Pared(c, W, abajo); else Disco(c, W, abajo); }
            if (e > 0f && e < 1f) Cielo(W, H, reloj.Elapsed.TotalSeconds, e);
            if (e > 0f) EstrellaDelCielo(c, W, H, e);
            if (e > 0f && m.EstadoExpandido)
            {
                float ph = Math.Min(H, Math.Max(220 * k, Math.Min(340 * k, H * 0.32f)));
                float top = abajo - (ph - (H - abajo)) * e;
                if (e < 0.5f) Suena(c, W, H, top); else Extendido(c, W, H, top);
                abajo = Math.Min(abajo, top - 40 * k);   // el aviso, encima del panel
            }
            else Suena(c, W, H, abajo);
            string av = c.AvisoVigente;
            if (av.Length > 0 && m.AvisosClaros) AvisoClaro(c, av, W);
            else if (av.Length > 0)
            {
                float wa = media.Mide(av) + 48 * k;
                d.Redondo((W - wa) / 2, abajo - 104 * k, wa, 52 * k, 26 * k, Color4.Hex(0x101830, 0.92f));
                media.EscribeCentrado(d, av, W / 2, abajo - 92 * k, Dorado);
            }
            Efectos(c, W, H);
            Ayuda(c, W, H);
            if (c.DiagEscena >= 0) { string dt = c.DiagTexto; float wd = chica.Mide(dt) + 30 * k; d.Redondo((W - wd) / 2, H - 150 * k, wd, 32 * k, 16 * k, Color4.Hex(0x7A1E1E, 0.95f)); chica.EscribeCentrado(d, dt, W / 2, H - 145 * k, Blanco); }
            d.Termina();
            sup.Presenta(); Fluidez.Cuadro();
        }

        // EL MODO ATRACCION (el escaparate de XP): la portada grande, que se mece un poco, y su ficha; debajo, un mensaje cada 5 s.
        private int atraccionVista = -1; private double atraccionDesde;
        private bool atraccionAntes; private double atraccionInicio, topDesde = -1; private List<Form1.FilaNoche> topLista; private int topTotal;
        private void Atraccion(Controlador c, float W, float H)
        {
            Form1 m = c.motor;
            double t = reloj.Elapsed.TotalSeconds;
            if (!atraccionAntes) { atraccionAntes = true; atraccionInicio = t - c.AtraccionDesfase; topDesde = -1; }
            // LO MAS PEDIDO ESTA NOCHE (top_noche=): cada 30 s, durante 15, la lista de las cinco mas pedidas desde las 8 de la manana
            double fase = (t - atraccionInicio) % 30.0;
            if (m.TopNocheOn && fase >= 15.0)
            {
                if (topDesde < 0) { topLista = m.TopNoche(out topTotal); topDesde = t; }
                if (topLista != null && topLista.Count > 0) { TopNoche(c, W, H, t - topDesde, fase); return; }
            }
            else topDesde = -1;
            if (atraccionVista != c.AtraccionDisco) { atraccionVista = c.AtraccionDisco; atraccionDesde = t; }
            double edad = t - atraccionDesde;
            float alfa = (float)Math.Max(0, Math.Min(1, Math.Min(edad / 0.6, (8.0 - edad) / 0.6)));   // entra y sale despacio
            Form1.Disco disco = c.AtraccionActual;
            // CuboEscaparate de XP: un cuadrado de min(0,50 alto, 0,36 ancho) a 0,08 del ancho, un poco por encima del centro
            float lado = Math.Max(80, Math.Min(H * .50f, W * .36f)), x = (int)(W * .08f), y = (int)((H - lado) / 2f - H * .04f) + (float)Math.Sin(t * 0.7) * 6 * k;
            Textura tx = caratulas.De(disco.Portada);
            if (tx != null) d.Brillo(x + lado / 2, y + lado / 2, lado * 0.95f, tx.Media.ConAlfa(0.45f * alfa));
            d.Brillo(x + lado / 2, y + lado + 8 * k, lado * 0.6f, new Color4(0, 0, 0.05f, 0.5f * alfa));
            // EL ESCAPARATE (escaparate=): el cubo, en vez de girar, se mece (la portada se ve de frente); sin el, gira como siempre
            Cubo(disco.Portada, disco.Artista, x, y, lado, m.Escaparate ? 0.28 * Math.Sin((t - atraccionInicio) * 2 * Math.PI / 9.0) : t * 1.44);   // MeceCubo de XP: +-16 grados cada 9 s
            if (alfa < 1) d.Rect(x - 2, y - 2, lado + 4, lado + 4, Color4.Hex(0x1C2050, 1 - alfa));
            // ColumnaEscaparate y HorneaFichaVitrina de XP (Trebuchet MS en negrita, en proporcion al alto)
            float fx = (int)(x + lado + W * .06f), fw = (int)(W - W * .05f) - fx, fy = H * 0.325f;
            Letra fEt = Tre(Math.Max(11f, H * .019f), true), fTit = Tre(Math.Max(24f, H * .062f), true), fArt = Tre(Math.Max(15f, H * .033f), true), fPil = Tre(Math.Max(11f, H * .021f), true);
            if (m.Escaparate)
            {
                float yy = fy;
                Espaciado(fEt, "EN ESTA ROCOLA", fx, yy, fEt.Tam * .22f, Color4.Hex(0xB4C0E6, alfa)); yy += fEt.Tam * 1.15f + H * .012f;
                string nombre = string.IsNullOrEmpty(disco.Nombre) ? disco.Artista : disco.Nombre;
                Letra lt = fTit;
                for (int i = 0; i < 2 && lt.Mide(nombre) > fw * 2; i++) lt = Tre(fTit.Tam * (i == 0 ? .88f : .78f), true);
                List<string> renglones = Envuelve(nombre, lt, fw);
                if (renglones.Count > 2) { renglones[1] = lt.Ajusta(renglones[1] + " " + string.Join(" ", renglones.GetRange(2, renglones.Count - 2)), fw); renglones.RemoveRange(2, renglones.Count - 2); }
                foreach (string r in renglones) { lt.Escribe(d, r, fx, yy, Blanco.ConAlfa(alfa)); yy += lt.Tam * 1.15f; }
                yy += H * .010f;
                if (!string.IsNullOrEmpty(disco.Artista) && !string.IsNullOrEmpty(disco.Nombre)) { fArt.Escribe(d, fArt.Ajusta(disco.Artista, fw), fx, yy, VerdeTexto.ConAlfa(alfa)); yy += fArt.Tam * 1.15f + H * .020f; }
                else yy += H * .010f;
                float px = fx, hp = fPil.Tam * 1.15f + 8;
                foreach (string pastilla in new[] { disco.Pedidas > 0 ? "pedido " + disco.Pedidas + (disco.Pedidas == 1 ? " vez" : " veces") : null, disco.Rutas.Count + (disco.Rutas.Count == 1 ? " canción" : " canciones") })
                {
                    if (pastilla == null) continue;
                    bool ambar = pastilla.StartsWith("pedido");
                    Color4 col = ambar ? Color4.Hex(0xFFC668) : Color4.Hex(0x8CB4FF);
                    float w = fPil.Mide(pastilla) + hp * 0.9f;
                    d.Redondo(px, yy, w, hp, hp / 2, new Color4(0, 0, 0, 0.30f * alfa));
                    d.Contorno(px, yy, w, hp, hp / 2, 1.3f * k, col.ConAlfa(0.9f * alfa));
                    EnRenglon(fPil, pastilla, px + hp * 0.45f, yy, w, hp, col.ConAlfa(alfa));
                    px += w + hp * 0.3f;
                }
            }
            // MensajesAtraccion y FaseMensaje de XP: uno cada 5 s, con 0,4 s de fundido; entra desde abajo y se va hacia arriba
            int nc = c.TotalCanciones;
            List<string> mensajes = new List<string> { nc > 0 ? "¡Elige tu canción! Hay " + nc.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) + " esperándote" : "¡Elige tu canción! Toca cualquier botón",
                "Una moneda = 4 canciones. ¡Tú pones la música!" };
            if (m.TopNocheOn && topLista != null && topLista.Count > 0) mensajes.Add("La favorita de esta noche: «" + topLista[0].Titulo + "»");
            mensajes.Add("¿Buscas una? ROJO + ARRIBA y escríbela");
            mensajes.Add("¿Primera vez? ROJO + ABAJO y te guío");
            double msA = (t - atraccionInicio) * 1000.0;
            int im = (int)(msA / 5000) % mensajes.Count; double sm = msA % 5000;
            float am = 1f, dyM = 0f;
            if (sm < 400) { float e = (float)(1 - Math.Pow(1 - sm / 400, 3)); am = e; dyM = 10 * k * (1f - e); }
            else if (sm > 4600) { float e = (float)((sm - 4600) / 400); e = e * e * (3 - 2 * e); am = 1f - e; dyM = -10 * k * e; }
            float my = H * 0.66f + dyM;
            Letra fMsg = Tre(Math.Max(14f, H * .029f), true);
            fMsg.Escribe(d, fMsg.Ajusta(mensajes[im], W - fx - 24 * k), m.Escaparate ? fx : (W - fMsg.Mide(mensajes[im])) / 2, my, new Color4(1f, 226 / 255f, 176 / 255f, am));
        }
        // Un letrero chico con las letras separadas (EN ESTA ROCOLA de XP).
        private void Espaciado(Letra f, string s, float x, float y, float aire, Color4 c)
        {
            foreach (char ch in s) { string l = ch.ToString(); f.Escribe(d, l, x, y, c); x += f.Mide(l) + aire; }
        }
        private void EspaciadoChico(string s, float x, float y, Color4 c)
        {
            foreach (char ch in s) { string l = ch.ToString(); mini.Escribe(d, l, x, y, c); x += mini.Mide(l) + (ch == ' ' ? 5 * k : 3 * k); }
        }

        // La lista de la noche: el cubo chico en la esquina, la cabecera a su lado y las cinco filas, que entran una a una desde la
        // izquierda con su portada y una barra que crece con las veces que se pidio cada una.
        private void TopNoche(Controlador c, float W, float H, double edad, double fase)
        {
            float alfa = (float)Math.Max(0, Math.Min(1, Math.Min(edad / 0.5, (30.0 - fase) / 0.5)));
            float lado = Math.Max(60 * k, Math.Min(W, H) * 0.20f), cx = W - W * 0.06f - lado, cy = H * 0.05f + 64 * k;
            Form1.FilaNoche p0 = topLista[0];
            Cubo(p0.Portada, p0.Artista ?? p0.Titulo, cx, cy, lado, reloj.Elapsed.TotalSeconds * 1.44);
            float x0 = W * 0.07f, x1 = W * 0.93f;
            grande.Escribe(d, "LO MÁS PEDIDO ESTA NOCHE", x0, cy + 10 * k, Dorado.ConAlfa(alfa));
            media.Escribe(d, topTotal + (topTotal == 1 ? " canción pedida" : " canciones pedidas") + " desde las 8 de la mañana", x0, cy + 60 * k, Gris.ConAlfa(alfa));
            float fy = cy + lado + 24 * k, fh = Math.Min(H * 0.11f, (H * 0.95f - fy) / Form1.TopFilas);
            int max = 1; foreach (Form1.FilaNoche f in topLista) max = Math.Max(max, f.Veces);
            for (int i = 0; i < topLista.Count; i++)
            {
                Form1.FilaNoche f = topLista[i];
                float u = (float)Math.Max(0, Math.Min(1, (edad - 0.3 - i * 0.15) / 0.45)); u = 1 - (1 - u) * (1 - u) * (1 - u);
                if (u <= 0) continue;
                float y = fy + i * fh, dx = -(1 - u) * W * 0.4f, a = u * alfa, pl = fh - 10 * k;
                d.Redondo(x0 + dx, y, x1 - x0, fh - 6 * k, 12 * k, Panel.ConAlfa(0.85f * a));
                grande.EscribeCentrado(d, (i + 1).ToString(), x0 + dx + 30 * k, y + (fh - 6 * k - grande.Tam) / 2 - 2 * k, Dorado.ConAlfa(a));
                Portada(f.Portada, f.Artista ?? f.Titulo, x0 + dx + 60 * k, y + 2 * k, pl);
                float tx = x0 + dx + 60 * k + pl + 16 * k, bx = x0 + (x1 - x0) * 0.62f + dx, tw = bx - tx - 16 * k;
                media.Escribe(d, media.Ajusta(f.Titulo, tw), tx, y + 4 * k, Blanco.ConAlfa(a));
                if (!string.IsNullOrEmpty(f.Artista)) chica.Escribe(d, chica.Ajusta(f.Artista, tw), tx, y + 4 * k + media.Tam * 1.2f, Gris.ConAlfa(a));
                float bw = (x1 - 24 * k - bx) * 0.75f * (f.Veces / (float)max) * u;
                d.Redondo(bx, y + (fh - 6 * k) / 2 - 8 * k, Math.Max(16 * k, bw), 16 * k, 8 * k, Dorado.ConAlfa(0.8f * a));
                string v = f.Veces == 1 ? "1 vez" : f.Veces + " veces";
                chica.Escribe(d, v, bx + Math.Max(16 * k, bw) + 10 * k, y + (fh - 6 * k) / 2 - chica.Tam * 0.6f, Dorado.ConAlfa(a));
            }
        }

        private static string FirmaAjustes(Controlador c)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Form1.Ajuste a in c.ListaAjustes) sb.Append(a.Texto).Append(a.Motivo != null ? "g" : "").Append(',');
            return sb.ToString();
        }
        private void AvisoEncima(Controlador c, float W, float y)
        {
            string av = c.AvisoVigente; if (av.Length == 0) return;
            float wa = media.Mide(av) + 48 * k;
            d.Redondo((W - wa) / 2, y - 52 * k, wa, 52 * k, 26 * k, Color4.Hex(0x101830, 0.95f));
            media.EscribeCentrado(d, av, W / 2, y - 40 * k, Dorado);
        }
        // LOS AJUSTES DEL DUENO (Ctrl+F3): por grupos, el elegido resaltado, lo que decide el modo billar en gris con el motivo.
        // El asistente de botones (como el de XP): paso, el boton que toca, y el aviso si ese ya se uso.
        private void Asistente(Controlador c, float W, float H)
        {
            float y = 140 * k;
            grande.EscribeCentrado(d, "CONFIGURACIÓN DE LOS BOTONES", W / 2, y, Dorado);
            media.EscribeCentrado(d, "Paso " + (c.AsistentePaso + 1) + " de " + Controlador.PasosAsistente.Length, W / 2, y + 60 * k, Gris);
            media.EscribeCentrado(d, "Presiona en la rocola el botón para:", W / 2, y + 130 * k, Blanco);
            string paso = Controlador.PasosAsistente[c.AsistentePaso];
            float w = enorme.Mide(paso) + 60 * k;
            d.Redondo((W - w) / 2, y + 180 * k, w, 80 * k, 16 * k, PanelClaro);
            enorme.EscribeCentrado(d, paso, W / 2, y + 194 * k, Dorado);
            if (c.AsistenteAviso.Length > 0) media.EscribeCentrado(d, c.AsistenteAviso, W / 2, y + 300 * k, Color4.Hex(0xFF7A6A));
            chica.EscribeCentrado(d, "(cada botón debe ser distinto)", W / 2, H - 90 * k, Gris);
        }

        // El buscador (como el de XP): lo escrito arriba, el teclado a la izquierda y los resultados a la derecha.
        private void Buscador(Controlador c, float W, float abajo)
        {
            float x0 = 24 * k, y0 = 80 * k, tecla = 52 * k, sep = 6 * k;
            d.Redondo(x0, y0, W - 48 * k, 54 * k, 12 * k, PanelClaro);
            grande.Escribe(d, c.Escrito.Length > 0 ? c.Escrito + "_" : "Escribe un artista, un disco o una canción", x0 + 18 * k, y0 + 8 * k, c.Escrito.Length > 0 ? Blanco : Gris);
            float ty = y0 + 80 * k;
            for (int f = 0; f < Controlador.FilasTeclado.Length; f++)
            {
                string fila = Controlador.FilasTeclado[f]; float tx = x0;
                for (int i = 0; i < fila.Length; i++)
                {
                    string et = Controlador.Etiqueta(fila[i]);
                    float w = et.Length > 1 ? tecla * 3 + sep * 2 : tecla;
                    bool sel = !c.EnResultados && f == c.FilaTecla && i == c.ColTecla;
                    d.Redondo(tx, ty, w, tecla, 8 * k, sel ? Dorado : Panel);
                    (et.Length > 1 ? chica : media).EscribeCentrado(d, et, tx + w / 2, ty + (et.Length > 1 ? 14 : 10) * k, sel ? Fondo : Blanco);
                    tx += w + sep;
                }
                ty += tecla + sep;
            }
            zTeclado = new Zona(x0, y0, 10 * (tecla + sep), ty - y0);
            float rx = x0 + 10 * (tecla + sep) + 24 * k, rw = W - rx - 24 * k, ry = y0 + 80 * k, fila_ = 36 * k;
            zResultados = new Zona(rx - 8 * k, ry - 4 * k, rw + 16 * k, Math.Max(fila_, abajo - ry - 40 * k));
            int caben = Math.Max(1, (int)((abajo - ry - 40 * k) / fila_));
            if (c.Resultados.Count == 0 && c.Escrito.Length > 0) media.Escribe(d, "Nada con eso", rx, ry, Gris);
            int desde = Math.Max(0, Math.Min(c.SelResultado - caben / 2, c.Resultados.Count - caben));
            for (int i = 0; i < caben && desde + i < c.Resultados.Count; i++)
            {
                Form1.Resultado r = c.Resultados[desde + i]; float y = ry + i * fila_;
                bool sel = c.EnResultados && desde + i == c.SelResultado;
                if (sel) { d.Redondo(rx - 8 * k, y - 2 * k, rw + 16 * k, fila_ - 4 * k, 8 * k, PanelClaro); zSelResultado = new Zona(rx - 8 * k, y - 2 * k, rw + 16 * k, fila_ - 4 * k); }
                string tipo = r.Tipo == 0 ? "ARTISTA" : r.Tipo == 1 ? "DISCO" : r.Tipo == 3 ? "SERVICIO" : "CANCIÓN";
                chica.Escribe(d, tipo, rx, y + 6 * k, r.Tipo == 2 ? Verde : Dorado);
                media.Escribe(d, media.Ajusta(r.Texto, rw - 100 * k), rx + 100 * k, y + 2 * k, sel ? Blanco : Gris);
            }
            chica.Escribe(d, "PONER: escribir / elegir    REGRESAR: borrar    REGRESAR + ARRIBA: cerrar", x0, abajo - 28 * k, Gris.ConAlfa(0.8f));
        }

        // LA PARED (la de XP con su pantalla viva: la barra de letras con lupa, el cubo que gira con la portada enfocada, pasar de pagina
        // deslizando y la ficha rapida).
        private int pagVista = -1, pagLetraVista = -1, pagAnt = -1; private double pagDesde; private float pagDir = 1f;
        private const double PaginaMs = 380, ColumnaMs = 45;   // lo que tarda cada columna y el retraso entre columnas
        // Cuanto ha avanzado una columna (0 a 1, frenando al llegar); las columnas salen una tras otra en el sentido del paso.
        private float CurvaPagina(double t, int col, int cols)
        {
            int orden = pagDir > 0 ? col : cols - 1 - col;
            double u = Math.Max(0, Math.Min(1, (t - orden * ColumnaMs) / PaginaMs));
            return (float)(1 - Math.Pow(1 - u, 3));
        }

        // LA FICHA RAPIDA (ficha_disco= de XP: PantallaVivaGpu.cs): si el cursor se queda 1,1 s en un disco, al lado sale una tarjeta con sus
        // tres canciones mas pedidas y cuantas suyas van ya en la cola; se va sola a los 9 s y en cuanto el cursor se mueve.
        internal const int FichaEsperaMs = 1100, FichaEntraMs = 220, FichaDuraMs = 9000, FichaSaleMs = 400;
        internal static float CurvaFicha(int dt)
        {
            if (dt < FichaEsperaMs) return 0f;
            int t = dt - FichaEsperaMs;
            if (t < FichaEntraMs) { float q = 1f - t / (float)FichaEntraMs; return 1f - q * q * q; }
            t -= FichaEntraMs;
            if (t < FichaDuraMs) return 1f;
            t -= FichaDuraMs;
            if (t < FichaSaleMs) { float u = t / (float)FichaSaleMs; return 1f - u * u * (3f - 2f * u); }
            return 0f;
        }
        private Form1.Disco fichaDe; private Form1.Ficha fichaDatos; private int fichaCola = -1;
        private void FichaRapida(Controlador c, Form1.Disco disco, double dtMs, Zona pared, float celW, float celH)
        {
            float alfa = CurvaFicha((int)dtMs);
            if (alfa <= 0f || zElegido.Vacia) return;
            if (!object.ReferenceEquals(disco, fichaDe) || fichaCola != c.motor.EnCola) { fichaDe = disco; fichaCola = c.motor.EnCola; fichaDatos = c.motor.FichaDe(disco); }
            Form1.Ficha f = fichaDatos;
            // TamanoFicha y LugarFicha de XP: dos casillas de ancho y un poco mas de una de alto, a la derecha de la elegida si cabe
            float w = Math.Max(260 * k, Math.Min(480 * k, celW * 2 - 20 * k)), h = Math.Max(170 * k, Math.Min(300 * k, celH * 1.36f));
            Zona celda = zPortadaElegida;   // RectElegida de XP: el cuadro de la portada (no la caja que crece)
            float sep = 16 * k, fx = celda.X + celda.W + sep;
            if (fx + w > pared.X + pared.W - 6 * k) fx = celda.X - sep - w;
            if (fx < pared.X + 6 * k) fx = pared.X + 6 * k;
            float fy = celda.Y - 4 * k;
            if (fy + h > pared.Y + pared.H - 6 * k) fy = pared.Y + pared.H - 6 * k - h;
            if (fy < pared.Y + 4 * k) fy = pared.Y + 4 * k;
            fy += 14 * k * (1f - alfa);
            // PintaFicha de XP: todo sale del alto de la tarjeta (u); letras en negrita de 18, 12,5, 13, 11 y 14 u
            float u = h / 230f, pad = 16 * u;
            Letra fTit = Ui(18 * u), fDisco = Ui(12.5f * u), fSub = Ui(13 * u), fMin = Ui(11 * u), fFila = Ui(14 * u);
            d.Redondo(fx + 1, fy + 1, w - 3, h - 3, 16 * u, new Color4(12 / 255f, 18 / 255f, 42 / 255f, 232 / 255f * alfa));
            d.Contorno(fx + 1, fy + 1, w - 3, h - 3, 16 * u, 1.5f, Color4.Hex(0x96AFE6, 150 / 255f * alfa));   // el filo: el color del disco con blanco
            float lado = 64 * u;
            Textura tx = caratulas.De(disco.Portada);
            if (tx != null) d.Imagen(tx, fx + pad, fy + pad, lado, lado, alfa); else d.Redondo(fx + pad, fy + pad, lado, lado, 7 * u, Color4.Hex(0x2D5FCD, 80 / 255f * alfa));
            float x = fx + pad + lado + 12 * u, ancho = fx + w - pad - x, y = fy + pad - 2 * u;
            string artista = string.IsNullOrEmpty(disco.Artista) ? disco.Nombre : disco.Artista, nombre = string.IsNullOrEmpty(disco.Artista) ? "" : disco.Nombre;
            EnRenglon(fTit, artista ?? "", x, y, ancho, 23 * u, Blanco.ConAlfa(alfa)); y += 22 * u;
            if (!string.IsNullOrEmpty(nombre)) { EnRenglon(fDisco, nombre, x, y, ancho, 18 * u, new Color4(200 / 255f, 212 / 255f, 245 / 255f, 230 / 255f * alfa)); y += 18 * u; }
            EnRenglon(fMin, TextoFoco(disco), x, y, ancho, 17 * u, new Color4(160 / 255f, 178 / 255f, 222 / 255f, 215 / 255f * alfa)); y += 18 * u;
            if (f.EnCola > 0)
            {
                string q = f.EnCola + " en la cola"; float altoP = 19 * u, wq = fMin.Mide(q) + altoP * 0.9f;
                d.Redondo(x, y, wq, altoP, altoP / 2, new Color4(0, 0, 0, 70 / 255f * alfa));
                d.Contorno(x, y, wq, altoP, altoP / 2, 1f, Color4.Hex(0x9AECBC, 220 / 255f * alfa));
                EnRenglon(fMin, q, x + altoP * 0.45f, y, wq, altoP, Color4.Hex(0x78E6A5, alfa));
                y += altoP;
            }
            float yl = Math.Max(fy + pad + lado, y) + 9 * u;
            d.Rect(fx + pad, yl, w - 2 * pad, 1, new Color4(160 / 255f, 180 / 255f, 230 / 255f, 60 / 255f * alfa));
            EnRenglon(fMin, f.Pedidas ? "LAS MÁS PEDIDAS DE ESTE DISCO" : "ALGUNAS DE ESTE DISCO", fx + pad, yl + 3 * u, w - 2 * pad, 17 * u, new Color4(150 / 255f, 170 / 255f, 220 / 255f, 200 / 255f * alfa));
            float yb = fy + h - pad - 17 * u, rb = 8 * u, yf = yl + 21 * u, altoF = 23 * u;
            int caben = Math.Max(0, (int)((yb - 3 * u - yf) / altoF));
            Color4 colaVerde = Color4.Hex(0x78E6A5, alfa);
            for (int i = 0; i < f.Titulos.Count && i < caben; i++)
            {
                float yk = yf + i * altoF, dc = altoF - 6 * u, cxk = fx + pad + dc / 2, cyk = yk + 3 * u + dc / 2;
                Anillo(cxk, cyk, dc / 2, 1.2f, colaVerde.ConAlfa(200 / 255f * alfa));
                fMin.EscribeCentrado(d, (i + 1).ToString(), cxk, cyk - fMin.Tam * 0.56f, colaVerde);
                float xt = fx + pad + dc + 9 * u, der = fx + w - pad;
                if (f.Pedidas && f.Veces[i] > 0) { string vt = f.Veces[i] == 1 ? "1 vez" : f.Veces[i] + " veces"; float vw = fMin.Mide(vt); EnRenglon(fMin, vt, der - vw, yk, vw + 2, altoF, Color4.Hex(0xFFC668, alfa)); der -= vw + 8 * u; }
                string du = f.Duraciones[i];
                if (du.Length > 0) { float dw = fMin.Mide(du); if (der - dw - 8 * u - xt >= (w - 2 * pad) * .5f) { EnRenglon(fMin, du, der - dw, yk, dw + 2, altoF, new Color4(160 / 255f, 176 / 255f, 218 / 255f, 200 / 255f * alfa)); der -= dw + 8 * u; } }
                EnRenglon(fFila, f.Titulos[i], xt, yk, Math.Max(10, der - xt), altoF, Blanco.ConAlfa(alfa));
            }
            Circulo(fx + pad + rb, yb + 1 * u + rb, rb, Color4.Hex(0x48D470, alfa));
            EnRenglon(fSub, disco.Rutas.Count > 0 ? "Entrar para ver las " + disco.Rutas.Count : "Entrar al disco", fx + pad + rb * 2 + 8 * u, yb - 1 * u, w - 2 * pad, 20 * u, new Color4(223 / 255f, 230 / 255f, 1f, 230 / 255f * alfa));
        }
        // Un renglon como el DrawString de XP con LineAlignment.Center, NoWrap y puntos al final: centrado en su alto y cortado a su ancho.
        private void EnRenglon(Letra l, string texto, float x, float y, float ancho, float alto, Color4 c)
        {
            l.Escribe(d, l.Ajusta(texto ?? "", ancho), x, y + (alto - l.Tam * 1.15f) / 2, c);
        }
        // El letrero de un disco (TextoFoco de XP): "12 canciones · 38 min"; los minutos solo si se sabe lo que dura cada cancion.
        internal static string TextoFoco(Form1.Disco disco)
        {
            int n = disco.Rutas.Count;
            if (disco.Videos) return n + (n == 1 ? " video" : " videos");
            string s = n + (n == 1 ? " canción" : " canciones");
            int min = disco.Minutos;
            if (min < 0 || n == 0) return s;
            if (min < 1) return s + " · menos de 1 min";
            if (min < 60) return s + " · " + min + " min";
            return s + " · " + (min / 60) + " h " + (min % 60).ToString("00") + " min";
        }
        // Letras de la interfaz (la negrita de XP) a cualquier tamano, hechas una vez por tamano.
        private readonly Dictionary<int, Letra> letrasUi = new Dictionary<int, Letra>();
        private byte[] uiNegrita, uiNormal, treNormal, treNegrita; private bool uiNegritaSint, treSint;
        private readonly Dictionary<int, Letra> letrasTre = new Dictionary<int, Letra>();
        // Trebuchet MS (la letra del asistente, la atraccion y el reposo de XP) a cualquier tamano, una vez por tamano.
        private Letra Tre(float px, bool negrita)
        {
            int clave = (int)Math.Round(px * 4) * 2 + (negrita ? 1 : 0);
            Letra l;
            if (!letrasTre.TryGetValue(clave, out l)) { l = new Letra(d, negrita ? treNegrita : treNormal, (clave / 2) / 4f, negrita && treSint, true); letrasTre[clave] = l; }
            return l;
        }
        private readonly Dictionary<int, Letra> letrasUiFina = new Dictionary<int, Letra>();
        private Letra UiFina(float px)
        {
            int clave = (int)Math.Round(px * 4);
            Letra l;
            if (!letrasUiFina.TryGetValue(clave, out l)) { l = new Letra(d, uiNormal, clave / 4f, false, true); letrasUiFina[clave] = l; }
            return l;
        }
        private Letra Ui(float px)
        {
            int clave = (int)Math.Round(px * 4);
            Letra l;
            if (!letrasUi.TryGetValue(clave, out l)) { l = new Letra(d, uiNegrita, clave / 4f, uiNegritaSint, true); letrasUi[clave] = l; }
            return l;
        }

        private float reposoAlfa; private double reposoT;

        // EL COLOR DEL GENERO (color_genero= de XP): un resplandor de ese color que respira despacio (con su propio reloj de 5 s, no con la
        // musica: nada da saltos) detras de la portada.
        private void Resplandor(uint rgb, float cx, float cy, float radio)
        {
            if (rgb == 0) return;
            float a = 0.80f + 0.20f * (float)Math.Sin(reloj.Elapsed.TotalSeconds * 2 * Math.PI / 5.0);
            d.Brillo(cx, cy, radio * 1.6f, Color4.Hex(rgb, a));
            d.Brillo(cx, cy, radio * 1.25f, Color4.Hex(rgb, 0.8f * a));
        }
        // EL CUBO QUE GIRA (Cube.Draw de XP y su gemelo en la tarjeta: D3D9Star.cs, PreparaCubo): la portada en las cuatro caras de un prisma
        // que gira sobre su eje vertical, visto de frente; cada cara visible se oscurece con la profundidad (hasta 130 de 255). Gira a 0,06 rad
        // por paso de 24 por segundo (1,44 rad/s), en el sentido en que se movio la seleccion.
        private int relevoIdx = -1, relevoLetra = -1; private double relevoAng; private float relevoGiro, giroDir = 1f; private double relevoT;
        private double AnguloCubo() { return (reloj.Elapsed.TotalMilliseconds - focoDesde) / 1000.0 * 1.44 * giroDir; }
        private double PasoRelevo()
        {
            double t = reloj.Elapsed.TotalSeconds, pasos = Math.Max(0, Math.Min(0.25, t - relevoT)) * 24; relevoT = t;
            for (int i = 0; i < (int)Math.Round(pasos); i++)
            {
                if (Math.Abs(relevoGiro) > 0.02f) { relevoAng += relevoGiro; relevoGiro *= 0.85f; }
                else
                {
                    double meta = Math.Round(relevoAng / (2 * Math.PI)) * (2 * Math.PI);   // se asienta plano, sin salto
                    relevoAng += (meta - relevoAng) * 0.25;
                    if (Math.Abs(meta - relevoAng) < 0.03) { relevoIdx = -1; relevoAng = meta; break; }
                }
            }
            return relevoAng;
        }
        private void Cubo(string ruta, string nombre, float x, float y, float lado, double angulo)
        {
            Textura t = caratulas.De(ruta);
            Color4 base_ = Color4.Hex(0x2A3A78);
            if (t == null)
            {
                uint h32 = 2166136261; foreach (char ch in nombre ?? "") h32 = (h32 ^ ch) * 16777619;
                base_ = new Color4(0.25f + (h32 & 255) / 640f, 0.2f + ((h32 >> 8) & 255) / 640f, 0.35f + ((h32 >> 16) & 255) / 640f);
            }
            double cx = x + lado / 2.0, R = lado / 2.0, g45 = Math.PI / 4, g90 = Math.PI / 2;
            double[] bx = new double[4], bz = new double[4]; int[] orden = { 0, 1, 2, 3 };
            for (int i = 0; i < 4; i++) { bx[i] = cx + R * Math.Cos(angulo + g45 + i * g90); bz[i] = Math.Sin(angulo + g90 + i * g90); }
            Array.Sort(orden, (a, b) => bz[a].CompareTo(bz[b]));   // de atras hacia adelante
            bool alguna = false;
            foreach (int i in orden)
            {
                if (bz[i] <= 0.0) continue;   // cara de atras: no se ve
                float iz = (float)Math.Round(Math.Min(bx[i], bx[(i + 1) % 4])), de = (float)Math.Round(Math.Max(bx[i], bx[(i + 1) % 4]));
                if (de - iz < 1f) continue;
                float g = 1f - (float)Math.Max(0, Math.Min(1, (1.0 - bz[i]) * 130 / 255.0));
                if (t != null) d.Quad(t, iz, y, de - iz, lado, 0, 0, 1, 1, new Color4(g, g, g, 1f));
                else d.Rect(iz, y, de - iz, lado, new Color4(base_.R * g, base_.G * g, base_.B * g, 1f));
                alguna = true;
            }
            if (!alguna) return;
            if (t == null && Math.Abs(Math.Sin(angulo % g90 * 2)) < 0.25)   // casi de frente: su inicial, como la portada sin imagen
            {
                string ini = string.IsNullOrEmpty(nombre) ? "?" : nombre.Substring(0, 1).ToUpperInvariant();
                Letra l = lado > 120 * k ? enorme : grande;
                l.EscribeCentrado(d, ini, x + lado / 2, y + lado / 2 - l.Tam * 0.55f, new Color4(1, 1, 1, 0.85f));
            }
        }

        // La caratula, o un cuadro de color con la inicial si no tiene.
        private void Portada(string ruta, string nombre, float x, float y, float lado)
        {
            Textura t = caratulas.De(ruta);
            if (t != null)
            {
                float a = t.Ancho / (float)t.Alto, w = lado, h = lado;
                if (a > 1) h = lado / a; else w = lado * a;
                d.Rect(x, y, lado, lado, Color4.Hex(0x000000, 0.5f));
                d.Imagen(t, x + (lado - w) / 2, y + (lado - h) / 2, w, h);
                return;
            }
            uint h32 = 2166136261; foreach (char ch in nombre ?? "") h32 = (h32 ^ ch) * 16777619;
            Color4 col = new Color4(0.25f + (h32 & 255) / 640f, 0.2f + ((h32 >> 8) & 255) / 640f, 0.35f + ((h32 >> 16) & 255) / 640f);
            d.Redondo(x, y, lado, lado, 10 * k, col);
            string ini = string.IsNullOrEmpty(nombre) ? "?" : nombre.Substring(0, 1).ToUpperInvariant();
            Letra l = lado > 120 * k ? enorme : grande;
            l.EscribeCentrado(d, ini, x + lado / 2, y + lado / 2 - l.Tam * 0.55f, new Color4(1, 1, 1, 0.85f));
        }

        public void Suelta() { sup.Suelta(); }
        public void Recupera() { sup.Recupera(); antes = null; }
        public void Dispose() { sup.Dispose(); }
    }
}
