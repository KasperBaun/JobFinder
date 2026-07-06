import { app, screen, type BrowserWindow, type Rectangle } from 'electron'
import * as fs from 'node:fs'
import * as path from 'node:path'

// Preferred minimum window size. On a short work area — e.g. a high-DPI display whose DIP work
// area is smaller than this — the effective minimum shrinks to fit (see effectiveMin), so the
// window is never forced larger than the screen (the cause of a clipped main view).
const PREF_MIN_WIDTH = 900
const PREF_MIN_HEIGHT = 620

// A comfortable ceiling for the first-run default — the SPA layout tops out at 1180px, so anything
// past this is just empty margin.
const DEFAULT_MAX_WIDTH = 1360
const DEFAULT_MAX_HEIGHT = 900

// Room reserved for the window frame/title bar and a little breathing space, so the window (content
// size + chrome) never spills past the work area.
const EDGE_MARGIN = 40
const SAVE_DEBOUNCE_MS = 300

export interface WindowState {
  x?: number
  y?: number
  width: number
  height: number
  isMaximized: boolean
  minWidth: number
  minHeight: number
}

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value))

function stateFile(): string {
  return path.join(app.getPath('userData'), 'window-state.json')
}

// Usable window area on the primary display, in DIPs, minus a margin for the taskbar/breathing room.
function usableArea(): { width: number; height: number } {
  const { width, height } = screen.getPrimaryDisplay().workAreaSize
  return { width: Math.max(320, width - EDGE_MARGIN), height: Math.max(320, height - EDGE_MARGIN) }
}

// The min size can never exceed what the screen can show, or the window would be forced off-screen.
function effectiveMin(area: { width: number; height: number }): { minWidth: number; minHeight: number } {
  return {
    minWidth: Math.min(PREF_MIN_WIDTH, area.width),
    minHeight: Math.min(PREF_MIN_HEIGHT, area.height),
  }
}

// First-run / fallback size: fit the primary display's work area so the window never launches larger
// than the screen. No x/y ⇒ centered.
function adaptiveDefault(): WindowState {
  const area = usableArea()
  const min = effectiveMin(area)
  return {
    width: clamp(Math.min(DEFAULT_MAX_WIDTH, area.width), min.minWidth, area.width),
    height: clamp(Math.min(DEFAULT_MAX_HEIGHT, area.height), min.minHeight, area.height),
    isMaximized: false,
    ...min,
  }
}

function intersects(a: Rectangle, b: Rectangle): boolean {
  return (
    a.x < b.x + b.width &&
    a.x + a.width > b.x &&
    a.y < b.y + b.height &&
    a.y + a.height > b.y
  )
}

// A saved position is only usable if it still overlaps a connected display — otherwise the window
// would restore off-screen (e.g. after unplugging an external monitor).
function isVisible(bounds: Rectangle): boolean {
  return screen.getAllDisplays().some((d) => intersects(d.bounds, bounds))
}

export function loadWindowState(): WindowState {
  const fallback = adaptiveDefault()
  let saved: Partial<WindowState>
  try {
    saved = JSON.parse(fs.readFileSync(stateFile(), 'utf8')) as Partial<WindowState>
  } catch {
    return fallback
  }

  if (typeof saved.width !== 'number' || typeof saved.height !== 'number') return fallback

  const area = usableArea()
  const min = effectiveMin(area)
  const state: WindowState = {
    width: clamp(saved.width, min.minWidth, area.width),
    height: clamp(saved.height, min.minHeight, area.height),
    isMaximized: saved.isMaximized === true,
    ...min,
  }

  if (
    typeof saved.x === 'number' &&
    typeof saved.y === 'number' &&
    isVisible({ x: saved.x, y: saved.y, width: state.width, height: state.height })
  ) {
    state.x = saved.x
    state.y = saved.y
  }

  return state
}

function write(state: Pick<WindowState, 'x' | 'y' | 'width' | 'height' | 'isMaximized'>): void {
  try {
    fs.writeFileSync(stateFile(), JSON.stringify(state, null, 2))
  } catch {
    // Window chrome state is best-effort — never let a failed write crash the shell.
  }
}

// Persist size/position as the user drags, resizes, or (un)maximizes. getNormalBounds() reports the
// restored (non-maximized) rect, so re-launching a maximized window still remembers its prior size.
export function trackWindowState(win: BrowserWindow): void {
  let timer: NodeJS.Timeout | null = null

  const save = () => {
    if (win.isDestroyed()) return
    const bounds = win.getNormalBounds()
    write({ ...bounds, isMaximized: win.isMaximized() })
  }

  const saveDebounced = () => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(save, SAVE_DEBOUNCE_MS)
  }

  win.on('resize', saveDebounced)
  win.on('move', saveDebounced)
  win.on('maximize', save)
  win.on('unmaximize', save)
  win.on('close', () => {
    if (timer) clearTimeout(timer)
    save()
  })
}
