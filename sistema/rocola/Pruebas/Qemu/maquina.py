#!/usr/bin/env python3
# LA MAQUINA DE LA PRUEBA EN QEMU (grupo qemu, 14-11-2026): arranca QEMU sin KVM (TCG) con la pantalla por software (la VGA de bochs a
# 1024x768, como la pantalla del A4; dentro dibuja Mesa softpipe), el sonido HDA grabado a un WAV, el disco de XP falso (discoxp.sh, con una
# capa qcow2 encima: el disco base no se toca) y, si se da, la memoria USB (arma.sh; lo que escriba no se guarda). Sin red. Se maneja
# desde fuera, por dos enchufes en la carpeta de trabajo:
#   - el monitor de QEMU (monitor.sock): teclas (sendkey, como si se pulsaran en el teclado), capturas de la pantalla (screendump), el
#     boton de encendido (system_powerdown, ACPI) y el apagon (quit: QEMU se cierra en seco, como cortar la luz);
#   - el puerto serie (serie.sock): una linea de ordenes de root (solo en la imagen de prueba: arma.sh) para mirar archivos y procesos sin
#     tocar tty1 ni la consola de mantenimiento.
#   python3 maquina.py arranca DIR XP.qcow2 SONIDO.wav [USB.img]   arranca QEMU suelto (deja DIR/qemu.pid)
#   python3 maquina.py tecla DIR TECLA...                          teclas (nombres de sendkey: ret, esc, m, ctrl-alt-f2...)
#   python3 maquina.py captura DIR ARCHIVO.png                     la pantalla
#   python3 maquina.py orden DIR 'ORDEN'                           una orden en la maquina (por el puerto serie); sale con su codigo
#   python3 maquina.py monitor DIR 'ORDEN'                         una orden del monitor de QEMU
#   python3 maquina.py apagon DIR | boton DIR                      se va la luz | el boton de encendido (espera a que se apague)
import os, re, socket, subprocess, sys, time

def lanza(d, xp_qcow2, wav, usb=None, cpu="SandyBridge", memoria=3072, extra=None):
    """Arranca QEMU (suelto: sigue aunque este programa acabe). Devuelve su pid. EXTRA: otro disco (en bruto, sin guardar lo que se
    escriba), como el segundo de la controladora IDE: lo monta rocola-discos en solo lectura, como cualquier otro disco."""
    os.makedirs(d, exist_ok=True)
    for s in ("monitor.sock", "serie.sock"):
        p = os.path.join(d, s)
        if os.path.exists(p): os.remove(p)
    cmd = ["qemu-system-x86_64", "-machine", "pc", "-cpu", cpu, "-smp", "2", "-m", str(memoria),
           "-device", "VGA,edid=on,xres=1024,yres=768",
           "-audiodev", "wav,id=snd,path=" + wav, "-device", "intel-hda", "-device", "hda-duplex,audiodev=snd",
           "-drive", "file=%s,format=qcow2,if=none,id=xp,discard=unmap,detect-zeroes=unmap" % xp_qcow2,
           "-device", "ide-hd,drive=xp,bus=ide.0,bootindex=1",
           "-nic", "none", "-display", "none",
           "-monitor", "unix:%s,server=on,wait=off" % os.path.join(d, "monitor.sock"),
           "-chardev", "socket,id=ser,path=%s,server=on,wait=off" % os.path.join(d, "serie.sock"), "-serial", "chardev:ser"]
    if extra:
        cmd += ["-drive", "file=%s,format=raw,if=none,id=extra,snapshot=on" % extra, "-device", "ide-hd,drive=extra,bus=ide.0,unit=1"]
    if usb:
        fmt = "qcow2" if usb.endswith(".qcow2") else "raw"   # (arma.sh la deja en bruto, con huecos; en qcow2 comprimido ocupa menos)
        cmd += ["-device", "qemu-xhci,id=xhci", "-drive", "file=%s,format=%s,if=none,id=usb,snapshot=on" % (usb, fmt),
                "-device", "usb-storage,bus=xhci.0,drive=usb,bootindex=0"]
    with open(os.path.join(d, "qemu.txt"), "ab") as reg:
        reg.write(("\n=== %s %s\n" % (time.strftime("%H:%M:%S"), " ".join(cmd))).encode())
        p = subprocess.Popen(cmd, stdout=reg, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL, start_new_session=True)
    with open(os.path.join(d, "qemu.pid"), "w") as f: f.write(str(p.pid))
    for _ in range(100):
        if os.path.exists(os.path.join(d, "monitor.sock")): break
        time.sleep(0.1)
    return p.pid

def pid(d):
    """El pid de QEMU si sigue vivo (si ya acabo y es hijo de este programa, se recoge: un zombi no cuenta como vivo)."""
    try:
        n = int(open(os.path.join(d, "qemu.pid")).read())
        try: os.waitpid(n, os.WNOHANG)
        except ChildProcessError: pass
        if open("/proc/%d/stat" % n).read().rsplit(")", 1)[1].split()[0] == "Z": return None
        os.kill(n, 0); return n
    except (OSError, ValueError, IndexError):
        return None

class Control:
    def __init__(self, d):
        self.d = d; self.mon = None; self.ser = None
        self.n = int(time.time() * 1000) % 1000000

    def _conecta(self, nombre):
        ruta = os.path.join(self.d, nombre)
        for _ in range(100):
            try:
                s = socket.socket(socket.AF_UNIX); s.connect(ruta); return s
            except OSError:
                time.sleep(0.1)
        raise RuntimeError("no conecta " + ruta)

    def _lee(self, s, espera):
        s.settimeout(espera); r = b""
        try:
            while True:
                b = s.recv(65536)
                if not b: break
                r += b
        except socket.timeout:
            pass
        return r.decode("utf-8", "replace")

    def monitor(self, orden, espera=0.4):
        if self.mon is None:
            self.mon = self._conecta("monitor.sock"); self._lee(self.mon, 0.3)
        self.mon.sendall((orden + "\n").encode()); return self._lee(self.mon, espera)

    def tecla(self, k, pausa=0.3):
        self.monitor("sendkey " + k, 0.05); time.sleep(pausa)

    def teclea(self, texto, pausa=0.15):
        nombres = {" ": "spc", "-": "minus", ".": "dot", "/": "slash", "_": "shift-minus", "\n": "ret", "=": "equal", ",": "comma",
                   "|": "shift-backslash", ">": "shift-dot", "<": "shift-comma", "'": "apostrophe", "$": "shift-4", "*": "shift-8"}
        for ch in texto:
            if ch in nombres: k = nombres[ch]
            elif ch.isupper(): k = "shift-" + ch.lower()
            else: k = ch
            self.tecla(k, pausa)

    def captura(self, png):
        ppm = png[:-4] + ".ppm"
        if os.path.exists(ppm): os.remove(ppm)
        self.monitor("screendump " + ppm, 1.0)
        tam = -1
        for _ in range(100):   # (que acabe de escribirla)
            if os.path.exists(ppm):
                t = os.path.getsize(ppm)
                if t > 0 and t == tam: break
                tam = t
            time.sleep(0.2)
        from PIL import Image
        Image.open(ppm).save(png); os.remove(ppm)
        return png

    def cuadro(self):
        """La pantalla como numeros (escala de grises, 256x192): para ver si cambio."""
        import numpy as np
        from PIL import Image
        f = os.path.join(self.d, "cuadro.png"); self.captura(f)
        return np.asarray(Image.open(f).convert("L").resize((256, 192)), dtype=np.int16)

    def espera_cuadros(self, n=2, maximo=240, cada=3):
        """Espera a que la pantalla cambie N veces (N cuadros nuevos de la rocola; en QEMU tarda decenas de segundos cada uno).
        Devuelve cuantos vio."""
        import numpy as np
        antes = self.cuadro(); vistos = 0; fin = time.time() + maximo
        while vistos < n and time.time() < fin:
            time.sleep(cada)
            ahora = self.cuadro()
            if np.abs(ahora - antes).max() > 8: vistos += 1; antes = ahora
        return vistos

    def _lee_ser(self, hasta, maximo):
        if self.ser is None: self.ser = self._conecta("serie.sock")
        self.ser.settimeout(1.0); r = b""; fin = time.time() + maximo
        while time.time() < fin:
            try:
                b = self.ser.recv(65536)
                if not b: raise OSError("serie cerrada")
                r += b
            except socket.timeout:
                pass
            if hasta:   # (el marcador entero, con su codigo y el fin de linea: puede llegar partido)
                i = r.find(hasta.encode())
                if i >= 0 and b"\n" in r[i:]: break
        return r.decode("utf-8", "replace")

    def orden(self, cmd, maximo=60):
        """Corre una orden en la maquina (root, por el puerto serie). Devuelve (codigo, salida); codigo None si no contesto."""
        self.n += 1; n = self.n
        ini, fin = "__INI_%d__" % n, "__FIN_%d__" % n
        # (en una sola linea: el eco de lo tecleado sale entero antes del primer marcador)
        linea = "printf '\\n__I''NI_%d__\\n'; %s; printf '\\n__F''IN_%d__ %%d\\n' $?\n" % (n, cmd, n)
        try:
            self._lee_ser(None, 0.3)   # lo que hubiera quedado
            # (Ctrl+C antes de cada orden: si algo llego a medias -lo tecleado mientras la maquina aun arrancaba, una orden que no acabo-,
            # la linea de ordenes se queda esperando el resto de una comilla y ya no contesta nunca; Ctrl+C la deja limpia)
            self.ser.sendall(b"\x03"); self._lee_ser(None, 0.3)
            self.ser.sendall(linea.encode())
            r = self._lee_ser(fin, maximo).replace("\r", "")
        except OSError:
            self.ser = None; return None, ""
        if fin not in r: return None, r
        i = r.find(ini + "\n")
        cuerpo = r[i + len(ini) + 1:] if i >= 0 else r
        j = cuerpo.find(fin)
        cod = cuerpo[j + len(fin):].strip().split("\n")[0].strip()
        sale = cuerpo[:j]
        if sale.endswith("\n"): sale = sale[:-1]
        try: cod = int(cod)
        except ValueError: cod = -1
        return cod, sale

    def espera(self, cmd, maximo=600, cada=5):
        """Espera a que la orden salga con 0 en la maquina. True/False."""
        fin = time.time() + maximo
        while time.time() < fin:
            if pid(self.d) is None: return False
            c, _ = self.orden(cmd, 30)
            if c == 0: return True
            time.sleep(cada)
        return False

    def apagon(self):
        """Se va la luz: QEMU se cierra en seco (nada corre al cerrar)."""
        n = pid(self.d)
        try: self.monitor("quit", 0.5)
        except (OSError, RuntimeError): pass
        espera_fin(self.d, 30)
        if n and pid(self.d): os.kill(n, 9)

    def boton(self, maximo=300):
        """El boton de encendido (ACPI). True si la maquina se apago sola antes de MAXIMO s."""
        self.monitor("system_powerdown")
        return espera_fin(self.d, maximo)

def espera_fin(d, maximo):
    fin = time.time() + maximo
    while time.time() < fin:
        if pid(d) is None: return True
        time.sleep(1)
    return False

if __name__ == "__main__":
    a = sys.argv[1:]
    if not a: print(__doc__ or "uso: ver la cabecera"); sys.exit(2)
    if a[0] == "arranca":
        print(lanza(a[1], a[2], a[3], a[4] if len(a) > 4 else None)); sys.exit(0)
    c = Control(a[1])
    if a[0] == "tecla":
        for k in a[2:]: c.tecla(k)
    elif a[0] == "captura": print(c.captura(a[2]))
    elif a[0] == "monitor": print(c.monitor(a[2], 1.0))
    elif a[0] == "orden":
        cod, sale = c.orden(a[2], int(a[3]) if len(a) > 3 else 60)
        print(sale); sys.exit(255 if cod is None else cod)
    elif a[0] == "cuadros": print(c.espera_cuadros(int(a[2]) if len(a) > 2 else 2))
    elif a[0] == "apagon": c.apagon()
    elif a[0] == "boton": sys.exit(0 if c.boton() else 1)
    else: print("no se: " + a[0]); sys.exit(2)
