# Ficha del disco: error de GDI+ visto en la maquina real

Las fotos del registro de la version del 16/10/2026 11:11 muestran
`ExternalException: A generic error occurred in GDI+`, en `Graphics.DrawImage`
desde `PintaFicha` (PantallaVivaGpu.cs, antigua linea 612). El recorrido sigue por
`HorneaFicha`, `BuildFichaState`, `PrepararPantallaViva` y el render del panel.
Eso situa el fallo en la portadita de la ficha emergente. Las fotos no permiten
determinar por si solas si la imagen estaba invalidada, si dependia todavia del
decodificador o si la rechazaba la version de GDI+ de la maquina.

## Cambio, solo Modest GPU

- La lectura de miniaturas para publicar en GPU copia sus pixeles a un bitmap
  propio antes de cerrar el stream y reutilizar su arreglo. Copia filas PArgb,
  sin otra composicion del alfa ni un arreglo grande nuevo por portada. Es una
  defensa frente a dependencias del decodificador; no se afirma haber reproducido
  ese mecanismo concreto de fallo en XP.
- La portadita se dibuja indicando todo el origen en pixeles, sin depender del
  DPI del archivo.
- Si esa imagen produce `ExternalException` o `ArgumentException`, se restaura
  el estado grafico y se termina la ficha con el recuadro de portada ausente.
  No se deja escapar ese error al bucle del panel ni se repite en cada cuadro
  de la misma ficha. El texto y las canciones siguen dibujandose.

Sin cambios en el renderizador CPU, audio, biblioteca ni archivos de datos.
Como salida temporal para el ejecutable anterior, se puede apagar **Ficha
rapida del disco** en Ctrl+F3; evita este recorrido concreto de dibujo.

## Verificacion y limites

Compilacion de ambos perfiles con advertencias como errores, C# 3 y referencias
oficiales .NET Framework 3.5. Ejecucion automatizada en Linux con Mono 6.12,
libgdiplus y Xvfb; Mono usa CLR 4 porque no tiene CLR 2 instalado. Esto comprueba
compilacion contra el destino y comportamiento sintetico, no ejecucion real en XP.

`pruebas/fichaportada.cs`: 9 comprobaciones GPU. La version anterior falla las dos
de portada retirada: propaga una excepcion y deja incompleta la tarjeta. La
correccion pasa ambas. Tambien compara pixeles/alfa de PNG y JPEG despues de
cerrar el stream, sobrescribir el arreglo de lectura, borrar la imagen del disco
y ejecutar GC; dibuja posteriormente la ficha y compara el resultado con DPI
distintos. No reproduce artificialmente la `ExternalException` exacta de las fotos.

Tambien pasan `caratulasjpeg` (26), `hojeofino` (35) y `papeleraportadas` (29).
La prueba nueva queda registrada en `todo.sh` y en
`validar-revision-octubre.ps1`, para poder ejecutarla con el GDI+ de Windows.

Dos baterias amplias no terminan correctamente en este entorno: `pantallaviva`
avanza por las comprobaciones de ficha y luego falla en la animacion de pagina
por falta de `RtlMoveMemory` de Windows; `auditoriageneral` detecta diferencia
de color 64 en la ruta historica de clonacion de miniaturas transparentes.
Ambos resultados tambien ocurren compilando los archivos originales de HEAD,
sin esta correccion. No se modifican ni se omiten sus aserciones.

Pendiente: ejecutar el nuevo binario en la rocola real, dejar aparecer la ficha
en los discos afectados y verificar que no reaparezca el error. No se han medido
FPS, intervalos de cuadro, audio real ni hardware Direct3D en esta sesion.
