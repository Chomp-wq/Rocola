using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// Sonda: con la barra dibujada sobre un lienzo TRANSPARENTE, que valor de alfa tiene el riel, el numero y la
// letra. Sirve para elegir el corte con el que la bateria distingue "tinta de texto" de "riel".
class Sonda {
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static int Main(){
        FontFamily fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true); Type tb=null;
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="LetterBar") tb=t;
        MethodInfo gc=tf.GetMethod("GenreToColor",BindingFlags.Static|BindingFlags.NonPublic);
        string L="★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►"; int W=996,H=40;
        HashSet<char> disp=new HashSet<char>(); Dictionary<char,int> cu=new Dictionary<char,int>();
        Dictionary<char,Color> ti=new Dictionary<char,Color>();
        string[] gen={"Cumbia","Rock","Balada","Reggaeton","Banda","Pop","Metal"}; int gi=0;
        foreach(char c in L){ if("KQWXY".IndexOf(c)>=0) continue; disp.Add(c); cu[c]=20+gi*3;
            ti[c]=(Color)gc.Invoke(null,new object[]{gen[gi%gen.Length]}); gi++; }
        Control bar=(Control)Activator.CreateInstance(tb,true);
        bar.SetBounds(0,0,W,H); bar.Font=new Font(fam,9f);
        tb.GetField("Letters").SetValue(bar,L); tb.GetField("Current").SetValue(bar,'M');
        tb.GetField("Available").SetValue(bar,disp); tb.GetField("Counts").SetValue(bar,cu);
        tb.GetField("Tints").SetValue(bar,ti); tb.GetField("Phase").SetValue(bar,0.6d);
        using(Bitmap b=new Bitmap(W,H,PixelFormat.Format32bppArgb)) {
            using(Graphics g=Graphics.FromImage(b)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);
                tb.GetMethod("RenderContent",new Type[]{typeof(Graphics)}).Invoke(bar,new object[]{g});
            }
            float slot=(float)W/L.Length;
            // histograma de alfa por franja de alto
            int[] maxFila=new int[H];
            for(int y=0;y<H;y++){ int mx=0; for(int x=0;x<W;x++){ int a=b.GetPixel(x,y).A; if(a>mx) mx=a; } maxFila[y]=mx; }
            Console.WriteLine("alfa MAXIMO por fila (0..39):");
            for(int y=0;y<H;y++) Console.WriteLine("   y="+y.ToString("00")+"  alfa max="+maxFila[y]);
            // en una casilla concreta: la A (letra + numero)
            int ia=L.IndexOf('A'); int xa=(int)(ia*slot), xb=(int)((ia+1)*slot);
            Console.WriteLine("\ncasilla de la A  (x "+xa+".."+xb+")");
            for(int y=0;y<H;y++){ int mx=0; for(int x=xa;x<xb;x++){ int a=b.GetPixel(x,y).A; if(a>mx) mx=a; }
                Console.WriteLine("   y="+y.ToString("00")+"  alfa max="+mx); }
        }
        return 0; }
}
