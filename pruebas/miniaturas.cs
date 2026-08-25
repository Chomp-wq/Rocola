using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Reflection;

// LAS MINIATURAS DE LAS PORTADAS TIENEN QUE SOBREVIVIR AL APAGON.
//
// La pared de discos se dibuja con la portada de cada uno. Decodificar el JPG grande de una portada es
// CARO, asi que la primera vez se guarda una miniatura ya escalada y a partir de ahi se lee esa. Medido
// aqui abajo: la primera vez cuesta unas diez veces mas que las siguientes.
//
// El problema era DONDE se guardaban: en la carpeta temporal del sistema. Y en la rocola del bar, que
// corre Puppy Linux, esa carpeta ESTA EN LA MEMORIA: se borra al apagar la maquina. O sea que la promesa
// del codigo -"la primera vez tarda; luego todo vuela"- alli era "la primera vez TODOS LOS DIAS".
//
// Peor: el precalentador que va construyendo las que faltan duerme entre portada y portada y se para en
// cuanto alguien toca un boton. Con unos cientos de discos tarda minutos, y si cada noche empieza de cero
// puede no terminar nunca. Ahora van con el resto de lo que la rocola guarda, en la carpeta de datos, que
// esta en un disco de verdad. Windows tambien limpia su carpeta temporal cada tanto, asi que alli tambien
// se gana.
//
// Y como ya nadie las borra, hay que barrer: pero por EDAD, no por "esto ya no esta en la biblioteca".
// Desconectar un disco de musica un fin de semana no puede costar reconstruir todas sus portadas.
class Miniaturas
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // Una portada de mentira pero del peso de una de verdad: ruido, que es lo que NO se comprime. Una
    // imagen de un solo color pesaria cuatro bytes y decodificarla no costaria nada, con lo que la medida
    // de aqui abajo no diria absolutamente nada.
    static void HazPortada(string ruta, int semilla)
    {
        Random r = new Random(semilla);
        using (Bitmap b = new Bitmap(500, 500, PixelFormat.Format24bppRgb))
        {
            for (int y = 0; y < 500; y++)
                for (int x = 0; x < 500; x++)
                    b.SetPixel(x, y, Color.FromArgb(r.Next(256), r.Next(256), r.Next(256)));
            b.Save(ruta, ImageFormat.Jpeg);
        }
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);

        FieldInfo fCarpeta = TF.GetField("carpetaDatos", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo fThumbDir = TF.GetField("thumbDir", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mRutaMini = TF.GetMethod("ThumbCachePath", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo mDecode = TF.GetMethod("DecodeCover", BindingFlags.Static | BindingFlags.NonPublic);
        if (fCarpeta == null || fThumbDir == null || mRutaMini == null || mDecode == null)
        { Console.WriteLine("\n  FALLO  no encuentro las piezas de las portadas en el ejecutable\n"); return 1; }

        string donde = Path.Combine(Path.GetTempPath(), "rocola_mini_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(donde);
        fCarpeta.SetValue(null, null);
        fThumbDir.SetValue(null, null);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", donde);

        try
        {
            Console.WriteLine("\n  1) DONDE SE GUARDAN  (esto es todo el asunto)\n");
            string ruta = (string)mRutaMini.Invoke(null, new object[] { Path.Combine(donde, "Disco 1", "cover.jpg") });
            Console.WriteLine("     la miniatura de una portada iria a:");
            Console.WriteLine("       " + ruta);
            Check(ruta != null && ruta.StartsWith(donde, StringComparison.Ordinal),
                  "las miniaturas van a la carpeta de datos (un disco de verdad), no a la temporal");
            // La carpeta temporal es justo lo que Puppy tiene en la memoria y borra al apagar.
            string temporal = Path.GetTempPath().TrimEnd('/', '\\');
            bool enLaTemporal = ruta != null && Path.GetFullPath(ruta).StartsWith(temporal + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                                && !ruta.StartsWith(donde, StringComparison.Ordinal);
            Check(!enLaTemporal, "y NO cuelgan de la carpeta temporal del sistema, que al apagar se pierde");

            Console.WriteLine("\n  2) Y PARA QUE SIRVE QUE SOBREVIVAN\n");
            string portada = Path.Combine(donde, "portada.jpg");
            HazPortada(portada, 12345);
            Console.WriteLine("     una portada de 500x500 pesa " + (new FileInfo(portada).Length / 1024) + " KB");

            Stopwatch sw = Stopwatch.StartNew();
            Image a = (Image)mDecode.Invoke(null, new object[] { portada });
            sw.Stop(); double primera = sw.Elapsed.TotalMilliseconds;
            if (a != null) a.Dispose();

            sw = Stopwatch.StartNew();
            Image b = (Image)mDecode.Invoke(null, new object[] { portada });
            sw.Stop(); double segunda = sw.Elapsed.TotalMilliseconds;
            if (b != null) b.Dispose();

            Console.WriteLine("     la 1a vez (decodificar el JPG y guardar la miniatura): " + primera.ToString("0.00") + " ms");
            Console.WriteLine("     con la miniatura ya hecha:                             " + segunda.ToString("0.00") + " ms");
            Console.WriteLine("     una pagina son 18 discos:  1a vez " + (primera * 18).ToString("0") + " ms   /   ya hecha " + (segunda * 18).ToString("0") + " ms");
            Check(segunda * 2 < primera, "tener la miniatura hecha ahorra mas de la mitad del trabajo");
            Check(File.Exists((string)mRutaMini.Invoke(null, new object[] { portada })),
                  "y la miniatura queda escrita donde toca");

            Console.WriteLine("\n  3) EL BARRIDO NO SE LLEVA POR DELANTE UN DISCO DESCONECTADO\n");
            // Pediste poder agregar y quitar discos de musica. Un barrido que borrara "lo que hoy no esta
            // en la biblioteca" castigaria desconectar un disco un fin de semana con reconstruir todas sus
            // portadas. Asi que se barre por EDAD.
            MethodInfo mBarre = TF.GetMethod("BarreMiniaturas", BindingFlags.Static | BindingFlags.NonPublic);
            Check(mBarre != null, "la rocola sabe barrer sus miniaturas viejas");
            if (mBarre != null)
            {
                string dirMini = (string)fThumbDir.GetValue(null);
                string vieja = Path.Combine(dirMini, "0000000000000001_256.jpg");
                string nueva = Path.Combine(dirMini, "0000000000000002_256.jpg");
                File.WriteAllBytes(vieja, new byte[4096]);
                File.WriteAllBytes(nueva, new byte[4096]);
                File.SetLastWriteTimeUtc(vieja, DateTime.UtcNow.AddDays(-400));

                // Mientras la carpeta sea pequena no se toca NADA, ni lo antiguo.
                mBarre.Invoke(null, null);
                Check(File.Exists(vieja) && File.Exists(nueva),
                      "con la carpeta pequena no borra nada (aunque haya miniaturas de hace un ano)");

                // Y pasada de tamano, se lleva lo antiguo y respeta lo reciente.
                FieldInfo fTope = TF.GetField("MiniaturasTopeMb", BindingFlags.Static | BindingFlags.NonPublic);
                int tope = (fTope != null) ? (int)fTope.GetRawConstantValue() : -1;
                Console.WriteLine("     se barre solo si la carpeta pasa de " + tope + " MB");
                Check(tope >= 100, "y el tope es holgado: con 23 KB por portada, miles de discos caben sin barrer nunca");
                if (tope > 0)
                {
                    string gordo = Path.Combine(dirMini, "0000000000000003_256.jpg");
                    using (FileStream fs = new FileStream(gordo, FileMode.Create))
                    { fs.SetLength((long)tope * 1024L * 1024L + 1024L); }
                    File.SetLastWriteTimeUtc(gordo, DateTime.UtcNow);
                    mBarre.Invoke(null, null);
                    Check(!File.Exists(vieja), "pasada de tamano, se lleva la miniatura de hace mas de un ano");
                    Check(File.Exists(nueva), "y respeta la reciente, este o no su disco conectado hoy");
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", null);
            fCarpeta.SetValue(null, null);
            fThumbDir.SetValue(null, null);
            try { Directory.Delete(donde, true); } catch { }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}
