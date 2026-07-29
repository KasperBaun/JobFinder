export const common = {
  /** Placeholder for a missing value — same glyph in both locales, kept here so it is never inlined. */
  emDash: '—',
  justNow: 'just now',
  /** SI abbreviations; identical in Danish, so only the decimal separator differs. */
  units: { ms: 'ms', s: 's', m: 'm' },

  save: 'Save',
  saving: 'Saving…',
  cancel: 'Cancel',
  close: 'Close',
  continue: 'Continue',
  back: 'Back',
  retry: 'Retry',
  remove: 'Remove',
  delete: 'Delete',
  loading: 'Loading…',
  none: 'None',
  unknown: 'Unknown',
  enabled: 'Enabled',
  disabled: 'Disabled',

  unsavedChanges: 'Unsaved changes',
  revert: 'Revert',
  saveChanges: 'Save changes',
  typeAndPressEnter: 'Type and press Enter',
  removeValue: (value: string) => `Remove ${value}`,

  serverDisconnectedTitle: 'Server disconnected',
  serverDisconnectedBody: 'The jobfinder app is no longer running. You can close this tab.',
  goodbyeTitle: 'Goodbye',
  goodbyeBody: 'The jobfinder app has stopped. You can close this tab.',
}
