using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// MP3 CON FRECUENCIA O CANALES MEZCLADOS (rocola_error.txt del 17-10-2026: «Got a frame at sample rate 32000, in an MP3 with sample rate 44100»).
// NAudio no soporta que un MP3 cambie de frecuencia ni de canales a mitad; la rocola lo reconstruye con los cuadros del formato dominante
// (BuildCleanMp3). Se comprueba con dos archivos de verdad (1 s estereo 44,1 kHz + otro trozo + 1 s estereo 44,1 kHz):
//  1) El original lo rechaza NAudio (el fallo del usuario).
//  2) La reconstruccion queda con solo el formato dominante y NAudio la abre: 2 s, no mas.
//  3) Con canales mezclados (un trozo mono) tambien: antes se quedaban los cuadros mono, que rompen el descompresor.
//  4) Un archivo sano se queda como esta (nada que reconstruir cambia su duracion), y uno sin cuadros da null.
//  5) Si la reconstruccion tampoco puede, se escribe por que (antes se tragaba) y el error original sigue su camino.
class DescompDummy : IMp3FrameDecompressor
{
    public WaveFormat OutputFormat { get { return new WaveFormat(44100, 16, 2); } }
    public int DecompressFrame(Mp3Frame f, byte[] b, int o) { return 0; }
    public void Reset() { }
    public void Dispose() { }
}
class Mp3Mezclado
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static string Rechaza(string ruta)
    {
        try { using (Mp3FileReader r = new Mp3FileReader(ruta, delegate(WaveFormat w) { return new DescompDummy(); })) { return null; } }
        catch (InvalidOperationException e) { return e.Message; }
    }
    static double Segundos(MemoryStream ms)
    {
        ms.Position = 0;
        using (Mp3FileReader r = new Mp3FileReader(ms, delegate(WaveFormat w) { return new DescompDummy(); })) return r.TotalTime.TotalSeconds;
    }
    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        MethodInfo limpia = TF.GetMethod("BuildCleanMp3", T);
        Check(limpia != null, "esta la reconstruccion del MP3 mezclado");
        string frec = "mezcla_frecuencia.mp3", can = "mezcla_canales.mp3";
        Check(File.Exists(frec) && File.Exists(can), "estan los dos archivos de prueba");
        string m1 = Rechaza(frec), m2 = Rechaza(can);
        Check(m1 != null && m1.Contains("sample rate"), "NAudio rechaza el de frecuencia mezclada (el fallo del usuario): " + m1);
        Check(m2 != null && m2.Contains("channel"), "y el de canales mezclados: " + m2);
        MemoryStream c1 = (MemoryStream)limpia.Invoke(null, new object[] { frec });
        Check(c1 != null && Rechaza2(c1) == null, "frecuencia: la reconstruccion queda y NAudio la abre");
        double s1 = c1 == null ? -1 : Segundos(c1);
        Check(s1 > 1.9 && s1 < 2.2, "frecuencia: queda lo del formato dominante, 2 s (" + s1.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s), sin el trozo de 32 kHz");
        MemoryStream c2 = (MemoryStream)limpia.Invoke(null, new object[] { can });
        double s2 = c2 == null ? -1 : Segundos(c2);
        Check(c2 != null && s2 > 1.9 && s2 < 2.2, "canales: queda el estereo dominante, 2 s, sin el trozo mono (" + s2.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s)");
        string vacio = Path.GetTempFileName();
        File.WriteAllBytes(vacio, new byte[3000]);
        Check(limpia.Invoke(null, new object[] { vacio }) == null, "un archivo sin ningun cuadro da null (no hay nada que reconstruir)");
        File.Delete(vacio);
        string cod = File.ReadAllText(Path.Combine("..", "Rocola", "Form1.Sonido.cs"));
        Check(cod.Contains("la reconstruccion tampoco pudo con") && cod.Contains("catch (Exception ex2) { porQue = ex2;"), "si la reconstruccion tampoco puede, se escribe por que en rocola_error.txt");
        Console.WriteLine("\n  " + oks + " comprobaciones bien, " + fallos + " fallos");
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : "  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
    static string Rechaza2(MemoryStream ms)
    {
        try { ms.Position = 0; using (Mp3FileReader r = new Mp3FileReader(ms, delegate(WaveFormat w) { return new DescompDummy(); })) { return null; } }
        catch (Exception e) { return e.Message; }
    }
}
