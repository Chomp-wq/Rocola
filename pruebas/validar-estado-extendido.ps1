param([string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-estado-extendido-' + [Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
$previousData = $env:ROCOLA_DATOS
try {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $out = (Resolve-Path -LiteralPath $OutputDirectory).Path
    $env:ROCOLA_DATOS = Join-Path $out 'datos-aislados'
    New-Item -ItemType Directory -Path $env:ROCOLA_DATOS -Force | Out-Null
    $repo = Split-Path -Parent $PSScriptRoot
    $source = Join-Path $repo 'Rocola'
    [xml]$project = Get-Content -LiteralPath (Join-Path $source 'Rocola.csproj')
    $sources = @($project.Project.ItemGroup.Compile | ForEach-Object { if ($_.Include) { Join-Path $source $_.Include } })
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    $naudio = Join-Path $repo 'packages\NAudio.1.9.0\lib\net35\NAudio.dll'
    $nlayer = Join-Path $repo 'packages\NLayer.1.12.0\lib\net35\NLayer.dll'
    Copy-Item -LiteralPath $naudio,$nlayer -Destination $out -Force
    $refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll',"/r:$naudio","/r:$nlayer")
    foreach ($mode in @('gpu','cpu')) {
        $define = if ($mode -eq 'gpu') { '/define:TRACE;ROCOLA_GPU' } else { '/define:TRACE' }
        $app = Join-Path $out ("Rocola-$mode.exe")
        & $compiler /nologo /target:winexe /optimize+ /warnaserror+ $define ("/out:$app") @refs ("/resource:$nlayer,Rocola.NLayer.dll") @sources
        if ($LASTEXITCODE -ne 0) { throw "No compila $mode" }
        $tests = if ($mode -eq 'gpu') {
            @('trabajovisible','campoestelarbuffers','dibujoligero','resolucionligera','estadoeficiente','performance','rendercache','franjareloj','panelreposo','fluidezcuadros','estrellaflujo','estrella3d','estelaestelar','ajustesvivos','fichaportada','caratulasjpeg','hojeofino','papeleraportadas','asistentecombo','asistentevisual','ayudaguia')
        } else { @('estadoeficiente','ajustesvivos','fichaportada') }
        foreach ($test in $tests) {
            $exe = Join-Path $out ("$test.exe")
            $support = @((Join-Path $PSScriptRoot 'leercodigo.cs'))
            $entry = @()
            if ($test -eq 'resolucionligera') {
                $support += Join-Path $PSScriptRoot 'performance.cs'
                $entry = @('/main:ResolucionLigera')
            }
            if ($test -eq 'trabajovisible') {
                $support += Join-Path $PSScriptRoot 'performance.cs'
                $entry = @('/main:TrabajoVisible')
            }
            if ($test -eq 'estadoeficiente') {
                $support += Join-Path $PSScriptRoot 'franjareloj.cs'
                $entry = @('/main:EstadoEficiente')
            }
            if ($test -eq 'rendercache') {
                $support += Join-Path $PSScriptRoot 'franjareloj.cs'
                $support += Join-Path $PSScriptRoot 'buscaportadas.cs'
                $entry = @('/main:RenderCache')
            }
            & $compiler /nologo /optimize+ /warnaserror+ ("/out:$exe") @refs @entry (Join-Path $PSScriptRoot "$test.cs") @support
            if ($LASTEXITCODE -ne 0) { throw "No compila $test" }
            Push-Location $PSScriptRoot
            try {
                & $exe $app | Tee-Object -FilePath (Join-Path $out ("$test-$mode.txt"))
                if ($LASTEXITCODE -ne 0) { throw "Fallo $test $mode" }
            } finally { Pop-Location }
        }
    }
    Write-Output "PASS: estado extendido, estrella y navegacion sintetica. Sin instalar, sin medir FPS reales ni audio. Salida: $out"
} finally { $env:ROCOLA_DATOS = $previousData }
