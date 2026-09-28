using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
// LA AURORA A LA CADENCIA DE LA PANTALLA (AuroraFinaGpu.cs).
//
// La forma de la aurora se recalculaba en el latido de la pantalla (24 por segundo) con medidas del sonido de ~42-50 ms;
// la tarjeta dibuja 60 cuadros por segundo y la silueta solo cambiaba en uno de cada dos o tres. Ahora el hilo de audio
// mide cada 1/60 s (con la misma ventana de 50 ms) y la tarjeta avanza las bandas en cada cuadro.
//
// Aqui, con la rocola de verdad y una cancion de prueba (bombo, platillos, acordes y una melodia), se pasa lo mismo por
// los dos caminos -el latido de antes, que sigue ahi de reserva, y el cuadro a cuadro- y se mide:
//   - cuantas veces cambia la forma en 60 cuadros por segundo, y el salto mas grande de un cuadro al siguiente;
//   - que se vea igual: la misma altura media, la misma forma, el golpe a la misma hora;
//   - que no dependa de los cuadros por segundo (30, 60, 120);
//   - que el latido haga la cuenta de antes bit a bit, y que la reserva, lo mudo, la cola y el vaciado funcionen;
//   - lo que cuesta.
// Son senales sinteticas y un reloj de mentira: miden la cuenta, no lo que se ve en la pantalla del bar.
class AuroraFina
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const int N = 8;
    const double Retraso = 850.0;
    static Type TF;
    static object F;
    static double reloj;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string D(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Fld(string n) { return TF.GetField(n, T).GetValue(F); }
    static void Set(string n, object v) { TF.GetField(n, T).SetValue(F, v); }
    static object M(string n, params object[] a) { return TF.GetMethod(n, T).Invoke(F, a); }
    static float[] Vis { get { return (float[])Fld("specVis"); } }

    // Un Form1 sin encender, con lo que la aurora necesita.
    static void Nuevo(bool sonando)
    {
        F = Codigo.FormSinEncender(TF);
        if (Fld("levelLock") == null) Set("levelLock", new object());
        foreach (string n in new string[] { "specVis", "specNow", "specPiso", "specGolpe" }) Set(n, new float[N]);
        if (Fld("levelQ") == null) Set("levelQ", Activator.CreateInstance(TF.GetField("levelQ", T).FieldType));
        Set("auroraAmount", 0.55f);
        FieldInfo dly = TF.GetField("audioVisualDelayMs", T);
        if (dly != null && !dly.IsLiteral) dly.SetValue(F, (int)Retraso);
        FieldInfo np = TF.GetField("nowPlaying", T);
        np.SetValue(F, sonando ? System.Runtime.Serialization.FormatterServices.GetUninitializedObject(np.FieldType) : null);
        Set("videoOn", false);
    }

    // LA CANCION DE PRUEBA, estereo. Bombo cada 0,5 s, platillo cada 0,25 s, un acorde de fondo y una melodia que
    // cambia de nota cada medio segundo.
    static float[] Cancion(int tasa, double seg, int semilla)
    {
        int n = (int)(tasa * seg); float[] x = new float[n * 2]; Random r = new Random(semilla);
        double[] notas = { 523, 659, 784, 1047, 880, 698, 587, 1319 };
        double fm = 0, hp = 0, ant = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / tasa, v = 0;
            double tb = t % 0.5; v += 0.55 * Math.Exp(-tb / 0.12) * Math.Sin(2 * Math.PI * (55 + 60 * Math.Exp(-tb / 0.03)) * tb);
            double w = r.NextDouble() * 2 - 1; hp = 0.5 * (hp + w - ant); ant = w;
            double th = t % 0.25; v += 0.25 * Math.Exp(-th / 0.035) * hp;
            v += 0.05 * (Math.Sin(2 * Math.PI * 220 * t) + Math.Sin(2 * Math.PI * 277 * t) + Math.Sin(2 * Math.PI * 330 * t));
            double f = notas[(int)(t / 0.5) % notas.Length]; fm += 2 * Math.PI * f / tasa;
            v += 0.12 * Math.Sin(fm) * (0.6 + 0.4 * Math.Exp(-(t % 0.5) / 0.2));
            x[2 * i] = (float)v; x[2 * i + 1] = (float)(v * 0.9);
        }
        return x;
    }
    static float[] Tono(int tasa, double seg, double hz, double amp, double desde)
    {
        int n = (int)(tasa * seg); float[] x = new float[n * 2];
        for (int i = 0; i < n; i++) { double t = (double)i / tasa; float v = t < desde ? 0f : (float)(amp * Math.Sin(2 * Math.PI * hz * t)); x[2 * i] = v; x[2 * i + 1] = v; }
        return x;
    }

    // Da un bloque al hilo de audio como si se leyera a la hora 'hora' (reloj de mentira).
    static void Lee(float[] x, int desde, int cuadros, int tasa, double hora)
    {
        reloj = hora;
        M("ReportAudioBlock", x, desde * 2, cuadros * 2, tasa, 2);
    }
    // Saca de la cola de siempre los trozos que acaba de meter, con su hora de mentira (los de siempre llevan la de
    // Environment.TickCount, que aqui no se puede mover).
    static void SacaGruesos(double hora, int cuadrosBloque, int tasa, List<double> at, List<float[]> esp)
    {
        object q = Fld("levelQ"); Type tq = q.GetType();
        int cnt = (int)tq.GetProperty("Count").GetValue(q, null);
        MethodInfo deq = tq.GetMethod("Dequeue");
        int trozos = cnt; int largo = cuadrosBloque / Math.Max(1, trozos);
        for (int c = 0; c < cnt; c++)
        {
            object l = deq.Invoke(q, null);
            float[] e = (float[])l.GetType().GetField("Esp").GetValue(l);
            at.Add(hora + c * largo * 1000.0 / tasa); esp.Add(e == null ? new float[N] : (float[])e.Clone());
        }
    }

    // UNA PASADA: la cancion leida en bloques de 'bloqueMs' y dibujada a 'hz' cuadros por segundo, por el camino nuevo
    // (cuadro a cuadro) o por el de antes (latido de 42 ms). Devuelve la forma de la aurora en cada cuadro.
    static List<float[]> Pasada(float[] x, int tasa, double bloqueMs, double hz, bool nuevo, out List<double> horas)
    {
        Nuevo(true);
        int total = x.Length / 2, bloque = (int)(tasa * bloqueMs / 1000.0);
        List<double> gAt = new List<double>(); List<float[]> gEsp = new List<float[]>();
        List<float[]> cuadros = new List<float[]>(); horas = new List<double>();
        int leido = 0; double fin = total * 1000.0 / tasa + Retraso;
        double proxLatido = 0; int g = -1;
        float[] now = (float[])Fld("specNow");
        for (int f = 0; ; f++)
        {
            double t = f * 1000.0 / hz;
            if (t > fin) break;
            while (leido < total && leido * 1000.0 / tasa <= t)   // el audio se lee por delante, bloque a bloque
            {
                int n = Math.Min(bloque, total - leido); double h = leido * 1000.0 / tasa;
                Lee(x, leido, n, tasa, h); SacaGruesos(h, n, tasa, gAt, gEsp); leido += n;
            }
            if (nuevo) { reloj = t; M("AvanzaAuroraFina", t); }
            else
                while (proxLatido <= t)
                {
                    while (g + 1 < gAt.Count && gAt[g + 1] + Retraso <= proxLatido) g++;
                    if (g >= 0) Array.Copy(gEsp[g], now, N);
                    M("AvanzaAurora");
                    proxLatido += 42.0;
                }
            cuadros.Add((float[])Vis.Clone()); horas.Add(t);
        }
        return cuadros;
    }

    static void Fluidez()
    {
        Console.WriteLine("\n  1) LA MISMA CANCION POR LOS DOS CAMINOS, DIBUJADA A 60 CUADROS POR SEGUNDO\n");
        int tasa = 44100; float[] x = Cancion(tasa, 12, 7);
        foreach (double bloqueMs in new double[] { 170, 10 })
        {
            List<double> h1, h2;
            List<float[]> viejo = Pasada(x, tasa, bloqueMs, 60, false, out h1), nuevo = Pasada(x, tasa, bloqueMs, 60, true, out h2);
            int desde = (int)((Retraso + 2000) / (1000.0 / 60)), hasta = Math.Min(viejo.Count, nuevo.Count) - 30;
            double cv = 0, cn = 0, sv = 0, sn = 0, mv = 0, mn = 0; int k = 0;
            double[] mediaV = new double[N], mediaN = new double[N];
            double sxy = 0, sxx = 0, syy = 0, sx = 0, sy = 0; int nn = 0;
            for (int i = desde; i < hasta; i++, k++)
            {
                double dv = 0, dn = 0;
                for (int b = 0; b < N; b++)
                {
                    double a = viejo[i][b] - viejo[i - 1][b], c = nuevo[i][b] - nuevo[i - 1][b];
                    if (-a > dv) dv = -a; if (-c > dn) dn = -c;
                    mediaV[b] += viejo[i][b]; mediaN[b] += nuevo[i][b];
                    sx += viejo[i][b]; sy += nuevo[i][b]; sxy += viejo[i][b] * nuevo[i][b]; sxx += viejo[i][b] * viejo[i][b]; syy += nuevo[i][b] * nuevo[i][b]; nn++;
                }
                if (Cambia(viejo[i], viejo[i - 1])) cv++; if (Cambia(nuevo[i], nuevo[i - 1])) cn++;
                if (dv > mv) mv = dv; if (dn > mn) mn = dn; sv += dv; sn += dn;
            }
            string cual = bloqueMs > 50 ? "WaveOut (bloques de 170 ms)" : "WASAPI (bloques de 10 ms)";
            Info(cual + ":");
            Info("  la forma cambia en " + D(100 * cv / k, "0") + " % de los cuadros antes, " + D(100 * cn / k, "0") + " % ahora");
            Info("  bajada mas grande de un cuadro al siguiente: " + D(mv, "0.000") + " antes, " + D(mn, "0.000") + " ahora (media "
                 + D(sv / k, "0.0000") + " / " + D(sn / k, "0.0000") + ")");
            double sube = 0, baja = 0; string medias = "";
            for (int b = 0; b < N; b++) { double a = mediaV[b] / k, c = mediaN[b] / k; sube = Math.Max(sube, c - a); baja = Math.Max(baja, a - c); medias += D(a, "0.00") + "/" + D(c, "0.00") + " "; }
            // El camino de antes va ademas a destiempo (cada forma se queda hasta el latido siguiente): el parecido se mide
            // con el mejor desfase de hasta 3 cuadros, que es lo que se ve.
            double corr = -1; int lag = 0;
            for (int d = -3; d <= 3; d++) { double cc = Correlacion(viejo, nuevo, desde, hasta, d); if (cc > corr) { corr = cc; lag = d; } }
            Info("  altura media por banda (antes/ahora): " + medias);
            Info("  parecido de las dos, cuadro a cuadro (correlacion): " + D(corr, "0.000") + " con " + lag + " cuadros de desfase");
            Check(cn / k >= 0.95, cual + ": la forma cambia en casi todos los cuadros (" + D(100 * cn / k, "0") + " %, antes " + D(100 * cv / k, "0") + " %)");
            Check(mn < 0.75 * mv, cual + ": el salto mas grande de un cuadro al siguiente es menor que antes");
            // Un poco mas alta, nunca mas baja: antes se perdian los picos que caian entre latido y latido (la subida es al
            // instante, asi que mirar 60 veces por segundo en vez de 24 coge mas). Se mide y se pone tope.
            Check(baja < 0.01 && sube < 0.08, cual + ": la altura media de cada banda sube como mucho " + D(sube, "0.000") + " (de 0 a 1) y no baja: ya no se pierden picos entre latidos");
            Check(corr > 0.9, cual + ": y la misma forma cuadro a cuadro (correlacion " + D(corr, "0.000") + ")");
        }
    }
    static double Correlacion(List<float[]> v, List<float[]> n, int desde, int hasta, int d)
    {
        double sxy = 0, sxx = 0, syy = 0, sx = 0, sy = 0; int nn = 0;
        for (int i = desde; i < hasta; i++)
        {
            int j = i + d; if (j < 0 || j >= n.Count) continue;
            for (int b = 0; b < N; b++) { double x = v[i][b], y = n[j][b]; sx += x; sy += y; sxy += x * y; sxx += x * x; syy += y * y; nn++; }
        }
        return (nn * sxy - sx * sy) / Math.Sqrt((nn * sxx - sx * sx) * (nn * syy - sy * sy));
    }
    static bool Cambia(float[] a, float[] b) { for (int i = 0; i < N; i++) if (Math.Abs(a[i] - b[i]) > 1e-4f) return true; return false; }

    static void Cadencia()
    {
        Console.WriteLine("\n  2) NO DEPENDE DE LOS CUADROS POR SEGUNDO\n");
        int tasa = 48000; float[] x = Cancion(tasa, 8, 3);
        List<double> h30, h60, h120;
        List<float[]> a = Pasada(x, tasa, 170, 30, true, out h30), b = Pasada(x, tasa, 170, 60, true, out h60), c = Pasada(x, tasa, 170, 120, true, out h120);
        double d1 = 0, d2 = 0;
        for (int i = 0; i < a.Count && 2 * i < b.Count; i++) for (int k = 0; k < N; k++) d1 = Math.Max(d1, Math.Abs(a[i][k] - b[2 * i][k]));
        for (int i = 0; i < b.Count && 2 * i < c.Count; i++) for (int k = 0; k < N; k++) d2 = Math.Max(d2, Math.Abs(b[i][k] - c[2 * i][k]));
        Check(d1 < 0.03 && d2 < 0.03, "a 30, 60 y 120 cuadros por segundo la aurora esta igual en los mismos instantes (diferencia maxima "
              + D(d1, "0.0000") + " y " + D(d2, "0.0000") + ")");
    }

    static void ALaHora()
    {
        Console.WriteLine("\n  3) EL GOLPE A LA HORA\n");
        int tasa = 44100; double desde = 2.0; float[] x = Tono(tasa, 4, 1000, 0.3, desde);
        List<double> hv, hn;
        List<float[]> v = Pasada(x, tasa, 170, 60, false, out hv), n = Pasada(x, tasa, 170, 60, true, out hn);
        int banda = 0; for (int b = 1; b < N; b++) if (n[n.Count - 40][b] > n[n.Count - 40][banda]) banda = b;
        double lleno = n[n.Count - 40][banda], tv = Llega(v, hv, banda, lleno), tn = Llega(n, hn, banda, lleno);
        double oido = desde * 1000 + Retraso;
        Info("se oye a los " + D(oido, "0") + " ms; la aurora llega a la mitad a los " + D(tv, "0") + " antes y " + D(tn, "0") + " ahora");
        Check(tn - oido >= -55 && tn - oido <= 20, "ahora el golpe sale con lo que se oye (" + D(tn - oido, "+0;-0") + " ms; la ventana de 50 ms se estampa al empezar, como siempre)");
        Check(tn <= tv + 1, "y no mas tarde que antes (" + D(tv - oido, "+0;-0") + " ms)");
    }
    static double Llega(List<float[]> c, List<double> h, int b, double lleno)
    {
        for (int i = 0; i < c.Count; i++) if (c[i][b] >= 0.5 * lleno) return h[i];
        return 1e9;
    }

    static void Medidas()
    {
        Console.WriteLine("\n  4) LAS MEDIDAS DEL HILO DE AUDIO\n");
        foreach (int tasa in new int[] { 44100, 48000 })
            foreach (double bloqueMs in new double[] { 170, 10 })
            {
                Nuevo(true); float[] x = Tono(tasa, 3, 1000, 0.3, 0);
                int bloque = (int)(tasa * bloqueMs / 1000), total = x.Length / 2;
                for (int l = 0; l < total; l += bloque) Lee(x, l, Math.Min(bloque, total - l), tasa, l * 1000.0 / tasa);
                List<double> at = Horas();
                double peor = 0; for (int i = 1; i < at.Count; i++) peor = Math.Max(peor, Math.Abs(at[i] - at[i - 1] - 1000.0 / 60));
                Check(Math.Abs(at.Count - 180) <= 1 && peor < 0.05, "a " + tasa + " Hz en bloques de " + D(bloqueMs, "0") + " ms: " + at.Count
                      + " medidas en 3 s, una cada 1/60 s (desvio maximo " + D(peor, "0.000") + " ms)");
            }
        // La ventana es de 50 ms, como los trozos de siempre: en un tono fijo miden lo mismo.
        {
            Nuevo(true); int tasa = 44100; float[] x = Tono(tasa, 2, 1000, 0.3, 0);
            reloj = 0; M("ReportAudioBlock", x, 0, x.Length, tasa, 2);
            object q = Fld("levelQ"); MethodInfo deq = q.GetType().GetMethod("Dequeue"); object ult = null;
            int cnt = (int)q.GetType().GetProperty("Count").GetValue(q, null);
            for (int i = 0; i < cnt; i++) ult = deq.Invoke(q, null);
            float[] grueso = (float[])ult.GetType().GetField("Esp").GetValue(ult), fino = UltimaFina();
            double peor = 0; for (int b = 0; b < N; b++) if (grueso[b] > 1e-3) peor = Math.Max(peor, Math.Abs(fino[b] / grueso[b] - 1));
            Check(peor < 0.03, "en un tono fijo, la medida fina (50 ms deslizantes) y la de siempre coinciden (diferencia maxima " + D(100 * peor, "0.0") + " %)");
        }
    }
    static List<double> Horas()
    {
        List<double> r = new List<double>(); object q = Fld("aurFinQ");
        if (q == null) return r;
        foreach (object e in (IEnumerable)q) r.Add((double)e.GetType().GetField("At").GetValue(e));
        return r;
    }
    static float[] UltimaFina()
    {
        float[] r = null; object q = Fld("aurFinQ");
        foreach (object e in (IEnumerable)q) r = (float[])e.GetType().GetField("Esp").GetValue(e);
        return r;
    }

    // LA CUENTA DE ANTES, copiada tal cual estaba (Form1.Sonido.cs, AvanzaAurora, hasta el 28/09).
    static float[] Tilt; static float Piso, Rango, Reposo, Onda, Paso; static MethodInfo Resp;
    static float[] rv = new float[N], rp = new float[N], rg = new float[N]; static float rPres, rFase;
    static void Viejo(float[] specNow, float quietud)
    {
        for (int b = 0; b < N; b++)
        {
            float rms = specNow[b];
            float cuerpo = 0f;
            if (rms > 1e-6f)
            {
                float db = (float)(20.0 * Math.Log10(rms)) + Tilt[b];
                cuerpo = (db - Piso) / Rango;
                if (cuerpo < 0f) cuerpo = 0f; else if (cuerpo > 1f) cuerpo = 1f;
                cuerpo *= cuerpo * (3f - 2f * cuerpo);
            }
            float piso = rp[b];
            piso += (rms - piso) * 0.045f;
            rp[b] = piso;
            float golpe = 0f;
            if (rms > piso * 1.18f + 0.0008f) { golpe = (rms - piso) * 9f; if (golpe > 1f) golpe = 1f; }
            float gp = rg[b];
            rg[b] = (golpe > gp) ? golpe : gp * 0.80f;
            float v = cuerpo * 0.55f + rg[b] * cuerpo * 0.45f;
            if (v > 1f) v = 1f;
            float prev = rv[b];
            rv[b] = (v > prev) ? v : prev + (v - prev) * 0.22f;
        }
        rFase += Paso;
        if (rFase > 6.2831853f) rFase -= 6.2831853f;
        float pico = 0f;
        for (int b = 0; b < N; b++) if (rv[b] > pico) pico = rv[b];
        bool hayMusica = pico > 0.02f;
        float meta = hayMusica ? 1f : (Reposo * (float)Resp.Invoke(null, new object[] { Onda, quietud, rFase }));
        rPres += (meta - rPres) * (hayMusica ? 0.075f : 0.014f);
        if (rPres < 0f) rPres = 0f; else if (rPres > 1f) rPres = 1f;
    }
    static float C(string n) { return (float)TF.GetField(n, T).GetValue(null); }

    static void BitABit()
    {
        Console.WriteLine("\n  5) EL LATIDO HACE LA CUENTA DE ANTES\n");
        Tilt = (float[])TF.GetField("SpecTilt", T).GetValue(null);
        Piso = C("SpecPisoDb"); Rango = C("SpecRangoDb"); Reposo = C("AurReposo"); Onda = C("AurReposoOnda"); Paso = C("AurReposoPaso");
        Resp = TF.GetMethod("Respiracion", T);
        Nuevo(true); Random r = new Random(11); float[] now = (float[])Fld("specNow");
        Set("quietud", 0.7f);
        bool igual = true; int i;
        for (i = 0; i < 1500 && igual; i++)
        {
            bool calla = (i / 200) % 3 == 2;
            for (int b = 0; b < N; b++) now[b] = calla ? 0f : (float)(r.NextDouble() * r.NextDouble() * 0.3);
            M("AvanzaAurora"); Viejo(now, 0.7f);
            float[] v = Vis, p = (float[])Fld("specPiso"), g = (float[])Fld("specGolpe");
            for (int b = 0; b < N; b++) if (v[b] != rv[b] || p[b] != rp[b] || g[b] != rg[b]) igual = false;
            if ((float)Fld("auroraPres") != rPres || (float)Fld("auroraFase") != rFase) igual = false;
        }
        Check(igual, "1500 latidos con musica al azar y silencios: bandas, piso, golpe, presencia y fase, iguales bit a bit" + (igual ? "" : " (se aparta en el " + i + ")"));
    }

    static void Mecanica()
    {
        Console.WriteLine("\n  6) LO DEMAS\n");
        int tasa = 44100; float[] x = Cancion(tasa, 20, 5);
        // Sin cuadros de la tarjeta, la cola no crece sin fin, y los espectros se reusan.
        Nuevo(true);
        int bloque = tasa * 170 / 1000, total = x.Length / 2;
        for (int l = 0; l < total; l += bloque) Lee(x, l, Math.Min(bloque, total - l), tasa, l * 1000.0 / tasa);
        int enCola = Horas().Count;
        reloj = 1e6; M("AvanzaAuroraFina", 1e6);
        int libres = ((ICollection)Fld("aurFinLibres")).Count;
        Check(enCola <= 240 && Horas().Count == 0 && libres <= 240, "20 s sin que la tarjeta pida cuadros: " + enCola + " medidas en espera como mucho, y "
              + libres + " espectros para reusar");
        // ClearLevels vacia tambien las medidas finas.
        Nuevo(true); for (int l = 0; l < tasa; l += bloque) Lee(x, l, bloque, tasa, l * 1000.0 / tasa);
        M("ClearLevels");
        Check(Horas().Count == 0, "al quitar la cancion (ClearLevels) no queda ninguna medida fina pendiente");
        // Mudo: con video o sin cancion, la aurora cae suave hasta cero.
        foreach (string que in new string[] { "video", "sin cancion" })
        {
            Nuevo(true); float[] sn = (float[])Fld("specNow"); for (int b = 0; b < N; b++) { Vis[b] = 0.9f; sn[b] = 0.3f; }
            if (que == "video") Set("videoOn", true); else Set("nowPlaying", null);
            for (int l = 0; l < tasa * 2; l += bloque) Lee(x, l, bloque, tasa, l * 1000.0 / tasa);
            double t = 0, bajo = -1; float antes = 1f; bool suave = true;
            for (int f = 0; f < 180; f++)
            {
                t = Retraso + f * 1000.0 / 60; reloj = t; M("AvanzaAuroraFina", t);
                float m = 0; foreach (float v in Vis) m = Math.Max(m, v);
                if (m > antes + 1e-6f || antes - m > 0.12f) suave = false;
                antes = m; if (bajo < 0 && m < 0.02f) bajo = f * 1000.0 / 60;
            }
            Check(suave && bajo > 0 && bajo < 2000, "con " + que + " la aurora cae sin saltos y queda en nada a los " + D(bajo, "0") + " ms");
        }
        // Tras un rato sin cuadros (ventana tapada), el primero no se come de golpe todo lo que no se dibujo.
        {
            Nuevo(false); for (int b = 0; b < N; b++) Vis[b] = 0.9f;
            reloj = 1000; M("AvanzaAuroraFina", 1000.0);
            reloj = 6000; M("AvanzaAuroraFina", 6000.0);
            float m = 0; foreach (float v in Vis) m = Math.Max(m, v);
            Check(m > 0.85f, "tras 5 s sin cuadros, el primero vuelve a empezar desde ahi y no salta (queda " + D(m, "0.00") + ")");
        }
        // La presencia (el fundido al irse la musica, y la respiracion del reposo) dura lo mismo que con el latido.
        {
            Nuevo(false); Set("auroraPres", 1f); Set("quietud", 0.5f);
            for (double t = 0; t <= 3000; t += 1000.0 / 60) { reloj = t; M("AvanzaAuroraFina", t); }
            float pn = (float)Fld("auroraPres"), fn = (float)Fld("auroraFase");
            Nuevo(false); Set("auroraPres", 1f); Set("quietud", 0.5f);
            for (double t = 42; t <= 3000; t += 42) M("AvanzaAurora");
            float pv = (float)Fld("auroraPres"), fv = (float)Fld("auroraFase");
            Check(Math.Abs(pn - pv) < 0.02 && Math.Abs(fn - fv) < 0.01, "sin musica, la aurora se retira al reposo al mismo ritmo que con el latido (a los 3 s: "
                  + D(pn, "0.000") + " y " + D(pv, "0.000") + "; respiracion " + D(fn, "0.000") + " y " + D(fv, "0.000") + ")");
        }
        // La reserva: con la tarjeta dibujando, el latido no pisa el espectro; si deja de pedir cuadros, vuelve a mandar.
        Nuevo(true);
        reloj = 5000; M("AvanzaAuroraFina", 5000.0);
        bool viva = (bool)M("AuroraFinaViva");
        EnColaGruesa(new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
        M("ConsumeLevels", int.MaxValue / 2);
        bool noPisa = ((float[])Fld("specNow"))[0] == 0f;
        reloj = 5300; bool suelta = !(bool)M("AuroraFinaViva");
        EnColaGruesa(new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
        M("ConsumeLevels", int.MaxValue / 2);
        bool vuelve = ((float[])Fld("specNow"))[0] == 1f;
        Check(viva && noPisa && suelta && vuelve, "con la tarjeta dibujando el latido no pisa el espectro; 300 ms sin cuadros y vuelve a llevarlo el latido");
    }
    static void EnColaGruesa(float[] esp)
    {
        object q = Fld("levelQ"); Type tl = TF.GetNestedType("Lvl", T);
        object l = Activator.CreateInstance(tl, new object[] { 0, 0f, 0f, 0f, esp });
        q.GetType().GetMethod("Enqueue").Invoke(q, new object[] { l });
    }

    static void Enganches()
    {
        Console.WriteLine("\n  7) DONDE VA ENCHUFADO\n");
        int construye = 0; bool delCuadro = false, latido = false, consume = false, avanza = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            List<string> ll = Codigo.Llamadas(m);
            if (Codigo.Llama(ll, "BuildAuroraState")) { construye++; if (m.Name != "AuroraDelCuadro") Info("BuildAuroraState tambien desde " + m.Name); }
            if (Codigo.Llama(ll, "AuroraDelCuadro") && m.Name != "AuroraDelCuadro") delCuadro = true;
            if (m.Name == "AuroraDelCuadro" && Codigo.Llama(ll, "AvanzaAuroraFina")) avanza = true;
            if (m.Name == "AnimTimer_Tick" && Codigo.Llama(ll, "AuroraFinaViva") && Codigo.Llama(ll, "AvanzaAurora")) latido = true;
            if (m.Name == "ConsumeLevels" && Codigo.Llama(ll, "AuroraFinaViva")) consume = true;
        }
        Check(construye == 1 && delCuadro && avanza, "la tarjeta pide la aurora por AuroraDelCuadro (que la avanza a ese cuadro) y nadie mas la arma");
        Check(latido && consume, "el latido y la cola de siempre solo llevan la aurora cuando la tarjeta no la lleva");
    }

    static void Coste()
    {
        Console.WriteLine("\n  8) LO QUE CUESTA\n");
        int tasa = 44100; float[] x = Cancion(tasa, 30, 9);
        Nuevo(true);
        int bloque = tasa * 170 / 1000, total = x.Length / 2;
        MethodInfo rep = TF.GetMethod("ReportAudioBlock", T), av = TF.GetMethod("AvanzaAuroraFina", T);
        object[] a = new object[5];
        for (int l = 0; l < total / 10; l += bloque) Lee(x, l, Math.Min(bloque, total / 10 - l), tasa, 0);   // calienta
        Stopwatch w = Stopwatch.StartNew();
        for (int l = 0; l < total; l += bloque)
        {
            reloj = l * 1000.0 / tasa; a[0] = x; a[1] = l * 2; a[2] = Math.Min(bloque, total - l) * 2; a[3] = tasa; a[4] = 2;
            rep.Invoke(F, a);
            if ((l / bloque) % 2 == 1) { reloj = 1e7; av.Invoke(F, new object[] { reloj }); }   // que la cola no se llene
        }
        w.Stop();
        double msPorSeg = w.Elapsed.TotalMilliseconds / (total / (double)tasa);
        Info("hilo de audio (espectro de siempre + el fino): " + D(msPorSeg, "0.00") + " ms por segundo de musica");
        Nuevo(true); for (int l = 0; l < total; l += bloque) Lee(x, l, Math.Min(bloque, total - l), tasa, l * 1000.0 / tasa);
        object[] arg = new object[1]; int cuadros = 0; w = Stopwatch.StartNew();
        for (double t = Retraso; t < Retraso + 29000; t += 1000.0 / 60) { reloj = t; arg[0] = t; av.Invoke(F, arg); cuadros++; }
        w.Stop();
        double us = w.Elapsed.TotalMilliseconds * 1000 / cuadros;
        Info("cada cuadro de la tarjeta: " + D(us, "0.0") + " us (incluye la llamada por reflexion de la prueba)");
        Check(us < 60, "avanzar la aurora en un cuadro cuesta menos de 60 us (" + D(us, "0.0") + ")");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        FieldInfo rj = TF.GetField("RelojAuroraPrueba", T);
        if (rj == null || TF.GetMethod("AvanzaAuroraFina", T) == null) { Check(false, "la rocola no avanza la aurora cuadro a cuadro (falta AuroraFinaGpu.cs)"); return 1; }
        rj.SetValue(null, (Func<double>)delegate { return reloj; });
        try { Fluidez(); } catch (Exception e) { Check(false, "fluidez: " + e); }
        try { Cadencia(); } catch (Exception e) { Check(false, "cadencia: " + e); }
        try { ALaHora(); } catch (Exception e) { Check(false, "a la hora: " + e); }
        try { Medidas(); } catch (Exception e) { Check(false, "medidas: " + e); }
        try { BitABit(); } catch (Exception e) { Check(false, "bit a bit: " + e); }
        try { Mecanica(); } catch (Exception e) { Check(false, "lo demas: " + e); }
        try { Enganches(); } catch (Exception e) { Check(false, "enganches: " + e); }
        try { Coste(); } catch (Exception e) { Check(false, "coste: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
