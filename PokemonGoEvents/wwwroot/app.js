const statusEl = document.getElementById('status');
const linksEl = document.getElementById('links');
const filterEl = document.getElementById('filter');
const refreshBtn = document.getElementById('refresh');

async function loadLinks() {
  statusEl.textContent = 'Loading...';
  linksEl.innerHTML = '';
  try {
    const res = await fetch('/events/links');
    if (!res.ok) throw new Error(res.statusText || 'Failed');
    const urls = await res.json();
    renderLinks(urls || []);
    statusEl.textContent = `Found ${urls.length} links`;
  } catch (err) {
    statusEl.textContent = 'Error loading links';
    console.error(err);
  }
}

function renderLinks(urls) {
  const q = (filterEl.value || '').toLowerCase();
  const filtered = urls.filter(u => u.toLowerCase().includes(q));
  linksEl.innerHTML = filtered.map(u => `
    <li class="link-item">
      <a href="${u}" target="_blank" rel="noreferrer">${u}</a>
      <button class="copy" data-url="${u}">Copy</button>
    </li>
  `).join('') || '<li class="empty">No links</li>';

  document.querySelectorAll('.copy').forEach(btn => {
    btn.onclick = () => navigator.clipboard.writeText(btn.dataset.url);
  });
}

filterEl.addEventListener('input', () => loadLinks());
refreshBtn.addEventListener('click', () => loadLinks());

loadLinks();
