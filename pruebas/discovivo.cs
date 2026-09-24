using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA VIVA, TANDA 2: DENTRO DEL DISCO (Rocola/DiscoVivoGpu.cs; solo tarjeta grafica)
//
// Lo que vigila:
//  1) EL DIBUJO POR PROCESADOR NO CAMBIA. La lista del disco y la capsula de la cola, pintadas por el camino del
//     procesador, salen pixel a pixel iguales que las del perfil sin tarjeta grafica, aunque la rocola tenga
//     puestos los minutos de la cola o una cancion aterrizando. Y la lista vuelve a ser la de siempre en cuanto la
//     pinta el procesador (video que tapa, Ctrl+F11, tarjeta apagada).
//  2) LA LISTA GRANDE: unas nueve filas por pagina, letra mas grande, el circulito y la duracion a su medida, y la
//     pastilla del genero en todas las filas de la pagina o en ninguna.
//  3) EN COLA, CON LOS MINUTOS: la misma cuenta que el aviso de tu turno, cuando se sabe; se rehace sola cada vez
//     que cambia un minuto (y solo entonces); manda el titulo.
//  4) EL ECUALIZADOR: la pastilla de SONANDO deja el hueco de las barritas, el horneado apunta donde van, y la
//     tarjeta las pone encima solo con la pagina quieta y la que suena a la vista.
//  5) EL VINILO: su dibujo, cuanto asoma (sin tocar la lista), el cubo que se aparta, la entrada y salida sin
//     saltos, las 33 1/3 vueltas, y que se hornea antes de hacer falta y una vez por cancion.
//  6) EL BOLETO Y LA CAPSULA: sale calcado encima de su fila, vuela en arco y cae dentro de la capsula; la capsula
//     ensena el numero de antes hasta que llega y entonces salta, destella y el numero rueda.
//  7) Ctrl+F3 -> Pantalla -> Dentro del disco: los cinco interruptores caben, se guardan, se leen y se aplican.
//  8) Lo que cuesta cada cosa, medido aqui (Mono: para comparar entre ellas, no son los milisegundos del bar).
//
// Corre contra los DOS perfiles: sin tarjeta grafica solo existe la pagina de ajustes (se comprueba que se guarda
// y que no hay nada mas). Con ella, ademas, se compara contra el ejecutable sin tarjeta (rocola_cpu.exe).
class DiscoVivo
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, gpu, star, slT, pvT, trackT, itemT, cubeT, infoT, cajaT, bgT;
    static Assembly asmCpu;   // el perfil sin tarjeta, para comparar pixel a pixel
    static int fallos, oks;
    static string dirPng;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string Fm(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static FieldInfo Campo(Type t, string n) { for (Type x = t; x != null; x = x.BaseType) { FieldInfo fi = x.GetField(n, F | BindingFlags.DeclaredOnly); if (fi != null) return fi; } return null; }
    static object Get(object o, string n) { return Campo(o.GetType(), n).GetValue(o); }
    static void Set(object o, string n, object v) { Campo(o.GetType(), n).SetValue(o, v); }
    static object St(Type t, string n, params object[] a) { return t.GetMethod(n, F).Invoke(null, a); }
    static object Run(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static T Cte<T>(Type t, string n) { return (T)t.GetField(n, F).GetValue(null); }
    static Control Find(Control p, string name) { foreach (Control c in p.Controls) { if (c.Text == name) return c; Control r = Find(c, name); if (r != null) return r; } return null; }
    static Bitmap Liso(int r, int g, int b, int lado)
    {
        Bitmap bm = new Bitmap(lado, lado);
        using (Graphics gg = Graphics.FromImage(bm)) gg.Clear(Color.FromArgb(r, g, b));
        return bm;
    }
    // Cuantos pixeles son distintos entre dos imagenes del mismo tamaño (-1 si el tamaño no coincide).
    static int Distintos(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return -1;
        int n = 0;
        for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) n++;
        return n;
    }
    static int DistintosEnFila(Bitmap a, Bitmap b, int y0, int y1)
    {
        int n = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(a.Height, y1); y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) n++;
        return n;
    }

    // ------------------------------------------------------------------------------------------------
    // UNA LISTA DE DISCO ARMADA A MANO: doce canciones, dos pedidas (la 3 va primera y la 6 tercera), la 1 sonando.
    // ------------------------------------------------------------------------------------------------
    static string[] titulos = { "Volver Volver", "El Rey", "Hermoso Cariño", "Mujeres Divinas", "Por Tu Maldito Amor", "Acá Entre Nos",
                                "La Ley del Monte", "Estos Celos", "Lástima Que Seas Ajena", "Aunque Me Duela el Alma", "De Qué Manera Te Olvido", "Mátalas" };
    static string[] durs = { "3:02", "3:14", "2:41", "3:25", "3:37", "4:06", "3:11", "3:29", "3:44", "3:05", "3:22", "2:58" };
    static Control Lista(Type t, string[] tit, int sel, bool marcas, string genero)
    {
        Control sl = (Control)Activator.CreateInstance(t, true);
        sl.Size = new Size(556, 452); sl.BackColor = Color.FromArgb(40, 30, 20);
        sl.Font = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold);
        Set(sl, "RowH", 30);
        Set(sl, "GetCount", (Func<int>)(() => tit.Length));
        Set(sl, "GetText", (Func<int, string>)(i => tit[i]));
        Set(sl, "GetRight", (Func<int, string>)(i => durs[i % durs.Length]));
        Set(sl, "GetQueuePos", (Func<int, int>)(i => !marcas ? 0 : i == 2 ? 1 : i == 5 ? 3 : 0));
        Set(sl, "GetSonando", (Func<int, bool>)(i => marcas && i == 0));
        Set(sl, "GetGenero", (Func<int, string>)(i => genero));
        Set(sl, "Accent", Color.FromArgb(215, 120, 25));
        Set(sl, "PageStart", 0); Set(sl, "Sel", sel);
        return sl;
    }
    static Bitmap Pinta(Control sl)
    {
        Bitmap b = new Bitmap(sl.Width, sl.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b)) sl.GetType().GetMethod("RenderContent", F, null, new Type[] { typeof(Graphics) }, null).Invoke(sl, new object[] { g });
        return b;
    }
    static Bitmap PintaGpu(Control sl)
    {
        Bitmap b = new Bitmap(sl.Width, sl.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.Black);
        using (Graphics g = Graphics.FromImage(b)) slT.GetMethod("RenderTransparentGpuTint", F).Invoke(sl, new object[] { g, false, false, Color.White });
        return b;
    }
    static void Rehornea(Control sl) { Set(sl, "Datos", (int)Get(sl, "Datos") + 1); }
    static void Grande(Control sl, int vivo)
    {
        int fila = (int)St(form, "FilaGrande", sl.Height);
        Set(sl, "RowH", fila);
        sl.Font = new Font(FontFamily.GenericSansSerif, (float)St(form, "CuerpoGrande", fila), FontStyle.Bold);
        Set(sl, "Vivo", vivo);
    }
    static Control Capsula(Type t, string valor)
    {
        Control pv = (Control)Activator.CreateInstance(t, true);
        pv.Size = new Size(200, 38); pv.Font = new Font(FontFamily.GenericSansSerif, 9f);
        Set(pv, "Etiqueta", "En cola"); Set(pv, "Valor", valor); Set(pv, "ConPortadas", true);
        Set(pv, "Portadas", new Image[] { Liso(200, 60, 60, 40), Liso(60, 60, 200, 40), null }); Set(pv, "Acento", Color.FromArgb(140, 205, 255));
        return pv;
    }
    static Bitmap PintaCapsula(Control pv, bool paraGpu)
    {
        Bitmap b = new Bitmap(260, 60, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(14, 16, 30)); g.TranslateTransform(30, 11);
            pv.GetType().GetMethod(paraGpu ? "RenderContentGpu" : "RenderContent", F).Invoke(pv, new object[] { g });
        }
        return b;
    }

    // ================================================================================================
    //  1) EL DIBUJO POR PROCESADOR NO CAMBIA
    // ================================================================================================
    static void Procesador()
    {
        Console.WriteLine("\n  1) EL DIBUJO POR PROCESADOR NO CAMBIA\n");
        Type cpuSl = asmCpu.GetType("Rocola.Form1").GetNestedType("SongListView", F);
        Type cpuPv = asmCpu.GetType("Rocola.Form1").GetNestedType("PastillaView", F);
        Check(cpuSl != null && Campo(cpuSl, "Vivo") == null && Campo(slT, "Vivo") != null, "se compara contra el perfil SIN tarjeta grafica, que ni tiene nada de esto");
        int peor = 0, casos = 0;
        foreach (bool marcas in new bool[] { false, true })
            foreach (int sel in new int[] { -1, 3, 0 })
            {
                Control a = Lista(slT, titulos, sel, marcas, "Regional"), b = Lista(cpuSl, titulos, sel, marcas, "Regional");
                if (marcas)
                {   // la rocola con los minutos a mano y un horneado nuevo: con Vivo = 0 no puede cambiar nada
                    Set(a, "GetEspera", (Func<int, int>)(p => p == 1 ? 3 : 9)); Set(a, "EsperaVer", 7);
                }
                using (Bitmap pa = Pinta(a)) using (Bitmap pb = Pinta(b)) { peor = Math.Max(peor, Distintos(pa, pb)); casos++; }
                a.Dispose(); b.Dispose();
            }
        Check(peor == 0, "la lista del disco sale IDENTICA en " + casos + " casos (sonando, pedidas, elegida; " + (556 * 452) + " pixeles cada uno)");
        Control gv = Lista(slT, titulos, 3, true, "Regional"), cv = Lista(cpuSl, titulos, 3, true, "Regional");
        Set(gv, "Vivo", 7); Set(gv, "GetEspera", (Func<int, int>)(p => p == 1 ? 3 : 9));
        using (Bitmap pa = Pinta(gv)) using (Bitmap pb = Pinta(cv)) Check(Distintos(pa, pb) > 1000, "(y la comparacion ve la diferencia: con los extras puestos sale otra cosa)");
        gv.Dispose(); cv.Dispose();

        // LA CAPSULA DE LA COLA, con una cancion aterrizando: por procesador no se ve.
        Control c1 = Capsula(pvT, "4"), c2 = Capsula(cpuPv, "5");
        Run(c1, "Llega", 0); Set(c1, "Valor", "5");
        int peorC = 0;
        foreach (int d in new int[] { -300, 0, 40, 120, 250, 400 })
        {
            Set(c1, "llegaTick", Environment.TickCount - d);
            using (Bitmap pa = PintaCapsula(c1, false)) using (Bitmap pb = PintaCapsula(c2, false)) peorC = Math.Max(peorC, Distintos(pa, pb));
        }
        Check(peorC == 0, "la capsula de la cola, pintada por procesador, sale IDENTICA con una cancion aterrizando (6 momentos)");
        Set(c1, "llegaTick", Environment.TickCount - 60);
        using (Bitmap pa = PintaCapsula(c1, true)) using (Bitmap pb = PintaCapsula(c2, false)) Check(Distintos(pa, pb) > 100, "(y por la tarjeta si se ve la llegada)");

        // LA LISTA VUELVE A SER LA DE SIEMPRE en cuanto la pinta el procesador.
        object f = Codigo.FormSinEncender(form);
        Control sl = Lista(slT, titulos, 3, true, "Regional");
        Font normal = sl.Font;
        Set(f, "songList", sl);
        bool vuelve = true, sinTarjeta = true;
        for (int mask = 0; mask < 8; mask++)
        {
            Set(f, "listaGrande", (mask & 1) != 0); Set(f, "eqSonando", (mask & 2) != 0); Set(f, "colaMinutos", (mask & 4) != 0);
            Run(f, "AjustaDiscoVivo", true);
            Run(f, "AjustaDiscoVivo", false);
            if ((int)Get(sl, "Vivo") != 0 || (int)Get(sl, "RowH") != 30 || !ReferenceEquals(sl.Font, normal)) vuelve = false;
            // Sin tarjeta encendida, ni ListaPorGpu da permiso.
            if ((bool)Run(f, "ListaPorGpu")) sinTarjeta = false;
        }
        Check(vuelve, "con cualquier combinacion de interruptores, al pintarla el procesador vuelve a filas de 30 y a su letra de siempre (la misma letra, no otra igual)");
        Check(sinTarjeta, "y sin la tarjeta encendida nadie le da permiso para cambiar");
        Set(f, "gpuEnabled", true); Set(f, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(f, "gpuDiscTry", true);
        MethodInfo lpg = form.GetMethod("ListaPorGpu", F);
        bool si = (bool)lpg.Invoke(f, null);
        Set(f, "videoOn", true); bool video = (bool)lpg.Invoke(f, null);
        Set(f, "videoGpuOn", true); bool videoGpu = (bool)lpg.Invoke(f, null);
        Set(f, "videoFull", true); bool lleno = (bool)lpg.Invoke(f, null); Set(f, "videoFull", false); Set(f, "videoOn", false);
        Set(f, "attractOn", true); bool atrae = (bool)lpg.Invoke(f, null); Set(f, "attractOn", false);
        Set(f, "gpuDiscTry", false); bool f11 = (bool)lpg.Invoke(f, null);
        Check(si && !video && videoGpu && !lleno && !atrae && !f11,
              "la tarjeta compone el disco (y la lista cambia) salvo con un video que lo tapa, a pantalla completa, en modo atraccion o con Ctrl+F11");
        MethodBase syncM = form.GetMethod("SyncGpuFullScreen", F);
        List<string> pasos = Codigo.Repasa(syncM);
        int iAj = pasos.IndexOf("AjustaDiscoVivo"), iOcul = -1;
        for (int i = 0; i < pasos.Count; i++) if (pasos[i] == "set_Visible") { iOcul = i; }
        Check(iAj >= 0 && Codigo.Llama(syncM, "ListaPorGpu") && iAj < iOcul, "se decide en cada sincronizacion del panel, ANTES de esconderlo (el procesador ya la pinta como siempre)");
        List<string> apaga = Codigo.Repasa(form.GetMethod("ApplyAlbumGpu", F));
        int iStop = apaga.IndexOf("StopGpu"), iAj2 = apaga.LastIndexOf("AjustaDiscoVivo");
        Check(iStop > 0 && iAj2 > 0 && iAj2 < iStop, "y al apagar la tarjeta, tambien (antes de soltarla)");
        // Las piezas que solo existen con tarjeta: el procesador dibuja la capsula por su camino, la tarjeta por el suyo.
        MethodBase dco = form.GetMethod("DrawCtrlOverlay", F);
        Check(dco != null && Codigo.Llama(dco, "RenderContentGpu"), "la imagen de la tarjeta pinta la capsula con su llegada; la ventana del procesador, sin ella");
    }

    // ================================================================================================
    //  2) LA LISTA GRANDE
    // ================================================================================================
    static void ListaGrande()
    {
        Console.WriteLine("\n  2) LA LISTA GRANDE\n");
        bool nueve = true; string cuales = "";
        foreach (int h in new int[] { 404, 452, 540, 707, 720 })
        {
            int fila = (int)St(form, "FilaGrande", h), rpp = h / fila;
            float c = (float)St(form, "CuerpoGrande", fila);
            if (rpp != 9 || c <= 13f) nueve = false;
            cuales += h + " px: filas de " + fila + " y letra de " + Fm(c, "0.0") + "; ";
        }
        Info(cuales);
        Check(nueve, "nueve filas por pagina de 404 a 720 px de alto, con letra mas grande que la de siempre (13)");
        Check((int)St(form, "FilaGrande", 100) == 44 && (int)St(form, "FilaGrande", 3000) == 80, "sin bajar de 44 px por fila ni pasar de 80");

        object f = Codigo.FormSinEncender(form);
        Control sl = Lista(slT, titulos, 3, true, "Regional");
        Font normal = sl.Font;
        Set(f, "songList", sl);
        bool bits = true;
        for (int mask = 0; mask < 8; mask++)
        {
            Set(f, "listaGrande", (mask & 1) != 0); Set(f, "eqSonando", (mask & 2) != 0); Set(f, "colaMinutos", (mask & 4) != 0);
            Run(f, "AjustaDiscoVivo", true);
            int esperado = ((mask & 1) != 0 ? 1 : 0) | ((mask & 2) != 0 ? 2 : 0) | ((mask & 4) != 0 ? 4 : 0);
            int fila = (mask & 1) != 0 ? (int)St(form, "FilaGrande", sl.Height) : 30;
            if ((int)Get(sl, "Vivo") != esperado || (int)Get(sl, "RowH") != fila) bits = false;
            if ((mask & 1) != 0 && Math.Abs(sl.Font.SizeInPoints - (float)St(form, "CuerpoGrande", fila)) > .01f) bits = false;
            if ((mask & 1) == 0 && !ReferenceEquals(sl.Font, normal)) bits = false;
        }
        Check(bits, "con la tarjeta, cada interruptor pone lo suyo en la lista (y la lista grande, su fila y su letra)");
        Check(Get(sl, "GetEspera") != null, "y la lista sabe a quien preguntar los minutos de cada puesto");
        // Otro alto de fila es otro reparto de paginas: se llega a la pagina de la elegida SIN hojear.
        Set(f, "listaGrande", false); Run(f, "AjustaDiscoVivo", true);
        Control panel = (Control)Activator.CreateInstance(bgT, true); panel.SetBounds(14, 106, 996, 540);
        IList items = (IList)Get(f, "songItems"); for (int i = 0; i < titulos.Length; i++) items.Add(titulos[i]);
        Set(f, "panelSongs", panel); Set(f, "songSel", 10); Run(f, "FillSongPage");
        using (Bitmap b0 = Pinta(sl)) { }
        int antes = (int)Get(sl, "PageStart");
        Set(f, "listaGrande", true); Run(f, "AjustaDiscoVivo", true);
        PropertyInfo desl = slT.GetProperty("Sliding", F);
        Check(antes == 0 && (int)Get(sl, "PageStart") == 9 && !(bool)desl.GetValue(sl, null), "al cambiar el alto de fila se llega a la pagina de la elegida (de la 1 a la 2) SIN hojear: no te has movido");
        // Encender una idea rehace la pagina aunque no haya cambiado nada mas (entra en la firma del horneado).
        Control fr = Lista(slT, titulos, 3, true, "Regional");
        Bitmap p0 = Pinta(fr); Set(fr, "Vivo", 2); Bitmap p1 = Pinta(fr);
        Set(fr, "Vivo", 4); Set(fr, "GetEspera", (Func<int, int>)(q => 3)); Bitmap p2 = Pinta(fr);
        Set(fr, "GetEspera", (Func<int, int>)(q => 7)); Bitmap p3 = Pinta(fr); Set(fr, "EsperaVer", 1); Bitmap p4 = Pinta(fr);
        Check(Distintos(p0, p1) > 0 && Distintos(p1, p2) > 0, "encender una idea rehace la pagina en el acto (entra en la firma de lo horneado)");
        Check(Distintos(p2, p3) == 0 && Distintos(p3, p4) > 0, "y los minutos nuevos, en cuanto sube su version (no antes: la pagina no se rehace porque si)");
        List<MethodBase> ui = Codigo.MetodosConAyudantes(form, "UiSig");
        Check(Codigo.LeeCampo(ui, "Vivo") && Codigo.LeeCampo(ui, "RowH") && Codigo.LeeCampo(ui, "EsperaVer"), "la imagen de la interfaz tambien se entera (su firma lee lo mismo)");

        // El circulito y la duracion, a la medida de la letra.
        Control g = Lista(slT, titulos, 3, false, "");
        Grande(g, 1);
        using (Bitmap b = Pinta(g))
        {
            int fila = (int)Get(g, "RowH");
            float rad = (float)St(slT, "RadioGrande", fila), xt = (float)Run(g, "XTexto");
            Check(rad > 11f && Math.Abs(xt - (16 + 2 * rad + 10)) < .01f, "el circulito del numero crece con la fila (radio " + Fm(rad, "0.0") + " px, el de siempre 11)");
            int primero = -1;
            for (int x = 0; x < b.Width && primero < 0; x++)
                for (int y = fila + 8; y < 2 * fila - 8; y++) { Color c = b.GetPixel(x, y); if (x > 2 * rad + 16 && c.R > 225 && c.G > 225 && c.B > 225) { primero = x; break; } }
            Check(primero >= xt - 2 && primero <= xt + 8, "y el titulo empieza donde dice XTexto (" + primero + " px; se usa para que el boleto salga calcado)");
            using (Graphics gg = Graphics.FromImage(b))
            {
                int dw = (int)Run(g, "AnchoDuracion", gg);
                int necesita = 0;
                using (Font fg = new Font(g.Font.FontFamily, g.Font.SizeInPoints, FontStyle.Bold)) necesita = (int)Math.Ceiling(gg.MeasureString("59:59", fg).Width);
                Check(dw >= necesita && dw >= 58, "la duracion tiene el sitio que pide su letra (" + dw + " px para \"59:59\", que mide " + necesita + ")");
            }
            b.Save(Path.Combine(dirPng, "lista_grande.png"));
        }

        // EL GENERO: EN TODA LA PAGINA O EN NINGUNA FILA.
        string[] cortos = { "Uno", "Dos", "Tres", "Cuatro", "Cinco", "Seis", "Siete", "Ocho", "Nueve", "Diez", "Once", "Doce" };
        Control con = Lista(slT, cortos, -1, false, "Regional"), sin = Lista(slT, cortos, -1, false, "");
        Grande(con, 1); Grande(sin, 1);
        using (Bitmap a = Pinta(con)) using (Bitmap b = Pinta(sin))
        {
            int fila = (int)Get(con, "RowH"), filasCon = 0;
            for (int r = 0; r < 9; r++) if (DistintosEnFila(a, b, r * fila, (r + 1) * fila) > 50) filasCon++;
            Check(!(bool)Get(con, "generosFuera") && filasCon == 9, "con titulos que caben, el genero sale en las nueve filas");
        }
        string[] uno = (string[])cortos.Clone(); uno[4] = "Un titulo larguisimo que no cabe entero con su pastilla de genero al lado";
        Control con2 = Lista(slT, uno, -1, false, "Regional"), sin2 = Lista(slT, uno, -1, false, "");
        Grande(con2, 1); Grande(sin2, 1);
        using (Bitmap a = Pinta(con2)) using (Bitmap b = Pinta(sin2))
            Check((bool)Get(con2, "generosFuera") && Distintos(a, b) == 0, "con UNO solo que no cabe, ninguna fila lleva genero (una sin genero entre otras pareceria de otra cosa)");
        // La fila elegida se hornea aparte (su propia pieza); tiene que decidir lo mismo que la pagina.
        Set(con2, "Sel", 7);
        using (Bitmap capa = (Bitmap)Run(con2, "BakeGpuListLayer", true)) { }
        Check((bool)Get(con2, "generosFuera"), "y la fila elegida, horneada aparte, decide lo mismo que su pagina");
        Control nor = Lista(slT, uno, -1, false, "Regional"), nor2 = Lista(slT, uno, -1, false, "");
        using (Bitmap a = Pinta(nor)) using (Bitmap b = Pinta(nor2))
            Check(DistintosEnFila(a, b, 0, 30) > 50, "(la lista de tamaño normal no cambia: su genero sale como siempre)");
    }

    // ================================================================================================
    //  3) EN COLA, CON LOS MINUTOS
    // ================================================================================================
    static object Cancion(string ruta)
    {
        object t = Activator.CreateInstance(trackT, true);
        Set(t, "Path", ruta); Set(t, "Title", Path.GetFileNameWithoutExtension(ruta)); Set(t, "Artist", "Artista");
        object it = Activator.CreateInstance(itemT, true);
        Set(it, "Song", t);
        return it;
    }
    static void Minutos()
    {
        Console.WriteLine("\n  3) EN COLA, CON LOS MINUTOS\n");
        MethodInfo me = form.GetMethod("MinutosEspera", F), tt = form.GetMethod("TextoTurno", F);
        Func<double, int> M = s => (int)me.Invoke(null, new object[] { s });
        Check(M(-1) == -1 && M(double.NaN) == -1 && M(0) == 0 && M(59.9) == 0 && M(60) == 1 && M(89.9) == 1 && M(90) == 2 && M(600) == 10,
              "-1 si no se sabe, 0 si falta menos de un minuto, y los minutos redondeados");
        bool igual = true;
        for (double s = 0; s < 4000; s += 7.3)
        {
            string aviso = (string)tt.Invoke(null, new object[] { 3, s });
            int m = M(s);
            string esperado = (m == 0) ? "menos de 1 min" : ("en ~" + m + " min");
            if (!aviso.EndsWith(esperado)) { igual = false; Info("distinto a " + Fm(s, "0.0") + " s: \"" + aviso + "\" y " + m); break; }
        }
        Check(igual, "la MISMA cuenta que el aviso de tu turno (548 esperas comparadas)");
        Check((string)St(form, "TextoCola", 3, 9) == "EN COLA 3 · en ~9 min" && (string)St(form, "TextoCola", 1, 0) == "EN COLA 1 · ya casi" && (string)St(form, "TextoCola", 2, -1) == "EN COLA 2",
              "\"EN COLA 3 · en ~9 min\", \"EN COLA 1 · ya casi\", y sin saberlo \"EN COLA 2\" como siempre");

        object f = Codigo.FormSinEncender(form);
        Control sl = Lista(slT, titulos, 3, true, "Regional");
        Set(f, "songList", sl); Set(sl, "Vivo", 4);
        IList cola = (IList)Get(f, "queue"); IDictionary dur = (IDictionary)Get(f, "durCache");
        string[] rutas = { "a.mp3", "b.mp3", "c.mp3", "d.mp3" };
        foreach (string r in rutas) cola.Add(Cancion(r));
        dur["a.mp3"] = 180f; dur["b.mp3"] = 200f; dur["d.mp3"] = 150f;   // c no se sabe
        int ver0 = (int)Get(sl, "EsperaVer"), datos0 = (int)Get(sl, "Datos");
        Run(f, "ActualizaEsperas", true);
        int[] e = (int[])Get(f, "esperaMin");
        Check(e.Length == 4 && e[0] == 0 && e[1] == 3 && e[2] == 6 && e[3] == -1,
              "sin nada sonando: la primera \"ya casi\", la segunda en ~3, la tercera en ~6 y la de detras de una sin duracion, sin minutos");
        Check((int)Get(sl, "EsperaVer") == ver0 + 1 && (int)Get(sl, "Datos") > datos0, "y la lista se vuelve a hornear con los minutos nuevos");
        Run(f, "ActualizaEsperas", true);
        Check((int)Get(sl, "EsperaVer") == ver0 + 1, "si no cambia ningun minuto, NO se vuelve a hornear");
        dur["c.mp3"] = 100f;
        Run(f, "ActualizaEsperas", false);
        Check(((int[])Get(f, "esperaMin"))[3] == -1, "entre vuelta y vuelta (medio segundo) no se recalcula");
        Set(f, "esperaTick", Environment.TickCount - 600);
        Run(f, "ActualizaEsperas", false);
        Check(((int[])Get(f, "esperaMin"))[3] == 8 && (int)Get(sl, "EsperaVer") == ver0 + 2, "y pasado el medio segundo si: sabida la duracion que faltaba, la cuarta sale en ~8");
        Check((int)Run(f, "EsperaDePuesto", 2) == 3 && (int)Run(f, "EsperaDePuesto", 9) == -1 && (int)Run(f, "EsperaDePuesto", 0) == -1, "la lista pregunta por puesto (1 = el primero); fuera de la cola, no se sabe");
        object video = Activator.CreateInstance(itemT, true); Set(video, "IsVideo", true); Set(video, "VideoPath", "v.mp4");
        Set(f, "nowPlaying", video);
        Run(f, "ActualizaEsperas", true);
        e = (int[])Get(f, "esperaMin");
        Check(e[0] == -1 && e[3] == -1, "con un VIDEO sonando no se sabe cuanto le falta: ninguna dice minutos (no se inventan)");
        Set(f, "nowPlaying", null);
        Set(sl, "Vivo", 1); int v2 = (int)Get(sl, "EsperaVer");
        Set(f, "esperaTick", 0); dur["a.mp3"] = 999f; Run(f, "ActualizaEsperas", true);
        Check((int)Get(sl, "EsperaVer") == v2, "con los minutos apagados no se calcula nada");
        Check(Codigo.Llama(form.GetMethod("RefrescaCola", F), "ActualizaEsperas"), "al cambiar la cola se recalcula en el acto, antes de hornear la lista");
        Check(Codigo.Llama(form.GetMethod("DiscoVivoTic", F), "ActualizaEsperas") && Codigo.Llama(form.GetMethod("AnimTimer_Tick", F), "DiscoVivoTic"), "y el reloj de la pantalla lo repasa");

        // MANDA EL TITULO.
        Control g = Lista(slT, titulos, -1, true, "Regional");
        Grande(g, 5); Set(g, "GetEspera", (Func<int, int>)(p => p == 1 ? 3 : 9));
        using (Bitmap b = Pinta(g)) b.Save(Path.Combine(dirPng, "lista_minutos.png"));
        Check((bool)Get(g, "generosFuera"), "(en esta pagina, con la letra grande, los titulos largos se quedan sin genero)");
        using (Bitmap b = new Bitmap(10, 10)) using (Graphics gg = Graphics.FromImage(b))
        {
            int fila = (int)Get(g, "RowH");
            float xt = (float)Run(g, "XTexto");
            MethodInfo plan = slT.GetMethod("PlanFilaGpu", F);
            int dw = (int)Run(g, "AnchoDuracion", gg);
            object[] a = { gg, 2, 1, true, false, new Rectangle(0, 2 * fila, g.Width, fila), dw, xt, 24, "Regional" };
            string t = (string)plan.Invoke(g, a);
            Check(t == "EN COLA 1 · en ~3 min", "\"Hermoso Cariño\" pedida la primera: \"" + t + "\"");
            Set(g, "GetText", (Func<int, string>)(i => "Una cancion con un titulo tan largo que con los minutos casi no se leeria nada de el"));
            a = new object[] { gg, 2, 1, true, false, new Rectangle(0, 2 * fila, g.Width, fila), dw, xt, 24, "Regional" };
            t = (string)plan.Invoke(g, a);
            Check(t == "EN COLA 1 · en ~3 min", "con un titulo larguisimo tambien salen, si al titulo le queda al menos el 30 % de su sitio");
            Rectangle estrecha = new Rectangle(0, 0, 330, fila);
            a = new object[] { gg, 2, 1, true, true, estrecha, dw, xt, 24, "Regional" };
            t = (string)plan.Invoke(g, a);
            Check(t == "EN COLA 1", "y si ni asi (fila estrecha, sonando y pedida a la vez), la de siempre: \"" + t + "\"");
            Set(g, "Vivo", 1);
            a = new object[] { gg, 2, 1, true, false, new Rectangle(0, 2 * fila, g.Width, fila), dw, xt, 24, "Regional" };
            t = (string)plan.Invoke(g, a);
            Check(t == "EN COLA 1", "con los minutos apagados, la lista grande dice lo de siempre");
            Set(g, "Vivo", 5);
            // En la lista de tamaño normal, para que quepan los minutos se calla el genero de esa fila.
            Control n = Lista(slT, titulos, -1, true, "Regional");
            Set(n, "Vivo", 4); Set(n, "GetEspera", (Func<int, int>)(p => p == 1 ? 3 : 9));
            Set(n, "GetText", (Func<int, string>)(i => "Una cancion de titulo largo pero no tanto"));
            using (Bitmap bn = Pinta(n)) { }
            object[] an = { gg, 2, 1, true, false, new Rectangle(0, 60, n.Width, 30), 58, 16f + 22f + 10f, 20, "Regional" };
            t = (string)plan.Invoke(n, an);
            Check(t == "EN COLA 1 · en ~3 min" && an[9] == null, "en la lista normal, si solo caben callando el genero de esa fila, se calla");
        }
    }

    // ================================================================================================
    //  4) EL ECUALIZADOR
    // ================================================================================================
    static void Ecualizador()
    {
        Console.WriteLine("\n  4) EL ECUALIZADOR\n");
        Control g = Lista(slT, titulos, 3, true, "Regional");
        Grande(g, 3);
        using (Bitmap b = Pinta(g))
        {
            int idx = (int)Get(g, "eqIdx"), barra = (int)Get(g, "eqBarra"), hueco = (int)Get(g, "eqHueco"), fila = (int)Get(g, "RowH");
            float ex = (float)Get(g, "eqX"), ew = (float)Get(g, "eqW"), eh = (float)Get(g, "eqH");
            Check(idx == 0 && ew == 4 * barra + 3 * hueco && eh >= 8 && ex > 0 && ex + ew < g.Width, "el horneado apunta donde van las cuatro barritas: fila " + idx + ", " + Fm(ew, "0") + "x" + Fm(eh, "0") + " px");
            int texto = 0, hueco0 = 0;
            float y0 = (fila - eh) / 2f;
            for (int x = (int)ex; x < (int)(ex + ew); x++) for (int y = (int)y0; y < (int)(y0 + eh); y++) { Color c = b.GetPixel(x, y); if (c.R > 200 && c.G > 150 && c.B < 190) hueco0++; }
            for (int x = (int)(ex + ew); x < (int)(ex + ew + 60); x++) for (int y = (int)y0; y < (int)(y0 + eh); y++) { Color c = b.GetPixel(x, y); if (c.R > 200 && c.G > 150 && c.B < 190) texto++; }
            Check(hueco0 == 0 && texto > 20, "su hueco queda vacio en la pastilla (sin triangulito) y SONANDO va justo a su derecha");
            using (Graphics gg = Graphics.FromImage(b))
            {
                float ancho = (float)Run(g, "AnchoSonando", gg, (int)Math.Round(eh / .56f));
                object[] a = { gg, 0, 300f, fila * .5f, (int)Math.Round(eh / .56f) };
                int ocupa = (int)slT.GetMethod("PastillaSonandoEq", F).Invoke(g, a);
                Check(Math.Abs(ocupa - 8 - ancho) < .5f && Math.Abs((float)a[2] - (300f - ancho)) < .5f, "la pastilla mide lo mismo cuando se reparte la fila que cuando se pinta");
            }
            b.Save(Path.Combine(dirPng, "lista_eq.png"));
        }
        Set(g, "PageStart", 9); Set(g, "Sel", 9);
        using (Bitmap b = Pinta(g)) Check((int)Get(g, "eqIdx") == -1, "en una pagina donde no esta la que suena no queda apuntado ningun sitio");

        MethodInfo alt = gpu.GetMethod("AlturaBarra", F);
        Func<int, double, float, float> A = (k, s, n) => (float)alt.Invoke(null, new object[] { k, s, n });
        bool rango = true, sube = true;
        for (int k = 0; k < 4; k++) for (double s = 0; s < 3; s += .013)
            {
                float a0 = A(k, s, 0f), a1 = A(k, s, .5f), a2 = A(k, s, 1f);
                if (a0 < .22f - 1e-4f || a2 > 1f + 1e-4f || a0 > 1f) rango = false;
                if (a1 < a0 || a2 < a1) sube = false;
            }
        float arriba = 1f, tope = 0f;
        for (int k = 0; k < 4; k++) for (double s = 0; s < 3; s += .013) { arriba = Math.Min(arriba, A(k, s, 1f)); tope = Math.Max(tope, A(k, s, 1f)); }
        Check(rango && sube && arriba >= .8f && tope == 1f && A(0, 0, float.NaN) >= .22f, "cada barra entre el 22 % y el alto entero, y la musica la empuja hacia arriba (a tope nunca baja del " + Fm(arriba * 100, "0") + " %)");
        Check(Math.Abs(A(0, .3, 0) - A(1, .3, 0)) > .05f && Math.Abs(A(2, .3, 0) - A(3, .3, 0)) > .05f, "y cada una a su ritmo: no suben y bajan a la vez");

        // Quien pide las barras.
        object f = Codigo.FormSinEncender(form);
        Control panel = (Control)Activator.CreateInstance(bgT, true); panel.SetBounds(14, 106, 996, 540);
        Control sl = Lista(slT, titulos, 3, true, "Regional"); Grande(sl, 2); sl.Location = new Point(428, 80);
        using (Bitmap b = Pinta(sl)) { }
        IList items = (IList)Get(f, "songItems"); for (int i = 0; i < titulos.Length; i++) items.Add(titulos[i]);
        Set(f, "panelSongs", panel); Set(f, "songList", sl); Set(f, "gpuMode", 3);
        MethodInfo be = form.GetMethod("BuildEqState", F);
        object q = be.Invoke(f, null);
        float qx = (float)Get(q, "X"), qy = (float)Get(q, "Y"), qh = (float)Get(q, "H");
        int filaG = (int)Get(sl, "RowH");
        Check((bool)Get(q, "Has") && Math.Abs(qx - (14 + 428 + (float)Get(sl, "eqX"))) < .01f && Math.Abs(qy + qh * .5f - (106 + 80 + filaG * .5f)) < .6f,
              "con la pagina quieta, las barras van en la pastilla de la que suena, en pantalla");
        Set(f, "searchOpen", true);
        Check(!(bool)Get(be.Invoke(f, null), "Has"), "con el buscador encima, no");
        Set(f, "searchOpen", false); Set(f, "gpuMode", 1);
        Check(!(bool)Get(be.Invoke(f, null), "Has"), "fuera del disco, no");
        Set(f, "gpuMode", 3); Set(sl, "slideTick", Environment.TickCount);
        Check(!(bool)Get(be.Invoke(f, null), "Has"), "mientras la pagina se desliza, no (volveran al llegar)");
        Set(sl, "slideTick", -1); Set(sl, "Vivo", 1);
        Check(!(bool)Get(be.Invoke(f, null), "Has"), "con el ecualizador apagado, no");
        Set(sl, "Vivo", 2); Set(sl, "GetSonando", (Func<int, bool>)(i => false));
        Check(!(bool)Get(be.Invoke(f, null), "Has"), "y si la que suena ya es otra (cambio de cancion antes de rehornear), tampoco");

        // El panel: sin tarjeta de verdad, pero con toda su cuenta.
        Control p = (Control)Activator.CreateInstance(gpu, true);
        Set(p, "stars", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(star));
        Set(p, "lunaBeat", 1f);
        MethodInfo paso = gpu.GetMethod("PasoEq", F);
        for (int i = 0; i < 6; i++) paso.Invoke(p, new object[] { 1000.0 + i * 16.0, .016f });
        float[] eq = (float[])Get(p, "eqAlto");
        Check(eq[0] > .9f, "con un golpe de graves la primera barra sube casi entera en 100 ms (" + Fm(eq[0], "0.00") + ")");
        Set(p, "lunaBeat", 0f);
        for (int i = 0; i < 6; i++) paso.Invoke(p, new object[] { 1100.0 + i * 16.0, .016f });
        eq = (float[])Get(p, "eqAlto");
        Check(eq[0] > .4f && eq[0] < .95f, "y baja mas despacio de lo que subio: marca el golpe sin temblar (" + Fm(eq[0], "0.00") + " a los 100 ms)");
        MethodBase rf = gpu.GetMethod("RenderFrame", F);
        List<string> orden = Codigo.Repasa(rf);
        int iEq = orden.IndexOf("DibujaEq"), iPieza = orden.LastIndexOf("DrawSongPage");
        Check(iEq > iPieza && iPieza > 0, "la tarjeta las pone DESPUES de la lista y de su fila elegida (si no, las taparian)");
    }

    // ================================================================================================
    //  5) EL VINILO
    // ================================================================================================
    static void Vinilo()
    {
        Console.WriteLine("\n  5) EL VINILO\n");
        using (Bitmap tapa = Liso(40, 200, 90, 80))
        using (Bitmap b = new Bitmap(210, 210, PixelFormat.Format32bppPArgb))
        {
            St(form, "PintaVinilo", b, tapa, Color.FromArgb(215, 120, 25));
            bool borde = true;
            for (int i = 0; i < 210; i++) for (int k = 0; k < 2; k++)
                    if (b.GetPixel(i, k).A != 0 || b.GetPixel(i, 209 - k).A != 0 || b.GetPixel(k, i).A != 0 || b.GetPixel(209 - k, i).A != 0) borde = false;
            Check(borde, "deja dos pixeles transparentes alrededor (la tarjeta lo dibuja girado y con filtro)");
            float R = 105f - Cte<float>(form, "ViniloAire"), et = R * Cte<float>(form, "EtiquetaVinilo");
            Color cuerpo = b.GetPixel(105 + (int)(R * .8f), 105), etiqueta = b.GetPixel(105 + (int)(et * .5f), 105 - (int)(et * .3f)), agujero = b.GetPixel(105, 105);
            Color aro = b.GetPixel(105 + (int)Math.Round(et), 105);
            Check(cuerpo.A == 255 && cuerpo.R < 60 && cuerpo.G < 60 && cuerpo.B < 70, "el disco es negro y opaco");
            Check(Math.Abs(etiqueta.R - 40) < 12 && Math.Abs(etiqueta.G - 200) < 12 && Math.Abs(etiqueta.B - 90) < 12, "la etiqueta lleva la portada");
            Check(aro.R > 150 && aro.G > 70 && aro.B < 90 && agujero.R < 30, "con un aro del color del genero y el agujero del centro");
            b.Save(Path.Combine(dirPng, "vinilo.png"));
        }
        MethodInfo corre = form.GetMethod("CorreVinilo", F);
        bool nunca = true;
        float margen = Cte<float>(form, "MargenVinilo"), asoma = Cte<float>(form, "AsomaMax");
        foreach (float lado in new float[] { 151f, 310f, 321f, 558f })
            foreach (float listaX in new float[] { 140f, 238f, 442f, 759f, 2000f })
            {
                float cx = 26f + lado * .64f, r = lado * .33f;
                float c = (float)corre.Invoke(null, new object[] { cx, r, lado, listaX });
                if (c < 0f || c > lado * asoma + .01f) nunca = false;
                if (c > 0f && cx + c + r > listaX - margen + .01f) nunca = false;
            }
        Check(nunca, "el vinilo nunca llega a " + Fm(margen, "0") + " px de la lista (no la tapa), ni sale mas del " + Fm(asoma * 100, "0") + " % del cubo, ni hacia atras");
        float c768 = (float)corre.Invoke(null, new object[] { 225f, 102.3f, 310f, 442f });
        Info("en 1024x768: el cubo mide 310 px, el vinilo 205 y asoma " + Fm(c768, "0") + " px");
        MethodInfo cede = form.GetMethod("CuboCorre", F);
        bool cabe = true;
        foreach (float lado in new float[] { 151f, 310f, 558f }) foreach (float cajaW in new float[] { lado / .78f, lado + 4f })
            {
                float c = (float)cede.Invoke(null, new object[] { lado, cajaW });
                if (c < 0f || c > lado * Cte<float>(form, "CuboCede") + .01f || c > (cajaW - lado) * .5f + 10.01f) cabe = false;
            }
        Check(cabe, "el cubo se aparta como mucho un " + Fm(Cte<float>(form, "CuboCede") * 100, "0") + " % de su lado, y sin salirse de su caja mas de 10 px");

        MethodInfo pv = gpu.GetMethod("PasoVinilo", F), cv = gpu.GetMethod("CurvaVinilo", F);
        float s = 0f; int ms = 0;
        while (s < 1f && ms < 5000) { s = (float)pv.Invoke(null, new object[] { s, true, .001f }); ms++; }
        float sale = Cte<float>(gpu, "ViniloSaleMs"), entra = Cte<float>(gpu, "ViniloEntraMs");
        Check(Math.Abs(ms - sale) <= 2, "sale en " + ms + " ms");
        int msIn = 0; while (s > 0f && msIn < 5000) { s = (float)pv.Invoke(null, new object[] { s, false, .001f }); msIn++; }
        Check(Math.Abs(msIn - entra) <= 2, "y se guarda en " + msIn + " ms");
        s = .5f; float s2 = (float)pv.Invoke(null, new object[] { s, false, .016f });
        Check(Math.Abs(s2 - (.5f - 16f / entra)) < .001f && (float)pv.Invoke(null, new object[] { .3f, true, 0f }) == .3f && (float)pv.Invoke(null, new object[] { .3f, true, float.NaN }) == .3f,
              "si cambia de idea a medio camino vuelve desde donde esta (sin saltos), y un reloj raro no lo mueve");
        bool suave = true; float ant = 0f, maxPaso = 0f;
        for (int i = 0; i <= 1000; i++) { float c = (float)cv.Invoke(null, new object[] { i / 1000f }); if (c < ant) suave = false; maxPaso = Math.Max(maxPaso, c - ant); ant = c; }
        Check(suave && ant == 1f && (float)cv.Invoke(null, new object[] { 0f }) == 0f && maxPaso < .0016f
              && (float)cv.Invoke(null, new object[] { .05f }) < .02f && (float)cv.Invoke(null, new object[] { .95f }) > .98f, "arranca y frena suave, en los dos sentidos");
        double w = Cte<double>(gpu, "ViniloRadS");
        Check(Math.Abs(w * 60.0 / (2 * Math.PI) - 100.0 / 3.0) < 1e-9, "gira a 33 1/3 vueltas por minuto (" + Fm(w, "0.000") + " rad/s), y gira con lo que ha salido: arranca y para como un tocadiscos");

        // Si suena el disco abierto.
        object f = Codigo.FormSinEncender(form);
        IList items = (IList)Get(f, "songItems");
        object it = Cancion("x/02 Cancion.mp3"); object tk = Get(it, "Song");
        items.Add(Get(Cancion("x/01 Otra.mp3"), "Song")); items.Add(tk);
        Set(f, "nowPlaying", it); Set(f, "sonandoKey", "X/02 CANCION.MP3"); Set(f, "colaVer", 1);
        MethodInfo sue = form.GetMethod("DiscoAbiertoSuena", F);
        Check((bool)sue.Invoke(f, null), "sabe cuando suena una cancion del disco abierto (sin distinguir mayusculas, como el resto)");
        Set(f, "sonandoKey", "otra.mp3");
        Check((bool)sue.Invoke(f, null), "no lo vuelve a buscar en cada cuadro: solo cuando cambia la cola o el disco");
        Set(f, "colaVer", 2);
        Check(!(bool)sue.Invoke(f, null), "y al cambiar, se entera");
        object vid = Activator.CreateInstance(itemT, true); Set(vid, "IsVideo", true);
        Set(f, "nowPlaying", vid); Set(f, "sonandoKey", "X/02 CANCION.MP3"); Set(f, "colaVer", 3);
        Check(!(bool)sue.Invoke(f, null), "un VIDEO no saca vinilo");

        // Se hornea antes de hacer falta, una vez por cancion; los surcos, una vez por tamaño.
        object g = Codigo.FormSinEncender(form);
        Control panel = (Control)Activator.CreateInstance(bgT, true); panel.SetBounds(14, 106, 996, 540);
        Control caja = (Control)Activator.CreateInstance(cajaT, true); caja.SetBounds(12, 52, 398, 398);
        Control mini = (Control)Activator.CreateInstance(cajaT, true); mini.SetBounds(14, 658, 96, 96);
        Bitmap t1 = Liso(200, 40, 40, 60), t2 = Liso(40, 40, 200, 60);
        Set(mini, "Cover", t1);
        Set(g, "panelSongs", panel); Set(g, "cubeBox", caja); Set(g, "miniCube", mini); Set(g, "viniloDisco", true);
        Set(g, "nowPlaying", Cancion("y/01 A.mp3"));
        MethodInfo prep = form.GetMethod("PreparaVinilo", F);
        prep.Invoke(g, null);
        int v1 = (int)Get(g, "viniloVer"); Bitmap surcos = (Bitmap)Get(g, "viniloSurcos"), vbmp = (Bitmap)Get(g, "viniloBmp");
        prep.Invoke(g, null); prep.Invoke(g, null);
        Check(v1 == 1 && (int)Get(g, "viniloVer") == 1 && vbmp != null && vbmp.Width == (int)Math.Ceiling((int)(398 * .78) * .33f * 2f + 5f),
              "se hornea al sonar la cancion, a la medida del cubo, y no otra vez en cada vuelta del reloj");
        Set(mini, "Cover", t2); prep.Invoke(g, null);
        Check((int)Get(g, "viniloVer") == 2 && ReferenceEquals(Get(g, "viniloSurcos"), surcos), "otra portada: otra etiqueta, pero los surcos (lo caro) son los mismos");
        Set(g, "nowPlaying", vid); Set(mini, "Cover", t1); prep.Invoke(g, null);
        Check((int)Get(g, "viniloVer") == 2, "con un video no se hornea nada");
        Set(g, "viniloDisco", false); Set(g, "nowPlaying", Cancion("y/01 A.mp3")); prep.Invoke(g, null);
        Check((int)Get(g, "viniloVer") == 2, "ni con el vinilo apagado");
        Control pg = (Control)Activator.CreateInstance(gpu, true);
        Set(g, "gpuPanel", pg); Set(g, "viniloDisco", true); Set(g, "nowPlaying", Cancion("y/09 Otra.mp3")); Set(mini, "Cover", t1);
        Set(pg, "viniloSal", .5f);   // a medio guardar: el disco abierto (sin canciones) ya no suena
        prep.Invoke(g, null);
        bool quieto = (int)Get(g, "viniloVer") == 2;
        Set(pg, "viniloSal", 0f); prep.Invoke(g, null);
        Check(quieto && (int)Get(g, "viniloVer") == 3, "mientras se guarda porque ya no suena este disco, su etiqueta no cambia a medio camino (la nueva, ya guardado)");
        Check(!Codigo.Llama(form.GetMethod("DiscoVivoTic", F), "PreparaVinilo"), "el reloj ya no prepara vinilos retirados");

        // Lo que se entrega al panel.
        Set(g, "viniloDisco", true); Set(g, "gpuMode", 3);
        Control sl = Lista(slT, titulos, 3, true, ""); sl.Location = new Point(428, 80); Set(g, "songList", sl);
        MethodInfo bv = form.GetMethod("BuildViniloState", F);
        object est = bv.Invoke(g, null);
        float R2 = (float)Get(est, "R"), cx2 = (float)Get(est, "Cx"), co = (float)Get(est, "Corre");
        Check((bool)Get(est, "Has") && Math.Abs(R2 - 398 * .78f * .33f) < 1f && Math.Abs(cx2 + co + R2 - (14 + 428 - margen)) < 1f,
              "dentro del disco se entrega con su medida y su sitio: en 1024x768 asoma hasta " + Fm(margen, "0") + " px de la lista");
        Set(g, "gpuMode", 1);
        Check(!(bool)Get(bv.Invoke(g, null), "Has"), "fuera del disco, no");

        // El panel: el cubo se aparta, el vinilo va detras.
        Control p = (Control)Activator.CreateInstance(gpu, true);
        Set(p, "stars", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(star));
        Set(p, "viniloTex", new IntPtr(1));
        Set(est, "Cubo", 40f); Set(p, "viniloCuadro", est); Set(p, "sceneMode", 3); Set(p, "viniloSal", 1f);
        object cs = Activator.CreateInstance(cubeT); object sel = Activator.CreateInstance(infoT);
        Set(sel, "Has", true); Set(sel, "X", 100f); Set(sel, "W", 310f); Set(sel, "H", 310f); Set(cs, "Sel", sel);
        object[] a = { cs };
        MethodInfo dv = gpu.GetMethod("DibujaVinilo", F);
        dv.Invoke(p, a);
        Check(Math.Abs((float)Get(Get(a[0], "Sel"), "X") - 60f) < .01f && Math.Abs((float)Get(p, "CuboCorrido") - 40f) < .01f, "con el vinilo fuera, el cubo se aparta hacia su lado lo que diga (y el panel lo apunta)");
        Set(p, "viniloSal", 0f); a = new object[] { cs }; dv.Invoke(p, a);
        Check(Math.Abs((float)Get(Get(a[0], "Sel"), "X") - 100f) < .01f && (float)Get(p, "CuboCorrido") == 0f, "guardado, el cubo esta en su sitio");
        Set(p, "viniloSal", 1f); Set(p, "sceneMode", 1); a = new object[] { cs }; dv.Invoke(p, a);
        Check(Math.Abs((float)Get(Get(a[0], "Sel"), "X") - 100f) < .01f && (float)Get(p, "CuboCorrido") == 0f, "y fuera del disco no toca nada");
        Set(p, "viniloTex", IntPtr.Zero);
        object h = Codigo.FormSinEncender(form);
        Control ph = (Control)Activator.CreateInstance(bgT, true); ph.SetBounds(14, 106, 996, 540);
        Control ch = (Control)Activator.CreateInstance(cajaT, true); ch.SetBounds(12, 52, 398, 398);
        Set(h, "panelSongs", ph); Set(h, "cubeBox", ch); Set(h, "gpuPanel", p);
        MethodInfo dcr = form.GetMethod("DiscCubeRect", F);
        Set(p, "CuboCorrido", 40f);
        object[] ang = { 0.0 };
        RectangleF rc = (RectangleF)dcr.Invoke(h, ang);
        Set(p, "CuboCorrido", 0f);
        RectangleF rq = (RectangleF)dcr.Invoke(h, ang);
        Check(Math.Abs(rq.X - rc.X - 40f) < .01f && rq.Width == rc.Width, "el viaje al salir del disco parte de donde se VE el cubo (apartado), sin saltito");
        List<string> orden = Codigo.Repasa(gpu.GetMethod("RenderFrame", F));
        int iv = orden.IndexOf("DibujaVinilo"), ifo = orden.IndexOf("DibujaFoco");
        bool cuboDespues = false; for (int i = iv + 1; i < ifo && iv >= 0; i++) if (orden[i] == "DrawCube") cuboDespues = true;
        Check(iv < 0 && !cuboDespues, "el renderizador ya no llama al vinilo");
        MethodInfo pdv = gpu.GetMethod("PreparaDiscoVivo", F);
        Set(p, "viniloSal", .7f); Set(p, "discoUltMs", 0.0);
        pdv.Invoke(p, null);
        Check((float)Get(p, "viniloSal") == 0f, "al salir del disco se da por guardado: al volver a entrar sale otra vez");
    }

    // ================================================================================================
    //  6) EL BOLETO Y LA CAPSULA
    // ================================================================================================
    static void Boleto()
    {
        Console.WriteLine("\n  6) EL BOLETO Y LA CAPSULA\n");
        MethodInfo pb = gpu.GetMethod("PasoBoleto", F);
        float aparece = Cte<float>(form, "BoletoAparece"), llega = Cte<float>(form, "BoletoLlega");
        RectangleF de = new RectangleF(470, 300, 262, 46), hasta = new RectangleF(690, 672, 200, 38);
        Func<float, float[]> P = u => { object[] a = { u, de, hasta, 0f, 0f, 0f, 0f, 0f }; pb.Invoke(null, a); return new float[] { (float)a[3], (float)a[4], (float)a[5], (float)a[6], (float)a[7] }; };
        float[] p0 = P(0f), p1 = P(aparece), p2 = P(llega), p3 = P(1f);
        float dx = de.X + de.Width / 2, dy = de.Y + de.Height / 2, ax = hasta.X + hasta.Width / 2, ay = hasta.Y + hasta.Height / 2;
        Check(p0[0] == dx && p0[1] == dy && p0[2] == 1f && p0[3] == 0f && p1[3] == 1f && p1[2] == 1f && p1[0] == dx,
              "aparece QUIETO y a su tamaño encima de su fila (la fila se pone verde y se despega)");
        float sFin = p2[2];
        Check(Math.Abs(p2[0] - ax) < .01f && Math.Abs(p2[1] - ay) < .01f && de.Width * sFin <= hasta.Width * .6f + .5f && de.Height * sFin <= hasta.Height * .85f + .5f && p2[4] == 0f,
              "toca la capsula en su centro, derecho y encogido para caber DENTRO (" + Fm(de.Width * sFin, "0") + "x" + Fm(de.Height * sFin, "0") + " px)");
        Check(p3[3] == 0f && p3[2] < sFin, "y se hunde en ella apagandose");
        float maxPaso = 0f, alto = dy, xAnt = dx, yAnt = dy; bool dentro = true;
        for (int i = 1; i <= 1000; i++)
        {
            float[] q = P(i / 1000f);
            maxPaso = Math.Max(maxPaso, (float)Math.Sqrt((q[0] - xAnt) * (q[0] - xAnt) + (q[1] - yAnt) * (q[1] - yAnt)));
            xAnt = q[0]; yAnt = q[1]; alto = Math.Min(alto, q[1]);
            if (q[1] < 0 || q[0] < 0 || q[0] > 1024) dentro = false;
        }
        Check(dentro && alto < dy - 10f, "vuela en arco: sube un poco (" + Fm(dy - alto, "0") + " px) y cae en la capsula, sin salirse de la pantalla");
        Check(maxPaso < 5f, "sin saltos: nunca mas de " + Fm(maxPaso, "0.0") + " px por milesima del vuelo");
        float[] temprano = P(aparece + .05f);
        Check(Math.Abs(temprano[0] - dx) > 3f || Math.Abs(temprano[1] - dy) > 3f, "se despega de la fila en cuanto aparece (no se queda esperando)");
        int ms = Cte<int>(form, "BoletoMs");
        Info("dura " + ms + " ms; aterriza a los " + (int)(ms * llega) + " ms");

        Font letra = new Font(FontFamily.GenericSansSerif, 16.7f, FontStyle.Bold);
        Size t = (Size)St(form, "TamanoBoleto", 50, "Hermoso Cariño", letra, 556);
        Size tl = (Size)St(form, "TamanoBoleto", 50, "Un titulo larguisimo que no cabria entero en el boleto ni queriendo", letra, 556);
        Check(t.Height == 46 && t.Width >= 3 * t.Height && tl.Width <= (int)(556 * .62f), "mide casi una fila de alto y lo que pida su titulo, sin pasar del 62 % de la lista");
        using (Bitmap b = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb))
        {
            St(form, "PintaBoleto", b, Liso(90, 60, 200, 40), "Hermoso Cariño", letra);
            bool borde = true;
            for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, 0).A != 0 || b.GetPixel(x, b.Height - 1).A != 0) borde = false;
            for (int y = 0; y < b.Height; y++) if (b.GetPixel(0, y).A != 0 || b.GetPixel(b.Width - 1, y).A != 0) borde = false;
            float xt = (float)St(form, "TextoBoletoX", t.Height);
            int primero = -1;
            for (int x = 0; x < b.Width && primero < 0; x++) for (int y = 8; y < b.Height - 8; y++) { Color c = b.GetPixel(x, y); if (x > 6 && c.R > 230 && c.G > 230 && c.B > 230) { primero = x; break; } }
            Check(borde, "con su margen transparente (vuela girado)");
            Check(primero >= xt - 1 && primero <= xt + 6, "su titulo empieza donde dice TextoBoletoX (" + primero + " px)");
            b.Save(Path.Combine(dirPng, "boleto.png"));
        }

        // El vuelo entero, con una rocola armada sin encender.
        object f = Codigo.FormSinEncender(form);
        Control panel = (Control)Activator.CreateInstance(bgT, true); panel.SetBounds(14, 106, 996, 540);
        Control sl = Lista(slT, titulos, 2, true, ""); Grande(sl, 7); sl.Location = new Point(428, 80);
        Control pc = Capsula(pvT, "4"); pc.SetBounds(690, 672, 200, 38);
        Control mini = (Control)Activator.CreateInstance(cajaT, true); mini.SetBounds(14, 658, 96, 96);
        Control caja = (Control)Activator.CreateInstance(cajaT, true); caja.SetBounds(12, 52, 398, 398); Set(caja, "Cover", Liso(90, 60, 200, 60));
        IList items = (IList)Get(f, "songItems"); for (int i = 0; i < titulos.Length; i++) items.Add(titulos[i]);
        Set(f, "panelSongs", panel); Set(f, "songList", sl); Set(f, "pastCola", pc); Set(f, "miniCube", mini); Set(f, "cubeBox", caja);
        Set(f, "gpuEnabled", true); Set(f, "gpuMode", 3); Set(f, "vueloCola", true); Set(f, "songSel", 2);
        Set(f, "gpuPanel", Activator.CreateInstance(gpu, true));
        Set(f, "nowPlaying", Cancion("z/01.mp3"));
        MethodInfo vb = form.GetMethod("VuelaBoleto", F);
        object pedida = Cancion("z/03 Hermoso Cariño.mp3");
        int ahora = Environment.TickCount;
        bool salio = (bool)vb.Invoke(f, new object[] { pedida });
        object bol = Get(f, "boleto");
        RectangleF bde = (RectangleF)Get(bol, "De"), ba = (RectangleF)Get(bol, "A");
        float xtexto = (float)Run(sl, "XTexto"), xb = (float)St(form, "TextoBoletoX", (int)bde.Height);
        int fila = (int)Get(sl, "RowH");
        Check(salio && (bool)Get(bol, "Has") && Math.Abs(bde.X + xb - (14 + 428 + xtexto)) < .01f && Math.Abs(bde.Y + bde.Height / 2 - (106 + 80 + 2.5f * fila)) < .01f,
              "sale CALCADO encima de su fila: su titulo, con la misma letra, justo encima del de la fila");
        Check(ba == new RectangleF(690, 672, 200, 38), "y va a la capsula de la cola");
        int tick = (int)Get(pc, "llegaTick");
        Check(Math.Abs(tick - (ahora + (int)(ms * llega))) <= 20 && (string)Get(pc, "llegaDe") == "4", "la capsula sabe cuando llega (a los " + (int)(ms * llega) + " ms) y guarda el numero de antes");
        Set(f, "nowPlaying", null); Set(pc, "llegaTick", 0);
        vb.Invoke(f, new object[] { pedida });
        Check((RectangleF)Get(Get(f, "boleto"), "A") == new RectangleF(14, 658, 96, 96) && (int)Get(pc, "llegaTick") == 0,
              "si no sonaba nada la cancion no espera: vuela a la tarjeta de lo que suena (y la capsula no se mueve)");
        Set(f, "nowPlaying", Cancion("z/01.mp3"));
        Set(f, "gpuMode", 1); Check(!(bool)vb.Invoke(f, new object[] { pedida }), "fuera del disco no hay boleto (en la pared vuela la portada, como siempre)");
        Set(f, "gpuMode", 3); Set(f, "vueloCola", false); Check(!(bool)vb.Invoke(f, new object[] { pedida }), "con el interruptor apagado, tampoco");
        Set(f, "vueloCola", true); Set(f, "songSel", 11); Check(!(bool)vb.Invoke(f, new object[] { pedida }), "ni si su fila no esta a la vista");
        Set(f, "songSel", 2); Set(sl, "slideTick", Environment.TickCount); Check(!(bool)vb.Invoke(f, new object[] { pedida }), "ni con la pagina deslizandose");
        Set(sl, "slideTick", -1);
        MethodInfo bbs = form.GetMethod("BuildBoletoState", F);
        double reloj = (double)St(gpu, "AhoraMs");
        object bb = Get(f, "boleto"); Set(bb, "Has", true); Set(bb, "Desde", reloj - 200); Set(f, "boleto", bb);
        Check((bool)Get(bbs.Invoke(f, null), "Has"), "mientras vuela se entrega al panel");
        bb = Get(f, "boleto"); Set(bb, "Desde", reloj - ms - 100); Set(f, "boleto", bb);
        Check(!(bool)Get(bbs.Invoke(f, null), "Has"), "y al acabar deja de entregarse");
        MethodBase add = form.GetMethod("AddItem", F);
        Check(Codigo.Llama(add, "VuelaBoleto") && Codigo.Llama(add, "VuelaALaCola"), "poner una cancion hace volar el boleto (dentro del disco) o la portada (en la pared)");
        List<string> orden = Codigo.Repasa(gpu.GetMethod("RenderFrame", F));
        int ib = orden.IndexOf("DibujaBoleto"), it = orden.LastIndexOf("DrawCube");
        Check(ib > it, "vuela por encima de todo (despues del viaje de las portadas)");

        // LA CAPSULA.
        Control cap = Capsula(pvT, "4");
        Run(cap, "Llega", 500);
        Set(cap, "Valor", "5");
        Run(cap, "Llega", 500); Set(cap, "Valor", "6");
        Check((string)Get(cap, "llegaDe") == "4", "si piden otra antes de que aterrice la primera, se sigue viendo el numero de antes de la primera");
        PropertyInfo anima = pvT.GetProperty("Animando", F);
        Check(!(bool)anima.GetValue(cap, null), "mientras el boleto vuela la capsula no se mueve (su imagen no se rehace en cada cuadro)");
        using (Bitmap a = PintaCapsula(cap, true)) using (Bitmap b = PintaCapsula(Capsula(pvT, "4"), true))
            Check(Distintos(a, b) == 0, "y ensena el numero de antes, tal cual");
        Set(cap, "llegaTick", Environment.TickCount - 100);
        Check((bool)anima.GetValue(cap, null), "al aterrizar, se mueve: salta, destella y el numero rueda");
        using (Bitmap a = PintaCapsula(cap, true)) using (Bitmap b = PintaCapsula(Capsula(pvT, "6"), true))
        {
            Check(Distintos(a, b) > 100, "(a los 100 ms se ve distinta: esta saltando)");
            a.Save(Path.Combine(dirPng, "capsula_llega.png"));
        }
        Set(cap, "llegaTick", Environment.TickCount - 700);
        Check(!(bool)anima.GetValue(cap, null), "y a los 700 ms ya esta quieta");
        using (Bitmap a = PintaCapsula(cap, true)) using (Bitmap b = PintaCapsula(Capsula(pvT, "6"), true))
            Check(Distintos(a, b) == 0 && (int)Get(cap, "llegaTick") == 0, "con el numero nuevo, igual que si nada");
        Check(Codigo.Llama(pvT.GetProperty("Animando", F).GetGetMethod(true), "LlegaViva") && Codigo.Llama(form.GetMethod("UiBotSig", F), "get_Paso"),
              "mientras se mueve, la imagen de la franja de abajo se rehace (si no, se veria congelada)");
    }

    // ================================================================================================
    //  7) CTRL+F3 -> PANTALLA -> DENTRO DEL DISCO
    // ================================================================================================
    static void Ajustes()
    {
        Console.WriteLine("\n  7) CTRL+F3 -> PANTALLA -> DENTRO DEL DISCO\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_discovivo_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        form.GetField("carpetaDatos", F).SetValue(null, dir);
        object owner = Codigo.FormSinEncender(form);
        string[] nom = { "Lista grande (GPU)", "Ecualizador en la que suena (GPU)", "En cola, con los minutos (GPU)", "Ver adónde va la canción (GPU)" };
        string[] campo = { "listaGrande", "eqSonando", "colaMinutos", "vueloCola" };
        string[] viejos = { "portadaEnfocada", "fichaDisco", "paginaDesliza", "letrasLupa" };
        Control sl = (Control)Activator.CreateInstance(slT, true);
        sl.Size = new Size(556, 452); sl.Font = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold); Set(sl, "RowH", 30);
        Set(owner, "songList", sl);
        if (gpu != null) { Set(owner, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(owner, "gpuEnabled", true); Set(owner, "gpuDiscTry", true); }
        using (Font f = new Font("Segoe UI", 9))
        using (Panel page = (Panel)form.GetMethod("PaginaPantalla", F).Invoke(owner, new object[] { f, f, f, Color.White, Color.Gray, Color.Gray }))
        {
            Control[] sw = new Control[4]; bool estan = true;
            for (int i = 0; i < 4; i++) { sw[i] = Find(page, nom[i]); if (sw[i] == null) estan = false; }
            Check(estan && Find(page, "Portada enfocada (GPU)") != null, "la pestaña Pantalla tiene los cuatro ajustes al lado de los cuatro de la pared");
            if (!estan) return;
            // Las tarjetas van en HOJAS (paneles lisos, una a la vista; tanda 3): dentro de cada hoja, lado a lado y sin pisarse.
            bool dentro = true, noSePisan = true, juntas = false; int fondo = 0, hojas = 0;
            foreach (Control hoja in page.Controls)
            {
                fondo = Math.Max(fondo, hoja.Bottom);
                if (hoja.Right > page.Width || hoja.Left < 0) dentro = false;
                if (hoja.GetType() != typeof(Panel)) continue;
                hojas++;
                List<Rectangle> tarjetas = new List<Rectangle>(); bool pared = false, disco = false;
                foreach (Control card in hoja.Controls)
                {
                    if (card.Left < 0 || card.Top < 0 || card.Right > hoja.Width || card.Bottom > hoja.Height) dentro = false;
                    foreach (Rectangle r in tarjetas) if (r.IntersectsWith(card.Bounds)) noSePisan = false;
                    tarjetas.Add(card.Bounds);
                    if (Find(card, "Portada enfocada (GPU)") != null) pared = true;
                    if (Find(card, nom[0]) != null) disco = true;
                    foreach (Control x in card.Controls)
                    {
                        if (x.Left < 0 || x.Top < 0 || x.Right > card.Width || x.Bottom > card.Height) dentro = false;
                        foreach (Control d in card.Controls) if (x != d && x.Bounds.IntersectsWith(d.Bounds)) dentro = false;
                    }
                }
                if (pared && disco && tarjetas.Count == 2) juntas = true;
            }
            Check(dentro && noSePisan && juntas && hojas >= 2 && fondo <= page.Height, "la pared y dentro del disco, lado a lado en su hoja; todo cabe sin taparse (lo mas bajo llega a " + fondo + " de " + page.Height + ")");
            PropertyInfo marcado = sw[0].GetType().GetProperty("Checked", F);
            bool guarda = true, aplica = true;
            for (int mask = 15; mask >= 0; mask--)
            {
                for (int i = 0; i < 4; i++) marcado.SetValue(sw[i], (mask & (1 << i)) != 0, null);
                form.GetMethod("SaveConfigPrefs", F).Invoke(owner, null);
                Codigo.EsperaGuardado(form, owner);
                object leido = Codigo.FormSinEncender(form); form.GetMethod("LoadConfigPrefs", F).Invoke(leido, null);
                for (int i = 0; i < 4; i++) if ((bool)Get(leido, campo[i]) != ((mask & (1 << i)) != 0) || (bool)Get(owner, campo[i]) != ((mask & (1 << i)) != 0)) guarda = false;
                if (gpu != null)
                {
                    int vivo = (int)Get(sl, "Vivo");
                    if (((vivo & 1) != 0) != ((mask & 1) != 0) || ((vivo & 2) != 0) != ((mask & 2) != 0) || ((vivo & 4) != 0) != ((mask & 4) != 0)) aplica = false;
                }
            }
            Check(guarda, "las 16 combinaciones se guardan en rocola_config.txt y se leen igual");
            if (gpu != null) Check(aplica, "y la lista del disco se entera al momento (con la tarjeta dibujando el disco)");
            Control reset = Find(page, "Pantalla como de fabrica");
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool todos = true;
            for (int i = 0; i < 4; i++) if (!(bool)Get(owner, campo[i]) || !(bool)marcado.GetValue(sw[i], null)) todos = false;
            foreach (string v in viejos) if (!(bool)Get(owner, v)) todos = false;
            Check(reset != null && todos, "\"Pantalla como de fabrica\" vuelve a encender las ocho");
        }
        try { Directory.Delete(dir, true); } catch { }
        if (gpu != null)
            Check(Codigo.Llama(form.GetMethod("AplicaEfectosGpu", F), "AjustaDiscoVivo"), "AplicaEfectosGpu se lo dice a la lista");
        else
            Check(Campo(slT, "Vivo") == null && form.GetMethod("BuildViniloState", F) == null && form.GetMethod("VuelaBoleto", F) == null && Campo(pvT, "llegaTick") == null,
                  "sin tarjeta grafica no hay nada de esto en el dibujo: solo se guardan los interruptores");
    }

    // ================================================================================================
    //  8) LO QUE CUESTA (medido aqui, con Mono: sirve para comparar, no son los milisegundos del bar)
    // ================================================================================================
    static double Mide(int n, Action a)
    {
        for (int i = 0; i < 3; i++) a();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < n; i++) a();
        return sw.Elapsed.TotalMilliseconds / n;
    }
    static void Coste()
    {
        Console.WriteLine("\n  8) LO QUE CUESTA\n");
        Control n = Lista(slT, titulos, 3, true, "Regional"), g = Lista(slT, titulos, 3, true, "Regional");
        Grande(g, 7); Set(g, "GetEspera", (Func<int, int>)(p => p == 1 ? 3 : 9));
        Bitmap lienzo = new Bitmap(556, 452);
        Graphics gl = Graphics.FromImage(lienzo);
        MethodInfo rc = slT.GetMethod("RenderContent", F);
        double tn = Mide(40, () => { Rehornea(n); rc.Invoke(n, new object[] { gl }); });
        double tg = Mide(40, () => { Rehornea(g); rc.Invoke(g, new object[] { gl }); });
        double tq = Mide(200, () => rc.Invoke(g, new object[] { gl }));
        Info("hornear la pagina: la de siempre " + Fm(tn, "0.00") + " ms (15 filas), la grande con todo " + Fm(tg, "0.00") + " ms (9 filas); sin cambios " + Fm(tq * 1000, "0") + " us");
        Check(tg < tn * 2.5 + 1, "la lista grande con todo no cuesta mas del doble y medio de hornear que la de siempre");
        MethodInfo plan = slT.GetMethod("PlanPaginaGpu", F);
        double tp = Mide(40, () => plan.Invoke(g, new object[] { gl, 9, 12, 556, 55f, 24 }));
        Info("decidir el genero de toda la pagina: " + Fm(tp, "0.00") + " ms (solo al hornear)");

        object f = Codigo.FormSinEncender(form);
        Control caja = (Control)Activator.CreateInstance(cajaT, true); caja.SetBounds(12, 52, 398, 398);
        Control mini = (Control)Activator.CreateInstance(cajaT, true);
        Set(f, "cubeBox", caja); Set(f, "miniCube", mini); Set(f, "panelSongs", Activator.CreateInstance(bgT, true)); Set(f, "viniloDisco", true);
        Set(f, "nowPlaying", Cancion("c/01.mp3"));
        Bitmap[] tapas = { Liso(200, 40, 40, 300), Liso(40, 200, 40, 300) };
        int k = 0;
        MethodInfo prep = form.GetMethod("PreparaVinilo", F);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Set(mini, "Cover", tapas[0]); prep.Invoke(f, null);
        double tPrimero = sw.Elapsed.TotalMilliseconds;
        double tCancion = Mide(20, () => { Set(mini, "Cover", tapas[(++k) & 1]); prep.Invoke(f, null); });
        double tQuieto = Mide(500, () => prep.Invoke(f, null));
        Info("el vinilo: la primera vez " + Fm(tPrimero, "0.0") + " ms (los surcos, una vez por tamaño), por cancion " + Fm(tCancion, "0.00") + " ms, y en cada vuelta del reloj " + Fm(tQuieto * 1000, "0") + " us");
        Check(tCancion < tPrimero, "por cancion cuesta menos que la primera vez: los surcos no se vuelven a hornear");
        Font letra = new Font(FontFamily.GenericSansSerif, 16.7f, FontStyle.Bold);
        Bitmap bol = new Bitmap(262, 46, PixelFormat.Format32bppPArgb);
        double tb = Mide(40, () => St(form, "PintaBoleto", bol, tapas[0], "Hermoso Cariño", letra));
        Info("el boleto: " + Fm(tb, "0.00") + " ms, una vez por cancion pedida");

        // Por cuadro: lo que hace el panel con las tres cosas.
        Control pan = (Control)Activator.CreateInstance(gpu, true);
        Set(pan, "stars", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(star));
        MethodInfo paso = gpu.GetMethod("PasoEq", F), pb = gpu.GetMethod("PasoBoleto", F);
        double tEq = Mide(2000, () => paso.Invoke(pan, new object[] { 1000.0, .016f }));
        RectangleF de = new RectangleF(470, 300, 262, 46), hasta = new RectangleF(690, 672, 200, 38);
        double tBo = Mide(2000, () => pb.Invoke(null, new object[] { .5f, de, hasta, 0f, 0f, 0f, 0f, 0f }));
        Info("por cuadro: barritas " + Fm(tEq * 1000, "0.0") + " us, vuelo del boleto " + Fm(tBo * 1000, "0.0") + " us (con la llamada por reflexion)");
        Check(tEq < .05 && tBo < .05, "cada cuadro son unas cuentas: ni se hornea ni se sube nada");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        form = asm.GetType("Rocola.Form1"); gpu = asm.GetType("Rocola.GpuPanel"); star = asm.GetType("Rocola.GpuStarfield");
        slT = form.GetNestedType("SongListView", F); pvT = form.GetNestedType("PastillaView", F);
        trackT = form.GetNestedType("Track", F); itemT = form.GetNestedType("PlayItem", F);
        cubeT = asm.GetType("Rocola.GpuCubeState"); infoT = asm.GetType("Rocola.GpuCubeInfo"); cajaT = form.GetNestedType("CubeBox", F); bgT = form.GetNestedType("BgPanel", F);
        dirPng = Path.Combine(Path.GetTempPath(), "rocola_discovivo_png"); Directory.CreateDirectory(dirPng);
        Console.WriteLine("  perfil: " + (gpu != null ? "con tarjeta grafica" : "sin tarjeta grafica (solo la pestaña de ajustes)"));
        if (gpu != null)
        {
            // El perfil sin tarjeta, cargado aparte (mismo nombre, otro ejecutable): contra el se comparan los pixeles.
            string cpu = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args.Length > 0 ? args[0] : "rocola.exe")), "rocola_cpu.exe");
            if (File.Exists(cpu)) asmCpu = Assembly.Load(File.ReadAllBytes(cpu));
            Check(asmCpu != null && asmCpu.GetType("Rocola.GpuPanel") == null, "esta el ejecutable sin tarjeta grafica para comparar (" + Path.GetFileName(cpu) + ")");
            if (asmCpu != null) Procesador();
            ListaGrande(); Minutos(); Ecualizador(); Vinilo(); Boleto();
        }
        Ajustes();
        if (gpu != null) Coste();
        Console.WriteLine("\n  imagenes para mirar: " + dirPng);
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }
}
