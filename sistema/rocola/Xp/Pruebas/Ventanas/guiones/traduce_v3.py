#!/usr/bin/env python3
# V3 de las ventanas: cambia, linea por linea y sin anadir ni quitar lineas, lo de las ventanas que no depende de la herencia.
import sys, os
D = sys.argv[1]   # la carpeta Codigo
GRUPO = sys.argv[2] if len(sys.argv) > 2 else "todo"   # relojes (Program.cs y los Reloj) | encargos (BeginInvoke, InvokeRequired, DoEvents) | todo
C_RELOJ_DECL = "EN LINUX: Reloj de la pantalla nativa (Nativo/Reloj.cs; en XP, Timer de WinForms: SetTimer y WM_TIMER en el hilo de la ventana)"
C_RELOJ = "EN LINUX: Reloj de la pantalla nativa (WM_TIMER)"
C_ENCARGO = "EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)"
C_ACCION = "EN LINUX: Action (en XP, MethodInvoker de WinForms: el mismo delegado sin argumentos)"
C_BOMBEA = "EN LINUX: todo lo pendiente de la pantalla nativa, sin Libre (en XP, Application.DoEvents)"
C_FUERA = "EN LINUX: FueraDeSuHilo es InvokeRequired (la rocola tiene ventana y este no es el hilo de la pantalla)"
cambios = [
 # (archivo, linea, viejo, nuevo, comentario)
 ("Program.cs", 90, "Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)", "PantallaRocola.AlFallar = delegate (Exception ex)", "EN LINUX: lo que lance una tecla, un reloj, un encargo o un pintado en la pantalla nativa (en XP, Application.ThreadException) se apunta y la rocola sigue"),
 ("Program.cs", 91, 'LogCrash("ThreadException", e.Exception);', 'LogCrash("ThreadException", ex);', "EN LINUX: la excepcion llega sola"),
 ("Program.cs", 106, "Application.Run(new Form1());", "PantallaRocola.Corre(new Form1());", "EN LINUX: la pantalla nativa (en XP, Application.Run): la rocola, sus relojes, encargos y teclas en este hilo hasta que se cierra; el apagado del sistema la cierra como el de Windows"),
 ("Form1.cs", 72, "private Timer progressTimer", "private Reloj progressTimer", C_RELOJ_DECL),
 ("Form1.cs", 73, "private Timer focusTimer;", "private Reloj focusTimer;", C_RELOJ),
 ("Form1.cs", 718, "progressTimer = new Timer();", "progressTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 719, "coverSettleTimer = new Timer();", "coverSettleTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 722, "posaTimer = new Timer();", "posaTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 724, "animTimer = new Timer();", "animTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 725, "toastTimer = new Timer();", "toastTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 726, "videoPoll = new Timer();", "videoPoll = new Reloj();", C_RELOJ),
 ("Form1.cs", 727, "statsTimer = new Timer();", "statsTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 789, "focusTimer = new Timer();", "focusTimer = new Reloj();", C_RELOJ),
 ("Form1.cs", 873, "Timer anterior = focusTimer;", "Reloj anterior = focusTimer;", C_RELOJ),
 ("Form1.cs", 818, "BeginInvoke((MethodInvoker)SigueArranque);", "PantallaRocola.Encola(this, SigueArranque);", C_ENCARGO),
 ("Form1.cs", 1630, "this.BeginInvoke(new MethodInvoker(AbrePorFaltaDeMusica));", "PantallaRocola.Encola(this, AbrePorFaltaDeMusica);", C_ENCARGO),
 ("Form1.Biblioteca.cs", 3264, "private Timer posaTimer;", "private Reloj posaTimer;", C_RELOJ_DECL),
 ("Form1.Datos.cs", 46, "private Timer statsTimer;", "private Reloj statsTimer;", C_RELOJ_DECL),
 ("Form1.Portadas.cs", 30, "private Timer coverSettleTimer;", "private Reloj coverSettleTimer;", C_RELOJ_DECL),
 ("Form1.Video.cs", 31, "private Timer videoPoll;", "private Reloj videoPoll;", C_RELOJ_DECL),
 ("SalidaSonido.cs", 375, "private Timer reintentoSalida;", "private Reloj reintentoSalida;", C_RELOJ_DECL),
 ("SalidaSonido.cs", 495, "reintentoSalida = new Timer();", "reintentoSalida = new Reloj();", C_RELOJ),
 ("Form1.Ajustes.cs", 1690, "Timer tTrain = new Timer();", "Reloj tTrain = new Reloj();", C_RELOJ),
 ("D3D9Star.cs", 1997, "private readonly Timer timer = new Timer();", "private readonly Reloj timer = new Reloj();", C_RELOJ),
 ("AperturaAudio.cs", 213, "BeginInvoke((MethodInvoker)delegate", "PantallaRocola.Encola(this, delegate", C_ENCARGO),
 ("AperturaAudio.cs", 230, "BeginInvoke((MethodInvoker)delegate { action(); });", "PantallaRocola.Encola(this, delegate { action(); });", C_ENCARGO),
 ("AperturaAudio.cs", 251, "BeginInvoke((MethodInvoker)delegate { action(); });", "PantallaRocola.Encola(this, delegate { action(); });", C_ENCARGO),
 ("AperturaAudio.cs", 328, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("BombaSonido.cs", 293, "internal Action<MethodInvoker> vuelveALaPantalla", "internal Action<Action> vuelveALaPantalla", C_ACCION),
 ("BombaSonido.cs", 297, "Action<MethodInvoker> vuelve = vuelveALaPantalla;", "Action<Action> vuelve = vuelveALaPantalla;", C_ACCION),
 ("BombaSonido.cs", 301, "MethodInvoker enLaPantalla = delegate", "Action enLaPantalla = delegate", C_ACCION),
 ("BombaSonido.cs", 307, "try { BeginInvoke(enLaPantalla); }", "try { PantallaRocola.Encola(this, enLaPantalla); }", C_ENCARGO),
 ("SubirTodo.cs", 129, "Action<MethodInvoker> vuelve = vuelveALaPantalla;", "Action<Action> vuelve = vuelveALaPantalla;", C_ACCION),
 ("SubirTodo.cs", 140, "MethodInvoker pon = delegate", "Action pon = delegate", C_ACCION),
 ("SubirTodo.cs", 148, "try { BeginInvoke(pon); }", "try { PantallaRocola.Encola(this, pon); }", C_ENCARGO),
 ("SalaLocal.cs", 477, "MethodInvoker suelta = delegate", "Action suelta = delegate", C_ACCION),
 ("SalaLocal.cs", 483, "{ BeginInvoke(suelta); return; }", "{ PantallaRocola.Encola(this, suelta); return; }", C_ENCARGO),
 ("MedidasBitacora.cs", 119, "MethodInvoker toque = delegate { atendido.Set(); };", "Action toque = delegate { atendido.Set(); };", C_ACCION),
 ("MedidasBitacora.cs", 129, "pantalla.BeginInvoke(toque);", "PantallaRocola.Encola(pantalla, toque);", C_ENCARGO),
 ("Form1.Ajustes.cs", 1654, "dlg.BeginInvoke((MethodInvoker)delegate { if (!dlg.IsDisposed) pintaIa(); });", "PantallaRocola.Encola(dlg, delegate { if (!dlg.IsDisposed) pintaIa(); });", "EN LINUX: un encargo al hilo de la pantalla nativa, con la vida de la hoja (en XP, dlg.BeginInvoke)"),
 ("Form1.Biblioteca.cs", 1000, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1057, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1098, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1544, "BeginInvoke((MethodInvoker)delegate", "PantallaRocola.Encola(this, delegate", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1596, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1661, "BeginInvoke((MethodInvoker)delegate", "PantallaRocola.Encola(this, delegate", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1680, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1735, "BeginInvoke((MethodInvoker)delegate", "PantallaRocola.Encola(this, delegate", C_ENCARGO),
 ("Form1.Biblioteca.cs", 1751, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Datos.cs", 2519, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Portadas.cs", 172, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Portadas.cs", 219, "BeginInvoke((MethodInvoker)delegate {", "PantallaRocola.Encola(this, delegate {", C_ENCARGO),
 ("Form1.Portadas.cs", 653, "this.BeginInvoke((MethodInvoker)FlushCoverArrivals);", "PantallaRocola.Encola(this, FlushCoverArrivals);", C_ENCARGO),
 ("Form1.Sonido.cs", 1577, "if (InvokeRequired) { try { BeginInvoke((MethodInvoker)delegate {", "if (PantallaRocola.FueraDeSuHilo(this)) { try { PantallaRocola.Encola(this, delegate {", C_FUERA + "; y un encargo al hilo de la pantalla (BeginInvoke)"),
 ("Form1.Video.cs", 296, "this.BeginInvoke((MethodInvoker)delegate { PlayNext(); });", "PantallaRocola.Encola(this, delegate { PlayNext(); });", C_ENCARGO),
 ("PlanBiblioteca.cs", 98, "if (InvokeRequired) BeginInvoke((MethodInvoker)delegate {", "if (PantallaRocola.FueraDeSuHilo(this)) PantallaRocola.Encola(this, delegate {", C_FUERA + "; y un encargo al hilo de la pantalla (BeginInvoke)"),
 ("Rendimiento.cs", 617, "Application.DoEvents();", "PantallaRocola.Bombea();", C_BOMBEA),
 ("Rendimiento.cs", 632, "Application.DoEvents();", "PantallaRocola.Bombea();", C_BOMBEA),
 ("Form1.DiagnosticoCompleto.cs", 275, "Application.DoEvents();", "PantallaRocola.Bombea();", C_BOMBEA),
 ("Form1.DiagnosticoCompleto.cs", 334, "Application.DoEvents();", "PantallaRocola.Bombea();", C_BOMBEA),
]
archivos = {}
malos = 0
def grupo(a, com):
    return "relojes" if (a == "Program.cs" or com in (C_RELOJ, C_RELOJ_DECL)) else "encargos"
cambios = [c for c in cambios if GRUPO == "todo" or grupo(c[0], c[4]) == GRUPO]
for (a, l, viejo, nuevo, com) in cambios:
    if a not in archivos:
        with open(os.path.join(D, a), encoding="utf-8-sig", newline="") as f: archivos[a] = f.read().split("\n")
    lineas = archivos[a]
    t = lineas[l - 1]
    if viejo not in t:
        print("NO ESTA: %s:%d «%s» en «%s»" % (a, l, viejo, t.strip())); malos += 1; continue
    cr = t.endswith("\r")
    if cr: t = t[:-1]
    t = t.replace(viejo, nuevo, 1)
    # el comentario: al final; si ya habia uno, se le anade
    i = t.find("//")
    if i >= 0 and t[:i].count('"') % 2 == 0: t = t.rstrip() + "; " + com
    else: t = t.rstrip() + "   // " + com
    lineas[l - 1] = t + ("\r" if cr else "")
for a, lineas in archivos.items():
    ruta = os.path.join(D, a)
    bom = open(ruta, "rb").read().startswith(b"\xef\xbb\xbf")
    with open(ruta, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as f: f.write("\n".join(lineas))
print("cambiadas %d lineas en %d archivos; %d sin encontrar" % (len(cambios) - malos, len(archivos), malos))
