using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// EL ENCABEZADO NUEVO, dibujado por el propio programa, en varias situaciones reales.
class Cabecera {
    static Type th; static FontFamily fam; static MethodInfo gc;
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static Bitmap Uno(int w,int h,string sec,int pag,int tot,Color ac){
        Control c=(Control)Activator.CreateInstance(th,true);
        c.SetBounds(0,0,w,h); c.Font=new Font(fam,9f);
        th.GetField("Section").SetValue(c,sec); th.GetField("Page").SetValue(c,pag);
        th.GetField("Total").SetValue(c,tot); th.GetField("Accent").SetValue(c,ac);
        Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
        using(Graphics g=Graphics.FromImage(b)){
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using(LinearGradientBrush lb=new LinearGradientBrush(new Rectangle(0,0,w,h),
                Color.FromArgb(11,14,32),Color.FromArgb(16,20,44),90f)) g.FillRectangle(lb,0,0,w,h);
            th.GetMethod("RenderContent",new Type[]{typeof(Graphics)}).Invoke(c,new object[]{g});
        }
        return b; }
    static int Main(){
        fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true);
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="HeaderView") th=t;
        gc=tf.GetMethod("GenreToColor",BindingFlags.Static|BindingFlags.NonPublic);
        Color oro=Color.FromArgb(255,205,90), vid=Color.FromArgb(140,220,232);
        object[][] casos={
            new object[]{"LO MAS SONADO",0,1,oro,      28,"lo mas sonado, una sola pagina"},
            new object[]{"Letra M",2,7,gc.Invoke(null,new object[]{"Cumbia"}),   28,"una letra con 7 paginas, estas en la 3a"},
            new object[]{"Letra R",0,3,gc.Invoke(null,new object[]{"Rock"}),     28,"tres paginas, en la primera"},
            new object[]{"VIDEOS",5,12,vid,            28,"videos, doce paginas (el ultimo caso con puntitos)"},
            new object[]{"Letra A",17,40,gc.Invoke(null,new object[]{"Metal"}),  28,"cuarenta paginas: ya no caben puntitos, va con numeros"},
            new object[]{"Letra M",2,7,gc.Invoke(null,new object[]{"Cumbia"}),   52,"el mismo, en una pantalla grande (encabezado de 52 px)"},
        };
        int W=980, alto=54; foreach(object[] c in casos) alto+=22+(int)c[4]+16;
        using(Bitmap bmp=new Bitmap(W,alto,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(bmp)){
            using(SolidBrush f=new SolidBrush(Color.FromArgb(7,9,20))) g.FillRectangle(f,0,0,W,alto);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                g.DrawString("EL ENCABEZADO NUEVO",ft,b,14,12);
            using(Font fn=new Font(fam,8.5f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(140,150,180)))
                g.DrawString("antes decia:  LO MAS SONADO    -    pagina 1 de 3",fn,b,14,32);
            int y=54;
            foreach(object[] c in casos){
                int h=(int)c[4];
                using(Font fn=new Font(fam,8f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(120,132,162)))
                    g.DrawString((string)c[5],fn,b,14,y+2);
                using(Bitmap t=Uno(W-28,h,(string)c[0],(int)c[1],(int)c[2],(Color)c[3]))
                    g.DrawImageUnscaled(t,14,y+18);
                y+=22+h+16; }
            bmp.Save("cabecera.png",ImageFormat.Png); }
        Console.WriteLine("listo: cabecera.png"); return 0; }
}
