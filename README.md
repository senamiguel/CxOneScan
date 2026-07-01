# CxOneScan — Checkmarx One Desktop Client

A modern WPF desktop application for managing and running [Checkmarx One](https://checkmarx.com/product/application-security-platform/) SAST/SCA scans across multiple projects with a unified interface.

## Features

- **Multi-Project Scanning** — Import projects from directories, or add manually. Run SAST/SCA scans across selected projects.
- **Per-Project Configuration** — Each project gets its own branch, tags, project groups, and scan type settings.
- **Incremental Scanning** — Supports incremental scans with baseline verification. Confirms before full scans.
- **Copilot Integration** — Parse SARIF reports and generate detailed prompts for GitHub Copilot with severity filters and auto-open in Visual Studio.
- **Results Dashboard** — View scan results with severity breakdown (Critical, High, Medium, Low, Info) and open HTML reports.
- **Built-in CLI Installer** — Download and install the latest Checkmarx AST CLI directly from GitHub releases with Zip Slip protection.
- **Encrypted Credentials** — API keys encrypted using Windows DPAPI with in-memory cache.
- **Session Authentication** — KeepLoggedIn option with automatic re-authentication on startup.
- **Progress Tracking & Cancellation** — Real-time console output, progress bar, and cancellation support.
- **Fluent Design UI** — Dark/Light themes built with [iNKORE.UI.WPF.Modern](https://github.com/iNKORE-Inc/UI.WPF.Modern).

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK to build)
- [Checkmarx One CLI (`cx.exe`)](https://github.com/Checkmarx/ast-cli/releases) — can be installed from within the app

## Build & Run

```bash
dotnet build
dotnet run
```

To publish:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## Installer

An Inno Setup script is available at `installer/CxOneScan.iss`. Compile with:

```bash
iscc installer/CxOneScan.iss
```

Output goes to `installer/output/CxOneScanSetup.exe`.

## Project Structure

```
├── MainWindow.xaml / .cs       # Main UI and logic
├── SetupWizardWindow.xaml / .cs # First-run setup wizard
├── CopilotFilterWindow.xaml / .cs # SARIF vulnerability filter
├── App.xaml                     # Application entry point
├── Common/
│   └── AppConstants.cs          # Centralized constants
├── Converters/
│   └── StringToVisibilityConverter.cs
├── Models/
│   ├── AppSettings.cs           # Observable settings with INotifyPropertyChanged
│   ├── ProjectItem.cs           # Project data model
│   ├── ScanResult.cs            # Observable scan result with INotifyPropertyChanged
│   └── VulnerabilityItem.cs     # SARIF vulnerability model
├── Services/
│   ├── AppSettingsService.cs    # Settings persistence
│   ├── CheckmarxApiService.cs   # REST API client (OAuth2 refresh_token)
│   ├── CliInstallerService.cs   # CLI download and extraction
│   ├── CredentialService.cs     # DPAPI credential storage
│   ├── CxCliService.cs          # CLI process execution with GeneratedRegex
│   ├── DirectoryScannerService.cs  # Project discovery
│   ├── ProjectPersistenceService.cs  # JSON persistence
│   └── ReportParserService.cs   # Report parsing (JSON, SARIF)
└── installer/
    └── CxOneScan.iss            # Inno Setup installer script
```

## License

MIT
