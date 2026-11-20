# mide_wav.py [--float] WAV...: los niveles de 50 Hz y 1 kHz (y su diferencia) en tramos de 2 s con sonido. --float: los datos son float32
# aunque la cabecera diga s32 (lo que escribe el plugin «file» de ALSA con lo que le manda mpv: su cabecera no es la de los datos).
import sys, subprocess, wave, os, tempfile, numpy as np
flt = '--float' in sys.argv
for p in [a for a in sys.argv[1:] if a != '--float']:
    if flt:
        b = open(p, 'rb').read(); i = b.find(b'data'); n = int.from_bytes(b[i + 4:i + 8], 'little')
        x = np.frombuffer(b[i + 8:i + 8 + n], dtype=np.float32).astype(float).reshape(-1, 2)[:, 0]; fs = 44100
    else:
        t = tempfile.mktemp(suffix='.wav'); subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-i', p, '-c:a', 'pcm_s32le', t], check=True)
        w = wave.open(t); fs = w.getframerate(); x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int32).astype(float).reshape(-1, w.getnchannels())[:, 0] / 2**31
        w.close(); os.unlink(t)
    L = 2 * fs; difs = []; n1k = []; n50 = []
    for i in range(0, len(x) - L, L):
        s = x[i:i + L]
        if np.sqrt(np.mean(s * s)) < 1e-5: continue
        f = np.fft.rfft(s * np.hanning(L)); fr = np.fft.rfftfreq(L, 1 / fs)
        bd = lambda f0: 10 * np.log10(np.sum(np.abs(f[(fr > f0 * 0.9) & (fr < f0 * 1.1)]) ** 2) + 1e-30)
        n50.append(bd(50)); n1k.append(bd(1000)); difs.append(bd(50) - bd(1000))
    if not difs: print(p, "sin sonido"); continue
    print("%s: %d tramos; 50 Hz - 1 kHz: mediana %.2f dB (de %.2f a %.2f); 1 kHz mediana %.2f dB; 50 Hz mediana %.2f dB" % (
        '/'.join(p.split('/')[-2:]), len(difs), np.median(difs), min(difs), max(difs), np.median(n1k), np.median(n50)))
