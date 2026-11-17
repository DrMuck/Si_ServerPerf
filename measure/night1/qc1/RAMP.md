## Continuous climb: quick-trickle (status ok, notes: climb stopped: fps_floor (median 9.5 < 10) at 625)

Fit per arm, U = lab units:  work_ms = a + b*U + c*U^2 (frame-weighted); frame_ms = 1000/fps fitted the same way.

| arm | s | U range | a | b (ms/unit) | c (ms/unit^2) | rmse | work@300 | work@500 | work@800 | work@1000 | frame_ms@800 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| agg | 190 | 119-599 | 27.510 | -0.23197 | 5.30e-04 | 6.30 | 5.65 | 44.12 | 181.39 | 325.94 | 201.91 |
| def | 195 | 103-606 | 20.535 | -0.18524 | 4.77e-04 | 6.21 | 7.91 | 47.21 | 177.73 | 312.47 | 200.94 |
| off | 203 | 87-615 | 26.450 | -0.25469 | 6.72e-04 | 7.63 | 10.56 | 67.20 | 253.01 | 444.13 | 265.68 |

50-unit bins (frame-weighted work ms, n seconds):

| U bin | agg | def | off |
|---|---:|---:|---:|
| 50-99 | - | - | 3.79 (15) |
| 100-149 | 3.27 (24) | 3.29 (24) | 4.61 (4) |
| 150-199 | 4.66 (19) | 4.72 (24) | 4.87 (20) |
| 200-249 | 5.22 (6) | 6.84 (18) | 6.93 (24) |
| 250-299 | 6.69 (23) | 8.69 (15) | 9.82 (22) |
| 300-349 | 9.01 (24) | 9.36 (14) | 13.80 (14) |
| 350-399 | 12.67 (23) | 13.34 (24) | 16.71 (14) |
| 400-449 | 22.56 (15) | 23.03 (24) | 25.00 (21) |
| 450-499 | 29.05 (12) | 43.93 (16) | 57.93 (23) |
| 500-549 | 51.61 (21) | 57.76 (11) | 98.07 (18) |
| 550-599 | 86.20 (23) | 81.45 (22) | 112.72 (18) |
| 600-649 | - | 90.00 (3) | 120.21 (10) |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| quick-trickle | Alien | creature | 116 | 0.53 | 0.104 | 24618 | 17275 | 1564 | 2272 |
| quick-trickle | Alien | nonlab | 0 | - | - | 849 | 0 | 0 | 0 |
| quick-trickle | Centauri | heavy | 35 | 0.95 | 2.880 | 0 | 22519 | 472 | 784 |
| quick-trickle | Centauri | infantry | 41 | 0.86 | 1.043 | 0 | 11255 | 217 | 1398 |
| quick-trickle | Centauri | light | 38 | 0.95 | 1.621 | 0 | 20686 | 329 | 846 |
| quick-trickle | Centauri | nonlab | 0 | - | - | 0 | 35 | 0 | 0 |
| quick-trickle | Centauri | structure | 0 | - | - | 0 | 0 | 0 | 1 |
| quick-trickle | Centauri | turret | 0 | - | - | 0 | 56 | 4 | 0 |
| quick-trickle | Sol | heavy | 37 | 0.86 | 1.717 | 0 | 26986 | 2945 | 227 |
| quick-trickle | Sol | infantry | 44 | 0.46 | 0.817 | 0 | 11686 | 322 | 581 |
| quick-trickle | Sol | light | 39 | 0.66 | 0.882 | 0 | 13357 | 351 | 522 |
| quick-trickle | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle | Sol | structure | 0 | - | - | 0 | 345 | 2 | 2 |

