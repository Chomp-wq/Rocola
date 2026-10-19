using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
// MENOS OBJETOS DE VIDA LARGA (19-10-2026): tres sitios donde la rocola guardaba, por cada cancion, objetos que no hacian falta.
//
//   a) RegistroEq (DiarioTablas.cs): el espejo de la tabla de EQ que usa el escritor copiaba el arreglo de 12 numeros de cada cancion; ahora comparte el de
//      eqCache (esos arreglos no se modifican una vez puestos: cada medida nueva arma otro). Se comprueba que se comparte y que lo que se escribe es igual.
//   b) RegistroFuente (FuentesMedidas.cs): eran cuatro objetos por cancion (el registro, su FuenteMedida y dos cadenas de 16 cifras hexadecimales); ahora
//      uno, con los tres numeros del sello y dos huellas de 64 bits. El archivo rocola_fuentes.txt sigue igual (16 cifras hexadecimales en mayusculas):
//      leerlo y escribirlo da los mismos bytes que antes, y se mide cuanta memoria cuesta una tabla de 20.000 canciones.
//   c) Track.FromFile (Form1.Vistas.cs): el artista que sale de la carpeta se calcula una vez por carpeta y las canciones de ella comparten la cadena.
//      Mismas canciones (ruta, artista, titulo, album, genero) que con el codigo de antes, en orden de carpeta y revueltas, y cuantas asignaciones se ahorran.
// Si se pasa el ejecutable anterior como segundo argumento, ademas se compara contra el codigo anterior de verdad y se mide la memoria de las dos formas.
class ObjetosMenos
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TAnt;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    // ---------- a) ----------
    static void Eq()
    {
        Console.WriteLine("\n  a) EL ESPEJO DEL EQ COMPARTE LOS ARREGLOS\n");
        Dictionary<string, float[]> eq = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        Random r = new Random(5);
        for (int i = 0; i < 500; i++) { float[] c = new float[12]; for (int k = 0; k < 12; k++) c[k] = (float)(r.NextDouble() * 20 - 10); eq["c" + i] = c; }
        object cambios = TF.GetMethod("PreparaEq", T).Invoke(null, new object[] { eq });
        System.Collections.IDictionary trabajo = (System.Collections.IDictionary)cambios.GetType().GetField("Trabajo", T).GetValue(cambios);
        bool comparte = trabajo.Count == eq.Count;
        foreach (KeyValuePair<string, float[]> kv in eq)
        {
            object reg = trabajo[kv.Key];
            float[] curva = (float[])reg.GetType().GetField("Curva", T).GetValue(reg);
            comparte &= object.ReferenceEquals(curva, kv.Value);
        }
        Check(comparte, "PreparaEq: cada registro del espejo usa el mismo arreglo que eqCache (500 canciones, ninguna copia)");
        // Una medida nueva sustituye el arreglo entero: el registro de antes sigue diciendo lo de antes (la copia sigue siendo una foto)
        object f = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
        TF.GetField("eqCache", T).SetValue(f, eq);
        TF.GetField("cambiosEq", T).SetValue(f, cambios);
        TF.GetField("medidasDeltasActivas", T).SetValue(f, true);
        float[] vieja = eq["c3"]; float[] nueva = (float[])vieja.Clone(); nueva[0] += 1f;
        eq["c3"] = nueva;
        TF.GetMethod("MarcarEq", T).Invoke(f, new object[] { "c3" });
        object reg3 = cambios.GetType().GetMethod("Copia", T).Invoke(cambios, null);
        System.Collections.IList lista = (System.Collections.IList)reg3;
        object kv0 = lista[0]; object val = kv0.GetType().GetProperty("Value").GetValue(kv0, null);
        float[] enRegistro = (float[])val.GetType().GetField("Curva", T).GetValue(val);
        Check(lista.Count == 1 && object.ReferenceEquals(enRegistro, nueva) && !object.ReferenceEquals(enRegistro, vieja) && vieja[0] != nueva[0],
              "MarcarEq: el registro apunta al arreglo nuevo que puso la medida; el viejo no se toco");
    }

    // ---------- b) ----------
    static string TextoFuentes(Random az, int n, bool conRaros)
    {
        StringBuilder sb = new StringBuilder("ROCOLA-FUENTES v1\r\n");
        for (int i = 0; i < n; i++)
        {
            long largo = az.Next(1000, int.MaxValue) * 3L, creada = 130000000000000000L + az.Next(), escrita = 130000000000000000L + az.Next();
            string med = ((ulong)az.Next() * 4294967296UL + (ulong)az.Next()).ToString("X16", CultureInfo.InvariantCulture);
            if (i % 11 == 0) med = ulong.MaxValue.ToString("X16"); if (i % 13 == 0) med = "0000000000000001";
            string mapa = (i % 3 == 0) ? "-" : ((ulong)az.Next() * 4294967296UL + (ulong)az.Next()).ToString("X16", CultureInfo.InvariantCulture);
            sb.Append(largo).Append('|').Append(creada).Append('|').Append(escrita).Append('|').Append(med).Append('|').Append(mapa).Append('|').Append("/musica/Disco ").Append(i / 20).Append("/Pista ").Append(i).Append(".mp3\r\n");
        }
        if (conRaros)
        {
            sb.Append("1|2|3|0123456789abcdef|-|/musica/minusculas.mp3\r\n");          // hexadecimal en minusculas
            sb.Append("1|2|3|0123456789ABCDEG|-|/musica/no_hex.mp3\r\n");              // 16 caracteres que no son hexadecimal
            sb.Append("1|2|3|0123456789ABCDEF|ZZZZZZZZZZZZZZZZ|/musica/mapa_no_hex.mp3\r\n");
            sb.Append("1|2|3|0123456789ABCDE|-|/musica/corta.mp3\r\n");                 // 15
            sb.Append("-1|2|3|0123456789ABCDEF|-|/musica/negativa.mp3\r\n");
            sb.Append("1|0|3|0123456789ABCDEF|-|/musica/creada_cero.mp3\r\n");
            sb.Append("1|2|3|0123456789ABCDEF|-|\r\n");                                 // sin ruta
            sb.Append("basura sin barras\r\n");
        }
        return sb.ToString();
    }
    static object LeeFuentes(Type form, string texto, out System.Collections.IDictionary d)
    {
        Type rf = form.GetNestedType("RegistroFuente", T);
        Type dt = typeof(Dictionary<,>).MakeGenericType(typeof(string), rf);
        d = (System.Collections.IDictionary)Activator.CreateInstance(dt, new object[] { StringComparer.OrdinalIgnoreCase });
        return form.GetMethod("LeeFuentes", T).Invoke(null, new object[] { new StringReader(texto), d });
    }
    static string EscribeTodo(Type form, System.Collections.IDictionary d)
    {
        MethodInfo e = form.GetMethod("EscribeFuente", T);
        List<string> claves = new List<string>(); foreach (object k in d.Keys) claves.Add((string)k);
        claves.Sort(StringComparer.Ordinal);
        StringWriter w = new StringWriter(CultureInfo.InvariantCulture);
        w.Write("ROCOLA-FUENTES v1\r\n");
        foreach (string k in claves) e.Invoke(null, new object[] { w, k, d[k] });
        return w.ToString();
    }
    static List<object> retenidas = new List<object>();   // las tablas de la medida de memoria siguen vivas, como en la rocola
    // La primera vuelta de un ejecutable carga el montaje y calienta el monton (da cifras raras): la cifra buena es la de la ultima de cuatro.
    static long Memoria(Type form, string texto)
    {
        long ultima = 0;
        for (int rep = 0; rep < 4; rep++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long antes = GC.GetTotalMemory(true);
            System.Collections.IDictionary d;
            LeeFuentes(form, texto, out d);
            retenidas.Add(d);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            ultima = GC.GetTotalMemory(true) - antes;
        }
        return ultima;
    }
    static void Fuentes()
    {
        Console.WriteLine("\n  b) UN OBJETO POR FUENTE MEDIDA, EL MISMO ARCHIVO\n");
        Random az = new Random(9);
        string limpio = TextoFuentes(az, 3000, false);
        System.Collections.IDictionary d;
        object estado = LeeFuentes(TF, limpio, out d);
        Check((int)estado == 1 && d.Count == 3000, "se leen las 3000 filas buenas (estado 1)");
        string salida = EscribeTodo(TF, d);
        // escribir en orden de claves: el archivo de entrada tambien va por orden de creacion; se compara linea a linea ordenadas
        List<string> a = new List<string>(limpio.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)), b = new List<string>(salida.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
        a.Sort(StringComparer.Ordinal); b.Sort(StringComparer.Ordinal);
        bool igual = a.Count == b.Count; for (int i = 0; igual && i < a.Count; i++) igual = a[i] == b[i];
        Check(igual, "leer y volver a escribir da las mismas 3000 filas, letra por letra (mismo formato: tres numeros, 16 cifras en mayusculas, \"-\" o 16 cifras, ruta)");
        if (TAnt != null)
        {
            System.Collections.IDictionary da;
            LeeFuentes(TAnt, limpio, out da);
            Check(EscribeTodo(TAnt, da) == salida, "y es el mismo texto que escribe el codigo anterior con el mismo archivo");
        }
        // lo raro: lo que se descartaba se sigue descartando (salvo los 16 caracteres que no son hexadecimal, que antes pasaban)
        string raros = TextoFuentes(new Random(3), 20, true);
        System.Collections.IDictionary dr;
        LeeFuentes(TF, raros, out dr);
        bool nohex = dr.Contains("/musica/no_hex.mp3") || dr.Contains("/musica/mapa_no_hex.mp3");
        bool descartadas = !dr.Contains("/musica/corta.mp3") && !dr.Contains("/musica/negativa.mp3") && !dr.Contains("/musica/creada_cero.mp3");
        Check(dr.Count == 21 && !nohex && descartadas && dr.Contains("/musica/minusculas.mp3"),
              "filas raras: se descartan la corta, la negativa, la de fecha cero, la sin ruta y la basura; tambien las de 16 caracteres que no son hexadecimal (antes se aceptaban y nunca coincidian); las de minusculas se leen");
        string minus = EscribeTodo(TF, dr);
        Check(minus.Contains("|0123456789ABCDEF|-|/musica/minusculas.mp3"), "y una huella en minusculas se vuelve a escribir en mayusculas (solo pasa con un archivo tocado a mano)");
        // las comparaciones de la firma
        object r1 = Activator.CreateInstance(TF.GetNestedType("RegistroFuente", T), true), r2 = Activator.CreateInstance(TF.GetNestedType("RegistroFuente", T), true);
        Type rt = r1.GetType();
        foreach (object o in new object[] { r1, r2 }) { rt.GetField("Largo", T).SetValue(o, 5L); rt.GetField("Creada", T).SetValue(o, 6L); rt.GetField("Escrita", T).SetValue(o, 7L); rt.GetField("Medidas", T).SetValue(o, 99UL); rt.GetField("Mapa", T).SetValue(o, 11UL); }
        MethodInfo igualReg = rt.GetMethod("Igual", T);
        bool v1 = (bool)igualReg.Invoke(r1, new object[] { r2 });
        rt.GetField("Mapa", T).SetValue(r2, 12UL); bool v2 = (bool)igualReg.Invoke(r1, new object[] { r2 });
        rt.GetField("SinMapa", T).SetValue(r1, true); rt.GetField("SinMapa", T).SetValue(r2, true); bool v3 = (bool)igualReg.Invoke(r1, new object[] { r2 });
        rt.GetField("SinMapa", T).SetValue(r2, false); bool v4 = (bool)igualReg.Invoke(r1, new object[] { r2 });
        rt.GetField("SinMapa", T).SetValue(r1, false); rt.GetField("SinMapa", T).SetValue(r2, false); rt.GetField("Mapa", T).SetValue(r2, 11UL); rt.GetField("Medidas", T).SetValue(r2, 98UL); bool v5 = (bool)igualReg.Invoke(r1, new object[] { r2 });
        rt.GetField("Medidas", T).SetValue(r2, 99UL); rt.GetField("Escrita", T).SetValue(r2, 8L); bool v6 = (bool)igualReg.Invoke(r1, new object[] { r2 });
        Check(v1 && !v2 && v3 && !v4 && !v5 && !v6, "RegistroFuente.Igual: sello, huella de las medidas y mapa (o ausencia de mapa) cuentan; un mapa distinto no importa si los dos no tienen");
        // memoria
        string grande = TextoFuentes(new Random(21), 20000, false);
        long nuevo = Memoria(TF, grande), viejo = TAnt != null ? Memoria(TAnt, grande) : -1;
        Console.WriteLine("     20.000 fuentes en memoria (Mono/Linux, contando las rutas, que son lo mismo antes y despues): " + (nuevo / 20000) + " bytes por cancion" + (viejo >= 0 ? " contra " + (viejo / 20000) + " con el codigo anterior (" + ((viejo - nuevo) / 1024) + " KB menos)" : ""));
        Check(viejo < 0 || nuevo < viejo, "el registro nuevo ocupa menos que los cuatro objetos de antes");
    }

    // ---------- c) ----------
    static Type TTrack;
    static object FromFileNuevo(string p) { return TTrack.GetMethod("FromFile", T).Invoke(null, new object[] { p }); }
    static string Campos(Type tt, object t)
    {
        StringBuilder sb = new StringBuilder();
        foreach (string c in new string[] { "Path", "Artist", "Title", "Album", "Genre" }) sb.Append(tt.GetField(c).GetValue(t)).Append('\u0001');
        return sb.ToString();
    }
    // FromFile de antes, copiado tal cual con las ayudas privadas de la rocola
    static object FromFileAntes(string path)
    {
        object t = Activator.CreateInstance(TTrack, true);
        TTrack.GetField("Path").SetValue(t, path); TTrack.GetField("Artist").SetValue(t, ""); TTrack.GetField("Title").SetValue(t, ""); TTrack.GetField("Album").SetValue(t, ""); TTrack.GetField("Genre").SetValue(t, "");
        string name = Path.GetFileNameWithoutExtension(path);
        string fa, ft;
        bool split = Split(name, out fa, out ft);
        string artist = "", title = "";
        if (split && Digits(fa))
        {
            string fa2, ft2;
            if (Split(ft, out fa2, out ft2) && !Digits(fa2)) { artist = fa2; title = ft2; }
            else title = name;
        }
        else if (split) { artist = fa; title = ft; }
        else { title = name; }
        if (string.IsNullOrEmpty(artist))
        {
            string d = Path.GetDirectoryName(path);
            string leaf = (d != null) ? (string)TF.GetMethod("LeafName", T).Invoke(null, new object[] { d }) : "Desconocido";
            string da, dn;
            if (Split(leaf, out da, out dn)) artist = da; else artist = leaf;
        }
        TTrack.GetField("Artist").SetValue(t, artist); TTrack.GetField("Title").SetValue(t, title);
        return t;
    }
    static bool Split(string s, out string l, out string r)
    { object[] a = new object[] { s, null, null }; bool b = (bool)TF.GetMethod("SplitArtist", T).Invoke(null, a); l = (string)a[1]; r = (string)a[2]; return b; }
    static bool Digits(string s) { return (bool)TF.GetMethod("AllDigits", T).Invoke(null, new object[] { s }); }

    static void Pistas()
    {
        Console.WriteLine("\n  c) EL ARTISTA DE LA CARPETA, UNA VEZ POR CARPETA\n");
        TTrack = TF.GetNestedType("Track", T);
        string[] carpetas = { "Artista Uno - Album A", "Album Suelto", "CD1", "CD2", "Various Artists - Hits (1999)", "01 - Intro", "Ángeles - Vida", "Cumbia Mix", "ROCK - clasico", "x-y", "-raro", "  espacios  ", "Un Disco Con Un Nombre Muy Largo Para Probar Prefijos Largos - Tomo 2" };
        string[] archivos = { "Artista - Titulo.mp3", "01 - Titulo.mp3", "01 - Artista - Titulo.mp3", "Titulo suelto.mp3", "12 Titulo.mp3", "02 - 03 - Cosa.mp3", "Ñandú - Uno.mp3", "a-b.mp3", "99.mp3", " - raro.mp3", "Titulo - .mp3", "Otro Artista - Tema.MP3", "x.flac", "sin extension" };
        Random az = new Random(2);
        List<string> rutas = new List<string>();
        for (int d = 0; d < 80; d++)
        {
            string car = "/musica/" + (d % 7 == 0 ? "" : "Nivel " + (d % 4) + "/") + carpetas[d % carpetas.Length] + (d >= carpetas.Length ? " " + d : "");
            for (int i = 0; i < 25; i++) rutas.Add(car + "/" + archivos[az.Next(archivos.Length)].Replace("Titulo", "T" + i));
        }
        rutas.Add("/suelta.mp3"); rutas.Add("suelta2.mp3"); rutas.Add("/musica/Artista Uno - Album A/otra.mp3");
        // carpetas con prefijos casi iguales: "/musica/A" y "/musica/B", y una con el mismo largo de prefijo
        rutas.Add("/musica/Aa/x - y.mp3"); rutas.Add("/musica/Ab/x.mp3"); rutas.Add("/musica/Aa/z.mp3"); rutas.Add("/musica/Ab/zz.mp3"); rutas.Add("/musica/AA/x.mp3");
        // rutas con separadores mezclados
        rutas.Add("/musica/Mixta\\Sub - Album/a.mp3"); rutas.Add("/musica/Mixta\\Sub - Album/b.mp3");
        List<string> revueltas = new List<string>(rutas);
        for (int i = revueltas.Count - 1; i > 0; i--) { int j = az.Next(i + 1); string tmp = revueltas[i]; revueltas[i] = revueltas[j]; revueltas[j] = tmp; }
        int malos = 0, n = 0;
        foreach (List<string> orden in new List<string>[] { rutas, revueltas, rutas })
            foreach (string p in orden)
            {
                string a = Campos(TTrack, FromFileAntes(p)), b = Campos(TTrack, FromFileNuevo(p)); n++;
                if (a != b) { if (malos++ < 3) Console.WriteLine("    distinto: " + p + "\n       antes " + a.Replace('\u0001', '|') + "\n       ahora " + b.Replace('\u0001', '|')); }
            }
        Check(malos == 0, "FromFile da las mismas canciones (ruta, artista, titulo, album, genero) que el de antes en " + n + " llamadas, por carpetas, revueltas y de nuevo por carpetas");
        if (TAnt != null)
        {
            Type tt = TAnt.GetNestedType("Track", T); MethodInfo ff = tt.GetMethod("FromFile", T);
            int m2 = 0;
            foreach (string p in revueltas) if (Campos(tt, ff.Invoke(null, new object[] { p })) != Campos(TTrack, FromFileNuevo(p))) m2++;
            Check(m2 == 0, "y contra el FromFile del ejecutable anterior de verdad, revueltas");
        }
        // las canciones de una carpeta comparten el artista (la misma cadena)
        object p0 = FromFileNuevo("/musica/Comparte/01 - Uno.mp3"), p1 = FromFileNuevo("/musica/Comparte/02 - Dos.mp3"), p2 = FromFileNuevo("/musica/Comparte/Tres.mp3");
        string a0 = (string)TTrack.GetField("Artist").GetValue(p0), a1 = (string)TTrack.GetField("Artist").GetValue(p1), a2 = (string)TTrack.GetField("Artist").GetValue(p2);
        Check(a0 == "Comparte" && object.ReferenceEquals(a0, a1) && object.ReferenceEquals(a1, a2), "las canciones seguidas de una carpeta comparten la misma cadena de artista");
        object q = FromFileNuevo("/musica/Otra Carpeta/Tres.mp3");
        Check((string)TTrack.GetField("Artist").GetValue(q) == "Otra Carpeta", "y al cambiar de carpeta se vuelve a sacar");
        // una cancion con artista en el nombre no usa ni cambia la recordada
        object con = FromFileNuevo("/musica/Comparte/Alguien - Tema.mp3");
        object despues = FromFileNuevo("/musica/Comparte/Cuatro.mp3");
        Check((string)TTrack.GetField("Artist").GetValue(con) == "Alguien" && (string)TTrack.GetField("Artist").GetValue(despues) == "Comparte", "una cancion con \"Artista - Titulo\" en el nombre conserva el suyo y no estropea lo recordado");
        // varios hilos a la vez: nunca el artista de otra carpeta
        int mal = 0;
        System.Threading.Thread[] hs = new System.Threading.Thread[4];
        for (int h = 0; h < hs.Length; h++)
        {
            int yo = h;
            hs[h] = new System.Threading.Thread(delegate()
            {
                for (int i = 0; i < 3000; i++)
                {
                    string carp = "Hilo" + ((i / 7 + yo) % 5);
                    object t = FromFileNuevo("/musica/" + carp + "/Pista " + i + ".mp3");
                    if ((string)TTrack.GetField("Artist").GetValue(t) != carp) System.Threading.Interlocked.Increment(ref mal);
                }
            });
            hs[h].Start();
        }
        foreach (System.Threading.Thread h in hs) h.Join();
        Check(mal == 0, "con cuatro hilos llamando a la vez (12.000 canciones de cinco carpetas) nunca sale el artista de otra carpeta");
        // memoria: asignaciones de 30.000 canciones
        List<string> todas = new List<string>();
        for (int d = 0; d < 1500; d++) for (int i = 0; i < 20; i++) todas.Add("/musica/Artista " + d + " - Album " + d + "/" + (i + 1).ToString("00") + " - Cancion " + i + ".mp3");
        // lo retenido: las listas de canciones
        List<object> r1 = new List<object>(); GC.Collect(); long m0 = GC.GetTotalMemory(true);
        foreach (string p in todas) r1.Add(FromFileNuevo(p));
        GC.Collect(); long m1 = GC.GetTotalMemory(true);
        List<object> r2 = new List<object>(); GC.Collect(); long m2b = GC.GetTotalMemory(true);
        foreach (string p in todas) r2.Add(FromFileAntes(p));
        GC.Collect(); long m3 = GC.GetTotalMemory(true);
        Console.WriteLine("     retenido por 30.000 canciones: antes " + ((m3 - m2b) / 1024) + " KB, ahora " + ((m1 - m0) / 1024) + " KB (Mono/Linux, sintetico)");
        Check((m1 - m0) < (m3 - m2b), "las 30.000 canciones retienen menos memoria (el artista de cada carpeta se comparte)");
        GC.KeepAlive(r1); GC.KeepAlive(r2);
    }
    static int Main(string[] args)
    {
        TF = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe").GetType("Rocola.Form1", true);
        if (args.Length > 1) { try { TAnt = Assembly.Load(File.ReadAllBytes(args[1])).GetType("Rocola.Form1", true); } catch (Exception e) { Console.WriteLine("  (sin ejecutable anterior: " + e.GetType().Name + ")"); } }
        try { Eq(); Fuentes(); Pistas(); }
        catch (Exception e) { Check(false, "sin excepciones: " + (e.InnerException ?? e)); }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  todo bien (" + oks + " comprobaciones)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }
}
