using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// La ficha de calidad de cada cancion (corte de arriba, recorte de origen, corte de abajo) y las cuatro
// cosas que ahora se hacen con ella: informe, des-recorte solo donde hace falta, realce y bajo por cancion.
class Calidad
{
    static Type TF; static object F;
    static Dictionary<string,float[]> cache;
    static int CUT, CLIP, CUTLO, LEN;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static object Call(string n, params object[] a)
    { return TF.GetMethod(n, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, a); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static void Pon(string nom, float corte, float recorte, float corteLo)
    {
        float[] c = new float[LEN];
        c[CUT] = corte; c[CLIP] = recorte; c[CUTLO] = corteLo; c[11] = 2f;
        cache[nom] = c;
    }
    static float Real(string m, string p) { return (float)TF.GetMethod(m, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, new object[]{ p }); }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        BindingFlags SF = BindingFlags.Static|BindingFlags.NonPublic;
        LEN = (int)TF.GetField("EqLen", SF).GetValue(null);
        CUT = (int)TF.GetField("EqCut", SF).GetValue(null);
        CLIP = (int)TF.GetField("EqClip", SF).GetValue(null);
        CUTLO = (int)TF.GetField("EqCutLo", SF).GetValue(null);
        Console.WriteLine(string.Format("\n  ficha de {0} numeros: corte arriba [{1}], recorte [{2}], corte abajo [{3}]", LEN, CUT, CLIP, CUTLO));
        Check(LEN >= 11 && CUT == 8 && CLIP == 9 && CUTLO == 10, "la ficha guarda los tres datos de calidad");

        cache = new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase);
        SetFld("eqCache", cache); SetFld("excAmount", 0.35f); SetFld("bassAmount", 0.30f);
        SetFld("eqAvg", null); SetFld("declipOn", true);

        // --- (A) resumen e informe
        for (int i = 0; i < 10; i++) Pon("buena" + i + ".mp3", 22050f, 0f, 30f);
        for (int i = 0; i < 6; i++)  Pon("media" + i + ".mp3", 18000f, 0f, 50f);
        for (int i = 0; i < 4; i++)  Pon("pobre" + i + ".mp3", 14000f, 0.004f, 85f);
        Pon("sinmedir.mp3", 0f, 0f, 0f);
        object[] args = { 0, 0, 0, 0, 0 };
        TF.GetMethod("CalidadResumen", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(F, args);
        Console.WriteLine(string.Format("  resumen: {0} completas, {1} aceptables, {2} pobres, {3} recortadas, {4} sin medir",
                                        args[0], args[1], args[2], args[3], args[4]));
        Check((int)args[0]==10 && (int)args[1]==6 && (int)args[2]==4 && (int)args[3]==4 && (int)args[4]==1,
              "reparte bien la biblioteca en completas / aceptables / pobres / recortadas / sin medir");

        // El informe ya NO se escribe en el hilo de la interfaz: ordenar decenas de miles de canciones y
        // volcar varios megas ahi congelaba la pantalla justo al abrir Ctrl+F3. Ahora se encarga a otro hilo,
        // asi que aqui hay que esperarlo (con tope, para que un fallo no deje la bateria colgada).
        string inf = (string)Call("CalidadFile");
        try { if (File.Exists(inf)) File.Delete(inf); } catch { }
        Call("EscribeInformeCalidad");
        bool hay = false;
        for (int esp = 0; esp < 100 && !hay; esp++) { hay = File.Exists(inf) && new FileInfo(inf).Length > 0; if (!hay) System.Threading.Thread.Sleep(50); }
        Check(hay, "escribe el informe rocola_calidad.txt");
        if (hay)
        {
            string[] ls = File.ReadAllLines(inf);
            int iPeor = -1, iMejor = -1;
            for (int i = 0; i < ls.Length; i++)
            { if (iPeor < 0 && ls[i].Contains("pobre")) iPeor = i; if (ls[i].Contains("buena")) iMejor = i; }
            Check(iPeor > 0 && iMejor > iPeor, "las peores salen PRIMERO (es la lista de que conviene reemplazar)");
            Console.WriteLine("  informe: " + ls.Length + " lineas, primera cancion listada: " + (iPeor > 0 ? ls[iPeor] : "?"));
        }

        // --- (B) des-recorte solo donde hace falta
        Check(!(bool)Call("DeclipVale", "buena3.mp3"), "una cancion SIN recorte no arrastra el des-recorte (ni su trabajo ni su retardo)");
        Check((bool)Call("DeclipVale", "pobre1.mp3"), "una cancion CON recorte si lo lleva");
        Check((bool)Call("DeclipVale", "sinmedir.mp3"), "una aun sin medir lo lleva por si acaso");
        Check((bool)Call("DeclipVale", "jamas_vista.mp3"), "una que ni esta en la tabla, tambien");

        // --- (C) realce por cancion
        float rBuena = Real("ExciteAmountFor", "buena0.mp3");
        float rMedia = Real("ExciteAmountFor", "media0.mp3");
        float rPobre = Real("ExciteAmountFor", "pobre0.mp3");
        float rNueva = Real("ExciteAmountFor", "sinmedir.mp3");
        Console.WriteLine(string.Format("\n  realce (perilla 35%%):  completa {0:0.0}%   aceptable {1:0.0}%   pobre {2:0.0}%   sin medir {3:0.0}%",
                                        rBuena*100, rMedia*100, rPobre*100, rNueva*100));
        Check(rPobre > rMedia && rMedia > rBuena, "cuanto mas dañada la cancion, MAS realce recibe (el EQ no puede llegar ahi, el realce si)");
        Check(Math.Abs(rNueva - 0.35f) < 0.001f, "una cancion sin medir recibe la perilla tal cual");
        SetFld("excAmount", 0f);
        Check(Real("ExciteAmountFor", "pobre0.mp3") == 0f, "con la perilla en 0 no hay realce, pase lo que pase");
        SetFld("excAmount", 0.35f);

        // --- (D) bajo por cancion segun el corte de ABAJO
        float bHonda = Real("BassFactorFor", "buena0.mp3");    // baja a 30 Hz
        float bCorta = Real("BassFactorFor", "pobre0.mp3");    // cortada en 85 Hz
        Console.WriteLine(string.Format("  bajo:  grabacion que baja a 30 Hz -> x{0:0.00}   |   cortada en 85 Hz -> x{1:0.00}", bHonda, bCorta));
        Check(bCorta > bHonda, "una grabacion sin grave propio recibe MAS ayuda del bajo psicoacustico");
        Check(bCorta <= 1.70f && bHonda >= 0.40f, "el ajuste sigue acotado (nunca se desmadra)");
        Pon("nueva2.mp3", 0f, 0f, 0f);
        Check(Math.Abs(Real("BassFactorFor", "nueva2.mp3") - 1f) < 0.001f, "una cancion sin medir: como siempre");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
