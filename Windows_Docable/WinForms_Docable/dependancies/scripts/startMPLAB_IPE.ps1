

param (
    [string]$project
)

function Get-CandidatePaths {
    @(
        'C:\Program Files\Microchip\MPLABX\v*\mplab_platform\bin\mplab_ipe64.exe',
        'C:\Program Files (x86)\Microchip\MPLABX\v*\mplab_platform\bin\mplab_ipe64.exe'
    )
}

function Resolve-MplabIpe {
    foreach ($n in 'mplab_ipe64','mplab_ipe') {
        $cmd = Get-Command $n -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    $c = foreach ($p in Get-CandidatePaths) { Get-ChildItem $p -File -ErrorAction SilentlyContinue }
    if (-not $c) { return $null }
    ($c | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}

$exe = Resolve-MplabIpe
if (-not $exe) {
    Write-Error "MPLAB IPE executable not found. Ensure it's in PATH (mplab_ipe) or installed under /opt/microchip/mplabx (Linux) or C:\Program Files\Microchip\MPLABX (Windows)."
    exit 1
}

# Include project path if provided
$args = @()
if ($project -and (Test-Path $project)) { $args += $project }

Start-Process -FilePath $exe -ArgumentList $args -NoNewWindow -Wait