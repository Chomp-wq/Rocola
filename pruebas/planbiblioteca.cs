using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

class PlanesBiblioteca
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type F; static int checks;
    static object Get(object o, string n) { return (o == null ? F : o.GetType()).GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { (o == null ? F : o.GetType()).GetField(n, B).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    { try { return (o == null ? F : o.GetType()).GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static void Check(bool ok, string s) { if (!ok) throw new Exception(s); checks++; Console.WriteLine("OK " + s); }
    static object Track(string path)
    { object t = Activator.CreateInstance(F.GetNestedType("Track", B), true); Set(t, "Path", path); Set(t, "Title", path); Set(t, "Artist", "Artista"); return t; }
    static object Album(string name, params object[] tracks)
    {
        object a = Activator.CreateInstance(F.GetNestedType("Album", B), true);
        Set(a, "Name", name); Set(a, "Artist", "Artista"); Set(a, "Folder", name); Set(a, "CoverPath", name + ".jpg");
        foreach (object t in tracks) ((IList)Get(a, "Tracks")).Add(t); return a;
    }
    [STAThread] static int Main(string[] args)
    {
        F = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        object f = Codigo.FormSinEncender(F);
        typeof(Form).GetConstructor(Type.EmptyTypes).Invoke(f, new object[0]); // no constructor de la aplicacion, ni musica
        object t1 = Track("C:\\musica\\uno.mp3"), t2 = Track("C:\\musica\\dos.mp3");
        object a1 = Album("Disco A", t1, t2), a2 = Album("Disco B", t1);
        IList albums = (IList)Get(f, "albums"), tracks = (IList)Get(f, "tracks"); albums.Add(a1); albums.Add(a2); tracks.Add(t1); tracks.Add(t2);
        ((IDictionary)Get(f, "durCache"))["uno"] = 10f;
        Set(f, "lastFreshRootSig", "biblioteca-prueba");
        object plan = Call(f, "CapturaLecturaBiblioteca");
        Check(plan != null, "captura una biblioteca disponible");
        ((IDictionary)Get(f, "durCache"))["uno"] = 30f; tracks.Clear();
        Call(plan, "Prepara");
        Check((float)((IDictionary)Get(plan, "Dur"))["uno"] == 10f && ((IList)Get(plan, "Tracks")).Count == 2,
              "listas y medidas del plan no cambian al modificar las tablas UI");
        Check(Get(plan, "DurDatos") == null && Get(plan, "LoudDatos") == null, "el trabajador libera las copias de preparacion");
        bool volvio = true;
        using (ManualResetEvent dentro = new ManualResetEvent(false))
        {
            Thread hilo = new Thread(new ThreadStart(delegate { dentro.Set(); volvio = (bool)Call(plan, "Espera", 15000); }));
            hilo.Start(); dentro.WaitOne(); Thread.Sleep(20); Stopwatch reloj = Stopwatch.StartNew(); Call(plan, "Cancela");
            Check(hilo.Join(1000) && !volvio && reloj.ElapsedMilliseconds < 1000, "cancelar despierta la espera de arranque de 15 s");
        }
        ((IDisposable)plan).Dispose(); Call(plan, "Cancela");
        Check(!(bool)Call(plan, "Espera", 1), "cancelacion despues de liberar la espera es segura");
        Set(f, "libBuilding", true); Check(Call(f, "CapturaLecturaBiblioteca") == null, "no captura listas mientras F5 las arma");
        Set(f, "libBuilding", false);
        IDictionary index = (IDictionary)Call(null, "IndiceTop", albums);
        IList origins = (IList)index["C:\\musica\\uno.mp3"];
        Check(origins.Count == 2 && ReferenceEquals(Get(origins[0], "Disco"), a1) && ReferenceEquals(Get(origins[1], "Disco"), a2),
              "indice TOP conserva todos los discos de origen de una ruta repetida");
        Check((int)Get(origins[0], "Orden") == 0 && (int)Get(origins[1], "Orden") == 2, "indice conserva orden original incluso con duplicados");
        IDictionary counts = (IDictionary)Get(f, "songCounts"); counts["C:\\musica\\uno.mp3"] = 5; counts["C:\\musica\\dos.mp3"] = 3;
        Set(f, "topDirty", true); Call(f, "BuildTopDiscs");
        string before = Top(Get(f, "topDiscs"));
        Set(f, "origenesTop", index); Set(f, "albumsDelIndiceTop", albums); Set(f, "topDirty", true); Call(f, "BuildTopDiscs");
        Check(before == Top(Get(f, "topDiscs")), "TOP indexado equivale al respaldo, con portadas y origenes");
        object oldPerfil = Get(null, "perfilDisco"); object oldSlow = Get(null, "diskSlow");
        IDictionary unidades = (IDictionary)Get(null, "discoLento"); Hashtable antes = new Hashtable(unidades);
        try
        {
            Set(null, "perfilDisco", 0); Set(null, "diskSlow", true); unidades.Clear(); unidades["C:\\"] = true; unidades["D:\\"] = false;
            Check(!(bool)Call(null, "RepasoEnDiscoLento"), "biblioteca mixta no limita globalmente el SSD al HDD");
            object turno = Call(f, "PideTurnoAnalisis", "C:\\musica\\uno.mp3", new Func<bool>(delegate { return false; }));
            object cancelado = Call(f, "PideTurnoAnalisis", "C:\\musica\\dos.mp3", new Func<bool>(delegate { return true; }));
            Check(turno != null && cancelado == null, "turno HDD cancelable sin abrir otro lector simultaneo");
            object ssd = Call(f, "PideTurnoAnalisis", "D:\\musica\\otro.mp3", new Func<bool>(delegate { return false; }));
            Check(ssd != null, "SSD independiente no espera al turno HDD");
            ((IDisposable)ssd).Dispose(); ((IDisposable)turno).Dispose();
            Check(((IDictionary)Get(f, "analisisUnidad")).Count == 0, "al terminar se liberan los turnos");
            Set(null, "perfilDisco", 1); Check(!(bool)Call(null, "RepasoEnDiscoLento"), "perfil SSD forzado conserva paralelismo");
            Set(null, "perfilDisco", 2); Check((bool)Call(null, "RepasoEnDiscoLento"), "perfil HDD forzado conserva proteccion");
            Check((int)Call(null, "CarrilesPara", true, true, false, 2, false) == 1
                && (int)Call(null, "CarrilesPara", true, true, false, 16, false) > 1, "dos hilos protegidos; maquina potente puede usar varios carriles");
        }
        finally { Set(null, "perfilDisco", oldPerfil); Set(null, "diskSlow", oldSlow); unidades.Clear(); foreach (DictionaryEntry e in antes) unidades[e.Key] = e.Value; }
        Check(Codigo.Nombres(F.GetMethod("StartScan", B)).IndexOf("CancelaPlanesBiblioteca") >= 0, "F5 cancela los planes antes del escaneo");
        Console.WriteLine(checks + " comprobaciones de planificacion; sin HDD/FPS reales."); return 0;
    }
    static string Top(object value)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        foreach (object a in (IEnumerable)value)
        { b.Append(Get(a, "Name")).Append('|'); foreach (object t in (IEnumerable)Get(a, "Tracks")) b.Append(Get(t, "Path")).Append(';');
          foreach (object origin in (IEnumerable)Get(a, "TrackAlbums")) b.Append(Get(origin, "Name")).Append(';'); }
        return b.ToString();
    }
}
