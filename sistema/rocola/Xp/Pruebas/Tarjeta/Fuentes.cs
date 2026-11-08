// LA PRUEBA DE LOS TEXTOS DE LOS PROGRAMAS (pruebas del port nativo). Escribe los textos de los programas de sombreado de un rocola-xp.dll:
//   - de la traduccion nativa: las constantes de SombreadoresTarjeta (el de vertices y los tres de fragmentos);
//   - de la referencia congelada (con capas): lo que su FuenteEtapas (Direct3DaGL.cs) genera para las tres combinaciones de etapas que
//     usa XP (TssDiffuse, TssModulate y la pasada de dos texturas de PerformanceGpu.cs), con los estados que XP pone, y su FuenteVertices.
// Los dos archivos tienen que ser iguales (diff).
//   reproductor-cintas --fuentes DIR SALIDA
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PruebasTarjeta
{
    internal static class Fuentes
    {
        const BindingFlags Todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static int Escribe(string dir, string salida)
        {
            Environment.SetEnvironmentVariable("ROCOLA_PANTALLA", "fuera:1024x768");
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object s, ResolveEventArgs e)
            {
                string p = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            Assembly asm = Assembly.LoadFrom(Path.Combine(dir, "rocola-xp.dll"));
            StringBuilder sb = new StringBuilder();
            Type nat = asm.GetType("Rocola.SombreadoresTarjeta");
            if (nat != null)
            {
                sb.Append("== vertices ==\n").Append((string)nat.GetField("FuenteVertices", Todo).GetValue(null));
                sb.Append("== color ==\n").Append((string)nat.GetField("FuenteColor", Todo).GetValue(null));
                sb.Append("== textura por color ==\n").Append((string)nat.GetField("FuenteTexturaPorColor", Todo).GetValue(null));
                sb.Append("== dos texturas ==\n").Append((string)nat.GetField("FuenteDosTexturas", Todo).GetValue(null));
                Console.WriteLine("SombreadoresTarjeta de " + asm.Location);
            }
            else
            {
                Type engl = asm.GetType("Rocola.D3D9+EnGl"), tDisp = asm.GetType("Rocola.D3D9+DispositivoGl");
                MethodInfo fuente = engl.GetMethod("FuenteEtapas", Todo);
                sb.Append("== vertices ==\n").Append((string)engl.GetField("FuenteVertices", Todo).GetValue(null));
                // TssDiffuse (D3D9Star.cs): COLOROP SELECTARG1, COLORARG1 DIFFUSE, ALPHAOP SELECTARG1, ALPHAARG1 DIFFUSE; sin textura
                sb.Append("== color ==\n").Append(Genera(fuente, tDisp, new uint[] { 2, 0, 0, 2, 0, 0 }, IntPtr.Zero, null, IntPtr.Zero));
                // TssModulate: MODULATE(TEXTURE, DIFFUSE) en color y alfa; con su textura
                sb.Append("== textura por color ==\n").Append(Genera(fuente, tDisp, new uint[] { 4, 2, 0, 4, 2, 0 }, new IntPtr(1), null, IntPtr.Zero));
                // DibujaDosTexturas (PerformanceGpu.cs): etapa 0 MODULATE(TEXTURE, TEXTURE|ALPHAREPLICATE), SELECTARG1(TEXTURE); etapa 1
                // MODULATE(TEXTURE|ALPHAREPLICATE|COMPLEMENT, CURRENT), ADDSMOOTH(TEXTURE, CURRENT), TEXCOORDINDEX 1
                sb.Append("== dos texturas ==\n").Append(Genera(fuente, tDisp, new uint[] { 4, 2, 0x22, 2, 2, 0 }, new IntPtr(1), new uint[] { 4, 0x32, 1, 11, 2, 1 }, new IntPtr(2)));
                Console.WriteLine("FuenteEtapas (Direct3DaGL) de " + asm.Location);
            }
            File.WriteAllText(salida, sb.ToString());
            return 0;
        }
        static string Genera(MethodInfo fuente, Type tDisp, uint[] e0, IntPtr t0, uint[] e1, IntPtr t1)
        {
            object d = Activator.CreateInstance(tDisp, true);
            Array etapas = (Array)tDisp.GetField("Etapas", Todo).GetValue(d);
            Pon(etapas, 0, e0, t0, 0);
            if (e1 != null) Pon(etapas, 1, e1, t1, 1);
            object[] a = { d, new bool[2], false, false, false };
            return (string)fuente.Invoke(null, a);
        }
        static void Pon(Array etapas, int i, uint[] v, IntPtr t, uint coordenadas)
        {
            object e = etapas.GetValue(i);
            Type te = e.GetType();
            string[] campos = { "ColorOp", "ColorArg1", "ColorArg2", "AlphaOp", "AlphaArg1", "AlphaArg2" };
            for (int k = 0; k < 6; k++) te.GetField(campos[k], Todo).SetValue(e, v[k]);
            te.GetField("IndiceCoordenadas", Todo).SetValue(e, coordenadas);
            te.GetField("Textura", Todo).SetValue(e, t);
            etapas.SetValue(e, i);
        }
    }
}
