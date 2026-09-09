## DESPLIEGUE (integrado con el release, portable entre discos)

El motor va CON la rocola, sin correr nada a mano:
1. Junto a `Rocola.exe` (en el MISMO disco que la carpeta `musica`) deja la carpeta **`ia`**
   completa (con `ia\upscale\venv` ya instalado por su `1_instalar.bat`, e `ia\tiempo_real`).
2. Enciende DSEE ULTRA (Ctrl+F3). La rocola ARRANCA el motor sola y oculto, y lo MANTIENE vivo
   (si se cae, lo revive; revisa cada ~10s). Nunca abres `iniciar_motor.bat`.
3. Por defecto, el motor por demanda guarda cache y temporales en
   `ia/tiempo_real/cache` y `cache/tmp`. `ia_cache=` permite otra ruta; revisala al mover
   la instalacion. El kit offline usa tambien temporales del sistema, que pueden estar en C:.

`iniciar_motor.bat` queda SOLO para depurar (ver mensajes de Apollo la primera vez).

# IA en vivo (Apollo por demanda) para la rocola-hd

Procesa por demanda canciones encoladas o reproducidas, con interruptor. No hace falta
procesar toda la biblioteca: conserva en cache las salidas terminadas y verificadas.
Reutilizarlas evita repetir la inferencia, pero la apertura y decodificacion aun cuestan tiempo.
El procesamiento no garantiza una mejora auditiva: compruebalo con las muestras A/B.

## Requisito importante

La maquina donde corre la Rocola necesita el motor y sus dependencias/checkpoint de Apollo.
Con CUDA disponible usa GPU; el cargador admite CPU cuando no hay CUDA, con un coste y
tiempo potencialmente mucho mayores. En un gabinete modesto puede convenir procesar offline
desde otra PC. No se garantiza que termine antes de que empiece una cancion.

## Como se usa (automatico, sin cuidar terminales)

1. Instala la carpeta `upscale` una vez (su `1_instalar.bat` en OK). Deja las carpetas `ia`
   junto a la rocola (la rocola busca `ia\upscale\venv` e `ia\tiempo_real` a su lado o
   hasta 5 niveles arriba, igual que busca la carpeta `musica`).
2. En la rocola: **Ctrl+F3 -> DSEE ULTRA** enciende/apaga la IA en vivo. Al encenderla, la
   rocola **arranca el motor SOLA y OCULTO** (sin ventana negra que cuidar; tampoco parpadea
   ningun CMD al poner canciones). Con `ia_vivo=1` en config, arranca encendida. Al cerrar la
   rocola, el motor que ella lanzo se cierra tambien.

`iniciar_motor.bat` sigue existiendo por si quieres arrancar el motor a mano y VER sus mensajes
(util para depurar la primera vez).

## Que pasa por dentro

- Al ENCOLAR una cancion, la rocola le pide al motor que la procese (deja un `.job`).
  Puede quedar lista antes de su turno, segun la cola y la velocidad de inferencia.
- Al sonar: si la version IA esta lista, suena esa; si no, suena la ORIGINAL al instante (la
  rocola no espera a la inferencia). Se reutiliza en una reproduccion posterior si termina
  correctamente y la cache no se ha limpiado.
- Todo se guarda en `ia\tiempo_real\cache\listos` y TU CARPETA DE MUSICA NO CRECE NUNCA:
  los originales no se tocan. En la cache, cada cancion mejorada es **WAV flotante de 32 bits**,
  sin otra compresion con perdida ni recorte antes del DSP. Ocupa unos 21 MB por minuto
  (aproximadamente 9 veces MP3 320); se mantiene el tope de espacio. Si una cancion ya era
  de calidad completa solo queda una marca `.orig` de unos bytes (suena el original tal cual).
- La cache se limpia sola al pasar el tope (20 GiB por defecto: unas 1000 min de WAV estereo
  a 44,1 kHz, aproximadamente 330 canciones de 3 min; se quita
  lo menos usado, y lo borrado simplemente se vuelve a procesar si algun dia se repite).
- BLINDAJE: canciones de mas de 15 min o archivos que ffprobe no puede leer (tipico de discos
  rescatados: un mp3 corrupto puede "decodificar" horas de basura y comerse toda la RAM) NO
  pasan por la IA: las largas se marcan de inmediato; los fallos de lectura se reintentan
  hasta tres veces con espera antes de marcar el original. El motor ademas corre
  con prioridad BAJA: la rocola y Windows siempre respiran primero.
- UN SOLO MOTOR, COLA DE UNO EN UNO: un candado del sistema (cache\motor.lock) hace IMPOSIBLE
  que corran dos motores a la vez sobre la misma cache (dos Apollos = doble RAM y VRAM), y los
  trabajos se procesan estrictamente uno por uno, en el orden en que se encolaron. El latido
  (motor.vivo) late cada ~3s desde un hilo propio, desde el segundo cero: la rocola ya no
  relanza motores mientras Apollo carga ni a mitad de un trabajo largo.

## Ajustes (en rocola_config.txt, junto a Rocola.exe)

- `ia_vivo=1`      enciende la IA en vivo al arrancar (o el switch DSEE ULTRA en Ctrl+F3).
- `ia_gpu=auto`    que GPU hace los calculos de IA (auto, 0, 1, ...). Mas facil: el SELECTOR en
                   Ctrl+F3 (muestra los nombres reales; al cambiarla el motor renace solo en esa
                   GPU). La lista la publica el motor en cache\gpus.txt al arrancar.
- `ia_mezcla=auto` cuanta IA: auto (recomendado) o un numero 0..100.
- `ia_cache=RUTA`  carpeta de cache (por defecto `ia/tiempo_real/cache`).
                   La Rocola pasa la ruta al motor que inicia; si lo abres manualmente,
                   debes indicarle la misma ruta. El ajuste se conserva al guardar opciones.

El modelo se cambia igual que en offline (upscale\modelo.txt): el motor usa el mismo cargador.

## Si "no aparece nada en listos" (diagnostico)

1. La rocola y el motor ahora usan LA MISMA cache sola (`ia\tiempo_real\cache`). No hay que
   configurar rutas. Si pusiste `ia_cache=` en el config, quitalo (deja que coincidan solos).
2. Arranca el motor en modo VISIBLE con `iniciar_motor.bat` y mira:
   - Debe decir "Apollo LISTO". Si truena al cargar Apollo, el error sale ahi y en
     `cache\motor_log.txt`.
   - Pon una cancion en la rocola con DSEE ULTRA encendido: en el log debe aparecer
     "procesada -> ...". Si aparece, en `cache\listos` ya hay un `.wav` (o una marca `.orig`
     si esa cancion ya era de calidad completa y no necesitaba IA).
3. En la rocola, la etiqueta "Reproduciendo" muestra:
   - `[DSEE]` = suena con el reconstructor espectral (respaldo instantaneo, 1a vez que suena
     esa cancion, o DSEE normal).
   - `[IA]`   = suena con la version de Apollo (ya procesada; ponla por 2a vez para verla).
   Si SIEMPRE ves `[DSEE]` y nunca `[IA]`, el motor no esta produciendo: revisa el log.
4. Recuerda: la rocola debe ser el `Rocola.exe` RECOMPILADO desde la rama rocola-hd.
