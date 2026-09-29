using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

// LA ETIQUETA DE LA SIGUIENTE CANCION SE LEE POR ADELANTADO, NO AL EMPEZARLA.
//
// Al empezar una cancion, la rocola leia su etiqueta ID3 (titulo, artista, disco y genero, que da el color del
// fondo) abriendo el MP3 en el hilo de la pantalla. Con un disco mecanico ocupado, o con el antivirus mirando
// cada archivo que se abre, eso es un tiron justo al cambiar de cancion. Ahora la lectura por adelantado de la
// siguiente (PrecalientaSiguiente) la lee en su hilo, de los mismos bytes que acaba de leer, y al empezar solo
// se mira que el archivo no haya cambiado (tamano y fechas, sin abrirlo).
//
// Lo que se vigila:
//   1) que el resultado es EL MISMO, campo por campo, que leerla al empezar, en todos los tipos de etiqueta;
//   2) que al empezar NO se abre el archivo (se comprueba con el archivo bloqueado por otro);
//   3) que nunca se usa una etiqueta vieja: archivo cambiado, cancion cambiada u otra cancion;
//   4) que la lectura por adelantado de verdad la deja lista, y el enganche en el programa;
//   5) lo que cuesta cada camino.
class EtiquetaDelante
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (ok) oks++; else fallos++; }

    static Type TF, TT;
    static MethodInfo mFromFile, mEnsure, mCopia, mLee, mPon, mFirma;
    static string dir;

    static object Cancion(string ruta) { return mFromFile.Invoke(null, new object[] { ruta }); }
    static string Campos(object t)
    {
        return "titulo=" + TT.GetField("Title").GetValue(t) + " | artista=" + TT.GetField("Artist").GetValue(t)
             + " | disco=" + TT.GetField("Album").GetValue(t) + " | genero=" + TT.GetField("Genre").GetValue(t);
    }
    static void Ensure(object t) { mEnsure.Invoke(t, null); }
    static string Firma(string ruta) { return (string)mFirma.Invoke(null, new object[] { ruta }); }
    // Lo que hace la lectura por adelantado: copia en la pantalla, lee en el hilo, la deja lista.
    static void Adelanta(object t, string ruta, bool deMemoria)
    {
        string firma = Firma(ruta);
        object antes = mCopia.Invoke(t, null);
        object leida = mLee.Invoke(null, new object[] { antes, deMemoria ? File.ReadAllBytes(ruta) : null });
        mPon.Invoke(null, new object[] { t, antes, leida, firma });
    }
    // Empieza la cancion con el archivo BLOQUEADO por otro: si EnsureTags lo abriera, no podria leer nada.
    static void EnsureBloqueado(object t, string ruta)
    {
        using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Ensure(t);
    }

    // ---------------------------------------------------------------- MP3 de mentira con distintas etiquetas
    static Encoding latin = Encoding.GetEncoding(28591);
    static void Marco(List<byte> l, string id, byte[] cuerpo, int major)
    {
        l.AddRange(Encoding.ASCII.GetBytes(id));
        int n = cuerpo.Length;
        if (major >= 4) l.AddRange(new byte[] { (byte)((n >> 21) & 0x7F), (byte)((n >> 14) & 0x7F), (byte)((n >> 7) & 0x7F), (byte)(n & 0x7F), 0, 0 });
        else l.AddRange(new byte[] { (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n, 0, 0 });
        l.AddRange(cuerpo);
    }
    static byte[] Texto(string s) { byte[] t = latin.GetBytes(s); byte[] c = new byte[1 + t.Length]; Array.Copy(t, 0, c, 1, t.Length); return c; }
    static void V2(List<byte> b, int major, int flags, int relleno, int declaraDeMas, params string[] pares)
    {
        List<byte> marcos = new List<byte>();
        for (int i = 0; i + 1 < pares.Length; i += 2)
        {
            if (pares[i] == "APIC") Marco(marcos, "APIC", new byte[int.Parse(pares[i + 1])], major);
            else Marco(marcos, pares[i], Texto(pares[i + 1]), major);
        }
        int tam = marcos.Count + relleno + declaraDeMas;
        b.AddRange(new byte[] { (byte)'I', (byte)'D', (byte)'3', (byte)major, 0, (byte)flags,
                                (byte)((tam >> 21) & 0x7F), (byte)((tam >> 14) & 0x7F), (byte)((tam >> 7) & 0x7F), (byte)(tam & 0x7F) });
        b.AddRange(marcos); b.AddRange(new byte[relleno]);
    }
    static void V1(List<byte> b, string titulo, string artista, string disco, int genero)
    {
        byte[] tag = new byte[128];
        tag[0] = (byte)'T'; tag[1] = (byte)'A'; tag[2] = (byte)'G';
        byte[] x = latin.GetBytes(titulo); Array.Copy(x, 0, tag, 3, Math.Min(30, x.Length));
        x = latin.GetBytes(artista); Array.Copy(x, 0, tag, 33, Math.Min(30, x.Length));
        x = latin.GetBytes(disco); Array.Copy(x, 0, tag, 63, Math.Min(30, x.Length));
        tag[127] = (byte)genero;
        b.AddRange(tag);
    }
    static void Audio(List<byte> b, int n) { for (int i = 0; i < n; i++) b.Add((byte)(i * 37 + 11)); }
    static string Escribe(string nombre, List<byte> b) { string r = Path.Combine(dir, nombre); File.WriteAllBytes(r, b.ToArray()); return r; }

    static List<string> Casos()
    {
        List<string> r = new List<string>();
        List<byte> b;
        b = new List<byte>(); V2(b, 3, 0, 64, 0, "APIC", "20000", "TIT2", "Titulo Tres", "TPE1", "Artista Tres", "TALB", "Disco Tres", "TCON", "(17)"); Audio(b, 8192);
        r.Add(Escribe("Nombre Falso - Cancion 23.mp3", b));
        b = new List<byte>(); V2(b, 4, 0, 32, 0, "TIT2", "Titulo Cuatro", "TPE1", "Artista Cuatro", "TCON", "Cumbia"); Audio(b, 8192);
        r.Add(Escribe("01 - Otro - Tema 24.mp3", b));
        b = new List<byte>(); Audio(b, 8192); V1(b, "Titulo Uno", "Artista Uno", "Disco Uno", 13);
        r.Add(Escribe("Solo v1.mp3", b));
        b = new List<byte>(); V2(b, 3, 0, 16, 0, "TIT2", "Solo Titulo"); Audio(b, 4096); V1(b, "Otro Titulo", "Del v1", "Disco v1", 9);
        r.Add(Escribe("Mezcla - v2 y v1.mp3", b));
        b = new List<byte>(); Audio(b, 8192);
        r.Add(Escribe("Sin Etiqueta - Nada.mp3", b));
        b = new List<byte>(); V2(b, 3, 0x80, 16, 0, "TIT2", "Con Escape", "TPE1", "Artista ÿé"); Audio(b, 4096);
        r.Add(Escribe("Escape v23.mp3", b));
        b = new List<byte>(); V2(b, 3, 0, 0, 900000, "TIT2", "Cortada", "TPE1", "Truncada"); Audio(b, 1024); V1(b, "De v1", "Rescate", "", 17);
        r.Add(Escribe("Truncada - Mal.mp3", b));
        b = new List<byte>(); V2(b, 3, 0, 16, 0, "TIT2", "No Se Lee", "TPE1", "Porque es WAV"); Audio(b, 4096);
        r.Add(Escribe("Un Wav - Con Etiqueta.wav", b));
        b = new List<byte>(); V2(b, 3, 0, 16, 0, "TIT2", "Mayusculas", "TPE1", "Extension MP3"); Audio(b, 4096);
        r.Add(Escribe("Grita - Fuerte.MP3", b));
        return r;
    }

    static void Igual(List<string> casos)
    {
        Console.WriteLine("\n  1) EL MISMO RESULTADO QUE LEERLA AL EMPEZAR, CAMPO POR CAMPO\n");
        int iguales = 0, igualesArchivo = 0, conEtiqueta = 0;
        foreach (string ruta in casos)
        {
            object a = Cancion(ruta); Ensure(a);                                  // como siempre: al empezar
            object b = Cancion(ruta); Adelanta(b, ruta, true); EnsureBloqueado(b, ruta);   // por adelantado, de los bytes
            object c = Cancion(ruta); Adelanta(c, ruta, false); EnsureBloqueado(c, ruta);  // por adelantado, del archivo
            string ca = Campos(a), cb = Campos(b), cc = Campos(c);
            if (ca == cb) iguales++; else Console.WriteLine("     distinto en " + Path.GetFileName(ruta) + ":\n       al empezar:     " + ca + "\n       por adelantado: " + cb);
            if (ca == cc) igualesArchivo++;
            if (ca != Campos(Cancion(ruta))) conEtiqueta++;
        }
        Console.WriteLine("     " + casos.Count + " archivos: v2.3 con caratula delante, v2.4, solo v1, v2 a medias completada por v1, sin etiqueta,");
        Console.WriteLine("     v2.3 con escape, v2 truncada (rescata el v1), un WAV (no se lee) y la extension en mayusculas");
        Check(iguales == casos.Count, "leida por adelantado de los bytes en memoria: identica en los " + iguales + " de " + casos.Count);
        Check(igualesArchivo == casos.Count, "y leida por adelantado del archivo (si no cabia en memoria): identica en los " + igualesArchivo + " de " + casos.Count);
        Check(conEtiqueta >= 6, "y la prueba mira de verdad: en " + conEtiqueta + " de ellos la etiqueta cambia lo que sale del nombre del archivo");
    }

    static void SinAbrir(List<string> casos)
    {
        Console.WriteLine("\n  2) AL EMPEZAR NO SE ABRE EL ARCHIVO\n");
        string ruta = casos[0];
        object sola = Cancion(ruta); EnsureBloqueado(sola, ruta);
        object a = Cancion(ruta); Ensure(a);
        Check(Campos(sola) == Campos(Cancion(ruta)) && Campos(a) != Campos(sola),
              "(control: con el archivo bloqueado y sin lectura por adelantado, al empezar no se puede leer nada)");
        object b = Cancion(ruta); Adelanta(b, ruta, true); EnsureBloqueado(b, ruta);
        Check(Campos(b) == Campos(a), "con la lectura por adelantado, la etiqueta llega entera aunque el archivo este bloqueado: no se abrio");
        Check((bool)TT.GetProperty("TagsLoaded", T).GetValue(b, null), "y la cancion queda con la etiqueta leida (no se vuelve a pedir)");
    }

    static void NuncaVieja(List<string> casos)
    {
        Console.WriteLine("\n  3) NUNCA UNA ETIQUETA VIEJA\n");
        string ruta = Path.Combine(dir, "Cambia - Luego.mp3");
        List<byte> b1 = new List<byte>(); V2(b1, 3, 0, 16, 0, "TIT2", "Primera", "TPE1", "Antes", "TCON", "Rock"); Audio(b1, 4096);
        List<byte> b2 = new List<byte>(); V2(b2, 3, 0, 16, 0, "TIT2", "Segunda version", "TPE1", "Despues", "TCON", "Jazz"); Audio(b2, 4096);
        List<byte> b3 = new List<byte>(); V2(b3, 3, 0, 16, 0, "TIT2", "Tercera", "TPE1", "Mismo largo", "TCON", "Rock"); Audio(b3, 4096);

        File.WriteAllBytes(ruta, b1.ToArray());
        object t = Cancion(ruta); Adelanta(t, ruta, true);
        File.WriteAllBytes(ruta, b2.ToArray());   // se cambia el archivo antes de que empiece
        Ensure(t);
        object fresca = Cancion(ruta); Ensure(fresca);
        Check(Campos(t) == Campos(fresca) && Campos(t).IndexOf("Segunda") >= 0, "si el archivo cambio desde que se leyo, al empezar se lee el de ahora");

        File.WriteAllBytes(ruta, b1.ToArray());
        object u = Cancion(ruta); Adelanta(u, ruta, true);
        DateTime fecha = File.GetLastWriteTimeUtc(ruta);
        byte[] mismoLargo = b3.ToArray(); Array.Resize(ref mismoLargo, b1.Count);
        File.WriteAllBytes(ruta, mismoLargo); File.SetLastWriteTimeUtc(ruta, fecha.AddSeconds(2));
        Ensure(u);
        Check(Campos(u).IndexOf("Tercera") >= 0, "y con el mismo tamano pero otra fecha, tambien: " + Campos(u));

        File.WriteAllBytes(ruta, b1.ToArray());
        object de = Cancion(ruta), otra = Cancion(ruta);
        Adelanta(de, ruta, true);
        EnsureBloqueado(otra, ruta);
        Check(Campos(otra) == Campos(Cancion(ruta)), "lo leido para una cancion no se le pasa a otra (aunque sea el mismo archivo)");
        EnsureBloqueado(de, ruta);
        Check(Campos(de).IndexOf("Primera") >= 0, "y sigue esperando a la suya, que lo recibe");

        object cambiada = Cancion(ruta); Adelanta(cambiada, ruta, true);
        TT.GetField("Genre").SetValue(cambiada, "Tango");   // alguien cambio la cancion entre medias
        Ensure(cambiada);
        object f2 = Cancion(ruta); TT.GetField("Genre").SetValue(f2, "Tango"); Ensure(f2);
        Check(Campos(cambiada) == Campos(f2), "si la cancion cambio desde que se copio, se lee como siempre sobre la de ahora");

        object dos = Cancion(ruta); Adelanta(dos, ruta, true); Ensure(dos);
        TT.GetField("Title").SetValue(dos, "Tocado a mano");
        Ensure(dos);
        Check(Campos(dos).IndexOf("Tocado a mano") >= 0, "una cancion ya leida no se vuelve a tocar");
        object tres = Cancion(ruta); Adelanta(tres, ruta, true);
        object cuatro = Cancion(ruta); Adelanta(cuatro, ruta, true);   // llega la de otra: hay un solo hueco
        EnsureBloqueado(tres, ruta);
        Check(Campos(tres) == Campos(Cancion(ruta)), "hay UN hueco (la siguiente es una): lo nuevo reemplaza a lo de antes, que se lee como siempre");
    }

    static void DeVerdad(string ruta)
    {
        Console.WriteLine("\n  4) LA LECTURA POR ADELANTADO LA DEJA LISTA, Y EL ENGANCHE\n");
        object f = Codigo.FormSinEncender(TF);
        Type tItem = TF.GetNestedType("PlayItem", T);
        object item = Activator.CreateInstance(tItem, true);
        object t = Cancion(ruta);
        tItem.GetField("Song").SetValue(item, t);
        IList cola = (IList)TF.GetField("queue", T).GetValue(f);
        cola.Add(item);
        IDictionary dur = TF.GetField("durCache", T).GetValue(f) as IDictionary;
        if (dur != null) dur[ruta] = 180f;   // ya medida: el calentador no se pone a medirla
        FieldInfo fDe = TT.GetField("etiquetaDe", T);
        mPon.Invoke(null, new object[] { null, null, null, null });
        Stopwatch sw = Stopwatch.StartNew();
        TF.GetMethod("PrecalientaSiguiente", T).Invoke(f, null);
        while (sw.ElapsedMilliseconds < 15000 && !object.ReferenceEquals(fDe.GetValue(null), t)) System.Threading.Thread.Sleep(20);
        Check(object.ReferenceEquals(fDe.GetValue(null), t), "la lectura por adelantado de la siguiente deja su etiqueta lista (" + sw.ElapsedMilliseconds + " ms, con su pausa de 1,5 s)");
        EnsureBloqueado(t, ruta);
        object a = Cancion(ruta); Ensure(a);
        Check(Campos(t) == Campos(a), "y al empezar, con el archivo bloqueado, sale igual que leerla entonces");
        TF.GetField("warmSongGen", T).SetValue(f, (int)TF.GetField("warmSongGen", T).GetValue(f) + 1);

        MethodInfo ensure = TT.GetMethod("EnsureTags", T);
        List<string> ll = Codigo.Llamadas(ensure);
        int toma = ll.IndexOf("Track.TomaEtiquetaLeida"), lee = ll.IndexOf("Track.ReadId3");
        Check(toma >= 0 && lee > toma, "EnsureTags mira primero lo leido por adelantado y solo si no vale abre el archivo");
        Check(Codigo.Llama(TT.GetMethod("TomaEtiquetaLeida", T), "AudioFileSignature"), "y antes de usarlo comprueba que el archivo no cambio (tamano y fechas)");
        MethodInfo pre = TF.GetMethod("PrecalientaSiguiente", T);
        List<string> propio = Codigo.Llamadas(pre), todo = Codigo.ConAyudantes(TF, "PrecalientaSiguiente");
        Check(Codigo.Llama(propio, "CopiaEtiqueta") && !Codigo.Llama(propio, "LeeEtiquetaAparte"),
              "la copia de la cancion se hace en la pantalla, y la lectura no");
        Check(Codigo.Llama(todo, "LeeEtiquetaAparte") && Codigo.Llama(todo, "PonEtiquetaLeida"), "la lectura va en el hilo del calentador, que la deja lista");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "PlayNext"), "EnsureTags") && Codigo.Llama(Codigo.ConAyudantes(TF, "ApuntaGeneroTrasRelevo"), "EnsureTags"),
              "empezar una cancion (y el relevo sin pausa) sigue pidiendo la etiqueta por EnsureTags");
    }

    static void Coste(List<string> casos)
    {
        Console.WriteLine("\n  5) LO QUE CUESTA EN LA PANTALLA AL EMPEZAR\n");
        string ruta = casos[0];
        int n = 400;
        object[] a = new object[n], b = new object[n];
        for (int i = 0; i < n; i++) { a[i] = Cancion(ruta); b[i] = Cancion(ruta); }
        Ensure(Cancion(ruta));
        Stopwatch s1 = Stopwatch.StartNew(); for (int i = 0; i < n; i++) Ensure(a[i]); s1.Stop();
        double pre = 0;
        Stopwatch s2 = new Stopwatch();
        for (int i = 0; i < n; i++) { Adelanta(b[i], ruta, true); s2.Start(); Ensure(b[i]); s2.Stop(); }
        pre = s2.Elapsed.TotalMilliseconds * 1000.0 / n;
        Console.WriteLine("     leerla al empezar (abrir y leer el archivo): " + (s1.Elapsed.TotalMilliseconds * 1000.0 / n).ToString("0") + " us");
        Console.WriteLine("     ya leida por adelantado (tamano y fechas):   " + pre.ToString("0") + " us");
        Console.WriteLine("     (aqui el archivo esta en la memoria del sistema; en el bar, abrirlo es lo que puede esperar al disco o al antivirus)");
        Check(true, "los numeros salen en cada pasada");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TT = TF.GetNestedType("Track", T);
        mFromFile = TT.GetMethod("FromFile", T); mEnsure = TT.GetMethod("EnsureTags", T);
        mCopia = TT.GetMethod("CopiaEtiqueta", T); mLee = TT.GetMethod("LeeEtiquetaAparte", T); mPon = TT.GetMethod("PonEtiquetaLeida", T);
        mFirma = TF.GetMethod("AudioFileSignature", T);
        Console.WriteLine("\n  LA ETIQUETA DE LA SIGUIENTE, LEIDA POR ADELANTADO");
        if (mCopia == null || mLee == null || mPon == null || TT.GetProperty("TagsLoaded", T) == null)
        {
            Check(false, "no estan la copia, la lectura aparte ni el hueco de la etiqueta leida por adelantado (Track.CopiaEtiqueta / LeeEtiquetaAparte / PonEtiquetaLeida)");
            Console.WriteLine("\n  FALLOS: " + fallos);
            return fallos;
        }
        dir = Path.Combine(Path.GetTempPath(), "rocola_etiqueta_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        try
        {
            List<string> casos = Casos();
            Igual(casos);
            SinAbrir(casos);
            NuncaVieja(casos);
            DeVerdad(casos[1]);
            Coste(casos);
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos;
    }
}
