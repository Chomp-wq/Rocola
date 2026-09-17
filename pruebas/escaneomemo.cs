using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
class EscaneoMemoTest {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static object Call(Type t,object o,string n,params object[] a){return t.GetMethod(n,F).Invoke(o,a);}
 static object Get(Type t,object o,string n){return t.GetField(n,F).GetValue(o);}
 static void Set(Type t,object o,string n,object v){t.GetField(n,F).SetValue(o,v);}
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 static void Equal(List<string> a,List<string> b,string why){
  Check(a.Count==b.Count,why+" cantidad");
  for(int i=0;i<a.Count;i++)Check(a[i]==b[i],why+" orden/ruta");
 }
 static void Hash(ref ulong h,string s){foreach(char c in s){h^=char.ToLowerInvariant(c);h*=1099511628211UL;}h^=0xff;h*=1099511628211UL;}
 // Referencia del algoritmo anterior: sin memoria de enumeraciones.
 static string Signature(Type form,string root){
  ulong h=1469598103934665603UL;long files=0,dirs=0;
  Stack<string> pending=new Stack<string>();pending.Push(Path.GetFullPath(root));
  while(pending.Count>0){
   string dir=pending.Pop();dirs++;
   Hash(ref h,dir);Hash(ref h,Directory.GetLastWriteTimeUtc(dir).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
   string[] paths=Directory.GetFiles(dir);Array.Sort(paths,StringComparer.OrdinalIgnoreCase);
   foreach(string path in paths){
    FileInfo f=new FileInfo(path);files++;Hash(ref h,f.FullName);
    Hash(ref h,f.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Hash(ref h,f.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
   }
   string[] subs=Directory.GetDirectories(dir);Array.Sort(subs,StringComparer.OrdinalIgnoreCase);
   for(int i=subs.Length-1;i>=0;i--)
    if(!(bool)Call(form,null,"EsAtajo",subs[i])&&!(bool)Call(form,null,"EsCarpetaDelSistema",subs[i]))pending.Push(subs[i]);
  }
  return dirs+":"+files+":"+h.ToString("x16");
 }
 static void Scan(Type form,object owner,string root,out List<string> audio,out List<string> video,out Dictionary<string,string> covers){
  audio=new List<string>();video=new List<string>();covers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  Call(form,owner,"ScanFolders",root,audio,video,covers);
 }
 static void Compare(Type form,object owner,Type memoType,string root){
  Set(form,owner,"scanDirectoryMemo",null);
  List<string> a,v;Dictionary<string,string> c;Scan(form,owner,root,out a,out v,out c);
  object memo=Activator.CreateInstance(memoType,true);Set(form,owner,"scanDirectoryMemo",memo);
  string sig=(string)Call(form,null,"RootSigCaptured",root,memo);
  Check(sig==Signature(form,root),"Firma distinta de referencia");
  Check(sig==(string)Call(form,null,"RootSig",root),"Captura cambia firma");
  List<string> b,w;Dictionary<string,string> d;Scan(form,owner,root,out b,out w,out d);
  Equal(a,b,"audio");Equal(v,w,"video");
  Check(c.Count==d.Count,"Cantidad portadas");foreach(string key in c.Keys)Check(d[key]==c[key],"Preferencia portada distinta");
  Check((int)Get(memoType,memo,"Hits")>0,"No reutiliza enumeraciones");
  Check((long)Get(memoType,memo,"Bytes")==0&&(int)Get(memoType,memo,"Paths")==0,"Retiene entradas consumidas");
 }
 static void Main(string[] args){
  Type form=Assembly.LoadFrom(args[0]).GetType("Rocola.Form1"),memoType=form.GetNestedType("ScanDirectoryMemo",F);
  object owner=FormatterServices.GetUninitializedObject(form);
  int[] cpu={1,2,3,4,8,32},workers={1,1,2,3,4,4};
  for(int i=0;i<cpu.Length;i++){
   Check((int)Call(form,null,"IndexWorkers",7999,cpu[i])==1,"Paraleliza indice pequeno");
   Check((int)Call(form,null,"IndexWorkers",20000,cpu[i])==workers[i],"No reserva margen al indexar");
  }
  Type item=form.GetNestedType("SearchItem",F);
  IList index=(IList)Activator.CreateInstance(form.GetField("searchIndex",F).FieldType);
  Set(form,owner,"searchIndex",index);
  for(int i=0;i<20000;i++){
   object entry=Activator.CreateInstance(item,true);
   Set(item,entry,"Display","Canción ÁÉ - Depeche Mode "+i);index.Add(entry);
  }
  Call(form,owner,"ClavesDelIndice");
  string[] keys=new string[index.Count];int[] masks=new int[index.Count];
  for(int i=0;i<index.Count;i++){keys[i]=(string)Get(item,index[i],"Key");masks[i]=(int)Get(item,index[i],"Mask");}
  Call(form,owner,"TramoDeClaves",0,index.Count);
  for(int i=0;i<index.Count;i++)Check(keys[i]==(string)Get(item,index[i],"Key")&&masks[i]==(int)Get(item,index[i],"Mask"),"Indice paralelo distinto de serial");
  string root=Path.Combine(Path.GetTempPath(),"rocola-scan-memo-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  try {
   string album=Path.Combine(root,"Artista - Álbum"),deep=Path.Combine(album,"CD2");
   Directory.CreateDirectory(deep);
   Directory.CreateDirectory(Path.Combine(root,"Windows"));Directory.CreateDirectory(Path.Combine(root,"Vacio"));
   string[] names={"b.MP3","a.wav","Video.mp4","cover.png","portada.jpg","zeta.JPG","ignorado.txt"};
   foreach(string n in names)File.WriteAllText(Path.Combine(album,n),"datos");
   File.WriteAllText(Path.Combine(root,"suelta.mp3"),"datos");
   File.WriteAllText(Path.Combine(deep,"pista.mp3"),"datos");
   File.WriteAllText(Path.Combine(deep,"alternativa.PNG"),"datos");
   File.WriteAllText(Path.Combine(root,"Windows\\no-incluir.mp3"),"datos");
   Compare(form,owner,memoType,root);
   object memo=Activator.CreateInstance(memoType,true);Set(form,owner,"scanDirectoryMemo",memo);
   string before=(string)Call(form,null,"RootSigCaptured",root,memo);
   File.WriteAllText(Path.Combine(album,"nueva.mp3"),"nueva");Directory.SetLastWriteTimeUtc(album,DateTime.UtcNow.AddSeconds(5));
   List<string>a,v;Dictionary<string,string>c;Scan(form,owner,root,out a,out v,out c);
   Check(a.Contains(Path.Combine(album,"nueva.mp3")),"Reutiliza carpeta modificada");
   Check((int)Get(memoType,memo,"Misses")>0,"No invalida carpeta modificada");
   Check(before!=(string)Call(form,null,"RootSig",root),"Verificacion final no detecta cambio");
   Compare(form,owner,memoType,root);
   before=(string)Call(form,null,"RootSig",root);
   File.AppendAllText(Path.Combine(deep,"pista.mp3"),"contenido cambiado");
   Check(before!=(string)Call(form,null,"RootSig",root),"No detecta cambio de archivo profundo");
   Compare(form,owner,memoType,root);
   memo=Activator.CreateInstance(memoType,true);Set(form,owner,"scanDirectoryMemo",memo);
   Call(form,null,"RootSigCaptured",root,memo);
   File.Delete(Path.Combine(album,"a.wav"));
   File.Move(Path.Combine(album,"b.MP3"),Path.Combine(album,"renombrada.mp3"));
   Directory.SetLastWriteTimeUtc(album,DateTime.UtcNow.AddSeconds(10));
   Scan(form,owner,root,out a,out v,out c);
   Check(!a.Contains(Path.Combine(album,"a.wav"))&&!a.Contains(Path.Combine(album,"b.MP3"))&&a.Contains(Path.Combine(album,"renombrada.mp3")),"Usa nombres borrados/renombrados");
   Compare(form,owner,memoType,root);
   memo=Activator.CreateInstance(memoType,true);
   for(int i=0;i<3000;i++)Call(memoType,memo,"Remember",Path.Combine(root,"simulada"+i),0L,new string[]{"pista"},new string[0]);
   Check((long)Get(memoType,memo,"Bytes")<=8L*1024*1024&&(int)Get(memoType,memo,"Paths")<=32768,"Memoria sin limites");
   string[] tooMany=new string[32769];for(int i=0;i<tooMany.Length;i++)tooMany[i]="ruta";
   long used=(long)Get(memoType,memo,"Bytes");
   Call(memoType,memo,"Remember","enorme",0L,tooMany,new string[0]);
   Check(used==(long)Get(memoType,memo,"Bytes"),"Acepta carpeta fuera de presupuesto");
   Set(form,owner,"scanDirectoryMemo",memo);
   List<string> fallbackAudio,fallbackVideo;Dictionary<string,string> fallbackCovers;
   Scan(form,owner,root,out fallbackAudio,out fallbackVideo,out fallbackCovers);
   Equal(a,fallbackAudio,"Catalogo con memo lleno");Equal(v,fallbackVideo,"Videos con memo lleno");
   Call(memoType,memo,"Clear");Check((long)Get(memoType,memo,"Bytes")==0,"Clear retiene memoria");
   // Camino de error del worker: no deja referencias ni altera prioridad del hilo reutilizado.
   ThreadPriority previous=Thread.CurrentThread.Priority;
   try{Call(form,owner,"Scanner_DoWork",null,new DoWorkEventArgs("argumento-invalido"));throw new Exception("No rechaza argumento invalido");}
   catch(TargetInvocationException ex){Check(ex.InnerException is InvalidCastException,"Error inesperado de worker");}
   Check(Get(form,owner,"scanDirectoryMemo")==null&&Thread.CurrentThread.Priority==previous,"Worker no limpia/restaura");
  }finally{Directory.Delete(root,true);}
  Console.WriteLine("PASS: catalogo/orden/portadas y firma equivalentes; cambios profundos, memoria acotada, invalidacion, limpieza y 20000 claves iguales al indice serial.");
 }
}
