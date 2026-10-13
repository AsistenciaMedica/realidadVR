const labels = {
  gym: 'Paciente y equipo de atención en el gimnasio de VITAL VR',
  mall: 'Zona de atención y escaparates del centro comercial de VITAL VR',
  football: 'Campo de fútbol y zona de atención de VITAL VR',
};

export default function ScenarioPreview({ environment, eager = false }) {
  if (environment === 'street') return (
    <svg className="scenario-preview" viewBox="0 0 1440 810" role="img" aria-label="Ilustración del entorno de calle con acera, fachadas y paso peatonal">
      <rect width="1440" height="810" fill="#c7dce5" />
      <path d="M0 120H330V470H0ZM380 50H870V470H380ZM930 140H1440V470H930Z" fill="#66818a" />
      {[90,210,450,590,730,1010,1160,1310].map(x => <path key={x} d={`M${x} 185h65v90h-65ZM${x} 315h65v90h-65Z`} fill="#d6e7ec" />)}
      <path d="M0 470H1440V650H0Z" fill="#b9b9ac" />
      <path d="M0 650H1440V810H0Z" fill="#394950" />
      <path d="M70 700H360M440 700H730M810 700H1100" stroke="#eee8d9" strokeWidth="12" />
      <path d="M1190 668h50v130h-50ZM1280 668h50v130h-50ZM1370 668h50v130h-50Z" fill="#eee8d9" />
      <path d="M340 525h290v30H340ZM360 555v65M610 555v65" fill="#845e44" stroke="#374950" strokeWidth="12" />
      <circle cx="185" cy="410" r="72" fill="#638473" /><path d="M185 470v138" stroke="#745c49" strokeWidth="18" />
      <text x="1070" y="585" fill="#34464d" fontSize="30" textAnchor="middle">CALLE · ILUSTRACIÓN</text>
    </svg>
  );
  if (!labels[environment]) return null;
  return (
    <img
      className="scenario-preview"
      src={`/media/scenarios/${environment}.png`}
      alt={labels[environment]}
      width="1440"
      height="810"
      loading={eager ? 'eager' : 'lazy'}
      decoding="async"
    />
  );
}
