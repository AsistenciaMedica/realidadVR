export const navigation = [
  { label: 'Producto', to: '/#producto' },
  { label: 'Escenarios', to: '/scenarios' },
  { label: 'Tecnología', to: '/technology' },
  { label: 'Contacto', to: '/contact' },
];

const environmentPresentation = {
  gym: { description: 'Valoración y respuesta a emergencias durante el entrenamiento físico.', accent: 'red' },
  mall: { description: 'Atención inicial y toma de decisiones ante emergencias en un espacio público.', accent: 'cyan' },
  football: { description: 'Respuesta ante emergencias deportivas en el campo de fútbol.', accent: 'red' },
};

export function getScenarioCards(catalog) {
  return catalog.releaseScope.environments.map((environment) => {
    const cases = environment.scenarioIds.map((id) => catalog.scenarios.find((scenario) => scenario.id === id));
    return {
      ...environmentPresentation[environment.id],
      id: environment.id,
      name: environment.name,
      cases,
      tags: [...new Set(cases.map((scenario) => scenario.category))],
    };
  });
}

export function getStats(catalog) {
  const environments = catalog.releaseScope.environments;
  return [
    { value: String(environments.length).padStart(2, '0'), label: 'escenarios' },
    { value: String(catalog.scenarios.length).padStart(2, '0'), label: 'casos' },
    { value: String(environments[0].scenarioIds.length).padStart(2, '0'), label: 'casos por escenario' },
  ];
}

export const procedureCards = [
  { name: 'Valoración inicial', icon: '✦', description: 'Identificación del paciente y primeras decisiones.', cta: 'Ver más' },
  { name: 'RCP', icon: '♥', description: 'Reanimación cardiopulmonar y control del ritmo.', cta: 'Ver más' },
  { name: 'DEA', icon: '⚡', description: 'Preparación y uso del desfibrilador externo automático.', cta: 'Ver más' },
  { name: 'Tensión arterial', icon: '◌', description: 'Interpretación y registro de constantes simuladas.', cta: 'Ver más' },
  { name: 'Saturación de oxígeno', icon: '◎', description: 'Control de oxigenación y valoración respiratoria.', cta: 'Ver más' },
  { name: 'Glucemia', icon: '◍', description: 'Evaluación del nivel de glucosa y su impacto clínico.', cta: 'Ver más' },
];

export const technologyFeatures = [
  ['Realidad virtual', 'Experiencia inmersiva pensada para la formación y la repetición.'],
  ['Interacción', 'Acciones y decisiones que responden a las decisiones del usuario.'],
  ['Evaluación', 'Registro de errores, tiempos y resultados para discusión posterior.'],
  ['Debrief', 'Revision de decisiones y puntos críticos tras cada intento.'],
  ['Repetibilidad', 'Ciclos de entrenamiento que se repiten en un entorno seguro.'],
  ['Multiplataforma', 'Compatibilidad con Windows y preparación para Meta Quest 3.'],
];

export const highlights = [
  { title: 'Realidad virtual', icon: '◉' },
  { title: 'Escenarios repetibles', icon: '◌' },
  { title: 'Evaluación y debrief', icon: '△' },
  { title: 'Mayor preparación', icon: '◇' },
];

export const environmentList = [
  'Centros de formación',
  'Instituciones educativas',
  'Organizaciones y empresas',
  'Equipos de emergencias',
  'Demostraciones comerciales',
];
