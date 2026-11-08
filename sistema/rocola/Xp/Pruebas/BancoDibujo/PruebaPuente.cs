#if NATIVO
// BANCO DEL DIBUJO: lo que el puente (Plataforma/Puente.cs) promete, comprobado una por una: el tipografico de GDI+ y el nativo son EL MISMO
// objeto en los dos sentidos, un formato cualquiera pasa sin copiarse, el estado del lienzo va y vuelve, y un mapa que se presta no se suelta.
using System;
using System.Drawing;
using Rocola;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Banco
{
    internal static class PruebaPuente
    {
        private static int bien, mal;
        private static void Mira(bool c, string que) { if (c) bien++; else { mal++; Console.WriteLine("MAL: " + que); } }
        internal static int Hace()
        {
            EjecutaGdi.GenericosDeFabrica();
            StringFormat gt = StringFormat.GenericTypographic;
            Mira(ReferenceEquals(PuenteDibujo.Formato(gt), FormatoTexto.TipograficoCompartido), "GenericTypographic es FormatoTexto.TipograficoCompartido");
            Mira(ReferenceEquals(PuenteDibujo.StringFormat(FormatoTexto.TipograficoCompartido), gt), "y al reves, el mismo StringFormat");
            Mira(ReferenceEquals(PuenteDibujo.Formato(StringFormat.GenericDefault), FormatoTexto.NormalCompartido), "GenericDefault es FormatoTexto.NormalCompartido");
            // lo que hace EstadoReposoGpu.cs: Center, Center y |= NoWrap en el de GDI+ -> lo ve el nativo
            gt.Alignment = StringAlignment.Center; gt.LineAlignment = StringAlignment.Center; gt.FormatFlags |= StringFormatFlags.NoWrap;
            FormatoTexto t = FormatoTexto.TipograficoCompartido;
            Mira(t.Horizontal == Alinea.Centro && t.Vertical == Alinea.Centro, "la alineacion cambiada en GDI+ se ve en el nativo");
            Mira(t.Banderas == (BanderasTexto.CajaNegra | BanderasTexto.SoloEnteros | BanderasTexto.SinRecortar | BanderasTexto.SinPartir), "|= NoWrap anade (no reemplaza)");
            // y al reves: el nativo cambia, GDI+ lo ve (y una copia hecha despues sale con el cambio)
            t.Corte = CorteTexto.PuntosPorLetra; t.Banderas = BanderasTexto.SinPartir;
            Mira(StringFormat.GenericTypographic.Trimming == StringTrimming.EllipsisCharacter, "el recorte cambiado en el nativo se ve en GDI+");
            Mira(StringFormat.GenericTypographic.FormatFlags == StringFormatFlags.NoWrap, "= SinPartir deja solo esa bandera (reemplaza)");
            StringFormat copia = new StringFormat(StringFormat.GenericTypographic);
            Mira(copia.Trimming == StringTrimming.EllipsisCharacter && copia.FormatFlags == StringFormatFlags.NoWrap && PuenteDibujo.Formato(copia).SinMargen, "la copia sale con el cambio y tipografica");
            Mira(!ReferenceEquals(PuenteDibujo.Formato(copia), t), "la copia es otro objeto");
            // un formato cualquiera pasa sin copiarse
            FormatoTexto f = new FormatoTexto(); StringFormat sf = PuenteDibujo.StringFormat(f);
            sf.Alignment = StringAlignment.Far; Mira(f.Horizontal == Alinea.Lejos && ReferenceEquals(PuenteDibujo.Formato(sf), f), "un formato prestado es el mismo objeto");
            EjecutaGdi.GenericosDeFabrica();
            // el estado del lienzo va y vuelve
            using (SKBitmap b = Mapas.Nuevo(8, 8)) using (SKCanvas c = new SKCanvas(b))
                foreach (bool s in new[] { false, true }) foreach (bool cp in new[] { false, true }) foreach (bool m in new[] { false, true })
                            for (int l = 0; l < 6; l++) for (int fi = 0; fi < 8; fi++)
                                {
                                    SKMatrix hdc = SKMatrix.CreateTranslation(fi, l);
                                    EstadoLienzo e = new EstadoLienzo(s, (AjusteLetra)l, cp, (Filtro)fi, m, hdc);
                                    using (Graphics g = PuenteDibujo.Graphics(c, e))
                                    {
                                        EstadoLienzo r = PuenteDibujo.Estado(g);
                                        Mira(r.Suave == s && r.Copia == cp && r.Medio == m && r.Letra == e.Letra && r.Filtro == e.Filtro && r.Hdc.Equals(hdc) && ReferenceEquals(PuenteDibujo.Lienzo(g), c), "el estado va y vuelve");
                                    }
                                }
            // un mapa prestado no se suelta al soltar el Bitmap
            SKBitmap pres = Mapas.Nuevo(4, 4); pres.SetPixel(1, 1, new SKColor(10, 20, 30, 255));
            using (Bitmap bm = PuenteDibujo.Bitmap(pres)) Mira(ReferenceEquals(PuenteDibujo.Mapa(bm), pres) && bm.GetPixel(1, 1).R == 10, "el Bitmap prestado tiene los mismos puntos");
            Mira(pres.Handle != IntPtr.Zero && pres.GetPixel(1, 1).Green == 20, "al soltar el Bitmap prestado los puntos siguen");
            // el mismo SKBitmap da el mismo Bitmap (XP compara por identidad para no volver a subir); soltado u otro formato, otro
            Bitmap p1 = PuenteDibujo.Bitmap(pres), p2 = PuenteDibujo.Bitmap(pres);
            Mira(ReferenceEquals(p1, p2), "el mismo SKBitmap da el mismo Bitmap prestado");
            Bitmap p3 = PuenteDibujo.Bitmap(pres, PixelFormat.Format32bppArgb);
            Mira(!ReferenceEquals(p1, p3) && p3.PixelFormat == PixelFormat.Format32bppArgb && ReferenceEquals(PuenteDibujo.Mapa(p3), pres), "pedido con otro formato, otro Bitmap con los mismos puntos");
            p3.Dispose(); Bitmap p4 = PuenteDibujo.Bitmap(pres, PixelFormat.Format32bppArgb);
            Mira(!ReferenceEquals(p3, p4) && ReferenceEquals(PuenteDibujo.Mapa(p4), pres) && pres.GetPixel(1, 1).Red == 10, "soltado el prestado, el siguiente es otro (y los puntos siguen)");
            pres.Dispose();
            // el mapa de un horno traducido (D4), entregado a quien lo pide como Bitmap: los mismos puntos, el formato de XP, y se hace su dueno
            SKBitmap ent = Mapas.Nuevo(3, 3); ent.SetPixel(2, 2, new SKColor(1, 2, 3, 255));
            Bitmap be = PuenteDibujo.Entrega(ent);
            Mira(ReferenceEquals(PuenteDibujo.Mapa(be), ent) && be.PixelFormat == PixelFormat.Format32bppArgb && be.GetPixel(2, 2).B == 3, "el mapa entregado: los mismos puntos y Format32bppArgb");
            using (Bitmap nb = new Bitmap(3, 3, PixelFormat.Format32bppArgb)) Mira(be.Flags == nb.Flags && be.Width == nb.Width && be.Height == nb.Height, "como un new Bitmap(w, h, Format32bppArgb): las mismas marcas (con alfa)");
            be.Dispose();
            Mira(ent.Handle == IntPtr.Zero, "al soltar el Bitmap entregado se sueltan los puntos (se hizo su dueno)");
            Mira(PuenteDibujo.Entrega(null) == null, "nada entregado, nada");
            // una letra y su Font: los mismos datos
            using (Font fo = new Font("Trebuchet MS", 15.3f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                Letra le = PuenteDibujo.Letra(fo);
                using (Font f2 = PuenteDibujo.Font(le))
                    Mira(f2.Name == fo.Name && f2.Size == fo.Size && f2.Unit == fo.Unit && f2.Style == fo.Style && f2.GetHeight() == fo.GetHeight() && f2.Height == fo.Height && f2.SizeInPoints == fo.SizeInPoints, "Font -> Letra -> Font con los mismos datos");
            }
            Console.WriteLine("puente: " + bien + " comprobaciones bien, " + mal + " mal");
            return mal == 0 ? 0 : 1;
        }
    }
}
#endif
