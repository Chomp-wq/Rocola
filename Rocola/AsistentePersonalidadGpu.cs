#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal enum AsistenteGestoGpu { Reposo, Saludar, Explicar, Buscar, Senalar, Confirmar, Volver, Credito, Despedir }

    // La estrella NO tiene rostro. Toda expresion viene de postura, brazos y
    // pasos; valores pequenos que se mezclan sin objetos/imagenes por cuadro.
    internal struct AsistenteExpresionGpu
    {
        public float ManoIzqX, ManoIzqY, ManoDerX, ManoDerY, PieIzqX, PieIzqY, PieDerX, PieDerY;
        public float Inclinacion, Rebote, Escala, Desplazamiento, Reverencia;
    }

    internal sealed partial class GpuPanel
    {
        private AsistenteExpresionGpu asistenteExpresion = AsistenteNeutral();
        private AsistenteExpresionGpu asistenteExpresionDesde = AsistenteNeutral();
        private int asistenteEventoVisto = Int32.MinValue;
        private double asistenteGestoInicio, asistenteMezclaInicio;

        internal static AsistenteExpresionGpu AsistenteNeutral()
        {
            return new AsistenteExpresionGpu { ManoIzqX = -1.18f, ManoIzqY = .60f,
                ManoDerX = 1.17f, ManoDerY = -.16f, PieIzqX = -.37f, PieIzqY = 1.40f,
                PieDerX = .39f, PieDerY = 1.40f, Escala = 1 };
        }

        private static double AsistenteTiempoSeguro(double t)
        { return double.IsNaN(t) || double.IsInfinity(t) ? 0 : Math.Max(0, t); }

        private static float AsistenteImpulso(double t, double inicio, double duracion)
        {
            double p = (t - inicio) / duracion;
            if (p <= 0 || p >= 1) return 0;
            double s = Math.Sin(p * Math.PI); return (float)(s * s);
        }

        internal static AsistenteExpresionGpu AsistenteActua(double vida, double edad,
            AsistenteGestoGpu gesto, int seleccion, bool animada)
        {
            AsistenteExpresionGpu e = AsistenteNeutral();
            if (!animada) return e;
            vida = AsistenteTiempoSeguro(vida); edad = AsistenteTiempoSeguro(edad);
            // Espera relajada: balanceo continuo, sin rasgos ni parpadeos faciales.
            e.Inclinacion = .022f * (float)Math.Sin(vida * 1.35);
            e.Rebote = -.014f * (float)(1 - Math.Cos(vida * 1.8));
            e.Desplazamiento = .012f * (float)Math.Sin(vida * .9);
            // Tras un rato espera con un pequeno pasito, no repite el saludo.
            float espera = AsistenteImpulso(vida % 13, 8, 3);
            e.PieIzqY -= .08f * espera * (float)(.5 + .5 * Math.Sin(vida * 5));
            e.PieDerY -= .08f * espera * (float)(.5 - .5 * Math.Sin(vida * 5));
            e.ManoIzqY += .05f * espera * (float)Math.Sin(vida * 2);
            float fuerza = AsistenteSuaviza(edad / .18) * (1 - AsistenteSuaviza((edad - 3.8) / .45));
            switch (gesto)
            {
                case AsistenteGestoGpu.Saludar:
                    e.ManoDerY += fuerza * (-.66f + .15f * (float)Math.Sin(edad * 11));
                    e.ManoDerX += fuerza * .07f * (float)Math.Sin(edad * 11);
                    e.ManoIzqY += .10f * fuerza;
                    e.Inclinacion -= .055f * fuerza;
                    e.PieIzqX -= .045f * fuerza;
                    e.Rebote -= .06f * AsistenteImpulso(edad, .05, .6);
                    e.Escala += .035f * AsistenteImpulso(edad, .05, .6);
                    break;
                case AsistenteGestoGpu.Explicar:
                    // Palmas abiertas: alterna presentar el globo y acompanarlo.
                    float presenta = (float)Math.Sin(edad * 4.2);
                    e.ManoDerY += fuerza * (.40f + .11f * presenta);
                    e.ManoDerX += .10f * fuerza;
                    e.ManoIzqY -= fuerza * (.45f + .10f * presenta);
                    e.ManoIzqX -= .04f * fuerza;
                    e.Inclinacion += .055f * fuerza * (float)Math.Sin(edad * 3);
                    e.Desplazamiento += .025f * fuerza;
                    e.Reverencia += .04f * fuerza * (float)(.5 + .5 * Math.Sin(edad * 3));
                    break;
                case AsistenteGestoGpu.Buscar:
                    // Recoge los brazos, se inclina curiosa y luego abre la mano
                    // hacia las opciones. No simula una cara sobre el icono.
                    float piensa = fuerza * (1 - AsistenteSuaviza((edad - 1.4) / .7));
                    float invita = fuerza * AsistenteSuaviza((edad - 1.4) / .7);
                    e.ManoIzqX += .73f * piensa; e.ManoIzqY -= .20f * piensa;
                    e.ManoDerX -= .69f * piensa; e.ManoDerY += .48f * piensa;
                    e.Inclinacion -= .075f * piensa;
                    e.Reverencia += .09f * piensa;
                    e.Desplazamiento -= .035f * piensa;
                    e.PieIzqX += .06f * piensa; e.PieDerX -= .06f * piensa;
                    e.ManoDerY += .40f * invita; e.ManoDerX += .11f * invita;
                    e.Inclinacion += .045f * invita;
                    break;
                case AsistenteGestoGpu.Senalar:
                    e.ManoDerX += .11f * fuerza;
                    e.ManoDerY += fuerza * (seleccion == 0 ? .39f : .57f);
                    e.Inclinacion += .05f * fuerza; e.Desplazamiento += .035f * fuerza;
                    e.PieDerX += .04f * fuerza;
                    break;
                case AsistenteGestoGpu.Confirmar:
                    e.ManoIzqY -= .76f * fuerza; e.ManoDerY -= .29f * fuerza;
                    e.ManoIzqX -= .04f * fuerza; e.ManoDerX += .05f * fuerza;
                    e.Rebote -= .08f * AsistenteImpulso(edad, 0, .7);
                    e.Reverencia += .15f * AsistenteImpulso(edad, .18, .75);
                    e.Inclinacion -= .06f * AsistenteImpulso(edad, .18, .75);
                    e.PieDerY -= .10f * AsistenteImpulso(edad, .18, .6);
                    break;
                case AsistenteGestoGpu.Volver:
                    e.ManoIzqY -= .48f * fuerza; e.ManoIzqX -= .12f * fuerza;
                    e.Inclinacion -= .075f * fuerza; e.Desplazamiento -= .045f * fuerza;
                    e.PieIzqX -= .09f * fuerza;
                    e.PieIzqY -= .12f * AsistenteImpulso(edad, .08, .8);
                    break;
                case AsistenteGestoGpu.Credito:
                    // Alegria reconocible sin sonrisa: ambos brazos y un saltito.
                    e.ManoIzqY -= 1.13f * fuerza; e.ManoDerY -= .66f * fuerza;
                    e.ManoIzqX += .17f * fuerza; e.ManoDerX -= .11f * fuerza;
                    float celebra = AsistenteImpulso(edad, 0, .85);
                    e.Rebote -= .13f * celebra; e.Escala += .04f * celebra;
                    e.PieIzqY -= .08f * celebra; e.PieDerY -= .08f * celebra;
                    e.Inclinacion += .055f * fuerza * (float)Math.Sin(edad * 5);
                    break;
                case AsistenteGestoGpu.Despedir:
                    e.ManoDerY -= fuerza * (.6f - .13f * (float)Math.Sin(edad * 13));
                    e.ManoDerX += .05f * fuerza * (float)Math.Sin(edad * 13);
                    e.Inclinacion -= .035f * fuerza;
                    e.Reverencia += .12f * AsistenteImpulso(edad, .1, .7);
                    break;
            }
            // Pequena respiracion del cuerpo; amplitud constante, sin retexturizar.
            e.Escala += .012f * (float)(1 - Math.Cos(vida * 1.8));
            return e;
        }

        internal static AsistenteExpresionGpu AsistenteMezcla(AsistenteExpresionGpu a, AsistenteExpresionGpu b, float p)
        {
            p = SafeMoonLevel(p);
            return new AsistenteExpresionGpu {
                ManoIzqX = a.ManoIzqX + (b.ManoIzqX-a.ManoIzqX)*p, ManoIzqY = a.ManoIzqY + (b.ManoIzqY-a.ManoIzqY)*p,
                ManoDerX = a.ManoDerX + (b.ManoDerX-a.ManoDerX)*p, ManoDerY = a.ManoDerY + (b.ManoDerY-a.ManoDerY)*p,
                PieIzqX = a.PieIzqX + (b.PieIzqX-a.PieIzqX)*p, PieIzqY = a.PieIzqY + (b.PieIzqY-a.PieIzqY)*p,
                PieDerX = a.PieDerX + (b.PieDerX-a.PieDerX)*p, PieDerY = a.PieDerY + (b.PieDerY-a.PieDerY)*p,
                Inclinacion = a.Inclinacion+(b.Inclinacion-a.Inclinacion)*p, Rebote = a.Rebote+(b.Rebote-a.Rebote)*p,
                Desplazamiento = a.Desplazamiento+(b.Desplazamiento-a.Desplazamiento)*p,
                Reverencia = a.Reverencia+(b.Reverencia-a.Reverencia)*p,
                Escala = a.Escala+(b.Escala-a.Escala)*p };
        }

        private void AvanzaPersonalidad(double ahora, bool entrada)
        {
            if (entrada) {
                asistenteExpresion = asistenteExpresionDesde = AsistenteNeutral();
                asistenteEventoVisto = asistenteEstado.Evento;
                asistenteGestoInicio = asistenteMezclaInicio = ahora + (asistenteEstado.Animada ? AsistenteSaltoSegundos : 0);
            }
            else if (asistenteEventoVisto != asistenteEstado.Evento) {
                asistenteExpresionDesde = asistenteExpresion;
                asistenteEventoVisto = asistenteEstado.Evento; asistenteGestoInicio = asistenteMezclaInicio = ahora;
            }
            double vida = Math.Max(0, ahora - asistenteInicio - AsistenteSaltoSegundos);
            AsistenteExpresionGpu hasta = AsistenteActua(vida, ahora - asistenteGestoInicio,
                asistenteEstado.Gesto, asistenteEstado.Seleccion, asistenteEstado.Animada);
            asistenteExpresion = asistenteEstado.Animada ? AsistenteMezcla(asistenteExpresionDesde, hasta,
                AsistenteSuaviza((ahora-asistenteMezclaInicio)/.14)) : AsistenteNeutral();
        }

        internal static int AsistenteMiembrosExpresivos(D3D9.Vtx[] v, float cx, float cy, float radio,
            float cantidad, float alfa, AsistenteExpresionGpu e, float giro)
        {
            if (!AsistenteGeometriaValida(v, 1900, cx, cy, radio)) return 0;
            cantidad = SafeMoonLevel(cantidad); alfa = SafeMoonLevel(alfa);
            if (cantidad <= 0 || alfa <= 0) return 0;
            float r = radio*cantidad, ancho = Math.Max(2, radio*.055f)*cantidad;
            uint negro = ((uint)(alfa*255)<<24)|0x0006080Du; int n = 0;
            float izqY = .18f + .20f*e.ManoIzqY, derY = .12f + .20f*e.ManoDerY;
            // Brazos superiores y piernas DETRAS del icono; los antebrazos van
            // delante para poder recoger/cruzar los brazos sin inventar un rostro.
            AsistenteSegmento(v, ref n, cx-r*.43f, cy+r*.03f, cx-r*.98f, cy+r*izqY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.43f, cy+r*.03f, cx+r*.98f, cy+r*derY, ancho, negro);
            AsistenteSegmento(v, ref n, cx-r*.38f, cy+r*.51f, cx-r*.43f, cy+r*1.02f, ancho, negro);
            AsistenteSegmento(v, ref n, cx-r*.43f, cy+r*1.02f, cx+r*e.PieIzqX, cy+r*e.PieIzqY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.38f, cy+r*.51f, cx+r*.43f, cy+r*1.02f, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.43f, cy+r*1.02f, cx+r*e.PieDerX, cy+r*e.PieDerY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*e.PieIzqX, cy+r*e.PieIzqY, cx+r*(e.PieIzqX-.19f), cy+r*(e.PieIzqY+.03f), ancho*1.42f, negro);
            AsistenteSegmento(v, ref n, cx+r*e.PieDerX, cy+r*e.PieDerY, cx+r*(e.PieDerX+.19f), cy+r*(e.PieDerY+.03f), ancho*1.42f, negro);
            AsistenteRotaMalla(v,n,cx,cy,giro); return n;
        }

        internal static int AsistenteBrazosExpresivos(D3D9.Vtx[] v, float cx, float cy, float radio,
            float cantidad, float alfa, AsistenteExpresionGpu e, float giro)
        {
            if (!AsistenteGeometriaValida(v, 700, cx, cy, radio)) return 0;
            cantidad = SafeMoonLevel(cantidad); alfa = SafeMoonLevel(alfa);
            if (cantidad <= 0 || alfa <= 0) return 0;
            int n = 0;
            AsistenteArmaBrazos(v, ref n, cx, cy, radio*cantidad,
                Math.Max(2, radio*.055f)*cantidad, ((uint)(alfa*255)<<24)|0x0006080Du, e);
            AsistenteRotaMalla(v,n,cx,cy,giro); return n;
        }

        private static bool AsistenteGeometriaValida(D3D9.Vtx[] v, int capacidad, float cx, float cy, float radio)
        {
            return v != null && v.Length >= capacidad && radio > 0 && !float.IsInfinity(radio)
                && !float.IsNaN(cx) && !float.IsInfinity(cx) && !float.IsNaN(cy) && !float.IsInfinity(cy);
        }

        private static void AsistenteArmaBrazos(D3D9.Vtx[] v, ref int n, float cx, float cy,
            float r, float ancho, uint negro, AsistenteExpresionGpu e)
        {
            float izqY = .18f + .20f*e.ManoIzqY, derY = .12f + .20f*e.ManoDerY;
            AsistenteSegmento(v, ref n, cx-r*.98f, cy+r*izqY, cx+r*e.ManoIzqX, cy+r*e.ManoIzqY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.98f, cy+r*derY, cx+r*e.ManoDerX, cy+r*e.ManoDerY, ancho, negro);
            AsistenteDisco(v, ref n, cx+r*e.ManoIzqX, cy+r*e.ManoIzqY, ancho*1.7f, negro);
            AsistenteDisco(v, ref n, cx+r*e.ManoDerX, cy+r*e.ManoDerY, ancho*1.7f, negro);
        }

        private static void AsistenteRotaMalla(D3D9.Vtx[] v,int n,float cx,float cy,float giro)
        {
            if(float.IsNaN(giro)||float.IsInfinity(giro)||Math.Abs(giro)<.00001f) return;
            float c=(float)Math.Cos(giro),s=(float)Math.Sin(giro);
            for(int i=0;i<n;i++) {
                float x=v[i].X-cx,y=v[i].Y-cy; v[i].X=cx+x*c-y*s; v[i].Y=cy+x*s+y*c;
            }
        }
    }
}
#endif
