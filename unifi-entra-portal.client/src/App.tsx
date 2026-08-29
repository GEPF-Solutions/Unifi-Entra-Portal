import { useState } from 'react';
import './App.css';
import { brandingConfig } from './branding/config';
import ChoicePathScreen from './components/ChoicePathScreen';
import GuestAgbScreen from './components/GuestAgbScreen';
import { useAuth } from './hooks/useAuth';
import { useGuestAuthorization } from './hooks/useGuestAuthorization';
import { usePortalRedirectParams } from './hooks/usePortalRedirectParams';

function App() {
    const { clientMac, originalUrl } = usePortalRedirectParams();
    const { isAuthenticated, name, loading, buildLoginUrl } = useAuth();
    const authorizationStatus = useGuestAuthorization(isAuthenticated, clientMac);
    const [guestPathChosen, setGuestPathChosen] = useState(false);
    const displayName = name ?? 'there';

    if (loading) {
        return (
            <div className="portal">
                <p>Loading…</p>
            </div>
        );
    }

    return (
        <div className="portal">
            {brandingConfig.logoUrl && (
                <img className="portal-logo" src={brandingConfig.logoUrl} alt={brandingConfig.orgName} />
            )}
            <h1>{brandingConfig.orgName}</h1>

            {!isAuthenticated && !guestPathChosen && (
                <ChoicePathScreen
                    memberLoginUrl={buildLoginUrl(window.location.pathname + window.location.search)}
                    onChooseGuest={() => setGuestPathChosen(true)}
                />
            )}

            {!isAuthenticated && guestPathChosen && (
                <GuestAgbScreen macAddress={clientMac} originalUrl={originalUrl} />
            )}

            {isAuthenticated && !clientMac && (
                <p>
                    You're signed in as {displayName}, but no device was detected to authorize. Please reconnect to the
                    guest Wi-Fi and try again.
                </p>
            )}

            {isAuthenticated && clientMac && authorizationStatus === 'authorizing' && <p>Connecting your device…</p>}

            {isAuthenticated && clientMac && authorizationStatus === 'authorized' && (
                <>
                    <p>You're connected, {displayName}. You can now browse normally.</p>
                    {originalUrl && (
                        <a className="portal-button" href={originalUrl}>
                            Continue browsing
                        </a>
                    )}
                </>
            )}

            {isAuthenticated && clientMac && authorizationStatus === 'error' && (
                <p>Something went wrong authorizing your device. Please try reconnecting to the guest Wi-Fi.</p>
            )}
        </div>
    );
}

export default App;
