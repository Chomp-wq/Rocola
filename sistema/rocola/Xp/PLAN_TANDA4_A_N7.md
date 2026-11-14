# Plan de la tanda 4 hasta N7 (12-11-2026)

Sale del inventario de solo lectura sobre f5a33ca (la tanda 3 entera juntada): dos inventarios (el dibujo, con [Obsolete] en cada miembro de la capa: 1 151 usos reales; las ventanas) y un plan que comprobo sus afirmaciones dudosas. Cada oleada son grupos que trabajan en paralelo en ramas aparte sin compartir archivos de Codigo/; cada grupo con revision independiente y la verificacion completa al juntar (PORT_JUNTAS.md). El coordinador puede cambiar el reparto si al juntar aparece algo.

## 2. Reglas para trabajar en paralelo

- **Codigo/:** en una oleada, ningún archivo de Codigo/ está en dos grupos. Las notas viejas de un archivo las arregla el grupo que es dueño de ese archivo en esa oleada.
- **Puente\*.cs:** un grupo solo borra líneas de un Puente\*.cs que nadie más toca en su oleada. Si una costura queda muerta y su archivo es compartido, se queda: compila igual y la borra la limpieza siguiente o N7.
- **Dibujo y ventanas:**
  - P0 de VENTANAS queda cubierto por las oleadas 1 y 2.
  - «onpaint-dibujo-por-procesador» se reparte entre W3a-W3f.
  - «letras-de-los-controles» va dentro de W6.
- **Comprobaciones de siempre**, que no repito en cada grupo:
  - compilar en JIT y en NativeAOT;
  - diff-xp.py (Pruebas/Tarjeta/guiones/diff-xp.py): fuera de la traducción, solo cambian comentarios;
  - Pruebas/Inventario: las cuentas de gdi y winforms solo bajan;
  - recorridos, las 29 cintas y las escenas, contra /root/rocola-trabajo/referencia-xp.

- **Modelo de cada grupo (decision del dueno del 12-11-2026):** los grupos chicos y mecanicos los hace un modelo mas ligero (3b, 4b, 5c, 7a y 8b); los grandes o delicados (4a, 6a, 7b, 8a y N7) y TODAS las revisiones, el modelo de la sesion. Los medianos, el de la sesion.

## 3. Oleadas

### OLEADA 1 (tanda 4)

**1a. directas-buscador-video** (medio)
- Toca: Codigo/Form1.Buscador.cs, Form1.Video.cs, Form1.cs (solo 2745-2755), Form1.Portadas.cs (solo 1187-1194), PrecargaPaginaGpu.cs (solo 38-49); Plataforma/Puente.buscador.cs y Puente.video-form1.cs; Pruebas/VistasBuscador/Buscador.cs:462 y VistasVideoForm1/VideoForm1.cs:255 y 306.
- Form1.Buscador.cs:
  - las 20 Ctrl\*/GlowAroundBuscador pasan a las formas de Form1.Pared.cs con SKCanvas;
  - CubeDrawBuscador:1519, con cover = PuenteDibujo.Mapa(ca) o BlendInto sin PuenteDibujo.Bitmap (1517);
  - DrawReactiveBuscador:965;
  - 10 RoundedPath y 20 Ruta pasan a ContornoRedondo (Form1.Fondo.cs:771).
- Form1.Video.cs: 1292, 1498 y 1786-1793 llaman directo a RenderContent(SKCanvas...).
- Form1.cs:2745-2755 llama directo (HelpBar).
- EnSeco: Form1.Portadas.cs:1187, 1188 y 1194, y PrecargaPaginaGpu.cs:38-39 y 49, pasan a la EnSeco traducida (Form1.Video.cs:1854).
- Se queda Form1.Portadas.cs:1199 (attractView), con Puente.video-form1.cs:29 y :74.
- Se borran:
  - Puente.buscador.cs:33, :59 y :72-120;
  - Puente.video-form1.cs:55-64.
- Se arreglan unas 28 notas en Form1.Buscador.cs y Form1.Video.cs:1292, 1498, 1787, 1788 y 1793.
- Comprobar: arneses buscador, video-form1, vistas-a y vistas-b; el banco; recorridos, cintas y escenas.

**1b. directas-vistas** (medio)
- Toca: Codigo/Form1.Vistas.cs y DiscoVivoGpu.cs; Plataforma/Puente.vistas-a.cs, Puente.vistas-b.cs, Puente.ventanas2.cs (solo :31) y Puente.pared.cs (solo :55-59).
- Form1.Vistas.cs:
  - Ctrl\*VistasA en 162, 163, 171 y 172;
  - 13 AnchoTextoVistasB pasan a AnchoTexto(AjusteLetra, string, Letra) y AnchoTexto(string, Letra);
  - 4 Formato(CreaSfDibuja(StringAlignment)) pasan a CreaSfDibuja(Alinea);
  - SueltaMapasVistasB (1913) pasa a SueltaBitmaps(ref SKBitmap[]);
  - 10 RoundedPath, 17 Ruta y el Dispose de 2090 pasan a ContornoRedondo;
  - se quita `using Rocola.Dibujo2D` de :10.
- DiscoVivoGpu.cs: los 9 AnchoTextoPantallaViva pasan a AnchoTexto. En 313, 337 y 363 va con PuenteDibujo.Letra(Font), hasta W6.
- Se borran:
  - Puente.vistas-a.cs:42 (muerta) y :56-73;
  - Puente.vistas-b.cs:28, :35, :42 y :46;
  - Puente.ventanas2.cs:31;
  - Puente.pared.cs:55-59 (CB muerto y SueltaBitmaps con Bitmap).
- NO toca Puente.pantalla-viva.cs: es compartido con 2a y lo limpia la oleada 3.
- Se arreglan unas 22 notas de Form1.Vistas.cs y DiscoVivoGpu.cs:235-405.
- Comprobar: arneses vistas-a y vistas-b; VistasAjustes (Vistas.cs:69 y :93 buscan por reflexión las de XP); pantalla-viva; el banco; recorridos y cintas.

**1c. pantalla-nativa (W2)** (grande)
- Toca solo Plataforma/Anfitrion.cs, TeclasLinux.cs y VideoEnVentanas.cs, y Nativo/PantallaRocola\*.cs, PantallaTarjeta.cs y Hoja\*.cs, más archivos nuevos en Nativo. Codigo/ no cambia.
- IVista: Bounds, Visible, Left, Top, Width, Height, SetBounds, Invalidate, Pinta(SKCanvas...) y Raton.
- El compositor nativo:
  - mezcla capas de los dos lados (Control de la capa e IVista);
  - respeta el orden fino de WinForms;
  - compone: fondo por procesador, vistas, textura de la tarjeta, video, rótulos, hojas y flecha.
- Las teclas van a ProcessCmdKey, Form_KeyPress y OnKeyUp, o a la hoja activa.
- IPantallaDelVideo nativo, al lado de VideoEnVentanas.cs (que todavía no se borra).
- TeclasLinux.cs:72-262 entrega a la pantalla, no a Escritorio.
- Comprobar: pruebas41 (41), hoja (62), pantalla (32), las 21 escenas alternadas y los recorridos.
- Prueba NUEVA en Pruebas/Ventanas/Pantalla: una IVista falsa junto a un Control de la capa. Tiene que dar el orden de composición y el reparto del ratón que da WinForms.

### OLEADA 2 (tanda 5)

**2a. directas-tanda2** (medio)
- Toca: Codigo/SuenaVivoGpu.cs, EstadoReposoGpu.cs, EstiloGpu.cs, TextoMovilGpu.cs, AtraccionGpu.cs y AcabadoGpu.cs. Plataforma: borra enteros Puente.suena.cs, Puente.reposo.cs y Puente.acabado-estilo.cs, y Puente.asistente.cs:29-60. Pruebas/VistasSuena/Suena.cs:222-260, VistasReposo/Reposo.cs:175 y 461-487, VistasAsistente/Asistente.cs:387 y 462-463, y VistasAcabadoEstilo.
- Redondo pasa a RutaRedonda (PantallaVivaGpu.cs:710), en 9 sitios: AtraccionGpu.cs:789, 803, 1011 y 1039, y SuenaVivoGpu.cs:230, 362, 368, 374 y 600.
- DegradadoV pasa a la de 4 argumentos con el SKShader en un SKPaint: AcabadoGpu.cs:347-349 y EstadoReposoGpu.cs:353-354.
- PintaTituloGrande y PintaMinutosCola llevan SKCanvas (EstadoReposoGpu.cs:385 y 431).
- AnchoTexto, PintaTextoMovil y LetraTituloReposo en SuenaVivoGpu.cs:210, 448, 551, 593 y 632.
- EstiloGpu.cs:98-100 y 195, y TextoMovilGpu.cs:175, llaman directo.
- Upload(PuenteDibujo.Bitmap(x)) pasa a Upload(x): SuenaVivoGpu.cs:704, 707, 715 y 718, y TextoMovilGpu.cs:301.
- NO toca la moneda (712 y 724) ni PrecargaPaginaGpu.cs: van a 5a.
- Los arneses pasan a llamar las firmas de XP por reflexión, como ya hacen con Nativa y Traducida, porque las firmas muertas de suena y reposo desaparecen.
- Cuidado:
  - con el SKPaint directo, el medio píxel tiene que dar los mismos puntos que el viaje SolidBrush a SKPaint;
  - hay que decidir quién suelta la Letra de LetraTituloReposo (SuenaVivoGpu.cs:551).
- Comprobar: arneses suena, reposo, acabado-estilo (0 de 6 603), atraccion, asistente y pantalla-viva; cintas y recorridos.

**2b. ajustes-roundrect-familiaui** (chico)
- Toca: Codigo/Form1.Ajustes.cs, Form1.Pared.cs, Form1.cs (solo 2270), PantallaVivaGpu.cs (376 y 569) y PrecargaPaginaGpu.cs (64); Plataforma/Puente.ventanas2.cs (un puente nuevo).
- RoundRect (Form1.Ajustes.cs:1932-1945) da un SKPath: figura cerrada, relleno par-impar.
- Sus llamadores: Form1.Ajustes.cs:1880-1883 y Form1.Pared.cs:1492-1497. Form1.cs:2270 pasa la Region al Control por un puente pequeño, Region desde SKPath, que se va en W6.
- FamiliaUi devuelve el nombre por Letras.Familia. Ojo: tiene que lanzar igual cuando falta la letra, y la de reserva es Letras.SinRemates.
- Se quitan los .Name de Form1.Ajustes.cs:669, PantallaVivaGpu.cs:376 y 569, y PrecargaPaginaGpu.cs:64.
- 7 RoundedPath de Form1.Ajustes.cs pasan a ContornoRedondo.
- PincelCtrl vuelve a llamarse CB (Form1.Pared.cs:250), porque CB de la capa se borró en 1b.
- Se quita `using Rocola.Dibujo2D` de Form1.Ajustes.cs:10.
- Comprobar: VistasAjustes (0 de 545), las escenas f3\* y aviso, el arnés pared (contorno) y bateria_f3.py.

**2c. lo-suelto (W1)** (chico)
- Toca: Codigo/Program.cs, Form1.Datos.cs (342 y 362), BombaSonido.cs:294 y Form1.Biblioteca.cs:1592; Plataforma/Traduccion.cs:8; y un archivo nuevo en Nativo para la carpeta del programa.
- Application.StartupPath pasa a la carpeta nativa.
- MethodInvoker pasa a Action.
- Comprobar: lo de siempre, y una prueba NUEVA chica.
  - Tiene que comparar la carpeta nativa con lo que daba Ventanas.Form.cs:374, en JIT y en NativeAOT.
  - Motivo: en NativeAOT, Assembly.Location sale vacío; vale AppContext.BaseDirectory.

### OLEADA 3 (tanda 6)

**3a. armado-vistas (W3-0, con W3e HelpBar dentro)** (medio)
- Toca: Codigo/Form1.cs, Form1.Video.cs, Form1.Biblioteca.cs y Form1.Portadas.cs, y solo las líneas de cabecera de las 15 vistas en Form1.Vistas.cs, Form1.Pared.cs, Form1.Buscador.cs, AyudaCasillaGpu.cs y Form1.Fondo.cs; Plataforma/Puente.video-form1.cs:41.
- Cada vista declara «, IVista» sin dejar de heredar.
- Controls.Add de las vistas (Form1.cs:1122-1357) pasa a la pantalla.
- El ratón: Form1.cs:1125-1126 y 1334.
- DrawCtrlOverlay(Control) y EnSeco(Control) reciben IVista (Form1.Video.cs:1779 y 1854).
- BringToFront y Focus: Form1.Biblioteca.cs:3217, 3319 y 3320, y Form1.Video.cs:1021 y 1026.
- HelpBar sin herencia (Form1.cs:2654-2681). Se borra Puente.video-form1.cs:41.
- Comprobar: pruebas41, pantalla, escenas y recorridos; el arnés video-form1.

**3b. limpieza-puentes** (chico)
- Toca solo Plataforma y Pruebas, más las notas de Codigo/GuiaRojoGpu.cs:140, AcabadoGpu.cs:122, 212 y 260, y EstiloGpu.cs:93.
- Se borran:
  - Puente.pared.cs:27-49;
  - Puente.fondo.cs:20 (RoundedPath);
  - Puente.ventanas2.cs:18;
  - Puente.cs:96 (Pincel);
  - Puente.pantalla-viva.cs entero, tras pasar Pruebas/VistasPantallaViva/PantallaViva.cs:206 y 255 a reflexión.
- Comprobar:
  - el inventario con [Obsolete] (el método de /tmp/claude-0/inventario-t4) da 0 llamadores de lo borrado;
  - el arnés pantalla-viva;
  - el banco: Puente.cs:39, 99, 104 y 105 se quedan para él.

### OLEADA 4 (tanda 7)

**4a. cache-portadas** (grande)
- Toca: Codigo/Form1.Portadas.cs, Form1.Biblioteca.cs, CaratulasAdaptativas.cs, PreviewsIndexadosGpu.cs, Form1.Pared.cs, Form1.Buscador.cs, Form1.Vistas.cs, Form1.cs, Form1.Fondo.cs, Form1.Video.cs, Form1.Sonido.cs, Form1.Datos.cs (solo 503), FluidezGpu.cs, CaratulasSeparadasGpu.cs, D3D9Star.cs (1926-2018, 2435-2456, 2572, 3072), RespuestaGpu.cs, AtraccionGpu.cs, EstadoReposoGpu.cs, BuscadorPortadasGpu.cs, PantallaVivaGpu.cs y DiscoVivoGpu.cs; Plataforma/Puente.portadas.cs:21 y :34.
- Las cachés de portadas, de previas de 32×32 y de celdas pasan a SKBitmap, con el FormatoMapa de XP de cada una.
- Se quitan unos 30 Mapa y 7 Entrega.
- Se quitan los using de FluidezGpu.cs:3 y CaratulasAdaptativas.cs:3.
- Se arreglan las notas «la leen archivos aun sin traducir» (29 en Form1.Biblioteca.cs y 4 en Form1.Portadas.cs), Form1.Pared.cs:134, Form1.Fondo.cs:771 y 1021, CaratulasAdaptativas.cs:507 y BuscadorPortadasGpu.cs:495 y 765.
- Las costuras muertas de Puente.fondo.cs:32 y Puente.pared.cs:68 y :79 se dejan para N7.
- Riesgos:
  - la misma portada tiene que dar el MISMO SKBitmap (identidad);
  - un JPEG es de 24 bits, y de eso dependen la paleta y el InteriorOpaco;
  - leer un SKBitmap ya soltado da violación de segmento.
- Comprobar: arneses portadas, pared, fondo, atraccion, pantalla-viva y buscador-portadas; cintas, recorridos y escenas.
- Caso NUEVO en el arnés portadas: identidad por portada, y «portada soltada», que tiene que fallar como en XP y no con violación de segmento.

**4b. vista-ayuda (W3d)** (chico)
- Toca: Codigo/AyudaCasillaGpu.cs y Plataforma/Puente.asistente.cs:72.
- AyudaView sin herencia. OnPaint pasa a Pinta(SKCanvas), que llama directo a RenderContent.
- Comprobar: los arneses asistente y pantalla.

### OLEADA 5 (tanda 8)

**5a. interfaz-a-skbitmap, con la moneda** (medio)
- Toca: Codigo/Form1.Video.cs, D3D9Star.cs, BuscadorLigeroGpu.cs, FranjaLigeraGpu.cs, BuscadorPortadasGpu.cs, AcabadoGpu.cs, EstadoReposoGpu.cs, Form1.Ajustes.cs:1924, Form1.Biblioteca.cs:3002-3020, Form1.Datos.cs:508 (y su using :10), Form1.Sonido.cs:1197, SuenaVivoGpu.cs (BakeMoneda devuelve SKBitmap: 695, 712, 724, 775 y 849) y PrecargaPaginaGpu.cs:123; Plataforma: borra enteros Puente.frontera.cs y Puente.portadas.cs, y PuenteDibujo.Bitmap (Puente.cs:55).
- uiOverlay, uiBotOverlay, uiPieza, el panel de reposo, el aviso y la ventana del buscador van a la tarjeta como SKBitmap: Bgra8888 premultiplicado, comparados por identidad del mapa y soltados en el orden del cuadro.
- Comprobar: las 29 cintas, el arnés frontera (cintas.sh), los arneses suena y reposo, y los recorridos con texturas quietas.

**5b. vistas-disco (W3a)** (medio)
- Toca: Codigo/Form1.Vistas.cs; Plataforma/Puente.vistas-a.cs:29 y :43, y Puente.vistas-b.cs:57-107.
- Las 7 vistas quedan sin Control: OnPaint pasa a Pinta(SKCanvas), y OnMouseClick y OnMouseDoubleClick pasan a Raton.
- Conservan BackColor, Font (todavía la Font de la capa), Width y Height.
- Comprobar: arneses vistas-a y vistas-b. Hay que quitar los casts a Control y el OnPaint por reflexión de VistasA.cs:25, 116, 128, 216, 265 y 286.

**5c. panel-del-disco (W3f)** (chico)
- Toca: Codigo/Form1.Fondo.cs:2372-2397; Plataforma/Puente.fondo.cs:43 y :53, si nadie más las pide.
- BgPanel queda sin Panel. IVista con padre y desplazamiento (Parent.ClientSize).
- DrawCached llama a la traducida, pero Puente.fondo.cs:48 se queda para Form1.cs:1062 hasta W7.
- Comprobar: el arnés fondo, las escenas y pantalla.

### OLEADA 6 (tanda 9)

**6a. gpupanel-sin-control (W4)** (grande)
- Toca: Codigo/D3D9Star.cs (1976, 2221-2370, 4519-4535), Form1.Video.cs:851-882, 981, 990 y 1167, y Form1.Biblioteca.cs:3203 y 3311; Nativo/PantallaTarjeta.cs y DispositivoTarjetaGl.cs; Plataforma/Anfitrion.cs.
- Idle pasa a PantallaRocola.Libre, y AppIdle a HayPendientes.
- El dispositivo recibe la superficie en vez del Handle.
- El ratón queda en la pantalla, y se va el Control.FromHandle.
- Comprobar: recorridos, cintas, escenas alternadas y NativeAOT.

**6b. vistas-pared (W3b)** (chico)
- Toca: Codigo/Form1.Pared.cs y Form1.Portadas.cs:1199-1202; Plataforma/Puente.video-form1.cs (borra :29 y :74, la EnSeco de XP y GraphicsVideoForm1).
- AttractView, AlbumView y CubeBox quedan sin Control.
- El precalentado de attractView pasa a la EnSeco traducida, con Pinta.
- Comprobar: los arneses pared y atraccion, las escenas y los recorridos.

**6c. vistas-buscador (W3c)** (chico)
- Toca: Codigo/Form1.Buscador.cs; Plataforma/Puente.buscador.cs:32 y :46.
- BuscaView y SearchOverlay quedan sin Control. El foco del teclado lo lleva la pantalla.
- Comprobar: los arneses buscador y buscador-portadas, y la escena del buscador sin tarjeta.

### OLEADA 7 (tanda 10)

**7a. f12-como-hoja (W5)** (chico)
- Toca: Codigo/D3D9Star.cs:1856-1914 y Plataforma/PruebasLinux.cs:195.
- GpuTestWindow pasa a ser una Hoja no modal. El MessageBox pasa a AvisoXp.Muestra.
- Se va TeclaDeMando de Ventanas.Escritorio.cs:507.
- Comprobar: las pruebas de hoja y una escena F12 NUEVA (abrir, cerrar y fallo del dispositivo).

**7b. rotulos-y-letras (W6 con «letras-de-los-controles»)** (grande)
- Toca: Codigo/Form1.cs, Form1.Video.cs, EstiloGpu.cs, TextoMovilGpu.cs, SuenaVivoGpu.cs, Form1.Biblioteca.cs, Form1.Ajustes.cs, FranjaLigeraGpu.cs, Form1.Fondo.cs, Form1.Datos.cs, EstadoReposoGpu.cs, DiscoVivoGpu.cs, AcabadoGpu.cs y Form1.Vistas.cs (Font de las vistas); Plataforma: borra VideoEnVentanas.cs, Puente.vistas-b.cs:83, Puente.ventanas2.cs entero y el puente Region de 2b; y Anfitrion.cs.
- Los 11 Label pasan a Rotulo con Letra.
- ContentAlignment pasa a AlineaTexto.
- La lista de lo que va encima del panel.
- Control.Font pasa a Letra en todas las vistas. Ojo con las identidades: Form1.Vistas.cs:1057 y 1067, y DiscoVivoGpu.cs:77-81.
- Se van los 27 PuenteDibujo.Letra y los 4 PuenteDibujo.Font.
- Comprobar: escenas aviso y f3\*, arneses acabado-estilo, suena, reposo y vistas-b, cintas y recorridos.

### OLEADA 8 (tanda 11)

**8a. form1-sin-form (W7)** (grande)
- Toca: las 14 partes con «: Form» (Form1.Ajustes.cs:20 … Form1.cs:26 y SalidaSonido.cs:154), Form1.Designer.cs, MedidasBitacora.cs:96 y 103, Form1.Portadas.cs:1120 y 1161, y Form1.cs:956 y 1028-1062 (también Application.ExecutablePath e Icon de :1112); Nativo/PantallaRocola.Rocola.cs, un archivo nuevo Form1.Ventana y Hoja con Form1 como dueña.
- Primero se declaran los miembros propios mientras todavía hereda. Después se quita «: Form» de las 14 a la vez.
- Se borra Puente.fondo.cs:48.
- Comprobar:
  - las escenas cierreq, altf4, sigterm, f3sigterm y kill9, con rocola_estado.txt: borrado tras SIGTERM y Alt+F4, conservado tras kill9 (regla del 8-11-2026);
  - pruebas41, hoja, pantalla y recorridos.

**8b. arneses-sin-capa** (medio)
- Toca solo Pruebas/: VistasAcabadoEstilo, VistasVideoForm1, VistasVistasA, VistasVistasB, VistasPared, VistasAsistente, VistasFondo, VistasPantallaViva, VistasReposo, BancoVistas/Hornos.cs, TrazaDibujo y Tarjeta/Reproductor.cs.
- Que dejen de nombrar tipos de Dibujo2D y de Ventanas en el lado del programa nuevo. La referencia sigue compilando su capa congelada.
- Comprobar: cada arnés da el mismo resultado que antes del cambio.

### OLEADA 9 = N7

**9a. borrar-capas** (medio)
- Plataforma, se borran:
  - Dibujo2D.cs;
  - los Puente\*.cs que queden, con Puente.cs (:29-30, 39, 99, 104 y 105) y el ejecutor puente del banco;
  - Ventanas.Control.cs, Ventanas.Escritorio.cs, Ventanas.Form.cs y Ventanas.Tipos.cs;
  - Traduccion.cs.
- Anfitrion.cs se pasa a Nativo.
- Se adaptan TrazaVentanas.cs:61-63, VolcadoEstado.cs:49-115, PruebasLinux.cs:54 y Hoja.cs:137-145 con Hoja.Foco.cs:50.
- Codigo/Form1.Biblioteca.cs:2732-2746: los bloques #else, sin compilar.
- Pruebas41 se borra con pruebas41.sh y lo suyo en compara_trazas.py y referencia.sh.
- Comprobar:
  - compila sin el global using y con 0 avisos, en JIT y en NativeAOT;
  - Pruebas/Inventario da 0 gdi y 0 winforms;
  - completos contra aef2211: banco, todos los arneses, cintas, recorridos, las 21 escenas, hoja (62) y pantalla (32).

**9b. docs-y-notas** (chico)
- Toca solo .md y comentarios fuera de Codigo:
  - LEEME.md:162, PruebaAot.cs:3 y RocolaXp.csproj:7 (LlamadasWindows.cs y Direct3DaGL.cs ya no existen);
  - PLAN_TRADUCCION…md («El port nativo»), PORT_JUNTAS.md y PORT_\*.md;
  - AGENTS.md: una línea con lo hecho, sin decir que funciona en el A4.

Recorrido más largo: Form1.Video.cs pasa por 1a, 3a, 4a, 5a, 6a, 7b y 8a, en siete oleadas distintas. No se puede acortar sin juntar archivos de Codigo/. Lo que más alarga es W2 (1c), que tiene que acabar antes de la oleada 3.
