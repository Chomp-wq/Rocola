#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal enum AsistenteGestoGpu { Reposo, Saludar, Explicar, Buscar, Mirar, Confirmar, Volver, Credito, Despedir }

    // Una pose pequena de valores, sin objetos/imagenes por cuadro. La cara y
    // los miembros comparten el reloj fino y se mezclan al interrumpir un gesto.
    internal struct AsistenteExpresionGpu
    {
        public float ManoIzqX, ManoIzqY, ManoDerX, ManoDerY, PieIzqY, PieDerY;
        public float Ojos, Guino, MiraX, MiraY, Cejas, Sonrisa, Boca;
        public float Inclinacion, Rebote, Escala;
    }

    internal sealed partial class GpuPanel
    {
        private AsistenteExpresionGpu asistenteExpresion = AsistenteNeutral();
        private AsistenteExpresionGpu asistenteExpresionDesde = AsistenteNeutral();
        private int asistenteEventoVisto = Int32.MinValue;
        private double asistenteGestoInicio, asistenteMezclaInicio;
        private readonly D3D9.Vtx[] asistenteCara = new D3D9.Vtx[2200];

        internal static AsistenteExpresionGpu AsistenteNeutral()
        {
            return new AsistenteExpresionGpu { ManoIzqX = -1.18f, ManoIzqY = .60f,
                ManoDerX = 1.17f, ManoDerY = -.16f, PieIzqY = 1.40f, PieDerY = 1.40f,
                Ojos = 1, Sonrisa = .7f, Escala = 1 };
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
            // Parpadeo breve y organico: no aleatorio ni ligado al numero de FPS.
            e.Ojos = 1 - .97f * AsistenteImpulso(vida % 5.7, 4.8, .24);
            e.Inclinacion = .022f * (float)Math.Sin(vida * 1.35);
            e.Rebote = -.014f * (float)(1 - Math.Cos(vida * 1.8));
            e.MiraX = .025f * (float)Math.Sin(vida * .65);
            e.Cejas = -.006f * (float)(1 - Math.Cos(vida));
            // Tras un rato espera con un pequeno pasito, no repite el saludo.
            float espera = AsistenteImpulso(vida % 13, 8, 3);
            e.PieIzqY -= .08f * espera * (float)(.5 + .5 * Math.Sin(vida * 5));
            e.PieDerY -= .08f * espera * (float)(.5 - .5 * Math.Sin(vida * 5));
            float fuerza = AsistenteSuaviza(edad / .18) * (1 - AsistenteSuaviza((edad - 3.8) / .45));
            switch (gesto)
            {
                case AsistenteGestoGpu.Saludar:
                    e.ManoDerY += fuerza * (-.58f + .14f * (float)Math.Sin(edad * 11));
                    e.ManoDerX += fuerza * .05f * (float)Math.Sin(edad * 11);
                    e.Sonrisa += .25f * fuerza; e.Cejas -= .04f * fuerza;
                    e.Guino = AsistenteImpulso(edad, 2.2, .34);
                    e.Rebote -= .05f * AsistenteImpulso(edad, .05, .6);
                    e.Escala += .035f * AsistenteImpulso(edad, .05, .6);
                    break;
                case AsistenteGestoGpu.Explicar:
                    e.ManoDerY += fuerza * (.38f + .08f * (float)Math.Sin(edad * 4.2));
                    e.ManoIzqY -= .32f * fuerza;
                    e.MiraX = .036f * fuerza; e.MiraY = .02f * fuerza;
                    e.Boca = fuerza * (float)(.22 + .22 * Math.Sin(edad * 6.4) * Math.Sin(edad * 6.4));
                    e.Cejas -= .025f * fuerza;
                    e.Inclinacion += .045f * fuerza * (float)Math.Sin(edad * 3);
                    break;
                case AsistenteGestoGpu.Buscar:
                    // Mano en la barbilla, mirada curiosa, luego invita al globo.
                    float piensa = fuerza * (1 - AsistenteSuaviza((edad - 1.4) / .7));
                    e.ManoIzqX += .81f * piensa; e.ManoIzqY -= .26f * piensa;
                    e.Inclinacion -= .07f * piensa; e.MiraY = -.027f * piensa;
                    e.Cejas -= .04f * piensa;
                    e.ManoDerY += .37f * fuerza * AsistenteSuaviza((edad - 1.4) / .7);
                    break;
                case AsistenteGestoGpu.Mirar:
                    e.MiraX = .038f; e.MiraY = seleccion == 0 ? .027f : .046f;
                    e.ManoDerY += fuerza * (seleccion == 0 ? .39f : .57f);
                    e.Inclinacion += .04f * fuerza; e.Cejas -= .025f * fuerza;
                    break;
                case AsistenteGestoGpu.Confirmar:
                    e.ManoIzqY -= .76f * fuerza; e.ManoDerY -= .29f * fuerza;
                    e.Sonrisa += .3f * fuerza;
                    e.Rebote -= .055f * AsistenteImpulso(edad, 0, .7);
                    e.Guino = AsistenteImpulso(edad, .22, .32);
                    break;
                case AsistenteGestoGpu.Volver:
                    e.ManoIzqY -= .7f * fuerza;
                    e.Inclinacion -= .065f * fuerza;
                    e.MiraX -= .025f * fuerza; e.Sonrisa += .2f * fuerza;
                    break;
                case AsistenteGestoGpu.Credito:
                    e.ManoIzqY -= 1.05f * fuerza; e.ManoDerY -= .36f * fuerza;
                    e.Rebote -= .10f * AsistenteImpulso(edad, 0, .85);
                    e.Cejas -= .05f * fuerza; e.Sonrisa = .7f + .3f * fuerza;
                    break;
                case AsistenteGestoGpu.Despedir:
                    e.ManoDerY -= fuerza * (.6f - .13f * (float)Math.Sin(edad * 13));
                    e.Sonrisa += .25f * fuerza;
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
                PieIzqY = a.PieIzqY + (b.PieIzqY-a.PieIzqY)*p, PieDerY = a.PieDerY + (b.PieDerY-a.PieDerY)*p,
                Ojos = a.Ojos+(b.Ojos-a.Ojos)*p, Guino = a.Guino+(b.Guino-a.Guino)*p,
                MiraX = a.MiraX+(b.MiraX-a.MiraX)*p, MiraY = a.MiraY+(b.MiraY-a.MiraY)*p,
                Cejas = a.Cejas+(b.Cejas-a.Cejas)*p, Sonrisa = a.Sonrisa+(b.Sonrisa-a.Sonrisa)*p, Boca = a.Boca+(b.Boca-a.Boca)*p,
                Inclinacion = a.Inclinacion+(b.Inclinacion-a.Inclinacion)*p, Rebote = a.Rebote+(b.Rebote-a.Rebote)*p,
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
            if (v == null || v.Length < 2600 || !(radio > 0) || float.IsInfinity(radio)
                || float.IsNaN(cx) || float.IsInfinity(cx) || float.IsNaN(cy) || float.IsInfinity(cy)) return 0;
            cantidad = SafeMoonLevel(cantidad); alfa = SafeMoonLevel(alfa);
            if (cantidad <= 0 || alfa <= 0) return 0;
            float r = radio*cantidad, ancho = Math.Max(2, radio*.055f)*cantidad;
            uint negro = ((uint)(alfa*255)<<24)|0x0006080Du; int n = 0;
            float izqY = .18f + .20f*e.ManoIzqY, derY = .12f + .20f*e.ManoDerY;
            AsistenteSegmento(v, ref n, cx-r*.43f, cy+r*.03f, cx-r*.98f, cy+r*izqY, ancho, negro);
            AsistenteSegmento(v, ref n, cx-r*.98f, cy+r*izqY, cx+r*e.ManoIzqX, cy+r*e.ManoIzqY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.43f, cy+r*.03f, cx+r*.98f, cy+r*derY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.98f, cy+r*derY, cx+r*e.ManoDerX, cy+r*e.ManoDerY, ancho, negro);
            AsistenteDisco(v, ref n, cx+r*e.ManoIzqX, cy+r*e.ManoIzqY, ancho*1.7f, negro);
            AsistenteDisco(v, ref n, cx+r*e.ManoDerX, cy+r*e.ManoDerY, ancho*1.7f, negro);
            AsistenteSegmento(v, ref n, cx-r*.38f, cy+r*.51f, cx-r*.43f, cy+r*1.02f, ancho, negro);
            AsistenteSegmento(v, ref n, cx-r*.43f, cy+r*1.02f, cx-r*.37f, cy+r*e.PieIzqY, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.38f, cy+r*.51f, cx+r*.43f, cy+r*1.02f, ancho, negro);
            AsistenteSegmento(v, ref n, cx+r*.43f, cy+r*1.02f, cx+r*.39f, cy+r*e.PieDerY, ancho, negro);
            AsistenteSegmento(v, ref n, cx-r*.37f, cy+r*e.PieIzqY, cx-r*.56f, cy+r*(e.PieIzqY+.03f), ancho*1.42f, negro);
            AsistenteSegmento(v, ref n, cx+r*.39f, cy+r*e.PieDerY, cx+r*.58f, cy+r*(e.PieDerY+.03f), ancho*1.42f, negro);
            AsistenteRotaMalla(v,n,cx,cy,giro); return n;
        }

        // Cara por delante del material original 2D/3D: sin una placa ni fondo
        // rectangular. El negro de manos/cara y el blanco de ojos siempre legibles.
        internal static int AsistenteMallaCara(D3D9.Vtx[] v, float cx, float cy, float radio,
            float cantidad, float alfa, AsistenteExpresionGpu e, float giro)
        {
            if (v == null || v.Length < 2200 || !(radio > 0) || float.IsInfinity(radio)
                || float.IsNaN(cx) || float.IsInfinity(cx) || float.IsNaN(cy) || float.IsInfinity(cy)) return 0;
            cantidad=SafeMoonLevel(cantidad); alfa=SafeMoonLevel(alfa)*cantidad;
            if (alfa <= 0) return 0;
            uint negro=((uint)(alfa*255)<<24)|0x0006080Du, blanco=((uint)(alfa*255)<<24)|0x00F4FAFFu;
            int n=0; float r=radio;
            // Manos visibles tambien al llevar una de ellas a la barbilla.
            AsistenteDisco(v,ref n,cx+r*e.ManoIzqX,cy+r*e.ManoIzqY,r*.094f*cantidad,negro);
            AsistenteDisco(v,ref n,cx+r*e.ManoDerX,cy+r*e.ManoDerY,r*.094f*cantidad,negro);
            for(int i=0;i<2;i++) {
                float x=cx+(i==0 ? -.185f : .185f)*r, y=cy-r*.105f;
                float abierto=Math.Max(.02f,e.Ojos*(i==1 ? 1-.97f*e.Guino : 1));
                AsistenteOvalo(v,ref n,x,y,r*.144f,r*.195f*abierto,negro);
                AsistenteOvalo(v,ref n,x,y,r*.117f,r*.167f*abierto,blanco);
                AsistenteOvalo(v,ref n,x+r*e.MiraX,y+r*e.MiraY*abierto,r*.060f,r*.098f*abierto,negro);
                AsistenteOvalo(v,ref n,x+r*(e.MiraX-.016f),y+r*(e.MiraY-.033f)*abierto,r*.019f,r*.026f*abierto,blanco);
                float ceja=cy+r*(-.35f+e.Cejas);
                for(int k=0;k<4;k++) {
                    float a=(k-2)*.066f, b=(k-1)*.066f;
                    AsistenteTrazo(v,ref n,x+r*a,ceja+r*a*a*2,x+r*b,ceja+r*b*b*2,Math.Max(1,r*.018f),negro);
                }
            }
            for(int i=0;i<8;i++) {
                float a=(i-4)*.05f,b=(i-3)*.05f;
                float y0=cy+r*(.13f+e.Sonrisa*(.11f-2.75f*a*a));
                float y1=cy+r*(.13f+e.Sonrisa*(.11f-2.75f*b*b));
                AsistenteTrazo(v,ref n,cx+r*a,y0,cx+r*b,y1,Math.Max(1,r*.018f),negro);
            }
            if(e.Boca>.02f) AsistenteOvalo(v,ref n,cx,cy+r*.205f,r*.112f,r*.13f*e.Boca,negro);
            AsistenteRotaMalla(v,n,cx,cy,giro); return n;
        }

        private static void AsistenteTrazo(D3D9.Vtx[] v,ref int n,float x0,float y0,float x1,float y1,float ancho,uint color)
        {
            float dx=x1-x0,dy=y1-y0,l=(float)Math.Sqrt(dx*dx+dy*dy);
            if(l<=.001f) return;
            float px=-dy/l,py=dx/l,ax=px*ancho,ay=py*ancho; uint fuera=color&0xFFFFFFu;
            AsistenteCuadro(v,ref n,x0-ax,y0-ay,x1-ax,y1-ay,x1+ax,y1+ay,x0+ax,y0+ay,color,color,color,color);
            AsistenteCuadro(v,ref n,x0-ax-px,y0-ay-py,x1-ax-px,y1-ay-py,x1-ax,y1-ay,x0-ax,y0-ay,fuera,fuera,color,color);
            AsistenteCuadro(v,ref n,x0+ax,y0+ay,x1+ax,y1+ay,x1+ax+px,y1+ay+py,x0+ax+px,y0+ay+py,color,color,fuera,fuera);
        }

        private static void AsistenteOvalo(D3D9.Vtx[] v,ref int n,float x,float y,float rx,float ry,uint color)
        {
            rx=Math.Max(.05f,rx); ry=Math.Max(.05f,ry); uint fuera=color&0xFFFFFFu;
            for(int i=1;i<=12;i++) {
                float ax=asistenteCirculoX[i-1],ay=asistenteCirculoY[i-1],bx=asistenteCirculoX[i],by=asistenteCirculoY[i];
                V3(v,n++,x,y,color); V3(v,n++,x+ax*rx,y+ay*ry,color); V3(v,n++,x+bx*rx,y+by*ry,color);
                AsistenteCuadro(v,ref n,x+ax*rx,y+ay*ry,x+bx*rx,y+by*ry,x+bx*(rx+1),y+by*(ry+1),x+ax*(rx+1),y+ay*(ry+1),color,color,fuera,fuera);
            }
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
