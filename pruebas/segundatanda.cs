using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// SEGUNDA TANDA DE FALLOS PARA LA VERSION FINAL (12-10-2026).
//  1) El reloj de Windows que se atrasa (cambio de horario de XP, sincronizacion, a mano): la inactividad cuenta desde
//     ahora. Antes la ultima tecla quedaba "en el futuro" y ni la atraccion ni el cierre del buscador llegaban en una hora.
//  2) Environment.TickCount pasa a negativo a los 24,9 dias de Windows encendido: el aviso de siempre y el hojeo de
//     paginas guardaban su arranque con -1 = "nada" y miraban "< 0", asi que dejaban de verse hasta los 49,7 dias.
//     Ahora -1 es "nada" (comparacion exacta) y TickVivo nunca lo da.
//  3) La linea de estado ya no muestra el mensaje de la excepcion (ingles, tecnico, cortado): dice el motivo en corto.
// Formulario aislado, sin ventana ni musica.
class SegundaTanda
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
    // Un campo de tiempo comparado contra cero: "ldfld campo; ldc.i4.0; (blt|bge|bgt|ble|clt|cgt)".
    static readonly string[] Compara = { "32", "2F", "30", "31", "3F", "3C", "3D", "3E", "FE04", "FE02", "FE05", "FE03" };
    static int ComparaConCero(MethodBase m, string campo)
    {
        List<string> il = Codigo.IlConNombres(m);
        if (il == null) return 0;
        int n = 0;
        for (int i = 0; i + 2 < il.Count; i++)
        {
            if (!il[i].StartsWith("7B ") || !il[i].EndsWith(" " + campo)) continue;
            if (il[i + 1] != "16") continue;
            string op = il[i + 2].Split(' ')[0];
            if (Array.IndexOf(Compara, op) >= 0) n++;
        }
        return n;
    }
    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        object f = Codigo.FormSinEncender(form);

        // 1) El reloj que se atrasa.
        try { RelojQueSeAtrasa(f); } catch (Exception e) { Check(false, "el reloj que se atrasa: " + e.Message); }
        Check(Codigo.Llama(form.GetMethod("AnimTimer_Tick", B), "RelojAtras"), "el latido lo comprueba");
        Tick(asm);
        try { Motivos(); } catch (Exception e) { Check(false, "el motivo del fallo: " + e.Message); }
        Console.WriteLine(mal == 0 ? "  OK segundatanda: " + ok + " comprobaciones (formulario aislado; sin ventana ni musica)"
                                   : "  FALLO segundatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    static void RelojQueSeAtrasa(object f)
    {
        DateTime ahora = DateTime.Now;
        Set(f, "lastInput", ahora.AddHours(1));
        Set(f, "searchLastActivity", ahora.AddMinutes(30));
        Set(f, "respiraDemoHasta", ahora.AddHours(1));
        Call(f, "RelojAtras");
        DateTime li = (DateTime)Get(f, "lastInput"), sa = (DateTime)Get(f, "searchLastActivity"), rd = (DateTime)Get(f, "respiraDemoHasta");
        DateTime despues = DateTime.Now;
        Check(li >= ahora && li <= despues, "con el reloj atrasado una hora, la ultima tecla cuenta desde ahora");
        Check(sa >= ahora && sa <= despues, "y la ultima actividad del buscador tambien (se cierra al minuto, no en media hora)");
        Check(rd <= despues.AddSeconds(7) && rd > ahora, "la vista previa de la respiracion no se alarga una hora");
        DateTime antes = ahora.AddMinutes(-3);
        Set(f, "lastInput", antes); Set(f, "searchLastActivity", antes); Set(f, "respiraDemoHasta", DateTime.MinValue);
        Call(f, "RelojAtras");
        Check((DateTime)Get(f, "lastInput") == antes && (DateTime)Get(f, "searchLastActivity") == antes, "con el reloj normal no se toca nada");
        Check((DateTime)Get(f, "respiraDemoHasta") == DateTime.MinValue, "ni la vista previa apagada");
    }

    static void Tick(Assembly asm)
    {
        // 2) La vuelta del contador de Windows.
        MethodInfo tv = form.GetMethod("TickVivo", B);
        Check(tv != null && tv.ReturnType == typeof(int), "existe TickVivo");
        if (tv != null) { int t = (int)tv.Invoke(null, null); Check(t != -1, "TickVivo no da el -1 de 'nada'"); }
        Dictionary<string, Type> duenos = new Dictionary<string, Type>();
        duenos["toastTick"] = form;
        foreach (Type t in form.GetNestedTypes(B))
        {
            if (t.Name == "AlbumView" || t.Name == "SongListView") duenos[t.Name + ".slideTick"] = t;
            if (t.Name == "SongPagerBar") duenos["animTick"] = t;
        }
        bool gpu = asm.GetType("Rocola.GpuPanel") != null;   // el aviso y el hojeo deslizado son de la tarjeta grafica
        Check(duenos.Count == 4, "estan los duenos de los cuatro relojes de animacion (" + duenos.Count + ")");
        List<MethodBase> todos = Codigo.TodosLosMetodos(form);
        foreach (KeyValuePair<string, Type> d in duenos)
        {
            string campo = d.Key.Contains(".") ? d.Key.Substring(d.Key.IndexOf('.') + 1) : d.Key;
            FieldInfo fi = d.Value.GetField(campo, B);
            if (fi == null && !gpu && d.Key != "animTick" && d.Key != "SongListView.slideTick") continue;   // sin tarjeta no existe
            Check(fi != null && fi.FieldType == typeof(int), d.Key + " es un entero");
            int malas = 0, escrituras = 0;
            foreach (MethodBase m in todos)
            {
                if (m.DeclaringType != d.Value) continue;
                // AlbumView.PeekOrBakeCell es del dibujo por CPU, congelado: lee slideTick por TickHojeo, no aqui.
                malas += ComparaConCero(m, campo);
                List<string> il = Codigo.IlConNombres(m);
                if (il == null) continue;
                for (int i = 1; i < il.Count; i++)
                    if (il[i].StartsWith("7D ") && il[i].EndsWith(" " + campo) && il[i - 1].Contains("get_TickCount")) escrituras++;
            }
            Check(malas == 0, d.Key + ": ninguna comparacion contra cero (" + malas + ")");
            Check(escrituras == 0, d.Key + ": se apunta con TickVivo, no con Environment.TickCount directo (" + escrituras + ")");
        }

    }

    static void Motivos()
    {
        // 3) El motivo del fallo, para el cliente.
        Func<Exception, string> motivo = delegate (Exception e) { return (string)form.GetMethod("MotivoFallo", B).Invoke(null, new object[] { e }); };
        string[][] casos = {
            new string[] { "nf", "no se encuentra el archivo" }, new string[] { "dnf", "no se encuentra el archivo" },
            new string[] { "eos", "incompleto" }, new string[] { "fmt", "dañado" }, new string[] { "io", "no se pudo leer" },
            new string[] { "acc", "no deja leer" }, new string[] { "ioe", "dañado" }, new string[] { "otra", "no se pudo abrir" } };
        foreach (string[] c in casos)
        {
            Exception e = c[0] == "nf" ? new FileNotFoundException("Could not find file \"x.mp3\"") :
                          c[0] == "dnf" ? (Exception)new DirectoryNotFoundException("Could not find a part of the path") :
                          c[0] == "eos" ? new EndOfStreamException("Unable to read beyond the end of the stream.") :
                          c[0] == "fmt" ? new FormatException("Not a WAVE file - no WAVE header") :
                          c[0] == "io" ? new IOException("The device is not ready") :
                          c[0] == "acc" ? new UnauthorizedAccessException("Access denied") :
                          c[0] == "ioe" ? new InvalidOperationException("Got a frame at sample rate 48000, in an MP3 with sample rate 8000.") :
                          new Exception("Something else");
            string s = motivo(e);
            Check(s != null && s.Contains(c[1]), c[0] + ": «" + s + "» dice " + c[1]);
            Check(s != null && s.IndexOf(e.Message, StringComparison.Ordinal) < 0 && s.Length <= 60, c[0] + ": corto y sin el mensaje en ingles");
        }
        foreach (string m in new string[] { "AudioStartFailed", "Output_PlaybackStopped", "StartVideoItem" })
        {
            MethodInfo mi = form.GetMethod(m, B);
            List<string> ll = Codigo.Llamadas(mi);
            Check(Codigo.Llama(ll, "MotivoFallo"), m + " dice el motivo en corto");
            int posMsg = -1, posEstado = -1;
            for (int i = 0; i < ll.Count; i++) { if (ll[i].Contains("get_Message") && posMsg < 0) posMsg = i; if (ll[i].Contains("EstadoPasajero") && posEstado < 0) posEstado = i; }
            Check(posEstado >= 0 && (posMsg < 0 || posMsg > posEstado), m + " no pone el mensaje de la excepcion en la linea de estado");
        }
        Check(Codigo.Llama(form.GetMethod("StartVideoItem", B), "LogError"), "el detalle del video que no abrio queda en rocola_error.txt");
    }
}
