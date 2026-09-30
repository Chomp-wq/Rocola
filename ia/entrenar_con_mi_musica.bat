@echo off
setlocal
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo ==============================================================
echo  ENTRENAR LA IA PROPIA DE LA ROCOLA CON TU MUSICA
echo --------------------------------------------------------------
echo  Hace TODO solo, en 2 pasos:
echo   1. Recorre tu biblioteca y junta 600 canciones de CALIDAD
echo      COMPLETA - las danadas no pueden ensenar. Tarda un rato.
echo   2. Entrena un candidato aislado; no reemplaza rocola_ia.bin.
echo  Es reanudable: si lo cierras, vuelve a abrirlo y continua.
echo ==============================================================
echo.
set "PY=%~dp0upscale\venv\Scripts\python.exe"
if not exist "%PY%" (
  echo [X] Primero corre upscale\1_instalar.bat
  pause
  exit /b 1
)
"%PY%" -c "import sys" >nul 2>nul
if not "%ERRORLEVEL%"=="0" (
  echo [X] El Python del entorno existe, pero no puede arrancar. Corre upscale\1_instalar.bat otra vez.
  pause
  exit /b 1
)
if not exist "%~dp0preparar_entrenamiento.py" ( echo [X] Falta preparar_entrenamiento.py & pause & exit /b 1 )
if not exist "%~dp0entrenar_ia.py" ( echo [X] Falta entrenar_ia.py & pause & exit /b 1 )
set "MUSICA=%~1"
if "%MUSICA%"=="" set "MUSICA=auto"
if not "%MUSICA%"=="auto" echo Usare la carpeta: "%MUSICA%"
if "%MUSICA%"=="auto" echo Buscare la carpeta "musica" cerca de aqui, como hace la rocola.
echo Consejo: tambien puedes ARRASTRAR tu carpeta de musica sobre este .bat
echo.
echo --- PASO 1 de 2: juntando canciones de calidad completa ---
"%PY%" "%~dp0preparar_entrenamiento.py" --musica "%MUSICA%" --canciones 600
set "COD=%ERRORLEVEL%"
if not "%COD%"=="0" (
  echo [X] No se pudo preparar el material. Revisa el mensaje de arriba.
  pause
  endlocal & exit /b %COD%
)
echo.
echo --- PASO 2 de 2: entrenando la red ---
:destino_nuevo
set "CANDIDATO_DIR=%~dp0entrenado\dsee-%RANDOM%-%RANDOM%"
if exist "%CANDIDATO_DIR%" goto destino_nuevo
mkdir "%CANDIDATO_DIR%"
if not "%ERRORLEVEL%"=="0" ( echo [X] No pude crear destino aislado. & pause & exit /b 1 )
"%PY%" "%~dp0entrenar_ia.py" --musica "%~dp0entrenamiento_wavs" --clips 7200 --por_archivo 12 --candidato "%CANDIDATO_DIR%\candidato.bin" --informe "%CANDIDATO_DIR%\informe_dsee.json"
set "COD=%ERRORLEVEL%"
rem 4 es candidato valido sin activar; 2/3 diagnostico rechazado/no verificable.
if "%COD%"=="2" (
  echo.
  echo  Candidato aislado rechazado: no le gano a la curva fija y al modelo que ya tenias
  echo  por el margen que se exige. No es un fallo. Tu rocola_ia.bin sigue igual.
  echo  Candidato e informe estan en "%CANDIDATO_DIR%". No activarlo.
  pause
  endlocal & exit /b 2
)
if "%COD%"=="3" (
  echo.
  echo  Candidato diagnostico no verificable: no hay canciones suficientes para validar con musica
  echo  que la red no haya visto. No es un fallo. Tu rocola_ia.bin sigue igual.
  echo  Junta mas canciones con el PASO 1 y vuelve a correr este archivo.
  pause
  endlocal & exit /b 3
)
if not "%COD%"=="4" (
  echo [X] El entrenamiento fallo. Revisa el mensaje de arriba. Tu rocola_ia.bin sigue igual.
  pause
  endlocal & exit /b %COD%
)
echo.
echo ==============================================================
echo  CANDIDATO ENTRENADO; NO ACTIVADO:
echo    %CANDIDATO_DIR%\candidato.bin
echo    %CANDIDATO_DIR%\informe_dsee.json
echo  PCM real y escucha a nivel igualado pendientes antes de activacion.
echo  El modelo instalado, musica y material preparado siguen intactos.
echo ==============================================================
pause
endlocal & exit /b 0
