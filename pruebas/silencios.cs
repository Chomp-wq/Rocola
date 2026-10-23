using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;
// SALTAR LOS SILENCIOS LARGOS (SinSilencios.cs).
//
// Con la rocola de verdad:
//   1) donde suena cada cancion, sacado de la medida entera (bloques de 100 ms, -60 dBFS);
//   2) la tabla rocola_sonoridad.txt con el algoritmo 3 (ida y vuelta, y lo que se descarta);
//   3) que se salta y que no (2 s en los extremos, 5 s en medio, los margenes y el fundido);
//   4) el recorte muestra a muestra, con lecturas de cualquier tamano, saltos del lector, un salto que falla y una
//      retomada;
//   5) de punta a punta (y si en la ruta hay OTRO archivo, el mapa viejo no se usa): un WAV medido con MideIntegrada y abierto con OpenAudio, con el interruptor encendido y
//      apagado, y el fundido calculado con lo que de verdad suena;
//   6) el interruptor de Ctrl+F3, lo que se guarda en rocola_config.txt y los enganches.
// Son senales sinteticas: miden la cuenta, no el oido.
class Silencios
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TM, TS, TMapa, TPlan, TRec;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string n) { FieldInfo f = TF.GetField(n, T); if (f != null) return f.GetValue(o); return TF.GetProperty(n, T).GetValue(o, null); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }
    static float Mf(object mapa, string n) { return (float)TMapa.GetField(n, T).GetValue(mapa); }
    static object Mapa(float desde, float hasta, float hd, float hh, float dura)
    {
        object m = Activator.CreateInstance(TMapa);
        TMapa.GetField("Desde", T).SetValue(m, desde); TMapa.GetField("Hasta", T).SetValue(m, hasta);
        TMapa.GetField("HuecoDesde", T).SetValue(m, hd); TMapa.GetField("HuecoHasta", T).SetValue(m, hh);
        TMapa.GetField("Dura", T).SetValue(m, dura);
        return m;
    }
    static object Plan(object mapa, int tasa, double fundido) { return TPlan.GetMethod("De", T).Invoke(null, new object[] { mapa, tasa, fundido }); }
    static long Pl(object p, string n) { return (long)TPlan.GetField(n, T).GetValue(p); }

    // UNA CANCION DE PRUEBA: trozos alternos de silencio (con un ruido muy bajo, -70 dBFS) y de tono.
    static float[] Cancion(int tasa, int ch, double[] trozos, bool empiezaSonando)
    {
        double total = 0; foreach (double d in trozos) total += d;
        int n = (int)Math.Round(total * tasa); float[] x = new float[n * ch]; Random r = new Random(5);
        double t0 = 0; bool suena = empiezaSonando; int i = 0;
        foreach (double d in trozos)
        {
            int hasta = Math.Min(n, (int)Math.Round((t0 + d) * tasa));
            for (; i < hasta; i++)
                for (int c = 0; c < ch; c++)
                    x[i * ch + c] = suena ? (float)(0.2 * Math.Sin(2 * Math.PI * 440 * i / tasa)) : (float)((r.NextDouble() * 2 - 1) * 0.0003);
            t0 += d; suena = !suena;
        }
        return x;
    }
    static object MideMapa(float[] x, int tasa, int ch, int trozo)
    {
        double[] pesos = new double[ch]; for (int c = 0; c < ch; c++) pesos[c] = 1.0;
        object med = Activator.CreateInstance(TM, T, null, new object[] { tasa, pesos }, null);
        MethodInfo pr = TM.GetMethod("Procesa", T);
        for (int i = 0; i < x.Length; i += trozo) pr.Invoke(med, new object[] { x, i, Math.Min(trozo, x.Length - i) });
        object picos = TM.GetField("Picos", T).GetValue(med);
        return picos.GetType().GetMethod("Mapa", T).Invoke(picos, new object[] { (long)TM.GetProperty("Cuadros", T).GetValue(med, null) });
    }
    static bool Cerca(float a, double b, double tol) { return Math.Abs(a - b) <= tol; }
    static float Pico(List<float> x, int cuadros) { float p = 0f; for (int i = 0; i < 2 * cuadros && i < x.Count; i++) p = Math.Max(p, Math.Abs(x[i])); return p; }   // lo mas alto de los primeros cuadros
    static string Txt(object m) { return F(Mf(m, "Desde"), "0.0#") + "-" + F(Mf(m, "Hasta"), "0.0#") + ", hueco " + F(Mf(m, "HuecoDesde"), "0.0#") + "-" + F(Mf(m, "HuecoHasta"), "0.0#") + ", dura " + F(Mf(m, "Dura"), "0.0#"); }

    static void DondeSuena()
    {
        Console.WriteLine("\n  1) DONDE SUENA (la medida entera)\n");
        // 3 s mudos, 10 de musica, 60 mudos (la pista oculta), 5 de musica, 4,05 mudos.
        float[] x = Cancion(44100, 2, new double[] { 3, 10, 60, 5, 4.05 }, false);
        object m = MideMapa(x, 44100, 2, 3001);
        Check(Cerca(Mf(m, "Desde"), 3.0, 0.1) && Cerca(Mf(m, "Hasta"), 78.0, 0.1) && Cerca(Mf(m, "HuecoDesde"), 13.0, 0.1)
              && Cerca(Mf(m, "HuecoHasta"), 73.0, 0.1) && Cerca(Mf(m, "Dura"), 82.05, 0.01),
              "44,1 kHz estereo, en lecturas de 3001 muestras: " + Txt(m) + " (3, 78, 13-73, 82,05)");
        float[] y = Cancion(22050, 1, new double[] { 0.05, 20, 2.5 }, false);
        m = MideMapa(y, 22050, 1, 777);
        Check(Cerca(Mf(m, "Desde"), 0.0, 0.1) && Cerca(Mf(m, "Hasta"), 20.05, 0.1) && Mf(m, "HuecoDesde") < 0,
              "22,05 kHz mono sin silencio en medio: " + Txt(m));
        float[] z = Cancion(48000, 2, new double[] { 5, 7.43 }, true);   // acaba sonando, en un bloque a medias
        m = MideMapa(z, 48000, 2, 4096);
        Check(Cerca(Mf(m, "Hasta"), 5.0, 0.1) && Cerca(Mf(m, "Dura"), 12.43, 0.01), "el final mudo en un bloque a medias: " + Txt(m));
        float[] w = Cancion(48000, 2, new double[] { 12.37 }, true);   // acaba sonando a mitad de un bloque
        m = MideMapa(w, 48000, 2, 4096);
        Check(Cerca(Mf(m, "Hasta"), 12.37, 0.005), "y si suena el bloque a medias del final, cuenta hasta el final: " + Txt(m));
        // Dos silencios en medio: se apunta el MAS LARGO, no el primero.
        float[] dos = Cancion(44100, 2, new double[] { 2, 1, 3, 8, 2 }, true);
        m = MideMapa(dos, 44100, 2, 4096);
        Check(Cerca(Mf(m, "HuecoDesde"), 6, 0.1) && Cerca(Mf(m, "HuecoHasta"), 14, 0.1), "con un silencio de 1 s y otro de 8, se apunta el de 8: " + Txt(m));
        float[] mudo = Cancion(44100, 2, new double[] { 8 }, false);
        m = MideMapa(mudo, 44100, 2, 4096);
        Check(Mf(m, "Desde") < 0 && Mf(m, "Hasta") < 0 && Cerca(Mf(m, "Dura"), 8, 0.01), "una cancion muda (ruido de -70 dBFS): no suena nada");
        // -55 dBFS ya es sonido: una intro muy bajita no se salta.
        float[] baja = Cancion(44100, 2, new double[] { 4, 6 }, false);
        for (int i = 0; i < 44100 * 4; i++) { float v = (float)(0.0018 * Math.Sin(2 * Math.PI * 300 * i / 44100.0)); baja[2 * i] = v; baja[2 * i + 1] = v; }
        m = MideMapa(baja, 44100, 2, 4096);
        Check(Cerca(Mf(m, "Desde"), 0, 0.05), "una intro a -55 dBFS cuenta como sonido (" + Txt(m) + ")");
    }

    static void LaTabla()
    {
        Console.WriteLine("\n  2) LA TABLA (rocola_sonoridad.txt, algoritmo 3)\n");
        int alg = (int)TM.GetField("Algoritmo", T).GetValue(null);
        // Desde el 23-10-2026 es el 4 (anade el LRA del modo automatico del local, Sonoridad.cs) y sigue llevando donde suena: lo que
        // se vigila es que las de 2, sin donde suena, se vuelvan a medir.
        Check(alg >= 3, "el algoritmo sube a 3 o mas (lleva donde suena): la tanda larga vuelve a medir las de 2");
        ConstructorInfo c7 = null;
        foreach (ConstructorInfo c in TS.GetConstructors(T)) if (c.GetParameters().Length == 7) c7 = c;
        object a = c7.Invoke(new object[] { 3, -14.25f, 2f, 1800, -0.31f, -3.4f, Mapa(3.1f, 78f, 13f, 73f, 82.05f) });
        object b = c7.Invoke(new object[] { 3, -12f, 2f, 900, -1f, -5f, Mapa(0f, 200.5f, -1f, -1f, 201f) });
        object mu = c7.Invoke(new object[] { 3, float.NegativeInfinity, 2f, 30, -200f, -200f, Mapa(-1f, -1f, -1f, -1f, 12f) });
        StringWriter w = new StringWriter(); w.Write((string)Get(null, "IntegCabecera") + "\r\n");
        Call(null, "EscribeIntegRegistro", w, "C:\\m\\a.mp3", a); Call(null, "EscribeIntegRegistro", w, "C:\\m\\b.mp3", b); Call(null, "EscribeIntegRegistro", w, "C:\\m\\muda.mp3", mu);
        string texto = w.ToString();
        Check(texto.Contains("3|-14.25|2|1800|-0.31|-3.4|3.1|78|13|73|82.05|C:\\m\\a.mp3") && texto.Contains("|0|200.5|-|-|201|C:\\m\\b.mp3") && texto.Contains("3|muda|2|30|-200|-200|-|-|-|-|12|C:\\m\\muda.mp3"),
              "formato: ...|pico|P99|desde|hasta|hueco desde|hueco hasta|duracion|ruta (\"-\" = no hay)");
        string otras = "2|-10|2|100|-1|-2|C:\\m\\dos.mp3\r\n9|x|C:\\m\\futura.mp3\r\n"
                     + "3|-10|2|100|-1|-2|5|4|-|-|10|C:\\m\\alreves.mp3\r\n"          // acaba antes de empezar
                     + "3|-10|2|100|-1|-2|1|9|0.5|3|10|C:\\m\\huecofuera.mp3\r\n"      // el hueco empieza antes del sonido
                     + "3|-10|2|100|-1|-2|1|9|3|-|10|C:\\m\\medio.mp3\r\n"             // hueco a medias
                     + "3|-10|2|100|-1|-2|1|12|-|-|10|C:\\m\\largo.mp3\r\n"            // suena despues de acabar
                     + "3|-10|2|100|-1|-2|1|9|-|-|0|C:\\m\\cero.mp3\r\n"               // no dura nada
                     + "3|-10|2|100|-1|-2|NaN|9|-|-|10|C:\\m\\nan.mp3\r\n"
                     + "3|-10|2|100|-1|-2|1|9|-|-|C:\\m\\corta.mp3\r\n"                // un campo de menos
                     + "3|-10|2|100|-1|-2|-|5|-|-|10|C:\\m\\mudamal.mp3\r\n";          // muda pero con final
        IDictionary t = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase);
        List<string> aj = new List<string>();
        bool ok;
        using (StringReader r = new StringReader(texto + otras)) ok = (bool)Call(null, "LeeIntegRegistros", r, t, aj);
        object la = t.Contains("C:\\m\\a.mp3") ? t["C:\\m\\a.mp3"] : null, dos = t.Contains("C:\\m\\dos.mp3") ? t["C:\\m\\dos.mp3"] : null;
        object lm = t.Contains("C:\\m\\muda.mp3") ? t["C:\\m\\muda.mp3"] : null;
        bool bien = ok && la != null && (bool)TS.GetProperty("ConSilencios", T).GetValue(la, null);
        if (bien) { object s = TS.GetField("Silencios", T).GetValue(la); bien = Cerca(Mf(s, "Desde"), 3.1, 1e-4) && Cerca(Mf(s, "HuecoHasta"), 73, 1e-4) && Cerca(Mf(s, "Dura"), 82.05, 1e-4); }
        Check(bien, "ida y vuelta de una de 3 con su mapa");
        Check(lm != null && (bool)TS.GetProperty("ConSilencios", T).GetValue(lm, null) && dos != null && !(bool)TS.GetProperty("ConSilencios", T).GetValue(dos, null),
              "la muda se lee (mapa sin sonido); una de 2 se sigue leyendo, sin mapa (suena como antes hasta volver a medirla)");
        Check(t.Count == 4 && aj.Count == 1, "se descartan: al reves, hueco fuera, hueco a medias, sonido tras el final, duracion 0, NaN, un campo de menos y muda con final ("
              + t.Count + " leidas, " + aj.Count + " ajena)");
    }

    static void QueSeSalta()
    {
        Console.WriteLine("\n  3) QUE SE SALTA Y QUE NO\n");
        Check(Plan(Mapa(1.9f, 100f, -1f, -1f, 101.9f), 44100, 0.5) == null, "1,9 s al principio y 1,9 al final: nada (las pausas cortas son de la musica)");
        object p = Plan(Mapa(2.1f, 100f, -1f, -1f, 101.9f), 44100, 0.5);
        Check(p != null && Pl(p, "Inicio") == (long)(((double)2.1f - 0.3) * 44100) && Pl(p, "Fin") == -1 && Pl(p, "HuecoDesde") == -1, "2,1 s al principio: se empieza 0,3 s antes del sonido; el final, como estaba");
        p = Plan(Mapa(0f, 100f, -1f, -1f, 102.5f), 44100, 0.5);
        Check(p != null && Pl(p, "Inicio") == 0 && Pl(p, "Fin") == (long)(100.5 * 44100), "2,5 s mudos al final: se acaba 0,5 s despues del ultimo sonido");
        p = Plan(Mapa(0f, 100f, -1f, -1f, 110f), 44100, 3.0);
        Check(p != null && Pl(p, "Fin") == (long)(103.0 * 44100), "con un fundido de 3 s se dejan 3 s: el fundido cae en lo mudo, no en la musica");
        Check(Plan(Mapa(0f, 100f, -1f, -1f, 102.8f), 44100, 3.0) == null, "y si con esos 3 s no se gana nada, no se toca");
        Check(Plan(Mapa(0f, 100f, 40f, 44.9f, 100.5f), 44100, 0.5) == null, "4,9 s de silencio en medio: se deja (puede ser una pausa larga de verdad)");
        p = Plan(Mapa(0f, 100f, 40f, 45.1f, 100.5f), 44100, 0.5);
        Check(p != null && Pl(p, "HuecoDesde") == (long)(40.5 * 44100) && Pl(p, "HuecoHasta") == (long)(44.8 * 44100), "5,1 s en medio: se salta, con 0,5 s de cola detras y 0,3 antes de volver");
        Check(Plan(Mapa(-1f, -1f, -1f, -1f, 30f), 44100, 0.5) == null && Plan(Mapa(5f, 3f, -1f, -1f, 30f), 44100, 0.5) == null,
              "una muda o un mapa que no cuadra: no se toca");
    }

    // Un lector de mentira: la muestra i vale i (a 1000 Hz caben de sobra en un float).
    class Lector
    {
        public long pos; public int lecturas, saltos; public bool fallaSalto; public int ch; public long total; Random r = new Random(3);
        public int Lee(float[] b, int o, int c)
        {
            lecturas++;
            int n = (int)Math.Min(c, total * ch - pos); if (n <= 0) return 0;
            if (n > 7 && r.Next(3) == 0) n = 1 + r.Next(n - 1);   // lecturas cortas y partidas, como algunos lectores
            for (int i = 0; i < n; i++) b[o + i] = pos + i;
            pos += n; return n;
        }
        public void Salta(long cuadro) { saltos++; if (fallaSalto) throw new IOException("no se puede"); pos = cuadro * ch; }
    }
    static object Recorte(Lector l, int tasa, object plan)
    {
        Func<float[], int, int, int> lee = l.Lee; Action<long> sal = l.Salta;
        return Activator.CreateInstance(TRec, T, null, new object[] { lee, sal, l.ch, tasa, plan }, null);
    }
    static List<long> Todo(object rec, int bloque)
    {
        MethodInfo lee = TRec.GetMethod("Lee", T); List<long> r = new List<long>(); float[] b = new float[bloque]; Random rnd = new Random(9);
        while (true)
        {
            int pide = 1 + rnd.Next(bloque);
            int n = (int)lee.Invoke(rec, new object[] { b, 0, pide });
            if (n <= 0) break;
            for (int i = 0; i < n; i++) r.Add((long)b[i]);
        }
        return r;
    }
    static bool Tramos(List<long> sale, long[][] tramos, int ch)
    {
        int k = 0;
        foreach (long[] t in tramos) for (long i = t[0] * ch; i < t[1] * ch; i++) { if (k >= sale.Count || sale[k] != i) return false; k++; }
        return k == sale.Count;
    }

    static void ElRecorte()
    {
        Console.WriteLine("\n  4) EL RECORTE, MUESTRA A MUESTRA\n");
        int tasa = 1000;
        object plan = Plan(Mapa(3f, 78f, 13f, 73f, 82.05f), tasa, 0.5);
        Lector l = new Lector { ch = 2, total = 82050 };
        object rec = Recorte(l, tasa, plan);
        long salen = (long)TRec.GetMethod("Salen", T).Invoke(rec, new object[] { 82050L });
        List<long> s = Todo(rec, 1500);
        long[][] esperado = { new long[] { 2700, 13500 }, new long[] { 72700, 78500 } };
        Check(Tramos(s, esperado, 2), "la cancion de la pista oculta sale exacta: de 2,7 s a 13,5 y de 72,7 a 78,5 (" + s.Count / 2 + " cuadros de 82050), con lecturas de cualquier tamano");
        Check(salen == s.Count / 2, "y el fundido sabe cuantos van a salir (" + salen + ")");
        Check(l.saltos == 1, "el principio mudo se tiro leyendo y el hueco de 60 s se salto con el lector (1 salto)");
        // Un salto que falla: se sigue leyendo el hueco, sin perder nada.
        Lector l2 = new Lector { ch = 2, total = 82050, fallaSalto = true };
        List<long> s2 = Todo(Recorte(l2, tasa, plan), 1500);
        Check(Tramos(s2, new long[][] { new long[] { 2700, 78500 } }, 2), "si el lector no puede saltar, se oye el hueco (como antes) y no se pierde ni una muestra");
        // Un principio mudo de mas de 20 s se salta con el lector, no descomprimiendo.
        Lector l3 = new Lector { ch = 2, total = 60000 };
        object rec3 = Recorte(l3, tasa, Plan(Mapa(30f, 55f, -1f, -1f, 60f), tasa, 0.5));
        int lecturasAlAbrir = l3.lecturas;
        List<long> s3 = Todo(rec3, 700);
        Check(l3.saltos == 1 && lecturasAlAbrir == 0 && Tramos(s3, new long[][] { new long[] { 29700, 55500 } }, 2),
              "30 s mudos al principio: un salto al abrirla, sin descomprimir nada, y de ahi a 55,5 s");
        // Retomada (volvio el aparato de sonido a los 10 s): se sigue contando desde ahi.
        Lector l4 = new Lector { ch = 2, total = 82050 };
        object rec4 = Recorte(l4, tasa, plan);
        l4.pos = 10000 * 2; TRec.GetMethod("Movido", T).Invoke(rec4, new object[] { 10000L });
        long salen4 = (long)TRec.GetMethod("Salen", T).Invoke(rec4, new object[] { 82050L });
        List<long> s4 = Todo(rec4, 999);
        Check(Tramos(s4, new long[][] { new long[] { 10000, 13500 }, new long[] { 72700, 78500 } }, 2) && salen4 == s4.Count / 2,
              "retomada a los 10 s: sale desde ahi, salta el hueco y acaba en 78,5 s; el fundido lo sabe (" + salen4 + " cuadros)");
        // Mono y un origen que entrega de uno en uno.
        Lector l5 = new Lector { ch = 1, total = 10000 };
        List<long> s5 = Todo(Recorte(l5, tasa, Plan(Mapa(2.5f, 7f, -1f, -1f, 10f), tasa, 0.5)), 3);
        Check(Tramos(s5, new long[][] { new long[] { 2200, 7500 } }, 1), "mono, pidiendo de 1 a 3 muestras: exacto");
    }

    static void DePuntaAPunta()
    {
        Console.WriteLine("\n  5) DE PUNTA A PUNTA: MEDIR, ABRIR Y SONAR\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_silencios_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            int fs = 44100;
            float[] x = Cancion(fs, 2, new double[] { 3, 10, 12, 5, 4 }, false);
            string ruta = Path.Combine(dir, "oculta.wav");
            using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
            object f = Codigo.FormSinEncender(TF);
            object[] a = { ruta, null };
            bool midio = (bool)Call(f, "MideIntegrada", a);
            object s = a[1];
            bool con = midio && (bool)TS.GetProperty("ConSilencios", T).GetValue(s, null);
            object m = con ? TS.GetField("Silencios", T).GetValue(s) : null;
            Check(con && Cerca(Mf(m, "Desde"), 3, 0.1) && Cerca(Mf(m, "HuecoDesde"), 13, 0.1) && Cerca(Mf(m, "HuecoHasta"), 25, 0.1) && Cerca(Mf(m, "Hasta"), 30, 0.1),
                  "MideIntegrada apunta donde suena: " + (con ? Txt(m) : "nada"));
            IDictionary tab = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase);
            tab[ruta] = s; Set(f, "integCache", tab); Set(f, "crossfadeSec", 0.5f);
            Call(f, "ActualizaFuenteMedida", ruta, Call(null, "TomaFuenteMedida")); // sello capturado por el analisis real
            // Encendido.
            Set(f, "saltaSilencios", true);
            object src = Call(f, "OpenAudio", ruta);
            Type tf = src.GetType();
            long total = (long)(34.0 * fs);
            long salen = tf.Name == "FuenteSinSilencios" ? (long)tf.GetMethod("Salen", T).Invoke(src, new object[] { total }) : -1;
            List<float> sale = Lee(src);
            int primero = -1, ultimo = -1;
            for (int i = 0; i < sale.Count / 2; i++) if (Math.Abs(sale[2 * i]) > 0.01f) { if (primero < 0) primero = i; ultimo = i; }
            double dura = sale.Count / 2.0 / fs;
            Info("sale " + F(dura, "0.00") + " s de 34; la musica empieza a los " + F(primero / (double)fs, "0.00") + " s y acaba a los " + F(ultimo / (double)fs, "0.00"));
            Check(tf.Name == "FuenteSinSilencios" && Math.Abs(primero / (double)fs - 0.3) < 0.11, "con el interruptor encendido, OpenAudio la abre sin el principio mudo: la musica a los 0,3 s");
            Check(Math.Abs(dura - (0.3 + 10 + 0.5 + 0.3 + 5 + 0.5)) < 0.25, "sin los 12 s del hueco ni el final mudo: " + F(dura, "0.00") + " s (esperados ~16,6)");
            Check(salen == sale.Count / 2, "el fundido cuenta lo que de verdad suena (" + salen + " cuadros)");
            PropertyInfo pt = tf.GetProperty("TotalTime"), pc = tf.GetProperty("CurrentTime");
            double quedan = ((TimeSpan)pt.GetValue(src, null) - (TimeSpan)pc.GetValue(src, null)).TotalSeconds, total2 = ((TimeSpan)pt.GetValue(src, null)).TotalSeconds;
            Check(Math.Abs(quedan) < 0.1 && Math.Abs(total2 - 30.5) < 0.05,
                  "la cancion dice su final de verdad (" + F(total2, "0.00") + " s de 34): al acabar no \"quedan\" los segundos mudos, y el paso sin pausa prepara la siguiente a tiempo");
            ((IDisposable)src).Dispose();
            // La version de Apollo (DSEE ULTRA, HD) es otro archivo alineado con la original: vale la medida de la original.
            FieldInfo mapaIa = TF.GetField("iaOriginal", T);
            if (mapaIa != null)
            {
                string apollo = Path.Combine(dir, "oculta_apollo.wav");
                File.Copy(ruta, apollo);
                ((IDictionary)mapaIa.GetValue(null))[apollo] = ruta;
                object srcA = Call(f, "OpenAudio", apollo);
                List<float> saleA = Lee(srcA); ((IDisposable)srcA).Dispose();
                ((IDictionary)mapaIa.GetValue(null)).Remove(apollo);
                Check(srcA.GetType().Name == "FuenteSinSilencios" && saleA.Count == sale.Count, "con DSEE ULTRA suena la version de Apollo (otro archivo) y tambien se le saltan los silencios");
                int leeIa, escIa; Codigo.UsosDeCampo(TF.GetMethod("RutaQueSonaria", T), "iaOriginal", out leeIa, out escIa);
                Check(Codigo.Llama(TF.GetMethod("MapaDe", T), "RutaOriginalDe") && leeIa > 0 && Codigo.Llama(TF.GetMethod("RutaQueSonaria", T), "set_Item"),
                      "la tabla se busca tambien por la cancion original, que se apunta al elegir la version de Apollo");
            }
            else Info("(esta rama no tiene la version de Apollo: nada que comprobar ahi)");
            // Apagado: tal cual.
            Set(f, "saltaSilencios", false);
            object src2 = Call(f, "OpenAudio", ruta);
            List<float> sale2 = Lee(src2); ((IDisposable)src2).Dispose();
            Check(src2.GetType().Name != "FuenteSinSilencios" && sale2.Count == x.Length, "con el interruptor apagado, la cancion entera como siempre (" + F(sale2.Count / 2.0 / fs, "0.00") + " s)");
            // Sin medir con el algoritmo 3: como siempre.
            Set(f, "saltaSilencios", true);
            ConstructorInfo c6 = null; foreach (ConstructorInfo c in TS.GetConstructors(T)) if (c.GetParameters().Length == 6) c6 = c;
            tab[ruta] = c6.Invoke(new object[] { 2, -20f, 2f, 300, -1f, -2f });
            object src3 = Call(f, "OpenAudio", ruta);
            List<float> sale3 = Lee(src3); ((IDisposable)src3).Dispose();
            Check(src3.GetType().Name != "FuenteSinSilencios" && sale3.Count == x.Length, "si la cancion aun no esta medida con el algoritmo 3, suena entera");
            // Misma duracion y numero de bytes: el mapa sigue perteneciendo al archivo anterior.
            tab[ruta] = s;
            DateTime anterior = File.GetLastWriteTimeUtc(ruta);
            float[] igualDura = Cancion(fs, 2, new double[] { 34 }, true);
            using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(igualDura, 0, igualDura.Length);
            File.SetLastWriteTimeUtc(ruta, anterior.AddSeconds(2));
            object srcIgual = Call(f, "OpenAudio", ruta);
            List<float> saleIgual = Lee(srcIgual); ((IDisposable)srcIgual).Dispose();
            Check(srcIgual.GetType().Name != "FuenteSinSilencios" && saleIgual.Count == igualDura.Length && Pico(saleIgual, 4410) > 0.1f,
                  "otro archivo de los mismos 34 s no hereda el recorte antiguo: suena entero desde el principio");
            // OTRO ARCHIVO CON LA MISMA RUTA (una version mejor con el mismo nombre): el mapa medido es del de antes. La de
            // ahora suena desde el primer instante y dura 40 s: con el mapa viejo se saltaban sus primeros 2,7 s y se cortaba
            // a los 30,5 s. No se usa: suena entera, como antes de medirla.
            tab[ruta] = s;
            float[] otra = Cancion(fs, 2, new double[] { 40 }, true);
            using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(otra, 0, otra.Length);
            object src4 = Call(f, "OpenAudio", ruta);
            List<float> sale4 = Lee(src4); ((IDisposable)src4).Dispose();
            Check(src4.GetType().Name != "FuenteSinSilencios" && sale4.Count == otra.Length && Pico(sale4, 4410) > 0.1f,
                  "si en la ruta hay OTRO archivo (no dura lo que se midio), el mapa viejo no se usa: suena entera (" + F(sale4.Count / 2.0 / fs, "0.00") + " s de 40)");
            // Y el mismo archivo con un poco de diferencia en la duracion (otro lector, el relleno del codificador): se usa.
            object mm = TS.GetField("Silencios", T).GetValue(s);
            MethodInfo deEste = TMapa.GetMethod("DeEsteArchivo", T);
            float d0 = Mf(mm, "Dura");
            Check((bool)deEste.Invoke(mm, new object[] { (double)d0 + 0.2 }) && (bool)deEste.Invoke(mm, new object[] { (double)d0 - 0.9 })
                  && !(bool)deEste.Invoke(mm, new object[] { (double)d0 + 1.5 }) && !(bool)deEste.Invoke(mm, new object[] { 0.0 }),
                  "con unas decimas de diferencia es el mismo archivo y se usa; con mas de un segundo, o sin duracion, no");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    static void LaEspera()
    {
        Console.WriteLine("\n  7) LA ESPERA DE LA COLA\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_silencios_eta_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            int fs = 44100;
            float[] x = Cancion(fs, 2, new double[] { 3, 10, 12, 5, 4 }, false);
            string ruta = Path.Combine(dir, "oculta.wav");
            using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, 2))) w.WriteSamples(x, 0, x.Length);
            object f = Codigo.FormSinEncender(TF);
            object[] a = { ruta, null };
            Call(f, "MideIntegrada", a);
            IDictionary tab = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), TS), StringComparer.OrdinalIgnoreCase);
            tab[ruta] = a[1]; Set(f, "integCache", tab); Set(f, "crossfadeSec", 0.5f);
            IDictionary dur = (IDictionary)Get(f, "durCache"); dur[ruta] = 34f;
            Call(f, "ActualizaFuenteMedida", ruta, Call(null, "TomaFuenteMedida")); // certificado coherente con las tablas de esta fixture
            Set(f, "saltaSilencios", true);
            float s1 = (float)Call(f, "SegundosQueSuenan", ruta);
            Set(f, "saltaSilencios", false);
            float s0 = (float)Call(f, "SegundosQueSuenan", ruta);
            Check(Math.Abs(s1 - 16.6) < 0.2 && s0 == 34f, "en la cola, la cancion cuenta " + F(s1, "0.0") + " s con el interruptor encendido (lo que suena) y 34 apagado");
            // La que suena: lo que de verdad le queda, sin el hueco que aun falta saltar.
            Set(f, "saltaSilencios", true);
            object src = Call(f, "OpenAudio", ruta);
            MethodInfo rd = src.GetType().GetMethod("Read"); float[] b = new float[44100 * 2];
            for (int k = 0; k < 5; k++) rd.Invoke(src, new object[] { b, 0, b.Length });   // 5 s de musica: va por los 7,7 s del archivo
            Type tp = TF.GetField("nowPlaying", T).FieldType; object it = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tp);
            object tr = Activator.CreateInstance(TF.GetNestedType("Track", T), true); TF.GetNestedType("Track", T).GetField("Path", T).SetValue(tr, ruta);
            tp.GetField("Song", T).SetValue(it, tr);
            Set(f, "nowPlaying", it); Set(f, "reader", src);
            double queda = (double)Call(f, "RestanDeLaQueSuena");
            ((IDisposable)src).Dispose();
            Check(Math.Abs(queda - 11.6) < 0.2, "a la que suena le quedan " + F(queda, "0.0") + " s de verdad (sin el hueco ni el final mudo; contando el archivo serian 26,3)");
            Check(Codigo.Llama(TF.GetMethod("AddItem", T), "SegundosQueSuenan") && Codigo.Llama(TF.GetMethod("RestanDeLaQueSuena", T), "QuedaSinSilencios")
                  && (TF.GetMethod("ActualizaEsperas", T) == null || Codigo.Llama(TF.GetMethod("ActualizaEsperas", T), "SegundosQueSuenan")),
                  "el aviso de turno, los minutos de la lista y lo que le queda a la que suena cuentan lo que de verdad suena");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    static List<float> Lee(object src)
    {
        MethodInfo r = src.GetType().GetMethod("Read"); float[] b = new float[8192]; List<float> l = new List<float>();
        while (true) { int n = (int)r.Invoke(src, new object[] { b, 0, b.Length }); if (n <= 0) break; for (int i = 0; i < n; i++) l.Add(b[i]); }
        return l;
    }

    static void ElInterruptor()
    {
        Console.WriteLine("\n  6) EL INTERRUPTOR Y LOS ENGANCHES\n");
        List<MethodBase> ven = Codigo.MetodosConAyudantes(TF, "VentanaAjustes");
        List<string> textos = new List<string>(); foreach (MethodBase m in ven) textos.AddRange(Codigo.Textos(m));
        bool letrero = false; foreach (string t in textos) if (t.StartsWith("Saltar silencios largos")) letrero = true;
        Check(letrero && Codigo.LeeCampo(ven, "saltaSilencios") && Codigo.EscribeCampo(ven, "saltaSilencios"),
              "Ctrl+F3 -> Sonido tiene el interruptor, lo pone como esta y lo cambia");
        bool retira = false; foreach (MethodBase m in ven) if (Codigo.Llama(m, "SiguienteConElInterruptor")) retira = true;
        Check(retira, "y al cambiarlo retira lo ya abierto para la siguiente: vale desde la siguiente, no desde la de despues (sinpausa.cs lo ejecuta)");
        // Si la ventana truena, los ajustes de sonido vuelven a como estaban: el interruptor tambien (lo hace la ventana
        // misma, en su catch, no el que lo cambia).
        List<MethodBase> sola = new List<MethodBase>(); sola.Add(TF.GetMethod("VentanaAjustes", T));
        Check(Codigo.EscribeCampo(sola, "saltaSilencios") && Codigo.Llama(sola[0], "SiguienteConElInterruptor"),
              "si la ventana falla, el interruptor vuelve a como estaba, como los demas ajustes de sonido");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_silencios_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            bool guarda = true;
            foreach (bool v in new bool[] { false, true })
            {
                object f = Codigo.FormSinEncender(TF); Set(f, "carpetaDatos", dir); Set(f, "saltaSilencios", v);
                Call(f, "SaveConfigPrefs"); Codigo.EsperaGuardado(TF, f);
                object g = Codigo.FormSinEncender(TF); Set(g, "carpetaDatos", dir); Set(g, "saltaSilencios", !v);
                Call(g, "LoadConfigPrefs");
                if ((bool)Get(g, "saltaSilencios") != v) guarda = false;
            }
            Check(guarda, "encendido y apagado se guardan en rocola_config.txt (saltar_silencios=) y se leen igual");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Check(Codigo.Llama(TF.GetMethod("OpenAudio", T), "SinSilencios"), "OpenAudio envuelve la cancion (tambien la reconstruida)");
        Check(Codigo.Llama(TF.GetMethod("ArmaCadena", T), "Salen"), "y el fundido de salida pregunta cuanto va a sonar");
        Check(Codigo.Llama(TF.GetMethod("PublicaIntegrada", T), "Enter") && Codigo.Llama(TF.GetMethod("MapaDe", T), "Enter"),
              "la tabla se cambia y se lee con cerrojo (las canciones se abren en segundo plano)");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TM = asm.GetType("Rocola.MedidorBs1770", false); TS = asm.GetType("Rocola.SonoridadIntegrada", false);
        TMapa = asm.GetType("Rocola.MapaSilencios", false); TPlan = asm.GetType("Rocola.PlanSilencios", false); TRec = asm.GetType("Rocola.RecorteSilencios", false);
        if (TMapa == null || TPlan == null || TRec == null) { Check(false, "la rocola no salta silencios (falta SinSilencios.cs)"); return 1; }
        try { DondeSuena(); } catch (Exception e) { Check(false, "donde suena: " + e); }
        try { LaTabla(); } catch (Exception e) { Check(false, "la tabla: " + e); }
        try { QueSeSalta(); } catch (Exception e) { Check(false, "que se salta: " + e); }
        try { ElRecorte(); } catch (Exception e) { Check(false, "el recorte: " + e); }
        try { DePuntaAPunta(); } catch (Exception e) { Check(false, "de punta a punta: " + e); }
        try { ElInterruptor(); } catch (Exception e) { Check(false, "el interruptor: " + e); }
        try { LaEspera(); } catch (Exception e) { Check(false, "la espera: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
