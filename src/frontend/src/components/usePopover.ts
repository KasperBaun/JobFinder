import { useEffect, useId, useRef } from 'react'

/**
 * Shared popover behaviour for a trigger + floating panel pair: Escape and outside-press
 * dismiss, focus moves into the panel on open and back to the trigger on close, and a
 * measured edge-flip right-aligns a panel that would run off screen. Markup stays with the
 * caller; the panel div must carry `panelId` and the .filter-pop__panel positioning class.
 */
export function usePopover(open: boolean, onOpenChange: (open: boolean) => void) {
  const panelId = useId()
  const wrapRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return
      onOpenChange(false)
      triggerRef.current?.focus()
    }
    // pointerdown rather than click: a press outside should dismiss before the press can act on
    // whatever is underneath.
    const onPointerDown = (e: PointerEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) onOpenChange(false)
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open, onOpenChange])

  useEffect(() => {
    const el = panelRef.current
    if (!open || !el) return
    // Right-align when opening leftward would run off screen. Measured, not assumed: the bar wraps,
    // so which trigger sits closest to the edge changes with the window. Written straight to the
    // class list rather than through state — this is a layout read, and it must not cause a render.
    el.classList.remove('filter-pop__panel--end')
    if (el.getBoundingClientRect().right > window.innerWidth - 8) {
      el.classList.add('filter-pop__panel--end')
    }
    // Move focus in, so the panel is operable the moment it opens.
    el.querySelector<HTMLElement>('input, button, select')?.focus()
  }, [open])

  return { panelId, wrapRef, triggerRef, panelRef }
}
