using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

// La metadata de genero no altera RIAD/RIAX ni atribuye rutas al corpus. Solo carpeta temporal.
class IaGeneros
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static int errores, comprobaciones;
    static void Check(bool ok, string texto) { comprobaciones++; if (!ok) { errores++; Console.Error.WriteLine("FALLO: " + texto); } }
    static int Main(string[] args)
    {
        string carpeta = Path.Combine(Path.GetTempPath(), "rocola-generos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        try
        {
            Type t = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
            t.GetField("carpetaDatos", F).SetValue(null, carpeta);
            object form = FormatterServices.GetUninitializedObject(t);
            t.GetField("iaFileLock", F).SetValue(form, new object());
            MethodInfo origenDe = t.GetMethod("IaOrigenDe", F), append = t.GetMethod("IaAppendPairsOrigen", F);
            object origen = origenDe.Invoke(null, new object[] { Path.Combine(carpeta, "01 Tema.mp3"), 120.0, 44100, 2048, 1 });
            Type o = origen.GetType();
            o.GetField("Genero", F).SetValue(origen, " ROCK\tALTERNATIVO\r\n ");
            List<float> pares = new List<float>(); for (int i = 0; i < 200; i++) pares.Add(.01f * i);
            Check((bool)append.Invoke(form, new object[] { pares, origen }), "guardar pares y origen");
            string datos = Path.Combine(carpeta, "rocola_ia_datos.bin"), idx = Path.Combine(carpeta, "rocola_ia_datos.idx");
            string generos = Path.Combine(carpeta, "rocola_ia_datos.generos.tsv");
            Check(new FileInfo(datos).Length == 16 + 5 * 160 && new FileInfo(idx).Length == 16 + 64,
                "contratos binarios y presupuesto inalterados");
            ulong cancion = (ulong)o.GetField("Cancion", F).GetValue(origen);
            string[] lineas = File.ReadAllLines(generos, Encoding.UTF8);
            string esperada = cancion.ToString("x16", CultureInfo.InvariantCulture) + "\trock alternativo";
            Check(Array.IndexOf(lineas, esperada) >= 0, "huella hexadecimal y genero fiable normalizado");
            Check(File.ReadAllText(generos).IndexOf(carpeta, StringComparison.OrdinalIgnoreCase) < 0,
                "no publicar rutas del usuario en metadata");
            byte[] entrada = File.ReadAllBytes(idx);
            Check(BitConverter.ToInt32(entrada, 16 + 24) == 1, "canal derecho conserva origen y grupo cancion");
            o.GetField("Genero", F).SetValue(origen, "unknown");
            Check((bool)append.Invoke(form, new object[] { pares, origen }), "anexar genero desconocido sin inventar");
            Check(File.ReadAllText(generos).EndsWith(cancion.ToString("x16", CultureInfo.InvariantCulture) + "\t\n", StringComparison.Ordinal),
                "desconocido representado explicitamente vacio");
            File.Delete(datos);   // simular otro corpus, solo el archivo creado por esta prueba
            o.GetField("Genero", F).SetValue(origen, "Pop");
            Check((bool)append.Invoke(form, new object[] { pares, origen }), "nuevo corpus empieza limpio");
            Check(File.Exists(generos + ".anterior") && File.Exists(idx + ".anterior"), "metadata e indice anteriores recuperables");
            Check(File.ReadAllText(generos).EndsWith("\tpop\n", StringComparison.Ordinal), "sidecar actual describe corpus nuevo");
            Console.WriteLine("Generos IA: " + comprobaciones + " comprobaciones, " + errores + " fallos");
            return errores == 0 ? 0 : 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.Delete(carpeta, true); }
    }
}
