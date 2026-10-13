const statusEl = document.getElementById('status');
const assistantEl = document.getElementById('assistant');
const refreshBtn = document.getElementById('refresh');

async function loadLinks() {
  statusEl.textContent = 'Loading...';
  assistantEl.textContent = '';
  try {
    const res = await fetch('/events/links');
    if (!res.ok) throw new Error(res.statusText || 'Failed');
    const data = await res.json();
    const assistant = data && data.assistant ? data.assistant : '';
    if (assistant) {
      assistantEl.textContent = assistant;
      statusEl.textContent = 'Assistant response received';
    } else {
      assistantEl.textContent = 'No assistant response';
      statusEl.textContent = 'No response from Gemini';
    }
  } catch (err) {
    statusEl.textContent = 'Error loading assistant response';
    console.error(err);
  }
}
refreshBtn.addEventListener('click', () => loadLinks());

loadLinks();
