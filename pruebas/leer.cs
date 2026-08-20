using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

// LAS PRUEBAS QUE LEEN EL PROGRAMA TIENEN QUE LEERLO BIEN.
//
// Media docena de baterias no aprietan botones: abren el ejecutable de la rocola y miran POR DENTRO lo que
// hace cada funcion, para comprobar que sigue enchufada donde debe. Es la unica forma de comprobar cosas
// como "la rocola avanza la deriva desde su latido" sin montar la rocola entera.
//
// Para eso hay que recorrer las instrucciones una por una, y cada una ocupa un largo distinto. Si el
// recorrido se equivoca en UNA, a partir de ahi lee desde el sitio equivocado, y lo que salga es basura.
// Y una prueba que lee basura no avisa: contesta que todo esta bien.
//
// Habia quince baterias leyendo asi, cada una con su copia del recorrido, y de las quince:
//
//   - siete NO recorrian nada: buscaban un byte suelto por todo el programa. Cualquier byte metido dentro
//     de un numero o de un texto vale lo mismo que "llamar a una funcion", asi que se inventaban llamadas
//     que no existen. Esas dicen OK sin haber comprobado nada.
//   - cuatro recorrian a medias y se desincronizaban a la primera instruccion de dos bytes.
//   - las cuatro "buenas" se saltaban dos instrucciones bastante comunes: coger la direccion de un
//     parametro, y el salto largo con el que se sale de un "intenta esto".
//
// Aqui se fabrican a proposito trozos de programa con esas trampas -sabiendo de antemano la respuesta
// correcta, porque los escribe esta misma prueba- y se comprueba que el lector compartido saca
// EXACTAMENTE las llamadas que hay y ninguna mas. Y luego se pasan el lector viejo y el nuevo por la
// rocola entera para ver en cuantos sitios de verdad no coinciden.
class Leer
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // ---------------- los lectores DE ANTES, para poder ensenar en que se equivocaban ----------------

    // El que no recorre: mira byte a byte y en cuanto encuentra uno que vale "llamar" resuelve los cuatro
    // siguientes. Es el que llevaban siete baterias.
    static List<string> ViejoBuscando(MethodBase m)
    {
        List<string> r = new List<string>();
        MethodBody b = null; try { b = m.GetMethodBody(); } catch { }
        if (b == null) return r;
        byte[] il = null; try { il = b.GetILAsByteArray(); } catch { }
        if (il == null) return r;
        for (int i = 0; i + 4 < il.Length; i++)
            if (il[i] == 0x28 || il[i] == 0x6F)
            {
                try { MethodBase mb = m.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)); if (mb != null) r.Add(mb.Name); }
                catch { }
            }
        return r;
    }

    // El que recorre a medias: tiene tabla de largos, pero da por hecho que las instrucciones de dos bytes
    // no llevan nada detras, y le faltan un par de largos mas. Es el que llevaban otras cuatro.
    static int LargoViejo(int op)
    {
        if (op >= 0x2B && op <= 0x37) return 1;
        if (op >= 0x38 && op <= 0x44) return 4;
        switch (op)
        {
            case 0x0E: case 0x10: case 0x11: case 0x12: case 0x13: case 0x1F: case 0xDE: return 1;
            case 0x20: case 0x22: case 0x27: case 0x28: case 0x6F: case 0x70: case 0x71:
            case 0x72: case 0x73: case 0x74: case 0x75: case 0x79:
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80:
            case 0x81: case 0x8C: case 0x8D: case 0x8F: case 0xA3: case 0xA4:
            case 0xA5: case 0xC2: case 0xC6: case 0xD0: case 0x29: return 4;
            case 0x21: case 0x23: return 8;
            default: return 0;
        }
    }
    static List<string> ViejoAndando(MethodBase m)
    {
        List<string> r = new List<string>();
        MethodBody b = null; try { b = m.GetMethodBody(); } catch { }
        if (b == null) return r;
        byte[] il = null; try { il = b.GetILAsByteArray(); } catch { }
        if (il == null) return r;
        Module mod = m.Module; int i = 0;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0xFE) { i += 2; continue; }
            if (op == 0x45) { if (i + 4 >= il.Length) break; int n = BitConverter.ToInt32(il, i + 1); if (n < 0 || n > (il.Length - i) / 4) break; i += 1 + 4 + 4 * n; continue; }
            int len = LargoViejo(op);
            if ((op == 0x28 || op == 0x6F) && i + 4 < il.Length)
            { try { MethodBase mb = mod.ResolveMethod(BitConverter.ToInt32(il, i + 1)); if (mb != null) r.Add(mb.Name); } catch { } }
            i += 1 + len;
        }
        return r;
    }

    // ---------------- los trozos de programa con trampa ----------------

    static Type Fabrica()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_leer");
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        Directory.CreateDirectory(dir);
        AssemblyName an = new AssemblyName("ConTrampa");
        AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave, dir);
        ModuleBuilder mod = ab.DefineDynamicModule("ConTrampa", "ConTrampa.dll");
        TypeBuilder tb = mod.DefineType("Casos", TypeAttributes.Public);
        MethodInfo marca = typeof(Leer).GetMethod("Marca", BindingFlags.Public | BindingFlags.Static);
        MethodInfo otra = typeof(Leer).GetMethod("Otra", BindingFlags.Public | BindingFlags.Static);

        // 1) UN NUMERO GRANDE CUYOS BYTES PARECEN UNA LLAMADA.
        //    Los ocho bytes del numero llevan dentro el byte de "llamar" seguido de algo que se puede
        //    resolver. Quien no recorra se inventara una llamada que no existe.
        {
            MethodBuilder m = tb.DefineMethod("NumeroQueParece", MethodAttributes.Public | MethodAttributes.Static, typeof(long), Type.EmptyTypes);
            ILGenerator g = m.GetILGenerator();
            g.Emit(OpCodes.Call, marca);                       // esta SI es una llamada
            g.Emit(OpCodes.Ldc_I8, unchecked((long)0x0A00000128L));
            g.Emit(OpCodes.Ret);
        }

        // 2) COGER LA DIRECCION DE UN PARAMETRO CON NUMERO ALTO.
        //    Esa instruccion lleva un byte detras, y a los lectores de antes les faltaba en la tabla: se
        //    comen el byte siguiente como si fuera otra instruccion y se pierden la llamada de despues.
        {
            Type[] muchos = new Type[40];
            for (int k = 0; k < muchos.Length; k++) muchos[k] = typeof(int);
            MethodBuilder m = tb.DefineMethod("DireccionDeParametro", MethodAttributes.Public | MethodAttributes.Static, typeof(void), muchos);
            ILGenerator g = m.GetILGenerator();
            g.Emit(OpCodes.Ldarga_S, (byte)33);                // 0x21 detras: para el lector viejo, un numero de 8 bytes
            g.Emit(OpCodes.Pop);
            g.Emit(OpCodes.Call, marca);
            g.Emit(OpCodes.Ret);
        }

        // 3) LA TABLA DE SALTO, que ocupa tanto como casos tenga.
        {
            MethodBuilder m = tb.DefineMethod("ConTablaDeSalto", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new Type[] { typeof(int) });
            ILGenerator g = m.GetILGenerator();
            Label a = g.DefineLabel(), b2 = g.DefineLabel(), c = g.DefineLabel(), fin = g.DefineLabel();
            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Switch, new Label[] { a, b2, c });
            g.Emit(OpCodes.Br, fin);
            g.MarkLabel(a); g.Emit(OpCodes.Call, marca); g.Emit(OpCodes.Br, fin);
            g.MarkLabel(b2); g.Emit(OpCodes.Call, otra); g.Emit(OpCodes.Br, fin);
            g.MarkLabel(c); g.Emit(OpCodes.Call, marca);
            g.MarkLabel(fin);
            g.Emit(OpCodes.Ret);
        }

        // 4) SALIR DE UN "INTENTA ESTO" CON EL SALTO LARGO, que es lo que sale cuando lo de dentro es
        //    grande. Ese salto lleva cuatro bytes detras y a los lectores de antes les faltaba tambien.
        {
            MethodBuilder m = tb.DefineMethod("IntentaEstoGrande", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            ILGenerator g = m.GetILGenerator();
            g.BeginExceptionBlock();
            g.Emit(OpCodes.Call, marca);
            g.BeginCatchBlock(typeof(Exception));
            g.Emit(OpCodes.Pop);
            for (int k = 0; k < 200; k++) g.Emit(OpCodes.Nop);   // el "si algo falla" largo: obliga al salto largo
            g.EndExceptionBlock();
            g.Emit(OpCodes.Call, otra);
            g.Emit(OpCodes.Ret);
        }

        // 5) UNA INSTRUCCION DE LAS DE DOS BYTES CON NUMERO DETRAS (el tamano de algo).
        {
            MethodBuilder m = tb.DefineMethod("DeDosBytes", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            ILGenerator g = m.GetILGenerator();
            g.Emit(OpCodes.Sizeof, typeof(long));
            g.Emit(OpCodes.Pop);
            g.Emit(OpCodes.Call, marca);
            g.Emit(OpCodes.Ret);
        }

        tb.CreateType();
        ab.Save("ConTrampa.dll");
        return Assembly.LoadFrom(Path.Combine(dir, "ConTrampa.dll")).GetType("Casos");
    }

    public static void Marca() { }
    public static void Otra() { }

    static string Junta(List<string> l) { return (l.Count == 0) ? "(nada)" : string.Join(", ", l.ToArray()); }
    static bool Igual(List<string> l, string[] espera)
    {
        if (l.Count != espera.Length) return false;
        for (int i = 0; i < l.Count; i++)
        {
            string s = l[i]; int p = s.LastIndexOf('.');
            if (p >= 0 && p + 1 < s.Length) s = s.Substring(p + 1);
            if (s != espera[i]) return false;
        }
        return true;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";

        Console.WriteLine("\n  1) CON TROZOS DE PROGRAMA HECHOS A PROPOSITO, SE SABE LA RESPUESTA BUENA\n");
        Type casos;
        try { casos = Fabrica(); }
        catch (Exception ex) { Console.WriteLine("  FALLO  no se pudieron fabricar los casos: " + ex.Message); return 1; }
        Check(casos != null, "se fabrican trozos de programa con las trampas de siempre");
        if (casos == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        string[] nombre = { "NumeroQueParece", "DireccionDeParametro", "ConTablaDeSalto", "IntentaEstoGrande", "DeDosBytes" };
        string[] cuento = { "un numero grande cuyos bytes parecen una llamada",
                            "coger la direccion de un parametro con numero alto",
                            "una tabla de salto (ocupa tanto como casos tenga)",
                            "salir de un \"intenta esto\" grande (salto largo)",
                            "una instruccion de dos bytes con numero detras" };
        string[][] espera = {
            new string[] { "Marca" },
            new string[] { "Marca" },
            new string[] { "Marca", "Otra", "Marca" },
            new string[] { "Marca", "Otra" },
            new string[] { "Marca" },
        };
        int fallaBuscando = 0, fallaAndando = 0;
        for (int i = 0; i < nombre.Length; i++)
        {
            MethodInfo m = casos.GetMethod(nombre[i]);
            List<string> nuevo = Codigo.Llamadas(m);
            List<string> vb = ViejoBuscando(m), va = ViejoAndando(m);
            bool okN = Igual(nuevo, espera[i]);
            if (!Igual(vb, espera[i])) fallaBuscando++;
            if (!Igual(va, espera[i])) fallaAndando++;
            Console.WriteLine("     " + cuento[i]);
            Console.WriteLine("        deberia dar : " + string.Join(", ", espera[i]));
            Console.WriteLine("        el lector nuevo: " + Junta(nuevo));
            Check(okN, "lo lee bien: " + cuento[i]);
        }

        Console.WriteLine("\n  2) Y LOS LECTORES DE ANTES SE EQUIVOCABAN EN ESOS MISMOS CASOS\n");
        Console.WriteLine("     el que no recorria se equivoca en " + fallaBuscando + " de " + nombre.Length);
        Console.WriteLine("     el que recorria a medias se equivoca en " + fallaAndando + " de " + nombre.Length);
        Check(fallaBuscando > 0 && fallaAndando > 0,
              "los dos lectores de antes fallan aqui (si no, esta prueba no probaria nada)");

        Console.WriteLine("\n  3) Y EN LA ROCOLA DE VERDAD, ¿EN CUANTOS SITIOS CAMBIA?\n");
        // Un numero, no una teoria: cuantas funciones de la rocola se leen distinto con uno y con otro.
        Type TF = null;
        try { TF = Assembly.LoadFrom(exe).GetType("Rocola.Form1", true); } catch { }
        Check(TF != null, "se puede abrir la rocola para repasarla entera");
        if (TF != null)
        {
            List<MethodBase> todos = Codigo.TodosLosMetodos(TF);
            int difB = 0, difA = 0, inventadas = 0, perdidas = 0;
            foreach (MethodBase m in todos)
            {
                List<string> n = Codigo.Nombres(m);
                List<string> b = ViejoBuscando(m), a = ViejoAndando(m);
                if (!MismaLista(n, b))
                {
                    difB++;
                    foreach (string s in b) if (!n.Contains(s)) inventadas++;
                }
                if (!MismaLista(n, a)) { difA++; foreach (string s in n) if (!a.Contains(s)) perdidas++; }
            }
            Console.WriteLine("     " + todos.Count + " funciones repasadas");
            Console.WriteLine("     el que no recorria se lee distinto en " + difB + " y se inventa " + inventadas + " llamadas que no estan");
            Console.WriteLine("     el que recorria a medias se lee distinto en " + difA + " y pierde " + perdidas + " llamadas que si estan");
            Check(inventadas > 0 || perdidas > 0,
                  "en la rocola de verdad los lectores de antes se equivocan (no era un peligro teorico)");
        }

        Console.WriteLine("\n  4) Y NINGUNA BATERIA SE QUEDA CON SU PROPIA COPIA\n");
        // Lo que hizo falta la vez pasada fue arreglar el mismo fallo en quince sitios. Que no vuelva a
        // haber quince sitios.
        List<string> conCopia = new List<string>();
        foreach (string f in Directory.GetFiles(".", "*.cs"))
        {
            string n = Path.GetFileName(f);
            if (n == "leercodigo.cs" || n == "leer.cs") continue;
            string txt = ""; try { txt = File.ReadAllText(f); } catch { }
            if (txt.IndexOf("GetILAsByteArray", StringComparison.Ordinal) >= 0) conCopia.Add(n);
        }
        Console.WriteLine("     baterias con su propia copia del recorrido: " + conCopia.Count
                          + (conCopia.Count > 0 ? ("   [" + string.Join(", ", conCopia.ToArray()) + "]") : ""));
        Check(conCopia.Count == 0, "todas leen el programa por el mismo sitio, que es el unico que hay que cuidar");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }

    static bool MismaLista(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
