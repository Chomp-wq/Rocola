// LA PRUEBA DEL ANDAMIO (fase 11, grupo andamio; la corre Pruebas/RocolaOs/andamio/prueba.sh). No es de XP ni hace nada en la rocola: es
// una funcion de Rocola OS de mentira, «prueba-andamio», SOLO para las pruebas (SoloEnLaLista: solo con ROCOLA_OS=prueba-andamio; con «1»
// nunca, diga lo que diga rocola_os.txt), que usa cada enganche del andamio y apunta en la bitacora (rocola_os_bitacora.txt) lo que ve:
//   Arranca      sus ajustes (numero, texto y contador: de fabrica o del archivo), si corre en el hilo de la pantalla, y sube el contador
//                (PonAjuste: rocola_os.txt se guarda detras); arranca un hilo de fondo que apunta su nice y encarga algo a la pantalla
//   Latido       cuantos lleva y, cada vez, /run/rocola-os-prueba-vivo.txt (ROCOLA_RUN) con los renglones vivos de todas (RenglonesVivos)
//   Vivo         su renglon: «prueba-andamio=latidos N»
//   AlCerrar     la razon del cierre, en la pantalla, y si rocola_estado.txt sigue ahi (XP aun no lo borro: va ANTES de que XP guarde)
//   ParaAlCerrar la razon, en su hilo, y espera prueba-andamio.cierre_ms (para probar el plazo comun)
//   con prueba-andamio.falla=1, su segundo latido y su hilo lanzan (la rocola sigue y queda apuntado)
// Y la orden `rocola-xp --prueba-rocola-os variable|ajustes` (con ROCOLA_PANTALLA=fuera: RocolaOs.Ordenes.cs): lo que decidio la variable,
// y los ajustes de rocola_os.txt de la carpeta de datos (ROCOLA_DATOS) sin la rocola.
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static class RocolaOsPruebaAndamio
    {
        private const string Nombre = "prueba-andamio";
        private static int latidos;

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = Nombre,
                Que = "la prueba del andamio (solo con ROCOLA_OS=prueba-andamio)",
                DeFabrica = false,
                SoloEnLaLista = true,
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.Entero(Nombre + ".numero", 7, 0, 100, true, "un numero de prueba"),
                    RocolaOsClave.Texto(Nombre + ".texto", "hola", null, false, "un texto de prueba"),
                    RocolaOsClave.Entero(Nombre + ".contador", 0, 0, 1000000, false, "cuantas veces arranco"),
                    RocolaOsClave.Entero(Nombre + ".cierre_ms", 0, 0, 60000, false, "lo que tarda su ParaAlCerrar"),
                    RocolaOsClave.SiNo(Nombre + ".falla", false, false, "su segundo latido y su hilo lanzan"),
                },
                Arranca = Arranca,
                Latido = Latido,
                Vivo = delegate (Form1 r, StringBuilder sb) { sb.Append(Nombre).Append("=latidos ").Append(latidos.ToString(CultureInfo.InvariantCulture)).Append('\n'); },
                AlCerrar = AlCerrar,
                ParaAlCerrar = ParaAlCerrar,
                AlCambiar = delegate (Form1 r, string clave) { RocolaOs.Apunta(Nombre, "cambio " + clave + " = " + RocolaOs.Ajuste(clave)); },
            });
            RocolaOsOrdenes.Si("--prueba-rocola-os", Orden);
        }

        private static string SiNo(bool b) { return b ? "si" : "no"; }
        private static bool EstadoDeXp { get { return File.Exists(RocolaOs.Datos("rocola_estado.txt")); } }

        private static void Arranca(Form1 r)
        {
            int contador = RocolaOs.AjusteEntero(Nombre + ".contador");
            RocolaOs.Apunta(Nombre, "arranque: numero=" + RocolaOs.Ajuste(Nombre + ".numero") + " texto=" + RocolaOs.Ajuste(Nombre + ".texto")
                + " contador=" + contador.ToString(CultureInfo.InvariantCulture) + " encendida=" + SiNo(RocolaOs.Encendida(Nombre))
                + " en la pantalla=" + SiNo(RocolaOs.EnLaPantalla));
            RocolaOs.PonAjuste(Nombre + ".contador", (contador + 1).ToString(CultureInfo.InvariantCulture));
            bool falla = RocolaOs.AjusteSiNo(Nombre + ".falla");
            RocolaOs.Hilo(Nombre, delegate
            {
                int nice = PrioridadHilo.NiceDelHilo(PrioridadHilo.Este());
                RocolaOs.Apunta(Nombre, "hilo de fondo: nice=" + nice.ToString(CultureInfo.InvariantCulture) + " nombre=" + Thread.CurrentThread.Name
                    + " de fondo=" + SiNo(Thread.CurrentThread.IsBackground) + " en la pantalla=" + SiNo(RocolaOs.EnLaPantalla));
                // la ventana se ensena despues de Arranca: hasta entonces no se puede encargar
                for (int i = 0; i < 200; i++)
                {
                    if (RocolaOs.Encarga(Nombre, delegate { RocolaOs.Apunta(Nombre, "encargo: en la pantalla=" + SiNo(RocolaOs.EnLaPantalla)); })) break;
                    Thread.Sleep(100);
                }
                if (falla) throw new InvalidOperationException("fallo de prueba en el hilo");
            });
        }

        private static void Latido(Form1 r)
        {
            int n = Interlocked.Increment(ref latidos);
            if (n == 1) RocolaOs.Apunta(Nombre, "primer latido: en la pantalla=" + SiNo(RocolaOs.EnLaPantalla));
            if (n == 2 && RocolaOs.AjusteSiNo(Nombre + ".falla")) throw new InvalidOperationException("fallo de prueba en el latido");
            StringBuilder sb = new StringBuilder();
            RocolaOs.RenglonesVivos(sb);
            string ruta = RocolaOs.Run("rocola-os-prueba-vivo.txt");
            File.WriteAllText(ruta + ".n", sb.ToString());
            File.Move(ruta + ".n", ruta, true);
        }

        private static void AlCerrar(Form1 r, RazonCierre razon)
        {
            RocolaOs.Apunta(Nombre, "al cerrar (pantalla): razon=" + razon + " razon del andamio=" + RocolaOs.RazonDelCierre + " latidos=" + latidos.ToString(CultureInfo.InvariantCulture)
                + " estado de XP presente=" + SiNo(EstadoDeXp) + " en la pantalla=" + SiNo(RocolaOs.EnLaPantalla));
        }

        private static void ParaAlCerrar(RazonCierre razon)
        {
            int ms = RocolaOs.AjusteEntero(Nombre + ".cierre_ms");
            RocolaOs.Apunta(Nombre, "para al cerrar (su hilo): razon=" + razon + " en la pantalla=" + SiNo(RocolaOs.EnLaPantalla) + " espera " + ms.ToString(CultureInfo.InvariantCulture) + " ms");
            if (ms > 0) Thread.Sleep(ms);
            RocolaOs.Apunta(Nombre, "para al cerrar: acabo");
        }

        // ---------- la orden de prueba (sin la rocola) ----------
        private static int Orden(string[] a)
        {
            string que = a.Length > 0 ? a[0] : "";
            if (que == "variable")
            {
                Console.WriteLine("activo=" + (RocolaOs.Activo ? 1 : 0) + " variable=«" + RocolaOs.Variable + "» lista=" + (RocolaOs.ConLista ? 1 : 0));
                foreach (string n in new string[] { Nombre, "no-existe" })
                    Console.WriteLine("funcion " + n + ": pedida=" + (RocolaOs.Pedida(n) ? 1 : 0) + " encendida=" + (RocolaOs.Encendida(n) ? 1 : 0));
                Console.WriteLine("claves: " + string.Join(" ", RocolaOs.NombresDeClaves()));
                return 0;
            }
            if (que == "ajustes")
            {
                string[] claves = { Nombre + ".numero", Nombre + ".texto", Nombre + ".contador", Nombre + ".cierre_ms", Nombre + ".falla", "no-declarada" };
                foreach (string k in claves) Console.WriteLine("lee " + k + " = «" + (RocolaOs.Ajuste(k) ?? "(null)") + "» cambiado=" + (RocolaOs.AjusteCambiado(k) ? 1 : 0));
                // lo que se pide en la linea de mandatos despues de «ajustes»: «pon CLAVE VALOR» (PonAjuste) o «fuera CLAVE=VALOR» (la orden del buzon)
                for (int i = 1; i < a.Length; i++)
                {
                    if (a[i] == "pon" && i + 2 < a.Length) { Console.WriteLine("pon " + a[i + 1] + " «" + a[i + 2] + "» -> " + (RocolaOs.PonAjuste(a[i + 1], a[i + 2]) ? "si" : "no")); i += 2; }
                    else if (a[i] == "quita" && i + 1 < a.Length) { Console.WriteLine("quita " + a[i + 1] + " -> " + (RocolaOs.PonAjuste(a[i + 1], "") ? "si" : "no")); i += 1; }
                    else if (a[i] == "fuera" && i + 1 < a.Length) { Console.WriteLine("fuera «" + a[i + 1] + "» -> " + RocolaOs.AjusteDeFuera(a[i + 1])); i += 1; }
                    else { Console.WriteLine("no entiendo «" + a[i] + "»"); return 2; }
                }
                RocolaOs.GuardaYa();   // (sin la rocola no hay GuardaDetras: aqui se escribe en este hilo)
                foreach (string k in claves) Console.WriteLine("queda " + k + " = «" + (RocolaOs.Ajuste(k) ?? "(null)") + "»");
                Bitacora.Vacia(2000);   // (y la bitacora, en este hilo: el suyo de fondo no entra mientras corre el inicializador)
                return 0;
            }
            Console.Error.WriteLine("uso: rocola-xp --prueba-rocola-os variable|ajustes [pon CLAVE VALOR | quita CLAVE | fuera CLAVE=VALOR]...");
            return 2;
        }
    }
}
