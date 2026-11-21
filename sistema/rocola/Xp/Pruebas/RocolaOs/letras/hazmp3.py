#!/usr/bin/env python3
# hazmp3.py SALIDA.mp3 SEGUNDOS TITULO ARTISTA DISCO [LETRA|-] : un MP3 INVENTADO (un tono, con ffmpeg) con su ID3v2.3 delante: TIT2, TPE1,
# TALB y, si se da, USLT con esa letra (inventada; en UTF-16 con BOM, como la escriben los programas de etiquetas). Solo para las pruebas.
import struct, subprocess, sys, tempfile, os
sal, seg, tit, art, dis = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5]
letra = sys.argv[6] if len(sys.argv) > 6 and sys.argv[6] != "-" else None
def marco(id, datos): return id.encode() + struct.pack(">I", len(datos)) + b"\0\0" + datos
def texto(s): return b"\x01" + b"\xff\xfe" + s.encode("utf-16-le") + b"\0\0"
fr = marco("TIT2", texto(tit)) + marco("TPE1", texto(art)) + marco("TALB", texto(dis))
if letra is not None:
    fr += marco("USLT", b"\x01" + b"spa" + b"\xff\xfe\0\0" + b"\xff\xfe" + letra.replace("\\n", "\n").encode("utf-16-le"))
n = len(fr) + 64
cab = b"ID3\x03\x00\x00" + bytes([(n >> 21) & 0x7f, (n >> 14) & 0x7f, (n >> 7) & 0x7f, n & 0x7f])
with tempfile.TemporaryDirectory() as d:
    t = os.path.join(d, "a.mp3")
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=" + seg, "-ac", "2", "-ar", "44100",
                    "-c:a", "libmp3lame", "-b:a", "128k", "-id3v2_version", "0", "-write_xing", "0", t], check=True)
    audio = open(t, "rb").read()
open(sal, "wb").write(cab + fr + b"\0" * 64 + audio)
