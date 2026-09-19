using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

// MIRAR LA CACHE DE APOLLO NO PUEDE RECORRER EL DISCO EN CADA CANCION.
//
// Al empezar una cancion, IaEnhancedPath pregunta si Apollo ya dejo lista una version mejorada. Para
// saber DONDE esta esa cache llamaba a IaCache(), y ese, cuando la carpeta no esta configurada -que es
// lo normal-, buscaba el motor SUBIENDO HASTA SEIS CARPETAS y preguntando por dos archivos en cada una:
// doce preguntas al disco, en cada cancion, a la hora en que ese disco esta ocupado leyendo por delante
// la siguiente y repasando la biblioteca.
//
// Ahora se recuerda. Y lo que esta bateria vigila no es el ahorro -que es de microsegundos aqui- sino
// las dos cosas que pueden salir mal:
//
//   1. QUE EL RECUERDO CONTESTE POR OTRA PREGUNTA. Si se pide la cache de una carpeta y contesta la de
//      otra, la rocola buscaria las canciones mejoradas donde no estan.
//   2. QUE EL RECUERDO SE VUELVA ETERNO. El motor se puede instalar con la rocola ya encendida, y tiene
//      que aparecer solo, sin reiniciar. Por eso el recuerdo caduca.
//
// Y una que se decidio NO hacer, y queda escrita aqui: la huella del modelo se sigue leyendo del disco
// cada vez. Cuesta 0,016 ms y es la que entra en el nombre de los archivos de cache; dejarla fresca
// ahorra tener que razonar sobre si una huella vieja puede hacer sonar el archivo equivocado.
class Apollo
{
    const BindingFlags EST = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags UNO = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type TF = a.GetType("Rocola.Form1", true);
        MethodInfo cacheFor = TF.GetMethod("IaCacheFor", EST);
        if (cacheFor == null)
        {
            // Esta rocola no lleva Apollo (es solo de la potente): no hay nada que vigilar.
            Bien("esta rocola no lleva Apollo: nada que vigilar aqui");
            return 0;
        }
        MethodInfo calcula = TF.GetMethod("IaCacheCalcula", EST);
        FieldInfo fCuando = TF.GetField("iaCacheCuando", EST);
        FieldInfo fRecordada = TF.GetField("iaCacheRecordada", EST);
        FieldInfo fMs = TF.GetField("IaCacheRecuerdoMs", EST);
        if (calcula == null || fCuando == null || fRecordada == null)
        { Mal("no esta el recuerdo de donde vive la cache (IaCacheCalcula / iaCacheRecordada)"); return 1; }

        string dir = Path.Combine(Path.GetTempPath(), "rocola-apollo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string uno = Path.Combine(dir, "uno"), dos = Path.Combine(dir, "dos");
        Directory.CreateDirectory(uno); Directory.CreateDirectory(dos);
        try
        {
            Console.WriteLine("\n  1) EL RECUERDO CONTESTA A LA PREGUNTA QUE LE HACEN\n");
            string a1 = (string)cacheFor.Invoke(null, new object[] { uno });
            string a2 = (string)cacheFor.Invoke(null, new object[] { uno });
            Exige(a1 == a2, "preguntar dos veces por la misma carpeta da la misma respuesta");
            string b1 = (string)cacheFor.Invoke(null, new object[] { dos });
            Exige(b1 != a1, "y preguntar por OTRA carpeta NO devuelve la de antes");
            Exige(b1 != null && b1.IndexOf("dos", StringComparison.OrdinalIgnoreCase) >= 0,
                  "la respuesta es la de la carpeta que se pidio");
            string c1 = (string)cacheFor.Invoke(null, new object[] { uno });
            Exige(c1 == a1, "y volviendo a la primera, vuelve su respuesta");

            // Y que el recuerdo no invente: tiene que decir lo MISMO que calcularlo a mano.
            string crudo = (string)calcula.Invoke(null, new object[] { uno });
            Exige(crudo == a1, "lo recordado es exactamente lo que sale de calcularlo");

            Console.WriteLine("\n  2) Y CADUCA, PARA QUE UN APOLLO RECIEN INSTALADO APAREZCA SOLO\n");
            int ms = (fMs != null) ? Convert.ToInt32(fMs.GetRawConstantValue()) : -1;
            Exige(ms > 0 && ms <= 30000,
                  "el recuerdo dura " + ms + " ms: poco, para que instalar el motor no pida reiniciar");
            // Se envejece el recuerdo a mano y se comprueba que de verdad vuelve a mirar.
            fCuando.SetValue(null, unchecked(Environment.TickCount - (ms + 1000)));
            fRecordada.SetValue(null, "respuesta-invalida-de-prueba");
            string d1 = (string)cacheFor.Invoke(null, new object[] { uno });
            Exige(d1 == a1, "pasado ese rato vuelve a mirar de verdad, no repite lo guardado");

            Console.WriteLine("\n  3) LA HUELLA DEL MODELO SE SIGUE LEYENDO FRESCA\n");
            MethodInfo huella = TF.GetMethod("IaPipelineFingerprint", UNO);
            MethodInfo leeHuella = TF.GetMethod("ReadIaPipelineFingerprint", EST);
            Exige(leeHuella != null && Codigo.Llama(leeHuella, "ReadAllText"),
                  "la huella se lee del archivo, no de un rastro de antes");
            // Y SE LEE CADA VEZ, comprobado cambiandola: preguntar solo si la llamada existe no vale,
            // porque guardarse la respuesta tambien la llama -una vez- y luego repite. Se cambia el
            // archivo por debajo y se exige que lo diga.
            if (huella == null) Mal("no esta IaPipelineFingerprint");
            else
            {
                object f = Codigo.FormSinEncender(TF);
                FieldInfo fDir = TF.GetField("iaCacheDir", UNO);
                if (fDir == null) Mal("no esta iaCacheDir");
                else
                {
                    fDir.SetValue(f, dir);
                    string aa = new string('a', 64), bb = new string('b', 64);
                    File.WriteAllText(Path.Combine(dir, "modelo.fingerprint"), aa);
                    string h1 = (string)huella.Invoke(f, null);
                    File.WriteAllText(Path.Combine(dir, "modelo.fingerprint"), bb);
                    string h2 = (string)huella.Invoke(f, null);
                    Exige(h1 == aa, "la huella que se lee es la que hay en el archivo");
                    Exige(h2 == bb, "y si el modelo cambia, se entera: se lee cada vez, no se recuerda");
                }
            }

            Console.WriteLine("\n  4) LO QUE CUESTA MIRAR LA CACHE\n");
            MethodInfo busca = TF.GetMethod("IaFindEngine", EST);
            if (busca != null)
            {
                object[] ar = new object[] { null, null };
                Console.WriteLine("     buscar el motor a pelo (6 carpetas)   " + Corre(200, delegate { busca.Invoke(null, ar); }).ToString("0.000") + " ms");
            }
            Console.WriteLine("     saber donde vive la cache (recordado) " + Corre(500, delegate { cacheFor.Invoke(null, new object[] { (string)null }); }).ToString("0.000") + " ms");
            Console.WriteLine("     leer la huella del modelo             " + Corre(200, delegate { leeHuella.Invoke(null, new object[] { dir }); }).ToString("0.000") + " ms");
            Bien("y los numeros salen en cada pasada, para poder compararlos el dia que alguien toque esto");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    static double Corre(int veces, Action a)
    {
        a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }
}
