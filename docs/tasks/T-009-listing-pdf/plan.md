# T-009 — Save a listing as PDF (R-106)

Tester feedback: their application process archives each ad as a PDF; they want to
print-to-PDF / download the ad directly from JobFinder.

## Design decisions

- **The PDF contains the full ad text.** The fetched body (`Listing.Description`,
  enriched by `BaseAdapter.Enrichment.cs`) is today persisted only in
  `all-listings.json` / `ranked-listings.json` (overwritten every run) — not in run
  history. Fix: persist it per run on shortlist entries going forward (additive field).
  Old runs fall back to the card content + source link.
- **Shortlist only.** `ScoredEntry` (longlist) and `RawListing` stay slim by design.
- **Both shells work.** Electron gets a native save-as-PDF dialog via a new IPC channel;
  the browser shell falls back to `window.print()` (system dialog offers save-as-PDF).
  `window.jobfinderDesktop` presence is the documented desktop probe.

## Implementation steps

### Backend

1. `src/backend/Jobmatch/Search/ListingMatch.cs` — new optional trailing field
   `string? Description = null`. Old history files still deserialize (optional +
   trailing; persisted shapes are additive-only per CLAUDE.md).
2. `src/backend/Jobmatch/Search/SearchService.Mapping.cs` `ToListingMatch` — copy
   `Listing.Description` in.
3. Tests: mapping copies the description; a history JSON without the field still
   round-trips (back-compat).

### Frontend

4. `src/frontend/src/api/types.ts` — `description?: string` on `ListingMatch`.
5. New `src/frontend/src/components/PrintListingButton.tsx` (+
   `src/frontend/src/hooks/usePrintListing.ts` if the logic outgrows the component) —
   button in the `ListingCard.tsx` footer next to "Open job posting →" (covers
   SearchPage results + HistoryPage ShortlistTab automatically). Renders a
   print-portal DOM (React portal into `document.body`): title, company · location,
   portal display name, posted date (via `i18n/format.ts` formatters), source URL, and
   the full ad text; when `description` is absent (old runs) → card content + link and
   the ad text is simply missing.
6. New `src/frontend/src/css/print.css`, imported from `main.tsx` — the repo has no
   print CSS today. `@media print`: hide the app root chrome (nav, buttons, footers),
   show only the print portal; sensible page margins; avoid `components.css` (already
   3078 lines).
7. Electron enhancement — `src/desktop/src/preload.ts` exposes
   `printToPdf(suggestedFileName: string)`; `src/desktop/src/main.ts` adds
   `ipcMain.handle('jobfinder:printToPdf', …)` → `dialog.showSaveDialog` (default
   `{company} - {title}.pdf`, sanitized) + `win.webContents.printToPDF({...})` +
   write the buffer. The frontend calls `window.jobfinderDesktop?.printToPdf` when
   present, else `window.print()`. Electron ^33 supports `printToPDF` returning a
   Buffer. Keep the existing `quit` channel pattern.
   Note: `printToPDF` captures the page with print CSS applied, so the same
   `@media print` rules drive both paths.
8. i18n — `i18n/en/listing.ts` + `i18n/da/listing.ts` in the same change:
   `savePdf` label, tooltip, and a failure toast/text if the desktop save errors.
   No user-facing literals in components (incl. aria-label/title).

### Docs

9. R-106 in `docs/requirements.md`; one CHANGELOG line; drop the T-009 entry from
   todo.md.

Draft R-106:

> **R-106** The system should let a user save any shortlisted listing as a PDF — a
> print-friendly rendering of the listing's title, company, location, portal, posting
> date, source URL and the full fetched ad text — via a native save dialog in the
> desktop shell and the system print dialog in the browser shell. The ad text persists
> per run in history from this change forward (additive field); runs recorded earlier
> fall back to the summary content without the ad body.

## Tests

- C#: `ToListingMatch` maps `Description`; old-history deserialization.
- Vitest: PrintListingButton renders the portal content (with and without
  `description`); desktop probe fallback logic; i18n catalog parity is enforced by
  existing tests.
- Manual/`verify`-skill: print dialog opens from a shortlist card and the preview
  contains the ad text; Electron smoke test of the IPC save path.

## Out of scope

Longlist rows; PDF generation server-side (PdfPig is read-only CV tooling); batch
export of multiple listings.
