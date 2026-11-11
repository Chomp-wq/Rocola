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

## 9-11-2026 (06:20-07:00): el grupo atraccion y el paso de la hoja de ventanas2

- `161f647`: el grupo atraccion (AtraccionGpu.cs con SkiaSharp directo, sus mapas a la tarjeta como SKBitmap sin puente) con su revisión («bien»).
- `7158ccb`: el paso H de ventanas2 hasta su revisión (`28e453f`): la hoja propia y sus piezas (Nativo/Hoja*.cs, Pieza, Rotulo, Grupo,
  BotonXp, ListaXp, AvisoXp, SelectorCarpetas, PinturaXp); Ctrl+F2, los avisos y «Agregar carpeta...» las llaman directo.

Sin choques; los 109 archivos de `Codigo/` con su número de líneas y de CRLF. Comprobado con todo junto (`scratchpad/merge/verifica-junta6.sh`):

- **Compila** desde cero: 0 errores, los 15 avisos de siempre. **NativeAOT**: 0 avisos IL/AOT.
- **Arneses**: atraccion 19 120 líneas (3 610 mapas, 68 432 llamadas directas) IGUALES con las dos letras; reposo, suena y
  acabado-estilo (0 distintas de 6 603) siguen iguales. La lista de la noche (`VistasAtraccion/top.sh`) contra las dos corridas de
  referencia de la pista: texturas quietas 30 de 30, capas 16 de 16.
- **Las 41 pruebas** 41 de 41; **la hoja** 52 de 52 y **las reglas de la pantalla** 32 de 32, en JIT y en NativeAOT.
- **Escenas alternadas** (RJ6/J6, las 19, con el ruido de `ventanas2/ruido`): arranque, moneda, video, tab, buscador, cierreq, altf4,
  kill9, asistente, captura, f2, f2barras, f2sin y aviso IGUALES (las capas de las hojas a 0 puntos); sigterm y f3sigterm lo aceptado;
  salirq solo una cuenta (305 frente a 318 encargos, holgura 12; secuencia y estados iguales). f2sigterm y f2agrega no se comparan con
  la referencia (allí Ctrl+F2 no se apaga con SIGTERM y «Agregar carpeta...» contesta Cancelar sin enseñar nada: diferencias aceptadas del
  paso H, que las comparó con el programa del paso T). NativeAOT: arranque, cierreq y kill9 IGUALES; f2, f2barras y aviso con la misma
  secuencia, estados y capas y menos encargos del repaso en el primer segundo (el nativo arranca antes).
- **Recorridos** (los nueve): 9 de 9 salen con 0 y sin excepciones; texturas quietas 479 de 487; capas quietas 215 de 217. Las dos que
  faltan: la hoja de Ctrl+F3 de hojas a los 18 s (el repaso, como arriba) y la de Ctrl+F2 de ctrlf3 a los 40 s, que solo cambió de
  nombre: ahora es `capa4-hoja-189,182-646x404` en vez de `capa4-dibujo2d-...` y sus puntos son idénticos a los de la referencia
  (0 distintos; corrido con los PNG de las dos).

## 11-11-2026 (04:10-05:20): pantalla-viva, buscador-portadas y asistente (la tanda 2 entera)

- `57d72f5`: pantalla-viva (PantallaVivaGpu y DiscoVivoGpu) y buscador-portadas (BuscadorPortadasGpu), cada uno con su revisión («bien»;
  los revisores solo añadieron controles negativos a los arneses).
- `ae8b50b`: asistente (AsistenteRocola, AsistenteGuiaRocola, AsistenteEstrellaGpu, AyudaCasillaGpu y DegradadoV de PulidoGpu) con su
  revisión («bien»; dos pruebas más en su arnés).

Sin choques; los 109 archivos de `Codigo/` con su número de líneas y de CRLF. Comprobado con todo junto (`scratchpad/merge/verifica-junta7.sh`):
compila desde cero (0 errores, 15 avisos) y NativeAOT sin avisos IL/AOT; los siete arneses (asistente, pantalla-viva, buscador-portadas,
atraccion, reposo, suena y acabado-estilo) IGUALES a la referencia con las dos letras; el banco igual en los 27 000 casos; 41 de 41
pruebas y 52 de 52 de la hoja; escenas alternadas (RJ7/J7) todas IGUALES salvo sigterm y f3sigterm (lo aceptado), y en NativeAOT
arranque, cierreq, kill9 y buscador IGUALES; recorridos 9 de 9 con 0, capas quietas 215 de 217 (las dos de siempre: el repaso en la
hoja de Ctrl+F3 y la hoja de Ctrl+F2 con otro nombre de capa y los mismos puntos) y texturas quietas 445 de 487 (el buscador, 45 de
78, depende de la hora como se midió el 8-11).

Para quitar después: `DegradadoV(r, c0, c1, false)` ya puede llamarse directo desde EstadoReposoGpu.cs:353 y AcabadoGpu.cs:347 (se borra
su firma de XP de Puente.asistente.cs); las 4 llamadas a `Redondo` de AtraccionGpu.cs y las de SuenaVivoGpu.cs pasan a `RutaRedonda`
(SKPath) y se borra `Redondo` de Puente.pantalla-viva.cs. Al juntar el paso de Ctrl+F3: choque en la línea 62 de PantallaVivaGpu.cs
(PaginaPantalla), manda la de ventanas2.

## 11-11-2026 (14:00-16:00): vistas-a y vistas-b (la primera parte de la tanda 3)

- `c86a213`: vistas-a (la barra de páginas y la barra de letras de Form1.Vistas.cs con SkiaSharp directo) con su revisión.
- `d320c7a`: vistas-b (de HeaderView al final de Form1.Vistas.cs) con su revisión («bien»; diez controles negativos más en su arnés).
  Un choque en la línea 10 de Form1.Vistas.cs (los `using`): queda el código de las dos (`using System.Drawing; using Rocola.Dibujo2D;
  using Rocola.Nativo; using SkiaSharp;`) con las dos notas juntas; el archivo sigue con 2 749 líneas y 2 568 CRLF.

Comprobado con todo junto (`scratchpad/merge/verifica-junta8.sh`): compila desde cero (0 errores, los 15 avisos) y NativeAOT sin
avisos IL/AOT; los arneses vistas-a, asistente, pantalla-viva, buscador-portadas, atraccion, reposo y suena IGUALES con las dos letras,
acabado-estilo 0 distintas de 6 603 y vistas-b 0 distintas de 2 865; el banco igual en los 27 000 casos (gdi, nativo y puente); 41 de
41 pruebas y 52 de 52 de la hoja; escenas alternadas (RJ8/J8) todas IGUALES salvo sigterm y f3sigterm (lo aceptado), y en NativeAOT
arranque, cierreq, kill9 y buscador IGUALES; recorridos 9 de 9 con 0, capas quietas 216 de 217 (la de Ctrl+F2 con su nombre nuevo de
capa y los mismos puntos) y texturas quietas 438 de 487 (el buscador, 45 de 78, depende de la hora).

## 11-11-2026 (20:15-21:35): ventanas2 entera (las teclas, la hoja propia y Ctrl+F3)

- `67e9a12`: la rama port-ventanas2 con sus tres pasos y sus revisiones: T (las teclas sin Keys ni Message, la flecha, el foco y el
  contexto del hilo), H (la hoja propia con Ctrl+F2, los avisos y el selector de carpetas) y C (Ctrl+F3 entero con las piezas nativas).
  La revisión de C no encontró fallos en el programa (solo en el banco de pruebas: la escena f3histsigterm, compara_medir.py y el
  ruido de f3raton y f3billar). La rama ya traía juntada la rama principal hasta `d320c7a`: sin choques. Tres commits del paso C
  (4897bca, 7cab6e6, 343ce4b) llevan otra línea Co-Authored-By; no se reescribe la historia.

Comprobado con todo junto (`scratchpad/merge/verifica-junta9.sh` y `verifica-junta9b.sh`): compila desde cero (0 errores, los 15
avisos) y NativeAOT sin avisos IL/AOT; los nueve arneses de antes IGUALES (acabado-estilo 0 de 6 603, vistas-b 0 de 2 865) y el de
Ctrl+F3 (VistasAjustes) 0 distintas de 545 con las dos letras; el banco igual en los 27 000 casos; 41 de 41 pruebas, 62 de 62 de la
hoja y 32 de 32 de la pantalla; recorridos 9 de 9 con 0, capas quietas 216 de 217 (la hoja de Ctrl+F3 de hojas a los 18 s, el
repaso; la de Ctrl+F2 de ctrlf3 ya sale igual) y texturas quietas 480 de 487.

Escenas alternadas con la referencia (RJ9/J9, 21 escenas): arranque, moneda, video, tab, buscador, cierreq, altf4, kill9, asistente,
f2, f2barras, f3vistas, f3grises, f3teclas, f3raton y f3billar IGUALES; sigterm y f3sigterm lo aceptado. captura, salirq, f2sin y
aviso salieron DIFERENTE solo por cuentas del reloj Form1.cs:724 y del encargo MedidasBitacora.cs:129 (carga de 5 a 6 con tres agentes)
y salirq además por el momento en que se leyó estado-mantenida.txt; repetidas (RJ9b/J9b): captura, f2sin y aviso IGUALES, y salirq
con la misma secuencia y solo una cuenta (280 frente a 301 encargos, holgura 12), como en la junta del 9-11. NativeAOT: arranque,
cierreq, kill9 y buscador IGUALES; f3vistas y f3teclas dieron 587 puntos en la hoja de Ctrl+F3 por la fecha de compilación del
título: con `touch -r /usr/lib/dotnet/dotnet` en rocola-xp (como hizo la revisión de C) salen IGUALES.

## 11-11-2026 (21:45-22:56): pared, fondo y portadas (tanda 3)

- `013996c`: pared (Form1.Pared.cs, CaratulasSeparadasGpu, PrecargaPaginaGpu, PaletaCaratulaGpu y PreviewsIndexadosGpu con SkiaSharp
  directo; Nativo/FormatoMapa.cs) con su revisión («bien»; faltaba en la tabla la costura de FamiliaUi).
- `9ccdf37`: fondo (Form1.Fondo.cs: el cielo por procesador, la luna, RoundedPath y el color de las portadas) con su revisión
  («bien»; PORT_reposo.md en la lista de la costura quitada de RoundedPath).
- `87f19b6`: portadas (DibujoLigeroGpu, RespuestaGpu, Form1.Portadas.cs, Form1.Biblioteca.cs y CaratulasAdaptativas) con su revisión
  («bien»; el arnés pasa ahora la rama no nativa de las teselas y de InteriorOpaco).

Las tres ramas salían de `ae8b50b` (antes de vistas-a/b y ventanas2): sin choques, compila y los 109 archivos de `Codigo/` con su
número de líneas y de CRLF. Comprobado con todo junto (`scratchpad/merge/verifica-junta10.sh`): compila desde cero (0 errores, los 15
avisos) y NativeAOT sin avisos IL/AOT; los arneses vistas-a, asistente, pantalla-viva, buscador-portadas, atraccion, reposo, suena,
pared y portadas IGUALES, acabado-estilo 0 de 6 603, vistas-b 0 de 2 865, Ctrl+F3 0 de 545; el del fondo, 2 763 líneas con solo la
primera distinta (`inicio traducida=True/False`, la marca de qué programa corre, como dice PORT_fondo.md); el banco igual en los
27 000 casos; 41 de 41, 62 de 62 de la hoja y 32 de 32 de la pantalla; recorridos 9 de 9 con 0, capas quietas 217 de 217 y texturas
quietas 476 de 487 (las que faltan dependen de la hora: buscador, ayuda y video); las 21 escenas alternadas (RJ10/J10) IGUALES salvo
sigterm y f3sigterm (lo aceptado); NativeAOT arranque, cierreq, kill9, buscador, f3vistas y f3teclas IGUALES (con la fecha de dotnet en
rocola-xp).
