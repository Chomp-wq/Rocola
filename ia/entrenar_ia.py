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
import sys, os, wave, struct, hashlib, json
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

def frames_of(sig, frame, con_energia=False):
    """Cuadros con ventana raiz-de-Hann (la MISMA del SpectralTap) -> |X/N|^2. Se saltan los
    cuadros casi mudos con el mismo umbral que la cosecha de la rocola. Con con_energia=True cada
    cuadro va con la energia del suyo y la del cuadro que empieza medio cuadro antes (aunque ese
    fuera mudo): con eso se sabe si es un ATAQUE (ver TRANSITORIO)."""
    win = (_ventana_entrenamiento(frame) if frame in (2048, 4096) else
           np.sqrt(0.5 - 0.5 * np.cos(2.0 * np.pi * np.arange(frame) / frame)))
    hop = frame // 2
    out = []
    previa = None                     # el primer cuadro de un trozo no tiene anterior: no se sabe si es ataque
    for p in range(0, len(sig) - frame, hop):
        trozo = np.asarray(sig[p:p + frame], dtype=np.float64)
        energia = float(np.dot(trozo, trozo)) / frame
        if energia > UMBRAL_MUDO:
            X = np.fft.rfft(trozo * win)
            m = (np.abs(X) / frame) ** 2
            out.append((m, energia, previa) if con_energia else m)
        previa = energia
    return out

# Un cuadro es TRANSITORIO (un ataque: golpe, pua, consonante) si su energia es al menos 4 veces (+6 dB)
# la del cuadro que empieza medio cuadro antes. Solo se puede saber con la senal a mano.
TRANSITORIO = 4.0

def es_transitorio(energia, previa):
    return previa is not None and energia >= TRANSITORIO * max(previa, UMBRAL_MUDO)

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
    """Trozos de 2 s de los WAV 44,1 kHz/16 bits de una carpeta. Devuelve (clips, grupos):
    clips es una lista de senales de UN canal (float32) y grupos, la huella de origen de cada una.

    CADA CANAL POR SEPARADO. Antes se promediaban L y R: un contenido en contrafase (L = s, R = -s)
    se anulaba y la red aprendia de un silencio que la cancion no tenia. La rocola tampoco mezcla:
    RegenNeural trabaja canal por canal. Los canales de un mismo archivo llevan el MISMO grupo, asi que
    nunca acaban uno en entrenamiento y otro en validacion. --clips cuenta trozos de 2 s (con todos sus
    canales), como antes. Se guardan en float32: el doble de canales ocupa lo mismo que el mono en float64.

    Si junto a los WAV hay un origen.tsv (lo deja preparar_entrenamiento.py: nombre<TAB>huella de la
    carpeta de la cancion original), los WAV de una misma carpeta de la biblioteca van juntos."""
    import origen_dsee as O
    carpeta_de = {}
    tsv = os.path.join(folder, "origen.tsv")
    if os.path.exists(tsv):
        with open(tsv, "r", encoding="utf-8", errors="replace") as f:
            for linea in f:
                t = linea.rstrip("\n").split("\t")
                if len(t) >= 2:
                    try:
                        carpeta_de[t[0].lower()] = int(t[1], 16)
                    except ValueError:
                        pass
    clips, claves = [], []
    trozos = 0
    files = [f for f in sorted(os.listdir(folder)) if f.lower().endswith(".wav")]
    rng.shuffle(files)
    for fn in files:
        try:
            with wave.open(os.path.join(folder, fn), "rb") as w:
                if w.getframerate() != FS or w.getsampwidth() != 2:
                    continue
                nch = w.getnchannels()
                nfr = min(w.getnframes(), FS * 60)
                quedan = max(1, max_clips - trozos)
                if max_per_file:
                    quedan = min(quedan, max(1, max_per_file))
                # El bucle historico excluye el ultimo trozo si termina EXACTAMENTE en EOF.
                # Leer una muestra extra mantiene ese borde sin retener un minuto por un solo clip.
                nfr = min(nfr, quedan * FS * 2 + 1)
                raw = np.frombuffer(w.readframes(nfr), dtype=np.int16).astype(np.float32) / np.float32(32768.0)
            canales = raw.reshape(-1, nch) if nch > 1 else raw.reshape(-1, 1)
            clave = (O.huella(fn), carpeta_de.get(fn.lower(), 0))
            propios = 0
            for p in range(0, canales.shape[0] - FS * 2, FS * 2):     # trozos de 2s
                for c in range(canales.shape[1]):
                    clips.append(np.ascontiguousarray(canales[p:p + FS * 2, c]))
                    claves.append(clave)
                trozos += 1; propios += 1
                if trozos >= max_clips: return clips, claves
                if max_per_file and propios >= max_per_file: break   # variedad: mejor MAS canciones que mas trozos de una
        except Exception:
            continue
    return clips, claves

def build_dataset_detalle(clips, rng):
    """Pares de todos los trozos, con el trozo de origen y si el cuadro es un transitorio."""
    X, Y, O, A = [], [], [], []
    usados = 0
    bloque = 4096
    for ci, clip in enumerate(clips):
        fc = rng.uniform(10500, 17500)                     # el "corte del MP3" simulado, por clip
        for frame in (2048, 4096):                          # ambos tamanos de ventana de la rocola
            for m, energia, previa in frames_of(clip, frame, con_energia=True):
                r = features_and_target(m, frame, fc)
                if r is not None:
                    fila = usados % bloque
                    if fila == 0:
                        X.append(np.empty((bloque, 28), dtype=np.float64))
                        Y.append(np.empty((bloque, 12), dtype=np.float64))
                        O.append(np.empty(bloque, dtype=np.int64))
                        A.append(np.empty(bloque, dtype=bool))
                    X[-1][fila] = r[0]; Y[-1][fila] = r[1]; O[-1][fila] = ci
                    A[-1][fila] = es_transitorio(energia, previa)
                    usados += 1
    if not usados:
        return (np.array([], dtype=np.float64), np.array([], dtype=np.float64),
                np.array([], dtype=np.int64), np.array([], dtype=bool))
    ultimo = (usados - 1) % bloque + 1
    X[-1] = X[-1][:ultimo]; Y[-1] = Y[-1][:ultimo]; O[-1] = O[-1][:ultimo]; A[-1] = A[-1][:ultimo]
    return np.concatenate(X), np.concatenate(Y), np.concatenate(O), np.concatenate(A)

def build_dataset(clips, rng, con_origen=False):
    """Pares de todos los trozos. Con con_origen=True devuelve ademas, por cada ejemplo, el indice del
    trozo del que salio (para repartir por grupo DESPUES sin mezclar)."""
    X, Y, O, _ = build_dataset_detalle(clips, rng)
    return (X, Y, O) if con_origen else (X, Y)

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
    best_epoch = 0
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
                best_epoch = ep + 1
                best_weights = [w.copy() for w in model.W]
                best_biases = [b.copy() for b in model.B]
            print("  epoca %3d  mse-entren %.4f  mse-valid %.4f" % (ep + 1, tr, vl))
    if best_weights is None:
        raise ValueError("Ningun punto de entrenamiento valido; se conserva el modelo anterior")
    model.W, model.B = best_weights, best_biases
    model.mejor_epoca, model.mejor_mse_validacion = best_epoch, best   # para el informe
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


# ---------------- particiones SIN MEZCLAR canciones ----------------
# Tres particiones, por GRUPO (cancion, y ademas carpeta/artista cuando se sabe), repartidas ANTES de
# mirar un solo cuadro:
#   entrenamiento -> ajusta los pesos
#   validacion    -> solo elige el mejor checkpoint
#   prueba        -> solo decide si el modelo se publica; nunca se usa para elegir nada
# Ningun grupo esta en dos particiones. Si no se sabe de donde viene el material (el rocola_ia_datos.bin
# de antes, sin rocola_ia_datos.idx), no se inventa: se entrena igual para poder mirar, pero la
# validacion NO es independiente y el resultado no se publica (codigo 3).
SALIDA_NO_VERIFICABLE = 3

def _toma(X, pos):
    return X.seleccionar(pos) if isinstance(X, FilasMapeadas) else X[pos]

def _reparto_legado(n, rng, motivo):
    """El reparto al azar de siempre, SOLO para poder mirar: marcado como no independiente."""
    idx = rng.permutation(n)
    nv = max(100, n // 10)
    return [idx[2 * nv:], idx[:nv], idx[nv:2 * nv]], {"independiente": False, "motivo": motivo}

def particiones_rocola(ruta, X, nivel="artista", incluir_sin_origen=False):
    """Reparto del material de la rocola segun su indice de origen. Devuelve (posiciones, informe) o
    (None, informe) si no hay reparto independiente posible. Las posiciones son indices dentro de X."""
    import origen_dsee as O
    filas = X.filas if isinstance(X, FilasMapeadas) else np.arange(len(X), dtype=np.int64)
    def avance(leidos, total):
        print("  comprobando que el indice describe este material: %.1f de %.1f GB" % (leidos / 1e9, total / 1e9))
    ent, inf = O.lee_indice(O.ruta_indice(ruta), ruta, avance=avance)
    informe = {"independiente": False, "motivo": "", "indice": inf, "nivel": nivel}
    if len(ent) == 0:
        informe["motivo"] = ("rocola_ia_datos.bin sin indice de origen (RIAD v1): no se sabe de que cancion "
                             "viene cada ejemplo" if not inf["indice"] else "el indice de origen no describe ningun ejemplo valido")
        return None, informe
    ini = ent["primero"].astype(np.int64)
    k = np.searchsorted(ini, filas, side="right") - 1
    dentro = (k >= 0) & (filas < (ini + ent["cuenta"])[np.maximum(k, 0)])
    pesos = np.bincount(k[dentro], minlength=len(ent))
    etiquetas, nivel_usado = O.grupos(ent, nivel)
    informe["nivel"] = nivel_usado
    parte, motivo = O.reparte(etiquetas, pesos)
    if motivo:
        informe["motivo"] = motivo
        return None, informe
    partes, sin = O.filas_de(ent, parte, filas)
    pos = [np.searchsorted(filas, p) for p in partes]
    informe["sin_origen"] = int(len(sin))
    informe["grupos"] = [int(len(np.unique(etiquetas[(parte == p) & (pesos > 0)]))) for p in range(3)]
    informe["independiente"] = True
    if incluir_sin_origen and len(sin):
        pos[0] = np.concatenate([pos[0], np.searchsorted(filas, sin)])
        informe["independiente"] = False
        informe["motivo"] = ("se sumaron %d ejemplos sin origen al entrenamiento: podrian ser de canciones "
                             "que estan en validacion o prueba" % len(sin))
    return pos, informe

def particiones_por_grupo(claves_trozo, origen_ejemplo, nivel="artista"):
    """El mismo reparto para trozos de WAV o sinteticos: claves_trozo[i] = (huella del archivo, huella de
    la carpeta o 0); origen_ejemplo[j] = trozo del que salio el ejemplo j."""
    import origen_dsee as O
    tipo = np.dtype([("primero", "<i8"), ("cuenta", "<i4"), ("cancion", "<u8"), ("carpeta", "<u8"), ("artista", "<u8")])
    unicas = sorted(set(claves_trozo))
    cual = {c: i for i, c in enumerate(unicas)}
    ent = np.zeros(len(unicas), dtype=tipo)
    for c, i in cual.items():
        ent[i]["cancion"], ent[i]["carpeta"] = c[0], c[1]
    grupo_trozo = np.array([cual[c] for c in claves_trozo], dtype=np.int64)
    grupo_ej = grupo_trozo[origen_ejemplo]
    ent["cuenta"] = np.bincount(grupo_ej, minlength=len(unicas))
    etiquetas, nivel_usado = O.grupos(ent, nivel)
    parte, motivo = O.reparte(etiquetas, ent["cuenta"])
    informe = {"independiente": not motivo, "motivo": motivo, "nivel": nivel_usado}
    if motivo:
        return None, informe
    parte_ej = parte[grupo_ej]
    informe["grupos"] = [int(len(np.unique(etiquetas[parte == p]))) for p in range(3)]
    return [np.where(parte_ej == p)[0] for p in range(3)], informe


def main():
    rng = np.random.default_rng(20260715)
    # Si una publicacion anterior se corto a medias, las copias se rehacen desde la version activa ANTES
    # de nada: asi el "modelo instalado" con el que se compara es de verdad el publicado.
    import publicar_dsee
    rec = publicar_dsee.recuperar(os.path.dirname(os.path.abspath(__file__)))
    if rec["limpiadas"] or rec["rehechas"] or rec["aviso"]:
        print("Publicacion anterior reparada: %d copias rehechas, %d a medias quitadas. %s"
              % (rec["rehechas"], rec["limpiadas"], rec["aviso"]))
    nivel = sys.argv[sys.argv.index("--agrupar") + 1] if "--agrupar" in sys.argv else "artista"
    if nivel not in ("artista", "carpeta", "cancion"):
        raise SystemExit("--agrupar admite artista, carpeta o cancion")
    sin_origen = "--incluir-sin-origen" in sys.argv
    if "--datos" in sys.argv:
        ruta = sys.argv[sys.argv.index("--datos") + 1]
        tope = int(sys.argv[sys.argv.index("--max-ejemplos") + 1]) if "--max-ejemplos" in sys.argv else 4000000
        print("Leyendo el material que junto la rocola:", ruta)
        X, Y = cargar_datos_rocola(ruta, tope, por_indices=True)
        print("  %d ejemplos de %d entradas -> %d salidas" % (X.shape[0], X.shape[1], Y.shape[1]))
        partes, informe = particiones_rocola(ruta, X, nivel, sin_origen)
        transitorio = None                                   # cuadros sueltos: no se sabe cuales son ataques
        import origen_dsee as O
        origen_datos = {"tipo": "rocola", "huella": O.huella_datos(ruta, O.ruta_indice(ruta)),
                        "ejemplos_usados": int(len(X)), "max_ejemplos": int(tope)}
    else:
        if "--musica" in sys.argv:
            folder = sys.argv[sys.argv.index("--musica") + 1]
            nclips = int(sys.argv[sys.argv.index("--clips") + 1]) if "--clips" in sys.argv else 400
            porarch = int(sys.argv[sys.argv.index("--por_archivo") + 1]) if "--por_archivo" in sys.argv else 0
            print("Cargando WAVs de:", folder)
            clips, claves = load_wavs(folder, rng, max_clips=nclips, max_per_file=porarch)
            if len(set(claves)) < 20:
                print("Muy pocos WAV 44.1k/16-bit utilizables (%d archivos). Convierte con ffmpeg y reintenta." % len(set(claves))); return 1
            print("  %d senales de un canal de %d archivos" % (len(clips), len(set(claves))))
            h = hashlib.sha256()
            for fn in sorted(f for f in os.listdir(folder) if f.lower().endswith((".wav", ".tsv"))):
                h.update(("%s:%d\n" % (fn.lower(), os.path.getsize(os.path.join(folder, fn)))).encode("utf-8"))
            origen_datos = {"tipo": "wav", "huella": h.hexdigest(), "archivos": len(set(claves)), "senales": len(clips)}
        else:
            print("Generando audio SINTETICO variado (prueba de concepto; para produccion usa --musica)...")
            nclips = int(sys.argv[sys.argv.index("--clips") + 1]) if "--clips" in sys.argv else 320
            clips = [synth_clip(rng) for _ in range(nclips)]
            claves = [(i + 1, 0) for i in range(len(clips))]       # cada trozo sintetico es independiente
            origen_datos = {"tipo": "sintetico", "trozos": len(clips)}
        print("Extrayendo pares (forma espectral -> envolvente real)...")
        X, Y, origen, transitorio = build_dataset_detalle(clips, rng)
        if len(X) == 0:
            print("No se extrajeron ejemplos utilizables; no se guarda ningun modelo.")
            return 1
        print("  %d ejemplos de %d entradas -> %d salidas" % (X.shape[0], X.shape[1], Y.shape[1]))
        partes, informe = particiones_por_grupo(claves, origen, nivel)
    if len(X) <= 500:
        print("Se requieren mas de 500 ejemplos para entrenar, validar y probar. No se guarda ningun modelo.")
        return 1
    if partes is None:
        partes, _ = _reparto_legado(len(X), rng, informe["motivo"])
    tr, va, pr = partes
    print("  reparto por %s: entrenamiento %d, validacion %d, prueba %d ejemplos%s"
          % (informe.get("nivel", "?"), len(tr), len(va), len(pr),
             ("" if "grupos" not in informe else " (%d / %d / %d grupos)" % tuple(informe["grupos"]))))
    if informe.get("sin_origen"):
        print("  %d ejemplos sin origen conocido quedan FUERA (no se sabe de que cancion son)" % informe["sin_origen"])
    if not informe["independiente"]:
        print("  [!] VALIDACION NO INDEPENDIENTE: " + informe["motivo"] + ". Se entrena para poder mirar, pero no se publicara.")
    Xtr, Ytr = _toma(X, tr), _toma(Y, tr)
    Xv, Yv = _toma(X, va), _toma(Y, va)
    Xp, Yp = _toma(X, pr), _toma(Y, pr)

    capas = [28, 48, 48, 12]
    if "--capas" in sys.argv:
        capas = [int(v) for v in sys.argv[sys.argv.index("--capas") + 1].split(",")]
    # La rocola solo acepta hasta 8 matrices de pesos y 512 neuronas por capa (ver NeuralEnv).
    if capas[0] != 28 or capas[-1] != 12:
        raise SystemExit("Las capas deben empezar en 28 y terminar en 12 (contrato de la rocola).")
    if len(capas) - 1 > 8 or max(capas) > 512 or min(capas) < 1:
        raise SystemExit("Demasiado grande para la rocola: maximo 8 capas de pesos y 512 neuronas.")
    epocas = int(sys.argv[sys.argv.index("--epocas") + 1]) if "--epocas" in sys.argv else 60
    print("Entrenando el MLP %s ..." % "->".join(str(c) for c in capas))
    model = train(Mlp(capas, rng), Xtr, Ytr, Xv, Yv, epochs=epocas)   # la validacion SOLO elige el checkpoint
    from calidad_dsee import (cuantizar, evaluar_tres, decide, cargar_ria1, bytes_ria1, escribir_atomico, POLITICA,
                              paridad_inferencia, errores_por_clase, avisos_por_clase, LIMITE_PARIDAD)
    cuantizar(model)

    # El modelo INSTALADO, leido como lo leeria la rocola. Por defecto, el que hay en esta carpeta.
    here = os.path.dirname(os.path.abspath(__file__))
    ruta_inst = sys.argv[sys.argv.index("--instalado") + 1] if "--instalado" in sys.argv else os.path.join(here, "rocola_ia.bin")
    instalado, estado_inst, huella_inst = None, "ausente: se compara solo con la curva fija", None
    if os.path.exists(ruta_inst):
        with open(ruta_inst, "rb") as f:
            huella_inst = hashlib.sha256(f.read()).hexdigest()
        try:
            instalado = cargar_ria1(ruta_inst)
            estado_inst = "comparado"
        except ValueError as e:
            estado_inst = "invalido (%s): la rocola no lo cargaria y usa la curva fija" % e

    val = evaluar_tres(model, instalado, Xv, Yv)
    err = evaluar_tres(model, instalado, Xp, Yp)          # la decision, con la particion de PRUEBA
    publicar, motivos = decide(err)
    # Lo que calcularia la ROCOLA con este modelo (float32, en su mismo orden): si no coincide con lo que
    # se acaba de evaluar, lo evaluado no es lo que va a sonar, y no se publica.
    paridad = paridad_inferencia(model, Xp)
    if paridad["no_finitas"] or not paridad["max_abs"] <= LIMITE_PARIDAD:
        publicar = False
        motivos = motivos + ["la rocola no calcularia lo mismo: diferencia %.3g (limite %g), %d filas no finitas"
                             % (paridad["max_abs"], LIMITE_PARIDAD, paridad["no_finitas"])]
    por_clase = errores_por_clase(model, instalado, Xp, Yp, None if transitorio is None else transitorio[pr])
    avisos = avisos_por_clase(por_clase)
    mse_net, mse_base = err["candidato"].mean(), err["referencia"].mean()
    print("VALIDACION (eligio el checkpoint, epoca %s): red = %.4f  |  curva fija = %.4f"
          % (getattr(model, "mejor_epoca", "?"), val["candidato"].mean(), val["referencia"].mean()))
    print("RESULTADO  mse-prueba: red = %.4f  |  curva fija = %.4f  (mejora %.1fx)" % (mse_net, mse_base, mse_base / max(mse_net, 1e-9)))
    if instalado is not None:
        print("           modelo instalado = %.4f  (%d parametros; el candidato tiene %d)"
              % (err["instalado"].mean(), instalado.parametros(), sum(w.size + b.size for w, b in zip(model.W, model.B))))
    else:
        print("           modelo instalado: " + estado_inst)
    print("  MSE por banda (prueba): red", err["candidato"], "| referencia", err["referencia"])
    for m in motivos:
        print("  " + m)
    print("  paridad con la rocola: %.2g en %d filas de prueba" % (paridad["max_abs"], paridad["filas"]))
    for a in avisos:
        print("  [!] " + a + " -> escucha ese tipo de musica en el A/B")

    if not informe["independiente"]:
        codigo = SALIDA_NO_VERIFICABLE
        motivos = motivos + ["sin validacion independiente: " + informe["motivo"]]
    else:
        codigo = 0 if publicar else 2
    modelo_bytes = bytes_ria1(model)
    rep = informe_reproducible(informe, capas, model, val, err, instalado, estado_inst, huella_inst,
                               codigo, motivos, modelo_bytes, (tr, va, pr), origen_datos, POLITICA,
                               paridad, por_clase, avisos, here)
    ruta_rep = sys.argv[sys.argv.index("--informe") + 1] if "--informe" in sys.argv else os.path.join(here, "informe_dsee.json")
    escribir_atomico(ruta_rep, lambda f: f.write(rep))
    print("Informe reproducible:", os.path.basename(ruta_rep))

    if codigo == SALIDA_NO_VERIFICABLE:
        print("NO se guarda: sin validacion independiente no se puede verificar la mejora. Se conservan modelo y vectores anteriores.")
        return codigo
    if codigo == 2:
        print("NO se guarda: no supera la politica (curva fija y modelo instalado). Se conservan modelo y vectores anteriores.")
        return 2
    vectors = vectores_de_paridad(model, Xp, modelo_bytes)
    # Modelo, vectores e informe se publican como UNA version (ver publicar_dsee.py): un corte de luz a
    # media publicacion deja el conjunto viejo o el nuevo, nunca una mezcla. rocola_ia.bin y los vectores
    # de esta carpeta son copias de la version activa.
    import publicar_dsee
    version = publicar_dsee.publicar(here, {"rocola_ia.bin": modelo_bytes,
                                            "ia_vectores_prueba.txt": vectors.encode("ascii"),
                                            "informe_dsee.json": rep}, EXTRACTOR)
    print("Listo: version %s publicada. rocola_ia.bin (copialo junto a Rocola.exe) + ia_vectores_prueba.txt" % version)
    return 0


def vectores_de_paridad(model, Xp, modelo_bytes, n=24):
    """24 entradas de la particion de PRUEBA y lo que responde el modelo ya en float32. La primera linea
    lleva la huella del modelo: asi se sabe si los vectores y el modelo que tienen al lado van juntos
    (dsee.cs ignora las lineas sin '|')."""
    P = model.predict(Xp[:n])
    cab = "# rocola_ia.bin sha256=%s extractor=%d origen=prueba\n" % (hashlib.sha256(modelo_bytes).hexdigest(), EXTRACTOR)
    return cab + "".join(" ".join("%.9g" % v for v in Xp[i]) + " | " + " ".join("%.9g" % v for v in P[i]) + "\n"
                         for i in range(min(n, len(Xp))))


def _sha_de(carpeta, nombre):
    try:
        with open(os.path.join(carpeta, nombre), "rb") as f:
            return hashlib.sha256(f.read()).hexdigest()
    except (OSError, TypeError):
        return None


def _num(v):
    return [float(repr(float(x))) for x in np.asarray(v).ravel()] if np.ndim(v) else float(v)


def informe_reproducible(informe, capas, model, val, err, instalado, estado_inst, huella_inst,
                         codigo, motivos, modelo_bytes, partes, origen_datos, politica,
                         paridad=None, por_clase=None, avisos=(), here=None):
    """El informe: la misma entrada da el MISMO archivo, byte a byte. Sin fechas, sin rutas ni nombres."""
    rep = {
        "formato": "ROCOLA-INFORME-DSEE 1",
        "extractor": EXTRACTOR,
        "contrato_ria1": {"entradas": 28, "salidas": 12},
        "semilla": 20260715,
        "datos": origen_datos,
        "reparto": {"independiente": bool(informe["independiente"]), "motivo": informe.get("motivo", ""),
                    "nivel": informe.get("nivel", ""), "grupos": informe.get("grupos"),
                    "ejemplos": [int(len(p)) for p in partes], "sin_origen": int(informe.get("sin_origen", 0))},
        "capas": [int(c) for c in capas],
        "parametros": int(sum(w.size + b.size for w, b in zip(model.W, model.B))),
        "checkpoint": {"epoca": getattr(model, "mejor_epoca", None),
                       "mse_validacion": _num(getattr(model, "mejor_mse_validacion", float("nan")))},
        "validacion": {k: _num(v) for k, v in sorted(val.items())},
        "prueba": {k: _num(v) for k, v in sorted(err.items())},
        "instalado": {"estado": estado_inst, "sha256": huella_inst,
                      "parametros": instalado.parametros() if instalado is not None else None},
        "politica": politica,
        "decision": {"codigo": int(codigo), "publicar": codigo == 0, "motivos": list(motivos)},
        "candidato_sha256": hashlib.sha256(modelo_bytes).hexdigest(),
        "paridad_inferencia": None if paridad is None else {k: _num(v) if isinstance(v, float) else v
                                                          for k, v in sorted(paridad.items())},
        "paridad_extractor": {"extractor": EXTRACTOR, "vectores_sha256": _sha_de(here, "paridad_extractor.txt")},
        "por_clase": por_clase,
        "avisos_por_clase": list(avisos),
    }
    return (json.dumps(rep, sort_keys=True, indent=1, ensure_ascii=True, allow_nan=True) + "\n").encode("ascii")

if __name__ == "__main__":
    sys.exit(main())
