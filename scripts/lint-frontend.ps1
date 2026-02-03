#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local frontend lint script matching CI pipeline checks
.DESCRIPTION
    Runs ESLint and TypeScript type checking
#>
[CmdletBinding()]
param(
    [switch]$Fix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$frontendDir = Join-Path $PSScriptRoot ".." "frontend"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Frontend Lint Check" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Push-Location $frontendDir
try {
    # Install dependencies if needed
    if (-not (Test-Path "node_modules")) {
        Write-Host "`n[1/3] Installing dependencies..." -ForegroundColor Yellow
        npm ci
        if ($LASTEXITCODE -ne 0) { 
            Write-Error "npm install failed"
            exit $LASTEXITCODE 
        }
    } else {
        Write-Host "`n[1/3] Dependencies already installed" -ForegroundColor Green
    }

    # ESLint
    if ($Fix) {
        Write-Host "`n[2/3] Running ESLint with auto-fix..." -ForegroundColor Yellow
        npm run lint -- --fix
    } else {
        Write-Host "`n[2/3] Running ESLint..." -ForegroundColor Yellow
        npm run lint
    }
    
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "ESLint check failed. Run with -Fix to auto-fix."
        exit $LASTEXITCODE 
    }

    # TypeScript type check
    Write-Host "`n[3/3] Checking TypeScript types..." -ForegroundColor Yellow
    npx tsc --noEmit
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "TypeScript type check failed"
        exit $LASTEXITCODE 
    }

    Write-Host "`n✅ Frontend lint checks passed!" -ForegroundColor Green
} finally {
    Pop-Location
}
