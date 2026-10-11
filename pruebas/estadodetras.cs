using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

// EL ESTADO SE ESCRIBE DETRAS DE LA PANTALLA (Form1.Datos.cs: SaveState, EscribeEstado, BorraEstado, EstadoSinEscribir).
//
// Los creditos y la cola se guardan con cada moneda, cada cancion que se pone y cada cambio de tema. Guardarlos es
// bajarlos al disco de verdad (archivo de paso, FlushFileBuffers y cambio de un tiron), y en un disco mecanico ocupado
// eso tarda decenas de milisegundos. Se hacia en el hilo de la pantalla: la moneda o el tema nuevo se notaban como un
// tiron. Ahora la pantalla arma el texto y lo encarga; lo escribe un hilo de fondo, en el mismo archivo y de la misma
// forma. Aqui, con la rocola de verdad y su archivo de estado:
//   1) con el disco ocupado (el candado del archivo tomado), guardar vuelve al momento; y lo encargado ya se ve al
//      restaurar aunque aun no este en el disco;
//   2) al soltarse, en el disco queda lo ULTIMO, con los mismos bytes de siempre y la copia de lo anterior;
//   3) un encargo viejo que llega tarde no resucita un estado borrado a proposito, y lo de despues si se guarda;
//   4) los sitios que borran, cierran y restauran van por el camino nuevo, y la escritura sigue bajando al disco.
// Con la rocola de antes, el punto 1 se queda esperando al disco y la bateria falla.
class EstadoDetras
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static object Get(object o, string n) { return TF.GetField(n, T).GetValue(o); }
    static void Set(object o, string n, object v) { TF.GetField(n, T).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) { try { return TF.GetMethod(n, T).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static string Lee(string p) { try { return File.Exists(p) ? File.ReadAllText(p) : ""; } catch { return ""; } }
    static string Linea(string texto, string clave)
    { if (texto == null) return null; foreach (string l in texto.Split('\n')) if (l.TrimEnd('\r').StartsWith(clave)) return l.TrimEnd('\r'); return null; }

    // "El disco ocupado": otro hilo toma el candado del archivo de estado y no lo suelta hasta que se le pida.
    sealed class DiscoOcupado
    {
        readonly object candado; readonly ManualResetEvent tomado = new ManualResetEvent(false), suelta = new ManualResetEvent(false);
        readonly Thread hilo;
        public DiscoOcupado(object c)
        {
            candado = c;
            hilo = new Thread(delegate () { Monitor.Enter(candado); tomado.Set(); suelta.WaitOne(); Monitor.Exit(candado); });
            hilo.IsBackground = true; hilo.Start(); tomado.WaitOne();
        }
        public void Suelta() { suelta.Set(); hilo.Join(); }
    }

    // Guardar en otro hilo, para que la rocola de antes -que espera al disco- no cuelgue la bateria.
    static double GuardaCronometrado(object f, int esperaMs, out bool volvio)
    {
        Stopwatch sw = new Stopwatch();
        Exception fallo = null;
        Thread t = new Thread(delegate () { sw.Start(); try { Call(f, "SaveState"); } catch (Exception e) { fallo = e; } sw.Stop(); });
        t.IsBackground = true; t.Start();
        volvio = t.Join(esperaMs);
        if (fallo != null) Console.WriteLine("        (SaveState lanzo " + fallo.GetType().Name + ")");
        return volvio ? sw.Elapsed.TotalMilliseconds : -1;
    }

    static object Nueva()
    {
        object f = Codigo.FormSinEncender(TF);
        Set(f, "startupReadPending", false);
        Set(f, "stateRestored", true);
        return f;
    }

    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_estadodetras_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        FieldInfo carpeta = TF.GetField("carpetaDatos", T);
        object previa = carpeta.GetValue(null);
        List<DiscoOcupado> sinSoltar = new List<DiscoOcupado>();
        try
        {
            carpeta.SetValue(null, dir);
            object f = Nueva();
            string est = (string)Call(f, "StateFile");
            object candado = Call(null, "CandadoDe", est);
            Check(est == Path.Combine(dir, "rocola_estado.txt") && candado != null, "el archivo de estado y su candado (el mismo que usa la escritura segura)");

            Console.WriteLine("\n  1) CON EL DISCO OCUPADO, LA MONEDA NO ESPERA\n");
            Set(f, "credits", 5);
            bool volvio;
            GuardaCronometrado(f, 5000, out volvio);   // el primero crea lo que haga falta, con el disco libre
            Codigo.EsperaGuardado(TF, f);
            Check(volvio && Linea(Lee(est), "creditos=") == "creditos=5", "con el disco libre, guardar deja los creditos en el archivo");

            DiscoOcupado ocupado = new DiscoOcupado(candado); sinSoltar.Add(ocupado);
            double peor = 0; bool todos = true;
            for (int c = 6; c <= 40; c++)
            {
                Set(f, "credits", c);
                double ms = GuardaCronometrado(f, 1500, out volvio);
                if (!volvio) { todos = false; break; }
                if (ms > peor) peor = ms;
            }
            Console.WriteLine("        35 guardados con el disco ocupado: el peor tardo " + (todos ? peor.ToString("0.00") + " ms" : "MAS DE 1,5 s (se quedo esperando al disco)"));
            Check(todos && peor < 250, "guardar vuelve al momento aunque el disco este ocupado (no espera a que se escriba)");
            if (!todos) { Console.WriteLine("        (la rocola de antes escribe en el hilo que guarda: aqui se queda esperando)"); }
            Check(Linea(Lee(est), "creditos=") == "creditos=5", "mientras tanto el disco sigue con lo anterior (lo nuevo esta encargado)");

            // Lo encargado se ve al restaurar aunque no haya llegado al disco.
            Set(f, "stateRestored", false); Set(f, "estadoAntes", null);
            string[] antes = null;
            try { antes = (string[])Call(f, "EstadoPorRestaurar"); } catch (Exception e) { Console.WriteLine("        " + e.GetType().Name); }
            string crAntes = null;
            if (antes != null) foreach (string l in antes) if (l.StartsWith("creditos=")) crAntes = l;
            Check(crAntes == "creditos=40", "lo que se restauraria es lo ultimo encargado (creditos=40), no lo que aun dice el disco: " + (crAntes ?? "nada"));
            Set(f, "stateRestored", true); Set(f, "estadoAntes", null);

            Console.WriteLine("\n  2) AL SOLTARSE, EN EL DISCO QUEDA LO ULTIMO Y DE LA MISMA FORMA\n");
            ocupado.Suelta(); sinSoltar.Remove(ocupado);
            Codigo.EsperaGuardado(TF, f);
            string t = Lee(est);
            Check(Linea(t, "creditos=") == "creditos=40", "queda el ultimo encargo: " + Linea(t, "creditos="));
            byte[] b = File.Exists(est) ? File.ReadAllBytes(est) : new byte[0];
            Check(b.Length > 3 && b[0] == (byte)'R' && t.StartsWith("ROCOLA-ESTADO v1\r\nts=") && t.EndsWith("\r\n"),
                  "los mismos bytes de siempre: sin marca UTF-8, cabecera y lineas con CRLF");
            // La copia es lo que habia justo antes del ultimo: lo de antes de ocuparse el disco (5) o el encargo que el
            // escritor ya tenia en la mano cuando se ocupo (entre 6 y 39). Nunca el ultimo.
            string bak = Linea(Lee(est + ".bak"), "creditos=");
            int nb; bool bakBien = bak != null && int.TryParse(bak.Substring(9), out nb) && nb >= 5 && nb <= 39;
            Check(bakBien, "y la copia guarda lo anterior, no lo ultimo (" + bak + ")");
            Check(!File.Exists(est + ".tmp"), "sin archivo de paso tirado");
            Check(Call(f, "EstadoSinEscribir") == null, "con todo escrito, restaurar vuelve a leer el disco");

            Console.WriteLine("\n  3) UN ENCARGO VIEJO NO RESUCITA UN ESTADO BORRADO\n");
            int resucitados = 0, vueltas = 25;
            for (int v = 0; v < vueltas; v++)
            {
                DiscoOcupado o = new DiscoOcupado(candado); sinSoltar.Add(o);
                Set(f, "credits", 9);
                GuardaCronometrado(f, 1500, out volvio);   // encargado; el escritor espera al candado
                Thread borra = new Thread(delegate () { try { Call(f, "BorraEstado"); } catch { } });
                borra.IsBackground = true; borra.Start();
                Thread.Sleep(v % 3);   // unas veces el borrado llega antes al candado, otras despues
                o.Suelta(); sinSoltar.Remove(o);
                borra.Join(5000);
                Codigo.EsperaGuardado(TF, f);
                if (File.Exists(est) || File.Exists(est + ".bak")) resucitados++;
                try { File.Delete(est); File.Delete(est + ".bak"); } catch { }
            }
            Console.WriteLine("        " + vueltas + " veces encargar y enseguida borrar con el disco ocupado: " + resucitados + " resucitaron");
            Check(resucitados == 0, "borrar a proposito deja el estado borrado aunque un encargo anterior llegue despues");
            Set(f, "credits", 2);
            GuardaCronometrado(f, 5000, out volvio);
            Codigo.EsperaGuardado(TF, f);
            Check(Linea(Lee(est), "creditos=") == "creditos=2", "y lo que se guarda despues del borrado si llega al disco");

            Console.WriteLine("\n  4) EL CAMINO DE CADA SITIO\n");
            List<string> guarda = Codigo.Nombres(TF.GetMethod("SaveState", T));
            Check(guarda.Contains("EscribeEstado") && !guarda.Contains("EscribeSeguro") && !guarda.Contains("EscribeSeguroYa"),
                  "SaveState encarga el texto y no escribe en el hilo que lo llama");
            // Desde la auditoria del 11-10-2026 el encargo lo valida y lo encola EncolaEstado (tambien los reintentos):
            // el camino es el de EscribeEstado mas el suyo.
            List<string> escribe = Codigo.ConAyudantes(TF, "EscribeEstado");
            escribe.AddRange(Codigo.ConAyudantes(TF, "EncolaEstado"));
            Check(Codigo.Llama(escribe, "GuardaUltimo") && Codigo.Llama(escribe, "EscribeSeguroYa") && Codigo.Llama(escribe, "CandadoDe"),
                  "el encargo va al escritor de fondo (el ultimo gana) y escribe con la escritura segura, bajo el candado del archivo");
            MethodInfo segura = TF.GetMethod("EscribeSeguroYa", T);
            Check(segura != null && Codigo.Llama(segura, "FlushFileBuffers"), "y esa escritura sigue bajando el archivo al disco de verdad");
            string[] borran = { "OnFormClosing", "CheckBusinessDayReset", "RestoreState" };
            foreach (string s in borran)
            {
                List<string> n = Codigo.Nombres(TF.GetMethod(s, T));
                Check(n.Contains("BorraEstado") && !n.Contains("BorraSeguro"), s + " borra el estado por el camino que deja atras los encargos viejos");
            }
            List<string> cierra = Codigo.Nombres(TF.GetMethod("OnFormClosing", T));
            Check(cierra.Contains("EsperaGuardados") || cierra.Contains("WaitDataWrites"), "al cerrar sin borrar se espera a que el ultimo encargo llegue al disco");
            Check(Codigo.Llama(TF.GetMethod("RestoreState", T), "EstadoSinEscribir") && Codigo.Llama(TF.GetMethod("EstadoPorRestaurar", T), "EstadoSinEscribir"),
                  "restaurar (y guardar antes de restaurar) miran primero lo encargado");

            Console.WriteLine("\n  5) LO QUE CUESTA GUARDAR EN LA PANTALLA (medida, no criterio)\n");
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) { Set(f, "credits", i); Call(f, "SaveState"); }
            sw.Stop();
            Codigo.EsperaGuardado(TF, f);
            Console.WriteLine("        200 guardados seguidos: " + (sw.Elapsed.TotalMilliseconds * 1000 / 200).ToString("0") + " microsegundos cada uno en el hilo que guarda");
            Check(Linea(Lee(est), "creditos=") == "creditos=199", "y en el disco queda el ultimo de los 200");
        }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        finally
        {
            foreach (DiscoOcupado o in sinSoltar) try { o.Suelta(); } catch { }
            carpeta.SetValue(null, previa);
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
