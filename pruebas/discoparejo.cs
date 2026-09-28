using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
// NIVEL POR DISCO (NivelDisco.cs).
//
// Cuando suenan seguidas pistas del mismo disco, se nivelan con la misma ganancia (la del disco entero): el disco llega
// al volumen de siempre y por dentro conserva sus diferencias. Con la rocola de verdad:
//   - en racha (la de antes o la de despues es del mismo disco), la ganancia es la del disco y es la misma para todas;
//   - sin racha, la de siempre, cancion por cancion; un video o un disco de otra carpeta no hacen racha;
//   - el disco se reconoce por su carpeta, tambien si se pidio desde "lo mas sonado";
//   - la ganancia del disco respeta los picos de su pista mas fuerte;
//   - sin bastantes medidas, cancion por cancion;
//   - enchufado en la cadena, en la siguiente preparada y en el ajuste en vivo; y se apunta lo que sono antes.
class DiscoParejo
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TS, TTrack, TAlbum, TItem;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string n) { return o.GetType().GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { return TF.GetMethod(n, T).Invoke(o, a); }
    static double Db(double x) { return 20 * Math.Log10(x); }
    static float Lin(double db) { return (float)Math.Pow(10, db / 20); }

    static object Integ(float lk, float pico, float p99, int bloques) { return Activator.CreateInstance(TS, T, null, new object[] { 2, lk, 2f, bloques, pico, p99 }, null); }
    static object Pista(string ruta) { object t = Activator.CreateInstance(TTrack, true); Set(t, "Path", ruta); return t; }
    static object Disco(string carpeta, bool top, params object[] pistas)
    {
        object a = Activator.CreateInstance(TAlbum, true); Set(a, "Folder", carpeta); Set(a, "IsTop", top);
        IList l = (IList)Get(a, "Tracks"); foreach (object p in pistas) l.Add(p);
        return a;
    }
    static object Item(object pista, object disco) { object it = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TItem); Set(it, "Song", pista); Set(it, "Disc", disco); return it; }

    static object f; static IDictionary tab;
    static object Form()
    {
        object g = Codigo.FormSinEncender(TF);
        Set(g, "normalizeOn", true); Set(g, "normTarget", 0.26f); Set(g, "normStrength", 1f);
        tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase);
        Set(g, "integCache", tab);
        return g;
    }
    static float G(object pista) { return (float)Call(f, "GananciaDe", pista); }
    static float N(object pista) { return (float)Call(f, "NormGainFor", (string)Get(pista, "Path")); }
    static void Suena(object antes, object ahora, params object[] cola)
    {
        Set(f, "sonoAntes", antes); Set(f, "nowPlaying", ahora);
        IList q = (IList)Get(f, "queue"); q.Clear(); foreach (object c in cola) q.Add(c);
    }

    static void EnRacha()
    {
        Console.WriteLine("\n  1) EN RACHA Y SIN RACHA\n");
        f = Form();
        // Un disco con una balada bajita (-20 LKFS) entre dos temas fuertes (-12).
        object a1 = Pista("/m/disco/01.mp3"), a2 = Pista("/m/disco/02 balada.mp3"), a3 = Pista("/m/disco/03.mp3");
        object otro = Pista("/m/otro/01.mp3");
        object D = Disco("/m/disco", false, a1, a2, a3), O = Disco("/m/otro", false, otro);
        tab["/m/disco/01.mp3"] = Integ(-12f, -1f, -4f, 600); tab["/m/disco/02 balada.mp3"] = Integ(-20f, -6f, -12f, 600);
        tab["/m/disco/03.mp3"] = Integ(-12f, -1f, -4f, 600); tab["/m/otro/01.mp3"] = Integ(-16f, -2f, -8f, 600);
        // Suena la 01 y en la cola la balada: las dos van con la ganancia del disco.
        Suena(Item(otro, O), Item(a1, D), Item(a2, D), Item(a3, D));
        float g1 = G(a1), g2 = G(a2);
        Info("cancion por cancion: la 01 " + F(Db(N(a1)), "+0.0;-0.0") + " dB, la balada " + F(Db(N(a2)), "+0.0;-0.0") + " dB (la balada acabaria " + F(8 - Db(N(a2)) + Db(N(a1)), "0.0") + " dB por debajo de la 01 en vez de 8)");
        Info("en racha de disco: las dos " + F(Db(g1), "+0.0;-0.0") + " dB");
        Check(g1 == g2 && Math.Abs(g1 - N(a1)) > 0.01f, "en racha, la que suena y la siguiente del mismo disco llevan la misma ganancia (la balada sigue 8 dB por debajo, como en el disco)");
        // Sin racha: la de antes y la de despues son de otros discos.
        Suena(Item(otro, O), Item(a2, D), Item(otro, O));
        Check(G(a2) == N(a2), "sin racha (la de antes y la de despues son de otros discos), la de siempre");
        // La siguiente preparada: racha con la que suena.
        Suena(null, Item(a1, D), Item(a3, D));
        Check(G(a3) == g1 && G(a1) == g1, "la siguiente preparada, del mismo disco que la que suena, va en racha (las dos con la del disco)");
        // Un video en medio no hace racha.
        object vid = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TItem); Set(vid, "IsVideo", true);
        Suena(vid, Item(a2, D), vid);
        Check(G(a2) == N(a2), "un video al lado no hace racha");
        // Pedida desde "lo mas sonado" (disco TOP): se reconoce por la carpeta y sus pistas se buscan en la biblioteca.
        object top = Disco(@"TOP", true, a2, otro);
        Suena(Item(a1, D), Item(a2, top));
        Check(G(a2) == N(a2), "pedida desde \"lo mas sonado\" sin el disco en la biblioteca, cancion por cancion (no hay de donde sacar el disco)");
        IList albs = (IList)Get(f, "albums"); albs.Clear(); albs.Add(O); albs.Add(D);
        Check(G(a2) == g1, "con el disco en la biblioteca, se reconoce por su carpeta y va en racha con la de antes");
    }

    static void Topes()
    {
        Console.WriteLine("\n  2) LOS PICOS Y LO QUE HACE FALTA\n");
        f = Form();
        float techo = (float)TF.GetField("TechoPicoReal", T).GetValue(null);
        // Un disco bajito con una pista de golpes muy fuertes: la ganancia del disco no la aplasta.
        object b1 = Pista("/m/b/01.mp3"), b2 = Pista("/m/b/02.mp3");
        object B = Disco("/m/b", false, b2, b1);   // la de los golpes primero: se toman los picos mas altos, no los de la ultima
        tab["/m/b/01.mp3"] = Integ(-24f, -12f, -18f, 600); tab["/m/b/02.mp3"] = Integ(-22f, -0.5f, -5f, 600);
        Suena(null, Item(b1, B), Item(b2, B));
        float g = G(b1);
        double grP99 = Db(Lin(-5) * g / techo), grPico = Db(Lin(-0.5) * g / techo);
        Check(grP99 <= 3.0001 && grPico <= 6.0001, "la ganancia del disco (" + F(Db(g), "+0.0") + " dB) respeta los picos de su pista mas fuerte: al limitador le quedan " + F(Math.Max(0, grP99), "0.0") + " dB en el P99 y " + F(Math.Max(0, grPico), "0.0") + " en el pico");
        // La media pesa lo que dura cada pista: una larga y bajita manda mas que una corta y fuerte.
        object e1 = Pista("/m/e/01.mp3"), e2 = Pista("/m/e/02.mp3");
        object E = Disco("/m/e", false, e1, e2);
        tab["/m/e/01.mp3"] = Integ(-12f, -10f, -14f, 100); tab["/m/e/02.mp3"] = Integ(-18f, -10f, -14f, 2000);
        Suena(null, Item(e1, E), Item(e2, E));
        double ge = Db(G(e1)), n1 = Db(N(e1)), n2 = Db(N(e2));
        Check(Math.Abs(ge - n2) < Math.Abs(ge - n1), "la media del disco pesa lo que dura cada pista (" + F(ge, "+0.0") + " dB, cerca de la larga " + F(n2, "+0.0") + " y lejos de la corta " + F(n1, "+0.0") + ")");
        // Sin bastantes medidas: cancion por cancion.
        object c1 = Pista("/m/c/01.mp3"), c2 = Pista("/m/c/02.mp3"), c3 = Pista("/m/c/03.mp3"), c4 = Pista("/m/c/04.mp3"), c5 = Pista("/m/c/05.mp3");
        object C = Disco("/m/c", false, c1, c2, c3, c4, c5);
        tab["/m/c/01.mp3"] = Integ(-12f, -1f, -4f, 600); tab["/m/c/02.mp3"] = Integ(-20f, -6f, -12f, 600);
        Suena(null, Item(c1, C), Item(c2, C));
        Check(G(c1) == N(c1), "con 2 de 5 pistas medidas (menos de la mitad), cancion por cancion");
        tab["/m/c/03.mp3"] = Integ(-14f, -2f, -6f, 600);
        Check(G(c1) != N(c1), "con 3 de 5, ya por disco");
        Suena(null, Item(c4, C), Item(c1, C));
        Check(G(c4) == N(c4), "si la propia cancion no esta medida, la de siempre");
        Set(f, "normalizeOn", false);
        Suena(null, Item(c1, C), Item(c2, C));
        Check(G(c1) == 1f, "con el nivelado apagado, nada");
    }

    static void Enchufado()
    {
        Console.WriteLine("\n  3) ENCHUFADO\n");
        bool cadena = Codigo.Llama(TF.GetMethod("ArmaCadena", T), "GananciaDe") && !Codigo.Llama(TF.GetMethod("ArmaCadena", T), "NormGainFor");
        bool prep = Codigo.Llama(TF.GetMethod("AplicaAPreparada", T), "GananciaDe");
        bool vivo = Codigo.Llama(TF.GetMethod("ApplyNormNow", T), "GananciaDe");
        Check(cadena && prep && vivo, "la cadena de cada cancion, la siguiente preparada y el ajuste en vivo usan la ganancia en su sitio de la cola");
        int l1, e1, l2, e2;
        Codigo.UsosDeCampo(TF.GetMethod("PlayNext", T), "sonoAntes", out l1, out e1);
        Codigo.UsosDeCampo(TF.GetMethod("PasaRelevo", T), "sonoAntes", out l2, out e2);
        Check(e1 > 0 && e2 > 0, "se apunta lo que sono antes al pasar a la siguiente (por el camino de siempre y por el relevo sin pausa)");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TS = asm.GetType("Rocola.SonoridadIntegrada", true);
        TTrack = TF.GetNestedType("Track", T); TAlbum = TF.GetNestedType("Album", T); TItem = TF.GetNestedType("PlayItem", T);
        if (TF.GetMethod("GananciaDe", T) == null) { Check(false, "la rocola no nivela por disco (falta NivelDisco.cs)"); return 1; }
        try { EnRacha(); } catch (Exception e) { Check(false, "en racha: " + e); }
        try { Topes(); } catch (Exception e) { Check(false, "topes: " + e); }
        try { Enchufado(); } catch (Exception e) { Check(false, "enchufado: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
