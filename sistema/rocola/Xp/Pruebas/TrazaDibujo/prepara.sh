#!/bin/bash
# LA COPIA INSTRUMENTADA DE LA REFERENCIA (traza por linea de XP): copia la referencia congelada (REFERENCIA, de fabrica
# /root/rocola-trabajo/referencia-xp, el commit aef2211) a $ROCOLA_TRABAJO/ref-instrumentada, le pone a su Dibujo2D.cs [CallerFilePath] y
# [CallerLineNumber] (instrumenta.py) y la traza (TrazaDibujo.cs), y la compila. El codigo de XP no se toca. Despues:
#   TRAZA=1 ../Recorridos/recorridos.sh $ROCOLA_TRABAJO/ref-instrumentada/sistema/rocola/Xp/bin/Release/net10.0 SALIDA
#   python3 tabla_estado.py SALIDA estado_por_linea.tsv ; python3 diferencias.py SALIDA > diferencias.txt
set -eu
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}; R=${REFERENCIA:-/root/rocola-trabajo/referencia-xp}/sistema/rocola
A=$(cd "$(dirname "$0")" && pwd); D=$W/ref-instrumentada/sistema/rocola
rm -rf "$W/ref-instrumentada"; mkdir -p "$D/Consola/Pantalla" "$D/Motor"
rsync -a --exclude bin --exclude obj "$R/Xp" "$D/"
cp -p "$R/Consola/Pantalla/Gl.cs" "$R/Consola/Pantalla/Kms.cs" "$D/Consola/Pantalla/"; cp -p "$R/Motor/Botones.cs" "$D/Motor/"
P=$D/Xp/Plataforma
python3 "$A/instrumenta.py" "$P/Dibujo2D.cs"
# lo que la traza lee del Graphics, el destino de FromImage y las llamadas de dentro que con los parametros nuevos serian ambiguas
sed -i 's/        private SKMatrix baseMatriz;/        internal SKMatrix baseMatriz; internal System.Drawing.Size Destino;/; s/        private bool conRecorte;/        internal bool conRecorte;/; s/        private RectangleF? recorte;  /        internal RectangleF? recorte;  /' "$P/Dibujo2D.cs"
sed -i 's|{ Traza.Op(null, "Graphics.FromImage", cf_, cl_, i); return new Graphics(new SKCanvas(i.Sk), true); }|{ Traza.Op(null, "Graphics.FromImage", cf_, cl_, i); Graphics g = new Graphics(new SKCanvas(i.Sk), true); g.Destino = i.Size; return g; }|' "$P/Dibujo2D.cs"
sed -i '/public SizeF MeasureString(/s/, null); }/, (StringFormat)null); }/' "$P/Dibujo2D.cs"
cp "$A/TrazaDibujo.cs" "$P/TrazaDibujo.cs"
(cd "$D/Xp" && dotnet build -c Release 2>&1 | tail -3)
