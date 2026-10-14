# Entrenadores instalados junto a la Rocola

En este PC, arrastra **D:\Release1** sobre `D:\Release1\ia\entrenar_dsee.bat`.
La carpeta completa permite encontrar los datos y los modelos instalados que sirven
de referencia. No hace falta mover ni duplicar los archivos grandes.

Requiere Python 3 y NumPy en un equipo compatible; no es una nueva dependencia del
reproductor Modest para XP. El material DSEE debe incluir `.bin` y `.idx` con el mismo
nombre base; el índice identifica el origen de los ejemplos. Si falta, el entrenador
no puede certificar validación independiente: no lo inventes ni renombres otro índice.

Los resultados quedan en `ia\entrenado\dsee-*` y `ia\entrenado\eq-*` como candidatos
aislados. No reemplazan los modelos de reproducción. Conserva los informes y realiza
la validación PCM y escucha antes de autorizar cualquier activación. Las herramientas
adicionales de evaluación están en el paquete completo del repositorio, no en este
conjunto mínimo de entrenamiento.

Desde el 14 de octubre de 2026 la rocola lee todos sus modelos de la carpeta **`IAS`** (junto a `Rocola.exe`, o
dentro de `ROCOLA_DATOS`): `rocola_ia.bin`, `rocola_eq_ia.bin` y los `rocola_ia_genero_*.bin`. Al arrancar pasa ahí
los que encuentre sueltos, sin borrar ninguno. El material (`rocola_ia_datos.*`, `rocola_eq.txt`) se queda donde
estaba. Arrastrando la carpeta de la rocola, el entrenador encuentra los modelos de `IAS`.

No llames `entrenar_ia.py --help`: ese entrenador no reconoce esa opción y podría
iniciar su prueba sintética. Utiliza el lanzador de arrastre indicado arriba.

## Modelos del DSEE por género, paso a paso

1. **Que la rocola junte material con género.** Deja la rocola funcionando con «Juntar material para la IA»
   encendido (o una noche de repaso a fondo). Al juntar, escribe junto a `Rocola.exe`:
   - `rocola_ia_datos.bin` y `rocola_ia_datos.idx`;
   - `rocola_ia_datos.generos.tsv`, el género de cada canción, sacado de su etiqueta.

   Las canciones sin género en la etiqueta cuentan con el que la rocola deduce de su disco (la pastilla de la
   pantalla: «Regional», «Rock»…). Hace falta que un género tenga **60 canciones o más** con
   material.
2. **Entrenar.** Arrastra la carpeta de la rocola (por ejemplo `D:\Release1`) sobre `ia\entrenar_dsee.bat`.
   Primero entrena el modelo general y después uno por cada género con material de sobra (como mucho los 6 con
   más). El RESUMEN del final dice qué salió:
   - cada modelo de género aceptado aparece como `DSEE <género>`;
   - si no salió ninguno, debajo dice por qué: falta `rocola_ia_datos.generos.tsv`, ningún género llega a 60
     canciones (con los que más tienen), o no le ganó al general en su género.
3. **Instalar.** Doble clic en `ia\instalar_dsee_genero.bat`:
   - busca solo los modelos de `ia\entrenado\dsee-genero-*` y dice de cada uno si se puede instalar o por qué no;
   - propone la carpeta de la rocola (Enter para aceptarla);
   - pregunta género por género si ya lo escuchaste;
   - la ventana no se cierra hasta que aprietes Enter, y lo que pasó queda en `ia\instalar_dsee_genero_log.txt`.

   Lo deja en la carpeta `IAS` de la rocola (la crea si no está). El modelo general no se toca. Si ya había un
   modelo de ese género, se guarda en `IAS` como `.anterior-N`.
4. **Usarlo.** Reinicia la rocola. En Ctrl+F3 → Sonido → Mejoras con IA, enciende «Modelos del DSEE por género»
   (con el DSEE encendido). Las canciones de ese género usan su modelo; las demás, el general.
