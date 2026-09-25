import type { CSSProperties, PropsWithChildren } from 'react';
import type { PortalConfig } from '../../branding/defaultConfig';

interface PortalLayoutProps extends PropsWithChildren {
    /** Branding/copy driving the hero image, logo and accent color. */
    config: PortalConfig;
    /**
     * Shrinks the hero region on phone-width layouts, matching the design's
     * behavior of a tall hero only on the Choose step and a compact one on
     * every other step. Ignored on wide layouts, where the hero always
     * fills the available height.
     */
    compactHero?: boolean;
}

/**
 * The captive portal's shared shell: a hero image region (phone: top band,
 * wide ≥900px: left column) plus a scrollable content panel, matching
 * design_handoff_wlan_captive_portal/design/Portal.dc.html. Renders the
 * operator's logo/hero image from `config`, falling back to a striped
 * placeholder / no logo when unset.
 */
export function PortalLayout({ config, compactHero = false, children }: PortalLayoutProps) {
    return (
        <div className="portal-shell" style={{ '--color-accent': config.accentColor } as CSSProperties}>
            <div className={`portal-hero${compactHero ? ' portal-hero--compact' : ''}${config.heroImageUrl ? '' : ' portal-hero--placeholder'}`}>
                {config.heroImageUrl && <img className="portal-hero-img" src={config.heroImageUrl} alt="" />}
                <div className="portal-hero-fade" />
                <div className="portal-hero-top">
                    {config.logoUrl && (
                        <div className={`portal-logo-wrap${config.logoPlate ? ' portal-logo-wrap--plate' : ''}`}>
                            <img className="portal-logo" src={config.logoUrl} alt={config.orgName} />
                        </div>
                    )}
                </div>
            </div>
            <div className="portal-panel">
                <div className="portal-panel-content">{children}</div>
                <div className="portal-credit-footer">
                    developed by{' '}
                    <span className="portal-credit-wordmark">
                        <span className="portal-credit-wordmark-ge">GE</span>
                        <span className="portal-credit-wordmark-pf">PF</span>
                    </span>
                </div>
            </div>
        </div>
    );
}
