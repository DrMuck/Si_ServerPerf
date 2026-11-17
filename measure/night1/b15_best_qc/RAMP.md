## Continuous climb: quick-trickle (status ok, notes: climb stopped: fps_floor (median 9 < 10) at 715)

Fit per arm, U = lab units:  work_ms = a + b*U + c*U^2 (frame-weighted); frame_ms = 1000/fps fitted the same way.

| arm | s | U range | a | b (ms/unit) | c (ms/unit^2) | rmse | work@300 | work@500 | work@800 | work@1000 | frame_ms@800 |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| def | 218 | 101-691 | 22.746 | -0.19126 | 4.65e-04 | 6.17 | 7.23 | 43.40 | 167.43 | 296.64 | 170.56 |
| new | 217 | 96-681 | 22.170 | -0.17328 | 3.98e-04 | 5.48 | 5.97 | 34.93 | 138.01 | 246.49 | 145.67 |
| off | 234 | 89-696 | 20.501 | -0.19680 | 5.42e-04 | 7.29 | 10.20 | 57.49 | 209.66 | 365.27 | 200.45 |

50-unit bins (frame-weighted work ms, n seconds):

| U bin | def | new | off |
|---|---:|---:|---:|
| 50-99 | - | 3.37 (3) | 3.40 (11) |
| 100-149 | 3.67 (24) | 3.83 (16) | 3.62 (13) |
| 150-199 | 5.00 (22) | 4.20 (10) | 4.96 (23) |
| 200-249 | 6.78 (10) | 5.43 (22) | 7.05 (24) |
| 250-299 | 7.50 (17) | 7.26 (24) | 10.38 (18) |
| 300-349 | 9.59 (23) | 9.73 (24) | 12.61 (12) |
| 350-399 | 13.97 (23) | 13.22 (14) | 14.93 (18) |
| 400-449 | 21.29 (19) | 15.28 (13) | 25.57 (23) |
| 450-499 | 34.64 (15) | 20.17 (21) | 54.63 (23) |
| 500-549 | 42.96 (14) | 41.11 (24) | 91.84 (9) |
| 550-599 | 72.84 (22) | 61.55 (18) | 87.72 (16) |
| 600-649 | 94.36 (18) | 70.11 (7) | 108.46 (22) |
| 650-699 | 101.39 (11) | 90.25 (21) | 135.86 (22) |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| quick-trickle | Alien | creature | 123 | 0.88 | 0.213 | 35271 | 24310 | 1713 | 5097 |
| quick-trickle | Alien | nonlab | 0 | - | - | 320 | 0 | 0 | 0 |
| quick-trickle | Centauri | heavy | 40 | 0.96 | 1.793 | 0 | 22838 | 453 | 574 |
| quick-trickle | Centauri | infantry | 48 | 0.88 | 0.545 | 0 | 9017 | 100 | 916 |
| quick-trickle | Centauri | light | 43 | 0.95 | 1.005 | 0 | 19401 | 342 | 704 |
| quick-trickle | Centauri | nonlab | 0 | - | - | 0 | 2931 | 1 | 2 |
| quick-trickle | Centauri | structure | 0 | - | - | 0 | 383 | 3 | 3 |
| quick-trickle | Centauri | turret | 0 | - | - | 0 | 22 | 1 | 0 |
| quick-trickle | Other | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| quick-trickle | Sol | heavy | 41 | 0.94 | 1.821 | 0 | 41235 | 5439 | 393 |
| quick-trickle | Sol | infantry | 49 | 0.81 | 0.588 | 0 | 7330 | 125 | 735 |
| quick-trickle | Sol | light | 43 | 0.92 | 1.402 | 0 | 21737 | 341 | 631 |
| quick-trickle | Sol | nonlab | 0 | - | - | 0 | 178 | 2 | 0 |
| quick-trickle | Sol | structure | 0 | - | - | 0 | 349 | 2 | 3 |
| quick-trickle | Sol | turret | 0 | - | - | 0 | 149 | 5 | 0 |

