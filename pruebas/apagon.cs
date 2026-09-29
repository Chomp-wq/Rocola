using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

// SI SE VA LA LUZ, EL DINERO DEL CLIENTE NO SE PIERDE.
//
// En un bar la luz se va. Por eso la rocola guarda en un archivo los creditos que le quedan al cliente y la
// cola de canciones: para que al volver la luz siga todo donde estaba. Ese archivo tenia un problema muy
// feo: se escribia con "abre, VACIA el archivo entero y vuelve a llenarlo". Entre el vaciado y el llenado
// el archivo esta vacio, y ademas Windows no baja lo escrito al disco al momento, se lo guarda en memoria y
// lo escribe cuando le viene bien. Si la luz se iba en ese rato, el archivo quedaba vacio o a medias.
//
// Y al arrancar con ese archivo roto la rocola no se queja: se queda tan tranquila con cero creditos y sin
// cola. O sea que el archivo que existe PARA sobrevivir a un corte de luz era destruido por el corte de luz,
// y el dinero del cliente desaparecia sin que nadie supiera por que.
//
// Detras van tambien horas de trabajo: el volumen medido de toda la biblioteca -que tarda horas en medirse-,
// lo mas sonado, los ajustes y las teclas se guardaban igual.
//
// Aqui se comprueba que ahora se guarda de forma que el disco SIEMPRE tenga un archivo completo, que lo
// guardado se sigue leyendo exactamente igual que antes, que si el archivo bueno aparece roto se tira de la
// copia, y que dos hilos guardando a la vez no se pisan.
class Apagon
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static string Dir() { return Path.Combine(Path.GetTempPath(), "rocola_apagon"); }

    static void Limpia()
    {
        try { if (Directory.Exists(Dir())) Directory.Delete(Dir(), true); } catch { }
        Directory.CreateDirectory(Dir());
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        BindingFlags SN = BindingFlags.Static | BindingFlags.NonPublic;
        BindingFlags IN = BindingFlags.Instance | BindingFlags.NonPublic;

        MethodInfo mEsc = TF.GetMethod("EscribeSeguro", SN);
        MethodInfo mLee = TF.GetMethod("LeeLineasSeguro", SN);

        Console.WriteLine("\n  1) LO GUARDADO SE ESCRIBE DE FORMA QUE UN CORTE NO LO ROMPA\n");
        Check(mEsc != null, "la rocola guarda con un metodo que no deja el archivo a medias");
        Check(mLee != null, "y sabe leer la copia si el archivo bueno aparece roto");
        if (mEsc == null || mLee == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        Limpia();
        string ruta = Path.Combine(Dir(), "prueba.txt");

        // Los MISMOS bytes que antes. Si cambiaran, todo lo guardado hasta hoy dejaria de leerse igual: los
        // creditos, el volumen de cada cancion, los ajustes... Esto es lo primero que hay que asegurar.
        string texto = "ROCOLA-ESTADO v1\r\ncreditos=7\r\ncola=S|C:\\Musica\\Uno\\dos.mp3\r\nacentos: Corazon Partio\r\n";
        mEsc.Invoke(null, new object[] { ruta, texto });
        byte[] nuevos = File.ReadAllBytes(ruta);
        string patron = Path.Combine(Dir(), "patron.txt");
        File.WriteAllText(patron, texto);   // la forma de antes
        byte[] viejos = File.ReadAllBytes(patron);
        bool iguales = nuevos.Length == viejos.Length;
        if (iguales) for (int i = 0; i < nuevos.Length; i++) if (nuevos[i] != viejos[i]) { iguales = false; break; }
        Console.WriteLine("     el archivo mide " + nuevos.Length + " bytes y antes median " + viejos.Length);
        Check(iguales, "lo guardado sale byte por byte igual que antes: nada de lo ya guardado se estropea");

        // No se queda basura al lado
        Check(!File.Exists(ruta + ".tmp"), "no deja archivos de paso tirados por ahi");

        // ---------- 2) SIEMPRE QUEDA UNA COPIA DEL ANTERIOR ----------
        Console.WriteLine("\n  2) Y SIEMPRE QUEDA UNA COPIA DE LO ANTERIOR\n");
        string texto2 = "ROCOLA-ESTADO v1\r\ncreditos=99\r\n";
        mEsc.Invoke(null, new object[] { ruta, texto2 });
        Check(File.Exists(ruta + ".bak"), "al volver a guardar, lo de antes queda como copia");
        Check(File.ReadAllText(ruta) == texto2, "y el bueno tiene lo nuevo");
        Check(File.ReadAllText(ruta + ".bak") == texto, "y la copia tiene lo de antes");

        // ---------- 3) EL CORTE DE LUZ ----------
        // Se deja el archivo bueno VACIO, que es exactamente como lo dejaba un corte de luz con la forma de
        // guardar de antes. La rocola tiene que seguir sabiendo cuantos creditos tenia el cliente.
        Console.WriteLine("\n  3) CON EL ARCHIVO BUENO ROTO, LOS CREDITOS NO SE PIERDEN\n");
        using (FileStream fs = new FileStream(ruta, FileMode.Create, FileAccess.Write)) { }   // 0 bytes: el corte
        Console.WriteLine("     el archivo bueno se queda en " + new FileInfo(ruta).Length + " bytes (asi lo dejaba el corte)");
        string[] leido = (string[])mLee.Invoke(null, new object[] { ruta });
        bool recuperado = false;
        if (leido != null) for (int i = 0; i < leido.Length; i++) if (leido[i] == "creditos=7") recuperado = true;
        Check(leido != null && leido.Length > 0, "con el archivo bueno vacio, todavia se lee algo");
        Check(recuperado, "y lo que se lee son los creditos que tenia el cliente");

        // ---------- 4) LOS CREDITOS DE VERDAD, GUARDADOS POR LA ROCOLA ----------
        // Se le pide a la rocola que guarde su estado DE VERDAD (su propio SaveState), dos veces con creditos
        // distintos, y se comprueba que queda copia del anterior.
        Console.WriteLine("\n  4) Y ESTO ES LO QUE USA LA ROCOLA PARA SUS CREDITOS\n");
        MethodInfo mSave = TF.GetMethod("SaveState", IN);
        MethodInfo mFile = TF.GetMethod("StateFile", IN);
        Check(mSave != null && mFile != null, "la rocola guarda creditos y cola en su archivo de estado");
        if (mSave != null && mFile != null)
        {
            // Con sus cerrojos puestos: el estado se escribe en un hilo de fondo (estadodetras.cs) y se espera a
            // cada guardado, porque aqui lo que se mira es que el segundo deje copia del primero.
            object f = Codigo.FormSinEncender(TF);
            Type TPI = TF.GetNestedType("PlayItem", BindingFlags.NonPublic | BindingFlags.Public);
            TF.GetField("queue", IN).SetValue(f, Activator.CreateInstance(typeof(List<>).MakeGenericType(TPI)));
            FieldInfo fCred = TF.GetField("credits", IN);
            string est = (string)mFile.Invoke(f, null);
            try { if (File.Exists(est)) File.Delete(est); } catch { }
            try { if (File.Exists(est + ".bak")) File.Delete(est + ".bak"); } catch { }

            fCred.SetValue(f, 12); mSave.Invoke(f, null); Codigo.EsperaGuardado(TF, f);
            fCred.SetValue(f, 3); mSave.Invoke(f, null); Codigo.EsperaGuardado(TF, f);
            bool hayCopia = File.Exists(est + ".bak");
            bool copiaBuena = hayCopia && File.ReadAllText(est + ".bak").Contains("creditos=12");
            bool buenoBueno = File.ReadAllText(est).Contains("creditos=3");
            Console.WriteLine("     tras guardar 12 y luego 3 creditos: el bueno dice 3 y la copia dice 12");
            Check(buenoBueno, "el archivo bueno lleva los creditos de ahora");
            Check(copiaBuena, "y la copia lleva los de antes: un corte a mitad de guardado no los borra");
            try { File.Delete(est); } catch { }
            try { File.Delete(est + ".bak"); } catch { }
        }

        // ---------- 5) DOS HILOS GUARDANDO A LA VEZ ----------
        // Las estadisticas -"LO MAS SONADO"- tienen dos escritores: uno de fondo cada tanto y otro al cerrar
        // la rocola. Si se pisaban, el archivo quedaba hecho un lio y se perdia la lista entera. Aqui ocho
        // hilos guardan a la vez el mismo archivo y al final tiene que haber quedado UNO de los textos
        // enteros, no un revoltijo de varios.
        Console.WriteLine("\n  5) DOS COSAS GUARDANDO A LA VEZ NO SE PISAN\n");
        Limpia();
        string rc = Path.Combine(Dir(), "compartido.txt");
        const int HILOS = 8, VECES = 30;
        string[] textos = new string[HILOS];
        for (int i = 0; i < HILOS; i++)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ROCOLA-STATS v1\r\n");
            for (int k = 0; k < 200; k++) sb.Append("hilo").Append(i).Append("=").Append(k).Append("\r\n");
            textos[i] = sb.ToString();
        }
        Thread[] th = new Thread[HILOS];
        for (int i = 0; i < HILOS; i++)
        {
            int mio = i;
            th[i] = new Thread(delegate ()
            {
                for (int k = 0; k < VECES; k++) mEsc.Invoke(null, new object[] { rc, textos[mio] });
            });
            th[i].IsBackground = true;
        }
        for (int i = 0; i < HILOS; i++) th[i].Start();
        for (int i = 0; i < HILOS; i++) th[i].Join();
        string quedo = File.ReadAllText(rc);
        int cual = -1;
        for (int i = 0; i < HILOS; i++) if (quedo == textos[i]) cual = i;
        Console.WriteLine("     " + HILOS + " hilos x " + VECES + " guardados sobre el mismo archivo: quedaron "
                          + quedo.Length + " bytes, y los textos miden " + textos[0].Length);
        Check(cual >= 0, "lo que queda en el disco es UNO de los textos entero, no un revoltijo");
        string[] sobras = Directory.GetFiles(Dir(), "*.tmp");
        Check(sobras.Length == 0, "y no quedan archivos de paso tirados (" + sobras.Length + ")");

        // ---------- 6) AL CERRAR A PROPOSITO NO QUEDA NADA QUE REANUDAR ----------
        // La rocola distingue un APAGON de un cierre A PROPOSITO por una sola cosa: si al arrancar el
        // archivo de estado esta ahi, fue apagon y hay que devolverle al cliente sus creditos y su cola;
        // si no esta, es que se cerro aposta -Alt+F4 o el boton de poder- y el dia arranca limpio.
        //
        // Guardar de forma segura deja SIEMPRE una copia al lado, y eso rompio la senal: los sitios que
        // descartan el estado borraban solo el archivo bueno y la copia se quedaba. Al arrancar, la
        // rocola tira de la copia -que es justo para lo que esta- y resucita todo. O sea que cerrar la
        // rocola ya no limpiaba nada: al dia siguiente aparecian los creditos de anoche y la cola se
        // ponia a sonar sola.
        Console.WriteLine("\n  6) AL CERRAR A PROPOSITO NO QUEDA NADA QUE REANUDAR\n");
        MethodInfo mBorra = TF.GetMethod("BorraSeguro", SN);
        Check(mBorra != null, "hay una forma de borrar un guardado que se lleva tambien su copia");
        if (mBorra != null)
        {
            Limpia();
            string re = Path.Combine(Dir(), "estado.txt");
            mEsc.Invoke(null, new object[] { re, "ROCOLA-ESTADO v1\r\ncreditos=9\r\n" });
            mEsc.Invoke(null, new object[] { re, "ROCOLA-ESTADO v1\r\ncreditos=4\r\n" });
            Check(File.Exists(re + ".bak"), "guardando dos veces queda una copia al lado (de eso iba todo esto)");
            mBorra.Invoke(null, new object[] { re });
            string[] queda = (string[])mLee.Invoke(null, new object[] { re });
            Console.WriteLine("     despues de borrar el estado se puede leer de vuelta: "
                              + ((queda == null) ? "nada" : (queda.Length + " lineas")));
            Check(queda == null, "y despues de borrarlo NO queda nada que leer: manana arranca limpia");
            Check(!File.Exists(re) && !File.Exists(re + ".bak") && !File.Exists(re + ".tmp"),
                  "ni el archivo, ni la copia, ni el de paso");
        }

        // Y los tres sitios por los que se descarta el estado tienen que borrarlo asi. Da igual por cual
        // se llegue -el cierre a proposito, el dia nuevo a las 7, o encontrarse un estado de otro dia-:
        // si uno solo se quedara borrando a mano, la copia volveria a resucitarlo. Desde que el estado se
        // escribe en un hilo de fondo, pasan por BorraEstado, que ademas deja atras los encargos viejos
        // (estadodetras.cs); por dentro sigue siendo este mismo borrado.
        MethodInfo mBorraEstado = TF.GetMethod("BorraEstado", IN);
        bool porEstado = mBorraEstado != null && Codigo.Nombres(mBorraEstado).Contains("BorraSeguro");
        Check(porEstado, "el borrado del estado (BorraEstado) usa este borrado que se lleva la copia");
        string[] sitios = { "OnFormClosing", "CheckBusinessDayReset", "RestoreState" };
        string[] cuando = { "al cerrar a proposito", "al empezar el dia a las 7", "con un estado de otro dia" };
        for (int i = 0; i < sitios.Length; i++)
        {
            MethodInfo m = TF.GetMethod(sitios[i], IN);
            List<string> hace = (m == null) ? new List<string>() : Codigo.Nombres(m);
            Check(m != null && (hace.Contains("BorraSeguro") || (porEstado && hace.Contains("BorraEstado"))), cuando[i] + " se borra el estado con la copia y todo");
            Check(m != null && !hace.Contains("Delete"), "   ...y ya no se borra a mano por su cuenta");
        }

        // ---------- 7) UN DISCO DESCONECTADO NO PARECE UNA CANCION BORRADA ----------
        // Al restaurar, una ruta ausente tiene dos significados muy distintos: si su raiz tambien falta,
        // el disco esta desconectado y hay que conservar intactos creditos/cola; si la raiz esta presente,
        // el archivo si desaparecio y toca devolver el credito de esa seleccion.
        Console.WriteLine("\n  7) UN DISCO DESCONECTADO NO CONSUME LA COLA PAGADA\n");
        MethodInfo mRaizAusente = TF.GetMethod("RaizConfiguradaAusente", IN);
        MethodInfo mRestore = TF.GetMethod("RestoreState", IN);
        MethodInfo mTimer = TF.GetMethod("AnimTimer_Tick", IN);
        Check(mRaizAusente != null, "la restauracion distingue una raiz desconectada de un archivo borrado");
        if (mRaizAusente != null)
        {
            Limpia();
            string presente = Path.Combine(Dir(), "presente");
            string ausente = Path.Combine(Dir(), "disco_desconectado");
            Directory.CreateDirectory(presente);
            object f = FormatterServices.GetUninitializedObject(TF);
            TF.GetField("extraMusicDirs", IN).SetValue(f, new List<string> { presente, ausente });
            FieldInfo datos = TF.GetField("carpetaDatos", SN);
            object datosAntes = datos.GetValue(null);
            try
            {
                datos.SetValue(null, Dir());
                string falta = (string)mRaizAusente.Invoke(f, new object[] { Path.Combine(Path.Combine(ausente, "Album"), "tema.mp3") });
                string borrada = (string)mRaizAusente.Invoke(f, new object[] { Path.Combine(Path.Combine(presente, "Album"), "tema.mp3") });
                Check(string.Equals(falta, Path.GetFullPath(ausente), StringComparison.OrdinalIgnoreCase),
                      "una seleccion de la raiz desconectada queda pendiente, sin perder su pago");
                Check(borrada == null, "una seleccion borrada dentro de una raiz presente si se reconoce como perdida");
            }
            finally { datos.SetValue(null, datosAntes); }
        }
        List<string> restaura = (mRestore == null) ? new List<string>() : Codigo.Nombres(mRestore);
        List<string> reintenta = (mTimer == null) ? new List<string>() : Codigo.Nombres(mTimer);
        Check(restaura.Contains("RaizConfiguradaAusente"), "RestoreState usa esa distincion antes de tocar creditos y cola");
        Check(reintenta.Contains("StartScan") && reintenta.Contains("AllMusicRoots"),
              "cuando vuelve el disco, la biblioteca se escanea otra vez y la restauracion puede continuar");

        Limpia();
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}
