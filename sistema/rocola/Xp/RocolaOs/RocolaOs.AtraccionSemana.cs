// LO MAS PEDIDO DE LA SEMANA Y LOS AVISOS DEL MODO ATRACCION (Rocola OS; peticion del dueno del 23-11-2026: «en el modo atraccion, cambia los
// avisos, por ejemplo en vez de decir el numero de canciones que hay, que ahora diga, cualquier cancion que te imagines esta aqui, luego, las
// canciones mas pedidas, en vez de ser las mas pedidas de la noche, seran las mas pedidas de la semana»).
// Con Rocola OS: (1) cada cancion que entra a la cola suma 1 al dia de hoy (el mismo punto y las mismas reglas que la cuenta de la noche de XP:
// ni videos ni sin ruta) y se guardan los ultimos 7 dias (ventana movil; el dia cambia a las 8 de la manana como la noche) en
// rocola_semana.txt; la primera vez se siembra con la cuenta de hoy de rocola_noche.txt. (2) La lista de la atraccion (ArmaTopNoche) usa la
// suma de la semana. (3) Textos: «Lo mas pedido de la semana», «La favorita de la semana» y «¡Cualquier cancion que te imagines esta aqui!»
// en vez de «Hay N esperandote». Todo en el hilo de la interfaz, como lo de la noche. El atajo «Lo mas pedido hoy» del buscador sigue
// con la cuenta de hoy (BuscadorPortadasGpu.cs, nocheCuenta). Sin Rocola OS, nada de esto se activa: la atraccion es la de XP tal cual.
// Ganchos (Codigo/AtraccionGpu.cs): g271 CuentaNoche (la cuenta), g272 GuardaNoche (el guardado), g273-g275 ArmaTopNoche (la lista),
// g276 y g277 MensajesAtraccion (los avisos), g278 HorneaTop (la cabecera). La logica pura: RocolaOs.AtraccionSemanaLogica.cs.
#if ROCOLA_GPU
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        private readonly AtraccionSemanaDatos semana = new AtraccionSemanaDatos();
        private bool semanaLeida, semanaSucia;
        private long semanaGuardaVer; private readonly object semanaGuardaLock = new object();
        private Dictionary<string, int> semanaSuma; private int semanaSumaVer = -1; private string semanaSumaDia;

        private static string RocolaOsSemanaArchivo() { return RutaDatos("rocola_semana.txt"); }

        // leida una vez (o sembrada con lo de hoy si no hay archivo) y sin los dias que ya salieron de la ventana
        private void RocolaOsSemanaAsegura(DateTime ahora)
        {
            string hoy = ClaveNoche(ahora);
            if (!semanaLeida)
            {
                semanaLeida = true;
                bool leida = false;
                try { leida = semana.Carga(LeeLineasSeguro(RocolaOsSemanaArchivo()), hoy); } catch { semana.PorDia.Clear(); semana.Orden.Clear(); }
                if (!leida)
                {
                    try
                    {
                        CambiaDeNoche(ahora);   // lee rocola_noche.txt si falta, y empieza de cero si es de otra noche
                        List<string> ks = new List<string>(nocheCuenta.Keys);
                        ks.Sort(delegate (string a, string b) { long x, y; nocheOrden.TryGetValue(a, out x); nocheOrden.TryGetValue(b, out y); return x.CompareTo(y); });
                        List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>(ks.Count);
                        for (int i = 0; i < ks.Count; i++) l.Add(new KeyValuePair<string, int>(ks[i], nocheCuenta[ks[i]]));
                        semana.Siembra(hoy, l);
                    }
                    catch { }
                    semanaSucia = true;
                }
            }
            if (semana.Poda(hoy)) semanaSucia = true;
        }

        // g271: una cancion entro a la cola (antes que la cuenta de la noche: si hay que sembrar, que no cuente esta dos veces)
        private void RocolaOsSemanaCuenta(PlayItem it, DateTime ahora)
        {
            if (!RocolaOs.Activo) return;
            if (it == null || it.IsVideo || it.Song == null || string.IsNullOrEmpty(it.Song.Path)) return;
            try
            {
                RocolaOsSemanaAsegura(ahora);
                if (semana.Cuenta(it.Song.Path, ClaveNoche(ahora))) semanaSucia = true;
            }
            catch { }
        }

        // g272: la cuenta de la semana, al disco si cambio (como GuardaNocheGpu)
        private void RocolaOsSemanaGuarda(bool enFondo)
        {
            if (!RocolaOs.Activo || !semanaLeida || !semanaSucia) return;
            semanaSucia = false;
            string texto, ruta = RocolaOsSemanaArchivo();
            try { texto = semana.Texto(ClaveNoche(DateTime.Now)); } catch { semanaSucia = true; return; }
            long ver = System.Threading.Interlocked.Increment(ref semanaGuardaVer);
            System.Action guarda = delegate
            {
                try
                {
                    lock (semanaGuardaLock)
                    {
                        if (ver != System.Threading.Interlocked.Read(ref semanaGuardaVer)) return;   // ya hay una mas nueva
                        EscribeSeguro(ruta, texto);
                    }
                }
                catch { }
            };
            if (enFondo) GuardaDetras(guarda); else guarda();
        }

        // g273-g275: con Rocola OS la lista usa la suma de la semana (y su orden); sin el, la cuenta de la noche que se le da
        private Dictionary<string, int> RocolaOsCuentaTop(Dictionary<string, int> deLaNoche)
        {
            if (!RocolaOs.Activo) return deLaNoche;
            try
            {
                DateTime ahora = DateTime.Now; string hoy = ClaveNoche(ahora);
                RocolaOsSemanaAsegura(ahora);
                if (semanaSuma == null || semanaSumaVer != semana.Version || semanaSumaDia != hoy)
                { semanaSuma = semana.Suma(hoy); semanaSumaVer = semana.Version; semanaSumaDia = hoy; }
                return semanaSuma;
            }
            catch { return deLaNoche; }
        }
        private Dictionary<string, long> RocolaOsOrdenTop(Dictionary<string, long> deLaNoche) { return RocolaOs.Activo && semanaSuma != null ? semana.Orden : deLaNoche; }

        // g278, g276, g277: los textos
        internal static string RocolaOsTituloTop() { return RocolaOs.Activo ? "Lo más pedido de la semana" : null; }
        internal static string RocolaOsMsgElige() { return RocolaOs.Activo ? "¡Cualquier canción que te imagines está aquí!" : null; }
        internal static string RocolaOsFavoritaDe() { return RocolaOs.Activo ? "la semana" : "esta noche"; }
    }
}
#endif
