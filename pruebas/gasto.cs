using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;

// EL GASTO DE RECURSOS CON LA ROCOLA ENCENDIDA DIAS.
//
// La maquina del bar es un Windows XP de 32 bits: por muchos gigas que tenga, UN programa no pasa de ~2 GB,
// y encima Windows lleva su propia cuenta aparte de "objetos de dibujo" (cada mapa de bits gasta uno) con un
// tope por programa. Un mapa de bits que se olvide de liberar no se nota en una hora... y a los tres dias
// tumba la rocola.
//
// Lo mas grande que la rocola tiene en memoria es la cache de caratulas. Cuantas caben ya no es un numero
// fijo: lo decide la maquina (220 en la de XP, 1000 en la del bar, que tiene 4 GB y es de 64 bits), asi que
// esta bateria se lo PREGUNTA al programa en vez de darlo por sabido. Aqui se le hacen pasar MILES por
// delante -que es lo que ocurre navegando una biblioteca grande- y se comprueba que ni se acumulan ni se
// quedan sin liberar.
class Gasto
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static long Mem()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        return GC.GetTotalMemory(true);
    }
    static string MB(long b) { return (b / 1024.0 / 1024.0).ToString("0.0") + " MB"; }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);

        object F = FormatterServices.GetUninitializedObject(TF);
        FieldInfo fCache = TF.GetField("coverCache", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fOrder = TF.GetField("coverOrder", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fTrash = TF.GetField("coverTrash", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fLock = TF.GetField("coverLock", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mPut = TF.GetMethod("CachePut", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mDrain = TF.GetMethod("DrainCoverTrash", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mMax = TF.GetMethod("TopeDePortadas", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(fCache != null && fOrder != null && fTrash != null && mPut != null && mDrain != null,
              "la cache de caratulas y su papelera estan donde tienen que estar");
        // Si esto no estuviera, la bateria mediria contra un tope inventado y diria que todo va bien
        // midiendo otra cosa. Mejor que se caiga aqui.
        Check(mMax != null, "la rocola dice cuantas caratulas se guarda segun la maquina");
        if (mPut == null || mDrain == null || mMax == null) return Math.Max(1, fallos);

        int tope = (int)mMax.Invoke(null, null);
        fCache.SetValue(F, Activator.CreateInstance(fCache.FieldType));
        fOrder.SetValue(F, Activator.CreateInstance(fOrder.FieldType));
        FieldInfo fNodes = TF.GetField("coverNodes", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fNodes != null) fNodes.SetValue(F, Activator.CreateInstance(fNodes.FieldType));
        fTrash.SetValue(F, Activator.CreateInstance(fTrash.FieldType));
        if (fLock != null) fLock.SetValue(F, new object());

        System.Collections.IDictionary cache = (System.Collections.IDictionary)fCache.GetValue(F);
        System.Collections.ICollection trash = (System.Collections.ICollection)fTrash.GetValue(F);

        Console.WriteLine("\n  1) LA CACHE NO CRECE POR MUCHAS QUE PASEN\n");
        Console.WriteLine("     tope de la cache en ESTA maquina: " + tope + " caratulas de 256x256 (" + MB((long)tope * 256 * 256 * 4) + ")");

        // Se hacen pasar MILES: es lo que ocurre paseando por una biblioteca grande, donde cada pagina trae
        // 18 caratulas nuevas y las viejas se van cayendo por el otro lado.
        const int N = 4000;
        long antes = Mem();
        List<Bitmap> vivas = new List<Bitmap>();   // referencias para poder comprobar cuales se liberaron
        for (int i = 0; i < N; i++)
        {
            Bitmap b = new Bitmap(256, 256, PixelFormat.Format32bppPArgb);
            mPut.Invoke(F, new object[] { @"D:\Musica\Disco" + i + @"\cover.jpg", b });
            if (i % 8 == 0) mDrain.Invoke(F, null);          // el temporizador vacia la papelera de tanto en tanto
            if (i >= N - 4) vivas.Add(b);                    // las ultimas siguen dentro
        }
        mDrain.Invoke(F, null);
        long despues = Mem();
        Console.WriteLine("     pasaron " + N.ToString("#,0") + " caratulas; en la cache quedan " + cache.Count
                          + " y en la papelera " + trash.Count);
        Console.WriteLine("     memoria: " + MB(antes) + " -> " + MB(despues) + "  (creceria " + MB((long)N * 256 * 256 * 4) + " si no se liberara ninguna)");
        Check(cache.Count <= tope, "la cache se queda en su tope y no crece con el uso");
        Check(trash.Count == 0, "la papelera se vacia entera");
        Check(despues - antes < (long)(tope + 60) * 256 * 256 * 4,
              "tras pasar " + N.ToString("#,0") + " caratulas la memoria se queda en el tamano de la cache, no en el de todas");

        Console.WriteLine("\n  2) LAS EXPULSADAS SE LIBERAN DE VERDAD (no solo se sueltan)\n");
        // Soltar la referencia no basta: un mapa de bits gasta un objeto de dibujo de Windows hasta que se
        // LIBERA. Si solo se soltara, el recolector acabaria haciendolo... cuando le pareciera, y mientras
        // tanto la cuenta de Windows sube. Se comprueba que la que salio esta liberada YA.
        cache.Clear();
        ((System.Collections.IList)trash).Clear();
        object cola = fOrder.GetValue(F);
        cola.GetType().GetMethod("Clear").Invoke(cola, null);
        if (fNodes != null) ((System.Collections.IDictionary)fNodes.GetValue(F)).Clear();
        Bitmap primera = new Bitmap(256, 256, PixelFormat.Format32bppPArgb);
        mPut.Invoke(F, new object[] { @"D:\Musica\primera\cover.jpg", primera });
        for (int i = 0; i < tope; i++)
            mPut.Invoke(F, new object[] { @"D:\Musica\relleno" + i + @"\cover.jpg", new Bitmap(256, 256, PixelFormat.Format32bppPArgb) });
        Console.WriteLine("     en la papelera antes de vaciarla: " + trash.Count);
        Check(trash.Count > 0, "al desbordar la cache, la que sale va a la papelera (no se libera en caliente)");
        mDrain.Invoke(F, null);
        bool liberada = false;
        try { int w = primera.Width; liberada = (w == 0); }
        catch (ArgumentException) { liberada = true; }
        catch (InvalidOperationException) { liberada = true; }
        Console.WriteLine("     la primera caratula, tras vaciar la papelera: " + (liberada ? "LIBERADA" : "sigue viva"));
        Check(liberada, "la caratula expulsada queda LIBERADA (devuelve su objeto de dibujo a Windows)");

        Console.WriteLine("\n  3) UNA QUE SE ESTA DIBUJANDO NO SE LIBERA EN LAS NARICES DEL RENDER\n");
        // La papelera se vacia desde el hilo que dibuja, pero el cubo del disco y el mini-cubo pueden estar
        // ensenando una caratula que acaba de salir de la cache. Liberarla ahi revienta el dibujo.
        FieldInfo fMini = TF.GetField("miniCube", BindingFlags.Instance | BindingFlags.NonPublic);
        Type TMini = (fMini != null) ? fMini.FieldType : null;
        if (TMini == null) Console.WriteLine("     (no se pudo montar el mini-cubo: se salta)");
        else
        {
            object mini = FormatterServices.GetUninitializedObject(TMini);
            FieldInfo fCov = TMini.GetField("Cover", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fCov == null) Console.WriteLine("     (el mini-cubo no expone su caratula: se salta)");
            else
            {
                Bitmap enUso = new Bitmap(256, 256, PixelFormat.Format32bppPArgb);
                fCov.SetValue(mini, enUso);
                fMini.SetValue(F, mini);
                ((System.Collections.IList)trash).Add(enUso);
                mDrain.Invoke(F, null);
                bool sigue = true;
                try { int w = enUso.Width; sigue = (w == 256); } catch { sigue = false; }
                Console.WriteLine("     la caratula que el mini-cubo esta ensenando: " + (sigue ? "SIGUE VIVA" : "la liberaron"));
                Check(sigue, "una caratula en uso NO se libera aunque este en la papelera");
            }
        }

        Console.WriteLine("\n  4) LO QUE OCUPA UNA BIBLIOTECA GRANDE EN MEMORIA\n");
        // Las mismas rutas viven en varias tablas a la vez (sonoridad, duracion, EQ, cosecha, repaso a fondo)
        // y en la propia lista de canciones. Si cada tabla se quedara con SU copia del texto, con cien mil
        // canciones serian decenas de megas tirados. La rocola comparte una sola copia por ruta; aqui se mide
        // lo que eso ahorra, y de paso queda vigilado que no aparezca una tabla nueva sin compartir.
        Type TTrack = TF.GetNestedType("Track", BindingFlags.NonPublic | BindingFlags.Public);
        if (TTrack == null) Console.WriteLine("     (no encuentro la ficha de cancion: se salta)");
        else
        {
            const int NC = 50000;
            string[] rutas = new string[NC];
            for (int i = 0; i < NC; i++)
                rutas[i] = @"D:\Musica\" + (char)('A' + (i % 26)) + @"\Artista " + (i % 900)
                         + @" - Album numero " + (i % 120) + @"\" + (i % 30 + 1).ToString("00")
                         + " - Cancion con nombre de largo normal " + i + ".mp3";

            FieldInfo fP = TTrack.GetField("Path"), fA = TTrack.GetField("Artist"),
                      fT = TTrack.GetField("Title"), fAl = TTrack.GetField("Album"), fG = TTrack.GetField("Genre");
            long m0 = Mem();
            System.Collections.IList lista = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TTrack));
            for (int i = 0; i < NC; i++)
            {
                object t = FormatterServices.GetUninitializedObject(TTrack);
                fP.SetValue(t, rutas[i]); fA.SetValue(t, "Artista " + (i % 900));
                fT.SetValue(t, "Cancion con nombre de largo normal " + i);
                fAl.SetValue(t, "Album numero " + (i % 120)); fG.SetValue(t, "");
                lista.Add(t);
            }
            long m1 = Mem();

            // las cinco tablas por ruta, COMPARTIENDO el texto (lo que hace la rocola)
            Dictionary<string, float> t1 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float> t2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float> t3 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float[]> t4 = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> t5 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < NC; i++)
            { string r = rutas[i]; t1[r] = 1f; t2[r] = 1f; t3[r] = 1f; t4[r] = new float[9]; t5[r] = 2; }
            long m2 = Mem();

            // las mismas cinco, cada una con SU propia copia (lo que costaria NO compartir)
            Dictionary<string, float> c1 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float> c2 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float> c3 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, float[]> c4 = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> c5 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < NC; i++)
            {
                c1[string.Copy(rutas[i])] = 1f; c2[string.Copy(rutas[i])] = 1f; c3[string.Copy(rutas[i])] = 1f;
                c4[string.Copy(rutas[i])] = new float[9]; c5[string.Copy(rutas[i])] = 2;
            }
            long m3 = Mem();

            Console.WriteLine("     " + NC.ToString("#,0") + " canciones:");
            Console.WriteLine("        la lista de canciones            " + MB(m1 - m0));
            Console.WriteLine("        5 tablas COMPARTIENDO la ruta    " + MB(m2 - m1));
            Console.WriteLine("        las mismas SIN compartir         " + MB(m3 - m2) + "   <- lo que se ahorra compartiendo");
            Check((m2 - m1) < (m3 - m2), "compartir una sola copia de cada ruta ahorra memoria de verdad");
            Check((m1 - m0) + (m2 - m1) < 60L * 1024 * 1024,
                  "toda la biblioteca de " + NC.ToString("#,0") + " canciones cabe en menos de 60 MB");
            GC.KeepAlive(lista); GC.KeepAlive(t1); GC.KeepAlive(c1);
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
