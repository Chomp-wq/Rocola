# Compara lo que sacan la rocola de XP y el motor nuevo (CompararConXP): por etapa, cuantas muestras son identicas bit a bit y la mayor
# diferencia en dB bajo el maximo. Uso: python3 compara.py <carpeta de XP> <carpeta del motor>   (devuelve 1 si alguna pasa de -100 dB)
import sys, os, numpy as np
a, b = sys.argv[1], sys.argv[2]
mal = 0
for f in sorted(x for x in os.listdir(a) if x.endswith('.f32')):
    x = np.fromfile(os.path.join(a, f), np.float32); y = np.fromfile(os.path.join(b, f), np.float32)
    if len(x) != len(y): print(f"  DISTINTO LARGO {f}: {len(x)} y {len(y)}"); mal = 1; continue
    iguales = np.count_nonzero(x.view(np.int32) == y.view(np.int32))
    d = float(np.max(np.abs(x.astype(np.float64) - y))) if len(x) else 0.0
    db = 20 * np.log10(d) if d > 0 else -999
    estado = "igual bit a bit" if iguales == len(x) else ("casi igual" if db < -100 else "DISTINTO")
    if db >= -100: mal = 1
    print(f"  {f[:-4]:20s} {estado:16s} {100.0*iguales/len(x):6.2f} % de muestras identicas, mayor diferencia {db:7.1f} dB")
ta, tb = os.path.join(a, 'sonoridad.txt'), os.path.join(b, 'sonoridad.txt')
if os.path.exists(ta):
    igual = open(ta).read() == open(tb).read()
    print(f"  {'sonoridad (medida)':20s} {'igual' if igual else 'DISTINTA'}: {open(tb).read().strip()}")
    if not igual: mal = 1
sys.exit(mal)
