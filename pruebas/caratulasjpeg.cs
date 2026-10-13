using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

// CARATULAS EN JPEG (interruptor de Ctrl+F3 del 11-10-2026, apagado de fabrica).
// Comprueba, sin abrir Direct3D: que apagado todo sigue en PNG sin perdida (y un JPEG suelto se ignora); que
// encendido una portada opaca se guarda en JPEG, opaca de verdad hasta el borde, y una transparente (PNG con alfa o
// GIF con color transparente) sigue en PNG; que el calentador convierte las que solo estaban en PNG y la pantalla
// sigue leyendo el PNG mientras tanto; que al apagarlo se vuelve al PNG; y que la opcion se guarda y vuelve de
// fabrica apagada. Con el perfil por CPU (congelado) el interruptor no cambia nada. No mide FPS ni discos reales.
class CaratulasJpeg
{
    const BindingFlags B = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form; static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static object Run(string n, params object[] a)
    {
        foreach (MethodInfo m in form.GetMethods(B))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + n);
    }
    static void Jpeg(bool on) { form.GetField("caratulasJpeg", B).SetValue(null, on); }

    static void Opaca(string ruta, int semilla)
    {
        Random r = new Random(semilla);
        using (Bitmap b = new Bitmap(500, 500, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, 500, 500), Color.FromArgb(30, 60, 140), Color.FromArgb(230, 120, 40), 45f))
                    g.FillRectangle(lg, 0, 0, 500, 500);
                for (int i = 0; i < 30; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(r.Next(256), r.Next(256), r.Next(256))))
                    g.FillEllipse(sb, r.Next(500), r.Next(500), 20 + r.Next(100), 20 + r.Next(100));
            }
            b.Save(ruta, ImageFormat.Jpeg);
        }
    }
    static void ConAlfa(string ruta)
    {
        using (Bitmap b = new Bitmap(300, 300, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.FillEllipse(Brushes.Gold, 40, 40, 220, 220); }
            b.Save(ruta, ImageFormat.Png);
        }
    }
    static void GifTransparente(string ruta)
    {
        using (Bitmap b = new Bitmap(200, 200, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.FillRectangle(Brushes.Red, 50, 50, 100, 100); }
            b.Save(ruta, ImageFormat.Gif);
        }
    }
    static Bitmap Decode(string portada)
    {
        Image im = (Image)Run("DecodeCover", portada);
        return im as Bitmap;
    }
    static bool TodaOpaca(Bitmap b)
    {
        for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, y).A != 255) return false;
        return true;
    }

    static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rocola_caratulasjpeg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        bool gpu = asm.GetType("Rocola.GpuPanel", false) != null;
        try
        {
            FieldInfo campo = form.GetField("caratulasJpeg", B);
            Check(campo != null && campo.IsStatic && !(bool)campo.GetValue(null), "el interruptor existe y viene apagado de fabrica");
            List<string> lee = Codigo.Textos(form.GetMethod("LoadConfigPrefs", B)), guarda = Codigo.Textos(form.GetMethod("TextoConfig", B));
            Check(lee.Contains("caratulas_jpeg=") && guarda.Contains("caratulas_jpeg="), "se lee y se guarda en la configuracion (caratulas_jpeg=)");

            // 1) APAGADO: PNG sin perdida, como siempre.
            string p1 = Path.Combine(dir, "opaca1.jpg"); Opaca(p1, 1);
            string id1 = (string)Run("ThumbCachePath", p1);
            Jpeg(false);
            using (Bitmap a = Decode(p1)) Check(a != null && File.Exists(id1) && !File.Exists((string)Run("MiniaturaJpeg", id1)), "apagado: la miniatura se guarda en PNG");
            File.Copy(id1, (string)Run("MiniaturaJpeg", id1));   // un JPEG suelto (de cuando estuvo encendido)
            Check((string)Run("MiniaturaGuardada", id1) == id1, "apagado: un JPEG que quedara se ignora y vale el PNG");
            Check(!(bool)Run("MiniaturaPorRehacer", id1), "apagado: con su PNG no hay nada que rehacer");
            File.Delete((string)Run("MiniaturaJpeg", id1));

            // 2) ENCENDIDO, portada opaca nueva.
            Jpeg(true);
            string p2 = Path.Combine(dir, "opaca2.jpg"); Opaca(p2, 2);
            string id2 = (string)Run("ThumbCachePath", p2), j2 = (string)Run("MiniaturaJpeg", id2);
            Bitmap primera = Decode(p2);
            if (gpu)
            {
                Check(primera != null && TodaOpaca(primera), "encendido: la portada opaca queda opaca hasta el borde (sin filo desvaido)");
                Check(File.Exists(j2) && !File.Exists(id2), "encendido: la opaca se guarda en JPEG");
                Check((string)Run("MiniaturaGuardada", id2) == j2, "encendido: se lee el JPEG");
                long png = new FileInfo(id1).Length, jpg = new FileInfo(j2).Length;
                Console.WriteLine("     PNG " + png / 1024 + " KB, JPEG " + jpg / 1024 + " KB (dos portadas parecidas)");
                Check(jpg * 2 < png, "encendido: el JPEG ocupa bastante menos que el PNG");
                using (Bitmap cache = Decode(p2))
                {
                    long suma = 0; int n = 0;
                    for (int y = 0; y < 256; y += 2) for (int x = 0; x < 256; x += 2)
                    {
                        Color c = primera.GetPixel(x, y), d = cache.GetPixel(x, y);
                        suma += Math.Abs(c.R - d.R) + Math.Abs(c.G - d.G) + Math.Abs(c.B - d.B); n += 3;
                    }
                    double media = (double)suma / n;
                    Console.WriteLine("     diferencia media con la primera lectura: " + media.ToString("0.00") + " niveles");
                    Check(cache.Width == 256 && TodaOpaca(cache) && media < 12, "encendido: la lectura del JPEG es la misma portada, opaca");
                }
            }
            else Check(primera != null && File.Exists(id2) && !File.Exists(j2), "sin tarjeta grafica: el interruptor no cambia nada (PNG)");
            if (primera != null) primera.Dispose();

            // 3) ENCENDIDO, portadas con transparencia: siguen en PNG, sin perdida.
            string p3 = Path.Combine(dir, "alfa.png"); ConAlfa(p3);
            string id3 = (string)Run("ThumbCachePath", p3);
            using (Bitmap a = Decode(p3))
                Check(a != null && a.GetPixel(2, 2).A == 0 && File.Exists(id3) && !File.Exists((string)Run("MiniaturaJpeg", id3)),
                      "encendido: una PNG con transparencia sigue en PNG y conserva su fondo transparente");
            string p4 = Path.Combine(dir, "gif.gif"); GifTransparente(p4);
            string id4 = (string)Run("ThumbCachePath", p4);
            // (El GIF que guarda Mono pierde su color transparente, asi que aqui no se mira el pixel: se mira la regla.
            // Toda imagen de paleta puede traer transparencia, y por eso nunca pasa a JPEG.)
            using (Bitmap a = Decode(p4))
                Check(a != null && File.Exists(id4) && !File.Exists((string)Run("MiniaturaJpeg", id4)),
                      "encendido: un GIF (de paleta, puede traer transparencia) se queda en PNG, sin ennegrecerse");

            // 4) ENCENDIDO con miniaturas viejas en PNG: la pantalla las sigue leyendo y el calentador las convierte.
            if (gpu)
            {
                Check(File.Exists(id1) && (string)Run("MiniaturaGuardada", id1) == id1, "encendido: la PNG ya hecha se sigue usando mientras tanto");
                Check((bool)Run("MiniaturaPorRehacer", id1), "encendido: la PNG ya hecha queda pendiente de pasar a JPEG");
                object[] pa = { p1, id1, null };
                using (Image im = (Image)Run("DecodeCoverForPublication", pa))
                    Check(im != null && pa[2] == null, "la pantalla lee la PNG (rapido), sin rehacer nada");
                object[] ca = { p1, id1, null };
                Image calentada = (Image)Run("DecodeCoverParaCalentar", ca);
                Bitmap porGuardar = ca[2] as Bitmap;
                Check(calentada != null && porGuardar != null, "el calentador la rehace desde la portada original");
                if (porGuardar != null) Run("SaveThumb", porGuardar, id1);
                Check(File.Exists((string)Run("MiniaturaJpeg", id1)) && !(bool)Run("MiniaturaPorRehacer", id1), "y queda en JPEG");
                if (calentada != null) calentada.Dispose(); if (porGuardar != null) porGuardar.Dispose();
                Check((bool)Run("MiniaturaPorRehacer", id3) || File.Exists(id3), "las transparentes pueden repasarse, pero nunca se pierden");
            }

            // 5) APAGADO otra vez: vuelve el PNG sin perdida.
            Jpeg(false);
            if (gpu) Check((string)Run("MiniaturaGuardada", id2) == null, "apagado: la que solo estaba en JPEG se rehara en PNG");
            using (Bitmap a = Decode(p2)) Check(a != null && File.Exists(id2), "apagado: al verla se guarda otra vez en PNG");
            Check((string)Run("MiniaturaGuardada", id1) == id1, "apagado: la que tenia PNG lo sigue usando");
        }
        finally { Jpeg(false); try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK caratulasjpeg: " + ok + " comprobaciones (" + (gpu ? "GPU" : "CPU") + "; sin HAL ni discos reales)"
                                   : "  FALLO caratulasjpeg: " + mal + " de " + (ok + mal));
        return mal;
    }
}
