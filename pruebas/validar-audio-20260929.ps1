param(
    [string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-audio29-' + [Guid]::NewGuid().ToString('N'))),
    [string]$Python = 'python',
    [string]$Ffmpeg = 'ffmpeg',
    [string[]]$Tests = @('iageneros', 'dsporiginal', 'salidacontinua', 'latenciaadaptable'),
    [switch]$BuildOnly
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
$sourceRoot = Join-Path $repoRoot 'Rocola'
[xml]$project = Get-Content -LiteralPath (Join-Path $sourceRoot 'Rocola.csproj')
$framework = if ($project.Project.PropertyGroup.TargetFrameworkVersion -contains 'v3.5') { 'v3.5' } else { 'v4.0.30319' }
$compiler = Join-Path $env:WINDIR ('Microsoft.NET\Framework\' + $framework + '\csc.exe')
$sources = @($project.Project.ItemGroup.Compile | ForEach-Object { if ($_.Include) { Join-Path $sourceRoot $_.Include } })
$naudio = Join-Path $repoRoot 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
$decoder = @()
if (Test-Path -LiteralPath (Join-Path $repoRoot 'packages\NLayer.1.12.0\lib\net35\NLayer.dll')) {
    $nlayer = Join-Path $repoRoot 'packages\NLayer.1.12.0\lib\net35\NLayer.dll'
    $decoder = @("/r:$nlayer", "/resource:$nlayer,Rocola.NLayer.dll")
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
Copy-Item -LiteralPath $naudio -Destination (Join-Path $OutputDirectory 'NAudio.dll') -Force
if (Test-Path -LiteralPath (Join-Path $repoRoot 'packages\NLayer.1.12.0\lib\net35\NLayer.dll')) { Copy-Item -LiteralPath $nlayer -Destination (Join-Path $OutputDirectory 'NLayer.dll') -Force }
foreach ($mode in @('gpu', 'cpu')) {
    $defines = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
    & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $defines ("/out:" + (Join-Path $OutputDirectory ('Rocola-' + $mode + '.exe'))) /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ("/r:$naudio") @decoder $sources
    if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
}
$app = Join-Path $OutputDirectory 'Rocola-gpu.exe'
& $compiler /nologo /optimize+ /warnaserror+ /r:System.Core.dll ("/r:$naudio") ("/out:" + (Join-Path $OutputDirectory 'renderdsee.exe')) (Join-Path $testRoot 'renderdsee.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila renderer PCM' }
if ($BuildOnly) { Write-Output "BUILD PASS: $OutputDirectory"; return }
$previousPython = $env:ROCOLA_TEST_PYTHON
$previousRenderer = $env:ROCOLA_PCM_RENDERER
$previousAssembly = $env:ROCOLA_PCM_ASSEMBLY
$previousModel = $env:ROCOLA_PCM_MODEL
try {
    $env:ROCOLA_TEST_PYTHON = (Get-Command $Python -ErrorAction Stop).Source
    $env:ROCOLA_PCM_RENDERER = Join-Path $OutputDirectory 'renderdsee.exe'
    $env:ROCOLA_PCM_ASSEMBLY = $app
    $env:ROCOLA_PCM_MODEL = Join-Path $repoRoot 'ia\rocola_ia.bin'
    Push-Location $testRoot
    try {
        foreach ($test in $Tests) {
            $testExe = Join-Path $OutputDirectory ($test + '.exe')
            & $compiler /nologo /optimize+ /warnaserror+ /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") ("/out:$testExe") (Join-Path $testRoot ($test + '.cs')) (Join-Path $testRoot 'leercodigo.cs')
            if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
            & $testExe $app
            if ($LASTEXITCODE -ne 0) { throw "Fallo $test" }
        }
    } finally { Pop-Location }
    # Probar el ejecutable distribuible sin la DLL externa que necesita la reflexion.
    $embedded = Join-Path $OutputDirectory ('nlayer-integrado-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $embedded | Out-Null
    Copy-Item -LiteralPath $app -Destination $embedded
    Copy-Item -LiteralPath $naudio -Destination $embedded
    $embeddedTest = Join-Path $embedded 'nlayerincluido.exe'
    & $compiler /nologo /optimize+ /warnaserror+ ("/r:$naudio") ("/out:$embeddedTest") (Join-Path $testRoot 'nlayerincluido.cs')
    if ($LASTEXITCODE -ne 0) { throw 'No compila NLayer integrado' }
    & $embeddedTest (Join-Path $embedded 'Rocola-gpu.exe') (Join-Path $testRoot 'flotante_overs.mp3')
    if ($LASTEXITCODE -ne 0) { throw 'Fallo NLayer integrado' }
    $smoke = Join-Path $OutputDirectory ('pcm-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $smoke | Out-Null
    $fixture = Join-Path $smoke 'dsee-entrada.wav'
    $render = Join-Path $smoke 'dsee-salida.wav'
    & $Ffmpeg -hide_banner -loglevel error -nostdin -n -f lavfi -i 'anoisesrc=color=white:amplitude=0.08:sample_rate=44100:duration=3:seed=421' -af 'lowpass=f=12000' -ac 2 -c:a pcm_f32le $fixture
    if ($LASTEXITCODE -ne 0) { throw 'No genera fixture WAV' }
    & (Join-Path $OutputDirectory 'renderdsee.exe') $app (Join-Path $repoRoot 'ia\rocola_ia.bin') $fixture $render
    if ($LASTEXITCODE -ne 0) { throw 'Fallo render PCM' }
    Push-Location (Join-Path $repoRoot 'ia')
    try {
        & $env:ROCOLA_TEST_PYTHON -m unittest discover -p 'test_*.py'
        if ($LASTEXITCODE -ne 0) { throw 'Fallo pruebas Python compartidas' }
    } finally { Pop-Location }
    $upscale = Join-Path $repoRoot 'ia\upscale'
    if (Test-Path -LiteralPath $upscale) {
        Push-Location $upscale
        try {
            & $env:ROCOLA_TEST_PYTHON -m unittest discover -p 'test_*.py'
            if ($LASTEXITCODE -ne 0) { throw 'Fallo pruebas Apollo' }
        } finally { Pop-Location }
    }
} finally {
    $env:ROCOLA_TEST_PYTHON = $previousPython
    $env:ROCOLA_PCM_RENDERER = $previousRenderer
    $env:ROCOLA_PCM_ASSEMBLY = $previousAssembly
    $env:ROCOLA_PCM_MODEL = $previousModel
}
Write-Output "PASS audio29: $OutputDirectory. Solo datos sinteticos/temporales; no prueba XP, CPU antiguo ni DAC."
} finally { $env:ROCOLA_DATOS = $previousRocolaData }
