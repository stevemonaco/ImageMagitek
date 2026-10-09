---
id: LIB-COLORS
title: Color models and color matching
project: ImageMagitek
sources:
  - ImageMagitek/Colors/ColorFormats
  - ImageMagitek/Colors/Converters
  - ImageMagitek/Colors/ColorFactory.cs
  - ImageMagitek/Colors/ColorDistance.cs
  - ImageMagitek/Colors/ColorMatchStrategy.cs
  - ImageMagitek/Colors/PaletteColorMatcher.cs
  - ImageMagitek/Colors/IColor.cs
  - ImageMagitek/Colors/IColor32.cs
  - ImageMagitek/Colors/ITableColor.cs
  - ImageMagitek/Colors/Serialization/ColorRgba32JsonConverter.cs
  - ImageMagitek/Utility/Parsing/ColorParser.cs
types:
  - ColorModel
  - IColor
  - IColor32
  - ITableColor
  - ColorRgba32
  - ColorBgr15
  - ColorRgb15
  - ColorAbgr16
  - ColorBgr9
  - ColorBgr6
  - ColorNes
  - IColorConverter
  - ColorConverterBgr15
  - ColorConverterRgb15
  - ColorConverterAbgr16
  - ColorConverterBgr9
  - ColorConverterBgr6
  - ColorConverterNes
  - IColorFactory
  - ColorFactory
  - ColorParser
  - ColorRgba32JsonConverter
  - ColorMatchStrategy
  - ColorMatch
  - PaletteColorMatcher
  - ColorDistance
tests:
  - ForeignColorTests
  - NativeColorTests
  - PaletteColorMatcherTests
  - PaletteTests
  - DirectCodecKnownAnswerTests
depends:
  - LIB-PALETTES
---

# Color models and color matching

## Purpose

Every color has a *native* form, RGBA32, used for display and image files, and a *foreign* form, the raw value a target system stores, in one of several color models. This spec defines the color models, the conversions between native and foreign colors, hex parsing and formatting, and how a native color is matched to a palette entry. Palettes (LIB-PALETTES) hold colors in a model; the NES model converts through the NES master palette that LIB-PALETTES loads. This spec also specifies `Palette.TryGetIndexByNativeColor`, `Palette.GetIndexByNativeColor`, `Palette.StringToColorModel`, `Palette.ColorModelToString` and `Palette.GetColorModelNames`.

## Requirements

### Color models

- **LIB-COLORS-001** — The library shall support the color models Rgba32, Bgr15, Rgb15, Abgr16, Bgr9, Bgr6 and Nes, with storage sizes of 32, 16, 16, 16, 16, 8 and 8 bits.
  - Tests: untested
- **LIB-COLORS-002** — An Rgba32 color shall hold 8-bit R, G, B and A channels, packed with R in the lowest byte.
  - Tests: untested
- **LIB-COLORS-003** — A Bgr15 color shall hold 5-bit channels with R in bits 0–4, G in bits 5–9 and B in bits 10–14, ignore bit 15 on read and write it as 0.
  - Tests: `NativeColorTests.ToForeignColor_Converts_Correctly`
- **LIB-COLORS-004** — An Rgb15 color shall hold 5-bit channels with B in bits 0–4, G in bits 5–9 and R in bits 10–14.
  - Tests: untested
- **LIB-COLORS-005** — An Abgr16 color shall hold 5-bit channels laid out as Bgr15, plus a 1-bit alpha (the PlayStation STP bit) in bit 15.
  - Tests: `DirectCodecKnownAnswerTests.Psx16Bpp_TransparentAndSemiTransparent`
- **LIB-COLORS-006** — A Bgr9 color shall hold 3-bit channels with R in bits 1–3, G in bits 5–7 and B in bits 9–11 (the Genesis CRAM word), ignoring the other bits.
  - Tests: `ForeignColorTests.Bgr9_RawGenesisWord_UnpacksThreeBitChannels`
- **LIB-COLORS-007** — A Bgr6 color shall hold 2-bit channels with R in bits 0–1, G in bits 2–3 and B in bits 4–5.
  - Tests: untested
- **LIB-COLORS-008** — A Nes color shall hold an index 0–63 into the NES master palette.
  - Tests: untested
- **LIB-COLORS-009** — If a channel or Nes index is set above its model's maximum, then the color shall throw `ArgumentOutOfRangeException`; constructing from a raw value masks the channels instead.
  - Tests: untested
- **LIB-COLORS-010** — Models without alpha (Bgr15, Rgb15, Bgr9, Bgr6) shall always report alpha 0 and ignore alpha writes.
  - Tests: `NativeColorTests.ToForeignColor_Converts_Correctly`
- **LIB-COLORS-011** — The color model names shall convert to and from the strings `Rgba32`, `Bgr15`, `Rgb15`, `Abgr16`, `Bgr9`, `Bgr6` and `Nes`, and an unknown name or model shall throw `ArgumentException`.
  - Tests: untested

### Native and foreign conversion

- **LIB-COLORS-012** — When a Bgr15 or Rgb15 color is converted to native, each channel shall become its value shifted left 3 bits, with alpha 255.
  - Tests: `ForeignColorTests.ToNative_AsExpected`
- **LIB-COLORS-013** — When a native color is converted to Bgr15 or Rgb15, each channel shall be truncated to its top 5 bits and alpha shall be dropped.
  - Tests: `NativeColorTests.ToForeignColor_Converts_Correctly`
- **LIB-COLORS-014** — When a Bgr9 color is converted to native, each channel shall map through the table 0, 36, 72, 109, 145, 182, 218, 255, with alpha 255.
  - Tests: `ForeignColorTests.ToNative_AsExpected`
- **LIB-COLORS-015** — When a native color is converted to Bgr9, each channel shall be rounded to the nearest of 8 levels and alpha dropped.
  - Tests: untested
- **LIB-COLORS-016** — When a Bgr6 color is converted to native, each channel shall be multiplied by 85, with alpha 255; converting to Bgr6 shall divide each channel by 85, truncating.
  - Tests: untested
- **LIB-COLORS-017** — When an Abgr16 color is converted to native, `0x0000` shall become transparent black (alpha 0), STP set on a non-black color shall become alpha 128, and every other value shall be opaque, with channels shifted left 3 bits.
  - Tests: `DirectCodecKnownAnswerTests.Psx16Bpp_TransparentAndSemiTransparent`, `DirectCodecKnownAnswerTests.Psx16Bpp_LittleEndianBgr555_OpaqueColorsClearStp`
- **LIB-COLORS-018** (inherited) — When a native color is converted to Abgr16, alpha below 64 shall become `0x0000`; otherwise channels shall be truncated to 5 bits and STP set when the result is black or alpha is below 192.
  - Tests: `DirectCodecKnownAnswerTests.Psx16Bpp_TransparentAndSemiTransparent`
- **LIB-COLORS-019** — Rgba32 conversion in either direction shall be the identity.
  - Tests: untested
- **LIB-COLORS-020** — When a Nes color is converted to native, it shall become the NES master palette's native color at that index.
  - Tests: untested
- **LIB-COLORS-021** — When a native color is converted to Nes, the factory shall pick the Nearest (CIE94) entry of the NES master palette.
  - Tests: untested
- **LIB-COLORS-022** — If a Nes conversion or a Nes color from components is requested before the NES master palette is set, then the factory shall throw `ArgumentException`.
  - Tests: untested

### Color factory

- **LIB-COLORS-023** — When a color is created from a model and a raw value, the factory shall unpack the value in that model; creating without a value shall give raw value 0.
  - Tests: untested
- **LIB-COLORS-024** — When a color is created from a model and R, G, B, A components, the components shall be in the model's own channel ranges, except for Nes, where they are a native color matched to the master palette.
  - Tests: untested
- **LIB-COLORS-025** — When a color is cloned, the factory shall return an independent color of the same model and value.
  - Tests: untested
- **LIB-COLORS-026** — If a color type or model is not one of the supported models, then the factory shall throw `NotSupportedException`.
  - Tests: untested

### Hex strings

- **LIB-COLORS-027** — When a color is formatted as hex, Rgba32 shall format as `#RRGGBBAA`, the 16-bit models as `#` plus 4 hex digits of the raw value, and Nes and Bgr6 as `#` plus 2 hex digits.
  - Tests: untested
- **LIB-COLORS-028** — When hex is parsed as Rgba32, `#RRGGBB` shall give alpha 255 and `#RRGGBBAA` the given alpha.
  - Tests: `PaletteTests.DeserializePalette_InvalidColor_ThrowsNamingEntry`
- **LIB-COLORS-029** — When hex is parsed in another model, the input shall be `#` plus the digit count that model formats with, read as the raw value.
  - Tests: untested
- **LIB-COLORS-030** — If the hex string does not have the model's form, then parsing shall return false without a color.
  - Tests: `PaletteTests.DeserializePalette_InvalidColor_ThrowsNamingEntry`
- **LIB-COLORS-031** — The JSON color converter shall read and write native colors as `#RRGGBBAA` strings and throw `JsonException` on a null or unparsable string.
  - Tests: untested

### Color matching

- **LIB-COLORS-032** — When a color is matched, an entry with the identical RGBA value shall be an exact match at distance 0, and among identical entries the lowest index shall win.
  - Tests: `PaletteColorMatcherTests.TryMatch_IdenticalColor_IsExactHit`, `PaletteColorMatcherTests.TryMatch_Exact_DuplicateEntries_FirstWins`
- **LIB-COLORS-033** — Under the Exact strategy, a color with no identical entry shall be rejected, and the match shall still report the nearest entry by CIE94 and its distance.
  - Tests: `PaletteColorMatcherTests.TryMatch_Exact_MissingColor_RejectsWithNearestHint`
- **LIB-COLORS-034** — Under the Nearest strategy, the matcher shall pick the entry with the smallest CIE94 (graphic arts) ΔE in Lab space, ignoring alpha.
  - Tests: `PaletteColorMatcherTests.TryMatch_Nearest_PicksClosestEntry`, `PaletteColorMatcherTests.TryMatch_Nearest_IgnoresAlpha`
- **LIB-COLORS-035** — Under the NearestRgb strategy, the matcher shall pick the entry with the smallest red-mean weighted RGB distance, ignoring alpha.
  - Tests: `PaletteColorMatcherTests.TryMatch_Nearest_PicksClosestEntry`
- **LIB-COLORS-036** — When distances tie, the matcher shall pick the lowest index.
  - Tests: untested
- **LIB-COLORS-037** — When a maximum distance is given, a nearest strategy shall reject a non-exact match farther than it and still report that candidate; without one, any distance is accepted.
  - Tests: `PaletteColorMatcherTests.TryMatch_Nearest_BeyondMaxDistance_RejectsWithHint`
- **LIB-COLORS-038** — When an entry limit is given, only entries below the limit shall be matchable by any strategy.
  - Tests: `PaletteColorMatcherTests.TryMatch_EntryLimit_ExcludesLaterEntries`
- **LIB-COLORS-039** — If the palette (or its limited range) has no entries, then every non-identical color shall be rejected.
  - Tests: `PaletteColorMatcherTests.TryMatch_EmptyPalette_Rejects`
- **LIB-COLORS-040** — The matcher shall return the same result for repeated matches of a color for its lifetime, even if the palette changes.
  - Tests: `PaletteColorMatcherTests.TryMatch_RepeatedColor_ReturnsSameResult`
- **LIB-COLORS-041** — `Palette.TryGetIndexByNativeColor` shall match with a fresh matcher and no distance or entry limit; `GetIndexByNativeColor` shall throw `ArgumentException` naming the color and palette when the match is rejected.
  - Tests: untested

## Invariants

- Converting a foreign color to native and back to the same model gives the same foreign color for Bgr15, Rgb15, Abgr16, Bgr9, Bgr6 and Rgba32.
- A matcher's accepted index is always below the palette's entry count and the entry limit.

## Edge cases

- Parsing `#40`–`#FF` as Nes throws `ArgumentOutOfRangeException` from the color instead of returning false.
- Creating an Rgba32 color from components above 255 wraps them to a byte.
- Palettes with more than 256 entries: match indices are bytes, so entries past 255 wrap.
- `ColorVector` gives each channel normalized to 0–1 by its model maximum; channel-only models report alpha 1 in the vector.

## Threading and lifetime

- Colors are value types. The color factory holds the NES converter, set once at startup (`SetNesPalette`) from the palette store (LIB-PALETTES); every palette created with that factory shares it.
- A matcher caches results per color and is not thread-safe. It snapshots exact lookups at construction but reads Lab entries lazily from the palette.

## Decisions

- **Bgr9 uses 3-bit channels (Genesis).** The color unpacks bits 1–3, 5–7 and 9–11 and the converter maps 7 to 255. Reason: the color used 4-bit nibbles while the converter indexed an 8-entry table, so values of 8 or more threw during palette load and 7 mapped to 182.
- **PSX STP semantics.** Abgr16 conversion keeps the STP bit: `0x0000` is transparent, STP on black is opaque black, STP on any other color is semi-transparent, and no STP is opaque. Reason: lossless round trips of PlayStation data.
- **Exact matching reports a hint.** A rejected exact match still carries the nearest entry and distance so import reports can suggest a fix (LIB-IMAGE-IO).
- **Alpha is ignored by nearest matching.** Reason: image editors often drop or alter alpha; only the exact match compares it.

## Non-goals

- Additional color models (RGB24, ARGB32, RGB565, GRB333, N64 IA/I, grayscale).
- Dithering or quantizing images to a palette.

## Open items

- `ColorConverterNes.ToForeignColor` casts the master palette's foreign color to `ColorNes`, but the master palette is an Rgba32 GlobalJson palette whose foreign colors are `ColorRgba32`, so every native-to-Nes conversion throws `InvalidCastException`. This breaks `SetNativeColor` and component `SetForeignColor` on Nes palettes and loading a GlobalJson palette in the Nes model.
- `Palette.GetColor` builds a `System.Drawing.Color` with `FromArgb` from the RGBA-packed value, which swaps R and B; it has no callers.
- No tests for Rgb15, Bgr6, Nes and Rgba32 conversion, hex formatting and parsing outside Rgba32, factory errors, or distance ties.
