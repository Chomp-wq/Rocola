using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

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
//
//  7) QUE BAJE POR ESCALONES Y NO SE SALTE EL DE EN MEDIO. Cada efecto tiene tres niveles -alto, medio y
//     bajo-, o sea DOS escalones. Ir de alto a bajo de una es afear la rocola el doble de lo que hacia
//     falta: si con el escalon de en medio ya se llegaba, ahi habia que pararse.
//
//  8) QUE LA MUSICA ENTRE EN LA CUENTA. Y esta es la que de verdad decide si el medidor sirve. El audio de
//     esta rocola se rellena DESDE EL HILO DE LA PANTALLA (esta escrito en PlayTrack): cada bloque que se
//     vacia, la interfaz se para en seco a descifrar MP3 y a pasarlo por toda la cadena de sonido. O sea
//     que la musica no es un ruido de fondo que se suma: PELEA POR EL MISMO HILO, veinte veces por segundo.
//     Y el medidor pide los cuadros el mismo, en un bucle: si ese bucle no deja correr el bucle de mensajes,
//     el audio se queda sin colchon y su trabajo NO ENTRA EN LA CUENTA. El numero saldria bueno y falso.
//     Aqui se comprueba que el bucle los deja correr, que para el temporizador de animacion mientras mide
//     (si no, se dispararia dos veces por cuadro y el numero saldria peor de lo que es), y que la cancion
//     que se para al acabar es SOLO la que puso el medidor: la que ya estuviera sonando es de un cliente
//     que la pago.
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

    // Un campo que lleva TRES niveles no puede ser un bool: un si/no no sabe decir "menos nieblas".
    static bool EsNivel(Type t, string n)
    {
        if (t == null) return false;
        FieldInfo f = t.GetField(n, TODO);
        if (f != null) return f.FieldType != typeof(bool);
        PropertyInfo pr = t.GetProperty(n, TODO);
        return pr != null && pr.PropertyType != typeof(bool);
    }

    static bool Todos(int[] v, int q) { foreach (int x in v) if (x != q) return false; return true; }
    static bool Existe(int[] v, int q) { foreach (int x in v) if (x == q) return true; return false; }
    static int Suma(int[] v) { int s = 0; foreach (int x in v) s += x; return s; }

    // Si el zoom esta encendido en este ejemplar, mirandolo como lo mira el programa. Vale igual si es
    // una propiedad calculada o un campo guardado: lo que se comprueba es el resultado, no la forma.
    static bool Zoom(object f)
    {
        PropertyInfo p = TF.GetProperty("transAnimOn", TODO);
        if (p != null) return (bool)p.GetValue(f, null);
        FieldInfo c = TF.GetField("transAnimOn", TODO);
        return c != null && (bool)c.GetValue(f);
    }

    static bool Campo(Type t, string n)
    {
        if (t == null) return false;
        if (t.GetField(n, TODO) != null) return true;
        return t.GetProperty(n, TODO) != null;
    }

    // Llama a ProponeEscalones por reflexion. Devuelve el NIVEL en que queda cada efecto: 2 alto, 1 medio,
    // 0 bajo. Se prueba asi -dandole costes de mentira- para que la decision se pueda comprobar SIN tarjeta
    // grafica: si solo se pudiera probar delante de una, no se probaria nunca.
    static int[] Escalones(MethodInfo mp, double[] aMedio, double[] aBajo, double msPeor, int meta, out double msFinal)
    {
        object[] arg = { aMedio, aBajo, msPeor, meta, null };
        int[] r = (int[])mp.Invoke(null, arg);
        msFinal = (double)arg[4];
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
        MethodInfo prop = Met(TF, "ProponeEscalones");
        Check(mide != null, "hay un medidor de este equipo (MideRendimiento)");
        Check(prop != null, "y decide que nivel poner con lo medido (ProponeEscalones)");
        Check(Met(TF, "PaginaImagen") != null, "la pagina 'Imagen y rendimiento' se arma en su funcion");

        // Las cuatro perillas nuevas, en el panel de la tarjeta grafica.
        if (TPanel != null)
        {
            Check(Campo(TPanel, "Fugaces"), "el panel tiene perilla de estrellas fugaces");
            Check(Campo(TPanel, "Nieblas"), "el panel tiene perilla de nieblas");
            Check(Campo(TPanel, "Estrellas"), "el panel tiene perilla de cantidad de estrellas");
            Check(Campo(TPanel, "RelevoPatron"), "el panel tiene perilla del relevo de patron");
            // Y ya no son de si/no: llevan TRES niveles. Un bool no puede decir "menos nieblas".
            Check(EsNivel(TPanel, "Nieblas"), "las nieblas van por nivel, no por si/no");
            Check(EsNivel(TPanel, "Fugaces"), "las fugaces van por nivel");
            Check(EsNivel(TPanel, "RelevoPatron"), "el relevo de patron va por nivel");
            Check(Campo(TPanel, "MsTrabajo"), "el panel cronometra el TRABAJO de un cuadro (sin la espera del monitor)");
        }
        else Console.WriteLine("  ---   sin panel de tarjeta grafica en este ejecutable (perfil sin GPU)");

        Console.WriteLine("\n  2) LAS PERILLAS SE GUARDAN Y SE RELEEN\n");
        string[] claves = { "estrellas=", "fugaces=", "nieblas=", "patron=", "espectro=", "animacion=" };
        MethodInfo lee = Met(TF, "LoadConfigPrefs"), esc = Met(TF, "SaveConfigPrefs");
        List<string> tLee = (lee != null) ? Codigo.Textos(lee) : new List<string>();
        List<string> tEsc = (esc != null) ? Codigo.Textos(esc) : new List<string>();
        foreach (string k in claves)
        {
            Check(tLee.Contains(k), "el archivo de configuracion se LEE con " + k);
            Check(tEsc.Contains(k), "y se GUARDA con " + k);
        }
        // Y los tres niveles se escriben con PALABRAS, no con numeros: el archivo de configuracion se abre
        // con el bloc de notas en el bar, y "nieblas=medio" se entiende sin tener que descifrar nada.
        // (Las palabras viven en el par que traduce nivel <-> texto, que es donde tienen que estar: una sola
        // pareja de funciones, y no repartidas por cada clave.)
        List<string> tNiv = new List<string>();
        tNiv.AddRange(Codigo.Textos(Met(TF, "TextoNivel")));
        tNiv.AddRange(Codigo.Textos(Met(TF, "NivelDe")));
        foreach (string w in new string[] { "alto", "medio", "bajo" })
            Check(tNiv.Contains(w), "los niveles se escriben con la palabra \"" + w + "\"");

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

        // EL ZOOM AL ENTRAR A UN DISCO ES LA PERILLA MAS FACIL DE DEJAR COLGADA, porque hay dos maneras de
        // apagarlo -el nivel y un si/no- y si son dos cosas distintas se contradicen. Aqui se pone el nivel
        // a mano y se mira si el zoom se entera: si "animacion" en bajo no lo apaga, la casilla de la
        // ventana miente y el operador la apaga sin que cambie nada.
        FieldInfo fAnim = TF.GetField("animNiv", TODO);
        Check(fAnim != null, "el nivel de la animacion es un campo que se puede mirar (animNiv)");
        if (fAnim != null)
        {
            object f1 = FormatterServices.GetUninitializedObject(TF);
            fAnim.SetValue(f1, 2);  bool enAlto = Zoom(f1);
            fAnim.SetValue(f1, 0);  bool enBajo = Zoom(f1);
            Check(enAlto, "en alto, el zoom al entrar a un disco esta encendido");
            Check(!enBajo, "y poniendo 'animacion' en bajo se apaga DE VERDAD (no solo se acorta)");
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

        Console.WriteLine("\n  5) LA MUSICA ENTRA EN LA CUENTA\n");
        // Sin esto el medidor mide una maquina que no existe: una donde nadie esta descomprimiendo un MP3.
        if (mide != null)
        {
            List<string> lla = Codigo.ConAyudantes(TF, "MideRendimiento");
            List<string> lb = Codigo.ConAyudantes(TF, "MideEscena");
            lb.AddRange(Codigo.ConAyudantes(TF, "CorreEscena"));   // el bucle de verdad vive aqui
            lb.AddRange(lla);
            Check(Codigo.Llama(lb, "DoEvents"),
                  "el bucle del medidor DEJA CORRER los mensajes (de ahi come el audio; sin eso la musica no cuenta)");
            Check(Codigo.Llama(lla, "PlayTrack"), "el medidor pone una cancion para medir con carga de verdad");
            Check(Codigo.Llama(lla, "DisposePlaybackNow"), "y la para al acabar");
            // SOLO LA SUYA. Si ya habia algo sonando, es de un cliente que lo pago: se usa esa carga y no se
            // toca. Que la regla este escrita en un campo -y que el final lo LEA- es lo que evita que un dia
            // alguien corte la cancion de alguien por medir.
            int leeMus, escMus;
            Codigo.UsosDeCampo(mide, "bancoPuseYoLaCancion", out leeMus, out escMus);
            Check(escMus > 0 && leeMus > 0, "se apunta si la cancion la puso el medidor, y al acabar se mira antes de pararla");
            // Y el temporizador de animacion se para: si no, se dispara ademas del que llama el medidor y la
            // rocola creeria que va al doble de velocidad -mas trabajo del real, numero peor del real-.
            Check(Codigo.Llama(lla, "Stop") && Codigo.Llama(lla, "Start"),
                  "para el temporizador de animacion mientras mide, y lo vuelve a arrancar");
        }

        Console.WriteLine("\n  6) BAJA POR ESCALONES, Y NO SE SALTA EL DE EN MEDIO\n");
        if (prop == null)
        {
            Check(false, "sin ProponeEscalones no se puede comprobar como decide");
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }

        // Cuatro efectos. De cada uno, lo que ahorra su PRIMER escalon (alto->medio) y lo que ahorra el
        // SEGUNDO (medio->bajo). El 3 no cuesta nada: no se puede tocar nunca.
        //   efecto:        0     1     2     3
        double[] aMedio = { 1.0,  6.0,  3.0,  0.05 };
        double[] aBajo  = { 1.0,  2.0,  1.5,  0.05 };
        double fin;
        int[] niv;

        // (a) YA PASA LA META: todo se queda en alto (2). 20 ms por cuadro son 50 por segundo.
        niv = Escalones(prop, aMedio, aBajo, 20.0, 30, out fin);
        Check(Todos(niv, 2), "si ya pasa de la meta, no baja nada de nivel");

        // (b) NO PASA, y con UN escalon basta. 40 ms son 25 por segundo; la meta de 30 pide 33.3 ms.
        //     El escalon que mas ahorra es el primero del efecto 1 (6 ms): 40 - 6 = 34, aun no. Luego el
        //     primero del 2 (3 ms): 31, ya pasa. Dos escalones, los dos PRIMEROS: nadie llega a bajo.
        niv = Escalones(prop, aMedio, aBajo, 40.0, 30, out fin);
        Check(niv[1] == 1 && niv[2] == 1, "baja de nivel los dos que mas ahorran");
        Check(niv[0] == 2 && niv[3] == 2, "y deja en alto los que no hizo falta tocar");
        Check(!Existe(niv, 0), "NADIE se salta el escalon de en medio para irse a bajo");
        Check(fin <= 1000.0 / 30, "y la cuenta que devuelve pasa de verdad la meta (" + fin.ToString("0.0") + " ms)");

        // (c) EL EFECTO QUE NO CUESTA NADA NO SE TOCA JAMAS, ni cuando hace falta bajarlo todo.
        niv = Escalones(prop, aMedio, aBajo, 200.0, 30, out fin);
        Check(niv[3] == 2, "el efecto que no se nota se queda en alto aunque falte rendimiento");
        Check(niv[0] == 0 && niv[1] == 0 && niv[2] == 0, "y los que si cuestan se bajan del todo");
        Check(fin > 1000.0 / 30, "y no miente: la cuenta sigue sin llegar (" + fin.ToString("0.0") + " ms)");

        // (d) EL SEGUNDO ESCALON SOLO DESPUES DEL PRIMERO. Aqui el segundo escalon del efecto 0 ahorra
        //     muchisimo (9 ms) y el primero casi nada (0.3). Aun asi hay que dar el primero antes: no se
        //     puede saltar de alto a bajo. Si alguien "optimizara" eso, este numero lo delata.
        double[] mA = { 0.3, 1.0, 0.0, 0.0 };
        double[] mB = { 9.0, 1.0, 0.0, 0.0 };
        niv = Escalones(prop, mA, mB, 40.0, 30, out fin);
        Check(niv[0] <= 1, "para llegar al segundo escalon pasa por el primero");

        // (e) DECIDE DISTINTO CON NUMEROS DISTINTOS: mide, no tiene una tabla escrita a mano.
        double[] alReves = { 6.0, 1.0, 3.0, 0.05 };
        niv = Escalones(prop, alReves, aBajo, 40.0, 30, out fin);
        Check(niv[0] == 1, "con otros costes baja otras cosas");

        // (f) LA META MANDA: pedir 60 baja mas cosas que pedir 30.
        int[] n30 = Escalones(prop, aMedio, aBajo, 40.0, 30, out fin);
        int[] n60 = Escalones(prop, aMedio, aBajo, 40.0, 60, out fin);
        Check(Suma(n60) <= Suma(n30), "pedir mas cuadros por segundo baja mas cosas");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
