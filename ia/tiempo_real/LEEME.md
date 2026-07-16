# IA en vivo (Apollo por demanda) para la rocola-hd

Procesa con IA SOLO las canciones que suenan, cuando suenan, con interruptor. No hay que
procesar la biblioteca entera: lo que se pone se mejora y se guarda (cache); si se repite, es
instantaneo. Con el uso, tu biblioteca se vuelve HD sola, solo con lo que de verdad se escucha.

## Requisito importante

La maquina DONDE CORRE LA ROCOLA debe tener la GPU (tu RTX 4070 Super) y este motor abierto.
Si la rocola corre en un gabinete sin GPU, esto no aplica: ahi conviene el procesado offline
(carpeta upscale) desde tu PC potente.

## Como se usa

1. En tu PC de la rocola, ya con la carpeta `upscale` instalada (su `1_instalar.bat` en OK).
2. Doble clic a **`iniciar_motor.bat`** y arrastra la carpeta donde esta `Rocola.exe`. Deja esa
   ventana ABIERTA. La primera vez carga Apollo unos segundos.
3. En la rocola, **Ctrl+F4** enciende/apaga la IA en vivo (sale un aviso). Se recuerda en config.

## Que pasa por dentro

- Al ENCOLAR una cancion, la rocola le pide al motor que la procese (deja un `.job`). Como casi
  siempre hay algo sonando antes, para cuando le toca YA esta lista.
- Al sonar: si la version IA esta lista, suena esa; si no, suena la ORIGINAL al instante (la
  rocola NUNCA se traba esperando) y queda lista para la proxima vez.
- Todo se guarda en `ia_cache\listos`. La cache se limpia sola al pasar el tope (20 GB por
  defecto, se quita lo menos usado).

## Ajustes (en rocola_config.txt, junto a Rocola.exe)

- `ia_vivo=1`      enciende la IA en vivo al arrancar (o usa Ctrl+F4).
- `ia_mezcla=auto` cuanta IA: auto (recomendado) o un numero 0..100.
- `ia_cache=RUTA`  carpeta de cache (por defecto `ia_cache` junto a Rocola.exe; debe COINCIDIR
                   con la que le das al motor en iniciar_motor.bat).

El modelo se cambia igual que en offline (upscale\modelo.txt): el motor usa el mismo cargador.
