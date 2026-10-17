using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// CTRL+F3: DEPENDENCIAS VIVAS (A1) Y GUARDADO AL MOMENTO (A2), 13-10-2026. Y LOS AJUSTES RETIRADOS.
//
// A1. Un interruptor que depende de otro (a veces en otra pagina) se pone en gris y dice que le falta, y vuelve solo
//     en cuanto su requisito se enciende. Su valor guardado no se toca. Sin tarjeta grafica, lo que solo se dibuja con
//     ella va en gris y la pagina lo dice una vez; ningun nombre lleva ya "(GPU)".
// A2. Lo tocado se guarda en cuanto se deja quieto medio segundo, detras de la pantalla, y un guardado que llega tarde
//     no pisa a uno mas nuevo.
// Y LOS RETIRADOS: la pared que respira, "Respuesta inmediata al toque", "Transiciones suaves de color" y "Ondas unidas
// al estado" ya no estan, ni en la ventana ni en el archivo.
//
// Se arman las paginas de verdad (formulario aislado, datos aparte) con la letra ancha de Linux (DejaVu Sans). No son
// las letras de XP.
class AjustesVivos
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF; static object f; static int ok, mal;
    static void Check(bool c, string s) { Console.WriteLine((c ? "  OK    " : "  FALLO ") + s); if (c) ok++; else mal++; }
    static object Get(string n) { return TF.GetField(n, B).GetValue(TF.GetField(n, B).IsStatic ? null : f); }
    static void Set(string n, object v) { FieldInfo fi = TF.GetField(n, B); fi.SetValue(fi.IsStatic ? null : f, v); }
    static object Call(string n, params object[] a)
    { try { return TF.GetMethod(n, B).Invoke(f, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static void Todos(Control c, List<Control> l) { foreach (Control h in c.Controls) { l.Add(h); Todos(h, l); } }
    static Control Busca(List<Control> l, string texto) { foreach (Control c in l) if (c.Text == texto) return c; return null; }
    static string Necesita(Control c) { FieldInfo fi = c.GetType().GetField("Necesita", B); return fi == null ? null : (string)fi.GetValue(c); }
    static bool DosRenglones(Control c) { FieldInfo fi = c.GetType().GetField("DosRenglones", B); return fi != null && (bool)fi.GetValue(c); }
    static void Refresca() { Call("RefrescaRequisitos"); }

    // Los ajustes de los que depende algo, y su valor de fabrica (encendidos).
    static readonly string[] requisitos = { "estadoExpandido", "progresoNeon", "cancionesTransparentes", "fondoPortadaOn",
                                            "cieloProfundo", "lunaActiva", "paginaDesliza", "muroReposo",
                                            "cieloMedia",     // «Fondo en una sola capa» (G2, 14-10-2026) va dentro del cielo a media resolucion
                                            "vinetaSuave",    // y la pasada de grano y vineta necesita la vineta (14-10-2026)
                                            "estrella3d", "reflejoAurora", "estelaEstelar", "haloLunar", "lunaOn" };   // y el pulido de la estrella, la estrella en 3D (PulidoGpu.cs)

    [STAThread] static int Main(string[] args)
    {
        string datos = Path.Combine(Path.GetTempPath(), "rocola_ajustesvivos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(datos);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", datos);
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        bool gpu = asm.GetType("Rocola.GpuPanel", false) != null;
        TF.GetField("carpetaDatos", B).SetValue(null, datos);
        f = Codigo.FormSinEncender(TF);
        try { Call("LoadConfigPrefs"); } catch { }

        Console.WriteLine("\n  0) LOS AJUSTES RETIRADOS\n");
        foreach (string ido in new string[] { "respiraOn", "respiraFuerza", "respuestaInmediata", "coloresCache", "ondasUnidas" })
            Check(TF.GetField(ido, B) == null, "ya no existe " + ido);
        string texto = (string)Call("TextoConfig");
        foreach (string clave in new string[] { "respira=", "respira_fuerza=", "respuesta_inmediata=", "colores_cache=", "ondas_unidas=" })
            Check(texto.IndexOf("\n" + clave, StringComparison.Ordinal) < 0 && !texto.StartsWith(clave), "el archivo ya no lleva " + clave);
        // Una configuracion vieja con esas claves se lee sin quejarse, y al guardar ya no salen.
        File.WriteAllText(Path.Combine(datos, "rocola_config.txt"),
            "respira=0\r\nrespira_fuerza=6\r\nrespuesta_inmediata=0\r\ncolores_cache=0\r\nondas_unidas=0\r\nturno=0\r\n");
        object vieja = Codigo.FormSinEncender(TF);
        TF.GetMethod("LoadConfigPrefs", B).Invoke(vieja, null);
        Check(!(bool)TF.GetField("turnoOn", B).GetValue(null), "una configuracion vieja con las claves retiradas se lee bien (turno=0 vale)");
        string guardada = (string)TF.GetMethod("TextoConfig", B).Invoke(vieja, null);
        Check(guardada.IndexOf("respira", StringComparison.Ordinal) < 0 && guardada.IndexOf("ondas_unidas", StringComparison.Ordinal) < 0,
              "y al guardarla, las claves retiradas no se vuelven a escribir");
        TF.GetField("turnoOn", B).SetValue(null, true);
        File.Delete(Path.Combine(datos, "rocola_config.txt"));

        FontFamily ff;
        try { ff = new FontFamily("DejaVu Sans"); } catch { ff = FontFamily.GenericSansSerif; }
        using (Font fNom = new Font(ff, 10f, FontStyle.Bold))
        using (Font fVal = new Font(ff, 11f, FontStyle.Bold))
        using (Font fDes = new Font(ff, 8.5f))
        using (Font fBot = new Font(ff, 10f, FontStyle.Bold))
        using (Font chica = new Font(ff, 8.5f))
        using (Bitmap bmp = new Bitmap(8, 8))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            Call("OlvidaRequisitos");
            Color c1 = Color.White, c2 = Color.Gray, c3 = Color.DimGray;
            Panel todo = new Panel(); todo.SetBounds(0, 0, 960, 732);
            Panel pImg = (Panel)Call("PaginaImagen", fNom, fVal, fDes, fBot, c1, c2, c3);
            Panel pDet = (Panel)Call("PaginaDetalles", fNom, fDes, fBot, c1, c2, c3);
            Panel pCol = (Panel)Call("PaginaColores", fNom, fDes, fBot, c1, c2, c3);
            Panel pFon = (Panel)Call("PaginaFondo", fNom, fVal, fDes, fBot, c1, c2, c3);
            Panel pPan = (Panel)Call("PaginaPantalla", fNom, fDes, fBot, c1, c2, c3);
            foreach (Panel p in new Panel[] { pImg, pDet, pCol, pFon, pPan }) todo.Controls.Add(p);
            List<Control> ctl = new List<Control>(); Todos(todo, ctl);
            Call("EnganchaRequisitos", todo);

            Console.WriteLine("\n  1) LOS NOMBRES, SIN \"(GPU)\"\n");
            int conGpu = 0; foreach (Control c in ctl) if ((c.Text ?? "").IndexOf("(GPU)", StringComparison.Ordinal) >= 0) conGpu++;
            Check(conGpu == 0, "ningun mando ni nota lleva \"(GPU)\" en el nombre (" + conGpu + ")");

            IList reqs = (IList)Get("f3Requisitos");
            float espectroAntes = (float)Get("auroraAmount");
            Console.WriteLine("\n  2) LAS DEPENDENCIAS (" + reqs.Count + " mandos)\n");
            Check(reqs.Count >= 12, "estan apuntadas las dependencias conocidas (al menos 12 mandos)");
            Dictionary<string, bool> antes0 = new Dictionary<string, bool>();
            foreach (string r in requisitos) antes0[r] = (bool)Get(r);
            if (!gpu)
                Console.WriteLine("     (este ejecutable dibuja sin tarjeta grafica: casi todo lo que depende de algo es de la tarjeta y va en gris;\n"
                                + "      aqui se mira solo el apartado 4)");
            else {
            Set("gpuEnabled", true);
            Dictionary<string, bool> antes = new Dictionary<string, bool>();
            foreach (string r in requisitos) { antes[r] = (bool)Get(r); Set(r, true); }
            int nieblasAntes = (int)Get("nieblasNiv"); Set("nieblasNiv", 2); Set("auroraAmount", .55f);   // los ahorros del cielo necesitan las nebulosas
            Refresca();
            bool todosVivos = true;
            foreach (object r in reqs)
            {
                Control m = (Control)r.GetType().GetField("Mando").GetValue(r);
                if (!m.Enabled || Necesita(m) != null) todosVivos = false;
            }
            Check(todosVivos, "con todo encendido, ningun mando sale en gris ni dice que le falta algo");

            // Lo que valen los que dependen, para ver que ponerlos en gris no los toca.
            string[] dependientes = { "colaDoce", "neonIntenso", "estadoTransparente", "cancionesCristal", "paletaCompleta",
                                      "estelaEstelar", "controlesReposo", "profundidadPagina", "nebNaturalFuerza", "portadaManda",
                                      "fondoCapa", "cieloCuarto", "estadoOpacoPerf", "auroraLigeraPerf", "reposoSinReflejo", "reposoSinEstela", "coronaCompacta", "estadoColorDirecto", "sinGranoPerf", "auroraSinSuelo", "auroraHaloCompacto" };
            List<object> valAntes = new List<object>();
            foreach (string r in requisitos) Set(r, false);
            Set("auroraAmount", 0f);
            Set("nieblasNiv", 0);
            // Algunos requisitos (la estela) tambien son dependientes: capturar despues
            // de preparar las entradas y antes de refrescar, para medir solo el efecto del gris.
            foreach (string d in dependientes) valAntes.Add(Get(d));
            Refresca();
            int grises = 0, dicen = 0, caben = 0, cabenTotal = 0, alto = 0;
            foreach (object r in reqs)
            {
                Control m = (Control)r.GetType().GetField("Mando").GetValue(r);
                Label aviso = (Label)r.GetType().GetField("Aviso").GetValue(r);
                string nec = (string)r.GetType().GetField("Necesita").GetValue(r);
                if (!m.Enabled) grises++;
                if (aviso != null)
                {
                    if (aviso.Text == nec) dicen++;
                    cabenTotal++;
                    if (g.MeasureString(aviso.Text, aviso.Font, aviso.Width).Height <= aviso.Height + 1) caben++;
                    else Console.WriteLine("     no cabe la nota: " + aviso.Text.Replace("\n", " | "));
                }
                else if (m.GetType().Name == "Interruptor")
                {
                    if (Necesita(m) == nec) dicen++;
                    cabenTotal++;
                    float ancho = DosRenglones(m) ? g.MeasureString(nec, chica).Width
                                                  : g.MeasureString(m.Text, m.Font).Width + 4 + g.MeasureString(nec, chica).Width;
                    if (ancho + 50 <= m.Width) caben++;
                    else Console.WriteLine("     no cabe: «" + m.Text + "» + «" + nec + "» (" + Math.Ceiling(ancho + 50) + " de " + m.Width + ")");
                    if (DosRenglones(m) && m.Height < 36) alto++;
                }
                else dicen++;   // los botones de Colores: el primero lo dice en su nota, los otros dos solo van en gris
            }
            Check(grises == reqs.Count, "con sus requisitos apagados, todos salen en gris (" + grises + " de " + reqs.Count + ")");
            Check(dicen == reqs.Count, "y cada uno dice que le falta, en su renglon o en su nota");
            Check(caben == cabenTotal, "y lo que dicen cabe con la letra ancha (" + caben + " de " + cabenTotal + ")");
            Check(alto == 0, "los que lo dicen debajo del nombre tienen sitio para el segundo renglon");
            // Ninguno se sale de su tarjeta ni pisa a otro al crecer.
            int choques = 0;
            foreach (object r in reqs)
            {
                Control m = (Control)r.GetType().GetField("Mando").GetValue(r);
                ScrollableControl scroll = m.Parent as ScrollableControl;
                int bottom = scroll != null && scroll.AutoScroll ? scroll.DisplayRectangle.Bottom : m.Parent.ClientSize.Height;
                if (m.Bottom > bottom) choques++;
                foreach (Control o in m.Parent.Controls) if (o != m && o.Visible && o.Bounds.IntersectsWith(m.Bounds)) choques++;
            }
            Check(choques == 0, "al crecer, ninguno se sale de su tarjeta ni pisa a otro");
            bool valores = true;
            for (int i = 0; i < dependientes.Length; i++) if (!object.Equals(Get(dependientes[i]), valAntes[i])) valores = false;
            Check(valores, "ponerlos en gris no cambia ningun valor guardado");

            Console.WriteLine("\n  3) VIVAS: ENTRE PAGINAS, AL MOMENTO\n");
            foreach (string r in requisitos) Set(r, true);
            Refresca();
            Control expandido = Busca(ctl, "Estado ampliado en reposo"), doce = Busca(ctl, "Cola ampliada de 12 canciones");
            Control transp = Busca(ctl, "Estado ampliado transparente"), ocultar = Busca(ctl, "Ocultar controles en reposo");
            Control cristal = Busca(ctl, "Interior de discos con cristal"), lista = Busca(ctl, "Lista de canciones transparente");
            Control portada = Busca(ctl, "Que el fondo tome el color de la portada"), paleta = Busca(ctl, "Paleta completa de caratula");
            Check(expandido != null && doce != null && transp != null && ocultar != null && cristal != null && lista != null
                  && portada != null && paleta != null, "estan los mandos de las dos puntas");
            if (expandido != null && doce != null && transp != null && ocultar != null && cristal != null && lista != null && portada != null && paleta != null)
            {
                PropertyInfo marca = expandido.GetType().GetProperty("Checked");
                foreach (Control c in new Control[] { expandido, lista, portada }) marca.SetValue(c, true, null);
                Control muro0 = Busca(ctl, "Ocultar discos en reposo con musica"); if (muro0 != null) marca.SetValue(muro0, true, null);
                marca.SetValue(expandido, false, null);   // en Imagen
                Check(!doce.Enabled && !transp.Enabled && !ocultar.Enabled, "apagar el estado ampliado (Imagen) pone en gris al momento sus tres de Fondo");
                Check(Necesita(doce) == "Necesita: Estado ampliado en reposo (Imagen)", "y dicen que les falta y en que pagina esta");
                Check(cristal.Enabled && paleta.Enabled, "los que no dependen de el siguen como estaban");
                marca.SetValue(expandido, true, null);
                Check(doce.Enabled && transp.Enabled && ocultar.Enabled && Necesita(doce) == null, "y al encenderlo vuelven solos");
                bool transparenteAntes = (bool)Get("estadoTransparente");
                Set("estadoOpacoPerf", true); Refresca();
                Check(!transp.Enabled && Necesita(transp) == "Sin efecto: estado opaco (Performance)"
                      && (bool)Get("estadoTransparente") == transparenteAntes, "el estado opaco deja la transparencia en gris sin cambiarla");
                Set("estadoOpacoPerf", false); Refresca();
                Check(transp.Enabled && Necesita(transp) == null, "la transparencia vuelve al quitar el ahorro opaco");
                marca.SetValue(lista, false, null);
                Check(!cristal.Enabled && Necesita(cristal) == "Necesita: Lista de canciones transparente (Imagen)", "el cristal, con la lista transparente (Imagen)");
                marca.SetValue(lista, true, null);
                marca.SetValue(portada, false, null);   // en Detalles
                Check(!paleta.Enabled, "la paleta completa (Fondo), con el color de la portada (Detalles)");
                int bGris = 0; foreach (string t in new string[] { "Disco 20%", "Mitad y mitad", "Disco 100%" }) { Control b = Busca(ctl, t); if (b != null && !b.Enabled) bGris++; }
                Check(bGris == 3, "y los tres pesos del disco (Colores), igual");
                marca.SetValue(portada, true, null);
                Check(paleta.Enabled, "y vuelven");
                Control muro = Busca(ctl, "Ocultar discos en reposo con musica");
                if (muro != null)
                {
                    marca.SetValue(muro, false, null);
                    Check(!expandido.Enabled && Necesita(expandido) == "Necesita: la de arriba", "el estado ampliado, con ocultar la pared en reposo (justo encima)");
                    marca.SetValue(muro, true, null);
                }
            }

            // LO QUE ANULA PERFORMANCE (14-10-2026): un ahorro que ahora no hace nada va en gris y dice por que.
            {
                foreach (string r in requisitos) Set(r, true);
                Set("nieblasNiv", 2); Set("fondoCapa", false);
                Refresca();
                Control g1 = Busca(ctl, "Grano y viñeta en una pasada"), g2 = Busca(ctl, "Fondo en una sola capa"), j2 = Busca(ctl, "Cielo a un cuarto de resolución");
                Check(g1 != null && g2 != null && j2 != null && g1.Enabled && g2.Enabled && j2.Enabled, "Performance: con todo lo suyo, los ahorros vivos");
                if (g1 != null && g2 != null && j2 != null)
                {
                    Control sinGrano = Busca(ctl, "Fondo sin grano"), sinSuelo = Busca(ctl, "Aurora sin velo inferior");
                    Check(sinGrano != null && sinSuelo != null, "estan los ahorros nuevos para resoluciones mayores");
                    if (sinGrano != null && sinSuelo != null)
                    {
                        PropertyInfo marca = sinGrano.GetType().GetProperty("Checked");
                        marca.SetValue(sinGrano, true, null);
                        Check(!g1.Enabled && Necesita(g1) == "Sin efecto: Fondo sin grano (Performance)" && g2.Enabled,
                              "sin grano anula la pasada doble, pero no el fondo en una capa");
                        marca.SetValue(sinGrano, false, null);
                        Check(g1.Enabled, "al restaurar el grano vuelve la pasada doble con su valor");
                        Set("auroraAmount", 0f); Refresca();
                        Check(!sinSuelo.Enabled && sinGrano.Enabled, "el velo necesita aurora; quitar grano no tiene requisitos adicionales");
                        Set("auroraAmount", .55f); Refresca();
                        Check(sinSuelo.Enabled, "la aurora reactiva su ahorro del velo inferior");
                    }
                    Control compacto = Busca(ctl, "Halo de aurora compacto");
                    Check(compacto != null && compacto.Enabled, "halo compacto disponible con aurora activa");
                    if (compacto != null)
                    {
                        PropertyInfo marca = compacto.GetType().GetProperty("Checked");
                        marca.SetValue(compacto, true, null);
                        Set("auroraAmount", 0f); Refresca();
                        Check(!compacto.Enabled && Necesita(compacto) == "Necesita: Aurora del horizonte en Medio o Alto"
                            && (bool)Get("auroraHaloCompacto") && (bool)marca.GetValue(compacto, null),
                            "halo en gris conserva su valor y explica el requisito exacto");
                        Set("auroraAmount", .55f); Refresca();
                        Check(compacto.Enabled && Necesita(compacto) == null && (bool)marca.GetValue(compacto, null),
                            "encender aurora restaura el halo compacto sin perder la preferencia");
                        foreach (bool value in new bool[] { false, true })
                        {
                            Set("auroraLigeraPerf", value); Set("auroraSinSuelo", value); Set("estadoOpacoPerf", value); Refresca();
                            Check(compacto.Enabled, "halo combinable con columnas, velo y estado opaco: " + value);
                        }
                        Set("estadoOpacoPerf", false); marca.SetValue(compacto, false, null);
                    }
                    Set("fondoCapa", true); Refresca();
                    Check(!g1.Enabled && Necesita(g1) == "Sin efecto: ya va dentro del cielo (Fondo en una sola capa)",
                          "con el fondo en una sola capa, la pasada de grano y vineta va en gris: ya no hace nada (lo dice)");
                    Set("fondoCapa", false); Set("vinetaSuave", false); Refresca();
                    Check(!g1.Enabled && Necesita(g1) == "Necesita: Viñeta suave (Fondo)", "sin la vineta (Fondo), la pasada de grano y vineta, en gris");
                    Set("vinetaSuave", true); Set("cieloMedia", false); Refresca();
                    Check(!g2.Enabled && !j2.Enabled && Necesita(j2) == "Necesita: Cielo a media resolución (arriba)", "sin el cielo a media resolucion, el fondo en una capa y el cuarto, en gris");
                    Set("cieloMedia", true); Set("nieblasNiv", 0); Refresca();
                    Control c0 = Busca(ctl, "Cielo a media resolución");
                    Check(c0 != null && !c0.Enabled && !g2.Enabled && !j2.Enabled && Necesita(c0) == "Necesita: Nieblas y nebulosas en Medio o Alto (Efectos)",
                          "con las nebulosas en Bajo, los ahorros del cielo no hacen nada: en gris");
                    Set("nieblasNiv", 2); Refresca();
                    Check(c0 != null && c0.Enabled && g2.Enabled && j2.Enabled && g1.Enabled, "y vuelven solos");
                }
            }

            // Los ahorros de reposo necesitan toda la cadena: no solo la preferencia hija.
            // Cambiar controles reales verifica el refresco entre paginas y que el gris no borra valores.
            {
                Set("auroraAmount", .55f);
                foreach (string r in requisitos) Set(r, true);
                Control opaco = Busca(ctl, "Estado extendido opaco"), color = Busca(ctl, "Color directo del estado extendido");
                Control sinReflejo = Busca(ctl, "Sin reflejo de aurora en reposo"), sinEstela = Busca(ctl, "Sin estela de estrella en reposo");
                Control corona = Busca(ctl, "Corona de estrella compacta"), ligera = Busca(ctl, "Aurora con menos columnas");
                Control muro = Busca(ctl, "Ocultar discos en reposo con musica");
                Control estadoNuevo = Busca(ctl, "Estado ampliado en reposo");
                Control ecos = Busca(ctl, "Ecos de color de la estrella"), halo = Busca(ctl, "Corona estelar");
                Check(opaco != null && color != null && sinReflejo != null && sinEstela != null && corona != null && ligera != null,
                      "estan los seis ahorros nuevos para comprobar sus conflictos vivos");
                if (opaco != null && color != null && sinReflejo != null && sinEstela != null && corona != null && ligera != null)
                {
                    PropertyInfo marca = opaco.GetType().GetProperty("Checked");
                    foreach (Control c in new Control[] { muro, estadoNuevo, ecos, halo, color, sinReflejo, sinEstela, corona, ligera })
                        marca.SetValue(c, true, null);
                    marca.SetValue(opaco, true, null);
                    Check(!sinReflejo.Enabled && Necesita(sinReflejo).StartsWith("Sin efecto:")
                          && (bool)Get("reposoSinReflejo"), "estado opaco anula solo el ahorro redundante de reflejo y conserva su valor");
                    marca.SetValue(opaco, false, null);
                    Check(sinReflejo.Enabled, "al quitar el opaco se puede volver a ajustar el reflejo");
                    marca.SetValue(muro, false, null);
                    bool cadena = true;
                    foreach (Control c in new Control[] { opaco, color, sinReflejo, sinEstela,
                        Busca(ctl, "Estado ampliado transparente"), Busca(ctl, "Cola ampliada de 12 canciones"), Busca(ctl, "Ocultar controles en reposo") })
                        if (c.Enabled || Necesita(c) != "Necesita: Ocultar discos en reposo (Imagen)") cadena = false;
                    Check(cadena && (bool)Get("estadoExpandido"), "sin ocultar discos toda la cadena del estado queda gris aunque su preferencia siga encendida");
                    Check(corona.Enabled && ligera.Enabled, "corona y aurora siguen disponibles: tambien actuan fuera del estado extendido");
                    Check((bool)Get("estadoColorDirecto") && (bool)Get("reposoSinEstela"), "la dependencia indirecta no borra ahorros guardados");
                    marca.SetValue(muro, true, null);
                    Check(opaco.Enabled && color.Enabled && sinReflejo.Enabled && sinEstela.Enabled,
                          "restaurar el requisito padre reactiva todos los ahorros al momento");
                    marca.SetValue(estadoNuevo, false, null);
                    Check(!opaco.Enabled && !color.Enabled && !sinReflejo.Enabled && !sinEstela.Enabled
                          && Necesita(color) == "Necesita: Estado ampliado en reposo (Imagen)", "sin estado ampliado se indica ese requisito concreto");
                    marca.SetValue(estadoNuevo, true, null);
                    Set("auroraAmount", 0f); Refresca();
                    bool aurora = true;
                    foreach (Control c in new Control[] { ligera, sinReflejo, Busca(ctl, "Aurora con bandas mas definidas"),
                        Busca(ctl, "Ondas suaves"), Busca(ctl, "Reflejo de la aurora"), Busca(ctl, "Aurora mas limpia") })
                        if (c.Enabled || Necesita(c) != "Necesita: Aurora del horizonte en Medio o Alto") aurora = false;
                    Check(aurora, "aurora apagada deja en gris su ahorro, reflejo y adornos con el motivo exacto");
                    Call("PonNivel", "espectro", 1); Refresca();
                    Check(ligera.Enabled && sinReflejo.Enabled, "aurora en Medio reactiva los ahorros");
                    Set("reflejoAurora", false); Refresca();
                    Check(!sinReflejo.Enabled && Necesita(sinReflejo) == "Necesita: Reflejo de la aurora (Fondo / Cielo vivo)",
                          "el ahorro de reflejo nombra su interruptor cuando falta");
                    Set("reflejoAurora", true); Set("lunaOn", false); Refresca();
                    Check(!corona.Enabled && !sinEstela.Enabled && Necesita(corona) == "Necesita: Astro principal en el cielo (Detalles)",
                          "sin estrella ambos ahorros dicen donde encenderla");
                    Set("lunaOn", true); Set("lunaActiva", false); Refresca();
                    Check(!sinEstela.Enabled && Necesita(sinEstela) == "Necesita: Estrella viajera con orbitas (Fondo)" && corona.Enabled,
                          "la estela necesita movimiento; la corona no");
                    Set("lunaActiva", true); marca.SetValue(ecos, false, null);
                    Check(!sinEstela.Enabled && Necesita(sinEstela) == "Necesita: Ecos de color de la estrella (Fondo)",
                          "sin ecos el ahorro de estela indica ese interruptor");
                    marca.SetValue(ecos, true, null); marca.SetValue(halo, false, null);
                    Check(!corona.Enabled && Necesita(corona) == "Necesita: Corona estelar (Fondo / Estrella)",
                          "sin halo el ahorro de corona indica su requisito");
                    marca.SetValue(halo, true, null);
                    Check(corona.Enabled && sinEstela.Enabled && (bool)Get("coronaCompacta") && (bool)Get("reposoSinEstela"),
                          "al restaurar vuelven activos con sus valores guardados");
                    // Estos ahorros solo suprimen reflejo/estela EN REPOSO. Sus mandos originales
                    // siguen sirviendo al navegar: no crear un bloqueo circular de controles.
                    marca.SetValue(opaco, true, null);
                    Check(ecos.Enabled && Busca(ctl, "Reflejo de la aurora").Enabled && halo.Enabled,
                          "los mandos globales siguen editables porque actuan tambien al navegar");
                    marca.SetValue(opaco, false, null);
                    foreach (Control c in new Control[] { color, sinReflejo, sinEstela, corona, ligera }) marca.SetValue(c, false, null);
                }
            }

            foreach (KeyValuePair<string, bool> kv in antes) Set(kv.Key, kv.Value);
            Set("nieblasNiv", nieblasAntes);
            }

            Console.WriteLine("\n  4) SIN TARJETA GRAFICA\n");
            foreach (string r in requisitos) Set(r, true);   // que no falte nada mas que la tarjeta
            Set("nieblasNiv", 2); Set("auroraAmount", .55f);
            IList solo = (IList)Get("f3SoloTarjeta"); IList avisos = (IList)Get("f3AvisoTarjeta");
            Check(solo.Count >= 60, "lo que solo se dibuja con tarjeta esta apuntado (" + solo.Count + " mandos)");
            Check(avisos.Count >= 4, "y hay un aviso por pagina u hoja (" + avisos.Count + ")");
            if (gpu) Set("gpuEnabled", false);
            Refresca();
            int vivos = 0; foreach (Control c in solo) if (c.Enabled) vivos++;
            int avisosVistos = 0; foreach (Label l in avisos) if (l.Visible || !l.Parent.Visible) avisosVistos++;
            int necesitaGpu = 0; foreach (Control c in solo) if (Necesita(c) != null) necesitaGpu++;
            Check(vivos == 0, "sin tarjeta, todos en gris (" + vivos + " vivos)");
            Check(avisosVistos == avisos.Count, "y cada pagina lo dice una vez");
            Check(necesitaGpu == 0, "sin que cada mando repita lo que le falta");
            int avisoCabe = 0; foreach (Label l in avisos) if (g.MeasureString(l.Text, l.Font, l.Width).Height <= l.Height + 1) avisoCabe++;
            Check(avisoCabe == avisos.Count, "y el aviso cabe en su sitio");
            Control b20 = Busca(ctl, "Disco 20%");
            Set("fondoPortadaOn", false); Refresca();
            Check(b20 != null && !b20.Enabled, "lo que no es de la tarjeta sigue con sus dependencias");
            Set("fondoPortadaOn", true); Refresca();
            Check(b20 != null && b20.Enabled, "y sin tarjeta sigue vivo si no le falta nada (los colores valen tambien sin ella)");
            if (gpu)
            {
                Set("gpuEnabled", true); Refresca();
                vivos = 0; foreach (Control c in solo) if (c.Enabled) vivos++;
                Check(vivos == solo.Count, "con la tarjeta, vuelven todos");
            }
            foreach (KeyValuePair<string, bool> kv in antes0) Set(kv.Key, kv.Value);
            Set("auroraAmount", espectroAntes);
        }

        Console.WriteLine("\n  5) GUARDAR AL MOMENTO\n");
        string cfg = Path.Combine(datos, "rocola_config.txt");
        Call("EmpiezaGuardadoAlMomento");
        Check(!(bool)Call("GuardaAlMomento"), "sin cambios, no escribe nada");
        Set("turnoOn", false);
        Check(!(bool)Call("GuardaAlMomento"), "recien cambiado, espera medio segundo (una barra que se arrastra no escribe en cada paso)");
        Check((bool)Call("GuardaAlMomento"), "quieto, lo guarda");
        Codigo.EsperaGuardado(TF, f);
        string escrito = File.Exists(cfg) ? File.ReadAllText(cfg) : "";
        Check(escrito.Contains("turno=0"), "y queda en el archivo sin cerrar la ventana");
        Check(!(bool)Call("GuardaAlMomento"), "y no lo vuelve a escribir si no cambia nada");
        // Un temporal bloqueado no debe hacer creer que ya se escribio. Sin dormir la prueba: mover solo el reloj
        // de reintento, no el de Windows ni el del audio.
        Set("turnoOn", true);
        Check(!(bool)Call("GuardaAlMomento"), "el cambio tras el primer guardado se estabiliza");
        using (FileStream bloqueado = new FileStream(cfg + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            Check((bool)Call("GuardaAlMomento"), "encola el intento con el temporal bloqueado");
            Codigo.EsperaGuardado(TF, f);
            Check(File.ReadAllText(cfg) == escrito && (bool)Get("f3Reintenta"), "el fallo conserva el archivo y queda pendiente");
            Call("EmpiezaGuardadoAlMomento");
            Check((string)Get("f3Guardado") == escrito, "reabrir la ventana no confirma el guardado que fallo");
            Check(!(bool)Call("GuardaAlMomento"), "durante la pausa no llena la cola con reintentos");
        }
        Set("f3FalloTick", unchecked(Environment.TickCount - 2000));
        Check((bool)Call("GuardaAlMomento"), "al liberar el temporal reintenta sin otro cambio ni cerrar Ctrl+F3");
        Codigo.EsperaGuardado(TF, f);
        Check(File.ReadAllText(cfg).Contains("turno=1") && !(bool)Get("f3Reintenta"), "el reintento queda escrito y confirmado");
        Check(!(bool)Call("GuardaAlMomento"), "el exito no se vuelve a escribir");
        // Si el cierre guarda antes que un trabajo pendiente, el antiguo ni escribe ni confirma su texto.
        Set("turnoOn", false); Call("GuardaAlMomento");
        object cerrojo = Get("configEscribe");
        lock (cerrojo)
        {
            Check((bool)Call("GuardaAlMomento"), "un trabajo pendiente espera al escritor");
            Check(!(bool)Call("GuardaAlMomento"), "no duplica el trabajo pendiente");
            Set("turnoOn", true); Call("SaveConfigPrefs");
        }
        Codigo.EsperaGuardado(TF, f);
        Check(File.ReadAllText(cfg).Contains("turno=1"), "un guardado al cerrar mas nuevo conserva prioridad");
        Check((string)Get("f3Guardado") == File.ReadAllText(cfg), "el guardado al cerrar tambien actualiza la confirmacion");
        // Un guardado de detras que llega tarde no pisa a uno mas nuevo.
        long turnoViejo = Convert.ToInt64(Get("configTurno")) + 1;
        Set("turnoOn", true); Call("SaveConfigPrefs"); Codigo.EsperaGuardado(TF, f);
        string nuevo = File.ReadAllText(cfg);
        Call("EscribeConfigEnTurno", escrito, turnoViejo);
        Check(File.ReadAllText(cfg) == nuevo && nuevo.Contains("turno=1"), "un guardado viejo que llega tarde no pisa al nuevo");
        Set("turnoOn", true);

        Console.WriteLine("\n  6) LA VENTANA LO USA\n");
        List<string> ventana = Codigo.ConAyudantes(TF, "VentanaAjustes");
        Check(Codigo.Llama(ventana, "EnganchaRequisitos") && Codigo.Llama(ventana, "RefrescaRequisitos"), "Ctrl+F3 engancha y pone las dependencias al abrir");
        Check(Codigo.Llama(ventana, "GuardaAlMomento") && Codigo.Llama(ventana, "EmpiezaGuardadoAlMomento"), "y guarda al momento mientras esta abierta");
        Check(Codigo.Llama(ventana, "Necesita"), "y el DSEE por genero (Sonido) depende del DSEE");
        Check(Codigo.Llama(ventana, "SaveConfigPrefs"), "y al cerrar guarda otra vez, como siempre");

        try { Directory.Delete(datos, true); } catch { }
        Console.WriteLine(mal == 0 ? "PASS ajustesvivos (" + ok + " comprobaciones; letra de Linux, no las de XP)" : "FAIL ajustesvivos: " + mal + " fallos");
        return mal;
    }
}
