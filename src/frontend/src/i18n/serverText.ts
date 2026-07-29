/**
 * Rendering for text the backend used to compose as English prose and now sends as a stable key
 * plus arguments. The catalog owns the wording, so a Danish UI reads Danish — including for runs
 * already persisted on disk, which re-render in whatever language is active.
 *
 * Runs recorded before the keys existed carry only the prose, so every call takes a fallback.
 */
export type ServerArgs = Record<string, unknown>
export type ServerRenderer = (args: ServerArgs) => string

export function serverText<K extends string>(
  map: Record<K, ServerRenderer>,
  key: string | null | undefined,
  args: ServerArgs | null | undefined,
  fallback: string,
): string {
  if (key && key in map) return map[key as K](args ?? {})
  return fallback
}

// Args cross a JSON boundary, so they arrive as `unknown`. These keep the casting in one place
// rather than scattering it through every catalog entry.
export const num = (args: ServerArgs, key: string): number =>
  typeof args[key] === 'number' ? (args[key] as number) : 0

export const str = (args: ServerArgs, key: string): string =>
  args[key] == null ? '' : String(args[key])

export const list = (args: ServerArgs, key: string): string[] =>
  Array.isArray(args[key]) ? (args[key] as unknown[]).map(String) : []
