import { CheckIcon } from '../icons/CheckIcon';
import { ClockIcon } from '../icons/ClockIcon';
import { XIcon } from '../icons/XIcon';

interface GuestTermsStepProps {
    /** How long guest access lasts, shown in the "valid for" tag. */
    guestSessionHours: number;
    /** Operator-configured AGB/terms text, or null while loading. */
    agbText: string | null;
    /** True while the terms text is still being fetched. */
    agbTextLoading: boolean;
    /** Whether the visitor has checked the acceptance checkbox. */
    agreed: boolean;
    /** Toggles the acceptance checkbox. */
    onToggleAgree: () => void;
    /** Submits the accepted terms and starts guest authorization. */
    onConnect: () => void;
    /** True while the guest authorize request is in flight. */
    connecting: boolean;
    /** Leaves the guest flow, back to the Choose step. */
    onClose: () => void;
}

/**
 * Guest terms-of-use acceptance screen — matching the "Terms" step of
 * design_handoff_wlan_captive_portal/design/Portal.dc.html. The Connect
 * button stays disabled until the checkbox is ticked.
 */
export function GuestTermsStep({
    guestSessionHours,
    agbText,
    agbTextLoading,
    agreed,
    onToggleAgree,
    onConnect,
    connecting,
    onClose,
}: GuestTermsStepProps) {
    const validForLabel = Number.isInteger(guestSessionHours)
        ? `Valid for ${guestSessionHours} h`
        : `Valid for ${guestSessionHours.toFixed(1)} h`;

    return (
        <>
            <div className="portal-panel-top-row">
                <div className="portal-kicker">Guest access</div>
                <button type="button" className="portal-panel-icon-btn" onClick={onClose} aria-label="Close">
                    <XIcon size={18} />
                </button>
            </div>
            <div className="portal-terms-header">
                <div className="portal-tags">
                    <span className="portal-tag">
                        <ClockIcon size={12} className="portal-tag-icon" />
                        {validForLabel}
                    </span>
                </div>
                <h2 className="portal-terms-title">Terms of use</h2>
            </div>
            <div className="portal-terms-box">{agbTextLoading ? 'Loading terms…' : (agbText ?? 'Terms of use are not available right now.')}</div>
            <button
                type="button"
                role="checkbox"
                aria-checked={agreed}
                className="portal-checkbox-row"
                disabled={agbTextLoading}
                onClick={onToggleAgree}
            >
                <span className={`portal-checkbox-box${agreed ? ' portal-checkbox-box--checked' : ' portal-checkbox-box--unchecked'}`}>
                    {agreed && <CheckIcon size={14} />}
                </span>
                <span>I have read and accept the terms of use.</span>
            </button>
            <button
                type="button"
                className={`portal-connect-btn${agreed ? ' portal-connect-btn--enabled' : ' portal-connect-btn--disabled'}`}
                disabled={!agreed || connecting}
                onClick={onConnect}
            >
                Connect
            </button>
        </>
    );
}
