@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"
echo ==========================================================================
echo   ENTRENAR EL DSEE DE LA ROCOLA
echo --------------------------------------------------------------------------
echo   ARRASTRA SOBRE ESTE ARCHIVO los dos que la rocola deja junto a Rocola.exe:
echo.
echo      rocola_ia_datos.bin    (el reconstructor de agudos; puede pesar gigas)
echo      rocola_eq.txt          (el EQ automatico; unos pocos MB)
echo.
echo   Tambien vale arrastrar la CARPETA donde esten: los busca dentro.
echo   No hace falta traer la musica ni ningun otro archivo.
echo ==========================================================================
echo.

rem --- Buscar Python. Primero el del entorno de upscale (si esta instalado), luego el del sistema. ---
set PY=
if exist "upscale\venv\Scripts\python.exe" set PY="upscale\venv\Scripts\python.exe"
if not defined PY (
  where py >nul 2>nul && set PY=py -3
)
if not defined PY (
  where python >nul 2>nul && set PY=python
)
if not defined PY (
  echo [X] No encontre Python en esta PC.
  echo     Instalalo desde https://www.python.org/downloads/  (marca "Add to PATH"^)
  echo     y despues, en una ventana de consola:   pip install numpy
  echo.
  pause
  exit /b 1
)

rem --- Sin archivos arrastrados: buscar al lado del .bat, por si ya se copiaron aqui. ---
if "%~1"=="" (
  set ENCONTRADOS=
  if exist "rocola_ia_datos.bin" set ENCONTRADOS=!ENCONTRADOS! "rocola_ia_datos.bin"
  if exist "rocola_eq.txt"       set ENCONTRADOS=!ENCONTRADOS! "rocola_eq.txt"
  if defined ENCONTRADOS (
    echo No arrastraste nada, pero encontre material en esta misma carpeta. Lo uso.
    echo.
    %PY% entrenar_dsee.py !ENCONTRADOS!
    goto fin
  )
  %PY% entrenar_dsee.py
  goto fin
)

%PY% entrenar_dsee.py %*

:fin
echo.
pause
