using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// LA PRIMERA PASADA DEL NIVELADO, POR BANDAS CUANDO NADA CAMBIA (22-10-2026; Realce.cs, LevelTap.PrimeraPasadaQuieta).
//
// LevelTap aplica la ganancia, el EQ automatico (hasta 8 bandas en cascada por muestra) y la mezcla del realce muestra a muestra, con cuatro cosas por cuadro (las rampas, el cruce de
// EQ, la limpieza de restos diminutos cada 64 cuadros). Casi siempre nada cambia: entonces el trozo (512 cuadros) se hace por bandas, cada una de punta a punta con su memoria en
// variables, los dos canales de un estereo a la vez, y la ganancia y la mezcla como constantes. Es la misma cuenta de cada muestra en el mismo orden; la limpieza cae en los mismos
// cuadros. Aqui se compara el LevelTap de verdad consigo mismo con el gancho de pruebas LevelTap.SinBandas (true = siempre cuadro a cuadro, como antes): la MISMA entrada, los mismos
// cambios en los mismos momentos y lecturas de todos los tamanos (muchas parten un cuadro), y se exige que la salida sea igual BIT A BIT y que lo que queda por dentro tambien (la
// memoria del EQ, la del realce, las rampas, el cruce, la cuenta de la limpieza).
// Escenarios: 1, 2, 3 y 6 canales (con LFE sin EQ), 44,1 y 48 kHz; EQ de 8 bandas, de 5, de 3, de una, sin ninguna activa y sin curva; cambios de curva a media cancion (cruce de 30 ms), de ganancia
// (rampa de 20 ms) y de realce (de apagado a encendido y al reves); musica, un silencio de ceros exactos de 3 s (para la limpieza de restos) y un trozo de -180 dB.
class EqPorBandas
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static int Bits(float x) { return BitConverter.ToInt32(BitConverter.GetBytes(x), 0); }

    static Type TForm, TTap;

    sealed class Fuente : ISampleProvider
    {
        readonly WaveFormat f; readonly int canales; long cuadro; readonly Random r = new Random(11);
        public Fuente(WaveFormat f) { this.f = f; canales = f.Channels; }
        public WaveFormat WaveFormat { get { return f; } }
        public int Read(float[] b, int o, int c)
        {
            for (int i = 0; i < c; i++)
            {
                long fr = (cuadro + (i / canales)); int ca = i % canales;
                double t = fr / (double)f.SampleRate;
                double amp = 0.3;
                if (t >= 2.0 && t < 3.0) amp = 0.0;                     // silencio de ceros exactos
                else if (t >= 3.5 && t < 4.5) amp = 1e-9;              // -180 dB
                double v = amp * (0.5 * Math.Sin(2 * Math.PI * (110 + 55 * ca) * t) + 0.3 * Math.Sin(2 * Math.PI * (880 + 130 * ca) * t) + 0.2 * Math.Sin(2 * Math.PI * 5300 * t)
                                  + 0.15 * (r.NextDouble() * 2 - 1));
                if (amp == 0.0) v = 0.0;
                b[o + i] = (float)v;
            }
            cuadro += c / canales;
            return c;
        }
    }

    static object CreaOwner()
    {
        object owner = FormatterServices.GetUninitializedObject(TForm);
        foreach (FieldInfo fi in TForm.GetFields(F))
            if (!fi.IsStatic && fi.FieldType.Namespace != null && fi.FieldType.Namespace.StartsWith("System.Collections") && fi.FieldType.GetConstructor(Type.EmptyTypes) != null)
                fi.SetValue(owner, Activator.CreateInstance(fi.FieldType));
        TForm.GetField("levelLock", F).SetValue(owner, new object());
        TForm.GetField("audioDatosLock", F).SetValue(owner, new object());
        return owner;
    }

    static WaveFormat Formato(int hz, int canales)
    {
        if (canales == 6)   // 5.1 con LFE: el LFE no pasa por el EQ
            return (WaveFormat)TForm.Assembly.GetType("Rocola.FormatoAudio", true).GetMethod("Flotante", F).Invoke(null, new object[] { hz, 6, 0x3F });
        return WaveFormat.CreateIeeeFloatWaveFormat(hz, canales);
    }

    static float[] Curva(int caso)
    {
        switch (caso)
        {
            case 0: return new float[] { 1.5f, -2f, 0.8f, 3f, -1.2f, 2.2f, -0.9f, 1.1f };
            case 1: return new float[] { 0f, -2f, 0f, 3f, 0f, 2.2f, 0f, 0f };
            case 2: return new float[] { 0.05f, -0.05f, 0f, 0.09f, 0f, 0.01f, 0f, -0.09f };   // ninguna activa (< 0,1 dB)
            case 3: return new float[] { -3f, 2.5f, -1f, 0.5f, 4f, -4f, 1f, 2f };
            case 5: return new float[] { 2f, 0f, -1.5f, 0f, 3f, 0f, -2f, 1f };                // 5 activas: dos pares y una suelta
            case 6: return new float[] { 0f, 0f, 0f, 0f, 0f, 2.7f, 0f, 0f };                  // una sola
            default: return null;
        }
    }

    static string Estado(object tap)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string n in new string[] { "limpia", "audioChannel", "eqFadeLeft", "eqMix", "modoRealce" }) sb.Append(n).Append('=').Append(TTap.GetField(n, F).GetValue(tap)).Append(';');
        foreach (string n in new string[] { "gainRamp", "exciteRamp" })
        {
            object rampa = TTap.GetField(n, F).GetValue(tap);
            foreach (FieldInfo fi in rampa.GetType().GetFields(F)) { object v = fi.GetValue(rampa); sb.Append(n).Append('.').Append(fi.Name).Append('=').Append(v is float ? Bits((float)v).ToString("X8") : v).Append(';'); }
        }
        float[] lp = (float[])TTap.GetField("exLp", F).GetValue(tap); foreach (float v in lp) sb.Append(Bits(v).ToString("X8")).Append(',');
        foreach (string n in new string[] { "currentEq", "previousEq" })
        {
            object eq = TTap.GetField(n, F).GetValue(tap);
            sb.Append(n).Append('=');
            if (eq == null) { sb.Append("null;"); continue; }
            foreach (float v in (float[])eq.GetType().GetField("State", F).GetValue(eq)) sb.Append(Bits(v).ToString("X8")).Append(',');
            sb.Append(';');
        }
        // la memoria del realce (RealceSobremuestreado): sus arreglos por canal
        object re = TTap.GetField("realce", F).GetValue(tap);
        foreach (string n in new string[] { "xh", "xv", "xg", "xn" })
        {
            double[][] a = (double[][])re.GetType().GetField(n, F).GetValue(re);
            ulong h = 14695981039346656037UL;
            foreach (double[] d in a) foreach (double x in d) { h ^= (ulong)BitConverter.DoubleToInt64Bits(x); h *= 1099511628211UL; }
            sb.Append(n).Append('=').Append(h.ToString("X16")).Append(';');
        }
        float[] lpr = (float[])re.GetType().GetField("lp", F).GetValue(re); foreach (float v in lpr) sb.Append(Bits(v).ToString("X8")).Append(',');
        return sb.ToString();
    }

    // Una pasada completa del guion con el gancho puesto; devuelve el hash de todo lo que salio y el estado final.
    static void Corre(bool sinBandas, int hz, int canales, int curva0, int guion, out ulong hash, out long muestras, out string estado)
    {
        TTap.GetField("SinBandas", F).SetValue(null, sinBandas);
        ISampleProvider tap = (ISampleProvider)Activator.CreateInstance(TTap, F, null, new object[] { new Fuente(Formato(hz, canales)), CreaOwner() }, null);
        PropertyInfo pGain = TTap.GetProperty("Gain", F), pMaster = TTap.GetProperty("Master", F), pExcite = TTap.GetProperty("Excite", F), pEq = TTap.GetProperty("EqDb", F);
        Action<string, object> pon = delegate(string n, object v)
        {
            PropertyInfo p = n == "Gain" ? pGain : n == "Master" ? pMaster : n == "Excite" ? pExcite : pEq;
            if (p != null) p.SetValue(tap, v, null); else TTap.GetField(n, F).SetValue(tap, v);
        };
        pon("Gain", 1f); pon("Master", 1f); pon("Excite", guion == 2 ? 0f : 0.6f); pon("EqDb", Curva(curva0));
        int[] tamanos = guion == 1 ? new int[] { 1024, 4410, 2048 } : new int[] { 1, 3, 1001, 4096, 7, 2205, 333, 4410, 999, 64, 65, 129 };
        float[] buf = new float[8192 * 6];
        ulong h = 14695981039346656037UL; long total = 0; int lecturas = 0;
        long meta = (long)hz * canales * 6;   // 6 segundos: pasa por el silencio de 2 a 3 s y el trozo de -180 dB
        int evento = 0;
        while (total < meta)
        {
            // los cambios del guion, en momentos fijos (en segundos de audio)
            double seg = total / (double)(hz * canales);
            if (evento == 0 && seg >= 0.4) { pon("EqDb", Curva((curva0 + 3) % 4)); evento++; }         // cruce de EQ
            else if (evento == 1 && seg >= 0.9) { pon("Gain", 0.7f); evento++; }                         // rampa de ganancia
            else if (evento == 2 && seg >= 1.5) { pon("Excite", guion == 2 ? 0.8f : 0f); evento++; }     // el realce se apaga (o se enciende)
            else if (evento == 3 && seg >= 2.4) { pon("Master", 1.3f); evento++; }                       // en pleno silencio
            else if (evento == 4 && seg >= 3.2) { pon("EqDb", null); evento++; }                         // sin EQ
            else if (evento == 5 && seg >= 4.2) { pon("EqDb", Curva(curva0)); evento++; }
            else if (evento == 6 && seg >= 5.0) { pon("Gain", 1f); pon("Excite", 0.5f); evento++; }
            int n = tamanos[(lecturas++) % tamanos.Length];   // tal cual: muchos parten un cuadro
            int leidos = tap.Read(buf, 0, n);
            for (int i = 0; i < leidos; i++) { h ^= (uint)Bits(buf[i]); h *= 1099511628211UL; }
            total += leidos;
            if (leidos <= 0) break;
        }
        hash = h; muestras = total; estado = Estado(tap);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TForm = asm.GetType("Rocola.Form1", true);
        TTap = TForm.GetNestedType("LevelTap", F);
        Check(TTap != null && TTap.GetField("SinBandas", F) != null, "LevelTap trae el gancho de pruebas SinBandas");
        if (mal > 0) { Console.WriteLine("  FALLO eqporbandas: esta rocola no tiene la pasada por bandas"); return 1; }
        FieldInfo gancho = TTap.GetField("SinBandas", F);
        Check(!(bool)gancho.GetValue(null), "de fabrica, por bandas (SinBandas = false)");
        int iguales = 0, distintos = 0, escenarios = 0;
        try
        {
            foreach (int hz in new int[] { 44100, 48000 })
            foreach (int canales in new int[] { 1, 2, 3, 6 })
            foreach (int curva in new int[] { 0, 1, 2, 3, 4, 5, 6 })
            foreach (int guion in new int[] { 0, 1, 2 })
            {
                if (hz == 48000 && (canales != 2 || curva > 1)) continue;   // el resto de las frecuencias no cambia la cuenta
                if (guion == 1 && (canales == 3 || curva == 3 || curva == 6 || curva == 2)) continue;
                if (guion == 2 && (curva == 2 || curva == 4)) continue;
                ulong hA, hB; long nA, nB; string eA, eB;
                Corre(true, hz, canales, curva, guion, out hA, out nA, out eA);
                Corre(false, hz, canales, curva, guion, out hB, out nB, out eB);
                escenarios++;
                bool igual = hA == hB && nA == nB && eA == eB;
                if (igual) iguales++;
                else
                {
                    distintos++;
                    if (distintos <= 4) Console.WriteLine("  FALLO " + hz + " Hz, " + canales + " canales, curva " + curva + ", guion " + guion + ": " + (hA != hB ? "la salida difiere" : "el estado difiere"));
                }
            }
        }
        finally { gancho.SetValue(null, false); }
        Check(distintos == 0, escenarios + " escenarios: " + iguales + " iguales bit a bit (salida de 6 s y estado interno) entre la pasada por bandas y la de cuadro a cuadro (" + distintos + " distintos)");

        // que el camino por bandas se usa de verdad (si no, la comparacion no probaria nada): casi todos los trozos de una cancion corriente
        Check(TTap.GetMethod("PrimeraPasadaQuieta", F) != null && TTap.GetMethod("BandasDelTrozo", F) != null, "LevelTap trae PrimeraPasadaQuieta y BandasDelTrozo");
        {
            FieldInfo contador = TTap.GetField("TrozosPorBandas", F);
            contador.SetValue(null, 0L);
            ulong h; long n; string e;
            Corre(false, 44100, 2, 0, 1, out h, out n, out e);
            long trozos = (long)contador.GetValue(null), esperados = n / 2 / 512;
            Check(trozos > esperados * 7 / 10, "estereo con EQ de 8 bandas y lecturas de 4.410 muestras: " + trozos + " trozos de ~" + esperados + " van por bandas");
        }

        Console.WriteLine();
        Console.WriteLine(mal == 0 ? "OK eqporbandas: " + ok + " comprobaciones" : "FALLAN " + mal + " de " + (ok + mal));
        return mal == 0 ? 0 : 1;
    }
}
