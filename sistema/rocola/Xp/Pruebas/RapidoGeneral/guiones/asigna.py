# gdb -p PID -batch -x asigna.py: pilas de las reservas lentas del recolector (RhpGcAlloc: una cada ~8 KB de memoria nueva) en el hilo
# principal (la pantalla), N veces, a ASIGNA_SALIDA
import gdb, os
N = int(os.environ.get("MUESTRAS", "200")); out = open(os.environ["ASIGNA_SALIDA"], "w"); pid = gdb.selected_inferior().pid
gdb.execute("set pagination off")
for s in ("SIGUSR1","SIGPWR","SIGXCPU","SIG34","SIG35","SIGINT"): gdb.execute("handle %s nostop noprint pass" % s)
class B(gdb.Breakpoint):
    def __init__(s): super().__init__("RhpGcAlloc", internal=True); s.n = 0
    def stop(s):
        if gdb.selected_thread().ptid[1] != pid: return False
        f = gdb.newest_frame(); names = []; k = 0
        while f is not None and k < 16: names.append(f.name() or "?"); f = f.older(); k += 1
        out.write(";".join(names) + "\n"); s.n += 1
        return s.n >= N
b = B()
gdb.execute("continue")
out.close(); gdb.execute("delete"); gdb.execute("detach")
