import { ArrowRightIcon } from '../icons/ArrowRightIcon';
import { IdentificationBadgeIcon } from '../icons/IdentificationBadgeIcon';
import { UserIcon } from '../icons/UserIcon';
import { WifiHighIcon } from '../icons/WifiHighIcon';
import type { PortalConfig } from '../../branding/defaultConfig';

interface ChooseStepProps {
    /** Branding/copy for the headline, intro and button labels. */
    config: PortalConfig;
    /** Called when the visitor picks the member (Entra sign-in) path. */
    onChooseMember: () => void;
    /** Called when the visitor picks the anonymous guest path. */
    onChooseGuest: () => void;
}

/**
 * Landing choice between the member (Entra sign-in) and guest (terms
 * acceptance) paths — the portal's first screen, matching the "Choose" step
 * of design_handoff_wlan_captive_portal/design/Portal.dc.html.
 */
export function ChooseStep({ config, onChooseMember, onChooseGuest }: ChooseStepProps) {
    return (
        <>
            <div className="portal-choose-header">
                {config.ssid && (
                    <div className="portal-kicker">
                        <WifiHighIcon size={14} />
                        {config.ssid}
                    </div>
                )}
                <h1 className="portal-headline">{config.headline}</h1>
                <p className="portal-intro">{config.intro}</p>
            </div>
            <div className="portal-choices">
                <button type="button" className="portal-choice-btn" onClick={onChooseMember}>
                    <span className="portal-choice-icon">
                        <IdentificationBadgeIcon size={24} />
                    </span>
                    <span className="portal-choice-text">
                        <span className="portal-choice-title">{config.memberTitle}</span>
                        <span className="portal-choice-sub">{config.memberSubtitle}</span>
                    </span>
                    <ArrowRightIcon size={18} className="portal-choice-arrow" />
                </button>
                <button type="button" className="portal-choice-btn" onClick={onChooseGuest}>
                    <span className="portal-choice-icon">
                        <UserIcon size={24} />
                    </span>
                    <span className="portal-choice-text">
                        <span className="portal-choice-title">{config.guestTitle}</span>
                        <span className="portal-choice-sub">{config.guestSubtitle}</span>
                    </span>
                    <ArrowRightIcon size={18} className="portal-choice-arrow" />
                </button>
            </div>
        </>
    );
}
