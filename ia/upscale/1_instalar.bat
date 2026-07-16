@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  INSTALADOR DEL KIT DE LA ROCOLA   paso 1 de 2   v3
echo ------------------------------------------------------------
echo  Instala todo lo necesario EL SOLO. A veces te pedira CERRAR
echo  esta ventana y volver a dar doble clic: es NORMAL, hazlo las
echo  veces que lo pida. La primera vez descarga varios GB.
echo ============================================================
echo.

where winget >nul 2>nul
if errorlevel 1 (
  echo [X] Tu Windows no tiene winget. Abre Microsoft Store, busca
  echo     "App Installer", instalalo, y vuelve a correr este archivo.
  pause
  exit /b 1
)

rem El motor de IA requiere Python 3.11 exacto: los Python mas nuevos aun no
rem tienen motor publicado. Se instala JUNTO al que ya tengas, no quita nada.
py -3.11 --version >nul 2>nul
if errorlevel 1 (
  echo [1/6] Instalando Python 3.11, el que pide el motor de IA...
  winget install -e --id Python.Python.3.11 --accept-source-agreements --accept-package-agreements
  echo.
  echo *** LISTO ESTE PASO: CIERRA esta ventana y vuelve a dar doble clic a 1_instalar.bat ***
  pause
  exit /b 0
)
echo [1/6] Python 3.11: OK

where git >nul 2>nul
if errorlevel 1 (
  echo [2/6] Instalando Git...
  winget install -e --id Git.Git --accept-source-agreements --accept-package-agreements
  echo.
  echo *** LISTO ESTE PASO: CIERRA esta ventana y vuelve a dar doble clic a 1_instalar.bat ***
  pause
  exit /b 0
)
echo [2/6] Git: OK

where ffmpeg >nul 2>nul
if errorlevel 1 (
  echo [3/6] Instalando FFmpeg...
  winget install -e --id Gyan.FFmpeg --accept-source-agreements --accept-package-agreements
  echo.
  echo *** LISTO ESTE PASO: CIERRA esta ventana y vuelve a dar doble clic a 1_instalar.bat ***
  pause
  exit /b 0
)
echo [3/6] FFmpeg: OK

rem Si ya habia un entorno hecho con OTRO Python, se rehace con 3.11.
if exist venv\Scripts\python.exe (
  venv\Scripts\python --version 2>nul | findstr /C:" 3.11" >nul
  if errorlevel 1 (
    echo [4/6] El entorno anterior usaba otro Python: rehaciendolo con 3.11...
    rmdir /s /q venv
  )
)
if not exist venv\Scripts\python.exe (
  echo [4/6] Creando el entorno de Python 3.11...
  py -3.11 -m venv venv
  if errorlevel 1 (
    echo [X] No se pudo crear el entorno.
    pause
    exit /b 1
  )
)
venv\Scripts\python --version
echo [4/6] Entorno: OK

echo [5/6] Instalando el motor de IA: PyTorch con CUDA, ~3 GB la primera vez, paciencia...
venv\Scripts\python -m pip install --upgrade pip >nul 2>nul
set TORCH_OK=
for %%I in (cu128 cu126 cu124 cu121) do (
  if not defined TORCH_OK (
    echo    ... probando el canal %%I
    venv\Scripts\pip install torch torchaudio --index-url https://download.pytorch.org/whl/%%I && set TORCH_OK=%%I
  )
)
if not defined TORCH_OK (
  echo [X] Ningun canal de PyTorch funciono. Revisa tu internet y vuelve a correr
  echo     este archivo. Si sigue igual, manda foto de esta ventana.
  pause
  exit /b 1
)
echo    Motor instalado desde el canal: %TORCH_OK%
venv\Scripts\pip install numpy matplotlib einops rotary-embedding-torch huggingface_hub soundfile
if not exist Apollo (
  echo [5/6] Descargando Apollo, el modelo de restauracion...
  git clone https://github.com/JusperLee/Apollo
)
if exist Apollo\requirements.txt (
  venv\Scripts\pip install -r Apollo\requirements.txt || echo    ...algun extra fallo, normalmente no importa
)
echo [5/6] Motor e IA: OK

echo [6/6] Verificacion final + descarga del modelo, ~180 MB...
venv\Scripts\python 00_verificar.py --descargar
echo.
echo ============================================================
echo  Si arriba dice "TODO LISTO": sigue con  2_piloto.bat
echo  Si algo salio con [X]: vuelve a correr este mismo archivo.
echo ============================================================
pause
