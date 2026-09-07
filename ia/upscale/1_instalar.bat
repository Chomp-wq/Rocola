@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ============================================================
echo  INSTALADOR DEL KIT DE LA ROCOLA   paso 1 de 2   v5
echo ------------------------------------------------------------
echo  Instala todo lo necesario EL SOLO. Si te pide cerrar y
echo  volver a dar doble clic, hazlo. Descarga varios GB la
echo  primera vez: deja la ventana abierta hasta el final.
echo ============================================================
echo.

where winget >nul 2>nul
if errorlevel 1 (
  echo [X] Tu Windows no tiene winget. Abre Microsoft Store, busca
  echo     "App Installer", instalalo, y vuelve a correr este archivo.
  pause
  exit /b 1
)

rem ---- Python 3.11 por RUTA EXACTA. El lanzador "py" nuevo de Windows miente:
rem ---- dice OK y luego no tiene el 3.11. Aqui se busca el programa real.
set "PYEXE="
if exist "%LocalAppData%\Programs\Python\Python311\python.exe" set "PYEXE=%LocalAppData%\Programs\Python\Python311\python.exe"
if not defined PYEXE if exist "%ProgramFiles%\Python311\python.exe" set "PYEXE=%ProgramFiles%\Python311\python.exe"
if not defined PYEXE (
  echo [1/6] Instalando Python 3.11, el que pide el motor de IA...
  call :instala_winget Python.Python.3.11
  if errorlevel 1 goto fallo_winget_python
  if exist "%LocalAppData%\Programs\Python\Python311\python.exe" set "PYEXE=%LocalAppData%\Programs\Python\Python311\python.exe"
  if not defined PYEXE if exist "%ProgramFiles%\Python311\python.exe" set "PYEXE=%ProgramFiles%\Python311\python.exe"
)
if not defined PYEXE (
  echo [X] No encuentro Python 3.11 ni despues de instalarlo.
  echo     Manda foto de esta ventana.
  pause
  exit /b 1
)
echo [1/6] Python 3.11: OK
echo        %PYEXE%

where git >nul 2>nul
if errorlevel 1 (
  echo [2/6] Instalando Git...
  call :instala_winget Git.Git
  if errorlevel 1 goto fallo_winget_git
  echo.
  echo *** LISTO ESTE PASO: CIERRA esta ventana y vuelve a dar doble clic a 1_instalar.bat ***
  pause
  endlocal & exit /b 2
)
echo [2/6] Git: OK

where ffmpeg >nul 2>nul
if errorlevel 1 (
  echo [3/6] Instalando FFmpeg...
  call :instala_winget Gyan.FFmpeg
  if errorlevel 1 goto fallo_winget_ffmpeg
  echo.
  echo *** LISTO ESTE PASO: CIERRA esta ventana y vuelve a dar doble clic a 1_instalar.bat ***
  pause
  endlocal & exit /b 2
)
echo [3/6] FFmpeg: OK

rem ---- Si ya habia un entorno hecho con OTRO Python, se rehace con 3.11.
if exist venv\Scripts\python.exe (
  venv\Scripts\python --version 2>nul | findstr /C:" 3.11" >nul
  if errorlevel 1 (
    echo [4/6] El entorno anterior usaba otro Python: rehaciendolo con 3.11...
    rmdir /s /q venv
    if exist venv\Scripts\python.exe goto fallo_venv_borrar
  )
)
if not exist venv\Scripts\python.exe (
  echo [4/6] Creando el entorno de Python 3.11...
  "%PYEXE%" -m venv venv
)
rem ---- Verificacion por ARCHIVO, no por codigos de salida: los archivos no mienten.
if not exist venv\Scripts\python.exe (
  echo [X] El entorno no se pudo crear. Manda foto de esta ventana.
  pause
  exit /b 1
)
venv\Scripts\python --version
if errorlevel 1 goto fallo_venv
echo [4/6] Entorno: OK

echo [5/6] Instalando el motor de IA: PyTorch con CUDA, ~3 GB la primera vez, paciencia...
venv\Scripts\python -m pip install --upgrade pip
if errorlevel 1 goto fallo_dependencias
set "TORCH_OK="
for %%I in (cu128 cu126 cu124 cu121) do (
  if not defined TORCH_OK (
    echo    ... probando el canal %%I
    venv\Scripts\python -m pip install torch torchaudio --index-url https://download.pytorch.org/whl/%%I && set "TORCH_OK=%%I"
  )
)
if not defined TORCH_OK (
  echo [X] Ningun canal de PyTorch funciono. Revisa tu internet y vuelve a correr
  echo     este archivo. Si sigue igual, manda foto de esta ventana.
  pause
  exit /b 1
)
echo    Motor instalado desde el canal: %TORCH_OK%
venv\Scripts\python -m pip install numpy matplotlib einops rotary-embedding-torch huggingface_hub soundfile omegaconf ml_collections
if errorlevel 1 goto fallo_dependencias
set "APOLLO_PATH=%APOLLO_DIR%"
if not defined APOLLO_PATH set "APOLLO_PATH=%~dp0Apollo"
if not exist "%APOLLO_PATH%\." (
  echo [5/6] Descargando Apollo, el modelo de restauracion...
  git clone https://github.com/JusperLee/Apollo "%APOLLO_PATH%"
  if errorlevel 1 goto fallo_apollo
)
if not exist "%APOLLO_PATH%\." goto fallo_apollo
if exist "%APOLLO_PATH%\requirements.txt" (
  venv\Scripts\python -m pip install -r "%APOLLO_PATH%\requirements.txt"
  if errorlevel 1 goto fallo_dependencias
)
echo [5/6] Motor e IA: OK

echo [6/6] Verificacion final + descarga del modelo, ~180 MB...
venv\Scripts\python 00_verificar.py --descargar
set "COD=%ERRORLEVEL%"
echo.
if not "%COD%"=="0" (
  echo ============================================================
  echo  [X] La verificacion encontro problemas. La instalacion NO esta lista.
  echo      Corrige lo marcado arriba y vuelve a correr este archivo.
  echo ============================================================
  pause
  endlocal & exit /b %COD%
)
echo ============================================================
echo  TODO LISTO. Sigue con  2_piloto.bat
echo ============================================================
pause
endlocal & exit /b 0

:instala_winget
winget install -e --id "%~1" --accept-source-agreements --accept-package-agreements
if "%ERRORLEVEL%"=="0" exit /b 0
exit /b 1

:fallo_winget_python
echo [X] winget no pudo instalar Python 3.11.
goto fallo_instalacion

:fallo_winget_git
echo [X] winget no pudo instalar Git.
goto fallo_instalacion

:fallo_winget_ffmpeg
echo [X] winget no pudo instalar FFmpeg.
goto fallo_instalacion

:fallo_venv_borrar
echo [X] No se pudo quitar el entorno de Python anterior.
goto fallo_instalacion

:fallo_venv
echo [X] El entorno de Python existe, pero no puede arrancar.
goto fallo_instalacion

:fallo_dependencias
echo [X] No se pudieron instalar todas las dependencias de Python.
goto fallo_instalacion

:fallo_apollo
echo [X] No se pudo preparar el repositorio Apollo en:
echo     "%APOLLO_PATH%"

:fallo_instalacion
echo     Revisa el error de arriba y vuelve a correr este archivo.
pause
endlocal & exit /b 1
