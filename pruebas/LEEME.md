# Las pruebas de la rocola

Cada archivo `.cs` de esta carpeta es una **batería**: un programa suelto que carga el
ejecutable de la rocola y le mira las tripas por dentro. No son pruebas de las que solo
comprueban que una función devuelve 2+2=4; miden lo que cuesta algo **de verdad**, cuentan
los píxeles de lo que se pinta, o leen el código ya compilado para comprobar que una función
sigue enchufada donde debe.

Cada una escribe líneas `OK` y `FALLO` y devuelve cuántos fallos hubo.

## Cómo se corren

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
| `mover` | que moverse entre las casillas del buscador NO rehaga ni vuelva a subir la pantalla entera a la tarjeta gráfica: el recuadro elegido va como pieza suelta, sale **0 píxeles** distinto, y mantener pulsada una flecha tiene tope |
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
| `miniaturas` | que las miniaturas de las portadas se guarden en el disco y no en la carpeta temporal (que el sistema limpia cada tanto), y que el barrido no toque las recientes |
| `pared` | que el cajón de portadas se ajuste a la máquina, y que con el grande se pueda hojear la pared entera y volver **sin tocar el disco ni una vez** |
| `resol` | que cambiar la resolución con la rocola abierta no la deje lenta |
| `disco`, `ssd` | disco duro contra SSD |
| `cosecha` | que el entrenamiento pasivo siga juntando material |

**Herramientas de medida** (no son pruebas de pasa/falla; se corren a mano)

| archivo | para qué |
|---|---|
| `dseeperf.cs` | dónde se va el tiempo dentro del reconstructor de agudos, por partes |
| `modelo.cs` | lo que costaría un modelo de IA más grande, tamaño por tamaño |
| `memoria.cs` | dónde se va la memoria con una biblioteca grande |
| `cuadro.cs` | de qué se compone un cuadro de pantalla cuando no ha cambiado nada |
| `vercielo.cs` | saca una imagen del cielo estrellado para poder mirarlo |
| `pantalla.cs` | dibuja la pantalla entera (1024x768) en un PNG, con los controles de verdad, para mirarla |
| `cielos.cs` | cuaderno de bocetos del fondo: dibuja nueve cielos -el de hoy y cada idea por separado- para poder elegir mirándolos, en `pruebas/cielos/` |
| `aspecto.cs` | si sacar el recuadro del resultado elegido de la imagen de pantalla completa cambiaría el aspecto: dibuja el buscador de las dos formas y compara píxel a píxel (9 casos, dos resoluciones: salen **0 píxeles** de diferencia) |
| `cachemem.cs` | lo que cuesta rehacer una carátula que se salió de la caché, y lo que costaría una caché mayor |
| `capacidad.py` | si un modelo de IA más grande predice mejor (entrena varios tamaños sobre los mismos datos) |
| `genmodelos.py` | genera modelos de varios tamaños para medirlos |

Los `diag*.cs` y `comp*.cs` son diagnósticos de un rato concreto: se dejan porque explican
cómo se cazó un fallo, pero no forman parte de la suite.

## Añadir una batería nueva

1. Escribe `mibateria.cs` con un `Main` que reciba la ruta del ejecutable como primer argumento.
2. Que imprima `  OK  ` / `  FALLO  ` por cada cosa que compruebe y devuelva el número de fallos.
3. Añade su nombre a la lista `CPU=` (o `GPU=`) de `todo.sh`.

**La regla que se sigue aquí:** antes de arreglar algo, la batería tiene que **fallar** con el
código de antes. Una prueba que pasa en las dos versiones no vigila nada.
