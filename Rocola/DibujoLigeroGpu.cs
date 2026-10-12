// EL DIBUJO MAS LIGERO (rendimiento, 24-09-2026). Solo con tarjeta grafica, automatico y sin interruptor: lo que se
// ve es lo mismo, cuesta menos pintarlo. Tres cosas:
//
//  1) SIN BORRAR LA PANTALLA en cada cuadro (D3D9Device.BeginFrame): el cielo, que es lo primero que se dibuja, es opaco
//     y la tapa entera.
//  2) SIN PINTAR LO TRANSPARENTE de las capas grandes del cielo (la via lactea y la vineta) y de la interfaz (la imagen
//     principal y la franja de abajo, cuando llevan un rato quietas). Ver TeselasVisibles. Y lo que va con opacidad
//     cero (la franja con el reposo del todo abierto, la interfaz del todo fundida) no se manda.
//  3) LA AURORA SIN REPETIR CUENTAS: los colores que solo dependen de la cancion y de la portada se guardan y se
//     rehacen cuando estas cambian, no en cada cuadro. Ver AurBases.
//
// SIN PINTAR LO TRANSPARENTE. Las capas grandes del cielo se dibujaban como UN cuadrilatero a pantalla completa, aunque
// buena parte de su imagen fuera transparente del todo: la tarjeta leia la textura, leia la pantalla, mezclaba con alfa
// cero y volvia a escribir lo mismo que habia. En una tarjeta modesta eso es lo que cuesta de verdad: pixeles pintados.
//
// Aqui se mira UNA vez, al hornear la imagen, que trozos (teselas de pocos texeles) tienen algo, y se dibujan solo
// esos, todos en la misma llamada de dibujo. Una tesela se salta solo si ella Y el texel de alrededor son
// transparentes del todo: el filtrado bilineal de la tarjeta lee como mucho un texel de la vecina, asi que un pixel
// que caiga en una tesela saltada solo podia mezclar alfa cero, o sea dejar la pantalla como estaba. Las teselas
// del borde de la imagen se pintan siempre: ahi la tarjeta puede leer fuera de lo horneado.
//
// Medido a 1024x768: la via lactea deja sin pintar el 40-48 % de su imagen y la vineta el 15 % (su centro). Con teselas de
// 2 texeles (12-10-2026) el 45 % y el 17 %, y las nebulosas sin detalle natural, la mitad.
#if ROCOLA_GPU
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal sealed class TeselasVisibles
    {
        // TESELAS DE 2 TEXELES (fondo ligero, 12-10-2026; antes 4): en pantalla, 8 px la via lactea y las nebulosas y 16 la
        // vineta. Mas finas se ajustan mejor al borde de lo transparente: la via lactea pasa de pintar el 59,7 % de su
        // imagen al 55,4 % y la vineta del 84,8 % al 82,9 % (a 1024x768), a cambio de unos cien triangulos mas, que para
        // la tarjeta no son nada. La regla de que tesela se salta es la misma.
        internal const int LadoTesela = 2;
        internal int Ancho, Alto;            // la imagen horneada, en texeles
        internal int[] Rect = new int[0];    // x0,y0,x1,y1 de cada rectangulo a pintar (texeles; x1,y1 excluidos)
        internal int N;                      // cuantos rectangulos
        internal D3D9.Vtx[] Vtx = new D3D9.Vtx[0];   // seis vertices por rectangulo, reusados en cada cuadro
        internal double Fraccion = 1.0;      // lo que queda por pintar, en parte del area de la imagen

        // Que hay que pintar de esta imagen. Solo mira el alfa. 'bordeSiempre': las teselas del borde se pintan siempre
        // (hace falta si la imagen se ESTIRA: ahi la tarjeta puede leer fuera de lo horneado); una imagen pintada texel a
        // pixel, como las de la interfaz, no lee fuera y no lo necesita.
        internal static TeselasVisibles De(Bitmap b, int lado, bool bordeSiempre)
        {
            if (b == null || lado < 1) return null;
            int w = b.Width, h = b.Height;
            if (w < 1 || h < 1) return null;
            int tx = (w + lado - 1) / lado, ty = (h + lado - 1) / lado;
            bool[] pinta = new bool[tx * ty];
            // Un texel con algo obliga a pintar las teselas que lo tienen a menos de un texel (el filtrado bilineal puede
            // leerlo desde la de al lado): las de (x-1)/lado a (x+1)/lado, y lo mismo a lo alto. Se recorre fila a fila,
            // por tramos seguidos de texeles con algo; sin un arreglo del tamano de la imagen.
            int[] fila = new int[w];
            BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), fila, 0, w);
                    int ja = (y == 0) ? 0 : (y - 1) / lado, jb = Math.Min(ty - 1, (y + 1) / lado);
                    for (int x = 0; x < w; )
                    {
                        if ((fila[x] & unchecked((int)0xFF000000)) == 0) { x++; continue; }
                        int xa = x; while (x < w && (fila[x] & unchecked((int)0xFF000000)) != 0) x++;
                        int ia = (xa == 0) ? 0 : (xa - 1) / lado, ib = Math.Min(tx - 1, x / lado);   // x = ultimo con algo + 1
                        for (int j = ja; j <= jb; j++) for (int i = ia; i <= ib; i++) pinta[j * tx + i] = true;
                    }
                }
            }
            finally { b.UnlockBits(bd); }
            if (bordeSiempre)
            {
                for (int i = 0; i < tx; i++) { pinta[i] = true; pinta[(ty - 1) * tx + i] = true; }
                for (int j = 0; j < ty; j++) { pinta[j * tx] = true; pinta[j * tx + tx - 1] = true; }
            }
            // 3) tiras de teselas seguidas en cada fila; una tira igual a la de la fila de arriba la alarga
            List<int> r = new List<int>();
            List<int> abiertas = new List<int>(), nuevas = new List<int>();   // indices (en r/4) que tocan la fila anterior
            long pintado = 0;
            for (int j = 0; j < ty; j++)
            {
                nuevas.Clear();
                int y0 = j * lado, y1 = Math.Min(h, y0 + lado);
                for (int i = 0; i < tx; )
                {
                    if (!pinta[j * tx + i]) { i++; continue; }
                    int k = i; while (k < tx && pinta[j * tx + k]) k++;
                    int x0 = i * lado, x1 = Math.Min(w, k * lado);
                    int sigue = -1;
                    foreach (int a in abiertas) if (r[a * 4] == x0 && r[a * 4 + 2] == x1) { sigue = a; break; }
                    if (sigue >= 0) { r[sigue * 4 + 3] = y1; nuevas.Add(sigue); }
                    else { nuevas.Add(r.Count / 4); r.Add(x0); r.Add(y0); r.Add(x1); r.Add(y1); }
                    pintado += (long)(x1 - x0) * (y1 - y0);
                    i = k;
                }
                List<int> t = abiertas; abiertas = nuevas; nuevas = t;
            }
            TeselasVisibles v = new TeselasVisibles();
            v.Ancho = w; v.Alto = h; v.Rect = r.ToArray(); v.N = r.Count / 4;
            v.Vtx = new D3D9.Vtx[v.N * 6];
            for (int i = 0; i < v.Vtx.Length; i++) { v.Vtx[i].Z = 0f; v.Vtx[i].Rhw = 1f; }
            v.Fraccion = (double)pintado / ((double)w * h);
            return v;
        }
    }

// Analiza la misma imagen en tramos pequenos, sin clonarla ni bloquearla entre cuadros.
    // Hasta terminar se pinta el quad completo; el recorte final es identico a De().
    internal sealed class TrabajoTeselas
    {
        internal readonly Bitmap Fuente;
        private readonly int w, h, lado, tx, ty;
        private readonly bool[] pinta;
        private readonly int[] fila;
        private readonly List<int> r = new List<int>();
        private List<int> abiertas = new List<int>(), nuevas = new List<int>();
        private int y, banda, vertice;
        private long pintado;
        private TeselasVisibles resultado;

        internal TrabajoTeselas(Bitmap fuente, int tamaño)
        {
            Fuente = fuente; w = fuente.Width; h = fuente.Height; lado = tamaño;
            tx = (w + lado - 1) / lado; ty = (h + lado - 1) / lado;
            pinta = new bool[tx * ty]; fila = new int[w];
        }
        internal TeselasVisibles Avanza(long presupuesto)
        {
            if (presupuesto <= 0) return null;
            long inicio = System.Diagnostics.Stopwatch.GetTimestamp();
            do
            {
                if (y < h)
                {
                    int filas = Math.Min(8, h - y);
                    BitmapData bd = Fuente.LockBits(new Rectangle(0, y, w, filas), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        for (int f = 0; f < filas; f++)
                        {
                            Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)f * bd.Stride), fila, 0, w);
                            int yy = y + f;
                            int ja = yy == 0 ? 0 : (yy - 1) / lado, jb = Math.Min(ty - 1, (yy + 1) / lado);
                            for (int x = 0; x < w; )
                            {
                                if ((fila[x] & unchecked((int)0xFF000000)) == 0) { x++; continue; }
                                int xa = x;
                                while (x < w && (fila[x] & unchecked((int)0xFF000000)) != 0) x++;
                                int ia = xa == 0 ? 0 : (xa - 1) / lado, ib = Math.Min(tx - 1, x / lado);
                                for (int j = ja; j <= jb; j++)
                                    for (int i = ia; i <= ib; i++) pinta[j * tx + i] = true;
                            }
                        }
                    }
                    finally { Fuente.UnlockBits(bd); }
                    y += filas;
                }
                else if (banda < ty)
                {
                    nuevas.Clear();
                    int y0 = banda * lado, y1 = Math.Min(h, y0 + lado);
                    for (int i = 0; i < tx; )
                    {
                        if (!pinta[banda * tx + i]) { i++; continue; }
                        int k = i; while (k < tx && pinta[banda * tx + k]) k++;
                        int x0 = i * lado, x1 = Math.Min(w, k * lado), sigue = -1;
                        foreach (int a in abiertas)
                            if (r[a * 4] == x0 && r[a * 4 + 2] == x1) { sigue = a; break; }
                        if (sigue >= 0) { r[sigue * 4 + 3] = y1; nuevas.Add(sigue); }
                        else { nuevas.Add(r.Count / 4); r.Add(x0); r.Add(y0); r.Add(x1); r.Add(y1); }
                        pintado += (long)(x1 - x0) * (y1 - y0); i = k;
                    }
                    List<int> t = abiertas; abiertas = nuevas; nuevas = t; banda++;
                }
                else
                {
                    if (resultado == null)
                    {
                        resultado = new TeselasVisibles();
                        resultado.Ancho = w; resultado.Alto = h;
                        resultado.Rect = r.ToArray(); resultado.N = r.Count / 4;
                        resultado.Vtx = new D3D9.Vtx[resultado.N * 6];
                        resultado.Fraccion = (double)pintado / ((double)w * h);
                    }
                    int hasta = Math.Min(resultado.Vtx.Length, vertice + 256);
                    for (; vertice < hasta; vertice++) { resultado.Vtx[vertice].Z = 0f; resultado.Vtx[vertice].Rhw = 1f; }
                    if (vertice == resultado.Vtx.Length) return resultado;
                }
            }
            while (System.Diagnostics.Stopwatch.GetTimestamp() - inicio < presupuesto);
            return null;
        }
    }

    internal sealed partial class GpuPanel
    {
        private TeselasVisibles hazTeselas, vinetaTeselas, nebTeselas;   // lo que tienen que pintar la via lactea, la vineta y las nebulosas
        // SOLO SI AHORRAN. Partir en decenas de trozos una imagen que tiene algo casi en toda ella no quita pixeles y deja el
        // redondeo del borde de los trozos (un nivel de 255 en algun pixel suelto). La nebulosa con el detalle natural pinta
        // el 98 %: va entera, exactamente como siempre. Sin el detalle, la mitad: va a trozos.
        internal const double TeselasAhorran = 0.9;
        internal static TeselasVisibles SiAhorran(TeselasVisibles t) { return t != null && t.Fraccion < TeselasAhorran ? t : null; }

        // LA INTERFAZ, TAMBIEN SIN LO TRANSPARENTE. La imagen principal de la interfaz ocupa casi toda la pantalla en la
        // atraccion, dentro de un disco y en el buscador, y la franja de abajo va a lo ancho: buena parte de las dos es
        // transparente del todo (en la atraccion con el escaparate, la principal y la franja lo son enteras). Pero cambian
        // -el reloj cada segundo, el buscador a cada tecla-, asi que sus teselas no se sacan al subirlas: se sacan cuando
        // la imagen lleva un rato QUIETA (TeselasQuieta), y mientras tanto se pinta entera como siempre. Asi escribir en
        // el buscador no espera a nada. Se sacan de la misma imagen que se subio: el form solo la retoca al cambiar su
        // version, y entonces se vuelve a subir y las teselas viejas se tiran.
        private TeselasVisibles uiTeselas, uiBotTeselas;
        private TrabajoTeselas uiAnalisisTeselas, uiBotAnalisisTeselas;
        private double uiSubidaMs, uiBotSubidaMs;
        internal const double TeselasQuieta = 300;           // ms sin cambios antes de sacar las teselas de la interfaz
        internal const int LadoTeselaUi = 16;                // la interfaz va texel a pixel: teselas de 16 x 16 pixeles
        private static TeselasVisibles TeselasQuietas(Bitmap b, int w, int h, double subidaMs)
        {
            if (b == null || b.Width != w || b.Height != h || AhoraMs() - subidaMs < TeselasQuieta) return null;
            return TeselasVisibles.De(b, LadoTeselaUi, false);
        }

        private TeselasVisibles TeselasEnPasos(ref TrabajoTeselas trabajo, Bitmap b, int w, int h, double subidaMs)
        {
            if (b == null || b.Width != w || b.Height != h || AhoraMs() - subidaMs < TeselasQuieta)
            { trabajo = null; return null; }
            if (trabajo == null || !ReferenceEquals(trabajo.Fuente, b)) trabajo = new TrabajoTeselas(b, LadoTeselaUi);
            long ticks = Math.Min(CargaTrabajo.PresupuestoGpu(CargaTrabajo.Procesadores, TopeFpsReal, lastFrameWork),
                (long)(System.Diagnostics.Stopwatch.Frequency * (CargaTrabajo.Procesadores <= 2 ? .00035 : .00075)));
            TeselasVisibles listas = trabajo.Avanza(ticks);
            if (listas != null) trabajo = null;
            return listas;
        }

        // La imagen 't' estirada de (x0,y0) a (x1,y1) con (0,0)-(uMax,vMax) de su textura: lo mismo que un SetTexQuad +
        // DrawQuad, pero solo sus rectangulos con algo, en UNA llamada. Cada rectangulo va exactamente donde caia dentro
        // del cuadrilatero entero y con las mismas coordenadas de textura; sin teselas, el cuadrilatero de siempre.
        private void DibujaTeselas(IntPtr tex, TeselasVisibles t, float x0, float y0, float x1, float y1, float uMax, float vMax, uint argb, D3D9.Vtx[] quad)
        {
            if (tex == IntPtr.Zero) return;
            if (t == null || t.Ancho < 1 || t.Alto < 1)
            {
                SetTexQuadA(quad, x0, y0, x1, y1, uMax, vMax, argb);
                stars.DrawQuad(tex, quad);
                return;
            }
            if (t.N == 0) return;   // toda transparente: no hay nada que pintar
            double sx = ((double)x1 - x0) / t.Ancho, sy = ((double)y1 - y0) / t.Alto;
            double su = (double)uMax / t.Ancho, sv = (double)vMax / t.Alto;
            D3D9.Vtx[] v = t.Vtx;
            for (int i = 0, o = 0; i < t.N; i++, o += 6)
            {
                int a = t.Rect[i * 4], b = t.Rect[i * 4 + 1], c = t.Rect[i * 4 + 2], d = t.Rect[i * 4 + 3];
                float px0 = (float)(x0 + a * sx) - 0.5f, py0 = (float)(y0 + b * sy) - 0.5f;
                float px1 = (float)(x0 + c * sx) - 0.5f, py1 = (float)(y0 + d * sy) - 0.5f;
                float u0 = (float)(a * su), v0 = (float)(b * sv), u1 = (float)(c * su), v1 = (float)(d * sv);
                TV(ref v[o + 0], px0, py0, argb, u0, v0); TV(ref v[o + 1], px1, py0, argb, u1, v0); TV(ref v[o + 2], px1, py1, argb, u1, v1);
                TV(ref v[o + 3], px0, py0, argb, u0, v0); TV(ref v[o + 4], px1, py1, argb, u1, v1); TV(ref v[o + 5], px0, py1, argb, u0, v1);
            }
            stars.DrawTexturedBatch(tex, v, t.N * 2);
        }
        private static void TV(ref D3D9.Vtx x, float px, float py, uint c, float u, float v) { x.X = px; x.Y = py; x.Color = c; x.U = u; x.V = v; }

        // LA AURORA SIN REPETIR CUENTAS. Cada franja de la cortina y el pie de cada columna parten de un color que solo
        // depende de los colores de la cancion (pie y punta de los dos extremos), de si van las bandas de color y de la
        // paleta de la portada. Ese color se calculaba en cada cuadro -1.160 mezclas de color- aunque no hubiera
        // cambiado nada. Ahora se guarda y se rehace solo cuando cambia alguna de esas cosas (al cambiar de cancion, o
        // mientras la paleta de la portada se funde). Lo que depende del cuadro -el color que fluye, la altura, la
        // intensidad de la musica- se sigue calculando en cada cuadro, igual que antes. Son las mismas cuentas, con los
        // mismos datos y en el mismo orden: el mismo color.
        private uint[] aurBase, aurBasePie;                 // (AurFilas + 1) * puntos, y uno por punto
        private readonly uint[] aurBaseDe = new uint[12];   // con que colores se calcularon (4 de la cancion + 8 de la portada)
        private float aurBasePeso; private bool aurBaseBandas, aurBaseOk;
        private void AurBases(GpuAurora au, int puntos)
        {
            bool igual = aurBaseOk && aurBase != null && aurBaseBandas == AuroraBandas && aurBasePeso == coverWeight
                && aurBaseDe[0] == au.Rgb && aurBaseDe[1] == au.RgbA && aurBaseDe[2] == au.Rgb2 && aurBaseDe[3] == au.Rgb2A;
            for (int i = 0; i < 8 && igual; i++) if (aurBaseDe[4 + i] != coverBands[i]) igual = false;
            if (igual) return;
            if (aurBase == null || aurBase.Length < (AurFilas + 1) * puntos) aurBase = new uint[(AurFilas + 1) * puntos];
            if (aurBasePie == null || aurBasePie.Length < puntos) aurBasePie = new uint[puntos];
            // El degradado del pie a la punta, de los dos extremos (grave y agudo): ver AurTonoMezcla.
            for (int k = 0; k <= AurFilas; k++)
            {
                float f = (float)k / AurFilas;
                aurRampPie[k] = AurTonoMezcla(au.Rgb, au.RgbA, f);
                aurRampAgu[k] = AurTonoMezcla(au.Rgb2, au.Rgb2A, f);
            }
            for (int i = 0; i < puntos; i++)
            {
                float fx = (float)i / AurCols;
                uint coverColumn = CoverColor(fx);
                for (int k = 0; k <= AurFilas; k++)
                {
                    uint c = AurLerpRgb(aurRampPie[k], aurRampAgu[k], fx);
                    if (AuroraBandas) c = AurLerpRgb(c, fx < .5f ? AurLerpRgb(0x009779F2u, 0x0059DBCCu, fx*2) : AurLerpRgb(0x0059DBCCu, 0x00E7BE88u, (fx-.5f)*2), .38f);
                    aurBase[k * puntos + i] = MixCoverColor(c, coverColumn);
                }
                aurBasePie[i] = MixCoverColor(AurLerpRgb(au.Rgb, au.Rgb2, fx), coverColumn);
            }
            aurBaseDe[0] = au.Rgb; aurBaseDe[1] = au.RgbA; aurBaseDe[2] = au.Rgb2; aurBaseDe[3] = au.Rgb2A;
            for (int i = 0; i < 8; i++) aurBaseDe[4 + i] = coverBands[i];
            aurBasePeso = coverWeight; aurBaseBandas = AuroraBandas; aurBaseOk = true;
        }
    }
}
#endif
