using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NAudio.Wave;

// CON DSEE ULTRA ENCENDIDO, EL DSEE NORMAL TIENE QUE ATENDER A LAS QUE APOLLO AUN NO HA HECHO.
//
// La rocola tiene dos niveles de mejora y estan bien pensados: con ULTRA encendido, la cancion que Apollo
// ya dejo lista suena mejorada (letrero DORADO), y la que todavia no, pasa por el DSEE normal mientras
// tanto (letrero PLATEADO). Eso lo dice el propio programa donde vive iaSpectralFallback.
//
// Lo que fallaba: UNA SOLA VARIABLE, sbrOn, hacia DOS trabajos distintos -"el usuario encendio el
// interruptor" y "el reconstructor esta funcionando ahora"- y de ahi salian tres averias encadenadas:
//
//   a) Encender ULTRA ponia sbrOn en false Y LO GUARDABA en la configuracion. Al apagar ULTRA, el
//      interruptor del DSEE seguia desmarcado: se perdia la preferencia PARA SIEMPRE.
//   b) La marca del respaldo solo se calculaba al empezar la cancion, asi que tocar el interruptor a mitad
//      de cancion apagaba el DSEE de esa cancion sin que nada lo volviera a encender.
//   c) Y el realce de brillo, que se baja a la mitad cuando el reconstructor va a recrear los agudos para
//      no empujar dos veces lo mismo, solo miraba sbrOn: con ULTRA encendido esa proteccion no se aplicaba
//      y sonaba mas aspero de lo que debe.
//
// El arreglo separa los dos significados: sbrOn vuelve a ser SOLO la preferencia, y una regla nueva
// -DseeActivo()- contesta si el reconstructor corre ahora. Aqui se comprueba la regla, que la preguntan
// todos los que la necesitan, y que el resultado se OYE (el realce) y se VE (el letrero plateado).
class Respaldo
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic;
    const int FS = 44100;

    static Type TF, TSbr, TItem, TAlb, TTrk;

    // Una fuente de sonido de mentira (la misma idea que en 'dsee').
    sealed class FuenteD : ISampleProvider
    {
        private readonly float[] d; private int pos; private readonly WaveFormat wf;
        public FuenteD(float[] datos, int rate, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(rate, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int off, int cnt)
        {
            int n = Math.Min(cnt, d.Length - pos);
            for (int i = 0; i < n; i++) b[off + i] = d[pos + i];
            pos += n; return n;
        }
    }

    static object CreaTap(ISampleProvider fu)
    {
        foreach (ConstructorInfo ci in TSbr.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length < 1 || !ps[0].ParameterType.IsInstanceOfType(fu)) continue;
            object[] full = new object[ps.Length];
            full[0] = fu;
            bool encaja = true;
            for (int i = 1; i < ps.Length; i++) { if (ps[i].ParameterType != typeof(int)) { encaja = false; break; } full[i] = 2048; }
            if (encaja) { try { return ci.Invoke(full); } catch { } }
        }
        return null;
    }

    // Musica de mentira con un ACANTILADO a 16 kHz, que es lo que deja un MP3 de 128k. Sin acantilado el
    // reconstructor no tiene nada que recrear y no enciende el letrero (y hace bien).
    static float[] Cortada(int muestras, int semilla)
    {
        float[] d = new float[muestras];
        Random r = new Random(semilla);
        double[] fase = new double[40];
        for (int k = 0; k < 40; k++) fase[k] = r.NextDouble() * 6.283;
        for (int i = 0; i < muestras; i++)
        {
            double t = (double)i / FS, v = 0;
            for (int k = 0; k < 40; k++)
            {
                double hz = 55.0 * Math.Pow(1.18, k);
                if (hz > 16000) break;
                v += Math.Sin(2 * Math.PI * hz * t + fase[k]) * (0.9 / (1.0 + hz / 300.0));
            }
            float s = (float)(v * 0.3);
            d[i] = (s > 1f) ? 1f : (s < -1f ? -1f : s);
        }
        return d;
    }

    static float[] Dos(float[] mono)
    {
        float[] d = new float[mono.Length * 2];
        for (int i = 0; i < mono.Length; i++) { d[i * 2] = mono[i]; d[i * 2 + 1] = mono[i]; }
        return d;
    }

    // Hace sonar la musica entera por un tap y lo devuelve, ya con su acantilado medido.
    static object PasaTodo(float[] entrelazado, int ch)
    {
        object tap = CreaTap(new FuenteD(entrelazado, FS, ch));
        if (tap == null) return null;
        TSbr.GetField("Active").SetValue(tap, true);
        ISampleProvider p = (ISampleProvider)tap;
        float[] buf = new float[4096];
        int n = entrelazado.Length, hecho = 0;
        while (hecho < n) { int got = p.Read(buf, 0, Math.Min(buf.Length, n - hecho)); if (got <= 0) break; hecho += got; }
        return tap;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SpectralTap") TSbr = t;
            else if (t.Name == "PlayItem") TItem = t;
            else if (t.Name == "Album") TAlb = t;
            else if (t.Name == "Track") TTrk = t;
        }
        FieldInfo fAll = TF.GetField("sbrAllowed", IF), fOn = TF.GetField("sbrOn", IF);
        FieldInfo fVivo = TF.GetField("iaVivo", IF), fResp = TF.GetField("iaSpectralFallback", IF);
        MethodInfo mRegla = TF.GetMethod("DseeActivo", IF);
        Check(TSbr != null && fAll != null && fOn != null && fVivo != null && fResp != null,
              "estan el reconstructor y los cuatro datos de los que depende");
        if (TSbr == null || fVivo == null) { Console.WriteLine("\n  (esta rama no lleva DSEE ULTRA)\n"); return fallos; }

        // ---------- 1) LA REGLA ----------
        Console.WriteLine("\n  1) UNA SOLA REGLA CONTESTA SI EL RECONSTRUCTOR CORRE AHORA\n");
        Check(mRegla != null, "existe la regla (DseeActivo), aparte del interruptor del usuario");

        object F = FormatterServices.GetUninitializedObject(TF);
        if (mRegla != null)
        {
        Console.WriteLine(string.Format("     {0,-8} {1,-12} {2,-10} {3,-9} | {4}", "maquina", "interruptor", "ULTRA", "respaldo", "corre?"));
        Console.WriteLine("     " + new string('-', 62));
        int malos = 0;
        // maquina, interruptor, ULTRA, respaldo, lo que TIENE que contestar
        bool[][] casos = new bool[][] {
            new bool[] { false, true,  false, false, false },   // la maquina no aguanta: nunca
            new bool[] { false, true,  true,  true,  false },
            new bool[] { true,  false, false, false, false },   // ULTRA apagado: manda el interruptor
            new bool[] { true,  true,  false, false, true  },
            new bool[] { true,  false, false, true,  false },   // ...y el respaldo de ULTRA no pinta nada
            new bool[] { true,  false, true,  true,  true  },   // ULTRA encendido: manda el respaldo
            new bool[] { true,  true,  true,  false, false },   // suena la version de Apollo: NO se empuja encima
            new bool[] { true,  false, true,  false, false },
        };
        foreach (bool[] c in casos)
        {
            fAll.SetValue(F, c[0]); fOn.SetValue(F, c[1]); fVivo.SetValue(F, c[2]); fResp.SetValue(F, c[3]);
            bool dio = (bool)mRegla.Invoke(F, null);
            if (dio != c[4]) malos++;
            Console.WriteLine(string.Format("     {0,-8} {1,-12} {2,-10} {3,-9} | {4,-5} {5}",
                              c[0] ? "apta" : "no apta", c[1] ? "encendido" : "apagado", c[2] ? "encendido" : "apagado",
                              c[3] ? "si" : "no", dio ? "SI" : "no", (dio == c[4]) ? "" : "  <-- TENIA QUE SER " + (c[4] ? "SI" : "no")));
        }
        Check(malos == 0, "la regla contesta bien en los ocho casos");
        }

        // ---------- 2) TU PREFERENCIA NO SE BORRA ----------
        Console.WriteLine("\n  2) ENCENDER ULTRA NO TE BORRA EL INTERRUPTOR DEL DSEE\n");
        // TODAS las partes de Form1, no solo Form1.cs: la clase esta repartida por temas.
        string[] src = Codigo.LineasDeForm1();
        Check(src.Length > 1000, "se puede leer el codigo de la rocola entero (" + src.Length + " lineas)");
        List<string> conUltra = new List<string>();
        foreach (string l in src)
        {
            int i = l.IndexOf("sbrOn =");
            if (i < 0) continue;
            string trozo = l.Substring(i);
            int com = trozo.IndexOf("//"); if (com >= 0) trozo = trozo.Substring(0, com);
            if (trozo.IndexOf("ultra", StringComparison.OrdinalIgnoreCase) >= 0) conUltra.Add(trozo.Trim());
        }
        foreach (string l in conUltra) Console.WriteLine("     lo apaga aqui:  " + l);
        Check(conUltra.Count == 0, "la preferencia del DSEE ya no se pisa al encender ULTRA (se guardaba apagada y no volvia)");

        // ---------- 3) TODOS LOS QUE LA NECESITAN, LA PREGUNTAN ----------
        Console.WriteLine("\n  3) Y TODOS LOS QUE LA NECESITAN PREGUNTAN LA REGLA\n");
        string[] quienes = { "ApplyNormNow", "PlayTrack", "MejKind", "ExciteFactorFor" };
        string[] paraQue = { "el cambio se oye al instante", "la cancion arranca con el reconstructor bien puesto",
                             "el letrero plateado", "el realce no empuja dos veces" };
        int sinPreguntar = 0;
        for (int i = 0; i < quienes.Length; i++)
        {
            MethodInfo m = TF.GetMethod(quienes[i], IF);
            bool ok = (m != null) && Codigo.Llama(m, "DseeActivo");
            if (!ok) sinPreguntar++;
            Console.WriteLine("     " + (ok ? "si " : "NO ") + " " + quienes[i].PadRight(16) + "(" + paraQue[i] + ")");
        }
        Check(sinPreguntar == 0, "los cuatro preguntan la misma regla en vez de mirar el interruptor por su cuenta");

        // ---------- 4) TOCAR ULTRA RECALCULA EL RESPALDO ----------
        Console.WriteLine("\n  4) TOCAR EL INTERRUPTOR A MITAD DE CANCION RECALCULA EL RESPALDO\n");
        MethodInfo mRev = TF.GetMethod("RevisaRespaldoDsee", IF);
        Check(mRev != null, "la decision del respaldo se puede volver a tomar sin cambiar de cancion");
        int lallaman = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) if (Codigo.Llama(m, "RevisaRespaldoDsee")) lallaman++;
        Console.WriteLine("     sitios que la vuelven a tomar: " + lallaman + "  (al empezar la cancion, y al tocar el interruptor)");
        Check(lallaman >= 2, "y se vuelve a tomar tambien al tocar el interruptor, no solo al cambiar de cancion");

        // ---------- 5) SE OYE: EL REALCE NO EMPUJA DOS VECES ----------
        Console.WriteLine("\n  5) SE OYE: CON ULTRA, EL REALCE TAMPOCO EMPUJA DOS VECES\n");
        FieldInfo fEq = TF.GetField("eqCache", IF);
        MethodInfo mExc = TF.GetMethod("ExciteFactorFor", IF);
        FieldInfo fCut = TF.GetField("EqCut", SF), fLen = TF.GetField("EqLen", SF);
        if (fEq == null || mExc == null || fCut == null || fLen == null)
            Check(false, "se puede consultar cuanto realce le toca a cada cancion");
        else
        {
            int iCut = (int)fCut.GetValue(null), len = (int)fLen.GetValue(null);
            Dictionary<string, float[]> cache = new Dictionary<string, float[]>();
            fEq.SetValue(F, cache); fAll.SetValue(F, true);
            float[] c = new float[len];
            for (int i = 0; i < len; i++) c[i] = -30f;
            c[iCut] = 15000f;                       // una cancion cortada a 15 kHz: de las mas dañadas
            cache["danada.mp3"] = c;
            fVivo.SetValue(F, false); fOn.SetValue(F, false); fResp.SetValue(F, false);
            float aPelo = (float)mExc.Invoke(F, new object[] { "danada.mp3" });
            fVivo.SetValue(F, false); fOn.SetValue(F, true); fResp.SetValue(F, false);
            float conDsee = (float)mExc.Invoke(F, new object[] { "danada.mp3" });
            fVivo.SetValue(F, true); fOn.SetValue(F, false); fResp.SetValue(F, true);
            float conUltraResp = (float)mExc.Invoke(F, new object[] { "danada.mp3" });
            Console.WriteLine("     realce que le toca a una cancion cortada a 15000 Hz:");
            Console.WriteLine("        sin reconstructor ................ " + aPelo.ToString("0.00"));
            Console.WriteLine("        con el DSEE normal ............... " + conDsee.ToString("0.00"));
            Console.WriteLine("        con ULTRA, atendida por el DSEE .. " + conUltraResp.ToString("0.00"));
            Check(conDsee < aPelo, "con el DSEE normal encendido recibe menos realce (los agudos ya se los devolvio el)");
            Check(Math.Abs(conUltraResp - conDsee) < 0.001f, "y con ULTRA, la que atiende el DSEE recibe EXACTAMENTE lo mismo");
        }

        // ---------- 6) SE VE: EL LETRERO PLATEADO ----------
        Console.WriteLine("\n  6) SE VE: EL LETRERO PLATEADO SALE EN LA QUE ATIENDE EL DSEE\n");
        MethodInfo mMej = TF.GetMethod("MejKind", IF);
        FieldInfo fCur = TF.GetField("curSbr", IF), fNow = TF.GetField("nowPlaying", IF), fEnh = TF.GetField("iaNowEnhanced", IF);
        if (mMej == null || fCur == null || fNow == null || TItem == null)
            Check(false, "se puede preguntar que letrero toca");
        else
        {
            object item = Activator.CreateInstance(TItem, true);
            TItem.GetField("IsVideo").SetValue(item, false);
            if (TTrk != null)
            {
                object tr = Activator.CreateInstance(TTrk, true);
                TTrk.GetField("Path").SetValue(tr, "/musica/danada.mp3");
                TItem.GetField("Song").SetValue(item, tr);
            }
            if (TAlb != null) TItem.GetField("Disc").SetValue(item, Activator.CreateInstance(TAlb, true));
            fNow.SetValue(F, item);
            if (fEnh != null) fEnh.SetValue(F, false);

            object tapPobre = PasaTodo(Dos(Cortada(FS, 3)), 2);
            Check(tapPobre != null, "se puede hacer sonar una cancion por el reconstructor");
            if (tapPobre != null)
            {
                bool mejorando = (bool)TSbr.GetProperty("Mejorando").GetValue(tapPobre, null);
                Console.WriteLine("     la cancion de prueba corta a 16000 Hz;  el reconstructor dice que la esta mejorando: " + (mejorando ? "si" : "no"));
                fCur.SetValue(F, tapPobre);
                fAll.SetValue(F, true); fVivo.SetValue(F, true); fOn.SetValue(F, false); fResp.SetValue(F, true);
                int conUltra2 = (int)mMej.Invoke(F, null);
                fVivo.SetValue(F, true); fResp.SetValue(F, false);   // esta ya la mejoro Apollo
                int deApollo = (int)mMej.Invoke(F, null);
                if (fEnh != null) { fEnh.SetValue(F, true); }
                int dorado = (int)mMej.Invoke(F, null);
                Console.WriteLine("     letrero con ULTRA y la cancion aun sin hacer: " + Nombre(conUltra2));
                Console.WriteLine("     letrero con ULTRA y la version de Apollo:     " + Nombre(dorado));
                Check(mejorando, "el reconstructor reconoce que le falta banda a la cancion");
                Check(conUltra2 == 1, "con ULTRA encendido, la cancion que Apollo aun no ha hecho sale con el letrero PLATEADO");
                Check(deApollo == 0, "y la que suena por la version de Apollo no lleva letrero plateado encima");
                Check(dorado == 2, "esa lleva el DORADO, que es el suyo");
            }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static string Nombre(int k) { return (k == 2) ? "DORADO" : (k == 1 ? "PLATEADO" : "ninguno"); }
}
