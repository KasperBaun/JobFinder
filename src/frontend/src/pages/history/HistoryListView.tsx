import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { deleteHistoryRuns, getHistory } from '../../api/client'
import { Toast } from '../../components/Toast'
import { formatAbsolute, formatRelative } from '../../utils/time'
import { dec, n, useT } from '../../i18n'
import { isTerminalState } from '../../api/types'
import { StateBadge } from './StateBadge'

export function HistoryListView() {
  const t = useT('history')
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({
    queryKey: ['history'],
    queryFn: getHistory,
    // While a run is queued/running, poll so its row (state, counts) updates live.
    refetchInterval: query => {
      const runs = query.state.data?.runs
      const anyActive = runs?.some(r => r.state !== undefined && !isTerminalState(r.state))
      return anyActive ? 2000 : false
    },
  })

  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const headerCheckboxRef = useRef<HTMLInputElement>(null)

  const visibleIds = useMemo(() => data?.runs.map(r => r.runId) ?? [], [data])
  const allSelected = visibleIds.length > 0 && visibleIds.every(id => selected.has(id))
  const someSelected = !allSelected && visibleIds.some(id => selected.has(id))

  useEffect(() => {
    if (headerCheckboxRef.current) {
      headerCheckboxRef.current.indeterminate = someSelected
    }
  }, [someSelected])

  useEffect(() => {
    if (!data) return
    setSelected(prev => {
      const valid = new Set(visibleIds)
      let changed = false
      const next = new Set<string>()
      for (const id of prev) {
        if (valid.has(id)) next.add(id)
        else changed = true
      }
      return changed ? next : prev
    })
  }, [data, visibleIds])

  const deleteMutation = useMutation({
    mutationFn: (runIds: string[]) => deleteHistoryRuns(runIds),
    onSuccess: (res) => {
      if (res.error) {
        setToast({ kind: 'err', message: res.error })
        return
      }
      setSelected(new Set())
      void queryClient.invalidateQueries({ queryKey: ['history'] })
      void queryClient.invalidateQueries({ queryKey: ['applications'] })
      setToast({ kind: 'ok', message: t.deleted(res.deleted, res.missing.length) })
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  function toggleRow(id: string) {
    setSelected(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function toggleAll() {
    if (allSelected) setSelected(new Set())
    else setSelected(new Set(visibleIds))
  }

  function onDeleteClick() {
    if (selected.size === 0) return
    if (!window.confirm(t.deleteConfirm(selected.size))) return
    deleteMutation.mutate(Array.from(selected))
  }

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="page__heading">{t.heading()}</h1>
        <p className="page__lede">{t.lede}</p>
      </header>

      {isLoading && <div className="muted">{t.loading}</div>}
      {error && <div className="error-text">{t.loadFailed}</div>}

      {data && data.runs.length === 0 && (
        <div className="hint-card">{t.noneYet} <Link to="/search">{t.noneYetLink}</Link>{t.noneYetSuffix}</div>
      )}

      {data && data.runs.length > 0 && (
        <>
          {selected.size > 0 && (
            <div className="selection-bar" role="region" aria-label={t.selectionAria}>
              <span className="selection-bar__count">{t.selectedCount(selected.size)}</span>
              <button
                type="button"
                className="btn btn--sm"
                onClick={() => setSelected(new Set())}
                disabled={deleteMutation.isPending}
              >
                {t.clearSelection}
              </button>
              <button
                type="button"
                className="btn btn--sm btn--danger"
                onClick={onDeleteClick}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? t.deleting : t.deleteSelected}
              </button>
            </div>
          )}

          <div className="table-wrap">
            <table className="table table--clickable">
              <thead>
                <tr>
                  <th className="table__select-cell">
                    <input
                      ref={headerCheckboxRef}
                      type="checkbox"
                      aria-label={allSelected ? t.deselectAll : t.selectAll}
                      checked={allSelected}
                      onChange={toggleAll}
                    />
                  </th>
                  <th>{t.colWhen}</th>
                  <th>{t.colStatus}</th>
                  <th>{t.colSources}</th>
                  <th>{t.colFetched}</th>
                  <th>{t.colTopJobs}</th>
                  <th>{t.colBestRating}</th>
                  <th>{t.colGoodMatches}</th>
                </tr>
              </thead>
              <tbody>
                {data.runs.map(run => {
                  const ok = run.providers.filter(p => p.status === 'ok').length
                  const failed = run.providers.filter(p => p.status === 'failed').length
                  const ratio = run.shortlistCount > 0 ? run.goodMarks / run.shortlistCount : 0
                  const isSelected = selected.has(run.runId)
                  return (
                    <tr
                      key={run.runId}
                      className={isSelected ? 'table__row--selected' : undefined}
                      onClick={() => navigate(`/history/${run.runId}`)}
                    >
                      <td
                        className="table__select-cell"
                        onClick={e => { e.stopPropagation(); toggleRow(run.runId) }}
                      >
                        <input
                          type="checkbox"
                          aria-label={t.selectRow(formatAbsolute(run.startedAt))}
                          checked={isSelected}
                          onChange={() => toggleRow(run.runId)}
                          onClick={e => e.stopPropagation()}
                        />
                      </td>
                      <td title={formatAbsolute(run.startedAt)}>
                        <Link to={`/history/${run.runId}`} onClick={e => e.stopPropagation()}>
                          {formatRelative(run.startedAt)}
                        </Link>
                      </td>
                      <td><StateBadge state={run.state} /></td>
                      <td className="tabular">
                        <span style={{ color: 'var(--c-good)' }}>{ok}</span>
                        <span className="subtle"> / </span>
                        <span style={{ color: failed ? 'var(--c-bad)' : 'var(--c-text-subtle)' }}>{failed}</span>
                      </td>
                      <td className="tabular">{n(run.fetchedCount)}</td>
                      <td className="tabular">{n(run.shortlistCount)}</td>
                      <td className="tabular mono">{dec(run.topScore, 2)}</td>
                      <td>
                        <div className="marks-cell">
                          <span>{n(run.goodMarks)} / {n(run.shortlistCount)}</span>
                          <div className="progress-bar" aria-hidden="true">
                            <div
                              className="progress-bar__fill"
                              style={{ width: `${Math.round(ratio * 100)}%` }}
                            />
                          </div>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}
