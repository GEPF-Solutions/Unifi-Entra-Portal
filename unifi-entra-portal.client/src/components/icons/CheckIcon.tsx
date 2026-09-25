import type { IconProps } from './IconProps';

/** Checkmark icon (Phosphor "regular" set, self-hosted). Shown in the checkbox and success badge. */
export function CheckIcon({ size = '1em', className }: IconProps) {
    return (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width={size} height={size} fill="currentColor" className={className} aria-hidden="true">
            <path d="M229.66,77.66l-128,128a8,8,0,0,1-11.32,0l-56-56a8,8,0,0,1,11.32-11.32L96,188.69,218.34,66.34a8,8,0,0,1,11.32,11.32Z" />
        </svg>
    );
}
