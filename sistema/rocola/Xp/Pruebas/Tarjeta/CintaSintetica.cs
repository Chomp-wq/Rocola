// UNA CINTA HECHA A MANO (pruebas del port nativo): las llamadas al dispositivo de la tarjeta que las pantallas de la rocola con los
// datos de prueba no hacen (o hacen poco), con entradas al azar de semilla fija, en el mismo formato que graba la rocola
// (Nativo/CintaTarjeta.cs). Asi la comparacion llega tambien a los rincones: DrawSky (el de tres paradas), DrawOpaqueQuad, las
// portadas compartidas (CreaPortadaCompartida dos veces con el mismo mapa), PreloadTexture, la envoltura repetida sobre la misma textura,
// soltar texturas y crear otras en su sitio (OpenGL repite los nombres), cambiar de tamano dos veces, presentar sin esperar (el Reset),
// los dibujos con el arreglo corto o nulo, dos dispositivos a la vez y, al final, un dibujo con una textura ya soltada.
//   reproductor-cintas --sintetica SALIDA.cinta
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PruebasTarjeta
{
    internal static class CintaSintetica
    {
        const int W = 1024, H = 768;
        static BinaryWriter w;
        static MemoryStream ms;
        static Stream sale;
        static Random z;
        static int siguienteMapa;
        static long siguienteTex = 0x1000;   // los nombres de las texturas en esta cinta (como si los diera el dispositivo)
        static double ms0;

        static void Registro(byte tipo) { ms.SetLength(0); w.Write(tipo); w.Write(0); }
        static void Termina()
        {
            w.Flush();
            byte[] b = ms.GetBuffer(); int n = (int)ms.Length, largo = n - 5;
            b[1] = (byte)largo; b[2] = (byte)(largo >> 8); b[3] = (byte)(largo >> 16); b[4] = (byte)(largo >> 24);
            sale.Write(b, 0, n);
        }
        static void Llamada(int disp, byte metodo) { Registro(2); w.Write(disp); w.Write(metodo); w.Write(ms0 += 1); }

        // ---- datos ----
        sealed class Mapa { internal int Id, W, H; internal byte[] Px; }   // BGRA premultiplicado (como lo guarda la rocola)
        static Mapa NuevoMapa(int mw, int mh, Func<int, int, uint> punto)
        {
            Mapa m = new Mapa { Id = ++siguienteMapa, W = mw, H = mh, Px = new byte[mw * mh * 4] };
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    uint c = punto(x, y);   // ARGB sin premultiplicar
                    uint a = c >> 24, r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                    int o = (y * mw + x) * 4;
                    m.Px[o] = (byte)(b * a / 255); m.Px[o + 1] = (byte)(g * a / 255); m.Px[o + 2] = (byte)(r * a / 255); m.Px[o + 3] = (byte)a;
                }
            return m;
        }
        static uint Azar(int alfaMax)
        {
            int r = z.Next(10);
            uint a = (uint)(r < 3 ? 255 : r < 4 ? 0 : z.Next(alfaMax + 1));
            return (a << 24) | ((uint)z.Next(256) << 16) | ((uint)z.Next(256) << 8) | (uint)z.Next(256);
        }
        static void EscribeMapa(Mapa m, int x, int y, int zw, int zh)
        {
            w.Write(m.Id); w.Write(m.W); w.Write(m.H); w.Write(2498570);   // PixelFormat.Format32bppArgb
            w.Write(x); w.Write(y); w.Write(zw); w.Write(zh);
            for (int k = 0; k < zh; k++) w.Write(m.Px, ((y + k) * m.W + x) * 4, zw * 4);
        }
        static void Vertices(float[] v)   // 7 numeros por vertice (X, Y, Z, Rhw, color en bits, U, V)
        {
            if (v == null) { w.Write(-1); w.Write(0); return; }
            int n = v.Length / 7;
            w.Write(n); w.Write(n);
            for (int i = 0; i < v.Length; i++) { if (i % 7 == 4) w.Write(BitConverter.SingleToUInt32Bits(v[i])); else w.Write(v[i]); }
        }
        static void VerticesCortos(float[] v, int largo)   // el arreglo de XP mas corto de lo que pide el dibujo
        {
            int n = v.Length / 7;
            w.Write(largo); w.Write(Math.Min(n, largo));
            for (int i = 0; i < Math.Min(n, largo) * 7; i++) { if (i % 7 == 4) w.Write(BitConverter.SingleToUInt32Bits(v[i])); else w.Write(v[i]); }
        }
        static float C(uint c) { return BitConverter.UInt32BitsToSingle(c); }
        static void V(List<float> l, float x, float y, uint c, float u, float vv) { l.Add(x - 0.5f); l.Add(y - 0.5f); l.Add(0); l.Add(1); l.Add(C(c)); l.Add(u); l.Add(vv); }
        static void Quad(List<float> l, float x0, float y0, float x1, float y1, uint c, float u0, float v0, float u1, float v1)
        {
            V(l, x0, y0, c, u0, v0); V(l, x1, y0, c, u1, v0); V(l, x1, y1, c, u1, v1);
            V(l, x0, y0, c, u0, v0); V(l, x1, y1, c, u1, v1); V(l, x0, y1, c, u0, v1);
        }
        static uint Color(int a0, int a1) { return ((uint)z.Next(a0, a1 + 1) << 24) | ((uint)z.Next(256) << 16) | ((uint)z.Next(256) << 8) | (uint)z.Next(256); }
        static float[] Halos(int n)
        {
            float[] hx = { 1f, 0.41421356f, -0.41421356f, -1f, -1f, -0.41421356f, 0.41421356f, 1f };
            float[] hy = { -0.41421356f, -1f, -1f, -0.41421356f, 0.41421356f, 1f, 1f, 0.41421356f };
            List<float> l = new List<float>();
            for (int i = 0; i < n; i++)
            {
                float cx = (float)(z.NextDouble() * W), cy = (float)(z.NextDouble() * H), r = 1.5f + (float)z.NextDouble() * 9; uint c = Color(30, 255);
                for (int k = 0; k < 6; k++)
                {
                    V(l, cx + hx[0] * r, cy + hy[0] * r, c, 0.5f + 0.5f * hx[0], 0.5f + 0.5f * hy[0]);
                    V(l, cx + hx[k + 1] * r, cy + hy[k + 1] * r, c, 0.5f + 0.5f * hx[k + 1], 0.5f + 0.5f * hy[k + 1]);
                    V(l, cx + hx[k + 2] * r, cy + hy[k + 2] * r, c, 0.5f + 0.5f * hx[k + 2], 0.5f + 0.5f * hy[k + 2]);
                }
            }
            return l.ToArray();
        }
        static float[] Triangulos(int n, int a0, int a1)
        {
            List<float> l = new List<float>();
            for (int i = 0; i < n * 3; i++) V(l, (float)(z.NextDouble() * (W + 100) - 50), (float)(z.NextDouble() * (H + 100) - 50), Color(a0, a1), 0, 0);
            return l.ToArray();
        }

        // ---- las llamadas ----
        static void Nuevo(int disp, int dw, int dh) { Registro(1); w.Write(disp); w.Write(0L); w.Write(dw); w.Write(dh); w.Write(ms0 += 1); Termina(); }
        static long CreaPow2(int disp, Mapa m, byte metodo = 17)
        {
            long t = ++siguienteTex;
            Llamada(disp, metodo); EscribeMapa(m, 0, 0, m.W, m.H); w.Write(t); w.Write(0f); w.Write(0f); Termina();
            return t;
        }
        static long CreaDeMapa(int disp, Mapa m)
        {
            long t = ++siguienteTex;
            Llamada(disp, 4); EscribeMapa(m, 0, 0, m.W, m.H); w.Write(t); Termina();
            return t;
        }
        static void Suelta(long t) { Registro(3); w.Write(t); w.Write(true); w.Write(ms0 += 1); Termina(); }
        static void Region(int disp, long t, Mapa m, int x, int y, int zw, int zh)
        {
            Llamada(disp, 18); w.Write(t); EscribeMapa(m, x, y, zw, zh); w.Write(x); w.Write(y); w.Write(zw); w.Write(zh); w.Write(true); Termina();
        }
        static void ActualizaPow2(int disp, long t, Mapa m) { Llamada(disp, 19); w.Write(t); EscribeMapa(m, 0, 0, m.W, m.H); w.Write(true); w.Write(0f); w.Write(0f); Termina(); }
        static void Abre(int disp) { Llamada(disp, 5); w.Write(true); Termina(); }
        static void Cierra(int disp) { Llamada(disp, 6); w.Write(true); Termina(); }
        static void Campo(int disp, byte campo, uint valor) { Registro(4); w.Write(disp); w.Write(campo); w.Write(valor); Termina(); }
        static void Dibujo(int disp, byte metodo, long tex, bool conTex, int tris, bool conTris, float[] v)
        {
            Llamada(disp, metodo); if (conTex) w.Write(tex); if (conTris) w.Write(tris); Vertices(v); Termina();
        }
        static void Cubo(int disp, long tex, float x, float y, float cw, float ch, double ang, float alfa, double incl, float um, float vm)
        {
            Llamada(disp, 16); w.Write(tex); w.Write(um); w.Write(vm); w.Write(x); w.Write(y); w.Write(cw); w.Write(ch); w.Write(ang); w.Write(alfa); w.Write(incl); Termina();
        }
        static void Media(int disp, int div)
        {
            Llamada(disp, 22); w.Write(W); w.Write(H); w.Write(div); w.Write(true); Termina();
        }
        static void FinMedia(int disp) { Llamada(disp, 23); w.Write(true); Termina(); }
        static void SinEspera(int disp, bool s) { Llamada(disp, 27); w.Write(s); w.Write(true); Termina(); }
        static void Redimensiona(int disp, int nw, int nh) { Llamada(disp, 1); w.Write(nw); w.Write(nh); w.Write(true); Termina(); }
        static void Prepara(int disp) { Llamada(disp, 2); w.Write(true); Termina(); }
        static void Precarga(int disp, long t) { Llamada(disp, 3); w.Write(t); w.Write(true); Termina(); }

        internal static int Escribe(string ruta)
        {
            z = new Random(20261108);
            ms = new MemoryStream(); w = new BinaryWriter(ms);
            using (FileStream f = File.Create(ruta))
            using (BrotliStream b = new BrotliStream(f, CompressionLevel.Optimal))
            {
                sale = b;
                byte[] cab = Encoding.ASCII.GetBytes("ROCOLA-CINTA-TARJETA 1\n");
                sale.Write(cab, 0, cab.Length);
                Nuevo(1, W, H);
                // las texturas: el resplandor, portadas al azar, el grano, la vineta, una nebulosa y un letrero ancho (NPOT)
                Mapa glow = NuevoMapa(64, 64, (x, y) => { double dx = (x + 0.5) / 32 - 1, dy = (y + 0.5) / 32 - 1, d = Math.Sqrt(dx * dx + dy * dy); uint a = d >= 1 ? 0u : (uint)(220 * (1 - d) + 0.5); return (a << 24) | 0xFFFFFF; });
                long tGlow = CreaDeMapa(1, glow);
                List<long> portadas = new List<long>(); List<Mapa> mapasPortada = new List<Mapa>();
                for (int i = 0; i < 12; i++)
                {
                    Mapa m = NuevoMapa(20 + z.Next(300), 20 + z.Next(200), (x, y) => Azar(255));
                    mapasPortada.Add(m); portadas.Add(CreaPow2(1, m));
                }
                long tGrano = CreaPow2(1, NuevoMapa(256, 256, (x, y) => Azar(40)));
                long tVineta = CreaPow2(1, NuevoMapa(256, 256, (x, y) => { double dx = 2.0 * x / 255 - 1, dy = 2.0 * y / 255 - 1, d = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / 1.414); return (uint)(110 * d * d + 0.5) << 24; }));
                long tNeb = CreaPow2(1, NuevoMapa(256, 192, (x, y) => Azar(120)));
                Mapa letrero = NuevoMapa(1000, 150, (x, y) => Azar(255));
                long tLetrero = CreaPow2(1, letrero);
                // portadas compartidas: dos veces el mismo mapa (la segunda es la misma textura) y otra
                Mapa compartido = mapasPortada[3];
                long tComp1 = CreaPow2(1, compartido, 28), tComp2 = CreaPow2(1, compartido, 28), tComp3 = CreaPow2(1, mapasPortada[5], 28);
                Precarga(1, portadas[0]); Precarga(1, tLetrero);
                Prepara(1);
                float[] cielo; { List<float> l = new List<float>(); Quad(l, 0.5f, 0.5f, W + 0.5f, H * 0.62f + 0.5f, 0xFF06081A, 0, 0, 0, 0); Quad(l, 0.5f, H * 0.62f + 0.5f, W + 0.5f, H + 0.5f, 0xFF101C3A, 0, 0, 0, 0); cielo = l.ToArray(); }
                float[] estrellas = Halos(1400);

                // cuadro 1: el cielo de tres paradas (DrawSky), las estrellas, fugaces y malla sumada
                Abre(1);
                Dibujo(1, 7, 0, false, 4, false, cielo);
                Dibujo(1, 8, tGlow, true, estrellas.Length / 21, true, estrellas);
                Dibujo(1, 9, 0, false, 8, true, Triangulos(8, 40, 200));
                Dibujo(1, 11, 0, false, 8, true, Triangulos(8, 0, 255));
                Cierra(1);

                // cuadro 2: mallas grandes (el bufer de vertices), cuadros con textura, opacos, envoltura repetida, cubos y la vineta por punto
                Abre(1);
                Dibujo(1, 25, 0, false, 4, true, cielo);
                float[] malla = Triangulos(150, 0, 255);
                for (int k = 0; k < 6; k++) { Dibujo(1, 10, 0, false, 150, true, malla); Dibujo(1, 11, 0, false, 150, true, malla); }
                for (int i = 0; i < 60; i++)
                {
                    int p = z.Next(portadas.Count); long t = portadas[p]; Mapa m = mapasPortada[p];
                    float x = (float)(z.NextDouble() * (W - 50)), y = (float)(z.NextDouble() * (H - 50)), esc = z.Next(3) == 0 ? 1f : 0.3f + (float)z.NextDouble() * 1.7f;
                    if (z.Next(2) == 0) { x = (float)Math.Round(x); y = (float)Math.Round(y); }
                    float u = (float)m.W / Pow2(m.W), vv = (float)m.H / Pow2(m.H);
                    List<float> q = new List<float>(); Quad(q, x, y, x + m.W * esc, y + m.H * esc, Color(0, 255) | (z.Next(3) == 0 ? 0xFF000000u : 0), 0, 0, u, vv);
                    int modo = z.Next(6);
                    if (i == 30) Campo(1, 1, 1);   // VinetaPuntos (G2) unos dibujos
                    if (i == 40) Campo(1, 1, 0);
                    if (modo == 0) Dibujo(1, 13, t, true, 2, false, q.ToArray());
                    else if (modo == 1) { List<float> g = new List<float>(); Quad(g, x, y, x + 300, y + 200, 0x80FFFFFF, 0, 0, 3.3f, 2.1f); Dibujo(1, 15, tGrano, true, 2, true, g.ToArray()); }
                    else if (modo == 2) { Dibujo(1, 15, t, true, 2, true, q.ToArray()); Dibujo(1, 12, t, true, 2, false, q.ToArray()); }   // la misma textura con WRAP y luego CLAMP
                    else if (modo == 3) Dibujo(1, 14, z.Next(2) == 0 ? t : 0, true, 2, true, q.ToArray());
                    else Dibujo(1, 12, t, true, 2, false, q.ToArray());
                }
                Campo(1, 2, 0x00805020);
                for (int i = 0; i < 6; i++) Cubo(1, i % 3 == 0 ? 0 : portadas[i], 100 + i * 150, 300, 120, 140, i * 0.7, 0.4f + i * 0.1f, (i - 3) * 0.05, 0.8f, 0.9f);
                Campo(1, 2, 0x003C466E);
                Cierra(1);

                // cuadro 3 y 4: la imagen intermedia a la mitad y a un cuarto, y su estirado
                for (int div = 2; div <= 4; div += 2)
                {
                    Abre(1);
                    Media(1, div);
                    { List<float> l = new List<float>(); Quad(l, 0, 0, (W + 8f) / div, (H + 8f) / div, 0xFF101C3A, 0, 0, 0, 0); Dibujo(1, 25, 0, false, 2, true, l.ToArray()); }
                    { List<float> l = new List<float>(); Quad(l, -60.3f, -40.7f, W + 61.2f, H + 39.9f, 0x9A6080FF, 0, 0, 1, 0.75f); Dibujo(1, 12, tNeb, true, 2, false, l.ToArray()); }
                    FinMedia(1);
                    { List<float> l = new List<float>(); Quad(l, 0, 0, W, H, 0xFFFFFFFF, 0, 0, 0.5f, 0.75f); Dibujo(1, 24, 0, false, 2, true, l.ToArray()); }
                    Dibujo(1, 8, tGlow, true, 1000, true, estrellas);
                    Cierra(1);
                }

                // cuadro 5: grano y vineta en una pasada, despues color y textura
                Abre(1);
                Dibujo(1, 25, 0, false, 4, true, cielo);
                {
                    List<float> l = new List<float>();
                    for (int y = 0; y < H; y += 512) for (int x = 0; x < W; x += 512)
                    {
                        float aw = Math.Min(512, W - x), ah = Math.Min(512, H - y);
                        float[] px = { x, x + aw, x + aw, x, x + aw, x }, py = { y, y, y + ah, y, y + ah, y + ah };
                        for (int k = 0; k < 6; k++) { l.Add(px[k] - 0.5f); l.Add(py[k] - 0.5f); l.Add(0); l.Add(1); l.Add(C(0xFFFFFFFF)); l.Add((px[k] - x) / 256f); l.Add((py[k] - y) / 256f); l.Add(px[k] / W); l.Add(py[k] / H); }
                    }
                    Llamada(1, 26); w.Write(tGrano); w.Write(tVineta); w.Write(l.Count / 27);
                    w.Write(l.Count / 9); w.Write(l.Count / 9);
                    for (int i = 0; i < l.Count; i++) { if (i % 9 == 4) w.Write(BitConverter.SingleToUInt32Bits(l[i])); else w.Write(l[i]); }
                    w.Write(true); Termina();
                }
                Dibujo(1, 10, 0, false, 60, true, malla);
                { List<float> q = new List<float>(); Quad(q, 100, 100, 400, 300, 0xC0FFFFFF, 0, 0, 1, 1); Dibujo(1, 12, portadas[0], true, 2, false, q.ToArray()); }
                Cierra(1);

                // cuadro 6: subir una zona (hasta el borde) y el letrero entero, soltar y crear otras (los nombres se repiten), las compartidas
                for (int y = 30; y < 150; y++) for (int x = 333; x < 1000; x++) { int o = (y * 1000 + x) * 4; letrero.Px[o] = (byte)(x ^ y); letrero.Px[o + 3] = 255; }
                Region(1, tLetrero, letrero, 333, 30, 667, 120);
                ActualizaPow2(1, portadas[1], mapasPortada[1]);
                Suelta(portadas[2]); Suelta(portadas[4]);
                portadas[2] = CreaPow2(1, NuevoMapa(64, 64, (x, y) => Azar(255)));
                portadas[4] = CreaPow2(1, NuevoMapa(90, 33, (x, y) => Azar(255)));
                mapasPortada[2] = null; mapasPortada[4] = null;
                Suelta(tComp1);   // la compartida: la otra sigue
                Abre(1);
                Dibujo(1, 25, 0, false, 4, true, cielo);
                { List<float> q = new List<float>(); Quad(q, 0, 500, 1000, 650, 0xFFFFFFFF, 0, 0, 1000f / 1024, 150f / 256); Dibujo(1, 12, tLetrero, true, 2, false, q.ToArray()); }
                for (int i = 0; i < 6; i++) { List<float> q = new List<float>(); Quad(q, 20 + i * 160, 40, 150 + i * 160, 200, 0xFFFFFFFF, 0, 0, 1, 1); Dibujo(1, 12, i < 3 ? portadas[1 + i * 1] : (i == 3 ? tComp2 : (i == 4 ? tComp3 : portadas[4])), true, 2, false, q.ToArray()); }
                Cubo(1, tComp2, 500, 260, 200, 200, 0.9, 1f, 0.02, 1, 1);
                Cierra(1);
                Suelta(tComp2); Suelta(tComp3);

                // cuadros 7 y 8: presentar sin esperar (Reset en el siguiente cuadro) y volver
                SinEspera(1, true); Prepara(1);
                Abre(1); Dibujo(1, 25, 0, false, 4, true, cielo); Dibujo(1, 8, tGlow, true, 500, true, estrellas); { List<float> q = new List<float>(); Quad(q, 10, 10, 300, 200, 0xFFFFFFFF, 0, 0, 1, 1); Dibujo(1, 15, tGrano, true, 2, true, q.ToArray()); } Cierra(1);
                SinEspera(1, false);
                Abre(1); Dibujo(1, 25, 0, false, 4, true, cielo); { List<float> q = new List<float>(); Quad(q, 10, 10, 300, 200, 0xFFFFFFFF, 0, 0, 1, 1); Dibujo(1, 12, tGrano, true, 2, false, q.ToArray()); } Cierra(1);

                // cuadros 9 y 10: otro tamano y de vuelta
                Redimensiona(1, 800, 600);
                Abre(1); Dibujo(1, 25, 0, false, 4, true, cielo); Media(1, 2); Dibujo(1, 25, 0, false, 4, true, cielo); FinMedia(1);
                { List<float> l = new List<float>(); Quad(l, 0, 0, 800, 600, 0xFFFFFFFF, 0, 0, 0.6f, 0.6f); Dibujo(1, 24, 0, false, 2, true, l.ToArray()); }
                Dibujo(1, 8, tGlow, true, 800, true, estrellas); Cierra(1);
                Redimensiona(1, W, H);
                Abre(1); Dibujo(1, 7, 0, false, 4, false, cielo); Dibujo(1, 8, tGlow, true, 800, true, estrellas); Cierra(1);

                // cuadro 11: arreglos cortos o nulos (XP no dibuja nada)
                Abre(1);
                Dibujo(1, 25, 0, false, 4, true, cielo);
                Llamada(1, 10); w.Write(10); VerticesCortos(malla, 12); Termina();
                Llamada(1, 12); w.Write(portadas[0]); Vertices(null); Termina();
                Dibujo(1, 10, 0, false, 0, true, malla);
                Cierra(1);

                // cuadros 12 a 14: un segundo dispositivo (la ventana F12) con sus cuadros entre los del primero, y se suelta
                Nuevo(2, 640, 480);
                long tGlow2 = CreaDeMapa(2, glow);
                Abre(2); Dibujo(2, 7, 0, false, 4, false, cielo); Dibujo(2, 8, tGlow2, true, 400, true, estrellas); Cierra(2);
                Abre(1); Dibujo(1, 25, 0, false, 4, true, cielo); Dibujo(1, 8, tGlow, true, 400, true, estrellas); Cierra(1);
                Abre(2); Dibujo(2, 7, 0, false, 4, false, cielo); Cierra(2);
                Suelta(tGlow2); Llamada(2, 20); Termina();

                // cuadro 15: un dibujo con una textura ya soltada (en la referencia: Direct3D dice que no y el dispositivo se da por perdido)
                Suelta(portadas[6]);
                Abre(1); Dibujo(1, 25, 0, false, 4, true, cielo);
                { List<float> q = new List<float>(); Quad(q, 300, 300, 500, 450, 0xFFFFFFFF, 0, 0, 1, 1); Dibujo(1, 12, portadas[6], true, 2, false, q.ToArray()); }
                Cierra(1);
                Prepara(1);
                Abre(1); Dibujo(1, 7, 0, false, 4, false, cielo); Dibujo(1, 8, tGlow, true, 300, true, estrellas); Cierra(1);
                Llamada(1, 20); Termina();
            }
            Console.WriteLine("cinta sintetica: " + ruta);
            return 0;
        }
        static int Pow2(int n) { int p = 1; while (p < n) p <<= 1; return p; }
    }
}
