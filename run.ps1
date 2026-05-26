# PHIght Club Run Script for Windows
# Runs the application and streams log output to console
# Usage: .\run.ps1 [Debug|Release]

param(
    [string]$Configuration = "Release"
)

$appPath = ".\src\PHIghtClub.App\bin\$Configuration\net8.0-windows\PHIghtClub.exe"

if (!(Test-Path $appPath)) {
    Write-Host "ERROR: Application not found at $appPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run build.ps1 first:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 -Configuration $Configuration"
    exit 1
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "PHIght Club v1.0.0" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting application..."
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Application: $appPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Log files: ./logs/" -ForegroundColor Yellow
Write-Host ""

# Run the application
& $appPath

Write-Host ""
Write-Host "Application closed." -ForegroundColor Green
