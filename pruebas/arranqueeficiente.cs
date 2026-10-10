using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Sin abrir la Rocola, sin musica real y con todos los archivos dentro de un directorio temporal propio.
// --medir informa tiempos/heap de escenarios sinteticos; no es FPS, HDD real ni compatibilidad real con XP.
class ArranqueEficiente
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type F; static int checks;
    static object Get(object o, string n) { return (o == null ? F : o.GetType()).GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { (o == null ? F : o.GetType()).GetField(n, B).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    { try { return (o == null ? F : o.GetType()).GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static void Check(bool ok, string s) { if (!ok) throw new Exception(s); checks++; Console.WriteLine("OK " + s); }
    static object Form()
    {
        object f = Codigo.FormSinEncender(F);
        typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(f, new object[0]);
        Set(f, "lastInputTick", unchecked(Environment.TickCount - 10000)); Set(f, "attractOn", true);
        return f;
    }
    static object Track(string path)
    { object t = Activator.CreateInstance(F.GetNestedType("Track", B), true); Set(t, "Path", path); Set(t, "Title", path); return t; }
    static object Album(string path)
    { object a = Activator.CreateInstance(F.GetNestedType("Album", B), true); Set(a, "CoverPath", path); return a; }
    static IList AlbumList()
    { return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(F.GetNestedType("Album", B))); }
    static object[] Pair(object f, bool compartir)
    {
        object full, light;
        if (compartir)
        {
            full = Call(f, "CapturaLecturaBiblioteca"); Set(f, "planArranqueComun", full);
            try { light = Call(f, "CapturaParaBiblioteca", false); CheckSame(full, Call(f, "CapturaLecturaBiblioteca")); }
            finally { Set(f, "planArranqueComun", null); }
        }
        else { light = Call(f, "CapturaParaBiblioteca", false); full = Call(f, "CapturaLecturaBiblioteca"); }
        return new object[] { full, light };
    }
    static void CheckSame(object a, object b) { if (!ReferenceEquals(a, b)) throw new Exception("plan completo inicial duplicado"); }
    static void Libera(object[] p) { foreach (object x in p) ((IDisposable)x).Dispose(); }
    static void PruebaPlanes()
    {
        object f = Form(); ((IList)Get(f, "tracks")).Add(Track("uno.mp3"));
        ((IDictionary)Get(f, "durCache"))["uno.mp3"] = 11f; ((IDictionary)Get(f, "durFast"))["dos.mp3"] = 22f;
        ((IDictionary)Get(f, "loudCache"))["uno.mp3"] = 0.5f;
        object[] p = Pair(f, true);
        Check(ReferenceEquals(Get(p[0], "Tracks"), Get(p[1], "Tracks"))
            && ReferenceEquals(Get(p[0], "Videos"), Get(p[1], "Videos")) && ReferenceEquals(Get(p[0], "Letras"), Get(p[1], "Letras")),
            "planes iniciales reutilizan listas privadas sin duplicar snapshots");
        ((IList)Get(f, "tracks")).Clear(); ((IDictionary)Get(f, "durCache"))["uno.mp3"] = 99f;
        Exception fallo = null;
        Thread a = new Thread(delegate () { try { Call(p[0], "Prepara"); } catch (Exception e) { fallo = e; } });
        Thread b = new Thread(delegate () { try { Call(p[1], "Prepara"); } catch (Exception e) { fallo = e; } });
        a.Start(); b.Start(); Check(a.Join(3000) && b.Join(3000) && fallo == null, "indices compartidos se preparan concurrentemente sin carrera");
        Check(ReferenceEquals(Get(p[0], "Dur"), Get(p[1], "Dur")) && ReferenceEquals(Get(p[0], "Rapidas"), Get(p[1], "Rapidas")),
            "duraciones y cabeceras se indexan una vez");
        Check((float)((IDictionary)Get(p[1], "Dur"))["uno.mp3"] == 11f && ((IList)Get(p[1], "Tracks")).Count == 1,
            "la captura privada no cambia cuando UI modifica biblioteca/tablas");
        Check(((IDictionary)Get(p[0], "Loud")).Count == 1 && ((IDictionary)Get(p[1], "Loud")).Count == 0,
            "analisis completo y lector ligero conservan sus tablas adicionales independientes");
        Call(p[1], "Cancela"); Check((bool)Get(p[1], "Cancelada") && !(bool)Get(p[0], "Cancelada"), "cancelar duraciones no cancela el repaso");
        Set(f, "planRepaso", p[0]); Set(f, "planDuracion", p[1]); Set(f, "preScanGen", 4);
        Call(f, "SueltaPlanTerminado", p[0], true, 4);
        Check(Get(f, "planRepaso") == null && ReferenceEquals(Get(f, "planDuracion"), p[1])
            && ReferenceEquals(Get(p[0], "Dur"), Get(p[1], "Dur")) && ((IList)Get(p[1], "Tracks")).Count == 1,
            "retirar repaso UI no borra la base compartida del lector aun activo");
        Call(f, "SueltaPlanTerminado", p[1], false, 4);
        Check(Get(f, "planDuracion") == null && ((IDictionary)Get(p[0], "Loud")).Count == 1,
            "retirar el segundo plan deja intacta su captura y tablas privadas");
        ((IDisposable)p[0]).Dispose(); Check(!(bool)Call(p[1], "Espera", 1), "liberar un plan no comparte ni invalida el cancelador del otro");
        ((IDisposable)p[1]).Dispose();
        object siguiente = Call(f, "CapturaLecturaBiblioteca"); Call(siguiente, "Prepara");
        Check(((IList)Get(siguiente, "Tracks")).Count == 0 && (float)((IDictionary)Get(siguiente, "Dur"))["uno.mp3"] == 99f,
            "una nueva publicacion no reutiliza el snapshot anterior");
        ((IDisposable)siguiente).Dispose();
        Check(Codigo.Llama(Codigo.ConAyudantes(F, "StartDurScan"), "PideTurnoAnalisis")
            && Codigo.Llama(Codigo.ConAyudantes(F, "StartDurScan"), "EsperaPortadas"), "cabeceras/generos comparten admision HDD y ceden a caratulas visibles");
    }
    static void PruebaTintes()
    {
        Type tipo = F.GetNestedType("VotosTinte`1", B).MakeGenericType(typeof(string));
        object votos = Activator.CreateInstance(tipo, B, null, new object[] { StringComparer.OrdinalIgnoreCase }, null);
        Dictionary<string, Dictionary<string, int>> referencia = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> antes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] generos = { "Otros", "Rock", "Pop", "Metal", "Country" }; Random azar = new Random(71);
        int emitidas = 0, todas = 0;
        for (int tanda = 0; tanda < 120; tanda++)
        {
            for (int j = 0; j < 256; j++)
            {
                string clave = "Disco-" + azar.Next(70), et = generos[azar.Next(generos.Length)];
                Call(votos, "Vota", clave, et); Dictionary<string, int> cuenta;
                if (!referencia.TryGetValue(clave, out cuenta)) { cuenta = new Dictionary<string, int>(); referencia[clave] = cuenta; }
                int n; cuenta.TryGetValue(et, out n); cuenta[et] = n + 1;
            }
            Dictionary<string, string> esperado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Dictionary<string, int>> par in referencia)
            {
                string mejor = null; int max = 0;
                foreach (KeyValuePair<string, int> voto in par.Value) if (voto.Key != "Otros" && voto.Value > max) { mejor = voto.Key; max = voto.Value; }
                if (mejor == null) continue; todas++;
                string previo; if (!antes.TryGetValue(par.Key, out previo) || previo != mejor) esperado[par.Key] = mejor;
                antes[par.Key] = mejor;
            }
            object[] salida = { null, null }; Call(votos, "Delta", salida);
            string[] keys = (string[])salida[0], labels = (string[])salida[1]; emitidas += keys.Length;
            if (keys.Length != esperado.Count || keys.Length != labels.Length) throw new Exception("delta distinto del censo original");
            for (int j = 0; j < keys.Length; j++) if (!esperado.ContainsKey(keys[j]) || esperado[keys[j]] != labels[j] || !esperado.Remove(keys[j]))
                throw new Exception("ganador, desempate o delta duplicado");
        }
        Check(emitidas < todas, "dominantes/dempates equivalen a 30.720 votos originales, enviando solo cambios: " + emitidas + "/" + todas + " filas");
        object[] vacio = { null, null }; Call(votos, "Delta", vacio);
        Check(((string[])vacio[0]).Length == 0 && ((string[])vacio[1]).Length == 0, "sin votos nuevos no hay publicaciones repetidas");
        Call(votos, "Vota", "Solo Otros", "Otros"); Call(votos, "Delta", vacio);
        Check(((string[])vacio[0]).Length == 0, "Otros no fuerza un color ni borra un genero");
        object f = Form(); Set(f, "discTag", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        char[] letras = { 'A' }; string[] etiquetas = { "Rock" }, discos = { "Album" };
        Check((bool)Call(f, "AplicaTintesNuevos", letras, etiquetas, discos, etiquetas), "primer genero se publica");
        Check(!(bool)Call(f, "AplicaTintesNuevos", letras, etiquetas, new string[] { "album" }, etiquetas),
            "generos identicos/carpeta con distinta capitalizacion no recomputan UI");
        Check((bool)Call(f, "AplicaTintesNuevos", new char[0], new string[0], discos, new string[] { "Pop" }),
            "un cambio real de disco sigue publicandose");
    }
    static void Imagen(string ruta, Color color)
    { using (Bitmap b = new Bitmap(64, 64)) { using (Graphics g = Graphics.FromImage(b)) g.Clear(color); b.Save(ruta, ImageFormat.Png); } }
    static void PruebaMiniaturas(string dir)
    {
        object f = Form(); Set(f, "warmGen", 7);
        string src = Path.Combine(dir, "portada.png"); Imagen(src, Color.Blue);
        object album = Album(src); IList snap = AlbumList(); snap.Add(album); snap.Add(Album(src)); snap.Add(null); snap.Add(Album(null));
        IList pendientes = (IList)Call(f, "CensaMiniaturas", 7, snap);
        Check(pendientes.Count == 1, "censo deduplica fuente repetida y omite discos sin portada");
        object p = pendientes[0]; string identidad = (string)Get(p, "Identidad");
        Check(identidad == (string)Call(null, "ThumbCachePath", src), "censo guarda la misma identidad de miniatura y LOD");
        Bitmap jpg = new Bitmap(64, 64);
        using (Graphics g = Graphics.FromImage(jpg)) g.Clear(Color.Blue);
        try
        {
            Check((bool)Call(f, "PublicaMiniaturaCalentada", 7, p, jpg, jpg) && File.Exists(identidad),
                "miniatura vigente se publica por el guardado atomico existente");
            Check(((IList)Call(f, "CensaMiniaturas", 7, snap)).Count == 0, "censo caliente no propone reconstruir caratulas ya terminadas");
            File.Delete(identidad); Imagen(src, Color.Red); File.SetLastWriteTimeUtc(src, DateTime.UtcNow.AddSeconds(4));
            Check(!(bool)Call(f, "PublicaMiniaturaCalentada", 7, p, jpg, jpg) && !File.Exists(identidad),
                "fuente reemplazada no publica un JPG antiguo con una identidad obsoleta");
            pendientes = (IList)Call(f, "CensaMiniaturas", 7, snap); p = pendientes[0];
            Set(f, "warmGen", 8);
            Check(!(bool)Call(f, "PublicaMiniaturaCalentada", 7, p, jpg, jpg), "F5 invalida miniaturas antes de guardar");
            Set(f, "warmGen", 7); Set(f, "startupClosing", true);
            Check(!(bool)Call(f, "PublicaMiniaturaCalentada", 7, p, jpg, jpg) && Call(f, "CensaMiniaturas", 7, snap) == null,
                "cerrar cancela censo/publicacion sin abrir la aplicacion");
        }
        finally { jpg.Dispose(); }
        Set(f, "startupClosing", false);
        Semaphore sem = (Semaphore)Get(null, "coverSem"); int tomados = 0;
        while (sem.WaitOne(0, false)) tomados++;
        bool retorno = true; Exception fallo = null;
        Thread espera = new Thread(delegate () { try { retorno = (bool)Call(f, "TomaTurnoMiniatura", 7); } catch (Exception e) { fallo = e; } });
        try
        {
            espera.Start(); Thread.Sleep(40); Set(f, "warmGen", 8);
            Check(espera.Join(600) && !retorno && fallo == null, "espera de turno de miniaturas cancelable cuando todos los carriles estan ocupados");
        }
        finally { if (tomados > 0) sem.Release(tomados); }
        Check(Codigo.Llama(Codigo.ConAyudantes(F, "StartThumbWarm"), "CensaMiniaturas")
            && !Codigo.Llama(Codigo.ConAyudantes(F, "StartThumbWarm"), "NeedsGpuPreview"),
            "calentador recorre fuentes una vez y no repite la identidad para LOD");
        Check(Codigo.Llama(Codigo.ConAyudantes(F, "BarreMiniaturasConPausas"), "Sleep"),
            "mantenimiento de miniaturas cede entre tandas de metadatos/borrados");
    }
    static void PruebaPrecalentado()
    {
        MethodInfo audio = F.GetMethod("PrecalientaAudioDespuesDelCuadro", B);
        if (audio == null) { Console.WriteLine("GPU no compilada: no se altera precalentador CPU"); return; }
        object f = Form(); Call(f, "PrecalientaAudioDespuesDelCuadro");
        Check(!(bool)Get(f, "precalAudioLanzado"), "DSP no se calienta antes del primer cuadro GPU");
        List<string> llamadas = Codigo.ConAyudantes(F, "PrecalientaAudioDespuesDelCuadro");
        Check(Codigo.Llama(llamadas, "PrecalientaTablasAudio") && Codigo.Llama(llamadas, "YieldCoverWork") == false,
            "calentador DSP usa las piezas de memoria del audio, no una precarga de canciones");
        Check(Codigo.Llama(llamadas, "get_CuadrosMedidos") && Codigo.Llama(llamadas, "get_MsTrabajo"),
            "admite precalentamiento despues del cuadro y con margen medido");
        Check(Codigo.Llama(Codigo.ConAyudantes(F, "PrecalientaUnPoco"), "TryEnter"),
            "UI no espera el turno del trabajador DSP");
    }
    static double Mediana(double[] a) { Array.Sort(a); return a[a.Length / 2]; }
    static void MidePlanes()
    {
        object f = Form(); IList tracks = (IList)Get(f, "tracks");
        IDictionary dur = (IDictionary)Get(f, "durCache"), fast = (IDictionary)Get(f, "durFast"), loud = (IDictionary)Get(f, "loudCache");
        for (int i = 0; i < 100000; i++) { string k = "sintetica-" + i; tracks.Add(Track(k)); dur[k] = i + 1f; fast[k] = i + 1f; loud[k] = 1f; }
        for (int modo = 0; modo < 2; modo++)
        {
            double[] captura = new double[5], prepara = new double[5], heap = new double[5];
            for (int r = 0; r < captura.Length; r++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long inicial = GC.GetTotalMemory(false);
                Stopwatch sw = Stopwatch.StartNew(); object[] p = Pair(f, modo == 1); captura[r] = sw.Elapsed.TotalMilliseconds;
                heap[r] = (GC.GetTotalMemory(false) - inicial) / 1048576.0;
                sw.Reset(); sw.Start(); Call(p[0], "Prepara"); Call(p[1], "Prepara"); prepara[r] = sw.Elapsed.TotalMilliseconds; Libera(p);
            }
            Console.WriteLine("MEDICION 100.000 pistas / " + (modo == 0 ? "snapshots separados" : "snapshot compartido")
                + ": captura mediana=" + Mediana(captura).ToString("F2") + " ms; preparar=" + Mediana(prepara).ToString("F2")
                + " ms; crecimiento heap capturas=" + Mediana(heap).ToString("F2") + " MiB (no bytes asignados totales ni FPS)");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola-arranque-eficiente-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string datosAntes = Environment.GetEnvironmentVariable("ROCOLA_DATOS"); Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            bool medir = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--medir") medir = true;
                if (args[i] == "--dos-hilos")
                {
                    Process proceso = Process.GetCurrentProcess(); long disponible = proceso.ProcessorAffinity.ToInt64(), dos = 0;
                    for (int bit = 0, encontrados = 0; bit < IntPtr.Size * 8 && encontrados < 2; bit++)
                        if ((disponible & (1L << bit)) != 0) { dos |= 1L << bit; encontrados++; }
                    proceso.ProcessorAffinity = new IntPtr(dos);
                    Console.WriteLine("Afinidad sintetica de dos hilos logicos; no es un procesador fisico 2C/2T");
                }
            }
            F = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
            PruebaPlanes(); PruebaTintes(); PruebaMiniaturas(dir); PruebaPrecalentado();
            if (medir) MidePlanes();
            Console.WriteLine("PASS " + checks + " comprobaciones; sin musica/datos de usuario ni FPS reales"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", datosAntes);
            // Unicamente el directorio que esta prueba acaba de crear, sin rutas de usuario.
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
