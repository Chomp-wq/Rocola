#!/usr/bin/env python3
# LO QUE PINTA EL CIELO EN UNA CINTA DE LA TARJETA (ROCOLA_CINTA, Nativo/CintaTarjeta.cs), para comparar dos corridas que no van al mismo
# ritmo (el reloj y el azar de las estrellas y las fugaces no son los mismos): de cada cuadro grabado del dispositivo de la rocola,
#   - los degradados del cielo (DrawSky y DrawSkyN): sus vertices, byte a byte (son del color del genero y de la hora: deterministas);
#   - la forma del cuadro: la lista de llamadas (sin las fugaces, DrawShootStreaks, que salen al azar) con cuantos vertices manda cada dibujo
#     de malla o de cielo, y las texturas que se suben (la mas comun);
#   - los colores de la nebulosa y la via lactea, las estrellas y la aurora con su raya (ver COLORES).
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

# LOS COLORES de los dibujos que tocan los ganchos del nucleo y no son el degradado: la nebulosa y la via lactea (DrawQuads: g124/g125), las
# estrellas (DrawStars: g126/g127) y la aurora con su raya (DrawMeshAdd: g128). Sus vertices (28 bytes: x, y, z, rhw, color ARGB, u, v) van
# tras la textura (8 bytes, si la hay), el modo (4) y el arreglo (largo y cuantos triangulos, 8). Se compara el conjunto de colores RGB (sin alfa)
# y el alfa medio de cada uno. Las estrellas titilan y la aurora fluye y entra poco a poco al arrancar (por cuadros: con la maquina cargada, la
# misma ventana de 1,5 s la agarra en otro punto), asi que de ellas solo se compara lo grueso: el alfa medio de las estrellas al 12 % (de dia,
# con DN1, cae de 135 a 5) y el de la aurora a la mitad, sin sus RGB. Ademas, «Raya»: los vertices de la aurora en la franja de la raya del
# horizonte (los 45 px de abajo de cada malla: la raya, su resplandor de 34 px y los 11 de abajo), con su color medio (R, G, B) a 8 niveles:
# es lo que cambia DN5 (el color medio de la raya pasa de 118/83/202 a 171/162/210 a las 13:00; entre dos corridas de XP, hasta 6).
# La nebulosa y la via lactea (DrawQuads) no se mueven: sus colores, los mismos, y su alfa medio al 3 %.
COLORES = {14: (True, 'DrawQuads'), 8: (True, 'DrawStars'), 11: (False, 'DrawMeshAdd')}
TOLERA_ALFA = {'DrawQuads': 0.03, 'DrawStars': 0.12, 'DrawMeshAdd': 0.5, 'Raya': 0.5}   # (relativa, del alfa medio)
COMPARTE_RGB = {'DrawQuads': 1.0, 'DrawStars': 0.0, 'DrawMeshAdd': 0.0, 'Raya': 0.0}    # (lo que deben compartir los conjuntos RGB: 1 = los mismos)
TOLERA_MEDIO = 8.0                                                                       # (el color medio de la raya, en niveles)
RAYA_PX = 45
def colores(metodo, args, rgb, alfa, medio):
    con_tex, nom = COLORES[metodo]
    o = (8 if con_tex else 0) + 4
    if len(args) < o + 8: return
    vl, n = struct.unpack_from('<ii', args, o); o += 8
    vl = min(vl, len(args[o:]) // 28)
    for k in range(vl):
        c = struct.unpack_from('<I', args, o + k * 28 + 16)[0]
        rgb[nom].add(c & 0xFFFFFF); alfa[nom].append(c >> 24)
    if metodo == 11 and vl > 0:
        ys = [struct.unpack_from('<f', args, o + k * 28 + 4)[0] for k in range(vl)]
        abajo = max(ys) - RAYA_PX
        for k in range(vl):
            if ys[k] < abajo: continue
            c = struct.unpack_from('<I', args, o + k * 28 + 16)[0]
            alfa['Raya'].append(c >> 24); rgb['Raya'].add(c & 0xFFFFFF)
            medio[0] += (c >> 16) & 255; medio[1] += (c >> 8) & 255; medio[2] += c & 255; medio[3] += 1

# LA FORMA de un cuadro: sus llamadas en orden, sin las mallas sumadas (DrawMeshAdd) que van despues de las primeras estrellas: los destellos y
# brillos de la estrella y del pulido de XP (PulidoGpu, Estrella3dGpu, el reflejo) salen a ratos por su reloj, y con la maquina cargada dos
# corridas no los agarran en los mismos cuadros (las dos de la aurora y su raya van antes y si cuentan); y con ellos una pasada mas de
# estrellas: dos llamadas iguales seguidas cuentan como una
def forma(c):
    out, estrellas = [], False
    for (m, f) in c:
        if m == 8: estrellas = True
        if m == 11 and estrellas: continue
        n = NOMBRES.get(m, str(m))
        if out and out[-1] == n: continue
        out.append(n)
    return ' '.join(out)

def resume(ruta):
    rgb = collections.defaultdict(set); alfa = collections.defaultdict(list); medio = [0, 0, 0, 0]
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
        if metodo in COLORES: colores(metodo, args, rgb, alfa, medio)
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
    formas = collections.Counter(forma(c) for c in cs)
    col = {nom: {'rgb': sorted(rgb[nom]), 'alfa': round(sum(alfa[nom]) / len(alfa[nom]), 2)} for nom in rgb if alfa[nom]}
    if medio[3] and 'Raya' in col: col['Raya']['medio'] = [round(x / medio[3], 1) for x in medio[:3]]
    return {'cuadros': len(cs), 'cielos': dict(cielos), 'formas': len(formas), 'forma_comun': formas.most_common(1)[0][0], 'formas_todas': dict(formas),
            'colores': col}

def compara_colores(a, b):
    mal, dice = [], []
    for nom in sorted(set(a) | set(b)):
        if nom not in a or nom not in b: mal.append('%s solo en una' % nom); continue
        ra, rb = set(a[nom]['rgb']), set(b[nom]['rgb']); ma, mb = a[nom]['alfa'], b[nom]['alfa']
        comun = len(ra & rb) / max(1, max(len(ra), len(rb)))
        dice.append('%s: %d y %d colores (%.0f %% comunes), alfa medio %.1f y %.1f' % (nom, len(ra), len(rb), comun * 100, ma, mb))
        if COMPARTE_RGB[nom] > 0 and comun < COMPARTE_RGB[nom] - 1e-9: mal.append('%s: los colores no son los mismos (%.0f %% comunes)' % (nom, comun * 100))
        if abs(ma - mb) > TOLERA_ALFA[nom] * max(ma, mb, 1): mal.append('%s: el alfa medio %.1f frente a %.1f' % (nom, ma, mb))
        if 'medio' in a[nom] or 'medio' in b[nom]:
            pa, pb = a[nom].get('medio', [0, 0, 0]), b[nom].get('medio', [0, 0, 0])
            dice.append('%s: color medio %s y %s' % (nom, '/'.join('%.0f' % x for x in pa), '/'.join('%.0f' % x for x in pb)))
            if max(abs(x - y) for x, y in zip(pa, pb)) > TOLERA_MEDIO: mal.append('%s: el color medio no es el mismo' % nom)
    return mal, dice

if __name__ == '__main__':
    if sys.argv[1] == 'resume':
        r = resume(sys.argv[2]); r.pop('formas_todas', None)
        for v in r.get('colores', {}).values(): v['rgb'] = len(v['rgb'])
        print(json.dumps(r, indent=1))
    elif sys.argv[1] == 'compara':
        a, b = resume(sys.argv[2]), resume(sys.argv[3])
        mal = []
        if a['cuadros'] == 0 or b['cuadros'] == 0: mal.append('sin cuadros (%d y %d)' % (a['cuadros'], b['cuadros']))
        else:
            if set(a['cielos']) != set(b['cielos']): mal.append('los degradados del cielo no son los mismos: %s / %s' % (sorted(a['cielos']), sorted(b['cielos'])))
            # (con la maquina cargada la ventana de 1,5 s agarra pocos cuadros, y un dibujo que sale de vez en cuando, como una malla mas de la
            # aurora, cambia cual es la mas comun: vale si la mas comun de una tambien sale en la otra)
            if a['forma_comun'] != b['forma_comun'] and a['forma_comun'] not in b['formas_todas'] and b['forma_comun'] not in a['formas_todas']: mal.append('la forma del cuadro mas comun no es la misma:\n  %s\n  %s' % (a['forma_comun'], b['forma_comun']))
            soloA = set(a['formas_todas']) - set(b['formas_todas']); soloB = set(b['formas_todas']) - set(a['formas_todas'])
        print('cuadros %d y %d; degradados del cielo distintos: %d y %d (%s); formas de cuadro: %d y %d' % (a['cuadros'], b['cuadros'], len(a['cielos']), len(b['cielos']),
              ', '.join(sorted(a['cielos'])), a['formas'], b['formas']))
        if a['cuadros'] and b['cuadros']:
            print('formas solo en la primera: %d; solo en la segunda: %d' % (len(soloA), len(soloB)))
        if a['cuadros'] and b['cuadros']:
            mc, dice = compara_colores(a['colores'], b['colores'])
            for d in dice: print('colores ' + d)
            mal += mc
        print('IGUALES' if not mal else 'DISTINTAS: ' + '; '.join(mal))
        sys.exit(0 if not mal else 1)
