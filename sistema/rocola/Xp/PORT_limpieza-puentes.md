# Port nativo: limpieza-puentes (3b, oleada 3 / tanda 6, 12-11-2026)

Seccion «3b. limpieza-puentes» de PLAN_TANDA4_A_N7.md. Rama `port-limpieza-puentes`, sobre e13d40c. Borra las costuras de Plataforma/ que ya
no llama nadie (las firmas de XP con un Graphics, una Font o un Brush de la capa que los archivos traducidos dejaron de pedir) y arregla las
notas EN LINUX que seguian nombrando puentes o firmas que ya no existen. No cambia nada de la logica de XP ni de lo que se dibuja.

## Que se borro (cada cosa, comprobada con el compilador y con grep, incluida la reflexion por nombre en Pruebas/)

- **Plataforma/Puente.pared.cs**: `Cube.Draw(Graphics, ...)` y las firmas de XP de `GlowAround`, `CtrlDot`, `CtrlTxt`, `CtrlTri`, `CtrlPad`,
  `CtrlDirLeft` y `CtrlDirRight` (todas con un Graphics). Todos los llamadores de Codigo/ ya llamaban a las traducidas con el lienzo y su
  estado. Se queda `ExtractCoverPalette(Bitmap)` (Form1.Fondo.cs:1147 y Form1.Portadas.cs:626 la llaman con un Bitmap de la capa; probado:
  sin ella, 2 errores de compilacion) y `PuenteDibujo.FormatoMapaPared`.
- **Plataforma/Puente.fondo.cs**: `RoundedPath(Rectangle, int)` (la firma que daba un GraphicsPath). Nadie la llama en Codigo/. Se quedan
  `ColoresDePortada(Bitmap, ...)` y las tres firmas del cielo de `Starfield` (con un Graphics).
- **Plataforma/Puente.ventanas2.cs**: `AnchoTexto(Graphics, string, Font)`. Se queda `AnchoTexto(string, Font)` (la llama Form1.Vistas.cs:1565
  con la Font de la lista; el plan decia que moria con Puente.pantalla-viva.cs, pero no: sin ella, error de compilacion), `AnchoToast(string,
  Font)` (Form1.cs:2268) y `RegionDeRuta` (Form1.cs:2270): las dos se quedan por lo mismo, comprobado quitandolas.
- **Plataforma/Puente.cs**: `PuenteDibujo.Pincel(Brush, bool)`. Se quedan `Pluma`, `Ruta`, `Matriz`, el resto de lo del banco (Puente.cs:39, 99, 104 y 105 del plan).
- **Plataforma/Puente.pantalla-viva.cs**: ENTERO (84 lineas): `PintaFoco(Bitmap, ..., FontFamily)`, `PintaFicha(Bitmap, Image, ..., FontFamily)`,
  `Redondo` (con su GraphicsPath), `AnchoTextoPantallaViva` (las dos), `PuenteDibujo.FamiliaPantallaViva`, `TrazoPantallaViva` y
  `GraphicsPantallaViva`. Ya sin llamadores en Codigo/ (PrecargaPaginaGpu.cs llama directo con el SKBitmap y el nombre de la familia;
  AtraccionGpu.cs y SuenaVivoGpu.cs llaman a RutaRedonda).

## Arneses de Pruebas/ que llamaban a lo borrado (por reflexion o con la firma de XP): adaptados

La regla: en la referencia congelada la firma de XP sigue existiendo (se llama como antes); en la copia de trabajo, donde ya no esta, el arnes
hace lo que hacia el puente (el lienzo y el estado del Graphics, los mismos puntos, el nombre de la familia), asi que la salida es la misma.
- VistasPantallaViva/PantallaViva.cs: `FirmaXp` (PintaFoco y PintaFicha) y `ConRuta` (Redondo: el GraphicsPath con el SKPath de RutaRedonda).
- VistasReposo/Reposo.cs y VistasSuena/Suena.cs: lo que hacia `Pincel` (un SKPaint y `Brush.Pon(p, !medio)` por reflexion).
- VistasPared/Pared.cs (`Cube.Draw` y las ayudas con Graphics: si no hay firma de XP, la traducida directa), VistasFondo/Fondo.cs
  (`ComoRoundedPath`), VistasAjustes/Vistas.cs (`AnchoConGraphics`: el modo de letra del Graphics y la Letra de la Font).

## Notas EN LINUX corregidas en Codigo/ (solo comentarios; mismo numero de lineas y de CRLF que en e13d40c)

GuiaRojoGpu.cs:140 y FranjaLigeraGpu.cs:98 (nombraban Puente.acabado-estilo.cs y Puente.reposo.cs, que ya no existen), AcabadoGpu.cs:122
(Form1.Video.cs ya esta traducido), EstiloGpu.cs:25 (RoundedPath -> ContornoRedondo), PulidoGpu.cs:23 y 142 (la firma con Brush de
Puente.asistente.cs ya no existe), PantallaVivaGpu.cs:30, 381, 574 y 710 y PaletaCaratulaGpu.cs:13 (ahora dicen quien la llama de verdad),
Form1.Ajustes.cs:290 y 292 (la firma de AnchoTexto con Graphics se quito). AcabadoGpu.cs:212, 260 y EstiloGpu.cs:93 ya decian «ya traducido».
FamiliaUi con FontFamily y RoundedPath para Form1.Ajustes.cs: no quedaba ninguna nota vieja.

## Para despues (no tocado por estar en archivos de otro grupo de la oleada)

- Form1.Fondo.cs:771 (nota de `ContornoRedondo`): dice que la firma de XP de RoundedPath «queda en el puente (Puente.fondo.cs)»; ya no queda.
- Form1.Video.cs y los demas de 3a: las notas que nombren lo borrado, las arregla 3a o N7.
- Puente.fondo.cs, Puente.pared.cs y Puente.ventanas2.cs conservan lo vivo (arriba) y se acaban en N7.

## Comprobaciones (12-11-2026, con numeros)

- **Compila** desde cero (`dotnet build -c Release --no-incremental`): 0 errores y los 15 avisos de la base, ninguno nuevo. **NativeAOT**
  (`dotnet publish ... -p:PublishAot=true`): 0 avisos IL/AOT.
- **diff-xp.py** sobre los 8 archivos de Codigo/ tocados: solo lineas de traduccion, todas con marca, salvo 9 lineas sin marca de
  Form1.Ajustes.cs que ya estaban asi antes (llaves y dos comentarios de la parte de MMDevices; yo solo cambie los comentarios de las lineas 290
  y 292). Numero de lineas y de CRLF de los 8 archivos igual que en e13d40c (`git diff --numstat`: 1 a 4 lineas por archivo, 13 en total).
- **Arneses**, la salida de la copia de trabajo contra la de la referencia congelada, linea por linea y con las dos letras (XP falsas y DejaVu):
  VistasPantallaViva 768 lineas, 0 distintas; VistasReposo 1 791, 0; VistasSuena 488, 0; VistasPared 1 715, 0; VistasAjustes 0 distintas de 545;
  VistasAcabadoEstilo 0 distintas de 6 603 frente a la referencia y de 6 607 frente a la base; VistasFondo 2 763 lineas, 1 distinta: la primera
  (`inicio traducida=False/True`, la que dice cual es, como en PORT_fondo.md).
- **Banco** (Pruebas/BancoDibujo, semillas 7 y 11 con las letras de XP falsas y 5 sin letras; ejecutores gdi, nativo y puente; 3 000 casos
  cada uno): 9 de 9 iguales al congelado, 0 casos distintos (27 000 casos).
- **Cintas de la tarjeta** (`Pruebas/VistasFrontera/cintas.sh`): 29 de 29 iguales (6 616 cuadros).
- **No corridos** (no toco ventanas, pantalla, teclas, raton, cierre ni lo que se sube a la tarjeta): recorridos, pruebas41, pruebas_hoja,
  pruebas_pantalla y escenas; los corre el coordinador al juntar. El inventario con [Obsolete]: la prueba mas fuerte es la del compilador (lo
  borrado ya no existe y compila con 0 errores, y ningun arnes lo llama por nombre: grep de Pruebas/ hecho).

## Costuras: lo que queda

Quitadas: las listadas arriba. Anadidas: ninguna. Quedan (vivas, con su razon arriba): `ExtractCoverPalette(Bitmap)`, `FormatoMapaPared`,
`ColoresDePortada(Bitmap)`, las tres firmas de `Starfield` con Graphics, `AnchoTexto(string, Font)`, `AnchoToast(string, Font)`,
`RegionDeRuta`, `AyudaView.RenderContent(Graphics, bool)` y todo Puente.cs menos `Pincel`.

## Riesgos

- Ninguno de logica: solo se borra lo que no llama nadie y se cambian comentarios. Los arneses de Pruebas/ que llamaban a las firmas borradas
  hacen ahora por su cuenta lo que hacia el puente; si alguien los corre contra una copia vieja, en la referencia congelada siguen llamando a
  la de XP.
- Los arneses VistasBuscador, VistasVideoForm1 y VistasAsistente no se corrieron (no usan nada de lo borrado por nombre: o llaman a la
  traducida si existe, o usan firmas que siguen vivas).

## Revision (12-11-2026, revisor independiente)

Leido el diff entero (e13d40c..4428337) contra el plan, Rocola/ y los arneses. Lo borrado de Plataforma/ esta bien: cada firma quitada era
solo de paso (sin logica de XP) y ya no la llama nadie (compila sin ella; grep por nombre en Codigo, Plataforma, Nativo y Pruebas, incluida la
reflexion). Los arneses adaptados hacen exactamente lo que hacia el puente borrado (PintaFoco/PintaFicha: los mismos puntos -Image.Sk, que es
PuenteDibujo.Mapa- y la familia por su nombre, la nula como FontFamily.GenericSansSerif de la capa, que es Letras.SinRemates como en
FamiliaPantallaViva; Pincel: un SKPaint nuevo y Brush.Pon(p, !medio); Redondo y RoundedPath: un GraphicsPath dueno de ESE SKPath). Lo que
faltaba y se arreglo (commit aparte, «revision»):

- **Notas EN LINUX viejas** que el grupo no vio: AsistenteRocola.cs:2 y AsistenteGuiaRocola.cs:3 decian que CtrlDot, CtrlTxt y CtrlTri
  «pasan por el puente (Plataforma/Puente.cs y Puente.asistente.cs)»; esas firmas (CtrlDotAsistente... y las de XP con un Graphics de
  Puente.pared.cs) ya no existen y los dos archivos las llaman directo (no usan nada de PuenteDibujo). Corregidas (solo el comentario; mismo
  numero de lineas, sin CRLF, como en e13d40c). La misma nota vieja esta en AyudaCasillaGpu.cs:3, que es de 3a (armado-vistas): no se toco.
- **Controles negativos que apuntaban a lo borrado**: Pruebas/VistasPared/control.sh («el puente de Cube.Draw sin el filtro del Graphics»,
  sobre Plataforma/Puente.pared.cs: el sed ya no cambiaba nada) y Pruebas/VistasPantallaViva/control.sh (los dos sobre
  Plataforma/Puente.pantalla-viva.cs, que ya no existe: el de AnchoTexto y el de PastillaSonandoEq, este ya muerto desde vistas-b). Retirados
  con una nota que dice por que (bash -n correcto en los dos).

Comprobado de nuevo por el revisor: `dotnet build -c Release --no-incremental` 0 errores y 15 avisos; diff-xp.py -v de los 10 archivos de
Codigo/ tocados: 0 sin marca en todos salvo 1 en AsistenteGuiaRocola.cs (una llave, ya estaba en e13d40c) y los 9 de Form1.Ajustes.cs (ya
estaban); lineas y CRLF de los 10 archivos iguales a e13d40c. Arneses contra la referencia congelada (letras falsas y DejaVu):
VistasPantallaViva 768 lineas IGUALES (291 directas), VistasReposo 1 791 IGUALES, VistasSuena 488 IGUALES, VistasPared 1 715 IGUALES.
VistasFondo 2 763 lineas, 1 distinta (la primera, `inicio traducida=False/True`, la esperada). VistasAjustes 0 distintas de 545 (falsas y
DejaVu). Banco (semillas 7 y 11 con letras falsas, 5 con SIN_LETRAS; ejecutores gdi, nativo y puente): 9 de 9 IGUALES (3 000 casos cada uno).
No repetidos por el revisor: NativeAOT (la revision solo cambia comentarios y guiones de prueba), las cintas (nada de lo que se sube a la
tarjeta cambio: Pincel no lo usa la subida) y recorridos/escenas (nada de ventanas, pantalla ni teclas).

Queda para despues (archivos de 3a o de 4a): AyudaCasillaGpu.cs:3 (la misma nota vieja de CtrlDot/CtrlTxt/CtrlTri por el puente) y
Form1.Fondo.cs:771 (RoundedPath «queda en el puente»); el plan del inventario decia que AnchoTexto(string, Font) moria con
Puente.pantalla-viva.cs, pero la sigue llamando Form1.Vistas.cs:1565 (confirmado): se queda hasta 5b o N7.
