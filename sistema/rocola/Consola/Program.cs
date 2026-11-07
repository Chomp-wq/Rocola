// LA ROCOLA SIN PANTALLA (Rocola OS, etapa 2). Toca una cola de canciones con el motor, como la rocola de XP:
//   0) al arrancar lee rocola_config.txt (los mismos ajustes del sonido que XP) y las tablas de lo aprendido, de la carpeta de los datos;
//   1) mide cada cancion de la cola que aun no lo este (sonoridad, picos y P90: el repaso de XP), para nivelarla "al limite";
//   2) cada una con la cadena ENTERA de XP (ArmaCadena) y, cuando se puede, seguidas en la misma salida sin hueco (el relevo sin
//      pausa de XP: la siguiente se prepara 12 s antes; las pistas seguidas de un disco, sin fundido); 16 bits con dither;
//   3) por ALSA (o a un WAV con --wav, para probar sin tarjeta); al acabar cada una se guarda lo aprendido, como en XP.
// Uso: rocola-consola [--wav salida.wav] [--dispositivo default] [--biblioteca] [cancion1.mp3 cancion2.wav ...]
//      rocola-consola --rocola [--texto] [--dispositivo default]   la rocola con sus botones: en la pantalla (KMS y OpenGL ES,
//                     VistaGrafica.cs) o, con --texto o si no hay pantalla grafica, en la consola (VistaTexto.cs)
//      rocola-consola --captura salida.ppm [--tam 1024x768] [--boton poner ...]   un cuadro de la pantalla, fuera de pantalla (pruebas)
//   --biblioteca: la cola es la biblioteca entera (la carpeta "musica" junto a los datos y las de "dir="), disco por disco.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static class Programa
    {
        internal static int Main(string[] args)
        {
            string wav = null, dispositivo = "default", captura = null, tam = "1024x768"; bool biblioteca = false, rocola = false, texto = false; string guion = null; List<string> lista = new List<string>(), botones = new List<string>();
            // --version: la version del programa ("rocola-consola programa N (commit)"); la comprueba rocola-actualiza antes de aceptarla.
            if (args.Length > 0 && args[0] == "--version") { Console.WriteLine("rocola-consola programa " + Version.Numero + " (" + Version.Commit + ")"); return 0; }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--busca")   // prueba del buscador (BuscadorFlexible.cs): cada argumento es una busqueda; dice los 8 primeros y cuanto tardo
                {
                    Form1 mb = new Form1(); mb.Arranca(); mb.LeeBiblioteca();
                    if (Environment.GetEnvironmentVariable("ROCOLA_LETRAS_SIN_PAUSA") == "1") mb.EsperaLetrasMp3();   // (que el repaso de las letras de los MP3 acabe antes)
                    for (int j = i + 1; j < args.Length; j++)
                    {
                        mb.Busca(args[j]);   // (la primera vez arma lo que guarda; se mide la segunda, como al teclear)
                        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                        List<Form1.Resultado> r = mb.BuscaSinGuardar(args[j]);
                        sw.Stop();
                        Console.WriteLine("«" + args[j] + "»: " + mb.TotalUltimaBusqueda + " resultados, " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms");
                        for (int k = 0; k < Math.Min(8, r.Count); k++) Console.WriteLine("   " + (r[k].Tipo == 0 ? "artista " : r[k].Tipo == 1 ? "disco   " : "cancion ") + r[k].Texto + (string.IsNullOrEmpty(r[k].Detalle) ? "" : "  (" + r[k].Detalle + ")"));
                        if (Form1.Palabras(args[j]) >= Form1.PalabrasDeFrase && mb.HayLlaveGenius)
                        {   // por la letra en internet (L1): lo que contesta Genius, ya llevado a la biblioteca
                            mb.PideBusquedaLetra(args[j]); List<Form1.Resultado> le = null;
                            for (int w = 0; w < 120 && (le = mb.ResultadosPorLetra(args[j])) == null; w++) System.Threading.Thread.Sleep(100);
                            if (le != null) foreach (Form1.Resultado x in le) Console.WriteLine("   letra   " + x.Texto + "  (" + x.Detalle + ")" + (x.Tipo == 5 ? " [no esta]" : ""));
                        }
                    }
                    return 0;
                }
                else if (args[i] == "--mide-portadas") { string[] rs = new string[args.Length - i - 1]; Array.Copy(args, i + 1, rs, 0, rs.Length); Rocola.Pantalla.Caratulas.Mide(rs, 256); return 0; }
                else if (args[i] == "--mide-repaso")   // prueba: cuanto tarda medir cada archivo (no publica nada)
                {
                    Form1 mr = new Form1(); mr.Arranca();
                    for (int j = i + 1; j < args.Length; j++) Console.WriteLine(mr.PruebaRepaso(args[j]));
                    return 0;
                }
                else if (args[i] == "--entrena-ia")   // IA1: entrenar la IA del DSEE ya, con lo cosechado (para medir cuanto tarda; IaNueva.cs)
                {
                    Form1 me = new Form1(); me.Arranca();
                    Console.WriteLine(me.EntrenaAhora());
                    while (me.TextoEntreno().StartsWith("Entrenando")) System.Threading.Thread.Sleep(500);
                    Console.WriteLine(me.TextoEntreno());
                    me.Cierra();
                    return 0;
                }
                else if (args[i] == "--wav" && i + 1 < args.Length) wav = args[++i];
                else if (args[i] == "--dispositivo" && i + 1 < args.Length) dispositivo = args[++i];
                else if (args[i] == "--biblioteca") biblioteca = true;
                else if (args[i] == "--rocola") rocola = true;
                else if (args[i] == "--texto") texto = true;
                else if (args[i] == "--guion" && i + 1 < args.Length) guion = args[++i];
                else if (args[i] == "--captura" && i + 1 < args.Length) captura = args[++i];
                else if (args[i] == "--tam" && i + 1 < args.Length) tam = args[++i];
                else if (args[i] == "--cuadros" && i + 1 < args.Length) botones.Add("cuadros=" + args[++i]);   // cuantos cuadros de 50 ms antes de la captura (26)
                else if (args[i] == "--boton" && i + 1 < args.Length) botones.Add(args[++i]);
                else if (args[i] == "--tecla" && i + 1 < args.Length) botones.Add("#" + args[++i]);
                else lista.Add(Path.GetFullPath(args[i]));
            }
            if (captura != null) return Captura(captura, tam, botones);
            if (rocola)
            {
                // V29: la pantalla primero, con el cielo y el nombre de la rocola, y despues lo que tarda (los datos y la biblioteca)
                IVista vista = null;
                if (!texto)
                {
                    try { VistaGrafica g = new VistaGrafica(new Rocola.Pantalla.Kms(), VistaGrafica.BuscaLetra()); Console.WriteLine("Pantalla: " + g.Descripcion); vista = g; g.Presentacion("Despertando la rocola..."); }
                    catch (Exception e) { Console.Error.WriteLine("Sin pantalla grafica (" + e.Message + "): en modo texto"); }
                }
                Form1 m = new Form1(); m.Arranca();
                if (vista == null) vista = new VistaTexto();
                int r;
                Controlador ctl = new Controlador(m, dispositivo); ctl.Guion = guion;
                using System.Runtime.InteropServices.PosixSignalRegistration apagar = System.Runtime.InteropServices.PosixSignalRegistration.Create(
                    System.Runtime.InteropServices.PosixSignal.SIGTERM, delegate (System.Runtime.InteropServices.PosixSignalContext ctx) { ctx.Cancel = true; ctl.Apaga(); });
                using (vista) r = ctl.Corre(vista);
                m.Cierra();
                return r;
            }
            if (lista.Count == 0 && !biblioteca) { Console.Error.WriteLine("Uso: rocola-consola [--wav salida.wav] [--dispositivo default] [--biblioteca] canciones..."); return 2; }
            Form1 motor = new Form1();   // los ajustes de fabrica de XP...
            motor.Arranca();             // ...y encima los de rocola_config.txt y lo aprendido (las tablas), como al arrancar la de XP
            if (wav != null) Form1.nocheOn = false;   // a un archivo: sin modo noche (sale igual a cualquier hora; no es el bar)
            Console.WriteLine(motor.Resumen());
            List<Form1.Disco> discos = motor.LeeBiblioteca();
            Console.WriteLine("Biblioteca (" + motor.CarpetaMusica + "): " + discos.Count + " discos");
            if (biblioteca) foreach (Form1.Disco d in discos) lista.AddRange(d.Rutas);
            foreach (string ruta in lista)
            {
                if (!motor.MideSiFalta(ruta)) Console.WriteLine("  (no se pudo medir " + Path.GetFileName(ruta) + ": suena con la regla de las canciones sin medir)");
                motor.Encola(ruta);
            }
            WaveFileWriter escritor = null;
            ISampleProvider s;
            while ((s = motor.EmpiezaSiguiente()) != null)
            {
                string dicha = null;
                Action cuenta = delegate
                {
                    string ahora = motor.Sonando;
                    if (ahora != null && ahora != dicha) { dicha = ahora; Console.WriteLine("Suena: " + Path.GetFileName(ahora) + "  (nivelado " + motor.SonandoGananciaDb.ToString("+0.0;-0.0") + " dB)"); }
                };
                cuenta();
                try
                {
                    if (wav != null)
                    {
                        if (escritor == null) escritor = new WaveFileWriter(wav, new WaveFormat(s.WaveFormat.SampleRate, 16, 2));
                        IWaveProvider d = Form1.A16Bits(s); byte[] b = new byte[16384]; int n; long cuadros = 0;
                        while ((n = d.Read(b.AsSpan())) > 0)
                        {
                            escritor.Write(b, 0, n);
                            cuadros += n / 4;
                            motor.Atiende(cuadros);   // lo escrito ya "sono"
                            cuenta();
                        }
                    }
                    else
                    {
                        using (SalidaAlsa salida = new SalidaAlsa(s, dispositivo))
                        {
                            salida.Empieza();
                            while (salida.Sonando) { Thread.Sleep(100); motor.Atiende(salida.CuadrosOidos); cuenta(); }
                            if (salida.Recuperaciones > 0) Console.WriteLine("  (cortes recuperados: " + salida.Recuperaciones + ")");
                            if (salida.ColasSuaves > 0) Console.WriteLine("  (acabo a media onda: cola de 10 ms)");
                        }
                    }
                }
                catch (Exception e) { Console.Error.WriteLine("Fallo la salida: " + e.Message); }
                motor.AcaboLaSalida();   // lo aprendido de lo que sonaba
            }
            if (escritor != null) escritor.Dispose();
            motor.Cierra();   // lo aprendido, al disco
            return 0;
        }

        // UN CUADRO DE LA PANTALLA SIN PANTALLA: la rocola con su biblioteca, los botones dados (moneda, derecha, poner...) y la imagen.
        private static int Cuadros(List<string> botones)
        {
            foreach (string b in botones) if (b.StartsWith("cuadros=")) return int.Parse(b.Substring(8));
            return 26;
        }
        private static int Captura(string ruta, string tam, List<string> botones)
        {
            string[] t = tam.Split('x');
            Form1 m = new Form1(); m.Arranca();
            Rocola.Pantalla.FueraDePantalla fp = new Rocola.Pantalla.FueraDePantalla(int.Parse(t[0]), int.Parse(t[1]));
            Rocola.Pantalla.Caratulas.AlMomento = Environment.GetEnvironmentVariable("ROCOLA_PORTADAS_EN_FONDO") == null;   // las capturas, con sus portadas desde el primer cuadro
            VistaGrafica v = new VistaGrafica(fp, VistaGrafica.BuscaLetra());
            Console.WriteLine("Pantalla: " + v.Descripcion);
            if (botones.Contains("presentacion")) { v.Presentacion("Leyendo tu musica..."); fp.Captura(ruta); Console.WriteLine("Captura: " + ruta); return 0; }   // V29
            Controlador c = new Controlador(m, "null");
            c.Prepara();
            // LA PELICULA (ROCOLA_PELICULA=N, pruebas del movimiento): cada boton por separado y N cuadros despues de cada uno, cada cuadro
            // guardado (ruta-000.ppm...); con ROCOLA_RELOJ_FIJO y ROCOLA_RELOJ_PASO el tiempo avanza igual en cada cuadro.
            int pelicula = int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_PELICULA"), out int np) ? np : 0, cuadroPeli = 0;
            if (pelicula > 0) { v.SiempreDibuja = true; for (int i = 0; i < 6; i++) v.Dibuja(c); }
            foreach (string b in botones)
            {
                if (pelicula > 0 && !b.StartsWith("cuadros=")) { Aplica(b, m, c, v); for (int i = 0; i < pelicula; i++) { v.Dibuja(c); fp.Captura(ruta + "-" + (cuadroPeli++).ToString("000") + ".ppm"); } continue; }
                if (pelicula == 0) Aplica(b, m, c, v);
            }
            if (pelicula > 0) { m.Cierra(); Console.WriteLine("Pelicula: " + cuadroPeli + " cuadros"); return 0; }
            Despues(ruta, botones, m, c, v, fp);
            return 0;
        }
        private static void Aplica(string b, Form1 m, Controlador c, VistaGrafica v)
        {
                if (b.StartsWith("cuadros=")) return;
                else if (b == "atraccion") c.FuerzaAtraccion();
                else if (b == "rojo") c.RojoDePrueba = true;
                else if (b == "diagnostico") { c.DiagnosticoDePrueba(v); Console.Write(c.UltimoDiagnostico); }   // el ROJO mantenido (las pistas del rojo)
                else if (b == "noche") { c.AtraccionDesfase = 15; c.FuerzaAtraccion(); }
                else if (b == "mosaico") { c.AtraccionDesfase = 32; c.FuerzaAtraccion(); }   // V20: la atraccion, ya en el mosaico   // la atraccion, ya en lo mas pedido esta noche
                else if (b == "espera") { v.Dibuja(c); System.Threading.Thread.Sleep(500); }   // como alguien que mira antes de apretar
                else if (b.StartsWith("ajuste:")) { c.AbreAjustes(); IList<Form1.Ajuste> la = c.ListaAjustes; for (int ia = 0; ia < la.Count; ia++) if (la[ia].Clave == b.Substring(7)) { c.AjusteSel = ia; break; } }   // (pruebas) la hoja de ese ajuste
                else if (b == "calma") c.FuerzaReposo();   // (pruebas) 20 s sin botones: sin nada sonando, la pared en calma (Color vivo entero)
                else if (b == "spotify") c.EsperaSpotify(v, 6000);
                else if (b.StartsWith("escribe:")) c.EscribeDePrueba(b.Substring(8));   // (pruebas) letras en el buscador abierto   // (pruebas) 6 s atendiendo Spotify: su programa, la biblioteca, el buscador
                else if (b == "reposo")
                {
                    // algo sonando y cinco en la cola (sin salida: solo para ver la pantalla), y 20 s sin botones
                    m.Moneda(); m.Moneda();
                    foreach (Form1.Disco dd in c.discos) foreach (string r in dd.Rutas) if (!dd.Videos && m.EnCola < 6) m.Pide(r);
                    m.EmpiezaSiguiente();
                    c.FuerzaReposo();
                }
                else if (b == "sonando")
                {
                    // algo sonando y cinco en la cola, en la pared (sin salida: solo para ver la pantalla)
                    m.Moneda(); m.Moneda();
                    foreach (Form1.Disco dd in c.discos) foreach (string r in dd.Rutas) if (!dd.Videos && m.EnCola < 6) m.Pide(r);
                    System.Diagnostics.Stopwatch se = System.Diagnostics.Stopwatch.StartNew();
                    m.EmpiezaSiguiente();
                    if (Environment.GetEnvironmentVariable("ROCOLA_MIDE_EMPIEZA") != null)
                    {   // (pruebas) cuanto tarda empezar una cancion en el hilo que la pide: la primera y cuatro mas
                        Console.WriteLine("Empezar la 1: " + se.Elapsed.TotalMilliseconds.ToString("0.0") + " ms");
                        for (int q = 2; q <= 5; q++) { m.AcaboLaSalida(); se.Restart(); m.EmpiezaSiguiente(); Console.WriteLine("Empezar la " + q + ": " + se.Elapsed.TotalMilliseconds.ToString("0.0") + " ms"); }
                    }
                }
                else if (b.StartsWith("#")) c.PulsaTecla(int.Parse(b.Substring(1)));   // --tecla 119: F8 (el asistente), o cualquier codigo de Windows
                else if (b.StartsWith("regresar+")) c.Pulsa((Boton)Enum.Parse(typeof(Boton), b.Substring(9), true), true);   // con REGRESAR mantenido
                else c.Pulsa((Boton)Enum.Parse(typeof(Boton), b, true));
        }
        private static void Despues(string ruta, List<string> botones, Form1 m, Controlador c, VistaGrafica v, Rocola.Pantalla.FueraDePantalla fp)
        {
            // BANCO DE MEDIDA (ROCOLA_BANCO=N): N cuadros seguidos de esta escena; cuanto tarda cada uno (preparar y dibujar, con el
            // dibujo por software de Mesa si no hay tarjeta: no es el A4) y cuanta memoria pide. No guarda nada.
            string banco = Environment.GetEnvironmentVariable("ROCOLA_BANCO");
            if (banco != null)
            {
                int nb = int.Parse(banco); v.SiempreDibuja = true;
                for (int i = 0; i < 30; i++) v.Dibuja(c);   // que se llenen las caches
                Perfil.Total.Clear(); Perfil.Bytes.Clear(); Perfil.Puntos.Clear(); Rocola.Pantalla.Dibujo.CuentaRelleno = true; Rocola.Pantalla.Dibujo.Relleno = 0; Rocola.Pantalla.Dibujo.RellenoMezclado = 0;
                List<double> ms = new List<double>(); long bytes = 0; int gc0 = GC.CollectionCount(0); Rocola.Pantalla.Dibujo.Dibujos = 0; Rocola.Pantalla.Dibujo.Vertices = 0;
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                for (int i = 0; i < nb; i++)
                {
                    long a = GC.GetAllocatedBytesForCurrentThread(); sw.Restart();
                    v.Dibuja(c);
                    ms.Add(sw.Elapsed.TotalMilliseconds); bytes += GC.GetAllocatedBytesForCurrentThread() - a;
                    if (Perfil.Activo && sw.Elapsed.TotalMilliseconds > 4) { var lc = new List<KeyValuePair<string, double>>(Perfil.Cuadro); lc.Sort((x, y) => y.Value.CompareTo(x.Value)); var sb = new System.Text.StringBuilder(); for (int q = 0; q < Math.Min(5, lc.Count); q++) sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:0.0}; ", lc[q].Key, lc[q].Value)); Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "  LENTO cuadro {0}: {1:0.0} ms ({2})", i, sw.Elapsed.TotalMilliseconds, sb)); }
                }
                ms.Sort();
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "BANCO {0}x{10}: mediana {1:0.00} ms, p95 {2:0.00}, peor {3:0.00}, {4:0.0} KB por cuadro, {5} recolecciones, {6} dibujos y {7} vertices por cuadro, {8:0.00} pantallas pintadas ({9:0.00} mezcladas)", Rocola.Pantalla.Dibujo.Actual.Ancho, ms[ms.Count / 2], ms[(int)(ms.Count * 0.95)], ms[ms.Count - 1], bytes / 1024.0 / nb, GC.CollectionCount(0) - gc0, Rocola.Pantalla.Dibujo.Dibujos / nb, Rocola.Pantalla.Dibujo.Vertices / nb, Rocola.Pantalla.Dibujo.Relleno / nb / ((double)Rocola.Pantalla.Dibujo.Actual.Ancho * Rocola.Pantalla.Dibujo.Actual.Alto), Rocola.Pantalla.Dibujo.RellenoMezclado / nb / ((double)Rocola.Pantalla.Dibujo.Actual.Ancho * Rocola.Pantalla.Dibujo.Actual.Alto), Rocola.Pantalla.Dibujo.Actual.Alto));
                if (Perfil.Activo) { var l = new List<KeyValuePair<string, double>>(Perfil.Total); l.Sort((a, b) => b.Value.CompareTo(a.Value)); foreach (var kv in l) Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "   {0,-28} {1,7:0.00} ms {2,7:0.0} KB {3,6:0.00} pantallas", kv.Key, kv.Value / nb, (Perfil.Bytes.TryGetValue(kv.Key, out long kb) ? kb : 0) / 1024.0 / nb, (Perfil.Puntos.TryGetValue(kv.Key, out double pp) ? pp : 0) / nb / ((double)Rocola.Pantalla.Dibujo.Actual.Ancho * Rocola.Pantalla.Dibujo.Actual.Alto))); }
                m.Cierra(); return;
            }
            // que termine de aparecer lo que se anima (la atraccion, el reposo, la estrella que vuela): unos cuadros durante 1,3 s
                for (int i = 0; i < Cuadros(botones); i++) { m.PublicaMedidas(); v.Dibuja(c); System.Threading.Thread.Sleep(50); }   // lo medido en fondo, a las tablas (como el bucle de la rocola)   // que termine de aparecer
            v.Dibuja(c);
            fp.Captura(ruta);
            // ROCOLA_CAPTURA_DOBLE=VARIABLE: el mismo cuadro otra vez con esa variable puesta, en ruta.b.ppm (comparar un ahorro con su
            // forma de antes sin que cambie lo que se ve: mismo disco, mismo reloj)
            string doble = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_DOBLE");
            if (doble != null) { Environment.SetEnvironmentVariable(doble, "1"); if (doble == "ROCOLA_SIN_LOTES") { Rocola.Pantalla.Dibujo.BrilloCuadrado = true; Rocola.Pantalla.Dibujo.TirasEnteras = true; } if (doble == "ROCOLA_TIRAS_ENTERAS") Rocola.Pantalla.Dibujo.TirasEnteras = true; VistaGrafica.RepiteCuadro(); v.Dibuja(c); fp.Captura(ruta + ".b.ppm"); }
            m.GuardaEstado(); m.Cierra();   // lo aprendido y las estadisticas, como al cerrar la rocola
            Console.WriteLine("Captura: " + ruta);
        }
    }
}
