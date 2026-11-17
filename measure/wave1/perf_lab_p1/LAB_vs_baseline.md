A (baseline): base_lab_p1, base_lab_p2; B: perf_lab_p1

- `base_lab_p1`: lab `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p1\perflab\20261116_032013`, probe `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p1\perfprobe\20261116_031916`
- `base_lab_p2`: lab `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p2\perflab\20261116_040517`, probe `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p2\perfprobe\20261116_040420`
- `perf_lab_p1`: lab `C:\Users\schwe\Projects\Si_ServerPerf\measure\wave1\perf_lab_p1\perflab\20261116_073349`, probe `C:\Users\schwe\Projects\Si_ServerPerf\measure\wave1\perf_lab_p1\perfprobe\20261116_073253`

### frame work avg (ms)

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % | perf_lab_p1 | B mean | delta | delta % | abs(delta)/noise |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline-empty | 0.88 | 0.96 | 0.92 | 0.08 | 9.1 | 0.75 | 0.75 | -0.17 | -18.3 | 2.0 |
| idle-soldiers-100 | 1.25 | 1.34 | 1.29 | 0.09 | 6.8 | 0.91 | 0.91 | -0.38 | -29.4 | 4.3 |
| idle-soldiers-300 | 2.02 | 2.14 | 2.08 | 0.11 | 5.5 | 1.30 | 1.30 | -0.78 | -37.7 | 6.9 |
| idle-soldiers-600 | 3.26 | 3.40 | 3.33 | 0.14 | 4.3 | 1.97 | 1.97 | -1.36 | -40.8 | 9.6 |
| move-soldiers-100 | 1.62 | 1.66 | 1.64 | 0.03 | 2.0 | 1.34 | 1.34 | -0.30 | -18.0 | 9.3 |
| move-soldiers-300 | 2.94 | 3.00 | 2.97 | 0.06 | 2.1 | 2.50 | 2.50 | -0.46 | -15.6 | 7.5 |
| move-soldiers-600 | 4.93 | 4.93 | 4.93 | 0.01 | 0.1 | 4.03 | 4.03 | -0.90 | -18.2 | 128.2 |
| idle-vehicles-100 | 1.90 | 1.92 | 1.91 | 0.03 | 1.5 | 1.75 | 1.75 | -0.16 | -8.5 | 5.6 |
| idle-vehicles-300 | 3.82 | 3.73 | 3.78 | 0.08 | 2.2 | 3.79 | 3.79 | 0.02 | 0.5 | 0.2 |
| move-vehicles-100 | 2.29 | 2.29 | 2.29 | 0.00 | 0.0 | 2.14 | 2.14 | -0.15 | -6.4 | 147.5 |
| move-vehicles-300 | 5.96 | 5.73 | 5.84 | 0.23 | 3.9 | 5.90 | 5.90 | 0.05 | 0.9 | 0.2 |
| idle-creatures-300 | 1.97 | 2.05 | 2.01 | 0.08 | 3.9 | 1.36 | 1.36 | -0.65 | -32.3 | 8.2 |
| fight-soldiers-50 | 1.34 | 1.28 | 1.31 | 0.06 | 4.7 | 1.04 | 1.04 | -0.27 | -20.5 | 4.3 |
| fight-soldiers-150 | 2.04 | 1.85 | 1.95 | 0.19 | 9.8 | 1.51 | 1.51 | -0.44 | -22.4 | 2.3 |
| fight-soldiers-300 | 3.31 | 2.90 | 3.11 | 0.42 | 13.4 | 2.20 | 2.20 | -0.91 | -29.3 | 2.2 |
| fight-vehicles-50 | 2.13 | 2.23 | 2.18 | 0.10 | 4.7 | 1.89 | 1.89 | -0.29 | -13.4 | 2.8 |
| fight-vehicles-150 | 6.81 | 7.67 | 7.24 | 0.86 | 11.8 | 8.55 | 8.55 | 1.32 | 18.2 | 1.5 |
| fight-creatures-50 | 1.22 | 1.07 | 1.15 | 0.15 | 13.4 | 0.99 | 0.99 | -0.16 | -13.6 | 1.0 |
| fight-creatures-150 | 1.75 | 1.46 | 1.61 | 0.29 | 18.3 | 1.42 | 1.42 | -0.19 | -11.6 | 0.6 |
| structures-built-30 | 0.97 | 1.02 | 0.99 | 0.05 | 5.3 | 0.81 | 0.81 | -0.18 | -18.3 | 3.4 |
| mixed-lategame | 3.27 | 3.28 | 3.27 | 0.01 | 0.2 | 2.29 | 2.29 | -0.99 | -30.2 | 123.6 |

### frame work p95 (ms)

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % | perf_lab_p1 | B mean | delta | delta % | abs(delta)/noise |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline-empty | 1.34 | 1.67 | 1.51 | 0.34 | 22.3 | 1.31 | 1.31 | -0.20 | -13.0 | 0.6 |
| idle-soldiers-100 | 1.74 | 2.10 | 1.92 | 0.36 | 18.7 | 1.42 | 1.42 | -0.50 | -26.0 | 1.4 |
| idle-soldiers-300 | 2.67 | 3.08 | 2.88 | 0.41 | 14.1 | 1.88 | 1.88 | -0.99 | -34.5 | 2.4 |
| idle-soldiers-600 | 4.18 | 4.54 | 4.36 | 0.36 | 8.2 | 2.75 | 2.75 | -1.61 | -36.9 | 4.5 |
| move-soldiers-100 | 2.49 | 2.74 | 2.62 | 0.25 | 9.6 | 2.18 | 2.18 | -0.44 | -16.8 | 1.8 |
| move-soldiers-300 | 4.76 | 5.06 | 4.91 | 0.30 | 6.1 | 4.20 | 4.20 | -0.71 | -14.5 | 2.4 |
| move-soldiers-600 | 8.19 | 8.35 | 8.27 | 0.16 | 1.9 | 7.09 | 7.09 | -1.17 | -14.2 | 7.5 |
| idle-vehicles-100 | 4.87 | 5.00 | 4.94 | 0.13 | 2.7 | 4.95 | 4.95 | 0.01 | 0.3 | 0.1 |
| idle-vehicles-300 | 11.60 | 11.45 | 11.52 | 0.16 | 1.3 | 12.29 | 12.29 | 0.76 | 6.6 | 4.9 |
| move-vehicles-100 | 6.27 | 6.30 | 6.28 | 0.03 | 0.4 | 6.28 | 6.28 | -0.00 | -0.1 | 0.2 |
| move-vehicles-300 | 17.25 | 16.38 | 16.82 | 0.87 | 5.2 | 17.38 | 17.38 | 0.56 | 3.3 | 0.6 |
| idle-creatures-300 | 2.62 | 2.94 | 2.78 | 0.31 | 11.3 | 1.98 | 1.98 | -0.81 | -29.0 | 2.6 |
| fight-soldiers-50 | 2.15 | 2.22 | 2.19 | 0.06 | 2.8 | 1.82 | 1.82 | -0.37 | -16.8 | 6.0 |
| fight-soldiers-150 | 3.88 | 3.35 | 3.61 | 0.52 | 14.4 | 2.90 | 2.90 | -0.71 | -19.7 | 1.4 |
| fight-soldiers-300 | 8.03 | 5.93 | 6.98 | 2.10 | 30.2 | 4.71 | 4.71 | -2.27 | -32.5 | 1.1 |
| fight-vehicles-50 | 6.84 | 6.43 | 6.63 | 0.42 | 6.3 | 5.91 | 5.91 | -0.73 | -11.0 | 1.8 |
| fight-vehicles-150 | 21.41 | 20.44 | 20.92 | 0.97 | 4.6 | 22.44 | 22.44 | 1.52 | 7.3 | 1.6 |
| fight-creatures-50 | 1.93 | 1.87 | 1.90 | 0.06 | 3.1 | 1.78 | 1.78 | -0.12 | -6.1 | 2.0 |
| fight-creatures-150 | 3.02 | 3.05 | 3.04 | 0.02 | 0.8 | 2.90 | 2.90 | -0.14 | -4.6 | 5.6 |
| structures-built-30 | 1.45 | 1.73 | 1.59 | 0.27 | 17.2 | 1.38 | 1.38 | -0.21 | -13.0 | 0.8 |
| mixed-lategame | 4.71 | 4.99 | 4.85 | 0.28 | 5.8 | 3.56 | 3.56 | -1.30 | -26.7 | 4.6 |

### main-thread CPU (ms per wall-second)

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % | perf_lab_p1 | B mean | delta | delta % | abs(delta)/noise |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline-empty | 139.06 | 172.50 | 155.78 | 33.44 | 21.5 | 121.25 | 121.25 | -34.53 | -22.2 | 1.0 |
| idle-soldiers-100 | 179.69 | 201.56 | 190.63 | 21.88 | 11.5 | 163.44 | 163.44 | -27.19 | -14.3 | 1.2 |
| idle-soldiers-300 | 282.19 | 286.82 | 284.50 | 4.63 | 1.6 | 184.38 | 184.38 | -100.13 | -35.2 | 21.6 |
| idle-soldiers-600 | 426.31 | 425.77 | 426.04 | 0.54 | 0.1 | 278.64 | 278.64 | -147.40 | -34.6 | 273.7 |
| move-soldiers-100 | 236.88 | 235.89 | 236.38 | 0.99 | 0.4 | 194.30 | 194.30 | -42.09 | -17.8 | 42.7 |
| move-soldiers-300 | 385.78 | 391.41 | 388.60 | 5.62 | 1.4 | 318.12 | 318.12 | -70.47 | -18.1 | 12.5 |
| move-soldiers-600 | 624.88 | 626.82 | 625.85 | 1.94 | 0.3 | 528.65 | 528.65 | -97.20 | -15.5 | 50.0 |
| idle-vehicles-100 | 265.54 | 290.26 | 277.90 | 24.72 | 8.9 | 231.25 | 231.25 | -46.65 | -16.8 | 1.9 |
| idle-vehicles-300 | 424.43 | 431.39 | 427.91 | 6.96 | 1.6 | 424.83 | 424.83 | -3.08 | -0.7 | 0.4 |
| move-vehicles-100 | 303.44 | 310.50 | 306.97 | 7.06 | 2.3 | 296.50 | 296.50 | -10.47 | -3.4 | 1.5 |
| move-vehicles-300 | 553.42 | 548.11 | 550.76 | 5.31 | 1.0 | 544.99 | 544.99 | -5.78 | -1.0 | 1.1 |
| idle-creatures-300 | 274.95 | 290.63 | 282.79 | 15.68 | 5.5 | 216.56 | 216.56 | -66.22 | -23.4 | 4.2 |
| fight-soldiers-50 | 196.58 | 200.59 | 198.59 | 4.01 | 2.0 | 194.96 | 194.96 | -3.62 | -1.8 | 0.9 |
| fight-soldiers-150 | 290.63 | 245.57 | 268.10 | 45.05 | 16.8 | 236.52 | 236.52 | -31.58 | -11.8 | 0.7 |
| fight-soldiers-300 | 416.17 | 364.30 | 390.24 | 51.87 | 13.3 | 312.63 | 312.63 | -77.61 | -19.9 | 1.5 |
| fight-vehicles-50 | 287.44 | 291.88 | 289.66 | 4.43 | 1.5 | 271.46 | 271.46 | -18.21 | -6.3 | 4.1 |
| fight-vehicles-150 | 621.69 | 677.76 | 649.73 | 56.07 | 8.6 | 705.48 | 705.48 | 55.76 | 8.6 | 1.0 |
| fight-creatures-50 | 191.93 | 165.00 | 178.47 | 26.93 | 15.1 | 168.75 | 168.75 | -9.72 | -5.4 | 0.4 |
| fight-creatures-150 | 264.95 | 204.65 | 234.80 | 60.30 | 25.7 | 211.25 | 211.25 | -23.55 | -10.0 | 0.4 |
| structures-built-30 | 154.95 | 168.75 | 161.85 | 13.80 | 8.5 | 153.91 | 153.91 | -7.94 | -4.9 | 0.6 |
| mixed-lategame | 428.95 | 423.73 | 426.34 | 5.22 | 1.2 | 315.88 | 315.88 | -110.46 | -25.9 | 21.2 |

### fps

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % | perf_lab_p1 | B mean | delta | delta % | abs(delta)/noise |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline-empty | 119.61 | 119.59 | 119.60 | 0.02 | 0.0 | 119.59 | 119.59 | -0.00 | -0.0 | 0.1 |
| idle-soldiers-100 | 119.59 | 119.58 | 119.59 | 0.01 | 0.0 | 119.59 | 119.59 | 0.00 | 0.0 | 0.3 |
| idle-soldiers-300 | 119.53 | 119.47 | 119.50 | 0.06 | 0.0 | 119.57 | 119.57 | 0.07 | 0.1 | 1.2 |
| idle-soldiers-600 | 119.49 | 119.48 | 119.49 | 0.01 | 0.0 | 119.52 | 119.52 | 0.03 | 0.0 | 5.0 |
| move-soldiers-100 | 119.58 | 119.53 | 119.55 | 0.06 | 0.0 | 119.57 | 119.57 | 0.01 | 0.0 | 0.2 |
| move-soldiers-300 | 119.54 | 119.54 | 119.54 | 0.00 | 0.0 | 119.54 | 119.54 | 0.00 | 0.0 | 1.0 |
| move-soldiers-600 | 119.29 | 119.10 | 119.20 | 0.19 | 0.2 | 119.41 | 119.41 | 0.21 | 0.2 | 1.1 |
| idle-vehicles-100 | 119.56 | 119.57 | 119.57 | 0.01 | 0.0 | 119.55 | 119.55 | -0.02 | -0.0 | 2.3 |
| idle-vehicles-300 | 110.55 | 111.28 | 110.92 | 0.73 | 0.7 | 108.55 | 108.55 | -2.37 | -2.1 | 3.2 |
| move-vehicles-100 | 119.55 | 119.48 | 119.51 | 0.07 | 0.1 | 119.51 | 119.51 | -0.00 | -0.0 | 0.1 |
| move-vehicles-300 | 94.83 | 96.53 | 95.68 | 1.70 | 1.8 | 93.61 | 93.61 | -2.07 | -2.2 | 1.2 |
| idle-creatures-300 | 119.56 | 119.56 | 119.56 | 0.00 | 0.0 | 119.56 | 119.56 | 0.00 | 0.0 | 0.8 |
| fight-soldiers-50 | 119.56 | 119.58 | 119.57 | 0.03 | 0.0 | 119.56 | 119.56 | -0.01 | -0.0 | 0.5 |
| fight-soldiers-150 | 119.55 | 119.58 | 119.56 | 0.04 | 0.0 | 119.58 | 119.58 | 0.01 | 0.0 | 0.3 |
| fight-soldiers-300 | 117.71 | 119.50 | 118.61 | 1.79 | 1.5 | 119.55 | 119.55 | 0.94 | 0.8 | 0.5 |
| fight-vehicles-50 | 119.53 | 119.57 | 119.55 | 0.04 | 0.0 | 119.57 | 119.57 | 0.03 | 0.0 | 0.7 |
| fight-vehicles-150 | 96.06 | 91.87 | 93.96 | 4.20 | 4.5 | 85.81 | 85.81 | -8.15 | -8.7 | 1.9 |
| fight-creatures-50 | 119.58 | 119.57 | 119.57 | 0.01 | 0.0 | 119.59 | 119.59 | 0.01 | 0.0 | 1.3 |
| fight-creatures-150 | 119.56 | 119.58 | 119.57 | 0.02 | 0.0 | 119.53 | 119.53 | -0.04 | -0.0 | 2.0 |
| structures-built-30 | 119.60 | 119.57 | 119.58 | 0.03 | 0.0 | 119.59 | 119.59 | 0.01 | 0.0 | 0.3 |
| mixed-lategame | 119.49 | 119.53 | 119.51 | 0.04 | 0.0 | 119.55 | 119.55 | 0.04 | 0.0 | 0.9 |

### Largest section changes per scenario (B mean - A mean, main-thread ms per wall-second)

- **baseline-empty**: Update.ScriptRunBehaviourUpdate -12.3 (A 68.0); PreLateUpdate.DirectorUpdateAnimationBegin -7.9 (A 7.9); Ordered Update Manager - update: Soldier.OrderedUpdate -2.9 (A 4.6); GameManager.Update: FogOfWar.OnUpdate -2.3 (A 4.0); FixedUpdate.PhysicsFixedUpdate -1.2 (A 9.8); GameManager.Update: NetworkComponent.TickNetworkObjects -0.6 (A 0.8)
- **idle-soldiers-100**: Update.ScriptRunBehaviourUpdate -29.9 (A 103.0); Ordered Update Manager - update: Soldier.OrderedUpdate -16.9 (A 23.7); PreLateUpdate.DirectorUpdateAnimationBegin -11.5 (A 11.5); GameManager.Update: FogOfWar.OnUpdate -6.1 (A 8.5); FixedUpdate.PhysicsFixedUpdate -4.4 (A 10.0); FixedUpdate.ScriptRunBehaviourFixedUpdate -1.8 (A 6.7)
- **idle-soldiers-300**: Update.ScriptRunBehaviourUpdate -70.2 (A 173.6); Ordered Update Manager - update: Soldier.OrderedUpdate -46.7 (A 62.4); PreLateUpdate.DirectorUpdateAnimationBegin -17.6 (A 17.6); GameManager.Update: FogOfWar.OnUpdate -13.6 (A 17.1); FixedUpdate.ScriptRunBehaviourFixedUpdate -4.4 (A 12.6); GameManager.Update: NetworkComponent.TickNetworkObjects -3.8 (A 4.0)
- **idle-soldiers-600**: Update.ScriptRunBehaviourUpdate -120.6 (A 279.5); Ordered Update Manager - update: Soldier.OrderedUpdate -86.8 (A 119.9); PreLateUpdate.DirectorUpdateAnimationBegin -35.8 (A 35.8); GameManager.Update: FogOfWar.OnUpdate -23.4 (A 28.4); GameManager.Update: NetworkComponent.TickNetworkObjects -7.2 (A 7.3); FixedUpdate.ScriptRunBehaviourFixedUpdate -5.2 (A 21.5)
- **move-soldiers-100**: Update.ScriptRunBehaviourUpdate -19.6 (A 121.2); PreLateUpdate.DirectorUpdateAnimationBegin -12.2 (A 12.2); GameManager.Update: FogOfWar.OnUpdate -6.5 (A 8.9); Ordered Update Manager - update: Soldier.OrderedUpdate -5.2 (A 29.9); FixedUpdate.PhysicsFixedUpdate -2.9 (A 17.1); GameManager.Update: NetworkComponent.TickNetworkObjects -2.1 (A 2.2)
- **move-soldiers-300**: Update.ScriptRunBehaviourUpdate -31.9 (A 222.0); PreLateUpdate.DirectorUpdateAnimationBegin -19.5 (A 19.6); GameManager.Update: FogOfWar.OnUpdate -13.7 (A 17.1); Ordered Update Manager - update: Soldier.OrderedUpdate -9.4 (A 77.5); GameManager.Update: NetworkComponent.TickNetworkObjects -5.0 (A 5.1); FixedUpdate.PhysicsFixedUpdate -2.6 (A 26.4)
- **move-soldiers-600**: Update.ScriptRunBehaviourUpdate -56.1 (A 356.3); PreLateUpdate.DirectorUpdateAnimationBegin -42.8 (A 42.8); GameManager.Update: FogOfWar.OnUpdate -23.1 (A 27.6); Ordered Update Manager - update: Soldier.OrderedUpdate -21.1 (A 145.1); GameManager.Update: NetworkComponent.TickNetworkObjects -8.7 (A 8.8); FixedUpdate.ScriptRunBehaviourFixedUpdate -4.3 (A 54.5)
- **idle-vehicles-100**: Update.ScriptRunBehaviourUpdate -17.7 (A 104.5); PreLateUpdate.DirectorUpdateAnimationBegin -7.0 (A 7.0); GameManager.Update: FogOfWar.OnUpdate -6.9 (A 9.2); Ordered Update Manager - update: Soldier.OrderedUpdate -2.8 (A 4.3); GameManager.Update: NetworkComponent.TickNetworkObjects -1.7 (A 1.8); Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate -1.1 (A 1.6)
- **idle-vehicles-300**: Update.ScriptRunBehaviourUpdate -25.1 (A 152.5); GameManager.Update: FogOfWar.OnUpdate -12.7 (A 15.6); PreLateUpdate.DirectorUpdateAnimationBegin -5.7 (A 5.7); GameManager.Update: NetworkComponent.TickNetworkObjects -3.5 (A 3.6); Ordered Update Manager - update: Soldier.OrderedUpdate -2.3 (A 3.6); Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate -2.1 (A 3.5)
- **move-vehicles-100**: Update.ScriptRunBehaviourUpdate -14.9 (A 121.4); PreLateUpdate.DirectorUpdateAnimationBegin -6.9 (A 6.9); GameManager.Update: FogOfWar.OnUpdate -6.8 (A 9.1); Ordered Update Manager - update: Soldier.OrderedUpdate -2.7 (A 4.2); GameManager.Update: NetworkComponent.TickNetworkObjects -1.9 (A 2.1); GameManager.Update: Unit.CompleteUpdateConcealmentAll -0.7 (A 2.6)
- **move-vehicles-300**: Update.ScriptRunBehaviourUpdate -23.8 (A 185.8); GameManager.Update: FogOfWar.OnUpdate -11.1 (A 14.0); PreLateUpdate.DirectorUpdateAnimationBegin -4.9 (A 4.9); GameManager.Update: NetworkComponent.TickNetworkObjects -4.4 (A 4.5); Ordered Update Manager - update: Soldier.OrderedUpdate -2.0 (A 3.2); Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate -1.4 (A 8.8)
- **idle-creatures-300**: PreLateUpdate.DirectorUpdateAnimationBegin -44.0 (A 44.0); Update.ScriptRunBehaviourUpdate -28.4 (A 143.0); GameManager.Update: FogOfWar.OnUpdate -14.7 (A 18.3); GameManager.Update: NetworkComponent.TickNetworkObjects -3.5 (A 3.7); FixedUpdate.ScriptRunBehaviourFixedUpdate -3.2 (A 10.5); FixedUpdate.PhysicsFixedUpdate -2.9 (A 12.3)
- **fight-soldiers-50**: Update.ScriptRunBehaviourUpdate -17.9 (A 97.0); PreLateUpdate.DirectorUpdateAnimationBegin -11.1 (A 11.1); GameManager.Update: FogOfWar.OnUpdate -4.9 (A 6.9); Ordered Update Manager - update: Soldier.OrderedUpdate -4.7 (A 18.0); FixedUpdate.PhysicsFixedUpdate -1.9 (A 13.6); Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents -1.5 (A 7.0)
- **fight-soldiers-150**: Update.ScriptRunBehaviourUpdate -35.0 (A 147.6); PreLateUpdate.DirectorUpdateAnimationBegin -13.8 (A 13.8); Ordered Update Manager - update: Soldier.OrderedUpdate -9.3 (A 39.2); GameManager.Update: FogOfWar.OnUpdate -8.6 (A 10.6); Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents -6.5 (A 18.0); FixedUpdate.ScriptRunBehaviourFixedUpdate -4.8 (A 13.2)
- **fight-soldiers-300**: Update.ScriptRunBehaviourUpdate -64.9 (A 222.5); Ordered Update Manager - update: Soldier.OrderedUpdate -20.7 (A 72.8); PreLateUpdate.DirectorUpdateAnimationBegin -20.7 (A 20.7); Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents -14.7 (A 35.4); GameManager.Update: FogOfWar.OnUpdate -13.1 (A 15.5); FixedUpdate.ScriptRunBehaviourFixedUpdate -6.7 (A 20.5)
- **fight-vehicles-50**: Update.ScriptRunBehaviourUpdate -19.7 (A 129.0); FixedUpdate.PhysicsFixedUpdate -7.0 (A 66.6); PreLateUpdate.DirectorUpdateAnimationBegin -6.6 (A 6.7); GameManager.Update: FogOfWar.OnUpdate -5.4 (A 7.4); Ordered Update Manager - update: Soldier.OrderedUpdate -2.7 (A 4.3); Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets -2.0 (A 28.4)
- **fight-vehicles-150**: TimeUpdate.WaitForLastPresentationAndUpdateTime -62.2 (A 347.3); GameManager.Update: FogOfWar.OnUpdate -9.9 (A 12.4); PreLateUpdate.DirectorUpdateAnimationBegin -7.1 (A 7.1); Update.ScriptRunBehaviourUpdate -4.4 (A 292.7); GameManager.Update: NetworkComponent.TickNetworkObjects -3.3 (A 3.4); Ordered Update Manager - update: Soldier.OrderedUpdate -2.1 (A 3.1)
- **fight-creatures-50**: PreLateUpdate.DirectorUpdateAnimationBegin -10.3 (A 10.3); Update.ScriptRunBehaviourUpdate -9.5 (A 84.4); GameManager.Update: FogOfWar.OnUpdate -4.0 (A 6.0); Ordered Update Manager - update: Soldier.OrderedUpdate -1.5 (A 11.5); GameManager.Update: NetworkComponent.TickNetworkObjects -1.0 (A 1.1); FixedUpdate.PhysicsFixedUpdate -0.9 (A 12.5)
- **fight-creatures-150**: PreLateUpdate.DirectorUpdateAnimationBegin -14.2 (A 14.2); Update.ScriptRunBehaviourUpdate -10.1 (A 117.3); GameManager.Update: FogOfWar.OnUpdate -7.0 (A 9.6); Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate -2.5 (A 3.2); GameManager.Update: NetworkComponent.TickNetworkObjects -1.8 (A 1.9); Ordered Update Manager - update: CreatureDecapod.OrderedUpdate -1.6 (A 4.6)
- **structures-built-30**: Update.ScriptRunBehaviourUpdate -13.9 (A 76.2); PreLateUpdate.DirectorUpdateAnimationBegin -7.3 (A 7.3); GameManager.Update: FogOfWar.OnUpdate -4.1 (A 5.8); Ordered Update Manager - update: Soldier.OrderedUpdate -2.9 (A 4.5); Ordered Update Manager - update: Structure.OrderedUpdate -1.6 (A 1.8); FixedUpdate.PhysicsFixedUpdate -1.1 (A 9.8)
- **mixed-lategame**: Update.ScriptRunBehaviourUpdate -88.4 (A 265.4); Ordered Update Manager - update: Soldier.OrderedUpdate -48.2 (A 88.5); GameManager.Update: FogOfWar.OnUpdate -27.9 (A 32.9); PreLateUpdate.DirectorUpdateAnimationBegin -25.9 (A 25.9); Ordered Update Manager - update: Structure.OrderedUpdate -8.5 (A 8.9); GameManager.Update: NetworkComponent.TickNetworkObjects -6.4 (A 6.5)

