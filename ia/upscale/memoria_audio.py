"""Almacenamiento adaptativo por trabajo; sin cambiar precision ni reducciones RMS."""
import ctypes
import os
import shutil
import tempfile

import numpy as np

from util_audio import mezclar

MIB = 1024 * 1024


def memoria_disponible():
    """Instantanea, no garantia: deja margen para sistema, rocola y modelo."""
    try:
        if os.name == "nt":
            # https://learn.microsoft.com/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex
            class Estado(ctypes.Structure):
                _fields_ = [("length", ctypes.c_uint32), ("load", ctypes.c_uint32)] + [
                    (n, ctypes.c_uint64) for n in ("total", "available", "page", "available_page",
                                                  "virtual", "available_virtual", "extended")]
            estado = Estado()
            estado.length = ctypes.sizeof(estado)
            consulta = ctypes.windll.kernel32.GlobalMemoryStatusEx
            consulta.argtypes = [ctypes.POINTER(Estado)]
            consulta.restype = ctypes.c_int
            if consulta(ctypes.byref(estado)):
                return min(estado.available, estado.available_page)
        else:
            return os.sysconf("SC_AVPHYS_PAGES") * os.sysconf("SC_PAGE_SIZE")
    except (AttributeError, OSError, ValueError):
        pass
    return None


def necesita_mapa(decoded_bytes, available):
    # WAV float32 -> float64, salida, mezcla y temporales. Nunca gastar toda la RAM libre.
    presupuesto = 256 * MIB if available is None else max(0, (available - 256 * MIB) // 3)
    return decoded_bytes * 10 + 32 * MIB > presupuesto


class MemoriaAudio:
    """Vive dentro del trabajo; sus arrays no se pueden usar despues del with.

    Con margen conserva la ruta RAM. Sin el, los arrays grandes se respaldan en
    temporales de la misma carpeta del WAV decodificado. Windows administra las
    paginas residentes: esto limita reservas completas del heap, no fija el RSS.
    """
    def __init__(self, decoded):
        self.mapeada = necesita_mapa(os.path.getsize(decoded), memoria_disponible())
        self.parent = os.path.dirname(os.path.abspath(decoded))
        self.directory = None
        self.maps = []

    def __enter__(self):
        return self

    def crear(self, shape, ceros=False):
        size = int(shape[0]) * int(shape[1]) * 8
        if not self.mapeada or not size:
            return np.zeros(shape, dtype=np.float64) if ceros else np.empty(shape, dtype=np.float64)
        if self.directory is None:
            self.directory = tempfile.TemporaryDirectory(prefix="rocola_audio_", dir=self.parent)
        if shutil.disk_usage(self.directory.name).free < size + 64 * MIB:
            raise OSError("espacio insuficiente para temporales de audio")
        path = os.path.join(self.directory.name, str(len(self.maps)) + ".f64")
        value = np.memmap(path, dtype=np.float64, mode="w+", shape=shape)
        self.maps.append(value)
        # Un archivo nuevo se extiende con ceros. No tocar todas sus paginas para inicializarlo.
        return value

    def mezclar(self, mejorada, original, m):
        if not self.mapeada or not mejorada.flags.c_contiguous:
            # La salida corta de Apollo puede ser Fortran: mantener su orden original
            # de reduccion. Solo ocupa un trozo del modelo, no una cancion larga.
            return mezclar(mejorada, original, m)
        if (mejorada.shape != original.shape or original.ndim != 2 or
                original.dtype != np.float64 or mejorada.dtype != np.float64):
            raise ValueError("se requieren arrays float64 del mismo formato")
        if not original.size:
            return original.copy()
        if not np.isfinite(m):
            raise ValueError("mezcla no finita")
        if m <= 0:
            return original
        # Esta ruta es duena de la salida: reutiliza solo un mapa propio que no sea la fuente.
        if any(mejorada is v for v in self.maps) and not np.shares_memory(mejorada, original):
            a = mejorada
        else:
            a = self.crear(mejorada.shape)
            for p in range(0, len(a), 32768):
                a[p:p+32768] = mejorada[p:p+32768]
        scratch = self.crear(original.shape)

        def rms(value):
            for p in range(0, len(value), 32768):
                np.square(value[p:p+32768], out=scratch[p:p+32768])
            # Misma reduccion GLOBAL C-contigua que (value ** 2).mean(); no sumar RMS de trozos.
            return np.sqrt(np.asarray(scratch).mean())

        def escala(factor):
            for p in range(0, len(a), 32768):
                np.multiply(a[p:p+32768], factor, out=a[p:p+32768])

        ro, rm = rms(original), rms(a)
        if not rm < 1e-9:
            escala(ro / rm)
        if m >= 1:
            return a
        for p in range(0, len(a), 32768):
            part = a[p:p+32768]
            np.multiply(m, part, out=part)
            np.add(part, (1.0 - m) * original[p:p+32768], out=part)
        rm = rms(a)
        if not rm < 1e-9:
            escala(ro / rm)
        return a

    def __exit__(self, exc_type, exc, traceback):
        try:
            for value in self.maps:
                value._mmap.close()  # liberar tambien en Windows antes de retirar los archivos
        finally:
            self.maps.clear()
            if self.directory is not None:
                self.directory.cleanup()
        return False
