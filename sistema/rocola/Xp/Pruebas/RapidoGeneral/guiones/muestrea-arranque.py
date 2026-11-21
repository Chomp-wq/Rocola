# gdb -p PID -batch -x muestrea.py : N muestras (env MUESTRAS, cada PAUSA s) de las pilas de todos los hilos, a MUESTRAS_SALIDA
import gdb, threading, os, signal, time
N = int(os.environ.get("MUESTRAS", "40")); P = float(os.environ.get("PAUSA", "0.4")); out = open(os.environ["MUESTRAS_SALIDA"], "w")
gdb.execute("set pagination off"); gdb.execute("handle SIGINT stop nopass print"); gdb.execute("handle SIGUSR1 nostop noprint pass")
gdb.execute("handle SIGPWR nostop noprint pass"); gdb.execute("handle SIGXCPU nostop noprint pass"); gdb.execute("handle SIG34 nostop noprint pass")
gdb.execute("set startup-with-shell off"); gdb.execute("starti"); pid = gdb.selected_inferior().pid
for i in range(N):
    out.write("=== muestra %d\n" % i)
    for th in [t for t in gdb.selected_inferior().threads() if t.ptid[1] == pid]:
        try:
            th.switch(); f = gdb.newest_frame(); names = []
            k = 0
            while f is not None and k < 24:
                names.append(f.name() or "?"); f = f.older(); k += 1
            out.write("%s|%s\n" % (th.name, ";".join(names)))
        except Exception as e: out.write("err %s\n" % e)
    out.flush()
    threading.Timer(P, lambda: os.kill(pid, signal.SIGINT)).start()
    try: gdb.execute("continue")
    except Exception: break
gdb.execute("kill")
