# Methods per arm: `b14_dead` (PerfProbe methods.csv; ms/s, calls/s, us/call)

## melee-dead14@450

| method | A ms/s | A calls/s | A us/call | D ms/s | D calls/s | D us/call | E ms/s | E calls/s | E us/call | C ms/s | C calls/s | C us/call |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| TargetFinder.CollectDetectedEnemies | 70.7 | 485 | 145.9 | 66.3 | 457 | 145.1 | 64.6 | 448 | 144.3 | 65.1 | 447 | 145.6 |
| BehaviorTrees.BTEnemyTargetDetected.ActualExecute | 121.9 | 1402 | 86.9 | 92.9 | 1021 | 91.0 | 93.3 | 1041 | 89.6 | 101.3 | 1151 | 88.0 |
| TargetFinder.FindBestTarget | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| TargetFinder.RequestScan | 117.8 | 737 | 159.9 | 91.4 | 576 | 158.5 | 91.5 | 582 | 157.1 | 98.5 | 619 | 159.3 |
| Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 152.0 | 58 | 2639.1 | 117.7 | 62 | 1901.6 | 118.7 | 64 | 1851.9 | 131.7 | 74 | 1777.8 |
| LineOfSightManager.PublishResults | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| AIManager.TickAI | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |

intervals (s): A 60, D 60, E 80, C 60

## melee-dead14@550

| method | A ms/s | A calls/s | A us/call | D ms/s | D calls/s | D us/call | E ms/s | E calls/s | E us/call | C ms/s | C calls/s | C us/call |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| TargetFinder.CollectDetectedEnemies | 107.9 | 559 | 193.0 | 103.8 | 544 | 190.9 | 104.4 | 546 | 191.1 | 106.1 | 555 | 191.0 |
| BehaviorTrees.BTEnemyTargetDetected.ActualExecute | 179.0 | 1623 | 110.3 | 143.6 | 1233 | 116.5 | 145.2 | 1234 | 117.7 | 155.8 | 1368 | 113.9 |
| TargetFinder.FindBestTarget | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| TargetFinder.RequestScan | 174.3 | 841 | 207.3 | 142.0 | 694 | 204.7 | 143.3 | 698 | 205.4 | 152.6 | 744 | 205.2 |
| Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 199.0 | 21 | 9527.9 | 161.2 | 25 | 6407.2 | 162.6 | 25 | 6377.4 | 178.5 | 36 | 4922.5 |
| LineOfSightManager.PublishResults | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |
| AIManager.TickAI | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - | 0.0 | 0 | - |

intervals (s): A 60, D 60, E 60, C 90

