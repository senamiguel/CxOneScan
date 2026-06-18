# CxOneScan — Checkmarx One Desktop Client

A modern WPF desktop application for managing and running [Checkmarx One](https://checkmarx.com/product/application-security-platform/) SAST/SCA scans across multiple projects with a unified interface.

## Features

- **Multi-Project Batch Scanning** — Import projects from `.sln` files, scan directories for `.csproj`, or add manually. Run SAST and SCA scans across all selected projects in one click.
- **Per-Project Configuration** — Each project gets its own branch, tags, project tags, project groups, and scan type settings.
- **Batch Tag Operations** — Apply tags, project tags, and project groups to multiple selected projects at once.
- **Results Dashboard** — View scan results with severity breakdown (High, Medium, Low, Info) and open HTML reports directly.
- **Built-in CLI Installer** — Download and install the latest Checkmarx AST CLI directly from GitHub releases.
- **Encrypted API Key Storage** — API keys are encrypted using Windows DPAPI (per-user scope) and stored securely.
- **Progress Tracking & Cancellation** — Real-time console output, progress bar, and the ability to cancel running scans.
- **Modern Dark UI** — Built with [iNKORE.UI.WPF.Modern](https://github.com/iNKORE-Inc/UI.WPF.Modern) for a Fluent Design experience.

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK to build)
- [Checkmarx One CLI (`cx.exe`)](https://github.com/Checkmarx/ast-cli/releases) — can be installed from within the app

## Build & Run

```bash
dotnet build
dotnet run
```

To publish as a single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Project Structure

```
├── MainWindow.xaml          # UI layout (WPF XAML)
├── MainWindow.xaml.cs       # UI logic and event handlers
├── App.xaml                 # Application entry point
├── Models/
│   ├── ProjectItem.cs       # Project data model with INotifyPropertyChanged
│   └── ScanResult.cs        # Scan result data model
├── Services/
│   ├── CxCliService.cs      # CLI process execution with cancellation support
│   ├── ProjectPersistenceService.cs  # JSON-based project persistence
│   ├── ReportParserService.cs        # Checkmarx report JSON parser
│   ├── SolutionParserService.cs      # .sln file parser for project import
│   └── DirectoryScannerService.cs    # Directory scanner for .csproj files
```

## License

MIT
