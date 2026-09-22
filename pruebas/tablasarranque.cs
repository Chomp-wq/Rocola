using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

// No enciende la Rocola ni toca sus datos: instantaneas privadas y archivos temporales.
class TablasArranque
{
 const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
 static Type form, dataType, loadType, readerType;
 static readonly string[] fields = { "Loud", "Eq", "Ia", "Deep", "Fast", "Incompatible" };
 static readonly string[] methods = { "ReadLoudCacheRecords", "ReadEqCacheRecords", "ReadIaDoneRecords", "ReadDeepDoneRecords", "ReadDurFastRecords", "ReadIncompatibleRecords" };
 static object New(Type t) { return Activator.CreateInstance(t, true); }
 static object Get(object o, string n) { return o.GetType().GetField(n,F).GetValue(o); }
 static void Set(object o, string n, object v) { o.GetType().GetField(n,F).SetValue(o,v); }
 static object Call(string n, params object[] a) { return form.GetMethod(n,F).Invoke(null,a); }
 static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
 static string Eq(string path) { return "1;2;3;4;5;6;7;8|"+path+"\n"; }
 static string[] Fixtures()
 {
  return new string[] {
   "mala\n0.2|40|A.mp3\n0.4|a.mp3\n0.3|20|B.mp3\n0.5|50|V.mp4\n0|X.mp3\n0.1|legacy.mp3\n",
   Eq("A.mp3")+Eq("a.mp3")+Eq("B.mp3")+"1;2|short.mp3\nno|broken.mp3\n",
   "A.mp3\n9|a.mp3\n2|A.mp3\n3|B.mp3\nB.mp3\n  \n",
   "A.mp3\nB.mp3\nV.mp4\nmissing.mp3\n",
   "40|A.mp3\n41|a.mp3\n20|B.mp3\n0|zero.mp3\nno|bad.mp3\n",
   "error  |  A.mp3  |  detalle\nerror  |  b.mp3\nbad\nerror  |    \n"
  };
 }
 static void OldRead(int table, TextReader reader, object data)
 {
  if(table==0) Call(methods[table],reader,Get(data,"Loud"),Get(data,"Dur"));
  else if(table==1) {
   object[] args={reader,Get(data,"Eq"),Get(data,"EqVersion")};
   Call(methods[table],args);Set(data,"EqVersion",args[2]);
  } else if(table==3) Call(methods[table],reader,Get(data,"Deep"),Get(data,"Loud"),Get(data,"Dur"),Get(data,"Eq"));
  else Call(methods[table],reader,Get(data,fields[table]));
 }
 static void Equal(object a,object b,string label)
 {
  IDictionary da=a as IDictionary,db=b as IDictionary;
  if(da!=null) {
   Check(da.Count==db.Count,label+" cantidad");
   foreach(DictionaryEntry e in da) {
    Check(db.Contains(e.Key),label+" clave");
    float[] fa=e.Value as float[],fb=db[e.Key] as float[];
    if(fa!=null) { Check(fa.Length==fb.Length,label+" vector");for(int i=0;i<fa.Length;i++)Check(fa[i].Equals(fb[i]),label+" valor"); }
    else Check(e.Value.Equals(db[e.Key]),label+" valor");
   }
  } else {
   HashSet<string> sa=(HashSet<string>)a,sb=(HashSet<string>)b;
   Check(sa.SetEquals(sb),label+" conjunto");
  }
 }
 sealed class FaultReader : StringReader {
  int left;
  public FaultReader(string text,int lines):base(text){left=lines;}
  public override string ReadLine(){if(left--==0)throw new IOException("fallo simulado antes de EOF");return base.ReadLine();}
 }
 sealed class ObservedReader : StringReader {
  public int Reads; public bool Closed; public Action OnRead;
  public ObservedReader(string text):base(text){}
  public override string ReadLine(){Reads++;if(OnRead!=null)OnRead();return base.ReadLine();}
  protected override void Dispose(bool disposing){Closed=true;base.Dispose(disposing);}
 }
 static void ExpectIo(Action action)
 {
  try { action();throw new Exception("Faltaba fallo de lectura"); }
  catch(TargetInvocationException e){Check(e.InnerException is IOException,"Excepcion inesperada");}
 }
 static void CompareAndFailure()
 {
  string[] text=Fixtures();object old=New(dataType),fresh=New(dataType);
  for(int i=0;i<6;i++) {
   using(StringReader r=new StringReader(text[i]))OldRead(i,r,old);
   object before=Get(fresh,fields[i]);
   using(StringReader r=new StringReader(text[i]))Call("ReadStartupTableRecords",r,i,fresh);
   Check(!ReferenceEquals(before,Get(fresh,fields[i])),"No adopta tabla "+i);
   Equal(Get(old,fields[i]),Get(fresh,fields[i]),fields[i]);
   Equal(Get(old,"Dur"),Get(fresh,"Dur"),"duraciones");
   Check(Get(old,"EqVersion").Equals(Get(fresh,"EqVersion")),"version EQ");
  }
  Check(((HashSet<string>)Get(fresh,"Deep")).Count==3,"Marcas completas no validadas");
  Check(((Dictionary<string,float>)Get(fresh,"Dur"))["a.mp3"]==40f,"Duplicado antiguo pierde duracion");
  for(int table=0;table<6;table++) {
   int t=table;object before=Get(fresh,fields[t]),dur=Get(fresh,"Dur"),version=Get(fresh,"EqVersion");
   using(FaultReader r=new FaultReader(text[t],2))ExpectIo(delegate { Call("ReadStartupTableRecords",r,t,fresh); });
   Check(ReferenceEquals(before,Get(fresh,fields[t])) && ReferenceEquals(dur,Get(fresh,"Dur")),"Publicacion parcial "+t);
   Check(version.Equals(Get(fresh,"EqVersion")),"Publica version incompleta");
   object expected=New(dataType);
   for(int j=0;j<6;j++)using(StringReader r=new StringReader(text[j]))OldRead(j,r,expected);
   using(FaultReader r=new FaultReader(text[t],2))ExpectIo(delegate { OldRead(t,r,old); });
   Equal(Get(old,fields[t]),Get(expected,fields[t]),"tabla viva tras fallo");
  }
  // La carga normal tambien debe respetar niveles ya aprendidos en tablas vivas.
  Dictionary<string,int> ia=(Dictionary<string,int>)Get(old,"Ia");
  ia["guardada.mp3"]=9;
  using(StringReader r=new StringReader("1|guardada.mp3\n"))OldRead(2,r,old);
  Check(ia["guardada.mp3"]==9,"Degrada cosecha existente");
 }
 static void Cancellation()
 {
  object load=New(loadType);ObservedReader inner=new ObservedReader("1|a\n2|b\n");
  TextReader reader=(TextReader)Activator.CreateInstance(readerType,F,null,new object[]{inner,load},null);
  using(reader) {
   Check(reader.ReadLine()=="1|a","Primera linea");
   Set(load,"Cancelled",true);
   try {reader.ReadLine();throw new Exception("Ignora cancelacion");}catch(OperationCanceledException){}
   Check(inner.Reads==1,"Lee despues de cancelar");
  }
  Check(inner.Closed,"No cierra lector");
  load=New(loadType);inner=new ObservedReader("1|a\n");
  object target=load;
  inner.OnRead=delegate {Set(target,"Cancelled",true);};
  using(TextReader r=(TextReader)Activator.CreateInstance(readerType,F,null,new object[]{inner,load},null)) {
   try {r.ReadLine();throw new Exception("Publica registro tras cancelacion");}catch(OperationCanceledException){}
  }
  Check(inner.Closed,"Cancelacion no dispone");
  Check(Call("ReadStartupData",load)==null,"Carga cancelada produce datos");
 }
 static void FilesAndWorker()
 {
  string dir=Path.Combine(Path.GetTempPath(),"rocola-tablas-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(dir);
  try {
   string[] texts=Fixtures(),paths=new string[6];object expected=New(dataType);
   for(int i=0;i<6;i++){
    paths[i]=Path.Combine(dir,i+".txt");File.WriteAllText(paths[i],texts[i],Encoding.UTF8);
    using(StringReader r=new StringReader(texts[i]))OldRead(i,r,expected);
   }
   object load=Call("CreateStartupLoad",(object)paths);paths[0]="cambio-del-llamador";
   object actual=null;bool done=false;
   for(int i=0;i<500;i++) {
    lock(load){done=(bool)Get(load,"Done");if(done)actual=Get(load,"Data");}
    if(done)break;Thread.Sleep(10);
   }
   Check(done&&actual!=null,"Trabajador no entrega");
   for(int i=0;i<6;i++)Equal(Get(expected,fields[i]),Get(actual,fields[i]),"archivo "+i);
   Equal(Get(expected,"Dur"),Get(actual,"Dur"),"archivo duraciones");
   // Apertura exclusiva demuestra que ya se cerraron los seis lectores.
   for(int i=0;i<6;i++)using(FileStream f=new FileStream(Path.Combine(dir,i+".txt"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(f.Length>0,"Archivo vacio");
   // Un archivo ausente no impide recuperar las otras tablas.
   paths[0]=Path.Combine(dir,"ausente");load=New(loadType);Set(load,"Paths",paths);
   actual=Call("ReadStartupData",load);
   Check(((IDictionary)Get(actual,"Loud")).Count==0,"Ausente inventa datos");
   Equal(Get(expected,"Fast"),Get(actual,"Fast"),"continua otros archivos");
   Check(((HashSet<string>)Get(actual,"Deep")).Count==0,"Acepta repaso sin sonoridad");
  } finally {Directory.Delete(dir,true);}
 }
 static void Benchmark()
 {
  StringBuilder s=new StringBuilder();
  for(int i=0;i<100000;i++)s.Append("180|cancion-").Append(i).Append(".mp3\n");
  string input=s.ToString();
  object warm=New(dataType);
  using(StringReader r=new StringReader(input))OldRead(4,r,warm);
  MethodInfo allocated=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);
  for(int mode=0;mode<2;mode++) {
   object data=New(dataType);
   long start=allocated==null?0:(long)allocated.Invoke(null,null);
   Stopwatch watch=Stopwatch.StartNew();
   using(StringReader r=new StringReader(input)){
    if(mode==0)OldRead(4,r,data);else Call("ReadStartupTableRecords",r,4,data);
   }
   watch.Stop();
   long bytes=allocated==null?0:(long)allocated.Invoke(null,null)-start;
   Check(((IDictionary)Get(data,"Fast")).Count==100000,"Pierde registros masivos");
   Console.WriteLine("  100000 duraciones, "+(mode==0?"copia":"adopcion")+": "+watch.ElapsedMilliseconds+" ms"+(allocated==null?"":", "+bytes+" bytes asignados")+" (sintetico; no mide HDD/FPS).");
  }
  GC.KeepAlive(warm);
 }
 [STAThread] static void Main(string[] args)
 {
  Assembly a=Assembly.LoadFrom(args[0]);form=a.GetType("Rocola.Form1");
  dataType=form.GetNestedType("StartupData",F);loadType=form.GetNestedType("StartupLoad",F);readerType=form.GetNestedType("StartupTextReader",F);
  CompareAndFailure();Cancellation();FilesAndWorker();Benchmark();
  Console.WriteLine("  OK    seis tablas equivalentes, adopcion sin copia, fallos sin publicacion parcial, cancelacion, BOM, ausencia y cierre de lectores.");
 }
}
