// EL CIELO VIVO DE ROCOLA OS, GRUPO «SOL, LUNA Y ESTRELLAS»: DN4 (LAS ESTRELLAS SE ENCIENDEN DE UNA EN UNA). PLAN_CIELO_VIVO.md §1 y §8;
// FASE11_cielo-sol.md. Solo la tarjeta. Clave estrellas_anochecer= (funcion cielo).
//
// XP pinta su campo de estrellas siempre a todo su brillo (y DN1, g126, lo baja todo junto por la fase del dia: «a medias al alba y al
// anochecer»). Con DN4 encendida el campo deja de bajar junto (RocolaOsCielo.EstrellasPorEstrella) y cada estrella lleva su propio umbral:
// la «luz de estrellas» p (RocolaOsCielo.EstrellasCuadro: 0 de dia, 0,12 al ponerse el sol, 0,55 a mitad del crepusculo, 1 con la noche cerrada,
// la misma curva que da la foto con los pesos de §5, interpolada) enciende a la estrella i cuando p pasa de su umbral (en 0,1 de p, suave): las
// grandes primero, luego las medianas y al final el polvo, y arriba antes que abajo (el cielo oscurece de arriba hacia el horizonte); en la
// manana se apagan en el orden contrario. Con la noche cerrada (p = 1) todas valen exactamente 1: el cielo es el de XP, bit a bit (regla 1).
// DN4 no depende de DN1 (la curva sale de la hora del sol, que siempre se calcula): sin DN1 de dia el cielo sigue siendo el de XP (oscuro), y
// las estrellas se apagan igual.
//
// LAS FUGACES AL AZAR DE XP (UpdateAndDrawShoots, D3D9Star.cs): no se apagaban con la fase (g126 solo toca las estrellas) y de dia son rayas
// blancas sumadas sobre un cielo claro. Con DN4 se atenuan con la misma luz de estrellas (g300 y g301: las que salen al azar llevan una marca en
// el bit 31 de su color, que XP no mira; la de festejo, no, y esa se ve siempre).
using System;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal sealed class RocolaOsCieloEstrellasDN4 : RocolaOsCieloEstrella
    {
        internal RocolaOsCieloEstrellasDN4() { Quien = "DN4 las estrellas de una en una"; Orden = RocolaOsCieloOrden.Estrellas; }
        internal override bool Activo { get { RocolaOsCieloFoto f = RocolaOsCielo.Foto; return f != null && f.DN4; } }
        // los umbrales por tamano (0 polvo, 1 mediana, 2 grande): el polvo va al final
        private static readonly float[] Base = { 0.55f, 0.30f, 0.0f };
        internal const float Ancho = 0.10f;           // lo que tarda cada estrella en encenderse, en p
        // EL UMBRAL de la estrella i, de tamano k, a la altura y (px) de una escena de alto h: entre 0 y 0,88 (con la noche cerrada, p = 1, todas encendidas)
        internal static float Umbral(int i, float y, int h, int k)
        {
            uint a = (uint)i * 2654435761u; a ^= a >> 15; a *= 2246822519u; a ^= a >> 13;
            float azar = (a & 0xFFFF) / 65536f;
            float yf = h > 0 ? Math.Max(0f, Math.Min(1f, y / h)) : 0.5f;
            int kk = k < 0 ? 0 : (k > 2 ? 2 : k);
            return 0.88f * (Base[kk] + 0.30f * azar + 0.15f * yf) / 1.05f;
        }
        // el brillo de la estrella (0 a 1) con la luz de estrellas p
        internal static float Brillo(float p, float umbral)
        {
            if (p >= 1f) return 1f;
            float t = (p - umbral) / Ancho;
            if (!(t > 0f)) return 0f; if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
        internal override float Brillo(bool actual, int i, float x, float y, int tamano)
        {
            float p = RocolaOsCielo.EstrellasCuadro;
            if (p >= 1f) return 1f;
            return Brillo(p, Umbral(i, y, RocolaOsCielo.Escena.Alto, tamano));
        }
        // (el que pone EstrellasPorEstrella antes de cada cuadro: g126 lo lee al armar las estrellas)
        private sealed class CampoDe : RocolaOsCieloPintor
        {
            internal CampoDe() { Quien = "DN4 (el campo de estrellas)"; Orden = RocolaOsCieloOrden.Estrellas; }
            internal override void Pinta(GpuPanel p, int capa) { }
            internal override void Prepara(GpuPanel p)
            {
                RocolaOsCieloFoto f = RocolaOsCielo.Foto;
                RocolaOsCielo.EstrellasPorEstrella = f != null && f.DN4;
            }
        }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOsCielo.PonEstrella(new RocolaOsCieloEstrellasDN4());
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasFondo, new CampoDe());
        }
    }

    internal static partial class RocolaOsCielo
    {
        // g300: la luz de las fugaces al azar de XP (1 si es la de festejo, sin DN4 o con la noche cerrada)
        internal static float FugazAzar(uint col)
        {
            if ((col & RocolaOsCieloEstrellasMarca.Bit) == 0) return 1f;
            RocolaOsCieloFoto f = Foto;
            if (f == null || !f.DN4 || NucleoCaido) return 1f;
            float p = EstrellasCuadro;
            if (p >= 1f) return 1f;
            if (!(p > 0f)) return 0f;
            return p * p * (3f - 2f * p);
        }
    }
    internal static class RocolaOsCieloEstrellasMarca { internal const uint Bit = 0x80000000u; }

    internal sealed partial class GpuStarfield
    {
        // g300 (UpdateAndDrawShoots, el alfa de cada fugaz): la luz de las fugaces al azar (1 sin ROCOLA_OS)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float RocolaOsFugazAzar(uint col) { return RocolaOs.Activo && ReferenceEquals(this, RocolaOsCielo.Principal) ? RocolaOsCielo.FugazAzar(col) : 1f; }
        // g301 (al sacar una fugaz al azar): su marca (el bit 31 del color, que XP no mira: solo usa los 24 de abajo); 0 sin ROCOLA_OS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint RocolaOsMarcaAzar() { return RocolaOs.Activo ? RocolaOsCieloEstrellasMarca.Bit : 0u; }
    }
}
