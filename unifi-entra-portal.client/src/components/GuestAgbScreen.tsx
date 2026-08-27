import { useState } from 'react';
import { useAgbText } from '../hooks/useAgbText';
import { useGuestVisitorAuthorization } from '../hooks/useGuestVisitorAuthorization';

interface GuestAgbScreenProps {
    macAddress: string | null;
    originalUrl: string | null;
}

/**
 * AGB/terms-accept screen for the anonymous guest path: no Entra sign-in,
 * just an acceptance checkbox before a short-duration UniFi authorization.
 */
function GuestAgbScreen({ macAddress, originalUrl }: GuestAgbScreenProps) {
    const [agbAccepted, setAgbAccepted] = useState(false);
    const { status, authorize } = useGuestVisitorAuthorization(macAddress);
    const { agbText, loading: agbTextLoading } = useAgbText();

    if (!macAddress) {
        return <p>No device was detected to authorize. Please reconnect to the guest Wi-Fi and try again.</p>;
    }

    if (status === 'authorized') {
        return (
            <>
                <p>You're connected as a guest. You can now browse normally.</p>
                {originalUrl && (
                    <a className="portal-button" href={originalUrl}>
                        Continue browsing
                    </a>
                )}
            </>
        );
    }

    return (
        <>
            <p>{agbTextLoading ? 'Loading terms…' : agbText}</p>
            <label className="portal-agb-label">
                <input
                    type="checkbox"
                    checked={agbAccepted}
                    disabled={agbTextLoading}
                    onChange={(e) => setAgbAccepted(e.target.checked)}
                />
                I accept the terms and conditions
            </label>
            <button
                className="portal-button"
                type="button"
                disabled={!agbAccepted || status === 'authorizing'}
                onClick={() => authorize(agbAccepted)}
            >
                Continue
            </button>
            {status === 'error' && <p>Something went wrong authorizing your device. Please try again.</p>}
        </>
    );
}

export default GuestAgbScreen;
