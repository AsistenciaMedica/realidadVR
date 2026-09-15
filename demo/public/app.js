const tabs = [...document.querySelectorAll('[data-mode]')];
function activate(tab) {
  tabs.forEach(item => { const active = item === tab; item.setAttribute('aria-selected', active); item.tabIndex = active ? 0 : -1; document.querySelector(`#mode-${item.dataset.mode}`).hidden = !active; });
}
tabs.forEach((tab, index) => { tab.addEventListener('click', () => activate(tab)); tab.addEventListener('keydown', event => { if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return; event.preventDefault(); const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length; activate(tabs[next]); tabs[next].focus(); }); });
const dialog = document.querySelector('#image-dialog');
document.querySelector('#open-image').addEventListener('click', () => dialog.showModal());
document.querySelector('#close-image').addEventListener('click', () => dialog.close());
dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
const element = (tag, text, className) => { const node = document.createElement(tag); node.textContent = text; if (className) node.className = className; return node; };
try {
  const response = await fetch('/api/releases'); if (!response.ok) throw new Error();
  const { windows } = await response.json();
  const link = document.querySelector('#download');
  if (windows.available) { link.href = windows.url; link.hidden = false; document.querySelector('#download-status').textContent = windows.bytes ? `ZIP completo · ${(windows.bytes / 1048576).toFixed(1)} MB · Extraer antes de ejecutar` : 'Descarga de la entrega publicada'; }
  else document.querySelector('#download-status').textContent = 'La descarga estará disponible cuando se adjunte el ZIP de esta entrega. Puedes revisar la sala y el alcance más abajo.';
} catch { document.querySelector('#download-status').textContent = 'No pudimos comprobar la descarga. Recarga la página para intentarlo de nuevo.'; }
try {
  const response = await fetch('/progress.json'); if (!response.ok) throw new Error(); const data = await response.json();
  const list = document.querySelector('#progress-list');
  function render(filter = 'all') {
    list.replaceChildren();
    for (const item of data.items.filter(item => filter === 'all' || item.category === filter)) { const row = element('article', '', 'progress-row'); row.append(element('h3', item.name), element('p', item.detail), element('span', item.statusLabel, `status ${item.status}`)); list.append(row); }
  }
  render();
  document.querySelectorAll('[data-filter]').forEach(button => button.addEventListener('click', () => { document.querySelectorAll('[data-filter]').forEach(item => { item.classList.toggle('active', item === button); item.setAttribute('aria-pressed', item === button); }); render(button.dataset.filter); }));
  const catalog = await (await fetch('/api/catalog')).json();
  for (const item of catalog.scenarios.slice(0,5)) { const detail = element('details', ''); detail.append(element('summary', item.name), element('p', item.description)); const link = element('a', 'Revisar escenario y referencias ?'); link.href = '/scenarios/' + item.id; detail.append(link); document.querySelector('#case-list').append(detail); }

} catch { document.querySelector('#progress-list').textContent = 'No pudimos cargar el alcance. Recarga la página.'; }
