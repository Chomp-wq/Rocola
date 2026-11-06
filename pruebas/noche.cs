using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// EL MODO NOCHE, DE VUELTA (decision del dueno del 7-11-2026: «nos aventaron piedras por tener musica a las 12 A.M»).
//
// De noche_desde= a noche_hasta= (23 a 7 de fabrica) la rocola suena al noche_pct= % (de 70 a 90, 80 de fabrica) de lo que se oye;
// «Probar» en Ctrl+F3 -> Sonido lo pone 15 s al momento. (Del 24-10 al 6-11-2026 esta bateria vigilaba que se hubiera retirado.)
//
// LO QUE ESTA BATERIA VIGILA:
//
//  1) EL CRUCE DE LA MEDIANOCHE. «hora >= 23 && hora < 7» es siempre falso: tiene que ser un O. Si falla, el modo no ocurre nunca.
//  2) EL PORCENTAJE ES DE LO QUE SE OYE: 70 % = -5,1 dB, 80 % = -3,2 dB, 90 % = -1,5 dB (10*log2), y la clave se acota a 70-90.
//  3) LA RAMPA VA POR RELOJ (milisegundos, no vueltas) y no salta de golpe; «Probar» baja rapido y vuelve solo.
//  4) EL VOLUMEN SALE POR DOS SITIOS: la cadena de cada cancion (la que se arma, la preparada y el ajuste en vivo) y los videos, en
//     particular la rama del video YA MEDIDO, que se salta la ganancia general.
//  5) DE DIA NO CAMBIA NADA: con el modo sin actuar, el factor es exactamente 1.
//  6) LA CONFIGURACION: noche=, noche_desde=, noche_hasta=, noche_pct= se leen y se guardan; noche_db= y noche_compensa= (de antes)
//     se ignoran sin error.
class Noche
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        FieldInfo fOn = TF.GetField("nocheOn", T), fDesde = TF.GetField("nocheDesde", T), fHasta = TF.GetField("nocheHasta", T), fPct = TF.GetField("nochePct", T);
        MethodInfo mEs = TF.GetMethod("EsDeNoche", T), mDb = TF.GetMethod("NocheDb", T), mRampa = TF.GetMethod("AvanzaRampa", T);
        Check(fOn != null && fDesde != null && fHasta != null && fPct != null && mEs != null && mDb != null && mRampa != null, "el modo noche esta (campos, hora, dB y rampa)");
        if (fallos > 0) return fallos;

        Console.WriteLine("\n  1) EL CRUCE DE LA MEDIANOCHE\n");
        fDesde.SetValue(null, 23); fHasta.SetValue(null, 7);
        Func<int, bool> es = h => (bool)mEs.Invoke(null, new object[] { new DateTime(2026, 11, 7, h, 30, 0) });
        Check(es(23) && es(0) && es(3) && es(6), "de 23 a 7: las 23:30, 0:30, 3:30 y 6:30 son de noche");
        Check(!es(7) && !es(12) && !es(22), "las 7:30, 12:30 y 22:30 no");
        fDesde.SetValue(null, 1); fHasta.SetValue(null, 5);
        Check(es(1) && es(4) && !es(5) && !es(0), "un rango dentro del mismo dia (1 a 5) tambien");
        fDesde.SetValue(null, 3); fHasta.SetValue(null, 3);
        Check(!es(3) && !es(12), "desde = hasta: nunca");
        fDesde.SetValue(null, 23); fHasta.SetValue(null, 7);

        Console.WriteLine("\n  2) EL PORCENTAJE ES DE LO QUE SE OYE\n");
        Func<int, float> db = p => { fPct.SetValue(null, p); return (float)mDb.Invoke(null, null); };
        Check(Math.Abs(db(70) + 5.15f) < 0.05f && Math.Abs(db(80) + 3.22f) < 0.05f && Math.Abs(db(90) + 1.52f) < 0.05f,
              string.Format("70 % = {0:0.00} dB, 80 % = {1:0.00} dB, 90 % = {2:0.00} dB", db(70), db(80), db(90)));
        fPct.SetValue(null, 80);
        Check((int)TF.GetField("NochePctMin", T).GetValue(null) == 70 && (int)TF.GetField("NochePctMax", T).GetValue(null) == 90, "el deslizador va de 70 a 90");

        Console.WriteLine("\n  3) LA RAMPA VA POR RELOJ\n");
        Func<float, float, int, int, float> rampa = (a, m, ms, r) => (float)mRampa.Invoke(null, new object[] { a, m, ms, r });
        float lin80 = (float)Math.Pow(10, db(80) / 20); fPct.SetValue(null, 80);
        float x = 1f; int pasos = 0;
        while (x != lin80 && pasos < 10000) { x = rampa(x, lin80, 16, 20000); pasos++; }
        Check(pasos * 16 >= 4000 && pasos * 16 <= 20000, "al 80 % llega en " + (pasos * 16 / 1000.0).ToString("0.0") + " s (poco a poco, sin escalon)");
        Check(rampa(0.9f, lin80, 1000, 20000) == rampa(rampa(0.9f, lin80, 500, 20000), lin80, 500, 20000) || Math.Abs(rampa(0.9f, lin80, 1000, 20000) - rampa(rampa(0.9f, lin80, 500, 20000), lin80, 500, 20000)) < 1e-6f,
              "un segundo de golpe = dos medios segundos (cuenta milisegundos, no vueltas)");
        Check(rampa(0.9f, lin80, 0, 20000) == 0.9f && rampa(lin80, lin80, 500, 20000) == lin80, "sin tiempo no se mueve; en la meta se queda");
        x = 1f; pasos = 0;
        while (x != lin80 && pasos < 10000) { x = rampa(x, lin80, 16, 1500); pasos++; }
        Check(pasos * 16 <= 1500, "«Probar» baja en " + (pasos * 16) + " ms (rampa rapida)");

        Console.WriteLine("\n  4) LA CADENA Y LOS VIDEOS\n");
        foreach (string n in new string[] { "ArmaCadena", "AplicaAPreparada", "ApplyNormNow" })
        {
            MethodInfo m = TF.GetMethod(n, T);
            Check(m != null && Codigo.Llama(Codigo.Llamadas(m), "FactorNoche"), n + " baja la cancion de noche (FactorNoche)");
        }
        MethodInfo vv = TF.GetMethod("VideoVolumeFor", T);
        Check(vv != null && Codigo.Llama(Codigo.Llamadas(vv), "FactorVideoNoche"), "los videos tambien (fuera de la rama del video medido)");
        Check(Codigo.Llama(Codigo.Llamadas(TF.GetMethod("MasterLin", T)), "FactorNoche") == false, "y no dentro de MasterLin (esa rama se la salta)");
        MethodInfo av = TF.GetMethod("AvanzaModoNoche", T);
        List<string> la = Codigo.Llamadas(av);
        Check(av != null && Codigo.Llama(la, "AvanzaRampa") && Codigo.Llama(la, "AplicaAPreparada") && Codigo.Llama(la, "MasterEfectivo"),
              "el reloj lleva la rampa y se la empuja a la que suena y a la preparada");
        bool enTick = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) if (Codigo.Llama(Codigo.Llamadas(m), "AvanzaModoNoche") && Codigo.Llama(Codigo.Llamadas(m), "CheckBusinessDayReset")) enTick = true;
        Check(enTick, "el temporizador lo lleva al lado del corte del dia");
        bool probar = false, textos = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            if (Codigo.Llama(Codigo.Llamadas(m), "PruebaNoche")) probar = true;
            foreach (string t in Codigo.Textos(m)) if (t == "Probar") textos = true;
        }
        Check(probar && textos, "Ctrl+F3 tiene el boton «Probar»");

        Console.WriteLine("\n  5) DE DIA NO CAMBIA NADA, Y «PROBAR» VUELVE SOLO\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_noche_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            object f = Codigo.FormSinEncender(TF);
            MethodInfo mFac = TF.GetMethod("FactorNoche", T), mVid = TF.GetMethod("FactorVideoNoche", T), mProb = TF.GetMethod("PruebaNoche", T);
            FieldInfo fArr = TF.GetField("nocheArrancada", T), fFac = TF.GetField("nocheFactor", T), fHastaP = TF.GetField("nochePruebaHasta", T);
            fOn.SetValue(null, false);
            Check((float)mFac.Invoke(f, null) == 1f && (float)mVid.Invoke(f, null) == 1f, "con el modo apagado y sin probar: exactamente 1 (musica y videos)");
            mProb.Invoke(f, null);
            Check(Math.Abs((float)mFac.Invoke(f, null) - lin80) < 1e-6f, "«Probar», antes de la primera vuelta del reloj: ya en el nivel de la noche");
            fArr.SetValue(f, true); fFac.SetValue(f, lin80);
            Check(Math.Abs((float)mVid.Invoke(f, null) - 0.80f) < 1e-4f, "los videos, al 80 % de su perilla");
            fHastaP.SetValue(f, (Environment.TickCount - 10) | 1);   // la prueba ya acabo
            TF.GetField("nocheUltMs", T).SetValue(f, Environment.TickCount - 2000);
            bool ok = true; try { av.Invoke(f, null); } catch (Exception e) { ok = false; Console.WriteLine("    " + (e.InnerException ?? e).Message); }
            float tras = (float)fFac.GetValue(f);
            Check(ok && tras == 1f && (int)fHastaP.GetValue(f) == 0, "acabada la prueba, de dia, vuelve a 1 rapido (en " + (tras == 1f ? "menos de 2 s" : "mas de 2 s") + ")");

            Console.WriteLine("\n  6) LA CONFIGURACION\n");
            foreach (FieldInfo fi in TF.GetFields(T))
                if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(f) == null) fi.SetValue(f, Activator.CreateInstance(fi.FieldType, true));
            File.WriteAllText(Path.Combine(dir, "rocola_config.txt"),
                "noche=0\r\nnoche_desde=22\r\nnoche_hasta=6\r\nnoche_pct=75\r\nnoche_db=-12\r\nnoche_compensa=80\r\nvideo_vol=70\r\n");
            bool leyo = true;
            try { TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null); } catch { leyo = false; }
            Check(leyo && !(bool)fOn.GetValue(null) && (int)fDesde.GetValue(null) == 22 && (int)fHasta.GetValue(null) == 6 && (int)fPct.GetValue(null) == 75,
                  "se leen noche=, noche_desde=, noche_hasta= y noche_pct= (noche_db= y noche_compensa= se ignoran sin error)");
            File.WriteAllText(Path.Combine(dir, "rocola_config.txt"), "noche_pct=40\r\n");
            TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null);
            Check((int)fPct.GetValue(null) == 70, "noche_pct=40 se acota a 70");
            fOn.SetValue(null, true); fPct.SetValue(null, 85);
            TF.GetMethod("SaveConfigPrefs", T).Invoke(f, null);
            Codigo.EsperaGuardado(TF, f);
            string nuevo = File.ReadAllText(Path.Combine(dir, "rocola_config.txt"));
            Check(nuevo.Contains("noche=1\r\n") && nuevo.Contains("noche_desde=22\r\n") && nuevo.Contains("noche_hasta=6\r\n") && nuevo.Contains("noche_pct=85\r\n")
                  && !nuevo.Contains("noche_db=") && !nuevo.Contains("noche_compensa="), "se guardan las cuatro (y las de antes ya no)");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN (" + oks + " comprobaciones)" : "  FALLOS: " + fallos);
        return fallos;
    }
}
