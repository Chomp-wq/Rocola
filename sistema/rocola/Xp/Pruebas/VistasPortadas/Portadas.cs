// ARNES DEL GRUPO «PORTADAS» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que hacen con los mapas Form1.Portadas.cs, Form1.Biblioteca.cs,
// CaratulasAdaptativas.cs y DibujoLigeroGpu.cs, con datos fijos: la huella de cada mapa, de cada archivo que se guarda y lo que devuelve cada
// llamada. Se compila con el programa de una carpeta Xp (VistasPortadas.csproj): la salida de la referencia congelada y la de la copia de
// trabajo tienen que ser iguales linea por linea. Las funciones se llaman por su nombre (reflexion), con el tipo que pida cada version: en la
// referencia, un Bitmap de la capa; en la traduccion, un SKBitmap (y, donde hay puente, tambien el Bitmap de la capa por el puente).
//   - Las miniaturas (DecodeCoverWithIdentity, DecodeCoverForPublication, DecodeCoverParaCalentar): de portadas JPEG opacas (cuadrada, ancha,
//     alta), PNG con alfa, opacas y diminutas, GIF de paleta con y sin color transparente, BMP y basura; sin miniatura guardada, con ella
//     (PNG o JPEG), con una guardada de otro tamano, con «Caratulas en JPEG» encendido y apagado; lo que se publica, la copia por guardar y los
//     archivos que quedan en la carpeta (nombre y huella de sus bytes). SaveThumb, MiniaturaOpaca, HazOpaca y CopiaMiniaturaGpu.
//   - Las previas de 32x32: Small, ToBytes y FromBytes.
//   - Las teselas: TeselasVisibles.De (lado 2 con borde, lado 16 sin el) y TrabajoTeselas paso a paso, de mapas con alfa en sitios sueltos,
//     enteros, vacios, de 1x1 y de pantalla; en la referencia, con los formatos con que XP los hace (Format32bppArgb los hornos, PArgb la
//     interfaz); InteriorOpaco con las portadas en sus formatos (PArgb, Argb, 24 bits, paleta).
//   VistasPortadas.dll CARPETA   (CARPETA: donde escribe las portadas y las miniaturas; se borra al empezar)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace BancoPortadas
{
    internal static class Portadas
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static int n, excepciones, noNativos;
        // (revision) el mismo mapa en BGRA sin premultiplicar: LecturaNativa da falso y la traducida lee por la copia de Pixeles
        private static SKBitmap SinPremultiplicar(SKBitmap b)
        {
            SKBitmap u = new SKBitmap(new SKImageInfo(b.Width, b.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            using (SKPixmap p = b.PeekPixels()) p.ReadPixels(u.Info, u.GetPixels(), u.RowBytes, 0, 0);
            return u;
        }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }

        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap de la traduccion): tal cual (premultiplicados) y su formato de XP. (cache-portadas,
        // 12-11-2026) La cache de portadas y la de previas ya guardan SKBitmap, que no lleva el formato: el de la referencia (el PixelFormat del
        // Bitmap de la capa) se escribe como el dato nativo (FormatoMapa) y el de la traduccion es el que el codigo declara para esas cachés
        // (Form1.FormatoPortada): las dos salidas solo coinciden si el formato que XP les daba es el declarado. Las marcas (Flags) de lo que se
        // publica no las lee nadie en XP (solo las de la portada original, en DecodeCoverCore) y ya no se escriben.
        private static string Formato(object o)
        {
            if (o is Rocola.Dibujo2D.Image)
            {
                Rocola.Dibujo2D.PixelFormat f = ((Rocola.Dibujo2D.Image)o).PixelFormat;
                return f == PixelFormat.Format32bppPArgb ? "PArgb32" : f == PixelFormat.Format32bppArgb ? "Argb32" : f == PixelFormat.Format32bppRgb ? "Rgb32" : f == PixelFormat.Format24bppRgb ? "Rgb24" : "Otro(" + f + ")";
            }
            FieldInfo d = typeof(Form1).GetField("FormatoPortada", Todo);
            return d != null ? Convert.ToString(d.GetValue(null), ci) : "sk";
        }
        private static unsafe string Puntos(object o)
        {
            if (o == null) return "null";
            if (o is string) return (string)o;
            SKBitmap s = o is SKBitmap ? (SKBitmap)o : ((Rocola.Dibujo2D.Image)o).Sk;
            string fmt = o is SKBitmap ? "sk" : Formato(o);
            if (s == null || s.Handle == IntPtr.Zero) return "soltado " + fmt;
            byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            return s.Width + "x" + s.Height + " " + s.ColorType + "/" + s.AlphaType + " " + Huella(px) + (o is SKBitmap ? "" : " " + fmt);
        }
        // lo que publican las caches (la de portadas y la de previas): en la traduccion, un SKBitmap con el formato que el codigo declara para ellas
        private static string PuntosCache(object o) { return o is SKBitmap ? Puntos(o) + " " + Formato(o) : Puntos(o); }
        // lo mismo, sin el formato (lo que una traducida devuelve como SKBitmap y la referencia como Bitmap)
        private static string PuntosSolo(object o) { string s = Puntos(o); if (o is SKBitmap || o == null || o is string) return s; int i = s.LastIndexOf(' '); return i < 0 || s.StartsWith("soltado") ? s : s.Substring(0, i); }

        // un argumento del tipo que pida la funcion: el SKBitmap tal cual, o un Bitmap de la capa con ese formato (sin hacerse dueno)
        private static object Arg(Type t, SKBitmap sk, PixelFormat f)
        {
            if (sk == null) return null;
            if (t.IsAssignableFrom(typeof(SKBitmap))) return sk;
            Bitmap b = new Bitmap(sk, f); return b;
        }
        private static MethodInfo Metodo(Type t, string nombre, Func<ParameterInfo[], bool> vale)
        {
            foreach (MethodInfo m in t.GetMethods(Todo)) if (m.Name == nombre && vale(m.GetParameters())) return m;
            throw new Exception("no esta " + nombre);
        }
        private static object Llama(MethodInfo m, object[] a)
        {
            try { return m.Invoke(null, a); }
            catch (TargetInvocationException e) { excepciones++; return "EXC " + e.InnerException.GetType().Name; }
        }

        // ---- las portadas de prueba
        private static SKBitmap Dibuja(int w, int h, bool alfa, int semilla)
        {
            SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, alfa ? SKAlphaType.Unpremul : SKAlphaType.Opaque));
            Random r = new Random(semilla);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = 255;
                    if (alfa) { a = (x * 255) / Math.Max(1, w - 1); if ((x / 7 + y / 5) % 4 == 0) a = 0; if (x == w / 2) a = 255; }
                    byte rr = (byte)((x * 3 + y + r.Next(8)) & 255), gg = (byte)((y * 5 + semilla) & 255), bb = (byte)((x ^ y) & 255);
                    b.SetPixel(x, y, new SKColor(rr, gg, bb, (byte)a));
                }
            return b;
        }
        private static byte[] Codifica(SKBitmap b, SKEncodedImageFormat f, int q) { using (SKData d = b.Encode(f, q)) return d.ToArray(); }
        private static byte[] Bmp(int w, int h)
        {
            int paso = (w * 3 + 3) & ~3; byte[] d = new byte[54 + paso * h];
            d[0] = (byte)'B'; d[1] = (byte)'M'; BitConverter.GetBytes(d.Length).CopyTo(d, 2); BitConverter.GetBytes(54).CopyTo(d, 10);
            BitConverter.GetBytes(40).CopyTo(d, 14); BitConverter.GetBytes(w).CopyTo(d, 18); BitConverter.GetBytes(h).CopyTo(d, 22);
            d[26] = 1; d[28] = 24; BitConverter.GetBytes(paso * h).CopyTo(d, 34);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { int o = 54 + y * paso + x * 3; d[o] = (byte)(x * 9); d[o + 1] = (byte)(y * 11); d[o + 2] = (byte)((x + y) * 5); }
            return d;
        }
        private const string GifA = "R0lGODlhKAAeAIMAAAD/ABDvNSDfajDPn0C/1FCvCWCfPnCPc4B/qJBv3aBfErBPRwAAAAAAAAAAAAAAACH5BAEAAAMALAAAAAAoAB4AQAj/AAEIFBigYEEBCBEOWLiQgEOHBSJGNECR4oGLFw0eTCiAYcOHBCROrGgA40UEKFFy7OjRoceXMAecTIkggU2bIEOKJFnSJM2aNxMoGDp0gVGjAwlqXPkyp8gCPE0eYNqSQMyrMg/8DJqgKcinUX3S5Ep0qNOdJKVuDVpWwVGkSTUGoMrwrMSwGOXS/fgQbFqxKfVyxIrVImCVg6sShjkzJVevfQssfomSbFnIENFWVDuWbVm7IzcfBnqz7dsFSQEITohZ592/GFOvVlg18mvRseMuTVz3q2bDeXezrp35NvDGKGez7G079HGtNJVPhvn8594BLqcvRE7a5vXs2rlzOP1uVXvWtTdbRzQ/oLJnoup/98SI3mbb+Mbni39v1nd+zo7x59ZboEEF236llXVagXghaJ+CbwUEADs=";
        private const string GifB = "R0lGODdhFwAfAIMAAAD/ABDvNSDfajDPn0C/1FCvCWCfPnCPc4B/qJBv3aBfErBPR8A/fNAvseAf5vAPGywAAAAAFwAfAEAItwABDDCQgMEDAQUQLHAQgMABBQ0EEjQoQCFDhxAlFjyYcGHDhxopdrz4MOLAjRU9YjQ5kaPFjwpWhnSpsqREkS9lnsS5UGdLhDltDuRJMuNOmg6A1jT6c2TDpkGZokwolShMlgWLYrUqU6nWmV6fTo261WVVpFdDfj0aVmZatk51wiULtkBcoVBr4h27FCvftXn71iVbtu1eg2h9/g3wNu/auYLh3j1r+HDivYsV84zs+GrhyWUDAgA7";

        private static string Archivos(string dir)
        {
            StringBuilder sb = new StringBuilder();
            List<string> fs = new List<string>(Directory.GetFiles(dir)); fs.Sort(StringComparer.Ordinal);
            foreach (string f in fs) sb.Append(Path.GetFileName(f)).Append('=').Append(Huella(File.ReadAllBytes(f))).Append(' ');
            return sb.Length == 0 ? "-" : sb.ToString().TrimEnd();
        }
        private static void Limpia(string dir) { if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir); }

        // ---- mapas para las teselas y lo opaco: premultiplicados, punto a punto
        private static SKBitmap Mapa(int w, int h, Func<int, int, int> alfa)
        {
            SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int a = alfa(x, y); int c = (x * 7 + y * 3) & 255;
                b.SetPixel(x, y, new SKColor((byte)(c * a / 255), (byte)(a / 2), (byte)((255 - c) * a / 255), (byte)a));
            }
            return b;
        }

        private static int Main(string[] args)
        {
            string dir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "vistas-portadas");
            Limpia(dir);
            Type F = typeof(Form1);
            FieldInfo jpegCampo = F.GetField("caratulasJpeg", Todo);
            MethodInfo conIdentidad = Metodo(F, "DecodeCoverWithIdentity", p => p.Length == 2);
            MethodInfo paraPublicar = Metodo(F, "DecodeCoverForPublication", p => p.Length == 3);
            MethodInfo paraCalentar = Metodo(F, "DecodeCoverParaCalentar", p => p.Length == 3);
            MethodInfo opaca = Metodo(F, "MiniaturaOpaca", p => p.Length == 1);
            MethodInfo hazOpaca = Metodo(F, "HazOpaca", p => p.Length == 1);
            MethodInfo copiaGpu = Metodo(F, "CopiaMiniaturaGpu", p => p.Length == 1);
            Type store = typeof(Form1).Assembly.GetType("Rocola.GpuPreviewStore");
            MethodInfo small = Metodo(store, "Small", p => p.Length == 1);
            MethodInfo toBytes = Metodo(store, "ToBytes", p => p.Length == 1);
            MethodInfo fromBytes = Metodo(store, "FromBytes", p => p.Length == 1);

            // 1) las portadas
            List<KeyValuePair<string, byte[]>> portadas = new List<KeyValuePair<string, byte[]>>();
            portadas.Add(new KeyValuePair<string, byte[]>("jpeg-600", Codifica(Dibuja(600, 600, false, 1), SKEncodedImageFormat.Jpeg, 90)));
            portadas.Add(new KeyValuePair<string, byte[]>("jpeg-1600x1000", Codifica(Dibuja(1600, 1000, false, 2), SKEncodedImageFormat.Jpeg, 80)));
            portadas.Add(new KeyValuePair<string, byte[]>("jpeg-200x900", Codifica(Dibuja(200, 900, false, 3), SKEncodedImageFormat.Jpeg, 95)));
            portadas.Add(new KeyValuePair<string, byte[]>("jpeg-256", Codifica(Dibuja(256, 256, false, 4), SKEncodedImageFormat.Jpeg, 92)));
            portadas.Add(new KeyValuePair<string, byte[]>("png-300-alfa", Codifica(Dibuja(300, 300, true, 5), SKEncodedImageFormat.Png, 100)));
            portadas.Add(new KeyValuePair<string, byte[]>("png-256-alfa", Codifica(Dibuja(256, 256, true, 6), SKEncodedImageFormat.Png, 100)));
            portadas.Add(new KeyValuePair<string, byte[]>("png-500-opaca", Codifica(Dibuja(500, 500, false, 7), SKEncodedImageFormat.Png, 100)));
            portadas.Add(new KeyValuePair<string, byte[]>("png-7x9-alfa", Codifica(Dibuja(7, 9, true, 8), SKEncodedImageFormat.Png, 100)));
            portadas.Add(new KeyValuePair<string, byte[]>("png-1x1", Codifica(Dibuja(1, 1, false, 9), SKEncodedImageFormat.Png, 100)));
            portadas.Add(new KeyValuePair<string, byte[]>("gif-transparente", Convert.FromBase64String(GifA)));
            portadas.Add(new KeyValuePair<string, byte[]>("gif-paleta", Convert.FromBase64String(GifB)));
            portadas.Add(new KeyValuePair<string, byte[]>("bmp-30x20", Bmp(30, 20)));
            byte[] basura = new byte[4000]; new Random(11).NextBytes(basura); basura[0] = 0xFF; basura[1] = 0xD8;
            portadas.Add(new KeyValuePair<string, byte[]>("basura", basura));
            string pdir = Path.Combine(dir, "portadas"); Directory.CreateDirectory(pdir);
            List<object> hechas = new List<object>();
            foreach (bool jpeg in new[] { false, true })
            {
                jpegCampo.SetValue(null, jpeg);
                foreach (KeyValuePair<string, byte[]> kv in portadas)
                {
                    string ruta = Path.Combine(pdir, kv.Key + ".img"); File.WriteAllBytes(ruta, kv.Value);
                    string c = Path.Combine(dir, "cache"), id = Path.Combine(c, "0123456789abcdef_256_v2.png");
                    string nom = "jpeg" + (jpeg ? 1 : 0) + " " + kv.Key;
                    // sin identidad: solo decodifica
                    Limpia(c);
                    object r = Llama(conIdentidad, new object[] { ruta, null });
                    Linea(nom + " sin-identidad", PuntosCache(r) + "\t" + Archivos(c));
                    // con identidad: decodifica y guarda; la segunda vez lee la guardada
                    r = Llama(conIdentidad, new object[] { ruta, id });
                    Linea(nom + " guarda", PuntosCache(r) + "\t" + Archivos(c));
                    r = Llama(conIdentidad, new object[] { ruta, id });
                    Linea(nom + " lee-guardada", PuntosCache(r) + "\t" + Archivos(c));
                    if (r is Rocola.Dibujo2D.Image || r is SKBitmap) hechas.Add(r);
                    // para publicar: la copia por guardar, guardada despues con SaveThumb (la de la version: SKBitmap o Bitmap)
                    Limpia(c);
                    object[] a = { ruta, id, null };
                    r = Llama(paraPublicar, a);
                    Linea(nom + " publica", PuntosCache(r) + "\t" + PuntosSolo(a[2]) + "\t" + Archivos(c));
                    if (a[2] != null)
                    {
                        MethodInfo guarda = Metodo(F, "SaveThumb", p => p.Length == 2 && p[0].ParameterType.IsAssignableFrom(a[2].GetType()));
                        Llama(guarda, new object[] { a[2], id });
                        Linea(nom + " publica-guarda", Archivos(c));
                    }
                    a = new object[] { ruta, id, null };
                    r = Llama(paraPublicar, a);
                    Linea(nom + " publica-guardada", PuntosCache(r) + "\t" + PuntosSolo(a[2]) + "\t" + Archivos(c));
                    // el calentador: con «JPEG» rehace las que solo estan en PNG; su copia la guarda el (SaveThumb con un Bitmap de la capa:
                    // en la traduccion, por el puente)
                    a = new object[] { ruta, id, null };
                    r = Llama(paraCalentar, a);
                    Linea(nom + " calienta", PuntosCache(r) + "\t" + PuntosCache(a[2]) + "\t" + Archivos(c));
                    if (a[2] != null)
                    {
                        MethodInfo guarda = Metodo(F, "SaveThumb", p => p.Length == 2 && p[0].ParameterType.IsAssignableFrom(a[2].GetType()));
                        Llama(guarda, new object[] { a[2], id });
                        Linea(nom + " calienta-guarda", Archivos(c));
                    }
                    // con «JPEG» y solo la PNG guardada (sin marca de alfa): el calentador la rehace desde la original
                    if (jpeg)
                    {
                        Limpia(c); jpegCampo.SetValue(null, false); Llama(conIdentidad, new object[] { ruta, id }); jpegCampo.SetValue(null, true);
                        a = new object[] { ruta, id, null };
                        r = Llama(paraCalentar, a);
                        Linea(nom + " calienta-rehace", PuntosCache(r) + "\t" + PuntosCache(a[2]) + "\t" + Archivos(c));
                        if (a[2] != null)
                        {
                            MethodInfo guarda = Metodo(F, "SaveThumb", p => p.Length == 2 && p[0].ParameterType.IsAssignableFrom(a[2].GetType()));
                            Llama(guarda, new object[] { a[2], id });
                            Linea(nom + " calienta-rehace-guarda", Archivos(c));
                        }
                    }
                    // una guardada de otro tamano: se rehace
                    Limpia(c);
                    File.WriteAllBytes(id, Codifica(Dibuja(100, 100, true, 12), SKEncodedImageFormat.Png, 100));
                    r = Llama(conIdentidad, new object[] { ruta, id });
                    Linea(nom + " otra-medida", PuntosCache(r) + "\t" + Archivos(c));
                }
            }
            jpegCampo.SetValue(null, false);

            // 2) MiniaturaOpaca, HazOpaca y CopiaMiniaturaGpu sobre mapas de 256 (y otros)
            Func<int, int, int>[] alfas = {
                (x, y) => 255, (x, y) => 0, (x, y) => (x == 0 || y == 0 || x == 255 || y == 255) ? 128 : 255, (x, y) => (x == 100 && y == 37) ? 254 : 255,
                (x, y) => (x + y) & 255, (x, y) => (x == 1 && y == 1) ? 1 : 255, (x, y) => (x == 2 && y == 3) ? 200 : 255, (x, y) => y < 128 ? 255 : 0 };
            for (int i = 0; i < alfas.Length; i++)
                foreach (int lado in new[] { 256, 33 })
                {
                    SKBitmap m = Mapa(lado, lado, alfas[i]);
                    Linea("opaca " + i + " " + lado, Convert.ToString(Llama(opaca, new[] { Arg(opaca.GetParameters()[0].ParameterType, m, PixelFormat.Format32bppPArgb) }), ci));
                    Linea("copia " + i + " " + lado, PuntosSolo(Llama(copiaGpu, new[] { Arg(copiaGpu.GetParameters()[0].ParameterType, m, PixelFormat.Format32bppPArgb) })));
                    Llama(hazOpaca, new[] { Arg(hazOpaca.GetParameters()[0].ParameterType, m, PixelFormat.Format32bppPArgb) });
                    Linea("hazopaca " + i + " " + lado, Puntos(m));
                    // con un mapa Format32bppArgb XP no lo toca (solo PArgb); en la traduccion todos los SKBitmap son BGRA premultiplicado (el
                    // PArgb de XP) y el unico que llama le pasa la miniatura recien hecha: ese caso no existe alli. Solo en la referencia, aparte.
                    if (hazOpaca.GetParameters()[0].ParameterType == typeof(Bitmap))
                    {
                        SKBitmap m2 = Mapa(lado, lado, alfas[i]); string antes = Puntos(m2);
                        Llama(hazOpaca, new[] { Arg(hazOpaca.GetParameters()[0].ParameterType, m2, PixelFormat.Format32bppArgb) });
                        Console.Error.WriteLine("solo referencia: hazopaca-argb " + i + " " + lado + (Puntos(m2) == antes ? " no lo toca" : " LO TOCA"));
                    }
                }

            // 3) las previas: Small de las miniaturas hechas, ToBytes y FromBytes
            for (int i = 0; i < hechas.Count; i++)
            {
                object arg = small.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(SKBitmap)) && hechas[i] is Rocola.Dibujo2D.Image ? (object)((Rocola.Dibujo2D.Image)hechas[i]).Sk : hechas[i];
                object s = Llama(small, new[] { arg });
                byte[] bytes = s is string ? null : (byte[])Llama(toBytes, new[] { s });
                Linea("previa " + i, PuntosSolo(s) + "\t" + (bytes != null ? Huella(bytes) : "-") + "\t" + (bytes != null ? PuntosCache(Llama(fromBytes, new object[] { bytes })) : "-"));
            }
            { object arg = small.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(SKBitmap)) ? null : (object)null; Linea("previa null", PuntosSolo(Llama(small, new[] { arg }))); }

            // 4) las teselas
            MethodInfo de = Metodo(typeof(TeselasVisibles), "De", p => p.Length == 3);
            Type tt = typeof(TrabajoTeselas);
            List<KeyValuePair<string, SKBitmap>> mapas = new List<KeyValuePair<string, SKBitmap>>();
            mapas.Add(new KeyValuePair<string, SKBitmap>("vacio-256x192", Mapa(256, 192, (x, y) => 0)));
            mapas.Add(new KeyValuePair<string, SKBitmap>("lleno-128", Mapa(128, 128, (x, y) => 255)));
            mapas.Add(new KeyValuePair<string, SKBitmap>("sueltos-256x192", Mapa(256, 192, (x, y) => ((x * 31 + y * 17) % 97 == 0) ? 1 + (x % 254) : 0)));
            mapas.Add(new KeyValuePair<string, SKBitmap>("centro-128", Mapa(128, 128, (x, y) => (Math.Abs(x - 64) + Math.Abs(y - 64) < 40) ? 0 : 255 - Math.Abs(x - 64))));
            mapas.Add(new KeyValuePair<string, SKBitmap>("esquinas-33x17", Mapa(33, 17, (x, y) => ((x == 0 || x == 32) && (y == 0 || y == 16)) ? 3 : 0)));
            mapas.Add(new KeyValuePair<string, SKBitmap>("1x1", Mapa(1, 1, (x, y) => 9)));
            mapas.Add(new KeyValuePair<string, SKBitmap>("franja-1024x104", Mapa(1024, 104, (x, y) => (y > 60 && (x / 40) % 3 != 1) ? 200 : (x == 500 && y == 3 ? 1 : 0))));
            mapas.Add(new KeyValuePair<string, SKBitmap>("ui-1024x664", Mapa(1024, 664, (x, y) => (x > 100 && x < 300 && y > 200 && y < 420) || (x > 700 && y < 50) ? 255 : 0)));
            foreach (KeyValuePair<string, SKBitmap> kv in mapas)
                foreach (PixelFormat f in new[] { PixelFormat.Format32bppArgb, PixelFormat.Format32bppPArgb })
                {
                    string nom = "teselas " + kv.Key + " " + (f == PixelFormat.Format32bppArgb ? "argb" : "pargb");
                    foreach (int lado in new[] { 2, 16 })
                    {
                        object t = Llama(de, new[] { Arg(de.GetParameters()[0].ParameterType, kv.Value, f), lado, lado == 2 });
                        Linea(nom + " de " + lado, Teselas(t));
                    }
                    // TrabajoTeselas: paso a paso (presupuesto chico) hasta el final
                    ConstructorInfo ctor = tt.GetConstructors(Todo)[0];
                    object tr = ctor.Invoke(new[] { Arg(ctor.GetParameters()[0].ParameterType, kv.Value, f), 16 });
                    MethodInfo avanza = tt.GetMethod("Avanza", Todo);
                    object res = null; int pasos = 0;
                    while (res == null && pasos < 1000000) { res = avanza.Invoke(tr, new object[] { 1L }); pasos++; }
                    Linea(nom + " en-pasos", Teselas(res));
                    object res2 = avanza.Invoke(ctor.Invoke(new[] { Arg(ctor.GetParameters()[0].ParameterType, kv.Value, f), 16 }), new object[] { long.MaxValue });
                    Linea(nom + " de-una", Teselas(res2));
                    // (revision) en la traduccion, la rama que no es BGRA premultiplicado (LecturaNativa falso: la copia sin premultiplicar de
                    // Pixeles.LeeSinPremultiplicar) no la pasa ningun mapa de la rocola: se pasa aqui con el mismo mapa sin premultiplicar y
                    // tiene que dar lo mismo que el premultiplicado (solo cuenta el alfa). Solo a la salida de errores: no cambia la comparacion.
                    if (de.GetParameters()[0].ParameterType == typeof(SKBitmap) && f == PixelFormat.Format32bppArgb)
                    {
                        SKBitmap u = SinPremultiplicar(kv.Value);
                        foreach (int lado in new[] { 2, 16 })
                            if (Teselas(Llama(de, new object[] { u, lado, lado == 2 })) != Teselas(Llama(de, new object[] { kv.Value, lado, lado == 2 })))
                                Console.Error.WriteLine("NO NATIVO DISTINTO de " + kv.Key + " " + lado);
                        object ru = null; int pu = 0; object tu = ctor.Invoke(new object[] { u, 16 });
                        while (ru == null && pu < 1000000) { ru = avanza.Invoke(tu, new object[] { 1L }); pu++; }
                        if (Teselas(ru) != Teselas(res)) Console.Error.WriteLine("NO NATIVO DISTINTO en-pasos " + kv.Key);
                        noNativos++;
                    }
                }

            // 5) InteriorOpaco: con un Bitmap de la capa en sus formatos (en la traduccion, por el puente) y, en la traduccion, directo
            Type panel = typeof(Form1).Assembly.GetType("Rocola.GpuPanel");
            // (cache-portadas, 12-11-2026) la de un Bitmap (el puente de Puente.portadas.cs) ya no existe en la traduccion: alli se llama a la de un
            // SKBitmap con lo que XP miraba del formato (Image.IsAlphaPixelFormat o de paleta), como hacia el puente y como hace ahora
            // CaratulasSeparadasGpu.cs con el formato de la portada (Form1.FormatoPortada)
            MethodInfo ioCapa = null; foreach (MethodInfo m in panel.GetMethods(Todo)) if (m.Name == "InteriorOpaco" && m.GetParameters().Length == 1) ioCapa = m;
            MethodInfo ioSk = null; foreach (MethodInfo m in panel.GetMethods(Todo)) if (m.Name == "InteriorOpaco" && m.GetParameters().Length == 2) ioSk = m;
            for (int i = 0; i < alfas.Length; i++)
                foreach (int lado in new[] { 256, 5, 4 })
                    foreach (PixelFormat f in new[] { PixelFormat.Format32bppPArgb, PixelFormat.Format32bppArgb, PixelFormat.Format24bppRgb, PixelFormat.Format8bppIndexed })
                    {
                        SKBitmap m = Mapa(lado, lado, alfas[i]);
                        bool alfa = Rocola.Dibujo2D.Image.IsAlphaPixelFormat(f) || (f & PixelFormat.Indexed) != 0;
                        string r = ioCapa != null ? Convert.ToString(Llama(ioCapa, new object[] { new Bitmap(m, f) }), ci) : Convert.ToString(Llama(ioSk, new object[] { m, alfa }), ci);
                        Linea("interior " + i + " " + lado + " " + f, r);
                        if (ioSk != null)
                        {
                            string d = Convert.ToString(Llama(ioSk, new object[] { m, alfa }), ci);
                            if (d != r) Console.Error.WriteLine("DIRECTO DISTINTO interior " + i + " " + lado + " " + f + ": " + d + " / " + r);
                            string du = Convert.ToString(Llama(ioSk, new object[] { SinPremultiplicar(m), alfa }), ci);   // (revision) la rama no nativa
                            if (du != r) Console.Error.WriteLine("NO NATIVO DISTINTO interior " + i + " " + lado + " " + f + ": " + du + " / " + r);
                            noNativos++;
                        }
                    }
            Linea("interior null", Convert.ToString(ioCapa != null ? Llama(ioCapa, new object[] { null }) : Llama(ioSk, new object[] { null, true }), ci));
            Cache(conIdentidad);
            if (noNativos > 0) Console.Error.WriteLine("no nativos comparados: " + noNativos);
            Console.Error.WriteLine(n + " lineas, " + excepciones + " excepciones");
            return 0;
        }
        // ---- (cache-portadas, 12-11-2026) LA CACHE DE PORTADAS: la identidad y la portada ya soltada. Un Form1 sin construir (solo sus
        // colecciones y su candado, que son los inicializadores de sus campos) con la cache de verdad: CachePut, CoverEnCache, PortadaEnMemoria,
        // LoadCover, LoadCoverByPath, LoadCoverAsync, PutGpuPreview, PeekGpuPreview y DrainCoverTrashAcotado.
        //   - identidad: lo que dan todas es el MISMO objeto que entro (como la Image de XP), y lo que sale de la cache va a la papelera tal cual;
        //   - soltada: DrainCoverTrashAcotado suelta las de la papelera que no estan en uso (la que vuela, transImg, se queda); despues, donde XP
        //     lee la medida de una portada y atrapa lo que lance (ColoresDePortada en el trabajador de portadas, Escala en las miniaturas del
        //     buscador), la portada soltada LANZA y se atrapa como en XP, sin violacion de segmento (si la hubiera, el programa se cerraria aqui
        //     y faltarian las lineas de abajo). En XP lanza ArgumentException (GDI+); la referencia, NullReferenceException (la capa); la
        //     traduccion, ArgumentException (Nativo/MapaVivo.cs): la salida dice solo «lanza» y el tipo va a la salida de errores.
        private static bool Soltada(object o) { return o is SKBitmap ? ((SKBitmap)o).Handle == IntPtr.Zero : ((Rocola.Dibujo2D.Image)o).Sk == null; }
        private static object Llama2(object blanco, string nombre, params object[] a)
        {
            foreach (MethodInfo m in blanco.GetType().GetMethods(Todo))
            {
                if (m.Name != nombre || m.GetParameters().Length != a.Length) continue;
                ParameterInfo[] ps = m.GetParameters(); bool vale = true;
                for (int i = 0; i < ps.Length && vale; i++) { Type pt = ps[i].ParameterType; if (pt.IsByRef) pt = pt.GetElementType(); if (a[i] != null && !pt.IsInstanceOfType(a[i])) vale = false; }
                if (!vale) continue;
                object r = m.Invoke(m.IsStatic ? null : blanco, a);
                return r;
            }
            throw new Exception("no esta " + nombre);
        }
        private static string Lanza(Func<object> f, string que)
        {
            try { object r = f(); return "no lanza (" + Convert.ToString(r, ci) + ")"; }
            catch (Exception e) { Exception x = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; Console.Error.WriteLine("soltada " + que + ": " + x.GetType().Name); return "lanza"; }
        }
        private static void Cache(MethodInfo conIdentidad)
        {
            Type F = typeof(Form1);
            object f1 = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(F);
            foreach (FieldInfo c in F.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (c.GetValue(f1) == null && (c.FieldType == typeof(object) || (c.FieldType.Namespace == "System.Collections.Generic" && !c.FieldType.IsInterface)))
                    try { c.SetValue(f1, Activator.CreateInstance(c.FieldType)); } catch { }
            F.GetField("topePortadas", Todo).SetValue(null, 3);
            FieldInfo lod = F.GetField("caratulasLod", Todo); if (lod != null) lod.SetValue(lod.IsStatic ? null : f1, true);
            Type TAlbum = F.GetNestedType("Album", BindingFlags.NonPublic);
            Func<string, object> disco = k => { object a = Activator.CreateInstance(TAlbum, true); TAlbum.GetField("CoverPath", Todo).SetValue(a, k); return a; };
            string pdir = Path.Combine(Path.GetTempPath(), "vistas-portadas-cache"); Limpia(pdir);
            object[] T = new object[6];
            for (int i = 1; i <= 5; i++)
            {
                string ruta = Path.Combine(pdir, "p" + i + ".png"); File.WriteAllBytes(ruta, Codifica(Dibuja(40 + i, 30 + i, i % 2 == 0, 20 + i), SKEncodedImageFormat.Png, 100));
                T[i] = conIdentidad.Invoke(null, new object[] { ruta, null });
            }
            Func<object, string> cual = o => { if (o == null) return "null"; for (int i = 1; i <= 5; i++) if (ReferenceEquals(o, T[i])) return "T" + i; return "OTRA"; };
            Func<string> papelera = () => { StringBuilder sb = new StringBuilder(); foreach (object o in (IEnumerable<object>)F.GetField("coverTrash", Todo).GetValue(f1)) sb.Append(cual(o)).Append(' '); return sb.Length == 0 ? "-" : sb.ToString().TrimEnd(); };
            Func<string, string> todas = k =>
            {
                object a = disco(k); object[] e = { k, true, null };
                bool en = (bool)Llama2(f1, "CoverEnCache", e);
                return "CoverEnCache=" + en + ":" + cual(e[2]) + " PortadaEnMemoria=" + cual(Llama2(f1, "PortadaEnMemoria", a)) + " LoadCover=" + cual(Llama2(f1, "LoadCover", a))
                    + " LoadCoverByPath=" + cual(Llama2(f1, "LoadCoverByPath", k)) + " LoadCoverAsync=" + cual(Llama2(f1, "LoadCoverAsync", a));
            };
            Llama2(f1, "CachePut", "k1", T[1]);
            Linea("cache identidad k1", todas("k1") + "\tpapelera " + papelera());
            Linea("cache identidad k1 otra vez", todas("k1"));
            Llama2(f1, "CachePut", "k1", T[2]);   // reemplaza: la de antes, a la papelera tal cual
            Linea("cache reemplaza k1", todas("k1") + "\tpapelera " + papelera());
            Llama2(f1, "CachePut", "k1", T[2]);   // la misma: no va a la papelera
            Linea("cache misma k1", todas("k1") + "\tpapelera " + papelera());
            Llama2(f1, "CachePut", "k2", T[3]); Llama2(f1, "CachePut", "k3", T[4]); Llama2(f1, "CachePut", "k4", T[5]);   // tope 3: sale la menos usada (k1)
            Func<string, string> en = k => { object[] e = { k, false, null }; bool b = (bool)Llama2(f1, "CoverEnCache", e); return b + ":" + cual(e[2]); };   // (sin encargar nada: solo mira)
            Linea("cache expulsa", "k1 " + en("k1") + " k2 " + en("k2") + " k3 " + en("k3") + " k4 " + en("k4") + "\tpapelera " + papelera());
            // las previas: la misma que entro
            object pv = Llama(Metodo(typeof(Form1).Assembly.GetType("Rocola.GpuPreviewStore"), "Small", p => p.Length == 1), new[] { T[3] });
            Llama2(f1, "PutGpuPreview", "k2", pv);
            object pv2 = Llama2(f1, "PeekGpuPreview", disco("k2"));
            Linea("previa identidad", "PeekGpuPreview=" + (ReferenceEquals(pv, pv2) ? "la misma" : "OTRA") + " " + PuntosSolo(pv2));
            // la papelera: se sueltan las que no estan en uso; la que vuela (transImg) se queda
            F.GetField("transImg", Todo).SetValue(f1, T[2]);
            int sueltas = (int)Llama2(f1, "DrainCoverTrashAcotado", 32, long.MaxValue);
            Linea("papelera vaciada", sueltas + " retiradas; T1 soltada=" + Soltada(T[1]) + " T2 (vuela) soltada=" + Soltada(T[2]) + " T3 soltada=" + Soltada(T[3]) + "\tpapelera " + papelera());
            // la portada soltada (T1) donde XP la lee y atrapa lo que lance
            object sinUso1, sinUso2; sinUso1 = null; sinUso2 = null;
            MethodInfo colores = Metodo(F, "ColoresDePortada", p => p.Length == 3 && p[0].ParameterType.IsInstanceOfType(T[1]));
            Linea("soltada ColoresDePortada", Lanza(() => colores.Invoke(null, new object[] { T[1], sinUso1, sinUso2 }), "ColoresDePortada"));
            // el trabajador de portadas (Form1.Portadas.cs): try { tonos.Prom = ColoresDePortada(...); tonos.Paleta = ExtractCoverPalette(...); } catch
            string tonos;
            try { colores.Invoke(null, new object[] { T[1], sinUso1, sinUso2 }); tonos = "con tonos"; } catch (TargetInvocationException) { tonos = "sin tonos (atrapada, como en XP)"; }
            Linea("soltada trabajador", tonos);
            MethodInfo escala = null;
            foreach (Type ty in typeof(Form1).Assembly.GetTypes()) foreach (MethodInfo m in ty.GetMethods(Todo | BindingFlags.DeclaredOnly)) if (m.Name == "Escala" && m.IsStatic && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsInstanceOfType(T[1])) escala = m;
            Linea("soltada Escala", escala == null ? "no esta" : Lanza(() => escala.Invoke(null, new object[] { T[1], 32 }), "Escala"));
            // una viva no lanza: la misma portada de la cache, la que vuela
            Linea("viva Escala", escala == null ? "no esta" : PuntosSolo(escala.Invoke(null, new object[] { T[2], 32 })));
            Type vivo = typeof(Form1).Assembly.GetType("Rocola.Nativo.MapaVivo");
            if (vivo != null)
            {
                SKBitmap s1 = (SKBitmap)T[1];
                Console.Error.WriteLine("MapaVivo.Soltado(T1)=" + vivo.GetMethod("Soltado").Invoke(null, new object[] { s1 }) + " Soltado(T2)=" + vivo.GetMethod("Soltado").Invoke(null, new object[] { T[2] }) + " Ancho(T1): " + Lanza(() => vivo.GetMethod("Ancho").Invoke(null, new object[] { s1 }), "MapaVivo.Ancho") + " Ancho(T2)=" + vivo.GetMethod("Ancho").Invoke(null, new object[] { T[2] }));
            }
            Linea("soltada sigue", "el programa sigue (sin violacion de segmento)");
        }
        private static string Teselas(object t)
        {
            if (t == null) return "null";
            if (t is string) return (string)t;
            TeselasVisibles v = (TeselasVisibles)t;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < v.N * 4; i++) sb.Append(v.Rect[i]).Append(',');
            return v.Ancho + "x" + v.Alto + " N=" + v.N + " F=" + v.Fraccion.ToString("R", ci) + " " + Huella(Encoding.ASCII.GetBytes(sb.ToString())) + " vtx=" + v.Vtx.Length;
        }
    }
}
