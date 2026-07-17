@echo off
setlocal
cd /d "%~dp0"
echo ==============================================================
echo  ENTRENAR LA IA PROPIA DE LA ROCOLA CON TU MUSICA
echo --------------------------------------------------------------
echo  Hace TODO solo, en 2 pasos:
echo   1. Recorre tu biblioteca y junta 600 canciones de CALIDAD
echo      COMPLETA - las danadas no pueden ensenar. Tarda un rato.
echo   2. Entrena la red y deja rocola_ia.bin en esta carpeta.
echo  Es reanudable: si lo cierras, vuelve a abrirlo y continua.
echo ==============================================================
echo.
if not exist upscale\venv\Scripts\python.exe (
  echo [X] Primero corre upscale\1_instalar.bat
  pause
  exit /b 1
)
set MUSICA=%~1
if "%MUSICA%"=="" set MUSICA=auto
if not "%MUSICA%"=="auto" echo Usare la carpeta: %MUSICA%
if "%MUSICA%"=="auto" echo Buscare la carpeta "musica" cerca de aqui, como hace la rocola.
echo Consejo: tambien puedes ARRASTRAR tu carpeta de musica sobre este .bat
echo.
echo --- PASO 1 de 2: juntando canciones de calidad completa ---
upscale\venv\Scripts\python preparar_entrenamiento.py --musica "%MUSICA%" --canciones 600
if errorlevel 1 (
  echo [X] No se pudo preparar el material. Revisa el mensaje de arriba.
  pause
  exit /b 1
)
echo.
echo --- PASO 2 de 2: entrenando la red ---
upscale\venv\Scripts\python entrenar_ia.py --musica entrenamiento_wavs --clips 7200 --por_archivo 12
if errorlevel 1 (
  echo [X] El entrenamiento fallo. Revisa el mensaje de arriba.
  pause
  exit /b 1
)
echo.
echo ==============================================================
echo  LISTO. El modelo entrenado quedo en:
echo    %~dp0rocola_ia.bin
echo  Copialo JUNTO a Rocola.exe y reinicia la rocola: el DSEE
echo  espectral usara la envolvente aprendida de TU musica.
echo  La carpeta entrenamiento_wavs ya se puede borrar si quieres.
echo ==============================================================
pause
