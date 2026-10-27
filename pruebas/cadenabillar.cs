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
        Check(son.Contains("new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto)") && son.Contains("new SuavizaEses(c.Ataque, loc.Eses, localAuto)"), "presencia y eses con la referencia del billar");
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
        // Varios puntos.
        MethodInfo prom = TF.GetMethod("PromedioPuntos", T);
        float[] pm = (float[])prom.Invoke(null, new object[] { new float[][] { new float[] { 80, 80, 80, 80, 80, 80, 80, float.NaN }, new float[] { 84, 82, float.NaN, 80, 80, 80, 80, float.NaN }, new float[] { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN } } });
        Check(pm[0] == 82f && pm[1] == 81f && pm[2] == 80f && float.IsNaN(pm[7]), "varios puntos: el promedio por banda de los que la tienen");
        MethodInfo lee2 = TF.GetMethod("LeeAfinado", T, null, new Type[] { typeof(string), typeof(int) }, null), texto2 = TF.GetMethod("TextoAfinado", T, null, new Type[] { typeof(int) }, null);
        lee2.Invoke(null, new object[] { "80;80;80;80;80;80;80;80", 1 }); lee2.Invoke(null, new object[] { "84;x;x;x;x;x;x;x", 2 });
        float[] ap = (float[])TF.GetMethod("AfinadoPromedio", T).Invoke(null, null);
        Check((string)texto2.Invoke(null, new object[] { 2 }) == "84;x;x;x;x;x;x;x" && ap[0] == 82f && ap[3] == 80f, "la configuracion de los otros puntos y su promedio");
        lee2.Invoke(null, new object[] { "x;x;x;x;x;x;x;x", 1 }); lee2.Invoke(null, new object[] { "x;x;x;x;x;x;x;x", 2 });
        Check(dat.Contains("StartsWith(\"afinado_local2=\")") && dat.Contains("sb.Append(\"afinado_local3=\")"), "se leen y se guardan afinado_local2= y afinado_local3=");
        // El bajo segun lo medido.
        MethodInfo fbm = TF.GetMethod("FactorBajoMedido", T);
        float bajoPlano = (float)fbm.Invoke(null, new object[] { new float[] { 82, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        float bajoFalta = (float)fbm.Invoke(null, new object[] { new float[] { 70, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        float bajoSobra = (float)fbm.Invoke(null, new object[] { new float[] { 92, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        float bajoNada = (float)fbm.Invoke(null, new object[] { new float[] { float.NaN, 81.5f, 80.5f, 80, 80, 79.5f, 78.5f, 77 }, 2 });
        Check(bajoPlano == 1f && bajoFalta > 1.3f && bajoFalta <= 1.4f && bajoSobra < 1f && bajoSobra >= 0.75f && bajoNada == 1f,
              "el bajo segun lo medido: igual si 63 Hz va bien, mas si falta (" + bajoFalta + "), menos si sobra (" + bajoSobra + "), igual sin medir");
        Check(son.Contains("bassSongFactor * BajoMedido") && son.Contains("* factor * BajoMedido"), "el bajo psicoacustico lo lleva al sonar y en la siguiente");

        PonStatic("localAuto", false);
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos (cuentas en Linux/Mono; nada oido en el billar)");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
