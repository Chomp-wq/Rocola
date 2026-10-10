using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NAudio.Wave;
class MemoriaAnalisis
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static object Field(object x, string n) { return x.GetType().GetField(n, B).GetValue(x); }
    static object Call(Type t, object x, string n, params object[] a) { return t.GetMethod(n, B).Invoke(x, a); }
    static void Set(Type t, object x, string n, object value) { t.GetField(n, B).SetValue(x, value); }
    static bool Bytes(byte[] a, byte[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static byte[] Bits(float[] a)
    {
        if (a == null) return null;
        byte[] b = new byte[a.Length * 4]; Buffer.BlockCopy(a, 0, b, 0, b.Length); return b;
    }
    static void Value(BinaryWriter writer, object value)
    {
        if (value == null) { writer.Write("null"); return; }
        Type t = value.GetType(); writer.Write(t.FullName);
        if (t == typeof(float)) writer.Write((float)value);
        else if (t == typeof(double)) writer.Write((double)value);
        else if (t == typeof(int)) writer.Write((int)value);
        else if (t == typeof(long)) writer.Write((long)value);
        else if (t == typeof(bool)) writer.Write((bool)value);
        else if (t == typeof(string)) writer.Write((string)value);
        else if (t == typeof(byte)) writer.Write((byte)value);
        else if (t == typeof(uint)) writer.Write((uint)value);
        else if (t == typeof(ulong)) writer.Write((ulong)value);
        else if (t.IsValueType)
        {
            FieldInfo[] fields = t.GetFields(B);
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b) { return String.CompareOrdinal(a.Name, b.Name); });
            foreach (FieldInfo f in fields) if (!f.IsStatic) { writer.Write(f.Name); Value(writer, f.GetValue(value)); }
        }
        else throw new Exception("Tipo no cubierto en snapshot: " + t);
    }
    class Snapshot
    {
        internal byte[] Measure, Corpus, Index;
        internal bool Ok;
        internal float[] Curve; internal byte[] CurveAtReturn;
    }
    static Snapshot Measure(Type form, string file, string data, bool harvest, bool deep, bool cancel)
    { return Measure(form, file, data, harvest, deep, cancel, false); }
    static Snapshot Measure(Type form, string file, string data, bool harvest, bool deep, bool cancel, bool videoRoute)
    {
        Directory.CreateDirectory(data);
        Set(form, null, "carpetaDatos", data);
        object owner = Codigo.FormSinEncender(form);
        Set(form, owner, "iaHarvestOn", harvest);
        object[] a = { file, deep, 0f, 0f, null };
        int polls = 0; Func<bool> cancellation = delegate { return ++polls >= 4; };
        Set(form, null, "measureCancellation", cancel ? cancellation : null);
        // Ejercitar la politica de video con PCM sintetico conocido. No pretende probar un codec de video.
        string[] videoExt = (string[])form.GetField("VideoExt", B).GetValue(null);
        string firstExt = videoExt[0]; if (videoRoute) videoExt[0] = Path.GetExtension(file);
        bool ok;
        try { ok = (bool)Call(form, owner, "MeasureFile", a); }
        finally { if (videoRoute) videoExt[0] = firstExt; Set(form, null, "measureCancellation", null); }
        Snapshot s = new Snapshot(); s.Ok = ok; s.Curve = a[4] as float[]; s.CurveAtReturn = Bits(s.Curve);
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(ok); writer.Write((float)a[2]); writer.Write((float)a[3]);
            byte[] curve = s.CurveAtReturn; writer.Write(curve == null ? -1 : curve.Length);
            if (curve != null) writer.Write(curve);
            bool ready = (bool)form.GetField("integradaLista", B).GetValue(null); writer.Write(ready);
            if (ready) Value(writer, form.GetField("integradaValor", B).GetValue(null));
            writer.Flush(); s.Measure = stream.ToArray();
        }
        string corpus = Path.Combine(data, "rocola_ia_datos.bin"), index = Path.Combine(data, "rocola_ia_datos.idx");
        s.Corpus = File.Exists(corpus) ? File.ReadAllBytes(corpus) : null;
        s.Index = File.Exists(index) ? File.ReadAllBytes(index) : null;
        return s;
    }
    static string Wave(string root, int rate, int channels, double seconds, bool silent, int seed)
    {
        string file = Path.Combine(root, rate + "-" + channels + "-" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + silent + "-" + seed + ".wav");
        if (File.Exists(file)) return file;
        Random random = new Random(seed); float[] b = new float[4096 * channels];
        long total = (long)(seconds * rate);
        using (WaveFileWriter w = new WaveFileWriter(file, WaveFormat.CreateIeeeFloatWaveFormat(rate, channels)))
        {
            for (long at = 0; at < total;)
            {
                int n = (int)Math.Min(4096, total - at);
                for (int i = 0; i < n; i++)
                {
                    double time = (at + i) / (double)rate;
                    float x = silent || (time > 1 && time < 1.6) ? 0f : (float)(.20 * (random.NextDouble() - .5));
                    if (!silent && (at + i) % 17011 < 6) x = .998f;
                    for (int c = 0; c < channels; c++) b[i * channels + c] = (c & 1) == 0 ? x : -x * .75f;
                }
                w.WriteSamples(b, 0, n * channels); at += n;
            }
        }
        return file;
    }
    static void Equivalence(Type form, Type before, string root)
    {
        string waveRoot = Path.Combine(root, "wav"); Directory.CreateDirectory(waveRoot);
        int[] rates = { 44100,44100,48000,48000,22050,96000,32000,16000,8000,44100,44100,44100,44100,48000,44100 };
        int[] channels = { 2,1,6,2,1,2,2,8,9,2,2,2,2,2,2 };
        double[] seconds = { 12,25,1.3,2.3,2.2,1.3,25,2.6,.2,61,2.3,2.3,12,2.3,75 };
        Snapshot previous = null;
        int recorded = 0;
        for (int i = 0; i < rates.Length; i++)
        {
            bool silent = i == 3 || i == 10;
            bool cancel = i == 11;
            bool harvest = i % 2 == 0 || i == 12;
            bool deep = i == 12 || i == 14;
            string file = Wave(waveRoot, rates[i], channels[i], seconds[i], silent, i + 73);
            Snapshot expected = Measure(before, file, Path.Combine(root, "base-" + i), harvest, deep, cancel);
            Snapshot actual = Measure(form, file, Path.Combine(root, "nuevo-" + i), harvest, deep, cancel);
            Check(expected.Ok == !cancel && actual.Ok == !cancel, "Mide WAV valido o cancela de verdad: caso " + i);
            Check(Bytes(expected.Measure, actual.Measure), "RMS/duracion/EQ/integrada/picos/silencios bit a bit: caso " + i);
            Check(Bytes(expected.Corpus, actual.Corpus), "Bytes de cosecha exactos: caso " + i);
            Check(Bytes(expected.Index, actual.Index), "Indice y CRC/origen de cosecha exactos: caso " + i);
            if (actual.Corpus != null && actual.Corpus.Length > 16) recorded++;
            if (previous != null)
            {
                Check(Bytes(previous.CurveAtReturn, Bits(previous.Curve)), "Siguiente cancion no modifica el resultado publicado");
                if (previous.Curve != null && actual.Curve != null) Check(!Object.ReferenceEquals(previous.Curve, actual.Curve), "Curvas publicadas independientes");
            }
            previous = actual;
            object scratch = form.GetField("trabajoMedidaDelHilo", B).GetValue(null);
            Check(scratch == null || !(bool)Field(scratch, "EnUso"), "Salida/cancelacion suelta el workspace: caso " + i);
            if (i == 14)
            {
                Check(actual.Corpus != null && actual.Corpus.Length > 2 * 65536 * 4,
                    "Cosecha profunda supera el limite retenido: los datos no se recortan");
                Array lists = (Array)Field(scratch, "pares");
                Check(lists.GetValue(0) == null && lists.GetValue(1) == null,
                    "Cosecha profunda productiva descarta ambos buffers grandes al acabar");
            }
        }
        Check(recorded >= 2, "Comparacion incluye cosecha real sintetica, no solo listas vacias");
        string broken = Path.Combine(waveRoot, "roto.wav");
        using (BinaryWriter writer = new BinaryWriter(File.Create(broken))) writer.Write(0x12345678);
        Check(Bytes(Measure(before, broken, Path.Combine(root,"base-roto"),true,false,false).Measure,
            Measure(form, broken, Path.Combine(root,"nuevo-roto"),true,false,false).Measure), "Archivo roto conserva resultado/fallback");
        string videoFile = Wave(waveRoot, 44100, 2, 12, false, 404);
        Snapshot videoBefore = Measure(before, videoFile, Path.Combine(root,"base-video"),true,false,false,true);
        Snapshot video = Measure(form, videoFile, Path.Combine(root,"nuevo-video"),true,false,false,true);
        Check(videoBefore.Ok && video.Ok && Bytes(videoBefore.Measure, video.Measure), "Ruta video conserva medida exacta");
        Check(video.Curve == null && video.Corpus == null && video.Index == null && !(bool)form.GetField("integradaLista", B).GetValue(null),
            "Ruta video no aplica EQ, integrada ni cosecha aunque estaban calientes");
        object videoScratch = form.GetField("trabajoMedidaDelHilo", B).GetValue(null);
        Check(videoScratch != null && !(bool)Field(videoScratch,"EnUso"), "Ruta video devuelve workspace");
        Check(Bytes(Measure(before, videoFile, Path.Combine(root,"base-tras-video"),false,false,false).Measure,
            Measure(form, videoFile, Path.Combine(root,"nuevo-tras-video"),false,false,false).Measure), "Volver a musica tras video no deja filtros viejos");
        Console.WriteLine("  OK equivalencia completa, cancelacion y resultados publicados; " + recorded + " cosechas sinteticas no vacias");
    }
    static object Acquire(Type form, int rate, int channels, bool tonal) { return Call(form, null, "TomaTrabajoMedida", rate, channels, tonal); }
    static void Release(object w) { Call(w.GetType(), w, "Suelta"); }
    sealed class Silent : ISampleProvider
    {
        public WaveFormat WaveFormat { get { return WaveFormat.CreateIeeeFloatWaveFormat(44100, 1); } }
        public int Read(float[] buffer, int offset, int count) { Array.Clear(buffer, offset, count); return count; }
    }
    static void Scratch(Type form)
    {
        object w = Acquire(form, 44100, 8, true);
        float[] buffer = (float[])Field(w,"Buffer"), state = (float[])Field(w,"Estado"), bpState=(float[])Field(w,"EstadoBandas");
        double[] sum=(double[])Field(w,"SumaBandas"); int[] clip=(int[])Field(w,"Recorte");
        buffer[buffer.Length-1]=state[state.Length-1]=bpState[bpState.Length-1]=9;sum[7]=9;clip[7]=9;
        object nested=Acquire(form,44100,2,true);
        Check(!Object.ReferenceEquals(w,nested)&&!Object.ReferenceEquals(buffer,Field(nested,"Buffer")), "Reentrada no comparte workspace/buffer");
        Check(state[state.Length-1]==9&&(bool)Field(w,"EnUso"), "Reentrada no borra estado externo");
        Release(nested); Check((bool)Field(w,"EnUso"),"Soltar reentrada no libera medida externa");
        Release(w);
        object next=Acquire(form,44100,1,true);
        Check(Object.ReferenceEquals(w,next)&&Object.ReferenceEquals(buffer,Field(next,"Buffer")), "Siguiente cancion reutiliza el mismo buffer");
        Check((int)Field(next,"LecturaMax")==4096&&buffer.Length==4096*8, "Cambio a mono conserva conteo logico, no usa capacidad");
        Check(state[state.Length-1]==0&&bpState[bpState.Length-1]==0&&sum[7]==0&&clip[7]==0&&buffer[buffer.Length-1]==0, "Estado incluso sobrante completamente reseteado");
        object tap=Call(next.GetType(),next,"Analizador",0,44100);
        float[] frame=(float[])Call(next.GetType(),next,"Cuadro",0,2048);
        for(int i=0;i<frame.Length;i++)frame[i]=(float)(.1*Math.Sin(i*2*Math.PI*5200/44100));
        Call(tap.GetType(),tap,"AnalyzeFrame",frame,0);
        Check((float)tap.GetType().GetProperty("CutHz",B).GetValue(tap,null)>0, "Analizador caliente realmente tiene historia");
        Release(next); next=Acquire(form,44100,2,true);
        object reused=Call(next.GetType(),next,"Analizador",0,44100);
        Check(Object.ReferenceEquals(tap,reused)&&(float)tap.GetType().GetProperty("CutHz",B).GetValue(tap,null)==0&&
            (float)tap.GetType().GetProperty("CutLoHz",B).GetValue(tap,null)==0, "Analizador se reutiliza sin cortes previos");
        Set(tap.GetType(), tap, "cutHz", 7000f);
        Check(!(bool)Call(tap.GetType(), tap, "ReiniciaAnalisis", 48000) && (float)Field(tap, "cutHz") == 7000f,
            "Reset rechaza otro formato sin alterar estado");
        ConstructorInfo hdPlayback = tap.GetType().GetConstructor(new Type[] { typeof(ISampleProvider), typeof(int) });
        object playback = hdPlayback != null ? hdPlayback.Invoke(new object[] { new Silent(), 2048 }) :
            Activator.CreateInstance(tap.GetType(), B, null, new object[] { new Silent() }, null);
        Set(tap.GetType(), playback, "cutHz", 7000f); object playbackBuffer = Field(playback, "inBuf");
        Check(!(bool)Call(tap.GetType(), playback, "ReiniciaAnalisis", 44100) && (float)Field(playback, "cutHz") == 7000f &&
            Object.ReferenceEquals(playbackBuffer, Field(playback, "inBuf")), "Reset nunca toca STFT/fuentes de reproduccion");
        Check(Object.ReferenceEquals(frame,Call(next.GetType(),next,"Cuadro",0,2048)), "Ventana 2048 reutilizada exacta");
        object right=Call(next.GetType(),next,"Analizador",1,44100), quality=Call(next.GetType(),next,"Analizador",2,44100);
        Check(!Object.ReferenceEquals(tap,right)&&!Object.ReferenceEquals(tap,quality)&&!Object.ReferenceEquals(right,quality), "Calidad y canales L/R separados");
        List<float> pairs=(List<float>)Call(next.GetType(),next,"Pares",0);pairs.Capacity=65536;pairs.Add(1);
        Release(next);next=Acquire(form,44100,2,true);
        Check(Object.ReferenceEquals(pairs,Call(next.GetType(),next,"Pares",0))&&pairs.Count==0, "Lista limitada reutilizada y vaciada");
        pairs.Capacity=65537;pairs.Add(1);Release(next);next=Acquire(form,44100,2,true);
        List<float> small=(List<float>)Call(next.GetType(),next,"Pares",0);
        Check(!Object.ReferenceEquals(pairs,small)&&small.Capacity==4096,"Lista grande no retenida tras entrenamiento profundo");
        object huge=Acquire(form,44100,9,true);
        Check(!Object.ReferenceEquals(next,huge)&&((float[])Field(huge,"Buffer")).Length==4096*9,"Multicanal grande se mide sin retener ni limitar");
        Release(huge);Release(next); next=Acquire(form,22050,2,true);
        object changed=Call(next.GetType(),next,"Analizador",0,22050);
        Check(!Object.ReferenceEquals(tap,changed),"Otro formato reemplaza analizador, no conserva un cache por frecuencia");
        float[][] bands=(float[][])Field(next,"Bandas");
        Check(bands[7]==null,"Bandas fuera de Nyquist siguen ausentes");
        Release(next);
        object[] workers=new object[2]; Exception failure=null;
        Thread[] threads=new Thread[2];
        for(int i=0;i<2;i++)
        {
            int slot=i;threads[i]=new Thread(delegate(){try{workers[slot]=Acquire(form,48000,2,true);Release(workers[slot]);}catch(Exception e){failure=e;}});
            threads[i].Start();
        }
        foreach(Thread thread in threads)Check(thread.Join(10000),"Trabajador termina sin bloqueo");
        if(failure!=null)throw failure;
        Check(!Object.ReferenceEquals(workers[0],workers[1])&&!Object.ReferenceEquals(Field(workers[0],"Buffer"),Field(workers[1],"Buffer")),"Hilos independientes, sin buffers compartidos");
        foreach(FieldInfo f in w.GetType().GetFields(B)) if(!f.IsStatic)
            Check(f.FieldType!=form&&f.FieldType!=typeof(string)&&!typeof(Delegate).IsAssignableFrom(f.FieldType)&&!typeof(IDisposable).IsAssignableFrom(f.FieldType),"Scratch no retiene propietario/ruta/lector/delegado");
        Console.WriteLine("  OK buffers, reentrada, hilos, formato, Nyquist, reset y limites de retencion");
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Assembly current=Assembly.LoadFrom(args[0]);
            Type form=current.GetType("Rocola.Form1",true);
            Scratch(form);
            if(args.Length>1)
            {
                Assembly baseline=Assembly.Load(File.ReadAllBytes(args[1]));
                string root=Path.Combine(Path.GetTempPath(),"rocola-memoria-analisis-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
                Equivalence(form,baseline.GetType("Rocola.Form1",true),root);
                Console.WriteLine("  Evidencia aislada: "+root);
            }
            Check(Codigo.Llama(form.GetMethod("MeasureFile",B),"TomaTrabajoMedida")&&Codigo.Llama(form.GetMethod("MeasureFile",B),"Suelta"),"Ruta productiva adquiere y devuelve workspace");
            Console.WriteLine("PASS memoriaanalisis: "+checks+" comprobaciones; no reproduce audio ni mide FPS");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
