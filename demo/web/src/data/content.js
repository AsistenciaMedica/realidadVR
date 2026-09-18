export const navigation = [
  { label: 'Producto', to: '/#producto' },
  { label: 'Escenarios', to: '/scenarios' },
  { label: 'Tecnología', to: '/technology' },
  { label: 'Contacto', to: '/contact' },
];

export const scenarioCards = [
  {
    id: 'dental',
    name: 'Clínica dental',
    tag: 'RCP',
    tags: ['RCP', 'Respiratorio', 'Procedimiento'],
    description: 'Ambiente clínico con paciente bajo evaluación en un entorno preparado para atención inmediata.',
    image: '/training-room.png',
    accent: 'cyan',
  },
  {
    id: 'gym',
    name: 'Gimnasio',
    tag: 'Trauma',
    tags: ['Trauma', 'RCP', 'Respiratorio'],
    description: 'Entrenamiento de respuesta rápida en un sitio físico con acceso limitado y ruido ambiental.',
    image: '/training-room.png',
    accent: 'red',
  },
  {
    id: 'mall',
    name: 'Centro comercial',
    tag: 'Conciencia',
    tags: ['Conciencia', 'Glucosa', 'Observación'],
    description: 'Situación de atención sanitaria en un espacio público con varios puntos de decisión.',
    image: '/training-room.png',
    accent: 'cyan',
  },
  {
    id: 'football',
    name: 'Campo de fútbol',
    tag: 'Trauma',
    tags: ['Trauma', 'RCP', 'Urgencia'],
    description: 'Emergencia deportiva con coordinación, monitorización y toma de decisiones bajo presión.',
    image: '/training-room.png',
    accent: 'red',
  },
];

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

export const stats = [
  { value: '04', label: 'ambientes' },
  { value: '44', label: 'escenarios' },
  { value: '03', label: 'rutas' },
];

export const environmentList = [
  'Centros de formación',
  'Instituciones educativas',
  'Organizaciones y empresas',
  'Equipos de emergencias',
  'Demostraciones comerciales',
];
