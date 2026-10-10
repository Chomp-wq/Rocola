"""Presupuesto del entrenador: afinidad real, margen a la Rocola y reproducibilidad.

Importar antes de NumPy. ROCOLA_HILOS_ENTRENAMIENTO permite fijar 1..CPU disponibles
para repetir una ejecución en la misma pila BLAS, sin cambiar semilla ni ejemplos.
No inicia entrenamiento ni cambia la afinidad del proceso.
"""
import os


def procesadores_disponibles():
    try:
        afinidad = os.sched_getaffinity(0)
        if afinidad:
            return len(afinidad)
    except (AttributeError, OSError):
        pass
    if os.name == "nt":
        try:
            import ctypes
            k = ctypes.windll.kernel32
            k.GetCurrentProcess.restype = ctypes.c_void_p
            k.GetProcessAffinityMask.argtypes = (ctypes.c_void_p, ctypes.POINTER(ctypes.c_size_t),
                                                ctypes.POINTER(ctypes.c_size_t))
            proceso, sistema = ctypes.c_size_t(), ctypes.c_size_t()
            if k.GetProcessAffinityMask(k.GetCurrentProcess(), ctypes.byref(proceso), ctypes.byref(sistema)):
                n = bin(proceso.value).count("1")
                if n:
                    return n
        except (AttributeError, OSError, ValueError):
            pass
    return max(1, os.cpu_count() or 1)


def hilos_entrenamiento(procesadores):
    n = max(1, int(procesadores))
    return max(1, n - 1) if n <= 4 else max(2, n // 2)


def configura_blas():
    disponibles = procesadores_disponibles()
    solicitado = os.environ.get("ROCOLA_HILOS_ENTRENAMIENTO")
    n = hilos_entrenamiento(disponibles)
    if solicitado is not None:
        n = int(solicitado)
        if not 1 <= n <= disponibles:
            raise ValueError("ROCOLA_HILOS_ENTRENAMIENTO requiere 1..%d" % disponibles)
    # Evitar paralelismo anidado y valores heredados pensados para otra maquina.
    for variable in ("OPENBLAS_NUM_THREADS", "OMP_NUM_THREADS", "MKL_NUM_THREADS",
                     "VECLIB_MAXIMUM_THREADS", "NUMEXPR_NUM_THREADS", "BLIS_NUM_THREADS"):
        os.environ[variable] = str(n)
    return n
