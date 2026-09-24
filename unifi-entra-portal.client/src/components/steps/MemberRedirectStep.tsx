import { LockSimpleIcon } from '../icons/LockSimpleIcon';

interface MemberRedirectStepProps {
    /** Organization name, interpolated into the body copy. */
    orgName: string;
    /** Friendly tenant domain shown in the URL chip, e.g. "contoso.onmicrosoft.com". Empty hides the chip. */
    tenant: string;
    /** Returns to the Choose step, e.g. before the real navigation has started. */
    onCancel: () => void;
}

/**
 * Brief interstitial shown while the browser navigates to Entra ID's sign-in
 * page — matching the "Member redirect" step of
 * design_handoff_wlan_captive_portal/design/Portal.dc.html. Microsoft's own
 * sign-in UI takes over once the real navigation completes; this screen is
 * not meant to be shown for long.
 */
export function MemberRedirectStep({ orgName, tenant, onCancel }: MemberRedirectStepProps) {
    return (
        <>
            <div className="portal-redirect-header">
                <div className="portal-spinner" />
                <div className="portal-redirect-text">
                    <h2 className="portal-redirect-title">Redirecting to Microsoft 365</h2>
                    <p className="portal-redirect-body">
                        Sign in with your {orgName} account. You'll be moved to the member network automatically.
                    </p>
                </div>
                {tenant && (
                    <div className="portal-url-chip">
                        <LockSimpleIcon size={14} className="portal-url-chip-lock" />
                        login.microsoftonline.com/{tenant}
                    </div>
                )}
            </div>
            <button type="button" className="portal-ghost-btn" onClick={onCancel}>
                Cancel
            </button>
        </>
    );
}
