export function BackgroundScene({ tone = 'cyan' }) {
  const colors = {
    cyan: { a: '#08add6', b: '#0b2037', c: '#dfeef6', d: '#ed1939' },
    red: { a: '#ed1939', b: '#081f37', c: '#f5edf0', d: '#08add6' },
  };
  const p = colors[tone] || colors.cyan;

  return (
    <svg viewBox="0 0 720 560" role="img" aria-label="Fondo médico y VR">
      <defs>
        <linearGradient id={`bg-${tone}`} x1="0" x2="1" y1="0" y2="1">
          <stop offset="0%" stopColor={p.b} />
          <stop offset="58%" stopColor="#0a1d30" />
          <stop offset="100%" stopColor="#040d1a" />
        </linearGradient>
      </defs>
      <rect width="720" height="560" fill={`url(#bg-${tone})`} />
      <circle cx="570" cy="100" r="150" fill={p.a} opacity="0.14" />
      <circle cx="170" cy="120" r="160" fill={p.d} opacity="0.1" />
      <rect x="90" y="60" width="540" height="400" rx="20" fill="rgba(7,24,38,0.26)" stroke="rgba(143,196,225,0.25)" />
      <rect x="120" y="194" width="110" height="170" rx="16" fill="rgba(255,255,255,0.05)" />
      <rect x="270" y="128" width="150" height="220" rx="18" fill="rgba(255,255,255,0.03)" />
      <rect x="460" y="150" width="130" height="180" rx="18" fill="rgba(255,255,255,0.04)" />
      <path d="M150 270 L180 270 L185 370 L145 370 Z" fill={p.c} opacity="0.18" />
      <path d="M300 160 L370 160 L390 360 L280 360 Z" fill={p.a} opacity="0.14" />
      <path d="M488 170 L560 170 L573 320 L475 320 Z" fill={p.d} opacity="0.12" />
      <path d="M140 430 L576 430" stroke="rgba(255,255,255,0.07)" strokeWidth="2" />
      <path d="M205 452 L510 452" stroke="rgba(255,255,255,0.05)" strokeWidth="2" />
      <path d="M245 112 L425 112" stroke="rgba(255,255,255,0.08)" strokeWidth="2" />
    </svg>
  );
}

export function HeroMedicalScene() {
  return (
    <svg viewBox="0 0 720 560" role="img" aria-label="Simulación médica en realidad virtual">
      <defs>
        <linearGradient id="heroBg" x1="0" x2="1" y1="0" y2="1">
          <stop offset="0%" stopColor="#071b2e" />
          <stop offset="50%" stopColor="#0d223b" />
          <stop offset="100%" stopColor="#0a1729" />
        </linearGradient>
        <linearGradient id="visorGlow" x1="0" x2="1">
          <stop offset="0%" stopColor="#5fe6ff" stopOpacity="0.9" />
          <stop offset="100%" stopColor="#e33d68" stopOpacity="0.7" />
        </linearGradient>
      </defs>
      <rect width="720" height="560" fill="url(#heroBg)" />
      <circle cx="430" cy="110" r="140" fill="rgba(8,173,214,0.18)" />
      <circle cx="240" cy="120" r="120" fill="rgba(237,25,57,0.12)" />
      <rect x="90" y="85" width="600" height="420" rx="34" fill="rgba(9,20,35,0.35)" stroke="rgba(143,196,225,0.28)" />
      <path d="M145 430 L480 430 L555 514 L140 514 Z" fill="rgba(10,26,42,0.9)" />
      <ellipse cx="355" cy="460" rx="180" ry="40" fill="rgba(2,15,26,0.4)" />
      <g transform="translate(200 80)">
        <ellipse cx="120" cy="310" rx="95" ry="14" fill="rgba(8,173,214,0.22)" />
        <rect x="76" y="70" width="102" height="145" rx="44" fill="#d8edf3" opacity="0.9" />
        <path d="M88 98 L160 98 L176 210 L78 210 Z" fill="#dfeef2" opacity="0.75" />
        <path d="M92 120 L162 120" stroke="#a8c8d8" strokeWidth="3" strokeLinecap="round" />
        <path d="M90 148 L163 148" stroke="#a8c8d8" strokeWidth="3" strokeLinecap="round" />
        <path d="M88 175 L165 175" stroke="#a8c8d8" strokeWidth="3" strokeLinecap="round" />
        <path d="M110 208 Q150 255 160 238 L165 300 L89 310 L105 236 Z" fill="#dfeef2" opacity="0.9" />
        <path d="M150 300 Q176 300 186 328 L167 349 L120 349 L110 325 Q122 304 150 300 Z" fill="#dfeef2" opacity="0.9" />
        <rect x="30" y="260" width="214" height="18" rx="9" fill="url(#visorGlow)" opacity="0.9" />
        <rect x="32" y="250" width="210" height="28" rx="14" fill="rgba(255,255,255,0.12)" stroke="rgba(190,230,247,0.75)" />
        <path d="M40 260 L209 260" stroke="rgba(255,255,255,0.35)" strokeWidth="1.8" />
        <circle cx="110" cy="118" r="9" fill="#f7f9fc" opacity="0.75" />
        <circle cx="136" cy="118" r="9" fill="#f7f9fc" opacity="0.75" />
      </g>
      <g>
        <rect x="472" y="102" width="148" height="82" rx="12" fill="rgba(7,18,31,0.5)" stroke="rgba(8,173,214,0.45)" />
        <path d="M508 140 L588 140" stroke="rgba(8,173,214,0.75)" strokeWidth="2" />
        <path d="M508 158 L580 158" stroke="rgba(8,173,214,0.75)" strokeWidth="2" />
        <path d="M508 176 L572 176" stroke="rgba(8,173,214,0.75)" strokeWidth="2" />
      </g>
      <g>
        <circle cx="95" cy="118" r="14" fill="rgba(8,173,214,0.32)" />
        <circle cx="95" cy="118" r="6" fill="#78ebff" />
      </g>
    </svg>
  );
}

export function ScenarioArt({ variant = 'dental' }) {
  const palette = {
    dental: { a: '#38d3f7', b: '#0d2541', c: '#d8eef5' },
    gym: { a: '#5fd3ff', b: '#0d3147', c: '#e3f0f8' },
    mall: { a: '#58d0f8', b: '#0a1f32', c: '#dfeef6' },
    football: { a: '#59d9f3', b: '#123a38', c: '#d8f6ef' },
  }[variant] || { a: '#38d3f7', b: '#0d2541', c: '#d8eef5' };

  return (
    <svg viewBox="0 0 520 300" role="img" aria-label={`${variant} scenario`}>
      <defs>
        <linearGradient id={`scene-${variant}`} x1="0" x2="1">
          <stop offset="0%" stopColor={palette.a} stopOpacity="0.58" />
          <stop offset="100%" stopColor={palette.b} stopOpacity="0.85" />
        </linearGradient>
      </defs>
      <rect width="520" height="300" fill={`url(#scene-${variant})`} />
      <circle cx="420" cy="68" r="66" fill={palette.a} opacity="0.12" />
      <rect x="0" y="180" width="520" height="120" fill="rgba(14,27,42,0.88)" />
      <rect x="60" y="150" width="180" height="80" rx="10" fill="rgba(255,255,255,0.08)" stroke="rgba(255,255,255,0.15)" />
      <rect x="78" y="170" width="62" height="18" rx="5" fill={palette.c} opacity="0.9" />
      <rect x="148" y="170" width="72" height="18" rx="5" fill={palette.a} opacity="0.7" />
      <rect x="268" y="120" width="140" height="110" rx="16" fill="rgba(12,27,42,0.66)" stroke="rgba(255,255,255,0.1)" />
      <path d="M116 120 L200 120" stroke="rgba(255,255,255,0.18)" strokeWidth="5" strokeLinecap="round" />
      <path d="M150 90 Q204 42 262 92" fill="none" stroke="rgba(255,255,255,0.14)" strokeWidth="6" strokeLinecap="round" />
      <circle cx="228" cy="82" r="30" fill={palette.a} opacity="0.12" />
      <path d="M300 154 L350 154" stroke="rgba(255,255,255,0.16)" strokeWidth="4" strokeLinecap="round" />
      <path d="M300 180 L360 180" stroke="rgba(255,255,255,0.16)" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}

export function MetaQuestVisual() {
  return (
    <svg viewBox="0 0 420 260" role="img" aria-label="Meta Quest 3 illustration">
      <defs>
        <linearGradient id="questBg" x1="0" x2="1" y1="0" y2="1">
          <stop offset="0%" stopColor="#0b233a" />
          <stop offset="100%" stopColor="#081827" />
        </linearGradient>
      </defs>
      <rect width="420" height="260" rx="18" fill="url(#questBg)" />
      <path d="M80 155 C130 70, 285 70, 340 153 L300 170 C260 120, 160 120, 120 170 Z" fill="#dfeef6" opacity="0.9" />
      <path d="M145 150 C145 110, 270 110, 275 150 L275 175 C275 220, 145 220, 145 175 Z" fill="rgba(9,24,39,0.82)" stroke="rgba(8,173,214,0.55)" />
      <rect x="160" y="120" width="100" height="48" rx="16" fill="rgba(8,173,214,0.15)" stroke="rgba(255,255,255,0.3)" />
      <path d="M110 170 L70 200" stroke="rgba(8,173,214,0.7)" strokeWidth="8" strokeLinecap="round" />
      <path d="M310 170 L350 200" stroke="rgba(8,173,214,0.7)" strokeWidth="8" strokeLinecap="round" />
      <circle cx="180" cy="149" r="7" fill="#7ad9ff" />
      <circle cx="240" cy="149" r="7" fill="#7ad9ff" />
    </svg>
  );
}

export function ProcedureIcon({ symbol = '✦' }) {
  return (
    <div className="procedure-inline-icon" aria-hidden="true">
      {symbol}
    </div>
  );
}
