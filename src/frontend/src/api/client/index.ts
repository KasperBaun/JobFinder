// Barrel for the API client, so call sites (and the vi.mock('../api/client') in tests) keep
// addressing one module. Split by endpoint family; the shared fetch lives in ./http.
export * from './history'
export * from './llm'
export * from './providers'
export * from './search'
export * from './skillset'
export * from './system'
