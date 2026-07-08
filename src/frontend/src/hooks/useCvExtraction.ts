import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getCvExtractionStatus, startCvExtraction, type CvExtractionInput } from '../api/client'
import type { CvExtractionStatus } from '../api/types'

// Server-side CV extraction state. The run lives in a backend singleton, so this hook only
// polls: it picks an in-flight extraction back up after navigation or reload, exactly like
// the LLM model download banner.
export function useCvExtraction(enabled: boolean) {
  const queryClient = useQueryClient()

  const { data: status } = useQuery({
    queryKey: ['cv-extract'],
    queryFn: getCvExtractionStatus,
    enabled,
    refetchOnWindowFocus: false,
    refetchInterval: (query) => query.state.data?.state === 'extracting' ? 1000 : false,
  })

  async function start(input: CvExtractionInput): Promise<void> {
    const snapshot = await startCvExtraction(input)
    queryClient.setQueryData<CvExtractionStatus>(['cv-extract'], snapshot)
    await queryClient.invalidateQueries({ queryKey: ['cv-extract'] })
  }

  return { status, start }
}
