using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// LA FILA DE ARRIBA COMPLETA, con las piezas de verdad del programa y en las posiciones que les toca ahora:
// instrucciones a la izquierda, encabezado de la seccion pegado a la derecha, y debajo la barra de letras.
class FilaArriba {
    const string L="★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
    static Type tHelp,tHead,tBarra; static FontFamily fam; static MethodInfo gc;
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static void Dib(Graphics g,Control c,int x,int y){
        GraphicsState st=g.Save(); g.TranslateTransform(x,y);
        MethodInfo rc=c.GetType().GetMethod("RenderContent",new Type[]{typeof(Graphics)});
        if(rc==null){ rc=c.GetType().GetMethod("RenderContent",new Type[]{typeof(Graphics),typeof(bool)});
            rc.Invoke(c,new object[]{g,false}); } else rc.Invoke(c,new object[]{g});
        g.Restore(st); }
    static int Main(){
        fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true);
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)){
            if(t.Name=="HelpBar") tHelp=t; if(t.Name=="HeaderView") tHead=t; if(t.Name=="LetterBar") tBarra=t; }
        gc=tf.GetMethod("GenreToColor",BindingFlags.Static|BindingFlags.NonPublic);
        string[] gen={"Cumbia","Rock","Balada romantica","Reggaeton","Banda","Pop","Salsa","Metal","Norteno","Bachata","Electronica","Corrido"};
        int[] n={0,253,2304,3162,7092,4251,7106,2036,1251,15,152,1775,56,2681,3260,989,1330,5656,52,5511,4751,3073,351,3414,970,87,760,429,96};
        HashSet<char> d=new HashSet<char>(); Dictionary<char,int> cu=new Dictionary<char,int>();
        Dictionary<char,Color> ti=new Dictionary<char,Color>(); int k=0,gi=0;
        foreach(char c in L){
            int v=(k<n.Length)?n[k]:10; k++;
            if(v>0){ d.Add(c); cu[c]=v; if(c!='★'&&c!='►'){ ti[c]=(Color)gc.Invoke(null,new object[]{gen[gi%gen.Length]}); gi++; } } }
        cu['★']=36; d.Add('★'); cu['►']=96; d.Add('►');

        int[][] res={ new int[]{1024,768}, new int[]{1366,768}, new int[]{1920,1080} };
        int PAD=14, alto=56;
        foreach(int[] r in res){ int lh=r[1]*40/768; int pc=((r[0]-28)*8)/(29*10); if(pc>lh) lh=pc;
            if(lh<40) lh=40; if(lh>104) lh=104; alto+=26+34+6+lh+26; }
        int W=1024;
        using(Bitmap bmp=new Bitmap(W,alto,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(bmp)){
            using(LinearGradientBrush lb=new LinearGradientBrush(new Rectangle(0,0,W,alto),
                Color.FromArgb(9,12,28),Color.FromArgb(14,18,40),90f)) g.FillRectangle(lb,0,0,W,alto);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                g.DrawString("LA FILA DE ARRIBA, YA APRETADA",ft,b,14,12);
            using(Font fn=new Font(fam,8.5f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(140,150,180)))
                g.DrawString("el encabezado ya no tiene fila propia: comparte la de las instrucciones y va pegado a la derecha",fn,b,14,32);
            int y=56;
            foreach(int[] r in res){
                int RW=r[0],RH=r[1];
                int contentW=RW-2*PAD;
                int lh=RH*40/768; int pc=(contentW*8)/(29*10); if(pc>lh) lh=pc;
                if(lh<40) lh=40; if(lh>104) lh=104;
                int helpW=Math.Min(470,contentW);
                int headX=PAD+helpW+12, headW=Math.Max(0,(RW-PAD)-headX);
                using(Font fn=new Font(fam,8f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(120,132,162)))
                    g.DrawString(RW+"x"+RH+"   instrucciones "+helpW+" px, encabezado "+headW+" px, barra de letras "+lh+" px de alto",fn,b,14,y);
                // se dibuja a escala para caber en la hoja
                using(Bitmap fila=new Bitmap(RW,34+6+lh,PixelFormat.Format32bppPArgb)){
                    using(Graphics gf=Graphics.FromImage(fila)){
                        gf.SmoothingMode=SmoothingMode.AntiAlias;
                        gf.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        gf.Clear(Color.Transparent);
                        Control hb=(Control)Activator.CreateInstance(tHelp,true);
                        hb.SetBounds(0,0,helpW,34); hb.Font=new Font(fam,9f);
                        Dib(gf,hb,PAD,0);
                        Control hv=(Control)Activator.CreateInstance(tHead,true);
                        hv.SetBounds(0,0,headW,34); hv.Font=new Font(fam,9f);
                        tHead.GetField("Section").SetValue(hv,"LETRA A");
                        tHead.GetField("Page").SetValue(hv,0); tHead.GetField("Total").SetValue(hv,9);
                        tHead.GetField("Accent").SetValue(hv,(Color)gc.Invoke(null,new object[]{"Balada romantica"}));
                        Dib(gf,hv,headX,0);
                        Control lb2=(Control)Activator.CreateInstance(tBarra,true);
                        lb2.SetBounds(0,0,contentW,lh); lb2.Font=new Font(fam,9f);
                        tBarra.GetField("Letters").SetValue(lb2,L);
                        tBarra.GetProperty("Current").SetValue(lb2,'A',null);
                        System.Threading.Thread.Sleep(230);
                        tBarra.GetField("Available").SetValue(lb2,d);
                        tBarra.GetField("Counts").SetValue(lb2,cu);
                        tBarra.GetField("Tints").SetValue(lb2,ti);
                        tBarra.GetField("Phase").SetValue(lb2,0.6d);
                        Dib(gf,lb2,PAD,40);
                    }
                    int dw=W-28, dh=(int)((double)fila.Height*dw/RW);
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.DrawImage(fila,new Rectangle(14,y+16,dw,dh));
                    y+=26+dh+22;
                }
            }
            bmp.Save("fila_arriba.png",ImageFormat.Png); }
        Console.WriteLine("listo: fila_arriba.png"); return 0; }
}
