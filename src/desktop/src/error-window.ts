import { BrowserWindow } from 'electron'

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
}

// A self-contained failure surface (inline data: URL, no bundled asset) so a startup failure is
// visible and quittable instead of a silent no-op.
export function showErrorWindow(err: unknown): BrowserWindow {
  const message = err instanceof Error ? err.message : String(err)
  const win = new BrowserWindow({
    width: 680,
    height: 460,
    title: 'Jobfinder — startup error',
    backgroundColor: '#F4F6FB',
  })
  win.setMenuBarVisibility(false)

  const html = `<!doctype html><html><head><meta charset="utf-8"></head>
<body style="font-family:system-ui,Segoe UI,sans-serif;margin:0;padding:28px;color:#04183B;background:#F4F6FB">
  <h2 style="margin:0 0 12px">Jobfinder couldn't start</h2>
  <p style="margin:0 0 16px;color:#2C4F8E">The background service failed to launch. Details:</p>
  <pre style="white-space:pre-wrap;background:#fff;border:1px solid #d3d9e6;border-radius:8px;padding:14px;max-height:240px;overflow:auto">${escapeHtml(message)}</pre>
  <button onclick="window.close()" style="margin-top:18px;padding:9px 18px;border:0;border-radius:8px;background:#0A2456;color:#fff;font-size:14px;cursor:pointer">Quit</button>
</body></html>`

  void win.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(html))
  return win
}
