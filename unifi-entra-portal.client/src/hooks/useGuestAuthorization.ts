import { useEffect, useState } from 'react';

export type GuestAuthorizationStatus = 'idle' | 'authorizing' | 'authorized' | 'error';

/**
 * Once the user is signed in and a client MAC address from the UniFi
 * redirect is known, calls the backend to authorize that device on the
 * UniFi network. The backend call is idempotent (re-authorizing an already
 * authorized MAC is harmless), so this intentionally does not guard against
 * running more than once — that guard would fight React StrictMode's
 * mount/cleanup/remount cycle in development: a "has run" ref set on the
 * first mount would suppress the effect on remount, while the first run's
 * own `cancelled` flag (set true by the synthetic cleanup) would suppress
 * its state update, leaving the status stuck on "authorizing" forever.
 */
export function useGuestAuthorization(isAuthenticated: boolean, macAddress: string | null): GuestAuthorizationStatus {
    const [status, setStatus] = useState<GuestAuthorizationStatus>('idle');

    useEffect(() => {
        if (!isAuthenticated || !macAddress) {
            return;
        }

        const mac = macAddress;
        let cancelled = false;

        async function authorize() {
            setStatus('authorizing');
            try {
                const response = await fetch(`/api/portal/authorize?mac=${encodeURIComponent(mac)}`, { method: 'POST' });
                if (!cancelled) {
                    setStatus(response.ok ? 'authorized' : 'error');
                }
            } catch {
                if (!cancelled) {
                    setStatus('error');
                }
            }
        }

        authorize();

        return () => {
            cancelled = true;
        };
    }, [isAuthenticated, macAddress]);

    return status;
}
