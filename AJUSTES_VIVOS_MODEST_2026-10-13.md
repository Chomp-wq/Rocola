# Ctrl+F3: dependencias vivas, guardar al momento y ajustes inútiles fuera — Modest, 13 de octubre de 2026

Petición del usuario: «Haz A1 y A2, y borra settings que ahora sean inútiles, por ejemplo cosas como la pared de discos
que respira es inútil, ya que al cabo de unos segundos se oculta todo mientras suena la música, borra ese tipo de cosas».

A1 y A2 son las dos primeras propuestas de la revisión de Ctrl+F3
([AJUSTES_CTRL_F3_REVISION_MODEST_2026-10-13.md](AJUSTES_CTRL_F3_REVISION_MODEST_2026-10-13.md)).

## A1. Lo que depende de otro ajuste, en gris y diciendo qué le falta

Si un interruptor solo funciona con otro encendido y ese otro está apagado:

- se pone **en gris** y no se puede tocar;
- dice qué le falta, en ámbar: **«Necesita: … (página)»**;
- **vuelve solo** en cuanto se enciende lo que le faltaba, aunque esté en otra página.

Su valor no se toca: se guarda tal como estaba y vale en cuanto puede.

| Este (página) | Necesita (página) |
| --- | --- |
| Estado ampliado transparente (Fondo) | Estado ampliado en reposo (Imagen) |
| Cola ampliada de 12 canciones (Fondo) | Estado ampliado en reposo (Imagen) |
| Ocultar controles en reposo (Fondo) | Estado ampliado en reposo (Imagen) |
| Estado ampliado en reposo (Imagen) | Ocultar discos en reposo con música (justo encima) |
| Interior de discos con cristal (Fondo) | Lista de canciones transparente (Imagen) |
| Neón de progreso intenso (Fondo) | Barra de progreso neón (misma hoja) |
| Fuerza del detalle natural (Fondo) | Nebulosas con detalle natural (hoja Cielo y aurora) |
| Ecos de color de la estrella (Fondo) | Estrella viajera con órbitas (misma hoja) |
| Más profundidad al pasar de página (Fondo) | Pasar de página deslizando (Pantalla) |
| Paleta completa de carátula (Fondo) | Que el fondo tome el color de la portada (Detalles) |
| Disco 20 % / Mitad / Disco 100 % (Colores) | Que el fondo tome el color de la portada (Detalles) |
| Modelos del DSEE por género (Sonido → Mejoras con IA) | El DSEE encendido (Sonido) |

Cuatro se añadieron a la lista de la revisión, porque el código muestra que tampoco hacen nada sin su requisito:

- el estado transparente;
- ocultar controles en reposo;
- los ecos de la estrella;
- los pesos del disco.

El interruptor «Estado y cola ampliados en reposo» se llama ahora **«Estado ampliado en reposo»**: con el nombre largo no
cabía «Necesita: la de arriba» en su renglón.

**Sin tarjeta gráfica**, lo que solo se dibuja con ella va en gris y cada página o hoja lo dice una vez. Así queda en
Fondo, Pantalla y en las hojas Fluidez y Performance de Imagen. Los cinco interruptores de Imagen lo dicen en la tarjeta
de la propuesta.

- **Ningún nombre lleva ya «(GPU)».**
- Los niveles de Efectos, los colores y Detalles siguen vivos sin tarjeta, porque también valen así.

Los botones en gris se ven en gris (antes, un botón desactivado se veía igual que uno normal).

## A2. Se guarda al momento

- Lo que se toca en Ctrl+F3 se guarda en cuanto **se deja quieto medio segundo**, sin esperar a cerrar la ventana. Una
  barra que se arrastra no escribe en cada paso.
- Se escribe **detrás de la pantalla**, por el mismo camino seguro de siempre (archivo temporal, bajado al disco y
  cambiado de sitio de una vez).
- **Al cerrar se guarda otra vez**, como antes.
- Cada guardado lleva su turno: si uno de detrás llega tarde, no pisa a uno más nuevo.

Si se va la luz con la ventana abierta, ya no se pierde lo tocado, salvo lo del último medio segundo.

## Ajustes retirados

| Ajuste | Por qué no servía | Qué queda |
| --- | --- | --- |
| **«Que la pared respire cuando nadie la usa»** y **«Cuánto respira»** (Detalles) | Respiraba en reposo, y con música la pared se oculta a los pocos segundos de reposo: no se llegaba a ver. | La pared y el cielo con su brillo de siempre, quietos. La aurora del horizonte sigue respirando sin música, con su freno. |
| **«Respuesta inmediata al toque»** (Fondo) | Es rendimiento, no aspecto: apagado solo hacía que la pantalla tardara más en responder. Según AGENTS.md, eso va siempre activo y sin interruptor. | Siempre activa. |
| **«Transiciones suaves de color»** (Fondo) | Solo actuaba con el buscador abierto al cambiar de canción, y apagado dejaba el buscador con el color de antes. | Siempre activa. |
| **«Ondas unidas al estado»** (Fondo) | Apagado, la aurora se despegaba del estado ampliado. | Siempre activa. |

- **Las claves viejas se ignoran** al leer una configuración y ya no se escriben: `respira=`, `respira_fuerza=`,
  `respuesta_inmediata=`, `colores_cache=` y `ondas_unidas=`. Una rocola vieja que las tenga apagadas pasa a tenerlas
  como de fábrica.
- **Detalles** queda con sus seis interruptores en dos tarjetas: «Al pedir canciones» y «El fondo y la barra».

Revisé los demás ajustes buscando más casos así: efectos que solo actúan con la pared a la vista en reposo, o
interruptores que no hacen nada.

- Los demás sí se ven:
  - lo de reposo (estado ampliado, realzar la estrella, ocultar controles) se ve precisamente con la pared oculta;
  - la ficha del disco sale al segundo.
- Todos los demás interruptores llegan al dibujo.

Por eso no quité nada más.

**Dibujo por CPU:** sus 14 métodos son idénticos instrucción por instrucción. El cielo por CPU deja de respirar
porque la respiración se quitó, como se pidió; el cálculo vivía fuera de esos métodos.

## Cómo se comprobó

- **`ajustesvivos`** (nueva). Pasa con tarjeta (50 comprobaciones) y sin ella (32).
  - Arma las páginas de verdad con la letra ancha de Linux:
    - ningún nombre lleva «(GPU)»;
    - con todo encendido no hay nada en gris;
    - con los requisitos apagados, todo lo que depende sale en gris, dice qué le falta y el texto cabe;
    - al crecer, ningún interruptor pisa a otro;
    - no cambia ningún valor guardado.
  - Entre páginas y al momento:
    - apagar el estado ampliado en Imagen pone en gris sus tres de Fondo, y vuelven al encenderlo;
    - la lista transparente con el cristal;
    - el color de la portada (Detalles) con la paleta y los tres pesos (Colores);
    - la pared en reposo con el estado ampliado.
  - Sin tarjeta:
    - todo lo de la tarjeta en gris, con un aviso que cabe por página u hoja;
    - los colores siguen con sus dependencias;
    - con la tarjeta vuelve todo.
  - Guardar al momento:
    - no escribe sin cambios;
    - espera a que se deje quieto;
    - escribe sin cerrar;
    - un guardado viejo que llega tarde no pisa al nuevo.
  - Los ajustes retirados: no están; una configuración vieja con sus claves se lee bien y al guardarla ya no salen.
- **Al día:**
  - `respira` ahora vigila que la pared no respire y que la aurora siga frenando igual;
  - `detalles`, `cristalcola` (64 combinaciones en vez de 512), `ajustes`, `bloqueos` y `iadatos` se ajustaron a lo
    retirado;
  - las que buscaban los nombres con «(GPU)»;
  - las que leían las claves en `SaveConfigPrefs`, que ahora se arman en `TextoConfig`;
  - `segundatanda` ya no mira la vista previa de la respiración.
- **`ventanaf3`** (la ventana entera, todas sus páginas y hojas, con dos letras): todo cabe y nada queda debajo de
  Listo.
- **Comprobación visual** con capturas de las páginas armadas (Fondo → Interfaz e Imagen) con los requisitos apagados:
  gris y el «Necesita» en ámbar debajo del nombre.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo, y uno nuevo: `segundatanda`
  todavía tocaba la vista previa de la respiración. Se puso al día y pasa en los dos perfiles.
- **Dibujo por CPU:** sus 14 métodos, idénticos a la versión anterior.

**No probado aquí:**

- **La ventana en la rocola del bar, con sus letras de Windows.**
- **Un apagón real con la ventana abierta.**
- **Windows XP.**
