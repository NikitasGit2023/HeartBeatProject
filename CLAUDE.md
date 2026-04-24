# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HeartBeatProject** is a full-stack .NET 8 application that monitors health by writing and reading timestamped "heartbeat" files. A single binary is deployed twice — once as TX, once as RX — each with its own `appsettings.json`.

- **TX (Transmitter)** — writes heartbeat files at a configured interval
- **RX (Receiver)** — monitors a folder for heartbeat files; alerts when the latest file exceeds a threshold age

Solution file: `HeartBeatProject/HeartBeatProject.slnx`

## Commands

```bash
# Build entire solution
dotnet build HeartBeatProject/HeartBeatProject.slnx

# Run the server
dotnet run --project HeartBeatProject.server/HeartBeatProject.server.csproj

# Publish as self-contained single-file executable (win-x64)
dotnet publish HeartBeatProject.server/HeartBeatProject.server.csproj -c Release
```

**Default URL:** `http://localhost:5000` (set via `"Urls"` in `appsettings.json`).  
TX and RX installations use different ports (e.g. 5000 / 5001) when co-located on the same machine.

## Architecture

Three projects in one solution:

### `HeartBeatProject` (Client — Blazor WebAssembly)
- `HttpClient` pre-injected with the server base address
- Client-side polling: Dashboard every 3 s (+ 1 s countdown), Logs every 2 s, Settings once on init
- Pages: `Dashboard.razor`, `Settings.razor`, `Logs.razor`
- CSS animations (spin, pulse-green, pulse-red, pulse-live) in `wwwroot/css/app.css`

### `HeartBeatProject.server` (Server — ASP.NET Core)
- Hosts Blazor WASM via `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html")`
- `appsettings.json` content root is forced to `AppContext.BaseDirectory` via `builder.Host.UseContentRoot(...)` — required for Windows Service deployments
- **Mode routing**: `Heartbeat:Mode` is validated at startup before NLog is initialised. An invalid mode throws `InvalidOperationException` immediately. Only the matching `BackgroundService` is registered in DI — `HeartbeatTxService` for TX, `HeartbeatRxService` for RX. Neither service contains a mode guard.
- **Controller**: `HeartbeatController` is thin. All business logic is in services. `GET /api/settings` masks the SMTP password as `"********"`; `POST /api/settings` preserves the existing password if the client echoes the mask back.
- **RuntimeSettingsStore**: lock-based thread-safe in-memory store. `Get()` and `Update()` both deep-copy the DTO. `Update()` validates all fields including port ranges, email format (`System.Net.Mail.MailAddress`), and blocks port 465 with `EnableSsl` (not supported by `System.Net.Mail`).

### `HeartBeatProject.shared` (Shared Library)
- DTOs shared by client and server: `StatusDto`, `SettingsDto`, `LogEntryDto`

## Heartbeat Flow

```
TX:  HeartbeatTxService ──(every IntervalSeconds)──► IHeartbeatFileGenerator.GenerateAsync()
       OverwriteExisting=true  → writes heartbeat_latest.txt (single file, always fresh)
       OverwriteExisting=false → writes heartbeat_YYYYMMDD_HHmmss.txt (accumulating)
       Alert on first failure; re-alert every 5 min; recovery alert on next success.

RX:  HeartbeatRxService ──(every CheckIntervalSeconds)──► scans FolderPath for latest *.txt
       HEALTHY if file age ≤ ThresholdSeconds
       Alert on HEALTHY→DOWN; re-alert every 5 min; recovery alert on DOWN→HEALTHY.
       Watchdog covers: folder missing, no files, stale file.
```

All timestamps use `DateTime.UtcNow`; file ages use `LastWriteTimeUtc`.  
Alert cooldown uses `DateTime.MinValue` as initial `_lastAlertTime` so the first event always fires immediately.

## Alert Pipeline

Alerts flow through `CompositeAlertService` which fires all three providers in parallel via `Task.WhenAll`:

```
IAlertService (CompositeAlertService)
  ├─ SmtpAlertService    → System.Net.Mail, 3 retries (2 s delay), 15 s timeout
  ├─ SnmpTrapSender      → Lextm.SharpSnmpLib 12.5.7, SNMPv2c trap, enterprise OID 1.3.6.1.4.1.99999
  └─ SyslogSender        → NLog.Targets.Syslog 7.0.0 via dedicated "SyslogAlert" NLog logger
```

Each provider reads **live settings** from `RuntimeSettingsStore` on every call, so alert config changes via `POST /api/settings` take effect immediately without restart.  
Each provider skips gracefully with a `LogWarning` if its `Enable*` flag is false or required fields are empty.

## Logging

NLog (`NLog.Web.AspNetCore`) drives all output:
- **File target**: daily-rolling `{BaseDirectory}/Logs/heartbeat_YYYYMMDD.txt`, 30-day archive
- **Console target**: coloured
- **Dashboard target**: `InMemoryNLogTarget` (custom `TargetWithLayout`) feeds `ILogStore` → `GET /api/logs`. Registered programmatically after `nlog.config` is loaded, not in XML.
- **Syslog target**: `NLog.Targets.Syslog`, routed only from the `"SyslogAlert"` logger (`final="true"` prevents duplication to file/console). `Server` is a Layout (`${gdc:item=SyslogHost}`); `Port` and `Facility` are set programmatically in `Program.cs` after config load.

`nlog.config` must sit beside the executable (`CopyToOutputDirectory=Always`). NLog and Syslog GDC vars are configured before `WebApplication.CreateBuilder` using an early `ConfigurationBuilder` seeded from `AppContext.BaseDirectory`.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/status` | `StatusDto` — mode, status, lastHeartbeat, uptime, intervalSeconds (CheckInterval in RX mode) |
| GET | `/api/settings` | `SettingsDto` with password masked as `"********"` |
| POST | `/api/settings` | Validates and updates `RuntimeSettingsStore`; `400` on invalid input |
| GET | `/api/logs` | Last 500 `LogEntryDto` entries |

## Configuration

Full `appsettings.json` schema — all fields are required in each installation:

```json
{
  "Urls": "http://localhost:5000",
  "Heartbeat": {
    "Mode": "TX",
    "FolderPath": "C:\\Heartbeat\\Shared\\HeartbeatFiles",
    "FileNamePrefix": "heartbeat",
    "IntervalSeconds": 30,
    "OverwriteExisting": true,
    "LogFolderPath": "",
    "CheckIntervalSeconds": 10,
    "ThresholdSeconds": 60
  },
  "Alerts": {
    "EnableEmail": false,
    "SmtpServer": "", "Port": 587, "From": "", "To": "",
    "Username": "", "Password": "", "EnableSsl": true,
    "EnableSnmp": false,
    "SnmpHost": "", "SnmpPort": 162, "Community": "public",
    "EnableSyslog": false,
    "SyslogHost": "", "SyslogPort": 514, "SyslogFacility": "Local0"
  }
}
```

`SyslogFacility` valid values: `Kernel`, `User`, `Daemons`, `Local0`–`Local7`.  
Port 465 + `EnableSsl: true` is rejected at validation time — use port 587 (STARTTLS).  
`To` supports semicolon- or comma-separated addresses.  
Runtime changes via `POST /api/settings` update `RuntimeSettingsStore` only — do not persist to disk.

## Deployment

```
C:\Heartbeat\
├── TX\   → HeartBeatProject.server.exe + nlog.config + appsettings.json (Mode=TX, Urls=:5000)
├── RX\   → HeartBeatProject.server.exe + nlog.config + appsettings.json (Mode=RX, Urls=:5001)
└── Shared\HeartbeatFiles\   ← TX writes here, RX reads here
```

Register as Windows Services:
```
sc create HeartbeatTX binPath="C:\Heartbeat\TX\HeartBeatProject.server.exe" start=auto
sc create HeartbeatRX binPath="C:\Heartbeat\RX\HeartBeatProject.server.exe" start=auto
```

## Key Conventions

- Nullable reference types enabled across all projects
- All background service loops use `CancellationToken`; swallow all exceptions except `OperationCanceledException`
- Thread-safety: `RuntimeSettingsStore` and `InMemoryLogStore` use `lock`; `HeartbeatRxService` uses `volatile bool` for HEALTHY/DOWN state
- New alert providers: implement `IAlertService`, add to `CompositeAlertService`, register in `Program.cs`
- New Blazor pages: add to `HeartBeatProject/Pages/` with `@page "/route"`, link from `Layout/NavMenu.razor`
- New API endpoints: add to `Controllers/`; business logic in a new `Services/` class
- New shared DTOs: add to `HeartBeatProject.shared/Dtos/`
