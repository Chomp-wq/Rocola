# Los guiones de la medida de rapido-general (copias; se corren desde /root/rocola-trabajo/fase11/rapido-general/guiones)

- `genera.py RAIZ ARTISTAS BASES`: la biblioteca inventada (MP3 de 8 s enlazados de 6 bases hechas con ffmpeg, portadas chicas generadas).
- `compila.sh ETIQUETA FUENTE [aot|jit|ambos]`: el programa de medir (`Pruebas/RapidoGeneral`, con `Mide.cs`) de una carpeta Xp; con
  `MIDEBASE=1` para la base (sin `CompositorPantalla.Prueba`), `DBG=1` deja los simbolos (para gdb), `EXTRAPROP` otras propiedades.
- `corre.sh PROG SALIDA DATOS PULSA T TRAMOS`: la rocola fuera de pantalla a 1024x768, el programa en los nucleos 0 y 1 y llvmpipe en el 2 y 3;
  `ROCOLA_MIDE_COMPARA=K` compara la pantalla compuesta con la de antes cada K cuadros; `ROCOLA_MIDE_COSTE=K` mide lo que cuesta componer.
- `escenas.sh`: las teclas de las escenas (las de `Pruebas/Recorridos/recorridos.sh`).
- `serie.sh`, `varias.sh`: las configuraciones alternadas; `tabla.py`: las medianas; `repaso.py`: el procesador del repaso por cancion.
- `muestrea.py`, `muestrea-arranque.py`, `asigna.py`, `resume.py`: el muestreo de pilas con gdb (por hilo, al arrancar y de la memoria nueva
  del hilo de la pantalla, en RhpGcAlloc).
