// Maps a source's endpoint host to the ATS / job-board platform it actually comes from, so the
// provider page can say "Ashby" / "Greenhouse" instead of leaving the origin implicit. Matched by
// host substring/suffix; falls back to the bare host (or '' for a missing/invalid endpoint).
const HOST_RULES: Array<{ match: (host: string) => boolean; label: string }> = [
  { match: (h) => h.includes('ashbyhq.com'), label: 'Ashby' },
  { match: (h) => h.includes('greenhouse.io'), label: 'Greenhouse' },
  { match: (h) => h.includes('myworkday'), label: 'Workday' },
  { match: (h) => h.includes('smartrecruiters.com'), label: 'SmartRecruiters' },
  { match: (h) => h.endsWith('teamtailor.com'), label: 'Teamtailor' },
  { match: (h) => h.endsWith('hr-manager.net'), label: 'HR Manager' },
  { match: (h) => h.includes('jobindex.dk'), label: 'Jobindex' },
  { match: (h) => h.includes('it-jobbank.dk'), label: 'IT-Jobbank' },
  { match: (h) => h.includes('lever.co'), label: 'Lever' },
  { match: (h) => h.includes('bamboohr.com'), label: 'BambooHR' },
  { match: (h) => h.includes('oraclecloud.com'), label: 'Oracle' },
  { match: (h) => h.includes('careerjet'), label: 'Careerjet' },
  { match: (h) => h.includes('jooble'), label: 'Jooble' },
  { match: (h) => h.includes('remotive'), label: 'Remotive' },
  { match: (h) => h.includes('thehub.io'), label: 'The Hub' },
]

export function platformHost(endpoint?: string): string {
  if (!endpoint) return ''
  try {
    return new URL(endpoint).host.replace(/^www\./, '')
  } catch {
    return ''
  }
}

export function platformLabel(endpoint?: string): string {
  const host = platformHost(endpoint)
  if (!host) return ''
  const rule = HOST_RULES.find((r) => r.match(host))
  return rule ? rule.label : host
}
