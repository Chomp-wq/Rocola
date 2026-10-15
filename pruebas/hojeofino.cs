using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// HOJEAR RAPIDO SIN MICRO TIRONES EN EL FONDO (13-10-2026).
//
// Con las caratulas ya al instante, el cielo seguia dando pequenos tirones al pasar paginas rapido. Las causas y lo que
// se comprueba de cada una:
//   1. El reloj. La pared y el empujon del cielo se median con Environment.TickCount, que en Windows salta de 15,6 en
//      15,6 ms. Ahora usan el cronometro fino (MsFino): se lee el codigo compilado para ver que TomaEmpuje, SlideNow y
//      la rejilla de la tarjeta ya no miran TickCount, y que las curvas finas dan lo mismo que las de siempre.
//   2. Cada pagina nueva tiraba lo que le faltaba al empujon anterior. Ahora se suman: dos paginas seguidas corren el
//      cielo exactamente dos empujones, en pasos siempre hacia el mismo lado.
//   3. La niebla frenaba en seco al llegar a su tope. Ahora pasa por una rodilla suave (solo con tarjeta grafica).
//   4. Cada portada leida pedia un arreglo nuevo, casi siempre de mas de 85 KB (los que solo recoge la limpieza
//      completa). Ahora el lector reutiliza el suyo: mismo contenido, misma imagen.
// Y los perfiles de carga de caratulas con JPEG: cuantas paginas de colchon pide cada uno (PaginasDeColchon).
class HojeoFino
{
    delegate int CuentaColchon(bool gpu, int perfil, bool lento, bool jpeg, int cpu); // Func de cinco entradas no existe en .NET 3.5
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) { ok++; Console.WriteLine("  OK " + s); } else { mal++; Console.WriteLine("  FALLO " + s); } }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(Path.GetFullPath(args.Length > 0 ? args[0] : "rocola.exe"));
        Type form = asm.GetType("Rocola.Form1");
        Type av = form.GetNestedType("AlbumView", B);
        Type panel = asm.GetType("Rocola.GpuPanel");
        bool gpu = panel != null && panel.GetMethod("Rodilla", B) != null;
        Console.WriteLine("Perfil: " + (gpu ? "GPU" : "CPU"));

        // Pasar de pagina deslizando y el empujon del cielo son solo del perfil con tarjeta grafica; sin ella (dibujo por
        // CPU, congelado) se prueba lo comun: la lectura de caratulas y el colchon.
        bool hojeo = av.GetMethod("TomaEmpuje", B) != null;
        Check(hojeo == gpu, hojeo ? "el hojeo deslizando esta en el perfil GPU" : "sin tarjeta grafica no hay hojeo deslizando: solo lo comun");
        if (hojeo) Hojeo(av, panel, gpu);
        Comun(form);
        Console.WriteLine(mal == 0 ? "PASS hojeofino (" + ok + " comprobaciones)" : "FALLAN " + mal);
        return mal;
    }

    static void Hojeo(Type av, Type panel, bool gpu)
    {
        // 1. EL RELOJ FINO, leyendo el codigo compilado.
        foreach (string m in new string[] { "TomaEmpuje", "SlideNow", "BuildGpuGrid", "StartSlide" })
        {
            MethodInfo mi = av.GetMethod(m, B);
            if (mi == null) { Check(m == "BuildGpuGrid" && !gpu, m + " existe"); continue; }
            Check(Codigo.Llama(mi, "MsFino"), m + " usa el reloj fino");
            if (m != "StartSlide")   // StartSlide sigue apuntando TickCount para Sliding (lo usa el dibujo por CPU)
                Check(!Codigo.Llama(mi, "get_TickCount") && !Codigo.Llama(mi, "TickVivo"), m + " ya no mide con TickCount");
        }
        MethodInfo cielo = av.GetMethod("CurvaCielo", B), cieloF = av.GetMethod("CurvaCieloFina", B);
        MethodInfo col = av.GetMethod("CurvaColumna", B), colF = av.GetMethod("CurvaColumnaFina", B);
        MethodInfo hoj = av.GetMethod("CurvaHojeo", B), hojF = av.GetMethod("CurvaHojeoFina", B);
        bool iguales = true, suaves = true;
        float antes = 0f;
        for (int dt = -5; dt <= 260; dt++)
        {
            if ((float)cielo.Invoke(null, new object[] { dt }) != (float)cieloF.Invoke(null, new object[] { (float)dt })) iguales = false;
            object[] a1 = { dt, null }, a2 = { (float)dt, null };
            if ((float)hoj.Invoke(null, a1) != (float)hojF.Invoke(null, a2) || (float)a1[1] != (float)a2[1]) iguales = false;
            foreach (bool vieja in new bool[] { false, true })
                for (int orden = 0; orden < 12; orden += 5)
                {
                    object[] c1 = { dt, orden, vieja, null }, c2 = { (float)dt, orden, vieja, null };
                    if ((float)col.Invoke(null, c1) != (float)colF.Invoke(null, c2) || (float)c1[3] != (float)c2[3]) iguales = false;
                }
            // Entre dos milisegundos la curva fina va por en medio: ya no se queda quieta un cuadro y salta al otro.
            float medio = (float)cieloF.Invoke(null, new object[] { dt + .5f });
            if (dt >= 0 && dt < 229 && !(medio > antes && medio < (float)cieloF.Invoke(null, new object[] { dt + 1f }))) suaves = false;
            antes = (float)cieloF.Invoke(null, new object[] { dt + 1f });
        }
        Check(iguales, "las curvas finas dan lo mismo que las de siempre con milisegundos enteros (cielo, hojeo y columnas)");
        Check(suaves, "con medio milisegundo la curva del cielo avanza por en medio");

        // 2. LOS EMPUJONES SE SUMAN.
        float fr = (float)av.GetField("EmpujeFr", B).GetValue(null);
        int slideMs = (int)av.GetField("SlideMs", B).GetValue(null);
        using (Control pared = (Control)Activator.CreateInstance(av, true))
        {
            pared.SetBounds(0, 0, 1000, 540);
            MethodInfo start = av.GetMethod("StartSlide", B), toma = av.GetMethod("TomaEmpuje", B);
            foreach (int dir in new int[] { 1, -1 })
            {
                toma.Invoke(pared, null);
                float total = 0f; bool mismoLado = true;
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                start.Invoke(pared, new object[] { dir, 1000 });
                bool segundo = false;
                while (sw.ElapsedMilliseconds < 60 + slideMs + 80)
                {
                    if (!segundo && sw.ElapsedMilliseconds >= 60) { start.Invoke(pared, new object[] { dir, 1000 }); segundo = true; }
                    float d = (float)toma.Invoke(pared, null);
                    if (d * -dir < -1e-4f) mismoLado = false;
                    total += d;
                    Thread.Sleep(4);
                }
                total += (float)toma.Invoke(pared, null);
                float esperado = 2f * -dir * fr * 1000f;
                Check(Math.Abs(total - esperado) < .01f, "dos paginas seguidas (" + (dir > 0 ? "derecha" : "izquierda") + ") corren el cielo dos empujones enteros: "
                    + total.ToString("0.000") + " de " + esperado.ToString("0.000") + " px");
                Check(mismoLado, "cada paso del cielo va hacia el mismo lado, sin frenar ni volver");
            }
            // Cuatro paginas en rafaga (mas que empujones vivos no se guardan): tampoco se pierde nada de las cuatro.
            int vivos = (int)av.GetField("EmpujesVivos", B).GetValue(null);
            toma.Invoke(pared, null);
            float rafaga = 0f;
            System.Diagnostics.Stopwatch sr = System.Diagnostics.Stopwatch.StartNew();
            int lanzadas = 0;
            while (sr.ElapsedMilliseconds < 3 * 40 + slideMs + 80)
            {
                if (lanzadas < vivos && sr.ElapsedMilliseconds >= lanzadas * 40) { start.Invoke(pared, new object[] { 1, 1000 }); lanzadas++; }
                rafaga += (float)toma.Invoke(pared, null);
                Thread.Sleep(4);
            }
            rafaga += (float)toma.Invoke(pared, null);
            Check(Math.Abs(rafaga - vivos * -fr * 1000f) < .02f, vivos + " paginas en rafaga: " + rafaga.ToString("0.000") + " px, todas enteras");
            // Si nadie pregunta (pared tapada), lo que faltaba se tira: nada de saltos tarde.
            start.Invoke(pared, new object[] { 1, 1000 });
            Thread.Sleep(slideMs + 200);
            Check((float)toma.Invoke(pared, null) == 0f, "un empujon que nadie pidio a tiempo no se cobra de golpe despues");
        }

        // 3. LA RODILLA DE LA NIEBLA (solo con tarjeta grafica).
        if (gpu)
        {
            MethodInfo rod = panel.GetMethod("Rodilla", B);
            float tope = 40f; bool identidad = true, crece = true, bajoTope = true, simetrica = true;
            float prev = -1e9f;
            for (float x = -200f; x <= 200f; x += .25f)
            {
                float y = (float)rod.Invoke(null, new object[] { x, tope });
                if (Math.Abs(x) <= .75f * tope && y != x) identidad = false;
                if (y < prev) crece = false;
                if (Math.Abs(y) > tope) bajoTope = false;
                if ((float)rod.Invoke(null, new object[] { -x, tope }) != -y) simetrica = false;
                prev = y;
            }
            // Sin quiebre: la pendiente justo antes y justo despues del 75 % es la misma.
            float k = .75f * tope, h = .01f;
            float izq = (k - (float)rod.Invoke(null, new object[] { k - h, tope })) / h;
            float der = ((float)rod.Invoke(null, new object[] { k + h, tope }) - k) / h;
            Check(identidad, "hasta el 75 % del tope la niebla se corre igual que antes");
            Check(crece && simetrica, "la rodilla siempre crece y es igual hacia los dos lados");
            Check(bajoTope, "nunca pasa del tope de antes");
            Check(Math.Abs(izq - der) < .02f, "sin quiebre al entrar en la rodilla (pendiente " + izq.ToString("0.000") + " y " + der.ToString("0.000") + ")");
        }

    }

    static void Comun(Type form)
    {
        // 4. LA LECTURA DE CARATULAS REUTILIZA SU ARREGLO.
        string dir0 = Path.Combine(Path.GetTempPath(), "hojeofino_" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir0);
        try
        {
            MethodInfo lee = form.GetMethod("LeeEntero", B);
            int reutil = (int)form.GetField("LecturaReutilizada", B).GetValue(null);
            string a = Path.Combine(dir0, "a.png"), b = Path.Combine(dir0, "b.png"), grande = Path.Combine(dir0, "grande.bin");
            using (Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
            {
                Random r = new Random(7);
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++) bmp.SetPixel(x, y, Color.FromArgb(255, r.Next(256), r.Next(256), r.Next(256)));
                bmp.Save(a, ImageFormat.Png);
                using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.Teal);
                bmp.Save(b, ImageFormat.Png);
            }
            byte[] big = new byte[reutil + 1000]; new Random(3).NextBytes(big); File.WriteAllBytes(grande, big);
            object[] pa = { a, 0 }, pb = { b, 0 }, pg = { grande, 0 };
            byte[] ra = (byte[])lee.Invoke(null, pa); int la = (int)pa[1];
            byte[] copiaA = new byte[la]; Array.Copy(ra, copiaA, la);
            Check(la == new FileInfo(a).Length && Igual(copiaA, File.ReadAllBytes(a)), "lee la caratula entera y bien (" + la / 1024 + " KB, de las que antes iban a la memoria de objetos grandes)");
            byte[] rb = (byte[])lee.Invoke(null, pb); int lb = (int)pb[1];
            Check(ReferenceEquals(ra, rb), "la siguiente caratula reutiliza el mismo arreglo: ninguno nuevo por portada");
            byte[] copiaB = new byte[lb]; Array.Copy(rb, copiaB, lb);
            Check(lb == new FileInfo(b).Length && Igual(copiaB, File.ReadAllBytes(b)), "y su contenido es el suyo, con su largo");
            byte[] rg = (byte[])lee.Invoke(null, pg);
            Check(!ReferenceEquals(rg, rb) && (int)pg[1] == big.Length && Igual(rg, big), "un archivo de mas de " + reutil / (1024 * 1024) + " MB va en su arreglo propio y no se guarda");
            Check(ReferenceEquals((byte[])lee.Invoke(null, new object[] { a, 0 }), rb), "despues de uno grande se vuelve al de siempre");
            byte[] otroHilo = null;
            Thread t = new Thread(delegate() { otroHilo = (byte[])lee.Invoke(null, new object[] { a, 0 }); }); t.Start(); t.Join();
            Check(otroHilo != null && !ReferenceEquals(otroHilo, rb), "cada hilo lector tiene el suyo: dos lecturas a la vez no se pisan");
            // La imagen que sale no depende del arreglo: se lee una, se lee otra encima, y la primera sigue igual.
            MethodInfo dec = form.GetMethod("DecodeCoverCore", B);
            if (dec != null)
            {
                object[] d1 = { a, null, false, false, null };
                using (Bitmap img = (Bitmap)dec.Invoke(null, d1))
                {
                    Color antesPx = img.GetPixel(37, 91);
                    lee.Invoke(null, new object[] { b, 0 });
                    Check(img.GetPixel(37, 91) == antesPx && antesPx != Color.FromArgb(255, Color.Teal), "la portada ya hecha no cambia al leer otra en el mismo arreglo");
                }
            }
        }
        finally { try { Directory.Delete(dir0, true); } catch { } }

        // 5. PAGINAS DE COLCHON POR PERFIL, DISCO Y FORMATO.
        MethodInfo colchon = form.GetMethod("PaginasDeColchon", B);
        CuentaColchon P = delegate(bool g, int perfil, bool lento, bool jpeg, int cpu)
        { return (int)colchon.Invoke(null, new object[] { g, perfil, lento, jpeg, cpu }); };
        Check(P(false, 3, false, true, 8) == 2 && P(false, 3, true, true, 8) == 1, "sin tarjeta grafica, lo de siempre: dos con SSD y una con disco duro");
        bool fluidez = true;
        foreach (bool lento in new bool[] { false, true }) foreach (bool jpeg in new bool[] { false, true }) foreach (int cpu in new int[] { 1, 2, 8 })
            if (P(true, 1, lento, jpeg, cpu) != 1) fluidez = false;
        Check(fluidez, "Fluidez: una pagina a cada lado, siempre (menos trabajo de fondo en equipos debiles)");
        foreach (int perfil in new int[] { 0, 2 })
            Check(P(true, perfil, false, false, 2) == 2 && P(true, perfil, false, true, 8) == 2 && P(true, perfil, true, false, 8) == 1 && P(true, perfil, true, true, 2) == 2,
                (perfil == 0 ? "Automatico" : "Equilibrado") + ": dos con SSD; con disco duro, dos en JPEG y una en PNG");
        Check(P(true, 3, false, true, 8) == 3 && P(true, 3, false, false, 8) == 2 && P(true, 3, false, true, 2) == 2,
            "Carga rapida: tres con SSD y JPEG si hay mas de dos hilos; si no, dos");
        Check(P(true, 3, true, true, 8) == 2 && P(true, 3, true, false, 8) == 1, "Carga rapida con disco duro: dos en JPEG, una en PNG (como los demas)");
        int topeCache = (int)form.GetMethod("TopeDePortadasPara", B).Invoke(null, new object[] { 0UL });
        int pagina = (int)form.GetField("PageSize", B).GetValue(null);
        Check((2 * 3 + 2) * pagina < topeCache, "el colchon mas grande (" + (2 * 3 + 2) * pagina + " portadas con la de al lado) cabe en la cache del equipo mas chico (" + topeCache + ")");
        MethodInfo settle = form.GetMethod("CoverSettle_Tick", B);
        Check(Codigo.Llama(settle, "PaginasDeColchon") && Codigo.Llama(settle, "PrefetchPage"), "al detenerse en una pagina se usa ese colchon");
    }

    static bool Igual(byte[] x, byte[] y)
    {
        if (x.Length != y.Length) return false;
        for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
        return true;
    }
}
