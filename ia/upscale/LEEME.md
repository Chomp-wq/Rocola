# Kit de upscaling — flujo completo

Orden de los dobles clic:

1. **`1_instalar.bat`** — instala todo una vez (ver detalle abajo).
2. **`3_escanear.bat`** — mide TODA tu biblioteca y arma `calidad_biblioteca.csv` +
   `peores_canciones.txt` (las mas danadas: donde el upscaling brilla).
3. **`4_muestras.bat`** — toma un abanico de las canciones MAS danadas y arma el A/B ciego
   (`piloto_danadas\ciego`). **Esta es la prueba que decide todo** — con archivos danados de
   verdad, no con rips buenos. Escucha X/Y sin ver, anota, abre `clave.txt` al final.
4. **`5_procesar.bat`** — SOLO si el A/B te convence: procesa TODA la biblioteca a una carpeta
   nueva en otro disco (mejora las danadas, copia las buenas tal cual, conserva etiquetas y
   portada, reanuda si lo cierras). Esa carpeta nueva tambien te sirve de RESPALDO.

`2_piloto.bat` sigue existiendo para probar con una carpeta cualquiera que tu elijas.

---

# Kit de upscaling (Fase 1: piloto) — que un MP3 suene como FLAC

La ruta completa esta en la conversacion del proyecto; resumen: **NO se procesa en la rocola** —
se mejora la biblioteca UNA VEZ, offline, en tu PC potente (RTX 4070 Super), con **Apollo**
(restauracion de musica con perdida, Tsinghua; GAN entrenada para deshacer el dano del MP3).
La rocola (cualquiera de las ramas, hasta la de XP) solo reproduce los archivos mejorados.

## MODO FACIL (recomendado): dos dobles clics

1. Doble clic a **`1_instalar.bat`** — instala todo solo. Si pide cerrar y volver a abrir, hazlo
   (puede pedirlo 2-3 veces). Termina cuando diga **TODO LISTO**.
2. Doble clic a **`2_piloto.bat`** — arrastra tu carpeta de canciones a la ventana, Enter, y espera.
   El resultado queda en `piloto_resultado\`.

Lo de abajo es lo mismo pero a mano (por si prefieres la consola).

## Puesta a punto (una vez, en tu PC — Windows)

```bat
:: 1) entorno de python
py -3.11 -m venv venv
venv\Scripts\activate

:: 2) pytorch con CUDA (para la 4070 Super)
pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu124

:: 3) apollo (dentro de esta carpeta ia/upscale/)
git clone https://github.com/JusperLee/Apollo
pip install -r Apollo\requirements.txt
::   (si algo de esa lista falla, con esto suele bastar:)
::   pip install numpy einops rotary-embedding-torch huggingface_hub soundfile matplotlib

:: 4) ffmpeg
winget install Gyan.FFmpeg     (reabre la consola despues)

:: 5) VERIFICAR TODO (y descargar el modelo, ~180MB, una sola vez)
python 00_verificar.py --descargar
```

## El piloto

1. Copia **12-20 canciones REALES de la rocola** (idealmente de 128-192 kbps, generos variados:
   cumbia, regional, K-pop, metal, 80s...) a una carpeta, p.ej. `C:\muestras`.
2. `python 01_piloto.py --entrada C:\muestras --salida C:\piloto`
   (si la GPU se quedara corta de memoria: agrega `--seg 6`)
3. Escucha. El protocolo importa:
   - Empieza por `ciego/`: pares X/Y **sin saber cual es cual** y con el MISMO volumen
     (RMS igualado: nadie te enganya con "mas fuerte = mejor"). Anota cual prefieres de cada par
     y LUEGO abre `clave.txt`.
   - Que escuchar: platillos/hi-hats (el "aire"), las "s" de las voces, la definicion general.
     Primero con audifonos decentes; despues en las bocinas de la rocola (el juez final).
   - `espectros/` te deja VER el antes/despues (la banda alta reconstruida) y `reporte.txt` trae
     el acantilado medido antes -> despues y la velocidad (x tiempo real) por cancion.

## Interpretacion y siguiente paso

- Si en ciego prefieres la mejorada en la mayoria de los pares -> el camino vale: seguimos con la
  **Fase 2** (fine-tuning de Apollo con TU musica: pares degradados con TU encoder) y la
  **Fase 3** (procesar toda la biblioteca con prioridad por acantilado + lo mas sonado, con
  reanudacion). Los scripts 02 y 03 se construyen en cuanto des el veredicto del piloto.
- Si no convence: no se gasta un peso mas y nos quedamos con el DSEE espectral + IA de envolvente
  (que ya estan en la rama) — costo cero por cancion.

## Notas honestas

- Apollo reconstruye PERCEPTUALMENTE: inventa detalle plausible donde el MP3 borro informacion.
  En escucha ciega se acerca mucho al original; no es una recuperacion bit a bit (imposible).
- Licencia del checkpoint Apollo: CC-BY-SA-4.0 (uso en tu rocola sin problema; da credito).
- De archivos ya buenos (256-320 kbps) casi no hay ganancia — el reporte del acantilado te dice
  donde SI vale la pena. Y lo MAS pedido, mejor re-consiguelo en calidad original (CD/compra):
  ninguna IA le gana al original.
- `test_util.py` prueba las utilerias del kit sin GPU (ya corrio en verde); `00_verificar.py`
  valida lo que solo tu maquina puede validar (CUDA, Apollo, ffmpeg).
