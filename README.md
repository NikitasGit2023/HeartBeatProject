# HeartBeat Monitor

A Windows health-monitoring system built on .NET 8 and Blazor WebAssembly.  
**TX** writes timestamped heartbeat files on a configurable interval. **RX** monitors the shared folder and sends alerts when files stop arriving or grow stale.

---

## Table of Contents

1. [How It Works](#1-how-it-works)
2. [Requirements](#2-requirements)
3. [Installation](#3-installation)
4. [Running the Application](#4-running-the-application)
5. [Web UI](#5-web-ui)
6. [Settings](#6-settings)
7. [Alerts](#7-alerts)
8. [Logs](#8-logs)
9. [Development & Build](#9-development--build)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. How It Works

```
TX machine  →  writes heartbeat files every N seconds  →  Shared Folder
RX machine  →  reads shared folder every N seconds     →  alerts if files are too old
```

TX and RX communicate only through the shared folder — no direct network connection between them is required. Both sides expose a browser-based dashboard for monitoring and configuration.

### File naming

| Mode | File written |
|---|---|
| `OverwriteExisting = false` (default) | `heartbeat_YYYYMMDD_HHmmss.txt` — one file per write cycle |
| `OverwriteExisting = true` | `heartbeat_latest.txt` — single file, always overwritten |

---

## 2. Requirements

### End-user machine

| Requirement | Detail |
|---|---|
| Operating System | Windows 10 (64-bit) or later; Windows Server 2016 or later |
| .NET Runtime | Not required — executables are self-contained |
| Disk space | ~150 MB per mode (TX or RX) |
| Privileges | Administrator (required for Windows Service registration) |
| Shared folder | A folder accessible by both TX and RX (local path, UNC path, or mapped drive) |
| Firewall | TCP port `5000` open for TX dashboard; `5002` for RX dashboard (only if accessed remotely) |

### Development machine (build only)

- .NET SDK 8.0 or later
- Inno Setup 6.3 or later (for building the installer)

---

## 3. Installation

### Using the installer (recommended)

1. Run `HeartBeatMonitor-Setup-1.0.0.exe` as **Administrator**.
2. Follow the wizard:

| Step | What to do |
|---|---|
| **Mode** | Choose **TX** (sender) or **RX** (monitor) |
| **Folder Path** | Path to the shared heartbeat folder (must be reachable by both machines) |
| **SMTP Settings** | Optional — configure email alerts during install |
| **Summary** | Review your choices before installation begins |
| **Install Directory** | Default: `C:\Heartbeat` |

3. The installer will:
   - Copy the TX or RX executable and its supporting files
   - Create the shared folder `C:\Heartbeat\Shared\HeartbeatFiles\` if it does not exist
   - Write a configured `appsettings.json` from your wizard inputs
   - Register a Windows Service (`HeartbeatTX` or `HeartbeatRX`) set to **Automatic** start
   - Start the service and open the dashboard in your browser

> **Reinstalling to the same folder:** You will be asked whether to keep or replace your existing configuration. Choosing **Keep** preserves `appsettings.json` and `nlog.config`; the executable is always updated.

### Uninstalling

Open **Add or Remove Programs**, find **HeartBeat Monitor**, and click **Uninstall**.  
You will be asked: _"Do you want to keep your existing settings?"_  
- **Yes** — binaries and service are removed; configuration files are preserved  
- **No** — everything is removed

---

## 4. Running the Application

### As a Windows Service (normal use)

The service starts automatically on boot after installation. To control it manually:

```cmd
sc start HeartbeatTX      sc stop HeartbeatTX
sc start HeartbeatRX      sc stop HeartbeatRX
```

Or open **Services** (`services.msc`) and locate **HeartbeatTX** / **HeartbeatRX**.

### Running directly (development)

```bash
dotnet run --project HeartBeatProject.Tx/HeartBeatProject.Tx.csproj
dotnet run --project HeartBeatProject.Rx/HeartBeatProject.Rx.csproj
```

### Default URLs

| Mode | URL |
|---|---|
| TX Dashboard | http://localhost:5000 |
| RX Dashboard | http://localhost:5002 |

---

## 5. Web UI

Both TX and RX serve the same Blazor WebAssembly interface. The UI adapts its labels and visible fields based on the running mode.

### Dashboard

Refreshes every 3 seconds and shows:

| Field | Description |
|---|---|
| **Status** | Current operational state |
| **Details** | Plain-English reason for any non-healthy status |
| **Last Heartbeat** | UTC timestamp of the last successful file write (TX) or healthy check (RX) |
| **Uptime** | Time elapsed since the service started |
| **Interval** | Configured write or check interval in seconds |

**TX status values:**

| Status | Meaning |
|---|---|
| `STARTING` | Service has not yet written its first file |
| `RUNNING` | Last file write succeeded |
| `DEGRADED` | Write failed due to a path or permissions problem |
| `ERROR` | Write failed for an unexpected reason |

**RX status values:**

| Status | Meaning |
|---|---|
| `STARTING` | Service has not yet completed its first check |
| `HEALTHY` | Latest file age is within the configured threshold |
| `DOWN` | Folder missing, no files found, or latest file exceeds threshold |

---

## 6. Settings

All settings can be changed at runtime from the **Settings** page — no service restart required.

> Runtime changes are held in memory only. To make them permanent, edit `appsettings.json` in the install folder and restart the service.

### Heartbeat settings

| Field | Applies to | Description |
|---|---|---|
| Folder Path | TX, RX | Directory where heartbeat files are written (TX) or monitored (RX) |
| Interval (seconds) | TX | How often TX writes a file (1–86400) |
| Overwrite Existing | TX | `false` (default) = timestamped files; `true` = single overwritten file |
| Check Interval (seconds) | RX | How often RX scans the folder (1–86400) |
| Threshold (seconds) | RX | Maximum acceptable file age; must be greater than Check Interval |

### Alert settings (both modes)

| Field | Description |
|---|---|
| Enable Email / SNMP / Syslog | Master switch per channel; saves immediately on click |
| SMTP Server, Port, From, To | Email delivery settings |
| Enable SSL | Use STARTTLS; required for port 587 |
| Username, Password | SMTP authentication |
| SNMP Host, Port, Community | SNMPv2c trap destination |
| Syslog Host, Port, Facility | Syslog UDP destination |

Boolean toggles (Enable Email, Enable SNMP, etc.) **save automatically on click**.  
All other fields require pressing **Save Changes** at the bottom of the page.

---

## 7. Alerts

Three channels fire in parallel on every alert event:

### Email (SMTP)

- Use port **587** with `Enable SSL: true` (STARTTLS)
- Port **465** (implicit SSL) is not supported
- The `To` field accepts multiple addresses separated by `;` or `,`
- Failed sends are retried up to 3 times with a 2-second delay
- For Gmail, use an [App Password](https://support.google.com/accounts/answer/185833) instead of your account password

### SNMP Trap (SNMPv2c)

- Sends a trap to the configured host and port (default: `162`)
- Uses enterprise OID `1.3.6.1.4.1.99999`
- Community string default: `public`

### Syslog (UDP)

- Sends a UDP message to the configured host and port (default: `514`)
- Valid facility values: `Kernel`, `User`, `Daemons`, `Local0` – `Local7`

### Alert timing

| Event | Behaviour |
|---|---|
| First failure | Alert fires immediately |
| Repeated failure | Re-alerts every 5 minutes |
| Recovery (DOWN → HEALTHY) | Recovery alert fires immediately; cooldown resets |

---

## 8. Logs

The **Logs** page shows the last 200 log entries, most-recent first, auto-refreshed every 2 seconds.

Log files are written to the `Logs\` subfolder inside the install directory — one file per day (`heartbeat_YYYYMMDD.txt`), retained for 30 days.

---

## 9. Development & Build

```bash
# Build the entire solution
dotnet build HeartBeatProject.slnx

# Publish TX  →  installer/publish/Tx/
dotnet publish HeartBeatProject.Tx/HeartBeatProject.Tx.csproj /p:PublishProfile=FolderProfile

# Publish RX  →  installer/publish/Rx/
dotnet publish HeartBeatProject.Rx/HeartBeatProject.Rx.csproj /p:PublishProfile=Release-win-x64

# Publish both at once
.\publish-all.ps1
```

After publishing, build the installer:

```cmd
iscc installer\installer.iss
```

Output: `installer/output/HeartBeatMonitor-Setup-1.0.0.exe`

---

## 10. Troubleshooting

| Problem | Likely cause | Fix |
|---|---|---|
| TX shows `DEGRADED` | Folder does not exist or service has no write permission | Create the folder or adjust NTFS permissions |
| RX shows `DOWN` | TX stopped writing, or shared folder is unreachable | Check TX service status and folder accessibility |
| No alert emails | Wrong SMTP settings or port | Use port 587 + SSL. For Gmail, use an App Password |
| SNMP traps not received | Wrong host/port or firewall blocking UDP 162 | Verify receiver config and firewall rules |
| Dashboard blank / 404 | Service not running | Open `services.msc` and start HeartbeatTX or HeartbeatRX |
| Port already in use | Another process on `:5000` or `:5002` | Edit `"Urls"` in `appsettings.json` and restart the service |
| Settings lost after restart | Runtime changes are in-memory only | Edit `appsettings.json` directly to persist changes |
