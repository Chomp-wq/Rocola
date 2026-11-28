// LO PROPIO DE ROCOLA OS ENCIMA DE LA ROCOLA DE XP (fase 11, grupo andamio; PLAN_FASE11.md §2.2-2.4; como se usa: FASE11_andamio.md).
// No es de XP: es la base sobre la que cada grupo de la fase 11 pone lo suyo, en SUS archivos de RocolaOs/, sin tocar los de los demas.
//
// EL INTERRUPTOR GENERAL, ROCOLA_OS (lo pone /usr/bin/rocola). Se lee UNA vez, la primera vez que alguien pregunta (a mas tardar en
// RocolaOs.Arranca; antes, si un gancho pregunta Activo o Pedida en el constructor de Form1, como el envoltorio del cierre):
//   sin la variable, vacia o «0»   nada de la fase 11: rocola-xp es la de XP tal cual (ni latido, ni bitacora, ni rocola_os.txt: no se
//                                  lee ni se escribe nada; cada gancho devuelve lo de XP)
//   «1»                            cada funcion registrada segun su clave en rocola_os.txt (la clave es su nombre: «red=0» la apaga), o su
//                                  valor de fabrica si no esta
//   una lista («vida,red»)         solo esas, para las pruebas: rocola_os.txt no las enciende ni las apaga (sus demas ajustes si valen)
// Cualquier otro valor se toma como lista (y la bitacora lo dice).
//
// UNA FUNCION (RocolaOsFuncion) se registra SOLA, al cargarse el programa, con un [ModuleInitializer] de su propio archivo que solo llama a
// RocolaOs.Registra: su nombre, sus claves y sus delegados. Nada mas (el registro corre siempre, tambien sin ROCOLA_OS, antes del Main de XP:
// ni archivos, ni la variable, ni Form1). Con ROCOLA_OS, RocolaOs.Arranca (gancho g21 de Nativo/PantallaRocola.Rocola.cs, en el hilo de la
// pantalla, antes de ensenarse la ventana) cierra el registro, decide cuales van, llama al Arranca de cada encendida y pone el latido: un
// reloj de la pantalla de 1 s con nombre fijo (RocolaOs:latido) que llama al Latido de cada una. Al cerrarse la rocola, el envoltorio del
// cierre (gancho g22 de Nativo/Form1.Ventana.cs) llama a SeCierra con la razon ANTES de que XP guarde (RocolaOs.Cierre.cs).
//
// Lo demas del andamio: los ajustes de rocola_os.txt (RocolaOs.Ajustes.cs), los hilos, los encargos, las rutas y la bitacora
// (RocolaOs.Hilos.cs), lo que se hace dentro de Form1 (RocolaOs.Form1.cs: el envoltorio, el guardado y el respaldo) y las ordenes de la
// linea de mandatos (RocolaOs.Ordenes.cs). Las pruebas del andamio: RocolaOs/PruebaAndamio.cs y Pruebas/RocolaOs/andamio/.
//
// LAS REGLAS (PLAN_FASE11.md §2.2 y §2.4): en las partes de Form1, todos los nombres empiezan por RocolaOs, sin inicializadores de campo con
// efectos y sin constructores estaticos que trabajen. En el hilo de la pantalla solo el latido y los encargos con nombre fijo «RocolaOs:...»;
// nada en el reposo (PantallaRocola.Libre: una excepcion ahi cierra el programa), nada de await, ningun reloj propio y nada de OpenGL fuera de
// el. Lo que espera (disco, red, procesos) va en hilos propios (RocolaOs.Hilo), que lo atrapan todo; el estado de XP se lee solo en el hilo
// de la pantalla (a otros hilos, una copia).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Rocola
{
    // UNA FUNCION DE ROCOLA OS: lo que cada grupo registra. Todos los delegados son opcionales; los de la pantalla corren en su hilo, cada uno
    // protegido (lo que lancen se apunta y la rocola sigue).
    internal sealed class RocolaOsFuncion
    {
        internal string Nombre;           // la palabra de ROCOLA_OS y su clave en rocola_os.txt («red», «vida»...): minusculas, cifras, «_», «-», «.»
        internal string Que;              // una linea: que hace (la bitacora y la orden «ajuste» la dicen)
        internal bool DeFabrica;          // con ROCOLA_OS=1, encendida si rocola_os.txt no dice otra cosa
        internal bool SoloEnLaLista;      // solo con ROCOLA_OS=lista que la nombre (las de prueba): con «1» nunca, diga lo que diga el archivo
        internal bool Siempre;            // (28-11-2026, la cadena de sonido nueva) con ROCOLA_OS=1 SIEMPRE encendida: su clave se ignora; con una lista, si la nombra
        internal RocolaOsClave[] Claves;  // sus ajustes en rocola_os.txt (RocolaOs.Ajustes.cs), con su valor de fabrica
        internal Action<Form1> Arranca;                    // al arrancar, en el hilo de la pantalla, antes de ensenarse la ventana
        internal Action<Form1> Latido;                     // cada latido (1 s), en el hilo de la pantalla: rapido, sin esperar a nada
        internal Action<Form1, StringBuilder> Vivo;        // sus renglones de /run/rocola-vivo.txt (renglones enteros, con su «\n»)
        internal Action<Form1, RazonCierre> AlCerrar;      // al cerrarse, en el hilo de la pantalla, ANTES de que XP guarde: rapido
        internal Action<RazonCierre> ParaAlCerrar;         // al cerrarse, en un hilo suyo, con el plazo comun: parar procesos e hilos
        internal Action<Form1, string> AlCambiar;          // una clave suya cambio con la orden «ajuste» (en el hilo de la pantalla)
    }

    internal static partial class RocolaOs
    {
        // ---------- la variable ----------
        private const int SinLeer = 0, Nada = 1, Todo = 2, Lista = 3;
        private static int modo;                  // (SinLeer hasta la primera pregunta)
        private static string variable;           // lo que decia ROCOLA_OS, sin espacios (para la bitacora)
        private static HashSet<string> lista;     // con una lista: sus nombres
        private static readonly object cerrojoVariable = new object();

        private static int Modo { get { int m = Volatile.Read(ref modo); return m != SinLeer ? m : LeeLaVariable(); } }
        private static int LeeLaVariable()
        {
            lock (cerrojoVariable)
            {
                if (modo != SinLeer) return modo;
                string v = null;
                try { v = Environment.GetEnvironmentVariable("ROCOLA_OS"); } catch { }
                v = (v ?? "").Trim();
                variable = v;
                int m;
                if (v.Length == 0 || v == "0") m = Nada;
                else if (v == "1") m = Todo;
                else
                {
                    HashSet<string> l = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string p in v.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)) l.Add(p.Trim());
                    lista = l;
                    m = Lista;
                }
                Volatile.Write(ref modo, m);
                return m;
            }
        }
        // Hay algo de Rocola OS (ROCOLA_OS puesta y no «0»). Barato y desde cualquier hilo: lo que pregunta un gancho antes que nada.
        internal static bool Activo { get { return Modo != Nada; } }
        // Lo que decia la variable (para la bitacora y las pruebas): «» sin ella
        internal static string Variable { get { if (Volatile.Read(ref modo) == SinLeer) LeeLaVariable(); return variable ?? ""; } }
        internal static bool ConLista { get { return Modo == Lista; } }

        // ---------- el registro ----------
        private static readonly object cerrojoRegistro = new object();
        private static RocolaOsFuncion[] registradas = new RocolaOsFuncion[0];   // (se cambia entero: quien lo lee no necesita el cerrojo)
        private static bool registroCerrado;
        private static readonly List<string> problemas = new List<string>();      // lo que salio mal al registrar (se apunta al arrancar)

        // REGISTRA UNA FUNCION: lo llama su [ModuleInitializer] (FASE11_andamio.md, «Como se anade una funcion»). Nunca lanza (corre antes del
        // Main de XP, tambien sin ROCOLA_OS): lo que este mal se apunta al arrancar y esa funcion (o esa clave) no se usa.
        internal static void Registra(RocolaOsFuncion f)
        {
            try
            {
                lock (cerrojoRegistro)
                {
                    string mal = null;
                    if (f == null || !NombreValido(f.Nombre)) mal = "una funcion sin nombre valido (" + (f == null ? "null" : "«" + f.Nombre + "»") + "): no se usa";
                    else if (registroCerrado) mal = "«" + f.Nombre + "» se registro despues de arrancar: no se usa";
                    else if (Busca(f.Nombre) != null) mal = "«" + f.Nombre + "» registrada dos veces: vale la primera";
                    else if (ClaveDe(f.Nombre) != null) mal = "«" + f.Nombre + "» se llama como una clave de «" + ClaveDe(f.Nombre).Duena + "»: no se usa";
                    if (mal != null) { problemas.Add(mal); return; }
                    // sus claves: la suya (encendida o no; con «1»), salvo las de prueba, y las que declara
                    List<RocolaOsClave> suyas = new List<RocolaOsClave>();
                    if (!f.SoloEnLaLista) suyas.Add(RocolaOsClave.SiNo(f.Nombre, f.DeFabrica || f.Siempre, false, f.Siempre ? "se ignora: con ROCOLA_OS=1 va siempre (" + (f.Que ?? f.Nombre) + ")" : "encendida (1) o apagada (0) con ROCOLA_OS=1: " + (f.Que ?? f.Nombre)));
                    if (f.Claves != null) suyas.AddRange(f.Claves);
                    Dictionary<string, RocolaOsClave> c = new Dictionary<string, RocolaOsClave>(claves, StringComparer.Ordinal);
                    foreach (RocolaOsClave k in suyas)
                    {
                        if (k == null || !NombreValido(k.Nombre)) { problemas.Add("«" + f.Nombre + "» declara una clave sin nombre valido: no se usa"); continue; }
                        if (c.ContainsKey(k.Nombre)) { problemas.Add("la clave «" + k.Nombre + "» de «" + f.Nombre + "» ya es de «" + c[k.Nombre].Duena + "»: no se usa"); continue; }
                        if (Busca(k.Nombre) != null) { problemas.Add("la clave «" + k.Nombre + "» de «" + f.Nombre + "» se llama como una funcion: no se usa"); continue; }
                        k.Duena = f.Nombre;
                        c[k.Nombre] = k;
                    }
                    RocolaOsFuncion[] r = new RocolaOsFuncion[registradas.Length + 1];
                    Array.Copy(registradas, r, registradas.Length);
                    r[registradas.Length] = f;
                    Volatile.Write(ref claves, c);
                    Volatile.Write(ref registradas, r);
                }
            }
            catch { }
        }
        internal static RocolaOsFuncion[] Registradas { get { return Volatile.Read(ref registradas); } }
        internal static RocolaOsFuncion Busca(string nombre)
        {
            if (nombre == null) return null;
            foreach (RocolaOsFuncion f in Volatile.Read(ref registradas)) if (f.Nombre == nombre) return f;
            return null;
        }
        // un nombre de funcion o de clave: minusculas, cifras, «_», «-» y «.», empezando por letra o cifra (cabe sin comillas en ROCOLA_OS y
        // en «clave=valor»)
        internal static bool NombreValido(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Length > 64) return false;
            for (int i = 0; i < n.Length; i++)
            {
                char ch = n[i];
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || (i > 0 && (ch == '_' || ch == '-' || ch == '.'));
                if (!ok) return false;
            }
            return true;
        }

        // ---------- la pregunta de cada funcion ----------
        private static readonly object cerrojoDecide = new object();
        private static Dictionary<string, bool> decididas;   // (se pone una vez)

        // SI UNA FUNCION VA EN ESTA SESION. Sin ROCOLA_OS, no (sin leer nada mas). Con «1», su clave en rocola_os.txt o su valor de fabrica;
        // con una lista, si esta en ella. Se decide UNA vez (la primera vez que alguien pregunta, a mas tardar en Arranca, con rocola_os.txt
        // leido) y vale toda la sesion: cambiar su clave con la orden «ajuste» vale desde el siguiente arranque. Desde cualquier hilo, sin
        // cerrojos despues de la primera vez. (No se pregunta en un [ModuleInitializer]: el registro aun no esta completo.)
        internal static bool Encendida(string nombre)
        {
            if (Modo == Nada || nombre == null) return false;
            Dictionary<string, bool> d = Volatile.Read(ref decididas) ?? Decide();
            bool e;
            return d.TryGetValue(nombre, out e) && e;
        }
        // LO QUE DICE LA VARIABLE, SIN LEER rocola_os.txt: para un gancho que corre antes de Arranca y no puede leer archivos (g11, un
        // inicializador de campo de Form1). Con una lista, si esta en ella; con «1», su valor de fabrica (el archivo aun no se leyo: lo que
        // se decida con Pedida se corrige en el Arranca de la funcion con Encendida, que ya mira rocola_os.txt).
        internal static bool Pedida(string nombre)
        {
            int m = Modo;
            if (m == Nada || nombre == null) return false;
            if (m == Lista) return lista.Contains(nombre) && Busca(nombre) != null;
            RocolaOsFuncion f = Busca(nombre);
            return f != null && !f.SoloEnLaLista && (f.DeFabrica || f.Siempre);
        }
        private static Dictionary<string, bool> Decide()
        {
            lock (cerrojoDecide)
            {
                if (decididas != null) return decididas;
                int m = Modo;
                Dictionary<string, bool> d = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (RocolaOsFuncion f in Registradas)
                {
                    bool e;
                    if (m == Lista) e = lista.Contains(f.Nombre);
                    else if (m == Todo) e = !f.SoloEnLaLista && (f.Siempre || AjusteSiNo(f.Nombre));   // (su clave, con su valor de fabrica: Registra la declaro; Siempre: se ignora)
                    else e = false;
                    d[f.Nombre] = e;
                }
                Volatile.Write(ref decididas, d);
                return d;
            }
        }

        // ---------- el arranque ----------
        private static Form1 rocola;
        private static RocolaOsFuncion[] encendidas = new RocolaOsFuncion[0];
        private static Reloj latido;
        // La rocola (Form1) desde que arranca lo de Rocola OS: para lo que se le pide a XP desde el hilo de la pantalla. (No se llama «Rocola»:
        // dentro de esta clase taparia el espacio de nombres.)
        internal static Form1 LaRocola { get { return Volatile.Read(ref rocola); } }
        // Las funciones encendidas en esta sesion, en el orden en que se registraron
        internal static RocolaOsFuncion[] Encendidas { get { return Volatile.Read(ref encendidas); } }

        // LO DE ROCOLA OS ARRANCA (gancho g21, Nativo/PantallaRocola.Rocola.cs: en el hilo de la pantalla, con la rocola construida y antes de
        // ensenarse su ventana). Sin ROCOLA_OS no hace nada. Nada de lo que pase aqui impide que la rocola de XP arranque.
        internal static void Arranca(Form1 r)
        {
            if (!Activo || r == null || LaRocola != null) return;   // (una vez)
            try { ArrancaYa(r); }
            catch (Exception ex) { Falla("andamio", "arranque", ex); }
        }
        private static void ArrancaYa(Form1 r)
        {
            string[] mal;
            lock (cerrojoRegistro) { registroCerrado = true; mal = problemas.ToArray(); }
            Volatile.Write(ref rocola, r);
            Dictionary<string, bool> d = Volatile.Read(ref decididas) ?? Decide();
            List<RocolaOsFuncion> si = new List<RocolaOsFuncion>();
            StringBuilder cuales = new StringBuilder();
            foreach (RocolaOsFuncion f in Registradas)
            {
                bool e;
                if (d.TryGetValue(f.Nombre, out e) && e) { si.Add(f); cuales.Append(cuales.Length > 0 ? ", " : "").Append(f.Nombre); }
            }
            Volatile.Write(ref encendidas, si.ToArray());
            Apunta("andamio", "arranca con ROCOLA_OS=«" + Variable + "»: " + Registradas.Length + " funciones registradas; encendidas: "
                + (cuales.Length > 0 ? cuales.ToString() : "ninguna"));
            foreach (string p in mal) Apunta("andamio", "registro: " + p);
            if (Modo == Lista)
                foreach (string n in lista) if (Busca(n) == null) Apunta("andamio", "ROCOLA_OS nombra «" + n + "», que no es ninguna funcion registrada");
            string ap = AjustesProblema;
            if (ap != null) Apunta("andamio", ap);
            DespuesDeArrancarLosAjustes(r);   // (lo que quedo por guardar antes de tener la rocola, y el respaldo: RocolaOs.Ajustes.cs)
            foreach (RocolaOsFuncion f in si)
            {
                if (f.Arranca == null) continue;
                long t = Stopwatch.GetTimestamp();
                try { f.Arranca(r); }
                catch (Exception ex) { Falla(f.Nombre, "arranque", ex); }
                double ms = Ms(t);
                if (ms >= LatidoLentoMs) Apunta(f.Nombre, "su arranque tardo " + ms.ToString("0", CultureInfo.InvariantCulture) + " ms en el hilo de la pantalla");
            }
            // EL LATIDO: el reloj de la pantalla de Rocola OS (Nativo/Reloj.cs, con nombre fijo: las trazas lo nombran «RocolaOs:latido» y no
            // mueve los nombres de los de XP, que van por archivo:linea; compara_trazas.py --ignora RocolaOs: lo quita). Vence con la regla de
            // WM_TIMER, como los de XP.
            Reloj l = new Reloj("RocolaOs:latido", null);
            l.Interval = LatidoMs;
            l.Tick += Late;
            latido = l;
            l.Start();
        }

        // ---------- el latido ----------
        internal const int LatidoMs = 1000;
        internal const double LatidoLentoMs = 50;   // un latido (o un arranque) que tarda esto o mas en la pantalla se apunta
        private static int enElLatido;
        private static long latidos, saltados;
        private static double latidoUltimoMs, latidoMaxMs;
        private static long lentoApuntado = long.MinValue / 2;
        internal static long Latidos { get { return Interlocked.Read(ref latidos); } }
        internal static double LatidoUltimoMs { get { return latidoUltimoMs; } }
        internal static double LatidoMaxMs { get { return latidoMaxMs; } }
        private static void Late(object remitente, EventArgs e)
        {
            Form1 r = LaRocola;
            if (r == null || Cerrandose) return;
            if (enElLatido != 0) { saltados++; return; }   // (uno que bombea la pantalla, como el diagnostico de XP: no vuelve a entrar en si mismo)
            enElLatido = 1;
            long t0 = Stopwatch.GetTimestamp();
            string lenta = null; double lentaMs = 0;
            try
            {
                LatidoDelAndamio(r);   // (el respaldo diario de rocola_os.txt: RocolaOs.Ajustes.cs)
                foreach (RocolaOsFuncion f in Encendidas)
                {
                    if (f.Latido == null) continue;
                    long t = Stopwatch.GetTimestamp();
                    try { f.Latido(r); }
                    catch (Exception ex) { Falla(f.Nombre, "latido", ex); }
                    double ms = Ms(t);
                    if (ms > lentaMs) { lentaMs = ms; lenta = f.Nombre; }
                }
            }
            finally { enElLatido = 0; }
            double total = Ms(t0);
            Interlocked.Increment(ref latidos);
            latidoUltimoMs = total;
            if (total > latidoMaxMs) latidoMaxMs = total;
            if (total >= LatidoLentoMs && Environment.TickCount64 - lentoApuntado >= 60000)
            {
                lentoApuntado = Environment.TickCount64;
                Apunta("andamio", "latido lento: " + total.ToString("0", CultureInfo.InvariantCulture) + " ms"
                    + (lenta != null ? " (" + lenta + ": " + lentaMs.ToString("0", CultureInfo.InvariantCulture) + " ms)" : "")
                    + "; el mas lento de la sesion, " + latidoMaxMs.ToString("0", CultureInfo.InvariantCulture) + " ms; saltados " + saltados);
            }
        }

        // ---------- /run/rocola-vivo.txt ----------
        // LOS RENGLONES QUE APORTA CADA FUNCION ENCENDIDA (el archivo lo escribe 1a, desde el latido: en el hilo de la pantalla). Cada Vivo
        // anade renglones enteros, con su «\n»; si lanza, lo que habia anadido se quita y se apunta.
        internal static void RenglonesVivos(StringBuilder sb)
        {
            Form1 r = LaRocola;
            if (r == null || sb == null) return;
            foreach (RocolaOsFuncion f in Encendidas)
            {
                if (f.Vivo == null) continue;
                int antes = sb.Length;
                try { f.Vivo(r, sb); if (sb.Length > antes && sb[sb.Length - 1] != '\n') sb.Append('\n'); }
                catch (Exception ex) { sb.Length = antes; Falla(f.Nombre, "renglones de vivo.txt", ex); }
            }
        }

        // ---------- la hora ----------
        internal static double Ms(long desde) { return (Stopwatch.GetTimestamp() - desde) * 1000.0 / Stopwatch.Frequency; }
    }
}
