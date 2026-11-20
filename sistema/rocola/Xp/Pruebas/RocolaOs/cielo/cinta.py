#!/usr/bin/env python3
# LO QUE PINTA EL CIELO EN UNA CINTA DE LA TARJETA (ROCOLA_CINTA, Nativo/CintaTarjeta.cs), para comparar dos corridas que no van al mismo
# ritmo (el reloj y el azar de las estrellas y las fugaces no son los mismos): de cada cuadro grabado del dispositivo de la rocola,
#   - los degradados del cielo (DrawSky y DrawSkyN): sus vertices, byte a byte (son del color del genero y de la hora: deterministas);
#   - la forma del cuadro: la lista de llamadas (sin las fugaces, DrawShootStreaks, que salen al azar) con cuantos vertices manda cada dibujo
#     de malla o de cielo, y las texturas que se suben.
#   cinta.py resume CINTA                      -> el resumen (JSON) en la salida
#   cinta.py compara CINTA_A CINTA_B          -> IGUALES o lo distinto; sale con 0 si iguales
# Necesita brotli (pip install --target DIR brotli; PYTHONPATH=DIR).
import sys, struct, hashlib, json, collections
import brotli

NOMBRES = {0: 'Nuevo', 1: 'Redimensiona', 2: 'PreparaDispositivo', 3: 'PreloadTexture', 4: 'CreateTextureFromBitmap', 5: 'BeginFrame', 6: 'EndFrame',
           7: 'DrawSky', 8: 'DrawStars', 9: 'DrawShootStreaks', 10: 'DrawMesh', 11: 'DrawMeshAdd', 12: 'DrawTexQuad', 13: 'DrawOpaqueQuad',
           14: 'DrawQuads', 15: 'DrawQuadsWrap', 16: 'DrawCube', 17: 'CreateTexturePow2', 18: 'UpdateTextureRegion', 19: 'UpdateTexturePow2',
           20: 'Dispose', 21: 'EmpiezaCuenta', 22: 'EmpiezaMedia', 23: 'TerminaMedia', 24: 'DibujaMedia', 25: 'DrawSkyN', 26: 'DibujaDosTexturas',
           27: 'PideSinEspera', 28: 'CreaPortadaCompartida'}
CIELO = (7, 25)
CON_TRIS = (8, 9, 10, 11, 14, 15, 25, 24, 26)

def lee(ruta):
    d = brotli.decompress(open(ruta, 'rb').read())
    cab = b'ROCOLA-CINTA-TARJETA 1\n'
    assert d.startswith(cab), 'no es una cinta'
    i = len(cab); n = len(d)
    while i + 5 <= n:
        tipo = d[i]; largo = struct.unpack_from('<i', d, i + 1)[0]
        datos = d[i + 5:i + 5 + largo]; i += 5 + largo
        yield tipo, datos

def resume(ruta):
    cuadros = collections.defaultdict(list)   # disp -> [cuadro], cuadro = lista de (metodo, firma)
    actual = {}
    for tipo, datos in lee(ruta):
        if tipo != 2: continue
        disp, metodo = struct.unpack_from('<iB', datos, 0)
        args = datos[13:]
        if metodo == 5: actual[disp] = []; continue
        if metodo == 6:
            if disp in actual: cuadros[disp].append(actual.pop(disp))
            continue
        if disp not in actual: continue
        if metodo == 9: continue   # las fugaces: al azar
        if metodo in CIELO:
            firma = hashlib.sha1(args).hexdigest()[:16]
        elif metodo in (10, 11):   # las mallas (la aurora): cuantos triangulos
            firma = str(struct.unpack_from('<i', args, 0)[0]) if len(args) >= 4 else '?'
        else:
            firma = ''
        actual[disp].append((metodo, firma))
    if not cuadros: return {'cuadros': 0}
    disp = max(cuadros, key=lambda k: len(cuadros[k]))
    cs = cuadros[disp]
    cielos = collections.Counter(f for c in cs for (m, f) in c if m in CIELO)
    formas = collections.Counter(' '.join(NOMBRES.get(m, str(m)) for (m, f) in c) for c in cs)
    return {'cuadros': len(cs), 'cielos': dict(cielos), 'formas': len(formas), 'forma_comun': formas.most_common(1)[0][0], 'formas_todas': dict(formas)}

if __name__ == '__main__':
    if sys.argv[1] == 'resume':
        r = resume(sys.argv[2]); r.pop('formas_todas', None); print(json.dumps(r, indent=1))
    elif sys.argv[1] == 'compara':
        a, b = resume(sys.argv[2]), resume(sys.argv[3])
        mal = []
        if a['cuadros'] == 0 or b['cuadros'] == 0: mal.append('sin cuadros (%d y %d)' % (a['cuadros'], b['cuadros']))
        else:
            if set(a['cielos']) != set(b['cielos']): mal.append('los degradados del cielo no son los mismos: %s / %s' % (sorted(a['cielos']), sorted(b['cielos'])))
            if a['forma_comun'] != b['forma_comun']: mal.append('la forma del cuadro mas comun no es la misma:\n  %s\n  %s' % (a['forma_comun'], b['forma_comun']))
            soloA = set(a['formas_todas']) - set(b['formas_todas']); soloB = set(b['formas_todas']) - set(a['formas_todas'])
        print('cuadros %d y %d; degradados del cielo distintos: %d y %d (%s); formas de cuadro: %d y %d' % (a['cuadros'], b['cuadros'], len(a['cielos']), len(b['cielos']),
              ', '.join(sorted(a['cielos'])), a['formas'], b['formas']))
        if a['cuadros'] and b['cuadros']:
            print('formas solo en la primera: %d; solo en la segunda: %d' % (len(soloA), len(soloB)))
        print('IGUALES' if not mal else 'DISTINTAS: ' + '; '.join(mal))
        sys.exit(0 if not mal else 1)
