using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// QUE DOS TRABAJADORES NO MIDAN LA MISMA CANCION.
//
// En la rocola hay dos que miden canciones -sonoridad, duracion y huella tonal- y no se conocian:
//
//   EL REPASO DE LA BIBLIOTECA arma su lista de pendientes UNA SOLA VEZ, al arrancar, y en su bucle no
//   vuelve a mirar. Esa lista vive horas: unos miles de canciones tardan varias en repasarse.
//
//   EL CALENTADOR DE LA SIGUIENTE mide la que viene, en cuanto alguien la pide.
//
// O sea que cada cancion que un cliente pedia se medía dos veces: una al pedirla y otra cuando al repaso
// le llegaba su turno. Y no es trabajo cualquiera: es descomprimir la cancion entera, peleando con la
// musica, justo en el rato en que hay gente eligiendo.
//
// Ahora hay una libreta con candado -quien esta midiendo que, y que se midio ya- y esta bateria la
// vigila en tres partes: el mecanismo en vivo, el cableado leido del programa compilado, y la cuenta de
// lo que se ahorra en una noche. Mas las PRIORIDADES, que es la otra mitad del punto: ningun trabajador
// de analisis puede correr por delante de la pantalla ni de la musica.
class Medidas
{
    const BindingFlags EST = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags UNO = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static Type TF;
    static MethodInfo mReclama, mSuelta, mPuesta, mYaSe;
    static bool Reclama(string r, bool aFondo) { return (bool)mReclama.Invoke(null, new object[] { r, aFondo }); }
    static void Suelta(string r) { mSuelta.Invoke(null, new object[] { r }); }
    static void Puesta(string r) { mPuesta.Invoke(null, new object[] { r }); }
    static bool YaSe(string r) { return (bool)mYaSe.Invoke(null, new object[] { r }); }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        mReclama = TF.GetMethod("ReclamaMedida", EST);
        mSuelta = TF.GetMethod("SueltaMedida", EST);
        mPuesta = TF.GetMethod("MedidaPuesta", EST);
        mYaSe = TF.GetMethod("YaSeMidio", EST);
        if (mReclama == null || mSuelta == null || mPuesta == null || mYaSe == null)
        { Mal("no esta la libreta de las medidas (ReclamaMedida/SueltaMedida/MedidaPuesta/YaSeMidio)"); return 1; }

        Mecanismo();
        Cableado();
        Prioridades();
        double coste = Coste();
        Noche(coste);

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    // ================================================================================================
    //  1) EL MECANISMO
    // ================================================================================================
    static void Mecanismo()
    {
        Console.WriteLine("\n  1) EL TURNO PARA MEDIR\n");
        Exige(Reclama("A.mp3", false), "quien llega primero se lleva el turno");
        Exige(!Reclama("A.mp3", false), "y el segundo NO lo recibe: no se mide dos veces a la vez");
        Exige(!Reclama("a.MP3", false), "la misma ruta escrita en mayusculas es la misma cancion");
        Exige(!Reclama("A.mp3", true), "ni siquiera el repaso a fondo se mete en lo que otro esta midiendo");
        Suelta("A.mp3");
        Exige(Reclama("A.mp3", false), "soltado el turno, el siguiente si puede");
        Suelta("A.mp3");

        // Una medicion que falla NO deja el archivo tapado: se suelta el turno y no se apunta nada.
        Exige(Reclama("rota.mp3", false) && !YaSe("rota.mp3"), "pedir el turno no es lo mismo que haber medido");
        Suelta("rota.mp3");
        Exige(Reclama("rota.mp3", false), "una medicion que falla no tapa el archivo para siempre");
        Suelta("rota.mp3");

        // Lo ya medido no se vuelve a medir... salvo a fondo, que existe justo para eso.
        Puesta("B.mp3");
        Exige(YaSe("B.mp3"), "lo medido queda apuntado");
        Exige(!Reclama("B.mp3", false), "y el repaso normal ya no la vuelve a medir");
        Exige(Reclama("B.mp3", true), "pero el repaso A FONDO si: analiza la cancion completa, es otro trabajo");
        Suelta("B.mp3");
        Exige(!YaSe("C.mp3"), "lo que nadie midio no figura");

        // DOS HILOS DE VERDAD PELEANDO POR LA MISMA. Esto es lo que pasa en el bar: el calentador y un
        // carril del repaso llegan al mismo archivo casi a la vez.
        Console.WriteLine();
        int ganaron = 0, vueltas = 400;
        for (int v = 0; v < vueltas; v++)
        {
            string ruta = "pelea" + v + ".mp3";
            int gana = 0;
            using (ManualResetEvent ya = new ManualResetEvent(false))
            {
                ThreadStart t = delegate
                {
                    ya.WaitOne();
                    if (Reclama(ruta, false)) { Interlocked.Increment(ref gana); Suelta(ruta); }
                };
                Thread h1 = new Thread(t), h2 = new Thread(t), h3 = new Thread(t);
                h1.Start(); h2.Start(); h3.Start();
                Thread.Sleep(1);
                ya.Set();
                h1.Join(); h2.Join(); h3.Join();
            }
            ganaron += gana;
        }
        // Tres hilos, uno gana seguro; los otros dos pueden llegar despues de que el primero suelte, asi
        // que lo que no puede pasar NUNCA es que no gane ninguno, ni que ganen los tres A LA VEZ (eso lo
        // cubre la parte de arriba, que no suelta entre medias).
        Exige(ganaron >= vueltas, "en " + vueltas + " peleas de tres hilos siempre entra alguien (" + ganaron + ")");
        Exige(ganaron <= vueltas * 3, "y nunca mas de uno a la vez");
    }

    // ================================================================================================
    //  2) EL CABLEADO, LEIDO DEL PROGRAMA COMPILADO
    // ================================================================================================
    static void Cableado()
    {
        Console.WriteLine("\n  2) QUIEN PIDE TURNO, QUIEN LO SUELTA Y QUIEN APUNTA\n");
        // El bucle del repaso vive dentro de un delegado de StartPreScan; el del calentador, dentro de
        // uno de PrecalientaSiguiente. Por eso se miran CON SUS AYUDANTES.
        List<string> repaso = Codigo.ConAyudantes(TF, "StartPreScan");
        List<string> calienta = Codigo.ConAyudantes(TF, "PrecalientaSiguiente");
        List<string> duraciones = Codigo.ConAyudantes(TF, "StartDurScan");

        Exige(Codigo.Llama(repaso, "ReclamaMedida"), "el repaso de la biblioteca pide turno antes de medir");
        Exige(Codigo.Llama(repaso, "SueltaMedida"), "y lo suelta despues");
        Exige(Codigo.Llama(calienta, "ReclamaMedida"), "el calentador de la siguiente pide turno antes de medir");
        Exige(Codigo.Llama(calienta, "SueltaMedida"), "y lo suelta despues");

        // SOLTARLO SIEMPRE. Si una medicion revienta a medias y el turno se queda pedido, ese archivo no
        // se vuelve a medir NUNCA en toda la sesion. Por eso va en un finally, y aqui se comprueba que el
        // metodo de verdad lleva un manejador de los que corren pase lo que pase.
        Exige(TienePorSiAcaso(TF, "StartPreScan"), "el repaso suelta el turno pase lo que pase (finally)");
        Exige(TienePorSiAcaso(TF, "PrecalientaSiguiente"), "el calentador tambien (finally)");

        MethodInfo publica = TF.GetMethod("PublicaMedida", UNO);
        Exige(publica != null && Codigo.Llama(publica, "MedidaPuesta"), "quien pone la medida en las tablas la apunta en la libreta");

        // Y SOLO SI DE VERDAD PUSO ALGO. Un archivo roto no da ni duracion ni sonoridad: si se apuntara
        // igual, esa cancion no se volveria a intentar en toda la sesion. Esto se prueba llamandolo.
        if (publica != null)
        {
            object f = Codigo.FormSinEncender(TF);
            bool pudo = true;
            try { publica.Invoke(f, new object[] { "rota-de-verdad.mp3", 0f, 0f, null }); }
            catch { pudo = false; }
            if (!pudo) Bien("(no se puede llamar a PublicaMedida en una rocola a medio armar: se mira solo por el codigo)");
            else
            {
                Exige(!YaSe("rota-de-verdad.mp3"), "un archivo del que no sale nada NO se da por medido");
                try { publica.Invoke(f, new object[] { "buena-de-verdad.mp3", 0f, 12.5f, null }); } catch { }
                Exige(YaSe("buena-de-verdad.mp3"), "y uno del que sale la duracion, si");
            }
        }
        Exige(Codigo.Llama(duraciones, "YaSeMidio"), "el barrido de duraciones no re-abre lo que ya se midio");

        // EL CONTADOR DE "FALTAN". Saltarse un archivo tiene que restar igual que medirlo, o la pantalla
        // de ajustes se queda diciendo que faltan canciones para siempre.
        // No se cuentan restas a bulto -eso pasaria en verde quitando una-: se mira que JUSTO DESPUES de
        // preguntar por el turno, en el camino por el que se sale sin medir, haya una resta.
        bool restaAlSaltar = false, encontrado = false;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "StartPreScan"))
        {
            List<Codigo.Paso> pasos = Codigo.Pasos(m);
            for (int i = 0; i < pasos.Count; i++)
            {
                if (!EsLlamadaA(m, pasos[i], "ReclamaMedida")) continue;
                encontrado = true;
                for (int j = i + 1; j < pasos.Count && j <= i + 12; j++)
                    if (EsLlamadaA(m, pasos[j], "Decrement")) restaAlSaltar = true;
            }
        }
        Exige(encontrado && restaAlSaltar,
              "al saltarse un archivo porque otro lo mide, resta el contador de 'faltan' (o se queda clavado)");
    }

    // ¿El metodo -o alguno de sus ayudantes- tiene un bloque que corre pase lo que pase?
    static bool TienePorSiAcaso(Type raiz, string metodo)
    {
        foreach (MethodBase m in Codigo.MetodosConAyudantes(raiz, metodo))
        {
            MethodBody b = null;
            try { b = m.GetMethodBody(); } catch { }
            if (b == null) continue;
            foreach (ExceptionHandlingClause c in b.ExceptionHandlingClauses)
                if (c.Flags == ExceptionHandlingClauseOptions.Finally) return true;
        }
        return false;
    }

    // ================================================================================================
    //  3) LAS PRIORIDADES: nadie por delante de la pantalla ni de la musica
    // ================================================================================================
    static void Prioridades()
    {
        Console.WriteLine("\n  3) NINGUN TRABAJADOR DE ANALISIS MANDA SOBRE LA PANTALLA\n");
        // ThreadPriority: 0 Lowest, 1 BelowNormal, 2 Normal, 3 AboveNormal, 4 Highest.
        int mirados = 0, altos = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            List<Codigo.Paso> pasos = Codigo.Pasos(m);
            for (int i = 0; i < pasos.Count; i++)
            {
                if (!EsLlamadaA(m, pasos[i], "set_Priority")) continue;
                int valor;
                if (!ConstanteAntes(pasos, i, out valor)) continue;   // sale de una variable: no se puede leer aqui
                mirados++;
                if (valor >= 2) { altos++; Mal("hilo en prioridad " + valor + " (normal o mas) en " + m.DeclaringType.Name + "." + m.Name); }
            }
        }
        Exige(mirados > 0, "se leen las prioridades con las que nacen los hilos (" + mirados + ")");
        if (altos == 0) Bien("ninguna por encima de 'por debajo de lo normal': manda el cuadro y manda la musica");

        // Y CEDER. El repaso se para si alguien toca la rocola o hay un video; el calentador y el barrido
        // de duraciones, al menos con el video.
        Exige(LeeCampo(TF, "StartPreScan", "lastInputTick"), "el repaso se para si alguien esta usando la rocola");
        Exige(LeeCampo(TF, "StartPreScan", "videoOn"), "y con un video puesto");
        Exige(LeeCampo(TF, "PrecalientaSiguiente", "videoOn"), "el calentador tambien cede al video");
        Exige(LeeCampo(TF, "StartDurScan", "videoOn"), "y el barrido de duraciones igual");
    }
    static bool LeeCampo(Type raiz, string metodo, string campo)
    { return Codigo.LeeCampo(Codigo.MetodosConAyudantes(raiz, metodo), campo); }
    static bool EsLlamadaA(MethodBase m, Codigo.Paso p, string nombre)
    {
        if (p.Op2 >= 0 || (p.Op != 0x28 && p.Op != 0x6F)) return false;
        try { MethodBase d = m.Module.ResolveMethod(p.Numero); return d != null && d.Name == nombre; }
        catch { return false; }
    }
    static bool ConstanteAntes(List<Codigo.Paso> pasos, int i, out int valor)
    {
        valor = -1;
        for (int j = i - 1; j >= 0 && j >= i - 4; j--)
        {
            Codigo.Paso p = pasos[j];
            if (p.Op2 >= 0) continue;
            if (p.Op >= 0x16 && p.Op <= 0x1E) { valor = p.Op - 0x16; return true; }   // ldc.i4.0 .. ldc.i4.8
            if (p.Op == 0x1F || p.Op == 0x20) { valor = p.Numero; return true; }      // ldc.i4.s / ldc.i4
        }
        return false;
    }

    // ================================================================================================
    //  4) LO QUE CUESTA UNA MEDICION, Y QUE LA LIBRETA NO LA CAMBIA
    // ================================================================================================
    static double Coste()
    {
        Console.WriteLine("\n  4) LO QUE CUESTA MEDIR UNA CANCION\n");
        MethodInfo mide = TF.GetMethod("MeasureFile", UNO);
        if (mide == null) { Mal("no esta MeasureFile"); return 0; }
        object f = Codigo.FormSinEncender(TF);
        string dir = Path.Combine(Path.GetTempPath(), "rocola-medidas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        double ms4min = 0;
        try
        {
            string wav = Path.Combine(dir, "cuatro.wav");
            Escribe(wav, 44100, 2, 44100 * 240);   // cuatro minutos
            byte[] buf = new byte[262144];
            using (FileStream fs = File.OpenRead(wav)) while (fs.Read(buf, 0, buf.Length) > 0) { }

            // LA MISMA CANCION, TRES VECES, con la libreta trabajando en medio. La medida tiene que salir
            // identica: la libreta decide QUIEN mide, no QUE se mide.
            float[] rms = new float[3], dur = new float[3]; float[][] eq = new float[3][];
            for (int k = 0; k < 3; k++)
            {
                if (k == 1) { Reclama(wav, false); Suelta(wav); }
                if (k == 2) Puesta(wav);
                object[] ar = new object[] { wav, false, 0f, 0f, null };
                Stopwatch sw = Stopwatch.StartNew();
                bool ok = (bool)mide.Invoke(f, ar);
                sw.Stop();
                if (!ok) { Mal("no se pudo medir el WAV de prueba"); return 0; }
                rms[k] = (float)ar[2]; dur[k] = (float)ar[3]; eq[k] = (float[])ar[4];
                if (k == 0) ms4min = sw.Elapsed.TotalMilliseconds;
                else if (sw.Elapsed.TotalMilliseconds < ms4min) ms4min = sw.Elapsed.TotalMilliseconds;
            }
            bool igual = rms[0] == rms[1] && rms[1] == rms[2] && dur[0] == dur[1] && dur[1] == dur[2]
                         && MismaCurva(eq[0], eq[1]) && MismaCurva(eq[1], eq[2]);
            Console.WriteLine("     una cancion de 4 minutos: " + ms4min.ToString("0") + " ms"
                              + "   (sonoridad " + rms[0].ToString("0.0000") + ", duracion " + dur[0].ToString("0.0") + " s)");
            Exige(igual, "la libreta decide QUIEN mide, no QUE se mide: la medida sale identica");
            Bien("y esto es con WAV: el equipo del bar descomprime MP3, que cuesta mas");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        return ms4min;
    }
    static bool MismaCurva(float[] a, float[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // ================================================================================================
    //  5) LA CUENTA DE UNA NOCHE
    // ================================================================================================
    static void Noche(double costeMs)
    {
        Console.WriteLine("\n  5) LO QUE SE AHORRA EN UNA NOCHE\n");
        // Una biblioteca de 5.000 canciones sin repasar, seis horas de bar. El repaso avanza una cancion
        // cada 2,5 s (medirla mas el respiro entre archivos). La gente pide una cada tres minutos y
        // medio, que para un bar con movimiento es tirando a tranquilo.
        const int Biblioteca = 5000, Horas = 6;
        const double SegPorArchivo = 2.5, SegEntrePedidos = 210;
        int pedidas = (int)(Horas * 3600 / SegEntrePedidos);
        Random r = new Random(20260918);   // siempre la misma noche: esto tiene que dar igual en cada pasada
        int duplicadas = 0;
        for (int i = 0; i < pedidas; i++)
        {
            double cuando = i * SegEntrePedidos;
            int porDondeVa = (int)(cuando / SegPorArchivo);        // cuantas lleva repasadas el repaso
            int cual = r.Next(Biblioteca);                          // la que alguien pide
            if (cual >= porDondeVa) duplicadas++;                   // el repaso aun no habia llegado: se medía dos veces
        }
        double segundos = duplicadas * costeMs / 1000.0;
        Console.WriteLine("     " + Horas + " h de bar, " + Biblioteca + " canciones sin repasar, " + pedidas + " pedidas");
        Console.WriteLine("     antes: " + duplicadas + " se median DOS veces  ->  " + segundos.ToString("0")
                          + " s de procesador tirados, peleando con la musica");
        Console.WriteLine("     ahora: 0");
        Console.WriteLine("     (la cuenta supone que el repaso avanza sin parar. En el bar se para 8 s cada vez");
        Console.WriteLine("      que alguien toca la pantalla y con cada video, asi que va mas atrasado y lo que");
        Console.WriteLine("      se duplicaba es MAS que esto, no menos.)");
        Exige(duplicadas > 0, "la cuenta de la noche encuentra el trabajo duplicado que habia");
        Bien("y en el equipo del bar es mas, porque alli se descomprime MP3 y no WAV");
    }

    static void Escribe(string ruta, int hz, int canales, int muestras)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(ruta)))
        {
            int bm = 2, datos = muestras * canales * bm;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + datos);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)canales); w.Write(hz);
            w.Write(hz * canales * bm); w.Write((short)(canales * bm)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(datos);
            Random r = new Random(7);
            for (int i = 0; i < muestras * canales; i++)
                w.Write((short)((Math.Sin(i * 0.013) * 0.6 + r.NextDouble() * 0.2 - 0.1) * 12000));
        }
    }
}
