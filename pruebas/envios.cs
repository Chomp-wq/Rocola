using System;
using System.Collections.Generic;
using System.Reflection;

// CUANTAS VECES SE LE HABLA A LA TARJETA PARA PINTAR UN CUADRO.
//
// El apunte de la auditoria lo dejo con una condicion: "envios de geometria D3D9 SI EL PERFIL LO
// JUSTIFICA". Aqui no hay tarjeta grafica, asi que no se puede medir un solo cuadro de verdad ni saber
// si agrupar merece la pena. Lo que SI se puede hacer -y es lo que hace esta bateria- es CONTAR: leer el
// programa ya compilado y decir cuantos envios sale haciendo cada cuadro, y cuantos de ellos van dentro
// de un bucle.
//
// Por que se puede contar sin tarjeta: TODO el dibujo pasa por un solo embudo, D3D9Device.Draw. Cada
// llegada a ese embudo es un envio. Asi que se cuenta el camino, no el dibujo.
//
// Y por que importa el numero: lo dice el propio codigo, en el comentario de los halos agrupados: "en
// Direct3D 9 lo caro no es el relleno sino la LLAMADA". Cada envio lleva detras su cambio de textura y
// de estado de mezcla.
//
// ESTA BATERIA NO PIDE CAMBIAR NADA. Entrega la tabla para que se pueda medir en el equipo del bar con
// una pregunta concreta -cuanto cambian los FPS al bajar de tantos envios a tantos- en vez de a ciegas.
class Envios
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    // Un sitio del programa que le habla a la tarjeta.
    sealed class Sitio
    {
        public MethodBase Donde;      // en que metodo esta
        public string Que;            // a que llama
        public bool EnBucle;          // ¿una vez, o una por vuelta?
    }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type TDev = a.GetType("Rocola.D3D9Device", false);
        Type TStar = a.GetType("Rocola.GpuStarfield", false);   // el que envuelve al dispositivo y lo usa todo el dibujo
        if (TDev == null || TStar == null)
        {
            // Perfil sin tarjeta grafica: no hay nada que contar.
            Bien("sin tarjeta grafica no hay envios de geometria: nada que contar");
            return 0;
        }

        // ---------- 1) EL EMBUDO, ENCONTRADO SOLO ----------
        Console.WriteLine("\n  1) POR DONDE SE LE HABLA A LA TARJETA\n");
        MethodBase embudo = null;
        foreach (MethodBase m in TDev.GetMethods(TODO))
            if (m.Name == "Draw" && !m.IsPublic) embudo = m;
        if (embudo == null) { Mal("no encuentro el embudo D3D9Device.Draw"); return 1; }

        List<string> puertas = new List<string>();   // los metodos del dispositivo que llegan al embudo
        foreach (MethodBase m in TDev.GetMethods(TODO))
            if (m != embudo && Codigo.Llama(m, "Draw")) puertas.Add(m.Name);
        puertas.Sort();
        Console.Write("     llegan al embudo:");
        foreach (string p in puertas) Console.Write(" " + p);
        Console.WriteLine();
        Exige(puertas.Count >= 4, "se encuentran solas las " + puertas.Count + " puertas de dibujo (si aparece otra, se cuenta tambien)");

        // ---------- 2) TODOS LOS SITIOS QUE ENVIAN ----------
        Console.WriteLine("\n  2) DONDE SE ENVIA, Y SI ES UNA VEZ O UNA POR VUELTA\n");
        List<Sitio> sitios = new List<Sitio>();
        foreach (Type t in Codigo.ConLosDeDentro(TStar))
            foreach (MethodBase m in t.GetMethods(TODO)) Recoge(m, puertas, sitios);
        // Y el panel, que vive en el mismo archivo y es quien arma el cuadro.
        Type TPanel = a.GetType("Rocola.GpuPanel", false);
        List<string> envian = new List<string>();     // los metodos de D3D9Star que envian: el panel los usa
        foreach (Sitio s in sitios) if (!envian.Contains(s.Donde.Name)) envian.Add(s.Donde.Name);
        if (TPanel != null)
            foreach (Type t in Codigo.ConLosDeDentro(TPanel))
                foreach (MethodBase m in t.GetMethods(TODO)) Recoge(m, envian, sitios);

        int sueltos = 0, enBucle = 0;
        Dictionary<string, int> porMetodo = new Dictionary<string, int>();
        foreach (Sitio s in sitios)
        {
            if (s.EnBucle) enBucle++; else sueltos++;
            string k = s.Donde.DeclaringType.Name + "." + s.Donde.Name;
            int n; porMetodo.TryGetValue(k, out n); porMetodo[k] = n + 1;
        }
        List<string> claves = new List<string>(porMetodo.Keys);
        claves.Sort();
        foreach (string k in claves)
        {
            int bucles = 0;
            foreach (Sitio s in sitios)
                if ((s.Donde.DeclaringType.Name + "." + s.Donde.Name) == k && s.EnBucle) bucles++;
            Console.WriteLine("     " + k.PadRight(34) + porMetodo[k].ToString().PadLeft(3) + " sitio(s)"
                              + (bucles > 0 ? ("   de ellos " + bucles + " DENTRO DE UN BUCLE") : ""));
        }
        Console.WriteLine();
        Console.WriteLine("     total: " + sitios.Count + " sitios que envian  ->  " + sueltos
                          + " de una vez y " + enBucle + " dentro de un bucle");
        Exige(sitios.Count > 10, "se recorre el dibujo entero y salen " + sitios.Count + " sitios que envian");

        // ---------- 3) LA CUENTA DE UN CUADRO ----------
        Cuadro(TStar, a);

        // ---------- 4) LO QUE YA VA AGRUPADO, Y LO QUE NO ----------
        Agrupado(TStar, sitios);

        Console.WriteLine();
        Console.WriteLine("     LO QUE ESTA CUENTA NO DICE: cuanto cuesta CADA envio en la tarjeta del bar.");
        Console.WriteLine("     Eso son FPS reales de ese equipo y aqui no hay tarjeta. La tabla esta para");
        Console.WriteLine("     poder medirlo alli con una pregunta concreta, no para decidir por el.");
        Bien("y se dice lo que la cuenta NO decide, en vez de darlo por bueno");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN" : ("\n  FALLOS: " + fallos));
        return fallos;
    }

    static void Recoge(MethodBase m, List<string> aQue, List<Sitio> sitios)
    {
        List<Codigo.Paso> pasos = Codigo.Pasos(m);
        if (pasos.Count == 0) return;
        List<int[]> bucles = Codigo.Bucles(m);
        Module mod = m.Module;
        foreach (Codigo.Paso p in pasos)
        {
            if (p.Op2 >= 0 || (p.Op != 0x28 && p.Op != 0x6F)) continue;
            MethodBase d = null;
            try { d = mod.ResolveMethod(p.Numero); } catch { continue; }
            if (d == null || !aQue.Contains(d.Name)) continue;
            if (d == m) continue;
            Sitio s = new Sitio();
            s.Donde = m; s.Que = d.Name; s.EnBucle = Codigo.EnBucle(bucles, p.Donde);
            sitios.Add(s);
        }
    }

    // ================================================================================================
    //  LA CUENTA DE UN CUADRO, con los topes leidos del propio programa
    // ================================================================================================
    static void Cuadro(Type TStar, Assembly a)
    {
        Console.WriteLine("\n  3) LO QUE SALE UN CUADRO DE LA PARED DE DISCOS\n");
        Type TPanel = a.GetType("Rocola.GpuPanel", false);
        int maxCasillas = Constante(TPanel, "MaxCells");
        Type TForm = a.GetType("Rocola.Form1", false);
        int cols = Constante(TForm, "Cols"), filas = Constante(TForm, "Rows");
        if (maxCasillas <= 0) { Mal("no encuentro el tope de casillas de la pared (MaxCells)"); return; }

        Console.WriteLine("     el cielo, las estrellas y las fugaces        3 envios");
        Console.WriteLine("     las nieblas, la aurora y el ruido            2 a 4 envios");
        Console.WriteLine("     LA PARED DE DISCOS, una por casilla          hasta " + maxCasillas + " envios"
                          + ((cols > 0 && filas > 0) ? ("   (la pagina son " + cols + "x" + filas + " = " + (cols * filas)
                             + "; hojeando se ven dos)") : ""));
        Console.WriteLine("     el disco elegido y el del relevo (cubos)     hasta 2 envios (uno por cubo visible)");
        Console.WriteLine("     las franjas de arriba y de abajo             2 envios");
        Console.WriteLine("     TODOS los halos juntos                       1 envio");
        Console.WriteLine();
        Console.WriteLine("     Estimacion de este escenario: hasta " + (maxCasillas + 12) + " envios; pared: hasta "
                          + maxCasillas + ". No es un contador de un cuadro real.");
        Console.WriteLine("     Y cada casilla lleva SU PROPIA textura, asi que juntarlas de verdad pediria un");
        Console.WriteLine("     atlas: meter muchas portadas en una sola imagen. Es un cambio grande, y sin los");
        Console.WriteLine("     FPS del equipo del bar no se justifica.");
        Exige(maxCasillas > 0 && maxCasillas <= 64, "el tope de casillas de la pared es " + maxCasillas + ", leido del programa");
    }

    static int Constante(Type t, string nombre)
    {
        if (t == null) return 0;
        FieldInfo f = t.GetField(nombre, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null || !f.IsLiteral) return 0;
        try { return Convert.ToInt32(f.GetRawConstantValue()); } catch { return 0; }
    }

    // ================================================================================================
    //  LO QUE YA VA AGRUPADO: eso es lo que hay que conservar
    // ================================================================================================
    static void Agrupado(Type TStar, List<Sitio> sitios)
    {
        Console.WriteLine("\n  4) LO QUE YA VA AGRUPADO, Y TIENE QUE SEGUIR ASI\n");

        // LOS HALOS. GlowBatchAdd solo ACUMULA -no puede enviar- y GlowBatchEnd los suelta todos juntos.
        //
        // Se mira si LLEGA a enviar, no solo si envia el mismo: la primera version de esto preguntaba solo
        // por la llamada directa al dispositivo, y metiendole un DrawGlow por halo -que es exactamente el
        // error que esto tiene que cazar- se quedaba en verde, porque DrawGlow es un metodo de al lado.
        // Y se miran TODAS las versiones del metodo, no la primera que aparezca: GlowBatchAdd tiene dos
        // -una con radio redondo y otra con radio ancho y alto- y la primera solo le pasa el trabajo a la
        // segunda. Mirando solo una, el error se colaba por la otra. Tambien paso, tambien se arreglo.
        List<MethodBase> acumula = Metodos(TStar, "GlowBatchAdd");
        List<MethodBase> suelta = Metodos(TStar, "GlowBatchEnd");
        List<string> envian = new List<string>();
        foreach (Sitio s in sitios)
            if (s.Donde.DeclaringType == TStar && !envian.Contains(s.Donde.Name)) envian.Add(s.Donde.Name);
        bool acumulaEnvia = false;
        foreach (MethodBase m in acumula) if (LlegaAEnviar(m, TStar, envian, sitios, 0)) acumulaEnvia = true;
        bool sueltaEnvia = false;
        foreach (MethodBase m in suelta) if (Envia(m, sitios)) sueltaEnvia = true;
        Exige(acumula.Count > 0 && !acumulaEnvia, "juntar un halo mas NO le habla a la tarjeta: solo lo apunta");
        Exige(sueltaEnvia, "y al final se mandan TODOS de una sola vez");

        // LA AURORA. El relleno entero en una llamada, no una por triangulo.
        int enBucleAurora = 0, sueltosAurora = 0;
        foreach (Sitio s in sitios)
            if (s.Donde.Name.IndexOf("Aurora", StringComparison.OrdinalIgnoreCase) >= 0)
            { if (s.EnBucle) enBucleAurora++; else sueltosAurora++; }
        Exige(enBucleAurora == 0, "la aurora se manda entera de una vez, no un envio por triangulo");
        if (sueltosAurora > 0) Bien("(la aurora usa " + sueltosAurora + " envio(s) sueltos: el relleno y su brillo)");

        // LA PARED. Aqui esta el unico bucle que de verdad multiplica, y se deja dicho a proposito: no es
        // un descuido, es lo que hay que ir a medir al bar.
        int bucles = 0;
        foreach (Sitio s in sitios) if (s.EnBucle) bucles++;
        Console.WriteLine("     sitios que envian dentro de un bucle: " + bucles);
        Exige(bucles >= 1, "hay al menos un sitio que envia por vuelta (las casillas de la pared)");
        Exige(bucles <= 6, "y NO se han ido multiplicando: siguen siendo " + bucles
                           + ". Si este numero sube, alguien metio un envio nuevo dentro de un bucle");
    }

    static MethodBase Metodo(Type t, string nombre)
    {
        foreach (MethodBase m in t.GetMethods(TODO)) if (m.Name == nombre) return m;
        return null;
    }
    static List<MethodBase> Metodos(Type t, string nombre)
    {
        List<MethodBase> r = new List<MethodBase>();
        foreach (MethodBase m in t.GetMethods(TODO)) if (m.Name == nombre) r.Add(m);
        return r;
    }
    static bool Envia(MethodBase m, List<Sitio> sitios)
    {
        foreach (Sitio s in sitios) if (s.Donde == m) return true;
        return false;
    }

    // ¿Este metodo acaba hablandole a la tarjeta, por si mismo o por el de al lado al que llama?
    static bool LlegaAEnviar(MethodBase m, Type TStar, List<string> envian, List<Sitio> sitios, int hondo)
    {
        if (m == null || hondo > 2) return false;
        if (Envia(m, sitios)) return true;
        foreach (string n in Codigo.Nombres(m))
        {
            if (!envian.Contains(n)) continue;
            foreach (MethodBase d in Metodos(TStar, n))
                if (d != m && LlegaAEnviar(d, TStar, envian, sitios, hondo + 1)) return true;
        }
        return false;
    }
}
