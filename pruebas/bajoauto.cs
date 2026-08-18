using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

// El bajo psicoacustico ya no es una perilla fija para todas: cada cancion recibe lo que le toca segun su
// propia huella tonal. Se comprueba que el ajuste apunte hacia donde debe y que nunca se desmadre.
class BajoAuto
{
    static Type TF; static object F; static MethodInfo FACTOR; static MethodInfo AHORA;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Dictionary<string, float[]> cache;
    static int LEN;   // el tamano de la ficha lo dice el programa: asi la prueba no se queda vieja
    static float Factor(string p) { return (float)FACTOR.Invoke(F, new object[] { p }); }

    // huella tonal: 8 bandas en dB respecto a la media de la propia cancion
    static void Pon(string nombre, float b50, float b158)
    {
        float[] c = new float[LEN];
        c[0] = b50; c[1] = b158;
        for (int i = 2; i < LEN; i++) c[i] = 0f;
        cache[nombre] = c;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        LEN = (int)TF.GetField("EqLen", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        cache = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        SetFld("eqCache", cache);
        SetFld("bassAmount", 0.30f);
        SetFld("bassSongFactor", 1f);
        FACTOR = TF.GetMethod("BassFactorFor", BindingFlags.Instance|BindingFlags.NonPublic);
        AHORA  = TF.GetMethod("BassAmountNow", BindingFlags.Instance|BindingFlags.NonPublic);
        if (FACTOR == null || AHORA == null) { Console.WriteLine("faltan metodos"); return 1; }

        // --- sin promedio de biblioteca todavia: todo se comporta como siempre
        SetFld("eqAvg", null);
        Pon("grave.mp3", +8f, -2f);
        Check(Math.Abs(Factor("grave.mp3") - 1f) < 0.0001f, "sin biblioteca medida (menos de 10 canciones) no se toca nada: como siempre");

        // --- ya hay promedio: la cancion promedio recibe exactamente lo que dice la perilla
        SetFld("eqAvg", new float[] { 2f, 1f, 0f, 0f, 0f, 0f, 0f, 0f });
        Pon("promedio.mp3", 2f, 1f);
        float fp = Factor("promedio.mp3");
        Console.WriteLine(string.Format("\n  cancion PROMEDIO de la biblioteca      -> factor {0:0.00}", fp));
        Check(Math.Abs(fp - 1f) < 0.02f, "la cancion tipica del local recibe la perilla tal cual");

        // --- una con MUCHO grave profundo y poco bajo-medio: se le ayuda mas
        Pon("mucho_grave.mp3", 2f + 6f, 1f - 2f);
        float fg = Factor("mucho_grave.mp3");
        Console.WriteLine(string.Format("  con MUCHO grave profundo (+8 dB rel.)  -> factor {0:0.00}", fg));
        Check(fg > 1.3f, "una cancion con mucho grave profundo recibe MAS ayuda");

        // --- una ya cargada de bajo-medio: se le baja (si no, se emborrona)
        Pon("cargada.mp3", 2f - 3f, 1f + 5f);
        float fc = Factor("cargada.mp3");
        Console.WriteLine(string.Format("  ya cargada de bajo-medio (-8 dB rel.)  -> factor {0:0.00}", fc));
        Check(fc < 0.7f, "una cancion que ya viene llena de bajo recibe MENOS (no se emborrona)");
        Check(fg > fp && fp > fc, "el orden es el correcto: grave > promedio > cargada");

        // --- nunca se desmadra, pase lo que pase
        Pon("absurda_alta.mp3", 60f, -20f);
        Pon("absurda_baja.mp3", -20f, 60f);
        float fa = Factor("absurda_alta.mp3"), fb = Factor("absurda_baja.mp3");
        Console.WriteLine(string.Format("  huellas absurdas (+80 / -80 dB)        -> factores {0:0.00} y {1:0.00}", fa, fb));
        Check(fa <= 1.55f && fb >= 0.45f, "por disparatada que sea la huella, el ajuste queda entre 0.45 y 1.55");

        // --- casos borde: no truena y cae en 1
        Check(Math.Abs(Factor(null) - 1f) < 0.0001f, "ruta nula: no truena, factor 1");
        Check(Math.Abs(Factor("nunca_medida.mp3") - 1f) < 0.0001f, "cancion aun sin medir: factor 1 (como siempre)");
        Pon("sin_graves.mp3", -30f, 1f);
        Check(Math.Abs(Factor("sin_graves.mp3") - 1f) < 0.0001f, "banda sin contenido medible: no se arriesga, factor 1");

        // --- la perilla sigue mandando y el resultado nunca se sale de 0..1
        SetFld("bassSongFactor", 1.55f);
        SetFld("bassAmount", 0.90f);
        float a1 = (float)AHORA.Invoke(F, null);
        Console.WriteLine(string.Format("\n  perilla al 90%% x factor 1.55           -> {0:0.00} (tope 1.00)", a1));
        Check(a1 <= 1.0001f, "aunque el ajuste multiplique, nunca se pasa del 100%");
        SetFld("bassAmount", 0f);
        Check((float)AHORA.Invoke(F, null) == 0f, "con la perilla en 0 el bajo queda APAGADO pase lo que pase");

        // ---------- BAJO EN AUTOMATICO: sin perilla, lo decide el analisis ----------
        // De cuanto se PARTE ya no lo dice una perilla: lo dice lo que el analisis del DSEE sabe del archivo.
        // Encima de eso sigue actuando el factor por cancion que se comprobo arriba.
        MethodInfo BAUTO = TF.GetMethod("BajoAuto", BindingFlags.Instance|BindingFlags.NonPublic);
        int CUT = (int)TF.GetField("EqCut", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        int CLIP = (int)TF.GetField("EqClip", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Check(BAUTO != null, "existe el bajo automatico");
        if (BAUTO != null)
        {
            cache.Clear();
            float[] normal = new float[LEN]; normal[CUT] = 21000f; cache["normal.mp3"] = normal;
            float[] recort = new float[LEN]; recort[CUT] = 21000f; recort[CLIP] = 0.01f; cache["recortada.mp3"] = recort;
            float[] pobre  = new float[LEN]; pobre[CUT] = 14000f; cache["pobre.mp3"] = pobre;
            float[] ambas  = new float[LEN]; ambas[CUT] = 14000f; ambas[CLIP] = 0.01f; cache["ambas.mp3"] = ambas;
            float bN = (float)BAUTO.Invoke(F, new object[]{ "normal.mp3" });
            float bR = (float)BAUTO.Invoke(F, new object[]{ "recortada.mp3" });
            float bP = (float)BAUTO.Invoke(F, new object[]{ "pobre.mp3" });
            float bA = (float)BAUTO.Invoke(F, new object[]{ "ambas.mp3" });
            float bSin = (float)BAUTO.Invoke(F, new object[]{ "nunca_medida.mp3" });
            Console.WriteLine("\n  DE CUANTO PARTE cada cancion, decidido por el analisis:");
            Console.WriteLine(string.Format("     completa y con margen         {0,3:0}%", bN*100f));
            Console.WriteLine(string.Format("     con los picos recortados      {0,3:0}%   (mas grave solo haria trabajar al limitador)", bR*100f));
            Console.WriteLine(string.Format("     copia pobre (agudos cortados) {0,3:0}%   (esas suelen ir flojas de abajo)", bP*100f));
            Console.WriteLine(string.Format("     pobre Y recortada             {0,3:0}%", bA*100f));
            Console.WriteLine(string.Format("     aun sin medir                 {0,3:0}%", bSin*100f));
            Check(bR < bN - 0.03f, "a una cancion con los picos aplastados se le mete MENOS grave");
            Check(bP > bN + 0.03f, "y a una copia pobre, MAS: es donde el bajo profundo mas se agradece");
            Check(Math.Abs(bSin - bN) < 0.001f, "una cancion aun sin medir recibe lo normal (no se le inventa nada)");
            Check(bA < bP && bA > bR, "y si viene pobre Y recortada, las dos cosas cuentan");
            Check(bN > 0.05f && bP < 0.6f, "ningun caso se va a los extremos");

            // y el automatico ES el que manda cuando esta puesto (la perilla queda en nada)
            SetFld("bassAuto", true); SetFld("bassAmount", 0f); SetFld("bassAutoBase", bP); SetFld("bassSongFactor", 1f);
            float conAuto = (float)AHORA.Invoke(F, null);
            SetFld("bassAuto", false);
            float conPerilla = (float)AHORA.Invoke(F, null);
            Console.WriteLine(string.Format("\n  con la perilla en 0%: en automatico suena al {0:0}%, a mano al {1:0}%", conAuto*100f, conPerilla*100f));
            Check(conAuto > 0.05f, "con la perilla en CERO el bajo sigue trabajando: es automatico de verdad");
            Check(conPerilla == 0f, "y quitando el automatico manda la perilla, como siempre");
            SetFld("bassAuto", true);

            // el factor por cancion sigue multiplicando encima del automatico
            SetFld("bassSongFactor", 1.5f);
            float conFactor = (float)AHORA.Invoke(F, null);
            Check(conFactor > conAuto * 1.4f, "y encima sigue actuando lo que le toca a cada cancion por su huella");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
