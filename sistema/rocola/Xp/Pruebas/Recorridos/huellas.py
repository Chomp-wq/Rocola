# Las huellas de lo que dejo un recorrido (recorridos.sh con VOLCADO=1 y CAPAS=1) y fuera los PNG (ocupan mucho): por cada textura subida a
# la tarjeta (tex/NNNN_T.Ts_WxH_nID_enX,Y_BWxBH.png) su medida y sitio y la huella de sus puntos; por cada capa de Dibujo2D de cada captura
# (cS.png.capaN-dibujo2d-X,Y-WxH.png; la de una hoja nativa, capaN-hoja-...) su nombre y su huella. Las capturas enteras y las capas de la tarjeta llevan el cielo animado: se
# apunta su huella pero no se comparan.
import hashlib, os, re, sys
for d in sys.argv[1:]:
    filas = []
    t = os.path.join(d, 'tex')
    if os.path.isdir(t):
        for f in sorted(os.listdir(t)):
            m = re.match(r'(\d+)_([\d.]+)s_(\d+x\d+)_n\d+_en([\d,]+)_(\d+x\d+)\.png$', f)
            if not m: continue
            h = hashlib.md5(open(os.path.join(t, f), 'rb').read()).hexdigest()[:16]
            filas.append('textura\t%s_en%s_%s\t%s\t%s' % (m.group(3), m.group(4), m.group(5), h, m.group(2)))
            os.remove(os.path.join(t, f))
        os.rmdir(t)
    for f in sorted(os.listdir(d)):
        if not f.endswith('.png'): continue
        h = hashlib.md5(open(os.path.join(d, f), 'rb').read()).hexdigest()[:16]
        # (ventanas2: la capa de una hoja nativa se guarda como «-hoja-»; es la misma capa que en la referencia sale como «-dibujo2d-» y se
        # compara con ella, con su nombre)
        tipo = 'dibujo2d' if '-dibujo2d-' in f or '-hoja-' in f else 'tarjeta' if '-tarjeta' in f else 'pantalla'
        filas.append('%s\t%s\t%s\t-' % (tipo, f.replace('-hoja-', '-dibujo2d-'), h))
        os.remove(os.path.join(d, f))
    open(os.path.join(d, 'huellas.tsv'), 'w').write('\n'.join(filas) + '\n')
