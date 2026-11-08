// INVENTARIO de lo que el codigo de XP (sistema/rocola/Xp/Codigo) usa de Windows, con el compilador (Roslyn).
// Uso: dotnet run -- RAIZ_XP CARPETA_SALIDA
// Compila Codigo + Plataforma + los enlazados como RocolaXp.csproj (ROCOLA_GPU) y clasifica cada nombre de Codigo que apunta a:
//   winforms: Rocola.Ventanas (imitacion de System.Windows.Forms), menos AxHost y Escritorio
//   gdi:      Rocola.Dibujo2D (imitacion de System.Drawing / Drawing2D / Imaging / Text y TextRenderer)
//   gdiprim:  System.Drawing de .NET 10 (Color, Point, Size, Rectangle...: System.Drawing.Primitives, no imitan nada) -> aparte
//   d3d9:     la clase D3D9 (sus tipos, constantes y delegados de XP en D3D9Star.cs, y D3D9.EnGl de Direct3DaGL.cs) y lo de D3D9Device
//             que esta en Plataforma
//   wmp:      AxHost, VideoHost, Get/Set/Call (reflexion), Type.InvokeMember, Marshal.ReleaseComObject, Activator.CreateInstance(Type),
//             LlamadasWindows.TipoDeProgId, ReproductorWmp...
//   otras:    LlamadasWindows.*, Escritorio.* (user32 traducido), las declaraciones y llamadas a funciones de Windows (stubs y extern),
//             Microsoft.Win32 (registro), WaveOut/AlsaWaveOut/MmException/MmResult (winmm)
//   info:     lo que no es de Windows pero conviene saber (UnicodeXp, Puente de NAudio 3, NAudio 1.9 de Plataforma, ComponentModel,
//             el D3D9Device de XP)
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

string raiz = Path.GetFullPath(args[0]);
string salida = Path.GetFullPath(args[1]);
Directory.CreateDirectory(salida);
var codigo = Directory.GetFiles(Path.Combine(raiz, "Codigo"), "*.cs").OrderBy(x => x, StringComparer.Ordinal).ToList();
var archivos = new List<string>(codigo);
archivos.AddRange(Directory.GetFiles(Path.Combine(raiz, "Plataforma"), "*.cs").OrderBy(x => x, StringComparer.Ordinal));
archivos.Add(Path.GetFullPath(Path.Combine(raiz, "../Consola/Pantalla/Gl.cs")));
archivos.Add(Path.GetFullPath(Path.Combine(raiz, "../Consola/Pantalla/Kms.cs")));
archivos.Add(Path.GetFullPath(Path.Combine(raiz, "../Motor/Botones.cs")));
var po = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: new[] { "ROCOLA_GPU", "DEBUG", "TRACE", "NET", "NET10_0", "NETCOREAPP",
    "NET10_0_OR_GREATER", "NET9_0_OR_GREATER", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER", "NET6_0_OR_GREATER", "NET5_0_OR_GREATER",
    "NETCOREAPP3_1_OR_GREATER", "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP1_0_OR_GREATER" });
var arboles = archivos.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), po, path: f)).ToList();
var refs = new List<MetadataReference>();
foreach (var d in Directory.GetFiles("/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.14/ref/net10.0", "*.dll")) refs.Add(MetadataReference.CreateFromFile(d));
string nu = Environment.GetEnvironmentVariable("HOME") + "/.nuget/packages/";
refs.Add(MetadataReference.CreateFromFile(nu + "skiasharp/3.116.1/ref/net8.0/SkiaSharp.dll"));
refs.Add(MetadataReference.CreateFromFile(Directory.GetFiles(nu + "naudio.core/3.3.0/lib", "NAudio.Core.dll", SearchOption.AllDirectories).OrderByDescending(x => x).First()));
refs.Add(MetadataReference.CreateFromFile(Directory.GetFiles(nu + "nlayer/3.0.0/lib", "NLayer.dll", SearchOption.AllDirectories).OrderByDescending(x => x).First()));
var comp = CSharpCompilation.Create("rocola-xp", arboles, refs, new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true,
    nullableContextOptions: NullableContextOptions.Disable));
var errores = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
Console.Error.WriteLine("archivos: " + archivos.Count + " (Codigo " + codigo.Count + "), errores de compilacion: " + errores.Count);
foreach (var e in errores.Take(20)) Console.Error.WriteLine("  " + e);

bool EnCarpeta(ISymbol s, string carpeta) => s != null && s.Locations.Any(l => l.IsInSource && l.SourceTree.FilePath.Contains("/" + carpeta + "/"));
// de Plataforma (o enlazado) y de ninguna parte de Codigo (Form1, GpuStarfield y D3D9Device son parciales con un trozo en Plataforma)
bool EnPlataforma(ISymbol s) => s != null && s.Locations.Any(l => l.IsInSource) && s.Locations.Where(l => l.IsInSource).All(l => !l.SourceTree.FilePath.Contains("/Codigo/"));
bool EnCodigo(ISymbol s) => EnCarpeta(s, "Codigo");
IEnumerable<INamedTypeSymbol> Cadena(ISymbol s)
{
    INamedTypeSymbol t = s as INamedTypeSymbol ?? s.ContainingType;
    while (t != null) { yield return t; t = t.ContainingType; }
}
INamedTypeSymbol Exterior(ISymbol s) => Cadena(s).LastOrDefault();
string Ns(ISymbol s) { var t = Exterior(s); var ns = t != null ? t.ContainingNamespace : s.ContainingNamespace; return ns == null ? "" : ns.ToDisplayString(); }

// Nombres de funciones de Windows (las [DllImport] de Rocola/*.cs de XP) -> su DLL. En Codigo/ quedan como extern o como metodo con el
// mismo nombre que contesta Linux ("stub").
var win32 = new Dictionary<string, string> {
    ["GetKeyState"] = "user32", ["GetAsyncKeyState"] = "user32", ["GetForegroundWindow"] = "user32", ["SetForegroundWindow"] = "user32",
    ["GetWindowThreadProcessId"] = "user32", ["AttachThreadInput"] = "user32", ["SetFocus"] = "user32", ["mouse_event"] = "user32",
    ["ShowWindow"] = "user32", ["GetDC"] = "user32", ["ReleaseDC"] = "user32", ["PeekMessage"] = "user32",
    ["MsgWaitForMultipleObjectsEx"] = "user32", ["EnumDisplayDevices"] = "user32", ["GetDeviceCaps"] = "gdi32",
    ["GetCurrentThreadId"] = "kernel32", ["GlobalMemoryStatusEx"] = "kernel32", ["PideEstadoMemoria"] = "kernel32", ["FlushFileBuffers"] = "kernel32",
    ["FindFirstFile"] = "kernel32", ["FindNextFile"] = "kernel32", ["FindClose"] = "kernel32", ["GetSystemTimes"] = "kernel32",
    ["OpenThread"] = "kernel32", ["GetThreadTimes"] = "kernel32", ["CloseHandle"] = "kernel32", ["CopyMemory"] = "kernel32",
    ["timeBeginPeriod"] = "winmm", ["timeEndPeriod"] = "winmm",
    ["RegOpenKeyEx"] = "advapi32", ["RegEnumKeyEx"] = "advapi32", ["RegQueryValueEx"] = "advapi32", ["RegCloseKey"] = "advapi32",
    ["DwmGetCompositionTimingInfo"] = "dwmapi",
};
string ExternDll(IMethodSymbol m)
{
    foreach (var a in m.GetAttributes())
        if (a.AttributeClass?.Name == "DllImportAttribute" && a.ConstructorArguments.Length > 0) return a.ConstructorArguments[0].Value as string;
    return null;
}
bool EsDllWindows(string dll) => dll != null && !dll.StartsWith("lib") && dll != "libc";

ISymbol BaseDe(ISymbol s, Func<ISymbol, bool> es)
{
    for (int i = 0; s != null && i < 30; i++)
    {
        if (es(s)) return s;
        if (s is IMethodSymbol m) s = m.OverriddenMethod;
        else if (s is IPropertySymbol p) s = p.OverriddenProperty;
        else if (s is IEventSymbol e) s = e.OverriddenEvent;
        else return null;
    }
    return null;
}
bool EsImitacion(ISymbol s) { string n = Ns(s); return n == "Rocola.Ventanas" || n == "Rocola.Dibujo2D"; }

(string cat, string sub) Clasifica(ISymbol s)
{
    if (s == null) return (null, null);
    s = s.OriginalDefinition;
    if (s is IMethodSymbol ma && ma.ReducedFrom != null) s = ma.ReducedFrom;
    if (s is IAliasSymbol al) s = al.Target;
    if (s is ILocalSymbol || s is IParameterSymbol || s is IRangeVariableSymbol || s is ILabelSymbol || s is IDiscardSymbol) return (null, null);
    if (s is INamespaceSymbol nsS || s is ITypeParameterSymbol) return (null, null);
    var cad = Cadena(s).ToList();
    string ns = Ns(s);
    string ext = Exterior(s)?.Name ?? "";
    // ---- WMP ----
    if (s is IMethodSymbol m0 && EnCodigo(s) && (m0.Name == "Get" || m0.Name == "Set" || m0.Name == "Call") && m0.ContainingType?.Name == "Form1"
        && m0.Parameters.Length >= 2 && m0.Parameters[0].Type.SpecialType == SpecialType.System_Object) return ("wmp", "Get/Set/Call (reflexion sobre el OCX del WMP)");
    if (cad.Any(t => t.Name == "VideoHost" && EnCodigo(t))) return ("wmp", "VideoHost (AxHost del WMP, CLSID 6bf52a52)");
    if (cad.Any(t => t.Name == "AxHost" && Ns(t) == "Rocola.Ventanas")) return ("wmp", "AxHost");
    if (ext == "LlamadasWindows") return s.Name == "TipoDeProgId" ? ("wmp", "LlamadasWindows.TipoDeProgId (ProgID WMPlayer.OCX)") : ("otras", "LlamadasWindows");
    if (EnPlataforma(s) && (ext == "ReproductorWmp" || ext == "AjustesWmp" || ext == "ControlesWmp" || ext == "MedioWmp" || ext == "Mpv")) return ("wmp", ext + " (VideoMpv.cs)");
    if (s is IMethodSymbol mi && mi.Name == "InvokeMember" && mi.ContainingType?.ToDisplayString() == "System.Type") return ("wmp", "Type.InvokeMember (COM por reflexion)");
    if (s is IMethodSymbol mr && (mr.Name == "ReleaseComObject" || mr.Name == "FinalReleaseComObject") && mr.ContainingType?.Name == "Marshal") return ("wmp", "Marshal.ReleaseComObject (COM)");
    if (s is IMethodSymbol mc && mc.Name == "CreateInstance" && mc.ContainingType?.ToDisplayString() == "System.Activator" && mc.Parameters.Length == 1
        && mc.Parameters[0].Type.ToDisplayString() == "System.Type") return ("wmp", "Activator.CreateInstance(Type) (objeto COM del WMP)");
    // ---- WinForms y GDI+ imitados ----
    if (ns == "Rocola.Ventanas")
    {
        if (ext == "Escritorio") return ("otras", "Escritorio (cola de mensajes de user32, traducida)");
        return ("winforms", ext);
    }
    if (ns == "Rocola.Dibujo2D") return ("gdi", ext);
    if (ns == "System.Drawing") return ("gdiprim", ext);
    // ---- Direct3D 9 ----
    if (cad.Any(t => t.Name == "D3D9" && Ns(t) == "Rocola"))
    {
        if (s.Name == "CopyMemory") return ("otras", "kernel32 CopyMemory (D3D9.CopyMemory)");
        if (cad.Any(t => t.Name == "EnGl")) return ("d3d9", "D3D9.EnGl (funcion de Direct3D 9 en OpenGL ES 2)");
        if (EnPlataforma(s)) return ("d3d9", "D3D9 (Plataforma: objetos y constantes de Direct3DaGL.cs)");
        return ("d3d9", "D3D9 (tipos, constantes y delegados de Direct3D 9 de XP, D3D9Star.cs)");
    }
    if (ext == "D3D9Device" && EnPlataforma(s) && !(s is INamedTypeSymbol)) return ("d3d9", "D3D9Device.GetDeviceCaps (Plataforma, D3D9Device.Gl.cs)");
    if (EnPlataforma(s) && (ext == "GlMas" || Ns(s) == "Rocola.Pantalla")) return ("d3d9", "OpenGL directo (" + ext + ")");
    // ---- otras llamadas de Windows ----
    if (s is IMethodSymbol mw && EnCodigo(s) && win32.ContainsKey(mw.Name) && ext != "D3D9Device")
    {
        string dll = ExternDll(mw);
        string como = mw.IsExtern ? (EsDllWindows(dll) ? "extern " + dll + " (sigue declarada de Windows)" : "extern " + dll) : "stub que contesta Linux";
        return ("otras", win32[mw.Name] + " " + mw.Name + " (" + como + ")");
    }
    if (s is IMethodSymbol mx && mx.IsExtern && EnCodigo(s))
    {
        string dll = ExternDll(mx);
        if (EsDllWindows(dll)) return ("otras", dll + " " + mx.Name + " (extern de Windows)");
        return ("info", "extern " + dll + " " + mx.Name);
    }
    if (ns.StartsWith("Microsoft.Win32")) return ("otras", "Microsoft.Win32 (registro de Windows)");
    if (s is IPropertySymbol pv && pv.Name == "OSVersion" && pv.ContainingType?.ToDisplayString() == "System.Environment") return ("otras", "Environment.OSVersion (version de Windows; en Linux la del nucleo)");
    if (s is IPropertySymbol pt && pt.Name == "Priority" && pt.ContainingType?.ToDisplayString() == "System.Threading.Thread") return ("otras", "Thread.Priority (SetThreadPriority de Windows; en Linux no hace nada: pendiente)");
    if (EnPlataforma(s) && (ext == "WaveOut" || ext == "AlsaWaveOut" || ext == "MmException" || ext == "MmResult")) return ("otras", "winmm: " + ext + " (SonidoAlsa.cs)");
    // ---- informativo ----
    if (EnPlataforma(s) && ext == "UnicodeXp") return ("info", "UnicodeXp.FormaD (Normalize FormD sin ICU)");
    if (EnPlataforma(s) && ext == "Puente") return ("info", "Puente de NAudio 3 (NAudioXp.cs)");
    if (EnPlataforma(s) && ext == "Mp3FileReader" && s.Name == "CreateAcmFrameDecompressor") return ("otras", "msacm32 (ACM): Mp3FileReader.CreateAcmFrameDecompressor (en Linux libmpg123, Mp3Xp.cs/NAudioXp.cs)");
    if (EnPlataforma(s) && ext == "AudioFileReader") return ("otras", "NAudio 1.9 AudioFileReader (ACM / Media Foundation de Windows; en Linux AudioXp.cs con mpg123 y ffmpeg)");
    if (EnPlataforma(s) && (ext == "Mp3FileReader" || ext == "FfmpegLector" || ext == "DescompresorMpg123" || ext == "DescompresorNLayer16"))
        return ("info", "NAudio 1.9 de XP en Plataforma: " + ext);
    if (EnPlataforma(s)) return ("info", "otro de Plataforma: " + ext);
    if (ns.StartsWith("System.ComponentModel")) return ("info", "System.ComponentModel: " + ext);
    if (ext == "D3D9Device" && EnCodigo(s)) return ("info", "D3D9Device de XP (D3D9Star.cs)");
    return (null, null);
}

string Nombre(ISymbol s)
{
    s = s.OriginalDefinition;
    if (s is IMethodSymbol ma && ma.ReducedFrom != null) s = ma.ReducedFrom;
    if (s is IMethodSymbol m && m.MethodKind == MethodKind.Constructor) return m.ContainingType.ToDisplayString().Replace("Rocola.Ventanas.", "").Replace("Rocola.Dibujo2D.", "").Replace("Rocola.", "").Replace("System.Drawing.", "") + "..ctor(" + string.Join(",", m.Parameters.Select(p => p.Type.Name)) + ")";
    if (s is IMethodSymbol m2 && (m2.MethodKind == MethodKind.EventAdd || m2.MethodKind == MethodKind.EventRemove)) s = m2.AssociatedSymbol;
    if (s is IMethodSymbol m3 && (m3.MethodKind == MethodKind.PropertyGet || m3.MethodKind == MethodKind.PropertySet)) s = m3.AssociatedSymbol;
    if (s is ITypeSymbol) return "[tipo] " + s.ToDisplayString().Replace("Rocola.Ventanas.", "").Replace("Rocola.Dibujo2D.", "").Replace("Rocola.", "").Replace("System.Drawing.", "");
    string ct = s.ContainingType?.ToDisplayString().Replace("Rocola.Ventanas.", "").Replace("Rocola.Dibujo2D.", "").Replace("Rocola.", "").Replace("System.Drawing.", "") ?? "";
    string extra = s is IMethodSymbol mm ? "(" + string.Join(",", mm.Parameters.Select(p => p.Type.Name)) + ")" : "";
    return ct + "." + s.Name + extra;
}

var filas = new List<(string archivo, int linea, string cat, string sub, string miembro, string como)>();
var herencias = new List<(string archivo, int linea, string clase, string base_, string cat)>();
var lineasArchivo = new Dictionary<string, int>();

foreach (var t in comp.SyntaxTrees.Where(t => t.FilePath.Contains("/Codigo/")))
{
    var m = comp.GetSemanticModel(t);
    string archivo = Path.GetFileName(t.FilePath);
    lineasArchivo[archivo] = t.GetText().Lines.Count;
    void Apunta(SyntaxNode n, ISymbol s, string como)
    {
        var (cat, sub) = Clasifica(s);
        if (cat == null) return;
        // el D3D9Device de XP: solo cuenta (como informacion) lo que lo usa desde fuera de su propia clase
        if (sub == "D3D9Device de XP (D3D9Star.cs)")
        {
            var td = n.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().LastOrDefault();
            var ts = td == null ? null : m.GetDeclaredSymbol(td) as INamedTypeSymbol;
            if (ts != null && ts.Name == "D3D9Device") return;
        }
        int linea = n.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        filas.Add((archivo, linea, cat, sub, Nombre(s), como));
    }
    foreach (var n in t.GetRoot().DescendantNodes())
    {
        // clases de XP que heredan de una clase imitada (WinForms / GDI+)
        if (n is ClassDeclarationSyntax cd)
        {
            var ct = m.GetDeclaredSymbol(cd);
            for (var b = ct?.BaseType; b != null; b = b.BaseType)
            {
                var (cat, sub) = Clasifica(b);
                if (cat != null && cat != "info") { herencias.Add((archivo, cd.GetLocation().GetLineSpan().StartLinePosition.Line + 1, ct.ToDisplayString().Replace("Rocola.", ""), b.ToDisplayString().Replace("Rocola.Ventanas.", "").Replace("Rocola.Dibujo2D.", "").Replace("Rocola.", ""), cat)); break; }
            }
            continue;
        }
        // declaraciones: override de un miembro imitado; y las funciones de Windows declaradas (extern o stub)
        if (n is MethodDeclarationSyntax || n is PropertyDeclarationSyntax || n is EventDeclarationSyntax || n is EventFieldDeclarationSyntax)
        {
            var d = n is EventFieldDeclarationSyntax ef ? m.GetDeclaredSymbol(ef.Declaration.Variables[0]) : m.GetDeclaredSymbol(n);
            if (d != null && d.IsOverride)
            {
                ISymbol sobre = d is IMethodSymbol mm ? mm.OverriddenMethod : d is IPropertySymbol pp ? pp.OverriddenProperty : (ISymbol)((IEventSymbol)d).OverriddenEvent;
                var b = BaseDe(sobre, x => Clasifica(x).cat is string c && c != "info");
                if (b != null) Apunta(n, b, "override");
            }
            if (d is IMethodSymbol dm && (win32.ContainsKey(dm.Name) && dm.ContainingType?.Name != "D3D9Device" && Clasifica(dm).cat == "otras" || (dm.IsExtern && EsDllWindows(ExternDll(dm)))))
                Apunta(n, dm, "declaracion");
            continue;
        }
        if (n is IdentifierNameSyntax || n is GenericNameSyntax || n is ObjectCreationExpressionSyntax || n is ImplicitObjectCreationExpressionSyntax
            || n is ElementAccessExpressionSyntax || n is ConstructorInitializerSyntax || n is PrimaryConstructorBaseTypeSyntax)
        {
            if (n is IdentifierNameSyntax idn && idn.IsVar) continue;
            // el nombre del tipo de un "new X(...)" va con el constructor (se cuenta una vez)
            if (n is SimpleNameSyntax sn && n.Parent is ObjectCreationExpressionSyntax oc0 && oc0.Type == n) continue;
            if (n is SimpleNameSyntax sn2 && n.Parent is QualifiedNameSyntax q && q.Right == n && q.Parent is ObjectCreationExpressionSyntax oc1 && oc1.Type == q) continue;
            var si = m.GetSymbolInfo(n);
            var s = si.Symbol ?? si.CandidateSymbols.FirstOrDefault();
            if ((n is ObjectCreationExpressionSyntax || n is ImplicitObjectCreationExpressionSyntax) && !(s is IMethodSymbol)) s = m.GetTypeInfo(n).Type;
            if (s == null) continue;
            // una llamada a Direct3D 9 por el delegado que XP guarda (present, drawUp, setRS, create...: campos o locales de un tipo D3D9.*Del)
            if (n is IdentifierNameSyntax && (s is IFieldSymbol || s is ILocalSymbol || s is IParameterSymbol || s is IPropertySymbol))
            {
                ITypeSymbol ty = s is IFieldSymbol fs ? fs.Type : s is ILocalSymbol ls ? ls.Type : s is IParameterSymbol ps ? ps.Type : ((IPropertySymbol)s).Type;
                if (ty is INamedTypeSymbol nt && nt.TypeKind == TypeKind.Delegate && Cadena(nt).Any(x => x.Name == "D3D9"))
                {
                    SyntaxNode llamado = n;
                    if (n.Parent is MemberAccessExpressionSyntax mz && mz.Name == n) llamado = mz;
                    if (llamado.Parent is InvocationExpressionSyntax inv && inv.Expression == llamado)
                    {
                        filas.Add((archivo, n.GetLocation().GetLineSpan().StartLinePosition.Line + 1, "d3d9", "llamada a Direct3D 9 por su delegado guardado (" + nt.Name + ")", "(delegado " + nt.Name + ") " + s.Name + "(...)", "llamada"));
                        continue;
                    }
                }
            }
            // el tipo que solo califica a un miembro (Color.FromArgb, Keys.Enter, D3D9.EnGl.Present, Control.ControlCollection): se cuenta el
            // miembro, no el tipo de delante
            if (s is INamedTypeSymbol && n is SimpleNameSyntax)
            {
                SyntaxNode completo = n;
                if (n.Parent is QualifiedNameSyntax qn0 && qn0.Right == n) completo = qn0;
                else if (n.Parent is MemberAccessExpressionSyntax ma0 && ma0.Name == n) completo = ma0;
                if ((completo.Parent is MemberAccessExpressionSyntax ma1 && ma1.Expression == completo) || (completo.Parent is QualifiedNameSyntax qn1 && qn1.Left == completo)) continue;
            }
            // un miembro de XP que sobreescribe uno imitado (OnPaint, Dispose, WndProc...) cuenta como el imitado
            if ((s is IMethodSymbol || s is IPropertySymbol || s is IEventSymbol) && s.IsOverride && Clasifica(s).cat == null)
            {
                var b = BaseDe(s, x => Clasifica(x).cat is string c && c != "info");
                if (b != null) s = b;
            }
            string como = n is ObjectCreationExpressionSyntax || n is ImplicitObjectCreationExpressionSyntax ? "new" : n is ConstructorInitializerSyntax ? "base()" : n is ElementAccessExpressionSyntax ? "indice" : "nombre";
            if (n is ElementAccessExpressionSyntax && !(s is IPropertySymbol)) continue;
            Apunta(n, s, como);
        }
    }
}

// ---- salida ----
var cats = new[] { "winforms", "gdi", "gdiprim", "d3d9", "wmp", "otras", "info" };
using (var w = new StreamWriter(Path.Combine(salida, "referencias.tsv")))
{
    w.WriteLine("archivo\tlinea\tcategoria\tsubcategoria\tmiembro\tcomo");
    foreach (var f in filas.OrderBy(x => x.archivo, StringComparer.Ordinal).ThenBy(x => x.linea))
        w.WriteLine(f.archivo + "\t" + f.linea + "\t" + f.cat + "\t" + f.sub + "\t" + f.miembro + "\t" + f.como);
}
string Rangos(IEnumerable<int> ls)
{
    var l = ls.Distinct().OrderBy(x => x).ToList();
    var sb = new StringBuilder();
    for (int i = 0; i < l.Count;)
    {
        int j = i; while (j + 1 < l.Count && l[j + 1] - l[j] <= 3) j++;
        if (sb.Length > 0) sb.Append(',');
        sb.Append(i == j ? l[i].ToString() : l[i] + "-" + l[j]);
        i = j + 1;
    }
    return sb.ToString();
}
using (var w = new StreamWriter(Path.Combine(salida, "por_archivo.tsv")))
{
    w.WriteLine("archivo\tlineas\t" + string.Join("\t", cats.Select(c => c + "_refs\t" + c + "_lineas")) + "\t" + string.Join("\t", cats.Select(c => c + "_rangos")));
    foreach (var a in codigo.Select(Path.GetFileName))
    {
        var fa = filas.Where(x => x.archivo == a).ToList();
        w.WriteLine(a + "\t" + lineasArchivo[a] + "\t" + string.Join("\t", cats.Select(c => fa.Count(x => x.cat == c) + "\t" + fa.Where(x => x.cat == c).Select(x => x.linea).Distinct().Count()))
            + "\t" + string.Join("\t", cats.Select(c => Rangos(fa.Where(x => x.cat == c).Select(x => x.linea)))));
    }
}
using (var w = new StreamWriter(Path.Combine(salida, "miembros.tsv")))
{
    w.WriteLine("categoria\tmiembro\tveces\tarchivos\tejemplos");
    foreach (var g in filas.GroupBy(x => (x.cat, x.miembro)).OrderByDescending(g => g.Count()))
    {
        var ej = g.Select(x => x.archivo + ":" + x.linea).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        w.WriteLine(g.Key.cat + "\t" + g.Key.miembro + "\t" + g.Count() + "\t" + g.Select(x => x.archivo).Distinct().Count() + "\t" + string.Join(" ", ej.Take(6)) + (ej.Count > 6 ? " ..." : ""));
    }
}
using (var w = new StreamWriter(Path.Combine(salida, "subcategorias.tsv")))
{
    w.WriteLine("categoria\tsubcategoria\tveces\tarchivos");
    foreach (var g in filas.GroupBy(x => (x.cat, x.sub)).OrderBy(g => g.Key.cat).ThenByDescending(g => g.Count()))
        w.WriteLine(g.Key.cat + "\t" + g.Key.sub + "\t" + g.Count() + "\t" + string.Join(" ", g.GroupBy(x => x.archivo).OrderByDescending(x => x.Count()).Select(x => x.Key + "(" + x.Count() + ")")));
}
using (var w = new StreamWriter(Path.Combine(salida, "top_por_archivo.tsv")))
{
    w.WriteLine("archivo\tcategoria\tmiembros (veces)");
    foreach (var a in codigo.Select(Path.GetFileName))
        foreach (var c in cats)
        {
            var g = filas.Where(x => x.archivo == a && x.cat == c).GroupBy(x => x.miembro).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
            if (g.Count == 0) continue;
            w.WriteLine(a + "\t" + c + "\t" + string.Join("; ", g.Take(12).Select(x => x.Key + " x" + x.Count())) + (g.Count > 12 ? "; ... (" + g.Count + " distintos)" : ""));
        }
}
using (var w = new StreamWriter(Path.Combine(salida, "herencias.tsv")))
{
    w.WriteLine("archivo\tlinea\tclase\tbase\tcategoria");
    foreach (var h in herencias.OrderBy(x => x.archivo, StringComparer.Ordinal).ThenBy(x => x.linea)) w.WriteLine(h.archivo + "\t" + h.linea + "\t" + h.clase + "\t" + h.base_ + "\t" + h.cat);
}
Console.Error.WriteLine("referencias: " + filas.Count + "  " + string.Join("  ", cats.Select(c => c + "=" + filas.Count(x => x.cat == c))));
