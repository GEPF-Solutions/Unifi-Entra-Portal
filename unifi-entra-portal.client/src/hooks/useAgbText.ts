import { useEffect, useState } from 'react';

interface UseAgbTextResult {
    /** Operator-configured AGB/terms text, or null while loading/on failure. */
    agbText: string | null;
    /** True while the initial `/api/guest/agb-text` fetch is in flight. */
    loading: boolean;
}

/**
 * Fetches the operator-configured AGB/terms text shown on the guest
 * acceptance screen from `/api/guest/agb-text` on mount. Sourced from the
 * backend (`GuestAgbSettings`) rather than baked into the frontend bundle,
 * so an operator can update legal copy without rebuilding the container.
 */
export function useAgbText(): UseAgbTextResult {
    const [agbText, setAgbText] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let cancelled = false;

        async function loadAgbText() {
            try {
                const response = await fetch('/api/guest/agb-text');
                const data = await response.json();
                if (!cancelled) {
                    setAgbText(response.ok ? (data.text ?? null) : null);
                }
            } catch {
                if (!cancelled) {
                    setAgbText(null);
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        }

        loadAgbText();

        return () => {
            cancelled = true;
        };
    }, []);

    return { agbText, loading };
}
