import { useEffect } from 'react';
import type { PortalConfig } from '../branding/defaultConfig';

/**
 * Keeps the browser tab's title and favicon in sync with the operator's
 * portal config, since index.html's static `<title>`/`<link rel="icon">`
 * can't know the branding at build time. Only touches the favicon when
 * `config.faviconUrl` is set — otherwise index.html's bundled default icon
 * is left in place.
 */
export function useDocumentBranding(config: PortalConfig): void {
    useEffect(() => {
        document.title = config.orgName;

        if (!config.faviconUrl) {
            return;
        }

        const favicon = document.querySelector<HTMLLinkElement>("link[rel='icon']");
        if (favicon) {
            favicon.href = config.faviconUrl;
        }
    }, [config.orgName, config.faviconUrl]);
}
