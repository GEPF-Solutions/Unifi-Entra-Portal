import { useEffect, useState } from 'react';
import './styles/portal.css';
import { LockSimpleIcon } from './components/icons/LockSimpleIcon';
import { PortalLayout } from './components/layout/PortalLayout';
import { ChooseStep } from './components/steps/ChooseStep';
import { ConnectedStep } from './components/steps/ConnectedStep';
import { GuestTermsStep } from './components/steps/GuestTermsStep';
import { NoticeStep } from './components/steps/NoticeStep';
import { ProgressStep } from './components/steps/ProgressStep';
import { useAgbText } from './hooks/useAgbText';
import { useAuth } from './hooks/useAuth';
import { useDocumentBranding } from './hooks/useDocumentBranding';
import { useGuestAuthorization } from './hooks/useGuestAuthorization';
import { useGuestVisitorAuthorization } from './hooks/useGuestVisitorAuthorization';
import { usePortalConfig } from './hooks/usePortalConfig';
import { usePortalRedirectParams } from './hooks/usePortalRedirectParams';

/** Renders the "login.microsoftonline.com/{tenant}" chip shown under the member redirect interstitial's body copy, or null when no tenant is configured. */
function TenantUrlChip({ tenant }: { tenant: string }) {
    if (!tenant) {
        return null;
    }
    return (
        <div className="portal-url-chip">
            <LockSimpleIcon size={14} className="portal-url-chip-lock" />
            login.microsoftonline.com/{tenant}
        </div>
    );
}

/**
 * Top-level captive portal page. Derives which step to show from the
 * device/auth signals reported by its hooks (mirroring the "State" section
 * of design_handoff_wlan_captive_portal/README.md) and renders the matching
 * step component inside the shared PortalLayout shell.
 */
function App() {
    const { config } = usePortalConfig();
    useDocumentBranding(config);
    const { clientMac, originalUrl } = usePortalRedirectParams();
    const { isAuthenticated, loading: authLoading, buildLoginUrl, buildLogoutUrl } = useAuth();
    const memberAuthorizationStatus = useGuestAuthorization(isAuthenticated, clientMac);
    const { agbText, loading: agbTextLoading } = useAgbText();
    const { status: guestStatus, expiresAtUtc, authorize: authorizeGuest, reset: resetGuestAuthorization } = useGuestVisitorAuthorization(clientMac);

    const [guestPathChosen, setGuestPathChosen] = useState(false);
    const [guestAgreed, setGuestAgreed] = useState(false);
    const [memberRedirectStarted, setMemberRedirectStarted] = useState(false);

    const memberLoginUrl = buildLoginUrl(window.location.pathname + window.location.search);
    const memberLogoutUrl = buildLogoutUrl(window.location.pathname + window.location.search);

    useEffect(() => {
        if (memberRedirectStarted) {
            window.location.href = memberLoginUrl;
        }
    }, [memberRedirectStarted, memberLoginUrl]);

    if (authLoading) {
        return (
            <PortalLayout config={config} compactHero>
                <ProgressStep title="Loading…" />
            </PortalLayout>
        );
    }

    if (isAuthenticated) {
        const compactHero = Boolean(clientMac) && memberAuthorizationStatus === 'authorizing';
        // There's no local "close" for an authenticated session the way
        // guestPathChosen/memberRedirectStarted can just be reset — the
        // browser holds a real signed-in cookie. Signing out is the actual
        // equivalent of "leave this and go back to Choose" here, so X wired
        // to buildLogoutUrl keeps that control meaningful on every step
        // instead of only where a local flag happens to make it easy.
        const signOutToChoose = () => {
            window.location.href = memberLogoutUrl;
        };
        return (
            <PortalLayout config={config} compactHero={compactHero}>
                {!clientMac && (
                    <NoticeStep
                        kicker="Member access"
                        onClose={signOutToChoose}
                        title="No device detected"
                        body="You're signed in, but no device was detected to authorize. Please reconnect to the guest Wi-Fi and try again."
                    />
                )}
                {clientMac && memberAuthorizationStatus === 'authorizing' && (
                    <ProgressStep
                        kicker="Member access"
                        onClose={signOutToChoose}
                        title="Connecting your device"
                        body="You'll be moved to the member network automatically."
                    />
                )}
                {clientMac && memberAuthorizationStatus === 'authorized' && (
                    <ConnectedStep
                        networkLabel={config.memberNetworkLabel}
                        bodyText="Your device is connected to the member network."
                        expiresAtUtc={null}
                        originalUrl={originalUrl}
                    />
                )}
                {clientMac && memberAuthorizationStatus === 'error' && (
                    <NoticeStep
                        kicker="Member access"
                        onClose={signOutToChoose}
                        title="Something went wrong"
                        body="Something went wrong authorizing your device. Please try reconnecting to the guest Wi-Fi."
                    />
                )}
            </PortalLayout>
        );
    }

    if (memberRedirectStarted) {
        return (
            <PortalLayout config={config} compactHero>
                <ProgressStep
                    kicker="Member access"
                    onClose={() => setMemberRedirectStarted(false)}
                    title="Redirecting to Microsoft 365"
                    body={`Sign in with your ${config.orgName} account. You'll be moved to the member network automatically.`}
                >
                    <TenantUrlChip tenant={config.tenant} />
                </ProgressStep>
            </PortalLayout>
        );
    }

    if (guestPathChosen) {
        const closeGuestFlow = () => {
            setGuestPathChosen(false);
            setGuestAgreed(false);
            resetGuestAuthorization();
        };

        if (!clientMac) {
            return (
                <PortalLayout config={config}>
                    <NoticeStep
                        kicker="Guest access"
                        onClose={closeGuestFlow}
                        title="No device detected"
                        body="No device was detected to authorize. Please reconnect to the guest Wi-Fi and try again."
                    />
                </PortalLayout>
            );
        }

        if (guestStatus === 'authorized') {
            return (
                <PortalLayout config={config}>
                    <ConnectedStep
                        networkLabel={config.guestNetworkLabel}
                        bodyText="Your device is connected to the guest network."
                        expiresAtUtc={expiresAtUtc}
                        originalUrl={originalUrl}
                    />
                </PortalLayout>
            );
        }

        if (guestStatus === 'error') {
            return (
                <PortalLayout config={config}>
                    <NoticeStep
                        kicker="Guest access"
                        onClose={closeGuestFlow}
                        title="Something went wrong"
                        body="Something went wrong authorizing your device. Please try again."
                    />
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
                    onClose={closeGuestFlow}
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
