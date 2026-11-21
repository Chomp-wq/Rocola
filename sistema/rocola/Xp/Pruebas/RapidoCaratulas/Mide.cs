// LA MEDIDA DE LA PARED (rapido-caratulas, 20-11-2026; prueba, no es de XP ni va en Rocola OS): una funcion de Rocola OS de mentira,
// «medir-caratulas», solo con ROCOLA_OS=medir-caratulas (SoloEnLaLista). En su primer latido con la tarjeta puesta envuelve el GetGrid del
// panel (lo que la pared le da cada cuadro) y apunta en ROCOLA_MIDE_CARATULAS (un renglon por suceso, el reloj de la tarjeta en ms):
//   pagina  CLAVE  T        la pagina cambio (la clave del regulador de XP: letra|pagina|generacion); se pone a 0 WorstPageMs del regulador
//   portadas CLAVE T  N     cuantas de las casillas con portada ya la tienen en la cache de memoria (cuando cambia)
//   decodas CLAVE  T  MS    las 18 casillas ya tienen su portada en la cache de memoria (las que tienen portada)
//   lista   CLAVE  T  MS    el regulador de XP dio la pagina por lista (FinishPage: las 18 con su textura subida): MS = lo que tardo
//   costes ...              lo que el panel aprendio que cuesta subir una portada y una casilla, y la pared hornear una casilla (ms)
//   cuadros N p50 p95 p99 max   los intervalos entre cuadros (de GetGrid a GetGrid) desde el renglon anterior de cuadros (cada 5 s)
//   sonido N MIN P1 P50 HUECOS  (21-11-2026) el colchon del hilo de la musica (BombaSonido: lo que deja hecho por delante, de 200 ms) mirado
//                               cada 2 ms por un hilo propio mientras suena: las muestras, el menor, el p1 y la mediana en ms, y los bloques
//                               que salieron en silencio por un hilo atrasado (BombaSonido.Huecos, desde el renglon anterior; cada 5 s)
//   lectores MAX PCT2           los hilos lectores de portadas (coverPumpCount) en esos 5 s: el mayor y el % del tiempo con dos o mas
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void MideCaratulasRegistra()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "medir-caratulas",
                Que = "la medida de las caratulas (prueba)",
                DeFabrica = false,
                SoloEnLaLista = true,
                Latido = f => f.MideLatido(),
            });
        }
        private bool mideEnvuelto; private string mideClave; private int mideListas, mideTienen = -1; private bool mideDecodas;
        private StreamWriter mideLog; private double mideUltimo = -1, mideCorte = -1;
        private readonly List<double> mideCuadros = new List<double>();
        private static readonly CultureInfo mci = CultureInfo.InvariantCulture;
        private void MideLatido()
        {
            if (mideEnvuelto || gpuPanel == null || gpuPanel.GetGrid == null) return;
            string ruta = Environment.GetEnvironmentVariable("ROCOLA_MIDE_CARATULAS");
            if (string.IsNullOrEmpty(ruta)) return;
            mideLog = new StreamWriter(ruta, true) { AutoFlush = true };
            mideLog.WriteLine("inicio\t" + GpuPanel.AhoraMs().ToString("0.0", mci) + "\tprocesadores=" + CargaTrabajo.Procesadores + " tope=" + TopeDePortadas());
            MideSonidoArranca();
            GpuGridFetch orig = gpuPanel.GetGrid; mideEnvuelto = true; mideListas = coverGovernor.CompletedPages;
            gpuPanel.GetGrid = delegate (GpuGridCell[] c)
            {
                double ahora = GpuPanel.AhoraMs();
                if (mideUltimo >= 0) mideCuadros.Add(ahora - mideUltimo);
                mideUltimo = ahora;
                if (mideCorte < 0) mideCorte = ahora;
                if (ahora - mideCorte >= 5000) { MideCuadros(); mideCorte = ahora; }
                if (coverGovernor.CompletedPages != mideListas)
                {
                    mideListas = coverGovernor.CompletedPages;
                    lock (mideLog) mideLog.WriteLine("lista\t" + mideClave + "\t" + ahora.ToString("0.0", mci) + "\t" + coverGovernor.WorstPageMs.ToString("0.0", mci));
                }
                int n = orig(c);
                string k = paginaGpuClave;
                if (k != mideClave)
                {
                    mideClave = k; coverGovernor.WorstPageMs = 0; mideDecodas = false; mideTienen = -1;
                    lock (mideLog) mideLog.WriteLine("pagina\t" + k + "\t" + ahora.ToString("0.0", mci));
                }
                if (!mideDecodas)
                {
                    int faltan = 0, tienen = 0;
                    for (int i = 0; i < n; i++) if (c[i].Slot < 18 && c[i].NeedsCover) { if (c[i].FullCover == null) faltan++; else tienen++; }
                    if (tienen != mideTienen) { mideTienen = tienen; lock (mideLog) mideLog.WriteLine("portadas\t" + k + "\t" + ahora.ToString("0.0", mci) + "\t" + tienen); }
                    if (n > 0 && faltan == 0) { mideDecodas = true; lock (mideLog) mideLog.WriteLine("decodas\t" + k + "\t" + ahora.ToString("0.0", mci)); }
                }
                return n;
            };
        }
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "lleno")] private static extern ref int MideLleno(BombaSonido b);
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "colchonN")] private static extern ref readonly int MideColchonN(BombaSonido b);
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "salidaLeyo")] private static extern ref bool MideSalidaLeyo(BombaSonido b);
        private void MideSonidoArranca()
        {
            Thread t = new Thread(delegate ()
            {
                List<double> m = new List<double>(); long huecosAntes = -1; BombaSonido antes = null; int lecMax = 0, lecDos = 0, lecN = 0;
                System.Diagnostics.Stopwatch corte = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    Thread.Sleep(2);
                    int lp = coverPumpCount; lecN++; if (lp > lecMax) lecMax = lp; if (lp >= 2) lecDos++;
                    BombaSonido b = bomba;
                    if (b != null && MideSalidaLeyo(b))
                    {
                        int cn = MideColchonN(b);
                        if (cn > 0) m.Add(MideLleno(b) * (double)BombaSonido.ColchonMs / cn);
                        if (!object.ReferenceEquals(b, antes)) { antes = b; huecosAntes = b.Huecos; }
                    }
                    if (corte.ElapsedMilliseconds >= 5000)
                    {
                        corte.Restart();
                        long hz = antes != null ? antes.Huecos - huecosAntes : 0; if (antes != null) huecosAntes = antes.Huecos;
                        if (m.Count > 0)
                        {
                            m.Sort();
                            lock (mideLog) mideLog.WriteLine("sonido\t" + m.Count + "\t" + m[0].ToString("0.0", mci) + "\t" + m[(int)(m.Count * .01)].ToString("0.0", mci) + "\t" + m[m.Count / 2].ToString("0.0", mci) + "\t" + hz);
                        }
                        m.Clear();
                        if (lecN > 0) lock (mideLog) mideLog.WriteLine("lectores\t" + lecMax + "\t" + (100.0 * lecDos / lecN).ToString("0.0", mci));
                        lecMax = lecDos = lecN = 0;
                    }
                }
            });
            t.IsBackground = true; t.Name = "mide-sonido"; t.Start();
        }
        private partial class AlbumView { internal double MideHornearMs { get { return gpuBakeCost * 1000.0 / System.Diagnostics.Stopwatch.Frequency; } } }
        private void MideCuadros()
        {
            try { lock (mideLog) mideLog.WriteLine("costes\t" + gpuPanel.MideCostes + "\thornear=" + albumView.MideHornearMs.ToString("0.00", mci)); } catch { }
            if (mideCuadros.Count == 0) return;
            mideCuadros.Sort();
            int n = mideCuadros.Count;
            lock (mideLog) mideLog.WriteLine("cuadros\t" + n + "\t" + mideCuadros[n / 2].ToString("0.0", mci) + "\t" + mideCuadros[Math.Min(n - 1, (int)(n * .95))].ToString("0.0", mci)
                + "\t" + mideCuadros[Math.Min(n - 1, (int)(n * .99))].ToString("0.0", mci) + "\t" + mideCuadros[n - 1].ToString("0.0", mci));
            mideCuadros.Clear();
        }
    }
    internal sealed partial class GpuPanel
    {
        internal string MideCostes
        {
            get
            {
                double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                return "subir_portada=" + (nativeUploadCost * f).ToString("0.00", CultureInfo.InvariantCulture) + " subir_casilla=" + (gridUploadCost * f).ToString("0.00", CultureInfo.InvariantCulture);
            }
        }
    }
}
