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

        // Al tocar un ajuste de verdad o salir de la hoja, la comparacion se corta y vuelve lo de antes (como XP).
        public void CortaCiego(string porQue) { if (ciegoActivo) TerminaCiego(porQue); }

        private string avisoAfinar = "";
        public string AvisoAfinar { get { string a = avisoAfinar; return a; } }
        private void AplicaAfinado() { try { ApplyNormNow(); } catch (Exception ex) { LogError("Afinar el local", ex); } }   // al instante en la que suena

        public IList<Ajuste> Ajustes()
        {
            if (ajustes != null) return ajustes;
            List<Ajuste> l = new List<Ajuste>();
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
            Ajuste vv = new Ajuste { Grupo = "Videos", Nombre = "Volumen de los videos", Clave = "video_vol" };
            vv.Valor = delegate { return videoVol + " %"; };
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
            l.Add(Interruptor("Pantalla", "La pared se aparta cuando nadie la usa", "muro_reposo", () => MuroReposo, v => MuroReposo = v, false));
            Ajuste ee = Interruptor("Pantalla", "Lo que suena, en grande (estado extendido)", "estado_expandido", () => EstadoExpandido, v => EstadoExpandido = v, false);
            ee.Bloqueo = delegate { return MuroReposo ? null : "necesita que la pared se aparte"; };   // como XP: lo enciende la pared quieta
            l.Add(ee);
            Ajuste c12 = Interruptor("Pantalla", "Doce de la cola en el estado extendido (si no, tres)", "cola_doce", () => ColaDoce, v => ColaDoce = v, false);
            c12.Bloqueo = delegate { return !MuroReposo ? "necesita que la pared se aparte" : !EstadoExpandido ? "necesita el estado extendido" : null; };
            l.Add(c12);
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
