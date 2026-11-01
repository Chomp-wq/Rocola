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
        private void Cielo(float W, float H, double t, float alfa = 1f)
        {
            d.Degradado(0, 0, W, H * 0.55f, Color4.Hex(0x03061A, alfa), Color4.Hex(0x0C1440, alfa));
            d.Degradado(0, H * 0.55f, W, H * 0.45f, Color4.Hex(0x0C1440, alfa), Color4.Hex(0x1A0F3A, alfa));
            for (int i = 0; i < estrellas.Length; i += 5)
            {
                float a = Math.Min(1f, 0.45f + 0.55f * (float)(0.5 + 0.5 * Math.Sin(t * estrellas[i + 4] + estrellas[i + 3])) + 0.35f * pAgudos * (i % 3 == 0 ? 1 : 0));
                float tam = estrellas[i + 2] * k;
                d.Brillo(estrellas[i] * W, estrellas[i + 1] * H, tam * 2.2f, new Color4(0.85f, 0.9f, 1f, alfa * a * Math.Min(1f, 0.35f + tam / (2.5f * k))));
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
                d.Degradado(x0, y0, w, (float)alto * 0.78f, new Color4(r, g, b, 0f), new Color4(r, g, b, fuerza * alfa));
                d.Degradado(x0, y0 + (float)alto * 0.78f, w, (float)alto * 0.3f, new Color4(r, g, b, fuerza * alfa), new Color4(r, g, b, 0f));
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
            zCreditos = new Zona(W - wcr - 24 * k, 8 * k, wcr + 8 * k, 48 * k);
            Casillas(c);
            media.Escribe(d, cr, W - wcr, 19 * k, m.Creditos > 0 ? Verde : Dorado);
            if (m.EnCola > 0) { string q = "En la cola: " + m.EnCola; float sq = Salto(c); chica.Escribe(d, q, W - wcr - 40 * k - chica.Mide(q), 24 * k - sq, sq > 0 ? Dorado : Gris); }
            zCola = new Zona(W - wcr - 40 * k - chica.Mide("En la cola: 00"), 16 * k, chica.Mide("En la cola: 00"), 32 * k);
            float abajo = H - 120 * k;   // donde empieza lo que suena
            if (c.AsistentePaso >= 0) { Asistente(c, W, H); d.Termina(); sup.Presenta(); return; }
            if (c.Atraccion && c.AtraccionActual != null) { Atraccion(c, W, H); d.Termina(); sup.Presenta(); return; }
            if (c.AjusteSel >= 0) { Ajustes(c, W, H); AvisoEncima(c, W * 1.45f, 130 * k); d.Termina(); sup.Presenta(); return; }
            // EL REPOSO (muro_reposo= y estado_expandido= de XP): la pared se aparta (el cielo vuelve encima) y lo que suena crece.
            double tr = reloj.Elapsed.TotalSeconds, dtr = Math.Max(0, Math.Min(0.1, tr - reposoT)); reposoT = tr;
            reposoAlfa = Math.Max(0f, Math.Min(1f, reposoAlfa + (float)(c.Reposo ? dtr / 0.6 : -dtr / 0.35)));
            float e = reposoAlfa * reposoAlfa * (3 - 2 * reposoAlfa);
            if (c.Buscando) Buscador(c, W, abajo);
            else if (e < 1f) { if (c.dentro == null) Pared(c, W, abajo); else Disco(c, W, abajo); }
            if (e > 0f && e < 1f) Cielo(W, H, reloj.Elapsed.TotalSeconds, e);
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
        private void Pared(Controlador c, float W, float abajo)
        {
            Form1 m = c.motor;
            double ahora = reloj.Elapsed.TotalMilliseconds;
            // LAS LETRAS (con lupa, como XP: la elegida y sus vecinas mas grandes, y una rayita debajo dice cuanta musica tiene cada una)
            float x = 24 * k, y = 76 * k;
            zLetras = new Zona(x - 4 * k, y - 8 * k, 0, 56 * k);
            int maxDiscos = 1; if (m.LetrasLupa) for (int i = 0; i < c.letras.Count; i++) maxDiscos = Math.Max(maxDiscos, c.DiscosEn(i));
            for (int i = 0; i < c.letras.Count; i++)
            {
                int dist = Math.Abs(i - c.letra);
                bool lupa = m.LetrasLupa && dist == 0, vecina = m.LetrasLupa && dist == 1;
                Letra f = lupa ? grande : media;
                string l = Controlador.NombreLetra(c.letras[i]); float w = Math.Max((lupa ? 46 : vecina ? 40 : 36) * k, f.Mide(l) + 18 * k);
                if (x + w > W - 24 * k) break;
                float yy = lupa ? y - 6 * k : y, hh = lupa ? 50 * k : 40 * k;
                if (i == c.letra) d.Redondo(x, yy, w, hh, 8 * k, Dorado);
                f.EscribeCentrado(d, l, x + w / 2, yy + (hh - f.Tam) / 2 - 2 * k, i == c.letra ? Fondo : vecina ? Blanco : Gris);
                if (m.LetrasLupa)
                {
                    float fr = Math.Max(0.08f, c.DiscosEn(i) / (float)maxDiscos);
                    d.Rect(x + 6 * k, y + 46 * k, (w - 12 * k) * fr, 3 * k, (i == c.letra ? Dorado : Gris).ConAlfa(i == c.letra ? 0.95f : 0.45f));
                }
                x += w + 4 * k;
            }
            zLetras.W = x - zLetras.X;
            List<Form1.Disco> ds = c.DiscosDeLetra();
            if (ds.Count == 0) { media.EscribeCentrado(d, "No hay música en la carpeta de la rocola", W / 2, 300 * k, Gris); return; }
            int cols = 4, filas = 2, porPag = Controlador.DiscosPorPagina, pag = c.cursorDisco / porPag;
            int paginas = (ds.Count + porPag - 1) / porPag;
            float top = 130 * k, alto = abajo - top - 10 * k, celW = (W - 48 * k) / cols, celH = alto / filas;
            float lado = Math.Min(celW - 24 * k, celH - 70 * k);
            // PASAR DE PAGINA DESLIZANDO (pagina_desliza= de XP): la vieja sale por un lado mientras la nueva entra por el otro, columna a
            // columna. Solo dentro de la misma letra.
            if (pagVista != pag || pagLetraVista != c.letra)
            {
                if (m.PaginaDesliza && pagVista >= 0 && pagLetraVista == c.letra && pagVista < paginas)
                {
                    pagAnt = pagVista; pagDesde = ahora;
                    pagDir = c.SentidoPagina >= 0 ? 1f : -1f;   // el de la flecha que se apreto (con dos paginas, ir y volver es la misma)
                }
                else pagAnt = -1;
                pagVista = pag; pagLetraVista = c.letra;
            }
            bool desliza = pagAnt >= 0 && ahora - pagDesde < PaginaMs + ColumnaMs * (cols - 1);
            if (!desliza) pagAnt = -1;
            if (desliza) Pagina(c, ds, pagAnt, false, top, celW, celH, lado, cols, porPag, col => -pagDir * W * CurvaPagina(ahora - pagDesde, col, cols));
            Pagina(c, ds, pag, true, top, celW, celH, lado, cols, porPag, col => desliza ? pagDir * W * (1f - CurvaPagina(ahora - pagDesde, col, cols)) : 0f);
            if (!desliza && m.FichaDisco && !c.Reposo && c.letras[c.letra] != Form1.LetraTop && c.cursorDisco < ds.Count)
                FichaRapida(c, ds[c.cursorDisco], ahora - focoDesde, new Zona(24 * k, top, W - 48 * k, alto), celW, celH);
            if (paginas > 1) chica.Escribe(d, "Página " + (pag + 1) + " de " + paginas, 24 * k, abajo - 26 * k, Gris.ConAlfa(0.8f));
            string ayuda = "IZQ/DER: disco   ARRIBA/ABAJO: página   REGRESAR + IZQ/DER: letra   REGRESAR + ARRIBA: buscar";
            chica.Escribe(d, ayuda, W - 24 * k - chica.Mide(ayuda), abajo - 26 * k, Gris.ConAlfa(0.8f));
        }
        // Cuanto ha avanzado una columna (0 a 1, frenando al llegar); las columnas salen una tras otra en el sentido del paso.
        private float CurvaPagina(double t, int col, int cols)
        {
            int orden = pagDir > 0 ? col : cols - 1 - col;
            double u = Math.Max(0, Math.Min(1, (t - orden * ColumnaMs) / PaginaMs));
            return (float)(1 - Math.Pow(1 - u, 3));
        }

        // Una pagina de la pared, corrida 'dx(columna)'. 'viva': la que se elige (portada enfocada, cubo, relevo y lo que senala la ayuda).
        private void Pagina(Controlador c, List<Form1.Disco> ds, int pag, bool viva, float top, float celW, float celH, float lado, int cols, int porPag, Func<int, float> dx)
        {
            for (int i = 0; i < porPag; i++)
            {
                int n = pag * porPag + i; if (n >= ds.Count) break;
                Form1.Disco disco = ds[n];
                float cx = 24 * k + (i % cols) * celW + celW / 2 + dx(i % cols), cy = top + (i / cols) * celH;
                bool sel = viva && n == c.cursorDisco;
                // LA PORTADA ENFOCADA (como XP): crece un 12 % en 170 ms desde el centro de su borde de abajo, con un halo de su propio
                // color; las demas bajan un 20 % de brillo.
                if (sel && (focoDisco != n || focoCarpeta != disco.Carpeta))
                {
                    // EL RELEVO DE GIRO (AlbumView.MoveSelectionTo de XP): el que se deja da un giro rapido que se apaga y el nuevo arranca plano
                    bool mismaPagina = focoCarpeta != null && focoDisco >= 0 && focoDisco / porPag == pag && relevoLetra == c.letra;
                    if (mismaPagina) { relevoIdx = focoDisco; relevoAng = AnguloCubo(); relevoGiro = 0.7f * (n > focoDisco ? 1f : -1f); } else relevoIdx = -1;
                    giroDir = n >= focoDisco ? 1f : -1f; relevoLetra = c.letra;
                    focoDisco = n; focoCarpeta = disco.Carpeta; focoDesde = reloj.Elapsed.TotalMilliseconds;
                }
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
                if (sel) Cubo(disco.Portada, disco.Artista, px, py, l, AnguloCubo());                       // el cubo que gira (XP)
                else if (viva && n == relevoIdx) Cubo(disco.Portada, disco.Artista, px, py, l, PasoRelevo());
                else Portada(disco.Portada, disco.Artista, px, py, l);
                if (sel) zElegido = new Zona(px - 6 * k, py - 6 * k, l + 12 * k, l + 12 * k);
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
            media.Escribe(d, media.Ajusta(disco.Artista, ancho), tx0, ty, Blanco.ConAlfa(alfa)); ty += 26 * u;
            if (!string.IsNullOrEmpty(disco.Nombre)) { chica.Escribe(d, chica.Ajusta(disco.Nombre, ancho), tx0, ty, Color4.Hex(0xC8D4F5, alfa)); ty += 21 * u; }
            string resumen = disco.Videos ? disco.Rutas.Count + " videos" : disco.Rutas.Count + (disco.Rutas.Count == 1 ? " canción" : " canciones") + (disco.Minutos > 0 ? " · " + disco.Minutos + " min" : "");
            chica.Escribe(d, resumen, tx0, ty, Color4.Hex(0xA0B2DE, alfa)); ty += 22 * u;
            if (f.EnCola > 0)
            {
                string q = f.EnCola + " en la cola"; float wq = chica.Mide(q) + 18 * k;
                d.Redondo(tx0, ty, wq, 22 * k, 11 * k, Verde.ConAlfa(0.22f * alfa));
                chica.Escribe(d, q, tx0 + 9 * k, ty + 1 * k, Verde.ConAlfa(alfa)); ty += 24 * k;
            }
            float yl = Math.Max(fy + pad + lado, ty) + 9 * u;
            d.Rect(fx + pad, yl, w - 2 * pad, 1, Color4.Hex(0xA0B4E6, 0.25f * alfa));
            chica.Escribe(d, f.Pedidas ? "LAS MÁS PEDIDAS DE ESTE DISCO" : "ALGUNAS DE ESTE DISCO", fx + pad, yl + 4 * u, Color4.Hex(0x96AADC, alfa));
            float yb = fy + h - pad - 20 * u, yf = yl + 26 * u, altoF = 26 * u;
            for (int i = 0; i < f.Titulos.Count; i++)
            {
                float yk = yf + i * altoF; if (yk + altoF > yb) break;
                float r = 10 * u; Circulo(fx + pad + r, yk + r, r, Verde.ConAlfa(0.25f * alfa));
                chica.EscribeCentrado(d, (i + 1).ToString(), fx + pad + r, yk + r - chica.Tam * 0.55f, Verde.ConAlfa(alfa));
                float xt = fx + pad + 2 * r + 9 * u, der = fx + w - pad;
                if (f.Pedidas && f.Veces[i] > 0) { string vt = f.Veces[i] == 1 ? "1 vez" : f.Veces[i] + " veces"; float vw = chica.Mide(vt); chica.Escribe(d, vt, der - vw, yk, Color4.Hex(0xFFB84D, alfa)); der -= vw + 8 * u; }
                string du = f.Duraciones[i];
                if (du.Length > 0 && der - chica.Mide(du) - 8 * u - xt >= (w - 2 * pad) * .5f) { float dw = chica.Mide(du); chica.Escribe(d, du, der - dw, yk, Color4.Hex(0xA0B0DA, alfa)); der -= dw + 8 * u; }
                chica.Escribe(d, chica.Ajusta(f.Titulos[i], Math.Max(10, der - xt)), xt, yk, Blanco.ConAlfa(alfa));
            }
            float rb = 9 * u; Circulo(fx + pad + rb, yb + rb + 2 * u, rb, Color4.Hex(0x48D470, alfa));
            chica.Escribe(d, disco.Rutas.Count > 0 ? "Entrar para ver las " + disco.Rutas.Count : "Entrar al disco", fx + pad + 2 * rb + 8 * u, yb, Color4.Hex(0xDFE6FF, alfa));
        }

        private void Disco(Controlador c, float W, float abajo)
        {
            Form1.Disco disco = c.dentro;
            float lado = Math.Min(360 * k, abajo - 110 * k);
            Portada(disco.Portada, disco.Artista, 32 * k, 90 * k, lado);
            float x = 32 * k + lado + 32 * k, ancho = W - x - 32 * k;
            grande.Escribe(d, grande.Ajusta(disco.Artista, ancho), x, 84 * k, Blanco);
            if (!string.IsNullOrEmpty(disco.Nombre)) media.Escribe(d, media.Ajusta(disco.Nombre, ancho), x, 124 * k, Dorado);
            // LA LISTA (DiscoVivoGpu.cs de XP): la grande lleva filas mas altas y letra mas grande; la que suena, su pastilla SONANDO con
            // cuatro barritas que se mueven con la musica; las que van en la cola, "EN COLA 3 · en ~9 min".
            Form1 m = c.motor;
            bool gr = m.ListaGrande; Letra lt = gr ? grande : media;
            float top = 168 * k, fila = gr ? 50 * k : 38 * k;
            int caben = Math.Max(1, (int)((abajo - top - 10 * k) / fila));
            c.PorPagina = caben;
            string suena = m.Sonando;
            Dictionary<string, int> enCola = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); Dictionary<string, int> minutos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<Form1.EnColaVista> cola = m.ColaParaPantalla(64);
            for (int q = 0; q < cola.Count; q++) if (cola[q].Ruta != null && !enCola.ContainsKey(cola[q].Ruta)) { enCola[cola[q].Ruta] = q + 1; minutos[cola[q].Ruta] = cola[q].Minutos; }
            int n = disco.Rutas.Count, desde = Math.Max(0, Math.Min(c.cursorCancion - caben / 2, n - caben));
            for (int i = 0; i < caben && desde + i < n; i++)
            {
                int j = desde + i; float y = top + i * fila;
                bool sel = j == c.cursorCancion;
                if (sel) { d.Redondo(x - 10 * k, y - 2 * k, ancho + 20 * k, fila - 4 * k, 8 * k, PanelClaro); zCancion = new Zona(x - 10 * k, y - 2 * k, ancho + 20 * k, fila - 4 * k); }
                chica.Escribe(d, (j + 1).ToString().PadLeft(2), x, y + (fila - 4 * k - chica.Tam) / 2, sel ? Dorado : Gris);
                // a la derecha, su pastilla: SONANDO o EN COLA
                string ruta = disco.Rutas[j], pastilla = null; Color4 cp = Verde; bool estaSonando = suena != null && string.Equals(suena, ruta, StringComparison.OrdinalIgnoreCase);
                int puesto;
                if (estaSonando) pastilla = "SONANDO";
                else if (enCola.TryGetValue(ruta, out puesto)) { pastilla = m.ColaMinutos ? Form1.TextoCola(puesto, minutos[ruta]) : "EN COLA " + puesto; cp = Dorado; }
                float der = x + ancho;
                if (pastilla != null)
                {
                    float barras = estaSonando && m.EqSonando ? 30 * k : 0;
                    float wp = chica.Mide(pastilla) + 20 * k + barras, hp = 24 * k, yp = y + (fila - 4 * k - hp) / 2;
                    d.Redondo(der - wp, yp, wp, hp, hp / 2, cp.ConAlfa(0.18f));
                    chica.Escribe(d, pastilla, der - wp + 10 * k + barras, yp + 2 * k, cp);
                    if (barras > 0) Barritas(der - wp + 10 * k, yp + 4 * k, 20 * k, hp - 8 * k, cp);
                    der -= wp + 10 * k;
                }
                lt.Escribe(d, lt.Ajusta(disco.Titulos[j], der - x - 44 * k), x + 44 * k, y + (fila - 4 * k - lt.Tam) / 2 - 2 * k, sel ? Blanco : Gris);
            }
            chica.Escribe(d, "ARRIBA/ABAJO: canción   IZQ/DER: página   PONER: pedirla (1 crédito)   REGRESAR: a los discos", x, abajo - 28 * k, Gris.ConAlfa(0.8f));
        }

        private float reposoAlfa; private double reposoT;

        // EL ESTADO EXTENDIDO (EstadoReposoGpu.cs y SuenaVivoGpu.cs de XP, en lo esencial): la portada grande abajo a la izquierda, el
        // titulo, el artista, el genero y el reloj con su barra; a la derecha, la cola con sus portadas y en cuanto suena cada una (doce
        // en dos columnas con cola_doce=, si no tres).
        private void Extendido(Controlador c, float W, float H, float top)
        {
            Form1 m = c.motor;
            float ph = H - top;
            d.Degradado(0, top - 30 * k, W, 30 * k, Panel.ConAlfa(0f), Panel.ConAlfa(0.88f));
            d.Rect(0, top, W, ph, Panel.ConAlfa(0.88f));
            string t = m.SonandoTitulo; if (t == null) return;
            float lado = Math.Max(32 * k, Math.Min(ph - 40 * k, W * 0.22f));
            float cy = top + (ph - lado) / 2;
            Textura tx = caratulas.De(m.SonandoPortada);
            d.Brillo(24 * k + lado / 2, cy + lado / 2, lado * 0.9f, (tx != null ? tx.Media : Dorado).ConAlfa(0.35f + 0.25f * pGraves));   // el halo late
            Portada(m.SonandoPortada, m.SonandoDisco ?? t, 24 * k, cy, lado);
            float x = 24 * k + lado + 28 * k;
            List<Form1.EnColaVista> cola = m.ColaParaPantalla(m.ColaDoce ? 12 : 3);
            float wc = cola.Count == 0 ? 0 : cola.Count > 6 ? W * 0.44f : W * 0.30f;   // doce: dos columnas de seis; hasta seis: una
            float ancho = W - x - wc - (wc > 0 ? 40 * k : 24 * k);
            chica.Escribe(d, "SUENA AHORA", x, top + 18 * k, Dorado);
            enorme.Escribe(d, enorme.Ajusta(t, ancho), x, top + 40 * k, Blanco);
            float y = top + 40 * k + enorme.Tam * 1.25f;
            if (m.SonandoDisco != null) { media.Escribe(d, media.Ajusta(m.SonandoDisco, ancho), x, y, Gris); y += media.Tam * 1.5f; }
            string gen = m.SonandoGenero;
            if (!string.IsNullOrEmpty(gen))
            {
                float wg = chica.Mide(gen) + 24 * k;
                d.Redondo(x, y, wg, 28 * k, 14 * k, Dorado.ConAlfa(0.22f));
                chica.Escribe(d, gen, x + 12 * k, y + 4 * k, Dorado);
            }
            // el reloj y la barra, abajo
            string reloj_ = m.SonandoTiempo;
            float by = top + ph - 26 * k;
            float wr = media.Mide(reloj_) + 16 * k;
            media.Escribe(d, reloj_, x, by - media.Tam * 0.62f, Blanco);
            float bx = x + wr, bw = Math.Max(20 * k, x + ancho - bx);
            d.Redondo(bx, by - 3 * k, bw, 6 * k, 3 * k, PanelClaro);
            d.Redondo(bx, by - 3 * k, Math.Max(6 * k, bw * c.Progreso), 6 * k, 3 * k, Dorado);
            if (cola.Count == 0) return;
            // la cola
            float xc = W - wc - 16 * k;
            chica.Escribe(d, "EN COLA (" + m.EnCola + ")", xc, top + 18 * k, Dorado);
            int porCol = cola.Count > 3 ? 6 : 3, cols = cola.Count > 6 ? 2 : 1;
            float fy = top + 46 * k, fh = (ph - 56 * k) / porCol, cw = wc / cols;
            for (int i = 0; i < cola.Count; i++)
            {
                Form1.EnColaVista q = cola[i];
                float qx = xc + (i / porCol) * cw, qy = fy + (i % porCol) * fh, ql = Math.Min(fh - 6 * k, 56 * k);
                Portada(q.Portada, q.Disco ?? q.Titulo, qx, qy, ql);
                Letra lt = fh > 50 * k ? media : chica;
                lt.Escribe(d, lt.Ajusta(q.Titulo, cw - ql - 20 * k), qx + ql + 10 * k, qy + (fh > 50 * k ? 2 * k : 0), Blanco);
                if (fh > 40 * k) chica.Escribe(d, chica.Ajusta(Form1.TextoCola(i + 1, q.Minutos), cw - ql - 20 * k), qx + ql + 10 * k, qy + lt.Tam * 1.3f, Gris);
            }
        }

        private void Suena(Controlador c, float W, float H, float abajo)
        {
            Form1 m = c.motor;
            d.Rect(0, abajo, W, H - abajo, Panel.ConAlfa(0.88f));
            float lado = H - abajo - 24 * k;
            string t = m.SonandoTitulo;
            if (t == null) { zSuena = new Zona(12 * k, abajo + 12 * k, H - abajo - 24 * k, H - abajo - 24 * k); media.Escribe(d, "Elige un disco y pon tu canción", 24 * k, abajo + 40 * k, Gris); return; }
            zSuena = new Zona(12 * k, abajo + 12 * k, lado, lado);
            Cubo(m.SonandoPortada, m.SonandoDisco ?? t, 12 * k, abajo + 12 * k, lado, reloj.Elapsed.TotalSeconds * 1.44);   // el cubo chico de la barra (XP)
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
