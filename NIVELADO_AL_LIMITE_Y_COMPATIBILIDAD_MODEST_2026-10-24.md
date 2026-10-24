# Nivelado al límite, medidas de versiones viejas, adiós al modo noche y volumen de Windows — 24 de octubre de 2026

Rama Modest (`claude/modest-carson-fa2v2n`), .NET 3.5 / Windows XP. Cuatro peticiones del dueño del 24-10-2026:

1. Varias canciones sonaban bajísimo después de los arreglos, como si el nivelador no funcionara: investigar la causa y hacer que
   las medidas de versiones anteriores sigan valiendo mientras se analizan para el estándar nuevo.
2. Quitar el modo que bajaba el volumen a partir de las 11 de la noche (el que hace el corte de la venta lo baja él).
3. Subir el volumen de Windows al máximo cada vez que arranca una sesión nueva del programa (se le olvida subirlo como estaba).
4. Cambiar el nivelado: que cada canción se suba hasta el límite de lo que aguanta sin escucharse rasposa ni perder calidad.

Todo lo de aquí está comprobado con pruebas automáticas y señales sintéticas en Mono/Linux. **No se ha oído en el billar ni se ha
probado en Windows XP**: el volumen de Windows, en concreto, no se puede probar en Linux (no hay mezclador).

---

## 1. Por qué varias canciones sonaban bajísimo

**La causa.** Desde el 29-09-2026 cada canción medida lleva un registro de identidad en `rocola_fuentes.txt`: su tamaño, sus
fechas de creación y de escritura y una firma de sus medidas. Al sonar una canción, `CompruebaFuenteMedida` (FuentesMedidas.cs)
miraba ese registro y, ante **cualquier** duda, borraba TODAS las medidas de la canción (volumen, duración, EQ y la medida entera
con sus picos) y guardaba las tablas así. Las dudas eran:

- no tener registro: canciones medidas por una versión anterior al 29-09, que nunca se volvieron a medir;
- fechas distintas: la música copiada a otro disco cambia la fecha de creación, y una etiqueta retocada cambia la de escritura;
- una firma que no cuadra.

Sin medidas, la canción suena a su volumen de origen, sin nivelar. Las grabaciones bajitas pierden de 10 a 19 dB.

**Medido con un viaje real entre versiones.** Se compiló cada versión, se midieron con ella cinco canciones de prueba, y la cadena
de sonido de otra versión (`ArmaCadena`, como al sonar) leyó esas tablas:

| Medidas hechas por… | Tocadas por la del 23-10 | Tocadas por la de hoy |
|---|---|---|
| la versión del 26-09 (sin `rocola_fuentes.txt`) | **5 de 5 perdidas**; la «muy bajita» pasa de ×8,8 (+18,9 dB) a ×1 | 0 de 5 perdidas |
| la del 13-10 | 0 de 5 | 0 de 5 |
| la del 22-10 (con registro de identidad) | 0 de 5 | 0 de 5 |
| la de hoy antes del nivelado nuevo (algoritmo 4) | 0 de 5 | 0 de 5 |

Con la del 23-10, las tablas quedaban vacías en el disco y solo el `.bak` conservaba la versión anterior. La firma de las medidas no
cambió el 23-10: con registros del 22-10 no se pierde nada. Así que el problema es de las canciones sin registro o con otras fechas.
Desde aquí no se puede saber cuál de los tres casos pasó en el billar. Los tres se arreglan con lo mismo.

También pudo sumar el modo noche: de 11 de la noche a 7 de la mañana bajaba 4 dB. Ya no existe (punto 2).

**El arreglo** (commit `279be7c`, FuentesMedidas.cs). Solo se olvida lo que es **otro archivo de verdad**: otro tamaño en bytes, u
otra duración (eso ya lo miraba `OlvidaSiEsOtroArchivo`). En los demás casos la medida se **adopta**:

- se apunta con el sello de hoy y sigue nivelando;
- la canción queda «por verificar», y la tanda larga la vuelve a medir antes que el resto.

Una canción que se reemplazó por otra versión del mismo tamaño y la misma duración conserva la medida vieja hasta que el repaso la
vuelve a medir. Ese repaso ya lo pide.

**Lo que ya se perdió.** Las medidas que la versión del 23-10 borró en la rocola no vuelven. Esas canciones se miden otra vez:

- la primera vez que suenan enteras (y desde la siguiente ya suenan niveladas);
- en la tanda larga del repaso, en segundo plano.

---

## 2. El modo noche, retirado

El modo noche bajaba el volumen de 11 de la noche a 7 de la mañana, en una rampa de 20 s, y devolvía graves y agudos según la norma
ISO 226. Se quitó por completo (commit `3804fc1`):

- `ModoNoche.cs` y el bloque «MODO NOCHE» de la cadena;
- la etapa que compensaba graves y agudos, en la cadena, en la siguiente preparada y en el ajuste en vivo;
- la bajada de los videos;
- el interruptor de Ctrl+F3.

Las claves `noche=`, `noche_desde=`, `noche_hasta=`, `noche_db=` y `noche_compensa=` de una configuración vieja se leen sin error,
se ignoran y ya no se escriben. Ningún sitio de la cadena ni de los videos mira ya la hora.

De día la cadena sale **idéntica bit a bit** a la de antes (`compara-cadena`: 16 de 16 combinaciones). La batería `noche` vigila que
no vuelva a aparecer.

---

## 3. El volumen de Windows al máximo al arrancar

Nuevo `VolumenWindows.cs` (commit `6764dfd`). Cada vez que arranca la rocola (al mostrarse la ventana, después de leer la
configuración) pone el volumen **general** de Windows al máximo y le quita el silencio:

- **Windows XP:** el control maestro del mezclador (la línea de los altavoces y su «Silencio») en cada tarjeta de sonido. Si la
  línea «Onda» está en silencio, se le quita; su nivel no se toca.
- **Vista y posteriores:** el volumen del aparato de salida predeterminado y el de la sesión de la propia rocola en el mezclador por
  aplicación.

Lo que no hace:

- Una vez por arranque, en un hilo de fondo y con todo atrapado: si una API falta o la tarjeta no tiene ese control, no pasa nada y
  se apunta en la bitácora.
- Mientras la rocola está abierta no lo vuelve a tocar. Si alguien lo baja a mano, se queda así hasta el siguiente arranque.
- Nunca toca el volumen de la salida WaveOut de la rocola.

Es una excepción expresa a la regla de no tocar el volumen de Windows. Lo pidió el dueño y no lleva interruptor.

La batería `volumenwindows` lo comprueba leyendo el programa compilado. **Sin probar en un XP ni en un Vista de verdad.**

---

## 4. El nivelado nuevo: cada canción hasta su límite

### La regla

Antes, el nivelado llevaba cada canción hacia un **volumen objetivo** (el 26 %, −11,7 dB por canal):

- subía las bajitas, con topes por sus picos (P99 + 3 dB y golpe más fuerte + 6 dB sobre el techo del limitador);
- bajaba hasta 4 dB las muy fuertes.

Ahora, con una medida del **algoritmo 5**, no hay objetivo. Cada canción sube lo más que aguanta sin que el limitador del final
(pico real, techo −1,1 dBTP, 3 ms de anticipación) la raspe. No se baja si ya va fuerte, solo lo justo si llega por encima de su
límite. El límite sale de sus picos por bloque de 100 ms, comparados con el techo del limitador:

| Pico de la canción | Puede pasar del techo | Qué protege |
|---|---|---|
| **P90** (el que solo supera el 10 % de los bloques: el grueso de la canción) | **1 dB** (nuevo) | que una canción ya aplastada no quede limitada todo el tiempo |
| **P99** (sus golpes) | 3 dB (el tope de antes) | los golpes |
| **el golpe más fuerte** de toda la canción | 6 dB (el tope de antes) | un golpe suelto |

La «Fuerza del nivelado» dice qué parte de ese camino recorre: 100 % hasta el límite, 50 % la mitad en dB, 0 % nada. Sigue el techo
de seguridad de +24 dB para una canción casi muda.

- Código: `GananciaAlLimite` y `LimiteSinRaspar` (Sonoridad.cs), usadas por `NormGainFor`, el nivel por disco (NivelDisco.cs) y la
  holgura de «Subir TODO» (SubirTodo.cs).
- El P90 sale del mismo histograma de picos que el P99 (`PicosBloques.PercentilDb`), y el P99 no cambia.

### Cómo se eligieron los márgenes

Se pasaron ocho canciones sintéticas de 60 s por el limitador de la rocola de verdad (`pruebas/allimite.cs`). Criterio: que el
limitador quite **más de 1 dB como mucho el 10 % del tiempo** y **más de 3 dB como mucho el 1 %**.

| Canción de prueba | Sube | Peor reducción | > 1 dB | > 3 dB |
|---|---|---|---|---|
| golpes parejos (4 por segundo) | +1,6 dB | 2,4 dB | 7,3 % | 0 % |
| golpes desiguales (2/s) | +2,9 | 3,7 | 6,5 % | 0,4 % |
| golpes sueltos (1/s) | +3,6 | 3,8 | 6,7 % | 0,4 % |
| balada sin golpes | +1,7 | 1,8 | 4,6 % | 0 % |
| balada con un golpe suelto | +5,4 | 8,9 (un instante) | 0,4 % | 0,2 % |
| moderna aplastada | +0,3 | 1,2 | 2,9 % | 0 % |
| grabación vieja bajita (pico −9 dBFS) | +11,8 | 4,1 | 8,4 % | 0,4 % |
| comprimida grabada baja (pico −7 dBFS) | +7,2 | 1,4 | 3,7 % | 0 % |

Las alternativas probadas:

- Con el P90 a **0,5 dB**, la de golpes parejos subía solo +1,2.
- Con el P90 a **1,5 dB**, la de golpes parejos pasaba al 16 % del tiempo por encima de 1 dB y la comprimida al 44 %.
- **Sin el P90** (solo los topes de picos y sin objetivo), las dos aplastadas quedaban limitadas más de 1 dB más del 90 % del
  tiempo. La batería lo vigila.
- Con el golpe más fuerte a **4 dB** en vez de 6, la balada con un golpe suelto subía 2 dB menos que con la regla de antes.

### Lo que cambia frente a la regla de antes

Sonoridad a la salida de la cadena entera, de fábrica, en LKFS (con la medida nueva comparada con la misma medida leída como de antes):

| Canción de prueba | Al límite | Regla de antes | Sin nivelar |
|---|---|---|---|
| golpes parejos 4/s | −13,7 | −13,0 | −15,1 |
| golpes desiguales 2/s | −12,5 | −12,5 | −15,2 |
| golpes sueltos 1/s | −14,6 | −14,6 | −18,0 |
| balada sin golpes | −8,5 | −9,5 | −9,8 |
| balada con un golpe suelto | −11,0 | −11,0 | −16,3 |
| moderna aplastada | −8,3 | −8,5 | −8,3 |
| vieja bajita | −10,5 | −10,4 | −22,0 |
| comprimida grabada baja | −8,4 | −9,6 | −12,5 |
| pop moderno aplastado (otra serie) | −6,7 | −7,3 | −7,1 |

Lo que se lee en la tabla, con sinceridad:

- **Las canciones con golpes ya estaban en su límite.** Con los picos medidos, la regla de antes las subía hasta donde dejaban sus
  picos, y ahora igual.
- **Suben más las que el objetivo frenaba:** las sostenidas (una balada sin golpes, +1 dB) y las comprimidas grabadas bajas (+1,2 dB).
- **Las muy fuertes ya no se bajan** (el pop aplastado, +0,6 dB).
- **El precio:** una canción con golpes en más del 10 % de los bloques (batería muy seguida) suena 0,7 dB menos que con la regla de
  antes. La de antes la dejaba con el limitador trabajando el 27 % del tiempo, y ahora el 7 %.
- **El volumen ya no es parejo, a propósito.** Una canción con golpes muy por encima del resto sigue sonando más suave que una
  aplastada, porque subirla más pediría aplastar sus golpes. Para eso está la «Fuerza del nivelado»: por debajo del 100 % todas se
  quedan más lejos del límite.

Son señales sintéticas: dicen que la cuenta hace lo que promete, no cómo suena la música del bar. Lo que manda es lo que se oiga ahí.

### Compatibilidad: varias versiones de la medida a la vez

`rocola_sonoridad.txt` sigue con la cabecera «ROCOLA-SONORIDAD v1». El registro del algoritmo 5 lleva un campo más, el P90, detrás
del LRA.

- **Lo medido antes sigue nivelando.**
  - Algoritmos 2 a 4: con la regla de antes y sus topes de picos.
  - Algoritmo 1 y la estimación de `rocola_volumen.txt`: con la regla de antes sin picos.
  - Nada se queda sin nivelar por ser de antes.
- **La tanda larga vuelve a medir toda la biblioteca para el algoritmo 5, con el modo automático del local encendido o apagado.**
  Antes solo lo hacía con ese modo encendido (la regla del 23-10 para el LRA). Va en segundo plano con el presupuesto de siempre para
  dos núcleos, detrás de las estimaciones y con las canciones «por verificar» delante. En el A4 y con una biblioteca grande, son
  horas (no se ha medido allí). Mientras, cada canción usa su medida de antes. Al pasar al algoritmo 5 su ganancia cambia una vez,
  nunca a media canción.
- **Una versión anterior de la rocola** guarda los registros del algoritmo 5 como ajenos, intactos, y los devuelve al guardar.
  Probado con los ejecutables del 26-09 (algoritmo 1), del 13-10 (algoritmo 3) y de hoy antes de este cambio (algoritmo 4).
- **El disco en racha** (dos o más pistas seguidas del mismo disco):
  - con todas sus pistas del algoritmo 5, sube hasta el límite de su pista más exigente, y las bajitas siguen por debajo, como en el
    disco;
  - si falta alguna medida nueva, usa la regla de antes para todo el disco.
- **Con el nivelado apagado** la cadena sale idéntica bit a bit a la de antes del cambio (`compara-cadena` con el nivelado apagado,
  16 de 16). Con el nivelado encendido cambia solo la ganancia.

### En Ctrl+F3 (Sonido)

- La tarjeta se llama «VOLUMEN AL LIMITE».
- El interruptor se llama «Subir cada cancion hasta su limite» (`normalizar=` de siempre).
- «Volumen objetivo» queda para las canciones que el repaso aún no volvió a medir.
- «Fuerza del nivelado» dice cuánto de su margen usa cada canción.
- «Subir TODO»: las canciones ya medidas con la fuerza al 100 % no tienen holgura. Cuando casi toda la biblioteca esté medida así, la
  tarjeta dirá que la música ya va al límite y no subirá nada. Para más volumen, el amplificador.

Esto sustituye el descarte del 28-09 («subir cada canción solo lo que aguantan sus picos rompe el volumen parejo»): ahora lo decidió
el dueño.

---

## 5. Pruebas

- **Nueva batería `allimite`** (31 comprobaciones):
  - el P90 y su formato (ida y vuelta, lo roto se salta, lo de un algoritmo posterior se guarda intacto);
  - la regla (márgenes, fuerza, techo de +24 dB, nunca NaN, lo que llega por encima de su límite baja lo justo);
  - las ocho canciones con el limitador de verdad, y sin el P90 para comparar;
  - cada medida con su regla;
  - de punta a punta, una tabla de septiembre sin registro de identidad que suena por la cadena real, conserva su medida, nivela y
    queda por verificar;
  - la tanda larga con el modo del local encendido y apagado;
  - el disco.
- **Ajustadas:**
  - `lra`: el algoritmo 5, y una versión anterior cualquiera lee el archivo nuevo sin perder nada;
  - `silencios`: el algoritmo 5;
  - `ajustes`, `vistaf3`, `banco` y `modosauto`: los textos nuevos;
  - `fuentes`: las medidas viejas se adoptan y otro tamaño se olvida;
  - `noche`, `videoaudio`, `compara-cadena` y otras: el modo noche retirado;
  - `volumenwindows`: nueva.
- Suite completa en verde salvo las tres de entorno conocidas (`sala`, `discoacotado`, `caratulasadaptativas`). Ver el README.
