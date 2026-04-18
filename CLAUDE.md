# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HeartBeatProject** is a full-stack .NET 8 application that monitors health by writing and reading timestamped "heartbeat" files. It supports two modes:
- **TX (Transmitter)** — writes heartbeat files at a configured interval
- **RX (Receiver)** — monitors a folder for heartbeat files and transitions to DOWN state when the latest file exceeds a threshold age

Solution file: `HeartBeatProject/HeartBeatProject.slnx`

## Commands

```bash
# Build entire solution
dotnet build HeartBeatProject/HeartBeatProject.slnx

# Run the server (serves both API and Blazor WASM client)
dotnet run --project HeartBeatProject.server/HeartBeatProject.server.csproj

# Build the WPF installer (Windows only)
dotnet build HeartBeatProjectInstaller/HeartBeatProjectInstaller.csproj
```

**Default URLs after `dotnet run` on the server:**
- HTTP: http://localhost:5295
- HTTPS: https://localhost:7266
- Swagger UI: https://localhost:7266/swagger

## Architecture

Four projects in one solution:

### `HeartBeatProject` (Client — Blazor WebAssembly)
- `HttpClient` is pre-injected with the server base address
- Pages auto-poll the server API: Dashboard every 3 s, Logs every 2 s
- Three real pages: `Pages/Dashboard.razor`, `Pages/Settings.razor`, `Pages/Logs.razor`

### `HeartBeatProject.server` (Server — ASP.NET Core)
- Hosts Blazor WASM via `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html")`
- `Controllers/HeartbeatController.cs` exposes the real API (see below); `WeatherForecastController` is boilerplate
- Two background services: `HeartbeatTxService` and `HeartbeatRxService` — only the one matching the configured mode actually runs
- `Services/RuntimeSettingsStore.cs` — thread-safe in-memory store updated by `POST /api/settings`
- Custom logging pipeline: all `ILogger` output flows through `InMemoryLoggerProvider` → `InMemoryLogStore` (circular queue, max 500 entries) and also writes to `Logs/heartbeat_YYYYMMDD.txt`

### `HeartBeatProject.shared` (Shared Library)
- DTOs used by both client and server: `StatusDto`, `SettingsDto`, `LogEntryDto`

### `HeartBeatProjectInstaller` (WPF Windows Installer)
- 4-step wizard: Welcome → Config → Install → Finish
- `Services/InstallerService.cs` writes `appsettings.json` and manages the Windows service via `sc.exe`

## Heartbeat Flow

```
TX mode:  HeartbeatTxService ──(every IntervalSeconds)──> IHeartbeatFileGenerator.GenerateAsync()
                                                           └─> writes timestamped file to FolderPath

RX mode:  HeartbeatRxService ──(every CheckIntervalSeconds)──> scans FolderPath
                                                                └─> HEALTHY if latest file age < ThresholdSeconds
                                                                └─> DOWN + alert if age >= ThresholdSeconds
```

Alerts are sent via `IAlertService` (SMTP implementation in `SmtpAlertService`). The interface is designed for additional providers (Syslog, SNMP).

## API Endpoints (`/api/`)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/status` | Returns `StatusDto` (mode, status, lastHeartbeat, uptime, intervalSeconds) |
| GET | `/api/settings` | Returns current `SettingsDto` |
| POST | `/api/settings` | Updates `RuntimeSettingsStore` with new `SettingsDto` |
| GET | `/api/logs` | Returns last 200 `LogEntryDto` entries |

## Configuration (`appsettings.json`)

```json
{
  "Heartbeat": {
    "Mode": "TX",              // "TX" or "RX"
    "FolderPath": "...",
    "IntervalSeconds": 30,     // TX: write interval
    "OverwriteExisting": true,
    "LogFolderPath": "...",
    "CheckIntervalSeconds": 10, // RX: scan interval
    "ThresholdSeconds": 60     // RX: age before DOWN
  },
  "Alerts": {
    "EnableEmail": false,
    "SmtpServer": "",
    "Port": 587,
    "From": "",
    "To": ""
  }
}
```

Runtime changes via `POST /api/settings` update `RuntimeSettingsStore` only — they do not persist to `appsettings.json`.

## Key Conventions

- Nullable reference types enabled across all projects
- New alert providers: implement `IAlertService` and register in `Program.cs`
- New Blazor pages: add to `HeartBeatProject/Pages/` with `@page "/route"` and link from `Layout/NavMenu.razor`
- New API endpoints: add to `HeartBeatProject.server/Controllers/`
- Shared data models: add to `HeartBeatProject.shared` DTOs folder
