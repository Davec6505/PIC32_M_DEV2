param (
    [string]$project
)

function Get-CandidateDirs {
    @(
        'C:\Users\Public\Documents\Mikroelektronika\USB HID BootLoader',
        'C:\Program Files\Mikroelektronika',
        'C:\Program Files (x86)\Mikroelektronika',
        "$env:ProgramData\Mikroelektronika",
        "$env:LOCALAPPDATA\Mikroelektronika"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
}

function Resolve-Mikroe {
    $names = @(
        'mikroBootloader USB HID.exe',
        'USB HID BootLoader.exe'
    )

    # 0) Explicit override: env var
    if ($env:MIKROE_HID_BOOTLOADER -and (Test-Path -LiteralPath $env:MIKROE_HID_BOOTLOADER)) {
        return (Resolve-Path -LiteralPath $env:MIKROE_HID_BOOTLOADER).Path
    }

    # 0b) Explicit override: file next to this script named startMikroe.path containing the full exe path
    $configPath = Join-Path -Path $PSScriptRoot -ChildPath 'startMikroe.path'
    if (Test-Path -LiteralPath $configPath) {
        $p = Get-Content -LiteralPath $configPath -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($p -and (Test-Path -LiteralPath $p)) {
            return (Resolve-Path -LiteralPath $p).Path
        }
    }

    # 1) Try PATH
    foreach ($n in $names) {
        $cmd = Get-Command $n -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }

    # 2) Try common install directories
    foreach ($d in Get-CandidateDirs) {
        foreach ($n in $names) {
            $candidate = Join-Path -Path $d -ChildPath $n
            if (Test-Path -LiteralPath $candidate) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }

        # 3) Fallback: search likely names
        $match = Get-ChildItem -LiteralPath $d -Filter '*.exe' -File -Recurse -ErrorAction SilentlyContinue |
                 Where-Object { $_.Name -like '*mikro*Boot*HID*.exe' -or $_.Name -like '*USB*HID*Boot*Loader*.exe' } |
                 Sort-Object LastWriteTime -Descending |
                 Select-Object -First 1
        if ($match) { return $match.FullName }
    }

    return $null
}

$exe = Resolve-Mikroe
if (-not $exe) {
    Write-Error "Mikroe executable not found. Set MIKROE_HID_BOOTLOADER env var, create $(Join-Path $PSScriptRoot 'startMikroe.path'), add it to PATH, or install under C:\Users\Public\Documents\Mikroelektronika."
    exit 1
}

# Include project path if provided
$argList = @()
if ($project) {
    if (Test-Path -LiteralPath $project) {
        $argList += (Resolve-Path -LiteralPath $project).Path
    } else {
        Write-Warning "Project path not found: $project"
    }
}

Start-Process -FilePath $exe -ArgumentList $argList -NoNewWindow -Wait