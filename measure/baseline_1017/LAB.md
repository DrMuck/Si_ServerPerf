A (baseline): base_lab_p1, base_lab_p2

- `base_lab_p1`: lab `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p1\perflab\20261116_032013`, probe `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p1\perfprobe\20261116_031916`
- `base_lab_p2`: lab `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p2\perflab\20261116_040517`, probe `C:\Users\schwe\Projects\Si_ServerPerf\measure\baseline_1017\base_lab_p2\perfprobe\20261116_040420`

### frame work avg (ms)

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % |
|---|---:|---:|---:|---:|---:|
| baseline-empty | 0.88 | 0.96 | 0.92 | 0.08 | 9.1 |
| idle-soldiers-100 | 1.25 | 1.34 | 1.29 | 0.09 | 6.8 |
| idle-soldiers-300 | 2.02 | 2.14 | 2.08 | 0.11 | 5.5 |
| idle-soldiers-600 | 3.26 | 3.40 | 3.33 | 0.14 | 4.3 |
| move-soldiers-100 | 1.62 | 1.66 | 1.64 | 0.03 | 2.0 |
| move-soldiers-300 | 2.94 | 3.00 | 2.97 | 0.06 | 2.1 |
| move-soldiers-600 | 4.93 | 4.93 | 4.93 | 0.01 | 0.1 |
| idle-vehicles-100 | 1.90 | 1.92 | 1.91 | 0.03 | 1.5 |
| idle-vehicles-300 | 3.82 | 3.73 | 3.78 | 0.08 | 2.2 |
| move-vehicles-100 | 2.29 | 2.29 | 2.29 | 0.00 | 0.0 |
| move-vehicles-300 | 5.96 | 5.73 | 5.84 | 0.23 | 3.9 |
| idle-creatures-300 | 1.97 | 2.05 | 2.01 | 0.08 | 3.9 |
| fight-soldiers-50 | 1.34 | 1.28 | 1.31 | 0.06 | 4.7 |
| fight-soldiers-150 | 2.04 | 1.85 | 1.95 | 0.19 | 9.8 |
| fight-soldiers-300 | 3.31 | 2.90 | 3.11 | 0.42 | 13.4 |
| fight-vehicles-50 | 2.13 | 2.23 | 2.18 | 0.10 | 4.7 |
| fight-vehicles-150 | 6.81 | 7.67 | 7.24 | 0.86 | 11.8 |
| fight-creatures-50 | 1.22 | 1.07 | 1.15 | 0.15 | 13.4 |
| fight-creatures-150 | 1.75 | 1.46 | 1.61 | 0.29 | 18.3 |
| structures-built-30 | 0.97 | 1.02 | 0.99 | 0.05 | 5.3 |
| mixed-lategame | 3.27 | 3.28 | 3.27 | 0.01 | 0.2 |

### frame work p95 (ms)

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % |
|---|---:|---:|---:|---:|---:|
| baseline-empty | 1.34 | 1.67 | 1.51 | 0.34 | 22.3 |
| idle-soldiers-100 | 1.74 | 2.10 | 1.92 | 0.36 | 18.7 |
| idle-soldiers-300 | 2.67 | 3.08 | 2.88 | 0.41 | 14.1 |
| idle-soldiers-600 | 4.18 | 4.54 | 4.36 | 0.36 | 8.2 |
| move-soldiers-100 | 2.49 | 2.74 | 2.62 | 0.25 | 9.6 |
| move-soldiers-300 | 4.76 | 5.06 | 4.91 | 0.30 | 6.1 |
| move-soldiers-600 | 8.19 | 8.35 | 8.27 | 0.16 | 1.9 |
| idle-vehicles-100 | 4.87 | 5.00 | 4.94 | 0.13 | 2.7 |
| idle-vehicles-300 | 11.60 | 11.45 | 11.52 | 0.16 | 1.3 |
| move-vehicles-100 | 6.27 | 6.30 | 6.28 | 0.03 | 0.4 |
| move-vehicles-300 | 17.25 | 16.38 | 16.82 | 0.87 | 5.2 |
| idle-creatures-300 | 2.62 | 2.94 | 2.78 | 0.31 | 11.3 |
| fight-soldiers-50 | 2.15 | 2.22 | 2.19 | 0.06 | 2.8 |
| fight-soldiers-150 | 3.88 | 3.35 | 3.61 | 0.52 | 14.4 |
| fight-soldiers-300 | 8.03 | 5.93 | 6.98 | 2.10 | 30.2 |
| fight-vehicles-50 | 6.84 | 6.43 | 6.63 | 0.42 | 6.3 |
| fight-vehicles-150 | 21.41 | 20.44 | 20.92 | 0.97 | 4.6 |
| fight-creatures-50 | 1.93 | 1.87 | 1.90 | 0.06 | 3.1 |
| fight-creatures-150 | 3.02 | 3.05 | 3.04 | 0.02 | 0.8 |
| structures-built-30 | 1.45 | 1.73 | 1.59 | 0.27 | 17.2 |
| mixed-lategame | 4.71 | 4.99 | 4.85 | 0.28 | 5.8 |

### main-thread CPU (ms per wall-second)

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % |
|---|---:|---:|---:|---:|---:|
| baseline-empty | 139.06 | 172.50 | 155.78 | 33.44 | 21.5 |
| idle-soldiers-100 | 179.69 | 201.56 | 190.63 | 21.88 | 11.5 |
| idle-soldiers-300 | 282.19 | 286.82 | 284.50 | 4.63 | 1.6 |
| idle-soldiers-600 | 426.31 | 425.77 | 426.04 | 0.54 | 0.1 |
| move-soldiers-100 | 236.88 | 235.89 | 236.38 | 0.99 | 0.4 |
| move-soldiers-300 | 385.78 | 391.41 | 388.60 | 5.62 | 1.4 |
| move-soldiers-600 | 624.88 | 626.82 | 625.85 | 1.94 | 0.3 |
| idle-vehicles-100 | 265.54 | 290.26 | 277.90 | 24.72 | 8.9 |
| idle-vehicles-300 | 424.43 | 431.39 | 427.91 | 6.96 | 1.6 |
| move-vehicles-100 | 303.44 | 310.50 | 306.97 | 7.06 | 2.3 |
| move-vehicles-300 | 553.42 | 548.11 | 550.76 | 5.31 | 1.0 |
| idle-creatures-300 | 274.95 | 290.63 | 282.79 | 15.68 | 5.5 |
| fight-soldiers-50 | 196.58 | 200.59 | 198.59 | 4.01 | 2.0 |
| fight-soldiers-150 | 290.63 | 245.57 | 268.10 | 45.05 | 16.8 |
| fight-soldiers-300 | 416.17 | 364.30 | 390.24 | 51.87 | 13.3 |
| fight-vehicles-50 | 287.44 | 291.88 | 289.66 | 4.43 | 1.5 |
| fight-vehicles-150 | 621.69 | 677.76 | 649.73 | 56.07 | 8.6 |
| fight-creatures-50 | 191.93 | 165.00 | 178.47 | 26.93 | 15.1 |
| fight-creatures-150 | 264.95 | 204.65 | 234.80 | 60.30 | 25.7 |
| structures-built-30 | 154.95 | 168.75 | 161.85 | 13.80 | 8.5 |
| mixed-lategame | 428.95 | 423.73 | 426.34 | 5.22 | 1.2 |

### fps

| scenario | base_lab_p1 | base_lab_p2 | A mean | A spread | A spread % |
|---|---:|---:|---:|---:|---:|
| baseline-empty | 119.61 | 119.59 | 119.60 | 0.02 | 0.0 |
| idle-soldiers-100 | 119.59 | 119.58 | 119.59 | 0.01 | 0.0 |
| idle-soldiers-300 | 119.53 | 119.47 | 119.50 | 0.06 | 0.0 |
| idle-soldiers-600 | 119.49 | 119.48 | 119.49 | 0.01 | 0.0 |
| move-soldiers-100 | 119.58 | 119.53 | 119.55 | 0.06 | 0.0 |
| move-soldiers-300 | 119.54 | 119.54 | 119.54 | 0.00 | 0.0 |
| move-soldiers-600 | 119.29 | 119.10 | 119.20 | 0.19 | 0.2 |
| idle-vehicles-100 | 119.56 | 119.57 | 119.57 | 0.01 | 0.0 |
| idle-vehicles-300 | 110.55 | 111.28 | 110.92 | 0.73 | 0.7 |
| move-vehicles-100 | 119.55 | 119.48 | 119.51 | 0.07 | 0.1 |
| move-vehicles-300 | 94.83 | 96.53 | 95.68 | 1.70 | 1.8 |
| idle-creatures-300 | 119.56 | 119.56 | 119.56 | 0.00 | 0.0 |
| fight-soldiers-50 | 119.56 | 119.58 | 119.57 | 0.03 | 0.0 |
| fight-soldiers-150 | 119.55 | 119.58 | 119.56 | 0.04 | 0.0 |
| fight-soldiers-300 | 117.71 | 119.50 | 118.61 | 1.79 | 1.5 |
| fight-vehicles-50 | 119.53 | 119.57 | 119.55 | 0.04 | 0.0 |
| fight-vehicles-150 | 96.06 | 91.87 | 93.96 | 4.20 | 4.5 |
| fight-creatures-50 | 119.58 | 119.57 | 119.57 | 0.01 | 0.0 |
| fight-creatures-150 | 119.56 | 119.58 | 119.57 | 0.02 | 0.0 |
| structures-built-30 | 119.60 | 119.57 | 119.58 | 0.03 | 0.0 |
| mixed-lategame | 119.49 | 119.53 | 119.51 | 0.04 | 0.0 |

