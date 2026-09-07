@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  ELEGIR CUANTA IA   -   compara niveles de mezcla
echo ------------------------------------------------------------
echo  Genera cada cancion danada en varios niveles de IA para
echo  que elijas tu punto. Usa las muestras de 4_muestras.bat.
echo ============================================================
echo.
set "PY=%~dp0venv\Scripts\python.exe"
if not exist "%PY%" ( echo [X] Primero corre 1_instalar.bat & pause & exit /b 1 )
if not exist "%~dp0muestras_danadas\." ( echo [X] Primero corre 4_muestras.bat & pause & exit /b 1 )
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo [X] El Python del entorno no puede arrancar. Corre 1_instalar.bat otra vez. & pause & exit /b 1 )
if not exist "%~dp0comparar_mezcla.py" ( echo [X] Falta comparar_mezcla.py & pause & exit /b 1 )
"%PY%" "%~dp0comparar_mezcla.py"
set "COD=%ERRORLEVEL%"
if not "%COD%"=="0" ( echo. & echo [X] No se pudieron generar las comparaciones. & pause & endlocal & exit /b %COD% )
findstr /R "." "%~dp0comparar_mezcla\recomendado.txt" >nul 2>nul
if not "%ERRORLEVEL%"=="0" ( echo. & echo [X] No se produjo ninguna comparacion valida. & pause & endlocal & exit /b 1 )
echo.
echo Resultados en la carpeta comparar_mezcla  -  ver recomendado.txt
pause
endlocal & exit /b %COD%
