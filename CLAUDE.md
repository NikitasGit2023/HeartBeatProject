# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HeartBeatProject** is a full-stack .NET 8 application that monitors health by writing and reading timestamped "heartbeat" files. TX and RX are separate executables — each has its own `appsettings.json` and its own Windows Service entry.

- **TX (Transmitter)** — writes heartbeat files at a configured interval
- **RX (Receiver)** — monitors a folder for heartbeat files; alerts when the latest file exceeds a threshold age

Solution file: `HeartBeatProject.slnx` (at repo root)

## Commands

```bash
# Build entire solution
dotnet build HeartBeatProject.slnx

# Run TX (Transmitter)
dotnet run --project HeartBeatProject.Tx/HeartBeatProject.Tx.csproj

# Run RX (Receiver)
dotnet run --project HeartBeatProject.Rx/HeartBeatProject.Rx.csproj

# Publish TX as self-contained single-file executable (win-x64)
dotnet publish HeartBeatProject.Tx/HeartBeatProject.Tx.csproj -c Release

# Publish RX as self-contained single-file executable (win-x64)
dotnet publish HeartBeatProject.Rx/HeartBeatProject.Rx.csproj -c Release
```

**Default URLs:** TX `http://localhost:5000`, RX `http://localhost:5001` (set via `"Urls"` in each `appsettings.json`).

## Architecture

Five projects in one solution:

### `HeartBeatProject.shared` (Shared DTOs)
- DTOs shared by client and server: `StatusDto`, `SettingsDto`, `LogEntryDto`
- Namespace: `HeartBeatProject.Shared.Dtos`

### `HeartBeatProject.Client` (Blazor WebAssembly)
- Assembly name kept as `HeartBeatProject` so no index.html changes were needed
- `HttpClient` pre-injected with the server base address
- Client-side polling: Dashboard every 3 s (+ 1 s countdown), Logs every 2 s, Settings once on init
- Pages: `Dashboard.razor`, `Settings.razor`, `Logs.razor`
- CSS animations (spin, pulse-green, pulse-red, pulse-live) in `wwwroot/css/app.css`

### `HeartBeatProject.server` (Server Library — shared infrastructure)
- Class library (`Sdk.NET` + `FrameworkReference Microsoft.AspNetCore.App`)
- Namespace: `HeartBeatProject.Server.*`
- Contains: `Configuration/` (options), `Controllers/HeartbeatController`, `Logging/` (InMemoryLogStore, InMemoryNLogTarget), `Services/Alerts/` (all providers), `Services/RuntimeSettingsStore`, `Services/IHeartbeatStatusService`
- Controllers are discovered in executables via `AddApplicationPart(typeof(HeartbeatController).Assembly)`
- **RuntimeSettingsStore**: lock-based thread-safe in-memory store. `Get()` and `Update()` both deep-copy the DTO. `Update()` validates all fields including port ranges, email format, and blocks port 465 with `EnableSsl`.
- **Controller**: `GET /api/settings` masks SMTP password as `"********"`; `POST /api/settings` preserves password when mask is echoed back.

### `HeartBeatProject.Tx` (TX Executable)
- Namespace: `HeartBeatProject.Tx.Services`
- References: `HeartBeatProject.server` + `HeartBeatProject.Client`
- Contains: `HeartbeatTxService`, `HeartbeatFileGenerator` (+ `IHeartbeatFileGenerator`), `TxHeartbeatStatusService`
- Mode is implicit — no `Heartbeat:Mode` in appsettings, no mode routing in Program.cs

### `HeartBeatProject.Rx` (RX Executable)
- Namespace: `HeartBeatProject.Rx.Services`
- References: `HeartBeatProject.server` + `HeartBeatProject.Client`
- Contains: `HeartbeatRxService`, `RxHeartbeatStatusService`
- Mode is implicit — no `Heartbeat:Mode` in appsettings, no mode routing in Program.cs

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
- **Syslog target**: `NLog.Targets.Syslog`, routed only from the `"SyslogAlert"` logger (`final="true"` prevents duplication). `Server` is a Layout (`${gdc:item=SyslogHost}`); `Port` and `Facility` are set programmatically in each `Program.cs`.

`nlog.config` must sit beside the executable (`CopyToOutputDirectory=Always`). Both Tx and Rx have their own copy.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/status` | `StatusDto` — mode, status, lastHeartbeat, uptime, intervalSeconds |
| GET | `/api/settings` | `SettingsDto` with password masked as `"********"` |
| POST | `/api/settings` | Validates and updates `RuntimeSettingsStore`; `400` on invalid input |
| GET | `/api/logs` | Last 200 `LogEntryDto` entries (most recent first) |

## Configuration

**TX `appsettings.json`** (no `Mode` field — binary IS the mode):
```json
{
  "Urls": "http://localhost:5000",
  "Heartbeat": {
    "FolderPath": "C:\\Heartbeat\\Shared\\HeartbeatFiles",
    "FileNamePrefix": "heartbeat",
    "IntervalSeconds": 30,
    "OverwriteExisting": true
  },
  "Alerts": { ... }
}
```

**RX `appsettings.json`**:
```json
{
  "Urls": "http://localhost:5001",
  "Heartbeat": {
    "FolderPath": "C:\\Heartbeat\\Shared\\HeartbeatFiles",
    "CheckIntervalSeconds": 10,
    "ThresholdSeconds": 60
  },
  "Alerts": { ... }
}
```

`SyslogFacility` valid values: `Kernel`, `User`, `Daemons`, `Local0`–`Local7`.  
Port 465 + `EnableSsl: true` is rejected at validation time — use port 587 (STARTTLS).  
`To` supports semicolon- or comma-separated addresses.  
Runtime changes via `POST /api/settings` update `RuntimeSettingsStore` only — do not persist to disk.

## Deployment

```
C:\Heartbeat\
├── TX\   → HeartBeatProject.Tx.exe + nlog.config + appsettings.json (Urls=:5000)
├── RX\   → HeartBeatProject.Rx.exe + nlog.config + appsettings.json (Urls=:5001)
└── Shared\HeartbeatFiles\   ← TX writes here, RX reads here
```

Register as Windows Services:
```
sc create HeartbeatTX binPath="C:\Heartbeat\TX\HeartBeatProject.Tx.exe" start=auto
sc create HeartbeatRX binPath="C:\Heartbeat\RX\HeartBeatProject.Rx.exe" start=auto
```

## Key Conventions

- Nullable reference types enabled across all projects
- All background service loops use `CancellationToken`; swallow all exceptions except `OperationCanceledException`
- Thread-safety: `RuntimeSettingsStore` and `InMemoryLogStore` use `lock`; `HeartbeatRxService` uses `volatile bool` for HEALTHY/DOWN state
- New alert providers: implement `IAlertService`, add to `CompositeAlertService` in `HeartBeatProject.server`, register in both `Tx/Program.cs` and `Rx/Program.cs`
- New Blazor pages: add to `HeartBeatProject.Client/Pages/` with `@page "/route"`, link from `Layout/NavMenu.razor`
- New API endpoints: add to `HeartBeatProject.server/Controllers/`; business logic in a new `Services/` class in that library
- New shared DTOs: add to `HeartBeatProject.shared/Dtos/`
