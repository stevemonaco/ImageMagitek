# Freeze the 1.0 project format

## Why

1.0 users keep projects for years and often keep them in git. After 1.0, any change to what is stored on disk, or to what a stored value means, breaks their projects. Everything still open about the format is settled here, before the freeze, so later changes are additive or go through migration. Backlog items, each checked against the code:

- **1.0** [LIB-PROJECT-FORMAT] Stable resource keys or path keys. Not yet decided. The analysis is under Decisions and the choice is open question 1.
- **1.0** [LIB-PROJECT-FORMAT] Schema versioning and migration. Confirmed: `XmlProjectWriter` writes `Version => "0.9"`, `XmlProjectReader` parses it into `ImageProjectModel.Version` and never compares it, and `ImageProjectModel.ResourceEquals` ignores `Version`. Only the project file carries a version; resource files have none.
- **1.0** [LIB-PROJECT-FORMAT] Remove the scattered color source from the 1.0 schema, together with:
  - [LIB-PALETTES, LIB-PROJECT-FORMAT] the `MapToModel` infinite loop. Confirmed: the `ScatteredColorSource` branch never advances `i`, and an `IColorSource` of any other type matches no branch, so it loops too.
  - [UI-PALETTE-EDITOR] opening such a palette throws. Confirmed: `PaletteEditorViewModel`'s source-row builder throws `NotSupportedException`.
  - The dead `ScatteredColorSourceModel` (TileShop.Shared), which has no references.
  - The reader branches for `scatteredcolor`, `import` and `export`, which the schema already rejects.

  Correction: none of this can be reached from a project file. The schema has no element for the source, and nothing in the solution constructs a `ScatteredColorSource`. The hang and the throw are reachable only from code. The removal is still worth doing so a stub is not frozen into the type surface. `ColorSourceSerializer.LoadColors` (an empty entry) and `StoreColors` (a throw) also branch on the type.
- (promoted) [LIB-PROJECT-FORMAT] A malformed or foreign `*.xml` anywhere under the project directory fails the whole load, and every other directory becomes a folder. Confirmed: `Directory.GetDirectories(..., AllDirectories)` and `GetFiles("*.xml", AllDirectories)` scan everything, and every file is validated against the schema before its root element is checked. Concretely, `.git` and all its subdirectories become folders, and Rider's `.idea/*.xml` or Visual Studio's `.vs` files fail the load. 1.0: romhackers keep projects in git, and which files the reader scans is part of the format contract, so it must be fixed before the freeze.
- (promoted) [LIB-ARRANGERS] Rotating a mirrored element turns it the opposite way on screen. Confirmed: render shows `M(R(decoded))` (LIB-IMAGES-006), and `TryRotateElement` composes the requested turn into `R` whatever the mirror is. Because `H∘L = R∘H`, Rotate Left on a horizontally mirrored element turns it clockwise. Correction to the backlog's reason for promoting this: the fix belongs in the edit operation, not in the stored meaning. Stored `(mirror, rotation)` already has one consistent meaning (rotate, then mirror) in render (LIB-IMAGES-006) and encode (LIB-IMAGES-015). Fixing `TryRotateElement` changes how no saved project renders, and no migration is needed. It is in this change to pin that meaning before the freeze.
- (promoted) [LIB-PROJECT-FORMAT] `version` is parsed with the current culture. Confirmed, and worse than the backlog says. `decimal.Parse("0.9")` throws `FormatException` under fr-FR, so every project fails to open on a French-locale machine. Under de-DE it returns `9`, which would read as a newer version once versions are compared. 1.0: every project fails to open for a large group of users.

## What

**Version.** The writer writes `version="1.0"`, and `ProjectFormat.Current` is the one place that defines it. The reader reads `version` before validating the schema and parses it as `major.minor` with `System.Version`, which does not depend on culture.

- A project newer than `Current` fails to load before any resource is read: "'FF2project.xml' uses project format 1.1; this version of TileShop reads format 1.0 and older."
- A project older than `Current` is migrated in memory. Chained `IProjectMigration` steps run on every file's `XDocument` before schema validation, and the tree records the version it was read at (`ImageProject.FormatVersion`).
- A version with no chain of steps to `Current` fails the load.
- The 0.9 → 1.0 step changes nothing but the version, unless open question 1 chooses stable keys; in that case the step assigns the keys.

**Upgrade.** A migrated project is never written partly in the new format. While a tree's `FormatVersion` is older than `Current`, every XML write throws, except `ProjectService.UpgradeProjectAsync`. The upgrade writes every resource file and the project file at `Current` in one write-ahead-log transaction. When the UI opens a migrated project it asks first: "Upgrade Project Format" — "'<name>' uses project format 0.9. TileShop will upgrade its files to format 1.0; older versions of TileShop cannot open it afterwards." [Upgrade] [Cancel]. Cancel closes the project. If the upgrade fails, the UI alerts with the reason and closes the project, and no file has changed. The CLI never writes XML, so it reads older projects in memory and never upgrades them.

**Directory scanning.** The reader skips every directory and file whose name starts with `.`, together with everything under such a directory. Other directories still become folders, including empty ones. Each `*.xml` file it finds is handled as follows:

- A file whose root element is not `project`, `datafile`, `palette` or `arranger` is ignored.
- Another project file is ignored, as today.
- A file that is not well-formed XML fails the load, naming the file and line.
- A file with a recognized root that fails the schema fails the load, as today.

Add, create folder and rename refuse a name that starts with `.`.

**Scattered color source.** The scattered color source is removed:

- `ScatteredColorSource` and `ScatteredColorSourceModel` are deleted.
- So are their branches in `ColorSourceSerializer`, `SerializationMapperExtensions` and the reader's `scatteredcolor`/`import`/`export` branches.
- So is the commented-out model mapping.

A color source of any type other than file, project native or project foreign throws `NotSupportedException` naming the type, in `MapToModel`, `LoadColors` and `StoreColors`. It never loops.

**Mirror and rotation.** The stored `(mirror, rotation)` keeps its meaning, decode → rotate → mirror. `TryRotateElement` turns the element as displayed: when the element is mirrored on exactly one axis, it reverses the requested direction before composing. `TryMirrorElement` already composes in display space and is unchanged.

**References.** Recommended: references stay path keys. The sibling proposal [project-format-round-trip.md](project-format-round-trip.md) makes an unresolved key fail the load, naming the key, so a hand rename or `git mv` fails loudly instead of losing references silently.

**What does not change.** The file layout, every element and attribute name, path-key syntax (if open question 1 keeps path keys), the write-ahead log, the render and encode order, and the in-memory model of references (object references). Every 0.9 project that loads today, has no dot-prefixed folder holding resources, and has no foreign XML loads with identical content.

## Decisions

- **References stay tree path keys (recommended, open question 1).** Analysis against the code:
  - **Where key strings exist.** In memory, elements and palettes hold object references, and key strings appear only at the file boundary.
    - On write, the key map is built in `XmlProjectWriter.CreateResourceMap`, and a duplicate is built in `ProjectService.UpdateNodeModel.GetResourceMap`.
    - On read, three lookups in `ProjectTreeBuilder` (`AddPalette`, `ResolvePalette`, `CreateElement`) go through `ProjectTree.TryGetItem(pathKey)`.
  - **Renames and moves.** No code rewrites references on rename or move. `RenameResourceAsync` (folder and resource branches) and `MoveNodeAsync` change the tree, then call `WriteProjectAsync`. Its model diff (LIB-PROJECT-FORMAT-032) catches every referencing file, and the whole write is one write-ahead-log transaction. The directory or file move itself is outside the log, with a compensating move on failure.
  - **Disk cost.** In the FF2 sample, renaming the data file `FF2` rewrites 114 of 116 resource files. Renaming the folder `Character Battle Sprites/Palettes` rewrites the 16 arrangers that reference it. The cost is diff noise and git merge conflicts, not correctness.
  - **The real hazard is hand renames.** A rename, move or `git mv` done outside TileShop leaves dangling references. A data file key then fails the load without naming the key. A palette key silently falls back (LIB-PROJECT-FORMAT-023), and the next project write stores the fallback, which loses work. project-format-round-trip.md fixes this by failing the load and naming the key, which works with either key scheme.
  - **What stable keys would cost.**
    - A `key` attribute on every resource file, plus a key on `ResourceModel` and the node.
    - Unique key assignment at both creation sites (`ProjectService.AddResource`, `CreateNewProjectWithExistingFileAsync`) and in the migration step.
    - A key index in `ProjectTreeBuilder`.
    - Detection of duplicate keys, which a file copied in Explorer would produce; the load fails naming both files.
    - A rule that separates project keys from global palette names.
    - Keys drift from names: after a rename, `palette="Pal0"` points at `Title Palette.xml`. That costs the hand-editability that is the reason path keys were chosen (ARCHITECTURE §6).
  - **What stable keys would gain.** A rename or move writes only the moved file (a folder rename writes nothing), hand renames keep their references, and diffs are smaller.
  - **Why path keys.** In-app renames are already atomic and correct with no code specific to references. Once the round-trip change lands, hand edits fail loudly and lose nothing. Keys add a second identity that drifts from the name. The choice is reversible: with the migration hook, stable keys can arrive later as format 1.x, with a step that derives each key from its path key and a forced upgrade. Doing it later costs a version bump, not a breaking change.
  - **Rejected.** GUID keys (cannot be edited by hand); an id plus a path hint (stale hints mislead); a fallback to a unique name match when a path fails (ambiguous, and it hides moves).
- **The version lives on the project file only.** A project has one format version, and every file under it is read and migrated as that version. Reason: per-file versions would allow mixed projects, and a step that rewrites references needs to see every file. Rejected: a `version` attribute on each resource file.
- **`major.minor` parsed as `System.Version`.** Reason: `Version.Parse` does not depend on culture, and it orders `1.10` after `1.9`; `decimal` treats `1.10` as `1.1`. The schema type becomes a pattern, digits `.` digits. Rejected: `decimal.Parse` with `InvariantCulture`, which fixes the culture bug but keeps the wrong ordering.
- **Reject every newer version, major or minor.** Reason: a 1.0 reader cannot keep elements it does not know, and its next save would drop them, which loses work. Rejected: accepting newer minor versions as "additive".
- **The version check runs before schema validation.** Reason: a newer project file may carry attributes this schema rejects. The user must see "newer format", not a schema error.
- **Migrate in memory, and write only on an explicit upgrade.** Migration steps take the whole set of `XDocument`s (the project file and every resource file, by relative path) and run before validation against the current schema, so only the current schema is kept.
  - Opening never writes. Reason: a project in git should not change because it was opened, and older TileShop versions keep reading it until the user agrees to upgrade.
  - All writes are refused until the upgrade. Reason: `SaveResourceAsync` writes one file (LIB-PROJECT-SERVICE-024), and `ImageProjectModel.ResourceEquals` ignores the version. A partial write would leave a 0.9 project file over resources in the new form, and the next open would migrate them a second time.
  - The writer enforces the refusal by throwing. Every XML write goes through `IProjectSerializerFactory.CreateWriter`, and the UI never reaches a refused write because it upgrades or closes first (ARCHITECTURE: exceptions for programming errors).
  - Rejected: upgrading silently on open; upgrading on the first save (mixed states as above); writing old formats back.
- **The CLI reads older projects without upgrading.** Reason: it never writes XML, and a build script must not rewrite a project behind the user's back.
- **Skip names starting with `.`.** Reason: this covers `.git`, `.vs`, `.idea`, `.vscode` and `.DS_Store`. It is deterministic on every OS and in every clone, unlike the Hidden attribute, which git does not version. Rejected:
  - the Hidden attribute;
  - an ignore file such as `.tileshopignore` (more format surface; it can be added later without a break);
  - skipping only `.git`.
- **Ignore XML with a foreign root; fail on XML that is not well formed.** A file with a foreign root cannot be a TileShop resource, because a newer resource type comes with a newer version, which is rejected first. A file that does not parse may be a resource damaged by a merge conflict (`<<<<<<<` markers), and dropping it would silently lose it and, on the next write, every reference to it. Rejected:
  - failing on every non-resource file (today's behavior, which blocks git and IDE use);
  - ignoring every file that fails (it hides damaged resources).
- **Refuse names starting with `.`.** Reason: such a resource or folder would vanish on the next open. This adds a rule to the `ResourceName` rule from [resource-name-validation.md](resource-name-validation.md), which currently lists `".hidden"` as accepted. Existing dot-named resources are skipped on load. If something references them, the load fails, naming the key.
- **Unknown color source types throw.** Reason: `IColorSource` is public, and an unknown type is a programming error (ARCHITECTURE: exceptions for programming errors). A loop or a silently empty entry is not acceptable. Rejected: sealing `IColorSource` behind an abstract base (more churn for no user benefit).
- **Stored mirror and rotation mean rotate, then mirror.** The rotate tool turns the element as shown on screen. For a horizontal or vertical mirror `M`, `M∘Q∘M = Q⁻¹`, so the requested quarter turn is reversed. Mirror `Both` equals a turn and commutes with rotation, so it is unchanged. Reason: the meaning stored in 0.9 is already consistent in render and encode, and only the edit was wrong. Rejected:
  - Changing render to mirror-then-rotate. That would change how every saved element that is both mirrored and rotated renders, and needs a migration, for no gain.
  - Normalizing to the 8 distinct transforms, for example `both` → `turn`. Files would be rewritten with no visible change, and `mirror="both"` is what people write by hand.

## Spec changes

**LIB-PROJECT-FORMAT**

- **-004** changed: When reading, every subdirectory of the base directory whose name does not start with `.`, and that is not inside such a directory, shall become a folder, including empty directories and directories holding only non-resource files.
- **-005** changed: When reading, every `*.xml` file under the base directory shall be read except:
  - the project file;
  - a file whose name starts with `.`;
  - a file inside a directory skipped by -004.

  A file whose root element is `project`, or is not `datafile`, `palette` or `arranger`, shall be ignored.
- **-006** changed: The writer shall write the current format version, `version="1.0"`, on the project element, and write `root` only when the project has a non-empty root.
- **-007** changed: The reader shall read `version` before validating the project file and parse it as `major.minor` independently of the current culture. If it is missing or does not parse, then the load shall fail naming the project file.
- **-026** changed: The reader shall validate every resource file with a recognized root element against `ResourceSchema.xsd`, after migration, and stop at the first file that fails, reporting its line numbers.
- **-027** changed: If the project file's root element is not `project`, then the load shall fail.
- **-036** changed: The writer shall fail on a palette without a data file. Mapping a palette whose color source is not a file, project native or project foreign source shall throw `NotSupportedException` naming the type.
- Added (Reading):
  - If a `*.xml` file read under -005 is not well-formed XML, then the load shall fail naming the file and line.
- Added (Version and migration, new group):
  - If the project's format version is newer than the reader's, then the load shall fail before any resource is read, naming the project file, its version and the newest version the reader supports.
  - When the project's format version is older than the reader's, the reader shall apply each migration step from that version up to the current one to the project file and every resource file in memory, before schema validation. The loaded project shall report the version it was read at.
  - If no chain of migration steps leads from an older version to the current one, then the load shall fail naming the version.
  - While a project's format version is older than current, every write other than an upgrade shall throw `InvalidOperationException`.
  - When a project is upgraded, the writer shall write every resource file and the project file at the current version in one write-ahead-log transaction, then set the project's format version to current. If the transaction fails, then no file, persisted model or format version shall change.
- Edge cases:
  - Replace "Any non-resource XML file under the project directory fails the load at schema validation." with: "XML with a foreign root is ignored; XML that is not well formed fails the load, since it may be a resource damaged by a merge conflict."
  - Add: "A resource or folder whose name starts with `.` (possible only from outside TileShop) is not loaded; references to it fail the load."
- Decisions:
  - Rewrite "References are tree path keys" with the analysis summary and rejected alternatives from Decisions above (stable keys, GUIDs, id plus path hint, name-match fallback), and the note that keys can arrive later through migration.
  - Add "The version lives on the project file only", "Reject every newer version", "Migrate in memory, and write only on an explicit upgrade", "Skip names starting with `.`" and "Ignore XML with a foreign root; fail on XML that is not well formed".
- Non-goals: replace "Migrating older formats; no migration hook exists." with "Writing or downgrading to older formats."
- Open items: delete the Version bullet, the Stable keys bullet and the Scattered color source bullet.
- `types`: add `ProjectFormat` and `IProjectMigration`.

**LIB-PALETTES**

- **-009** struck: Removed. The scattered color source was an empty stub that nothing created and the schema never stored. It was removed from the 1.0 format.
- Added: If a color source is not a file, project native or project foreign source, then loading or storing its colors shall throw `NotSupportedException` naming its type.
- `types`: remove `ScatteredColorSource`. Open items: delete the scattered color source bullet.

**LIB-PROJECT-SERVICE**

- Added:
  - When a project in an older format is opened, the service shall open it with its read format version, write nothing, and raise `ProjectOpened` as for any project.
  - When asked to upgrade a project, the service shall have the writer upgrade it (LIB-PROJECT-FORMAT) and return the result. Upgrading a project already at the current version shall succeed without writing.
- With resource-name-validation's "Resource names" group (LIB-PROJECT-TREE), add a rule: a name starting with `.` is rejected, because the reader skips it (LIB-PROJECT-FORMAT-004, -005).

**UI-PROJECT-TREE**

- Added:
  - When an opened project reports an older format version, the tree shall ask "Upgrade Project Format" with the name and both versions, and the actions Upgrade and Cancel, before any other open alert. Cancel shall close the project.
  - If the upgrade fails, then the tree shall alert "Upgrade Failed" with the reason and close the project.

**CLI-COMMANDS**

- Added: When a project in an older format is opened, the CLI shall read it without upgrading its files. A project in a newer format fails under CLI-COMMANDS-009 (-5).

**LIB-ARRANGERS**

- **-012** changed: When an element is rotated, the rotation shall turn it as displayed. The requested quarter turn composes with its rotation, reversed in direction while the element is mirrored on exactly one axis: left then right cancels, and two lefts are a turn. The call shall fail for an empty cell, a non-square element or a location past the grid.
- Decisions: add "Stored mirror and rotation mean rotate, then mirror" (see Decisions above; it names LIB-IMAGES-006 and -015 as the render and encode order).
- Open items: delete the bullet on rotating a mirrored element.

**UI-ARRANGING**

- **-041** changed: When the user clicks a square element with a rotate tool, the editor shall turn it as displayed in the tool's direction, whatever its mirror, record history and mark the editor modified. A non-square element is left unchanged and not outlined.

**UI-PALETTE-EDITOR**

- `types`: remove `ScatteredColorSourceModel`.
- Edge cases: delete "A palette with a scattered color source cannot be opened".
- Open items: delete "Palettes with a scattered color source throw on open", and drop "`ScatteredColorSourceModel` is unused" from the dead-code bullet.

**ARCHITECTURE.md**

- §6 "Path keys, not ids": unchanged if path keys are kept. Add: "A rename outside TileShop leaves references that fail the load, naming the key." If stable keys are chosen, rewrite it as "Stable keys, not paths", with the drift and duplicate-key costs.
- §6 gains "**One format version, migrated in memory.**" Newer projects are refused, older ones migrate in memory, and files are rewritten only by an explicit upgrade.

## Tasks

Lands after [project-format-round-trip.md](project-format-round-trip.md), whose `XmlProjectReaderTests` and `XmlProjectRoundTripTests` this change extends, and after [resource-name-validation.md](resource-name-validation.md), whose `ResourceName` rule gains the dot rule.

1. **Version and migration hook.**
   - Code:
     - `ProjectFormat.Current` (`new Version(1, 0)`) replaces the writer's private version constant.
     - `ImageProjectModel.Version` and `ImageProject.FormatVersion` become `Version`.
     - Change the schema `version` type to the pattern `[0-9]+\.[0-9]+`.
     - The reader reads `version` before validation, rejects newer versions, and runs the migration chain over the in-memory document set. `IProjectMigration` (`From`, `To`, `Migrate(documents)`), plus a 0.9 → 1.0 step that only sets the version.
   - Tests:
     - `ProjectFormatVersionTests`:
       - `NewerMinor_FailsNamingVersions`
       - `NewerMajor_FailsBeforeReadingResources` (a resource file that fails the schema is not reported)
       - `NewerWithUnknownAttributes_ReportsVersionNotSchema`
       - `MissingOrUnparseableVersion_FailsNamingFile`
       - `Parse_IsCultureInvariant` (theory over fr-FR, de-DE, invariant; `0.9` reads as 0.9)
       - `Ordering_1_10_IsNewerThan_1_9`
       - `OlderVersion_RunsEveryStepInOrder` (fake steps 0.8 → 0.9 → 1.0 record their calls)
       - `OlderVersion_StepRunsBeforeValidation` (a fake step renames an attribute that the current schema requires)
       - `NoMigrationPath_FailsNamingVersion`
       - `Writer_WritesCurrentVersion`
     - `XmlProjectRoundTripTests`: `Sample09_LoadsThroughMigration` (each `_xmlprojectsamples` zip reports `FormatVersion` 0.9 and the same resources as before).
2. **Upgrade.**
   - Code:
     - The writer throws on any write while `FormatVersion < Current`.
     - `IProjectWriter.UpgradeAsync` writes every resource and the project file in one transaction.
     - `IProjectService.UpgradeProjectAsync`.
     - Adapt `XmlProjectRoundTripTests` to upgrade 0.9 fixtures before their write assertions.
   - Tests:
     - `ProjectUpgradeTests`:
       - `Open09_WritesNothing` (file bytes and timestamps unchanged)
       - `WriteBeforeUpgrade_Throws` (save, save resource, add, rename, move)
       - `Upgrade_RewritesEveryFileAtCurrentVersion`
       - `Upgrade_ThenReopen_NoMigration`
       - `Upgrade_TransactionFails_FilesAndVersionUnchanged` (a read-only target)
       - `Upgrade_AtCurrentVersion_WritesNothing`
     - `CliApplicationTests` (from cli-1-0.md): `OlderFormat_ExportsWithoutWriting`, `NewerFormat_Exits5`.
3. **Upgrade prompt.**
   - Code: `ProjectTreeViewModel` asks after open and before the missing-data-files alert; Upgrade calls the service; Cancel and failure close the project.
   - Tests: `ProjectTreeViewModelTests` (or the nearest existing ViewModel test class), with a fake interaction service: `OpenOlderFormat_Cancel_ClosesProject`, `OpenOlderFormat_Upgrade_UpgradesThenShowsMissingFiles`, `UpgradeFails_AlertsAndCloses`.
   - Verify in the running app with DevTools: Load FF2 (a 0.9 project) shows the prompt overlay; Upgrade opens the project; Load FF2 again shows no prompt. Restore the FF2 test project from its 0.9 copy afterwards (`D:\ImageMagitekTest\FF2`, see Open questions).
4. **Directory scanning.**
   - Code:
     - A recursive enumeration that skips dot-prefixed names.
     - Check the root element before schema validation.
     - Fail on XML that is not well formed, naming the file.
     - Add the dot rule to `ResourceName`.
   - Tests:
     - `XmlProjectReaderTests`:
       - `DotDirectory_NotAFolderAndNotScanned` (`.git/objects/ab`, `.idea/workspace.xml` with a foreign root)
       - `DotPrefixedXmlFile_Ignored`
       - `ForeignRootXml_Ignored` (a `notes.xml` with `<notes/>`)
       - `MalformedXml_FailsNamingFileAndLine` (an arranger with merge-conflict markers)
       - `RecognizedRootSchemaInvalid_Fails`
       - `EmptyDirectory_BecomesFolder`
       - `ReferenceIntoDotDirectory_FailsNamingKey`
     - `ResourceNameTests`: move `".hidden"` from accepted to rejected.
     - `ProjectServiceTests`: `AddResource_LeadingDot_FailsWithoutWriting`, `CreateNewFolder_LeadingDot_Fails`, `RenameResource_LeadingDot_Fails`.
5. **Remove the scattered color source.**
   - Code:
     - Delete `ScatteredColorSource`, `ScatteredColorSourceModel` and their branches.
     - Delete the reader's `scatteredcolor`/`import`/`export` branches and the commented-out mapping.
     - Unknown source types throw in `MapToModel`, `LoadColors` and `StoreColors`.
   - Tests:
     - `SerializationMapperTests`: `MapToModel_UnknownColorSource_ThrowsNamingType` (a test-only `IColorSource`; the call returns, it does not hang).
     - `PaletteTests`: `LoadColors_UnknownColorSource_Throws`, `StoreColors_UnknownColorSource_Throws`.
6. **Rotation of mirrored elements.**
   - Code: `TryRotateElement` reverses Left and Right while `Mirror` is `Horizontal` or `Vertical`.
   - Tests:
     - `ElementTransformTests` (new, `ArrangerTests/`; an asymmetric 8×8 indexed tile):
       - `Rotate_TurnsDisplayedPixels` (theory over every mirror × every rotation × Left/Right/Turn: the rendered pixels after the call equal `RotateArray2D` of the rendered pixels before)
       - `Mirror_FlipsDisplayedPixels` (same theory for mirrors)
       - `HMirrorThenRotateLeft_TurnsCounterClockwise`
       - `StoredMeaning_RotateThenMirror` (render of a stored `(horizontal, left)` equals mirror(rotate-left(decoded)))
     - `GraphicsEditHistoryTests.Rotate_UndoRedo` stays green.
   - Manual (canvas clicks are pointer input): in the Adult Rydia Map arranger, Mirror Horizontal then Rotate Left on one element; it turns counter-clockwise on screen.
7. **References.** If open question 1 keeps path keys, there is no code task beyond project-format-round-trip.md. If stable keys are chosen, this task becomes:
   - the `key` attribute, model and node property;
   - assignment in `AddResource`, `CreateNewProjectWithExistingFileAsync` and the 0.9 → 1.0 step;
   - the key index in `ProjectTreeBuilder`, and both resource maps writing keys;
   - duplicate-key failure, and global-palette disambiguation.

   With tests in `XmlProjectReaderTests` and `ProjectServiceTests` (rename and move write one file; a duplicate key fails naming both files).
8. **Specs and backlog.**
   - Apply the spec changes above, with real test names on each `Tests:` line, and update ARCHITECTURE §6.
   - Delete these lines from `docs/BACKLOG.md`:
     - the 1.0 stable-keys, versioning and scattered-source items;
     - the `MapToModel` loop item;
     - the palette-editor scattered-source item;
     - the mirrored-rotation item;
     - the culture item;
     - the malformed/foreign XML item.
   - Trim `ScatteredColorSourceModel` from the UI-PALETTE-EDITOR dead-code line, and trim the `scatteredcolor`/`import`/`export` branches and the commented-out mapping from the LIB-PROJECT-FORMAT dead-code line (delete that line if nothing remains).
   - Delete this proposal. Run `dotnet test ImageMagitek.UnitTests`.

## Open questions

1. Path keys or stable resource keys for references? Recommendation: keep path keys. In-app renames and moves already rewrite every referencing file in one transaction, with no code specific to references. Once project-format-round-trip.md lands, hand renames fail loudly, naming the key, instead of losing references. Stable keys add a second identity that drifts from file names, duplicate when a file is copied, and need assignment, indexing and duplicate detection. With the migration hook, keys can still be introduced later as a format version with a derived-key step. If keys are chosen, ARCHITECTURE §6 "Path keys, not ids" is rewritten, and task 7 and its spec changes replace the path-key Decision.
2. Should the upgrade keep a copy of the 0.9 files (for example `<project>.0.9.zip` beside the project file)? Recommendation: no. The prompt says older versions cannot open the upgraded project, so users can copy it first; git users have history; and the write is one transaction. The debug "Load FF2" project at `D:\ImageMagitekTest\FF2` needs a 0.9 copy kept by hand, so the prompt can be checked again.
3. Cancel on the upgrade prompt: close the project (specified), or open it with every write refused? Recommendation: close. A project where save, rename, move and add all fail is a screen full of controls that do nothing (ARCHITECTURE §1).
