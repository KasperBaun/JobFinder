import type { SkillsetResponse, SkillsetUpdateRequest } from '../api/types'

export type Form = SkillsetUpdateRequest

export const EMPTY_FORM: Form = {
  name: '', location: '', experienceYears: 0, targetRoles: [],
  remotePreference: 'any', seniority: 'mid', primaryStack: [], secondaryStack: [],
  domains: [], disqualifiers: [], languages: [], employmentTypes: [],
  country: '', region: '', metro: [], preferredCompanies: [],
  address: '', radiusKm: 0,
}

export function toForm(s: SkillsetResponse): Form {
  return {
    name: s.name,
    location: s.location,
    experienceYears: s.experienceYears,
    targetRoles: [...s.targetRoles],
    remotePreference: s.remotePreference,
    seniority: s.seniority,
    primaryStack: [...s.primaryStack],
    secondaryStack: [...s.secondaryStack],
    domains: [...s.domains],
    disqualifiers: [...s.disqualifiers],
    languages: [...s.languages],
    employmentTypes: [...s.employmentTypes],
    country: s.country ?? '',
    region: s.region ?? '',
    metro: [...s.metro],
    preferredCompanies: [...s.preferredCompanies],
    address: s.address ?? '',
    radiusKm: s.radiusKm ?? 0,
  }
}

export function isDirty(a: Form, b: Form): boolean {
  return JSON.stringify(a) !== JSON.stringify(b)
}
