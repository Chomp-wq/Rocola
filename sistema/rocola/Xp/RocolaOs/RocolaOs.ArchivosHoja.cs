// ROCOLA OS (fase 11): LA HOJITA «ARCHIVOS» DE LA VENTANA DE CTRL+F4 (peticion del dueno del 20-11-2026; FASE11_navegador-archivos.md). Lo de
// fuera de la hoja (las raices, la regla de no salir, las llaves, copiar, guardar...) esta en RocolaOs.Archivos.cs; la lista y el editor, en
// RocolaOs.ArchivosPiezas.cs.
//   Arriba, un boton por raiz: «Datos de la rocola» y cada memoria USB (de solo lectura; se miran cada 2 s con la ventana abierta). Debajo,
//   donde se esta, la lista (carpetas primero; nombre, tamano y fecha; la llave con su icono) y los botones: Abrir (entrar en la carpeta o
//   abrir el texto), Subir, Copiar a los datos (desde una USB, a la carpeta de los datos donde se estuvo por ultimo), Renombrar, Borrar (con
//   confirmacion) y Archivo nuevo (en los datos). Abajo, lo que paso.
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
            RocolaOs.Registra(new RocolaOsFuncion { Nombre = RocolaOsArchivos.Nombre, Que = "el gestor de archivos de Ctrl+F4 (los datos de la rocola y las USB)", DeFabrica = true });
            RocolaOsHojitas.Registra(new RocolaOsHojita { Nombre = "Archivos", Orden = 30, Arma = (f, x) => f.RocolaOsPaginaArchivos(x) });
        }

        private RocolaOsPagina RocolaOsPaginaArchivos(RocolaOsVentanaCtx x)
        {
            Grupo pg = new Grupo(); pg.SetBounds(0, 96, 960, 580); pg.BackColor = x.CFondo;
            bool va = RocolaOs.Encendida(RocolaOsArchivos.Nombre);
            Rocola.Nativo.Letra fLista = Rocola.Nativo.Letra.EnPuntos(FamiliaUi(), 10f), fChica = Rocola.Nativo.Letra.EnPuntos(FamiliaUi(), 8.5f, true);
            RocolaOsRaiz datos = RocolaOsArchivos.Datos();
            RocolaOsRaiz raiz = datos;
            string dir = datos.Ruta, dirDatos = datos.Ruta;
            string estado = null; bool estadoBien = false;

            // los botones de las raices (hasta 5), arriba
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
            BotonPlano bAbrir = bs[0], bSubir = bs[1], bCopiar = bs[2], bRen = bs[3], bBorra = bs[4], bNuevo = bs[5];
            bBorra.Relleno = Color.FromArgb(70, 34, 44); bBorra.Borde = Color.FromArgb(170, 84, 96);
            Rotulo lEstado = new Rotulo(); lEstado.SetBounds(18, 520, 926, 54); lEstado.BackColor = Color.Transparent; lEstado.Font = x.FDes; lEstado.UseMnemonic = false;
            pg.Controls.Add(lEstado);

            int fuera = 0;
            Action pintaBotones = null;
            Action<string, string> carga = delegate (string carpeta, string elegir)
            {
                string err;
                List<RocolaOsEntrada> l = RocolaOsArchivos.Lista(raiz, carpeta, out fuera, out err);
                if (err != null) { estado = err; estadoBien = false; if (carpeta != raiz.Ruta) { carpeta = raiz.Ruta; l = RocolaOsArchivos.Lista(raiz, carpeta, out fuera, out err); } }
                dir = carpeta;
                if (raiz.Escribe) dirDatos = carpeta;
                int e = 0;
                if (elegir != null) for (int i = 0; i < l.Count; i++) if (l[i].Nombre == elegir) e = i;
                if (elegir == null && l.Count > 0 && l[0].Subir && l.Count > 1) e = 1;
                lista.Pon(l, l.Count > 0 ? e : -1);
                string rel = dir.Length > raiz.Ruta.Length ? dir.Substring(raiz.Ruta.Length) : "";
                string donde = (raiz.Escribe ? raiz.Ruta : raiz.Nombre + " (solo lectura)") + (raiz.Escribe ? rel : (rel.Length == 0 ? "" : " " + rel));
                // (si no cabe, se corta por la izquierda: lo que importa es donde se esta)
                while (donde.Length > 8 && TextoGdiPlus.Mide(donde, x.FNom, AjusteLetra.ClearTypeAjustada, new SizeF(0, 0), FormatoTexto.SinFormato).Width > 690) donde = "..." + donde.Substring(Math.Min(donde.Length - 4, 8));
                lDonde.Text = donde;
                long lib = RocolaOsArchivos.Libre(dirDatos);
                lLibre.Text = lib >= 0 ? "En los datos quedan " + RocolaOsArchivos.Medida(lib) : "";
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
                bAbrir.Enabled = va && e != null;
                bAbrir.Text = e != null && e.Carpeta ? "Entrar" : raiz.Escribe ? "Abrir" : "Ver";
                bSubir.Enabled = va && dir != raiz.Ruta;
                bCopiar.Enabled = va && !raiz.Escribe && hay && !e.Carpeta;
                bRen.Enabled = va && raiz.Escribe && hay;
                bBorra.Enabled = va && raiz.Escribe && hay;
                bNuevo.Enabled = va && raiz.Escribe;
                string rd = dirDatos.Length > datos.Ruta.Length ? dirDatos.Substring(datos.Ruta.Length + 1) + "/" : "";
                string s = estado;
                if (s == null)
                {
                    if (!va) s = "Necesita la función «archivos» (archivos=1 en rocola_os.txt).";
                    else if (!raiz.Escribe) s = "Memoria USB: solo se ve y se copia a los datos (a " + (rd.Length == 0 ? "la carpeta de los datos" : rd) + ", donde estuviste por última vez).";
                    else s = "Los datos de la rocola. Las llaves (llave, key, api, token, secret...) se ven tapadas y se guardan 0600.";
                    if (fuera > 0) s += "\n" + fuera + (fuera == 1 ? " enlace sale" : " enlaces salen") + " de la carpeta: no se muestra" + (fuera == 1 ? "." : "n.");
                }
                lEstado.ForeColor = estado == null ? x.CTenue : estadoBien ? Color.FromArgb(120, 230, 165) : x.CAviso;
                lEstado.Text = s;
            };
            Action<string, bool> dice = delegate (string s, bool bien) { estado = s; estadoBien = bien; pintaBotones(); };
            Action pideRaices = delegate
            {
                List<RocolaOsRaiz> n = new List<RocolaOsRaiz> { datos };
                n.AddRange(RocolaOsArchivos.Usbs);
                bool igual = n.Count == raices.Count;
                for (int i = 0; igual && i < n.Count; i++) igual = n[i].Clave == raices[i].Clave;
                if (igual) return;
                raices = n;
                bool sigue = false; foreach (RocolaOsRaiz r in raices) if (r.Clave == raiz.Clave) sigue = true;
                if (!sigue) { raiz = datos; carga(dirDatos, null); dice("La memoria USB ya no está.", false); }
                pintaRaices();
            };
            for (int i = 0; i < bRaiz.Count; i++)
            {
                int k = i;
                bRaiz[i].Click += delegate
                {
                    if (k >= raices.Count) return;
                    raiz = raices[k]; estado = null;
                    carga(raiz.Escribe ? dirDatos : raiz.Ruta, null);
                    pintaRaices(); lista.Focus();
                };
            }

            Action abre = null, sube = null;
            sube = delegate
            {
                if (dir == raiz.Ruta) return;
                string antes = Path.GetFileName(dir);
                estado = null; carga(Path.GetDirectoryName(dir), antes);
            };
            abre = delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || !va) return;
                if (e.Subir) { sube(); return; }
                if (e.Carpeta) { estado = null; carga(e.Ruta, null); return; }
                string r = RocolaOsAbreTexto(x, raiz, e.Ruta, false);
                if (r != null) dice(r, r.StartsWith("Guardado", StringComparison.Ordinal));
                carga(dir, e.Nombre);
                lista.Focus();
            };
            lista.Elegido += delegate { estado = null; pintaBotones(); };
            lista.Abre += delegate { abre(); };
            lista.Sube += delegate { sube(); };
            bAbrir.Click += delegate { abre(); };
            bSubir.Click += delegate { sube(); lista.Focus(); };
            bCopiar.Click += delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || e.Carpeta || raiz.Escribe) return;
                string destino;
                string r = RocolaOsArchivos.Copia(raiz, e.Ruta, datos, dirDatos, false, out destino);
                if (r == "YA_HAY")
                {
                    if (!RocolaOsConfirma(x, "¿Reemplazar?", "En los datos ya hay un «" + Path.GetFileName(destino) + "».\nSe cambia por el de la memoria USB.", "Sí, reemplazar")) { lista.Focus(); return; }
                    r = RocolaOsArchivos.Copia(raiz, e.Ruta, datos, dirDatos, true, out destino);
                }
                if (r == null)
                {
                    string n = Path.GetFileName(destino);
                    bool llave = RocolaOsArchivos.EsLlave(destino, datos.Ruta);
                    RocolaOs.Apunta(RocolaOsArchivos.Nombre, "copiado de la USB a los datos: " + n + (llave ? " (llave, 0600)" : ""));
                    dice("Copiado a " + destino + (n != e.Nombre ? " (como " + n + ")" : "") + (llave ? ": es una llave, queda con permisos 0600." : "."), true);
                }
                else dice(r, false);
                lista.Focus();
            };
            bRen.Click += delegate
            {
                RocolaOsEntrada e = lista.LaElegida;
                if (e == null || e.Subir || !raiz.Escribe) return;
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
                if (e == null || e.Subir || !raiz.Escribe) return;
                if (RocolaOsConfirma(x, "¿Borrar?", "Se borra " + (e.Carpeta ? "la carpeta (vacía)" : "el archivo") + " «" + e.Nombre + "».\nNo se puede deshacer.", "Sí, borrar"))
                {
                    string r = RocolaOsArchivos.Borra(raiz, e.Ruta);
                    if (r == null) { RocolaOs.Apunta(RocolaOsArchivos.Nombre, "borrado: " + e.Nombre); int i = lista.Elegida; carga(dir, null); lista.Elegida = Math.Min(i, lista.Renglones.Count - 1); dice("Borrado «" + e.Nombre + "».", true); }
                    else dice(r, false);
                }
                lista.Focus();
            };
            bNuevo.Click += delegate
            {
                if (!raiz.Escribe) return;
                string n = RocolaOsPideNombre(x, "Archivo nuevo", "El nombre del archivo nuevo (por ejemplo rocola_genius.txt):", "", "Crear");
                if (n != null)
                {
                    string ruta = Path.Combine(dir, n);
                    if (File.Exists(ruta) || Directory.Exists(ruta) || RocolaOsArchivos.EsEnlace(ruta)) dice("Ya hay algo que se llama «" + n + "».", false);
                    else
                    {
                        string r = RocolaOsAbreTexto(x, raiz, ruta, true);
                        carga(dir, n);
                        if (r != null) dice(r, r.StartsWith("Guardado", StringComparison.Ordinal));
                    }
                }
                lista.Focus();
            };

            raices.Add(datos);
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
            pag.AlCerrar = delegate { RocolaOsArchivos.Desmonta(); fLista.Dispose(); fChica.Dispose(); };
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

        // ABRIR UN TEXTO (o uno nuevo) en su hoja; devuelve lo que paso (o null si no se hizo nada)
        private string RocolaOsAbreTexto(RocolaOsVentanaCtx x, RocolaOsRaiz raiz, string ruta, bool nuevo)
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
            bool llave = RocolaOsArchivos.EsLlave(ruta, raiz.Ruta);
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
                lD.Text = ruta + (nuevo ? " (nuevo)" : "") + "\n" + (solo ? (t.Aviso ?? "Solo se ve (memoria USB).") : llave ? "Es una llave: se ve tapada (los 3 primeros y los 3 últimos de cada renglón) y se guarda con permisos 0600." : "Texto. Guardar (o Ctrl+S) lo guarda; Escape cierra.");
                RocolaOsEditor ed = new RocolaOsEditor(); ed.SetBounds(22, 84, 876, 486); ed.Letra = fm; ed.TabIndex = 0;
                ed.SoloLeer = solo; ed.Tapado = llave; ed.Texto = t.Contenido;
                Rotulo lE = new Rotulo(); lE.SetBounds(24, 576, 600, 22); lE.BackColor = Color.Transparent; lE.ForeColor = x.CTenue; lE.Font = x.FDes; lE.UseMnemonic = false;
                BotonPlano bGuarda = new BotonPlano(); bGuarda.SetBounds(22, 604, 220, 40); bGuarda.Font = x.FBot; bGuarda.TabIndex = 1; bGuarda.Text = "Guardar";
                bGuarda.Relleno = Color.FromArgb(30, 96, 70); bGuarda.Borde = Color.FromArgb(72, 182, 132);
                BotonPlano bMuestra = new BotonPlano(); bMuestra.SetBounds(258, 604, 220, 40); bMuestra.Font = x.FBot; bMuestra.TabIndex = 2; bMuestra.Text = "Mostrar";
                BotonPlano bCierra = new BotonPlano(); bCierra.SetBounds(678, 604, 220, 40); bCierra.Font = x.FBot; bCierra.TabIndex = 3; bCierra.Text = "Cerrar";
                RocolaOsVe(bGuarda, !solo); RocolaOsVe(bMuestra, llave);
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
    }
}
