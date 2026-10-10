param(
    [string]$ApplicationPath = (Join-Path $PSScriptRoot '..\Rocola\bin\Release\Rocola.exe'),
    [string]$BaselinePath,
    [string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-recursos-validacion-' + [Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$previousData = $env:ROCOLA_DATOS
try {
    $repo = Split-Path -Parent $PSScriptRoot
    $app = (Resolve-Path -LiteralPath $ApplicationPath).Path
    $reference = if ($BaselinePath) { (Resolve-Path -LiteralPath $BaselinePath).Path } else { $null }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $out = (Resolve-Path -LiteralPath $OutputDirectory).Path
    $data = Join-Path $out ('datos-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $data | Out-Null
    $env:ROCOLA_DATOS = $data
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    $naudio = Join-Path $repo 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
    Copy-Item -LiteralPath $naudio -Destination (Join-Path $out 'NAudio.dll') -Force
    Copy-Item -LiteralPath (Join-Path $repo 'packages\NLayer.1.12.0\lib\net35\NLayer.dll') -Destination (Join-Path $out 'NLayer.dll') -Force
    $mp3Fixtures = Join-Path $out 'mp3-fixtures'
    New-Item -ItemType Directory -Path $mp3Fixtures -Force | Out-Null
    foreach ($name in @('relleno_p0','relleno_mono','relleno_vbr','relleno_48k','relleno_mpeg2')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ($name+'.mp3')) -Destination $mp3Fixtures -Force
    }
    if (-not $reference) { Write-Output 'Sin referencia anterior: contrato y reutilizacion; NO equivalencia antes/despues.' }
    foreach ($platform in @('anycpu','x86')) {
        foreach ($test in @('corpusacotado','discoacotado','prefijosguardados','flotantesobrantes','planbiblioteca','arranqueeficiente','ruidomalla','papeleraportadas','mp3medidaligera')) {
            $exe = Join-Path $out ($test + '-' + $platform + '.exe')
            & $compiler /nologo /optimize+ /warnaserror+ ("/platform:$platform") ("/out:$exe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ("/r:$naudio") (Join-Path $PSScriptRoot ($test + '.cs')) (Join-Path $PSScriptRoot 'leercodigo.cs')
            if ($LASTEXITCODE -ne 0) { throw "No compila $test/$platform" }
            $arguments = @($app)
            if ($reference -and $test -in @('flotantesobrantes','ruidomalla','mp3medidaligera')) { $arguments += $reference }
            & $exe @arguments | Tee-Object -FilePath (Join-Path $out ($test + '-' + $platform + '.txt'))
            if ($LASTEXITCODE -ne 0) { throw "Fallo $test/$platform" }
        }
    }
    Write-Output "PASS recursos Modest: 9 baterias AnyCPU y x86; archivos artificiales y sin HAL/FPS/XP real. Evidencia: $out"
} finally { $env:ROCOLA_DATOS = $previousData }
