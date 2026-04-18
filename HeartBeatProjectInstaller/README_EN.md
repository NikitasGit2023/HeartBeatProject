# HeartBeat Monitoring System

A .NET 8 application for monitoring system health using a file-based heartbeat mechanism. The system detects when a remote process stops responding and sends email alerts.

---

## Overview

Many one-way systems (automated jobs, background processes, remote agents) run silently with no built-in feedback. If they stop, no one knows until something fails downstream.

HeartBeat solves this by having the monitored process write a small file at regular intervals. A separate monitor checks for that file — if it goes missing or becomes too old, an alert is sent.

---

## Architecture

The system has two roles:

| Role | Name | Responsibility |
|------|------|----------------|
| **TX** | Transmitter | Writes a timestamped heartbeat file at a configured interval |
| **RX** | Receiver | Monitors a folder for that file; alerts if it is missing or stale |

In a real deployment, TX and RX run on **separate machines** sharing a network folder. In this project, both are **simulated on the same machine** using two different local folders — one for writing, one for monitoring.

---

## Features

- **Heartbeat generation** — TX writes a timestamped `.txt` file at a configurable interval
- **Threshold monitoring** — RX flags the system as DOWN if the latest file exceeds a configured age
- **Email alerts** — SMTP alert on HEALTHY → DOWN transition, with a 5-minute cooldown to prevent flooding
- **Live logging** — All service events are captured in a categorized in-memory log (TX / RX / Email / System)
- **Web UI** — Browser-based dashboard showing system status, settings, and live log stream
- **Installer** — Step-by-step WPF wizard that configures and installs the system as a Windows Service

---

## System Requirements

| Requirement | Details |
|---|---|
| **Operating System** | Windows 10, Windows 11, or Windows Server |
| **Permissions** | Administrator privileges required for installation (needed to register the Windows Service) |
| **Runtime** | None — the application is published as a self-contained executable, no .NET installation needed |
| **Network** | Access to `http://localhost:5000` for the Web UI |

> The application runs as a Windows Service and uses Windows-specific APIs, so it cannot run on Linux or macOS.

---

## Installation

### 1. Publish the server

```bash
dotnet publish HeartBeatProject.server/HeartBeatProject.server.csproj -c Release -r win-x64 --self-contained true
```

### 2. Run the installer

Launch `HeartBeatProjectInstaller.exe` as Administrator and follow the wizard:

1. **Welcome** — introduction
2. **Configure** — choose TX or RX mode, set folder paths, intervals, and optional SMTP credentials
3. **Install** — the wizard writes `appsettings.json` and registers the Windows Service
4. **Finish** — the service starts automatically

---

## Usage

After installation, the application does not start automatically. To run it:

1. Navigate to the installation/publish folder
2. Run `HeartBeatProject.exe`

Once running, open a browser and navigate to:

```
http://localhost:5000
```

---

## Configuration

The service is configured via `appsettings.json`:

```json
{
  "Heartbeat": {
    "Mode": "TX",
    "FolderPath": "C:\\HeartbeatFiles",
    "IntervalSeconds": 30,
    "CheckIntervalSeconds": 10,
    "ThresholdSeconds": 60
  },
  "Alerts": {
    "EnableEmail": true,
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "From": "monitor@example.com",
    "To": "admin@example.com",
    "Username": "",
    "Password": "",
    "EnableSsl": true
  }
}
```

---

## Notes

- TX and RX are designed for separate machines but run on the same machine in this project for simulation
- Email alerts require full SMTP credentials — if any field is missing, alerts are skipped and a warning is logged
- Additional alert providers (Syslog, SNMP) can be added by implementing the `IAlertService` interface

---

## Technologies

- .NET 8
- ASP.NET Core (Web API + Blazor WebAssembly)
- BackgroundService (Worker Services)
- SMTP (System.Net.Mail)
- WPF (Installer UI)
