#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Rocola
{
    // LA ESTRELLA TE LLEVA DE LA MANO (peticion del 11-10-2026).
    //
    // Nada de leer tres pasos y apanarse: la guia mira lo que esta pasando DE VERDAD en la Rocola cada latido
    // (donde estas, si tienes credito, si abriste el buscador, si ya pediste la cancion) y decide el paso que
    // toca AHORA. La estrella se pone en un globo pequeno, apartado de lo que tienes que mirar, senala con la mano
    // y con una flecha dorada la pieza de la pantalla que toca usar, la ilumina, y dibuja los botones que hay que
    // apretar. El cliente los aprieta: los botones hacen lo de siempre. Si se equivoca o vuelve atras, la guia
    // vuelve al paso que corresponde sin regañar; si acierta, la estrella lo celebra y pasa al siguiente.
    // Cuando se logra lo que pidio, lo festeja y se vuelve a guardar sola en el cielo.
    public partial class Form1
    {
        internal const int MisionCancion = 1, MisionBuscar = 2, MisionAprender = 3;
        internal const int PasoDisco = 1, PasoMoneda = 2, PasoCancion = 3, PasoAbreBuscador = 4, PasoEscribe = 5,
            PasoResultado = 6, PasoMover = 7, PasoPagina = 8, PasoLetra = 9, PasoEntrar = 10, PasoVolver = 11,
            PasoCierraBuscador = 12;
        internal const int GuiaLogroMs = 2600, GuiaBienMs = 1300;
        private static readonly int[] PasosAprender = { PasoMover, PasoPagina, PasoLetra, PasoEntrar, PasoVolver };

        private int asistenteMision, asistentePaso, asistenteLogroTick, asistenteBienTick, asistenteMonedaTick;
        private int asistenteAprende;                      // por cual de PasosAprender va
        private int asistentePedidos, asistentePedidosBase; // canciones/videos puestos (sube en AddItem)
        private int guiaSelBase, guiaPagBase; private char guiaLetraBase;

        // Una cancion o un video quedo en la cola: lo apunta AddItem, por donde pasa todo pedido (desde un disco,
        // un video o el buscador).
        private void NotaPedidoAsistente() { asistentePedidos = unchecked(asistentePedidos + 1); }

        private void EmpiezaMision(int mision)
        {
            if (mision < MisionCancion || mision > MisionAprender) return;
            asistenteMision = mision; asistentePaso = 0; asistenteLogroTick = 0; asistenteBienTick = 0;
            asistenteAprende = 0; asistentePedidosBase = asistentePedidos;
            BasesGuia();
            AnimaAsistente(AsistenteGestoGpu.Explicar);
            // El cuadro del video sigue oculto mientras guia (el sonido no): asi no tapa el globo ni la flecha.
            // Vuelve solo al guardarse la ayuda (CierraAsistente).
            EvaluaGuia();
        }

        private void BasesGuia() { guiaSelBase = selAlbumIndex; guiaPagBase = page; guiaLetraBase = currentLetter; }

        private bool EnDisco { get { return panelSongs != null && panelSongs.Visible; } }
        private bool EnResultados
        {
            get
            {
                return searchOpen && searchOverlay != null && searchOverlay.FocusResults
                    && searchOverlay.Results != null && searchOverlay.Results.Count > 0;
            }
        }

        // El paso que toca para poner una cancion, mirando solo el estado real. Pura: se prueba sin ventana.
        internal static int PasoParaCancion(int mision, bool buscadorAbierto, bool enResultados, bool enDisco, int creditos)
        {
            if (buscadorAbierto) return !enResultados ? PasoEscribe : creditos <= 0 ? PasoMoneda : PasoResultado;
            if (enDisco) return creditos <= 0 ? PasoMoneda : PasoCancion;
            return mision == MisionBuscar ? PasoAbreBuscador : PasoDisco;
        }

        // En que orden van los pasos de cada mision (para decir "paso 2 de 3" y saber si se avanzo).
        internal static int OrdenPaso(int mision, int paso)
        {
            if (mision == MisionCancion) return paso == PasoDisco ? 0 : paso == PasoMoneda ? 1 : paso == PasoCancion ? 2 : -1;
            if (mision == MisionBuscar)
                return paso == PasoAbreBuscador ? 0 : paso == PasoEscribe ? 1 : paso == PasoMoneda ? 2 : paso == PasoResultado ? 3 : -1;
            for (int i = 0; i < PasosAprender.Length; i++) if (PasosAprender[i] == paso) return i;
            return -1;
        }

        // "Paso 2 de 3": la moneda solo cuenta si hace falta. Devuelve 0 si el paso no es de la mision.
        internal static int NumeroPaso(int mision, int paso, bool conCredito, out int total)
        {
            int orden = OrdenPaso(mision, paso);
            if (mision == MisionAprender) { total = PasosAprender.Length; return orden + 1; }
            int moneda = OrdenPaso(mision, PasoMoneda);
            total = (mision == MisionCancion ? 3 : 4) - (conCredito ? 1 : 0);
            if (orden < 0) return 0;
            if (conCredito && orden == moneda) return 0;
            return orden + 1 - (conCredito && orden > moneda ? 1 : 0);
        }

        private void EvaluaGuia()
        {
            if (asistenteLogroTick != 0)
            {
                if (unchecked(Environment.TickCount - asistenteLogroTick) >= GuiaLogroMs) CierraAsistente();   // se guarda sola
                return;
            }
            if (asistenteMision == MisionCancion || asistenteMision == MisionBuscar)
            {
                if (asistentePedidos != asistentePedidosBase) { Logro(); return; }
                PonPaso(PasoParaCancion(asistenteMision, searchOpen, EnResultados, EnDisco, credits));
                return;
            }
            // Aprender a moverse: cinco pasos seguidos; cada uno se da por hecho al verlo pasar.
            int meta = PasosAprender[Math.Max(0, Math.Min(PasosAprender.Length - 1, asistenteAprende))];
            bool hecho = false;
            if (!searchOpen)
            {
                if (meta == PasoMover) hecho = !EnDisco && (selAlbumIndex != guiaSelBase || page != guiaPagBase || currentLetter != guiaLetraBase);
                else if (meta == PasoPagina) hecho = !EnDisco && (page != guiaPagBase || currentLetter != guiaLetraBase);
                else if (meta == PasoLetra) hecho = !EnDisco && currentLetter != guiaLetraBase;
                else if (meta == PasoEntrar) hecho = EnDisco;
                else if (meta == PasoVolver) hecho = !EnDisco;
            }
            if (hecho)
            {
                if (asistenteAprende >= PasosAprender.Length - 1) { Logro(); return; }
                asistenteAprende++; BasesGuia();
                meta = PasosAprender[asistenteAprende];
            }
            // Si se fue a otro sitio, primero le ayuda a volver; sin perder lo ya aprendido.
            int muestra = meta;
            if (searchOpen) muestra = PasoCierraBuscador;
            else if (EnDisco && meta != PasoVolver) muestra = PasoVolver;
            PonPaso(muestra);
        }

        private void PonPaso(int paso)
        {
            if (paso == asistentePaso) return;
            int antes = OrdenPaso(asistenteMision, asistentePaso), ahora = OrdenPaso(asistenteMision, paso);
            bool avanza = asistentePaso != 0 && antes >= 0 && ahora > antes;
            asistentePaso = paso;
            if (avanza)
            {
                asistenteBienTick = Environment.TickCount; if (asistenteBienTick == 0) asistenteBienTick = 1;
                // Si acaba de celebrar una moneda, la deja terminar en vez de cortarla con otro gesto.
                if (asistenteMonedaTick == 0 || unchecked(Environment.TickCount - asistenteMonedaTick) > 1200) AnimaAsistente(AsistenteGestoGpu.Confirmar);
            }
            else { asistenteBienTick = 0; AnimaAsistente(paso == PasoMoneda ? AsistenteGestoGpu.Buscar : AsistenteGestoGpu.Senalar); }
            asistenteActividadTick = Environment.TickCount;
        }

        private void Logro()
        {
            asistenteLogroTick = Environment.TickCount; if (asistenteLogroTick == 0) asistenteLogroTick = 1;
            asistenteBienTick = 0;
            AnimaAsistente(AsistenteGestoGpu.Credito);   // celebra con los dos brazos y un saltito
        }

        private bool GuiaBienAhora { get { return asistenteBienTick != 0 && unchecked(Environment.TickCount - asistenteBienTick) < GuiaBienMs; } }

        // Lo que cambia el dibujo del globo: paso, "¡muy bien!", credito (cambia "paso X de N") y logro.
        private int ClaveGuia()
        {
            return asistentePaso * 16 + (GuiaBienAhora ? 1 : 0) + (credits > 0 ? 2 : 0) + (asistenteLogroTick != 0 ? 4 : 0)
                + Math.Min(7, asistenteAprende) * 256;
        }

        // Lo que hay que senalar en la pantalla para el paso de ahora (coordenadas del formulario).
        private RectangleF ObjetivoGuia(int paso)
        {
            switch (paso)
            {
                case PasoDisco: case PasoMover: case PasoEntrar: return DiscoElegidoRect();
                case PasoPagina: return headerView != null && headerView.Visible && headerView.Width > 40 ? (RectangleF)headerView.Bounds : DiscoElegidoRect();
                case PasoLetra: return letterBar != null && letterBar.Visible ? (RectangleF)letterBar.Bounds : RectangleF.Empty;
                case PasoMoneda: return pastCred != null && pastCred.Visible ? (RectangleF)pastCred.Bounds : RectangleF.Empty;
                case PasoCancion: return CancionElegidaRect();
                case PasoAbreBuscador: return buscaView != null && buscaView.Visible ? (RectangleF)buscaView.Bounds : RectangleF.Empty;
                case PasoEscribe: return ZonaBuscadorGuia(1);
                case PasoResultado: return ZonaBuscadorGuia(2);
                case PasoCierraBuscador: return ZonaBuscadorGuia(0);
                case PasoVolver: return CancionElegidaRect();
            }
            return RectangleF.Empty;
        }

        private RectangleF DiscoElegidoRect()
        {
            if (albumView == null || !albumView.Visible) return RectangleF.Empty;
            Rectangle r = albumView.ContourScreenRect();
            if (r.Width <= 0) return RectangleF.Empty;
            r.Offset(albumView.Left, albumView.Top);
            return r;
        }

        private RectangleF CancionElegidaRect()
        {
            if (songList == null || panelSongs == null || !panelSongs.Visible) return RectangleF.Empty;
            int fila = songList.Sel - songList.PageStart;
            if (fila < 0 || fila >= songList.RowsPerPage) return new RectangleF(panelSongs.Left + songList.Left, panelSongs.Top + songList.Top, songList.Width, songList.Height);
            return new RectangleF(panelSongs.Left + songList.Left, panelSongs.Top + songList.Top + fila * songList.RowH, songList.Width, songList.RowH);
        }

        // 0 = la casilla de lo escrito, 1 = el teclado, 2 = el resultado elegido.
        private RectangleF ZonaBuscadorGuia(int que)
        {
            if (searchOverlay == null || !searchOpen) return RectangleF.Empty;
            Rectangle r = searchOverlay.ZonaGuia(que);
            if (r.Width <= 0) return RectangleF.Empty;
            r.Offset(searchOverlay.Left, searchOverlay.Top);
            return r;
        }

        private partial class SearchOverlay
        {
            internal Rectangle ZonaGuia(int que)
            {
                if (Nuevo)
                {
                    RepartoBusca r = AseguraReparto();
                    if (que == 0) return r.Caja;
                    if (que == 1) return r.Teclado;
                    Rectangle c = FocusResults ? CartaRect(SelResult) : Rectangle.Empty;
                    return c.Width > 0 ? c : r.Resultados;
                }
                Rectangle t = RegionTeclado();
                if (que == 0) return CajaEscrito();
                if (que == 1) return t;
                return new Rectangle(t.Right + 16, t.Top, Math.Max(1, Width - t.Right - 46), t.Height);
            }
        }

        // ================== LO QUE DICE EL GLOBO ==================
        // Titulo corto, una frase y LOS BOTONES DIBUJADOS: B rojo, V verde, M moneda, U/D flechas rojas (arriba,
        // abajo), L/R flechas amarillas (izquierda, derecha), '+' a la vez, '/' o, ' ' separacion.
        internal static void TextosGuia(int paso, out string titulo, out string texto, out string teclas)
        {
            switch (paso)
            {
                case PasoDisco: titulo = "Elige un disco"; texto = "Muévete con las flechas. Cuando veas uno que te guste, pulsa VERDE."; teclas = "L/R U/D V"; break;
                case PasoMoneda: titulo = "Mete una moneda"; texto = "Cada canción cuesta 1 crédito. Inserta una moneda y seguimos."; teclas = "M"; break;
                case PasoCancion: titulo = "Elige tu canción"; texto = "Sube o baja con las flechas y pulsa VERDE para ponerla."; teclas = "U/D V"; break;
                case PasoAbreBuscador: titulo = "Abre el buscador"; texto = "Mantén el botón ROJO y pulsa la flecha de ARRIBA."; teclas = "B+U"; break;
                case PasoEscribe: titulo = "Escribe el nombre"; texto = "Elige cada letra con las flechas y VERDE (o escribe con el teclado). Luego ve con las flechas hasta tu canción."; teclas = "L/R U/D V"; break;
                case PasoResultado: titulo = "Elige tu canción"; texto = "Muévete hasta ella con las flechas y pulsa VERDE para ponerla."; teclas = "L/R U/D V"; break;
                case PasoMover: titulo = "Muévete entre los discos"; texto = "Pulsa la flecha IZQUIERDA o la DERECHA."; teclas = "L/R"; break;
                case PasoPagina: titulo = "Cambia de página"; texto = "Pulsa la flecha de ARRIBA o la de ABAJO para ver más discos."; teclas = "U/D"; break;
                case PasoLetra: titulo = "Salta a otra letra"; texto = "Mantén ROJO y pulsa IZQUIERDA o DERECHA: cambia la letra de arriba."; teclas = "B+L/R"; break;
                case PasoEntrar: titulo = "Entra en un disco"; texto = "Pulsa VERDE para ver sus canciones."; teclas = "V"; break;
                case PasoVolver: titulo = "Vuelve a los discos"; texto = "Pulsa el botón ROJO para volver a la pared."; teclas = "B"; break;
                case PasoCierraBuscador: titulo = "Cierra el buscador"; texto = "Mantén ROJO y pulsa la flecha de ARRIBA."; teclas = "B+U"; break;
                default: titulo = ""; texto = ""; teclas = ""; break;
            }
        }

        private Bitmap PintaGuiaActual(int w, int h)
        {
            string arriba, titulo, texto, teclas;
            if (asistenteLogroTick != 0)
            {
                arriba = "¡LO LOGRASTE!";
                titulo = asistenteMision == MisionAprender ? "¡Ya sabes moverte!" : "¡Tu canción está en la cola!";
                texto = asistenteMision == MisionAprender ? "Cuando quieras, pon una canción. Si me necesitas: ROJO + ABAJO."
                    : "Ya va en camino. Si me necesitas otra vez: ROJO + ABAJO.";
                teclas = "";
            }
            else
            {
                TextosGuia(asistentePaso, out titulo, out texto, out teclas);
                int total, numero = NumeroPaso(asistenteMision, asistenteMision == MisionAprender ? PasosAprender[Math.Min(asistenteAprende, PasosAprender.Length - 1)] : asistentePaso, credits > 0, out total);
                arriba = GuiaBienAhora ? "¡MUY BIEN! SIGUIENTE PASO"
                    : numero > 0 ? "PASO " + numero + " DE " + total : "SIGAMOS";
            }
            return PintaGuiaAsistente(w, h, arriba, titulo, texto, teclas, asistenteLogroTick != 0);
        }

        internal static Bitmap PintaGuiaAsistente(int w, int h, string arriba, string titulo, string texto, string teclas, bool logro)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font fArriba = new Font("Trebuchet MS", Math.Max(9, Math.Min(18, h * .085f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fTitulo = new Font("Trebuchet MS", Math.Max(13, Math.Min(34, h * .155f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fTexto = new Font("Trebuchet MS", Math.Max(10, Math.Min(20, h * .088f)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font fPie = new Font("Trebuchet MS", Math.Max(8, Math.Min(14, h * .065f)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush blanco = new SolidBrush(Color.FromArgb(246, 248, 255)))
            using (SolidBrush tenue = new SolidBrush(Color.FromArgb(198, 214, 238)))
            using (SolidBrush oro = new SolidBrush(Color.FromArgb(255, 214, 102)))
            using (SolidBrush azul = new SolidBrush(Color.FromArgb(150, 215, 255)))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (GraphicsPath borde = AsistenteGloboCamino(w, h))
                using (LinearGradientBrush vidrio = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(250, 26, 44, 78), Color.FromArgb(246, 10, 18, 37), 90f))
                using (Pen halo = new Pen(logro ? Color.FromArgb(70, 255, 214, 102) : Color.FromArgb(40, 101, 206, 255), 5f))
                using (Pen brillo = new Pen(logro ? Color.FromArgb(240, 255, 214, 102) : Color.FromArgb(220, 142, 215, 255), 1.8f))
                { g.DrawPath(halo, borde); g.FillPath(vidrio, borde); g.DrawPath(brillo, borde); }
                int x = AsistenteGloboMargen(w), tw = Math.Max(1, w - x - Math.Max(10, w / 24));
                g.DrawString(arriba ?? "", fArriba, oro, new RectangleF(x, h * .06f, tw, h * .11f));
                g.DrawString(titulo ?? "", fTitulo, blanco, new RectangleF(x, h * .17f, tw, h * .22f));
                bool conTeclas = !string.IsNullOrEmpty(teclas);
                g.DrawString(texto ?? "", fTexto, tenue, new RectangleF(x, h * .39f, tw, conTeclas ? h * .29f : h * .44f));
                if (conTeclas) PintaTeclas(g, teclas, x, (int)(h * .775f), Math.Max(14, (int)(h * .18f)), fTitulo);
                if (!logro)
                    using (StringFormat derecha = new StringFormat { Alignment = StringAlignment.Far })
                        g.DrawString("ROJO + ABAJO: guardar la ayuda", fPie, azul, new RectangleF(x, h * .885f, tw, h * .10f), derecha);
            }
            return bmp;
        }

        // Los botones, dibujados como son en la maquina. Devuelve donde acaba.
        internal static float PintaTeclas(Graphics g, string teclas, float x, int cy, int d, Font f)
        {
            Color rojo = Color.FromArgb(228, 72, 72), verde = Color.FromArgb(70, 200, 105), amarillo = Color.FromArgb(242, 206, 72);
            Color tinta = Color.FromArgb(235, 245, 255);
            using (Font chico = new Font(f.FontFamily, Math.Max(8f, d * .62f), FontStyle.Bold, GraphicsUnit.Pixel))
                foreach (char c in teclas)
                {
                    switch (c)
                    {
                        case 'B': x = CtrlDot(g, x, cy, d, rojo) + 6; break;
                        case 'V': x = CtrlDot(g, x, cy, d, verde) + 6; break;
                        case 'M':
                            using (SolidBrush m = new SolidBrush(Color.FromArgb(255, 205, 90))) g.FillEllipse(m, x, cy - d / 2f, d, d);
                            using (Pen p = new Pen(Color.FromArgb(160, 110, 30), Math.Max(1.5f, d * .08f))) g.DrawEllipse(p, x + d * .18f, cy - d * .32f, d * .64f, d * .64f);
                            x += d + 6; break;
                        case 'U': TeclaFlecha(g, x, cy, d, rojo, 0); x += d + 6; break;
                        case 'D': TeclaFlecha(g, x, cy, d, rojo, 1); x += d + 6; break;
                        case 'L': TeclaFlecha(g, x, cy, d, amarillo, 2); x += d + 6; break;
                        case 'R': TeclaFlecha(g, x, cy, d, amarillo, 3); x += d + 6; break;
                        case '+': x = CtrlTxt(g, chico, "+", x, cy, tinta) + 6; break;
                        case '/': x = CtrlTxt(g, chico, "o", x, cy, tinta) + 6; break;
                        case ' ': x += d * .9f; break;
                    }
                }
            return x;
        }

        // Una tecla redondeada con su flecha: 0 arriba, 1 abajo, 2 izquierda, 3 derecha.
        internal static void TeclaFlecha(Graphics g, float x, int cy, int d, Color c, int dir)
        {
            Rectangle k = new Rectangle((int)x, cy - d / 2, d, d);
            using (GraphicsPath gp = RoundedPath(k, Math.Max(3, d / 4)))
            {
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, c))) g.FillPath(sb, gp);
                using (Pen pn = new Pen(Color.FromArgb(215, c), 1.6f)) g.DrawPath(pn, gp);
            }
            float cx = x + d / 2f, t = d * 0.30f, an = d * 0.24f;
            if (dir == 0) CtrlTri(g, c, new PointF(cx, cy - t), new PointF(cx - an, cy + t * .62f), new PointF(cx + an, cy + t * .62f));
            else if (dir == 1) CtrlTri(g, c, new PointF(cx, cy + t), new PointF(cx - an, cy - t * .62f), new PointF(cx + an, cy - t * .62f));
            else if (dir == 2) CtrlTri(g, c, new PointF(cx - t, cy), new PointF(cx + t * .62f, cy - an), new PointF(cx + t * .62f, cy + an));
            else CtrlTri(g, c, new PointF(cx + t, cy), new PointF(cx - t * .62f, cy - an), new PointF(cx - t * .62f, cy + an));
        }
    }

    // LO QUE LA TARJETA DIBUJA DE LA GUIA: el globo pequeno en su esquina, la estrella a su lado senalando con la
    // mano, un velo suave con un hueco donde esta lo que hay que usar, un marco dorado que respira alrededor y una
    // flecha dorada que bota hacia alli. Todo son triangulos de colores en arreglos fijos: nada se reserva al
    // dibujar y la textura del globo solo cambia cuando cambia el paso.
    internal sealed partial class GpuPanel
    {
        private double asistenteUltimoAvance = -1;
        private AsistentePoseGpu asistenteSigue; private bool asistenteSigueListo;
        private RectangleF asistenteGuiaPanel; private bool asistenteGuiaListo, asistenteGuiaVisibleAntes;
        private Rectangle asistenteGuiaMeta;   // la esquina elegida (el globo se desliza hacia ella)
        private readonly D3D9.Vtx[] asistenteMarco = new D3D9.Vtx[72];
        private readonly D3D9.Vtx[] asistenteFlecha = new D3D9.Vtx[36];

        private void AvanzaGlobosGuia(double dt)
        {
            bool visible = asistenteEstado.Visible;
            if (visible && !asistenteGuiaVisibleAntes) asistenteGuiaListo = false;   // cada vez que se abre, desde cero
            asistenteGuiaVisibleAntes = visible;
            if (!visible) return;                                                    // regresando: el globo se queda donde estaba
            if (!asistenteEstado.Guia) { asistenteGuiaListo = false; return; }
            Rectangle meta = asistenteGuiaListo ? AsistenteGuiaRectSigue(Width, Height, asistenteEstado.Objetivo, asistenteGuiaMeta)
                                                : AsistenteGuiaRect(Width, Height, asistenteEstado.Objetivo);
            asistenteGuiaMeta = meta;
            if (!asistenteGuiaListo) { asistenteGuiaPanel = meta; asistenteGuiaListo = true; return; }
            float k = asistenteEstado.Animada ? 1 - (float)Math.Exp(-dt * 7) : 1;
            asistenteGuiaPanel = new RectangleF(asistenteGuiaPanel.X + (meta.X - asistenteGuiaPanel.X) * k,
                asistenteGuiaPanel.Y + (meta.Y - asistenteGuiaPanel.Y) * k, meta.Width, meta.Height);
        }

        private AsistentePoseGpu AsistentePoseFrente()
        {
            if (asistenteEstado.Guia && asistenteGuiaListo)
            {
                float r = AsistenteGuiaRadio((int)asistenteGuiaPanel.Height);
                return new AsistentePoseGpu { Centro = new PointF(asistenteGuiaPanel.Left - r * 1.40f - 6, asistenteGuiaPanel.Top + asistenteGuiaPanel.Height * .46f),
                    Radio = r, Pitch = .08, Miembros = 1, Alfa = 1 };
            }
            return new AsistentePoseGpu { Centro = AsistenteCentroFrente(Width, Height),
                Radio = AsistenteRadioFrente(Width, Height), Pitch = .08, Miembros = 1, Alfa = 1 };
        }

        private bool GuiaSenala
        {
            get
            {
                return asistenteEstado.Visible && asistenteEstado.Guia && !asistenteEstado.Logro
                    && asistenteEstado.Objetivo.Width > 0 && asistenteEstado.Objetivo.Height > 0;
            }
        }

        // La mano que queda del lado del objetivo se estira hacia el. Solo se cambia la copia para dibujar.
        private AsistenteExpresionGpu ExpresionGuia(float cx, float cy, float presencia)
        {
            AsistenteExpresionGpu e = asistenteExpresion;
            if (!GuiaSenala) return e;
            RectangleF o = asistenteEstado.Objetivo;
            float dx = o.X + o.Width * .5f - cx, dy = o.Y + o.Height * .5f - cy, l = (float)Math.Sqrt(dx * dx + dy * dy);
            if (!(l > 1f)) return e;
            dx /= l; dy /= l;
            float k = SafeMoonLevel(presencia);
            if (dx >= -.25f) { e.ManoDerX += (.98f + dx * .95f - e.ManoDerX) * k; e.ManoDerY += (.12f + dy * .95f - e.ManoDerY) * k; }
            else { e.ManoIzqX += (-.98f + dx * .95f - e.ManoIzqX) * k; e.ManoIzqY += (.18f + dy * .95f - e.ManoIzqY) * k; }
            return e;
        }

        // Velo suave con un hueco sobre lo que hay que usar, y un marco dorado que respira alrededor.
        private void DibujaFocoGuia(float presencia)
        {
            RectangleF o = GuiaSenala ? asistenteEstado.Objetivo : RectangleF.Empty;
            uint tinte = 0x00060B1Du;
            if (o.Width <= 0)
            {
                uint a0 = (uint)(presencia * 46);
                if (a0 == 0) return;
                SetTexQuadA(asistenteVelo, 0, 0, Width, Height, 1, 1, (a0 << 24) | tinte);
                stars.DrawColorQuad(asistenteVelo);
                return;
            }
            RectangleF h = RectangleF.Inflate(o, 8, 8);
            uint a = (uint)(presencia * 96);
            if (a > 0)
            {
                uint c = (a << 24) | tinte;
                float x0 = Math.Max(0, h.Left), x1 = Math.Min(Width, h.Right), y0 = Math.Max(0, h.Top), y1 = Math.Min(Height, h.Bottom);
                if (y0 > 0) { SetTexQuadA(asistenteVelo, 0, 0, Width, y0, 1, 1, c); stars.DrawColorQuad(asistenteVelo); }
                if (y1 < Height) { SetTexQuadA(asistenteVelo, 0, y1, Width, Height, 1, 1, c); stars.DrawColorQuad(asistenteVelo); }
                if (x0 > 0 && y1 > y0) { SetTexQuadA(asistenteVelo, 0, y0, x0, y1, 1, 1, c); stars.DrawColorQuad(asistenteVelo); }
                if (x1 < Width && y1 > y0) { SetTexQuadA(asistenteVelo, x1, y0, Width, y1, 1, 1, c); stars.DrawColorQuad(asistenteVelo); }
            }
            double t = (cuadroMs >= 0 ? cuadroMs : AhoraMs()) / 1000;
            float pulso = .55f + .45f * (float)Math.Sin(t * 4.5);
            int n = MarcoGuia(asistenteMarco, h, 3f, Math.Max(8f, Math.Min(22f, Height * .02f)), presencia * (.65f + .35f * pulso));
            if (n > 0) stars.DrawMeshAdd(asistenteMarco, n / 3);
        }

        // Marco de esquinas en inglete: un nucleo dorado y un resplandor que se apaga hacia fuera y hacia dentro.
        internal static int MarcoGuia(D3D9.Vtx[] v, RectangleF r, float grosor, float halo, float fuerza)
        {
            if (v == null || v.Length < 72 || !(r.Width > 0) || !(r.Height > 0)) return 0;
            fuerza = SafeMoonLevel(fuerza);
            if (fuerza <= 0) return 0;
            uint oro = 0x00FFC84Au;
            uint nucleo = ((uint)(235 * fuerza) << 24) | oro, medio = ((uint)(120 * fuerza) << 24) | oro, suave = ((uint)(70 * fuerza) << 24) | oro;
            int n = 0;
            BandaGuia(v, ref n, r, RectangleF.Inflate(r, grosor, grosor), nucleo, nucleo);
            BandaGuia(v, ref n, RectangleF.Inflate(r, grosor, grosor), RectangleF.Inflate(r, grosor + halo, grosor + halo), medio, oro);
            float dentro = Math.Min(halo * .6f, Math.Min(r.Width, r.Height) * .3f);
            BandaGuia(v, ref n, RectangleF.Inflate(r, -dentro, -dentro), r, oro, suave);
            return n;
        }
        private static void BandaGuia(D3D9.Vtx[] v, ref int n, RectangleF a, RectangleF b, uint ca, uint cb)
        {
            AsistenteCuadro(v, ref n, b.Left, b.Top, b.Right, b.Top, a.Right, a.Top, a.Left, a.Top, cb, cb, ca, ca);
            AsistenteCuadro(v, ref n, b.Right, b.Top, b.Right, b.Bottom, a.Right, a.Bottom, a.Right, a.Top, cb, cb, ca, ca);
            AsistenteCuadro(v, ref n, b.Right, b.Bottom, b.Left, b.Bottom, a.Left, a.Bottom, a.Right, a.Bottom, cb, cb, ca, ca);
            AsistenteCuadro(v, ref n, b.Left, b.Bottom, b.Left, b.Top, a.Left, a.Top, a.Left, a.Bottom, cb, cb, ca, ca);
        }

        // Donde entra en el rectangulo la recta que va de 'desde' a su centro (para la punta de la flecha).
        internal static bool EntradaRect(PointF desde, RectangleF r, out PointF punto)
        {
            punto = desde;
            if (r.Contains(desde)) return false;
            float cx = r.X + r.Width * .5f, cy = r.Y + r.Height * .5f, dx = cx - desde.X, dy = cy - desde.Y;
            float t0 = 0, t1 = 1;
            if (!Recorta(-dx, desde.X - r.Left, ref t0, ref t1) || !Recorta(dx, r.Right - desde.X, ref t0, ref t1)
                || !Recorta(-dy, desde.Y - r.Top, ref t0, ref t1) || !Recorta(dy, r.Bottom - desde.Y, ref t0, ref t1)) return false;
            punto = new PointF(desde.X + dx * t0, desde.Y + dy * t0);
            return true;
        }
        private static bool Recorta(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-6f) return q >= 0;
            float t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }

        // La flecha dorada que bota desde la mano de la estrella hasta lo que hay que usar.
        private void DibujaFlechaGuia(float cx, float cy, float radio, float presencia)
        {
            if (!GuiaSenala || presencia < .05f) return;
            RectangleF o = RectangleF.Inflate(asistenteEstado.Objetivo, 12, 12);
            float tx = o.X + o.Width * .5f, ty = o.Y + o.Height * .5f, dx = tx - cx, dy = ty - cy, l = (float)Math.Sqrt(dx * dx + dy * dy);
            if (!(l > 1f)) return;
            dx /= l; dy /= l;
            PointF desde = new PointF(cx + dx * radio * 1.9f, cy + dy * radio * 1.9f), punta;
            if (!EntradaRect(desde, o, out punta)) return;
            double t = (cuadroMs >= 0 ? cuadroMs : AhoraMs()) / 1000;
            float largo = Math.Max(24f, Math.Min(84f, Height * .075f));
            float hueco = (float)Math.Sqrt((punta.X - desde.X) * (punta.X - desde.X) + (punta.Y - desde.Y) * (punta.Y - desde.Y));
            if (hueco < 22f) return;
            largo = Math.Min(largo, hueco);
            float bota = (.5f + .5f * (float)Math.Sin(t * 6.5)) * largo * .22f;
            punta = new PointF(punta.X - dx * bota, punta.Y - dy * bota);
            int n = FlechaGuia(asistenteFlecha, punta.X, punta.Y, dx, dy, largo, presencia);
            stars.GlowBatchBegin(1);
            stars.GlowBatchAdd(punta.X - dx * largo * .3f, punta.Y - dy * largo * .3f, largo * .75f, ((uint)(60 * presencia) << 24) | 0xFFC84Au);
            stars.GlowBatchEnd();
            if (n > 0) stars.DrawMesh(asistenteFlecha, n / 3);
        }

        internal static int FlechaGuia(D3D9.Vtx[] v, float px, float py, float dx, float dy, float largo, float fuerza)
        {
            if (v == null || v.Length < 36 || !(largo > 0) || float.IsNaN(px) || float.IsNaN(py)) return 0;
            fuerza = SafeMoonLevel(fuerza);
            if (fuerza <= 0) return 0;
            float qx = -dy, qy = dx;   // perpendicular
            uint borde = ((uint)(210 * fuerza) << 24) | 0x00140D04u;
            uint claro = ((uint)(255 * fuerza) << 24) | 0x00FFE27Au, oscuro = ((uint)(255 * fuerza) << 24) | 0x00FFB020u;
            int n = 0;
            for (int capa = 0; capa < 2; capa++)
            {
                float m = capa == 0 ? 3f : 0f;   // primero el borde oscuro, un poco mayor
                uint cp = capa == 0 ? borde : claro, cb = capa == 0 ? borde : oscuro;
                float bx = px - dx * largo * .50f, by = py - dy * largo * .50f, ab = largo * .34f + m;
                V3(v, n++, px + dx * m, py + dy * m, cp);
                V3(v, n++, bx - dx * m * .6f + qx * ab, by - dy * m * .6f + qy * ab, cb);
                V3(v, n++, bx - dx * m * .6f - qx * ab, by - dy * m * .6f - qy * ab, cb);
                float ex = px - dx * largo, ey = py - dy * largo, a = largo * .13f + m;
                float sx0 = bx + dx * 2f, sy0 = by + dy * 2f, sx1 = ex - dx * m, sy1 = ey - dy * m;
                AsistenteCuadro(v, ref n, sx0 + qx * a, sy0 + qy * a, sx1 + qx * a, sy1 + qy * a, sx1 - qx * a, sy1 - qy * a,
                    sx0 - qx * a, sy0 - qy * a, cb, cb, cb, cb);
            }
            return n;
        }
    }
}
#endif
