using System;
using System.Collections.Generic;
using System.Reflection;
// Compilar junto a leercodigo.cs: lee el IL por el mismo sitio que las baterias (Codigo.BytesIL).
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
     byte[] b0=Codigo.BytesIL(m0),b1=Codigo.BytesIL(m1);
     if(b0==null||b1==null)throw new Exception("Sin cuerpo "+m0.Name);
     bool mismos=b0.Length==b1.Length;
     for(int i=0;mismos&&i<b0.Length;i++)if(b0[i]!=b1[i])mismos=false;
     string como="mismos bytes";
     if(!mismos)
     {
      // Un campo o un texto nuevo en OTRO sitio corre los numeros de tabla: se compara lo que nombra cada instruccion.
      List<string> n0=Codigo.IlConNombres(m0),n1=Codigo.IlConNombres(m1);
      for(int i=0;i<Math.Min(n0.Count,n1.Count);i++)if(n0[i]!=n1[i])throw new Exception("Cambio IL "+m0.Name+" instruccion "+i+": "+n0[i]+" / "+n1[i]);
      if(n0.Count!=n1.Count)throw new Exception("Cambio IL "+m0.Name+": "+n0.Count+" / "+n1.Count+" instrucciones");
      como="mismo IL; solo cambian numeros de tabla";
     }
     count++;Console.WriteLine("OK CPU congelado "+typeName+"."+m0.Name+" ("+como+")");
    }
   }
   if(count<10)throw new Exception("No verifico suficientes metodos");
   Console.WriteLine("PASS CPU: "+count+" metodos con IL identico al ejecutable previo (nombres resueltos si los bytes difieren).");return 0;
  }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
 }
}
