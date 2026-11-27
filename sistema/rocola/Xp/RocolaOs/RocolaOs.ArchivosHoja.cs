// ROCOLA OS (fase 11): LA HOJITA «ARCHIVOS» DE LA VENTANA DE CTRL+F4 (peticion del dueno del 20-11-2026; FASE11_navegador-archivos.md). Lo de
// fuera de la hoja (las raices, la regla de no salir, las llaves, copiar, guardar...) esta en RocolaOs.Archivos.cs; la lista y el editor, en
// RocolaOs.ArchivosPiezas.cs.
//   Arriba, un boton por raiz: «Datos de la rocola» y cada memoria USB (de solo lectura; se miran cada 2 s con la ventana abierta). Debajo,
//   donde se esta, la lista (carpetas primero; nombre, tamano y fecha; la llave con su icono) y los botones: Abrir (entrar en la carpeta o
//   abrir el texto), Subir, Copiar a los datos (desde una USB, a la carpeta de los datos donde se estuvo por ultimo), Renombrar, Borrar (con
//   confirmacion: manda a la PAPELERA) y Archivo nuevo (en los datos). Abajo, lo que paso. Ademas de los datos y las USB, un apartado: la
//   PAPELERA (Ver, Devolver, Borrar ya y Vaciar papelera, con confirmacion; RocolaOs.Papelera.cs). (El apartado DESCARGAS, de lo que bajaba el
//   navegador, se quito el 27-11-2026 con el navegador.) Lo de la papelera y copiar, en un hilo (nunca en el de la pantalla).
//   El texto se abre en su hoja (hasta 64 KB): se escribe con el teclado; «Guardar» (o Ctrl+S) lo guarda de un tiron; las llaves, tapadas
//   salvo con «Mostrar» (pide confirmar) y guardadas 0600; Escape cierra (si hay cambios sin guardar, pregunta).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraArchivos()
        {
            int latidos = 0;
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsArchivos.Nombre, Que = "el gestor de archivos de Ctrl+F4 (los datos de la rocola, la papelera y las USB)", DeFabrica = true,
                Claves = new RocolaOsClave[] {
                    RocolaOsClave.Entero(RocolaOsPapelera.ClaveTope, 64, 16, 256, true, "el tope de la papelera de Archivos (MB; va en lo aprendido, datos.img, de 512 MB): si lo pasa, se borra lo mas viejo primero; lo de mas de 30 dias, siempre"),
                },
                // (la papelera se vacia sola: al arrancar y cada hora, en un hilo)
                Arranca = f => RocolaOsPapelera.PurgaDeFondo("al arrancar"),
                Latido = f => { if (++latidos % 3600 == 0) RocolaOsPapelera.PurgaDeFondo("cada hora"); },
            });
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Archivos", Orden = 30, Arma = (f, x) => f.RocolaOsPaginaArchivos(x) });
        }

        private RocolaOsPagina RocolaOsPaginaArchivos(RocolaOsVentanaCtx x)
        {
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            bool va = RocolaOs.Encendida(RocolaOsArchivos.Nombre);
            Rocola.Nativo.Letra fLista = Rocola.Nativo.Letra.EnPuntos(FamiliaUi(), 10f), fChica = Rocola.Nativo.Letra.EnPuntos(FamiliaUi(), 8.5f, true);
            RocolaOsRaiz datos = RocolaOsArchivos.Datos();
            RocolaOsRaiz papelera = RocolaOsArchivos.Papelera();
            RocolaOsRaiz raiz = datos;
            string dir = datos.Ruta, dirDatos = datos.Ruta;
            string estado = null; bool estadoBien = false;
            bool ocupado = false, cerrada = false;
            List<RocolaOsTirado> tirados = new List<RocolaOsTirado>();

            // los botones de los apartados (hasta 5), arriba: los datos, la papelera y las USB
            List<BotonPlano> bRaiz = new List<BotonPlano>();
            List<RocolaOsRaiz> raices = new List<RocolaOsRaiz>();
            for (int i = 0; i < 5; i++)
            {
                BotonPlano b = new BotonPlano(); b.SetBounds(16 + i * 186, 4, 178, 34); b.Font = x.FBot; RocolaOsVe(b, false);
                pg.Controls.Add(b); bRaiz.Add(b);
            }
            Rotulo lDonde = new Rotulo(); lDonde.SetBounds(18, 46, 700, 20); lDonde.BackColor = Color.Transparent; lDonde.ForeColor = x.CTexto; lDonde.Font = x.FNom; lDonde.UseMnemonic = false;
            Rotulo lLibre = new Rotulo(); lLibre.SetBounds(724, 47, 220, 18); lLibre.BackColor = Color.Transparent; lLibre.ForeColor = x.CTenue; lLibre.Font = x.FDes; lLibre.TextAlign = AlineaTexto.TopRight; lLibre.UseMnemonic = false;
            pg.Controls.Add(lDonde); pg.Controls.Add(lLibre);
            RocolaOsListaArchivos lista = new RocolaOsListaArchivos(); lista.SetBounds(16, 70, 928, 396); lista.Letra = fLista; lista.LetraChica = fChica;
            pg.Controls.Add(lista);
            string[] nb = { "Abrir", "Subir", "Copiar a los datos", "Renombrar", "Borrar", "Archivo nuevo" };
            int[] anchos = { 130, 120, 190, 150, 130, 160 };
            BotonPlano[] bs = new BotonPlano[nb.Length];
            for (int i = 0, bx = 16; i < nb.Length; bx += anchos[i] + 8, i++)
            {
                bs[i] = new BotonPlano(); bs[i].SetBounds(bx, 474, anchos[i], 38); bs[i].Font = x.FBot; bs[i].Text = nb[i];
                pg.Controls.Add(bs[i]);
            }
            // (en la papelera los mismos seis, con otro texto: Ver, Subir, Devolver, Renombrar, Borrar ya, Vaciar papelera)
            BotonPlano bAbrir = bs[0], bSubir = bs[1], bCopiar = bs[2], bRen = bs[3], bBorra = bs[4], bNuevo = bs[5];
            bBorra.Relleno = Color.FromArgb(70, 34, 44); bBorra.Borde = Color.FromArgb(170, 84, 96);
            Rotulo lEstado = new Rotulo(); lEstado.SetBounds(18, 520, 926, 54); lEstado.BackColor = Color.Transparent; lEstado.Font = x.FDes; lEstado.UseMnemonic = false;
            pg.Controls.Add(lEstado);

            int fuera = 0;
            Action pintaBotones = null, cargaPapelera = null;
            Func<long, string> fecha = ms => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            Action<string> pintaDonde = delegate (string donde)
            {
                // (si no cabe, se corta por la izquierda: lo que importa es donde se esta)
                while (donde.Length > 8 && TextoGdiPlus.Mide(donde, x.FNom, AjusteLetra.ClearTypeAjustada, new SizeF(0, 0), FormatoTexto.SinFormato).Width > 690) donde = "..." + donde.Substring(Math.Min(donde.Length - 4, 8));
                lDonde.Text = donde;
                long lib = RocolaOsArchivos.Libre(dirDatos);
                lLibre.Text = lib >= 0 ? "En los datos quedan " + RocolaOsArchivos.Medida(lib) : "";
            };
            Action<string, string> carga = delegate (string carpeta, string elegir)
            {
                if (raiz.Tipo == RocolaOsArchivos.TPapelera) { cargaPapelera(); return; }
                lista.TituloFecha = "Fecha";
                string err;
                List<RocolaOsEntrada> l = RocolaOsArchivos.Lista(raiz, carpeta, out fuera, out err);
                if (err != null) { estado = err; estadoBien = false; if (carpeta != raiz.Ruta) { carpeta = raiz.Ruta; l = RocolaOsArchivos.Lista(raiz, carpeta, out fuera, out err); } }
                dir = carpeta;
                if (raiz.Tipo == RocolaOsArchivos.TDatos) dirDatos = carpeta;
                int e = 0;
                if (elegir != null) for (int i = 0; i < l.Count; i++) if (l[i].Nombre == elegir) e = i;
                if (elegir == null && l.Count > 0 && l[0].Subir && l.Count > 1) e = 1;
                lista.Pon(l, l.Count > 0 ? e : -1);
                string rel = dir.Length > raiz.Ruta.Length ? dir.Substring(raiz.Ruta.Length) : "";
                string donde = raiz.Tipo == RocolaOsArchivos.TUsb ? raiz.Nombre + " (solo lectura)" + (rel.Length == 0 ? "" : " " + rel) : raiz.Ruta + rel;
                pintaDonde(donde);
                pintaBotones();
            };
            Action pintaRaices = delegate
            {
                for (int i = 0; i < bRaiz.Count; i++)
                {
                    BotonPlano b = bRaiz[i];
                    if (i >= raices.Count) { RocolaOsVe(b, false); continue; }
                    bool act = raices[i].Clave == raiz.Clave;
                    RocolaOsVe(b, true); b.Text = raices[i].Nombre;
                    b.Relleno = act ? Color.FromArgb(28, 62, 104) : Color.FromArgb(28, 34, 62);
                    b.Borde = act ? Color.FromArgb(150, 205, 255) : Color.FromArgb(60, 72, 118);
                    b.ForeColor = act ? Color.FromArgb(226, 240, 255) : x.CApagado;
                    b.Invalidate();
                }
            };
            pintaBotones = delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                bool hay = e != null && !e.Subir;
                bool p = raiz.Tipo == RocolaOsArchivos.TPapelera, li = va && !ocupado;
                bool desdeFuera = raiz.Tipo == RocolaOsArchivos.TUsb;
                bAbrir.Text = p ? "Ver" : e != null && e.Carpeta ? "Entrar" : raiz.Tipo == RocolaOsArchivos.TUsb ? "Ver" : "Abrir";
                bCopiar.Text = p ? "Devolver" : "Copiar a los datos";
                bBorra.Text = p ? "Borrar ya" : "Borrar";
                bNuevo.Text = p ? "Vaciar papelera" : "Archivo nuevo";
                bAbrir.Enabled = li && e != null && !(p && e.Carpeta);
                bSubir.Enabled = li && !p && dir != raiz.Ruta;
                bCopiar.Enabled = li && hay && (p || (desdeFuera && !e.Carpeta));
                bRen.Enabled = li && !p && raiz.Escribe && hay;
                bBorra.Enabled = li && hay && (p || raiz.Escribe);
                bNuevo.Enabled = li && (p ? tirados.Count > 0 : raiz.Tipo == RocolaOsArchivos.TDatos);
                string rd = dirDatos.Length > datos.Ruta.Length ? dirDatos.Substring(datos.Ruta.Length + 1) + "/" : "";
                string s = estado;
                if (s == null)
                {
                    long tope = RocolaOsPapelera.TopeBytes;
                    if (!va) s = "Necesita la función «archivos» (archivos=1 en rocola_os.txt).";
                    else if (ocupado) s = "Un momento...";
                    else if (p)
                    {
                        if (hay && e.Tirado != null)
                        {
                            RocolaOsTirado t = e.Tirado;
                            s = "«" + t.Nombre + "» vino de " + t.DeNombre + "/" + t.Ruta + " · se tiró el " + fecha(t.Cuando) + "\nSe borra sola el " + fecha(t.Cuando + RocolaOsPapelera.Dias * RocolaOsPapelera.DiaMs).Substring(0, 10)
                                + " (o antes, lo más viejo primero, si la papelera pasa su tope de " + RocolaOsArchivos.Medida(tope) + ")." + (t.Llave ? " Es una llave: se ve tapada." : "");
                        }
                        else s = "La papelera: lo que se borra en los datos. Se vacía sola a los " + RocolaOsPapelera.Dias + " días o al pasar\nsu tope de " + RocolaOsArchivos.Medida(tope) + " (lo más viejo primero). «Devolver» lo pone donde estaba, sin pisar nada.";
                    }
                    else if (raiz.Tipo == RocolaOsArchivos.TUsb) s = "Memoria USB: solo se ve y se copia a los datos (a " + (rd.Length == 0 ? "la carpeta de los datos" : rd) + ", donde estuviste por última vez).";
                    else s = "Los datos de la rocola. Las llaves (llave, key, api, token, secret...) se ven tapadas y se guardan 0600. «Borrar» manda a la papelera.";
                    if (fuera > 0 && !p) s += "\n" + fuera + (fuera == 1 ? " enlace sale" : " enlaces salen") + " de la carpeta: no se muestra" + (fuera == 1 ? "." : "n.");
                }
                lEstado.ForeColor = estado == null ? x.CTenue : estadoBien ? Color.FromArgb(120, 230, 165) : x.CAviso;
                lEstado.Text = s;
            };
            Action<string, bool> dice = delegate (string s, bool bien) { estado = s; estadoBien = bien; pintaBotones(); };
            // LO QUE TARDA (la papelera, copiar): en un hilo, nunca en el de la pantalla; al acabar, un encargo a la pantalla
            Action<string, Func<string>, Action<string>> enHilo = delegate (string nombre, Func<string> trabajo, Action<string> alAcabar)
            {
                ocupado = true; estado = null; pintaBotones();
                RocolaOs.Hilo("archivos-" + nombre, delegate
                {
                    string r;
                    try { r = trabajo(); } catch (Exception ex) { r = "No se pudo: " + ex.Message; RocolaOs.Falla(RocolaOsArchivos.Nombre, nombre, ex); }
                    if (!RocolaOs.Encarga("archivos-hecho", delegate { ocupado = false; if (cerrada) return; alAcabar(r); }))
                        ocupado = false;
                });
            };
            cargaPapelera = delegate
            {
                List<RocolaOsTirado> l = null;
                enHilo("papelera-lista", delegate { l = RocolaOsPapelera.Lista(); return null; }, delegate (string r)
                {
                    if (raiz.Tipo != RocolaOsArchivos.TPapelera) return;
                    tirados = l ?? new List<RocolaOsTirado>();
                    List<RocolaOsEntrada> en = new List<RocolaOsEntrada>();
                    foreach (RocolaOsTirado t in tirados)
                        en.Add(new RocolaOsEntrada { Nombre = t.Nombre, Ruta = t.Cosa, Carpeta = t.Carpeta, Llave = t.Llave, Tamano = t.Bytes, Fecha = DateTimeOffset.FromUnixTimeMilliseconds(t.Cuando).LocalDateTime, Tirado = t });
                    int i = Math.Max(0, lista.Elegida);
                    lista.TituloFecha = "Se tiró el";
                    lista.Pon(en, en.Count > 0 ? Math.Min(i, en.Count - 1) : -1);
                    dir = raiz.Ruta; fuera = 0;
                    long b = 0; foreach (RocolaOsTirado t in tirados) b += t.Bytes;
                    pintaDonde("Papelera: " + tirados.Count + (tirados.Count == 1 ? " cosa, " : " cosas, ") + RocolaOsArchivos.Medida(b) + " de " + RocolaOsArchivos.Medida(RocolaOsPapelera.TopeBytes));
                    pintaBotones();
                });
            };
            Action pideRaices = delegate
            {
                List<RocolaOsRaiz> n = new List<RocolaOsRaiz> { datos, papelera };
                n.AddRange(RocolaOsArchivos.Usbs);
                bool igual = n.Count == raices.Count;
                for (int i = 0; igual && i < n.Count; i++) igual = n[i].Clave == raices[i].Clave;
                if (igual) return;
                raices = n;
                bool sigue = false; foreach (RocolaOsRaiz r in raices) if (r.Clave == raiz.Clave) sigue = true;
                if (!sigue && !ocupado) { raiz = datos; carga(dirDatos, null); dice("La memoria USB ya no está.", false); }
                pintaRaices();
            };
            for (int i = 0; i < bRaiz.Count; i++)
            {
                int k = i;
                bRaiz[i].Click += delegate
                {
                    if (k >= raices.Count || ocupado) return;
                    raiz = raices[k]; estado = null;
                    carga(raiz.Tipo == RocolaOsArchivos.TDatos ? dirDatos : raiz.Ruta, null);
                    pintaRaices(); lista.Focus();
                };
            }

            Action abre = null, sube = null;
            sube = delegate
            {
                if (dir == raiz.Ruta || ocupado || raiz.Tipo == RocolaOsArchivos.TPapelera) return;
                string antes = Path.GetFileName(dir);
                estado = null; carga(Path.GetDirectoryName(dir), antes);
            };
            abre = delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || !va || ocupado) return;
                if (e.Subir) { sube(); return; }
                if (raiz.Tipo == RocolaOsArchivos.TPapelera)
                {
                    if (e.Carpeta || e.Tirado == null) return;
                    // (en la papelera solo se ve; las llaves, siempre tapadas)
                    string rp = RocolaOsAbreTexto(x, raiz, e.Ruta, false, e.Tirado.Llave);
                    if (rp != null) dice(rp, false);
                    lista.Focus();
                    return;
                }
                if (e.Carpeta) { estado = null; carga(e.Ruta, null); return; }
                string r = RocolaOsAbreTexto(x, raiz, e.Ruta, false, false);
                if (r != null) dice(r, r.StartsWith("Guardado", StringComparison.Ordinal));
                carga(dir, e.Nombre);
                lista.Focus();
            };
            lista.Elegido += delegate { estado = null; pintaBotones(); };
            lista.Abre += delegate { abre(); };
            lista.Sube += delegate { sube(); };
            bAbrir.Click += delegate { abre(); };
            bSubir.Click += delegate { sube(); lista.Focus(); };
            // COPIAR A LOS DATOS (de una USB; en un hilo: puede ser grande). Si ya hay uno, pregunta; el de antes va a la
            // papelera (nada se pisa sin poder devolverlo). En la papelera, este boton es «Devolver».
            Action copia = delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || e.Carpeta || e.Subir || ocupado) return;
                RocolaOsRaiz origen = raiz;
                string dDest = dirDatos, nom = e.Nombre, ruta = e.Ruta;
                string destino = Path.Combine(dDest, RocolaOsArchivos.NombreAlCopiar(nom));
                bool reemplaza = false;
                if (File.Exists(destino) || Directory.Exists(destino) || RocolaOsArchivos.EsEnlace(destino))
                {
                    if (!File.Exists(destino) || RocolaOsArchivos.EsEnlace(destino) || Directory.Exists(destino)) { dice(Directory.Exists(destino) ? "En los datos hay una carpeta con ese nombre." : "En los datos hay un enlace con ese nombre: no se escribe.", false); lista.Focus(); return; }
                    if (!RocolaOsConfirma(x, "¿Reemplazar?", "En los datos ya hay un «" + Path.GetFileName(destino) + "».\nSe cambia por el de " + "la memoria USB" + " (el de antes va a la papelera).", "Sí, reemplazar")) { lista.Focus(); return; }
                    reemplaza = true;
                }
                string dest = null; bool aPapelera = false;
                enHilo("copia", delegate
                {
                    if (reemplaza && File.Exists(destino) && !RocolaOsArchivos.EsEnlace(destino))
                    {
                        RocolaOsTirado t; string rp = RocolaOsPapelera.Manda(datos, destino, out t);
                        if (rp != null) return "El de antes no se pudo mandar a la papelera: " + rp;
                        aPapelera = true;
                    }
                    string r = RocolaOsArchivos.Copia(origen, ruta, datos, dDest, false, out dest);
                    if (aPapelera) { int v, p; RocolaOsPapelera.Purga(RocolaOsPapelera.AhoraMs, RocolaOsPapelera.TopeBytes, out v, out p); }
                    return r;
                }, delegate (string r)
                {
                    if (r == null)
                    {
                        string n = Path.GetFileName(dest);
                        bool llave = RocolaOsArchivos.EsLlave(dest, datos.Ruta);
                        RocolaOs.Apunta(RocolaOsArchivos.Nombre, "copiado de " + "la USB" + " a los datos: " + n + (llave ? " (llave, 0600)" : "") + (aPapelera ? " (el de antes, a la papelera)" : ""));
                        dice("Copiado a " + dest + (n != nom ? " (como " + n + ")" : "") + (llave ? ": es una llave, queda con permisos 0600." : ".") + (aPapelera ? " El de antes está en la papelera." : ""), true);
                    }
                    else dice(r == "YA_HAY" ? "En los datos ya hay uno con ese nombre." : r, false);
                    lista.Focus();
                });
            };
            Action devuelve = delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || e.Tirado == null || ocupado) return;
                RocolaOsTirado t = e.Tirado;
                string queda = null; bool otra = false;
                enHilo("papelera-devuelve", delegate { return RocolaOsPapelera.Devuelve(t, out queda, out otra); }, delegate (string r)
                {
                    if (r == null)
                    {
                        string n = Path.GetFileName(queda);
                        RocolaOs.Apunta(RocolaOsArchivos.Nombre, "devuelto de la papelera: " + t.Nombre + (n != t.Nombre ? " (como " + n + ")" : ""));
                        estado = "Devuelto a " + queda + (n != t.Nombre ? " (ya había un «" + t.Nombre + "»: no se pisó)" : "") + (otra ? ". Su carpeta ya no estaba: quedó en la de " + t.DeNombre.ToLowerInvariant().Replace("datos de la rocola", "los datos") + "." : ".");
                        estadoBien = true;
                    }
                    else { estado = r; estadoBien = false; }
                    string g = estado; bool gb = estadoBien;
                    cargaPapelera();
                    estado = g; estadoBien = gb;
                    lista.Focus();
                });
            };
            bCopiar.Click += delegate { if (raiz.Tipo == RocolaOsArchivos.TPapelera) devuelve(); else copia(); };
            bRen.Click += delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || e.Subir || !raiz.Escribe || ocupado) return;
                string nuevo = RocolaOsPideNombre(x, "Renombrar", "El nombre nuevo de «" + e.Nombre + "»:", e.Nombre, "Renombrar");
                if (nuevo != null && nuevo != e.Nombre)
                {
                    string r = RocolaOsArchivos.Renombra(raiz, e.Ruta, nuevo);
                    if (r == null) { RocolaOs.Apunta(RocolaOsArchivos.Nombre, "renombrado: " + e.Nombre + " -> " + nuevo); carga(dir, nuevo); dice("Renombrado a «" + nuevo + "».", true); }
                    else dice(r, false);
                }
                lista.Focus();
            };
            bBorra.Click += delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || e.Subir || ocupado) return;
                if (raiz.Tipo == RocolaOsArchivos.TPapelera)
                {
                    RocolaOsTirado t = e.Tirado;
                    if (t == null) return;
                    if (!RocolaOsConfirma(x, "¿Borrar para siempre?", "Se borra «" + t.Nombre + "» de la papelera.\nNo se puede deshacer.", "Sí, borrar")) { lista.Focus(); return; }
                    enHilo("papelera-borra", delegate { return RocolaOsPapelera.BorraYa(t); }, delegate (string r)
                    {
                        if (r == null) RocolaOs.Apunta(RocolaOsArchivos.Nombre, "borrado de la papelera: " + t.Nombre);
                        string g = r ?? "Borrado para siempre «" + t.Nombre + "».";
                        cargaPapelera(); estado = g; estadoBien = r == null;
                        lista.Focus();
                    });
                    return;
                }
                if (!raiz.Escribe) return;
                long tope = RocolaOsPapelera.TopeBytes;
                long mide = e.Carpeta ? 0 : e.Tamano;
                bool grande = mide > tope;
                string q = grande
                    ? "«" + e.Nombre + "» mide " + RocolaOsArchivos.Medida(mide) + ": más que el tope de la papelera\n(" + RocolaOsArchivos.Medida(tope) + "), así que se borra para siempre. No se puede deshacer."
                    : "Se manda a la papelera " + (e.Carpeta ? "la carpeta (vacía)" : "el archivo") + " «" + e.Nombre + "».\nSe puede devolver durante " + RocolaOsPapelera.Dias + " días (Papelera, «Devolver»).";
                if (!RocolaOsConfirma(x, grande ? "¿Borrar?" : "¿A la papelera?", q, grande ? "Sí, borrar" : "Sí, a la papelera")) { lista.Focus(); return; }
                RocolaOsRaiz rz = raiz; string ruta = e.Ruta, nom = e.Nombre;
                int antes = lista.Elegida;
                RocolaOsTirado tt = null; bool sola = false;
                enHilo("papelera-manda", delegate
                {
                    string r = RocolaOsPapelera.Manda(rz, ruta, out tt);
                    if (r != null) return r;
                    int v, p; RocolaOsPapelera.Purga(RocolaOsPapelera.AhoraMs, tope, out v, out p);
                    sola = !Directory.Exists(tt.Dir);
                    if (v + p > 0) RocolaOs.Apunta(RocolaOsArchivos.Nombre, "la papelera se vacio sola (al tirar): " + v + " de mas de " + RocolaOsPapelera.Dias + " dias, " + p + " por el tope de " + (tope / 1048576) + " MB");
                    return null;
                }, delegate (string r)
                {
                    if (r == null)
                    {
                        RocolaOs.Apunta(RocolaOsArchivos.Nombre, (sola ? "borrado (mas que el tope de la papelera): " : "a la papelera: ") + nom);
                        carga(dir, null); lista.Elegida = Math.Min(antes, lista.Renglones.Count - 1);
                        dice(sola ? "Borrado «" + nom + "» (medía más que el tope de la papelera)." : "«" + nom + "» está en la papelera (se puede devolver).", true);
                    }
                    else dice(r, false);
                    lista.Focus();
                });
            };
            bNuevo.Click += delegate
            {
                if (ocupado) return;
                if (raiz.Tipo == RocolaOsArchivos.TPapelera)
                {
                    if (tirados.Count == 0) return;
                    long b = 0; foreach (RocolaOsTirado t in tirados) b += t.Bytes;
                    if (!RocolaOsConfirma(x, "¿Vaciar la papelera?", "Se borra todo lo de la papelera (" + tirados.Count + (tirados.Count == 1 ? " cosa, " : " cosas, ") + RocolaOsArchivos.Medida(b) + ").\nNo se puede deshacer.", "Sí, vaciar")) { lista.Focus(); return; }
                    int n = 0; string err = null;
                    enHilo("papelera-vacia", delegate { n = RocolaOsPapelera.Vacia(out err); return err; }, delegate (string r)
                    {
                        RocolaOs.Apunta(RocolaOsArchivos.Nombre, "papelera vaciada: " + n);
                        string g = r ?? "Papelera vacía (" + n + (n == 1 ? " cosa borrada)." : " cosas borradas).");
                        cargaPapelera(); estado = g; estadoBien = r == null;
                        lista.Focus();
                    });
                    return;
                }
                if (raiz.Tipo != RocolaOsArchivos.TDatos) return;
                string nn = RocolaOsPideNombre(x, "Archivo nuevo", "El nombre del archivo nuevo (por ejemplo rocola_genius.txt):", "", "Crear");
                if (nn != null)
                {
                    string ruta = Path.Combine(dir, nn);
                    if (File.Exists(ruta) || Directory.Exists(ruta) || RocolaOsArchivos.EsEnlace(ruta)) dice("Ya hay algo que se llama «" + nn + "».", false);
                    else
                    {
                        string r = RocolaOsAbreTexto(x, raiz, ruta, true, false);
                        carga(dir, nn);
                        if (r != null) dice(r, r.StartsWith("Guardado", StringComparison.Ordinal));
                    }
                }
                lista.Focus();
            };

            raices.Add(datos); raices.Add(papelera);
            pintaRaices();
            carga(dir, null);
            if (va) RocolaOsArchivos.BuscaUsbs();
            int vueltas = 0;
            RocolaOsPagina pag = new RocolaOsPagina();
            pag.Panel = pg; pag.Titulo = "Archivos";
            pag.CadaMedioSegundo = delegate
            {
                if (!va) return;
                pideRaices();
                if (++vueltas % 4 == 0 && pg.Visible) RocolaOsArchivos.BuscaUsbs();   // (cada 2 s, solo con la hojita a la vista)
            };
            pag.AlCerrar = delegate { cerrada = true; RocolaOsArchivos.Desmonta(); fLista.Dispose(); fChica.Dispose(); };
            return pag;
        }

        // (el boton plano de XP no tiene Visible: se lo pone su caja)
        private static void RocolaOsVe(BotonPlano b, bool si) { CajaPieza c = ((IPieza)b).Caja; if (c.Visible != si) c.Visible = si; }

        // PEDIR UN NOMBRE (renombrar, archivo nuevo): una hoja chica con un renglon que se escribe; null si se cancela o no vale
        private string RocolaOsPideNombre(RocolaOsVentanaCtx x, string titulo, string texto, string inicial, string si)
        {
            string res = null;
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            using (Rocola.Nativo.Letra fm = Rocola.Nativo.Letra.EnPuntos(Letras.AnchoFijo, 11f))
            {
                q.Text = "Rocola OS - " + titulo;
                q.ClientSize = new Size(600, 230);
                q.BackColor = x.CFondo; q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 556, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = titulo;
                Rotulo lD = new Rotulo(); lD.SetBounds(24, 48, 556, 20); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTenue; lD.Font = x.FSub; lD.Text = texto; lD.UseMnemonic = false;
                RocolaOsEditor ed = new RocolaOsEditor(); ed.UnRenglon = true; ed.Tope = 255; ed.Letra = fm; ed.SetBounds(22, 76, 556, 40); ed.TabIndex = 0;
                ed.Texto = inicial; ed.AlFinal();
                Rotulo lE = new Rotulo(); lE.SetBounds(24, 122, 556, 34); lE.BackColor = Color.Transparent; lE.ForeColor = x.CAviso; lE.Font = x.FDes; lE.UseMnemonic = false;
                BotonPlano bNo = new BotonPlano(); bNo.SetBounds(22, 168, 270, 40); bNo.Font = x.FBot; bNo.TabIndex = 2; bNo.Text = "Cancelar";
                BotonPlano bSi = new BotonPlano(); bSi.SetBounds(308, 168, 270, 40); bSi.Font = x.FBot; bSi.TabIndex = 1; bSi.Text = si;
                bSi.Relleno = Color.FromArgb(30, 96, 70); bSi.Borde = Color.FromArgb(72, 182, 132);
                Action acepta = delegate
                {
                    string n = ed.Texto; string e = RocolaOsArchivos.NombreNoVale(n);
                    if (e != null) { lE.Text = e; ed.Focus(); return; }
                    res = n; q.Close();
                };
                ed.Intro += acepta;
                bSi.Click += delegate { acepta(); };
                bNo.Click += delegate { res = null; q.Close(); };
                q.Controls.Add(ed); q.Controls.Add(bSi); q.Controls.Add(bNo); q.Controls.Add(lT); q.Controls.Add(lD); q.Controls.Add(lE);
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { res = null; q.Close(); } };
                // (SIN MonedaTambienAqui: aqui se escribe, y el monedero y el teclado son la misma tecla para la rocola: cada moneda seria una
                // letra perdida -y un credito-. Con esta hoja abierta las monedas no cuentan; con la de Ctrl+F4 si)
                q.MuestraModal(x.Dlg);
            }
            return res;
        }

        // ABRIR UN TEXTO (o uno nuevo) en su hoja; devuelve lo que paso (o null si no se hizo nada). llaveFija: es una llave aunque su nombre de
        // ahora no lo diga (en la papelera: por su ruta de antes)
        private string RocolaOsAbreTexto(RocolaOsVentanaCtx x, RocolaOsRaiz raiz, string ruta, bool nuevo, bool llaveFija)
        {
            RocolaOsArchivos.Texto t;
            if (nuevo) t = new RocolaOsArchivos.Texto { Contenido = "", FinalConSalto = true };
            else
            {
                if (!RocolaOsArchivos.Dentro(raiz.Ruta, ruta)) return "Ese archivo no es de la rocola.";
                if (raiz.Escribe && RocolaOsArchivos.EsEnlace(ruta)) return "Es un enlace: no se abre aquí.";
                string err; t = RocolaOsArchivos.LeeTexto(ruta, out err);
                if (t == null) return err;
            }
            bool llave = llaveFija || RocolaOsArchivos.EsLlave(ruta, raiz.Ruta);
            bool enPapelera = raiz.Tipo == RocolaOsArchivos.TPapelera;
            bool solo = !raiz.Escribe || t.SoloLeer;
            string res = null;
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            using (Rocola.Nativo.Letra fm = Rocola.Nativo.Letra.EnPuntos(Letras.AnchoFijo, 10f))
            {
                string nom = Path.GetFileName(ruta);
                q.Text = "Rocola OS - " + nom;
                q.ClientSize = new Size(920, 660);
                q.BackColor = x.CFondo; q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 10, 876, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = nom; lT.UseMnemonic = false;
                Rotulo lD = new Rotulo(); lD.SetBounds(24, 42, 876, 36); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTenue; lD.Font = x.FDes; lD.UseMnemonic = false;
                lD.Text = ruta + (nuevo ? " (nuevo)" : "") + "\n" + (enPapelera ? "Está en la papelera: solo se ve" + (llave ? " (es una llave: siempre tapada, aquí no se muestra)." : ".") : solo ? (t.Aviso ?? "Solo se ve (memoria USB).") : llave ? "Es una llave: se ve tapada (de cada renglón, los 3 primeros y los 3 últimos; los cortos, enteros) y se guarda con permisos 0600." : "Texto. Guardar (o Ctrl+S) lo guarda; Escape cierra.");
                RocolaOsEditor ed = new RocolaOsEditor(); ed.SetBounds(22, 84, 876, 486); ed.Letra = fm; ed.TabIndex = 0;
                ed.SoloLeer = solo; ed.Tapado = llave; ed.Texto = t.Contenido;
                Rotulo lE = new Rotulo(); lE.SetBounds(24, 576, 600, 22); lE.BackColor = Color.Transparent; lE.ForeColor = x.CTenue; lE.Font = x.FDes; lE.UseMnemonic = false;
                BotonPlano bGuarda = new BotonPlano(); bGuarda.SetBounds(22, 604, 220, 40); bGuarda.Font = x.FBot; bGuarda.TabIndex = 1; bGuarda.Text = "Guardar";
                bGuarda.Relleno = Color.FromArgb(30, 96, 70); bGuarda.Borde = Color.FromArgb(72, 182, 132);
                BotonPlano bMuestra = new BotonPlano(); bMuestra.SetBounds(258, 604, 220, 40); bMuestra.Font = x.FBot; bMuestra.TabIndex = 2; bMuestra.Text = "Mostrar";
                BotonPlano bCierra = new BotonPlano(); bCierra.SetBounds(678, 604, 220, 40); bCierra.Font = x.FBot; bCierra.TabIndex = 3; bCierra.Text = "Cerrar";
                RocolaOsVe(bGuarda, !solo); RocolaOsVe(bMuestra, llave && !enPapelera);   // (en la papelera una llave nunca se ve entera)
                Action info = delegate
                {
                    lE.Text = "Renglón " + (ed.Fila + 1) + " de " + ed.Renglones + ", columna " + (ed.Columna + 1) + (ed.Cambiado ? " · con cambios sin guardar" : "") + (llave ? (ed.Tapado ? " · tapada" : " · A LA VISTA") : "");
                };
                bool guardado = false;
                Action guarda = delegate
                {
                    if (solo) return;
                    string e = RocolaOsArchivos.GuardaTexto(raiz, ruta, ed.Texto, t.Crlf, t.Bom, t.FinalConSalto || nuevo || llave, llave);
                    if (e != null) { lE.Text = e; lE.ForeColor = x.CAviso; return; }
                    ed.Cambiado = false; guardado = true; nuevo = false;
                    RocolaOs.Apunta(RocolaOsArchivos.Nombre, "guardado: " + nom + (llave ? " (llave, 0600)" : ""));   // (nunca lo de dentro)
                    lE.ForeColor = Color.FromArgb(120, 230, 165); info(); lE.Text = "Guardado. " + lE.Text;
                };
                Func<bool> puedeCerrar = delegate
                {
                    if (!ed.Cambiado || solo) return true;
                    return RocolaOsConfirma(new RocolaOsVentanaCtx { Dlg = q, FTit = x.FTit, FSub = x.FSub, FNom = x.FNom, FVal = x.FVal, FDes = x.FDes, FBot = x.FBot, FInt = x.FInt },
                        "¿Cerrar sin guardar?", "Lo que cambiaste en «" + nom + "» se pierde.", "Sí, cerrar");
                };
                ed.Cambia += delegate { lE.ForeColor = x.CTenue; };
                ed.Guarda += delegate { guarda(); };
                bGuarda.Click += delegate { guarda(); ed.Focus(); };
                bMuestra.Click += delegate
                {
                    if (!ed.Tapado) { ed.Tapado = true; bMuestra.Text = "Mostrar"; ((IPieza)ed).Caja.Invalidate(); info(); ed.Focus(); return; }
                    if (RocolaOsConfirma(new RocolaOsVentanaCtx { Dlg = q, FTit = x.FTit, FSub = x.FSub, FNom = x.FNom, FVal = x.FVal, FDes = x.FDes, FBot = x.FBot, FInt = x.FInt },
                        "¿Mostrar la llave?", "Se verá entera en la pantalla: cualquiera que la mire la puede copiar.", "Sí, mostrar"))
                    { ed.Tapado = false; bMuestra.Text = "Tapar"; ((IPieza)ed).Caja.Invalidate(); RocolaOs.Apunta(RocolaOsArchivos.Nombre, "llave a la vista: " + nom); }
                    info(); ed.Focus();
                };
                bCierra.Click += delegate { if (puedeCerrar()) q.Close(); };
                q.Controls.Add(ed); q.Controls.Add(bGuarda); q.Controls.Add(bMuestra); q.Controls.Add(bCierra); q.Controls.Add(lT); q.Controls.Add(lD); q.Controls.Add(lE);
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { if (puedeCerrar()) q.Close(); } };
                Reloj r = new Reloj("RocolaOs:archivos-editor", null); r.Interval = 250; r.Tick += delegate { if (!lE.Text.StartsWith("Guardado", StringComparison.Ordinal) && !lE.Text.StartsWith("No ", StringComparison.Ordinal) && !lE.Text.StartsWith("Pasa", StringComparison.Ordinal)) info(); };
                info(); r.Start();
                // (SIN MonedaTambienAqui: aqui se escribe, y el monedero y el teclado son la misma tecla para la rocola: cada moneda seria una
                // letra perdida -y un credito-. Con esta hoja abierta las monedas no cuentan; con la de Ctrl+F4 si)
                q.MuestraModal(x.Dlg);
                r.Stop(); r.Dispose();
                if (guardado) res = "Guardado: " + ruta + (llave ? " (llave, permisos 0600)." : ".");
            }
            return res;
        }

        // UNA CONFIRMACION como «¿Se ve bien?» de la hoja Pantalla: «No» con el foco (Intro o Escape sin mirar: no) y el boton de si.
        internal bool RocolaOsConfirma(RocolaOsVentanaCtx x, string titulo, string texto, string si)
        {
            bool ok = false;
            using (Hoja q = new Hoja(MarcoXp.Dialogo))
            {
                q.Text = "Rocola OS";
                q.ClientSize = new Size(520, 200);
                q.BackColor = x.CFondo; q.Font = x.FSub;
                Rotulo lT = new Rotulo(); lT.SetBounds(22, 14, 476, 28); lT.BackColor = Color.Transparent; lT.ForeColor = Color.FromArgb(232, 238, 255); lT.Font = x.FTit; lT.Text = titulo; lT.UseMnemonic = false;
                Rotulo lD = new Rotulo(); lD.SetBounds(24, 52, 476, 70); lD.BackColor = Color.Transparent; lD.ForeColor = x.CTenue; lD.Font = x.FSub; lD.Text = texto; lD.UseMnemonic = false;
                BotonPlano bNo = new BotonPlano(); bNo.SetBounds(22, 138, 230, 40); bNo.Font = x.FBot; bNo.TabIndex = 0; bNo.Text = "No";
                BotonPlano bSi = new BotonPlano(); bSi.SetBounds(268, 138, 230, 40); bSi.Font = x.FBot; bSi.TabIndex = 1; bSi.Text = si;
                bSi.Relleno = Color.FromArgb(110, 40, 46); bSi.Borde = Color.FromArgb(220, 96, 96);
                bNo.Click += delegate { ok = false; q.Close(); };
                bSi.Click += delegate { ok = true; q.Close(); };
                q.Controls.Add(bNo); q.Controls.Add(bSi); q.Controls.Add(lT); q.Controls.Add(lD);
                q.TeclaAntes += delegate (PulsoTecla ke) { if (ke.KeyCode == Tecla.Escape) { ok = false; q.Close(); } };
                MonedaTambienAqui(q);
                q.MuestraModal(x.Dlg);
            }
            return ok;
        }
    }
}
