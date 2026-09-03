using System;
using System.Collections.Generic;
using System.Reflection;

// LOS DETALLES, CON MANDO.
//
// Los seis adornos que se le pusieron a la rocola -el turno en la cola, el palomito de "ya la pusiste",
// el fondo con el tono de la portada, las portaditas de la cola, el relevo del cubito y la pared que
// respira- solo se podian apagar escribiendo a mano en el archivo de configuracion. Para quien no toca
// archivos de texto, eso es lo mismo que no poder apagarlos.
//
// Y no cabian en la ventana que ya existia: la tarjeta EFECTOS mide 568 px y su contenido llega al 562,
// y las dos columnas de la pagina de Imagen acaban en 572 de 636. Estan llenas, medido. Asi que los
// detalles tienen pestaña propia, que ademas es su sitio: no son perillas de rendimiento que el medidor
// pueda proponer, ni son sonido. Son adornos que se encienden y se dejan.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LA PAGINA EXISTA Y SE ARME EN SU PROPIA FUNCION, como la de Imagen: asi la de Sonido no se
//      entera de nada y sigue exactamente donde estaba.
//   2) QUE CADA INTERRUPTOR ESTE ATADO A SU ADORNO. Un interruptor que se mueve y no apaga nada es peor
//      que no tenerlo: el operador cree que lo apago y sigue viendolo.
//   3) QUE LO QUE SE TOQUE SE GUARDE. Si no, al cerrar la ventana se pierde y vuelve al abrir.
//   4) QUE LA FUERZA DE LA RESPIRACION PASE POR DONDE SE ACOTA. Es la unica de las siete que lleva un
//      numero, y un numero grande ahi marea a todo el bar.
//   5) QUE EL TURNO SE PUEDA APAGAR DE VERDAD (era el unico de los seis que no tenia ni clave).
class Detalles
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

    // Los metodos de la pagina: el suyo propio MAS los que el compilador le saca aparte para cada
    // delegado (los que llevan el nombre entre picos). Sin esto, todo lo que hace un interruptor al
    // moverse quedaria escondido dentro de un ayudante y no se veria desde aqui.
    static List<MethodBase> DeLaPagina()
    {
        List<MethodBase> r = new List<MethodBase>();
        const string picos = "<PaginaDetalles>";
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            string n = m.Name;
            string t = (m.DeclaringType != null) ? m.DeclaringType.Name : "";
            if (n == "PaginaDetalles"
                || n.IndexOf(picos, StringComparison.Ordinal) >= 0
                || t.IndexOf(picos, StringComparison.Ordinal) >= 0) r.Add(m);
        }
        return r;
    }
    static bool LaPaginaEscribe(string campo)
    {
        foreach (MethodBase m in DeLaPagina())
        {
            int lee, esc;
            Codigo.UsosDeCampo(m, campo, out lee, out esc);
            if (esc > 0) return true;
        }
        return false;
    }
    static bool LaPaginaLee(string campo)
    {
        foreach (MethodBase m in DeLaPagina())
        {
            int lee, esc;
            Codigo.UsosDeCampo(m, campo, out lee, out esc);
            if (lee > 0) return true;
        }
        return false;
    }
    static bool LaPaginaLlama(string que)
    {
        foreach (MethodBase m in DeLaPagina()) if (Codigo.Llama(m, que)) return true;
        return false;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LA PAGINA\n");
        MethodBase pag = Met("PaginaDetalles");
        Check(pag != null, "los detalles tienen su propia pagina, armada aparte (PaginaDetalles)");
        if (pag == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }
        // Que alguien la abra: una pagina que nadie arma no la ve nunca nadie.
        bool laMonta = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (m != pag && Codigo.Llama(m, "PaginaDetalles")) laMonta = true;
        Check(laMonta, "y la ventana de Ctrl+F3 la monta (hay una tercera pestaña)");

        Console.WriteLine("\n  2) CADA INTERRUPTOR, ATADO A SU ADORNO\n");
        string[] campo = { "turnoOn", "marcaColaOn", "fondoPortadaOn", "colaPortadasOn", "relevoOn", "respiraOn" };
        string[] loQueEs = {
            "el turno que dice cuando suena tu cancion",
            "el palomito de \"ya la pusiste\" en la pared",
            "el fondo que toma el tono de la portada",
            "las portaditas de la capsula de la cola",
            "la media vuelta del cubito al cambiar de cancion",
            "la pared que respira",
        };
        for (int i = 0; i < campo.Length; i++)
        {
            Check(TF.GetField(campo[i], TODO) != null, loQueEs[i] + ": se puede apagar (" + campo[i] + ")");
            Check(LaPaginaEscribe(campo[i]), "   ...y su interruptor de la ventana lo apaga de verdad");
            // Y AL REVES: la ventana tiene que ABRIRSE diciendo la verdad. Un interruptor que sale siempre
            // encendido aunque el adorno este apagado hace que el operador crea que ya lo probo.
            Check(LaPaginaLee(campo[i]), "   ...y al abrir la ventana sale como este de verdad");
        }

        Console.WriteLine("\n  3) Y LO QUE SE TOCA, SE GUARDA\n");
        Check(LaPaginaEscribe("f3CambioGraf"),
              "al mover algo, la rocola se apunta que hay que guardar (si no, se pierde al cerrar)");

        Console.WriteLine("\n  4) LA FUERZA DE LA RESPIRACION, POR DONDE SE ACOTA\n");
        // Es la unica de las siete que lleva un numero, y un numero grande ahi marea a todo el bar. Se le
        // pone SOLO por la funcion que acota (la misma que lee el archivo), nunca a mano.
        Check(LaPaginaEscribe("respiraFuerza"), "la ventana puede cambiar cuanto respira la pared");
        Check(LaPaginaLlama("FuerzaRespira"), "y lo hace por la funcion que acota, no poniendo el numero a mano");

        Console.WriteLine("\n  5) EL TURNO SE APAGA DE VERDAD\n");
        // Era el unico de los seis que no tenia ni clave en el archivo: se veia siempre.
        FieldInfo fTurno = TF.GetField("turnoOn", TODO);
        MethodInfo mTexto = (MethodInfo)Met("TextoTurno");
        Check(mTexto != null, "sigue habiendo un solo sitio que escribe el letrero del turno");
        if (fTurno != null && mTexto != null)
        {
            object antes = fTurno.GetValue(null);
            try
            {
                fTurno.SetValue(null, true);
                string encendido = (string)mTexto.Invoke(null, new object[] { 3, 615.0 });
                fTurno.SetValue(null, false);
                string apagado = (string)mTexto.Invoke(null, new object[] { 3, 615.0 });
                Console.WriteLine("     encendido -> \"" + encendido + "\"");
                Console.WriteLine("     apagado   -> \"" + apagado + "\"");
                Check(encendido.Length > 0, "encendido, el turno dice el puesto y los minutos");
                Check(apagado.Length == 0, "y apagado no dice nada: el aviso vuelve a ser \"Cancion agregada\"");
            }
            finally { fTurno.SetValue(null, antes); }
        }

        // Y que lo lea del archivo, para que el operador pueda dejarlo puesto entre encendidas.
        MethodBase lee = Met("LoadConfigPrefs"), escribe = Met("SaveConfigPrefs");
        bool clave = false;
        if (lee != null) foreach (string s in Codigo.Textos(lee)) if (s == "turno=") clave = true;
        Check(clave, "y la rocola se acuerda al apagarla y encenderla (clave \"turno=\" en el archivo)");
        bool guarda = false;
        if (escribe != null) foreach (string s in Codigo.Textos(escribe)) if (s == "turno=") guarda = true;
        Check(guarda, "y la escribe al guardar");

        Console.WriteLine("\n  6) Y LA VENTANA DICE DE CUANDO ES LA ROCOLA\n");
        // Esto costo una tanda entera: se arreglo el buscador, se pidio una foto, la foto seguia igual... y
        // no habia forma de distinguir "esta roto" de "es la version de antes, que no se copio al bar". Las
        // dos cosas se ven exactamente igual desde fuera.
        //
        // La fecha sale del PROPIO ARCHIVO y no de un numero escrito a mano: un numero hay que acordarse de
        // subirlo, y el dia que se olvide la rocola mentira, que es peor que callarse.
        MethodInfo mCuando = (MethodInfo)Met("DeCuandoEs");
        Check(mCuando != null, "la rocola sabe decir de cuando es (DeCuandoEs)");
        if (mCuando != null)
        {
            string cuando = (string)mCuando.Invoke(null, null);
            Console.WriteLine("     este ejecutable dice ser del " + (string.IsNullOrEmpty(cuando) ? "(nada)" : cuando));
            Check(!string.IsNullOrEmpty(cuando), "y lo dice de verdad, no una cadena vacia");
            bool enLaVentana = false, enLaBitacora = false;
            foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            {
                if (!Codigo.Llama(m, "DeCuandoEs")) continue;
                if (Codigo.Llama(m, "Diag")) enLaBitacora = true;
                foreach (string t in Codigo.Textos(m)) if (t.IndexOf("Esta rocola es del", StringComparison.Ordinal) >= 0) enLaVentana = true;
            }
            Check(enLaVentana, "y sale en la ventana de Ctrl+F3, que es donde se mira");
            Check(enLaBitacora, "y se apunta en la bitacora al encender");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
