# IA propia de la rocola (modelo entrenado por el dueno con su biblioteca)

## Validacion del corpus DSEE — 11 de octubre de 2026

El entrenador directo tambien rechaza lotes cuyo extractor no sea el contrato
actual (2), aunque se permita material sin origen. Usar
`preparar_candidato_dsee.py` para preparar un candidato compatible; no editar
el numero del extractor ni alterar el corpus para eludir este control.
Los grupos sin ejemplos seleccionados no cuentan para los minimos de
entrenamiento/validacion/prueba. Un rechazo por grupos insuficientes requiere
mas material independiente realmente seleccionado, no lotes vacios.
Los modelos instalados y los datos originales no se sustituyen.

## Eficiencia y repeticion de entrenamiento — 9 de octubre de 2026

DSEE lee entradas y objetivos RIAD juntos por lote, sin cargar todo el corpus en RAM ni
alterar ejemplos, semillas, permutaciones, normalizacion, perdida o Adam. La prueba
`test_eficiencia_entrenamiento.py` compara lotes y pesos finales exactamente contra la
lectura anterior, tanto con MSE como con la perdida de envolvente.

`presupuesto_entrenamiento.py` debe permanecer junto a los tres entrenadores. Antes de
importar NumPy limita BLAS segun la afinidad disponible: dos hilos de CPU usan un hilo de
entrenamiento; una maquina de 32 usa 16 por defecto. No existe un tope universal de uno
o dos para equipos potentes. La politica deja margen a la musica y la interfaz; no cambia
modelos instalados, la afinidad ni las opciones del reproductor.

Para repetir un entrenamiento con un presupuesto fijo, en una consola PowerShell nueva:

```powershell
$env:ROCOLA_HILOS_ENTRENAMIENTO = "1"
python entrenar_ia.py --datos rocola_ia_datos.bin
```

Se admite de 1 al numero de CPU disponibles (tambien todas, si no se necesita dejar margen).
El informe registra los hilos solicitados y la version NumPy. La repeticion exacta requiere
la misma entrada, parametros, semilla, numero de hilos y pila NumPy/BLAS; no se garantiza
igualdad bit a bit entre maquinas, bibliotecas BLAS o presupuestos distintos. Ejecutar desde
una consola nueva asegura que el presupuesto se fije antes de cargar BLAS.

## Candidatos seguros — 29 de septiembre de 2026

Entrenar no activa ni recupera modelos instalados. `entrenar_dsee.bat` deja candidatos e informes
aislados en `entrenado/dsee-*` y `entrenado/eq-*`; si hay alguno aceptado numericamente termina
correctamente, pero indica **PCM/escucha pendientes**. Nunca copiar un candidato rechazado al reproductor.
CLI directo deja tambien el informe junto al candidato aislado; `--informe` y `--candidato`
deben ser destinos nuevos, sin alias entre si ni con datos, indice, genero, modelos o paridad.
Se valida ANTES de entrenar; no se sobrescriben informes anteriores.

DSEE usa muestreo determinista estratificado (no un paso fijo sincronizado con los cinco cortes),
normalizacion solo de entrenamiento, L2 pequena y una semilla por defecto. Hasta tres semillas
se eligen por VALIDACION, nunca por prueba. `--ajustar-instalado` reutiliza pesos de la misma
arquitectura con LR menor, regularizacion al modelo inicial y checkpoint de epoca cero: si los
checkpoints empeoran validacion, conserva ese inicial. Su origen de entrenamiento sigue siendo desconocido.
No se amplian redes por defecto. El guardian mide tambien float32/clamp runtime y bloquea
regresiones mayores de 10% en clases con al menos 200 filas.

AutoEQ normal y arrastrado usan siempre el evaluador agrupado por album/copia exacta, comparador
instalado actual y PRUEBA independiente. Solo acepta huellas limpias `versionHuella=2`.
`--diagnostico-legacy` inspecciona exclusivamente versiones 0/1, separado de 2 y futuras;
es `legacy_no_publicable`, no certifica origen ni permite sustituir el instalado. Reanalizar las
mediciones antiguas desde el original con la rocola actual; no modificar ni recodificar la musica.

Copiar .bin y .idx juntos, conservando el mismo nombre base; copiar tambien el sidecar opcional
`rocola_ia_datos.generos.tsv` (hash hexadecimal minusculo de 16 caracteres, TAB, genero; ultimo
registro gana, vacio significa desconocido). Genero sirve para cobertura, no como entrada de red.

`evaluar_pcm_dsee.py` usa el renderer C# real `pruebas/renderdsee.cs` (sin dispositivo ni UI),
no reconstruye PCM a partir de X28/Y12. Manifest JSON:

```json
{"pares":[{"referencia":"master.wav","degradada":"codec.wav","genero":"rock",
"tipo":"musica","degradacion_conocida":true,"referencia_sin_perdidas":true,
"alineacion_verificada":true}]}
```

```text
python evaluar_pcm_dsee.py --manifest pares.json --renderer renderdsee.exe --rocola Rocola.exe --candidato candidato.bin --instalado instalado.bin --destino ab_nuevo
```

Presupuesto: hasta 20 pares y los primeros 30s de cada uno; la duracion COMPLETA debe coincidir.
WAV PCM16/32 o float32, mismo formato y alineacion comprobada. Mide PCM, espectro, envolvente,
ataques, imagen estereo y picos; bloquea regresiones y escribe A/B float a RMS/margen comun.
Las declaraciones de master/alineacion las verifica quien prepara el manifest, no se deducen del WAV.
Un fixture sintetico solo valida el circuito; no es evidencia musical ni anula un rechazo de envolvente.
La escucha autorizada sigue pendiente aunque ambas politicas numericas pasen.

## Antecedente de calidad — 22 de septiembre de 2026

Historicamente se publicaba tras el guardian; ahora solo se acepta un candidato aislado si mejora
al menos 5% el MSE global frente a su curva fija y ninguna banda empeora más de 10%.
El código 2 significa rechazo por calidad; se conserva el modelo anterior.
Esto no sustituye validación por canciones independientes ni escucha a volumen igualado.

Copiar la carpeta de herramientas completa: `entrenar_ia.py` ahora necesita
`calidad_dsee.py` a su lado. Esta dependencia es del entrenador, no del reproductor.
Los modelos existentes no necesitan convertirse ni se reemplazan automáticamente.
AutoEQ usa huellas de 12 campos con version actual 2 y normaliza solo con entrenamiento.

Ver [implementación, pruebas y pendientes detallados](../MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md).

## LO CORTO: que archivos me llevo de la rocola y que hago con ellos

Junto a `Rocola.exe` estan los datos de entrenamiento (DSEE requiere tambien su `.idx`):

| Archivo | Que entrena | Tamano tipico |
|---|---|---|
| `rocola_ia_datos.bin` | el **reconstructor de agudos** del DSEE (`rocola_ia.bin`) | de cientos de MB a varios GB |
| `rocola_eq.txt` | el **objetivo del EQ automatico** (`rocola_eq_ia.bin`) | unos pocos MB |

No hace falta llevarse la musica para entrenar envolventes. Llevar `.idx`, sidecar opcional y los
modelos instalados actuales para comparar; pares de audio aparte para verificar PCM.

Con los dos en una USB, en la PC donde entrenas: **arrastralos sobre `ia\entrenar_dsee.bat`**.
Deja candidatos separados en `ia\entrenado\`, nunca modelos activados. Se puede arrastrar uno solo (entrena esa mitad y avisa de lo
que falto) o la carpeta entera donde esten (los busca dentro).

Lo unico que necesita la PC es **Python + numpy** (`pip install numpy`).

### Que NO se entrena, y por que

El resto de lo automatico no es una red: son medidas que la rocola ya lleva sola y reglas
sobre esas medidas. No hay nada que entrenar ni ningun archivo que traer.

- **Volumen parejo** (`rocola_volumen.txt`): se mide cancion por cancion en el pre-analisis.
- **Fuerza del EQ automatico** y **cantidad de graves**: salen de cuanto se puede fiar uno de
  lo medido (ver mas abajo). Suben solas segun avanza el pre-analisis.
- **Des-recorte, realce de agudos y fundido**: ajustes fijos, no aprendidos.
- **El acantilado** (donde el MP3 talo los agudos): lo detecta el propio DSEE al vuelo.

### De donde sale el material y como hacer que crezca mas rapido

`rocola_ia_datos.bin` lo va juntando la rocola sola mientras esta encendida y nadie la usa
(pre-analisis). Cada cancion se mira varias veces, y cada vez se toman cuadros de un sitio
distinto, asi que el archivo sigue creciendo aunque ya haya pasado por toda la biblioteca.

Si tienes prisa: en Ctrl+F3, **REPASO A FONDO**. Se dedica a esto sin esperar a que la rocola
este en reposo y saca de cada cancion unas ocho veces mas material. Dejalo una noche.


- `rocola_ia.bin` es la red de envolvente espectral ENTRENADA CON LA MUSICA DEL LOCAL
  (misma red y formato RIA1 que en la rama rocola-hd; paridad C#<->numpy verificada: 2e-6).
- Para activarla: copia `rocola_ia.bin` en la carpeta `IAS` junto a `Rocola.exe` (o suelto junto a `Rocola.exe`:
  al arrancar, la rocola lo pasa a `IAS` y guarda el de antes como `.anterior-N`) y reinicia. En Ctrl+F3, la nota
  del reconstructor dira "con IA PROPIA". Sin el archivo, el DSEE usa su curva clasica (cero cambio).
- Solo actua con el DSEE espectral encendido y sobre canciones con agudos cortados.
  La red distribuida es 28 -> 96 -> 96 -> 12: 13.260 parametros y 13.056 multiplicaciones
  por ventana y canal, aparte de FFT/reconstruccion. Medir el coste total en el CPU destino.
  El entrenador CLI predetermina 28 -> 48 -> 48 -> 12 (4.332 parametros); no confundirlo
  con el modelo distribuido. `--capas` permite elegir la arquitectura del candidato.
- `ia_vectores_prueba.txt` son los vectores de paridad de ESTE modelo (los usa la bateria de pruebas).
- Para RE-entrenar con mas musica: rama rocola-hd, `ia\entrenar_con_mi_musica.bat`.

## El contrato (RIA1) — no romperlo

Manda la ROCOLA: la red se aplica en `SpectralTap.RegenNeural` y la cosecha sale de `MakePair`.
`contrato_dsee.py` transcribe esas funciones y `BuildIaFeatures` paso a paso, con su mismo redondeo
float32, y es lo que usa `entrenar_ia.py` (version del extractor `EXTRACTOR = 2`).
- entrada[0..23]: log10 de la energia media por bin (|X/N|^2) de 24 bandas log de 200 Hz al corte,
  restada su media; [24] = esa media; [25] = corte/20000 **con el corte redondeado a bin**;
  [26] = planitud (ultimas 8 bandas); [27] = 0.
- salida[0..11]: log10(energia objetivo / energia de la costura) de 12 sub-bandas log del corte al
  `topBin` de la rocola: el menor entre 20,5 kHz, 95 % de Nyquist, 2·corte−2 bins y ny−4.
  Topes −4 y **+0,1**. Si una sub-banda queda vacia el par no vale (como en la rocola).
- Ventana raiz-de-Hann, cuadro 2048 (en esta rama es fijo: cosecha y reproduccion), solape 50 %.
  El contrato cubre tambien 4096 porque lo usa la rama HD; aqui esos casos se saltan diciendolo.

La version 1 (el extractor Python anterior) usaba 20 kHz de techo, corte sin redondear y tope +0,5:
un modelo entrenado con `--musica` o con audio sintetico aprendia bandas que no son las que la
rocola rellena. Lo que junta la propia rocola nunca tuvo ese problema: `MakePair` no ha cambiado
desde que existe (3 de agosto de 2026). Los modelos RIA1 instalados siguen funcionando igual: la
reproduccion no cambio.

`paridad_extractor.txt` son los vectores de paridad del extractor (28 entradas y 12 objetivos para
espectros y senales reproducibles bit a bit). `pruebas/iaparidad.cs` los compara con la rocola y
ademas recorre TODOS los cortes a seis frecuencias de muestreo contrastandolos con este contrato.
Si se cambia el contrato a proposito: `python3 contrato_dsee.py --generar-paridad paridad_extractor.txt`
y subir `EXTRACTOR`.

## Validacion de verdad: de que cancion viene cada ejemplo

La rocola escribe, junto a `rocola_ia_datos.bin`, el archivo `rocola_ia_datos.idx`: de que cancion
(y de que carpeta) viene cada ejemplo, solo como huellas. **Traete los dos.** Con el, el entrenador
separa por cancion/carpeta ANTES de mirar un cuadro: entrenamiento, validacion (elige el mejor
checkpoint) y prueba (decide aceptacion numerica, nunca activa). Ningun grupo esta en dos sitios.

Sin el `.idx` (todo lo cosechado antes de esta version) no se inventan grupos: se entrena para mirar,
pero sale el aviso VALIDACION NO INDEPENDIENTE, el codigo es 3 y no se reemplaza ningun modelo.
Opciones: `--agrupar artista|carpeta|cancion` (defecto artista) y `--incluir-sin-origen` (los suma
solo al entrenamiento; el resultado tampoco se publica). Con `--musica`, cada canal de cada WAV da
sus propios ejemplos (una mezcla mono anulaba lo que estuviera en contrafase) y los dos canales
van siempre juntos.

## Cuando se acepta numericamente un candidato (sin reemplazar)

Con `--genero`, filtrar no vuelve a repartir ni mezcla canciones entre particiones. Desde el 15-10-2026 se
comprueban de nuevo los mismos minimos: 1/3/3 grupos y 500/200/200 ejemplos en entrenamiento/validacion/prueba.
Si un genero no los conserva, se explica el motivo y se termina antes de entrenar o guardar el candidato.
La separacion y la diversidad son comprobaciones distintas; pocos grupos no significan por si solos que hubo mezcla.

El candidato se mide sobre la particion de PRUEBA contra la curva fija y contra el modelo que ya
tienes (arrastra tu `rocola_ia.bin` junto al material, o se usa el de esta carpeta). Se acepta solo si
gana a la curva (5 % global, ninguna banda 10 % peor) y al instalado (2 % global, ninguna banda 10 %
peor). Es una politica inicial para no publicar regresiones, no una prueba de que suene mejor: eso se
decide escuchando A/B a volumen igualado. Cada entrenamiento deja `informe_dsee.json` (sin rutas ni
fechas: la misma entrada da el mismo informe) y los vectores llevan la huella SHA-256 de su modelo.
Codigos DSEE: 4 candidato aceptado numericamente y aislado; 2 rechazo numerico; 3 no verificable.
En todos los casos numericamente finitos se guarda un candidato diagnostico, nunca el instalado.
El informe trae ademas los errores por clase de senal (nivel, tonalidad, brillo, corte y, con WAV,
ataques) con avisos si el candidato empeora en alguna, y cuanto se aparta lo evaluado de lo que
calcularia la rocola en float32: si se aparta mas de 1e-3, se rechaza el candidato.

## Activacion explicita (herramienta historica)

Solo tras PCM/escucha y autorizacion, `publicar_dsee.py` puede publicar modelo, vectores e informe como una version en
`modelos/dsee-<huella>/` con su manifiesto, y se activa cambiando un solo archivo (`modelos/ACTIVO`).
`rocola_ia.bin` y `ia_vectores_prueba.txt` de esta carpeta (y de `entrenado/`) son copias de la
version activa. El entrenador ya NO repara ni activa esas copias automaticamente.
`python publicar_dsee.py --estado`, `--recuperar` o `--activar dsee-XXXX` para volver a una anterior.

## El EQ y el bajo van SOLOS (no hay perilla que ajustar)

De fabrica, en Ctrl+F3, el "EQ automatico" y los "Graves (bajo profundo)" llevan puesta la casilla
**Automatico**: las perillas quedan apagadas y lo decide el programa cancion por cancion, con lo que
el analisis del DSEE ya midio de cada archivo.

- **Fuerza del EQ**: sale de cuanto se puede FIAR uno del objetivo. Con menos de 10 canciones sanas
  medidas no corrige nada; segun el pre-analisis avanza sube del 31% al 75% (85% con el modelo
  `rocola_eq_ia.bin` cargado, porque entonces el objetivo es por cancion y no uno para todas). Nunca
  llega al 100%: siempre deja un resto sin corregir para no aplanar el caracter de cada disco.
- **Cancion rara**: si una se aleja muchisimo del resto de la biblioteca, casi seguro queria sonar
  asi (un disco muy oscuro, una grabacion en vivo). A esas se les toca menos, hasta la mitad.
- **Bajo profundo**: parte del 30% y lo ajusta el analisis. A una cancion con los picos recortados se
  le baja al 23% (mas grave solo haria trabajar al limitador y emborrona); a una copia pobre, con los
  agudos cortados, se le sube al 36% (esas suelen ir tambien flojas de abajo). Encima sigue actuando
  el ajuste por huella tonal de siempre, asi que el margen real va de ~12% a ~55% segun la cancion.

Se ve trabajando en la propia ventana: junto a cada casilla aparece el porcentaje que eligio el
programa y, en los graves, tambien lo que le toca a la cancion que esta sonando.

Quien prefiera mandar a mano solo tiene que quitar la casilla; la perilla vuelve a valer como siempre.
En el archivo de configuracion son `eq_automatico=` y `bajo_automatico=` (1 = automatico).

## Modelo del EQ AUTOMATICO (`rocola_eq_ia.bin`)

Es OTRA red, aparte de la del DSEE, y sirve para otra cosa.

- El EQ automatico corrige cada cancion hacia un objetivo. Ese objetivo era UNO para toda la
  biblioteca: el promedio. Con esta red el objetivo se calcula POR CANCION: mirando el cuerpo
  de su espectro (158 Hz - 5.6 kHz, lo que define de que musica se trata y lo que los codecs
  no destrozan) predice cuanto grave y cuanto aire tendria si estuviera bien grabada.
- Se entrena SOLO con las canciones sanas de tu biblioteca. Aprende de tus mejores grabaciones.
- Los datos ya existen: `rocola_eq.txt`, que la rocola escribe sola al pre-analizar. No hay que
  cosechar nada aparte.

Para entrenarlo:

    python3 entrenar_eq.py --datos rocola_eq.txt --instalado rocola_eq_ia.bin --destino eq_candidato_nuevo

y se obtiene un candidato aislado con informe, nunca se copia automaticamente al reproductor.
Sin modelo instalado la rocola usa el promedio (exactamente como antes).

**AutoEQ exige huellas limpias v2 y modelo instalado legible.** Solo exporta candidato si mejora
5% el RMSE frente al promedio de train y 2% frente al instalado, sin bandas/clases >10% peores,
paridad float32 <=1e-4 y guard del objetivo runtime ±6dB. La prueba no selecciona checkpoints.

Hacen falta al menos 200 canciones sanas analizadas para intentarlo.
