// ARNES DEL GRUPO «VIDEO-FORM1» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que dibujan Form1.Video.cs (DrawLabelOverlay, las etiquetas en la
// imagen de la interfaz; DrawCtrlOverlay, las vistas en ella; EnSeco, el precalentado; PartirNow) y Form1.cs (la barra de ayuda, HelpBar: su
// dibujo con tarjeta y por procesador, y su ancho) con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con el
// programa de una carpeta Xp (VistasVideoForm1.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales
// linea por linea.
//   - Las llamadas de la referencia son las de XP (Graphics, Font). En la traduccion se llaman como las llama la traduccion: con el lienzo y el
//     estado del que llama en variables, y una Letra en vez de la Font; la barra de ayuda tambien DIRECTO.
//   - El estado del Graphics del que llama va de ocho maneras (suavizado, los modos de letra, copia, medio pixel, traslacion entera y con
//     decimales, escala, giro y recorte); despues de cada llamada se apunta el estado que le queda al Graphics (en la traduccion no se toca:
//     tiene que ser el mismo que deja el Restore de XP).
//   - El texto de GDI (TextRenderer en la referencia, Nativo/TextoGdi.cs en la traduccion): con la matriz del HDC.
//   - EnSeco: en la referencia con un Action<Graphics>; en la traduccion, el traducido (Action<SKCanvas, bool, AjusteLetra>), y a lo que
//     dibuja se le da un lienzo de prueba sobre ese lienzo con su estado.
//   - Lo privado va por reflexion.
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+ ni la de WinForms: los mapas, el lienzo, las letras, los rotulos y el
//     OnPaint de la referencia van por Pruebas/Comun (Pru, Lz, Fte: la capa en la referencia congelada y lo nativo en el programa nuevo).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasVideoForm1.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoVideoForm1
{
    internal static class VideoForm1
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas;
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF r) { return "(" + R(r.X) + "," + R(r.Y) + "," + R(r.Width) + "," + R(r.Height) + ")"; }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Exc(Exception x) { return "EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }

        // los puntos de un mapa: tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, object b)
        {
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }
        private static string Mat(PointF[] p) { return R(p[0].X) + "," + R(p[0].Y) + ";" + R(p[1].X) + "," + R(p[1].Y); }   // ((0,0) y (1,1) pasados por la transformacion)
        // el estado que le queda al lienzo (lo que XP deja puesto en el Graphics despues de una llamada)
        private static string EstadoG(Lz g)
        {
            return "s=" + g.Suavizado + " t=" + g.Letra + " c=" + g.Copia + " i=" + g.Filtro + " p=" + g.Pixel
                + " m=" + Mat(g.Transformados()) + " r=" + R(g.LimitesRecorte);
        }

        // ---- el estado del Graphics del que llama (los ocho de Pruebas/VistasReposo y VistasAsistente)
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "sin-suavizar", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = ModoSuave.AntiAlias, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, Trans = 2 },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, T = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.None, Trans = 5 },
        };
        private static Lz Prepara(object b, Estado e)
        {
            Lz g = Pru.Lienzo(b);
            g.Limpia(Color.FromArgb(120, 30, 60, 90));
            g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 25, Pru.Sk(b).Width, 9);
            g.Suavizado = e.S; g.Letra = e.T; g.Copia = e.C; g.Pixel = e.P;
            if (e.Trans == 1) g.Traslada(3.5f, 2.25f); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(7f);
            else if (e.Trans == 4) g.Traslada(0, -12); else if (e.Trans == 5) g.Traslada(0.5f, 0f);
            if (e.Recorte) g.Recorta(new RectangleF(10.5f, 6, 220, 50));
            return g;
        }

        // ---- la traduccion, por reflexion
        private static MethodInfo Metodo(Type t, string nombre, Type primero, int parametros = -1)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (m.Name == nombre && m.GetParameters().Length > 0 && (primero == null || m.GetParameters()[0].ParameterType == primero) && (parametros < 0 || m.GetParameters().Length == parametros)) return m;
            return null;
        }
        // el lienzo y su estado (lienzo, suave, letra, copia, filtro, medio), como los lleva en sus variables un archivo traducido
        private static object[] EstadoDe(Lz g)
        {
            object[] e = g.Estado();
            return new[] { e[0], e[1], e[2], e[3], e[4], e[5] };
        }
        private static object[] Junta(object[] a, params object[] b) { object[] r = new object[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
        private static Type Anidado(string nombre) { return typeof(Form1).GetNestedType(nombre, Todo); }
        // (rotulos-y-letras) la traduccion pide una Letra donde la referencia pide la Font (las vistas y los rotulos: una letra del arnes, Fte, va
        // por la suya) y AlineaTexto donde pide ContentAlignment (el mismo numero); el rotulo de la traduccion es RotuloVista
        private static object Adapta(Type destino, object v)
        {
            if (v == null || destino.IsInstanceOfType(v)) return v;
            if (v is Fte) return ((Fte)v).Obj;
            if (v is int && destino.IsEnum) return Enum.ToObject(destino, (int)v);
            return v;
        }
        private static readonly Type tRotulo = typeof(Form1).Assembly.GetType("Rocola.RotuloVista");
        private static object NuevoRotulo() { return tRotulo != null ? Activator.CreateInstance(tRotulo) : Pru.Rotulo(); }
        private static object LeeP(object o, string p) { return o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance).GetValue(o); }
        private static void Pon(object o, string campo, object v)
        {
            Type t = o.GetType();
            while (t != null)
            {
                FieldInfo f = t.GetField(campo, Todo | BindingFlags.DeclaredOnly); if (f != null) { f.SetValue(o, v); return; }
                PropertyInfo p = t.GetProperty(campo, Todo | BindingFlags.DeclaredOnly); if (p != null && p.CanWrite) { p.SetValue(o, Adapta(p.PropertyType, v)); return; }
                t = t.BaseType;
            }
            throw new Exception("no hay " + campo);
        }

        // ================================================================ LAS ETIQUETAS (DrawLabelOverlay)
        private static void Etiquetas()
        {
            MethodInfo trad4 = Metodo(typeof(Form1), "DrawLabelOverlay", typeof(SKCanvas), 7), trad2 = Metodo(typeof(Form1), "DrawLabelOverlay", typeof(SKCanvas), 5);
            MethodInfo xp4 = Pru.Capa ? Metodo(typeof(Form1), "DrawLabelOverlay", Pru.TLienzo, 4) : null, xp2 = Pru.Capa ? Metodo(typeof(Form1), "DrawLabelOverlay", Pru.TLienzo, 2) : null;
            bool trad = trad4 != null;
            // (los numeros de ContentAlignment, que son los de AlineaTexto: arriba 1, 2 y 4; en medio 16, 32 y 64; abajo 256, 512 y 1024)
            int[] alin = { 1, 2, 4, 16, 32, 64, 256, 512, 1024 };
            string[] textos = { "Reproduciendo: Canción de prueba", "Ángel Ñandú y la orquesta que no cabe nunca en su renglón", "0:07 / 2:30", "", "A&B — «x»" };
            Fte[] letras = { Fte.Generica(9.5f, 1), Fte.Generica(13f, 1, "Pixel"), new Fte("Microsoft Sans Serif", 11.5f, 1) };   // (1: negrita)
            int k = 0;
            foreach (Estado e in Estados)
                for (int a = 0; a < alin.Length; a++)
                    for (int t = 0; t < textos.Length; t++)
                    {
                        if ((a + t) % 2 == 1 && t != 1) continue;   // (no todas las combinaciones: unas 30 por estado)
                        object l = NuevoRotulo(); Pon(l, "Text", textos[t]); Pon(l, "Font", letras[(a + t) % letras.Length]); Pon(l, "ForeColor", Color.FromArgb(255, 255, 220, 120)); Pon(l, "TextAlign", alin[a]);   // (rotulos-y-letras: Label o RotuloVista)
                        Pon(l, "Bounds", new Rectangle(6 + a, 4 + t, 150 + 9 * t, 22 + a));
                        int caso = k++ % 4;   // 0: la etiqueta (2 argumentos); 1: con color; 2: caja angosta; 3: caja sin ancho
                        Rectangle lb = (Rectangle)LeeP(l, "Bounds");
                        Color color = caso == 0 ? (Color)LeeP(l, "ForeColor") : Color.FromArgb(200, 120, 230, 255);
                        RectangleF caja = caso == 2 ? new RectangleF(lb.Left + 0.5f, lb.Top, 61.25f, lb.Height) : caso == 3 ? new RectangleF(lb.Left, lb.Top, 0f, lb.Height) : (RectangleF)lb;
                        string nombre = "etiqueta-" + e.Nombre + "-" + a + "-" + t + "-" + caso;
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(240, 64, "Format32bppPArgb"))
                        {
                            string ex = "", st;
                            using (Lz g = Prepara(b, e))
                            {
                                try
                                {
                                    if (trad)
                                    {
                                        object[] s = EstadoDe(g);   // (lienzo, suave, letra, copia, filtro, medio): DrawLabelOverlay pide el lienzo, la letra, la copia y el medio
                                        if (caso == 0) trad2.Invoke(null, new[] { s[0], s[2], s[3], s[5], l });
                                        else trad4.Invoke(null, new[] { s[0], s[2], s[3], s[5], l, color, caja });
                                    }
                                    else if (caso == 0) xp2.Invoke(null, new object[] { g.Objeto, l });
                                    else xp4.Invoke(null, new object[] { g.Objeto, l, color, caja });
                                }
                                catch (Exception x) { ex = " " + Exc(x); }
                                st = EstadoG(g);
                            }
                            Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                        }
                    }
            foreach (Fte f in letras) f.Dispose();
        }

        // ================================================================ LAS VISTAS EN LA IMAGEN DE LA INTERFAZ (DrawCtrlOverlay)
        // (tanda 6, armado-vistas: la barra de ayuda ya no hereda de Control en la traduccion -una IVista-: las vistas van como object, con su
        // sitio por reflexion; la letra solo a las que son Control, y la barra no la lee: en la referencia se la pone igual que antes)
        private static object Vista(int cual, int w, int h)
        {
            object c;
            switch (cual)
            {
                case 0: c = Activator.CreateInstance(Anidado("HelpBar"), true); break;
                case 1: c = Activator.CreateInstance(Anidado("HelpBar"), true); Pon(c, "Justa", true); break;
                case 2: c = Activator.CreateInstance(Anidado("ProgressView"), true); Pon(c, "Value", 0.37); Pon(c, "Accent", Color.FromArgb(240, 120, 40)); break;
                case 3: c = Activator.CreateInstance(Anidado("ProgressView"), true); Pon(c, "Value", 0.81); Pon(c, "Neon", true); break;
                case 4: c = Activator.CreateInstance(Anidado("GeneroView"), true); Pon(c, "Texto", "Cumbia"); Pon(c, "Tinte", Color.FromArgb(60, 200, 120)); break;
                case 5: c = Activator.CreateInstance(Anidado("LetterBar"), true); break;
                default: c = Activator.CreateInstance(Anidado("PastillaView"), true); Pon(c, "Etiqueta", "CRÉDITOS"); Pon(c, "Valor", "12"); break;
            }
            // (arreglo-junta14, 12-11-2026) desde vistas-disco las siete vistas del disco tampoco heredan de Control en la traduccion: la letra se le
            // pone por reflexion a la que tiene la suya (LetterBar, PastillaView, GeneroView: en la referencia, todas son Control y la reciben).
            // Sin esto GeneroView se media y pintaba con la ambiental (Control.DefaultFont) y no con esta: las 24 lineas vista-*-4-* distintas
            if (c.GetType().GetProperty("Font", BindingFlags.Public | BindingFlags.Instance) != null) Pon(c, "Font", Fte.Generica(10f, 1));   // (rotulos-y-letras: la vista sin herencia con Font, la misma letra que en la referencia)
            Pon(c, "Bounds", new Rectangle(7 + cual, 3 + cual % 3, w, h));
            return c;
        }
        private static void Vistas()
        {
            MethodInfo trad = Metodo(typeof(Form1), "DrawCtrlOverlay", typeof(SKCanvas)), xp = Pru.Capa ? Metodo(typeof(Form1), "DrawCtrlOverlay", Pru.TLienzo) : null;
            int[][] medidas = { new[] { 300, 30 }, new[] { 220, 24 }, new[] { 410, 40 } };
            foreach (Estado e in Estados)
                for (int cual = 0; cual <= 6; cual++)
                    foreach (int[] m in medidas)
                    {
                        string nombre = "vista-" + e.Nombre + "-" + cual + "-" + m[0] + "x" + m[1];
                        object c = null; string ex = "", st = "";
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(440, 64, "Format32bppPArgb"))
                        {
                            try
                            {
                                c = Vista(cual, m[0], m[1]);
                                using (Lz g = Prepara(b, e))
                                {
                                    try
                                    {
                                        if (trad != null) trad.Invoke(null, Junta(EstadoDe(g), c));
                                        else xp.Invoke(null, new object[] { g.Objeto, c });
                                    }
                                    catch (Exception x) { ex = " " + Exc(x); }
                                    st = EstadoG(g);
                                }
                            }
                            catch (Exception x) { ex += " vista " + Exc(x); }
                            Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                        }
                    }
        }

        // ================================================================ LA BARRA DE AYUDA: por procesador (OnPaint, con un Graphics), directo, el halo y su ancho
        private static void Ayuda()
        {
            Type hb = Anidado("HelpBar");
            MethodInfo xp = Pru.Capa ? hb.GetMethod("RenderContent", Todo, null, new[] { Pru.TLienzo, typeof(bool) }, null) : null;
            MethodInfo trad = Metodo(hb, "RenderContent", typeof(SKCanvas), 5);
            // (tanda 6, armado-vistas) la firma de XP con un Graphics ya no esta en la traduccion (era el puente de su OnPaint): ahi se llama a la
            // traducida como lo hacia ese puente -el lienzo y el estado del Graphics, y despues le deja el suavizado en AntiAlias, como XP-
            bool sinPuente = xp == null;
            foreach (Estado e in Estados)
                foreach (bool justa in new[] { false, true })
                    foreach (bool forGpu in new[] { false, true })
                        foreach (int alto in new[] { 18, 28, 37 })
                        {
                            object c = Activator.CreateInstance(hb, true); Pon(c, "Justa", justa); Pon(c, "Bounds", new Rectangle(0, 0, 330, alto));
                            string nombre = "ayuda-" + e.Nombre + "-" + justa + "-" + forGpu + "-" + alto;
                            // la firma de XP (la que llama su OnPaint): en la traduccion, por el puente
                            using (IDisposable b = (IDisposable)Pru.NuevoMapa(360, 48, "Format32bppPArgb"))
                            {
                                string ex = "", st;
                                using (Lz g = Prepara(b, e))
                                {
                                    try
                                    {
                                        if (!sinPuente) xp.Invoke(c, new object[] { g.Objeto, forGpu });
                                        else { object[] s = EstadoDe(g); trad.Invoke(c, new[] { s[0], s[2], s[3], s[5], (object)forGpu }); g.Suavizado = ModoSuave.AntiAlias; }
                                    }
                                    catch (Exception x) { ex = " " + Exc(x); }
                                    st = EstadoG(g);
                                }
                                Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                            }
                            // y directo, como la llama DrawCtrlOverlay traducida (en la referencia, la de XP otra vez)
                            using (IDisposable b = (IDisposable)Pru.NuevoMapa(360, 48, "Format32bppPArgb"))
                            {
                                string ex = "", st;
                                using (Lz g = Prepara(b, e))
                                {
                                    try
                                    {
                                        if (trad != null) { object[] s = EstadoDe(g); trad.Invoke(c, new[] { s[0], s[2], s[3], s[5], (object)forGpu }); directas++; g.Suavizado = ModoSuave.AntiAlias; }   // (lo que deja la de XP: el suavizado)
                                        else xp.Invoke(c, new object[] { g.Objeto, forGpu });
                                    }
                                    catch (Exception x) { ex = " " + Exc(x); }
                                    st = EstadoG(g);
                                }
                                Linea(nombre + "-directo", Puntos(nombre + "-directo", b) + ex + " " + st);
                            }
                        }
            // (tanda 6, armado-vistas) SU PINTURA POR PROCESADOR: en XP OnPaint(PaintEventArgs) con el Graphics de un WM_PAINT (el estado de
            // fabrica: Graphics.FromImage; la capa lo da asi, trasladado a su sitio y recortado a el); en la traduccion, Pinta(SKCanvas) (IVista),
            // que pinta su hueco o la pantalla, en el lienzo de ese mismo Graphics
            MethodInfo onPaint = Pru.Capa ? Metodo(hb, "OnPaint", null, 1) : null, pinta = hb.GetMethod("Pinta", Todo, null, new[] { typeof(SKCanvas) }, null);   // (OnPaint(PaintEventArgs): solo la referencia)
            foreach (bool justa in new[] { false, true })
                foreach (int alto in new[] { 18, 28, 37 })
                    foreach (int sitio in new[] { 0, 1 })
                    {
                        object c = Activator.CreateInstance(hb, true); Pon(c, "Justa", justa); Pon(c, "Bounds", new Rectangle(0, 0, 330, alto));
                        string nombre = "ayuda-pinta-" + justa + "-" + alto + "-" + sitio;
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(360, 48, "Format32bppPArgb"))
                        {
                            string ex = "";
                            using (Lz g = Pru.Lienzo(b))
                            {
                                g.Limpia(Color.FromArgb(120, 30, 60, 90));
                                if (sitio == 1) { g.Traslada(9, 4); g.Intersecta(new Rectangle(0, 0, 330, alto)); }   // (como Escritorio.PintaArbol)
                                try
                                {
                                    if (pinta != null) { pinta.Invoke(c, new[] { g.Estado()[0] }); directas++; }
                                    else Pru.OnPaint(onPaint, c, g, new Rectangle(0, 0, 330, alto));
                                }
                                catch (Exception x) { ex = " " + Exc(x); }
                            }
                            Linea(nombre, Puntos(nombre, b) + ex);
                        }
                    }
            // el halo del boton pulsado con niveles fijos (el de la barra depende del reloj): en la traduccion la traducida de Form1.Pared.cs, directo,
            // como la llama la barra desde la tanda 4; en la referencia la de XP con el Graphics (por su primer parametro: GetMethod por el nombre
            // solo puede ser ambiguo)
            MethodInfo glowXp = Pru.Capa ? Metodo(typeof(Form1), "GlowAround", Pru.TLienzo) : null, glowTr = Metodo(typeof(Form1), "GlowAround", typeof(SKCanvas));
            float[] niveles = { 0f, 0.02f, 0.03f, 0.5f, 1f };
            foreach (Estado e in Estados)
                foreach (float lv in niveles)
                {
                    string nombre = "halo-" + e.Nombre + "-" + R(lv);
                    using (IDisposable b = (IDisposable)Pru.NuevoMapa(120, 64, "Format32bppPArgb"))
                    {
                        string ex = "", st;
                        using (Lz g = Prepara(b, e))
                        {
                            try
                            {
                                if (glowTr != null) { object[] s = EstadoDe(g); glowTr.Invoke(null, new[] { s[0], s[1], s[2], s[3], s[5], 20.5f, 30f, 24f, lv, (object)Color.FromArgb(242, 206, 72) }); }
                                else glowXp.Invoke(null, new object[] { g.Objeto, 20.5f, 30f, 24f, lv, Color.FromArgb(242, 206, 72) });
                            }
                            catch (Exception x) { ex = " " + Exc(x); }
                            st = EstadoG(g);
                        }
                        Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                    }
                }
            // el ancho de la barra (HelpBar.Ancho y AnchoJusta: mide con un Graphics de usar y tirar)
            MethodInfo ancho = hb.GetMethod("Ancho", Todo), anchoJ = hb.GetMethod("AnchoJusta", Todo);
            StringBuilder sb = new StringBuilder();
            for (int alto = 6; alto <= 64; alto += 3) sb.Append(alto).Append(':').Append(ancho.Invoke(null, new object[] { alto })).Append('/').Append(anchoJ.Invoke(null, new object[] { alto })).Append(' ');
            Linea("ayuda-anchos", sb.ToString());
        }

        // ================================================================ PartirNow (nadie la llama, tampoco en XP): con la Font en XP, con la Letra en la traduccion
        private static void Partir()
        {
            MethodInfo m = typeof(Form1).GetMethod("PartirNow", Todo);
            string[] textos = { "", "Corto", "Una cancion con un titulo bastante largo que no cabe", "Supercalifragilisticoespialidosoyalgomasquenocabe", "Ángel Ñandú — «la de siempre» & más", "a b c d e f g h i j k l m n o p" };
            Fte[] letras = { Fte.Generica(11.5f, 1), new Fte("Microsoft Sans Serif", 13f, 0, "Pixel") };
            foreach (Fte f in letras)
                foreach (string t in textos)
                    foreach (int[] w in new[] { new[] { 300, 120 }, new[] { 180, 60 }, new[] { 90, 19 }, new[] { 60, 25 }, new[] { 1000, 400 } })
                    {
                        object[] a = { t, f.Obj, w[0], w[1], null, null };   // (la Font en la referencia, la Letra en la traduccion)
                        string ex = "";
                        try { m.Invoke(null, a); } catch (Exception x) { ex = " " + Exc(x); }
                        Linea("partir-" + f.Tamano + "-" + t.Length + "-" + w[0] + "x" + w[1], "[" + a[4] + "] [" + a[5] + "]" + ex);
                    }
        }

        // ================================================================ EnSeco (el precalentado): el estado del Graphics que le da a lo que llama
        private static void Seco()
        {
            object form = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            // (tanda 6, armado-vistas) en la traduccion la vista va como IVista (la barra de ayuda, que ya no es Control: EnSeco solo lee la
            // medida); en la referencia, un Label como antes
            MethodInfo m = null;
            foreach (MethodInfo mi in typeof(Form1).GetMethods(Todo))
                if (Pru.Capa && mi.Name == "EnSeco" && mi.GetParameters().Length == 2 && mi.GetParameters()[1].ParameterType == typeof(Action<>).MakeGenericType(Pru.TLienzo)) m = mi;
            // (vistas-pared) en la traduccion, la traducida (Action<SKCanvas, bool, AjusteLetra>)
            MethodInfo tr = null;
            if (m == null) foreach (MethodInfo mi in typeof(Form1).GetMethods(Todo)) if (mi.Name == "EnSeco" && mi.GetParameters().Length == 2) tr = mi;
            bool conControl = Pru.Capa && (m ?? tr).GetParameters()[0].ParameterType.Name == "Control";   // (un Control de WinForms: solo la referencia)
            int[][] medidas = { new[] { 7, 40 }, new[] { 40, 7 }, new[] { 8, 8 }, new[] { 333, 121 }, new[] { 1024, 600 } };
            foreach (int[] md in medidas)
            {
                object c = conControl ? Pru.Rotulo() : Activator.CreateInstance(Anidado("HelpBar"), true); Pon(c, "Bounds", new Rectangle(3, 4, md[0], md[1]));
                string visto = "nada";
                Action<Lz> dibuja = delegate (Lz g)
                {
                    using (Fte f = Fte.Generica(9.5f, 1))
                    {
                        SizeF s = g.Mide("Entrar / Poner", f);
                        visto = EstadoG(g) + " mide=" + R(s.Width) + "x" + R(s.Height) + " visible=" + R(g.LimitesVisibles);
                        g.RellenaOvalo(Color.Red, 1, 1, 5, 5); g.EscribeEn("x", f, Color.Red, 0, 0);
                    }
                };
                object r = m != null ? m.Invoke(form, new object[] { c, Pru.AccionLienzo(dibuja) }) : tr.Invoke(form, new object[] { c, ComoElPuente(tr.GetParameters()[1].ParameterType, dibuja) });
                Linea("enseco-" + md[0] + "x" + md[1], r + " " + visto);
            }
        }

        // (vistas-pared) lo que EnSeco traducido le da a lo que llama: a 'dibuja' se le da un lienzo de prueba sobre el lienzo de la traducida
        // con su estado (suavizado y modo de letra; sin copia, filtro Bilinear, sin medio pixel) y la matriz del HDC de un mapa (la identidad)
        private static Delegate ComoElPuente(Type accion, Action<Lz> dibuja)
        {
            Type[] ts = accion.GetGenericArguments();   // (SKCanvas, bool, AjusteLetra)
            System.Linq.Expressions.ParameterExpression c = System.Linq.Expressions.Expression.Parameter(ts[0], "g"), sv = System.Linq.Expressions.Expression.Parameter(ts[1], "suave"), aj = System.Linq.Expressions.Expression.Parameter(ts[2], "ajuste");
            Action<SKCanvas, bool, object> hace = delegate (SKCanvas g, bool suave, object ajuste) { dibuja(Pru.LienzoSobre(g, suave, Convert.ToInt32(ajuste))); };
            return System.Linq.Expressions.Expression.Lambda(accion, System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(hace), c, sv, System.Linq.Expressions.Expression.Convert(aj, typeof(object))), c, sv, aj).Compile();
        }

        // ================================================================ EL TEXTO DE GDI: la matriz del HDC
        // Lo que se pinta en la imagen de la interfaz con el texto de GDI (TextRenderer en XP) va con la matriz del HDC y no con la del lienzo.
        // Los recorridos no pasan por ahi (la traza no tiene ningun TextRenderer.DrawText del codigo de XP): aqui, el mismo texto en el Graphics
        // de XP (un mapa de bits: HDC identidad) y, en la traduccion, con Nativo/TextoGdi.cs sobre el lienzo con el mismo estado y la misma matriz.
        private static void TextoGdi()
        {
            Fte[] letras = { Fte.Generica(10f, 1), new Fte("Microsoft Sans Serif", 16f, 0, "Pixel") };
            // (los numeros de TextFormatFlags: HorizontalCenter 1 | VerticalCenter 4; Left 0 | VerticalCenter 4 | EndEllipsis 32768)
            int[] banderas = { 1 | 4, 0 | 4 | 32768 };
            int k = 0;
            foreach (Estado e in Estados)
                foreach (Fte f in letras)
                    foreach (int fl in banderas)
                    {
                        string nombre = "textogdi-" + e.Nombre + "-" + (k++);
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(200, 60, "Format32bppPArgb"))
                        {
                            string ex = "", st;
                            using (Lz g = Prepara(b, e))
                            {
                                try
                                {
                                    g.Traslada(13f, 5f);   // (como DrawCtrlOverlay y EnsureUiOverlay antes de llamar a la vista)
                                    if (!Pru.Capa) directas++;
                                    g.EscribeGdi("Tecla Ñ & más texto", f, new Rectangle(4, 6, 150, 30), Color.FromArgb(255, 250, 240, 90), fl);
                                }
                                catch (Exception x) { ex = " " + Exc(x); }
                                st = EstadoG(g);
                            }
                            Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                        }
                    }
            foreach (Fte f in letras) f.Dispose();
        }

        public static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "vuelca") { carpeta = args[1]; System.IO.Directory.CreateDirectory(carpeta); }
            Etiquetas();
            Vistas();
            Ayuda();
            Partir();
            Seco();
            TextoGdi();
            Console.Error.WriteLine(n + " lineas; " + directas + " llamadas directas a lo traducido");
            return 0;
        }
    }
}
