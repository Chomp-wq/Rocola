using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// LO YA IMPLEMENTADO: la lista de un disco (circulito + "EN COLA") y una tarjeta de resultado del buscador
// (lo tecleado encendido + disco + genero). Todo dibujado por el propio programa.
class Nuevo {
    const int W=1024, LW=620, ROWH=34, FILAS=6;
    static FontFamily fam; static Type tLista, tBusc, tForm;
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static readonly string[] CANC={"Enjoy The Silence","Personal Jesus","Never Let Me Down Again",
        "Policy Of Truth","Walking In My Shoes","It's No Good"};
    static readonly string[] DUR={"6:12","4:56","4:47","4:55","5:35","5:58"};
    static readonly int[] COLA={0,1,0,0,2,0};
    static int Main(){
        fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); tForm=asm.GetType("Rocola.Form1",true);
        foreach(Type t in tForm.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)){
            if(t.Name=="SongListView") tLista=t; if(t.Name=="SearchOverlay") tBusc=t; }
        int alto=230+ROWH*FILAS;
        using(Bitmap bmp=new Bitmap(W,alto,PixelFormat.Format32bppArgb))
        using(Graphics gg=Graphics.FromImage(bmp)){
            using(SolidBrush f=new SolidBrush(Color.FromArgb(7,9,20))) gg.FillRectangle(f,0,0,W,alto);
            gg.SmoothingMode=SmoothingMode.AntiAlias;
            gg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                gg.DrawString("YA IMPLEMENTADO  -  dibujado por el propio programa",ft,b,14,12);
            using(Font fn=new Font(fam,9.5f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(150,190,250)))
                gg.DrawString("DENTRO DE UN DISCO   (la 2a y la 5a ya estan pedidas)",fn,b,14,40);
            // ---- la lista de verdad ----
            Control c=(Control)Activator.CreateInstance(tLista,true);
            c.SetBounds(0,0,LW,ROWH*FILAS); c.Font=new Font(fam,11f,FontStyle.Bold);
            tLista.GetField("RowH").SetValue(c,ROWH);
            tLista.GetField("Sel").SetValue(c,1);
            tLista.GetField("Accent").SetValue(c,Color.FromArgb(178,152,255));
            tLista.GetField("GetCount").SetValue(c,new Func<int>(delegate{return CANC.Length;}));
            tLista.GetField("GetText").SetValue(c,new Func<int,string>(delegate(int i){return CANC[i];}));
            tLista.GetField("GetRight").SetValue(c,new Func<int,string>(delegate(int i){return DUR[i];}));
            tLista.GetField("GetQueuePos").SetValue(c,new Func<int,int>(delegate(int i){return COLA[i];}));
            using(Bitmap tira=new Bitmap(LW,ROWH*FILAS,PixelFormat.Format32bppPArgb)){
                using(Graphics g=Graphics.FromImage(tira)){
                    g.SmoothingMode=SmoothingMode.AntiAlias;
                    g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    tLista.GetMethod("RenderContent",new Type[]{typeof(Graphics)}).Invoke(c,new object[]{g});
                }
                gg.DrawImageUnscaled(tira,14,60);
            }
            // ---- tarjetas del buscador ----
            int y=60+ROWH*FILAS+24;
            using(Font fn=new Font(fam,9.5f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(150,190,250)))
                gg.DrawString("BUSCADOR   (tecleado: \"DE\")",fn,b,14,y);
            Type tSI=null;
            foreach(Type t in tForm.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="SearchItem") tSI=t;
            object[][] casos={
                new object[]{2,"Depeche Mode - Enjoy The Silence","Violator","Electronica"},
                new object[]{2,"Deep Purple - Smoke On The Water","Machine Head","Rock"},
                new object[]{1,"Deftones - White Pony","","Metal"},
                new object[]{0,"Def Leppard","","Rock"},
            };
            Control ov=(Control)Activator.CreateInstance(tBusc,true);
            ov.SetBounds(0,0,W,600); ov.Font=new Font(fam,10f);
            tBusc.GetField("Query").SetValue(ov,"DE");
            tBusc.GetField("Accent").SetValue(ov,Color.FromArgb(178,152,255));
            System.Collections.IList res=(System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(tSI));
            Dictionary<object,string> disco=new Dictionary<object,string>(), gene=new Dictionary<object,string>();
            foreach(object[] cs in casos){
                object it=Activator.CreateInstance(tSI,true);
                tSI.GetField("Kind").SetValue(it,(int)cs[0]);
                tSI.GetField("Display").SetValue(it,(string)cs[1]);
                res.Add(it); disco[it]=(string)cs[2]; gene[it]=(string)cs[3];
            }
            tBusc.GetField("Results").SetValue(ov,res);
            Type fd=typeof(Func<,>).MakeGenericType(tSI,typeof(string));
            tBusc.GetField("GetDisco").SetValue(ov,Delegate.CreateDelegate(fd,new Cierre(disco),typeof(Cierre).GetMethod("Dame")));
            tBusc.GetField("GetGenero").SetValue(ov,Delegate.CreateDelegate(fd,new Cierre(gene),typeof(Cierre).GetMethod("Dame")));
            MethodInfo dib=tBusc.GetMethod("DrawOneResultCard",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo ens=tBusc.GetMethod("EnsurePaintRes",BindingFlags.Instance|BindingFlags.NonPublic);
            int cw=(W-40)/2, chh=46;
            using(Bitmap tira=new Bitmap(W-28,(chh+8)*2,PixelFormat.Format32bppPArgb)){
                using(Graphics g=Graphics.FromImage(tira)){
                    g.SmoothingMode=SmoothingMode.AntiAlias;
                    g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using(SolidBrush b=new SolidBrush(Color.FromArgb(18,14,38))) g.FillRectangle(b,0,0,tira.Width,tira.Height);
                    if(ens!=null) try{ ens.Invoke(ov,null);}catch{}
                    for(int i=0;i<4;i++){
                        Rectangle rr=new Rectangle((i%2)*(cw+10), (i/2)*(chh+8), cw, chh);
                        dib.Invoke(ov,new object[]{g,i,rr,i==0});
                    }
                }
                gg.DrawImageUnscaled(tira,14,y+22);
            }
            bmp.Save("nuevo.png",ImageFormat.Png); }
        Console.WriteLine("listo: nuevo.png"); return 0; }
    public class Cierre {
        Dictionary<object,string> d;
        public Cierre(Dictionary<object,string> x){ d=x; }
        public string Dame(object it){ string v; return (it!=null && d.TryGetValue(it,out v))?v:""; }
    }
}
