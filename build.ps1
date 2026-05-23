$ErrorActionPreference = "Stop"

Write-Host "Building PHIght Club v1.0.0..."

dotnet restore .\src\PHIghtClub.App\PHIghtClub.App.csproj
dotnet build .\src\PHIghtClub.App\PHIghtClub.App.csproj -c Release --no-restore

dotnet test .\tests\PHIghtClub.Tests\PHIghtClub.Tests.csproj -c Release --no-restore

Write-Host "Build completed."
