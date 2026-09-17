param([Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$Ffmpeg = 'ffmpeg')
$ErrorActionPreference = 'Stop'
$out = (Resolve-Path $OutputDirectory).Path
$test = Join-Path $out 'aperturaaudio.exe'
$app = Join-Path $out 'Rocola-gpu.exe'
if (!(Test-Path $test) -or !(Test-Path $app)) { throw 'Ejecutar validar-capas.ps1 primero.' }
$fixtures = Join-Path $env:TEMP ('rocola-codecs-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures | Out-Null
# Senales sinteticas; no se lee ni modifica la biblioteca del usuario.
foreach ($codec in @(@('cbr.mp3','libmp3lame','320k'), @('audio.wma','wmav2','192k'), @('audio.m4a','aac','192k'))) {
    & $Ffmpeg -hide_banner -loglevel error -nostdin -n -f lavfi -i 'sine=frequency=523:sample_rate=44100:duration=2' -ac 2 -c:a $codec[1] -b:a $codec[2] (Join-Path $fixtures $codec[0])
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el audio de prueba.' }
}
& $Ffmpeg -hide_banner -loglevel error -nostdin -n -f lavfi -i 'sine=frequency=880:sample_rate=48000:duration=2' -ac 1 -c:a libmp3lame -q:a 3 (Join-Path $fixtures 'vbr.mp3')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar MP3 VBR.' }
& $test $app $fixtures
if ($LASTEXITCODE -ne 0) { throw 'Fallo de apertura/PCM en los formatos adicionales.' }
Write-Output "Senales de prueba conservadas en $fixtures"
