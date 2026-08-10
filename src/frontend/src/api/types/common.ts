/** Response envelopes shared by several endpoints. */
export type SaveResponse = { success: boolean; error?: string }
export type CreateResponse = { success: boolean; id: number; error?: string }
