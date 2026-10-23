using System;
using System.Collections.Generic;
using System.Reflection;

// LAS MINIATURAS, CUANDO NO HAY NADIE.
//
// La pared de discos se dibuja con la portada de cada uno, y decodificar el JPG grande es CARO. Por eso
// la primera vez de cada disco se guarda una miniatura ya escalada y a partir de ahi se lee esa.
// Medido por 'cachemem':
//
//     desde la miniatura ya hecha : 0.90 ms por caratula
//     desde la portada original   : 71.97 ms por caratula
//
// Ochenta veces. Con 3000 discos, hacerlas todas son tres minutos y medio de un nucleo, UNA vez en la
// vida de la rocola. Hasta que eso pasa, la pared enseña las portadas llegando tarde.
//
// Y hoy ese trabajo casi no avanza: el calentador espera OCHO SEGUNDOS sin que nadie toque un boton
// antes de cada miniatura. En un bar con gente esos ocho segundos no llegan nunca. Semanas para
// terminar algo de tres minutos y medio.
//
// Es la misma enfermedad que tenia el pre-analisis, y la cura ya esta construida: RocolaSola(), que
// dice si estamos en modo atraccion, sin musica y sin video. En ese rato no hay nadie a quien
// estorbar.
//
// LO QUE MAS IMPORTA, IGUAL QUE EN EL PRE-ANALISIS, NO ES QUE CORRA MAS: ES QUE FRENE. Y hay una
// segunda cosa que vigilar aqui, que no existia alla: si las miniaturas y el pre-analisis se sueltan
// A LA VEZ en la maquina de dos nucleos del bar, se pelean por el disco y salen las dos peor. Las
// miniaturas van primero, porque son lo que SE VE.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE SOLA NO SE FRENE, y que con alguien delante SI.
//   2) QUE LO QUE YA CEDIA, SIGA CEDIENDO: caratulas del primer plano y video.
//   3) QUE SE SEPA CUANTAS QUEDAN, para que el pre-analisis pueda apartarse.
//   4) QUE CON MINIATURAS PENDIENTES EL PRE-ANALISIS SE QUEDE EN UN CARRIL, aunque este sola.
class Duerme
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
    // Lo que hace el calentador vive en el delegado que se lanza como hilo; el compilador lo saca aparte.
    static List<MethodBase> Ayudantes(string metodo)
    {
        List<MethodBase> r = new List<MethodBase>();
        string picos = "<" + metodo + ">";
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            string n = m.Name, t = (m.DeclaringType != null) ? m.DeclaringType.Name : "";
            if (n == metodo) continue;
            if (n.IndexOf(picos, StringComparison.Ordinal) >= 0 || t.IndexOf(picos, StringComparison.Ordinal) >= 0) r.Add(m);
        }
        return r;
    }
    static bool AyudanteLlama(string metodo, string que)
    {
        foreach (MethodBase m in Ayudantes(metodo)) if (Codigo.Llama(m, que)) return true;
        return false;
    }
    static bool AyudanteUsa(string metodo, string campo)
    {
        foreach (MethodBase m in Ayudantes(metodo))
        {
            int lee, esc; Codigo.UsosDeCampo(m, campo, out lee, out esc);
            if (lee > 0 || esc > 0) return true;
        }
        return false;
    }
    static MethodInfo mCarriles;
    static int Carriles(bool turbo, bool sola, bool discoLento, int nucleos, bool minis)
    {
        return Convert.ToInt32(mCarriles.Invoke(null, new object[] { turbo, sola, discoLento, nucleos, minis }));
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) SOLA, LAS MINIATURAS NO SE FRENAN\n");
        MethodBase cal = Met("StartThumbWarm"), sola = Met("RocolaSola");
        Check(cal != null, "sigue habiendo un calentador de miniaturas (StartThumbWarm)");
        Check(sola != null, "y el sitio que dice si la rocola esta sola (RocolaSola)");
        if (cal == null || sola == null) { Console.WriteLine("\n  " + fallos + " FALLO(S)"); return Math.Max(fallos, 1); }
        Check(AyudanteLlama("StartThumbWarm", "RocolaSola"),
              "el calentador pregunta si esta sola antes de frenarse por nadie");
        // Y el freno SIGUE AHI para cuando no lo esta: es el mismo reloj de siempre.
        Check(AyudanteUsa("StartThumbWarm", "lastInputTick"),
              "y con alguien delante se sigue apartando a los primeros ocho segundos");

        Console.WriteLine("\n  2) Y LO QUE YA CEDIA, SIGUE CEDIENDO\n");
        // Esto es lo que NO se puede romper: el calentador existe para no estorbar. Si por correr mas
        // dejara de cederle el disco a lo que se esta viendo, haria mas daño del que arregla.
        Check(AyudanteUsa("StartThumbWarm", "coverLoading"), "si hay caratulas del primer plano en camino, se aparta");
        Check(AyudanteUsa("StartThumbWarm", "videoOn"), "y si hay un video sonando, tambien");
        // Desde el 10-10 el calentador ya no llama a WaitOne en el propio delegado: pide el turno por TomaTurnoMiniatura (que espera
        // con plazo de 100 ms para poder retirarse si el calentador se cancela). Sigue siendo el MISMO turno unico de disco que las
        // cargas normales (coverSem, el de Form1.Portadas.cs), asi que se vigila el camino entero: el calentador llama a
        // TomaTurnoMiniatura, esta espera sobre coverSem, y el calentador devuelve el turno con coverSem.Release.
        MethodBase toma = Met("TomaTurnoMiniatura");
        Check(toma != null, "el turno del calentador sale de un unico sitio (TomaTurnoMiniatura)");
        Check(AyudanteLlama("StartThumbWarm", "TomaTurnoMiniatura"), "el calentador pide el turno por ahi");
        Check(toma != null && Codigo.Llama(toma, "WaitOne"), "y ahi se espera el turno");
        {
            int lee = 0, esc = 0;
            if (toma != null) Codigo.UsosDeCampo(toma, "coverSem", out lee, out esc);
            Check(lee > 0, "sigue siendo el mismo turno unico de disco que las cargas normales (coverSem)");
        }
        Check(AyudanteLlama("StartThumbWarm", "Release"), "y el calentador devuelve el turno cuando acaba");

        Console.WriteLine("\n  3) SE SABE CUANTAS QUEDAN\n");
        // Sin esto, el pre-analisis no tiene forma de apartarse de las miniaturas.
        FieldInfo fPend = TF.GetField("minisPendientes", TODO);
        Check(fPend != null, "la rocola lleva la cuenta de las miniaturas que faltan (minisPendientes)");
        Check(AyudanteLlama("StartThumbWarm", "PublishThumbRemaining"), "el calentador publica el contador por generacion");
        MethodInfo publica = (MethodInfo)Met("PublishThumbRemaining"), avanza = (MethodInfo)Met("AdvanceThumbWarm");
        Check(publica != null && avanza != null, "existen publicacion y cancelacion del calentador");
        if (publica != null && avanza != null)
        {
            object f = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
            TF.GetField("coverLock", TODO).SetValue(f, new object());
            int viejo = (int)avanza.Invoke(f, null), actual = (int)avanza.Invoke(f, null);
            publica.Invoke(f, new object[] { actual, 9 });
            Check(!(bool)publica.Invoke(f, new object[] { viejo, 0 }), "un hilo retirado no borra el contador nuevo");
            publica.Invoke(f, new object[] { actual, -1 });
            Check((int)TF.GetField("minisPendientes", TODO).GetValue(f) == 8, "el hilo vigente descuenta una miniatura");
        }

        Console.WriteLine("\n  4) LAS MINIATURAS PRIMERO, EL PRE-ANALISIS DESPUES\n");
        MethodInfo c = (MethodInfo)Met("CarrilesPara");
        Check(c != null, "la cuenta de carriles sigue estando aparte (CarrilesPara)");
        if (c != null)
        {
            ParameterInfo[] ps = c.GetParameters();
            Check(ps.Length == 5, "y ahora tambien se le dice si quedan miniaturas por hacer");
            if (ps.Length == 5)
            {
                mCarriles = c;
                Console.WriteLine("     sola, 8 nucleos, SIN miniaturas pendientes ->  " + Carriles(false, true, false, 8, false) + " carriles");
                Console.WriteLine("     sola, 8 nucleos, CON miniaturas pendientes ->  " + Carriles(false, true, false, 8, true) + " carriles");
                Check(Carriles(false, true, false, 8, false) > 1, "sin miniaturas pendientes, el pre-analisis se suelta");
                Check(Carriles(false, true, false, 8, true) == 1,
                      "con miniaturas pendientes se queda en uno: primero lo que se VE");
                // Y el freno de siempre sigue mandando por encima de todo.
                bool siempreUno = true;
                for (int n = 1; n <= 64; n++)
                    for (int d = 0; d < 2; d++)
                        for (int mm = 0; mm < 2; mm++)
                            if (Carriles(false, false, d == 1, n, mm == 1) != 1) siempreUno = false;
                Check(siempreUno, "y con alguien usandola, un carril: en cualquier maquina y haya lo que haya pendiente");
            }
            Check(Codigo.Llama(Codigo.ConAyudantes(TF, "StartPreScan"), "CarrilesPara"),
                  "el pre-analisis sigue preguntandole a esa cuenta");
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}
