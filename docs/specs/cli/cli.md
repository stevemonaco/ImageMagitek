---
id: CLI-COMMANDS
title: CLI commands
project: TileShop.CLI
sources:
  - TileShop.CLI/Program.cs
  - TileShop.CLI/Commands
  - TileShop.CLI/CommandHandlers
  - TileShop.CLI/Porters
types:
  - Program
  - ExitCode
  - PrintOptions
  - ExportOptions
  - ExportAllOptions
  - ImportOptions
  - ImportAllOptions
  - ProjectCommandHandler
  - PrintHandler
  - ExportHandler
  - ExportAllHandler
  - ImportHandler
  - ImportAllHandler
  - Exporter
  - Importer
  - ImportResult
tests: []
depends:
  - LIB-PROJECT-SERVICE
  - LIB-PROJECT-TREE
  - LIB-IMAGE-IO
  - LIB-IMAGES
  - LIB-CODECS
  - LIB-DATASOURCE
---

# CLI commands

## Purpose

`TileShopCLI` exports a TileShop project's scattered arrangers to PNG and imports edited PNGs back, for build toolchains. It opens an existing project through `ProjectService` (LIB-PROJECT-SERVICE), writes and reads images through LIB-IMAGE-IO, and reports through console output and an exit code. Packaging is CLI-PUBLISH.

## Requirements

### Invocation and exit codes

- **CLI-COMMANDS-001** (inherited) — The CLI shall print "TileShopCLI v0.992 by Klarth" before anything else.
  - Tests: untested
- **CLI-COMMANDS-002** — The CLI shall accept the verbs `print`, `export`, `exportall`, `import` and `importall`, ignoring case.
  - Tests: untested
- **CLI-COMMANDS-003** — If the arguments do not parse (unknown verb, missing positional value, unknown option), then the CLI shall print the help with a verb index and exit with -3.
  - Tests: untested
- **CLI-COMMANDS-004** — When `--help` or `--version` is given, the CLI shall print help or version text and exit with -3.
  - Tests: untested
- **CLI-COMMANDS-005** — The CLI shall exit with 0 on success, -1 when no code was set, -2 on an exception, -3 on invalid arguments, -4 when the environment fails to load, -5 when the project cannot be opened, -6 on an import failure, and -7 on an export failure.
  - Tests: untested
- **CLI-COMMANDS-006** — Before exiting, the CLI shall print a one-line description of its exit code ("Operation completed successfully", "Operation failed due to an import error", …).
  - Tests: untested
- **CLI-COMMANDS-007** — If settings, codecs, palettes or schemas cannot be loaded from the application directory, then the CLI shall log the failure and exit with -4.
  - Tests: untested
- **CLI-COMMANDS-008** — The CLI shall not load plugin codecs.
  - Tests: untested
- **CLI-COMMANDS-009** — If the project cannot be opened or validated, then the CLI shall print "Project '<file>' contained N errors" followed by each numbered reason, and exit with -5.
  - Tests: untested
- **CLI-COMMANDS-010** — If a command throws, then the CLI shall print the exception message and stack trace and exit with -2.
  - Tests: untested
- **CLI-COMMANDS-011** — If opening the project completes asynchronously (it reads a leftover transaction journal), then the CLI shall exit with -1 before the command finishes.
  - Tests: untested

### Logging

- **CLI-COMMANDS-012** — The CLI shall write Information and higher messages to the console, message text only.
  - Tests: untested
- **CLI-COMMANDS-013** (inherited) — The CLI shall append Warning and higher messages, timestamped, to `errorlogCLI<yyyyMM>.txt` in the current directory, starting a new file each month.
  - Tests: untested
- **CLI-COMMANDS-014** — Every verb shall accept `--log <file>`, and the option shall have no effect.
  - Tests: untested

### print

- **CLI-COMMANDS-020** — When `print <project>` runs, the CLI shall print every project node depth-first as "<name>: Type '<type>'; Resource Key '<key>'" and exit with 0.
  - Tests: untested

### export and exportall

- **CLI-COMMANDS-030** — When `export <project> <directory> <key>...` runs, the CLI shall export each key in order to `<directory>/<the arranger's project path>.png`.
  - Tests: untested
- **CLI-COMMANDS-031** — When an arranger is exported, the CLI shall print "Exporting '<key>' to '<file>'..." and then "Completed successfully".
  - Tests: untested
- **CLI-COMMANDS-032** — If a key is not in the project, then the CLI shall print "Exporting '<key>'...Resource key not found in project" and continue with the next key.
  - Tests: untested
- **CLI-COMMANDS-033** — If a key names something other than a scattered arranger, then the CLI shall print "...Resource key is not a Scattered Arranger" and continue.
  - Tests: untested
- **CLI-COMMANDS-034** — If the PNG already exists and `--overwrite` is not given, then the CLI shall print "File already exists and was skipped to not overwrite it" and continue; with `--overwrite` it shall replace the file.
  - Tests: untested
- **CLI-COMMANDS-035** — The exported PNG shall be paletted when the arranger's palettes fit one PNG palette and RGBA otherwise, as LIB-IMAGE-IO specifies.
  - Tests: untested
- **CLI-COMMANDS-036** — If the PNG's directory does not exist (the export directory itself, or a folder for a nested arranger), then the export shall throw and the CLI shall exit with -2 without exporting the remaining keys.
  - Tests: untested
- **CLI-COMMANDS-037** — If an arranger's color type is neither indexed nor direct, then the CLI shall write no file and still print "Completed successfully".
  - Tests: untested
- **CLI-COMMANDS-038** — `export` and `exportall` shall exit with 0 even when keys were missing, skipped or of the wrong type.
  - Tests: untested
- **CLI-COMMANDS-039** — When `exportall <project> <directory>` runs, the CLI shall export every scattered arranger in the project, depth-first, as `export` does.
  - Tests: untested
- **CLI-COMMANDS-040** — If an arranger reads from a missing data file, then the export shall throw and the CLI shall exit with -2.
  - Tests: untested

### import and importall

- **CLI-COMMANDS-050** — When `import <project> <directory> <key>...` runs, the CLI shall import each key in order from `<directory>/<key's segments split on / or \>.png`.
  - Tests: untested
- **CLI-COMMANDS-051** — When an image is imported, the CLI shall print "Importing '<file>' to '<key>'..." and then the outcome.
  - Tests: untested
- **CLI-COMMANDS-052** — If the image file does not exist, then the CLI shall print "File does not exist", checked before the key.
  - Tests: untested
- **CLI-COMMANDS-053** — If the key is not in the project or is not a scattered arranger, then the CLI shall print "Resource key does not exist or is not a ScatteredArranger".
  - Tests: untested
- **CLI-COMMANDS-054** — The CLI shall stage images with exact color matching and without mapping transparent pixels to index 0 (LIB-IMAGE-IO defaults), and shall offer no option to change that.
  - Tests: untested
- **CLI-COMMANDS-055** — If any image color has no exact palette match, then the CLI shall print the import report's summary and write nothing for that arranger.
  - Tests: untested
- **CLI-COMMANDS-056** — If the arranger is read-only or the image cannot be loaded, then the CLI shall print the reason and treat the import as failed.
  - Tests: `ReadOnlyArrangerTests.Prepare_ReadOnly_Fails` (library side); the CLI path is untested.
- **CLI-COMMANDS-057** — When an import succeeds, the CLI shall write the pixels to the data files at once, print "Completed successfully (<report summary>)", and leave the project files unchanged.
  - Tests: untested
- **CLI-COMMANDS-058** — With `-f` the CLI shall skip missing image files, and with `-r` skip bad keys, continuing with the next arranger.
  - Tests: untested
- **CLI-COMMANDS-059** — If an import does not succeed and is not skipped, then the CLI shall stop at once and exit with -6, leaving arrangers imported before it written.
  - Tests: untested
- **CLI-COMMANDS-060** — When `importall <project> <directory>` runs, the CLI shall import every scattered arranger, depth-first, from `<directory>/<the arranger's project path>.png`, with the same skipping and stopping rules as `import`.
  - Tests: untested
- **CLI-COMMANDS-061** — If an arranger reads from a missing data file, then the import shall throw and the CLI shall exit with -2.
  - Tests: untested

## Invariants

- Only scattered arrangers are exported or imported.
- Export never writes a data file; import never writes a project file.
- Each run opens one project and runs one verb.

## Edge cases

- A run whose transaction journal needs recovery exits -1 while the command is still running (CLI-COMMANDS-011).
- `importall` stops at the first read-only arranger, so arrangers after it are never imported.
- `-r` has no effect on `importall`, whose keys always exist.
- Keys may start with `/` or use `\`; both resolve to the same node and image path.
- Project-open errors and per-key messages go to the console only, never to the log file.
- A project naming a plugin codec gets no codec for those elements (CLI-COMMANDS-008); building the tree then throws, which should surface as a project-open failure (-5). Unverified.

## Threading and lifetime

- Single run, single thread in the common case; the verb handler runs inside the parser's callback, which is async-void (CLI-COMMANDS-011).
- The logger and project service are process-wide statics, created at startup and never disposed.

## Decisions

- **Distinct exit codes per failure class.** Zero for success and a negative code per kind of failure. Reason: toolchain scripts branch on the outcome without parsing output.
- **Exported paths mirror the project tree.** An arranger at `Sprites/Hero` exports to `<directory>/Sprites/Hero.png`. Reason: `importall` finds every edited image again with no manifest.
- **Import writes data files only.** Reason: an import changes pixels, never project structure, so the project files stay as they were.
- **Skips are opt-in.** Without `-f`/`-r`, a missing image or bad key fails the run. Reason: a build should fail loudly unless the caller says partial input is expected.

## Non-goals

- Creating or editing projects (new, add-datafile, add-palette, add-arranger verbs).
- Palette import/export verbs, dry-run reports, machine-readable (`--json`) output, key globs, batch manifests, scripting.
- Sequential arrangers and raw data-file export.

## Open items

- The parser callback is async-void: exit codes are correct only because the project open completes synchronously when no journal exists.
- The export directory check is inverted (`Directory.Exists` before `CreateDirectory`), so nested and missing directories are never created (CLI-COMMANDS-036). It fails with an exception, exit -2.
- Export ignores `ExportArranger`'s result, so -7 is never returned (CLI-COMMANDS-038), and unknown color types report success (CLI-COMMANDS-037).
- `--log` is parsed and its full path computed, but the logger is created at startup with the default name and the path is never used (CLI-COMMANDS-014). `GetFullLogFileName`'s doc says the default log lives in the application directory; it is written to the current directory.
- Import has no `--match`, `--max-distance` or `--transparent-index0` options (CLI-COMMANDS-054).
- Plugin codecs are not loaded; the blocking constructor problem is fixed (CLI-COMMANDS-008).
- Read-only arrangers fail the import instead of being skipped and reported (CLI-COMMANDS-056), and halt `importall`.
- Missing data files surface as an exception (-2) rather than a per-arranger message (CLI-COMMANDS-040, -061).
- `--help` and `--version` exit -3 and log "Operation failed due to invalid command line options" (CLI-COMMANDS-004).
- Verb help text disagrees with behavior: `ExportAll` says "all project resources" (only scattered arrangers); `Import`/`ImportAll` say "skipping resources that cannot be located" (only with `-f`/`-r`).
- README has no CLI usage and lists .NET 6, Autofac, Jot and Nuke (backlog).
- No handler or exit code has a test.
