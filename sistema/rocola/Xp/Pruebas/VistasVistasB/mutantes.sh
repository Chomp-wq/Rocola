#!/bin/bash
# mutantes.sh [CARPETA]: comprueba que el arnes VE los errores. Compila el arnes con copias de esta carpeta Xp a las que se les mete un
# error a proposito (uno cada vez, en la traduccion de Form1.Vistas.cs o en su puente) y cuenta las lineas que cambian frente a la salida
# de esta copia sin tocar (CARPETA/../arnes/nuevo-falsas.tsv, de arnes.sh). Ninguno debe dar 0. La copia de trabajo no se toca.
set -eu
AQUI=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$AQUI/../.." && pwd)
W=${1:-/root/rocola-trabajo/port/vistas-b/mut}
BUENO=${BUENO:-$W/../arnes/nuevo-falsas.tsv}
LETRAS=${LETRAS_FALSAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
V=Codigo/Form1.Vistas.cs; P=Plataforma/Puente.vistas-b.cs
mut() {  # nombre archivo 'viejo' 'nuevo'
  if [ -n "${MUTANTES:-}" ] && ! echo " $MUTANTES " | grep -q " $1 "; then return 0; fi
  rm -rf "$W/xp" "$W/proj" "$W/bin"; mkdir -p "$W/xp/Xp" "$W/xp/Consola/Pantalla" "$W/xp/Motor" "$W/proj"
  cp -r "$XP/Codigo" "$XP/Plataforma" "$XP/Nativo" "$W/xp/Xp/"; cp "$XP/../Consola/Pantalla/Gl.cs" "$XP/../Consola/Pantalla/Kms.cs" "$W/xp/Consola/Pantalla/"; cp "$XP/../Motor/Botones.cs" "$W/xp/Motor/"
  python3 - "$W/xp/Xp/$2" "$3" "$4" <<'PY'
import sys
p, a, b = sys.argv[1:4]; s = open(p, 'rb').read(); a = a.encode(); b = b.encode()
assert s.count(a) >= 1, "no esta: %r" % a
open(p, 'wb').write(s.replace(a, b, 1))
PY
  cp "$AQUI/VistasVistasB.csproj" "$AQUI/VistasB.cs" "$W/proj/"
  (cd "$W/proj" && dotnet build -c Release -nologo -v q -p:Fuente="$W/xp/Xp" -o "$W/bin" 2>&1 | grep -E " error " | head -3 || true)
  env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP="$LETRAS" dotnet "$W/bin/VistasVistasB.dll" > "$W/$1.tsv" 2> "$W/$1.err" || true
  echo "$1: $(diff "$BUENO" "$W/$1.tsv" | grep -c '^>' || true) lineas distintas de $(wc -l < "$BUENO")"
  rm -rf "$W/xp" "$W/proj" "$W/bin" "$W/$1.err"
}
mut raya-de-2 $V 'Color.FromArgb(70, 150, 175, 225).Sk(), StrokeWidth = 1f' 'Color.FromArgb(70, 150, 175, 225).Sk(), StrokeWidth = 2f'
mut arco-sin-unir $V 'p.ArcTo(SKRect.Create(r.Right - d, r.Y, d, d), 270, 90, false);' 'p.ArcTo(SKRect.Create(r.Right - d, r.Y, d, d), 270, 90, true);'
mut recorte-del-rodillo $V 'Math.Max(wv, 10f) + 12f, h - 2f); g.ClipRect' 'Math.Max(wv, 10f) + 12f, h - 9f); g.ClipRect'
mut filtro-portaditas $V 'filtro = Filtro.BilinealAlta;' 'filtro = Filtro.Bilineal;'
mut genero-sin-formato $V 'brocha, new RectangleF(0, 0, w, h), sfCen, copia, medio);' 'brocha, new RectangleF(0, 0, w, h), null, copia, medio);'
mut degradado-horizontal $V 'Shade(_accent, 0.1), Degradado.Vertical, true)' 'Shade(_accent, 0.1), Degradado.Horizontal, true)'
mut titulo-sin-puntos $V 'txtFmt.Corte = CorteTexto.PuntosPorLetra;' 'txtFmt.Corte = CorteTexto.Nada;'
mut pagina-sin-suavizar $V 'suave = true;   // EN LINUX: g.SmoothingMode = AntiAlias: queda puesto' 'suave = false;   // EN LINUX: g.SmoothingMode = AntiAlias: queda puesto'
mut cifras-ajustadas $V 'AjusteLetra ajuste = AjusteLetra.Suave;   // EN LINUX: g.TextRenderingHint = AntiAlias, en una variable' 'AjusteLetra ajuste = AjusteLetra.SuaveAjustada;   // EN LINUX: g.TextRenderingHint = AntiAlias, en una variable'
mut puente-sin-suavizado $P 'if (!(Width < 8 || Height < 8)) g.SmoothingMode = SmoothingMode.AntiAlias;' ';'
mut sacudida-sin-volver $V 'if (stSac != -1) g.RestoreToCount(stSac);' ';'
mut copia-olvidada $V 'TextoGdiPlus.Escribe(g, sec, fUsar, ajuste, brocha, new RectangleF(x, 1.4f, tope, h), sfIzq, copia, medio);' 'TextoGdiPlus.Escribe(g, sec, fUsar, ajuste, brocha, new RectangleF(x, 1.4f, tope, h), sfIzq, false, medio);'
mut medio-olvidado $V 'Pixel.Rellena(g, PuenteDibujo.Ruta(fp), fb, suave, medio, copia);' 'Pixel.Rellena(g, PuenteDibujo.Ruta(fp), fb, suave, false, copia);'
mut pluma-del-riel $V 'trackP = new SKPaint { Color = Color.FromArgb(40, 255, 255, 255).Sk(), StrokeWidth = 1f };' 'trackP = new SKPaint { Color = Color.FromArgb(40, 255, 255, 255).Sk(), StrokeWidth = 1f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };'
mut hojeo-filtro $V 'new SKRect(xa, 0, xa + hornoAnt.Width, hornoAnt.Height), filtro, medio, copia); }' 'new SKRect(xa, 0, xa + hornoAnt.Width, hornoAnt.Height), Filtro.Vecino, medio, copia); }'
mut hojeo-sitio $V '{ int xa = (int)(dx - slideDesde);' '{ int xa = (int)dx;'
mut modo-de-letra-olvidado $V 'TextoGdiPlus.Escribe(g, t, fCola, ajuste, colaB, cr, cenFmt, copia, medio);' 'TextoGdiPlus.Escribe(g, t, fCola, AjusteLetra.Suave, colaB, cr, cenFmt, copia, medio);'
# (revision) otros diez, en sitios que los de arriba no tocan: la duracion, el «+N», los puntos de las paginas, el neon, los puentes del genero y de
# la barra, el circulito, el recorte de las portaditas, la pluma de la fila elegida y la copia de la lista
mut duracion-a-la-izquierda $V 'durFmt.Horizontal = Alinea.Lejos;' 'durFmt.Horizontal = Alinea.Cerca;'
mut mas-n-otro-fondo $V 'Color.FromArgb(205, 8, 11, 28).Sk()' 'Color.FromArgb(200, 8, 11, 28).Sk()'
mut halo-del-punto $V 'new RectangleF(cx - rr * 2.1f, cy - rr * 2.1f, rr * 4.2f, rr * 4.2f), halo' 'new RectangleF(cx - rr * 2.0f, cy - rr * 2.0f, rr * 4.0f, rr * 4.0f), halo'
mut nucleo-del-neon $V 'StrokeWidth = Math.Max(1f, h * 0.16f)' 'StrokeWidth = Math.Max(1f, h * 0.2f)'
mut genero-puente-sin-suavizado $P 'if (!(Width < 6 || Height < 6 || texto.Length == 0)) g.SmoothingMode = SmoothingMode.AntiAlias;' ';'
mut circulo-sin-suavizar $V 'Pixel.TrazaOvalo(g, r, p, suave);' 'Pixel.TrazaOvalo(g, r, p, false);'
mut portadita-sin-recorte $V 'g.ClipPath(ph, SKClipOperation.Intersect, false);   // EN LINUX: g.SetClip(trazo, Intersect): en pixeles enteros' 'g.ClipPath(ph, SKClipOperation.Intersect, true);   // EN LINUX: g.SetClip(trazo, Intersect): en pixeles enteros'
mut fila-elegida-pluma $V 'hlP = new SKPaint { Color = Color.Black.Sk(), StrokeWidth = 1.4f }' 'hlP = new SKPaint { Color = Color.Black.Sk(), StrokeWidth = 1.0f }'
mut barra-puente-sin-suavizado $P 'g.SmoothingMode = SmoothingMode.AntiAlias;   // (lo pone antes de mirar su medida)' ';'
mut lista-copia-olvidada $V '{ Imagenes.Pinta(g, hornoPag, new SKRect(0, 0, hornoPag.Width, hornoPag.Height), new SKRect(0, 0, hornoPag.Width, hornoPag.Height), filtro, medio, copia); return; }' '{ Imagenes.Pinta(g, hornoPag, new SKRect(0, 0, hornoPag.Width, hornoPag.Height), new SKRect(0, 0, hornoPag.Width, hornoPag.Height), filtro, medio, false); return; }'
