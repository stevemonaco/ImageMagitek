---
id: LIB-EXAMPLE
title: Example feature
project: ImageMagitek
sources:
  - ImageMagitek/Example/Example.cs
types:
  - Example
  - ExampleOptions
tests:
  - ExampleTests
depends:
  - LIB-DATASOURCE
---

# Example feature

## Purpose

Two or three sentences: what the feature is for and where it sits (which spec it builds on, which builds on it). When the spec specifies members of a type another spec owns, one line names them.

## Requirements

### Group name

- **LIB-EXAMPLE-001** — When `Offset` points past the end of the source, the example shall read the missing bytes as zero.
  - Tests: `ExampleTests.ReadPastEndReturnsZeros`
- **LIB-EXAMPLE-002** — If the codec cannot encode, then the example shall refuse to save before writing any element.
  - Tests: untested
- **LIB-EXAMPLE-003** — When the editor is closed with pending edits, the app shall prompt to save.
  - Tests: manual — close a modified editor tab in TileShop.UI.
- ~~**LIB-EXAMPLE-004**~~ — Removed: superseded by LIB-EXAMPLE-005.

### Defaults

- **LIB-EXAMPLE-005** (inherited) — When `MaxDistance` is not set, matching shall accept any distance.
  - Tests: untested

## Invariants

- What holds after every call.

## Edge cases

- Empty sources, zero-size elements, missing files, reentrant events, failures mid-way…

## Threading and lifetime

- Which thread, what keeps the object alive, what releases it, which events it raises and who subscribes.

## Decisions

- **A short title.** The decision. Reason: why. Rejected: the alternatives and why, when known.

## Non-goals

- What the feature deliberately does not do.

## Open items

- Disagreements between code, tests and old plans found while writing the spec; behavior with no test that should have one; pending manual checks. "None." when empty.
