param([string]$OutputDirectory = (Join-Path $env:TEMP 'rocola-capas-validacion'))
$ErrorActionPreference = 'Stop'
$testRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $testRoot
$sourceRoot = Join-Path $repoRoot 'Rocola'
[xml]$project = Get-Content (Join-Path $sourceRoot 'Rocola.csproj')
$framework = if ($project.Project.PropertyGroup.TargetFrameworkVersion -contains 'v3.5') { 'v3.5' } else { 'v4.0.30319' }
$compiler = Join-Path $env:WINDIR ('Microsoft.NET\Framework\' + $framework + '\csc.exe')
$sources = @($project.Project.ItemGroup.Compile | ForEach-Object { if ($_.Include) { Join-Path $sourceRoot $_.Include } })
$naudio = Join-Path $repoRoot 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
Copy-Item -LiteralPath $naudio -Destination (Join-Path $OutputDirectory 'NAudio.dll') -Force
foreach ($mode in @('gpu', 'cpu')) {
    $defines = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
    $exe = Join-Path $OutputDirectory ('Rocola-' + $mode + '.exe')
    & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $defines ("/out:$exe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ("/r:$naudio") $sources
    if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
}
$exe = Join-Path $OutputDirectory 'Rocola-gpu.exe'
Push-Location $testRoot
try {
foreach ($test in @('capascolor', 'cristalcola', 'cubofila', 'respuesta', 'efectoslayout', 'reposounido', 'lunadetalle', 'optimizaciones', 'eqpreparado', 'hojeoportadas', 'cargaadaptativa', 'escaneomemo', 'aperturaaudio', 'precarga', 'medidas', 'arranque', 'apollo', 'panelreposo', 'envios', 'revision20260921', 'cuboagrupado', 'etiqueta', 'id3selectivo')) {
    $testExe = Join-Path $OutputDirectory ($test + '.exe')
    & $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$testExe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot ($test + '.cs')) (Join-Path $testRoot 'leercodigo.cs')
    if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
    if ($test -eq 'cristalcola') { & $testExe $exe (Join-Path $sourceRoot 'Form1.Ajustes.cs') }
    elseif ($test -eq 'cubofila' -or $test -eq 'efectoslayout') { & $testExe $exe (Join-Path $OutputDirectory ($test + '.png')) }
    else { & $testExe $exe }
    if ($LASTEXITCODE -ne 0) { throw "Fallo: $test" }
}
} finally { Pop-Location }
Write-Output "PASS: compilacion GPU/CPU y veintitres baterias. No se reemplazo ningun ejecutable de produccion. Salida: $OutputDirectory"
