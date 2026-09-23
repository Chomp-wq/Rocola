param(
    [string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-audio-' + [Guid]::NewGuid().ToString('N'))),
    [string]$Python = 'python',
    [string]$Ffmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
$testRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $testRoot
& (Join-Path $testRoot 'validar-capas.ps1') -OutputDirectory $OutputDirectory
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
    foreach ($test in @('dsee','limite','entrenador','iaparidad','iaorigen','sonoridad','picoreal','realce','transiciones','sinpausa','salida','eqpreparado','iadatos','eqauto','calidad','nivel','bajoauto','bajo')) {
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
        if ($test -in @('dsee','limite','entrenador','iaparidad','iaorigen','sonoridad','picoreal','realce','transiciones')) { Push-Location $testRoot } else { Push-Location $out }
        try {
            & $exe $app
            if ($LASTEXITCODE -ne 0) { throw "Fallo $test" }
        } finally { Pop-Location }
    }
    Push-Location (Join-Path $repoRoot 'ia')
    try {
        & $env:ROCOLA_TEST_PYTHON -m unittest test_calidad_dsee test_entrenar_eq test_contrato_dsee test_origen_dsee test_estereo_dsee test_seleccion_dsee test_publicacion_dsee test_informe_dsee
        if ($LASTEXITCODE -ne 0) { throw 'Fallo Python/IA' }
    } finally { Pop-Location }
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
