@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  ESCANER DE CALIDAD DE TU BIBLIOTECA
echo ------------------------------------------------------------
echo  Mide TODAS tus canciones y te dice cuales son las mas
echo  danadas por el MP3: ahi es donde el upscaling brilla.
echo  Con biblioteca grande puede tardar un buen rato: dejalo.
echo ============================================================
echo.
if not exist venv\Scripts\python.exe (
  echo [X] Primero corre 1_instalar.bat
  pause
  exit /b 1
)
set /p M=ARRASTRA aqui la carpeta de TODA tu musica y presiona Enter: 
set M=%M:"=%
if not exist "%M%" (
  echo [X] Esa carpeta no existe: %M%
  pause
  exit /b 1
)
venv\Scripts\python escanear_calidad.py --musica "%M%"
echo.
echo Revisa: calidad_biblioteca.csv  y  peores_canciones.txt
pause
