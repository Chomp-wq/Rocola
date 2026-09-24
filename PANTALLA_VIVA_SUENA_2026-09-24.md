# La pantalla viva, tanda 3: lo que suena y los créditos

Cambio del 24 de septiembre de 2026, compartido por HD y Modest. Son las ideas del lienzo de bocetos para lo que suena,
las monedas y los avisos. Solo cambian el dibujo por tarjeta gráfica. El renderizador por procesador sigue congelado y
sale exactamente igual: una batería lo comprueba píxel a píxel contra el programa compilado sin tarjeta gráfica.

Cada idea tiene su interruptor en Ctrl+F3 → **Pantalla**. La pestaña tiene ahora dos hojas, con un botón arriba para
cada una: **La pared y el disco** (las tandas 1 y 2, como estaban) y **Lo que suena y los créditos** (esta). Todas
vienen encendidas y se guardan en `rocola_config.txt`. Sin tarjeta gráfica los interruptores se guardan, pero no cambian
nada de lo que se ve.

El código nuevo está en `Rocola/SuenaVivoGpu.cs`. Los demás archivos llevan solo los enganches: el panel de reposo
(`EstadoReposoGpu.cs`), la cápsula de créditos (`Form1.Vistas.cs`), los avisos (`Form1.Ajustes.cs`), el cuadro de la
tarjeta (`D3D9Star.cs`) y la pestaña (`PantallaVivaGpu.cs`).

## 1. Lo que suena, en grande — `suena_grande=`

Es el panel que crece abajo cuando nadie toca la rocola durante 15 segundos ("A H O R A   S U E N A").
- Un **vinilo gira detrás de la portada** y asoma por su derecha a la vez que el panel crece. Es el mismo dibujo que el
  de dentro del disco (surcos, etiqueta con la portada y aro del color del género) y gira a 33 ⅓.
- El texto se corre lo que asoma el vinilo. En 1024×768 son 69 píxeles y la insignia MEJORADA se corre con él.
- **El título va en una sola línea.** Si cabe, quieto. Si no cabe, se desliza dentro de su caja:
  1. se queda quieto 2,2 s para leerse;
  2. va hasta el final a unos 42 px por segundo;
  3. se para 1,4 s;
  4. vuelve.

  Los bordes se desvanecen, así nunca se corta de golpe, y no hay saltos: como mucho se mueve 3,5 px de un cuadro al
  siguiente.
- **Cuánto falta:** "1:47 · faltan 2:25", del mismo instante de la canción.
- **La pastilla del género** va junto al artista, o debajo si al lado no cabe entero: el artista manda. Tiene un
  brillo de su color que sube con los golpes de graves y se apaga solo. Es solo luz: la pastilla no se mueve ni cambia
  de tamaño.
- **Las siguientes dicen en cuánto suenan**, en azul: "en ~6 min". Es la misma cuenta que "EN COLA 3 · en ~9 min" de
  dentro del disco. Si no se sabe lo que dura alguna de las de delante, no se inventa.
- El panel se rehace solo cuando cambia algo de lo que enseña, como antes. Un minuto de la cola o el género que llega
  tarde avisan al panel. El reloj se parchea cada segundo sin rehacer nada más.

## 2. La moneda que cae — `moneda_cae=`

- Al meter una moneda (o al dar créditos de servicio) cae una **moneda dorada** desde unas tres cápsulas y media de
  alto. Cae acelerando, como algo que pesa, y gira dos vueltas y media.
- **Aterriza en el número** justo cuando el número rueda: tarda lo mismo, 420 ms.
- Al tocar, la cápsula da un **golpe** (crece un 12 % y vuelve) con un **destello verde** detrás.
- Sube **"+4 créditos"** (lo que entró de verdad) y se desvanece en un segundo.
- La monedita de dentro de la cápsula no se pinta mientras tanto, para que no haya dos monedas.
- Con la moneda a la vista, el aviso de arriba "+4 creditos" no sale: lo dice la cápsula. En modo atracción, o con la
  idea apagada, sale como siempre.

## 3. Sin créditos, invitar — `sin_creditos_invita=`

- Si alguien intenta poner algo sin créditos, en lugar del letrero naranja "Inserta una moneda" sale una **tarjeta
  amable** en el centro:
  - **"Te falta un crédito"**;
  - "Inserta una moneda para poner «Hermoso Cariño»";
  - "Una moneda = 4 canciones".
- La tarjeta lleva una ranura con una moneda que entra una y otra vez, y una línea que va enseñando cuánto le queda.
- **Se va sola** a los 5 segundos, o en cuanto llegan créditos.
- Si la persona insiste, la tarjeta no vuelve a entrar: solo se alarga. La cápsula sigue sacudiéndose como antes.
- **No pone nada en la cola sola.** El crédito lo gasta quien lo mete, en lo que elija: así una moneda ajena nunca paga
  una canción que pidió otro.
- No sale en modo atracción ni con un vídeo a pantalla completa.

## 4. Avisos que se entienden — `avisos_claros=`

- Los avisos **entran por la derecha**, arriba, frenando. Se quedan y se van hacia la derecha apagándose.
- Cada tipo lleva su **color y su icono**. El tipo sale del color con que lo pide cada sitio del programa:

  | Tipo | Color | Icono | Cuándo |
  |---|---|---|---|
  | Hecho | verde | palomita | canción o vídeo pedido, ajustes guardados… |
  | Cuidado | amarillo | exclamación | falta un crédito, "esa ya va seguida"… |
  | Error | rojo | cruz | "no se pudo reproducir: crédito devuelto"… |
  | Novedad | azul | nota musical | "carpetas actualizadas", "reanudando la música" |

- **Dos renglones:** lo que pasó arriba y el detalle debajo ("No se pudo reproducir" / "credito devuelto").
- Al pedir una canción dice **cuál y cuánto falta**: "Pusiste «El Rey»" / "va la 3.ª en la cola · en ~9 min". Si no
  sonaba nada: "ya está sonando".
- Una **línea** abajo va enseñando cuánto le queda al aviso.
- Duran **2,8 s** en vez de 1,3, porque son dos renglones. Mientras se ven, las monedas y las canciones seguidas se
  siguen sumando como antes.
- En el buscador también salen así, encima de todo.

## Lo que cuesta

Medido en este contenedor, con Mono. Sirve para comparar unas cosas con otras; los milisegundos del bar serán otros.

| Qué | Cuánto | Cuándo pasa |
|---|---|---|
| Hornear un aviso | 0,27 ms | Una vez por aviso |
| Hornear la tarjeta de sin créditos | 0,69 ms | Al pedir algo sin créditos |
| Hornear la moneda | 0,48 ms | Una vez |
| El panel de reposo en grande | 1,50 ms (el de siempre: 1,30 ms) | Solo cuando cambia algo de lo que enseña |
| La curva del aviso, la caída de la moneda y el deslizar del título | 3 µs juntas | Cada cuadro |

## Cómo se comprobó y qué falta

- **Pruebas automáticas.** La batería nueva `pruebas/suenavivo.cs` corre con los dos perfiles:
  - Con tarjeta gráfica: 83 comprobaciones.
  - Sin tarjeta gráfica: 9. Los enganches no hacen nada: vuelven el letrero naranja, el "+4 creditos" de arriba y los
    avisos de 1,3 s. El resto es la pestaña.

  Entre las comprobaciones con tarjeta:
  - La cápsula de créditos con una moneda entrando, pintada por el procesador, sale idéntica píxel a píxel a la del
    programa sin tarjeta en 9 momentos.
  - El panel de reposo con la idea apagada sale idéntico al de siempre.
  - Se rompió a propósito cada protección de la tanda (61 mutaciones) y la batería las cazó todas. Tres sobrevivieron
    al principio y ahora se cazan:
    - el "+N créditos" inventado: ahora se prueba también con 10 créditos de servicio;
    - el título largo que nunca se horneaba: ahora se mira la tira que recibe la tarjeta;
    - el botón "Pantalla como de fábrica" que solo movía los interruptores: ahora se mira que ponga las preferencias.
  - Tandas completas en verde en las dos ramas.
- **Las baterías de antes** se pusieron al día donde cambió algo a propósito: la pestaña Pantalla tiene hojas y
  `pestanas` conoce los cuatro interruptores nuevos.
- **Simulación, no comprobación real.** Con las mismas funciones del programa se compuso el panel de reposo, los cuatro
  avisos, la tarjeta de sin créditos y la caída de la moneda a 1024×768. No es la tarjeta gráfica dibujando.
- **Sin comprobación visual real.** En este contenedor no hay Direct3D: hace falta mirarlo en el equipo del bar y en
  el de HD.
- **Sin probar en Windows XP.** La rama Modest compila con el lenguaje de .NET 3.5 (C# 3), pero no se ha probado en un
  Windows XP real.

## Lo que viene

4. **Modo atracción.** Hecha: el escaparate y lo más pedido esta noche. Ver
   [la guía de la tanda 4](PANTALLA_VIVA_ATRACCION_2026-09-24.md).
5. **Estilo general.** Hecha: letra para leer de lejos y el color del género en todo. Ver
   [la guía de la tanda 5](PANTALLA_VIVA_ESTILO_2026-09-24.md).
