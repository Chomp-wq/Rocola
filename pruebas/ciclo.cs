using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

// EL CIRCUITO ENTERO DE LA IA DEL EQ, DE PUNTA A PUNTA.
//
// La rocola aprende de tu biblioteca en tres pasos, y hasta ahora cada uno se probaba por su lado:
//
//   1. LA ROCOLA JUNTA el material: mide la huella tonal de cada cancion y la apunta en rocola_eq.txt
//   2. SE ENTRENA un modelo con ese archivo             -> rocola_eq_ia.bin
//   3. LA ROCOLA LO CARGA y deja de ir a ciegas
//
// El paso 3 no lo probaba NADIE, y es el que decide si todo lo demas sirvio de algo: sin modelo, el EQ
// automatico usa "el promedio de TODAS las canciones" y ademas se guarda un 12% por prudencia, porque
// no sabe de que biblioteca esta hablando. Con modelo, apunta a lo que esa cancion necesita.
//
// Aqui se recorren los tres seguidos con material de mentira pero DEL FORMATO DE VERDAD, y se exige
// que al final la rocola diga que si, que tiene modelo. Ademas es lo unico que prueba que el guion de
// entrenar de Linux -ia/entrenar.sh- de verdad deja el archivo donde la rocola lo va a buscar: en
// Windows eso lo hacia un .bat que aqui no sirve.
class Ciclo
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // Una biblioteca de mentira, pero con la forma que tiene una de verdad: cada cancion con su huella
    // tonal de 8 bandas, y una relacion aprendible entre el cuerpo de la cancion y sus extremos -que es
    // justo lo que el modelo tiene que descubrir-.
    static string MaterialDeMentira(int cuantas)
    {
        StringBuilder sb = new StringBuilder();
        Random r = new Random(20250825);
        for (int i = 0; i < cuantas; i++)
        {
            double[] b = new double[8];
            double cuerpo = -6 + r.NextDouble() * 12;
            for (int k = 1; k <= 5; k++) b[k] = cuerpo + (r.NextDouble() - 0.5) * 3;
            // Los extremos SIGUEN al cuerpo con una regla fija mas un poco de ruido: hay algo que aprender.
            b[0] = cuerpo * 0.8 - 2 + (r.NextDouble() - 0.5) * 1.2;
            b[6] = cuerpo * 0.5 + 1 + (r.NextDouble() - 0.5) * 1.2;
            b[7] = cuerpo * 0.3 + 3 + (r.NextDouble() - 0.5) * 1.2;
            // El formato de verdad: las ocho bandas separadas por ";", detras el corte de agudos, y
            // detras de la barra la ruta. Si esto no coincide con lo que escribe la rocola, el entrenador
            // no entiende ni una linea -y eso ya paso una vez-.
            for (int k = 0; k < 8; k++) { if (k > 0) sb.Append(';'); sb.Append(b[k].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)); }
            sb.Append(';').Append("18000");                       // corte (Hz): cancion sana
            sb.Append('|').Append("/mnt/home/rocola/musica/disco/cancion").Append(i).Append(".mp3");
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    static int Main(string[] argv)
    {
        string exe = Path.GetFullPath((argv.Length > 0) ? argv[0] : "rocola.exe");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_ciclo");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);

        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        FieldInfo fNet = TF.GetField("EqNet", BindingFlags.Static | BindingFlags.NonPublic);
        Check(fNet != null, "la rocola tiene su IA del EQ donde siempre");
        if (fNet == null) { Console.WriteLine("\n  1 FALLO(S)\n"); return 1; }
        object net = fNet.GetValue(null);
        PropertyInfo pLoaded = net.GetType().GetProperty("Loaded");
        FieldInfo fLoaded = net.GetType().GetField("Loaded");
        MethodInfo mTry = net.GetType().GetMethod("TryLoad");

        Console.WriteLine("\n  1) DE ENTRADA, LA ROCOLA VA A CIEGAS  (si no, esto no probaria nada)\n");
        bool cargadoAntes = (fLoaded != null) ? (bool)fLoaded.GetValue(net) : (bool)pLoaded.GetValue(net, null);
        Check(!cargadoAntes, "sin modelo entrenado, la IA del EQ dice que no lo tiene");

        Console.WriteLine("\n  2) SE JUNTA MATERIAL Y SE ENTRENA  (el guion de Linux, no el .bat de Windows)\n");
        string eq = Path.Combine(dir, "rocola_eq.txt");
        File.WriteAllText(eq, MaterialDeMentira(900));
        Console.WriteLine("     material: 900 canciones medidas en rocola_eq.txt");

        string guion = Path.GetFullPath(Path.Combine("..", Path.Combine("ia", "entrenar.sh")));
        Check(File.Exists(guion), "existe el guion de entrenar para Linux (ia/entrenar.sh)");
        if (!File.Exists(guion)) { Console.WriteLine("\n  " + fallos + " FALLO(S)\n"); return fallos; }

        ProcessStartInfo psi = new ProcessStartInfo("sh", "\"" + guion + "\" \"" + dir + "\"");
        psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
        Process p = Process.Start(psi);
        string salida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(600000);
        foreach (string l in salida.Replace("\r", "").Split('\n'))
            if (l.IndexOf("EQ AUTOMATICO", StringComparison.Ordinal) >= 0 || l.IndexOf("->", StringComparison.Ordinal) >= 0
                || l.IndexOf("NO SE PUDO", StringComparison.Ordinal) >= 0) Console.WriteLine("     " + l.Trim());

        string modelo = Path.Combine(dir, "rocola_eq_ia.bin");
        bool hay = File.Exists(modelo) && new FileInfo(modelo).Length > 0;
        Check(hay, "el guion dejo un modelo entrenado donde la rocola lo va a buscar");
        if (!hay)
        {
            Console.WriteLine("     lo que dijo el guion:");
            foreach (string l in salida.Replace("\r", "").Split('\n')) if (l.Trim().Length > 0) Console.WriteLine("       " + l);
            Console.WriteLine("\n  " + fallos + " FALLO(S)\n"); return fallos;
        }
        Console.WriteLine("     el modelo pesa " + new FileInfo(modelo).Length + " bytes");

        Console.WriteLine("\n  3) Y LA ROCOLA LO CARGA  (que es el paso que nadie probaba)\n");
        mTry.Invoke(net, new object[] { modelo });
        bool cargado = (fLoaded != null) ? (bool)fLoaded.GetValue(net) : (bool)pLoaded.GetValue(net, null);
        Check(cargado, "la rocola carga el modelo entrenado y deja de ir a ciegas");

        // Y que sirva de algo: que prediga numeros distintos para canciones distintas. Un modelo que
        // contestara siempre lo mismo estaria "cargado" y no valdria para nada.
        MethodInfo mPred = net.GetType().GetMethod("Predict");
        if (cargado && mPred != null)
        {
            float[] a1 = new float[5] { -5f, -4f, -5f, -4f, -5f }, s1 = new float[3];
            float[] a2 = new float[5] { 5f, 4f, 5f, 4f, 5f }, s2 = new float[3];
            bool ok1 = (bool)mPred.Invoke(net, new object[] { a1, s1 });
            bool ok2 = (bool)mPred.Invoke(net, new object[] { a2, s2 });
            double dif = 0;
            for (int i = 0; i < 3; i++) dif += Math.Abs(s1[i] - s2[i]);
            Console.WriteLine("     a una cancion apagada le dice (" + Tres(s1) + ") y a una brillante (" + Tres(s2) + ")");
            Check(ok1 && ok2 && dif > 0.5, "y contesta cosas DISTINTAS a canciones distintas (no un numero fijo)");
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static string Tres(float[] v) { return v[0].ToString("0.0") + ", " + v[1].ToString("0.0") + ", " + v[2].ToString("0.0"); }
}
