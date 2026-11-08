// NATIVEAOT (Rocola OS va compilado asi): LOS AVISOS DEL COMPILADOR POR LO QUE XP HACE CON REFLEXION, UNO POR UNO (revisados el 7-11-2026).
// Con NativeAOT no hay compilador en marcha: lo que el codigo pide por su nombre en tiempo de ejecucion (reflexion) solo existe si el
// compilador sabe que hace falta, y lo que necesita codigo nuevo no se puede crear. Cada aviso se comprobo corriendo el mismo camino con el
// programa de JIT y con el de NativeAOT (rocola-xp --prueba-aot, PruebaAot.cs, y la rocola entera fuera de pantalla) y se apaga aqui solo
// cuando los dos hacen lo mismo, diciendo por que. Codigo/ queda como XP.
//
//  IL3050 Marshal.SizeOf(typeof(...)) y Marshal.OffsetOf (CancionEnMemoria, DiagnosticoCompleto, Form1, MedidasBitacora, RecorridoRapido,
//    SalidaPropia): lo avisa el analizador al compilar; el compilador de NativeAOT no, porque con el tipo escrito (typeof) genera los datos de
//    la estructura. Comprobado: mismos tamanos y desplazamientos (64, 424, 592 y la WAVEHDR) y la misma memoria en las dos.
//  IL2075 FormatoAudio.Mascara (AudioControles): busca una propiedad «Mascara» en el formato; ningun formato de NAudio ni de la rocola la
//    tiene, asi que en las dos da null y sigue con la mascara de siempre (el campo dwChannelMask de NAudio lo busca con typeof y su nombre,
//    y eso el compilador lo conserva: la mascara 5.1 sale igual).
//  IL2026 Assembly.Load(byte[]) (FlotanteNLayer: NLayerIncluido.Resuelve): en XP, NLayer.dll va dentro de Rocola.exe y se carga a mano
//    si .NET no la encuentra. Aqui NLayer es parte del programa (NuGet) y .NET siempre la encuentra: el evento no llega nunca, y ademas el
//    recurso no esta (sin recurso, devuelve null antes de cargar nada).
//  IL2072 Activator.CreateInstance (Form1.MideDuracionVideo): arreglado en LlamadasWindows.TipoDeProgId (su constructor queda marcado).
//  IL2075 InvokeMember (Form1.Get, Set y Call de Form1.Video.cs): XP le habla asi al reproductor de video (ReproductorWmp, AjustesWmp,
//    ControlesWmp, MedioWmp de VideoMpv.cs), que los conserva enteros con DynamicDependency en ReproductorWmp.Engancha (la llama el
//    anfitrion al arrancar). Comprobado propiedad por propiedad y con el video sonando (estado, tiempo y fin). Si se cambian esas clases,
//    sus DynamicDependency van con ellas.
//  IL2070 / IL2075 LectorMp3Ligero.Campo y PosicionEnTabla (Mp3Ligero): el lector de MP3 sin basura de XP busca piezas privadas de NAudio
//    1.9. Las del lector ya estan, con los nombres de 1.9 (Mp3Xp.cs es el Mp3FileReader de 1.9 traducido), pero Mp3Frame es el de NAudio 3
//    y su IsValidHeader recibe ahora ReadOnlySpan<byte> (no byte[]), que no se puede pasar por reflexion: no lo encuentra ni con JIT ni con
//    NativeAOT, Posible = false en los dos y XP abre su lector de siempre, como XP manda («si NAudio no fuera la 1.9 [...] no se usa»). El
//    mismo sonido (el lector de siempre es el de 1.9), con mas basura para el recolector. Para encenderlo hay que tocar Mp3Ligero.cs.
using System.Diagnostics.CodeAnalysis;

[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.Form1.MemoriaLibre", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.Form1.CacheCeldasMb", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.Form1.ProbeAudioTier", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.Form1.TopeDePortadas", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.Form1.ContextoDeParada", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.DiagEquipo.Memoria(System.Int64@,System.Int64@)", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.DiagEquipo.Tarjeta", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "M:Rocola.ListadoWin32.TamanoEstructura", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("AOT", "IL3050", Scope = "member", Target = "F:Rocola.WaveOutPropio.TamCabecera", Justification = "Marshal.SizeOf(typeof): NativeAOT genera los datos de la estructura (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Scope = "member", Target = "M:Rocola.FormatoAudio.Mascara(NAudio.Wave.WaveFormat)", Justification = "Ningun formato tiene la propiedad Mascara: null con JIT y con NativeAOT (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2026", Scope = "member", Target = "M:Rocola.NLayerIncluido.Resuelve(System.Object,System.ResolveEventArgs)", Justification = "NLayer va compilada dentro: el evento no llega nunca (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Scope = "member", Target = "M:Rocola.Form1.Get(System.Object,System.String)", Justification = "El reproductor de video se conserva con DynamicDependency (VideoMpv.cs, Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Scope = "member", Target = "M:Rocola.Form1.Set(System.Object,System.String,System.Object)", Justification = "El reproductor de video se conserva con DynamicDependency (VideoMpv.cs, Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Scope = "member", Target = "M:Rocola.Form1.Call(System.Object,System.String,System.Object[])", Justification = "El reproductor de video se conserva con DynamicDependency (VideoMpv.cs, Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2070", Scope = "member", Target = "M:Rocola.LectorMp3Ligero.Campo(System.Type,System.String,System.Type)", Justification = "Piezas de NAudio 1.9: no estan con NAudio 3 ni con JIT ni con NativeAOT (Aot.cs)")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Scope = "member", Target = "M:Rocola.LectorMp3Ligero.PosicionEnTabla(System.Int32)", Justification = "Piezas de NAudio 1.9: no estan con NAudio 3 ni con JIT ni con NativeAOT (Aot.cs)")]
