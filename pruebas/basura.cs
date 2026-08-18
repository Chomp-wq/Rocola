using System; using System.Reflection; using System.Runtime.Serialization; using System.Windows.Forms;
// Cuanta BASURA (memoria que despues hay que recoger) genera cada cuadro del fondo. En el equipo de XP, con
// 4 GB, cada recogida del recolector es un tiron: lo ideal por cuadro es CERO.
namespace Rocola { static class Basura {
  static void Main(){
    Type TP=null,TS=null,TAu=null;
    foreach(Type t in typeof(Basura).Assembly.GetTypes()){
      if(t.Name=="GpuPanel") TP=t; if(t.Name=="GpuStarfield") TS=t; if(t.Name=="GpuAurora") TAu=t; }
    object apagado=FormatterServices.GetUninitializedObject(TS);
    Control pan=(Control)Activator.CreateInstance(TP,true);
    pan.SetBounds(0,0,1024,768);
    TP.GetField("stars",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(pan,apagado);
    MethodInfo mAur=TP.GetMethod("DrawAurora",BindingFlags.Instance|BindingFlags.NonPublic);
    object au=Activator.CreateInstance(TAu);
    float[] B=new float[64]; for(int i=0;i<B.Length;i++) B[i]=0.4f;
    TAu.GetField("Has").SetValue(au,true); TAu.GetField("B").SetValue(au,B);
    TAu.GetField("BaseY").SetValue(au,672f); TAu.GetField("Alto").SetValue(au,300f);
    TAu.GetField("Dim").SetValue(au,1f); TAu.GetField("DimFilo").SetValue(au,1f); TAu.GetField("Calma").SetValue(au,0f);
    TAu.GetField("Rgb").SetValue(au,(uint)0x7A3CC8); TAu.GetField("Rgb2").SetValue(au,(uint)0x3C6AC8);
    TAu.GetField("RgbA").SetValue(au,(uint)0xC85AA0); TAu.GetField("Rgb2A").SetValue(au,(uint)0x8A5AC8);
    object[] arg=new object[]{au};
    for(int i=0;i<50;i++) mAur.Invoke(pan,arg);   // calentar y dejar reservados sus arreglos
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long a=GC.GetTotalMemory(true);
    const int N=2000;
    for(int i=0;i<N;i++) mAur.Invoke(pan,arg);
    long b=GC.GetTotalMemory(false);
    Console.WriteLine("  aurora: "+((b-a)/(double)N).ToString("0")+" bytes de basura por cuadro  (incluye la reflexion de la prueba)");
    // Cuanto de eso es la propia llamada por reflexion (una caja por argumento).
    MethodInfo nada=typeof(Basura).GetMethod("Nada",BindingFlags.Static|BindingFlags.NonPublic);
    object[] arg2=new object[]{au};
    for(int i=0;i<50;i++) nada.Invoke(null,arg2);
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long c=GC.GetTotalMemory(true);
    for(int i=0;i<N;i++) nada.Invoke(null,arg2);
    long d=GC.GetTotalMemory(false);
    Console.WriteLine("  de eso, la reflexion de la prueba: "+((d-c)/(double)N).ToString("0")+" bytes");
    Console.WriteLine("  -> la aurora en si: "+(((b-a)-(d-c))/(double)N).ToString("0")+" bytes por cuadro");
  }
  static void Nada(object x){}
} }
