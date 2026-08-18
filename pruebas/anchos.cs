using System; using System.Drawing; using System.Windows.Forms;
// Cuanto mide de verdad "VIDEO" y "TOP" comparado con el hueco que les toca.
class Anchos {
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static int Main(){
        FontFamily f=Fam(); Console.WriteLine("letra usada aqui: "+f.Name);
        using(Font fv=new Font(f,7.5f,FontStyle.Bold)) using(Font fm=new Font(f,10.5f,FontStyle.Bold))
        using(Bitmap b=new Bitmap(4,4)) using(Graphics g=Graphics.FromImage(b)){
            int n=29;
            int[] anchos={996,486};  // pantalla completa  /  con video al lado
            string[] como={"sin video (996 px)","CON VIDEO (486 px)"};
            for(int k=0;k<2;k++){
                float slot=(float)anchos[k]/n;
                float wv=g.MeasureString("VIDEO",fv).Width, wt=g.MeasureString("TOP",fv).Width, wm=g.MeasureString("W",fm).Width;
                Console.WriteLine("\n  "+como[k]+"   hueco por letra = "+slot.ToString("0.0")+" px");
                Console.WriteLine("     VIDEO mide "+wv.ToString("0.0")+" px  -> "+(wv<=slot?"cabe":"NO CABE, se parte en varios renglones"));
                Console.WriteLine("     TOP   mide "+wt.ToString("0.0")+" px  -> "+(wt<=slot?"cabe":"NO CABE, se parte en varios renglones"));
                Console.WriteLine("     W     mide "+wm.ToString("0.0")+" px  -> "+(wm<=slot?"cabe":"NO CABE"));
            }
        }
        return 0; }
}
