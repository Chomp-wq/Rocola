using System; using System.Collections.Generic; using System.Diagnostics; using System.Drawing;
using System.Drawing.Drawing2D; using System.Drawing.Imaging; using System.Reflection; using System.Windows.Forms;
// QUE PARTE de la lista de canciones cuesta. Se dibuja la misma lista quitando piezas para ver cual pesa.
class ListaPerf {
    static FontFamily Fam(){ string[] p={"Segoe UI","DejaVu Sans","Tahoma","Verdana"};
        foreach(string q in p) try{return new FontFamily(q);}catch{} return FontFamily.GenericSansSerif; }
    static double Mide(int veces, Action a){
        a(); a(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor=double.MaxValue;
        for(int t=0;t<7;t++){ Stopwatch sw=Stopwatch.StartNew();
            for(int i=0;i<veces;i++) a(); sw.Stop();
            double us=sw.Elapsed.TotalMilliseconds*1000.0/veces; if(us<mejor) mejor=us; }
        return mejor; }
    static int Main(string[] argv){
        string exe=(argv.Length>0)?argv[0]:"rocola.exe";
        Assembly asm=Assembly.LoadFrom(exe); Type tf=asm.GetType("Rocola.Form1",true); Type tl=null;
        foreach(Type t in tf.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if(t.Name=="SongListView") tl=t;
        string[] casos={"sin cola (solo circulitos)","con 3 de 11 pedidas","TODAS pedidas"};
        Func<int,int>[] colas={ delegate(int i){return 0;}, delegate(int i){return (i%4==1)?(i/4+1):0;}, delegate(int i){return i+1;} };
        Console.WriteLine("\n  midiendo: "+exe+"   (11 filas visibles)\n");
        for(int k=0;k<casos.Length;k++){
            Control c=(Control)Activator.CreateInstance(tl,true);
            c.SetBounds(0,0,620,400); c.Font=new Font(Fam(),11f,FontStyle.Bold);
            tl.GetField("RowH").SetValue(c,34); tl.GetField("Sel").SetValue(c,3);
            tl.GetField("Accent").SetValue(c,Color.FromArgb(178,152,255));
            tl.GetField("GetCount").SetValue(c,new Func<int>(delegate{return 200;}));
            tl.GetField("GetText").SetValue(c,new Func<int,string>(delegate(int i){return "Cancion numero "+i;}));
            tl.GetField("GetRight").SetValue(c,new Func<int,string>(delegate(int i){return "4:32";}));
            FieldInfo fq=tl.GetField("GetQueuePos");
            if(fq!=null) fq.SetValue(c,colas[k]);
            MethodInfo rc=tl.GetMethod("RenderContent",new Type[]{typeof(Graphics)});
            using(Bitmap b=new Bitmap(620,400,PixelFormat.Format32bppPArgb))
            using(Graphics g=Graphics.FromImage(b)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                object[] args=new object[]{g};
                Console.WriteLine(string.Format("     {0,-32} {1,8:0.0} us", casos[k], Mide(400, delegate{ rc.Invoke(c,args); })));
            }
        }
        return 0; }
}
