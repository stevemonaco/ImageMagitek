# One rule for resource names

## Why

A resource's name is its file or directory name (LIB-PROJECT-FORMAT-001, -002) and its path key, yet nothing checks it. Backlog items covered:

- **1.0** [LIB-PROJECT-SERVICE, UI-PROJECT-TREE] Nothing rejects empty names, `/` or invalid file-name characters on add, create-folder, rename or new arranger from selection; name checks are case-sensitive while Windows paths are not. Confirmed: `AddResource`, `CreateNewFolder` and `RenameResourceAsync` check only `ContainsChildNode` (ordinal, LIB-PROJECT-TREE-015), and `MoveNodeAsync`'s check is the same. On Windows, adding `Data` beside `data` passes the check and `File.WriteAllText` overwrites `data.xml`; on Linux it succeeds and yields a project that cannot be checked out on Windows or macOS. A name with `/` breaks its own path key (LIB-PROJECT-TREE edge case). Found while reading: a root-level arranger, palette or data file named like the project writes `<name>.xml` over the project file, since both live in the base directory (`ResourceFileLocator`); a folder named `x.xml` collides with a sibling resource `x`.
- (promoted) [UI-PROJECT-TREE] Add New Palette and Add New Scattered Arranger compute `CanAdd`/`ValidationErrors` but never gate Accept. Confirmed: neither overrides `CanAccept`, and `AddScatteredArrangerViewModel.ValidateModel` has no caller. For 1.0: a dialog that shows an error and accepts anyway is a control that silently does nothing.
- (promoted) [UI-PROJECT-TREE] Accepting Rename... with the name unchanged fails with "already contains a node named X". Confirmed: `RenameResourceAsync` finds the node itself as the sibling. For 1.0: the most common way to dismiss the dialog reports an error.
- (promoted) [UI-PROJECT-TREE] Add New Folder... fails when "New Folder" exists though the doc comment says the name is augmented. Confirmed: the `ContainsChildNode` check runs before `FindFirstNewChildResourceName`, which is unreachable. For 1.0: the second use of a menu item fails.
- (promoted) [UI-PROJECT-TREE] New Project from Existing File... does not detect a file already open as a data file in a project. Confirmed: `NewProjectFromFile` only looks for a standalone root, and `CreateNewProjectWithExistingFileAsync`'s "already exists" check compares tree names to file paths. For 1.0: two projects then hold separate `FileDataSource`s on one ROM, and saves from one are invisible to the other's editors and palettes (risk of overwriting bytes with stale data).

## What

A library type `ResourceName` holds the rule. Every service operation that introduces a name (add, create folder, rename, create project, create project from file) refuses a name that breaks it, and the move check applies its sibling part, all without touching disk. `IProjectService` gains `CanAddResource(parent, name, isFolder)` and `CanRenameResource(node, newName)`, which run the same checks as the operations; the dialogs call them on every edit, show the first failure under the name box, and keep Accept disabled while it fails.

The rule, checked in this order, with the first failure as the message:

1. Not empty.
2. No leading or trailing whitespace; no trailing `.`.
3. None of `< > : " / \ | ? *` and no control characters (U+0000–U+001F).
4. The part before the first `.` is not a Windows reserved device name, ignoring case: `CON`, `PRN`, `AUX`, `NUL`, `COM0`–`COM9`, `LPT0`–`LPT9`, `COM¹²³`, `LPT¹²³`.
5. At most 100 characters and at most 240 bytes as UTF-8.
6. A folder name does not end with `.xml`, ignoring case.
7. Unique among the parent's children, comparing NFC-normalized names with `OrdinalIgnoreCase`, ignoring the node being renamed or moved.
8. A non-folder resource directly under the project root does not match the project's name (same comparison), and the project is not renamed to a root-level non-folder resource's name.

Rename to the identical name (ordinal) succeeds with no write and no `Renamed` change; a case-only rename still works (LIB-PROJECT-SERVICE-036).

UI: Rename..., Add New Folder... and "Add as New Scattered Arranger..." use one name dialog (`NameResourceViewModel`, with a title, an initial name and a validator). Add New Folder... opens it prefilled with the first free of "New Folder", "New Folder (2)", "New Folder (3)"…. Add New Palette and Add New Scattered Arranger validate through the service instead of a list of sibling names. Add Existing Data File... whose file name is not a valid new name opens the name dialog prefilled with the file name and the error, instead of alerting. New Project from Existing File... on a file held by an open project expands to and selects that data file node and alerts "'<file>' is already in project '<project>'"; the service refuses the same case.

Unchanged: projects load with whatever names are on disk; tree lookups and path keys stay case-sensitive; the full path length is not checked (an over-long path fails with the OS's message through the existing failure results); data file locations on disk are not validated, only node names.

## Decisions

- **One rule in the library, used by every path.** `ResourceName` (ImageMagitek, owned by LIB-PROJECT-TREE) validates a name; `ProjectService` adds the sibling and project-name checks and is the only enforcer; dialogs ask the service. Reason: the backlog defects come from the UI holding a partial copy of the check (or none). Rejected: validation only in dialogs (the CLI, messages and future callers bypass it); validation in `ResourceNode`/`ProjectTree` attach (the reader must keep loading existing names, and throwing from attach turns user errors into exceptions).
- **Cross-platform rule on every OS.** The Windows character set, reserved names and trailing dot/space apply on Linux and macOS too. Reason: projects are shared between romhackers on different OSes and kept in git; a name valid only on Linux makes a project that cannot be checked out elsewhere. The Windows set is a superset of the Linux (`/`, NUL) and macOS (`/`, `:`) sets. Rejected: `Path.GetInvalidFileNameChars()`, which differs per OS.
- **Case-insensitive uniqueness on every OS.** Same reason; NTFS and APFS are case-insensitive by default. NFC normalization covers APFS treating composed and decomposed accents as one name. Rejected: OS-dependent comparison.
- **Tree lookups stay case-sensitive.** LIB-PROJECT-TREE-015 is kept: path keys in project files resolve exactly, and changing the tree's comparer would change reference resolution in existing projects. Only the service's uniqueness check ignores case.
- **Length: 100 characters and 240 UTF-8 bytes.** `<name>.xml` must fit a 255-byte component on ext4/APFS (bytes) and 255 UTF-16 units on NTFS; 100 characters covers No-Intro ROM file names (often 60–80 characters) while 240 bytes catches long non-Latin names. Rejected: 255 characters (fails on Linux for non-ASCII); 60 characters (rejects real ROM names added as data files).
- **Reject, never sanitize.** Leading/trailing whitespace and bad characters are errors, not silently trimmed or replaced, so the name on disk is the name typed. Rejected: trimming on accept (the user sees one name and gets another).
- **Existing names load.** The reader does not apply the rule; a project with an on-disk name that breaks it opens, and the node keeps that name until renamed. Moving such a node checks only uniqueness at the target. Reason: the names exist on disk, so the OS that wrote them accepts them; refusing the load would lock users out of their work. Rejected: failing or warning on load.
- **The project file name is a sibling.** The project file and root-level resource files share the base directory, so rule 8 treats the project name as a reserved root-level name. Rejected: moving the project file (a format change for 1.0).
- **Folder names cannot end in `.xml`.** A folder `x.xml` and a resource `x` map to the same disk entry with different node names. Rejected: comparing disk entry names instead (harder to explain in an error).
- **Unchanged rename is a no-op success in the service.** So every caller gets it, not just the dialog. Rejected: disabling ✓ when unchanged (Enter on the prefilled dialog should just close it).
- **Add New Folder... asks for a name.** Its label has an ellipsis and users rename new folders immediately; the dialog prefills the first free "New Folder (n)" so Enter keeps the old one-step flow. `CreateNewFolder` stops augmenting and takes the name verbatim. Rejected: keeping augmentation in the service (a service that silently changes the requested name).
- **Derived names follow the rule.** Names taken from file names (Add Existing Data File..., New Project from Existing File..., New Empty Project...) are checked like typed ones. Add Existing Data File... falls back to the name dialog; the project-creation paths fail with the rule's message, since a project's name is its file name and the picker already chose it.
- **One data file, one project.** `CreateNewProjectWithExistingFileAsync` fails when any open project holds a `FileDataSource` at the same full path (ignoring case); the UI converts a standalone root before calling (UI-PROJECT-TREE-038), so that path is unaffected. Extends the "One `FileDataSource` per file" decision in UI-PROJECT-TREE.
- **Independent of stable keys.** If project-format-freeze adopts stable resource keys, names still name files, so this rule still applies; keys derived from names at creation would inherit it.

## Spec changes

**LIB-PROJECT-TREE**

- Add `ResourceName` to `types`. Add a "Resource names" group:
  - The name rule shall reject an empty name; a name with leading or trailing whitespace or a trailing `.`; a name containing `< > : " / \ | ? *` or a control character; a name whose part before the first `.` is a Windows reserved device name, ignoring case; a name over 100 characters or 240 UTF-8 bytes; and, for a folder, a name ending in `.xml` ignoring case. Each rejection shall carry a message naming the problem.
  - Two names shall count as the same sibling name when their NFC-normalized forms are equal ignoring case (ordinal).
- LIB-PROJECT-TREE-015: remove `(inherited)`; reason in Decisions ("Tree lookups stay case-sensitive").
- Non-goals: strike "Validating resource names" (the tree still does not enforce it; LIB-PROJECT-SERVICE does).
- Edge cases: "A child name containing `/` is accepted…" becomes "The tree accepts any child name, including `/`; LIB-PROJECT-SERVICE refuses new names that break the rule, but a hand-made project may still hold them."
- Open items: strike "Case-sensitive child names (LIB-PROJECT-TREE-015)…".

**LIB-PROJECT-SERVICE**

- 028 changed: If the parent is not in an open tree or cannot contain children, or the resource's name breaks the name rule, matches a sibling's name, or (directly under the project root) matches the project's name, then adding shall fail with the rule's message without writing.
- 031 changed: If the parent cannot contain children or the name breaks the name rule or matches a sibling's name, then creating a folder shall fail without writing; the service shall not choose another name.
- 032 changed: If the node is not in an open tree, or the new name breaks the name rule, matches a sibling's name other than the node's own, or (for a non-folder directly under the root) matches the project's name, or (for the root) matches a root-level non-folder resource's name, then rename shall fail without changes.
- 040 changed: "the target has a child of the same name" becomes "the target has a child whose name matches the node's (ignoring case), or the node is a non-folder resource, the target is the project root and the node's name matches the project's name".
- 009 changed: If the project file already exists, the data file does not, an open project already holds a data file at the same full path (ignoring case), or the project or data file name derived from the file names breaks the name rule, then creating a project from an existing data file shall fail without writing.
- Added: When a node is renamed to its current name (ordinal), rename shall succeed without writing and without a `Renamed` change.
- Added: If the name derived from the project file name breaks the name rule, then creating a new project shall fail without writing.
- Added: `CanAddResource` and `CanRenameResource` shall return the same failure the add, create-folder or rename operation would return for that name, without changing anything.
- Non-goals: strike "Validating resource names against file-system rules."
- Open items: strike the LIB-PROJECT-SERVICE-031 item (folder-name helper unreachable) and the LIB-PROJECT-SERVICE-028/032 item (case-sensitive checks, no character checks).
- Edge cases: unchanged; the "already-open check compares tree names to file paths" line concerns `CreateNewProject` (a separate backlog line).
- Decisions: add "One rule for names, enforced by the service" (from this proposal's first four decisions, with the project-file, `.xml` folder and existing-names points).

**LIB-PROJECT-FORMAT**

- Added: The reader shall load resource and folder names as they are on disk, including names the name rule (LIB-PROJECT-TREE) would refuse.
- Decisions: add "Existing names load" (pointer to LIB-PROJECT-SERVICE).

**UI-PROJECT-TREE**

- If the rename dialog is merged (Task 4), remove `RenameNodeViewModel.cs` and `RenameNodeView.axaml` from `sources` and `RenameNodeViewModel` from `types`.
- 070 changed: Add New Folder... shall open the name dialog titled "New Folder" prefilled with the first of "New Folder", "New Folder (2)", "New Folder (3)"… no sibling has (ignoring case), create the folder on accept and select it; on failure the tree shall alert "Folder Creation Error".
- 072 changed: If the chosen file's name is not a valid new name under the node, then Add Existing Data File... shall open the name dialog prefilled with the file name and showing the error, and add the file under the accepted name; Cancel adds nothing.
- 077 changed: While the name fails `CanAddResource`, the Add New Palette and Add New Scattered Arranger dialogs shall show the failure and keep Add disabled.
- 082 changed Tests: manual — make the parent directory read-only, then add an arranger (a duplicate name can no longer reach the service).
- 083 changed: …the tree shall ask for a name in the name dialog, gated by `CanAddResource` at the project root, create an arranger…
- 090 changed: Rename... (and Rename Project...) shall open the name dialog titled "Rename <name>" with the current name filled in and ✓ and x buttons; while the name fails `CanRenameResource`, it shall show the failure and keep ✓ disabled.
- 091 changed: When the rename is accepted, the tree shall rename the resource through the service; an unchanged name shall close the dialog with no alert; on failure it shall alert "Rename failed" with the reason.
- Added (after 037): If New Project from Existing File... picks a file held by an open project, then the tree shall expand to and select that data file node and alert "'<file>' is already in project '<project>'", creating nothing.
- Edge cases: strike "Add New Folder... in a parent that already has…", "Accepting Rename... without changing the name fails…", "Add New Palette's and Add New Scattered Arranger's validation does not block…", "New Project from Existing File... on a file already in an open project is not detected…".
- Open items: strike "Add dialogs compute `CanAdd`…", "Rename with an unchanged name…", "'Add New Folder...' has an ellipsis but opens no dialog…"; reduce the `NameResourceViewModel` item to "the arranger is always added at the project root, not near the source."
- Decisions: add "Dialogs ask the service" (validators are `CanAddResource`/`CanRenameResource`, not sibling-name lists) and "Add New Folder... asks for a name".

## Tasks

1. **Name rule.** `ResourceName.Validate(string name, bool isFolder)` returning `MagitekResult`, and `ResourceName.AreSame(string a, string b)`. Tests: `ResourceNameTests` (new, `ImageMagitek.UnitTests/Project`): a theory of rejected names, one per rule (`""`, `" a"`, `"a "`, `"a."`, each forbidden character, `"\u0001"`, `"con"`, `"Nul.sfc"`, `"COM1"`, `"LPT¹"`, 101 characters, 61 four-byte characters, folder `"x.XML"`) each with its message; a theory of accepted names (`"FF2.sfc"`, `"New Folder (2)"`, `"Console"`, `"COM10"`, `".hidden"`, `"Ünïcödé"`, 100 ASCII characters, resource `"x.xml"`); `AreSame` for case variants and NFC/NFD pairs.
2. **Service enforcement.** `CanAddResource`, `CanRenameResource` on `IProjectService`/`ProjectService`; wire them into `AddResource`, `CreateNewFolder` (drop `FindFirstNewChildResourceName`, fix the doc comment), `RenameResourceAsync` (identical-name no-op), `CanMoveNode`, `CreateNewProject`, `CreateNewProjectWithExistingFileAsync` (open-project check). Tests in `ProjectServiceTests` (`ImageMagitek.UnitTests/Project`): `AddResource_InvalidName_FailsWithoutWriting` (theory; directory listing unchanged); `AddResource_CaseVariantOfSibling_FailsAndKeepsSiblingFile` (`Data` beside `data`, `data.xml` bytes unchanged); `AddResource_RootResourceNamedLikeProject_FailsAndKeepsProjectFile`; `CreateNewFolder_ExistingName_Fails`; `CreateNewFolder_InvalidName_CreatesNoDirectory`; `RenameResource_SameName_SucceedsWithoutRenamed`; `RenameResource_CaseVariantOfSibling_Fails`; `RenameProject_ToRootResourceName_Fails`; `MoveNode_TargetHasCaseVariant_Fails`; `CreateNewProjectWithExistingFile_FileInOpenProject_Fails`; `CreateNewProject_InvalidName_FailsWithoutWriting`; `CanRenameResource_MatchesRenameFailure`. Existing `RenameFile_CaseOnly_KeepsResourceFile` must stay green.
3. **Existing names load.** Tests: `ProjectServiceTests.OpenProject_NameBreakingRule_LoadsAndMoves` (a fixture with a 120-character arranger name and a folder with a leading space, both legal on NTFS: loads, renames to its own name as a no-op, moves into another folder).
4. **Dialogs.** `NameResourceViewModel(title, initialName, Func<string, MagitekResult> validate)` with `ErrorText` and `CanAccept`, used by Rename..., Add New Folder... and the from-selection path; remove `RenameNodeViewModel`/`RenameNodeView` and their `ConfigureViewLocator` registration if the merge holds. `AddPaletteViewModel` and `AddScatteredArrangerViewModel` take the validator instead of `existingResourceNames`, override `CanAccept`, call `TryAcceptCommand.NotifyCanExecuteChanged()` on name changes, and drop `CanAdd`/`ExistingResourceNames`/the `ValidateModel` command; views bind the error text. Tests: `NameResourceViewModelTests` (Accept disabled for an empty or invalid name and shows the message; enabled for a valid one; an unchanged rename name is acceptable), `AddPaletteViewModelTests` and `AddScatteredArrangerViewModelTests` (Accept disabled for empty, whitespace, case-variant duplicate; enabled after a valid name).
5. **Project tree commands.** Add New Folder... dialog with the first free name; Add Existing Data File... falling back to the name dialog; New Project from Existing File... selecting the existing node. Verify in the running app with DevTools: Add New Folder... twice on the FF2 root shows "New Folder (2)" prefilled; Rename... with the name unchanged closes with no alert; Rename... to `a/b` disables ✓ with the message; Add New Scattered Arranger with an existing sibling name in another case disables Add. Manual (file picker): Add Existing Data File... of a file whose name collides; New Project from Existing File... on a ROM already in the FF2 project.
6. **Specs and backlog.** Apply the spec changes above with the tests named; delete from `docs/BACKLOG.md` the 1.0 name-validation line and the four promoted UI-PROJECT-TREE lines (Add dialogs gating, Rename unchanged, Add New Folder, New Project from Existing File); delete this proposal.

## Open questions

- LIB-PROJECT-TREE-015 is inherited: keep tree child lookups and path keys case-sensitive, enforcing case-insensitivity only in the service? Recommendation: yes; changing the comparer alters how existing project references resolve, for no gain once new names cannot collide.
- Name length limit: 100 characters and 240 UTF-8 bytes? Recommendation: yes; it covers real No-Intro ROM names and fits `<name>.xml` in a 255-byte component on every supported file system.
- Merge the Rename dialog into `NameResourceViewModel` (deleting `RenameNodeViewModel`/`RenameNodeView`)? Recommendation: yes; the two are the same text box with ✓/x, and one validated dialog serves rename, new folder and new arranger from selection.
