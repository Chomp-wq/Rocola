using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

// LA SIGUIENTE CANCION PASA ANTES QUE LAS CARATULAS CERCA DEL FINAL (22-10-2026; Form1.SinPausa.cs y Form1.Portadas.cs).
//
// El calentador de la siguiente cancion (PrecalientaSiguiente) espera 1,5 s al empezar, cede el paso a las caratulas en
// camino y vuelve a mirar en cada trozo. Cuando a la cancion que suena le quedan 45 s o menos, se salta esas esperas y la
// lee y abre ya; con mas tiempo, todo como siempre. No cambia el sonido: solo el orden en que se usa el disco.
//   1) la regla: 45 s es urgente, 46 no; sin dato (nada suena), no;
//   2) el reloj de la cancion apunta lo que queda, y nuevo cada cancion o al parar;
//   3) el hilo de verdad, con un archivo en disco: con tiempo espera (1,5 s y las caratulas); a 45 s del final no;
//      una espera de caratulas ya empezada se rompe en cuanto la cancion entra en los ultimos 45 s;
//   4) el cableado en el programa.
// No es medir un disco de verdad: el tiempo que se gana lo dicen las esperas que desaparecen, no un cronometro de HDD.
class AdelantoPrioridad
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static Type TF, TTrack, TItem;
    static object f;
    static object Get(object o, string n) { return o.GetType().GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, T).SetValue(o, v); }

    static string Adelanto() { return (string)TF.GetField("adelantoRuta", T).GetValue(null); }
    static void LimpiaAdelanto() { TF.GetMethod("SueltaEnMemoria", T).Invoke(null, null); }
    // Espera hasta 'ms' a que la lectura por adelantado deje la cancion en memoria. Devuelve cuanto tardo (-1 si no llego).
    static int Espera(string ruta, int ms)
    {
        int t0 = Environment.TickCount;
        while (Environment.TickCount - t0 < ms)
        {
            if (string.Equals(Adelanto(), ruta, StringComparison.OrdinalIgnoreCase)) return Environment.TickCount - t0;
            Thread.Sleep(5);
        }
        return -1;
    }
    static void Pone(string ruta)
    {
        object tr = Activator.CreateInstance(TTrack, true); Set(tr, "Path", ruta);
        object it = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TItem); Set(it, "Song", tr);
        IList q = (IList)Get(f, "queue"); q.Clear(); q.Add(it);
        Set(f, "warmSongPath", null);
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TTrack = TF.GetNestedType("Track", T); TItem = TF.GetNestedType("PlayItem", T);
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_adelanto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try
        {
            Console.WriteLine("\n  1) LA REGLA\n");
            MethodInfo regla = TF.GetMethod("SiguienteEsUrgente", T);
            Check(regla != null, "existe la regla (SiguienteEsUrgente)");
            if (regla == null) return 1;
            Func<int, bool> urg = delegate (int ms) { return (bool)regla.Invoke(null, new object[] { ms }); };
            Check((int)TF.GetField("SiguientePrioridadSeg", T).GetValue(null) == 45, "el margen son 45 s");
            Check(urg(45000) && urg(44999) && urg(1000) && urg(0), "con 45 s o menos, urgente");
            Check(!urg(45001) && !urg(60000) && !urg(240000), "con mas de 45 s, no");
            Check(!urg(int.MaxValue), "con un valor enorme, no");

            Console.WriteLine("\n  2) EL RELOJ DE LA CANCION\n");
            f = Codigo.FormSinEncender(TF);
            Check(!(bool)TF.GetMethod("SiguienteUrgente", T).Invoke(f, null), "sin dato (nada suena o aun no hay tic), no hay prisa");
            MethodInfo urgente = TF.GetMethod("SiguienteUrgente", T);
            Check(!(bool)urgente.Invoke(f, null), "sin dato (nada suena o aun no hay tic), no hay prisa");
            FieldInfo fq = TF.GetField("quedanMs", T);
            MethodInfo apunta = TF.GetMethod("ApuntaQuedan", T);
            Check((int)fq.GetValue(f) == 0, "al principio no se sabe (0)");
            apunta.Invoke(f, new object[] { TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(240) });
            Check((int)fq.GetValue(f) == 40001, "a 200 s de 240 quedan 40000 ms (guardados como 40001: el 0 es \"no se sabe\")");
            apunta.Invoke(f, new object[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(240) });
            Check((int)fq.GetValue(f) == 230001, "a 10 s de 240 quedan 230000 ms");
            apunta.Invoke(f, new object[] { TimeSpan.FromSeconds(250), TimeSpan.FromSeconds(240) });
            Check((int)fq.GetValue(f) == 1, "pasado del final queda 0 ms, no negativo");
            apunta.Invoke(f, new object[] { TimeSpan.FromSeconds(5), TimeSpan.Zero });
            Check((int)fq.GetValue(f) == 0, "sin duracion (no se sabe), vuelve a no saber");
            Check(Codigo.Llama(TF.GetMethod("ProgressTimer_Tick", T), "ApuntaQuedan"), "el reloj de la cancion lo apunta en cada tic");
            int l, e; Codigo.UsosDeCampo(TF.GetMethod("DisposePlaybackNow", T), "quedanMs", out l, out e);
            Check(e > 0, "al soltar la cancion que suena, se olvida lo que quedaba (la siguiente no hereda la prisa)");
            Codigo.UsosDeCampo(TF.GetMethod("PasaRelevo", T), "quedanMs", out l, out e);
            Check(e > 0, "y al pasar al relevo sin pausa, tambien");

            Console.WriteLine("\n  3) EL HILO DE VERDAD\n");
            string ruta = Path.Combine(tmp, "tema.mp3");
            byte[] datos = new byte[900 * 1024]; new Random(7).NextBytes(datos); File.WriteAllBytes(ruta, datos);
            MethodInfo precalienta = TF.GetMethod("PrecalientaSiguiente", T);
            // a) con tiempo de sobra: espera su 1,5 s (no llega antes de 1 s) y luego lee.
            LimpiaAdelanto(); fq.SetValue(f, 240001); Pone(ruta);
            int t0 = Environment.TickCount; precalienta.Invoke(f, null);
            int antes = Espera(ruta, 1000);
            Check(antes < 0, "con 240 s por delante, a la primera ni 1 s no ha leido nada (espera su 1,5 s)");
            int tarde = Espera(ruta, 4000);
            Check(tarde >= 0, "y despues la deja en memoria como siempre (a los " + (Environment.TickCount - t0) + " ms)");
            // b) a 40 s del final: sin esperas.
            LimpiaAdelanto(); fq.SetValue(f, 40001); Pone(ruta);
            t0 = Environment.TickCount; precalienta.Invoke(f, null);
            int rapido = Espera(ruta, 1200);
            Check(rapido >= 0 && rapido < 1000, "a 40 s del final la lee ya, sin el 1,5 s (" + rapido + " ms)");
            // c) a 40 s del final y con caratulas en camino: la siguiente pasa antes (ni la espera de antes ni la de cada trozo).
            IDictionary cargando = (IDictionary)Get(f, "coverLoading");
            object trabajo = Activator.CreateInstance(TF.GetNestedType("CoverJob", T), true);
            lock (Get(f, "coverLock")) { cargando["caratula-falsa"] = trabajo; }
            LimpiaAdelanto(); fq.SetValue(f, 40001); Pone(ruta);
            precalienta.Invoke(f, null);
            rapido = Espera(ruta, 1500);
            Check(rapido >= 0 && rapido < 1200, "a 40 s del final y con caratulas cargando, la lee igual (" + rapido + " ms)");
            // d) con tiempo y con caratulas cargando: espera (no lee en 3 s); y si la cancion entra en los ultimos 45 s a media
            //    espera, la espera se rompe (en cuanto el hilo vuelve a mirar, cada 300 ms).
            LimpiaAdelanto(); fq.SetValue(f, 240001); Pone(ruta);
            precalienta.Invoke(f, null);
            Check(Espera(ruta, 3000) < 0, "con 240 s por delante y caratulas cargando, espera (nada leido en 3 s)");
            fq.SetValue(f, 44001);
            rapido = Espera(ruta, 2000);
            Check(rapido >= 0 && rapido < 1200, "al entrar en los ultimos 45 s a media espera, la rompe y lee (" + rapido + " ms)");
            lock (Get(f, "coverLock")) { cargando.Clear(); }
            Set(f, "warmSongPath", null);
            ((IList)Get(f, "queue")).Clear();
            Thread.Sleep(50);
            precalienta.Invoke(f, null);   // cola vacia: cancela lo que hubiera

            Console.WriteLine("\n  4) EL CABLEADO\n");
            List<string> cal = Codigo.ConAyudantes(TF, "PrecalientaSiguiente");
            int n = 0; foreach (string s in cal) if (s == "Form1.SiguienteUrgente" || s.EndsWith(".SiguienteUrgente")) n++;
            Check(n >= 3, "el calentador mira la prisa en la espera de arranque, en la de las caratulas y en cada trozo (" + n + " veces)");
            Check(Codigo.Llama(cal, "Sleep") && Codigo.Llama(cal, "EsperaPortadas"), "y sigue esperando y cediendo el disco cuando no hay prisa");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  OK adelantoprioridad: " + oks + " comprobaciones" : "  FALLO adelantoprioridad: " + fallos + " de " + (oks + fallos));
        return fallos;
    }
}
