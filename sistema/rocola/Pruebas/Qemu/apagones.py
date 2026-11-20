#!/usr/bin/env python3
# EL CORTE DE LUZ DE VERDAD, EN QEMU (auditoria «apagones», 20-11-2026; sistema/rocola/Xp/FASE11_apagones.md). Sobre el sistema que dejo
# instalado prueba.py (BASE: su xp.qcow2, que no se toca: aqui va otra capa qcow2 encima, la de cada corrida), QEMU se cierra en seco
# («quit» del monitor: lo que la maquina tenia en su memoria y no habia escrito en su disco se pierde, como al cortar la luz) poco despues de
# guardar, SIN sync, y al volver a prender se mira que quedo en /rocola/datos (ext4 dentro de datos.img, en el NTFS por ntfs3).
#   python3 apagones.py PRUEBA...   (Q: la carpeta de trabajo, /root/rocola-trabajo/fase11/apagones/qemu; BASE: el xp.qcow2 de prueba.py)
#   PRUEBA:
#     dd:MODO:ESPERA   el guardado de XP hecho a mano con BusyBox (EscribeSeguroYa: el de paso con fsync -dd conv=fsync- y el cambio de nombre
#                      -mv-), cinco veces seguidas con el numero 1..5; ESPERA s despues del ultimo, el corte. MODO: «normal» (/rocola/datos como
#                      lo monta el initramfs instalado) o «initrd» (con el init del repositorio, que lo monta con dirsync: ver moneda:initrd).
#                      («dirsync», remontar con dirsync, NO sirve: el nucleo no cambia dirsync al remontar; queda solo para verlo).
#     moneda:MODO:ESPERA   rocola-xp de verdad: una moneda (la M), se espera a ver los creditos nuevos en rocola_estado.txt y ESPERA s despues,
#                      el corte; al volver, los creditos que quedaron.
#     moneda:initrd:ESPERA   lo mismo con el ARREGLO DE VERDAD: el init del repositorio (sistema/board/rocola/initramfs/init, con dirsync)
#                      va en un cpio aparte pegado al final del initrd.gz instalado (el nucleo desempaca los cpio seguidos y el ultimo
#                      «init» manda; BusyBox no arma cpio, asi que se arma aqui y entra por un segundo disco en bruto), se vuelve a
#                      prender, se mira que /rocola/datos quedo con dirsync y se hace la moneda y el corte sin remontar nada.
#     mira             arranca y dice como esta montado /rocola/datos (opciones, commit) y si el NTFS quedo sucio.
# Cada una: una linea «RESULTADO ...» (y el resumen en Q/apagones.txt).
import os, re, subprocess, sys, time
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
from maquina import lanza, Control, pid

Q = os.environ.get("Q", "/root/rocola-trabajo/fase11/apagones/qemu")
BASE = os.environ.get("BASE", "/root/rocola-trabajo/act/qemu/xp.qcow2")
MAQ = os.path.join(Q, "maq"); os.makedirs(MAQ, exist_ok=True)
XP = os.path.join(Q, "xp.qcow2")
RES = open(os.path.join(Q, "apagones.txt"), "a")
c = None

def di(s):
    s = time.strftime("%H:%M:%S ") + s
    print(s, flush=True); RES.write(s + "\n"); RES.flush()

def capa_nueva():
    if os.path.exists(XP): os.remove(XP)
    subprocess.run(["qemu-img", "create", "-q", "-f", "qcow2", "-b", BASE, "-F", "qcow2", XP], check=True)

def arranca(extra=None):
    global c
    # (con la maquina cargada -otros trabajos y QEMU sin KVM- el puerto serie a veces no contesta en todo el arranque: entonces se corta y se
    # vuelve a prender, hasta tres veces; un corte antes de que haya contestado no cambia lo que se mide, que se mira despues)
    for vez in range(3):
        lanza(MAQ, XP, os.path.join(Q, "sonido.wav"), extra=extra)
        c = Control(MAQ)
        if c.espera("mountpoint -q /rocola/datos", 420, 5): return
        di("(el puerto serie no contesto en 420 s: se vuelve a prender, vez %d)" % (vez + 2))
        c.apagon()
    raise SystemExit("la maquina no contesto por el puerto serie")

def orden(cmd, maximo=60):
    cod, sale = c.orden(cmd, maximo); return sale if cod is not None else ""

def corte():
    c.apagon()

def para_la_rocola():
    # (que no pinte: con Mesa softpipe en QEMU se come la maquina; el lanzador primero, para que no la vuelva a abrir; SIGUSR1 conserva)
    orden("killall rocola 2>/dev/null; killall -USR1 rocola-xp 2>/dev/null; i=0; while pidof rocola-xp >/dev/null && [ $i -lt 40 ]; do sleep 1; i=$((i+1)); done; killall -KILL rocola-xp 2>/dev/null; true", 90)

def monta(modo):
    # (OJO: «mount -o remount,dirsync» no hace nada -el nucleo no cambia dirsync al remontar: MS_RMT_MASK-; visto aqui el 20-11-2026: el
    # modo «dirsync» de antes medía sin dirsync. Por eso el arreglo se prueba con el init de verdad: modo «initrd»)
    if modo == "dirsync":
        orden("mount -o remount,dirsync /rocola/datos")
    return orden("grep ' /rocola/datos ' /proc/mounts").strip()

def dd(modo, espera):
    capa_nueva()
    if modo == "initrd": pon_init_nuevo()
    else: arranca()
    para_la_rocola()
    m = monta(modo); di("dd %s %s: montado: %s" % (modo, espera, m))
    if modo != "normal" and "dirsync" not in m: di("RESULTADO dd %s: /rocola/datos NO quedo con dirsync" % modo); corte(); return
    f = "/rocola/datos/xp/prueba_apagon.txt"
    orden("rm -f %s %s.tmp; sync" % (f, f))
    t = None
    for n in range(1, 6):
        orden("printf 'v=%d\\n' | dd of=%s.tmp conv=fsync 2>/dev/null && mv %s.tmp %s" % (n, f, f, f))
        t = time.time()
    while time.time() < t + espera: time.sleep(0.05)
    corte()
    arranca()
    v = orden("cat %s 2>/dev/null; ls %s* 2>/dev/null | tr '\\n' ' '" % (f, f)).strip().replace("\n", " ")
    di("RESULTADO dd %s espera=%s: quedo [%s] (se escribio v=5)" % (modo, espera, v))
    corte()

def init_nuevo():
    """El init del repositorio en un cpio newc comprimido, relleno hasta 512 bytes (un disco en bruto); devuelve (ruta, bytes del gz)."""
    repo_init = os.path.abspath(os.path.join(AQUI, "..", "..", "..", "board", "rocola", "initramfs", "init"))
    t = os.path.join(Q, "init-nuevo"); subprocess.run(["rm", "-rf", t], check=True); os.makedirs(t)
    subprocess.run(["install", "-m", "755", repo_init, os.path.join(t, "init")], check=True)
    gz = subprocess.run("cd %s && echo init | cpio -o -H newc -R 0:0 --quiet | gzip -9" % t, shell=True, check=True, capture_output=True).stdout
    raw = os.path.join(Q, "init-nuevo.raw")
    with open(raw, "wb") as f: f.write(gz + b"\0" * ((-len(gz)) % 512 or 512))
    return raw, len(gz)

def pon_init_nuevo():
    """Pega el init nuevo a los initrd.gz instalados (en esta capa: el disco base no se toca) y vuelve a prender. Devuelve el montaje."""
    raw, n = init_nuevo()
    arranca(extra=raw)
    para_la_rocola()
    r = orden("for f in $(find /rocola/disco/rocola-os -maxdepth 2 -name initrd.gz); do head -c %d /dev/sdb >> $f && echo \"$f\"; done; sync" % n, 120)
    di("initrd: el init del repositorio pegado a: " + r.strip().replace("\n", " "))
    orden("sync; sync"); corte()
    arranca()
    return orden("grep ' /rocola/datos ' /proc/mounts").strip()

def moneda(modo, espera):
    capa_nueva()
    if modo == "initrd":
        m = pon_init_nuevo(); di("initrd: tras volver a prender: " + m)
        if "dirsync" not in m: di("RESULTADO moneda initrd: /rocola/datos NO quedo con dirsync"); corte(); return
    else: arranca()
    if not c.espera("for p in $(pidof rocola-xp); do tr '\\0' '\\n' < /proc/$p/cmdline | grep -qx -- --importa-xp || exit 0; done; exit 1", 900, 10):
        di("RESULTADO moneda %s: rocola-xp no se abrio" % modo); corte(); return
    m = monta(modo if modo != "initrd" else "normal")
    time.sleep(60)   # (que acabe de arrancar: la biblioteca y la restauracion del estado)
    cr0 = orden("sed -n 's/^creditos=//p' /rocola/datos/xp/rocola_estado.txt 2>/dev/null | tr -d '\\r'").strip() or "0"
    di("moneda %s %s: montado: %s; creditos antes: %s" % (modo, espera, m, cr0))
    c.tecla("m", 0.1)
    fin = time.time() + 300; cr = cr0; t = None
    while time.time() < fin:
        cr = orden("sed -n 's/^creditos=//p' /rocola/datos/xp/rocola_estado.txt 2>/dev/null | tr -d '\\r'", 20).strip() or "0"
        if cr != cr0: t = time.time(); break
        time.sleep(0.2)
    if t is None: di("RESULTADO moneda %s: la moneda no llego a rocola_estado.txt en 300 s" % modo); corte(); return
    while time.time() < t + espera: time.sleep(0.05)
    corte()
    arranca()
    cr2 = orden("sed -n 's/^creditos=//p' /rocola/datos/xp/rocola_estado.txt 2>/dev/null | tr -d '\\r'").strip()
    di("RESULTADO moneda %s espera=%s: creditos %s -> %s (visto en el archivo) -> tras el corte %s" % (modo, espera, cr0, cr, cr2 or "(sin archivo)"))
    corte()

def mira():
    capa_nueva(); arranca()
    di("mira: " + orden("grep -E ' /rocola/(datos|disco) ' /proc/mounts; cat /proc/fs/ext4/loop1/options 2>/dev/null | grep -E 'commit|barrier|data='; "
                       "for v in /proc/fs/ntfs3/*/volinfo; do echo $v; sed -n 6,7p $v; done").replace("\n", " | "))
    corte()

for p in sys.argv[1:]:
    a = p.split(":")
    if a[0] == "dd": dd(a[1], float(a[2]))
    elif a[0] == "moneda": moneda(a[1], float(a[2]))
    elif a[0] == "mira": mira()
    else: di("no se: " + p)
if c and pid(MAQ): corte()
