using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

// EL ARCHIVO CON EL QUE SE ENTRENA EL DSEE TIENE QUE ARRANCAR.
//
// El entrenador vive fuera de la rocola: se copian dos archivos a una USB (rocola_ia_datos.bin y
// rocola_eq.txt), se llevan a una PC con fuerza y alli se arrastran sobre entrenar_dsee.bat. Ese .bat es la
// unica puerta de entrada, y estaba roto de una forma especialmente fea: se abria, ensenaba el cartel, y al
// arrastrarle los archivos la ventana se cerraba de golpe sin decir nada.
//
// Estaba guardado con saltos de linea de UNIX. cmd.exe busca las etiquetas -:fin, :prueba- recorriendo el
// archivo por posicion, y con saltos de linea de Unix no las encuentra: cuando eso pasa NO avisa, aborta el
// archivo a media ejecucion y la ventana desaparece. Al editarlo desde Linux -que es de donde salen estos
// cambios- se cuela solo y no se ve en ninguna parte.
//
// Aqui se comprueba eso y las otras tres formas que tenia de cerrarse en silencio: que no se use la
// expansion retardada (se come los signos de admiracion de las rutas que se arrastran), que toda etiqueta a
// la que salta exista de verdad, y que se llegue SIEMPRE a la pausa del final. Y ademas que arrastrar la
// carpeta de la rocola no acabe recorriendo la biblioteca de musica entera.
class Entrenador
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static int Main(string[] argv)
    {
        string bat = Path.Combine("..", Path.Combine("ia", "entrenar_dsee.bat"));
        string py = Path.Combine("..", Path.Combine("ia", "entrenar_dsee.py"));

        Console.WriteLine("\n  1) EL ARCHIVO ESTA Y ES EL QUE WINDOWS SABE LEER\n");
        Check(File.Exists(bat), "esta el .bat con el que se entrena");
        Check(File.Exists(py), "y a su lado el motor de verdad (entrenar_dsee.py)");
        if (!File.Exists(bat) || !File.Exists(py)) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        byte[] crudo = File.ReadAllBytes(bat);
        int crlf = 0, lfSolo = 0;
        for (int i = 0; i < crudo.Length; i++)
            if (crudo[i] == (byte)'\n') { if (i > 0 && crudo[i - 1] == (byte)'\r') crlf++; else lfSolo++; }
        Console.WriteLine("     saltos de linea: " + crlf + " de Windows, " + lfSolo + " de Unix");
        Check(lfSolo == 0 && crlf > 0,
              "va con saltos de linea de WINDOWS: con los de Unix, cmd no encuentra las etiquetas y se cierra solo");
        Check(crudo.Length > 0 && crudo[crudo.Length - 1] == (byte)'\n',
              "y termina en un salto de linea (sin el, la ultima etiqueta puede no verse)");

        string txt = Encoding.UTF8.GetString(crudo);
        string[] lineas = txt.Replace("\r\n", "\n").Split('\n');

        // ---------------------------------------------------------------------------------
        Console.WriteLine("\n  2) NO SE COME LAS RUTAS QUE SE LE ARRASTRAN\n");
        // La expansion retardada hace que cmd trate los signos de admiracion como si fueran variables. Una
        // carpeta "Musica!" o "Rocola (2)!" llegaba con el nombre cambiado, no se encontraba el archivo, y
        // el que arrastraba no tenia forma de saber por que.
        bool retardada = false;
        foreach (string l in lineas)
        {
            string t = l.Trim();
            if (t.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) || t.StartsWith("::")) continue;
            if (t.IndexOf("enabledelayedexpansion", StringComparison.OrdinalIgnoreCase) >= 0) retardada = true;
        }
        Check(!retardada, "no usa expansion retardada, que se comeria los signos de admiracion de una ruta");

        // ---------------------------------------------------------------------------------
        Console.WriteLine("\n  3) NO SALTA A NINGUN SITIO QUE NO EXISTA\n");
        // Un salto a una etiqueta que no esta es justo lo que cierra la ventana de golpe.
        List<string> etiquetas = new List<string>();
        foreach (string l in lineas)
        {
            string t = l.Trim();
            if (t.StartsWith(":") && !t.StartsWith("::"))
            {
                int corte = t.IndexOfAny(new char[] { ' ', '\t' });
                etiquetas.Add((corte > 0 ? t.Substring(1, corte - 1) : t.Substring(1)).ToLowerInvariant());
            }
        }
        List<string> perdidos = new List<string>();
        int saltos = 0;
        foreach (string l in lineas)
        {
            string t = l.Trim().ToLowerInvariant();
            string destino = null;
            if (t.StartsWith("goto ")) destino = t.Substring(5).Trim();
            else if (t.StartsWith("call :")) destino = t.Substring(6).Trim();
            if (destino == null) continue;
            int esp = destino.IndexOfAny(new char[] { ' ', '\t' });
            if (esp > 0) destino = destino.Substring(0, esp);
            destino = destino.TrimStart(':');
            if (destino == "eof") continue;                 // :eof lo entiende cmd solo
            saltos++;
            if (!etiquetas.Contains(destino)) perdidos.Add(destino);
        }
        Console.WriteLine("     " + saltos + " saltos y " + etiquetas.Count + " etiquetas");
        Check(saltos > 0, "el .bat salta de un sitio a otro (si no, esta comprobacion no valdria nada)");
        Check(perdidos.Count == 0, "todas las etiquetas a las que salta existen"
              + (perdidos.Count > 0 ? ("   [falta: " + string.Join(", ", perdidos.ToArray()) + "]") : ""));

        // ---------------------------------------------------------------------------------
        Console.WriteLine("\n  4) LA VENTANA NO SE CIERRA SIN QUE SE LEA LO QUE PASO\n");
        // Cada final del archivo tiene que pasar por la pausa. Si no, un error se ensena durante una
        // centesima de segundo y el que lo uso se queda igual que estaba.
        int pausas = 0, salidasSinPausa = 0; bool vistaPausa = false;
        foreach (string l in lineas)
        {
            string t = l.Trim().ToLowerInvariant();
            if (t.StartsWith("rem ") || t.StartsWith("::")) continue;
            if (t == "pause" || t.StartsWith("pause ")) { pausas++; vistaPausa = true; }
            if (t.StartsWith("exit") && !vistaPausa) salidasSinPausa++;
        }
        Console.WriteLine("     " + pausas + " pausas, " + salidasSinPausa + " salidas antes de la primera pausa");
        Check(pausas > 0, "se para al final para que se pueda leer");
        Check(salidasSinPausa == 0, "y no se sale por ningun lado sin pasar antes por la pausa");
        Check(txt.IndexOf("entrenar_dsee.py", StringComparison.OrdinalIgnoreCase) >= 0, "y llama al motor de verdad");

        // ---------------------------------------------------------------------------------
        Console.WriteLine("\n  5) ARRASTRAR LA CARPETA NO SE PONE A LEER LA BIBLIOTECA ENTERA\n");
        // El cartel dice "tambien vale arrastrar la carpeta". Y la carpeta de la rocola tiene DENTRO la
        // musica: bajaba disco por disco abriendo cada cancion para mirarle los primeros bytes. Con una
        // biblioteca de verdad eso es el disco moliendo un buen rato sin ensenar una sola linea, que desde
        // fuera se ve exactamente igual que si se hubiera colgado.
        string raiz = Path.Combine(Path.GetTempPath(), "rocola_entrenador_" + Guid.NewGuid().ToString("N"));
        try { if (Directory.Exists(raiz)) Directory.Delete(raiz, true); } catch { }
        Directory.CreateDirectory(Path.Combine(raiz, Path.Combine("musica", "Un disco")));
        Directory.CreateDirectory(Path.Combine(raiz, Path.Combine("musica", "Otro disco")));
        byte[] datos = new byte[4096];
        datos[0] = (byte)'R'; datos[1] = (byte)'I'; datos[2] = (byte)'A'; datos[3] = (byte)'D';
        File.WriteAllBytes(Path.Combine(raiz, "rocola_ia_datos.bin"), datos);
        File.WriteAllText(Path.Combine(raiz, "rocola_eq.txt"), "1;2;3;4;5;6;7;8;19000|C:\\M\\a.mp3\n");
        for (int i = 0; i < 3; i++)
        {
            File.WriteAllBytes(Path.Combine(raiz, Path.Combine("musica", Path.Combine("Un disco", "c" + i + ".mp3"))), new byte[2048]);
            File.WriteAllBytes(Path.Combine(raiz, Path.Combine("musica", Path.Combine("Otro disco", "c" + i + ".mp3"))), new byte[2048]);
        }

        string guion =
            "import sys; sys.path.insert(0, r'" + Path.GetFullPath(Path.Combine("..", "ia")) + "')\n" +
            "import entrenar_dsee as e\n" +
            "d, q, o, s = e.junta([r'" + raiz + "'])\n" +
            "print('DSEE', len(d)); print('EQ', len(q)); print('OTROS', len(o)); print('SALTADOS', s)\n";
        string tmpPy = Path.Combine(Path.GetTempPath(), "rocola_entrenador_" + Guid.NewGuid().ToString("N") + ".py");
        File.WriteAllText(tmpPy, guion);
        string salida = "";
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON") ?? "python3", "\"" + tmpPy + "\"");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            using (Process pr = Process.Start(psi))
            {
                salida = pr.StandardOutput.ReadToEnd() + pr.StandardError.ReadToEnd();
                pr.WaitForExit();
            }
        }
        catch (Exception ex) { salida = "no se pudo lanzar python3: " + ex.Message; }

        int dsee = Num(salida, "DSEE"), eq = Num(salida, "EQ"), otros = Num(salida, "OTROS");
        Console.WriteLine("     arrastrando la carpeta entera encuentra: " + dsee + " de material del reconstructor, "
                          + eq + " del EQ, y mira " + otros + " archivos mas");
        Check(dsee == 1, "encuentra el material del reconstructor dentro de la carpeta");
        Check(eq == 1, "y el del EQ automatico");
        Check(otros == 0, "y NO se pone a abrir las canciones de la biblioteca una por una");

        try { Directory.Delete(raiz, true); } catch { }
        try { File.Delete(tmpPy); } catch { }

        // ---------------------------------------------------------------------------------
        Console.WriteLine("\n  6) EL ENTRENADOR DEL EQ ENTIENDE EL ARCHIVO QUE LA ROCOLA ESCRIBE HOY\n");
        // La rocola apunta la huella tonal de cada cancion en rocola_eq.txt. Esa ficha fue creciendo con el
        // tiempo -primero ocho numeros, luego el corte de los agudos, luego el recorte y el corte de abajo- y
        // el entrenador del EQ se quedo entendiendo solo la version de hace dos cambios. Con el archivo de HOY
        // decia "no tiene huellas legibles" y no entrenaba nada. Y con uno viejo, que no trae el dato del
        // corte, decia "0 canciones sanas" y se plantaba igual. En los dos casos, meses de pre-analisis a la
        // basura y ningun modelo, sin que el mensaje diera una pista de por donde iban los tiros.
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        int cuantos = 11, dondeCorte = 8; float sanaHz = 19300f;
        try
        {
            Type TF = Assembly.LoadFrom(exe).GetType("Rocola.Form1", true);
            BindingFlags SN = BindingFlags.Static | BindingFlags.NonPublic;
            cuantos = (int)TF.GetField("EqLen", SN).GetRawConstantValue();
            dondeCorte = (int)TF.GetField("EqCut", SN).GetRawConstantValue();
            sanaHz = (float)TF.GetField("EqSanaHz", SN).GetRawConstantValue();
        }
        catch (Exception ex) { Console.WriteLine("     (no se pudo mirar el ejecutable: " + ex.Message + ")"); }
        Console.WriteLine("     la rocola escribe " + cuantos + " numeros por cancion, y el corte va en el "
                          + (dondeCorte + 1) + "o");

        // El archivo de HOY, tal cual lo escribe la rocola, con una de cada tres canciones sana.
        string eqHoy = Path.Combine(Path.GetTempPath(), "rocola_eq_hoy_" + Guid.NewGuid().ToString("N") + ".txt");
        int cuantasCanciones = 1200;
        int sanasPuestas = EscribeHuellas(eqHoy, cuantasCanciones, cuantos, dondeCorte, sanaHz, true);
        string salHoy = CorreEq(eqHoy);
        int total, sanas;
        LeeCuenta(salHoy, out total, out sanas);
        Console.WriteLine("     con el archivo de hoy lee " + total + " canciones de " + cuantasCanciones + " y ve " + sanas + " sanas");
        Check(total == cuantasCanciones, "lee TODAS las canciones del archivo que la rocola escribe hoy");
        Check(sanas == sanasPuestas, "y reconoce cuales conservan sus agudos (" + sanasPuestas + ")");
        Check(salHoy.IndexOf("ilegibles", StringComparison.Ordinal) < 0, "y no da por ilegible ni una sola linea");

        // Y el de una rocola VIEJA, que no trae el dato del corte: no puede saber cuales estan sanas, pero
        // eso no es motivo para tirar meses de trabajo. Se entrena con todas, que es exactamente lo que la
        // rocola hace con su promedio cuando no tiene bastantes sanas medidas.
        string eqViejo = Path.Combine(Path.GetTempPath(), "rocola_eq_viejo_" + Guid.NewGuid().ToString("N") + ".txt");
        EscribeHuellas(eqViejo, cuantasCanciones, 8, dondeCorte, sanaHz, false);
        string salViejo = CorreEq(eqViejo);
        LeeCuenta(salViejo, out total, out sanas);
        Console.WriteLine("     con uno de una rocola vieja lee " + total + " canciones y ve " + sanas + " sanas");
        Check(total == cuantasCanciones, "lee tambien los archivos de las versiones de antes");
        Check(salViejo.IndexOf("Hacen falta al menos", StringComparison.Ordinal) < 0,
              "y no se planta por no poder saber cuales estan sanas");
        Check(salViejo.IndexOf("MEJORA GLOBAL", StringComparison.Ordinal) >= 0,
              "entrena con todas y dice si le gana al promedio (que es lo que decide si guarda o no)");
        Check(salViejo.IndexOf("copia otra vez", StringComparison.OrdinalIgnoreCase) >= 0
              || salViejo.IndexOf("version vieja", StringComparison.OrdinalIgnoreCase) >= 0,
              "y avisa de que ese archivo es de una version vieja, para que traiga uno nuevo");

        try { File.Delete(eqHoy); } catch { }
        try { File.Delete(eqViejo); } catch { }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }

    // Escribe un rocola_eq.txt con 'cuantos' numeros por linea, como lo escribiria la rocola.
    // Devuelve cuantas canciones quedaron SANAS (con sus agudos completos).
    static int EscribeHuellas(string ruta, int lineas, int cuantos, int dondeCorte, float sanaHz, bool conCorte)
    {
        Random r = new Random(11);
        int sanas = 0;
        StringBuilder sb = new StringBuilder(lineas * 90);
        for (int i = 0; i < lineas; i++)
        {
            bool sana = conCorte && (i % 3 == 0);
            if (sana) sanas++;
            for (int b = 0; b < cuantos; b++)
            {
                if (b > 0) sb.Append(';');
                double v;
                if (b < 8) v = Math.Round(-12.0 + r.NextDouble() * 18.0, 2);
                else if (b == dondeCorte) v = sana ? (sanaHz + 700.0) : (14000.0 + r.NextDouble() * 4000.0);
                else v = Math.Round(r.NextDouble() * 60.0, 2);
                sb.Append(v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.Append("|C:\\Musica\\Disco ").Append(i % 40).Append("\\cancion").Append(i).Append(".mp3\r\n");
        }
        File.WriteAllText(ruta, sb.ToString());
        return sanas;
    }

    static string CorreEq(string datos)
    {
        string py = Path.GetFullPath(Path.Combine("..", Path.Combine("ia", "entrenar_eq.py")));
        string fuera = Path.Combine(Path.GetTempPath(), "rocola_eq_ia_prueba_" + Guid.NewGuid().ToString("N") + ".bin");
        try { File.Delete(fuera); } catch { }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON") ?? "python3",
                "\"" + py + "\" --datos \"" + datos + "\" --salida \"" + fuera + "\" --epocas 6");
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            using (Process pr = Process.Start(psi))
            {
                string s = pr.StandardOutput.ReadToEnd() + pr.StandardError.ReadToEnd();
                pr.WaitForExit();
                return s;
            }
        }
        catch (Exception ex) { return "no se pudo lanzar python3: " + ex.Message; }
        finally { try { File.Delete(fuera); } catch { } }
    }

    // De la linea "  N canciones en total, M sanas (...)" saca los dos numeros.
    static void LeeCuenta(string salida, out int total, out int sanas)
    {
        total = -1; sanas = -1;
        foreach (string l in salida.Replace("\r", "").Split('\n'))
        {
            int i = l.IndexOf(" canciones en total,", StringComparison.Ordinal);
            if (i < 0) continue;
            int.TryParse(l.Substring(0, i).Trim(), out total);
            string resto = l.Substring(i + " canciones en total,".Length).Trim();
            int j = resto.IndexOf(' ');
            if (j > 0) int.TryParse(resto.Substring(0, j), out sanas);
            return;
        }
    }

    static int Num(string salida, string clave)
    {
        foreach (string l in salida.Replace("\r", "").Split('\n'))
        {
            string t = l.Trim();
            if (!t.StartsWith(clave + " ")) continue;
            int v; if (int.TryParse(t.Substring(clave.Length + 1).Trim(), out v)) return v;
        }
        return -1;
    }
}
