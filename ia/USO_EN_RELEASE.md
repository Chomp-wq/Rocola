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

Las herramientas Apollo instaladas en `ia\upscale` y `ia\tiempo_real` no se sustituyen
con esta actualización. El escáner optimizado del repositorio requiere un paquete
Apollo coherente; no copies `util_audio.py` aisladamente sobre una versión antigua.
