using System;
using System.IO;
using System.Reflection;

// 14-10-2026, dos peticiones:
//  1) «Como son muchas IAs, haz que el programa lea las IAs dentro de una carpeta llamada IAS». Los modelos van en IAS\
//     de la carpeta de datos; los sueltos se pasan alli al arrancar (sin borrar ninguno) y se siguen leyendo donde
//     estaban si no se pudieron pasar.
//  2) «Sobre el buscador y el asistente, haz que solo reciba un input a la vez aunque se mantenga presionado el boton».
//     ROJO + ARRIBA y ROJO + ABAJO hacen lo suyo una vez por pulsacion (UnDisparo.cs).
// Con carpetas temporales y el reloj a mano: no toca la carpeta de la rocola ni necesita teclado.
class IasDisparo
{
    static int ok, mal;
    static void Check(bool c, string s) { Console.WriteLine((c ? "  OK    " : "  FALLO ") + s); if (c) ok++; else mal++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF;
    static object Call(string m, params object[] a) { return TF.GetMethod(m, T).Invoke(null, a); }

    static void Ria1(string ruta, int entradas)
    {
        using (BinaryWriter w = new BinaryWriter(File.Create(ruta)))
        {
            w.Write(new byte[] { (byte)'R', (byte)'I', (byte)'A', (byte)'1' }); w.Write(1);
            w.Write(12); w.Write(entradas);
            for (int i = 0; i < 12 * entradas; i++) w.Write(0f);
            for (int i = 0; i < 12; i++) w.Write(-1f - i * 0.1f);
        }
    }
    static string Lee(string ruta) { return File.Exists(ruta) ? File.ReadAllText(ruta) : null; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);

        Console.WriteLine("\n  1) LAS IAS, EN LA CARPETA IAS\n");
        string datos = Path.Combine(Path.GetTempPath(), "rocola_ias_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(datos);
        FieldInfo cache = TF.GetField("carpetaDatos", T);
        cache.SetValue(null, null);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", datos);
        try
        {
            string ias = (string)TF.GetProperty("CarpetaIas", T).GetValue(null, null);
            Check(ias == Path.Combine(datos, "IAS"), "la carpeta se llama IAS y esta en la carpeta de datos");
            Check(!Directory.Exists(ias) && (int)Call("OrdenaIas") == 0 && !Directory.Exists(ias),
                  "sin modelos sueltos no se crea nada");
            Check(((string)Call("RutaLectura", "rocola_ia.bin")) == Path.Combine(ias, "rocola_ia.bin"),
                  "sin ningun modelo, se busca en IAS (no se carga nada, como siempre)");

            File.WriteAllText(Path.Combine(datos, "rocola_ia.bin"), "general A");
            File.WriteAllText(Path.Combine(datos, "rocola_eq_ia.bin"), "eq");
            Ria1(Path.Combine(datos, "rocola_ia_genero_cumbia.bin"), 28);
            File.WriteAllText(Path.Combine(datos, "rocola_ia_genero_cumbia.bin.anterior-1"), "cumbia vieja");
            File.WriteAllText(Path.Combine(datos, "rocola_ia_datos.bin"), "material");          // no es un modelo
            File.WriteAllText(Path.Combine(datos, "rocola_ia_genero_x.binario"), "otra cosa");   // "*.bin" de Windows
            File.WriteAllText(Path.Combine(datos, "rocola_config.txt"), "gpu=1");
            int n = (int)Call("OrdenaIas");
            Check(n == 4, "al arrancar pasa a IAS los cuatro: DSEE, EQ, el de cumbia y su copia de antes (" + n + ")");
            Check(Lee(Path.Combine(ias, "rocola_ia.bin")) == "general A" && Lee(Path.Combine(ias, "rocola_eq_ia.bin")) == "eq"
                  && File.Exists(Path.Combine(ias, "rocola_ia_genero_cumbia.bin"))
                  && Lee(Path.Combine(ias, "rocola_ia_genero_cumbia.bin.anterior-1")) == "cumbia vieja",
                  "estan en IAS, enteros");
            Check(!File.Exists(Path.Combine(datos, "rocola_ia.bin")) && !File.Exists(Path.Combine(datos, "rocola_eq_ia.bin"))
                  && !File.Exists(Path.Combine(datos, "rocola_ia_genero_cumbia.bin")), "y ya no estan sueltos");
            Check(File.Exists(Path.Combine(datos, "rocola_ia_datos.bin")) && File.Exists(Path.Combine(datos, "rocola_ia_genero_x.binario"))
                  && File.Exists(Path.Combine(datos, "rocola_config.txt")), "el material, la configuracion y lo que no es un modelo, en su sitio");
            Check(((string)Call("RutaLectura", "rocola_ia.bin")) == Path.Combine(ias, "rocola_ia.bin")
                  && ((string)Call("RutaLectura", "rocola_eq_ia.bin")) == Path.Combine(ias, "rocola_eq_ia.bin"), "y se leen de IAS");
            Check((int)Call("OrdenaIas") == 0, "la segunda vez no hay nada que pasar");

            // Alguien deja un modelo nuevo suelto, como se hacia antes: manda el nuevo y el de IAS se guarda.
            File.WriteAllText(Path.Combine(datos, "rocola_ia.bin"), "general B");
            Check((int)Call("OrdenaIas") == 1 && Lee(Path.Combine(ias, "rocola_ia.bin")) == "general B"
                  && Lee(Path.Combine(ias, "rocola_ia.bin.anterior-1")) == "general A",
                  "un modelo nuevo dejado suelto pasa a IAS y el de antes se guarda como .anterior-1 (nunca se borra)");
            File.WriteAllText(Path.Combine(datos, "rocola_ia.bin"), "general C");
            Call("OrdenaIas");
            Check(Lee(Path.Combine(ias, "rocola_ia.bin")) == "general C" && Lee(Path.Combine(ias, "rocola_ia.bin.anterior-2")) == "general B"
                  && Lee(Path.Combine(ias, "rocola_ia.bin.anterior-1")) == "general A", "y otro mas, como .anterior-2");

            // Si uno no se pudo pasar (suelto), se sigue leyendo donde esta.
            File.Delete(Path.Combine(ias, "rocola_eq_ia.bin"));
            File.WriteAllText(Path.Combine(datos, "rocola_eq_ia.bin"), "eq suelto");
            Check(((string)Call("RutaLectura", "rocola_eq_ia.bin")) == Path.Combine(datos, "rocola_eq_ia.bin"),
                  "un modelo que se quedo suelto se sigue leyendo alli");

            // Los de genero: de IAS; si el de IAS no sirve, el suelto.
            FieldInfo gen = TF.GetField("dseeGeneros", T);
            Action limpia = delegate { object d = gen.GetValue(null); d.GetType().GetMethod("Clear").Invoke(d, null); };
            limpia(); Call("CargaModelosPorGenero");
            Check((int)Call("ModelosPorGenero") == 1, "el modelo de cumbia se carga desde IAS");
            Ria1(Path.Combine(datos, "rocola_ia_genero_salsa.bin"), 28);                                    // suelto
            File.WriteAllBytes(Path.Combine(ias, "rocola_ia_genero_bolero.bin"), new byte[] { 1, 2, 3 });   // roto en IAS
            Ria1(Path.Combine(datos, "rocola_ia_genero_bolero.bin"), 28);                                   // y bueno suelto
            limpia(); Call("CargaModelosPorGenero");
            Check((int)Call("ModelosPorGenero") == 3, "tambien los sueltos, y si el de IAS esta roto vale el suelto ("
                  + Call("ModelosPorGenero") + ")");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", null);
            cache.SetValue(null, null);
            try { Directory.Delete(datos, true); } catch { }
        }
        Check(Codigo.Llama(TF.GetMethod("OnShown", T), "OrdenaIas") && Codigo.Llama(TF.GetMethod("OnShown", T), "RutaLectura"),
              "al arrancar se ordenan y luego se cargan");
        Check(Codigo.Llama(TF.GetMethod("CargaModelosPorGenero", T), "CarpetasIas") && Codigo.Llama(TF.GetMethod("RutaLectura", T), "CarpetasIas"),
              "el DSEE, el EQ y los de genero se buscan en las mismas carpetas");

        Console.WriteLine("\n  2) UN DISPARO POR PULSACION\n");
        Type TU = asm.GetType("Rocola.UnDisparo", true);
        Func<object> nuevo = delegate { return Activator.CreateInstance(TU, true); };
        MethodInfo aprieta = TU.GetMethod("Aprieta", T), suelta = TU.GetMethod("Suelta", T), pide = TU.GetMethod("Pide", T);
        Func<object, int, bool> AP = delegate (object u, int t) { aprieta.Invoke(u, new object[] { t }); return (bool)pide.Invoke(u, null); };
        Action<object, int> SU = delegate (object u, int t) { suelta.Invoke(u, new object[] { t }); };

        object d1 = nuevo();
        Check(AP(d1, 1000), "la primera pulsacion abre");
        int veces = 0;
        for (int t = 1500; t < 6000; t += 33) if (AP(d1, t)) veces++;   // la repeticion del teclado, 4,5 s apretados
        Check(veces == 0, "manteniendo los botones 4,5 s, con la repeticion del teclado, no vuelve a hacer nada (" + veces + ")");
        Check(!AP(d1, 6000 + 900), "ni aunque la repeticion tarde casi un segundo en empezar (el maximo de Windows)");
        SU(d1, 7000);
        Check(!AP(d1, 7000 + 20), "un rebote del boton (soltar 20 ms) no cuenta");
        SU(d1, 7100);
        Check(AP(d1, 7100 + 200), "soltar y volver a apretar, si: cierra");
        Check(!AP(d1, 7400), "y otra vez tomada mientras se mantenga");
        // Un mapeador de botones que manda apretar-soltar seguido mientras se mantiene.
        object d2 = nuevo(); AP(d2, 100); veces = 0;
        for (int t = 130; t < 3000; t += 30) { SU(d2, t); if (AP(d2, t + 5)) veces++; }
        Check(veces == 0, "con un mapeador que suelta y aprieta cada 30 ms, tampoco (" + veces + ")");
        // El soltar se lo quedo otra ventana.
        object d3 = nuevo(); AP(d3, 100);
        Check(AP(d3, 100 + 1600), "si el aviso de soltar se perdio, una pulsacion tras 1,6 s sin nada es nueva");
        object d4 = nuevo(); AP(d4, int.MaxValue - 10); SU(d4, int.MaxValue - 5);
        Check(AP(d4, unchecked(int.MaxValue + 200)), "aunque el reloj de Windows de la vuelta");
        object d5 = nuevo();
        Check(AP(d5, 0) && !AP(d5, 30), "con el reloj en 0 tambien");

        MethodInfo cmd = TF.GetMethod("ProcessCmdKey", T);
        Check(Codigo.Llama(cmd, "ApuntaDisparos") && Codigo.Llama(TF.GetMethod("OnKeyUp", T), "ApuntaDisparos"),
              "cada pulsacion y cada soltar de los botones se apunta");
        Check(Codigo.Llama(cmd, "get_disparoBuscador") && Codigo.Llama(cmd, "ToggleSearch"), "ROJO + ARRIBA (buscador) pasa por su seguro");
        MethodInfo entrada = TF.GetMethod("AsistenteEntrada", T);
        if (entrada != null) Check(Codigo.Llama(entrada, "get_disparoAyuda") && Codigo.Llama(entrada, "AbreAsistente"), "ROJO + ABAJO (ayuda) pasa por el suyo");
        else Check(TF.GetMethod("AbreAsistente", T) == null, "sin tarjeta grafica no hay ayuda: solo el buscador");
        object f = Codigo.FormSinEncender(TF);
        object b1 = TF.GetProperty("disparoBuscador", T).GetValue(f, null), b2 = TF.GetProperty("disparoAyuda", T).GetValue(f, null);
        Check(b1 != null && b2 != null && !object.ReferenceEquals(b1, b2), "cada combinacion tiene el suyo, tambien en un formulario sin encender");

        Console.WriteLine(mal == 0 ? "PASS iasdisparo (" + ok + " comprobaciones)" : "FAIL iasdisparo: " + mal + " fallos");
        return mal;
    }
}
