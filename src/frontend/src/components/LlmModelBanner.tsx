import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getLlmStatus, downloadLlmModel } from '../api/client'

function formatBytes(n: number | null): string {
  if (n === null || n === undefined) return '?'
  const gb = n / (1024 * 1024 * 1024)
  if (gb >= 1) return gb.toFixed(2) + ' GB'
  const mb = n / (1024 * 1024)
  return mb.toFixed(1) + ' MB'
}

// Surfaced on the Search page when the LLM is enabled but the model file isn't on
// disk yet. Single click triggers an SSE-streaming download from Hugging Face;
// progress shows in a thin bar; on complete, query cache invalidates so the
// banner disappears on next render.
export function LlmModelBanner() {
  const queryClient = useQueryClient()
  const { data: status, isLoading } = useQuery({
    queryKey: ['llm-status'],
    queryFn: getLlmStatus,
    refetchOnWindowFocus: false,
  })
  const [progress, setProgress] = useState<{ downloaded: number; total: number | null } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  useEffect(() => {
    if (done) {
      queryClient.invalidateQueries({ queryKey: ['llm-status'] })
    }
  }, [done, queryClient])

  if (isLoading || !status) return null
  if (!status.enabled) return null
  if (status.modelPresent && !done) return null

  async function startDownload() {
    setError(null)
    setProgress({ downloaded: 0, total: null })
    try {
      for await (const evt of downloadLlmModel()) {
        if (evt.type === 'progress') {
          setProgress({ downloaded: evt.downloadedBytes, total: evt.totalBytes })
        } else if (evt.type === 'complete') {
          setDone(true)
          setProgress({ downloaded: evt.bytes, total: evt.bytes })
        } else if (evt.type === 'error') {
          setError(evt.message)
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  const pct = progress?.total
    ? Math.min(100, Math.round((progress.downloaded / progress.total) * 100))
    : null
  const downloading = progress !== null && !done && error === null

  return (
    <aside
      style={{
        border: '1px solid #1d3557',
        borderRadius: 6,
        padding: '14px 18px',
        margin: '12px 0 24px 0',
        background: '#fafafa',
        color: '#1d3557',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 16 }}>
        <div>
          <strong>AI review is enabled, but the local model hasn't been downloaded yet.</strong>
          <div style={{ fontSize: 13, marginTop: 4 }}>
            Engine <code>{status.provider}</code> expects {' '}
            <code style={{ wordBreak: 'break-all' }}>{status.modelPath}</code>.
          </div>
        </div>
        {!downloading && !done && (
          <button
            onClick={startDownload}
            style={{
              padding: '8px 16px',
              background: '#1d3557',
              color: 'white',
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              whiteSpace: 'nowrap',
            }}
          >
            Download model (~2.3 GB)
          </button>
        )}
      </div>

      {downloading && (
        <div style={{ marginTop: 12 }}>
          <div style={{ fontSize: 13 }}>
            Downloading {formatBytes(progress!.downloaded)}{' '}
            {progress!.total ? `of ${formatBytes(progress!.total)}` : ''}
            {pct !== null ? ` (${pct}%)` : ''}
          </div>
          <div style={{ height: 4, background: '#e0e0e0', borderRadius: 2, marginTop: 6, overflow: 'hidden' }}>
            <div
              style={{
                height: '100%',
                width: pct !== null ? `${pct}%` : '8%',
                background: '#1d3557',
                transition: 'width 0.3s ease',
              }}
            />
          </div>
        </div>
      )}

      {done && (
        <div style={{ marginTop: 12, fontSize: 13, color: '#0a7d3a' }}>
          ✓ Model downloaded ({formatBytes(progress!.downloaded)}). AI review will run on the next search.
        </div>
      )}

      {error && (
        <div style={{ marginTop: 12, fontSize: 13, color: '#a00' }}>
          Download failed: {error}
        </div>
      )}
    </aside>
  )
}
