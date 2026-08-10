/** The shared JSON fetch. Throws on non-2xx so react-query surfaces the message. */
export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(path, options)
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`API error ${res.status}: ${text}`)
  }
  return res.json() as Promise<T>
}

/** POST/PUT with a JSON body — the shape most of the write endpoints share. */
export function jsonBody(method: 'POST' | 'PUT' | 'DELETE', body?: unknown): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  }
}
