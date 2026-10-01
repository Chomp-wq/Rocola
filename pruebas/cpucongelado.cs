using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
class CpuCongelado
{
 const BindingFlags B=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static string[] names={"Draw","BakeCell","GetOrBakeCell","PeekOrBakeCell","RebuildCubes","UpdateCells","OnPaint","CellMetrics","Geometry","ClearCellCache","PodaCeldas","BakeAhead"};
 // Agregar tipos GPU renumera tokens de metadatos sin cambiar las instrucciones CPU.
 // Comparar cada operando resuelto, nunca omitir los operandos ni aceptar solo igual longitud.
 static string Code(MethodInfo method)
 {
  Dictionary<short,OpCode> ops=new Dictionary<short,OpCode>();
  foreach(FieldInfo f in typeof(OpCodes).GetFields(B))if(f.FieldType==typeof(OpCode)){OpCode op=(OpCode)f.GetValue(null);ops[op.Value]=op;}
  MethodBody body=method.GetMethodBody();byte[] code=body.GetILAsByteArray();StringBuilder result=new StringBuilder();
  result.Append(body.InitLocals).Append('|').Append(body.MaxStackSize);
  foreach(LocalVariableInfo local in body.LocalVariables)result.Append('|').Append(local.LocalType.FullName).Append(':').Append(local.IsPinned);
  foreach(ExceptionHandlingClause clause in body.ExceptionHandlingClauses){result.Append('|').Append(clause.Flags).Append(':').Append(clause.TryOffset).Append(':').Append(clause.TryLength).Append(':').Append(clause.HandlerOffset).Append(':').Append(clause.HandlerLength);if(clause.Flags==ExceptionHandlingClauseOptions.Clause)result.Append(':').Append(clause.CatchType.FullName);if(clause.Flags==ExceptionHandlingClauseOptions.Filter)result.Append(':').Append(clause.FilterOffset);}
  for(int i=0;i<code.Length;)
  {
   int first=code[i++];short value=(short)(first==254 ? 0xfe00|code[i++] : first);OpCode op=ops[value];result.Append('|').Append(op.Name).Append('=');int size=0;
   switch(op.OperandType)
   {
    case OperandType.InlineField:case OperandType.InlineMethod:case OperandType.InlineType:case OperandType.InlineTok:
     MemberInfo member=method.Module.ResolveMember(BitConverter.ToInt32(code,i),method.DeclaringType.GetGenericArguments(),method.GetGenericArguments());
     result.Append(member.DeclaringType==null ? "" : member.DeclaringType.FullName).Append(':').Append(member.MemberType).Append(':').Append(member);i+=4;continue;
    case OperandType.InlineString:result.Append(method.Module.ResolveString(BitConverter.ToInt32(code,i)));i+=4;continue;
    case OperandType.InlineSig:result.Append(BitConverter.ToString(method.Module.ResolveSignature(BitConverter.ToInt32(code,i))));i+=4;continue;
    case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(code,i);break;
    case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
    case OperandType.InlineI:case OperandType.InlineBrTarget:case OperandType.ShortInlineR:size=4;break;
    case OperandType.InlineVar:size=2;break;
    case OperandType.ShortInlineI:case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineVar:size=1;break;
    case OperandType.InlineNone:break;
    default:throw new Exception("Operando no validado: "+op.OperandType);
   }
   if(i+size>code.Length)throw new Exception("IL truncado");for(int j=0;j<size;j++)result.Append(code[i++].ToString("x2"));
  }
  return result.ToString();
 }
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
     string c0=Code(m0),c1=Code(m1);
     if(c0!=c1){int p=0;while(p<Math.Min(c0.Length,c1.Length)&&c0[p]==c1[p])p++;Console.WriteLine("ANTES "+c0.Substring(Math.Max(0,p-80),Math.Min(200,c0.Length-Math.Max(0,p-80))));Console.WriteLine("AHORA "+c1.Substring(Math.Max(0,p-80),Math.Min(200,c1.Length-Math.Max(0,p-80))));throw new Exception("Cambio IL/operandos/locales/excepciones "+m0.Name);}
     count++;Console.WriteLine("OK CPU congelado "+typeName+"."+m0.Name);
    }
   }
   if(count<10)throw new Exception("No verifico suficientes metodos");
   Console.WriteLine("PASS CPU: "+count+" metodos con instrucciones y operandos resueltos identicos al ejecutable previo.");return 0;
  }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
 }
}
