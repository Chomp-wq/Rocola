# desaloja.py CARPETA... : saca de la cache del sistema (posix_fadvise DONTNEED) los archivos de esas carpetas; solo los nuestros
import os, sys
n = 0
for raiz in sys.argv[1:]:
    for d, _, fs in os.walk(raiz, followlinks=True):
        for f in fs:
            try:
                fd = os.open(os.path.join(d, f), os.O_RDONLY)
                os.posix_fadvise(fd, 0, 0, os.POSIX_FADV_DONTNEED); os.close(fd); n += 1
            except OSError: pass
print("desalojados", n)
