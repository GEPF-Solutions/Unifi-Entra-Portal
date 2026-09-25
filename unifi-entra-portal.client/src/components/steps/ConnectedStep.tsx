import { CheckIcon } from '../icons/CheckIcon';

interface ConnectedStepProps {
    /** Label for the network row, e.g. "Guest network" or "Member network". */
    networkLabel: string;
    /** Body copy under the title, e.g. "Your device is connected to the guest network." */
    bodyText: string;
    /** ISO 8601 UTC expiry timestamp from the authorize response, or null to hide the "Valid until" row. */
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
 * "Connected" success screen — matching the "Connected" step of
 * design_handoff_wlan_captive_portal/design/Portal.dc.html. Shared by both
 * the guest and member paths (see App.tsx), which differ only in network
 * label/copy and whether a real expiry is known.
 */
export function ConnectedStep({ networkLabel, bodyText, expiresAtUtc, originalUrl }: ConnectedStepProps) {
    const expiryLabel = formatExpiry(expiresAtUtc);

    return (
        <>
            <div className="portal-status-header">
                <div className="portal-icon-badge portal-icon-badge--accent">
                    <CheckIcon size={40} />
                </div>
                <div className="portal-status-text">
                    <h2 className="portal-status-title">You're online</h2>
                    <p className="portal-status-body">{bodyText}</p>
                </div>
            </div>
            <div className="portal-detail-card">
                <div className="portal-detail-row">
                    <span className="portal-detail-label">Network</span>
                    <span>{networkLabel}</span>
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
