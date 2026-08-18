using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// LA BARRA A LAS DOS ESCALAS: la del bar (1024x768 -> barra de 40 px) y la de una pantalla ancha
// (3440x1440 -> barra de 75). Dibujada por el propio programa.
class Escala {
    const string L="★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
    static Type tb; static FontFamily fam; static MethodInfo gc;
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static void Fondo(Graphics g,int w,int h,int s){
        using(LinearGradientBrush lb=new LinearGradientBrush(new Rectangle(0,0,w,h),
            Color.FromArgb(10,13,30),Color.FromArgb(15,19,42),90f)) g.FillRectangle(lb,0,0,w,h);
        Random r=new Random(s);
        for(int i=0;i<w*h/900;i++){ int x=r.Next(w),y=r.Next(h),a=30+r.Next(70);
            using(SolidBrush b=new SolidBrush(Color.FromArgb(a,200,215,255))) g.FillRectangle(b,x,y,1,1); } }
    static Bitmap Tira(int w,int h,char act,HashSet<char> d,Dictionary<char,int> c,Dictionary<char,Color> t,int sem){
        Control bar=(Control)Activator.CreateInstance(tb,true);
        bar.SetBounds(0,0,w,h); bar.Font=new Font(fam,9f);
        tb.GetField("Letters").SetValue(bar,L);
        tb.GetProperty("Current").SetValue(bar,act,null);
        System.Threading.Thread.Sleep(230);
        tb.GetField("Available").SetValue(bar,d); tb.GetField("Counts").SetValue(bar,c);
        tb.GetField("Tints").SetValue(bar,t); tb.GetField("Phase").SetValue(bar,0.6d);
        Bitmap bmp=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
        using(Graphics g=Graphics.FromImage(bmp)){
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Fondo(g,w,h,sem);
            tb.GetMethod("RenderContent",new Type[]{typeof(Graphics)}).Invoke(bar,new object[]{g});
        }
        return bmp; }
    static int Main(){
        fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true);
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="LetterBar") tb=t;
        gc=tf.GetMethod("GenreToColor",BindingFlags.Static|BindingFlags.NonPublic);
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
        // pantalla del bar y pantalla ancha; el alto de barra sale de la misma cuenta que el programa
        int[] pw={1024,1920,3440}, ph={768,1080,1440};
        string[] tit={"PANTALLA DEL BAR  1024x768","PANTALLA 1920x1080","PANTALLA ANCHA  3440x1440"};
        int maxW=1200, alto=58; int[] bw=new int[3], bh=new int[3];
        for(int i=0;i<3;i++){ bw[i]=pw[i]-28; bh[i]=ph[i]*40/768;
            int pc=(bw[i]*8)/(29*10); if(pc>bh[i]) bh[i]=pc;
            if(bh[i]<40) bh[i]=40; if(bh[i]>104) bh[i]=104;
            alto+=26+bh[i]+18; }
        using(Bitmap bmp=new Bitmap(maxW,alto,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(bmp)){
            using(SolidBrush f=new SolidBrush(Color.FromArgb(7,9,20))) g.FillRectangle(f,0,0,maxW,alto);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                g.DrawString("LA BARRA SE AJUSTA A LA PANTALLA",ft,b,14,14);
            using(Font fn=new Font(fam,8.5f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(140,150,180)))
                g.DrawString("cada tira esta reducida para caber en esta hoja; lo que importa es la PROPORCION letra/casilla",fn,b,14,34);
            int y=58;
            for(int i=0;i<3;i++){
                using(Font ft=new Font(fam,9.5f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(150,190,250)))
                    g.DrawString(tit[i]+"   ->  barra de "+bh[i]+" px de alto, casilla de "+(bw[i]/L.Length)+" px",ft,b,14,y);
                using(Bitmap t2=Tira(bw[i],bh[i],'M',d,cu,ti,7+i)){
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    int dw=maxW-28; int dh=(int)((double)bh[i]*dw/bw[i]);
                    g.DrawImage(t2,new Rectangle(14,y+20,dw,dh));
                }
                y+=26+bh[i]+18; }
            bmp.Save("barra_escala.png",ImageFormat.Png); }
        Console.WriteLine("listo: barra_escala.png"); return 0; }
}
