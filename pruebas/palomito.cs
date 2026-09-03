using System;
using System.Collections.Generic;
using System.Reflection;

// EL PALOMITO DE "YA LA PUSISTE", QUE NO SE VEIA CON TARJETA GRAFICA.
//
// LO QUE PASABA. La marca verde va HORNEADA DENTRO del mapa de bits de cada casilla, no dibujada
// encima. Para que al pedir una cancion se hornee una casilla NUEVA con la marca, su clave de cache
// lleva un "#q" pegado al final. Eso estaba bien puesto en los dos sitios que hornean.
//
// Pero la clave que se le entrega a la TARJETA GRAFICA se armaba en un tercer sitio, y ahi no se le
// pegaba el "#q":
//
//     BuildGpuGrid:   buf[n].Key = CellKey(a);          <- sin marca
//     el panel:       cellTexCache.TryGetValue(c.Key)   <- y cachea las texturas por esa clave
//
// O sea: la rocola horneaba correctamente la casilla nueva CON la marca, se la entregaba al panel, y
// el panel encontraba esa clave ya en su cache de texturas y reutilizaba la vieja, SIN marca. El
// palomito no aparecia nunca y tampoco desaparecia nunca. En las dos maquinas de Windows -que dibujan
// por tarjeta grafica- el adorno entero estaba invisible.
//
// Y EL ARREGLO NO ES PEGAR EL "#q" EN UN TERCER SITIO: asi fue como nacio el fallo. Los tres piden la
// clave a UNA sola funcion que ya la trae con la marca puesta. Eso es lo que se vigila aqui.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE HAYA UN SOLO SITIO que arma la clave de la casilla.
//   2) QUE LOS TRES LO USEN: los dos que hornean y el que se la da a la tarjeta.
//   3) QUE LA MARCA CAMBIE LA CLAVE de verdad.
//   4) Y QUE NO CUESTE. Esa clave se pide para CADA casilla en CADA cuadro: si armara una cadena
//      nueva cada vez, serian dieciocho cadenas sesenta veces por segundo, para siempre.
class Palomito
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TAV, TAlb;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static List<MethodBase> Todos(Type t, string n)
    {
        List<MethodBase> r = new List<MethodBase>();
        if (t == null) return r;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) r.Add(m);
        return r;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "AlbumView") TAV = t;
            if (t.Name == "Album") TAlb = t;
        }
        if (TF == null || TAV == null || TAlb == null) { Console.WriteLine("  FALLO  no se encontro la pared"); return 1; }

        Console.WriteLine("\n  1) UN SOLO SITIO QUE ARMA LA CLAVE\n");
        MethodBase clave = Met(TAV, "ClaveCasilla");
        Check(clave != null, "hay una sola funcion que arma la clave de la casilla (ClaveCasilla)");
        if (clave == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }
        // Y el "#q" se escribe UNA vez en todo el programa: si aparece en dos sitios, ya se pueden
        // desparejar, que es exactamente lo que paso.
        int conMarca = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            foreach (string s in Codigo.Textos(m)) if (s == "#q") { conMarca++; break; }
        Console.WriteLine("     el \"#q\" se escribe en " + conMarca + " sitio(s) del programa");
        Check(conMarca == 1, "y la marca se pega en UN solo sitio: dos ya se pueden desparejar");

        Console.WriteLine("\n  2) Y LOS TRES LO USAN\n");
        // 'PeekOrBakeCell' tiene dos versiones y la corta solo reenvia a la larga, asi que no arma
        // ninguna clave. Lo que se exige es que de cada nombre haya al menos una que la pida, y -esto es
        // lo importante- que NINGUNA se arme la suya por su cuenta con la clave pelada.
        List<MethodBase> horno = Todos(TAV, "GetOrBakeCell");
        horno.AddRange(Todos(TAV, "PeekOrBakeCell"));
        MethodBase gpu = Met(TAV, "BuildGpuGrid");
        int usan = 0, pelada = 0;
        foreach (MethodBase m in horno)
        {
            if (Codigo.Llama(m, "ClaveCasilla")) usan++;
            if (Codigo.Llama(m, "CellKey")) pelada++;
        }
        Console.WriteLine("     de " + horno.Count + " funciones que hornean, " + usan + " piden la clave con marca y " + pelada + " la pelada");
        Check(usan >= 2, "las que hornean la casilla piden la clave ahi");
        Check(pelada == 0, "y ninguna se arma la suya por su cuenta");
        if (gpu == null)
            Console.WriteLine("     (esta version no dibuja por tarjeta grafica: no hay rejilla que entregarle)");
        else
        {
            // ESTE ES EL FALLO QUE SE ARREGLA. La clave que va a la tarjeta tiene que ser LA MISMA con la
            // que se horneo, o el panel reusa la textura vieja y la marca no aparece nunca.
            Check(Codigo.Llama(gpu, "ClaveCasilla"), "y la que se le entrega a la tarjeta grafica es LA MISMA");
            Check(!Codigo.Llama(gpu, "CellKey"), "no la de antes, que iba sin la marca");
        }

        Console.WriteLine("\n  3) LA MARCA CAMBIA LA CLAVE\n");
        object av = Activator.CreateInstance(TAV, true);
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Un disco");
        FieldInfo fEnCola = TAV.GetField("EnCola", TODO);
        Check(fEnCola != null, "la pared sabe preguntar si un disco tiene algo pedido (EnCola)");
        MethodInfo mClave = (MethodInfo)clave;
        Type tDel = fEnCola.FieldType;
        bool pedido = false;
        fEnCola.SetValue(av, Delegate.CreateDelegate(tDel, new Caja(delegate { return pedido; }), typeof(Caja).GetMethod("Responde")));
        string sinMarca = (string)mClave.Invoke(av, new object[] { alb });
        pedido = true;
        string conM = (string)mClave.Invoke(av, new object[] { alb });
        Console.WriteLine("     sin nada pedido:  \"" + sinMarca + "\"");
        Console.WriteLine("     con algo pedido:  \"" + conM + "\"");
        Check(sinMarca != conM, "pedir una cancion de ese disco cambia su clave (si no, la textura no se rehace)");
        pedido = false;
        Check((string)mClave.Invoke(av, new object[] { alb }) == sinMarca, "y al vaciarse la cola, vuelve la de antes");

        Console.WriteLine("\n  4) Y NO CUESTA UNA CADENA NUEVA POR CUADRO\n");
        // Esta clave se pide para CADA casilla en CADA cuadro. Armar una cadena cada vez serian dieciocho
        // por cuadro, sesenta veces por segundo, de basura para el recolector, para siempre.
        pedido = true;
        object p1 = mClave.Invoke(av, new object[] { alb });
        object p2 = mClave.Invoke(av, new object[] { alb });
        Check(object.ReferenceEquals(p1, p2), "pedirla dos veces devuelve LA MISMA cadena, no una copia nueva");
        pedido = false;
        object q1 = mClave.Invoke(av, new object[] { alb });
        object q2 = mClave.Invoke(av, new object[] { alb });
        Check(object.ReferenceEquals(q1, q2), "y sin marca, igual");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }

    // Cajita para poder darle a la pared un "¿esta pedido?" que conteste lo que queramos en cada momento.
    class Caja
    {
        readonly Func<bool> f;
        public Caja(Func<bool> f) { this.f = f; }
        public bool Responde(object a) { return f(); }
    }
}
