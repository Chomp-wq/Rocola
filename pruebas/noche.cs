using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL MODO NOCHE, DE VUELTA (decision del dueno del 7-11-2026: «nos aventaron piedras por tener musica a las 12 A.M»).
//
// De noche_desde= a noche_hasta= (22 a 7 de fabrica desde el 17-11-2026; antes 23 a 7) la rocola suena al noche_pct= % (de 70 a 90, 80 de
// fabrica) de lo que se oye; «Probar» en Ctrl+F3 -> Sonido lo pone al momento hasta otro toque (antes 15 s). (Del 24-10 al 6-11-2026 esta bateria vigilaba que se
// hubiera retirado.)
//
// 17-11-2026 (el dueno: «que empiece desde las 10, pero que vaya bajando extremadamente lento el volumen [...] que tarde una hora en
// llegar al objetivo» y N1 «de noche, menos grave hacia afuera»): empieza a las 22 h (las rocolas con noche_desde=23 guardado pasan a 22
// UNA vez, con la marca noche_desde_22=1); la bajada de la noche tarda 60 minutos, PAREJA EN dB y sin aviso en pantalla; la subida de
// la manana, el deslizador y «Probar» siguen con sus rampas rapidas; y de noche el estante de 120 Hz del local baja noche_graves= dB
// (0 a 6, 4 de fabrica) con el mismo avance (SalaLocal.GravesNocheDb: 0 de dia y la etapa sale bit a bit como antes).
//
// LO QUE ESTA BATERIA VIGILA:
//
//  1) EL CRUCE DE LA MEDIANOCHE. «hora >= 23 && hora < 7» es siempre falso: tiene que ser un O. Si falla, el modo no ocurre nunca.
//  2) EL PORCENTAJE ES DE LO QUE SE OYE: 70 % = -5,1 dB, 80 % = -3,2 dB, 90 % = -1,5 dB (10*log2), y la clave se acota a 70-90.
//  3) LA RAMPA VA POR RELOJ (milisegundos, no vueltas) y no salta de golpe; «Probar» baja rapido y vuelve solo.
//  4) EL VOLUMEN SALE POR DOS SITIOS: la cadena de cada cancion (la que se arma, la preparada y el ajuste en vivo) y los videos, en
//     particular la rama del video YA MEDIDO, que se salta la ganancia general.
//  5) DE DIA NO CAMBIA NADA: con el modo sin actuar, el factor es exactamente 1.
//  6) LA CONFIGURACION: noche=, noche_desde=, noche_hasta=, noche_pct=, noche_graves= se leen y se guardan; noche_db= y noche_compensa= (de antes)
//     se ignoran sin error; el 23 guardado pasa a 22 una vez (noche_desde_22=).
//  7) LA BAJADA DE UNA HORA, PAREJA EN dB, y el arranque ya de noche.
//  8) LA SUBIDA DE LA MANANA (20 s), «PROBAR» RAPIDO, EL GRAVE DE NOCHE (N1) y SalaLocal bit a bit de dia.
class FuenteN : ISampleProvider
{
    readonly float[] x; int pos; readonly WaveFormat wf;
    public FuenteN(float[] datos, int fs, int ch) { x = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n)
    {
        int k = Math.Min(n, x.Length - pos);
        if (k <= 0) return 0;
        Array.Copy(x, pos, b, o, k); pos += k; return k;
    }
}

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
        Check((int)fDesde.GetValue(null) == 22 && (int)fHasta.GetValue(null) == 7, "de fabrica empieza a las 22 h y acaba a las 7");

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
        Check(pasos * 16 >= 4000 && pasos * 16 <= 20000, "la rampa de 20 s (subida de la manana, deslizador): al 80 % llega en " + (pasos * 16 / 1000.0).ToString("0.0") + " s (poco a poco, sin escalon)");
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
        Check(av != null && Codigo.Llama(la, "AvanzaRampa") && Codigo.Llama(la, "MasterEfectivo") && Codigo.LeeCampo(new List<MethodBase> { av }, "cadenaPreparada"),
              "el reloj lleva la rampa y se la empuja a la que suena y a la preparada (su volumen)");
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
            // Interruptor (17-11-2026, «que se quede hasta que lo vuelva a pulsar»): se queda mucho mas que los 15 s de antes, y otro toque la quita.
            int quedan = unchecked((int)fHastaP.GetValue(f) - Environment.TickCount);
            Check(quedan > 25 * 60 * 1000, "«Probar» se queda hasta otro toque (con tope de seguridad de 30 min): quedan " + quedan / 60000 + " min");
            mProb.Invoke(f, null);
            fArr.SetValue(f, false);
            Check((float)mFac.Invoke(f, null) == 1f, "otro toque la quita: de dia y sin el modo, otra vez exactamente 1");
            mProb.Invoke(f, null);
            Check(Math.Abs((float)mFac.Invoke(f, null) - lin80) < 1e-6f, "y un tercer toque la vuelve a poner");
            fArr.SetValue(f, true); fFac.SetValue(f, lin80);
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
            // El 23 guardado (el de fabrica de antes) pasa a 22 UNA vez; con la marca, o con otro valor, se respeta.
            FieldInfo fGrav = TF.GetField("nocheGraves", T);
            Action<string> carga = txt => { File.WriteAllText(Path.Combine(dir, "rocola_config.txt"), txt); TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null); };
            carga("noche=1\r\nnoche_desde=23\r\nnoche_hasta=7\r\n");
            Check((int)fDesde.GetValue(null) == 22, "un noche_desde=23 sin la marca pasa a 22 (la rocola que guardo el valor de fabrica de antes)");
            carga("noche_desde=23\r\nnoche_desde_22=1\r\n");
            Check((int)fDesde.GetValue(null) == 23, "con la marca noche_desde_22=1, un 23 puesto a mano se respeta (la marca despues de la clave)");
            carga("noche_desde_22=1\r\nnoche_desde=23\r\n");
            Check((int)fDesde.GetValue(null) == 23, "(la marca antes de la clave, igual)");
            carga("noche_desde=21\r\n");
            Check((int)fDesde.GetValue(null) == 21, "un 21 sin marca tambien se respeta (solo el 23 se movia)");
            carga("noche_desde=0\r\n");
            Check((int)fDesde.GetValue(null) == 0, "y un 0");
            carga("noche_graves=9\r\n");
            Check((int)fGrav.GetValue(null) == 6, "noche_graves=9 se acota a 6");
            carga("noche_graves=2\r\n");
            Check((int)fGrav.GetValue(null) == 2, "noche_graves=2 se lee");
            carga("noche_graves=0\r\n");
            Check((int)fGrav.GetValue(null) == 0, "noche_graves=0 (apagado) se lee");
            fDesde.SetValue(null, 22); fHasta.SetValue(null, 6);
            File.WriteAllText(Path.Combine(dir, "rocola_config.txt"), "noche_pct=40\r\n");
            TF.GetMethod("LoadConfigPrefs", T).Invoke(f, null);
            Check((int)fPct.GetValue(null) == 70, "noche_pct=40 se acota a 70");
            fOn.SetValue(null, true); fPct.SetValue(null, 85);
            TF.GetMethod("SaveConfigPrefs", T).Invoke(f, null);
            Codigo.EsperaGuardado(TF, f);
            string nuevo = File.ReadAllText(Path.Combine(dir, "rocola_config.txt"));
            Check(nuevo.Contains("noche=1\r\n") && nuevo.Contains("noche_desde=22\r\n") && nuevo.Contains("noche_hasta=6\r\n") && nuevo.Contains("noche_pct=85\r\n")
                  && !nuevo.Contains("noche_db=") && !nuevo.Contains("noche_compensa="), "se guardan las cuatro (y las de antes ya no)");
            Check(nuevo.Contains("noche_desde_22=1\r\n") && nuevo.Contains("noche_graves=0\r\n"), "y se guardan la marca noche_desde_22=1 y noche_graves=");

            Console.WriteLine("\n  7) LA BAJADA DE UNA HORA, PAREJA EN dB\n");
            Type TSala = TF.GetNestedType("SalaLocal", T);
            FieldInfo fGN = TSala.GetField("GravesNocheDb", T), fUlt = TF.GetField("nocheUltMs", T), fMira = TF.GetField("nocheMirada", T);
            MethodInfo mFacAv = TF.GetMethod("FactorDeAvance", T), mAvAv = TF.GetMethod("AvanzaAvance", T);
            Check(fGN != null && fGrav != null && fUlt != null && mFacAv != null && mAvAv != null, "estan el avance de la noche, su factor y el grave de noche");
            fPct.SetValue(null, 80); fGrav.SetValue(null, 4);
            double dbN = db(80); fPct.SetValue(null, 80);
            Func<float, double> aDb = v => 20.0 * Math.Log10(v);
            // Un reloj de mentira: cada vuelta del temporizador pasan 'ms' milisegundos (AvanzaModoNoche mide contra nocheUltMs; el tope es 5000).
            Action<int> vuelta = ms => { fUlt.SetValue(f, Environment.TickCount - ms); fMira.SetValue(f, 0); av.Invoke(f, null); };
            Func<float> factor = () => (float)mFac.Invoke(f, null);
            Func<float> grave = () => (float)fGN.GetValue(null);
            // Dia: arranca sin rampa, en 1.
            fDesde.SetValue(null, 3); fHasta.SetValue(null, 3); fOn.SetValue(null, true); fArr.SetValue(f, false); fHastaP.SetValue(f, 0);
            vuelta(4000);
            Check(factor() == 1f && grave() == 0f, "de dia: el volumen es 1 y el grave de noche 0");
            // Empieza la noche (para el rango 0 a 24 siempre es de noche) con la rocola encendida: baja en 60 minutos.
            fDesde.SetValue(null, 0); fHasta.SetValue(null, 24);
            bool fallo = false;
            double d10 = 0, d20 = 0, d59 = 0, d60 = 0, g30 = 0, g60 = 0; bool bajaSiempre = true; float previo = 1f; int k; bool dormido = true;
            for (k = 1; k <= 900; k++)
            {
                try { vuelta(4000); } catch (Exception e) { fallo = true; Console.WriteLine("    " + (e.InnerException ?? e).Message); break; }
                float v = factor(); if (v > previo) bajaSiempre = false; previo = v;
                if (k == 100) { vuelta(6000); dormido = factor() == v; }   // un salto de reloj de mas de 5 s no cuenta (dt > 5000 -> 0)
                if (k == 150) d10 = aDb(v); if (k == 300) d20 = aDb(v); if (k == 450) g30 = grave(); if (k == 885) d59 = aDb(v); if (k == 900) { d60 = aDb(v); g60 = grave(); }
            }
            Check(!fallo, "la bajada corre sin aviso ni fallo (el aviso de cuando empieza ya no sale)");
            Console.WriteLine("        a 10 min " + d10.ToString("0.000") + " dB, a 20 min " + d20.ToString("0.000") + " dB, a 59 min " + d59.ToString("0.000") + " dB, a 60 min " + d60.ToString("0.000") + " dB (objetivo " + dbN.ToString("0.000") + ")");
            Check(bajaSiempre, "el volumen solo baja, vuelta a vuelta (sin escalon)");
            Check(dormido, "el tope se conserva: una vuelta de mas de 5 s no mueve nada (equipo dormido)");
            Check(d59 > dbN + 0.05 && d59 < 0, "a los 59 minutos todavia no llega al objetivo (le faltan " + (d59 - dbN).ToString("0.000") + " dB)");
            Check(Math.Abs(d60 - dbN) < 0.002, "a los 60 minutos llega al objetivo (" + d60.ToString("0.000") + " dB)");
            Check(Math.Abs(d10 - dbN / 6.0) < 0.01 && Math.Abs(d20 - dbN / 3.0) < 0.01, "PAREJO EN dB: a 10 y 20 min lleva 1/6 y 2/6 de los dB (" + d10.ToString("0.000") + ", " + d20.ToString("0.000") + ")");
            float fMitad = (float)mFacAv.Invoke(null, new object[] { 0.5 });
            Check(Math.Abs(aDb(fMitad) - dbN / 2.0) < 0.001, "a la mitad del tiempo, la mitad de los dB");
            Check(Math.Abs((float)mVid.Invoke(f, null) - 0.80f) < 1e-3f, "los videos, al final, al 80 % de su perilla (coherentes con la bajada)");
            Check(Math.Abs(g30 - 2.0) < 0.02 && Math.Abs(g60 - 4.0) < 0.01, "el grave de noche: la mitad (" + g30.ToString("0.00") + " dB) a mitad de la bajada y todo (" + g60.ToString("0.00") + " dB) al final");
            double avTodo = (double)mAvAv.Invoke(null, new object[] { 0.0, true, 3600000 }), avCasi = (double)mAvAv.Invoke(null, new object[] { 0.0, true, 3599000 });
            Check(avTodo == 1.0 && avCasi < 1.0, "AvanzaAvance: 3 600 000 ms hacia la noche = el avance completo; 3 599 000, no");
            // Videos a mitad de la bajada: entre 1 y 0,80.
            fArr.SetValue(f, false); fOn.SetValue(null, true); vuelta(4000);   // arranca ya de noche: al momento, en el nivel de la noche
            Check(Math.Abs(factor() - lin80) < 1e-6f && Math.Abs(grave() - 4f) < 0.01f, "si la rocola se ENCIENDE ya de noche, arranca en el nivel de la noche al momento (volumen y grave)");

            Console.WriteLine("\n  8) LA SUBIDA DE LA MANANA, «PROBAR» Y EL GRAVE DE NOCHE\n");
            // Se acaba la noche con la bajada completa: sube en 20 s.
            fDesde.SetValue(null, 3); fHasta.SetValue(null, 3);   // rango vacio: ya es de dia
            TF.GetField("nocheAvisada", T).SetValue(f, false);    // (el aviso de «se acabo» necesita la ventana viva: aqui no hay; se salta)
            bool sube = true; int pasosSube = 0;
            for (k = 0; k < 12; k++)
            {
                try { vuelta(2000); } catch (Exception e) { sube = false; Console.WriteLine("    " + (e.InnerException ?? e).Message); break; }
                pasosSube++; if (factor() == 1f) break;
            }
            Check(sube && factor() == 1f && pasosSube * 2 <= 22 && pasosSube * 2 >= 8, "la subida de la manana sigue en unos 20 s (llego en " + (pasosSube * 2) + " s)");
            Check(grave() == 0f, "y el grave de noche vuelve a 0");
            // «Probar»: rapido, con el grave tambien, y vuelve solo.
            mProb.Invoke(f, null);
            int pasosP = 0; for (k = 0; k < 12; k++) { vuelta(500); pasosP++; if (Math.Abs(factor() - lin80) < 1e-6f && Math.Abs(grave() - 4f) < 0.01f) break; }
            Check(pasosP * 0.5 <= 3.0, "«Probar» baja el volumen y el grave en " + (pasosP * 0.5) + " s (rampa rapida de 1,5 s)");
            fHastaP.SetValue(f, (Environment.TickCount - 10) | 1);   // la prueba acabo
            int pasosV = 0; for (k = 0; k < 12; k++) { vuelta(500); pasosV++; if (factor() == 1f && grave() == 0f) break; }
            Check(factor() == 1f && grave() == 0f && pasosV * 0.5 <= 4.0, "acabada la prueba, de dia, vuelve a 1 y a 0 dB de grave rapido (" + (pasosV * 0.5) + " s)");
            // El avance del grave: a la mitad, la mitad.
            MethodInfo mGN = TF.GetMethod("GravesNocheDe", T);
            Check((float)mGN.Invoke(null, new object[] { 0f }) == 0f && Math.Abs((float)mGN.Invoke(null, new object[] { 0.5f }) - 2f) < 1e-6f && Math.Abs((float)mGN.Invoke(null, new object[] { 1f }) - 4f) < 1e-6f,
                  "el grave de noche: 0 de dia, la mitad a mitad de la rampa, el valor completo al final (4 dB)");
            fGrav.SetValue(null, 0);
            Check((float)mGN.Invoke(null, new object[] { 1f }) == 0f, "noche_graves=0: apagado del todo");
            fGrav.SetValue(null, 4); fGN.SetValue(null, 0f);

            // SalaLocal: de dia (0) bit a bit; de noche, el grave baja y el medio casi no.
            int fs = 44100;
            Func<float[], int, float[]> pasa = (xs, ch) =>
            {
                ISampleProvider s = (ISampleProvider)Activator.CreateInstance(TSala, T, null, new object[] { new FuenteN(xs, fs, ch), null, 0.26f, false, false, 0, 0, 0 }, null);
                List<float> r = new List<float>(); float[] b = new float[4096]; int n;
                while ((n = s.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) r.Add(b[i]);
                return r.ToArray();
            };
            Func<double, float[]> tono = hz => { float[] xt = new float[fs * 2 * 2]; for (int i = 0; i < fs * 2; i++) { float v = (float)(0.25 * Math.Sin(2 * Math.PI * hz * i / fs)); xt[2 * i] = v; xt[2 * i + 1] = v; } return xt; };
            Func<float[], double> ef = y => { double s = 0; int a = fs, b = y.Length / 2; for (int i = a; i < b; i++) s += (double)y[2 * i] * y[2 * i]; return Math.Sqrt(s / (b - a)); };
            Random rnd = new Random(7); float[] ruido = new float[fs * 2]; for (int i = 0; i < ruido.Length; i++) ruido[i] = (float)(0.3 * (rnd.NextDouble() * 2 - 1));
            fGN.SetValue(null, 0f);
            float[] ry = pasa(ruido, 2); bool igual = ry.Length == ruido.Length; for (int i = 0; igual && i < ruido.Length; i++) if (ry[i] != ruido[i]) igual = false;
            Check(igual, "con el grave de noche en 0 (de dia), una senal pasa por SalaLocal IDENTICA, bit a bit");
            float[] t60 = tono(60), t1k = tono(1000);
            double g60d = 20 * Math.Log10(ef(pasa(t60, 2)) / ef(t60)), g1kd = 20 * Math.Log10(ef(pasa(t1k, 2)) / ef(t1k));
            Check(Math.Abs(g60d) < 1e-9 && Math.Abs(g1kd) < 1e-9, "de dia: 60 Hz y 1 kHz salen sin cambio (" + g60d.ToString("0.0000") + " y " + g1kd.ToString("0.0000") + " dB)");
            fGN.SetValue(null, 4f);
            g60d = 20 * Math.Log10(ef(pasa(t60, 2)) / ef(t60)); g1kd = 20 * Math.Log10(ef(pasa(t1k, 2)) / ef(t1k));
            Console.WriteLine("        con 4 dB menos de grave: 60 Hz " + g60d.ToString("0.00") + " dB, 1 kHz " + g1kd.ToString("0.00") + " dB");
            Check(g60d < -3.0 && g60d > -4.5, "de noche una senal de 60 Hz baja cerca de 4 dB");
            Check(Math.Abs(g1kd) < 0.3, "y una de 1 kHz casi no cambia");
            // Se SUMA al grave del local: el mismo filtro (mismo objetivo = mismos bits).
            Func<int, float, float[]> conGraves = (gl, gn) =>
            {
                fGN.SetValue(null, gn);
                ISampleProvider s = (ISampleProvider)Activator.CreateInstance(TSala, T, null, new object[] { new FuenteN(ruido, fs, 2), null, 0.26f, false, false, 0, gl, 0 }, null);
                List<float> r = new List<float>(); float[] b = new float[4096]; int n;
                while ((n = s.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) r.Add(b[i]);
                return r.ToArray();
            };
            float[] a1 = conGraves(0, 4f), a2 = conGraves(-4, 0f);
            bool mismo = a1.Length == a2.Length; for (int i = 0; mismo && i < a1.Length; i++) if (a1[i] != a2[i]) mismo = false;
            Check(mismo, "se SUMA al grave del local: graves 0 + noche 4 dB = graves -4 dB (mismos bits)");
            float[] b1 = conGraves(5, 3f), b2 = conGraves(2, 0f);
            mismo = b1.Length == b2.Length; for (int i = 0; mismo && i < b1.Length; i++) if (b1[i] != b2[i]) mismo = false;
            Check(mismo, "con graves del local (+5) y noche (3 dB): el mismo filtro que +2 dB");
            fGN.SetValue(null, 0f);
            // La etapa esta SIEMPRE en la cadena (no depende de ningun ajuste del local): el grave de noche funciona con todo de fabrica.
            MethodInfo mArma = TF.GetMethod("ArmaCadena", T);
            Check(mArma != null && Codigo.Llama(Codigo.Llamadas(mArma), "SalaLocal..ctor"), "SalaLocal se arma SIEMPRE en la cadena (no depende de ningun ajuste del local)");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN (" + oks + " comprobaciones)" : "  FALLOS: " + fallos);
        return fallos;
    }
}
