# Despliegue y comprobacion con biblioteca real — 30-09-2026

## Instalaciones actualizadas

Con autorizacion del usuario para compilar y probar, se desplego Release de las
dos ramas en las carpetas existentes. Solo se sustituyeron Rocola.exe, Rocola.pdb
y NAudio.dll, y se agrego el aviso NLayer. No se sustituyeron configuracion, musica,
creditos, cola, tablas, miniaturas ni modelos originales.

- Modest: `D:\Release1`, compilacion MSBuild 3.5 con `/tv:3.5`, destino .NET 3.5.
- HD: `D:\ReleaseHD`, compilacion MSBuild actual, destino .NET 4.8.
- Release optimizado, advertencias del compilador tratadas como errores. MSBuild
  3.5 emite el aviso conocido de SDK 6.0A ausente y termina correctamente.

El contenido instalado coincide por SHA-256 con la compilacion temporal:

| Version | SHA-256 de Rocola.exe |
| --- | --- |
| Modest | `D71B708C8E3A611ABD15184734B76378D700533BBF34602B5DE658C2DDF2E8F9` |
| HD | `C51144617961289BD3902115391B0AC41E18EFB866BF045A4B727AB290E263EE` |

Los ejecutables proceden de los cambios de produccion de los commits
`e102667bc0ca0dc4f794dc46d9dfc1ad4d37289d` (Modest) y
`de05bfdb1c5c9211459e4d0151278dbe91fb1742` (HD).

Antes de sustituir cada archivo existente se guardo en `respaldo` de SU instalacion,
dentro de `codex-atlas-85117cca24cd41f8aeba47f7749e06c0`. Esa copia permite volver al
ejecutable anterior sin restaurar ni sobrescribir los datos del usuario.

## Sesiones aisladas con datos reales

Las sesiones de prueba utilizaron copias de las tablas y las 4475 miniaturas en
TEMP, con `ROCOLA_DATOS` apuntando a esas copias y `dir=` a la biblioteca existente.
La recogida de material IA estuvo desactivada SOLO en las copias. Se copiaron los
dos modelos pequenos para mantener inferencia; no se copio ni modifico el material
de entrenamiento de mas de 11 GB. Los archivos de datos originales conservaron
sus fechas de modificacion anteriores al despliegue.

La comprobacion visual inicial mostro el muro y caratulas reales a 3440x1440.
El control automatico del dialogo de medicion encontro un problema de coordenadas
del helper y no completo la medicion por clics. No confundir esa limitacion con un
fallo del atlas ni presentar esa tentativa como una medicion terminada.

Para medir se agrego `pruebas/sesionreal.cs`: abre Form1 completo, espera la biblioteca,
llama al MISMO medidor integrado con cancion real muda y ejecuta cambios internos
de pagina con el bucle de eventos y dibujo real GPU. No inyecta teclas ni mide su
latencia fisica. Se ejecutaron ambas ramas SECUENCIALMENTE, no compitiendo entre si.
Biblioteca cargada: 62776 canciones; panel GPU: 3440x1440. Se uso la seccion C,
con suficientes discos para que pasar de pagina realmente cambiara de contenido.

## Resultados observados en este PC

| Escena del medidor | Modest: trabajo medio ms | Modest: Present/s | HD: trabajo medio ms | HD: Present/s |
| --- | ---: | ---: | ---: | ---: |
| Pasando de pagina | 1.312 | 178 | 1.125 | 179 |
| Cambiando de letra | 1.609 | 170 | 1.431 | 172 |
| Escribiendo en el buscador | 0.655 | 178 | 0.494 | 178 |
| Entrando a discos | 1.670 | 148 | 1.511 | 149 |
| Pantalla de atraccion | 0.430 | 179 | 0.272 | 180 |

Present/s cuenta presentaciones aceptadas por Direct3D, no fotones, Hz efectivos
ni una medida de latencia del monitor. El trabajo medio excluye espera de monitor
y no equivale al peor cuadro ni a percentiles de tirones. En ambas sesiones el
medidor confirmo audio real activo y mudo.

Despues, seis paginas nuevas o revisitadas de C, con un segundo de precarga entre
solicitudes. Cada resultado exige portada en memoria y la textura GPU del bitmap
vigente, no solo un placeholder ni una textura antigua.

| Pagina | Modest: NextPage ms | Modest: espera texturas ms | HD: NextPage ms | HD: espera texturas ms |
| --- | ---: | ---: | ---: | ---: |
| 1 | 0.563 | 0.549 | 0.463 | 0.204 |
| 2 | 1.929 | 17.782 | 1.422 | 24.070 |
| 3 | 1.726 | 18.380 | 1.478 | 15.156 |
| 4 | 1.864 | 26.538 | 1.416 | 15.776 |
| 5 | 1.753 | 16.351 | 1.569 | 16.248 |
| 6 | 1.799 | 15.698 | 1.431 | 13.705 |

La suma de ambos tiempos estuvo por debajo de 29 ms en estas doce solicitudes.
No se espera a acabar la animacion, solo a tener los recursos listos. El medidor
previo ya visito algunas paginas y las miniaturas existian; NO es una prueba de
disco frio, primer escaneo ni lectura de todas las portadas originales sin cache.
Los errores de UI del recorrido final fueron cero en ambas ramas.

Tras la ultima comprobacion del instrumento (rechazo explicito de GPU no activa),
se recompilo con advertencias como errores y se repitio `--solo-paginas` en ambas
ramas. Las otras doce solicitudes completaron sin errores: suma maxima 30.740 ms
en Modest y 24.462 ms en HD. La variacion confirma que no debe prometerse un tiempo
exacto fijo; en los 24 pasos de esos dos recorridos el maximo fue inferior a 31 ms.

## Limites y conclusion

La mejora esta compilada e instalada, y la navegacion pausada probada carga rapido
a resolucion alta en ESTE equipo y con estas caches. No demuestra carga instantanea
universal ni permite atribuir toda la mejora al atlas: no se hizo una comparacion
antes/despues de estas sesiones completas en condiciones iguales.

Los atlas reducen memoria y transferencia, pero siguen componiendo las caratulas
con GDI para conservar el aspecto. Las rafagas, paginas sin precarga, primer arranque,
datos originales ausentes y equipos modestos pueden dar otros tiempos. Windows XP,
CPU antigua fisica de dos nucleos y otro HDD no se han certificado con esta prueba.

El instrumento es opcional y no se agrega al runner sintetico. Su uso y requisitos
de aislamiento estan en `pruebas/LEEME.md`. Los intentos iniciales del instrumento
detectaron fallos del propio arnes (faltaba impulsar el dibujo en un bucle anidado
y se permitia entrar en atraccion por falta de actividad); se corrigieron en el
instrumento, no en el renderizador de produccion. CPU sigue congelado.
