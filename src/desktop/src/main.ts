import { app } from 'electron'
import { startBackend, shutdownBackend, killChild, type BackendHandle } from './backend'
import { createMainWindow } from './window'
import { showErrorWindow } from './error-window'

const APP_ID = 'dev.kasperbaun.jobfinder'

let backend: BackendHandle | null = null
let shuttingDown = false

if (process.platform === 'win32') app.setAppUserModelId(APP_ID)

// One shell instance owns one backend; a second launch just focuses the first.
if (!app.requestSingleInstanceLock()) {
  app.quit()
} else {
  app.whenReady().then(async () => {
    try {
      backend = await startBackend()
      createMainWindow(backend.port)
    } catch (err) {
      showErrorWindow(err)
    }
  })

  app.on('window-all-closed', () => {
    void shutdown()
  })

  app.on('before-quit', (event) => {
    if (!shuttingDown && backend) {
      event.preventDefault()
      void shutdown()
    }
  })

  // Last-resort synchronous reap if the main process is torn down abnormally.
  process.on('exit', () => {
    if (backend) killChild(backend.child)
  })
}

async function shutdown(): Promise<void> {
  if (shuttingDown) return
  shuttingDown = true
  if (backend) {
    await shutdownBackend(backend)
    backend = null
  }
  app.exit(0)
}
