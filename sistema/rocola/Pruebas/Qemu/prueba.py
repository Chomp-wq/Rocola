#!/usr/bin/env python3
# LA PRUEBA DEL SISTEMA ENTERO EN QEMU (grupo qemu, 14-11-2026): Rocola OS con rocola-xp como unico programa, de punta a punta, con datos
# INVENTADOS (mundo.sh: canciones que son tonos, un video de barras, la carpeta de una rocola de XP falsa; discoxp.sh: el disco de XP
# falso; arma.sh: la memoria USB). QEMU sin KVM (TCG): todo va muy lento (la pantalla de la rocola con Mesa softpipe tarda decenas de
# segundos por cuadro), asi que los tiempos son generosos y lo que depende de lo rapido que pinta se dice aparte (INFO).
#   python3 prueba.py [FASE...]   sin nada, todas en orden: instala arranque teclas video consola caida apagon ia boton (y aparte, a4)
#   Q: la carpeta de trabajo (por omision /root/rocola-trabajo/act/qemu): ahi estan usb.img y discoxp.img, y quedan xp.qcow2 (el disco de
#      XP con lo que escribio Rocola OS), capturas/ (las pantallas), sonido/ (lo que sono, en WAV) y prueba.txt (el resumen).
# Cada comprobacion dice BIEN o MAL en una linea (INFO: lo que se apunta sin juzgar); al final, cuantas.
import os, re, subprocess, sys, time
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
from maquina import lanza, Control, pid, espera_fin
import numpy as np
from PIL import Image

Q = os.environ.get("Q", "/root/rocola-trabajo/act/qemu")
MAQ, CAPS, SON = os.path.join(Q, "maq"), os.path.join(Q, "capturas"), os.path.join(Q, "sonido")
USB, XPIMG, XP = os.path.join(Q, "usb.img"), os.path.join(Q, "discoxp.img"), os.path.join(Q, "xp.qcow2")
MUNDO = os.path.join(Q, "mundo")
for d in (MAQ, CAPS, SON): os.makedirs(d, exist_ok=True)
RESUMEN = open(os.path.join(Q, "prueba.txt"), "a")
bien = mal = 0
c = None

def di(s):
    s = time.strftime("%H:%M:%S ") + s
    print(s, flush=True); RESUMEN.write(s + "\n"); RESUMEN.flush()

def si(desc, ok, detalle=""):
    global bien, mal
    if ok: bien += 1
    else: mal += 1
    di(("BIEN  " if ok else "MAL   ") + desc + (("  [" + detalle + "]") if detalle else ""))
    return ok

def info(desc): di("INFO  " + desc)

def orden(cmd, maximo=60):
    cod, sale = c.orden(cmd, maximo)
    return sale if cod is not None else ""

def ok(cmd, maximo=60):
    cod, _ = c.orden(cmd, maximo); return cod == 0

def espera(cmd, maximo, cada=5): return c.espera(cmd, maximo, cada)

def captura(nombre):
    f = os.path.join(CAPS, nombre); c.captura(f); return f

def colores(f):
    """Cuantos colores distintos tiene una captura (la consola de texto tiene 2 o 3; la rocola, miles)."""
    a = np.asarray(Image.open(f).convert("RGB")).reshape(-1, 3)
    return len(np.unique(a[::7], axis=0))

def claros(f):
    a = np.asarray(Image.open(f).convert("L")); return int((a > 60).sum())

def vivos(f):
    """Cuantos puntos de colores vivos tiene una captura (las barras del video de prueba: rojo, verde, amarillo, azul, magenta, cian; el
    cielo de la rocola es oscuro y apagado)."""
    a = np.asarray(Image.open(f).convert("RGB")).astype(int)
    puro = ((a < 40) | (a > 200)).all(axis=2) & (a.max(axis=2) > 200) & (a.min(axis=2) < 40)   # (cada canal o casi 0 o casi 255)
    return int(puro.sum())

def tonos(wav, desde=0.0):
    """Lo que sono (sonido.py): [(desde, hasta, frecuencia o 0/-1, dBFS)]"""
    r = subprocess.run([sys.executable, "-I", os.path.join(AQUI, "sonido.py"), wav, str(desde)], capture_output=True, text=True).stdout
    sale = []
    for l in r.splitlines()[1:]:
        m = re.match(r"\s*([\d.]+) -\s+([\d.]+) s\s+(\S+)(?: Hz)?\s+(-?[\d.]+) dBFS", l)
        if not m: continue
        f = m.group(3); f = 0 if f == "silencio" else (-1 if f == "ruido" else int(f))
        sale.append((float(m.group(1)), float(m.group(2)), f, float(m.group(4))))
    return sale

def sono(wav, hz, minimo_s, desde=0.0):
    t = sum(b - a for a, b, f, db in tonos(wav, desde) if f > 0 and abs(f - hz) <= 2 and db > -40)
    return t >= minimo_s, "%.0f s de %d Hz" % (t, hz)

def wav_actual():
    """El WAV que esta grabando el QEMU de ahora (el de su ultima linea en maq/qemu.txt)."""
    r = ""
    for l in open(os.path.join(MAQ, "qemu.txt"), errors="replace"):
        m = re.search(r"wav,id=snd,path=(\S+)", l)
        if m: r = m.group(1)
    return r

def arranca(wav, usb=None, extra=None):
    global c
    lanza(MAQ, XP, wav, usb, extra=extra)
    c = Control(MAQ)
    if not c.espera("true", 600, 5): raise SystemExit("la maquina no contesto por el puerto serie")

def apaga():
    """Se va la luz (QEMU en seco)."""
    if c and pid(MAQ): c.apagon()

def xp_pid():
    """El rocola-xp abierto como rocola (no el importador)."""
    s = orden("for p in $(pidof rocola-xp); do tr '\\0' '\\n' < /proc/$p/cmdline | grep -qx -- --importa-xp || echo $p; done", 20).split()
    return s[0] if s else None

def espera_xp(maximo=600):
    fin = time.time() + maximo
    while time.time() < fin:
        p = xp_pid()
        if p: return p
        time.sleep(5)
    return None

def estado():
    return orden("cat /rocola/datos/xp/rocola_estado.txt 2>/dev/null", 20)

def creditos():
    m = re.search(r"creditos=(\d+)", estado()); return int(m.group(1)) if m else None

def teclas(*ks, pausa=1.0):
    for k in ks: c.tecla(k, pausa)

# ======================================================================================================================================
def instala():
    di("=== instala: la memoria USB y el disco de XP falso; el instalador en tty1; ENTER; hasta que acaba")
    if os.path.exists(XP): os.remove(XP)
    subprocess.run(["qemu-img", "create", "-q", "-f", "qcow2", "-b", XPIMG, "-F", "raw", XP], check=True)
    arranca(os.path.join(SON, "instala.wav"), USB)
    si("el instalador encontro el disco y dice que hace falta", espera("grep -q 'hacen falta' /tmp/rocola-instalar.txt", 600))
    log = orden("cat /tmp/rocola-instalar.txt")
    si("elige la particion de la musica (sda2) y ve XP en sda1", "elegido /dev/sda2" in log and "XP: sda1" in log)
    si("encuentra los datos de la rocola de XP (Rocola/Release)", "datos XP: /tmp/rocola-mira/sda2/Rocola/Release" in log)
    m = re.search(r"arranque: (\d+) bytes, hueco (\d+) bytes", log)
    si("el arranque cabe en el hueco de XP (62 sectores)", bool(m) and int(m.group(1)) <= int(m.group(2)) == 31744, m.group(0) if m else "")
    captura("01-instalador.png")
    c.tecla("ret")
    si("instalado (copia a)", espera("grep -q 'instalado en la copia a' /tmp/rocola-instalar.txt", 1800, 15))
    log = orden("cat /tmp/rocola-instalar.txt")
    si("guardo el arranque de antes (63 sectores) y GRUB sin errores", "Arranque de antes guardado (63 sectores)" in log and "No error reported" in log)
    captura("02-instalado.png")
    orden("sync", 30); apaga()

def arranque():
    di("=== arranque: el disco de XP solo (instalado); la primera vez la prueba de sonido y grafica, luego el lanzador: importador y rocola-xp")
    arranca(os.path.join(SON, "arranque.wav"))
    cmd = orden("cat /proc/cmdline")
    si("arranca el sistema instalado (GRUB del disco de XP, copia a)", "rocola.slot=a" in cmd and "rocola.disco=2b3c4d5e6f708192" in cmd, cmd.strip()[:120])
    si("rocola-discos encuentra la carpeta de XP y la musica", "datos de la rocola de XP en /rocola/disco/Rocola/Release" in orden("cat /run/rocola-discos.txt")
       and orden("cat /run/rocola-musica").strip() == "/rocola/disco/musica")
    si("la prueba de sonido y grafica acaba (primer arranque)", espera("grep -q 'Pantalla de la rocola' /root/rocola-prueba.txt", 1500, 15))
    p = orden("grep -e '^Sonido:' -e '^Grafica:' -e '^Rocola:' -e '^Pantalla de la rocola' /root/rocola-prueba.txt")
    info("rocola-prueba: " + " | ".join(p.splitlines()))
    si("rocola-prueba: el tono, la cancion de prueba y el cubo", "Sonido: sono" in p and "Rocola: toco la cancion de prueba" in p and "Grafica: dibujo el cubo" in p)
    si("el lanzador corre el importador y abre rocola-xp", espera("grep -q 'rocola-xp --importa-xp: importado de /rocola/disco/Rocola/Release a /rocola/datos/xp' /rocola/datos/rocola.txt", 600, 10))
    p = espera_xp(600)
    si("rocola-xp abierta", bool(p), str(p))
    env = orden("tr '\\0' '\\n' < /proc/%s/environ | grep ^ROCOLA_ | sort" % p)
    si("con sus datos, el material de la IA en el disco y la tarjeta", "ROCOLA_DATOS=/rocola/datos/xp" in env and "ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp" in env
       and "ROCOLA_ALSA=plughw:0,0" in env, env.replace("\n", " "))
    marca = orden("cat /rocola/datos/xp/rocola_importado_xp.txt")
    si("la marca: de que carpeta y la unidad D: al disco de la rocola", "carpeta=/rocola/disco/Rocola/Release" in marca and "unidad D: -> /rocola/disco" in marca)
    si("las tablas traducidas, sin rutas de Windows", ok("! grep -l 'D:\\\\' /rocola/datos/xp/rocola_sonoridad.txt /rocola/datos/xp/rocola_volumen.txt /rocola/datos/xp/rocola_eq.txt /rocola/datos/xp/rocola_fuentes.txt /rocola/datos/xp/rocola_duracion.txt /rocola/datos/xp/rocola_stats.txt /rocola/datos/xp/rocola_config.txt")
       and ok("[ $(grep -c '|/rocola/disco/musica/' /rocola/datos/xp/rocola_volumen.txt) = 6 ]"))
    si("la cancion con acentos, con su nombre del disco", ok("grep -q '|/rocola/disco/musica/Banda Ficticia - Primer Disco/02 Canción Ñandú.mp3' /rocola/datos/xp/rocola_volumen.txt"))
    si("el estado del dia de XP (5 creditos) NO se trae", "creditos=5" not in estado())
    si("los botones de XP tal cual", orden("md5sum < /rocola/datos/xp/rocola_teclas.txt").split()[0:1] == orden("md5sum < /rocola/disco/Rocola/Release/rocola_teclas.txt").split()[0:1])
    cfg = orden("cat /rocola/datos/xp/rocola_config.txt")
    si("la configuracion: la de XP con su carpeta traducida, una sola vez", cfg.count("dir=/rocola/disco/musica") == 1 and "noche_pct=80" in cfg, cfg.replace("\n", " ")[:120])
    # la carpeta de XP sin tocar: lo mismo (contenido y fecha) que en el disco de XP falso
    host = set(subprocess.run("cd '%s/d/Rocola/Release' && find . -type f -print0 | xargs -0 md5sum" % MUNDO, shell=True, capture_output=True, text=True).stdout.splitlines())
    dentro = set(l for l in orden("cd /rocola/disco/Rocola/Release && find . -type f | while read -r f; do md5sum \"$f\"; done").splitlines() if l.strip())
    si("la carpeta de XP sin tocar (los mismos archivos y contenido)", host == dentro and len(host) > 10, "%d y %d archivos" % (len(host), len(dentro)))
    c.espera_cuadros(2, 300)
    f = captura("03-rocola.png")
    si("la rocola en la pantalla por KMS (no la consola de texto)", colores(f) > 500, "%d colores" % colores(f))

def teclas_y_sonido():
    di("=== teclas: la moneda (la tecla de rocola_teclas.txt: M) y pedir la primera del disco de la letra B; suena")
    p = xp_pid() or espera_xp(300)
    c.espera_cuadros(1, 300)
    # (la moneda despierta y acredita; ENTER abre el disco elegido y ENTER pide su primera cancion: seguidas, porque con la tarjeta en QEMU
    # cada cuadro tarda 20 a 30 s y a los 40 s sin teclas entra la atraccion)
    teclas("m", "ret", "ret", pausa=1.5)
    si("pedida: «01 Pulso Grave» suena y quedan 3 de los 4 creditos de la moneda",
       espera("grep -q 'sonando=S|/rocola/disco/musica/Banda Ficticia - Primer Disco/01 Pulso Grave.mp3' /rocola/datos/xp/rocola_estado.txt", 400, 5)
       and creditos() == 3, "creditos=%s" % creditos())
    c.espera_cuadros(1, 300); captura("04-sonando.png")
    fin = time.time() + 600
    while time.time() < fin and "sonando=" in estado(): time.sleep(10)
    ok_, det = sono(wav_actual(), 196, 40)
    si("SUENA: el WAV grabado de la tarjeta tiene la cancion (196 Hz)", ok_, det)
    stats = orden("grep 'Pulso Grave' /rocola/datos/xp/rocola_stats.txt")
    si("lo mas sonado cuenta la que sono (2 de XP + 1)", stats.startswith("3|"), stats.strip())
    fu = orden("grep 'Pulso Grave' /rocola/datos/xp/rocola_fuentes.txt").strip().split("|")
    si("su sello (rocola_fuentes.txt) vale tal cual: no se volvio a medir", len(fu) > 3 and fu[1] == fu[2] == "636923016001234567", "|".join(fu[:3]))

def consola():
    di("=== consola: Ctrl+Alt+F2 cierra la rocola con SIGUSR1 (conserva el estado) y se ve la consola; Ctrl+Alt+F1 la vuelve a abrir")
    p = xp_pid() or espera_xp(300)
    antes = creditos() or 0
    teclas("m")
    fin = time.time() + 300
    while time.time() < fin and (creditos() or 0) == antes: time.sleep(5)
    antes = creditos(); info("creditos antes: %s" % antes)
    t0 = time.time(); c.tecla("ctrl-alt-f2")
    fin = time.time() + 120
    while time.time() < fin and xp_pid(): time.sleep(2)
    t = time.time() - t0
    si("rocola-xp se cerro", not xp_pid(), "%.0f s" % t)
    bit = orden("tail -3 /rocola/datos/rocola_actualizaciones.txt")
    if "no se cerro en 20 s" in bit: info("en QEMU no se cerro en orden en 20 s (cada cuadro tarda decenas de segundos): el lanzador la corto como un apagon (el estado queda)")
    si("apuntado: «se cerro para la consola de mantenimiento»", "La rocola se cerro para la consola de mantenimiento" in bit)
    si("el estado se conserva (creditos)", creditos() == antes, "creditos=%s" % creditos())
    time.sleep(6)
    f = captura("08-consola.png")
    si("la consola de mantenimiento se ve sin tocar nada («rocola login:»)", claros(f) > 400, "%d puntos claros" % claros(f))
    si("no se abre en la consola", not xp_pid() and ok("[ -f /run/rocola-en-consola ]"))
    c.tecla("ctrl-alt-f1")
    p2 = espera_xp(300)
    si("Ctrl+Alt+F1: la rocola se abre otra vez", bool(p2) and ok("[ ! -f /run/rocola-en-consola ]"))
    c.espera_cuadros(2, 300); captura("09-vuelve.png")
    si("con su estado (creditos)", creditos() == antes, "creditos=%s" % creditos())

def caida():
    di("=== caida: kill -SEGV a rocola-xp; el lanzador la vuelve a abrir y lo apunta")
    p = xp_pid() or espera_xp(300)
    time.sleep(15)
    # (un SIGSEGV mandado con kill no es un fallo de memoria de verdad: el primero lo recoge el manejador de .NET, que vuelve a poner el
    # de fabrica y sigue; el segundo ya la tira, como la tiraria un fallo de verdad. Visto en QEMU el 14-11-2026: con uno solo, rocola-xp
    # seguia abierta 5 minutos despues)
    orden("kill -SEGV %s" % p)
    fin = time.time() + 30
    while time.time() < fin and xp_pid() == p: time.sleep(3)
    if xp_pid() == p:
        info("el primer SIGSEGV no la tiro (lo recoge el manejador de .NET): un segundo")
        orden("kill -SEGV %s" % p)
    si("apuntado: «rocola-xp se cayo (codigo 139)»", espera("grep -q 'rocola-xp se cayo (codigo 139)' /rocola/datos/rocola_actualizaciones.txt", 300, 5),
       orden("grep 'se cayo' /rocola/datos/rocola_actualizaciones.txt | tail -1").strip())
    p2 = espera_xp(300)
    si("y se vuelve a abrir (otro proceso)", bool(p2) and p2 != p, "%s -> %s" % (p, p2))

def apagon():
    di("=== apagon: con creditos, una sonando y otra en la cola se va la luz (y el sistema esta «en prueba»); al volver reanuda y se confirma")
    p = xp_pid() or espera_xp(300)
    c.espera_cuadros(2, 300)
    teclas("m", "ret", "ret", "down", "ret")
    si("una sonando y otra en la cola", espera("grep -q sonando= /rocola/datos/xp/rocola_estado.txt && grep -q cola= /rocola/datos/xp/rocola_estado.txt", 400, 5))
    antes = estado(); info("estado antes del apagon: " + " ".join(l for l in antes.splitlines() if not l.startswith("ts=")))
    orden("echo 0 > /rocola/disco/rocola-os/probando; sync", 30)   # (lo que deja rocola-actualiza con un sistema nuevo)
    time.sleep(10)
    apaga()
    arranca(os.path.join(SON, "apagon.wav"))
    si("el arranque conto el sistema en prueba (probando = 1)", orden("cat /rocola/disco/rocola-os/probando").strip() == "1")
    p = espera_xp(600)
    despues = estado()
    lim = lambda e: sorted(l for l in e.splitlines() if l.startswith(("creditos=", "sonando=", "cola=")))
    si("el estado sobrevivio al apagon (creditos, la que sonaba y la cola)", lim(antes) == lim(despues) and len(lim(antes)) >= 3, " ".join(lim(despues)))
    c.espera_cuadros(1, 300); captura("10-reanuda.png")
    # (lo que sonaba vuelve a sonar y despues la de la cola: se mira en lo grabado desde que prendio, cuando ya no hay nada sonando; con
    # QEMU la pantalla tarda tanto que las dos pueden acabar antes de la primera captura)
    fin = time.time() + 600
    while time.time() < fin and "sonando=" in estado(): time.sleep(10)
    wav = wav_actual()
    ok1, d1 = sono(wav, 196, 30); ok2, d2 = sono(wav, 660, 30)
    si("y reanuda: suena la que sonaba y despues la de la cola", ok1 and ok2, d1 + ", " + d2)
    t0 = time.time()
    si("el sistema nuevo se confirma (rocola-xp 2 min seguidos en pie)", espera("[ ! -f /rocola/disco/rocola-os/probando ]", 900, 10), "%.0f s" % (time.time() - t0))
    s = orden("cat /rocola/disco/rocola-os/sistema.txt")
    si("apuntado en sistema.txt: «quedo bien»", "quedo bien" in s)
    d = orden("a=$(date -r /run/rocola-confirmado +%s); p=$(for q in $(pidof rocola-xp); do echo $q; done | head -1); "
              "s=$(awk '{print int($22/100)}' /proc/$p/stat); u=$(cut -d. -f1 /proc/uptime); n=$(date +%s); echo $(( (u - (n - a)) - s ))").strip()
    si("no antes de 120 s de rocola-xp en pie", d.lstrip("-").isdigit() and int(d) >= 115, "%s s" % d)

def ia():
    di("=== ia: lo grande de rocola-xp (material de la IA, miniaturas, respaldo) va al disco de la rocola (ROCOLA_IA_DATOS), no a datos.img")
    xp_pid() or espera_xp(300)
    si("el material de la IA de la cancion nueva (WAV) en ia-xp", espera("[ -s /rocola/disco/rocola-os/ia-xp/rocola_ia_datos.bin ]", 1500, 20))
    si("de que cancion se saco, en lo aprendido", ok("grep -q '03 Ruido Nuevo.wav' /rocola/datos/xp/rocola_ia_hechas.txt"))
    si("el respaldo de lo aprendido en ia-xp", ok("ls /rocola/disco/rocola-os/ia-xp/respaldo/*/rocola_volumen.txt"))
    si("las miniaturas de las portadas en ia-xp", ok("[ -n \"$(ls /rocola/disco/rocola-os/ia-xp/rocola_miniaturas)\" ]"), orden("ls /rocola/disco/rocola-os/ia-xp/rocola_miniaturas | wc -l").strip())
    si("nada de eso en /rocola/datos/xp (datos.img)", ok("! ls -d /rocola/datos/xp/rocola_ia_datos.* /rocola/datos/xp/rocola_miniaturas /rocola/datos/xp/respaldo 2>/dev/null | grep -q ."))
    info("datos.img: " + orden("df -k /rocola/datos | tail -1").strip())
    si("el disco NTFS de la rocola queda limpio (S99rocola hace sync)", espera("[ \"$(sed -n 7p /proc/fs/ntfs3/sda2/volinfo)\" = clean ]", 120, 10))

def boton():
    di("=== boton: el boton de encendido (ACPI) cierra rocola-xp con SIGTERM, borra el estado del dia y apaga")
    # En QEMU la pantalla de la tarjeta (softpipe) tarda decenas de segundos por cuadro y rocola-xp atiende el cierre entre cuadro y cuadro:
    # no llega en los 15 s que le da /etc/acpi/PWRF/00000080. Para probar la cadena del boton se pone el dibujo por procesador (gpu=0:
    # unos 5 s por cuadro en QEMU), cerrando antes la rocola con la consola (conserva el estado).
    tarjeta(False)
    espera_xp(300); c.espera_cuadros(3, 300)
    teclas("m"); espera("grep -q creditos= /rocola/datos/xp/rocola_estado.txt", 120, 5)
    info("estado antes del boton: creditos=%s" % creditos())
    c.monitor("system_powerdown")
    t0 = time.time(); borrado = None
    while time.time() - t0 < 300 and pid(MAQ):
        if borrado is None and ok("[ ! -f /rocola/datos/xp/rocola_estado.txt ]", 5): borrado = time.time() - t0
        time.sleep(1)
    si("rocola_estado.txt se borro (cierre con WindowsShutDown)", borrado is not None, "%.0f s" % (borrado or -1))
    si("y la maquina se apago sola", espera_fin(MAQ, 300), "%.0f s" % (time.time() - t0))
    arranca(os.path.join(SON, "boton.wav"))
    si("al prender, sin creditos del dia anterior", "creditos=" not in estado())
    err = orden("tr -d '\\r' < /rocola/datos/xp/rocola_error.txt | grep -v -e 'Pantalla parada' -e '^(sin excepcion)' -e '^$' -e 'Latencia de audio' -e 'Sin huecos' -e 'MP3 sin huecos' -e 'Volumen de Windows' | head -20")
    info("rocola_error.txt (sin las paradas de pantalla, que en QEMU son de cada cuadro): " + (err.replace("\n", " | ")[:600] or "nada"))
    info("bitacora: " + orden("cat /rocola/datos/rocola_actualizaciones.txt").replace("\n", " | "))
    orden("sync", 30); apaga()

def tarjeta(con):
    """Con la tarjeta (gpu=1, lo de fabrica) o con el dibujo por procesador (gpu=0): se cambia rocola_config.txt con la rocola cerrada
    (la consola de mantenimiento: la cierra con SIGUSR1, conservando el estado) y se vuelve a la 1."""
    hay0 = "gpu=0" in orden("cat /rocola/datos/xp/rocola_config.txt")
    if hay0 != con: return
    c.tecla("ctrl-alt-f2")
    fin = time.time() + 120
    while time.time() < fin and xp_pid(): time.sleep(2)
    orden("f=/rocola/datos/xp/rocola_config.txt; grep -v '^gpu=' $f > $f.n; %s mv $f.n $f" % ("" if con else "printf 'gpu=0\\r\\n' >> $f.n;"))
    info("rocola_config.txt: " + ("con la tarjeta (sin gpu=0)" if con else "gpu=0 (dibujo por procesador)"))
    c.tecla("ctrl-alt-f1")
    espera_xp(300)

def video():
    di("=== video: pedir el video (la letra de los videos) y verlo con la tarjeta (gpu=1, lo de fabrica)")
    # Para llegar a la letra de los videos hay que pasar de letra cuatro veces (ARRIBA), y la rocola solo cambia de letra una vez por
    # cuadro (como en XP: las pulsaciones que llegan juntas, mientras pasa la pagina, se pierden). Con la tarjeta, en QEMU, cada cuadro
    # tarda 20 a 30 s y a los 40 s sin teclas entra la atraccion: se navega con el dibujo por procesador (gpu=0, unos 5 s por cuadro), se
    # pide el video y despues se vuelve a la tarjeta por la consola de mantenimiento (SIGUSR1: el estado, con el video, se conserva) y la
    # rocola lo reanuda con la tarjeta.
    if not pid(MAQ): arranca(os.path.join(SON, "video.wav"))
    wav = wav_actual()
    tarjeta(False)
    espera_xp(300); c.espera_cuadros(2, 300)
    antes = creditos() or 0
    teclas("m"); c.espera_cuadros(1, 120)
    for _ in range(4): teclas("up"); c.espera_cuadros(1, 120)
    f = captura("11-letra-videos.png")
    teclas("ret"); c.espera_cuadros(1, 120); teclas("ret")
    si("el video pedido (la letra de los videos, un credito)", espera("grep -q 'Barras de Prueba.mp4' /rocola/datos/xp/rocola_estado.txt", 300, 3),
       "creditos %s -> %s" % (antes, creditos()))
    desde = max(0, os.path.getsize(wav) - 44) / 176400.0   # (lo grabado hasta aqui: lo de despues ya es con la tarjeta)
    tarjeta(True)
    vio = None; n = 0
    fin = time.time() + 900
    while time.time() < fin:
        if "Barras de Prueba" not in estado(): break
        n += 1; f = captura("12-video-tarjeta-%d.png" % n)
        v = vivos(f)
        if vio is None or v > vio[1]: vio = (f, v)
        time.sleep(6)
    ok_, det = sono(wav, 880, 15, desde)
    si("con la tarjeta, el video suena (880 Hz en el WAV)", ok_, det + " en " + os.path.basename(wav))
    if vio:
        info("la captura con mas colores vivos mientras estaba el video: %s (%d puntos)" % (os.path.basename(vio[0]), vio[1]))
        si("y se ve (las barras de colores del video en la pantalla)", vio[1] > 100000, "%d puntos de colores vivos" % vio[1])

def a4():
    di("=== a4: el rocola-xp del perfil del A4 (x86-64-v2 y AVX: el que pone rapida.sh) en el procesador emulado SandyBridge (AVX sin AVX2)")
    # Va en un disco aparte (a4.img: una particion ext2 con ese rocola-xp), que rocola-discos monta en solo lectura; se copia encima del
    # del sistema instalado y la rocola se reinicia como en «reiniciar ya» (SIGUSR1 con su marca).
    A4 = os.path.join(Q, "a4.img")
    if not os.path.exists(A4): si("falta %s (ver LEEME.md)" % A4, False); return
    if pid(MAQ): orden("sync", 30); apaga()
    arranca(os.path.join(SON, "a4.wav"), extra=A4)
    tarjeta(True)
    cpu = orden("grep -m1 -o -w -e avx2 -e avx /proc/cpuinfo | sort -u | tr '\\n' ' '").strip()
    si("el procesador emulado tiene AVX y no AVX2", cpu == "avx", cpu)
    espera_xp(600)
    v = orden("/rocola/discos/sdb1/rocola-xp --version").strip()
    si("el rocola-xp del A4 contesta --version aqui", v.startswith("rocola-xp programa"), v)
    orden("cp /rocola/discos/sdb1/rocola-xp /usr/lib/rocola-xp/rocola-xp.a4 && mv -f /usr/lib/rocola-xp/rocola-xp.a4 /usr/lib/rocola-xp/rocola-xp && echo '%s' > /usr/lib/rocola-xp/version && sync" % v, 120)
    p = xp_pid()
    orden("echo 'prueba del A4' > /run/rocola-xp-reinicio; kill -USR1 %s" % p)
    fin = time.time() + 300
    while time.time() < fin and xp_pid() in (None, p): time.sleep(5)
    p2 = xp_pid()
    si("la rocola se abre con el rocola-xp del A4", bool(p2) and p2 != p and v in orden("tail -20 /rocola/datos/rocola.txt"))
    c.espera_cuadros(3, 400); f = captura("13-a4.png")
    si("y pinta la pared por KMS", colores(f) > 500, "%d colores" % colores(f))
    teclas("m", "ret", "ret")
    si("pedida con el del A4: suena", espera("grep -q sonando= /rocola/datos/xp/rocola_estado.txt", 400, 5))
    time.sleep(60)
    e = estado()
    m = re.search(r"sonando=S\|.*/(\d\d [^/]*)\.(mp3|wav)", e)
    info("con el del A4 sono: %s" % (m.group(1) if m else "?"))
    hz = {"01 Pulso Grave": 196, "02 Canción Ñandú": 660, "01 Tono La": 440, "02 Tono Mi": 330, "01 Tono Alto": 1000}.get(m.group(1) if m else "", 196)
    ok_, det = sono(os.path.join(SON, "a4.wav"), hz, 20)
    si("el WAV tiene la cancion", ok_, det)
    orden("sync", 30); apaga()

FASES = {"instala": instala, "arranque": arranque, "teclas": teclas_y_sonido, "consola": consola, "caida": caida, "apagon": apagon, "ia": ia, "boton": boton, "video": video, "a4": a4}
if __name__ == "__main__":
    pedidas = sys.argv[1:] or ["instala", "arranque", "teclas", "video", "consola", "caida", "apagon", "ia", "boton"]
    if pedidas[0] not in ("instala", "arranque", "a4") and pid(MAQ): c = Control(MAQ)
    elif pedidas[0] not in ("instala", "arranque", "a4"): arranca(os.path.join(SON, "sigue-%s.wav" % time.strftime("%H%M%S")))
    di("##### prueba.py " + " ".join(pedidas))
    try:
        for f in pedidas: FASES[f]()
    finally:
        di("##### RESUMEN: %d BIEN, %d MAL" % (bien, mal))
    sys.exit(1 if mal else 0)
