// EL MOTOR NUEVO FRENTE A LA ROCOLA DE XP (Rocola OS, etapa 2). El mismo programa se compila dos veces: con Mono contra Rocola.exe
// de XP (NAudio 1.9) y con .NET 10 contra el motor (NAudio 3). Pasa la misma musica sintetica por cada etapa de la cadena, con sus
// variantes, y escribe lo que sale (float32 crudo) en una carpeta. compara.py compara las dos carpetas muestra a muestra.
//   uso: CompararConXP <ensamblado> <carpeta de salida>
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

class FuenteFija : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteFija(float[] d, int fs, int ch) { x = d; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
#if NET
    public int Read(Span<float> b) { int k = Math.Min(b.Length, x.Length - pos); if (k <= 0) return 0; x.AsSpan(pos, k).CopyTo(b); pos += k; return k; }
#else
    public int Read(float[] b, int o, int n) { int k = Math.Min(n, x.Length - pos); if (k <= 0) return 0; Array.Copy(x, pos, b, o, k); pos += k; return k; }
#endif
}

static class CompararConXP
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly asm;
    static Type Tipo(string nombre)
    {
        foreach (Type t in asm.GetTypes()) if (t.Name == nombre) return t;
        throw new Exception("no esta el tipo " + nombre);
    }
    // Musica de prueba: bombo, bajo, una voz con silabas, charles y un fondo de ruido; con estrofa suave y coro fuerte y un zumbido de 60 Hz.
    static float[] Musica(int fs, double seg, double nivel)
    {
        int n = (int)(fs * seg); float[] x = new float[2 * n]; Random r = new Random(7); double hp = 0, ant = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / fs, w = r.NextDouble() * 2 - 1; hp = 0.6 * (hp + w - ant); ant = w;
            double sec = (t % 6.0) < 3.0 ? 0.45 : 1.0, sil = Math.Max(0, Math.Sin(2 * Math.PI * 2.6 * t));
            double voz = sil * (0.08 * Math.Sin(2 * Math.PI * 330 * t) + 0.05 * Math.Sin(2 * Math.PI * 660 * t + 1) + 0.03 * Math.Sin(2 * Math.PI * 3300 * t) + 0.02 * Math.Sin(2 * Math.PI * 6800 * t)) * (0.6 + 0.8 * sec);
            double tb = t % 0.5, kick = 0.5 * Math.Exp(-tb / 0.11) * Math.Sin(2 * Math.PI * (48 + 55 * Math.Exp(-tb / 0.03)) * tb);
            double bajo = 0.22 * sec * Math.Exp(-(t % 0.5) / 0.25) * (Math.Sin(2 * Math.PI * 55 * t) + 0.4 * Math.Sin(2 * Math.PI * 110 * t));
            double hat = 0.06 * sec * Math.Exp(-(t % 0.25) / 0.02) * hp, hum = 0.004 * Math.Sin(2 * Math.PI * 60 * t);
            double l = nivel * (voz + kick * sec + bajo + hat + hum), rr = nivel * (voz * 0.9 + kick * sec + bajo + hat * 1.2 + hum);
            x[2 * i] = (float)l; x[2 * i + 1] = (float)rr;
        }
        return x;
    }
    static void Pasa(string nombre, object etapa, string dir)
    {
        MethodInfo lee = null;
        foreach (MethodInfo m in etapa.GetType().GetMethods(T))
            if (m.Name == "Read" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(float[])) lee = m;
        List<float> y = new List<float>(); float[] b = new float[4096]; int n; int vueltas = 0;
        Random r = new Random(3);
        while ((n = (int)lee.Invoke(etapa, new object[] { b, 0, 1024 + 2 * r.Next(1500) })) > 0 && vueltas++ < 100000) for (int i = 0; i < n; i++) y.Add(b[i]);
        using (BinaryWriter w = new BinaryWriter(File.Create(Path.Combine(dir, nombre + ".f32")))) foreach (float v in y) w.Write(v);
        Console.WriteLine("  " + nombre + ": " + y.Count + " muestras");
    }
    static object Nueva(string tipo, params object[] a) { return Activator.CreateInstance(Tipo(tipo), T, null, a, null); }
    // La ventana "duena" del nivelado, sin encender (en XP con el ayudante de las pruebas; en el motor basta un objeto vacio).
    static object Duena()
    {
#if NET
        return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(Tipo("Form1"));
#else
        return Codigo.FormSinEncender(Tipo("Form1"));
#endif
    }
    static object Nivelado(ISampleProvider s, float ganancia, float[] eq, float realce)
    {
        object t = Nueva("LevelTap", s, Duena());
        t.GetType().GetField("Gain", T).SetValue(t, ganancia);
        t.GetType().GetField("Excite", T).SetValue(t, realce);
        if (eq != null) t.GetType().GetProperty("EqDb", T).SetValue(t, eq, null);
        return t;
    }
    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        string dir = args[1]; Directory.CreateDirectory(dir);
        const int FS = 44100;
        float[] x = Musica(FS, 12, 1.0), fuerte = Musica(FS, 12, 2.2);
        Func<float[], ISampleProvider> F = d => new FuenteFija(d, FS, 2);
        float techo = (float)Tipo("Form1").GetField("TechoPicoReal", T).GetValue(null);
        Pasa("limitador", Nueva("LimitadorPicoReal", F(fuerte), techo, 3.0, false), dir);
        Pasa("limitador_respira", Nueva("LimitadorPicoReal", F(fuerte), techo, 3.0, true), dir);
        Pasa("presencia", Nueva("PresenciaSala", F(x), null, 0.26f, true, false), dir);
        Pasa("presencia_billar", Nueva("PresenciaSala", F(x), null, 0.26f, true, true), dir);
        Pasa("local", Nueva("SalaLocal", F(x), null, 0.26f, true, true, 2, 3, -2), dir);
        Pasa("grave_mono", Nueva("GraveMono", F(x), 120f, 0.6f, 40f, true, true), dir);
        Pasa("eses", Nueva("SuavizaEses", F(x), true, false), dir);
        Pasa("eses_billar", Nueva("SuavizaEses", F(x), true, true), dir);
        Pasa("retumbe", Nueva("RetumbeDinamico", F(x), true, false, 0.0), dir);
        Pasa("ataque", Nueva("AtaqueClaro", F(x), true), dir);
        Pasa("asperos", Nueva("AsperezaDinamica", F(x), true), dir);
        Pasa("bombo", Nueva("BomboSinHundir", F(x), true), dir);
        Pasa("zumbido", Nueva("QuitaZumbido", F(x), 60, true), dir);
        Pasa("preeco", Nueva("QuitaPreEco", F(x)), dir);
        Pasa("arreglos", Nueva("ArreglaGrabacion", F(x), true, true), dir);
        Pasa("nivelado", Nivelado(F(x), 2.0f, null, 0f), dir);
        Pasa("nivelado_eq", Nivelado(F(x), 1.5f, new float[] { -2f, 1f, 0f, 1.5f, -1f, 2f, 1f, -3f }, 0f), dir);
        Pasa("nivelado_realce", Nivelado(F(x), 1.2f, new float[] { 0f, 0f, 1f, 0f, 0f, 0f, 2f, 0f }, 0.6f), dir);
        // La medida de la sonoridad (el repaso): los numeros de la medida, en texto.
        object m = Nueva("MedidorBs1770", FS, new double[] { 1.0, 1.0 });
        m.GetType().GetMethod("Procesa", T).Invoke(m, new object[] { x, 0, x.Length });
        MethodInfo cierra = Tipo("Form1").GetMethod("CierraIntegrada", T); object[] args2 = { m, 12.0, FS, null }; cierra.Invoke(null, args2);
        StringWriter sw = new StringWriter();
        Tipo("Form1").GetMethod("EscribeIntegRegistro", T).Invoke(null, new object[] { sw, "prueba.wav", args2[3] });
        File.WriteAllText(Path.Combine(dir, "sonoridad.txt"), sw.ToString());
        Console.WriteLine("  sonoridad: " + sw.ToString().Trim());
        return 0;
    }
}
