using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

// LA CADENA DEL MODO BILLAR (Rocola/CadenaBillar.cs, 27-10-2026). El modo billar (local_auto=) ya no solo enciende piezas: ajusta la
// cadena entera para las bocinas del billar. Se comprueba:
//  1) El EQ automatico del billar (CorreccionBillar, BandaBillar): 50 Hz y 5,6 kHz solo bajan, 15,8 kHz no se toca, la banda por encima
//     del corte del codec (con margen) y la de 50 Hz de una grabacion cortada en grave no se tocan, zona muerta, sin refuerzos por encima
//     de 2 kHz en una cancion recortada, y mas cuerpo a una cancion flaca.
//  2) EqCorrFor en el billar con una huella de verdad: no sube los agudos de una cancion completa, no ecualiza una de menos de 45 s, y con
//     el modo apagado da lo mismo de siempre.
//  3) Realce, DSEE, siseo, a la medida, arreglos y bajo: lo que el modo decide, y que apagado manda el interruptor.
//  4) El codigo: la cadena usa lo efectivo y la ventana lo pone en gris.
// Son cuentas, no oido: nada de esto se ha oido en el billar.
class CadenaBillarPrueba
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static void PonStatic(string n, object v) { TF.GetField(n, T).SetValue(null, v); }
    static void Pon(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Lee(string n) { FieldInfo f = TF.GetField(n, T); return f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null); }
    static float Corr(int b, float pide, float fuerza, bool rec, bool flaca)
    {
        return (float)TF.GetMethod("CorreccionBillar", T, null, new Type[] { typeof(int), typeof(float), typeof(float), typeof(bool), typeof(bool) }, null)
                         .Invoke(null, new object[] { b, pide, fuerza, rec, flaca });
    }
    static string Texto(string rel) { string p = Path.Combine("..", "Rocola", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);

        Console.WriteLine("\n  1) EL EQ DEL BILLAR, BANDA POR BANDA\n");
        Check(Corr(0, 5f, 1f, false, false) == 0f && Corr(0, -5f, 1f, false, false) < -2f, "50 Hz: solo baja (ninguna bocina del billar da casi nada ahi)");
        Check(Corr(5, 5f, 1f, false, false) == 0f && Corr(5, -5f, 1f, false, false) < -3f, "5,6 kHz: solo baja (la franja aspera que la loseta devuelve)");
        Check(Corr(7, 5f, 1f, false, false) == 0f && Corr(7, -5f, 1f, false, false) == 0f, "15,8 kHz: no se toca");
        Check(Math.Abs(Corr(2, -5f, 1f, false, false) + 2.1f) < 1e-4f, "400 Hz baja como mucho 2,5 dB (con la claridad no se queda sin cuerpo), menos la zona muerta");
        Check(Corr(3, 0.3f, 1f, false, false) == 0f && Corr(3, -0.3f, 1f, false, false) == 0f, "lo que corregiria menos de 0,4 dB no se toca");
        Check(Corr(4, 3f, 1f, true, false) == 0f && Corr(6, 3f, 1f, true, false) == 0f && Corr(3, 3f, 1f, true, false) > 0f, "cancion recortada: sin refuerzos por encima de 2 kHz (debajo, como siempre)");
        Check(Corr(1, 5f, 1f, false, true) > Corr(1, 5f, 1f, false, false) + 1f && Corr(2, 5f, 1f, false, true) > Corr(2, 5f, 1f, false, false) + 1f, "cancion flaca: 158 y 400 Hz pueden subir mas (mas cuerpo en voces)");
        Check(Math.Abs(Corr(4, -5f, 0.5f, false, false) - (-1.6f)) < 1e-4f, "la fuerza del EQ escala la correccion antes de la zona muerta");
        MethodInfo banda = TF.GetMethod("BandaBillar", T);
        float[] h = new float[12]; h[10] = 30f;
        Check(!(bool)banda.Invoke(null, new object[] { 7, h, 20000f }), "banda de 15,8 kHz: fuera");
        Check(!(bool)banda.Invoke(null, new object[] { 6, h, 12000f }) && (bool)banda.Invoke(null, new object[] { 6, h, 13000f }), "una banda solo se toca si su centro queda 1,25 veces por debajo del corte del codec");
        h[10] = 90f;
        Check(!(bool)banda.Invoke(null, new object[] { 0, h, 20000f }) && (bool)banda.Invoke(null, new object[] { 1, h, 20000f }), "grabacion cortada en grave (> 60 Hz): la banda de 50 Hz no se toca");
        MethodInfo realce = TF.GetMethod("RealceBillar", T);
        float r0 = (float)realce.Invoke(null, new object[] { 0.75f }), r1 = (float)realce.Invoke(null, new object[] { 1.45f }), rm = (float)realce.Invoke(null, new object[] { 1.1f });
        Check(r0 == 0f && r1 == 1f && rm > 0.4f && rm < 0.6f, "realce en el billar: nada a una cancion completa, entero a una cortada en 15 kHz, continuo entre medias");

        Console.WriteLine("\n  2) EqCorrFor CON UNA HUELLA\n");
        object f = FormatterServices.GetUninitializedObject(TF);
        Pon(f, "eqCache", new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase));
        Pon(f, "durCache", new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase));
        Pon(f, "durFast", new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase));
        float[] avg = { -6f, -3f, -2f, -4f, -8f, -12f, -16f, -22f };
        Pon(f, "eqAvg", avg); Pon(f, "eqAvgSanas", 100); Pon(f, "eqAuto", true); Pon(f, "eqAmount", 0f);
        Dictionary<string, float[]> cache = (Dictionary<string, float[]>)TF.GetField("eqCache", T).GetValue(f);
        Dictionary<string, float> dur = (Dictionary<string, float>)TF.GetField("durCache", T).GetValue(f);
        float ver = (float)Lee("EqVersionOriginal");
        // Una cancion completa (corte 20 kHz) con mucho grave profundo de menos y los agudos un poco bajos: el EQ de siempre la empujaria
        // arriba en 50 Hz y en los agudos.
        float[] c = { -10f, -3.5f, -2f, -4f, -9f, -13.5f, -17.5f, -23f, 20000f, 0f, 30f, ver };
        cache["cancion.mp3"] = c; dur["cancion.mp3"] = 200f;
        MethodInfo eq = TF.GetMethod("EqCorrFor", T);
        PonStatic("localAuto", false);
        float[] siempre = (float[])eq.Invoke(f, new object[] { "cancion.mp3" });
        PonStatic("localAuto", true);
        float[] billar = (float[])eq.Invoke(f, new object[] { "cancion.mp3" });
        Check(siempre != null && siempre[0] > 1f && siempre[5] > 0.5f, "apagado: el EQ de siempre sube el grave profundo y los agudos (lo de antes, sin cambios)");
        Check(billar == null || (billar[0] <= 0f && billar[5] <= 0f && billar[7] == 0f), "billar: ni 50 Hz ni 5,6 kHz suben, 15,8 kHz no se toca");
        dur["cancion.mp3"] = 30f;
        Check(eq.Invoke(f, new object[] { "cancion.mp3" }) == null, "billar: una cancion de menos de 45 s no se ecualiza");
        PonStatic("localAuto", false);
        float[] corta = (float[])eq.Invoke(f, new object[] { "cancion.mp3" });
        Check(corta != null && corta[0] == siempre[0], "apagado: la de 30 s se ecualiza como siempre");

        Console.WriteLine("\n  3) LO QUE DECIDE EL MODO, Y APAGADO MANDA EL INTERRUPTOR\n");
        PropertyInfo dsee = TF.GetProperty("DseeEfectivo", T);
        Pon(f, "sbrOn", true);
        PonStatic("localAuto", false); bool dOff = (bool)dsee.GetValue(f, null);
        PonStatic("localAuto", true); bool dOn = (bool)dsee.GetValue(f, null);
        Check(dOff && !dOn, "DSEE: con el interruptor encendido suena; en el billar no (lo guardado no se toca)");
        PonStatic("canalMudo", false); PonStatic("quitaContinua", false); PonStatic("quitaZumbido", false);
        PonStatic("localAuto", true);
        bool aOn = (bool)TF.GetProperty("CanalMudoEf", T).GetValue(null, null) && (bool)TF.GetProperty("QuitaContinuaEf", T).GetValue(null, null) && (bool)TF.GetProperty("QuitaZumbidoEf", T).GetValue(null, null);
        PonStatic("localAuto", false);
        bool aOff = (bool)TF.GetProperty("CanalMudoEf", T).GetValue(null, null) || (bool)TF.GetProperty("QuitaContinuaEf", T).GetValue(null, null) || (bool)TF.GetProperty("QuitaZumbidoEf", T).GetValue(null, null);
        Check(aOn && !aOff, "arreglos de la grabacion: siempre en el billar; apagado, sus interruptores");
        PonStatic("canalMudo", true); PonStatic("quitaContinua", true); PonStatic("quitaZumbido", true);
        Pon(f, "bassAuto", false); Pon(f, "bassAmount", 0.9f); Pon(f, "bassAutoBase", 0.3f);
        MethodInfo bajo = TF.GetMethod("BassBaseNow", T);
        PonStatic("localAuto", false); float bOff = (float)bajo.Invoke(f, null);
        PonStatic("localAuto", true); float bOn = (float)bajo.Invoke(f, null);
        Check(bOff == 0.9f && bOn == 0.3f, "bajo psicoacustico: en el billar, en automatico; apagado, la barra");
        Pon(f, "excAmount", 1f);
        cache["cancion.mp3"] = c; dur["cancion.mp3"] = 200f;
        MethodInfo exc = TF.GetMethod("ExciteAmountFor", T);
        PonStatic("localAuto", true); float eCompleta = (float)exc.Invoke(f, new object[] { "cancion.mp3" });
        float[] cortada = (float[])c.Clone(); cortada[8] = 15000f; cache["cortada.mp3"] = cortada;
        float eCortada = (float)exc.Invoke(f, new object[] { "cortada.mp3" });
        PonStatic("localAuto", false); float eSiempre = (float)exc.Invoke(f, new object[] { "cancion.mp3" });
        float cant = (float)Lee("RealceBillarCantidad");
        Check(eCompleta == 0f && Math.Abs(eCortada - cant) < 1e-4f && eSiempre > 0.5f, "realce: en el billar nada a la completa y la cantidad de fabrica a la cortada; apagado, la barra");

        Console.WriteLine("\n  4) EL CODIGO\n");
        string son = Texto("Form1.Sonido.cs"), dat = Texto("Form1.Datos.cs"), gm = Texto("GraveMono.cs"), vid = Texto("Form1.Video.cs"), aj = Texto("Form1.Ajustes.cs"), bil = Texto("CadenaBillar.cs");
        Check(son.Contains("c.Sbr.Active = DseeEfectivo;") && son.Contains("return DseeEfectivo;") && dat.Contains("sp.Active = DseeEfectivo;") && vid.Contains("return DseeEfectivo &&"), "el DSEE se mira por lo efectivo al armar, en vivo, en la siguiente y en el letrero");
        Check(son.Contains("if (quitaSiseo && !localAuto)"), "el siseo no se arma en el billar");
        Check(son.Contains("new ArreglaGrabacion(chain, CanalMudoEf, QuitaContinuaEf)") && son.Contains("QuitaZumbidoEf") && gm.Contains("c.Arreglo.Ajusta(CanalMudoEf, QuitaContinuaEf)"), "los arreglos se arman y se ajustan en vivo con lo efectivo");
        Check(dat.Contains("aLaMedida || localAuto ? MetaDeCancion(c) : null"), "a la medida: siempre en el billar");
        Check(son.Contains("new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto)") && son.Contains("new SuavizaEses(c.Ataque, loc.Eses, localAuto)"), "presencia y eses con la referencia del billar");
        Check(bil.Contains("private bool DseeEfectivo { get { return sbrOn && !localAuto; } }"), "lo efectivo: el interruptor guardado manda con el modo apagado");
        Check(aj.Contains("\"SonidoAjustesAbrir\"") && aj.Contains("tBil.Controls.Add(swAutoL)") && aj.Contains("pAju.Controls.Add(tCal)") && aj.Contains("tBil.Controls.Add(bPruI)"),
              "la ventana: el modo billar, su boton de ajustes al lado y la prueba de bocinas en la pagina; la calidad en la hoja de ajustes");
        Check(aj.Contains("Necesita(swEses, sinAuto") && aj.Contains("Necesita(swSis, sinAuto") && aj.Contains("Necesita(swMudo, sinAuto") && aj.Contains("Necesita(swRet, sinAuto") && aj.Contains("Necesita(swBom, sinAuto")
              && aj.Contains("NecesitaMotivo(swSbr,") && aj.Contains("bar[6].Enabled = !localAuto;"), "lo que decide el modo, en gris en las hojas diciendo por que");
        Check(aj.Contains("bomboSinHundir = swBom.Checked; retumbeDinamico = swRet.Checked; ataqueClaro = swAta.Checked;") && aj.Contains("bomboSinHundir = bkBom;"), "voz, golpe y retumbe: por fin con su mando (y Cancelar lo devuelve)");

        PonStatic("localAuto", false);
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos (cuentas en Linux/Mono; nada oido en el billar)");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
