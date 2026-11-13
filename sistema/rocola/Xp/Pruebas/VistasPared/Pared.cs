// ARNES DEL GRUPO «PARED» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que dibujan Form1.Pared.cs (el cubo, las ayudas de los botones,
// las casillas de la pared por procesador y para la tarjeta, el contorno, la pared entera por OnPaint, la atraccion y el cubo de la cancion),
// CaratulasSeparadasGpu.cs (la casilla por separado), PaletaCaratulaGpu.cs (la paleta de una portada) y PrecargaPaginaGpu.cs (el
// precalentado), con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con el programa de una carpeta Xp
// (VistasPared.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - En la referencia las llamadas son las de XP (Graphics, Bitmap, Image, Font) y van a GDI+ (la capa congelada); en la traduccion se llama
//     DIRECTO a lo traducido (el lienzo y su estado, la Letra, los puntos), como lo llaman los archivos traducidos. Cada caso sale dos veces
//     (la segunda, «-directo»): en los dos lados son la misma llamada. Lo demas, por reflexion con lo que pida cada lado.
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+ ni la de WinForms: los mapas, las portadas leidas, el lienzo, las letras,
//     el formato tipografico y el OnPaint de la referencia van por Pruebas/Comun (Pru, Lz, Fte, Fmt: la capa en la referencia congelada y lo
//     nativo en el programa nuevo).
//   - El estado del Graphics del que llama va de nueve maneras (suavizado, los modos de letra, copia, filtro, medio pixel, traslacion entera
//     y con decimales, escala, giro y recorte), y los OnPaint tambien como los pinta el escritorio de WinForms (la matriz de su sitio y el recorte
//     sellado). Despues de cada OnPaint se apunta el estado que le queda al Graphics (XP se lo deja cambiado).
//   - Lo privado (las vistas, el cubo, las casillas) va por reflexion.
//   - (vistas-pared, 12-11-2026) AttractView, AlbumView y CubeBox ya no heredan de Control en la traduccion: no tienen OnPaint. Donde la
//     referencia llama a su OnPaint con un Graphics, la traduccion llama a Pinta(SKCanvas) (la de la pantalla) cuando el Graphics trae el
//     estado de un WM_PAINT (el de fabrica), y si no a la Pinta con el estado en argumentos (la del OnPaint de XP, la que pide el precalentado),
//     con el lienzo y el estado de ese Graphics; despues se le pone al Graphics lo que XP le dejaba (lo que hacia el puente). El sitio y el
//     BackColor, por reflexion (de Control en la referencia, de su CajaVista en la traduccion). Caso nuevo: el precalentado de la atraccion
//     (Form1.PrecalientaUnPoco, paso 2: EnSeco con InvokePaint en la referencia, con Pinta en la traduccion).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasPared.dll [vuelca CARPETA]
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoPared
{
    internal static class Pared
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas;   // directas: las llamadas a la traducida sin la firma de XP (en la referencia, ninguna)
        private static string R(float v) { return v.ToString("R", ci); }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Exc(Exception x) { return "EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }

        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap de la traduccion): tal cual (premultiplicados) y como los sube la tarjeta
        // (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, object o)
        {
            if (o == null) return "null";
            object b = Pru.Aprecia(o, "Format32bppPArgb");
            SKBitmap s = Pru.Sk(b); if (s == null) return "soltado";
            byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre.Replace('/', '_') + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }

        // ---- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P; public ModoFiltro I = ModoFiltro.Bilinear; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "de-fabrica", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1, I = ModoFiltro.HighQualityBilinear },
            new Estado { Nombre = "cleartype-escala", S = ModoSuave.AntiAlias, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, Trans = 2, I = ModoFiltro.HighQualityBicubic },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, T = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, Recorte = true, I = ModoFiltro.NearestNeighbor },
            new Estado { Nombre = "traslada-recorte", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 4, Recorte = true, I = ModoFiltro.Bicubic },
            new Estado { Nombre = "suave-sin-ajuste", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.None, Trans = 5, I = ModoFiltro.Low },
            new Estado { Nombre = "sin-suavizar-decimales", S = ModoSuave.None, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 6, I = ModoFiltro.High },
        };
        // un mapa con fondo (para ver la copia y el alfa) y el Graphics con ese estado
        private static Lz Prepara(object b, Estado e)
        {
            Lz g = Pru.Lienzo(b);
            g.Limpia(Color.FromArgb(120, 30, 60, 90));
            g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 25, Pru.Sk(b).Width, 9);
            g.Suavizado = e.S; g.Letra = e.T; g.Copia = e.C; g.Pixel = e.P; g.Filtro = e.I;
            if (e.Trans == 1) g.Traslada(3.5f, 2.25f); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(7f);
            else if (e.Trans == 4) g.Traslada(0, -12); else if (e.Trans == 5) g.Traslada(0.5f, 0f); else if (e.Trans == 6) g.Traslada(0.3f, 0.7f);
            if (e.Recorte) g.Recorta(new RectangleF(10.5f, 6, 120, 80));
            return g;
        }
        private static string Queda(Lz g) { return "queda=" + g.Suavizado + "/" + g.Letra + "/" + g.Copia + "/" + g.Filtro + "/" + g.Pixel; }
        // (vistas-pared) el sitio y el fondo de una vista: Control.SetBounds/BackColor en la referencia, los de su CajaVista en la traduccion
        private static void PonSitio(object v, int w, int h) { v.GetType().GetMethod("SetBounds", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null).Invoke(v, new object[] { 0, 0, w, h }); }
        private static void PonFondo(object v, Color c) { v.GetType().GetProperty("BackColor", BindingFlags.Public | BindingFlags.Instance).SetValue(v, c); }
        private static int pintas;   // (vistas-pared) las veces que se llamo a Pinta(SKCanvas), la de la pantalla
        // (vistas-pared) lo que hacia el OnPaint de XP en una vista sin herencia: Pinta con el lienzo y su estado (la de la pantalla si es el de
        // un WM_PAINT), y despues, en el lienzo de prueba, lo que XP le dejaba al Graphics (que en la traduccion se va con el)
        private static void PintaSinOnPaint(object vista, Lz g)
        {
            Type t = vista.GetType(); string tipo = t.Name;
            object[] e = g.Estado();   // (el lienzo, suave, letra, copia, filtro, medio)
            object lienzo = e[0];
            bool suave = (bool)Campo(e, "Suave"), copia = (bool)Campo(e, "Copia"), medio = (bool)Campo(e, "Medio"); object filtro = Campo(e, "Filtro");
            object bilineal = Enum.Parse(filtro.GetType(), "Bilineal");
            bool wmPaint = !suave && !copia && !medio && filtro.Equals(bilineal);
            int w = (int)t.GetProperty("Width").GetValue(vista), h = (int)t.GetProperty("Height").GetValue(vista);
            object queda = filtro;
            if (wmPaint) { pintas++; t.GetMethod("Pinta", Todo, null, new[] { typeof(SKCanvas) }, null).Invoke(vista, new[] { lienzo }); queda = bilineal; }
            else
            {
                directas++;
                object[] a = tipo == "CubeBox" ? new[] { lienzo, copia, medio } : new[] { lienzo, suave, copia, filtro, medio };
                MethodInfo m = null; foreach (MethodInfo x in t.GetMethods(Todo)) if (x.Name == "Pinta" && x.GetParameters().Length == a.Length) m = x;
                m.Invoke(vista, a);
                if (tipo == "AlbumView") queda = a[3];   // (el filtro que DrawWall deja puesto, por ref)
            }
            if (tipo == "AttractView" && !(w < 8 || h < 8)) { g.Filtro = ModoFiltro.Bilinear; g.Letra = ModoLetra.ClearTypeGridFit; }
            else if (tipo == "AlbumView") g.Filtro = (ModoFiltro)(int)queda;
            else if (tipo == "CubeBox" && !(w < 12 || h < 12)) { g.Filtro = ModoFiltro.Bilinear; g.Suavizado = ModoSuave.AntiAlias; }
        }

        // ---- la traduccion, por reflexion (en la referencia no esta)
        private static MethodInfo Metodo(Type t, string nombre, Type primero, int parametros = -1)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (m.Name == nombre && m.GetParameters().Length > 0 && (primero == null || m.GetParameters()[0].ParameterType == primero) && (parametros < 0 || m.GetParameters().Length == parametros)) return m;
            return null;
        }
        // el estado del lienzo (Lz.Estado: el lienzo, suave, letra, copia, filtro, medio): lo que un archivo traducido lleva en sus variables
        private static object Campo(object[] e, string c) { return e[c == "Suave" ? 1 : c == "Letra" ? 2 : c == "Copia" ? 3 : c == "Filtro" ? 4 : 5]; }
        private static object[] Junta(object[] a, params object[] b) { object[] r = new object[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
        // llama a un metodo por su nombre y su numero de argumentos, dandole a cada parametro lo que pida (los mapas del arnes son ya los de
        // cada lado: la Image de la referencia o los puntos, SKBitmap, de la traduccion); los out vuelven en 'args'
        private static object Llama(object blanco, Type t, string nombre, params object[] args)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
            {
                if (m.Name != nombre) continue;
                ParameterInfo[] ps = m.GetParameters(); if (ps.Length != args.Length) continue;
                object[] a = new object[args.Length]; bool vale = true;
                for (int i = 0; i < ps.Length && vale; i++)
                {
                    Type pt = ps[i].ParameterType; if (pt.IsByRef) pt = pt.GetElementType();
                    object x = args[i];
                    if (x == null || pt.IsInstanceOfType(x)) a[i] = x;
                    else if (pt == typeof(SKBitmap) && Pru.EsCapa(x)) a[i] = Pru.Sk(x);
                    else vale = false;
                }
                if (!vale) continue;
                object r = m.Invoke(blanco, a);
                for (int i = 0; i < ps.Length; i++) if (ps[i].ParameterType.IsByRef) args[i] = a[i];
                return r;
            }
            throw new MissingMethodException(t.Name + "." + nombre + "/" + args.Length);
        }
        // (cache-portadas, 12-11-2026) la cache de portadas de la traduccion guarda SKBitmap y la de la referencia Image: las portadas del
        // arnes son ya lo que pide cada una (el mismo objeto para la misma portada, asi que la identidad que XP compara se conserva); un
        // delegado del arnes que devuelve una portada (como object) pasa al delegado que pide el campo (que la devuelve con su tipo)
        private static object Adapta(Type destino, object valor)
        {
            if (valor == null || destino.IsInstanceOfType(valor)) return valor;
            if (typeof(Delegate).IsAssignableFrom(destino) && valor is Delegate)
            {
                MethodInfo inv = destino.GetMethod("Invoke"), de = valor.GetType().GetMethod("Invoke");
                ParameterInfo[] ips = inv.GetParameters(), dps = de.GetParameters();
                System.Linq.Expressions.ParameterExpression[] ps = new System.Linq.Expressions.ParameterExpression[ips.Length];
                System.Linq.Expressions.Expression[] args = new System.Linq.Expressions.Expression[ips.Length];
                for (int i = 0; i < ips.Length; i++) { ps[i] = System.Linq.Expressions.Expression.Parameter(ips[i].ParameterType, "p" + i); args[i] = System.Linq.Expressions.Expression.Convert(ps[i], dps[i].ParameterType); }
                System.Linq.Expressions.Expression cuerpo = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(valor), args);
                if (inv.ReturnType != de.ReturnType) cuerpo = System.Linq.Expressions.Expression.Convert(cuerpo, inv.ReturnType);
                return System.Linq.Expressions.Expression.Lambda(destino, cuerpo, ps).Compile();
            }
            return valor;
        }
        private static void Pon(object o, string campo, object valor)
        {
            Type t = o.GetType();
            FieldInfo f = t.GetField(campo, Todo); if (f != null) { f.SetValue(o, Adapta(f.FieldType, valor)); return; }
            t.GetProperty(campo, Todo).SetValue(o, valor, null);
        }
        private static string Campos(object o)
        {
            if (o == null) return "null";
            StringBuilder sb = new StringBuilder();
            foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) sb.Append(f.Name).Append('=').Append(Convert.ToString(f.GetValue(o), ci)).Append(' ');
            return sb.ToString();
        }

        // ---- las portadas de prueba (con el formato de GDI+ que se diga, por su nombre: el mismo dibujo en los dos lados)
        private static object Portada(int w, int h, int semilla, bool alfa, string f)
        {
            object b = Pru.NuevoMapa(w, h, f);
            using (Lz g = Pru.Lienzo(b))
            {
                g.Limpia(alfa ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(255, 40 + semilla * 30 % 200, 90, 160));
                g.RellenaDegradado(new Rectangle(0, 0, w, h), Color.FromArgb(alfa ? 180 : 255, 250, 80 + semilla * 17 % 150, 30), Color.FromArgb(255, 20, 60, 200), 35f,
                    w / 8, h / 8, w * 3 / 4, h * 3 / 4);
                g.Suavizado = ModoSuave.AntiAlias;
                g.RellenaOvalo(Color.FromArgb(alfa ? 120 : 255, 30, 220, 90), w / 4, h / 3, w / 2, h / 2);
                g.Linea(Color.FromArgb(255, 250, 250, 250), 3f, 0, 0, w, h);
            }
            return b;
        }
        private static object Leida(object origen, bool jpeg)
        {
            using (SKData d = Pru.Sk(origen).Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 90))
                return Pru.Lee(d.ToArray());
        }
        private static object opaca, conAlfa, chica; private static object jpg, png;
        private static void HazPortadas()
        {
            opaca = Portada(300, 300, 1, false, "Format32bppPArgb"); conAlfa = Portada(200, 150, 2, true, "Format32bppPArgb");
            chica = Portada(48, 48, 3, false, "Format32bppArgb");
            using (IDisposable o = (IDisposable)Portada(600, 600, 4, false, "Format32bppPArgb")) jpg = Leida(o, true);
            using (IDisposable o = (IDisposable)Portada(160, 160, 5, true, "Format32bppPArgb")) png = Leida(o, false);
        }

        // ================================================================ EL CUBO (Cube.Draw)
        private static Type Cube { get { return typeof(Form1).GetNestedType("Cube", BindingFlags.NonPublic); } }
        private static void Cubos()
        {
            MethodInfo xp = Pru.Capa ? Metodo(Cube, "Draw", Pru.TLienzo) : null, nuevo = Metodo(Cube, "Draw", typeof(SKCanvas));
            object[] tapas = { null, opaca, conAlfa, jpg, chica };
            string[] nt = { "sin", "opaca", "alfa", "jpg", "chica" };
            double[] angulos = { 0.0, 0.35, 1.1, 2.9, -0.7, Math.PI / 4 };
            Rectangle[] cajas = { new Rectangle(20, 10, 120, 120), new Rectangle(7, 5, 64, 90), new Rectangle(30, 30, 9, 40) };
            foreach (Estado e in Estados)
                for (int t = 0; t < tapas.Length; t++)
                    for (int a = 0; a < angulos.Length; a++)
                    {
                        Rectangle caja = cajas[(a + t) % cajas.Length];
                        string nombre = "cubo-" + e.Nombre + "-" + nt[t] + "-" + a;
                        for (int vez = 0; vez < 2; vez++)
                            using (IDisposable b = (IDisposable)Pru.NuevoMapa(180, 150, "Format32bppPArgb"))
                            using (Lz g = Prepara(b, e))
                            {
                                string ex = "";
                                try
                                {
                                    if (xp != null && (vez == 0 || nuevo == null)) xp.Invoke(null, new object[] { g.Objeto, caja, tapas[t], angulos[a] });
                                    else
                                    {
                                        object[] es = g.Estado();
                                        nuevo.Invoke(null, new object[] { es[0], Campo(es, "Suave"), Campo(es, "Copia"), Campo(es, "Filtro"), Campo(es, "Medio"), caja, Pru.Sk(tapas[t]), angulos[a] });
                                        directas++;
                                    }
                                }
                                catch (Exception x) { ex = " " + Exc(x); }
                                Linea(nombre + (vez == 0 ? "" : "-directo"), Puntos(nombre + "-" + vez, b) + ex + " " + Queda(g));
                            }
                    }
        }

        // ================================================================ LAS AYUDAS DE LOS BOTONES
        private static object[] EstadoAyudas(Lz g)
        {
            object[] es = g.Estado();
            return new[] { es[0], Campo(es, "Suave"), Campo(es, "Letra"), Campo(es, "Copia"), Campo(es, "Medio") };
        }
        private static object Ayuda(bool directo, Lz g, string nombre, params object[] args)
        {
            Type f1 = typeof(Form1);
            MethodInfo nuevo = Metodo(f1, nombre, typeof(SKCanvas));
            // la firma de XP (con un Graphics) solo esta en la referencia: en la traduccion las dos vueltas van a la traducida con el lienzo y
            // su estado. Una letra del arnes (Fte) va por la suya: la Font de la referencia o la Letra de la traduccion
            MethodInfo xpm = Pru.Capa ? Metodo(f1, nombre, Pru.TLienzo) : null;
            for (int i = 0; i < args.Length; i++) if (args[i] is Fte) args[i] = ((Fte)args[i]).Obj;
            if (xpm != null && (!directo || nuevo == null)) return xpm.Invoke(null, Junta(new object[] { g.Objeto }, args));
            directas++;
            return nuevo.Invoke(null, Junta(EstadoAyudas(g), args));
        }
        private static void Ayudas()
        {
            Color rojo = Color.FromArgb(228, 72, 72), verde = Color.FromArgb(96, 200, 120), amarillo = Color.FromArgb(242, 206, 72), tinta = Color.FromArgb(235, 240, 255);
            using (Fte f1 = Fte.Generica(9.5f, 1))   // (1: negrita; 2: cursiva)
            using (Fte f2 = new Fte("Tahoma", 11f))
            using (Fte f3 = new Fte("Arial", 13f, 2, "Pixel"))
            {
                Fte[] letras = { f1, f2, f3 };
                foreach (Estado e in Estados)
                    for (int k = 0; k < letras.Length; k++)
                        for (int vez = 0; vez < 2; vez++)
                            using (IDisposable b = (IDisposable)Pru.NuevoMapa(330, 96, "Format32bppPArgb"))
                            using (Lz g = Prepara(b, e))
                            {
                                bool d = vez == 1; StringBuilder sb = new StringBuilder(); string ex = "";
                                try
                                {
                                    int cy = 22, dd = 14 + k * 3; float x = 6;
                                    Ayuda(d, g, "GlowAround", x, (float)cy, (float)(dd + 4), 0.8f, amarillo);
                                    x = (float)Ayuda(d, g, "CtrlPad", x, cy, dd + 4); sb.Append(R(x)).Append(' ');
                                    x = (float)Ayuda(d, g, "CtrlTxt", letras[k], "Mover", x + 4, cy, tinta) + 10; sb.Append(R(x)).Append(' ');
                                    Ayuda(d, g, "GlowAround", x, (float)cy, (float)dd, 0.35f, verde);
                                    x = (float)Ayuda(d, g, "CtrlDot", x, cy, dd, verde); sb.Append(R(x)).Append(' ');
                                    x = (float)Ayuda(d, g, "CtrlTxt", letras[k], "Entrar / Poner", x + 4, cy, tinta) + 10; sb.Append(R(x)).Append(' ');
                                    Ayuda(d, g, "GlowAround", x, (float)cy, (float)dd, 0.01f, rojo);   // (por debajo del umbral: no pinta)
                                    x = (float)Ayuda(d, g, "CtrlDot", x, cy, dd, rojo); sb.Append(R(x)).Append(' ');
                                    x = (float)Ayuda(d, g, "CtrlTxt", letras[k], "", x + 4, cy, tinta); sb.Append(R(x)).Append(' ');
                                    cy = 64; x = 8.5f;
                                    x = (float)Ayuda(d, g, "CtrlDirLeft", x, cy, 11 + k * 4, amarillo) + 5; sb.Append(R(x)).Append(' ');
                                    x = (float)Ayuda(d, g, "CtrlTxt", letras[k], "página 3 ¿qué? ÁÑ", x, cy, Color.FromArgb(160, tinta)); sb.Append(R(x)).Append(' ');
                                    x = (float)Ayuda(d, g, "CtrlDirRight", x + 5, cy, 11 + k * 4, Color.FromArgb(80, amarillo)); sb.Append(R(x)).Append(' ');
                                    Ayuda(d, g, "CtrlTri", rojo, new PointF(x + 20, cy - 12), new PointF(x + 8.5f, cy + 9.25f), new PointF(x + 31, cy + 9));
                                    Ayuda(d, g, "GlowAround", x + 40, (float)cy + 0.5f, 22.5f, 1f, Color.FromArgb(120, rojo));
                                }
                                catch (Exception x2) { ex = " " + Exc(x2); }
                                string nombre = "ayudas-" + e.Nombre + "-" + k;
                                Linea(nombre + (d ? "-directo" : ""), Puntos(nombre + "-" + vez, b) + ex + " x=" + sb + Queda(g));
                            }
            }
        }

        // ================================================================ LA PALETA DE UNA PORTADA
        private static string Colores(Color[] c)
        {
            if (c == null) return "null";
            StringBuilder sb = new StringBuilder(c.Length + ":");
            foreach (Color x in c) sb.Append(x.ToArgb().ToString("x8")).Append(' ');
            return sb.ToString();
        }
        private static void Paletas()
        {
            MethodInfo nuevo = Metodo(typeof(Form1), "ExtractCoverPalette", typeof(SKBitmap));
            List<KeyValuePair<string, object>> casos = new List<KeyValuePair<string, object>>();
            casos.Add(new KeyValuePair<string, object>("nulo", null));
            casos.Add(new KeyValuePair<string, object>("opaca-pargb", opaca));
            casos.Add(new KeyValuePair<string, object>("alfa-pargb", conAlfa));
            casos.Add(new KeyValuePair<string, object>("chica-argb", chica));
            casos.Add(new KeyValuePair<string, object>("jpg-24", jpg));
            casos.Add(new KeyValuePair<string, object>("png-alfa", png));
            casos.Add(new KeyValuePair<string, object>("alfa-argb", Portada(120, 90, 6, true, "Format32bppArgb")));
            casos.Add(new KeyValuePair<string, object>("alfa-rgb32", Portada(120, 90, 7, true, "Format32bppRgb")));
            casos.Add(new KeyValuePair<string, object>("opaca-rgb24", Portada(97, 131, 8, false, "Format24bppRgb")));
            casos.Add(new KeyValuePair<string, object>("alfa-rgb24", Portada(33, 21, 9, true, "Format24bppRgb")));
            casos.Add(new KeyValuePair<string, object>("opaca-paleta8", Portada(64, 64, 10, false, "Format8bppIndexed")));
            casos.Add(new KeyValuePair<string, object>("alfa-paleta8", Portada(64, 64, 11, true, "Format8bppIndexed")));
            casos.Add(new KeyValuePair<string, object>("uno", Portada(1, 1, 12, false, "Format32bppPArgb")));
            casos.Add(new KeyValuePair<string, object>("tira", Portada(5, 700, 13, true, "Format32bppArgb")));
            for (int s = 20; s < 32; s++) casos.Add(new KeyValuePair<string, object>("varia-" + s, Portada(40 + s * 7, 30 + s * 5, s, s % 3 == 0, s % 4 == 0 ? "Format32bppArgb" : s % 4 == 1 ? "Format24bppRgb" : s % 4 == 2 ? "Format32bppRgb" : "Format32bppPArgb")));
            // la de XP (con un Bitmap) en la referencia; en la traduccion, la traducida con los puntos y el formato que el Bitmap tenia en XP
            // (el dato nativo: Pru.FormatoNativo), y sin portada ningun color, como en XP
            MethodInfo xp = Pru.Capa ? Metodo(typeof(Form1), "ExtractCoverPalette", Pru.TMapa) : null;
            Func<object, Color[]> deXp = delegate (object v)
            {
                if (xp != null) return (Color[])xp.Invoke(null, new[] { v });
                return v == null ? new Color[0] : (Color[])nuevo.Invoke(null, new object[] { Pru.Sk(v), Pru.FormatoNativo(v) });
            };
            foreach (KeyValuePair<string, object> c in casos)
            {
                string r; try { r = Colores(deXp(c.Value)); } catch (Exception x) { r = Exc(x); }
                Linea("paleta-" + c.Key, r);
                try
                {
                    r = Colores(deXp(c.Value));
                    if (nuevo != null && c.Value != null) directas++;
                }
                catch (Exception x) { r = Exc(x); }
                Linea("paleta-" + c.Key + "-directo", r);
            }
        }

        // ================================================================ LA PARED (AlbumView)
        private static Type TAlbum { get { return typeof(Form1).GetNestedType("Album", BindingFlags.NonPublic); } }
        private static Type TVista { get { return typeof(Form1).GetNestedType("AlbumView", BindingFlags.NonPublic); } }
        private static readonly Dictionary<object, object> tapaDe = new Dictionary<object, object>();
        private static readonly Dictionary<string, object> tapaPorRuta = new Dictionary<string, object>();
        private static readonly HashSet<object> enCola = new HashSet<object>();
        public static object TapaDe(object album) { object i; return album != null && tapaDe.TryGetValue(album, out i) ? i : null; }
        public static bool EnColaDe(object album) { return album != null && enCola.Contains(album); }
        private static object TapaPorRuta(string ruta) { object i; return ruta != null && tapaPorRuta.TryGetValue(ruta, out i) ? i : null; }
        // un delegado Func<Album, T> (Album es privado) que llama a un metodo de aqui con el disco como object
        private static Delegate ConAlbum(string metodo, Type devuelve)
        {
            ParameterExpression p = Expression.Parameter(TAlbum, "a");
            return Expression.Lambda(typeof(Func<,>).MakeGenericType(TAlbum, devuelve), Expression.Convert(Expression.Call(typeof(Pared).GetMethod(metodo), Expression.Convert(p, typeof(object))), devuelve), p).Compile();
        }
        private static object Album(int i, string artista, string nombre, string portada, bool top)
        {
            object a = Activator.CreateInstance(TAlbum, true);
            Pon(a, "Folder", "/musica/Disco " + i); Pon(a, "Artist", artista); Pon(a, "Name", nombre); Pon(a, "CoverPath", portada); Pon(a, "IsTop", top);
            return a;
        }
        private static IList Discos(int cuantos)
        {
            IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlbum));
            string[] artistas = { "Los Ángeles Azules", "Café Tacvba", "Juan Gabriel", "La Sonora Dinamita con un nombre larguísimo que no cabe en dos renglones", "AC/DC", "Ñengo", "Rock & Pop", "X" };
            string[] nombres = { "Cómo te voy a olvidar", "Re", "", "Éxitos de siempre y de nunca, volumen 12", "Back in Black", null, "Lo mejor", "Y" };
            object[] tapas = { opaca, null, jpg, conAlfa, chica, png, null, opaca };
            for (int i = 0; i < cuantos; i++)
            {
                object a = Album(i, artistas[i % artistas.Length], nombres[(i * 3) % nombres.Length], tapas[i % tapas.Length] != null ? "/musica/Disco " + i + "/portada.jpg" : (i % 5 == 1 ? "/musica/Disco " + i + "/llega.jpg" : null), i == 4);
                if (tapas[i % tapas.Length] != null) { tapaDe[a] = tapas[i % tapas.Length]; tapaPorRuta["/musica/Disco " + i + "/portada.jpg"] = tapas[i % tapas.Length]; }
                if (i % 4 == 2) enCola.Add(a);
                if (i == 4) { IList cc = new List<string> { "/musica/Disco 0/portada.jpg", "/musica/Disco 2/portada.jpg", "/musica/Disco 3/portada.jpg" }; Pon(a, "CycleCovers", cc); }
                l.Add(a);
            }
            return l;
        }
        private static object Vista(int w, int h, IList discos)
        {
            object v = Activator.CreateInstance(TVista, true);
            PonSitio(v, w, h);
            Pon(v, "Page", discos);
            // (lo que devuelven GetCover y PeekCoverGpu: la Image de la referencia o los puntos, SKBitmap, de la traduccion; lo dice el campo)
            Type tTapa = TVista.GetField("GetCover", Todo).FieldType.GetMethod("Invoke").ReturnType;
            Pon(v, "GetCover", ConAlbum("TapaDe", tTapa)); Pon(v, "PeekCoverGpu", ConAlbum("TapaDe", tTapa));
            Pon(v, "EnCola", ConAlbum("EnColaDe", typeof(bool)));
            Pon(v, "GetCoverByPath", (Func<string, object>)TapaPorRuta);
            return v;
        }
        private static void Medidas(int w, int h, out int cw, out int side, out int labelH, out int gap)
        {
            cw = w / 6; int ch = h / 3; labelH = 38; gap = 3; side = Math.Min(cw - 16, ch - labelH - gap - 12); if (side < 10) side = 10;
        }
        private static void Casillas()
        {
            int[][] pantallas = { new[] { 1024, 560 }, new[] { 800, 420 }, new[] { 1280, 800 }, new[] { 640, 300 }, new[] { 100, 60 } };
            foreach (int[] p in pantallas)
            {
                tapaDe.Clear(); tapaPorRuta.Clear(); enCola.Clear();
                IList discos = Discos(18);
                object v = Vista(p[0], p[1], discos);
                int cw, side, labelH, gap; Medidas(p[0], p[1], out cw, out side, out labelH, out gap);
                string s = p[0] + "x" + p[1];
                for (int i = 0; i < 9; i++)
                {
                    object a = discos[i]; object tapa = TapaDe(a);
                    foreach (float esc in new[] { 1f, 0.5f, 0.999f, 0.01f })
                    {
                        object r = null; string ex = "";
                        try { r = Llama(v, TVista, "BakeCell", a, cw, side, labelH, gap, tapa, esc); } catch (Exception x) { ex = " " + Exc(x); }
                        Linea("casilla-" + s + "-" + i + "-" + R(esc), Puntos("casilla-" + s + "-" + i + "-" + R(esc), r) + ex);
                        ((IDisposable)r)?.Dispose();
                    }
                    foreach (bool lod in new[] { true, false })
                    {
                        Pon(v, "PortadasLod", lod);
                        object[] args = { a, cw, side, labelH, gap, tapa, null }; object r = null; string ex = "";
                        try { r = Llama(v, TVista, "BakeGpuCell", args); } catch (Exception x) { ex = " " + Exc(x); }
                        Linea("atlas-" + s + "-" + i + "-" + lod, Puntos("atlas-" + s + "-" + i + "-" + lod, r) + ex + " " + Campos(args[6]));
                        ((IDisposable)r)?.Dispose();
                        args = new object[] { a, cw, side, labelH, gap, null }; r = null; ex = "";
                        for (int vez = 0; vez < 2; vez++)   // la segunda copia los bordes ya hechos
                        {
                            try { r = Llama(v, TVista, "BakeSeparatedCell", args); } catch (Exception x) { ex = " " + Exc(x); }
                            Linea("separada-" + s + "-" + i + "-" + lod + "-" + vez, Puntos("separada-" + s + "-" + i + "-" + lod, r) + ex + " " + Campos(args[5]));
                            ((IDisposable)r)?.Dispose();
                        }
                    }
                    Pon(v, "PortadasLod", true);
                }
                object contorno = null; string exc = "";
                try { contorno = TVista.GetProperty("ContourSpriteBmp", Todo).GetValue(v, null); } catch (Exception x) { exc = " " + Exc(x); }
                Linea("contorno-" + s, Puntos("contorno-" + s, contorno) + exc);
                // la rejilla para la tarjeta: se pide hasta que todas las casillas estan horneadas (el presupuesto por cuadro depende del reloj)
                GpuGridCell[] buf = new GpuGridCell[40]; int cuantas = 0;
                try
                {
                    for (int vuelta = 0; vuelta < 40; vuelta++)
                    {
                        cuantas = (int)TVista.GetMethod("BuildGpuGrid", Todo).Invoke(v, new object[] { buf });
                        bool todas = true; for (int i = 0; i < cuantas; i++) if (typeof(GpuGridCell).GetField("Bmp").GetValue(buf[i]) == null) todas = false;
                        if (todas) break;
                    }
                }
                catch (Exception x) { exc = " " + Exc(x); }
                Linea("rejilla-" + s, cuantas + exc);
                for (int i = 0; i < cuantas; i++)
                    Linea("rejilla-" + s + "-" + i, Puntos("rejilla-" + s + "-" + i, typeof(GpuGridCell).GetField("Bmp").GetValue(buf[i])) + " clave=" + buf[i].Key + " tapa=" + buf[i].HasCover + "/" + buf[i].NeedsCover + "/" + (buf[i].FullCover != null) + " sitio=" + R(buf[i].X) + "," + R(buf[i].Y) + "," + R(buf[i].W) + "," + R(buf[i].H) + " " + buf[i].CoverRect + " a=" + R(buf[i].A) + " sel=" + buf[i].Sel + " " + Campos(buf[i].Layout));
                ((IDisposable)v).Dispose();
            }
        }

        // un OnPaint sobre un mapa con el estado 'e'; con 'sitio', como lo pinta el escritorio de WinForms (la matriz de su sitio y el recorte sellado)
        private static void Pinta(string nombre, object vista, int w, int h, Estado e, bool sitio, int veces)
        {
            MethodInfo onPaint = Pru.Capa ? vista.GetType().GetMethod("OnPaint", Todo) : null;   // (OnPaint(PaintEventArgs): solo la referencia)
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(w + (sitio ? 60 : 0), h + (sitio ? 40 : 0), "Format32bppPArgb"))
            using (Lz g = Prepara(b, e))
            {
                string ex = "";
                if (sitio) { g.Traslada(37, 21); g.Recorta(new Rectangle(0, 0, w, h)); g.Sella(); }
                try { for (int i = 0; i < veces; i++) if (onPaint == null) PintaSinOnPaint(vista, g); else Pru.OnPaint(onPaint, vista, g, new Rectangle(0, 0, w, h)); }
                catch (Exception x) { ex = " " + Exc(x); }
                Linea(nombre, Puntos(nombre, b) + ex + " " + Queda(g));
            }
        }
        private static void Paredes()
        {
            int[][] pantallas = { new[] { 1024, 560 }, new[] { 640, 300 }, new[] { 6, 300 } };
            foreach (int[] p in pantallas)
            {
                tapaDe.Clear(); tapaPorRuta.Clear(); enCola.Clear();
                IList discos = Discos(18);
                object v = Vista(p[0], p[1], discos);
                string s = "pared-" + p[0] + "x" + p[1];
                Pinta(s + "-primera", v, p[0], p[1], Estados[1], false, 1);    // la primera vez hornea media pagina
                Pinta(s + "-entera", v, p[0], p[1], Estados[1], false, 3);
                foreach (Estado e in Estados)
                {
                    Pon(v, "Selected", 3); Pon(v, "SelAngle", 0.45); Pon(v, "WallOpacity", 1f);
                    Pinta(s + "-" + e.Nombre, v, p[0], p[1], e, false, 1);
                    Pinta(s + "-" + e.Nombre + "-sitio", v, p[0], p[1], e, true, 1);
                    Pon(v, "WallOpacity", 0.55f);
                    Pinta(s + "-" + e.Nombre + "-velada", v, p[0], p[1], e, e.Trans % 2 == 0, 1);
                }
                Pon(v, "WallOpacity", 1f);
                // el relevo: el que se deja sigue girando
                Llama(v, TVista, "MoveSelectionTo", 9, 1); Llama(v, TVista, "StepSpin"); Llama(v, TVista, "StepSpin");
                Pinta(s + "-relevo", v, p[0], p[1], Estados[0], false, 1);
                // el disco TOP elegido: sus portadas se funden (a media mezcla y con una sola)
                Pon(v, "Selected", 4); Pon(v, "Phase", 3.2); Pinta(s + "-top-mezcla", v, p[0], p[1], Estados[0], false, 1);
                Pon(v, "Phase", 1.0); Pinta(s + "-top-una", v, p[0], p[1], Estados[1], false, 1);
                Pon(v, "Phase", 10.9); Pinta(s + "-top-ultima", v, p[0], p[1], Estados[3], false, 1);
                // llega una portada: solo se rehace su casilla
                object llega = discos[1]; tapaDe[llega] = jpg;
                try { Llama(v, TVista, "RefreshCover", "/musica/Disco 1/llega.jpg"); } catch (Exception x) { Linea(s + "-llega-exc", Exc(x)); }
                Pinta(s + "-llega", v, p[0], p[1], Estados[1], false, 1);
                // sin nada elegido y con la pagina vacia
                Pon(v, "Selected", -1); Llama(v, TVista, "CancelHandoff"); Pinta(s + "-sin-elegir", v, p[0], p[1], Estados[0], false, 1);
                // (sin cubo que pintar, DrawWall no toca el filtro: al Graphics le queda el que traia)
                Pinta(s + "-sin-elegir-bicubico", v, p[0], p[1], Estados[3], false, 1); Pinta(s + "-sin-elegir-vecino", v, p[0], p[1], Estados[5], true, 1);
                Pon(v, "Page", Discos(0)); Pinta(s + "-vacia", v, p[0], p[1], Estados[0], false, 2); Pinta(s + "-vacia-baja", v, p[0], p[1], Estados[7], false, 1);
                ((IDisposable)v).Dispose();
            }
        }

        // ================================================================ LA ATRACCION Y EL CUBO DE LA CANCION
        private static void Atracciones()
        {
            Type t = typeof(Form1).GetNestedType("AttractView", BindingFlags.NonPublic);
            tapaPorRuta.Clear(); tapaPorRuta["a"] = opaca; tapaPorRuta["b"] = conAlfa; tapaPorRuta["c"] = jpg;
            int[][] pantallas = { new[] { 640, 480 }, new[] { 1024, 768 }, new[] { 7, 300 }, new[] { 300, 150 } };
            foreach (int[] p in pantallas)
            {
                object v = Activator.CreateInstance(t, true);
                PonSitio(v, p[0], p[1]);
                Pon(v, "GetCoverByPath", (Func<string, object>)TapaPorRuta);
                // el cielo de la atraccion (Starfield, Form1.Fondo.cs: no es de este grupo) depende del reloj y del azar: se rehace cada
                // RefrescoMs y tira fugaces con un Random sin semilla. Para que las dos salidas se puedan comparar, aqui se rehace en cada
                // pintada (RefrescoMs = 0) y no tira fugaces (la siguiente, dentro de un dia)
                object cielo = t.GetField("attractStars", Todo).GetValue(v);
                Pon(cielo, "RefrescoMs", 0); Pon(cielo, "nextShoot", unchecked(Environment.TickCount + 86400000));
                string s = "atraccion-" + p[0] + "x" + p[1];
                // las portadas que da (sin pintar el cielo): una, dos a media mezcla, la que falta
                foreach (double fase in new[] { 0.5, 3.6, 4.1, 7.9, 12.0 })
                    foreach (string ciclo in new[] { "a", "ab", "abc", "xb", "ax", "" })
                    {
                        List<string> cc = new List<string>(); foreach (char c in ciclo) cc.Add(c.ToString());
                        Pon(v, "CycleCovers", cc); Pon(v, "Phase", fase);
                        object r = null; string ex = "";
                        try { r = Llama(v, t, "CurrentCover"); } catch (Exception x) { ex = " " + Exc(x); }
                        Linea(s + "-portada-" + R((float)fase) + "-" + ciclo, Puntos(s + "-portada-" + R((float)fase) + "-" + ciclo, r) + ex);
                    }
                Pon(v, "CycleCovers", new List<string> { "a", "b", "c" });
                int k = 0;
                foreach (Estado e in Estados)
                {
                    Pon(v, "Phase", 3.6 + k); Pon(v, "Angle", 0.3 * k); Pon(v, "SongCount", k % 3 == 0 ? 0 : 1234 * k); k++;
                    Pinta(s + "-" + e.Nombre, v, p[0], p[1], e, false, 1);
                    Pinta(s + "-" + e.Nombre + "-sitio", v, p[0], p[1], e, true, 1);
                }
                PrecalientaAtraccion(s, v);
                (v as IDisposable)?.Dispose();   // (vistas-pared: en la traduccion no es IDisposable, como en XP no le sobrescribe Dispose)
            }
        }
        // (vistas-pared) el precalentado de la atraccion: Form1.PrecalientaUnPoco en su paso 2 (EnSeco de la vista: en XP, InvokePaint con el
        // Graphics de un mapa con suavizado; en la traduccion, la Pinta con ese estado). El mapa se tira: se ve lo que hornea (la mezcla de las
        // portadas del cubo, blendBmp) y si dio el paso por hecho (3) o lo reintenta (2: la vista aun no mide 8x8)
        private static void PrecalientaAtraccion(string s, object v)
        {
            Type t = v.GetType();
            Pon(v, "CycleCovers", new List<string> { "a", "b", "c" }); Pon(v, "Phase", 3.6); Pon(v, "Angle", 0.7); Pon(v, "SongCount", 77);
            t.GetField("blendBmp", Todo).SetValue(v, null);
            object f = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            Pon(f, "attractView", v); Pon(f, "precalPaso", 2); Pon(f, "precalReintentos", 0); Pon(f, "lastInputTick", unchecked(Environment.TickCount - 100000));
            string ex = "";
            try { Llama(f, typeof(Form1), "PrecalientaUnPoco"); } catch (Exception x) { ex = " " + Exc(x); }
            Linea(s + "-precalienta", "paso=" + Campo2(f, "precalPaso") + " reintentos=" + Campo2(f, "precalReintentos") + " " + Puntos(s + "-precalienta", t.GetField("blendBmp", Todo).GetValue(v)) + ex);
        }
        private static object Campo2(object o, string c) { for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(c, Todo | BindingFlags.DeclaredOnly); if (fi != null) return fi.GetValue(o); } return null; }
        private static void CubosDeCancion()
        {
            Type t = typeof(Form1).GetNestedType("CubeBox", BindingFlags.NonPublic);
            int[][] pantallas = { new[] { 120, 120 }, new[] { 96, 140 }, new[] { 11, 50 }, new[] { 260, 200 } };
            object[] tapas = { null, opaca, conAlfa, jpg };
            foreach (int[] p in pantallas)
            {
                object v = Activator.CreateInstance(t, true);
                PonSitio(v, p[0], p[1]);
                int k = 0;
                foreach (Estado e in Estados)
                    foreach (bool opaco in new[] { true, false })
                    {
                        Pon(v, "Cover", tapas[k % tapas.Length]); Pon(v, "Angle", 0.37 * k); Pon(v, "Phase", 0.8 * k);
                        PonFondo(v, opaco ? Color.FromArgb(10, 15, 44) : Color.Transparent); k++;
                        string s = "cubito-" + p[0] + "x" + p[1] + "-" + e.Nombre + "-" + (opaco ? "opaco" : "transparente");
                        Pinta(s, v, p[0], p[1], e, false, 1);
                        Pinta(s + "-sitio", v, p[0], p[1], e, true, 1);
                    }
                // el relevo: media vuelta y la portada nueva
                Pon(v, "Cover", opaca); Llama(v, t, "Relevo", (object)jpg); Llama(v, t, "Avanza", 0.06);
                Linea("cubito-relevo-" + p[0], "girando=" + t.GetProperty("Girando", Todo).GetValue(v, null) + " usa=" + Llama(v, t, "UsaPortada", (object)jpg) + "/" + Llama(v, t, "UsaPortada", (object)conAlfa));
                (v as IDisposable)?.Dispose();
            }
        }

        // ================================================================ EL PRECALENTADO
        private static void Precalentados()
        {
            int[][] pantallas = { new[] { 1024, 768 }, new[] { 640, 480 }, new[] { 1920, 1080 }, new[] { 300, 200 }, new[] { 6, 6 } };
            foreach (int[] p in pantallas)
                for (int paso = 2; paso <= 7; paso++)
                    foreach (bool doce in new[] { false, true })
                    {
                        string r = "bien";
                        try { Form1.PrecalientaPiezaGpuEn(paso, p[0], p[1], doce, paso % 2 == 0, doce && paso == 3, paso - 3); } catch (Exception x) { r = Exc(x); }
                        Linea("precalienta-" + p[0] + "x" + p[1] + "-" + paso + "-" + doce, r + " tipografico=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas);
                    }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            HashSet<string> solo = new HashSet<string>(); for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "solo") foreach (string x in a[i + 1].Split(',')) solo.Add(x);
            HazPortadas();
            Linea("inicio", "tipografico=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas);
            if (solo.Count == 0 || solo.Contains("cubos")) Cubos();
            if (solo.Count == 0 || solo.Contains("ayudas")) Ayudas();
            if (solo.Count == 0 || solo.Contains("paletas")) Paletas();
            if (solo.Count == 0 || solo.Contains("casillas")) Casillas();
            if (solo.Count == 0 || solo.Contains("paredes")) Paredes();
            if (solo.Count == 0 || solo.Contains("atracciones")) Atracciones();
            if (solo.Count == 0 || solo.Contains("cubitos")) CubosDeCancion();
            if (solo.Count == 0 || solo.Contains("precalentados")) Precalentados();
            Console.Error.WriteLine("pared: " + n + " lineas, " + directas + " llamadas directas a la traducida, " + pintas + " a Pinta");
            return 0;
        }
    }
}
