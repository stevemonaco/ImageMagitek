# FileStreamReopenPerRead
``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.557 (1809/October2018Update/Redstone5)
Intel Core i7-2600K CPU 3.40GHz (Sandy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.300
  [Host]     : .NET Core 2.2.5 (CoreCLR 4.6.27617.05, CoreFX 4.6.27618.01), 64bit RyuJIT
  DefaultJob : .NET Core 2.2.5 (CoreCLR 4.6.27617.05, CoreFX 4.6.27618.01), 64bit RyuJIT


```
|        Method |    N | TotalReadSize |         Mean |       Error |      StdDev |
|-------------- |----- |-------------- |-------------:|------------:|------------:|
|      **KeepOpen** | **1000** |            **16** |     **44.02 us** |   **0.2526 us** |   **0.2362 us** |
| ReopenPerRead | 1000 |            16 |     44.43 us |   0.2273 us |   0.1775 us |
|      **KeepOpen** | **1000** |            **64** |     **53.20 us** |   **0.1759 us** |   **0.1559 us** |
| ReopenPerRead | 1000 |            64 |    177.80 us |   0.9408 us |   0.7856 us |
|      **KeepOpen** | **1000** |           **256** |     **82.95 us** |   **0.4771 us** |   **0.4463 us** |
| ReopenPerRead | 1000 |           256 |    703.03 us |   2.1382 us |   2.0001 us |
|      **KeepOpen** | **1000** |           **512** |    **125.61 us** |   **0.6964 us** |   **0.5815 us** |
| ReopenPerRead | 1000 |           512 |  1,408.45 us |   8.4856 us |   7.9375 us |
|      **KeepOpen** | **1000** |         **16384** |  **2,677.68 us** |  **42.8831 us** |  **38.0147 us** |
| ReopenPerRead | 1000 |         16384 | 44,844.35 us | 249.6111 us | 221.2738 us |

# Snes3bppDecodeToImage
``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.615 (1809/October2018Update/Redstone5)
Intel Core i7-2600K CPU 3.40GHz (Sandy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.401
  [Host]     : .NET Core 2.2.6 (CoreCLR 4.6.27817.03, CoreFX 4.6.27818.02), 64bit RyuJIT
  DefaultJob : .NET Core 2.2.6 (CoreCLR 4.6.27817.03, CoreFX 4.6.27818.02), 64bit RyuJIT


```
|        Method |     Mean |    Error |   StdDev | Ratio | RatioSD |
|-------------- |---------:|---------:|---------:|------:|--------:|
|  DecodeNative | 415.1 ms | 6.669 ms | 5.912 ms |  1.00 |    0.00 |
| DecodeGeneric | 457.3 ms | 8.464 ms | 8.313 ms |  1.10 |    0.02 |
