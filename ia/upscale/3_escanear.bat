@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  ESCANER DE CALIDAD DE TU BIBLIOTECA
echo ------------------------------------------------------------
echo  Mide TODAS tus canciones y te dice cuales son las mas
echo  danadas por el MP3: ahi es donde el upscaling brilla.
echo  Con biblioteca grande puede tardar un buen rato: dejalo.
echo ============================================================
echo.
set "PY=%~dp0venv\Scripts\python.exe"
if not exist "%PY%" (
  echo [X] Primero corre 1_instalar.bat
  pause
  exit /b 1
)
set "M="
set /p "M=ARRASTRA aqui la carpeta de TODA tu musica y presiona Enter: "
set "M=%M:"=%"
if not defined M (
  echo [X] No escribiste ninguna carpeta.
  pause
  exit /b 1
)
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] El Python del entorno no puede arrancar. Corre 1_instalar.bat otra vez. & pause & exit /b 1 )
if not exist "%~dp0escanear_calidad.py" ( echo [X] Falta escanear_calidad.py & pause & exit /b 1 )
if not exist "%M%\." (
  echo [X] Esa carpeta no existe: "%M%"
  pause
  exit /b 1
)
"%PY%" "%~dp0escanear_calidad.py" --musica "%M%"
set "COD=%ERRORLEVEL%"
if not "%COD%"=="0" (
  echo.
  echo [X] El escaneo fallo. Revisa el mensaje de arriba.
  pause
  endlocal & exit /b %COD%
)
if not exist "%~dp0calidad_biblioteca.csv" ( echo [X] El escaneo no produjo calidad_biblioteca.csv & pause & endlocal & exit /b 1 )
if not exist "%~dp0peores_canciones.txt" ( echo [X] El escaneo no produjo peores_canciones.txt & pause & endlocal & exit /b 1 )
echo.
echo Revisa: calidad_biblioteca.csv  y  peores_canciones.txt
pause
endlocal & exit /b %COD%
