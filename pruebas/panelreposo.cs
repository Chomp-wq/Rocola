using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// EL PANEL DE REPOSO TIENE QUE ENTERARSE DE TODO LO QUE ENSENA.
//
// Antes se enteraba a lo bruto: cada 400 ms rearmaba su firma entera -titulo, artista, y para cada una de
// las doce de la cola su ruta, su texto y su caratula- y la comparaba con la de antes. Casi siempre no
// habia cambiado nada. Costaba casi nada -0,005 ms medidos, con doce en la cola- pero se veia tarde:
// pones una cancion y el panel va hasta 400 ms por detras. La latencia es el motivo, no el ahorro.
//
// Ahora quien cambia algo AVISA, y la firma solo se rearma cuando hay aviso. Eso mueve el peligro de
// sitio: antes era imposible que el panel se quedara viejo -se lo preguntaba todo, solo que tarde-, y
// ahora un sitio que cambie algo y no avise deja el panel congelado enseNando lo de antes. Esta bateria
// existe por eso, y es la mitad del encargo que dice "comprobar que ningun control quede mostrando
// informacion antigua".
//
// NO LLEVA UNA LISTA A MANO DE LO QUE EL PANEL ENSENA. Se la saca del programa ya compilado: mira que
// campos lee BuildIdlePanel DESPUES de la puerta del contador -los de antes se vuelven a leer en cada
// cuadro, esos no pueden quedarse viejos- y exige un aviso para cada uno. Si manana el panel ensena una
// cosa mas, esta bateria la exige sola, sin que nadie se acuerde de venir a apuntarla aqui.
//
// Y MIRA LA CADENA ENTERA, no solo que la llamada exista: quien toca la cola -> RefrescaCola -> el aviso.
// Quitando cualquier eslabon de los seis, esto se pone rojo y dice el nombre del metodo y de la cosa.
//
// Se le perdona a un sitio no avisar SOLO si no puede correr con el panel montado, o sea si al unico
// sitio desde donde se le llama es el arranque (el constructor y OnShown). Eso tambien se comprueba
// leyendo el programa, no por una lista: el dia que alguien llame a LoadConfigPrefs desde un boton de
// los ajustes, esta bateria se pone roja sola.
class PanelReposo
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    const BindingFlags UNO = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static Type TF;
    static List<MethodBase> todos;
    static Dictionary<string, List<MethodBase>> porNombre;
    static Dictionary<MethodBase, List<string>> nombresDe;
    static readonly string[] AVISOS = { "CambioElPanelDeReposo", "CambioElAcentoDelPanel" };

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        MethodInfo bip = TF.GetMethod("BuildIdlePanel", UNO);
        if (bip == null)
        {
            // Perfil sin tarjeta grafica: el panel de reposo no existe alli, y los avisos son metodos
            // parciales sin cuerpo, asi que el compilador se los lleva a ellos y a sus llamadas.
            Bien("sin tarjeta grafica no hay panel de reposo: nada que vigilar");
            return 0;
        }
        MethodInfo reloj = TF.GetMethod("ParcheaElReloj", UNO);
        if (reloj == null) { Mal("falta ParcheaElReloj: el reloj ya no tiene camino propio"); return 1; }

        todos = Codigo.TodosLosMetodos(TF);
        porNombre = new Dictionary<string, List<MethodBase>>();
        nombresDe = new Dictionary<MethodBase, List<string>>();
        foreach (MethodBase m in todos)
        {
            List<MethodBase> l;
            if (!porNombre.TryGetValue(m.Name, out l)) { l = new List<MethodBase>(); porNombre[m.Name] = l; }
            l.Add(m);
        }

        // ---------- 1) LA PUERTA: UN CONTADOR, NO UN RELOJ ----------
        Console.WriteLine("\n  1) EL PANEL YA NO SE PREGUNTA CADA 400 ms\n");
        int puerta = Puerta(bip);
        Exige(puerta >= 0, "el panel tiene la puerta del contador (escribe idlePanelDatosDe)");
        if (puerta < 0) { Console.WriteLine("\n  FALLOS: " + fallos + " (sin la puerta no se puede mirar lo demas)"); return fallos; }
        Exige(TF.GetField("idlePanelCheck", UNO) == null,
              "ya no queda el reloj de los 400 ms (idlePanelCheck)");
        Exige(!Codigo.Llama(bip, "get_TickCount"), "y el panel no mira el reloj del sistema para decidir");
        Exige(Lee(bip, "idlePanelSig", puerta),
              "LA FIRMA SE QUEDA de segundo filtro: un aviso de mas cuesta 0,005 ms, no un redibujo de 1,70");

        // ---------- 2) QUE ENSENA EL PANEL, SACADO DEL PANEL ----------
        Console.WriteLine("\n  2) QUE ENSENA EL PANEL (leido del programa, no de una lista)\n");
        List<string> perdonados = new List<string>();
        List<string> cosas = Ingredientes(bip, reloj, puerta, perdonados);
        CintasDelPanel(cosas, perdonados);
        Console.Write("     detras de la puerta:");
        foreach (string c in cosas) Console.Write(" " + c);
        Console.WriteLine();
        Console.Write("     con camino propio y perdonados:");
        foreach (string c in perdonados) Console.Write(" " + c);
        Console.WriteLine();
        Exige(cosas.Count >= 5, "el panel ensena " + cosas.Count + " cosas que solo se releen cuando hay aviso");

        // ---------- 3) CADA COSA TIENE SU AVISO ----------
        Console.WriteLine("\n  3) CADA UNA DE ESAS COSAS AVISA CUANDO CAMBIA\n");
        foreach (string campo in cosas) MiraElCampo(campo);

        // ---------- 3b) LOS DOS CENTROS POR LOS QUE PASA "ALGO CAMBIO" ----------
        // POR QUE ESTOS DOS VAN POR SU NOMBRE Y LO DE ARRIBA NO.
        //
        // Leyendo el programa compilado no se distingue una llamada que SIEMPRE corre de una que solo
        // corre dentro de un si. Eso hace que la comprobacion de arriba se pueda dar por contenta con un
        // aviso que casi nunca salta: lo probe quitando el de RefrescaCola y se quedo verde, porque los
        // cuatro sitios que tocan la cola llaman ademas a UpdateNowPlayingUI. Puede que asi la cola se
        // salvara igual; puede que no, segun por que rama entre. No es sitio para un "puede".
        //
        // RefrescaCola y AplicaEfectosGpu son los dos sitios por los que TODO el programa dice "la cola
        // cambio" y "las perillas cambiaron". El aviso va ahi, y esto lo exige por su nombre.
        Console.WriteLine("\n  3b) LOS DOS SITIOS POR LOS QUE PASA TODO\n");
        Centro("RefrescaCola", "la cola y la cancion pasan por aqui");
        Centro("AplicaEfectosGpu", "las perillas de la tarjeta pasan por aqui");

        // ---------- 4) LAS CARATULAS, QUE NO SON UN CAMPO ----------
        // El panel no guarda las caratulas en ningun sitio: se las pide a LoadCoverAsync, y esa respuesta
        // CAMBIA SOLA cuando el hilo de las portadas termina de leer una del disco. O sea que lo que tiene
        // que avisar no es un campo, es quien publica las que van llegando.
        Console.WriteLine("\n  4) UNA CARATULA QUE LLEGA TARDE TAMBIEN AVISA\n");
        Exige(Codigo.Llama(bip, "LoadCoverAsync"), "el panel pide las caratulas al vuelo (LoadCoverAsync)");
        MethodBase publica = null;
        foreach (MethodBase m in todos)
            if (Codigo.Llama(m, "ApplyArrivedCover")) { publica = m; break; }
        if (publica == null) Mal("no encuentro quien publica las caratulas que llegan tarde");
        else Exige(Avisa(publica), "quien las publica (" + publica.Name + ") avisa al panel");

        // ---------- 5) EL RELOJ VA POR SU CUENTA ----------
        Console.WriteLine("\n  5) EL RELOJ SE PARCHEA SOLO, SIN REHACER EL PANEL\n");
        Exige(Codigo.Llama(reloj, "UpdateIdleClockRegion"), "el parche del reloj repinta solo el recuadro de los digitos");
        Exige(!Codigo.Llama(reloj, "RenderIdlePanelLayout"), "y NO rehace el panel entero por un digito");
        Exige(Veces(bip, "ParcheaElReloj") >= 2,
              "los dos caminos cortos del panel parchean el reloj (los " + Veces(bip, "ParcheaElReloj") + ")");
        // La tarjeta sube a la textura SOLO ese rectangulo (UpdateOverlayRegion). Sin marcarlo, subiria
        // el panel entero cada segundo por un digito del reloj.
        Exige(Escritos(reloj).Contains("IdlePanelDirty"), "y le marca a la tarjeta que solo cambio esa franja");
        Exige(Escritos(reloj).Contains("IdlePanelVersion"), "y le sube la version, que es lo que la hace mirar");

        // ---------- 6) EL COLOR ANIMADO NO PUEDE PROVOCAR UNA TORMENTA ----------
        Tormenta();

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    // ================================================================================================
    //  LA PUERTA: el sitio donde el panel se da por enterado (escribe idlePanelDatosDe). Lo que se lee
    //  ANTES se relee en cada cuadro y no puede quedarse viejo; lo de DESPUES solo se relee cuando hay
    //  aviso, y es lo que hay que vigilar.
    // ================================================================================================
    static int Puerta(MethodBase m)
    {
        Module mod = m.Module;
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op2 >= 0 || p.Op != 0x7D) continue;   // stfld
            try { FieldInfo f = mod.ResolveField(p.Numero); if (f != null && f.Name == "idlePanelDatosDe") return p.Donde; }
            catch { }
        }
        return -1;
    }

    static List<string> Ingredientes(MethodBase bip, MethodBase reloj, int puerta, List<string> perdonados)
    {
        // Lo que el panel escribe el mismo -su firma, su mapa, su version- es contabilidad suya, no algo
        // que le cuenten. Y lo del reloj tiene su propio camino.
        List<string> suyos = Escritos(bip);
        foreach (string s in Escritos(reloj)) if (!suyos.Contains(s)) suyos.Add(s);
        foreach (string s in Leidos(reloj, 0)) if (!suyos.Contains(s)) suyos.Add(s);
        // La cache propia del reloj vive en su ayudante: sus escrituras tambien son contabilidad,
        // no datos externos del panel. No se perdonan las entradas que el ayudante solo lee.
        foreach (MethodBase d in Llamados(reloj, 0))
            foreach (string s in Escritos(d)) if (!suyos.Contains(s)) suyos.Add(s);

        List<string> crudo = Leidos(bip, puerta);
        // Y LO QUE LEEN LOS METODOS A LOS QUE LLAMA, un escalon. Sin esto se escaparia lo que el panel no
        // guarda en un campo suyo sino que le pregunta a otro (en la rocola de XP la insignia es asi).
        foreach (MethodBase d in Llamados(bip, puerta))
        {
            if (d == reloj) continue;
            foreach (string s in Leidos(d, 0)) if (!crudo.Contains(s)) crudo.Add(s);
        }

        List<string> r = new List<string>();
        foreach (string s in crudo)
        {
            FieldInfo f = TF.GetField(s, UNO);
            if (f == null) { continue; }                       // no es un campo de la rocola (Color.Empty, PlayItem.Disc)
            if (f.FieldType.Name == "GpuPanel") { Apunta(perdonados, s); continue; }   // es el destino, no lo que se ensena
            if (suyos.Contains(s)) { Apunta(perdonados, s); continue; }
            r.Add(s);
        }
        return r;
    }
    static void Apunta(List<string> l, string s) { if (!l.Contains(s)) l.Add(s); }

    // LAS CINTAS DE LOS TITULOS DESLIZANTES (TextoMovilGpu.cs) van por grupos: cada horneado rellena el suyo mientras se
    // hace (CapturaTexto(n)) y la tarjeta las dibuja al vuelo. El 1 es del panel de reposo: lo rellena el mismo al
    // hornearse, asi que cambiarlo no es algo que haya que contarle. Soltarlas todas (SueltaCintasTexto) es solo al cerrar.
    // Se perdona el campo SOLO si eso se cumple; si otro horneado toma el grupo 1 o se sueltan en otro momento, falla.
    static void CintasDelPanel(List<string> cosas, List<string> perdonados)
    {
        if (!cosas.Contains("cintasTexto")) return;
        string dir = System.IO.Path.Combine("..", "Rocola");
        int delPanel = 0, deOtros = 0, sueltas = 0, alCerrar = 0;
        foreach (string arch in System.IO.Directory.GetFiles(dir, "*.cs"))
            foreach (string linea in System.IO.File.ReadAllLines(arch))
            {
                string l = linea.Trim();
                if (l.StartsWith("//")) continue;
                if (l.Contains("CapturaTexto(1)")) { if (System.IO.Path.GetFileName(arch) == "EstadoReposoGpu.cs") delPanel++; else deOtros++; }
                if (l.Contains("SueltaCintasTexto()") && !l.Contains("void SueltaCintasTexto")) { sueltas++; if (l.Contains("FormClosed")) alCerrar++; }
            }
        Exige(delPanel == 1 && deOtros == 0, "el grupo 1 de las cintas es solo del panel de reposo, que lo rellena al hornearse");
        Exige(sueltas >= 1 && sueltas == alCerrar, "y las cintas solo se sueltan todas al cerrar la ventana");
        if (delPanel == 1 && deOtros == 0 && sueltas == alCerrar) { cosas.Remove("cintasTexto"); Apunta(perdonados, "cintasTexto"); }
    }

    static void Centro(string metodo, string porque)
    {
        MethodInfo m = TF.GetMethod(metodo, UNO);
        if (m == null) { Mal("no encuentro " + metodo + ", que es por donde pasa todo"); return; }
        Exige(Nombres(m).Contains("CambioElPanelDeReposo"), metodo + " avisa al panel (" + porque + ")");
    }

    // ================================================================================================
    //  CADA COSA: quien la cambia tiene que avisar. Para un campo suelto, quien le escribe encima; para
    //  una lista -la cola-, quien le mete o le saca, que en el codigo compilado no es una escritura sino
    //  una llamada a Add/Insert/RemoveAt sobre ESE tipo exacto de lista.
    // ================================================================================================
    static void MiraElCampo(string campo)
    {
        FieldInfo f = TF.GetField(campo, UNO);
        bool lista = f.FieldType.IsGenericType && f.FieldType.Namespace == "System.Collections.Generic";
        // UN COLOR NO CAMBIA: SE DESLIZA, y por eso tiene aviso propio con umbral. Si a un color se le
        // pusiera el aviso normal, cada latido de la transicion rehaceria el panel. Asi que a los colores
        // se les exige EL DEL ACENTO, y ademas en el mismo metodo: un color pasa por aqui veinticuatro
        // veces por segundo y el umbral tiene que estar antes de nada.
        bool color = (f.FieldType == typeof(Color));
        List<MethodBase> cambian = new List<MethodBase>();
        foreach (MethodBase m in todos)
        {
            int lee, esc;
            Codigo.UsosDeCampo(m, campo, out lee, out esc);
            if (esc > 0) { cambian.Add(m); continue; }
            if (lista && lee > 0 && Muta(m, f.FieldType)) cambian.Add(m);
        }
        if (cambian.Count == 0) { Mal("nadie cambia " + campo + ": esta bateria esta mirando mal"); return; }
        int mudos = 0;
        foreach (MethodBase m in cambian)
        {
            if (color)
            {
                if (Nombres(m).Contains("CambioElAcentoDelPanel"))
                { Console.WriteLine("       " + campo.PadRight(20) + m.Name + " -> CambioElAcentoDelPanel"); continue; }
                if (SoloDelArranque(m)) { Console.WriteLine("       " + campo.PadRight(20) + m.Name + "  (solo en el arranque)"); continue; }
                Mal("desliza " + campo + " sin el aviso con umbral: " + m.DeclaringType.Name + "." + m.Name);
                mudos++; continue;
            }
            string ruta = Ruta(m);
            if (ruta != null) { Console.WriteLine("       " + campo.PadRight(20) + m.Name + ruta); continue; }
            if (SoloDelArranque(m)) { Console.WriteLine("       " + campo.PadRight(20) + m.Name + "  (solo en el arranque)"); continue; }
            Mal("cambia " + campo + " y el panel no se entera: " + m.DeclaringType.Name + "." + m.Name);
            mudos++;
        }
        if (mudos == 0)
            Bien(campo.PadRight(20) + " avisa desde los " + cambian.Count + " sitios que lo cambian");
    }

    static bool Muta(MethodBase m, Type tipo)
    {
        Module mod = m.Module;
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op2 >= 0) continue;
            if (p.Op != 0x28 && p.Op != 0x6F) continue;   // call / callvirt
            MethodBase d = null;
            try { d = mod.ResolveMethod(p.Numero); } catch { continue; }
            if (d == null || d.DeclaringType != tipo) continue;
            string n = d.Name;
            if (n == "Add" || n == "AddRange" || n == "Insert" || n == "InsertRange" || n == "Remove"
             || n == "RemoveAt" || n == "RemoveAll" || n == "RemoveRange" || n == "Clear"
             || n == "Sort" || n == "Reverse") return true;
        }
        return false;
    }

    // ¿LLEGA AL AVISO, Y LLEGA CERCA? Vale que avise el propio metodo que cambia la cosa, o el de al
    // lado al que llama: la cola no avisa desde PlayNext, avisa desde RefrescaCola, que es a quien
    // PlayNext llama en la linea siguiente. UN PASO, NO MAS, y esto no es tacaneria.
    //
    // Mirando mas hondo esta prueba se vuelve mentira, y lo comprobe quitando avisos a mano: sin el de
    // RefrescaCola seguia verde, porque mas abajo del todo alguien acababa avisando por otra cosa. Pero
    // ese aviso de mas abajo esta DENTRO DE UN SI -solo salta cuando cambia la insignia-, y leyendo el
    // programa compilado no se distingue una llamada que siempre corre de una que casi nunca. Con un
    // solo paso eso no puede pasar: el aviso esta a la vista, al lado de lo que cambia.
    static bool Avisa(MethodBase m) { return Alcanza(m, 0, new List<MethodBase>()); }
    static string Ruta(MethodBase m) { return Camino(m, 0, new List<MethodBase>()); }
    static string Camino(MethodBase m, int hondo, List<MethodBase> vistos)
    {
        if (hondo > 1 || vistos.Contains(m)) return null;
        vistos.Add(m);
        List<string> ll = Nombres(m);
        for (int i = 0; i < AVISOS.Length; i++) if (ll.Contains(AVISOS[i])) return " -> " + AVISOS[i];
        foreach (string n in ll)
        {
            List<MethodBase> hijos;
            if (!porNombre.TryGetValue(n, out hijos)) continue;
            foreach (MethodBase h in hijos)
            {
                string r = Camino(h, hondo + 1, vistos);
                if (r != null) return " -> " + n + r;
            }
        }
        return null;
    }
    static bool Alcanza(MethodBase m, int hondo, List<MethodBase> vistos)
    {
        if (hondo > 1 || vistos.Contains(m)) return false;
        vistos.Add(m);
        List<string> ll = Nombres(m);
        for (int i = 0; i < AVISOS.Length; i++) if (ll.Contains(AVISOS[i])) return true;
        foreach (string n in ll)
        {
            List<MethodBase> hijos;
            if (!porNombre.TryGetValue(n, out hijos)) continue;
            foreach (MethodBase h in hijos) if (Alcanza(h, hondo + 1, vistos)) return true;
        }
        return false;
    }

    // ================================================================================================
    //  EL PERDON, Y POR QUE NO ES UNA LISTA A MANO.
    //
    //  Leer la configuracion cambia 'estado transparente' y 'cola de doce' y no avisa. Esta bien: corre
    //  una sola vez, en OnShown, ANTES de que exista el panel de la tarjeta grafica, asi que no hay nada
    //  que pueda quedarse viejo. Lo mismo el constructor.
    //
    //  Pero eso deja de ser verdad en cuanto alguien llame a ese metodo desde otro sitio -un boton de
    //  "recargar configuracion", por ejemplo-, y entonces el panel se quedaria con lo de antes sin que
    //  nadie se entere. Por eso el perdon no se escribe aqui con nombres: se COMPRUEBA, subiendo por
    //  quien llama a quien hasta el arranque. El dia que aparezca otro camino, esto se pone rojo solo.
    // ================================================================================================
    static bool SoloDelArranque(MethodBase m) { return DelArranque(m, 0, new List<MethodBase>()); }
    static bool DelArranque(MethodBase m, int hondo, List<MethodBase> vistos)
    {
        if (m.IsConstructor || m.Name == "OnShown") return true;
        if (hondo > 4 || vistos.Contains(m)) return false;
        vistos.Add(m);
        bool alguno = false;
        foreach (MethodBase q in todos)
        {
            if (q == m || !Nombres(q).Contains(m.Name)) continue;
            alguno = true;
            if (!DelArranque(q, hondo + 1, vistos)) return false;
        }
        return alguno;
    }

    // ================================================================================================
    //  6) EL COLOR DEL GENERO NO CAMBIA: SE DESLIZA.
    //
    //  En cada latido del reloj de la animacion -24 por segundo- el color se acerca un 16% al de la
    //  cancion nueva. Avisar en cuanto se mueve un punto seria rehacer el panel en casi todos esos
    //  latidos: unos 2,17 ms cada uno durante mas de un segundo. Peor que lo que se venia a arreglar.
    //
    //  Por eso el aviso del acento lleva umbral. Aqui se le da la transicion DE VERDAD -con el mismo
    //  LerpColor del programa y el mismo 16%- y se cuentan los avisos.
    // ================================================================================================
    static void Tormenta()
    {
        Console.WriteLine("\n  6) EL COLOR ANIMADO NO PROVOCA UNA TORMENTA DE REDIBUJOS\n");
        MethodInfo aviso = TF.GetMethod("CambioElAcentoDelPanel", UNO);
        MethodInfo lerp = TF.GetMethod("LerpColor", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo datos = TF.GetField("idlePanelDatos", UNO);
        if (aviso == null || lerp == null || datos == null)
        {
            Mal("falta CambioElAcentoDelPanel, LerpColor o idlePanelDatos");
            return;
        }
        object f = Codigo.FormSinEncender(TF);

        // Una transicion entera, como la del programa: del azul de noche al rojo de una ranchera.
        Color desde = Color.FromArgb(255, 20, 30, 60), hasta = Color.FromArgb(255, 200, 60, 40);
        List<Color> paso = new List<Color>();
        Color c = desde;
        for (int i = 0; i < 200; i++)
        {
            c = (Color)lerp.Invoke(null, new object[] { c, hasta, 0.16 });
            paso.Add(c);
            if (Math.Abs(c.R - hasta.R) <= 2 && Math.Abs(c.G - hasta.G) <= 2 && Math.Abs(c.B - hasta.B) <= 2) break;
        }
        int n = Cuenta(f, aviso, datos, paso.ToArray());
        Console.WriteLine("     una transicion entera: " + paso.Count + " latidos -> " + n + " redibujos  ("
                          + (n * 1.70).ToString("0.0") + " ms de panel, en vez de " + (paso.Count * 1.70).ToString("0.0") + ")");
        Exige(n < paso.Count, "el umbral quita redibujos de la transicion (" + n + " en vez de " + paso.Count + ")");
        Exige(n <= 17, "y una transicion entera no pasa de 17 redibujos (fueron " + n + ")");
        Exige(n >= 3, "pero el color SI se ve moverse: mas de 3 redibujos en la transicion (fueron " + n + ")");

        // Quieto de verdad: ni uno.
        Color[] igual = new Color[60];
        for (int i = 0; i < igual.Length; i++) igual[i] = hasta;
        Exige(Cuenta(f, aviso, datos, igual) == 0, "con el color ya puesto, sesenta latidos no rehacen nada");

        // Un salto de golpe: uno, y al momento.
        Exige(Cuenta(f, aviso, datos, new Color[] { Color.FromArgb(255, 10, 200, 90) }) == 1,
              "un cambio de color de golpe se ve al momento, en un solo redibujo");

        // La deriva lenta no se puede perder: de uno en uno, 240 pasos, tiene que acabar notandose.
        Color[] deriva = new Color[240];
        for (int i = 0; i < deriva.Length; i++) deriva[i] = Color.FromArgb(255, 10, 200, 90 + ((i + 1) % 160));
        int d = Cuenta(f, aviso, datos, deriva);
        Console.WriteLine("     deriva de uno en uno: " + deriva.Length + " latidos -> " + d + " redibujos");
        Exige(d >= 10, "una deriva lenta NO se pierde: acaba avisando (" + d + " veces)");
        Exige(d <= 90, "y aun asi no se rehace en cada latido (" + d + " de " + deriva.Length + ")");
    }

    static int Cuenta(object f, MethodInfo aviso, FieldInfo datos, Color[] ramp)
    {
        int antes = (int)datos.GetValue(f);
        for (int i = 0; i < ramp.Length; i++) aviso.Invoke(f, new object[] { ramp[i] });
        return (int)datos.GetValue(f) - antes;
    }

    // ================================================================================================
    //  Lectura del programa compilado (encima de pruebas/leercodigo.cs)
    // ================================================================================================
    static List<string> Nombres(MethodBase m)
    {
        List<string> r;
        if (nombresDe.TryGetValue(m, out r)) return r;
        r = Codigo.Nombres(m);
        nombresDe[m] = r;
        return r;
    }
    static int Veces(MethodBase m, string que)
    {
        int n = 0;
        foreach (string s in Nombres(m)) if (s == que) n++;
        return n;
    }
    static bool Lee(MethodBase m, string campo, int desde) { return Leidos(m, desde).Contains(campo); }
    static List<string> Leidos(MethodBase m, int desde)
    {
        List<string> r = new List<string>();
        Module mod = m.Module;
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op2 >= 0 || p.Donde < desde) continue;
            if (p.Op != 0x7B && p.Op != 0x7E) continue;   // ldfld / ldflda
            try { FieldInfo f = mod.ResolveField(p.Numero); if (f != null && !r.Contains(f.Name)) r.Add(f.Name); }
            catch { }
        }
        return r;
    }
    static List<string> Escritos(MethodBase m)
    {
        List<string> r = new List<string>();
        Module mod = m.Module;
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op2 >= 0 || p.Op != 0x7D) continue;   // stfld
            try { FieldInfo f = mod.ResolveField(p.Numero); if (f != null && !r.Contains(f.Name)) r.Add(f.Name); }
            catch { }
        }
        return r;
    }
    static List<MethodBase> Llamados(MethodBase m, int desde)
    {
        List<MethodBase> r = new List<MethodBase>();
        Module mod = m.Module;
        foreach (Codigo.Paso p in Codigo.Pasos(m))
        {
            if (p.Op2 >= 0 || p.Donde < desde) continue;
            if (p.Op != 0x28 && p.Op != 0x6F) continue;
            MethodBase d = null;
            try { d = mod.ResolveMethod(p.Numero); } catch { continue; }
            if (d == null || d.DeclaringType != TF || d.IsStatic || r.Contains(d)) continue;
            r.Add(d);
        }
        return r;
    }
}
