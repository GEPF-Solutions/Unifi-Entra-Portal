import { useCallback, useState } from 'react';

export type GuestVisitorAuthorizationStatus = 'idle' | 'authorizing' | 'authorized' | 'error';

interface UseGuestVisitorAuthorizationResult {
    status: GuestVisitorAuthorizationStatus;
    /** When the guest's authorization expires, as reported by the backend. Set once `status` is 'authorized'. */
    expiresAtUtc: string | null;
    /** Call once the visitor has checked the AGB box and clicked Continue. */
    authorize: (agbAccepted: boolean) => Promise<void>;
    /** Returns to 'idle', e.g. when the visitor leaves the guest flow (closes an error screen) and might re-enter it later. */
    reset: () => void;
}

/**
 * Authorizes an anonymous guest's device on the UniFi network after they
 * accept the AGB/terms checkbox. Exposes an imperative `authorize()`
 * function rather than auto-running from an effect (contrast with
 * `useGuestAuthorization`) — this must fire exactly once, when the visitor
 * clicks Continue, not as a side effect of some value becoming truthy.
 * Since it's only ever invoked from a click handler, it isn't subject to
 * React StrictMode's dev-mode double-effect-on-mount behavior; a simple
 * in-flight guard (disabling Continue while `status === 'authorizing'`)
 * still prevents a double-click from double-submitting.
 */
export function useGuestVisitorAuthorization(macAddress: string | null): UseGuestVisitorAuthorizationResult {
    const [status, setStatus] = useState<GuestVisitorAuthorizationStatus>('idle');
    const [expiresAtUtc, setExpiresAtUtc] = useState<string | null>(null);

    const authorize = useCallback(
        async (agbAccepted: boolean) => {
            if (!macAddress || !agbAccepted) {
                setStatus('error');
                return;
            }

            setStatus('authorizing');
            try {
                const response = await fetch(
                    `/api/guest/authorize?mac=${encodeURIComponent(macAddress)}&agbAccepted=${agbAccepted}`,
                    { method: 'POST' },
                );
                if (response.ok) {
                    const data = await response.json();
                    setExpiresAtUtc(data.expiresAtUtc ?? null);
                    setStatus('authorized');
                } else {
                    setStatus('error');
                }
            } catch {
                setStatus('error');
            }
        },
        [macAddress],
    );

    const reset = useCallback(() => {
        setStatus('idle');
        setExpiresAtUtc(null);
    }, []);

    return { status, expiresAtUtc, authorize, reset };
}
