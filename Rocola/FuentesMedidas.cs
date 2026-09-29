using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        // La ruta y la duracion no identifican una grabacion. El sello es del ORIGINAL,
        // tambien cuando se reproduce una version alineada de Apollo. No lee el audio.
        private sealed class FuenteMedida
        {
            internal readonly long Largo, Creada, Escrita;
            internal FuenteMedida(long largo, long creada, long escrita) { Largo = largo; Creada = creada; Escrita = escrita; }
            internal bool Igual(FuenteMedida otra)
            { return otra != null && Largo == otra.Largo && Creada == otra.Creada && Escrita == otra.Escrita; }
        }
        private sealed class RegistroFuente
        {
            internal FuenteMedida Fuente;
            internal string Medidas, Mapa;
        }
        private readonly object fuentesCerrojo = new object();
        private Dictionary<string, RegistroFuente> fuentesMedidas = new Dictionary<string, RegistroFuente>(StringComparer.OrdinalIgnoreCase);
        private int fuentesEstado = 1; // 2: formato posterior/ilegible: nunca sobrescribirlo
        private long fuentesCambios, fuentesGuardados;
        private readonly GuardadoReciente fuentesCola = new GuardadoReciente();
        private FuenteMedida curFuenteMedida;
        // El sello pertenece a los bytes del lector abierto, no a lo que haya en la
        // ruta cuando BeginInvoke entregue ese lector o termine la precarga.
        private sealed class FuenteAudioSellada : IAudioSource
        {
            internal readonly string Ruta;
            internal readonly FuenteMedida Fuente;
            private readonly IAudioSource interior;
            internal FuenteAudioSellada(string ruta, FuenteMedida fuente, IAudioSource lector)
            { Ruta = ruta; Fuente = fuente; interior = lector; }
            public NAudio.Wave.WaveFormat WaveFormat { get { return interior.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count) { return interior.Read(buffer, offset, count); }
            public float Volume { set { interior.Volume = value; } }
            public TimeSpan CurrentTime { get { return interior.CurrentTime; } }
            public TimeSpan TotalTime { get { return interior.TotalTime; } }
            public void Salta(TimeSpan donde) { interior.Salta(donde); }
            public void Dispose() { interior.Dispose(); }
        }
        private static IAudioSource SellaFuenteAudio(string original, FuenteMedida inicio, IAudioSource lector)
        {
            FuenteMedida fin = CapturaFuente(original);
            // Un sello imposible rechaza las medidas de una apertura que cambio.
            // null esta reservado al respaldo de lectores sinteticos sin sello.
            FuenteMedida sello = inicio != null && inicio.Igual(fin) ? inicio : new FuenteMedida(-1, 0, 0);
            return new FuenteAudioSellada(original, sello, lector);
        }
        private FuenteMedida FuenteDeLector(string ruta, IAudioSource lector)
        {
            FuenteSinSilencios silencio = lector as FuenteSinSilencios;
            if (silencio != null) lector = silencio.Interior;
            FuenteSinRelleno relleno = lector as FuenteSinRelleno;
            if (relleno != null) lector = relleno.Interior;
            FuenteAudioSellada sellada = lector as FuenteAudioSellada;
            if (sellada == null) return null; // respaldo sintetico, no una apertura real
            return string.Equals(RutaOriginalDe(ruta), sellada.Ruta, StringComparison.OrdinalIgnoreCase)
                ? sellada.Fuente : new FuenteMedida(-1, 0, 0); // una ruta equivocada tampoco se certifica
        }
        private bool CoincideFuenteLector(string ruta, IAudioSource lector)
        {
            FuenteMedida fuente = FuenteDeLector(ruta, lector);
            return fuente != null && fuente.Igual(CapturaFuente(RutaOriginalDe(ruta)));
        }
        [ThreadStatic] private static FuenteMedida fuenteLeida;
        private static FuenteMedida TomaFuenteMedida() { return fuenteLeida; }
        private string FuentesFile() { return RutaDatos("rocola_fuentes.txt"); }
        private static FuenteMedida CapturaFuente(string ruta)
        {
            try { FileInfo f = new FileInfo(ruta); return f.Exists ? new FuenteMedida(f.Length, f.CreationTimeUtc.Ticks, f.LastWriteTimeUtc.Ticks) : null; }
            catch { return null; }
        }
        // El worker conserva el sello que habia ANTES de leer, no el de una edicion
        // que aparecio durante la medida. Una sustitucion a mitad descarta el resultado.
        private static bool TerminaFuenteLeida(string ruta, FuenteMedida inicio)
        {
            fuenteLeida = null;
            FuenteMedida fin = CapturaFuente(ruta);
            if (inicio != null && !inicio.Igual(fin)) return false;
            fuenteLeida = inicio;
            return true;
        }
        private static string HuellaTexto(string texto)
        {
            ulong h = 14695981039346656037UL;
            unchecked { for (int i = 0; i < texto.Length; i++) { h ^= texto[i]; h *= 1099511628211UL; } }
            return h.ToString("X16", CultureInfo.InvariantCulture);
        }
        private static string FirmaMapa(MapaSilencios m)
        { return HuellaTexto(MapaSilencios.Texto(m.Desde) + "|" + MapaSilencios.Texto(m.Hasta) + "|" + MapaSilencios.Texto(m.HuecoDesde) + "|" + MapaSilencios.Texto(m.HuecoHasta) + "|" + MapaSilencios.Texto(m.Dura)); }
        private string FirmaMedidas(string ruta)
        {
            // Redondeo IDENTICO al de las tablas. Una recarga no cambia la firma por
            // perder decimales. Si un apagon publica solo una tabla, la firma no cuadra
            // y se vuelve a medir: jamas se etiqueta una medida vieja como nueva.
            StringBuilder b = new StringBuilder(); float v;
            b.Append(loudCache.TryGetValue(ruta, out v) ? v.ToString("0.######", CultureInfo.InvariantCulture) : "-").Append('|');
            b.Append(durCache.TryGetValue(ruta, out v) ? v.ToString("0.##", CultureInfo.InvariantCulture) : "-").Append('|');
            float[] eq;
            if (eqCache.TryGetValue(ruta, out eq)) for (int i = 0; i < eq.Length; i++) b.Append(eq[i].ToString("0.##", CultureInfo.InvariantCulture)).Append(';');
            b.Append('|'); SonoridadIntegrada s = new SonoridadIntegrada(); bool hay;
            lock (integCerrojo) hay = integCache != null && integCache.TryGetValue(ruta, out s);
            if (hay) { using (StringWriter w = new StringWriter(CultureInfo.InvariantCulture)) { EscribeIntegRegistro(w, "", s); b.Append(w.ToString()); } }
            return HuellaTexto(b.ToString());
        }
        private void ActualizaFuenteMedida(string ruta, FuenteMedida fuente)
        {
            if (ruta == null || fuente == null) return;
            RegistroFuente r = new RegistroFuente(); r.Fuente = fuente; r.Medidas = FirmaMedidas(ruta);
            SonoridadIntegrada s = new SonoridadIntegrada(); bool hay;
            lock (integCerrojo) hay = integCache != null && integCache.TryGetValue(ruta, out s);
            r.Mapa = hay && s.Silencios.Valido ? FirmaMapa(s.Silencios) : "-";
            lock (fuentesCerrojo) { fuentesMedidas[ruta] = r; fuentesCambios++; }
        }
        // Solo en UI: invalida TODAS las medidas correlacionadas, antes de aplicar EQ.
        private FuenteMedida CompruebaFuenteMedida(string ruta)
        {
            FuenteMedida f = CapturaFuente(ruta); if (f == null) return null;
            RegistroFuente r; lock (fuentesCerrojo) fuentesMedidas.TryGetValue(ruta, out r);
            if (r == null || !f.Igual(r.Fuente) || r.Medidas != FirmaMedidas(ruta))
            {
                bool hay = loudCache.ContainsKey(ruta) || durCache.ContainsKey(ruta) || eqCache.ContainsKey(ruta);
                lock (integCerrojo) hay |= integCache != null && integCache.ContainsKey(ruta);
                if (hay) OlvidaMedidas(ruta);
            }
            return f;
        }
        // Este filtro tambien se usa desde el abridor en fondo. No muta tablas UI.
        private bool FuenteDelMapa(string ruta, MapaSilencios mapa)
        {
            ruta = RutaOriginalDe(ruta); // Apollo usa las medidas del original validado, no las de su WAV temporal
            RegistroFuente r; lock (fuentesCerrojo) fuentesMedidas.TryGetValue(ruta, out r);
            return r != null && r.Fuente.Igual(CapturaFuente(ruta)) && r.Mapa == FirmaMapa(mapa);
        }
        private bool AceptaFuenteMedida(string ruta, FuenteMedida fuente)
        {
            if (fuente == null) return true; // respaldo sin sello: se conserva medida, nunca se certifica
            if (!fuente.Igual(CapturaFuente(ruta))) return false;
            CompruebaFuenteMedida(ruta);
            return true;
        }
        private static int LeeFuentes(TextReader reader, Dictionary<string, RegistroFuente> destino)
        {
            if (reader.ReadLine() != "ROCOLA-FUENTES v1") return 2;
            string linea;
            while ((linea = reader.ReadLine()) != null)
            {
                string[] p = linea.Split(new char[] { '|' }, 6); long n, c, e;
                if (p.Length != 6 || p[5].Length == 0 || !long.TryParse(p[0], out n) || !long.TryParse(p[1], out c) || !long.TryParse(p[2], out e)
                    || n < 0 || c <= 0 || e <= 0 || p[3].Length != 16 || (p[4] != "-" && p[4].Length != 16)) continue;
                destino[p[5]] = new RegistroFuente { Fuente = new FuenteMedida(n, c, e), Medidas = p[3], Mapa = p[4] };
            }
            return 1;
        }
        private void LoadFuentesMedidas()
        {
            Dictionary<string, RegistroFuente> d = new Dictionary<string, RegistroFuente>(StringComparer.OrdinalIgnoreCase); int estado = 1;
            try { if (File.Exists(FuentesFile())) using (StreamReader r = new StreamReader(FuentesFile())) estado = LeeFuentes(r, d); }
            catch { estado = 2; d.Clear(); }
            lock (fuentesCerrojo) { fuentesMedidas = d; fuentesEstado = estado; }
        }
        private void SaveFuentesMedidas()
        {
            if (startupReadPending || fuentesEstado != 1) return;
            List<KeyValuePair<string, RegistroFuente>> snap; long hasta;
            lock (fuentesCerrojo)
            {
                hasta = fuentesCambios; if (hasta == System.Threading.Interlocked.Read(ref fuentesGuardados)) return;
                snap = new List<KeyValuePair<string, RegistroFuente>>(fuentesMedidas);
            }
            string ruta = FuentesFile();
            GuardaUltimo(fuentesCola, delegate
            {
                try
                {
                    Func<bool> vigente = delegate { lock (fuentesCerrojo) return hasta == fuentesCambios; };
                    if (EscribeFlujoSeguro(ruta, delegate(TextWriter w)
                    {
                        w.Write("ROCOLA-FUENTES v1\r\n");
                        for (int i = 0; i < snap.Count; i++)
                        {
                            if ((i & 127) == 0 && !vigente()) return false;
                            RegistroFuente r = snap[i].Value;
                            w.Write(r.Fuente.Largo.ToString(CultureInfo.InvariantCulture)); w.Write('|');
                            w.Write(r.Fuente.Creada.ToString(CultureInfo.InvariantCulture)); w.Write('|');
                            w.Write(r.Fuente.Escrita.ToString(CultureInfo.InvariantCulture)); w.Write('|');
                            w.Write(r.Medidas); w.Write('|'); w.Write(r.Mapa); w.Write('|'); w.Write(snap[i].Key); w.Write("\r\n");
                        }
                        return true;
                    }, vigente)) System.Threading.Interlocked.Exchange(ref fuentesGuardados, hasta);
                }
                catch { } // igual que las medidas: fallo de disco no detiene la reproduccion
            });
        }
        private bool PublicaMedidaDeFuente(string ruta, float rms, float dur, float[] eq, FuenteMedida fuente)
        {
            if (ruta == null || !AceptaFuenteMedida(ruta, fuente)) return false;
            PublicaMedida(ruta, rms, dur, eq);
            ActualizaFuenteMedida(ruta, fuente);
            return true;
        }
        private void PublicaIntegradaDeFuente(string ruta, SonoridadIntegrada s, FuenteMedida fuente)
        {
            if (!AceptaFuenteMedida(ruta, fuente)) return;
            PublicaIntegrada(ruta, s);
            ActualizaFuenteMedida(ruta, fuente);
        }
        private void CierraSonoridadDeFuente(string ruta, double sum, long n, int tasa, int canales, FuenteMedida fuente)
        {
            if (!AceptaFuenteMedida(ruta, fuente)) return;
            CierraSonoridad(ruta, sum, n, tasa, canales);
            ActualizaFuenteMedida(ruta, fuente); SaveFuentesMedidas();
        }
        private void CierraHuellaDeFuente(string ruta, double[] sum, long n, int tasa, float corte, float bajo, FuenteMedida fuente)
        {
            if (!AceptaFuenteMedida(ruta, fuente)) return;
            CierraHuella(ruta, sum, n, tasa, corte, bajo);
            ActualizaFuenteMedida(ruta, fuente); SaveFuentesMedidas();
        }
    }
}
