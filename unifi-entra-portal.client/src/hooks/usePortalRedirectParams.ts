import { useMemo } from 'react';

/**
 * The UniFi captive portal redirect's query parameters, describing the
 * connecting client device and network. See
 * https://help.ui.com/hc/en-us/articles/31228198640023 for the shape UniFi
 * appends when redirecting a guest to an external portal, e.g.
 * `?ap=<ap-mac>&id=<client-mac>&t=<unix-timestamp>&url=<original-url>&ssid=<ssid>`.
 */
export interface PortalRedirectParams {
    /** MAC address of the connecting client device (`id` query param). */
    clientMac: string | null;
    /** MAC address of the access point the client associated with (`ap` query param). */
    apMac: string | null;
    /** The URL the client originally tried to reach before being redirected (`url` query param). */
    originalUrl: string | null;
    /** SSID name the client connected to (`ssid` query param). */
    ssid: string | null;
}

/**
 * Restricts the `url` redirect param to http(s) links before it's ever used
 * as an anchor href. Without this, a crafted portal link like
 * `?url=javascript:...` would run attacker script in the signed-in guest's
 * session when they click "Continue browsing" — React does not block
 * javascript: URIs in href the way it does for e.g. dangerouslySetInnerHTML.
 */
function sanitizeOriginalUrl(value: string | null): string | null {
    if (!value) {
        return null;
    }

    try {
        const parsed = new URL(value);
        return parsed.protocol === 'http:' || parsed.protocol === 'https:' ? value : null;
    } catch {
        return null;
    }
}

/**
 * Reads the UniFi captive portal redirect's query parameters from the
 * current browser location. Parsed once per mount, since these only ever
 * arrive on the page's initial load, not from in-app navigation.
 */
export function usePortalRedirectParams(): PortalRedirectParams {
    return useMemo(() => {
        const params = new URLSearchParams(window.location.search);
        return {
            clientMac: params.get('id'),
            apMac: params.get('ap'),
            originalUrl: sanitizeOriginalUrl(params.get('url')),
            ssid: params.get('ssid'),
        };
    }, []);
}
