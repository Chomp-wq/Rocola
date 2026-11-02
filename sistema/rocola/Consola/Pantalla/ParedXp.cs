// LA PARED DE DISCOS CON EL ASPECTO DE LA ROCOLA DE XP (dibujo propio, OpenGL ES directo). Arriba, la leyenda de los botones, las
// casillas de BUSCAR y AYUDA y la letra en la que se esta; debajo, la barra de letras (cada una con cuantos discos tiene); en medio,
// seis columnas y tres filas de portadas con su nombre; y bajo la linea del horizonte lo que suena, la cola, los creditos y la barra de
// avance. Las medidas salen de la de XP a 1024x768 y se escalan con la pantalla (k).
using System;
using System.Collections.Generic;
using System.IO;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private static readonly Color4 AzulBorde = Color4.Hex(0x5AAEFF), OroBorde = Color4.Hex(0xE6B43C), Lavanda = Color4.Hex(0xB9C4FF),
            Leyenda = Color4.Hex(0xD7DDF5), VerdeTexto = Color4.Hex(0x6EE590), AzulNumero = Color4.Hex(0x7FB8FF), TintaPastilla = Color4.Hex(0x0B1434);

        // La letra en negrita junto a la normal (DejaVuSans.ttf -> DejaVuSans-Bold.ttf); null si no esta.
        internal static string Negrita(string ruta)
        {
            try
            {
                string dir = Path.GetDirectoryName(ruta), nom = Path.GetFileNameWithoutExtension(ruta), ext = Path.GetExtension(ruta);
                foreach (string n in new[] { nom + "-Bold", nom + "Bold", nom + "-bold", nom.Replace("-Regular", "-Bold") })
                {
                    string r = Path.Combine(dir, n + ext);
                    if (r != ruta && File.Exists(r)) return r;
                }
            }
            catch { }
            return null;
        }

        // ---------------- LA CABECERA ----------------
        private void Cabecera(Controlador c, float W)
        {
            float cy = 30 * k;
            // la leyenda: mover (el rombo de las flechas), entrar o poner (verde) y volver (rojo)
            float x = 18 * k;
            Rombo(x + 10 * k, cy, 10 * k);
            x += 26 * k; mini.Escribe(d, "Mover", x, cy - mini.Tam * 0.62f, Leyenda); x += mini.Mide("Mover") + 16 * k;
            Boton(x + 9 * k, cy, 9 * k, 'V'); x += 24 * k; mini.Escribe(d, "Entrar / Poner", x, cy - mini.Tam * 0.62f, Leyenda); x += mini.Mide("Entrar / Poner") + 16 * k;
            Boton(x + 9 * k, cy, 9 * k, 'B'); x += 24 * k; mini.Escribe(d, "Volver", x, cy - mini.Tam * 0.62f, Leyenda); x += mini.Mide("Volver") + 22 * k;
            // las dos casillas
            float h = 42 * k, y = cy - h / 2;
            // dentro de un disco no van (FilaBuscaAyudaVisible de XP: se esconden con la lista de canciones)
            if (c.dentro == null)
            {
                zBuscar = Casilla(Math.Max(x, W * 0.30f), y, h, "BUSCAR", 'U', c.Buscando, AzulBorde);
                zAyuda = Casilla(zBuscar.X + zBuscar.W + 12 * k, y, h, "AYUDA", 'D', c.AyudaAbierta, OroBorde);
            }
            else { zBuscar = new Zona(x, y, 0, 0); zAyuda = zBuscar; }
            // la letra en la que se esta
            if (c.dentro == null && !c.Buscando && c.letras.Count > 0)
            {
                string l = "LETRA " + Controlador.NombreLetra(c.letras[c.letra]);
                if (c.letras[c.letra] == Form1.LetraTop) l = Controlador.NombreLetra(c.letras[c.letra]).ToUpperInvariant();
                float wl = nGrande.Mide(l), xl = W - 26 * k - wl;
                if (xl > zAyuda.X + zAyuda.W + 20 * k)
                {
                    d.Redondo(xl - 16 * k, cy - 13 * k, 5 * k, 26 * k, 2.5f * k, Color4.Hex(0x7088E6));
                    nGrande.Escribe(d, l, xl, cy - nGrande.Tam * 0.62f, Lavanda);
                }
            }
        }
        // El rombo de las flechas (como el de la leyenda de XP): cuatro puntas, dos rojas y dos doradas.
        private void Rombo(float cx, float cy, float r)
        {
            d.Triangulo(cx, cy - r, cx + r, cy, cx, cy, Rojo); d.Triangulo(cx + r, cy, cx, cy + r, cx, cy, Oro);
            d.Triangulo(cx, cy + r, cx - r, cy, cx, cy, Rojo); d.Triangulo(cx - r, cy, cx, cy - r, cx, cy, Oro);
            d.Triangulo(cx, cy - r * 0.42f, cx + r * 0.42f, cy, cx, cy + r * 0.42f, Color4.Hex(0x1A1430)); d.Triangulo(cx, cy - r * 0.42f, cx - r * 0.42f, cy, cx, cy + r * 0.42f, Color4.Hex(0x1A1430));
        }
        // Una casilla de la cabecera: borde de color, su dibujo (la lupa o el signo), el nombre y ROJO + la flecha.
        private Zona Casilla(float x, float y, float h, string nombre, char flecha, bool abierta, Color4 borde)
        {
            float wl = nMedia.Mide(nombre), w = 44 * k + wl + 16 * k + 62 * k, r = 12 * k;
            if (abierta) d.Brillo(x + w / 2, y + h / 2, w * 0.7f, borde.ConAlfa(0.35f));
            d.RedondoDegradado(x, y, w, h, r, Color4.Hex(0x16224E, 0.80f), Color4.Hex(0x0B1434, 0.80f));
            d.Contorno(x, y, w, h, r, 2f * k, borde.ConAlfa(abierta ? 1f : 0.85f));
            float cy = y + h / 2, ix = x + 22 * k;
            if (nombre == "BUSCAR")
            {
                // la lupa
                float rl = 8 * k;
                for (int i = 0; i < 28; i++)
                {
                    double a0 = 2 * Math.PI * i / 28, a1 = 2 * Math.PI * (i + 1) / 28;
                    d.Linea(ix - 2 * k + rl * (float)Math.Cos(a0), cy - 2 * k + rl * (float)Math.Sin(a0), ix - 2 * k + rl * (float)Math.Cos(a1), cy - 2 * k + rl * (float)Math.Sin(a1), 2.4f * k, Blanco);
                }
                d.Linea(ix + 4 * k, cy + 4 * k, ix + 10 * k, cy + 10 * k, 3f * k, Blanco);
            }
            else
            {
                // el signo de la ayuda, dorado y con su chispa
                double t = reloj.Elapsed.TotalSeconds;
                nEnorme.EscribeCentrado(d, "?", ix, cy - nEnorme.Tam * 0.56f - 1.5f * k * (float)Math.Sin(t * 2.2), Oro);
                d.Brillo(ix + 9 * k, cy - 12 * k, 5 * k * (0.7f + 0.3f * (float)Math.Sin(t * 3.1)), OroClaro.ConAlfa(0.9f));
            }
            float tx = x + 44 * k;
            nMedia.Escribe(d, nombre, tx, cy - nMedia.Tam * 0.62f, Blanco);
            float bx = tx + wl + 14 * k, rb = 10 * k;
            Boton(bx + rb, cy, rb, 'B');
            nChica.Escribe(d, "+", bx + 2 * rb + 4 * k, cy - nChica.Tam * 0.62f, Blanco);
            // la flecha roja en su cajita
            float qx = bx + 2 * rb + 18 * k, q = 18 * k;
            d.Redondo(qx, cy - q / 2, q, q, 3 * k, Color4.Hex(0x2A0F18, 0.9f));
            d.Contorno(qx, cy - q / 2, q, q, 3 * k, 1.6f * k, Rojo);
            float a = q * 0.28f;
            if (flecha == 'U') d.Triangulo(qx + q / 2, cy - a, qx + q / 2 + a, cy + a * 0.7f, qx + q / 2 - a, cy + a * 0.7f, Rojo);
            else d.Triangulo(qx + q / 2 - a, cy - a * 0.7f, qx + q / 2 + a, cy - a * 0.7f, qx + q / 2, cy + a, Rojo);
            return new Zona(x, y, w, h);
        }

        // ---------------- LA BARRA DE LETRAS ----------------
        // Todas las letras repartidas a lo ancho; debajo de cada una una rayita del largo de lo que tiene y cuantas canciones tiene (como XP). La elegida,
        // dentro de su marco.
        private float BarraLetras(Controlador c, float W, float y)
        {
            float h = 38 * k, x0 = 20 * k, x1 = W - 34 * k;
            d.RedondoDegradado(x0, y, x1 - x0, h, 10 * k, Color4.Hex(0x1A2658, 0.55f), Color4.Hex(0x101A40, 0.55f));
            int n = c.letras.Count; if (n == 0) return y + h;
            float paso = (x1 - x0 - 16 * k) / n;
            int maxD = 1; for (int i = 0; i < n; i++) maxD = Math.Max(maxD, c.CancionesEn(i));
            zLetras = new Zona(x0, y, x1 - x0, h);
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + 8 * k + paso * (i + 0.5f);
                bool sel = i == c.letra;
                string l = Controlador.NombreLetra(c.letras[i]);
                Letra f = sel ? nMedia : nChica;
                if (f.Mide(l) > paso - 6 * k) l = l.Substring(0, 1);
                int cuantos = c.CancionesEn(i);
                if (sel)
                {
                    float ws = Math.Min(paso * 0.97f, 232 * k);
                    d.Brillo(cx, y + 13 * k, ws * 0.5f, AzulBorde.ConAlfa(0.18f));
                    d.Redondo(cx - ws / 2, y + 2 * k, ws, 22 * k, 10 * k, Color4.Hex(0x0E1A44, 0.85f));
                    d.Contorno(cx - ws / 2, y + 2 * k, ws, 22 * k, 10 * k, 2f * k, Color4.Hex(0x9CCBFF));
                }
                f.EscribeCentrado(d, l, cx, y + 13 * k - f.Tam * 0.50f, sel ? Blanco : Color4.Hex(0xC8D0F0));
                float wr = Math.Max(10 * k, (paso * 0.78f) * (float)Math.Sqrt(cuantos / (float)maxD));
                d.Rect(cx - wr / 2, y + 25 * k, wr, 1.2f * k, (sel ? Color4.Hex(0x9CCBFF) : Color4.Hex(0x8C9AD0)).ConAlfa(sel ? 0.9f : 0.55f));
                string nn = cuantos < 10000 ? cuantos.ToString() : Math.Min(999, cuantos / 1000) + "k";
                micro.EscribeCentrado(d, nn, cx, y + 26 * k, Color4.Hex(0x8C98C8, 0.9f));
            }
            return y + h;
        }

        // ---------------- LA PARED ----------------
        private const int Columnas = 6, Filas = 3;
        private void Pared(Controlador c, float W, float abajo)
        {
            Form1 m = c.motor;
            double ahora = reloj.Elapsed.TotalMilliseconds;
            float top = BarraLetras(c, W, 58 * k) + 8 * k;
            List<Form1.Disco> ds = c.DiscosDeLetra();
            if (ds.Count == 0) { nMedia.EscribeCentrado(d, "No hay música en la carpeta de la rocola", W / 2, 300 * k, Gris); return; }
            int cols = Columnas, porPag = Controlador.DiscosPorPagina, pag = c.cursorDisco / porPag;
            int paginas = (ds.Count + porPag - 1) / porPag;
            // LA GEOMETRIA DE XP (AlbumView.Geometry y CellMetrics): la pared va de 93 a 645 (de 768), cada casilla es un sexto del ancho y
            // un tercio del alto; la portada, un cuadrado de min(ancho - 16, alto - 53) arriba de la casilla, con la etiqueta de dos
            // renglones (38 px) pegada debajo.
            top = 107 * k;
            float margen = 14 * k, alto = 555 * k * (abajo / (662f * k)), celW = (W - 2 * margen) / cols, celH = alto / Filas;
            float lado = Math.Max(10 * k, Math.Min(celW - 16 * k, celH - 53 * k));
            // PASAR DE PAGINA DESLIZANDO (pagina_desliza= de XP): la vieja sale por un lado mientras la nueva entra, columna a columna.
            if (pagVista != pag || pagLetraVista != c.letra)
            {
                if (m.PaginaDesliza && pagVista >= 0 && pagLetraVista == c.letra && pagVista < paginas) { pagAnt = pagVista; pagDesde = ahora; pagDir = c.SentidoPagina >= 0 ? 1f : -1f; }
                else pagAnt = -1;
                pagVista = pag; pagLetraVista = c.letra;
            }
            bool desliza = pagAnt >= 0 && ahora - pagDesde < PaginaMs + ColumnaMs * (cols - 1);
            if (!desliza) pagAnt = -1;
            if (desliza) Pagina(c, ds, pagAnt, false, top, margen, celW, celH, lado, cols, porPag, col => -pagDir * W * CurvaPagina(ahora - pagDesde, col, cols));
            Pagina(c, ds, pag, true, top, margen, celW, celH, lado, cols, porPag, col => desliza ? pagDir * W * (1f - CurvaPagina(ahora - pagDesde, col, cols)) : 0f);
            if (!desliza && m.FichaDisco && !c.Reposo && !c.AyudaAbierta && c.letras[c.letra] != Form1.LetraTop && c.cursorDisco < ds.Count)
                FichaRapida(c, ds[c.cursorDisco], ahora - focoDesde, new Zona(margen, top, W - 2 * margen, alto), celW, celH);
            // las paginas: unos puntos a la derecha de abajo (la pagina en la que se esta, encendida)
            if (paginas > 1)
            {
                float px = W - 30 * k, py = abajo - 16 * k;
                for (int i = paginas - 1; i >= 0; i--) { Circulo(px, py, i == pag ? 4.5f * k : 3f * k, i == pag ? Lavanda : Lavanda.ConAlfa(0.35f)); px -= 14 * k; }
            }
        }
        // Una pagina de la pared, corrida 'dx(columna)'. 'viva': la que se elige (portada enfocada, cubo, relevo y lo que senala la ayuda).
        private void Pagina(Controlador c, List<Form1.Disco> ds, int pag, bool viva, float top, float margen, float celW, float celH, float lado, int cols, int porPag, Func<int, float> dx)
        {
            int elegido = -1;
            for (int pasada = 0; pasada < 2; pasada++)   // la elegida, al final (por encima de sus vecinas)
                for (int i = 0; i < porPag; i++)
                {
                    int n = pag * porPag + i; if (n >= ds.Count) break;
                    bool sel = viva && n == c.cursorDisco;
                    if (sel) elegido = n;
                    if ((pasada == 1) != sel) continue;
                    Form1.Disco disco = ds[n];
                    float cx = margen + (i % cols) * celW + celW / 2 + dx(i % cols), y0 = top + (i / cols) * celH + 6 * k;
                    if (sel && (focoDisco != n || focoCarpeta != disco.Carpeta))
                    {
                        // EL RELEVO DE GIRO (AlbumView.MoveSelectionTo de XP): el que se deja da un giro rapido que se apaga y el nuevo arranca plano
                        bool mismaPagina = focoCarpeta != null && focoDisco >= 0 && focoDisco / porPag == pag && relevoLetra == c.letra;
                        if (mismaPagina) { relevoIdx = focoDisco; relevoAng = AnguloCubo(); relevoGiro = 0.7f * (n > focoDisco ? 1f : -1f); } else relevoIdx = -1;
                        giroDir = n >= focoDisco ? 1f : -1f; relevoLetra = c.letra;
                        focoDisco = n; focoCarpeta = disco.Carpeta; focoDesde = reloj.Elapsed.TotalMilliseconds;
                    }
                    // la casilla, como en XP: la portada arriba (centrada en lo que sobra) y la etiqueta debajo
                    float bloque = lado + 3 * k + 38 * k, y1 = y0 - 6 * k + Math.Max(6 * k, (celH - bloque) / 2);
                    if (sel)
                    {
                        // LA ELEGIDA: su caja (el ancho de la casilla menos 16) crece desde la portada en 170 ms y dentro gira el cubo
                        double avance = Math.Min(1.0, (reloj.Elapsed.TotalMilliseconds - focoDesde) / 170.0);
                        float e = (float)(1 - Math.Pow(1 - avance, 3));
                        float caja = lado + ((celW - 16 * k) * 1.0f - lado) * e, cy0 = y1 + lado - caja - 6 * k * e;   // crece hacia arriba y a los lados: el pie queda con la portada
                        if (caja < lado) caja = lado;
                        float bx = cx - caja / 2, by = Math.Max(top - 4 * k, cy0);
                        Cubo(disco.Portada, disco.Artista, bx, by, caja, AnguloCubo());
                        d.Brillo(bx + caja / 2, by + caja / 2, caja * 0.62f, new Color4(0.8f, 0.85f, 1f, 0.10f));
                        d.Contorno(bx - 3 * k, by - 3 * k, caja + 6 * k, caja + 6 * k, 7 * k, 3f * k, Color4.Hex(0xC4C8D4, 0.95f));
                        zElegido = new Zona(bx - 4 * k, by - 4 * k, caja + 8 * k, caja + 8 * k); zPortadaElegida = new Zona(cx - lado / 2, y1, lado, lado);
                        // LA PASTILLA (PintaFoco de XP): la letra, de 0,46 a 0,34 del alto, la mas grande que quepa; si ni asi, el texto corto
                        float hp = 18 * k, cabe = caja - 8 * k - 2 - hp * 1.1f;
                        string letrero = TextoFoco(disco);
                        Letra lp = LetraQueQuepa(letrero, hp, cabe);
                        if (lp == null) { string corto = disco.Rutas.Count + (disco.Videos ? (disco.Rutas.Count == 1 ? " video" : " videos") : (disco.Rutas.Count == 1 ? " canción" : " canciones")); lp = LetraQueQuepa(corto, hp, cabe); letrero = corto; }
                        if (lp == null) { lp = Ui(hp * 0.34f); letrero = lp.Ajusta(letrero, cabe); }
                        float wl = Math.Min(caja - 8 * k - 2, lp.Mide(letrero) + hp * 1.1f), yp = by + caja - hp - 5 * k;
                        d.Redondo(cx - wl / 2, yp, wl, hp, hp / 2, new Color4(0.02f, 0.03f, 0.08f, 0.70f));
                        d.Contorno(cx - wl / 2, yp, wl, hp, hp / 2, 1f, new Color4(1, 1, 1, 0.35f));
                        EnRenglon(lp, letrero, cx - lp.Mide(letrero) / 2, yp, wl, hp, Blanco);
                    }
                    else if (viva && n == relevoIdx) Cubo(disco.Portada, disco.Artista, cx - lado / 2, y1, lado, PasoRelevo());
                    else
                    {
                        // quieta: el cubo de frente (la cara mide el 71 % del cuadrado) con su filo oscuro, como Cube.Draw de XP
                        Cubo(disco.Portada, disco.Artista, cx - lado / 2, y1, lado, 0);
                        float wc = (float)Math.Round(lado * 0.7071f);
                        d.Contorno(cx - wc / 2, y1, wc, lado, 0.1f, 1f, new Color4(0, 0, 0, 0.47f));
                    }
                    // la etiqueta: «Artista - Disco» en dos renglones centrados, negrita, con puntos si no cabe (BakeCell de XP)
                    string etiqueta = string.IsNullOrEmpty(disco.Nombre) ? disco.Artista : disco.Artista + " - " + disco.Nombre;
                    List<string> renglones = Envuelve(etiqueta, mini, celW - 4 * k);
                    if (renglones.Count > 2) { renglones[1] = mini.Ajusta(renglones[1] + " " + string.Join(" ", renglones.GetRange(2, renglones.Count - 2)), celW - 4 * k); renglones.RemoveRange(2, renglones.Count - 2); }
                    float ly = y1 + lado + 3 * k + (38 * k - renglones.Count * 15.5f * k) / 2;
                    for (int rr = 0; rr < renglones.Count; rr++) mini.EscribeCentrado(d, renglones[rr], cx, ly + rr * 15.5f * k, Blanco);
                }
        }

        // La letra mas grande (de 0,46 a 0,34 del alto, de medio en medio pixel) con la que 'texto' cabe en 'ancho'; null si ninguna.
        private Letra LetraQueQuepa(string texto, float alto, float ancho)
        {
            for (float t = alto * 0.46f; t >= alto * 0.34f - 0.01f; t -= 0.5f) { Letra l = Ui(t); if (l.Mide(texto) <= ancho) return l; }
            return null;
        }

        // ---------------- LO QUE SUENA (bajo el horizonte) ----------------
        private void Suena(Controlador c, float W, float H, float abajo)
        {
            Form1 m = c.motor;
            float yh = Horizonte(H);
            if (abajo < yh - 1) d.Degradado(0, abajo, W, H - abajo, Color4.Hex(0x101A40, 0.75f * Math.Min(1f, (yh - abajo) / (40 * k))), Color4.Hex(0x0A1230, 0.92f));
            float y = abajo + 10 * k;
            // a la derecha: los creditos y la cola
            string cr = m.Creditos > 0 ? m.Creditos.ToString() : "";
            float hp = 35 * k;
            float wcr = m.Creditos > 0 ? nChica.Mide("Creditos") + nGrande.Mide(cr) + 34 * k : nChica.Mide("Inserta una moneda") + 30 * k;
            float xcr = W - 14 * k - wcr;
            Pastilla(xcr, y + 1 * k, wcr, hp);
            if (m.Creditos > 0)
            {
                nChica.Escribe(d, "Creditos", xcr + 14 * k, y + 1 * k + hp / 2 - nChica.Tam * 0.62f, Color4.Hex(0xC4CCE8));
                nGrande.Escribe(d, cr, xcr + 22 * k + nChica.Mide("Creditos"), y + 1 * k + hp / 2 - nGrande.Tam * 0.64f, VerdeTexto);
            }
            else nChica.Escribe(d, "Inserta una moneda", xcr + 15 * k, y + 1 * k + hp / 2 - nChica.Tam * 0.62f, Color4.Hex(0xE2E6F8));
            zCreditos = new Zona(xcr, y + 1 * k, wcr, hp);
            string q = m.EnCola.ToString();
            float wq = nChica.Mide("En cola") + nGrande.Mide(q) + 3 * 20 * k + 34 * k, xq = xcr - 12 * k - wq;
            Pastilla(xq, y + 1 * k, wq, hp);
            nChica.Escribe(d, "En cola", xq + 14 * k, y + 1 * k + hp / 2 - nChica.Tam * 0.62f, Color4.Hex(0xC4CCE8));
            float sq = Salto(c);
            nGrande.Escribe(d, q, xq + 22 * k + nChica.Mide("En cola"), y + 1 * k + hp / 2 - nGrande.Tam * 0.64f - sq, sq > 0 ? Dorado : AzulNumero);
            List<string> ports = m.PortadasEnCola(3);
            float bx = xq + 28 * k + nChica.Mide("En cola") + nGrande.Mide(q);
            for (int i = 0; i < 3; i++)
            {
                float qx = bx + i * 20 * k, qy = y + 1 * k + hp / 2 - 8 * k;
                Textura tq = i < ports.Count && ports[i] != null ? caratulas.De(ports[i]) : null;
                if (tq != null) d.Imagen(tq, qx, qy, 16 * k, 16 * k);
                else d.Rect(qx, qy, 16 * k, 16 * k, i < m.EnCola ? Color4.Hex(0x5A6AB0, 0.6f) : Color4.Hex(0x2A3462, 0.55f));
                d.Contorno(qx, qy, 16 * k, 16 * k, 2 * k, 1f * k, Color4.Hex(0x6E7CB8, 0.6f));
            }
            zCola = new Zona(xq, y + 1 * k, wq, hp);
            // a la izquierda: lo que suena
            string t = m.SonandoTitulo;
            float ancho = xq - 24 * k;
            if (t == null)
            {
                zSuena = new Zona(14 * k, y, 70 * k, 70 * k);
                nEnorme.Escribe(d, nEnorme.Ajusta("Nada reproduciéndose", ancho - 110 * k), 110 * k, y + 2 * k, VerdeTexto);
            }
            else
            {
                float lado = 56 * k;
                zSuena = new Zona(24 * k, y, lado, lado);
                if (m.ColorGenero) Resplandor(m.ColorSonando, 24 * k + lado / 2, y + lado / 2, lado * 0.9f);
                Cubo(m.SonandoPortada, m.SonandoDisco ?? t, 24 * k, y, lado, reloj.Elapsed.TotalSeconds * 1.44);   // el cubo chico (XP)
                float xt = 24 * k + lado + 16 * k, at = ancho - xt;
                EscribeDesliza(c, nGrande, t, xt, y - 1 * k, at, Blanco);
                string sub = m.SonandoDisco ?? "";
                string gen = m.ColorGenero ? m.EtiquetaSonando : "";
                float xs = xt;
                if (gen.Length > 0 && gen != "Otros")
                {
                    float wg = mini.Mide(gen) + 14 * k;
                    d.Redondo(xs, y + 33 * k, wg, 20 * k, 10 * k, Color4.Hex(m.ColorSonando, 0.30f + 0.25f * pGraves));
                    mini.Escribe(d, gen, xs + 7 * k, y + 34 * k, Blanco);
                    xs += wg + 8 * k;
                }
                if (sub.Length > 0) mini.Escribe(d, mini.Ajusta(sub, at - (xs - xt)), xs, y + 34 * k, Color4.Hex(0xAEB8DE));
            }
            // la barra de avance (neon) y el reloj
            string rl = m.SonandoTiempo; if (string.IsNullOrEmpty(rl)) rl = "0:00 / 0:00";
            float wr = nMedia.Mide(rl), xb = 114 * k, yb = H - 25 * k, wb = W - xb - wr - 40 * k, hb = 9 * k;
            d.Redondo(xb, yb, wb, hb, hb / 2, Color4.Hex(0x141D46, 0.92f));
            float p = Math.Max(0f, Math.Min(1f, c.Progreso));
            if (p > 0)
            {
                Color4 neon = m.ColorGenero && m.ColorSonando != 0 ? Color4.Hex(m.ColorSonando) : Color4.Hex(0x58D2FF);
                float wp = Math.Max(hb, wb * p);
                d.Brillo(xb + wp / 2, yb + hb / 2, wp * 0.55f, neon.ConAlfa(0.10f));
                d.RedondoDegradado(xb, yb, wp, hb, hb / 2, Dibujo.Mezcla(neon, Blanco, 0.45f), neon);
                d.Brillo(xb + wp, yb + hb / 2, 16 * k, Blanco.ConAlfa(0.65f));
                d.Brillo(xb + wp, yb + hb / 2, 5 * k, Blanco);
            }
            nMedia.Escribe(d, rl, W - 22 * k - wr, yb + hb / 2 - nMedia.Tam * 0.62f, Blanco);
        }
        // Una pastilla de la franja de abajo (la cola, los creditos): oscura, con un filo claro.
        private void Pastilla(float x, float y, float w, float h)
        {
            d.RedondoDegradado(x, y, w, h, h / 2, Color4.Hex(0x1A2658, 0.72f), Color4.Hex(0x0E1638, 0.72f));
            d.Contorno(x, y, w, h, h / 2, 1.2f * k, Color4.Hex(0x96A8E6, 0.28f));
        }
    }
}
