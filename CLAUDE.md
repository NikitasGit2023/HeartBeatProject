# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HeartBeatProject** is a full-stack .NET 8 application using Blazor WebAssembly (client) + ASP.NET Core Web API (server), with a shared library for common models/DTOs.

Solution file: `HeartBeatProject/HeartBeatProject.slnx`

## Commands

```bash
# Build entire solution
dotnet build HeartBeatProject/HeartBeatProject.slnx

# Run the server (serves both API and Blazor WASM client)
dotnet run --project HeartBeatProject.server/HeartBeatProject.server.csproj

# Run client in dev mode (hot reload)
dotnet watch --project HeartBeatProject/HeartBeatProject.csproj
```

**Default URLs after `dotnet run` on the server:**
- HTTP: http://localhost:5295
- HTTPS: https://localhost:7266
- Swagger UI: https://localhost:7266/swagger

## Architecture

Three projects in one solution:

### `HeartBeatProject` (Client — Blazor WebAssembly)
- Entry point: `Program.cs` registers services and mounts `App.razor`
- Routing is defined in `App.razor`; pages live in `Pages/`
- `HttpClient` is pre-injected for API calls — base address points to the server
- Static assets served from `wwwroot/`

### `HeartBeatProject.server` (Server — ASP.NET Core)
- Entry point: `Program.cs` configures middleware, Swagger, and static WASM file hosting
- API controllers live in `Controllers/` (currently `WeatherForecastController`)
- Hosts the Blazor WASM app via `UseBlazorFrameworkFiles()` + `MapFallbackToFile("index.html")`
- Swagger is enabled in Development via `/swagger`

### `HeartBeatProject.shared` (Shared Library)
- Intended for models and DTOs shared between client and server
- Currently a placeholder — add shared types here to avoid duplication

## Key Conventions

- Nullable reference types are enabled across all projects (`<Nullable>enable</Nullable>`)
- Client components use `@inject HttpClient Http` to call the server API
- New API endpoints go in `HeartBeatProject.server/Controllers/`
- New Blazor pages go in `HeartBeatProject/Pages/` with a corresponding `@page "/route"` directive
- Shared data models should live in `HeartBeatProject.shared` and be referenced by both client and server projects
