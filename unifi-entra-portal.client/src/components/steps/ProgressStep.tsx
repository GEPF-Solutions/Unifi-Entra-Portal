import type { ReactNode } from 'react';
import { XIcon } from '../icons/XIcon';

interface ProgressStepProps {
    /** Optional kicker label for the top row, e.g. "Member access". Omit for states with no flow context yet (e.g. the initial session check). */
    kicker?: string;
    /** Leaves the current flow, e.g. before the real navigation has started. Omit to render no close button. */
    onClose?: () => void;
    /** Title shown next to the spinner. */
    title: string;
    /** Body copy explaining what's happening. Omit for a bare "please wait" moment with no further detail. */
    body?: string;
    /** Extra content rendered under the title/body, e.g. the redirect interstitial's URL chip. */
    children?: ReactNode;
}

/**
 * Brief "please wait" screen — a spinner plus centered title/body, with an
 * optional kicker+close row and extra content slot (e.g. a URL chip).
 * Shared by the member redirect interstitial, the post-sign-in "connecting
 * your device" wait, and the initial session-check loading state; matches
 * the "Member redirect" step of
 * design_handoff_wlan_captive_portal/design/Portal.dc.html.
 */
export function ProgressStep({ kicker, onClose, title, body, children }: ProgressStepProps) {
    return (
        <>
            {(kicker || onClose) && (
                <div className="portal-panel-top-row">
                    <div className="portal-kicker">{kicker}</div>
                    {onClose && (
                        <button type="button" className="portal-panel-icon-btn" onClick={onClose} aria-label="Close">
                            <XIcon size={18} />
                        </button>
                    )}
                </div>
            )}
            <div className="portal-progress-header">
                <div className="portal-spinner" />
                <div className="portal-progress-text">
                    <h2 className="portal-progress-title">{title}</h2>
                    {body && <p className="portal-progress-body">{body}</p>}
                </div>
                {children}
            </div>
        </>
    );
}
