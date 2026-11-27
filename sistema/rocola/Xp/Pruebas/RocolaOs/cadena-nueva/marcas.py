#!/usr/bin/env python3
# marcas.py MP3_LAME SALIDA_ITUNES SALIDA_VBRI: desde un MP3 de LAME (con su cuadro Info/Xing) arma uno con iTunSMPB (sin el cuadro de LAME,
# con un COMM «iTunSMPB» en una etiqueta ID3v2.3 con el mismo retraso, relleno y muestras) y otro con VBRI (el cuadro de LAME cambiado por uno
# VBRI de Fraunhofer con el mismo retraso y los mismos cuadros: VBRI no dice el relleno). Para la parte «sinhuecos» de prueba.sh.
import sys, struct
src, itunes, vbri = sys.argv[1:4]
d = open(src, 'rb').read()
i = 0
if d[:3] == b'ID3':
    sz = (d[6] & 0x7f) << 21 | (d[7] & 0x7f) << 14 | (d[8] & 0x7f) << 7 | (d[9] & 0x7f)
    i = 10 + sz + (10 if d[5] & 0x10 else 0)
h = d[i:i + 4]
assert h[0] == 0xFF and (h[1] & 0xE0) == 0xE0
ver = (h[1] >> 3) & 3
br_idx = h[2] >> 4; sr_idx = (h[2] >> 2) & 3; pad = (h[2] >> 1) & 1
mono = ((h[3] >> 6) & 3) == 3
br = ([0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320] if ver == 3 else [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160])[br_idx]
sr = [44100, 48000, 32000][sr_idx] // (1 if ver == 3 else 2 if ver == 2 else 4)
spf = 1152 if ver == 3 else 576
flen = (144000 if ver == 3 else 72000) * br // sr + pad
side = (17 if mono else 32) if ver == 3 else (9 if mono else 17)
x = i + 4 + side
assert d[x:x + 4] in (b'Info', b'Xing'), d[x:x + 4]
flags = struct.unpack('>I', d[x + 4:x + 8])[0]; p = x + 8
frames = struct.unpack('>I', d[p:p + 4])[0]; p += 4
if flags & 2: p += 4
if flags & 4: p += 100
if flags & 8: p += 4
delay = (d[p + 21] << 4) | (d[p + 22] >> 4); padding = ((d[p + 22] & 0x0F) << 8) | d[p + 23]
samples = frames * spf - delay - padding
resto = d[i + flen:]
txt = (' 00000000 %08X %08X %016X 00000000 00000000 00000000 00000000 00000000 00000000 00000000 00000000' % (delay, padding, samples)).encode('latin-1')
comm = b'\x00' + b'eng' + b'iTunSMPB\x00' + txt
fr = b'COMM' + struct.pack('>I', len(comm)) + b'\x00\x00' + comm
size = len(fr)
ss = bytes([(size >> 21) & 0x7f, (size >> 14) & 0x7f, (size >> 7) & 0x7f, size & 0x7f])
open(itunes, 'wb').write(b'ID3\x03\x00\x00' + ss + fr + resto)
vf = bytearray(flen); vf[0:4] = h
vh = b'VBRI' + struct.pack('>HHHII', 1, delay, 75, len(resto), frames) + struct.pack('>HHHH', 0, 1, 2, 1)
vf[36:36 + len(vh)] = vh
open(vbri, 'wb').write(bytes(vf) + resto)
print('MARCAS delay=%d padding=%d frames=%d samples=%d flen=%d' % (delay, padding, frames, samples, flen))
