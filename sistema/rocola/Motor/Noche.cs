// LO MAS PEDIDO ESTA NOCHE (en XP, AtraccionGpu.cs, top_noche=; la cuenta, tal cual): se cuenta al entrar una cancion a la cola, desde
// las 8 de la manana, y se guarda en rocola_noche.txt cada minuto si cambio y al cerrar. Solo canciones, no videos. La atraccion la ensena
// cada 30 s durante 15 si se han pedido al menos tres distintas.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        public bool Escaparate = true;   // "escaparate=": la atraccion como escaparate (la portada se mece, la ficha del disco)
        public bool TopNocheOn = true;   // "top_noche="
        private void CuentaNoche(PlayItem it) { CuentaNocheGpu(it, DateTime.Now); }
        public void GuardaNoche(bool enFondo) { GuardaNocheGpu(enFondo); }

        public sealed class FilaNoche { public string Titulo, Artista, Portada; public int Veces; }
        // Las cinco mas pedidas esta noche (o ninguna si no llegan a tres distintas) y cuantas se pidieron en total.
        public List<FilaNoche> TopNoche(out int total)
        {
            total = 0;
            List<FilaNoche> r = new List<FilaNoche>();
            CambiaDeNoche(DateTime.Now);
            foreach (int v in nocheCuenta.Values) total += v;
            if (nocheCuenta.Count < TopMinimo) return r;
            foreach (string p in MasPedidasNoche(nocheCuenta, nocheOrden, TopFilas))
            {
                FilaNoche f = new FilaNoche { Veces = nocheCuenta[p], Titulo = System.IO.Path.GetFileNameWithoutExtension(p) };
                Track t; Album d;
                if (pistaDeRuta != null && pistaDeRuta.TryGetValue(p, out t) && t != null) f.Titulo = t.ToString();
                if (discoDeRuta != null && discoDeRuta.TryGetValue(p, out d) && d != null) { f.Artista = d.Artist; f.Portada = d.CoverPath; }
                r.Add(f);
            }
            return r;
        }

        internal const int NocheHora = 8;       // la noche va de las 8 de la manana a las 8 de la manana siguiente
        internal const int NocheTope = 400;     // canciones distintas como mucho: ninguna noche llega, y el archivo no crece sin fin
        public const int TopFilas = 5, TopMinimo = 3;
        private readonly Dictionary<string, int> nocheCuenta = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> nocheOrden = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private long nocheSeq; private string nocheClave; private bool nocheLeida, nocheSucia;
        private long nocheGuardaVer; private readonly object nocheGuardaLock = new object();

        // La noche a la que pertenece un momento: hasta las 8 de la manana, todavia es la de ayer.
        internal static string ClaveNoche(DateTime t) { return t.AddHours(-NocheHora).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        private string ArchivoNoche() { return RutaDatos("rocola_noche.txt"); }

        // Si ya es otra noche (dieron las 8 de la manana), se empieza de cero.
        private void CambiaDeNoche(DateTime ahora)
        {
            LeeNocheSiFalta(ahora);
            string k = ClaveNoche(ahora);
            if (k == nocheClave) return;
            nocheClave = k; nocheCuenta.Clear(); nocheOrden.Clear(); nocheSucia = true;
        }
        private void CuentaNocheGpu(PlayItem it, DateTime ahora)
        {
            if (it == null || it.IsVideo || it.Song == null || string.IsNullOrEmpty(it.Song.Path)) return;
            CambiaDeNoche(ahora);
            string p = it.Song.Path;
            int n; nocheCuenta.TryGetValue(p, out n);
            if (n == 0 && nocheCuenta.Count >= NocheTope) return;
            nocheCuenta[p] = n + 1; nocheOrden[p] = ++nocheSeq; nocheSucia = true;
        }
        // rocola_noche.txt: "noche=AAAA-MM-DD" y luego "veces|ruta", de la pedida hace mas tiempo a la ultima. Si es de
        // otra noche, o no se puede leer, se empieza de cero: es solo la cuenta de hoy. Se lee una vez, al hacer falta.
        private void LeeNocheSiFalta(DateTime ahora)
        {
            if (nocheLeida) return;
            nocheLeida = true;
            nocheClave = ClaveNoche(ahora);
            try
            {
                string[] ls = LeeLineasSeguro(ArchivoNoche());
                if (ls == null || ls.Length == 0 || ls[0].Trim() != "noche=" + nocheClave) return;
                for (int i = 1; i < ls.Length && nocheCuenta.Count < NocheTope; i++)
                {
                    string s = ls[i]; int bar = s.IndexOf('|');
                    if (bar <= 0 || bar >= s.Length - 1) continue;
                    int n;
                    if (!int.TryParse(s.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) continue;
                    string p = s.Substring(bar + 1);
                    nocheCuenta[p] = n; nocheOrden[p] = ++nocheSeq;
                }
            }
            catch { nocheCuenta.Clear(); nocheOrden.Clear(); }
        }
        private void GuardaNocheGpu(bool enFondo)
        {
            if (!nocheLeida || !nocheSucia) return;
            nocheSucia = false;
            string texto = TextoNoche(), ruta = ArchivoNoche();
            long ver = System.Threading.Interlocked.Increment(ref nocheGuardaVer);
            System.Action guarda = delegate
            {
                try
                {
                    lock (nocheGuardaLock)
                    {
                        if (ver != System.Threading.Interlocked.Read(ref nocheGuardaVer)) return;   // ya hay una mas nueva
                        EscribeSeguro(ruta, texto);
                    }
                }
                catch { }
            };
            if (enFondo) QueueDataWrite(delegate { guarda(); }); else guarda();   // detras: con la cuenta de pendientes (el cierre espera)
        }
        // Se arma en el hilo de la interfaz (los diccionarios son suyos) y se escribe donde toque.
        private string TextoNoche()
        {
            List<string> ks = new List<string>(nocheCuenta.Keys);
            ks.Sort(delegate (string a, string b)
            {
                long x, y; nocheOrden.TryGetValue(a, out x); nocheOrden.TryGetValue(b, out y);
                return x.CompareTo(y);
            });
            StringBuilder sb = new StringBuilder(32 + ks.Count * 96);
            sb.Append("noche=").Append(nocheClave).Append("\r\n");
            for (int i = 0; i < ks.Count; i++) sb.Append(nocheCuenta[ks[i]].ToString(CultureInfo.InvariantCulture)).Append('|').Append(ks[i]).Append("\r\n");
            return sb.ToString();
        }
        // Las mas pedidas: mas veces primero y, si empatan, la pedida mas tarde.
        internal static List<string> MasPedidasNoche(Dictionary<string, int> cuenta, Dictionary<string, long> orden, int max)
        {
            List<string> ks = new List<string>(cuenta.Keys);
            ks.Sort(delegate (string a, string b)
            {
                int ca = cuenta[a], cb = cuenta[b];
                if (ca != cb) return cb.CompareTo(ca);
                long oa, ob; orden.TryGetValue(a, out oa); orden.TryGetValue(b, out ob);
                return ob.CompareTo(oa);
            });
            if (ks.Count > max) ks.RemoveRange(max, ks.Count - max);
            return ks;
        }

    }
}
