# Port nativo: lo que se junta en la rama principal (el coordinador)

Cada pista y cada grupo trabaja en su rama (`port-*`) con su revisor independiente; aquí se apunta cada vez que se junta en la rama
principal (`claude/modest-carson-fa2v2n`), qué se comprobó con todo junto y las diferencias que quedan con su causa. Lo de antes del
8-11-2026 por la tarde está en `PORT_WINDOWS.md` («Al juntar con la rama principal»). Nada de esto se ha probado en el A4: todo es fuera de
pantalla (llvmpipe) en la máquina de pruebas, compartida con los agentes (carga de 3 a 9 en 4 núcleos).

## 8-11-2026 (20:10-21:00): la revisión de frontera, el grupo suena y el paso de las teclas

- `c290039`: los dos commits del revisor de frontera (el grupo ya había entrado en `564788b` antes de su revisión).
- `9f0b640`: el grupo suena (`SuenaVivoGpu.cs` con SkiaSharp directo) con su revisión.
- `b831169`: el paso de las teclas de ventanas2 (Tecla, PulsoTecla, PulsoCaracter, la flecha, el foco y el contexto del hilo, nativos)
  hasta su revisión (`089d18b`; lo que la pista siga haciendo en su rama entra después).
- `d94eaed`: el banco por función (`Pruebas/BancoDibujo/Nuevo`) no compilaba desde que se juntó la pista windows (incluía todo
  `Nativo/`, que ya tiene el sonido, el video y la pantalla y pide el programa entero): ahora compila la lista escrita del dibujo y la tarjeta.

Sin choques. Los 109 archivos de `Codigo/` conservan su número de líneas y de CRLF (comparado con `564788b`).

Comprobado con todo junto (registro: `scratchpad/merge/verifica-junta3.sh`):

- **Compila** (`dotnet build -c Release`): 0 errores, los 15 avisos de siempre. **NativeAOT**: 0 avisos IL/AOT.
- **Cintas de la tarjeta** (`Pruebas/VistasFrontera/cintas.sh`): 29 de 29 iguales (6 616 cuadros).
- **Arnés de la frontera** (`compara.sh`, referencia compilada con `-p:Fuente=` la copia congelada): por SKBitmap y por el puente, con las
  letras de XP falsas y con DejaVu: 106 líneas y 153 texturas, 0 distintas en las cuatro.
- **Arnés de suena** (`arnes.sh`): IGUAL a la referencia en 487 líneas con las dos letras (72 llamadas directas a lo traducido).
- **Banco por función**: semillas 7 y 11 (letras falsas) y 5 (DejaVu), 3 000 casos, ejecutores gdi, nativo y puente contra el
  congelado: iguales los 9; `puente-objetos`: 407 bien, 0 mal.
- **Las 41 pruebas** de las ventanas: 41 de 41; **reglas de la pantalla**: 32 de 32 en JIT y en NativeAOT.
- **Escenas alternadas con la referencia** (RJ3/J3): arranque, moneda, video, buscador, cierreq, altf4 y kill9 IGUALES; asistente,
  captura y salirq IGUALES con el ruido de 6 corridas de la referencia (`/root/rocola-trabajo/port/ventanas2/rv/ruido`); sigterm y
  f3sigterm, lo aceptado (el estado se borra, como XP al apagar); tab: ver abajo. NativeAOT (JA3): arranque y cierreq IGUALES, sigterm lo
  aceptado, kill9 y asistente solo con más relojes en el primer segundo (el programa nativo arranca antes, como en V3).
- **Recorridos** (los nueve, con el cerrojo, contra `cmp-ref` y `cmp-ref2`): capas de Dibujo2D quietas 216 de 217 (la que falta: ver
  abajo); texturas quietas 449 de 487, casi todas del buscador (ver abajo).

Las diferencias que quedan, con su causa:

- **El repaso de la biblioteca va más despacio con la máquina cargada** (escena tab: «faltan 4 archivos» frente a «ya está al día»;
  recorrido hojas, captura de los 18 s: «faltan 5.» frente a «faltan 4.», 51 puntos; las dos corridas de la traducción dan lo mismo y las
  dos de la referencia también). La traducción pone de verdad las prioridades de hilo de XP (`Nativo/PrioridadHilo.cs`, pista windows: el
  nivel más bajo es el nice del programa + 10), y en la referencia `Thread.Priority` no hacía nada en Linux: con los agentes compilando a
  nice 0, el repaso (sus carriles van en XP con la prioridad mínima, `Lowest`: Form1.Biblioteca.cs:1782) avanza menos que en la referencia. No es del dibujo ni de la lógica de XP; en la rocola, sin
  nada más corriendo, no se nota. Antes de juntar la pista windows (`b609319`) la captura de los 18 s salía idéntica.
- **Las texturas del buscador dependen de la hora** (resultados y portadas que llegan de otros hilos): con la misma carga, dos corridas de
  la referencia entre sí pierden 5 de 46 texturas quietas y sacan 37 nuevas; las dos de la traducción (41 y 39 de 42) caen dentro de ese
  ruido. Alternadas en `/root/rocola-trabajo/port/junta/pares`.

Riesgos apuntados por los revisores (para lo que falta traducir y para N7):

- Un `SKBitmap` ya soltado no lanza una excepción como un `Bitmap` de GDI+: leer su `Width` o sus puntos es una violación de segmento que
  cierra la rocola (revisión de reposo, `PORT_reposo.md`). En los caminos de XP no se llega; al traducir D3D9Star.cs, conservar el orden
  del cuadro (los horneados antes de `PreparaCintasTexto`).
- Lo que se pase a `Upload`, `CreateOverlay` y las demás subidas tiene que ser SKBitmap Bgra8888 premultiplicado (`Mapas.Nuevo`,
  `Imagenes.Lee`, `Mapas.Copia`): `Pixeles.CopiaSinPremultiplicar` lee ese formato (revisión de frontera).
- `HorneaDegradadoV` (PulidoGpu.cs) suelta el mapa después de crear el `TextureBrush`: al traducirlo, hacer el shader antes de soltarlo
  (solo afecta a degradados de más de 2 048 px; revisión de reposo).

## 8-11-2026 (21:00-22:20): los grupos reposo y acabado-estilo (la tanda 2a entera)

- `f2f9dcb`: el grupo reposo (EstadoReposoGpu, FranjaLigeraGpu y TextoMovilGpu con SkiaSharp directo) con su revisión («bien»).
- `f997e61`: el grupo acabado-estilo (AcabadoGpu, EstiloGpu, GuiaRojoGpu y BuscadorLigeroGpu) con su revisión (solo la tabla de su
  documento).

Sin choques; los 109 archivos de `Codigo/` con su número de líneas y de CRLF. Comprobado con todo junto (`scratchpad/merge/verifica-junta4.sh`
y `verifica-junta5.sh`):

- **Compila** desde cero (`--no-incremental`): 0 errores, los 15 avisos de siempre. **NativeAOT**: 0 avisos IL/AOT; escenas arranque,
  cierreq y kill9 IGUALES.
- **Arneses**: acabado-estilo 0 líneas distintas de 6 603 frente a la referencia y de 6 607 frente a la base, con las dos letras; reposo
  (1 791 líneas) y suena (487) IGUALES con las dos letras, ya juntos.
- **Recorridos** (los nueve): con reposo, capas quietas 217 de 217; con acabado-estilo, 212 de 217: las 5 son la hoja de Ctrl+F3 del
  recorrido hojas (ver abajo). Recorrido del disco sin el vidrio en piezas (`VistasAcabadoEstilo/disco.sh`), alternado con la referencia:
  capas 60 de 60, ninguna textura fuera de la referencia. ayuda y pared2 alternados con la referencia (dos veces): la rama queda en el ruido
  de la referencia contra sí misma (146 y 152 de 157 texturas quietas frente a 146 de 149).
- **Escenas alternadas** (RJ5/J5): todas IGUALES (también tab) salvo sigterm y f3sigterm, lo aceptado.

La hoja de Ctrl+F3 del recorrido hojas (capa 4, captura a captura contra las de la referencia guardadas en `junta/pares`): con la máquina
más cargada difieren las capturas de 8 a 12 s y de 16,5 a 21 s; todo lo que cambia es lo que depende de cuánto lleva medido el repaso de
la biblioteca: «34%» frente a «0%», «(van 14)» frente a «aún no hay bastantes medidas», «12 aceptables 1 pobres» frente a «6 aceptables
0 pobres», «ya está al día» frente a «faltan 9 archivos». Es la misma causa de arriba (las prioridades de hilo de XP puestas de verdad).

Para quitar después (lo apuntaron los revisores): el renglón del artista de la franja pasa por tres puentes de reposo y acabado-estilo
(FranjaLejos → PintaArtistaCompacto → PintaRenglonLejos → TextoConCinta: sale lo mismo, pero ya puede llamarse directo), y entre suena y
reposo PintaTituloGrandeReposo, PintaMinutosColaReposo, PintaTextoMovilSuena y FuenteTituloReposo pasan a la llamada directa.
