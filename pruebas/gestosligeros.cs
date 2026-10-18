using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

// Comparacion independiente de todas las componentes de la pose contra otro release.
// Uso: gestosligeros Rocola.exe [anterior.exe | medir]. Sin GPU real ni audio.
class GestosLigeros
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    delegate float Actua(double vida, double edad, int gesto, int seleccion, bool animada);
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static int Bits(float f) { return BitConverter.ToInt32(BitConverter.GetBytes(f), 0); }
    static MethodInfo Method(Assembly a) { return a.GetType("Rocola.GpuPanel").GetMethod("AsistenteActua", B); }
    static Actua Fast(MethodInfo method)
    {
        DynamicMethod m = new DynamicMethod("PoseSinReflection", typeof(float),
            new Type[] { typeof(double), typeof(double), typeof(int), typeof(int), typeof(bool) }, typeof(GestosLigeros), true);
        ILGenerator il = m.GetILGenerator(); LocalBuilder pose = il.DeclareLocal(method.ReturnType);
        for (int i = 0; i < 5; i++) il.Emit(OpCodes.Ldarg, (short)i);
        il.Emit(OpCodes.Call, method); il.Emit(OpCodes.Stloc, pose); il.Emit(OpCodes.Ldloca, pose);
        il.Emit(OpCodes.Ldfld, method.ReturnType.GetField("Rebote", B)); il.Emit(OpCodes.Ret);
        return (Actua)m.CreateDelegate(typeof(Actua));
    }
    static void Measure(MethodInfo method)
    {
        Actua run = Fast(method); double sum = 0;
        for (int gesture = 0; gesture < 9; gesture++) {
            double[] samples = new double[31];
            for (int b = -3; b < samples.Length; b++) {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 10000; i++) sum += run(i / 240.0, (i % 1440) / 240.0, gesture, i % 3, true);
                double us = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency / 10000;
                if (b >= 0) samples[b] = us;
            }
            Array.Sort(samples);
            Console.WriteLine("gesto=" + gesture + " mediana_us=" + samples[15].ToString("F4", CultureInfo.InvariantCulture)
                + " p95_lote_us=" + samples[29].ToString("F4", CultureInfo.InvariantCulture));
        }
        Console.WriteLine("Control=" + sum.ToString("R", CultureInfo.InvariantCulture) + "; tiempos de calculo de pose, no FPS ni GPU.");
    }
    static void Compare(MethodInfo current, MethodInfo old, object[] args)
    {
        object a = current.Invoke(null, args), b = old == null ? null : old.Invoke(null, args);
        foreach (FieldInfo field in current.ReturnType.GetFields(B)) {
            float value = (float)field.GetValue(a);
            Check(!float.IsNaN(value) && !float.IsInfinity(value), "Pose finita: " + field.Name);
            if (b != null) Check(Bits(value) == Bits((float)old.ReturnType.GetField(field.Name, B).GetValue(b)),
                "Componente exacta: " + field.Name + " vida=" + args[0] + " edad=" + args[1] + " gesto=" + args[2]);
        }
    }
    static int Main(string[] args)
    {
        try {
            MethodInfo current = Method(Assembly.LoadFile(System.IO.Path.GetFullPath(args[0])));
            if (args.Length > 1 && args[1] == "medir") { Measure(current); return 0; }
            MethodInfo old = args.Length > 1 ? Method(Assembly.LoadFile(System.IO.Path.GetFullPath(args[1]))) : null;
            if (old == null) Console.WriteLine("Comparacion con release anterior OMITIDA: falta segundo ejecutable.");
            double[] edges = { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, 0, .02, .05, .18, .6, .7, .75, .85, 1.4, 3.8, 4.6, 7, 13, 21, 1000000 };
            for (int gesture = 0; gesture < 9; gesture++) {
                // Todos los ciclos de espera, transiciones de gesto y elecciones; 240 muestras/s.
                for (int i = 0; i < 5040; i++) Compare(current, old, new object[] { i / 240.0, (i % 1440) / 240.0, gesture, i % 3, true });
                foreach (double life in edges) foreach (double age in edges)
                    foreach (bool animated in new bool[] { false, true }) Compare(current, old, new object[] { life, age, gesture, 0, animated });
            }
            Console.WriteLine("OK " + checks + " comprobaciones; 51858 poses, nueve gestos, animacion y tiempos excepcionales.");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
