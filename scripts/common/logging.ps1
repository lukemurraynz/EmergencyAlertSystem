<#
.SYNOPSIS
Shared logging functions for PowerShell scripts.

.DESCRIPTION
Provides consistent, colorized logging output for deployment and infrastructure scripts.
Import this module in scripts using: . "$PSScriptRoot/../common/logging.ps1" (adjust path as needed)

.EXAMPLE
. "$PSScriptRoot/common/logging.ps1"
Write-StepHeader "Starting deployment"
Write-Success "Deployment complete"
#>

function Write-StepHeader {
    <#
    .SYNOPSIS
    Writes a prominent section header for major steps.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    Write-Host "`n$('#' * 70)" -ForegroundColor Cyan
    Write-Host "█ $Message" -ForegroundColor Cyan
    Write-Host ('#' * 70) -ForegroundColor Cyan
}

function Write-Success {
    <#
    .SYNOPSIS
    Writes a success message with green checkmark.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Info {
    <#
    .SYNOPSIS
    Writes an informational message with info icon.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    Write-Host "ℹ️  $Message" -ForegroundColor Cyan
}

function Write-WarningMsg {
    <#
    .SYNOPSIS
    Writes a warning message with warning icon.
    Uses Write-WarningMsg to avoid conflict with built-in Write-Warning cmdlet.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    <#
    .SYNOPSIS
    Writes an error message with red X icon.
    Uses Write-ErrorMsg to avoid conflict with built-in Write-Error cmdlet.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Test-CommandExists {
    <#
    .SYNOPSIS
    Checks if a command exists in the current session.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# Export functions for module usage
Export-ModuleMember -Function Write-StepHeader, Write-Success, Write-Info, Write-WarningMsg, Write-ErrorMsg, Test-CommandExists -ErrorAction SilentlyContinue
