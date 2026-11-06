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

        // Un ajuste de botones (la hoja Colores de XP): IZQ/DER pasa al de al lado; lo guardado es el numero, como en XP.
        private Ajuste Opciones(string grupo, string nombre, string clave, string[] nombres, int[] valores, Func<int> lee, Action<int> pon, string nota)
        {
            Ajuste a = new Ajuste { Grupo = grupo, Nombre = nombre, Clave = clave };
            Func<int> cual = delegate { int v = lee(), mejor = 0; for (int i = 1; i < valores.Length; i++) if (Math.Abs(valores[i] - v) < Math.Abs(valores[mejor] - v)) mejor = i; return mejor; };
            a.Valor = delegate { int i = cual(); return valores[i] == lee() ? nombres[i] : lee() + "%"; };
            a.Guardado = delegate { return lee().ToString(CultureInfo.InvariantCulture); };
            a.Cambia = delegate (int d) { int i = cual() + (d < 0 ? -1 : 1); if (i < 0) i = valores.Length - 1; if (i >= valores.Length) i = 0; pon(valores[i]); };
            a.Nota = () => nota;
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

        // Un tono del local en dB (de -6 a +6, de 1 en 1), con su barra; lo decide el modo billar.
        private Ajuste TonoDb(string nombre, string clave, Func<int> lee, Action<int> pon, string nota)
        {
            Ajuste a = new Ajuste { Grupo = "El local", Nombre = nombre, Clave = clave };
            a.Valor = delegate { int v = lee(); return v == 0 ? "0 dB" : (v > 0 ? "+" : "") + v + " dB"; };
            a.Guardado = delegate { return lee().ToString(CultureInfo.InvariantCulture); };
            a.Cambia = delegate (int d) { pon(Math.Max(-SalaLocal.TonoMax, Math.Min(SalaLocal.TonoMax, lee() + d))); AplicaAlInstante(); };
            a.Barra = delegate { return (int)Math.Round((lee() + SalaLocal.TonoMax) * 100f / (2 * SalaLocal.TonoMax)); };
            a.Nota = () => nota;
            a.Bloqueo = delegate { return LocalAutoAGuardar ? PorElBillar : null; };
            return a;
        }
        // Una cantidad de 0 a 100 % con su barra (el EQ, los graves, el realce de XP); en gris si la decide la rocola o el modo billar.
        private Ajuste Cantidad(string grupo, string nombre, string clave, Func<float> lee, Action<float> pon, bool delBillar, Func<bool> automatico, string nota, bool enPorCiento)
        {
            Ajuste a = new Ajuste { Grupo = grupo, Nombre = nombre, Clave = clave };
            a.Valor = delegate { return (int)Math.Round(lee() * 100f) + "%"; };
            a.Guardado = delegate { return enPorCiento ? ((int)Math.Round(lee() * 100f)).ToString(CultureInfo.InvariantCulture) : lee().ToString("0.####", CultureInfo.InvariantCulture); };
            a.Cambia = delegate (int d) { pon(Math.Max(0f, Math.Min(1f, (float)Math.Round(lee() * 20f + d) / 20f))); AplicaAlInstante(); };
            a.Barra = delegate { return (int)Math.Round(lee() * 100f); };
            a.Nota = () => nota;
            a.Bloqueo = delegate { return delBillar && LocalAutoAGuardar ? PorElBillar : automatico() ? "lo decide la rocola (arriba)" : null; };
            return a;
        }
        private void AplicaAlInstante() { try { AplicaBocinas(); ApplyNormNow(); } catch (Exception ex) { LogError("Ajustes del sonido", ex); } }

        private int ObjetivoPct() { int p = (int)Math.Round((normTarget - NormTargetMin) / (NormTargetMax - NormTargetMin) * 100f); return p < 0 ? 0 : (p > 100 ? 100 : p); }
        // El nivel se oye al instante en la que suena, como al mover las barras en XP.
        private void AplicaNivel() { try { ApplyNormNow(); } catch (Exception ex) { LogError("Volumen al limite", ex); } }

        // DEJAR COMO DE FABRICA (el boton de la ventana de XP): los valores con que arranca un motor nuevo, guardados antes de leer
        // rocola_config.txt, se vuelven a poner con la misma lectura de cada linea. Se guardan al salir con REGRESAR, como lo demas.
        private Dictionary<string, string> deFabrica;
        private int fabricaPide;   // la primera pulsacion pide confirmar; la segunda, en 5 s, lo hace
        private void CapturaFabrica()
        {
            deFabrica = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Ajuste a in Ajustes())
            {
                if (a.Clave == null || a.Herramienta) continue;
                try { string v = a.Guardado(); if (v != null) deFabrica[a.Clave] = v; } catch { }
            }
        }
        public bool DejaDeFabrica()
        {
            if (deFabrica == null) return false;
            CortaCiego("Ajustes de fabrica: comparacion cortada. ");
            ParaPrueba();
            foreach (KeyValuePair<string, string> kv in deFabrica) { try { AplicaLineaConfig(kv.Key + "=" + kv.Value); } catch { } }
            if (!sbrAllowed) sbrOn = false;
            AplicaNivel();
            return true;
        }

        // Al tocar un ajuste de verdad o salir de la hoja, la comparacion se corta y vuelve lo de antes (como XP).
        public void CortaCiego(string porQue) { if (ciegoActivo) TerminaCiego(porQue); }

        public volatile bool PideDiagnostico;   // la hoja lo pide; el programa lo corre y lo apaga
        public string DatosEn { get { return CarpetaDatos; } }
        private string avisoAfinar = "";
        public string AvisoAfinar { get { string a = avisoAfinar; return a; } }
        private void AplicaAfinado() { try { ApplyNormNow(); } catch (Exception ex) { LogError("Afinar el local", ex); } }   // al instante en la que suena

        // LAS PAGINAS (las pestanas de Ctrl+F3 de XP): Sonido, Pantalla y Detalles, en este orden al recorrerlas.
        public static readonly string[] PaginasAjustes = { "Sonido", "Imagen", "Fondo", "Pantalla", "Detalles", "Colores" };
        public static string PaginaDe(string grupo)
        {
            if (grupo == "Pantalla") return "Pantalla";
            if (grupo == "El cielo") return "Fondo";
            if (Array.IndexOf(GruposImagen, grupo) >= 0) return "Imagen";
            if (grupo == "De que color es cada cancion" || grupo == "Cuanto azul" || grupo == "Cuanto negro") return "Colores";
            if (grupo == "Al pedir canciones" || grupo == "El fondo y la barra" || grupo == "Internet" || grupo == "Diagnóstico") return "Detalles";
            return "Sonido";
        }
        private static readonly string[] GruposImagen = { "La pared", "Dentro de un disco", "La franja de lo que suena", "En reposo", "Buscador y ayuda", "La atraccion", "Monedas y avisos", "Ajustes y pantalla", "Vidrio y letra" };
        // Una mejora visual de Rocola OS (MejorasVisuales.cs): su interruptor y su explicacion; 'medio' = de coste medio (apagada de fabrica).
        private Ajuste Visual(string grupo, string nombre, string clave, Func<bool> lee, Action<bool> pon, bool medio, string nota)
        {
            Ajuste a = Interruptor(grupo, nombre, clave, lee, pon, false);
            a.Nota = () => nota + (medio ? " Coste medio: apagada de fabrica hasta medirla en el A4." : "");
            return a;
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
            // TU BIBLIOTECA: el boton de repasar a fondo (la tarjeta pone encima la calidad y como va el repaso)
            Ajuste fondo = new Ajuste { Grupo = "Tu biblioteca", Nombre = "Repasar la biblioteca a fondo", Herramienta = true };
            fondo.Valor = delegate { fondo.Nombre = RepasoAFondo ? "Detener el repaso a fondo" : "Repasar la biblioteca a fondo"; return ""; };
            fondo.Cambia = delegate (int d) { PonRepasoAFondo(!RepasoAFondo); };
            fondo.Nota = delegate { return RepasoAFondo ? "Midiendo con los dos nucleos. La musica no se corta; con un video se para." : porRepasar.Count > 0 ? "Mide ya, con los dos nucleos, las canciones que faltan (si no, se miden poco a poco)." : "No falta nada por medir."; };
            fondo.Bloqueo = delegate { return !RepasoAFondo && porRepasar.Count == 0 ? "no falta nada" : null; };
            l.Add(fondo);
            Ajuste iaDat = Interruptor("Tu biblioteca", "Juntar material para la IA", "ia_datos", () => iaHarvestOn, v => iaHarvestOn = v, false);
            iaDat.Nota = delegate { return "Guarda ejemplos para entrenar la IA del DSEE en rocola_ia_datos.bin (hasta " + (iaMaxBytes > 0 ? (iaMaxBytes / (1024L * 1024 * 1024)) + " GB" : "sin tope") + "). Apagado, lo demas se sigue midiendo igual."; };
            l.Add(iaDat);
            l.Add(Interruptor("El local", "Claridad para el local", "claridad_local", () => claridadLocal, v => claridadLocal = v, true));
            l.Add(Interruptor("El local", "Que las partes suaves se oigan", "partes_suaves", () => partesSuaves, v => partesSuaves = v, true));
            l.Add(Interruptor("El local", "Presencia en sala ruidosa", "presencia_sala", () => presenciaSala, v => presenciaSala = v, true));
            l.Add(Interruptor("El local", "Quitar el subgrave", "corta_subgrave", () => cortaSubgrave, v => cortaSubgrave = v, true));
            l.Add(Interruptor("El local", "Estereo mas estrecho", "estereo_estrecho", () => estereoEstrecho, v => estereoEstrecho = v, true));
            l.Add(Interruptor("El local", "Suavizar las eses", "suaviza_eses", () => suavizaEses, v => suavizaEses = v, true));
            l.Add(Interruptor("El local", "Estereo estrecho solo si hace falta (S3)", "estereo_auto", () => estereoAuto, v => estereoAuto = v, true));
            l.Add(TonoDb("Graves del local", "graves_local", () => gravesLocalDb, v => gravesLocalDb = v, "El cuerpo de la musica (120 Hz): mas para llenar el local, menos si retumba."));
            l.Add(TonoDb("Agudos del local", "agudos_local", () => agudosLocalDb, v => agudosLocalDb = v, "El brillo (6 kHz): mas si el local se traga los agudos, menos si silba o cansa."));
            Ajuste bal = new Ajuste { Grupo = "El local", Nombre = "Balance", Clave = "balance" };
            bal.Valor = delegate { return balanceDb == 0 ? "Centrado" : balanceDb < 0 ? "Derecha -" + (-balanceDb) + " dB" : "Izquierda -" + balanceDb + " dB"; };
            bal.Guardado = delegate { return balanceDb.ToString(CultureInfo.InvariantCulture); };
            bal.Cambia = delegate (int d) { balanceDb = Math.Max(-SalaLocal.BalanceMax, Math.Min(SalaLocal.BalanceMax, balanceDb + d)); AplicaAlInstante(); };
            bal.Barra = delegate { return (int)Math.Round((balanceDb + SalaLocal.BalanceMax) * 100f / (2 * SalaLocal.BalanceMax)); };
            bal.Nota = delegate { return "Si una zona suena mas fuerte, baja aqui ese canal (hasta 6 dB). Mejor aun: igualar las perillas IN A e IN B del amplificador."; };
            l.Add(bal);
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
            l.Add(Ciego("Comparar a ciegas: la IA de ahora o la nueva", CiegoIaNueva));   // IA1 (IaNueva.cs)
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
            Ajuste dbc = Interruptor("Mejoras con IA", "DSEE en el billar, solo en las muy cortadas", "dsee_billar_cortes", () => dseeBillarCortes, v => { dseeBillarCortes = v; ApplyNormNow(); }, false);
            dbc.Nota = delegate { return "Con el modo billar, el DSEE solo en canciones cortadas bajo 13 kHz (lo que falta cae donde las cornetas dan)."; };
            dbc.Bloqueo = delegate { return !sbrAllowed ? "este equipo no llega (" + sbrReason + ")" : !LocalAutoAGuardar ? "solo con el modo billar (pagina de Sonido)" : !sbrOn ? "necesita «Reconstruir los agudos (DSEE)» guardado en SI" : null; };
            l.Add(dbc);
            l.Add(Interruptor("Mejoras con IA", "Bajo y brillo a la medida (I1)", "a_la_medida", () => aLaMedida, v => aLaMedida = v, false));
            Ajuste gen = Interruptor("Mejoras con IA", "DSEE con el modelo de su genero (I3)", "dsee_genero", () => dseePorGenero, v => dseePorGenero = v, false);
            gen.Nota = delegate { return "Cada cancion usa el modelo de su genero, si se entreno. Solo con el DSEE encendido; desde la siguiente cancion."; };
            l.Add(gen);
            // IA1: LA IA QUE ENTRENA LA PROPIA ROCOLA (IaNueva.cs, EntrenaIa.cs): herramientas, sin nada guardado en la configuracion.
            Ajuste en = new Ajuste { Grupo = "Mejoras con IA", Nombre = "Entrenar la IA ahora", Herramienta = true };
            en.Valor = delegate { return entrenando ? "ENTRENANDO" : "EMPEZAR"; };
            en.Cambia = delegate (int d) { iaMensaje = EntrenaAhora(); };
            en.Bloqueo = delegate { return entrenando ? "ya esta entrenando" : null; };
            en.Nota = delegate { return (iaMensaje.Length > 0 && !entrenando ? iaMensaje + " " : "") + TextoEntreno(); };
            l.Add(en);
            Ajuste qd = new Ajuste { Grupo = "Mejoras con IA", Nombre = "Quedarse con la IA nueva", Herramienta = true };
            qd.Valor = delegate { return "PONER"; };
            qd.Cambia = delegate (int d) { iaMensaje = QuedateConIaNueva(); };
            qd.Bloqueo = delegate { return !HayIaNueva ? "no hay IA nueva" : !iaNuevaGano ? "antes tiene que ganar a ciegas (Lo que suena ahora)" : null; };
            l.Add(qd);
            Ajuste ds = new Ajuste { Grupo = "Mejoras con IA", Nombre = "Descartar la IA nueva", Herramienta = true };
            ds.Valor = delegate { return "APARTAR"; };
            ds.Cambia = delegate (int d) { iaMensaje = DescartaIaNueva(); };
            ds.Bloqueo = delegate { return !HayIaNueva ? "no hay IA nueva" : null; };
            l.Add(ds);
            // CALIDAD DEL SONIDO (la tarjeta de «Ajustes del sonido» de XP): el EQ automatico, los graves y el realce, cada uno con su
            // «lo decide la rocola» y su barra a mano; con el modo billar los decide el modo
            Ajuste eqA = Interruptor("Calidad del sonido", "EQ automatico: lo decide la rocola", "eq_automatico", () => eqAuto, v => eqAuto = v, true);
            eqA.Nota = delegate { return "Empareja el tono de cada cancion con el de tu biblioteca: mas fuerza cuantas mas canciones medidas."; };
            l.Add(eqA);
            l.Add(Cantidad("Calidad del sonido", "EQ automatico a mano", "eq_auto", () => eqAmount, v => eqAmount = v, true, () => eqAuto, "A mano: la fuerza del EQ, la misma para todas.", true));
            Ajuste baA = Interruptor("Calidad del sonido", "Graves: lo decide la rocola", "bajo_automatico", () => bassAuto, v => bassAuto = v, true);
            baA.Nota = delegate { return "Devuelve el golpe de bajo que el archivo no trae: a cada cancion lo que le falta."; };
            l.Add(baA);
            l.Add(Cantidad("Calidad del sonido", "Graves (bajo profundo) a mano", "bajo", () => bassAmount, v => bassAmount = v, true, () => bassAuto, "A mano: manda la barra.", false));
            l.Add(Cantidad("Calidad del sonido", "Realce de brillo", "realce", () => excAmount, v => excAmount = v, true, () => false, "Devuelve el 'aire' que el MP3 apaga. Se le da mas a las canciones peor grabadas.", false));
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
            vv.Nota = delegate { return normalizeOn ? "Los videos ya medidos suenan al nivel de las canciones; esto vale para los que aun no se midieron." : "Los videos no pasan por la cadena de sonido: los pone el reproductor de videos."; };
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
            // LAS MEJORAS VISUALES DE ROCOLA OS (V1-V30 de la presentacion; las elegidas el 2-11-2026)
            l.Add(Visual("El cielo", "Nebulosas vivas (V1)", "nebulosas_vivas", () => NebulosasVivas, v => NebulosasVivas = v, true, "Las nubes cambian de forma despacio, calculadas en la tarjeta."));
            l.Add(Visual("El cielo", "Aurora que respira con la musica (V2)", "aurora_musica", () => AuroraMusica, v => AuroraMusica = v, false, "La aurora sobre el horizonte, ondulando con los graves y con el color del genero."));
            l.Add(Visual("El cielo", "Estrellas con aire y brillo (V3)", "estrellas_aire", () => EstrellasAire, v => EstrellasAire = v, false, "Halo suave en las grandes; cerca del horizonte, mas tenues, calidas y titilando mas."));
            l.Add(Visual("La pared", "Portadas siempre nitidas (V4)", "portadas_nitidas", () => PortadasNitidas, v => PortadasNitidas = v, false, "Achicadas con filtro de calidad, y la elegida a mas resolucion. Vale para las que se carguen desde ahora."));
            l.Add(Visual("La pared", "El cubo con luz (V5)", "cubo_luz", () => CuboLuz, v => CuboLuz = v, false, "Un brillo recorre las caras del cubo al girar, y la elegida lleva su sombra en el piso."));
            l.Add(Visual("Dentro de un disco", "La portada, de vidrio esmerilado (V7)", "disco_vidrio", () => DiscoVidrio, v => DiscoVidrio = v, true, "Detras de la lista, la portada en grande y desenfocada."));
            l.Add(Visual("Dentro de un disco", "La lista con los colores del disco (V8)", "lista_colores", () => ListaColores, v => ListaColores = v, false, "La fila elegida y la barra con el color de la portada."));
            l.Add(Visual("Dentro de un disco", "El ecualizador, de verdad (V9)", "eq_real", () => EqReal, v => EqReal = v, false, "Las barritas de la que suena leen el sonido real que sale por las bocinas."));
            l.Add(Visual("La franja de lo que suena", "La barra con la forma de la cancion (V10)", "barra_forma", () => BarraForma, v => BarraForma = v, false, "La barra de avance dibuja el volumen de la cancion de principio a fin."));
            l.Add(Visual("La franja de lo que suena", "Espectro sobre el horizonte (V11)", "espectro_franja", () => EspectroFranja, v => EspectroFranja = v, false, "Barras de las 16 bandas del sonido, al compas de las bocinas, con sus picos."));
            l.Add(Visual("La franja de lo que suena", "Cambio de cancion con transicion (V12)", "titulo_transicion", () => TituloTransicion, v => TituloTransicion = v, false, "El titulo nuevo entra deslizandose mientras el viejo se va."));
            l.Add(Visual("En reposo", "Portada grande con luz ambiental (V13)", "reposo_luz", () => ReposoLuz, v => ReposoLuz = v, true, "En el estado extendido, la portada derrama su color por la pantalla."));
            l.Add(Visual("En reposo", "Un anillo que late con la musica (V14)", "anillo_musica", () => AnilloMusica, v => AnilloMusica = v, true, "Alrededor de la portada grande, un anillo de luz que crece con los golpes."));
            l.Add(Visual("En reposo", "La cola en carrusel (V15)", "cola_carrusel", () => ColaCarrusel, v => ColaCarrusel = v, true, "Las siguientes, en un carrusel de portadas con perspectiva."));
            l.Add(Visual("Buscador y ayuda", "Teclado que se siente (V16)", "teclado_vivo", () => TecladoVivo, v => TecladoVivo = v, false, "La tecla se hunde y brilla; en los resultados se iluminan las letras que coinciden."));
            l.Add(Visual("Buscador y ayuda", "Resultados en cascada (V17)", "resultados_cascada", () => ResultadosCascada, v => ResultadosCascada = v, false, "Cada resultado entra un instante despues del anterior."));
            l.Add(Visual("Buscador y ayuda", "La estrella con volumen y luz (V18)", "estrella_luz", () => EstrellaLuz, v => EstrellaLuz = v, false, "La asistente sombreada, con chispas y sombra en el piso. Sin cara."));
            l.Add(Visual("La atraccion", "Portadas de cine (V19)", "atraccion_cine", () => AtraccionCine, v => AtraccionCine = v, false, "Acercamientos lentos sobre las portadas de la atraccion."));
            l.Add(Visual("La atraccion", "Mosaico de lo mas pedido (V20)", "mosaico_exitos", () => MosaicoExitos, v => MosaicoExitos = v, true, "Una pantalla mas en la atraccion: las portadas de lo mas pedido volteandose una a una."));
            l.Add(Visual("La atraccion", "Pasos sin cortes (V21)", "atraccion_fundidos", () => AtraccionFundidos, v => AtraccionFundidos = v, false, "Entre una pantalla y otra de la atraccion, un fundido en vez de un salto."));
            l.Add(Visual("Monedas y avisos", "Una moneda de verdad (V22)", "moneda_metal", () => MonedaMetal, v => MonedaMetal = v, false, "La moneda que cae, con brillo de metal y un rebote al llegar."));
            l.Add(Visual("Monedas y avisos", "Chispas al poner una cancion (V23)", "chispas_pedir", () => ChispasPedir, v => ChispasPedir = v, false, "Chispas del color del genero que vuelan hasta la cola."));
            Ajuste v24 = Visual("Monedas y avisos", "Avisos de vidrio (V24)", "avisos_vidrio", () => AvisosVidrio, v => AvisosVidrio = v, true, "Los avisos sobre un vidrio que desenfoca lo de atras.");
            v24.Bloqueo = () => VidrioAvisos ? "lo hace «Avisos de vidrio nuevos» (G10), que esta encendido" : null;
            l.Add(v24);
            l.Add(Visual("Ajustes y pantalla", "Ver el cambio mientras se elige (V28)", "ajustes_vista_previa", () => AjustesVistaPrevia, v => AjustesVistaPrevia = v, false, "En las hojas Imagen, Fondo y Colores la ventana se hace transparente para ver el cambio detras."));
            // VIDRIO Y LETRA (3-11-2026): la letra nueva y las ventanas de vidrio (G1-G11)
            Ajuste letra = Visual("Vidrio y letra", "Letra nueva (L1)", "letra_nueva", () => LetraNueva, v => LetraNueva = v, false, "Outfit en los titulos y Plus Jakarta Sans en el texto, en toda la rocola. Apagado: las letras de XP.");
            letra.Bloqueo = () => LetraNuevaFalta && LetraNueva ? "faltan los archivos de la letra en /usr/share/rocola/letras" : null;
            l.Add(letra);
            l.Add(Visual("Vidrio y letra", "Cabecera de vidrio (G1)", "vidrio_cabecera", () => VidrioCabecera, v => VidrioCabecera = v, false, "La leyenda, BUSCAR, AYUDA y la letra sobre una placa de vidrio que deja ver el cielo."));
            l.Add(Visual("Vidrio y letra", "Barra de letras de vidrio (G2)", "vidrio_letras", () => VidrioLetras, v => VidrioLetras = v, false, "La barra de las letras de vidrio; la elegida, una pastilla clara que brilla."));
            l.Add(Visual("Vidrio y letra", "Lo que suena en vidrio (G3)", "vidrio_suena", () => VidrioSuena, v => VidrioSuena = v, false, "Abajo, lo que suena en una barra de vidrio teñida del color del disco."));
            l.Add(Visual("Vidrio y letra", "Ficha rapida de vidrio (G4)", "vidrio_ficha", () => VidrioFicha, v => VidrioFicha = v, true, "La ficha del disco desenfoca de verdad las portadas de detras y toma el color del disco."));
            l.Add(Visual("Vidrio y letra", "Las canciones en vidrio (G5)", "vidrio_disco", () => VidrioDisco, v => VidrioDisco = v, false, "Dentro de un disco, la hoja de canciones es de vidrio teñido con la portada."));
            l.Add(Visual("Vidrio y letra", "Reposo en vidrio (G6)", "vidrio_reposo", () => VidrioReposo, v => VidrioReposo = v, false, "En reposo, lo que suena va en una tarjeta grande de vidrio."));
            l.Add(Visual("Vidrio y letra", "Buscador de vidrio (G7)", "vidrio_buscador", () => VidrioBuscador, v => VidrioBuscador = v, false, "La caja, el teclado y los resultados sobre vidrio."));
            l.Add(Visual("Vidrio y letra", "Atraccion en vidrio (G8)", "vidrio_atraccion", () => VidrioAtraccion, v => VidrioAtraccion = v, false, "El nombre del disco y los mensajes de la atraccion sobre placas de vidrio."));
            l.Add(Visual("Vidrio y letra", "Ajustes de vidrio (G9)", "vidrio_ajustes", () => VidrioAjustes, v => VidrioAjustes = v, true, "Esta ventana, de vidrio oscuro que deja ver la pared detras, borrosa (no en Colores, Imagen ni Fondo con V28)."));
            l.Add(Visual("Vidrio y letra", "Avisos de vidrio nuevos (G10)", "vidrio_avisos", () => VidrioAvisos, v => VidrioAvisos = v, true, "Todos los avisos sobre vidrio que desenfoca lo de detras, con su color."));
            l.Add(Visual("Vidrio y letra", "Globo de ayuda de vidrio (G11)", "vidrio_ayuda", () => VidrioAyuda, v => VidrioAyuda = v, true, "El globo de la estrella asistente, de vidrio dorado."));
            l.Add(Visual("Ajustes y pantalla", "A la resolucion del monitor (V30)", "resolucion_nativa", () => ResolucionNativa, v => ResolucionNativa = v, false, "Dibuja a la resolucion real de la pantalla, sin estirar. Apagado: 1024x768 si el monitor la ofrece (menos trabajo para el A4). Vale al volver a encender la rocola."));
            // LA HOJA DETALLES DE XP (PaginaDetalles): al pedir canciones, y el fondo y la barra (la estrella del cielo sigue en Pantalla)
            { Ajuste x = Interruptor("Al pedir canciones", "Decir cuando suena tu cancion", "turno", () => Turno, v => Turno = v, false); x.Nota = () => "Al pedir una cancion, el aviso dice que puesto ocupa y cuantos minutos faltan, en vez de solo «Cancion agregada»."; l.Add(x); }
            { Ajuste x = Interruptor("Al pedir canciones", "Marcar los discos que ya pediste", "marca_cola", () => MarcaCola, v => MarcaCola = v, false); x.Nota = () => "Un palomito verde en la esquina de los discos de los que ya hay algo pedido o sonando, para no volver a pedir lo mismo."; l.Add(x); }
            { Ajuste x = Interruptor("Al pedir canciones", "Ensenar las portadas de la cola", "cola_portadas", () => ColaPortadas, v => ColaPortadas = v, false); x.Nota = () => "En la pastilla de «En cola», las caratulas de las tres proximas. Si hay mas de tres, la ultima lleva un «+2»."; l.Add(x); }
            { Ajuste x = Interruptor("El fondo y la barra", "Que el fondo tome el color de la portada", "fondo_portada", () => FondoPortada, v => FondoPortada = v, false); x.Nota = () => "Del disco se toma SOLO de que color es. Lo oscuro y lo vivo lo sigue mandando el genero: ninguna portada estropea la pantalla."; l.Add(x); }
            { Ajuste x = Interruptor("El fondo y la barra", "Media vuelta al cambiar de cancion", "relevo", () => Relevo, v => Relevo = v, false); x.Nota = () => "El cubito de la barra de abajo gira media vuelta y la portada nueva aparece en el camino, en vez de cambiar de golpe."; l.Add(x); }
            // LA HOJA COLORES DE XP (PaginaColores): tres botones cada una, con sus valores
            l.Add(Opciones("De que color es cada cancion", "El tono de la portada", "portada_manda", new[] { "Disco 20%", "Mitad y mitad", "Disco 100%" }, new[] { 20, 50, 100 }, () => PortadaManda, v => PortadaManda = v,
                "El tono del disco pesa esto en el color del cielo; el resto viene del genero. Sin genero reconocido manda todo el tono del disco. Se conservan el brillo y la viveza del cielo."));
            l.Add(Opciones("Cuanto azul", "El azul del medio del cielo", "cielo_azul", new[] { "Poco azul", "Normal", "Todo azul" }, new[] { 35, 70, 100 }, () => CieloAzul, v => CieloAzul = v,
                "El medio del cielo es azul noche y casi no depende de la cancion: es lo que hace que parezca cielo. Con menos azul, el color de la cancion sube desde el horizonte."));
            l.Add(Opciones("Cuanto negro", "Donde acaba el negro de arriba", "cielo_negro", new[] { "Mas negro", "Normal", "Menos negro" }, new[] { 58, 42, 30 }, () => CieloNegro, v => CieloNegro = v,
                "Arriba el cielo es casi negro y ahi se ven mejor las estrellas: mas negro deja una noche mas cerrada, menos negro una pantalla mas encendida."));
            // INTERNET (1-11-2026): actualizarse solo desde GitHub (rocola-actualiza, en el sistema) y buscar ya. El recuadro de abajo dice la
            // version, la red (con la MAC, la que pide el router para el encendido remoto) y como va la actualizacion.
            l.Add(Interruptor("Internet", "Actualizarse sola desde internet", "actualizar_solo", () => ActualizarSolo, v => ActualizarSolo = v, false));
            Ajuste ya = new Ajuste { Grupo = "Internet", Nombre = "Buscar actualización ahora", Herramienta = true };
            ya.Valor = delegate { return File.Exists("/run/rocola-buscar-ya") ? "BUSCANDO" : "BUSCAR"; };
            ya.Cambia = delegate (int d) { try { File.WriteAllText("/run/rocola-buscar-ya", ""); } catch { } };
            ya.Bloqueo = delegate { return ActualizarSolo ? null : "necesita «Actualizarse sola»"; };
            l.Add(ya);
            // SPOTIFY (decision del dueno del 6-11-2026, Spotify.cs): para las reuniones familiares, apagado de fabrica. El recuadro de abajo dice
            // como conectarse y lo que suena.
            { Ajuste x = Interruptor("Spotify", "Spotify en la rocola (reuniones familiares)", "spotify", () => SpotifyOn, v => SpotifyOn = v, false);
              x.Nota = () => "Solo para la familia: Spotify no permite ponerlo para el publico de un negocio. Necesita Spotify Premium."; l.Add(x); }
            // LO QUE SPOTIFY ANADE (SpotifyMas.cs, SP1-SP11): cada uno con su interruptor, encendidos, y en gris sin el modo Spotify
            Func<string> sinSpotify = () => SpotifyOn ? null : "necesita «Spotify en la rocola»";
            { Ajuste x = Interruptor("Spotify", "Tus canciones dañadas suenan desde Spotify (sin que se note)", "calidad_spotify", () => CalidadSpotify, v => CalidadSpotify = v, false);
              x.Bloqueo = sinSpotify; x.Nota = () => "Las tuyas con los agudos cortados suenan con la version de Spotify (320 kbps) si es la misma grabacion. En la pantalla no cambia nada; si Spotify falla, suena tu MP3."; l.Add(x); }
            { Ajuste x = Interruptor("Spotify", "Letras en el estado extendido", "letras_spotify", () => LetrasSpotify, v => LetrasSpotify = v, false);
              x.Bloqueo = sinSpotify; x.Nota = () => "La letra de lo que suena (de Spotify, o tuya si tiene pareja en Spotify), al estilo de Apple Music, cuando nadie toca la rocola."; l.Add(x); }
            { Ajuste x = Interruptor("Spotify", "Portadas y generos que faltan", "portadas_spotify", () => PortadasSpotify, v => PortadasSpotify = v, false);
              x.Bloqueo = sinSpotify; x.Nota = () => "Tus discos sin caratula toman la de Spotify, y tus canciones sin genero, el del artista. Tus archivos no se tocan."; l.Add(x); }
            { Ajuste x = Interruptor("Spotify", "Completar tus discos", "completar_discos", () => CompletarDiscos, v => CompletarDiscos = v, false);
              x.Bloqueo = sinSpotify; x.Nota = () => "Las canciones que le faltan a un disco tuyo salen al final, marcadas «Spotify», con su credito."; l.Add(x); }
            { Ajuste x = Interruptor("Spotify", "La foto del artista en el estado extendido", "artista_fondo", () => ArtistaFondo, v => ArtistaFondo = v, false);
              x.Bloqueo = sinSpotify; x.Nota = () => "De fondo, tenue, la foto del artista que suena, y el ano del disco."; l.Add(x); }
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
            Ajuste fab = new Ajuste { Grupo = "Dejar como de fabrica", Nombre = "Dejar como de fabrica", Herramienta = true };
            fab.Valor = delegate { return unchecked(Environment.TickCount - fabricaPide) < 5000 && fabricaPide != 0 ? "PONER OTRA VEZ" : ""; };
            fab.Cambia = delegate (int d)
            {
                if (fabricaPide != 0 && unchecked(Environment.TickCount - fabricaPide) < 5000) { fabricaPide = 0; DejaDeFabrica(); }
                else fabricaPide = Environment.TickCount | 1;
            };
            fab.Nota = delegate { return "Todos los ajustes de esta ventana vuelven a como vienen de fabrica (lo aprendido de cada cancion no se toca). Pide confirmar: PONER otra vez."; };
            l.Add(fab);
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
            foreach (string pg in PaginasAjustes)
            {
                List<string> gs = new List<string>();   // y dentro de la pagina, tarjeta por tarjeta (asi se recorre en el orden en que se ven)
                foreach (Ajuste x in l) if (PaginaDe(x.Grupo) == pg && !gs.Contains(x.Grupo)) gs.Add(x.Grupo);
                foreach (string g in gs) foreach (Ajuste x in l) if (x.Grupo == g) orden.Add(x);
            }
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
