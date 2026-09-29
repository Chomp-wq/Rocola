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
// Desde el 29 de septiembre (decision del usuario, que sustituye a la de leerla fresca cada vez) tambien
// se recuerdan, diez segundos, la HUELLA DEL MODELO y la BUSQUEDA DE CADA CANCION: la pantalla pregunta
// por la misma cancion dos veces por segundo en sus ultimos segundos (al prepararla sin pausa), y cada
// pregunta era abrir y leer la huella, hasta cuatro consultas a la carpeta y marcar el archivo como usado.
// Las mismas dos vigilancias valen para ellas, mas una: la identidad de la cancion (ruta, tamano y fecha)
// se sigue mirando cada vez, asi que una cancion reemplazada nunca hereda lo recordado de la anterior.
class Apollo
{
    const BindingFlags EST = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags UNO = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }
    static Type TF;
    static int ms;

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
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
            ms = (fMs != null) ? Convert.ToInt32(fMs.GetRawConstantValue()) : -1;
            Exige(ms > 0 && ms <= 30000,
                  "el recuerdo dura " + ms + " ms: poco, para que instalar el motor no pida reiniciar");
            // Se envejece el recuerdo a mano y se comprueba que de verdad vuelve a mirar.
            fCuando.SetValue(null, unchecked(Environment.TickCount - (ms + 1000)));
            fRecordada.SetValue(null, "respuesta-invalida-de-prueba");
            string d1 = (string)cacheFor.Invoke(null, new object[] { uno });
            Exige(d1 == a1, "pasado ese rato vuelve a mirar de verdad, no repite lo guardado");

            Huella(dir, uno);
            Busqueda(dir, uno);

            Console.WriteLine("\n  5) LO QUE CUESTA MIRAR LA CACHE\n");
            MethodInfo busca = TF.GetMethod("IaFindEngine", EST);
            MethodInfo leeHuella = TF.GetMethod("ReadIaPipelineFingerprint", EST);
            if (busca != null)
            {
                object[] ar = new object[] { null, null };
                Console.WriteLine("     buscar el motor a pelo (6 carpetas)   " + Corre(200, delegate { busca.Invoke(null, ar); }).ToString("0.000") + " ms");
            }
            Console.WriteLine("     saber donde vive la cache (recordado) " + Corre(500, delegate { cacheFor.Invoke(null, new object[] { (string)null }); }).ToString("0.000") + " ms");
            if (leeHuella != null)
                Console.WriteLine("     leer la huella del modelo del archivo " + Corre(200, delegate { leeHuella.Invoke(null, new object[] { dir }); }).ToString("0.000") + " ms");
            Bien("y los numeros salen en cada pasada, para poder compararlos el dia que alguien toque esto");
        }
        catch (Exception e) { Mal("sin excepciones: " + (e.InnerException ?? e)); }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    // ----------------------------------------------------------------------------- la huella del modelo
    static void Huella(string dir, string uno)
    {
        Console.WriteLine("\n  3) LA HUELLA DEL MODELO SE RECUERDA DIEZ SEGUNDOS, Y LUEGO SE VUELVE A MIRAR\n");
        MethodInfo huella = TF.GetMethod("IaPipelineFingerprint", UNO);
        MethodInfo leeHuella = TF.GetMethod("ReadIaPipelineFingerprint", EST);
        FieldInfo fHCuando = TF.GetField("iaHuellaCuando", EST);
        FieldInfo fDir = TF.GetField("iaCacheDir", UNO);
        Exige(leeHuella != null && Codigo.Llama(leeHuella, "ReadAllText"), "la huella se lee del archivo, no de un rastro de antes");
        if (huella == null || fHCuando == null || fDir == null)
        { Mal("no esta el recuerdo de la huella del modelo (IaPipelineFingerprint / iaHuellaCuando / iaCacheDir)"); return; }
        object f = Codigo.FormSinEncender(TF);
        fDir.SetValue(f, dir);
        string archivo = Path.Combine(dir, "modelo.fingerprint");
        string aa = new string('a', 64), bb = new string('b', 64), cc = new string('c', 64);
        File.WriteAllText(archivo, aa);
        Envejece(fHCuando);
        string h1 = (string)huella.Invoke(f, null);
        File.WriteAllText(archivo, bb);
        string h2 = (string)huella.Invoke(f, null);
        Exige(h1 == aa, "la huella que se lee es la que hay en el archivo");
        Exige(h2 == aa, "si el modelo cambia, dentro de esos diez segundos se usa la recordada: no se abre el archivo en cada busqueda");
        Envejece(fHCuando);
        string h3 = (string)huella.Invoke(f, null);
        Exige(h3 == bb, "pasado ese rato se vuelve a leer: un modelo nuevo aparece solo, sin reiniciar");
        fDir.SetValue(f, uno);
        string h4 = (string)huella.Invoke(f, null);
        Exige(h4 == null, "otra carpeta de cache no recibe la huella de la anterior (alli no hay modelo)");
        fDir.SetValue(f, dir);
        File.Delete(archivo);
        Envejece(fHCuando);
        string h5 = (string)huella.Invoke(f, null);
        File.WriteAllText(archivo, cc);
        string h6 = (string)huella.Invoke(f, null);
        Envejece(fHCuando);
        string h7 = (string)huella.Invoke(f, null);
        Exige(h5 == null && h6 == null && h7 == cc, "tambien 'aun no hay modelo' se recuerda ese rato, y en cuanto pasa, el modelo aparece");
        MethodInfo job = TF.GetMethod("IaWriteJob", EST);
        Exige(job != null && Codigo.Llama(job, "ReadIaPipelineFingerprint") && !Codigo.Llama(job, "IaHuellaFor"),
              "el encargo al motor (en segundo plano) la sigue leyendo fresca: los trabajos nuevos van con el modelo de ahora");
    }

    // ----------------------------------------------------------------------------- la busqueda de cada cancion
    static Type TB;
    static FieldInfo fBusquedas;
    static void Olvida()
    {
        Array arr = (Array)fBusquedas.GetValue(null);
        for (int i = 0; i < arr.Length; i++) arr.SetValue(null, i);
    }
    static void EnvejeceBusquedas()
    {
        Array arr = (Array)fBusquedas.GetValue(null);
        FieldInfo c = TB.GetField("Cuando", UNO);
        for (int i = 0; i < arr.Length; i++) { object b = arr.GetValue(i); if (b != null) c.SetValue(b, unchecked(Environment.TickCount - (ms + 1000))); }
    }
    static void Envejece(FieldInfo f) { f.SetValue(null, unchecked(Environment.TickCount - (ms + 1000))); }

    static void Busqueda(string dir, string uno)
    {
        Console.WriteLine("\n  4) LA BUSQUEDA DE CADA CANCION SE RECUERDA DIEZ SEGUNDOS, CON TODA SU IDENTIDAD\n");
        MethodInfo enh = TF.GetMethod("IaEnhancedPath", UNO);
        MethodInfo hashFor = TF.GetMethod("IaHashFor", EST);
        TB = TF.GetNestedType("IaBusqueda", EST);
        fBusquedas = TF.GetField("iaBusquedas", EST);
        FieldInfo fHCuando = TF.GetField("iaHuellaCuando", EST);
        FieldInfo fDir = TF.GetField("iaCacheDir", UNO), fMezcla = TF.GetField("iaMezcla", UNO);
        if (enh == null || hashFor == null || TB == null || fBusquedas == null || fHCuando == null || fDir == null || fMezcla == null)
        { Mal("no esta el recuerdo de la busqueda de cada cancion (IaEnhancedPath / IaBusqueda / iaBusquedas)"); return; }
        object f = Codigo.FormSinEncender(TF);
        fDir.SetValue(f, dir); fMezcla.SetValue(f, "voz");
        string pipe = new string('d', 64);
        File.WriteAllText(Path.Combine(dir, "modelo.fingerprint"), pipe);
        File.WriteAllText(Path.Combine(uno, "modelo.fingerprint"), pipe);
        Envejece(fHCuando);
        string listos = Path.Combine(Path.GetFullPath(dir), "listos");
        Directory.CreateDirectory(listos);
        // En minusculas: la identidad de la cancion pasa la ruta a minusculas (en Windows da igual; aqui, no).
        string cancion = Path.Combine(dir, "cancion.mp3");
        File.WriteAllBytes(cancion, new byte[5000]);
        string h = (string)hashFor.Invoke(null, new object[] { cancion, "voz", pipe });
        string wav = Path.Combine(listos, h + ".wav");
        File.WriteAllBytes(wav, new byte[100]);
        Olvida();
        object[] ar = new object[] { cancion, false };

        string r1 = (string)enh.Invoke(f, ar); bool y1 = (bool)ar[1];
        Exige(r1 == wav && !y1, "encuentra la version mejorada de la cancion");
        DateTime viejo = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Local);
        File.SetLastAccessTime(wav, viejo);
        string r2 = (string)enh.Invoke(f, ar);
        Exige(r2 == wav && File.GetLastAccessTime(wav) == viejo, "la segunda pregunta contesta lo mismo sin tocar el disco: ni busca ni vuelve a marcar el archivo");
        File.Delete(wav);
        string r3 = (string)enh.Invoke(f, ar);
        Exige(r3 == wav, "en esos diez segundos contesta lo recordado aunque el archivo se haya ido (si no abre, la rocola ya sabe sonar la original)");
        EnvejeceBusquedas();
        string r4 = (string)enh.Invoke(f, ar);
        Exige(r4 == null, "pasado ese rato vuelve a buscar de verdad");
        File.WriteAllBytes(Path.Combine(listos, h + ".orig"), new byte[0]);
        string r5 = (string)enh.Invoke(f, ar);
        Exige(r5 == null, "y una respuesta 'aun no esta' tambien se recuerda ese rato");
        EnvejeceBusquedas();
        string r6 = (string)enh.Invoke(f, ar); bool y6 = (bool)ar[1];
        string r6b = (string)enh.Invoke(f, ar); bool y6b = (bool)ar[1];
        Exige(r6 == cancion && y6 && r6b == cancion && y6b, "la marca de calidad completa (.orig): suena la original, recordada igual");

        // La identidad entera: cancion reemplazada, otra mezcla, otra carpeta de cache.
        File.WriteAllBytes(wav, new byte[100]);
        File.Delete(Path.Combine(listos, h + ".orig"));
        EnvejeceBusquedas();
        Exige((string)enh.Invoke(f, ar) == wav, "(de nuevo con su version mejorada, recordada)");
        File.WriteAllBytes(cancion, new byte[7000]);   // la cancion se reemplaza por otra en la misma ruta
        string r7 = (string)enh.Invoke(f, ar);
        Exige(r7 == null, "una cancion reemplazada en la misma ruta es otra: no hereda la version mejorada de la anterior");
        File.WriteAllBytes(cancion, new byte[5000]); File.SetLastWriteTimeUtc(cancion, File.GetLastWriteTimeUtc(cancion).AddSeconds(3));
        string h2 = (string)hashFor.Invoke(null, new object[] { cancion, "voz", pipe });
        File.WriteAllBytes(Path.Combine(listos, h2 + ".wav"), new byte[100]);
        Exige((string)enh.Invoke(f, ar) == Path.Combine(listos, h2 + ".wav"), "(y la nueva encuentra la suya)");
        fMezcla.SetValue(f, "otra");
        Exige((string)enh.Invoke(f, ar) == null, "otra mezcla es otra pregunta");
        fMezcla.SetValue(f, "voz");
        fDir.SetValue(f, uno);
        Exige((string)enh.Invoke(f, ar) == null, "otra carpeta de cache es otra pregunta (alli no esta)");
        fDir.SetValue(f, dir);
        for (int i = 0; i < 12; i++)
        {
            string otra = Path.Combine(dir, "otra" + i + ".mp3"); File.WriteAllBytes(otra, new byte[100 + i]);
            enh.Invoke(f, new object[] { otra, false });
        }
        Array arr = (Array)fBusquedas.GetValue(null);
        Exige(arr.Length <= 8, "recuerda pocas (" + arr.Length + "): la que suena y la siguiente, no la biblioteca");

        Olvida();
        double frio = Corre(200, delegate { Olvida(); Envejece(fHCuando); enh.Invoke(f, ar); });
        enh.Invoke(f, ar);
        double tibio = Corre(500, delegate { enh.Invoke(f, ar); });
        Console.WriteLine("     preguntar sin recuerdo (huella, identidad, carpeta y marca): " + (frio * 1000).ToString("0") + " us");
        Console.WriteLine("     preguntar con recuerdo (solo la identidad de la cancion):    " + (tibio * 1000).ToString("0") + " us");
        Exige(Codigo.Llama(TF.GetMethod("IaEnhancedPath", UNO), "IaHashFor"),
              "la identidad de la cancion (ruta, tamano y fecha) se sigue mirando en cada pregunta");
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
