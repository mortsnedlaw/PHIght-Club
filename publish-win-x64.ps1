$ErrorActionPreference = "Stop"

$publishDir = ".\artifacts\publish\win-x64"

Write-Host "Publishing PHIght Club v1.0.0 for win-x64..."

dotnet publish .\src\PHIghtClub.App\PHIghtClub.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishDir=$publishDir

Copy-Item .\README.md $publishDir -Force
Copy-Item .\VERSION $publishDir -Force
Copy-Item .\CHANGELOG.md $publishDir -Force
Copy-Item .\docs\release-v1.0.0.md $publishDir -Force

Write-Host "Published to $publishDir"
