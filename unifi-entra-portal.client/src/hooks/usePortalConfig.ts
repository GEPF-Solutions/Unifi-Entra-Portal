import { useEffect, useState } from 'react';
import { defaultPortalConfig, type PortalConfig } from '../branding/defaultConfig';

interface UsePortalConfigResult {
    /** Portal branding/copy config, merged over the neutral defaults so a partially-configured backend still renders sensibly. */
    config: PortalConfig;
    /** True while the initial `/api/portal/config` fetch is in flight. */
    loading: boolean;
}

/**
 * Fetches the operator-configured branding and copy for the captive portal
 * landing screen from `/api/portal/config` on mount. Sourced from the
 * backend (`PortalBrandingSettings`) rather than baked into the frontend
 * bundle, so the same container image can be re-skinned for a different
 * organization purely through config — see
 * design_handoff_wlan_captive_portal/README.md.
 */
export function usePortalConfig(): UsePortalConfigResult {
    const [config, setConfig] = useState<PortalConfig>(defaultPortalConfig);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let cancelled = false;

        async function loadConfig() {
            try {
                const response = await fetch('/api/portal/config');
                if (!response.ok) {
                    return;
                }
                const data = await response.json();
                if (!cancelled) {
                    setConfig({ ...defaultPortalConfig, ...data });
                }
            } catch {
                // Keep the neutral defaults already in state.
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        }

        loadConfig();

        return () => {
            cancelled = true;
        };
    }, []);

    return { config, loading };
}
