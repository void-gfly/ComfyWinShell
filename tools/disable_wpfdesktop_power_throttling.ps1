param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $arguments = @(
        '-NoProfile'
        '-ExecutionPolicy'
        'Bypass'
        '-File'
        ('"{0}"' -f $PSCommandPath)
        '-ProjectRoot'
        ('"{0}"' -f $ProjectRoot)
    ) -join ' '

    Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait
    exit $LASTEXITCODE
}

$exePaths = @(
    Join-Path $ProjectRoot 'bin\Debug\net10.0-windows\WpfDesktop.exe'
    Join-Path $ProjectRoot 'bin\Release\net10.0-windows\WpfDesktop.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Sort-Object -Unique

if ($exePaths.Count -eq 0) {
    Write-Host 'No WpfDesktop.exe found under bin\Debug or bin\Release.'
    Write-Host 'Build the project first, then run this script again.'
    exit 1
}

foreach ($exePath in $exePaths) {
    Write-Host "Disabling power throttling for: $exePath"
    & powercfg /powerthrottling disable /path $exePath
    if ($LASTEXITCODE -ne 0) {
        throw "powercfg failed for: $exePath"
    }
}

Write-Host ''
Write-Host 'Current power throttling exemptions:'
& powercfg /powerthrottling list
Write-Host ''
Write-Host 'Done. Restart WpfDesktop to apply the path exemption.'
