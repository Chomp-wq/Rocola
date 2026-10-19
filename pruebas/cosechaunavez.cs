using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
// LA COSECHA DE LA IA, UNA VEZ POR CANCION (19-10-2026): Form1.Ia.cs (IaAppendPairsLote, IaApuntaOrigenes, IaApuntaGeneros, IaOrigenParaCanal) y
// Form1.Sonido.cs (MeasureFile).
//
// La cosecha escribia cada canal de una cancion por su cuenta: sacaba su origen (leyendo la etiqueta del archivo, el genero, las huellas), anexaba sus
// pares, confirmaba en disco, anotaba su entrada del indice, confirmaba, anotaba su linea de genero y confirmaba: seis confirmaciones (FlushFileBuffers) y dos
// lecturas de etiqueta por cancion en estereo. Ahora el origen se saca una vez y los canales se anexan juntos con una confirmacion por archivo. Todo lo
// que se guarda tiene que ser IGUAL, byte a byte, a lo que dejaban los canales anexados uno tras otro con IaAppendPairsOrigen (que es un lote de uno):
//   1) estereo, mono, varias canciones seguidas, lotes invalidos (vacio, no multiplo de 40, null) entre otros buenos, sin origen en alguno;
//   2) archivos nuevos que apartan el indice y los generos que habia (.anterior), y archivos con cola rota de un apagon (datos, indice, generos);
//   3) el tope del archivo de material (el segundo canal no cabe: se escribe el primero, igual que antes), el archivo de datos ajeno (nada se toca) y el
//      indice ajeno (los datos se escriben, el indice y los generos no);
//   4) el origen del segundo canal es el del primero con otro numero de canal (IaOrigenParaCanal);
//   5) el enganche: la cosecha de MeasureFile saca el origen y anexa por IaAppendPairsLote.
class CosechaUnaVez
{
    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic;
    static Type form, originType;
    static Type formRef, originRef;   // el ejecutable ANTERIOR, si se pasa como segundo argumento: la referencia de verdad
    static MethodInfo unico, lote, paraCanal, unicoRef;
    static string raiz;
    static int checks, failures;
    static void Check(bool v, string t) { checks++; if (!v) failures++; Console.WriteLine((v ? "  OK    " : "  FALLO ") + t); }

    static object Nueva(string subcarpeta, long limite, out string dir) { return Nueva(form, subcarpeta, limite, out dir); }
    static object Nueva(Type form, string subcarpeta, long limite, out string dir)
    {
        dir = Path.Combine(raiz, subcarpeta);
        Directory.CreateDirectory(dir);
        form.GetField("carpetaDatos", SF).SetValue(null, dir);
        object f = FormatterServices.GetUninitializedObject(form);
        form.GetField("iaFileLock", IF).SetValue(f, new object());
        form.GetField("iaMaxBytes", IF).SetValue(f, limite);
        return f;
    }
    static object Origen(int cancion, int canal, string genero, ulong artista) { return Origen(originType, cancion, canal, genero, artista); }
    static object Origen(Type originType, int cancion, int canal, string genero, ulong artista)
    {
        object o = Activator.CreateInstance(originType, true);
        originType.GetField("Cancion").SetValue(o, (ulong)(cancion + 1) * 0x9E3779B97F4A7C15UL);
        originType.GetField("Carpeta").SetValue(o, 3UL);
        originType.GetField("Artista").SetValue(o, artista);
        originType.GetField("Rate").SetValue(o, 44100);
        originType.GetField("Frame").SetValue(o, 2048);
        originType.GetField("Canal").SetValue(o, canal);
        originType.GetField("Genero").SetValue(o, genero);
        return o;
    }
    static List<float> Pares(int filas, int semilla)
    {
        List<float> r = new List<float>(filas * 40);
        for (int i = 0; i < filas * 40; i++) r.Add((i % 13 == 0) ? -0.0f : (float)Math.Sin(i * .037 + semilla));
        return r;
    }
    // Una cancion = lista de (lote, origen). Se anexa "como antes" (lote por lote) o junta.
    sealed class Lote { public List<float> Pares; public object Origen; }
    static bool[] ComoAntes(object f, List<Lote> ls)
    {
        bool[] r = new bool[ls.Count];
        for (int i = 0; i < ls.Count; i++) r[i] = (bool)unico.Invoke(f, new object[] { ls[i].Pares, ls[i].Origen });
        return r;
    }
    static bool[] Junto(object f, List<Lote> ls)
    {
        List<float>[] a = new List<float>[ls.Count]; Array whole = Array.CreateInstance(originType, ls.Count);
        for (int i = 0; i < ls.Count; i++) { a[i] = ls[i].Pares; whole.SetValue(ls[i].Origen, i); }
        return (bool[])lote.Invoke(f, new object[] { a, whole });
    }
    static string Lista(string dir)
    {
        List<string> n = new List<string>();
        foreach (string f in Directory.GetFiles(dir)) n.Add(Path.GetFileName(f));
        n.Sort(StringComparer.Ordinal);
        return string.Join("|", n.ToArray());
    }
    static bool IgualesBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    // Compara todo lo que hay en las dos carpetas: los mismos archivos con los mismos bytes.
    static bool MismaCarpeta(string a, string b, out string porque)
    {
        porque = "";
        if (Lista(a) != Lista(b)) { porque = "archivos distintos: [" + Lista(a) + "] contra [" + Lista(b) + "]"; return false; }
        foreach (string f in Directory.GetFiles(a))
        {
            byte[] x = File.ReadAllBytes(f), y = File.ReadAllBytes(Path.Combine(b, Path.GetFileName(f)));
            if (!IgualesBytes(x, y)) { porque = Path.GetFileName(f) + " distinto (" + x.Length + " contra " + y.Length + " bytes)"; return false; }
        }
        return true;
    }
    static bool Igual(bool[] a, bool[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    delegate void Preparar(string dir);
    // Corre el mismo guion en dos carpetas nuevas, una como antes y otra junta, y exige lo mismo.
    static void Caso(string nombre, long limite, Preparar prepara, params List<Lote>[] canciones)
    {
        string dA, dB;
        object fa = Nueva(nombre + "_antes", limite, out dA);
        if (prepara != null) prepara(dA);
        List<bool> oa = new List<bool>(), ob = new List<bool>();
        foreach (List<Lote> c in canciones) oa.AddRange(ComoAntes(fa, c));
        int totalA = (int)form.GetField("iaPairsTotal", IF).GetValue(fa);
        object fb = Nueva(nombre + "_junto", limite, out dB);
        if (prepara != null) prepara(dB);
        foreach (List<Lote> c in canciones) ob.AddRange(Junto(fb, c));
        int totalB = (int)form.GetField("iaPairsTotal", IF).GetValue(fb);
        string porque;
        if (formRef != null)
        {
            string dR; object fr = Nueva(formRef, nombre + "_anterior", limite, out dR);
            if (prepara != null) prepara(dR);
            List<bool> orf = new List<bool>();
            foreach (List<Lote> c in canciones)
                foreach (Lote l in c)
                {
                    object o = null;
                    if (l.Origen != null)
                    {
                        o = Activator.CreateInstance(originRef, true);
                        foreach (string campo in new string[] { "Cancion", "Carpeta", "Artista", "Rate", "Frame", "Canal", "Genero" })
                            originRef.GetField(campo).SetValue(o, originType.GetField(campo).GetValue(l.Origen));
                    }
                    orf.Add((bool)unicoRef.Invoke(fr, new object[] { l.Pares, o }));
                }
            string pr;
            bool conRef = MismaCarpeta(dR, dB, out pr) && Igual(orf.ToArray(), ob.ToArray());
            Check(conRef, nombre + ": y contra el ejecutable ANTERIOR (IaAppendPairsOrigen canal por canal) " + (conRef ? "igual" : pr));
            form.GetField("carpetaDatos", SF).SetValue(null, dB);
        }
        bool igual = MismaCarpeta(dA, dB, out porque);
        Check(igual && Igual(oa.ToArray(), ob.ToArray()) && totalA == totalB,
              nombre + ": mismos bytes en todos los archivos, mismas respuestas y mismo contador (" + Lista(dB).Replace("|", ", ") + ")" + (igual ? "" : " -> " + porque));
    }
    static List<Lote> Cancion(int n, bool estereo, string genero, ulong artista, int filas0, int filas1)
    {
        List<Lote> c = new List<Lote>();
        c.Add(new Lote { Pares = Pares(filas0, n * 2), Origen = Origen(n, 0, genero, artista) });
        if (estereo) c.Add(new Lote { Pares = Pares(filas1, n * 2 + 1), Origen = Origen(n, 1, genero, artista) });
        return c;
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        form = asm.GetType("Rocola.Form1", true); originType = form.GetNestedType("IaOrigen", BindingFlags.NonPublic);
        if (args.Length > 1)
        {
            try
            {
                Assembly ar = Assembly.LoadFrom(args[1]);
                formRef = ar.GetType("Rocola.Form1", true); originRef = formRef.GetNestedType("IaOrigen", BindingFlags.NonPublic); unicoRef = formRef.GetMethod("IaAppendPairsOrigen", IF);
            }
            catch (Exception e) { Console.WriteLine("  (sin ejecutable anterior: " + e.GetType().Name + ")"); formRef = null; }
        }
        unico = form.GetMethod("IaAppendPairsOrigen", IF); lote = form.GetMethod("IaAppendPairsLote", IF); paraCanal = form.GetMethod("IaOrigenParaCanal", SF);
        Check(unico != null && lote != null && paraCanal != null && originType != null, "existen IaAppendPairsOrigen (un lote), IaAppendPairsLote e IaOrigenParaCanal");
        raiz = Path.Combine(Path.GetTempPath(), "rocola_cosecha_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(raiz);
        object previa = form.GetField("carpetaDatos", SF).GetValue(null);
        try
        {
            // ---------- 1) lo normal ----------
            Caso("estereo", 0, null, Cancion(1, true, "rock", 5, 30, 30));
            Caso("mono", 0, null, Cancion(2, false, "cumbia", 0, 7, 0));
            Caso("seguidas", 0, null, Cancion(3, true, "rock", 5, 12, 12), Cancion(4, false, "", 0, 3, 0), Cancion(5, true, "salsa", 77, 40, 41), Cancion(6, true, "rock", 5, 1, 2));
            Caso("grande", 0, null, Cancion(7, true, "rock", 5, 1536, 1536));
            // lotes invalidos entre buenos: vacio, no multiplo de 40, uno solo de 39, null
            List<Lote> raros = Cancion(8, true, "rock", 5, 5, 6);
            raros.Insert(1, new Lote { Pares = new List<float>(), Origen = Origen(8, 0, "x", 1) });
            raros.Insert(2, new Lote { Pares = new List<float>(new float[41]), Origen = Origen(8, 0, "x", 1) });
            raros.Add(new Lote { Pares = null, Origen = Origen(8, 0, "x", 1) });
            raros.Add(new Lote { Pares = new List<float>(new float[39]), Origen = Origen(8, 0, "x", 1) });
            Caso("invalidos", 0, null, raros);
            // sin origen en alguno (como las pruebas de los carriles): los datos se escriben, la entrada no
            List<Lote> sinOrigen = Cancion(9, true, "rock", 5, 5, 6); sinOrigen[0].Origen = null;
            Caso("sin_origen_el_primero", 0, null, sinOrigen, Cancion(10, true, "rock", 5, 5, 6));
            List<Lote> sinOrigen2 = Cancion(11, true, "rock", 5, 5, 6); sinOrigen2[1].Origen = null;
            Caso("sin_origen_el_segundo", 0, null, sinOrigen2);
            // generos: vacio, con tabulador y salto de linea, muy largo (el filtro los normaliza igual en los dos)
            Caso("generos_raros", 0, null, Cancion(12, true, "Rock\tLatino\r\ny mas", 5, 4, 4), Cancion(13, true, new string('g', 300), 5, 4, 4), Cancion(14, true, "UNKNOWN", 5, 4, 4));

            // ---------- 2) archivos que ya existen ----------
            Preparar conIndicePrevio = delegate(string d)
            {
                File.WriteAllBytes(Path.Combine(d, "rocola_ia_datos.idx"), new byte[] { (byte)'R', (byte)'I', (byte)'A', (byte)'X', 1, 0, 0, 0, 64, 0, 0, 0, 0, 0, 0, 0 });
                File.WriteAllText(Path.Combine(d, "rocola_ia_datos.generos.tsv"), "\nviejo\tjazz\n");
            };
            Caso("nuevo_aparta_lo_anterior", 0, conIndicePrevio, Cancion(15, true, "rock", 5, 6, 6));
            // datos con cabecera buena y una cola rota de un apagon (no multiplo de 160), indice con media entrada
            Preparar colaRota = delegate(string d)
            {
                string f = Path.Combine(d, "rocola_ia_datos.bin");
                byte[] cab = new byte[16 + 3 * 160 + 77];
                cab[0] = (byte)'R'; cab[1] = (byte)'I'; cab[2] = (byte)'A'; cab[3] = (byte)'D'; cab[4] = 1; cab[8] = 28; cab[12] = 12;
                for (int i = 16; i < cab.Length; i++) cab[i] = (byte)(i * 7);
                File.WriteAllBytes(f, cab);
                byte[] ix = new byte[16 + 64 + 30];
                ix[0] = (byte)'R'; ix[1] = (byte)'I'; ix[2] = (byte)'A'; ix[3] = (byte)'X'; ix[4] = 1; ix[8] = 64;
                for (int i = 16; i < ix.Length; i++) ix[i] = (byte)(i * 3);
                File.WriteAllBytes(Path.Combine(d, "rocola_ia_datos.idx"), ix);
                File.WriteAllText(Path.Combine(d, "rocola_ia_datos.generos.tsv"), "\nabcd\trock\n\nmedia linea sin salto");
            };
            Caso("cola_rota", 0, colaRota, Cancion(16, true, "rock", 5, 6, 6), Cancion(17, false, "pop", 5, 2, 0));
            Caso("indice_ajeno", 0, delegate(string d) { File.WriteAllBytes(Path.Combine(d, "rocola_ia_datos.idx"), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }); },
                  Cancion(18, true, "rock", 5, 6, 6));
            Caso("datos_ajenos", 0, delegate(string d) { File.WriteAllBytes(Path.Combine(d, "rocola_ia_datos.bin"), new byte[] { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 }); },
                  Cancion(19, true, "rock", 5, 6, 6));

            // ---------- 3) el tope ----------
            Caso("tope_justo_los_dos", 16 + 20 * 160, null, Cancion(20, true, "rock", 5, 10, 10));
            Caso("tope_cabe_uno", 16 + 15 * 160, null, Cancion(21, true, "rock", 5, 10, 10));
            Caso("tope_no_cabe_ninguno", 16 + 5 * 160, null, Cancion(22, true, "rock", 5, 10, 10));
            Caso("tope_el_segundo_si_cabe", 16 + 14 * 160, null, Cancion(23, true, "rock", 5, 10, 4), Cancion(24, true, "rock", 5, 10, 4));

            // ---------- el contador de la pantalla ----------
            {
                string d; object f = Nueva("contador", 0, out d);
                form.GetField("iaPairsTotal", IF).SetValue(f, 12345);
                bool[] r = Junto(f, Cancion(25, true, "rock", 5, 7, 9));
                Check(r[0] && r[1] && (int)form.GetField("iaPairsTotal", IF).GetValue(f) == 16, "el contador de ejemplos queda en el total real (7 + 9 = 16), como antes");
            }
            // ---------- el origen del segundo canal ----------
            {
                object a = Origen(30, 0, "rock", 5);
                object b = paraCanal.Invoke(null, new object[] { a, 1 });
                bool ok = !object.ReferenceEquals(a, b) && (int)originType.GetField("Canal").GetValue(b) == 1 && (int)originType.GetField("Canal").GetValue(a) == 0;
                foreach (string c in new string[] { "Cancion", "Carpeta", "Artista", "Rate", "Frame", "Genero" })
                    ok &= object.Equals(originType.GetField(c).GetValue(a), originType.GetField(c).GetValue(b));
                Check(ok, "IaOrigenParaCanal: todo igual salvo el canal, sin tocar el original");
                Check(object.ReferenceEquals(paraCanal.Invoke(null, new object[] { a, 0 }), a) && paraCanal.Invoke(null, new object[] { null, 1 }) == null, "mismo canal: el mismo origen; sin origen: sin origen");
                // contra IaOrigenDe de verdad, con un archivo que no existe y con uno con etiqueta ID3v1
                MethodInfo origenDe = form.GetMethod("IaOrigenDe", SF);
                string sin = Path.Combine(raiz, "no_existe.mp3");
                string con = Path.Combine(raiz, "con etiqueta.mp3");
                byte[] mp3 = new byte[3000]; byte[] tag = new byte[128];
                tag[0] = (byte)'T'; tag[1] = (byte)'A'; tag[2] = (byte)'G';
                System.Text.Encoding.ASCII.GetBytes("Titulo").CopyTo(tag, 3); System.Text.Encoding.ASCII.GetBytes("Un Artista").CopyTo(tag, 33); tag[127] = 17;
                tag.CopyTo(mp3, mp3.Length - 128);
                File.WriteAllBytes(con, mp3);
                bool todos = true;
                foreach (string ruta in new string[] { sin, con })
                {
                    object o0 = origenDe.Invoke(null, new object[] { ruta, 200.4, 44100, 2048, 0 }), o1 = origenDe.Invoke(null, new object[] { ruta, 200.4, 44100, 2048, 1 });
                    object c1 = paraCanal.Invoke(null, new object[] { o0, 1 });
                    foreach (string c in new string[] { "Cancion", "Carpeta", "Artista", "Rate", "Frame", "Canal", "Genero" })
                        todos &= object.Equals(originType.GetField(c).GetValue(o1), originType.GetField(c).GetValue(c1));
                }
                Check(todos, "el origen del canal 1 sacado una vez del canal 0 es el mismo que IaOrigenDe(canal 1) (archivo inexistente y archivo con etiqueta)");
            }

            // ---------- 5) el enganche ----------
            MethodInfo mide = form.GetMethod("MeasureFile", IF);
            List<string> ll = mide == null ? new List<string>() : Codigo.Llamadas(mide);
            int origenes = 0; foreach (string s in ll) if (s.EndsWith(".IaOrigenDe")) origenes++;
            Check(mide != null && Codigo.Llama(ll, "IaAppendPairsLote") && Codigo.Llama(ll, "IaOrigenParaCanal") && origenes == 1 && !Codigo.Llama(ll, "IaAppendPairsOrigen") && !Codigo.Llama(ll, "IaAppendPairs"),
                  "la cosecha de la biblioteca saca el origen una vez (IaOrigenDe) y anexa los canales por IaAppendPairsLote, no por el camino sin origen");
            MethodInfo lt = lote;
            int confirma = 0;
            foreach (string s in Codigo.Llamadas(lt)) if (s.EndsWith(".ConfirmaDatosIa")) confirma++;
            int flushEnLote = 0; foreach (string s in Codigo.Llamadas(lt)) if (s.EndsWith(".FlushFileBuffers")) flushEnLote++;
            Check(confirma >= 1 && flushEnLote == 0 && Codigo.Llama(form.GetMethod("ConfirmaDatosIa", IF), "FlushFileBuffers"),
                  "los datos se confirman por un solo camino (ConfirmaDatosIa) y no dentro del bucle de canales");
            MethodInfo ao = form.GetMethod("IaApuntaOrigenes", IF), ag = form.GetMethod("IaApuntaGeneros", IF);
            int fo = 0, fg = 0;
            foreach (string s in Codigo.Llamadas(ao)) if (s.EndsWith(".FlushFileBuffers")) fo++;
            foreach (string s in Codigo.Llamadas(ag)) if (s.EndsWith(".FlushFileBuffers")) fg++;
            Check(fo == 1 && fg == 1 && Codigo.Llama(ao, "IaApuntaGeneros"), "el indice y los generos se confirman una vez cada uno (un sitio de confirmacion en cada funcion)");
        }
        finally
        {
            form.GetField("carpetaDatos", SF).SetValue(null, previa);
            try { Directory.Delete(raiz, true); } catch { }
        }
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? ("  todo bien (" + checks + " comprobaciones)") : ("  FALLOS: " + failures));
        return failures == 0 ? 0 : 1;
    }
}
