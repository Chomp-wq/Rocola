# Port nativo: arreglo tras N7, la escena `f12vigia` (el reloj de la ventana de prueba F12)

Sobre de1498b (la junta de borrar-capas, N7). La verificacion final dio la escena `f12vigia` DIFERENTE de forma repetida: el reloj
`D3D9Star.cs:reloj-F12` (GpuTestWindow.timer, 15 ms) contaba 233, 228, 226 y 221 Tick en el programa nuevo frente a 280, 263, 259 y 283 en la
referencia (holgura 27), y antes de N7 salia IGUAL.

**Resultado: no es un fallo del programa ni lo trajo N7. Era la PRUEBA: la cuenta de ese reloj no se puede comparar con una holgura fija.**
No se toco `Codigo/`, `Nativo/` ni `Plataforma/` (`git diff de1498b -- sistema/rocola/Xp/Codigo sistema/rocola/Xp/Nativo
sistema/rocola/Xp/Plataforma` sale vacio): solo `Pruebas/Ventanas` y este documento.

## La causa, con medidas

Todo medido el 14-11-2026 con la maquina libre (`top`: 100 % en reposo), fuera de pantalla (llvmpipe), alternando los programas en cada
vuelta, con `Pruebas/Ventanas/guiones/reloj_saturado.py mide` sobre las trazas (`ROCOLA_TRAZA_VENTANAS`).

1. **El reloj va saturado.** Cada Tick de la ventana F12 pinta su campo de estrellas y lo presenta (la pantalla compone): 7 a 10 ms en
   llvmpipe. Entre dos Tick, si el reloj no ha vencido, sale `Application.Idle` y la rocola pinta su cuadro (unos 20 ms). Un Tick y un cuadro
   de la rocola pasan de dos plazos (30 ms), asi que el reloj no llega a sus 15 ms: en los 5,25 s que la escena tiene la ventana abierta sin
   capturas da de 200 a 290 Tick, no los 350 del plazo. En la traza se ven dos intervalos: el corto (dos Tick seguidos) y el largo (un Tick
   y un cuadro de la rocola).

2. **La cuenta de un reloj saturado no es monotona con la velocidad.** Con la regla de WM_TIMER (Nativo/PantallaRocola.cs, la misma en la
   referencia: los vencimientos van en una rejilla fija de 15 ms y los que se juntan salen como uno), que el Tick siguiente caiga antes o
   despues de un vencimiento cambia con un milisegundo. El modelo (`reloj_saturado.py simula`), con un Tick de 8 ms y el cuadro de la rocola
   de 18, 19, 20, 21, 22 y 24 ms: **263, 297, 219, 343, 175 y 197 Tick**. Una maquina mas lenta puede dar MAS Tick.

3. **Medido: el signo de la diferencia cambia con la velocidad de la maquina.** Mismos binarios, alternados; la velocidad de llvmpipe se
   bajo con `LP_NUM_THREADS` (4 es lo de fabrica en esta maquina):

   | llvmpipe | referencia (aef2211) | antes de N7 (535be08) | nuevo (de1498b) |
   |---|---|---|---|
   | 4 hilos (6 vueltas) | 214, 219, 215, 212, 210, 215 | 245, 248, 243, 251, 230, 233 | 228, 241, 236, 250, 244, 241 |
   | 3 hilos (2 vueltas) | 253, 257 | 242, 234 | 233, 221 |
   | 2 hilos (2 vueltas) | 286, 276 | 261, 272 | 271, 282 |

   Con 4 hilos el nuevo da unos 28 Tick MAS que la referencia; con 3, unos 28 MENOS (lo que vio la verificacion: 221-233 frente a 259-283: la
   maquina iba entonces un 20-30 % mas lenta que ahora); con 2, lo mismo. La referencia contra si misma va de **203 a 286** (un 29 %) segun
   la velocidad: la holgura de 27 (el ruido de 3 corridas seguidas, `ventanas2/ruido/f12vigia.json`: 18, mas la mitad) no vale para este
   reloj. El de la escena `f12` ya lo decia: su ruido medido es 191.

4. **N7 no cambio nada aqui.** 535be08 (el programa de antes de N7, compilado aparte) da lo mismo que de1498b en las tres velocidades
   (tabla), con los mismos intervalos (corto 7,4-8,2 ms, largo 27,3-29,0 frente a 7,3-8,2 y 28,0-29,3). Que antes saliera IGUAL fue por la
   velocidad de la maquina de ese dia (las corridas guardadas de f12-como-hoja, del 12-11: 231-251 las dos).

5. **De donde sale la diferencia de coste: una capa transparente de la referencia.** La capa de ventanas de la referencia arma, encima de
   la tarjeta de la ventana F12, otro tramo de Dibujo2D para los controles que lleve por encima (no lleva ninguno): 1030x732 transparente
   del todo, que se mezcla en cada composicion (6 capas con la ventana detras de la rocola; la hoja nativa, 5). Es lo aceptado desde
   f12-como-hoja (`compara_escena.py`: «transparente del todo en la referencia (la hoja no lo arma)»). Cuesta 1,5 a 2 ms por composicion en
   llvmpipe: intervalo corto 8,6-9,6 ms en la referencia frente a 7,3-8,8 en el nuevo; largo 30,0-31,5 frente a 28,0-29,6.
   **Prueba:** el programa nuevo con esa capa transparente puesta a proposito (un cambio de prueba en `Anfitrion.Capas`, no guardado),
   alternado 3 veces con la referencia y el nuevo:

   | | Tick | corto | largo |
   |---|---|---|---|
   | referencia | 210, 221, 211 | 9,6 / 8,8 / 9,1 ms | 31,5 / 30,0 / 30,6 ms |
   | nuevo + la capa transparente | 210, 217, 219 | 9,9 / 9,1 / 9,6 ms | 31,2 / 30,8 / 31,1 ms |
   | nuevo | 227, 232, 240 | 8,8 / 8,0 / 7,6 ms | 29,6 / 28,7 / 28,1 ms |

   Con la capa, el nuevo da lo que la referencia. El modelo con esos costes (referencia: Tick 9,2 ms y cuadro 21,2; nuevo: 7,5 y 20,5) da
   204 y 233 a la velocidad de hoy y 254-279 y 215-241 con la maquina un 20-30 % mas lenta: las dos situaciones medidas.

6. **No hay espera de mas ni composicion doble.** `PantallaRocola.PresentaTarjeta` hace lo que hacia `Escritorio.PresentaNativa` de la
   capa (apunta la presentacion, da por compuesto y compone una vez); `Anfitrion.Capas` arma las mismas capas en el mismo orden que
   `Escritorio.Capas` (menos la transparente); la presentacion fuera de pantalla es el mismo `glFinish`. Lo que se ve es igual (capturas y
   capas de las escenas, IGUALES).

**Lo correcto segun XP** es lo del programa nuevo: en XP la ventana F12 es un Form sin controles con un dispositivo de Direct3D 9 en su
ventana; encima de lo que presenta no hay nada que componer. La capa transparente era de la capa de imitacion de la referencia (borrada
en N7 por orden del dueno: nada que frene el procesador), y no se vuelve a poner.

## Lo hecho (solo en las pruebas)

- `Pruebas/Ventanas/guiones/compara_trazas.py`: los RELOJES DE CUADRO (`RELOJES_DE_CUADRO`: solo `D3D9Star.cs:reloj-F12`, el unico reloj
  de XP cuyo Tick pinta y presenta un cuadro entero de la tarjeta). Si uno va saturado en LAS DOS trazas (su intervalo medio, sin los
  huecos de mas de 10 plazos, pasa del 110 % del plazo), su TOTAL se compara con la holgura del ruido o con el 30 % de la cuenta mayor, lo
  que sea mas (lo que la referencia se separa de si misma: 203 a 286), y se dice en una linea «NOTA» con los dos ritmos y la holgura. Por
  segundo se compara como siempre, y si en alguna de las dos no va saturado, como cualquier reloj. Los demas relojes, los encargos, la
  secuencia, los estados y las capas, sin cambio. Las otras escenas con ese reloj (`f12`, `f12sigterm`, `f12fallo`) entran en la misma
  regla si va saturado en las dos trazas (ver «Revision»); las escenas sin ventana F12 comparan como antes.
- `Pruebas/Ventanas/guiones/reloj_saturado.py` (nuevo): `mide` (Tick, intervalo corto y largo de cada traza) y `simula` (el modelo de la
  regla de WM_TIMER con lo que cuestan los cuadros).
- `Pruebas/Ventanas/guiones/escenas.sh` y `Pruebas/Ventanas/LEEME.md`: la nota.

## Costuras

Ninguna quitada ni anadida (no se toco el programa).

## Comprobaciones

Maquina libre; el programa es de1498b sin cambios (solo cambian las pruebas), compilado en esta copia de trabajo.

| Comprobacion | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, 13 avisos (los de la base) |
| NativeAOT (`dotnet publish ... -p:PublishAot=true`) | salida 0, 0 avisos IL/AOT |
| `git diff de1498b` en `Codigo/`, `Nativo/`, `Plataforma/` | vacio (diff-xp.py no aplica: ningun archivo de `Codigo/`) |
| `f12vigia`, 5 corridas alternadas con la referencia (JIT) | **5 de 5 IGUAL**. Tick de la referencia / nuevo: 210/250, 210/252, 216/260, 226/238, 225/253 (holgura 71 a 78; con la holgura fija de 27, 4 de las 5 habrian salido DIFERENTE, y hacia el otro lado que en la verificacion) |
| `f12`, 3 corridas alternadas (JIT) | **3 de 3 IGUAL** (560/667, 607/662, 578/656; holgura 287, la del ruido) |
| `arranque`, `video`, `buscador`, `f2`, `aviso`, `f3vistas`, `f3teclas` alternadas (JIT) | las 7 IGUAL |
| `f12` en NativeAOT contra la referencia | IGUAL (597/635) |
| `f12vigia` en NativeAOT contra la referencia, 3 corridas | el reloj dentro de la holgura (229/246, 213/239, 210/251); la SECUENCIA difiere en la linea de siempre (el KeyUp del segundo F12 llega a la rocola y no a la ventana): la carrera del vigia del foco ya apuntada en PORT_f12-como-hoja.md («Lo que queda»): NativeAOT arranca unos 100 ms antes y el vigia (cada 1,5 s: 9037, ..., 15037 ms) cae entre el KeyDown (15004) y el KeyUp (15060). No es de este cambio ni de N7 |
| `pruebas_hoja.sh` | pasan 86, fallan 0 |
| `AOT=1 pruebas_pantalla.sh` | pasan 32, fallan 0 (JIT) y 32, 0 (NativeAOT) |
| `PantallaVistas/pruebas_vistas.sh` | pasan 78, fallan 0 |
| Las 29 cintas (`VistasFrontera/cintas.sh`, con el reproductor compilado de `Pruebas/Tarjeta` de hoy) | 29 iguales (6 616 cuadros), 0 distintas |
| Recorridos `ctrlf3` y `hojas` (con el candado, VOLCADO, CAPAS, HUELLAS; compara.py contra cmp-ref y cmp-ref2) | los 2 salieron con 0; texturas quietas 52 de 52; capas quietas 94 de 98: las 4 de siempre (PORT_JUNTAS.md: las tres de `ctrlf3` a los 46 s con la ventana F12 -la capa transparente que la hoja no arma corre los nombres- y la de `hojas` a los 18 s, el repaso) |

Nota: el reproductor de cintas que hay en `/root/rocola-trabajo/port/frontera/tarjeta/reproductor` es anterior a N7 y ya no carga el
programa (NullReferenceException en `CargaPrograma`); hay que compilar el de `Pruebas/Tarjeta/ReproductorCintas.csproj` y pasarlo con
`FRONTERA=` (aqui, en la carpeta de trabajo del grupo; no se toco la de frontera).

Las trazas de todas las corridas quedan comprimidas en `/root/rocola-trabajo/port/arreglo-f12vigia/trazas` (fuera del repositorio).

## Lo que queda

- El ruido guardado de `f12vigia` (`/root/rocola-trabajo/port/ventanas2/ruido/f12vigia.json`, fuera del repositorio) sigue con su 18: ya
  no decide el total de ese reloj cuando va saturado. No se volvio a medir (es de la carpeta de otro grupo).
- La cuenta por segundo de ese reloj (holgura 20 de unos 45) y el encargo del vigia de la pantalla (`MedidasBitacora.cs:129`, uno por
  vuelta del bucle) tambien son de la maquina: con llvmpipe frenado a 3 hilos el encargo salio una vez 456 frente a 410 (holgura 45),
  igual en 535be08 (422). Es lo ya aceptado («las cuentas de un encargo que se pasan con la maquina cargada se repiten una vez»).

## Riesgos

- La holgura relativa del 30 % deja pasar una diferencia real de ritmo de ese reloj menor que eso. Lo que sigue vigilado: que el reloj
  exista y de Tick solo con la ventana abierta (por segundo), la secuencia entera, los estados y las capas. El ritmo de un reloj que SI
  llega a su plazo se sigue comparando con la holgura del ruido.
- En el A4 (tarjeta de verdad, presentacion con el barrido) nada de esto se ha medido: alli un Tick de la ventana F12 espera al barrido y
  el reloj tambien ira saturado, como en XP con Direct3D 9; no se afirma nada del A4.

## Revision

Revisor independiente, 14-11-2026, sobre e55910c. Sin fallos en el programa (no cambia: `git diff de1498b` en `Codigo/`, `Nativo/` y
`Plataforma/` vacio; numero de lineas y CRLF de `Codigo/`, por tanto, los de de1498b).

- Leido el diff entero de `compara_trazas.py` y `reloj_saturado.py`. La regla nueva solo afloja el TOTAL de `D3D9Star.cs:reloj-F12` y solo
  si el intervalo medio pasa del 110 % del plazo en las dos trazas; la traza trae el plazo (`tick D3D9Star.cs:1867 15`) y la linea 1867 de
  `Codigo/D3D9Star.cs` sigue siendo la del `Reloj timer` de GpuTestWindow (1997 en la referencia).
- Repetido con las trazas guardadas del grupo (`/root/rocola-trabajo/port/arreglo-f12vigia/trazas`): las 5 parejas de `f12vigia` dan IGUAL
  con la NOTA (210/250, 210/252, 216/260, 226/238, 225/253; holgura 71-78) y con el `compara_trazas.py` de de1498b las tres primeras dan
  DIFERENTE (holgura 27): los numeros del informe salen. `reloj_saturado.py mide`: corto 8,6-9,5 ms y largo 29,7-31,2 en la referencia;
  7,2-8,3 y 27,2-28,7 en el nuevo (el nuevo es mas RAPIDO por cuadro, 1,5 a 3 ms: no hay espera de mas).
- `dotnet build -c Release --no-incremental`: 0 errores, 13 avisos.
- Corregido en este documento: decia que ninguna otra escena tiene ese reloj; lo tienen `f12`, `f12sigterm` y `f12fallo`. En `f12` no
  cambia nada (su ruido medido da holgura 287, mas que el 30 %); en las otras dos, que no tienen ruido medido, el total de ese reloj pasa
  a compararse con el 30 % si va saturado en las dos.
- No repetido (el informe lo da por pasado y la lectura no lo pone en duda, el programa es de1498b): NativeAOT, las escenas en vivo, los
  recorridos, las cintas, pruebas_hoja, pruebas_pantalla y PantallaVistas. La prueba de la capa transparente puesta a proposito (punto 5)
  y la tabla de `LP_NUM_THREADS` no se repitieron: quedan como las midio el grupo.
- Riesgo que se subraya: el 30 % (unos 75 Tick) es mas ancho que la mayor diferencia vista entre los dos programas a una misma velocidad
  (62: 221 frente a 283, un 22 %); sale del recorrido de la referencia entre velocidades distintas. Un freno real del Present de la hoja
  F12 de menos de un 30 % solo se veria con `reloj_saturado.py mide` (el intervalo corto y el largo), no con IGUAL/DIFERENTE.
