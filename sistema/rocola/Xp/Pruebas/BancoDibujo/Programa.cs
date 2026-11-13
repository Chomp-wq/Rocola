// BANCO DEL DIBUJO (port nativo de XP, pista del dibujo). Hace N casos al azar (Casos.cs) con un ejecutor y escribe una linea por caso:
// numero, clase, la huella de los bytes de la imagen y lo que devolvieron las llamadas. Se compara la salida de dos compilaciones (o de dos
// ejecutores) con compara.sh: tiene que salir igual, linea por linea.
//   dotnet Congelado/bin/Release/net10.0/BancoCongelado.dll casos 3000 semilla 1 salida /ruta/congelado.tsv
//   dotnet Nuevo/bin/Release/net10.0/BancoNuevo.dll casos 3000 semilla 1 salida /ruta/nuevo.tsv [ejecutor nativo]
//   ... vuelca 17,230 carpeta /ruta   (las imagenes de esos casos en PNG, para mirarlas)
//   ... clase texto                   (solo los casos de esa clase: texto, mide, gdi, midegdi, forma, imagen, puntos, lee, guarda,
//                                      recorte, letra, cadena, estado)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkiaSharp;

namespace Banco
{
    internal static class Programa
    {
        internal static string Huella(byte[] b)
        {
            byte[] h = System.Security.Cryptography.SHA256.HashData(b);
            StringBuilder sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
            return sb.ToString();
        }
        private static void Png(byte[] bgraPremul, int w, int h, string ruta)
        {
            using (SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                System.Runtime.InteropServices.Marshal.Copy(bgraPremul, 0, b.GetPixels(), bgraPremul.Length);
                b.NotifyPixelsChanged();
                using (SKData d = b.Encode(SKEncodedImageFormat.Png, 100)) File.WriteAllBytes(ruta, d.ToArray());
            }
        }
        internal static int Main(string[] a)
        {
            int casos = 2000, semilla = 1; string salida = null, clase = null, ejecutor = "gdi", carpeta = ".";
            HashSet<int> vuelca = new HashSet<int>();
            for (int i = 0; i + 1 < a.Length; i += 2)
            {
                switch (a[i])
                {
                    case "casos": casos = int.Parse(a[i + 1]); break;
                    case "semilla": semilla = int.Parse(a[i + 1]); break;
                    case "salida": salida = a[i + 1]; break;
                    case "clase": clase = a[i + 1]; break;
                    case "ejecutor": ejecutor = a[i + 1]; break;
                    case "carpeta": carpeta = a[i + 1]; break;
                    case "vuelca": foreach (string s in a[i + 1].Split(',')) vuelca.Add(int.Parse(s)); break;
                }
            }
            if (Array.IndexOf(a, "puente-objetos") >= 0) { Console.Error.WriteLine("este banco ya no lleva puente-objetos (n7-adelanto: se quito con el ejecutor «puente»)"); return 2; }   // (revision: antes corria callado el banco por defecto)
            List<Caso> l = Casos.Genera(casos, semilla, clase);
            StringBuilder todo = new StringBuilder();
            System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, int> porClase = new Dictionary<string, int>();
            foreach (Caso c in l)
            {
                StringBuilder v = new StringBuilder();
                byte[] px;
                try
                {
#if NATIVO
                    if (ejecutor != "gdi" && ejecutor != "nativo") throw new InvalidOperationException("este banco no lleva el ejecutor " + ejecutor);   // (n7-adelanto: el ejecutor «puente» se quito; antes, lo que no era «nativo» ni «puente» corria como «gdi»)
                    px = ejecutor == "nativo" ? EjecutaNativo.Hace(c, v) : EjecutaGdi.Hace(c, v);
#else
                    if (ejecutor != "gdi") throw new InvalidOperationException("este banco no lleva el ejecutor " + ejecutor);
                    px = EjecutaGdi.Hace(c, v);
#endif
                }
                catch (Exception ex) { px = new byte[0]; v.Append(" FALLO:" + ex.GetType().Name + ":" + ex.Message); }
                todo.Append(c.Num).Append('\t').Append(c.Clase).Append('\t').Append(Huella(px)).Append('\t').Append(v.ToString()).Append('\n');
                int n; porClase.TryGetValue(c.Clase, out n); porClase[c.Clase] = n + 1;
                if (vuelca.Contains(c.Num) && px.Length == c.W * c.H * 4) Png(px, c.W, c.H, Path.Combine(carpeta, "caso" + c.Num + "-" + ejecutor + ".png"));
            }
            if (salida != null) File.WriteAllText(salida, todo.ToString()); else Console.Write(todo.ToString());
            StringBuilder resumen = new StringBuilder();
            foreach (KeyValuePair<string, int> p in porClase) resumen.Append(p.Key).Append('=').Append(p.Value).Append(' ');
            Console.Error.WriteLine("banco (" + ejecutor + "): " + l.Count + " casos en " + reloj.Elapsed.TotalSeconds.ToString("0.0") + " s; " + resumen);
            return 0;
        }
    }
}
