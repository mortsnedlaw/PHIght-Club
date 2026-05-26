# PHIght Club Build Script for Windows
# Requires .NET 8.0 SDK or later
# Usage: .\build.ps1 [Debug|Release]

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "PHIght Club v1.0.0 Build Script" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Platform: Windows" -ForegroundColor Yellow
Write-Host ""

# Check .NET SDK version
Write-Host "Checking .NET SDK version..."
$dotnetVersion = & dotnet --version
Write-Host "✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
dotnet restore .\PHIghtClub.sln
Write-Host "✓ Packages restored" -ForegroundColor Green
Write-Host ""

# Build solution
Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
dotnet build .\PHIghtClub.sln -c $Configuration --no-restore
Write-Host "✓ Build completed" -ForegroundColor Green
Write-Host ""

# Run tests
Write-Host "Running unit tests..." -ForegroundColor Cyan
dotnet test .\PHIghtClub.sln -c $Configuration --no-build --verbosity minimal
Write-Host "✓ Tests completed" -ForegroundColor Green
Write-Host ""

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output binaries:" -ForegroundColor Yellow
Write-Host "  App:  src\PHIghtClub.App\bin\$Configuration\net8.0-windows\PHIghtClub.exe"
Write-Host "  Libs: src\*\bin\$Configuration\net8.0\*.dll"
Write-Host ""
Write-Host "To run the application:" -ForegroundColor Yellow
Write-Host "  .\src\PHIghtClub.App\bin\$Configuration\net8.0-windows\PHIghtClub.exe"
Write-Host ""
Write-Host "Log files will be created in:" -ForegroundColor Yellow
Write-Host "  ./logs/phightclub-.log"

