import { useEffect, useState } from 'react';
import { getScenarioCards, getStats } from './content';

let catalogRequest;

function loadCatalog() {
  if (!catalogRequest) {
    catalogRequest = fetch('/scenarios.json')
      .then((response) => {
        if (!response.ok) throw new Error('No se pudo cargar el catálogo.');
        return response.json();
      })
      .then((catalog) => {
        if (!catalog.releaseScope?.environments?.length) throw new Error('Catálogo sin alcance de lanzamiento.');
        return { catalog, scenarioCards: getScenarioCards(catalog), stats: getStats(catalog) };
      })
      .catch((error) => {
        catalogRequest = undefined;
        throw error;
      });
  }
  return catalogRequest;
}

export default function useReleaseCatalog() {
  const [state, setState] = useState({ catalog: null, scenarioCards: [], stats: [], error: null });
  useEffect(() => {
    let ignore = false;
    loadCatalog().then((data) => {
      if (!ignore) setState({ ...data, error: null });
    }).catch(() => {
      if (!ignore) setState((previous) => ({ ...previous, error: 'No se pudo cargar el catálogo. Recarga para intentarlo de nuevo.' }));
    });
    return () => { ignore = true; };
  }, []);
  return state;
}
