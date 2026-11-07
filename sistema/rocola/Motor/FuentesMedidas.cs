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
        // Una fila por cancion medida, tal como esta en rocola_fuentes.txt. Antes eran cuatro objetos por cancion (el registro, su FuenteMedida y dos
        // cadenas de 16 cifras hexadecimales): con 20.000 canciones, unos 190 bytes y 80.000 objetos que viven toda la sesion. Ahora es un objeto
        // con los tres numeros del sello y las dos huellas de 64 bits (en el archivo siguen siendo 16 cifras hexadecimales: el mismo formato).
        // Sigue siendo una clase: las tablas con deltas usan null como "borrado" y comparan por referencia.
        private sealed class RegistroFuente
        {
            internal long Largo, Creada, Escrita;   // el sello del original (lo mismo que FuenteMedida)
            internal ulong Medidas, Mapa;           // huellas de las medidas y del mapa de silencios
            internal bool SinMapa;                  // "-" en el archivo: no hay mapa valido
            internal void PonSello(FuenteMedida f) { Largo = f.Largo; Creada = f.Creada; Escrita = f.Escrita; }
            internal bool Coincide(FuenteMedida f) { return f != null && Largo == f.Largo && Creada == f.Creada && Escrita == f.Escrita; }
            internal bool Igual(RegistroFuente o)
            {
                return Largo == o.Largo && Creada == o.Creada && Escrita == o.Escrita && Medidas == o.Medidas
                    && SinMapa == o.SinMapa && (SinMapa || Mapa == o.Mapa);
            }
        }
        private readonly object fuentesCerrojo = new object();
        private Dictionary<string, RegistroFuente> fuentesMedidas = new Dictionary<string, RegistroFuente>(StringComparer.OrdinalIgnoreCase);
        private int fuentesEstado = 1; // 2: formato posterior/ilegible: nunca sobrescribirlo
        private long fuentesCambios, fuentesGuardados;
        private readonly GuardadoReciente fuentesCola = new GuardadoReciente();
        private readonly Dictionary<string, RegistroFuente> fuentesPendientes = new Dictionary<string, RegistroFuente>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, RegistroFuente> fuentesTrabajo; // solo el escritor serial; copia completa una vez
        private DiarioTabla diarioFuentes;
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
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
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
        private static ulong HuellaTexto(string texto)
        {
            ulong h = 14695981039346656037UL;
            unchecked { for (int i = 0; i < texto.Length; i++) { h ^= texto[i]; h *= 1099511628211UL; } }
            return h;
        }
        // Como se escribe una huella en el archivo (y se escribia siempre): 16 cifras hexadecimales en mayusculas.
        private static string HuellaEnArchivo(ulong h) { return h.ToString("X16", CultureInfo.InvariantCulture); }
        private static bool LeeHuella(string s, out ulong h)
        {
            h = 0;
            return s.Length == 16 && ulong.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out h);
        }
        private static ulong FirmaMapa(MapaSilencios m)
        { return HuellaTexto(MapaSilencios.Texto(m.Desde) + "|" + MapaSilencios.Texto(m.Hasta) + "|" + MapaSilencios.Texto(m.HuecoDesde) + "|" + MapaSilencios.Texto(m.HuecoHasta) + "|" + MapaSilencios.Texto(m.Dura)); }
        private ulong FirmaMedidas(string ruta)
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
            RegistroFuente r = new RegistroFuente(); r.PonSello(fuente); r.Medidas = FirmaMedidas(ruta);
            SonoridadIntegrada s = new SonoridadIntegrada(); bool hay;
            lock (integCerrojo) hay = integCache != null && integCache.TryGetValue(ruta, out s);
            if (hay && s.Silencios.Valido) r.Mapa = FirmaMapa(s.Silencios); else r.SinMapa = true;
            lock (fuentesCerrojo)
            {
                RegistroFuente antes;
                if (fuentesMedidas.TryGetValue(ruta, out antes) && antes.Igual(r)) return;
                fuentesMedidas[ruta] = r; fuentesPendientes[ruta] = r; fuentesCambios++;
            }
        }
        // Solo en UI, antes de aplicar nivelado y EQ: decide si las medidas de la ruta son del archivo que hay hoy en ella.
        //
        // CONSERVAR LO VIEJO MIENTRAS SE VUELVE A MEDIR (24-10-2026). Antes, cualquier duda borraba TODAS las medidas de la cancion al
        // sonar: una biblioteca medida por una version anterior al registro de identidad (29-09-2026), una musica copiada a otro disco
        // (cambia la fecha de creacion) o una etiqueta retocada sin cambiar el tamano la dejaban sonar SIN NIVELAR, a su volumen de
        // origen; las grabaciones bajitas, con 10 o 15 dB de menos. Ahora solo se olvida lo que es otro archivo de verdad: otro tamano
        // en bytes aqui, u otra duracion (OlvidaSiEsOtroArchivo, que lo mira antes). En los demas casos la medida se ADOPTA: se apunta
        // con el sello de hoy, sigue nivelando, y la cancion queda por verificar para que el repaso la vuelva a medir antes que el resto.
        private FuenteMedida CompruebaFuenteMedida(string ruta)
        {
            FuenteMedida f = CapturaFuente(ruta); if (f == null) return null;
            RegistroFuente r; lock (fuentesCerrojo) fuentesMedidas.TryGetValue(ruta, out r);
            if (r != null && r.Coincide(f) && r.Medidas == FirmaMedidas(ruta)) return f;
            bool hay = loudCache.ContainsKey(ruta) || durCache.ContainsKey(ruta) || eqCache.ContainsKey(ruta);
            lock (integCerrojo) hay |= integCache != null && integCache.ContainsKey(ruta);
            if (!hay) return f;
            if (r != null && r.Largo != f.Largo) { OlvidaMedidas(ruta); return f; }   // otro tamano: otro archivo
            ActualizaFuenteMedida(ruta, f);   // la identidad de hoy con las medidas que hay
            lock (fuentesCerrojo) porVerificar.Add(ruta);
            lock (integHechasCerrojo) integHechas.Remove(ruta);   // que el repaso la pueda medir otra vez en esta sesion
            lock (deepDoneLock) deepDone.Remove(ruta);
            OlvidaMedidaHecha(ruta);
            SaveFuentesMedidas();
            return f;
        }
        // Las canciones con una medida adoptada sin certificar (ver arriba). Solo en memoria: el registro ya lleva el sello de hoy, y
        // tras reiniciar la medida vale como cualquier otra. La tanda larga las pone delante (TandaIntegrada).
        private readonly HashSet<string> porVerificar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Este filtro tambien se usa desde el abridor en fondo. No muta tablas UI.
        private bool FuenteDelMapa(string ruta, MapaSilencios mapa)
        {
            ruta = RutaOriginalDe(ruta); // Apollo usa las medidas del original validado, no las de su WAV temporal
            RegistroFuente r; lock (fuentesCerrojo) fuentesMedidas.TryGetValue(ruta, out r);
            return r != null && r.Coincide(CapturaFuente(ruta)) && !r.SinMapa && r.Mapa == FirmaMapa(mapa);
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
                string[] p = linea.Split(new char[] { '|' }, 6); long n, c, e; ulong medidas, mapa = 0;
                if (p.Length != 6 || p[5].Length == 0 || !long.TryParse(p[0], out n) || !long.TryParse(p[1], out c) || !long.TryParse(p[2], out e)
                    || n < 0 || c <= 0 || e <= 0 || !LeeHuella(p[3], out medidas) || (p[4] != "-" && !LeeHuella(p[4], out mapa))) continue;
                destino[p[5]] = new RegistroFuente { Largo = n, Creada = c, Escrita = e, Medidas = medidas, Mapa = mapa, SinMapa = p[4] == "-" };
            }
            return 1;
        }
        private void LoadFuentesMedidas()
        {
            Dictionary<string, RegistroFuente> d = new Dictionary<string, RegistroFuente>(StringComparer.OrdinalIgnoreCase); int estado = 1;
            try { if (File.Exists(FuentesFile())) using (FileStream fs = File.OpenRead(FuentesFile()))
                using (TextReader r = LeeLineasSanas(fs)) estado = LeeFuentes(r, d); }
            catch { estado = 2; d.Clear(); }
            lock (fuentesCerrojo) { fuentesMedidas = d; fuentesEstado = estado; fuentesTrabajo = null;
                diarioFuentes = null; fuentesPendientes.Clear(); fuentesGuardados = ++fuentesCambios; }
        }
        private void SaveFuentesMedidas()
        {
            if (startupReadPending || fuentesEstado != 1) return;
            lock (fuentesCerrojo) if (fuentesCambios == fuentesGuardados) return;
            string ruta = FuentesFile();
            GuardaUltimo(fuentesCola, delegate
            {
                try
                {
                    long hasta; Dictionary<string, RegistroFuente> trabajo;
                    List<KeyValuePair<string, RegistroFuente>> cambios; DiarioTabla diario;
                    lock (fuentesCerrojo)
                    {
                        if (fuentesEstado != 1 || fuentesCambios == fuentesGuardados) return;
                        hasta = fuentesCambios;
                        if (fuentesTrabajo == null) fuentesTrabajo = new Dictionary<string, RegistroFuente>(fuentesMedidas, StringComparer.OrdinalIgnoreCase);
                        trabajo = fuentesTrabajo;
                        cambios = new List<KeyValuePair<string, RegistroFuente>>(fuentesPendientes);
                        if (diarioFuentes == null) diarioFuentes = new DiarioTabla();
                        diario = diarioFuentes;
                    }
                    for (int i = 0; i < cambios.Count; i++)
                    { if (cambios[i].Value == null) trabajo.Remove(cambios[i].Key); else trabajo[cambios[i].Key] = cambios[i].Value; }
                    // Las escrituras normales solo comparan los cambios. El diccionario es del escritor,
                    // no de la UI; la instantanea completa solo hace falta al reconstruir/compactar.
                    Func<bool> vigente = delegate { lock (fuentesCerrojo)
                        return fuentesEstado == 1 && hasta == fuentesCambios && object.ReferenceEquals(trabajo, fuentesTrabajo); };
                    if (diario.GuardaCambios(ruta, trabajo, cambios, HuellaFuente, EscribeFuente, "ROCOLA-FUENTES v1", vigente))
                    {
                        lock (fuentesCerrojo)
                        {
                            if (!object.ReferenceEquals(trabajo, fuentesTrabajo)) return;
                            for (int i = 0; i < cambios.Count; i++)
                            { RegistroFuente actual; if (fuentesPendientes.TryGetValue(cambios[i].Key, out actual)
                                && object.ReferenceEquals(actual, cambios[i].Value)) fuentesPendientes.Remove(cambios[i].Key); }
                            System.Threading.Interlocked.Exchange(ref fuentesGuardados, hasta);
                        }
                    }
                }
                catch { } // igual que las medidas: fallo de disco no detiene la reproduccion
            });
        }
        private static long HuellaFuente(string ruta, RegistroFuente r)
        {
            long h = HuellaVacia;
            h = Mezcla(h, (int)r.Largo); h = Mezcla(h, (int)(r.Largo >> 32));
            h = Mezcla(h, (int)r.Creada); h = Mezcla(h, (int)(r.Creada >> 32));
            h = Mezcla(h, (int)r.Escrita); h = Mezcla(h, (int)(r.Escrita >> 32));
            h = Mezcla(h, (int)r.Medidas); h = Mezcla(h, (int)(r.Medidas >> 32));
            h = Mezcla(h, '|');
            h = Mezcla(h, r.SinMapa ? 1 : 0);
            if (!r.SinMapa) { h = Mezcla(h, (int)r.Mapa); h = Mezcla(h, (int)(r.Mapa >> 32)); }
            return h;
        }
        private static void EscribeFuente(TextWriter w, string ruta, RegistroFuente r)
        {
            w.Write(r.Largo.ToString(CultureInfo.InvariantCulture)); w.Write('|');
            w.Write(r.Creada.ToString(CultureInfo.InvariantCulture)); w.Write('|');
            w.Write(r.Escrita.ToString(CultureInfo.InvariantCulture)); w.Write('|');
            w.Write(HuellaEnArchivo(r.Medidas)); w.Write('|'); w.Write(r.SinMapa ? "-" : HuellaEnArchivo(r.Mapa)); w.Write('|'); w.Write(ruta); w.Write("\r\n");
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
            if (fuente != null) lock (fuentesCerrojo) porVerificar.Remove(ruta);   // medida entera de este archivo: verificada
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
