# Small library fixes for 1.0

## Why

A batch of small library bugs that need little or no design. Each crashes on, refuses or silently miswrites data a user can reach from the UI or CLI. Bundled so they are tracked to 1.0 together. All were confirmed in the code (not reproduced).

- **1.0** [LIB-COLORS] `ColorConverterNes.ToForeignColor` returns `(ColorNes)_nesPalette.GetForeignColor(index)`. The master palette is an Rgba32 GlobalJson palette, so the cast throws `InvalidCastException` on every native→NES conversion. Reachable: `SetNativeColor` on an NES palette (color flyout, palette editor), pasting colors into an NES palette (`PaletteEditorViewModel.TryParseColors` → `ToForeign`), and loading a GlobalJson palette in the NES model.
- (promoted) [LIB-DATASOURCE] `FileDataSource` always opens with `FileAccess.ReadWrite`, so a file with the read-only attribute fails its first read with `UnauthorizedAccessException`, cached by the `Lazy` until `Reopen`. ROMs extracted from archives, copied from discs or kept read-only on purpose are common. Today the user cannot even view them, and the read-only attribute is often the user's own protection against corrupting a ROM. 1.0 must open them and keep them unwritten.
- (promoted) [LIB-SERVICES] The `.tim` association names `PSX 4bpp` in both `appsettings.json` and `SettingsService`'s defaults. It works today only through LIB-CODECS-037's legacy-name table, so shipped defaults depend on a compatibility path meant for old projects. A one-line fix before the defaults are frozen with 1.0.
- (promoted) [LIB-COLORS] `ColorParser.TryParse` accepts `#40`–`#FF` for Nes and then `new ColorNes(raw)` throws `ArgumentOutOfRangeException` (LIB-COLORS-009). Reachable: typing a foreign color in an NES palette's Sources row (`ForeignColorSourceModel.ValidateHexColor` throws instead of reporting invalid) and loading a hand-edited `foreigncolor`. A `Try` method that throws breaks every caller's error path.
- (promoted) [LIB-DATASOURCE] `BitAddress.Equals(object)` casts its argument, so comparing to any non-`BitAddress` throws `InvalidCastException`. It breaks the `object.Equals` contract that collections, bindings and test assertions rely on, and the fix is one line.

Included from the LIB "Likely bugs" list because they write wrong data from reachable paths:

- [LIB-IMAGES] `ImageCopier.CanRemapByExactIndex` is inverted. It compares the destination's current pixel with `1 << sourceDepth` and never checks that a source index fits the destination codec. Pasting 4bpp pixels into a 2bpp arranger (Ctrl+V in Draw mode; the UI tries exact index first, `GraphicsEditHistory`) copies indices up to 15. The editor shows them, and the codec's encode keeps only the low bits (`IndexedFlowGraphicsCodec.EncodeElement` masks per plane), so the ROM receives different pixels than the user saw and saved. The inverted check also rejects valid pastes when the destination already holds an index ≥ `1 << sourceDepth`.

Left out of the LIB "Likely bugs" list, with the reason:

- Bit-unaligned write past EOF merging into `0xFF`: unreachable. Image saves skip elements not wholly inside their source (LIB-ARRANGERS-008), and palette file colors are byte-aligned (`FileColorSourceModel` holds a byte offset).
- `DirectImage` sub-rectangle save: unreachable. Editors always build whole-arranger images (`ArrangerImageAdapter.CreateImage(0, 0, …)`), the CLI exports and imports whole arrangers, and the only sub-rectangle `DirectImage` (`ArrangerCopy`) is read, never saved.
- Zero-bit read, `PatternList` bounds, `AreResourcesInSameProject`, the working-directory bootstrap, rotation of mirrored elements, sequential arranger palettes, clone rectangles, `GetInitialSequentialFileAddress` (latent), foreign-color palettes rewritten on save, unquantized `SetNativeColor`, the data file lock after deletion, sub-rectangle export: each fails loudly, affects display or project XML only, or rewrites identical bytes. None writes wrong bytes to a data file.
- `CreateNewProject` overwriting an existing project file: owned by [project-service-integrity.md](project-service-integrity.md).
- The mixed-endian palette writer: owned by [project-format-round-trip.md](project-format-round-trip.md).
- The **1.0** items in that section are left to their own changes.

## What

- Native→NES conversion returns the nearest master-palette index as a `ColorNes`, so NES palettes take native colors, pasted colors and NES-model global palettes.
- A data file that cannot be opened for writing because of the read-only attribute, an ACL or write-protected media opens for reading. `DataSource.IsReadOnly` is true for it. Through the one read-only gate (ARCHITECTURE §6), every arranger with an element on it is read-only: Draw hidden, import refused, pixel save skipped, element arrangement still saved to the project XML. Its palettes become read-only when they have file colors on it. Every write overload on a read-only source throws `InvalidOperationException` naming the source, as a backstop. Read-only messages say why: the codec, or the read-only data file. The graphics editor shows a "Read only" note with the reason.
- `.tim` maps to `PSX 4bpp Flow`.
- `ColorParser.TryParse` returns false for Nes values above `#3F`.
- `BitAddress.Equals(object)` returns false for other types.
- Exact-index pixel paste succeeds only when every source index fits the destination element's codec; otherwise the next requested operation (exact palette colors) is tried, as today.

Unchanged: files that open read-write behave as today; codec read-only behavior; the legacy codec-name table; the NES master palette and how it is chosen.

## Decisions

- **NES conversion matches within the first 64 master entries.** `ToForeignColor` uses a `PaletteColorMatcher` with the Nearest strategy and an entry limit of 64 (LIB-COLORS-038) and returns `new ColorNes(index)`. Reason: `ColorNes` holds 0–63 (LIB-COLORS-008), and a master palette may carry more than 64 entries (the bootstrapper checks only for at least 64). Rejected: the cast (wrong type), and an unlimited match (can return 64+ and throw in `ColorNes`).
- **A read-only file opens read-only, and the arranger becomes read-only through the existing gate.** This aligns with [compression-support.md](compression-support.md): `DataSource.IsReadOnly` is virtual and false by default, and the arranger read-only check also holds when any element's source `IsReadOnly`. That proposal's "one read-only gate" decision is built here first, and compressed sources reuse it unchanged. Reason: every UI and CLI write path already consults the arranger check (draw, paste pixels, color remap, import, save, CLI import), so nothing new can forget it, and the user learns up front instead of on Save. Rejected: opening read-write lazily on first save and failing then with an error (the user loses the edits they just made), and failing the open (today's behavior).
- **Writes to a read-only source throw `InvalidOperationException`; `Flush` does nothing.** Same contract as the compression proposal. It is a backstop for callers that bypass the gate. Rejected: letting `FileStream` throw `NotSupportedException` (a different exception per source type).
- **Fall back to read-only on access denial only.** `FileDataSource` tries `ReadWrite`. On `UnauthorizedAccessException` (read-only attribute, ACL) or a write-protect `IOException` (`ERROR_WRITE_PROTECT`, read-only media) it opens `FileAccess.Read` with `FileShare.Read`. A sharing violation still fails as today. Reason: a sharing violation is transient (an emulator holding the file), and falling back would make the file read-only for the whole session without the user knowing why. Rejected: checking `File.GetAttributes` up front (misses ACLs and media, races with the open).
- **Read-only state is decided at open and kept until `Reopen`.** `IsReadOnly` opens the file if it is not open yet (like `Length`, LIB-DATASOURCE-020). It is false for a missing file, whose missing-file gate takes over, and false when the open failed, whose failure surfaces on read as today (LIB-DATASOURCE-022). Clearing the attribute mid-session takes effect after the project is reopened or the file relinked. Rejected: re-checking on every query (a file-system call per element per gate check).
- **Palettes with file colors on a read-only source are read-only.** `Palette.IsReadOnly` is true for global palettes and for palettes with at least one file color source on a read-only data source. `SavePalette` on such a palette writes nothing and returns false, and the palette editor's existing read-only mode (UI-PALETTE-EDITOR-090/091) keys off `Palette.IsReadOnly`. A palette made only of project colors stays editable. Reason: that mode exists and hides every edit path. It also makes it reachable, which the spec's Open items doubt. Rejected: a second, ROM-specific read-only mode that keeps Sources visible (two modes to keep in step).
- **Read-only messages give the reason.** `ArrangerExtensions.GetReadOnlyReason` returns null or "uses codec '<name>' that cannot encode" / "reads data file '<name>', which is read-only" for the first offending element. The importer, `SaveImage`, UI import alert and CLI message use it. Reason: "read-only because it uses a codec that cannot encode" would be false for a read-only file. The graphics editor shows a "Read only: <reason>" text in its toolbar while read-only, because Draw just disappears today, which reads as a control that silently went missing. Rejected: a tooltip only (a popup DevTools cannot check).
- **The exact-index check tests source indices against the destination codec.** For every pixel, the destination element must exist and the source index must be below `1 << destCodec.ColorDepth`. The destination's current pixels are irrelevant. Rejected: clamping or masking indices on paste (silently changes pixels).
- **Shipped association defaults name canonical codecs.** `.tim` → `PSX 4bpp Flow` in both `appsettings.json` and the code defaults. LIB-SERVICES-007 lists only extensions and stays as worded. The legacy-name table stays for old projects and old user settings files.

## Spec changes

- **LIB-COLORS:**
  - LIB-COLORS-021 changed: "When a native color is converted to Nes, the factory shall return the index of the Nearest (CIE94) entry among the first 64 entries of the NES master palette." Tests: `NesColorConversionTests.ToForeign_Native_ReturnsNearestMasterIndex`.
  - LIB-COLORS-030 changed: "If the hex string does not have the model's form, or its value is outside the model's range (a Nes value above `#3F`), then parsing shall return false without a color."
  - Edge cases: strike the `#40`–`#FF` line. Open items: strike the `ColorConverterNes` line and update the test-gap line.
- **LIB-DATASOURCE:**
  - LIB-DATASOURCE-004: add "and a bit address compared with any other type, or null, shall be unequal."
  - LIB-DATASOURCE-021 changed: "When a file source opens its file, it shall open it for reading and writing and allow other processes to read but not write it; if write access is denied (`UnauthorizedAccessException` or a write-protected volume), then it shall open the file for reading only."
  - Added: "A data source shall report `IsReadOnly`: false for memory sources, and for a file source true exactly when its file was opened for reading only; reading it opens the file if needed, and it is false while the file is missing or its open failed."
  - Added: "If a read-only source is written by any write overload, then the write shall throw `InvalidOperationException` naming the source and change nothing; `Flush` shall do nothing."
  - Added: "When `Reopen` is called, the file source shall decide its access again on the next open."
  - Edge cases: strike the read-only attribute line. Non-goals: replace "Read-only or compressed sources…" with "Compressed sources ([proposal](../../changes/compression-support.md))". Open items: strike the `BitAddress.Equals` and `IsReadOnly` lines. Decisions: add "Read-only files open read-only" and "Fall back on access denial only".
- **LIB-ARRANGERS:**
  - LIB-ARRANGERS-009 changed: "The arranger shall be read-only when any element uses a codec that cannot encode or reads a read-only data source."
  - Added: "When the read-only reason is requested, the arranger shall return null when writable, otherwise a reason naming the first offending element's codec or data file."
- **LIB-PALETTES:**
  - Added: "A palette shall be read-only when it is a global palette, or when it has a file color source and its data source is read-only."
  - LIB-PALETTES-024 changed: "When a read-only palette is saved, the palette shall write nothing and return false."
  - Decision "Global palettes are read-only" extended to read-only data files.
- **LIB-IMAGES:**
  - LIB-IMAGES-028 changed: "When pasting indexed into indexed, the copier shall try the requested remap operations in order and apply the first whose check passes: exact index passes only when every source index is below `1 << ` the destination element's codec color depth, and copies indices; exact palette colors writes each source color's exact index in the destination palette."
  - Open items: strike the `CanRemapByExactIndex` line and the `ImageCopierTests` line.
  - LIB-IMAGES Decisions "Read-only arrangers refuse to save": "A codec that cannot encode or a read-only data source makes the arranger read-only…".
- **LIB-SERVICES:** Open items: strike the `.tim` line.
- **LIB-IMAGE-IO:** the importer's read-only failure message is "Arranger '<name>' is read-only because it <reason>".
- **UI-GRAPHICS-EDITOR:**
  - UI-GRAPHICS-EDITOR-002 changed: "While the arranger is read-only (LIB-ARRANGERS-009), the editor shall hide the Draw mode button and refuse to switch to Draw."
  - Added: "While the arranger is read-only, the editor toolbar shall show "Read only: <reason>" (LIB-ARRANGERS)."
- **UI-IMAGE-IO-005** changed: the alert text is "'<name>' is read-only because it <reason>".
- **UI-PALETTE-EDITOR:**
  - UI-PALETTE-EDITOR-090 and -091 changed: "While the palette is read-only (LIB-PALETTES: a built-in global palette, or file colors on a read-only data file), …" (rest unchanged). Tests: manual, see task 3.
  - Open items: the "read-only mode looks unreachable" line now notes it is reachable through read-only data files; only the "Duplicate to project" half remains.
- **docs/changes/compression-support.md:** its LIB-DATASOURCE `IsReadOnly` and LIB-ARRANGERS read-only-check items and the first half of Task 1 are done here; reword them to "reuse".

## Tasks

1. **Colors.** Fix `ColorConverterNes.ToForeignColor` (matcher with entry limit 64, `new ColorNes(index)`) and `ColorParser.TryParse` (return false above `#3F`). Tests: `NesColorConversionTests.ToForeign_Native_ReturnsNearestMasterIndex` (each master entry's own color maps to its index; an off color maps to the nearest), `NesColorConversionTests.ToForeign_MasterLongerThan64_StaysBelow64`, `PaletteTests.SetNativeColor_NesPalette_StoresMatchingIndex`, `PaletteTests.GlobalJsonPalette_NesModel_Loads`, `ColorParserTests.TryParse_Nes` (theory: `#00`, `#3F` succeed; `#40`, `#FF` return false; nothing throws).
2. **BitAddress and `.tim`.** `Equals(object)` becomes `obj is BitAddress other && Equals(other)`. Change `.tim` in `appsettings.json` and `SettingsService`. Tests: `DataSourceBitAddressTests.Equals_NonBitAddress_ReturnsFalse` (a string, a boxed int, null); `SettingsServiceTests.Defaults_AssociationsNameRegisteredCodecs` (every code-default and shipped `appsettings.json` association names a codec the factory registers from `_codecs` directly, not through the legacy table).
3. **Read-only data files.** `DataSource.IsReadOnly` (virtual, false), the write guards and no-op `Flush`, the `FileDataSource` fallback open, `ArrangerExtensions.IsReadOnly` over sources plus `GetReadOnlyReason`, `Palette.IsReadOnly` and the `SavePalette` guard, reason text in `ImageImporter`, `IndexedImage`/`DirectImage.SaveImage`, the UI import alert, the editor's "Read only" text, and `PaletteEditorViewModel.IsReadOnly` → `Palette.IsReadOnly`. Tests (each creates a temp file and sets `FileAttributes.ReadOnly`, clearing it in `Dispose`): `FileDataSourceTests.ReadOnlyAttribute_OpensAndReads`, `FileDataSourceTests.ReadOnlyAttribute_IsReadOnly`, `FileDataSourceTests.WritableFile_IsNotReadOnly`, `FileDataSourceTests.MissingFile_IsNotReadOnly`, `FileDataSourceTests.ReadOnly_EveryWriteOverload_ThrowsAndLeavesBytes` (sentinel bytes unchanged on disk), `FileDataSourceTests.Reopen_AfterClearingAttribute_IsWritable`; `ReadOnlyArrangerTests.IsReadOnly_ElementOnReadOnlySource_IsTrue`, `ReadOnlyArrangerTests.GetReadOnlyReason_NamesCodecOrDataFile`, `ReadOnlyArrangerTests.SaveImage_ReadOnlySource_ThrowsBeforeWriting`, `ReadOnlyArrangerTests.Prepare_ReadOnlySource_Fails`; `PaletteTests.IsReadOnly_FileColorsOnReadOnlySource_IsTrue`, `PaletteTests.IsReadOnly_ProjectColorsOnly_IsFalse`, `PaletteTests.SavePalette_ReadOnly_WritesNothingReturnsFalse`. DevTools check: stop the app, set the read-only attribute on the FF2 ROM the debug project uses, launch, Load FF2. "Adult Rydia Map" opens with no Draw button and shows "Read only: …"; Arrange-mode element moves save; a palette on that ROM opens with the Read Only badge. Clear the attribute afterwards. Manual: open a read-only ROM through File → Open File... (file picker).
4. **Exact-index paste.** Rewrite `CanRemapByExactIndex`. Tests (in `ImageCopierTests`, replacing its empty method): `CopyPixels_ExactIndex_IndexAboveDestDepth_Fails` (4bpp source with index 15 into 2bpp, exact index only: fails, destination unchanged), `CopyPixels_ExactIndexThenColors_IndexAboveDestDepth_UsesColors`, `CopyPixels_ExactIndex_DestHoldsHighIndex_Succeeds` (2bpp into a 4bpp region already holding index 9).
5. **Close.** Update LIB-COLORS, LIB-DATASOURCE, LIB-ARRANGERS, LIB-PALETTES, LIB-PROJECT-FORMAT, LIB-IMAGES, LIB-IMAGE-IO, LIB-SERVICES, UI-GRAPHICS-EDITOR, UI-IMAGE-IO, UI-PALETTE-EDITOR and compression-support.md as listed. Delete these backlog lines: the **1.0** `ColorConverterNes` line, `ColorParser`, `BitAddress.Equals`, the read-only-attribute line, `CanRemapByExactIndex`, and the `.tim` line under Dead code. Add a line to the release notes draft: read-only data files now open read-only.

## Open questions

None. The read-only behavior was chosen above (open read-only and gate through the arranger, rather than open and fail on write). Confirm it if you prefer the other.
