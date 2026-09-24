# Handoff: WLAN Captive Portal (Member / Guest)

## Overview
An open-source captive portal shown when a device joins the Wi‑Fi. The first customer is Feuerwehr Fußach (a volunteer fire department in Austria). Visitors choose one of two paths:

- **Member**: redirect to Microsoft 365 (Entra ID) sign-in. On success, the device is placed in the **member VLAN**.
- **Guest**: accept the terms of use. The device is then placed in the **guest VLAN** for a limited session.

There is **no admin UI and no user management**. All branding, text and network settings come from **environment variables** on the Kubernetes deployment. Images and long text are files mounted from a ConfigMap.

## About the Design Files
The files in `design/` are **design references built in HTML**: prototypes that show the intended look and behavior. They are not production code. Recreate them in the target codebase's stack. If no stack exists yet, pick one that suits a small, server-rendered captive portal (plain server-rendered HTML + minimal JS is ideal, because captive-portal browsers on iOS/Android/macOS are restricted mini-browsers). The `.dc.html` files use a custom template runtime (`support.js`). Read them for markup and inline styles. Don't port the runtime.

## Fidelity
**High-fidelity.** Colors, type, spacing, radii, copy and interactions are final. Recreate them closely.

## Screens / Views
The portal is a single page with four states (steps). It has two layouts: **phone** (stacked) and **laptop / wide** (side-by-side).

### Shared shell
- Root: full viewport, background `#161826`, text `#e9e9ed`, font Inter.
- CSS custom property `--color-accent` is set from `PORTAL_ACCENT` on the root. All accent usages read it.
- **Hero area** (image region):
  - Phone: top band, `flex: 0 0 290px` on the Choose step, shrinking to `0 0 120px` on the other steps (`transition: flex-basis .35s ease`).
  - Wide (≥ ~900px): left column, `flex: 1 1 auto`, full height. The content panel is a fixed `480px` on the right.
  - Background when there's no image: `repeating-linear-gradient(135deg, #232532 0 10px, #161826 10px 20px)`.
  - The image is `object-fit: cover` with `mix-blend-mode: lighten`, so dark areas fall into the page.
  - Fade overlay:
    - Phone: `linear-gradient(to bottom, rgba(22,24,38,.45), transparent 35%, #161826)`.
    - Wide: `linear-gradient(to right, transparent 55%, #161826)` plus a top darkening `linear-gradient(to bottom, rgba(22,24,38,.45), transparent 30%)`.
- **Top row inside hero** (absolute, 16px inset, space-between):
  - **Logo**: img height 30px (phone) / 44px (wide). If `PORTAL_LOGO_PLATE=true`, the logo sits on a plate: background `#f3f5fe`, radius 8px, padding `8px 11px` (phone) / `10px 14px` (wide), shadow `0 6px 20px rgba(0,0,0,.35)`. The plate exists because the Fußach logo has black lettering.
  - **Language switcher** (right): pill container, padding 3px, gap 2px, radius 8px, background `rgba(22,24,38,.72)` + `backdrop-filter: blur(10px)`, 1px ring `#3f424d`. Buttons are min-width 36px, height 28px, radius 6px, Inter 500 12px, letter-spacing .04em, labels `DE` / `EN`. The active button has background `#e9e9ed` and text `#161826`. Inactive buttons are transparent with text `#cfd3e5`. Show one button per entry in `PORTAL_LANGUAGES`.
- **Content panel**: flex column, gap 22px.
  - Phone: padding `0 20px 22px`, justify start.
  - Wide: padding `56px 56px 40px 40px`, justify center.
  - The panel scrolls if it overflows.

### Step 1: Choose
- Kicker: Wi‑Fi icon (Phosphor `wifi-high`, 14px) + SSID. 11px, uppercase, letter-spacing .1em, accent color.
- H1 headline: 28px (phone) / 36px (wide), weight 500, line-height 1.12, letter-spacing -.02em, `text-wrap: balance`.
- Intro: 15px, text color at 65% opacity.
- Two large buttons, stacked, gap 10px, full width, min-height 76px, padding 14px 16px, radius 14px:
  - **Member (primary)**:
    - Container: border 1px accent, background accent @ 9%, shadow `0 10px 32px -14px <accent>`. On hover, background goes to accent @ 17%.
    - Icon tile: 44×44, radius 10px, background accent @ 18%, Phosphor `identification-badge` 24px in accent.
    - Title: 17px/500. Subtitle: 13px at 62% text.
    - Trailing `arrow-right` 18px in accent.
  - **Guest (secondary)**:
    - Container: border 1px `rgba(233,233,237,.16)`, background `#232532`. On hover, the border goes to text @ 40%.
    - Icon tile: background `#3f424d`, Phosphor `user` in `#cfd3e5`.
    - Trailing arrow in `#b2b6ca`.
- Footer (pushed to bottom): `lock-simple` icon + footer line, 12px, `#9397ab`.

### Step 2: Guest terms
- Back link: ghost, accent color, `arrow-left` + "Zurück"/"Back", 14px/500. On hover, background accent @ 10%.
- Kicker "Gastzugang"/"Guest access" (as above). H2 "Nutzungsbedingungen"/"Terms of use", 26px/500.
- Two tags: background `#3f424d`, text `#f3f5fe`, 11px, padding 3px 10px, radius 6px.
  - `clock` icon + "24 Std. gültig"/"Valid for 24 h" (from `GUEST_SESSION_HOURS`).
  - `shield-check` icon + "Getrenntes Gastnetz"/"Separate guest network".
- Terms box:
  - Grows to fill the space (min 120px), scrolls, padding 14px 16px, radius 8px.
  - Background `#232532`, ring `0 0 0 1px #3f424d`.
  - Text 13.5px/1.6 at 82% text. Content rendered from the Markdown file.
- Checkbox row:
  - Box: 22×22, radius 6px. Unchecked: 1.5px border `#75798c`. Checked: accent fill + `check` icon in `#161826`.
  - Label: 14px/1.45. The whole row toggles.
- Connect button: min-height 54px, radius 12px, 16px/500.
  - Disabled (terms not accepted): border `rgba(233,233,237,.16)`, text `#9397ab`, `cursor: not-allowed`.
  - Enabled: accent border, background accent @ 12% (hover 20%), accent text, same glow shadow as the Member button.

### Step 3: Guest connected
- Success badge: 84px circle, 1px accent border, background accent @ 12%, shadow `0 0 48px -8px <accent>`, `check` icon 40px in accent.
- H2 "Du bist online"/"You're online", 28px/500. Body "Dein Gerät ist mit dem Gastnetz verbunden." 15px @ 65%.
- Detail card: `#232532`, 1px ring, radius 8px, 14px text. Two rows, padding 12px 16px, separated by a 1px line at text @ 8%.
  - "Netz"/"Network" → `Gastnetz · VLAN 30`.
  - "Gültig bis"/"Valid until" → expiry. Localised `weekday short, day, month short, HH:MM` (`de-AT` / `en-GB`).
- Secondary button at the bottom: "Weiter surfen"/"Continue browsing". Min-height 52px, radius 12px, 1px divider border, hover text @ 7%.

### Step 4: Member redirect (interstitial)
- Spinner: 56px circle, 2px ring accent @ 22%, top segment accent, `rotate 360deg` at `.9s linear infinite`.
- H2 "Weiterleitung zu Microsoft 365"/"Redirecting to Microsoft 365", 26px/500.
- Body: "Melde dich mit deinem Konto der {ORG} an. Danach wirst du automatisch ins Mitgliedernetz verbunden." / "Sign in with your {ORG} account. You'll be moved to the member network automatically."
- URL chip: monospace 12px, `#b2b6ca`, lock icon in accent, `login.microsoftonline.com/{M365_TENANT}`.
- Ghost "Abbrechen"/"Cancel" returns to Choose.
- In production this screen shows briefly (or not at all) before the OIDC redirect. Don't recreate Microsoft's login UI.

## Interactions & Behavior
- Choose → Member: show the redirect state, then start the OIDC auth-code flow against Entra ID (`M365_TENANT`). On callback success, authorize the client MAC into `MEMBER_VLAN`, e.g. via RADIUS CoA / controller API, depending on the network stack. On failure, return to Choose with an error message. The error state isn't designed yet.
- Choose → Guest: go to the Terms step. Connect stays disabled until the checkbox is ticked. Connect authorizes the client into `GUEST_VLAN` for `GUEST_SESSION_HOURS`, then shows Connected.
- Back / Cancel / Continue: reset to Choose and clear the checkbox. In production, "Continue" would close the captive sheet or open a success URL.
- Language switch: swaps all copy immediately. No reload is required.
- Focus: `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px }`. No browser-default rings.
- Hit targets are ≥ 44px. The layout must work in iOS/Android captive-portal webviews (no external fonts guaranteed, so self-host Inter and the icons).

## State
- `step`: `choose | terms | connected | redirect`
- `agreed`: boolean
- `lang`: from `PORTAL_DEFAULT_LANG`, user-switchable
- The server provides all config and the session expiry.

## Configuration (environment variables)
These names are proposals. Rename them to match your code conventions. Unset values fall back to neutral defaults: no logo, striped hero, English, accent `#9184d9`.

| Variable | Example | Controls |
|---|---|---|
| `PORTAL_ORG_NAME` | `Feuerwehr Fußach` | Org name in copy and alt text |
| `PORTAL_LOGO` | `/config/branding/logo.png` | Logo (transparent PNG/SVG) |
| `PORTAL_LOGO_PLATE` | `true` | Light plate behind dark logos |
| `PORTAL_HERO_IMAGE` | `/config/branding/hero.jpg` | Hero photo; dark images blend best |
| `PORTAL_ACCENT` | `#e0695f` | Accent color |
| `PORTAL_LANGUAGES` | `de,en` | Languages in switcher |
| `PORTAL_DEFAULT_LANG` | `de` | Initial language |
| `PORTAL_TEXT_{LANG}_HEADLINE` | `Willkommen im WLAN der Feuerwehr Fußach` | Headline |
| `PORTAL_TEXT_{LANG}_INTRO` | `Wähle, wie du dich verbinden möchtest.` | Intro line |
| `PORTAL_TEXT_{LANG}_MEMBER_TITLE` / `_SUBTITLE` | `Mitglied` / `Mit Microsoft 365 anmelden` | Member button |
| `PORTAL_TEXT_{LANG}_GUEST_TITLE` / `_SUBTITLE` | `Gast` / `Internetzugang im Gastnetz` | Guest button |
| `PORTAL_TERMS_{LANG}` | `/config/terms/de.md` | Guest terms (Markdown file) |
| `PORTAL_SSID` | `FF-Fussach` | SSID label on Choose |
| `MEMBER_VLAN` | `10` | VLAN after M365 sign-in |
| `GUEST_VLAN` | `30` | VLAN after accepting terms |
| `GUEST_SESSION_HOURS` | `24` | Guest session length |
| `M365_TENANT` | `ff-fussach.onmicrosoft.com` | Entra ID tenant (store client secret in a Secret) |

Fixed UI strings (back, connect, connected title, etc.) ship with the app in DE and EN. The full list is in the `UI` object in `design/Portal.dc.html`. Default customizable copy for DE and EN is in the `DEF.text` object in the same file.

Example `deployment.yaml` excerpt:
```yaml
env:
  - name: PORTAL_ORG_NAME
    value: "Feuerwehr Fußach"
  - name: PORTAL_ACCENT
    value: "#e0695f"
  - name: PORTAL_LOGO
    value: /config/branding/logo.png
  - name: PORTAL_LOGO_PLATE
    value: "true"
  - name: PORTAL_DEFAULT_LANG
    value: de
  - name: PORTAL_TERMS_DE
    value: /config/terms/de.md
  - name: GUEST_VLAN
    value: "30"
  - name: M365_TENANT
    valueFrom:
      secretKeyRef: { name: m365, key: tenant }
volumeMounts:
  - name: portal-config
    mountPath: /config
volumes:
  - name: portal-config
    configMap:
      name: ff-fussach-portal
```

## Design Tokens (Nocturne design system; see `design/nocturne.css`)
- **Colors**
  - Ground and text: bg `#161826`, surface `#232532`, text `#e9e9ed`, divider = text @ 16%.
  - Neutral ramp: 100 `#f3f5fe` · 200 `#e4e7f5` · 300 `#cfd3e5` · 400 `#b2b6ca` · 500 `#9397ab` · 600 `#75798c` · 700 `#595d6c` · 800 `#3f424d` · 900 `#292b31`.
  - Accent (configurable): default for Fußach `#e0695f`. System default `#9184d9`. Other preset options: water blue `#4aa3df`, amber `#d9a441`.
  - Accent tints are `color-mix(in srgb, var(--color-accent) N%, transparent)` with N = 9 / 10 / 12 / 16–18 / 20 / 22.
- **Type**: Inter (400/500). Headings weight 500 max. Sizes: 36 / 28 / 26 / 17 / 16 / 15 / 14 / 13.5 / 13 / 12 / 11. Monospace: `ui-monospace, Menlo`.
- **Radii**: 6 (small controls), 8 (cards, inputs), 10 (icon tiles), 12 (action buttons), 14 (choice buttons).
- **Shadows**: ring `0 0 0 1px #3f424d`. Accent glow `0 10px 32px -14px <accent>`. Logo plate `0 6px 20px rgba(0,0,0,.35)`.
- **Icons**: Phosphor Regular (`wifi-high`, `identification-badge`, `user`, `arrow-right`, `arrow-left`, `lock-simple`, `clock`, `shield-check`, `check`). Self-host them.

## Assets
- `design/assets/logo-fussach.png`: the Feuerwehr Fußach logo, supplied by the customer. Transparent PNG, 2421×1096, black lettering.
- The hero photo is not supplied yet. The design shows a striped placeholder.

## Files
- `design/Portal.dc.html`: **the portal page itself**. All four steps, both layouts, DE/EN strings, defaults.
- `design/WLAN Portal.dc.html`: presentation canvas. Phone flow (1a), laptop (1b), env-var reference (1c).
- `design/nocturne.css`: design tokens and base component classes.
- `design/support.js`: runtime used only to open the `.dc.html` files in a browser. Don't port it.

Open `design/WLAN Portal.dc.html` in a browser (served over http) to click through the prototype.
