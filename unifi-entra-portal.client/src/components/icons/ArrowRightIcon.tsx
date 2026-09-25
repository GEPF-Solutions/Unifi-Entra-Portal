import type { IconProps } from './IconProps';

/** Right-pointing arrow icon (Phosphor "regular" set, self-hosted). Trails the choice buttons. */
export function ArrowRightIcon({ size = '1em', className }: IconProps) {
    return (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width={size} height={size} fill="currentColor" className={className} aria-hidden="true">
            <path d="M221.66,133.66l-72,72a8,8,0,0,1-11.32-11.32L196.69,136H40a8,8,0,0,1,0-16H196.69L138.34,61.66a8,8,0,0,1,11.32-11.32l72,72A8,8,0,0,1,221.66,133.66Z" />
        </svg>
    );
}
