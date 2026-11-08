// LA PRUEBA DEL RELOJ DE LA PANTALLA (pruebas del port nativo, T4). Escribe lo que contesta RelojPantalla (Codigo/RelojPantallaGpu.cs) de
// un rocola-xp.dll: Rejilla sin compositor de pruebas (lo de la rocola: en Linux, y en Windows XP en el A4, no hay compositor), Rejilla con
// uno de mentira (RelojPantalla.Prueba: varias frecuencias y fases), y la cuenta sola (EnMs, Tras, Despues, MomentoVista y Siguiente) en
// una rejilla de valores. Dos programas con la misma logica escriben el mismo archivo (diff).
//   reproductor-cintas --reloj DIR SALIDA
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace PruebasTarjeta
{
    internal static class Reloj
    {
        const BindingFlags Todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static string F(object v) { return ((double)v).ToString("R", CultureInfo.InvariantCulture); }

        internal static int Escribe(string dir, string salida)
        {
            Environment.SetEnvironmentVariable("ROCOLA_PANTALLA", "fuera:1024x768");
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
            {
                string p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            Assembly asm = Assembly.LoadFrom(Path.Combine(dir, "rocola-xp.dll"));
            Type r = asm.GetType("Rocola.RelojPantalla");
            long inicio = (long)asm.GetType("Rocola.GpuPanel").GetField("InicioRelojTicks", Todo).GetValue(null);
            long frec = Stopwatch.Frequency;
            MethodInfo rejilla = r.GetMethod("Rejilla", Todo), enMs = r.GetMethod("EnMs", Todo), tras = r.GetMethod("Tras", Todo),
                despues = r.GetMethod("Despues", Todo), momento = r.GetMethod("MomentoVista", Todo), siguiente = r.GetMethod("Siguiente", Todo);
            FieldInfo prueba = r.GetField("Prueba", Todo);
            StringBuilder sb = new StringBuilder();
            sb.Append("# reloj de la pantalla, frecuencia del reloj fino ").Append(frec).Append('\n');

            // Sin compositor de pruebas, varias veces (XP volvia a preguntar cada 250 ms mientras el compositor no contestara)
            foreach (double ahora in new double[] { 0, 10, 260, 600, 5000, 5300 })
            {
                object[] a = { ahora, 0.0, 0.0 };
                bool ok = (bool)rejilla.Invoke(null, a);
                sb.Append("rejilla sin compositor ahora=").Append(F(ahora)).Append(" -> ").Append(ok).Append(' ').Append(F(a[1])).Append(' ').Append(F(a[2])).Append('\n');
            }
            // Con uno de mentira: el periodo y un refresco (relativo al origen del reloj de la rocola, para que no dependa de la corrida)
            foreach (double hz in new double[] { 60, 59.94, 75, 144, 10, 600 })
                foreach (double fase in new double[] { 0, 3.3, -7.1, 250.25 })
                {
                    long per = (long)Math.Round(frec / hz), refr = inicio + (long)Math.Round(fase * frec / 1000.0);
                    prueba.SetValue(null, (Func<long[]>)(() => new long[] { per, refr }));
                    object[] a = { 1234.5, 0.0, 0.0 };
                    bool ok = (bool)rejilla.Invoke(null, a);
                    sb.Append("rejilla de mentira hz=").Append(F(hz)).Append(" fase=").Append(F(fase)).Append(" -> ").Append(ok).Append(' ').Append(F(a[1])).Append(' ').Append(F(a[2])).Append('\n');
                }
            prueba.SetValue(null, null);
            {
                object[] a = { 77.0, 0.0, 0.0 };
                bool ok = (bool)rejilla.Invoke(null, a);
                sb.Append("rejilla sin compositor otra vez -> ").Append(ok).Append(' ').Append(F(a[1])).Append(' ').Append(F(a[2])).Append('\n');
            }
            // La cuenta sola
            foreach (long periodo in new long[] { 0, -5, frec / 60, frec / 19, frec / 501, frec / 144 })
                foreach (long refresco in new long[] { 0, 12345, -98765 })
                {
                    object[] a = { 100.25, 5000L, periodo, refresco, frec, 0.0, 0.0 };
                    bool ok = (bool)enMs.Invoke(null, a);
                    sb.Append("enms periodo=").Append(periodo).Append(" refresco=").Append(refresco).Append(" -> ").Append(ok).Append(' ').Append(F(a[5])).Append(' ').Append(F(a[6])).Append('\n');
                }
            foreach (double p in new double[] { 16.6667, 13.3333, 6.9444, 33.3, 100 })
            {
                sb.Append("tras ").Append(F(p)).Append(" -> ").Append(F(tras.Invoke(null, new object[] { p }))).Append('\n');
                foreach (double t in new double[] { 0, 5.5, 16.6667, 1000.123, -3 })
                {
                    sb.Append("despues t=").Append(F(t)).Append(" p=").Append(F(p)).Append(" -> ").Append(F(despues.Invoke(null, new object[] { t, 2.5, p }))).Append('\n');
                    foreach (double previsto in new double[] { -1, 0, 3, 1000 })
                        sb.Append("momento ahora=").Append(F(t)).Append(" previsto=").Append(F(previsto)).Append(" p=").Append(F(p)).Append(" -> ").Append(F(momento.Invoke(null, new object[] { t, previsto, p }))).Append('\n');
                    foreach (int tope in new int[] { 1, 15, 16, 17, 22, 33, 45, 50, 66 })
                        sb.Append("siguiente ahora=").Append(F(t)).Append(" tope=").Append(tope).Append(" p=").Append(F(p)).Append(" -> ").Append(F(siguiente.Invoke(null, new object[] { t, tope, 2.5, p }))).Append('\n');
                }
            }
            File.WriteAllText(salida, sb.ToString());
            Console.WriteLine("reloj de " + asm.Location + ": " + sb.ToString().Split('\n').Length + " lineas en " + salida);
            return 0;
        }
    }
}
