"""Las herramientas de comparar A/B tienen que atenuar TODOS los niveles con el MISMO margen."""
import inspect
import re
import unittest
import numpy as np

import util_audio


class MargenComunTests(unittest.TestCase):
    """Por que esto importa, y por que se comprueba leyendo el codigo.

    Las dos herramientas de fuera generan la misma cancion a varios niveles de IA para que una persona
    escuche y elija. Si cada nivel se atenuara por su cuenta -cada uno con SU ganancia de exportacion-
    saldrian a volumenes distintos, y entonces la eleccion ya no es 'cual suena mejor' sino 'cual suena
    mas fuerte'. El margen tiene que salir UNA vez, del peor de todos, y aplicarse a todos por igual.

    Aqui no se pueden correr: piden ffmpeg y el modelo de Apollo, y ninguno esta en esta maquina. Asi
    que se lee su codigo, igual que hacen las baterias de la rocola. Se dice tal cual para que nadie
    lea esto como 'probado sonando'.
    """

    def _fuente(self, modulo):
        import importlib.util, os, sys
        ruta = os.path.join(os.path.dirname(os.path.abspath(__file__)), modulo + ".py")
        with open(ruta, encoding="utf-8") as f:
            return f.read()

    def test_comparar_mezcla_saca_un_solo_margen(self):
        src = self._fuente("comparar_mezcla")
        # Un solo margen, sacado del MINIMO sobre todos los niveles...
        self.assertRegex(src, r"margen\s*=\s*min\(\s*ganancia_wav\(.*for\s+niv\s+in\s+niveles_cancion\s*\)",
                         "el margen ya no sale del minimo sobre todos los niveles")
        # ...y aplicado a cada mezcla.
        self.assertRegex(src, r"mezcla\s*=\s*mezclar\(.*\)\s*\*\s*margen",
                         "el margen comun ya no se aplica a cada nivel")
        # Y que no haya vuelto a aparecer una ganancia por nivel dentro del bucle.
        dentro = src.split("for niv in niveles_cancion:")[-1]
        self.assertNotIn("ganancia_wav(", dentro,
                         "volvio a calcularse una ganancia por nivel: cada uno saldria a otro volumen")

    def test_piloto_usa_el_mismo_margen_para_los_dos(self):
        src = self._fuente("01_piloto")
        self.assertRegex(src, r"margen\s*=\s*min\(\s*ganancia_wav\(x\)\s*,\s*ganancia_wav\(y\)\s*,\s*ganancia_wav\(auto\)\s*\)",
                         "el piloto ya no saca un margen comun para el original y la mejorada")
        self.assertRegex(src, r"x\s*\*=\s*margen\s*;\s*y\s*\*=\s*margen",
                         "el margen comun ya no se aplica a los dos")

    def test_la_ganancia_es_la_misma_cuenta_para_todos(self):
        # Y la propiedad de verdad, esta si corriendo: aplicar un margen comun conserva la relacion de
        # volumen entre dos mezclas; aplicar el suyo a cada una, no.
        rng = np.random.default_rng(21)
        base = rng.normal(0, .2, (8000, 2))
        fuerte = base * 3.0
        comun = min(util_audio.ganancia_wav(base), util_audio.ganancia_wav(fuerte))
        razon_antes = float(np.sqrt((fuerte ** 2).mean()) / np.sqrt((base ** 2).mean()))
        razon_comun = float(np.sqrt(((fuerte * comun) ** 2).mean()) / np.sqrt(((base * comun) ** 2).mean()))
        self.assertAlmostEqual(razon_antes, razon_comun, places=9)
        propio_b = util_audio.ganancia_wav(base)
        propio_f = util_audio.ganancia_wav(fuerte)
        if propio_b != propio_f:   # con estas dos senales, atenuar por separado SI cambia la relacion
            razon_propia = float(np.sqrt(((fuerte * propio_f) ** 2).mean())
                                 / np.sqrt(((base * propio_b) ** 2).mean()))
            self.assertNotAlmostEqual(razon_antes, razon_propia, places=3)


if __name__ == "__main__":
    unittest.main()
