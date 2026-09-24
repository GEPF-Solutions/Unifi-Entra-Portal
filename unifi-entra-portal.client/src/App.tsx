import { useEffect, useState } from 'react';
import './styles/portal.css';
import { PortalLayout } from './components/layout/PortalLayout';
import { ChooseStep } from './components/steps/ChooseStep';
import { GuestConnectedStep } from './components/steps/GuestConnectedStep';
import { GuestTermsStep } from './components/steps/GuestTermsStep';
import { MemberRedirectStep } from './components/steps/MemberRedirectStep';
import { useAgbText } from './hooks/useAgbText';
import { useAuth } from './hooks/useAuth';
import { useGuestAuthorization } from './hooks/useGuestAuthorization';
import { useGuestVisitorAuthorization } from './hooks/useGuestVisitorAuthorization';
import { usePortalConfig } from './hooks/usePortalConfig';
import { usePortalRedirectParams } from './hooks/usePortalRedirectParams';

/**
 * Top-level captive portal page. Derives which step to show from the
 * device/auth signals reported by its hooks (mirroring the "State" section
 * of design_handoff_wlan_captive_portal/README.md) and renders the matching
 * step component inside the shared PortalLayout shell.
 */
function App() {
    const { config } = usePortalConfig();
    const { clientMac, originalUrl } = usePortalRedirectParams();
    const { isAuthenticated, loading: authLoading, buildLoginUrl } = useAuth();
    const memberAuthorizationStatus = useGuestAuthorization(isAuthenticated, clientMac);
    const { agbText, loading: agbTextLoading } = useAgbText();
    const { status: guestStatus, expiresAtUtc, authorize: authorizeGuest } = useGuestVisitorAuthorization(clientMac);

    const [guestPathChosen, setGuestPathChosen] = useState(false);
    const [guestAgreed, setGuestAgreed] = useState(false);
    const [memberRedirectStarted, setMemberRedirectStarted] = useState(false);

    const memberLoginUrl = buildLoginUrl(window.location.pathname + window.location.search);

    useEffect(() => {
        if (memberRedirectStarted) {
            window.location.href = memberLoginUrl;
        }
    }, [memberRedirectStarted, memberLoginUrl]);

    if (authLoading) {
        return (
            <div className="portal-loading">
                <p>Loading…</p>
            </div>
        );
    }

    if (isAuthenticated) {
        return (
            <PortalLayout config={config} compactHero>
                {!clientMac && (
                    <p className="portal-message">
                        You're signed in, but no device was detected to authorize. Please reconnect to the guest Wi-Fi and try again.
                    </p>
                )}
                {clientMac && memberAuthorizationStatus === 'authorizing' && <p className="portal-message">Connecting your device…</p>}
                {clientMac && memberAuthorizationStatus === 'authorized' && (
                    <>
                        <p className="portal-message">You're connected. You can now browse normally.</p>
                        {originalUrl && (
                            <a className="portal-secondary-btn" href={originalUrl}>
                                Continue browsing
                            </a>
                        )}
                    </>
                )}
                {clientMac && memberAuthorizationStatus === 'error' && (
                    <p className="portal-message">Something went wrong authorizing your device. Please try reconnecting to the guest Wi-Fi.</p>
                )}
            </PortalLayout>
        );
    }

    if (memberRedirectStarted) {
        return (
            <PortalLayout config={config} compactHero>
                <MemberRedirectStep orgName={config.orgName} tenant={config.tenant} onCancel={() => setMemberRedirectStarted(false)} />
            </PortalLayout>
        );
    }

    if (guestPathChosen) {
        if (!clientMac) {
            return (
                <PortalLayout config={config} compactHero>
                    <p className="portal-message">No device was detected to authorize. Please reconnect to the guest Wi-Fi and try again.</p>
                </PortalLayout>
            );
        }

        if (guestStatus === 'authorized') {
            return (
                <PortalLayout config={config} compactHero>
                    <GuestConnectedStep guestNetworkLabel={config.guestNetworkLabel} expiresAtUtc={expiresAtUtc} originalUrl={originalUrl} />
                </PortalLayout>
            );
        }

        return (
            <PortalLayout config={config} compactHero>
                <GuestTermsStep
                    guestSessionHours={config.guestSessionHours}
                    agbText={agbText}
                    agbTextLoading={agbTextLoading}
                    agreed={guestAgreed}
                    onToggleAgree={() => setGuestAgreed((prev) => !prev)}
                    onConnect={() => authorizeGuest(guestAgreed)}
                    connecting={guestStatus === 'authorizing'}
                    hasError={guestStatus === 'error'}
                    onBack={() => {
                        setGuestPathChosen(false);
                        setGuestAgreed(false);
                    }}
                />
            </PortalLayout>
        );
    }

    return (
        <PortalLayout config={config}>
            <ChooseStep config={config} onChooseMember={() => setMemberRedirectStarted(true)} onChooseGuest={() => setGuestPathChosen(true)} />
        </PortalLayout>
    );
}

export default App;
