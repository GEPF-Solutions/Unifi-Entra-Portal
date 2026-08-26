# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A self-hosted, open-source external captive portal for a UniFi network that authenticates guests via Microsoft Entra ID sign-in, built in C# / ASP.NET Core (`net10.0`) with a React/Vite/TypeScript frontend, using `Microsoft.Identity.Web` for the OIDC flow. On sign-in, the backend server-side calls the UniFi Network API's `authorize-guest` command to authorize the client's MAC address. See `Context.md` for full background, rejected alternatives, and UniFi/Entra API specifics.

**Solution structure:**
- `Unifi-Entra-Portal.Server/` — ASP.NET Core Web API (auth, UniFi API client, captive portal endpoints)
- `unifi-entra-portal.client/` — React + Vite + TypeScript frontend
- `Unifi-Entra-Portal.Tests/` — xUnit test project

## Build & Run Commands

```bash
dotnet build
dotnet run --project Unifi-Entra-Portal.Server/Unifi-Entra-Portal.Server.csproj
dotnet test
```

The SPA proxy serves the React frontend through the .NET dev server.

Database: SQLite (file-based, no separate server) via `Microsoft.EntityFrameworkCore.Sqlite`. Migrations are applied automatically at startup (`Database.Migrate()` in `Program.cs`) — no manual `dotnet ef database update` step needed. To add a schema change: update the entity/`PortalDbContext`, then run `dotnet ef migrations add <Name> --output-dir DbModel/Migrations` from `Unifi-Entra-Portal.Server/`.

## Commit conventions

- **Never include "Co-Authored-By: Claude" or any mention of Claude in commit messages.** Write commit messages as if authored by the developer.

## Implementation Guidelines

**If anything is unclear, ask before implementing.**

### Open-source / secrets handling

- **This repo is published open-source.** Nothing org-specific (tenant ID, client ID/secret, redirect URIs, UniFi console/site IDs, UniFi API key, Entra group ID used for gating) may be committed. All of it goes through environment variables or `appsettings.Development.json` (gitignored). Ship an `appsettings.example.json` with placeholders whenever new config keys are added.
- Branding (logo, org name, colors, welcome text) must be config-driven / in a swappable folder, not hardcoded into HTML/Razor — other orgs will reuse this project.
- Gating logic (which Entra group/claims allow access) must be configurable, not hardcoded to a single group.

### Backend architecture

- **Follow a layered pattern:** Controller → Service → Repository. No business logic in controllers, no direct DB access in services.
- Every service and repository should have a matching interface (e.g. in an `Abstractions/` folder). Register both in DI.
- Keep DB entity models, API DTOs, and EF Core configuration in separate, clearly named folders (e.g. `Models/`, `Dto/`, `DbModel/`).
- New config keys added to `appsettings.json` should have a matching typed configuration class (e.g. under `Infrastructure/`), bound via `IOptions<T>`.
- Controller actions that do I/O should be `async` and accept a `CancellationToken`.
- A service that calls out over HTTP (e.g. `UniFiClientService`) should accept an optional `HttpMessageHandler` constructor parameter (defaulting to null/production behavior) so tests can substitute a fake handler instead of hitting a real server.

### Code style

- **Always write XML doc comment blocks (`/// <summary>`) for every method on the backend**, and JSDoc blocks (`/** */`) for every function/hook if/when a frontend is introduced. This applies to interfaces, implementations, and helpers alike.
- **Never write single-line conditionals** (e.g. `if (x) return;`, `if (x) foo();`) in C# or TypeScript/TSX. Always brace the body on its own line, even for a single early-return statement.

### Frontend

- Data fetching belongs in a custom hook, not directly inside a component.
- Global state goes through framework-appropriate context/state management — avoid introducing a global state library unless there's a clear need.
- Keep one page/route component per file, with reusable UI split out separately.
