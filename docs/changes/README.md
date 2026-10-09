# Change proposals

New work starts here. A change proposal is a short file, `docs/changes/<name>.md` (kebab-case), describing one change: a new feature, a behavior change or a fix that needs design. It exists while the change is being agreed and built; when the change lands, its content lives in the specs (`docs/specs/`) and the file is deleted. Git history keeps it.

Small fixes that need no design skip the proposal: take the item from [`docs/BACKLOG.md`](../BACKLOG.md) and update the affected spec with the fix.

## Format

```markdown
# <Title>

## Why

What problem it solves, for whom, and what prompted it (a backlog item, a bug, a request).

## What

The change, in a few paragraphs: the behavior after it lands, and what does not change.

## Decisions

Each choice the change makes: the decision, the reason, and the alternatives rejected. These move into the governing spec's Decisions section when the change lands.

## Spec changes

Per spec id: requirements added (draft wording, no ids yet), requirements changed (by id, new wording), requirements struck (by id, with the reason). A new feature names its new spec file.

## Tasks

The ordered work items, each with its tests. Checks in the running app that DevTools cannot drive are listed as manual tasks.

## Open questions

What must be settled before or during the work. Empty before implementation starts.
```

## Lifecycle

1. **Draft** the proposal; settle its open questions.
2. **Implement** it with the `implement-plan` skill (pass `docs/changes/<name>.md`), or by hand. Each task updates its spec as it goes: requirements, their `Tests:` lines, and the Decisions section.
3. **Close**: when the tests are green and the specs say what the code does, delete the proposal and remove the backlog lines it resolved. Anything left undone goes back into the backlog.
