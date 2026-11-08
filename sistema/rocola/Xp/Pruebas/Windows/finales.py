#!/usr/bin/env python3
# finales.py REV ARCHIVO... : devuelve a cada linea del archivo el fin de linea (CRLF o LF) que tenia en la revision REV (las lineas iguales,
# el suyo; las cambiadas, el de la linea que sustituyen; las nuevas, el de la anterior). Para no cambiar de golpe los finales de un archivo
# al editarlo con herramientas que los normalizan. Tambien el CR CR LF que tienen unas pocas lineas de XP (un editor que lo lee en modo texto
# lo parte en la linea y una vacia: esa vacia se quita).
import sys, subprocess, difflib
rev = sys.argv[1]
for ruta in sys.argv[2:]:
    raiz = subprocess.run(['git', 'rev-parse', '--show-toplevel'], capture_output=True, text=True).stdout.strip()
    rel = subprocess.run(['git', 'ls-files', '--full-name', ruta], capture_output=True, text=True).stdout.strip() or ruta
    orig = subprocess.run(['git', 'show', rev + ':' + rel], capture_output=True).stdout
    ahora = open(ruta, 'rb').read()
    def partes(b):
        ls = b.split(b'\n')
        fin = []
        out = []
        for i, l in enumerate(ls):
            if i == len(ls) - 1:
                if l == b'': break
                out.append(l.rstrip(b'\r')); fin.append(b'')   # sin fin de linea al final
            else:
                if l.endswith(b'\r\r'): out.append(l[:-2]); fin.append(b'\r\r\n')
                elif l.endswith(b'\r'): out.append(l[:-1]); fin.append(b'\r\n')
                else: out.append(l); fin.append(b'\n')
        return out, fin
    o, of = partes(orig)
    a, af = partes(ahora)
    sm = difflib.SequenceMatcher(None, o, a, autojunk=False)
    nf = [None] * len(a)
    quita = set()
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == 'insert' and i1 > 0 and of[i1 - 1] == b'\r\r\n' and a[j1] == b'': quita.add(j1)   # (la vacia del CR CR LF partido)
        if tag == 'equal':
            for k in range(j2 - j1): nf[j1 + k] = of[i1 + k]
        elif tag == 'replace':
            for k in range(j2 - j1): nf[j1 + k] = of[min(i1 + k, i2 - 1)]
        elif tag == 'insert':
            for k in range(j2 - j1): nf[j1 + k] = of[i1 - 1] if i1 > 0 else (of[0] if of else b'\n')
    for k in range(len(a)):
        if nf[k] is None: nf[k] = b'\n'
    if af and af[-1] == b'': nf[-1] = b''
    res = b''.join(l + f for k, (l, f) in enumerate(zip(a, nf)) if k not in quita)
    open(ruta, 'wb').write(res)
    print(ruta, 'CRLF', res.count(b'\r\n'), 'lineas', len(a) - len(quita))
