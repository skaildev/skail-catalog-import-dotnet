const SECONDS_BETWEEN_PAGES = 2;
const KEY = 'catalog-import.importId';
const TERMINAL = ['Completed', 'Failed'];

const $ = (id) => document.getElementById(id);
const els = {
  button: $('import'), simulateFailure: $('simulateFailure'), state: $('state'), counter: $('counter'),
  bar: $('bar'), fill: $('fill'), products: $('products'), error: $('error'), detail: $('detail'),
  pagesSection: $('pagesSection'), pages: $('pages'), importId: $('importId'),
};

let current = null;
let source = null;
let busy = false;
let startError = null;
let countdownTimer = null;
let savedAtCountdown = -1;
let secondsLeft = 0;

// Pages the target catalog already accepted.
const savedPages = () => (current?.pages ?? []).filter((p) => p.products !== null).length;

// Visual hint of the 2-second wait between pages: it restarts each time a page is saved.
function updateCountdown() {
  const saved = savedPages();
  const waiting = current?.stage === 'Started' && saved > 0;
  if (!waiting) {
    clearInterval(countdownTimer);
    countdownTimer = null;
    savedAtCountdown = -1;
    return;
  }
  if (saved !== savedAtCountdown) {
    savedAtCountdown = saved;
    secondsLeft = SECONDS_BETWEEN_PAGES;
    clearInterval(countdownTimer);
    countdownTimer = setInterval(() => {
      secondsLeft = Math.max(0, secondsLeft - 1);
      renderBadge();
    }, 1000);
  }
}

function badgeLabel() {
  const stage = current?.stage;
  if (!stage) return 'Idle';
  if (stage === 'Starting') return 'Starting…';
  if (stage === 'Started') {
    const waiting = savedPages() > 0 && secondsLeft > 0 && countdownTimer;
    return waiting ? `Next page in ${secondsLeft}s` : `Importing page ${savedPages() + 1}/${current.expectedPages}`;
  }
  return stage;
}

function renderBadge() {
  const stage = current?.stage;
  els.state.textContent = badgeLabel();
  els.state.className = 'badge ' + (!stage ? 'idle' : stage === 'Failed' ? 'failed' : stage === 'Completed' ? 'done' : 'active');
}

function render() {
  const ui = current?.ui ?? { label: 'Import catalog', enabled: true };
  els.button.textContent = ui.label;
  els.button.disabled = !ui.enabled || busy;
  els.simulateFailure.disabled = !ui.enabled || busy;

  updateCountdown();
  renderBadge();
  els.counter.textContent = current ? `${savedPages()}/${current.expectedPages} pages` : '';

  const imported = current?.imported ?? 0;
  const expected = current?.expectedProducts ?? 194;
  els.bar.setAttribute('aria-valuenow', imported);
  els.bar.setAttribute('aria-valuemax', expected);
  els.fill.style.width = `${Math.min(100, (imported / expected) * 100)}%`;
  els.products.textContent = `${imported}/${expected} products`;

  els.error.hidden = !startError;
  els.error.textContent = startError ?? '';
  els.detail.hidden = !current?.detail;
  els.detail.textContent = current?.detail ?? '';
  els.detail.className = current?.stage === 'Failed' ? 'error' : 'detail';

  const pages = current?.pages ?? [];
  els.pagesSection.hidden = pages.length === 0;
  els.pages.replaceChildren(...pages.map((p) => {
    const row = document.createElement('tr');
    if (p.attempts > 1) row.className = 'retried';
    const cells = [p.page, p.products ?? 'rejected, retrying', p.durationMs === null ? '' : `${p.durationMs} ms`, p.attempts];
    for (const text of cells) {
      const cell = document.createElement('td');
      cell.textContent = text;
      row.append(cell);
    }
    return row;
  }));

  els.importId.textContent = current?.importId ?? '-';
}

function follow(importId) {
  source?.close();
  source = new EventSource(`/api/imports/${importId}/stream`);
  source.onmessage = (event) => {
    current = JSON.parse(event.data);
    render();
    if (TERMINAL.includes(current.stage)) {
      source.close();
      localStorage.removeItem(KEY);
    }
  };
  // EventSource reconnects by itself, and the stream sends the current snapshot first
}

async function start() {
  busy = true;
  startError = null;
  render();
  try {
    const res = await fetch('/api/imports', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ simulateFailure: els.simulateFailure.checked }),
    });
    if (!res.ok) throw new Error((await res.json().catch(() => ({}))).error ?? `HTTP ${res.status}`);
    current = await res.json();
    localStorage.setItem(KEY, current.importId);
    follow(current.importId);
  } catch (e) {
    startError = `Could not start: ${e.message}`;
  } finally {
    busy = false;
    render();
  }
}

els.button.addEventListener('click', start);

// reload keeps the controls right: the state always comes from the server
const saved = localStorage.getItem(KEY);
if (saved) {
  fetch(`/api/imports/${saved}`).then((r) => (r.ok ? r.json() : null)).then((restored) => {
    if (!restored) return localStorage.removeItem(KEY);
    current = restored;
    els.simulateFailure.checked = restored.simulateFailure;
    render();
    if (TERMINAL.includes(restored.stage)) localStorage.removeItem(KEY);
    else follow(restored.importId);
  }, () => localStorage.removeItem(KEY));
}
render();
