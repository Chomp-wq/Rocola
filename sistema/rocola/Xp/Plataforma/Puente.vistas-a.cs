// EL PUENTE DEL GRUPO «VISTAS-A», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Ya no queda nada: la
// primera parte de Form1.Vistas.cs (SongPagerBar, la barra de paginas de la lista, y LetterBar, la barra de letras) dibuja con SkiaSharp
// directo y las funciones nativas (Xp/Nativo) y ya no hereda de Control.
//   (Las firmas de XP de RenderContent con un Graphics -SongPagerBar.RenderContent(Graphics) y LetterBar.RenderContent(Graphics, bool)-, que
//   pedian sus OnPaint, se quitaron con la oleada 5, grupo vistas-disco: las dos barras son vistas de la pantalla nativa y su Pinta llama
//   directo a la traducida; DrawCtrlOverlay, Form1.Video.cs, ya la llamaba directo. PORT_vistas-disco.md.)
//   (Lo que la traducida le pedia a Form1.Pared.cs -CtrlDirLeft, CtrlDirRight y CtrlTxt- ya va directo: Form1.Pared.cs esta traducido; se
//   quitaron CtrlDirLeftVistasA, CtrlDirRightVistasA, CtrlTxtVistasA y GraphicsVistasA, y la firma de XP de un solo argumento de
//   LetterBar.RenderContent, que nadie llamaba: port nativo, grupo directas-vistas, PORT_directas-vistas.md.)
// El archivo, ahora solo con comentarios, lo borra la limpieza o N7.
