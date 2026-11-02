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
        private readonly Letra micro, mini, nChica, nMedia, nGrande, nEnorme, grandeNegrita, nMediaFina;   // las de la pared al estilo de XP (las negritas, de la letra en negrita)
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
            string rutaNegrita = Negrita(letraTtf);
            byte[] neg = rutaNegrita != null ? File.ReadAllBytes(rutaNegrita) : ttf;
            micro = new Letra(d, ttf, 10.5f * k); mini = new Letra(d, ttf, 14 * k); nChica = new Letra(d, neg, 15 * k); nMedia = new Letra(d, neg, 19 * k); nGrande = new Letra(d, neg, 26 * k); nEnorme = new Letra(d, neg, 30 * k); grandeNegrita = new Letra(d, neg, 40 * k); nMediaFina = new Letra(d, ttf, 19 * k);
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
            float lado = H * 0.46f, x = W * 0.105f, y = H * 0.22f + (float)Math.Sin(t * 0.7) * 6 * k;
            Textura tx = caratulas.De(disco.Portada);
            if (tx != null) d.Brillo(x + lado / 2, y + lado / 2, lado * 0.95f, tx.Media.ConAlfa(0.45f * alfa));
            d.Brillo(x + lado / 2, y + lado + 8 * k, lado * 0.6f, new Color4(0, 0, 0.05f, 0.5f * alfa));
            // EL ESCAPARATE (escaparate=): el cubo, en vez de girar, se mece (la portada se ve de frente); sin el, gira como siempre
            Cubo(disco.Portada, disco.Artista, x, y, lado, m.Escaparate ? 0.22 * Math.Sin(t * 1.1) : t * 1.44);
            if (alfa < 1) d.Rect(x - 2, y - 2, lado + 4, lado + 4, Color4.Hex(0x1C2050, 1 - alfa));
            float fx = W * 0.498f, fw = W - fx - 40 * k, fy = H * 0.32f;
            if (m.Escaparate)
            {
                EspaciadoChico("EN ESTA ROCOLA", fx, fy, Color4.Hex(0xC8D0EE, alfa));
                string nombre = string.IsNullOrEmpty(disco.Nombre) ? disco.Artista : disco.Nombre;
                Letra lt = nEnorme.Mide(nombre) * 1.3f < fw ? grandeNegrita : nEnorme;
                lt.Escribe(d, lt.Ajusta(nombre, fw), fx, fy + 26 * k, Blanco.ConAlfa(alfa));
                nMedia.Escribe(d, nMedia.Ajusta(disco.Artista, fw), fx, fy + 26 * k + lt.Tam * 1.35f, VerdeTexto.ConAlfa(alfa));
                float px = fx, py = fy + 26 * k + lt.Tam * 1.35f + 42 * k;
                foreach (string pastilla in new[] { disco.Rutas.Count + (disco.Rutas.Count == 1 ? " canción" : " canciones"), disco.Pedidas > 0 ? "pedido " + disco.Pedidas + (disco.Pedidas == 1 ? " vez" : " veces") : null })
                {
                    if (pastilla == null) continue;
                    float w = nChica.Mide(pastilla) + 28 * k, hp = 28 * k;
                    d.Redondo(px, py, w, hp, hp / 2, Color4.Hex(0x16224E, 0.75f * alfa));
                    d.Contorno(px, py, w, hp, hp / 2, 1.3f * k, AzulBorde.ConAlfa(0.8f * alfa));
                    nChica.Escribe(d, pastilla, px + 14 * k, py + hp / 2 - nChica.Tam * 0.6f, Color4.Hex(0xB8D4FF, alfa));
                    px += w + 10 * k;
                }
            }
            List<string> mensajes = new List<string> { "Toca un botón y elige entre " + c.TotalCanciones.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " canciones",
                "Pon tu canción: una moneda = 4 canciones" };
            if (m.TopNocheOn && topLista != null && topLista.Count > 0) mensajes.Add("Lo más pedido esta noche: «" + topLista[0].Titulo + "»");
            mensajes.Add("¿Buscas una? ROJO + ARRIBA y escríbela");
            int im = (int)(t / 5.0) % mensajes.Count; double em = t % 5.0;
            float am = (float)Math.Max(0, Math.Min(1, Math.Min(em / 0.5, (5.0 - em) / 0.5)));
            float my = H * 0.66f + (1 - am) * 14 * k;
            nMedia.Escribe(d, nMedia.Ajusta(mensajes[im], W - fx - 24 * k), m.Escaparate ? fx : (W - nMedia.Mide(mensajes[im])) / 2, my, Color4.Hex(0xFFD98A, am));
        }
        // Un letrero chico con las letras separadas (EN ESTA ROCOLA de XP).
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
        private void Ajustes(Controlador c, float W, float H)
        {
            grande.Escribe(d, "AJUSTES DEL SONIDO", 24 * k, 76 * k, Dorado);
            chica.Escribe(d, "ARRIBA/ABAJO: elegir    IZQ/DER o PONER: cambiar    REGRESAR: guardar y salir", 24 * k, 118 * k, Gris);
            IList<Form1.Ajuste> l = c.ListaAjustes;
            float top = 150 * k, fila = 30 * k;
            // abajo, lo que la cadena le hace a la que suena (S5 de XP), con su recuadro
            string[] lq = (c.LoQueSuena ?? "").Split('\n');
            float lin = 22 * k, alto = 34 * k + lq.Length * lin;
            float py = H - alto - 12 * k;
            d.Redondo(16 * k, py, W - 32 * k, alto, 12 * k, Panel.ConAlfa(0.9f));
            chica.Escribe(d, c.RecuadroTitulo, 32 * k, py + 8 * k, Dorado);
            for (int j = 0; j < lq.Length; j++) chica.Escribe(d, chica.Ajusta(lq[j], W - 64 * k), 32 * k, py + 32 * k + j * lin, j == 0 ? Blanco : Gris);
            int caben = Math.Max(4, (int)((py - top - 8 * k) / fila)), n = c.AjustesTotal;
            int desde = Math.Max(0, Math.Min(c.AjusteSel - caben / 2, n - caben));
            string grupo = null;
            for (int i = desde, f = 0; i < n && f < caben; i++, f++)
            {
                float y = top + f * fila; bool sel = i == c.AjusteSel;
                if (sel) d.Redondo(16 * k, y - 2 * k, W - 32 * k, fila - 2 * k, 8 * k, PanelClaro);
                if (i == n - 1) { media.Escribe(d, "Configurar los botones de la rocola  (PONER)", 32 * k, y, sel ? Blanco : Gris); continue; }
                Form1.Ajuste a = l[i];
                if (a.Grupo != grupo) { grupo = a.Grupo; }
                string mot = a.Motivo;
                chica.Escribe(d, a.Grupo, 32 * k, y + 5 * k, Dorado.ConAlfa(0.7f));
                media.Escribe(d, media.Ajusta(a.Nombre, W * 0.5f), 240 * k, y, mot != null ? Gris.ConAlfa(0.5f) : sel ? Blanco : Gris);
                string v = mot != null ? a.Texto + "  (" + mot + ")" : a.Texto;
                Color4 cv = mot != null ? Gris.ConAlfa(0.5f) : a.Texto.StartsWith("SÍ") ? Verde : a.Texto == "NO" ? Color4.Hex(0xFF8A7A) : Dorado;
                media.Escribe(d, media.Ajusta(v, W - 240 * k - W * 0.5f - 40 * k), 240 * k + W * 0.5f + 16 * k, y, cv);
            }
        }

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
            float w = Math.Max(260 * k, Math.Min(480 * k, celW * 2 - 20 * k)), h = Math.Max(170 * k, Math.Min(300 * k, celH * 1.36f));
            // a la derecha de la casilla si cabe; si no, a la izquierda; nunca fuera de la pared (LugarFicha de XP)
            float sep = 16 * k, fx = zElegido.X + zElegido.W + sep;
            if (fx + w > pared.X + pared.W - 6 * k) fx = zElegido.X - sep - w;
            if (fx < pared.X + 6 * k) fx = pared.X + 6 * k;
            float fy = zElegido.Y - 4 * k;
            if (fy + h > pared.Y + pared.H - 6 * k) fy = pared.Y + pared.H - 6 * k - h;
            if (fy < pared.Y + 4 * k) fy = pared.Y + 4 * k;
            fy += 14 * k * (1f - alfa);
            float u = h / 230f, pad = 16 * u;
            d.Redondo(fx - 1.5f * k, fy - 1.5f * k, w + 3 * k, h + 3 * k, 17 * u, Dorado.ConAlfa(0.45f * alfa));
            d.Redondo(fx, fy, w, h, 16 * u, Color4.Hex(0x0C122A, 0.93f * alfa));
            float lado = 64 * u;
            Textura tx = caratulas.De(disco.Portada);
            if (tx != null) d.Imagen(tx, fx + pad, fy + pad, lado, lado, alfa); else d.Redondo(fx + pad, fy + pad, lado, lado, 7 * u, Dorado.ConAlfa(0.3f * alfa));
            float tx0 = fx + pad + lado + 12 * u, ancho = w - (tx0 - fx) - pad, ty = fy + pad - 2 * u;
            nMedia.Escribe(d, nMedia.Ajusta(disco.Artista, ancho), tx0, ty, Blanco.ConAlfa(alfa)); ty += 26 * u;
            if (!string.IsNullOrEmpty(disco.Nombre)) { mini.Escribe(d, mini.Ajusta(disco.Nombre, ancho), tx0, ty, Color4.Hex(0xC8D4F5, alfa)); ty += 21 * u; }
            string resumen = disco.Videos ? disco.Rutas.Count + " videos" : disco.Rutas.Count + (disco.Rutas.Count == 1 ? " canción" : " canciones") + (disco.Minutos > 0 ? " · " + disco.Minutos + " min" : "");
            mini.Escribe(d, resumen, tx0, ty, Color4.Hex(0xA0B2DE, alfa)); ty += 22 * u;
            if (f.EnCola > 0)
            {
                string q = f.EnCola + " en la cola"; float wq = mini.Mide(q) + 18 * k;
                d.Redondo(tx0, ty, wq, 22 * k, 11 * k, Verde.ConAlfa(0.22f * alfa));
                mini.Escribe(d, q, tx0 + 9 * k, ty + 1 * k, Verde.ConAlfa(alfa)); ty += 24 * k;
            }
            float yl = Math.Max(fy + pad + lado, ty) + 9 * u;
            d.Rect(fx + pad, yl, w - 2 * pad, 1, Color4.Hex(0xA0B4E6, 0.25f * alfa));
            mini.Escribe(d, f.Pedidas ? "LAS MÁS PEDIDAS DE ESTE DISCO" : "ALGUNAS DE ESTE DISCO", fx + pad, yl + 4 * u, Color4.Hex(0x96AADC, alfa));
            float yb = fy + h - pad - 20 * u, yf = yl + 26 * u, altoF = 26 * u;
            for (int i = 0; i < f.Titulos.Count; i++)
            {
                float yk = yf + i * altoF; if (yk + altoF > yb) break;
                float r = 10 * u; Circulo(fx + pad + r, yk + r, r, Verde.ConAlfa(0.25f * alfa));
                mini.EscribeCentrado(d, (i + 1).ToString(), fx + pad + r, yk + r - mini.Tam * 0.55f, Verde.ConAlfa(alfa));
                float xt = fx + pad + 2 * r + 9 * u, der = fx + w - pad;
                if (f.Pedidas && f.Veces[i] > 0) { string vt = f.Veces[i] == 1 ? "1 vez" : f.Veces[i] + " veces"; float vw = mini.Mide(vt); mini.Escribe(d, vt, der - vw, yk, Color4.Hex(0xFFB84D, alfa)); der -= vw + 8 * u; }
                string du = f.Duraciones[i];
                if (du.Length > 0 && der - mini.Mide(du) - 8 * u - xt >= (w - 2 * pad) * .5f) { float dw = mini.Mide(du); mini.Escribe(d, du, der - dw, yk, Color4.Hex(0xA0B0DA, alfa)); der -= dw + 8 * u; }
                mini.Escribe(d, mini.Ajusta(f.Titulos[i], Math.Max(10, der - xt)), xt, yk, Blanco.ConAlfa(alfa));
            }
            float rb = 9 * u; Circulo(fx + pad + rb, yb + rb + 2 * u, rb, Color4.Hex(0x48D470, alfa));
            mini.Escribe(d, disco.Rutas.Count > 0 ? "Entrar para ver las " + disco.Rutas.Count : "Entrar al disco", fx + pad + 2 * rb + 8 * u, yb, Color4.Hex(0xDFE6FF, alfa));
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
