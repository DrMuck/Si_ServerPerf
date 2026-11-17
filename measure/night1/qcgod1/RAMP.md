## Continuous climb: quick-trickle-god (status ok, notes: climb stopped: fps_floor (median 9 < 10) at 895)

Fit per arm, U = lab units:  work_ms = a + b*U + c*U^2 (frame-weighted); frame_ms = 1000/fps fitted the same way.

| arm | s | U range | a | b (ms/unit) | c (ms/unit^2) | rmse | work@300 | work@500 | work@800 | work@1000 | frame_ms@800 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| agg | 284 | 126-895 | 17.603 | -0.11034 | 2.05e-04 | 5.37 | 2.93 | 13.62 | 60.38 | 112.02 | 62.55 |
| def | 290 | 113-866 | 14.055 | -0.09101 | 1.90e-04 | 4.98 | 3.86 | 16.08 | 62.92 | 113.15 | 64.99 |
| off | 296 | 98-883 | 17.070 | -0.12204 | 2.68e-04 | 5.39 | 4.55 | 22.97 | 90.75 | 162.71 | 90.30 |

50-unit bins (frame-weighted work ms, n seconds):

| U bin | agg | def | off |
|---|---:|---:|---:|
| 50-99 | - | - | 3.53 (15) |
| 100-149 | 3.09 (24) | 3.07 (24) | - |
| 150-199 | 4.05 (8) | 4.11 (24) | 4.13 (24) |
| 200-249 | 4.15 (16) | 5.58 (19) | 5.72 (24) |
| 250-299 | 5.44 (24) | 5.81 (5) | 7.50 (18) |
| 300-349 | 6.76 (18) | 7.06 (24) | 8.72 (14) |
| 350-399 | 7.82 (15) | 9.25 (21) | 10.37 (16) |
| 400-449 | 8.87 (15) | 11.04 (15) | 13.11 (24) |
| 450-499 | 10.82 (24) | 12.11 (12) | 16.50 (12) |
| 500-549 | 14.09 (22) | 14.42 (24) | 17.95 (11) |
| 550-599 | 17.38 (9) | 19.76 (23) | 27.36 (24) |
| 600-649 | 19.92 (19) | 27.90 (9) | 40.59 (23) |
| 650-699 | 31.63 (21) | 34.59 (23) | 59.63 (18) |
| 700-749 | 48.60 (18) | 47.47 (16) | 75.56 (13) |
| 750-799 | 63.04 (16) | 63.00 (18) | 93.33 (14) |
| 800-849 | 74.75 (12) | 78.95 (17) | 107.64 (21) |
| 850-899 | 85.96 (23) | 85.62 (16) | 115.94 (25) |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| quick-trickle-god | Alien | creature | 161 | 0.95 | 0.158 | 192903 | 0 | 0 | 806 |
| quick-trickle-god | Alien | nonlab | 0 | - | - | 393 | 0 | 0 | 0 |
| quick-trickle-god | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle-god | Centauri | heavy | 51 | 1.00 | 2.328 | 0 | 0 | 0 | 0 |
| quick-trickle-god | Centauri | infantry | 62 | 0.97 | 1.303 | 0 | 0 | 0 | 0 |
| quick-trickle-god | Centauri | light | 55 | 1.00 | 1.530 | 0 | 0 | 0 | 0 |
| quick-trickle-god | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle-god | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle-god | Sol | heavy | 51 | 1.00 | 2.009 | 0 | 0 | 0 | 0 |
| quick-trickle-god | Sol | infantry | 62 | 0.95 | 1.489 | 0 | 0 | 0 | 0 |
| quick-trickle-god | Sol | light | 55 | 1.00 | 1.991 | 0 | 0 | 0 | 0 |
| quick-trickle-god | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle-god | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |

