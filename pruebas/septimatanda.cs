using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// SEPTIMA TANDA (12-10-2026), lo que pidio el usuario con fotos:
//  1) En pantallas bajas (1024x768) la casilla de AYUDA perdia su palabra: ahora el encabezado cede hasta un 15 % (se
//     achica solo) y las instrucciones van con menos aire hasta 1180 de ancho.
//  2) Dentro de un disco se veian el latido de BUSCAR y la interrogacion que bota de AYUDA: solo en la pared.
//  3) El menu del asistente con los controles DIBUJADOS en la opcion elegida (flechas y VERDE), que van con la
//     seleccion, y un latido verde que se desliza de una opcion a otra; la leyenda de abajo, con sus botones.
//  4) La estrella mas caricaturesca: estira y aplasta, anticipacion, saltitos y resortes (sin cara).
//  5) Tope a ROJO + ARRIBA y ROJO + ABAJO: abrir el buscador o la ayuda no se puede cientos de veces seguidas.
//  6) La barra de progreso neon, redisenada como un tubo (resplandor que se desvanece, nucleo y chispa en la punta).
// Formulario, casillas y tarjeta aislados: sin ventana, sin musica y sin Direct3D.
class SeptimaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form, gpu;
    static object Get(object o, string n) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, B | BindingFlags.DeclaredOnly); if (f != null) return f.GetValue(o); } return null; }
    static void Set(object o, string n, object v) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, B | BindingFlags.DeclaredOnly); if (f != null) { f.SetValue(o, v); return; } } throw new Exception("falta " + n); }
    static object Run(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in (o as Type ?? o.GetType()).GetMethods(B))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(o is Type ? null : o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + n);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true); gpu = asm.GetType("Rocola.GpuPanel", false);
        Tope(asm);
        if (gpu == null || form.GetNestedType("AyudaView", B) == null)
            Check(form.GetMethod("TuboNeon", B) == null && form.GetMethod("PintaPanelAsistente", B) == null, "sin tarjeta grafica nada de esto existe (el dibujo por CPU no cambia)");
        else
        {
            Ayuda();
            DentroDelDisco();
            Menu();
            Caricatura(asm);
            Neon();
        }
        Console.WriteLine(mal == 0 ? "  OK septimatanda: " + ok + " comprobaciones (aislado; sin ventana, musica ni Direct3D)"
                                   : "  FALLO septimatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    // 5) El tope: cerrar siempre; abrir, no dos en 0,7 s ni mas de seis en 30 s (y aguanta la vuelta del reloj).
    static void Tope(Assembly asm)
    {
        Type tt = asm.GetType("Rocola.TopeAperturas", false);
        Check(tt != null, "esta el tope de aperturas");
        if (tt == null) return;
        Func<int, int, int, object> nuevo = delegate (int r, int v, int m) { return Activator.CreateInstance(tt, B, null, new object[] { r, v, m }, null); };
        object t = nuevo(6, 30000, 700);
        Func<int, bool> pide = delegate (int ahora) { return (bool)tt.GetMethod("Pide", B).Invoke(t, new object[] { ahora }); };
        Check(pide(1000), "la primera vez, si");
        Check(!pide(1300), "otra a los 0,3 s, no");
        Check(pide(1800), "a los 0,8 s, si");
        int ahora2 = 1800, si = 2;
        for (int i = 0; i < 20; i++) { ahora2 += 800; if (pide(ahora2)) si++; }
        Check(si == 6, "rafaga: como mucho seis en 30 s aunque se apriete cada 0,8 s (" + si + ")");
        Check(pide(1000 + 30001), "pasados 30 s desde la primera, vuelve a dejar");
        object t2 = nuevo(6, 30000, 700);
        Check((bool)tt.GetMethod("Pide", B).Invoke(t2, new object[] { int.MaxValue - 100 }) && !(bool)tt.GetMethod("Pide", B).Invoke(t2, new object[] { int.MinValue + 100 })
              && (bool)tt.GetMethod("Pide", B).Invoke(t2, new object[] { int.MinValue + 1000 }), "aguanta la vuelta del contador de Windows");
        // Cableado: abrir pasa por el tope, cerrar no.
        Check(Codigo.Llama(form.GetMethod("ProcessCmdKey", B), "TopeBuscadorDeja") && Codigo.Llama(form.GetMethod("TopeBuscadorDeja", B), "Pide")
              && Codigo.Llama(form.GetMethod("TopeBuscadorDeja", B), "AvisaTopeCombo"), "ROJO + ARRIBA mira el tope antes de abrir el buscador");
        MethodInfo ent = form.GetMethod("AsistenteEntrada", B);
        if (ent != null) Check(Codigo.Llama(ent, "Pide") && Codigo.Llama(ent, "AvisaTopeCombo"), "ROJO + ABAJO mira el tope antes de abrir la ayuda");
        // Un formulario armado a mano (como en las demas pruebas) tambien tiene sus topes.
        object f = Codigo.FormSinEncender(form);
        PropertyInfo pa = form.GetProperty("topeAyuda", B);
        Check(pa != null && pa.GetValue(f, null) != null, "el tope se crea al primer uso (sin depender del constructor)");
    }

    // 1) La palabra AYUDA en 1024x768 aunque el encabezado pida un poco mas de lo que cabe.
    static void Ayuda()
    {
        Type tb = form.GetNestedType("BuscaView", B), ta = form.GetNestedType("AyudaView", B), th = form.GetNestedType("HeaderView", B);
        int fila = 44;
        int busW = (int)tb.GetMethod("AnchoJusto", B).Invoke(null, new object[] { fila });
        int full = (int)ta.GetMethod("Ancho", B).Invoke(null, new object[] { fila, false });
        foreach (int deficit in new int[] { 0, 20, 35 })
        {
            object f = Codigo.FormSinEncender(form);
            Control busca = (Control)Activator.CreateInstance(tb, true), ayuda = (Control)Activator.CreateInstance(ta, true), cab = (Control)Activator.CreateInstance(th, true);
            cab.Font = new Font(FontFamily.GenericSansSerif, 8.25f);
            Set(f, "buscaView", busca); Set(f, "ayudaView", ayuda); Set(f, "headerView", cab); Set(f, "gpuEnabled", true);
            int pide = (int)Run(cab, "AnchoPedido", fila);
            int libre = busW + 10 + full + 12 + pide - deficit, minX = 330, barraDer = minX + libre;
            busca.SetBounds(400, 8, busW, fila);
            int fin = (int)Run(f, "AcomodaAyuda", minX, 1024, fila, barraDer, true);
            bool compacta = (bool)Get(ayuda, "Compacta");
            int cabecera = barraDer - (fin + 12);
            string c = " (faltaban " + deficit + " px)";
            Check(!compacta && ayuda.Visible, "AYUDA conserva su palabra" + c);
            Check(cabecera >= pide * 85 / 100 - 1, "y al encabezado le queda al menos el 85 % de lo que pide (" + cabecera + " de " + pide + ")" + c);
            Check(busca.Left >= minX && ayuda.Left == busca.Right + 10 && ayuda.Right <= barraDer, "las dos casillas en orden y dentro de la fila" + c);
        }
        // Si falta mucho mas, como antes: sin la palabra.
        {
            object f = Codigo.FormSinEncender(form);
            Control busca = (Control)Activator.CreateInstance(tb, true), ayuda = (Control)Activator.CreateInstance(ta, true), cab = (Control)Activator.CreateInstance(th, true);
            cab.Font = new Font(FontFamily.GenericSansSerif, 8.25f);
            Set(f, "buscaView", busca); Set(f, "ayudaView", ayuda); Set(f, "headerView", cab); Set(f, "gpuEnabled", true);
            int pide = (int)Run(cab, "AnchoPedido", fila);
            int libre = busW + 10 + full + 12 + pide - pide / 2;
            Run(f, "AcomodaAyuda", 330, 1024, fila, 330 + libre, true);
            Check((bool)Get(ayuda, "Compacta"), "si falta mucho, la casilla va compacta como siempre (el encabezado no se aplasta)");
        }
        string fuente = Codigo.FuenteDeForm1();
        Check(fuente.Contains("helpJusta = gpuEnabled && W < 1180;"), "las instrucciones van con menos aire hasta 1180 de ancho (1024 incluido)");
    }

    // 2) Dentro de un disco no flota nada de la fila de BUSCAR y AYUDA.
    static void DentroDelDisco()
    {
        object f = Codigo.FormSinEncender(form);
        Type ta = form.GetNestedType("AyudaView", B);
        Control ayuda = (Control)Activator.CreateInstance(ta, true); ayuda.SetBounds(600, 8, 200, 44);
        Control disco = (Control)Activator.CreateInstance(form.GetNestedType("BgPanel", B), true);   // el panel del interior de un disco
        Set(f, "ayudaView", ayuda);
        Set(f, "panelSongs", disco); disco.Visible = false;
        RectangleF enPared = (RectangleF)Run(f, "AyudaIconoRect");
        disco.Visible = true;
        RectangleF enDisco = (RectangleF)Run(f, "AyudaIconoRect");
        Check(enPared.Width > 0 || !ayuda.Visible, "en la pared la interrogacion se ve");
        Check(enDisco.Width <= 0, "dentro de un disco la interrogacion no se dibuja");
        Check(!(bool)Run(f, "AyudaTocada", new Point(650, 20)), "y tocar ese sitio dentro de un disco no abre la ayuda");
        string fuente = Codigo.FuenteDeForm1();
        Check(fuente.Contains("buscaView.Visible && FilaBuscaAyudaVisible()"), "el latido de BUSCAR tambien mira que la pared este a la vista");
    }

    // 3) Los controles dibujados en la opcion elegida del menu del asistente.
    static void Menu()
    {
        MethodInfo pinta = form.GetMethod("PintaPanelAsistente", B), ctrl = form.GetMethod("AsistenteControlesOpcion", B), opc = form.GetMethod("AsistenteOpcionRect", B);
        Check(ctrl != null, "estan los controles de la opcion");
        if (ctrl == null) return;
        foreach (int[] wh in new int[][] { new int[] { 574, 499 }, new int[] { 356, 312 }, new int[] { 900, 640 } })
            for (int sel = 0; sel < 3; sel++)
                using (Bitmap b = (Bitmap)pinta.Invoke(null, new object[] { wh[0], wh[1], 0, 0, sel }))
                {
                    string c = " (" + wh[0] + "x" + wh[1] + ", opcion " + (sel + 1) + ")";
                    for (int i = 0; i < 3; i++)
                    {
                        Rectangle r = (Rectangle)opc.Invoke(null, new object[] { wh[0], wh[1], i });
                        object[] a = { r, null, null, null }; ctrl.Invoke(null, a);
                        Rectangle arriba = (Rectangle)a[1], abajo = (Rectangle)a[2], verde = (Rectangle)a[3];
                        Color v = b.GetPixel(verde.X + verde.Width / 2, verde.Y + verde.Height / 2);
                        bool hayVerde = v.G > 150 && v.R < 140 && v.A > 200;
                        if (i == sel)
                        {
                            Check(hayVerde, "el boton VERDE esta en la opcion elegida" + c);
                            Check(r.Contains(arriba) && r.Contains(abajo) && r.Contains(verde) && arriba.Bottom <= abajo.Top + 1 && abajo.Right <= verde.Left,
                                  "las flechas y el verde caben dentro de la opcion, en orden" + c);
                            Color fa = b.GetPixel(arriba.X + arriba.Width / 2, arriba.Y + (int)(arriba.Height * .38f));
                            Color fb = b.GetPixel(abajo.X + abajo.Width / 2, abajo.Y + (int)(abajo.Height * .62f));
                            bool rojaA = fa.R > 170 && fa.G < 120, rojaB = fb.R > 170 && fb.G < 120;
                            Check(rojaA == (sel > 0) && rojaB == (sel < 2), "la flecha que no lleva a ningun lado va apagada" + c);
                        }
                        else Check(!hayVerde, "las otras opciones no llevan controles: van con la seleccion" + c);
                    }
                    // La leyenda de abajo, dibujada: un verde y un rojo en el ultimo renglon.
                    bool verdeAbajo = false, rojoAbajo = false;
                    for (int y = (int)(wh[1] * .925f); y < wh[1] - 2; y++)
                        for (int x = 0; x < wh[0]; x++)
                        {
                            Color p = b.GetPixel(x, y);
                            if (p.A > 200 && p.G > 170 && p.R < 120) verdeAbajo = true;
                            if (p.A > 200 && p.R > 200 && p.G < 100) rojoAbajo = true;
                        }
                    Check(verdeAbajo && rojoAbajo, "la leyenda de abajo lleva sus botones dibujados" + c);
                }
        // El latido verde se desliza de una opcion a otra.
        MethodInfo sigue = gpu.GetMethod("AsistenteSigueSeleccion", B), verdeEn = gpu.GetMethod("AsistenteVerdeEn", B);
        float p0 = (float)sigue.Invoke(null, new object[] { -1f, 0, .016, true });
        float pos = 0f; int pasos = 0;
        for (; pasos < 120 && pos != 2f; pasos++) pos = (float)sigue.Invoke(null, new object[] { pos, 2, 1.0 / 60, true });
        Check(p0 == 0f && pos == 2f && pasos > 6 && pasos < 40, "el latido llega a la opcion nueva deslizandose (" + pasos + " cuadros)");
        Check((float)sigue.Invoke(null, new object[] { 0f, 2, .016, false }) == 2f, "sin animaciones, salta");
        RectangleF v0 = (RectangleF)verdeEn.Invoke(null, new object[] { 574, 499, 0f }), v1 = (RectangleF)verdeEn.Invoke(null, new object[] { 574, 499, 1f }), vm = (RectangleF)verdeEn.Invoke(null, new object[] { 574, 499, .5f });
        Check(vm.Y > v0.Y && vm.Y < v1.Y && Math.Abs(vm.Y - (v0.Y + v1.Y) / 2) < 1f, "a medio camino va entre las dos opciones");
        Check(((RectangleF)verdeEn.Invoke(null, new object[] { 574, 499, float.NaN })).Width == 0f, "un valor roto no dibuja nada");
        foreach (string m in new string[] { "LatidoOpcionAsistente", "AsistenteVerdeEn", "AsistenteSigueSeleccion" })
            foreach (Codigo.Paso p in Codigo.Pasos(gpu.GetMethod(m, B))) Check(p.Op != 0x8D, "sin reservar arreglos al dibujar: " + m);
    }

    // 4) La estrella caricaturesca: estira y aplasta, sin cara.
    static void Caricatura(Assembly asm)
    {
        Type exp = asm.GetType("Rocola.AsistenteExpresionGpu", true), ges = asm.GetType("Rocola.AsistenteGestoGpu", true);
        Check(exp.GetField("Estira", B) != null, "la expresion puede estirarse y aplastarse");
        foreach (string cara in new string[] { "Ojos", "Boca", "Cejas", "Sonrisa", "Guino", "MiraX" }) Check(exp.GetField(cara, B) == null, "sin rasgos de cara: " + cara);
        MethodInfo salto = gpu.GetMethod("AsistenteSaltito", B);
        Func<double, float[]> en = delegate (double t) { object[] a = { t, 0.0, 1.0, .25f, 0f, 0f, 0f }; salto.Invoke(null, a); return new float[] { (float)a[4], (float)a[5], (float)a[6] }; };
        float[] agacha = en(.20), arriba = en(.46), cae = en(.80), fuera = en(1.2);
        Check(agacha[1] < -.1f && agacha[0] > 0f, "antes de saltar se agacha y se aplasta (anticipacion)");
        Check(arriba[0] < -.2f && arriba[2] > .9f, "luego sube (y los brazos van al aire)");
        Check(cae[1] < -.08f, "al caer se aplasta contra el suelo");
        Check(fuera[0] == 0f && fuera[1] == 0f && fuera[2] == 0f, "y despues no deja nada");
        MethodInfo resorte = gpu.GetMethod("AsistenteResorte", B);
        float r1 = (float)resorte.Invoke(null, new object[] { .1, 15.0, 5.5 }), r2 = (float)resorte.Invoke(null, new object[] { 3.0, 15.0, 5.5 });
        Check((float)resorte.Invoke(null, new object[] { 0.0, 15.0, 5.5 }) == 0f && Math.Abs(r1) > .3f && Math.Abs(r2) < .001f, "el resorte se pasa y se apaga");
        // Mientras espera hace algo distinto cada 7 s (saltito, pie, vaiven): las tres vueltas se mueven.
        MethodInfo actua = gpu.GetMethod("AsistenteActua", B);
        object reposo = Enum.ToObject(ges, 0);
        for (int vuelta = 0; vuelta < 3; vuelta++)
        {
            double t0 = vuelta * 7.0;
            object quieto = actua.Invoke(null, new object[] { t0 + 1.0, t0 + 1.0, reposo, 0, true }), mueve = actua.Invoke(null, new object[] { t0 + 5.4, t0 + 5.4, reposo, 0, true });
            float dif = 0f; foreach (FieldInfo fi in exp.GetFields(B)) dif += Math.Abs((float)fi.GetValue(mueve) - (float)fi.GetValue(quieto));
            Check(dif > .08f, "mientras espera no se queda quieta (vuelta " + (vuelta + 1) + ")");
        }
        // Estirar alrededor de los pies: el ancla no se mueve, lo de arriba sube, lo de los lados se recoge.
        Type vtx = asm.GetType("Rocola.D3D9+Vtx", true);
        Array v = Array.CreateInstance(vtx, 3);
        float[,] pts = { { 100, 200 }, { 100, 100 }, { 140, 200 } };
        for (int i = 0; i < 3; i++) { object o = Activator.CreateInstance(vtx); vtx.GetField("X").SetValue(o, pts[i, 0]); vtx.GetField("Y").SetValue(o, pts[i, 1]); v.SetValue(o, i); }
        gpu.GetMethod("AsistenteEstiraMalla", B).Invoke(null, new object[] { v, 3, 100f, 200f, .2f });
        Func<int, string, float> F = delegate (int i, string n) { return (float)vtx.GetField(n).GetValue(v.GetValue(i)); };
        Check(F(0, "X") == 100f && F(0, "Y") == 200f && F(1, "Y") < 100f - 15f && F(2, "X") < 140f && F(2, "X") > 130f, "estira con los pies quietos y adelgaza un poco (pelota de goma)");
        foreach (string m in new string[] { "AsistenteCaricatura", "AsistenteSaltito", "AsistenteResorte", "AsistenteEstiraMalla", "DibujaAsistente" })
        {
            MethodInfo mi = gpu.GetMethod(m, B);
            Check(mi != null, "esta " + m);
            if (mi != null) foreach (Codigo.Paso p in Codigo.Pasos(mi)) Check(p.Op != 0x8D, "sin reservar arreglos al animar: " + m);
        }
        Check(Codigo.Llama(gpu.GetMethod("DibujaAsistente", B), "AsistenteEstiraMalla"), "el cuerpo, los miembros y los brazos se estiran juntos al dibujar");
    }

    // 6) El tubo de neon.
    static void Neon()
    {
        MethodInfo draw = gpu.GetMethod("DrawSmoothProgress", B), tubo = gpu.GetMethod("TuboNeon", B);
        Check(tubo != null && Codigo.Llama(draw, "TuboNeon"), "con neon, la barra es el tubo nuevo");
        if (tubo == null) return;
        Check(Codigo.Llama(tubo, "BandaSuave") && Codigo.Llama(tubo, "GlowBatchAdd"), "con su resplandor que se desvanece y la chispa de la punta");
        foreach (string m in new string[] { "TuboNeon", "BandaSuave", "MediaBanda", "DrawSmoothProgress" })
            foreach (Codigo.Paso p in Codigo.Pasos(gpu.GetMethod(m, B))) Check(p.Op != 0x8D, "sin reservar arreglos por cuadro: " + m);
    }
}
