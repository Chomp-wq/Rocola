using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class Optimizaciones
{
 const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
 static object Call(Type t, object o, string name, params object[] a) { return t.GetMethod(name,F).Invoke(o,a); }
 static void Check(bool v, string s) { if(!v) throw new Exception(s); }
 static int[] Pixels(Bitmap b) {
  BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
  try { int[] a=new int[b.Width*b.Height]; for(int y=0;y<b.Height;y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64()+(long)y*d.Stride),a,y*b.Width,b.Width); return a; }
  finally { b.UnlockBits(d); }
 }
 static void Equal(Array a, Array b, string s) {
  Check(a.Length==b.Length,s);
  int[] ai=a as int[],bi=b as int[];if(ai!=null&&bi!=null){for(int i=0;i<ai.Length;i++)if(ai[i]!=bi[i])throw new Exception(s+" @"+i);return;}
  float[] af=a as float[],bf=b as float[];if(af!=null&&bf!=null){for(int i=0;i<af.Length;i++)if(af[i]!=bf[i])throw new Exception(s+" @"+i);return;}
  for(int i=0;i<a.Length;i++)if(!a.GetValue(i).Equals(b.GetValue(i)))throw new Exception(s+" @"+i);
 }
 static Bitmap Render(Type t,int w,int h,string time,bool transparent,int count) {
  string[] titles=new string[count],artists=new string[count];Image[] covers=new Image[count];
  for(int i=0;i<count;i++){titles[i]="Cancion "+i;artists[i]="Artista";}
  return (Bitmap)Call(t,null,"RenderIdlePanelQueue",w,h,"Titulo","Artista actual",time,titles,artists,covers,count,Color.CornflowerBlue,transparent,2,true);
 }
 [StructLayout(LayoutKind.Sequential)] struct Locked { public int Pitch; public IntPtr Bits; }
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int LockTex(IntPtr self,uint level,out Locked data,IntPtr rect,uint flags);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int UnlockTex(IntPtr self,uint level);
 static int[] ReadTexture(IntPtr texture,int w,int h) {
  IntPtr vt=Marshal.ReadIntPtr(texture);
  LockTex take=(LockTex)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vt,19*IntPtr.Size),typeof(LockTex));
  UnlockTex give=(UnlockTex)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vt,20*IntPtr.Size),typeof(UnlockTex));
  Locked data;Check(take(texture,0,out data,IntPtr.Zero,16)>=0,"Lock textura");
  try {int[] p=new int[w*h];for(int y=0;y<h;y++)Marshal.Copy(new IntPtr(data.Bits.ToInt64()+(long)y*data.Pitch),p,y*w,w);return p;}
  finally {Check(give(texture,0)>=0,"Unlock textura");}
 }
 static void TestRegion(Assembly assembly) {
  Type device=assembly.GetType("Rocola.D3D9Device");
  using(Control host=new Control())
  using(Bitmap image=new Bitmap(640,220,PixelFormat.Format32bppPArgb)) {
   host.Size=new Size(640,480);
   object gpu=Activator.CreateInstance(device,F,null,new object[]{host.Handle,640,480},null);
   try {
    Check((bool)device.GetProperty("Ok",F).GetValue(gpu,null),"HAL no disponible");
    using(Graphics g=Graphics.FromImage(image))g.Clear(Color.FromArgb(180,31,83,167));
    object[] create={image,0f,0f};IntPtr texture=(IntPtr)Call(device,gpu,"CreateTexturePow2",create);
    Check(texture!=IntPtr.Zero,"No crea textura");
    try {
     using(Graphics g=Graphics.FromImage(image)) {
      g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
      using(Brush b=new SolidBrush(Color.FromArgb(73,220,160,70)))g.FillRectangle(b,11,186,190,34);
     }
     Check((bool)Call(device,gpu,"UpdateTextureRegion",texture,image,new Rectangle(11,186,190,34)),"No actualiza region");
     IntPtr full=(IntPtr)Call(device,gpu,"CreateTexturePow2",new object[]{image,0f,0f});
     Check(full!=IntPtr.Zero,"No crea referencia");
     try {Equal(ReadTexture(texture,640,220),ReadTexture(full,640,220),"Subida regional distinta");}
     finally {Marshal.Release(full);}
     Check(!(bool)Call(device,gpu,"UpdateTextureRegion",texture,image,new Rectangle(-1,0,10,10)),"Acepta region invalida");
    } finally {Marshal.Release(texture);}
   } finally {Call(device,gpu,"Dispose");}
  }
 }

 static object Blank(Type t) {
  object o=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
  foreach(FieldInfo f in t.GetFields(F))
   if(!f.IsStatic&&f.FieldType.Namespace!=null&&f.FieldType.Namespace.StartsWith("System.Collections")&&f.FieldType.GetConstructor(Type.EmptyTypes)!=null)
    f.SetValue(o,Activator.CreateInstance(f.FieldType));
  return o;
 }
 static void CheckNoPerformanceSwitches(Control control) {
  Check(control.Text!="Optimizaciones"&&!control.Text.Contains("Cache de portadas")&&!control.Text.Contains("Optimizar seleccion"),"Control de rendimiento visible: "+control.Text);
  foreach(Control child in control.Controls)CheckNoPerformanceSwitches(child);
 }
 static void TestSettingsAndJobs(Type form,string root,string source) {
  form.GetField("carpetaDatos",F).SetValue(null,root);
  object owner=Blank(form);
  string config=Path.Combine(root,"rocola_config.txt");
  File.WriteAllText(config,"rendimiento_opciones=0\r\ncache_portadas_gpu=0\r\nbuscador_cache=0\r\nprogreso_neon=0\r\n");
  Call(form,owner,"LoadConfigPrefs");
  Check(!(bool)Get(form,owner,"progresoNeon"),"Cambia opcion visual antigua");
  Call(form,owner,"SaveConfigPrefs");
  string saved=File.ReadAllText(config);
  Check(!saved.Contains("rendimiento_opciones=")&&!saved.Contains("cache_portadas_gpu=")&&!saved.Contains("buscador_cache="),"Persiste preferencias de rendimiento retiradas");
  Check(form.GetField("perfOptions",F)==null&&form.GetField("buscadorCache",F)==null&&form.GetField("cachePortadasGpu",F)==null,"Quedan interruptores internos");
  using(Font font=new Font("Tahoma",10,FontStyle.Bold))
  foreach(string method in new string[]{"PaginaImagen","PaginaFondo"})
  using(Panel page=(Panel)Call(form,owner,method,font,font,font,font,Color.White,Color.Gray,Color.Gray)) {
   CheckNoPerformanceSwitches(page);
  }
  if(form.GetMethod("IaRequestJob",F)==null)return;
  string cache=Path.Combine(root,"ia");Directory.CreateDirectory(cache);
  string pipeline=new string('a',64);File.WriteAllText(Path.Combine(cache,"modelo.fingerprint"),pipeline);
  form.GetField("iaCacheDir",F).SetValue(owner,cache);
  form.GetField("iaMezcla",F).SetValue(owner,"0.60");
  object gate=new object();form.GetField("iaRequestLock",F).SetValue(owner,gate);
  lock(gate) {
   for(int i=0;i<100;i++)Call(form,owner,"IaRequestJob",source);
   object queue=form.GetField("iaRequestQueue",F).GetValue(owner);
   Check((int)queue.GetType().GetProperty("Count").GetValue(queue,null)==1,"No deduplica solicitudes");
   form.GetField("iaMezcla",F).SetValue(owner,"0.80");
  }
  Stopwatch wait=Stopwatch.StartNew();
  while(true) {
   bool working;lock(gate)working=(bool)form.GetField("iaRequestWorker",F).GetValue(owner);
   if(!working)break;
   Check(wait.ElapsedMilliseconds<10000,"Trabajador Apollo no termina");
   System.Threading.Thread.Sleep(10);
  }
  string hash=(string)Call(form,null,"IaHashFor",source,"0.60",pipeline);
  string job=Path.Combine(Path.Combine(cache,"pendientes"),hash+".job");
  Check(File.Exists(job)&&File.ReadAllText(job).Contains("\"mezcla\":\"0.60\""),"No conserva instantanea de mezcla");
  Call(form,owner,"IaRequestJob",source);
  wait=Stopwatch.StartNew();
  while(true) {
   bool working;lock(gate)working=(bool)form.GetField("iaRequestWorker",F).GetValue(owner);
   if(!working)break;
   Check(wait.ElapsedMilliseconds<10000,"Segunda solicitud Apollo no termina");
   System.Threading.Thread.Sleep(10);
  }
  Check(Directory.GetFiles(Path.Combine(cache,"pendientes"),"*.job").Length==2,"No publica mezcla nueva");
  Check(Directory.GetFiles(Path.Combine(cache,"pendientes"),"*.tmp").Length==0,"Quedan temporales");
 }

 static string SearchResult(Type form,object owner) {
  object view=form.GetField("searchOverlay",F).GetValue(owner);
  System.Collections.IList items=(System.Collections.IList)view.GetType().GetField("Results",F).GetValue(view);
  System.Text.StringBuilder text=new System.Text.StringBuilder();
  foreach(object item in items){Type t=item.GetType();text.Append(t.GetField("Display",F).GetValue(item)).Append('|').Append(t.GetField("Score",F).GetValue(item)).Append(';');}
  return text.ToString();
 }
 static void TestSearch(Type form) {
  object owner=Blank(form);
  Type itemType=form.GetNestedType("SearchItem",F),viewType=form.GetNestedType("SearchOverlay",F);
  using(Control view=(Control)Activator.CreateInstance(viewType,true)) {
   form.GetField("searchOverlay",F).SetValue(owner,view);
   System.Collections.IList index=(System.Collections.IList)form.GetField("searchIndex",F).GetValue(owner);
   for(int i=0;i<20000;i++){
    string key=(i%3==0?"depeche mode noche ":"amor camino disco ")+i.ToString();
    object item=Activator.CreateInstance(itemType,true);
    itemType.GetField("Key",F).SetValue(item,key);itemType.GetField("Display",F).SetValue(item,key);
    itemType.GetField("Mask",F).SetValue(item,Call(form,null,"MarcaLetras",key));
    itemType.GetField("Kind",F).SetValue(item,i%3);index.Add(item);
   }
   string[] queries={"am","amor","amor ca","de","depeche","mode noche","noche","999","zzzz","amor camino","am"};
   string[] expected=new string[queries.Length];
   for(int i=0;i<queries.Length;i++){Call(form,owner,"ClearRecentQueries");Call(form,owner,"RecomputeResults",queries[i]);expected[i]=SearchResult(form,owner);}
   for(int pass=0;pass<3;pass++) for(int i=0;i<queries.Length;i++) {
    Call(form,owner,"RecomputeResults",queries[i]);
    Check(SearchResult(form,owner)==expected[i],"Buscador cambia orden/puntos: "+queries[i]);
   }
   for(int i=0;i<24;i++)Call(form,owner,"RecomputeResults","amor "+i);
   System.Collections.IList cache=(System.Collections.IList)form.GetField("recentQueries",F).GetValue(owner);
   Check(cache.Count<=8&&(int)form.GetField("recentQueryItems",F).GetValue(owner)<=65536,"Cache sin limite");
   foreach(int mode in new int[]{31,95}) {
    Call(form,owner,"RecomputeResults","depeche");Call(form,owner,"RecomputeResults","amor");
    Stopwatch watch=Stopwatch.StartNew();
    for(int i=0;i<100;i++){if(mode==31)Call(form,owner,"ClearRecentQueries");Call(form,owner,"RecomputeResults","depeche");if(mode==31)Call(form,owner,"ClearRecentQueries");Call(form,owner,"RecomputeResults","amor");}
    watch.Stop();Console.WriteLine("Buscador 20000: cache "+(mode==95)+" "+(watch.Elapsed.TotalMilliseconds/200).ToString("F3")+" ms/consulta repetida.");
   }
   Call(form,owner,"BuildSearchIndex");
   Check((int)form.GetField("recentQueryItems",F).GetValue(owner)==0,"Indice deja cache vieja");
   Call(form,owner,"RecomputeResults","amor");Check(SearchResult(form,owner)=="","Indice vacio usa resultados viejos");
  }
 }

 static void TestVideoSummary(Type form,string root) {
  object owner=Blank(form);Type albumType=form.GetNestedType("Album",F);
  object album=Activator.CreateInstance(albumType,true);
  System.Collections.IList paths=(System.Collections.IList)Get(albumType,album,"VideoFiles");
  System.Collections.IList albums=(System.Collections.IList)Get(form,owner,"videoAlbums");albums.Add(album);
  for(int i=0;i<20000;i++) {
   string path="video"+i+".mp4";paths.Add(path);
   Call(form,owner,"PublicaMedida",path,.08f,0f,null);
  }
  paths.Add("VIDEO0.MP4");paths.Add(null);albums.Add(album); // duplicados y mayusculas
  Check((int)Call(form,owner,"VideosMedidos")==20000,"Cuenta videos repetidos");
  Check(!(bool)Get(form,owner,"videoMeasurementsDirty"),"No conserva resumen");
  Stopwatch watch=Stopwatch.StartNew();
  for(int i=0;i<200;i++)Check((int)Call(form,owner,"VideosMedidos")==20000,"Recuento estable distinto");
  watch.Stop();Console.WriteLine("Resumen 20000 videos en cache: "+(watch.Elapsed.TotalMilliseconds/200).ToString("F4")+" ms/consulta; no FPS.");
  Set(form,owner,"videoAuto",true);Set(form,owner,"normalizeOn",true);Set(form,owner,"normTarget",.1f);Set(form,owner,"videoVol",85);
  Check((int)Call(form,owner,"VideoBase")==80,"Base de referencia distinta");
  for(int i=0;i<20000;i++)Call(form,owner,"PublicaMedida","video"+i+".mp4",.05f,0f,null);
  Check((int)Call(form,owner,"VideoBase")==50,"Base obsoleta con el mismo numero de medidas");
  Call(form,owner,"PublicaMedida","video0.mp4",.00001f,0f,null);
  Check((int)Call(form,owner,"VideosMedidos")==19999,"Silencio conserva contador viejo");
  string loud=(string)Call(form,owner,"LoudFile");
  File.WriteAllText(loud,"0.08|video0.mp4");
  Call(form,owner,"LoadLoudCache");
  Check((int)Call(form,owner,"VideosMedidos")==20000,"Carga de medidas no invalida");
  Call(form,owner,"BuildVideoSection",new System.Collections.Generic.List<string>());
  Check((int)Call(form,owner,"VideosMedidos")==0,"Reconstruccion deja contador viejo");
 }

 static void TestCancellation(Type form,string root) {
  string path=Path.Combine(root,"medida.wav");const int frames=44100*3;
  using(BinaryWriter w=new BinaryWriter(File.Create(path))) {
   w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*2);
   w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
   w.Write(44100);w.Write(88200);w.Write((short)2);w.Write((short)16);
   w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(frames*2);
   for(int i=0;i<frames;i++)w.Write((short)(12000*Math.Sin(2*Math.PI*440*i/44100)));
  }
  object owner=Blank(form);form.GetField("iaDoneLock",F).SetValue(owner,new object());
  Func<bool> never=delegate {return false;};
  object[] full={path,true,never,0f,0f,null};
  Check((bool)Call(form,owner,"MeasureFileResponsive",full),"No mide WAV sintetico");
  Check((float)full[3]>0&&full[5]!=null,"Medida incompleta");
  int calls=0;Func<bool> stop=delegate {return ++calls>=4;};
  object[] cancel={path,true,stop,0f,0f,null};
  Check(!(bool)Call(form,owner,"MeasureFileResponsive",cancel),"Ignora cancelacion por bloque");
  Check(calls==4&&(float)cancel[3]==0&&cancel[5]==null,"Publica resultado parcial");
  using(FileStream exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
   Check(exclusive.Length>0,"No libera archivo cancelado");
  Check(form.GetField("measureCancellation",F).GetValue(null)==null,"Filtra cancelacion a siguiente trabajo");
  object[] legacy={path,true,never,0f,0f,null};
  Check((bool)Call(form,owner,"MeasureFileResponsive",legacy),"Contexto cancelado contamina medida siguiente");
  Check(full[3].Equals(legacy[3])&&full[4].Equals(legacy[4]),"RMS/duracion distintos");
  Equal((Array)full[5],(Array)legacy[5],"EQ distinto");
 }

 static void Set(Type t,object o,string field,object value){t.GetField(field,F).SetValue(o,value);}
 static object Get(Type t,object o,string field){return t.GetField(field,F).GetValue(o);}
 static void CheckCellAccounting(Type t,object panel) {
  long fast=(long)Call(t,panel,"CachedCellBytes");
  Set(t,panel,"cellBytesDirty",true);long full=(long)Call(t,panel,"CachedCellBytes");
  Check(fast==full,"Contabilidad GPU distinta");
 }
 static void TestPanelGpu(Assembly assembly) {
  Type t=assembly.GetType("Rocola.GpuPanel"),cell=assembly.GetType("Rocola.GpuGridCell");
  using(Control panel=(Control)Activator.CreateInstance(t,true))
  using(Bitmap image=new Bitmap(640,220,PixelFormat.Format32bppPArgb))
  using(Bitmap cover=new Bitmap(129,67))
  using(Bitmap larger=new Bitmap(301,89)) {
   panel.Size=new Size(640,480);
   using(Graphics g=Graphics.FromImage(image))g.Clear(Color.FromArgb(130,23,55,170));
   Set(t,panel,"GetIdlePanel",new Func<Bitmap>(delegate{return image;}));
   Set(t,panel,"IdleExpandTarget",true);Set(t,panel,"IdlePanelVersion",1);
   Check((bool)Call(t,panel,"StartGpu"),"No inicia GPU panel");
   try {
    Check((bool)Call(t,panel,"DibujaUnCuadro"),"No dibuja panel");
    IntPtr texture=(IntPtr)Get(t,panel,"idleTex");Check(texture!=IntPtr.Zero,"Falta textura estado");
    int[] before=ReadTexture(texture,640,220);
    using(Graphics g=Graphics.FromImage(image))g.Clear(Color.Gold);
    Set(t,panel,"IdlePanelDirty",new Rectangle(0,186,640,34));Set(t,panel,"IdlePanelVersion",2);
    Call(t,panel,"DibujaUnCuadro");
    Check((IntPtr)Get(t,panel,"idleTex")==texture,"Recrea textura al cambiar reloj");
    int[] after=ReadTexture(texture,640,220);
    for(int i=0;i<640*186;i++)Check(before[i]==after[i],"Sube zona fuera de region");
    Check(after[640*210]!=before[640*210],"No actualiza version del reloj");
    Call(t,panel,"DibujaUnCuadro");Equal(after,ReadTexture(texture,640,220),"Sube sin cambio de version");
    // Una region invalida debe caer a la subida completa, nunca dejar el reloj congelado.
    Set(t,panel,"IdlePanelDirty",new Rectangle(-1,0,10,10));Set(t,panel,"IdlePanelVersion",3);
    Call(t,panel,"DibujaUnCuadro");
    Check(ReadTexture((IntPtr)Get(t,panel,"idleTex"),640,220)[0]!=before[0],"No hace fallback completo");
    for(int i=0;i<150;i++) {
     Set(t,panel,"cellFrame",i+1);
     object item=Activator.CreateInstance(cell);
     Set(cell,item,"Key",i%4==0?null:"album-"+(i%30));
     Set(cell,item,"Slot",i%4);Set(cell,item,"Bmp",i%3==0?larger:cover);
     object[] args={item,10,IntPtr.Zero,0f,0f};
     Check((bool)Call(t,panel,"EnsureCellTex",args),"No crea celda");
     CheckCellAccounting(t,panel);Call(t,panel,"EvictCellTex");CheckCellAccounting(t,panel);
    }
    Set(t,panel,"cellFrame",1000);
    System.Collections.IDictionary cache=(System.Collections.IDictionary)Get(t,panel,"cellTexCache");
    // Simular presion del presupuesto con dimensiones efectivas grandes.
    foreach(object entry in cache.Values){Set(entry.GetType(),entry,"Bw",4096);Set(entry.GetType(),entry,"Bh",4096);}
    Set(t,panel,"cellBytesDirty",true);Call(t,panel,"EvictCellTex");CheckCellAccounting(t,panel);
    Check((long)Call(t,panel,"CachedCellBytes")<=32L*1024*1024,"No respeta limite de texturas");
   } finally {Call(t,panel,"StopGpu");}
   Check((long)Call(t,panel,"CachedCellBytes")==0,"No limpia contabilidad al liberar GPU");
  }
 }

 sealed class AudioInput : NAudio.Wave.ISampleProvider {
  readonly float[] samples; int position; readonly NAudio.Wave.WaveFormat format;
  public AudioInput(int rate,int channels) {
   format=NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(rate,channels);
   samples=new float[36923*channels];
   for(int i=0;i<36923;i++)for(int c=0;c<channels;c++)samples[i*channels+c]=(float)(.1*Math.Sin(2*Math.PI*(733+c*811)*i/rate)+.03*Math.Sin(2*Math.PI*7391*i/rate));
  }
  public NAudio.Wave.WaveFormat WaveFormat {get{return format;}}
  public int Read(float[] buffer,int offset,int count){int n=Math.Min(count,samples.Length-position);Array.Copy(samples,position,buffer,offset,n);position+=n;return n;}
 }
 static NAudio.Wave.ISampleProvider MakeSpectral(Type form,int rate,int channels,int frame) {
  Type tap=form.GetNestedType("SpectralTap",F);
  bool hd=tap.GetConstructor(F,null,new Type[]{typeof(NAudio.Wave.ISampleProvider),typeof(int)},null)!=null;
  object[] parameters=hd?new object[]{new AudioInput(rate,channels),frame}:new object[]{new AudioInput(rate,channels)};
  object result=Activator.CreateInstance(tap,F,null,parameters,null);Set(tap,result,"Active",true);
  return (NAudio.Wave.ISampleProvider)result;
 }
 static void TestPcm(Type form,Type baseline) {
  bool hd=form.GetNestedType("SpectralTap",F).GetConstructor(F,null,new Type[]{typeof(NAudio.Wave.ISampleProvider),typeof(int)},null)!=null;
  foreach(int rate in new int[]{44100,48000})
  foreach(int channels in new int[]{1,2})
  foreach(int frame in hd?new int[]{2048,4096}:new int[]{2048}) {
   NAudio.Wave.ISampleProvider optimized=MakeSpectral(form,rate,channels,frame);
   Set(form,null,"fft2048",null);Set(form,null,"fft4096",null);
   NAudio.Wave.ISampleProvider reference=MakeSpectral(form,rate,channels,frame);
   NAudio.Wave.ISampleProvider old=baseline==null?null:MakeSpectral(baseline,rate,channels,frame);
   int block=1536*channels,total=0;
   float[] a=new float[block+6],b=new float[a.Length],c=new float[a.Length];
   while(true){
    int n=optimized.Read(a,3,block),m=reference.Read(b,3,block);
    Check(n==m,"Longitud PCM cache distinta");Equal(a,b,"PCM cache cambia audio");
    if(old!=null){int p=old.Read(c,3,block);Check(n==p,"Longitud PCM anterior distinta");Equal(a,c,"PCM distinto del Release anterior");}
    if(n==0)break;total+=n;Check(total<=40000*channels,"Audio no termina");
   }
   Check(total==36923*channels,"Trunca final PCM");
  }
 }

 [STAThread] static int Main(string[] args)
 {
  Assembly assembly=Assembly.LoadFrom(args[0]); Type form=assembly.GetType("Rocola.Form1",true);
  TestRegion(assembly);
  TestPanelGpu(assembly);
  Type baseline=args.Length>1 ? Assembly.LoadFile(Path.GetFullPath(args[1])).GetType("Rocola.Form1",true) : null;
  int cases=0;
  foreach(int w in new int[]{480,800,1280,1920,3440})
  foreach(int h in new int[]{220,340})
  foreach(bool transparent in new bool[]{false,true})
  foreach(int count in new int[]{0,2,12})
  using(Bitmap panel=Render(form,w,h,"",transparent,count))
  using(Bitmap background=panel.Clone(new Rectangle(0,h-34,w,34),PixelFormat.Format32bppPArgb))
  foreach(string time in new string[]{"9:59 / 59:59","10:00 / 59:59","0:00 / 0:00",""})
  {
   Call(form,null,"UpdateIdleClock",panel,background,time);
   using(Bitmap full=Render(form,w,h,time,transparent,count)) Equal(Pixels(full),Pixels(panel),"Panel distinto "+w+"x"+h+" "+transparent+" "+time);
   if(baseline!=null) using(Bitmap old=Render(baseline,w,h,time,transparent,count)) Equal(Pixels(old),Pixels(panel),"Cambio respecto al Release anterior");
   cases++;
  }
  using(Bitmap panel=Render(form,3440,340,"",true,12))
  using(Bitmap under=panel.Clone(new Rectangle(0,306,3440,34),PixelFormat.Format32bppPArgb))
  {
   Stopwatch watch=Stopwatch.StartNew();
   for(int i=0;i<100;i++)Call(form,null,"UpdateIdleClock",panel,under,"1:"+i.ToString("00")+" / 4:36");
   watch.Stop();Console.WriteLine("Reloj 3440x340: "+(watch.Elapsed.TotalMilliseconds/100).ToString("F3")+" ms media, banda 34/340 filas; no FPS.");
  }
  TestPcm(form,baseline);
  TestSearch(form);
  foreach(int size in new int[]{2048,4096})
  {
   object cached=Call(form,null,"GetAudioFftPlan",size);
   Check(Object.ReferenceEquals(cached,Call(form,null,"GetAudioFftPlan",size)),"No reutiliza FFT");
   object fresh=Activator.CreateInstance(cached.GetType(),F,null,new object[]{size},null);
   Check(!Object.ReferenceEquals(cached,fresh),"Referencia FFT debe ser independiente");
   foreach(string field in new string[]{"Window","Reverse","Cos","Sin","SinNeg"})
     Equal((Array)cached.GetType().GetField(field,F).GetValue(cached),(Array)fresh.GetType().GetField(field,F).GetValue(fresh),"Tabla FFT");
  }
  Type spectral=form.GetNestedType("SpectralTap",F);
  object shared=Activator.CreateInstance(spectral,F,null,new object[]{44100,2},null);
  Set(form,null,"fft2048",null);Set(form,null,"fft4096",null);
  object privatePlan=Activator.CreateInstance(spectral,F,null,new object[]{44100,2},null);
  Random random=new Random(42);float[] signal=new float[2048];
  for(int frame=0;frame<30;frame++) {
   for(int i=0;i<signal.Length;i++)signal[i]=(float)(random.NextDouble()*.4-.2);
   Call(spectral,shared,"AnalyzeFrame",signal,0);Call(spectral,privatePlan,"AnalyzeFrame",signal,0);
   foreach(string field in new string[]{"re","im"})Equal((Array)spectral.GetField(field,F).GetValue(shared),(Array)spectral.GetField(field,F).GetValue(privatePlan),"Espectro distinto");
  }
  string root=Path.Combine(Path.GetTempPath(),"rocola_perf_test_"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  try {
   byte[] bytes=new byte[150003];random.NextBytes(bytes);
   string source=Path.Combine(root,"datos.bin"),empty=Path.Combine(root,"vacio.bin");
   File.WriteAllBytes(source,bytes);File.WriteAllBytes(empty,new byte[0]);
   TestSettingsAndJobs(form,root,source);
   TestCancellation(form,root);
   TestVideoSummary(form,root);
   for(int mode=0;mode<2;mode++) {
    string dest=Path.Combine(root,"backup"+mode);
    Call(form,null,"BackupTo",dest,new string[]{source,empty,Path.Combine(root,"no-existe")});
    Equal(bytes,File.ReadAllBytes(Path.Combine(dest,"datos.bin")),"Respaldo");
    Check(new FileInfo(Path.Combine(dest,"vacio.bin")).Length==0,"Respaldo vacio");
   }
  } finally { Directory.Delete(root,true); }
  Console.WriteLine("PASS: "+cases+" paneles pixel-identicos; HAL regional, versiones/fallback y 150 cambios de cache; FFT y 30 espectros identicos; consultas, persistencia, Apollo HD, cancelacion y respaldos.");
  return 0;
 }
}
