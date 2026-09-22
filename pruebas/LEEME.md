# Las pruebas de la rocola

Cada archivo `.cs` de esta carpeta es una **batería**: un programa suelto que carga el
ejecutable de la rocola y le mira las tripas por dentro. No son pruebas de las que solo
comprueban que una función devuelve 2+2=4; miden lo que cuesta algo **de verdad**, cuentan
los píxeles de lo que se pinta, o leen el código ya compilado para comprobar que una función
sigue enchufada donde debe.

Cada una escribe líneas `OK` y `FALLO` y devuelve cuántos fallos hubo.

## Cómo se corren

En Windows: `pruebas/validar-capas.ps1 -OutputDirectory <carpeta-temporal>`
compila GPU/CPU y ejecuta 27 baterías, incluida `espacioprecarga`
(siguiente página/letra, prioridades, memoria adaptativa, estrella y ocultación).
No reemplaza ejecutables instalados. La prueba HAL usa Direct3D real cuando está
disponible; si falta, lo indica y no demuestra el comportamiento de la tarjeta.

```sh
sh pruebas/todo.sh              # la suite entera (compila la rocola sola y arranca)
sh pruebas/todo.sh disco ssd    # solo esas dos
sh pruebas/todo.sh --lista      # cuáles hay
```

No hay que preparar nada. El lanzador detecta si la rama es la de Windows XP (.NET 3.5) o la
de tarjeta gráfica (.NET 4.8), compila los dos perfiles —con y sin GPU— y corre todo. Hace
falta `mcs` (Mono), `xvfb-run` y `python3`.

## Qué vigila cada una

**Sonido**

| batería | qué comprueba |
|---|---|
| `dsee` | el reconstructor de agudos: que no se cuele un canal en el otro al meter los dos en una FFT, que apagado sea bit a bit idéntico, que una canción completa salga intacta, y que el realce no empuje dos veces lo mismo |
| `iaparidad` | que el entrenador mide lo mismo que la rocola: las 28 entradas y los 12 objetivos de `MakePair` contra `ia/paridad_extractor.txt`, todos los cortes a seis frecuencias de muestreo contrastados con `ia/contrato_dsee.py` (lanza Python; sin Python lo dice y no lo da por comprobado), las bandas que rellena `RegenNeural`, y que la imitación en float32 de la red que usa el entrenador da las mismas salidas que `RedChica.Predict`, bit a bit |
| `iaorigen` | que la rocola apunta de qué canción viene cada ejemplo de la IA (`rocola_ia_datos.idx`) y que el lector del entrenador nunca lo atribuye mal: apagones a media escritura, bytes cambiados, datos recortados o borrados, índice ajeno y cuatro hilos a la vez (lanza Python) |
| `respaldo` | *(ramas con DSEE ULTRA)* que con ULTRA encendido el DSEE normal SI atienda a las canciones que Apollo aún no ha hecho: que salga su letrero plateado, que el realce no empuje dos veces, y que encender ULTRA no te borre para siempre el interruptor del DSEE |
| `atoron` | lo que la pantalla se queda parada cada vez que hay que dar de comer al sonido |
| `nivel`, `bajo`, `bajoauto` | nivelado de volumen y bajo psicoacústico |
| `limite` | que el limitador BAJE la ganancia en vez de recortar: distorsión de un tono fuerte y aplanado de la punta en un golpe seco |
| `eqauto`, `eqia`, `calidad` | el EQ automático y su objetivo por canción |
| `duracion`, `titulo`, `etiqueta`, `genero` | lo que se lee de cada archivo |
| `colores` | que ningún género musical se quede sin color: los que trae un ID3 y los que se leen del nombre de la carpeta, en español y con acentos; que el color sea el mismo se pregunte por donde se pregunte; y que no haya dos que no se puedan distinguir |
| `mudos` | *(sólo en la rama de tarjeta gráfica)* la limpieza de videos que no suenan. Lo que vigila no es que borre, es que NO BORRE DE MÁS: uno sin medir no puede aparecer nunca, y si Windows no puede mandarlos a la papelera la biblioteca se queda exactamente como estaba |
| `videos` | que los videos entren en el repaso que los mide, y que suenen todos parejo: el reproductor de Windows sólo sabe BAJAR, así que se comprueba con qué volumen quedaría cada uno y cuánta diferencia queda entre el que más suena y el que menos |

**Pantalla**

| batería | qué comprueba |
|---|---|
| `letrero` | que ningún texto salga cortado ni se calle teniendo sitio |
| `hojea`, `pagina` | pasar de página: que lo horneado no se quede viejo y que ninguna página cueste el triple |
| `busca`, `buscaigual`, `busmov`, `parcial` | el buscador: que rinda y que se vea igual que antes |
| `mover` | que moverse entre las **teclas** del buscador NO rehaga ni vuelva a subir la pantalla entera: la tecla elegida va como pieza suelta con su letra dentro, sin mover el lienzo (eso la dejaba vacía en Windows), y no existe cuando el teclado no se ve. Y lo que SÍ tiene que seguir rehaciendo la pantalla: pasar a los resultados y moverse por ellos. Mantener pulsada una flecha tiene tope |
| `bolsa` | la primera letra tecleada, que es la única cara: que guardarla dé **exactamente** la misma lista y en el mismo orden, que se vacíe al recargar la música, y cuánto ahorra de verdad |
| `abc`, `neon`, `espacio`, `barrido2` | la barra de letras y los efectos |
| `vacias` | que las letras sin discos detrás no se dibujen, y que las que quedan salgan más grandes |
| `franja` | la franja de abajo: que al sonar una canción se vea de qué género es, y que los créditos y la cola se lean de lejos |
| `estrellas` | que el fondo tenga las cuatro siluetas de estrella, que el color fuerte se lo queden las grandes y que dibujarlo no cueste más |
| `sala` | dentro de un disco: las marcas de "sonando" y "en cola" |
| `fondo`, `anim`, `presencia`, `barrido`, `tope`, `calma` | la aurora y el fondo reactivo (sólo con tarjeta gráfica) |

**Recursos**

| batería | qué comprueba |
|---|---|
| `sobras` | que un cuadro en reposo no reserve ni un byte de memoria |
| `gasto` | que la caché de carátulas no crezca, que lo expulsado se libere de verdad, y lo que ocupa una biblioteca de 50.000 canciones |
| `historial` | cuántas páginas de la pared de discos se guardan, que el cajón respete su tope, y que volver a una guardada sea mucho más barato |
| `carpetas` | que se le pueda decir "la música está en ESTE disco": que el disco entero se pueda elegir, que mande la carpeta de fuera, y que el barrido no se quede dando vueltas en los atajos ni entre en las carpetas del sistema |
| `miniaturas` | que las miniaturas de las portadas se guarden en el disco y no en la carpeta temporal (que en Puppy está en la memoria y se borra al apagar), y que el barrido no toque las recientes |
| `pared` | que el cajón de portadas se ajuste a la máquina, y que con el grande se pueda hojear la pared entera y volver **sin tocar el disco ni una vez** |
| `usando` | la rocola **con música sonando y alguien pulsando flechas**, que es el momento que aprieta: que mover la selección repinte sólo las dos casillas que cambian y no la pared entera, que salga **idéntico** píxel a píxel, y que un cuadro navegando no deje sobras para el recolector |
| `resol` | que cambiar la resolución con la rocola abierta no la deje lenta |
| `primera` | que las animaciones vayan fluidas **desde la primera vez**: mide cada pantalla —el primer cuadro contra uno normal—, comprueba que la rocola precalienta sola en segundo plano las que al encender no se ven, que no calienta lo que se midió y no gana nada, y que precalentar no cambia ni un píxel |
| `disco`, `ssd` | disco duro contra SSD |
| `cosecha` | que el entrenamiento pasivo siga juntando material |
| `proyecto` | que el archivo con el que compila Visual Studio (`Rocola.csproj`) liste **exactamente** los mismos archivos y bibliotecas que compila la tanda, y que ninguno se quede fuera sin que nadie diga por qué |

**Herramientas de medida** (no son pruebas de pasa/falla; se corren a mano)

| archivo | para qué |
|---|---|
| `dseeperf.cs` | dónde se va el tiempo dentro del reconstructor de agudos, por partes |
| `modelo.cs` | lo que costaría un modelo de IA más grande, tamaño por tamaño |
| `memoria.cs` | dónde se va la memoria con una biblioteca grande |
| `cuadro.cs` | de qué se compone un cuadro de pantalla cuando no ha cambiado nada (el cielo incluido, que es la pieza más cara) |
| `vercielo.cs` | saca una imagen del cielo estrellado para poder mirarlo |
| `pantalla.cs` | dibuja la pantalla entera (1024x768) en un PNG, con los controles de verdad, para mirarla |
| `aspecto.cs` | si sacar el recuadro del resultado elegido de la imagen de pantalla completa cambiaría el aspecto: dibuja el buscador de las dos formas y compara píxel a píxel (9 casos, dos resoluciones: salen **0 píxeles** de diferencia) |
| `cachemem.cs` | lo que cuesta rehacer una carátula que se salió de la caché, y lo que costaría una caché mayor |
| `capacidad.py` | si un modelo de IA más grande predice mejor (entrena varios tamaños sobre los mismos datos) |
| `genmodelos.py` | genera modelos de varios tamaños para medirlos |

Los `diag*.cs` y `comp*.cs` son diagnósticos de un rato concreto: se dejan porque explican
cómo se cazó un fallo, pero no forman parte de la suite.

## Baterías nativas de capas y color (Windows)

Ejecutar `powershell -File pruebas/validar-capas.ps1` desde esta rama. Puede pasarse
`-OutputDirectory` con una carpeta temporal propia. No ejecuta la biblioteca real
ni sustituye archivos de los releases; necesita Direct3D 9 para las comprobaciones HAL.

- `capascolor`: 18 composiciones idénticas píxel a píxel; firmas de selección/datos,
  conservación del hojeo y coste de preparación; propiedad de texturas, reutilización
  durante la mezcla, cancelación al toque/cambio de contenido y limpieza al apagar.
- `cristalcola`: 1024 combinaciones de configuración y mandos sin solapamientos; 56 paneles entre 480 y 3440 px,
  miniaturas visibles y celdas sin cruces; transparencia y acceso a Fondo.
- `cubofila`, `respuesta`, `efectoslayout`: regresiones del cubo/tiempo/progreso,
  caché de portadas y mandos de Imagen.
- `reposounido`: 297 rejillas compactas/simétricas, 126 uniones entre la malla del
  horizonte y el borde de la barra; atenuación/restauración a 24–240 Hz y prueba
  HAL del alfa, posición y reutilización de texturas. Detecta el defecto de
  tarjetas gigantes al ejecutarse contra la versión anterior.

- `lunadetalle`: superficie determinista de 256 px, borde alfa, luz lateral,
  16 combinaciones de opciones guardadas/aplicadas a GPU, geometria y brillo del
  realce, cache entre cuadros/resoluciones, regreso al diseno anterior y liberacion
  HAL. Genera una vista de la textura (no una captura de la aplicacion con musica).

Estas baterías usan el runtime de cada rama. No equivalen a una prueba con música,
una medición de FPS en el equipo de destino ni una certificación de Windows XP.

## Añadir una batería nueva

1. Escribe `mibateria.cs` con un `Main` que reciba la ruta del ejecutable como primer argumento.
2. Que imprima `  OK  ` / `  FALLO  ` por cada cosa que compruebe y devuelva el número de fallos.
3. Añade su nombre a la lista `CPU=` (o `GPU=`) de `todo.sh`.

**La regla que se sigue aquí:** antes de arreglar algo, la batería tiene que **fallar** con el
código de antes. Una prueba que pasa en las dos versiones no vigila nada.
