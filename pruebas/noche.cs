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
        MethodInfo lee = Met("LoadConfigPrefs"), esc = Met("SaveConfigPrefs");
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

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
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
