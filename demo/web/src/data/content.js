export const navigation = [
  { label: 'Producto', to: '/#producto' },
  { label: 'Escenarios', to: '/scenarios' },
  { label: 'Tecnología', to: '/technology' },
  { label: 'Contacto', to: '/contact' },
];

const environmentPresentation = {
  gym: { description: 'Valoración y respuesta a emergencias durante el entrenamiento físico.', accent: 'red' },
  street: { description: 'Mareo, paro cardiorrespiratorio y atragantamiento en una zona de atención de la vía pública.', accent: 'cyan' },
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
  { name: 'Toma de síntomas', icon: '◌', description: 'Conversación, observación y registro de lo que refiere el paciente.', cta: 'Ver más' },
  { name: 'Desobstrucción', icon: '◎', description: 'Maniobras de desobstrucción y expulsión del objeto en el paciente virtual.', cta: 'Ver más' },
  { name: 'Reevaluación', icon: '◍', description: 'Observación de la respuesta del paciente y comunicación del relevo.', cta: 'Ver más' },
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
