@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  PROCESADOR MASIVO   -   toda la biblioteca en calidad HD
echo ------------------------------------------------------------
echo  Procesa las canciones aptas en una carpeta NUEVA.
echo  Las buenas se copian solo si coincide el formato elegido;
echo  las demas se convierten. WAV convertido no lleva portada.
echo  Se puede retomar. Esta salida NO sustituye un respaldo original.
echo  Puede tardar horas: dejalo corriendo, hasta de noche.
echo ============================================================
echo.
set "PY=%~dp0venv\Scripts\python.exe"
set "CSV=%~dp0calidad_biblioteca.csv"
if not exist "%PY%" ( echo [X] Primero corre 1_instalar.bat & pause & exit /b 1 )
if not exist "%CSV%" ( echo [X] Primero corre 3_escanear.bat & pause & exit /b 1 )
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] El Python del entorno no puede arrancar. Corre 1_instalar.bat otra vez. & pause & exit /b 1 )
if not exist "%~dp005_procesar_todo.py" ( echo [X] Falta 05_procesar_todo.py & pause & exit /b 1 )
set "M="
set /p "M=ARRASTRA aqui la carpeta de TU musica y Enter: "
set "M=%M:"=%"
if not defined M ( echo [X] No escribiste la carpeta de musica. & pause & exit /b 1 )
if not exist "%M%\." ( echo [X] Esa carpeta no existe: "%M%" & pause & exit /b 1 )
set "S="
set /p "S=ARRASTRA aqui una carpeta VACIA en OTRO disco para la copia HD y Enter: "
set "S=%S:"=%"
if not defined S ( echo [X] No escribiste la carpeta de salida. & pause & exit /b 1 )
if not exist "%S%\." ( echo [X] Esa carpeta de salida no existe: "%S%" & pause & exit /b 1 )
echo.
echo Formato de salida:  mp3 = compatible con todo, 320k.  wav = sin perdida, mucho mas pesado.
set "F="
set /p "F=Escribe mp3 o wav y Enter (Enter = mp3): "
set "F=%F:"=%"
if not defined F set "F=mp3"
if /i not "%F%"=="mp3" if /i not "%F%"=="wav" (
  echo [X] El formato solo puede ser mp3 o wav.
  pause
  exit /b 1
)
echo.
echo Cuanta IA: auto = suave en lo muy danado, fuerte en lo poco danado. O un numero 0-100.
set "IA="
set /p "IA=Escribe auto o un numero y Enter (Enter = auto): "
set "IA=%IA:"=%"
if not defined IA set "IA=auto"
"%PY%" "%~dp005_procesar_todo.py" --musica "%M%" --salida "%S%" --formato "%F%" --mezcla "%IA%"
set "COD=%ERRORLEVEL%"
echo.
if not "%COD%"=="0" echo [X] El procesamiento termino con errores; revisa el detalle de arriba.
pause
endlocal & exit /b %COD%
