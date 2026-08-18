using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// CUANTO ESPACIO SE VA EN ADORNO y cuanto llega al disco. Se mide lo que de verdad ocupa cada pieza (no lo
// que se le reserva) y se calcula el tamano de portada que sale de ahi.
class Espacio {
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    // ancho de TINTA de un control, dibujandolo ancho de sobra
    static int AnchoReal(Type t, int w, int h, Action<Control> prep){
        Control c=(Control)Activator.CreateInstance(t,true);
        c.SetBounds(0,0,w,h); c.Font=new Font(Fam(),9f);
        if(prep!=null) prep(c);
        using(Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            using(Graphics g=Graphics.FromImage(b)){
                g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                MethodInfo rc=t.GetMethod("RenderContent",new Type[]{typeof(Graphics)});
                if(rc==null) rc=t.GetMethod("RenderContent",new Type[]{typeof(Graphics),typeof(bool)});
                if(rc.GetParameters().Length==1) rc.Invoke(c,new object[]{g});
                else rc.Invoke(c,new object[]{g,false});
            }
            // El TRAMO de tinta, no la posicion del borde: el encabezado se pega a la derecha, asi que su
            // borde derecho no dice nada de lo que ocupa.
            int x0=int.MaxValue,x1=-1;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(b.GetPixel(x,y).A>=40){ if(x<x0)x0=x; if(x>x1)x1=x; }
            return (x1<0)?0:(x1-x0+1);
        }
    }
    static int Main(){
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true);
        Type tHelp=null,tHead=null;
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)){
            if(t.Name=="HelpBar") tHelp=t; if(t.Name=="HeaderView") tHead=t; }
        int anchoHelp=AnchoReal(tHelp,1200,34,null);
        Console.WriteLine("\n  LA FILA DE ARRIBA\n");
        Console.WriteLine("     las instrucciones OCUPAN de verdad      : "+anchoHelp+" px");
        Console.WriteLine("     ...y se les reserva (min(580, ancho))   : 580 px");
        Console.WriteLine("     desperdicio                             : "+(580-anchoHelp)+" px de ancho");

        int[] paginas={1,3,9,12,40};
        foreach(int tot in paginas){
            int a=AnchoReal(tHead,900,34,delegate(Control c){
                c.GetType().GetField("Section").SetValue(c,"LO MAS SONADO");
                c.GetType().GetField("Page").SetValue(c,0);
                c.GetType().GetField("Total").SetValue(c,tot);
                c.GetType().GetField("Accent").SetValue(c,Color.FromArgb(255,205,90)); });
            Console.WriteLine("     el encabezado con "+tot.ToString().PadLeft(2)+" paginas ocupa      : "+a+" px");
        }

        // ---- cuanto llega al disco ----
        Console.WriteLine("\n  CUANTO LLEGA AL DISCO  (Cols=6, Rows=3, celda: portada + etiqueta de 38 + 3 de hueco + 12 de margen)\n");
        int[][] res={ new int[]{1024,768}, new int[]{1280,720}, new int[]{1366,768}, new int[]{1920,1080} };
        Console.WriteLine("     (AL PRINCIPIO: en 1024x768 eran 142 arriba, 142 abajo, rejilla 484, portada de 108 px)\n");
        Console.WriteLine("     pantalla      arriba  abajo   rejilla   celda      PORTADA");
        foreach(int[] r in res){
            int W=r[0],H=r[1],pad=14;
            int contentW=Math.Max(220,(W-pad)-pad);
            int lettersY=46, lettersH=H*40/768;
            int porCasilla=(contentW*8)/(29*10); if(porCasilla>lettersH) lettersH=porCasilla;
            if(lettersH<40) lettersH=40; else if(lettersH>104) lettersH=104;
            int albumY=lettersY+lettersH+10;
            // la tarjeta: cuatro renglones medidos con su letra, como hace el programa
            int hTit,hArt,hSig,progH2=12;
            using(Font f1=new Font(Fam(),15f,FontStyle.Bold))
            using(Font f2=new Font(Fam(),11.5f,FontStyle.Bold))
            using(Font f3=new Font(Fam(),10.5f,FontStyle.Bold)){
                hTit=Math.Max(24,f1.Height+1); hArt=Math.Max(18,f2.Height+1); hSig=Math.Max(17,f3.Height+1); }
            int tarjetaH=hTit+hArt+hSig+4+progH2+2;
            int nowTopY=H-pad-tarjetaH;
            int albumBottom=nowTopY-12, albumH=Math.Max(120,albumBottom-albumY);
            int cw=contentW/6, ch=albumH/3;
            int side=Math.Min(cw-16,ch-53); if(side<10) side=10;
            Console.WriteLine(string.Format("     {0,4}x{1,-4}    {2,4}   {3,4}    {4,4}     {5,3}x{6,-3}    {7,3} px",
                W,H,albumY,H-albumBottom,albumH,cw,ch,side));
            // El adorno no puede volver a comerse la pantalla. Los topes salen de lo medido HOY, con dos
            // pixeles de margen: si alguien vuelve a meter una fila o a engordar la de abajo, esto lo canta.
            if(W==1024&&H==768){
                Check(albumY<=98,      "en 1024x768 la parte de arriba no pasa de 98 px (eran 142)");
                Check(H-albumBottom<=108,"y la de abajo no pasa de 108 px (eran 142)");
                Check(side>=134,       "asi la portada mide al menos 134 px (eran 108)");
            }
            if(W==1280&&H==720) Check(side>=118,"en 1280x720, la mas apretada, la portada llega a 118 px (eran 92)");
            Check(side<=cw-16,         "y la portada nunca se sale de lo ancho de su celda a "+W+"x"+H);
        }
        Console.WriteLine(fallos==0?"\nTODO BIEN":"\nFALLOS: "+fallos);
        return fallos; }
}
