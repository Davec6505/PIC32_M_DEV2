param(
    [string]$project
)

function Get-CandidatePaths {
    @(
        'C:\Program Files\Microchip\MccStandalone-*\startMcc*',
        'C:\Program Files (x86)\Microchip\MccStandalone-*\startMcc*'
    )
}

function Resolve-Mcc {
    foreach ($n in 'startMcc','mcc') {
        $cmd = Get-Command $n -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    $c = foreach ($p in Get-CandidatePaths) { Get-ChildItem $p -File -ErrorAction SilentlyContinue }
    if (-not $c) { return $null }
    ($c | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}

$exe = Resolve-Mcc
if (-not $exe) { Write-Error "MCC not found."; exit 1 }

$argList = @()
if ($project -and (Test-Path $project)) { $argList += $project }

& $exe @argList
exit $LASTEXITCODE