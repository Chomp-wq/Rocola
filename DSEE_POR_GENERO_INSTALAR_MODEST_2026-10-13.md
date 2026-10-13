# El instalador del DSEE por género ya no se cierra solo — Modest, 13 de octubre de 2026

Petición del usuario: «Busca más mejoras y dime cómo entrenar el DSEE por género. Lo entrené, luego usé
instalar_dsee_genero.py y se cierra en cuanto se abre».

## Por qué se cerraba

`instalar_dsee_genero.py` era un programa de consola que esperaba sus datos escritos detrás:
`--candidato`, `--destino` y `--escuchado`. Con doble clic no recibe ninguno, así que imprimía su ayuda y terminaba. La
ventana se cerraba antes de poder leer nada.

## Qué se cambió

- **Doble clic en `ia\instalar_dsee_genero.bat`** (nuevo), o en el `.py`. Sin nada escrito detrás, el programa:
  1. busca solo los modelos por género que dejó el entrenador en `ia\entrenado\dsee-genero-*`, y dice de cada uno si
     se puede instalar o por qué no (por ejemplo, «el entrenador no lo aceptó: …»);
  2. propone la carpeta de la rocola: `ROCOLA_DATOS` si está puesta, o la de `Rocola.exe`, que está encima de la
     carpeta `ia`. Enter la acepta;
  3. pregunta género por género si ya lo escuchaste, e instala los que digas que sí;
  4. **no cierra la ventana hasta que aprietes Enter**, también si algo falla (por ejemplo, si falta numpy). Lo que
     pasó queda en `ia\instalar_dsee_genero_log.txt`.
- El `.bat` busca Python igual que `entrenar_dsee.bat` y, si no lo encuentra, lo dice y espera.
- **El entrenador avisa cuando no hace modelos por género.** Antes, si faltaba `rocola_ia_datos.generos.tsv`, se los
  saltaba sin decir nada. Ahora el resumen lo dice. Si ningún género llega a 60 canciones con material, dice cuáles se
  acercan y con cuántas. Al final apunta a `instalar_dsee_genero.bat`.
- **La rocola encuentra un modelo recién instalado sin reiniciar.** Al abrir Ctrl+F3 → Sonido → Mejoras con IA vuelve
  a buscarlos, en segundo plano, y la hoja dice cuántos hay.
- La guía paso a paso está en [ia/USO_EN_RELEASE.md](ia/USO_EN_RELEASE.md).

Las reglas para instalar no cambian. Solo se instala si:

- el entrenador lo aceptó, porque le ganó al general en su género;
- es de un género;
- la huella coincide;
- tú confirmas que lo escuchaste.

El modelo general no se toca.

## Cómo se comprobó

- **`ia/test_genero_dsee.py`**: 10 pruebas, 5 nuevas.
  - Con una carpeta `entrenado` de prueba (cumbia aceptado, bolero aceptado, salsa rechazado y rock a medias), lista
    cada uno con su estado. Con las respuestas «Enter, no, sí» instala solo la cumbia.
  - Sin modelos, explica por qué. Con una carpeta que no existe, no instala nada.
  - Sin argumentos, espera el Enter antes de cerrar, también si algo falla.
  - El `.bat` existe, busca Python, espera y lleva finales de línea de Windows.
- **Doble clic simulado** (sin argumentos, desde otra carpeta): explica que no hay modelos y espera el Enter.
- **Las otras 13 pruebas de `ia/`** siguen pasando.
- **`mejorasia`** (37 comprobaciones): un modelo instalado con la rocola abierta se encuentra al volver a buscar.
- **`ventanaf3`** sigue pasando con el texto nuevo de la hoja, y **`bloqueos`** también: la búsqueda no va en el hilo
  de la pantalla.
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre, ninguno nuevo. Dibujo por CPU: sin tocar.

**No probado aquí:** Windows de verdad. El `.bat` sigue el mismo modelo que `entrenar_dsee.bat`, que ya funciona allí.
