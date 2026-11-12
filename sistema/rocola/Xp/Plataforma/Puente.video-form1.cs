// EL PUENTE DEL GRUPO «VIDEO-FORM1», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Video.cs (la imagen de la
// interfaz que se sube a la tarjeta: EnsureUiOverlay, EnsureUiBotOverlay, EnsureUiPieza, DrawCtrlOverlay, DrawLabelOverlay; y el precalentado,
// EnSeco) y Form1.cs (la barra de ayuda, HelpBar) ya dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo). Ya no queda nada aqui:
//   - La firma de XP de HelpBar.RenderContent(Graphics, bool), para su OnPaint, se borro con la tanda 6 (grupo armado-vistas): la barra ya no
//     hereda de Control y su Pinta llama directo a la traducida.
//   - Desde la tanda 4 (grupo directas-buscador-video, 12-11-2026) Form1.Video.cs y Form1.cs llaman DIRECTO a lo ya traducido.
//   - La firma de XP de EnSeco(vista, Action<Graphics>) y el Graphics sobre un lienzo traducido que daba (GraphicsVideoForm1) se borraron con la
//     oleada 6 (grupo vistas-pared, 12-11-2026): el precalentado de la atraccion (Form1.Portadas.cs) llama a la EnSeco traducida y pinta la vista
//     con su Pinta (Form1.Pared.cs: la atraccion ya no hereda de Control ni tiene OnPaint).
// El archivo, solo con comentarios, lo borra la limpieza o N7.
