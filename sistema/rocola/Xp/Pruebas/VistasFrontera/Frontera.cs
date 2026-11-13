// EL ARNES DE LA FRONTERA CON LA TARJETA (port nativo, tanda 2, grupo frontera, 8-11-2026). Se compila con el programa entero de una carpeta
// Xp (VistasFrontera.csproj: esta copia de trabajo o la referencia congelada) y llama con datos fijos a lo que los recorridos no pasan, o
// pasan con datos que cambian con la hora:
//   1) los hornos de D3D9Star.cs (BakeViaLactea, BakeNebulosas, BakeRadialGlow, BakeStarTexture, BakeVineta) con todas las medidas que usa
//      XP (la cuarta parte de cada pantalla, el atlas de 64, el halo de 64, la vineta de 128) y otras, y los tres de otros archivos cuya
//      firma cambio (BakeStar, BakeNatural, BakeRuidoGrande): la huella de cada mapa, los puntos tal cual y como los sube la tarjeta
//      (LockBits Format32bppArgb de la capa);
//   2) la subida a la tarjeta (un GpuStarfield fuera de pantalla: su constructor hornea y sube el atlas y el halo) con mapas de prueba al
//      azar con semilla fija (opacos, con alfa, con los alfas de las orillas, de 1x1, finos, grandes, en teselas con el limite bajo):
//      CreateOverlay, UpdateOverlay, UpdateOverlayRegion, CreateCoverShared y CreateTextureFromBitmap, lo que devuelven, y con
//      ROCOLA_VOLCADO_TEXTURAS cada zona subida de 64x64 o mas (compara.sh las junta por orden, medida y huella);
//   3) el contador de cuadros (EnsureFpsTex, Ctrl+F1) en un GpuPanel sin construir, con numeros fijos y dos anchos de pantalla.
// La referencia recibe Bitmap de la capa; la traduccion, SKBitmap (Pru.TMapa: Comun/PruCapa.cs y PruNativo.cs). La puerta «puente» (el Bitmap
// de la capa por las sobrecargas de Plataforma/Puente.frontera.cs) se quito en la oleada 8b (13-11-2026): desde interfaz-a-skbitmap
// (12-11-2026) iba por la misma SKBitmap que «sk». Las salidas tienen que ser iguales linea por linea, y los volcados.
//   ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_VOLCADO_TEXTURAS=DIR dotnet bin/Release/net10.0/VistasFrontera.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Rocola;
using SkiaSharp;

namespace Banco
{
    internal static class Frontera
    {
        const BindingFlags Todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static string carpeta;
        private static int n;

        // ---- 1) los mapas: los puntos tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb de la capa)
        private static unsafe void Apunta(string nombre, object o)
        {
            n++;
            if (o == null) { Console.WriteLine(nombre + "\tnull"); return; }
            object b = Pru.Aprecia(o, "Format32bppArgb");
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            Console.WriteLine(nombre + "\t" + s.Width + "x" + s.Height + "\t" + s.ColorType + "/" + s.AlphaType + "\t" + Huella(px) + "\t" + Huella(sub));
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            Pru.Suelta(b);
        }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static object Llama(Type t, string metodo, params object[] a)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (m.Name == metodo && m.IsStatic && m.GetParameters().Length == a.Length) return m.Invoke(null, a);
            throw new MissingMethodException(t.Name + "." + metodo);
        }

        // ---- 2) la subida: los mapas de prueba son SKBitmap (premultiplicados, como los de la rocola); a quien pide un Bitmap se le da uno de
        // la capa sobre los mismos puntos (Format32bppArgb, como los de XP)
        private static object stars, gpu;
        private static readonly Dictionary<IntPtr, int> nombres = new Dictionary<IntPtr, int>();
        private static string Nombre(IntPtr t) { if (t == IntPtr.Zero) return "0"; int k; if (!nombres.TryGetValue(t, out k)) { k = nombres.Count + 1; nombres[t] = k; } return "t" + k; }
        private static readonly Dictionary<SKBitmap, object> envueltos = new Dictionary<SKBitmap, object>();
        private static MethodInfo Metodo(object en, string nombre, int parametros)
        {
            MethodInfo hallado = null;
            foreach (MethodInfo m in en.GetType().GetMethods(Todo))
            {
                if (m.Name != nombre || m.GetParameters().Length != parametros) continue;
                foreach (ParameterInfo p in m.GetParameters())
                {
                    if (p.ParameterType == typeof(SKBitmap) || p.ParameterType == Pru.TMapa) hallado = m;
                }
            }
            if (hallado != null) return hallado;
            throw new MissingMethodException(en.GetType().Name + "." + nombre);
        }
        private static object Dale(MethodInfo m, SKBitmap sk)
        {
            foreach (ParameterInfo p in m.GetParameters())
                if (p.ParameterType == Pru.TMapa)
                {
                    object b;
                    if (!envueltos.TryGetValue(sk, out b)) { b = Pru.DeSk(sk, "Format32bppArgb"); envueltos[sk] = b; }
                    return b;
                }
            return sk;
        }
        private static string F(float f) { return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
        private static IntPtr Crea(string que, string nombre, SKBitmap sk)
        {
            MethodInfo m = Metodo(stars, que, 3);
            object[] a = { Dale(m, sk), 0f, 0f };
            IntPtr t = (IntPtr)m.Invoke(stars, a);
            Console.WriteLine(que + " " + nombre + " " + sk.Width + "x" + sk.Height + " -> " + Nombre(t) + " u=" + F((float)a[1]) + " v=" + F((float)a[2]));
            return t;
        }
        private static void Renueva(string nombre, IntPtr t, SKBitmap sk)
        {
            MethodInfo m = Metodo(stars, "UpdateOverlay", 4);
            object[] a = { t, Dale(m, sk), 0f, 0f };
            bool r = (bool)m.Invoke(stars, a);
            Console.WriteLine("UpdateOverlay " + Nombre(t) + " " + nombre + " -> " + r + " u=" + F((float)a[2]) + " v=" + F((float)a[3]));
        }
        private static void Zona(string nombre, IntPtr t, SKBitmap sk, Rectangle r)
        {
            MethodInfo m = Metodo(stars, "UpdateOverlayRegion", 3);
            bool ok = (bool)m.Invoke(stars, new object[] { t, Dale(m, sk), r });
            Console.WriteLine("UpdateOverlayRegion " + Nombre(t) + " " + nombre + " " + r.X + "," + r.Y + "," + r.Width + "x" + r.Height + " -> " + ok);
        }
        private static void DelDispositivo(string nombre, SKBitmap sk)
        {
            MethodInfo m = Metodo(gpu, "CreateTextureFromBitmap", 1);
            IntPtr t = (IntPtr)m.Invoke(gpu, new object[] { Dale(m, sk) });
            Console.WriteLine("CreateTextureFromBitmap " + nombre + " " + sk.Width + "x" + sk.Height + " -> " + Nombre(t));
        }
        // un mapa de prueba con semilla fija: 0 opaco, 1 con alfa al azar, 2 con los alfas de las orillas (0, 1, 2, 32, 128, 254, 255), 3 franjas
        private static unsafe SKBitmap Prueba(int w, int h, int clase, int semilla)
        {
            SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
            Random r = new Random(semilla);
            int[] orillas = { 0, 1, 2, 32, 128, 254, 255 };
            byte* p = (byte*)b.GetPixels();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = clase == 0 ? 255 : clase == 1 ? r.Next(256) : clase == 2 ? orillas[r.Next(orillas.Length)] : ((x / 7 + y / 5) % 3 == 0 ? 0 : 200);
                    byte* q = p + (long)y * b.RowBytes + (long)x * 4;
                    q[0] = (byte)r.Next(a + 1); q[1] = (byte)r.Next(a + 1); q[2] = (byte)r.Next(a + 1); q[3] = (byte)a;
                }
            b.NotifyPixelsChanged();
            return b;
        }

        public static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "vuelca" && i + 1 < args.Length) { carpeta = args[++i]; System.IO.Directory.CreateDirectory(carpeta); }
            }
            // ---- 1) los hornos de D3D9Star.cs, con la cuarta parte de cada pantalla (EnsureNieblas: Math.Max(8, Width / 4)) y otras medidas
            int[][] cuartos = { new[] { 256, 192 }, new[] { 320, 256 }, new[] { 200, 150 }, new[] { 341, 192 }, new[] { 400, 225 }, new[] { 160, 120 },
                                new[] { 480, 270 }, new[] { 341, 256 }, new[] { 8, 8 }, new[] { 9, 200 }, new[] { 333, 8 } };
            foreach (int[] m in cuartos) Apunta("BakeViaLactea-" + m[0] + "x" + m[1], GpuStarfield.BakeViaLactea(m[0], m[1]));
            foreach (int[] m in cuartos) Apunta("BakeNebulosas-" + m[0] + "x" + m[1], GpuStarfield.BakeNebulosas(m[0], m[1]));
            foreach (int celda in new[] { 64, 32, 16, 8, 50, 128 }) Apunta("BakeStarTexture-" + celda, Llama(typeof(GpuStarfield), "BakeStarTexture", celda));
            foreach (int lado in new[] { 64, 1, 2, 15, 33, 128, 256 }) Apunta("BakeRadialGlow-" + lado, Llama(typeof(GpuStarfield), "BakeRadialGlow", lado));
            foreach (int lado in new[] { 128, 2, 3, 64, 127, 255, 256 }) Apunta("BakeVineta-" + lado, GpuPanel.BakeVineta(lado));
            // los de otros archivos cuya firma cambio (los sube D3D9Star.cs)
            Apunta("BakeStar-sencilla", GpuPanel.BakeStar(false));
            Apunta("BakeStar-detallada", GpuPanel.BakeStar(true));
            float fuerzaAntes = Form1.nebNaturalFuerza;
            foreach (float fz in new[] { fuerzaAntes, 0f, 0.37f, 1f })
            {
                Form1.nebNaturalFuerza = fz;
                foreach (int[] m in new[] { new[] { 256, 192 }, new[] { 320, 256 }, new[] { 8, 8 } })
                    Apunta("BakeNatural-" + fz.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + m[0] + "x" + m[1], GpuPanel.BakeNatural(m[0], m[1]));
            }
            Form1.nebNaturalFuerza = fuerzaAntes;
            foreach (int lado in new[] { 1024, 512, 100, 64, 1 }) Apunta("BakeRuidoGrande-" + lado, Form1.BakeRuidoGrande(lado));

            // ---- 2) la subida a la tarjeta
            stars = new GpuStarfield(IntPtr.Zero, 1024, 768, 7);   // (el constructor hornea el atlas y el halo y los sube: CreateTextureFromBitmap)
            gpu = stars.GetType().GetField("gpu", Todo).GetValue(stars);
            Console.WriteLine("GpuStarfield Ok=" + stars.GetType().GetProperty("Ok", Todo).GetValue(stars) + " puente=False");
            SKBitmap opaco = Prueba(300, 200, 0, 1), opaco2 = Prueba(300, 200, 0, 2), alfa = Prueba(257, 129, 1, 3), alfa2 = Prueba(257, 129, 1, 4),
                orillas = Prueba(64, 64, 2, 5), orillas2 = Prueba(64, 64, 2, 6), uno = Prueba(1, 1, 1, 7), fino = Prueba(1000, 3, 1, 8),
                franjas = Prueba(128, 96, 3, 9), grande = Prueba(1500, 900, 1, 10), grande2 = Prueba(1500, 900, 2, 11), portada = Prueba(256, 256, 0, 12);
            IntPtr t1 = Crea("CreateOverlay", "opaco", opaco);
            Renueva("opaco2", t1, opaco2);
            Zona("opaco", t1, opaco, new Rectangle(10, 20, 100, 70));
            Zona("opaco", t1, opaco, new Rectangle(200, 100, 100, 100));   // toca la derecha y abajo: el borde de la potencia de 2
            Zona("opaco", t1, opaco, new Rectangle(0, 0, 300, 200));
            Zona("opaco2", t1, opaco2, new Rectangle(299, 199, 1, 1));
            Zona("opaco2", t1, opaco2, new Rectangle(250, 150, 51, 10));   // se sale: no
            IntPtr t2 = Crea("CreateOverlay", "alfa", alfa);
            Renueva("alfa2", t2, alfa2);
            Zona("alfa", t2, alfa, new Rectangle(0, 0, 257, 129));
            Zona("alfa2", t2, alfa2, new Rectangle(193, 65, 64, 64));
            IntPtr t3 = Crea("CreateOverlay", "orillas", orillas);
            Renueva("orillas2", t3, orillas2);
            Zona("orillas", t3, orillas, new Rectangle(0, 0, 64, 64));
            IntPtr t4 = Crea("CreateOverlay", "uno", uno);
            Renueva("uno", t4, uno);
            IntPtr t5 = Crea("CreateOverlay", "fino", fino);
            Zona("fino", t5, fino, new Rectangle(0, 0, 1000, 3));
            IntPtr t6 = Crea("CreateOverlay", "franjas", franjas);
            Renueva("franjas", t6, franjas);
            IntPtr c1 = Crea("CreateCoverShared", "portada", portada);
            IntPtr c2 = Crea("CreateCoverShared", "portada-otra-vez", portada);
            Console.WriteLine("compartida la misma: " + (c1 == c2));
            DelDispositivo("orillas", orillas);
            DelDispositivo("alfa", alfa);
            DelDispositivo("uno", uno);
            IntPtr t7 = Crea("CreateOverlay", "grande", grande);
            Renueva("grande2", t7, grande2);
            Zona("grande", t7, grande, new Rectangle(1000, 500, 500, 400));
            // en teselas: el limite de textura en 256 (las mas grandes van en trozos con un texel de margen: TexturasDivididasGpu)
            gpu.GetType().GetField("limiteTexturaW", Todo).SetValue(gpu, 256); gpu.GetType().GetField("limiteTexturaH", Todo).SetValue(gpu, 256);
            IntPtr t8 = Crea("CreateOverlay", "grande-teselas", grande);
            Renueva("grande2-teselas", t8, grande2);
            Zona("grande-teselas", t8, grande, new Rectangle(250, 250, 600, 300));
            Zona("grande2-teselas", t8, grande2, new Rectangle(0, 0, 1500, 900));
            IntPtr t9 = Crea("CreateOverlay", "opaco-teselas", opaco);
            Zona("opaco2-teselas", t9, opaco2, new Rectangle(250, 100, 50, 100));
            IntPtr c3 = Crea("CreateCoverShared", "grande-teselas", grande);   // en teselas no se comparte
            IntPtr c4 = Crea("CreateCoverShared", "grande-teselas-otra-vez", grande);
            Console.WriteLine("teselas compartidas: " + (c3 == c4));
            gpu.GetType().GetField("limiteTexturaW", Todo).SetValue(gpu, 0); gpu.GetType().GetField("limiteTexturaH", Todo).SetValue(gpu, 0);

            // ---- 3) el contador de cuadros (Ctrl+F1): EnsureFpsTex en un GpuPanel sin construir, con numeros fijos
            GpuPanel panel = (GpuPanel)RuntimeHelpers.GetUninitializedObject(typeof(GpuPanel));
            Type tp = typeof(GpuPanel);
            tp.GetField("stars", Todo).SetValue(panel, stars);
            FluidezGpu fl = new FluidezGpu(); fl.P95 = 12.345; fl.Maximo = 33.25; fl.Version = 3;
            tp.GetField("fluidez", Todo).SetValue(panel, fl);
            tp.GetField("topeMs", Todo).SetValue(panel, 15);
            tp.GetField("fpsShown", Todo).SetValue(panel, int.MinValue); tp.GetField("fpsFluidezMostrada", Todo).SetValue(panel, -1);
            FieldInfo bounds = null;
            object duenoBounds = panel;
            // (gpupanel-sin-control) en la traduccion el GpuPanel ya no hereda de Control: su sitio esta en su CajaVista (el campo «caja», que
            // un objeto sin construir no tiene: se le pone una nueva); en la referencia, el campo «bounds» de Control
            FieldInfo caja = tp.GetField("caja", Todo);
            if (caja != null) { duenoBounds = Activator.CreateInstance(caja.FieldType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { panel }, null); caja.SetValue(panel, duenoBounds); bounds = caja.FieldType.GetField("bounds", Todo); }
            for (Type t = tp; t != null && bounds == null; t = t.BaseType) bounds = t.GetField("bounds", Todo | BindingFlags.DeclaredOnly);
            MethodInfo fps = tp.GetMethod("EnsureFpsTex", Todo);
            foreach (object[] caso in new[] { new object[] { 0, 57 }, new object[] { 0, 60 }, new object[] { 1024, 1 }, new object[] { 1024, 1234 }, new object[] { 300, 0 } })
            {
                bounds.SetValue(duenoBounds, new Rectangle(0, 0, (int)caso[0], 768));
                tp.GetField("Fps", Todo).SetValue(panel, (int)caso[1]);
                fps.Invoke(panel, null);
                // (sin nombre: al rehacerla, Direct3D da otro puntero y OpenGL vuelve a dar el mismo numero; lo subido lo dice el volcado)
                Console.WriteLine("EnsureFpsTex ancho=" + caso[0] + " fps=" + caso[1] + " -> " + ((IntPtr)tp.GetField("fpsTex", Todo).GetValue(panel) != IntPtr.Zero ? "textura" : "0") + " " + tp.GetField("fpsTW", Todo).GetValue(panel) + "x" + tp.GetField("fpsTH", Todo).GetValue(panel)
                    + " u=" + F((float)tp.GetField("fpsU", Todo).GetValue(panel)) + " v=" + F((float)tp.GetField("fpsV", Todo).GetValue(panel)));
            }
            Console.Error.WriteLine("frontera: " + n + " mapas");
            return 0;
        }
    }
}
