using System;
using System.Reflection;

// LA PARED QUE RESPIRA, Y SU FRENO.
//
// La pared de discos sube y baja de brillo muy despacio, una respiracion cada medio minuto. Es lo que
// hace que una pantalla quieta parezca encendida en vez de colgada.
//
// Y TIENE FRENO, que es la parte importante y la que se pidio: mientras alguien esta usando la rocola,
// la pared NO respira. Una pantalla enorme moviendose despacio delante de quien esta eligiendo cancion
// no es bonita, es mareante.
//
// PERO FRENAR NO ES PARARSE EN SECO. Si la respiracion se cortara de golpe, la pared se quedaria
// clavada en el brillo que le tocara en ese instante -a veces mas oscura, a veces mas clara que lo
// normal- y eso SI se nota, porque queda asi hasta que alguien vuelva a soltar el boton. Lo que se hace
// es quitarle el AIRE: la fuerza baja a cero mientras el ciclo sigue corriendo por dentro, asi que la
// pared siempre acaba exactamente en su brillo de siempre.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE SE PUEDA COMPROBAR. La respiracion es una cuenta pura: se le dan numeros y devuelve el
//      brillo. Metida dentro del dibujo no habria forma de saber si frena o no.
//   2) QUE CON ALGUIEN TOCANDO LA PARED ESTE QUIETA, y quieta en su brillo normal.
//   3) QUE EL TOPE SE RESPETE aunque en el archivo de configuracion se ponga un disparate.
//   4) QUE FRENE DEPRISA Y VUELVA DESPACIO. Parar tiene que notarse poco; arrancar, nada.
//   5) QUE EL CICLO SEA LENTO DE VERDAD (medio minuto), no un latido.
//   6) QUE LA AURORA DEL HORIZONTE, QUE YA RESPIRABA DESDE ANTES, FRENE IGUAL. Es exactamente la
//      misma queja, y hoy no frenaba con nadie.
class Respira
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;

    static MethodBase Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static float Cte(string n)
    {
        FieldInfo f = TF.GetField(n, TODO);
        return (f == null) ? float.NaN : Convert.ToSingle(f.GetValue(null));
    }

    static MethodInfo mResp, mPaso, mFuerza;
    static float Resp(float fuerza, float quietud, float fase)
    { return Convert.ToSingle(mResp.Invoke(null, new object[] { fuerza, quietud, fase })); }
    static float Paso(float quietud, bool tocando)
    { return Convert.ToSingle(mPaso.Invoke(null, new object[] { quietud, tocando })); }
    static float Fuerza(string txt)
    { return Convert.ToSingle(mFuerza.Invoke(null, new object[] { txt })); }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS, Y QUE SE PUEDAN COMPROBAR\n");
        MethodBase r = Met("Respiracion"), p = Met("PasoQuietud"), f = Met("FuerzaRespira");
        Check(r != null, "el brillo de la respiracion es una cuenta aparte (Respiracion)");
        Check(p != null, "y el freno, otra (PasoQuietud)");
        Check(f != null, "y lo que se le deja respirar sale del archivo por un solo sitio (FuerzaRespira)");
        if (r == null || p == null || f == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return fallos; }
        Check(r.IsStatic && p.IsStatic && f.IsStatic, "las tres son puras: se les pueden dar casos a mano");
        mResp = (MethodInfo)r; mPaso = (MethodInfo)p; mFuerza = (MethodInfo)f;

        Console.WriteLine("\n  2) CON ALGUIEN TOCANDO, LA PARED NO SE MUEVE\n");
        bool quieta = true;
        for (int i = 0; i < 64; i++)
        {
            float fase = (float)(i * 6.2831853 / 64);
            if (Resp(0.025f, 0f, fase) != 1f) quieta = false;
        }
        Check(quieta, "con quietud 0 la pared vale su brillo normal EN TODO EL CICLO, no solo en un punto");
        Check(Resp(0f, 1f, 1.2f) == 1f, "y con la respiracion apagada, tampoco se mueve");

        Console.WriteLine("\n  3) Y SOLA, RESPIRA\n");
        float alto = Resp(0.025f, 1f, (float)(Math.PI / 2)), bajo = Resp(0.025f, 1f, (float)(-Math.PI / 2));
        Console.WriteLine("     con el 2.5% de fuerza: del " + (bajo * 100).ToString("0.0") + "% al " + (alto * 100).ToString("0.0") + "% del brillo normal");
        Check(alto > 1.02f && alto < 1.03f, "arriba sube un 2.5%");
        Check(bajo < 0.98f && bajo > 0.97f, "y abajo baja otro tanto");

        Console.WriteLine("\n  4) EL TOPE, AUNQUE EL ARCHIVO PIDA UN DISPARATE\n");
        float tope = Cte("RespiraTope");
        Console.WriteLine("     el tope es " + (tope * 100).ToString("0.0") + "%");
        Check(tope > 0.001f && tope <= 0.08f, "hay un tope, y es pequeno");
        Console.WriteLine("     respira_fuerza=500  ->  " + (Fuerza("500") * 100).ToString("0.0") + "%");
        Console.WriteLine("     respira_fuerza=-3   ->  " + (Fuerza("-3") * 100).ToString("0.0") + "%");
        Console.WriteLine("     respira_fuerza=2.5  ->  " + (Fuerza("2.5") * 100).ToString("0.0") + "%");
        Check(Fuerza("500") <= tope + 1e-6f, "poner 500 en el archivo no marea a nadie: se queda en el tope");
        Check(Fuerza("-3") >= 0f, "y un numero negativo no da la vuelta a la respiracion");
        Check(Math.Abs(Fuerza("2.5") - 0.025f) < 1e-6f, "un 2.5 en el archivo es un 2.5%");
        Check(Fuerza("hola") > 0f, "y una linea sin sentido no apaga la pared: se queda como de fabrica");
        // Y NO HAY OTRA PUERTA. La cuenta de la respiracion no lleva tope propio -la comparte con la
        // aurora, que respira con mucha mas fuerza desde antes y no hay que recortarsela-, asi que la
        // garantia tiene que ser que a la pared no se le pueda poner fuerza por ningun otro lado.
        int sitios = 0; bool porAhi = true;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            if (m.Name.Length > 0 && m.Name[0] == '.') continue;   // el valor de fabrica, una constante escrita ahi mismo
            int lee, esc;
            Codigo.UsosDeCampo(m, "respiraFuerza", out lee, out esc);
            if (esc > 0) { sitios++; if (!Codigo.Llama(m, "FuerzaRespira")) porAhi = false; }
        }
        Console.WriteLine("     a la pared se le pone fuerza en " + sitios + " sitio(s)");
        Check(sitios == 1 && porAhi, "y solo se le pone fuerza por ese sitio, que es el que acota");

        Console.WriteLine("\n  5) FRENA DEPRISA, VUELVE DESPACIO\n");
        // A 24 cuadros por segundo.
        float q = 1f; int nBaja = 0;
        while (q > 0f && nBaja < 400) { q = Paso(q, true); nBaja++; }
        float q2 = 0f; int nSube = 0;
        while (q2 < 1f && nSube < 400) { q2 = Paso(q2, false); nSube++; }
        Console.WriteLine("     frenar del todo: " + nBaja + " cuadros (" + (nBaja / 24f).ToString("0.0") + " s)");
        Console.WriteLine("     volver del todo: " + nSube + " cuadros (" + (nSube / 24f).ToString("0.0") + " s)");
        Check(q == 0f, "frenando se llega a CERO exacto, no a un pelin de movimiento que no acaba nunca");
        Check(nBaja <= 24, "y se frena en menos de un segundo");
        Check(nSube > nBaja * 2, "volver a respirar tarda bastante mas que parar: arrancar no se nota");
        // LA IMPORTANTE, Y ES LA QUE SE PIDIO: al frenar, la pared acaba en su brillo NORMAL y no
        // clavada a media respiracion, sea cual sea el punto del ciclo en que alguien toco el boton.
        bool acaba = true;
        for (int i = 0; i < 64; i++)
        {
            float fase = (float)(i * 6.2831853 / 64);
            float qq = 1f;
            for (int k = 0; k < nBaja; k++) qq = Paso(qq, true);
            if (Resp(0.025f, qq, fase) != 1f) acaba = false;
        }
        Check(acaba, "y al frenar la pared queda en su brillo de siempre, no clavada a media respiracion");

        Console.WriteLine("\n  6) UNA RESPIRACION CADA MEDIO MINUTO, NO UN LATIDO\n");
        float paso = Cte("RespiraPaso");
        double segs = (6.2831853 / paso) / 24.0;   // el reloj de la pantalla va a 24 cuadros/s
        Console.WriteLine("     el ciclo entero dura " + segs.ToString("0") + " segundos");
        Check(segs > 20 && segs < 45, "una respiracion cada medio minuto (" + segs.ToString("0") + " s)");

        Console.WriteLine("\n  7) Y CASI NUNCA OBLIGA A REPINTAR\n");
        // El fondo del formulario solo se repinta cuando la atenuacion cambia lo bastante para verse
        // (0.0015). La respiracion mueve el brillo tan despacio que la mayoria de los cuadros no llegan.
        // Esto importa en la maquina del bar, que es la que va justa.
        float ant = -9f; int repinta = 0, cuadros = 0;
        float pasoF = Cte("RespiraPaso");
        for (float fase = 0f; fase < 6.2831853f; fase += pasoF)
        {
            float v = Resp(0.025f, 1f, fase);
            cuadros++;
            if (Math.Abs(v - ant) > 0.0015f) { repinta++; ant = v; }
        }
        Console.WriteLine("     en un ciclo entero de " + cuadros + " cuadros, repinta " + repinta + " veces (1 de cada " + (cuadros / Math.Max(repinta, 1)) + ")");
        Check(repinta * 4 < cuadros, "la respiracion obliga a repintar menos de un cuadro de cada cuatro");
        Check(repinta > 10, "pero las suficientes para que se vea moverse y no a saltos");

        Console.WriteLine("\n  8) LA ROCOLA LA USA, Y LA AURORA FRENA IGUAL\n");
        MethodBase tick = Met("AnimTimer_Tick");
        Check(tick != null && Codigo.Llama(Codigo.ConAyudantes(TF, "AnimTimer_Tick"), "Respiracion"),
              "el reloj de la pantalla la aplica al fondo");
        Check(tick != null && Codigo.Llama(Codigo.ConAyudantes(TF, "AnimTimer_Tick"), "PasoQuietud"),
              "y lleva la cuenta de si hay alguien tocando");
        // La aurora del horizonte respira desde antes cuando no suena nada, y hasta hoy no frenaba con
        // nadie. Es la misma queja: pasa por la misma cuenta, asi que con quietud 0 se queda quieta.
        //
        // La aurora SOLO existe en la version con tarjeta grafica; sin ella no hay nada que frenar. Por eso
        // esta bateria sale en las dos listas: aqui es donde se comprueba la parte que solo vive alli.
        if (TF.GetField("auroraFase", TODO) == null)
            Console.WriteLine("     (esta version no lleva aurora: no hay nada que frenar ahi)");
        else
        {
            bool aurora = false;
            foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            {
                if (m == tick) continue;
                int lee, esc;
                Codigo.UsosDeCampo(m, "auroraFase", out lee, out esc);
                if (lee > 0 && Codigo.Llama(m, "Respiracion")) aurora = true;
            }
            Check(aurora, "la aurora del horizonte, que ya respiraba, frena por la misma cuenta");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
