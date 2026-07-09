# Directory Layout

The standard tree under a project's `documentation/business-analysis/` folder.

## Layout

```
business-analysis/
├── overview/
│   ├── index.md                    # Table-of-contents for /overview
│   ├── requirements.md             # System-wide FRs + NFRs + traceability summary
│   ├── personas.md                 # All actors, roles, contextual permissions
│   ├── domain_model.png            # Optional: exported domain model
│   └── system_diagram.png          # Optional: exported architecture diagram
├── domain/
│   ├── index.md                    # TOC for /domain
│   └── <concept>.md                # One file per major domain concept
├── use-cases/
│   ├── index.md                    # TOC linking every UC folder
│   ├── 01_<slug>/
│   │   ├── UC01_requirements.md
│   │   ├── UC01_<Topic>_SequenceDiagram.md
│   │   └── UC01_<Topic>_SequenceDiagram.png   # optional render
│   ├── 02_<slug>/
│   │   └── …
│   └── NN_<slug>/
├── diagrams.md                     # Master index of every diagram in the docs
└── _archive/                       # Superseded docs; never delete, never link from index
```

## Per-use-case folder

| File | Required | Purpose |
|------|----------|---------|
| `UC0X_requirements.md` | Yes | Single source of truth for the use case |
| `UC0X_<Topic>_SequenceDiagram.md` | One per workflow | Mermaid sequence diagram |
| `UC0X_<Topic>_StateDiagram.md` | If lifecycle is non-trivial | Mermaid or ASCII state diagram |
| `UC0X_<Topic>_ER_Diagram.md` | If the UC introduces data | Mermaid `erDiagram` |
| `*.png` | Optional | Pre-rendered export of the mermaid file |
| Extra topic notes (`UC03_AccessionLetter_Customization.md`) | Optional | Side-topics that don't belong in the main requirements file |

## Rules

| MUST | MUST NOT |
|------|----------|
| Keep folders flat — no nesting beyond `NN_slug/` | Group use cases by team or quarter |
| Number use-case folders sequentially (`01_`, `02_` …) | Skip or re-use numbers |
| Keep one diagram file per diagram | Put multiple diagrams in one `.md` |
| Move superseded docs into `_archive/` | Delete old documents |
| Reference diagrams from `diagrams.md` | Hide diagrams in random folders |

## Why this layout

- **Overview** answers "what is the system?" in one place.
- **Domain** answers "what nouns do we share?" without coupling them to a use case.
- **Use cases** answer "what does the system do?" — each one self-contained.
- **`_archive/`** preserves history without polluting the active tree.

## Related

- [file-naming.md](file-naming.md)
- [id-conventions.md](id-conventions.md)
- [../content/use-case-document.md](../content/use-case-document.md)
