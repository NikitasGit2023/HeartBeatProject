# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HeartBeatProject** is a full-stack .NET 8 application that monitors health by writing and reading timestamped "heartbeat" files. TX and RX are separate executables — each has its own `appsettings.json` and its own Windows Service entry.

- **TX (Transmitter)** — writes heartbeat files at a configured interval
- **RX (Receiver)** — monitors a folder for heartbeat files; alerts when the latest file exceeds a threshold age

Solution file: `HeartBeatProject.slnx` (at repo root)

## Commands

There are no test projects in this solution.

```bash
# Build entire solution
dotnet build HeartBeatProject.slnx

# Run TX (Transmitter)
dotnet run --project HeartBeatProject.Tx/HeartBeatProject.Tx.csproj

# Run RX (Receiver)
dotnet run --project HeartBeatProject.Rx/HeartBeatProject.Rx.csproj

# Publish TX → publish/TX
dotnet publish HeartBeatProject.Tx/HeartBeatProject.Tx.csproj /p:PublishProfile=Release-win-x64

# Publish RX → publish/RX
dotnet publish HeartBeatProject.Rx/HeartBeatProject.Rx.csproj /p:PublishProfile=Release-win-x64

# Publish both at once (PowerShell)
.\publish-all.ps1

# Publish both at once (batch)
publish-all.bat
```

**Default URLs:** TX `http://localhost:5000`, RX `http://localhost:5002` (set via `"Urls"` in each `appsettings.json`). Note: the fallback hardcoded in `Rx/Program.cs` is `:5001` — since `appsettings.json` is loaded with `optional: false`, the effective port is always `:5002`.  
Both executables auto-open the browser on startup via `Process.Start`. Swagger UI is available at `/swagger` when running in Development mode.  
Both projects have `Properties/launchSettings.json` that sets `ASPNETCORE_ENVIRONMENT=Development` — required for `UseBlazorFrameworkFiles()` to serve WASM assets from the static web assets manifest (no physical `wwwroot` needed in the output folder).

## Architecture

Five projects in one solution:

### `HeartBeatProject.Shared` (Shared DTOs)
- DTOs shared by client and server: `StatusDto`, `SettingsDto`, `LogEntryDto`
- Namespace: `HeartBeatProject.Shared.Dtos`
- `SettingsDto` is a flat combined DTO: TX only uses `IntervalSeconds`/`OverwriteExisting`; RX only uses `CheckIntervalSeconds`/`ThresholdSeconds`. Both see all fields at runtime.

### `HeartBeatProject.Client` (Blazor WebAssembly)
- Assembly name kept as `HeartBeatProject` so no index.html changes were needed
- `HttpClient` pre-injected with the server base address
- **`AppState`** singleton (`AppState.cs` at project root) — caches the current mode (`IsTx`/`IsRx`) and fires `OnChange` when the mode is first resolved. Set by `MainLayout` on init and kept fresh by `Dashboard` polling. `NavMenu` and `Settings` subscribe to `OnChange` for reactive re-renders.
- Client-side polling: Dashboard every 3 s (+ 1 s countdown), Logs every 2 s, Settings once on init
- Pages: `Dashboard.razor`, `Settings.razor`, `Logs.razor`
- CSS animations (spin, pulse-green, pulse-red, pulse-yellow, pulse-live) in `wwwroot/css/app.css`
- `Settings.razor` exposes all editable fields, filtered by mode:
  - **TX fields**: FolderPath, IntervalSeconds, OverwriteExisting
  - **RX fields**: FolderPath, CheckIntervalSeconds, ThresholdSeconds
  - **Both**: full SMTP (server/port/from/to/username/password/ssl), SNMP (host/port/community), Syslog (host/port/facility)
  - API validation errors (HTTP 400) are surfaced inline as a red error banner
- `Dashboard.razor` shows mode-aware status labels and `StatusDto.Details` reason text

### `HeartBeatProject.Server` (Server Library — shared infrastructure)
- Class library (`Sdk.NET` + `FrameworkReference Microsoft.AspNetCore.App`)
- Namespace: `HeartBeatProject.Server.*`
- Contains: `Configuration/` (options), `Controllers/HeartbeatController`, `Logging/` (InMemoryLogStore, InMemoryNLogTarget), `Services/Alerts/` (all providers), `Services/RuntimeSettingsStore`, `Services/IHeartbeatStatusService`
- Controllers are discovered in executables via `AddApplicationPart(typeof(HeartbeatController).Assembly)`
- **RuntimeSettingsStore**: lock-based thread-safe in-memory store. `Get()` and `Update()` both deep-copy the DTO. `Update()` validates all fields including port ranges, email format, and blocks port 465 with `EnableSsl`.
- **Controller**: `GET /api/settings` masks SMTP password as `"********"`; `POST /api/settings` preserves password when mask is echoed back.

### `HeartBeatProject.Tx` (TX Executable)
- Namespace: `HeartBeatProject.Tx.Services`
- References: `HeartBeatProject.Server` + `HeartBeatProject.Client`
- Contains: `HeartbeatTxService`, `HeartbeatFileGenerator` (+ `IHeartbeatFileGenerator`), `TxHeartbeatStatusService`, `TxOperationalState`
- **`TxOperationalState`** — lock-based singleton shared between `HeartbeatTxService` (writes state) and `TxHeartbeatStatusService` (reads state). `HeartbeatTxService` calls `RecordSuccess()` on each successful write and `RecordFailure(isPathIssue, msg)` on exception. Status values: `STARTING` → `RUNNING` / `ERROR` / `DEGRADED` (path/permissions issues map to DEGRADED).
- Mode is implicit — no `Heartbeat:Mode` in appsettings, no mode routing in Program.cs

### `HeartBeatProject.Rx` (RX Executable)
- Namespace: `HeartBeatProject.Rx.Services`
- References: `HeartBeatProject.Server` + `HeartBeatProject.Client`
- Contains: `HeartbeatRxService`, `RxHeartbeatStatusService`, `RxOperationalState`
- **`RxOperationalState`** — lock-based singleton shared between `HeartbeatRxService` (writes state) and `RxHeartbeatStatusService` (reads state). `HeartbeatRxService` calls `RecordHealthy(fileName, ageSeconds)` or `RecordDown(reason)` on each check cycle. Status values: `STARTING` → `HEALTHY` / `DOWN`.
- Mode is implicit — no `Heartbeat:Mode` in appsettings, no mode routing in Program.cs

## Heartbeat Flow

```
TX:  HeartbeatTxService ──(every IntervalSeconds)──► IHeartbeatFileGenerator.GenerateAsync()
       OverwriteExisting=true  → writes heartbeat_latest.txt (single file, always fresh)
       OverwriteExisting=false → writes heartbeat_YYYYMMDD_HHmmss.txt (accumulating)
       On success → TxOperationalState.RecordSuccess()  → status: RUNNING
       On failure → TxOperationalState.RecordFailure()  → status: DEGRADED (path issues) | ERROR (other)
       Alert on first failure; re-alert every 5 min; recovery alert on next success.

RX:  HeartbeatRxService ──(every CheckIntervalSeconds)──► scans FolderPath for latest *.txt
       HEALTHY if file age ≤ ThresholdSeconds → RxOperationalState.RecordHealthy(file, age)
       DOWN otherwise                          → RxOperationalState.RecordDown(reason)
       Alert on HEALTHY→DOWN; re-alert every 5 min; recovery alert on DOWN→HEALTHY.
       Watchdog covers: folder missing, no files, stale file.
```

Status values exposed via `/api/status`:
- TX: `STARTING` (pre-first-write) → `RUNNING` | `DEGRADED` (path/permissions) | `ERROR` (write failed)
- RX: `STARTING` (pre-first-check) → `HEALTHY` | `DOWN`

`StatusDto.Details` carries a human-readable reason string (e.g. `"Threshold exceeded: 'heartbeat_latest.txt' is 95s old"`).

All timestamps use `DateTime.UtcNow`; file ages use `LastWriteTimeUtc`.  
Alert cooldown: `_lastAlertTime` initializes to `DateTime.MinValue` so the first DOWN fires immediately; recovery (`TransitionToHealthyAsync`) also resets it to `DateTime.MinValue` so the next DOWN after recovery also fires immediately.

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
| GET | `/api/status` | `StatusDto` — mode, status, **details**, lastHeartbeat, uptime, intervalSeconds |
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
    "OverwriteExisting": true,
    "LogFolderPath": ""
  },
  "Alerts": { ... }
}
```

**RX `appsettings.json`**:
```json
{
  "Urls": "http://localhost:5002",
  "Heartbeat": {
    "FolderPath": "C:\\Heartbeat\\Shared\\HeartbeatFiles",
    "FileNamePrefix": "heartbeat",
    "CheckIntervalSeconds": 10,
    "ThresholdSeconds": 60
  },
  "Alerts": { ... }
}
```

Note: `FileNamePrefix` and `LogFolderPath` are `HeartbeatOptions` fields read at startup — they are **not** in `SettingsDto` and cannot be changed via `POST /api/settings`.

`SyslogFacility` valid values: `Kernel`, `User`, `Daemons`, `Local0`–`Local7`.  
Port 465 + `EnableSsl: true` is rejected at validation time — use port 587 (STARTTLS).  
`To` supports semicolon- or comma-separated addresses.  
Runtime changes via `POST /api/settings` update `RuntimeSettingsStore` only — do not persist to disk.

## Publish

Each executable has a `Properties/PublishProfiles/Release-win-x64.pubxml` profile:
- `win-x64`, self-contained, single-file, `EnableCompressionInSingleFile=true`
- `DeleteExistingFiles=true` on every publish
- TX output → `publish/TX/`, RX output → `publish/RX/` (relative to solution root, gitignored)
- `appsettings.Development.json` is excluded from publish output (`CopyToPublishDirectory=Never`)

Publish output structure:
```
publish/TX/  (or RX/)
├── HeartBeatProject.Tx.exe   ← ~80–120 MB self-contained single file
├── appsettings.json
├── nlog.config
└── wwwroot/_framework/       ← Blazor WASM runtime (served to browser, cannot be bundled)
```

**Do not add `PublishTrimmed=true`** — NLog and SharpSnmpLib use reflection and are not trim-safe. The Blazor WASM `_framework/` files are already IL-linked by the SDK.

## Deployment

```
C:\Heartbeat\
├── TX\   → HeartBeatProject.Tx.exe + nlog.config + appsettings.json (Urls=:5000)
├── RX\   → HeartBeatProject.Rx.exe + nlog.config + appsettings.json (Urls=:5002)
└── Shared\HeartbeatFiles\   ← TX writes here, RX reads here
```

Register as Windows Services:
```
sc create HeartbeatTX binPath="C:\Heartbeat\TX\HeartBeatProject.Tx.exe" start=auto
sc create HeartbeatRX binPath="C:\Heartbeat\RX\HeartBeatProject.Rx.exe" start=auto
```

## Program.cs Startup Order

Both TX and RX `Program.cs` files use a two-phase startup pattern:

1. **Pre-builder phase** — an `earlyConfig` `ConfigurationBuilder` reads `appsettings.json` before `WebApplication.CreateBuilder`. This is used to configure NLog (load `nlog.config`), set `GlobalDiagnosticsContext` values (e.g. `SyslogHost`), and patch the `SyslogTarget`'s port and facility. Any new configuration that must be applied before the DI container builds (e.g. new GDC items, additional NLog target patching) must be added here.
2. **Builder phase** — standard `WebApplication.CreateBuilder` / `AddSingleton` / `MapControllers` / `app.Run()`.

The `InMemoryNLogTarget` (dashboard log feed) is also registered in the pre-builder phase, immediately after `nlog.config` is loaded, not in XML.

## Key Conventions

- Nullable reference types enabled across all projects
- All background service loops use `CancellationToken`; swallow all exceptions except `OperationCanceledException`
- Thread-safety: `RuntimeSettingsStore`, `InMemoryLogStore`, `TxOperationalState`, and `RxOperationalState` all use `lock`; `HeartbeatRxService` additionally uses `volatile bool` for HEALTHY/DOWN transition tracking
- New operational state fields: add to `TxOperationalState` (TX) or `RxOperationalState` (RX); both are registered as singletons and injected into the background service and the status service
- New alert providers: implement `IAlertService`, add to `CompositeAlertService` in `HeartBeatProject.Server`, register in both `Tx/Program.cs` and `Rx/Program.cs`
- New Blazor pages: add to `HeartBeatProject.Client/Pages/` with `@page "/route"`, link from `Layout/NavMenu.razor`
- New API endpoints: add to `HeartBeatProject.Server/Controllers/`; business logic in a new `Services/` class in that library
- New shared DTOs: add to `HeartBeatProject.Shared/Dtos/`
- `appsettings.Development.json` in TX and RX must contain only the `Logging` section — any other keys will override `appsettings.json` in Development mode and break configuration
- Physical folder names on disk may still be `HeartBeatProject.server` / `HeartBeatProject.shared` (lowercase) — Windows is case-insensitive so builds are unaffected; the csproj files inside are already renamed to `HeartBeatProject.Server.csproj` / `HeartBeatProject.Shared.csproj`
- `HeartBeatProject.Client/HeartBeatProject.csproj` is a stale leftover and is **not** referenced by the solution — the active file is `HeartBeatProject.Client/HeartBeatProject.Client.csproj`
