# TileShop.CLI specs

One spec per feature; format and usage in [../README.md](../README.md).

| Spec | Id | Owns |
|---|---|---|
| [CLI commands](cli.md) | CLI-COMMANDS | `Program`, `ExitCode`, the verb options (`PrintOptions`, `ExportOptions`, `ExportAllOptions`, `ImportOptions`, `ImportAllOptions`), `ProjectCommandHandler` and its handlers, `Exporter`, `Importer`, `ImportResult` |
| [Release packaging and CI](publish.md) | CLI-PUBLISH | `publish.ps1`, `TileShop.CLI.csproj` build settings, `Directory.Build.targets` versioning, the CI and release workflows |
