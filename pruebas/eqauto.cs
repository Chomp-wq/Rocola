using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

// EQ automatico. Antes el objetivo era el promedio de TODA la biblioteca -dañadas incluidas-, asi que las
// canciones sanas eran empujadas hacia la mediocridad. Ahora el objetivo sale solo de las sanas, y a una
// cancion dañada no se le suben las bandas que el codec le borro (ahi no hay musica que subir).
class EqAuto
{
    static Type TF; static object F; static MethodInfo RECALC, CORR;
    static Dictionary<string,float[]> cache;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }
    static int CUT, LEN;

    // huella: 8 bandas en dB + corte en Hz
    static void Pon(string nom, float b0, float b6, float b7, float corte)
    {
        float[] c = new float[9];
        c[0]=b0; c[1]=0; c[2]=0; c[3]=0; c[4]=0; c[5]=0; c[6]=b6; c[7]=b7; c[CUT]=corte;
        cache[nom]=c;
    }
    static float[] Corr(string p) { return (float[])CORR.Invoke(F, new object[] { p }); }
    static float[] Avg() { RECALC.Invoke(F, null); return (float[])Fld("eqAvg"); }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        CUT = (int)TF.GetField("EqCut", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        LEN = (int)TF.GetField("EqLen", BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        cache = new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase);
        SetFld("eqCache", cache); SetFld("eqAmount", 1f);
        SetFld("sbrOn", false); SetFld("sbrAllowed", false);
        RECALC = TF.GetMethod("RecomputeEqAvg", BindingFlags.Instance|BindingFlags.NonPublic);
        CORR = TF.GetMethod("EqCorrFor", BindingFlags.Instance|BindingFlags.NonPublic);
        if (RECALC == null || CORR == null) { Console.WriteLine("faltan metodos"); return 1; }

        // Biblioteca: 12 canciones DAÑADAS (sin aire, corte a 15 kHz) y 12 SANAS (con aire, sin corte).
        for (int i = 0; i < 12; i++) Pon("dañada" + i + ".mp3", 0f, -12f, -30f, 15000f);
        for (int i = 0; i < 12; i++) Pon("sana" + i + ".mp3",   0f,   0f,  -2f, 22050f);
        float[] a = Avg();
        int sanas = (int)Fld("eqAvgSanas"); bool soloSanas = (bool)Fld("eqAvgSoloSanas");
        Console.WriteLine(string.Format("\n  Biblioteca: 12 dañadas + 12 sanas"));
        Console.WriteLine(string.Format("  objetivo sacado de {0} canciones (solo sanas: {1})", sanas, soloSanas));
        Console.WriteLine(string.Format("  objetivo en brillo (10 kHz) = {0:0.0} dB   |   en aire (15.8 kHz) = {1:0.0} dB", a[6], a[7]));
        Check(soloSanas && sanas == 12, "el objetivo se calcula SOLO con las 12 sanas");
        Check(a[6] > -1f && a[7] > -3f, "el objetivo NO viene apagado (antes, con las dañadas dentro, se hundia)");

        // ¿cuanto se hundia antes? el promedio de las 24
        float viejo6 = (12*-12f + 12*0f)/24f, viejo7 = (12*-30f + 12*-2f)/24f;
        Console.WriteLine(string.Format("  (con el promedio VIEJO de las 24 habria sido {0:0.0} y {1:0.0} dB)", viejo6, viejo7));
        Check(a[6] - viejo6 > 3f, "el objetivo de brillo sube de verdad al dejar fuera las dañadas");

        // una cancion sana no deberia recibir casi correccion
        float[] cs = Corr("sana3.mp3");
        Console.WriteLine(string.Format("\n  cancion SANA      -> correccion en brillo {0,5:0.0} dB, en aire {1,5:0.0} dB", cs == null ? 0f : cs[6], cs == null ? 0f : cs[7]));
        Check(cs == null || (Math.Abs(cs[6]) < 0.5f && Math.Abs(cs[7]) < 1.5f), "a una cancion bien grabada casi no se le toca");

        // una dañada: NO se le suben las bandas muertas (el codec las borro)
        float[] cd = Corr("dañada3.mp3");
        Console.WriteLine(string.Format("  cancion DAÑADA    -> correccion en brillo {0,5:0.0} dB, en aire {1,5:0.0} dB   (corte a 15 kHz)", cd == null ? 0f : cd[6], cd == null ? 0f : cd[7]));
        Check(cd == null || Math.Abs(cd[7]) < 0.001f, "NO se sube el aire de una cancion a la que el codec se lo borro (seria subir ruido)");
        Check(cd != null && cd[6] > 0.5f, "pero el brillo (10 kHz, POR DEBAJO de su corte) si se corrige: ahi si hay musica");

        // con el DSEE encendido tampoco se toca: lo que suena ya lleva los agudos re-creados
        SetFld("sbrOn", true); SetFld("sbrAllowed", true);
        float[] cd2 = Corr("dañada3.mp3");
        Console.WriteLine(string.Format("  la misma, con DSEE-> correccion en brillo {0,5:0.0} dB, en aire {1,5:0.0} dB", cd2 == null ? 0f : cd2[6], cd2 == null ? 0f : cd2[7]));
        Check(cd2 != null && Math.Abs(cd2[7]) < 0.001f, "con el DSEE encendido TAMPOCO se corrige el aire (ya lo re-creo el DSEE; corregir seria empujar dos veces)");
        SetFld("sbrOn", false); SetFld("sbrAllowed", false);

        // biblioteca ENTERA de baja calidad: no puede quedarse sin objetivo
        cache.Clear();
        for (int i = 0; i < 20; i++) Pon("mala" + i + ".mp3", 0f, -12f, -30f, 15000f);
        float[] a2 = Avg();
        Check(a2 != null && !(bool)Fld("eqAvgSoloSanas") && (int)Fld("eqAvgSanas") == 20,
              "si NINGUNA cancion esta sana, se usan todas (no se queda sin EQ automatico)");

        // pocas canciones: sin objetivo, como siempre
        cache.Clear(); Pon("una.mp3", 0f, 0f, 0f, 22050f);
        Check(Avg() == null, "con menos de 10 canciones medidas no hay objetivo (como siempre)");
        Check(Corr("una.mp3") == null, "y sin objetivo no se corrige nada");

        // huella vieja (sin corte guardado): no cuenta como sana, pero tampoco rompe nada
        cache.Clear();
        for (int i = 0; i < 15; i++) Pon("vieja" + i + ".mp3", 0f, -4f, -6f, 0f);
        float[] a3 = Avg();
        Check(a3 != null && (int)Fld("eqAvgSanas") == 15, "las huellas del formato viejo (sin corte) siguen sirviendo");

        // ---------- MODO AUTOMATICO: sin perilla, lo decide el programa ----------
        // De fabrica no hay nada que ajustar: la fuerza sale de cuanto se puede fiar uno del objetivo, que
        // depende de cuantas canciones sanas lleva medidas el pre-analisis.
        MethodInfo FUERZA = TF.GetMethod("EqFuerzaAuto", BindingFlags.Instance|BindingFlags.NonPublic);
        Check(FUERZA != null, "existe la fuerza automatica del EQ");
        if (FUERZA != null)
        {
            SetFld("eqAuto", true);
            SetFld("eqAmount", 0f);          // la perilla en CERO: en automatico no pinta nada
            Console.WriteLine("\n  FUERZA AUTOMATICA segun cuantas canciones sanas lleva medidas:");
            int[] cuantas = { 4, 10, 20, 35, 60, 200 };
            float ant = -1f; bool sube = true, tope = true, ceroAlPrincipio = false;
            for (int k = 0; k < cuantas.Length; k++)
            {
                cache.Clear();
                for (int i = 0; i < cuantas[k]; i++) Pon("s" + i + ".mp3", 0f, 0f, -2f, 22050f);
                Avg();
                float f = (float)FUERZA.Invoke(F, null);
                Console.WriteLine(string.Format("     {0,4:0} canciones  ->  {1,3:0}%", cuantas[k], f*100f));
                if (cuantas[k] < 10 && f == 0f) ceroAlPrincipio = true;
                if (f < ant - 0.0001f) sube = false;
                if (f > 0.86f) tope = false;
                ant = f;
            }
            Check(ceroAlPrincipio, "con pocas canciones medidas el EQ no toca NADA (el objetivo aun no significa nada)");
            Check(sube, "y segun se mide la biblioteca va corrigiendo mas: nunca al reves");
            Check(tope, "pero nunca llega al 100%: siempre deja un resto sin corregir para no aplanar los discos");

            // y con la perilla en CERO sigue corrigiendo: eso es lo que hace que sea automatico de verdad
            cache.Clear();
            for (int i = 0; i < 40; i++) Pon("s" + i + ".mp3", 0f, 0f, -2f, 22050f);
            Pon("apagada.mp3", 0f, -8f, -9f, 22050f);
            Avg();
            float[] cA = Corr("apagada.mp3");
            Console.WriteLine(string.Format("\n  con la perilla en 0%, una cancion apagada recibe {0:0.0} dB de brillo", cA == null ? 0f : cA[6]));
            Check(cA != null && cA[6] > 1.5f, "con la perilla en CERO el EQ sigue trabajando: es automatico de verdad");

            // CANCION RARA: cuanto mas se aleja del resto, con mas cuidado se le toca (no se la aplana).
            // Las dos van 3 dB flojas de brillo: lo unico que cambia es que a una ADEMAS le falta medio
            // espectro. La que se sale de madre debe recibir MENOS correccion en ese brillo, no la misma.
            cache.Clear();
            for (int i = 0; i < 40; i++) Pon("s" + i + ".mp3", 0f, 0f, 0f, 22050f);
            float[] cerca = new float[LEN]; cerca[6] = -3f; cerca[CUT] = 22050f; cache["cerca.mp3"] = cerca;
            float[] lejos = new float[LEN]; lejos[6] = -3f; lejos[CUT] = 22050f;
            for (int b = 1; b <= 5; b++) lejos[b] = -20f;      // ademas, medio espectro hundido: es rarisima
            cache["lejos.mp3"] = lejos;
            Avg();
            float[] cc = Corr("cerca.mp3"), cl = Corr("lejos.mp3");
            Console.WriteLine(string.Format("  las dos van 3 dB flojas de brillo: a la NORMAL se le suben {0:0.00} dB y a la RARA {1:0.00} dB",
                                            cc == null ? 0f : cc[6], cl == null ? 0f : cl[6]));
            Check(cc != null && cl != null && cl[6] < cc[6] * 0.80f, "a una cancion muy distinta al resto se le toca MENOS: seguramente queria sonar asi");
            Check(cl != null && cl[6] > cc[6] * 0.35f, "pero tampoco se la deja sin tocar del todo");

            // a mano: manda la perilla y no se aplica el cuidado de la cancion rara
            SetFld("eqAuto", false); SetFld("eqAmount", 1f);
            float[] cm = Corr("lejos.mp3");
            Console.WriteLine(string.Format("  a mano, con la perilla al 100%, esa misma recibiria {0:0.00} dB", cm == null ? 0f : cm[6]));
            Check(cm != null && cm[6] > (cl == null ? 0f : cl[6]) + 0.5f, "quitando el automatico manda la perilla, como siempre");
            SetFld("eqAuto", true);
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}
