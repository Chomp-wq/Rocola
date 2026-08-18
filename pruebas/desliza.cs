using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// EL DESLIZAMIENTO, cuadro a cuadro. Se cambia de letra y se dibuja la barra de verdad varias veces mientras
// la capsula viaja, para poder ver el movimiento en una sola imagen.
class Desliza {
    const string L="★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
    const int BARW=996, BARH=40;
    static Type tb; static FontFamily fam;
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static void Fondo(Graphics g,int w,int h,int s){
        using(LinearGradientBrush lb=new LinearGradientBrush(new Rectangle(0,0,w,h),
            Color.FromArgb(10,13,30),Color.FromArgb(15,19,42),90f)) g.FillRectangle(lb,0,0,w,h);
        Random r=new Random(s);
        for(int i=0;i<w*h/900;i++){ int x=r.Next(w),y=r.Next(h),a=30+r.Next(70);
            using(SolidBrush b=new SolidBrush(Color.FromArgb(a,200,215,255))) g.FillRectangle(b,x,y,1,1); } }
    static int Main(){
        fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true);
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="LetterBar") tb=t;
        MethodInfo gc=tf.GetMethod("GenreToColor",BindingFlags.Static|BindingFlags.NonPublic);
        string[] gen={"Cumbia","Rock","Balada romantica","Reggaeton","Banda","Pop","Salsa","Metal","Norteno","Bachata","Electronica","Corrido"};
        int[] n={0,12,41,33,52,28,19,24,31,17,22,9,0,47,38,21,26,14,0,44,39,29,16,11,0,0,0,8};
        HashSet<char> d=new HashSet<char>(); Dictionary<char,int> cu=new Dictionary<char,int>();
        Dictionary<char,Color> ti=new Dictionary<char,Color>(); int k=0,gi=0;
        foreach(char c in L){
            if(c=='★'){ d.Add(c); cu[c]=25; continue; }
            if(c=='►'){ d.Add(c); cu[c]=37; continue; }
            int v=(k<n.Length)?n[k]:10; k++;
            if("KQWXY".IndexOf(c)>=0) v=0;
            if(v>0){ d.Add(c); cu[c]=v; ti[c]=(Color)gc.Invoke(null,new object[]{gen[gi%gen.Length]}); gi++; } }

        Control bar=(Control)Activator.CreateInstance(tb,true);
        bar.SetBounds(0,0,BARW,BARH); bar.Font=new Font(fam,9f);
        tb.GetField("Letters").SetValue(bar,L); tb.GetField("Available").SetValue(bar,d);
        tb.GetField("Counts").SetValue(bar,cu); tb.GetField("Tints").SetValue(bar,ti);
        tb.GetField("Phase").SetValue(bar,0.6d);
        PropertyInfo pCur=tb.GetProperty("Current"), pPaso=tb.GetProperty("AnimPaso");
        MethodInfo rc=tb.GetMethod("RenderContent",new Type[]{typeof(Graphics)});

        pCur.SetValue(bar,'D',null); System.Threading.Thread.Sleep(240);
        // se corta un trozo de la D a la S, que es por donde viaja
        int i0=L.IndexOf('D'), i1=L.IndexOf('S');
        float slot=(float)BARW/L.Length;
        int rx=(int)(i0*slot)-6, rw=(int)((i1-i0+1)*slot)+12;
        int cuadros=6, W=rw+28, alto=76+cuadros*(BARH+22);
        using(Bitmap bmp=new Bitmap(W,alto,PixelFormat.Format32bppArgb))
        using(Graphics gg=Graphics.FromImage(bmp)){
            using(SolidBrush f=new SolidBrush(Color.FromArgb(7,9,20))) gg.FillRectangle(f,0,0,W,alto);
            gg.SmoothingMode=SmoothingMode.AntiAlias;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                gg.DrawString("AL PASAR DE LETRA, LA CAPSULA SE DESLIZA",ft,b,14,12);
            using(Font fn=new Font(fam,8.5f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(140,150,180)))
                gg.DrawString("de la D a la P; la barra de verdad, dibujada mientras la capsula viaja (dura 190 ms)",fn,b,14,32);
            pCur.SetValue(bar,'P',null);
            int y=60;
            for(int i=0;i<cuadros;i++){
                using(Bitmap tira=new Bitmap(BARW,BARH,PixelFormat.Format32bppPArgb)){
                    float paso;
                    using(Graphics g=Graphics.FromImage(tira)){
                        g.SmoothingMode=SmoothingMode.AntiAlias;
                        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        Fondo(g,BARW,BARH,7);
                        paso=(float)pPaso.GetValue(bar,null);
                        rc.Invoke(bar,new object[]{g});
                    }
                    using(Font fn=new Font(fam,8f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(120,132,162)))
                        gg.DrawString(paso>=1f?"llego":("en camino: "+(int)(paso*100)+"%"),fn,b,14,y);
                    gg.DrawImage(tira,new Rectangle(14,y+14,rw,BARH),new Rectangle(rx,0,rw,BARH),GraphicsUnit.Pixel);
                }
                y+=BARH+22;
                System.Threading.Thread.Sleep(38);
            }
            bmp.Save("desliza.png",ImageFormat.Png); }
        Console.WriteLine("listo: desliza.png"); return 0; }
}
