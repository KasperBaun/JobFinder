# Recruit IT (recruit-it.com)

**Prior:** Specialized IT recruitment agency. Small list (tens of roles). No public API expected.

**Check:** HTML structure of `recruit-it.com/jobs` (or wherever the listings page is). Selector candidates for `html` adapter (`list_selector`, `title_selector`, `link_selector`, `company_selector`, `location_selector`, `description_selector`, `url_attribute`). Low absolute volume vs. ATS feeds; only worth wiring if selectors are stable. If the page is JS-rendered, the existing Playwright-backed `HtmlAdapter` handles it.

---

**Findings:** `recruit-it.com` 301-redirects to `recruit-it.dk` (the live property). Listings live at `https://recruit-it.dk/ledige-stillinger-danmark` — server-rendered WordPress (no JS gating; selectors stable across the page). Each row has the shape:

```html
<a href="https://recruit-it.dk/ledige-stillinger/<id>/">
  <div class="ledige-stillinger-box">
    <div class="row">
      <div class="col-md-6">
        <strong class="ls-title">Programleder til IT29 ...</strong>
        <i class="icon icon-location2"></i> Frederiksberg
      </div>
      <div class="col-md-6 ls-description"><p>...</p></div>
      <div class="ledige-stillinger-alle-hidden" style="display:none">Program Management</div>
    </div>
  </div>
</a>
```

Title (`.ls-title`) and description (`.ls-description`) selectors are clean. The link is the *parent* `<a>` wrapping each card — Playwright's `:scope` works inside `QuerySelectorAsync` so we can target the wrapping anchor as the list element and use `:scope` as `link_selector` to pull `href` from the card itself. **Company** is not surfaced as a separate field (Recruit IT is the agency; the actual hiring company is embedded in titles like "IT-chef til 3F", "Programleder til IT29"). **Location** sits as a plain text node next to an `<i class="icon-location2">` icon with no dedicated wrapper class, so a CSS-only selector cannot extract it cleanly — leave `location_selector` unset and accept null locations. Volume on the page is ~tens of active roles. ~25 listings observed. Selectors are stable WordPress theme classes (`ledige-stillinger-*` is bespoke to this site).

**Verdict:** `html` — small but viable scrape; selectors are server-rendered and stable. Worth wiring if Danish IT-agency roles matter; otherwise low ROI vs. ATS feeds already covered.

**Stub block:**

```yaml
- name: recruit-it
  type: html
  enabled: false
  endpoint: https://recruit-it.dk/ledige-stillinger-danmark
  html:
    list_selector: "a:has(> div.ledige-stillinger-box)"
    title_selector: ".ls-title"
    link_selector: ":scope"
    description_selector: ".ls-description"
    url_attribute: "href"
  static_fields:
    company: "Recruit IT (agency)"
  notes: |
    Small Danish IT recruitment agency (~tens of roles). The hiring company
    is embedded in the title (e.g. "IT-chef til 3F") rather than a separate
    field, so the static_fields.company stamps the agency name and downstream
    parsing/ranking should treat the title as authoritative for company.
    Location is rendered as a plain text node next to an icon with no
    wrapper class, so location_selector is intentionally omitted and
    listings will have null location until the markup changes. Requires
    Playwright; install with `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`.
```
