# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HeartBeatProjectInstaller** is a WPF desktop installer application targeting `net8.0-windows`. It is a companion to the main HeartBeatProject solution (Blazor WASM + ASP.NET Core), found in the sibling `HeartBeatProject/` directory.

Solution file: `HeartBeatProjectInstaller.slnx`

## Commands

```bash
# Build the installer
dotnet build HeartBeatProjectInstaller.slnx

# Run the installer
dotnet run --project HeartBeatProjectInstaller.csproj
```

> Must be built/run on Windows — `net8.0-windows` target-framework is Windows-only.

## Architecture

Single WPF project (`OutputType=WinExe`, `UseWPF=true`):

- `App.xaml` / `App.xaml.cs` — application entry point and lifecycle
- `MainWindow.xaml` / `MainWindow.xaml.cs` — main installer UI window (currently a skeleton)

## Key Conventions

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- UI is defined in XAML; code-behind in the corresponding `.xaml.cs` partial class
- This project targets Windows only — do not add cross-platform dependencies
