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
        private static int n, excepciones;
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }

        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap de la traduccion): tal cual (premultiplicados) y su formato de la capa
        private static unsafe string Puntos(object o)
        {
            if (o == null) return "null";
            if (o is string) return (string)o;
            SKBitmap s = o is SKBitmap ? (SKBitmap)o : ((Rocola.Dibujo2D.Image)o).Sk;
            string fmt = o is Rocola.Dibujo2D.Image ? ((Rocola.Dibujo2D.Image)o).PixelFormat + "/" + ((Rocola.Dibujo2D.Image)o).Flags : "sk";
            if (s == null) return "soltado " + fmt;
            byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            return s.Width + "x" + s.Height + " " + s.ColorType + "/" + s.AlphaType + " " + Huella(px) + (o is SKBitmap ? "" : " " + fmt);
        }
        // lo mismo, sin el formato de la capa (lo que una traducida devuelve como SKBitmap y la referencia como Bitmap)
        private static string PuntosSolo(object o) { string s = Puntos(o); int i = s.IndexOf(" Format"); return i < 0 ? s : s.Substring(0, i); }

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
                    Linea(nom + " sin-identidad", Puntos(r) + "\t" + Archivos(c));
                    // con identidad: decodifica y guarda; la segunda vez lee la guardada
                    r = Llama(conIdentidad, new object[] { ruta, id });
                    Linea(nom + " guarda", Puntos(r) + "\t" + Archivos(c));
                    r = Llama(conIdentidad, new object[] { ruta, id });
                    Linea(nom + " lee-guardada", Puntos(r) + "\t" + Archivos(c));
                    if (r is Rocola.Dibujo2D.Image) hechas.Add(r);
                    // para publicar: la copia por guardar, guardada despues con SaveThumb (la de la version: SKBitmap o Bitmap)
                    Limpia(c);
                    object[] a = { ruta, id, null };
                    r = Llama(paraPublicar, a);
                    Linea(nom + " publica", Puntos(r) + "\t" + PuntosSolo(a[2]) + "\t" + Archivos(c));
                    if (a[2] != null)
                    {
                        MethodInfo guarda = Metodo(F, "SaveThumb", p => p.Length == 2 && p[0].ParameterType.IsAssignableFrom(a[2].GetType()));
                        Llama(guarda, new object[] { a[2], id });
                        Linea(nom + " publica-guarda", Archivos(c));
                    }
                    a = new object[] { ruta, id, null };
                    r = Llama(paraPublicar, a);
                    Linea(nom + " publica-guardada", Puntos(r) + "\t" + PuntosSolo(a[2]) + "\t" + Archivos(c));
                    // el calentador: con «JPEG» rehace las que solo estan en PNG; su copia la guarda el (SaveThumb con un Bitmap de la capa:
                    // en la traduccion, por el puente)
                    a = new object[] { ruta, id, null };
                    r = Llama(paraCalentar, a);
                    Linea(nom + " calienta", Puntos(r) + "\t" + Puntos(a[2]) + "\t" + Archivos(c));
                    if (a[2] != null)
                    {
                        MethodInfo guarda = Metodo(F, "SaveThumb", p => p.Length == 2 && p[0].ParameterType == typeof(Bitmap));
                        Llama(guarda, new object[] { a[2], id });
                        Linea(nom + " calienta-guarda", Archivos(c));
                    }
                    // con «JPEG» y solo la PNG guardada (sin marca de alfa): el calentador la rehace desde la original
                    if (jpeg)
                    {
                        Limpia(c); jpegCampo.SetValue(null, false); Llama(conIdentidad, new object[] { ruta, id }); jpegCampo.SetValue(null, true);
                        a = new object[] { ruta, id, null };
                        r = Llama(paraCalentar, a);
                        Linea(nom + " calienta-rehace", Puntos(r) + "\t" + Puntos(a[2]) + "\t" + Archivos(c));
                        if (a[2] != null)
                        {
                            MethodInfo guarda = Metodo(F, "SaveThumb", p => p.Length == 2 && p[0].ParameterType == typeof(Bitmap));
                            Llama(guarda, new object[] { a[2], id });
                            Linea(nom + " calienta-rehace-guarda", Archivos(c));
                        }
                    }
                    // una guardada de otro tamano: se rehace
                    Limpia(c);
                    File.WriteAllBytes(id, Codifica(Dibuja(100, 100, true, 12), SKEncodedImageFormat.Png, 100));
                    r = Llama(conIdentidad, new object[] { ruta, id });
                    Linea(nom + " otra-medida", Puntos(r) + "\t" + Archivos(c));
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
                object arg = small.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(SKBitmap)) ? (object)((Rocola.Dibujo2D.Image)hechas[i]).Sk : hechas[i];
                object s = Llama(small, new[] { arg });
                byte[] bytes = s is string ? null : (byte[])Llama(toBytes, new[] { s is SKBitmap ? s : (object)(Bitmap)s });
                Linea("previa " + i, PuntosSolo(s) + "\t" + (bytes != null ? Huella(bytes) : "-") + "\t" + (bytes != null ? Puntos(Llama(fromBytes, new object[] { bytes })) : "-"));
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
                }

            // 5) InteriorOpaco: con un Bitmap de la capa en sus formatos (en la traduccion, por el puente) y, en la traduccion, directo
            Type panel = typeof(Form1).Assembly.GetType("Rocola.GpuPanel");
            MethodInfo ioCapa = Metodo(panel, "InteriorOpaco", p => p.Length == 1);
            MethodInfo ioSk = null; foreach (MethodInfo m in panel.GetMethods(Todo)) if (m.Name == "InteriorOpaco" && m.GetParameters().Length == 2) ioSk = m;
            for (int i = 0; i < alfas.Length; i++)
                foreach (int lado in new[] { 256, 5, 4 })
                    foreach (PixelFormat f in new[] { PixelFormat.Format32bppPArgb, PixelFormat.Format32bppArgb, PixelFormat.Format24bppRgb, PixelFormat.Format8bppIndexed })
                    {
                        SKBitmap m = Mapa(lado, lado, alfas[i]);
                        string r = Convert.ToString(Llama(ioCapa, new object[] { new Bitmap(m, f) }), ci);
                        Linea("interior " + i + " " + lado + " " + f, r);
                        if (ioSk != null)
                        {
                            bool alfa = Rocola.Dibujo2D.Image.IsAlphaPixelFormat(f) || (f & PixelFormat.Indexed) != 0;
                            string d = Convert.ToString(Llama(ioSk, new object[] { m, alfa }), ci);
                            if (d != r) Console.Error.WriteLine("DIRECTO DISTINTO interior " + i + " " + lado + " " + f + ": " + d + " / " + r);
                        }
                    }
            Linea("interior null", Convert.ToString(Llama(ioCapa, new object[] { null }), ci));
            Console.Error.WriteLine(n + " lineas, " + excepciones + " excepciones");
            return 0;
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
