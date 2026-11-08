// LA TRAZA DE LOS DIBUJOS DE LA TARJETA (pruebas; port nativo de XP). Las mismas dos herramientas que tenia la traduccion con capas
// (Plataforma/Direct3DaGL.cs), con el mismo formato, para comparar una corrida con la otra:
//   ROCOLA_TRAZA_DIBUJOS=S   cada dibujo del primer cuadro que empieza a los S segundos (de cualquier dispositivo): cuantos vertices, la
//                            caja que ocupan, el color del primero, la mezcla y las etapas como se las pidio XP a Direct3D 9 (los numeros de
//                            D3DBLEND y D3DTSS), la textura de la etapa 0 (medida y nombre de OpenGL), el filtro (siempre LINEAR) y el destino.
//   ROCOLA_SALTA_DIBUJOS=k1,k2  en cada cuadro no se hacen esos dibujos (para buscar cual pinta mal)
// Solo cuentan los dibujos de la memoria del programa (los de DrawPrimitiveUP en XP), como antes: los del bufer de vertices no.
#if ROCOLA_GPU
using System;
using System.Globalization;
using Rocola.Pantalla;

namespace Rocola
{
    internal static unsafe class TrazaTarjeta
    {
        private static readonly double desde = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_DIBUJOS") != null ? double.Parse(Environment.GetEnvironmentVariable("ROCOLA_TRAZA_DIBUJOS"), CultureInfo.InvariantCulture) * 1000.0 : 0;
        private static readonly int[] salta = Environment.GetEnvironmentVariable("ROCOLA_SALTA_DIBUJOS") == null ? null : Array.ConvertAll(Environment.GetEnvironmentVariable("ROCOLA_SALTA_DIBUJOS").Split(','), int.Parse);
        private static bool trazando, trazado;
        private static int dibujoEnCuadro;

        // al empezar un cuadro (BeginScene en XP)
        internal static void Cuadro(double ahoraMs)
        {
            dibujoEnCuadro = 0;
            if (desde > 0 && !trazado && ahoraMs >= desde) { trazando = true; trazado = true; Console.WriteLine("---- cuadro a los " + (ahoraMs / 1000).ToString("0.00") + " s"); }
        }
        // al presentar (Present)
        internal static void Presenta() { trazando = false; }

        // Antes de cada dibujo. False: la prueba pidio no hacerlo.
        internal static bool Dibujo(SombreadoresTarjeta s, byte* v, int paso, int n, int ancho, int alto, bool enFondo)
        {
            if (trazando) Escribe(s, v, paso, n, ancho, alto, enFondo);
            dibujoEnCuadro++;
            return salta == null || Array.IndexOf(salta, dibujoEnCuadro) < 0;
        }
        private static void Escribe(SombreadoresTarjeta s, byte* v, int paso, int n, int ancho, int alto, bool enFondo)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue; uint c0 = 0;
            for (int i = 0; i < n; i++) { float* f = (float*)(v + (long)i * paso); x0 = Math.Min(x0, f[0]); y0 = Math.Min(y0, f[1]); x1 = Math.Max(x1, f[0]); y1 = Math.Max(y1, f[1]); if (i == 0) c0 = *(uint*)(v + 16); }
            // la mezcla: la de OpenGL, en los numeros de D3DBLEND
            string mezcla = "no";
            if (GlMas.IsEnabled(GL.BLEND) != 0)
            {
                int f, d; GL.GetIntegerv(GlMas.BLEND_SRC_RGB, &f); GL.GetIntegerv(GlMas.BLEND_DST_RGB, &d);
                mezcla = Factor((uint)f) + "/" + Factor((uint)d);
            }
            System.Text.StringBuilder etapas = new System.Text.StringBuilder();
            for (int k = 0; k < 6; k++) etapas.Append(s.Etapa0[k]).Append(k < 5 ? ',' : ';');
            if (s.Etapa0[0] != 1) for (int k = 0; k < 6; k++) etapas.Append(s.Etapa1[k]).Append(k < 5 ? ',' : ';');
            int t; GL.GetIntegerv(GlMas.TEXTURE_BINDING_2D, &t);
            int tw, th;
            string tex = t != 0 && TexturasGl.Medida((uint)t, out tw, out th) ? tw + "x" + th + "#" + t : "-";
            Console.WriteLine("dibujo n=" + n + " caja=(" + x0.ToString("0") + "," + y0.ToString("0") + ")-(" + x1.ToString("0") + "," + y1.ToString("0") + ") color=" + c0.ToString("X8") +
                " mezcla=" + mezcla + " etapas=" + etapas + " tex=" + tex + " filtro=2,2" + " destino=" + (enFondo ? "fondo" : ancho + "x" + alto));
        }
        private static int Factor(uint g)
        {
            switch (g)
            {
                case GlMas.ZERO: return 1; case GL.ONE: return 2; case GlMas.SRC_COLOR: return 3; case GlMas.ONE_MINUS_SRC_COLOR: return 4;
                case GL.SRC_ALPHA: return 5; case GL.ONE_MINUS_SRC_ALPHA: return 6; case GlMas.DST_ALPHA: return 7; case GlMas.ONE_MINUS_DST_ALPHA: return 8;
                case GlMas.DST_COLOR: return 9; case GlMas.ONE_MINUS_DST_COLOR: return 10; default: return 0;
            }
        }
    }
}
#endif
