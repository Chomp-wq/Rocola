using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// EL LOCAL EN AUTOMATICO (Rocola/LocalAuto.cs, 23-10-2026). Un interruptor que enciende y ajusta las mejoras del local segun el
// tipo de local y el rango de cada cancion. Se comprueba:
//  1) La tabla: cada tipo de local, con una cancion de poco rango, de mucho, de muchisimo y sin medir.
//  2) Apagado (de fabrica): LocalDe devuelve los mandos tal cual (la cadena sale bit a bit como antes).
//  3) Encendido: los mandos guardados no se pierden, y se decide por la medida de CADA cancion (integCache real).
//  4) Que la cadena, los mandos en vivo y «Escuchar sin mejoras» leen LocalDe y no los mandos sueltos.
//  5) La configuracion (local_auto=, tipo_local=) y la ventana (interruptor, tipo, mandos en gris con su motivo).
// 18-11-2026 (el dueno, tras oir «Roundabout»: «el volumen deberia ser constante»): el modo billar YA NO pone la presencia, las partes suaves, el
// retumbe, el ataque ni los agudos asperos (nada que suba o baje el volumen o la envolvente en tiempo real): LocalAutomatico las da en false y LocalDe
// las toma de los interruptores del dueno (apagados de fabrica), con el modo encendido igual que apagado. Antes se comprobaba que el modo las ENCENDIA.
// No es escucharlo en el bar: eso solo se sabe alli.
class LocalAutoPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TA;
    static object Campo(object o, string n) { return (o == null ? TA : o.GetType()).GetField(n, T).GetValue(o); }
    static void PonStatic(string n, object v) { TF.GetField(n, T).SetValue(null, v); }
    // Desde el 25-10-2026 solo hay un local (el billar): LocalAutomatico(rango, lra, corte). 'rango' es el P99 - sonoridad de la regla
    // de antes; 'lra' el rango de sonoridad (LU); 'corte' el de la presencia (L5: el tercio que mas varia de la biblioteca).
    static object AutoC(float rango, float lra, float corte) { return TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(float), typeof(float), typeof(float) }, null).Invoke(null, new object[] { rango, lra, corte }); }
    static object Auto(int tipo, float rango) { return AutoC(rango, -1f, 10f); }
    static object AutoLra(int tipo, float rango, float lra) { return AutoC(rango, lra, 10f); }
    static float Umbral(int[] cuenta, int n) { return (float)TF.GetMethod("UmbralPresencia", T).Invoke(null, new object[] { cuenta, n }); }
    static int[] Hist(float[] v) { int[] c = new int[600]; foreach (float x in v) c[(int)TF.GetMethod("CasillaLra", T).Invoke(null, new object[] { x })]++; return c; }
    static bool B(object a, string n) { return (bool)a.GetType().GetField(n).GetValue(a); }
    static int I(object a, string n) { return (int)a.GetType().GetField(n).GetValue(a); }
    static string Texto(string rel) { string p = Path.Combine("..", "Rocola", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TA = TF.GetNestedType("AjusteLocal", T);
        Check(TA != null && TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(float), typeof(float), typeof(float) }, null) != null, "esta el modo automatico del local");
        if (TA == null) return 1;
        float alto = (float)TF.GetField("RangoAltoDb", T).GetValue(null), muy = (float)TF.GetField("RangoMuyAltoDb", T).GetValue(null);

        Console.WriteLine("\n  1) LO QUE PONE EN EL BILLAR (el unico local desde el 25-10-2026)\n");
        Check(TF.GetField("tipoLocal", T) == null && TF.GetField("NombresTipoLocal", T) == null && TF.GetMethod("TipoLocalDe", T) == null && TF.GetField("LraSalaLu", T) == null,
              "ya no hay tipos de local: ni el campo, ni los nombres, ni la lectura, ni el corte de la sala buena");
        object b0 = Auto(0, 8f), b1 = Auto(0, alto + 1f), b2 = Auto(0, muy + 1f), bx = Auto(0, -1f);
        Check(B(b0, "Subgrave") && B(b0, "Estrecho") && B(b0, "EstrechoSoloSiHaceFalta") && B(b0, "Claridad") && I(b0, "Graves") == 0 && I(b0, "Agudos") == 0,
              "billar: sin subgrave, estereo estrecho solo si hace falta y claridad, graves y agudos en 0, en cualquier cancion");
        Check(!B(b0, "Bombo") && !B(b0, "Retumbe") && !B(b0, "Ataque") && !B(b0, "Aspereza") && !B(bx, "Bombo") && !B(bx, "Retumbe") && !B(bx, "Ataque") && !B(bx, "Aspereza")
              && !B(b2, "Bombo") && !B(b2, "Retumbe") && !B(b2, "Ataque") && !B(b2, "Aspereza"),
              "18-11-2026: el modo billar ya no pone el retumbe, el ataque, los agudos asperos ni el bombo, en ninguna cancion (con mucho rango, poco o sin medir)");
        Check(B(b0, "Eses") && B(bx, "Eses") && B(b2, "Eses"), "cadena del billar: las eses, siempre");
        MethodInfo auto4 = TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null);
        Check(auto4 != null, "esta la variante con la falta de cuerpo");
        if (auto4 != null)
        {
            float flacaDb = (float)TF.GetField("CuerpoFlacaDb", T).GetValue(null);
            object fl = auto4.Invoke(null, new object[] { 8f, -1f, 10f, flacaDb + 0.5f }), nf = auto4.Invoke(null, new object[] { 8f, -1f, 10f, flacaDb - 0.5f }), sx = auto4.Invoke(null, new object[] { 8f, -1f, 10f, float.NaN });
            Check(!B(fl, "Claridad") && B(fl, "Cuerpo") && !B(fl, "Suaves"), "cancion flaca: sin la claridad (que corta a 300 Hz) y con cuerpo del EQ");
            Check(B(nf, "Claridad") && !B(nf, "Cuerpo") && B(sx, "Claridad") && !B(sx, "Cuerpo"), "con cuerpo de sobra, o sin saberlo, la claridad como siempre");
        }
        Check(!B(b0, "Suaves") && !B(b1, "Suaves") && !B(b2, "Suaves") && !B(bx, "Suaves"), "18-11-2026: las partes suaves ya no las pone el modo (ni con poco rango, ni con mucho, ni con muchisimo, ni sin medir)");
        Check(!B(b0, "Presencia") && !B(b1, "Presencia") && !B(b2, "Presencia") && !B(bx, "Presencia"), "18-11-2026: la presencia ya no la pone el modo, ni siquiera con " + muy + " dB de P99 - sonoridad");
        Check(!B(Auto(0, muy - 0.01f), "Presencia") && !B(Auto(0, muy), "Presencia"), "la regla de antes por picos ya no decide nada, ni en su valor justo");
        Check(!B(AutoC(3f, 12f, 12f), "Presencia") && !B(AutoC(3f, 11.99f, 12f), "Presencia") && !B(AutoC(3f, 7f, 6.5f), "Presencia"), "con LRA y corte, tampoco: el LRA ya no decide la presencia");
        Check(!B(AutoLra(0, 3f, 18f), "Presencia") && !B(AutoLra(0, 20f, 0.2f), "Presencia"), "ni la balada de mucho LRA ni el rock parejo la llevan por decision del modo");
        Check(!B(AutoC(3f, float.NaN, 10f), "Presencia") && !B(AutoC(17f, float.NaN, 10f), "Presencia") && !B(AutoC(3f, 12f, float.NaN), "Presencia") && !B(AutoC(3f, 9f, float.NaN), "Presencia"),
              "un LRA o un corte que no son un numero tampoco la encienden (y no truenan)");
        Check(AutoC(20f, 13f, 10f).Equals(AutoC(20f, 13f, 10f)), "la misma entrada da lo mismo (no tiene estado)");

        Console.WriteLine("\n  1b) L5: EL CORTE, CON TU BIBLIOTECA\n");
        Check(Umbral(new int[600], 0) == 10f && Umbral(Hist(new float[] { 1f, 2f, 30f }), 3) == 10f, "con menos de 200 canciones con LRA, el de siempre: 10 LU");
        float[] lib = new float[3000]; Random r = new Random(7);
        for (int i = 0; i < lib.Length; i++) lib[i] = (float)(r.NextDouble() * 12.0);   // de 0 a 12 LU, parejo
        float u = Umbral(Hist(lib), lib.Length); int encima = 0; foreach (float x in lib) if (x >= u) encima++;
        Check(Math.Abs(u - 8f) <= 0.15f && Math.Abs(encima - 1000) <= 40, "biblioteca pareja de 0 a 12 LU: corte en " + u + " LU, y la lleva el tercio que mas varia (" + encima + " de 3000)");
        float[] pop = new float[1000]; for (int i = 0; i < pop.Length; i++) pop[i] = (float)(2.0 + r.NextDouble() * 3.0);   // todo muy parejo
        float[] clas = new float[1000]; for (int i = 0; i < clas.Length; i++) clas[i] = (float)(15.0 + r.NextDouble() * 10.0);   // todo con mucho rango
        Check(Umbral(Hist(pop), pop.Length) == 6f && Umbral(Hist(clas), clas.Length) == 14f, "se acota: una biblioteca toda pareja no baja de 6 LU (no se la pone a todas) y una de clasica no pasa de 14");
        int[] raro = new int[600]; raro[599] = 500;
        Check(Umbral(raro, 500) == 14f && (int)TF.GetMethod("CasillaLra", T).Invoke(null, new object[] { 99f }) == 599 && (int)TF.GetMethod("CasillaLra", T).Invoke(null, new object[] { -3f }) == 0, "los LRA fuera de la tabla caen en sus bordes, sin tronar");

        Console.WriteLine("\n  2) APAGADO: LOS MANDOS TAL CUAL\n");
        object f = FormatterServices.GetUninitializedObject(TF);
        Type TTrack = TF.GetNestedType("Track", T);
        object cancion = FormatterServices.GetUninitializedObject(TTrack);
        TTrack.GetField("Path", T).SetValue(cancion, @"C:\musica\a\uno.mp3");
        MethodInfo localDe = null;
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "LocalDe" && m.GetParameters()[0].ParameterType == TTrack) localDe = m;
        Check(localDe != null, "LocalDe(cancion) existe");
        PonStatic("localAuto", false);
        PonStatic("cortaSubgrave", true); PonStatic("estereoEstrecho", false); PonStatic("estereoAuto", true);
        PonStatic("claridadLocal", false); PonStatic("partesSuaves", true); PonStatic("presenciaSala", true);
        PonStatic("gravesLocalDb", 3); PonStatic("agudosLocalDb", -2);
        object m0 = localDe.Invoke(f, new object[] { cancion });
        Check(B(m0, "Subgrave") && !B(m0, "Estrecho") && B(m0, "EstrechoSoloSiHaceFalta") && !B(m0, "Claridad") && B(m0, "Suaves") && B(m0, "Presencia") && I(m0, "Graves") == 3 && I(m0, "Agudos") == -2,
              "con el automatico apagado salen los siete mandos exactamente como estan, tengan lo que tengan");
        Check(!B(m0, "Bombo") && !B(m0, "Retumbe") && !B(m0, "Ataque"), "y con los tres interruptores de voz, golpe y retumbe apagados (de fabrica) el apagado los deja apagados");
        PonStatic("bomboSinHundir", true); PonStatic("retumbeDinamico", false); PonStatic("ataqueClaro", true);
        object mv = localDe.Invoke(f, new object[] { cancion });
        Check(B(mv, "Bombo") && !B(mv, "Retumbe") && B(mv, "Ataque"), "con el automatico apagado mandan sus tres interruptores, cada uno el suyo");
        PonStatic("bomboSinHundir", false); PonStatic("ataqueClaro", false);
        Check(!(bool)TF.GetField("localAuto", T).GetValue(null), "de fabrica: apagado");
        object mn = localDe.Invoke(f, new object[] { null });
        Check(B(mn, "Subgrave") && I(mn, "Graves") == 3, "sin cancion tambien sale lo de los mandos");

        Console.WriteLine("\n  3) ENCENDIDO: DECIDE POR CADA CANCION\n");
        Type TInt = asm.GetType("Rocola.SonoridadIntegrada", false) ?? TF.Assembly.GetType("Rocola.SonoridadIntegrada", false);
        if (TInt == null) foreach (Type t in asm.GetTypes()) if (t.Name == "SonoridadIntegrada") TInt = t;
        Check(TInt != null, "esta la medida integrada");
        IDictionary dic = (IDictionary)Activator.CreateInstance(typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(typeof(string), TInt), new object[] { StringComparer.OrdinalIgnoreCase });
        TF.GetField("integCache", T).SetValue(f, dic);
        // algoritmo, lkfs, pesos, bloques, pico, p99: el rango es p99 - lkfs
        object dinamica = Activator.CreateInstance(TInt, T, null, new object[] { 3, -24f, 2f, 1800, -3f, -6f }, null);   // rango 18 dB
        object comprimida = Activator.CreateInstance(TInt, T, null, new object[] { 3, -9f, 2f, 1800, -0.2f, -1f }, null);  // rango 8 dB
        object vieja = Activator.CreateInstance(TInt, T, null, new object[] { 1, -14f, 2f, 1800 }, null);                 // sin picos (algoritmo 1)
        dic[@"C:\musica\a\uno.mp3"] = dinamica; dic[@"C:\musica\a\dos.mp3"] = comprimida; dic[@"C:\musica\a\tres.mp3"] = vieja;
        object cDin = cancion, cComp = FormatterServices.GetUninitializedObject(TTrack), cVieja = FormatterServices.GetUninitializedObject(TTrack), cNueva = FormatterServices.GetUninitializedObject(TTrack);
        TTrack.GetField("Path", T).SetValue(cComp, @"C:\musica\a\dos.mp3"); TTrack.GetField("Path", T).SetValue(cVieja, @"C:\musica\a\tres.mp3"); TTrack.GetField("Path", T).SetValue(cNueva, @"C:\musica\a\nueva.mp3");
        MethodInfo rango = TF.GetMethod("RangoDeCancion", T);
        float rd = (float)rango.Invoke(f, new object[] { @"C:\musica\a\uno.mp3" }), rc = (float)rango.Invoke(f, new object[] { @"C:\musica\a\dos.mp3" });
        Check(Math.Abs(rd - 18f) < 0.01f && Math.Abs(rc - 8f) < 0.01f, "el rango sale del P99 menos la sonoridad integrada (18 y 8 dB; salio " + rd + " y " + rc + ")");
        Check((float)rango.Invoke(f, new object[] { @"C:\musica\a\tres.mp3" }) < 0f && (float)rango.Invoke(f, new object[] { @"C:\musica\a\nueva.mp3" }) < 0f && (float)rango.Invoke(f, new object[] { null }) < 0f, "una medida vieja (sin picos), una cancion sin medir o sin ruta: sin rango");
        PonStatic("localAuto", true);
        object a1 = localDe.Invoke(f, new object[] { cDin }), a2 = localDe.Invoke(f, new object[] { cComp }), a3 = localDe.Invoke(f, new object[] { cVieja });
        // Los interruptores del dueno estan a como los dejo la parte 2: partes suaves y presencia ENCENDIDOS; retumbe apagado, ataque apagado.
        Check(B(a1, "Suaves") && B(a1, "Presencia") && B(a1, "Claridad") && B(a1, "Subgrave"), "billar con partes suaves y presencia puestas A MANO: la dinamica las lleva (son los interruptores del dueno)");
        Check(B(a2, "Suaves") && B(a2, "Presencia") && B(a2, "Claridad"), "billar, la comprimida: igual (el modo ya no mira el rango)");
        Check(B(a3, "Suaves") && B(a3, "Presencia"), "billar, la de medida vieja: igual");
        PonStatic("partesSuaves", false); PonStatic("presenciaSala", false);
        object d1 = localDe.Invoke(f, new object[] { cDin }), d2 = localDe.Invoke(f, new object[] { cComp }), d3 = localDe.Invoke(f, new object[] { cNueva });
        Check(!B(d1, "Suaves") && !B(d1, "Presencia") && !B(d2, "Suaves") && !B(d2, "Presencia") && !B(d3, "Suaves") && !B(d3, "Presencia") && B(d1, "Claridad") && B(d1, "Subgrave"),
              "18-11-2026: con el modo billar y los interruptores de fabrica no hay partes suaves ni presencia en ninguna cancion (la de 18 dB de rango, la de 8 y la sin medir), y la claridad sigue");
        PonStatic("retumbeDinamico", true); PonStatic("ataqueClaro", true); PonStatic("asperezaDinamica", true);
        object d4 = localDe.Invoke(f, new object[] { cDin });
        Check(B(d4, "Retumbe") && B(d4, "Ataque") && B(d4, "Aspereza") && !B(d4, "Bombo"), "con el modo billar, si el dueno enciende retumbe, ataque y asperos a mano, LocalDe los da (el bombo, no)");
        PonStatic("retumbeDinamico", false); PonStatic("ataqueClaro", false); PonStatic("asperezaDinamica", false);
        object d5 = localDe.Invoke(f, new object[] { cDin });
        Check(!B(d5, "Retumbe") && !B(d5, "Ataque") && !B(d5, "Aspereza"), "y apagados vuelven a no ir");
        PonStatic("partesSuaves", true); PonStatic("presenciaSala", true);   // para lo que sigue: a mano, se ve que lo del dueno manda
        // P2: medidas del algoritmo 4, con rango de sonoridad: manda el LRA (una balada de picos bajos y mucho LRA; un rock de picos altos y volumen parejo).
        Type TSil = null; foreach (Type t in asm.GetTypes()) if (t.Name == "MapaSilencios") TSil = t;
        object mudo = TSil.GetMethod("Mudo", T).Invoke(null, new object[] { 0f });
        object balada = Activator.CreateInstance(TInt, T, null, new object[] { 4, -20f, 2f, 1800, -8f, -12f, mudo, 18.3f }, null);   // P99 - sonoridad = 8 dB, LRA 18
        object rock = Activator.CreateInstance(TInt, T, null, new object[] { 4, -17f, 2f, 1800, -0.5f, -0.7f, mudo, 0.2f }, null);    // 16,3 dB de picos, LRA 0,2
        object sinLraCorta = Activator.CreateInstance(TInt, T, null, new object[] { 4, -12f, 2f, 1800, -1f, -2f, mudo, float.NaN }, null);   // de menos de 3 s: no hay LRA
        dic[@"C:\musica\a\balada.mp3"] = balada; dic[@"C:\musica\a\rock.mp3"] = rock; dic[@"C:\musica\a\corta.mp3"] = sinLraCorta;
        object cBal = FormatterServices.GetUninitializedObject(TTrack), cRock = FormatterServices.GetUninitializedObject(TTrack), cCorta = FormatterServices.GetUninitializedObject(TTrack);
        TTrack.GetField("Path", T).SetValue(cBal, @"C:\musica\a\balada.mp3"); TTrack.GetField("Path", T).SetValue(cRock, @"C:\musica\a\rock.mp3"); TTrack.GetField("Path", T).SetValue(cCorta, @"C:\musica\a\corta.mp3");
        MethodInfo lraDe = TF.GetMethod("LraDeCancion", T);
        float lb = (float)lraDe.Invoke(f, new object[] { @"C:\musica\a\balada.mp3" }), lr = (float)lraDe.Invoke(f, new object[] { @"C:\musica\a\rock.mp3" });
        Check(Math.Abs(lb - 18.3f) < 0.001f && Math.Abs(lr - 0.2f) < 0.001f && (float)lraDe.Invoke(f, new object[] { @"C:\musica\a\tres.mp3" }) < 0f && (float)lraDe.Invoke(f, new object[] { @"C:\musica\a\corta.mp3" }) < 0f
              && (float)lraDe.Invoke(f, new object[] { @"C:\musica\a\nueva.mp3" }) < 0f && (float)lraDe.Invoke(f, new object[] { null }) < 0f, "el LRA de la cancion sale de la medida (18,3 y 0,2); la vieja, la corta, la sin medir y la nula, -1");
        object ab = localDe.Invoke(f, new object[] { cBal }), ar = localDe.Invoke(f, new object[] { cRock }), ac = localDe.Invoke(f, new object[] { cCorta });
        Check(B(ab, "Presencia") && B(ab, "Suaves") && B(ar, "Presencia") && B(ar, "Suaves"), "con los interruptores a mano, la balada (18 LU) y el rock (0,2 LU) las llevan igual: el LRA ya no decide");
        Check(B(ac, "Presencia") && B(ac, "Suaves"), "billar, la corta (sin LRA): igual");
        Check(B(a1, "Estrecho") && B(a1, "EstrechoSoloSiHaceFalta") && !B(m0, "Estrecho"), "el estereo estrecho entra solo donde hace falta (S3), aunque el mando este apagado");
        Check(I(a1, "Graves") == 0 && I(a1, "Agudos") == 0, "en el billar los graves y agudos no se tocan, aunque el mando tenga 3 y -2");
        // L5 con la tabla de verdad. Con 3 canciones con LRA vale el corte de siempre (10 LU): la de 9 LU no lleva presencia. Con 300 mas,
        // de 0 a 12 LU, el corte baja a ~8 (el tercio que mas varia de ESTA biblioteca) y la de 9 LU ya la lleva; el rock (0,2), no.
        object cNueve = FormatterServices.GetUninitializedObject(TTrack); TTrack.GetField("Path", T).SetValue(cNueve, @"C:\musica\a\nueve.mp3");
        dic[@"C:\musica\a\nueve.mp3"] = Activator.CreateInstance(TInt, T, null, new object[] { 4, -18f, 2f, 1800, -6f, -9f, mudo, 9f }, null);
        PonStatic("presenciaSala", false);
        Check(!B(localDe.Invoke(f, new object[] { cNueve }), "Presencia"), "con el interruptor de la presencia apagado, la de 9 LU no la lleva (el corte ya no decide)");
        for (int i = 0; i < 300; i++) dic[@"C:\musica\z\" + i + ".mp3"] = Activator.CreateInstance(TInt, T, null, new object[] { 4, -14f, 2f, 1800, -2f, -4f, mudo, i * 12f / 300f }, null);
        TF.GetField("integCambios", T).SetValue(f, 1L + (long)TF.GetField("integCambios", T).GetValue(f));
        MethodInfo umbralAhora = TF.GetMethod("UmbralPresenciaAhora", T);
        object[] arg = new object[] { 0 }; float sinRehacer = (float)umbralAhora.Invoke(f, arg);
        Check(sinRehacer == 10f, "con medidas nuevas no se rehace antes de 10 minutos (sigue en " + sinRehacer + " LU)");
        TF.GetField("umbralCuando", T).SetValue(f, Environment.TickCount - 700000);   // ya pasaron los 10 minutos
        float corteLib = (float)umbralAhora.Invoke(f, arg);
        Check(Math.Abs(corteLib - 8f) <= 0.3f && (int)arg[0] == 303, "L5: pasados los 10 minutos, el corte sale de la biblioteca (" + corteLib + " LU, con " + arg[0] + " medidas)");
        Check(!B(localDe.Invoke(f, new object[] { cNueve }), "Presencia") && !B(localDe.Invoke(f, new object[] { cRock }), "Presencia"), "y aun con el corte nuevo, la de 9 LU y el rock siguen sin presencia: el corte solo se calcula (informe de rangos)");

        // L1: lo decidido para una cadena se fija al armarla, y en un disco continuo la siguiente lo hereda.
        Type TCad = TF.GetNestedType("CadenaCancion", T);
        MethodInfo localDeC = null, hereda = TF.GetMethod("HeredaLocal", T);
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "LocalDe" && m.GetParameters()[0].ParameterType == TCad) localDeC = m;
        object cadA = FormatterServices.GetUninitializedObject(TCad); TCad.GetField("Cancion", T).SetValue(cadA, cNueve);
        object cadB = FormatterServices.GetUninitializedObject(TCad); TCad.GetField("Cancion", T).SetValue(cadB, cRock);
        PonStatic("presenciaSala", true);
        Check(B(localDeC.Invoke(f, new object[] { cadA }), "Presencia") && (bool)TCad.GetField("AutoFijo", T).GetValue(cadA), "la cadena de la de 9 LU con el interruptor encendido: con presencia, y queda apuntado al armarla");
        TF.GetField("umbralLra", T).SetValue(f, 12f);   // el corte cambia a media cancion (llegaron medidas nuevas): no importa, ya no decide
        Check(B(localDeC.Invoke(f, new object[] { cadA }), "Presencia"), "el corte ya no mueve nada a media cancion");
        PonStatic("presenciaSala", false);
        Check(!B(localDeC.Invoke(f, new object[] { cadA }), "Presencia"), "18-11-2026: un cambio del interruptor se toma al instante (no queda fijo como con la regla por cancion)");
        PonStatic("presenciaSala", true);
        hereda.Invoke(f, new object[] { cadB, cadA });
        Check(B(localDeC.Invoke(f, new object[] { cadB }), "Presencia"), "disco continuo: el rock que entra sin pausa lleva lo del interruptor, como la anterior (sin escalon en el cambio)");
        object cadC = FormatterServices.GetUninitializedObject(TCad); TCad.GetField("Cancion", T).SetValue(cadC, cRock);
        PonStatic("presenciaSala", false);
        Check(!B(localDeC.Invoke(f, new object[] { cadC }), "Presencia"), "con el interruptor apagado, sin presencia en ninguna cadena");
        TF.GetField("umbralLra", T).SetValue(f, 8f);
        PonStatic("presenciaSala", true);   // como estaba al empezar esta parte
        PonStatic("localAuto", false);
        object m1 = localDe.Invoke(f, new object[] { cDin });
        Check(B(m1, "Subgrave") && I(m1, "Graves") == 3 && I(m1, "Agudos") == -2 && B(m1, "Presencia"), "al apagarlo vuelven los mandos que habia guardados (no se perdio nada)");

        Console.WriteLine("\n  4) LA CADENA Y LOS MANDOS EN VIVO LEEN LocalDe\n");
        string son = Texto("Form1.Sonido.cs"), gm = Texto("GraveMono.cs"), qs = Texto("LoQueSuena.cs");
        Check(son.Contains("AjusteLocal loc = LocalDe(c);") && son.Contains("new SalaLocal(c.Mono, tap, nivelLocal, loc.Claridad, loc.Suaves, balanceDb, loc.Graves, loc.Agudos)")
              && son.Contains("new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto)") && son.Contains("new SuavizaEses(c.Aspereza, loc.Eses, localAuto)")
              && son.Contains("loc.Estrecho, loc.Subgrave"), "al armar la cancion se usa lo que decide LocalDe, no los mandos sueltos");
        Check(gm.Contains("LocalDe(c), lp = LocalDe(p)") && gm.Contains("c.Sala.Ajusta(lc.Claridad, lc.Suaves, balanceDb)") && gm.Contains("p.Presencia.Ajusta(lp.Presencia)"), "AplicaBocinas pone en vivo lo decidido para la que suena y para la preparada");
        Check(qs.Contains("LocalDe(c)") && qs.Contains("sin ? false : lo.Claridad") && qs.Contains("sin ? 0 : lo.Graves"), "«Escuchar sin mejoras» sigue quitando todo, tambien lo del automatico");
        Check(!son.Contains("claridadLocal, partesSuaves") && !gm.Contains("claridadLocal, partesSuaves"), "ya no queda ninguna etapa armada con los mandos sueltos");

        Console.WriteLine("\n  5) CONFIGURACION Y VENTANA\n");
        string dat = Texto("Form1.Datos.cs"), aj = Texto("Form1.Ajustes.cs");
        Check(dat.Contains("StartsWith(\"local_auto=\")") && dat.Contains("sb.Append(\"local_auto=\")") && dat.Contains("StartsWith(\"tipo_local=\")) { }") && !dat.Contains("sb.Append(\"tipo_local=\")"), "local_auto= se lee y se guarda; tipo_local= se ignora y ya no se escribe");
        Check(aj.Contains("SonidoLocalAuto") && aj.Contains("SonidoLocalHecho") && !aj.Contains("SonidoLocalTipo") && aj.Contains("bkLoA = localAuto") && aj.Contains("localAuto = bkLoA;") && aj.Contains("swAutoL.Checked = false;"), "la ventana: interruptor, para que local esta hecho (sin boton de tipo), Cancelar lo devuelve y «Dejar como de fabrica» lo apaga");
        string sp = Texto("Form1.SinPausa.cs");
        Check(sp.Contains("SigueA(c, cadenaSonando); HeredaLocal(c, cadenaSonando);"), "L1: el relevo sin pausa hereda lo decidido justo donde la siguiente sigue con los filtros de la anterior");
        Check(aj.Contains("Necesita(swSub, sinAuto") && aj.Contains("Necesita(barGra, sinAuto") && aj.Contains("Necesita(barAgu, sinAuto") && aj.Contains("Necesita(swCla, sinAuto") && aj.Contains("Necesita(swEst, sinAuto") && aj.Contains("Necesita(swEses, sinAuto"),
              "los mandos que decide el modo (subgrave, estereo, claridad, graves, agudos, eses) se ponen en gris diciendo por que");
        Check(!aj.Contains("Necesita(swPre, sinAuto") && !aj.Contains("Necesita(swSua, sinAuto") && !aj.Contains("Necesita(swRet, sinAuto") && !aj.Contains("Necesita(swAta, sinAuto") && !aj.Contains("Necesita(swAsp, sinAuto"),
              "18-11-2026: la presencia, las partes suaves, el retumbe, el ataque y los asperos ya NO van en gris por el modo billar");
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
