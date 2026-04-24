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

# Publish server as self-contained single-file executable (win-x64)
dotnet publish HeartBeatProject.server/HeartBeatProject.server.csproj -c Release
```

**Default URLs after `dotnet run` on the server:**
- HTTP: http://localhost:5295
- HTTPS: https://localhost:7266 (launches to `/dashboard` by default)
- Swagger UI: https://localhost:7266/swagger (development only)

## Architecture

Three projects in one solution:

### `HeartBeatProject` (Client — Blazor WebAssembly)
- `HttpClient` is pre-injected with the server base address
- Client-side polling: Dashboard every 3 s (plus a 1 s countdown timer for smooth UX), Logs every 2 s, Settings loaded once on init
- Three pages: `Pages/Dashboard.razor`, `Pages/Settings.razor`, `Pages/Logs.razor`
- Shared CSS animations (spin, pulse-green, pulse-red, pulse-live) live in `wwwroot/css/app.css`

### `HeartBeatProject.server` (Server — ASP.NET Core)
- Hosts Blazor WASM via `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html")`
- `Controllers/HeartbeatController.cs` exposes the real API (see below)
- Two background services: `HeartbeatTxService` and `HeartbeatRxService` — only the one matching the configured mode is registered in `Program.cs`
- `Services/RuntimeSettingsStore.cs` — lock-based thread-safe in-memory store, initialized from `appsettings.json`, updated by `POST /api/settings`
- Custom logging: all `ILogger` output flows through `InMemoryLoggerProvider` → `InMemoryLogStore` (circular queue, max 500 entries, lock-based) and also writes to `{BaseDirectory}/Logs/heartbeat_YYYYMMDD.txt` (daily rotation). Only `Information` and above are captured.
- Log category mapping: TxService→"TX", RxService→"RX", AlertService→"Email", everything else→"System"
- Published as a self-contained `win-x64` single-file executable (see `.csproj`)

### `HeartBeatProject.shared` (Shared Library)
- DTOs used by both client and server: `StatusDto`, `SettingsDto`, `LogEntryDto`

## Heartbeat Flow

```
TX mode:  HeartbeatTxService ──(every IntervalSeconds)──> IHeartbeatFileGenerator.GenerateAsync()
                                                           └─> writes heartbeat_{YYYYMMDD_HHmmss}.txt to FolderPath

RX mode:  HeartbeatRxService ──(every CheckIntervalSeconds)──> scans FolderPath for .txt files
                                                                └─> HEALTHY if latest file age < ThresholdSeconds
                                                                └─> DOWN + alert on HEALTHY→DOWN transition only
                                                                    (5-minute cooldown between repeated alerts)
```

TX alerts on first failure only, then again on recovery. Alerts are sent via `IAlertService` (SMTP implementation in `SmtpAlertService`). The interface is designed for additional providers (Syslog, SNMP).

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
    "Mode": "TX",               // "TX" or "RX"
    "FolderPath": "...",
    "FileNamePrefix": "heartbeat",
    "IntervalSeconds": 30,      // TX: write interval
    "OverwriteExisting": true,
    "LogFolderPath": "...",
    "CheckIntervalSeconds": 10, // RX: scan interval
    "ThresholdSeconds": 60      // RX: age before DOWN
  },
  "Alerts": {
    "EnableEmail": false,
    "SmtpServer": "",
    "Port": 587,
    "From": "",
    "To": "",
    "Username": "",
    "Password": "",
    "EnableSsl": true
  }
}
```

Runtime changes via `POST /api/settings` update `RuntimeSettingsStore` only — they do not persist to `appsettings.json`.

## Key Conventions

- Nullable reference types enabled across all projects
- All background service loops use `CancellationToken`; services swallow all exceptions except `OperationCanceledException`
- Thread-safety: `RuntimeSettingsStore` and `InMemoryLogStore` use `lock`; `HeartbeatRxService` uses a `volatile` flag for HEALTHY/DOWN state
- New alert providers: implement `IAlertService` and register in `Program.cs`
- New Blazor pages: add to `HeartBeatProject/Pages/` with `@page "/route"` and link from `Layout/NavMenu.razor`
- New API endpoints: add to `HeartBeatProject.server/Controllers/`
- Shared data models: add to `HeartBeatProject.shared/Dtos/`
