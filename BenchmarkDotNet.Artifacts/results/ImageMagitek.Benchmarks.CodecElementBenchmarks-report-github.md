```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method      | CodecName        | Mean         | Error         | StdDev       | Gen0   | Allocated |
|------------ |----------------- |-------------:|--------------:|-------------:|-------:|----------:|
| **Decode**      | **PSX 4bpp Flow**    | **15,158.01 ns** | **26,154.540 ns** | **1,433.619 ns** |      **-** |         **-** |
| Encode      | PSX 4bpp Flow    |  9,762.41 ns |  1,184.248 ns |    64.913 ns |      - |         - |
| ReadElement | PSX 4bpp Flow    |     34.33 ns |      7.094 ns |     0.389 ns |      - |         - |
| **Decode**      | **SNES 3bpp**        |    **465.68 ns** |    **446.895 ns** |    **24.496 ns** | **0.0029** |      **48 B** |
| Encode      | SNES 3bpp        |    656.53 ns |  1,736.811 ns |    95.200 ns | 0.0057 |      96 B |
| ReadElement | SNES 3bpp        |     31.14 ns |     18.879 ns |     1.035 ns | 0.0029 |      48 B |
| **Decode**      | **SNES 3bpp Flow**   |    **197.98 ns** |  **1,026.775 ns** |    **56.281 ns** |      **-** |         **-** |
| Encode      | SNES 3bpp Flow   |    179.47 ns |    107.866 ns |     5.913 ns |      - |         - |
| ReadElement | SNES 3bpp Flow   |     35.93 ns |     10.378 ns |     0.569 ns |      - |         - |
| **Decode**      | **SNES4bpp Pattern** |    **352.31 ns** |     **49.049 ns** |     **2.689 ns** |      **-** |         **-** |
| Encode      | SNES4bpp Pattern |    181.24 ns |    398.042 ns |    21.818 ns |      - |         - |
| ReadElement | SNES4bpp Pattern |     30.88 ns |      6.638 ns |     0.364 ns |      - |         - |
