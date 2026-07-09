import { describe, expect, it } from 'vitest'
import { platformHost, platformLabel } from './platform'

describe('platformLabel', () => {
  it('maps known ATS hosts to friendly labels', () => {
    expect(platformLabel('https://api.ashbyhq.com/posting-api/job-board/pleo')).toBe('Ashby')
    expect(platformLabel('https://boards-api.greenhouse.io/v1/boards/trustpilot/jobs')).toBe('Greenhouse')
    expect(platformLabel('https://api.smartrecruiters.com/v1/companies/x/postings')).toBe('SmartRecruiters')
    expect(platformLabel('https://lego.wd3.myworkdayjobs.com/wday/cxs/lego/jobs')).toBe('Workday')
    expect(platformLabel('https://danske-spil.teamtailor.com/sitemap.xml')).toBe('Teamtailor')
    expect(platformLabel('https://change.hr-manager.net/')).toBe('HR Manager')
    expect(platformLabel('https://www.jobindex.dk/jobsoegning.rss')).toBe('Jobindex')
    expect(platformLabel('https://jobs.lever.co/h1')).toBe('Lever')
  })

  it('falls back to the bare host for unknown platforms', () => {
    expect(platformLabel('https://careers.example.com/api/jobs')).toBe('careers.example.com')
  })

  it('strips a www prefix', () => {
    expect(platformHost('https://www.jobindex.dk/x')).toBe('jobindex.dk')
  })

  it('returns empty string for missing or invalid endpoints', () => {
    expect(platformLabel(undefined)).toBe('')
    expect(platformLabel('not a url')).toBe('')
    expect(platformHost(undefined)).toBe('')
  })
})
