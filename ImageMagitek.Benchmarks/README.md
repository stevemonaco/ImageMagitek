
# FileStreamReopenPerRead

|        Method | TotalReadSize |         Mean |       Error |      StdDev |
|-------------- |-------------- |-------------:|------------:|------------:|
|      KeepOpen |            16 |     44.02 us |   0.2526 us |   0.2362 us |
| ReopenPerRead |            16 |     44.43 us |   0.2273 us |   0.1775 us |
|      KeepOpen |            64 |     53.20 us |   0.1759 us |   0.1559 us |
| ReopenPerRead |            64 |    177.80 us |   0.9408 us |   0.7856 us |
|      KeepOpen |           256 |     82.95 us |   0.4771 us |   0.4463 us |
| ReopenPerRead |           256 |    703.03 us |   2.1382 us |   2.0001 us |
|      KeepOpen |           512 |    125.61 us |   0.6964 us |   0.5815 us |
| ReopenPerRead |           512 |  1,408.45 us |   8.4856 us |   7.9375 us |
|      KeepOpen |         16384 |  2,677.68 us |  42.8831 us |  38.0147 us |
| ReopenPerRead |         16384 | 44,844.35 us | 249.6111 us | 221.2738 us |