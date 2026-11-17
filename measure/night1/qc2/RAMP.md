## Continuous climb: quick-trickle (status ok, notes: climb stopped: fps_floor (median 9.5 < 10) at 610)

Fit per arm, U = lab units:  work_ms = a + b*U + c*U^2 (frame-weighted); frame_ms = 1000/fps fitted the same way.

| arm | s | U range | a | b (ms/unit) | c (ms/unit^2) | rmse | work@300 | work@500 | work@800 | work@1000 | frame_ms@800 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| agg | 188 | 103-577 | 20.525 | -0.18133 | 4.39e-04 | 7.83 | 5.66 | 39.69 | 156.62 | 278.51 | 192.44 |
| def | 188 | 89-595 | 18.847 | -0.16827 | 4.28e-04 | 7.97 | 6.93 | 41.82 | 158.43 | 279.02 | 190.66 |
| off | 190 | 118-598 | 33.320 | -0.29673 | 7.15e-04 | 8.05 | 8.65 | 63.70 | 253.52 | 451.56 | 272.56 |

50-unit bins (frame-weighted work ms, n seconds):

| U bin | agg | def | off |
|---|---:|---:|---:|
| 50-99 | - | 3.27 (15) | - |
| 100-149 | 2.99 (23) | 3.69 (6) | 3.89 (25) |
| 150-199 | 3.94 (24) | 4.05 (20) | 5.59 (17) |
| 200-249 | 5.47 (20) | 5.39 (23) | 6.28 (10) |
| 250-299 | 6.84 (13) | 7.85 (23) | 8.17 (21) |
| 300-349 | 7.59 (15) | 10.99 (17) | 11.34 (24) |
| 350-399 | 10.66 (25) | 11.86 (8) | 17.90 (22) |
| 400-449 | 16.70 (21) | 15.67 (25) | 36.46 (13) |
| 450-499 | 35.75 (20) | 30.68 (21) | 47.22 (12) |
| 500-549 | 49.58 (7) | 66.67 (18) | 75.40 (23) |
| 550-599 | 74.16 (20) | 76.92 (12) | 115.30 (23) |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| quick-trickle | Alien | creature | 111 | 0.79 | 0.144 | 27796 | 18813 | 1547 | 2837 |
| quick-trickle | Alien | nonlab | 0 | - | - | 1431 | 0 | 0 | 0 |
| quick-trickle | Alien | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle | Alien | turret | 0 | - | - | 0 | 74 | 1 | 0 |
| quick-trickle | Centauri | heavy | 35 | 0.70 | 1.661 | 0 | 12475 | 426 | 460 |
| quick-trickle | Centauri | infantry | 40 | 0.56 | 0.484 | 0 | 4770 | 32 | 1370 |
| quick-trickle | Centauri | light | 37 | 0.63 | 0.994 | 0 | 14369 | 319 | 698 |
| quick-trickle | Centauri | nonlab | 0 | - | - | 0 | 59 | 2 | 2 |
| quick-trickle | Centauri | structure | 0 | - | - | 0 | 0 | 0 | 1 |
| quick-trickle | Centauri | turret | 0 | - | - | 0 | 3 | 1 | 0 |
| quick-trickle | Sol | heavy | 36 | 0.84 | 1.524 | 0 | 23020 | 3179 | 199 |
| quick-trickle | Sol | infantry | 43 | 0.62 | 0.551 | 0 | 6241 | 129 | 405 |
| quick-trickle | Sol | light | 38 | 0.78 | 0.873 | 0 | 11368 | 260 | 393 |
| quick-trickle | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle | Sol | structure | 0 | - | - | 0 | 333 | 2 | 2 |

