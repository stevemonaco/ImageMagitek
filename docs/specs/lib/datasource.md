---
id: LIB-DATASOURCE
title: Data sources
project: ImageMagitek
sources:
  - ImageMagitek/DataSource.cs
  - ImageMagitek/FileDataSource.cs
  - ImageMagitek/MemoryDataSource.cs
  - ImageMagitek/BitAddress.cs
  - ImageMagitek/ExtensionMethods/StreamReadExtensionMethods.cs
  - ImageMagitek/ExtensionMethods/StreamWriteExtensionMethods.cs
types:
  - DataSource
  - FileDataSource
  - MemoryDataSource
  - BitAddress
  - StreamReadExtensionMethods
  - StreamWriteExtensionMethods
tests:
  - DataSourceBitAddressTests
  - FileDataSourceTests
  - StreamReadExtensionTests
  - StreamWriteExtensionTests
  - ProjectTreeEventTests
  - ProjectServiceTests
  - IndexedImageTests
depends: []
---

# Data sources

## Purpose

A data source is the byte store that graphics and palette colors are read from and written to: a file on disk (a ROM or graphics file) or a block of memory. Reads and writes are bit-addressed, so codecs and color sources can start mid-byte. Codecs (LIB-CODECS) and palettes (LIB-PALETTES) read through it; the project tree (LIB-PROJECT-TREE) forwards its `DataWritten` event; `ProjectService.RelinkDataFileAsync` (LIB-PROJECT-SERVICE) repairs a missing file through `IsMissing` and `Reopen`.

## Requirements

### Bit addresses

- **LIB-DATASOURCE-001** — The bit address shall identify a position as a byte offset plus a bit offset of 0–7, where bit offset 0 is the most significant bit of the byte.
  - Tests: `StreamReadExtensionTests.ReadShifted_AsExpected`
- **LIB-DATASOURCE-002** — If a bit address is constructed with a bit offset outside 0–7, then it shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **LIB-DATASOURCE-003** — When a bit address is constructed from a total bit count, it shall split the count into byte and bit offsets.
  - Tests: untested
- **LIB-DATASOURCE-004** — Bit address addition, subtraction, equality and ordering shall operate on the total bit offset, and a bit address compared with any other type, or null, shall be unequal.
  - Tests: `DataSourceBitAddressTests.Equals_NonBitAddress_ReturnsFalse`

### Reading

- **LIB-DATASOURCE-005** — When N bits are read at a bit address, the source shall return ⌈N/8⌉ bytes holding those bits shifted to start at the most significant bit of the first byte, with the unused trailing bits of the last byte cleared.
  - Tests: `DataSourceBitAddressTests.Write_ThenRead_AtBitOffset_RoundTripsAndPreservesNeighbors`, `StreamReadExtensionTests.ReadShifted_AsExpected`
- **LIB-DATASOURCE-006** — When N bits are read into a caller buffer longer than ⌈N/8⌉ bytes, the source shall leave the bytes past ⌈N/8⌉ untouched.
  - Tests: `DataSourceBitAddressTests.Read_IntoOversizedBuffer_LeavesExtraBytesUntouched`
- **LIB-DATASOURCE-007** — If the caller buffer is shorter than ⌈N/8⌉ bytes, then the read shall throw `ArgumentException`.
  - Tests: untested
- **LIB-DATASOURCE-008** — If the bit count to read is negative, then the read shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **LIB-DATASOURCE-009** — If a read extends past the end of the source, then it shall throw `EndOfStreamException`; callers that must tolerate truncated data check `Length` first.
  - Tests: untested
- **LIB-DATASOURCE-010** — The asynchronous read and write overloads shall produce the same bytes as their synchronous counterparts.
  - Tests: `DataSourceBitAddressTests.WriteAsync_ThenReadAsync_AtBitOffset_RoundTripsAndPreservesNeighbors`
- **LIB-DATASOURCE-011** — `Length` shall report the current length of the source in bytes.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`

### Writing

- **LIB-DATASOURCE-012** — When N bits are written at a bit address, the source shall replace exactly those N bits and preserve every other bit of the first and last bytes touched.
  - Tests: `DataSourceBitAddressTests.Write_ThenRead_AtBitOffset_RoundTripsAndPreservesNeighbors`, `StreamWriteExtensionTests.WriteShifted_AsExpected`
- **LIB-DATASOURCE-013** — When a buffer is written at a bit address without a bit count, the source shall write all of the buffer's bits.
  - Tests: untested
- **LIB-DATASOURCE-014** — When a buffer is written without an address, the source shall write it at the current stream position left by the previous operation or `Seek`.
  - Tests: untested
- **LIB-DATASOURCE-015** — When a write extends past the end of a file source or an unbounded memory source, the source shall grow to hold it.
  - Tests: untested
- **LIB-DATASOURCE-016** — If a write extends past the capacity of a fixed-capacity memory source, then it shall throw `NotSupportedException`.
  - Tests: untested
- **LIB-DATASOURCE-017** — `Flush` shall push buffered writes to the underlying storage and shall not raise `DataWritten`.
  - Tests: untested

### Change notification

- **LIB-DATASOURCE-018** — When `NotifyDataWritten` is called, the source shall raise `DataWritten` once, synchronously, with itself as sender.
  - Tests: `ProjectTreeEventTests.DataWritten_AttachedSource_RaisesResourceChanged`, `ProjectTreeEventTests.DataFileRoot_DataWritten_RaisesResourceChanged`
- **LIB-DATASOURCE-019** — The source shall not raise `DataWritten` from `Write`, `WriteAsync`, `Flush` or `FlushAsync`; writers that change graphics data call `NotifyDataWritten` themselves (image saves in LIB-IMAGES, relink in LIB-PROJECT-SERVICE).
  - Tests: untested

### File sources

- **LIB-DATASOURCE-020** — A file source shall not open its file until the first operation that needs the stream, including reading `Length`.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`
- **LIB-DATASOURCE-021** — When a file source opens its file, it shall open it for reading and writing and allow other processes to read but not write it; if write access is denied (`UnauthorizedAccessException` or a write-protected volume), then it shall open the file for reading only.
  - Tests: `FileDataSourceTests.ReadOnlyAttribute_OpensAndReads`
- **LIB-DATASOURCE-022** — If the file cannot be opened, then every operation that needs the stream shall throw the open failure, and keep throwing it after the file reappears, until `Reopen` is called.
  - Tests: `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`
- **LIB-DATASOURCE-023** — If the file location is null, empty or whitespace, then the first operation that needs the stream shall throw `ArgumentException`.
  - Tests: untested
- **LIB-DATASOURCE-024** — `IsMissing` shall report whether a file exists at the file location at the moment it is read.
  - Tests: `ProjectServiceTests.OpenProject_MissingDataFile_LoadsWithSourceMarkedMissing`, `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`
- **LIB-DATASOURCE-025** — When `Reopen` is called, the file source shall dispose any open stream and discard a cached open failure, so the next operation opens the file again.
  - Tests: `ProjectServiceTests.Relink_CopiesFileToExpectedLocation_AndRaisesResourceChanged`
- **LIB-DATASOURCE-026** — A file source shall be serialized with the project (`ShouldBeSerialized` true by default).
  - Tests: untested
- **LIB-DATASOURCE-032** — A data source shall report `IsReadOnly`: false for memory sources, and for a file source true exactly when its file was opened for reading only; reading it opens the file if needed, and it is false while the file is missing or its open failed.
  - Tests: `FileDataSourceTests.ReadOnlyAttribute_IsReadOnly`, `FileDataSourceTests.WritableFile_IsNotReadOnly`, `FileDataSourceTests.MissingFile_IsNotReadOnly`
- **LIB-DATASOURCE-033** — If a read-only source is written by any write overload, then the write shall throw `InvalidOperationException` naming the source and change nothing; `Flush` shall do nothing.
  - Tests: `FileDataSourceTests.ReadOnly_EveryWriteOverload_ThrowsAndLeavesBytes`
- **LIB-DATASOURCE-034** — When `Reopen` is called, the file source shall decide its access again on the next open.
  - Tests: `FileDataSourceTests.Reopen_AfterClearingAttribute_IsWritable`

### Memory sources

- **LIB-DATASOURCE-027** — A memory source shall never be serialized with the project (`ShouldBeSerialized` false).
  - Tests: untested
- **LIB-DATASOURCE-028** — When a memory source is created without a capacity, it shall start empty and grow on write.
  - Tests: untested
- **LIB-DATASOURCE-029** — When a memory source is created with a capacity, it shall start zero-filled at exactly that length and never change length.
  - Tests: `IndexedImageTests.ArrangerPastEndOfSource_RendersEmptyAndSavesWithoutGrowing`

### Resource behavior

- **LIB-DATASOURCE-030** — A data source shall contain no child resources and link no other resources.
  - Tests: untested

### Disposal

- **LIB-DATASOURCE-031** — When a source is disposed, it shall dispose its stream if one was opened, releasing a file source's file handle.
  - Tests: `ProjectServiceTests.CloseProject_StandaloneFile_RaisesProjectClosedAndReleasesFile`

## Invariants

- A write changes only the addressed bits; bytes outside the written range are untouched.
- `IsMissing` is never cached; it always reflects the file system.

## Edge cases

- A read of zero bits at a byte-aligned address throws `IndexOutOfRangeException`; at a bit offset it returns an empty array.
- A bit-unaligned write whose last partial byte lies past the end of the source merges into a byte read as `0xFF` (the stream's end-of-file `-1`), so the untouched bits of that new byte become 1.
- Seeking past the end is allowed; the next read throws, the next write grows the source (or throws for a fixed-capacity memory source).
- Constructing a bit address from a negative bit count yields a negative bit offset.

## Threading and lifetime

- Each source serializes its own operations with a semaphore: one read, write, seek or flush at a time, sync or async. A `Seek` followed by an address-less `Write` is two operations and can interleave with another caller.
- `DataWritten` is raised on the calling thread. `ProjectTree` subscribes while the source is in its index and unsubscribes when it leaves (LIB-PROJECT-TREE).
- The owner disposes the source (`ProjectService` on project close). Using a source after `Dispose` throws `ObjectDisposedException`.

## Decisions

- **`DataWritten` is not raised from `Flush`.** Only explicit `NotifyDataWritten` calls raise it. Reason: palette saves also write the source, and a flush-triggered event would reload graphics editors and clear their history. Rejected: raising from `Flush` or from every `Write`.
- **A missing data file loads instead of failing.** A file source over a missing path is constructed normally and reports `IsMissing`; the first read throws. Reason: a renamed or moved ROM should not make the whole project unloadable; the user relinks it. Rejected: failing the project load.
- **`Reopen` instead of a new source.** `Lazy` caches a failed open, so relink calls `Reopen` on the same instance. Reason: the source is referenced by palettes, arrangers and tree nodes, and replacing it would mean rewriting every reference. Rejected: constructing a replacement source.
- **Memory sources are never serialized.** They hold scratch data only.
- **Read-only files open read-only.** A file that cannot be opened for writing opens for reading, `IsReadOnly` becomes true, and every arranger with an element on it is read-only through the one gate (LIB-ARRANGERS, ARCHITECTURE §6). The state is decided at open and kept until `Reopen`. Reason: ROMs extracted from archives or kept read-only on purpose must be viewable, and the read-only attribute is often the user's own protection against writing them; the gate tells the user up front instead of on Save. Rejected: opening read-write lazily on first save and failing then (the user loses the edits just made), failing the open (the old behavior), and re-checking access on every query (a file-system call per element per gate check).
- **Fall back on access denial only.** The read-only fallback runs on `UnauthorizedAccessException` (read-only attribute, ACL) or a write-protect `IOException` (`ERROR_WRITE_PROTECT`). A sharing violation still fails the open. Reason: a sharing violation is transient (an emulator holding the file), and falling back would leave the file read-only for the session without the user knowing why. Rejected: checking `File.GetAttributes` up front (misses ACLs and media, and races with the open).
- **Writes to a read-only source throw `InvalidOperationException`; `Flush` does nothing.** A backstop for callers that bypass the arranger gate, with one exception type for every source type. Rejected: letting `FileStream` throw `NotSupportedException`.

## Non-goals

- Compressed sources ([proposal](../../changes/compression-support.md)).
- Change counters or versioning of written data.

## Open items

- The write-protect fallback detects `ERROR_WRITE_PROTECT` by HResult, which is Windows-specific; a read-only mount on Linux or macOS may still fail the open.
- A zero-bit read at a byte-aligned address throws instead of returning an empty array.
- Past-end reads, fixed-capacity overflow, `Flush` not raising `DataWritten`, and the file share mode have no tests.
