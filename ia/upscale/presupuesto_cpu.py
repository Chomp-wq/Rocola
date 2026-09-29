"""Presupuesto de los trabajos de Apollo, sin cambiar muestras ni parametros del modelo."""
import os


def procesadores_disponibles():
    """Respeta afinidad/restricciones; cpu_count solo es el respaldo."""
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
            k.GetProcessAffinityMask.argtypes = (ctypes.c_void_p, ctypes.POINTER(ctypes.c_size_t), ctypes.POINTER(ctypes.c_size_t))
            proceso, sistema = ctypes.c_size_t(), ctypes.c_size_t()
            if k.GetProcessAffinityMask(k.GetCurrentProcess(), ctypes.byref(proceso), ctypes.byref(sistema)):
                n = bin(proceso.value).count("1")
                if n:
                    return n
        except (AttributeError, OSError, ValueError):
            pass
    return max(1, os.cpu_count() or 1)


def hilos_audio(procesadores):
    """Deja margen a musica/pantalla. Crece con la maquina, sin el tope global de dos."""
    n = max(1, int(procesadores))
    return max(1, n - 1) if n <= 4 else max(2, n // 2)


def aplica_presupuesto(torch):
    n = hilos_audio(procesadores_disponibles())
    try:
        torch.set_num_threads(n)
    except (AttributeError, RuntimeError):
        pass  # runtimes que no permiten cambiarlo tras empezar a trabajar
    try:
        torch.set_num_interop_threads(1)  # un modelo/un trabajo; evita otro nivel de paralelismo
    except (AttributeError, RuntimeError):
        pass
    return n
