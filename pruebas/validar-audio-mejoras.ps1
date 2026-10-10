param(
    [string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-audio-' + [Guid]::NewGuid().ToString('N'))),
    [string]$Python = 'python',
    [string]$Ffmpeg = 'ffmpeg',
    [switch]$SkipLayers
)
$ErrorActionPreference = 'Stop'
$previousRocolaData = $env:ROCOLA_DATOS
try {
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
$isolatedRocolaData = Join-Path $OutputDirectory ('datos-pruebas-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $isolatedRocolaData | Out-Null
$env:ROCOLA_DATOS = $isolatedRocolaData
$testRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $testRoot
if ($SkipLayers) {
    # Repetir audio sin repetir las capas ya verificadas; siempre recompila las fuentes actuales.
    & (Join-Path $testRoot 'validar-audio-20260929.ps1') -OutputDirectory $OutputDirectory -BuildOnly
} else {
    & (Join-Path $testRoot 'validar-capas.ps1') -OutputDirectory $OutputDirectory
}
$out = (Resolve-Path $OutputDirectory).Path
[xml]$project = Get-Content (Join-Path $repoRoot 'Rocola\Rocola.csproj')
$framework = if ($project.Project.PropertyGroup.TargetFrameworkVersion -contains 'v3.5') { 'v3.5' } else { 'v4.0.30319' }
$compiler = Join-Path $env:WINDIR ('Microsoft.NET\Framework\' + $framework + '\csc.exe')
$naudio = Join-Path $repoRoot 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
$app = Join-Path $out 'Rocola-gpu.exe'
Copy-Item -LiteralPath $app -Destination (Join-Path $out 'rocola.exe') -Force
$previousPython = $env:ROCOLA_TEST_PYTHON
try {
    $env:ROCOLA_TEST_PYTHON = (Get-Command $Python -ErrorAction Stop).Source
    foreach ($test in @('dsee','limite','entrenador','iaparidad','iaorigen','sonoridad','picoreal','realce','transiciones','sinpausa','salida','videoaudio','eqpreparado','iadatos','eqauto','calidad','nivel','bajoauto','bajo','noche','gravemono','nivelpicos','soltar','relleno','silencios','todojunto','discoparejo','bomba','diario','enmemoria','memoriaanalisis','memoriaintegrada')) {
        $exe = Join-Path $out ($test + '.exe')
        & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot ($test+'.cs')) (Join-Path $testRoot 'leercodigo.cs')
        if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
        if ($test -eq 'iadatos') {
            $wav = Join-Path $out 'wav'
            New-Item -ItemType Directory -Force -Path $wav | Out-Null
            $fixture = Join-Path $wav ('white-' + [Guid]::NewGuid().ToString('N') + '.wav')
            & $Ffmpeg -hide_banner -loglevel error -nostdin -n -f lavfi -i 'anoisesrc=color=white:amplitude=0.12:sample_rate=44100:duration=12:seed=1234' -ac 2 -c:a pcm_s16le $fixture
            if ($LASTEXITCODE -ne 0) { throw 'No genera WAV de prueba' }
        }
        if ($test -in @('dsee','limite','entrenador','iaparidad','iaorigen','sonoridad','picoreal','realce','transiciones','relleno','enmemoria')) { Push-Location $testRoot } else { Push-Location $out }
        try {
            & $exe $app
            if ($LASTEXITCODE -ne 0) { throw "Fallo $test" }
        } finally { Pop-Location }
    }
    Push-Location (Join-Path $repoRoot 'ia')
    try {
        & $env:ROCOLA_TEST_PYTHON -m unittest test_calidad_dsee test_entrenar_eq test_contrato_dsee test_origen_dsee test_estereo_dsee test_seleccion_dsee test_publicacion_dsee test_informe_dsee test_candidatos_audio
        if ($LASTEXITCODE -ne 0) { throw 'Fallo Python/IA' }
    } finally { Pop-Location }
    # Remuestreo de Apollo (solo HD tiene ia\upscale): eleccion probada, calidad medida con el
    # FFmpeg del PATH, 44,1 kHz bit a bit, cache versionada y reaprovechamiento.
    $upscale = Join-Path $repoRoot 'ia\upscale'
    if (Test-Path -LiteralPath (Join-Path $upscale 'test_remuestreo.py')) {
        Push-Location $upscale
        try {
            & $env:ROCOLA_TEST_PYTHON -m unittest test_remuestreo
            if ($LASTEXITCODE -ne 0) { throw 'Fallo Python/remuestreo de Apollo' }
        } finally { Pop-Location }
    }
    # Remuestreador de la salida moderna (solo HD tiene SalidaRemuestreada). En Windows mide el DMO de verdad:
    # respuesta, plegado, imagenes, retraso, duracion, final, canales y coste, con la calidad de siempre y la 60.
    $remuestreo = Join-Path $testRoot 'remuestreo.cs'
    if (Test-Path -LiteralPath $remuestreo) {
        $exe = Join-Path $out 'remuestreo.exe'
        & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") $remuestreo
        if ($LASTEXITCODE -ne 0) { throw 'No compila remuestreo' }
        Push-Location $out
        try {
            & $exe $app
            if ($LASTEXITCODE -ne 0) { throw 'Fallo remuestreo' }
        } finally { Pop-Location }
    }
    # WASAPI exclusivo (solo HD tiene SalidaExclusiva.cs): el formato que ve Windows, la negociacion, la conversion,
    # el volumen de Windows, las caidas al compartido y el cableado, con un aparato de mentira. Que un aparato de
    # verdad lo admita y suene bien se comprueba en el equipo del bar.
    $exclusivo = Join-Path $testRoot 'exclusivo.cs'
    if (Test-Path -LiteralPath $exclusivo) {
        $exe = Join-Path $out 'exclusivo.exe'
        & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") $exclusivo (Join-Path $testRoot 'leercodigo.cs')
        if ($LASTEXITCODE -ne 0) { throw 'No compila exclusivo' }
        Push-Location $out
        try {
            & $exe $app
            if ($LASTEXITCODE -ne 0) { throw 'Fallo exclusivo' }
        } finally { Pop-Location }
    }
    # Decodificacion en coma flotante (solo HD tiene DecodificaFlotante.cs): la logica con decodificadores de mentira y,
    # aqui en Windows, el decodificador de MP3 de Windows DE VERDAD contra el de siempre con pruebas\flotante_overs.mp3
    # (la comparacion, cuanto pasa de 1,0 y lo que cuesta). Corre en la carpeta de pruebas, donde esta ese MP3.
    $flotante = Join-Path $testRoot 'flotante.cs'
    if (Test-Path -LiteralPath $flotante) {
        $exe = Join-Path $out 'flotante.exe'
        & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") $flotante (Join-Path $testRoot 'leercodigo.cs')
        if ($LASTEXITCODE -ne 0) { throw 'No compila flotante' }
        Push-Location $testRoot
        try {
            & $exe $app
            if ($LASTEXITCODE -ne 0) { throw 'Fallo flotante' }
        } finally { Pop-Location }
    }
    if ($SkipLayers) {
        # Estos ejecutables normalmente los crean las capas. El modo audio tambien funciona en un TEMP nuevo.
        foreach ($test in @('audiosuave','audioestereo','aperturaaudio')) {
            $exe = Join-Path $out ($test + '.exe')
            & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot ($test + '.cs')) (Join-Path $testRoot 'leercodigo.cs')
            if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
        }
    }
    & (Join-Path $testRoot 'validar-audio-formatos.ps1') -OutputDirectory $out -Ffmpeg $Ffmpeg
    # Solo este proceso de pruebas y sus hijos. No cambia afinidad del sistema/rocola instalada.
    $process = [System.Diagnostics.Process]::GetCurrentProcess()
    $previousAffinity = $process.ProcessorAffinity
    try {
        $mask = $previousAffinity.ToInt64()
        [long]$limited = 0
        $count = 0
        for ($bit = 0; $bit -lt 63 -and $count -lt 2; $bit++) {
            [long]$flag = [long]1 -shl $bit
            if (($mask -band $flag) -ne 0) { $limited = $limited -bor $flag; $count++ }
        }
        if ($count -gt 0) { $process.ProcessorAffinity = [IntPtr]$limited }
        foreach ($test in @('audiosuave','audioestereo')) {
            & (Join-Path $out ($test+'.exe')) $app
            if ($LASTEXITCODE -ne 0) { throw "Fallo con afinidad limitada: $test" }
        }
    } finally { $process.ProcessorAffinity = $previousAffinity }
} finally { $env:ROCOLA_TEST_PYTHON = $previousPython }
Write-Output "PASS audio: controles, huellas, DSEE, limitador, cosecha, entrenadores y formatos. Salida: $out"
Write-Output 'Afinidad a dos procesadores logicos NO equivale a probar un CPU antiguo, XP ni el DAC.'
} finally { $env:ROCOLA_DATOS = $previousRocolaData }
