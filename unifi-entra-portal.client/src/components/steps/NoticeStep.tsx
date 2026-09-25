import { XIcon } from '../icons/XIcon';

interface NoticeStepProps {
    /** Optional kicker label for the top row, e.g. "Guest access". Omit for states with no clear flow context (e.g. already signed in). */
    kicker?: string;
    /** Leaves the current flow, back to the Choose step. Omit to render no close button (e.g. when there's nothing meaningful to return to). */
    onClose?: () => void;
    /** Title shown above the body copy. */
    title: string;
    /** Body copy explaining what happened and what to do next. */
    body: string;
}

/**
 * Neutral "something's not right" screen — used for the missing-device and
 * authorization-failure states on both the guest and member paths, which
 * design_handoff_wlan_captive_portal/README.md leaves undesigned ("the
 * error state isn't designed yet"). Structurally identical to the Choose
 * step's kicker→headline→intro and the Terms step's kicker→title: no icon,
 * just a title and body stacked under the optional kicker/close row.
 */
export function NoticeStep({ kicker, onClose, title, body }: NoticeStepProps) {
    const hasTopRow = Boolean(kicker || onClose);

    return (
        <>
            {hasTopRow && (
                <div className="portal-panel-top-row">
                    <div className="portal-kicker">{kicker}</div>
                    {onClose && (
                        <button type="button" className="portal-panel-icon-btn" onClick={onClose} aria-label="Close">
                            <XIcon size={18} />
                        </button>
                    )}
                </div>
            )}
            <div className={`portal-notice-header${hasTopRow ? '' : ' portal-notice-header--standalone'}`}>
                <h2 className="portal-notice-title">{title}</h2>
                <p className="portal-notice-body">{body}</p>
            </div>
        </>
    );
}
