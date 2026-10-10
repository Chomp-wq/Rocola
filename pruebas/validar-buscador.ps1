param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [string]$CpuExecutable,
    [string]$OutputDirectory = (Join-Path $env:TEMP ('rocola-buscador-' + [Guid]::NewGuid().ToString('N'))),
    [switch]$DosHilos
)
$ErrorActionPreference = 'Stop'
$previousData = $env:ROCOLA_DATOS
$previousAffinity = (Get-Process -Id $PID).ProcessorAffinity
try {
    if ($DosHilos) {
        # Limitar a DOS procesadores logicos disponibles, no inventar un CPU antiguo.
        $available = $previousAffinity.ToInt64()
        [long]$selected = 0
        $count = 0
        for ($bit = 0; $bit -lt 63 -and $count -lt 2; $bit++) {
            [long]$flag = [long]1 -shl $bit
            if (($available -band $flag) -ne 0) { $selected = $selected -bor $flag; $count++ }
        }
        (Get-Process -Id $PID).ProcessorAffinity = [IntPtr]$selected
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $out = (Resolve-Path -LiteralPath $OutputDirectory).Path
    $data = Join-Path $out ('datos-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $data | Out-Null
    $env:ROCOLA_DATOS = $data
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    foreach ($platform in @('anycpu','x86')) {
        $testExe = Join-Path $out ('buscainteligente-' + $platform + '.exe')
        & $compiler /nologo /optimize+ /warnaserror+ ("/platform:$platform") ("/out:$testExe") /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'buscainteligente.cs')
        if ($LASTEXITCODE -ne 0) { throw 'No compila buscainteligente' }
        foreach ($app in @($Executable, $CpuExecutable)) {
            if (-not $app) { continue }
            $resolved = (Resolve-Path -LiteralPath $app).Path
            $log = Join-Path $out ((Split-Path -Leaf $resolved) + '-' + $platform + '.txt')
            $arguments = @($resolved)
            if ($DosHilos) { $arguments += '--dos-hilos' }
            & $testExe @arguments | Tee-Object -FilePath $log
            if ($LASTEXITCODE -ne 0) { throw "Fallo buscador $resolved / $platform" }
        }
    }
    Write-Output "PASS buscador: AnyCPU/x86, .NET 3.5; dos hilos logicos: $DosHilos. No mide FPS ni acredita XP/CPU antiguo. Evidencia: $out"
} finally {
    $env:ROCOLA_DATOS = $previousData
    (Get-Process -Id $PID).ProcessorAffinity = $previousAffinity
}
