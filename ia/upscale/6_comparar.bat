@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  ELEGIR CUANTA IA   -   compara niveles de mezcla
echo ------------------------------------------------------------
echo  Genera cada cancion danada en varios niveles de IA para
echo  que elijas tu punto. Usa las muestras de 4_muestras.bat.
echo ============================================================
echo.
if not exist venv\Scripts\python.exe ( echo [X] Primero corre 1_instalar.bat & pause & exit /b 1 )
if not exist muestras_danadas ( echo [X] Primero corre 4_muestras.bat & pause & exit /b 1 )
venv\Scripts\python comparar_mezcla.py
echo.
echo Resultados en la carpeta comparar_mezcla  -  ver recomendado.txt
pause
