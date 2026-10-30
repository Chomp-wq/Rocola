using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// LA PRESENCIA SIN EL GRAVE, LO QUE HIZO CADA ETAPA Y RECOGER EL SONIDO (30-10-2026; AUDIO_PROPUESTAS_MODEST_2026-10-29.md, P1, D1 y D2).
//  1) P1: en el billar el detector de la presencia mide de 150 Hz hacia arriba: una voz fija con notas de bajo sube y baja mucho menos;
//     sin el billar no cambia nada (ni con SinGrave); cambiarlo en vivo no da chasquido; el disco continuo hereda el filtro.
//  2) «Presencia: nueva o de antes» a ciegas: el resultado, que pide cancion, y que cambia la presencia de la que suena.
//  3) D1: las 16 columnas de la bitacora (promedio y maximo por etapa), el muestreo cada medio segundo y la ventana que lee lo mismo.
//  4) D2: Recoger_sonido_XP.cmd: ASCII con CRLF, copia los archivos del sonido y no borra ni cambia nada.
// Cuentas con senales sinteticas en Linux/Mono; nada oido en el billar.
class FuentePG : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuentePG(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

class PresenciaGravePrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TPre, TTap;
    const int FS = 44100;
    static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static string Texto(string archivo) { return File.ReadAllText(Path.Combine(Path.Combine("..", "Rocola"), archivo)); }
    static object Tap()
    {
        object tap = FormatterServices.GetUninitializedObject(TTap);
        TTap.GetField("Master", T).SetValue(tap, 1f);
        return tap;
    }
    static object Pre(float[] x, bool billar) { return Nuevo(TPre, new FuentePG(x, FS, 2), Tap(), 0.26f, true, billar); }
    static void SinGrave(object p, bool v) { TPre.GetProperty("SinGrave", T).SetValue(p, v, null); }
    static float[] LeeTodo(object p, int bloque, int cambiaEn, bool valor)
    {
        ISampleProvider sp = (ISampleProvider)p;
        List<float> r = new List<float>(); float[] buf = new float[bloque];
        while (true)
        {
            if (cambiaEn >= 0 && r.Count >= cambiaEn) { SinGrave(p, valor); cambiaEn = -1; }
            int n = sp.Read(buf, 0, bloque); if (n <= 0) break;
            for (int i = 0; i < n; i++) r.Add(buf[i]);
        }
        return r.ToArray();
    }
    // Una voz fija (440 a 1760 Hz) y notas de bajo (55 y 110 Hz) cada medio segundo.
    static float[] Mezcla(double seg, double bajoPico, double vozAmp)
    {
        int n = (int)(FS * seg); float[] x = new float[2 * n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / FS;
            double voz = vozAmp * (0.10 * Math.Sin(2 * Math.PI * 440 * t) + 0.06 * Math.Sin(2 * Math.PI * 880 * t + 1) + 0.04 * Math.Sin(2 * Math.PI * 1320 * t + 2) + 0.03 * Math.Sin(2 * Math.PI * 1760 * t + 3));
            double tn = t % 0.5, env = tn < 0.01 ? tn / 0.01 : (tn < 0.3 ? Math.Exp(-(tn - 0.01) / 0.18) : 0.0);
            double bajo = bajoPico * env * (Math.Sin(2 * Math.PI * 55 * t) + 0.35 * Math.Sin(2 * Math.PI * 110 * t + 0.5));
            x[2 * i] = x[2 * i + 1] = (float)(voz + bajo);
        }
        return x;
    }
    static double Goertzel(float[] y, int ini, int largo, double hz)
    {
        double w = 2 * Math.PI * hz / FS, c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
        for (int i = 0; i < largo; i++) { double v = y[2 * (ini + i)] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (largo - 1))); double s0 = v + c * s1 - s2; s2 = s1; s1 = s0; }
        double re = s1 - s2 * Math.Cos(w), im = s2 * Math.Sin(w);
        return Math.Sqrt(re * re + im * im) / (largo * 0.25);
    }
    // Cuanto sube y baja la voz (880 Hz) en la salida frente a la entrada: percentil 95 menos 5, ventanas de 25 ms, de 2 s en adelante.
    static double Vaiven(float[] x, float[] y)
    {
        int vent = (int)(0.025 * FS), salto = vent / 2; List<double> r = new List<double>();
        for (int ini = 2 * FS; ini + vent < x.Length / 2; ini += salto)
            r.Add(20 * Math.Log10(Goertzel(y, ini, vent, 880) / Goertzel(x, ini, vent, 880)));
        r.Sort();
        return r[(int)(r.Count * 0.95)] - r[(int)(r.Count * 0.05)];
    }
    static double SaltoMax(float[] y, int desde, int hasta)
    {
        double m = 0; for (int i = Math.Max(2, desde); i < Math.Min(y.Length, hasta); i++) m = Math.Max(m, Math.Abs(y[i] - y[i - 2])); return m;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TPre = TF.GetNestedType("PresenciaSala", T); TTap = TF.GetNestedType("LevelTap", T);
        Check(TPre != null && TPre.GetProperty("SinGrave", T) != null, "la presencia tiene el detector sin el grave (SinGrave)");

        Console.WriteLine("\n  1) P1: EL DETECTOR SIN EL GRAVE\n");
        double[][] casos = { new double[] { 0.55, 1.0 }, new double[] { 0.40, 1.0 }, new double[] { 0.70, 0.8 }, new double[] { 0.25, 1.4 } };
        bool todos = true; string detalle = "";
        foreach (double[] c in casos)
        {
            float[] x = Mezcla(12, c[0], c[1]);
            object antes = Pre(x, true); SinGrave(antes, false);
            double va = Vaiven(x, LeeTodo(antes, 4096, -1, false)), vn = Vaiven(x, LeeTodo(Pre(x, true), 4096, -1, false));
            todos &= vn < va * 2.0 / 3.0;
            detalle += " " + F(va) + "->" + F(vn);
        }
        Check(todos, "billar: con notas de bajo la voz sube y baja menos de dos tercios que antes (dB, p95-p5:" + detalle + ")");
        float[] xm = Mezcla(6, 0.55, 1.0);
        object fab = Pre(xm, false), fab2 = Pre(xm, false); SinGrave(fab2, true);
        float[] yf = LeeTodo(fab, 4096, -1, false), yf2 = LeeTodo(fab2, 4096, -1, false);
        bool igual = yf.Length == yf2.Length; for (int i = 0; i < yf.Length && igual; i++) igual = yf[i] == yf2[i];
        Check(igual && !(bool)TPre.GetProperty("SinGrave", T).GetValue(fab2, null), "sin el billar no hay filtro: SinGrave no hace nada y sale igual bit a bit");
        object pb = Pre(xm, true);
        Check((bool)TPre.GetProperty("SinGrave", T).GetValue(pb, null), "en el billar va sin el grave de fabrica");
        // Cambiarlo en vivo, a mitad de una nota de bajo: el salto mayor no pasa del de las dos lecturas sin tocar.
        int cambio = 2 * (int)(3.05 * FS);
        object v1 = Pre(xm, true); SinGrave(v1, false);
        float[] yv = LeeTodo(v1, 1000, cambio, true), ya = LeeTodo(Pre(xm, true), 1000, -1, false);
        object v0 = Pre(xm, true); SinGrave(v0, false); float[] yb = LeeTodo(v0, 1000, -1, false);
        double sv = SaltoMax(yv, cambio - 2000, cambio + 2 * FS / 2), sr = Math.Max(SaltoMax(ya, cambio - 2000, cambio + 2 * FS / 2), SaltoMax(yb, cambio - 2000, cambio + 2 * FS / 2));
        Check(sv <= sr * 1.05 + 1e-6, "cambiarlo en vivo no da chasquido (salto " + F(sv * 1000) + " frente a " + F(sr * 1000) + " milesimas)");
        string pre = Texto("PresenciaSala.cs");
        Check(pre.Contains("if (hpOn && a.hpOn) Array.Copy(a.hpEst, hpEst, hpEst.Length);"), "en un disco continuo la siguiente hereda el filtro del detector");
        Check(pre.Contains("if (yh < 1e-30 && yh > -1e-30) yh = 0.0;"), "sin desnormales en el silencio");
        Check(Texto("Form1.Sonido.cs").Contains("new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto);"), "la cadena la arma con el billar (solo ahi va sin el grave)");

        Console.WriteLine("\n  2) PRESENCIA: NUEVA O DE ANTES, A CIEGAS\n");
        MethodInfo resC = TF.GetMethod("ResultadoCiego", T);
        Check(((string)resC.Invoke(null, new object[] { 3, 1, 2 })).Contains("la presencia nueva") && ((string)resC.Invoke(null, new object[] { 0, 2, 2 })).Contains("la presencia de antes"),
              "al final dice si te gusto mas la presencia nueva o la de antes");
        object g = FormatterServices.GetUninitializedObject(TF);
        TF.GetField("presenciaSinGrave", T).SetValue(g, true);
        FieldInfo la = TF.GetField("localAuto", T); bool laAntes = (bool)la.GetValue(null);
        la.SetValue(null, true);
        string sinCancion = (string)TF.GetMethod("EmpiezaCiego", T).Invoke(g, new object[] { 2 });
        Check(sinCancion.StartsWith("Pon una cancion"), "sin cancion no empieza");
        la.SetValue(null, false);
        Check(((string)TF.GetMethod("EmpiezaCiego", T).Invoke(g, new object[] { 2 })).StartsWith("Enciende el modo billar"), "y sin el modo billar tampoco");
        la.SetValue(null, laAntes);
        // CiegoPon cambia la presencia de la que suena
        Type TCad = TF.GetNestedType("CadenaCancion", T); object cad = Activator.CreateInstance(TCad, true);
        object pc = Pre(xm, true); TCad.GetField("Presencia", T).SetValue(cad, pc);
        TF.GetField("cadenaSonando", T).SetValue(g, cad); TF.GetField("ciegoQue", T).SetValue(g, 2);
        MethodInfo pon = TF.GetMethod("CiegoPon", T);
        pon.Invoke(g, new object[] { false }); bool trasFalso = (bool)TPre.GetProperty("SinGrave", T).GetValue(pc, null);
        pon.Invoke(g, new object[] { true }); bool trasVerdad = (bool)TPre.GetProperty("SinGrave", T).GetValue(pc, null);
        Check(!trasFalso && trasVerdad && (bool)TF.GetField("presenciaSinGrave", T).GetValue(g), "cada tramo cambia en vivo la presencia de la cancion que suena, y al acabar vuelve la nueva");
        string aj = Texto("Form1.Ajustes.cs");
        Check(aj.Contains("\"SonidoCiegoPresencia\"") && aj.Contains("EmpiezaCiego(CiegoPresencia)"), "el boton «Presencia nueva o antes» en Lo que suena ahora");
        Check(Texto("Form1.Sonido.cs").Contains("if (!presenciaSinGrave) c.Presencia.SinGrave = false;"), "una cancion armada en un tramo «de antes» nace como ese tramo");

        Console.WriteLine("\n  3) D1: LO QUE HIZO CADA ETAPA, EN LA BITACORA\n");
        Type TAct = TF.GetNestedType("ActividadEtapas", T);
        object act = Activator.CreateInstance(TAct, true);
        MethodInfo anota = TAct.GetMethod("Anota", T);
        anota.Invoke(act, new object[] { new double[] { 2, 3, 1, 0.5, 1, 0.2, 0, 4 } });
        anota.Invoke(act, new object[] { new double[] { 4, 1, 3, 1.5, -1, double.NaN, 0, 2 } });
        string cols = (string)TF.GetMethod("ColumnasActividad", T).Invoke(null, new object[] { act });
        string colsNull = (string)TF.GetMethod("ColumnasActividad", T).Invoke(null, new object[] { null });
        Check(cols == "\t3.00\t4.0\t2.00\t3.0\t2.00\t3.0\t1.00\t1.5\t0.50\t1.0\t0.10\t0.2\t0.00\t0.0\t3.00\t4.0", "promedio y maximo de cada etapa; lo negativo o roto cuenta como nada (" + cols.Replace("\t", " ") + ")");
        Check(colsNull.Split('\t').Length == 17 && colsNull.EndsWith("\t-\t-"), "sin muestras: 16 guiones");
        string cab = (string)TF.GetField("BitacoraBillarCabecera", T).GetRawConstantValue();
        Type TAj = TF.GetNestedType("AjusteLocal", T); object ajB = Activator.CreateInstance(TAj, true);
        string lin = (string)TF.GetMethod("LineaBitacoraBillar", T).Invoke(null, new object[] { new DateTime(2026, 10, 30, 21, 0, 0), "a.mp3", true, 200.0, 0f, float.NaN, 0f, 0f, 0f, 0f, 0f, ajB, false, 1f, 40f, null, act });
        Check(cab.Split('\t').Length == 44 && lin.Split('\t').Length == cab.Split('\t').Length && cab.Contains("\teq15k8\tpresencia_baja_media\t") && cab.EndsWith("\teses_max"),
              "la cabecera crece al final (las 28 de antes no se mueven) y cada linea lleva tantas columnas");
        Check(Texto("Form1.Video.cs").Contains("MuestreaEtapas(cadenaSonando);") && Texto("BitacoraBillar.cs").Contains("c.Tap != null ? c.Tap.EqDb : null, c.Actividad);"),
              "el reloj de la cancion muestrea cada medio segundo y la bitacora apunta lo de esa cancion");
        string lq = Texto("LoQueSuena.cs");
        Check(!lq.Contains("c.Eses.BajaDb") && !lq.Contains("c.Retumbe.CortaDb") && !lq.Contains("c.Ataque.Toma(") && !lq.Contains("c.Aspereza.CortaDb") && lq.Contains("act.Ultimo[ActividadEtapas.Eses]"),
              "la ventana lee lo muestreado: las lecturas que se ponen a cero tienen un solo lector");

        Console.WriteLine("\n  4) D2: RECOGER LOS DATOS DEL SONIDO\n");
        string ruta = Path.Combine("..", "Recoger_sonido_XP.cmd");
        byte[] b = File.ReadAllBytes(ruta); bool ascii = true, crlf = true;
        for (int i = 0; i < b.Length; i++) { if (b[i] > 127) ascii = false; if (b[i] == 10 && (i == 0 || b[i - 1] != 13)) crlf = false; }
        string cmd = File.ReadAllText(ruta);
        Check(ascii && crlf, "el guion va en ASCII con CRLF, como Recoger_error_XP.cmd");
        bool todosArch = true;
        foreach (string a in new string[] { "rocola_billar_bitacora.txt", "rocola_billar_bitacora.txt.1", "rocola_billar_nivel.txt", "rocola_sonoridad.txt", "rocola_volumen.txt", "rocola_eq.txt", "rocola_config.txt", "rocola_error.txt" })
            todosArch &= cmd.Contains(a);
        string bajo = cmd.ToLowerInvariant();
        Check(todosArch && cmd.Contains("Sonido_XP") && cmd.Contains("ROCOLA_DATOS"), "copia los ocho archivos del sonido a Sonido_XP (de la carpeta de datos si la hay)");
        bool soloCopia = !bajo.Contains(" > \"%rocola_origen%") && !bajo.Contains(">> \"%rocola_origen%");
        foreach (string l0 in bajo.Split('\n'))
        {
            string l = l0.Trim(); if (l.StartsWith("rem ") || l.StartsWith("echo ")) continue;
            foreach (string orden in new string[] { "del ", "erase ", "reg ", "move ", "rmdir ", "rd ", "ren ", "attrib " }) if (l.StartsWith(orden) || l.Contains("& " + orden)) soloCopia = false;
        }
        Check(soloCopia, "solo copia: no borra, no mueve, no toca el registro ni escribe en los datos");
        Check(File.ReadAllText(Path.Combine("..", ".gitignore")).Contains("Sonido_XP/"), "y la carpeta no se sube a Git");

        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos (cuentas en Linux/Mono; nada oido en el billar)");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
