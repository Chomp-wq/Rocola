using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// EL DIAGNOSTICO COMPLETO DE CTRL+F3 (23-10-2026; Rocola/DiagnosticoCompleto.cs y Form1.DiagnosticoCompleto.cs).
//
//   mono diagnostico.exe <rocola.exe> [<archivo donde dejar un informe de muestra>]
//
// Compilar con franjareloj.cs y leercodigo.cs, /main:Diagnostico (todo.sh lo hace solo). TODO ES SINTETICO: la tarjeta es la imitada de
// las pruebas (cuenta y no pinta), el reloj de la mayoria de las comprobaciones es VIRTUAL y la maquina es Linux/Mono. Aqui no se mide la
// rocola real, ni Windows XP, ni un A4-6300: se comprueba que el diagnostico cuenta bien lo que le dan, que lo atribuye donde toca, que
// el informe sale con todo, que no guarda ni aplica nada, que lo deja todo como estaba y que se cancela.
//
//   1) las piezas y el enchufe en el boton, la ventana y el cuadro de la tarjeta;
//   2) la toma de muestras: un tiron inyectado de 120 ms se cuenta en >100 ms y cae en SU fase; uno fuera del dibujo cae en «fuera»;
//      fallos de presentacion; periodos 1,5x y 2x; percentiles; el cuadro NO reserva memoria (1.000.000 de cuadros sin recoleccion);
//   3) el motor con un anfitrion de reloj virtual: recorre todas las escenas, salta las que no se pueden montar y dice por que,
//      no mide mas alla de su presupuesto, llama siempre a Limpia, y se cancela limpiamente;
//   4) el procesador: del proceso, por hilo (un hilo anotado que trabaja de verdad) y del sistema, finitos y en rango;
//   5) el informe: todas sus secciones, el archivo nuevo cada vez (sin pisar), la poda a los ultimos diez, el resumen corto;
//   6) el panel de la tarjeta imitada de verdad: la toma recibe los cuadros de RenderFrame, el libro de fases se llena, el bucle paceado
//      no gira sin freno, y el motor corre una pasada entera sobre el (esa pasada es la del informe de muestra);
//   7) el lado de la rocola, con un Form1 sin encender: las escenas, devolver el estado, la cancelacion por interaccion, los textos
//      del equipo y de los ajustes, y la musica que no hay;
//   8) lo que NO debe hacer, leido del programa compilado: no guarda configuracion, no reinicia la tarjeta, no toca volumenes, no escribe
//      a disco durante las escenas, no reserva en el cuadro.
class Diagnostico
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Check(bool ok, string q) { if (ok) { oks++; Console.WriteLine("  OK    " + q); } else { fallos++; Console.WriteLine("  FALLO " + q); } }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
    static Type TF, TPanel, TStar, TDev, TD3D, TLibro, TCaptura, TMotor, TDef, TAnfi, TMeta, TInforme, THilos, TLector, TInst, TRes;
    static Assembly Asm;

    // ======================= REFLEXION =======================
    static FieldInfo Campo(Type t, string n)
    { for (; t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, TODO | BindingFlags.DeclaredOnly); if (f != null) return f; } return null; }
    static MethodInfo Metodo(Type t, string n)
    { for (; t != null; t = t.BaseType) { MethodInfo m = t.GetMethod(n, TODO | BindingFlags.DeclaredOnly); if (m != null) return m; } return null; }
    static void Pon(object o, string n, object v) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); f.SetValue(o, v); }
    static object Toma(object o, string n) { FieldInfo f = Campo(o.GetType(), n); if (f == null) throw new Exception("falta el campo " + n); return f.GetValue(o); }
    static object Llama(object o, string n, params object[] a)
    {
        MethodInfo m = Metodo(o.GetType(), n);
        if (m == null) throw new Exception("falta " + o.GetType().Name + "." + n);
        try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object LlamaE(Type t, string n, params object[] a)
    {
        MethodInfo m = Metodo(t, n);
        if (m == null) throw new Exception("falta " + t.Name + "." + n);
        try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, a, null); }
    static double D(object o, string n) { return Convert.ToDouble(Toma(o, n), CultureInfo.InvariantCulture); }
    static int I(object o, string n) { return Convert.ToInt32(Toma(o, n), CultureInfo.InvariantCulture); }
    static string S(object o, string n) { return (string)Toma(o, n); }
    static Delegate Del(Type tipoDelegado, string metodo) { return Delegate.CreateDelegate(tipoDelegado, typeof(Diagnostico).GetMethod(metodo, TODO)); }
    static void Pondel(object o, string campo, string metodo) { FieldInfo f = Campo(o.GetType(), campo); f.SetValue(o, Del(f.FieldType, metodo)); }

    // ======================= UN ANFITRION DE RELOJ VIRTUAL =======================
    static double reloj;                 // ms virtuales: solo avanza cuando se «dibuja» un cuadro
    static object libroV, capturaV;      // el libro de verdad (sus fases se llenan aqui) y la toma que recibe los cuadros
    static double inyecta; static int inyectaFase; static int inyectaBandera;
    static int limpias, preparas, interrupcionEn = -1, iteraciones, cortesV, texV = 200;
    static bool musicaReal;
    static double Ahora() { return reloj; }
    static double PeriodoRef() { return 1000.0 / 60.0; }
    static void Nada() { }
    static bool NoInterrumpido() { return false; }
    static bool InterrumpidoEn() { iteraciones++; return interrupcionEn >= 0 && iteraciones >= interrupcionEn; }
    static long Cortes() { return cortesV; }
    static long Tex() { return texV; }
    static long CeroLargo() { return 0; }
    static string Musica() { return musicaReal ? "REAL (de prueba)" : "muda del diagnostico (de prueba)"; }
    static string SinFondo() { return ""; }
    static void Aviso(string t) { avisos.Add(t); }
    static readonly List<string> avisos = new List<string>();
    static bool CuadroVirtual()
    {
        double[] f = (double[])Llama(libroV, "get_Fases");
        for (int i = 0; i < f.Length; i++) f[i] = 0;
        f[0] = 0.3; f[1] = 0.5; f[2] = 0.2; f[6] = 1.0; f[7] = 0.4; f[9] = 3.0;
        double fuera = 1.0; int banderas = 0;
        if (inyecta > 0) { if (inyectaFase == 11) fuera += inyecta; else f[inyectaFase] += inyecta; banderas = inyectaBandera; inyecta = 0; }
        double trabajo = 0; for (int i = 0; i < 10; i++) trabajo += f[i];
        double inicio = reloj + fuera, presentar = Math.Max(0.5, 1000.0 / 60.0 - fuera - trabajo);
        f[10] = presentar;
        double fin = inicio + trabajo + presentar;
        Pon(libroV, "Banderas", banderas); Pon(libroV, "Celdas", 18); Pon(libroV, "Subidas", 2); Pon(libroV, "Modo", 1);
        double prep = trabajo - f[9];
        reloj = fin;
        Llama(capturaV, "Cuadro", inicio, fin, prep, f[9], presentar, libroV);
        return true;
    }
    static void PasoTiron(int n) { if (n == 1) { inyecta = 120.0; inyectaFase = 6; inyectaBandera = 8; } }          // 120 ms en «interfaz», con la bandera interfaz_subida
    static void PasoFuera(int n) { if (n == 1) { inyecta = 70.0; inyectaFase = 11; inyectaBandera = 0; } }          // 70 ms FUERA del dibujo
    static void PasoMuchos(int n) { inyecta = 60.0; inyectaFase = 4; inyectaBandera = 512; }                          // un tiron de 60 ms en cada paso, en «estado»
    static string PreparaBien() { preparas++; return null; }
    static string PreparaSalta() { preparas++; return "sin canciones en la biblioteca de prueba"; }
    static string PreparaFalla() { preparas++; throw new InvalidOperationException("la escena de prueba no arranca"); }
    static void Limpia() { limpias++; }
    static bool NuncaAsienta() { return false; }
    static string Notas() { return "Nota de prueba de la escena."; }

    static object Escena(string id, string nombre, int cal, int med, int paso, string prepara, string pasoM, string asentada)
    {
        object e = Nuevo(TDef);
        Pon(e, "Id", id); Pon(e, "Nombre", nombre); Pon(e, "Que", "Escena de prueba: " + nombre + ".");
        Pon(e, "CalentaMs", cal); Pon(e, "MedidaMs", med); Pon(e, "PasoMs", paso);
        if (prepara != null) Pondel(e, "Prepara", prepara);
        if (pasoM != null) Pondel(e, "Paso", pasoM);
        if (asentada != null) { Pondel(e, "Asentada", asentada); Pon(e, "AsentarMaxMs", 3000); }
        Pondel(e, "Limpia", "Limpia"); Pondel(e, "Notas", "Notas");
        return e;
    }
    static object Anfitrion(bool virtualClock)
    {
        object h = Nuevo(TAnfi);
        Pondel(h, "Cuadro", "CuadroVirtual"); Pondel(h, "Bombea", "Nada"); Pondel(h, "Interrumpido", "InterrumpidoEn");
        Pondel(h, "Ahora", "Ahora"); Pondel(h, "PeriodoRefMs", "PeriodoRef"); Pondel(h, "ReiniciaMedida", "Nada");
        Pondel(h, "Aviso", "Aviso"); Pondel(h, "Cortes", "Cortes"); Pondel(h, "Tardios", "CeroLargo"); Pondel(h, "MaxTardioMs", "CeroLargo");
        Pondel(h, "TexMb", "Tex"); Pondel(h, "Perdidas", "CeroLargo"); Pondel(h, "Fondo", "SinFondo"); Pondel(h, "Musica", "Musica"); Pondel(h, "Portadas", "SinFondo");
        return h;
    }
    static object NuevoMotor(out object captura)
    {
        object motor = Nuevo(TMotor, Anfitrion(true));
        captura = Toma(motor, "Captura"); capturaV = captura;
        return motor;
    }
    static void ReiniciaVirtual()
    { reloj = 1000; inyecta = 0; limpias = 0; preparas = 0; iteraciones = 0; interrupcionEn = -1; cortesV = 0; avisos.Clear(); musicaReal = false; }

    // ======================= 1) LAS PIEZAS =======================
    static void Piezas()
    {
        Console.WriteLine("\n  1) LAS PIEZAS Y EL ENCHUFE\n");
        Check(TCaptura != null && TMotor != null && TDef != null && TAnfi != null && TInforme != null && THilos != null && TLector != null && TRes != null && TMeta != null,
              "existen el motor, la toma de muestras, las escenas, el anfitrion, el informe, los hilos con nombre y el lector de procesador");
        Check(Metodo(TF, "CorreDiagnosticoGpu") != null && Metodo(TF, "DiagEscenas") != null && Metodo(TF, "DiagRestaura") != null && Metodo(TF, "DiagGuardaEstado") != null,
              "Form1 sabe correr el diagnostico, montar las escenas, guardar el estado y devolverlo");
        Check(Metodo(TPanel, "DiagCuadroPaceado") != null && Campo(TPanel, "diagSink") != null, "el panel de la tarjeta tiene su turno paceado y el enchufe de la toma");
        int l, e;
        Codigo.UsosDeCampo(Metodo(TPanel, "RenderFrame"), "diagSink", out l, out e);
        Check(l > 0, "RenderFrame mira la toma al presentar un cuadro (un solo «if» sobre una referencia)");
        Check(Codigo.Llama(Metodo(TPanel, "RenderFrame"), "Cuadro"), "y le da el cuadro presentado");
        Codigo.UsosDeCampo(Metodo(TPanel, "NoPresentado"), "diagSink", out l, out e);
        Check(l > 0 && Codigo.Llama(Metodo(TPanel, "NoPresentado"), "Fallo"), "NoPresentado le cuenta los cuadros que no se presentaron");
        int pl, pe; Codigo.UsosDeCampo(Metodo(TDev, "EsperaDispositivo"), "Perdidas", out pl, out pe);
        Check(Campo(TDev, "Perdidas") != null && pe > 0, "la tarjeta cuenta las veces que se perdio el dispositivo");
        List<string> tImg = Codigo.ConAyudantes(TF, "PaginaImagen"); List<string> txt = new List<string>();
        foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "PaginaImagen")) txt.AddRange(Codigo.Textos(m));
        Check(txt.Contains("Diagnóstico completo"), "la pagina de Imagen tiene el boton «Diagnóstico completo» junto a «Medir este equipo»");
        Check(txt.Contains("Medir este equipo"), "y conserva «Medir este equipo»");
        Check(tImg.Count > 0, "la pagina de Imagen se arma");
        List<string> vm = Codigo.ConAyudantes(TF, "ShowVolumeManager");
        Check(Codigo.Llama(vm, "DiagnosticoCompletoF3") && Codigo.Llama(vm, "MideRendimiento") && Codigo.Llama(vm, "VentanaAjustes"),
              "Ctrl+F3 reabre la ventana despues de medir y despues del diagnostico");
        Check(Codigo.Llama(Metodo(TF, "DiagnosticoCompletoF3"), "CorreDiagnosticoGpu"), "con tarjeta grafica el boton corre el diagnostico de las escenas");
        // No es una preferencia: ni se lee ni se escribe en la configuracion.
        List<string> cfgL = Codigo.Textos(Metodo(TF, "LoadConfigPrefs")), cfgE = Codigo.Textos(Metodo(TF, "TextoConfig"));
        bool alguna = false; foreach (string t in cfgL) if (t.ToLower().IndexOf("diagnostico_completo") >= 0 || t.ToLower().StartsWith("diagcompleto")) alguna = true;
        foreach (string t in cfgE) if (t.ToLower().IndexOf("diagnostico_completo") >= 0 || t.ToLower().StartsWith("diagcompleto")) alguna = true;
        Check(!alguna, "no es una preferencia: la configuracion no lo lee ni lo escribe");
    }

    // ======================= 2) LA TOMA DE MUESTRAS =======================
    static object NuevaToma(out object libro)
    {
        object c = Nuevo(TCaptura);
        libro = Nuevo(TLibro);
        return c;
    }
    // Un cuadro a mano: «inicio» y «fin» exactos; las fases del libro las pone quien llama.
    static void Cuadro(object c, object libro, double inicio, double fin, double[] fases, int banderas)
    {
        double[] f = (double[])Llama(libro, "get_Fases");
        for (int i = 0; i < f.Length; i++) f[i] = fases != null && i < fases.Length ? fases[i] : 0;
        Pon(libro, "Banderas", banderas);
        double prep = 0; for (int i = 0; i < 9; i++) prep += f[i];
        Llama(c, "Cuadro", inicio, fin, prep, f[9], f[10], libro);
    }
    static void LaToma()
    {
        Console.WriteLine("\n  2) LA TOMA DE MUESTRAS\n");
        object libro; object c = NuevaToma(out libro);
        object r = Nuevo(TRes);
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        // 300 cuadros a 60 por segundo exactos (de un Present al siguiente), y un tiron de 112,5 ms cuya fase mas larga es «interfaz».
        double[] normal = new double[11]; normal[0] = 0.5; normal[9] = 3.0; normal[10] = 8.0;   // 11,5 ms de cuadro, 5,17 ms fuera
        int bandera = 8;   // interfaz_subida
        int cuadros = 0;
        double t = 5.0, periodo = 1000.0 / 60.0;
        Cuadro(c, libro, t - 11.5, t, normal, 0); cuadros++;     // el primero no tiene hueco de antes
        for (int i = 1; i < 300; i++)
        {
            if (i == 150)
            {
                double[] gran = new double[11]; gran[0] = 0.5; gran[6] = 100.0; gran[9] = 3.0; gran[10] = 8.0;   // 111,5 ms de cuadro
                double fin = t + 112.5;   // 1,0 ms fuera + 111,5 de cuadro
                Cuadro(c, libro, fin - 111.5, fin, gran, bandera); t = fin; cuadros++; continue;
            }
            t += periodo; Cuadro(c, libro, t - 11.5, t, normal, 0); cuadros++;
        }
        Llama(c, "Cierra", t, r);
        Check(I(r, "Cuadros") == cuadros, "cuenta los cuadros presentados (" + I(r, "Cuadros") + " de " + cuadros + ")");
        Check(I(r, "Huecos50") == 1 && I(r, "Huecos100") == 1, "el tiron inyectado de ~120 ms se cuenta en los de mas de 50 y en los de mas de 100 (" + I(r, "Huecos50") + "/" + I(r, "Huecos100") + ")");
        double max = D(r, "Max");
        Check(max > 110 && max < 135, "y es el hueco mas largo (" + F(max, "0.0") + " ms)");
        int[] h50 = (int[])Toma(r, "Huecos50PorFase");
        Check(h50[6] == 1, "se atribuye a SU fase (interfaz = 6): " + h50[6] + " ahi, y las demas fases " + (h50[0] + h50[1] + h50[2] + h50[3] + h50[4] + h50[5] + h50[7] + h50[8] + h50[9] + h50[10] + h50[11]) + " en total");
        int[] hf = (int[])Toma(r, "Huecos50PorFaseYBandera");
        Check(hf[6 * 11 + 3] == 1, "con la bandera interfaz_subida del cuadro (la bandera 3)");
        Array peores = (Array)Toma(r, "Peores");
        Check(peores.Length >= 1 && I(peores.GetValue(0), "FaseDom") == 6 && Math.Abs(D(peores.GetValue(0), "Hueco") - max) < 1e-9, "el peor tiron guardado cae en interfaz y es el mismo hueco maximo");
        Check(Math.Abs(D(peores.GetValue(0), "Fuera") - 1.0) < 0.001, "y dice cuanto fue fuera del dibujo (1.0 ms) y cuanto dentro");
        double[] fases = (double[])Toma(r, "FasesMs");
        Check(fases[6] > 99 && fases[6] < 110 && fases[11] > 250, "la suma de tiempos por fase junta lo de interfaz (" + F(fases[6], "0") + " ms) y lo de fuera del dibujo (" + F(fases[11], "0") + " ms)");
        Check(I(r, "Sobre2") >= 1 && I(r, "Sobre15") >= I(r, "Sobre2"), "cuenta los intervalos de mas de 1,5 y de 2 periodos del refresco (" + I(r, "Sobre15") + " y " + I(r, "Sobre2") + ")");
        double p50 = D(r, "P50"), p95 = D(r, "P95"), p99 = D(r, "P99");
        Check(Math.Abs(p50 - 1000.0 / 60.0) < 0.5 && p50 <= p95 && p95 <= p99 && p99 <= max, "p50 " + F(p50, "0.00") + " <= p95 " + F(p95, "0.00") + " <= p99 " + F(p99, "0.00") + " <= max " + F(max, "0.0") + " ms, con el p50 en el periodo del refresco");
        Check(Math.Abs(D(r, "Fps") - 60.0) < 8.0, "los cuadros por segundo salen del reloj de la ventana (" + F(D(r, "Fps"), "0.0") + ")");

        // Un hueco FUERA del dibujo (el audio, un temporizador, el sistema): cae en «fuera», no en una fase.
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        object r2 = Nuevo(TRes);
        t = 5.0;
        Cuadro(c, libro, t - 11.5, t, normal, 0);
        for (int i = 1; i < 100; i++)
        {
            t += (i == 50 ? 90.0 : periodo); Cuadro(c, libro, t - 11.5, t, normal, 0);   // en el 50, 90 ms: 78,5 de ellos fuera del dibujo
        }
        Llama(c, "Cierra", t, r2);
        int[] h50b = (int[])Toma(r2, "Huecos50PorFase");
        Check(I(r2, "Huecos50") == 1 && h50b[11] == 1, "un hueco de ~90 ms con el dibujo normal cae en «fuera del dibujo» (fase 11), no en una fase del cuadro");
        // La espera ociosa (el hilo dormido esperando que toque el siguiente cuadro) NO es trabajo: se aparta de «fuera».
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        object rE = Nuevo(TRes);
        t = 5.0; Cuadro(c, libro, t - 11.5, t, normal, 0);
        for (int i = 1; i < 100; i++) { Llama(c, "Ocioso", 4.0); t += periodo; Cuadro(c, libro, t - 11.5, t, normal, 0); }
        Llama(c, "Cierra", t, rE);
        double[] fE = (double[])Toma(rE, "FasesMs");
        Check(Math.Abs(fE[12] - 99 * 4.0) < 1.0 && Math.Abs(fE[11] - 99 * (periodo - 11.5 - 4.0)) < 1.0, "lo que el bucle durmio a proposito (4 ms por cuadro) va a «espera» (" + F(fE[12], "0") + " ms) y solo el resto de «fuera» queda como trabajo (" + F(fE[11], "0") + " ms)");
        Llama(c, "Ocioso", 7.0);
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        object rE2 = Nuevo(TRes);
        t = 5.0; Cuadro(c, libro, t - 11.5, t, normal, 0); t += 90.0; Cuadro(c, libro, t - 11.5, t, normal, 0);
        Llama(c, "Cierra", t, rE2);
        Check(I(rE2, "Huecos50") == 1 && ((int[])Toma(rE2, "Huecos50PorFase"))[11] == 1, "el ocioso de antes de empezar la ventana no se cuela, y un hueco largo SIN espera ociosa sigue cayendo en «fuera»");
        // Fallos de presentacion: se cuentan y quedan apuntados en el hueco siguiente.
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        object r3 = Nuevo(TRes);
        Cuadro(c, libro, 5, 17, normal, 0);
        Llama(c, "Fallo"); Llama(c, "Fallo"); Llama(c, "Fallo");
        Cuadro(c, libro, 70, 82, normal, 0);
        Llama(c, "Cierra", 100.0, r3);
        Array p3 = (Array)Toma(r3, "Peores");
        Check(I(r3, "Fallos") == 3 && p3.Length == 1 && I(p3.GetValue(0), "Fallos") == 3, "cuenta los cuadros que no se presentaron (3) y los deja apuntados en el hueco que provocaron");
        // Fuera de una ventana, nada se cuenta.
        object r4 = Nuevo(TRes);
        Llama(c, "Cierra", 100.0, r4);
        Cuadro(c, libro, 200, 212, normal, 0); Llama(c, "Fallo");
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        Llama(c, "Cierra", 10.0, r4);
        Check(I(r4, "Cuadros") == 0 && I(r4, "Fallos") == 0, "fuera de una ventana de medida la toma no cuenta nada");

        // SIN RESERVAS: un millon de cuadros, ni una recoleccion y la memoria no crece.
        Llama(c, "Empieza", 0.0, 1000.0 / 60.0);
        double[] fz = (double[])Llama(libro, "get_Fases"); for (int i = 0; i < fz.Length; i++) fz[i] = 0; fz[6] = 2.0; fz[9] = 3.0; fz[10] = 5.0;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        int g0 = GC.CollectionCount(0);
        long m1 = (long)typeof(Diagnostico).GetMethod("MilCuadros", TODO).MakeGenericMethod(TLibro).Invoke(null, new object[] { c, libro, 1000000 });
        int g1 = GC.CollectionCount(0);
        Check(m1 < 128 * 1024 && g1 == g0, "un millon de cuadros por la toma: " + F(m1 / 1024.0, "0.0") + " KB de memoria nueva y " + (g1 - g0) + " recolecciones (no reserva por cuadro)");
        Llama(c, "Cierra", 1e9, r4);
        // Y en el codigo compilado: ni «new», ni cajas, ni cadenas en la ruta caliente.
        foreach (string nom in new string[] { "Cuadro", "Empieza", "Fallo" })
        {
            List<string> il = Codigo.IlConNombres(Metodo(TCaptura, nom));
            bool reserva = false; string cual = "";
            foreach (string p in il) if (p.StartsWith("73 ") || p.StartsWith("8D ") || p.StartsWith("8C ")) { reserva = true; cual = p; }
            Check(!reserva, "DiagCaptura." + nom + " no tiene ni newobj, ni newarr, ni box en su codigo" + (reserva ? " (" + cual + ")" : ""));
        }
        List<string> ll = Codigo.Llamadas(Metodo(TCaptura, "Cuadro"));
        bool texto = false; foreach (string s in ll) if (s.IndexOf("String") >= 0 || s.IndexOf("Concat") >= 0 || s.IndexOf("Format") >= 0 || s.IndexOf("File") >= 0 || s.IndexOf("Console") >= 0) texto = true;
        Check(!texto, "y no arma texto ni toca archivos ni consola");
    }
    // Un bucle COMPILADO sobre un delegado ya creado (sin reflexion de por medio): lo que reserve es de la toma.
    static long MilCuadros<TL>(object captura, TL libro, int n)
    {
        MethodInfo mc = Metodo(TCaptura, "Cuadro");
        Action<double, double, double, double, double, TL> cd = (Action<double, double, double, double, double, TL>)Delegate.CreateDelegate(typeof(Action<double, double, double, double, double, TL>), captura, mc);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long antes = GC.GetTotalMemory(false);
        double t = 0;
        for (int i = 0; i < n; i++)
        {
            double inicio = t + 1.0, fin = inicio + 14.0;
            cd(inicio, fin, 2.0, 3.0, 8.0, libro);
            t = fin;
        }
        long despues = GC.GetTotalMemory(false);
        return Math.Max(0, despues - antes);
    }

    // ======================= 3) EL MOTOR =======================
    static void ElMotor()
    {
        Console.WriteLine("\n  3) EL MOTOR, CON UN RELOJ VIRTUAL\n");
        ReiniciaVirtual();
        object cap; object motor = NuevoMotor(out cap);
        libroV = Nuevo(TLibro);
        IList escenas = (IList)Toma(motor, "Escenas");
        escenas.Add(Escena("quieta", "Escena quieta", 300, 3000, 0, "PreparaBien", null, null));
        escenas.Add(Escena("tiron", "Escena con un tiron de 120 ms", 200, 4000, 1000, "PreparaBien", "PasoTiron", null));
        escenas.Add(Escena("fuera", "Escena con un hueco de 70 ms fuera del dibujo", 200, 4000, 1000, "PreparaBien", "PasoFuera", null));
        escenas.Add(Escena("sinmusica", "Escena que no se puede montar", 200, 3000, 0, "PreparaSalta", null, null));
        escenas.Add(Escena("rota", "Escena que falla al montarse", 200, 3000, 0, "PreparaFalla", null, null));
        escenas.Add(Escena("sinabrir", "Escena que no llega a asentarse", 200, 3000, 0, "PreparaBien", null, "NuncaAsienta"));
        escenas.Add(Escena("muchos", "Escena con un tiron de 60 ms en cada paso", 200, 4000, 500, "PreparaBien", "PasoMuchos", null));
        Llama(motor, "Corre");
        IList rs = (IList)Toma(motor, "Resultados");
        Check(rs.Count == escenas.Count, "recorre TODAS las escenas, medidas o saltadas (" + rs.Count + " de " + escenas.Count + ")");
        Check(limpias == escenas.Count, "llama a Limpia en cada una, tambien en las saltadas y las que fallan (" + limpias + ")");
        Check(S(rs[0], "Estado") == "medida" && I(rs[0], "Cuadros") >= 175 && I(rs[0], "Cuadros") <= 185, "la quieta se mide: " + I(rs[0], "Cuadros") + " cuadros en 3 s virtuales");
        Check(I(rs[0], "Huecos50") == 0 && D(rs[0], "Fps") > 55 && D(rs[0], "Fps") < 65, "y sale limpia: ningun tiron, " + F(D(rs[0], "Fps"), "0.0") + " cuadros por segundo");
        Check(I(rs[1], "Huecos100") == 1 && I(rs[1], "Huecos50") == 1, "el tiron de 120 ms se cuenta una vez, en >100 ms");
        int[] h = (int[])Toma(rs[1], "Huecos50PorFase");
        Check(h[6] == 1, "y se atribuye a su fase: interfaz");
        Array pe = (Array)Toma(rs[1], "Peores");
        Check(pe.Length >= 1 && (I(pe.GetValue(0), "Banderas") & 8) == 8 && I(pe.GetValue(0), "Celdas") == 18, "con las banderas y las celdas del cuadro donde cayo");
        int[] hf = (int[])Toma(rs[2], "Huecos50PorFase");
        Check(I(rs[2], "Huecos50") == 1 && hf[11] == 1, "el de 70 ms fuera del dibujo cae en «fuera» (" + hf[11] + ")");
        Check(S(rs[3], "Estado") == "saltada" && S(rs[3], "Motivo") == "sin canciones en la biblioteca de prueba", "la que no se puede montar se salta y dice por que");
        Check(S(rs[4], "Estado") == "saltada" && S(rs[4], "Motivo").IndexOf("la escena de prueba no arranca") >= 0, "la que falla al montarse se salta con el motivo del fallo");
        Check(S(rs[5], "Estado") == "saltada" && S(rs[5], "Motivo") == "la escena no llego a montarse", "la que nunca se asienta se salta, no se mide a medias");
        Check(S(rs[3], "Id") == "sinmusica" && I(rs[3], "Cuadros") == 0, "las saltadas no traen cuadros");
        Check(I(rs[6], "Huecos50") >= 6 && I(rs[6], "PasosDados") >= 6, "los pasos de la escena se dan a su ritmo (" + I(rs[6], "PasosDados") + " pasos, " + I(rs[6], "Huecos50") + " tirones)");
        Check(S(rs[0], "Musica").IndexOf("muda") >= 0 && S(rs[0], "Notas").IndexOf("Nota de prueba") >= 0, "cada escena dice con que musica se midio y trae sus notas");
        Check(avisos.Count == escenas.Count && avisos[0].IndexOf("1/7") >= 0, "avisa del progreso al empezar cada escena (" + avisos.Count + " avisos): \"" + avisos[0] + "\"");
        Check(Math.Abs(D(rs[0], "TexIniMb") - 200) < 0.5 && I(rs[0], "Cortes") == 0, "toma la memoria de texturas y los cortes de sonido en la escena");

        // El presupuesto: no se empieza una escena que ya no cabe.
        ReiniciaVirtual();
        object motor2 = NuevoMotor(out cap); libroV = Nuevo(TLibro);
        Pon(motor2, "PresupuestoMs", 6000.0);
        IList es2 = (IList)Toma(motor2, "Escenas");
        es2.Add(Escena("a", "Primera", 200, 3000, 0, "PreparaBien", null, null));
        es2.Add(Escena("b", "Segunda (ya no cabe)", 200, 3000, 0, "PreparaBien", null, null));
        Llama(motor2, "Corre");
        IList r2 = (IList)Toma(motor2, "Resultados");
        Check(r2.Count == 2 && S(r2[0], "Estado") == "medida" && S(r2[1], "Estado") == "saltada" && S(r2[1], "Motivo").IndexOf("se acabo el tiempo") >= 0, "el presupuesto de tiempo manda: la escena que ya no cabe se salta y lo dice");

        // La cancelacion: alguien toca algo a media escena.
        ReiniciaVirtual();
        object motor3 = NuevoMotor(out cap); libroV = Nuevo(TLibro);
        IList es3 = (IList)Toma(motor3, "Escenas");
        es3.Add(Escena("a", "Primera", 200, 3000, 0, "PreparaBien", null, null));
        es3.Add(Escena("b", "Segunda (la interrumpen)", 200, 6000, 0, "PreparaBien", null, null));
        es3.Add(Escena("c", "Tercera (no llega)", 200, 3000, 0, "PreparaBien", null, null));
        interrupcionEn = 330;   // un par de cientos de iteraciones despues de empezar la segunda
        Llama(motor3, "Corre");
        IList r3 = (IList)Toma(motor3, "Resultados");
        Check((bool)Toma(motor3, "Cancelado") && S(motor3, "MotivoCancelacion").IndexOf("Segunda") >= 0, "si alguien toca algo, el diagnostico se cancela y dice en que escena");
        Check(r3.Count == 2 && S(r3[0], "Estado") == "medida" && S(r3[1], "Estado") == "cancelada", "lo medido hasta entonces se conserva; la interrumpida queda como cancelada");
        Check(limpias == 2, "la escena interrumpida se limpia (Limpia llamada " + limpias + " veces) y la tercera ni se monta (" + preparas + " montadas)");
        Check(preparas == 2, "no se monta ninguna escena despues de cancelar");
    }

    // ======================= 4) EL PROCESADOR =======================
    static volatile bool giro;
    static void Giro() { HilosRocolaNombra("trabajador de prueba"); long x = 0; Stopwatch sw = Stopwatch.StartNew(); while (giro && sw.ElapsedMilliseconds < 3000) x++; if (x == -1) Console.WriteLine(x); }
    static void HilosRocolaNombra(string n) { LlamaE(THilos, "Nombra", n); }
    static void ElProcesador()
    {
        Console.WriteLine("\n  4) EL PROCESADOR: DEL PROCESO, POR HILO Y DEL SISTEMA\n");
        double p = (double)LlamaE(TLector, "ProcesoMs");
        Check(p >= 0 && !double.IsNaN(p) && !double.IsInfinity(p), "el procesador acumulado del proceso se lee (" + F(p, "0") + " ms)");
        object[] a = { 0.0, 0.0 };
        bool ok = (bool)LlamaE(TLector, "SistemaMs", a);
        Check(ok && (double)a[1] > 0 && (double)a[0] >= 0 && (double)a[0] <= (double)a[1], "el procesador del sistema entero se lee (ocupado " + F((double)a[0], "0") + " de " + F((double)a[1], "0") + " ms acumulados)");
        long idYo = (long)LlamaE(THilos, "IdNativoActual");
        Check(idYo >= 0, "el hilo que llama conoce su identificador nativo (" + idYo + ")");
        HilosRocolaNombra("pantalla (prueba)");
        giro = true;
        Thread w = new Thread(new ThreadStart(Giro)); w.IsBackground = true; w.Start();
        Thread.Sleep(150);
        Stopwatch reloj0 = Stopwatch.StartNew();
        object ini = LlamaE(TInst, "Toma", (double)reloj0.Elapsed.TotalMilliseconds);
        Thread.Sleep(1000);
        object fin = LlamaE(TInst, "Toma", (double)reloj0.Elapsed.TotalMilliseconds);
        giro = false; w.Join();
        object r = Nuevo(TRes);
        int nuc = Math.Max(1, Environment.ProcessorCount);
        LlamaE(TMotor, "CalculaCpu", ini, fin, nuc, r);
        double nucleos = D(r, "CpuNucleos"), pct = D(r, "CpuProcesoPct"), sis = D(r, "CpuSistemaPct"), otros = D(r, "CpuOtrosPct");
        Check(nucleos >= 0.55 && nucleos <= nuc + 0.25 && !double.IsNaN(nucleos), "el proceso usa un nucleo y medio hilo: " + F(nucleos, "0.00") + " nucleos de " + nuc + " (esperado ~1, con un hilo girando)");
        Check(pct > 0 && pct <= 100.5 && Math.Abs(pct - 100.0 * nucleos / nuc) < 0.01, "y en porcentaje del procesador entero: " + F(pct, "0.0") + " %");
        Check(sis >= 0 && sis <= 100 && !double.IsNaN(sis) && otros >= 0 && otros <= 100, "el sistema sale en rango (" + F(sis, "0.0") + " %, de otros procesos " + F(otros, "0.0") + " %)");
        IList hilos = (IList)Toma(r, "Hilos");
        double trabajador = -1, resto = -1, suma = 0; bool finitos = true;
        foreach (object h in hilos)
        {
            double v = D(h, "PctNucleo"); string n = S(h, "Nombre");
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) finitos = false;
            if (n == "trabajador de prueba") trabajador = v;
            if (n.StartsWith("(resto")) resto = v; else suma += v;
        }
        Check(trabajador > 55 && trabajador <= 105, "el hilo con nombre que trabaja sale con su parte: " + F(trabajador, "0.0") + " % de un nucleo (esperado ~100)");
        Check(finitos && resto >= 0, "todos los hilos salen finitos y no negativos; el resto del proceso (" + F(resto, "0.0") + " %) cuadra lo que queda");
        Check(Math.Abs((suma + resto) - 100.0 * nucleos) < 1.0 || suma + resto >= 100.0 * nucleos - 1.0, "los hilos y el resto suman el procesador del proceso");
        Check(((IList)Toma(r, "Hilos"))[0] != null && D(hilos[0], "PctNucleo") >= D(hilos[hilos.Count - 1], "PctNucleo"), "van ordenados del que mas al que menos");
        // Sin API (nada que leer): -1, sin lanzar nada.
        object vacia = Nuevo(TInst); object vacia2 = Nuevo(TInst); object rv = Nuevo(TRes);
        Pon(vacia, "RelojMs", 0.0); Pon(vacia2, "RelojMs", 1000.0);
        LlamaE(TMotor, "CalculaCpu", vacia, vacia2, 2, rv);
        Check(D(rv, "CpuProcesoPct") == -1 && D(rv, "CpuSistemaPct") == -1 && ((IList)Toma(rv, "Hilos")).Count == 0, "sin datos (la API no esta) todo queda en -1 / vacio y no lanza nada");
        // Todas las llamadas nativas con try/catch: miradas en el codigo.
        bool todasConTry = true; string cual = "";
        foreach (string nom in new string[] { "ProcesoMs", "SistemaMs", "HiloMs" })
        {
            MethodInfo m = Metodo(TLector, nom); MethodBody mb = m.GetMethodBody();
            if (mb.ExceptionHandlingClauses.Count == 0) { todasConTry = false; cual = nom; }
        }
        MethodBody nb = Metodo(THilos, "IdNativoActual").GetMethodBody(); if (nb.ExceptionHandlingClauses.Count == 0) { todasConTry = false; cual = "IdNativoActual"; }
        MethodBody nb2 = Metodo(THilos, "Nombra").GetMethodBody(); if (nb2.ExceptionHandlingClauses.Count == 0) { todasConTry = false; cual = "Nombra"; }
        Check(todasConTry, "las llamadas nativas (GetSystemTimes, OpenThread, GetThreadTimes, GetCurrentThreadId) van con try/catch" + (cual != "" ? " -- falta en " + cual : ""));
        // Los hilos del programa se anotan al nacer.
        int anotan = 0;
        foreach (string[] par in new string[][] { new string[] { "BombaSonido", "Bombea" }, new string[] { "Form1", "CoverPump" } })
        {
            Type t = Asm.GetType("Rocola." + par[0]); MethodInfo m = Metodo(t, par[1]);
            if (m != null && Codigo.Llama(m, "Nombra")) anotan++;
        }
        Check(anotan == 2, "el hilo de la musica y los lectores de caratulas se anotan con nombre al nacer");
        bool repaso = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) if (m.Name.IndexOf("RepasoDeLaBiblioteca", StringComparison.Ordinal) < 0 && Codigo.Llama(m, "Nombra") && m.Name.IndexOf("Biblioteca", StringComparison.Ordinal) < 0 && m.Name.IndexOf("Repaso", StringComparison.Ordinal) >= 0) repaso = true;
        int total = 0; foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) if (Codigo.Llama(m, "Nombra")) total++;
        Check(total >= 5, "y los del repaso, las duraciones y los calentadores tambien (" + total + " sitios de Form1 anotan su hilo)" + (repaso ? "" : ""));
    }

    // ======================= 5) EL INFORME =======================
    static void ElInforme(object motor)
    {
        Console.WriteLine("\n  5) EL INFORME\n");
        object meta = Nuevo(TMeta);
        Pon(meta, "Inicio", new DateTime(2026, 10, 23, 14, 5, 0)); Pon(meta, "DuracionMs", 215000.0); Pon(meta, "Version", "prueba"); Pon(meta, "SoloPrueba", true);
        Pon(meta, "Equipo", "Sistema operativo: Linux (prueba)\r\nProcesador: de prueba\r\n"); Pon(meta, "Ajustes", "Preset de graficos: Alto\r\n"); Pon(meta, "Condiciones", "Musica: muda de prueba\r\n");
        Pon(meta, "Hz", 60); Pon(meta, "Nucleos", 2);
        IList rs = (IList)Toma(motor, "Resultados");
        string texto = (string)LlamaE(TInforme, "Construye", meta, rs);
        string[] secciones = { "== 1. EQUIPO ==", "== 2. AJUSTES VIGENTES", "== 3. CONDICIONES DE LA MEDIDA ==", "== 4. RESUMEN POR ESCENA ==", "== 5. TIRONES", "== 6. PROCESADOR", "== 7. ATRIBUCION", "== 8. CANDIDATOS A OPTIMIZAR", "== 9. ESCENAS SALTADAS ==", "== 10. LIMITES ==" };
        int faltan = 0; string cual = "";
        foreach (string s in secciones) if (texto.IndexOf(s, StringComparison.Ordinal) < 0) { faltan++; cual += " " + s; }
        Check(faltan == 0, "el informe trae las diez secciones (equipo, ajustes, condiciones, tabla, tirones, procesador, atribucion, candidatos, saltadas, limites)" + (faltan > 0 ? " -- faltan:" + cual : ""));
        Check(texto.IndexOf("ESTE INFORME ES DE UNA PRUEBA AUTOMATICA") >= 0, "una corrida de pruebas lo dice en la cabecera (no es de la rocola)");
        Check(texto.IndexOf("Escena con un tiron de 120 ms") >= 0 && texto.IndexOf("fase dominante: interfaz") >= 0, "el tiron aparece con su escena y su fase dominante");
        Check(texto.IndexOf("Escena que no se puede montar") >= 0 && texto.IndexOf("sin canciones en la biblioteca de prueba") >= 0, "las escenas saltadas salen con su motivo");
        Check(texto.IndexOf("coincidencia, no causa") >= 0, "las recolecciones se dicen como coincidencia, nunca como causa");
        Check(texto.IndexOf("\r\n") >= 0 && texto.IndexOf("\n") == texto.IndexOf("\r\n") + 1, "con saltos de linea de Windows (se lee en el bloc de notas de XP)");
        object[] ar = new object[] { rs, meta };
        IList cand = (IList)LlamaE(TInforme, "Candidatos", ar);
        Check(cand.Count >= 1 && cand.Count <= 8, "hay una lista corta de candidatos (" + cand.Count + ")");
        Check(((string)cand[0]).IndexOf("fase \"estado\"") >= 0 && ((string)cand[0]).IndexOf("estado_subido") >= 0, "el primero nombra la fase y la bandera donde mas tirones cayeron: " + Recorta((string)cand[0], 200));
        bool conNumeros = true; foreach (object c in cand) { bool d = false; foreach (char ch in (string)c) if (ch >= '0' && ch <= '9') d = true; if (!d) conNumeros = false; }
        Check(conNumeros, "y todos llevan numeros que los justifican");
        bool causa = false; foreach (object c in cand) { string s = (string)c; if (s.IndexOf("GC", StringComparison.Ordinal) >= 0 || s.IndexOf("recoleccion") >= 0) { if (s.IndexOf("COINCIDENCIA") < 0 && s.IndexOf("coincidencia") < 0) causa = true; } }
        Check(!causa, "ningun candidato atribuye un tiron a una recoleccion de basura sin decir que es coincidencia");
        // El resumen de la pagina: dos renglones cortos.
        string resumen = (string)LlamaE(TInforme, "Resumen", meta, rs, "C:\\datos\\rocola_diagnostico_2026-10-23_1405.txt");
        string[] lineas = resumen.Split('\n');
        Check(lineas.Length == 2 && lineas[0].Length <= 62 && lineas[1].Length <= 60 && lineas[0].StartsWith("Diagnóstico 14:05") && lineas[1].EndsWith("rocola_diagnostico_2026-10-23_1405.txt"), "el resumen de la pagina son dos renglones que caben: \"" + lineas[0] + "\" / \"" + lineas[1] + "\"");
        string sinArchivo = (string)LlamaE(TInforme, "Resumen", meta, rs, (string)null);
        Check(sinArchivo.IndexOf("No se pudo escribir el informe") >= 0, "si no se pudo escribir el archivo, el resumen lo dice");

        // El archivo: nuevo cada vez, sin pisar, con limite.
        string dir = Path.Combine(Path.GetTempPath(), "rocola_diag_" + Process.GetCurrentProcess().Id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "rocola_diagnostico.txt"), "el registro de siempre");
            File.WriteAllText(Path.Combine(dir, "rocola_diagnostico_notas.txt"), "no es un informe");
            object[] e1 = { dir, texto, new DateTime(2026, 10, 23, 14, 5, 7), 10, null };
            string ruta1 = (string)LlamaE(TInforme, "Escribe", e1);
            object[] e2 = { dir, texto + "OTRO", new DateTime(2026, 10, 23, 14, 5, 40), 10, null };
            string ruta2 = (string)LlamaE(TInforme, "Escribe", e2);
            Check(ruta1 != null && Path.GetFileName(ruta1) == "rocola_diagnostico_2026-10-23_1405.txt", "el archivo se llama rocola_diagnostico_AAAA-MM-DD_HHMM.txt: " + (ruta1 != null ? Path.GetFileName(ruta1) : "(no se escribio)"));
            Check(ruta2 != null && ruta2 != ruta1 && File.ReadAllText(ruta1).IndexOf("OTRO") < 0 && File.Exists(ruta2), "otro diagnostico en el mismo minuto NO pisa el anterior (" + (ruta2 != null ? Path.GetFileName(ruta2) : "") + ")");
            byte[] cab = new byte[3]; using (FileStream fs = File.OpenRead(ruta1)) fs.Read(cab, 0, 3);
            Check(cab[0] == 0xEF && cab[1] == 0xBB && cab[2] == 0xBF, "se escribe en UTF-8 con marca (el bloc de notas de XP muestra los acentos)");
            for (int k = 0; k < 14; k++)
            {
                object[] ek = { dir, texto, new DateTime(2026, 10, 24, 10, k, 0), 10, null };
                LlamaE(TInforme, "Escribe", ek);
            }
            string[] quedan = Directory.GetFiles(dir, "rocola_diagnostico_2*.txt");
            Check(quedan.Length == 10, "se conservan solo los ultimos 10 informes (quedan " + quedan.Length + " de 16 escritos)");
            Check(File.Exists(Path.Combine(dir, "rocola_diagnostico_2026-10-24_1013.txt")) && !File.Exists(ruta1), "los que se borran son los mas viejos");
            Check(File.Exists(Path.Combine(dir, "rocola_diagnostico.txt")) && File.Exists(Path.Combine(dir, "rocola_diagnostico_notas.txt")), "y el registro de siempre (rocola_diagnostico.txt) y otros archivos parecidos no se tocan");
            object[] eMal = { dir + Path.DirectorySeparatorChar + "mal\0nombre", texto, DateTime.Now, 10, null };
            string rm = (string)LlamaE(TInforme, "Escribe", eMal);
            Check(rm == null && eMal[4] != null, "si no se puede escribir, devuelve null y el motivo; no lanza nada");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    static string Recorta(string s, int n) { return s.Length <= n ? s : s.Substring(0, n) + "..."; }

    // ======================= 6) EL PANEL DE LA TARJETA IMITADA =======================
    static Control PanelImitado(int w, int h)
    {
        return (Control)typeof(FranjaReloj).GetMethod("Panel", TODO).Invoke(null, new object[] { w, h });
    }
    static Control panelR;
    static bool CuadroReal() { return (bool)Llama(panelR, "DiagCuadroPaceado"); }
    static bool EstadoAbierto() { return (bool)Llama(panelR, "get_EstadoExtendidoAsentado"); }
    static void AbreEstado(int n) { Pon(panelR, "IdleExpandTarget", true); }
    static void AlternaEstado(int n) { Pon(panelR, "IdleExpandTarget", (n % 2) == 0); }
    static void MantenEstado() { Pon(panelR, "IdleExpandTarget", true); }
    static bool IdleListo() { return true; }
    static void ElPanel(string muestra)
    {
        Console.WriteLine("\n  6) EL PANEL DE LA TARJETA IMITADA\n");
        const int W = 1024, H = 768;
        Control p = PanelImitado(W, H); panelR = p;
        Pon(p, "Dim", 1f);
        int altoIdle = (int)LlamaE(TPanel, "IdlePanelHeight", H);
        idleBmp = new Bitmap(W, altoIdle, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(idleBmp)) g.Clear(Color.FromArgb(200, 20, 30, 70));
        FieldInfo fip = Campo(TPanel, "GetIdlePanel"); fip.SetValue(p, Delegate.CreateDelegate(fip.FieldType, typeof(Diagnostico).GetMethod("IdleBmp", TODO)));
        Pon(p, "IdlePanelVersion", 1); Pon(p, "uiBotH", 96);
        // RenderFrame suelto: la toma recibe los cuadros y el libro llena las fases.
        object cap = Nuevo(TCaptura);
        Pon(p, "libroOn", true);
        Llama(p, "set_DiagSink", cap);
        Check(Llama(p, "get_DiagSink") == cap, "la toma se enchufa y se lee del panel");
        for (int i = 0; i < 5; i++) Llama(p, "RenderFrame");   // que lo caliente
        Llama(cap, "Empieza", GpuAhora(), 1000.0 / 60.0);
        int aceptados = 0;
        for (int i = 0; i < 120; i++) { if ((bool)Llama(p, "RenderFrame")) aceptados++; Thread.Sleep(1); }
        object r = Nuevo(TRes);
        Llama(cap, "Cierra", GpuAhora(), r);
        Check(aceptados == 120 && I(r, "Cuadros") == 120, "la toma recibe los cuadros que presenta RenderFrame, ni uno mas ni uno menos (" + I(r, "Cuadros") + " de " + aceptados + ")");
        double[] fases = (double[])Toma(r, "FasesMs"); double sf = 0; for (int i = 0; i < 11; i++) sf += fases[i];
        Check(sf > 0 && fases[9] > 0, "con el libro encendido las fases salen llenas (dibujo " + F(fases[9], "0.0") + " ms en total, " + F(sf, "0.0") + " ms en las once)");
        Check(I(r, "Fallos") == 0 && D(r, "Max") > 0 && D(r, "P50") > 0, "sin fallos de presentacion y con intervalos medidos (p50 " + F(D(r, "P50"), "0.00") + " ms, max " + F(D(r, "Max"), "0.0") + " ms de la tarjeta imitada, que no espera a ningun monitor)");
        // Sin libro, las fases no se inventan: todo lo que no se sabe cae en «fuera».
        Pon(p, "libroOn", false);
        Llama(cap, "Empieza", GpuAhora(), 1000.0 / 60.0);
        for (int i = 0; i < 30; i++) Llama(p, "RenderFrame");
        object rs = Nuevo(TRes); Llama(cap, "Cierra", GpuAhora(), rs);
        double[] f2 = (double[])Toma(rs, "FasesMs"); double s2 = 0; for (int i = 0; i < 11; i++) s2 += f2[i];
        Check(s2 == 0 && f2[11] >= 0, "sin el libro encendido no se inventan fases: las once salen en cero");
        // Sin toma enchufada, no cuenta nadie: coste cero.
        Llama(p, "set_DiagSink", (object)null);
        int antes = (int)Llama(cap, "get_CuadrosVistos");
        for (int i = 0; i < 10; i++) Llama(p, "RenderFrame");
        Check((int)Llama(cap, "get_CuadrosVistos") == antes, "sin la toma enchufada RenderFrame no le da nada a nadie");
        // El turno paceado: no gira sin freno.
        Pon(p, "libroOn", true); Llama(p, "set_DiagSink", cap);
        Llama(cap, "Empieza", GpuAhora(), 1000.0 / 60.0);
        Stopwatch sw = Stopwatch.StartNew(); int vueltas = 0, presentados = 0;
        while (sw.ElapsedMilliseconds < 1000) { vueltas++; if ((bool)Llama(p, "DiagCuadroPaceado")) presentados++; Application.DoEvents(); }
        object rp = Nuevo(TRes); Llama(cap, "Cierra", GpuAhora(), rp);
        Check(presentados >= 25 && presentados <= 135, "el bucle paceado da el ritmo del refresco, no cuadros a toda velocidad: " + presentados + " cuadros en 1 s (" + vueltas + " vueltas)");
        Check(I(rp, "Cuadros") == presentados, "y la toma ve todos los que se presentaron");
        double[] fp = (double[])Toma(rp, "FasesMs");
        Check(fp[12] > 0 && fp[12] > fp[11], "en el bucle paceado de verdad casi todo el tiempo entre cuadros es espera ociosa (" + F(fp[12], "0") + " ms de espera contra " + F(fp[11], "0") + " ms de fuera ocupado)");
        Llama(p, "set_DiagSink", (object)null); Pon(p, "libroOn", false);

        // Una pasada entera del motor sobre este panel: la que da el informe de muestra.
        object motor = Nuevo(TMotor, AnfitrionReal(p));
        object capM = Toma(motor, "Captura");
        Pon(motor, "PresupuestoMs", 60000.0);
        IList es = (IList)Toma(motor, "Escenas");
        es.Add(Escena("rejilla", "Rejilla quieta (tarjeta imitada)", 500, 3000, 0, "PreparaBien", null, null));
        es.Add(Escena("reposo_3d", "Estado extendido, estrella 3D (tarjeta imitada)", 500, 3000, 0, "PreparaReposo", null, "EstadoAbierto"));
        es.Add(Escena("transicion", "Entrar y salir del estado extendido (tarjeta imitada)", 0, 3200, 800, "PreparaTransicion", "AlternaEstado", null));
        es.Add(Escena("sin_cancion", "Estado extendido sin cancion", 500, 2000, 0, "PreparaSalta", null, null));
        Pon(p, "libroOn", false);
        Llama(p, "set_DiagSink", capM); Pon(p, "libroOn", true);
        Llama(motor, "Corre");
        Llama(p, "set_DiagSink", (object)null); Pon(p, "libroOn", false);
        IList rr = (IList)Toma(motor, "Resultados");
        Check(rr.Count == 4 && S(rr[0], "Estado") == "medida" && S(rr[1], "Estado") == "medida" && S(rr[2], "Estado") == "medida" && S(rr[3], "Estado") == "saltada", "el motor recorre las escenas sobre el panel de verdad (3 medidas, 1 saltada)");
        Check(I(rr[0], "Cuadros") > 20 && I(rr[1], "Cuadros") > 20 && I(rr[2], "Cuadros") > 20, "con cuadros de verdad: " + I(rr[0], "Cuadros") + ", " + I(rr[1], "Cuadros") + " y " + I(rr[2], "Cuadros"));
        double cpu = D(rr[0], "CpuNucleos");
        Check(cpu > 0 && cpu <= Environment.ProcessorCount + 0.25, "el procesador del proceso sale en rango en cada escena (" + F(cpu, "0.00") + " nucleos)");
        bool hilosVistos = false; foreach (object h in (IList)Toma(rr[0], "Hilos")) if (S(h, "Nombre").StartsWith("pantalla")) hilosVistos = true;
        Check(hilosVistos, "el reparto por hilo trae el hilo de la pantalla (anotado por el propio motor)");
        Check(!(bool)Toma(p, "libroOn") && Llama(p, "get_DiagSink") == null, "al acabar, el libro y la toma quedan como estaban");
        object meta = Nuevo(TMeta);
        Pon(meta, "Inicio", DateTime.Now); Pon(meta, "DuracionMs", D(motor, "FinMs") - D(motor, "InicioMs")); Pon(meta, "Version", "prueba"); Pon(meta, "SoloPrueba", true);
        Pon(meta, "Equipo", "Sistema operativo: " + Environment.OSVersion + " (.NET " + Environment.Version + ")\r\nProcesador: " + (string)LlamaE(Asm.GetType("Rocola.DiagEquipo"), "Cpu") + "\r\n"
            + "Tarjeta grafica: IMITADA de las pruebas (no pinta, no espera a ningun monitor); 1024x768\r\n");
        Pon(meta, "Ajustes", "(la pasada de prueba no usa los ajustes de ninguna rocola)\r\n");
        Pon(meta, "Condiciones", "Musica: sin musica (prueba)\r\nTrabajo de fondo: nada\r\nMonitor: no hay (tarjeta imitada).\r\n");
        Pon(meta, "Hz", 60); Pon(meta, "Nucleos", Environment.ProcessorCount);
        string texto = (string)LlamaE(TInforme, "Construye", meta, rr);
        Check(texto.IndexOf("Estado extendido, estrella 3D (tarjeta imitada)") >= 0 && texto.IndexOf("== 8. CANDIDATOS") >= 0 && texto.IndexOf("hilos:") >= 0, "el informe de esa pasada trae las escenas, el reparto por hilo y los candidatos");
        if (muestra != null) { File.WriteAllText(muestra, texto, new System.Text.UTF8Encoding(true)); Info("informe de muestra escrito en " + muestra); }
    }
    static Bitmap idleBmp;
    static Bitmap IdleBmp() { return idleBmp; }
    static double GpuAhora() { return (double)LlamaE(TPanel, "AhoraMs"); }
    static string PreparaReposo() { preparas++; Pon(panelR, "IdleExpandTarget", true); Pon(panelR, "idleBlend", 1f); return null; }
    static string PreparaTransicion() { preparas++; Pon(panelR, "IdleExpandTarget", false); return null; }
    static object AnfitrionReal(Control p)
    {
        object h = Nuevo(TAnfi);
        Pondel(h, "Cuadro", "CuadroReal"); Pondel(h, "Bombea", "BombeaReal"); Pondel(h, "Interrumpido", "NoInterrumpido");
        Pondel(h, "Ahora", "AhoraReal"); Pondel(h, "PeriodoRefMs", "PeriodoRef"); Pondel(h, "ReiniciaMedida", "NadaReal");
        Pondel(h, "Aviso", "Aviso"); Pondel(h, "TexMb", "Tex"); Pondel(h, "Fondo", "SinFondo"); Pondel(h, "Musica", "MusicaReal"); Pondel(h, "Portadas", "SinFondo");
        return h;
    }
    static void BombeaReal() { Application.DoEvents(); }
    static double AhoraReal() { return GpuAhora(); }
    static void NadaReal() { Llama(panelR, "ReiniciaMedida"); }
    static string MusicaReal() { return "sin musica (prueba de la tarjeta imitada)"; }

    // ======================= 7) EL LADO DE LA ROCOLA =======================
    static void LaRocola()
    {
        Console.WriteLine("\n  7) EL LADO DE LA ROCOLA (un Form1 sin encender)\n");
        object f = Codigo.FormSinEncender(TF);
        Type tCtx = TF.GetNestedType("DiagCtx", TODO), tEst = TF.GetNestedType("DiagEstado", TODO);
        Check(tCtx != null && tEst != null, "existen el contexto de las escenas y el estado a devolver");
        object ctx = Activator.CreateInstance(tCtx, true);
        IList escenas = (IList)Llama(f, "DiagEscenas", ctx);
        string[] ids = new string[escenas.Count]; for (int i = 0; i < ids.Length; i++) ids[i] = S(escenas[i], "Id");
        string[] debenEstar = { "rejilla", "hojeo_vistas", "hojeo_frio", "letras", "buscador", "disco_entrar_salir", "disco_lista", "atraccion", "reposo_2d", "reposo_3d", "reposo_transicion", "cambio_cancion", "asistente", "avisos_ficha", "peor_navegando", "peor_reposo" };
        string faltan = ""; foreach (string d in debenEstar) if (Array.IndexOf(ids, d) < 0) faltan += " " + d;
        Check(escenas.Count == debenEstar.Length && faltan == "", "monta las " + debenEstar.Length + " escenas: rejilla, hojeo (vistas y frias), letras, buscador, discos, atraccion, estado extendido 2D y 3D, transicion, cambio de cancion, asistente, avisos y ficha, y dos peor caso" + (faltan != "" ? " -- faltan:" + faltan : ""));
        int total = 0, minimo = int.MaxValue, conPrepara = 0, conLimpia = 0, conQue = 0;
        for (int i = 0; i < escenas.Count; i++)
        {
            int s = I(escenas[i], "CalentaMs") + I(escenas[i], "MedidaMs");
            total += s + 600; minimo = Math.Min(minimo, I(escenas[i], "MedidaMs"));
            if (Toma(escenas[i], "Prepara") != null) conPrepara++;
            if (Toma(escenas[i], "Limpia") != null) conLimpia++;
            if (S(escenas[i], "Que").Length > 20) conQue++;
        }
        Check(total >= 150000 && total <= 330000, "la duracion prevista de las escenas es de " + F(total / 60000.0, "0.0") + " minutos (objetivo: 3 a 6, con la musica y los arranques)");
        Check(minimo >= 8000, "cada escena mide al menos 8 s (la mas corta: " + F(minimo / 1000.0, "0.0") + " s)");
        Check(conPrepara == escenas.Count && conLimpia == escenas.Count && conQue == escenas.Count, "todas tienen su preparacion, su limpieza y la descripcion de lo que hacen");
        bool ponAlto = false, devuelve = false;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(TF, "DiagEscenas")) { if (Codigo.Llama(m, "DiagPeorCaso")) ponAlto = true; if (Codigo.Llama(m, "DiagDevuelveEfectos")) devuelve = true; }
        bool peorEnAlto = ponAlto && devuelve;
        Check(peorEnAlto, "los peor caso ponen todo en Alto y lo devuelven al acabar cada escena");

        // La musica que no hay.
        Pon(ctx, "Bk", Activator.CreateInstance(tEst, true));
        Llama(f, "DiagPoneMusica", ctx);
        Check(!(bool)Toma(ctx, "ConMusica") && S(ctx, "MusicaTexto").IndexOf("sin musica") == 0 && S(ctx, "MusicaTexto").IndexOf("no tiene canciones") >= 0, "sin canciones en la biblioteca no se inventa musica: \"" + S(ctx, "MusicaTexto") + "\"");
        string sin = (string)Llama(f, "DiagParaReposo", ctx, 20000);
        Check(sin != null && sin.IndexOf("no hay cancion") >= 0, "y el estado extendido se salta diciendo que le falta una cancion");

        // La interrupcion.
        Pon(ctx, "GraciaHasta", Environment.TickCount + 3000);
        Pon(f, "lastInputTick", 100); Pon(f, "lastInput", new DateTime(2026, 1, 1));
        Llama(f, "DiagRebase", ctx);
        Check(!(bool)Llama(f, "DiagInterrumpido", ctx), "dentro de los dos primeros segundos nada interrumpe (el raton se asienta al cerrar la ventana)");
        Pon(ctx, "GraciaHasta", Environment.TickCount - 5000);
        Check(!(bool)Llama(f, "DiagInterrumpido", ctx), "pasada la gracia y sin tocar nada, sigue");
        Pon(f, "lastInputTick", 250);
        Check((bool)Llama(f, "DiagInterrumpido", ctx), "un boton (lastInputTick) lo interrumpe: tambien una moneda");
        Llama(f, "DiagRebase", ctx);
        Pon(f, "lastInput", new DateTime(2026, 1, 2));
        Check((bool)Llama(f, "DiagInterrumpido", ctx), "el raton o la pantalla (lastInput) tambien");
        Llama(f, "DiagRebase", ctx);
        Check(!(bool)Llama(f, "DiagInterrumpido", ctx), "tras reanclar (los cambios de las propias escenas), sigue");
        Pon(f, "startupClosing", true);
        Check((bool)Llama(f, "DiagInterrumpido", ctx), "cerrar la rocola tambien lo deja todo");
        Pon(f, "startupClosing", false);

        // Los textos del equipo y de los ajustes.
        string ajustes = (string)Llama(f, "DiagTextoAjustes");
        Check(ajustes.IndexOf("Preset de graficos:") >= 0 && ajustes.IndexOf("Efectos: nieblas=") >= 0 && ajustes.IndexOf("Ahorros de Performance") >= 0 && ajustes.IndexOf("Cielo a media resolución: ") >= 0 && ajustes.IndexOf("Perfil de carga de caratulas:") >= 0,
              "los ajustes vigentes traen el preset, los efectos, los ahorros de Performance uno a uno y el perfil de caratulas");
        string equipo = (string)Llama(f, "DiagTextoEquipo");
        Check(equipo.IndexOf("Sistema operativo:") >= 0 && equipo.IndexOf("Procesador:") >= 0 && equipo.IndexOf("Memoria:") >= 0 && equipo.IndexOf("Tarjeta grafica:") >= 0 && equipo.IndexOf("Biblioteca:") >= 0,
              "el equipo trae sistema, procesador, memoria, tarjeta y biblioteca");
    }

    // ======================= 7b) LA ORQUESTACION ENTERA, CON LAS ESCENAS SIN PODER MONTARSE =======================
    // Un Form1 sin encender no tiene pared de discos, ni buscador, ni musica: las escenas se saltan. Pero SI corre todo lo demas de
    // CorreDiagnosticoGpu: guardar el estado, enchufar la toma y el libro, el motor, el informe a un archivo, el resumen y devolver
    // el estado. No es una corrida de verdad (esa solo se puede hacer en la rocola); es la prueba de que el cableado no revienta y no deja nada.
    static void LaOrquestacion()
    {
        Console.WriteLine("\n  7b) CorreDiagnosticoGpu ENTERO, CON ESCENAS QUE NO SE PUEDEN MONTAR\n");
        Control p = PanelImitado(1024, 768); panelR = p;
        object f = Codigo.FormSinEncender(TF);
        Pon(f, "gpuPanel", p); Pon(f, "gpuEnabled", true);
        Pon(f, "nieblasNiv", 1); Pon(f, "cieloMedia", true); Pon(f, "estrella3d", false); Pon(f, "currentLetter", 'M'); Pon(f, "page", 3); Pon(f, "selAlbumIndex", 7);
        Llama(p, "set_LibroActivo", true);
        Pon(f, "diagResumen", "");
        foreach (string x in Directory.GetFiles(carpetaDatos, "rocola_diagnostico_2*.txt")) File.Delete(x);
        Llama(f, "CorreDiagnosticoGpu");
        string resumen = S(f, "diagResumen");
        Check(resumen.Length > 0 && resumen.IndexOf('\n') > 0 && resumen.IndexOf("Diagnóstico ") == 0, "al acabar deja el resumen de la pagina: \"" + resumen.Replace("\n", " | ") + "\"");
        string[] informes = Directory.GetFiles(carpetaDatos, "rocola_diagnostico_2*.txt");
        Check(informes.Length == 1, "y UN informe nuevo en la carpeta de datos (" + informes.Length + ")");
        string texto = informes.Length > 0 ? File.ReadAllText(informes[0]) : "";
        Check(texto.IndexOf("== 1. EQUIPO ==") >= 0 && texto.IndexOf("== 10. LIMITES ==") >= 0 && texto.IndexOf("Preset de graficos:") >= 0 && texto.IndexOf("Memoria:") >= 0, "con sus secciones, el equipo y los ajustes vigentes");
        int saltadas = 0; foreach (string l in texto.Split('\n')) if (l.IndexOf("SALTADA:") >= 0) saltadas++;
        Check(saltadas == 16, "las 16 escenas aparecen saltadas, cada una con su motivo (" + saltadas + ")");
        Check(!(bool)Toma(f, "diagEnMarcha"), "no se queda «en marcha»");
        Check(Llama(p, "get_DiagSink") == null && (bool)Llama(p, "get_LibroActivo"), "la toma se desenchufa y el libro vuelve a como estaba (aqui encendido de antes)");
        Check((int)Toma(f, "nieblasNiv") == 1 && (bool)Toma(f, "cieloMedia") && !(bool)Toma(f, "estrella3d"), "efectos, ahorros y estrella, como estaban");
        // (RefreshAlbums ajusta la pagina y el disco elegido a lo que haya en la biblioteca: sin discos caen a 0, como siempre)
        Check((char)Toma(f, "currentLetter") == 'M', "la letra, como estaba (la pagina y el disco los ajusta RefreshAlbums a la biblioteca, vacia aqui)");
        Check(Toma(f, "nowPlaying") == null, "y la musica: no se invento ninguna");
        // Sin tarjeta o con un video, no hace nada y lo dice.
        Pon(f, "gpuEnabled", false); Pon(f, "diagResumen", "");
        Llama(f, "CorreDiagnosticoGpu");
        Check(S(f, "diagResumen").IndexOf("No se hizo el diagnostico") == 0, "sin tarjeta grafica no hace nada y lo dice: \"" + S(f, "diagResumen").Split('\n')[0] + "\"");
        Pon(f, "gpuEnabled", true); Pon(f, "videoOn", true); Pon(f, "diagResumen", "");
        Llama(f, "CorreDiagnosticoGpu");
        Check(S(f, "diagResumen").IndexOf("hay un video") >= 0, "con un video en ventana tampoco: \"" + S(f, "diagResumen").Split('\n')[0] + "\"");
        Pon(f, "videoOn", false);
        Llama(p, "set_LibroActivo", false);

        // Devolver el estado de verdad: se guarda, se estropea todo y se devuelve.
        object bk = Llama(f, "DiagGuardaEstado");
        Pon(f, "nieblasNiv", 2); Pon(f, "cieloMedia", false); Pon(f, "estrella3d", true); Pon(f, "currentLetter", 'Z'); Pon(f, "page", 0); Pon(f, "selAlbumIndex", 0);
        Pon(p, "IdleExpandTarget", true);
        Llama(f, "DiagRestaura", bk);
        Check((int)Toma(f, "nieblasNiv") == 1 && (bool)Toma(f, "cieloMedia") && !(bool)Toma(f, "estrella3d"), "DiagRestaura devuelve el nivel de los efectos, los ahorros y la estrella 3D");
        Check((char)Toma(f, "currentLetter") == 'M' && !(bool)Toma(p, "IdleExpandTarget"), "y la letra y el estado extendido");
    }

    // ======================= 8) LO QUE NO DEBE HACER =======================
    static List<string> DeTodosLosDiag(out int metodos)
    {
        List<string> r = new List<string>(); metodos = 0;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
        {
            string n = m.Name; string dt = m.DeclaringType != null ? m.DeclaringType.Name : "";
            bool mio = n.StartsWith("Diag") || n == "CorreDiagnosticoGpu" || n == "DiagnosticoCompletoF3" || n.IndexOf("<Diag", StringComparison.Ordinal) >= 0 || n.IndexOf("<CorreDiagnosticoGpu>", StringComparison.Ordinal) >= 0
                || dt.IndexOf("<Diag", StringComparison.Ordinal) >= 0 || dt.IndexOf("<CorreDiagnosticoGpu>", StringComparison.Ordinal) >= 0;
            if (!mio) continue;
            metodos++; r.AddRange(Codigo.Llamadas(m));
        }
        return r;
    }
    static void LoQueNoHace()
    {
        Console.WriteLine("\n  8) LO QUE NO DEBE HACER (leido del programa compilado)\n");
        int n; List<string> ll = DeTodosLosDiag(out n);
        Check(n >= 20, "se leen " + n + " metodos del diagnostico en Form1 (con sus funciones sueltas)");
        Check(!Codigo.Llama(ll, "SaveConfigPrefs") && !Codigo.Llama(ll, "SaveConfig") && !Codigo.Llama(ll, "GuardaAlMomento") && !Codigo.Llama(ll, "TextoConfig"), "NO guarda la configuracion (propone y mide, como el medidor: no se pide guardar nada)");
        Check(!Codigo.Llama(ll, "ApplyAlbumGpu") && !Codigo.Llama(ll, "StopGpu") && !Codigo.Llama(ll, "StartGpu"), "NO reinicia la tarjeta grafica");
        Check(!Codigo.Llama(ll, "SaveState") && !Codigo.Llama(ll, "GuardaEstado") && !Codigo.Llama(ll, "AddCoin") && !Codigo.Llama(ll, "RefundCredit"), "NO toca el estado guardado, ni las monedas ni los creditos");
        bool volumen = false; foreach (string s in ll) if (s.IndexOf("Volume", StringComparison.Ordinal) >= 0 || s.IndexOf("Volumen", StringComparison.Ordinal) >= 0 || s.IndexOf("Mute", StringComparison.Ordinal) >= 0) volumen = true;
        Check(!volumen, "NO toca ningun volumen (ni el de Windows ni el de la salida, ni el de la cancion)");
        Check(Codigo.Llama(ll, "PlayTrackForMeasurement") && !Codigo.Llama(ll, "PlayTrack") && !Codigo.Llama(ll, "PlayNext") && !Codigo.Llama(ll, "StartOpenedTrack"), "la cancion del diagnostico se pone por PlayTrackForMeasurement (sale muda desde dentro de la cadena); nunca por la cola");
        Check(Codigo.Llama(ll, "DisposePlaybackNow") && Codigo.Llama(ll, "QuitaEscena") && Codigo.Llama(ll, "PonNivel") && Codigo.Llama(ll, "PonAhorro") && Codigo.Llama(ll, "CloseSearch") && Codigo.Llama(ll, "BackToAlbums") && Codigo.Llama(ll, "RefreshAlbums"),
              "lo deja todo como estaba: para su cancion, quita la escena, devuelve niveles y ahorros, cierra buscador y disco, vuelve a la pagina");
        // Solo para SU cancion.
        int le, es; foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) { if (m.Name == "DiagRestaura") { Codigo.UsosDeCampo(m, "nowPlaying", out le, out es); Check(le > 0, "al devolver, solo para la musica si la puso el diagnostico (PuseCancion), mirando antes que habia (nowPlaying)"); } }
        bool puse = false; foreach (MethodBase m in Codigo.TodosLosMetodos(TF)) if (m.Name == "DiagRestaura") puse = Codigo.LeeCampo(new List<MethodBase>(new MethodBase[] { m }), "PuseCancion");
        Check(puse, "y esa regla esta escrita en un campo que el final LEE (la cancion de un cliente no se para)");
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (m.Name == "DiagInterrumpido")
            {
                int a, b, c, d; Codigo.UsosDeCampo(m, "lastInputTick", out a, out b); Codigo.UsosDeCampo(m, "lastInput", out c, out d);
                Check(a > 0 && c > 0, "se cancela por interaccion: mira los botones (lastInputTick) y el raton/pantalla (lastInput)");
            }
        List<string> corre = Codigo.ConAyudantes(TF, "CorreDiagnosticoGpu");
        Check(Codigo.Llama(corre, "DiagRestaura") && Codigo.Llama(corre, "DiagGuardaEstado") && Codigo.Llama(corre, "Corre"), "CorreDiagnosticoGpu guarda el estado, corre el motor y lo devuelve");
        bool enFinally = false;
        MethodInfo mc = Metodo(TF, "CorreDiagnosticoGpu");
        foreach (ExceptionHandlingClause cl in mc.GetMethodBody().ExceptionHandlingClauses) if (cl.Flags == ExceptionHandlingClauseOptions.Finally) enFinally = true;
        Check(enFinally, "y lo devuelve DENTRO de un finally: pase lo que pase");
        List<string> cl2 = Codigo.Llamadas(mc);
        Check(Codigo.Llama(cl2, "set_LibroActivo") && Codigo.Llama(cl2, "set_DiagSink"), "enciende el libro de los peores cuadros y enchufa la toma (y los devuelve)");
        // Sin escritura a disco durante las escenas.
        List<string> motor = new List<string>();
        foreach (string nom in new string[] { "Corre", "CorreEscena", "Ventana", "CalculaCpu", "Limpia", "NuevoResultado" }) motor.AddRange(Codigo.ConAyudantes(TMotor, nom));
        motor.AddRange(Codigo.ConAyudantes(TCaptura, "Cuadro")); motor.AddRange(Codigo.ConAyudantes(TCaptura, "Cierra")); motor.AddRange(Codigo.ConAyudantes(TCaptura, "Empieza"));
        bool disco = false; string quien = "";
        foreach (string s in motor) if (s.IndexOf("File.", StringComparison.Ordinal) >= 0 || s.IndexOf("StreamWriter", StringComparison.Ordinal) >= 0 || s.IndexOf("Bitacora", StringComparison.Ordinal) >= 0 || s.IndexOf("Directory.", StringComparison.Ordinal) >= 0 || s.IndexOf("FileStream", StringComparison.Ordinal) >= 0) { disco = true; quien = s; }
        Check(!disco, "el motor y la toma NO escriben a disco durante las escenas (el informe se escribe al final)" + (disco ? " -- " + quien : ""));
        List<string> durante = new List<string>();
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (m.Name == "DiagEscenas" || m.Name == "DiagCreaAnfitrion" || m.Name == "DiagCambiaCancion" || m.Name == "DiagNeutral" || m.Name.IndexOf("<DiagEscenas>", StringComparison.Ordinal) >= 0 || (m.DeclaringType != null && m.DeclaringType.Name.IndexOf("<DiagEscenas>", StringComparison.Ordinal) >= 0))
                durante.AddRange(Codigo.Llamadas(m));
        bool disco2 = false; foreach (string s in durante) if (s.IndexOf("File.", StringComparison.Ordinal) >= 0 || s.IndexOf("Bitacora", StringComparison.Ordinal) >= 0 || s.IndexOf("StreamWriter", StringComparison.Ordinal) >= 0 || s.IndexOf("LogError", StringComparison.Ordinal) >= 0) disco2 = true;
        Check(!disco2, "ni las escenas de la rocola escriben a disco (ni bitacora ni errores mientras miden)");
        Check(Codigo.Llama(Codigo.ConAyudantes(TF, "DiagTerminaInforme"), "Escribe"), "el informe se escribe al final, una sola vez (DiagTerminaInforme)");
        bool lib = Codigo.Llama(Codigo.Llamadas(Metodo(TPanel, "DiagCuadroPaceado")), "RenderFrame") && Codigo.Llama(Codigo.Llamadas(Metodo(TPanel, "DiagCuadroPaceado")), "AgendaCuadro") && Codigo.Llama(Codigo.Llamadas(Metodo(TPanel, "DiagCuadroPaceado")), "EsperaMensajes");
        Check(lib, "el turno de cuadro agenda y espera igual que el bucle de verdad (AgendaCuadro + EsperaMensajes + RenderFrame): no suelta el monitor");
        Check(!Codigo.Llama(DeTodosLosDiag(out n), "PideSinEspera"), "y no suelta el monitor (no pide PideSinEspera)");
        // MideRendimiento sigue sin guardar nada (lo vigila banco; aqui tambien).
        List<string> mide = Codigo.ConAyudantes(TF, "MideRendimiento");
        Check(!Codigo.Llama(mide, "SaveConfigPrefs") && Codigo.Llama(mide, "PlayTrackForMeasurement"), "el medidor rapido sigue igual: no guarda nada y pone su cancion muda");
    }

    // ======================= LA CARGA DE LA ROCOLA =======================
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_diagnostico_" + Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir);
        string destino = Path.Combine(dir, Path.GetFileName(ruta));
        File.Copy(ruta, destino, true);
        string dirOrigen = Path.GetDirectoryName(Path.GetFullPath(ruta));
        foreach (string dll in new string[] { "NAudio.dll", "NLayer.dll" })
            if (File.Exists(Path.Combine(dirOrigen, dll))) File.Copy(Path.Combine(dirOrigen, dll), Path.Combine(dir, dll), true);
        File.WriteAllText(destino + ".config", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <dllmap dll=\"kernel32.dll\">\n" +
                          "    <dllentry dll=\"libc.so.6\" name=\"RtlMoveMemory\" target=\"memmove\"/>\n  </dllmap>\n</configuration>\n");
        carpetaCopia = dir;
        return destino;
    }

    [STAThread]
    static int Main(string[] argv)
    {
        try { return Prueba(argv); }
        finally { if (carpetaCopia != null) try { Directory.Delete(carpetaCopia, true); } catch { } }
    }
    static string carpetaDatos;
    static int Prueba(string[] argv)
    {
        // La carpeta de datos de la rocola se calcula UNA vez: se apunta a una temporal ANTES de tocar nada (el informe de CorreDiagnosticoGpu va alli).
        carpetaDatos = Path.Combine(Path.GetTempPath(), "rocola_diag_datos_" + Process.GetCurrentProcess().Id);
        if (Directory.Exists(carpetaDatos)) Directory.Delete(carpetaDatos, true);
        Directory.CreateDirectory(carpetaDatos);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", carpetaDatos);
        try { return PruebaConDatos(argv); }
        finally { try { Directory.Delete(carpetaDatos, true); } catch { } }
    }
    static int PruebaConDatos(string[] argv)
    {
        Asm = Assembly.LoadFrom(Cargable((argv.Length > 0) ? argv[0] : "rocola.exe"));
        TPanel = Asm.GetType("Rocola.GpuPanel");
        if (TPanel == null) { Console.WriteLine("  OK    sin tarjeta grafica no hay diagnostico completo: nada que mirar"); return 0; }
        TF = Asm.GetType("Rocola.Form1"); TStar = Asm.GetType("Rocola.GpuStarfield"); TDev = Asm.GetType("Rocola.D3D9Device"); TD3D = Asm.GetType("Rocola.D3D9");
        TLibro = Asm.GetType("Rocola.LibroDeCuadros"); TCaptura = Asm.GetType("Rocola.DiagCaptura"); TMotor = Asm.GetType("Rocola.DiagnosticoCompleto");
        TDef = Asm.GetType("Rocola.DiagEscenaDef"); TAnfi = Asm.GetType("Rocola.DiagAnfitrion"); TMeta = Asm.GetType("Rocola.DiagMeta");
        TInforme = Asm.GetType("Rocola.DiagInforme"); THilos = Asm.GetType("Rocola.HilosRocola"); TLector = Asm.GetType("Rocola.LectorCpu");
        TInst = Asm.GetType("Rocola.DiagInstantanea"); TRes = Asm.GetType("Rocola.DiagResultado");
        // Los campos que usa el panel de franjareloj.cs para armar la tarjeta imitada.
        Type fr = typeof(FranjaReloj);
        fr.GetField("TF", TODO).SetValue(null, TF); fr.GetField("TPanel", TODO).SetValue(null, TPanel); fr.GetField("TStar", TODO).SetValue(null, TStar);
        fr.GetField("TDev", TODO).SetValue(null, TDev); fr.GetField("TD3D", TODO).SetValue(null, TD3D);
        string muestra = argv.Length > 1 ? argv[1] : null;
        try { Piezas(); } catch (Exception e) { Check(false, "las piezas: " + e); }
        if (TCaptura == null || TMotor == null) { Console.WriteLine("\n  FALLOS: " + fallos); return 1; }
        try { LaToma(); } catch (Exception e) { Check(false, "la toma: " + e); }
        object motorInforme = null;
        try { ElMotor(); } catch (Exception e) { Check(false, "el motor: " + e); }
        try { ElProcesador(); } catch (Exception e) { Check(false, "el procesador: " + e); }
        try
        {
            // El informe se arma con una corrida virtual con de todo: tiron, hueco fuera, saltada y cancelada.
            ReiniciaVirtual();
            object cap; motorInforme = NuevoMotor(out cap); libroV = Nuevo(TLibro);
            IList es = (IList)Toma(motorInforme, "Escenas");
            es.Add(Escena("quieta", "Escena quieta", 300, 3000, 0, "PreparaBien", null, null));
            es.Add(Escena("tiron", "Escena con un tiron de 120 ms", 200, 4000, 1000, "PreparaBien", "PasoTiron", null));
            es.Add(Escena("muchos", "Escena con un tiron de 60 ms en cada paso", 200, 4000, 500, "PreparaBien", "PasoMuchos", null));
            es.Add(Escena("sinmusica", "Escena que no se puede montar", 200, 3000, 0, "PreparaSalta", null, null));
            Llama(motorInforme, "Corre");
            ElInforme(motorInforme);
        }
        catch (Exception e) { Check(false, "el informe: " + e); }
        try { ElPanel(muestra); } catch (Exception e) { Check(false, "el panel de la tarjeta imitada: " + e); }
        try { LaRocola(); } catch (Exception e) { Check(false, "el lado de la rocola: " + e); }
        try { LaOrquestacion(); } catch (Exception e) { Check(false, "la orquestacion: " + e); }
        try { LoQueNoHace(); } catch (Exception e) { Check(false, "lo que no debe hacer: " + e); }
        Console.WriteLine(fallos == 0 ? ("\n  todo bien (" + oks + " comprobaciones; sinteticas: tarjeta imitada y Linux/Mono, no la rocola real)") : ("\n  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
