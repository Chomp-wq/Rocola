#!/usr/bin/env python3
# Genera MP3 de prueba VALIDOS con duracion EXACTA conocida, sin necesitar un codificador.
#
# Un MP3 es una tira de "frames", y cada frame arranca con una cabecera de 4 bytes que dice a que tasa y a
# que frecuencia va. El contenido de sonido puede ser relleno: lo que se prueba aqui es si el programa sabe
# leer la duracion de la cabecera en vez de recorrerse el archivo entero.
#
# Se generan los casos que de verdad aparecen en una biblioteca de bar:
#   - tasa fija (CBR), que se resuelve con una division
#   - tasa variable con etiqueta Xing / Info / VBRI, que trae la cuenta EXACTA de frames
#   - con etiqueta de texto delante (ID3v2), incluida una gorda con caratula dentro
#   - con etiqueta de texto detras (ID3v1)
#   - mono, otras frecuencias, MPEG2
#   - y archivos ROTOS, para comprobar que no revienta y que cae al camino lento
import os, struct, sys

BR1 = [0,32,40,48,56,64,80,96,112,128,160,192,224,256,320,0]
BR2 = [0,8,16,24,32,40,48,56,64,80,96,112,128,144,160,0]
SR1 = [44100,48000,32000,0]
SR2 = [22050,24000,16000,0]

def cabecera(mpeg1, br_kbps, sr, mono, pad=0):
    ver = 3 if mpeg1 else 2
    tabla = BR1 if mpeg1 else BR2
    tsr = SR1 if mpeg1 else SR2
    ibr = tabla.index(br_kbps); isr = tsr.index(sr)
    modo = 3 if mono else 0
    b1 = 0xE0 | (ver << 3) | (1 << 1) | 1          # sync alto + version + capa III + sin CRC
    b2 = (ibr << 4) | (isr << 2) | (pad << 1)
    b3 = (modo << 6)
    return bytes([0xFF, b1, b2, b3])

def tam_frame(mpeg1, br_kbps, sr, pad=0):
    return (144 if mpeg1 else 72) * (br_kbps * 1000) // sr + pad

def marco(mid, texto):
    p = b'\x00' + texto.encode('latin-1') + b'\x00'
    return mid + struct.pack('>I', len(p)) + b'\x00\x00' + p

def marco_apic(n):
    # Un marco de CARATULA gordo: es justo lo que no hay que leer.
    p = b'\x00image/jpeg\x00\x03\x00' + (b'\xAB' * n)
    return b'APIC' + struct.pack('>I', len(p)) + b'\x00\x00' + p

def id3v2(tam_extra=0, genero=None, arte_antes=0, arte_despues=0):
    # Etiqueta de texto con un titulo y, si se pide, genero y una caratula delante o detras.
    cuerpo = b''
    if arte_antes: cuerpo += marco_apic(arte_antes)
    cuerpo += marco(b'TIT2', 'Cancion X')
    if genero: cuerpo += marco(b'TCON', genero)
    if arte_despues: cuerpo += marco_apic(arte_despues)
    cuerpo += (b'\x00' * tam_extra)
    n = len(cuerpo)
    ss = bytes([(n >> 21) & 0x7F, (n >> 14) & 0x7F, (n >> 7) & 0x7F, n & 0x7F])
    return b'ID3' + b'\x03\x00' + b'\x00' + ss + cuerpo

def id3v1():
    return b'TAG' + b'Titulo'.ljust(30, b'\x00') + b'Artista'.ljust(30, b'\x00') + \
           b'Album'.ljust(30, b'\x00') + b'2024' + b'Comentario'.ljust(30, b'\x00') + bytes([12])

def cuerpo_frames(mpeg1, br, sr, mono, nframes, xing=None, xing_marca=b'Xing', vbri=False):
    """xing = numero de frames a declarar en la etiqueta (None = sin etiqueta)."""
    out = bytearray()
    h = cabecera(mpeg1, br, sr, mono)
    tf = tam_frame(mpeg1, br, sr)
    lado = (17 if mono else 32) if mpeg1 else (9 if mono else 17)
    for k in range(nframes):
        f = bytearray(h) + bytearray(tf - 4)
        if k == 0 and xing is not None:
            if vbri:
                # VBRI va SIEMPRE a 32 bytes del inicio del frame
                pos = 4 + 32
                f[pos:pos+4] = b'VBRI'
                f[pos+14:pos+18] = struct.pack('>I', xing)
            else:
                pos = 4 + lado
                f[pos:pos+4] = xing_marca
                f[pos+4:pos+8] = struct.pack('>I', 1)          # banderas: trae cuenta de frames
                f[pos+8:pos+12] = struct.pack('>I', xing)
        out += f
    return bytes(out)

def dur(mpeg1, sr, nframes):
    return nframes * (1152 if mpeg1 else 576) / sr

def main():
    d = sys.argv[1] if len(sys.argv) > 1 else 'mp3s'
    os.makedirs(d, exist_ok=True)
    casos = []   # (nombre, bytes, duracion esperada, comentario)  -> el genero se deduce del nombre

    # tasa fija, lo mas comun
    n = 3800
    casos.append(('cbr128.mp3', cuerpo_frames(True,128,44100,False,n), dur(True,44100,n), 'tasa fija 128k estereo'))
    n = 1500
    casos.append(('cbr320.mp3', cuerpo_frames(True,320,44100,False,n), dur(True,44100,n), 'tasa fija 320k'))
    n = 2200
    casos.append(('cbr64mono.mp3', cuerpo_frames(True,64,44100,True,n), dur(True,44100,n), 'tasa fija 64k mono'))
    n = 900
    casos.append(('cbr48k.mp3', cuerpo_frames(True,192,48000,False,n), dur(True,48000,n), 'tasa fija a 48 kHz'))
    n = 1200
    casos.append(('mpeg2.mp3', cuerpo_frames(False,64,22050,False,n), dur(False,22050,n), 'MPEG2 a 22 kHz'))

    # con etiqueta Xing (tasa variable): la cuenta de frames es la verdad
    n = 4100
    casos.append(('xing.mp3', cuerpo_frames(True,128,44100,False,n,xing=n), dur(True,44100,n), 'con etiqueta Xing'))
    casos.append(('info.mp3', cuerpo_frames(True,128,44100,False,n,xing=n,xing_marca=b'Info'), dur(True,44100,n), 'con etiqueta Info'))
    casos.append(('vbri.mp3', cuerpo_frames(True,128,44100,False,n,xing=n,vbri=True), dur(True,44100,n), 'con etiqueta VBRI'))
    n = 2600
    casos.append(('xingmono.mp3', cuerpo_frames(True,64,44100,True,n,xing=n), dur(True,44100,n), 'Xing en mono'))

    # con etiquetas de texto delante y detras
    n = 3000
    cuerpo = cuerpo_frames(True,128,44100,False,n,xing=n)
    casos.append(('id3delante.mp3', id3v2(200) + cuerpo, dur(True,44100,n), 'con etiqueta de texto delante'))
    casos.append(('id3gorda.mp3', id3v2(220000) + cuerpo, dur(True,44100,n), 'con caratula gorda dentro (215 KB)'))
    casos.append(('id3ambas.mp3', id3v2(200) + cuerpo + id3v1(), dur(True,44100,n), 'etiquetas delante y detras'))
    cuerpo2 = cuerpo_frames(True,128,44100,False,n)
    casos.append(('cbr_id3ambas.mp3', id3v2(500) + cuerpo2 + id3v1(), dur(True,44100,n), 'tasa fija con las dos etiquetas'))
    # con basura entre la etiqueta y el audio (pasa con archivos manoseados)
    casos.append(('conbasura.mp3', id3v2(100) + b'\x00' * 37 + cuerpo, dur(True,44100,n), 'con relleno antes del audio'))

    # con genero y caratula, en los dos ordenes: el genero tiene que salir en los DOS
    n = 2000
    cu = cuerpo_frames(True,128,44100,False,n,xing=n)
    casos.append(('gen_antes.mp3', id3v2(genero='Rock', arte_despues=300000) + cu, dur(True,44100,n), 'genero ANTES de una caratula de 300 KB'))
    casos.append(('gen_despues.mp3', id3v2(genero='Cumbia', arte_antes=300000) + cu, dur(True,44100,n), 'genero DESPUES de una caratula de 300 KB'))
    casos.append(('gen_sinarte.mp3', id3v2(genero='Balada') + cu, dur(True,44100,n), 'genero sin caratula'))
    casos.append(('gen_ninguno.mp3', id3v2(arte_despues=120000) + cu, dur(True,44100,n), 'sin genero, con caratula'))

    # rotos: no debe reventar
    casos.append(('vacio.mp3', b'', 0.0, 'archivo vacio'))
    casos.append(('chico.mp3', b'\x00' * 50, 0.0, 'cincuenta bytes de nada'))
    casos.append(('nomp3.mp3', b'esto no es un mp3 ni de lejos' * 40, 0.0, 'no es un mp3'))
    casos.append(('solotag.mp3', id3v2(4000), 0.0, 'solo la etiqueta, sin audio'))

    with open(os.path.join(d, 'esperado.txt'), 'w') as idx:
        for nombre, datos, segundos, com in casos:
            with open(os.path.join(d, nombre), 'wb') as f:
                f.write(datos)
            idx.write('%s|%.6f|%d|%s\n' % (nombre, segundos, len(datos), com))
    print('generados %d archivos en %s' % (len(casos), d))

main()
