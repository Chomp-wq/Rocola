param([string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-mejoras-29-' + [Guid]::NewGuid().ToString('N'))), [string[]]$Tests)
$ErrorActionPreference = 'Stop'
$testRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $testRoot
$sourceRoot = Join-Path $repoRoot 'Rocola'
[xml]$project = Get-Content (Join-Path $sourceRoot 'Rocola.csproj')
$framework = if ($project.Project.PropertyGroup.TargetFrameworkVersion -contains 'v3.5') { 'v3.5' } else { 'v4.0.30319' }
$compiler = Join-Path $env:WINDIR ('Microsoft.NET\Framework\' + $framework + '\csc.exe')
$sources = @($project.Project.ItemGroup.Compile | ForEach-Object { if ($_.Include) { Join-Path $sourceRoot $_.Include } })
$naudio = Join-Path $repoRoot 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
$decoder = @()
if ($framework -eq 'v3.5') {
    $nlayer = Join-Path $repoRoot 'packages\NLayer.1.12.0\lib\net35\NLayer.dll'
    $decoder = @("/r:$nlayer", "/resource:$nlayer,Rocola.NLayer.dll")
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
Copy-Item -LiteralPath $naudio -Destination (Join-Path $OutputDirectory 'NAudio.dll') -Force
foreach ($mode in @('gpu', 'cpu')) {
    $defines = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
    & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $defines ("/out:" + (Join-Path $OutputDirectory ('Rocola-' + $mode + '.exe'))) /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ("/r:$naudio") @decoder $sources
    if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
}
$app = Join-Path $OutputDirectory 'Rocola-gpu.exe'
if (!$Tests) { $Tests = @('fuentes','planbiblioteca','cierrebomba','bomba','sinpausa','diario','otroarchivo','silencios','nivelpicos','nofinitos','tablasarranque','historialarranque','etiquetadelante','enmemoria','tirones','panelreposo','textosgpu','franjareloj','buscaportadas','bloqueos','cristalcache','paginasgpu') }
Push-Location $testRoot
try {
    foreach ($test in $Tests) {
        $exe = Join-Path $OutputDirectory ($test + '.exe')
        & $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$exe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot ($test + '.cs')) (Join-Path $testRoot 'leercodigo.cs')
        if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
        & $exe $app
        if ($LASTEXITCODE -ne 0) { throw "Fallo $test" }
    }
    $render = Join-Path $OutputDirectory 'rendercache.exe'
    & $compiler /nologo /target:exe /optimize+ /warnaserror+ /main:RenderCache ("/out:$render") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot 'rendercache.cs') (Join-Path $testRoot 'franjareloj.cs') (Join-Path $testRoot 'buscaportadas.cs') (Join-Path $testRoot 'leercodigo.cs')
    if ($LASTEXITCODE -ne 0) { throw 'No compila rendercache' }
    & $render $app
    if ($LASTEXITCODE -ne 0) { throw 'Fallo rendercache' }
} finally { Pop-Location }
Write-Output "PASS: perfiles GPU/CPU y regresiones. No se reemplazo ningun ejecutable ni datos de produccion. $OutputDirectory"
