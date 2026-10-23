using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using NAudio.Wave;

// PCM de MP3 de prueba, nunca audio de usuario ni HAL. Comparacion con Release
// anterior opcional; sin ella la referencia es AbreMp3 de reproduccion, intacto.
class Mp3MedidaLigera
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type current, legacy, view, formType;
    private sealed class OriginalReader : IDisposable
    {
        private readonly Mp3FileReader reader;
        private readonly NAudio.Wave.SampleProviders.SampleChannel channel;
        private readonly WaveFormat format;
        public OriginalReader(string path)
        {
            reader = (Mp3FileReader)formType.GetMethod("AbreMp3",B,null,new Type[]{typeof(string)},null).Invoke(null,new object[]{path});
            try
            {
                channel = new NAudio.Wave.SampleProviders.SampleChannel(reader,false);
                format = (WaveFormat)Call(formType.Assembly.GetType("Rocola.FormatoAudio",true),null,"DeArchivo",channel.WaveFormat,path);
            }
            catch { reader.Dispose(); throw; }
        }
        public WaveFormat WaveFormat { get { return format; } }
        public TimeSpan TotalTime { get { return reader.TotalTime; } }
        public TimeSpan CurrentTime { set { reader.CurrentTime=value; } }
        public int Read(float[] b,int o,int n) { return channel.Read(b,o,n); }
        public void Dispose() { reader.Dispose(); }
    }
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static object Call(Type t, object o, string n, params object[] a)
    {
        try { return t.GetMethod(n, B).Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void Header(byte[] b, int at, int version, int flags, int size, bool footer)
    {
        b[at] = (byte)(footer ? '3' : 'I'); b[at+1] = (byte)'D'; b[at+2] = (byte)(footer ? 'I' : '3');
        b[at+3] = (byte)version; b[at+4] = 0; b[at+5] = (byte)flags;
        for (int i = 9; i >= 6; i--) { b[at+i] = (byte)(size & 127); size >>= 7; }
    }
    static byte[] AudioBytes(string path)
    {
        byte[] b = File.ReadAllBytes(path); int start = 0;
        if (b.Length >= 10 && b[0] == 'I' && b[1] == 'D' && b[2] == '3')
        {
            start = 10 + ((b[6] & 127) << 21) + ((b[7] & 127) << 14) + ((b[8] & 127) << 7) + (b[9] & 127);
            if (b[3] == 4 && (b[5] & 16) != 0) start += 10;
        }
        byte[] audio = new byte[b.Length - start]; Buffer.BlockCopy(b, start, audio, 0, audio.Length); return audio;
    }
    static string Tagged(string dir, byte[] audio, int version, int flags, int size, bool footer)
    {
        byte[] b = new byte[10 + size + (footer ? 10 : 0) + audio.Length];
        Header(b, 0, version, flags, size, false);
        if (footer) Header(b, 10 + size, version, flags, size, true);
        Buffer.BlockCopy(audio, 0, b, b.Length - audio.Length, audio.Length);
        string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".mp3"); File.WriteAllBytes(path, b); return path;
    }
    static object NewReader(Type t, string path)
    { return Activator.CreateInstance(t, B, null, new object[] { path }, null); }
    static byte[] Samples(Type t, object r)
    {
        using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
        {
            int[] counts = { 1, 7, 4093, 32, 1151, 2304 }; int turn = 0, n;
            float[] b = new float[4103];
            while ((n = (int)Call(t, r, "Read", b, 3, counts[turn++ % counts.Length])) > 0)
                for (int i = 0; i < n; i++) w.Write(b[3+i]);
            w.Flush(); return m.ToArray();
        }
    }
    static bool Equal(byte[] a, byte[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static long Start(byte[] bytes)
    {
        using (MemoryStream m = new MemoryStream(bytes))
        { m.Position = Math.Min(3, m.Length); long p = m.Position; long start = (long)Call(current, null, "InicioAudioMp3", m); Check(m.Position == p, "restaura posicion al validar ID3"); return start; }
    }
    static void Compare(string path, bool light)
    {
        object a = NewReader(legacy, path), b = null;
        try
        {
            b = NewReader(current, path);
            Check(((TimeSpan)legacy.GetProperty("TotalTime", B).GetValue(a,null)).Ticks == ((TimeSpan)current.GetProperty("TotalTime",B).GetValue(b,null)).Ticks, "misma duracion MP3");
            Check(legacy.GetProperty("WaveFormat",B).GetValue(a,null).ToString() == current.GetProperty("WaveFormat",B).GetValue(b,null).ToString(), "mismo formato");
            Stream owner = (Stream)current.GetField("flujoMp3",B).GetValue(b);
            // Desde Mp3Ligero (13-10) el medidor tambien es dueno del FileStream SIN saltar ID3. Lo que distingue
            // saltar la etiqueta del camino conservador es VistaMp3, no que exista un stream propio.
            Check(owner != null && (owner.GetType() == view) == light, "vista solo para ID3 que se puede omitir; si no, archivo completo");
            if (light)
            {
                Mp3FileReader reader = (Mp3FileReader)current.GetField("mp3",B).GetValue(b);
                Check(reader.Id3v2Tag == null, "NAudio no carga/copia la etiqueta omitida");
            }
            foreach (double seek in new double[] { 0, .07, .15, 0 })
            {
                legacy.GetProperty("CurrentTime",B).SetValue(a,TimeSpan.FromSeconds(seek),null);
                current.GetProperty("CurrentTime",B).SetValue(b,TimeSpan.FromSeconds(seek),null);
                Check(Equal(Samples(legacy,a), Samples(current,b)), "PCM bit a bit, lectura fragmentada y seek " + seek);
            }
        }
        finally { ((IDisposable)a).Dispose(); if (b != null) ((IDisposable)b).Dispose(); }
        using (FileStream s = new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) Check(s.Length > 0, "archivo liberado al cerrar");
    }
    static void ViewContract()
    {
        byte[] b = new byte[4096]; for (int i=0;i<b.Length;i++) b[i]=(byte)i;
        MemoryStream m = new MemoryStream(b);
        Stream s = (Stream)Activator.CreateInstance(view,B,null,new object[] {m, 25L},null);
        Check(s.Length == b.Length-25 && s.Position == 0 && s.CanRead && s.CanSeek && !s.CanWrite,"vista relativa, solo lectura");
        byte[] outB = new byte[80]; Check(s.Read(outB,4,70)==70 && outB[4]==b[25] && outB[73]==b[94],"offset de lectura");
        Check(s.Seek(-10,SeekOrigin.Current)==60 && s.ReadByte()==b[85],"seek relativo y byte");
        Check(s.Seek(-3,SeekOrigin.End)==4068 && s.Read(outB,0,80)==3 && s.ReadByte()==-1,"final y lectura corta");
        Check(s.Seek(5000,SeekOrigin.Begin)==5000 && s.Read(outB,0,80)==0,"seek posterior al final");
        bool negative=false; try{s.Seek(-1,SeekOrigin.Begin);}catch(IOException){negative=true;} Check(negative,"no busca antes del audio");
        s.Dispose(); s.Dispose(); Check(!m.CanRead && !s.CanRead,"cierre idempotente del origen");
        bool closed=false;try{long ignored=s.Length;}catch(ObjectDisposedException){closed=true;}Check(closed,"cerrado no expone longitud");
    }
    [STAThread] static int Main(string[] args)
    {
        // Fuera de Windows no hay descompresor ACM: el PCM de MP3 reales no se puede comparar y esa parte se omite de forma explicita.
        // Pero la vista que salta el ID3 y la lectura de la etiqueta NO necesitan descompresor (solo bytes), y antes se omitian
        // tambien: la bateria salia sin ninguna comprobacion y el lanzador (que exige al menos una OK/PASS) la daba por fallida.
        bool hayAcm = Environment.OSVersion.Platform == PlatformID.Win32NT;
        string dir = Path.Combine(Path.GetTempPath(),"rocola-mp3-ligero-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        string old = Environment.GetEnvironmentVariable("ROCOLA_DATOS"); Environment.SetEnvironmentVariable("ROCOLA_DATOS",dir);
        try
        {
            Assembly app = Assembly.LoadFile(Path.GetFullPath(args[0]));
            current = app.GetType("Rocola.Form1+LectorMedida",true); view=current.GetNestedType("VistaMp3",B); formType=app.GetType("Rocola.Form1",true);
            bool baseline = args.Length > 1;
            if(baseline)
            {
                Check(!Equal(File.ReadAllBytes(args[0]),File.ReadAllBytes(args[1])),"referencia anterior distinta, no misma copia");
                legacy=Assembly.LoadFile(Path.GetFullPath(args[1])).GetType("Rocola.Form1+LectorMedida",true);
            }
            else { legacy=typeof(OriginalReader); Console.WriteLine("Sin Release anterior: contrato contra apertura de reproduccion intacta; NO A/B entre compilaciones."); }
            ViewContract();
            byte[] small = new byte[1024]; Header(small,0,3,0,400,false); Check(Start(small)==410,"ID3 valido");
            for(int flag=0;flag<256;flag++)
            {
                Header(small,0,3,flag,400,false); bool valid=(flag & ~0xA0)==0 && (flag&0x40)==0;
                Check(Start(small)==(valid?410:0),"flags ID3v3 conservadores");
            }
            Header(small,0,3,0,2000,false);Check(Start(small)==0,"tamano declarado truncado");
            Header(small,0,3,0,400,false);small[6]=128;Check(Start(small)==0,"syncsafe invalido");
            Header(small,0,5,0,400,false);Check(Start(small)==0,"version desconocida");
            Check(Start(new byte[5])==0,"cabecera truncada");
            Header(small,0,4,16,400,false); Header(small,410,4,16,400,true); Check(Start(small)==420,"footer valido");
            small[419]^=1;Check(Start(small)==0,"footer incongruente vuelve al camino anterior");
            if(!hayAcm)
            {
                Console.WriteLine("OMISION EXPLICITA: MP3 real de ACM necesita Windows; no confirma PCM en esta plataforma.");
                Console.WriteLine("PASS mp3medidaligera (parcial, sin ACM): "+checks+" controles de la vista de bytes y de la deteccion de ID3. NO compara PCM de MP3 reales; eso solo se comprueba en Windows.");
                return 0;
            }
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mp3-fixtures");
            if(args.Length>2) root=Path.GetDirectoryName(Path.GetFullPath(args[2]));
            else if(!Directory.Exists(root)) root=Directory.GetCurrentDirectory();
            foreach(string name in new string[] {"relleno_p0.mp3","relleno_mono.mp3","relleno_vbr.mp3","relleno_48k.mp3","relleno_mpeg2.mp3"})
            {
                byte[] audio=AudioBytes(Path.Combine(root,name)); string plain=Path.Combine(dir,name); File.WriteAllBytes(plain,audio); Compare(plain,false);
                foreach(int version in new int[]{2,3,4}) Compare(Tagged(dir,audio,version,0,2*1024*1024,false),true);
                Compare(Tagged(dir,audio,4,16,200000,true),true);
                Compare(Tagged(dir,audio,3,64,400,false),false);
            }
            string broken = Path.Combine(dir,"solo-id3.mp3"); byte[] emptyTag=new byte[410];Header(emptyTag,0,3,0,400,false);File.WriteAllBytes(broken,emptyTag);
            Exception oldFailure=null,newFailure=null;
            try{((IDisposable)NewReader(legacy,broken)).Dispose();}catch(Exception e){oldFailure=e;}
            try{((IDisposable)NewReader(current,broken)).Dispose();}catch(Exception e){newFailure=e;}
            Check(oldFailure!=null && newFailure!=null,"ID3 sin audio rechaza ambos caminos");
            using(FileStream released=new FileStream(broken,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) Check(released.Length==410,"constructor fallido libera el archivo");
            // El camino de reproduccion sigue incluyendo la etiqueta; no se optimiza su API publica aqui.
            string example = Tagged(dir,AudioBytes(Path.Combine(root,"relleno_p0.mp3")),3,0,200000,false);
            Type form=app.GetType("Rocola.Form1",true);
            using(Mp3FileReader playback=(Mp3FileReader)form.GetMethod("AbreMp3",B,null,new Type[]{typeof(string)},null).Invoke(null,new object[]{example}))
                Check(playback.Id3v2Tag!=null,"reproduccion/etiquetas intactas");
            Console.WriteLine("PASS mp3medidaligera: "+checks+" controles, PCM real de fixtures contra "+(baseline?"Release anterior":"apertura historica intacta")+". Sin HAL/FPS/audio de usuario. Evidencia: "+dir); return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{Environment.SetEnvironmentVariable("ROCOLA_DATOS",old);}
    }
}
