/**
 * Neutral fallback portal configuration, matching the shape returned by
 * `GET /api/portal/config`. Shown while that fetch is in flight and used to
 * fill in any field the backend leaves unset — see
 * `design_handoff_wlan_captive_portal/README.md`: "Unset values fall back
 * to neutral defaults: no logo, striped hero, accent #9184d9."
 */
export interface PortalConfig {
    orgName: string;
    ssid: string;
    accentColor: string;
    logoUrl: string | null;
    logoPlate: boolean;
    faviconUrl: string | null;
    heroImageUrl: string | null;
    headline: string;
    intro: string;
    memberTitle: string;
    memberSubtitle: string;
    guestTitle: string;
    guestSubtitle: string;
    tenant: string;
    guestNetworkLabel: string;
    memberNetworkLabel: string;
    guestSessionHours: number;
}

/** Neutral defaults shown before `usePortalConfig` resolves, or for any field the backend leaves unset. */
export const defaultPortalConfig: PortalConfig = {
    orgName: 'Your Organization',
    ssid: '',
    accentColor: '#9184d9',
    logoUrl: null,
    logoPlate: false,
    faviconUrl: null,
    heroImageUrl: null,
    headline: 'Welcome to the Wi-Fi',
    intro: "Choose how you'd like to connect.",
    memberTitle: 'Member',
    memberSubtitle: 'Sign in with your organization account',
    guestTitle: 'Guest',
    guestSubtitle: 'Internet access on the guest network',
    tenant: '',
    guestNetworkLabel: 'Guest network',
    memberNetworkLabel: 'Member network',
    guestSessionHours: 24,
};
