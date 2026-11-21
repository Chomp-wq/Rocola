// NOCHE Y VECINOS (20-11-2026): que mas se puede bajar de noche para que la musica moleste menos a los vecinos. NO ES UNA BATERIA de
// OK/FALLO: es el banco de medida del documento AUDIO_NOCHE_VECINOS_MODEST_2026-11-20.md. Pasa canciones SINTETICAS (nunca canciones de
// verdad) por la CADENA DE VERDAD de la rocola de XP (OpenAudio + ArmaCadena, con el modo billar puesto, como la usa el dueno) en cada
// propuesta, mide su sonoridad con el medidor BS.1770 de la propia rocola (MedidorBs1770) y deja la salida en float de 32 bits para que
// nochevecinos.py la analice por tercios de octava (dentro, afuera tras un muro y lo que se pierde dentro). El audio no se guarda en git.
//
// Lo que no existe en el programa se imita con lo que si existe, sin tocar el programa:
//   - el grave de noche mas hondo (N1 con mas dB): SalaLocal.GravesNocheDb, que no esta acotado;
//   - un paso-alto de noche (60, 80, 100 Hz): el corte del subgrave del modo billar (subgraveHz, fase lineal, Butterworth de 4.o orden);
//   - una curva de noche DETRAS del bajo psicoacustico: el ecualizador del dueno (eqDuenoBillar / eqDuenoNormal);
//   - mas bajo psicoacustico: el Amount del DeepBassProvider de la cadena ya armada;
//   - el techo del limitador mas bajo X dB: el limitador no depende de la escala, asi que se sube el Master X dB y la salida se baja X dB
//     (todo lo que hay entre el nivelado y el limitador en el modo billar es lineal u homogeneo: el grave en mono, el subgrave, el estante,
//     el bajo psicoacustico, el bombo y el ecualizador);
//   - el bombo «sin hundir» a ese techo: c.Hundir.Ajusta(true) en la cadena ya armada.
//
//   mcs -sdk:4 -r:NAudio.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:nochevecinos.exe nochevecinos.cs leercodigo.cs
//   mono nochevecinos.exe rocola.exe CARPETA_SALIDA [cancion ...]        (con NAudio.dll y NLayer.dll junto a los .exe)
// Escribe CARPETA_SALIDA/<cancion>__<propuesta>.f32 (estereo intercalado, 44,1 kHz) y una linea por pasada en CARPETA_SALIDA/medidas.tsv.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NAudio.Wave;

class NocheVecinos
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const int Fs = 44100;
    const double Seg = 24.0;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static object Get(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) return f.GetValue(o); }
        PropertyInfo p = o.GetType().GetProperty(n, T); return p != null ? p.GetValue(o, null) : null;
    }
    static void Set(object o, string n, object v)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, T); if (f != null) { f.SetValue(o, v); return; } }
        PropertyInfo p = o.GetType().GetProperty(n, T); if (p == null) throw new MissingFieldException(n); p.SetValue(o, v, null);
    }

    // ======================================= LA MUSICA SINTETICA =======================================
    // Cinco estilos de lo que suena en un billar (cumbia, banda, reggaeton, rock, balada) y ruido rosa. Cada voz es una suma de
    // armonicos con su envolvente; luego un «masterizado» sencillo (saturacion suave y pico a 0,97) como el de un MP3 comprado.
    sealed class Mezcla
    {
        public readonly int N; public readonly double[] L, R; readonly Random rnd;
        public Mezcla(double seg, int semilla) { N = (int)(Fs * seg); L = new double[N]; R = new double[N]; rnd = new Random(semilla); }
        public double Ruido() { return rnd.NextDouble() * 2 - 1; }
        // Una nota: armonicos 'amps' de f0, envolvente con ataque 'ata' s y caida exp 'tau' s, durante 'dur' s; paneo -1..1.
        public void Nota(double t0, double f0, double[] amps, double amp, double ata, double tau, double dur, double pan, double vibrato)
        {
            int i0 = (int)(t0 * Fs), n = (int)((dur + 5 * Math.Min(tau, 1.0)) * Fs);
            double gl = Math.Sqrt(0.5 * (1 - pan)), gr = Math.Sqrt(0.5 * (1 + pan));
            double[] fase = new double[amps.Length];
            for (int k = 0; k < amps.Length; k++) fase[k] = rnd.NextDouble() * 2 * Math.PI;
            for (int j = 0; j < n; j++)
            {
                int i = i0 + j; if (i < 0) continue; if (i >= N) break;
                double t = (double)j / Fs;
                double env = (t < ata ? t / ata : 1.0) * Math.Exp(-Math.Max(0, t - ata) / tau);
                if (t > dur) env *= Math.Exp(-(t - dur) / 0.04);   // se suelta
                if (env < 1e-5) { if (t > ata) break; else continue; }
                double f = f0 * (1 + vibrato * Math.Sin(2 * Math.PI * 5.5 * t));
                double s = 0;
                for (int k = 0; k < amps.Length; k++)
                {
                    double fk = f * (k + 1); if (fk > 16000) break;
                    s += amps[k] * Math.Sin(2 * Math.PI * fk * t + fase[k]);
                }
                s *= amp * env; L[i] += gl * s; R[i] += gr * s;
            }
        }
        // Un golpe de bombo: seno que cae de f1 a f0 en 'caidaF' s, con su chasquido.
        public void Bombo(double t0, double f0, double f1, double amp, double tau, double click)
        {
            int i0 = (int)(t0 * Fs), n = (int)(6 * tau * Fs); double fase = 0;
            for (int j = 0; j < n; j++)
            {
                int i = i0 + j; if (i >= N) break;
                double t = (double)j / Fs, f = f0 + (f1 - f0) * Math.Exp(-t / 0.025);
                fase += 2 * Math.PI * f / Fs;
                double s = amp * Math.Exp(-t / tau) * Math.Sin(fase) + click * Math.Exp(-t / 0.002) * Ruido();
                L[i] += s; R[i] += s;
            }
        }
        // Ruido filtrado (tarola, guiro, platillo): 'brillo' 0 = grave, 1 = solo agudo; con un tono de cuerpo opcional.
        public void Golpe(double t0, double amp, double tau, double brillo, double pan, double cuerpoHz, double cuerpo)
        {
            int i0 = (int)(t0 * Fs), n = (int)(6 * tau * Fs);
            double gl = Math.Sqrt(0.5 * (1 - pan)), gr = Math.Sqrt(0.5 * (1 + pan)), a1 = 0, a2 = 0, lp = 0;
            for (int j = 0; j < n; j++)
            {
                int i = i0 + j; if (i >= N) break;
                double t = (double)j / Fs, w = Ruido();
                double hp1 = w - a1; a1 = w; double hp2 = hp1 - a2; a2 = hp1;   // dos diferencias: sube 12 dB/oct
                lp += (w - lp) * 0.2;
                double s = brillo * hp2 * 0.5 + (1 - brillo) * lp;
                s = amp * Math.Exp(-t / tau) * s + cuerpo * Math.Exp(-t / (tau * 0.8)) * Math.Sin(2 * Math.PI * cuerpoHz * t);
                L[i] += gl * s; R[i] += gr * s;
            }
        }
        public void Voz(double t0, double f0, double dur, double amp, double pan)
        {   // armonicos con los formantes de una «a» (800, 1200, 2600 Hz) y vibrato
            double[] a = new double[40];
            for (int k = 0; k < a.Length; k++)
            {
                double fk = f0 * (k + 1);
                double fo = Math.Exp(-Math.Pow(Math.Log(fk / 800) / 0.25, 2)) + 0.6 * Math.Exp(-Math.Pow(Math.Log(fk / 1200) / 0.2, 2)) + 0.35 * Math.Exp(-Math.Pow(Math.Log(fk / 2600) / 0.18, 2));
                a[k] = (0.15 + fo) / Math.Sqrt(k + 1);
            }
            Nota(t0, f0, a, amp, 0.04, 3.0, dur, pan, 0.006);
        }
        public float[] Master(double drive)
        {
            double pico = 1e-9; for (int i = 0; i < N; i++) pico = Math.Max(pico, Math.Max(Math.Abs(L[i]), Math.Abs(R[i])));
            float[] x = new float[2 * N]; double tn = drive > 0 ? Math.Tanh(drive) : 1, p2 = 1e-9;
            for (int i = 0; i < N; i++)
            {
                double l = L[i] / pico, r = R[i] / pico;
                if (drive > 0) { l = Math.Tanh(drive * l) / tn; r = Math.Tanh(drive * r) / tn; }
                x[2 * i] = (float)l; x[2 * i + 1] = (float)r; p2 = Math.Max(p2, Math.Max(Math.Abs(l), Math.Abs(r)));
            }
            float g = (float)(0.97 / p2); for (int i = 0; i < x.Length; i++) x[i] *= g;
            // 20 ms de entrada y de salida
            int f = Fs / 50; for (int i = 0; i < f; i++) { float e = (float)i / f; x[2 * i] *= e; x[2 * i + 1] *= e; x[x.Length - 2 - 2 * i] *= e; x[x.Length - 1 - 2 * i] *= e; }
            return x;
        }
    }

    static double[] Arm(params double[] a) { return a; }
    static readonly double[] BajoElectrico = Arm(1, 0.55, 0.35, 0.22, 0.14, 0.09, 0.06, 0.04);
    static readonly double[] Tuba = Arm(1, 0.85, 0.55, 0.38, 0.25, 0.16, 0.1, 0.06, 0.04);
    static readonly double[] Acordeon = Arm(1, 0.7, 0.55, 0.45, 0.35, 0.28, 0.22, 0.17, 0.13, 0.1, 0.08, 0.06);
    static readonly double[] Metal = Arm(1, 0.8, 0.7, 0.6, 0.5, 0.42, 0.35, 0.3, 0.25, 0.2, 0.16, 0.13, 0.1, 0.08);
    static readonly double[] Piano = Arm(1, 0.5, 0.3, 0.2, 0.12, 0.08, 0.05, 0.03);
    static readonly double[] Cuerdas = Arm(1, 0.6, 0.45, 0.35, 0.28, 0.2, 0.15, 0.1, 0.07, 0.05);
    static double Nt(int midi) { return 440.0 * Math.Pow(2, (midi - 69) / 12.0); }

    static float[] Cancion(string cual)
    {
        int semilla = 0; foreach (char ch in cual) semilla = semilla * 31 + ch;
        Mezcla m = new Mezcla(Seg, semilla & 0xFFFF);
        switch (cual)
        {
            case "cumbia":
            {   // 95 BPM: bajo en el 1 y en el «y» del 2, bombo suave, guacharaca en corcheas, congas, acordeon y voz
                double b = 60.0 / 95; int[] raiz = { 45, 52, 50, 52 };   // A1, E2, D2, E2
                for (int c = 0; c * 4 * b < Seg; c++)
                {
                    double t0 = c * 4 * b; int r = raiz[c % 4];
                    m.Nota(t0, Nt(r), BajoElectrico, 0.32, 0.005, 0.30, 0.9 * b, 0, 0);
                    m.Nota(t0 + 1.5 * b, Nt(r + 7), BajoElectrico, 0.28, 0.005, 0.30, 0.9 * b, 0, 0);
                    m.Nota(t0 + 2.5 * b, Nt(r), BajoElectrico, 0.26, 0.005, 0.30, 0.9 * b, 0, 0);
                    for (int k = 0; k < 4; k += 2) m.Bombo(t0 + k * b, 50, 120, 0.25, 0.10, 0.03);
                    for (int k = 0; k < 8; k++) m.Golpe(t0 + k * b / 2, (k % 2 == 0) ? 0.06 : 0.09, 0.05, 0.95, 0.4, 0, 0);
                    for (int k = 0; k < 4; k++) m.Golpe(t0 + (k + 0.5) * b, 0.02, 0.05, 0.3, -0.4, k % 2 == 0 ? 220 : 330, 0.12);
                    int[] acorde = (c % 2 == 0) ? new int[] { 57, 60, 64 } : new int[] { 56, 59, 64 };
                    for (int k = 0; k < 4; k++) foreach (int n in acorde) m.Nota(t0 + (k + 0.5) * b, Nt(n), Acordeon, 0.035, 0.01, 0.4, 0.4 * b, -0.3, 0.003);
                    if (c % 2 == 0) { m.Voz(t0, Nt(64), 1.5 * b, 0.10, 0.05); m.Voz(t0 + 2 * b, Nt(62), 1.8 * b, 0.10, 0.05); }
                }
                return m.Master(1.3);
            }
            case "banda":
            {   // 2/4 a 100 BPM: tuba en cada tiempo (fundamental fuerte, 44-90 Hz), bombo, tarola, metales y platillos
                double b = 60.0 / 100; int[] raiz = { 46, 41, 46, 41, 43, 48 };   // Bb1, F1, Bb1, F1, G1, C2
                for (int c = 0; c * 2 * b < Seg; c++)
                {
                    double t0 = c * 2 * b; int r = raiz[c % raiz.Length];
                    m.Nota(t0, Nt(r), Tuba, 0.42, 0.02, 0.45, 0.85 * b, 0, 0.002);
                    m.Nota(t0 + b, Nt(r + 7), Tuba, 0.36, 0.02, 0.45, 0.85 * b, 0, 0.002);
                    m.Bombo(t0, 55, 110, 0.30, 0.12, 0.04);
                    m.Golpe(t0 + 0.5 * b, 0.12, 0.07, 0.6, 0.2, 190, 0.06); m.Golpe(t0 + 1.5 * b, 0.12, 0.07, 0.6, 0.2, 190, 0.06);
                    if (c % 2 == 0) m.Golpe(t0, 0.05, 0.35, 1.0, -0.5, 0, 0);
                    int[] mel = { 70, 72, 74, 72, 70, 69 };
                    m.Nota(t0, Nt(mel[c % 6]), Metal, 0.05, 0.02, 1.5, 0.9 * b, -0.4, 0.004);
                    m.Nota(t0 + b, Nt(mel[(c + 1) % 6]), Metal, 0.05, 0.02, 1.5, 0.9 * b, 0.4, 0.004);
                    m.Nota(t0, Nt(mel[c % 6] - 12), Metal, 0.04, 0.02, 1.5, 1.8 * b, 0.1, 0.004);
                }
                return m.Master(1.4);
            }
            case "reggaeton":
            {   // dembow a 95 BPM: bombo en cada tiempo, 808 sostenido en 49 Hz con su saturacion, tarola del dembow, hi-hat, pad y voz
                double b = 60.0 / 95; int[] raiz = { 31, 31, 34, 29 };   // G1, G1, Bb1, F1
                for (int c = 0; c * 4 * b < Seg; c++)
                {
                    double t0 = c * 4 * b; int r = raiz[c % 4];
                    for (int k = 0; k < 4; k++) m.Bombo(t0 + k * b, 58, 140, 0.32, 0.09, 0.05);
                    m.Nota(t0, Nt(r), Arm(1, 0.25, 0.12, 0.05), 0.45, 0.004, 0.9, 1.6 * b, 0, 0);
                    m.Nota(t0 + 2.5 * b, Nt(r), Arm(1, 0.25, 0.12, 0.05), 0.40, 0.004, 0.9, 1.4 * b, 0, 0);
                    double[] dembow = { 0.75, 1.5, 2.75, 3.5 };
                    foreach (double d in dembow) m.Golpe(t0 + d * b, 0.14, 0.06, 0.55, 0, 200, 0.05);
                    for (int k = 0; k < 16; k++) m.Golpe(t0 + k * b / 4, 0.025, 0.02, 1.0, 0.3, 0, 0);
                    foreach (int n in new int[] { 55, 58, 62 }) m.Nota(t0, Nt(n), Cuerdas, 0.025, 0.3, 3.0, 3.8 * b, -0.2, 0.002);
                    if (c % 2 == 1) { m.Voz(t0, Nt(62), 1.0 * b, 0.10, 0); m.Voz(t0 + 1.25 * b, Nt(60), 1.0 * b, 0.10, 0); m.Voz(t0 + 2.5 * b, Nt(58), 1.2 * b, 0.10, 0); }
                }
                float[] x = Satura(m, 2.2);
                return x;
            }
            case "rock":
            {   // 120 BPM: bombo en 1 y 3, bajo en corcheas (E1-A1-D2), guitarras saturadas a los lados, tarola, platillos y voz
                double b = 0.5; int[] raiz = { 28, 28, 33, 26 };
                for (int c = 0; c * 4 * b < Seg; c++)
                {
                    double t0 = c * 4 * b; int r = raiz[c % 4];
                    m.Bombo(t0, 60, 150, 0.35, 0.08, 0.08); m.Bombo(t0 + 2 * b, 60, 150, 0.35, 0.08, 0.08); m.Bombo(t0 + 2.5 * b, 60, 150, 0.25, 0.08, 0.06);
                    for (int k = 0; k < 8; k++) m.Nota(t0 + k * b / 2, Nt(r), BajoElectrico, 0.22, 0.004, 0.25, 0.45 * b, 0, 0);
                    m.Golpe(t0 + b, 0.18, 0.09, 0.5, 0, 210, 0.08); m.Golpe(t0 + 3 * b, 0.18, 0.09, 0.5, 0, 210, 0.08);
                    for (int k = 0; k < 8; k++) m.Golpe(t0 + k * b / 2, 0.03, 0.12, 1.0, 0.5, 0, 0);
                    double[] gt = new double[30]; for (int k = 0; k < gt.Length; k++) gt[k] = 1.0 / Math.Pow(k + 1, 0.8);
                    m.Nota(t0, Nt(r + 12), gt, 0.035, 0.005, 2.0, 4 * b, -0.8, 0.001);
                    m.Nota(t0, Nt(r + 19), gt, 0.03, 0.005, 2.0, 4 * b, 0.8, 0.001);
                    m.Nota(t0, Nt(r + 24), gt, 0.025, 0.005, 2.0, 4 * b, -0.6, 0.001);
                    if (c % 2 == 0) { m.Voz(t0, Nt(59), 1.8 * b, 0.10, 0); m.Voz(t0 + 2 * b, Nt(57), 1.8 * b, 0.10, 0); }
                }
                return Satura(m, 2.0);
            }
            case "balada":
            {   // 70 BPM: piano, cuerdas, bajo suave y voz; sin bateria
                double b = 60.0 / 70; int[][] acordes = { new int[] { 48, 55, 60, 64 }, new int[] { 45, 52, 57, 60 }, new int[] { 41, 53, 57, 60 }, new int[] { 43, 55, 59, 62 } };
                int[] bajos = { 36, 33, 29, 31 };
                for (int c = 0; c * 4 * b < Seg; c++)
                {
                    double t0 = c * 4 * b; int[] a = acordes[c % 4];
                    for (int k = 0; k < 4; k++) foreach (int n in a) m.Nota(t0 + k * b, Nt(n), Piano, 0.05, 0.003, 1.2, b, -0.2, 0);
                    foreach (int n in a) m.Nota(t0, Nt(n + 12), Cuerdas, 0.02, 0.4, 3.0, 3.9 * b, 0.3, 0.004);
                    m.Nota(t0, Nt(bajos[c % 4]), BajoElectrico, 0.20, 0.01, 0.8, 1.9 * b, 0, 0);
                    m.Nota(t0 + 2 * b, Nt(bajos[c % 4] + 7), BajoElectrico, 0.18, 0.01, 0.8, 1.9 * b, 0, 0);
                    m.Voz(t0 + 0.2, Nt(67 - (c % 3)), 3.2 * b, 0.13, 0);
                }
                return m.Master(0.6);
            }
            case "rosa":
            {   // ruido rosa (Paul Kellet), los dos canales a medias juntos y separados, a -18 dBFS
                float[] x = new float[2 * m.N]; double[] bb = new double[14];
                Random r = new Random(7);
                for (int i = 0; i < m.N; i++)
                    for (int c = 0; c < 2; c++)
                    {
                        double w = r.NextDouble() * 2 - 1; int o = 7 * c;
                        bb[o] = 0.99886 * bb[o] + w * 0.0555179; bb[o + 1] = 0.99332 * bb[o + 1] + w * 0.0750759; bb[o + 2] = 0.96900 * bb[o + 2] + w * 0.1538520;
                        bb[o + 3] = 0.86650 * bb[o + 3] + w * 0.3104856; bb[o + 4] = 0.55000 * bb[o + 4] + w * 0.5329522; bb[o + 5] = -0.7616 * bb[o + 5] - w * 0.0168980;
                        double p = bb[o] + bb[o + 1] + bb[o + 2] + bb[o + 3] + bb[o + 4] + bb[o + 5] + bb[o + 6] + w * 0.5362; bb[o + 6] = w * 0.115926;
                        x[2 * i + c] = (float)p;
                    }
                double s2 = 0; for (int i = 0; i < x.Length; i++) s2 += (double)x[i] * x[i];
                float g = (float)(Math.Pow(10, -18.0 / 20) / Math.Sqrt(s2 / x.Length));
                for (int i = 0; i < x.Length; i++) x[i] *= g;
                return x;
            }
        }
        throw new ArgumentException(cual);
    }
    static float[] Satura(Mezcla m, double drive) { return m.Master(drive); }

    // ======================================= LAS PROPUESTAS =======================================
    sealed class Prop
    {
        public string Nombre; public double NocheDb; public double GravesNoche = 0; public float SubHz = 0; public float MonoHz = 120;
        public double BajoPor = 1; public double[] Eq;   // dB por banda (31 Hz estante, 63..8k campanas, 16k estante)
        public double TechoDb = 0; public bool Bombo;
        public double PaFinHz = 0;   // un paso-alto Butterworth de 4.o orden DETRAS de todo (detras del bajo psicoacustico): lo que no existe hoy
        public Prop(string n, double nocheDb) { Nombre = n; NocheDb = nocheDb; }
    }
    static double Pct(int p) { return 10.0 * Math.Log(p / 100.0, 2.0); }   // el % de lo que se oye, en dB (como NocheDb)

    static List<Prop> Propuestas()
    {
        List<Prop> l = new List<Prop>();
        double n80 = Pct(80), n70 = Pct(70), n50 = Pct(50);
        l.Add(new Prop("dia", 0));
        l.Add(new Prop("noche80_sinN1", n80));
        l.Add(new Prop("noche80", n80) { GravesNoche = 4 });          // lo de hoy de fabrica
        l.Add(new Prop("noche70", n70) { GravesNoche = 4 });
        l.Add(new Prop("noche60", Pct(60)) { GravesNoche = 4 });
        l.Add(new Prop("noche50", n50) { GravesNoche = 4 });          // el deslizador nuevo, en su minimo
        l.Add(new Prop("vol_menos3", n80 - 3) { GravesNoche = 4 });   // referencia: solo 3 dB mas de volumen
        l.Add(new Prop("vol_menos6", n80 - 6) { GravesNoche = 4 });
        // NV1: el estante de noche mas hondo
        l.Add(new Prop("N1_8", n80) { GravesNoche = 8 });
        l.Add(new Prop("N1_12", n80) { GravesNoche = 12 });
        // NV2: paso-alto de noche (el subgrave mas alto, fase lineal, 24 dB/oct)
        l.Add(new Prop("pa60", n80) { GravesNoche = 4, SubHz = 60 });
        l.Add(new Prop("pa80", n80) { GravesNoche = 4, SubHz = 80 });
        l.Add(new Prop("pa100", n80) { GravesNoche = 4, SubHz = 100 });
        l.Add(new Prop("pa80_bajo2", n80) { GravesNoche = 4, SubHz = 80, BajoPor = 2 });
        l.Add(new Prop("pa80_bajo3", n80) { GravesNoche = 4, SubHz = 80, BajoPor = 3 });
        // NV2b: el mismo paso-alto pero DETRAS del bajo psicoacustico (sus armonicos se quedan, su fuga de grave no), sin N1
        l.Add(new Prop("paFin80", n80) { GravesNoche = 4, PaFinHz = 80 });
        l.Add(new Prop("paFin80_bajo3", n80) { GravesNoche = 4, PaFinHz = 80, BajoPor = 3 });
        l.Add(new Prop("paFin100_bajo3", n80) { GravesNoche = 4, PaFinHz = 100, BajoPor = 3 });
        // NV1 + NV2: el estante hondo y el paso-alto juntos
        l.Add(new Prop("N1_8_pa80", n80) { GravesNoche = 8, SubHz = 80 });
        l.Add(new Prop("N1_12_pa80", n80) { GravesNoche = 12, SubHz = 80 });
        // lo que de verdad pone el bajo psicoacustico (sin el, y con el triple), para restar
        l.Add(new Prop("noche80_bajo0", n80) { GravesNoche = 4, BajoPor = 0 });
        l.Add(new Prop("noche80_bajo3", n80) { GravesNoche = 4, BajoPor = 3 });
        l.Add(new Prop("pa80_bajo0", n80) { GravesNoche = 4, SubHz = 80, BajoPor = 0 });
        // NV3: la curva de noche DETRAS del bajo psicoacustico (por el ecualizador del dueno): -10 en 31 (estante), -6 en 63, -2 en 125
        double[] curva = { -10, -6, -2, 0, 0, 0, 0, 0, 0, 0 };
        l.Add(new Prop("eqn", n80) { Eq = curva });
        l.Add(new Prop("eqn_bajo2", n80) { Eq = curva, BajoPor = 2 });
        double[] curvaH = { -12, -9, -3, 0, 0, 0, 0, 0, 0, 0 };
        l.Add(new Prop("eqnH_bajo2", n80) { Eq = curvaH, BajoPor = 2 });
        // NV4: el grave en mono hasta mas arriba
        l.Add(new Prop("mono250", n80) { GravesNoche = 4, MonoHz = 250 });
        // NV5: el techo de noche mas bajo, sin y con el bombo «sin hundir»
        l.Add(new Prop("techo3", n80) { GravesNoche = 4, TechoDb = 3 });
        l.Add(new Prop("techo6", n80) { GravesNoche = 4, TechoDb = 6 });
        l.Add(new Prop("techo6_bombo", n80) { GravesNoche = 4, TechoDb = 6, Bombo = true });
        // NV6: menos 2-5 kHz
        l.Add(new Prop("medios_menos3", n80) { GravesNoche = 4, Eq = new double[] { 0, 0, 0, 0, 0, 0, -3, -3, 0, 0 } });
        // NV7: la madrugada mas honda (despues de la 1): 70 % y la curva fuerte detras del bajo
        l.Add(new Prop("madrugada", n70) { Eq = curvaH, BajoPor = 2 });
        l.Add(new Prop("madrugada_pa", n70) { GravesNoche = 4, PaFinHz = 100, BajoPor = 3 });
        l.Add(new Prop("madrugada_N1", n70) { GravesNoche = 12, SubHz = 80 });
        l.Add(new Prop("madrugada60_N1", Pct(60)) { GravesNoche = 12, SubHz = 80 });
        l.Add(new Prop("noche70_N1_8_pa80", n70) { GravesNoche = 8, SubHz = 80 });
        string solo = Environment.GetEnvironmentVariable("NV_PROPS");
        if (!string.IsNullOrEmpty(solo)) { List<string> q = new List<string>(solo.Split(',')); l = l.FindAll(delegate(Prop x) { return q.Contains(x.Nombre); }); }
        return l;
    }

    // Paso-alto Butterworth de 4.o orden (dos bicuadradas, Q 0,5412 y 1,3066; RBJ), por canal, en coma doble.
    sealed class PasoAlto4
    {
        readonly double[,] c = new double[2, 5]; readonly double[] z = new double[2 * 2 * 4];
        public PasoAlto4(double hz)
        {
            double[] qs = { 0.54119610, 1.3065630 };
            for (int e = 0; e < 2; e++)
            {
                double w0 = 2 * Math.PI * hz / Fs, al = Math.Sin(w0) / (2 * qs[e]), co = Math.Cos(w0), a0 = 1 + al;
                c[e, 0] = (1 + co) / 2 / a0; c[e, 1] = -(1 + co) / a0; c[e, 2] = (1 + co) / 2 / a0; c[e, 3] = -2 * co / a0; c[e, 4] = (1 - al) / a0;
            }
        }
        public void Pasa(float[] b, int n)
        {
            for (int i = 0; i < n; i++)
            {
                int ch = i & 1; double x = b[i];
                for (int e = 0; e < 2; e++)
                {
                    int o = (ch * 2 + e) * 4;
                    double y = c[e, 0] * x + c[e, 1] * z[o] + c[e, 2] * z[o + 1] - c[e, 3] * z[o + 2] - c[e, 4] * z[o + 3];
                    z[o + 1] = z[o]; z[o] = x; z[o + 3] = z[o + 2]; z[o + 2] = y; x = y;
                }
                b[i] = (float)x;
            }
        }
    }

    // ======================================= LA CADENA =======================================
    static Type TF; static object F;
    static void SetS(string n, object v) { FieldInfo f = TF.GetField(n, T); if (f == null) throw new MissingFieldException(n); f.SetValue(f.IsStatic ? null : F, v); }
    static object Call(string n, params object[] a)
    {
        foreach (MethodInfo m in TF.GetMethods(T)) if (m.Name == n && m.GetParameters().Length == a.Length)
        { try { return m.Invoke(F, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
        throw new MissingMethodException(n);
    }

    static void ModoBillar()
    {   // como lo usa el dueno: el modo billar encendido, todo lo demas de fabrica (y el EQ automatico sin huella: no se ecualiza)
        Set(F, "normalizeOn", true); Set(F, "normTarget", 0.26f); Set(F, "normStrength", 1f); Set(F, "masterDb", 0f);
        Set(F, "excAmount", 0.35f); Set(F, "bassAuto", true); Set(F, "bassAmount", 0.3f); Set(F, "bassSongFactor", 1f);
        Set(F, "eqAuto", true); Set(F, "eqAmount", 0f);
        Set(F, "sbrAllowed", true); Set(F, "sbrOn", false); Set(F, "declipOn", true);
        Set(F, "crossfadeSec", 0f); Set(F, "saltaSilencios", false);
        SetS("claridadLocal", false); SetS("partesSuaves", false); SetS("localAuto", true); SetS("presenciaSala", false);
        SetS("gravesLocalDb", 0); SetS("agudosLocalDb", 0); SetS("balanceDb", 0);
        SetS("cortaSubgrave", false); SetS("estereoEstrecho", false); SetS("estereoAuto", false);
        SetS("canalMudo", true); SetS("quitaContinua", true);
        Set(F, "sinMejoras", false); Set(F, "aLaMedida", false); Set(F, "quitaSiseo", false);
        SetS("subgraveHz", 30f); SetS("graveMonoHz", 120f);
        int[] eqN = (int[])TF.GetField("eqDuenoNormal", T).GetValue(null), eqB = (int[])TF.GetField("eqDuenoBillar", T).GetValue(null);
        Array.Clear(eqN, 0, eqN.Length); Array.Clear(eqB, 0, eqB.Length);
    }

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("Uso: nochevecinos.exe rocola.exe CARPETA_SALIDA [cancion ...]"); return 2; }
        string salida = args[1]; Directory.CreateDirectory(salida);
        string dir = Path.Combine(Path.GetTempPath(), "rocola_nochevecinos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Assembly asm = Assembly.Load(File.ReadAllBytes(args[0]));
            TF = asm.GetType("Rocola.Form1", true);
            F = Codigo.FormSinEncender(TF);
            foreach (FieldInfo fi in TF.GetFields(T))
                if (!fi.IsStatic && fi.FieldType.Name == "GuardadoReciente" && fi.GetValue(F) == null) fi.SetValue(F, Activator.CreateInstance(fi.FieldType, true));
            Type tSala = TF.GetNestedType("SalaLocal", T); FieldInfo fGrN = tSala.GetField("GravesNocheDb", T);
            Type tMed = asm.GetType("Rocola.MedidorBs1770", true);
            ConstructorInfo cMed = tMed.GetConstructor(T, null, new Type[] { typeof(int), typeof(double[]) }, null);
            MethodInfo mProc = tMed.GetMethod("Procesa", T), mInt = tMed.GetMethod("Integrada", T);
            Type tt = TF.GetNestedType("Track", T);
            List<string> canciones = new List<string>();
            for (int i = 2; i < args.Length; i++) canciones.Add(args[i]);
            if (canciones.Count == 0) canciones.AddRange(new string[] { "cumbia", "banda", "reggaeton", "rock", "balada", "rosa" });
            string tsv = Path.Combine(salida, "medidas.tsv");
            bool nuevo = !File.Exists(tsv);
            using (StreamWriter w = new StreamWriter(tsv, true))
            {
                if (nuevo) w.WriteLine("cancion\tpropuesta\tlufs\tpico_dbfs\tbajo_amount\tganancia_db\tsegundos");
                foreach (string can in canciones)
                {
                    float[] x = Cancion(can);
                    string wav = Path.Combine(dir, can + ".wav");
                    using (WaveFileWriter ww = new WaveFileWriter(wav, WaveFormat.CreateIeeeFloatWaveFormat(Fs, 2))) ww.WriteSamples(x, 0, x.Length);
                    object track = Activator.CreateInstance(tt, true); Set(track, "Path", wav);
                    // medida como la del repaso (para que el nivelado la suba hasta su limite)
                    ModoBillar();
                    object[] a = { wav, null };
                    if (!(bool)Call("MideIntegrada", a)) throw new Exception("no se pudo medir " + can);
                    IDictionary tab = (IDictionary)Get(F, "integCache");
                    if (tab == null) { tab = (IDictionary)Activator.CreateInstance(TF.GetField("integCache", T).FieldType, StringComparer.OrdinalIgnoreCase); Set(F, "integCache", tab); }
                    tab[wav] = a[1];
                    object fuente = TF.GetMethod("TomaFuenteMedida", T).Invoke(null, null);
                    Call("ActualizaFuenteMedida", wav, fuente);
                    foreach (Prop p in Propuestas())
                    {
                        ModoBillar();
                        Set(F, "nocheArrancada", true);
                        Set(F, "nocheFactor", (float)Math.Pow(10, (p.NocheDb + p.TechoDb) / 20.0));
                        fGrN.SetValue(null, (float)p.GravesNoche);
                        if (p.SubHz > 0) SetS("subgraveHz", p.SubHz);
                        SetS("graveMonoHz", p.MonoHz);
                        if (p.Eq != null)
                        {
                            int[] eqN = (int[])TF.GetField("eqDuenoNormal", T).GetValue(null), eqB = (int[])TF.GetField("eqDuenoBillar", T).GetValue(null);
                            for (int k = 0; k < 10; k++) { eqN[k] = (int)Math.Round(2 * p.Eq[k]); eqB[k] = eqN[k]; }
                        }
                        object lector = Call("OpenAudio", wav);
                        TF.GetNestedType("IAudioSource", T).GetProperty("Volume").SetValue(lector, 1.0f, null);
                        object cad = Call("ArmaCadena", track, lector, false, 0.0);
                        object bajo = Get(cad, "Bajo");
                        float amount = (float)Get(bajo, "Amount");
                        if (p.BajoPor != 1) { amount = (float)(amount * p.BajoPor); Set(bajo, "Amount", amount); }
                        if (p.Bombo) { object h = Get(cad, "Hundir"); h.GetType().GetMethod("Ajusta", T).Invoke(h, new object[] { true }); }
                        float ganancia = (float)Get(Get(cad, "Tap"), "Gain");
                        ISampleProvider sal = (ISampleProvider)Get(cad, "Salida");
                        float post = (float)Math.Pow(10, -p.TechoDb / 20.0);
                        PasoAlto4 paFin = p.PaFinHz > 0 ? new PasoAlto4(p.PaFinHz) : null;
                        object med = cMed.Invoke(new object[] { Fs, new double[] { 1, 1 } });
                        string f32 = Path.Combine(salida, can + "__" + p.Nombre + ".f32");
                        float[] buf = new float[4410 * 2]; byte[] bytes = new byte[buf.Length * 4]; double pico = 0; long n = 0;
                        using (FileStream fs = File.Create(f32))
                            while (true)
                            {
                                int k = sal.Read(buf, 0, buf.Length); if (k <= 0) break;
                                if (paFin != null) paFin.Pasa(buf, k);
                                for (int i = 0; i < k; i++) { buf[i] *= post; double v = Math.Abs(buf[i]); if (v > pico) pico = v; }
                                mProc.Invoke(med, new object[] { buf, 0, k });
                                Buffer.BlockCopy(buf, 0, bytes, 0, k * 4); fs.Write(bytes, 0, k * 4); n += k;
                            }
                        ((IDisposable)lector).Dispose();
                        object[] ia = { 0.0, 0 }; mInt.Invoke(med, ia);
                        w.WriteLine(string.Join("\t", new string[] { can, p.Nombre, ((double)ia[0]).ToString("0.000", Inv), (20 * Math.Log10(pico + 1e-12)).ToString("0.00", Inv),
                            amount.ToString("0.000", Inv), (20 * Math.Log10(ganancia)).ToString("0.00", Inv), (n / 2.0 / Fs).ToString("0.00", Inv) }));
                        w.Flush();
                        Console.WriteLine("  " + can + " " + p.Nombre + ": " + ((double)ia[0]).ToString("0.00", Inv) + " LUFS, bajo " + amount.ToString("0.00", Inv));
                    }
                    File.Delete(wav);
                    fGrN.SetValue(null, 0f);
                }
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        return 0;
    }
}
