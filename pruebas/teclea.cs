using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// TECLEAR EN EL BUSCADOR NO PUEDE REHACER LA PANTALLA ENTERA.
//
// El medidor de Ctrl+F3 dice que la escena que manda -la que peor va- es "Escribiendo en el buscador".
// Medido por 'buscadib' antes de tocar nada:
//
//     1024x748  (2.9 MB):  rehacer 1.23 ms + subir 0.48 ms = 1.72 ms por pulsacion
//     1920x1000 (7.3 MB):  rehacer 1.60 ms + subir 1.19 ms = 2.80 ms por pulsacion
//     los resultados REHACIENDOLOS (una tecla nueva):        3.07 ms
//
// Cada letra rehacia y subia la textura de pantalla completa, porque la consulta entraba en la firma de
// esa textura. Y quien teclea suelta cinco o diez letras por segundo.
//
// LA TAPADERA ES LA MISMA QUE LA DEL HOJEO: mientras las letras estan entrando, nadie esta leyendo los
// resultados. Lo que SI tiene que verse al instante es lo que se escribe.
//
// Asi que se parte en dos:
//   - LO TECLEADO va en su propia imagen chica, como ya iba la tecla elegida. Se rehace en cada letra
//     -son unos cientos de KB en vez de siete megas- y se ve al momento.
//   - LOS RESULTADOS SE POSAN: la consulta que los manda se actualiza cuando han pasado unos 150 ms sin
//     teclas. Las consultas intermedias, las que nadie llega a leer, ni se buscan ni se hornean.
//
// Y la primera letra despues de un rato quieto NO espera: abrir el buscador y escribir se tiene que
// sentir igual de vivo que antes.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE LO TECLEADO Y LO QUE MANDA LOS RESULTADOS SEAN DOS COSAS.
//   2) QUE TECLEAR NO MUEVA LOS RESULTADOS, y que posar SI.
//   3) QUE LA PRIMERA LETRA NO ESPERE, y que la espera sea corta de verdad.
//   4) QUE LO TECLEADO VAYA EN SU PROPIA IMAGEN, no dentro de la de pantalla completa.
//   5) QUE LA FIRMA DE LA IMAGEN GRANDE MIRE LA CONSULTA POSADA, que es lo que hace que las letras
//      intermedias no la rehagan.
class Teclea
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TB;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static object Campo(object o, string n)
    {
        FieldInfo f = TB.GetField(n, TODO);
        return (f == null) ? null : f.GetValue(o);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "SearchOverlay") TB = t;
        }
        if (TF == null || TB == null) { Console.WriteLine("  FALLO  no se encontro el buscador"); return 1; }

        Console.WriteLine("\n  1) LO TECLEADO Y LO QUE MANDA LOS RESULTADOS SON DOS COSAS\n");
        FieldInfo fEsc = TB.GetField("Escrito", TODO), fQ = TB.GetField("Query", TODO);
        MethodBase posa = Met(TB, "PosaConsulta"), posarYa = Met(TF, "PosarYa");
        Check(fEsc != null, "el buscador se acuerda de lo que la persona lleva tecleado (Escrito)");
        Check(fQ != null, "y aparte, de la consulta que estan enseñando los resultados (Query)");
        Check(posa != null, "y hay un solo sitio que pasa la una a la otra (PosaConsulta)");
        Check(posarYa != null && posarYa.IsStatic, "y una cuenta pura que dice si hay que posar ya (PosarYa)");
        if (fEsc == null || fQ == null || posa == null || posarYa == null)
        { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }

        Console.WriteLine("\n  2) TECLEAR NO MUEVE LOS RESULTADOS; POSAR, SI\n");
        Control b = (Control)Activator.CreateInstance(TB, true);
        b.SetBounds(0, 0, 1024, 700);
        MethodInfo mSet = (MethodInfo)Met(TB, "SetQuery");
        MethodInfo mChar = (MethodInfo)Met(TB, "TypeChar");
        MethodInfo mBack = (MethodInfo)Met(TB, "Backspace");
        mSet.Invoke(b, new object[] { "" });
        // Cuantas veces se dispara la busqueda de verdad: es lo caro (rehornear 40 tarjetas: 3.07 ms).
        int busquedas = 0;
        FieldInfo fOn = TB.GetField("OnQueryChanged", TODO);
        fOn.SetValue(b, new Action<string>(delegate(string q) { busquedas++; }));
        foreach (char c in "rober") mChar.Invoke(b, new object[] { c });
        Console.WriteLine("     tecleado \"rober\" de un tiron:");
        Console.WriteLine("       lo escrito  = \"" + Campo(b, "Escrito") + "\"");
        Console.WriteLine("       los resultados siguen en = \"" + Campo(b, "Query") + "\"");
        Console.WriteLine("       busquedas disparadas = " + busquedas);
        Check((string)Campo(b, "Escrito") == "rober", "lo tecleado se apunta letra a letra, al instante");
        Check((string)Campo(b, "Query") == "", "y los resultados NO se mueven mientras las letras entran");
        Check(busquedas == 0, "ni se dispara la busqueda por cada letra intermedia");
        posa.Invoke(b, null);
        Console.WriteLine("     y al posar: los resultados pasan a \"" + Campo(b, "Query") + "\", busquedas = " + busquedas);
        Check((string)Campo(b, "Query") == "rober", "al posar, los resultados van a lo que hay tecleado");
        Check(busquedas == 1, "y la busqueda se dispara UNA vez, no cinco");
        posa.Invoke(b, null);
        Check(busquedas == 1, "posar dos veces lo mismo no vuelve a buscar");
        mBack.Invoke(b, null);
        Check((string)Campo(b, "Escrito") == "robe" && (string)Campo(b, "Query") == "rober",
              "borrar tambien es teclear: se ve al momento y los resultados esperan");

        Console.WriteLine("\n  3) LA PRIMERA LETRA NO ESPERA\n");
        MethodInfo mYa = (MethodInfo)posarYa;
        bool alInstante = (bool)mYa.Invoke(null, new object[] { 9999 });
        bool enRafaga = (bool)mYa.Invoke(null, new object[] { 0 });
        FieldInfo fMs = TF.GetField("PosarMs", TODO);
        int ms = (fMs != null) ? Convert.ToInt32(fMs.GetValue(null)) : -1;
        Console.WriteLine("     la espera es de " + ms + " ms");
        Check(alInstante, "tras un rato sin teclear, la primera letra se posa en el acto");
        Check(!enRafaga, "y en mitad de una rafaga, espera");
        Check(ms >= 90 && ms <= 300, "y esa espera es corta: no se puede notar escribiendo (" + ms + " ms)");

        Console.WriteLine("\n  3b) Y ESTO ES LO QUE SE AHORRA DE VERDAD\n");
        // OJO CON COMO SE MIDE ESTO. Lo que cuesta UNA reconstruccion no ha cambiado -ni podia: la pantalla
        // se ve exactamente igual que antes, y eso lo comprueba 'busmov' pixel por pixel-. Lo que cambia es
        // CUANTAS hay. Asi que lo que se cuenta aqui son reconstrucciones, no milisegundos.
        //
        // Se le da a la cuenta una racha de tecleo de verdad y se aplica la misma regla que el formulario:
        // si desde la ultima tecla pasaron ya los 150 ms, se posa en el acto; si no, se espera, y el reloj
        // se reinicia con cada tecla nueva. Al final, una mas cuando la persona para.
        int[] ritmos = { 125, 200, 400 };        // ms entre letra y letra: rapido, normal, despacio
        string[] comoEs = { "rapido (8 letras/s)", "normal (5 letras/s)", "despacio (2.5 letras/s)" };
        for (int r = 0; r < ritmos.Length; r++)
        {
            int letras = 14, posadas = 0, desde = 99999;   // se empieza tras un rato quieto
            bool ultimaEspero = false;
            for (int i = 0; i < letras; i++)
            {
                bool ya = (bool)mYa.Invoke(null, new object[] { desde });
                if (ya) posadas++;
                ultimaEspero = !ya;
                desde = ritmos[r];
            }
            // Y una mas cuando la persona para, PERO solo si la ultima letra se quedo esperando: si se poso
            // en el acto, el reloj ni se arranco y no hay nada pendiente.
            if (ultimaEspero) posadas++;
            Console.WriteLine("     " + comoEs[r].PadRight(24) + letras + " letras  ->  " + posadas
                              + " reconstrucciones de pantalla (antes eran " + letras + ")");
            if (ritmos[r] < ms) Check(posadas * 3 <= letras, "tecleando " + comoEs[r] + ", al menos tres veces menos trabajo");
            else Check(posadas <= letras, "tecleando " + comoEs[r] + " no se hace MAS trabajo que antes");
        }
        Console.WriteLine("     (y escribiendo despacio se posa en cada letra, como siempre: no se nota nada)");

        Console.WriteLine("\n  4) LO TECLEADO VA EN SU PROPIA IMAGEN\n");
        // Las imagenes chicas SOLO existen en la version con tarjeta grafica: sin ella no hay texturas que
        // subir y la pantalla se repinta por trozos, que es otro mecanismo. Por eso esta bateria sale en las
        // dos listas: aqui se comprueba la parte que solo vive alli.
        FieldInfo fN = TF.GetField("NumPiezas", TODO);
        MethodInfo mViva = (MethodInfo)Met(TB, "PiezaViva");
        FieldInfo fPE = TB.GetField("PiezaEscrito", TODO);
        if (fN == null)
            Console.WriteLine("     (esta version no dibuja por tarjeta grafica: no hay imagenes chicas que mirar)");
        else
        {
        int n = Convert.ToInt32(fN.GetValue(null));
        Console.WriteLine("     imagenes chicas aparte: " + n);
        Check(n >= 2, "hay una segunda imagen chica, ademas de la de la tecla elegida");
        Check(fPE != null, "y tiene nombre propio (PiezaEscrito)");
        if (mViva != null && fPE != null)
        {
            int cual = Convert.ToInt32(fPE.GetValue(null));
            object[] arg = new object[] { cual, Rectangle.Empty };
            bool viva = (bool)mViva.Invoke(b, arg);
            Rectangle donde = (Rectangle)arg[1];
            Console.WriteLine("     ocupa " + donde.Width + "x" + donde.Height + " px de una pantalla de 1024x700");
            Check(viva && donde.Width > 100 && donde.Height > 20, "y tiene su sitio: la caja donde se escribe");
            long px = (long)donde.Width * donde.Height, todo = 1024L * 700L;
            Console.WriteLine("     o sea el " + (px * 100 / todo) + "% de lo que se rehacia antes en cada letra");
            Check(px * 4 < todo, "que es una parte chica de la pantalla, que es de lo que se trataba");
        }
        // La caja la dibuja UNA sola funcion, y de ella tiran los dos caminos: el normal y la imagen chica.
        MethodBase caja = Met(TB, "DibujaCajaEscrito");
        Check(caja != null, "la caja de lo escrito se dibuja en un solo sitio (DibujaCajaEscrito)");
        if (caja != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(caja, "Escrito", out lee, out esc);
            Check(lee > 0, "y es la que pinta lo tecleado");
        }
        MethodBase dib = Met(TB, "DibujaPiezaViva");
        Check(dib != null && Codigo.Llama(dib, "DibujaCajaEscrito"), "la imagen chica la usa");
        }

        Console.WriteLine("\n  5) Y LA IMAGEN GRANDE MIRA LA CONSULTA POSADA\n");
        // Si mirara lo tecleado, cada letra volveria a rehacer los siete megas y no habriamos hecho nada.
        MethodBase sig = Met(TF, "UiSig");
        if (sig == null)
            Console.WriteLine("     (sin tarjeta grafica no hay textura de pantalla completa que firmar)");
        else
        {
            int lee, esc;
            Codigo.UsosDeCampo(sig, "Query", out lee, out esc);
            Check(lee > 0, "la firma de la pantalla completa mira la consulta ya posada");
            Codigo.UsosDeCampo(sig, "Escrito", out lee, out esc);
            Check(lee == 0, "y NO lo tecleado: por eso las letras intermedias no la rehacen");
        }
        // Y alguien tiene que posarla, o los resultados no llegarian nunca.
        bool laPosa = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "PosaConsulta")) laPosa = true;
        Check(laPosa, "y la rocola la posa cuando toca (si no, los resultados no llegarian nunca)");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
