# File and Folder Naming

Names must be predictable enough that a reader can guess the path from the use case ID alone.

## Folders

| Element | Convention | Example |
|---------|------------|---------|
| Top folder | lowercase, kebab | `business-analysis/` |
| Section folder | lowercase, kebab | `overview/`, `use-cases/`, `domain/` |
| Use-case folder | `NN_kebab-case-topic` | `03_vessel-onboarding/` |
| Archive | leading underscore | `_archive/` |

`NN` is **two digits**, zero-padded, allocated in order of first introduction. Never renumber.

## Files

| File type | Convention | Example |
|-----------|------------|---------|
| Requirements (use case) | `UC0X_requirements.md` | `UC03_requirements.md` |
| Requirements (system) | `requirements.md` | one file under `overview/` |
| Sequence diagram | `UC0X_<TopicPascal>_SequenceDiagram.md` | `UC02_PoolMembership_SequenceDiagram.md` |
| State diagram | `UC0X_<TopicPascal>_StateDiagram.md` | `UC05_RevisionOfPoolAgreement_StateDiagram.md` |
| ER diagram | `UC0X_<TopicPascal>_ER_Diagram.md` | `UC04_Authorization_ER_Diagram.md` |
| Side-topic note | `UC0X_<TopicPascal>.md` | `UC03_AccessionLetter_Customization.md` |
| Index / TOC | `index.md` | one per section folder |
| Personas | `personas.md` | one, under `overview/` |
| Domain concept | `snake_case.md` | `pool_and_vessel.md` |

## Rules

| MUST | MUST NOT |
|------|----------|
| Match the use-case folder number to the file IDs inside | Have `UC04` files inside `03_*/` |
| Use `PascalCase` for topic segments inside filenames | Use spaces or hyphens inside the topic segment |
| Keep one artefact type per file | Pack a sequence + state diagram into one file |
| Keep the `.md` companion alongside any `.png` export | Ship `.png` without a `.md` source |

## Examples

```
use-cases/03_vessel-onboarding/
├── UC03_requirements.md
├── UC03_VesselOnboarding_SequenceDiagram.md
├── UC03_VesselOnboarding_SequenceDiagram.png
├── UC03_VesselOnboardingApplication_SequenceDiagram.md
└── UC03_AccessionLetter_Customization.md
```

## Related

- [directory-layout.md](directory-layout.md)
- [id-conventions.md](id-conventions.md)
