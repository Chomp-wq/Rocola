using System;
using System.Collections.Generic;
using System.Reflection;

// EL LECTOR DEL PROGRAMA YA COMPILADO, PARA TODAS LAS PRUEBAS.
//
// Muchas baterias no aprietan botones: abren el ejecutable de la rocola y LEEN por dentro lo que hace cada
// funcion, para comprobar que sigue enchufada donde debe ("¿la rocola avanza la deriva desde su latido?").
//
// Eso obliga a recorrer las instrucciones una por una, y ahi esta la trampa: cada instruccion ocupa un
// largo distinto. Unas van solas, otras llevan un numero de un byte, otras de cuatro, otras de ocho, la
// tabla de salto lleva tantos como casos tenga, y hay una familia entera que empieza por un byte de aviso
// (0xFE) y ocupa dos. Si el recorrido se equivoca en UNA sola, a partir de ahi va leyendo desde el sitio
// equivocado: lo que venga despues es basura, y una prueba que lee basura contesta que todo esta bien.
//
// Habia quince baterias leyendo el codigo y cada una con su propia copia del recorrido; solo cuatro estaban
// completas, y hasta esas se saltaban dos instrucciones (coger la direccion de un parametro, y el salto
// largo con el que se sale de un "intenta esto"). Por eso ahora hay UN solo lector, aqui, y todas lo usan:
// un unico sitio que arreglar la proxima vez.
//
// No se compila como una bateria: se compila JUNTO a cada una (mira pruebas/todo.sh).
static class Codigo
{
    // Cuanto ocupa lo que va DETRAS de una instruccion normal.
    private static int Largo1(int op)
    {
        if (op >= 0x2B && op <= 0x37) return 1;          // saltos cortos
        if (op >= 0x38 && op <= 0x44) return 4;          // saltos largos
        switch (op)
        {
            // Un byte: coger/guardar un parametro o una variable por su numero, un numero chico, y el
            // salto corto de salida de un "intenta esto". El 0x0F -coger la DIRECCION de un parametro- es
            // el que faltaba en todas las copias de antes; sale cada vez que se llama a un metodo de un
            // parametro que es una fecha, un tiempo o cualquier otra cosa de ese estilo.
            case 0x0E: case 0x0F: case 0x10: case 0x11: case 0x12: case 0x13:
            case 0x1F: case 0xDE: return 1;
            // Cuatro bytes: los numeros grandes, los textos, los campos, y todo lo que nombra algo del
            // programa. El 0xDD -el salto largo de salida de un "intenta esto"- es el otro que faltaba, y
            // en un programa lleno de "intenta esto" como este sale a cada paso.
            case 0x20: case 0x22: case 0x27: case 0x28: case 0x29: case 0x6F: case 0x70: case 0x71:
            case 0x72: case 0x73: case 0x74: case 0x75: case 0x79:
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80:
            case 0x81: case 0x8C: case 0x8D: case 0x8F: case 0xA3: case 0xA4:
            case 0xA5: case 0xC2: case 0xC6: case 0xD0: case 0xDD: return 4;
            case 0x21: case 0x23: return 8;              // los numeros de ocho bytes
            default: return 0;
        }
    }

    // Y lo mismo para la familia que empieza por el byte de aviso 0xFE.
    private static int Largo2(int op2)
    {
        switch (op2)
        {
            case 0x06: case 0x07: case 0x15: case 0x16: case 0x1C: return 4;
            case 0x09: case 0x0A: case 0x0B: case 0x0C: case 0x0D: case 0x0E: return 2;
            case 0x12: case 0x19: return 1;
            default: return 0;
        }
    }

    // El recorrido. Va apuntando lo que pide: las llamadas, los textos, o las dos cosas.
    private static void Recorre(MethodBase m, List<string> llamadas, List<string> textos)
    {
        if (m == null) return;
        MethodBody b = null; try { b = m.GetMethodBody(); } catch { }
        if (b == null) return;
        byte[] il = null; try { il = b.GetILAsByteArray(); } catch { }
        if (il == null) return;
        Module mod = m.Module;
        int i = 0;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0xFE)
            {
                if (i + 1 >= il.Length) break;
                i += 2 + Largo2(il[i + 1]);
                continue;
            }
            if (op == 0x45)                              // la tabla de salto: lleva tantos destinos como casos
            {
                if (i + 4 >= il.Length) break;
                int n = BitConverter.ToInt32(il, i + 1);
                if (n < 0 || n > (il.Length - i) / 4) break;
                i += 1 + 4 + 4 * n;
                continue;
            }
            int largo = Largo1(op);
            if (largo == 4 && i + 4 < il.Length)
            {
                int tok = BitConverter.ToInt32(il, i + 1);
                if (op == 0x28 || op == 0x6F || op == 0x73)          // llamar / llamar por tabla / fabricar
                {
                    if (llamadas != null)
                        try
                        {
                            MethodBase mb = mod.ResolveMethod(tok);
                            if (mb != null)
                                llamadas.Add((mb.DeclaringType != null ? mb.DeclaringType.Name : "?") + "." + mb.Name);
                        }
                        catch { }
                }
                else if (op == 0x72)                                  // nombrar un texto
                {
                    if (textos != null)
                        try { string s = mod.ResolveString(tok); if (s != null) textos.Add(s); } catch { }
                }
            }
            i += 1 + largo;
        }
    }

    // UNA INSTRUCCION YA LEIDA. Para las baterias que necesitan mirar el orden de las cosas y no solo
    // "¿llama a esto?": por ejemplo "¿justo antes de copiar en bloque estaba cogiendo el buffer que le dan?".
    public struct Paso
    {
        public int Donde;      // en que byte del metodo empieza
        public int Op;         // la instruccion (o 0xFE si es de las de dos bytes)
        public int Op2;        // la segunda mitad de las de dos bytes, o -1
        public int Numero;     // los cuatro bytes de detras, si los lleva (si no, 0)
    }

    // El metodo entero ya leido, instruccion por instruccion y en orden.
    public static List<Paso> Pasos(MethodBase m)
    {
        List<Paso> r = new List<Paso>();
        if (m == null) return r;
        MethodBody b = null; try { b = m.GetMethodBody(); } catch { }
        if (b == null) return r;
        byte[] il = null; try { il = b.GetILAsByteArray(); } catch { }
        if (il == null) return r;
        int i = 0;
        while (i < il.Length)
        {
            Paso p = new Paso();
            p.Donde = i; p.Op = il[i]; p.Op2 = -1; p.Numero = 0;
            if (il[i] == 0xFE)
            {
                if (i + 1 >= il.Length) break;
                p.Op2 = il[i + 1];
                int l2 = Largo2(il[i + 1]);
                if (l2 == 4 && i + 5 < il.Length + 1 && i + 2 + 4 <= il.Length) p.Numero = BitConverter.ToInt32(il, i + 2);
                r.Add(p);
                i += 2 + l2;
                continue;
            }
            if (il[i] == 0x45)
            {
                if (i + 4 >= il.Length) break;
                int n = BitConverter.ToInt32(il, i + 1);
                if (n < 0 || n > (il.Length - i) / 4) break;
                p.Numero = n; r.Add(p);
                i += 1 + 4 + 4 * n;
                continue;
            }
            int largo = Largo1(il[i]);
            if (largo == 4 && i + 4 < il.Length) p.Numero = BitConverter.ToInt32(il, i + 1);
            r.Add(p);
            i += 1 + largo;
        }
        return r;
    }

    // Lo que un metodo LLAMA o LEE, en orden: el nombre pelado de cada llamada, y el nombre del campo con un
    // "$" delante cuando lo que hace es leerlo. Sirve para preguntarle al programa "¿que mira justo antes de
    // hacer esto?", que es como se comprueba una condicion sin apretar un solo boton.
    public static List<string> Repasa(MethodBase m)
    {
        List<string> r = new List<string>();
        if (m == null) return r;
        Module mod = m.Module;
        foreach (Paso p in Pasos(m))
        {
            if (p.Op2 >= 0) continue;
            if (p.Op == 0x28 || p.Op == 0x6F || p.Op == 0x73)
            { try { MethodBase mb = mod.ResolveMethod(p.Numero); if (mb != null) r.Add(mb.Name); } catch { } }
            else if (p.Op == 0x7B || p.Op == 0x7E)
            { try { FieldInfo f = mod.ResolveField(p.Numero); if (f != null) r.Add("$" + f.Name); } catch { } }
        }
        return r;
    }

    // Cuantas veces un metodo LEE y cuantas ESCRIBE un campo (por su nombre).
    public static void UsosDeCampo(MethodBase m, string campo, out int lee, out int escribe)
    {
        lee = 0; escribe = 0;
        if (m == null) return;
        Module mod = m.Module;
        foreach (Paso p in Pasos(m))
        {
            if (p.Op2 >= 0) continue;
            bool esLee = (p.Op == 0x7B || p.Op == 0x7E || p.Op == 0x7C || p.Op == 0x7F);   // leer / coger su direccion
            bool esEsc = (p.Op == 0x7D || p.Op == 0x80);                                    // escribir
            if (!esLee && !esEsc) continue;
            try { FieldInfo f = mod.ResolveField(p.Numero); if (f == null || f.Name != campo) continue; } catch { continue; }
            if (esLee) lee++; else escribe++;
        }
    }

    // TODO LO QUE LLAMA un metodo, en orden, con el nombre del tipo delante ("Mp3FileReader..ctor").
    public static List<string> Llamadas(MethodBase m)
    {
        List<string> r = new List<string>();
        Recorre(m, r, null);
        return r;
    }

    // Lo mismo pero solo el nombre, sin el tipo ("AvanzaDeriva").
    public static List<string> Nombres(MethodBase m)
    {
        List<string> r = new List<string>();
        foreach (string s in Llamadas(m))
        {
            int p = s.LastIndexOf('.');
            r.Add((p >= 0 && p + 1 < s.Length) ? s.Substring(p + 1) : s);
        }
        return r;
    }

    // Los TEXTOS que nombra ("rocola_ia.bin").
    public static List<string> Textos(MethodBase m)
    {
        List<string> r = new List<string>();
        Recorre(m, null, r);
        return r;
    }

    // ¿Llama a esto? Vale tanto "AvanzaDeriva" como "Form1.AvanzaDeriva".
    public static bool Llama(List<string> lista, string que)
    {
        bool conTipo = que.IndexOf('.') > 0;
        foreach (string s in lista)
        {
            if (s == que) return true;
            if (!conTipo)
            {
                int p = s.LastIndexOf('.');
                if (p >= 0 && p + 1 < s.Length && s.Substring(p + 1) == que) return true;
            }
        }
        return false;
    }
    public static bool Llama(MethodBase m, string que) { return Llama(Llamadas(m), que); }

    // Lo que llama un metodo MAS lo que llaman las funciones sueltas que el compilador le saca aparte con el
    // nombre entre picos (<AbreMp3>m__0). Sin esto, un delegado esconde lo que hace dentro.
    public static List<string> ConAyudantes(Type raiz, string metodo)
    {
        List<string> r = new List<string>();
        if (raiz == null) return r;
        BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        List<Type> tipos = ConLosDeDentro(raiz);
        string picos = "<" + metodo + ">";
        foreach (Type t in tipos)
            foreach (MethodInfo m in t.GetMethods(bf))
                if (m.Name == metodo || m.Name.IndexOf(picos, StringComparison.Ordinal) >= 0
                    || t.Name.IndexOf(picos, StringComparison.Ordinal) >= 0)
                    r.AddRange(Llamadas(m));
        return r;
    }

    // Los METODOS de un metodo mas sus ayudantes -lo mismo que ConAyudantes, pero devolviendo las funciones
    // en vez de lo que llaman-. Hace falta para mirar QUE CAMPOS toca un delegado, no solo a quien llama.
    public static List<MethodBase> MetodosConAyudantes(Type raiz, string metodo)
    {
        List<MethodBase> r = new List<MethodBase>();
        if (raiz == null) return r;
        BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        string picos = "<" + metodo + ">";
        foreach (Type t in ConLosDeDentro(raiz))
            foreach (MethodInfo m in t.GetMethods(bf))
                if (m.Name == metodo || m.Name.IndexOf(picos, StringComparison.Ordinal) >= 0
                    || t.Name.IndexOf(picos, StringComparison.Ordinal) >= 0)
                    r.Add(m);
        return r;
    }

    // ¿Alguno de esos lee ese campo? Tres baterias tenian su propia copia de esta vuelta -y solo una miraba
    // dentro de los delegados-, asi que vive aqui: un solo sitio que arreglar.
    public static bool LeeCampo(List<MethodBase> ms, string campo)
    {
        foreach (MethodBase m in ms)
        {
            int lee, esc;
            UsosDeCampo(m, campo, out lee, out esc);
            if (lee > 0) return true;
        }
        return false;
    }
    public static bool EscribeCampo(List<MethodBase> ms, string campo)
    {
        foreach (MethodBase m in ms)
        {
            int lee, esc;
            UsosDeCampo(m, campo, out lee, out esc);
            if (esc > 0) return true;
        }
        return false;
    }

    // Todos los metodos del programa, para pasarles algo por encima a todos.
    public static List<MethodBase> TodosLosMetodos(Type raiz)
    {
        List<MethodBase> r = new List<MethodBase>();
        if (raiz == null) return r;
        BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        List<Type> tipos = ConLosDeDentro(raiz);
        foreach (Type t in tipos)
        {
            foreach (MethodInfo m in t.GetMethods(bf)) r.Add(m);
            foreach (ConstructorInfo c in t.GetConstructors(bf)) r.Add(c);
        }
        return r;
    }

    // UN Form1 SIN ENCENDER LA ROCOLA, CON SUS CERROJOS PUESTOS.
    //
    // Las baterias arman un Form1 sin pasar por el constructor: encenderlo de verdad levantaria la ventana
    // entera, y lo que se quiere probar son funciones sueltas. El precio es que los campos que se rellenan
    // AL DECLARARLOS -"private readonly object candado = new object()"- se quedan en nada, porque eso lo
    // hace el constructor.
    //
    // Mientras esos campos solo se leian, no pasaba nada. Desde que la rocola guarda en segundo plano hay
    // cerrojos por medio, y un "lock" sobre nada no falla suave: revienta la bateria entera con un error
    // que no tiene que ver con lo que se estaba probando. Aqui se les pone lo que el constructor les
    // habria puesto.
    //
    // Los avisos de "ya no hay nada pendiente" se dejan ENCENDIDOS, que es como nacen: en una bateria no
    // hay nada guardandose, asi que quien pregunte "¿ya acabo todo?" tiene que oir que si.
    public static object FormSinEncender(Type tipo)
    {
        object f = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tipo);
        PonCerrojos(f);
        return f;
    }

    // ESPERAR A QUE EL GUARDADO EN SEGUNDO PLANO TERMINE DE VERDAD. Desde que la rocola escribe sin
    // parar la musica, pedir "guarda" ya no deja el archivo escrito al volver: lo deja ENCARGADO. Una
    // prueba que guardaba y acto seguido leia, leia lo de antes. Se hacia esperando a ciegas medio
    // segundo, que es peor de lo que parece: en una maquina cargada no alcanza y la bateria falla sin
    // que nada este roto, y en una despejada sobra y la tanda entera tarda de mas por nada.
    //
    // Cada rama le puso NOMBRE DISTINTO a la barrera -EsperaGuardados() en la de Windows potente,
    // WaitDataWrites(ms) en la de XP- asi que se prueban las dos. Si no hay ninguna, esa rocola guarda
    // a la cara y no hay nada que esperar. Preguntar por un solo nombre dejaba a la otra rama sin
    // barrera y aguantando solo por la espera a ciegas, que es justo lo que no se quiere.
    public static void EsperaGuardado(Type tf, object f)
    {
        if (tf == null || f == null) return;
        MethodInfo m = tf.GetMethod("EsperaGuardados", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m != null) { try { m.Invoke(f, null); } catch { } return; }
        m = tf.GetMethod("WaitDataWrites", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m != null) { try { m.Invoke(f, new object[] { 15000 }); } catch { } }
    }

    public static void PonCerrojos(object obj)
    {
        if (obj == null) return;
        for (Type t = obj.GetType(); t != null && t.Name != "Object"; t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public
                                              | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsStatic) continue;
                object v = null;
                try { v = f.GetValue(obj); } catch { continue; }
                if (v != null) continue;
                try
                {
                    if (f.FieldType == typeof(object)) f.SetValue(obj, new object());
                    else if (f.FieldType == typeof(System.Threading.ManualResetEvent))
                        f.SetValue(obj, new System.Threading.ManualResetEvent(true));
                }
                catch { }
            }
    }

    // Un tipo y TODOS los que lleva dentro, por hondo que esten.
    //
    // Antes se miraba un solo nivel, y eso dejaba fuera lo que mas importa mirar: cuando un delegado lleva
    // otro delegado dentro -por ejemplo un hilo que al terminar le encarga algo al hilo de la pantalla-, el
    // compilador mete la clase del de dentro DENTRO de la del de fuera. Con un solo nivel, todo lo que hace
    // ese trozo era invisible desde las pruebas: no aparecia ni una llamada, y una bateria que preguntara
    // por el se lo tragaba diciendo que no habia nada.
    public static List<Type> ConLosDeDentro(Type raiz)
    {
        List<Type> r = new List<Type>();
        if (raiz == null) return r;
        Mete(raiz, r);
        return r;
    }
    private static void Mete(Type t, List<Type> r)
    {
        if (t == null || r.Contains(t)) return;
        r.Add(t);
        foreach (Type h in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) Mete(h, r);
    }
}
