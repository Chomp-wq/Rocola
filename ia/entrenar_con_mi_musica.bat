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
echo   2. Entrena la red y deja rocola_ia.bin en esta carpeta.
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
"%PY%" "%~dp0entrenar_ia.py" --musica "%~dp0entrenamiento_wavs" --clips 7200 --por_archivo 12
set "COD=%ERRORLEVEL%"
rem 2 y 3 NO son fallos: el entrenamiento salio bien y decidio, con razon, no cambiar nada.
if "%COD%"=="2" (
  echo.
  echo  No se guardo ningun modelo nuevo: no le gano a la curva fija y al modelo que ya tenias
  echo  por el margen que se exige. No es un fallo. Tu rocola_ia.bin sigue igual.
  echo  Los numeros estan en informe_dsee.json, en esta carpeta.
  pause
  endlocal & exit /b 2
)
if "%COD%"=="3" (
  echo.
  echo  No se guardo ningun modelo nuevo: no hay canciones suficientes para validar con musica
  echo  que la red no haya visto. No es un fallo. Tu rocola_ia.bin sigue igual.
  echo  Junta mas canciones con el PASO 1 y vuelve a correr este archivo.
  pause
  endlocal & exit /b 3
)
if not "%COD%"=="0" (
  echo [X] El entrenamiento fallo. Revisa el mensaje de arriba. Tu rocola_ia.bin sigue igual.
  pause
  endlocal & exit /b %COD%
)
echo.
echo ==============================================================
echo  LISTO. El modelo entrenado quedo en:
echo    %~dp0rocola_ia.bin
echo  Copialo JUNTO a Rocola.exe y reinicia la rocola: el DSEE
echo  espectral usara la envolvente aprendida de TU musica.
echo  Queda tambien guardado como version en la carpeta modelos, con su
echo  informe, para poder volver atras con publicar_dsee.py.
echo  La carpeta entrenamiento_wavs ya se puede borrar si quieres.
echo ==============================================================
pause
endlocal & exit /b 0
