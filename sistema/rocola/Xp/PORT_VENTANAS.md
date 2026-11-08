# Port nativo: las ventanas

Pista de las ventanas del port nativo de la rocola de XP a Rocola OS (decisiones del 8-11-2026 en AGENTS.md, «Diseno del port
nativo»): lo que en XP hace WinForms (el bucle de mensajes, los temporizadores, los encargos de otros hilos, el foco, las teclas, las
hojas) pasa a piezas nativas propias que el codigo de XP llama directo, sin clases que imiten a Form, Control ni Timer. Se compara
siempre contra la copia congelada de la referencia (`/root/rocola-trabajo/referencia-xp`, aef2211), que nunca se toca. Base de la
rama: 34cb741. Nada de esto se ha probado en el A4.

## V1. El banco

Lo que llega al codigo de XP desde las ventanas, apuntado en el sitio por donde pasa (hoy la capa de `Plataforma/Ventanas.*.cs`;
despues la pantalla y la hoja nativas), igual en el programa y en una copia de trabajo de la referencia:

- `ROCOLA_TRAZA_VENTANAS=ruta` (`Plataforma/TrazaVentanas.cs`): cada tecla con su keyData, si es la repeticion (el bit 30 de
  WM_KEYDOWN) y lo que devolvio ProcessCmdKey; cada KeyDown (con Handled y SuppressKeyPress), cada caracter a KeyPress (en la
  rocola, Form_KeyPress por KeyPreview), cada KeyUp; cada Tick con el sitio donde se creo su reloj (archivo:linea) y su intervalo;
  cada encargo (BeginInvoke) con el sitio de la llamada, y los que se pierden porque su ventana ya no esta; cada aviso del raton con
  su destino; cada cambio de foco; cada ventana que se ensena o se esconde (con su DialogResult), el fin de cada ShowDialog; Shown,
  FormClosing y FormClosed con la razon del cierre.
- `ROCOLA_ESTADO_EN=S:ruta,...` (`Plataforma/VolcadoEstado.cs`): a los S segundos, en el hilo de la pantalla y sin reflexion, el
  estado de XP (creditos, la cola, lo que suena, la letra, la pagina, el disco y la cancion elegidos, el buscador y lo escrito, el
  asistente de los botones, la atraccion, el mantenimiento, la pestana de Ctrl+F3, el foco y la ventana activa) y cada pieza de cada
  hoja abierta (clase, texto, sitio, si se ve, si esta activa, su orden del tabulador, Checked o Value, y la del foco).
- La referencia con los mismos ganchos solo en su Plataforma: `Pruebas/Ventanas/ganchos-referencia.patch` y `guiones/referencia.sh`.
- Las 41 pruebas de WinForms, los guiones de las escenas y la herramienta de Roslyn del inventario, en `Pruebas/` (ver
  `Pruebas/Ventanas/LEEME.md`).

Comprobado (8-11-2026, maquina de pruebas compartida con otras pistas, llvmpipe, JIT):

- Las 41 pruebas: 41 de 41 con la referencia congelada, con la referencia con ganchos y con este programa; la salida de las pruebas
  de la congelada y la de la de ganchos es la misma linea por linea.
- Los ganchos no cambian lo que se ve: en las escenas moneda (Ctrl+F3 en Sonido y en Imagen) y tab (45 Tab, 12 Mayus+Tab, la pestana
  Fondo y 25 Tab), las capas de Dibujo2D de las capturas (la hoja de Ctrl+F3 de 966x764 y las de la rocola) salen iguales pixel a
  pixel entre la congelada sin ganchos y la de ganchos (0 pixeles distintos en las 15 capas). La capa de la tarjeta y la pantalla
  entera no se pueden comparar asi: el cielo se mueve solo entre dos corridas.
- El ruido de la referencia contra si misma (3 corridas de las 10 escenas): la SECUENCIA (teclas, ProcessCmdKey, KeyDown, caracteres,
  KeyUp, raton, foco, ventanas, cierres) sale igual en las 3 en las 10 escenas, una vez sacada de ella la repeticion de una tecla
  mantenida (la manda otro hilo cada 33 ms: donde cae entre las demas teclas depende de la hora; se cuenta aparte). Las cuentas se
  mueven: relojes por segundo hasta 4-6 (moneda, tab, sigterm, kill9) y hasta 10-24 en las escenas que arrancan o cierran con
  carga (arranque, video, buscador, cierreq, altf4); encargos de otros hilos por segundo hasta 4-29 y en total hasta 10-75 (casi
  todos son el vigia de la pantalla, MedidasBitacora.cs:129, unas 30 por segundo). Con carga en la maquina las teclas de prueba
  llegan tarde (en dos corridas de cierreq las Q llegaron 600 ms tarde), por eso cuentan las secuencias y no los milisegundos.
  Los estados de XP (ROCOLA_ESTADO_EN) salen iguales en las tres.
- La referencia y el apagado: tras Ctrl+Alt+Q x3 y tras Alt+F4 falta `rocola_estado.txt` (UserClosing lo borra, como XP); tras
  SIGTERM y tras SIGKILL queda: la referencia no atiende SIGTERM (lo cambia V2, regla del 8-11-2026).

Herramientas: `Pruebas/Ventanas/guiones/` (datos.sh, referencia.sh, escenas.sh, corre.sh, ruido.sh, compara.sh, compara_trazas.py,
compara_escena.py, pruebas41.sh, teclas_tab.py) y `Pruebas/Inventario/` (Roslyn: 0 errores de compilacion, 4 578 referencias a
WinForms, como el inventario del diseno).
