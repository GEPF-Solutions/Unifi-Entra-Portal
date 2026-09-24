import { CheckIcon } from '../icons/CheckIcon';

interface GuestConnectedStepProps {
    /** Label for the network row, e.g. "Guest network". */
    guestNetworkLabel: string;
    /** ISO 8601 UTC expiry timestamp from the guest authorize response, or null if unknown. */
    expiresAtUtc: string | null;
    /** URL the client originally tried to reach before being redirected to the portal, if any. */
    originalUrl: string | null;
}

/**
 * Formats an ISO UTC timestamp for display, e.g. "Wed, 24 Sep, 14:30".
 * Returns null for an unparsable value so the caller can hide the row
 * entirely rather than show "Invalid Date".
 */
function formatExpiry(expiresAtUtc: string | null): string | null {
    if (!expiresAtUtc) {
        return null;
    }
    const date = new Date(expiresAtUtc);
    if (Number.isNaN(date.getTime())) {
        return null;
    }
    return date.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
}

/**
 * Guest "connected" success screen — matching the "Connected" step of
 * design_handoff_wlan_captive_portal/design/Portal.dc.html.
 */
export function GuestConnectedStep({ guestNetworkLabel, expiresAtUtc, originalUrl }: GuestConnectedStepProps) {
    const expiryLabel = formatExpiry(expiresAtUtc);

    return (
        <>
            <div className="portal-connected-header">
                <div className="portal-success-badge">
                    <CheckIcon size={40} />
                </div>
                <div className="portal-connected-text">
                    <h2 className="portal-connected-title">You're online</h2>
                    <p className="portal-connected-body">Your device is connected to the guest network.</p>
                </div>
            </div>
            <div className="portal-detail-card">
                <div className="portal-detail-row">
                    <span className="portal-detail-label">Network</span>
                    <span>{guestNetworkLabel}</span>
                </div>
                {expiryLabel && (
                    <div className="portal-detail-row">
                        <span className="portal-detail-label">Valid until</span>
                        <span>{expiryLabel}</span>
                    </div>
                )}
            </div>
            {originalUrl && (
                <a className="portal-secondary-btn" href={originalUrl}>
                    Continue browsing
                </a>
            )}
        </>
    );
}
