using System;
using System.Collections.Generic;
using System.Reflection;

// LA CEREMONIA DE PAGAR Y DE PONER UNA CANCION.
//
// Un cliente de bar llega de noche, con prisa, con los amigos mirando y a veces con tres cervezas
// encima. Mete una moneda y la rocola cambiaba un numero de 0 a 1. Aprieta un boton y salia un letrero
// de texto. Las dos cosas son informacion CORRECTA y las dos son invisibles a dos metros: el numero es
// chico y el letrero hay que leerlo.
//
// Lo que se agrega no es adorno. Es lo mismo dicho de una forma que se entiende SIN LEER:
//
//   LA MONEDA. Cae girando hasta el contador, el numero rueda y la pastilla destella. Es el instante en
//   que el dinero se vuelve musica, y hasta ahora no se veia ni se oia.
//
//   LA PORTADA QUE VUELA. Al poner una cancion, su portada sale de la pared y aterriza en la pastilla de
//   la cola, que da un brinco. Sin una sola palabra, el cliente ve QUE paso y DONDE quedo su cancion.
//   Y no se escribe una animacion nueva: la rocola YA tiene una que lleva una portada de un rectangulo a
//   otro girando -la del disco que se abre-, asi que esto es un segundo uso de esa misma pieza. Eso
//   ademas la hace nacer apagable: obedece al interruptor de "animacion" que ya existia.
//
//   EL AVISO DE SIN CREDITOS. La pastilla se sacude y suena el "no". Hoy solo salia un letrero que dice
//   "Inserta una moneda", y a las dos de la manana nadie lee letreros: el cliente sigue picandole al
//   boton creyendo que la maquina esta descompuesta.
//
// LO QUE ESTA BATERIA VIGILA, que es donde esto se rompe sin que nadie lo note:
//
//   1) QUE CADA MOMENTO DISPARE LO SUYO. Un sonido enchufado en el sitio equivocado -o desenchufado por
//      un arreglo de otra cosa- no da error: simplemente deja de sonar, y nadie se entera hasta que
//      alguien lo echa de menos en el bar.
//
//   2) QUE TODO SE PUEDA APAGAR, Y QUE APAGADO SEA APAGADO. Los sonidos con "efectos=" y el vuelo de la
//      portada con "animacion=", el mismo interruptor que ya apagaba el zoom del disco. Un ajuste que
//      no apaga de verdad es peor que no tenerlo.
class Ceremonia
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TPast;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static bool Tiene(Type t, string n)
    {
        if (t == null) return false;
        return t.GetField(n, TODO) != null || t.GetProperty(n, TODO) != null;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                if (x.Name == "PastillaView") TPast = x;
        }
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) CADA MOMENTO SUENA\n");
        MethodBase moneda = Met(TF, "AddCoin"), poner = Met(TF, "AddItem");
        Check(moneda != null, "hay un sitio por donde entra la moneda (AddCoin)");
        Check(poner != null, "y otro por donde entra una cancion a la cola (AddItem)");
        if (moneda != null) Check(Codigo.Llama(moneda, "SuenaEfecto"), "meter una moneda SUENA");
        if (poner != null) Check(Codigo.Llama(poner, "SuenaEfecto"), "poner una cancion SUENA (y quedarse sin creditos, tambien)");

        // Un solo sitio por donde salen todos: si cada momento se buscara la vida por su cuenta, apagar
        // los efectos habria que acordarse de hacerlo en seis lugares.
        MethodBase suena = Met(TF, "SuenaEfecto");
        Check(suena != null, "todos salen por el MISMO sitio (SuenaEfecto)");
        if (suena != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(suena, "efectosOn", out lee, out esc);
            Check(lee > 0, "y ese sitio mira el interruptor: apagados, no suena ni uno");
            Codigo.UsosDeCampo(suena, "efectosTic", out lee, out esc);
            Check(lee > 0, "y mira aparte el del tic, que va apagado de fabrica");
        }

        Console.WriteLine("\n  2) LA MONEDA SE VE CAER\n");
        Check(TPast != null, "existe la pastilla del contador (PastillaView)");
        Check(TPast != null && Met(TPast, "Moneda") != null, "la pastilla sabe hacer caer una moneda");
        Check(TPast != null && Met(TPast, "Aviso") != null, "y sabe sacudirse cuando no hay creditos");
        Check(TPast != null && Tiene(TPast, "Animando"), "y sabe decir si esta en mitad de una animacion");
        if (moneda != null) Check(Codigo.Llama(moneda, "Moneda"), "al entrar la moneda, la pastilla la ve caer");

        // EL NUMERO RUEDA. Sin esto la moneda cae, la pastilla destella... y el numero cambia de golpe
        // como siempre, que es justo lo que se venia a arreglar.
        if (TPast != null)
        {
            Check(Tiene(TPast, "rodDe") && Tiene(TPast, "rodTick"),
                  "el numero de creditos RUEDA de uno a otro, no salta");
        }

        Console.WriteLine("\n  3) LA PORTADA VUELA A LA COLA\n");
        MethodBase vuela = Met(TF, "VuelaALaCola");
        Check(vuela != null, "hay un vuelo de la portada hasta la cola (VuelaALaCola)");
        if (vuela != null)
        {
            // NO SE ESCRIBE UNA ANIMACION NUEVA. La rocola ya sabe llevar una portada de un rectangulo a
            // otro girando -es la del disco que se abre-, y esto tiene que ser un segundo uso de ESA.
            // Si algun dia alguien escribe otra en paralelo, esta linea se pone roja.
            Check(Codigo.Llama(vuela, "StartDiscTrans"),
                  "y REUSA la animacion que ya existia, no escribe otra en paralelo");
        }
        if (poner != null) Check(Codigo.Llama(poner, "VuelaALaCola"), "poner una cancion la hace volar");

        // Y como reusa esa pieza, hereda su interruptor sin trabajo extra: con "animacion" en bajo,
        // StartDiscTrans se planta en su primera linea y no vuela nada.
        MethodBase arranca = Met(TF, "StartDiscTrans");
        if (arranca != null)
        {
            List<string> pasos = Codigo.Repasa(arranca);
            bool miraNivel = false;
            for (int i = 0; i < pasos.Count && i < 8; i++)
                if (pasos[i] == "get_transAnimOn" || pasos[i] == "$animNiv") miraNivel = true;
            Check(miraNivel, "y hereda el interruptor de 'animacion': en bajo, no vuela nada");
        }

        Console.WriteLine("\n  4) LO QUE SE APAGA, SE APAGA\n");
        MethodBase lee2 = Met(TF, "LoadConfigPrefs");
        MethodBase esc2 = Met(TF, "SaveConfigPrefs");
        string[] claves = { "efectos=", "efectos_vol=", "efectos_tic=" };
        List<string> tLee = (lee2 != null) ? Codigo.Textos(lee2) : new List<string>();
        List<string> tEsc = (esc2 != null) ? Codigo.Textos(esc2) : new List<string>();
        foreach (string k in claves)
        {
            Check(tLee.Contains(k), "el archivo de configuracion se LEE con " + k);
            Check(tEsc.Contains(k), "y se GUARDA con " + k);
        }

        // Y EL VOLUMEN DE LOS EFECTOS BAJA CON EL MODO NOCHE. Si no, a las tres de la manana la rocola
        // baja 4 dB y la moneda se queda igual: seria lo mas fuerte que suena en el local.
        MethodBase vol = Met(TF, "EfectoVolLin");
        Check(vol != null, "los efectos tienen su propio volumen");
        if (vol != null) Check(Codigo.Llama(vol, "FactorNoche"), "y de madrugada bajan con la musica");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
