# Errores de rocola_error.txt (16 al 23 de octubre de 2026) — Modest, 23 de octubre de 2026

Se revisó el `rocola_error.txt` de la rocola real (XP, A4). Lo medido aquí es de Linux/Mono con fixtures reales; nada se ha probado en XP.

| Qué salía | Qué era | Qué se hizo |
| --- | --- | --- |
| `UnhandledException … A generic error occurred in GDI+` en `PintaFicha` (16-10, 13:45) | Cerraba la rocola: GDI+ no pudo dibujar una portada en la ficha y nada atrapaba la excepción en el bucle del cuadro. | El dibujo de la portada de la ficha **ya estaba protegido** en el código de hoy (la rocola del log era de antes). Además, ahora **un cuadro que truena ya no cierra la rocola**: se apunta en este mismo archivo («Un cuadro de la tarjeta fallo y se siguio», como mucho una vez cada 10 s con cuántos van) y se sigue; si fallan 3 seguidos, espera 200 ms para no girar en vano. |
| `PlayTrack … Got a frame at sample rate 32000, in an MP3 with sample rate 44100` (17-10) | Un MP3 que cambia de frecuencia a mitad: NAudio no lo soporta. La rocola ya intentaba reconstruirlo con los cuadros de la frecuencia dominante, pero (1) **solo miraba la frecuencia**, no los canales: un trozo mono dentro de un MP3 estéreo se colaba y rompía el descompresor; y (2) si la reconstrucción fallaba, **no quedaba escrito por qué**. | La reconstrucción elige ahora el formato dominante por **frecuencia y canales**. Y si tampoco puede, escribe el motivo («MP3 con frecuencias mezcladas: la reconstruccion tampoco pudo con …»). Batería `mp3mezclado` con dos MP3 de verdad (frecuencia mezclada y canales mezclados): NAudio rechaza los originales y los reconstruidos abren con 2 s. **Si ese archivo concreto sigue fallando, el motivo quedará en este archivo.** |

## Lo que NO es un error (informativo)

- `Sin huecos`: dice que el recorte LAME está en uso.
- `Rojo mantenido sin pistas …`: avisos de la guía del rojo.
- `Latencia de audio` y `Cortes de sonido` (nuevos del 22-10): la rocola ajustó su margen de audio (1000/750/500 ms). El 23-10 hubo atascos de 59, 193 y 435 ms y el margen subió cada vez; un solo aviso tardío (435 ms en «Enjoy the Silence») y ningún hueco apuntado.

## Lo que sigue pendiente: las paradas de pantalla

`Pantalla parada 500 a 2063 ms` aparece unas 20 veces en la semana. Dos patrones:

- **«publicar la biblioteca»** (1172, 1766, 719, 578, 547, 532 ms…): ocurre al terminar de escanear/cargar la biblioteca, todo en el hilo de la pantalla. El apunte del 23-10 11:52 ya trae el contexto nuevo: `gc_desde_el_apunte_anterior=181/18/2` con montón gestionado de 132 MB, 15 hilos y 1752 MB de RAM libre. Es una **coincidencia** con 2 recolecciones completas, no una causa probada. No hay RAM justa (30 % de carga).
- **«un cuadro de la tarjeta»** (515, 2063 ms), **«el latido»** (1469 ms), **«el reloj de la canción»** (1000 ms): paradas largas sueltas; las tres con `repaso_pendiente` en 0 o con el repaso de 74.000 a 116.000 canciones corriendo (las de 2063 y 1000 ms).

No se tocó nada de esto: sin un perfil real no se sabe qué parte de «publicar la biblioteca» pesa (hay una docena de pasos seguidos) y mover o repartir esos pasos sin medirlos arriesga más de lo que gana. **Siguiente paso:** con `diagnostico=1`, la línea `peores cuadros` (libro de los peores cuadros, 20-10) y el contexto de parada dicen qué fase pesa; pasar el `rocola_diagnostico.txt` de una sesión en la rocola.
