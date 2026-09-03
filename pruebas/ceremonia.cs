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
// SE PROBO CON SONIDOS Y NO PAGO. Hubo una version con seis efectos -moneda, golpe al entrar a la cola,
// aviso de sin creditos, brazo mecanico, aguja de vinilo y un tic al moverse-. En el bar, el de la
// moneda gustaba, pero LOS DEMAS QUEDABAN TAPADOS POR LA MUSICA: suenan mientras hay cancion, mezclados
// debajo de ella, y no se oyen. Un sonido que no se oye no es una funcion, es codigo que mantener, asi
// que se quitaron todos. Queda apuntado aqui para que a nadie se le ocurra volver a intentarlo sin
// saber que ya se intento.
//
// LO QUE SI PAGO, y es lo que esta bateria vigila, es decir lo mismo POR LOS OJOS:
//
//   LA MONEDA. Cae girando hasta el contador, el numero rueda como un odometro y la pastilla destella.
//   Es el instante en que el dinero se vuelve musica, y antes no se veia.
//
//   LA PORTADA QUE VUELA. Al poner una cancion, su portada sale de la pared y aterriza en la pastilla de
//   la cola, que da un brinco. Sin una sola palabra, el cliente ve QUE paso y DONDE quedo su cancion.
//   Y no se escribio una animacion nueva: la rocola YA tiene una que lleva una portada de un rectangulo
//   a otro girando -la del disco que se abre-, asi que esto es un segundo uso de esa misma pieza. Eso
//   ademas la hace nacer apagable: obedece al interruptor de "animacion" que ya existia.
//
//   EL AVISO DE SIN CREDITOS. La pastilla se sacude. Hoy solo salia un letrero que dice "Inserta una
//   moneda", y a las dos de la manana nadie lee letreros: el cliente sigue picandole al boton creyendo
//   que la maquina esta descompuesta. Un movimiento si se ve.
//
// LO QUE VIGILA, que es donde esto se rompe sin que nadie lo note:
//
//   1) QUE CADA MOMENTO DISPARE LO SUYO. Una animacion desenchufada por un arreglo de otra cosa no da
//      error: simplemente deja de verse, y nadie se entera hasta que alguien la echa de menos.
//
//   2) QUE SE PUEDA APAGAR, Y QUE APAGADO SEA APAGADO. El vuelo de la portada obedece a "animacion", el
//      mismo interruptor que ya apagaba el zoom del disco. Un ajuste que no apaga de verdad es peor que
//      no tenerlo.
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
    // TODAS las versiones de un metodo con ese nombre. Varias de estas funciones tienen dos -una corta
    // que solo llama a la larga-, y mirar solo la primera es mirar la que no hace nada.
    static List<MethodBase> Todas(Type t, string n)
    {
        List<MethodBase> r = new List<MethodBase>();
        if (t == null) return r;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) r.Add(m);
        return r;
    }
    static bool AlgunaLlama(Type t, string n, string que)
    {
        foreach (MethodBase m in Todas(t, n)) if (Codigo.Llama(m, que)) return true;
        return false;
    }
    static bool AlgunaNombra(Type t, string n, string texto)
    {
        foreach (MethodBase m in Todas(t, n))
            foreach (string x in Codigo.Textos(m)) if (x == texto) return true;
        return false;
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

        MethodBase moneda = Met(TF, "AddCoin"), poner = Met(TF, "AddItem");

        Console.WriteLine("\n  1) LA MONEDA SE VE CAER\n");
        Check(moneda != null, "hay un sitio por donde entra la moneda (AddCoin)");
        Check(TPast != null, "existe la pastilla del contador (PastillaView)");
        Check(TPast != null && Met(TPast, "Moneda") != null, "la pastilla sabe hacer caer una moneda");
        Check(TPast != null && Met(TPast, "Aviso") != null, "y sabe sacudirse cuando no hay creditos");
        Check(TPast != null && Tiene(TPast, "Animando"), "y sabe decir si esta en mitad de una animacion");
        if (moneda != null) Check(Codigo.Llama(moneda, "Moneda"), "al entrar la moneda, la pastilla la ve caer");

        // EL NUMERO RUEDA. Sin esto la moneda cae, la pastilla destella... y el numero cambia de golpe
        // como siempre, que es justo lo que se venia a arreglar.
        if (TPast != null)
            Check(Tiene(TPast, "rodDe") && Tiene(TPast, "rodTick"),
                  "el numero de creditos RUEDA de uno a otro, no salta");

        // Y LA CAPSULA SE TIENE QUE REPINTAR MIENTRAS DURA. Con tarjeta grafica el control no se repinta
        // -lo dibuja la textura de la interfaz-, asi que lo que tiene que cambiar es la FIRMA de esa
        // textura. Sin eso, la moneda caeria congelada dentro de una imagen que no se rehace.
        MethodBase firma = Met(TF, "UiBotSig");
        if (firma != null && TPast != null)
            Check(Codigo.Llama(firma, "get_Paso"),
                  "y la imagen de la interfaz se rehace mientras cae (si no, se veria congelada)");

        Console.WriteLine("\n  2) SIN CREDITOS, LA PASTILLA SE SACUDE\n");
        Check(poner != null, "hay un sitio por donde entra una cancion a la cola (AddItem)");
        if (poner != null) Check(Codigo.Llama(poner, "Aviso"), "quedarse sin creditos SE VE, no solo se escribe");

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

        Console.WriteLine("\n  4) LA MARCA DE \"YA LA PUSISTE\"\n");
        // Un cliente recorre la pared, pide tres canciones y a los cinco minutos ya no se acuerda de
        // cuales: vuelve a pedir la misma. Un palomito en la esquina lo resuelve sin una palabra.
        Type TAlb = null;
        foreach (Type t in asm.GetTypes())
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                if (x.Name == "AlbumView") TAlb = x;
        Check(TAlb != null && Tiene(TAlb, "EnCola"), "la pared puede preguntar si un disco tiene algo pedido");
        MethodBase refresca = Met(TF, "RefrescaCola");
        if (refresca != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(refresca, "colaDiscos", out lee, out esc);
            Check(lee > 0 || esc > 0, "y la rocola apunta DE QUE DISCOS hay algo pedido al refrescar la cola");
        }
        if (TAlb != null)
        {
            Check(Met(TAlb, "Palomito") != null, "hay un palomito que dibujar");
            Check(AlgunaLlama(TAlb, "BakeCell", "Palomito"), "y se hornea DENTRO de la casilla, no encima");
            // LA MARCA TIENE QUE ENTRAR EN LA LLAVE DE LA CACHE. Si no, al pedir una cancion la casilla
            // ya horneada se seguiria usando tal cual y el palomito no saldria hasta que algo mas
            // obligara a rehacerla.
            //
            // Antes esto se comprobaba buscando el "#q" DENTRO de cada caja, y ahi estaba el fallo: eran
            // dos sitios copiados, llego un tercero -la clave que se le entrega a la tarjeta grafica- y
            // ese se quedo sin la marca, con lo que el palomito no se veia en ninguna de las dos maquinas
            // de Windows. Ahora la clave la arma UNA funcion y las cajas se la piden; quien vigila que
            // nadie se la salte es la bateria 'palomito'.
            string[] cajas = { "GetOrBakeCell", "PeekOrBakeCell" };
            for (int i = 0; i < cajas.Length; i++)
                Check(AlgunaLlama(TAlb, cajas[i], "ClaveCasilla"),
                      "la caja de casillas pide la llave a quien le pone la marca (" + cajas[i] + ")");
            Check(AlgunaNombra(TAlb, "ClaveCasilla", "#q"), "y esa llave lleva la marca de \"ya la pediste\"");
        }

        Console.WriteLine("\n  5) Y NO QUEDA NI RASTRO DE LOS SONIDOS\n");
        // Se probaron y no pagaron (ver arriba). Que no vuelvan a colarse a medias: ni la pieza que los
        // fabricaba, ni la que los mezclaba, ni el sitio por donde se disparaban.
        bool rastro = false;
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Efectos" || t.Name == "MezclaEfectos" || t.Name == "Efecto") rastro = true;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                if (x.Name == "Efectos" || x.Name == "MezclaEfectos" || x.Name == "Efecto") rastro = true;
        }
        Check(!rastro, "no quedo el motor de sonidos a medias");
        Check(Met(TF, "SuenaEfecto") == null, "ni el sitio por donde se disparaban");
        Check(Met(TF, "EfectoVolLin") == null, "ni su volumen");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
