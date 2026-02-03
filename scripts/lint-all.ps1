#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run all lint checks (backend + frontend)
.DESCRIPTION
    Runs both backend and frontend linting matching CI pipeline
#>
[CmdletBinding()]
param(
    [switch]$Fix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"
$scriptsDir = $PSScriptRoot

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Running All Lint Checks" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$backendResult = 0
$frontendResult = 0

# Run backend lint
Write-Host "`n==> Backend Lint" -ForegroundColor Magenta
& "$scriptsDir\lint-backend.ps1" -Fix:$Fix
$backendResult = $LASTEXITCODE

# Run frontend lint
Write-Host "`n==> Frontend Lint" -ForegroundColor Magenta
& "$scriptsDir\lint-frontend.ps1" -Fix:$Fix
$frontendResult = $LASTEXITCODE

# Summary
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "Lint Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

if ($backendResult -eq 0) {
    Write-Host "✅ Backend: PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Backend: FAILED (exit code: $backendResult)" -ForegroundColor Red
}

if ($frontendResult -eq 0) {
    Write-Host "✅ Frontend: PASSED" -ForegroundColor Green
} else {
    Write-Host "❌ Frontend: FAILED (exit code: $frontendResult)" -ForegroundColor Red
}

if ($backendResult -ne 0 -or $frontendResult -ne 0) {
    Write-Host "`n❌ Some checks failed" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`n✅ All checks passed!" -ForegroundColor Green
    exit 0
}
