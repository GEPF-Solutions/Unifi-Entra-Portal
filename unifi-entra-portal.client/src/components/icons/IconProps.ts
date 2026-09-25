/** Shared props for the self-hosted Phosphor "regular" icon components. */
export interface IconProps {
    /** Pixel size for both width and height. Defaults to the current font size via 1em. */
    size?: number | string;
    /** Additional class names to apply to the root `<svg>`. */
    className?: string;
}
