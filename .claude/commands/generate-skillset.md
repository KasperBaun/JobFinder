---
description: Author or refresh the active skillset profile (config/skillset.md).
argument-hint: "[cv-file-or-url]"
---

Author or refresh the jobmatch skillset that drives ranking. The active skillset lives at `config/skillset.md` and is gitignored. An example lives at `config/skillset.example.md`.

## What to do

1. If the user passed a CV path or URL as an argument, mention you will load it as reference material for the prompts. If they didn't, proceed straight to the prompts.
2. Run the subcommand below. The CLI will walk the user through every field (name, location, experience, target roles, remote preference, seniority, primary stack, secondary stack, domains, disqualifiers, languages, employment types) and show a preview before writing.
3. If `config/skillset.md` already exists, the CLI will show a diff and ask before overwriting. Respect the user's choice.
4. After the command exits, summarise what changed (new file, or fields that differ from the previous version) and suggest the user run `/verify-config` next.

## Command

From the repo root:

```
dotnet run --project src/Jobmatch.Cli -- skillset $ARGUMENTS
```

The `$ARGUMENTS` are optional. If the user passes a path or URL, the CLI loads it and shows the first chunk as reference while the prompts run.
