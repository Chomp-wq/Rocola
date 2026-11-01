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
    internal sealed class VistaGrafica : IVista
    {
        private readonly ISuperficie sup;
        private readonly Dibujo d;
        private readonly Letra chica, media, grande, enorme;
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
        private void Cielo(float W, float H, double t)
        {
            d.Degradado(0, 0, W, H * 0.55f, Color4.Hex(0x03061A), Color4.Hex(0x0C1440));
            d.Degradado(0, H * 0.55f, W, H * 0.45f, Color4.Hex(0x0C1440), Color4.Hex(0x1A0F3A));
            for (int i = 0; i < estrellas.Length; i += 5)
            {
                float a = Math.Min(1f, 0.45f + 0.55f * (float)(0.5 + 0.5 * Math.Sin(t * estrellas[i + 4] + estrellas[i + 3])) + 0.35f * pAgudos * (i % 3 == 0 ? 1 : 0));
                float tam = estrellas[i + 2] * k;
                d.Brillo(estrellas[i] * W, estrellas[i + 1] * H, tam * 2.2f, new Color4(0.85f, 0.9f, 1f, a * Math.Min(1f, 0.35f + tam / (2.5f * k))));
            }
            // la aurora: tiras verticales finas; su altura, su fuerza y su color ondulan despacio (verde -> turquesa -> violeta). Sube
            // desde nada hasta su fuerza y se apaga hacia el horizonte, como un velo.
            int tiras = 180; float w = W / tiras, base_ = H * 0.64f;
            for (int i = 0; i < tiras; i++)
            {
                double x = (i + 0.5) / tiras;
                double alto = H * (0.20 + 0.07 * Math.Sin(x * 7.0 + t * 0.30) + 0.035 * Math.Sin(x * 19.0 - t * 0.55)) * (1.0 + 0.30 * pMedios);   // la aurora late con la musica
                double mezcla = 0.5 + 0.5 * Math.Sin(x * 3.5 + t * 0.10);
                float fuerza = (float)(0.26 + 0.12 * Math.Sin(x * 11.0 + t * 0.7) + 0.05 * Math.Sin(x * 31.0 - t * 1.3)) * (0.75f + 0.8f * pGraves);
                float r = (float)(0.25 + 0.45 * mezcla), g = (float)(0.95 - 0.45 * mezcla), b = (float)(0.65 + 0.3 * mezcla);
                float y0 = (float)(base_ - alto), x0 = i * w;
                d.Degradado(x0, y0, w, (float)alto * 0.78f, new Color4(r, g, b, 0f), new Color4(r, g, b, fuerza));
                d.Degradado(x0, y0 + (float)alto * 0.78f, w, (float)alto * 0.3f, new Color4(r, g, b, fuerza), new Color4(r, g, b, 0f));
            }
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
            // franja de arriba
            d.Rect(0, 0, W, 64 * k, Panel.ConAlfa(0.82f));
            grande.Escribe(d, "ROCOLA", 24 * k, 14 * k, Dorado);
            string cr = m.Creditos > 0 ? "Créditos: " + m.Creditos : "Inserta una moneda";
            float wcr = media.Mide(cr) + 40 * k;
            d.Redondo(W - wcr - 20 * k, 12 * k, wcr, 40 * k, 20 * k, m.Creditos > 0 ? Verde.ConAlfa(0.25f) : Dorado.ConAlfa(0.25f));
            media.Escribe(d, cr, W - wcr, 19 * k, m.Creditos > 0 ? Verde : Dorado);
            if (m.EnCola > 0) { string q = "En la cola: " + m.EnCola; chica.Escribe(d, q, W - wcr - 40 * k - chica.Mide(q), 24 * k, Gris); }
            float abajo = H - 120 * k;   // donde empieza lo que suena
            if (c.AsistentePaso >= 0) { Asistente(c, W, H); d.Termina(); sup.Presenta(); return; }
            if (c.Atraccion && c.AtraccionActual != null) { Atraccion(c, W, H); d.Termina(); sup.Presenta(); return; }
            if (c.AjusteSel >= 0) { Ajustes(c, W, H); AvisoEncima(c, W * 1.45f, 130 * k); d.Termina(); sup.Presenta(); return; }
            if (c.Buscando) Buscador(c, W, abajo);
            else if (c.dentro == null) Pared(c, W, abajo); else Disco(c, W, abajo);
            Suena(c, W, H, abajo);
            string av = c.AvisoVigente;
            if (av.Length > 0)
            {
                float wa = media.Mide(av) + 48 * k;
                d.Redondo((W - wa) / 2, abajo - 104 * k, wa, 52 * k, 26 * k, Color4.Hex(0x101830, 0.92f));
                media.EscribeCentrado(d, av, W / 2, abajo - 92 * k, Dorado);
            }
            d.Termina();
            sup.Presenta();
        }

        // EL MODO ATRACCION (el escaparate de XP): la portada grande, que se mece un poco, y su ficha; debajo, un mensaje cada 5 s.
        private int atraccionVista = -1; private double atraccionDesde;
        private void Atraccion(Controlador c, float W, float H)
        {
            double t = reloj.Elapsed.TotalSeconds;
            if (atraccionVista != c.AtraccionDisco) { atraccionVista = c.AtraccionDisco; atraccionDesde = t; }
            double edad = t - atraccionDesde;
            float alfa = (float)Math.Max(0, Math.Min(1, Math.Min(edad / 0.6, (8.0 - edad) / 0.6)));   // entra y sale despacio
            Form1.Disco disco = c.AtraccionActual;
            float lado = H * 0.48f, x = W * 0.08f, y = H * 0.16f + (float)Math.Sin(t * 0.7) * 6 * k;
            Textura tx = caratulas.De(disco.Portada);
            if (tx != null) d.Brillo(x + lado / 2, y + lado / 2, lado * 0.9f, tx.Media.ConAlfa(0.5f * alfa));
            Portada(disco.Portada, disco.Artista, x, y, lado);
            if (alfa < 1) d.Rect(x - 2, y - 2, lado + 4, lado + 4, Color4.Hex(0x0A1230, 1 - alfa));
            float fx = x + lado + 40 * k, fw = W - fx - 40 * k;
            chica.Escribe(d, "EN ESTA ROCOLA", fx, y + 10 * k, Gris.ConAlfa(alfa));
            string nombre = string.IsNullOrEmpty(disco.Nombre) ? disco.Artista : disco.Nombre;
            enorme.Escribe(d, enorme.Ajusta(nombre, fw), fx, y + 40 * k, Blanco.ConAlfa(alfa));
            grande.Escribe(d, grande.Ajusta(disco.Artista, fw), fx, y + 100 * k, Verde.ConAlfa(alfa));
            float px = fx, py = y + 160 * k;
            foreach (string pastilla in new[] { disco.Pedidas > 0 ? "pedido " + disco.Pedidas + (disco.Pedidas == 1 ? " vez" : " veces") : null, disco.Rutas.Count + (disco.Rutas.Count == 1 ? " canción" : " canciones") })
            {
                if (pastilla == null) continue;
                float w = media.Mide(pastilla) + 28 * k;
                d.Redondo(px, py, w, 40 * k, 20 * k, PanelClaro.ConAlfa(alfa));
                media.Escribe(d, pastilla, px + 14 * k, py + 6 * k, Dorado.ConAlfa(alfa));
                px += w + 12 * k;
            }
            string[] mensajes = { "Toca un botón y elige entre " + c.TotalCanciones.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " canciones",
                "Pon tu canción: una moneda = 4 canciones", "Busca cualquier canción con el botón rojo y la flecha" };
            int im = (int)(t / 5.0) % mensajes.Length; double em = t % 5.0;
            float am = (float)Math.Max(0, Math.Min(1, Math.Min(em / 0.5, (5.0 - em) / 0.5)));
            float my = H * 0.80f + (1 - am) * 20 * k;
            d.Brillo(W / 2, my + 20 * k, W * 0.35f, Dorado.ConAlfa(0.10f * am));
            grande.EscribeCentrado(d, mensajes[im], W / 2, my, Dorado.ConAlfa(am));
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
            float rx = x0 + 10 * (tecla + sep) + 24 * k, rw = W - rx - 24 * k, ry = y0 + 80 * k, fila_ = 36 * k;
            int caben = Math.Max(1, (int)((abajo - ry - 40 * k) / fila_));
            if (c.Resultados.Count == 0 && c.Escrito.Length > 0) media.Escribe(d, "Nada con eso", rx, ry, Gris);
            int desde = Math.Max(0, Math.Min(c.SelResultado - caben / 2, c.Resultados.Count - caben));
            for (int i = 0; i < caben && desde + i < c.Resultados.Count; i++)
            {
                Form1.Resultado r = c.Resultados[desde + i]; float y = ry + i * fila_;
                bool sel = c.EnResultados && desde + i == c.SelResultado;
                if (sel) d.Redondo(rx - 8 * k, y - 2 * k, rw + 16 * k, fila_ - 4 * k, 8 * k, PanelClaro);
                string tipo = r.Tipo == 0 ? "ARTISTA" : r.Tipo == 1 ? "DISCO" : r.Tipo == 3 ? "SERVICIO" : "CANCIÓN";
                chica.Escribe(d, tipo, rx, y + 6 * k, r.Tipo == 2 ? Verde : Dorado);
                media.Escribe(d, media.Ajusta(r.Texto, rw - 100 * k), rx + 100 * k, y + 2 * k, sel ? Blanco : Gris);
            }
            chica.Escribe(d, "PONER: escribir / elegir    REGRESAR: borrar    REGRESAR + ARRIBA: cerrar", x0, abajo - 28 * k, Gris.ConAlfa(0.8f));
        }

        private void Pared(Controlador c, float W, float abajo)
        {
            // las letras
            float x = 24 * k, y = 76 * k;
            for (int i = 0; i < c.letras.Count; i++)
            {
                string l = Controlador.NombreLetra(c.letras[i]); float w = Math.Max(36 * k, media.Mide(l) + 18 * k);
                if (x + w > W - 24 * k) break;
                if (i == c.letra) d.Redondo(x, y, w, 40 * k, 8 * k, Dorado);
                media.EscribeCentrado(d, l, x + w / 2, y + 6 * k, i == c.letra ? Fondo : Gris);
                x += w + 4 * k;
            }
            List<Form1.Disco> ds = c.DiscosDeLetra();
            if (ds.Count == 0) { media.EscribeCentrado(d, "No hay música en la carpeta de la rocola", W / 2, 300 * k, Gris); return; }
            int cols = 4, filas = 2, porPag = Controlador.DiscosPorPagina, pag = c.cursorDisco / porPag;
            float top = 130 * k, alto = abajo - top - 10 * k, celW = (W - 48 * k) / cols, celH = alto / filas;
            float lado = Math.Min(celW - 24 * k, celH - 70 * k);
            for (int i = 0; i < porPag; i++)
            {
                int n = pag * porPag + i; if (n >= ds.Count) break;
                Form1.Disco disco = ds[n];
                float cx = 24 * k + (i % cols) * celW + celW / 2, cy = top + (i / cols) * celH;
                bool sel = n == c.cursorDisco;
                // LA PORTADA ENFOCADA (como XP): crece un 12 % en 170 ms desde el centro de su borde de abajo, con un halo de su propio
                // color; las demas bajan un 20 % de brillo.
                if (sel && (focoDisco != n || focoCarpeta != disco.Carpeta)) { focoDisco = n; focoCarpeta = disco.Carpeta; focoDesde = reloj.Elapsed.TotalMilliseconds; }
                double avance = sel ? Math.Min(1.0, (reloj.Elapsed.TotalMilliseconds - focoDesde) / 170.0) : 0.0;
                float crece = 1f + 0.12f * (float)(1 - Math.Pow(1 - avance, 3));
                float l = lado * crece, px = cx - l / 2, py = cy + lado - l;
                if (sel)
                {
                    Textura tx = caratulas.De(disco.Portada);
                    Color4 halo = tx != null ? tx.Media : Dorado;
                    d.Brillo(cx, py + l / 2, l * 0.95f, halo.ConAlfa(0.55f));
                    d.Redondo(px - 5 * k, py - 5 * k, l + 10 * k, l + 10 * k, 10 * k, Dorado);
                }
                Portada(disco.Portada, disco.Artista, px, py, l);
                if (!sel) d.Rect(px, py, l, l, new Color4(0, 0, 0, 0.2f));
                else
                {
                    string letrero = disco.Videos ? disco.Rutas.Count + (disco.Rutas.Count == 1 ? " video" : " videos")
                        : disco.Rutas.Count + (disco.Rutas.Count == 1 ? " canción" : " canciones") + (disco.Minutos > 0 ? " · " + disco.Minutos + " min" : "");
                    float wl = chica.Mide(letrero) + 16 * k;
                    if (wl > l + 20 * k) letrero = disco.Rutas.Count + (disco.Videos ? " videos" : " canciones");
                    wl = chica.Mide(letrero) + 16 * k;
                    d.Redondo(cx - wl / 2, py + l - 30 * k, wl, 24 * k, 12 * k, new Color4(0, 0, 0, 0.6f));
                    chica.EscribeCentrado(d, letrero, cx, py + l - 28 * k, Blanco);
                }
                string a = chica.Ajusta(disco.Artista, celW - 12 * k), nm = chica.Ajusta(disco.Nombre, celW - 12 * k);
                chica.EscribeCentrado(d, a, cx, py + l + 10 * k, sel ? Blanco : Gris);
                if (!string.IsNullOrEmpty(nm)) chica.EscribeCentrado(d, nm, cx, py + l + 32 * k, sel ? Dorado : Gris.ConAlfa(0.7f));
            }
            int paginas = (ds.Count + porPag - 1) / porPag;
            if (paginas > 1) chica.Escribe(d, "Página " + (pag + 1) + " de " + paginas, 24 * k, abajo - 26 * k, Gris.ConAlfa(0.8f));
            string ayuda = "IZQ/DER: disco   ARRIBA/ABAJO: página   REGRESAR + IZQ/DER: letra   REGRESAR + ARRIBA: buscar";
            chica.Escribe(d, ayuda, W - 24 * k - chica.Mide(ayuda), abajo - 26 * k, Gris.ConAlfa(0.8f));
        }

        private void Disco(Controlador c, float W, float abajo)
        {
            Form1.Disco disco = c.dentro;
            float lado = Math.Min(360 * k, abajo - 110 * k);
            Portada(disco.Portada, disco.Artista, 32 * k, 90 * k, lado);
            float x = 32 * k + lado + 32 * k, ancho = W - x - 32 * k;
            grande.Escribe(d, grande.Ajusta(disco.Artista, ancho), x, 84 * k, Blanco);
            if (!string.IsNullOrEmpty(disco.Nombre)) media.Escribe(d, media.Ajusta(disco.Nombre, ancho), x, 124 * k, Dorado);
            float top = 168 * k, fila = 38 * k;
            int caben = Math.Max(1, (int)((abajo - top - 10 * k) / fila));
            c.PorPagina = caben;
            int n = disco.Rutas.Count, desde = Math.Max(0, Math.Min(c.cursorCancion - caben / 2, n - caben));
            for (int i = 0; i < caben && desde + i < n; i++)
            {
                int j = desde + i; float y = top + i * fila;
                bool sel = j == c.cursorCancion;
                if (sel) d.Redondo(x - 10 * k, y - 2 * k, ancho + 20 * k, fila - 4 * k, 8 * k, PanelClaro);
                chica.Escribe(d, (j + 1).ToString().PadLeft(2), x, y + 6 * k, sel ? Dorado : Gris);
                media.Escribe(d, media.Ajusta(disco.Titulos[j], ancho - 50 * k), x + 44 * k, y + 2 * k, sel ? Blanco : Gris);
            }
            chica.Escribe(d, "ARRIBA/ABAJO: canción   IZQ/DER: página   PONER: pedirla (1 crédito)   REGRESAR: a los discos", x, abajo - 28 * k, Gris.ConAlfa(0.8f));
        }

        private void Suena(Controlador c, float W, float H, float abajo)
        {
            Form1 m = c.motor;
            d.Rect(0, abajo, W, H - abajo, Panel.ConAlfa(0.88f));
            float lado = H - abajo - 24 * k;
            string t = m.SonandoTitulo;
            if (t == null) { media.Escribe(d, "Elige un disco y pon tu canción", 24 * k, abajo + 40 * k, Gris); return; }
            Portada(m.SonandoPortada, m.SonandoDisco ?? t, 12 * k, abajo + 12 * k, lado);
            float x = 24 * k + lado, ancho = W - x - 24 * k;
            // las siguientes de la cola, a la derecha (como la capsula de la cola de XP)
            List<string> prox = m.ProximasEnCola(3);
            if (prox.Count > 0)
            {
                float wc = W * 0.30f, xc = W - wc - 16 * k;
                chica.Escribe(d, "SIGUEN (" + m.EnCola + ")", xc, abajo + 14 * k, Dorado);
                for (int i = 0; i < prox.Count; i++) chica.Escribe(d, chica.Ajusta((i + 1) + ". " + prox[i], wc), xc, abajo + (38 + 22 * i) * k, Gris);
                ancho = xc - x - 16 * k;
            }
            chica.Escribe(d, "SUENA", x, abajo + 14 * k, Dorado);
            grande.Escribe(d, grande.Ajusta(t, ancho), x, abajo + 36 * k, Blanco);
            if (m.SonandoDisco != null) chica.Escribe(d, chica.Ajusta(m.SonandoDisco, ancho), x, abajo + 76 * k, Gris);
            float p = c.Progreso;
            float wb = W - x - 24 * k;
            d.Rect(x, H - 10 * k, wb, 4 * k, PanelClaro);
            d.Rect(x, H - 10 * k, wb * p, 4 * k, Dorado);
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
