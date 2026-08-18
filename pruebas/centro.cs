using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// CUANTO SE DESVIA cada letra del centro de su casilla. Se dibuja la barra de verdad sobre un lienzo
// transparente, y por casilla se saca el centro de la TINTA (alfa >= 190) y se compara con el centro del hueco.
class Centro {
    const int ALTO=40, CORTE=190;
    const string L="★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static int Main(){
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true); Type tb=null;
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="LetterBar") tb=t;
        MethodInfo gc=tf.GetMethod("GenreToColor",BindingFlags.Static|BindingFlags.NonPublic);
        HashSet<char> disp=new HashSet<char>(); Dictionary<char,int> cu=new Dictionary<char,int>();
        Dictionary<char,Color> ti=new Dictionary<char,Color>();
        string[] gen={"Cumbia","Rock","Balada","Reggaeton","Banda","Pop","Metal","Salsa"}; int gi=0;
        foreach(char c in L){ if("KQWXY".IndexOf(c)>=0) continue; disp.Add(c); cu[c]=8+gi*5;
            ti[c]=(Color)gc.Invoke(null,new object[]{gen[gi%gen.Length]}); gi++; }
        int[] anchos={1972,1892,996,486};
        foreach(int W in anchos){
            Control bar=(Control)Activator.CreateInstance(tb,true);
            bar.SetBounds(0,0,W,ALTO); bar.Font=new Font(Fam(),9f);
            tb.GetField("Letters").SetValue(bar,L); tb.GetProperty("Current").SetValue(bar, 'M', null);
        // Current es una PROPIEDAD y al cambiarla arranca el deslizamiento de la capsula. Aqui se
        // mide la barra QUIETA, asi que se le deja terminar antes de dibujar.
        System.Threading.Thread.Sleep(230);
            tb.GetField("Available").SetValue(bar,disp); tb.GetField("Counts").SetValue(bar,cu);
            tb.GetField("Tints").SetValue(bar,ti); tb.GetField("Phase").SetValue(bar,0.6d);
            using(Bitmap b=new Bitmap(W,ALTO,PixelFormat.Format32bppArgb)){
                using(Graphics g=Graphics.FromImage(b)){
                    g.SmoothingMode=SmoothingMode.AntiAlias;
                    g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);
                    tb.GetMethod("RenderContent",new Type[]{typeof(Graphics)}).Invoke(bar,new object[]{g});
                }
                float slot=(float)W/L.Length;
                Console.WriteLine("\n  ancho "+W+"   hueco por casilla "+slot.ToString("0.00")+" px");
                double peor=0; char peorC=' '; double suma=0; int n=0;
                for(int i=0;i<L.Length;i++){
                    int xa=(int)(i*slot), xb=(int)((i+1)*slot); if(xb>W) xb=W;
                    // solo la zona de la LETRA (arriba), no el numero
                    long sx=0; int cnt=0; int y1=ALTO;
                    for(int y=0;y<26;y++) for(int x=xa;x<xb;x++) if(b.GetPixel(x,y).A>=CORTE){ sx+=x; cnt++; if(y<y1) y1=y; }
                    if(cnt==0) continue;
                    double cx=(double)sx/cnt;                    // centro de la tinta
                    double cs=i*slot+slot/2.0;                   // centro del hueco
                    double d=cx-cs; suma+=Math.Abs(d); n++;
                    if(Math.Abs(d)>Math.Abs(peor)){ peor=d; peorC=L[i]; }
                    if(i<6||i>=L.Length-2) Console.WriteLine(string.Format("     {0}  tinta en {1,7:0.00}  hueco centra en {2,7:0.00}  ->  {3,6:+0.00;-0.00}",L[i],cx,cs,d));
                }
                Console.WriteLine(string.Format("     desviacion MEDIA {0:0.00} px, PEOR {1:+0.00;-0.00} px (letra {2})",suma/Math.Max(1,n),peor,peorC));
            }
        }
        // y el desfase de la zona pulsable
        Console.WriteLine("\n  DONDE CREE LA BARRA QUE PINCHASTE (Width/n entero) vs donde DIBUJA (Width/n con decimales)");
        foreach(int W in anchos){
            int si=W/L.Length; float sf=(float)W/L.Length;
            double drift=(L.Length-1)*(sf-si);
            Console.WriteLine(string.Format("     ancho {0,5}: pincha cada {1} px, dibuja cada {2:0.00} px  ->  en la ultima casilla se desfasa {3:0.0} px",W,si,sf,drift));
        }
        return 0; }
}
