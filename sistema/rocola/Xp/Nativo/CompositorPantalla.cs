// EL COMPOSITOR DE LA PANTALLA (port nativo, tanda 4, grupo pantalla-nativa: W2). Lo que en XP hacia Windows con las ventanas de la rocola
// al ponerlas en la pantalla: en cada cuadro junta las capas, de abajo arriba, y presenta (KMS: el cambio de pagina con el barrido). Viene
// del anfitrion de la transicion (Plataforma/Anfitrion.cs, que ya no compone: le da las capas) sin cambiar nada de lo que hace: el mismo
// programa de OpenGL, la misma mezcla, las mismas texturas que solo se suben cuando la capa cambio, la misma flecha y las mismas capturas.
//
// LAS CAPAS (CapaPantalla), en el orden en que se ven, de abajo arriba:
//   - lo pintado por procesador (un SKBitmap premultiplicado: el fondo de la rocola con las vistas de cada tramo; una hoja nativa): se sube
//     a su textura solo si su Version cambio, y se mezcla con alfa premultiplicado (ONE, ONE_MINUS_SRC_ALPHA) y su opacidad;
//   - lo que pinta la tarjeta (el fondo del dispositivo: Present de Direct3D 9) o el video (libmpv): su textura tal cual, sin mezcla; la
//     de la tarjeta estirada entera a su sitio (Present sin rectangulos), la del video su esquina (RGBA);
// y encima la flecha del raton (cuando XP la ensena) y, para las pruebas, las capturas (sin la flecha, como las de Windows).
// Quien da las capas: hoy el anfitrion de la transicion (las ventanas de la capa, la lista nativa de la ventana -PantallaRocola.Vistas.cs-
// y las hojas nativas); sin la capa, solo la pantalla nativa.
//   ROCOLA_CAPTURA=S:ruta.png[,S2:ruta2.png...]  guarda la pantalla a los S segundos de arrancar; ROCOLA_CAPTURA_SALE=1 cierra despues
//   de la ultima (pruebas). ROCOLA_CAPTURA_CAPAS=1, cada capa aparte; ROCOLA_CAPTURA_TEXTURAS, texturas de la tarjeta; ROCOLA_TRAZA_CAPAS=1,
//   la lista de capas una vez por segundo; ROCOLA_CAPTURA_FLECHA=1, las capturas con la flecha.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Rocola.Pantalla;
using SkiaSharp;

namespace Rocola
{
    // UNA CAPA DE LA PANTALLA
    internal sealed class CapaPantalla
    {
        internal object Clave;              // la misma capa de un cuadro al otro (su textura se guarda y solo se vuelve a subir si cambio)
        internal Rectangle Rect;            // en la pantalla
        internal SKBitmap Mapa;             // lo pintado por procesador (null: una de la tarjeta o del video)
        internal int Version;               // cambia cuando Mapa se repinto
        internal float Opacidad = 1f;
        internal bool DeTextura;            // de la tarjeta o del video: Textura (0: aun sin cuadro, no se pinta)
        internal uint Textura; internal int AnchoGl, AltoGl; internal bool Rgba;
        internal string Tipo;               // (pruebas: ROCOLA_CAPTURA_CAPAS) «dibujo2d», «hoja», «vista»; las de textura, «tarjeta»
        internal string Nombre;             // (pruebas: ROCOLA_TRAZA_CAPAS) de quien es la textura
    }

    internal sealed unsafe class CompositorPantalla
    {
        private readonly ISuperficie sup;
        private readonly uint fboPantalla;
        private readonly int ancho, alto;
        private uint programa; private int lugarM, lugarA, lugarS, lugarRgba;
        private sealed class CapaGl { internal uint Tex; internal int Version = -1, W, H; internal bool Vista; }
        private readonly Dictionary<object, CapaGl> capas = new Dictionary<object, CapaGl>();
        private readonly List<KeyValuePair<double, string>> capturas = new List<KeyValuePair<double, string>>();
        private readonly bool saleTrasCapturas;
        internal int Composiciones;
        private static readonly bool trazaCapas = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_CAPAS") == "1";
        private double ultimaTraza;

        internal CompositorPantalla(ISuperficie s)
        {
            sup = s; ancho = s.Ancho; alto = s.Alto;
            int f = 0; GL.GetIntegerv(GL.FRAMEBUFFER_BINDING, &f);   // KMS: 0 (la ventana); fuera de pantalla: su framebuffer
            fboPantalla = (uint)f;
            string c = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA");
            if (!string.IsNullOrEmpty(c))
                foreach (string e in c.Split(','))
                {
                    int k = e.IndexOf(':'); double seg;
                    if (k > 0 && double.TryParse(e.Substring(0, k), NumberStyles.Float, CultureInfo.InvariantCulture, out seg)) capturas.Add(new KeyValuePair<double, string>(seg * 1000.0, e.Substring(k + 1)));
                }
            capturas.Sort((x, y) => x.Key.CompareTo(y.Key));
            saleTrasCapturas = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_SALE") == "1";
        }

        // ---- componer ----
        private const string FuenteV =
            "attribute vec2 p; attribute vec2 t; uniform vec2 m; varying vec2 vt;\n" +
            "void main() { gl_Position = vec4(p.x * m.x - 1.0, 1.0 - p.y * m.y, 0.0, 1.0); vt = t; }\n";
        // las texturas guardan B, G, R, A (lo pintado por procesador y Direct3D): se leen en el orden de OpenGL; lo pintado ya va premultiplicado
        private const string FuenteF =
            "precision mediump float; varying vec2 vt; uniform sampler2D s; uniform float a; uniform float rgba;\n" +
            "void main() { vec4 c = texture2D(s, vt); gl_FragColor = (rgba > 0.5 ? c : c.bgra) * a; }\n";
        private void Programa()
        {
            if (programa != 0) return;
            uint v = Sombreador(GL.VERTEX_SHADER, FuenteV), f = Sombreador(GL.FRAGMENT_SHADER, FuenteF);
            uint p = GL.CreateProgram();
            GL.AttachShader(p, v); GL.AttachShader(p, f);
            GL.BindAttribLocation(p, 0, "p"); GL.BindAttribLocation(p, 1, "t");
            GL.LinkProgram(p);
            int ok; GL.GetProgramiv(p, GL.LINK_STATUS, &ok);
            if (ok == 0) throw new InvalidOperationException("Anfitrion: el programa de componer no enlaza");
            programa = p;
            lugarM = GL.GetUniformLocation(p, "m"); lugarA = GL.GetUniformLocation(p, "a"); lugarS = GL.GetUniformLocation(p, "s"); lugarRgba = GL.GetUniformLocation(p, "rgba");
        }
        private static uint Sombreador(uint tipo, string fuente)
        {
            uint s = GL.CreateShader(tipo);
            byte[] b = Encoding.ASCII.GetBytes(fuente);
            fixed (byte* pb = b) { byte* q = pb; int n = b.Length; GL.ShaderSource(s, 1, &q, &n); }
            GL.CompileShader(s);
            int ok; GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
            if (ok == 0) throw new InvalidOperationException("Anfitrion: un sombreador no compila");
            return s;
        }

        // UN CUADRO: las capas que da 'dame' (se piden ya con OpenGL puesto para componer, en el hilo de la pantalla), la flecha y presentar
        internal void Compone(Func<List<CapaPantalla>> dame)
        {
            Programa();
            // los videos (Nativo/ReproductorVideo.cs): libmpv pinta su cuadro nuevo en su textura antes de componer
            ReproductorVideo[] rs = ReproductorVideo.Vivos();
            foreach (ReproductorVideo r in rs) { try { r.Pinta(); } catch (Exception ex) { PantallaRocola.Falla(ex); } }
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GL.Viewport(0, 0, ancho, alto);
            GlMas.ColorMask(1, 1, 1, 1);
            GL.Disable(GL.SCISSOR_TEST);
            GL.ClearColor(0, 0, 0, 1); GL.Clear(GL.COLOR_BUFFER_BIT);
            GL.UseProgram(programa);
            GL.Uniform2f(lugarM, 2f / ancho, 2f / alto);
            GL.Uniform1i(lugarS, 0); GL.Uniform1f(lugarRgba, 0f);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.BindBuffer(GL.ARRAY_BUFFER, 0);
            foreach (CapaGl c in capas.Values) c.Vista = false;
            List<CapaPantalla> lista = dame();
            if (trazaCapas && PantallaRocola.AhoraMs - ultimaTraza > 1000)   // (pruebas) ROCOLA_TRAZA_CAPAS=1: las capas, una vez por segundo
            {
                ultimaTraza = PantallaRocola.AhoraMs;
                StringBuilder sb = new StringBuilder("capas " + (PantallaRocola.AhoraMs / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s:");
                foreach (CapaPantalla c in lista) sb.Append(' ').Append(c.DeTextura ? (c.Nombre + "#" + c.Textura + (c.Rgba ? "rgba" : "")) : "dibujo").Append('@').Append(c.Rect.X).Append(',').Append(c.Rect.Y).Append(',').Append(c.Rect.Width).Append('x').Append(c.Rect.Height);
                Console.WriteLine(sb.ToString());
            }
            foreach (CapaPantalla c in lista)
            {
                if (c.DeTextura)
                {
                    if (c.Textura == 0 || c.AnchoGl <= 0) continue;
                    GL.Disable(GL.BLEND);
                    GL.BindTexture(GL.TEXTURE_2D, c.Textura);
                    GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                    GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
                    // el fondo de la tarjeta (Direct3D 9): Present sin rectangulos lo estira entero a su sitio (si aun no tiene su medida
                    // -el cuadro de un cambio de tamano, antes del Reset de XP-, estirado, como en XP); el video, la esquina de su textura
                    if (c.Rgba) GL.Uniform1f(lugarRgba, 1f);
                    if (c.Rgba) Cuadro(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height, Math.Min(1f, (float)c.Rect.Width / c.AnchoGl), Math.Min(1f, (float)c.Rect.Height / c.AltoGl), 1f);
                    else Cuadro(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height, 1f, 1f, 1f);
                    if (c.Rgba) GL.Uniform1f(lugarRgba, 0f);
                    continue;
                }
                SKBitmap mapa = c.Mapa;
                if (mapa == null) continue;
                CapaGl g;
                if (!capas.TryGetValue(c.Clave, out g)) { g = new CapaGl(); capas[c.Clave] = g; }
                g.Vista = true;
                if (g.Version != c.Version || g.W != mapa.Width || g.H != mapa.Height) Sube(mapa, g);
                g.Version = c.Version;
                GL.Enable(GL.BLEND); GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
                GL.BindTexture(GL.TEXTURE_2D, g.Tex);
                Cuadro(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height, 1f, 1f, c.Opacidad);
            }
            // las capas que ya no estan, fuera
            List<object> fuera = null;
            foreach (KeyValuePair<object, CapaGl> k in capas) if (!k.Value.Vista) { if (fuera == null) fuera = new List<object>(); fuera.Add(k.Key); }
            if (fuera != null) foreach (object c in fuera) { uint t = capas[c].Tex; if (t != 0) GL.DeleteTextures(1, &t); capas.Remove(c); }
            GL.Disable(GL.BLEND);
            Composiciones++;
            if (conFlecha) { Flecha(); Capturas(lista); }   // (pruebas: ROCOLA_CAPTURA_FLECHA=1)
            else { Capturas(lista); Flecha(); }   // como las capturas de Windows: sin la flecha
            sup.Presenta();
            foreach (ReproductorVideo r in rs) r.TrasPresentar();
            PantallaTarjeta.Tocada();   // lo puesto en OpenGL ya no es lo del dispositivo de la tarjeta (Nativo/PantallaTarjeta.cs)
        }
        // LA FLECHA DEL RATON, encima de todo cuando XP la ensena (Cursor.Show; en la pantalla de la rocola XP la esconde): la flecha o la
        // mano (Cursors.Hand), dibujadas una vez como las de Windows XP (blanca con borde negro; la mano, con el dedo arriba).
        private uint texFlecha, texMano;
        private static readonly bool conFlecha = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_FLECHA") == "1";
        private void Flecha()
        {
            if (!PantallaRocola.FlechaVisible) return;
            bool mano = Hoja.ManoBajoRaton;   // (la mano de las piezas de una hoja nativa: Cursors.Hand de lo que esta bajo el raton; las vistas de la rocola no la usan)
            if (texFlecha == 0) { texFlecha = HazCursor(false); texMano = HazCursor(true); }
            Point p = PantallaRocola.Flecha;
            GL.UseProgram(programa);
            GL.Uniform2f(lugarM, 2f / ancho, 2f / alto); GL.Uniform1i(lugarS, 0);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.Enable(GL.BLEND); GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            GL.BindTexture(GL.TEXTURE_2D, mano ? texMano : texFlecha);
            if (mano) Cuadro(p.X - 6, p.Y, 32, 32, 1f, 1f, 1f); else Cuadro(p.X, p.Y, 32, 32, 1f, 1f, 1f);
            GL.Disable(GL.BLEND);
        }
        private uint HazCursor(bool mano)
        {
            using (SKBitmap b = new SKBitmap(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul)))
            using (SKCanvas c = new SKCanvas(b))
            using (SKPath r = new SKPath())
            using (SKPaint relleno = new SKPaint { Color = SKColors.White, IsAntialias = false, Style = SKPaintStyle.Fill })
            using (SKPaint borde = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
            {
                c.Clear(SKColors.Transparent);
                if (!mano)
                {
                    r.MoveTo(0.5f, 0.5f); r.LineTo(0.5f, 16.5f); r.LineTo(4.5f, 12.5f); r.LineTo(7.5f, 18.5f); r.LineTo(9.5f, 17.5f); r.LineTo(6.5f, 11.5f); r.LineTo(11.5f, 11.5f); r.Close();
                }
                else
                {
                    r.MoveTo(5.5f, 0.5f); r.LineTo(8.5f, 0.5f); r.LineTo(8.5f, 7.5f); r.LineTo(16.5f, 8.5f); r.LineTo(17.5f, 10.5f); r.LineTo(16.5f, 16.5f);
                    r.LineTo(13.5f, 21.5f); r.LineTo(6.5f, 21.5f); r.LineTo(1.5f, 14.5f); r.LineTo(1.5f, 11.5f); r.LineTo(3.5f, 11.5f); r.LineTo(5.5f, 13.5f); r.Close();
                }
                c.DrawPath(r, relleno); c.DrawPath(r, borde);
                c.Flush();
                uint t; GL.GenTextures(1, &t);
                GL.BindTexture(GL.TEXTURE_2D, t);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
                GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, 32, 32, 0, GL.RGBA, GL.UNSIGNED_BYTE, (void*)b.GetPixels());
                return t;
            }
        }
        private void Sube(SKBitmap sk, CapaGl g)
        {
            if (g.Tex == 0)
            {
                uint t; GL.GenTextures(1, &t); g.Tex = t;
                GL.BindTexture(GL.TEXTURE_2D, t);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GlMas.NEAREST); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GlMas.NEAREST);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE); GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
            }
            else GL.BindTexture(GL.TEXTURE_2D, g.Tex);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            if (sk.RowBytes == sk.Width * 4)
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, sk.Width, sk.Height, 0, GL.RGBA, GL.UNSIGNED_BYTE, (void*)sk.GetPixels());
            else
            {
                GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, sk.Width, sk.Height, 0, GL.RGBA, GL.UNSIGNED_BYTE, null);
                byte* p = (byte*)sk.GetPixels();
                for (int y = 0; y < sk.Height; y++) GL.TexSubImage2D(GL.TEXTURE_2D, 0, 0, y, sk.Width, 1, GL.RGBA, GL.UNSIGNED_BYTE, p + (long)y * sk.RowBytes);
            }
            g.W = sk.Width; g.H = sk.Height;
        }
        private void Cuadro(float x, float y, float w, float h, float u1, float v1, float a)
        {
            float* v = stackalloc float[] { x, y, 0, 0, x + w, y, u1, 0, x, y + h, 0, v1, x + w, y + h, u1, v1 };
            GL.Uniform1f(lugarA, a);
            GL.VertexAttribPointer(0, 2, GL.FLOAT, 0, 16, (IntPtr)v);
            GL.VertexAttribPointer(1, 2, GL.FLOAT, 0, 16, (IntPtr)(v + 2));
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1);
            GlMas.DisableVertexAttribArray(2); GlMas.DisableVertexAttribArray(3);
            GL.DrawArrays(GlMas.TRIANGLE_STRIP, 0, 4);
        }

        // ---- capturas (pruebas) ----
        private void Capturas(List<CapaPantalla> lista)
        {
            if (capturas.Count == 0) return;
            double t = PantallaRocola.AhoraMs;
            if (t < capturas[0].Key) return;
            string ruta = capturas[0].Value;
            capturas.RemoveAt(0);
            GuardaPng(ruta);
            string tx = Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_TEXTURAS");   // (pruebas) "nombre:AxB,..." texturas de OpenGL tal cual estan en la tarjeta
            if (!string.IsNullOrEmpty(tx))
                foreach (string e in tx.Split(','))
                {
                    string[] p = e.Split(':'); if (p.Length != 2) continue; string[] m = p[1].Split('x');
                    GuardaTextura(uint.Parse(p[0]), int.Parse(m[0]), int.Parse(m[1]), ruta + ".tex" + p[0] + ".png");
                }
            if (Environment.GetEnvironmentVariable("ROCOLA_CAPTURA_CAPAS") == "1")
            {
                // (pruebas) cada capa por separado: las de textura (tal cual en la tarjeta) y las pintadas por procesador (la de una ventana de
                // la capa, «dibujo2d»; la de una hoja nativa, «hoja»; un tramo de la lista nativa de la ventana, «vista»)
                int i = 0;
                foreach (CapaPantalla c in lista)
                {
                    i++;
                    if (c.DeTextura) { if (c.Textura != 0) GuardaTextura(c.Textura, c.AnchoGl, c.AltoGl, ruta + ".capa" + i + "-tarjeta.png"); }
                    else if (c.Mapa != null) { try { Rocola.Nativo.Imagenes.GuardaPng(c.Mapa, ruta + ".capa" + i + "-" + c.Tipo + "-" + c.Rect.X + "," + c.Rect.Y + "-" + c.Rect.Width + "x" + c.Rect.Height + ".png"); } catch { } }
                }
            }
            Console.WriteLine("Captura: " + ruta + " (" + (t / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s, " + Composiciones + " composiciones)");
            if (capturas.Count == 0 && saleTrasCapturas) Environment.Exit(0);
        }
        private void GuardaTextura(uint tex, int w, int h, string ruta)
        {
            uint f; GL.GenFramebuffers(1, &f);
            GL.BindFramebuffer(GL.FRAMEBUFFER, f);
            GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, tex, 0);
            byte[] px = new byte[w * h * 4];
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            fixed (byte* p = px) GL.ReadPixels(0, 0, w, h, GL.RGBA, GL.UNSIGNED_BYTE, p);
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GlMas.DeleteFramebuffers(1, &f);
            using (SKBitmap b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul)))   // la tarjeta guarda B, G, R, A, fila 0 arriba
            {
                Marshal.Copy(px, 0, b.GetPixels(), px.Length);
                using (SKData data = b.Encode(SKEncodedImageFormat.Png, 100)) using (System.IO.FileStream fs = System.IO.File.Create(ruta)) data.SaveTo(fs);
            }
        }
        internal void GuardaPng(string ruta)
        {
            byte[] px = new byte[ancho * alto * 4];
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            fixed (byte* p = px) GL.ReadPixels(0, 0, ancho, alto, GL.RGBA, GL.UNSIGNED_BYTE, p);
            using (SKBitmap b = new SKBitmap(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Opaque)))
            {
                byte* d = (byte*)b.GetPixels();
                for (int y = 0; y < alto; y++)   // OpenGL lee de abajo arriba
                    Marshal.Copy(px, (alto - 1 - y) * ancho * 4, (IntPtr)(d + (long)y * b.RowBytes), ancho * 4);
                using (SKData data = b.Encode(SKEncodedImageFormat.Png, 100))
                using (System.IO.FileStream f = System.IO.File.Create(ruta)) data.SaveTo(f);
            }
        }
        // (pruebas) lee un punto de lo que quedo compuesto en la pantalla, como 0xAARRGGBB
        internal uint PuntoEn(int x, int y)
        {
            byte* p = stackalloc byte[4];
            GL.BindFramebuffer(GL.FRAMEBUFFER, fboPantalla);
            GL.PixelStorei(GL.PACK_ALIGNMENT, 4);
            GL.ReadPixels(x, alto - 1 - y, 1, 1, GL.RGBA, GL.UNSIGNED_BYTE, p);
            return (uint)(p[2] | (p[1] << 8) | (p[0] << 16)) | ((uint)p[3] << 24);
        }
    }
}
