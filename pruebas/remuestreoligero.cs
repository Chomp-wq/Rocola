using System;
using System.Reflection;
using NAudio.Wave;

// EL CONVERTIDOR DE FRECUENCIA, MAS LIGERO Y CON LOS MISMOS BITS (SinPausa.cs, RemuestreoContinuo; 13-10-2026).
//
// El convertidor (el de "Tarjeta a 44,1/48 kHz" y el del relevo sin pausa entre tasas distintas) interpolaba en cada
// cuadro de salida sus 192 coeficientes entre dos fases de la tabla. La fase solo toma tasaOut / mcd(tasaIn, tasaOut)
// valores (160 de 44,1 a 48 kHz), asi que ahora se interpolan una vez ("filas") y el cuadro solo los lee; y el anillo
// guarda cada cuadro dos veces, en double, para que la ventana no de la vuelta. Aqui se compara, en la misma rocola,
// el camino nuevo con el de siempre (el mismo convertidor con 'filas' quitadas a mano): tienen que salir los mismos
// bits, en muchos pares de tasas, en mono, estereo y 5.1, y con cebado descartado.
// Son senales sinteticas: miden la cuenta, no el oido.
class RemuestreoLigero
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }

    class Senal : ISampleProvider
    {
        readonly WaveFormat wf; readonly Random r; long quedan; double fase;
        public Senal(WaveFormat w, long cuadros, int semilla) { wf = w; r = new Random(semilla); quedan = cuadros * w.Channels; }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int n)
        {
            int k = (int)Math.Min(n, quedan);
            for (int i = 0; i < k; i++) { fase += 0.0313; b[o + i] = (float)(0.6 * Math.Sin(fase) + 0.35 * (r.NextDouble() - 0.5)); }
            quedan -= k; return k;
        }
    }

    static float[] Convierte(Type tr, WaveFormat de, WaveFormat a, int descarta, bool sinFilas, int semilla, int bloque, out bool usoFilas)
    {
        object rc = Activator.CreateInstance(tr, B, null, new object[] { new Senal(de, de.SampleRate * 2 + 777, semilla), a, descarta }, null);
        FieldInfo ff = tr.GetField("filas", B);
        usoFilas = ff != null && ff.GetValue(rc) != null;
        if (sinFilas && ff != null) ff.SetValue(rc, null);
        ISampleProvider sp = (ISampleProvider)rc;
        System.Collections.Generic.List<float> y = new System.Collections.Generic.List<float>();
        float[] b = new float[bloque]; int k;
        while ((k = sp.Read(b, 0, b.Length)) > 0) for (int i = 0; i < k; i++) y.Add(b[i]);
        return y.ToArray();
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type tr = asm.GetType("Rocola.RemuestreoContinuo", true), tfa = asm.GetType("Rocola.FormatoAudio", true);
        MethodInfo flot = tfa.GetMethod("Flotante", B);
        Check(tr.GetField("filas", B) != null, "el convertidor tiene sus fases ya interpoladas ('filas')");
        int[][] pares = { new[] { 44100, 48000 }, new[] { 48000, 44100 }, new[] { 22050, 48000 }, new[] { 32000, 44100 },
                          new[] { 96000, 48000 }, new[] { 88200, 44100 }, new[] { 11025, 44100 }, new[] { 16000, 48000 }, new[] { 44100, 88200 } };
        int pruebas = 0, conFilas = 0;
        foreach (int[] p in pares)
            foreach (int ch in new[] { 1, 2, 6 })
            {
                int mask = ch == 1 ? 4 : ch == 2 ? 3 : 0x3F;
                WaveFormat de = (WaveFormat)flot.Invoke(null, new object[] { p[0], ch, mask }), a = (WaveFormat)flot.Invoke(null, new object[] { p[1], ch, mask });
                if (!(bool)tr.GetMethod("SePuede", B).Invoke(null, new object[] { de, a })) continue;   // fuera de presupuesto: va directo
                foreach (int descarta in new[] { 0, 37 })
                {
                    bool filas, nada;
                    int bloque = (ch * 997) | 1;   // lecturas de tamano raro: cuadros partidos entre lecturas
                    float[] nuevo = Convierte(tr, de, a, descarta, false, p[0] + ch, bloque, out filas);
                    float[] viejo = Convierte(tr, de, a, descarta, true, p[0] + ch, bloque, out nada);
                    bool igual = nuevo.Length == viejo.Length && nuevo.Length > 0;
                    for (int i = 0; igual && i < nuevo.Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(nuevo[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(viejo[i]), 0)) igual = false;
                    Check(igual, p[0] + " -> " + p[1] + " Hz, " + ch + " canales, cebado " + descarta + ": los mismos bits con y sin las fases interpoladas (" + nuevo.Length + " / " + viejo.Length + " muestras)");
                    pruebas++; if (filas) conFilas++;
                    if ((p[0] == 44100 && p[1] == 48000) || (p[0] == 48000 && p[1] == 44100))
                        Check(filas, p[0] + " -> " + p[1] + " Hz, " + ch + " canales: usa las fases interpoladas");
                }
            }
        Check(pruebas >= 30, "se probaron " + pruebas + " combinaciones de tasas, canales y cebado (" + conFilas + " con las fases interpoladas)");
        Console.WriteLine(mal == 0 ? "  OK remuestreoligero: " + ok + " comprobaciones (" + pruebas + " conversiones, mismos bits; senales sinteticas)"
                                   : "  remuestreoligero: " + mal + " FALLOS de " + (ok + mal));
        return mal;
    }
}
