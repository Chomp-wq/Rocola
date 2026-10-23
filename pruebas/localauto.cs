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
// No es escucharlo en el bar: eso solo se sabe alli.
class LocalAutoPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TA;
    static object Campo(object o, string n) { return (o == null ? TA : o.GetType()).GetField(n, T).GetValue(o); }
    static void PonStatic(string n, object v) { TF.GetField(n, T).SetValue(null, v); }
    static object Auto(int tipo, float rango) { return TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(int), typeof(float) }, null).Invoke(null, new object[] { tipo, rango }); }
    // Con el rango de sonoridad (LRA, en LU) de la cancion; 'rango' es el P99 - sonoridad de la regla de antes.
    static object AutoLra(int tipo, float rango, float lra) { return TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(int), typeof(float), typeof(float) }, null).Invoke(null, new object[] { tipo, rango, lra }); }
    static bool B(object a, string n) { return (bool)a.GetType().GetField(n).GetValue(a); }
    static int I(object a, string n) { return (int)a.GetType().GetField(n).GetValue(a); }
    static string Texto(string rel) { string p = Path.Combine("..", "Rocola", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        TA = TF.GetNestedType("AjusteLocal", T);
        Check(TA != null && TF.GetMethod("LocalAutomatico", T, null, new Type[] { typeof(int), typeof(float) }, null) != null, "esta el modo automatico del local");
        if (TA == null) return 1;
        float alto = (float)TF.GetField("RangoAltoDb", T).GetValue(null), muy = (float)TF.GetField("RangoMuyAltoDb", T).GetValue(null);

        Console.WriteLine("\n  1) LA TABLA POR TIPO DE LOCAL Y RANGO\n");
        object b0 = Auto(0, 8f), b1 = Auto(0, alto + 1f), b2 = Auto(0, muy + 1f), bx = Auto(0, -1f);
        Check(B(b0, "Subgrave") && B(b0, "Estrecho") && B(b0, "EstrechoSoloSiHaceFalta") && B(b0, "Claridad"), "billar: sin subgrave, estereo estrecho solo si hace falta y claridad, en cualquier cancion");
        // P1 (24-10-2026): las partes suaves van SIEMPRE en el billar y en las bocinas chicas (la etapa se frena sola por nivel).
        Check(B(b0, "Suaves") && B(b1, "Suaves") && B(b2, "Suaves") && B(bx, "Suaves"), "P1 billar: las partes suaves siempre, con poco rango, con mucho, con muchisimo y sin medir");
        Check(!B(b0, "Presencia") && !B(b1, "Presencia"), "billar, sin rango de sonoridad y de poco rango: sin presencia");
        Check(B(b2, "Presencia"), "billar, de muchisimo rango (regla de antes, P99 - sonoridad): presencia");
        Check(B(bx, "Subgrave") && !B(bx, "Presencia"), "billar, sin medir: lo fijo del tipo y las partes suaves, sin presencia");
        object s0 = Auto(1, alto + 1f), s1 = Auto(1, muy + 1f);
        Check(!B(s0, "Subgrave") && !B(s0, "Estrecho") && !B(s0, "Claridad") && !B(s0, "Suaves") && !B(s0, "Presencia") && I(s0, "Graves") == 0 && I(s0, "Agudos") == 0, "sala buena: el tono no se toca (ni a una de mucho rango)");
        Check(B(s1, "Suaves") && !B(s1, "Presencia"), "sala buena, de muchisimo rango (regla de antes): solo las partes suaves");
        object c0 = Auto(2, 8f), c1 = Auto(2, alto + 1f);
        Check(B(c0, "Subgrave") && B(c0, "Claridad") && I(c0, "Agudos") == 1 && !B(c0, "Estrecho") && !B(c0, "Presencia"), "bocinas chicas: sin subgrave, claridad y +1 dB de agudos; sin estrechar el estereo");
        Check(B(c1, "Suaves") && B(c0, "Suaves") && B(Auto(2, -1f), "Suaves"), "P1 bocinas chicas: las partes suaves siempre (tambien las de poco rango y las sin medir)");
        Check(!B(Auto(0, muy - 0.01f), "Presencia") && B(Auto(0, muy), "Presencia"), "el corte de la regla de antes (" + muy + " dB) para la presencia vale justo en su valor");
        Check(B(Auto(0, float.NaN), "Suaves") && !B(Auto(0, float.NaN), "Presencia") && B(Auto(99, 5f), "Subgrave") == B(Auto(2, 5f), "Subgrave") && B(Auto(-4, 5f), "Estrecho"), "un tipo o un rango fuera de lo posible no truena (se queda en el mas cercano)");
        // P2 (24-10-2026): la presencia del billar y las partes suaves de la sala, por el rango de sonoridad (LRA).
        float lraP = (float)TF.GetField("LraPresenciaLu", T).GetValue(null), lraS = (float)TF.GetField("LraSalaLu", T).GetValue(null);
        Check(lraP == 10f && lraS == 14f, "los cortes del rango de sonoridad: " + lraP + " LU la presencia del billar, " + lraS + " LU las partes suaves de la sala");
        Check(B(AutoLra(0, 3f, lraP), "Presencia") && !B(AutoLra(0, 3f, lraP - 0.01f), "Presencia"), "P2 billar: la presencia desde 10 LU de rango de sonoridad, justo en su valor");
        Check(B(AutoLra(0, 3f, 18f), "Presencia") && !B(AutoLra(0, 20f, 0.2f), "Presencia"), "billar: manda el LRA, no los picos (un rock de picos altos y volumen parejo no la lleva; una balada con picos bajos, si)");
        Check(B(AutoLra(1, 3f, lraS), "Suaves") && !B(AutoLra(1, 20f, lraS - 0.01f), "Suaves") && !B(AutoLra(1, 3f, 12f), "Suaves"), "P2 sala buena: las partes suaves desde 14 LU, y no por los picos");
        Check(!B(AutoLra(2, 3f, 30f), "Presencia") && !B(AutoLra(1, 3f, 30f), "Presencia"), "las bocinas chicas y la sala buena no llevan presencia, tenga el rango que tenga");
        Check(B(AutoLra(0, -1f, 12f), "Presencia") && !B(AutoLra(0, -1f, -1f), "Presencia"), "con LRA y sin medida de picos decide el LRA; sin ninguno de los dos, nada");
        Check(AutoLra(0, 20f, -1f).Equals(Auto(0, 20f)) && AutoLra(1, 17f, -1f).Equals(Auto(1, 17f)), "una medida de antes (sin LRA) sigue por la regla de antes, igual que la forma de dos argumentos");
        Check(!B(AutoLra(0, 3f, float.NaN), "Presencia") && B(AutoLra(0, 17f, float.NaN), "Presencia"), "un LRA que no es un numero cuenta como que no hay");
        Check(Auto(0, 20f).Equals(Auto(0, 20f)), "la misma entrada da lo mismo (no tiene estado)");

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
        Check(!(bool)TF.GetField("localAuto", T).GetValue(null) && (int)TF.GetField("tipoLocal", T).GetValue(null) == 0, "de fabrica: apagado y el tipo del billar");
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
        PonStatic("localAuto", true); PonStatic("tipoLocal", 0);
        object a1 = localDe.Invoke(f, new object[] { cDin }), a2 = localDe.Invoke(f, new object[] { cComp }), a3 = localDe.Invoke(f, new object[] { cVieja });
        Check(B(a1, "Suaves") && B(a1, "Presencia") && B(a1, "Claridad") && B(a1, "Subgrave"), "billar, la dinamica: claridad, sin subgrave, partes suaves y presencia");
        Check(B(a2, "Suaves") && !B(a2, "Presencia") && B(a2, "Claridad"), "billar, la comprimida: lo fijo del tipo y las partes suaves, sin presencia");
        Check(B(a3, "Suaves") && !B(a3, "Presencia"), "billar, la de medida vieja: lo fijo del tipo y las partes suaves, sin presencia");
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
        Check(B(ab, "Presencia") && B(ab, "Suaves") && !B(ar, "Presencia") && B(ar, "Suaves"), "billar: la balada (8 dB de picos, 18 LU) lleva presencia; el rock (16 dB de picos, 0,2 LU) no, y los dos las partes suaves");
        Check(!B(ac, "Presencia") && B(ac, "Suaves"), "billar, la corta (sin LRA, 1 dB de picos): por la regla de antes, sin presencia");
        PonStatic("tipoLocal", 1);
        Check(B(localDe.Invoke(f, new object[] { cBal }), "Suaves") && !B(localDe.Invoke(f, new object[] { cRock }), "Suaves"), "sala buena: la balada lleva las partes suaves (18 LU) y el rock no (0,2 LU, aunque tenga 16 dB de picos)");
        PonStatic("tipoLocal", 0);
        Check(B(a1, "Estrecho") && B(a1, "EstrechoSoloSiHaceFalta") && !B(m0, "Estrecho"), "el estereo estrecho entra solo donde hace falta (S3), aunque el mando este apagado");
        Check(I(a1, "Graves") == 0 && I(a1, "Agudos") == 0, "en el billar los graves y agudos no se tocan, aunque el mando tenga 3 y -2");
        PonStatic("tipoLocal", 2);
        Check(I(localDe.Invoke(f, new object[] { cComp }), "Agudos") == 1, "al cambiar el tipo cambia lo decidido (bocinas chicas: +1 dB de agudos)");
        PonStatic("localAuto", false);
        object m1 = localDe.Invoke(f, new object[] { cDin });
        Check(B(m1, "Subgrave") && I(m1, "Graves") == 3 && I(m1, "Agudos") == -2 && B(m1, "Presencia"), "al apagarlo vuelven los mandos que habia guardados (no se perdio nada)");
        PonStatic("tipoLocal", 0);

        Console.WriteLine("\n  4) LA CADENA Y LOS MANDOS EN VIVO LEEN LocalDe\n");
        string son = Texto("Form1.Sonido.cs"), gm = Texto("GraveMono.cs"), qs = Texto("LoQueSuena.cs");
        Check(son.Contains("AjusteLocal loc = LocalDe(t);") && son.Contains("new SalaLocal(c.Mono, tap, normTarget, loc.Claridad, loc.Suaves, balanceDb, loc.Graves, loc.Agudos)")
              && son.Contains("new PresenciaSala(c.Sala, tap, normTarget, loc.Presencia)") && son.Contains("loc.Estrecho, loc.Subgrave"), "al armar la cancion se usa lo que decide LocalDe, no los mandos sueltos");
        Check(gm.Contains("LocalDe(c), lp = LocalDe(p)") && gm.Contains("c.Sala.Ajusta(lc.Claridad, lc.Suaves, balanceDb)") && gm.Contains("p.Presencia.Ajusta(lp.Presencia)"), "AplicaBocinas pone en vivo lo decidido para la que suena y para la preparada");
        Check(qs.Contains("LocalDe(c)") && qs.Contains("sin ? false : lo.Claridad") && qs.Contains("sin ? 0 : lo.Graves"), "«Escuchar sin mejoras» sigue quitando todo, tambien lo del automatico");
        Check(!son.Contains("claridadLocal, partesSuaves") && !gm.Contains("claridadLocal, partesSuaves"), "ya no queda ninguna etapa armada con los mandos sueltos");

        Console.WriteLine("\n  5) CONFIGURACION Y VENTANA\n");
        string dat = Texto("Form1.Datos.cs"), aj = Texto("Form1.Ajustes.cs");
        Check(dat.Contains("StartsWith(\"local_auto=\")") && dat.Contains("sb.Append(\"local_auto=\")") && dat.Contains("StartsWith(\"tipo_local=\")") && dat.Contains("sb.Append(\"tipo_local=\")"), "local_auto= y tipo_local= se leen y se guardan");
        MethodInfo tipoDe = TF.GetMethod("TipoLocalDe", T);
        Check((int)tipoDe.Invoke(null, new object[] { "2", 0 }) == 2 && (int)tipoDe.Invoke(null, new object[] { "9", 0 }) == 2 && (int)tipoDe.Invoke(null, new object[] { "-1", 1 }) == 0 && (int)tipoDe.Invoke(null, new object[] { "x", 1 }) == 1 && (int)tipoDe.Invoke(null, new object[] { null, 1 }) == 1, "el tipo guardado se acota a 0..2 y lo ilegible deja el que habia");
        Check(aj.Contains("SonidoLocalAuto") && aj.Contains("SonidoLocalTipo") && aj.Contains("bkLoA = localAuto") && aj.Contains("localAuto = bkLoA; tipoLocal = bkTipo;") && aj.Contains("swAutoL.Checked = false; tipoLocal = 0;"), "la ventana: interruptor, tipo, Cancelar lo devuelve y «Dejar como de fabrica» lo apaga");
        Check(aj.Contains("Necesita(swSub, sinAuto") && aj.Contains("Necesita(swPre, sinAuto") && aj.Contains("Necesita(barGra, sinAuto") && aj.Contains("Necesita(barAgu, sinAuto") && aj.Contains("Necesita(swCla, sinAuto") && aj.Contains("Necesita(swSua, sinAuto") && aj.Contains("Necesita(swEst, sinAuto"), "los siete mandos que decide el automatico se ponen en gris diciendo por que");
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
