# Palettes for sequential browsing

## Why

Sequential browsing is how a romhacker finds graphics: scroll a file in a sequential editor until tiles appear. Two things make that harder than it should be.

- **The palette combo does nothing.** In a sequential editor's View mode, choosing a palette never recolors the view: the `ChangePalette` call in `GraphicsEditorViewModel.View.cs` is commented out. This is a visible control that does nothing, which ARCHITECTURE §1 rules out for 1.0. (Backlog, **1.0**, UI-GRAPHICS-EDITOR.)
- **There is nothing good to switch to.** The only shipped global palette is `DefaultRgba32`, a 256-entry table whose first 16 colors are close in places (three pure blacks at indices 0, 12 and 28; two similar greens). 2bpp and 4bpp graphics, the bulk of what people browse, show only the first 4 or 16 entries, so muddy or duplicate early colors hide tile structure.

Underneath, the library also loses the palette (backlog, LIB-ARRANGERS). The `SequentialArranger` constructor and `ArrangerBuilder` set `ActivePalette`, but element codecs keep the codec factory's default palette. `ChangeCodec` ignores `ActivePalette`, and cloning drops it. So even with the combo wired up, changing codec reverts the colors.

## What

**Switching palettes.** In a sequential editor, choosing a palette in the combo recolors the whole view at once, in View and Draw modes. It is a view setting, not an edit: no history step, and the editor is not marked modified. The pixels are indices, and the data file does not change. The choice survives codec, layout, size and offset changes for the life of the editor, as long as the new codec is indexed. When an editor opens, it starts on the default palette (the first global palette).

**Palette list.** The sequential editor's combo lists palettes associated in this editor first, in the order they were added, then the global palettes in settings order. Settings order is the curated order below, not alphabetical. Scattered editors keep their current list (referenced palettes, then globals).

**Built-in palettes.** Three new 256-entry global palettes ship in `_palettes`, and the first becomes the default palette:

| Name | Character |
|---|---|
| `TileShop Vivid` (default) | Balanced, clearly distinct hues at even spacing; the general-purpose browsing palette. |
| `TileShop Synthwave` | Deep indigo through violet, magenta, hot pink, peach and teal; rows are smooth gradients. |
| `TileShop Dusk` | Muted, natural tones (slate, moss, clay, sand, sky); high contrast without saturation. |

Each follows one layout:

- **Index 0** is the darkest color, a near-black tinted to the palette's character, so backgrounds read as background.
- **Indices 0–3** step clearly in lightness (dark, mid-dark, mid-light, light), so 2bpp graphics read by lightness alone, colorblind users included, and are also distinct in hue.
- **Indices 0–15** are 16 colors that are all clearly distinct from one another. This is the 4bpp view.
- **Indices 16–255** are 15 rows of 16. Each row is a smooth ramp, dark to light, of one hue family, so 8bpp graphics and photos decode recognizably, and the palette editor's grid reads as an orderly sheet.
- **Not harsh.** No row pairs fully saturated complementary colors at equal lightness. Saturation is capped below full for the brightest entries. `Synthwave` is the only "neon" palette, and its neon sits in the ramps, not in indices 0–3.

Distinctness is measured in OKLab, a perceptual color space. The thresholds are set and pinned by tests (see Tasks). `DefaultRgba32` stays, last in the list, so existing projects that name it keep loading (LIB-PROJECT-FORMAT-023).

**What does not change.** Saved sequential view state (codec, offset, palette) is still not persisted (deferred to 1.1 with sequential arrangers as resources). The NES master palette and the Preferences NES list are unchanged, though the new JSON files also appear in that list, as every `_palettes/*.json` does (UI-SHELL-051). Scattered arrangers' per-element palettes are unchanged.

## Decisions

- **A palette switch in a sequential editor is a view setting, not an edit.** A sequential arranger has one palette for every element (LIB-ARRANGERS), and the data file holds indices, so recoloring changes nothing that Save writes. Reason: browsing means switching often, and history steps or a modified marker would make the editor prompt to save after every look. Rejected: recording it as history (prompts on close for an unchanged file); allowing it only in View mode (Draw mode would show colors the user can't change).
- **Global palettes in settings order.** The order in `appsettings.json` is curated: the default first, then the alternates. Alphabetical sorting would put `DefaultRgba32` first, and the default should be first in the list. Rejected: alphabetical (today's behavior).
- **New built-in palettes are hand-tuned JSON pinned by design tests, not generated at build.** Colors are tuned by eye in a sequential editor. A test enforces what must hold (entry count, opaque, lightness steps in 0–3, distinctness in 0–15, monotonic row ramps), so a later tweak cannot regress them. Rejected:
  - A checked-in generator. Every hand tweak would have to be expressed as generator code.
  - No tests. "High contrast" would be an opinion, not a requirement.
- **Distinctness in OKLab.** OKLab distance tracks perceived difference far better than RGB distance, at a few lines of code with no new package. Rejected: CIEDE2000 (more code for no practical gain here); RGB Euclidean (overrates blue differences).
- **`TileShop Vivid` becomes the default; `DefaultRgba32` stays.** The default palette is what every new codec and every unassigned element uses. Keeping the old one avoids breaking projects that reference it by name. Changing the default changes how existing projects show elements whose palette is the global default. That is a visible change, recorded in the release notes ([docs-1-0](docs-1-0.md)).

## Spec changes

**LIB-ARRANGERS**

- **-017** changed: adds "...and every indexed element shall use the given palette, or the default palette when none is given".
- **-022** changed: adds "...and, when the new codec is indexed, every element shall keep the arranger's active palette".
- Added: When a sequential arranger is cloned, the clone's elements shall use the original's active palette.
- Open items: delete the two bullets on the constructor/`ArrangerBuilder` palette and the clone losing the palette.

**LIB-PALETTES**

- Added (Global palettes):
  - Each shipped 256-color global palette shall have 256 opaque entries.
  - Entries 0–3 shall increase in OKLab lightness, each step at least the threshold set in Tasks.
  - Every pair among entries 0–15 shall be at least the distinctness threshold apart in OKLab.
  - Each row of 16 from entry 16 shall increase in lightness.
- Decisions: add "New built-in palettes are hand-tuned JSON pinned by design tests" and "Distinctness in OKLab".

**LIB-SERVICES**

- **-007** (inherited) changed, and no longer inherited (decided here): the default global palettes shall be `["TileShop Vivid", "TileShop Synthwave", "TileShop Dusk", "DefaultRgba32"]`.

**UI-GRAPHICS-EDITOR**

- Added (View mode):
  - While the arranger is sequential and indexed, the palette combo shall list the palettes associated in this editor, in the order added, then the global palettes in settings order.
  - When a palette is chosen in a sequential editor, the editor shall recolor the whole view with it, in View or Draw mode, without recording history or marking the editor modified.
  - When the codec, element layout, size or offset changes, a sequential editor shall keep the chosen palette while the codec is indexed.
  - When a sequential editor opens, it shall start on the default palette.
- **-023** changed: "rebuild the palette list" becomes "keep the palette list and the chosen palette".
- Decisions: add "A palette switch in a sequential editor is a view setting, not an edit" and "Global palettes in settings order".

## Tasks

1. **Library.**
   - Code: `SequentialArranger` constructor, `ArrangerBuilder`'s sequential path, `ChangeCodec` and `CloneArranger` apply `ActivePalette` to every indexed element's codec.
   - Tests: `SequentialArrangerTests` (new, `ArrangerTests/`): `Construct_ElementsUseGivenPalette`, `ArrangerBuilder_ElementsUsePalette`, `ChangeCodec_KeepsActivePalette`, `Clone_KeepsActivePalette`, `ChangePalette_AllElements` (LIB-ARRANGERS-023, now tested).
2. **Editor.**
   - Code:
     - Restore and finish `ChangePalette` in `GraphicsEditorViewModel.View.cs`, applied from `OnSelectedPaletteChanged` for sequential arrangers in any mode.
     - `InitializePalettes` keeps the selection across codec changes.
     - The sequential list order (associated, then globals in settings order).
   - Tests: `GraphicsEditorPaletteTests` (new; extract the list and selection logic into a plain class if the ViewModel cannot be built without Avalonia, per ARCHITECTURE §5): `Sequential_ChoosePalette_RecolorsWithoutHistory`, `Sequential_ChangeCodec_KeepsPalette`, `Sequential_List_AssociatedThenGlobalsInSettingsOrder`, `Sequential_Opens_OnDefaultPalette`.
   - Verify in the running app with DevTools: Open File... on the FF2 ROM, set the codec to SNES 4bpp, choose each new palette in the combo and screenshot (the view recolors, the tab shows no `*`); change codec to SNES 2bpp and check the palette is kept; switch to Draw mode and back.
3. **Palettes.**
   - Code: author `TileShop Vivid.json`, `TileShop Synthwave.json` and `TileShop Dusk.json` in `ImageMagitek/_palettes` (JSON format of LIB-PALETTES-028, with `author`), tuned against real graphics in a sequential editor (FF2 at 2bpp and 4bpp, and the welcome demo file from [welcome-screen](welcome-screen.md) once it exists). Add each file to `ImageMagitek.csproj` (palettes are copied to the output by name). Update `ImageMagitek.Services/appsettings.json` and the built-in defaults.
   - Tests:
     - `GlobalPaletteDesignTests` (new, `ColorTests/`; theory over the three files): `Has256OpaqueEntries`, `FirstFour_StepInLightness` (each step ≥ 0.15 OKLab L), `FirstSixteen_PairwiseDistinct` (every pair ≥ 0.12 OKLab ΔE), `Rows_RampInLightness`.
     - `OkLabTests` for the conversion helper (known sRGB → OKLab values).
     - `SettingsServiceTests`: update the defaults assertion.
     - The thresholds are starting values: tune them while authoring, then pin them in the tests and the LIB-PALETTES requirements.
   - Manual: review each palette at 2bpp, 4bpp and 8bpp in the running app with the user before landing. Taste is the user's call.
4. **Specs and backlog.**
   - Apply the spec changes above with real test names.
   - Delete from `docs/BACKLOG.md` the **1.0** UI-GRAPHICS-EDITOR palette combo line and the LIB-ARRANGERS sequential palette line.
   - Add the default palette change to the release-notes list in [docs-1-0](docs-1-0.md).
   - Delete this proposal. Run `dotnet test ImageMagitek.UnitTests`.

## Open questions

None.
