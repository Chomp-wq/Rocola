using System;
using System.Collections.Generic;
using System.Reflection;

// MODO NOCHE: de las 11 de la noche a las 7 de la manana, la rocola baja 4 dB.
//
// Se pidio "al 80%". Al pie de la letra eso son -1.9 dB y el oido apenas distingue 1 dB: en un bar con
// gente no se habria notado NADA. Se hablo y quedo en -4 dB, que deja la musica sonando a un ~76% de
// fuerte, que es lo que de verdad se queria decir.
//
// LO QUE ESTA BATERIA VIGILA, que es donde esto se rompe:
//
//  1) EL CRUCE DE LA MEDIANOCHE. Es LA trampa. Un rango que empieza a las 23 y acaba a las 7 no se puede
//     escribir "hora >= 23 && hora < 7": eso es SIEMPRE FALSO, no hay ninguna hora que cumpla las dos.
//     Tiene que ser un O. Y el fallo no da la cara: el programa compila, arranca, no truena... y el modo
//     noche simplemente no ocurre nunca. Nadie se enteraria hasta que un vecino se quejara.
//
//  2) QUE LOS VIDEOS TAMBIEN BAJEN, Y EN PARTICULAR LOS YA MEDIDOS. El volumen no sale por un sitio,
//     sale por dos: la musica por LevelTap.Master y los videos por el reproductor de Windows. Y en el
//     camino del video hay una rama -la del video YA MEDIDO- que se salta la ganancia general A
//     PROPOSITO (esta comentado en el codigo: el WMP no pasa de 100 y subirlo deshacia el emparejado).
//     Meter el factor de noche dentro de MasterLin(), que es lo primero que uno haria, dejaria esos
//     videos sonando a todo volumen a las tres de la manana.
//
//  3) QUE LA RAMPA VAYA POR RELOJ DE PARED. Baja poco a poco para que no se oiga el escalon a mitad de
//     cancion. Si contara "vueltas" en vez de milisegundos, el medidor de Ctrl+F3 -que PARA el
//     temporizador de animacion y llama al tick a mano- la volveria loca.
//
//  4) QUE DE DIA NO CAMBIE ABSOLUTAMENTE NADA. Un ajuste que solo debe actuar ocho horas al dia no
//     puede tocar ni un bit las otras dieciseis.
//
//  5) LO QUE EL OIDO PIERDE AL BAJAR EL VOLUMEN (Rocola/ModoNoche.cs). Mientras la noche baja, se devuelve un
//     poco de graves y de agudos: lo que dicen las curvas de igual sonoridad (ISO 226). Se mide con tonos por la
//     etapa de verdad contra la norma calculada aqui, y se vigila que siga la rampa, que de dia salga bit a bit, que
//     no de saltos y que la pista siguiente de un disco continuo siga con sus filtros.
class FuenteNoche : NAudio.Wave.ISampleProvider
{
    readonly float[] x; int pos; readonly NAudio.Wave.WaveFormat wf;
    public FuenteNoche(float[] datos, int fs, int ch) { x = datos; wf = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public NAudio.Wave.WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int n) { int k = Math.Min(n, x.Length - pos); if (k <= 0) return 0; Array.Copy(x, pos, b, o, k); pos += k; return k; }
}

class Noche
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static Type TF;
    static MethodInfo Met(string n)
    {
        if (TF == null) return null;
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static FieldInfo Campo(string n) { return (TF == null) ? null : TF.GetField(n, TODO); }

    static bool EsDeNoche(MethodInfo m, int hora, int minuto)
    {
        DateTime t = new DateTime(2026, 8, 30, hora, minuto, 0);
        return (bool)m.Invoke(null, new object[] { t });
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS\n");
        MethodInfo esNoche = Met("EsDeNoche");
        MethodInfo rampa = Met("AvanzaRampa");
        MethodInfo factor = Met("FactorNoche");
        Check(esNoche != null, "hay quien sepa decir si AHORA es de noche (EsDeNoche)");
        Check(factor != null, "y cuanto hay que bajar (FactorNoche)");
        Check(rampa != null, "y la rampa que lo baja poco a poco (AvanzaRampa)");

        Console.WriteLine("\n  2) LAS CUATRO PERILLAS SE GUARDAN Y SE RELEEN\n");
        MethodInfo lee = Met("LoadConfigPrefs"), esc = Met("TextoConfig");
        List<string> tLee = (lee != null) ? Codigo.Textos(lee) : new List<string>();
        List<string> tEsc = (esc != null) ? Codigo.Textos(esc) : new List<string>();
        foreach (string k in new string[] { "noche=", "noche_desde=", "noche_hasta=", "noche_db=" })
        {
            Check(tLee.Contains(k), "el archivo de configuracion se LEE con " + k);
            Check(tEsc.Contains(k), "y se GUARDA con " + k);
        }

        Console.WriteLine("\n  3) EL CRUCE DE LA MEDIANOCHE\n");
        if (esNoche == null)
        {
            Check(false, "sin EsDeNoche no se puede comprobar el horario");
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }
        // El rango va de las 23 a las 7 PASANDO POR LA MEDIANOCHE. Escrito con un Y en vez de un O, todas
        // estas darian "no es de noche" y el modo noche no ocurriria jamas.
        Check(!EsDeNoche(esNoche, 22, 59), "22:59 todavia NO es de noche");
        Check(EsDeNoche(esNoche, 23, 0), "23:00 ya es de noche");
        Check(EsDeNoche(esNoche, 23, 30), "23:30 es de noche");
        Check(EsDeNoche(esNoche, 0, 30), "00:30 SIGUE siendo de noche (el rango cruza la medianoche)");
        Check(EsDeNoche(esNoche, 3, 0), "03:00 es de noche");
        Check(EsDeNoche(esNoche, 6, 59), "06:59 aun es de noche");
        Check(!EsDeNoche(esNoche, 7, 0), "07:00 ya NO es de noche");
        Check(!EsDeNoche(esNoche, 12, 0), "12:00 no es de noche");
        Check(!EsDeNoche(esNoche, 20, 0), "20:00 no es de noche");

        Console.WriteLine("\n  4) CUANTO BAJA\n");
        // -4 dB en ganancia lineal son 10^(-4/20) = 0.6310. Es lo que se acordo: la musica se oye a un
        // ~76% de fuerte, que es lo que quiere decir "al 80%" cuando se dice en voz alta.
        FieldInfo fDb = Campo("nocheDb");
        if (fDb != null)
        {
            object v = fDb.GetValue(null) ?? fDb.GetValue(System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF));
            double db = Convert.ToDouble(v);
            Check(Math.Abs(db - (-4.0)) < 0.001, "de fabrica baja 4 dB (dio " + db.ToString("0.0") + ")");
            double lin = Math.Pow(10.0, db / 20.0);
            Check(Math.Abs(lin - 0.6310) < 0.001, "que en ganancia son 0.631 (dio " + lin.ToString("0.000") + ")");
        }
        else Check(false, "no se encontro cuanto baja (nocheDb)");

        Console.WriteLine("\n  5) LOS VIDEOS TAMBIEN BAJAN, INCLUIDOS LOS YA MEDIDOS\n");
        // El agujero: en VideoVolumeFor hay una rama para el video YA MEDIDO que se salta MasterLin() a
        // proposito. Si el factor de noche viviera dentro de MasterLin(), esos videos -que en una rocola
        // usada son la mayoria- seguirian a todo volumen de madrugada. Asi que VideoVolumeFor tiene que
        // consultarlo por su cuenta.
        MethodInfo vvf = Met("VideoVolumeFor");
        Check(vvf != null && Codigo.Llama(vvf, "FactorNoche"), "el volumen del video consulta el factor de noche");
        // La cadena de cada cancion se arma en ArmaCadena: la usan el arranque de siempre (StartOpenedTrack) y el
        // relevo sin pausa, que arma la siguiente antes de que suene.
        MethodInfo play = Met("ArmaCadena"), arranca = Met("StartOpenedTrack");
        Check(play != null && Codigo.Llama(play, "FactorNoche") && arranca != null && Codigo.Llama(arranca, "ArmaCadena"), "y el de la musica tambien");
        MethodInfo prep = Met("AplicaAPreparada");
        Check(prep != null && Codigo.Llama(prep, "FactorNoche"), "y el de la siguiente ya preparada para entrar sin pausa, si cambia la hora mientras espera");
        MethodInfo apply = Met("ApplyNormNow");
        Check(apply != null && Codigo.Llama(apply, "FactorNoche"), "y el ajuste en vivo, para que se oiga bajar sin cambiar de cancion");

        Console.WriteLine("\n  5b) EL INTERRUPTOR DE LA VENTANA DE SONIDO\n");
        // De nada sirve una casilla si no la mira nadie: el operador la apaga, no cambia nada, y deja de
        // fiarse del resto de la ventana. Se comprueba que la ventana de Ctrl+F3 LEE el ajuste -para que la
        // casilla salga como esta ahora- y lo ESCRIBE -para que apagarla sirva de algo-.
        int leeV, escV;
        UsosConAyudantes("VentanaAjustes", "nocheOn", out leeV, out escV);
        Check(leeV > 0, "la ventana de sonido LEE el modo noche (la casilla sale como esta ahora)");
        Check(escV > 0, "y lo ESCRIBE: la casilla sirve de algo");
        List<string> tv = new List<string>();
        foreach (Type t in new List<Type>(TF.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)).ToArray())
            foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (t.Name.IndexOf("<VentanaAjustes>", StringComparison.Ordinal) >= 0) tv.AddRange(Codigo.Textos(m));
        foreach (MethodInfo m in TF.GetMethods(TODO | BindingFlags.DeclaredOnly))
            if (m.Name == "VentanaAjustes" || m.Name.IndexOf("<VentanaAjustes>", StringComparison.Ordinal) >= 0)
                tv.AddRange(Codigo.Textos(m));
        Check(tv.Contains("Bajar el volumen de "), "y el letrero dice de que hora a que hora, sacado de la configuracion");

        Console.WriteLine("\n  6) LA RAMPA\n");
        if (rampa == null)
        {
            Check(false, "sin AvanzaRampa no se puede comprobar como baja");
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }
        // Avanza por MILISECUNDOS DE RELOJ, no por numero de llamadas. Se comprueba llamandola de dos
        // maneras que suman el mismo tiempo: mil vueltas de 20 ms y veinte de 1000. Si contara vueltas,
        // una llegaria y la otra no ni de lejos.
        float a = Anda(rampa, 1f, 0.631f, 20, 1000);
        float b = Anda(rampa, 1f, 0.631f, 1000, 20);
        Check(Math.Abs(a - b) < 0.02f, "da lo mismo en pasos chicos que en pasos grandes: cuenta el reloj, no las vueltas (" + a.ToString("0.000") + " vs " + b.ToString("0.000") + ")");
        Check(Math.Abs(a - 0.631f) < 0.01f, "y en 20 segundos ya llego abajo (" + a.ToString("0.000") + ")");

        // No se pasa de la meta ni en un paso enorme ni bajando ni subiendo.
        float baja = (float)rampa.Invoke(null, new object[] { 1f, 0.631f, 999999 });
        Check(baja >= 0.631f - 0.0001f, "bajando no se pasa de la meta (dio " + baja.ToString("0.000") + ")");
        float sube = (float)rampa.Invoke(null, new object[] { 0.631f, 1f, 999999 });
        Check(sube <= 1.0001f, "y subiendo tampoco (dio " + sube.ToString("0.000") + ")");

        // Y ningun paso da un salto que se oiga. A 24 vueltas por segundo, cada una mueve muy poquito:
        // un salto de golpe a mitad de cancion suena a averia, y eso es lo que se quiso evitar.
        float paso = 1f - (float)rampa.Invoke(null, new object[] { 1f, 0.631f, 42 });
        Check(paso > 0f && paso < 0.005f, "cada vuelta mueve menos de media centesima de ganancia: no se oye el escalon (" + paso.ToString("0.00000") + ")");

        Console.WriteLine("\n  7) DE DIA, Y APAGADO, NO CAMBIA NADA\n");
        // El factor de dia tiene que ser 1 CLAVADO. No 0.999: uno, para que la cuenta de dia salga bit a
        // bit la de siempre.
        float quieto = (float)rampa.Invoke(null, new object[] { 1f, 1f, 42 });
        Check(quieto == 1f, "con la meta en 1, la rampa se queda en 1 exacto");

        // Y con noche=0 el modo esta apagado del todo, sea la hora que sea. Se comprueba de verdad, no de
        // palabra: se le fuerza el horario para que AHORA MISMO caiga dentro -asi da igual a que hora se
        // corra la tanda- y se mira que con el interruptor en falso sigue saliendo uno exacto.
        MethodInfo meta = Met("MetaNoche");
        FieldInfo fOn = Campo("nocheOn"), fD = Campo("nocheDesde"), fH = Campo("nocheHasta");
        if (meta != null && fOn != null && fD != null && fH != null)
        {
            object bkOn = fOn.GetValue(null), bkD = fD.GetValue(null), bkH = fH.GetValue(null);
            int h = DateTime.Now.Hour;
            fD.SetValue(null, h); fH.SetValue(null, (h + 1) % 24);   // ahora mismo cae DENTRO del rango
            fOn.SetValue(null, true);
            float dentro = (float)meta.Invoke(null, null);
            Check(Math.Abs(dentro - 0.631f) < 0.001f, "dentro del horario, el factor baja a 0.631 (dio " + dentro.ToString("0.000") + ")");
            fOn.SetValue(null, false);
            float apagado = (float)meta.Invoke(null, null);
            Check(apagado == 1f, "y con noche=0 es 1 exacto aunque la hora caiga dentro (dio " + apagado.ToString("0.000") + ")");
            fOn.SetValue(null, bkOn); fD.SetValue(null, bkD); fH.SetValue(null, bkH);
        }
        else Check(false, "no se encontro el interruptor del modo noche (MetaNoche / nocheOn)");

        LaCompensacion();

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }

    // ============================================================================================
    //  8) LO QUE EL OIDO PIERDE AL BAJAR EL VOLUMEN
    // ============================================================================================
    static string F(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static void Info(string q) { Console.WriteLine("        " + q); }

    // ISO 226:2003, las tablas de la norma: frecuencia, exponente, magnitud lineal y umbral de audicion.
    static readonly double[] IsoF = { 20, 25, 31.5, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500 };
    static readonly double[] IsoA = { 0.532, 0.506, 0.480, 0.455, 0.432, 0.409, 0.387, 0.367, 0.349, 0.330, 0.315, 0.301, 0.288, 0.276, 0.267, 0.259, 0.253, 0.250, 0.246, 0.244, 0.243, 0.243, 0.243, 0.242, 0.242, 0.245, 0.254, 0.271, 0.301 };
    static readonly double[] IsoL = { -31.6, -27.2, -23.0, -19.1, -15.9, -13.0, -10.3, -8.1, -6.2, -4.5, -3.1, -2.0, -1.1, -0.4, 0.0, 0.3, 0.5, 0.0, -2.7, -4.1, -1.0, 1.7, 2.5, 1.2, -2.1, -7.1, -11.2, -10.7, -3.1 };
    static readonly double[] IsoT = { 78.5, 68.7, 59.5, 51.1, 44.0, 37.5, 31.5, 26.5, 22.1, 17.9, 14.4, 11.4, 8.6, 6.2, 4.4, 3.0, 2.2, 2.4, 3.5, 1.7, -1.3, -4.2, -6.0, -5.4, -1.5, 6.0, 12.6, 13.9, 12.3 };
    // Nivel de presion de un tono de la tabla que suena a 'fonios'.
    static double IsoLp(int i, double fonios)
    {
        double af = 4.47e-3 * (Math.Pow(10, 0.025 * fonios) - 1.15) + Math.Pow(0.4 * Math.Pow(10, (IsoT[i] + IsoL[i]) / 10 - 9), IsoA[i]);
        return 10 / IsoA[i] * Math.Log10(af) - IsoL[i] + 94;
    }
    // Cuanto hay que subir el tono i, respecto de 1 kHz, para que al bajar 'baja' dB se oiga bajar lo mismo.
    static double IsoCompensa(int i, double fonios, double baja)
    {
        double c = IsoLp(i, fonios - baja) - IsoLp(i, fonios) + baja;
        double c1k = IsoLp(17, fonios - baja) - IsoLp(17, fonios) + baja;
        return c - c1k;
    }

    static Type TNoche;
    static NAudio.Wave.ISampleProvider Etapa(NAudio.Wave.ISampleProvider s, float db)
    {
        object e = Activator.CreateInstance(TNoche, TODO, null, new object[] { s }, null);
        TNoche.GetField("Db", TODO).SetValue(e, db);
        return (NAudio.Wave.ISampleProvider)e;
    }
    static float[] Lee(NAudio.Wave.ISampleProvider p, int bloque)
    {
        List<float> r = new List<float>(); float[] b = new float[bloque]; int n;
        while ((n = p.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i++) r.Add(b[i]);
        return r.ToArray();
    }
    static float[] Tono(int fs, int ch, double hz, double seg)
    {
        int n = (int)(seg * fs); float[] x = new float[n * ch];
        for (int i = 0; i < n; i++) { float v = (float)(0.25 * Math.Sin(2 * Math.PI * hz * i / fs)); for (int c = 0; c < ch; c++) x[i * ch + c] = v; }
        return x;
    }
    // Amplitud de 'hz' en el canal 0, en los ultimos 'seg' segundos (periodos enteros).
    static double Amp(float[] y, int ch, int fs, double hz, double seg)
    {
        int frames = y.Length / ch, per = Math.Max(1, (int)Math.Round(seg * hz)), n = Math.Min(frames / 2, (int)Math.Round(per * fs / hz));
        double s = 0, c = 0;
        for (int i = frames - n; i < frames; i++) { double w = 2 * Math.PI * hz * i / fs; s += y[i * ch] * Math.Sin(w); c += y[i * ch] * Math.Cos(w); }
        return 2 * Math.Sqrt(s * s + c * c) / n;
    }
    static double Ganancia(int fs, double hz, float db)
    {
        double seg = Math.Max(0.5, 20.0 / hz);
        float[] y = Lee(Etapa(new FuenteNoche(Tono(fs, 2, hz, seg + 0.3), fs, 2), db), 4096);
        return 20 * Math.Log10(Amp(y, 2, fs, hz, seg * 0.5) / 0.25);
    }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static float[] Musica(int fs, double seg, int semilla)
    {
        Random r = new Random(semilla); int n = (int)(seg * fs); float[] x = new float[n * 2]; double a = 0;
        for (int i = 0; i < n; i++)
        {
            a = 0.99 * a + 0.05 * (r.NextDouble() - 0.5);
            double v = 0.3 * Math.Sin(2 * Math.PI * 55 * i / fs) + a;
            x[2 * i] = (float)(v + 0.05 * (r.NextDouble() - 0.5)); x[2 * i + 1] = (float)(0.8 * v + 0.05 * (r.NextDouble() - 0.5));
        }
        return x;
    }

    static void LaCompensacion()
    {
        Console.WriteLine("\n  8) LO QUE EL OIDO PIERDE AL BAJAR: GRAVES Y AGUDOS\n");
        TNoche = TF.GetNestedType("CompensaNoche", TODO);
        Check(TNoche != null, "existe la etapa que devuelve graves y agudos (CompensaNoche)");
        if (TNoche == null) return;
        float gHz = (float)TNoche.GetField("GraveHz", TODO).GetValue(null), gDb = (float)TNoche.GetField("GraveDbPorDb", TODO).GetValue(null);
        float aHz = (float)TNoche.GetField("AgudoHz", TODO).GetValue(null), aDb = (float)TNoche.GetField("AgudoDbPorDb", TODO).GetValue(null);
        Info("graves: estante de primer orden en " + F(gHz, "0") + " Hz, " + F(gDb, "0.00") + " dB por dB; agudos: segundo orden en " + F(aHz, "0") + " Hz, " + F(aDb, "0.00") + " dB por dB");

        // a) Contra la norma. Lo que pide casi no depende de lo fuerte que suene el local.
        double peorNivel = 0;
        for (int i = 0; i < IsoF.Length; i++) peorNivel = Math.Max(peorNivel, Math.Abs(IsoCompensa(i, 60, 4) - IsoCompensa(i, 90, 4)));
        Check(peorNivel < 0.15, "la norma pide casi lo mismo a 60 que a 90 fonios: se aparta " + F(peorNivel, "0.00") + " dB como mucho (por eso vale sin saber lo fuerte que suena el local)");
        foreach (int fs in new int[] { 44100, 48000 })
            foreach (float baja in new float[] { 4f, 8f })
            {
                double peor = 0, suma = 0; int n = 0; string fila = "";
                for (int i = 0; i < IsoF.Length; i++)
                {
                    if (IsoF[i] < 40) continue;
                    double g = Ganancia(fs, IsoF[i], baja), meta = IsoCompensa(i, 75, baja), e = g - meta;
                    peor = Math.Max(peor, Math.Abs(e)); suma += e * e; n++;
                    if (IsoF[i] == 50 || IsoF[i] == 100 || IsoF[i] == 200 || IsoF[i] == 1000 || IsoF[i] == 4000 || IsoF[i] == 12500)
                        fila += F(IsoF[i], "0") + " Hz: " + F(g, "+0.00;-0.00") + " (norma " + F(meta, "+0.00;-0.00") + ")  ";
                }
                double rms = Math.Sqrt(suma / n);
                Info(fs + " Hz, bajando " + F(baja, "0") + " dB: " + fila);
                double tope = baja <= 4 ? 0.25 : 0.5;
                Check(peor <= tope && rms <= 0.1 * baja / 4,
                      fs + " Hz, bajando " + F(baja, "0") + " dB: de 40 Hz a 12,5 kHz se aparta de la norma " + F(rms, "0.000") + " dB de media y " + F(peor, "0.00") + " como mucho");
            }
        double arriba = Ganancia(44100, 16000, 4f), alto = Ganancia(44100, 19000, 4f);
        Info("por encima de 12,5 kHz (la norma no llega): +" + F(arriba, "0.00") + " dB en 16 kHz y +" + F(alto, "0.00") + " en 19 kHz");
        Check(arriba > 0.5 && arriba < 1.2 && alto < 1.2, "arriba del todo sigue subiendo un poco, sin pasarse de 1,2 dB");

        // b) De dia, nada: bit a bit, en estereo y en mono.
        float[] m = Musica(44100, 1.0, 1);
        Check(Igual(Lee(Etapa(new FuenteNoche(m, 44100, 2), 0f), 4096), m), "de dia (0 dB) sale bit a bit lo que entra");
        float[] m1 = new float[m.Length / 2]; for (int i = 0; i < m1.Length; i++) m1[i] = m[2 * i];
        Check(Igual(Lee(Etapa(new FuenteNoche(m1, 44100, 1), 0f), 999), m1), "tambien en un archivo de un canal");
        Check(Igual(Lee(Etapa(new FuenteNoche(m, 44100, 2), float.NaN), 4096), m) && Igual(Lee(Etapa(new FuenteNoche(m, 44100, 2), -3f), 4096), m),
              "un valor roto (NaN o negativo) no hace nada");
        float[] y24 = Lee(Etapa(new FuenteNoche(m, 44100, 2), 24f), 4096), y99 = Lee(Etapa(new FuenteNoche(m, 44100, 2), 99f), 4096);
        Check(Igual(y24, y99), "y uno enorme se queda en lo mas que puede bajar la noche (24 dB)");

        // c) Llega la noche a media cancion, y se va: sin saltos, y al irse vuelve a salir bit a bit.
        {
            int fs = 44100; float[] t = Tono(fs, 2, 50, 3.0);
            object e = Activator.CreateInstance(TNoche, TODO, null, new object[] { new FuenteNoche(t, fs, 2) }, null);
            FieldInfo fDb = TNoche.GetField("Db", TODO);
            NAudio.Wave.ISampleProvider sp = (NAudio.Wave.ISampleProvider)e;
            List<float> y = new List<float>(); float[] b = new float[882 * 2];   // bloques de 20 ms
            int bloque = 0, n;
            while ((n = sp.Read(b, 0, b.Length)) > 0)
            {
                for (int i = 0; i < n; i++) y.Add(b[i]);
                bloque++;
                // la rampa de 20 s del modo noche, en pasos de 42 ms como la da el temporizador, pero rapida
                if (bloque >= 30 && bloque < 80) fDb.SetValue(e, Math.Min(4f, (bloque - 29) * 0.4f));
                else if (bloque >= 100) fDb.SetValue(e, 0f);
            }
            double salto = 0, pendiente = 0;
            for (int i = 2; i < y.Count; i += 2) { double d = Math.Abs(y[i] - y[i - 2]); if (d > salto) salto = d; }
            pendiente = 0.25 * 1.26 * 2 * Math.PI * 50 / fs;   // lo mas que se mueve el tono ya subido 2 dB, de una muestra a otra
            Check(salto <= pendiente * 1.05, "subiendo y bajando no hay saltos: de una muestra a otra, como mucho " + F(salto, "0.00000") + " (el tono solo ya se mueve " + F(pendiente, "0.00000") + ")");
            int desde = (100 * 882 + 2 * 882) * 2;   // 40 ms despues de volver a cero
            bool tal = true; for (int i = desde; i < y.Count && i < t.Length; i++) if (y[i] != t[i]) tal = false;
            Check(tal && y.Count == t.Length, "al amanecer, pasada la rampa, vuelve a salir bit a bit lo que entra");
        }

        // d) Disco continuo: la siguiente sigue con los filtros de la anterior; es lo mismo que una sola etapa.
        {
            int fs = 44100; float[] a = Musica(fs, 1.1, 5), c = Musica(fs, 0.8, 6);
            float[] ac = new float[a.Length + c.Length]; Array.Copy(a, ac, a.Length); Array.Copy(c, 0, ac, a.Length, c.Length);
            float[] junta = Lee(Etapa(new FuenteNoche(ac, fs, 2), 4f), 4096);
            NAudio.Wave.ISampleProvider ea = Etapa(new FuenteNoche(a, fs, 2), 4f), ec = Etapa(new FuenteNoche(c, fs, 2), 4f);
            float[] ya = Lee(ea, 4096);
            TNoche.GetMethod("Sigue", TODO).Invoke(ec, new object[] { ea });
            float[] yc = Lee(ec, 999);
            float[] seguidas = new float[ya.Length + yc.Length]; Array.Copy(ya, seguidas, ya.Length); Array.Copy(yc, 0, seguidas, ya.Length, yc.Length);
            Check(Igual(junta, seguidas), "disco continuo: la siguiente sigue con los filtros de la anterior y da lo mismo, bit a bit, que una sola etapa");
        }

        // e) La configuracion y cuanto se compensa.
        MethodInfo lee = Met("LoadConfigPrefs"), esc = Met("TextoConfig");
        Check(Codigo.Textos(lee).Contains("noche_compensa=") && Codigo.Textos(esc).Contains("noche_compensa="), "noche_compensa= se lee y se guarda en la configuracion");
        MethodInfo pc = Met("NocheCompensa"); FieldInfo fc = Campo("nocheCompensa");
        object bkC = fc.GetValue(null);
        Func<string, float> P = delegate(string x) { return (float)pc.Invoke(null, new object[] { x }); };
        Check(P("100") == 1f && P("50") == 0.5f && P("0") == 0f && P("150") == 1f && P("-3") == 0f, "en %: 100 es lo de la norma, 50 la mitad, 0 nada; lo de fuera se acota");
        fc.SetValue(null, 0.25f);
        Check(P("abc") == 0.25f && P("NaN") == 0.25f, "un texto sin sentido deja lo que habia");
        MethodInfo cdb = Met("NocheCompensaDb");
        FieldInfo fArr = Campo("nocheArrancada"), fFac = Campo("nocheFactor");
        object f = Codigo.FormSinEncender(TF);
        fArr.SetValue(f, true);
        Func<float, float, float> Q = delegate(float factor, float compensa) { fFac.SetValue(f, factor); fc.SetValue(null, compensa); return (float)cdb.Invoke(f, null); };
        float d4 = Q((float)Math.Pow(10, -4 / 20.0), 1f), d2 = Q((float)Math.Pow(10, -4 / 20.0), 0.5f), d0 = Q((float)Math.Pow(10, -4 / 20.0), 0f), dDia = Q(1f, 1f);
        Check(Math.Abs(d4 - 4f) < 0.001f && Math.Abs(d2 - 2f) < 0.001f && d0 == 0f && dDia == 0f,
              "lo que se compensa sigue a lo que baja la noche AHORA: bajando 4 dB, 4 (" + F(d4, "0.000") + "); con noche_compensa=50, 2 (" + F(d2, "0.000") + "); con 0, nada; de dia, nada");
        float dMitad = Q((float)Math.Pow(10, -2 / 20.0), 1f);
        Check(Math.Abs(dMitad - 2f) < 0.001f, "y a media rampa, lo de media rampa (" + F(dMitad, "0.000") + ")");
        fc.SetValue(null, bkC);

        // f) El cableado: la cadena la pone, y la rampa, la siguiente preparada y los ajustes en vivo la mueven.
        MethodInfo arma = Met("ArmaCadena");
        Check(arma != null && Codigo.Llama(arma, "CompensaNoche..ctor") && Codigo.Llama(arma, "NocheCompensaDb"), "cada cancion la lleva en su cadena, con lo que baja la noche al empezar");
        foreach (string quien in new string[] { "AvanzaModoNoche", "AplicaAPreparada", "ApplyNormNow" })
        {
            MethodInfo mq = Met(quien);
            Check(mq != null && Codigo.Llama(mq, "NocheCompensaDb"), quien + " le pasa lo que baja la noche (la rampa, la siguiente preparada y los ajustes en vivo)");
        }
        int l1, e1, l2, e2, l3, e3;
        Codigo.UsosDeCampo(Met("StartOpenedTrack"), "curNoche", out l1, out e1);
        Codigo.UsosDeCampo(Met("PasaRelevo"), "curNoche", out l2, out e2);
        Codigo.UsosDeCampo(Met("DisposePlaybackNow"), "curNoche", out l3, out e3);
        Check(e1 > 0 && e2 > 0 && e3 > 0, "la de la cancion que suena se apunta al empezar, en el relevo sin pausa, y se suelta al parar");
        List<string> prep = Codigo.Llamadas(Met("PreparaSiguienteSinPausa"));
        Check(Codigo.Llama(prep, "SigueA") && Codigo.Llama(Met("SigueA"), "CompensaNoche.Sigue") && Codigo.Llama(Met("SigueA"), "GraveMono.Sigue"),
              "al preparar la siguiente de un disco continuo, sus filtros siguen a los de la que suena");

        // g) Lo que cuesta de noche (de dia no se calcula).
        {
            int fs = 44100; float[] x = Musica(fs, 20.0, 9); float[] b = new float[2205 * 2];
            double sin = double.MaxValue, con = double.MaxValue;
            for (int k = 0; k < 5; k++)
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                NAudio.Wave.ISampleProvider s0 = new FuenteNoche(x, fs, 2); while (s0.Read(b, 0, b.Length) > 0) { }
                sin = Math.Min(sin, sw.Elapsed.TotalMilliseconds);
                sw = System.Diagnostics.Stopwatch.StartNew();
                NAudio.Wave.ISampleProvider s1 = Etapa(new FuenteNoche(x, fs, 2), 4f); while (s1.Read(b, 0, b.Length) > 0) { }
                con = Math.Min(con, sw.Elapsed.TotalMilliseconds);
            }
            double ns = (con - sin) * 1e6 / x.Length;
            Info("de noche cuesta " + F((con - sin) / 20.0, "0.00") + " ms por segundo de musica estereo (" + F(ns, "0.0") + " ns por muestra; Mono, esta maquina)");
            Check(ns < 100, "menos de 100 ns por muestra");
        }
    }

    // Cuantas veces LEE y ESCRIBE un campo una funcion Y las funciones sueltas que el compilador le saca
    // aparte con el nombre entre picos (<VentanaAjustes>c__AnonStorey...). Sin esto, lo que hace un boton
    // -que vive dentro de un delegado- se daria por no hecho.
    static void UsosConAyudantes(string metodo, string campo, out int lee, out int escribe)
    {
        lee = 0; escribe = 0;
        string picos = "<" + metodo + ">";
        List<Type> tipos = new List<Type>();
        tipos.Add(TF);
        tipos.AddRange(TF.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        foreach (Type t in tipos)
            foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (m.Name == metodo || m.Name.IndexOf(picos, StringComparison.Ordinal) >= 0
                    || t.Name.IndexOf(picos, StringComparison.Ordinal) >= 0)
                {
                    int l, e; Codigo.UsosDeCampo(m, campo, out l, out e);
                    lee += l; escribe += e;
                }
    }

    // Llama a la rampa 'vueltas' veces, cada una avanzando 'ms' milisegundos.
    static float Anda(MethodInfo rampa, float desde, float meta, int ms, int vueltas)
    {
        float v = desde;
        for (int i = 0; i < vueltas; i++) v = (float)rampa.Invoke(null, new object[] { v, meta, ms });
        return v;
    }
}
