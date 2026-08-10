import { useParams } from 'react-router-dom'
import { HistoryListView } from './history/HistoryListView'
import { RunDetailView } from './history/RunDetailView'

// One route serving two views: /history lists the runs, /history/:runId opens one.
export function HistoryPage() {
  const { runId } = useParams<{ runId: string }>()
  if (runId) return <RunDetailView runId={runId} />
  return <HistoryListView />
}
