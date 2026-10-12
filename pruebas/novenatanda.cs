using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// NOVENA TANDA DE FALLOS (12-10-2026):
//  1) La moneda se perdia con una ventana de ajustes abierta (Ctrl+F3, carpetas de musica, comparacion de caratulas):
//     el pulso del monedero le llegaba a esa ventana. Ahora cada una lo cuenta, un pulso = una moneda.
//  2) Con tarjeta grafica, tocar la pantalla en la atraccion no la despertaba (solo reiniciaba el reposo). Ahora la
//     despierta, y ese toque no abre nada debajo.
//  3) El reproductor de video ya no saca su menu (Pausa, Detener, Pantalla completa) con el boton derecho.
class NovenaTanda
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; Console.WriteLine("  FALLO " + s); } }
    static Type form;

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true);
        string tmp = Path.Combine(Path.GetTempPath(), "rocola_novena_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", tmp);
        try
        {
            Moneda();
            bool gpu = form.GetNestedType("AyudaView", B) != null;
            if (gpu) Toque();
            Video();
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        Console.WriteLine(mal == 0 ? "  OK novenatanda: " + ok + " comprobaciones (formulario aislado y codigo compilado; sin monedero ni pantalla tactil de verdad)"
                                   : "  FALLO novenatanda: " + mal + " de " + (ok + mal));
        return mal;
    }

    static void Tecla(Form dlg, string cual, Keys k)
    {
        MethodInfo m = typeof(Control).GetMethod(cual, B);
        try { m.Invoke(dlg, new object[] { new KeyEventArgs(k) }); }
        catch (TargetInvocationException) { }   // lo de despues de sumar (avisos, guardar) en un formulario aislado
    }

    static void Moneda()
    {
        MethodInfo aqui = form.GetMethod("MonedaTambienAqui", B);
        Check(aqui != null, "las ventanas de ajustes pueden contar la moneda");
        if (aqui == null) return;
        object f = Codigo.FormSinEncender(form);
        try { Codigo.PonColecciones(f); } catch { }
        FieldInfo cr = form.GetField("credits", B), kc = form.GetField("keyCoin", B);
        kc.SetValue(f, Keys.D5);
        int porMoneda = 4;
        FieldInfo cpc = form.GetField("CreditsPerCoin", B);
        if (cpc != null) porMoneda = Convert.ToInt32(cpc.GetValue(cpc.IsStatic ? null : f));
        using (Form dlg = new Form())
        {
            aqui.Invoke(f, new object[] { dlg });
            Check(dlg.KeyPreview, "la ventana mira las teclas antes que sus botones");
            cr.SetValue(f, 0);
            Tecla(dlg, "OnKeyDown", Keys.D5);
            Check((int)cr.GetValue(f) == porMoneda, "una moneda con la ventana abierta da sus creditos (" + cr.GetValue(f) + ")");
            Tecla(dlg, "OnKeyDown", Keys.D5); Tecla(dlg, "OnKeyDown", Keys.D5);
            Check((int)cr.GetValue(f) == porMoneda, "el pulso que se queda apretado (autorrepeticion) no suma mas");
            Tecla(dlg, "OnKeyUp", Keys.D5); Tecla(dlg, "OnKeyDown", Keys.D5);
            Check((int)cr.GetValue(f) == 2 * porMoneda, "soltar y volver a pulsar es otra moneda");
            Tecla(dlg, "OnKeyUp", Keys.D5);
            Tecla(dlg, "OnKeyDown", Keys.D6); Tecla(dlg, "OnKeyDown", Keys.Enter);
            Check((int)cr.GetValue(f) == 2 * porMoneda, "las demas teclas no dan creditos");
            KeyEventArgs e = new KeyEventArgs(Keys.D5);
            try { typeof(Control).GetMethod("OnKeyDown", B).Invoke(dlg, new object[] { e }); } catch (TargetInvocationException) { }
            Check(e.Handled && e.SuppressKeyPress, "la tecla de la moneda no le llega a los controles de la ventana");
            KeyEventArgs otra = new KeyEventArgs(Keys.D6);
            typeof(Control).GetMethod("OnKeyDown", B).Invoke(dlg, new object[] { otra });
            Check(!otra.Handled, "las demas teclas siguen siendo de la ventana");
        }
        foreach (string v in new string[] { "VentanaAjustes", "ShowMusicDirManager", "ShowCoverHistory" })
        {
            bool llama = false;
            foreach (MethodInfo m in form.GetMethods(B)) if (m.Name == v && Codigo.Llama(m, "MonedaTambienAqui")) llama = true;
            Check(llama, v + ": la moneda cuenta con esta ventana abierta");
        }
    }

    static void Toque()
    {
        // El toque en el panel de la tarjeta: hay un manejador que apunta el toque que despierta y quita la atraccion, y
        // el del clic se sale si ese toque fue el que desperto, ANTES de mirar la casilla de AYUDA.
        bool despierta = false, clicCalla = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(form))
        {
            List<string> il = Codigo.IlConNombres(m);
            if (il == null) continue;
            int iTq = il.FindIndex(delegate (string x) { return x.StartsWith("7D ") && x.Contains("toqueDespierta"); });
            int iSale = il.FindIndex(delegate (string x) { return x.Contains("ExitAttract"); });
            if (iTq >= 0 && iSale > iTq && m.Name != "ExitAttract") despierta = true;
            int iLee = il.FindIndex(delegate (string x) { return x.StartsWith("7B ") && x.Contains("toqueDespierta"); });
            int iAyuda = il.FindIndex(delegate (string x) { return x.Contains("AyudaTocada"); });
            if (iLee >= 0 && iAyuda > iLee) clicCalla = true;
        }
        Check(despierta, "tocar la pantalla en la atraccion la despierta");
        Check(clicCalla, "y ese mismo toque no abre la ayuda ni nada de debajo");
    }

    static void Video()
    {
        MethodInfo inicia = form.GetMethod("StartVideoItem", B);
        bool sinMenu = false;
        List<string> il = Codigo.IlConNombres(inicia);
        if (il != null) foreach (string x in il) if (x.Contains("\"enableContextMenu\"")) sinMenu = true;
        Check(sinMenu, "el reproductor de video sin su menu del boton derecho (Pausa, Detener, Pantalla completa)");
    }
}
