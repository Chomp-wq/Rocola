using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

// LA ACTUALIZACION SOBRE UNA ROCOLA VIEJA (13-10-2026).
//
// Peticion del usuario: «Voy a meter esta actualizacion a la rocola, la rocola tiene una version vieja: revisa que esta
// version sea compatible con versiones viejas y que de ser necesario convierta las cosas viejas a las nuevas».
//
// Se escriben en una carpeta los archivos TAL COMO LOS DEJABAN las versiones anteriores (copiados de lo que escribia su
// codigo en el historial: la primera importada del 24-06-2026, la de julio, la de agosto...) y la version de hoy los lee:
//   - rocola_config.txt con claves que cambiaron de forma (nieblas/fugaces/patron/animacion de 0/1 a bajo/medio/alto;
//     la aurora que un dia se llamo "fondo=") y con claves de cosas que ya no existen (se ignoran sin romper nada);
//   - rocola_stats.txt (lo mas sonado, monedas y creditos), igual desde el primer dia;
//   - rocola_teclas.txt (los botones del mueble);
//   - rocola_volumen.txt viejo ("rms|ruta", sin duracion) y rocola_eq.txt de julio (8 valores);
//   - rocola_biblioteca.txt de la primera version (ROCOLA-LIB v1): no se usa y se vuelve a escanear;
//   - rocola_ia_datos.bin de agosto sin su indice: se cuenta y se le sigue anadiendo sin tocar lo de antes.
// Y al guardar, la configuracion sale en el formato nuevo con los valores de antes.
class CompatVieja
{
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos, oks;
    static void Exige(bool c, string q) { if (c) { oks++; Console.WriteLine("  OK    " + q); } else { fallos++; Console.WriteLine("  FALLO " + q); } }
    static Type TF;
    static object Toma(object o, string n) { FieldInfo f = TF.GetField(n, T); if (f == null) throw new Exception("falta " + n); return f.GetValue(o); }
    static void Pon(object o, string n, object v) { FieldInfo f = TF.GetField(n, T); if (f == null) throw new Exception("falta " + n); f.SetValue(o, v); }
    static object Llama(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + n);
    }
    static string raiz;
    static string Ruta(string n) { return Path.Combine(raiz, n); }
    static object Rocola()
    {
        object f = Codigo.FormSinEncender(TF);
        Pon(f, "carpetaDatos", raiz);
        return f;
    }

    static void Configuracion()
    {
        Console.WriteLine("\n  1) LA CONFIGURACION DE ANTES\n");
        string musicaExtra = Path.Combine(raiz, "Discos D");
        // Lo que escribia la version de agosto (claves con 0/1 y la aurora como "fondo="), mas cosas que ya no existen.
        File.WriteAllText(Ruta("rocola_config.txt"),
            "gpu=1\r\nnieblas=1\r\nfugaces=0\r\npatron=1\r\nanimacion=0\r\nfondo=40\r\nestrellas=60\r\n"
          + "efectos=1\r\nefectos_tic=1\r\nefectos_vol=70\r\nrecorte_silencio=1\r\naurora_real=1\r\nestilo_fondo=natural\r\n"
          + "luna_fase=3\r\ncielo_hora=1\r\nvinilo_disco=1\r\nbuscador_cache=1\r\ncache_portadas_gpu=1\r\n"
          + "nivel_objetivo=0.3\r\nnivel_fuerza=0.8\r\nrealce=0.5\r\nsubir_db=3\r\neq_auto=40\r\ndsee=1\r\nvideo_vol=70\r\n"
          + "noche=0\r\ndir=" + musicaExtra + "\r\n");
        object f = Rocola();
        Llama(f, "LoadConfigPrefs");
        Exige((int)Toma(f, "nieblasNiv") == 2 && (int)Toma(f, "fugacesNiv") == 0 && (int)Toma(f, "relevoNiv") == 2 && (int)Toma(f, "animNiv") == 0,
              "nieblas/fugaces/patron/animacion de 0/1 pasan a niveles: 1 = alto, 0 = bajo");
        Exige(Math.Abs((float)Toma(f, "auroraAmount") - 0.40f) < 1e-4, "la aurora guardada como \"fondo=40\" (version del 10-08) se lee: 40 %");
        Exige(Math.Abs((float)Toma(f, "estrellasDens") - 0.60f) < 1e-4 && (int)Toma(f, "videoVol") == 70 && TF.GetField("nocheOn", T) == null,
              "estrellas y volumen de videos, como estaban; el noche=0 de antes se ignora (el modo noche se retiro el 24-10-2026)");
        Exige(Math.Abs((float)Toma(f, "normTarget") - 0.3f) < 1e-4 && Math.Abs((float)Toma(f, "normStrength") - 0.8f) < 1e-4
              && (float)Toma(f, "masterDb") == 0f && Math.Abs((float)Toma(f, "eqAmount") - 0.4f) < 1e-4,
              "el sonido de antes: volumen objetivo, fuerza y EQ al 40 % (subir_db=3 se ignora desde el 25-10-2026)");
        IList dirs = (IList)Toma(f, "extraMusicDirs");
        Exige(dirs.Count == 1 && (string)dirs[0] == musicaExtra, "la carpeta de musica extra (dir=) se conserva");
        // Al guardar: formato nuevo, mismos valores; lo que ya no existe no se vuelve a escribir.
        Llama(f, "SaveConfigPrefs"); Codigo.EsperaGuardado(TF, f);
        string nuevo = File.ReadAllText(Ruta("rocola_config.txt"));
        Exige(nuevo.Contains("nieblas=alto") && nuevo.Contains("fugaces=bajo") && nuevo.Contains("espectro=40") && !nuevo.Contains("\nfondo=")
              && !nuevo.Contains("efectos=") && !nuevo.Contains("estilo_fondo=") && !nuevo.Contains("\nnoche=") && !nuevo.Contains("noche_db=") && nuevo.Contains("dir=" + musicaExtra),
              "al guardar sale en el formato nuevo (nieblas=alto, espectro=40), sin las claves retiradas y con su carpeta extra");
        object g = Rocola(); Llama(g, "LoadConfigPrefs");
        Exige((int)Toma(g, "nieblasNiv") == 2 && Math.Abs((float)Toma(g, "auroraAmount") - 0.40f) < 1e-4 && (int)Toma(g, "videoVol") == 70,
              "y se relee igual");
        // Si estan las dos ("fondo=" viejo y "espectro=" de ahora), manda "espectro=".
        File.WriteAllText(Ruta("rocola_config.txt"), "espectro=55\r\nfondo=10\r\n");
        object h = Rocola(); Llama(h, "LoadConfigPrefs");
        Exige(Math.Abs((float)Toma(h, "auroraAmount") - 0.55f) < 1e-4, "si estan \"espectro=\" y el viejo \"fondo=\", manda espectro");
    }

    static void Estadisticas()
    {
        Console.WriteLine("\n  2) LO MAS SONADO, LAS MONEDAS Y LOS BOTONES\n");
        // Exactamente lo que escribia la primera version importada (BuildStatsText, 24-06-2026).
        File.WriteAllText(Ruta("rocola_stats.txt"),
            "ROCOLA-STATS v1\r\ntotal-reproducciones=1234\r\ntotal-canciones=1200\r\ntotal-videos=34\r\nmonedas=617\r\ncreditos-cargados=2468\r\n"
          + "[CANCIONES] count|ruta\r\n12|C:\\musica\\Los Bukis\\01 Tu carcel.mp3\r\n40|C:\\musica\\Selena\\03 Como la flor.mp3\r\n"
          + "[ARTISTAS] count|artista\r\n12|Los Bukis\r\n40|Selena\r\n"
          + "[VIDEOS] count|ruta\r\n5|C:\\musica\\videos\\Mana - Rayando el sol.mp4\r\n"
          + "[GENEROS] genero|ruta\r\nGrupera|C:\\musica\\Los Bukis\\01 Tu carcel.mp3\r\n");
        object f = Rocola();
        Llama(f, "LoadStats");
        IDictionary songs = (IDictionary)Toma(f, "songCounts"), arts = (IDictionary)Toma(f, "artistCounts"), vids = (IDictionary)Toma(f, "videoCounts");
        Exige((long)Toma(f, "statsTotal") == 1234 && (long)Toma(f, "statsCoins") == 617 && (long)Toma(f, "statsCreditsAdded") == 2468,
              "totales, monedas (617) y creditos cargados (2468) de la primera version");
        Exige(songs.Count == 2 && (int)songs["C:\\musica\\Selena\\03 Como la flor.mp3"] == 40 && (int)arts["Selena"] == 40
              && (int)vids["C:\\musica\\videos\\Mana - Rayando el sol.mp4"] == 5, "las canciones, los artistas y los videos mas pedidos");
        Llama(f, "SaveStats", false);
        System.Threading.Thread.Sleep(300);
        object g = Rocola(); Llama(g, "LoadStats");
        Exige((long)Toma(g, "statsCoins") == 617 && ((IDictionary)Toma(g, "songCounts")).Count == 2, "guardadas otra vez y releidas: lo mismo");
        // Los botones del mueble (rocola_teclas.txt, igual desde el primer dia).
        File.WriteAllText(Ruta("rocola_teclas.txt"), "arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=8\r\nmoneda=67\r\nsaltar=83\r\n");
        object k = Rocola();
        Exige((bool)Llama(k, "LoadKeys") && (Keys)Toma(k, "keyCoin") == Keys.C && (Keys)Toma(k, "keySkip") == Keys.S && (Keys)Toma(k, "keyOK") == Keys.Enter,
              "los botones del mueble se leen igual: no hay que volver a configurarlos");
    }

    static void Medidas()
    {
        Console.WriteLine("\n  3) LAS MEDIDAS Y LAS CACHES\n");
        // rocola_volumen.txt de julio: "rms|ruta", sin duracion.
        File.WriteAllText(Ruta("rocola_volumen.txt"), "0.123456|C:\\musica\\a.mp3\r\n0.05|C:\\musica\\b.mp3\r\n");
        Dictionary<string, float> loud = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase), dur = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Llama(null, "ReadLoudCache", Ruta("rocola_volumen.txt"), loud, dur);
        Exige(loud.Count == 2 && Math.Abs(loud["C:\\musica\\a.mp3"] - 0.123456f) < 1e-6, "el volumen medido con el formato de julio (sin duracion) se lee");
        // rocola_eq.txt de julio: 8 valores separados por ';'.
        File.WriteAllText(Ruta("rocola_eq.txt"), "1.5;0.5;0;0;0;-0.5;-2;-6|C:\\musica\\a.mp3\r\n");
        Dictionary<string, float[]> eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        object[] args = { Ruta("rocola_eq.txt"), eq, 0 };
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "ReadEqCache") m.Invoke(null, args);
        Exige(eq.Count == 1 && Math.Abs(eq["C:\\musica\\a.mp3"][7] + 6f) < 1e-4, "la huella de EQ de julio (8 bandas) se lee");
        // La cache de la biblioteca de la primera version: no sirve, se reescanea (sin romper nada).
        File.WriteAllText(Ruta("rocola_biblioteca.txt"), "ROCOLA-LIB v1\r\nroot=C:\\musica\r\nmtime=1\r\ndirs=0\r\nA0\r\nV0\r\nC0\r\n");
        object f = Rocola();
        object[] a = { new List<string> { "C:\\musica" }, null, null, null, null };
        bool usada = true;
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "TryLoadLibraryCache") usada = (bool)m.Invoke(f, a);
        Exige(!usada, "la cache de biblioteca de la primera version no se usa: la musica se vuelve a escanear sola (solo la primera vez)");
        // La de julio (ROCOLA-LIB v2, la misma forma que hoy): con la firma de las carpetas al dia se aprovecha.
        string musica = Ruta("musica"); Directory.CreateDirectory(Path.Combine(musica, "Selena"));
        string c1 = Path.Combine(musica, "Selena\\01 Como la flor.mp3".Replace("\\", Path.DirectorySeparatorChar.ToString()));
        string c2 = Path.Combine(musica, "Selena" + Path.DirectorySeparatorChar + "02 Amor prohibido.mp3");
        File.WriteAllBytes(c1, new byte[16]); File.WriteAllBytes(c2, new byte[16]);
        List<string> raices = new List<string> { musica };
        string firma = null;
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "RootsSigForScan") firma = (string)m.Invoke(f, new object[] { raices });
        File.WriteAllText(Ruta("rocola_biblioteca.txt"), "ROCOLA-LIB v2\r\nroots=" + musica + "\r\nsig=" + firma + "\r\ndirs=0\r\nA2\r\n" + c1 + "\r\n" + c2
            + "\r\nV0\r\nC1\r\n" + Path.Combine(musica, "Selena") + "|" + Path.Combine(musica, "Selena" + Path.DirectorySeparatorChar + "folder.jpg") + "\r\n");
        object[] b = { raices, null, null, null, null };
        bool julio = false;
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == "TryLoadLibraryCache") julio = (bool)m.Invoke(f, b);
        Exige(firma != null && julio && ((IList)b[1]).Count == 2, "la cache de julio (v2) se aprovecha si las carpetas no cambiaron: 2 canciones sin reescanear");
    }

    static void MaterialIa()
    {
        Console.WriteLine("\n  4) EL MATERIAL PARA LA IA\n");
        // rocola_ia_datos.bin de agosto (cabecera RIAD 1/28/12, pares de 160 bytes) y SIN rocola_ia_datos.idx.
        byte[] cab = new byte[16]; cab[0] = (byte)'R'; cab[1] = (byte)'I'; cab[2] = (byte)'A'; cab[3] = (byte)'D';
        Buffer.BlockCopy(BitConverter.GetBytes(1), 0, cab, 4, 4); Buffer.BlockCopy(BitConverter.GetBytes(28), 0, cab, 8, 4); Buffer.BlockCopy(BitConverter.GetBytes(12), 0, cab, 12, 4);
        byte[] viejo = new byte[16 + 3 * 160]; Buffer.BlockCopy(cab, 0, viejo, 0, 16);
        for (int i = 16; i < viejo.Length; i += 4) Buffer.BlockCopy(BitConverter.GetBytes((float)(i % 97) / 97f), 0, viejo, i, 4);
        File.WriteAllBytes(Ruta("rocola_ia_datos.bin"), viejo);
        if (File.Exists(Ruta("rocola_ia_datos.idx"))) File.Delete(Ruta("rocola_ia_datos.idx"));
        object f = Rocola();
        Llama(f, "IaCountPairs");
        Exige((int)Toma(f, "iaPairsTotal") == 3, "los 3 ejemplos juntados con la version de agosto se cuentan");
        List<float> pares = new List<float>(); for (int i = 0; i < 40; i++) pares.Add(i / 40f);
        bool ok = (bool)Llama(f, "IaAppendPairs", pares);
        byte[] ahora = File.ReadAllBytes(Ruta("rocola_ia_datos.bin"));
        bool intacto = ahora.Length == viejo.Length + 160;
        for (int i = 0; intacto && i < viejo.Length; i++) if (ahora[i] != viejo[i]) intacto = false;
        Exige(ok && intacto, "se le sigue anadiendo al mismo archivo y lo de antes queda intacto, byte a byte");
    }

    static int Main(string[] argv)
    {
        TF = System.Reflection.Assembly.LoadFrom(argv.Length > 0 ? argv[0] : "rocola.exe").GetType("Rocola.Form1", true);
        raiz = Path.Combine(Path.GetTempPath(), "compatvieja_" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(raiz);
        try
        {
            try { Configuracion(); } catch (Exception e) { Exige(false, "configuracion: " + e); }
            try { Estadisticas(); } catch (Exception e) { Exige(false, "estadisticas: " + e); }
            try { Medidas(); } catch (Exception e) { Exige(false, "medidas: " + e); }
            try { MaterialIa(); } catch (Exception e) { Exige(false, "material de la IA: " + e); }
        }
        finally { try { Directory.Delete(raiz, true); } catch { } }
        Console.WriteLine(fallos == 0 ? "\n  PASS compatvieja (" + oks + " comprobaciones; archivos con el formato de las versiones anteriores)" : "\n  " + fallos + " fallos");
        return fallos;
    }
}
