# Genera pruebas/flotante_overs.mp3: 3 s de una mezcla SINTETICA (no es musica de nadie) masterizada "a lo bruto":
# empujada +8 dB y recortada en seco a 0 dBFS, como los discos de la guerra del volumen, y pasada a MP3 con LAME a
# 128 kbps. Al descomprimirla, el MP3 trae picos por encima del maximo digital: lo que la bateria flotante busca.
# Uso: python3 genera_flotante_overs.py  (necesita NumPy y FFmpeg con libmp3lame)
import numpy as np, subprocess, wave, os, sys
fs = 44100; seg = 3; n = fs * seg
rng = np.random.default_rng(20260923)
t = np.arange(n) / fs
mix = np.zeros((n, 2))
def env(dur, tau):
    k = np.arange(int(dur * fs)) / fs
    return np.exp(-k / tau)
for b in np.arange(0, seg, 0.5):                     # bombo
    i = int(b * fs); e = env(0.35, 0.08); m = min(len(e), n - i)
    f = 50 + 60 * np.exp(-np.arange(m) / fs / 0.03)
    s = np.sin(2 * np.pi * np.cumsum(f) / fs) * e[:m] * 0.9
    mix[i:i+m] += s[:, None]
for b in np.arange(0.5, seg, 1.0):                   # caja
    i = int(b * fs); e = env(0.25, 0.06); m = min(len(e), n - i)
    s = rng.standard_normal(m) * e[:m] * 0.35
    mix[i:i+m, 0] += s; mix[i:i+m, 1] += s * 0.9
hh = rng.standard_normal(n); hh = hh - np.convolve(hh, np.ones(8) / 8, mode='same')
for b in np.arange(0, seg, 0.25):                    # charles
    i = int(b * fs); e = env(0.06, 0.015); m = min(len(e), n - i)
    mix[i:i+m, 0] += hh[i:i+m] * e[:m] * 0.25; mix[i:i+m, 1] += hh[i:i+m] * e[:m] * 0.18
for j, f0 in enumerate((261.6, 329.6, 392.0)):       # acorde
    v = sum(np.sin(2 * np.pi * f0 * h * t) / (h * h) for h in range(1, 6)) * 0.12
    mix[:, 0] += v * (1.0 if j != 2 else 0.6); mix[:, 1] += v * (1.0 if j != 0 else 0.6)
mix = mix / np.max(np.abs(mix)) * 10 ** (8 / 20)     # +8 dB...
mix = np.clip(mix, -1.0, 32767 / 32768)             # ...y recortado en seco
wav = 'flotante_overs.wav'
w = wave.open(wav, 'wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(fs)
w.writeframes(np.round(mix * 32767).astype('<i2').tobytes()); w.close()
salida = sys.argv[1] if len(sys.argv) > 1 else 'flotante_overs.mp3'
subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', wav, '-c:a', 'libmp3lame', '-b:a', '128k',
                '-map_metadata', '-1', '-id3v2_version', '0', '-write_xing', '0', salida], check=True)
os.remove(wav)
print(salida, os.path.getsize(salida), 'bytes')
