// EL CIELO VIVO DE ROCOLA OS, GRUPO «SOL, LUNA Y ESTRELLAS»: DN3 (LA LUNA). PLAN_CIELO_VIVO.md §1 y §8; FASE11_cielo-sol.md. Solo la tarjeta.
//
// LA LUNA DE XP (CieloVivoGpu.cs: HorneaLuna, EnsureLunaCielo, DibujaLunaCielo) ya tiene su fase real (la media, FaseLunar) y su sitio (arriba a la
// derecha, junto a la estrella: SitioLuna) y se pinta tambien de dia. DN3 la toma con TomaLaLuna (g132) en dos casos y no toca nada mas:
//   A (luna_camino=0, la de fabrica; con DN1 encendida, que es la que pone el cielo de dia): de dia (el cielo de DN1, con el peso de la noche
//      bajo 1) la luna sigue en su sitio de XP pero solo se ve si de verdad esta sobre el horizonte, palida (0,55 y su halo al cuadrado) como se
//      ve de dia en el cielo; al anochecer sube a su brillo de XP y, con la noche cerrada (la noche de XP: regla 1) la pinta XP tal cual.
//   B (luna_camino=1): la luna en su CAMINO REAL (la altura y el acimut de la foto, la misma proyeccion que el sol): mas grande cerca del
//      horizonte (hasta x 1,3), el horizonte la corta y asoma al salir y ponerse; siempre detras de todo (capa ConLaLuna: antes de la estrella, las
//      portadas y los textos). Solo se ve sobre el horizonte; de noche con su brillo de XP, de dia palida. En la pared (rejilla de portadas a la
//      vista) solo asoma en la franja de 0,848 a 0,868 del horizonte, como el sol (nunca detras de un titulo). Es la unica de las ideas del
//      grupo que pinta algo distinto en la noche cerrada (la regla 1 lo permite: «la luna B»).
// La textura de la luna es la de XP (la hornea EnsureLunaCielo): cambia cada ~6 h de fase. Con «Ver como se veria» o ROCOLA_CIELO_LUNA=1 (las
// pruebas, con ROCOLA_CIELO_FECHA) la fase sale de la hora del cielo y no del reloj (gancho g302); si no, la de XP, sin tocar.
using System;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal sealed class RocolaOsCieloLuna : RocolaOsCieloPintor
    {
        internal RocolaOsCieloLuna() { Quien = "DN3 la luna"; Orden = RocolaOsCieloOrden.Luna; }
        private bool toma, esB;
        private double sEste, sAlt, sDia = -1; private bool listo;
        internal override bool TomaLaLuna(GpuPanel p)
        {
            toma = false;
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.Cielo) { listo = false; return false; }
            if (f.DN3B) { toma = true; esB = true; return true; }
            listo = false;
            if (f.DN1 && RocolaOsCielo.NocheCuadro < 1f) { toma = true; esB = false; return true; }
            return false;   // la noche cerrada: la luna de XP, tal cual (regla 1)
        }
        internal override void Pinta(GpuPanel p, int capa)
        {
            if (!toma || capa != RocolaOsCieloCapa.ConLaLuna) return;
            toma = false;
            RocolaOsCieloFoto f = RocolaOsCielo.Foto; RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (f == null || e.Ancho < 64 || e.Alto < 64) return;
            float noche = RocolaOsCielo.NocheCuadro;
            double kd = 1.0 - Math.Exp(-e.Dt / 0.5);
            double altT = f.LunaAlt;
            if (!listo) { sEste = RocolaOsCieloAstros.Este(f.LunaAlt, f.LunaAz); sAlt = altT; sDia = -1; listo = true; }
            else { sEste += (RocolaOsCieloAstros.Este(f.LunaAlt, f.LunaAz) - sEste) * kd; sAlt += (altT - sAlt) * kd; }
            // de dia, palida: el peso del dia (1 - noche) la lleva de 1 (noche) a 0,55; con la luna bajo el horizonte, de dia, se apaga
            float visible = RocolaOsCieloAstros.Paso(sAlt, -2.0, 4.0);
            float dia = (1f - noche), alfa = noche + dia * 0.55f * visible;
            if (!esB)
            {
                // A: en su sitio de XP; de dia solo si esta sobre el horizonte
                float cx, cy, r; Form1.SitioLuna(e.Ancho, e.Alto, out cx, out cy, out r);
                p.RocolaOsLunaPinta(cx, cy, r, alfa, dia > 0f ? alfa * alfa : alfa, false, -2e9f, 2e9f);
                return;
            }
            if (sAlt < -3.0) return;
            bool pared = RocolaOsCieloAstros.ConPortadas(e);
            float hz = e.Horizonte, x, y, rr, yTop = -2e9f, yBot = hz;
            float c0, c1, r0; Form1.SitioLuna(e.Ancho, e.Alto, out c0, out c1, out r0);
            float cerca = 1f + 0.3f * (1f - (float)Math.Max(0.0, Math.Sin(Math.Max(0.0, sAlt) * Math.PI / 180.0)));   // mas grande cerca del horizonte
            rr = r0 * cerca * (pared ? RocolaOsCieloAstros.RadioParedK : 1f);
            float kAsoma = 1f;
            if (pared)
            {
                RocolaOsCieloAstros.Proyecta(sEste, 0.0, e.Ancho, hz, out x, out y);
                float s = (float)((Math.Max(-4.0, Math.Min(14.0, sAlt)) + 4.0) / 18.0);
                y = hz + 1.2f * rr - s * 2.6f * rr;
                kAsoma = 1f - RocolaOsCieloAstros.Paso(sAlt, 2.0, 9.0);
                yTop = hz * (RocolaOsCieloDia.Regla3Hasta / RocolaOsCieloDia.HorizonteMaqueta);
                if (kAsoma <= 0.003f) return;
            }
            else RocolaOsCieloAstros.Proyecta(sEste, sAlt, e.Ancho, hz, out x, out y);
            p.RocolaOsLunaPinta(x, y, rr, alfa * kAsoma, alfa * alfa * kAsoma, true, yTop, yBot);
        }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsCielo.PonPintor(RocolaOsCieloCapa.ConLaLuna, new RocolaOsCieloLuna()); }
    }

    internal static partial class RocolaOsCielo
    {
        // g302: la fase de la luna que hornea XP (cada 20 s): la de XP, salvo con «Ver como se veria» o ROCOLA_CIELO_LUNA=1 (las pruebas con
        // ROCOLA_CIELO_FECHA), que sale de la hora del cielo
        private static bool lunaPorFechaLeida, lunaPorFecha;
        internal static double FaseLunaPara(double deXp)
        {
            RocolaOsCieloFoto f = Foto;
            if (f == null || !f.Cielo || NucleoCaido) return deXp;
            if (!lunaPorFechaLeida) { lunaPorFechaLeida = true; lunaPorFecha = Environment.GetEnvironmentVariable("ROCOLA_CIELO_LUNA") == "1"; }
            return f.VerComo || lunaPorFecha ? f.LunaFase : deXp;
        }
    }

    internal sealed partial class GpuPanel
    {
        private readonly Tarjeta.Vtx[] RocolaOsLunaQuad = new Tarjeta.Vtx[6];
        // g302 (ActualizaCieloVivo): la fase de la luna de XP
        private double RocolaOsFaseLuna(double deXp) { return RocolaOs.Activo ? RocolaOsCielo.FaseLunaPara(deXp) : deXp; }
        // LA LUNA DE XP (su textura, la de EnsureLunaCielo) pintada donde y como diga DN3: centro, radio, alfa del disco y del halo (0 a 1), si el halo
        // lleva recorte (B: mallas) o es el de XP (DrawGlow), y el recorte (yTop, yBot: pixeles; sin recorte con +-2e9). Dentro del cuadro.
        internal void RocolaOsLunaPinta(float cx, float cy, float r, float alfa, float alfaHalo, bool recorta, float yTop, float yBot)
        {
            if (!LunaDelCielo || lunaCieloTex == IntPtr.Zero || stars == null || Width < 64 || Height < 64) return;
            double ilum = Form1.LunaIluminada(faseLuna);
            if (ilum < Form1.LunaMinima) return;
            float atenua = 0.6f + 0.4f * Math.Max(0f, Math.Min(1f, Dim));   // como XP: menos que las estrellas al navegar
            uint ha = (uint)Math.Max(0, Math.Min(255, (int)(70.0 * ilum * atenua * alfaHalo)));
            if (recorta) RocolaOsCieloAstros.Halo(stars, true, cx, cy, r * 2.6f, 0xC8D2EB, (float)(70.0 * ilum * atenua * alfaHalo / 255.0), 2f, yTop, yBot);
            else if (ha > 0) stars.DrawGlow(cx, cy, r * 2.6f, (ha << 24) | 0x00C8D2EBu);
            uint a = (uint)Math.Max(0, Math.Min(255, (int)(255f * atenua * alfa)));
            if (a == 0) return;
            float y0 = cy - r, y1 = cy + r, ya = Math.Max(y0, yTop), yb = Math.Min(y1, yBot);
            if (!(yb > ya)) return;
            SetTexQuadA(RocolaOsLunaQuad, cx - r, ya, cx + r, yb, lunaCieloU, lunaCieloV, (a << 24) | 0x00FFFFFFu);
            float va = lunaCieloV * (ya - y0) / (y1 - y0), vb = lunaCieloV * (yb - y0) / (y1 - y0);
            RocolaOsLunaQuad[0].V = RocolaOsLunaQuad[1].V = RocolaOsLunaQuad[3].V = va;
            RocolaOsLunaQuad[2].V = RocolaOsLunaQuad[4].V = RocolaOsLunaQuad[5].V = vb;
            stars.DrawQuad(lunaCieloTex, RocolaOsLunaQuad);
        }
    }
}
