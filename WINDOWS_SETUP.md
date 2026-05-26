# PHIght Club - Windows Setup & Build Instructions

## Prerequisites

1. **Windows 10 / 11 (64-bit)**
2. **.NET 8.0 SDK or later** - Download from https://dotnet.microsoft.com/download
3. **Visual Studio 2022** (optional, for IDE development) or **VS Code** (recommended)

### Verify .NET Installation

Open PowerShell and run:
```powershell
dotnet --version
```

Should output version 8.0 or higher.

---

## Quick Start

### 1. Clone / Open Repository

```powershell
cd C:\path\to\PHIght-Club
```

### 2. Build the Solution

```powershell
.\build.ps1
```

Or with Debug configuration:
```powershell
.\build.ps1 -Configuration Debug
```

**What this does:**
- Restores NuGet packages
- Compiles all projects
- Runs 25+ unit tests
- Displays build status and output paths

### 3. Run the Application

```powershell
.\run.ps1
```

Or run directly:
```powershell
.\src\PHIghtClub.App\bin\Release\net8.0-windows\PHIghtClub.exe
```

---

## Build & Run Details

### Manual Build

```powershell
# Restore packages
dotnet restore PHIghtClub.sln

# Build Release
dotnet build PHIghtClub.sln -c Release

# Run tests
dotnet test PHIghtClub.sln -c Release
```

### Run Tests Only

```powershell
dotnet test .\tests\PHIghtClub.Tests\PHIghtClub.Tests.csproj -c Release --verbosity detailed
```

### Clean Build

```powershell
dotnet clean PHIghtClub.sln
.\build.ps1
```

---

## Output Structure

After successful build:

```
src/
  PHIghtClub.App/bin/Release/net8.0-windows/
    ├── PHIghtClub.exe                    # Main executable
    ├── PHIghtClub.dll                    # App assembly
    └── *.dll                             # Dependencies
  
  PHIghtClub.Core/bin/Release/net8.0/
  PHIghtClub.Storage/bin/Release/net8.0/
  ... (other libraries)

logs/
  ├── phightclub-.log                     # Main application log
  └── phightclub-YYYY-MM-DD.log           # Daily rotated logs
```

---

## Logging & Audit Trail

Application creates structured logs in `./logs/` directory:

- **Log Level**: Information (production), Debug (development)
- **Format**: Timestamp | Level | Message | Exception
- **Rotation**: Daily (e.g., `phightclub-2026-05-26.log`)
- **Audit Events**: All pseudonymization, manifest operations, exports logged

View logs in real-time:
```powershell
Get-Content .\logs\phightclub-.log -Wait
```

---

## Project Structure

```
PHIght-Club/
├── src/
│   ├── PHIghtClub.App/              # WPF Desktop Application (Windows-only)
│   ├── PHIghtClub.Core/             # Domain models, enums, logging
│   ├── PHIghtClub.Storage/          # Manifest integrity, vault abstractions
│   ├── PHIghtClub.DeIdentification/ # Pseudonymization, date offset
│   ├── PHIghtClub.Dicom/            # DICOM SCP/SCU interfaces (placeholders)
│   ├── PHIghtClub.Export/           # Export contracts, manifest workflow
│   ├── PHIghtClub.Ocr/              # OCR engine interfaces (placeholders)
│   └── PHIghtClub.Pixel/            # Pixel scrubbing interfaces (placeholders)
│
├── tests/
│   └── PHIghtClub.Tests/            # Unit tests (25+ tests)
│
├── docs/                             # Documentation
├── samples/                          # Sample profiles & templates
├── build.ps1                         # Windows build script
├── build.sh                          # Linux/macOS build script
├── run.ps1                           # Windows run script
└── PHIghtClub.sln                    # Visual Studio solution file
```

---

## Development Workflow

### IDE Setup (Visual Studio 2022)

1. Open `PHIghtClub.sln` in Visual Studio 2022
2. Build → Build Solution (Ctrl+Shift+B)
3. Test → Run All Tests (Ctrl+R, A)
4. Run application (F5 or Ctrl+F5)

### IDE Setup (VS Code)

1. Open folder in VS Code
2. Install C# extension (ms-dotnettools.csharp)
3. Terminal → Run Build Task (Ctrl+Shift+B)
4. Run debug configuration (F5)

---

## Troubleshooting

### Error: "dotnet: command not found"
- Install .NET 8.0 SDK from https://dotnet.microsoft.com/download
- Restart PowerShell/Command Prompt

### Error: "Project targeting Windows on this OS"
- Only affects Linux/macOS builds
- Windows builds should work automatically
- Set `EnableWindowsTargeting=true` in .csproj if needed

### Error: "Tests failed to run"
- Ensure .NET 8.0+ is installed
- Run `dotnet test --verbosity diagnostic` for details
- Check logs in `.trx` files in `bin/` directories

### Permission Denied on Scripts
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

---

## Dependencies

**Production:**
- Serilog 3.1.1 (structured logging)
- Serilog.Sinks.File 5.0.0 (file logging)
- Serilog.Sinks.Console 5.0.1 (console output)
- Microsoft.Extensions.DependencyInjection 8.0.0 (DI container)

**Testing:**
- xunit 2.* (test framework)
- Moq 4.20.70 (mocking library)
- Microsoft.NET.Test.Sdk 17.* (test runner)

All dependencies are resolved automatically via NuGet during `dotnet restore`.

---

## Validation Checklist

After build, verify:

- [ ] `build.ps1` completes without errors
- [ ] All 25+ unit tests pass
- [ ] Executable exists: `src/PHIghtClub.App/bin/Release/net8.0-windows/PHIghtClub.exe`
- [ ] Log directory created: `./logs/`
- [ ] Application starts without crash
- [ ] Dry-run manifest generated successfully

---

## Next Steps (Phase 2)

- [ ] Integrate fo-dicom library (DICOM parsing)
- [ ] Implement real DICOM SCP/SCU services
- [ ] Add pixel scrubbing implementation
- [ ] Implement OCR integration (DirectML/CUDA)
- [ ] Persistent vault storage (SQLite + encryption)
- [ ] Security audit & penetration testing

---

## Support & Documentation

- **Security**: See `docs/security-hardening.md`
- **DICOM**: See `docs/dicom-conformance-statement-draft.md`
- **Validation**: See `docs/validation-plan-v1.0.md`
- **Architecture**: See main `README.md`

---

**Version**: 1.0.0 source release  
**Status**: Beta (not production-ready for real patient data)  
**Last Updated**: 2026-05-26
