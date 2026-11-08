# Lee la tuberia donde escribe el complemento "file" de ALSA (FORMAT=wav) al ritmo de su cabecera: asi la rocola suena a tiempo real
# sin tarjeta de sonido (con la salida nula de ALSA una cancion se acaba en un instante).
import struct, sys, time
fifo = sys.argv[1]
while True:
    with open(fifo, 'rb', buffering=0) as f:
        cab = b''
        while len(cab) < 44:
            b = f.read(44 - len(cab))
            if not b: break
            cab += b
        if len(cab) < 44: continue
        ritmo = struct.unpack('<I', cab[28:32])[0] or 176400
        t0 = time.monotonic(); total = 0
        while True:
            b = f.read(8192)
            if not b: break
            total += len(b)
            espera = t0 + total / ritmo - time.monotonic()
            if espera > 0: time.sleep(espera)
