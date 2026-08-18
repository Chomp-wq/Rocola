using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D;
using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// LA TARJETA DE ABAJO, ya implementada: se colocan las piezas de VERDAD en las posiciones que calcula el
// programa y se dibujan con su propio codigo.
class Tarjeta {
    const int W=1024, H=768, PAD=14;
    static FontFamily fam;
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static void Fondo(Graphics g,int w,int h){
        using(LinearGradientBrush lb=new LinearGradientBrush(new Rectangle(0,0,w,h+1),
            Color.FromArgb(26,20,54),Color.FromArgb(52,26,78),90f)) g.FillRectangle(lb,0,0,w,h);
        Random r=new Random(11);
        for(int i=0;i<w*h/700;i++){ int x=r.Next(w),y=r.Next(h),a=25+r.Next(60);
            using(SolidBrush b=new SolidBrush(Color.FromArgb(a,220,210,255))) g.FillRectangle(b,x,y,1,1); } }
    static void Etq(Graphics g,string s,Font f,Color c,int x,int y,int w,int h){
        using(StringFormat sf=new StringFormat()){
            sf.LineAlignment=StringAlignment.Center; sf.Trimming=StringTrimming.EllipsisCharacter;
            sf.FormatFlags=StringFormatFlags.NoWrap|StringFormatFlags.LineLimit;
            // recorte a mano: una etiqueta de verdad (AutoEllipsis) recorta contra su caja, y aqui hay que
            // imitarlo o la maqueta ensenaria texto que en la rocola no se veria
            if(g.MeasureString(s,f).Width>w){
                while(s.Length>1 && g.MeasureString(s+"...",f).Width>w) s=s.Substring(0,s.Length-1);
                s+="..."; }
            using(SolidBrush b=new SolidBrush(Color.FromArgb(150,0,0,0)))
                g.DrawString(s,f,b,new RectangleF(x,y+1.3f,w,h),sf);
            using(SolidBrush b=new SolidBrush(c)) g.DrawString(s,f,b,new RectangleF(x,y,w,h),sf); } }
    static void Ctrl(Graphics g,Control c,MethodInfo rc){
        GraphicsState st=g.Save(); g.TranslateTransform(c.Left,c.Top);
        rc.Invoke(c,new object[]{g}); g.Restore(st); }
    static int Main(){
        fam=Fam();
        Assembly asm=Assembly.LoadFrom("rocola.exe"); Type tf=asm.GetType("Rocola.Form1",true);
        Type tPast=null,tProg=null;
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)){
            if(t.Name=="PastillaView") tPast=t; if(t.Name=="ProgressView") tProg=t; }
        MethodInfo anchoPast=tPast.GetMethod("Ancho",BindingFlags.Static|BindingFlags.Public);

        object[][] casos={
            new object[]{"Con creditos y cola","I Feel You","Depeche Mode","Sigue:  Alejandro Fernandez - Que Seas Muy Feliz","Creditos","3",Color.FromArgb(120,240,140),"2",true},
            new object[]{"Sin creditos (inserta una moneda)","Que Seas Muy Feliz","Alejandro Fernandez","Sigue:  -","Inserta una moneda","",Color.FromArgb(255,205,90),"0",false},
            new object[]{"Titulo y artista largos","Rompiendo Fronteras (En Vivo Desde El Auditorio Nacional)","Alejandro Fernandez feat. Los Tigres del Norte","Sigue:  Depeche Mode - Enjoy The Silence","Creditos","12",Color.FromArgb(120,240,140),"7",true},
        };
        int alto=52+casos.Length*(24+104+18);
        using(Bitmap bmp=new Bitmap(W,alto,PixelFormat.Format32bppArgb))
        using(Graphics gg=Graphics.FromImage(bmp)){
            using(SolidBrush f=new SolidBrush(Color.FromArgb(7,9,20))) gg.FillRectangle(f,0,0,W,alto);
            gg.SmoothingMode=SmoothingMode.AntiAlias;
            gg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using(Font ft=new Font(fam,13f,FontStyle.Bold)) using(SolidBrush b=new SolidBrush(Color.FromArgb(235,240,252)))
                gg.DrawString("LA TARJETA, YA IMPLEMENTADA",ft,b,14,12);
            using(Font fn=new Font(fam,8.5f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(140,150,180)))
                gg.DrawString("las capsulas y la barra son las piezas de verdad del programa, en las posiciones que calcula el acomodo",fn,b,14,32);
            int y=52;
            foreach(object[] c in casos){
                using(Font fn=new Font(fam,8f)) using(SolidBrush b=new SolidBrush(Color.FromArgb(120,132,162)))
                    gg.DrawString((string)c[0],fn,b,14,y);
                int tY=8;
                int hTit, hArt, hSig, progH=12, cubo;
                using(Bitmap tira=new Bitmap(W,104,PixelFormat.Format32bppPArgb)){
                    using(Graphics g=Graphics.FromImage(tira)){
                        g.SmoothingMode=SmoothingMode.AntiAlias;
                        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        Fondo(g,W,104);
                        using(Font f1=new Font(fam,15f,FontStyle.Bold))
                        using(Font f2=new Font(fam,11.5f,FontStyle.Bold))
                        using(Font f3=new Font(fam,10.5f,FontStyle.Bold)){
                            hTit=Math.Max(24,f1.Height+1); hArt=Math.Max(18,f2.Height+1); hSig=Math.Max(17,f3.Height+1); }
                        cubo=hTit+hArt+hSig+4+progH+2;
                        // portada
                        using(LinearGradientBrush lb=new LinearGradientBrush(new Rectangle(PAD,tY,cubo,cubo+1),
                            Color.FromArgb(70,40,110),Color.FromArgb(30,18,60),45f)) g.FillRectangle(lb,PAD,tY,cubo,cubo);
                        using(Pen pe=new Pen(Color.FromArgb(120,190,170,255),1f)) g.DrawRectangle(pe,PAD,tY,cubo,cubo);
                        // capsulas
                        int pastH=26;
                        int wC=(int)anchoPast.Invoke(null,new object[]{(string)c[4],(string)c[5],pastH,fam});
                        int wQ=(int)anchoPast.Invoke(null,new object[]{"En cola",(string)c[7],pastH,fam});
                        Control pc=(Control)Activator.CreateInstance(tPast,true);
                        pc.SetBounds(W-PAD-wC,tY,wC,pastH); pc.Font=new Font(fam,9f);
                        tPast.GetField("Etiqueta").SetValue(pc,(string)c[4]);
                        tPast.GetField("Valor").SetValue(pc,(string)c[5]);
                        tPast.GetField("Acento").SetValue(pc,(Color)c[6]);
                        Control pq=(Control)Activator.CreateInstance(tPast,true);
                        pq.SetBounds(W-PAD-wC-10-wQ,tY,wQ,pastH); pq.Font=new Font(fam,9f);
                        tPast.GetField("Etiqueta").SetValue(pq,"En cola");
                        tPast.GetField("Valor").SetValue(pq,(string)c[7]);
                        tPast.GetField("Acento").SetValue(pq,Color.FromArgb(140,205,255));
                        MethodInfo rcP=tPast.GetMethod("RenderContent",new Type[]{typeof(Graphics)});
                        Ctrl(g,pc,rcP); Ctrl(g,pq,rcP);
                        // textos
                        int txtX=PAD+cubo+14, derPast=W-PAD-wC-wQ-24, txtW=Math.Max(120,derPast-txtX);
                        using(Font fT=new Font(fam,15f,FontStyle.Bold))
                        using(Font fA=new Font(fam,11.5f,FontStyle.Bold))
                        using(Font fS=new Font(fam,10.5f,FontStyle.Bold))
                        using(Font fM=new Font(fam,12f,FontStyle.Bold)){
                            Etq(g,(string)c[1],fT,Color.FromArgb(236,242,255),txtX,tY,txtW,hTit);
                            int anchoArt=txtW;
                            if((bool)c[8]){
                                int bw=TextRenderer.MeasureText("MEJORADA",fM).Width+4;
                                anchoArt=Math.Max(60,txtW-bw-12);
                                int tw=Math.Min(TextRenderer.MeasureText((string)c[2],fA).Width,anchoArt);
                                Etq(g,"MEJORADA",fM,Color.FromArgb(255,196,96),txtX+tw+12,tY+hTit,bw,hArt);
                            }
                            Etq(g,(string)c[2],fA,Color.FromArgb(120,230,165),txtX,tY+hTit,anchoArt,hArt);
                            int anchoDer=W-PAD-txtX, timeW=130;
                            int progW=Math.Max(50,anchoDer-timeW-12);
                            Etq(g,(string)c[3],fS,Color.FromArgb(150,165,200),txtX,tY+hTit+hArt,progW,hSig);
                            int filaY=tY+hTit+hArt+hSig+4;
                            Control pr=(Control)Activator.CreateInstance(tProg,true);
                            pr.SetBounds(txtX,filaY,progW,progH);
                            tProg.GetProperty("Value").SetValue(pr,0.67,null);
                            tProg.GetProperty("Accent").SetValue(pr,Color.FromArgb(178,152,255),null);
                            Ctrl(g,pr,tProg.GetMethod("RenderContent",new Type[]{typeof(Graphics)}));
                            Etq(g,"3:05 / 4:36",fS,Color.FromArgb(236,242,255),txtX+progW+12,filaY-(24-progH)/2,timeW,24);
                        }
                    }
                    gg.DrawImageUnscaled(tira,0,y+16);
                }
                y+=24+104+18;
            }
            bmp.Save("tarjeta.png",ImageFormat.Png); }
        Console.WriteLine("listo: tarjeta.png"); return 0; }
}
