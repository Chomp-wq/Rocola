using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// EL SONIDO DE LOS VIDEOS (etapa 9 de audio): lo que la rocola controla hoy, y la regla de no pasarse.
//
// Los videos los reproduce el control del Reproductor de Windows Media (WMP), con SU propio camino de sonido:
// la cadena de la musica (DSEE, EQ, grave, realce, limitador de pico real, continuidad sin pausas) NO les
// llega. Lo unico que la rocola les pone es el VOLUMEN del reproductor (0 a 100, y el 100 es el archivo tal
// cual: el reproductor no sabe subir). Esta bateria fija ese contrato para que un cambio no lo rompa en
// silencio:
//
//   1) nunca se le pide al reproductor mas de 100 ni menos de 0, se combine como se combine;
//   2) un video ya medido va al volumen que lo empareja con el objetivo de las canciones, dentro de sus limites
//      (no mas de x4 ni menos de x0,15 sobre el base), y "Subir TODO" NO lo empuja contra el techo;
//   3) uno sin medir sigue a "Subir TODO", topado en 100;
//   4) el volumen no depende de la hora (el modo noche se retiro el 24-10-2026);
//   5) el base automatico deja hueco para que casi todos los videos lleguen al objetivo sin pasar de 100, y
//      nunca baja de 40;
//   6) el fundido del final solo baja, desde el volumen del video, y no toca el techo.
//
// Lo que NO dice: cuanto suena de verdad cada numero del reproductor (si su 50 es -6 dB o no). Eso se mide
// en Windows con una grabacion en bucle; esta cuenta supone que el volumen del reproductor es lineal en
// amplitud, y la guia (1.20) dice como comprobarlo.

class VideoAudio
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TAlbum;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string campo) { return o.GetType().GetField(campo, T).GetValue(o); }
    static void Set(object o, string campo, object v) { o.GetType().GetField(campo, T).SetValue(o, v); }
    static object Inv(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(T))
            if (mi.Name == m && mi.GetParameters().Length == a.Length)
            {
                try { return mi.Invoke(o, a); }
                catch (TargetInvocationException tie) { throw tie.InnerException; }
            }
        throw new MissingMethodException(o.GetType().Name, m);
    }
    static int Vol(object f, string ruta) { return (int)Inv(f, "VideoVolumeFor", ruta); }

    // Deja la rocola en un estado conocido: nivelado, base a mano, "Subir TODO" en 'masterDb' ('noche' ya no se usa).
    static void Pon(object f, bool auto, int vol, float master, float noche)
    {
        Set(f, "normalizeOn", true); Set(f, "videoAuto", auto); Set(f, "videoVol", vol);
        Set(f, "masterDb", master);
        Set(f, "videoBaseFirma", -1); Set(f, "videoBaseAuto", -1);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TAlbum = TF.GetNestedType("Album", T);
        if (TF.GetMethod("VideoVolumeFor", T) == null) { Check(false, "no esta VideoVolumeFor"); return 1; }
        object f = Codigo.FormSinEncender(TF);
        float objetivo = (float)Get(f, "normTarget");
        if (objetivo <= 0.0001f) { objetivo = 0.1f; Set(f, "normTarget", objetivo); }
        IDictionary loud = (IDictionary)Get(f, "loudCache");

        Console.WriteLine("\n  1) NUNCA FUERA DE 0..100\n");
        // Videos de todas las sonoridades: sin medir, y medidos de 26 dB por debajo a 26 dB por encima del objetivo.
        string[] rutas = { "sinmedir.mp4", "v-26.mp4", "v-10.mp4", "v-3.mp4", "v0.mp4", "v+3.mp4", "v+10.mp4", "v+26.mp4" };
        double[] dbs = { double.NaN, -26, -10, -3, 0, 3, 10, 26 };
        for (int i = 1; i < rutas.Length; i++) loud[rutas[i]] = (float)(objetivo * Math.Pow(10, dbs[i] / 20.0));
        int casos = 0, fuera = 0, peor = 0;
        foreach (int vol in new int[] { 0, 1, 40, 85, 100 })
            foreach (float master in new float[] { -12f, -6f, 0f, 6f, 12f })
                foreach (float noche in new float[] { 1f })
                    foreach (string r in rutas)
                    {
                        Pon(f, false, vol, master, noche);
                        int v = Vol(f, r); casos++;
                        if (v < 0 || v > 100) { fuera++; peor = v; }
                    }
        Check(fuera == 0, casos + " combinaciones (base, Subir TODO de -12 a +12 dB, videos de -26 a +26 dB): ninguna le pide al reproductor mas de 100 ni menos de 0" + (fuera > 0 ? " (salio " + peor + ")" : ""));

        Console.WriteLine("\n  2) UN VIDEO MEDIDO SE EMPAREJA, DENTRO DE LO QUE EL REPRODUCTOR PUEDE\n");
        Pon(f, false, 50, 0f, 1f);
        int v0 = Vol(f, "v0.mp4"), vm3 = Vol(f, "v-3.mp4"), vp3 = Vol(f, "v+3.mp4"), vp10 = Vol(f, "v+10.mp4"), vm26 = Vol(f, "v-26.mp4"), vp26 = Vol(f, "v+26.mp4");
        Check(v0 == 50 && vm3 == (int)Math.Round(50 * Math.Pow(10, 3 / 20.0)) && vp3 == (int)Math.Round(50 * Math.Pow(10, -3 / 20.0)) && vp10 == (int)Math.Round(50 * Math.Pow(10, -10 / 20.0)),
              "con base 50: el que ya esta al objetivo va a 50; el de -3 dB sube a " + vm3 + " y el de +3 dB baja a " + vp3 + " (lo justo para quedar iguales)");
        Check(vm26 == 100 && vp26 == (int)Math.Round(50 * 0.15),
              "uno muy flojo (-26 dB) se queda en 100 -no se puede subir mas- y uno muy fuerte (+26 dB) no baja de x0,15 (" + vp26 + ")");
        int conMaster = 0, sinMaster = 0;
        Pon(f, false, 85, 6f, 1f); conMaster = Vol(f, "v+3.mp4");
        Pon(f, false, 85, 0f, 1f); sinMaster = Vol(f, "v+3.mp4");
        Check(conMaster == sinMaster && sinMaster < 100, "\"Subir TODO\" no empuja a los videos ya medidos contra el techo (uno de +3 dB con +6 dB de Subir TODO: " + conMaster + "; sin: " + sinMaster + ")");

        Console.WriteLine("\n  3) UNO SIN MEDIR SIGUE A \"SUBIR TODO\", TOPADO EN 100\n");
        Pon(f, false, 50, 6f, 1f); int s6 = Vol(f, "sinmedir.mp4");
        Pon(f, false, 50, -6f, 1f); int sm6 = Vol(f, "sinmedir.mp4");
        Pon(f, false, 85, 12f, 1f); int s12 = Vol(f, "sinmedir.mp4");
        Check(s6 == (int)Math.Round(50 * Math.Pow(10, 6 / 20.0)) && sm6 == (int)Math.Round(50 * Math.Pow(10, -6 / 20.0)) && s12 == 100,
              "base 50 con +6 dB: " + s6 + "; con -6 dB: " + sm6 + "; base 85 con +12 dB: " + s12 + " (el techo)");

        Console.WriteLine("\n  4) SIN MODO NOCHE: A CUALQUIER HORA, EL MISMO VOLUMEN\n");
        // El modo noche (bajaba 4 dB de 11 a 7) se retiro el 24-10-2026: el volumen de un video ya no puede depender de la hora.
        Check(TF.GetMethod("FactorNoche", T) == null && !Codigo.Llama(TF.GetMethod("VideoVolumeFor", T), "get_Now") && !Codigo.Llama(TF.GetMethod("VideoVolumeFor", T), "get_Hour"),
              "el volumen de los videos no mira el reloj (no queda nada del modo noche)");

        Console.WriteLine("\n  5) EL BASE AUTOMATICO DEJA HUECO\n");
        IList discos = (IList)Get(f, "videoAlbums"); discos.Clear();
        object d = Activator.CreateInstance(TAlbum, true);
        List<string> vf = (List<string>)Get(d, "VideoFiles");
        Random rnd = new Random(4);
        double[] niveles = new double[40];
        for (int i = 0; i < 40; i++)
        {
            string r = "auto" + i + ".mp4"; vf.Add(r);
            niveles[i] = -6 + 12 * rnd.NextDouble();   // de -6 a +6 dB respecto del objetivo (el suelo de 40 no estorba)
            loud[r] = (float)(objetivo * Math.Pow(10, niveles[i] / 20.0));
        }
        discos.Add(d);
        Pon(f, true, 85, 0f, 1f);
        int b = (int)Inv(f, "VideoBase");
        int llegan = 0; for (int i = 0; i < 40; i++) if (100.0 * Math.Pow(10, niveles[i] / 20.0) >= b - 0.5) llegan++;
        Check(b >= 40 && b <= 100 && llegan >= 34,
              "con 40 videos medidos elige base " + b + ": " + llegan + " de 40 pueden llegar al objetivo sin pasar de 100 (se da por perdido el 15 % mas flojo)");
        for (int i = 0; i < 40; i++) loud["auto" + i + ".mp4"] = (float)(objetivo * Math.Pow(10, -20 / 20.0));   // todos muy flojos
        Pon(f, true, 85, 0f, 1f);
        int bMin = (int)Inv(f, "VideoBase");
        vf.Clear(); for (int i = 0; i < 5; i++) vf.Add("auto" + i + ".mp4");
        Pon(f, true, 85, 0f, 1f);
        int bPocos = (int)Inv(f, "VideoBase");
        Pon(f, true, 85, 0f, 1f); Set(f, "normalizeOn", false);
        int bSinNivelar = (int)Inv(f, "VideoBase");
        Check(bMin == 40 && bPocos == 85 && bSinNivelar == 85,
              "nunca baja de 40 (-8 dB) aunque todos sean flojos; con menos de 8 medidos, o sin nivelado, manda la perilla (85)");

        Console.WriteLine("\n  6) EL FUNDIDO DEL FINAL SOLO BAJA\n");
        List<string> poll = Codigo.Nombres(TF.GetMethod("VideoPoll_Tick", T) ?? TF.GetMethod("VideoPoll", T));
        bool usa = poll.Contains("VideoVolumeFor") && poll.Contains("SetVideoVolume");
        // El fundido multiplica el volumen del video por un factor de 0 a 1 (se lee en el codigo: Math.Round(volBase * f)).
        Check(usa, "el sondeo del video pone el volumen desde VideoVolumeFor y el fundido lo multiplica por un factor entre 0 y 1: nunca por encima del volumen del video");
        // Todo el que le pone volumen al reproductor lo saca de VideoVolumeFor (al empezar el video, en el sondeo y al
        // tocar Ctrl+F3): nadie se lo inventa por su cuenta.
        bool deVideoVolume = true; int sitios = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            List<string> ll = Codigo.Llamadas(m);
            if (Codigo.Llama(ll, "SetVideoVolume")) { sitios++; if (!Codigo.Llama(ll, "VideoVolumeFor")) deVideoVolume = false; }
        }
        bool arranque = Codigo.Llama(TF.GetMethod("StartVideoItem", T), "VideoVolumeFor");
        Check(sitios >= 2 && deVideoVolume && arranque, "todo el que le pone volumen al reproductor lo saca de VideoVolumeFor (" + sitios + " sitios, y el arranque del video)");

        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}
