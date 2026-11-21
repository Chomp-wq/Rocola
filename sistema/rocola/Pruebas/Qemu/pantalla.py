#!/usr/bin/env python3
# LA HOJA PANTALLA DE CTRL+F4 EN QEMU, CON UN KMS DE VERDAD (grupo pantalla-ctrlf4, 20-11-2026; Xp/FASE11_pantalla.md). Sigue a prueba.py
# (su maquina, sus comprobaciones y su resumen) sobre un sistema YA INSTALADO por prueba.py (de fabrica, el de la junta de Spotify:
# BASE=/root/rocola-trabajo/act/qemu-spotify/xp.qcow2), con una capa qcow2 propia encima (Q/xp.qcow2: lo de BASE no se toca). El rocola-xp de
# esta rama (perfil qemu) y la orden rocola-pantalla van en un disco aparte (Q/pantalla.img: un MBR con una particion ext2, como a4.img),
# que rocola-discos monta en solo lectura; se copian encima de los del sistema y la rocola se reinicia como «reiniciar ya».
# La tarjeta es la VGA de bochs de QEMU (bochs-drm): da sus modos por EDID, cambia de modo por KMS de verdad y NO tiene tabla de gamma (los
# colores van en gris diciendo por que). NADA de esto es el A4 (radeon) ni un monitor de verdad.
#   Q=/root/rocola-trabajo/act/qemu-pantalla python3 pantalla.py PROGRAMA_QEMU   (la carpeta del dotnet publish -p:RocolaPerfil=qemu)
import os, re, shutil, struct, subprocess, sys, time
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
os.environ.setdefault("Q", "/root/rocola-trabajo/act/qemu-pantalla")
import prueba as P
from maquina import Control, pid

Q = P.Q
BASE = os.environ.get("BASE", "/root/rocola-trabajo/act/qemu-spotify/xp.qcow2")
REPO = os.path.abspath(os.path.join(AQUI, "../../../.."))
GUION = os.path.join(REPO, "sistema/board/rocola/rootfs_overlay/usr/bin/rocola-pantalla")
IMG = os.path.join(Q, "pantalla.img")
P.RESUMEN = open(os.path.join(Q, "pantalla.txt"), "a")
CAPS_DUENO = os.environ.get("CAPTURAS", "")
di, si, info, orden, ok, espera, teclas = P.di, P.si, P.info, P.orden, P.ok, P.espera, P.teclas

def captura(nombre):
    f = P.captura(nombre)
    if CAPS_DUENO: os.makedirs(CAPS_DUENO, exist_ok=True); shutil.copy(f, os.path.join(CAPS_DUENO, nombre))
    return f

def medida(f):
    from PIL import Image
    return "%dx%d" % Image.open(f).size

def disco(prog):
    """pantalla.img: 64 MB, MBR con una particion Linux (83) desde el sector 2048, ext2 con rocola-xp y rocola-pantalla (mke2fs -d)."""
    d = os.path.join(Q, "pantalla-disco"); shutil.rmtree(d, ignore_errors=True); os.makedirs(d)
    shutil.copy(os.path.join(prog, "rocola-xp"), d); shutil.copy(GUION, d)
    n = 64 * 2048
    with open(IMG, "wb") as f:
        f.truncate(n * 512)
        mbr = bytearray(512)
        mbr[446:462] = struct.pack("<B3sB3sII", 0, b"\0\0\0", 0x83, b"\0\0\0", 2048, n - 2048)
        mbr[510:512] = b"\x55\xaa"; f.seek(0); f.write(mbr)
    subprocess.run(["mke2fs", "-q", "-t", "ext2", "-E", "offset=%d" % (2048 * 512), "-d", d, IMG, "%dk" % ((n - 2048) // 2)], check=True)
    shutil.rmtree(d)

def principal(prog):
    di("=== pantalla: la hoja Pantalla de Ctrl+F4 con el KMS de verdad de QEMU (bochs-drm), sobre el sistema de %s" % BASE)
    if pid(P.MAQ): si("ya hay una maquina en %s" % P.MAQ, False); return
    if os.path.exists(P.XP): os.remove(P.XP)
    subprocess.run(["qemu-img", "create", "-q", "-f", "qcow2", "-b", BASE, "-F", "qcow2", P.XP], check=True)
    disco(prog)
    P.arranca(os.path.join(P.SON, "pantalla.wav"), extra=IMG)
    c = P.c
    P.espera_xp(600)
    si("rocola-discos monta el disco de la prueba", espera("test -x /rocola/discos/sdb1/rocola-xp", 300, 5))
    v = orden("/rocola/discos/sdb1/rocola-xp --version").strip()
    si("el rocola-xp de esta rama contesta --version en la maquina", v.startswith("rocola-xp programa"), v)
    orden("cp /rocola/discos/sdb1/rocola-xp /usr/lib/rocola-xp/rocola-xp.n && mv -f /usr/lib/rocola-xp/rocola-xp.n /usr/lib/rocola-xp/rocola-xp"
          " && echo '%s' > /usr/lib/rocola-xp/version && cp /rocola/discos/sdb1/rocola-pantalla /usr/bin/rocola-pantalla"
          " && chmod 755 /usr/bin/rocola-pantalla && rm -f /rocola/datos/xp/rocola_pantalla.txt* && sync" % v, 120)
    # la orden de la consola, con el sistema de verdad
    mo = orden("rocola-pantalla modos", 30)
    info("rocola-pantalla modos: " + " | ".join(mo.splitlines()[:6]))
    si("rocola-pantalla modos: la salida conectada de bochs y sus modos (1024x768 entre ellos)", "1024x768" in mo, mo.splitlines()[0] if mo else "")
    es = orden("rocola-pantalla estado", 30)
    si("rocola-pantalla estado sin nada guardado: automatica", "autom" in es.lower(), es.splitlines()[0] if es else "")
    # la rocola con el rocola-xp de la rama
    p = P.xp_pid()
    orden("echo 'prueba de la pantalla' > /run/rocola-xp-reinicio; kill -USR1 %s" % p)
    fin = time.time() + 300
    while time.time() < fin and P.xp_pid() in (None, p): time.sleep(5)
    p2 = P.xp_pid()
    si("la rocola se abre con el rocola-xp de la rama", bool(p2) and p2 != p and v in orden("tail -20 /rocola/datos/rocola.txt"))
    c.espera_cuadros(2, 400)
    b = "/rocola/datos/xp/rocola_os_bitacora.txt"
    bit = orden("grep -a 'pantalla\\|modos del monitor' %s | tail -4" % b, 30)
    info("bitacora: " + " / ".join(bit.splitlines()))
    m = re.search(r"modos del monitor \((\d+)\): (.*)", bit)
    si("la bitacora dice los modos que da bochs por KMS, con el automatico (*) en 1024x768", bool(m) and "1024x768" in (m.group(2) if m else "") and "*" in (m.group(2) if m else ""),
       m.group(0)[:150] if m else "")
    si("la bitacora: controlador bochs y sin tabla de gamma ni CTM", re.search(r"controlador bochs[^;]*; gamma no; CTM no", bit) is not None)
    f0 = captura("p1-antes.png"); si("antes: la pantalla en 1024x768 (lo automatico)", medida(f0) == "1024x768", medida(f0))
    # Ctrl+F4 (el foco en la pestana); Tab x4 (junta 2: con las pestanas Navegador y Archivos, dos paradas mas, como en
    # Pruebas/RocolaOs/pantalla/prueba.sh): la barra de la resolucion; derecha: la siguiente mas grande (en bochs, 1280x768, que solo
    # tiene 60 Hz: la barra de los hercios va en gris y el tabulador la salta); Tab: «Probar»; Intro
    teclas("m", pausa=2.0)   # (despierta de la atraccion si hace falta y acredita: la moneda)
    c.espera_cuadros(1, 200)
    teclas("ctrl-f4", pausa=2.0); c.espera_cuadros(1, 200)
    f1 = captura("p2-ventana.png")
    si("Ctrl+F4 abre la ventana de Rocola OS (la pantalla cambia)", P.colores(f1) > 50, "%d colores" % P.colores(f1))
    teclas("tab", "tab", "tab", "tab", "right", "tab", pausa=1.5); c.espera_cuadros(1, 200)
    t0 = time.time(); teclas("ret", pausa=0.5)
    # el modo nuevo: lo dice la bitacora y la pantalla de QEMU cambia de medida
    nuevo = None; fin = time.time() + 120
    while time.time() < fin and not nuevo:
        mm = re.search(r"prueba de (\d+x\d+@\d+): ", orden("grep -a 'Se ve bien' %s | tail -1" % b, 20))
        nuevo = mm.group(1) if mm else None
        if not nuevo: time.sleep(3)
    f2 = captura("p3-prueba-15s.png")
    ancho_nuevo = nuevo.split("@")[0] if nuevo else "?"
    si("con Tab, Tab, derecha, Tab e Intro: se prueba otro modo y QEMU muestra la pantalla a esa medida", bool(nuevo) and medida(f2) == ancho_nuevo,
       "%s; captura %s" % (nuevo, medida(f2)))
    # sin contestar: vuelve sola (lo que tardo, por las horas de la bitacora: de «¿Se ve bien?» a «nadie contesto»)
    fin = time.time() + 120; vuelta = None
    while time.time() < fin:
        r = orden("grep -a -e 'Se ve bien' -e 'nadie contest' %s | tail -2 | cut -c12-19" % b, 20).split()
        if "nadie contest" in orden("grep -a 'nadie contest' %s | tail -1" % b, 20) and len(r) == 2:
            h = [int(x[0:2]) * 3600 + int(x[3:5]) * 60 + int(x[6:8]) for x in r]; vuelta = h[1] - h[0]; break
        time.sleep(2)
    c.espera_cuadros(1, 200)
    f3 = captura("p4-vuelta-sola.png")
    si("sin contestar vuelve sola a 1024x768 a los 15 s (por el reloj; la bitacora va por segundos)", vuelta is not None and 14 <= vuelta <= 20 and medida(f3) == "1024x768",
       "%s s; captura %s" % (vuelta, medida(f3)))
    si("sin confirmar no se guardo ningun modo", "modo=" not in orden("cat /rocola/datos/xp/rocola_pantalla.txt 2>/dev/null", 20).replace("modo=auto", ""))
    # otra vez, y «Si»: tras la vuelta el foco queda en la barra de la resolucion (derecha, Tab, Intro), y en la pregunta Tab e Intro
    pr = orden("grep -ac 'Se ve bien' %s" % b, 20).strip()
    teclas("right", "tab", pausa=1.5); c.espera_cuadros(1, 200); teclas("ret", pausa=0.5)
    fin = time.time() + 120
    while time.time() < fin and orden("grep -ac 'Se ve bien' %s" % b, 20).strip() == pr: time.sleep(3)
    c.espera_cuadros(1, 200)
    teclas("tab", pausa=1.0); teclas("ret", pausa=1.0)
    fin = time.time() + 120; g = ""
    while time.time() < fin:
        g = orden("cat /rocola/datos/xp/rocola_pantalla.txt 2>/dev/null", 20)
        if re.search(r"^modo=\d+x\d+@\d+", g, re.M): break
        time.sleep(3)
    si("probada otra vez y confirmada con «Si»: el modo queda guardado en rocola_pantalla.txt", re.search(r"^modo=%s" % re.escape(nuevo or "-"), g, re.M) is not None,
       g.replace("\n", " ")[:120])
    c.espera_cuadros(1, 200); f4 = captura("p5-guardado.png")
    si("y la pantalla sigue en esa medida", medida(f4) == ancho_nuevo, medida(f4))
    es = orden("rocola-pantalla estado", 30)
    si("rocola-pantalla estado lo dice", (nuevo or "-") in es, es.replace("\n", " ")[:150])
    teclas("esc", pausa=2.0)
    # el siguiente arranque: lo guardado antes de pintar
    p = P.xp_pid(); orden("echo 'prueba de la pantalla' > /run/rocola-xp-reinicio; kill -USR1 %s" % p)
    fin = time.time() + 300
    while time.time() < fin and P.xp_pid() in (None, p): time.sleep(5)
    c.espera_cuadros(2, 400); f5 = captura("p6-arranque-guardado.png")
    si("al volver a abrirse, la rocola arranca ya en lo guardado", medida(f5) == ancho_nuevo, medida(f5))
    # la consola: rocola-pantalla auto
    au = orden("rocola-pantalla auto", 120)
    info("rocola-pantalla auto: " + au.replace("\n", " / ")[:200])
    fin = time.time() + 400
    while time.time() < fin and not P.xp_pid(): time.sleep(5)
    c.espera_cuadros(2, 400); f6 = captura("p7-auto.png")
    si("rocola-pantalla auto: lo guardado se borra y la rocola vuelve a abrirse en lo automatico (1024x768)",
       medida(f6) == "1024x768" and "modo=" not in orden("cat /rocola/datos/xp/rocola_pantalla.txt 2>/dev/null", 20), medida(f6))
    err = orden("grep -a 'Rocola OS' /rocola/datos/xp/rocola_error.txt | tail -3", 20)
    si("rocola_error.txt sin fallos de Rocola OS", err.strip() == "", err[:200])
    orden("sync", 30); P.apaga()
    di("=== pantalla: %d bien, %d mal" % (P.bien, P.mal))

if __name__ == "__main__":
    principal(os.path.abspath(sys.argv[1]))
    sys.exit(1 if P.mal else 0)
