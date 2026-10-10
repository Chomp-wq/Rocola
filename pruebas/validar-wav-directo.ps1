param(
    [string]$ApplicationPath,
    [string]$BaselinePath,
    [string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-wav-validacion-' + [Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$testRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $testRoot
if (!$ApplicationPath) { $ApplicationPath = Join-Path $repoRoot 'Rocola\bin\Release\Rocola.exe' }
$ApplicationPath = (Resolve-Path -LiteralPath $ApplicationPath).Path
if ($BaselinePath) { $BaselinePath = (Resolve-Path -LiteralPath $BaselinePath).Path }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
[xml]$project = Get-Content -LiteralPath (Join-Path $repoRoot 'Rocola\Rocola.csproj')
$framework = if ($project.Project.PropertyGroup.TargetFrameworkVersion -contains 'v3.5') { 'v3.5' } else { 'v4.0.30319' }
$compiler = Join-Path $env:WINDIR ('Microsoft.NET\Framework\' + $framework + '\csc.exe')
$naudio = Join-Path $repoRoot 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
Copy-Item -LiteralPath $naudio -Destination (Join-Path $OutputDirectory 'NAudio.dll') -Force
$previousRocolaData = $env:ROCOLA_DATOS
try {
    $env:ROCOLA_DATOS = Join-Path $OutputDirectory ('datos-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $env:ROCOLA_DATOS | Out-Null
    foreach ($architecture in @('anycpu', 'x86')) {
        $exe = Join-Path $OutputDirectory ('wavdirecto-' + $architecture + '.exe')
        & $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/platform:$architecture") ("/out:$exe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot 'wavdirecto.cs') (Join-Path $testRoot 'leercodigo.cs')
        if ($LASTEXITCODE -ne 0) { throw "No compila WAV ($architecture)" }
        if ($BaselinePath) { & $exe $ApplicationPath $BaselinePath } else { & $exe $ApplicationPath }
        if ($LASTEXITCODE -ne 0) { throw "Fallo WAV ($architecture)" }
    }
    Write-Output "PASS WAV directo, nativo/x86. Evidencia: $OutputDirectory. No reproduce musica ni mide FPS; no certifica XP real."
} finally { $env:ROCOLA_DATOS = $previousRocolaData }
