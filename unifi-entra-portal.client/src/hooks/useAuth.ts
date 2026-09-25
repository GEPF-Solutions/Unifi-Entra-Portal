import { useCallback, useEffect, useState } from 'react';

/** Current browser session's Entra ID sign-in state, as reported by the backend. */
export interface AuthState {
    isAuthenticated: boolean;
    name: string | null;
    email: string | null;
}

interface UseAuthResult extends AuthState {
    /** True while the initial `/api/auth/me` check is in flight. */
    loading: boolean;
    /** Builds the URL to navigate the browser to in order to sign in, returning to `returnUrl` afterward. */
    buildLoginUrl: (returnUrl: string) => string;
    /** Builds the URL to navigate the browser to in order to sign out, returning to `returnUrl` afterward. */
    buildLogoutUrl: (returnUrl: string) => string;
}

/**
 * Fetches the current session's Entra ID authentication state from
 * `/api/auth/me` on mount. Does not itself trigger a sign-in redirect, so it
 * can be called unconditionally before the user has chosen to sign in.
 */
export function useAuth(): UseAuthResult {
    const [state, setState] = useState<AuthState>({ isAuthenticated: false, name: null, email: null });
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let cancelled = false;

        async function loadAuthState() {
            try {
                const response = await fetch('/api/auth/me');
                const data = await response.json();
                if (!cancelled) {
                    setState({
                        isAuthenticated: Boolean(data.isAuthenticated),
                        name: data.name ?? null,
                        email: data.email ?? null,
                    });
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        }

        loadAuthState();

        return () => {
            cancelled = true;
        };
    }, []);

    const buildLoginUrl = useCallback(
        (returnUrl: string) => `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`,
        [],
    );

    const buildLogoutUrl = useCallback(
        (returnUrl: string) => `/api/auth/logout?returnUrl=${encodeURIComponent(returnUrl)}`,
        [],
    );

    return { ...state, loading, buildLoginUrl, buildLogoutUrl };
}
