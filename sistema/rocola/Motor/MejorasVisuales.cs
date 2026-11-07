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
        public bool NebulosasVivas = true;      // V1 "nebulosas_vivas=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool AuroraMusica = true;        // V2 "aurora_musica="
        public bool EstrellasAire = true;       // V3 "estrellas_aire="
        // INTERRUPTORES RETIRADOS (7-11-2026, «borra interruptores que sean innecesarios»): ya no se pueden apagar; las claves se ignoran
        public bool PortadasNitidas { get { return true; } }     // V4 (portadas_nitidas=): siempre (los mipmaps se hacen en el hilo de las portadas)
        public bool CuboLuz = true;             // V5 "cubo_luz="
        public bool DiscoVidrio = true;         // V7 "disco_vidrio=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool ListaColores = true;        // V8 "lista_colores="
        public bool EqReal { get { return true; } }              // V9 (eq_real=): siempre el sonido real
        public bool BarraForma = true;          // V10 "barra_forma="
        public bool EspectroFranja = true;      // V11 "espectro_franja="
        public bool TituloTransicion = true;    // V12 "titulo_transicion="
        public bool ReposoLuz = true;           // V13 "reposo_luz=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool AnilloMusica = true;        // V14 "anillo_musica=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool ColaCarrusel = true;        // V15 "cola_carrusel=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool TecladoVivo = true;         // V16 "teclado_vivo="
        public bool ResultadosCascada = true;   // V17 "resultados_cascada="
        public bool EstrellaLuz = true;         // V18 "estrella_luz="
        public bool AtraccionCine = true;       // V19 "atraccion_cine="
        public bool MosaicoExitos = true;       // V20 "mosaico_exitos=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool AtraccionFundidos { get { return true; } }   // V21 (atraccion_fundidos=): siempre con fundidos
        public bool MonedaMetal = true;         // V22 "moneda_metal="
        public bool ChispasPedir = true;        // V23 "chispas_pedir="
        public bool AvisosVidrio { get { return false; } }       // V24 (avisos_vidrio=): retirado, lo hace G10 (vidrio_avisos=)
        public bool AjustesVistaPrevia = true;  // V28 "ajustes_vista_previa="
        public bool ResolucionNativa = true;    // V30 "resolucion_nativa=": la del monitor (apagado: 1024x768 si el monitor la ofrece; se aplica al arrancar)
        // VIDRIO Y LETRA (decision del 3-11-2026, presentacion «Rocola OS: vidrio y letra»: G1-G11 y la letra L1). Los vidrios sobre el cielo
        // (coste bajo) encendidos de fabrica; los que desenfocan lo que hay detras de verdad (G4, G9, G10, G11: coste medio), apagados
        // hasta medirlos en el A4.
        // COLOR VIVO (decision del 7-11-2026, maqueta «Rocola OS: mas color», la opcion Vivo: C1-C6, C9-C11; no C7 ni C8). Todo de coste
        // bajo, encendido de fabrica; mientras alguien usa la rocola los efectos bajan su intensidad (VistaGrafica.Viveza, ColorVivo.cs).
        public bool TextosColor = true;         // C1 "textos_color="
        public bool TitulosDegradado = true;    // C2 "titulos_degradado="
        public bool LetrasColor = true;         // C3 "letras_color="
        public bool VidrioColor = true;         // C4 "vidrio_color="
        public bool NeonBorde = true;           // C5 "neon_borde="
        public bool VidrioLate = true;          // C6 "vidrio_late="
        public bool PortadasBrillan = true;     // C9 "portadas_brillan="
        public bool DestelloPedir = true;       // C10 "destello_pedir="
        public bool BarraNeon = true;           // C11 "barra_neon="
        // EL MOVIMIENTO (decision del 7-11-2026: «rediseñar todas las animaciones y transiciones»; Consola/Pantalla/Movimiento.cs), coste bajo
        public bool Transiciones = true;        // M1 "transiciones=": entrar y salir de discos, buscador, atraccion, ajustes y letras
        public bool SeleccionDesliza = true;    // M2 "seleccion_desliza=": lo elegido viaja a su sitio en vez de saltar
        public bool ListasSuaves = true;        // M3 "listas_suaves=": la lista de canciones se corre suave
        public bool LetraNueva = true;          // L1 "letra_nueva=": Outfit en los titulos y Plus Jakarta Sans en el texto
        public bool VidrioCabecera = true;      // G1 "vidrio_cabecera="
        public bool VidrioLetras = true;        // G2 "vidrio_letras="
        public bool VidrioSuena = true;         // G3 "vidrio_suena="
        public bool VidrioFicha = true;         // G4 "vidrio_ficha=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool VidrioDisco = true;         // G5 "vidrio_disco="
        public bool VidrioReposo = true;        // G6 "vidrio_reposo="
        public bool VidrioBuscador = true;      // G7 "vidrio_buscador="
        public bool VidrioAtraccion = true;     // G8 "vidrio_atraccion="
        public bool VidrioAjustes = true;       // G9 "vidrio_ajustes=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool VidrioAvisos = true;        // G10 "vidrio_avisos=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public bool VidrioAyuda = true;         // G11 "vidrio_ayuda=" (medio; encendida de fabrica desde el 7-11-2026, como XP)
        public static bool LetraNuevaFalta;     // la pantalla avisa si no encontro los archivos de L1 (el interruptor, en gris)
        // Alguno de los vidrios del cielo: la pantalla prepara la copia desenfocada del cielo solo si hace falta.
        public bool AlgunVidrioDelCielo { get { return VidrioCabecera || VidrioLetras || VidrioSuena || VidrioDisco || VidrioReposo || VidrioBuscador || VidrioAtraccion; } }

        // V30 antes de tener el motor (la pantalla se abre primero, V29): lo guardado en rocola_config.txt, encendido si no dice nada.
        public static bool ResolucionNativaGuardada()
        {
            try
            {
                string f = System.IO.Path.Combine(CarpetaDatos, "rocola_config.txt");
                if (!System.IO.File.Exists(f)) return true;
                foreach (string l in System.IO.File.ReadAllLines(f)) if (l.StartsWith("resolucion_nativa=")) return l.Substring(18).Trim() != "0";
            }
            catch { }
            return true;
        }
        // Un interruptor visual antes de tener el motor (la pantalla se abre primero): lo guardado, o 'defecto' si no dice nada.
        public static bool VisualGuardado(string clave, bool defecto)
        {
            try
            {
                string f = System.IO.Path.Combine(CarpetaDatos, "rocola_config.txt");
                if (!System.IO.File.Exists(f)) return defecto;
                foreach (string l in System.IO.File.ReadAllLines(f)) if (l.StartsWith(clave + "=")) return l.Substring(clave.Length + 1).Trim() != "0";
            }
            catch { }
            return defecto;
        }
        // LAS DE COSTE MEDIO, ENCENDIDAS COMO EN XP (decision del dueno del 7-11-2026, «2»: la rocola de Linux se veia recortada frente a la
        // de XP). Una rocola que ya guardo Ctrl+F3 las tenia escritas apagadas: se encienden UNA vez (la marca va en rocola_config.txt al
        // guardar); despues manda lo que el dueno elija.
        internal const string MarcaMedio = "medio_encendido=1";
        private void EnciendeMedio()
        {
            NebulosasVivas = DiscoVidrio = ReposoLuz = AnilloMusica = ColaCarrusel = MosaicoExitos = true;
            VidrioFicha = VidrioAjustes = VidrioAvisos = VidrioAyuda = true;
        }
        // Las claves, para leerlas y guardarlas de una sola tabla.
        private KeyValuePair<string, Func<bool>>[] clavesVisuales;
        private KeyValuePair<string, Action<bool>>[] ponVisuales;
        private void TablaVisuales()
        {
            if (clavesVisuales != null) return;
            var l = new List<KeyValuePair<string, Func<bool>>>(); var p = new List<KeyValuePair<string, Action<bool>>>();
            Action<string, Func<bool>, Action<bool>> a = (k, g, s) => { l.Add(new KeyValuePair<string, Func<bool>>(k, g)); p.Add(new KeyValuePair<string, Action<bool>>(k, s)); };
            a("nebulosas_vivas", () => NebulosasVivas, v => NebulosasVivas = v); a("aurora_musica", () => AuroraMusica, v => AuroraMusica = v);
            a("estrellas_aire", () => EstrellasAire, v => EstrellasAire = v);
            a("cubo_luz", () => CuboLuz, v => CuboLuz = v); a("disco_vidrio", () => DiscoVidrio, v => DiscoVidrio = v);
            a("lista_colores", () => ListaColores, v => ListaColores = v);
            a("barra_forma", () => BarraForma, v => BarraForma = v); a("espectro_franja", () => EspectroFranja, v => EspectroFranja = v);
            a("titulo_transicion", () => TituloTransicion, v => TituloTransicion = v); a("reposo_luz", () => ReposoLuz, v => ReposoLuz = v);
            a("anillo_musica", () => AnilloMusica, v => AnilloMusica = v); a("cola_carrusel", () => ColaCarrusel, v => ColaCarrusel = v);
            a("teclado_vivo", () => TecladoVivo, v => TecladoVivo = v); a("resultados_cascada", () => ResultadosCascada, v => ResultadosCascada = v);
            a("estrella_luz", () => EstrellaLuz, v => EstrellaLuz = v); a("atraccion_cine", () => AtraccionCine, v => AtraccionCine = v);
            a("mosaico_exitos", () => MosaicoExitos, v => MosaicoExitos = v);
            a("moneda_metal", () => MonedaMetal, v => MonedaMetal = v); a("chispas_pedir", () => ChispasPedir, v => ChispasPedir = v);
            a("ajustes_vista_previa", () => AjustesVistaPrevia, v => AjustesVistaPrevia = v);
            a("resolucion_nativa", () => ResolucionNativa, v => ResolucionNativa = v);
            a("letra_nueva", () => LetraNueva, v => LetraNueva = v);
            a("vidrio_cabecera", () => VidrioCabecera, v => VidrioCabecera = v); a("vidrio_letras", () => VidrioLetras, v => VidrioLetras = v);
            a("vidrio_suena", () => VidrioSuena, v => VidrioSuena = v); a("vidrio_ficha", () => VidrioFicha, v => VidrioFicha = v);
            a("vidrio_disco", () => VidrioDisco, v => VidrioDisco = v); a("vidrio_reposo", () => VidrioReposo, v => VidrioReposo = v);
            a("vidrio_buscador", () => VidrioBuscador, v => VidrioBuscador = v); a("vidrio_atraccion", () => VidrioAtraccion, v => VidrioAtraccion = v);
            a("vidrio_ajustes", () => VidrioAjustes, v => VidrioAjustes = v); a("vidrio_avisos", () => VidrioAvisos, v => VidrioAvisos = v);
            a("vidrio_ayuda", () => VidrioAyuda, v => VidrioAyuda = v);
            a("textos_color", () => TextosColor, v => TextosColor = v); a("titulos_degradado", () => TitulosDegradado, v => TitulosDegradado = v);
            a("letras_color", () => LetrasColor, v => LetrasColor = v); a("vidrio_color", () => VidrioColor, v => VidrioColor = v);
            a("neon_borde", () => NeonBorde, v => NeonBorde = v); a("vidrio_late", () => VidrioLate, v => VidrioLate = v);
            a("portadas_brillan", () => PortadasBrillan, v => PortadasBrillan = v); a("destello_pedir", () => DestelloPedir, v => DestelloPedir = v);
            a("barra_neon", () => BarraNeon, v => BarraNeon = v);
            a("transiciones", () => Transiciones, v => Transiciones = v); a("seleccion_desliza", () => SeleccionDesliza, v => SeleccionDesliza = v);
            a("listas_suaves", () => ListasSuaves, v => ListasSuaves = v);
            // el cielo segun la hora, la fecha y el clima (CieloClima.cs)
            a("cielo_hora", () => CieloHora, v => CieloHora = v); a("sol_hora", () => SolHora, v => SolHora = v);
            a("estrellas_anochecer", () => EstrellasAnochecer, v => EstrellasAnochecer = v); a("luna_arco", () => LunaArco, v => LunaArco = v);
            a("luna_sin_disco", () => LunaSinDisco, v => LunaSinDisco = v); a("aurora_hora", () => AuroraHora, v => AuroraHora = v);
            a("medianoche", () => MedianocheCielo, v => MedianocheCielo = v); a("fiestas_cielo", () => FiestasCielo, v => FiestasCielo = v);
            a("lluvias_estrellas", () => LluviasEstrellas, v => LluviasEstrellas = v); a("luna_llena", () => LunaLlenaCielo, v => LunaLlenaCielo = v);
            a("constelaciones", () => Constelaciones, v => Constelaciones = v); a("nubes_cielo", () => NubesCielo, v => NubesCielo = v);
            a("clima_cielo", () => ClimaCielo, v => ClimaCielo = v);
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
