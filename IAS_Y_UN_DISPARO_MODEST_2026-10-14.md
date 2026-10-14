# Carpeta IAS y un disparo por pulsación — Modest, 14 de octubre de 2026

Peticiones del usuario:

1. «Como son muchas IAs, haz que el programa lea las IAs dentro de una carpeta llamada IAS, para así tener más orden».
2. «Sobre el buscador y el asistente, haz que solo reciba un input a la vez aunque se mantenga presionado el botón.
   Así, si el cliente quiere abrir y cerrar, tendría que hacer la combinación de botones varias veces, lo cual ya no
   se puede hacer por accidente».

## 1. Las IAs, en la carpeta IAS

- **Qué va en la carpeta `IAS`:** los modelos entrenados.
  - `rocola_ia.bin`: el DSEE.
  - `rocola_eq_ia.bin`: el EQ automático.
  - `rocola_ia_genero_*.bin`: el DSEE por género, con sus copias `.anterior-N`.
- **Dónde está.** Junto a `Rocola.exe`, o dentro de `ROCOLA_DATOS` si se usa.
- **Lo que no es un modelo se queda donde estaba:** el material para entrenar (`rocola_ia_datos.bin`, `.idx`,
  `.generos.tsv`), la configuración y las tablas.
- **Al arrancar, los ordena sola.**
  - Los modelos que encuentre sueltos en la carpeta de datos los pasa a `IAS`; la crea si no existe.
  - Es un cambio de nombre en el mismo disco: no se copia nada y nunca queda un modelo a medias.
  - **Nunca se borra un modelo.** Si en `IAS` ya había uno con ese nombre, el suelto se toma por el recién puesto
    (como se hacía antes, dejándolo junto a `Rocola.exe`). Pasa a `IAS`, y el que estaba se guarda como
    `.anterior-1`, `.anterior-2`…
- **Dónde los busca**, por orden:
  1. `IAS` de la carpeta de datos;
  2. la carpeta de datos, por si alguno no se pudo pasar (carpeta de solo lectura o archivo en uso);
  3. `IAS` junto al programa;
  4. junto al programa.

  Así una instalación vieja sigue funcionando aunque no se pueda ordenar.
- **Los de género.** Si el de `IAS` está roto, vale el de la carpeta siguiente.
- **En Ctrl+F3 → Sonido → Mejoras con IA**, la hoja dice que los modelos van en la carpeta IAS.
- **Los programas de la carpeta `ia`:**
  - `instalar_dsee_genero.bat` instala los de género en `IAS`. Si había uno de ese género, suelto o en `IAS`, lo
    guarda en `IAS` como `.anterior-N`. Si se le da la carpeta `IAS` misma, no crea otra dentro.
  - `entrenar_dsee.bat`, al arrastrar la carpeta de la rocola, toma como «instalado» el modelo de `IAS` antes que la
    copia de la carpeta `ia`.
- **Una precaución.** Si se vuelve a poner una versión de la rocola anterior a hoy, esa no mira dentro de `IAS`: usaría
  la curva clásica del DSEE y nada de EQ automático ni modelos por género. Para volver atrás, saca los modelos de `IAS`
  y déjalos junto a `Rocola.exe`.

## 2. ROJO + ARRIBA y ROJO + ABAJO: una vez por pulsación

- **Lo que pasaba con el buscador.** ROJO + ARRIBA no miraba si los botones seguían apretados. Con los dos apretados, la
  repetición del teclado (unas treinta por segundo) lo abría y lo cerraba sin parar: abrir tenía el tope de 0,7 s y
  cerrar, ninguno.
- **Lo que pasaba con la ayuda.** ROJO + ABAJO ya descartaba la repetición, pero por una marca de Windows que no llega
  con todos los teclados ni con todos los programas que convierten los botones en teclas.
- **Ahora.** Cada combinación hace lo suyo una vez y queda bloqueada hasta que se suelta uno de sus dos botones.
  Mientras sigan apretados no vuelve a hacer nada, ni mueve la selección por debajo. Para abrir y cerrar hay que
  soltar y volver a apretar.
- **Rebote.** Un soltar de menos de 80 ms no cuenta. Es el rebote del botón, o un programa de botones que manda
  apretar y soltar seguidos mientras se mantiene. Una persona que suelta y vuelve a apretar tarda más que eso.
- **Si el aviso de soltar se pierde** (por ejemplo, porque lo recibió otra ventana): pasado 1,5 s sin ninguna pulsación
  de esos botones, la siguiente cuenta como nueva. Manteniendo apretado, Windows repite como mucho al segundo de
  empezar y luego cada pocos milisegundos, así que esa pausa solo ocurre si se soltó.
- **Sigue igual:**
  - el tope de aperturas: no dos en menos de 0,7 s ni más de seis en 30 s; cerrar, siempre;
  - lo demás de la ayuda: la despedida a los 15 s, mantener una flecha para recorrer la lista y la moneda.

## Cómo se comprobó

- **`iasdisparo`** (nueva; 30 comprobaciones; pasa con y sin tarjeta). Usa una carpeta de datos temporal y el reloj a
  mano.
  - **La carpeta IAS:**
    - sin modelos no se crea nada;
    - se pasan los cuatro sueltos (DSEE, EQ, cumbia y su copia de antes), enteros;
    - el material, la configuración y un `.binario` se quedan;
    - un modelo nuevo dejado suelto manda, y los de antes quedan como `.anterior-1` y `.anterior-2`;
    - uno que se quedó suelto se sigue leyendo;
    - los de género se cargan desde `IAS` y desde la carpeta de datos, y si el de `IAS` está roto vale el suelto;
    - al arrancar, primero se ordena y luego se carga.
  - **Un disparo por pulsación:**
    - 4,5 s apretados con repetición, nada;
    - con la repetición empezando casi a 1 s, nada;
    - un rebote de 20 ms no cuenta, y soltar y apretar sí;
    - un programa de botones que suelta y aprieta cada 30 ms, nada;
    - si el soltar se perdió, la pulsación tras 1,6 s vale;
    - funciona aunque el reloj de Windows dé la vuelta;
    - el buscador y la ayuda pasan por su seguro, y lo apuntan al pulsar y al soltar.
- **`ia/test_genero_dsee.py`:** el instalador escribe en `IAS`, aparta el suelto de antes y no anida `IAS/IAS`. Las 14
  pruebas de `ia/` pasan.
- **`bloqueos`:** los dos sitios nuevos que tocan el disco al arrancar quedan anotados, con su motivo (renombrar unos
  pocos archivos, una vez).
- **Siguen pasando:** `carpeta`, `asistentecombo`, `mejorasia` y `ventanaf3`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo; ninguno nuevo.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:**

- **Los botones del bar.** Las pulsaciones se simularon con el reloj a mano; aquí no hay teclado ni el programa de
  botones del bar.
- **Ordenar los modelos en la carpeta real de la rocola.** Se probó en una carpeta temporal de Linux, no en Windows XP
  ni en la carpeta del bar.
