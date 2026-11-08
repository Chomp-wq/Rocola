# Le pone a una letra las tablas que GDI lee y que las letras libres no traen: hdmx (el ancho en pixeles de cada glifo por tamano) y VDMX
# (el alto por tamano), con valores a proposito distintos de los calculados, para que el banco pase por ese camino de TextRenderer.
import os, sys
sys.path.insert(0, os.environ.get('ROCOLA_TRABAJO', '/root/rocola-trabajo/port/dibujo') + '/py')
from fontTools.ttLib import TTFont, newTable
ent, sal, conVdmx = sys.argv[1], sys.argv[2], sys.argv[3] == '1'
f = TTFont(ent)
upem = f['head'].unitsPerEm
hm = f['hmtx'].metrics
orden = f.getGlyphOrder()
h = newTable('hdmx'); h.version = 0; h.hdmx = {}
for ppem in (9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 21, 24):
    h.hdmx[ppem] = {g: int(hm[g][0] * ppem / upem + 0.5) + (1 if (i % 7 == 3) else 0) for i, g in enumerate(orden)}
f['hdmx'] = h
if conVdmx:
    v = newTable('VDMX'); v.version = 1; v.numRecs = 1; v.numRatios = 1
    v.ratRanges = [{'bCharSet': 1, 'xRatio': 0, 'yStartRatio': 0, 'yEndRatio': 0, 'groupIndex': 0}]
    asc, desc = f['OS/2'].usWinAscent, f['OS/2'].usWinDescent
    grupo = {}
    for ppem in range(8, 31):
        grupo[ppem] = (int(asc * ppem / upem) + 1, -(int(desc * ppem / upem) + (ppem % 3 == 0)))
    v.groups = [grupo]
    f['VDMX'] = v
f.save(sal)
