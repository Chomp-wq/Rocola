param([string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-revision-octubre-' + [Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$previousData = $env:ROCOLA_DATOS
try {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
    $env:ROCOLA_DATOS = Join-Path $OutputDirectory 'datos-aislados'
    New-Item -ItemType Directory -Path $env:ROCOLA_DATOS -Force | Out-Null
    $repo = Split-Path -Parent $PSScriptRoot
    $source = Join-Path $repo 'Rocola'
    [xml]$project = Get-Content -LiteralPath (Join-Path $source 'Rocola.csproj')
    $sources = @($project.Project.ItemGroup.Compile | ForEach-Object { if ($_.Include) { Join-Path $source $_.Include } })
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    $naudio = Join-Path $repo 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
    $nlayer = Join-Path $repo 'packages\NLayer.1.12.0\lib\net35\NLayer.dll'
    Copy-Item -LiteralPath $naudio,$nlayer -Destination $OutputDirectory -Force
    $refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll',"/r:$naudio","/r:$nlayer")
    foreach ($mode in @('gpu','cpu')) {
        $define = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
        $app = Join-Path $OutputDirectory ("Rocola-$mode.exe")
        & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $define ("/out:$app") @refs /r:System.Xml.dll ("/resource:$nlayer,Rocola.NLayer.dll") @sources
        if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
        foreach ($test in @('ajustesvivos','iasdisparo','subgrave','gravemono','caratulasjpeg','fotos1013','asistentecombo','hojeofino','mp3ligero')) {
            $exe = Join-Path $OutputDirectory ("$test.exe")
            & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") @refs (Join-Path $PSScriptRoot "$test.cs") (Join-Path $PSScriptRoot 'leercodigo.cs')
            if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
            Push-Location $PSScriptRoot
            try {
                & $exe $app | Tee-Object -FilePath (Join-Path $OutputDirectory ("$test-$mode.txt"))
                if ($LASTEXITCODE -ne 0) { throw "Fallo $test $mode" }
            } finally { Pop-Location }
        }
    }
    Write-Output "PASS: regresiones de octubre en .NET 3.5 GPU/CPU. Datos aislados, sin instalar ni medir FPS/XP. Salida: $OutputDirectory"
} finally { $env:ROCOLA_DATOS = $previousData }
