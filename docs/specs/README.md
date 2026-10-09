# Feature specs

One spec per feature, describing how it behaves **now**. The specs are the source of truth for behavior: where a change proposal, a commit message or an old plan disagrees with a spec, the spec wins. Specs are written for AI agents first. They are terse, numbered and checkable, so an agent can read the spec instead of reconstructing a feature from its code, tests and history.

## Folders

One folder per layer, so a spec never mixes the library with the UI:

| Folder | Projects | Id prefix |
|---|---|---|
| [`lib/`](lib/index.md) | `ImageMagitek`, `ImageMagitek.Services` | `LIB-` |
| [`ui/`](ui/index.md) | `TileShop.UI`, `TileShop.Shared` | `UI-` |
| [`cli/`](cli/index.md) | `TileShop.CLI` | `CLI-` |

`LIB-` specs depend only on other `LIB-` specs and never mention ViewModels, views or Avalonia. A UI or CLI spec builds on library specs by id (`depends`) instead of restating them.

## Using the specs

- **Before changing behavior**, read the feature's spec (find it in the folder's `index.md`, or by a type in its `types` list) and the specs in its `depends`.
- **With the change**, update the spec in the same commit: change or add requirements, update their `Tests:` lines, and strike requirements that no longer hold. A change whose behavior diverges from its spec without updating it is incomplete.
- **New work starts as a change proposal** in [`docs/changes/`](../changes/README.md); a new feature also gets a draft spec (`status: draft` in the front matter), requirements before code. The draft becomes current, and the proposal is deleted, when the change lands.
- **When code and spec disagree** and the spec was not changed on purpose, one of them is a bug. Decide which, and log it in the spec's Open items and in [`docs/BACKLOG.md`](../BACKLOG.md) until it is fixed.

## Format

Copy [`_template.md`](_template.md). The format is not yet enforced by a test.

**Front matter** (between `---` lines; a scalar is `key: value`, a list is `key:` followed by `  - item` lines, or `key: []` when empty):

| Key | Content |
|---|---|
| `id` | `LIB-PALETTES`, `UI-PALETTE-EDITOR`: the prefix for its folder, then an upper-case name. Unique. |
| `title` | The feature's name. |
| `status` | Optional. `draft` for a spec written ahead of its feature; omitted when current. |
| `project` | The project(s) the sources live in. |
| `sources` | Files or folders, relative to the repo root. Each must exist. |
| `types` | The main types this spec owns. A type is owned by one spec; others refer to it by spec id. |
| `tests` | Test classes that cover the feature, or `[]`. |
| `depends` | Ids of specs this one builds on. |

**Sections**, as `##` headings in this order: Purpose, Requirements, Invariants, Edge cases, Threading and lifetime, Decisions, Non-goals, Open items. A section that does not apply says why in one line ("No shared state."). Specs carry no API listings or code samples: signatures belong to the code.

**Requirements** may be grouped under `###` headings. Each is one bullet, one behavior:

```markdown
- **LIB-PALETTES-014** — When a JSON palette entry fails to parse, loading shall fail and name the bad entry.
  - Tests: `PaletteJsonTests.InvalidColorFailsLoad`
```

- Ids are `<spec id>-NNN`, never reused or renumbered. A requirement that no longer holds is struck through and kept: `- ~~**LIB-PALETTES-014**~~ — Removed: reason.`
- Wording is EARS-style: "When …, the X shall …", "While …, the X shall …", "If …, then the X shall …" for unwanted behavior, or "The X shall …" for unconditional behavior. State observable behavior, not implementation.
- `Tests:` lists `Class.Method` for each test that pins the requirement (theory methods by name only). Every cited test must exist. Otherwise it says `untested`, or `manual — <what has to be checked by hand, and where>`. UI behavior that DevTools cannot drive (drag and drop, pointer gestures, popups) is `manual`.

**Decisions** record why the feature behaves as it does: one bullet per decision, led by a bold title, then the decision, its reason and, when known, the alternatives rejected. Requirements refer to a decision by its title, never by a number. A decision governing several features lives in the spec it most constrains; the others name that spec. Project-wide decisions are in [docs/ARCHITECTURE.md](../ARCHITECTURE.md).

```markdown
- **Compressed sources are read-only.** A compressed block can be viewed, arranged and exported, never saved. Reason: recompression rarely fits the original block, and relocating it needs game-specific pointer and archive updates. Rejected: write-back with a size check.
```

**Inherited requirements** are marked `(inherited)` after the id:

```markdown
- **UI-SHELL-031** (inherited) — When a short status message is shown, the status bar shall clear it after 2 s.
  - Tests: untested
```

An inherited requirement records behavior the code has that may not have been human-guided: a default value, a threshold, a limit, or a choice made while implementing that no decision asked for. It is specified so changes to it are visible, but it carries no design intent. Before relying on it as a contract, or when changing it, a human confirms it; once confirmed, the marker is removed.

## Where the specs came from

The specs were extracted from the code, then the tests and the planning documents that preceded them (`PLAN.md`, `docs/FeatureRoadmap.md`, `docs/FeatureGaps.md`, `docs/CompressionSupport.md`); where those disagreed the code won and the disagreement went to Open items. The plans' decisions moved into each spec's Decisions section, their open work into [docs/BACKLOG.md](../BACKLOG.md), and the compression design into a change proposal. Git history keeps the plans. Specs describe; they do not fix: a behavior that looks wrong is specified as it is and logged.
