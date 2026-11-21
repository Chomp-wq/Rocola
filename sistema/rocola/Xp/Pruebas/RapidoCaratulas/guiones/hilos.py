# hilos.py PATRON SALIDA: espera la rocola (la linea de mando con PATRON), y mientras corre, cada 0,2 s, de cada hilo: nombre, nice y
# utime+stime (ticks de 10 ms); al final, por hilo, el nombre, los nice vistos (y cuantos ticks gasto con cada uno) y el total
import os, sys, time, subprocess
pat, sal = sys.argv[1], sys.argv[2]
pid = None
for _ in range(3000):
    r = subprocess.run(["pgrep", "-n", "-f", "^" + pat], capture_output=True, text=True).stdout.split()
    if r: pid = r[0]; break
    time.sleep(0.2)
hil = {}
while pid and os.path.exists("/proc/%s" % pid):
    try: tids = os.listdir("/proc/%s/task" % pid)
    except OSError: break
    for t in tids:
        try:
            s = open("/proc/%s/task/%s/stat" % (pid, t)).read(); nm = open("/proc/%s/task/%s/comm" % (pid, t)).read().strip()
        except OSError: continue
        f = s[s.rfind(")") + 2:].split(); cpu = int(f[11]) + int(f[12]); ni = int(f[16])
        h = hil.setdefault(t, {"nm": nm, "cpu": 0, "por": {}})
        h["por"][ni] = h["por"].get(ni, 0) + max(0, cpu - h["cpu"]); h["cpu"] = cpu; h["nm"] = nm
    time.sleep(0.2)
with open(sal, "w") as o:
    for t, h in sorted(hil.items(), key=lambda x: -x[1]["cpu"]):
        if h["cpu"] > 0: o.write("%s\t%s\t%d\t%s\n" % (t, h["nm"], h["cpu"], " ".join("nice%d:%d" % kv for kv in sorted(h["por"].items()))))
