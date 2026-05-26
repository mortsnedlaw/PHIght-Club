#!/bin/bash
# Build PHIght Club on non-Windows platforms
# Usage: ./build.sh [Debug|Release]

set -e

CONFIG=${1:-Release}

echo "=========================================="
echo "PHIght Club Build Script"
echo "=========================================="
echo "Configuration: $CONFIG"
echo ""

# Restore NuGet packages
echo "Restoring NuGet packages..."
dotnet restore

# Build solution
echo ""
echo "Building solution..."
dotnet build -c $CONFIG

# Run tests
echo ""
echo "Running unit tests..."
dotnet test -c $CONFIG --no-build --verbosity minimal

echo ""
echo "=========================================="
echo "Build completed successfully!"
echo "=========================================="
echo ""
echo "Output binaries:"
echo "  App:  src/PHIghtClub.App/bin/$CONFIG/net8.0-windows/PHIghtClub.dll"
echo "  Libs: src/*/bin/$CONFIG/net8.0/*.dll"
echo ""
echo "To run on Windows:"
echo "  cd src/PHIghtClub.App/bin/$CONFIG/net8.0-windows"
echo "  PHIghtClub.exe"
