using System; using System.Diagnostics; using System.Reflection;
// Cuanto cuesta ANALIZAR un cuadro para la IA (la FFT + armar los pares) contra lo que cuesta DESCOMPRIMIR
// el audio del que sale ese cuadro. De esa proporcion depende cuanto material mas se puede cosechar gratis.
class Midia {
  static int Main(string[] a){
    Assembly asm=Assembly.LoadFrom(a.Length>0?a[0]:"rocola.exe");
    Type TF=asm.GetType("Rocola.Form1",true); Type TS=null;
    foreach(Type t in TF.GetNestedTypes(BindingFlags.NonPublic)) if(t.Name=="SpectralTap") TS=t;
    object tap=Activator.CreateInstance(TS,new object[]{44100,1});
    int Frame=(int)TS.GetProperty("FrameLen").GetValue(tap,null);
    float[] mono=new float[Frame];
    Random r=new Random(7);
    for(int i=0;i<Frame;i++){ double t=i/44100.0; mono[i]=(float)(0.4*Math.Sin(2*Math.PI*220*t)+0.2*Math.Sin(2*Math.PI*3300*t)+0.05*(r.NextDouble()-0.5)); }
    MethodInfo mAn=TS.GetMethod("AnalyzeFrame");
    MethodInfo mPar=TS.GetMethod("MakePair");
    float[] feat=new float[28], tgt=new float[12];
    object[] argAn=new object[]{mono,0};
    float[] cortes={11000f,13000f,15000f,16500f,18000f};
    for(int i=0;i<200;i++){ mAn.Invoke(tap,argAn); foreach(float c in cortes) mPar.Invoke(tap,new object[]{c,feat,tgt}); }
    double mA=double.MaxValue, mP=double.MaxValue;
    for(int b=0;b<7;b++){
      Stopwatch sw=Stopwatch.StartNew(); for(int i=0;i<2000;i++) mAn.Invoke(tap,argAn); sw.Stop();
      double ms=sw.Elapsed.TotalMilliseconds/2000; if(ms<mA) mA=ms;
    }
    object[][] ap=new object[5][]; for(int k=0;k<5;k++) ap[k]=new object[]{cortes[k],feat,tgt};
    for(int b=0;b<7;b++){
      Stopwatch sw=Stopwatch.StartNew(); for(int i=0;i<2000;i++) for(int k=0;k<5;k++) mPar.Invoke(tap,ap[k]); sw.Stop();
      double ms=sw.Elapsed.TotalMilliseconds/2000; if(ms<mP) mP=ms;
    }
    Console.WriteLine("  cuadro = "+Frame+" muestras = "+(Frame/44100.0*1000).ToString("0")+" ms de musica");
    Console.WriteLine("  analizar un cuadro (FFT + acantilado): "+mA.ToString("0.000")+" ms");
    Console.WriteLine("  armar sus 5 pares:                     "+mP.ToString("0.000")+" ms");
    double porCuadro=mA+mP;
    Console.WriteLine("  -> por cuadro cosechado: "+porCuadro.ToString("0.000")+" ms");
    Console.WriteLine();
    double musicaPorCuadro=Frame/44100.0;
    Console.WriteLine("  analizar CADA cuadro de la musica costaria "+(porCuadro/1000/musicaPorCuadro*100).ToString("0.0")+"% del tiempo real");
    Console.WriteLine("  (descomprimir un MP3 va a ~2-5% del tiempo real en un equipo moderno)");
    Console.WriteLine();
    Console.WriteLine("  HOY, en modo pasivo:");
    Console.WriteLine("    se descomprimen 3 tramos x 20 s = 60 s de musica");
    Console.WriteLine("    se analizan 192 cuadros = "+(192*musicaPorCuadro).ToString("0.0")+" s SEGUIDOS, siempre del mismo sitio");
    Console.WriteLine("    de esos, "+(192-64)+" dan pares -> "+((192-64)*5)+" ejemplos = "+((192-64)*5*160/1024)+" KB por cancion");
    Console.WriteLine("    coste del analisis: "+(192*porCuadro).ToString("0") + " ms de los ~"+(60*0.03*1000).ToString("0")+" ms que cuesta descomprimir");
    Console.WriteLine();
    int disp=(int)(60.0/musicaPorCuadro);
    Console.WriteLine("  DISPONIBLES en esos mismos 60 s ya descomprimidos: "+disp+" cuadros");
    Console.WriteLine("    o sea que se esta usando el "+(192.0/disp*100).ToString("0.0")+"% de lo que ya se pago por descomprimir");
    return 0; }
}
