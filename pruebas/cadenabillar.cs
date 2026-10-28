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
class Fuente : NAudio.Wave.ISampleProvider { float[] x; int p; public Fuente(float[] a) { x = a; } public NAudio.Wave.WaveFormat WaveFormat { get { return NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); } }
    public int Read(float[] b, int o, int n) { int k = Math.Min(n, x.Length - p); Array.Copy(x, p, b, o, k); p += k; return k; } }
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
        Check(son.Contains("new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto)") && son.Contains("new SuavizaEses(c.Aspereza, loc.Eses, localAuto)"), "presencia y eses con la referencia del billar");
        Check(bil.Contains("private bool DseeEfectivo { get { return sbrOn && !localAuto; } }"), "lo efectivo: el interruptor guardado manda con el modo apagado");
        Check(aj.Contains("\"SonidoAjustesAbrir\"") && aj.Contains("tBil.Controls.Add(swAutoL)") && aj.Contains("pAju.Controls.Add(tCal)") && aj.Contains("tBil.Controls.Add(bPruI)"),
              "la ventana: el modo billar, su boton de ajustes al lado y la prueba de bocinas en la pagina; la calidad en la hoja de ajustes");
        Check(aj.Contains("Necesita(swEses, sinAuto") && aj.Contains("Necesita(swSis, sinAuto") && aj.Contains("Necesita(swMudo, sinAuto") && aj.Contains("Necesita(swRet, sinAuto") && aj.Contains("Necesita(swBom, sinAuto")
              && aj.Contains("NecesitaMotivo(swSbr,") && aj.Contains("bar[6].Enabled = !localAuto;"), "lo que decide el modo, en gris en las hojas diciendo por que");
        Check(aj.Contains("bomboSinHundir = swBom.Checked; retumbeDinamico = swRet.Checked; ataqueClaro = swAta.Checked;") && aj.Contains("bomboSinHundir = bkBom;"), "voz, golpe y retumbe: por fin con su mando (y Cancelar lo devuelve)");

        Console.WriteLine("\n  5) SEGUNDA TANDA: PRESENCIA, SUBGRAVE A 40 Hz Y AFINAR EL LOCAL\n");
        Type TP = TF.GetNestedType("PresenciaSala", T);
        Check((double)TP.GetField("CompensaBillarDb", T).GetRawConstantValue() == 5.0 && (double)TP.GetField("CompensaMaxDb", T).GetRawConstantValue() == 3.0,
              "presencia: en el billar devuelve hasta 5 dB (lo de siempre, 3)");
        PropertyInfo subEf = TF.GetProperty("SubgraveEf", T); FieldInfo subHz = TF.GetField("subgraveHz", T); object subAntes = subHz.GetValue(null);
        subHz.SetValue(null, 30f); PonStatic("localAuto", false); float s0 = (float)subEf.GetValue(null, null);
        PonStatic("localAuto", true); float s1 = (float)subEf.GetValue(null, null);
        subHz.SetValue(null, 55f); float s2 = (float)subEf.GetValue(null, null);
        subHz.SetValue(null, subAntes); PonStatic("localAuto", false);
        Check(s0 == 30f && s1 == 40f && s2 == 55f, "subgrave: en el billar al menos a 40 Hz (si alguien puso uno mas alto, manda el suyo); apagado, el de siempre");
        MethodInfo cAfi = TF.GetMethod("CorreccionAfinada", T), enEq = TF.GetMethod("AfinadaEnBandasEq", T);
        float[] plano = { 80f, 80f, 80f, 80f, 80f, 80f, 80f, 80f };
        float[] cp = (float[])cAfi.Invoke(null, new object[] { plano, 2 });
        Check(cp != null && cp[0] == 0f && cp[3] == 0f && cp[7] < 0f && cp[7] > -1.5f && cp[1] >= 0f, "local plano en Z: casi nada (la curva de casa pide un poco menos de 8 kHz y no sube 63 Hz)");
        float[] falta = (float[])plano.Clone(); falta[3] = float.NaN;
        Check(cAfi.Invoke(null, new object[] { falta, 2 }) == null, "sin las bandas de la voz no hay afinacion");
        float[] chillon = (float[])plano.Clone(); chillon[6] = 92f;
        float[] cc = (float[])cAfi.Invoke(null, new object[] { chillon, 2 });
        Check(cc[6] <= -6f + 1e-4f && cc[6] >= -6f - 1e-4f, "4 kHz 12 dB de mas: baja, con el tope de 6 dB");
        float[] hueco = (float[])plano.Clone(); hueco[1] = 70f; hueco[0] = 60f;
        float[] ch = (float[])cAfi.Invoke(null, new object[] { hueco, 2 });
        Check(ch[1] == 3f && ch[0] == 0f, "125 Hz 10 dB de menos: sube, con el tope de 3 dB; 63 Hz nunca sube");
        float[] conA = { 80f - 26.2f, 80f - 16.1f, 80f - 8.6f, 80f - 3.2f, 80f, 81.2f, 81f, 78.9f };
        float[] ca = (float[])cAfi.Invoke(null, new object[] { conA, 0 }), cz = (float[])cAfi.Invoke(null, new object[] { plano, 2 });
        bool igual = true; for (int b = 0; b < 8; b++) if (Math.Abs(ca[b] - cz[b]) > 1e-3f) igual = false;
        Check(igual, "la ponderacion A de la app se devuelve: el mismo local leido en A da la misma correccion que en Z");
        float[] e = (float[])enEq.Invoke(null, new object[] { new float[] { -1f, -2f, -3f, -4f, -5f, -6f, -4f, -2f } });
        Check(e[0] == -1f && e[3] == -5f && Math.Abs(e[6] + 1f) < 1e-4f && e[7] == 0f && e[1] < -2f && e[1] > -3f, "a las bandas del EQ: interpolada en octavas, 10 kHz la mitad de 8 kHz y 15,8 kHz nada");
        // En EqCorrFor: la afinacion encima, tambien en una cancion que el EQ no toca, y sin la inclinacion estimada.
        float[] med = (float[])TF.GetField("afinadoDb", T).GetValue(null); float[] medAntes = (float[])med.Clone();
        for (int b = 0; b < 8; b++) med[b] = chillon[b];
        TF.GetField("afinadoPonderacion", T).SetValue(null, 2); PonStatic("afinadoUsar", true);
        dur["cancion.mp3"] = 30f;
        PonStatic("localAuto", false); object apag = eq.Invoke(f, new object[] { "cancion.mp3" });
        PonStatic("localAuto", true); float[] corta2 = (float[])eq.Invoke(f, new object[] { "cancion.mp3" });
        Check(apag != null && ((float[])apag)[5] > 0f, "con el modo apagado la afinacion no hace nada (el EQ de siempre)");
        Check(corta2 != null && corta2[5] < -3f && corta2[0] == 0f, "en el billar, la afinacion va tambien en una cancion que el EQ no toca (la de 30 s)");
        PonStatic("afinadoUsar", false);
        Check(eq.Invoke(f, new object[] { "cancion.mp3" }) == null, "con su interruptor apagado, nada");
        for (int b = 0; b < 8; b++) med[b] = medAntes[b];
        PonStatic("afinadoUsar", false); PonStatic("localAuto", false);
        MethodInfo lee = TF.GetMethod("LeeAfinado", T, null, new Type[] { typeof(string) }, null), texto = TF.GetMethod("TextoAfinado", T, null, Type.EmptyTypes, null);
        lee.Invoke(null, new object[] { "78;84.5;x;85;84;86;87;200" });
        string vuelta = (string)texto.Invoke(null, null);
        Check(vuelta == "78;84.5;x;85;84;86;87;x", "la configuracion: ida y vuelta, \"x\" sin medir y lo absurdo se descarta (" + vuelta + ")");
        lee.Invoke(null, new object[] { "x;x;x;x;x;x;x;x" });
        Check(son.Contains("SubgraveEf") && dat.Contains("ConLocalAfinado(EqCorrCancion(path))") && aj.Contains("\"SonidoAfinarAbrir\"") && aj.Contains("NecesitaMotivo(swAfi,"),
              "el codigo: el subgrave del billar al armar, la afinacion dentro del EQ y su hoja en la ventana");

        Console.WriteLine("\n  6) TERCERA TANDA: VOLUMEN PAREJO, VARIOS PUNTOS Y EL BAJO SEGUN LO MEDIDO\n");
        MethodInfo faltaM = TF.GetMethod("ParejoFalta", T), nuevoM = TF.GetMethod("ParejoNuevo", T);
        float fa = (float)faltaM.Invoke(null, new object[] { -1.1f, -0.3f, 1.1f });
        Check(Math.Abs(fa - 1.85f) < 1e-4f, "volumen parejo: al reggaeton del banco (P90 -1,1 dB del techo) le faltan 1,85 dB");
        float fb = (float)faltaM.Invoke(null, new object[] { 0.5f, 4f, 3f });
        Check(Math.Abs(fb + 1f) < 1e-4f, "y si su P99 pasa 1 dB de lo que se aguanta, baja 1 dB");
        float n1 = (float)nuevoM.Invoke(null, new object[] { float.NaN, 0f, 1.85f });
        float n2 = (float)nuevoM.Invoke(null, new object[] { 1.3f, 1.3f, 0.5f });
        float n3 = (float)nuevoM.Invoke(null, new object[] { 1.7f, 1.7f, 0.1f });
        float n4 = (float)nuevoM.Invoke(null, new object[] { float.NaN, 0f, 9f });
        Check(Math.Abs(n1 - 1.3f) < 0.01f && Math.Abs(n2 - 1.65f) < 0.01f && float.IsNaN(n3) && Math.Abs(n4 - 1.4f) < 0.01f,
              "se acerca el 70 % cada vez, lo de menos de 0,25 dB se deja y nunca pasa de 2 dB (" + n1 + ", " + n2 + ", " + n3 + ", " + n4 + ")");
        float enRacha = (float)nuevoM.Invoke(null, new object[] { 1.8f, 0f, 1.85f });
        Check(float.IsNaN(enRacha), "en racha con su disco (sonando con 0 dB y faltandole 1,85), lo suyo (1,8) no se pierde: ya estaba bien");
        // El oido: pasa tal cual, cuenta bloques y solo aprende si sono entera.
        Type TM = TF.GetNestedType("MideMargen", T);
        float[] tono = new float[44100 * 2 * 40]; for (int i = 0; i < tono.Length; i += 2) { float v = (float)(0.8 * Math.Sin(2 * Math.PI * 100 * (i / 2) / 44100.0)); tono[i] = v; tono[i + 1] = v; }
        object mm = Activator.CreateInstance(TM, T, null, new object[] { new Fuente(tono), 0.88f, true, 0f, 40.0 }, null);
        NAudio.Wave.ISampleProvider mp = (NAudio.Wave.ISampleProvider)mm;
        float[] bl = new float[4410 * 2]; bool igualM = true; int leido = 0; int r;
        while ((r = mp.Read(bl, 0, bl.Length)) > 0) { for (int i = 0; i < r; i++) if (bl[i] != tono[leido + i]) igualM = false; leido += r; }
        object[] po = { 0f, 0f, 0f }; bool hay = (bool)TM.GetMethod("Percentiles", T).Invoke(mm, po);
        Check(igualM && leido == tono.Length, "el oido no toca ni una muestra");
        Check(hay && Math.Abs((float)po[0] - 20 * (float)Math.Log10(0.8 / 0.88)) < 0.1f && (bool)TM.GetProperty("Entera", T).GetValue(mm, null), "y mide el pico por bloque frente al techo (" + po[0] + " dB), con la cancion entera");
        object mc = Activator.CreateInstance(TM, T, null, new object[] { new Fuente(tono), 0.88f, true, 0f, 80.0 }, null);
        ((NAudio.Wave.ISampleProvider)mc).Read(bl, 0, bl.Length);
        Check(!(bool)TM.GetProperty("Entera", T).GetValue(mc, null), "si se salta a media cancion no aprende nada");
        string son3 = Texto("Form1.SinPausa.cs"), ld = Texto("NivelDisco.cs"), lq = Texto("LoQueSuena.cs");
        Check(son.Contains("if (localAuto) { c.Margen = new MideMargen(") && son3.Contains("AprendeParejo(a);") && son3.Contains("AprendeParejo(cadenaSonando);")
              && ld.Contains("ParejoAplicado(t)") && lq.Contains("c.Margen.Descarta = true"), "se mide solo en el billar, se aprende al soltar la cadena, se usa al nivelar y no con «Escuchar sin mejoras»");
        // Varios puntos: retirados en la sexta tanda (el dueno pidio afinar con el celular lo mas simple posible: un solo punto).
        Check(!dat.Contains("afinado_local2=") && !dat.Contains("afinado_local3=") && TF.GetMethod("PromedioPuntos", T) == null && TF.GetField("afinadoPuntos", T) == null,
              "un solo punto: afinado_local2= y afinado_local3= ya no se leen ni se guardan");
        // El bajo segun lo medido.
        MethodInfo fbm = TF.GetMethod("FactorBajoMedido", T);
        float bajoPlano = (float)fbm.Invoke(null, new object[] { new float[] { 82, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        float bajoFalta = (float)fbm.Invoke(null, new object[] { new float[] { 70, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        float bajoSobra = (float)fbm.Invoke(null, new object[] { new float[] { 92, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        float bajoNada = (float)fbm.Invoke(null, new object[] { new float[] { float.NaN, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        Check(bajoPlano == 1f && bajoFalta > 1.3f && bajoFalta <= 1.4f && bajoSobra < 1f && bajoSobra >= 0.75f && bajoNada == 1f,
              "el bajo segun lo medido: igual si 63 Hz va bien, mas si falta (" + bajoFalta + "), menos si sobra (" + bajoSobra + "), igual sin medir");
        Check(son.Contains("bassSongFactor * BajoMedido") && son.Contains("* factor * BajoMedido"), "el bajo psicoacustico lo lleva al sonar y en la siguiente");

        Console.WriteLine("\n  7) CUARTA TANDA: VOLUMEN PAREJO DESDE LA PRIMERA VEZ Y LAS BANDAS SEGUIDAS\n");
        MethodInfo ajL = TF.GetMethod("AjusteLineal", T);
        System.Collections.Generic.List<double[]> XX = new System.Collections.Generic.List<double[]>(); System.Collections.Generic.List<double> YY = new System.Collections.Generic.List<double>();
        Random rnd = new Random(5);
        for (int i = 0; i < 60; i++) { double a1 = rnd.NextDouble() * 10 - 5, a2 = rnd.NextDouble() * 6 - 3, a3 = 5 + rnd.NextDouble() * 10; XX.Add(new double[] { a1, a2, a3 }); YY.Add(0.2 + 0.3 * a1 - 0.1 * a2 + 0.0 * a3 + (rnd.NextDouble() - 0.5) * 0.2); }
        object[] aa = { XX, YY, 1.0, 0.0 };
        double[] ww = (double[])ajL.Invoke(null, aa);
        Check(ww != null && Math.Abs(ww[1] - 0.3) < 0.03 && Math.Abs(ww[2] + 0.1) < 0.04 && (double)aa[3] > 0.9, "la recta: encuentra lo que manda (0,3 por dB de grave profundo) y explica casi todo (R2 " + ((double)aa[3]).ToString("0.00") + ")");
        // En la rocola: 40 canciones aprendidas cuyo aprendido sigue al grave profundo; una sin aprender recibe la mitad de lo predicho.
        object g2 = FormatterServices.GetUninitializedObject(TF);
        System.Collections.Generic.Dictionary<string, float[]> cache2 = new System.Collections.Generic.Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        TF.GetField("eqCache", T).SetValue(g2, cache2); TF.GetField("eqAvg", T).SetValue(g2, new float[] { -6f, -3f, -2f, -4f, -8f, -12f, -16f, -22f });
        TF.GetField("parejoModeloN", T).SetValue(g2, -1000); TF.GetField("parejoModeloCuando", T).SetValue(g2, DateTime.MinValue);
        FieldInfo dbF = TF.GetField("parejoDb", T); object dbAntes = dbF.GetValue(null);
        System.Collections.Generic.Dictionary<string, float> apr = new System.Collections.Generic.Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 40; i++)
        {
            float g = -6f + (i % 9) - 4f; string ruta = "c" + i + ".mp3";
            cache2[ruta] = new float[] { g, -3f, -2f, -4f, -8f, -12f, -16f, -22f, 20000f, 0f, 30f, ver };
            apr[ruta] = (float)Math.Round(0.3 * (g + 6f), 2);
        }
        cache2["nueva.mp3"] = new float[] { -2f, -3f, -2f, -4f, -8f, -12f, -16f, -22f, 20000f, 0f, 30f, ver };   // 4 dB de grave profundo de mas: 1,2 dB
        cache2["sinhuella.mp3"] = new float[] { -2f, -3f, -2f, -4f, -8f, -12f, -16f, -22f, 20000f, 0f, 30f, 1f };
        dbF.SetValue(null, apr);
        MethodInfo est = TF.GetMethod("ParejoEstimado", T);
        float eN = (float)est.Invoke(g2, new object[] { "nueva.mp3" }), eA = (float)est.Invoke(g2, new object[] { "c3.mp3" }), eS = (float)est.Invoke(g2, new object[] { "sinhuella.mp3" });
        Check(Math.Abs(eN - 0.6f) < 0.08f, "una sin aprender con 4 dB de grave profundo de mas: la mitad de lo predicho (" + eN + " dB)");
        Check(eA == apr["c3.mp3"] && eS == 0f, "la aprendida usa lo suyo, y una sin huella nada");
        apr.Clear(); for (int i = 0; i < 20; i++) apr["c" + i + ".mp3"] = 1f;
        TF.GetField("parejoModeloN", T).SetValue(g2, -1000); TF.GetField("parejoModeloCuando", T).SetValue(g2, DateTime.MinValue); TF.GetField("parejoModelo", T).SetValue(g2, null);
        Check((float)est.Invoke(g2, new object[] { "nueva.mp3" }) == 0f, "con menos de 30 aprendidas, nada de recta");
        dbF.SetValue(null, dbAntes);
        Check(Texto("VolumenParejo.cs").Contains("return ParejoEstimado(t.Path);"), "al sonar se usa lo estimado (y al aprender, lo que llevaba)");
        // Las bandas seguidas.
        Type TB = TF.GetNestedType("PruebaBanda", T);
        object pb = Activator.CreateInstance(TB, T, null, new object[] { 0, 7 }, null);
        long tot = (long)TB.GetProperty("Total", T).GetValue(pb, null);
        MethodInfo oida = TB.GetMethod("BandaOida", T); FieldInfo posF = TB.GetField("pos", T);
        posF.SetValue(pb, (long)(44100 * 9.5 * 2 + 44100 * 2)); int bo = (int)oida.Invoke(pb, new object[] { 0 });
        posF.SetValue(pb, (long)(44100 * 9.5 - 44100)); int bp = (int)oida.Invoke(pb, new object[] { 0 });
        posF.SetValue(pb, 0L);
        Check(tot == (long)(44100 * 9.5 * 7 + 44100 * 8) && bo == 2 && bp == -1, "las 8 seguidas: 8 s cada una con 1,5 s de silencio entre ellas, y el letrero sabe cual suena");
        float[] buf = new float[(int)tot * 2]; int got = 0, rr2;
        while ((rr2 = ((NAudio.Wave.ISampleProvider)pb).Read(buf, got, Math.Min(8820, buf.Length - got))) > 0) got += rr2;
        bool parejas = true; string niveles = "";
        for (int k = 0; k < 8; k++)
        {
            long a0 = (long)(44100 * 9.5 * k + 44100 * 2), a1 = a0 + 44100 * 4; double sq = 0;
            for (long i = a0; i < a1; i++) sq += buf[2 * i] * buf[2 * i];
            double db = 10 * Math.Log10(sq / (a1 - a0)); niveles += db.ToString("0.0") + " ";
            if (Math.Abs(db + 20.0) > 1.0) parejas = false;
        }
        Check(got == buf.Length && parejas, "y todas a -20 dB por canal (" + niveles.Trim() + ")");

        Console.WriteLine("\n  8) QUINTA TANDA: AGUDOS ASPEROS, LIMITADOR QUE RESPIRA, BITACORA Y COMPARAR A CIEGAS\n");
        Type TAs = TF.Assembly.GetType("Rocola.AsperezaDinamica");
        int nA = 44100 * 30; float[] xa = new float[nA * 2]; Random ra = new Random(3);
        double bw = 2 * Math.PI * 3200 / 44100, bal = Math.Sin(bw) / 2, ba0 = 1 + bal, bb0 = bal / ba0, ba1 = -2 * Math.Cos(bw) / ba0, ba2 = (1 - bal) / ba0, bx1 = 0, bx2 = 0, by1 = 0, by2 = 0;
        for (int i = 0; i < nA; i++)
        {
            double t = i / 44100.0, voz = 0.15 * (Math.Sin(2 * Math.PI * 220 * t) + 0.6 * Math.Sin(2 * Math.PI * 440 * t) + 0.4 * Math.Sin(2 * Math.PI * 1000 * t) + 0.25 * Math.Sin(2 * Math.PI * 2900 * t));
            double rr = ra.NextDouble() * 2 - 1, bv = bb0 * (rr - bx2) - ba1 * by1 - ba2 * by2; bx2 = bx1; bx1 = rr; by2 = by1; by1 = bv;
            bool raf = (t % 4.0) > 2.0 && (t % 4.0) < 2.5; double v = voz + (raf ? 0.35 : 0.04) * bv;
            xa[2 * i] = (float)v; xa[2 * i + 1] = (float)v;
        }
        object asOff = Activator.CreateInstance(TAs, T, null, new object[] { new Fuente(xa), false }, null);
        float[] ya = new float[xa.Length]; int ga = 0, ka; while ((ka = ((NAudio.Wave.ISampleProvider)asOff).Read(ya, ga, Math.Min(4410, ya.Length - ga))) > 0) ga += ka;
        bool igualA = ga == xa.Length; for (int i = 0; i < ga && igualA; i++) if (ya[i] != xa[i]) igualA = false;
        Check(igualA, "agudos asperos apagado: sale bit a bit");
        object asOn = Activator.CreateInstance(TAs, T, null, new object[] { new Fuente(xa), true }, null);
        ga = 0; while ((ka = ((NAudio.Wave.ISampleProvider)asOn).Read(ya, ga, Math.Min(4410, ya.Length - ga))) > 0) ga += ka;
        double maxCorte = (double)TAs.GetProperty("CortaDb", T).GetValue(asOn, null);
        bool vozIgual = true; for (int i = (int)(44100 * 8.5) * 2; i < (int)(44100 * 9.8) * 2; i++) if (ya[i] != xa[i]) vozIgual = false;
        Check(maxCorte > 0.8f && maxCorte <= 3.0001f, "encendido: en una rafaga que grita a 3,2 kHz corta (la mitad de lo que pasa de 4 dB sobre lo normal; aqui " + maxCorte.ToString("0.00") + " dB, tope 3)");
        Check(vozIgual, "y fuera de las rafagas (la voz sola) no toca ni una muestra");
        // El limitador que respira: sin el, bit a bit; con el, la ganancia se mueve menos con un bajo que pasa del techo a golpes.
        Type TL = TF.GetNestedType("LimitadorPicoReal", T);
        float[] xl = new float[44100 * 2 * 20];
        for (int i = 0; i < xl.Length / 2; i++) { double t = i / 44100.0, golpe = (t * 2.0) % 1.0 < 0.12 ? 1.5 : 0.6; float v = (float)(golpe * Math.Sin(2 * Math.PI * 55 * t) + 0.2 * Math.Sin(2 * Math.PI * 700 * t)); xl[2 * i] = v; xl[2 * i + 1] = v; }
        object l0 = Activator.CreateInstance(TL, T, null, new object[] { new Fuente(xl), 0.88f, 3.0 }, null), l1 = Activator.CreateInstance(TL, T, null, new object[] { new Fuente(xl), 0.88f, 3.0, false }, null), l2 = Activator.CreateInstance(TL, T, null, new object[] { new Fuente(xl), 0.88f, 3.0, true }, null);
        float[] o0 = new float[xl.Length + 2000], o1 = new float[o0.Length], o2 = new float[o0.Length];
        double mov1 = 0, mov2 = 0; int g0 = 0, g1 = 0, g2b = 0, kk2; float prev1 = 1f, prev2 = 1f;
        while ((kk2 = ((NAudio.Wave.ISampleProvider)l0).Read(o0, g0, Math.Min(882, o0.Length - g0))) > 0) g0 += kk2;
        while ((kk2 = ((NAudio.Wave.ISampleProvider)l1).Read(o1, g1, Math.Min(882, o1.Length - g1))) > 0) g1 += kk2;
        FieldInfo envF = TL.GetField("env", T);
        while ((kk2 = ((NAudio.Wave.ISampleProvider)l2).Read(o2, g2b, Math.Min(882, o2.Length - g2b))) > 0) { g2b += kk2; float e2 = (float)envF.GetValue(l2); mov2 += Math.Abs(20 * Math.Log10(e2) - 20 * Math.Log10(prev2)); prev2 = e2; }
        object l3 = Activator.CreateInstance(TL, T, null, new object[] { new Fuente(xl), 0.88f, 3.0, false }, null); int g3 = 0; float[] o3 = new float[o0.Length];
        while ((kk2 = ((NAudio.Wave.ISampleProvider)l3).Read(o3, g3, Math.Min(882, o3.Length - g3))) > 0) { g3 += kk2; float e1 = (float)envF.GetValue(l3); mov1 += Math.Abs(20 * Math.Log10(e1) - 20 * Math.Log10(prev1)); prev1 = e1; }
        bool igualL = g0 == g1; for (int i = 0; i < g0 && igualL; i++) if (o0[i] != o1[i]) igualL = false;
        Check(igualL, "limitador sin respirar: el mismo de siempre, bit a bit");
        Check(mov2 < mov1 * 0.9, "respirando, su ganancia se mueve menos con golpes seguidos (" + mov1.ToString("0") + " -> " + mov2.ToString("0") + " dB de recorrido)");
        Check(son.Contains("new LimitadorPicoReal(alLimitador, TechoPicoReal, 3.0, localAuto)"), "y solo respira en el billar");
        // La bitacora: tantas columnas como la cabecera.
        string cab = (string)TF.GetField("BitacoraBillarCabecera", T).GetRawConstantValue();
        Type TAj = TF.GetNestedType("AjusteLocal", T); object ajB = Activator.CreateInstance(TAj, true);
        string lin = (string)TF.GetMethod("LineaBitacoraBillar", T).Invoke(null, new object[] { new DateTime(2026, 10, 27, 21, 5, 0), "cumbia.mp3", true, 215.0, 0.6f, float.NaN, 0.4f, 2.1f, 3.3f, 0.05f, 0.002f, ajB, true, 1.2f, 40f, new float[] { -1, 0, 0, 0, 0, -2, -1, 0 } });
        Check(lin.Split('\t').Length == cab.Split('\t').Length && lin.StartsWith("2026-10-27 21:05:00\tcumbia.mp3\tsi\t215\t0.60\t-\t0.4"), "la bitacora: una linea por cancion con tantas columnas como la cabecera");
        Check(Texto("VolumenParejo.cs").Contains("ApuntaBitacoraBillar(c, m, p90, p99, max, nuevo)") && Texto("BitacoraBillar.cs").Contains("QueueUserWorkItem"), "se apunta al soltar la cancion, aprenda o no, y se escribe en segundo plano");
        // Comparar a ciegas.
        bool[] orden = (bool[])TF.GetMethod("OrdenCiego", T).Invoke(null, new object[] { new Random(1) }); int nb = 0; foreach (bool b2 in orden) if (b2) nb++;
        MethodInfo resC = TF.GetMethod("ResultadoCiego", T);
        Check(orden.Length == 8 && nb == 4, "a ciegas: ocho tramos, cuatro de cada uno, al azar");
        Check(((string)resC.Invoke(null, new object[] { 4, 1, 0 })).Contains("el modo billar") && ((string)resC.Invoke(null, new object[] { 1, 3, 0 })).Contains("fabrica") && ((string)resC.Invoke(null, new object[] { 2, 2, 0 })).StartsWith("Empate") && ((string)resC.Invoke(null, new object[] { 0, 0, 0 })).StartsWith("No votaste"), "y al final dice cual elegiste mas veces");
        Check(dat.Contains("Append(LocalAutoAGuardar ?") && aj.Contains("if (ciegoActivo) TerminaCiego(\"Ventana cerrada") && aj.Contains("if (ciegoActivo) TerminaCiego(\"Cambiaste un ajuste"), "no se guarda: la configuracion lleva el modo de antes y vuelve al cerrar o al tocar un ajuste");

        Console.WriteLine("\n  9) SEXTA TANDA: AFINAR CON EL CELULAR, LO MAS SIMPLE, Y EL RESUMEN DE LA BITACORA\n");
        Check(aj.Contains("\"SonidoAfinarTodas\"") && aj.Contains("\"SonidoAfinarDb\" + k") && aj.Contains("new BotonPlano[2]") && aj.Contains("\"dB\" + AfinaPonderacionNombre[k]")
              && !aj.Contains("SonidoAfinarPunto") && !aj.Contains("SonidoAfinarBanda") && !aj.Contains("SonidoAfinarBorrar"),
              "la hoja: un boton toca las 8 bandas, una barra por banda y la app en dBA o dBC; sin puntos ni botones por banda");
        Check(TF.GetMethod("ProbarBanda", T) == null && TF.GetMethod("EstadoBanda", T) == null && TF.GetMethod("ProbarBandas", T) != null && TF.GetMethod("BandaGrande", T) != null,
              "las bandas solo suenan seguidas, con la que suena en grande");
        float[] cZ = (float[])cAfi.Invoke(null, new object[] { plano, 2 });
        Check(cZ != null && (int)TF.GetMethod("PonderacionDe", T).Invoke(null, new object[] { "Z" }) == 2, "la Z guardada de antes se sigue entendiendo");
        Check(!File.Exists(Path.Combine(Path.Combine("..", "Rocola"), "AfinarMicrofono.cs")), "nada de microfono (el cable no llega)");
        // El resumen de la bitacora en la tarjeta del billar.
        Type TRB = TF.GetNestedType("ResumenBitacora", T); object rb = Activator.CreateInstance(TRB, true);
        MethodInfo acu = TF.GetMethod("AcumulaBitacora", T), txtR = TF.GetMethod("TextoResumenBitacora", T);
        DateTime ahoraB = new DateTime(2026, 10, 27, 23, 0, 0);
        string vacia = (string)txtR.Invoke(null, new object[] { rb, ahoraB });
        string lin2 = (string)TF.GetMethod("LineaBitacoraBillar", T).Invoke(null, new object[] { new DateTime(2026, 10, 27, 22, 0, 0), "Los Tigres del Norte - Contrabando y traicion.mp3", true, 200.0, 0f, float.NaN, 0f, 0f, 0f, 0.1f, 0.074f, ajB, false, 1f, 40f, null });
        bool rb1 = (bool)acu.Invoke(null, new object[] { rb, cab, ahoraB }), rb2 = (bool)acu.Invoke(null, new object[] { rb, lin, ahoraB }), rb3 = (bool)acu.Invoke(null, new object[] { rb, lin2, ahoraB });
        string res = (string)txtR.Invoke(null, new object[] { rb, ahoraB }), resManana = (string)txtR.Invoke(null, new object[] { rb, ahoraB.AddDays(1) });
        Check(vacia.StartsWith("Bitacora: aun vacia") && !rb1 && rb2 && rb3, "el resumen: sin lineas dice que esta vacia, y la cabecera no cuenta");
        Check(res.StartsWith("Bitacora: 2 canciones; 2 en las ultimas 12 h.\n") && res.Contains("la que mas: Los Tigres del Norte...") && res.Contains("(7 %)") && res.Split('\n').Length == 2,
              "en dos lineas: cuantas, cuantas esta noche y la que mas apreto el limitador (" + res.Replace("\n", " | ") + ")");
        Check(resManana.StartsWith("Bitacora: 2 canciones; 0 en las ultimas 12 h.\nEl limitador bajo mas de 3 dB el"), "sin canciones recientes, el promedio de todas");
        Check(Texto("Form1.cs").Contains("QueueUserWorkItem(delegate { CargaResumenBitacora(); })") && Texto("BitacoraBillar.cs").Contains("if (r != null) lock (r) AcumulaBitacora(r, linea, DateTime.Now);")
              && aj.Contains("+ TextoBitacoraTarjeta()"), "se lee al arrancar en segundo plano, se suma cada linea nueva y va en la tarjeta del billar");

        Console.WriteLine("\n  10) SEPTIMA TANDA: REVISION Y CON O SIN AFINAR A CIEGAS\n");
        // El limitador que respira, tras 100 s sin limitar: su actividad llega a cero exacto (sin desnormales).
        float[] xq = new float[44100 * 2 * 102];
        for (int i = 0; i < 44100 * 2; i++) xq[2 * i] = xq[2 * i + 1] = (float)(1.6 * Math.Sin(i * 0.05));
        for (int i = 44100 * 2; i < xq.Length / 2; i++) xq[2 * i] = xq[2 * i + 1] = (float)(1e-3 * Math.Sin(i * 0.05));
        object lq2 = Activator.CreateInstance(TL, T, null, new object[] { new Fuente(xq), 0.88f, 3.0, true }, null);
        float[] oq = new float[xq.Length]; int gq = 0, kq; float actMax = 0f;
        FieldInfo actF = TL.GetField("actividad", T);
        while ((kq = ((NAudio.Wave.ISampleProvider)lq2).Read(oq, gq, Math.Min(8820, oq.Length - gq))) > 0) { gq += kq; if (gq == 44100 * 2 * 2) actMax = (float)actF.GetValue(lq2); }
        float actFin = (float)actF.GetValue(lq2);
        Check(actMax > 0.5f && actFin == 0f, "limitador que respira: tras 100 s sin limitar, su actividad queda en cero exacto, sin desnormales (" + actMax.ToString("0.00") + " -> " + actFin + ")");
        Check(Texto("PicoReal.cs").Contains("if (actividad < 1e-6f) actividad = 0f;"), "y se corta a cero en el codigo");
        // Comparar a ciegas: no se aprende de esa vuelta y se acaba antes de armar otra cancion.
        string ccSrc = Texto("CompararCiegas.cs");
        Check(ccSrc.Contains("if (cs != null && cs.Margen != null) cs.Margen.Descarta = true;"), "mientras se compara, el volumen parejo no aprende de esa cancion");
        Check(son.Contains("if (ciegoActivo) TerminaCiego(\"Se acaba la cancion: comparacion cortada. \");\r\n            CadenaCancion c = new CadenaCancion();")
              || son.Contains("if (ciegoActivo) TerminaCiego(\"Se acaba la cancion: comparacion cortada. \");\n            CadenaCancion c = new CadenaCancion();"),
              "la comparacion se acaba antes de armar la siguiente cancion (no se arma con un tramo de fabrica)");
        // Con o sin afinar.
        Check(((string)resC.Invoke(null, new object[] { 3, 1, 1 })).Contains("con tu afinacion") && ((string)resC.Invoke(null, new object[] { 0, 2, 1 })).Contains("sin tu afinacion"),
              "con o sin afinar: al final dice si te gusto mas con o sin tu afinacion");
        object gC = FormatterServices.GetUninitializedObject(TF);
        float[] afi = (float[])TF.GetField("afinadoDb", T).GetValue(null); float[] afiAntes = (float[])afi.Clone();
        for (int b = 0; b < 8; b++) afi[b] = float.NaN;
        PonStatic("localAuto", true);
        string rNo = (string)TF.GetMethod("EmpiezaCiego", T).Invoke(gC, new object[] { 1 });
        Check(rNo.StartsWith("Anota antes las bandas de la voz"), "sin las bandas de la voz anotadas no se puede comparar la afinacion");
        Array.Copy(afiAntes, afi, 8);
        TF.GetField("ciegoActivo", T).SetValue(gC, true); TF.GetField("ciegoQue", T).SetValue(gC, 1); TF.GetField("ciegoGuardado", T).SetValue(gC, false); PonStatic("afinadoUsar", true);
        PropertyInfo aGu = TF.GetProperty("AfinadoUsarAGuardar", T), lGu = TF.GetProperty("LocalAutoAGuardar", T);
        Check(!(bool)aGu.GetValue(gC, null) && (bool)lGu.GetValue(gC, null), "mientras compara la afinacion se guarda la de antes, y el modo billar tal cual");
        TF.GetField("ciegoActivo", T).SetValue(gC, false); PonStatic("afinadoUsar", false);
        Check(dat.Contains("Append(AfinadoUsarAGuardar ?") && aj.Contains("EmpiezaCiego(CiegoAfinacion)") && aj.Contains("\"SonidoCiegoAfinar\"") && aj.Contains("if (ciegoActivo) TerminaCiego(\"Cambiaste un ajuste: comparacion cortada. \");"),
              "el boton «Con o sin afinar» y la hoja de afinar corta la comparacion si tocas algo");

        PonStatic("localAuto", false);
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos (cuentas en Linux/Mono; nada oido en el billar)");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
