# -*- coding: utf-8 -*-
# ============================================================================================
#  MOTOR DE IA EN TIEMPO REAL (rocola-hd): mantiene Apollo cargado en la GPU y procesa canciones
#  BAJO DEMANDA. La rocola (C#) pide trabajos dejando un archivo .job; el motor los procesa y
#  deja el resultado en cache/listos/<hash>.wav. La rocola reproduce ese wav si existe; si no,
#  reproduce el original (nunca se bloquea). Con cache: cada cancion se procesa UNA vez.
#
#  Protocolo (carpeta, sin sockets ni dependencias raras -> robusto y facil de depurar):
#    cache/pendientes/<hash>.job   (JSON: {"src": ruta, "mezcla": "auto"|"0".."100"})  <- lo deja la rocola
#    cache/listos/<hash>.wav                                                            <- lo deja el motor
#    cache/listos/<hash>.error                                                          <- si algo fallo
#  El HASH lo calculan IGUAL la rocola y el motor: sha1( ruta_minusculas | tamano_bytes | mezcla )[:16].
#  Usa el TAMANO del archivo (no la fecha) para que C# y Python coincidan exacto.
#
#    python daemon_ia.py [--cache <carpeta>] [--tope_gb 20]
# ============================================================================================
import sys, os, json, time, glob, hashlib, tempfile, subprocess, argparse, shutil
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "upscale"))
from util_audio import FS, leer_wav, escribir_wav, acantilado_hz, por_trozos, mezclar, mezcla_auto

def hash_trabajo(src, mezcla):
    try: size = os.path.getsize(src)
    except OSError: size = 0
    clave = (os.path.abspath(src).lower() + "|" + str(size) + "|" + str(mezcla)).encode("utf-8", "replace")
    return hashlib.sha1(clave).hexdigest()[:16]

def _log(cache, msg):
    try:
        with open(os.path.join(cache, "motor_log.txt"), "a", encoding="utf-8") as f:
            f.write(time.strftime("%Y-%m-%d %H:%M:%S  ") + msg + "\n")
    except OSError: pass

def _dirs(cache):
    p = os.path.join(cache, "pendientes"); l = os.path.join(cache, "listos")
    os.makedirs(p, exist_ok=True); os.makedirs(l, exist_ok=True)
    return p, l

def procesar_uno(job_path, listos, proc, ffmpeg, seg=10.0):
    """Procesa UN trabajo: decodifica, Apollo, mezcla, escribe cache/listos/<hash>.wav (atomico).
    Los temporales van a cache/tmp (MISMO disco del release), nunca a C:\Temp."""
    tmpdir = os.path.join(os.path.dirname(listos), "tmp")
    try: os.makedirs(tmpdir, exist_ok=True)
    except OSError: tmpdir = os.path.dirname(listos)
    try:
        with open(job_path, encoding="utf-8") as f: job = json.load(f)
    except Exception:
        try: os.remove(job_path)
        except OSError: pass
        return None
    h = os.path.splitext(os.path.basename(job_path))[0]
    dst = os.path.join(listos, h + ".wav")
    if os.path.exists(dst):                        # ya en cache: nada que hacer
        _quita(job_path); return dst
    src = job.get("src"); mez = job.get("mezcla", "auto")
    try:
        tmp = os.path.join(tmpdir, "ia_dec_%s.wav" % h)
        subprocess.run([ffmpeg, "-y", "-v", "error", "-i", src, "-ac", "2", "-ar", str(FS),
                        "-sample_fmt", "s16", tmp], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        x, _ = leer_wav(tmp)
        cliff = acantilado_hz(x)
        if cliff >= 18500:                         # ya buena: el "procesado" es el original (rapido)
            escribir_wav(dst + ".tmp", x)
        else:
            y = por_trozos(x, proc, seg=seg)
            m = mezcla_auto(cliff) if mez == "auto" else max(0.0, min(1.0, float(mez) / 100.0))
            escribir_wav(dst + ".tmp", mezclar(y, x, m))
        os.replace(dst + ".tmp", dst)              # atomico: la rocola nunca ve un wav a medias
        _quita(job_path)
        try: os.remove(tmp)
        except OSError: pass
        return dst
    except Exception as e:
        try: open(os.path.join(listos, h + ".error"), "w", encoding="utf-8").write(str(e))
        except OSError: pass
        _quita(job_path)
        return None

def _quita(p):
    try: os.remove(p)
    except OSError: pass

def evictar(listos, tope_gb):
    """Si la cache pasa del tope, borra los wav MENOS usados recientemente (por fecha de acceso)."""
    tope = tope_gb * (1024 ** 3)
    wavs = [os.path.join(listos, f) for f in os.listdir(listos) if f.endswith(".wav")]
    total = sum(os.path.getsize(w) for w in wavs)
    if total <= tope: return
    wavs.sort(key=lambda w: os.path.getatime(w))   # mas viejo (menos usado) primero
    for w in wavs:
        if total <= tope: break
        try: total -= os.path.getsize(w); os.remove(w)
        except OSError: pass

def un_ciclo(pend, listos, proc, ffmpeg, tope_gb, seg=10.0):
    jobs = sorted(glob.glob(os.path.join(pend, "*.job")), key=lambda p: os.path.getmtime(p))
    n = 0
    for j in jobs:
        procesar_uno(j, listos, proc, ffmpeg, seg); n += 1
    if n: evictar(listos, tope_gb)
    return n

def _un_ciclo_log(pend, listos, proc, ffmpeg, tope_gb, cache, seg=10.0):
    jobs = sorted(glob.glob(os.path.join(pend, "*.job")), key=lambda p: os.path.getmtime(p))
    for j in jobs:
        r = procesar_uno(j, listos, proc, ffmpeg, seg)
        _log(cache, ("procesada -> " + os.path.basename(r)) if r else ("fallo un trabajo: " + os.path.basename(j)))
    if jobs: evictar(listos, tope_gb)
    return len(jobs)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", default=os.path.join(AQUI, "cache"))
    ap.add_argument("--tope_gb", type=float, default=20.0)
    ap.add_argument("--seg", type=float, default=10.0)
    a = ap.parse_args()
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg: print("[X] Falta ffmpeg"); return 1
    pend, listos = _dirs(a.cache)
    _log(a.cache, "arrancando motor; cache=" + a.cache)
    print("Motor de IA: cargando Apollo en la GPU (una vez)...")
    try:
        import modelo_apollo
        proc = modelo_apollo.cargar()
    except Exception as e:
        _log(a.cache, "ERROR cargando Apollo: " + repr(e))
        print("[X] No se pudo cargar Apollo:", e)
        raise
    _log(a.cache, "Apollo LISTO")
    print("Motor de IA LISTO. Escuchando trabajos en:", pend)
    print("Deja esta ventana ABIERTA mientras uses la rocola con IA en vivo.")
    vivo = os.path.join(a.cache, "motor.vivo")
    while True:
        try:
            try: open(vivo, "w").write(str(int(time.time())))   # LATIDO: la rocola ve que el motor corre (no lo re-lanza)
            except OSError: pass
            hechos = _un_ciclo_log(pend, listos, proc, ffmpeg, a.tope_gb, a.cache, a.seg)
            time.sleep(0.2 if hechos else 0.6)     # atiende rapido si hay cola; descansa si no
        except KeyboardInterrupt:
            print("\nMotor detenido."); return 0
        except Exception as e:
            print("aviso:", e); time.sleep(1.0)

if __name__ == "__main__":
    sys.exit(main())
