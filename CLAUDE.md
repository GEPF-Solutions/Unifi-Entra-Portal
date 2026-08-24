# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A self-hosted, open-source external captive portal for a UniFi network that authenticates guests via Microsoft Entra ID sign-in, built in C# / ASP.NET Core (`net10.0`) with a React/Vite/TypeScript frontend (`Unifi-Entra-Portal.Server` + `unifi-entra-portal.client`), using `Microsoft.Identity.Web` for the OIDC flow. On sign-in, the backend server-side calls the UniFi Network API's `authorize-guest` command to authorize the client's MAC address. See `Context.md` for full background, rejected alternatives, and UniFi/Entra API specifics.

## Commit conventions

- **Never include "Co-Authored-By: Claude" or any mention of Claude in commit messages.** Write commit messages as if authored by the developer.

## Implementation Guidelines

**If anything is unclear, ask before implementing.**

### Open-source / secrets handling

- **This repo is published open-source.** Nothing org-specific (tenant ID, client ID/secret, redirect URIs, UniFi controller URL/site ID/local admin credentials, Entra group ID used for gating) may be committed. All of it goes through environment variables or `appsettings.Development.json` (gitignored). Ship an `appsettings.example.json` with placeholders whenever new config keys are added.
- Branding (logo, org name, colors, welcome text) must be config-driven / in a swappable folder, not hardcoded into HTML/Razor — other orgs will reuse this project.
- Gating logic (which Entra group/claims allow access) must be configurable, not hardcoded to a single group.

### Backend architecture

- **Follow a layered pattern:** Controller → Service → Repository. No business logic in controllers, no direct DB access in services.
- Every service and repository should have a matching interface (e.g. in an `Abstractions/` folder). Register both in DI.
- Keep DB entity models, API DTOs, and EF Core configuration in separate, clearly named folders (e.g. `Models/`, `Dto/`, `DbModel/`).
- New config keys added to `appsettings.json` should have a matching typed configuration class (e.g. under `Infrastructure/`), bound via `IOptions<T>`.
- Controller actions that do I/O should be `async` and accept a `CancellationToken`.

### Code style

- **Always write XML doc comment blocks (`/// <summary>`) for every method on the backend**, and JSDoc blocks (`/** */`) for every function/hook if/when a frontend is introduced. This applies to interfaces, implementations, and helpers alike.

### Frontend (if/when introduced)

- Data fetching belongs in a custom hook, not directly inside a component.
- Global state goes through framework-appropriate context/state management — avoid introducing a global state library unless there's a clear need.
- Keep one page/route component per file, with reusable UI split out separately.
