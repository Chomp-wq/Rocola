// LO QUE LA PANTALLA ENSENA EN EL ESTADO EXTENDIDO (en XP, EstadoReposoGpu.cs y SuenaVivoGpu.cs): el reloj de la que suena, su genero y
// la cola con sus portadas y en cuantos minutos suena cada una (MinutosEspera/TextoCola de XP: sin los silencios que se saltan; de una
// que no se sabe en adelante, ninguna se sabe). Y sus tres interruptores de XP, con sus claves y valores de fabrica (encendidos).
using System;
using System.Collections.Generic;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        public bool MuroReposo = true;        // "muro_reposo=": la pared se aparta a los 15 s sin botones mientras suena algo
        public bool EstadoExpandido = true;   // "estado_expandido=": y lo que suena crece (el estado extendido)
        public bool ColaDoce = true;          // "cola_doce=": en el estado extendido, hasta 12 de la cola (si no, 3)

        public string SonandoTiempo
        {
            get
            {
                CadenaCancion c = cadenaSonando;
                if (c == null || nowPlaying == null || nowPlaying.IsVideo) return "";
                try
                {
                    TimeSpan cur = c.Lector.CurrentTime, tot = c.Lector.TotalTime;
                    if (tot.TotalSeconds <= 0 || cur.TotalSeconds < 0) return "";
                    if (cur > tot) cur = tot;
                    return Reloj(cur) + " / " + Reloj(tot);
                }
                catch { return ""; }
            }
        }
        private static string Reloj(TimeSpan t) { return ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00"); }
        public string SonandoGenero { get { PlayItem p = nowPlaying; return p != null && p.Song != null ? (p.Song.Genre ?? "") : ""; } }

        public sealed class EnColaVista { public string Titulo, Disco, Portada, Ruta; public int Minutos; public bool Video; }
        public List<EnColaVista> ColaParaPantalla(int n)
        {
            List<EnColaVista> r = new List<EnColaVista>();
            double s = RestanDeLaQueSuena();
            for (int i = 0; i < queue.Count && r.Count < n; i++)
            {
                PlayItem q = queue[i];
                EnColaVista v = new EnColaVista { Video = q.IsVideo, Minutos = MinutosEspera(s), Ruta = q.PathKey() };
                v.Titulo = q.IsVideo ? Path.GetFileNameWithoutExtension(q.VideoPath) : q.Song.ToString();
                if (q.Disc != null) { v.Disco = string.IsNullOrEmpty(q.Disc.Name) ? q.Disc.Artist : q.Disc.Artist + " - " + q.Disc.Name; v.Portada = q.Disc.CoverPath; }
                r.Add(v);
                if (s >= 0) { double d = q.IsVideo ? 0 : SegundosQueSuenan(q.PathKey()); s = d > 0 ? s + d : -1; }
            }
            return r;
        }
        internal static int MinutosEspera(double s)
        {
            if (s < 0 || double.IsNaN(s)) return -1;
            if (s < 60) return 0;
            return (int)((s + 30) / 60);
        }
        public static string TextoCola(int puesto, int minutos)
        {
            string t = "EN COLA " + puesto;
            if (minutos < 0) return t;
            return t + ((minutos == 0) ? " · ya casi" : (" · en ~" + minutos + " min"));
        }
    }
}
