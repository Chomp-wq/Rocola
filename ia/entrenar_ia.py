#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# ============================================================================================
#  ENTRENADOR DE LA IA PROPIA DE LA ROCOLA
# --------------------------------------------------------------------------------------------
#  Entrena el MLP (28 -> 48 -> 48 -> 12, tanh) que predice la ENVOLVENTE de la banda de agudos
#  que el codec corto, a partir de la forma del espectro que sobrevivio. El reconstructor
#  espectral de la rocola (SpectralTap.RegenNeural) usa esa envolvente en vez de la caida fija
#  de -9 dB/octava. El CONTRATO de entrada/salida (RIA1) debe coincidir EXACTO con Form1.cs.
#
#  Uso:
#    python3 entrenar_ia.py                        # entrena con audio SINTETICO (prueba de concepto)
#    python3 entrenar_ia.py --musica /ruta/wavs    # entrena con TU musica (archivos .wav 44.1k)
#    python3 entrenar_ia.py --datos rocola_ia_datos.bin --capas 28,96,96,12
#        entrena con el material que la ROCOLA junto sola de toda su biblioteca. Ese archivo lo
#        genera el pre-analisis (se copia en una USB); no hace falta mover el disco de la rocola.
#      --max-ejemplos N cuantos ejemplos usar como mucho (defecto 4 millones; 0 = todos). El archivo
#                       puede pesar gigas: se toma una muestra REPARTIDA por todo el, sin cargarlo entero.
#      --clips N        cuantos trozos de 2s usar en total (defecto 400; para produccion 6000+)
#      --por_archivo M  tope de trozos por cancion (defecto sin tope; 12 = mas VARIEDAD de canciones)
#  Para preparar los wav desde tu biblioteca mp3 usa entrenar_con_mi_musica.bat (hace todo solo).
#
#  Salidas (en esta carpeta):
#    rocola_ia.bin          -> copialo JUNTO AL Rocola.exe para activar la IA
#    ia_vectores_prueba.txt -> vectores de paridad (los usa la bateria de pruebas en C#)
#
#  Requisitos: python3 + numpy. Para MP3, convierte antes a WAV (ffmpeg -i cancion.mp3 cancion.wav).
# ============================================================================================
import sys, os, wave, struct
from functools import lru_cache
import numpy as np

FS = 44100
EPS = 1e-12

# ---------------- contrato RIA1: la geometria de la ROCOLA (ver contrato_dsee.py) ----------------
# Hasta la version 1 del extractor este archivo tenia su propia geometria, y no era la de la rocola:
# techo de 20 kHz (la rocola llega a 20,5), corte sin redondear a bin, tope del objetivo en +0,5
# (la rocola en +0,1) y bandas vacias estiradas. La red aprendia 12 bandas que no eran las que la
# rocola rellena. Ahora el extractor ES el de la rocola, transcrito con su mismo redondeo float32,
# y pruebas/iaparidad.cs compara los dos lenguajes corte por corte.
from contrato_dsee import EXTRACTOR, corte_de, par_desde_potencia

# La cosecha de la rocola salta los cuadros casi mudos ANTES de analizarlos (energia media por muestra
# <= 1e-8). Es un filtro del cuadro, no del extractor: aqui se aplica en el mismo sitio.
UMBRAL_MUDO = 1e-8

def features_and_target(mag2n, frame, fc_hz, fs=FS):
    """mag2n = |X[k]/N|^2 del cuadro COMPLETO (con banda alta real). fc_hz es el corte SIMULADO,
    como el cutHzSim de MakePair: se redondea a bin igual que alli. Devuelve (x28, y12) o None
    exactamente cuando la rocola devolveria false."""
    cut = corte_de(fs, frame, fc_hz)
    r = par_desde_potencia(fs, frame, cut, np.asarray(mag2n, dtype=np.float64) * float(frame * frame))
    if r is None:
        return None
    return r[0].astype(np.float64), r[1].astype(np.float64)

@lru_cache(maxsize=2, typed=True)
def _ventana_entrenamiento(frame):
    # Solo se cachean los dos tamanos del contrato; inmutable para no contaminar otros clips.
    win = np.sqrt(0.5 - 0.5 * np.cos(2.0 * np.pi * np.arange(frame) / frame))
    win.flags.writeable = False
    return win

def frames_of(sig, frame):
    """Cuadros con ventana raiz-de-Hann (la MISMA del SpectralTap) -> |X/N|^2. Se saltan los
    cuadros casi mudos con el mismo umbral que la cosecha de la rocola."""
    win = (_ventana_entrenamiento(frame) if frame in (2048, 4096) else
           np.sqrt(0.5 - 0.5 * np.cos(2.0 * np.pi * np.arange(frame) / frame)))
    hop = frame // 2
    out = []
    for p in range(0, len(sig) - frame, hop):
        trozo = np.asarray(sig[p:p + frame], dtype=np.float64)
        if float(np.dot(trozo, trozo)) / frame <= UMBRAL_MUDO:
            continue
        X = np.fft.rfft(trozo * win)
        m = (np.abs(X) / frame) ** 2
        out.append(m)
    return out

# ---------------- datos: sintetico "musical" o carpeta de WAVs ----------------
def synth_clip(rng, secs=1.2):
    """Musica de mentiras pero VARIADA: voces armonicas con distinto brillo + percusion ruidosa.
    La variedad de brillo (tilt) es lo que ensena a la red la relacion forma->envolvente."""
    n = int(FS * secs)
    t = np.arange(n) / FS
    x = np.zeros(n)
    for _ in range(rng.integers(2, 6)):
        f0 = rng.uniform(80, 900)
        nh = int(min(20000 / f0, rng.uniform(10, 70)))
        if nh < 3: continue
        tilt = rng.uniform(0.4, 2.2)
        h = np.arange(1, nh + 1)
        amps = h ** (-tilt) * rng.uniform(0.6, 1.4, nh)
        ph = rng.uniform(0, 2 * np.pi, nh)
        env = np.minimum(1.0, t * rng.uniform(2, 30)) * np.exp(-t * rng.uniform(0.2, 2.5))
        x += env * (amps[None, :] * np.sin(2 * np.pi * t[:, None] * (f0 * h)[None, :] + ph[None, :])).sum(axis=1) * 0.12
    for _ in range(rng.integers(2, 7)):                   # "platillos": ruido con paso-altas y decaimiento
        p0 = rng.integers(0, n - FS // 5)
        ln = rng.integers(FS // 40, FS // 6)
        burst = rng.normal(0, 1, ln)
        B = np.fft.rfft(burst)
        f = np.fft.rfftfreq(ln, 1 / FS)
        fcut = rng.uniform(1500, 9000)
        B *= 1.0 / (1.0 + (fcut / (f + 1)) ** 4)          # paso-altas suave
        burst = np.fft.irfft(B, ln) * np.exp(-np.arange(ln) / (ln * rng.uniform(0.15, 0.6)))
        x[p0:p0 + ln] += burst * rng.uniform(0.02, 0.15)
    m = np.abs(x).max()
    return (x / m * 0.5) if m > 0 else x

def load_wavs(folder, rng, max_clips=400, max_per_file=0):
    clips = []
    files = [f for f in sorted(os.listdir(folder)) if f.lower().endswith(".wav")]
    rng.shuffle(files)
    for fn in files:
        try:
            with wave.open(os.path.join(folder, fn), "rb") as w:
                if w.getframerate() != FS or w.getsampwidth() != 2:
                    continue
                nch = w.getnchannels()
                nfr = min(w.getnframes(), FS * 60)
                quedan = max(1, max_clips - len(clips))
                if max_per_file:
                    quedan = min(quedan, max(1, max_per_file))
                # El bucle historico excluye el ultimo trozo si termina EXACTAMENTE en EOF.
                # Leer una muestra extra mantiene ese borde sin retener un minuto por un solo clip.
                nfr = min(nfr, quedan * FS * 2 + 1)
                raw = np.frombuffer(w.readframes(nfr), dtype=np.int16).astype(np.float64) / 32768.0
            mono = raw.reshape(-1, nch).mean(axis=1) if nch > 1 else raw
            propios = 0
            for p in range(0, len(mono) - FS * 2, FS * 2):     # trozos de 2s
                clips.append(mono[p:p + FS * 2]); propios += 1
                if len(clips) >= max_clips: return clips
                if max_per_file and propios >= max_per_file: break   # variedad: mejor MAS canciones que mas trozos de una
        except Exception:
            continue
    return clips

def build_dataset(clips, rng):
    X, Y = [], []
    usados = 0
    bloque = 4096
    for ci, clip in enumerate(clips):
        fc = rng.uniform(10500, 17500)                     # el "corte del MP3" simulado, por clip
        for frame in (2048, 4096):                          # ambos tamanos de ventana de la rocola
            for m in frames_of(clip, frame):
                r = features_and_target(m, frame, fc)
                if r is not None:
                    fila = usados % bloque
                    if fila == 0:
                        X.append(np.empty((bloque, 28), dtype=np.float64))
                        Y.append(np.empty((bloque, 12), dtype=np.float64))
                    X[-1][fila] = r[0]; Y[-1][fila] = r[1]
                    usados += 1
    if not usados:
        return np.array([], dtype=np.float64), np.array([], dtype=np.float64)
    ultimo = (usados - 1) % bloque + 1
    X[-1] = X[-1][:ultimo]; Y[-1] = Y[-1][:ultimo]
    return np.concatenate(X), np.concatenate(Y)

# ---------------- MLP + Adam (backprop a mano: sin dependencias pesadas) ----------------
class Mlp:
    def __init__(self, dims, rng):
        self.W = [rng.normal(0, np.sqrt(2.0 / dims[i]), (dims[i + 1], dims[i])) for i in range(len(dims) - 1)]
        self.B = [np.zeros(dims[i + 1]) for i in range(len(dims) - 1)]
    def forward(self, X):
        acts = [X]
        for L in range(len(self.W)):
            z = acts[-1] @ self.W[L].T + self.B[L]
            acts.append(np.tanh(z) if L < len(self.W) - 1 else z)
        return acts
    def predict(self, X): return self.forward(X)[-1]

class FilasMapeadas:
    """Vista por indices: solo el lote solicitado se convierte a float64."""
    def __init__(self, mapa, filas, inicio, fin):
        self.mapa, self.filas = mapa, filas
        self.inicio, self.fin = inicio, fin
        self.shape = (len(filas), fin-inicio)
    def __len__(self): return len(self.filas)
    def seleccionar(self, indices):
        return FilasMapeadas(self.mapa, self.filas[indices], self.inicio, self.fin)
    def __getitem__(self, key):
        rows, columns = key if isinstance(key, tuple) else (key, slice(None))
        values = np.asarray(self.mapa[self.filas[rows], self.inicio:self.fin], dtype=np.float64)
        return values[..., columns]

def mse_modelo(model, X, Y, lote=4096):
    if not isinstance(X, FilasMapeadas):
        return float(((model.predict(X) - Y) ** 2).mean())
    total = 0.0
    count = 0
    for p in range(0, len(X), lote):
        error = (model.predict(X[p:p+lote]) - Y[p:p+lote]) ** 2
        total += float(error.sum()); count += error.size
    return total / max(1, count)

def train(model, X, Y, Xv, Yv, epochs=60, lr=2e-3, batch=256, seed=3):
    if epochs <= 0 or batch <= 0 or len(X) == 0 or len(Xv) == 0:
        raise ValueError("Se requieren epocas/lote positivos y ejemplos de entrenamiento y validacion")
    rng = np.random.default_rng(seed)
    mW = [np.zeros_like(w) for w in model.W]; vW = [np.zeros_like(w) for w in model.W]
    mB = [np.zeros_like(b) for b in model.B]; vB = [np.zeros_like(b) for b in model.B]
    b1, b2, eps, tstep = 0.9, 0.999, 1e-8, 0
    best = float('inf')
    best_weights = best_biases = None
    for ep in range(epochs):
        idx = rng.permutation(len(X))
        for s in range(0, len(X), batch):
            bi = idx[s:s + batch]
            acts = model.forward(X[bi])
            g = 2.0 * (acts[-1] - Y[bi]) / len(bi)          # dMSE
            tstep += 1
            c1, c2 = 1 - b1 ** tstep, 1 - b2 ** tstep
            for L in reversed(range(len(model.W))):
                gW = g.T @ acts[L]; gB = g.sum(axis=0)
                if L > 0: g = (g @ model.W[L]) * (1.0 - acts[L] ** 2)
                mW[L] = b1 * mW[L] + (1 - b1) * gW; vW[L] = b2 * vW[L] + (1 - b2) * gW ** 2
                mB[L] = b1 * mB[L] + (1 - b1) * gB; vB[L] = b2 * vB[L] + (1 - b2) * gB ** 2
                model.W[L] -= lr * (mW[L] / c1) / (np.sqrt(vW[L] / c2) + eps)
                model.B[L] -= lr * (mB[L] / c1) / (np.sqrt(vB[L] / c2) + eps)
        if (ep + 1) % 10 == 0 or ep == 0 or ep + 1 == epochs:
            tr = mse_modelo(model, X, Y)
            vl = mse_modelo(model, Xv, Yv)
            if np.isfinite(vl) and vl < best and all(np.isfinite(v).all() for v in model.W + model.B):
                best = vl
                best_weights = [w.copy() for w in model.W]
                best_biases = [b.copy() for b in model.B]
            print("  epoca %3d  mse-entren %.4f  mse-valid %.4f" % (ep + 1, tr, vl))
    if best_weights is None:
        raise ValueError("Ningun punto de entrenamiento valido; se conserva el modelo anterior")
    model.W, model.B = best_weights, best_biases
    return model

def export_ria1(model, path):
    from calidad_dsee import exportar
    exportar(model, path)

def cargar_datos_rocola(ruta, max_ejemplos=4000000, por_indices=False):
    """Lee el material que la rocola junto sola (rocola_ia_datos.bin).

    Formato: cabecera "RIAD" + version + n_entradas + n_salidas, y luego cada par como
    28 float32 de entrada + 12 float32 de objetivo. Son los MISMOS numeros que la rocola
    calcula al reproducir, asi que lo aprendido aqui le sirve tal cual alla.

    El archivo puede pesar MUCHOS gigas (la rocola lo deja crecer hasta su tope), y cargarlo
    entero en memoria pediria mas RAM de la que tiene una PC normal. Asi que se abre "asomado"
    al disco (memmap) y se toma una muestra REPARTIDA de punta a punta: uno de cada tantos
    ejemplos. Como el archivo va en orden de cancion, repartir es justo lo que hace falta para
    que entren canciones de toda la biblioteca y no solo las primeras. Con --max-ejemplos se
    cambia cuantos se usan (0 = todos, solo si te sobra la RAM).
    """
    if max_ejemplos < 0:
        raise ValueError("max_ejemplos debe ser cero o positivo")
    with open(ruta, "rb") as f:
        head = f.read(16)
        if len(head) < 16 or head[:4] != b"RIAD":
            raise SystemExit("No parece un archivo de la rocola: " + ruta)
        ver, nin, nout = struct.unpack("<iii", head[4:16])
        if ver != 1 or nin != 28 or nout != 12:
            raise SystemExit("Version/tamanos no compatibles (ver=%d in=%d out=%d)" % (ver, nin, nout))
    por = nin + nout
    cuerpo = os.path.getsize(ruta) - 16
    n = cuerpo // (por * 4)
    if n < 100:
        raise SystemExit("Muy pocos ejemplos (%d). Deja que la rocola junte mas material." % n)
    mm = np.memmap(ruta, dtype="<f4", mode="r", offset=16, shape=(n, por))
    paso = 1
    if max_ejemplos and n > max_ejemplos:
        paso = int(np.ceil(n / float(max_ejemplos)))
        print("  el archivo trae %s ejemplos (%.1f GB): se toma 1 de cada %d, repartidos de punta a punta"
              % (format(n, ","), cuerpo / 1024.0 ** 3, paso))
    if por_indices:
        filas = np.arange(0, n, paso, dtype=np.int64)
        return FilasMapeadas(mm, filas, 0, nin), FilasMapeadas(mm, filas, nin, por)
    crudo = np.array(mm[::paso], dtype=np.float64)
    del mm
    return crudo[:, :nin], crudo[:, nin:]


def main():
    rng = np.random.default_rng(20260715)
    if "--datos" in sys.argv:
        ruta = sys.argv[sys.argv.index("--datos") + 1]
        tope = int(sys.argv[sys.argv.index("--max-ejemplos") + 1]) if "--max-ejemplos" in sys.argv else 4000000
        print("Leyendo el material que junto la rocola:", ruta)
        X, Y = cargar_datos_rocola(ruta, tope, por_indices=True)
        print("  %d ejemplos de %d entradas -> %d salidas" % (X.shape[0], X.shape[1], Y.shape[1]))
        clips = None
    elif "--musica" in sys.argv:
        folder = sys.argv[sys.argv.index("--musica") + 1]
        nclips = int(sys.argv[sys.argv.index("--clips") + 1]) if "--clips" in sys.argv else 400
        porarch = int(sys.argv[sys.argv.index("--por_archivo") + 1]) if "--por_archivo" in sys.argv else 0
        print("Cargando WAVs de:", folder)
        clips = load_wavs(folder, rng, max_clips=nclips, max_per_file=porarch)
        if len(clips) < 20:
            print("Muy pocos WAV 44.1k/16-bit utilizables (%d). Convierte con ffmpeg y reintenta." % len(clips)); return 1
        print("  %d trozos de musica real" % len(clips))
    else:
        print("Generando audio SINTETICO variado (prueba de concepto; para produccion usa --musica)...")
        clips = [synth_clip(rng) for _ in range(320)]
    if clips is not None:
        print("Extrayendo pares (forma espectral -> envolvente real)...")
        X, Y = build_dataset(clips, rng)
        if len(X) == 0:
            print("No se extrajeron ejemplos utilizables; no se guarda ningun modelo.")
            return 1
        print("  %d ejemplos de %d entradas -> %d salidas" % (X.shape[0], X.shape[1], Y.shape[1]))
    if len(X) <= 500:
        print("Se requieren mas de 500 ejemplos: 500 se reservan para validar. No se guarda ningun modelo.")
        return 1
    idx = rng.permutation(len(X))
    nv = max(500, len(X) // 10)
    if isinstance(X, FilasMapeadas):
        Xv, Yv = X.seleccionar(idx[:nv]), Y.seleccionar(idx[:nv])
        X, Y = X.seleccionar(idx[nv:]), Y.seleccionar(idx[nv:])
    else:
        X, Y = X[idx], Y[idx]
        Xv, Yv, X, Y = X[:nv], Y[:nv], X[nv:], Y[nv:]

    capas = [28, 48, 48, 12]
    if "--capas" in sys.argv:
        capas = [int(v) for v in sys.argv[sys.argv.index("--capas") + 1].split(",")]
    # La rocola solo acepta hasta 8 matrices de pesos y 512 neuronas por capa (ver NeuralEnv).
    if capas[0] != 28 or capas[-1] != 12:
        raise SystemExit("Las capas deben empezar en 28 y terminar en 12 (contrato de la rocola).")
    if len(capas) - 1 > 8 or max(capas) > 512 or min(capas) < 1:
        raise SystemExit("Demasiado grande para la rocola: maximo 8 capas de pesos y 512 neuronas.")
    print("Entrenando el MLP %s ..." % "->".join(str(c) for c in capas))
    model = train(Mlp(capas, rng), X, Y, Xv, Yv)
    from calidad_dsee import cuantizar, evaluar, escribir_atomico
    cuantizar(model)
    accepted, error_red, error_base = evaluar(model, Xv, Yv)
    mse_net, mse_base = error_red.mean(), error_base.mean()
    print("RESULTADO  mse-valid: red = %.4f  |  curva fija = %.4f  (mejora %.1fx)" % (mse_net, mse_base, mse_base / max(mse_net, 1e-9)))

    print("  MSE por banda: red", error_red, "| referencia", error_base)
    if not accepted:
        print("NO se guarda: exige 5% de mejora global y ninguna banda mas de 10% peor. Se conservan modelo y vectores anteriores.")
        return 2
    here = os.path.dirname(os.path.abspath(__file__))
    P = model.predict(Xv[:24])
    vectors = "".join(" ".join("%.9g" % v for v in Xv[i]) + " | " + " ".join("%.9g" % v for v in P[i]) + "\n" for i in range(24))
    # Cada archivo se reemplaza atomicamente. No constituyen una transaccion conjunta.
    escribir_atomico(os.path.join(here, "ia_vectores_prueba.txt"), lambda f: f.write(vectors.encode("ascii")))
    export_ria1(model, os.path.join(here, "rocola_ia.bin"))
    print("Listo: rocola_ia.bin (copialo junto a Rocola.exe) + ia_vectores_prueba.txt")
    return 0

if __name__ == "__main__":
    sys.exit(main())
