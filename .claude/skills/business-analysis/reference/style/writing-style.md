# Writing Style

These rules govern **how the prose reads**. They are the difference between documentation a human reads and documentation a human skims, sighs at, and closes.

## The three commandments

1. **Tables before prose.** If the information has structure, put it in a table.
2. **Short. Then shorter.** Aim for the same point in fewer words. If a sentence runs past 30 words, split it.
3. **Active voice, present tense.** "The system sends an email." Not "An email shall be sent by the system." Not "An email will eventually be sent."

## Voice and tense

| Use                                        | Don't use                                       |
| ------------------------------------------ | ----------------------------------------------- |
| "The system must …" / "The system shall …" | "We will build a feature that …"                |
| "A Manager initiates …"                    | "It would be initiated by the Manager"          |
| Present tense for behaviour                | Future tense ("will", "going to") for behaviour |
| Concrete role names from `personas.md`     | "The user", "the administrator", "someone"      |

## Sentence shape

| MUST | MUST NOT |
|------|----------|
| Lead each FR with "The system must …" | Lead with "There should be a way to …" |
| Front-load the subject | Open with a clause: "When and if conditions are met, the system…" |
| Reference IDs verbatim | Paraphrase ("the second requirement") |
| Bold key thresholds, role names, outcomes | Leave critical values floating in plain text |

## Tables-first reflex

Convert prose to a table whenever there are:

- 3+ items in a list with the same shape
- 2+ attributes per item
- Pairs of MUST/MUST NOT, IF/THEN, INPUT/OUTPUT

Two-column comparison tables (Good vs Bad, Use vs Don't use) are extremely effective — use them liberally.

## Word-count rules of thumb

| Artefact | Target | Hard cap |
|----------|--------|----------|
| FR row | 15–30 words | 50 |
| User story row | ≤ 25 words | 40 |
| BR bullet | ≤ 20 words | 30 |
| AC row | ≤ 15 words | 25 |
| Overview section | 2–4 sentences | 6 |

If you cannot meet the cap, split into two artefacts.

## AI tells to delete on sight

| Tell | Replacement |
|------|-------------|
| "Comprehensive", "robust", "seamless", "leverage" | Cut entirely or replace with a measurable fact |
| "In order to …" | "to …" |
| "It is important to note that …" | Delete the phrase, keep the noted fact |
| "Various", "several", "a number of" | A concrete number, or omit |
| "Ensure / facilitate / enable / support" without an object | A concrete verb on a concrete object |
| Multi-paragraph explanations of a one-row table | Delete the paragraphs |
| Section headers with no content underneath | Delete the section |
| Self-referential intros ("This document is…", "Single source of truth for…", "Defines X for the Y project") | Cut. Filename and folder location already convey purpose. |
| Author-facing rules embedded in artefact prose ("X is a code smell", "Don't do Y here") | Move to `docs/rules/…` if the rule deserves to exist; otherwise delete. |
| `Field \| Value` metadata tables at the top of an artefact ("Status: Draft", "Date: 2026-…", "Owner: …", "Related: …") | Cut. Git tracks dates and history; navigation belongs in the project README; "draft" status rots the moment it's wrong. |

## Layout rules

| MUST | MUST NOT |
|------|----------|
| Lead `## H2` directly with its table | Insert intro paragraphs above each table |
| Separate top-level sections with `---` | Use only blank lines |
| Code-fence ASCII diagrams (` ```text` or ` ``` `) | Render diagrams as prose lists |
| Use sentence-case headers | TITLE CASE / Random Capitalization |
| Keep H2/H3/H4 — don't go deeper | Use H5/H6 |

## Brevity test

Before committing a section, ask:

1. Can a reader find the answer to one question in under 5 seconds?
2. Can I delete a sentence without losing information?
3. Could a table replace this paragraph?

If any answer is yes → revise.

## Good vs bad

```markdown
✅
## Functional Requirements

| ID | Requirement |
|----|-------------|
| UC01-FR-1 | The system must allow a Manager to invite a new partner. |
| UC01-FR-2 | The system must send an invitation email containing a unique onboarding link. |

❌
## Functional Requirements

In order to provide a comprehensive invitation experience, the system facilitates the
process whereby Managers can initiate the onboarding of new partners by
leveraging the various capabilities exposed by the platform. It is important to note
that this process is designed to be seamless and robust …

✅
# System Personas

## Internal Personas

### User
The person the system serves — owner of the device, originator of every request.

❌
# System Personas

The single source of truth for every actor referenced anywhere in this project's business analysis. Defining a role outside this document is a code smell.

## Internal Personas
```

## Related

- [../README.md](../README.md)
- [../content/use-case-document.md](../content/use-case-document.md)
- [../content/functional-requirements.md](../content/functional-requirements.md)
