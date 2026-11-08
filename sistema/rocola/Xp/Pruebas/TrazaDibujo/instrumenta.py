# INSTRUMENTA UNA COPIA DE Dibujo2D.cs (la de la referencia congelada, copiada en ref-instrumentada): a los metodos publicos de Graphics,
# TextRenderer, Bitmap, Image, GraphicsPath.AddString y a los constructores de Font les anade [CallerFilePath]/[CallerLineNumber] y una
# llamada a Traza.Op con la operacion, los argumentos y (si hay Graphics) su estado. El codigo de XP no se toca.
import re, sys
ruta = sys.argv[1]
src = open(ruta, encoding='utf-8').read()
lineas = src.split('\n')
clase = None
salida = []
CLASES = {'Graphics', 'TextRenderer', 'Bitmap', 'Image', 'GraphicsPath', 'Font'}
SALTA = {'Dispose', 'Flush', 'GetBounds', 'Clone', 'IsVisible', 'Transform', 'Reset', 'CloseFigure', 'CloseAllFigures', 'StartFigure',
         'UnlockBits', 'GetHeight', 'AddLine', 'AddLines', 'AddArc', 'AddBezier', 'AddRectangle', 'AddEllipse', 'AddPolygon', 'AddPie', 'AddPath', 'IsAlphaPixelFormat'}
firma = re.compile(r'^(\s*)public ((?:static |unsafe )*)(?:([\w\[\]<>?.]+) )?(\w+)\((.*?)\)(\s*(?::\s*this\(.*?\))?\s*)(\{.*)?$')
n = 0
for i, l in enumerate(lineas):
    m = re.match(r'^\s*public (?:sealed |abstract |static )*class (\w+)', l)
    if m:
        clase = m.group(1)
    mm = firma.match(l)
    if clase in CLASES and mm and '=>' not in l:
        ind, est, tipo, nombre, params, cola, cuerpo = mm.groups()
        es_ctor = tipo is None and nombre == clase
        if nombre in SALTA or (tipo is None and not es_ctor):
            salida.append(l); continue
        if clase == 'GraphicsPath' and nombre != 'AddString':
            salida.append(l); continue
        if clase == 'Font' and not es_ctor:
            salida.append(l); continue
        if clase == 'Bitmap' and es_ctor and 'SKBitmap' in params:
            salida.append(l); continue
        if cuerpo is None:
            # el cuerpo empieza en la linea siguiente
            if i + 1 < len(lineas) and lineas[i + 1].strip() == '{':
                pass
            else:
                salida.append(l); continue
        # los argumentos que se apuntan (sin los out)
        args = []
        for p in [x.strip() for x in params.split(',') if x.strip()]:
            if p.startswith('out '):
                continue
            args.append(p.split()[-1])
        nuevos = (params + ', ' if params.strip() else '') + '[System.Runtime.CompilerServices.CallerFilePath] string cf_ = "", [System.Runtime.CompilerServices.CallerLineNumber] int cl_ = 0'
        g = 'this' if (clase == 'Graphics' and 'static' not in (est or '')) else ('g' if clase == 'TextRenderer' and 'Graphics g' in params else 'null')
        lamada = 'Traza.Op(%s, "%s.%s", cf_, cl_%s);' % (g, clase, nombre, ''.join(', ' + a for a in args))
        if es_ctor:
            lamada = 'Traza.Op(null, "%s.ctor", cf_, cl_%s);' % (clase, ''.join(', ' + a for a in args))
        cab = '%spublic %s%s%s(%s)%s' % (ind, est or '', (tipo + ' ') if tipo else '', nombre, nuevos, cola)
        if cuerpo is not None:
            salida.append(cab + '{ ' + lamada + cuerpo[1:])
        else:
            salida.append(cab.rstrip())
            lineas[i + 1] = lineas[i + 1].replace('{', '{ ' + lamada, 1)
        n += 1
        continue
    salida.append(l)
open(ruta, 'w', encoding='utf-8').write('\n'.join(salida))
print('metodos instrumentados:', n)
