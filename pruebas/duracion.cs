using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

class Duracion
{
    static Type TF; static object F;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Call(string n, params object[] a)
    { MethodInfo mi = TF.GetMethod(n, BindingFlags.Instance|BindingFlags.NonPublic); object r = mi.Invoke(F, a); EsperaGuardado(); return r; }
    // AL GUARDAR HAY QUE ESPERAR A QUE SE HAYA GUARDADO. La rocola escribe en segundo plano: pedir
    // "guarda" no deja el archivo escrito al volver, lo deja ENCARGADO. La barrera esta en Codigo
    // porque cada rama le puso un nombre distinto y aqui no hay por que saberlo.
    static void EsperaGuardado() { Codigo.EsperaGuardado(TF, F); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static void Limpia()
    {
        SetFld("durCache", new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase));
        SetFld("durFast",  new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase));
        SetFld("durFastPend", new List<string>());
        SetFld("durFastLock", new object());
        SetFld("durScanGen", 0);
        SetFld("durScanLeft", 0);
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = Codigo.FormSinEncender(TF);
        Limpia();
        string f = (string)Call("DurFastFile");
        if (File.Exists(f)) File.Delete(f);

        string[] wavs = Directory.GetFiles("wav", "*.wav"); Array.Sort(wavs);

        // --- 1) sin duracion conocida, la lista no muestra nada (comportamiento de hoy)
        Check((string)Call("DurText", wavs[0]) == "", "sin medir todavia: la lista no muestra duracion");

        // --- 2) el barrido rapido: leer la duracion de cada archivo y publicarla (mismo camino que el hilo)
        List<string> rutas = new List<string>(); List<float> segs = new List<float>();
        foreach (string p in wavs)
            using (NAudio.Wave.AudioFileReader rd = new NAudio.Wave.AudioFileReader(p))
            { rutas.Add(p); segs.Add((float)rd.TotalTime.TotalSeconds); }
        SetFld("durScanLeft", rutas.Count);
        Call("PublicaDuraciones", 0, rutas.ToArray(), segs.ToArray(), 0);

        Dictionary<string,float> df = (Dictionary<string,float>)Fld("durFast");
        Check(df.Count == wavs.Length, "el barrido publica las " + wavs.Length + " duraciones (hay " + df.Count + ")");
        Check((int)Fld("durScanLeft") == 0, "el contador de pendientes baja a 0");
        Console.WriteLine("        ejemplo: " + Path.GetFileNameWithoutExtension(wavs[0]) + " -> " + Call("DurText", wavs[0]));
        Check((string)Call("DurText", wavs[0]) == "3:00", "la lista YA muestra 3:00 (cancion de 180 s)");
        Check((string)Call("DurText", wavs[4]) == "0:40", "y 0:40 en la corta de 40 s");

        // --- 3) se guarda por anadido y sobrevive al reinicio
        Check(File.Exists(f) && File.ReadAllLines(f).Length == wavs.Length, "se guardan " + wavs.Length + " lineas en rocola_duracion.txt");
        long t1 = new FileInfo(f).Length;
        Call("PublicaDuraciones", 0, rutas.ToArray(), segs.ToArray(), 0);   // repetir NO debe duplicar
        Check(new FileInfo(f).Length == t1, "volver a publicar lo mismo NO duplica el archivo");

        object F2 = Codigo.FormSinEncender(TF); F = F2; Limpia();
        Call("LoadDurFast");
        Check(((Dictionary<string,float>)Fld("durFast")).Count == wavs.Length, "tras reiniciar la rocola las recuerda todas");
        Check((string)Call("DurText", wavs[0]) == "3:00", "y se siguen viendo en la lista");

        // --- 4) la del analisis COMPLETO manda sobre la del barrido
        Dictionary<string,float> dc = (Dictionary<string,float>)Fld("durCache");
        dc[wavs[0]] = 123.0f;
        Check((string)Call("DurText", wavs[0]) == "2:03", "si el analisis completo la mide, esa manda (2:03)");

        // --- 4b) un lote VACIO (solo archivos rotos) TAMBIEN baja el contador
        SetFld("durScanLeft", 55);
        Call("PublicaDuraciones", 0, new string[0], new float[0], 40);
        Check((int)Fld("durScanLeft") == 40, "un lote sin duraciones (archivos rotos) igual reporta avance (55 -> 40)");
        Call("PublicaDuraciones", 0, new string[0], new float[0], 0);
        Check((int)Fld("durScanLeft") == 0, "y al final llega a 0 aunque lo ultimo fueran puros rotos");

        // --- 5) generacion caducada (re-escaneo con F5): se descarta, no ensucia
        object F3 = Codigo.FormSinEncender(TF); F = F3; Limpia();
        SetFld("durScanGen", 7);
        Call("PublicaDuraciones", 3, rutas.ToArray(), segs.ToArray(), 0);
        Check(((Dictionary<string,float>)Fld("durFast")).Count == 0, "un lote de una generacion vieja se descarta");

        // --- 6) LOS VIDEOS. Antes no se median: la tanda se armaba solo con 'tracks', que son canciones, y
        // los videos viven aparte (en VideoFiles de cada disco), asi que en la lista salian sin duracion.
        Console.WriteLine("\n  LOS VIDEOS\n");
        MethodInfo ESVID = TF.GetMethod("EsVideo", BindingFlags.Static|BindingFlags.NonPublic);
        Check(ESVID != null, "la rocola sabe distinguir un video de una cancion por su extension");
        if (ESVID != null)
        {
            string[] vids = { "a.mp4", "b.AVI", "c.wmv", "d.mkv", "e.mpg", "f.vob", "g.flv", "h.m4v" };
            string[] noVids = { "x.mp3", "y.flac", "z.wav", "w.ogg", "v.m4a" };
            bool todos = true, ninguno = true;
            foreach (string v in vids) if (!(bool)ESVID.Invoke(null, new object[]{ v })) todos = false;
            foreach (string v in noVids) if ((bool)ESVID.Invoke(null, new object[]{ v })) ninguno = false;
            Check(todos, "reconoce los " + vids.Length + " formatos de video (mp4, avi, wmv, mkv, mpg, vob, flv, m4v)");
            Check(ninguno, "y no confunde una cancion con un video");
            Check(!(bool)ESVID.Invoke(null, new object[]{ (string)null }), "un archivo sin nombre no truena");
        }
        // Un video con duracion ya medida se ve en la lista igual que una cancion. Aqui se publica a mano lo
        // que el hilo mediria con el reproductor de Windows (que en esta maquina no existe).
        object F4 = Codigo.FormSinEncender(TF); F = F4; Limpia();
        string vid = Path.Combine(Path.GetDirectoryName(wavs[0]), "concierto.mp4");
        Call("PublicaDuraciones", 0, new string[]{ vid }, new float[]{ 273f }, 0);
        Console.WriteLine("        " + Path.GetFileName(vid) + " -> " + Call("DurText", vid));
        Check((string)Call("DurText", vid) == "4:33", "un video medido se ve en la lista igual que una cancion (4:33)");
        Check(ESVID != null && (bool)ESVID.Invoke(null, new object[]{ vid }), "y se mide por el camino de los videos, no por el de NAudio");
        // Y el medidor tiene que existir, con su parametro por referencia (lo lleva el hilo, no un campo)
        MethodInfo MV = TF.GetMethod("MideDuracionVideo", BindingFlags.Instance|BindingFlags.NonPublic);
        Check(MV != null && MV.GetParameters().Length == 2 && MV.GetParameters()[1].ParameterType.IsByRef,
              "el reproductor con el que se miden lo lleva el propio hilo: dos tandas a la vez no se cruzan");
        MethodInfo MA = TF.GetMethod("MideDuracionAudio", BindingFlags.Static|BindingFlags.NonPublic);
        Check(MA != null && Math.Abs((float)MA.Invoke(null, new object[]{ wavs[0] }) - 180f) < 0.5f,
              "y las canciones se siguen midiendo como siempre (180 s)");

        if (File.Exists(f)) File.Delete(f);
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
