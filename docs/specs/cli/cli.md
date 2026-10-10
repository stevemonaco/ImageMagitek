---
id: CLI-COMMANDS
title: CLI commands
project: TileShop.CLI
sources:
  - TileShop.CLI/Program.cs
  - TileShop.CLI/CliApplication.cs
  - TileShop.CLI/CliBootstrap.cs
  - TileShop.CLI/Commands
  - TileShop.CLI/CommandHandlers
  - TileShop.CLI/Porters
types:
  - Program
  - CliApplication
  - CliBootstrap
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
tests:
  - CliApplicationTests
depends:
  - LIB-PROJECT-SERVICE
  - LIB-PROJECT-TREE
  - LIB-IMAGE-IO
  - LIB-IMAGES
  - LIB-CODECS
  - LIB-DATASOURCE
  - LIB-ARRANGERS
---

# CLI commands

## Purpose

`TileShopCLI` exports a TileShop project's scattered arrangers to PNG and imports edited PNGs back, for build toolchains. It opens an existing project through `ProjectService` (LIB-PROJECT-SERVICE), writes and reads images through LIB-IMAGE-IO, and reports through console output and an exit code. Parsing uses System.CommandLine; `CliApplication` holds the whole run so tests drive it in-process, and `Program.Main` only supplies the console and the real environment. Packaging is CLI-PUBLISH.

## Requirements

### Invocation and exit codes

- **CLI-COMMANDS-001** — The CLI shall print "TileShopCLI v<informational version> by Klarth" before anything else, the same version `--version` prints.
  - Tests: untested
- **CLI-COMMANDS-002** — The CLI shall accept the verbs `print`, `export`, `exportall`, `import` and `importall`, and their PascalCase spellings `Print`, `Export`, `ExportAll`, `Import` and `ImportAll`.
  - Tests: `CliApplicationTests.PascalCaseVerb_Parses`, `CliApplicationTests.UnknownVerbCasing_Exits3`
- **CLI-COMMANDS-003** — If the arguments do not parse (unknown verb, missing positional value, unknown option), then the CLI shall print the parse errors and the help of the command that failed and exit with -3.
  - Tests: `CliApplicationTests.InvalidArguments_Exits3`, `CliApplicationTests.UnknownVerbCasing_Exits3`
- **CLI-COMMANDS-004** — When `--help`, `<verb> --help` or `--version` is given, the CLI shall print the help or version text and exit with 0 without printing an exit-code description.
  - Tests: `CliApplicationTests.Help_Exits0WithoutStatusLine`, `CliApplicationTests.VerbHelp_Exits0`, `CliApplicationTests.Version_Exits0`
- **CLI-COMMANDS-005** — The CLI shall exit with 0 on success, -2 on an exception, -3 on invalid arguments, -4 when the environment fails to load, -5 when the project cannot be opened, -6 on an import failure, and -7 on an export failure.
  - Tests: `CliApplicationTests.OpenThrows_Exits2`, `CliApplicationTests.InvalidArguments_Exits3`, `CliApplicationTests.EnvironmentFails_Exits4`, `CliApplicationTests.ProjectFileMissing_Exits5`, `CliApplicationTests.Import_MissingImage_Exits6`, `CliApplicationTests.Export_UnknownKey_ExportsOthersExits7`
- **CLI-COMMANDS-006** — Before exiting after running a verb or rejecting arguments, the CLI shall print a one-line description of its exit code ("Operation completed successfully", "Operation failed due to an import error", …).
  - Tests: `CliApplicationTests.OpenYields_ReturnsVerbCode`, `CliApplicationTests.InvalidArguments_Exits3`, `CliApplicationTests.Export_UnknownKey_ExportsOthersExits7`
- **CLI-COMMANDS-007** — If an essential resource cannot be loaded from the application directory (LIB-SERVICES), then the CLI shall print and log the failure and exit with -4; other startup issues shall be printed as warnings and shall not change the exit code.
  - Tests: `CliApplicationTests.BootstrapIssue_PrintsWarningExits0`, `CliApplicationTests.EnvironmentFails_Exits4` (an injected failure); manual — run `TileShopCLI print` with `_palettes/DefaultRgba32.json` renamed in the build output (exit -4)
- **CLI-COMMANDS-008** — When the environment loads, the CLI shall load plugin codecs from `_plugins` in the application directory, as LIB-CODECS-057 specifies.
  - Tests: `CliApplicationTests.Export_ProjectUsingPluginCodec_Exits0`
- **CLI-COMMANDS-009** — If the project cannot be opened or validated, then the CLI shall print "Project '<file>' contained N errors" followed by each numbered reason, and exit with -5.
  - Tests: `CliApplicationTests.ProjectFileMissing_Exits5`, `CliApplicationTests.ProjectNamesUnknownCodec_Exits5`
- **CLI-COMMANDS-010** — If a command throws, then the CLI shall print the exception message and stack trace and exit with -2.
  - Tests: `CliApplicationTests.OpenThrows_Exits2`
- ~~**CLI-COMMANDS-011**~~ — Removed: the CLI awaits the verb, so the exit code is the command's whether or not the project open completes synchronously (CLI-COMMANDS-015).
- **CLI-COMMANDS-015** — When opening the project completes asynchronously (it reads a leftover transaction journal), the CLI shall exit with the verb's code after the verb finishes.
  - Tests: `CliApplicationTests.OpenYields_ReturnsVerbCode`

### Logging

- **CLI-COMMANDS-012** — The CLI shall write its messages to standard output, message text only.
  - Tests: `CliApplicationTests.Print_ListsEveryNode_Exits0` (through the injected writer); manual for the console itself
- **CLI-COMMANDS-013** — When `--log` is not given, the CLI shall append Warning and higher messages, timestamped, to `errorlogCLI<yyyyMM>.txt` in the application directory, starting a new file each month.
  - Tests: `CliApplicationTests.Log_Default_WritesUnderAppDirectory`
- **CLI-COMMANDS-014** — When `--log <file>` is given, the CLI shall append Warning and higher messages to exactly that file, resolved against the current directory.
  - Tests: `CliApplicationTests.Log_ExplicitPath_WritesFailureToThatFile`
- **CLI-COMMANDS-016** — If the log file cannot be written, then the CLI shall run and exit as if logging succeeded.
  - Tests: `CliApplicationTests.Log_Unwritable_RunsAsIfLogged`

### print

- **CLI-COMMANDS-020** — When `print <project>` runs, the CLI shall print every project node depth-first as "<name>: Type '<type>'; Resource Key '<key>'" and exit with 0.
  - Tests: `CliApplicationTests.Print_ListsEveryNode_Exits0`

### export and exportall

- **CLI-COMMANDS-030** — When `export <project> <directory> <key>...` runs, the CLI shall export each key in order to `<directory>/<the arranger's project path>.png`.
  - Tests: `CliApplicationTests.Export_NestedArranger_FreshDirectory_WritesPngExits0`
- **CLI-COMMANDS-031** — When an arranger is exported, the CLI shall print "Exporting '<key>' to '<file>'..." and then "Completed successfully".
  - Tests: `CliApplicationTests.Export_NestedArranger_FreshDirectory_WritesPngExits0`
- **CLI-COMMANDS-032** — If a key is not in the project, then the CLI shall print "Exporting '<key>'...Resource key not found in project", continue with the next key, and count the key as failed.
  - Tests: `CliApplicationTests.Export_UnknownKey_ExportsOthersExits7`
- **CLI-COMMANDS-033** — If a key names something other than a scattered arranger, then the CLI shall print "...Resource key is not a Scattered Arranger", continue, and count the key as failed.
  - Tests: `CliApplicationTests.Export_NotScatteredArranger_Exits7`
- **CLI-COMMANDS-034** — If the PNG already exists and `--overwrite` is not given, then the CLI shall print "File already exists and was skipped to not overwrite it" and continue; with `--overwrite` it shall replace the file.
  - Tests: `CliApplicationTests.Export_Existing_WithoutOverwrite_SkipsExits0`, `CliApplicationTests.Export_Existing_WithOverwrite_Replaces`
- **CLI-COMMANDS-035** — The exported PNG shall be paletted when the arranger's palettes fit one PNG palette and RGBA otherwise, as LIB-IMAGE-IO specifies.
  - Tests: untested
- **CLI-COMMANDS-036** — When the PNG's directory does not exist, the CLI shall create it, including folders for a nested arranger.
  - Tests: `CliApplicationTests.Export_NestedArranger_FreshDirectory_WritesPngExits0`
- ~~**CLI-COMMANDS-037**~~ — Removed: `PixelColorType` has only indexed and direct; an unsupported type would count as failed under CLI-COMMANDS-038.
- **CLI-COMMANDS-038** — When `export` or `exportall` has exported every key, it shall exit with -7 if any key failed and 0 otherwise; a PNG skipped under CLI-COMMANDS-034 is not a failure.
  - Tests: `CliApplicationTests.Export_UnknownKey_ExportsOthersExits7`, `CliApplicationTests.Export_Existing_WithoutOverwrite_SkipsExits0`
- **CLI-COMMANDS-039** — When `exportall <project> <directory>` runs, the CLI shall export every scattered arranger in the project, depth-first, as `export` does.
  - Tests: `CliApplicationTests.ExportAll_WritesEveryScatteredArranger`
- **CLI-COMMANDS-040** — If an arranger reads from a missing data file, then the export shall print "...Data file '<name>' is missing at '<path>'", write nothing for that arranger and count it as failed.
  - Tests: `CliApplicationTests.Export_MissingDataFile_NamesFileExits7`, `CliApplicationTests.ExportAll_MissingDataFile_ExportsOthersExits7`
- **CLI-COMMANDS-041** — If writing a PNG fails with an I/O or access error, then the CLI shall print the reason, count the key as failed and continue.
  - Tests: `CliApplicationTests.Export_UnwritablePng_ContinuesExits7`

### import and importall

- **CLI-COMMANDS-050** — When `import <project> <directory> <key>...` runs, the CLI shall import each key in order from `<directory>/<key's segments split on / or \>.png`.
  - Tests: `CliApplicationTests.Import_EditedPng_WritesDataFileExits0`
- **CLI-COMMANDS-051** — When an image is imported, the CLI shall print "Importing '<file>' to '<key>'..." and then the outcome.
  - Tests: `CliApplicationTests.Import_EditedPng_WritesDataFileExits0`
- **CLI-COMMANDS-052** — If the image file does not exist, then the CLI shall print "File does not exist", checked after the key, read-only and data file checks.
  - Tests: `CliApplicationTests.Import_MissingImage_Exits6`, `CliApplicationTests.Import_ReadOnlyArranger_Exits6`
- **CLI-COMMANDS-053** — If the key is not in the project or is not a scattered arranger, then the CLI shall print "Resource key does not exist or is not a ScatteredArranger".
  - Tests: `CliApplicationTests.Import_BadKey_Exits6`
- **CLI-COMMANDS-054** — The CLI shall stage indexed imports with the strategy given by `--match exact|nearest|nearestrgb` (exact by default, any casing), the distance limit given by `--max-distance`, and transparent pixels mapped to index 0 when `--transparent-index0` is given (LIB-IMAGE-IO-028 to -032).
  - Tests: `CliApplicationTests.Import_MatchNearest_SubstitutesExits0`, `CliApplicationTests.Import_MaxDistance_RejectsFarColorExits6`, `CliApplicationTests.Import_TransparentIndex0_MapsToIndexZero`
- **CLI-COMMANDS-055** — If any image color has no palette match, then the CLI shall print the import report's summary and write nothing for that arranger.
  - Tests: `CliApplicationTests.Import_UnmatchedColor_Exits6_DataUnchanged`
- **CLI-COMMANDS-056** — If the image cannot be loaded, then the CLI shall print the reason and treat the import as failed.
  - Tests: `CliApplicationTests.Import_UnreadableImage_Exits6`
- **CLI-COMMANDS-057** — When an import succeeds, the CLI shall write the pixels to the data files at once, print "Completed successfully (<report summary>)", and leave the project files unchanged.
  - Tests: `CliApplicationTests.Import_EditedPng_WritesDataFileExits0`
- **CLI-COMMANDS-058** — With `-f` the CLI shall skip missing image files, and with `-r` skip bad keys, continuing with the next arranger.
  - Tests: `CliApplicationTests.Import_MissingImage_WithF_Exits0`, `CliApplicationTests.Import_BadKey_WithR_Exits0`
- **CLI-COMMANDS-059** — If an import does not succeed and is not skipped, then the CLI shall stop at once and exit with -6, leaving arrangers imported before it written.
  - Tests: `CliApplicationTests.Import_StopsAtFirstFailure_EarlierArrangersWritten`
- **CLI-COMMANDS-060** — When `importall <project> <directory>` runs, the CLI shall import every scattered arranger, depth-first, from `<directory>/<the arranger's project path>.png`, with the same skipping and stopping rules as `import`, and skipping read-only arrangers.
  - Tests: `CliApplicationTests.ImportAll_ReadOnlyArranger_SkipsAndImportsRest`
- **CLI-COMMANDS-061** — If an arranger reads from a missing data file, then the import shall print "...Data file '<name>' is missing at '<path>'", write nothing, and fail even with `-f`.
  - Tests: `CliApplicationTests.Import_MissingDataFile_NamesFileExits6`, `CliApplicationTests.Import_MissingDataFile_WithF_StillExits6`
- **CLI-COMMANDS-062** — If `--max-distance` is given with exact matching or is negative, then the CLI shall reject the arguments (-3).
  - Tests: `CliApplicationTests.Import_MaxDistanceWithExact_Exits3`
- **CLI-COMMANDS-063** — When `importall` reaches an arranger that is read-only only because a codec cannot encode, the CLI shall print "Skipped: arranger is read-only (<reason>)" and continue, without affecting the exit code.
  - Tests: `CliApplicationTests.ImportAll_ReadOnlyArranger_SkipsAndImportsRest`
- **CLI-COMMANDS-065** — If `importall` reaches an arranger that reads a read-only data file, then the CLI shall print "Arranger is read-only because it <reason>" and treat the import as failed.
  - Tests: `CliApplicationTests.ImportAll_ReadOnlyDataFile_Exits6`
- **CLI-COMMANDS-064** — If `import` names a read-only arranger, then the CLI shall print "Arranger is read-only because it <reason>" and treat the import as failed.
  - Tests: `CliApplicationTests.Import_ReadOnlyArranger_Exits6`

## Invariants

- Only scattered arrangers are exported or imported.
- Export never writes a data file; import never writes a project file.
- Each run opens one project and runs one verb.

## Edge cases

- `-r` has no effect on `importall`, whose keys always exist.
- Keys may start with `/` or use `\`; both resolve to the same node and image path.
- Project-open errors and per-key messages go to standard output only, never to the log file.
- The `--match` parser also accepts the enum's defined numeric values (`--match 1`); help lists only the names. An undefined number is an argument error (-3).
- A project path starting with `@` is taken literally; response files are not expanded.
- An indexed arranger whose pixel indices lie past its palette's last entry fails export with an exception (-2) rather than -7; LIB-IMAGE-IO's RGBA fallback does not handle it (backlog).

## Threading and lifetime

- Single run: `Main` awaits `CliApplication.RunAsync`, which awaits the verb handler, so the exit code is read only after the verb finishes.
- The logger and project service are created per run, after parsing, and disposed or closed before `RunAsync` returns; nothing is process-wide, so tests run in parallel.

## Decisions

- **Distinct exit codes per failure class.** Zero for success and a negative code per kind of failure. Reason: toolchain scripts branch on the outcome without parsing output.
- **Exported paths mirror the project tree.** An arranger at `Sprites/Hero` exports to `<directory>/Sprites/Hero.png`. Reason: `importall` finds every edited image again with no manifest.
- **Import writes data files only.** Reason: an import changes pixels, never project structure, so the project files stay as they were.
- **Skips are opt-in, except read-only arrangers in `importall`.** Without `-f`/`-r`, a missing image or bad key fails the run. Reason: a build should fail loudly unless the caller says partial input is expected. The one default skip is `importall` over a read-only arranger (below).
- **The CLI's exit code ignores non-fatal startup issues.** Issues print as warnings (`CliBootstrap` writes each `BootstrapService.Issues` message to the output writer, since the console log sink is gone) and go to the log; the exit code is unchanged, and an essential failure (`BootstrapException`) exits -4 as before. Reason: a toolchain should not break because of an unrelated bad codec file; if the project needs a skipped codec, opening it fails with -5. Rejected: a new "degraded" exit code (breaks toolchains that only check for zero).
- **System.CommandLine replaces CommandLineParser.** Version 2.0 (stable since .NET 10, Microsoft-maintained). Commands are built in code, so nothing is discovered by reflection and the trimmed publish no longer roots the parser; help and version exit 0 natively; `Parse` returns a result the CLI inspects itself, so exit codes stay ours. Rejected: keeping CommandLineParser (no release in four years, reflection, help as an error); Spectre.Console.Cli (reflection-based, trimming warnings); ConsoleAppFramework (source-generated and AOT-friendly, but less control over help and a smaller user base); CliFx and Cocona (reflection, Cocona stalled).
- **`Main` awaits; no parser callbacks for verbs.** `CliApplication` parses, and awaits the handler for the parsed command itself; no verb has a System.CommandLine action. Help, version and parse errors do use the parser's own actions (`ParseResult.Invoke` with both writers set to the CLI's output), and the CLI maps them to 0 or -3. Reason: the old async-void trap came from running the handler inside the parser; dispatching ourselves keeps the exit code and the status line in one place. Rejected: async actions through `InvokeAsync` (sound, but splits exit-code mapping between the parser and the CLI).
- **Lowercase verbs with PascalCase aliases.** Other casings are unknown verbs (-3); options are lowercase only. Reason: System.CommandLine matches case-sensitively; the aliases keep the spellings existing scripts use. Rejected: rewriting the verb token before parsing to stay fully case-insensitive (a shim for casings nobody documents); lowercase only (breaks scripts written against the PascalCase names).
- **Per-verb help is `<verb> --help`.** `-h` and `-?` also work; `help <verb>` is an unknown verb (-3). Reason: the parser's native form. Rejected: a `help` command kept for compatibility.
- **Response files are off.** `ResponseFileTokenReplacer` is null. Reason: a project or directory path starting with `@` must not be read as a response file; scripts pass a handful of arguments.
- **An in-process `CliApplication`, tested by project reference.** `CliApplication` takes the output writer, a bootstrap function that builds the `IProjectService` (or throws, for -4), and the default log path. `ImageMagitek.UnitTests` references `TileShop.CLI` as it references `TileShop.UI`. Reason: tests inject a project service whose open yields (the journal case) or throws (-2), point plugins at a temp folder, and run in parallel. Rejected: spawning the built exe (slow, build-order coupling, cannot inject); a separate `TileShop.CLI.Core` library (not needed: the exe reference builds).
- **Output through an injected writer, not `Console` or the Serilog console sink.** Handlers, `Exporter`, `Importer`, the bootstrap issues and the parser's help and errors all write to the writer; Serilog keeps only the file log. Reason: `Console.SetOut` is process-wide, so parallel tests would interleave.
- **Export reports every failure, then exits -7.** `Exporter.ExportArranger` returns false for a failed key (an existing PNG skipped without `--overwrite` is not a failure); the handler exports all keys and exits -7 if any key failed. `IOException` and `UnauthorizedAccessException` writing one PNG fail that key. Reason: export writes no data files, so finishing the rest is harmless and reports every broken key in one run. Rejected: stopping at the first failure like import (import stops because it writes ROM data); a `-r` flag for export (the caller named the keys, so a bad one is an error).
- **An existing PNG without `--overwrite` is a skip, not a failure.** Reason: it is the documented protective default and the caller chose it. Rejected: failing -7, which would make re-running an export without `--overwrite` always fail.
- **Missing data files are checked before reading.** Both verbs call `Arranger.FindMissingDataSource` (LIB-ARRANGERS), the same rule the UI uses. Reason: the message names the file instead of a `FileNotFoundException` stack. Rejected: catching `IOException` around the read, which cannot reliably name the file and fires mid-image.
- **A missing data file is never skipped.** `-f` skips missing image files only. Reason: a moved ROM is an environment fault the build must surface; skipping it would silently ship stale graphics.
- **`importall` skips decode-only arrangers by default; `import` fails on them.** Reason: a decode-only codec makes the arranger unwritable by design, not by bad input, and `importall` cannot know which arrangers the caller meant; `import` names the key and supplies an image that will not be written, so it is an error (-6). A read-only data file fails `importall` too: it is an environment fault (file attribute, permissions), and skipping it would exit 0 having written nothing.
- **Check order: key, read-only, missing data file, image file.** Reason: a read-only arranger with no PNG is skipped rather than failed, and a bad key is reported as a bad key even when its image is also missing.
- **Import options mirror the UI.** `--transparent-index0` sets `MapTransparentToIndexZero` with `AlphaThreshold` 0 as the UI does; the options do not affect direct-color arrangers. `--max-distance` with exact matching, or negative, is an argument error. Reason: one behavior per option across UI and CLI; rejecting a meaningless combination catches a mistyped script instead of silently ignoring it. Rejected: exposing `AlphaThreshold` (the UI does not; add both together later).
- **The banner shows the informational version.** It reads the same `AssemblyInformationalVersionAttribute` as `--version`, so both print `1.0.0+<hash>`. Reason: one version string per build in bug reports; how the hash is produced is decided in CLI-PUBLISH. Rejected: `Assembly.GetName().Version` (drops the suffix and the commit, and disagrees with `--version`).
- **Help and version are successes.** They print with no status line and nothing logged. Reason: asking for help is not a failed operation.
- **The logger is built after parsing.** An explicit `--log` path gets a non-rolling file sink, so the file named is the file written; the default is `errorlogCLI.txt` in the application directory, rolling monthly. Parse errors, before a log path is known, use the default. Serilog's file sink drops writes it cannot make (`SelfLog`). Reason: the documented default, and a build tree free of log files. Rejected: the current directory (litters the caller's tree, and was never the stated intent).
- **Plugins load like the UI's.** `CliBootstrap` calls `CreatePluginService` after the codec service and before the serializer factory, in the UI's order. How a bad plugin DLL is tolerated is decided in ARCHITECTURE.md §6 and LIB-CODECS.

## Non-goals

- Creating or editing projects (new, add-datafile, add-palette, add-arranger verbs).
- Palette import/export verbs, dry-run reports, machine-readable (`--json`) output, key globs, batch manifests, scripting.
- Sequential arrangers and raw data-file export.

## Open items

- README has no CLI usage and lists .NET 6, Autofac and Jot (backlog).
