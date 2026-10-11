param([string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-auditoria-general-' + [Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$previousData = $env:ROCOLA_DATOS
try {
    $repo = Split-Path -Parent $PSScriptRoot
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $out = (Resolve-Path -LiteralPath $OutputDirectory).Path
    $env:ROCOLA_DATOS = Join-Path $out ('datos-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $env:ROCOLA_DATOS | Out-Null
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    [xml]$project = Get-Content (Join-Path $repo 'Rocola\Rocola.csproj')
    $sources = @($project.Project.ItemGroup.Compile | ForEach-Object { if ($_.Include) { Join-Path (Join-Path $repo 'Rocola') $_.Include } })
    $naudio = Join-Path $repo 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
    $nlayer = Join-Path $repo 'packages\NLayer.1.12.0\lib\net35\NLayer.dll'
    Copy-Item -LiteralPath $naudio,$nlayer -Destination $out -Force
    $references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Xml.dll',"/r:$naudio","/r:$nlayer","/resource:$nlayer,Rocola.NLayer.dll")
    $auditor = Join-Path $out 'auditoriageneral.exe'
    & $compiler /nologo /optimize+ /warnaserror+ ("/out:$auditor") @references (Join-Path $PSScriptRoot 'auditoriageneral.cs') (Join-Path $PSScriptRoot 'leercodigo.cs')
    if ($LASTEXITCODE -ne 0) { throw 'No compila auditoria general' }
    foreach ($mode in @('gpu','cpu')) {
        $defines = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
        $app = Join-Path $out ("Rocola-$mode.exe")
        & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $defines ("/out:$app") @references $sources
        if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
        & $auditor $app | Tee-Object -FilePath (Join-Path $out ("auditoria-$mode.txt"))
        if ($LASTEXITCODE -ne 0) { throw "Fallo auditoria $mode" }
    }
    $tex = Join-Path $out 'auditoriatexturas.exe'
    & $compiler /nologo /optimize+ /warnaserror+ '/define:TRACE;ROCOLA_GPU' /main:Rocola.AuditoriaTexturas ("/out:$tex") @references $sources (Join-Path $PSScriptRoot 'auditoriatexturas.cs')
    if ($LASTEXITCODE -ne 0) { throw 'No compila driver artificial' }
    & $tex | Tee-Object -FilePath (Join-Path $out 'texturas.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Fallo texturas compatibles' }
    Write-Output "PASS auditoria general .NET 3.5 GPU/CPU y driver artificial. Sin HAL ni instalacion de produccion. Evidencia: $out"
} finally { $env:ROCOLA_DATOS = $previousData }
