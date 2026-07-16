# IA en vivo (Apollo por demanda) para la rocola-hd

Procesa con IA SOLO las canciones que suenan, cuando suenan, con interruptor. No hay que
procesar la biblioteca entera: lo que se pone se mejora y se guarda (cache); si se repite, es
instantaneo. Con el uso, tu biblioteca se vuelve HD sola, solo con lo que de verdad se escucha.

## Requisito importante

La maquina DONDE CORRE LA ROCOLA debe tener la GPU (tu RTX 4070 Super) y este motor abierto.
Si la rocola corre en un gabinete sin GPU, esto no aplica: ahi conviene el procesado offline
(carpeta upscale) desde tu PC potente.

## Como se usa (automatico, sin cuidar terminales)

1. Instala la carpeta `upscale` una vez (su `1_instalar.bat` en OK). Deja las carpetas `ia`
   junto a la rocola (la rocola busca `ia\upscale\venv` e `ia\tiempo_real` a su lado o
   hasta 5 niveles arriba, igual que busca la carpeta `musica`).
2. En la rocola: **Ctrl+F4** enciende/apaga la IA en vivo. Al encenderla, la rocola **arranca
   el motor SOLA y OCULTO** (sin ventana negra que cuidar). Con `ia_vivo=1` en config, arranca
   encendida. Al cerrar la rocola, el motor que ella lanzo se cierra tambien.

`iniciar_motor.bat` sigue existiendo por si quieres arrancar el motor a mano y VER sus mensajes
(util para depurar la primera vez).

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

## Si "no aparece nada en listos" (diagnostico)

1. La rocola y el motor ahora usan LA MISMA cache sola (`ia\tiempo_real\cache`). No hay que
   configurar rutas. Si pusiste `ia_cache=` en el config, quitalo (deja que coincidan solos).
2. Arranca el motor en modo VISIBLE con `iniciar_motor.bat` y mira:
   - Debe decir "Apollo LISTO". Si truena al cargar Apollo, el error sale ahi y en
     `cache\motor_log.txt`.
   - Pon una cancion en la rocola con DSEE ULTRA encendido: en el log debe aparecer
     "procesada -> ...". Si aparece, en `cache\listos` ya hay un `.wav`.
3. En la rocola, la etiqueta "Reproduciendo" muestra:
   - `[DSEE]` = suena con el reconstructor espectral (respaldo instantaneo, 1a vez que suena
     esa cancion, o DSEE normal).
   - `[IA]`   = suena con la version de Apollo (ya procesada; ponla por 2a vez para verla).
   Si SIEMPRE ves `[DSEE]` y nunca `[IA]`, el motor no esta produciendo: revisa el log.
4. Recuerda: la rocola debe ser el `Rocola.exe` RECOMPILADO desde la rama rocola-hd.
