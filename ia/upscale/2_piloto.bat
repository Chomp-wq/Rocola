@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  PILOTO DE UPSCALING   (paso 2 de 2)
echo ------------------------------------------------------------
echo  Necesitas una carpeta con 12-20 canciones MP3 de la rocola
echo  (variadas: cumbia, regional, K-pop, metal, 80s...).
echo ============================================================
echo.
if not exist venv\Scripts\python.exe (
  echo [X] Primero corre 1_instalar.bat
  pause
  exit /b 1
)
set /p ENTRADA=ARRASTRA aqui la carpeta con tus canciones y presiona Enter: 
set ENTRADA=%ENTRADA:"=%
if not exist "%ENTRADA%" (
  echo [X] Esa carpeta no existe: %ENTRADA%
  pause
  exit /b 1
)
set SALIDA=%~dp0piloto_resultado
echo.
echo Procesando... (unos minutos; veras el avance cancion por cancion)
venv\Scripts\python 01_piloto.py --entrada "%ENTRADA%" --salida "%SALIDA%"
echo.
echo ============================================================
echo  RESULTADO en:  %SALIDA%
echo   1) Abre la carpeta "ciego" y escucha cada par X / Y
echo   2) Anota cual te gusto mas de cada numero
echo   3) Al FINAL abre clave.txt para ver cual era cual
echo ============================================================
pause
