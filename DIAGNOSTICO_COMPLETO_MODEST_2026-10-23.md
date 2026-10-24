# Diagnostico completo de la rocola (Modest, 23-10-2026)

Peticion del dueno: «Mejora el benchmark para que sea mas extenso, que revise el rendimiento en todos los apartados, incluyendo el modo extendido, con los escenarios mas dificiles [...] Esto es una herramienta de diagnostico: que nos diga que mas debe optimizarse para el A4, y que capture uso del CPU, stutters, tirones.»

Es un SEGUNDO boton, junto a «Medir este equipo» (que queda igual): **Ctrl+F3 -> Imagen -> «Diagnostico completo»**. No es una preferencia: no lleva interruptor ni se guarda. No guarda ni aplica ningun ajuste.

## Como usarlo en el A4
1. Con la rocola en servicio tranquilo (o con una cancion de un cliente sonando: se usa esa, sin tocarla). Si no suena nada, el diagnostico pone una cancion MUDA de la biblioteca (misma via que el medidor rapido: sale callada desde dentro de la cadena, no toca ningun volumen).
2. Ctrl+F3 -> Imagen -> «Diagnostico completo». La ventana se cierra y la rocola recorre 16 escenas (unos 4 minutos; tope 7). Un aviso dice en que escena va. **No toques nada** (ni el raton): un boton, una moneda o el raton lo cancelan y lo dejan todo como estaba (los 2 primeros segundos no cuentan).
3. Al acabar, la ventana vuelve a Imagen con un resumen de dos renglones y el nombre del informe.
4. El informe es `rocola_diagnostico_AAAA-MM-DD_HHMM.txt` en la carpeta de datos (se conservan los 10 ultimos; nunca pisa uno anterior; UTF-8 con marca para el bloc de notas de XP). Se copia entero y se pasa tal cual.
5. Para separar el efecto del repaso/preanalisis, repetirlo con la biblioteca ya repasada: el informe dice, escena por escena, si el repaso o las miniaturas estaban activos.

## Que mide
Escenas (cada una con calentamiento y ventana de 8 a 21 s): rejilla quieta; hojeo rapido con portadas ya vistas; hojeo rapido por letras y paginas (portadas probablemente frias, sin borrar ninguna cache); cambio rapido de letra; buscador escribiendo; entrar y salir de discos; dentro de un disco; atraccion; **estado extendido con estrella 2D y 3D**; **entrar y salir del estado extendido**; cambio de cancion (color del genero y panel de estado); asistente y su guia; avisos y ficha; y dos **PEOR CASO** (navegando, y en reposo con estrella 3D) con todos los efectos en Alto, sin ahorros de Performance, musica con la cadena entera y trabajo de fondo real. Una escena que no se puede montar (sin canciones, sin discos, video, ayuda no disponible) se salta y el informe dice por que.

No se suelta el monitor: los cuadros se piden con el ritmo del bucle de verdad (`AgendaCuadro` + espera de mensajes + `RenderFrame`), asi que los intervalos son los que se ven, y el procesador es el de servicio (el medidor rapido, que si lo suelta, sigue contestando «cuantos cuadros caben»).

Por escena: cuadros presentados; intervalos p50/p95/p99/max entre presentaciones; cuantos pasan de 1,5 y 2 periodos del refresco; huecos de mas de 50 y 100 ms; los 12 peores huecos con su reparto por fase (las 11 fases del cuadro del `LibroDeCuadros`, que se enciende solo durante el diagnostico y se devuelve, mas «fuera» y «espera»: **13 ranuras**), banderas del cuadro, celdas, texturas subidas y recolecciones coincidentes; cuadros sin presentar y veces que se perdio el dispositivo; procesador del proceso, **por hilo** y del sistema (`GetSystemTimes`, `OpenThread`/`GetThreadTimes`, todo con try/catch); recolecciones por generacion; memoria privada y de trabajo; memoria de texturas (solo entre ventanas, nunca dentro); cortes de sonido y avisos tardios (la caja negra) durante la escena; y el trabajo de fondo activo (repaso, miniaturas, escaneo).

Hilos con nombre: el hilo de la pantalla, la musica, los lectores de caratulas, los carriles y la preparacion del repaso, las duraciones, los calentadores y el vigia se anotan al nacer (`HilosRocola.Nombra`, una llamada al arrancar el hilo). Lo demas cae en «resto del proceso».

## Como leer el informe
Diez secciones: 1 equipo; 2 ajustes vigentes; 3 condiciones (musica real o muda, trabajo de fondo al empezar y acabar); 4 tabla por escena con veredicto; 5 tirones con su fase; 6 procesador; 7 atribucion; 8 candidatos; 9 escenas saltadas; 10 limites.
- **Hueco** = tiempo entre dos cuadros presentados = «fuera ocupado» (entre el Present anterior y el inicio del cuadro: mensajes, audio, temporizadores, otros hilos, el sistema) + las fases del cuadro. **Espera ociosa** (el hilo dormido esperando el siguiente cuadro) se aparta aparte: no cuesta nada.
- Las **13 ranuras** son las 11 fases del cuadro (`pre`, `rejilla`, `portadas`, `cubos`, `estado`, `vivos`, `interfaz`, `adornos`, `adelanto`, `dibujo`, `presentar`) mas «fuera» (ocupado fuera del dibujo) y «espera» (hilo dormido a proposito). La **fase dominante** de un hueco es la mas larga de las doce primeras (la espera ociosa nunca domina un hueco). Es una atribucion de tiempo de reloj, **no una causa**.
- Las recolecciones de basura que coinciden con un hueco son una **coincidencia de contadores**: no dicen cuanto duro la recoleccion ni que fuera la causa.
- Los **candidatos** salen de reglas con numeros (ej. «el 62 % de los huecos de mas de 50 ms cayo en la fase "interfaz", sobre todo con la bandera "estado_subido"»; escena de cola mas larga; fases mas caras por cuadro; procesador casi saturado y hilo que mas usa; otros programas robando CPU; cortes de sonido; cuadros sin presentar; memoria alta). Si hay menos de 3 tirones no se atribuye nada.

## Coste del propio diagnostico
La toma de muestras (`DiagCaptura`) tiene todos sus arreglos creados una vez; `Cuadro` solo hace cuentas (la bateria la llama un millon de veces sin una sola recoleccion y comprueba en el IL que no hay `newobj`/`newarr`/`box`). Nada se escribe a disco durante las escenas: el informe va al final. Sin diagnostico, el bucle normal paga un `if` sobre una referencia nula en `RenderFrame` y en `NoPresentado`. Cambios en `D3D9Star.cs`: solo esos dos ganchos y un contador de dispositivo perdido; `LibroDeCuadros` gana el acceso a las fases del cuadro.

## Correcciones de la revision del 24-10-2026
Una revision independiente encontro estos defectos; cada uno se verifico y se corrigio con cambios minimos:
1. **La ventana de Ctrl+F3 se reabria siempre**, tambien tras cancelar porque un cliente toco la rocola o metio una moneda. Ahora `diagCancelado` (o `IsDisposed`/`Disposing`/`startupClosing`) rompe el bucle de `ShowVolumeManager`: la rocola queda en servicio.
2. **Cola parada y estado guardado con la muda.** Durante el diagnostico la muda es `nowPlaying`: una moneda y una peticion solo se encolaban. Al devolver el estado, si no suena nada y hay cola, arranca (`PlayNext`); si no, se rehace el estado guardado (`SaveState`). `SaveState` ignora la muda (`EsMudaDiag`), asi que nunca queda guardada como «sonando».
3. **Caminos de fallo sin «muda».** `AudioStartFailed`, `SalidaCaida` (y con ella el reintento), `Output_PlaybackStopped` y la caja negra (`ApuntaCortesDeSonido`) distinguen la muda: ni credito devuelto, ni cancion marcada danada, ni reintento audible, ni «[Cortes de sonido]» de una cancion que nadie pidio. Solo se para lo que sigue siendo SU muda. El perfil de latencia de la salida sigue viendo la muda (es la salida de verdad): un limite honesto.
4. **Restauracion exacta.** Se guardan y devuelven los valores crudos de `estrellasDens` y `auroraAmount` (`PonNivel` los cuantiza), se reabre el disco que estaba abierto y el asistente, y `LibroAntes` se captura al guardar el estado, dentro del `try/finally` junto con `diagEnMarcha`.
5. **Dispositivo perdido por episodios.** ``PerdidaNueva()` suma una vez por cada paso de «bien» a «perdido» (no por los reintentos de 250 ms) y se llama tambien desde Begin/End/Present; los Reset por cambio de tamano no cuentan.
6. `DiagCuadroPaceado` respeta `PuedeDibujar` (duerme en vez de girar al 100 %) y atrapa lo que reviente.
7. El veredicto usa el periodo real (`DiagPeriodoMs`: el del refresco, o el del tope de cuadros/video si es mas largo), no solo `1000/RefrescoHz`.
8. La atraccion no entra sola mientras corre el diagnostico (`diagEnMarcha` en `AnimTimer_Tick`). Comprobado: sin musica ni muda podia entrar tras 40 s.
9. Una cancelacion antes de medir nada no escribe informe (no gasta uno de los diez cupos).
10. Menores: los fps salen de los intervalos, las medias por cuadro usan `Cuadros-1` en todas partes, el texto de los hilos aclara que varios hilos con el mismo nombre se suman, y se quito una linea CRLF espuria de `Form1.Portadas.cs`.

## Cancelacion y estado
Se cancela con `lastInputTick` (botones y monedas) o `lastInput` (raton, pantalla); entonces la ventana de ajustes **no** vuelve. Devuelve, en un `finally`: niveles de efectos, ahorros de Performance, estrella 3D, estado extendido, buscador, atraccion, disco abierto, asistente, letra/pagina/seleccion, colores del genero, el libro de cuadros y la toma, y la cancion **solo si la puso el diagnostico**. Con la cancion de un cliente, el «cambio de cancion» solo mueve el color del genero (su cancion no se cambia).

## Pruebas
`pruebas/diagnostico.cs` (perfil con tarjeta): toma (un tiron inyectado de ~120 ms cuenta en >100 ms y cae en SU fase; uno fuera del dibujo cae en «fuera»; fallos; percentiles; cero reservas), motor con reloj virtual (todas las escenas, saltadas con motivo, presupuesto, cancelacion, Limpia siempre), procesador (hilo anotado que gira, proceso y sistema finitos y en rango), informe (secciones, archivo nuevo, poda a 10, resumen), panel de la tarjeta imitada (la toma recibe los cuadros de `RenderFrame`, el bucle paceado no gira sin freno), `CorreDiagnosticoGpu` entero con un Form1 sin encender, y lectura del IL (no guarda configuracion, no reinicia la tarjeta, no toca volumenes, no escribe a disco durante las escenas). `banco`, `huellacuadros`, `ajustesfluidez`, `ventanaf3`, `performance`, `tironescambio` y `fluidezcuadros` siguen en verde.

## Limites honestos
- Todo lo probado aqui es **sintetico** (tarjeta imitada, Linux/Mono): no mide la rocola real, ni Windows XP, ni el A4-6300. Las escenas de la rocola de verdad (hojeo, buscador, estado extendido, asistente...) solo se pueden ejercitar en la rocola; aqui se prueba el cableado, el motor y el informe.
- Es UNA corrida en UN rato. El procesador por hilo de XP avanza por tics de ~15 ms: sirve en ventanas de segundos, no para tirones sueltos.
- Si Windows le quita el procesador al hilo en medio de una fase, ese tiempo cuenta en la fase. No se promete ausencia de pausas del sistema operativo.
- Con musica muda del diagnostico trabaja toda la cadena de sonido pero no se oye; el cambio de cancion mueve la pantalla como un relevo, pero no reabre el MP3 en el hilo de la pantalla.
- El medidor rapido no mide el estado extendido si no suena una cancion de un cliente (`nowPlaying` nulo); el diagnostico completo si, presentando su cancion muda como «sonando» mientras dura.
- Los titulos largos del estado extendido no se inventan: el informe dice cuantos caracteres tiene el titulo medido.

## Informe de muestra (SINTETICO: tarjeta imitada de las pruebas en Linux/Mono, tres escenas; NO son cifras del A4)
```
======================================================================================================================
ROCOLA - DIAGNOSTICO COMPLETO
======================================================================================================================
Fecha: 2026-10-23 21:00:03   Duracion: 10 s   Programa: prueba
Este archivo se copia entero y se pasa tal cual. Los numeros llevan punto decimal; los tiempos estan en milisegundos salvo que se diga otra cosa.
OJO: ESTE INFORME ES DE UNA PRUEBA AUTOMATICA (tarjeta imitada, Linux/Mono): las cifras NO son de la rocola ni de Windows XP.

== 1. EQUIPO ==
Sistema operativo: Unix 6.18.44.153 (.NET 4.0.30319.42000)
Procesador: Intel(R) Xeon(R) Processor @ 2.10GHz, 4 nucleo(s)/hilo(s)
Tarjeta grafica: IMITADA de las pruebas (no pinta, no espera a ningun monitor); 1024x768

== 2. AJUSTES VIGENTES (los de la rocola al empezar) ==
(la pasada de prueba no usa los ajustes de ninguna rocola)

== 3. CONDICIONES DE LA MEDIDA ==
Musica: sin musica (prueba)
Trabajo de fondo: nada
Monitor: no hay (tarjeta imitada).

== 4. RESUMEN POR ESCENA ==
Intervalos = tiempo entre dos cuadros PRESENTADOS seguidos. Periodo del refresco: 16.7 ms (60 Hz). >1.5x y >2x = cuantos intervalos pasan de 1,5 y 2 periodos.
Escena                                    cuad  fps   p50   p95    p99    max >1.5x  >2x  >50 >100 fall  veredicto
Rejilla quieta (tarjeta imitada)           200   67  15.1  15.7   15.9   16.0     0    0    0    0    0  bien
Estado extendido, estrella 3D (tarjeta i   200   67  15.1  15.8   16.1   21.3     0    0    0    0    0  bien
Entrar y salir del estado extendido (tar   211   66  15.1  15.7   16.0   42.8     1    1    0    0    0  bien
Estado extendido sin cancion              (saltada: sin canciones en la biblioteca de prueba)

Procesador y fases por cuadro (medianas): prep+dibujo = trabajo del procesador al armar y mandar el cuadro; present = lo que espera Present.
Escena                                   prep+dib present  medio  cpu%  nucl sist% otros%   GC0/1/2 priv MB tex MB
Rejilla quieta (tarjeta imitada)              0.1     0.0   15.0   2.2  0.09   2.1    0.0     0/0/0      66    200
Estado extendido, estrella 3D (tarjeta i      0.1     0.0   15.0   2.1  0.08   5.4    3.3     1/0/0      69    200
Entrar y salir del estado extendido (tar      0.1     0.0   15.1   2.1  0.08   4.1    2.0     0/0/0      69    200

Detalle de cada escena:
 - Rejilla quieta (tarjeta imitada): Escena de prueba: Rejilla quieta (tarjeta imitada).
     3.0 s medidos; musica: sin musica (prueba de la tarjeta imitada)
     Nota de prueba de la escena.
 - Estado extendido, estrella 3D (tarjeta imitada): Escena de prueba: Estado extendido, estrella 3D (tarjeta imitada).
     3.0 s medidos; musica: sin musica (prueba de la tarjeta imitada)
     Nota de prueba de la escena.
 - Entrar y salir del estado extendido (tarjeta imitada): Escena de prueba: Entrar y salir del estado extendido (tarjeta imitada).
     3.2 s medidos; musica: sin musica (prueba de la tarjeta imitada); pasos de la escena: 4
     Nota de prueba de la escena.
 - Estado extendido sin cancion: Escena de prueba: Estado extendido sin cancion.
     SALTADA: sin canciones en la biblioteca de prueba

== 5. TIRONES (huecos de mas de 33 ms entre cuadros presentados), con la fase donde cayeron ==
Cada linea: hueco = fuera del dibujo (ocupado; entre el Present anterior y el inicio del cuadro) + las fases del cuadro. La fase dominante es la mas larga; no es una causa.
 Entrar y salir del estado extendido (tarjeta imitada)  (>50 ms: 0, >100 ms: 0)
   #1 a los 2.4 s: hueco 42.8 ms = fuera(ocupado) 28.5 espera(ociosa) 14.1 dibujo 0.1 | fase dominante: fuera | banderas: estado_extendido,transicion_del_estado,estado_subido | celdas 0, texturas subidas 0 | recolecciones en el hueco 0/0/0 (coincidencia, no causa)

== 6. PROCESADOR: DEL PROCESO, DEL SISTEMA Y POR HILO ==
Proceso = % de todos los nucleos (4). Hilos = % de UN nucleo (un hilo no pasa de 100). 'Otros' = lo que usa el resto del equipo, ajeno a la rocola.
 Rejilla quieta (tarjeta imitada)         proceso 2.2 % (0.09 nucleos); sistema 2.1 %; otros 0.0 %
      hilos: pantalla (interfaz) 8.7; (resto) 0.0
 Estado extendido, estrella 3D (tarjeta i proceso 2.1 % (0.08 nucleos); sistema 5.4 %; otros 3.3 %
      hilos: pantalla (interfaz) 8.7; (resto) 0.0
 Entrar y salir del estado extendido (tar proceso 2.1 % (0.08 nucleos); sistema 4.1 %; otros 2.0 %
      hilos: pantalla (interfaz) 8.4; (resto) 0.0
 Suma de todas las escenas, por hilo (media sobre el tiempo medido y la escena donde mas):
   pantalla (interfaz)                         8.6 % de un nucleo de media; maximo 8.7 % en "Estado extendido, estrella 3D (tarjeta imitada)"
   (resto del proceso)                         0.0 % de un nucleo de media; maximo 0.0 % en "Rejilla quieta (tarjeta imitada)"

== 7. ATRIBUCION: QUE CUESTA MAS ==
Tiempo por cuadro y por fase (ms de reloj del hilo de la pantalla, media de la ventana). 'fuera' = ocupado fuera del dibujo; 'espera' = hilo dormido esperando el siguiente cuadro (no cuesta).
Escena                                      pre rejill portad  cubos estado  vivos interf  adorn adelan dibujo presen  fuera espera
Rejilla quieta (tarjeta imitada)           0.00   0.00   0.00   0.00   0.00   0.00   0.00   0.01   0.00   0.09   0.00   0.81  14.08
Estado extendido, estrella 3D (tarjeta i   0.00   0.00   0.00   0.00   0.00   0.00   0.00   0.01   0.00   0.13   0.00   0.86  13.99
Entrar y salir del estado extendido (tar   0.00   0.00   0.00   0.00   0.00   0.00   0.00   0.01   0.00   0.10   0.00   0.95  14.06
TODAS (% del tiempo entre cuadros)          0.0    0.0    0.0    0.0    0.0    0.0    0.0    0.1    0.0    0.7    0.0    5.8   93.4

Huecos de mas de 50 ms por fase (total 0, de mas de 100 ms: 0):
   ninguno.

== 8. CANDIDATOS A OPTIMIZAR PARA EL A4 (justificados con los numeros de arriba) ==
 1. Ningun hueco de mas de 50 ms en ninguna escena medida (3 escenas, 611 cuadros presentados). Es esta maquina y este rato; no es una promesa para la rocola real ni para siempre.
 2. CPU: el pico fue "Rejilla quieta (tarjeta imitada)" con 0.09 nucleos de 4 (2 % del procesador). El hilo que mas: "pantalla (interfaz)" con 9 % de un nucleo.

== 9. ESCENAS SALTADAS ==
 - Estado extendido sin cancion: sin canciones en la biblioteca de prueba

== 10. LIMITES ==
 - Es UNA corrida en UN rato: la rocola real cambia con la biblioteca, el repaso, el disco y lo que hagan los clientes. Repetirla antes de decidir.
 - El tiempo de cada fase es de reloj de pared del hilo de la pantalla: si Windows le quita el procesador en medio de una fase, ese tiempo cuenta en ella. No se puede separar.
 - 'Fuera del dibujo' mezcla mensajes, audio que se rellena desde el hilo de la pantalla, temporizadores, recolecciones y otros hilos; el reparto por hilo ayuda pero no lo resuelve.
 - El procesador por hilo sale de GetThreadTimes: en Windows XP avanza por tics de ~15 ms; sobre ventanas de varios segundos es fiable, no para tirones sueltos.
 - El numero de recolecciones de basura en un hueco es una coincidencia de contadores, no su duracion ni su causa.
 - Con musica muda del diagnostico la cadena de sonido entera trabaja, pero no se oye; la salida real puede pedir la musica de otra manera. No se promete ausencia de pausas del sistema operativo.
 - Las escenas de cambio de cancion mueven la pantalla como un relevo de verdad, pero con musica de un cliente no se cambia su cancion: solo se mide la transicion visual del color del genero.
```
