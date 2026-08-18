using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// La barra de letras DE VERDAD, dibujada por el propio programa, a los dos anchos que usa: pantalla entera
// y la mitad (cuando hay un video puesto al lado).
class VideoBar {
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static int Main(){
        FontFamily fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true); Type tb=null;
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="LetterBar") tb=t;
        string L="★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
        HashSet<char> disp=new HashSet<char>(); foreach(char c in L) if("KQWXY".IndexOf(c)<0) disp.Add(c);
        int[] anchos={996,486}; string[] tit={"PANTALLA ENTERA  (996 px)","CON UN VIDEO PUESTO AL LADO  (486 px)"};
        int H=54+anchos.Length*(26+40+22);
        using(Bitmap bmp=new Bitmap(1024,H,PixelFormat.Format32bppArgb)) using(Graphics g=Graphics.FromImage(bmp)){
            using(SolidBrush f=new SolidBrush(Color.FromArgb(7,9,20))) g.FillRectangle(f,0,0,1024,H);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                g.DrawString("LA BARRA DE HOY, A SUS DOS ANCHOS",ft,b,14,14);
            using(Font fn=new Font(fam,8.5f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(150,160,190)))
                g.DrawString("dibujada por el propio programa; mira TOP y VIDEO en la de abajo",fn,b,14,34);
            int y=58;
            for(int k=0;k<anchos.Length;k++){
                using(Font ft=new Font(fam,9.5f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(150,190,250)))
                    g.DrawString(tit[k],ft,b,14,y);
                Control bar=(Control)Activator.CreateInstance(tb,true);
                bar.SetBounds(0,0,anchos[k],40); bar.Font=new Font(fam,9f);
                tb.GetField("Letters").SetValue(bar,L); tb.GetProperty("Current").SetValue(bar, 'M', null);
        // Current es una PROPIEDAD y al cambiarla arranca el deslizamiento de la capsula. Aqui se
        // mide la barra QUIETA, asi que se le deja terminar antes de dibujar.
        System.Threading.Thread.Sleep(230);
                tb.GetField("Available").SetValue(bar,disp); tb.GetField("Phase").SetValue(bar,0.6d);
                using(Bitmap tira=new Bitmap(anchos[k],40,PixelFormat.Format32bppPArgb)){
                    using(Graphics gt=Graphics.FromImage(tira)){
                        gt.SmoothingMode=SmoothingMode.AntiAlias;
                        gt.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        using(SolidBrush f2=new SolidBrush(Color.FromArgb(12,15,34))) gt.FillRectangle(f2,0,0,anchos[k],40);
                        tb.GetMethod("RenderContent",new Type[]{typeof(Graphics)}).Invoke(bar,new object[]{gt});
                    }
                    g.DrawImageUnscaled(tira,14,y+20);
                    using(Pen p=new Pen(Color.FromArgb(60,120,150,220))) g.DrawRectangle(p,14,y+20,anchos[k],40);
                }
                y+=26+40+22;
            }
            bmp.Save("barra_video.png",ImageFormat.Png);
        }
        Console.WriteLine("listo: barra_video.png"); return 0; }
}
