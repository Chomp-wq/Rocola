#!/usr/bin/env python3
# LA JUNTA DE SPOTIFY EN EL SISTEMA ENTERO, EN QEMU (15-11-2026; sistema/ACT_qemu_spotify.md). Sigue a prueba.py (usa su maquina, sus
# comprobaciones y su resumen) sobre el sistema que dejo instalado, y pone a prueba lo de Spotify de punta a punta con TODO INVENTADO:
#   - el Soloist FALSO del nucleo (Pruebas/RocolaOs/spotify-xp/SoloistFalso, compilado NativeAOT) servido como «soloist_release_x86_64.tar.gz»
#     por el CDN de mentira del grupo soloist (Pruebas/Soloist/servidor.py), en el anfitrion. NUNCA el Soloist de verdad.
#   - la API web de Spotify FALSA del catalogo (Pruebas/RocolaOs/catalogo/ApiFalsa), en el anfitrion.
#   - llaves inventadas en una memoria USB de mentira (FAT, «rocola_spotify.txt» y «rocola_spotify_api.txt»), enchufada en caliente.
# La maquina llega a los dos servidores por la red de usuario de QEMU AISLADA (maquina.py, red=: solo 10.0.2.100:PUERTO, que va a
# 127.0.0.1:PUERTO del anfitrion) y, dentro, reenvia (reenvia.c, solo en la imagen de prueba) los pone en 127.0.0.1: la rocola, el vigilante
# y curl les hablan en http://127.0.0.1:PUERTO, con las variables de prueba de SPOTIFY_SOLOIST.md y FASE11_catalogo.md
# (/etc/rocola-prueba-entorno, que S99rocola lee solo en la imagen de prueba: arma.sh).
#   python3 spotify.py [FASE...]   sin nada: prepara limpia sinllave red pide noche kill sonido fin
# Fases:
#   prepara   (anfitrion) el .tar.gz del Soloist falso, la memoria de las llaves y los dos servidores (quedan corriendo: Q/spotify/)
#   limpia    la rocola instalada sin nada de Spotify (llaves, programa bajado, variables de prueba) y sin creditos (el boton de encendido)
#   sinllave  la rocola instalada SIN llaves y sin red (lo de fabrica): ROCOLA_OS=1, el vigilante esperando la llave, «spotify estado» y
#             «spotify prueba»; despues deja las variables de prueba y el dibujo por procesador (gpu=0) y se apaga
#   red       con la red aislada: la memoria de las llaves, «spotify llave», el vigilante baja el Soloist falso y lo arranca con rocola-pulso
#   pide      en la rocola, con sus teclas: una de XP, el buscador con lo de Spotify, una de Spotify, un disco de Spotify y otra suya; cobra,
#             suena (lo grabado de la tarjeta: el tono del Soloist falso) y sigue la cola; capturas
#   noche     el modo noche (las 23:00) baja la de Spotify como a las de XP
#   kill      dos de Spotify (una sonando, otra en la cola) y SIGKILL a rocola-xp: al volver, su credito (R5) y nada de Spotify en el estado
#   sonido    lo grabado: el tono del Soloist falso, el orden de la cola y el modo noche
#   fin       los servidores fuera, la bitacora sin secretos, el resumen
import json, os, re, shutil, signal, struct, subprocess, sys, time
AQUI = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, AQUI)
import prueba as P
from maquina import lanza, Control, pid, espera_fin
import numpy as np

REPO = os.path.abspath(os.path.join(AQUI, "../../../.."))
Q = P.Q
S = os.path.join(Q, "spotify")
P.RESUMEN = open(os.path.join(Q, "spotify.txt"), "a")   # (su resumen aparte: Q/spotify.txt)
CAPS_DUENO = os.environ.get("CAPTURAS", "")
os.makedirs(S, exist_ok=True)
# lo inventado (nada de esto es de nadie)
LLAVE = "llave-inventada-de-prueba-qemu-0123456789"
CID, SECRETO, REFRESCO = "id-de-prueba", "secreto-de-prueba", "refresco-1"
SECRETOS = [LLAVE, SECRETO, REFRESCO]
SEG = 75   # (lo que dura cada cancion del Soloist falso: SOLOIST_FALSO_SEG; con 30 s, en QEMU, se acababa antes de mirarla)

def lee(f, d=""):
    try: return open(f).read().strip()
    except OSError: return d

def puertos():
    return int(lee(os.path.join(S, "api.puerto"), "0")), int(lee(os.path.join(S, "cdn.puerto"), "0"))

def vivo(n):
    try: os.kill(n, 0); return True
    except (OSError, TypeError): return False

def arranca(wav, red=True):
    """Como prueba.arranca, con la red aislada hasta los dos servidores de mentira (si RED)."""
    lanza(P.MAQ, P.XP, wav, None, red=list(puertos()) if red else None)
    P.c = Control(P.MAQ)
    if not P.c.espera("true", 600, 5): raise SystemExit("la maquina no contesto por el puerto serie")

# lo que se corre dentro con las variables de prueba (las de /etc/rocola-prueba-entorno)
def orden_e(cmd, maximo=120):
    return P.orden("set -a; . /etc/rocola-prueba-entorno; set +a; " + cmd, maximo)

def captura(nombre):
    f = P.captura(nombre)
    if CAPS_DUENO:
        os.makedirs(CAPS_DUENO, exist_ok=True); shutil.copy(f, os.path.join(CAPS_DUENO, nombre))
    return f

def tonos_rms(wav, hz, desde=0.0):
    """[(segundo, dBFS de la banda de HZ +-3)] de lo grabado (por segundos)."""
    d = np.fromfile(wav, dtype=np.int16, offset=44).astype(np.float64) / 32768.0
    d = d.reshape(-1, 2).mean(axis=1); r = 44100; sale = []
    for i in range(int(desde), len(d) // r):
        x = d[i * r:(i + 1) * r]
        if len(x) < r: break
        f = np.fft.rfft(x * np.hanning(r)); a = np.abs(f)
        b = a[hz - 3:hz + 4].sum(); tot = a.sum() + 1e-12
        if b / tot > 0.5:
            sale.append((i, 20 * np.log10(np.sqrt(np.mean(x * x)) + 1e-12)))
    return sale

# ======================================================================================================================================
def prepara():
    P.di("=== prepara: el Soloist FALSO en un .tar.gz, la memoria USB de las llaves inventadas y los servidores de mentira (anfitrion)")
    sf = os.path.join(Q, "soloist-falso", "soloist-falso")
    P.si("el Soloist falso compilado (NativeAOT, un solo programa)", os.path.isfile(sf), sf)
    cdn = os.path.join(S, "cdn"); os.makedirs(cdn, exist_ok=True)
    t = os.path.join(S, "tar"); shutil.rmtree(t, ignore_errors=True); os.makedirs(t)
    shutil.copy(sf, os.path.join(t, "soloist")); os.chmod(os.path.join(t, "soloist"), 0o755)
    open(os.path.join(t, "CHANGELOG.md"), "w").write("# Soloist FALSO (pruebas de la rocola)\n\n## Release v0.0.1\n- nada de verdad\n")
    tg = os.path.join(cdn, "soloist_release_x86_64.tar.gz")
    subprocess.run(["tar", "-czf", tg, "-C", t, "soloist", "CHANGELOG.md"], check=True)
    ayer = time.time() - 86400; os.utime(tg, (ayer, ayer))   # (compilado ayer: le quedan 89 dias)
    P.info("el .tar.gz del Soloist falso: %d bytes" % os.path.getsize(tg))
    # la memoria USB: MBR con una particion FAT32 (0x0c) en el sector 2048, y los dos archivos en su raiz
    img = os.path.join(S, "llaves.img")
    with open(img, "wb") as f: f.truncate(64 << 20)
    mbr = bytearray(512); n = (64 << 20) // 512 - 2048
    mbr[446:462] = struct.pack("<B3sB3sII", 0, b"\x00\x02\x00", 0x0c, b"\xff\xff\xff", 2048, n)
    mbr[510:512] = b"\x55\xaa"
    with open(img, "r+b") as f: f.write(mbr)
    # (FAT32 con mkfs.vfat y los archivos con mtools, sin montar: el nucleo del anfitrion puede no traer vfat)
    subprocess.run(["mkfs.vfat", "-F", "32", "-n", "LLAVES", "--offset", "2048", img, str(n // 2)], check=True, capture_output=True)
    t2 = os.path.join(S, "llaves-tmp"); shutil.rmtree(t2, ignore_errors=True); os.makedirs(t2)
    # (como las dejaria el Bloc de notas de Windows: con CRLF; la de Soloist con espacios alrededor)
    open(os.path.join(t2, "rocola_spotify.txt"), "w", newline="").write("  %s  \r\n" % LLAVE)
    open(os.path.join(t2, "rocola_spotify_api.txt"), "w", newline="").write("client_id=%s\r\nclient_secret=%s\r\nrefresh_token=%s\r\n" % (CID, SECRETO, REFRESCO))
    for nom in ("rocola_spotify.txt", "rocola_spotify_api.txt"):
        subprocess.run(["mcopy", "-i", "%s@@%d" % (img, 2048 * 512), os.path.join(t2, nom), "::" + nom], check=True)
    shutil.rmtree(t2)
    hay = subprocess.run(["mdir", "-b", "-i", "%s@@%d" % (img, 2048 * 512), "::"], capture_output=True, text=True).stdout
    P.si("la memoria USB de las llaves inventadas (FAT32 en una particion, MBR)", "ROCOLA_SPOTIFY.TXT" in hay.upper() and "ROCOLA_SPOTIFY_API.TXT" in hay.upper(), hay.replace("\n", " ").strip())
    # los servidores (sueltos: siguen entre una fase y otra)
    for nombre in ("api", "cdn"):
        n0 = int(lee(os.path.join(S, nombre + ".pid"), "0"))
        if n0 and vivo(n0): os.kill(n0, signal.SIGTERM)
    pa = 41000 + os.getpid() % 2000
    api = os.path.join(S, "api"); shutil.rmtree(api, ignore_errors=True); os.makedirs(api)
    pr = subprocess.Popen(["dotnet", os.path.join(Q, "api-falsa", "api-falsa.dll"), str(pa), api, CID, SECRETO, REFRESCO],
                          stdout=open(os.path.join(api, "salida.txt"), "w"), stderr=subprocess.STDOUT, start_new_session=True)
    open(os.path.join(S, "api.pid"), "w").write(str(pr.pid)); open(os.path.join(S, "api.puerto"), "w").write(str(pa))
    for p in ("peticiones.log", "cdn.puerto"):
        if os.path.exists(os.path.join(cdn if p.endswith(".log") else S, p)): os.remove(os.path.join(cdn if p.endswith(".log") else S, p))
    pc = subprocess.Popen([sys.executable, "-I", os.path.join(REPO, "sistema/rocola/Pruebas/Soloist/servidor.py"), cdn, os.path.join(S, "cdn.puerto")],
                          stdout=subprocess.DEVNULL, stderr=open(os.path.join(S, "cdn.err"), "w"), start_new_session=True)
    open(os.path.join(S, "cdn.pid"), "w").write(str(pc.pid))
    fin = time.time() + 30
    while time.time() < fin and not (os.path.exists(os.path.join(api, "listo")) and os.path.exists(os.path.join(S, "cdn.puerto"))): time.sleep(0.2)
    pa, pc_ = puertos()
    ok_api = subprocess.run(["curl", "-s", "--noproxy", "*", "-o", "/dev/null", "-w", "%{http_code}", "-u", CID + ":" + SECRETO, "-d", "grant_type=client_credentials",
                             "http://127.0.0.1:%d/api/token" % pa], capture_output=True, text=True).stdout
    ok_cdn = subprocess.run(["curl", "-s", "--noproxy", "*", "-o", "/dev/null", "-w", "%{http_code}", "-I", "http://127.0.0.1:%d/soloist_release_x86_64.tar.gz" % pc_], capture_output=True, text=True).stdout
    P.si("la API web falsa contesta (una llave de la app: 200) y el CDN falso tiene el .tar.gz (200)", ok_api == "200" and ok_cdn == "200", "API %d: %s, CDN %d: %s" % (pa, ok_api, pc_, ok_cdn))
    open(os.path.join(api, "peticiones.txt"), "w").close()   # (lo de esta comprobacion no cuenta)
    open(os.path.join(cdn, "peticiones.log"), "w").close()

def entorno():
    pa, pc = puertos()
    return "\n".join([
        "# PRUEBA EN QEMU (sistema/rocola/Pruebas/Qemu/spotify.py): las variables de prueba de SPOTIFY_SOLOIST.md y FASE11_catalogo.md.",
        "# Todo va a servidores de MENTIRA del anfitrion (reenvia los pone en 127.0.0.1). No va en Rocola OS.",
        "SOLOIST_URL=http://127.0.0.1:%d/soloist_release_x86_64.tar.gz" % pc,
        "ROCOLA_SPOTIFY_APRESOLVE=http://127.0.0.1:%d/apresolve" % pc,
        "ROCOLA_SPOTIFY_API=http://127.0.0.1:%d/v1" % pa,
        "ROCOLA_SPOTIFY_CUENTAS=http://127.0.0.1:%d" % pa,
        "SOLOIST_FALSO_PULSO=1",
        "SOLOIST_FALSO_SEG=%d" % SEG,
        "SOLOIST_FALSO_LOG=/run/soloist-falso.log",
        "# el reenviador, desde S99rocola (que lee este archivo con «.» al arrancar, antes de abrir la rocola): asi el vigilante y la rocola",
        "# ya lo tienen al empezar (lanzado despues por el puerto serie, con la maquina aun arrancando, a veces no llegaba a correr)",
        "pidof reenvia >/dev/null || setsid /usr/lib/rocola-prueba/reenvia 10.0.2.100 %d %d </dev/null >/run/reenvia.txt 2>&1 &" % (pa, pc), ""])   # (al acabar una, el falso pasa solo a otra de 990 Hz, como Spotify: la rocola la tiene que pausar)

def vigilante_pid():
    """El vigilante en marcha: el pid que el dice en su estado (vigilante=), si ese proceso es «rocola-soloist corre»."""
    v = ""
    for _ in range(3):   # (mientras la maquina arranca, el puerto serie a veces devuelve otra cosa)
        m = re.search(r"^vigilante=(\d+)\s*$", P.orden("cat /run/rocola-soloist/estado 2>/dev/null"), re.M)
        if m: v = m.group(1); break
        time.sleep(5)
    if not v: return None
    cl = P.orden("tr '\\0' ' ' 2>/dev/null < /proc/%s/cmdline" % v)
    return v if "rocola-soloist corre" in cl else None

def limpia():
    P.di("=== limpia: la rocola instalada vuelve a como llega al bar en lo de Spotify (sin llaves, sin el programa de Spotify bajado, sin las variables de prueba) y sin creditos")
    if not pid(P.MAQ): arranca(os.path.join(P.SON, "limpia.wav"), red=False)
    P.espera_xp(600)
    P.orden("rm -rf /rocola/datos/xp/rocola_spotify_llave.txt /rocola/datos/xp/rocola_spotify_api.txt /rocola/datos/xp/soloist /rocola/disco/rocola-os/soloist "
            "/etc/rocola-prueba-entorno /rocola/datos/xp/rocola_os.txt /rocola/datos/xp/rocola_os_apagado; sync", 60)
    P.si("sin llaves, sin el programa de Spotify y sin rocola_os.txt", P.ok("[ ! -e /rocola/datos/xp/rocola_spotify_llave.txt ] && [ ! -e /rocola/datos/xp/rocola_spotify_api.txt ] && [ ! -e /rocola/disco/rocola-os/soloist ] && [ ! -e /rocola/datos/xp/rocola_os.txt ]"))
    # (el boton de encendido: rocola-xp se cierra como Windows al apagarse y borra el estado del dia: sin creditos)
    P.si("apagada con el boton (el estado del dia, borrado)", P.c.boton(300))

def sinllave():
    P.di("=== sinllave: Spotify ENCENDIDO de fabrica y SIN llaves (como llega la rocola al bar): ROCOLA_OS, el vigilante espera la llave, la rocola es la de XP")
    if not pid(P.MAQ): arranca(os.path.join(P.SON, "sinllave.wav"), red=False)
    p = P.espera_xp(600)
    env = P.orden("tr '\\0' '\\n' < /proc/%s/environ | grep -e ^ROCOLA_OS= -e ^ROCOLA_DATOS=" % p)
    P.si("el lanzador abre rocola-xp con ROCOLA_OS=1 (sin rocola_os.txt ni rocola_os_apagado)", "ROCOLA_OS=1" in env and P.ok("[ ! -f /rocola/datos/xp/rocola_os_apagado ]"),
         env.replace("\n", " ") + "; rocola_os.txt: " + (P.orden("cat /rocola/datos/xp/rocola_os.txt 2>/dev/null || echo no esta").strip() or "vacio"))
    P.espera("grep -q '^fase=sin_llave' /run/rocola-soloist/estado", 120, 5)
    est = P.orden("cat /run/rocola-soloist/estado")
    P.info("estado del vigilante: " + est.replace("\n", " "))
    v = vigilante_pid()
    pp = P.orden("awk '{print $4}' /proc/%s/stat" % v).strip() if v else ""
    P.si("rocola-soloist corre, hijo de rocola-xp, en la fase sin_llave", "fase=sin_llave" in est and bool(v) and pp == str(p), "vigilante %s, su padre %s, rocola-xp %s" % (v, pp, p))
    P.si("sin llave no se baja ni se arranca nada de Soloist", P.ok("[ ! -e /run/rocola-soloist/bin/soloist ] && [ ! -e /rocola/disco/rocola-os/soloist/soloist.tar.gz ] && ! pidof soloist"))
    xe = P.orden("cat /run/rocola-xp/spotify/estado 2>/dev/null")
    P.info("estado de Spotify en rocola-xp: " + xe.replace("\n", " "))
    P.si("rocola-xp: sin sesion, nada cobrado, el vigilante sin_llave", "vigilante=sin_llave" in xe and re.search(r"^cobradas=0$", xe, re.M) is not None, xe.replace("\n", " ")[:200])
    cat = P.orden("cat /run/rocola-xp/spotify/catalogo 2>/dev/null")
    P.si("el catalogo sin la llave de la API: api=sin_llave y 0 peticiones", "api=sin_llave" in cat and re.search(r"^peticiones=0$", cat, re.M) is not None, cat.replace("\n", " ")[:160])
    # las ordenes del dueno, tal cual (se guardan para el documento)
    e = P.orden("spotify estado 2>&1", 120); open(os.path.join(S, "spotify-estado-sin-llave.txt"), "w").write(e + "\n")
    P.si("«spotify estado»: modo encendido de fabrica y que falta la llave de Soloist", "Modo Spotify: encendido" in e and re.search(r"llave.*(falta|no esta)", e, re.I) is not None, " | ".join(l.strip() for l in e.splitlines()[:6])[:300])
    cod, pr = P.c.orden("spotify prueba 2>&1", 180); open(os.path.join(S, "spotify-prueba-sin-llave.txt"), "w").write(pr + "\n")
    P.si("«spotify prueba» dice MAL: falta la llave de Soloist y la de la API web (sale con error)", cod not in (0, None) and "falta la llave de Soloist" in pr and "falta la llave de la API web" in pr,
         (re.search(r"=== \d+ BIEN, \d+ MAL ===", pr) or [""])[0])
    P.si("el puente de sonido (rocola-pulso) esta en el sistema: 48 funciones", P.ok("[ -s /usr/lib/rocola-pulso/libpulse.so.0 ]") and "BIEN: el puente de sonido" in pr)
    P.c.espera_cuadros(1, 300); f = captura("20-sin-llave.png")
    P.info("captura sin llave (la rocola de XP tal cual, sin la letra de Spotify): " + os.path.basename(f))
    bit = P.orden("cat /rocola/datos/xp/rocola_spotify.txt 2>/dev/null | tail -5")
    P.info("bitacora del nucleo: " + bit.replace("\n", " | ")[:300])
    # para lo que sigue: las variables de prueba y el dibujo por procesador (en QEMU, unos 5 s por cuadro; la tarjeta, 20 a 30)
    # (de una linea en una: por el puerto serie cada orden va en una sola linea)
    P.orden(": > /etc/rocola-prueba-entorno")
    for l in entorno().splitlines(): P.orden("printf '%%s\\n' '%s' >> /etc/rocola-prueba-entorno" % l)
    P.si("las variables de prueba en /etc/rocola-prueba-entorno (solo la imagen de prueba)", "SOLOIST_URL=http://127.0.0.1" in P.orden("cat /etc/rocola-prueba-entorno"))
    P.tarjeta(False)
    P.orden("sync", 30); P.apaga()

def red():
    P.di("=== red: con la red aislada hasta los servidores de mentira; la memoria USB con las llaves inventadas; el vigilante baja el Soloist FALSO")
    pa, pc = puertos()
    # (S99rocola lee /etc/rocola-prueba-entorno al arrancar: se arranca de nuevo, ahora con la red)
    if pid(P.MAQ): P.orden("sync", 30); P.apaga()
    arranca(os.path.join(P.SON, "spotify.wav"), red=True)
    P.si("dentro de la maquina, eth0 con direccion (la red de usuario de QEMU)", P.espera("ip -4 addr show eth0 | grep -q 'inet 10.0.2.'", 120, 5), P.orden("ip -4 addr show eth0 | grep inet").strip())
    P.si("pero no a internet (red aislada)", not P.ok("curl -s -m 8 -o /dev/null http://1.1.1.1/", 30))
    p = P.espera_xp(600)
    # el reenviador (solo la prueba) ya lo abrio S99rocola (/etc/rocola-prueba-entorno); si no, aqui
    escucha = "netstat -ltn | grep -q 127.0.0.1:%d && netstat -ltn | grep -q 127.0.0.1:%d" % (pa, pc)
    P.si("el reenviador de la prueba ya escucha al abrirse la rocola (lo abre S99rocola)", P.ok(escucha, 60))
    for _ in range(3):
        if P.ok(escucha, 60): break
        P.orden("setsid /usr/lib/rocola-prueba/reenvia 10.0.2.100 %d %d </dev/null >/run/reenvia.txt 2>&1 & sleep 3" % (pa, pc), 60)
    P.si("y llega a los servidores de mentira en 127.0.0.1 (apresolve del CDN falso)", P.espera("curl -s -m 10 http://127.0.0.1:%d/apresolve | grep -q accesspoint" % pc, 120, 5))
    env = P.orden("tr '\\0' '\\n' < /proc/%s/environ | grep -e ^SOLOIST_URL -e ^ROCOLA_SPOTIFY_API= -e ^ROCOLA_OS=" % p)
    P.si("rocola-xp con ROCOLA_OS=1 y las variables de prueba (a donde se baja Soloist, la API falsa)", "ROCOLA_OS=1" in env and "SOLOIST_URL=http://127.0.0.1" in env, env.replace("\n", " "))
    P.espera("grep -q '^fase=sin_llave' /run/rocola-soloist/estado", 180, 5)
    P.si("otra vez sin llave: espera", P.ok("grep -q '^fase=sin_llave' /run/rocola-soloist/estado"))
    antes_cdn = len([l for l in lee(os.path.join(S, "cdn", "peticiones.log")).splitlines() if ".tar.gz" in l])
    P.si("sin llave no se pidio nada al CDN falso (sin la llave no se baja Soloist)", antes_cdn == 0, "%d peticiones del .tar.gz" % antes_cdn)
    # LA MEMORIA USB
    r = P.c.enchufa(os.path.join(S, "llaves.img"))
    P.info("enchufada la memoria de las llaves: " + r.strip().replace("\r\n", " ")[:120])
    P.espera("grep -q 1 /sys/block/sd*/removable 2>/dev/null && ls /sys/block/sd*/sd*1 >/dev/null 2>&1", 60, 3)
    P.info("discos: " + P.orden("for d in /sys/block/sd*; do echo $(basename $d) rem=$(cat $d/removable) $(ls -d $d/sd*[0-9] 2>/dev/null | xargs -n1 basename); done").replace("\n", " | "))
    t0 = time.time()
    cod, sale = P.c.orden("set -a; . /etc/rocola-prueba-entorno; set +a; spotify llave </dev/null 2>&1", 120)
    open(os.path.join(S, "spotify-llave.txt"), "w").write(sale + "\n")
    P.info("«spotify llave»: " + sale.replace("\n", " | ")[:300])
    P.si("«spotify llave» (o el vigilante, que mira la USB cada 30 s) pone las dos llaves",
         P.espera("[ -s /rocola/datos/xp/rocola_spotify_llave.txt ] && [ -s /rocola/datos/xp/rocola_spotify_api.txt ]", 90, 3), "%.0f s" % (time.time() - t0))
    ls = P.orden("ls -l /rocola/datos/xp/rocola_spotify_llave.txt /rocola/datos/xp/rocola_spotify_api.txt")
    P.si("las dos con permisos 0600 (-rw-------)", ls.count("-rw-------") == 2, ls.replace("\n", " | "))
    P.si("la de Soloist, limpia (sin los espacios ni el CRLF del Bloc de notas)", P.orden("cat /rocola/datos/xp/rocola_spotify_llave.txt").strip() == LLAVE)
    api = P.orden("cat /rocola/datos/xp/rocola_spotify_api.txt")
    P.si("la de la API: client_id, client_secret y refresh_token", api.splitlines() == ["client_id=" + CID, "client_secret=" + SECRETO, "refresh_token=" + REFRESCO])
    P.si("la memoria USB se desmonto (solo se leyo)", P.ok("! grep -q /run/rocola-soloist/usb /proc/mounts"))
    # EL VIGILANTE: baja el programa del CDN falso, lo saca a /run y lo arranca con rocola-pulso
    t0 = time.time()
    ok = P.espera("grep -q '^fase=corriendo' /run/rocola-soloist/estado", 400, 5)
    est = P.orden("cat /run/rocola-soloist/estado")
    P.si("el vigilante baja el Soloist falso y lo arranca: fase=corriendo", ok, "%.0f s; %s" % (time.time() - t0, est.replace("\n", " ")[:220]))
    pet = lee(os.path.join(S, "cdn", "peticiones.log"))
    P.si("lo bajo del CDN falso (GET del .tar.gz)", "GET /soloist_release_x86_64.tar.gz" in pet, pet.replace("\n", " | ")[:200])
    P.si("lo guardo en el disco de la rocola y lo saco a /run (en memoria)", P.ok("[ -s /rocola/disco/rocola-os/soloist/soloist.tar.gz ] && [ -x /run/rocola-soloist/bin/soloist ]"))
    sp = P.orden("pidof soloist").split()
    P.si("un solo Soloist corriendo", len(sp) == 1, " ".join(sp))
    if sp:
        se = P.orden("tr '\\0' '\\n' < /proc/%s/environ | grep -e ^LD_LIBRARY_PATH= -e ^HOME=" % sp[0])
        mp = P.orden("grep -c /usr/lib/rocola-pulso/libpulse.so.0 /proc/%s/maps" % sp[0]).strip()
        P.si("Soloist con LD_LIBRARY_PATH=/usr/lib/rocola-pulso y NUESTRO libpulse.so.0 cargado (en sus mapas)", "LD_LIBRARY_PATH=/usr/lib/rocola-pulso" in se and mp not in ("", "0"), se.replace("\n", " ") + "; mapas: " + mp)
        cl = P.orden("tr '\\0' ' ' < /proc/%s/cmdline" % sp[0])
        P.info("la orden de Soloist (la llave va en -k, como la pide el de verdad; solo root la lee): " + cl.replace(LLAVE, "<LLAVE>")[:200])
    ok = P.espera("grep -q '^sesion=1' /run/rocola-xp/spotify/estado && grep -q '^ws=' /run/rocola-xp/spotify/estado", 180, 5)
    xe = P.orden("cat /run/rocola-xp/spotify/estado")
    P.si("rocola-xp habla con Soloist por su WebSocket: sesion y bocina", ok and re.search(r"^ws=(1|si|conectado)", xe, re.M) is not None, xe.replace("\n", " ")[:240])
    cod, pr = P.c.orden("set -a; . /etc/rocola-prueba-entorno; set +a; spotify prueba 2>&1", 180)
    open(os.path.join(S, "spotify-prueba-con-llaves.txt"), "w").write(pr + "\n")
    P.si("«spotify prueba» con las llaves: todo BIEN", cod == 0, (re.search(r"=== \d+ BIEN, \d+ MAL ===", pr) or [""])[0] + " | " + " | ".join(l.strip() for l in pr.splitlines() if "MAL:" in l)[:300])
    e = P.orden("set -a; . /etc/rocola-prueba-entorno; set +a; spotify estado 2>&1", 120); open(os.path.join(S, "spotify-estado-con-llaves.txt"), "w").write(e + "\n")
    P.info("«spotify estado»: " + " | ".join(l.strip() for l in e.splitlines()[:14])[:500])
    P.c.desenchufa()

# ---------- las teclas de la rocola (mundo.sh: arriba 38, abajo 40, izq 37, der 39, poner 13, regresar 27, moneda 77 = M) ----------
def escribe(texto):
    for ch in texto: P.c.tecla(ch, 0.4)

def nucleo():
    """La bitacora del nucleo (rocola_spotify.txt) desde que empezo esta corrida (la marca de «pide»: lo de corridas de antes no cuenta)."""
    t = P.orden("cat /rocola/datos/xp/rocola_spotify.txt 2>/dev/null")
    m = lee(os.path.join(S, "marca.txt"))
    return t[t.find(m) + len(m):] if m and m in t else t
def cobradas(): return re.findall(r"COBRADA (spotify:track:\S+)", nucleo())
def marca():
    """El ultimo renglon de la bitacora del nucleo ahora: lo de despues es de esta corrida."""
    u = P.orden("tail -n 1 /rocola/datos/xp/rocola_spotify.txt 2>/dev/null").strip()
    open(os.path.join(S, "marca.txt"), "w").write(u)

def flecha(k, n=1):
    """Las flechas, de una en una y un cuadro entre una y otra: XP descarta las que llegan a menos de 40 ms de la anterior (el freno de
    la auto-repeticion, Form1.cs) y en QEMU las que llegan juntas se atienden a la vez, entre cuadro y cuadro."""
    for _ in range(n):
        P.c.tecla(k, 0.5); P.c.espera_cuadros(1, 90)

def estado_linea(clave):
    m = re.search(r"^%s=(.*)$" % clave, P.estado(), re.M); return m.group(1) if m else ""

def espera_nucleo(patron, maximo):
    fin = time.time() + maximo
    while time.time() < fin:
        if re.search(patron, nucleo()): return True
        time.sleep(4)
    return False

def pide():
    P.di("=== pide: en la rocola, con sus teclas (por evdev): una de XP, el buscador con lo de Spotify, un disco de Spotify y sus canciones, otra de XP")
    # (de una maquina recien arrancada, con la red y las llaves que dejo «red»: la pared en su letra de siempre -la B: como prueba.py- y no
    # en la que dejo otra corrida; lo que quede pagado de antes reanuda o vuelve como credito, como tras un apagon)
    if pid(P.MAQ): P.orden("sync", 30); P.apaga()
    arranca(os.path.join(P.SON, "spotify-pide.wav"), red=True)
    P.espera_xp(600)
    P.espera("grep -q '^sesion=1' /run/rocola-xp/spotify/estado", 400, 5)
    # (si reanudo un video de antes, ocupa la pantalla: se espera a que acabe)
    P.espera("! grep -q '^sonando=V|' /rocola/datos/xp/rocola_estado.txt", 300, 5)
    P.c.espera_cuadros(2, 300)
    # (lo grabado se mira desde aqui: si la maquina ya venia sonando de otra fase, eso no cuenta)
    w = P.wav_actual()
    desde = max(0.0, (os.path.getsize(w) - 44) / (44100 * 4)) if os.path.exists(w) else 0.0
    open(os.path.join(S, "wav.txt"), "w").write("%s\n%.1f\n" % (w, desde))
    marca()
    P.orden("pkill -f /run/noche.sh; date -s \"$(date +%Y-%m-%d) 15:00:00\" >/dev/null; :")   # (el guion de una corrida de antes, si quedo esperando; y de dia, sin pasar por las 7)
    # EL MODO NOCHE (lo mira despues «sonido»): en cuanto la primera de Spotify lleve 10 s sonando, el reloj de la maquina pasa a las
    # 23:00:05 (lo hace la maquina sola, en segundo plano: aqui las teclas tardan minutos); la rampa de 20 s la baja a media cancion y la
    # segunda de Spotify ya suena entera de noche
    # (un guion en /run lanzado con setsid: no depende de la linea de ordenes del puerto serie)
    P.orden("rm -f /run/noche.antes /run/noche.despues; printf '%s\\n' 'while ! grep -q \"sonando=S|spotify:track:\" /rocola/datos/xp/rocola_estado.txt; do sleep 1; done' "
            "'sleep 10; date +%s > /run/noche.antes; date -s \"$(date +%Y-%m-%d) 23:00:05\" > /run/noche.despues' > /run/noche.sh; setsid sh /run/noche.sh </dev/null >/dev/null 2>&1 & :")   # (con «& :»: orden() pone «; printf» detras)
    P.si("el guion que pasa el reloj a las 23:00 esta esperando", P.ok("grep -q 23:00:05 /run/noche.sh && ps | grep -v grep | grep -q 'sh /run/noche.sh'"), P.orden("cat /run/noche.sh").replace("\n", " | "))
    c0 = P.creditos() or 0
    # 1) la moneda (4 creditos) y la primera del disco de la letra B: «01 Pulso Grave» (196 Hz), como en prueba.py
    P.teclas("m", "ret", "ret", pausa=1.5)
    ok = P.espera("grep -q 'Pulso Grave' /rocola/datos/xp/rocola_estado.txt", 300, 5)
    c1 = P.creditos() or 0
    P.si("una de XP pedida («01 Pulso Grave»): la moneda da 4 y cobra 1", ok and c1 == c0 + 3, "creditos %s -> %s" % (c0, c1))
    # 2) el buscador (REGRESAR+ARRIBA) y «entira»: en la API falsa, la cancion «Tema Tema de Mentira» (can0000) y el disco «Tema de
    # Mentira» (dis000); en la musica de XP, nada
    P.c.espera_cuadros(1, 120)
    P.c.tecla("esc-up", 1.0); P.c.espera_cuadros(1, 120)
    escribe("entira")   # (sin «m» ni «s»: en rocola_teclas.txt de esta prueba la M es la moneda y la S saltar, como botones de la rocola)
    ok = P.espera("grep -q '^busquedas=[1-9]' /run/rocola-xp/spotify/catalogo", 120, 3)
    P.info("creditos con el buscador abierto: %s (antes %s)" % (P.creditos(), c1))
    P.c.espera_cuadros(2, 120)
    captura("21-buscador-spotify.png")
    pet = lee(os.path.join(S, "api", "peticiones.txt"))
    P.si("el buscador pregunta a la API falsa: «entira»", ok and "q=entira" in pet, (pet.splitlines() or [""])[-1])
    cat = P.orden("cat /run/rocola-xp/spotify/catalogo")
    P.si("el catalogo con la llave de la API que llego por la USB (sin reiniciar la rocola): api=bien, llave=si", "api=bien" in cat and "llave=si" in cat, cat.replace("\n", " ")[:200])
    # 3) a las tarjetas: DERECHA hasta salir del teclado y a la ultima (el disco); PONER lo abre en la vista de disco de XP
    flecha("right", 14)
    captura("21b-buscador-elegido.png")
    P.c.tecla("ret", 1.0)
    fin = time.time() + 120
    while time.time() < fin and "/v1/albums/dis000" not in lee(os.path.join(S, "api", "peticiones.txt")): time.sleep(3)
    pet = lee(os.path.join(S, "api", "peticiones.txt"))
    P.si("PONER en la tarjeta del disco lo abre: la API falsa da el disco (GET /v1/albums/dis000)", "/v1/albums/dis000" in pet, (pet.splitlines() or [""])[-1])
    P.c.espera_cuadros(2, 120)
    captura("22-disco-spotify.png")
    c1b = P.creditos() or 0
    P.info("creditos con el disco de Spotify abierto: %s" % c1b)
    # 4) PONER pide su primera (can0000); ABAJO y PONER, la segunda (can0001): por I6, un credito cada una y su turno
    # (en QEMU un PONER a veces se pierde -llega mientras la vista del disco aun se abre-: si en 90 s no se cobro, otro PONER)
    def pon(uri):
        for _ in range(2):
            P.c.tecla("ret", 1.0)
            if espera_nucleo(r"COBRADA " + uri, 90): return True
            P.info("el PONER de %s no llego: otro" % uri)
        return False
    ok1 = pon("spotify:track:can0000")
    flecha("down")
    ok2 = pon("spotify:track:can0001")
    c2 = P.creditos() or 0
    P.si("las dos de Spotify cobradas (COBRADA en rocola_spotify.txt) y un credito cada una", ok1 and ok2 and c2 == c1b - 2, "creditos %s -> %s; %s" % (c1b, c2, " | ".join(cobradas())))
    est = P.estado()
    P.si("en rocola_estado.txt, en la cola, con su ruta spotify: (lo pagado: vuelve tras un apagon)", "spotify:track:can0000" in est and "spotify:track:can0001" in est,
         " ".join(l for l in est.splitlines() if l.startswith(("sonando=", "cola=")))[:300])
    # 5) REGRESAR: la pared en la letra de Spotify (♫), con ese disco
    P.c.tecla("esc", 1.0); P.c.espera_cuadros(2, 120)
    captura("23-pared-letra-spotify.png")
    # 6) otra de XP: dos veces REGRESAR+IZQUIERDA (la de antes de ♫ es VIDEO; la de antes, una de canciones: un video ocupa la pantalla
    # entera y las teclas solo la despiertan), PONER y PONER (la primera del disco elegido)
    P.c.tecla("esc-left", 1.0); P.c.espera_cuadros(2, 120)
    P.c.tecla("esc-left", 1.0); P.c.espera_cuadros(2, 120)
    captura("23b-pared-otra-letra.png")
    antes = P.estado()
    P.teclas("ret", pausa=1.5); P.c.espera_cuadros(1, 120); P.teclas("ret", pausa=1.5)
    fin = time.time() + 180
    while time.time() < fin and P.estado().count("cola=") <= antes.count("cola="): time.sleep(4)
    est = P.estado(); c3 = P.creditos() or 0
    x2 = [l for l in est.splitlines() if l.startswith("cola=") and "spotify:" not in l]
    P.si("otra de XP en la cola, despues de las dos de Spotify (cobra 1)", bool(x2) and x2[-1].startswith("cola=S|") and c3 == c2 - 1, "creditos %s -> %s; %s" % (c2, c3, (x2 or [""])[-1][:120]))
    open(os.path.join(S, "x2.txt"), "w").write((x2 or [""])[-1])

def noche():
    P.di("=== noche: el modo noche (de fabrica, 23 a 7, al 80 %: -3,2 dB) tambien para la de Spotify (el nivel se mira en «sonido»)")
    ok = P.espera("[ -s /run/noche.despues ]", 400, 5)
    P.si("el reloj de la maquina paso a las 23:00 con la primera de Spotify sonando", ok, P.orden("cat /run/noche.despues; date").replace("\n", " ").strip())
    P.info("bitacora de la rocola (lo de la noche): " + P.orden("tr -d '\\r' < /rocola/datos/xp/rocola_error.txt | grep -i noche | tail -3").replace("\n", " | ")[:300])

def kill():
    P.di("=== kill: dos de Spotify desde la letra ♫ (una suena, otra en la cola) y SIGKILL a rocola-xp: al volver, lo pagado de Spotify vuelve como credito (R5)")
    # esperar a la de XP de la cola (la que va despues de las dos de Spotify) y pedir dos de Spotify desde la letra ♫
    t0 = time.time()
    x2 = lee(os.path.join(S, "x2.txt"))
    ruta = x2.split("|", 1)[1] if "|" in x2 else "@@"
    # (sono o esta sonando: ya no esta en la cola ni suena nada de Spotify; la cancion de prueba dura 3 s y entre vistazo y vistazo se
    # puede acabar: lo que sono de verdad lo dice «sonido», en lo grabado)
    def ya_x2():
        e = P.estado()
        return any(l.startswith("sonando=") and l.endswith("|" + ruta) for l in e.splitlines()) or \
            (not any(l.startswith("cola=") and l.endswith("|" + ruta) for l in e.splitlines()) and "sonando=S|spotify:" not in e)
    while time.time() - t0 < 900 and not ya_x2(): time.sleep(3)
    P.si("despues de las dos de Spotify, la de XP de la cola (sonando o ya sonada)", ya_x2(), ruta[-60:])
    P.c.espera_cuadros(1, 60)
    c0 = P.creditos() or 0
    P.teclas("m", pausa=1.5); P.c.espera_cuadros(1, 120)
    P.c.tecla("esc-right", 1.0); P.c.espera_cuadros(2, 120)   # (de la letra de la de XP a VIDEO y de VIDEO a ♫)
    P.c.tecla("esc-right", 1.0); P.c.espera_cuadros(2, 120)
    captura("25-pared-letra-spotify-otra-vez.png")
    n0 = len(cobradas())
    P.teclas("ret", pausa=1.5); P.c.espera_cuadros(2, 120)
    captura("25b-disco-spotify.png")
    def pon_n(n):   # (PONER hasta que haya N cobradas; en QEMU uno a veces se pierde: otro a los 90 s)
        for _ in range(2):
            P.teclas("ret", pausa=1.5)
            fin = time.time() + 90
            while time.time() < fin and len(cobradas()) < n: time.sleep(4)
            if len(cobradas()) >= n: return
            P.info("el PONER no llego: otro")
    pon_n(n0 + 1)
    flecha("down")
    pon_n(n0 + 2)
    nuevas = cobradas()[n0:]
    c1 = P.creditos() or 0
    P.si("dos de Spotify pedidas desde la letra ♫ (cobradas, un credito cada una, despues de una moneda)", len(nuevas) == 2 and c1 == c0 + 4 - 2, "creditos %s -> %s; %s" % (c0, c1, " | ".join(nuevas)))
    s3 = (nuevas or [""])[0]
    # (sonar: la bitacora del nucleo dice «suena URI» al empezar; el estado de XP, «sonando=S|URI» mientras suena)
    t0 = time.time()
    while time.time() - t0 < 400 and ("sonando=S|" + s3) not in P.estado(): time.sleep(3)
    P.si("y suena la primera de las dos (sonando= en el estado de XP; «suena» en la bitacora)", ("sonando=S|" + s3) in P.estado() and ("suena " + s3) in nucleo(), s3)
    P.c.espera_cuadros(1, 60)
    captura("24-suena-spotify.png")
    p = P.xp_pid()
    vig = vigilante_pid(); sol = P.orden("pidof soloist").strip()
    # (lo que hay en el disco justo antes y el SIGKILL en la misma orden)
    antes = P.orden("cp /rocola/datos/xp/rocola_estado.txt /run/estado-antes-del-kill.txt; kill -9 %s; cat /run/estado-antes-del-kill.txt" % p)
    m = re.search(r"creditos=(\d+)", antes); c_antes = int(m.group(1)) if m else None
    sp_antes = [l.strip() for l in antes.splitlines() if l.startswith(("sonando=", "cola=")) and "spotify:" in l]
    P.info("en el disco al SIGKILL: creditos=%s; de Spotify: %s" % (c_antes, " ".join(sp_antes)))
    P.si("al SIGKILL, en el estado: la de Spotify que suena y la de la cola", len(sp_antes) == 2 and any(l.startswith("sonando=S|spotify:") for l in sp_antes), " ".join(sp_antes))
    p2 = None; fin = time.time() + 300
    while time.time() < fin:
        q = P.xp_pid()
        if q and q != p: p2 = q; break
        time.sleep(3)
    P.si("el lanzador vuelve a abrir rocola-xp", bool(p2), "%s -> %s" % (p, p2))
    P.si("apuntado: «rocola-xp se cayo (codigo 137)»", P.espera("grep -q 'rocola-xp se cayo (codigo 137)' /rocola/datos/rocola_actualizaciones.txt", 120, 5))
    # (RestoreState de XP devuelve el credito EN MEMORIA y no vuelve a escribir rocola_estado.txt hasta el siguiente cambio -como XP-: el
    # archivo sigue diciendo lo de antes del SIGKILL. Se espera a la biblioteca armada -la pantalla de la atraccion o la pared-, se
    # despierta con una flecha y una moneda (4) lo hace escribir: tiene que quedar lo de antes + lo de Spotify devuelto + 4.)
    P.espera("grep -q 'sesion=1' /run/rocola-xp/spotify/estado", 300, 5)
    P.c.espera_cuadros(2, 300)
    P.c.tecla("right", 1.0); P.c.espera_cuadros(1, 120)
    P.teclas("m", pausa=1.5)
    fin = time.time() + 240
    while time.time() < fin and "ts=" in P.estado() and (P.creditos() or 0) == (c_antes or 0): time.sleep(5)
    time.sleep(5)
    c_desp = P.creditos(); est = P.estado()
    P.si("al volver, lo pagado de Spotify vuelve como credito (R5: %s + %d, y la moneda de despues: + 4) y nada de Spotify en el estado" % (c_antes, len(sp_antes)),
         c_antes is not None and c_desp == c_antes + len(sp_antes) + 4 and "spotify:" not in est,
         "creditos %s -> %s; %s" % (c_antes, c_desp, " ".join(l for l in est.splitlines() if l.startswith(("sonando=", "cola=")))[:200]))
    P.si("y no se vuelve a pedir nada a Soloist (ningun play despues de volver)", not re.search(r"^.*play spotify:track", nucleo().split("vigilante abierto")[-1], re.M))
    P.espera("grep -q '^fase=corriendo' /run/rocola-soloist/estado", 300, 5)
    vig2 = vigilante_pid(); sol2 = P.orden("pidof soloist").strip()
    P.si("un vigilante nuevo (el viejo se fue con su padre) y un solo Soloist", bool(vig2) and vig2 != vig and len(sol2.split()) == 1,
         "vigilante %s -> %s, soloist %s -> %s" % (vig, vig2, sol, sol2))

def sonido():
    P.di("=== sonido: lo grabado de la tarjeta de sonido de la maquina (el WAV de QEMU)")
    wl = lee(os.path.join(S, "wav.txt")).splitlines()
    wav = wl[0] if wl else ""; desde = float(wl[1]) if len(wl) > 1 else 0.0
    P.info("lo grabado: %s, desde el segundo %.0f (el principio de «pide»)" % (os.path.basename(wav), desde))
    t = P.tonos(wav, desde)
    run = []   # tramos seguidos de la misma frecuencia
    for a, b, f, db in t:
        if f <= 0 or db < -45: continue
        if run and abs(run[-1][2] - f) <= 3 and a - run[-1][1] < 2.5: run[-1][1] = b
        else: run.append([a, b, f])
    largos = [(round(a), round(b), f) for a, b, f in run if b - a >= 2]   # (2 s: la cancion de prueba de la cola dura 3)
    P.info("lo que sono (tramos de 2 s o mas): " + ", ".join("%d-%d s %d Hz" % x for x in largos))
    open(os.path.join(S, "tramos.txt"), "w").write("\n".join("%d %d %d" % x for x in largos) + "\n")
    fs = [f for _, _, f in largos]
    sp = [x for x in largos if abs(x[2] - 300) <= 3]
    P.si("SUENA la de Spotify por la salida de la rocola: el tono del Soloist falso (300 Hz), pasando por rocola-pulso y el anillo",
         len(sp) >= 1 and sp[0][1] - sp[0][0] >= 20, ", ".join("%d-%d s" % (a, b) for a, b, _ in sp))
    i196 = next((i for i, f in enumerate(fs) if abs(f - 196) <= 2), -1)
    P.si("la cola sigue: la de XP (196 Hz) y despues las dos de Spotify (300 Hz, 300 Hz) y otra de XP",
         i196 >= 0 and len(fs) > i196 + 2 and abs(fs[i196 + 1] - 300) <= 3 and any(abs(f - 300) > 3 for f in fs[i196 + 2:]), " -> ".join(str(f) for f in fs))
    P.si("nada de lo que el Soloist falso toca solo al acabar una (990 Hz: su «autoplay») se oye", not any(abs(f - 990) <= 3 for _, _, f, db in t if db > -45))
    # el modo noche: el nivel del tono de 300 Hz en la primera de Spotify (de dia: sus segundos 1 a 8; el reloj pasa a las 23:00 a sus 10 s)
    # y en la ultima (la de «kill», pedida ya de noche y con la rampa de 20 s acabada). (En QEMU la rampa de XP no avanza mientras suena
    # algo: con musica la pantalla se para 6 a 7 s por vuelta y XP no da un paso de mas de 5 s -AvanzaModoNoche, «por si el equipo
    # durmio»-; avanza con la rocola callada (vueltas de 3 a 4 s). Por eso la que ya sonaba al pasar las 23:00, y la siguiente, siguen a
    # su nivel de dia, y la de «kill» -armada 15 min despues- sale ya baja. La rampa a media cancion la mide la escena «noche» de
    # spotify-xp, en el anfitrion.)
    tr = [x for x in largos if abs(x[2] - 300) <= 3]
    r = dict(tonos_rms(wav, 300, desde))
    if len(tr) >= 2:
        dia = [r[sg] for sg in range(tr[0][0] + 1, tr[0][0] + 9) if sg in r]
        nochex = [r[sg] for sg in range(tr[-1][0] + 3, tr[-1][1] - 1) if sg in r]
        if dia and len(nochex) >= 10:
            d1 = float(np.median(dia)); d2 = float(np.median(nochex))
            P.info("nivel con el tono de 300 Hz: la primera de Spotify, de dia, %.1f dBFS (%d s); la ultima, de noche, %.1f dBFS (%d s)" % (d1, len(dia), d2, len(nochex)))
            P.si("el modo noche baja la de Spotify como a las de XP (80 %%: -3,2 dB)", -4.2 <= d2 - d1 <= -2.2, "%.2f dB" % (d2 - d1))
        else: P.si("el modo noche: faltan segundos de 300 Hz en lo grabado", False, "%d de dia, %d de noche" % (len(dia), len(nochex)))
    else: P.si("el modo noche: no hay dos tramos de 300 Hz en lo grabado", False)

def fin():
    P.di("=== fin: secretos, bitacoras y el reloj de la maquina")
    # ninguna llave en las bitacoras ni en lo que escribe la rocola (salvo sus dos archivos)
    pat = "|".join(re.escape(x) for x in SECRETOS)
    hay = P.orden("grep -rlE '%s' /rocola/datos /run 2>/dev/null | grep -v -e '/rocola_spotify_llave.txt$' -e '/rocola_spotify_api.txt$' -e '^/run/soloist-falso.log$' | head" % pat, 120).strip()
    P.si("ninguna llave fuera de sus dos archivos (ni en las bitacoras, ni en el estado, ni en /run)", hay == "", hay.replace("\n", " "))
    for b in ("rocola_soloist.txt", "rocola_spotify.txt", "rocola_os_bitacora.txt"):
        P.orden("cp /rocola/datos/xp/%s /tmp/ 2>/dev/null" % b)
        t = P.orden("cat /rocola/datos/xp/%s 2>/dev/null" % b, 60)
        open(os.path.join(S, b), "w").write(t + "\n")
    P.info("bitacora del vigilante (ultimas): " + " | ".join(lee(os.path.join(S, "rocola_soloist.txt")).splitlines()[-6:])[:500])
    err = P.orden("tr -d '\\r' < /rocola/datos/xp/rocola_error.txt | grep -v -e 'Pantalla parada' -e '^(sin excepcion)' -e '^$' -e 'Latencia de audio' -e 'Sin huecos' -e 'MP3 sin huecos' -e 'Volumen de Windows' -e 'Cortes de sonido' | tail -12")
    P.info("rocola_error.txt (sin las paradas de pantalla de QEMU ni los cortes): " + (err.replace("\n", " | ")[:800] or "nada"))
    P.orden("date -s @%d >/dev/null" % int(time.time()))   # (el reloj de la maquina, otra vez a la hora del anfitrion)
    P.orden("sync", 60)
    # los dos servidores de mentira (los dejo corriendo «prepara»): fuera
    for f in ("api.pid", "cdn.pid"):
        try: os.kill(int(lee(os.path.join(S, f), "0")), signal.SIGTERM)
        except (OSError, ValueError): pass

FASES = {"prepara": prepara, "limpia": limpia, "sinllave": sinllave, "red": red, "pide": pide, "noche": noche, "kill": kill, "sonido": sonido, "fin": fin}
if __name__ == "__main__":
    pedidas = sys.argv[1:] or ["prepara", "limpia", "sinllave", "red", "pide", "noche", "kill", "sonido", "fin"]
    if pedidas[0] not in ("prepara", "limpia", "sinllave") and pid(P.MAQ): P.c = Control(P.MAQ)
    P.di("##### spotify.py " + " ".join(pedidas))
    try:
        for f in pedidas: FASES[f]()
    finally:
        P.di("##### RESUMEN spotify: %d BIEN, %d MAL" % (P.bien, P.mal))
    sys.exit(1 if P.mal else 0)
