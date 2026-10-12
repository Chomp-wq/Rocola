param([string]$OutputDirectory = (Join-Path $env:TEMP 'rocola-capas-validacion'), [switch]$SinHal)
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
[xml]$project = Get-Content (Join-Path $sourceRoot 'Rocola.csproj')
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
if (Test-Path -LiteralPath (Join-Path $repoRoot 'packages\NLayer.1.12.0\lib\net35\NLayer.dll')) {
    # Las pruebas por reflexion no llaman Main, que registra el resolver del recurso embebido.
    Copy-Item -LiteralPath $nlayer -Destination (Join-Path $OutputDirectory 'NLayer.dll') -Force
}
foreach ($mode in @('gpu', 'cpu')) {
    $defines = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
    $exe = Join-Path $OutputDirectory ('Rocola-' + $mode + '.exe')
    & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $defines ("/out:$exe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll ("/r:$naudio") @decoder $sources
    if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
}
$exe = Join-Path $OutputDirectory 'Rocola-gpu.exe'
$mp3Fixtures = Join-Path $OutputDirectory 'mp3-fixtures'
New-Item -ItemType Directory -Path $mp3Fixtures -Force | Out-Null
foreach ($name in @('relleno_p0','relleno_mono','relleno_vbr','relleno_48k','relleno_mpeg2')) {
    Copy-Item -LiteralPath (Join-Path $testRoot ($name+'.mp3')) -Destination $mp3Fixtures -Force
}
Push-Location $testRoot
try {
foreach ($test in @('capascolor', 'cristalcola', 'cubofila', 'respuesta', 'efectoslayout', 'reposounido', 'lunadetalle', 'estelaestelar', 'estrellaflujo', 'optimizaciones', 'eqpreparado', 'audiosuave', 'audioestereo', 'hojeoportadas', 'cargaadaptativa', 'escaneomemo', 'aperturaaudio', 'precarga', 'medidas', 'arranque', 'apollo', 'panelreposo', 'envios', 'revision20260921', 'cuboagrupado', 'etiqueta', 'id3selectivo', 'tablasarranque', 'historialarranque', 'paletaservicio', 'espacioprecarga', 'caratulasadaptativas')) {
    $testExe = Join-Path $OutputDirectory ($test + '.exe')
    & $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$testExe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot ($test + '.cs')) (Join-Path $testRoot 'leercodigo.cs')
    if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
    if ($test -eq 'cristalcola') { & $testExe $exe (Join-Path $sourceRoot 'Form1.Ajustes.cs') }
    elseif ($test -eq 'cubofila' -or $test -eq 'efectoslayout') { & $testExe $exe (Join-Path $OutputDirectory ($test + '.png')) }
    elseif ($test -eq 'caratulasadaptativas' -and $SinHal) { & $testExe $exe --sin-hal }
    else { & $testExe $exe }
    if ($LASTEXITCODE -ne 0) { throw "Fallo: $test" }
    if ($test -eq 'caratulasadaptativas') {
        & $testExe (Join-Path $OutputDirectory 'Rocola-cpu.exe') --sin-hal
        if ($LASTEXITCODE -ne 0) { throw "Fallo: perfiles comunes de caratulas (CPU)" }
    }
}
} finally { Pop-Location }
$searchExe = Join-Path $OutputDirectory 'buscainteligente.exe'
$assistantComboExe = Join-Path $OutputDirectory 'asistentecombo.exe'
& $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$assistantComboExe") (Join-Path $testRoot 'asistentecombo.cs') (Join-Path $testRoot 'leercodigo.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila gesto asistente' }
foreach ($mode in @('gpu', 'cpu')) {
    & $assistantComboExe (Join-Path $OutputDirectory ('Rocola-' + $mode + '.exe'))
    if ($LASTEXITCODE -ne 0) { throw "Fallo: asistente $mode" }
}
$assistantVisualExe = Join-Path $OutputDirectory 'asistentevisual.exe'
& $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$assistantVisualExe") /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $testRoot 'asistentevisual.cs') (Join-Path $testRoot 'leercodigo.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila asistente visual' }
$assistantVisualOutput = Join-Path $OutputDirectory 'asistente-visual'
if ($SinHal) { & $assistantVisualExe $exe $assistantVisualOutput --sin-hal } else { & $assistantVisualExe $exe $assistantVisualOutput }
if ($LASTEXITCODE -ne 0) { throw 'Fallo: asistente visual' }
$guideExe = Join-Path $OutputDirectory 'guiarojo.exe'
& $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$guideExe") /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $testRoot 'guiarojo.cs') (Join-Path $testRoot 'leercodigo.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila guia roja' }
Push-Location $testRoot
try { & $guideExe $exe; if ($LASTEXITCODE -ne 0) { throw 'Fallo: guia roja' } } finally { Pop-Location }
& $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$searchExe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $testRoot 'buscainteligente.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila buscainteligente' }
& $searchExe $exe
if ($LASTEXITCODE -ne 0) { throw 'Fallo: buscador GPU' }
& $searchExe (Join-Path $OutputDirectory 'Rocola-cpu.exe')
if ($LASTEXITCODE -ne 0) { throw 'Fallo: motor comun de busqueda (CPU)' }
$stutterExe = Join-Path $OutputDirectory 'tironescaratulas.exe'
foreach ($extraTest in @('colacompacta', 'ajustesfluidez', 'cadenciavisual', 'hojasfondo', 'estrella3d', 'ecosgeometria', 'estrellasesionreal', 'pausasestrella', 'audioeficiencia', 'datosdeltas', 'arranqueeficiente', 'fluidezcuadros', 'relojreposo', 'campoestelarbuffers', 'memoriaanalisis', 'memoriaintegrada', 'wavdirecto', 'planbiblioteca', 'corpusacotado', 'discoacotado', 'prefijosguardados', 'flotantesobrantes', 'ruidomalla', 'papeleraportadas', 'mp3medidaligera')) {
    $extraExe = Join-Path $OutputDirectory ($extraTest + '.exe')
    & $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$extraExe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $testRoot ($extraTest + '.cs')) (Join-Path $testRoot 'leercodigo.cs')
    if ($LASTEXITCODE -ne 0) { throw "No compila $extraTest" }
    if ($extraTest -eq 'estrellasesionreal') { & $extraExe --autoprueba $exe }
    else { & $extraExe $exe }
    if ($LASTEXITCODE -ne 0) { throw "Fallo: $extraTest" }
    if ($extraTest -eq 'ajustesfluidez' -or $extraTest -eq 'hojasfondo' -or $extraTest -eq 'memoriaanalisis' -or $extraTest -eq 'memoriaintegrada' -or $extraTest -eq 'wavdirecto') {
        & $extraExe (Join-Path $OutputDirectory 'Rocola-cpu.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Fallo: ajustes comunes (CPU)' }
    }
}
& $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$stutterExe") /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $testRoot 'tironescaratulas.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila tironescaratulas' }
if ($SinHal) { & $stutterExe $exe --sin-hal } else { & $stutterExe $exe }
if ($LASTEXITCODE -ne 0) { throw 'Fallo: tironescaratulas' }
$partsExe = Join-Path $OutputDirectory 'partescaratulas.exe'
& $compiler /nologo /target:exe /optimize+ /warnaserror+ ("/out:$partsExe") /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $testRoot 'partescaratulas.cs') (Join-Path $testRoot 'leercodigo.cs')
if ($LASTEXITCODE -ne 0) { throw 'No compila partescaratulas' }
if ($SinHal) { & $partsExe $exe --sin-hal } else { & $partsExe $exe }
if ($LASTEXITCODE -ne 0) { throw 'Fallo: partescaratulas' }
Write-Output "PASS: compilacion GPU/CPU y todas las baterias de la lista (Espacio natural retirado el 12-10-2026). HAL omitido explicitamente: $SinHal. No se reemplazo ningun ejecutable de produccion. Salida: $OutputDirectory"
} finally { $env:ROCOLA_DATOS = $previousRocolaData }
