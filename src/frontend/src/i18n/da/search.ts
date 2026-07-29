import type { Messages } from '../en'

export const search: Messages['search'] = {
  phase: {
    pending: 'I kø',
    fetching: 'Henter opslag',
    deduping: 'Fjerner dubletter',
    ranking: 'Vurderer job',
    llmJudging: 'AI-gennemgang',
    writing: 'Gør færdig',
    done: 'Færdig',
  },

  state: {
    queued: 'I kø',
    running: 'Kører',
    succeeded: 'Fuldført',
    failed: 'Mislykkedes',
    cancelled: 'Annulleret',
    interrupted: 'Afbrudt',
  },
}
