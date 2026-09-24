// LA PANTALLA VIVA: las ideas del lienzo de bocetos que eligio el dueño (24-09-2026). Solo con tarjeta grafica.
//
// Cada idea tiene su interruptor en Ctrl+F3 -> Pantalla y se guarda en rocola_config.txt. Sin tarjeta grafica no
// hacen nada: el dibujo por procesador esta congelado y sale exactamente como antes.
//
// TANDA 1, LA PARED Y LAS LETRAS:
//  - PORTADA ENFOCADA. La portada elegida crece un 12 % hacia arriba y hacia los lados (hacia abajo no: debajo va su
//    nombre y tiene que seguir leyendose), su halo toma el color de su propia portada, las demas bajan un poco de
//    brillo y sobre su borde de abajo sale un letrero con cuantas canciones trae y cuanto dura.
//  - FICHA RAPIDA. Si el cursor se queda un segundo en un disco, al lado sale una tarjeta con sus canciones mas
//    pedidas y cuantas suyas van ya en la cola. Si nadie toca nada se va sola a los nueve segundos.
//  - PASAR DE PAGINA DESLIZANDO. La pagina vieja sale por un lado mientras la nueva entra por el otro, columna a
//    columna, y el cielo se corre un poco, mas despacio que las portadas: da profundidad. Dura lo mismo que el
//    deslizamiento de antes (Form1.Pared.cs, AlbumView.CurvaColumna).
//  - BARRA DE LETRAS CON LUPA. La letra elegida y sus vecinas se ven mas grandes, y una rayita debajo de cada una
//    dice cuanta musica tiene (Form1.Vistas.cs, LetterBar.RenderLupa).
//
// TANDA 2, DENTRO DEL DISCO, en DiscoVivoGpu.cs: el vinilo que asoma, la lista grande, el ecualizador en la que suena,
// los minutos de la cola y el boleto que vuela a la cola. Sus interruptores van en esta misma pestaña, en su tarjeta.
//
// TANDA 3, LO QUE SUENA Y LOS CREDITOS, en SuenaVivoGpu.cs: lo que suena en grande, la moneda que cae, la tarjeta que
// invita cuando no hay creditos y los avisos que se entienden. Van en la segunda hoja de esta pestaña.
//
// TANDA 4, EL MODO ATRACCION, en AtraccionGpu.cs: el escaparate y lo mas pedido esta noche. Van en la tercera hoja.
//
// TANDA 5, EL ESTILO GENERAL, en EstiloGpu.cs: la letra para leer de lejos y el color del genero en todo. En la tercera
// hoja, al lado del modo atraccion.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        // Existen en los dos perfiles porque la configuracion es la misma; sin tarjeta grafica no se usan.
        private bool portadaEnfocada = true;   // "portada_enfocada="
        private bool fichaDisco = true;        // "ficha_disco="
        private bool paginaDesliza = true;     // "pagina_desliza="
        private bool letrasLupa = true;        // "letras_lupa="

        // El letrero de la portada elegida: "12 canciones · 38 min". Los minutos solo si se sabe lo que dura CADA
        // cancion ('segundos' <= 0 = falta alguna): mejor callar que adivinar.
        internal static string TextoFoco(int canciones, int videos, double segundos)
        {
            string s;
            if (canciones <= 0 && videos > 0) s = videos + (videos == 1 ? " video" : " videos");
            else s = canciones + (canciones == 1 ? " canción" : " canciones");
            if (segundos > 0 && canciones > 0)
            {
                int min = (int)Math.Round(segundos / 60.0);
                if (min < 1) s += " · menos de 1 min";
                else if (min < 60) s += " · " + min + " min";
                else s += " · " + (min / 60) + " h " + (min % 60).ToString("00") + " min";
            }
            return s;
        }

        // ---------------- CTRL+F3 -> PANTALLA ----------------
        // Esta en los dos perfiles, como la pestaña del Fondo: la configuracion es la misma y el operador ve lo
        // que hay. Sin tarjeta grafica los interruptores se guardan y no cambian nada.
        private Panel PaginaPantalla(Font fNom, Font fDes, Font fBot, Color cTexto, Color cTenue, Color cApagado)
        {
            Color cAc = Color.FromArgb(150, 190, 255);
            Panel pg = new Panel();
            pg.SetBounds(0, 96, 960, 636);
            pg.BackColor = Color.FromArgb(11, 14, 32);
            // HOJAS: ya no caben todas las tarjetas a la vez. Arriba, un boton por hoja; debajo, la que toque.
            Panel hPared = new Panel(), hSuena = new Panel(), hAtr = new Panel();
            hPared.SetBounds(0, 48, 960, 452); hSuena.SetBounds(0, 48, 960, 452); hAtr.SetBounds(0, 48, 960, 452);
            hPared.BackColor = pg.BackColor; hSuena.BackColor = pg.BackColor; hAtr.BackColor = pg.BackColor;
            Tarjeta tPared = new Tarjeta(); tPared.SetBounds(16, 0, 456, 412); tPared.Titulo = "LA PARED Y LAS LETRAS"; tPared.Acento = cAc;
            string[] nom = {
                "Portada enfocada (GPU)",
                "Ficha rápida del disco (GPU)",
                "Pasar de página deslizando (GPU)",
                "Barra de letras con lupa (GPU)",
            };
            string[] explica = {
                "La portada elegida crece, brilla con su propio color y dice cuantas\ncanciones trae y cuanto dura. Las demas se apagan un poco.",
                "Si el cursor se queda un segundo en un disco, sale una tarjeta con\nsus canciones mas pedidas y cuantas van ya en la cola.",
                "La pagina vieja sale mientras entra la nueva, columna a columna,\ny el cielo se corre un poco mas despacio: da profundidad.",
                "La letra elegida y sus vecinas se ven mas grandes, y una rayita\ndebajo de cada una dice cuanta musica tiene.",
            };
            bool[] val = { portadaEnfocada, fichaDisco, paginaDesliza, letrasLupa };
            Interruptor[] sw = new Interruptor[nom.Length];
            for (int i = 0; i < sw.Length; i++)
            {
                int y = 40 + i * 72;
                sw[i] = new Interruptor(); sw[i].SetBounds(16, y, 424, 26);
                sw[i].Font = fNom; sw[i].ForeColor = cTexto; sw[i].Acento = cAc;
                sw[i].Text = nom[i]; sw[i].Checked = val[i];
                Label le = new Label(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explica[i];
                tPared.Controls.Add(sw[i]); tPared.Controls.Add(le);
            }
            sw[0].CheckedChanged += delegate { portadaEnfocada = sw[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[1].CheckedChanged += delegate { fichaDisco = sw[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[2].CheckedChanged += delegate { paginaDesliza = sw[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            sw[3].CheckedChanged += delegate { letrasLupa = sw[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            // LA TANDA 2, al lado: lo que pasa dentro de un disco (DiscoVivoGpu.cs).
            Tarjeta tDisco = new Tarjeta(); tDisco.SetBounds(488, 0, 456, 412); tDisco.Titulo = "DENTRO DEL DISCO"; tDisco.Acento = cAc;
            string[] nomD = {
                "Vinilo que asoma (GPU)",
                "Lista grande (GPU)",
                "Ecualizador en la que suena (GPU)",
                "En cola, con los minutos (GPU)",
                "Ver adónde va la canción (GPU)",
            };
            string[] explicaD = {
                "Si suena ese disco, un vinilo sale de detras de la portada y gira\nmientras suena. Al acabar se vuelve a guardar.",
                "Filas mas altas y letra mas grande dentro del disco: se lee de\nlejos. Caben menos canciones por pagina.",
                "La cancion que suena lleva cuatro barritas que se mueven con\nla musica, en vez del triangulito.",
                "Las canciones pedidas dicen tambien cuanto falta para que\nsuenen: \"EN COLA 3 · en ~9 min\".",
                "Al pedir una cancion, su boleto vuela desde la fila hasta \"En\ncola\" y el numero sube cuando llega.",
            };
            bool[] valD = { viniloDisco, listaGrande, eqSonando, colaMinutos, vueloCola };
            Interruptor[] swD = new Interruptor[nomD.Length];
            for (int i = 0; i < swD.Length; i++)
            {
                int y = 40 + i * 72;
                swD[i] = new Interruptor(); swD[i].SetBounds(16, y, 424, 26);
                swD[i].Font = fNom; swD[i].ForeColor = cTexto; swD[i].Acento = cAc;
                swD[i].Text = nomD[i]; swD[i].Checked = valD[i];
                Label le = new Label(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explicaD[i];
                tDisco.Controls.Add(swD[i]); tDisco.Controls.Add(le);
            }
            swD[0].CheckedChanged += delegate { viniloDisco = swD[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swD[1].CheckedChanged += delegate { listaGrande = swD[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swD[2].CheckedChanged += delegate { eqSonando = swD[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swD[3].CheckedChanged += delegate { colaMinutos = swD[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swD[4].CheckedChanged += delegate { vueloCola = swD[4].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            // LA TANDA 3, en la segunda hoja: lo que suena y los creditos (SuenaVivoGpu.cs).
            Tarjeta tSuena = new Tarjeta(); tSuena.SetBounds(16, 0, 456, 328); tSuena.Titulo = "LO QUE SUENA Y LOS CREDITOS"; tSuena.Acento = cAc;
            string[] nomS = {
                "Lo que suena, en grande (GPU)",
                "La moneda que cae (GPU)",
                "Sin creditos, invitar (GPU)",
                "Avisos que se entienden (GPU)",
            };
            string[] explicaS = {
                "En reposo, un vinilo gira detras de la portada, el titulo se\ndesliza si no cabe y dice cuanto falta, tambien de las que siguen.",
                "Al meter una moneda cae una moneda dorada en la capsula de\ncreditos, el numero rueda y sube \"+4 creditos\".",
                "Si alguien pide algo sin creditos, en vez del letrero naranja\nsale una tarjeta amable que invita a meter una moneda.",
                "Los avisos entran por la derecha con su color y su icono, en\ndos renglones, y una linea dice cuanto les queda.",
            };
            bool[] valS = { suenaGrande, monedaCae, sinCreditosInvita, avisosClaros };
            Interruptor[] swS = new Interruptor[nomS.Length];
            for (int i = 0; i < swS.Length; i++)
            {
                int y = 40 + i * 72;
                swS[i] = new Interruptor(); swS[i].SetBounds(16, y, 424, 26);
                swS[i].Font = fNom; swS[i].ForeColor = cTexto; swS[i].Acento = cAc;
                swS[i].Text = nomS[i]; swS[i].Checked = valS[i];
                Label le = new Label(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explicaS[i];
                tSuena.Controls.Add(swS[i]); tSuena.Controls.Add(le);
            }
            swS[0].CheckedChanged += delegate { suenaGrande = swS[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swS[1].CheckedChanged += delegate { monedaCae = swS[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swS[2].CheckedChanged += delegate { sinCreditosInvita = swS[2].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swS[3].CheckedChanged += delegate { avisosClaros = swS[3].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            // LA TANDA 4, en la tercera hoja: el modo atraccion (AtraccionGpu.cs).
            Tarjeta tAtr = new Tarjeta(); tAtr.SetBounds(16, 0, 456, 184); tAtr.Titulo = "MODO ATRACCION"; tAtr.Acento = cAc;
            string[] nomA = {
                "El escaparate (GPU)",
                "Lo más pedido esta noche (GPU)",
            };
            string[] explicaA = {
                "Cuando nadie usa la rocola, el cubo se mece a la izquierda y a\nsu lado sale el disco en grande, con mensajes que se turnan.",
                "Cada 30 segundos, la lista de las cinco más pedidas desde las\n8 de la mañana, con barras que crecen.",
            };
            bool[] valA = { escaparate, topNoche };
            Interruptor[] swA = new Interruptor[nomA.Length];
            for (int i = 0; i < swA.Length; i++)
            {
                int y = 40 + i * 72;
                swA[i] = new Interruptor(); swA[i].SetBounds(16, y, 424, 26);
                swA[i].Font = fNom; swA[i].ForeColor = cTexto; swA[i].Acento = cAc;
                swA[i].Text = nomA[i]; swA[i].Checked = valA[i];
                Label le = new Label(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explicaA[i];
                tAtr.Controls.Add(swA[i]); tAtr.Controls.Add(le);
            }
            swA[0].CheckedChanged += delegate { escaparate = swA[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swA[1].CheckedChanged += delegate { topNoche = swA[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            // LA TANDA 5, al lado: el estilo general (EstiloGpu.cs).
            Tarjeta tEst = new Tarjeta(); tEst.SetBounds(488, 0, 456, 184); tEst.Titulo = "ESTILO GENERAL"; tEst.Acento = cAc;
            string[] nomE = {
                "Letra para leer de lejos (GPU)",
                "El color del género en todo (GPU)",
            };
            string[] explicaE = {
                "El titulo de lo que suena, abajo, crece y lo que sigue pasa a la\nderecha en un gris mas claro: se lee de pie, a dos metros.",
                "La portada de lo que suena y la de dentro de un disco brillan con\nel color del genero, y tambien el pulso de la letra elegida.",
            };
            bool[] valE = { letraLejos, colorGenero };
            Interruptor[] swE = new Interruptor[nomE.Length];
            for (int i = 0; i < swE.Length; i++)
            {
                int y = 40 + i * 72;
                swE[i] = new Interruptor(); swE[i].SetBounds(16, y, 424, 26);
                swE[i].Font = fNom; swE[i].ForeColor = cTexto; swE[i].Acento = cAc;
                swE[i].Text = nomE[i]; swE[i].Checked = valE[i];
                Label le = new Label(); le.SetBounds(16, y + 30, 424, 32); le.BackColor = Color.Transparent;
                le.ForeColor = cTenue; le.Font = fDes; le.Text = explicaE[i];
                tEst.Controls.Add(swE[i]); tEst.Controls.Add(le);
            }
            swE[0].CheckedChanged += delegate { letraLejos = swE[0].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            swE[1].CheckedChanged += delegate { colorGenero = swE[1].Checked; AplicaEfectosGpu(); f3CambioGraf = true; };
            Label lPie = new Label(); lPie.SetBounds(16, 508, 928, 48); lPie.BackColor = Color.Transparent;
            lPie.ForeColor = cApagado; lPie.Font = fDes;
            lPie.Text = "Las ideas que se eligieron del lienzo de bocetos. Solo se ven con la tarjeta grafica: sin ella la pantalla\n"
                      + "sale exactamente como siempre. Ninguna le quita rendimiento que se note; son de gusto, y todo lo que se\n"
                      + "toque aqui se guarda al cerrar la ventana.";
            BotonPlano bReset = new BotonPlano(); bReset.SetBounds(16, 572, 300, 38); bReset.Font = fBot; bReset.Text = "Pantalla como de fabrica";
            bReset.Click += delegate
            {
                portadaEnfocada = fichaDisco = paginaDesliza = letrasLupa = true;
                viniloDisco = listaGrande = eqSonando = colaMinutos = vueloCola = true;
                suenaGrande = monedaCae = sinCreditosInvita = avisosClaros = true;
                escaparate = topNoche = true;
                letraLejos = colorGenero = true;
                for (int i = 0; i < sw.Length; i++) sw[i].Checked = true;
                for (int i = 0; i < swD.Length; i++) swD[i].Checked = true;
                for (int i = 0; i < swS.Length; i++) swS[i].Checked = true;
                for (int i = 0; i < swA.Length; i++) swA[i].Checked = true;
                for (int i = 0; i < swE.Length; i++) swE[i].Checked = true;
                f3CambioGraf = true; AplicaEfectosGpu();
                ShowToast("Pantalla como de fabrica", Color.FromArgb(40, 130, 90));
            };
            hPared.Controls.Add(tPared); hPared.Controls.Add(tDisco); hSuena.Controls.Add(tSuena); hAtr.Controls.Add(tAtr); hAtr.Controls.Add(tEst);
            Panel[] hojas = { hPared, hSuena, hAtr };
            string[] nomHoja = { "La pared y el disco", "Lo que suena y los creditos", "La atracción y el estilo" };
            BotonPlano[] bHoja = new BotonPlano[hojas.Length];
            for (int i = 0; i < hojas.Length; i++)
            {
                bHoja[i] = new BotonPlano(); bHoja[i].SetBounds(16 + i * 312, 4, 300, 36); bHoja[i].Font = fBot; bHoja[i].Text = nomHoja[i];
                int k = i;   // una copia por vuelta: cada boton recuerda su hoja
                bHoja[i].Click += delegate { MuestraHoja(hojas, bHoja, k, cApagado); };
                pg.Controls.Add(bHoja[i]); pg.Controls.Add(hojas[i]);
            }
            MuestraHoja(hojas, bHoja, 0, cApagado);
            pg.Controls.Add(lPie); pg.Controls.Add(bReset);
            return pg;
        }
        // Enseña una hoja y enciende su boton, con los mismos colores que las pestañas de arriba.
        private static void MuestraHoja(Panel[] hojas, BotonPlano[] botones, int k, Color cApagado)
        {
            for (int i = 0; i < hojas.Length; i++)
            {
                bool act = (i == k);
                hojas[i].Visible = act;
                botones[i].Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                botones[i].Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                botones[i].ForeColor = act ? Color.FromArgb(226, 240, 255) : cApagado;
                botones[i].Invalidate();
            }
        }
    }

#if ROCOLA_GPU
    // La ficha rapida y el letrero de la portada elegida, tal como se los pasa Form1 al panel.
    internal struct GpuFicha { public bool Has; public Bitmap Bmp; public int Ver; public float X, Y, Alpha; }
    internal struct GpuFoco { public bool Has; public Bitmap Bmp; public int Ver; }

    public partial class Form1
    {
        // ---------------- EL DISCO ELEGIDO EN LA PARED ----------------
        // Solo cuando la pared es lo que se esta mirando: ni dentro de un disco, ni con el buscador, ni en modo
        // atraccion, ni mientras la pagina se desliza.
        private Album AlbumElegidoEnPared()
        {
            if (!gpuEnabled || albumView == null || !albumView.Visible || searchOpen || attractOn) return null;
            if (panelSongs != null && panelSongs.Visible) return null;
            if (albumView.Sliding || albumView.Page == null) return null;
            int i = albumView.Selected;
            if (i < 0 || i >= albumView.Page.Count) return null;
            return albumView.Page[i];
        }

        // Cuantas canciones, cuantos videos y cuanto dura el disco (-1 si falta la duracion de alguna). Las
        // duraciones son las ya apuntadas (SegundosDe): aqui no se abre ningun archivo.
        private void CuentaDisco(Album a, out int canciones, out int videos, out double segundos)
        {
            canciones = (a != null && a.Tracks != null) ? a.Tracks.Count : 0;
            videos = (a != null && a.VideoFiles != null) ? a.VideoFiles.Count : 0;
            segundos = 0;
            if (canciones == 0) { segundos = -1; return; }
            for (int i = 0; i < canciones; i++)
            {
                float d = SegundosDe(a.Tracks[i].Path);
                if (d <= 0f) { segundos = -1; return; }
                segundos += d;
            }
        }

        // ---------------- EL LETRERO DE LA PORTADA ELEGIDA ----------------
        // Una sola imagen, horneada al cambiar de disco (no en cada cuadro) y reusada: el panel la sube solo
        // cuando cambia la version.
        private Album focoAlbum; private Bitmap focoBmp; private int focoVer, focoLado = -1;
        private GpuFoco BuildFocoState()
        {
            GpuFoco f = new GpuFoco();
            if (!portadaEnfocada) return f;
            Album a = AlbumElegidoEnPared();
            if (a == null) return f;
            int lado = AlbumView.LadoPortada(albumView.Width, albumView.Height), cw = albumView.Width / Cols;
            int medida = lado * 4096 + cw;   // se rehace si cambia la portada o la casilla
            if (!ReferenceEquals(a, focoAlbum) || focoBmp == null || medida != focoLado)
            { HorneaFoco(a, lado, cw); focoAlbum = a; focoLado = medida; }
            f.Has = focoBmp != null; f.Bmp = focoBmp; f.Ver = focoVer;
            return f;
        }
        // La pastilla puede ser un poco mas ancha que la portada -"12 canciones · 38 min" no cabe a una letra que se
        // lea en una portada de 128 px, que es la de 1024x768- pero NUNCA se sale de su casilla: asi no pisa a la de
        // al lado. Si aun asi el texto no cabe, la letra baja un poco y, si ni asi, se quitan los minutos. Su alto
        // sale del lado de la portada.
        internal static int AltoFoco(int lado) { return Math.Max(18, Math.Min(34, lado / 5)); }
        internal static int AnchoFoco(int lado, int cw) { return Math.Max(40, Math.Min(cw - 8, (int)(lado * 1.35f))); }
        private void HorneaFoco(Album a, int lado, int cw)
        {
            int can, vid; double seg; CuentaDisco(a, out can, out vid, out seg);
            int w = AnchoFoco(lado, cw), h = AltoFoco(lado);
            if (focoBmp == null || focoBmp.Width != w || focoBmp.Height != h)
            {
                if (focoBmp != null) focoBmp.Dispose();
                focoBmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            }
            PintaFoco(focoBmp, TextoFoco(can, vid, seg), TextoFoco(can, vid, -1), FamiliaUi());
            focoVer++;
        }
        // La pastilla, centrada en su imagen: fondo oscuro translucido, filo claro y el texto blanco. Devuelve el
        // texto que de verdad escribio (el largo, o el corto si el largo no cabia).
        internal static string PintaFoco(Bitmap bmp, string txt, string corto, FontFamily fam)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                int h = bmp.Height;
                float cabe = bmp.Width - 2f - h * 1.1f;   // lo que queda para el texto, quitando las puntas redondas
                string t = txt;
                Font f = LetraQueQuepa(g, fam, t, h * .46f, h * .34f, cabe);
                if (f == null && corto != null && corto != txt) { t = corto; f = LetraQueQuepa(g, fam, t, h * .46f, h * .34f, cabe); }
                if (f == null) f = new Font(fam, h * .34f, FontStyle.Bold, GraphicsUnit.Pixel);   // y si ni asi, puntos suspensivos
                using (f)
                {
                    float tw = g.MeasureString(t, f).Width;
                    float w = Math.Min(bmp.Width - 2f, tw + h * 1.1f);
                    RectangleF r = new RectangleF((bmp.Width - w) / 2f, 0.5f, w, h - 1.5f);
                    using (GraphicsPath p = Pildora(r))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 8, 12, 30))) g.FillPath(b, p);
                        using (Pen pe = new Pen(Color.FromArgb(110, 225, 235, 255), 1f)) g.DrawPath(pe, p);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 240, 245, 255)))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                        sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                        g.DrawString(t, f, b, r, sf);
                    }
                }
                return t;
            }
        }
        // La letra mas grande, entre 'desde' y 'hasta' (en pixeles), con la que 't' cabe en 'ancho'; null si ninguna.
        private static Font LetraQueQuepa(Graphics g, FontFamily fam, string t, float desde, float hasta, float ancho)
        {
            for (float c = desde; c >= hasta - 0.01f; c -= 0.5f)
            {
                Font f = new Font(fam, c, FontStyle.Bold, GraphicsUnit.Pixel);
                if (g.MeasureString(t, f).Width <= ancho) return f;
                f.Dispose();
            }
            return null;
        }
        private static GraphicsPath Pildora(RectangleF r)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(r.Height, r.Width);
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }

        // ---------------- LA FICHA RAPIDA ----------------
        // Sale si el cursor se queda FichaEsperaMs en el mismo disco y se va sola si nadie toca nada. Se hornea UNA
        // vez por parada, un poco ANTES de salir (FichaAntesMs): asi el cuadro en que aparece no carga con el dibujo
        // ni con la subida a la tarjeta. Se va en cuanto el cursor se mueve.
        internal const int FichaEsperaMs = 1100, FichaEntraMs = 220, FichaDuraMs = 9000, FichaSaleMs = 400, FichaAntesMs = 250;
        private Album fichaAlbum; private int fichaDesde, fichaColaVer; private bool fichaHecha, fichaAcabada;
        private Bitmap fichaBmp; private int fichaVer;
        private GpuFicha BuildFichaState()
        {
            GpuFicha f = new GpuFicha();
            Album a = (fichaDisco && !(gpuPanel != null && gpuPanel.IdleExpandTarget)) ? AlbumElegidoEnPared() : null;
            if (a != null && a.IsTop) a = null;   // "lo mas sonado" ya es una lista de las mas pedidas
            int ahora = Environment.TickCount;
            if (a == null) { fichaAlbum = null; return f; }
            if (!ReferenceEquals(a, fichaAlbum)) { fichaAlbum = a; fichaDesde = ahora; fichaHecha = false; fichaAcabada = false; return f; }
            if (fichaAcabada) return f;
            int dt = ahora - fichaDesde;
            if (dt < FichaEsperaMs - FichaAntesMs) return f;
            if (dt >= FichaEsperaMs + FichaEntraMs + FichaDuraMs + FichaSaleMs) { fichaAcabada = true; return f; }
            Rectangle celda = albumView.RectElegida();
            if (celda.Width < 8) return f;
            celda.Offset(albumView.Left, albumView.Top);
            // Se rehace si cambio la cola mientras se ve (alguien pidio una de este disco): el numero tiene que cuadrar.
            if (!fichaHecha || fichaColaVer != colaVer)
            { HorneaFicha(a, albumView.Width / Cols, albumView.Height / Rows); fichaHecha = true; fichaColaVer = colaVer; }
            if (fichaBmp == null) return f;
            float alfa = CurvaFicha(dt);
            PointF p = LugarFicha(celda, albumView.Bounds, fichaBmp.Width, fichaBmp.Height);
            f.Has = true; f.Bmp = fichaBmp; f.Ver = fichaVer;   // con alfa 0 tambien: el panel la sube antes de enseñarla
            f.X = p.X; f.Y = p.Y + 14f * (1f - alfa); f.Alpha = alfa;
            return f;
        }
        // 0 mientras espera; sube a 1 en FichaEntraMs frenando al llegar, se queda FichaDuraMs y se va en FichaSaleMs.
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
        // A la derecha de la casilla si cabe; si no, a la izquierda. Nunca fuera de la pared.
        internal static PointF LugarFicha(Rectangle celda, Rectangle pared, int w, int h)
        {
            const int sep = 16;
            float x = celda.Right + sep;
            if (x + w > pared.Right - 6) x = celda.Left - sep - w;
            if (x < pared.Left + 6) x = pared.Left + 6;
            float y = celda.Top - 4;
            if (y + h > pared.Bottom - 6) y = pared.Bottom - 6 - h;
            if (y < pared.Top + 4) y = pared.Top + 4;
            return new PointF(x, y);
        }
        // Las tres mas pedidas (por las veces que entraron en la cola), de mas a menos; a igualdad, en el orden del
        // disco. Si ninguna se ha pedido nunca, 'pedidas' sale false y van las tres primeras.
        internal static List<int> MasPedidas(IList<string> rutas, IDictionary<string, int> cuentas, out bool pedidas)
        {
            List<int> r = new List<int>();
            pedidas = false;
            if (rutas == null) return r;
            int n = rutas.Count;
            int[] veces = new int[n];
            for (int i = 0; i < n; i++)
            {
                int v = 0;
                if (cuentas != null && rutas[i] != null) cuentas.TryGetValue(rutas[i], out v);
                veces[i] = v;
                if (v > 0) pedidas = true;
            }
            if (!pedidas) { for (int i = 0; i < n && i < 3; i++) r.Add(i); return r; }
            for (int k = 0; k < 3; k++)
            {
                int mejor = -1;
                for (int i = 0; i < n; i++)
                    if (veces[i] > 0 && !r.Contains(i) && (mejor < 0 || veces[i] > veces[mejor])) mejor = i;
                if (mejor < 0) break;
                r.Add(mejor);
            }
            return r;
        }
        // El tamaño de la tarjeta sale de la casilla: dos casillas de ancho y un poco mas de una de alto.
        internal static Size TamanoFicha(int cw, int ch)
        {
            return new Size(Math.Max(260, Math.Min(480, cw * 2 - 20)), Math.Max(170, Math.Min(300, (int)(ch * 1.36f))));
        }
        private void HorneaFicha(Album a, int cw, int ch)
        {
            Size tam = TamanoFicha(cw, ch);
            if (fichaBmp == null || fichaBmp.Width != tam.Width || fichaBmp.Height != tam.Height)
            {
                if (fichaBmp != null) fichaBmp.Dispose();
                fichaBmp = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppPArgb);
            }
            List<string> rutas = new List<string>();
            if (a.Tracks != null) for (int i = 0; i < a.Tracks.Count; i++) rutas.Add(a.Tracks[i].Path);
            bool pedidas; List<int> top = MasPedidas(rutas, songCounts, out pedidas);
            int enCola = 0;
            for (int i = 0; i < queue.Count; i++) if (queue[i] != null && ReferenceEquals(queue[i].Disc, a)) enCola++;
            int can, vid; double seg; CuentaDisco(a, out can, out vid, out seg);
            // La portadita, SOLO si ya esta en memoria: la ficha no encarga nada al disco.
            Image tapa = (albumView.PeekCoverGpu != null) ? albumView.PeekCoverGpu(a) : null;
            string[] filas = new string[top.Count], durs = new string[top.Count]; int[] veces = new int[top.Count];
            for (int k = 0; k < top.Count; k++)
            {
                Track t = a.Tracks[top[k]];
                filas[k] = t.ToString(); durs[k] = DurText(t.Path);
                int v = 0; if (t.Path != null) songCounts.TryGetValue(t.Path, out v); veces[k] = v;
            }
            // Arriba el artista y debajo el disco, como en el boceto; si el disco no trae artista, solo su nombre.
            string artista = string.IsNullOrEmpty(a.Artist) ? a.Name : a.Artist;
            string disco = string.IsNullOrEmpty(a.Artist) ? "" : a.Name;
            PintaFicha(fichaBmp, tapa, artista, disco, TextoFoco(can, vid, seg), EtiquetaDisco(a), ColorDisco(a), enCola,
                       pedidas, filas, durs, veces, can, FamiliaUi());
            fichaVer++;
        }
        // El dibujo de la ficha, aparte del estado: asi se puede mirar y comprobar sin arrancar la rocola. Va de arriba
        // abajo con un cursor: si las filas no caben entre la cabecera y el pie, van las que caben (nunca se montan).
        internal static void PintaFicha(Bitmap bmp, Image tapa, string artista, string disco, string resumen, string genero, Color colGenero,
            int enCola, bool pedidas, string[] filas, string[] durs, int[] veces, int total, FontFamily fam)
        {
            int w = bmp.Width, h = bmp.Height;
            float u = h / 230f;   // todo sale del alto de la tarjeta
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font fTit = new Font(fam, 18f * u, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fDisco = new Font(fam, 12.5f * u, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fSub = new Font(fam, 13f * u, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fMin = new Font(fam, 11f * u, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fFila = new Font(fam, 14f * u, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(Color.White))
            using (StringFormat izq = new StringFormat())
            using (StringFormat cen = new StringFormat())
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                izq.FormatFlags = StringFormatFlags.NoWrap; izq.Trimming = StringTrimming.EllipsisCharacter;
                izq.LineAlignment = StringAlignment.Center;
                cen.Alignment = StringAlignment.Center; cen.LineAlignment = StringAlignment.Center;
                RectangleF caja = new RectangleF(1, 1, w - 3, h - 3);
                Color filo = Color.FromArgb(150, Mezcla(colGenero, Color.White, .45f));
                using (GraphicsPath p = Redondo(caja, 16f * u))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 12, 18, 42))) g.FillPath(b, p);
                    using (Pen pe = new Pen(filo, 1.5f)) g.DrawPath(pe, p);
                }
                float pad = 16f * u, lado = 64f * u;
                RectangleF rT = new RectangleF(pad, pad, lado, lado);
                if (tapa != null)
                {
                    GraphicsState st = g.Save();
                    using (GraphicsPath pt = Redondo(rT, 7f * u)) g.SetClip(pt);
                    g.DrawImage(tapa, rT);
                    g.Restore(st);
                }
                else
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(80, colGenero)))
                    using (GraphicsPath pt = Redondo(rT, 7f * u)) g.FillPath(b, pt);
                // LA CABECERA: artista, disco, cuantas y cuanto duran, y las pastillas del genero y de la cola.
                float x = rT.Right + 12f * u, anchoTexto = w - x - pad, y = pad - 2f * u;
                br.Color = Color.White;
                g.DrawString(artista ?? "", fTit, br, new RectangleF(x, y, anchoTexto, 23f * u), izq);
                y += 22f * u;
                if (!string.IsNullOrEmpty(disco))
                {
                    br.Color = Color.FromArgb(230, 200, 212, 245);
                    g.DrawString(disco, fDisco, br, new RectangleF(x, y, anchoTexto, 18f * u), izq);
                    y += 18f * u;
                }
                br.Color = Color.FromArgb(215, 160, 178, 222);
                g.DrawString(resumen ?? "", fMin, br, new RectangleF(x, y, anchoTexto, 17f * u), izq);
                y += 18f * u;
                float xp = x, altoP = 19f * u;
                bool hayPastilla = !string.IsNullOrEmpty(genero) || enCola > 0;
                if (!string.IsNullOrEmpty(genero)) xp += Etiqueta(g, genero, fMin, colGenero, xp, y, altoP) + 8f * u;
                if (enCola > 0) Etiqueta(g, enCola + " en la cola", fMin, ColaVerde, xp, y, altoP);
                if (hayPastilla) y += altoP;
                float yl = Math.Max(rT.Bottom, y) + 9f * u;
                using (Pen sep = new Pen(Color.FromArgb(60, 160, 180, 230), 1f)) g.DrawLine(sep, pad, yl, w - pad, yl);
                br.Color = Color.FromArgb(200, 150, 170, 220);
                g.DrawString(pedidas ? "LAS MÁS PEDIDAS DE ESTE DISCO" : "ALGUNAS DE ESTE DISCO", fMin, br,
                             new RectangleF(pad, yl + 3f * u, w - 2 * pad, 17f * u), izq);
                // LAS FILAS: numero, titulo y cuantas veces se pidio. La duracion solo si al titulo le sigue quedando
                // al menos la mitad de la fila: en una tarjeta estrecha manda el titulo.
                float yb = h - pad - 17f * u, rb = 8f * u;   // el pie
                float yf = yl + 21f * u, altoF = 23f * u;
                int caben = Math.Max(0, (int)((yb - 3f * u - yf) / altoF));
                for (int k = 0; k < filas.Length && k < caben; k++)
                {
                    float yk = yf + k * altoF;
                    RectangleF rc = new RectangleF(pad, yk + 3f * u, altoF - 6f * u, altoF - 6f * u);
                    using (Pen pc = new Pen(Color.FromArgb(200, ColaVerde), 1.2f)) g.DrawEllipse(pc, rc);
                    br.Color = ColaVerde; g.DrawString((k + 1).ToString(), fMin, br, rc, cen);
                    float xt = rc.Right + 9f * u, derecha = w - pad;
                    if (pedidas && veces != null && k < veces.Length && veces[k] > 0)
                    {
                        string vt = veces[k] == 1 ? "1 vez" : (veces[k] + " veces");
                        float vw = g.MeasureString(vt, fMin).Width;
                        br.Color = SonAmbar;
                        g.DrawString(vt, fMin, br, new RectangleF(derecha - vw, yk, vw + 2, altoF), izq);
                        derecha -= vw + 8f * u;
                    }
                    string dur = (durs != null && k < durs.Length) ? durs[k] : null;
                    if (!string.IsNullOrEmpty(dur))
                    {
                        float dw = g.MeasureString(dur, fMin).Width;
                        if (derecha - dw - 8f * u - xt >= (w - 2 * pad) * .5f)
                        {
                            br.Color = Color.FromArgb(200, 160, 176, 218);
                            g.DrawString(dur, fMin, br, new RectangleF(derecha - dw, yk, dw + 2, altoF), izq);
                            derecha -= dw + 8f * u;
                        }
                    }
                    br.Color = Color.White;
                    g.DrawString(filas[k] ?? "", fFila, br, new RectangleF(xt, yk, Math.Max(10f, derecha - xt), altoF), izq);
                }
                // El pie: el boton verde y lo que hace.
                using (SolidBrush verde = new SolidBrush(Color.FromArgb(72, 212, 112))) g.FillEllipse(verde, pad, yb + 1f * u, rb * 2, rb * 2);
                br.Color = Color.FromArgb(230, 223, 230, 255);
                string pie = total > 0 ? ("Entrar para ver las " + total) : "Entrar al disco";
                g.DrawString(pie, fSub, br, new RectangleF(pad + rb * 2 + 8f * u, yb - 1f * u, w - 2 * pad, 20f * u), izq);
            }
        }
        // Pastilla de texto con borde del color dado; devuelve su ancho.
        private static float Etiqueta(Graphics g, string txt, Font f, Color c, float x, float y, float alto)
        {
            float tw = g.MeasureString(txt, f).Width, w = tw + alto * 0.9f;
            RectangleF r = new RectangleF(x, y, w, alto);
            using (GraphicsPath p = Pildora(r))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(b, p);
                using (Pen pe = new Pen(Color.FromArgb(220, Mezcla(c, Color.White, .25f)), 1f)) g.DrawPath(pe, p);
            }
            using (SolidBrush b = new SolidBrush(Mezcla(c, Color.White, .35f)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(txt, f, b, r, sf);
            }
            return w;
        }
        private static Color Mezcla(Color a, Color b, float t)
        {
            return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        private static GraphicsPath Redondo(RectangleF r, float radio)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radio * 2f, Math.Min(r.Width, r.Height));
            if (d < 1f) d = 1f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    internal sealed partial class GpuPanel
    {
        public bool PortadaEnfocada = true;               // Ctrl+F3 -> Pantalla
        public Func<GpuFoco> GetFoco;                     // el letrero "12 canciones · 38 min" de la elegida
        public Func<GpuFicha> GetFicha;                   // la ficha rapida
        public float EmpujeCielo;                         // lo que se corre el cielo este cuadro (pasar de pagina)
        internal const float EnfoqueCrece = .12f;         // la elegida crece un 12 %
        internal const double EnfoqueMs = 170;            // en lo que tarda en crecer
        internal const float AtenuaOtras = .20f;          // y las demas bajan un 20 % de brillo
        private float enfoqueX = float.NaN, enfoqueY = float.NaN;
        private double enfoqueDesde, enfoqueUltMs;
        private float enfoque;                            // 0..1: cuanto crecio la elegida (0 si no toca)
        private float selX0, selY0, selW0, selH0;         // donde estaba la elegida ANTES de crecer
        private Bitmap enfoqueDe;                         // la portada de la que se saco el color
        private uint enfoqueColor = 0xFF8FB4FFu;          // el color de esa portada
        private float haloR = -1f, haloG, haloB;          // el color que se VE en el halo: va hacia enfoqueColor sin saltos
        private float relevoDesde;                        // cuanto habia crecido la que se deja (el relevo encoge desde ahi)
        private readonly float[] atenua = new float[MaxCells];
        private IntPtr focoTex; private int focoVer = -1, focoBw, focoBh; private float focoU = 1f, focoV = 1f;
        private IntPtr fichaTex; private int fichaVer = -1, fichaBw, fichaBh; private float fichaU = 1f, fichaV = 1f;
        private readonly D3D9.Vtx[] focoQuad = new D3D9.Vtx[6], fichaQuad = new D3D9.Vtx[6];

        // Crece desde el borde de abajo, centrado: hacia abajo no se mueve, que ahi va el nombre del disco.
        internal static void Enfoca(float s, ref float x, ref float y, ref float w, ref float h)
        {
            float nw = w * s, nh = h * s;
            x -= (nw - w) * .5f; y -= nh - h; w = nw; h = nh;
        }
        // 0 al llegar a la casilla; 1 a los EnfoqueMs, frenando al final.
        internal static float CurvaEnfoque(double ms)
        {
            if (!(ms > 0)) return 0f;
            if (ms >= EnfoqueMs) return 1f;
            float q = 1f - (float)(ms / EnfoqueMs);
            return 1f - q * q * q;
        }
        // Cuanto se acerca algo a su objetivo en 'dt' segundos (frenado exponencial: 95 % en un cuarto de segundo).
        internal static float Acerca(float v, float meta, float dt)
        {
            if (!(dt > 0f)) return v;
            float k = 1f - (float)Math.Exp(-dt * 12.0);
            return v + (meta - v) * k;
        }

        // EL COLOR DE UNA PORTADA, para su halo: la media de 36 puntos, pesando mas los que tienen color y luz (un
        // fondo negro o gris no deberia mandar), llevada a un tono vivo. Una portada sin color (gris, negra) se
        // queda con 'siNo'. Se calcula UNA vez por portada, no en cada cuadro. Devuelve false solo si no se pudo
        // leer (la portada estaba ocupada): entonces se vuelve a intentar en el cuadro siguiente.
        internal static bool ColorDePortada(Bitmap b, uint siNo, out uint color)
        {
            color = siNo;
            if (b == null || b.Width < 2 || b.Height < 2) return true;
            double r = 0, g = 0, bl = 0, peso = 0;
            try
            {
                for (int j = 0; j < 6; j++)
                    for (int i = 0; i < 6; i++)
                    {
                        Color c = b.GetPixel((int)((i + .5) * b.Width / 6), (int)((j + .5) * b.Height / 6));
                        int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B));
                        if (mx < 24) continue;
                        double sat = (mx - mn) / (double)mx, p = (0.08 + sat) * (mx / 255.0) * (c.A / 255.0);
                        r += c.R * p; g += c.G * p; bl += c.B * p; peso += p;
                    }
            }
            catch { return false; }
            if (peso <= 1e-6) return true;
            r /= peso; g /= peso; bl /= peso;
            double m = Math.Max(r, Math.Max(g, bl)), n = Math.Min(r, Math.Min(g, bl));
            if (m < 20 || (m - n) / m < .12) return true;   // casi gris: no hay color que dar
            // El MISMO tono, con toda la luz y al menos un 55 % de saturacion: cada canal conserva donde cae entre
            // el mas bajo y el mas alto (eso es el tono), y ese tramo se estira a lo que pide la saturacion.
            double s = Math.Max(.55, (m - n) / m);
            color = 0xFF000000u | ((uint)Canal(r, m, n, s) << 16) | ((uint)Canal(g, m, n, s) << 8) | (uint)Canal(bl, m, n, s);
            return true;
        }
        private static int Canal(double v, double m, double n, double s)
        {
            double c = 255.0 * (1.0 - s * (1.0 - (v - n) / (m - n)));
            return c <= 0 ? 0 : c >= 255 ? 255 : (int)(c + .5);
        }
        // Mezcla dos colores (solo RGB) en 't' (0 = el primero).
        internal static uint MezclaRgb(uint a, uint b, float t)
        {
            if (t <= 0f) return a & 0x00FFFFFFu;
            if (t >= 1f) return b & 0x00FFFFFFu;
            uint r = 0;
            for (int k = 0; k < 24; k += 8)
            {
                float ca = (a >> k) & 0xFF, cb = (b >> k) & 0xFF;
                r |= (uint)(ca + (cb - ca) * t + .5f) << k;
            }
            return r;
        }

        // PORTADA ENFOCADA: si toca, agranda el cubo elegido (y encoge el del relevo) y apunta el color de su halo.
        // Devuelve cuanto crecio (0..1); 0 = no toca (dentro de un disco, buscador, hojeando, o apagado).
        private float AplicaEnfoque(ref GpuCubeState cs)
        {
            double ahora = AhoraMs();
            float dt = enfoqueUltMs > 0 ? (float)Math.Max(0, Math.Min(.1, (ahora - enfoqueUltMs) / 1000.0)) : 0f;
            enfoqueUltMs = ahora;
            bool toca = PortadaEnfocada && ShowAlbum && cs.Pared && cs.Sel.Has;
            AtenuaCeldas(toca, dt);
            if (!toca) { enfoqueX = float.NaN; enfoque = 0f; relevoDesde = 0f; return 0f; }
            if (cs.Sel.X != enfoqueX || cs.Sel.Y != enfoqueY)
            {
                // Otra casilla: la que se deja encoge desde lo que llego a crecer (si el cursor corre, no llego al 12 %).
                relevoDesde = float.IsNaN(enfoqueX) ? 0f : enfoque;
                enfoqueX = cs.Sel.X; enfoqueY = cs.Sel.Y; enfoqueDesde = ahora;
            }
            float e = CurvaEnfoque(ahora - enfoqueDesde);
            selX0 = cs.Sel.X; selY0 = cs.Sel.Y; selW0 = cs.Sel.W; selH0 = cs.Sel.H;
            Enfoca(1f + EnfoqueCrece * e, ref cs.Sel.X, ref cs.Sel.Y, ref cs.Sel.W, ref cs.Sel.H);
            if (cs.Relay.Has) Enfoca(1f + EnfoqueCrece * relevoDesde * (1f - e), ref cs.Relay.X, ref cs.Relay.Y, ref cs.Relay.W, ref cs.Relay.H);
            if (!ReferenceEquals(cs.Sel.CoverA, enfoqueDe))
            {
                uint c;
                if (ColorDePortada(cs.Sel.CoverA, NebColor | 0xFF000000u, out c)) { enfoqueDe = cs.Sel.CoverA; enfoqueColor = c; }
            }
            // El color del halo va hacia el de la portada en medio segundo: "lo mas sonado" cambia de portada cada
            // pocos segundos y el halo no puede saltar de un color a otro.
            float cr = (enfoqueColor >> 16) & 255, cg = (enfoqueColor >> 8) & 255, cb = enfoqueColor & 255;
            if (haloR < 0f) { haloR = cr; haloG = cg; haloB = cb; }
            else
            {
                float k = (dt > 0f) ? 1f - (float)Math.Exp(-dt * 6.0) : 0f;
                haloR += (cr - haloR) * k; haloG += (cg - haloG) * k; haloB += (cb - haloB) * k;
            }
            enfoque = e;
            return e;
        }
        // Las demas casillas bajan de brillo poco a poco (y vuelven igual). 'atenua' va por puesto en la rejilla.
        private void AtenuaCeldas(bool toca, float dt)
        {
            for (int i = 0; i < gridN && i < atenua.Length; i++)
            {
                float meta = (toca && !gridBuf[i].Sel) ? AtenuaOtras : 0f;
                atenua[i] = Acerca(atenua[i], meta, dt);
                if (atenua[i] < .002f && meta == 0f) atenua[i] = 0f;
            }
        }
        // El color de una casilla: su alfa y, si esta apagada por la portada enfocada, menos luz. Sin apagar sale
        // exactamente el blanco de siempre.
        internal static uint ColorCelda(int alfa, float apagada)
        {
            if (!(apagada > 0f)) return ((uint)alfa << 24) | 0x00FFFFFFu;
            uint g = (uint)(255f * (1f - Math.Min(1f, apagada)) + .5f);
            return ((uint)alfa << 24) | (g << 16) | (g << 8) | g;
        }
        // El contorno y el halo de la elegida crecen con ella, desde el mismo punto (el centro de su borde de abajo).
        private void EnfocaCaja(ref float x, ref float y, ref float w, ref float h)
        {
            if (enfoque <= 0f) return;
            float s = 1f + EnfoqueCrece * enfoque, ax = selX0 + selW0 * .5f, ay = selY0 + selH0;
            x = ax + (x - ax) * s; y = ay + (y - ay) * s; w *= s; h *= s;
        }
        // Donde se VE ahora el cubo elegido (para que la portada que vuela al entrar al disco salga de ahi y no
        // de la casilla sin crecer, que seria un saltito).
        internal RectangleF Enfocado(RectangleF r)
        {
            if (enfoque <= 0f) return r;
            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            Enfoca(1f + EnfoqueCrece * enfoque, ref x, ref y, ref w, ref h);
            return new RectangleF(x, y, w, h);
        }
        // El halo de la elegida: el de siempre (color de la nebulosa, alfa 90) y, enfocada, el color de SU portada y
        // un poco mas fuerte. Sin enfocar sale bit a bit lo de antes.
        private uint HaloArgb()
        {
            float wo = Math.Max(0f, Math.Min(1f, WallOpacity));
            if (enfoque <= 0f) return ((uint)(90f * wo) << 24) | (NebColor & 0x00FFFFFFu);
            uint visto = 0xFF000000u | ((uint)(haloR + .5f) << 16) | ((uint)(haloG + .5f) << 8) | (uint)(haloB + .5f);
            return ((uint)((90f + 60f * enfoque) * wo) << 24) | MezclaRgb(NebColor, visto, enfoque);
        }
        // Texturas de la ficha y del letrero: ANTES de Begin (crear o bloquear una textura con la escena abierta no
        // vale en Direct3D 9). Solo se suben cuando cambia su version.
        private GpuFicha fichaCuadro; private GpuFoco focoCuadro;
        private void PreparaPantallaViva()
        {
            focoCuadro = (GetFoco != null) ? GetFoco() : new GpuFoco();
            if (focoCuadro.Has && focoCuadro.Bmp != null && (focoCuadro.Ver != focoVer || focoTex == IntPtr.Zero))
            { focoTex = Upload(focoTex, ref focoBw, ref focoBh, focoCuadro.Bmp, out focoU, out focoV); focoVer = focoCuadro.Ver; }
            fichaCuadro = (GetFicha != null) ? GetFicha() : new GpuFicha();
            if (fichaCuadro.Has && fichaCuadro.Bmp != null && (fichaCuadro.Ver != fichaVer || fichaTex == IntPtr.Zero))
            { fichaTex = Upload(fichaTex, ref fichaBw, ref fichaBh, fichaCuadro.Bmp, out fichaU, out fichaV); fichaVer = fichaCuadro.Ver; }
            if (EmpujeCielo != 0f) { stars.Empuja(EmpujeCielo, Width); EmpujeCielo = 0f; }
        }
        // El letrero, DENTRO del borde de abajo de la portada que crecio (aparece con ella), centrado con ella: debajo
        // va el nombre del disco, y ese no se tapa.
        private void DibujaFoco(GpuCubeState cs)
        {
            if (enfoque <= 0.01f || !cs.Sel.Has || !focoCuadro.Has || focoTex == IntPtr.Zero || focoCuadro.Bmp == null) return;
            float bw = focoCuadro.Bmp.Width, bh = focoCuadro.Bmp.Height;
            float x = cs.Sel.X + cs.Sel.W * .5f - bw * .5f, y = LetreroY(cs.Sel.Y, cs.Sel.H, bh);
            int a = (int)(enfoque * Math.Max(0f, Math.Min(1f, WallOpacity)) * 255f + .5f); if (a > 255) a = 255; else if (a < 0) a = 0;
            if (a <= 0) return;
            SetTexQuadA(focoQuad, x, y, x + bw, y + bh, focoU, focoV, ((uint)a << 24) | 0x00FFFFFFu);
            stars.DrawQuad(focoTex, focoQuad);
        }
        // Donde va el letrero: pegado al borde de abajo de la portada, por dentro.
        internal static float LetreroY(float y, float h, float alto) { return y + h - alto - Math.Max(3f, h * .04f); }
        private void DibujaFicha()
        {
            if (!fichaCuadro.Has || fichaTex == IntPtr.Zero || fichaCuadro.Bmp == null || fichaCuadro.Alpha <= 0.004f) return;
            int a = (int)(fichaCuadro.Alpha * Math.Max(0f, Math.Min(1f, WallOpacity)) * 255f + .5f); if (a > 255) a = 255;
            if (a <= 0) return;
            float x = fichaCuadro.X, y = fichaCuadro.Y;
            SetTexQuadA(fichaQuad, x, y, x + fichaCuadro.Bmp.Width, y + fichaCuadro.Bmp.Height, fichaU, fichaV, ((uint)a << 24) | 0x00FFFFFFu);
            stars.DrawQuad(fichaTex, fichaQuad);
        }
        private void SueltaPantallaViva()
        {
            if (focoTex != IntPtr.Zero) { D3D9.Release(focoTex); focoTex = IntPtr.Zero; }
            if (fichaTex != IntPtr.Zero) { D3D9.Release(fichaTex); fichaTex = IntPtr.Zero; }
            focoVer = fichaVer = -1; focoBw = focoBh = fichaBw = fichaBh = 0;
            enfoque = 0f; enfoqueX = float.NaN; enfoqueDe = null; haloR = -1f; relevoDesde = 0f;
        }
    }

    internal sealed partial class GpuStarfield
    {
        // PASAR DE PAGINA: el campo se corre 'dx' px, cada tamaño segun lo lejos que esta (el polvo menos, las
        // grandes mas: DerivaKind). Deja la deriva en [0, w), que es lo que espera BuildStarVerts.
        internal void Empuja(float dx, int w)
        {
            if (w <= 0 || float.IsNaN(dx) || float.IsInfinity(dx) || dx == 0f) return;
            for (int k = 0; k < 3; k++)
            {
                float v = (driftX[k] + dx * DerivaKind[k]) % w;
                if (v < 0f) v += w;
                if (v >= w) v = 0f;
                driftX[k] = v;
            }
        }
    }
#endif
}
