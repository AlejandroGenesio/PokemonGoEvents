const statusEl = document.getElementById('status');
const assistantEl = document.getElementById('assistant');
const widgetEl = document.getElementById('widget');
const refreshBtn = document.getElementById('refresh');

async function loadLinks() {
  statusEl.textContent = 'Loading...';
  assistantEl.innerHTML = '';
  try {
    const res = await fetch('/events/links');
    if (!res.ok) throw new Error(res.statusText || 'Failed');
    const data = await res.json();
    const assistant = data && data.assistant ? data.assistant : '';
    const widget = data && data.widget ? data.widget : '';
    if (assistant) {
      // Sanitize by removing any <script> tags before rendering as HTML
      const sanitized = assistant.replace(/<script[\s\S]*?>[\s\S]*?<\/script>/gi, '');
      assistantEl.innerHTML = sanitized;
      statusEl.textContent = 'Assistant response received';
    } else {
      assistantEl.textContent = 'No assistant response';
      statusEl.textContent = 'No response from Gemini';
    }

    if (widget) {
      const sanitizedWidget = widget.replace(/<script[\s\S]*?>[\s\S]*?<\/script>/gi, '');
      widgetEl.innerHTML = sanitizedWidget;
    } else {
      widgetEl.textContent = 'No widget content yet.';
    }
  } catch (err) {
    statusEl.textContent = 'Error loading assistant response';
    console.error(err);
  }
}
refreshBtn.addEventListener('click', () => loadLinks());

loadLinks();
