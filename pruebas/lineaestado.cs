using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

// LA LINEA DE ESTADO NO SE QUEDA CON AVISOS VIEJOS (barrido del 12-10-2026).
//  1) Un aviso de avance del escaneo que llega TARDE (despues de terminar, o de un escaneo viejo tras F5) no pisa el
//     letrero: antes "Encontradas N canciones..." podia quedarse puesto encima de la pared para siempre.
//  2) Los avisos de error ("No se pudo reproducir ... (mensaje de Windows)", "Video (WMP): ...", "Aviso al
//     reproducir: ...", "Sin salida de sonido") se van solos; antes se quedaban hasta volver a leer la biblioteca.
//     Solo se borran si nadie puso otra cosa encima.
// Formulario aislado, sin ventana, sin musica ni escaneo de verdad.
class LineaEstado
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;
    static object Get(object o, string n) { return form.GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { form.GetField(n, B).SetValue(o, v); }
    static object Call(object o, string n, params object[] a)
    {
        foreach (MethodInfo m in form.GetMethods(B)) if (m.Name == n && m.GetParameters().Length == a.Length)
            try { return m.Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("Falta " + n);
    }
    static int Main(string[] args)
    {
        form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        object f = Codigo.FormSinEncender(form);
        Label st = new Label(); Set(f, "lblStatus", st);

        // 1) Avisos de avance del escaneo.
        BackgroundWorker vivo = new BackgroundWorker(), viejo = new BackgroundWorker();
        Set(f, "scanner", vivo);
        Set(f, "libBuilding", true); st.Text = "Buscando musica ...";
        Call(f, "Scanner_ProgressChanged", vivo, new ProgressChangedEventArgs(0, 120));
        Check(st.Text == "Encontradas 120 canciones...", "mientras se lee la biblioteca, el avance se ve");
        Call(f, "Scanner_ProgressChanged", viejo, new ProgressChangedEventArgs(0, 999));
        Check(st.Text == "Encontradas 120 canciones...", "el avance de un escaneo viejo (tras F5) no pisa el del actual");
        Set(f, "libBuilding", false); st.Text = "";
        Call(f, "Scanner_ProgressChanged", vivo, new ProgressChangedEventArgs(0, 352));
        Check(st.Text == "", "un avance que llega despues de terminar no vuelve a poner el letrero");

        // 2) Los avisos de error se van solos.
        Call(f, "EstadoPasajero", "Video (WMP): Exception from HRESULT: 0xC00D1199", 15000);
        Call(f, "CaducaEstado");
        Check(st.Text.StartsWith("Video (WMP)"), "recien puesto, el aviso se ve");
        Set(f, "estadoPasajeroHasta", unchecked(Environment.TickCount - 1));
        Call(f, "CaducaEstado");
        Check(st.Text == "", "pasado su tiempo, el aviso se va solo");
        Call(f, "EstadoPasajero", "Aviso al reproducir: algo", 15000);
        st.Text = "Buscando musica en: D:\\musica ...";   // otro letrero encima, antes de que caduque
        Set(f, "estadoPasajeroHasta", unchecked(Environment.TickCount - 1));
        Call(f, "CaducaEstado");
        Check(st.Text.StartsWith("Buscando musica"), "si alguien puso otra cosa encima, no se borra");
        Call(f, "EstadoPasajero", "x", 0);
        int hasta = (int)Get(f, "estadoPasajeroHasta");
        Check(unchecked(hasta - Environment.TickCount) >= 900, "como minimo un segundo a la vista");
        Set(f, "estadoPasajeroHasta", unchecked(int.MaxValue - 10));   // la vuelta del contador de Windows
        Call(f, "CaducaEstado");
        Check(st.Text == "x" || unchecked(Environment.TickCount - (int.MaxValue - 10)) >= 0, "aguanta la vuelta del contador");

        // Cableado: cada aviso de error pasa por aqui, y el latido los caduca.
        foreach (string m in new string[] { "AudioStartFailed", "Output_PlaybackStopped", "StartVideoItem", "SalidaCaida" })
        {
            MethodInfo mi = form.GetMethod(m, B);
            Check(mi != null && Codigo.Llama(mi, "EstadoPasajero"), m + " pone su aviso con tiempo");
        }
        Check(Codigo.Llama(form.GetMethod("AnimTimer_Tick", B), "CaducaEstado"), "el latido caduca los avisos");
        Console.WriteLine(mal == 0 ? "  OK lineaestado: " + ok + " comprobaciones (formulario aislado; sin musica ni escaneo de verdad)"
                                   : "  FALLO lineaestado: " + mal + " de " + (ok + mal));
        return mal;
    }
}
