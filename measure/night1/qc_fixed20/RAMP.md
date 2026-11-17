## Continuous climb: quick-trickle (status ok, notes: climb stopped: fps_floor (median 9.5 < 10) at 970)

Fit per arm, U = lab units:  work_ms = a + b*U + c*U^2 (frame-weighted); frame_ms = 1000/fps fitted the same way.

| arm | s | U range | a | b (ms/unit) | c (ms/unit^2) | rmse | work@300 | work@500 | work@800 | work@1000 | frame_ms@800 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| def | 468 | 103-960 | 3.166 | -0.02686 | 1.64e-04 | 6.23 | 9.84 | 30.65 | 86.41 | 139.96 | 82.07 |
| def20 | 479 | 91-960 | 6.064 | -0.04366 | 1.67e-04 | 6.22 | 7.97 | 25.92 | 77.86 | 129.16 | 74.82 |

50-unit bins (frame-weighted work ms, n seconds):

| U bin | def | def20 |
|---|---:|---:|
| 50-99 | - | 3.41 (15) |
| 100-149 | 3.35 (27) | 3.40 (24) |
| 150-199 | 4.54 (39) | 4.24 (24) |
| 200-249 | 6.02 (24) | 5.26 (24) |
| 250-299 | 7.46 (28) | 7.15 (28) |
| 300-349 | 9.21 (25) | 8.42 (30) |
| 350-399 | 11.70 (25) | 11.49 (35) |
| 400-449 | 16.96 (28) | 14.71 (23) |
| 450-499 | 27.90 (32) | 18.32 (27) |
| 500-549 | 39.59 (26) | 28.38 (26) |
| 550-599 | 52.58 (28) | 38.33 (26) |
| 600-649 | 60.55 (20) | 52.05 (29) |
| 650-699 | 67.70 (27) | 63.45 (22) |
| 700-749 | 76.90 (23) | 71.26 (30) |
| 750-799 | 82.52 (24) | 78.64 (26) |
| 800-849 | 88.77 (26) | 83.79 (23) |
| 850-899 | 100.02 (25) | 92.45 (26) |
| 900-949 | 108.12 (32) | 100.31 (29) |
| 950-999 | 114.89 (9) | 103.21 (12) |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| quick-trickle | Alien | creature | 171 | 0.88 | 0.116 | 54830 | 37279 | 3202 | 4965 |
| quick-trickle | Alien | nonlab | 0 | - | - | 1540 | 0 | 0 | 0 |
| quick-trickle | Centauri | heavy | 53 | 0.75 | 0.947 | 0 | 25237 | 932 | 789 |
| quick-trickle | Centauri | infantry | 64 | 0.74 | 0.403 | 0 | 13209 | 189 | 1400 |
| quick-trickle | Centauri | light | 57 | 0.74 | 0.732 | 0 | 29131 | 597 | 1228 |
| quick-trickle | Centauri | nonlab | 0 | - | - | 0 | 169 | 2 | 0 |
| quick-trickle | Centauri | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle | Centauri | turret | 0 | - | - | 0 | 15 | 1 | 0 |
| quick-trickle | Sol | heavy | 54 | 0.92 | 1.108 | 0 | 35402 | 4045 | 451 |
| quick-trickle | Sol | infantry | 65 | 0.77 | 0.280 | 0 | 8760 | 122 | 825 |
| quick-trickle | Sol | light | 58 | 0.88 | 0.684 | 0 | 19066 | 418 | 717 |
| quick-trickle | Sol | nonlab | 0 | - | - | 0 | 167 | 3 | 0 |
| quick-trickle | Sol | structure | 0 | - | - | 0 | 472 | 3 | 3 |
| quick-trickle | Sol | turret | 0 | - | - | 0 | 1 | 0 | 0 |

