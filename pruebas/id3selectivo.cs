using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
class Id3Selectivo {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Type track; static MethodInfo parse, frames;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 sealed class CountStream:MemoryStream {
  public long Bytes; public int MaxRead;
  public CountStream(byte[] data,int cap):base(data,false){MaxRead=cap;}
  public override int Read(byte[] b,int off,int n){int got=base.Read(b,off,Math.Min(n,MaxRead));Bytes+=got;return got;}
 }
 static byte[] Join(params byte[][] parts){int n=0;foreach(byte[] a in parts)n+=a.Length;byte[] b=new byte[n];int p=0;foreach(byte[] a in parts){Array.Copy(a,0,b,p,a.Length);p+=a.Length;}return b;}
 static byte[] Number(int n,bool safe){return safe?new byte[]{(byte)(n>>21&127),(byte)(n>>14&127),(byte)(n>>7&127),(byte)(n&127)}:new byte[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};}
 static byte[] Frame(int version,string id,byte[] data,byte flags){return Join(Encoding.ASCII.GetBytes(id),Number(data.Length,version==4),new byte[]{0,flags},data);}
 static byte[] Text(int enc,string s){
  Encoding e=enc==0?Encoding.GetEncoding(28591):enc==1?Encoding.Unicode:enc==2?Encoding.BigEndianUnicode:Encoding.UTF8;
  return Join(new byte[]{(byte)enc},enc==1?new byte[]{255,254}:new byte[0],e.GetBytes(s));
 }
 static object Empty(){object t=FormatterServices.GetUninitializedObject(track);foreach(string f in new string[]{"Title","Artist","Album","Genre"})track.GetField(f).SetValue(t,"");return t;}
 static void Equal(object a,object b){foreach(string f in new string[]{"Title","Artist","Album","Genre"})Check(object.Equals(track.GetField(f).GetValue(a),track.GetField(f).GetValue(b)),"Difiere "+f);}
 static long Compare(byte[] body,int major,int flags,int cap,int declared){
  object expected=Empty(),actual=Empty();
  parse.Invoke(null,new object[]{body,body.Length,major,flags,expected});
  using(CountStream s=new CountStream(body,cap)){
   frames.Invoke(null,new object[]{s,declared,major,flags,actual});Equal(expected,actual);return s.Bytes;
  }
 }
 static byte[] V1(){
  byte[] data=new byte[128];Array.Copy(Encoding.ASCII.GetBytes("TAG"),data,3);
  Array.Copy(Encoding.ASCII.GetBytes("Titulo v1"),0,data,3,9);
  Array.Copy(Encoding.ASCII.GetBytes("Artista v1"),0,data,33,10);
  Array.Copy(Encoding.ASCII.GetBytes("Album v1"),0,data,63,8);data[127]=17;return data;
 }
 static void SingleHandle(){
  MethodInfo full=track.GetMethod("ReadId3Stream",F),genre=track.GetMethod("ReadGenreStream",F),v1=track.GetMethod("ReadId3v1Stream",F);
  foreach(MethodInfo method in new MethodInfo[]{full,genre,v1})
   Check(!Codigo.Llamadas(method).Contains("FileStream..ctor")&&!Codigo.Nombres(method).Contains("ReadId3"),"Lector de stream reabre archivo");
  foreach(string method in new string[]{"ReadId3","SoloGenero"}){
   int opens=0;foreach(string call in Codigo.Llamadas(track.GetMethod(method,F)))if(call=="FileStream..ctor")opens++;
   Check(opens==1,"Aperturas de archivo en "+method);
  }
  int tested=0;
  // Campos modernos, defaults del nombre y v1: la precedencia no puede cambiar.
  foreach(int version in new int[]{3,4})foreach(int flags in new int[]{0,128})foreach(int mask in new int[]{0,1,3,7,15})
  foreach(int cap in new int[]{1,7,4096})foreach(bool defaults in new bool[]{false,true}){
   List<byte[]> parts=new List<byte[]>();string[] ids={"TIT2","TPE1","TALB","TCON"},names={"Title","Artist","Album","Genre"},values={"Titulo moderno","Artista moderno","Album moderno","Pop"};
   for(int i=0;i<4;i++)if((mask&(1<<i))!=0)parts.Add(Frame(version,ids[i],Text(0,values[i]),0));
   parts.Add(new byte[10]);byte[] body=Join(parts.ToArray());
   byte[] bytes=Join(Encoding.ASCII.GetBytes("ID3"),new byte[]{(byte)version,0,(byte)flags},Number(body.Length,true),body,new byte[160],V1());
   object t=Empty();track.GetField("Path").SetValue(t,"no-se-debe-abrir-ninguna-ruta.mp3");
   if(defaults){track.GetField("Title").SetValue(t,"Nombre archivo");track.GetField("Artist").SetValue(t,"Nombre carpeta");}
   using(CountStream s=new CountStream(bytes,cap)){
    full.Invoke(null,new object[]{s,t});Check(s.CanRead,"Cierra stream prestado");
   }
   string[] expected={"Titulo v1","Artista v1","Album v1","Rock"};
   if(defaults){expected[0]="Nombre archivo";expected[1]="Nombre carpeta";}
   for(int i=0;i<4;i++)Check((string)track.GetField(names[i]).GetValue(t)==(((mask&(1<<i))!=0)?values[i]:expected[i]),"Prioridad "+names[i]);
   object g=Empty();
   using(CountStream s=new CountStream(bytes,cap)){
    genre.Invoke(null,new object[]{s,g});Check(s.CanRead,"Genero cierra stream");
   }
   Check((string)track.GetField("Genre").GetValue(g)==((mask&8)!=0?"Pop":"Rock"),"Genero v1/v2");
   tested++;
  }
  // Corruptos con v1 util: el lector de genero debe recuperar sin reabrir.
  byte[][] bad={
   new byte[160],
   Join(Encoding.ASCII.GetBytes("ID3"),new byte[]{3,0,0},Number(10000,true),new byte[10]),
   Join(Encoding.ASCII.GetBytes("ID3"),new byte[]{4,0,64},Number(4,true),Number(100,true)),
   Join(Encoding.ASCII.GetBytes("ID3"),new byte[]{3,0,0},new byte[]{255,255,255,255})
  };
  foreach(byte[] prefix in bad)foreach(int cap in new int[]{1,17,4096}){
   foreach(MethodInfo method in new MethodInfo[]{full,genre}){
    object t=Empty();using(CountStream s=new CountStream(Join(prefix,V1()),cap))method.Invoke(null,new object[]{s,t});
    Check((string)track.GetField("Genre").GetValue(t)=="Rock","Pierde v1 con cabecera corrupta");
   }tested++;
  }
  for(int n=0;n<128;n++){
   object t=Empty();using(CountStream s=new CountStream(new byte[n],1))v1.Invoke(null,new object[]{s,t});
   Check((string)track.GetField("Genre").GetValue(t)=="","Inventa v1 corta");tested++;
  }
  Console.WriteLine("  OK    "+tested+" casos de un solo stream: prioridad v2/defaults/v1, genero, truncados y lecturas de 1 byte.");
 }
 static void Main(string[] args){
  Assembly asm=Assembly.LoadFrom(args.Length>0?args[0]:"rocola.exe");
  track=asm.GetType("Rocola.Form1").GetNestedType("Track",F);
  parse=track.GetMethod("ParseId3v2",F,null,new Type[]{typeof(byte[]),typeof(int),typeof(int),typeof(int),track},null);
  frames=track.GetMethod("ReadId3Frames",F);
  SingleHandle();
  int tested=0;long largest=0;
  foreach(int v in new int[]{3,4})foreach(bool ext in new bool[]{false,true})foreach(int enc in new int[]{0,1,2,3})foreach(byte ff in new byte[]{0,1,2,4,8,32,64,128}){
   byte[] extension=!ext?new byte[0]:v==4?new byte[]{0,0,0,6,1,0}:new byte[]{0,0,0,6,0,0,0,0,0,0};
   byte[] body=Join(extension,Frame(v,"TIT2",Text(enc,"Primero"),0),Frame(v,"APIC",new byte[1024*1024],0),
      Frame(v,"TPE1",Text(enc,"Artista"),ff),Frame(v,"TALB",Text(enc,"Album"),0),
      Frame(v,"TCON",Text(enc,"(17)"),ff),Frame(v,"TIT2",Text(enc,"Final"),0),new byte[20]);
   int flags=ext?64:0;
   long read=Compare(body,v,flags,3,body.Length);largest=Math.Max(largest,read);Check(read<1024,"Se leyo APIC");
   tested++;
  }
  Random rnd=new Random(91);
  for(int i=0;i<500;i++){
   int v=i%2+3;byte[] text=new byte[rnd.Next(1,90)];rnd.NextBytes(text);
   byte[] body=Join(Frame(v,"TIT2",text,(byte)rnd.Next(256)),Frame(v,"TCON",Text(0,"Rock"),0));
   if(i%3==0)Array.Resize(ref body,rnd.Next(body.Length));
   Compare(body,v,i%5==0&&v==4?128:0,7,body.Length+50);tested++;
  }
  // Cabeceras extendidas corruptas/limites: nunca cruzar el cuerpo declarado.
  foreach(int v in new int[]{3,4})foreach(int n in new int[]{0,1,3,4,5,6,10,50}){
   byte[] b=new byte[n];if(n>=4)Array.Copy(Number(100,v==4),b,4);
   Compare(b,v,64,2,n);tested++;
  }
  // Fallback ID3v1 y segunda consulta sin reabrir.
  string dir=Path.Combine(Path.GetTempPath(),"rocola-id3-selectivo-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(dir);
  try{
   string path=Path.Combine(dir,"prueba.mp3");
   foreach(int version in new int[]{3,4}){
    byte[] body=Join(Frame(version,"APIC",new byte[1024*1024],0),Frame(version,"TIT2",Text(1,"Titulo real"),0),Frame(version,"TCON",Text(0,"Rock"),0));
    byte[] head=Join(Encoding.ASCII.GetBytes("ID3"),new byte[]{(byte)version,0,0},Number(body.Length,true));
    File.WriteAllBytes(path,Join(head,body));
    object expected=Empty(),actual=Empty();track.GetField("Path").SetValue(actual,path);
    parse.Invoke(null,new object[]{body,body.Length,version,0,expected});
    track.GetMethod("EnsureTags").Invoke(actual,null);Equal(expected,actual);
   }
   byte[] v1=new byte[128];Array.Copy(Encoding.ASCII.GetBytes("TAGTitulo de v1"),v1,15);v1[127]=17;
   File.WriteAllBytes(path,Join(new byte[160],v1));object t=Empty();track.GetField("Path").SetValue(t,path);
   track.GetMethod("EnsureTags").Invoke(t,null);Check((string)track.GetField("Title").GetValue(t)=="Titulo de v1","Perdio v1");
   Check((string)track.GetMethod("SoloGenero",F).Invoke(null,new object[]{path})=="Rock","SoloGenero pierde v1 real");
   using(FileStream exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(exclusive.Length>0,"Retiene archivo");
   File.Delete(path);track.GetMethod("EnsureTags").Invoke(t,null);Check((string)track.GetField("Title").GetValue(t)=="Titulo de v1","Relee etiquetas cargadas");
  }finally{Directory.Delete(dir,true);}
  Console.WriteLine("  OK    "+tested+" etiquetas equivalentes; APIC de 1 MiB omitido, maximo "+largest+" bytes logicos; lecturas parciales y v1.");
 }
}
