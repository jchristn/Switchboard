import PropTypes from 'prop-types';

// Inline SVG icons. Each accepts { size = 18, ...props } and inherits color via
// stroke="currentColor" (fill for Github). Keep them presentational — callers add
// aria-label/title on the surrounding button.

function Svg({ size, children, fill = 'none', stroke = 'currentColor', ...props }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={fill}
      stroke={stroke}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {children}
    </svg>
  );
}

Svg.propTypes = {
  size: PropTypes.oneOfType([PropTypes.number, PropTypes.string]),
  children: PropTypes.node,
  fill: PropTypes.string,
  stroke: PropTypes.string,
};

export function Kebab({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="12" cy="5" r="1.5" />
      <circle cx="12" cy="12" r="1.5" />
      <circle cx="12" cy="19" r="1.5" />
    </Svg>
  );
}

export function Copy({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <rect x="9" y="9" width="13" height="13" rx="2" />
      <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
    </Svg>
  );
}

export function Check({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M20 6 9 17l-5-5" />
    </Svg>
  );
}

export function Github({ size = 18, ...props }) {
  return (
    <Svg size={size} fill="currentColor" stroke="none" {...props}>
      <path d="M12 2C6.48 2 2 6.58 2 12.25c0 4.53 2.87 8.37 6.84 9.73.5.09.68-.22.68-.49 0-.24-.01-.87-.01-1.71-2.78.62-3.37-1.37-3.37-1.37-.45-1.18-1.11-1.49-1.11-1.49-.91-.64.07-.62.07-.62 1 .07 1.53 1.06 1.53 1.06.89 1.56 2.34 1.11 2.91.85.09-.66.35-1.11.63-1.36-2.22-.26-4.56-1.14-4.56-5.07 0-1.12.39-2.03 1.03-2.75-.1-.26-.45-1.3.1-2.71 0 0 .84-.28 2.75 1.05a9.36 9.36 0 0 1 5 0c1.91-1.33 2.75-1.05 2.75-1.05.55 1.41.2 2.45.1 2.71.64.72 1.03 1.63 1.03 2.75 0 3.94-2.34 4.81-4.57 5.06.36.32.68.94.68 1.9 0 1.37-.01 2.48-.01 2.82 0 .27.18.59.69.49A10.26 10.26 0 0 0 22 12.25C22 6.58 17.52 2 12 2Z" />
    </Svg>
  );
}

export function Sun({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
    </Svg>
  );
}

export function Moon({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79Z" />
    </Svg>
  );
}

export function Logout({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="m16 17 5-5-5-5" />
      <path d="M21 12H9" />
    </Svg>
  );
}

export function Refresh({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M21 12a9 9 0 1 1-2.64-6.36" />
      <path d="M21 3v6h-6" />
    </Svg>
  );
}

export function ChevronLeft({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="m15 18-6-6 6-6" />
    </Svg>
  );
}

export function ChevronRight({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="m9 18 6-6-6-6" />
    </Svg>
  );
}

export function ChevronUp({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="m18 15-6-6-6 6" />
    </Svg>
  );
}

export function ChevronDown({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="m6 9 6 6 6-6" />
    </Svg>
  );
}

export function Search({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="11" cy="11" r="8" />
      <path d="m21 21-4.3-4.3" />
    </Svg>
  );
}

export function Plus({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M12 5v14M5 12h14" />
    </Svg>
  );
}

export function Close({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M18 6 6 18M6 6l12 12" />
    </Svg>
  );
}

export function Warning({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z" />
      <path d="M12 9v4M12 17h.01" />
    </Svg>
  );
}

export function Globe({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="12" cy="12" r="10" />
      <path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z" />
    </Svg>
  );
}

export function Eye({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z" />
      <circle cx="12" cy="12" r="3" />
    </Svg>
  );
}

export function Pencil({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M17 3a2.85 2.83 0 0 1 4 4L7.5 20.5 2 22l1.5-5.5Z" />
      <path d="m15 5 4 4" />
    </Svg>
  );
}

export function Trash({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m2 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
      <path d="M10 11v6M14 11v6" />
    </Svg>
  );
}

export function Code({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="m16 18 6-6-6-6M8 6l-6 6 6 6" />
    </Svg>
  );
}

export function Menu({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M3 12h18M3 6h18M3 18h18" />
    </Svg>
  );
}

export function ExternalLink({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M15 3h6v6M10 14 21 3" />
      <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
    </Svg>
  );
}

export function ArrowRight({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M5 12h14M12 5l7 7-7 7" />
    </Svg>
  );
}

export function Power({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M12 2v10" />
      <path d="M18.36 6.64a9 9 0 1 1-12.73 0" />
    </Svg>
  );
}

export function Gauge({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M3.5 18a9 9 0 1 1 17 0" />
      <path d="M12 12l4-3" />
    </Svg>
  );
}

export function History({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M3 12a9 9 0 1 0 3-6.7L3 8" />
      <path d="M3 3v5h5" />
      <path d="M12 7v5l3 2" />
    </Svg>
  );
}

export function Server({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <rect x="2" y="3" width="20" height="7" rx="2" />
      <rect x="2" y="14" width="20" height="7" rx="2" />
      <path d="M6 6.5h.01M6 17.5h.01" />
    </Svg>
  );
}

export function Route({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="6" cy="19" r="3" />
      <circle cx="18" cy="5" r="3" />
      <path d="M9 19h6.5a3.5 3.5 0 0 0 0-7h-7A3.5 3.5 0 0 1 5 8.5V5" />
    </Svg>
  );
}

export function Shuffle({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M2 18h1.4c1.3 0 2.5-.6 3.3-1.7l6.1-8.6c.7-1.1 2-1.7 3.3-1.7H22" />
      <path d="m18 2 4 4-4 4" />
      <path d="M2 6h1.9c1.5 0 2.9.9 3.6 2.2" />
      <path d="M22 18h-5.9c-1.3 0-2.6-.7-3.3-1.8l-.5-.8" />
      <path d="m18 14 4 4-4 4" />
    </Svg>
  );
}

export function Users({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
    </Svg>
  );
}

export function Key({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="7.5" cy="15.5" r="5.5" />
      <path d="m21 2-9.6 9.6" />
      <path d="m15.5 7.5 3 3L22 7l-3-3" />
    </Svg>
  );
}

export function Shield({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
    </Svg>
  );
}

export function Settings({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </Svg>
  );
}

export function Terminal({ size = 18, ...props }) {
  return (
    <Svg size={size} {...props}>
      <path d="m4 17 6-6-6-6" />
      <path d="M12 19h8" />
    </Svg>
  );
}

const iconPropTypes = { size: PropTypes.oneOfType([PropTypes.number, PropTypes.string]) };

Kebab.propTypes = iconPropTypes;
Copy.propTypes = iconPropTypes;
Check.propTypes = iconPropTypes;
Github.propTypes = iconPropTypes;
Sun.propTypes = iconPropTypes;
Moon.propTypes = iconPropTypes;
Logout.propTypes = iconPropTypes;
Refresh.propTypes = iconPropTypes;
ChevronLeft.propTypes = iconPropTypes;
ChevronRight.propTypes = iconPropTypes;
ChevronUp.propTypes = iconPropTypes;
ChevronDown.propTypes = iconPropTypes;
Search.propTypes = iconPropTypes;
Plus.propTypes = iconPropTypes;
Close.propTypes = iconPropTypes;
Warning.propTypes = iconPropTypes;
Globe.propTypes = iconPropTypes;
Eye.propTypes = iconPropTypes;
Pencil.propTypes = iconPropTypes;
Trash.propTypes = iconPropTypes;
Code.propTypes = iconPropTypes;
Menu.propTypes = iconPropTypes;
ExternalLink.propTypes = iconPropTypes;
ArrowRight.propTypes = iconPropTypes;
Power.propTypes = iconPropTypes;
Gauge.propTypes = iconPropTypes;
History.propTypes = iconPropTypes;
Server.propTypes = iconPropTypes;
Route.propTypes = iconPropTypes;
Shuffle.propTypes = iconPropTypes;
Users.propTypes = iconPropTypes;
Key.propTypes = iconPropTypes;
Shield.propTypes = iconPropTypes;
Settings.propTypes = iconPropTypes;
Terminal.propTypes = iconPropTypes;
