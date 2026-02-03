#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local backend lint script matching CI pipeline checks
.DESCRIPTION
    Runs dotnet format verification and build with warnings as errors
.PARAMETER Fix
    Auto-fix formatting issues
.PARAMETER SkipTests
    Skip test project compilation (only use when tests are broken)
#>
[CmdletBinding()]
param(
    [switch]$Fix,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$backendDir = Join-Path $PSScriptRoot ".." "backend"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Backend Lint Check" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Restore dependencies
Write-Host "`n[1/3] Restoring dependencies..." -ForegroundColor Yellow
Push-Location $backendDir
try {
    dotnet restore EmergencyAlerts.sln
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Dependency restoration failed"
        exit $LASTEXITCODE 
    }

    # Format check or fix
    if ($Fix) {
        Write-Host "`n[2/3] Fixing code formatting..." -ForegroundColor Yellow
        dotnet format EmergencyAlerts.sln --verbosity diagnostic
    } else {
        Write-Host "`n[2/3] Checking code formatting..." -ForegroundColor Yellow
        dotnet format EmergencyAlerts.sln --verify-no-changes --verbosity diagnostic
    }
    
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Code formatting check failed. Run with -Fix to auto-fix."
        exit $LASTEXITCODE 
    }

    # Build with warnings as errors
    Write-Host "`n[3/4] Building src projects with warnings as errors..." -ForegroundColor Yellow
    
    dotnet build "$backendDir/src/EmergencyAlerts.Domain/EmergencyAlerts.Domain.csproj" /p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Domain build failed"
        exit $LASTEXITCODE 
    }
    
    dotnet build "$backendDir/src/EmergencyAlerts.Application/EmergencyAlerts.Application.csproj" /p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Application build failed"
        exit $LASTEXITCODE 
    }
    
    dotnet build "$backendDir/src/EmergencyAlerts.Infrastructure/EmergencyAlerts.Infrastructure.csproj" /p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Infrastructure build failed"
        exit $LASTEXITCODE 
    }
    
    dotnet build "$backendDir/src/EmergencyAlerts.Api/EmergencyAlerts.Api.csproj" /p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "API build failed"
        exit $LASTEXITCODE 
    }

    # Validate test compilation
    if ($SkipTests) {
        Write-Host "`n[4/4] Skipping test compilation (use only when tests are broken)" -ForegroundColor Gray
        Write-Host "Note: See backend/TEST_FIXES_NEEDED.md for known test issues" -ForegroundColor Gray
    } else {
        Write-Host "`n[4/4] Validating test project compilation..." -ForegroundColor Yellow
        
        $testProjects = @(
            "tests/EmergencyAlerts.Domain.Tests/EmergencyAlerts.Domain.Tests.csproj",
            "tests/EmergencyAlerts.Application.Tests/EmergencyAlerts.Application.Tests.csproj",
            "tests/EmergencyAlerts.Infrastructure.Tests/EmergencyAlerts.Infrastructure.Tests.csproj",
            "tests/EmergencyAlerts.Api.Tests/EmergencyAlerts.Api.Tests.csproj"
        )
        
        foreach ($project in $testProjects) {
            $projectName = Split-Path $project -Leaf
            Write-Host "  Building $projectName..." -ForegroundColor Gray
            dotnet build "$backendDir/$project" --no-restore
            if ($LASTEXITCODE -ne 0) { 
                Write-Error "Test compilation failed: $projectName. Use -SkipTests to bypass (discouraged)."
                exit $LASTEXITCODE 
            }
        }
    }

    Write-Host "`n✅ Backend lint checks passed!" -ForegroundColor Green
} finally {
    Pop-Location
}
