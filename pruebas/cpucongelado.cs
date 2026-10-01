using System;
using System.Reflection;
class CpuCongelado
{
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static string[] names={"Draw","BakeCell","GetOrBakeCell","PeekOrBakeCell","RebuildCubes","UpdateCells","OnPaint","CellMetrics","Geometry","ClearCellCache","PodaCeldas","BakeAhead"};
 static int Main(string[] args)
 {
  try{
   Assembly before=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0])),after=Assembly.LoadFile(System.IO.Path.GetFullPath(args[1]));
   Type f0=before.GetType("Rocola.Form1",true),f1=after.GetType("Rocola.Form1",true);int count=0;
   foreach(string typeName in new string[]{"Cube","AlbumView","CubeBox"})
   {
    Type t0=f0.GetNestedType(typeName,B),t1=f1.GetNestedType(typeName,B);
    foreach(MethodInfo m0 in t0.GetMethods(B|BindingFlags.DeclaredOnly))
    {
     if(Array.IndexOf(names,m0.Name)<0)continue;
     MethodInfo m1=null;
     foreach(MethodInfo candidate in t1.GetMethods(B|BindingFlags.DeclaredOnly))
     {
      if(candidate.Name!=m0.Name)continue;ParameterInfo[] x=m0.GetParameters(),y=candidate.GetParameters();if(x.Length!=y.Length)continue;
      bool same=true;for(int i=0;i<x.Length;i++)if(x[i].ParameterType.FullName!=y[i].ParameterType.FullName)same=false;
      if(same){m1=candidate;break;}
     }
     if(m1==null)throw new Exception("Falta "+m0.Name);
     byte[] b0=m0.GetMethodBody().GetILAsByteArray(),b1=m1.GetMethodBody().GetILAsByteArray();
     if(b0.Length!=b1.Length)throw new Exception("Cambio IL "+m0.Name);
     for(int i=0;i<b0.Length;i++)if(b0[i]!=b1[i])throw new Exception("Cambio IL "+m0.Name+" "+i);
     count++;Console.WriteLine("OK CPU congelado "+typeName+"."+m0.Name);
    }
   }
   if(count<10)throw new Exception("No verifico suficientes metodos");
   Console.WriteLine("PASS CPU: "+count+" metodos IL identicos al ejecutable previo.");return 0;
  }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
 }
}
