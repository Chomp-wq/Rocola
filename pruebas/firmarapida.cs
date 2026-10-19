using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
// LA FIRMA DE LA BIBLIOTECA, MAS RAPIDA PERO LA MISMA (19-10-2026): Form1.Biblioteca.cs (RootHash, OrdenaRutasDeCarpeta, FirmaCarpeta).
//
// La firma de la biblioteca (rocola_biblioteca.txt) decide si el arranque se salta el recorrido del disco: si cambia UNA letra, todas las
// bibliotecas ya guardadas dejan de valer. Por eso aqui no se pide "parecida" sino IGUAL, letra por letra, que la del codigo anterior,
// que va copiado tal cual en esta bateria (RefHash, RefFirma) como referencia:
//   1) RootHash: la minuscula a mano para ASCII da el mismo valor que ToLowerInvariant en TODOS los caracteres (los 65.536) y en cadenas
//      reales (acentos, I turca con punto y sin punto, eszett, el signo Kelvin, mayusculas y minusculas mezcladas, rutas con prefijos largos);
//   2) OrdenaRutasDeCarpeta (ordenar por la parte del nombre) deja las rutas en el mismo orden que Array.Sort(rutas, OrdinalIgnoreCase), tambien
//      con empates, con prefijos que no son comunes (vuelve a lo de siempre) y con rutas de Windows y de Linux;
//   3) la firma de arboles de verdad (nombres con acentos, I turca, eszett, Cirilico, mayusculas mezcladas, carpetas vacias, un atajo, una carpeta
//      "Windows", miles de archivos) sale igual con el camino de .NET 4 y con el de CLR 2 y con el codigo anterior.
class FirmaRapida
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }

    delegate void HashDel(ref ulong h, string s);
    delegate void FirmaDel(string dir, object memo, bool meta, Stack<string> pendientes, ref ulong h, ref long filesN);
    static HashDel NuevoHash;
    static FirmaDel NuevaFirma;

    // ===== el codigo de antes, copiado tal cual =====
    static void RefHash(ref ulong h, string s)
    {
        if (s == null) s = "";
        for (int i = 0; i < s.Length; i++) { h ^= char.ToLowerInvariant(s[i]); h *= 1099511628211UL; }
        h ^= 0xff; h *= 1099511628211UL;
    }
    static void RefFirma(string dir, object memo, bool metadataEnumerada, Stack<string> pendientes, ref ulong h, ref long filesN)
    {
        long stamp = Directory.GetLastWriteTimeUtc(dir).Ticks;
        RefHash(ref h, dir); RefHash(ref h, stamp.ToString(CultureInfo.InvariantCulture));
        FileInfo[] files = null; string[] paths = null; int count;
        if (metadataEnumerada)
        {
            files = new DirectoryInfo(dir).GetFiles();
            Array.Sort(files, delegate(FileInfo a, FileInfo b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name); });
            count = files.Length;
        }
        else
        {
            paths = Directory.GetFiles(dir);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            count = paths.Length;
        }
        for (int i = 0; i < count; i++)
        {
            FileInfo fi = metadataEnumerada ? files[i] : new FileInfo(paths[i]); filesN++;
            RefHash(ref h, fi.FullName); RefHash(ref h, fi.Length.ToString(CultureInfo.InvariantCulture));
            RefHash(ref h, fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
        }
        string[] subs = Directory.GetDirectories(dir);
        Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
        for (int i = subs.Length - 1; i >= 0; i--)
            if (!(bool)TF.GetMethod("EsAtajo", T).Invoke(null, new object[] { subs[i] }) && !(bool)TF.GetMethod("EsCarpetaDelSistema", T).Invoke(null, new object[] { subs[i] })) pendientes.Push(subs[i]);
    }
    // El bucle de RootSigCaptured con la carpeta por parametro (la funcion de la rocola decide el modo por la version del CLR; aqui se elige).
    static string Firma(FirmaDel carpeta, string root, bool meta)
    {
        ulong h = 1469598103934665603UL; long filesN = 0, dirsN = 0;
        Stack<string> pendientes = new Stack<string>(); pendientes.Push(Path.GetFullPath(root));
        while (pendientes.Count > 0)
        {
            string dir = pendientes.Pop(); dirsN++;
            try { carpeta(dir, null, meta, pendientes, ref h, ref filesN); }
            catch (Exception ex) { RefHash(ref h, "!" + ex.GetType().Name); }
        }
        return dirsN.ToString(CultureInfo.InvariantCulture) + ":" + filesN.ToString(CultureInfo.InvariantCulture) + ":" + h.ToString("x16");
    }
    static FirmaDel DeEnsamblado(Type form)
    {
        // Un DynamicMethod con la forma de FirmaDel que llama a la funcion privada de la rocola: asi los parametros por referencia (el hash a medias
        // cuando algo falla) se comportan igual que en el codigo de antes, sin pasar por el arreglo de MethodInfo.Invoke.
        MethodInfo m = form.GetMethod("FirmaCarpeta", T);
        System.Reflection.Emit.DynamicMethod dm = new System.Reflection.Emit.DynamicMethod("FirmaCarpetaPrueba", typeof(void),
            new Type[] { typeof(string), typeof(object), typeof(bool), typeof(Stack<string>), typeof(ulong).MakeByRefType(), typeof(long).MakeByRefType() }, form.Module, true);
        System.Reflection.Emit.ILGenerator il = dm.GetILGenerator();
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
        il.Emit(System.Reflection.Emit.OpCodes.Castclass, m.GetParameters()[1].ParameterType);
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_2); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_3); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, (byte)4); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, (byte)5);
        il.Emit(System.Reflection.Emit.OpCodes.Call, m); il.Emit(System.Reflection.Emit.OpCodes.Ret);
        return (FirmaDel)dm.CreateDelegate(typeof(FirmaDel));
    }

    static string[] Nombres()
    {
        return new string[] { "01 - Uno.mp3", "02 - Dos.MP3", "Zebra.mp3", "abeja.mp3", "ABEJA 2.mp3", "\u00c1ngel.mp3", "\u00e1ngel 2.mp3", "\u00d1and\u00fa.mp3", "\u00f1and\u00fa 2.mp3",
            "\u0130stanbul.mp3", "\u0131spanak.mp3", "ISPANAK.mp3", "istanbul.mp3", "Stra\u00dfe.mp3", "STRASSE.mp3", "strasse.mp3", "\u212aelvin.mp3", "kelvin.mp3",
            "\u0416\u0443\u043a.mp3", "\u0436\u0443\u043a 2.mp3", "\u00c5ngstr\u00f6m.mp3", "\u00e5ngstr\u00f6m 2.mp3", "caf\u00e9.mp3", "CAF\u00c9 2.mp3", "x_y.mp3", "X-Y.mp3", "a b c.mp3", "A  B.mp3",
            "[1] intro.mp3", "(2) outro.mp3", "~tmp.mp3", "_a.mp3", "Z.mp3", "z.mp3 copia.mp3", "10.mp3", "9.mp3", "100.mp3", "folder.jpg", "Cover.JPG", "desktop.ini" };
    }
    static string Rutas(string prefijo, char sep, string nombre) { return prefijo + sep + nombre; }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        Type tAnt = null;
        if (args.Length > 1) { try { tAnt = Assembly.LoadFrom(args[1]).GetType("Rocola.Form1", true); } catch (Exception ex) { Console.WriteLine("  (sin el ejecutable anterior: " + ex.GetType().Name + ")"); } }
        NuevoHash = (HashDel)Delegate.CreateDelegate(typeof(HashDel), TF.GetMethod("RootHash", T));
        NuevaFirma = DeEnsamblado(TF);

        // ---------- 1) RootHash ----------
        int malos = 0;
        for (int c = 0; c <= 0xFFFF; c++)
        {
            string s = new string((char)c, 1);
            ulong a = 1469598103934665603UL, b = a; RefHash(ref a, s); NuevoHash(ref b, s);
            if (a != b) { if (malos++ < 3) Console.WriteLine("    distinto en U+" + c.ToString("X4")); }
        }
        Check(malos == 0, "RootHash da el mismo valor que ToLowerInvariant para los 65.536 caracteres uno por uno");
        List<string> cad = new List<string>(Nombres());
        cad.Add(null); cad.Add(""); cad.Add("C:\\Musica\\Rock\\Disco\\"); cad.Add("C:\\MUSICA\\ROCK\\DISCO\\"); cad.Add("c:\\musica\\rock\\disco\\");
        string largo = "D:\\" + new string('A', 120) + "\\" + new string('b', 100) + "\\\u0130\u0131\u00df\u212a";
        cad.Add(largo); cad.Add(largo.ToUpperInvariant()); cad.Add(largo.ToLowerInvariant()); cad.Add("\ud83c\udfb5 emoji.mp3"); cad.Add("\ud800 suelto"); cad.Add("\udc00");
        Random az = new Random(20261019);
        for (int i = 0; i < 4000; i++)
        {
            StringBuilder sb = new StringBuilder(); int n = az.Next(0, 90);
            for (int j = 0; j < n; j++)
            {
                int tipo = az.Next(10); char ch;
                if (tipo < 5) ch = (char)az.Next(32, 127); else if (tipo < 7) ch = (char)az.Next(128, 0x250); else if (tipo < 8) ch = (char)az.Next(0x370, 0x4FF);
                else if (tipo < 9) ch = "\u0130\u0131\u00df\u212a\u017f\u1e9e\u03a3\u03c3\u03c2"[az.Next(9)]; else ch = (char)az.Next(0, 0x10000);
                sb.Append(ch);
            }
            cad.Add(sb.ToString());
        }
        malos = 0;
        foreach (string s in cad)
        {
            ulong a = 1469598103934665603UL, b = a, a2 = 0xdeadbeefcafeUL, b2 = a2;
            RefHash(ref a, s); NuevoHash(ref b, s); RefHash(ref a2, s); NuevoHash(ref b2, s);
            if (a != b || a2 != b2) malos++;
        }
        Check(malos == 0, "y en " + cad.Count + " cadenas (ASCII, acentos, I turca, eszett, Kelvin, Cirilico, pares sustitutos, null, vacia, prefijos largos) y desde dos valores de partida");

        // ---------- 2) el orden ----------
        MethodInfo orden = TF.GetMethod("OrdenaRutasDeCarpeta", T, null, new Type[] { typeof(string[]) }, null);
        Check(orden != null, "existe OrdenaRutasDeCarpeta");
        string[] prefijos = { "C:\\Musica\\Rock", "C:\\Musica\\Rock\\Disco con un nombre bastante largo para que el prefijo pese (1999)", "/tmp/musica/\u00c1ngeles", "\\\\servidor\\recurso\\a", "C:", "/" };
        int ordenOk = 0, ordenMal = 0, conEmpates = 0;
        foreach (string pre in prefijos)
        {
            char sep = pre.IndexOf('\\') >= 0 ? '\\' : '/';
            for (int ronda = 0; ronda < 60; ronda++)
            {
                int n = az.Next(0, ronda < 10 ? 6 : 400);
                List<string> lista = new List<string>();
                string[] nom = Nombres();
                for (int i = 0; i < n; i++)
                {
                    string nm = nom[az.Next(nom.Length)];
                    if (az.Next(3) == 0) nm = nm + az.Next(1000);
                    if (az.Next(4) == 0) nm = nm.ToUpperInvariant();
                    lista.Add(Rutas(pre, sep, nm));
                }
                if (ronda % 7 == 3 && n > 2) lista[az.Next(n)] = "D:\\otra\\carpeta\\cosa.mp3";   // sin prefijo comun: vuelve a lo de siempre
                if (ronda % 11 == 5 && n > 2) lista[az.Next(n)] = "";
                string[] a = lista.ToArray(), b = lista.ToArray();
                Array.Sort(a, StringComparer.OrdinalIgnoreCase);
                orden.Invoke(null, new object[] { b });
                bool igual = a.Length == b.Length;
                for (int i = 0; igual && i < a.Length; i++) igual = a[i] == b[i];
                bool empate = false; for (int i = 1; i < a.Length; i++) if (string.Compare(a[i - 1], a[i], StringComparison.OrdinalIgnoreCase) == 0 && a[i - 1] != a[i]) empate = true;
                if (empate) conEmpates++;
                if (igual) ordenOk++; else { ordenMal++; if (ordenMal < 3) Console.WriteLine("    distinto: " + pre + " ronda " + ronda); }
            }
        }
        Check(ordenMal == 0, "OrdenaRutasDeCarpeta deja el mismo orden que Array.Sort(OrdinalIgnoreCase) en " + ordenOk + " listas de rutas de Windows y de Linux (" + conEmpates + " con empates de mayusculas, otras sin prefijo comun o con una ruta vacia)");

        // ---------- 3) arboles de verdad ----------
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_firmarapida_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            string raiz = Path.Combine(tmp, "Musica");
            Directory.CreateDirectory(raiz);
            DateTime baseT = new DateTime(2024, 3, 5, 10, 20, 30, DateTimeKind.Utc);
            Random az2 = new Random(7);
            string[] nombres = Nombres();
            List<string> carpetas = new List<string>();
            carpetas.Add(raiz);
            string[] dnom = { "Rock", "Pop", "\u00c1ngeles y Demonios", "\u0130stanbul", "Stra\u00dfe", "Windows", "Program Files", "Vac\u00eda", "ZETA", "alfa", "Disco 1", "Disco 2 (1999)", "CD1", "cd2" };
            for (int i = 0; i < dnom.Length; i++)
            {
                string d = Path.Combine(raiz, dnom[i]); Directory.CreateDirectory(d); carpetas.Add(d);
                if (i % 3 == 0) for (int j = 0; j < 4; j++) { string s = Path.Combine(d, dnom[(i + j + 1) % dnom.Length] + " " + j); Directory.CreateDirectory(s); carpetas.Add(s); }
            }
            Directory.CreateDirectory(Path.Combine(raiz, "Vac\u00eda", "vacia"));
            int hechos = 0;
            foreach (string d in carpetas)
            {
                if (d.EndsWith("Vac\u00eda")) continue;
                int n = d == raiz ? 15 : az2.Next(0, 40);
                for (int i = 0; i < n; i++)
                {
                    string nm = nombres[az2.Next(nombres.Length)];
                    if (File.Exists(Path.Combine(d, nm))) nm = i + " " + nm;
                    string f = Path.Combine(d, nm);
                    try { File.WriteAllBytes(f, new byte[az2.Next(0, 300)]); File.SetLastWriteTimeUtc(f, baseT.AddSeconds(az2.Next(0, 100000000)).AddTicks(az2.Next(0, 10000000))); hechos++; } catch { }
                }
            }
            // un directorio grande: 3000 archivos de nombre ASCII, para el orden y para el tiempo
            string grande = Path.Combine(raiz, "Grande"); Directory.CreateDirectory(grande);
            for (int i = 0; i < 3000; i++) File.WriteAllBytes(Path.Combine(grande, "Pista " + (i * 7919 % 3001).ToString("0000") + " - " + (char)('A' + i % 26) + (char)('a' + (i * 3) % 26) + ".mp3"), new byte[i % 5]);
            // un atajo a una carpeta (como la rocola: no se baja a el)
            string trg = Path.Combine(raiz, "Rock"), lnk = Path.Combine(raiz, "atajo");
            bool enlace = false;
            try { enlace = Simbolico(trg, lnk); } catch { }
            Console.WriteLine("  (" + hechos + " archivos de prueba" + (enlace ? ", con un atajo (enlace simbolico) a una carpeta" : ", sin atajo: este sistema no lo permite") + ")");

            string r1 = Firma(RefFirmaDel(), raiz, true), r2 = Firma(RefFirmaDel(), raiz, false);
            string n1 = Firma(NuevaFirma, raiz, true), n2 = Firma(NuevaFirma, raiz, false);
            Check(!r1.StartsWith("0:") && r1.Split(':')[0] != "1", "la referencia vio varias carpetas y archivos (" + r1.Substring(0, r1.LastIndexOf(':')) + ")");
            Check(r1 == n1, "camino de .NET 4 (FileInfo[] ordenados por nombre): igual que antes: " + n1);
            Check(r2 == n2, "camino de CLR 2 (Directory.GetFiles ordenado por ruta): igual que antes: " + n2);
            Check(n1 == n2, "y los dos caminos dan la misma firma entre si");
            if (tAnt != null)
            {
                FirmaDel ant = DeEnsamblado(tAnt);
                string a1 = Firma(ant, raiz, true), a2 = Firma(ant, raiz, false);
                Check(a1 == n1 && a2 == n2, "y contra el ejecutable ANTERIOR de verdad (" + a1 + " / " + a2 + ")");
            }
            // Y con la funcion real de la rocola de arriba abajo (RootSig decide el modo por la version del CLR).
            string real = (string)TF.GetMethod("RootSig", T).Invoke(null, new object[] { raiz });
            Check(real == n1 || real == n2, "RootSig de la rocola (modo del CLR de aqui) da la misma firma: " + real);

            // ---------- tiempos (informativo; Mono/Linux, no es Windows XP ni el A4) ----------
            List<string> rutas = new List<string>();
            string pref = "C:\\Users\\Rocola\\Musica\\Artista con nombre largo\\Disco con nombre largo (1999)\\";
            for (int i = 0; i < 30000; i++) rutas.Add(pref + "Pista " + (i * 7919 % 30011).ToString("00000") + " - Cancion Numero " + i + ".mp3");
            Stopwatch sw = new Stopwatch();
            double tRefHash = 0, tNuevoHash = 0, tRefOrden = 0, tNuevoOrden = 0;
            for (int rep = 0; rep < 5; rep++)
            {
                ulong h1 = 1; sw.Reset(); sw.Start(); for (int i = 0; i < rutas.Count; i++) { RefHash(ref h1, rutas[i]); RefHash(ref h1, "123456"); RefHash(ref h1, "638000000000000000"); } sw.Stop(); tRefHash += sw.Elapsed.TotalMilliseconds;
                ulong h2 = 1; sw.Reset(); sw.Start(); for (int i = 0; i < rutas.Count; i++) { NuevoHash(ref h2, rutas[i]); NuevoHash(ref h2, "123456"); NuevoHash(ref h2, "638000000000000000"); } sw.Stop(); tNuevoHash += sw.Elapsed.TotalMilliseconds;
                if (h1 != h2) Check(false, "los tiempos: mismos hashes");
                string[] a = rutas.ToArray(), b = rutas.ToArray();
                sw.Reset(); sw.Start(); Array.Sort(a, StringComparer.OrdinalIgnoreCase); sw.Stop(); tRefOrden += sw.Elapsed.TotalMilliseconds;
                sw.Reset(); sw.Start(); orden.Invoke(null, new object[] { b }); sw.Stop(); tNuevoOrden += sw.Elapsed.TotalMilliseconds;
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { Check(false, "los tiempos: mismo orden"); break; }
            }
            Console.WriteLine("  (Mono/Linux, 30.000 rutas, media de 5: hash de 3 cadenas por archivo " + (tRefHash / 5).ToString("0.0") + " -> " + (tNuevoHash / 5).ToString("0.0") + " ms; ordenar " + (tRefOrden / 5).ToString("0.0") + " -> " + (tNuevoOrden / 5).ToString("0.0") + " ms. Medidas sinteticas, no son de Windows XP)");
            Check(tNuevoHash <= tRefHash * 1.25 + 5 && tNuevoOrden <= tRefOrden * 1.25 + 5, "ni el hash ni el orden nuevos son mas lentos que los de antes (con margen de ruido)");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  OK firmarapida: " + oks + " comprobaciones (Mono/Linux)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }

    // --- ayudas ---
    static FirmaDel RefFirmaDel() { return RefFirma; }
    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] static extern int symlink(string destino, string enlace);
    static bool Simbolico(string destino, string enlace) { return symlink(destino, enlace) == 0; }
}
