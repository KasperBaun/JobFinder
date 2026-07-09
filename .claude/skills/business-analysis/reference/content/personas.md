# Personas

Personas live in **one place** — `overview/personas.md` — and are referenced by every use case. Adding a new role anywhere else is a code smell.

## Document structure

```markdown
# System Personas

## Internal Personas (<Client / Org Name>)

### <Role Name>
<One-sentence description.>

| Attribute | Description |
|-----------|-------------|
| **Organization** | <Org> |
| **Access Level** | <one line> |

#### Capabilities
- <verb-led bullet> (UC0X)
- …

#### Restrictions
- <only when meaningful>

#### Use Case Involvement
| Use Case | Role |
|----------|------|
| UC0X - <Title> | Initiator / Approver / Viewer / … |

---

## External Personas

### <Role Name>
…

---

## Contextual Permissions

### <Permission Name>
<Short note.>

| Attribute | Description |
|-----------|-------------|
| **Context** | UC0X - <Title> |
| **Granted To** | <who> |

#### Enables
- <bullet>

#### Constraints
- <bullet>

---

## System Actors

### System
<What automated behaviour does.>

#### Capabilities
- <verb-led bullet>

---

## Role Hierarchy Overview

```text
<Org> (Internal)
├── <Role>
│   └── <one-line summary>
└── <Role>

External
├── <Role>
└── <Role>
```

## Permission Scopes

| Scope | Description | Example |
|-------|-------------|---------|
| **Global** | System-wide access | … |
| **Org / Tenant** | Scoped to a tenant | … |
| **Resource** | Limited to specific items | … |
```

## Required sub-sections per persona

| Section | Required | Notes |
|---------|----------|-------|
| One-line intro | Yes | Above the attributes table |
| Attributes table (`Organization`, `Access Level`) | Yes | Two rows minimum |
| Capabilities | Yes | Verb-led bullets, each ending with `(UC0X)` reference |
| Restrictions | Optional | Only when meaningful boundaries exist |
| Use Case Involvement | Yes if persona appears in ≥ 1 UC | Maps UC ID to role label |

## Rules

| MUST | MUST NOT |
|------|----------|
| Define each role once, in `overview/personas.md` | Redefine the role inside a UC document |
| Reference roles by their exact name elsewhere | Use synonyms ("Manager" vs "Pool Manager") |
| Split **Personas** (people) from **Contextual Permissions** (assignable abilities) | Mix them under one header |
| Add a `Use Case Involvement` table per persona | Force readers to grep across UCs |
| Use verb-led capability bullets | Use noun phrases ("Access to pools") |

## Persona categories

| Category | Description |
|----------|-------------|
| **Internal personas** | Employees of the client / operator |
| **External personas** | Customer/partner users |
| **Contextual permissions** | Capabilities (Voter, Signee) granted to people but not themselves a persona |
| **Other users** | Third parties not affiliated with internal or external orgs |
| **System actors** | Automation — usually one entry, `System` |

## Related

- [user-stories.md](user-stories.md)
- [../structure/id-conventions.md](../structure/id-conventions.md)
