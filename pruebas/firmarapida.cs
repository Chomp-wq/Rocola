using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
// LA FIRMA DE LA BIBLIOTECA, MAS RAPIDA PERO LA MISMA (19-10-2026): Form1.Biblioteca.cs (RootHash, OrdenaRutasDeCarpeta, FirmaCarpeta,
// FirmaListado) y RecorridoRapido.cs (ListadoCarpeta, ListadoWin32).
//
// La firma de la biblioteca (rocola_biblioteca.txt) decide si el arranque se salta el recorrido del disco: si cambia UNA letra, todas las
// bibliotecas ya guardadas dejan de valer. Por eso aqui no se pide "parecida" sino IGUAL, letra por letra, que la del codigo anterior,
// que va copiado tal cual en esta bateria (RefHash, RefFirma) como referencia:
//   1) RootHash: la minuscula a mano para ASCII da el mismo valor que ToLowerInvariant en TODOS los caracteres (los 65.536) y en cadenas
//      reales (acentos, I turca con punto y sin punto, eszett, el signo Kelvin, mayusculas y minusculas mezcladas, rutas con prefijos largos);
//   2) OrdenaRutasDeCarpeta (ordenar por la parte del nombre) deja las rutas en el mismo orden que Array.Sort(rutas, OrdinalIgnoreCase), tambien
//      con empates, con prefijos que no son comunes (vuelve a lo de siempre) y con rutas de Windows y de Linux;
//   3) la firma de arboles de verdad (nombres con acentos, I turca, eszett, Cirilico, mayusculas mezcladas, carpetas vacias, un atajo, una carpeta
//      "Windows", miles de archivos) sale igual con el camino de .NET 4 y con el de CLR 2, con el codigo anterior y con el recorrido rapido;
//   4) el recorrido rapido se prueba con un origen de datos inventado (la parte de Windows, FindFirstFile, NO se puede ejecutar fuera de
//      Windows y esta bateria no lo afirma): lo que hace con las entradas (orden, tamano, fecha, atajos, ".", "..", nombres raros, rutas
//      largas, errores) y que, si el origen falla o devuelve null, la firma es la de siempre, con el mismo "!TipoDeError".
class FirmaRapida
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TL, TW, TE;
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
        TF = asm.GetType("Rocola.Form1", true); TL = asm.GetType("Rocola.ListadoCarpeta", false); TW = asm.GetType("Rocola.ListadoWin32", false); TE = asm.GetType("Rocola.EntradaListado", false);
        Type tAnt = null;
        if (args.Length > 1) { try { tAnt = Assembly.Load(File.ReadAllBytes(args[1])).GetType("Rocola.Form1", true); } catch (Exception ex) { Console.WriteLine("  (sin el ejecutable anterior: " + ex.GetType().Name + ")"); } }
        NuevoHash = (HashDel)Delegate.CreateDelegate(typeof(HashDel), TF.GetMethod("RootHash", T));
        NuevaFirma = DeEnsamblado(TF);
        Check(TL != null && TW != null && TE != null, "existen el recorrido rapido (ListadoCarpeta, ListadoWin32, EntradaListado)");

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
        MethodInfo orden2 = TF.GetMethod("OrdenaRutasDeCarpeta", T, null, new Type[] { typeof(string[]), typeof(int[]) }, null);
        Check(orden != null && orden2 != null, "existe OrdenaRutasDeCarpeta (con y sin arreglo acompanante)");
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
                string[] a = lista.ToArray(), b = lista.ToArray(), c = lista.ToArray();
                Array.Sort(a, StringComparer.OrdinalIgnoreCase);
                orden.Invoke(null, new object[] { b });
                int[] idx = new int[c.Length]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
                orden2.Invoke(null, new object[] { c, idx });
                bool igual = a.Length == b.Length, igualIdx = a.Length == c.Length;
                for (int i = 0; igual && i < a.Length; i++) igual = a[i] == b[i];
                for (int i = 0; igualIdx && i < a.Length; i++) igualIdx = a[i] == c[i] && lista[idx[i]] == c[i];
                bool empate = false; for (int i = 1; i < a.Length; i++) if (string.Compare(a[i - 1], a[i], StringComparison.OrdinalIgnoreCase) == 0 && a[i - 1] != a[i]) empate = true;
                if (empate) conEmpates++;
                if (igual && igualIdx) ordenOk++; else { ordenMal++; if (ordenMal < 3) Console.WriteLine("    distinto: " + pre + " ronda " + ronda); }
            }
        }
        Check(ordenMal == 0, "OrdenaRutasDeCarpeta deja el mismo orden que Array.Sort(OrdinalIgnoreCase) en " + ordenOk + " listas de rutas de Windows y de Linux (" + conEmpates + " con empates de mayusculas, otras sin prefijo comun o con una ruta vacia), solas o con el arreglo acompanante");

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

            // ---------- 4) el recorrido rapido con un origen inventado ----------
            FieldInfo fuente = TF.GetField("fuenteListado", T);
            Check(fuente != null, "la fuente del listado se puede cambiar (fuenteListado)");
            Check(fuente.GetValue(null) == null, "en este sistema (Mono/Linux, CLR 4) no hay recorrido de Windows: el origen es null y todo sigue como siempre");
            Check((int)TW.GetMethod("TamanoEstructura", T).Invoke(null, null) == 592, "WIN32_FIND_DATA mide 592 bytes con texto Unicode (la estructura del recorrido de Windows)");
            Check(!(bool)TW.GetMethod("Disponible", T).Invoke(null, null), "ListadoWin32.Disponible es false fuera de Windows con CLR 2");
            Type tFuente = asm.GetType("Rocola.FuenteListado", true);
            // origen inventado a partir del disco de verdad, en el orden en que lo dan GetFiles y GetDirectories
            int llamadas = 0; Func<string, object> deDisco = null;
            deDisco = delegate(string dir)
            {
                llamadas++;
                List<object> entradas = new List<object>();
                foreach (string f in Directory.GetFiles(dir)) entradas.Add(Entrada(f, 0x80, new FileInfo(f).LastWriteTimeUtc.ToFileTimeUtc(), new FileInfo(f).Length));
                foreach (string s in Directory.GetDirectories(dir))
                {
                    uint at = 0x10; try { if ((new DirectoryInfo(s).Attributes & FileAttributes.ReparsePoint) != 0) at |= 0x400; } catch { }
                    entradas.Add(Entrada(s, at, 0, 0));
                }
                // "." y ".." y el mezclado: los directorios no tienen por que ir despues
                entradas.Insert(0, EntradaNombre(".", 0x10)); entradas.Insert(1, EntradaNombre("..", 0x10));
                return Construye(dir, entradas);
            };
            FuenteHolder.Func = deDisco;
            Delegate d1 = CreaFuente(tFuente);
            fuente.SetValue(null, d1);
            string v2 = Firma(NuevaFirma, raiz, false), v1 = Firma(NuevaFirma, raiz, true);
            Check(llamadas > 5, "con un origen inventado se pidio la lista de " + llamadas + " carpetas (camino de CLR 2)");
            Check(v2 == r2, "firma con el recorrido rapido (origen inventado): la misma que la de antes, letra por letra: " + v2);
            Check(v1 == r1, "y con .NET 4 el origen no se pide y la firma sigue siendo la de siempre");

            // memoria del recorrido: lo mismo que antes (archivos en el orden del sistema, carpetas en el orden del sistema)
            Type tMemo = TF.GetNestedType("ScanDirectoryMemo", T);
            object m1 = Activator.CreateInstance(tMemo, true), m2 = Activator.CreateInstance(tMemo, true);
            fuente.SetValue(null, null);
            Firma2(NuevaFirma, raiz, false, m1);
            fuente.SetValue(null, d1);
            Firma2(NuevaFirma, raiz, false, m2);
            bool memoIgual = true; int sitios = 0;
            foreach (string d in carpetas)
            {
                object[] p1 = new object[] { d, null, null }, p2 = new object[] { d, null, null };
                bool t1 = (bool)tMemo.GetMethod("Take", T).Invoke(m1, p1), t2 = (bool)tMemo.GetMethod("Take", T).Invoke(m2, p2);
                if (t1 != t2) { memoIgual = false; continue; }
                if (!t1) continue; sitios++;
                memoIgual &= Igual((string[])p1[1], (string[])p2[1]) && Igual((string[])p1[2], (string[])p2[2]);
            }
            Check(memoIgual && sitios > 5, "la memoria del recorrido (ScanDirectoryMemo) queda igual: mismos archivos y carpetas, en el mismo orden del sistema, en " + sitios + " carpetas");

            // origen que devuelve null / que falla: la firma es la de siempre
            FuenteHolder.Func = delegate(string dir) { return null; };
            Check(Firma(NuevaFirma, raiz, false) == r2, "si el origen no puede (devuelve null), se hace lo de siempre y la firma es la misma");
            FuenteHolder.Func = delegate(string dir) { throw new InvalidOperationException("origen roto"); };
            Check(Firma(NuevaFirma, raiz, false) == r2, "si el origen lanza una excepcion, tampoco cambia nada");
            // una carpeta que no existe: el mismo !TipoDeError con el origen que dice que no puede
            string nada = Path.Combine(tmp, "no_existe");
            FuenteHolder.Func = delegate(string dir) { return null; };
            string e1 = Firma(RefFirmaDel(), nada, false), e2 = Firma(NuevaFirma, nada, false);
            Check(e1 == e2, "carpeta que no existe: la firma con el error es la misma (" + e2 + ")");
            // Y un origen que SI contesta para algo que no existe no cambia el error (devuelve null: nadie inventa archivos)
            fuente.SetValue(null, null);

            // ---------- las entradas ----------
            Check(Prueba_Construye(), "ListadoCarpeta.Construye: ver las comprobaciones de arriba");

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
        finally { try { FuenteHolder.Func = null; Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? ("  OK firmarapida: " + oks + " comprobaciones (Mono/Linux; el recorrido de Windows no se ejecuta aqui)") : ("  FALLOS: " + fallos));
        return fallos == 0 ? 0 : 1;
    }

    // --- ayudas ---
    public static class FuenteHolder
    {
        public static Func<string, object> Func;
        public static object Llama(string dir) { return Func == null ? null : Func(dir); }
    }
    // Un FuenteListado (tipo interno de la rocola) que llama a FuenteHolder.Func.
    static Delegate CreaFuente(Type tFuente)
    {
        System.Linq.Expressions.ParameterExpression p = System.Linq.Expressions.Expression.Parameter(typeof(string), "dir");
        System.Linq.Expressions.Expression llama = System.Linq.Expressions.Expression.Call(typeof(FuenteHolder).GetMethod("Llama"), p);
        return System.Linq.Expressions.Expression.Lambda(tFuente, System.Linq.Expressions.Expression.Convert(llama, TL), p).Compile();
    }
    static FirmaDel RefFirmaDel() { return RefFirma; }
    static bool Igual(string[] a, string[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static string Firma2(FirmaDel carpeta, string root, bool meta, object memo)
    {
        ulong h = 1469598103934665603UL; long filesN = 0, dirsN = 0;
        Stack<string> pendientes = new Stack<string>(); pendientes.Push(Path.GetFullPath(root));
        while (pendientes.Count > 0)
        {
            string dir = pendientes.Pop(); dirsN++;
            try { carpeta(dir, memo, meta, pendientes, ref h, ref filesN); }
            catch (Exception ex) { RefHash(ref h, "!" + ex.GetType().Name); }
        }
        return dirsN + ":" + filesN + ":" + h.ToString("x16");
    }
    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] static extern int symlink(string destino, string enlace);
    static bool Simbolico(string destino, string enlace) { return symlink(destino, enlace) == 0; }

    static object EntradaNombre(string nombre, uint atributos)
    {
        object e = Activator.CreateInstance(TE);
        TE.GetField("Nombre").SetValue(e, nombre); TE.GetField("Atributos").SetValue(e, atributos);
        return e;
    }
    static object Entrada(string rutaOCarpeta, uint atributos, long fecha, long largo)
    {
        object e = EntradaNombre(Path.GetFileName(rutaOCarpeta), atributos);
        TE.GetField("FechaEscritura").SetValue(e, fecha); TE.GetField("Largo").SetValue(e, largo);
        return e;
    }
    static object Construye(string dir, List<object> entradas)
    {
        // IEnumerable<EntradaListado> con la lista de objetos ya armados
        Type ti = typeof(List<>).MakeGenericType(TE);
        object lista = Activator.CreateInstance(ti);
        foreach (object e in entradas) ti.GetMethod("Add").Invoke(lista, new object[] { e });
        return TL.GetMethod("Construye", T).Invoke(null, new object[] { dir, lista });
    }
    static T Campo<T>(object o, string n) { return (T)TL.GetField(n).GetValue(o); }

    // Lo que hace Construye con las entradas, sin tocar el disco.
    static bool Prueba_Construye()
    {
        bool todo = true;
        // 1) separa archivos y carpetas en el orden en que llegan, ignora . y .., y calcula tamano, fecha y atajo
        char s = Path.DirectorySeparatorChar;
        string dir = s == '\\' ? "C:\\Musica\\Disco" : "/musica/Disco";
        long ft = 133500000000000000L;   // un FILETIME cualquiera
        List<object> es = new List<object>();
        es.Add(EntradaNombre(".", 0x10)); es.Add(EntradaNombre("..", 0x10));
        es.Add(Entrada("zz.mp3", 0x20, ft + 5, 5000000000L));
        es.Add(Entrada("Sub B", 0x10, 0, 0));
        es.Add(Entrada("aa.mp3", 0x2 | 0x20, ft, 12));
        es.Add(Entrada("Enlace", 0x10 | 0x400, 0, 0));
        es.Add(Entrada("mm.mp3", 0x80, 0, 0));
        object l = Construye(dir, es);
        string[] arch = Campo<string[]>(l, "Archivos"), car = Campo<string[]>(l, "Carpetas"); long[] lar = Campo<long[]>(l, "Largos"), fec = Campo<long[]>(l, "Fechas"); bool[] ata = Campo<bool[]>(l, "Atajos");
        todo &= Check2(arch.Length == 3 && arch[0] == dir + s + "zz.mp3" && arch[1] == dir + s + "aa.mp3" && arch[2] == dir + s + "mm.mp3", "los archivos salen en el orden en que llegan, con la ruta completa (sin . ni ..)");
        todo &= Check2(car.Length == 2 && car[0] == dir + s + "Sub B" && car[1] == dir + s + "Enlace" && !ata[0] && ata[1], "las carpetas salen en su orden y solo la de punto de reanalisis es un atajo");
        todo &= Check2(lar[0] == 5000000000L && lar[1] == 12 && lar[2] == 0, "el tamano es el de antes, tambien por encima de 4 GB");
        todo &= Check2(fec[0] == DateTime.FromFileTimeUtc(ft + 5).Ticks && fec[1] == DateTime.FromFileTimeUtc(ft).Ticks && fec[2] == DateTime.FromFileTimeUtc(0).Ticks, "la fecha es DateTime.FromFileTimeUtc(...).Ticks, lo mismo que FileInfo.LastWriteTimeUtc.Ticks (incluida la fecha cero)");
        // 2) una carpeta que acaba en separador (la raiz de una unidad) no lleva dos
        object raiz = Construye(s == '\\' ? "C:\\" : "/", new List<object>(new object[] { Entrada("a.mp3", 0x20, ft, 1) }));
        todo &= Check2(Campo<string[]>(raiz, "Archivos")[0] == (s == '\\' ? "C:\\a.mp3" : "/a.mp3"), "en la raiz de una unidad la ruta no lleva dos separadores");
        // 3) lo que no es el caso limpio devuelve null (que lo haga lo de siempre)
        string[] raros = { "fin.", "fin ", "CON", "con.mp3", "Nul.txt", "aux", "PRN.x", "COM1", "lpt9.mp3", "com1 .mp3", "a:b.mp3", "a*b", "a?b", "a\"b", "a<b", "a>b", "a|b", "a\\b", "", "tab\tx" };
        int nulos = 0;
        foreach (string r in raros) if (Construye(dir, new List<object>(new object[] { Entrada(r, 0x20, ft, 1) })) == null) nulos++;
        todo &= Check2(nulos == raros.Length, "nombres que GetFullPath cambiaria o que Windows toma por aparatos (punto o espacio al final, CON, NUL, AUX, PRN, COM1, LPT9, caracteres prohibidos) no son el caso limpio: " + nulos + " de " + raros.Length + " dan null");
        string[] buenos = { "con tinta.mp3", "Console.mp3", "Contrabando.mp3", "auxilio.mp3", "com.mp3", "com10.mp3", "comp1.mp3", "nulo.mp3", "prn1.mp3", "lpt.mp3", "a.b.c.mp3", "fin.mp3", ".oculto", "\u00c1ngel.mp3", "a b.mp3", "a-b_c (1).mp3" };
        int bien = 0;
        foreach (string r in buenos) if (Construye(dir, new List<object>(new object[] { Entrada(r, 0x20, ft, 1) })) != null) bien++;
        todo &= Check2(bien == buenos.Length, "y los nombres normales que se le parecen (Console, Contrabando, auxilio, com10, comp1...) si pasan: " + bien + " de " + buenos.Length);
        // 4) rutas largas: a partir de 240 letras entre carpeta y nombre se deja a lo de siempre
        string larga = dir + s + new string('x', 200);
        todo &= Check2(Construye(larga, new List<object>(new object[] { Entrada("a" + new string('b', 60) + ".mp3", 0x20, ft, 1) })) == null, "carpeta + nombre de 240 letras o mas: null (GetFullPath lanza PathTooLongException a partir de 260 y su tipo forma parte de la firma)");
        todo &= Check2(Construye(dir + s + new string('x', 150), new List<object>(new object[] { Entrada("a.mp3", 0x20, ft, 1) })) != null, "con una ruta de 160 letras si");
        // 5) una fecha imposible no se inventa: lanza (la firma la atrapa y hace lo de siempre)
        bool lanzo = false;
        try { Construye(dir, new List<object>(new object[] { Entrada("a.mp3", 0x20, long.MaxValue, 1) })); } catch (TargetInvocationException) { lanzo = true; } catch (Exception) { lanzo = true; }
        todo &= Check2(lanzo, "una fecha imposible lanza y no se inventa nada (FirmaCarpeta atrapa y vuelve a lo de siempre)");
        // 6) rutas normales
        MethodInfo rn = TL.GetMethod("RutaNormal", T);
        string[] ok = { "C:\\Musica", "c:\\x\\y", "D:\\", "\\\\servidor\\recurso\\carpeta" };
        string[] no = { "", "C:", "C:Musica", "\\\\?\\C:\\x", "\\\\.\\pipe", "\\\\\\x", "/tmp/x", "1:\\x", "relativa\\x", "C:\\" + new string('x', 250) };
        bool rnOk = true;
        foreach (string x in ok) rnOk &= (bool)rn.Invoke(null, new object[] { x });
        foreach (string x in no) rnOk &= !(bool)rn.Invoke(null, new object[] { x });
        todo &= Check2(rnOk, "RutaNormal: unidades y recursos de red si; sin unidad, con prefijo \\\\?\\ o \\\\.\\, relativas y de 240 letras o mas no");
        return todo;
    }
    static bool Check2(bool ok, string q) { Check(ok, q); return ok; }
}
