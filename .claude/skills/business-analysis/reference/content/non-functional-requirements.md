# Non-Functional Requirements

Non-functional requirements (NFRs) describe **qualities** of the system — security, performance, availability, maintainability — rather than features.

## Table format

```markdown
| ID  | Requirement |
|-----|-------------|
| NFR1 | **Security**: All communication must be encrypted (HTTPS/TLS). … |
| NFR2 | **Authentication**: Integration with Azure AD must ensure only authorized users can log in. … |
| NFR3 | **Performance**: Response times for common operations should typically be under 2 seconds. … |
```

Lead the requirement text with a bold **category** label, then the actual constraint. The label makes the table scannable.

## ID conventions

| Scope | Format | Lives in |
|-------|--------|----------|
| System-wide | `NFR-N` (or `NFR1`, `NFR2`, …) | `overview/requirements.md` |
| Use-case-specific | `NFR-UC0X-N` | inside `UC0X_requirements.md` |

## Standard categories

Use these labels first; only invent a new one when none fit.

| Category | Typical concerns |
|----------|------------------|
| **Security** | Encryption, secrets, link expiry, access control |
| **Authentication** | SSO, identity provider integration, session handling |
| **Authorization** | Permission model, scope, audit of permission changes |
| **Performance** | Response times, throughput, latency budgets |
| **Availability** | Uptime target, redundancy, degradation behaviour |
| **Scalability** | Growth dimensions and ceilings |
| **Integrations** | External API robustness, retry, graceful degradation |
| **Maintainability** | Modularity, documentation, extension points |
| **Auditability** | What must be logged, retention, attribution |
| **Compliance** | GDPR, regulatory, contractual obligations |

## Rules

| MUST | MUST NOT |
|------|----------|
| Phrase each as a constraint, not a feature | "The system should be fast" |
| Make it measurable wherever feasible | Use only adjectives — "high", "robust", "modern" |
| Pick one category per row | Cram security + performance into one |
| Keep system-wide NFRs in `requirements.md` | Duplicate the same NFR across every UC |
| Add a per-UC NFR only when the constraint is specific to that UC | Re-state global NFRs at UC level |

## Good vs bad

```
✅ NFR3 | **Performance**: Response times for common operations should typically be under 2 seconds.
         The system must scale to handle increasing partners and vessels.

❌ NFR3 | The system should feel snappy and modern.
```

## Related

- [functional-requirements.md](functional-requirements.md)
- [../structure/id-conventions.md](../structure/id-conventions.md)
