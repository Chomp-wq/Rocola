#!/usr/bin/env python3
# teclas.py SEGUNDO PASO TEXTO: lo que hay que poner en ROCOLA_PULSA para escribir TEXTO (teclado de EE. UU.) desde el segundo SEGUNDO,
# una tecla cada PASO segundos (con Mayus: 16+VK). Solo letras, cifras y los signos del teclado de EE. UU.
import sys
s, paso, texto = float(sys.argv[1]), float(sys.argv[2]), sys.argv[3]
sin = {'-': 189, '=': 187, '[': 219, ']': 221, '\\': 220, ';': 186, "'": 222, ',': 188, '.': 190, '/': 191, '`': 192, ' ': 32}
con = {'_': 189, '+': 187, '{': 219, '}': 221, '|': 220, ':': 186, '"': 222, '<': 188, '>': 190, '?': 191, '~': 192,
       ')': 48, '!': 49, '@': 50, '#': 51, '$': 52, '%': 53, '^': 54, '&': 55, '*': 56, '(': 57}
out = []
for i, c in enumerate(texto):
    t = "%.2f" % (s + i * paso)
    if c.isdigit(): out.append("%s:%d" % (t, ord(c)))
    elif 'a' <= c <= 'z': out.append("%s:%d" % (t, ord(c.upper())))
    elif 'A' <= c <= 'Z': out.append("%s:16+%d" % (t, ord(c)))
    elif c in sin: out.append("%s:%d" % (t, sin[c]))
    elif c in con: out.append("%s:16+%d" % (t, con[c]))
    else: raise SystemExit("no se escribir %r" % c)
print(",".join(out))
