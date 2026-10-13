using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// EL CIELO VIVO DE LEGACY (Rocola/CieloVivoGpu.cs, 12-10-2026): las cuentas de los siete adornos, sin tarjeta grafica.
//  1) La luna: la fase de fechas conocidas (luna nueva del 10-10-2026, llena del 26-10-2026), cuanto se ilumina, el
//     horneado (cuarto creciente: la derecha encendida; llena: todo) y lo que tarda.
//  2) Las lluvias: Perseidas, Geminidas, Cuadrantidas (cruzan el ano), Orionidas; fuera de fechas, nada. Sus fugaces
//     nacen dentro de la pantalla y se alejan del radiante.
//  3) La fugaz de festejo: entra por un costado, arriba, y baja.
//  4) El cometa: retirado el 13-10-2026 (ya no esta, y su clave se ignora).
//  5) La profundidad: las nebulosas no salen nunca de su margen.
//  6) El cielo segun la hora: madrugada, anochecer, mediodia; con todo en cero sale el mismo color.
//  7) El reflejo de la aurora: no pasa del hueco, se apaga hacia abajo y lleva el color del filo.
//  8) Que todo este conectado: el dibujo, la configuracion, Ctrl+F3 y el festejo de moneda y cancion.
// No es ver la tarjeta grafica del bar: eso solo se sabe alli.
class CieloVivo
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v) { return v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture); }
    static Type TF, TVtx, TPanel, TStars;
    static object S(string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == n && m.IsStatic && m.GetParameters().Length == a.Length)
            try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + n);
    }
    static float Fl(object[] a, int i) { return (float)a[i]; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TPanel = asm.GetType("Rocola.GpuPanel"); TStars = asm.GetType("Rocola.GpuStarfield");
        Type td = asm.GetType("Rocola.D3D9"); TVtx = td != null ? td.GetNestedType("Vtx", T) : null;
        if (TPanel == null || TF.GetMethod("FaseLunar", T) == null)
        {
            // Sin tarjeta grafica no hay cielo vivo: el dibujo por CPU esta congelado. Solo se mira que la configuracion
            // se conserve igual en este ejecutable.
            List<string> tl0 = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts0 = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            foreach (string k in new string[] { "luna_del_cielo=", "lluvias_estrellas=", "fugaz_festejo=", "profundidad_pagina=", "cielo_por_hora=", "reflejo_aurora=" })
                Check(tl0.Contains(k) && ts0.Contains(k), "sin tarjeta, la configuracion conserva " + k);
            Console.WriteLine(fallos == 0 ? "  OK cielovivo: " + oks + " comprobaciones (sin tarjeta grafica: solo la configuracion; CPU congelado)"
                                          : "  FALLO cielovivo: " + fallos + " de " + (oks + fallos));
            return fallos;
        }

        Console.WriteLine("\n  1) LA LUNA\n");
        double nueva = (double)S("FaseLunar", new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc));
        double llena = (double)S("FaseLunar", new DateTime(2026, 10, 26, 4, 0, 0, DateTimeKind.Utc));
        double cuarto = (double)S("FaseLunar", new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc));
        Console.WriteLine("        10-10-2026 " + F(nueva) + ", 18-10-2026 " + F(cuarto) + ", 26-10-2026 " + F(llena));
        Check(Math.Min(nueva, 1 - nueva) < 0.035, "el 10-10-2026 es luna nueva (+-1 dia)");
        Check(Math.Abs(llena - 0.5) < 0.035, "el 26-10-2026 es luna llena (+-1 dia)");
        Check(Math.Abs(cuarto - 0.25) < 0.05, "el 18-10-2026, cuarto creciente");
        Check(Math.Abs((double)S("LunaIluminada", 0.0)) < 1e-9 && Math.Abs((double)S("LunaIluminada", 0.5) - 1) < 1e-9 && Math.Abs((double)S("LunaIluminada", 0.25) - 0.5) < 1e-9,
              "iluminada: nueva 0, cuarto la mitad, llena entera");
        foreach (int[] wh in new int[][] { new int[] { 800, 600 }, new int[] { 1024, 768 }, new int[] { 1920, 1080 } })
        {
            object[] a = { wh[0], wh[1], 0f, 0f, 0f };
            TF.GetMethod("SitioLuna", T).Invoke(null, a);
            float cx = Fl(a, 2), cy = Fl(a, 3), r = Fl(a, 4);
            Check(cx - r > 0 && cx + r < wh[0] && cy - r > 0 && cy + r < wh[1] * 0.4f, wh[0] + "x" + wh[1] + ": la luna entera en el cielo de arriba (r " + r.ToString("0") + ")");
        }
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Bitmap c1 = (Bitmap)S("HorneaLuna", 128, 0.25);
        sw.Stop();
        Bitmap c2 = (Bitmap)S("HorneaLuna", 128, 0.5), c3 = (Bitmap)S("HorneaLuna", 128, 0.75);
        Func<Bitmap, int, int, double> brillo = delegate (Bitmap b, int x0, int x1)
        {
            double s = 0; int n = 0;
            for (int y = 40; y < 88; y++) for (int x = x0; x < x1; x++) { Color c = b.GetPixel(x, y); s += c.R; n++; }
            return s / n;
        };
        double izq = brillo(c1, 20, 56), der = brillo(c1, 72, 108);
        Console.WriteLine("        cuarto creciente: izquierda " + F(izq) + ", derecha " + F(der) + " (horneada en " + sw.ElapsedMilliseconds + " ms)");
        Check(der > 4 * izq && der > 120, "cuarto creciente: la derecha encendida y la izquierda apagada");
        Check(brillo(c3, 20, 56) > 4 * brillo(c3, 72, 108), "cuarto menguante: al reves");
        Check(brillo(c2, 20, 56) > 120 && brillo(c2, 72, 108) > 120, "llena: entera");
        Check(c1.GetPixel(0, 0).A == 0 && c1.GetPixel(127, 127).A == 0 && c1.GetPixel(30, 64).A >= 220, "fuera del disco transparente; la parte apagada casi opaca (tapa las estrellas)");
        Check(sw.ElapsedMilliseconds < 150, "hornearla cuesta poco (una vez cada ~6 h)");
        c1.Dispose(); c2.Dispose(); c3.Dispose();

        Console.WriteLine("\n  2) LAS LLUVIAS DE ESTRELLAS\n");
        Func<DateTime, string> lluvia = delegate (DateTime d)
        {
            object[] a = { d, 0.0, 0f, 0f };
            string n = (string)TF.GetMethod("LluviaDe", T).Invoke(null, a);
            return n + "|" + ((double)a[1]).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        };
        string per = lluvia(new DateTime(2026, 8, 12, 12, 0, 0)), per2 = lluvia(new DateTime(2026, 8, 1, 22, 0, 0));
        string gem = lluvia(new DateTime(2026, 12, 14, 12, 0, 0)), cua = lluvia(new DateTime(2027, 1, 1, 23, 0, 0));
        string nada = lluvia(new DateTime(2026, 3, 15, 22, 0, 0)), ori = lluvia(new DateTime(2026, 10, 21, 12, 0, 0));
        Console.WriteLine("        " + per + "  " + per2 + "  " + gem + "  " + cua + "  " + ori + "  [" + nada + "]");
        Check(per.StartsWith("Perseidas|1.0"), "el 12 de agosto, Perseidas en su maximo");
        double f2 = double.Parse(per2.Substring(per2.IndexOf('|') + 1), System.Globalization.CultureInfo.InvariantCulture);
        Check(per2.StartsWith("Perseidas|") && f2 > 0.05 && f2 < 0.4, "el 1 de agosto, Perseidas flojas (empiezan semanas antes)");
        Check(gem.StartsWith("Geminidas|1.0"), "el 14 de diciembre, Geminidas");
        Check(cua.StartsWith("Cuadrantidas|"), "el 1 de enero, Cuadrantidas (el maximo es del ano siguiente)");
        Check(ori.StartsWith("Orionidas|0.6"), "el 21 de octubre, Orionidas");
        Check(nada == "|0.00", "a mediados de marzo no hay lluvia: todo como siempre");
        Random rnd = new Random(3);
        bool dentro = true, aleja = true;
        for (int i = 0; i < 200; i++)
        {
            object[] a = { rnd, 1024f, 768f, 256f, -92f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false };
            TF.GetMethod("TiraFugazRadiante", T).Invoke(null, a);
            float x = Fl(a, 5), y = Fl(a, 6), vx = Fl(a, 7), vy = Fl(a, 8);
            if (x < 0 || x > 1024 || y < 0 || y > 768 * 0.7f) dentro = false;
            double px = x - 256, py = y + 92, pl = Math.Sqrt(px * px + py * py), vl = Math.Sqrt(vx * vx + vy * vy);
            if ((px * vx + py * vy) / (pl * vl) < 0.999) aleja = false;
        }
        Check(dentro, "las de la lluvia nacen dentro de la pantalla, en la parte de arriba");
        Check(aleja, "y se alejan del radiante (la cola le apunta)");

        Console.WriteLine("\n  3) LA FUGAZ DE FESTEJO\n");
        bool costado = true, baja = true;
        for (int i = 0; i < 100; i++)
        {
            object[] a = { rnd, 1024f, 768f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            TF.GetMethod("TiraFugazFestejo", T).Invoke(null, a);
            float x = Fl(a, 3), y = Fl(a, 4), vx = Fl(a, 5), vy = Fl(a, 6);
            if (!((x < 0 && vx > 0) || (x > 1024 && vx < 0)) || y > 768 * 0.3f) costado = false;
            if (!(vy > 0) || Math.Abs(vy) > Math.Abs(vx)) baja = false;
        }
        Check(costado, "entra por un costado, arriba, hacia dentro");
        Check(baja, "y baja en diagonal suave");
        Check((int)TF.GetField("FestejoCadaMs", T).GetValue(null) >= 1500, "como mucho una cada " + TF.GetField("FestejoCadaMs", T).GetValue(null) + " ms");

        Console.WriteLine("\n  4) EL COMETA, RETIRADO (13-10-2026)\n");
        {
            Check(TF.GetMethod("CometaEn", T) == null && TF.GetMethod("CometaColas", T) == null && TF.GetField("cometaOn", T) == null,
                  "el cometa ya no esta (ni su cuenta ni su interruptor)");
            Check(TStars.GetMethod("DibujaCometa", T) == null, "y el cielo ya no lo dibuja");
            List<string> tlc = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), tsc = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            Check(!tlc.Contains("cometa=") && !tsc.Contains("cometa="), "cometa= de una configuracion vieja se ignora y ya no se escribe");
        }

        Console.WriteLine("\n  5) LA PROFUNDIDAD\n");
        {
            MethodInfo em = TPanel.GetMethod("EnMargen", T);
            Check(em != null, "existe el tope del margen");
            if (em != null)
            {
                bool igual = true;
                for (int i = 0; i <= 64; i++) { float c = (float)Math.Cos(i * 0.1), m = 61.44f; if ((float)em.Invoke(null, new object[] { c * m, m }) != c * m) igual = false; }
                Check(igual, "sin empuje, la cuenta de siempre (ni un decimal distinto)");
                Check((float)em.Invoke(null, new object[] { 90f, 61.44f }) == 61.44f && (float)em.Invoke(null, new object[] { -90f, 61.44f }) == -61.44f, "con empuje, nunca sale del margen (el borde no asoma)");
            }
            float fNeb = (float)TPanel.GetField("NebEmpujeFr", T).GetValue(null), fHaz = (float)TPanel.GetField("HazEmpujeFr", T).GetValue(null);
            Check(fNeb < 1f && fHaz < fNeb, "las nebulosas se corren menos que las estrellas y la via lactea menos todavia");
        }

        Console.WriteLine("\n  6) EL CIELO SEGUN LA HORA\n");
        {
            Func<int, int, float[]> hora = delegate (int h, int m)
            {
                object[] a = { new DateTime(2026, 10, 12, h, m, 0), 0f, 0f };
                TF.GetMethod("CieloHora", T).Invoke(null, a);
                return new float[] { Fl(a, 1), Fl(a, 2) };
            };
            float[] h3 = hora(3, 0), h1930 = hora(19, 30), h13 = hora(13, 0), h630 = hora(6, 30), h23 = hora(23, 30);
            Console.WriteLine("        03:00 " + F(h3[0]) + "/" + F(h3[1]) + "  06:30 " + F(h630[0]) + "/" + F(h630[1]) + "  13:00 " + F(h13[0]) + "/" + F(h13[1]) + "  19:30 " + F(h1930[0]) + "/" + F(h1930[1]) + "  23:30 " + F(h23[0]) + "/" + F(h23[1]));
            Check(h3[0] == 1f && h3[1] == 0f, "a las 3 de la madrugada: hondo, sin calor");
            Check(h1930[1] == 1f && h1930[0] == 0f, "a las 19:30: calido, sin hondo");
            Check(h13[0] == 0f && h13[1] == 0f, "a mediodia: como siempre");
            Check(h630[1] > 0.3f && h630[1] < 0.7f, "al amanecer, algo calido");
            Check(h23[0] > 0.2f && h23[0] < 0.8f, "a las 23:30, a medio camino");
            uint c = 0xFF1A2C5Au;
            Check((uint)S("TineCielo", c, 0f, 0f) == c, "con el interruptor apagado (todo en 0) sale el mismo color, exacto");
            uint oscuro = (uint)S("TineCielo", c, 0.18f, 0f), calido = (uint)S("TineCielo", c, 0f, 0.16f);
            Check(((oscuro >> 16) & 0xFF) < 0x1A && (oscuro & 0xFF) < 0x5A, "de madrugada, mas oscuro");
            Check(((calido >> 16) & 0xFF) > 0x1A + 20 && (calido >> 24) == 0xFF, "al anochecer, mas rojo (y opaco igual)");
        }

        Console.WriteLine("\n  7) EL REFLEJO DE LA AURORA\n");
        {
            int cols = 144;
            float[] h = new float[cols + 1], inten = new float[cols + 1]; uint[] col = new uint[cols + 1];
            for (int i = 0; i <= cols; i++) { h[i] = 40f + 60f * (float)Math.Abs(Math.Sin(i * 0.1)); inten[i] = 0.8f; col[i] = 0x0040E0A0u; }
            Array v = Array.CreateInstance(TVtx, cols * 6);
            float hueco = (float)S("ReflejoHueco", 768f);
            int n = (int)TF.GetMethod("AurReflejo", T).Invoke(null, new object[] { v, h, col, inten, cols, 1024f, 600f, hueco });
            Check(n == cols * 6, "una tira por columna (" + n + " vertices)");
            Check(hueco <= 16f && hueco >= 6f, "el hueco no pasa de 16 px (" + hueco + "): se apaga antes del texto de la franja");
            float maxY = 0; bool arribaAlfa = true, abajoCero = true, color = true;
            for (int i = 0; i < n; i++)
            {
                object x = v.GetValue(i);
                float y = (float)TVtx.GetField("Y").GetValue(x); uint c = (uint)TVtx.GetField("Color").GetValue(x);
                maxY = Math.Max(maxY, y);
                if ((c & 0x00FFFFFFu) != 0x0040E0A0u) color = false;
                if (y <= 601.01f && (c >> 24) != (uint)(255 * 0.30f * 0.8f)) arribaAlfa = false;
                if (y > 601.01f && (c >> 24) != 0) abajoCero = false;
            }
            Check(maxY <= 601f + hueco + 0.01f, "nunca baja mas del hueco");
            Check(arribaAlfa && abajoCero, "pegado al horizonte, tenue (30 % del filo); abajo, nada");
            Check(color, "con el color del filo de cada columna");
        }

        Console.WriteLine("\n  8) TODO CONECTADO\n");
        {
            List<string> begin = Codigo.Llamadas(TStars.GetMethod("Begin", T));
            Check(Codigo.Llama(begin, "TineCielo"), "el cielo de cada cuadro lleva la hora");
            List<string> fug = Codigo.Llamadas(TStars.GetMethod("UpdateAndDrawShoots", T));
            Check(Codigo.Llama(fug, "TiraFugazRadiante") && Codigo.Llama(fug, "TiraFugazFestejo") && Codigo.Llama(fug, "TiraFugaz"), "las fugaces: las de siempre, las de la lluvia y la de festejo");
            List<string> rf = Codigo.Llamadas(TPanel.GetMethod("RenderFrame", T));
            Check(Codigo.Llama(rf, "ActualizaCieloVivo") && Codigo.Llama(rf, "EnsureLunaCielo") && Codigo.Llama(rf, "VuelvenNieblas"), "antes de cada cuadro: el reloj, la luna (horneada fuera del cuadro) y la vuelta de las nebulosas");
            List<string> ctor = new List<string>();
            foreach (ConstructorInfo ci in TPanel.GetConstructors(T)) ctor.AddRange(Codigo.ConAyudantes(TPanel, ".ctor"));
            bool luna = false, refl = false;
            foreach (MethodBase m in Codigo.TodosLosMetodos(TPanel))
            {
                List<string> ll = Codigo.Llamadas(m);
                if (Codigo.Llama(ll, "DibujaLunaCielo") && Codigo.Llama(ll, "DibujaLuna")) luna = true;
                if (Codigo.Llama(ll, "DibujaReflejoAurora") && Codigo.Llama(ll, "DrawAurora")) refl = true;
            }
            Check(luna, "la luna del cielo va con la estrella, detras de ella");
            Check(refl, "el reflejo va justo despues de la aurora");
            Check(Codigo.Llama(TPanel.GetMethod("DibujaNieblas", T), "EnMargen"), "las nebulosas y la via lactea con su empuje, dentro del margen");
            Check(Codigo.Llama(TPanel.GetMethod("PreparaPantallaViva", T), "EmpujaNieblas"), "al pasar de pagina se empujan tambien las nebulosas");
            Check(Codigo.Llama(TF.GetMethod("AddItem", T), "FestejaEnElCielo") && Codigo.Llama(TF.GetMethod("AddCoin", T), "FestejaEnElCielo"), "la moneda y la cancion puesta piden su fugaz");
            List<string> ap = Codigo.IlConNombres(TF.GetMethod("AplicaEfectosGpu", T));
            foreach (string p in new string[] { "LunaDelCielo", "LluviasEstrellas", "FugazFestejo", "ProfundidadPagina", "CieloPorHora", "ReflejoAurora" })
            {
                string campo = p;
                Check(ap.Exists(delegate (string s) { return s.StartsWith("7D ") && s.EndsWith(" " + campo); }), "Ctrl+F3 lleva " + p + " a la tarjeta");
            }
            List<string> tl = Codigo.Textos(TF.GetMethod("LoadConfigPrefs", T)), ts = Codigo.Textos(TF.GetMethod("TextoConfig", T));
            foreach (string k in new string[] { "luna_del_cielo=", "lluvias_estrellas=", "fugaz_festejo=", "profundidad_pagina=", "cielo_por_hora=", "reflejo_aurora=" })
                Check(tl.Contains(k) && ts.Contains(k), "la configuracion lee y guarda " + k);
            Check(!tl.Contains("cielo_hora=") && !tl.Contains("luna_fase="), "las claves viejas de Espacio natural siguen ignoradas");
            List<string> pag = new List<string>();
            foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "PaginaFondo")) pag.AddRange(Codigo.Textos(m));
            foreach (string s in new string[] { "Luna con su fase real", "Lluvias de estrellas en sus fechas", "Fugaz al poner una cancion", "Mas profundidad al pasar de pagina", "Cielo segun la hora", "Reflejo de la aurora" })
                Check(pag.Contains(s), "en Ctrl+F3 -> Fondo: «" + s + "»");
            List<string> hojas = Codigo.Textos(TF.GetMethod("MontaHojasFondo", T));
            Check(hojas.Contains("Cielo vivo"), "con su hoja: Cielo vivo");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK cielovivo: " + oks + " comprobaciones (las cuentas, sin tarjeta grafica; no es ver el bar)" : "  FALLO cielovivo: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}
