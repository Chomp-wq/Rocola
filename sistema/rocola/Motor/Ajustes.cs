// LOS AJUSTES DEL DUENO (en XP, Ctrl+F3 -> Sonido): los interruptores del sonido de rocola_config.txt, con los mismos nombres y valores de
// fabrica que XP. Se cambian con los botones (la vista los ensena; Controlador los maneja) y se guardan en rocola_config.txt (las demas
// lineas del archivo se conservan tal cual). Lo que cambia se oye desde la siguiente cancion (la cadena se arma al empezar cada una).
// Con el modo billar encendido, lo que decide el modo va "en gris" con el motivo, como en XP, y conserva lo guardado.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
        public sealed class Ajuste
        {
            public string Nombre, Clave, Grupo;
            internal Func<string> Valor;            // como se ve
            internal Func<string> Guardado;         // como va en rocola_config.txt
            internal Action<int> Cambia;            // +1 / -1
            internal Func<string> Bloqueo;          // null si se puede cambiar; si no, el motivo
            public bool Herramienta;                // no se guarda ni corta la comparacion a ciegas (S4, las comparaciones, el voto)
            internal Func<int> Barra;               // con barra (los deslizadores de XP): donde va, de 0 a 100; null si no lleva
            internal Func<string> Nota;             // el renglon de explicacion de XP, debajo del nombre (null si no lleva)
            public int? Posicion { get { return Barra != null ? Barra() : (int?)null; } }
            public string Explicacion { get { return Nota != null ? Nota() : null; } }
            public string Texto { get { return Valor(); } }
            public string ComoSeGuarda { get { return Guardado != null ? Guardado() : null; } }
            public string Motivo { get { return Bloqueo != null ? Bloqueo() : null; } }
            public void Mueve(int d) { if (Motivo == null) Cambia(d); }
        }
        private List<Ajuste> ajustes;
        private const string PorElBillar = "lo decide el modo billar";

        private Ajuste Interruptor(string grupo, string nombre, string clave, Func<bool> lee, Action<bool> pon, bool delBillar)
        {
            Ajuste a = new Ajuste { Grupo = grupo, Nombre = nombre, Clave = clave };
            a.Valor = delegate { return lee() ? "SÍ" : "NO"; };
            a.Guardado = delegate { return lee() ? "1" : "0"; };
            a.Cambia = delegate (int d) { pon(!lee()); };
            if (delBillar) a.Bloqueo = delegate { return LocalAutoAGuardar ? PorElBillar : null; };   // a ciegas, sin delatar el tramo
            return a;
        }

        private Ajuste Ciego(string nombre, int que)
        {
            Ajuste a = new Ajuste { Grupo = "Lo que suena ahora", Nombre = nombre, Herramienta = true };
            a.Valor = delegate { return ciegoActivo && ciegoQue == que ? "PARAR" : "EMPEZAR"; };
            a.Cambia = delegate (int d)
            {
                if (ciegoActivo && ciegoQue == que) TerminaCiego("Comparacion parada. ");
                else { if (ciegoActivo) TerminaCiego(""); string r = EmpiezaCiego(que); if (r.Length > 0) ciegoResultado = r; }
            };
            return a;
        }

        private int ObjetivoPct() { int p = (int)Math.Round((normTarget - NormTargetMin) / (NormTargetMax - NormTargetMin) * 100f); return p < 0 ? 0 : (p > 100 ? 100 : p); }
        // El nivel se oye al instante en la que suena, como al mover las barras en XP.
        private void AplicaNivel() { try { ApplyNormNow(); } catch (Exception ex) { LogError("Volumen al limite", ex); } }

        // Al tocar un ajuste de verdad o salir de la hoja, la comparacion se corta y vuelve lo de antes (como XP).
        public void CortaCiego(string porQue) { if (ciegoActivo) TerminaCiego(porQue); }

        public volatile bool PideDiagnostico;   // la hoja lo pide; el programa lo corre y lo apaga
        public string DatosEn { get { return CarpetaDatos; } }
        private string avisoAfinar = "";
        public string AvisoAfinar { get { string a = avisoAfinar; return a; } }
        private void AplicaAfinado() { try { ApplyNormNow(); } catch (Exception ex) { LogError("Afinar el local", ex); } }   // al instante en la que suena

        // LAS PAGINAS (las pestanas de Ctrl+F3 de XP): Sonido, Pantalla y Detalles, en este orden al recorrerlas.
        public static readonly string[] PaginasAjustes = { "Sonido", "Pantalla", "Detalles" };
        public static string PaginaDe(string grupo)
        {
            if (grupo == "Pantalla") return "Pantalla";
            if (grupo == "Internet" || grupo == "Diagnóstico") return "Detalles";
            return "Sonido";
        }
        public IList<Ajuste> Ajustes()
        {
            if (ajustes != null) return ajustes;
            List<Ajuste> l = new List<Ajuste>();
            // VOLUMEN AL LIMITE (la tarjeta de arriba de la pagina Sonido de XP, con sus mismas claves): el interruptor y las barras
            Ajuste niv = Interruptor("Volumen al limite", "Subir cada cancion hasta su limite", "normalizar", () => normalizeOn, v => { normalizeOn = v; AplicaNivel(); }, false);
            l.Add(niv);
            Ajuste fue = new Ajuste { Grupo = "Volumen al limite", Nombre = "Cuanto se sube cada cancion", Clave = "nivel_fuerza" };
            fue.Valor = delegate { int p = (int)Math.Round(normStrength * 100f); return p >= 100 ? "Al limite" : p <= 0 ? "Tal cual" : p + "%"; };
            fue.Guardado = delegate { return normStrength.ToString("0.####", CultureInfo.InvariantCulture); };
            fue.Cambia = delegate (int d) { normStrength = Math.Max(0f, Math.Min(1f, (float)Math.Round(normStrength * 20f + d) / 20f)); AplicaNivel(); };
            fue.Barra = delegate { return (int)Math.Round(normStrength * 100f); };
            fue.Nota = delegate { return "100 % = cada una sube todo lo que aguanta sin aplastarse; " + (normalizeOn ? "menos, se quedan un poco por debajo de su limite." : "apagado, todas suenan como vienen."); };
            fue.Bloqueo = delegate { return normalizeOn ? null : "apagado arriba"; };
            l.Add(fue);
            Ajuste obj = new Ajuste { Grupo = "Volumen al limite", Nombre = "Videos y canciones aun sin medir", Clave = "nivel_objetivo" };
            obj.Valor = delegate { return ObjetivoPct() + "%"; };
            obj.Guardado = delegate { return normTarget.ToString("0.####", CultureInfo.InvariantCulture); };
            obj.Cambia = delegate (int d) { int p = Math.Max(0, Math.Min(100, ObjetivoPct() + 5 * d)); normTarget = NormTargetMin + (p / 100f) * (NormTargetMax - NormTargetMin); AplicaNivel(); };
            obj.Barra = ObjetivoPct;
            obj.Nota = delegate { int falta = porRepasar.Count; return falta > 0 ? "Los videos y las " + falta.ToString("#,0", CultureInfo.InvariantCulture) + " canciones que faltan por medir suben hacia aqui; las ya medidas, hasta su limite." : "Los videos suben hacia aqui. Las canciones ya estan medidas: cada una sube hasta su limite."; };
            obj.Bloqueo = fue.Bloqueo;
            l.Add(obj);
            Ajuste billar = Interruptor("El billar", "Modo billar (la cadena para las bocinas del billar)", "local_auto", () => localAuto, v => localAuto = v, false);
            billar.Guardado = delegate { return LocalAutoAGuardar ? "1" : "0"; };
            billar.Valor = delegate { return LocalAutoAGuardar ? "SÍ" : "NO"; };   // a ciegas no se ve cual suena   // durante la comparacion a ciegas, el de antes (como XP)
            l.Add(billar);
            l.Add(Interruptor("El local", "Claridad para el local", "claridad_local", () => claridadLocal, v => claridadLocal = v, true));
            l.Add(Interruptor("El local", "Que las partes suaves se oigan", "partes_suaves", () => partesSuaves, v => partesSuaves = v, true));
            l.Add(Interruptor("El local", "Presencia en sala ruidosa", "presencia_sala", () => presenciaSala, v => presenciaSala = v, true));
            l.Add(Interruptor("El local", "Quitar el subgrave", "corta_subgrave", () => cortaSubgrave, v => cortaSubgrave = v, true));
            l.Add(Interruptor("El local", "Estereo mas estrecho", "estereo_estrecho", () => estereoEstrecho, v => estereoEstrecho = v, true));
            l.Add(Interruptor("El local", "Suavizar las eses", "suaviza_eses", () => suavizaEses, v => suavizaEses = v, true));
            // LA PRUEBA DE BOCINAS (SalaLocal.cs de XP): IZQ/DER pasan por callada, izquierda, derecha, las dos y la polaridad.
            Ajuste pr = new Ajuste { Grupo = "El local", Nombre = "Prueba de bocinas (con la rocola callada)", Herramienta = true };
            pr.Valor = delegate { return pruebaSonando == null ? "callada" : pruebaSonando.Modo == PruebaModo.Izquierda ? "izquierda" : pruebaSonando.Modo == PruebaModo.Derecha ? "derecha" : pruebaSonando.Modo == PruebaModo.Ambas ? "las dos" : "polaridad"; };
            pr.Cambia = delegate (int d)
            {
                int i = pruebaSonando == null ? 0 : 1 + (int)pruebaSonando.Modo;   // 0 callada, 1..4 los modos
                i = (i + d + 5) % 5;
                if (i == 0) ParaPrueba(); else { string r = ProbarBocinas((PruebaModo)(i - 1)); avisoAfinar = r.StartsWith("Sonando") ? "" : r; }
            };
            l.Add(pr);
            l.Add(Interruptor("Voz, golpe y retumbe", "Retumbe dinamico", "retumbe_dinamico", () => retumbeDinamico, v => retumbeDinamico = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Ataque mas claro", "ataque_claro", () => ataqueClaro, v => ataqueClaro = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Agudos asperos dinamicos", "aspereza_dinamica", () => asperezaDinamica, v => asperezaDinamica = v, true));
            l.Add(Interruptor("Voz, golpe y retumbe", "Que el bombo no hunda la voz", "bombo_sin_hundir", () => bomboSinHundir, v => bomboSinHundir = v, true));
            // S4 de XP: una herramienta, no un ajuste (no se guarda); 15 s, o hasta volver a pulsar o salir de la hoja.
            Ajuste sin = new Ajuste { Grupo = "Lo que suena ahora", Nombre = "Escuchar sin mejoras (" + SinMejorasSeg + " s)", Herramienta = true };
            sin.Valor = delegate { int q = SinMejorasQuedan(); return q > 0 ? "SÍ (" + q + " s)" : "NO"; };
            sin.Cambia = delegate (int d) { PonSinMejoras(SinMejorasQuedan() == 0); };
            l.Add(sin);
            // COMPARAR A CIEGAS (CompararCiegas.cs de XP): empezar o parar cada una, y el voto del tramo que suena.
            l.Add(Ciego("Comparar a ciegas: billar o fábrica", CiegoBillar));
            l.Add(Ciego("Comparar a ciegas: con o sin afinar", CiegoAfinacion));
            l.Add(Ciego("Comparar a ciegas: presencia nueva o de antes", CiegoPresencia));
            Ajuste voto = new Ajuste { Grupo = "Lo que suena ahora", Nombre = "Me gusta esta (un voto por tramo)", Herramienta = true };
            voto.Valor = delegate { return ciegoActivo ? (ciegoVotado != null && ciegoTramo >= 0 && ciegoVotado[ciegoTramo] ? "VOTADO" : "VOTAR") : "—"; };
            voto.Cambia = delegate (int d) { VotaCiego(); };
            voto.Bloqueo = delegate { return ciegoActivo ? null : "solo durante una comparación"; };
            l.Add(voto);
            l.Add(Interruptor("Lo que suena ahora", "Saltar los silencios largos", "saltar_silencios", () => saltaSilencios, v => saltaSilencios = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Quitar el zumbido de la corriente", "quita_zumbido", () => quitaZumbido, v => quitaZumbido = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Quitar el pre-eco de los MP3 viejos", "quita_preeco", () => quitaPreEco, v => quitaPreEco = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Un canal mudo, por los dos", "canal_mudo", () => canalMudo, v => canalMudo = v, false));
            l.Add(Interruptor("Lo que suena ahora", "Quitar la corriente continua", "quita_continua", () => quitaContinua, v => quitaContinua = v, false));
            Ajuste dsee = Interruptor("Mejoras con IA", "Reconstruir los agudos (DSEE)", "dsee", () => sbrOn, v => sbrOn = v, true);
            dsee.Bloqueo = delegate { return !sbrAllowed ? "este equipo no llega (" + sbrReason + ")" : LocalAutoAGuardar ? PorElBillar : null; };
            l.Add(dsee);
            l.Add(Interruptor("Mejoras con IA", "Bajo y brillo a la medida (I1)", "a_la_medida", () => aLaMedida, v => aLaMedida = v, false));
            l.Add(Interruptor("Mejoras con IA", "Quitar el siseo de las grabaciones viejas (I2)", "quita_siseo", () => quitaSiseo, v => quitaSiseo = v, true));
            // AFINAR EL LOCAL CON EL CELULAR (AfinarLocal.cs de XP, en su forma mas simple de la sexta tanda): tocar las 8 bandas, decir si la
            // app mide en dBA o dBC, anotar lo que marca cada banda y usar la afinacion (con el modo billar). Tocar algo aqui corta la
            // comparacion a ciegas (como en XP); lo que cambia se aplica al instante en la que suena.
            Ajuste ab = new Ajuste { Grupo = "Afinar con el celular", Nombre = "Tocar las 8 bandas (de grave a agudo)", Herramienta = true };
            ab.Valor = delegate { return bandaSonando != null ? "PARAR" : "EMPEZAR"; };
            ab.Cambia = delegate (int d) { if (bandaSonando != null) ParaPrueba(); else { string r = ProbarBandas(0, 7); avisoAfinar = r.StartsWith("Sonando") ? "" : r; } };
            l.Add(ab);
            Ajuste app = new Ajuste { Grupo = "Afinar con el celular", Nombre = "Tu app dice", Clave = "afinado_ponderacion" };
            app.Valor = delegate { return "dB" + AfinaPonderacionNombre[afinadoPonderacion]; };
            app.Guardado = delegate { return AfinaPonderacionNombre[afinadoPonderacion]; };
            app.Cambia = delegate (int d) { afinadoPonderacion = afinadoPonderacion == 0 ? 1 : 0; AplicaAfinado(); };   // A o C (la Z guardada se sigue leyendo)
            l.Add(app);
            for (int b = 0; b < 8; b++)
            {
                int banda = b;
                Ajuste lb = new Ajuste { Grupo = "Afinar con el celular", Nombre = "Marca en " + NombreBanda(banda), Clave = "afinado_local" };
                lb.Valor = delegate { float v = afinadoDb[banda]; return float.IsNaN(v) ? "sin medir" : v.ToString("0", CultureInfo.InvariantCulture) + " dB"; };
                lb.Guardado = delegate { return TextoAfinado(); };
                lb.Cambia = delegate (int d)
                {
                    float v = afinadoDb[banda];
                    if (float.IsNaN(v)) v = 70f;   // de "sin medir" se empieza en 70 dB (lo tipico de musica bajita a unos metros)
                    else { v += d; if (v < AfinaMinDb) v = float.NaN; else if (v > AfinaMaxDb) v = AfinaMaxDb; }
                    afinadoDb[banda] = v; AplicaAfinado();
                };
                l.Add(lb);
            }
            Ajuste usa = new Ajuste { Grupo = "Afinar con el celular", Nombre = "Usar la afinacion", Clave = "afinado_usar" };
            usa.Valor = delegate { return AfinadoUsarAGuardar ? "SÍ" : "NO"; };
            usa.Guardado = delegate { return AfinadoUsarAGuardar ? "1" : "0"; };
            usa.Cambia = delegate (int d) { afinadoUsar = !afinadoUsar; AplicaAfinado(); };
            usa.Bloqueo = delegate
            {
                if (!LocalAutoAGuardar) return "necesita el modo billar";
                if (CorreccionAfinada(AfinadoPromedio(), afinadoPonderacion) == null) return "faltan las bandas de la voz (250 Hz a 2 kHz)";
                return null;
            };
            l.Add(usa);
            float[] fundidos = { 0f, 0.5f, 1f, 1.5f, 2f, 3f };
            Ajuste fu = new Ajuste { Grupo = "Entre canciones", Nombre = "Fundido entre canciones", Clave = "fundido" };
            fu.Valor = delegate { return crossfadeSec <= 0.01f ? "sin fundido" : crossfadeSec.ToString("0.#", CultureInfo.InvariantCulture) + " s"; };
            fu.Guardado = delegate { return crossfadeSec.ToString("0.##", CultureInfo.InvariantCulture); };
            fu.Cambia = delegate (int d)
            {
                int i = 0; for (int j = 0; j < fundidos.Length; j++) if (Math.Abs(fundidos[j] - crossfadeSec) < 0.05f) i = j;
                crossfadeSec = fundidos[Math.Max(0, Math.Min(fundidos.Length - 1, i + d))];
            };
            l.Add(fu);
            Ajuste vv = new Ajuste { Grupo = "Volumen al limite", Nombre = "Volumen de los videos", Clave = "video_vol" };
            vv.Valor = delegate { return videoVol + "%"; };
            vv.Barra = delegate { return videoVol; };
            vv.Nota = delegate { return "Los videos no pasan por la cadena de sonido: los pone el reproductor de videos."; };
            vv.Guardado = delegate { return videoVol.ToString(CultureInfo.InvariantCulture); };
            vv.Cambia = delegate (int d) { videoVol = Math.Max(0, Math.Min(100, videoVol + 5 * d)); };
            l.Add(vv);
            l.Add(Interruptor("Pantalla", "Ficha rapida del disco (al quedarse en uno)", "ficha_disco", () => FichaDisco, v => FichaDisco = v, false));
            l.Add(Interruptor("Pantalla", "Barra de letras con lupa", "letras_lupa", () => LetrasLupa, v => LetrasLupa = v, false));
            l.Add(Interruptor("Pantalla", "Pasar de pagina deslizando", "pagina_desliza", () => PaginaDesliza, v => PaginaDesliza = v, false));
            l.Add(Interruptor("Pantalla", "Lista grande dentro del disco", "lista_grande", () => ListaGrande, v => ListaGrande = v, false));
            l.Add(Interruptor("Pantalla", "Ecualizador en la que suena", "eq_sonando", () => EqSonando, v => EqSonando = v, false));
            l.Add(Interruptor("Pantalla", "En cola, con los minutos", "cola_minutos", () => ColaMinutos, v => ColaMinutos = v, false));
            l.Add(Interruptor("Pantalla", "Ver adonde va (el boleto vuela a la cola)", "vuelo_cola", () => VueloCola, v => VueloCola = v, false));
            l.Add(Interruptor("Pantalla", "La moneda que cae", "moneda_cae", () => MonedaCae, v => MonedaCae = v, false));
            l.Add(Interruptor("Pantalla", "Sin creditos, invitar", "sin_creditos_invita", () => SinCreditosInvita, v => SinCreditosInvita = v, false));
            l.Add(Interruptor("Pantalla", "Avisos que se entienden", "avisos_claros", () => AvisosClaros, v => AvisosClaros = v, false));
            l.Add(Interruptor("Pantalla", "La atraccion como escaparate", "escaparate", () => Escaparate, v => Escaparate = v, false));
            l.Add(Interruptor("Pantalla", "Lo mas pedido esta noche (en la atraccion)", "top_noche", () => TopNocheOn, v => TopNocheOn = v, false));
            l.Add(Interruptor("Pantalla", "Letra para leer de lejos (lo que suena)", "letra_lejos", () => LetraLejos, v => LetraLejos = v, false));
            l.Add(Interruptor("Pantalla", "El color del genero en todo", "color_genero", () => ColorGenero, v => ColorGenero = v, false));
            l.Add(Interruptor("Pantalla", "Pistas del rojo mantenido", "guia_rojo", () => GuiaRojo, v => GuiaRojo = v, false));
            l.Add(Interruptor("Pantalla", "Los textos que no caben se deslizan", "textos_deslizantes", () => TextosDeslizantes, v => TextosDeslizantes = v, false));
            l.Add(Interruptor("Pantalla", "El buscador con portadas", "buscador_portadas", () => BuscadorPortadas, v => BuscadorPortadas = v, false));
            // INTERNET (1-11-2026): actualizarse solo desde GitHub (rocola-actualiza, en el sistema) y buscar ya. El recuadro de abajo dice la
            // version, la red (con la MAC, la que pide el router para el encendido remoto) y como va la actualizacion.
            l.Add(Interruptor("Internet", "Actualizarse sola desde internet", "actualizar_solo", () => ActualizarSolo, v => ActualizarSolo = v, false));
            Ajuste ya = new Ajuste { Grupo = "Internet", Nombre = "Buscar actualización ahora", Herramienta = true };
            ya.Valor = delegate { return File.Exists("/run/rocola-buscar-ya") ? "BUSCANDO" : "BUSCAR"; };
            ya.Cambia = delegate (int d) { try { File.WriteAllText("/run/rocola-buscar-ya", ""); } catch { } };
            ya.Bloqueo = delegate { return ActualizarSolo ? null : "necesita «Actualizarse sola»"; };
            l.Add(ya);
            // EL DIAGNOSTICO COMPLETO (herramienta de XP: no guarda ni aplica nada); lo corre el programa (Diagnostico.cs).
            Ajuste dg = new Ajuste { Grupo = "Diagnóstico", Nombre = "Diagnóstico completo de la pantalla (unos 50 s)", Herramienta = true };
            dg.Valor = delegate { return "EMPEZAR"; };
            dg.Cambia = delegate (int d) { PideDiagnostico = true; };
            l.Add(dg);
            l.Add(Interruptor("Pantalla", "La estrella del cielo", "luna", () => EstrellaCielo, v => EstrellaCielo = v, false));
            Ajuste e3 = Interruptor("Pantalla", "La estrella en 3D, girando", "estrella_3d", () => Estrella3d, v => Estrella3d = v, false);
            e3.Bloqueo = delegate { return EstrellaCielo ? null : "necesita la estrella del cielo"; }; l.Add(e3);
            Ajuste hl = Interruptor("Pantalla", "Su corona", "halo_lunar", () => HaloLunar, v => HaloLunar = v, false);
            hl.Bloqueo = e3.Bloqueo; l.Add(hl);
            Ajuste pl = Interruptor("Pantalla", "Su polvo en orbita", "polvo_lunar", () => PolvoLunar, v => PolvoLunar = v, false);
            pl.Bloqueo = e3.Bloqueo; l.Add(pl);
            Ajuste es = Interruptor("Pantalla", "Su estela de copias", "estela_estelar", () => EstelaEstelar, v => EstelaEstelar = v, false);
            es.Bloqueo = e3.Bloqueo; l.Add(es);
            l.Add(Interruptor("Pantalla", "La pared se aparta cuando nadie la usa", "muro_reposo", () => MuroReposo, v => MuroReposo = v, false));
            Ajuste ee = Interruptor("Pantalla", "Lo que suena, en grande (estado extendido)", "estado_expandido", () => EstadoExpandido, v => EstadoExpandido = v, false);
            ee.Bloqueo = delegate { return MuroReposo ? null : "necesita que la pared se aparte"; };   // como XP: lo enciende la pared quieta
            l.Add(ee);
            Ajuste c12 = Interruptor("Pantalla", "Doce de la cola en el estado extendido (si no, tres)", "cola_doce", () => ColaDoce, v => ColaDoce = v, false);
            c12.Bloqueo = delegate { return !MuroReposo ? "necesita que la pared se aparte" : !EstadoExpandido ? "necesita el estado extendido" : null; };
            l.Add(c12);
            // LAS NOTAS de la ventana de XP (el renglon gris debajo de cada mando), las mismas frases
            Dictionary<string, string> notas = new Dictionary<string, string>
            {
                { "Modo billar (la cadena para las bocinas del billar)", "Apagado: el sonido sale como lo dejes en los ajustes de abajo. Encendido, la rocola ajusta sola cada cancion a las bocinas del billar (bocinas repartidas por zonas, sin subwoofer)." },
                { "Quitar el subgrave", "Ninguna bocina del billar da menos de 30 Hz: solo gasta amplificador." },
                { "Estereo mas estrecho", "Con las bocinas por zonas, lo grabado a un lado se oye en todas." },
                { "Claridad para el local", "Menos retumbe (300 Hz) y la voz mas presente (3 kHz)." },
                { "Que las partes suaves se oigan", "Sube hasta 4 dB las intros y pasajes bajitos; lo fuerte, igual." },
                { "Presencia en sala ruidosa", "La voz que baja en la estrofa y las intros se quedan mas cerca de lo fuerte (unos 3 dB); el volumen total no cambia." },
                { "Prueba de bocinas (con la rocola callada)", "Un ruido suave por donde elijas: para igualar IN A e IN B del amplificador y revisar cada bocina." },
                { "Bajo y brillo a la medida (I1)", "Compara cada cancion con lo que tendria una bien grabada como ella (la IA del EQ), no con el promedio de todas." },
                { "Quitar el siseo de las grabaciones viejas (I2)", "Aprende el ruido fijo de cada cancion y lo baja hasta 6 dB en los agudos. Se nota desde la siguiente cancion." },
                { "Un canal mudo, por los dos", "Si una cancion trae un canal vacio, los dos dan el que suena, a la misma potencia. Solo en las que lo traen." },
                { "Quitar la corriente continua", "Si la onda viene corrida, se centra: el limitador no la baja de mas y los conos no se empujan." },
                { "Quitar el pre-eco de los MP3 viejos", "El siseo antes de cada golpe de los MP3 mal hechos. Solo en las que el repaso midio con el; se nota desde la siguiente." },
            };
            foreach (Ajuste x in l) { string nt; if (x.Nota == null && notas.TryGetValue(x.Nombre, out nt)) { string fija = nt; x.Nota = () => fija; } }
            foreach (Ajuste x in l) if (x.Nombre.StartsWith("Escuchar sin mejoras", StringComparison.Ordinal) && x.Nota == null) x.Nota = () => "Mientras dura, la cancion suena sin las mejoras, al mismo volumen: para comparar con el oido. No se guarda.";
            // Por paginas, como las pestanas de la ventana de XP (el orden dentro de cada una se conserva)
            List<Ajuste> orden = new List<Ajuste>(l.Count);
            foreach (string pg in PaginasAjustes) foreach (Ajuste x in l) if (PaginaDe(x.Grupo) == pg) orden.Add(x);
            l = orden;
            ajustes = l;
            return l;
        }

        // A rocola_config.txt: cada clave de los ajustes en su linea (cambiada o anadida); lo demas, tal cual.
        public void GuardaAjustes()
        {
            List<string> lineas = new List<string>();
            try { if (File.Exists(ConfigFile)) lineas.AddRange(File.ReadAllLines(ConfigFile)); } catch { }
            foreach (Ajuste a in Ajustes())
            {
                if (a.Clave == null || a.Herramienta) continue;   // las herramientas no se guardan
                string linea = a.Clave + "=" + a.Guardado();
                int i = lineas.FindIndex(x => x.Trim().StartsWith(a.Clave + "=", StringComparison.Ordinal));
                if (i >= 0) lineas[i] = linea; else lineas.Add(linea);
            }
            StringBuilder sb = new StringBuilder();
            foreach (string x in lineas) sb.Append(x).Append("\r\n");
            EscribeYa(ConfigFile, sb.ToString());
        }
    }
}
