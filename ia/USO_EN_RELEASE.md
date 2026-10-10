# Entrenadores instalados junto a la Rocola

En este PC, arrastra **D:\ReleaseHD** sobre `D:\ReleaseHD\ia\entrenar_dsee.bat`.
La carpeta completa permite encontrar los datos y los modelos instalados que sirven
de referencia. No hace falta mover ni duplicar los archivos grandes.

Requiere Python 3 y NumPy. El material DSEE debe incluir `.bin` y `.idx` con el mismo
nombre base; el índice identifica el origen de los ejemplos. Si falta, el entrenador
no puede certificar validación independiente: no lo inventes ni renombres otro índice.

Los resultados quedan en `ia\entrenado\dsee-*` y `ia\entrenado\eq-*` como candidatos
aislados. No reemplazan los modelos de reproducción. Conserva los informes y realiza
la validación PCM y escucha antes de autorizar cualquier activación. Las herramientas
adicionales de evaluación están en el paquete completo del repositorio, no en este
conjunto mínimo de entrenamiento.

No llames `entrenar_ia.py --help`: ese entrenador no reconoce esa opción y podría
iniciar su prueba sintética. Utiliza el lanzador de arrastre indicado arriba.

Actualizacion posterior del 10-10-2026: Apollo de HD ya tiene el paquete coherente
de `ia\upscale` y `ia\tiempo_real` (35 archivos), con respaldo anterior/nuevo y
manifiesto en `D:\ReleaseHD\respaldo_apollo_20261010_9a681e66b2654de59a9a3e332a42cacc`.
Conserva pesos, venv, codigo externo Apollo, cache, modelo.txt y ajustes del usuario.
Se comprobaron 108 pruebas y una inferencia no silenciosa real en CUDA, tambien
despues de instalar. No copies `util_audio.py` aisladamente sobre versiones antiguas.

`pruebas/apollosesionreal.py` es una integracion opcional del repositorio: utiliza
el paquete y codigo Apollo indicados, crea solamente salidas nuevas en TEMP y lee
la entrada sin modificarla. Necesita FFmpeg/ffprobe en PATH y el entorno Apollo
instalado. No ejecutarla durante un benchmark grafico o entrenamiento.

Ejemplo desde el repositorio HD (sustituir la ruta de cancion):

```powershell
& 'D:\ReleaseHD\ia\upscale\venv\Scripts\python.exe' pruebas\apollosesionreal.py --entrada 'D:\musica\album\cancion.mp3' --paquete 'D:\ReleaseHD\ia' --apollo 'D:\ReleaseHD\ia\upscale\Apollo'
```

Usa una degradacion CONTROLADA de 12 segundos para probar la red, publica WAV
float32 y genera X/Y a ciegas con niveles igualados. No demuestra preferencia
audible ni reconstruccion fiel del original. No instala ni activa nuevos modelos.
Detalle y limites: `CIERRE_REAL_Y_APOLLO_2026-10-10.md` en la raiz del repositorio.
