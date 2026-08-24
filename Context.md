# Project brief: UniFi + Entra ID captive portal

## Goal
A self-hosted, open-source external captive portal for a UniFi network that
authenticates guests via **Microsoft Entra ID (Azure AD) sign-in**, built in
**C# / ASP.NET Core**. Primary use case is a volunteer fire department's
member WiFi, but the project should stay generic/reusable, not hardcoded to
that org.

## Why this approach (context on what was ruled out)
- **FreeRADIUS + step-ca + EAP-TLS** — technically solid but requires
  building/maintaining a CA, a certificate enrollment portal, and loses MFA
  support (PEAP-MSCHAPv2 doesn't support it, and Entra doesn't store NT
  hashes needed for that protocol anyway). Too much ongoing operational
  burden for a volunteer org.
- **UniFi's native Fabric/Identity One-Click WiFi (SAML SSO)** — works, but
  requires members to install the UniFi Endpoint app. Rejected because the
  target membership isn't tech-savvy and app-install friction kills adoption.
- **UniFi's built-in Guest Hotspot portal** — no native Microsoft/Entra login
  option (only Facebook/Google/PayPal natively).
- **Paid third-party portals** (Art of WiFi, Spotipo, etc.) — do exactly this
  as a commercial product, confirming the approach works, but cost is a
  blocker and it's proprietary/not self-hosted-and-owned.
- **Conclusion**: build a small, free, self-hosted **external captive
  portal** using UniFi's documented external portal API + standard Microsoft
  OAuth. This keeps MFA (Conditional Access applies at the Microsoft
  sign-in step, before the user returns to the portal), requires no app
  install (just a browser redirect), and is a much smaller build than the
  RADIUS route.

## How UniFi's external captive portal mechanism works
1. Guest connects to an SSID with Hotspot/Captive Portal enabled, external
   portal type set to "External Portal Server" pointing at our app's URL.
2. Client is tagged `GUEST`, `authorized: false` — a "pending" state where it
   can only reach whitelisted (walled garden) domains.
3. Any web request redirects the client to our portal URL (query params
   include the client's MAC, AP MAC, site, and original destination URL).
4. Our portal shows "Sign in with Microsoft" → standard OAuth 2.0 /
   Microsoft.Identity.Web flow against the org's Entra tenant.
5. On successful sign-in, **server-side** (not client-side JS — CORS blocks
   direct browser calls to the controller), our backend calls the UniFi
   Network API's `authorize-guest` command with the client's MAC address and
   a duration, authorizing the device.
6. Device is authorized going forward for that MAC, for the configured
   duration — no repeat portal visits until expiry or explicit revocation
   (see persistence section below).

## UniFi API specifics to get right
- **This deployment's console is enrolled in UniFi Fabric**, which does not
  support local admin accounts at all — the originally planned "dedicated
  local admin account + cookie/CSRF login" approach (classic
  `/api/s/{site}/cmd/stamgr`) is a dead end here. Superseded by **API key
  auth against the official UniFi Network Integration API** instead — see
  `UniFiSettings`/`UniFiClientService` for the actual implementation. If a
  future deployment targets a non-Fabric console, that classic approach
  remains an option, but isn't what this codebase does.
- **Auth**: `X-API-Key: <key>` header, no login/session/CSRF handshake at
  all. Key is created either locally on the console (Network app → Settings
  → Control Plane → Integrations) or via Site Manager
  (unifi.ui.com → Settings → API Keys) — the latter is required whenever
  this app has no direct network path to the controller (the normal case
  for an off-site-hosted portal), and routes calls through Ubiquiti's cloud
  connector (`api.ui.com`) rather than hitting the console directly.
- **Site ID is a UUID** under this API, not the classic "default" slug —
  fetch it from `GET .../network/integration/v1/sites` and match on
  `internalReference`.
- **Clients are addressed by an internal UUID, not MAC** — authorizing a
  guest requires first looking up that UUID by filtering the site's
  connected-clients list on `macAddress.eq('<mac>')`, then
  `POST .../sites/{siteId}/clients/{clientId}/actions` with
  `{"action":"AUTHORIZE_GUEST_ACCESS","timeLimitMinutes":<n>}` (or
  `UNAUTHORIZE_GUEST_ACCESS` to revoke, no time limit needed).
- **Base URL shape**:
  `https://api.ui.com/v1/connector/consoles/{consoleId}/proxy/network/integration/v1/...`
  when using the cloud connector (`consoleId` from `GET https://api.ui.com/v1/hosts`),
  or `https://{consoleIP}/proxy/network/integration/v1/...` for direct
  local/VPN access.
- Full reference: https://developer.ui.com/network (the "Execute Client
  Action", "List Connected Clients", and "Connector" pages cover everything
  above). Do not fetch `ai-gettingstarted.md`-style pages linked from that
  site through an automated summarizer — one such page triggered a
  prompt-injection refusal when fetched that way during this project's
  development; reading the rendered docs directly in a browser was fine.
- Confirm the controller is patched past **CVE-2026-54405** (fixed in UniFi
  Network 10.4.57+) before poking at the API surface.

## Entra ID / OAuth side
- New **App Registration** dedicated to this portal (separate from any
  existing Fabric/SAML setup), minimal scope — just enough to identify the
  signed-in user (and optionally check group membership).
- Decide on **gating**: restrict to a specific Entra security group (real
  members only) vs. any valid tenant account. Should probably be
  configurable rather than hardcoded, since other orgs using this project
  will want different scoping.
- Walled garden must allow Microsoft's OAuth domains
  (`login.microsoftonline.com`, `graph.microsoft.com`, etc.) reachable
  pre-authentication or the redirect flow hangs.

## Persistence / no repeat auth
- UniFi tracks authorization by **MAC address + duration**, not "user
  identity" — so set a long authorization window (weeks/months) rather than
  a short guest default, so devices don't hit the portal on every connect.
- Combine with a **background job** that periodically re-validates against
  Entra (group membership / account enabled) and calls `unauthorize-guest`
  for anyone no longer eligible — this is the offboarding mechanism, and
  it's actually a better model than a long fixed expiry alone.
- Known non-issue, already discussed and accepted: some devices use
  privacy MAC randomization and may occasionally present a new MAC,
  triggering a one-off re-auth. Not something to engineer around — treated
  as an acceptable, occasional inconvenience, not a bug.

## Hosting requirements
- Public, real HTTPS endpoint with a trusted (non-self-signed) certificate —
  devices in the pending/pre-auth state generally won't complete an OAuth
  redirect against a self-signed cert.
- Hosting location (VM, container, homelab) not yet finalized.

## Open source considerations (this will be published)
- **Nothing fire-department-specific in the repo.** Tenant ID, client
  ID/secret, redirect URIs, UniFi console/site IDs, the UniFi API key,
  any Entra group ID used for gating — all via environment
  variables or `appsettings.Development.json` (gitignored), never
  committed. Ship `appsettings.example.json` with placeholders.
- Branding (logo, org name, colors, welcome text) should be config-driven /
  in a swappable `branding/` folder, not hardcoded HTML strings — makes the
  project genuinely reusable by other orgs.
- `.gitignore` and clean git history from the very first commit — don't
  want to scrub secrets out of history later.
- License: Apache-2.0 (see `LICENSE`).
- Gating logic should ideally support arbitrary claims/group config rather
  than assuming exactly one hardcoded group — barely more work, much more
  useful to other adopters.

## Naming
Not finalized yet. Candidates discussed: `entra-gatekeeper`,
`unifi-entra-portal`, `turnstile`, `vestibule`. Leaning toward something
short/literal for discoverability (e.g. `unifi-entra-portal` or
`entra-gatekeeper`), but open to a more distinctive name if the project
scope grows beyond just UniFi+Entra later.

## Stack / starting point
- ASP.NET Core (C#), `net10.0`, React/Vite/TypeScript frontend, `Microsoft.Identity.Web` for the OIDC flow.
- Scaffolded via Visual Studio's "React and ASP.NET Core" template:
  `Unifi-Entra-Portal.Server` (API) + `unifi-entra-portal.client` (SPA),
  wired together via the SPA proxy. Currently just the default template
  boilerplate (`WeatherForecastController`, default React page) — no
  UniFi/Entra-specific code yet.