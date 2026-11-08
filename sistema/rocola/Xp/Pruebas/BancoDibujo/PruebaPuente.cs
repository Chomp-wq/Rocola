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
            pres.Dispose();
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
