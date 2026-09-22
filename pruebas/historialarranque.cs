using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

class HistorialArranque
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static Type form,startup,loadType;
 static readonly string[] dictionaries={"songCounts","songSeq","artistCounts","videoCounts","videoSeq","songGenre"};
 static readonly string[] totals={"statsTotal","statsSongTotal","statsVideoTotal","statsCoins","statsCreditsAdded","statsSeq"};
 class Golden
 {
  public Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, int> artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, int> videoCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, long> videoSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, string> songGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
  public long statsTotal, statsSongTotal, statsVideoTotal, statsCoins, statsCreditsAdded, statsSeq;
 }
 // Parser anterior a esta tanda: array completo, mismas reglas de secciones y recencia.
 static Golden Old(string text)
 {
  List<string> lines=new List<string>();
  using(StringReader reader=new StringReader(text)){string line;while((line=reader.ReadLine())!=null)lines.Add(line);}
  string[] L=lines.ToArray();Golden data=new Golden();
                if (L.Length < 1 || L[0] != "ROCOLA-STATS v1") return null;
                int section = 0;   // 0=totales 1=canciones 2=artistas 3=videos
                for (int i = 1; i < L.Length; i++)
                {
                    string line = L[i];
                    if (line.Length == 0) continue;
                    if (line.StartsWith("[CANCIONES]")) { section = 1; continue; }
                    if (line.StartsWith("[ARTISTAS]")) { section = 2; continue; }
                    if (line.StartsWith("[VIDEOS]")) { section = 3; continue; }
                    if (line.StartsWith("[GENEROS]")) { section = 4; continue; }
                    if (section == 0)
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq); long v;
                        if (!long.TryParse(line.Substring(eq + 1), out v)) continue;
                        if (k == "total-reproducciones") data.statsTotal = v;
                        else if (k == "total-canciones") data.statsSongTotal = v;
                        else if (k == "total-videos") data.statsVideoTotal = v;
                        else if (k == "monedas") data.statsCoins = v;
                        else if (k == "creditos-cargados") data.statsCreditsAdded = v;
                    }
                    else
                    {
                        int b = line.IndexOf('|');
                        if (b <= 0) continue;
                        string key = line.Substring(b + 1);
                        if (key.Length == 0) continue;
                        if (section == 4) { data.songGenre[key] = line.Substring(0, b); continue; }   // genero|ruta (lo de antes del | es la etiqueta, no un numero)
                        int cnt; if (!int.TryParse(line.Substring(0, b), out cnt)) continue;
                        if (section == 1) { data.songCounts[key] = cnt; data.songSeq[key] = ++data.statsSeq; }
                        else if (section == 2) data.artistCounts[key] = cnt;
                        else if (section == 3) { data.videoCounts[key] = cnt; data.videoSeq[key] = ++data.statsSeq; }
                    }
                }

  return data;
 }
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Call(object owner,string n,params object[] args){return form.GetMethod(n,F).Invoke(owner,args);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Compare(Golden expected,object actual)
 {
  if(expected==null){Check(actual==null,"Acepta cabecera invalida");return;}
  Check(actual!=null,"Pierde historial valido");
  foreach(string n in dictionaries){
   IDictionary a=(IDictionary)Get(expected,n),b=(IDictionary)Get(actual,n);Check(a.Count==b.Count,n+" cantidad");
   foreach(DictionaryEntry e in a)Check(b.Contains(e.Key)&&e.Value.Equals(b[e.Key]),n+" valor/recencia");
  }
  foreach(string n in totals)Check(Get(expected,n).Equals(Get(actual,n)),n+" total");
 }
 static object Parse(string text){using(StringReader r=new StringReader(text))return Call(null,"ReadStatsRecords",r);}
 static void CheckText(string text)
 {
  Golden expected=Old(text);object actual=Parse(text);Compare(expected,actual);
  if(expected==null)return;
  object left=Codigo.FormSinEncender(form),right=Codigo.FormSinEncender(form);
  foreach(string n in dictionaries)form.GetField(n,F).SetValue(left,Get(expected,n));
  foreach(string n in totals)form.GetField(n,F).SetValue(left,Get(expected,n));
  form.GetField("credits",F).SetValue(right,37);
  object queue=Get(right,"queue");
  Call(right,"PublishStartupStats",actual);
  Check((int)Get(right,"credits")==37&&ReferenceEquals(queue,Get(right,"queue")),"Toca creditos/cola");
  Check((string)Call(left,"BuildStatsText")==(string)Call(right,"BuildStatsText"),"Formato/orden guardado diferente");
  foreach(string n in dictionaries)Check(ReferenceEquals(Get(right,n),Get(actual,n)),"Copia diccionario "+n);
 }
 sealed class FailingReader:StringReader {
  int remaining;public FailingReader(string text,int lines):base(text){remaining=lines;}
  public override string ReadLine(){if(remaining--==0)throw new IOException("EOF no alcanzado");return base.ReadLine();}
 }
 static void Failure()
 {
  object data=Activator.CreateInstance(startup,true);object sentinel=Parse("ROCOLA-STATS v1\nmonedas=51\n");
  startup.GetField("Stats",F).SetValue(data,sentinel);
  for(int fail=0;fail<5;fail++){
   using(FailingReader r=new FailingReader("ROCOLA-STATS v1\nmonedas=7\n[CANCIONES]\n3|a.mp3\n",fail)){
    try{Call(null,"ReadStartupTableRecords",r,6,data);throw new Exception("No detecta error");}
    catch(TargetInvocationException e){Check(e.InnerException is IOException,"Error inesperado");}
   }
   Check(ReferenceEquals(sentinel,Get(data,"Stats")),"Publica historial parcial");
  }
 }
 static void Worker()
 {
  string dir=Path.Combine(Path.GetTempPath(),"rocola-stats-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  CultureInfo previous=Thread.CurrentThread.CurrentCulture;
  object previousDir=form.GetField("carpetaDatos",F).GetValue(null);
  try{
   CultureInfo custom=(CultureInfo)CultureInfo.InvariantCulture.Clone();custom.NumberFormat.NegativeSign="~";
   Thread.CurrentThread.CurrentCulture=custom;
   string text="ROCOLA-STATS v1\nmonedas=~17\n[CANCIONES]\n3|canci\u00f3n.mp3\n~4|B.mp3\n";
   string[] paths=new string[7];for(int i=0;i<paths.Length;i++)paths[i]=Path.Combine(dir,i+".txt");
   foreach(Encoding encoding in new Encoding[]{new UTF8Encoding(false),Encoding.UTF8,Encoding.Unicode}){
    File.WriteAllText(paths[6],text,encoding);
    object load=Call(null,"CreateStartupLoad",(object)paths);object data=null;bool done=false;
    for(int i=0;i<500;i++){
     lock(load){done=(bool)Get(load,"Done");if(done)data=Get(load,"Data");}
     if(done)break;Thread.Sleep(10);
    }
    Check(done&&data!=null,"Trabajador no completa");
    Compare(Old(text),Get(data,"Stats"));
    Check((long)Get(Get(data,"Stats"),"statsCoins")==-17,"Cultura del llamador perdida");
    using(FileStream f=new FileStream(paths[6],FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(f.Length>0,"Lector retenido");
   }
   // Fallback real y cabeceras invalidas no tocan el historial ya publicado.
   form.GetField("carpetaDatos",F).SetValue(null,dir);object owner=Codigo.FormSinEncender(form);
   string statsFile=(string)Call(owner,"LibStatsFile");
   File.WriteAllText(statsFile,text,Encoding.UTF8);Call(owner,"LoadStats");
   Check((long)Get(owner,"statsCoins")==-17,"Fallback pierde contadores");
   object before=Get(owner,"songCounts");
   File.WriteAllText(statsFile,"cabecera invalida");Call(owner,"LoadStats");
   Check(ReferenceEquals(before,Get(owner,"songCounts"))&&(long)Get(owner,"statsCoins")==-17,"Cabecera invalida publica");
   File.Delete(statsFile);Call(owner,"LoadStats");Check(ReferenceEquals(before,Get(owner,"songCounts")),"Ausente publica");
  }finally{Thread.CurrentThread.CurrentCulture=previous;form.GetField("carpetaDatos",F).SetValue(null,previousDir);Directory.Delete(dir,true);}
 }
 static void Allocations()
 {
  MethodInfo allocated=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);
  if(allocated==null)return;   // API de medida opcional: no existe en el CLR de Modest
  StringBuilder text=new StringBuilder("ROCOLA-STATS v1\n[CANCIONES]\n");
  for(int i=0;i<100000;i++)text.Append("2|tema-").Append(i).Append(".mp3\n");
  string input=text.ToString();
  Old("ROCOLA-STATS v1\n");Parse("ROCOLA-STATS v1\n");
  for(int mode=0;mode<2;mode++){
   long before=(long)allocated.Invoke(null,null);
   object data=mode==0?(object)Old(input):Parse(input);
   long bytes=(long)allocated.Invoke(null,null)-before;
   Check(((IDictionary)Get(data,"songCounts")).Count==100000,"Pierde historial grande");
   Console.WriteLine("  100000 entradas, "+(mode==0?"array anterior":"lector por registros")+": "+bytes+" bytes asignados (sintetico; no es pico/RSS ni tiempo de arranque).");
   GC.KeepAlive(data);
  }
 }
 [STAThread] static void Main(string[] args)
 {
  form=Assembly.LoadFrom(args[0]).GetType("Rocola.Form1");startup=form.GetNestedType("StartupData",F);loadType=form.GetNestedType("StartupLoad",F);
  Check(loadType!=null,"Falta lector de arranque");
  foreach(string invalid in new string[]{"","ROCOLA-STATS v2\n"," ROCOLA-STATS v1\n"})CheckText(invalid);
  CheckText("ROCOLA-STATS v1\n");
  CheckText("ROCOLA-STATS v1\nmonedas=10\nmonedas=23\ntotal-canciones=8\ntotal-videos=9\ncreditos-cargados=71\ntotal-reproducciones=17\n[CANCIONES]\n2|A\n4|a\ninvalid\n[GENEROS]\nPop|A\n[VIDEOS]\n3|V\n[ARTISTAS]\n4|Diego\n");
  Random random=new Random(581);
  for(int sample=0;sample<120;sample++){
   StringBuilder text=new StringBuilder("ROCOLA-STATS v1\nmonedas=12\ncreditos-cargados=24\n");
   string[] sections={"[CANCIONES]","[ARTISTAS]","[VIDEOS]","[GENEROS]"};
   for(int j=0;j<160;j++){
    if(j%9==0)text.Append(sections[random.Next(4)]).Append(" count|ruta\n");
    string value=j%7==0?"no":random.Next(-2,90).ToString();
    string key=(j%5==0?"A":"a")+random.Next(15);
    text.Append(value).Append('|').Append(key).Append(j%3==0?"\r\n":"\n");
    if(j%17==0)text.Append("2147483648|overflow\n|vacio\n4|\n\n[desconocida]\n");
   }
   CheckText(text.ToString());
  }
  Failure();Worker();Allocations();
  List<string> shown=Codigo.Nombres(form.GetMethod("OnShown",F));
  Check(shown.IndexOf("CompleteStartupDataLoad")<shown.IndexOf("LoadStats"),"Lee historial antes del trabajador");
  Check(shown.IndexOf("LoadStats")<shown.IndexOf("LoadMusicFolder"),"Biblioteca antes del historial");
  Check(Codigo.Nombres(form.GetMethod("CompleteStartupDataLoad",F)).Contains("PublishStartupStats"),"No publica instantanea");
  Check(!Codigo.Nombres(form.GetMethod("LoadStats",F)).Contains("ReadAllLines"),"Fallback conserva array de lineas");
  Console.WriteLine("  OK    125 historiales equivalentes, recencia y guardado identicos; publicacion por referencia, fallos, BOM, cultura del trabajador y fallback sin tocar creditos/cola.");
 }
}
