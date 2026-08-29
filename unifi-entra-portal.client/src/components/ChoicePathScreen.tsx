import { brandingConfig } from '../branding/config';

interface ChoicePathScreenProps {
    /** URL to navigate to for the Entra sign-in ("I'm a member") path. */
    memberLoginUrl: string;
    /** Called when the visitor picks the anonymous guest path. */
    onChooseGuest: () => void;
}

/**
 * Landing choice between the member (Entra sign-in) and guest (AGB) paths,
 * shown regardless of which SSID the visitor connected via — VLAN
 * separation between the two is handled entirely by UniFi's SSID→network
 * binding, not by this screen. See Context.md, "Planned: dual
 * guest/member portal".
 */
function ChoicePathScreen({ memberLoginUrl, onChooseGuest }: ChoicePathScreenProps) {
    return (
        <>
            <p>{brandingConfig.welcomeText}</p>
            <a className="portal-button" href={memberLoginUrl}>
                I'm a fire department member
            </a>
            <button className="portal-button" type="button" onClick={onChooseGuest}>
                I'm a guest
            </button>
        </>
    );
}

export default ChoicePathScreen;
