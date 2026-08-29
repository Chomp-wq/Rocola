using System;
using System.Collections.Generic;
using System.Reflection;

// EL MEDIDOR DE ESTE EQUIPO (Ctrl+F3 -> Imagen y rendimiento).
//
// La rocola no sabia decir como le va al equipo donde esta puesta. Se sabia lo que costaba cada trozo -eso lo
// miden estas baterias- pero nadie podia encenderla en un bar, pulsar un boton y que contestara "la atraccion
// va a 21 cuadros por segundo, apagando las fugaces se pone en 34". Eso es lo que se agrega.
//
// LO QUE ESTA BATERIA VIGILA, y que es donde un medidor se vuelve mentiroso:
//
//  1) QUE EL VEREDICTO SALGA DE LO MEDIDO. Un medidor que decide por una tabla de "equipos buenos y malos"
//     escrita a mano no mide nada: adivina. Aqui se le dan numeros de mentira -unos caros, otros baratos- y
//     tiene que decidir DISTINTO con cada uno.
//
//  2) QUE APAGUE EN ORDEN DE COSTE Y PARE. El orden lo manda el numero, no el gusto de nadie: primero el
//     efecto que mas ahorra. Y en cuanto la cuenta pasa de la meta, PARA. Apagar de mas es tan malo como no
//     apagar: deja la rocola fea sin motivo.
//
//  3) QUE UN EFECTO QUE NO CUESTA NADA NO SE PROPONGA NUNCA. Es la misma regla de siempre (buscacosto.cs:
//     "Se midio, y NO PAGA"): lo que no se distingue del ruido no se toca.
//
//  4) QUE CUANDO NO LLEGA, LO DIGA. Hay equipos que no dan 30 cuadros por segundo ni con todo apagado. Ahi
//     el medidor tiene que decirlo, no fingir que lo arreglo.
//
//  5) QUE NO APLIQUE NADA SOLO. Se pidio que PROPUSIERA. Medir no puede guardar la configuracion ni reiniciar
//     la tarjeta: hasta que no se pulsa "Aplicar", nada se mueve.
//
//  6) QUE LAS PERILLAS CORTEN DE VERDAD. De nada sirve una casilla "nieblas" si el dibujo de las nieblas no
//     la mira. Se lee el codigo ya compilado y se comprueba que cada perilla se consulta donde toca.
class Banco
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static Type TF, TPanel;

    static MethodInfo Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    // TODOS los textos de una funcion, incluidos los de las funciones sueltas que el compilador le saca
    // aparte con el nombre entre picos (<VentanaAjustes>c__AnonStorey...). Sin esto, un texto que vive
    // dentro de un delegado -como el titulo, que cambia al pulsar la pestana- se daria por no escrito.
    static List<string> TextosConAyudantes(Type raiz, string metodo)
    {
        List<string> r = new List<string>();
        if (raiz == null) return r;
        string picos = "<" + metodo + ">";
        List<Type> tipos = new List<Type>();
        tipos.Add(raiz);
        tipos.AddRange(raiz.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        foreach (Type t in tipos)
            foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (m.Name == metodo || m.Name.IndexOf(picos, StringComparison.Ordinal) >= 0
                    || t.Name.IndexOf(picos, StringComparison.Ordinal) >= 0)
                    r.AddRange(Codigo.Textos(m));
        return r;
    }

    static bool Campo(Type t, string n)
    {
        if (t == null) return false;
        if (t.GetField(n, TODO) != null) return true;
        return t.GetProperty(n, TODO) != null;
    }

    // Llama a ProponeApagados por reflexion y devuelve los indices que propone apagar.
    static int[] Propone(MethodInfo mp, double[] ahorro, double msPeor, int meta, out double msFinal)
    {
        object[] arg = { ahorro, msPeor, meta, null };
        int[] r = (int[])mp.Invoke(null, arg);
        msFinal = (double)arg[3];
        return r;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "GpuPanel") TPanel = t;
        }
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS QUE HACEN FALTA\n");

        MethodInfo mide = Met(TF, "MideRendimiento");
        MethodInfo prop = Met(TF, "ProponeApagados");
        Check(mide != null, "hay un medidor de este equipo (MideRendimiento)");
        Check(prop != null, "y decide que apagar con lo medido (ProponeApagados)");
        Check(Met(TF, "PaginaImagen") != null, "la pagina 'Imagen y rendimiento' se arma en su funcion");

        // Las cuatro perillas nuevas, en el panel de la tarjeta grafica.
        if (TPanel != null)
        {
            Check(Campo(TPanel, "Fugaces"), "el panel tiene perilla de estrellas fugaces");
            Check(Campo(TPanel, "Nieblas"), "el panel tiene perilla de nieblas");
            Check(Campo(TPanel, "Estrellas"), "el panel tiene perilla de cantidad de estrellas");
            Check(Campo(TPanel, "RelevoPatron"), "el panel tiene perilla del relevo de patron");
            Check(Campo(TPanel, "MsTrabajo"), "el panel cronometra el TRABAJO de un cuadro (sin la espera del monitor)");
        }
        else Console.WriteLine("  ---   sin panel de tarjeta grafica en este ejecutable (perfil sin GPU)");

        Console.WriteLine("\n  2) LAS CUATRO PERILLAS SE GUARDAN Y SE RELEEN\n");
        string[] claves = { "estrellas=", "fugaces=", "nieblas=", "patron=" };
        MethodInfo lee = Met(TF, "LoadConfigPrefs"), esc = Met(TF, "SaveConfigPrefs");
        List<string> tLee = (lee != null) ? Codigo.Textos(lee) : new List<string>();
        List<string> tEsc = (esc != null) ? Codigo.Textos(esc) : new List<string>();
        foreach (string k in claves)
        {
            Check(tLee.Contains(k), "el archivo de configuracion se LEE con " + k);
            Check(tEsc.Contains(k), "y se GUARDA con " + k);
        }

        Console.WriteLine("\n  3) QUE LAS PERILLAS CORTEN DE VERDAD\n");
        if (TPanel != null)
        {
            // Cada perilla tiene que CONSULTARSE donde se dibuja lo que apaga. Una casilla que no mira nadie
            // es peor que no tenerla: el operador la apaga, no cambia nada, y deja de fiarse del resto.
            int l, e;
            Codigo.UsosDeCampo(Met(TPanel, "DibujaNieblas"), "Nieblas", out l, out e);
            Check(l > 0, "el dibujo de las nieblas mira su perilla");

            Type TStar = null;
            foreach (Type t in asm.GetTypes()) if (t.Name == "GpuStarfield") TStar = t;
            if (TStar != null)
            {
                Codigo.UsosDeCampo(Met(TStar, "UpdateAndDrawShoots"), "fugaces", out l, out e);
                Check(l > 0, "las fugaces miran su perilla");
                Codigo.UsosDeCampo(Met(TStar, "BuildStars"), "densidad", out l, out e);
                Check(l > 0, "la cantidad de estrellas mira su perilla");
                Codigo.UsosDeCampo(Met(TStar, "Begin"), "relevo", out l, out e);
                Check(l > 0, "el relevo de patron mira su perilla");
            }
            else Check(false, "no se encontro el campo de estrellas de la tarjeta grafica");
        }

        Console.WriteLine("\n  3b) LA PESTANA NUEVA NO SE LLEVO POR DELANTE LA DE SONIDO\n");
        // La ventana de sonido llevaba anos funcionando y estaba llena hasta el ultimo pixel: meterle una
        // pestana era justo la ocasion de romperla sin enterarse. Se comprueba que las tres tarjetas de
        // Sonido siguen ahi Y que las dos pestanas existen, en la MISMA funcion.
        MethodInfo vent = Met(TF, "VentanaAjustes");
        Check(vent != null, "el cuerpo de la ventana sigue en su funcion (VentanaAjustes)");
        List<string> tv = TextosConAyudantes(TF, "VentanaAjustes");
        foreach (string t in new string[] { "VOLUMEN PAREJO", "CALIDAD DEL SONIDO", "TU BIBLIOTECA", "Tipo de disco" })
            Check(tv.Contains(t), "la pagina de Sonido conserva \"" + t + "\"");
        Check(tv.Contains("Sonido") && tv.Contains("Imagen"), "y estan las dos pestanas");
        Check(tv.Contains("Imagen y rendimiento"), "el titulo cambia al pasar a la pestana de Imagen");
        MethodInfo abre = Met(TF, "ShowVolumeManager");
        List<string> la = (abre != null) ? Codigo.Llamadas(abre) : new List<string>();
        Check(Codigo.Llama(la, "VentanaAjustes"), "Ctrl+F3 abre la ventana");
        Check(Codigo.Llama(la, "MideRendimiento"), "y la reabre sola despues de medir");

        Console.WriteLine("\n  4) MEDIR NO APLICA NADA\n");
        if (mide != null)
        {
            List<string> lla = Codigo.ConAyudantes(TF, "MideRendimiento");
            Check(!Codigo.Llama(lla, "SaveConfigPrefs"), "medir NO guarda la configuracion (se pidio que propusiera)");
            Check(!Codigo.Llama(lla, "ApplyAlbumGpu"), "medir NO reinicia la tarjeta grafica");
        }

        Console.WriteLine("\n  5) EL VEREDICTO SALE DE LO MEDIDO\n");
        if (prop == null)
        {
            Check(false, "sin ProponeApagados no se puede comprobar como decide");
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }

        double fin;
        // Cuatro efectos: uno caro, dos medianos y uno que practicamente no cuesta nada.
        //   indice:      0      1      2      3
        //   ahorro ms:  1.0    6.0    3.0    0.05
        double[] ah = { 1.0, 6.0, 3.0, 0.05 };

        // (a) YA PASA LA META: no hay nada que apagar. 20 ms por cuadro son 50 por segundo.
        int[] r = Propone(prop, ah, 20.0, 30, out fin);
        Check(r.Length == 0, "si ya pasa de la meta, no propone apagar nada");

        // (b) NO PASA: 50 ms por cuadro son 20 por segundo. La meta de 30 pide bajar a 33.3 ms.
        //     Con quitar el mas caro (6 ms) no basta (44), ni con el segundo (41)... hay que llegar a 33.3.
        r = Propone(prop, ah, 50.0, 30, out fin);
        Check(r.Length > 0, "si no llega, propone apagar algo");
        // EL ORDEN LO MANDA EL NUMERO: primero el que mas ahorra (indice 1, 6 ms), luego el 2 (3 ms).
        Check(r.Length >= 2 && r[0] == 1 && r[1] == 2, "apaga en orden de coste medido: primero el que mas ahorra");
        Check(!Array.Exists(r, delegate (int i) { return i == 3; }), "el efecto que no cuesta nada NO se propone nunca");

        // (c) PARA EN CUANTO PASA: no apaga de mas. Con 40 ms por cuadro (25 por segundo) basta quitar el
        //     mas caro para bajar a 34 ms (29.4)... no llega; con el segundo, 31 ms (32.2) ya pasa. Dos.
        r = Propone(prop, ah, 40.0, 30, out fin);
        Check(r.Length == 2, "para en cuanto pasa la meta: no apaga de mas (esperaba 2, dio " + r.Length + ")");
        Check(fin <= 1000.0 / 30, "y la cuenta que devuelve pasa de verdad la meta (" + fin.ToString("0.0") + " ms)");

        // (d) NI APAGANDO TODO LLEGA: lo tiene que DECIR, no fingir. Un cuadro de 200 ms son 5 por segundo;
        //     apagando los tres que cuestan algo se queda en 190. Devuelve los tres y la cuenta real.
        r = Propone(prop, ah, 200.0, 30, out fin);
        Check(r.Length == 3, "cuando no alcanza, propone TODO lo que cuesta algo (dio " + r.Length + ")");
        Check(fin > 1000.0 / 30, "y no miente: la cuenta que devuelve sigue sin llegar (" + fin.ToString("0.0") + " ms)");

        // (e) DECIDE DISTINTO CON NUMEROS DISTINTOS. Si los costes se dan la vuelta, el orden se da la vuelta:
        //     esto es lo que separa medir de tener una tabla escrita a mano.
        double[] alReves = { 6.0, 1.0, 3.0, 0.05 };
        r = Propone(prop, alReves, 40.0, 30, out fin);
        Check(r.Length >= 1 && r[0] == 0, "con otros costes decide otra cosa: mide, no adivina");

        // (f) LA META MANDA. Pedirle 60 en vez de 30 tiene que apagar mas cosas.
        int[] r30 = Propone(prop, ah, 40.0, 30, out fin);
        int[] r60 = Propone(prop, ah, 40.0, 60, out fin);
        Check(r60.Length >= r30.Length, "pedir mas cuadros por segundo apaga mas cosas");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
