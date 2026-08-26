# UniFi + Entra Portal

A self-hosted external captive portal for UniFi networks that gates WiFi
access behind Microsoft Entra ID sign-in, instead of a click-through
agreement or a shared password.

Built for a volunteer fire department's member WiFi, where the alternatives
all fell short: UniFi's built-in guest hotspot has no Microsoft login option,
RADIUS/EAP-TLS means running your own CA and loses MFA support, and UniFi's
own SAML SSO requires installing an app most members wouldn't bother with.
This just redirects to a normal Microsoft login page in a browser, then
authorizes the device's MAC address on the UniFi side once sign-in succeeds
(and, optionally, once the signed-in account passes a group-membership
check). No agent, no app install, and Conditional Access / MFA still apply
because the actual authentication happens on Microsoft's side.

## How it works

1. A guest connects to an SSID with the captive portal enabled and gets
   redirected to this app, with their MAC address, AP, and site attached as
   query params.
2. The portal shows a "Sign in with Microsoft" button that kicks off a
   standard OpenID Connect flow against your Entra tenant.
3. Once signed in, the backend checks the user against your configured
   gating rule (e.g. Entra group membership), then calls UniFi's Network
   Integration API server-side to authorize that MAC address for a
   configurable duration.
4. The device stays authorized without hitting the portal again until that
   duration expires — or until a background job revokes it early, because a
   periodic job re-checks every authorized guest against Entra and
   deauthorizes anyone who's left the group or been disabled. That's the
   real offboarding mechanism, not just a timer.

## Stack

- **Backend:** ASP.NET Core (`net10.0`), `Microsoft.Identity.Web` for the
  OIDC flow, EF Core + SQLite for tracking authorized guests.
- **Frontend:** React + Vite + TypeScript.
- **UniFi side:** the official Network Integration API (API-key auth), not
  the legacy local-admin/cookie login — this also means it works against
  UniFi Fabric-enrolled consoles, which don't support local admin accounts
  at all.

```
Unifi-Entra-Portal.Server/    ASP.NET Core API (auth, UniFi client, portal endpoints)
unifi-entra-portal.client/    React/Vite/TS frontend
Unifi-Entra-Portal.Tests/     xUnit tests
```

## Running it locally

```bash
dotnet build
dotnet run --project Unifi-Entra-Portal.Server/Unifi-Entra-Portal.Server.csproj
dotnet test
```

The .NET dev server proxies the Vite frontend, so one `dotnet run` is
enough for both. The SQLite database is created and migrated automatically
on startup.

## Configuration

Nothing org-specific lives in the repo — copy the placeholder keys from
[`appsettings.example.json`](Unifi-Entra-Portal.Server/appsettings.example.json)
into `appsettings.Development.json` (gitignored) for local dev, or set them
as environment variables in production (`AzureAd__ClientSecret`, etc.).

You'll need:

- **An Entra app registration** for the portal, used both for the OIDC
  sign-in flow and, app-only via client credentials, for the background
  re-validation job (grant it `User.Read.All`, and `GroupMember.Read.All`
  if you're using group-based gating, with admin consent).
- **A UniFi API key** — either created locally on the console (Network app
  → Settings → Control Plane → Integrations) or through Site Manager
  (unifi.ui.com → Settings → API Keys). The Site Manager key is what you
  need if this app doesn't have a direct network path to your console,
  since it routes calls through Ubiquiti's cloud connector instead.
- **Your UniFi site ID**, a UUID rather than the old `default` slug — see
  `GET .../network/integration/v1/sites` and match on `internalReference`.
- **A gating rule**, if you want one — `Gating.AllowedGroupIds` in config
  restricts access to specific Entra security groups. Leave it empty to
  allow any account in the tenant.
- **`ForwardedHeaders.TrustAllProxies = true`** (e.g. `ForwardedHeaders__TrustAllProxies=true`),
  but only if this instance sits exclusively behind a reverse proxy/ingress
  that strips client-supplied `X-Forwarded-*` headers before forwarding its
  own (an OpenShift Route, most Kubernetes Ingress controllers). It's `false`
  by default because trusting these headers unconditionally lets a client
  spoof `X-Forwarded-Proto: https` and bypass HTTPS redirection on any
  deployment that doesn't have that guarantee — which includes local dev
  and exposing Kestrel directly.

Branding (org name, welcome text, logo) lives in
[`unifi-entra-portal.client/src/branding/config.ts`](unifi-entra-portal.client/src/branding/config.ts)
so re-skinning the portal for a different org doesn't touch any component
code.

On the UniFi side, set the SSID's captive portal type to "External Portal
Server" pointing at this app's public URL, and make sure the walled garden
allows Microsoft's OAuth domains (`login.microsoftonline.com`,
`graph.microsoft.com`) so the sign-in redirect can actually complete before
the device is authorized.

## A couple of things worth knowing

- The portal needs a real, publicly trusted HTTPS certificate. Devices in
  the pre-auth captive state generally won't complete an OAuth redirect
  against a self-signed one.
- UniFi authorizes by MAC address, not user identity, so set a long
  authorization window (weeks/months) — the revalidation job is what
  actually handles offboarding, not a short expiry.
- Some devices use MAC randomization and will occasionally show up as
  "new" and need to re-authenticate. That's expected, not a bug.

## License

Apache-2.0 — see [LICENSE](LICENSE).
