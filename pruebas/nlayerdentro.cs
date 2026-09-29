using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;
// NLAYER DENTRO DE Rocola.exe (FlotanteNLayer.cs, NLayerIncluido; solo esta rama).
//
// Antes habia que copiar NLayer.dll junto a Rocola.exe al desplegar; si se olvidaba, los MP3 que llegan al maximo
// volvian a recortarse. Ahora va dentro del exe. Aqui se mira, con la rocola de verdad:
//   1) el exe lleva dentro la MISMA NLayer.dll de packages/, y el proyecto de Visual Studio tambien la mete;
//   2) en una carpeta con solo Rocola.exe y NAudio.dll (sin NLayer.dll), el descompresor de NLayer se arma igual;
//      y sin el enganche, en esa misma carpeta, no (asi se sabe que la carpeta de verdad no la tiene);
//   3) Main lo engancha antes que nada.
class NLayerDentro
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    // El hijo: en una carpeta sin NLayer.dll, arma el descompresor de NLayer de la rocola.
    static int Hijo(string exe, bool engancha)
    {
        Assembly roc = Assembly.LoadFrom(exe);
        if (engancha) roc.GetType("Rocola.NLayerIncluido", true).GetMethod("Engancha", T).Invoke(null, null);
        try
        {
            Activator.CreateInstance(roc.GetType("Rocola.MotorNLayer", true), new object[] { new WaveFormat(44100, 16, 2) });
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == "NLayer")
                {
                    string donde = "";
                    try { donde = a.Location; } catch { }
                    Console.WriteLine("CARGADA " + a.GetName().Version + " [" + donde + "]");
                }
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FALLA " + (e.InnerException ?? e).GetType().Name); return 1; }
    }

    static int Corre(string dir, string modo, out string salida)
    {
        string yo = Path.Combine(dir, "nlayerdentro.exe");
        bool mono = Type.GetType("Mono.Runtime") != null;
        ProcessStartInfo p = new ProcessStartInfo(mono ? "mono" : yo, (mono ? "\"" + yo + "\" " : "") + "hijo Rocola.exe " + modo);
        p.WorkingDirectory = dir; p.UseShellExecute = false; p.RedirectStandardOutput = true; p.RedirectStandardError = true;
        using (Process h = Process.Start(p))
        {
            salida = (h.StandardOutput.ReadToEnd() + h.StandardError.ReadToEnd()).Trim();
            h.WaitForExit();
            return h.ExitCode;
        }
    }

    static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "hijo") return Hijo(args[1], args[2] == "engancha");
        string exe = args.Length > 0 ? args[0] : "rocola.exe";
        Assembly roc = Assembly.LoadFrom(exe);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_nlayer_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Console.WriteLine("\n  1) VA DENTRO DEL EXE\n");
            byte[] dentro = null;
            using (Stream s = roc.GetManifestResourceStream("Rocola.NLayer.dll"))
                if (s != null) { dentro = new byte[s.Length]; int n = 0, r; while (n < dentro.Length && (r = s.Read(dentro, n, dentro.Length - n)) > 0) n += r; }
            byte[] original = File.ReadAllBytes("NLayer.dll");   // la de packages/, que todo.sh copia aqui
            bool igual = dentro != null && dentro.Length == original.Length;
            for (int i = 0; igual && i < original.Length; i++) if (dentro[i] != original[i]) igual = false;
            Check(igual, "Rocola.exe lleva dentro la misma NLayer.dll de packages/ (" + (dentro != null ? dentro.Length + " bytes" : "no esta") + ")");
            string proy = File.Exists(Path.Combine("..", Path.Combine("Rocola", "Rocola.csproj"))) ? File.ReadAllText(Path.Combine("..", Path.Combine("Rocola", "Rocola.csproj"))) : "";
            Check(proy.Contains("<LogicalName>Rocola.NLayer.dll</LogicalName>") && proy.Contains("<Private>False</Private>"),
                  "y el proyecto de Visual Studio tambien la mete dentro (y ya no la copia al lado)");

            Console.WriteLine("\n  2) EN UNA CARPETA SIN NLayer.dll\n");
            Directory.CreateDirectory(dir);
            File.Copy(exe, Path.Combine(dir, "Rocola.exe"));
            File.Copy("NAudio.dll", Path.Combine(dir, "NAudio.dll"));
            File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(dir, "nlayerdentro.exe"));
            Check(!File.Exists(Path.Combine(dir, "NLayer.dll")), "la carpeta tiene solo Rocola.exe y NAudio.dll");
            string con, sin;
            int rc = Corre(dir, "engancha", out con);
            Check(rc == 0 && con.StartsWith("CARGADA 1.12"), "el descompresor de NLayer se arma igual, con la de dentro (" + con + ")");
            int rs = Corre(dir, "sin", out sin);
            Check(rs != 0 && sin.StartsWith("FALLA"), "sin el enganche, en esa carpeta no se puede (" + sin + "): la de dentro es la que sirve");

            Console.WriteLine("\n  3) MAIN LO ENGANCHA ANTES QUE NADA\n");
            List<string> pasos = Codigo.Nombres(roc.GetType("Rocola.Program", true).GetMethod("Main", T));
            int iE = pasos.IndexOf("Engancha"), iC = pasos.IndexOf("get_CarpetaDatos");
            Check(iE >= 0 && iC > iE, "Main engancha NLayer antes de todo lo demas");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
