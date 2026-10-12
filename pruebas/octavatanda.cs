using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

// OCTAVA TANDA (12-10-2026), lo que eligio el usuario de las propuestas:
//  (A1, saltar sin chasquido, NO se hizo: bajar el volumen de la salida es tocar el mezclador de Windows, y la bateria
//   'salida' exige que ningun sitio lo haga. Dentro de la cadena se oiria un segundo tarde.)
//  A4) LA TARJETA A UNA FRECUENCIA FIJA ("frecuencia_salida=" 44100/48000; 0 = como la cancion, de fabrica): cada
//      cancion se convierte en la rocola con el convertidor sinc del relevo, en vez del mezclador de Windows.
//  T1) Letra ajustada a los pixeles en las texturas de la tarjeta (mas nitida con letra chica).
//  T2) Los numeros de cada letra, mas grandes con los miles abreviados ("2.3k"); solo con tarjeta grafica.
// (A2 no hizo falta: el modo noche ya devuelve graves y agudos segun la norma ISO 226, ModoNoche.cs.)
class OctavaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        A4(asm);
        T1yT2(asm);
        Console.WriteLine(mal == 0 ? "  OK octavatanda: " + ok + " comprobaciones (formulario aislado; sin sonido de verdad)"
                                   : "  FALLO octavatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    static void A4(Assembly asm)
    {
        MethodInfo valida = form.GetMethod("FrecuenciaSalidaValida", B), convierte = form.GetMethod("AFrecuenciaDeLaTarjeta", B);
        FieldInfo campo = form.GetField("frecuenciaSalida", B);
        Check(valida != null && convierte != null && campo != null, "esta la frecuencia fija de la tarjeta");
        if (valida == null || convierte == null || campo == null) return;
        Check((int)valida.Invoke(null, new object[] { 48000 }) == 48000 && (int)valida.Invoke(null, new object[] { 44100 }) == 44100
              && (int)valida.Invoke(null, new object[] { 0 }) == 0 && (int)valida.Invoke(null, new object[] { 96000 }) == 0 && (int)valida.Invoke(null, new object[] { -1 }) == 0,
              "solo 44100 o 48000; lo demas, como la cancion");
        Check((int)campo.GetValue(null) == 0, "de fabrica: como la cancion (no se convierte nada)");
        Type tc = form.GetNestedType("CadenaCancion", B);
        Func<ISampleProvider, object> cadena = delegate (ISampleProvider p)
        {
            object c = Activator.CreateInstance(tc, true);
            tc.GetField("Salida", B).SetValue(c, p); tc.GetField("Retardo", B).SetValue(c, 0); tc.GetField("CebadoInicial", B).SetValue(c, 0);
            return c;
        };
        object f = Codigo.FormSinEncender(form);
        ISampleProvider s44 = new NAudio.Wave.SampleProviders.SignalGenerator(44100, 2);
        // De fabrica no cambia nada.
        object c0 = cadena(s44); convierte.Invoke(f, new object[] { c0 });
        Check(ReferenceEquals(tc.GetField("Salida", B).GetValue(c0), s44), "de fabrica la cancion sale tal cual");
        // A 48 kHz: la primera vez la tabla se prepara aparte; despues, convertida.
        campo.SetValue(null, 48000);
        try
        {
            Type rc = asm.GetType("Rocola.RemuestreoContinuo", true);
            for (int i = 0; i < 200; i++)
            {
                object t = rc.GetMethod("CapturaTabla", B).Invoke(null, new object[] { s44.WaveFormat, WaveFormat.CreateIeeeFloatWaveFormat(48000, 2) });
                if (t != null) break;
                Thread.Sleep(25);
            }
            object c1 = cadena(s44); convierte.Invoke(f, new object[] { c1 });
            ISampleProvider sal = (ISampleProvider)tc.GetField("Salida", B).GetValue(c1);
            Check(sal.WaveFormat.SampleRate == 48000 && sal.WaveFormat.Channels == 2, "con 48 kHz la cancion de 44,1 sale a 48 (" + sal.WaveFormat + ")");
            Check(sal.GetType().Name == "LimitadorPicoReal", "y detras del convertidor va su limitador de picos, como en el relevo");
            int ceb = (int)tc.GetField("CebadoInicial", B).GetValue(c1), ret = (int)tc.GetField("Retardo", B).GetValue(c1);
            Check(ret > 0 && Math.Abs(ceb - ret * 44100.0 / 48000) <= 1, "el reloj de la cancion descuenta el cebado en sus cuadros (" + ceb + " de " + ret + ")");
            float[] buf = new float[9600]; int n = sal.Read(buf, 0, buf.Length);
            bool finito = n > 0; for (int i = 0; i < n; i++) if (float.IsNaN(buf[i]) || Math.Abs(buf[i]) > 1.01f) finito = false;
            Check(finito, "y suena: muestras finitas y sin pasarse");
            ISampleProvider s48 = new NAudio.Wave.SampleProviders.SignalGenerator(48000, 2);
            object c2 = cadena(s48); convierte.Invoke(f, new object[] { c2 });
            Check(ReferenceEquals(tc.GetField("Salida", B).GetValue(c2), s48), "una de 48 kHz ya va a su frecuencia: no se toca");
            ISampleProvider s6 = new NAudio.Wave.SampleProviders.SignalGenerator(44100, 6);
            object c3 = cadena(s6); convierte.Invoke(f, new object[] { c3 });
            Check(ReferenceEquals(tc.GetField("Salida", B).GetValue(c3), s6), "seis canales sin disposicion conocida: como venga, sin inventar nada");
        }
        finally { campo.SetValue(null, 0); }
        string fuente = Codigo.FuenteDeForm1();
        Check(fuente.Contains("AFrecuenciaDeLaTarjeta(cad);"), "cada cancion pasa por la frecuencia de la tarjeta al arrancar");
        Check(fuente.Contains("if (frecuenciaSalida > 0 && s.WaveFormat.SampleRate != frecuenciaSalida) return;"), "si se cambio la frecuencia, la siguiente abre la salida ya a la nueva");
        Check(fuente.Contains("frecuencia_salida=") && fuente.Contains("FrecuenciaSalidaValida(v)"), "se guarda y se lee en la configuracion");
        Check(fuente.Contains("SonidoFrecuencia48") && fuente.Contains("if (frecuenciaSalida != 0) { frecuenciaSalida = 0; pintaFrec(); }"), "esta en Ctrl+F3 y \"Dejar como de fabrica\" la vuelve a como la cancion");
    }

    static bool Iguales(Bitmap a, Bitmap b)
    {
        for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
        return true;
    }

    static void T1yT2(Assembly asm)
    {
        Type lb = form.GetNestedType("LetterBar", B);
        MethodInfo corta = lb.GetMethod("TextoCuentaCorta", B);
        Check(corta != null, "estan los numeros cortos");
        if (corta != null)
        {
            Func<int, string> C = delegate (int n) { return (string)corta.Invoke(null, new object[] { n }); };
            Check(C(147) == "147" && C(999) == "999" && C(1000) == "1.0k" && C(2304) == "2.3k" && C(9949) == "9.9k" && C(9950) == "10k" && C(12480) == "12k" && C(5000000) == "999k",
                  "147, 2.3k, 12k... (" + C(2304) + ", " + C(9950) + ")");
        }
        bool gpu = form.GetNestedType("AyudaView", B) != null;
        string fuente = Codigo.FuenteDeForm1();
        if (!gpu) { Check(lb.GetProperty("CuentaCorta", B) != null, "sin tarjeta la barra tiene el ajuste pero nadie lo enciende"); return; }
        Check(fuente.Contains("if (letterBar != null) letterBar.CuentaCorta = gpuEnabled;"), "con tarjeta grafica se encienden los numeros cortos");
        // Con numeros cortos la letra de los numeros es mas grande.
        float[] cuerpo = new float[2];
        for (int k = 0; k < 2; k++)
        {
            Control c = (Control)Activator.CreateInstance(lb, true);
            c.Font = new Font(FontFamily.GenericSansSerif, 8.25f); c.SetBounds(0, 0, 916, 40);
            Dictionary<char, int> cnt = new Dictionary<char, int>(); HashSet<char> av = new HashSet<char>();
            foreach (char ch in (string)lb.GetField("Letters", B).GetValue(c)) { cnt[ch] = 7101; av.Add(ch); }
            lb.GetField("Counts", B).SetValue(c, cnt); lb.GetField("Available", B).SetValue(c, av);
            if (k == 1) lb.GetProperty("CuentaCorta", B).SetValue(c, true, null);
            using (Bitmap b = new Bitmap(916, 40)) using (Graphics g = Graphics.FromImage(b))
                lb.GetMethod("RenderContent", B, null, new Type[] { typeof(Graphics), typeof(bool) }, null).Invoke(c, new object[] { g, true });
            cuerpo[k] = ((Font)lb.GetField("fNum", B).GetValue(c)).Size;
        }
        Check(cuerpo[1] > cuerpo[0] * 1.12f, "en 1024 los numeros salen mas grandes (" + cuerpo[0].ToString("0.0") + " -> " + cuerpo[1].ToString("0.0") + " pt)");
        // Si la tarjeta se enciende o se apaga con la barra ya pintada, los numeros cambian YA: la tira cacheada se rehace.
        {
            Control c = (Control)Activator.CreateInstance(lb, true);
            c.Font = new Font(FontFamily.GenericSansSerif, 8.25f); c.SetBounds(0, 0, 916, 40);
            Dictionary<char, int> cnt = new Dictionary<char, int>(); HashSet<char> av = new HashSet<char>();
            foreach (char ch in (string)lb.GetField("Letters", B).GetValue(c)) { cnt[ch] = 7101; av.Add(ch); }
            lb.GetField("Counts", B).SetValue(c, cnt); lb.GetField("Available", B).SetValue(c, av);
            MethodInfo pinta = lb.GetMethod("RenderContent", B, null, new Type[] { typeof(Graphics), typeof(bool) }, null);
            Bitmap[] fotos = new Bitmap[3];
            for (int k = 0; k < 3; k++)
            {
                if (k > 0) lb.GetProperty("CuentaCorta", B).SetValue(c, k == 1, null);
                fotos[k] = new Bitmap(916, 40);
                using (Graphics g = Graphics.FromImage(fotos[k])) { g.Clear(Color.Black); pinta.Invoke(c, new object[] { g, true }); }
            }
            Check(!Iguales(fotos[0], fotos[1]), "al encender los numeros cortos con la barra ya pintada, se ven al momento (7.1k, no 7101)");
            Check(Iguales(fotos[0], fotos[2]), "y al apagarlos vuelve exactamente a la de antes");
            foreach (Bitmap b in fotos) b.Dispose();
        }
        // T1: las texturas de la tarjeta, ajustadas a los pixeles; lo compartido con la CPU, como estaba.
        string video = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.Combine("..", "Rocola"), "Form1.Video.cs"));
        int gf = 0, idx = 0; while ((idx = video.IndexOf("TextRenderingHint.AntiAliasGridFit;", idx)) >= 0) { gf++; idx++; }
        Check(gf >= 3, "las texturas de la interfaz, la franja y las piezas van ajustadas a los pixeles (" + gf + ")");
        string cinta = System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.Combine("..", "Rocola"), "TextoMovilGpu.cs"));
        Check(cinta.Contains("AntiAliasGridFit") && !cinta.Contains("TextRenderingHint.AntiAlias;"), "y las cintas de texto que se desplazan, igual (sin saltos entre una y otra)");
    }
}
