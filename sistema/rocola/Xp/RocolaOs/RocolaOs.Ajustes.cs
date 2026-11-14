// LOS AJUSTES DE ROCOLA OS: rocola_os.txt, en la carpeta de datos de rocola-xp (Form1.CarpetaDatos: ROCOLA_DATOS, /rocola/datos/xp en el
// sistema). No van en rocola_config.txt porque XP lo reescribe ENTERO con solo sus claves (Form1.Datos.cs, TextoConfig, desde
// SaveConfigPrefs y el guardado al momento de Ctrl+F3 y Ctrl+F2): una clave de Rocola OS escrita alli se borraria en el primer guardado.
// rocola_config.txt, el importador de XP y las «dir=» del lanzador no cambian.
//
// EL ARCHIVO: un renglon «clave=valor» por ajuste, en UTF-8 sin marca y con finales LF (lo leen tambien los guiones del sistema con
// BusyBox: «sed -n 's/^actualizar_solo=//p'»). Solo lleva lo que alguien cambio: lo que no esta vale lo de fabrica de su funcion (asi un
// valor de fabrica que cambie en una version nueva llega solo, salvo donde el dueno ya eligio). Se conserva todo lo que el programa no
// conoce, en su sitio y en su orden: las claves de otras versiones, los comentarios («#...») y los renglones raros. De una clave conocida que
// salga repetida vale la ultima, y al cambiarla se queda una sola (donde estaba la primera). NUNCA van llaves aqui (el buzon sube este
// archivo con los registros).
//
// CADA FUNCION DECLARA SUS CLAVES (RocolaOsFuncion.Claves, con RocolaOsClave.SiNo, Entero o Texto: su valor de fabrica y que valores
// valen) y las lee y las cambia con Ajuste, AjusteSiNo, AjusteEntero y PonAjuste: no hay un registro central que tocar. La clave de cada
// funcion (su nombre: encendida o no con ROCOLA_OS=1) la declara Registra. Leer es desde cualquier hilo y sin cerrojos (una foto que se
// cambia entera: el hilo del sonido nunca espera); cambiar, desde cualquiera (un cerrojo corto, nada de disco dentro).
//
// SE GUARDA como lo de XP: con su EscribeSeguro (el de paso, el .bak y el cambio de nombre: un apagon no deja el archivo a medias) por su
// GuardaDetras (el trabajo cuenta para WaitDataWrites, asi que el cierre espera a que acabe), solo si hubo cambios y con lo ultimo. Se lee
// con LeeLineasSeguro de XP (si el bueno falta o quedo vacio, el .bak).
//
// EL RESPALDO DIARIO (lo de XP, BackupLearnedData, lleva una lista fija de sus tablas que no se toca): rocola_os.txt va en su propia
// carpeta del respaldo de XP (respaldo/rocola-os/<fecha>, en ROCOLA_IA_DATOS si esta: Nativo/DatosGrandes.cs; y en respaldo_dir= si hay),
// con las mismas funciones de XP (BackupTo y PruneBackups: las 3 copias mas nuevas; el PruneBackups de XP no la ve, no es una fecha). Una
// vez al arrancar (lo de la sesion anterior, como XP en SigueArranque) y otra al cambiar el dia del negocio (7 AM).
//
// LA ORDEN «ajuste» DEL BUZON (decision del dueno del 14-11-2026: mientras no este la ventana de Ctrl+F4, los ajustes se cambian desde casa):
// el buzon deja /run/rocola-ajuste y el latido de 1a se la da a AjusteDeFuera, que solo acepta claves declaradas, valida el valor, lo aplica
// y lo guarda. El programa es el dueno del archivo: nadie mas lo escribe mientras corre (a mano, desde la consola de mantenimiento, con la
// rocola cerrada).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Rocola
{
    // UNA CLAVE DE rocola_os.txt: la declara su funcion (RocolaOsFuncion.Claves)
    internal sealed class RocolaOsClave
    {
        internal readonly string Nombre;      // la clave («calidad_spotify»): minusculas, cifras, «_», «-», «.»
        internal readonly string DeFabrica;   // lo que vale si no esta en el archivo (o si lo que dice no vale)
        internal readonly string Que;         // una linea: que hace (la orden «ajuste» la dice)
        internal readonly string Tipo;        // que valores valen, dicho para el dueno («1 o 0», «de 0 a 100», «texto»)
        internal readonly bool EnVivo;        // cambia sin reiniciar el programa (su funcion la lee cada vez o atiende AlCambiar)
        internal string Duena;                // la funcion que la declara (lo pone RocolaOs.Registra)
        private readonly Func<string, bool> vale;

        private RocolaOsClave(string nombre, string deFabrica, string tipo, Func<string, bool> vale, bool enVivo, string que)
        { Nombre = nombre; DeFabrica = deFabrica; Tipo = tipo; this.vale = vale; EnVivo = enVivo; Que = que; }

        // si un valor vale para esta clave: una sola linea, hasta 1 000 caracteres, y lo que diga su tipo
        internal bool Vale(string v)
        {
            if (v == null || v.Length > 1000 || v.IndexOf('\r') >= 0 || v.IndexOf('\n') >= 0) return false;
            try { return vale == null || vale(v); } catch { return false; }
        }
        // un interruptor: «1» o «0»
        internal static RocolaOsClave SiNo(string nombre, bool deFabrica, bool enVivo, string que)
        { return new RocolaOsClave(nombre, deFabrica ? "1" : "0", "1 o 0", delegate (string v) { return v == "1" || v == "0"; }, enVivo, que); }
        // un numero entero de minimo a maximo (las cifras de siempre, sin signo de mas ni espacios)
        internal static RocolaOsClave Entero(string nombre, int deFabrica, int minimo, int maximo, bool enVivo, string que)
        {
            return new RocolaOsClave(nombre, deFabrica.ToString(CultureInfo.InvariantCulture),
                "un numero de " + minimo.ToString(CultureInfo.InvariantCulture) + " a " + maximo.ToString(CultureInfo.InvariantCulture),
                delegate (string v)
                {
                    int n;
                    return int.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n) && n >= minimo && n <= maximo
                        && n.ToString(CultureInfo.InvariantCulture) == v;
                }, enVivo, que);
        }
        // un texto de una linea (con vale = null, cualquiera; si no, lo que diga vale)
        internal static RocolaOsClave Texto(string nombre, string deFabrica, Func<string, bool> vale, bool enVivo, string que)
        { return new RocolaOsClave(nombre, deFabrica ?? "", "texto", vale, enVivo, que); }
    }

    internal static partial class RocolaOs
    {
        internal const string ArchivoAjustes = "rocola_os.txt";

        // ---------- las claves declaradas (las pone Registra; se cambia entero) ----------
        private static Dictionary<string, RocolaOsClave> claves = new Dictionary<string, RocolaOsClave>(StringComparer.Ordinal);
        internal static RocolaOsClave ClaveDe(string nombre)
        {
            RocolaOsClave c;
            return nombre != null && Volatile.Read(ref claves).TryGetValue(nombre, out c) ? c : null;
        }
        // todas, por orden de nombre (para la respuesta de la orden «ajuste» y las pruebas)
        internal static string[] NombresDeClaves()
        {
            List<string> l = new List<string>(Volatile.Read(ref claves).Keys);
            l.Sort(StringComparer.Ordinal);
            return l.ToArray();
        }

        // ---------- lo que dice el archivo: una foto que no cambia (cada cambio pone otra) ----------
        private sealed class Foto
        {
            internal readonly string[] Lineas;                 // los renglones, tal cual (sin el fin de linea)
            internal readonly Dictionary<string, int> Donde;   // clave -> su ultimo renglon (el que vale)
            internal readonly long Version;                    // 0 = la leida; cada cambio, una mas
            internal Foto(string[] lineas, long version)
            {
                Lineas = lineas; Version = version;
                Donde = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < lineas.Length; i++) { string k = ClaveDeLinea(lineas[i]); if (k != null) Donde[k] = i; }
            }
            internal string Texto()
            {
                StringBuilder sb = new StringBuilder();
                foreach (string l in Lineas) sb.Append(l).Append('\n');
                return sb.ToString();
            }
        }
        private static Foto foto;
        private static readonly object cerrojoAjustes = new object();
        private static string ajustesProblema;
        private static string AjustesProblema { get { return ajustesProblema; } }

        // la clave de un renglon «clave=valor» (null si es un comentario, esta vacio o no tiene forma de clave: se conserva tal cual)
        private static string ClaveDeLinea(string l)
        {
            if (l == null) return null;
            int i = l.IndexOf('=');
            if (i <= 0) return null;
            string k = l.Substring(0, i).Trim();
            return NombreValido(k) ? k : null;
        }
        private static string ValorDeLinea(string l) { int i = l.IndexOf('='); return i < 0 ? "" : l.Substring(i + 1).Trim(); }

        private static Foto LaFoto { get { return Volatile.Read(ref foto) ?? CargaAjustes(); } }
        private static Foto CargaAjustes()
        {
            lock (cerrojoAjustes)
            {
                if (foto != null) return foto;
                string[] l = null;
                try { l = Form1.RocolaOsLeeLineas(Datos(ArchivoAjustes)); }
                catch (Exception ex) { ajustesProblema = "no se pudo leer " + ArchivoAjustes + " (" + ex.GetType().Name + ": " + ex.Message + "): valen los de fabrica"; }
                Foto f = new Foto(l ?? new string[0], 0);
                Volatile.Write(ref foto, f);
                return f;
            }
        }

        // ---------- leer ----------
        // EL VALOR DE UNA CLAVE: el de rocola_os.txt si esta y vale, o el de fabrica. Sin ROCOLA_OS, siempre el de fabrica (no se lee nada).
        // Una clave sin declarar: lo que diga el archivo, o null.
        internal static string Ajuste(string clave)
        {
            RocolaOsClave c = ClaveDe(clave);
            if (!Activo || clave == null) return c != null ? c.DeFabrica : null;
            Foto f = LaFoto;
            int i;
            if (f.Donde.TryGetValue(clave, out i))
            {
                string v = ValorDeLinea(f.Lineas[i]);
                if (c == null || c.Vale(v)) return v;
                AvisaUnaVez("en " + ArchivoAjustes + ", «" + clave + "=" + v + "» no vale (" + c.Tipo + "): vale el de fabrica, «" + c.DeFabrica + "»");
            }
            return c != null ? c.DeFabrica : null;
        }
        internal static bool AjusteSiNo(string clave) { return Ajuste(clave) == "1"; }
        internal static int AjusteEntero(string clave)
        {
            int n;
            return int.TryParse(Ajuste(clave), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n) ? n : 0;
        }
        // si el dueno la cambio (esta en el archivo y vale)
        internal static bool AjusteCambiado(string clave)
        {
            if (!Activo || clave == null) return false;
            Foto f = LaFoto; int i;
            RocolaOsClave c = ClaveDe(clave);
            return f.Donde.TryGetValue(clave, out i) && (c == null || c.Vale(ValorDeLinea(f.Lineas[i])));
        }

        // ---------- cambiar ----------
        // CAMBIA UNA CLAVE DECLARADA y la guarda detras. valor vacio (o null): vuelve a la de fabrica (se quita su renglon). false si la clave
        // no esta declarada o el valor no vale; sin ROCOLA_OS no hace nada (false). Desde cualquier hilo.
        internal static bool PonAjuste(string clave, string valor)
        {
            if (!Activo) return false;
            RocolaOsClave c = ClaveDe(clave);
            if (c == null) { AvisaUnaVez("PonAjuste de una clave sin declarar: «" + clave + "» (no se guarda)"); return false; }
            valor = (valor ?? "").Trim();
            if (valor.Length > 0 && !c.Vale(valor)) return false;
            lock (cerrojoAjustes)
            {
                Foto f = LaFoto;
                List<int> donde = new List<int>();
                for (int i = 0; i < f.Lineas.Length; i++) if (ClaveDeLinea(f.Lineas[i]) == clave) donde.Add(i);
                if (valor.Length == 0 && donde.Count == 0) return true;                                            // ya era la de fabrica
                if (donde.Count == 1 && valor.Length > 0 && ValorDeLinea(f.Lineas[donde[0]]) == valor) return true;   // ya era esa
                List<string> l = new List<string>(f.Lineas.Length + 1);
                for (int i = 0; i < f.Lineas.Length; i++)
                {
                    if (donde.Count > 0 && i == donde[0] && valor.Length > 0) { l.Add(clave + "=" + valor); continue; }   // (en el sitio de la primera)
                    if (donde.Contains(i)) continue;                                                                     // (las demas, fuera)
                    l.Add(f.Lineas[i]);
                }
                if (donde.Count == 0) l.Add(clave + "=" + valor);
                Volatile.Write(ref foto, new Foto(l.ToArray(), f.Version + 1));
            }
            Guarda();
            return true;
        }

        // LA ORDEN «ajuste» DEL BUZON: «clave=valor» (valor vacio: la de fabrica). Solo claves declaradas por las funciones de Rocola OS. Se
        // valida, se guarda y se le avisa a su funcion (AlCambiar, si esta encendida y la clave cambia sin reiniciar). En el hilo de la pantalla
        // (1a la llama desde el latido). Devuelve la respuesta para el buzon: una linea que empieza por «bien:» o «mal:».
        internal static string AjusteDeFuera(string linea)
        {
            if (!Activo) return "mal: Rocola OS esta apagado (sin ROCOLA_OS)";
            linea = (linea ?? "").Trim();
            int p = linea.IndexOf('=');
            if (p <= 0) return "mal: se espera «clave=valor» (valor vacio: el de fabrica)";
            string clave = linea.Substring(0, p).Trim(), valor = linea.Substring(p + 1).Trim();
            RocolaOsClave c = ClaveDe(clave);
            if (c == null) return "mal: «" + clave + "» no es un ajuste de Rocola OS (los que hay: " + string.Join(", ", NombresDeClaves()) + ")";
            if (valor.Length > 0 && !c.Vale(valor)) return "mal: «" + valor + "» no vale para «" + clave + "» (" + c.Tipo + ")";
            string antes = Ajuste(clave);
            if (!PonAjuste(clave, valor)) return "mal: no se pudo cambiar «" + clave + "»";
            string ahora = Ajuste(clave);
            bool deLaFuncion = c.Nombre == c.Duena;   // encender o apagar una funcion: se decide al arrancar
            RocolaOsFuncion f = Busca(c.Duena);
            Form1 r = LaRocola;
            if (!deLaFuncion && ahora != antes && f != null && f.AlCambiar != null && r != null && Encendida(f.Nombre))
            {
                try { f.AlCambiar(r, clave); }
                catch (Exception ex) { Falla(f.Nombre, "AlCambiar(" + clave + ")", ex); }
            }
            Apunta("andamio", "ajuste de fuera: " + clave + "=" + ahora + " (antes " + antes + ")");
            return "bien: " + clave + "=" + ahora + (valor.Length == 0 ? " (el de fabrica)" : "")
                + (deLaFuncion || !c.EnVivo ? " (vale desde el siguiente arranque del programa)" : "");
        }

        // ---------- guardar ----------
        private static readonly object cerrojoGuardado = new object();
        private static long versionEscrita;        // la de la foto que esta en el disco (con cerrojoGuardado)
        private static int guardadoEncargado;      // hay un guardado encargado que aun no empieza
        private static bool guardarAlArrancar;     // se cambio algo antes de tener la rocola (su GuardaDetras)

        private static void Guarda()
        {
            Form1 r = LaRocola;
            if (r == null) { guardarAlArrancar = true; return; }
            if (Interlocked.Exchange(ref guardadoEncargado, 1) != 0) return;   // (ya hay uno que empezara con lo ultimo)
            try { r.RocolaOsGuardaDetras(GuardaYa); }
            catch (Exception ex) { Interlocked.Exchange(ref guardadoEncargado, 0); Falla("andamio", "guardar " + ArchivoAjustes, ex); }
        }
        // escribe la foto de ahora si no es la que esta en el disco (con XP: EscribeSeguro). La llama el trabajo de GuardaDetras, y las pruebas.
        internal static void GuardaYa()
        {
            Interlocked.Exchange(ref guardadoEncargado, 0);
            try
            {
                lock (cerrojoGuardado)   // (uno detras de otro: el que escribe despues escribe lo mas nuevo)
                {
                    Foto f = Volatile.Read(ref foto);
                    if (f == null || f.Version <= versionEscrita) return;
                    Form1.RocolaOsEscribeSeguro(Datos(ArchivoAjustes), f.Texto());
                    versionEscrita = f.Version;
                }
            }
            catch (Exception ex) { Falla("andamio", "guardar " + ArchivoAjustes, ex); }
        }

        // ---------- al arrancar y en cada latido: lo pendiente y el respaldo ----------
        private static DateTime diaRespaldado;
        private static void DespuesDeArrancarLosAjustes(Form1 r)
        {
            if (guardarAlArrancar) { guardarAlArrancar = false; Guarda(); }
            diaRespaldado = Form1.RocolaOsDiaDelNegocio();
            r.RocolaOsRespalda(ArchivoAjustes);
        }
        private static void LatidoDelAndamio(Form1 r)
        {
            DateTime d = Form1.RocolaOsDiaDelNegocio();
            if (d == diaRespaldado) return;
            diaRespaldado = d;
            r.RocolaOsRespalda(ArchivoAjustes);
        }
    }
}
