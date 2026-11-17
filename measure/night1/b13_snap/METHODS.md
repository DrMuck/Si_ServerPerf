# Methods per arm: `b13_snap` (PerfProbe methods.csv; ms/s, calls/s, us/call)

## melee-snap@450

| method | A ms/s | A calls/s | A us/call | S1 ms/s | S1 calls/s | S1 us/call | S2 ms/s | S2 calls/s | S2 us/call | S3 ms/s | S3 calls/s | S3 us/call | L ms/s | L calls/s | L us/call |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| TargetFinder.CollectDetectedEnemies | 100.3 | 731 | 137.2 | 91.0 | 742 | 122.6 | 67.6 | 688 | 98.2 | 65.4 | 725 | 90.3 | 110.7 | 867 | 127.7 |
| BehaviorTrees.BTEnemyTargetDetected.ActualExecute | 140.8 | 1299 | 108.4 | 127.2 | 1265 | 100.6 | 103.8 | 1270 | 81.8 | 99.6 | 1250 | 79.7 | 136.5 | 1228 | 111.2 |
| TargetFinder.FindBestTarget | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| TargetFinder.RequestScan | 136.8 | 943 | 145.1 | 122.6 | 918 | 133.5 | 98.8 | 868 | 113.7 | 94.9 | 901 | 105.3 | 133.7 | 1011 | 132.2 |
| Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 170.5 | 60 | 2822.6 | 157.3 | 63 | 2515.8 | 135.5 | 63 | 2134.5 | 130.2 | 63 | 2075.9 | 167.7 | 66 | 2559.2 |
| LineOfSightManager.PublishResults | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| AIManager.TickAI | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |

intervals (s): A 30, S1 30, S2 20, S3 30, L 30

## melee-snap@550

| method | A ms/s | A calls/s | A us/call | S1 ms/s | S1 calls/s | S1 us/call | S2 ms/s | S2 calls/s | S2 us/call | S3 ms/s | S3 calls/s | S3 us/call | L ms/s | L calls/s | L us/call |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| TargetFinder.CollectDetectedEnemies | 169.6 | 922 | 183.9 | 144.2 | 896 | 160.9 | 114.9 | 934 | 123.0 | 107.2 | 923 | 116.2 | 177.2 | 1011 | 175.2 |
| BehaviorTrees.BTEnemyTargetDetected.ActualExecute | 220.8 | 1491 | 148.1 | 193.8 | 1476 | 131.3 | 170.3 | 1518 | 112.2 | 159.1 | 1491 | 106.7 | 213.6 | 1444 | 147.9 |
| TargetFinder.FindBestTarget | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| TargetFinder.RequestScan | 215.3 | 1128 | 190.9 | 187.2 | 1093 | 171.3 | 162.6 | 1146 | 141.9 | 151.7 | 1114 | 136.1 | 209.8 | 1178 | 178.2 |
| Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 239.7 | 22 | 10673.3 | 212.6 | 20 | 10491.7 | 191.5 | 25 | 7605.8 | 181.0 | 26 | 6877.2 | 233.2 | 24 | 9767.6 |
| LineOfSightManager.PublishResults | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| AIManager.TickAI | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |

intervals (s): A 20, S1 20, S2 20, S3 20, L 20

