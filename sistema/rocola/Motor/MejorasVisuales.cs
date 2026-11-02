// LAS MEJORAS VISUALES DE ROCOLA OS (peticion del 2-11-2026: el dueño eligio V1-V5, V7-V24, V28-V30 de la presentacion «Rocola OS:
// mejoras visuales»). Cada una es una opcion visual con su interruptor persistente en Ctrl+F3 (paginas Imagen y Fondo). Las de coste
// bajo vienen ENCENDIDAS de fabrica; las de coste medio, APAGADAS hasta medirlas en el A4 (no se afirma que vayan fluidas sin medirlo).
// Aqui viven los interruptores y lo que el motor les da: el espectro de lo que se oye (16 bandas, al compas de las bocinas) y la forma de
// cada cancion (su volumen a lo largo del tiempo, para la barra de avance).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // ---- los interruptores (clave de configuracion entre comillas) ----
        public bool NebulosasVivas = false;     // V1 "nebulosas_vivas=" (medio)
        public bool AuroraMusica = true;        // V2 "aurora_musica="
        public bool EstrellasAire = true;       // V3 "estrellas_aire="
        public bool PortadasNitidas = true;     // V4 "portadas_nitidas="
        public bool CuboLuz = true;             // V5 "cubo_luz="
        public bool DiscoVidrio = false;        // V7 "disco_vidrio=" (medio)
        public bool ListaColores = true;        // V8 "lista_colores="
        public bool EqReal = true;              // V9 "eq_real="
        public bool BarraForma = true;          // V10 "barra_forma="
        public bool EspectroFranja = true;      // V11 "espectro_franja="
        public bool TituloTransicion = true;    // V12 "titulo_transicion="
        public bool ReposoLuz = false;          // V13 "reposo_luz=" (medio)
        public bool AnilloMusica = false;       // V14 "anillo_musica=" (medio)
        public bool ColaCarrusel = false;       // V15 "cola_carrusel=" (medio)
        public bool TecladoVivo = true;         // V16 "teclado_vivo="
        public bool ResultadosCascada = true;   // V17 "resultados_cascada="
        public bool EstrellaLuz = true;         // V18 "estrella_luz="
        public bool AtraccionCine = true;       // V19 "atraccion_cine="
        public bool MosaicoExitos = false;      // V20 "mosaico_exitos=" (medio)
        public bool AtraccionFundidos = true;   // V21 "atraccion_fundidos="
        public bool MonedaMetal = true;         // V22 "moneda_metal="
        public bool ChispasPedir = true;        // V23 "chispas_pedir="
        public bool AvisosVidrio = false;       // V24 "avisos_vidrio=" (medio)
        public bool AjustesVistaPrevia = true;  // V28 "ajustes_vista_previa="
        public bool ResolucionNativa = false;   // V30 "resolucion_nativa=" (medio; se aplica al arrancar)

        // Las claves, para leerlas y guardarlas de una sola tabla.
        private KeyValuePair<string, Func<bool>>[] clavesVisuales;
        private KeyValuePair<string, Action<bool>>[] ponVisuales;
        private void TablaVisuales()
        {
            if (clavesVisuales != null) return;
            var l = new List<KeyValuePair<string, Func<bool>>>(); var p = new List<KeyValuePair<string, Action<bool>>>();
            Action<string, Func<bool>, Action<bool>> a = (k, g, s) => { l.Add(new KeyValuePair<string, Func<bool>>(k, g)); p.Add(new KeyValuePair<string, Action<bool>>(k, s)); };
            a("nebulosas_vivas", () => NebulosasVivas, v => NebulosasVivas = v); a("aurora_musica", () => AuroraMusica, v => AuroraMusica = v);
            a("estrellas_aire", () => EstrellasAire, v => EstrellasAire = v); a("portadas_nitidas", () => PortadasNitidas, v => PortadasNitidas = v);
            a("cubo_luz", () => CuboLuz, v => CuboLuz = v); a("disco_vidrio", () => DiscoVidrio, v => DiscoVidrio = v);
            a("lista_colores", () => ListaColores, v => ListaColores = v); a("eq_real", () => EqReal, v => EqReal = v);
            a("barra_forma", () => BarraForma, v => BarraForma = v); a("espectro_franja", () => EspectroFranja, v => EspectroFranja = v);
            a("titulo_transicion", () => TituloTransicion, v => TituloTransicion = v); a("reposo_luz", () => ReposoLuz, v => ReposoLuz = v);
            a("anillo_musica", () => AnilloMusica, v => AnilloMusica = v); a("cola_carrusel", () => ColaCarrusel, v => ColaCarrusel = v);
            a("teclado_vivo", () => TecladoVivo, v => TecladoVivo = v); a("resultados_cascada", () => ResultadosCascada, v => ResultadosCascada = v);
            a("estrella_luz", () => EstrellaLuz, v => EstrellaLuz = v); a("atraccion_cine", () => AtraccionCine, v => AtraccionCine = v);
            a("mosaico_exitos", () => MosaicoExitos, v => MosaicoExitos = v); a("atraccion_fundidos", () => AtraccionFundidos, v => AtraccionFundidos = v);
            a("moneda_metal", () => MonedaMetal, v => MonedaMetal = v); a("chispas_pedir", () => ChispasPedir, v => ChispasPedir = v);
            a("avisos_vidrio", () => AvisosVidrio, v => AvisosVidrio = v); a("ajustes_vista_previa", () => AjustesVistaPrevia, v => AjustesVistaPrevia = v);
            a("resolucion_nativa", () => ResolucionNativa, v => ResolucionNativa = v);
            clavesVisuales = l.ToArray(); ponVisuales = p.ToArray();
        }
        // Una linea del archivo de configuracion: true si era de estas.
        private bool LineaVisual(string s)
        {
            TablaVisuales();
            foreach (var kv in ponVisuales)
                if (s.StartsWith(kv.Key + "=", StringComparison.Ordinal)) { kv.Value(s.Substring(kv.Key.Length + 1).Trim() != "0"); return true; }
            return false;
        }

        // ---- EL ESPECTRO DE LO QUE SE OYE (V9, V11, V14): 16 bandas de 0 a 1, del cuadro que suena por las bocinas ----
        public const int BandasEspectro = 16;
        public bool Espectro(long cuadroOido, float[] dst)
        {
            Array.Clear(dst, 0, Math.Min(dst.Length, BandasEspectro));
            MedidorPulso p = pulso; if (p == null || cuadroOido < 0) return false;
            int e = p.Escritos; if (e == 0) return false;
            for (int j = 0; j < Math.Min(e, MedidorPulso.Anillo); j++)
            {
                int k = (e - 1 - j) % MedidorPulso.Anillo;
                if (p.Cuadros[k] <= cuadroOido) { Array.Copy(p.Bandas, k * BandasEspectro, dst, 0, Math.Min(dst.Length, BandasEspectro)); return true; }
            }
            return false;
        }

        // ---- LA FORMA DE LA CANCION (V10): su volumen en 240 tramos (0 a 1), medida una vez en un hilo de fondo con el lector rapido
        // (mpg123: unos 0,3 s una cancion de 4 min aqui) y guardada en memoria (las ultimas 32). null mientras se mide o si no se puede.
        public const int TramosForma = 240;
        private readonly Dictionary<string, float[]> formas = new Dictionary<string, float[]>(StringComparer.Ordinal);
        private readonly LinkedList<string> formasOrden = new LinkedList<string>();
        private string formaMidiendo;
        public float[] FormaDe(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return null;
            lock (formas)
            {
                float[] f;
                if (formas.TryGetValue(ruta, out f)) return f;
                if (formaMidiendo != null) return null;   // una a la vez
                formaMidiendo = ruta;
            }
            System.Threading.Thread t = new System.Threading.Thread(delegate ()
            {
                Prioridad.Fondo();
                float[] r = null;
                try { r = MideForma(ruta); } catch { }
                lock (formas)
                {
                    formas[ruta] = r; formasOrden.AddFirst(ruta);
                    while (formasOrden.Count > 32) { formas.Remove(formasOrden.Last.Value); formasOrden.RemoveLast(); }
                    formaMidiendo = null;
                }
            }) { IsBackground = true, Name = "forma de la cancion", Priority = System.Threading.ThreadPriority.Lowest };
            t.Start();
            return null;
        }
        private static float[] MideForma(string ruta)
        {
            using (LectorCancion l = new LectorCancion(ruta, false))
            {
                int ch = l.WaveFormat.Channels, sr = l.WaveFormat.SampleRate;
                double total = l.Duracion.TotalSeconds;
                if (total <= 1 || total > 36000) return null;
                long porTramo = Math.Max(1, (long)(total * sr / TramosForma));
                double[] suma = new double[TramosForma]; long[] n = new long[TramosForma];
                float[] buf = new float[16384 - 16384 % ch]; long cuadro = 0; int got;
                while ((got = l.Read(buf.AsSpan())) > 0)
                {
                    for (int i = 0; i + ch - 1 < got; i += ch)
                    {
                        float x = 0; for (int c = 0; c < ch; c++) x += buf[i + c];
                        x /= ch;
                        int k = (int)Math.Min(TramosForma - 1, cuadro / porTramo);
                        suma[k] += x * x; n[k]++; cuadro++;
                    }
                }
                float[] f = new float[TramosForma]; float mx = 1e-6f;
                for (int k = 0; k < TramosForma; k++) { f[k] = n[k] > 0 ? (float)Math.Sqrt(suma[k] / n[k]) : 0f; mx = Math.Max(mx, f[k]); }
                // en dB, de -36 dB bajo lo mas fuerte (0) a lo mas fuerte (1): asi se ven las partes suaves
                for (int k = 0; k < TramosForma; k++) f[k] = Math.Max(0f, Math.Min(1f, 1f + (float)(20 * Math.Log10(Math.Max(1e-6f, f[k]) / mx)) / 36f));
                return f;
            }
        }
    }
}
