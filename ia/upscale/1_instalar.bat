@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  INSTALADOR DEL KIT DE LA ROCOLA   (paso 1 de 2)
echo ------------------------------------------------------------
echo  Instala todo lo necesario EL SOLO. A veces te pedira CERRAR
echo  esta ventana y volver a dar doble clic: es NORMAL, hazlo las
echo  veces que lo pida. La primera vez descarga varios GB.
echo ============================================================
echo.

where winget >nul 2>nul
if errorlevel 1 (
  echo [X] Tu Windows no tiene 'winget'. Abre Microsoft Store, busca
  echo     "App Installer", instalalo, y vuelve a correr este archivo.
  pause
  exit /b 1
)

py -3 --version >nul 2>nul
if errorlevel 1 (
  echo [1/6] Instalando Python...
  winget install -e --id Python.Python.3.11 --accept-source-agreements --accept-package-agreements
  echo.
  echo *** LISTO ESTE PASO: CIERRA esta ventana y vuelve a dar doble clic a 1_instalar.bat ***
  pause
  exit /b 0
)
echo [1/6] Python: OK

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

if not exist venv\Scripts\python.exe (
  echo [4/6] Creando el entorno de Python...
  py -3 -m venv venv
  if errorlevel 1 ( echo [X] No se pudo crear el entorno. & pause & exit /b 1 )
)
echo [4/6] Entorno: OK

echo [5/6] Instalando el motor de IA (PyTorch con CUDA, ~3 GB la primera vez; paciencia)...
venv\Scripts\python -m pip install --upgrade pip >nul 2>nul
venv\Scripts\pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu124
if errorlevel 1 ( echo [X] Fallo PyTorch: revisa tu internet y vuelve a correr este archivo. & pause & exit /b 1 )
venv\Scripts\pip install numpy matplotlib einops rotary-embedding-torch huggingface_hub soundfile
if not exist Apollo (
  echo [5/6] Descargando Apollo (el modelo de restauracion)...
  git clone https://github.com/JusperLee/Apollo
)
if exist Apollo\requirements.txt (
  venv\Scripts\pip install -r Apollo\requirements.txt || echo    (algun extra fallo; normalmente no importa)
)
echo [5/6] Motor e IA: OK

echo [6/6] Verificacion final + descarga del modelo (~180 MB)...
venv\Scripts\python 00_verificar.py --descargar
echo.
echo ============================================================
echo  Si arriba dice "TODO LISTO": sigue con  2_piloto.bat
echo  Si algo salio con [X]: vuelve a correr este mismo archivo.
echo ============================================================
pause
