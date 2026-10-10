# Freeze the 1.0 project format

## Why

1.0 users keep projects for years and often keep them in git. After 1.0, any change to what is stored on disk, or to what a stored value means, breaks their projects. Everything still open about the format is settled here, before the freeze, so later changes are additive or come with a format version that older readers refuse. Backlog items, each checked against the code:

- **1.0** [LIB-PROJECT-FORMAT] Stable resource keys or path keys. **Decided: path keys.** The analysis is under Decisions.
- **1.0** [LIB-PROJECT-FORMAT] Schema versioning. Confirmed: `XmlProjectWriter` writes `Version => "0.9"`, `XmlProjectReader` parses it into `ImageProjectModel.Version` and never compares it, and `ImageProjectModel.ResourceEquals` ignores `Version`. Only the project file carries a version; resource files have none. **Decided: no upgrade or migration path for 1.0**, because there are no projects in the wild to carry forward. 1.0 needs only a version that newer formats can be told apart by.
- **1.0** [LIB-PROJECT-FORMAT] The scattered color source. **Decided: keep and implement it**, in [scattered-color-sources](scattered-color-sources.md), which lands before this change so the `scatteredcolor` element is part of the frozen format. That proposal also fixes the `MapToModel` loop and the palette editor throw.
- (promoted) [LIB-PROJECT-FORMAT] A malformed or foreign `*.xml` anywhere under the project directory fails the whole load, and every other directory becomes a folder. Confirmed: `Directory.GetDirectories(..., AllDirectories)` and `GetFiles("*.xml", AllDirectories)` scan everything, and every file is validated against the schema before its root element is checked. Concretely, `.git` and all its subdirectories become folders, and Rider's `.idea/*.xml` or Visual Studio's `.vs` files fail the load. 1.0: romhackers keep projects in git, and which files the reader scans is part of the format contract, so it must be fixed before the freeze.
- (promoted) [LIB-ARRANGERS] Rotating a mirrored element turns it the opposite way on screen. Confirmed: render shows `M(R(decoded))` (LIB-IMAGES-006), and `TryRotateElement` composes the requested turn into `R` whatever the mirror is. Because `H∘L = R∘H`, Rotate Left on a horizontally mirrored element turns it clockwise. The fix belongs in the edit operation, not in the stored meaning. Stored `(mirror, rotation)` already has one consistent meaning (rotate, then mirror) in render (LIB-IMAGES-006) and encode (LIB-IMAGES-015). Fixing `TryRotateElement` changes how no saved project renders. It is in this change to pin that meaning before the freeze.
- (promoted) [LIB-PROJECT-FORMAT] `version` is parsed with the current culture. Confirmed, and worse than the backlog says. `decimal.Parse("0.9")` throws `FormatException` under fr-FR, so every project fails to open on a French-locale machine. Under de-DE it returns `9`, which would read as a newer version once versions are compared. 1.0: every project fails to open for a large group of users.

## What

**Version.** The writer writes `version="1.0"`, and `ProjectFormat.Current` is the one place that defines it. The reader reads `version` before validating the schema and parses it as `major.minor` with `System.Version`, which does not depend on culture.

- A project newer than `Current` fails to load before any resource is read: "'FF2project.xml' uses project format 1.1; this version of TileShop reads format 1.0 and older."
- A project at `0.9` or `1.0` loads as format 1.0. The 1.0 format is 0.9 plus the additive `scatteredcolor` element, so a 0.9 file is already a valid 1.0 file. Its project file is written as `1.0` the next time the project file is written, with no prompt.
- A project older than `0.9`, or a version that does not parse, fails the load naming the project file and the version.
- There is no migration hook and no upgrade command. The first format change that needs one adds it, together with the version that requires it.

**Directory scanning.** The reader skips every directory and file whose name starts with `.`, together with everything under such a directory. Other directories still become folders, including empty ones. Each `*.xml` file it finds is handled as follows:

- A file whose root element is not `project`, `datafile`, `palette` or `arranger` is ignored.
- Another project file is ignored, as today.
- A file that is not well-formed XML fails the load, naming the file and line.
- A file with a recognized root that fails the schema fails the load, as today.

Add, create folder and rename refuse a name that starts with `.`.

**Mirror and rotation.** The stored `(mirror, rotation)` keeps its meaning, decode → rotate → mirror. `TryRotateElement` turns the element as displayed: when the element is mirrored on exactly one axis, it reverses the requested direction before composing. `TryMirrorElement` already composes in display space and is unchanged.

**References.** References stay path keys. Since the round-trip change landed, an unresolved key fails the load naming the key (LIB-PROJECT-FORMAT-022, -023), so a hand rename or `git mv` fails loudly instead of losing references silently.

**What does not change.** The file layout, every element and attribute name, path-key syntax, the write-ahead log, the render and encode order, and the in-memory model of references (object references). Every 0.9 project that loads today, has no dot-prefixed folder holding resources, and has no foreign XML loads with identical content.

## Decisions

- **References stay tree path keys.** Analysis against the code:
  - **Where key strings exist.** In memory, elements and palettes hold object references, and key strings appear only at the file boundary: `XmlProjectWriter.CreateResourceMap` (and its duplicate, `ProjectService.CreateResourceMap`) on write, and three `ProjectTree.TryGetItem(pathKey)` lookups in `ProjectTreeBuilder` on read.
  - **Renames and moves are already correct.** `RenameResourceAsync` and `MoveNodeAsync` change the tree, then call `WriteProjectAsync`, whose model diff (LIB-PROJECT-FORMAT-032) rewrites every referencing file in one write-ahead-log transaction.
  - **The cost is diff noise.** In the FF2 sample, renaming the data file `FF2` rewrites 114 of 116 resource files. That costs diff noise and merge conflicts, not correctness.
  - **Hand renames fail loudly.** A rename done outside TileShop fails the load, naming the key.
  - **Stable keys would cost more than they give.** They need a `key` attribute on every file, assignment at both creation sites, a key index, duplicate-key detection (a file copied in Explorer duplicates its key), and a rule separating keys from global palette names. Keys also drift from names: after a rename, `palette="Pal0"` points at `Title Palette.xml`. That costs the hand-editability that is the reason path keys were chosen (ARCHITECTURE §6).
  - **Reversible.** Stable keys can arrive later as a new format version, with a step that derives each key from its path key.
  - **Rejected.** Stable keys (above); GUID keys (cannot be edited by hand); an id plus a path hint (stale hints mislead); a fallback to a unique name match when a path fails (ambiguous, and it hides moves).
- **No migration hook or upgrade path in 1.0.** There are no projects in the wild to carry forward, and 0.9 files are valid 1.0 files. Reason: a migration chain, an upgrade transaction and an upgrade prompt are code with no caller until a format change needs them. When one does, the reader already refuses newer versions, so older TileShop builds fail cleanly on the new format. Rejected: building the hook now (speculative), and keeping a backup copy on upgrade (no upgrade exists).
- **The version lives on the project file only.** A project has one format version, and every file under it is read as that version. Reason: per-file versions would allow mixed projects, and a future step that rewrites references needs to see every file. Rejected: a `version` attribute on each resource file.
- **`major.minor` parsed as `System.Version`.** Reason: `Version.Parse` does not depend on culture, and it orders `1.10` after `1.9`; `decimal` treats `1.10` as `1.1`. The schema type becomes a pattern, digits `.` digits. Rejected: `decimal.Parse` with `InvariantCulture`, which fixes the culture bug but keeps the wrong ordering.
- **Reject every newer version, major or minor.** Reason: a 1.0 reader cannot keep elements it does not know, and its next save would drop them, which loses work. Rejected: accepting newer minor versions as "additive".
- **The version check runs before schema validation.** Reason: a newer project file may carry attributes this schema rejects. The user must see "newer format", not a schema error.
- **Skip names starting with `.`.** Reason: this covers `.git`, `.vs`, `.idea`, `.vscode` and `.DS_Store`. It is deterministic on every OS and in every clone, unlike the Hidden attribute, which git does not version. Rejected:
  - the Hidden attribute;
  - an ignore file such as `.tileshopignore` (more format surface; it can be added later without a break);
  - skipping only `.git`.
- **Ignore XML with a foreign root; fail on XML that is not well formed.** A file with a foreign root cannot be a TileShop resource, because a newer resource type comes with a newer version, which is rejected first. A file that does not parse may be a resource damaged by a merge conflict (`<<<<<<<` markers), and dropping it would silently lose it and, on the next write, every reference to it. Rejected:
  - failing on every non-resource file (today's behavior, which blocks git and IDE use);
  - ignoring every file that fails (it hides damaged resources).
- **Refuse names starting with `.`.** Reason: such a resource or folder would vanish on the next open. This adds a rule to the `ResourceName` rule (LIB-PROJECT-TREE-030), which currently accepts `".hidden"`. Existing dot-named resources are skipped on load. If something references them, the load fails, naming the key.
- **Stored mirror and rotation mean rotate, then mirror.** The rotate tool turns the element as shown on screen. For a horizontal or vertical mirror `M`, `M∘Q∘M = Q⁻¹`, so the requested quarter turn is reversed. Mirror `Both` equals a turn and commutes with rotation, so it is unchanged. Reason: the meaning stored in 0.9 is already consistent in render and encode, and only the edit was wrong. Rejected:
  - Changing render to mirror-then-rotate. That would change how every saved element that is both mirrored and rotated renders, for no gain.
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
- **-007** changed: The reader shall read `version` before validating the project file and parse it as `major.minor` independently of the current culture. If it is missing, does not parse, or is older than 0.9, then the load shall fail naming the project file and the version.
- **-027** changed: If the project file's root element is not `project`, then the load shall fail.
- Added (Reading):
  - If a `*.xml` file read under -005 is not well-formed XML, then the load shall fail naming the file and line.
- Added (Version, new group):
  - If the project's format version is newer than the reader's, then the load shall fail before any resource is read, naming the project file, its version and the newest version the reader supports.
  - When the project's format version is 0.9, the reader shall load it as the current format.
- Edge cases:
  - Replace "Any non-resource XML file under the project directory fails the load at schema validation." with: "XML with a foreign root is ignored; XML that is not well formed fails the load, since it may be a resource damaged by a merge conflict."
  - Add: "A resource or folder whose name starts with `.` (possible only from outside TileShop) is not loaded; references to it fail the load."
- Decisions:
  - Rewrite "References are tree path keys" with the analysis summary and rejected alternatives from Decisions above.
  - Add "No migration hook or upgrade path in 1.0", "The version lives on the project file only", "`major.minor` parsed as `System.Version`", "Reject every newer version", "The version check runs before schema validation", "Skip names starting with `.`" and "Ignore XML with a foreign root; fail on XML that is not well formed".
- Non-goals: replace "Migrating older formats; no migration hook exists." with "Migrating or upgrading older formats, and writing older formats; 0.9 reads as 1.0, and a migration hook arrives with the first format change that needs one."
- Open items: delete the Version bullet and the Stable keys bullet.
- `types`: add `ProjectFormat`.

**LIB-PROJECT-SERVICE**

- In the "Resource names" group (LIB-PROJECT-TREE-030), add a rule: a name starting with `.` is rejected, because the reader skips it (LIB-PROJECT-FORMAT-004, -005).

**CLI-COMMANDS**

- Added: A project in a newer format fails under CLI-COMMANDS-009 (-5).

**LIB-ARRANGERS**

- **-012** changed: When an element is rotated, the rotation shall turn it as displayed. The requested quarter turn composes with its rotation, reversed in direction while the element is mirrored on exactly one axis: left then right cancels, and two lefts are a turn. The call shall fail for an empty cell, a non-square element or a location past the grid.
- Decisions: add "Stored mirror and rotation mean rotate, then mirror" (see Decisions above; it names LIB-IMAGES-006 and -015 as the render and encode order).
- Open items: delete the bullet on rotating a mirrored element.

**UI-ARRANGING**

- **-041** changed: When the user clicks a square element with a rotate tool, the editor shall turn it as displayed in the tool's direction, whatever its mirror, record history and mark the editor modified. A non-square element is left unchanged and not outlined.

**ARCHITECTURE.md**

- §6 "Path keys, not ids": add "A rename outside TileShop leaves references that fail the load, naming the key."
- §6 gains "**One format version, newer refused.**" The project file carries the format version. A reader refuses newer versions and reads 0.9 as 1.0. There is no migration until a format change needs one.

## Tasks

Lands after [scattered-color-sources](scattered-color-sources.md), so the frozen schema includes `scatteredcolor`.

1. **Version.**
   - Code:
     - `ProjectFormat.Current` (`new Version(1, 0)`) and `ProjectFormat.Oldest` (`new Version(0, 9)`) replace the writer's private version constant.
     - `ImageProjectModel.Version` becomes `Version`.
     - Change the schema `version` type to the pattern `[0-9]+\.[0-9]+`.
     - The reader reads `version` before validation and rejects newer, older and unparseable versions.
   - Tests:
     - `ProjectFormatVersionTests` (new):
       - `NewerMinor_FailsNamingVersions`
       - `NewerMajor_FailsBeforeReadingResources` (a resource file that fails the schema is not reported)
       - `NewerWithUnknownAttributes_ReportsVersionNotSchema`
       - `MissingOrUnparseableVersion_FailsNamingFile`
       - `OlderThan09_FailsNamingVersion`
       - `Parse_IsCultureInvariant` (theory over fr-FR, de-DE and invariant; `0.9` reads as 0.9)
       - `Ordering_1_10_IsNewerThan_1_9`
       - `Version09_LoadsAsCurrent`
       - `Writer_WritesCurrentVersion`
     - `XmlProjectRoundTripTests`: the `_xmlprojectsamples` zips (0.9) still load with the same resources.
     - `CliApplicationTests`: `NewerFormat_Exits5`.
2. **Directory scanning.**
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
3. **Rotation of mirrored elements.**
   - Code: `TryRotateElement` reverses Left and Right while `Mirror` is `Horizontal` or `Vertical`.
   - Tests:
     - `ElementTransformTests` (new, `ArrangerTests/`; an asymmetric 8×8 indexed tile):
       - `Rotate_TurnsDisplayedPixels` (theory over every mirror × every rotation × Left/Right/Turn: the rendered pixels after the call equal `RotateArray2D` of the rendered pixels before)
       - `Mirror_FlipsDisplayedPixels` (same theory for mirrors)
       - `HMirrorThenRotateLeft_TurnsCounterClockwise`
       - `StoredMeaning_RotateThenMirror` (render of a stored `(horizontal, left)` equals mirror(rotate-left(decoded)))
     - `GraphicsEditHistoryTests.Rotate_UndoRedo` stays green.
   - Manual (canvas clicks are pointer input): in the Adult Rydia Map arranger, Mirror Horizontal then Rotate Left on one element; it turns counter-clockwise on screen.
4. **Specs and backlog.**
   - Apply the spec changes above, with real test names on each `Tests:` line, and update ARCHITECTURE §6.
   - Delete these lines from `docs/BACKLOG.md`:
     - the 1.0 stable-keys and versioning items;
     - the mirrored-rotation item;
     - the culture item;
     - the malformed/foreign XML item.
   - Delete this proposal. Run `dotnet test ImageMagitek.UnitTests`.

## Open questions

None.
