using System; using System.Diagnostics; using System.Drawing; using System.Drawing.Imaging;
// Lo que cuesta la textura de UI aparte de la lista: borrarla y las piezas sueltas.
class Mide2 {
  static double Min(int reps, Action f){ f(); f(); double m=double.MaxValue;
    for(int b=0;b<7;b++){ Stopwatch sw=Stopwatch.StartNew(); for(int i=0;i<reps;i++) f(); sw.Stop();
      double ms=sw.Elapsed.TotalMilliseconds/reps; if(ms<m) m=ms; } return m; }
  static void Main(){
    foreach(int[] wh in new int[][]{ new int[]{1024,700}, new int[]{1920,1000} }){
      int W=wh[0],H=wh[1];
      Bitmap b=new Bitmap(W,H,PixelFormat.Format32bppPArgb);
      Graphics g=Graphics.FromImage(b);
      double clr=Min(40,delegate{ g.Clear(Color.Transparent); });
      double fill=Min(40,delegate{ g.FillRectangle(Brushes.Black,0,0,W,H); });
      Bitmap chico=new Bitmap(980,532,PixelFormat.Format32bppPArgb);
      double blit=Min(200,delegate{ g.DrawImageUnscaled(chico,20,60); });
      Console.WriteLine("  "+W+"x"+H+":  borrar la textura entera "+clr.ToString("0.000")
        +" ms   rellenarla "+fill.ToString("0.000")+" ms   copiar la lista dentro "+blit.ToString("0.000")+" ms");
      g.Dispose(); b.Dispose(); chico.Dispose();
    }
  }
}
