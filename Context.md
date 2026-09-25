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

**Updated 2026-08-27:** `UniFi:AuthorizeDurationMinutes` was pushed from a
"long" 30 days to UniFi's documented max (1,000,000 minutes ≈ 1.9 years)
— effectively permanent, on purpose. `GuestRevalidationBackgroundService`
is now the *only* thing that ever deactivates a member's device; the
duration is purely a last-resort ceiling in case that job is ever silently
broken for good. Considered adding a staleness safeguard (revoke access if
a guest hasn't successfully re-validated in N cycles) to keep that
worst-case bounded, and explicitly decided **not** to for now — a known,
accepted tradeoff, not an oversight. True "no expiry" (omitting
`timeLimitMinutes` entirely) was considered and rejected: UniFi documents
the field as optional but never documents what omitting it actually does,
so the verified documented max was used instead.

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

## Dual guest/member portal

**Implemented 2026-08-27.** The problem, verification, and decided approach
below are kept as-written for history; see "Implementation status" at the
end of this section for what actually shipped and what's still open.

**Problem discovered while configuring the real console (2026-08-26/27):**
UniFi's Guest/Hotspot Portal Type (Simple/Password/External/etc.) is **one
setting per site**, not per-SSID or per-WLAN-group (WLAN Groups were
replaced by AP Groups, which only control which APs broadcast an SSID —
unrelated to portal config). It applies to every guest-enabled SSID on the
site. There is no native way to give one SSID an Entra-gated external
portal while another keeps UniFi's built-in landing page.

The existing guest SSID currently uses UniFi's built-in landing page: an
AGB (terms) checkbox + a 24-hour authorization expiry, no identity check.
That network needs to keep working exactly as it does today. This app's
own SSID (`ffw-auth`, host `ffw-auth.gepf.at`) needs the full Entra-gated
flow. Since only one Portal Type can be active site-wide, both SSIDs will
end up redirected to the same External Portal Server URL once that's
turned on — so the split has to happen inside this app, not in UniFi.

**Verified before committing to this** (2026-08-27) — this constraint and
workaround are real, not a guess:
- A live Ubiquiti community thread is literally titled *"Feature request:
  Multiple Guest Portals based on SSID on the same site controller"* —
  its existence as an open feature request is itself strong evidence this
  doesn't work natively (nobody requests a feature that already exists).
- Independently, *"Two guest portals"*, *"Multiple Captive Portals (Guest
  Portals) on single AP"*, and *"Wifi Hotspot Portal for a single SSID is
  getting applied to all SSIDs for a site"* (all on community.ui.com) each
  separately describe the same one-Portal-Type-per-site behavior.
- The commonly cited workaround across those threads is exactly what we
  landed on: one external portal, branching on the SSID/MAC UniFi already
  passes as query params — not something invented for this project.
- Varying the UniFi authorization **duration per request** (30 days for
  members vs. 24h for guests) is already proven working in this
  codebase — `UniFiClientService.SendGuestActionAsync` sends
  `timeLimitMinutes` as an explicit per-call parameter today (see
  `Services/UniFiClientService.cs`), exercised by passing tests.
- Could not get a direct quote from Ubiquiti's own official docs in this
  pass — `help.ui.com` 403s automated fetches and `community.ui.com` is a
  JS-rendered SPA that doesn't return content without a real browser (see
  the existing "Do not fetch ai-gettingstarted.md-style pages" note
  above — same domain, same problem, not new). If anyone wants a
  zero-ambiguity check: open the console's Portal Type dropdown directly
  and confirm there's no per-SSID option.

**Decided approach:** one landing page in this app, with a choice between
two paths, shown to every guest regardless of which SSID they connected
to:

- **"I'm a fire department member"** — the existing flow, unchanged:
  Entra sign-in → `GatingService` group check → UniFi authorize for
  `UniFi:AuthorizeDurationMinutes` (30 days) → tracked in
  `AuthorizedGuest` for the background revalidation/offboarding job.
- **"I'm a guest"** (new) — AGB checkbox → UniFi authorize directly for a
  short duration (**24 hours**, confirmed) → no Entra sign-in, no
  `AuthorizedGuest` row (nothing to revalidate against; it just expires
  on UniFi's side, same as the current behavior).

**VLAN separation is orthogonal to all of this, and lives entirely in
UniFi** (verified 2026-08-27, before implementing): the guest-authorize
API (`AUTHORIZE_GUEST_ACCESS`) only accepts `timeLimitMinutes`,
`dataUsageLimitMBytes`, `rxRateLimitKbps`, `txRateLimitKbps` — no `vlan`
or `network` field, confirmed against UniFi's own help article content
and a third-party API client that mirrors it. VLAN is decided by which
SSID/Network a device associates with at Wi-Fi connection time, before
the captive portal even loads — nothing this app calls can move a device
to a different VLAN afterward. Practically: an "Internal" SSID and a
"Guest" SSID, each bound to its own Network/VLAN in the UniFi console,
gives real VLAN separation independent of which button someone clicks on
this portal's choice screen. **There is no VLAN setting in this app and
none is needed** — don't add one; point whoever asks at this paragraph.

**Implementation status (2026-08-27):**
- ✅ Choice screen (`ChoicePathScreen`), guest AGB screen (`GuestAgbScreen`),
  anonymous `GuestController` (`GET /api/guest/agb-text`,
  `POST /api/guest/authorize`), and `UniFi:GuestAuthorizeDurationMinutes`
  (default 1440 = 24h) are all built and tested (`dotnet test`: 42/42
  passing; `npm run build`/`npm run lint` clean).
- ✅ **AGB text is runtime-configurable**, not baked into the frontend
  bundle: `GuestAgbSettings.AgbText` (config section `GuestAgb`), served
  over the anonymous `GET /api/guest/agb-text` endpoint and fetched by the
  frontend's `useAgbText` hook. An operator changes it via
  `appsettings.json`/env var (e.g. `GuestAgb__AgbText`), no rebuild
  needed.
- ⏳ **Still blocked on real AGB text.** It ships with an obvious
  placeholder (`"TODO: replace with your organization's actual terms and
  conditions text."`) — nobody has invented real legal copy. Whoever
  operates this deployment needs to either pull whatever custom text is
  configured in the console's Hotspot Portal → Landing Page settings (if
  any was actually typed in there) or get real terms drafted by someone
  authorized to write binding legal text for the org, then set
  `GuestAgb:AgbText` accordingly.
- ⏳ **Don't flip UniFi's Portal Type to External Portal Server yet** —
  the app-side guest path exists now, but the actual console setting
  change (and binding the Internal/Guest SSIDs to their respective
  Networks/VLANs) is still a manual step for whoever administers that
  console, not yet done as of this writing.

**Security gap found and fixed (2026-09-25):** since both SSIDs share one
external portal, the choice screen shows both options to every visitor
regardless of which SSID they actually joined. UniFi's captive-portal
authorization is per-MAC, not per-network — it just unblocks whatever
network a device is already connected to (fixed at Wi-Fi association,
before the portal loads). That meant a device actually connected to the
**internal** SSID could click "I'm a guest," hit the anonymous
`POST /api/guest/authorize` endpoint, and get itself authorized — on the
internal network — without ever going through Entra sign-in or the
gating check, and with no `AuthorizedGuest` row for the revalidation job
to ever catch later.

Fixed by having `GuestController` verify, server-side, that the target
MAC is actually on the guest network before authorizing it — trusting a
frontend-supplied `ssid` param wouldn't work, since the anonymous
endpoint is callable directly (curl, devtools) with any MAC, bypassing
the frontend entirely. The verification itself can't be "is this MAC on
VLAN X" — **empirically confirmed against the live console (2026-09-25)**
that UniFi's client API is genuinely minimal: `GET /v1/sites/{siteId}/clients`
returns only `type, id, name, connectedAt, ipAddress, macAddress,
uplinkDeviceId, access.type` — no SSID, network, or VLAN field at all, for
any client. What *is* available and trustworthy (UniFi-reported, not
client-supplied) is the client's live-assigned `ipAddress`. Since the
guest network already has its own subnet (`10.10.60.0/24`), the app checks
that against a new `UniFi:GuestNetworkCidr` config value
(`UniFiClientService.AuthorizeGuestIfOnGuestNetworkAsync`) before ever
calling UniFi's authorize action — fails closed (refuses the request) if
the CIDR isn't configured or the client's IP can't be determined, rather
than silently trusting an unverified MAC. Verified live: a real device on
the internal 192.168.12.x subnet now gets `403 not_on_guest_network`
instead of being authorized.

Active VLAN *assignment* (the app moving a device onto a VLAN based on
which button is clicked, rather than just verifying) was considered and
rejected: UniFi's authorize action has no VLAN parameter (confirmed
earlier), and Wi-Fi VLAN membership is fixed at association time in
general — there's no confirmed way to move an already-connected wireless
client to a different VLAN without it reconnecting. Don't attempt to
"route" a device to a VLAN via this app; the VLAN split still has to be
real SSID→Network bindings done in UniFi, same as everywhere else in this
section.

**Already done on the UniFi side:** the `ffw-auth` SSID exists (Open
security, Hotspot application, Captive Portal), and the Hotspot Portal's
Landing Page redirect URL was being pointed at `https://ffw-auth.gepf.at`
as this was written — but per the above, don't actually switch Portal
Type over site-wide until the real AGB text is in place too.

## Stack / starting point
- ASP.NET Core (C#), `net10.0`, React/Vite/TypeScript frontend, `Microsoft.Identity.Web` for the OIDC flow.
- Scaffolded via Visual Studio's "React and ASP.NET Core" template:
  `Unifi-Entra-Portal.Server` (API) + `unifi-entra-portal.client` (SPA),
  wired together via the SPA proxy. Currently just the default template
  boilerplate (`WeatherForecastController`, default React page) — no
  UniFi/Entra-specific code yet.