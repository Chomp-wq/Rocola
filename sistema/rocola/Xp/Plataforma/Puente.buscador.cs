// EL PUENTE DEL GRUPO «BUSCADOR», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026; lo que queda tras la tanda 4,
// grupo directas-buscador-video, 12-11-2026). Form1.Buscador.cs (la casilla de BUSCAR, BuscaView, y el buscador de siempre, SearchOverlay: el
// teclado, los resultados, el cubo de la vista previa, el aviso) ya dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo), y llama
// directo a lo ya traducido de otros archivos (las ayudas de los botones y el cubo de Form1.Pared.cs, el cielo y ContornoRedondo de
// Form1.Fondo.cs); a ella la llaman directo DrawCtrlOverlay, EnsureUiOverlay y EnsureUiPieza (Form1.Video.cs) y los precalentados
// (Form1.Portadas.cs y PrecargaPaginaGpu.cs). Aqui solo queda lo que llama el OnPaint de WinForms (la capa de ventanas, aun sin traducir) con un
// Graphics: las firmas de XP de BuscaView.RenderContent(Graphics) y SearchOverlay.RenderContent(Graphics, Rectangle, bool). Le pasan a la
// traducida el lienzo del Graphics y su estado (PuenteDibujo.Lienzo y Estado: suavizado, modo de letra, copia, filtro, medio pixel y la matriz
// del HDC) y despues le dejan al Graphics lo que XP le dejaba cambiado: el suavizado (SmoothingMode = AntiAlias, que las dos ponen y no
// devuelven, cuando llegan a esa linea) y, en SearchOverlay.RenderContent, el filtro (InterpolationMode = Bilinear de DrawPreviewCube, el cubo
// del dibujo por procesador). Se borran cuando se traduzcan los OnPaint (oleada 6 de PLAN_TANDA4_A_N7.md), y todo al final, con Dibujo2D.cs y
// Puente.cs (PORT_buscador.md y PORT_directas-buscador-video.md dicen cual es cual).
// (vistas-buscador, oleada 6c, 12-11-2026) Ya no queda nada: BuscaView y SearchOverlay dejaron de heredar de Control y la pantalla las pinta
// con su Pinta(SKCanvas), que llama directo a la traducida con el estado de un WM_PAINT (Codigo/Form1.Buscador.cs; PORT_vistas-buscador.md).
// Se borraron las firmas de XP BuscaView.RenderContent(Graphics) y SearchOverlay.RenderContent(Graphics, Rectangle, bool), las de sus OnPaint.
// El archivo, solo con comentarios, lo borra la limpieza o N7.
