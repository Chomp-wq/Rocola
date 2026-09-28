using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// QUE LA VENTANA DE Ctrl+F3 QUEPA, Y QUE NINGUN MANDO SE QUEDE SIN PESTAÑA.
//
// La ventana mide 960x766 a proposito: con su barra de titulo entra justo en una pantalla de 1024x768,
// que es la del bar. Dentro no sobra un pixel, y por eso cada vez que se le añade algo hay que volver a
// medir. Ya paso una vez: los quince interruptores nuevos se apilaron todos en "Imagen", la tarjeta
// crecio a 1026 px dentro de una pagina de 636 y hubo que ponerle barra de desplazamiento. Una lista que
// hay que desplazar para ver es una lista donde la mitad de las cosas no existen: se ven cuando alguien
// las busca, y nadie busca lo que no sabe que esta.
//
// Aqui se arman las paginas DE VERDAD -las mismas que monta la ventana- y se miden. Contra un dibujo a
// mano esto no valdria nada; contra las de verdad, si alguien vuelve a llenar una, salta aqui.
class Pestanas
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static MethodInfo Met(string n)
    {
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }

    // Arma una pagina con la instancia sin encender: no hace falta la rocola entera para medir cajas.
    static Panel Arma(object f, string nombre)
    {
        MethodInfo m = Met(nombre);
        if (m == null) return null;
        Font fNom = new Font("Segoe UI", 9.5f), fVal = new Font("Segoe UI", 11f, FontStyle.Bold);
        Font fDes = new Font("Segoe UI", 8.25f), fBot = new Font("Segoe UI", 9f);
        Color c1 = Color.White, c2 = Color.Gray, c3 = Color.DimGray;
        ParameterInfo[] ps = m.GetParameters();
        List<object> a = new List<object>();
        foreach (ParameterInfo pi in ps)
        {
            if (pi.ParameterType == typeof(Font))
                a.Add(pi.Name == "fVal" ? fVal : (pi.Name == "fDes" ? fDes : (pi.Name == "fBot" ? fBot : fNom)));
            else a.Add(pi.Name == "cTexto" ? c1 : (pi.Name == "cTenue" ? c2 : c3));
        }
        try { return (Panel)m.Invoke(f, a.ToArray()); }
        catch (Exception e) { Console.WriteLine("     (no se pudo armar " + nombre + ": " + e.InnerException + ")"); return null; }
    }

    // El punto mas bajo que ocupa algo dentro de la pagina.
    static int Fondo(Control p)
    {
        int max = 0;
        foreach (Control c in p.Controls) if (c.Bottom > max) max = c.Bottom;
        return max;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }
        object f = Codigo.FormSinEncender(TF);

        Console.WriteLine("\n  1) LAS PAGINAS CABEN SIN BARRA DE DESPLAZAMIENTO\n");
        string[] pagina = { "PaginaImagen", "PaginaDetalles", "PaginaColores", "PaginaFondo", "PaginaPantalla" };
        int mirados = 0;
        foreach (string nom in pagina)
        {
            Panel pg = Arma(f, nom);
            if (pg == null) { Check(false, nom + ": se puede armar para medirla"); continue; }
            mirados++;
            int fondo = Fondo(pg);
            Console.WriteLine("     " + nom + ": lo mas bajo llega a " + fondo + " de " + pg.Height);
            Check(fondo <= pg.Height, nom + " entra entera (" + fondo + " de " + pg.Height + ")");
            Check(!pg.AutoScroll, nom + " no necesita barra de desplazamiento");
            int ancho = 0; foreach (Control c in pg.Controls) if (c.Right > ancho) ancho = c.Right;
            Check(ancho <= pg.Width, nom + " tampoco se sale por el costado (" + ancho + " de " + pg.Width + ")");
        }
        Check(mirados >= 5, "se midieron las cinco paginas (si no, esta prueba no probaria nada)");

        Console.WriteLine("\n  2) NINGUN MANDO SE QUEDA SIN PESTAÑA\n");
        // Cada bandera de un efecto tiene que salir en ALGUNA pagina. Mover una de pestaña y olvidarse de
        // ponerla en la nueva la dejaria viva en el archivo de configuracion y muerta en la ventana: el
        // operador no podria tocarla, y el que la escribio creeria que si.
        string[] mando = {
            "hojeoLigero", "progresoNeon", "muroReposo", "nebulosasCine", "vinetaSuave", "haloSeleccion",
            "estrellasFinas", "ondasSuaves", "progresoFluido", "estadoExpandido", "neonIntenso",
            "estadoTransparente", "cancionesTransparentes", "cieloProfundo",
            "auroraBandas", "nebNaturalFuerza",
            "portadaEnfocada", "fichaDisco", "paginaDesliza", "letrasLupa",   // la pestaña de la pantalla
            "listaGrande", "eqSonando", "colaMinutos", "vueloCola",                  // y su tarjeta de dentro del disco (el vinilo se retiro el 24/09)
            "suenaGrande", "monedaCae", "sinCreditosInvita", "avisosClaros",         // y la de lo que suena y los creditos
            "escaparate", "topNoche",                                                // y la del modo atraccion
            "letraLejos", "colorGenero",                                             // y la del estilo general
            "buscadorPortadas",                                                      // y la del buscador
        };
        // Y el vinilo retirado (24/09) no puede volver a tener mando sin que alguien lo decida: si reaparece, se avisa.
        Check(TF.GetField("viniloDisco", TODO) == null, "el vinilo retirado el 24/09 no tiene mando (ni campo)");
        List<string> huerfanos = new List<string>();
        foreach (string campo in mando)
        {
            if (TF.GetField(campo, TODO) == null) { huerfanos.Add(campo + " (ni existe)"); continue; }
            bool enAlguna = false;
            foreach (string nom in pagina)
            {
                if (Met(nom) == null) continue;
                if (Codigo.EscribeCampo(Codigo.MetodosConAyudantes(TF, nom), campo)) { enAlguna = true; break; }
            }
            if (!enAlguna) huerfanos.Add(campo);
        }
        if (huerfanos.Count > 0)
            foreach (string h in huerfanos) Console.WriteLine("     sin pestaña donde tocarlo:  " + h);
        Check(huerfanos.Count == 0, "los " + mando.Length + " mandos del fondo se pueden tocar desde alguna pestaña");

        Console.WriteLine("\n  3) LA TIRA DE PESTAÑAS NO SE SALE NI PISA EL TITULO\n");
        // Las pestañas viven en el renglon del titulo, no en una fila propia: la ventana esta ocupada del
        // primer pixel al ultimo y una fila mas habria obligado a encoger algo que ya funciona. Por eso
        // añadir una pestaña es apretar las que hay, y por eso hay que medirlo.
        bool laMonta = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "PaginaFondo")) laMonta = true;
        Check(laMonta, "la ventana monta la pestaña del fondo (si no, no se llega a ella)");
        // Las posiciones se leen del CODIGO y no del programa compilado: los botones de pestaña se arman
        // dentro del metodo que abre la ventana, y abrir la ventana de verdad aqui pediria la rocola
        // entera. La linea dice donde va cada uno, que es justo lo que hay que medir.
        int anchoBoton = 0;
        int[] xs = LeePestanas(Codigo.FuenteDeForm1(), out anchoBoton);
        if (xs != null && xs.Length > 0)
        {
            Array.Sort(xs);
            Console.WriteLine("     pestañas en x = " + string.Join(", ", Array.ConvertAll(xs, delegate(int v) { return v.ToString(); }))
                              + "  (ancho " + anchoBoton + ")");
            Check(xs.Length >= 6, "hay al menos seis pestañas (" + xs.Length + ")");
            Check(xs[xs.Length - 1] + anchoBoton <= 960, "la ultima acaba dentro de la ventana ("
                  + (xs[xs.Length - 1] + anchoBoton) + " de 960)");
            // El titulo es una etiqueta de 284 px que empieza en 22, o sea que acaba en 306. Que reclame
            // menos de lo que ocupa la pestaña no es cosmetico: dos controles que dicen ser dueños del
            // mismo sitio se pintan uno encima del otro y cual gana depende del orden en que se añadieron.
            int finTitulo = FinDelTitulo(Codigo.FuenteDeForm1());
            Console.WriteLine("     el titulo acaba en " + finTitulo);
            Check(finTitulo > 0 && xs[0] >= finTitulo, "y la primera no pisa el titulo, que acaba en "
                  + finTitulo + " (la pestaña empieza en " + xs[0] + ")");
            bool solapan = false;
            for (int i = 1; i < xs.Length; i++) if (xs[i] < xs[i - 1] + anchoBoton) solapan = true;
            Check(!solapan, "y ninguna se encima con la de al lado");
        }
        else Check(false, "se pueden leer las posiciones de las pestañas");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    // Donde acaba la caja del titulo: "lTit.SetBounds(x, 10, ancho, 28)".
    static int FinDelTitulo(string src)
    {
        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
            src, @"lTit\.SetBounds\((\d+),\s*\d+,\s*(\d+),");
        return m.Success ? int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[2].Value) : 0;
    }

    // Saca las x de los botones de pestaña del codigo fuente: "bPagX.SetBounds(x, 8, ancho, 30)".
    static int[] LeePestanas(string src, out int ancho)
    {
        ancho = 0;
        List<int> xs = new List<int>();
        System.Text.RegularExpressions.MatchCollection ms =
            System.Text.RegularExpressions.Regex.Matches(src, @"bPag\w+\.SetBounds\((\d+),\s*8,\s*(\d+),\s*30\)");
        foreach (System.Text.RegularExpressions.Match m in ms)
        {
            xs.Add(int.Parse(m.Groups[1].Value));
            ancho = int.Parse(m.Groups[2].Value);
        }
        return xs.ToArray();
    }
}
