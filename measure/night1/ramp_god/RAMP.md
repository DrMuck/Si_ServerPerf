## Plateaus (ramp steps)

| plateau | status | target units | lab units avg (min-max) | count dev max % | structures | fps | work avg / p95 ms | deaths/s | respawns/s | shots | hits | kills | with target avg | stalls |
|---|---|---:|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| ramp-god@200 | ok | 200 | 193 (193-194) | 3.5 | 24 | 117.4 | 4.33 / 9.58 | 0.00 | 0.00 | 124391 | 1 | 0 | 155 | 0 |
| ramp-god@300 | ok | 300 | 292 (292-294) | 2.7 | 36 | 107.8 | 6.13 / 13.15 | 0.01 | 0.00 | 166768 | 2 | 0 | 239 | 0 |
| ramp-god@400 | ok | 400 | 389 (389-390) | 2.8 | 48 | 93.6 | 8.72 / 18.17 | 0.00 | 0.00 | 213427 | 1 | 0 | 331 | 0 |
| ramp-god@500 | ok | 500 | 485 (485-486) | 3.0 | 60 | 73.9 | 12.53 / 24.67 | 0.00 | 0.00 | 261572 | 1 | 0 | 409 | 0 |
| ramp-god@600 | ok | 600 | 582 (582-584) | 3.0 | 72 | 51.1 | 19.21 / 34.41 | 0.01 | 0.00 | 299271 | 2 | 0 | 501 | 0 |
| ramp-god@700 | ok | 700 | 681 (681-682) | 2.7 | 84 | 28.3 | 35.34 / 66.39 | 0.00 | 0.00 | 326762 | 1 | 0 | 584 | 0 |
| ramp-god@800 | ok | 800 | 779 (779-781) | 2.6 | 94 | 16.4 | 60.96 / 101.42 | 0.01 | 0.00 | 339476 | 2 | 0 | 679 | 0 |
| ramp-god@1000 | ok | 1000 | 971 (971-975) | 2.9 | 94 | 9.5 | 104.72 / 163.63 | 0.01 | 0.00 | 327213 | 4 | 0 | 843 | 0 |

Per plateau and arm (1 Hz Measure samples; paired = each X block minus the mean of its neighbouring A blocks):

| plateau | arm | s | lab units | fps median | work ms | X-A paired ms | s.e. | n pairs |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| ramp-god@200 | agg | 72 | 193 | 120.0 | 3.75 | - | - | 0 |
| ramp-god@200 | def | 72 | 193 | 119.0 | 4.23 | - | - | 0 |
| ramp-god@200 | off | 71 | 193 | 116.0 | 4.98 | - | - | 0 |
| ramp-god@300 | agg | 70 | 292 | 112.0 | 5.32 | - | - | 0 |
| ramp-god@300 | def | 73 | 292 | 110.0 | 5.93 | - | - | 0 |
| ramp-god@300 | off | 71 | 292 | 105.0 | 7.14 | - | - | 0 |
| ramp-god@400 | agg | 71 | 389 | 100.0 | 7.44 | - | - | 0 |
| ramp-god@400 | def | 72 | 389 | 96.0 | 8.53 | - | - | 0 |
| ramp-god@400 | off | 73 | 389 | 89.0 | 10.33 | - | - | 0 |
| ramp-god@500 | agg | 72 | 485 | 84.0 | 10.43 | - | - | 0 |
| ramp-god@500 | def | 72 | 486 | 77.0 | 12.16 | - | - | 0 |
| ramp-god@500 | off | 70 | 485 | 65.0 | 15.55 | - | - | 0 |
| ramp-god@600 | agg | 71 | 582 | 65.0 | 15.15 | - | - | 0 |
| ramp-god@600 | def | 69 | 582 | 55.0 | 18.87 | - | - | 0 |
| ramp-god@600 | off | 70 | 582 | 38.5 | 26.86 | - | - | 0 |
| ramp-god@700 | agg | 72 | 681 | 38.0 | 27.22 | - | - | 0 |
| ramp-god@700 | def | 70 | 681 | 30.0 | 34.63 | - | - | 0 |
| ramp-god@700 | off | 69 | 681 | 21.0 | 50.34 | - | - | 0 |
| ramp-god@800 | agg | 68 | 779 | 20.0 | 51.93 | - | - | 0 |
| ramp-god@800 | def | 68 | 779 | 19.0 | 55.68 | - | - | 0 |
| ramp-god@800 | off | 68 | 779 | 13.0 | 82.37 | - | - | 0 |
| ramp-god@1000 | agg | 69 | 971 | 11.0 | 94.81 | - | - | 0 |
| ramp-god@1000 | def | 69 | 971 | 11.0 | 96.69 | - | - | 0 |
| ramp-god@1000 | off | 68 | 971 | 8.0 | 127.82 | - | - | 0 |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| ramp-god@200 | Alien | creature | 59 | 0.73 | 0.109 | 12938 | 0 | 0 | 1 |
| ramp-god@200 | Alien | nonlab | 0 | - | - | 790 | 0 | 0 | 0 |
| ramp-god@200 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@200 | Centauri | heavy | 21 | 0.93 | 4.338 | 0 | 0 | 0 | 0 |
| ramp-god@200 | Centauri | infantry | 19 | 0.90 | 2.103 | 0 | 0 | 0 | 0 |
| ramp-god@200 | Centauri | light | 27 | 0.95 | 2.850 | 0 | 0 | 0 | 0 |
| ramp-god@200 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@200 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@200 | Sol | heavy | 21 | 0.78 | 2.291 | 0 | 0 | 0 | 0 |
| ramp-god@200 | Sol | infantry | 19 | 0.63 | 1.826 | 0 | 0 | 0 | 0 |
| ramp-god@200 | Sol | light | 27 | 0.79 | 3.575 | 0 | 0 | 0 | 0 |
| ramp-god@200 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@300 | Alien | creature | 92 | 0.77 | 0.148 | 23034 | 0 | 0 | 2 |
| ramp-god@300 | Alien | nonlab | 0 | - | - | 585 | 0 | 0 | 0 |
| ramp-god@300 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@300 | Centauri | heavy | 29 | 0.95 | 4.103 | 0 | 0 | 0 | 0 |
| ramp-god@300 | Centauri | infantry | 38 | 0.93 | 2.488 | 0 | 0 | 0 | 0 |
| ramp-god@300 | Centauri | light | 33 | 0.96 | 2.537 | 0 | 0 | 0 | 0 |
| ramp-god@300 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@300 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@300 | Sol | heavy | 29 | 0.75 | 1.787 | 0 | 0 | 0 | 0 |
| ramp-god@300 | Sol | infantry | 38 | 0.70 | 2.030 | 0 | 0 | 0 | 0 |
| ramp-god@300 | Sol | light | 33 | 0.79 | 3.725 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Alien | creature | 122 | 0.77 | 0.146 | 29401 | 0 | 0 | 1 |
| ramp-god@400 | Alien | nonlab | 0 | - | - | 243 | 0 | 0 | 0 |
| ramp-god@400 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@400 | Centauri | heavy | 40 | 0.98 | 4.093 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Centauri | infantry | 51 | 0.92 | 2.134 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Centauri | light | 42 | 0.98 | 2.737 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@400 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@400 | Sol | heavy | 41 | 0.84 | 2.209 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Sol | infantry | 51 | 0.74 | 1.803 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Sol | light | 42 | 0.88 | 3.391 | 0 | 0 | 0 | 0 |
| ramp-god@400 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@400 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@500 | Alien | creature | 151 | 0.78 | 0.122 | 35519 | 0 | 0 | 1 |
| ramp-god@500 | Alien | nonlab | 0 | - | - | 742 | 0 | 0 | 0 |
| ramp-god@500 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@500 | Centauri | heavy | 49 | 0.97 | 3.818 | 0 | 0 | 0 | 0 |
| ramp-god@500 | Centauri | infantry | 60 | 0.93 | 2.082 | 0 | 0 | 0 | 0 |
| ramp-god@500 | Centauri | light | 58 | 0.98 | 2.520 | 0 | 0 | 0 | 0 |
| ramp-god@500 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@500 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@500 | Sol | heavy | 49 | 0.80 | 2.219 | 0 | 0 | 0 | 0 |
| ramp-god@500 | Sol | infantry | 60 | 0.70 | 1.626 | 0 | 0 | 0 | 0 |
| ramp-god@500 | Sol | light | 58 | 0.86 | 3.953 | 0 | 0 | 0 | 0 |
| ramp-god@500 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@500 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@600 | Alien | creature | 182 | 0.77 | 0.125 | 44457 | 0 | 0 | 2 |
| ramp-god@600 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@600 | Centauri | heavy | 63 | 0.98 | 3.583 | 0 | 0 | 0 | 0 |
| ramp-god@600 | Centauri | infantry | 72 | 0.92 | 1.831 | 0 | 0 | 0 | 0 |
| ramp-god@600 | Centauri | light | 65 | 1.00 | 2.389 | 0 | 0 | 0 | 0 |
| ramp-god@600 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@600 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@600 | Sol | heavy | 63 | 0.84 | 2.295 | 0 | 0 | 0 | 0 |
| ramp-god@600 | Sol | infantry | 72 | 0.76 | 1.521 | 0 | 0 | 0 | 0 |
| ramp-god@600 | Sol | light | 65 | 0.92 | 4.055 | 0 | 0 | 0 | 0 |
| ramp-god@600 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@600 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@700 | Alien | creature | 214 | 0.77 | 0.107 | 49922 | 0 | 0 | 1 |
| ramp-god@700 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@700 | Centauri | heavy | 67 | 0.99 | 3.700 | 0 | 0 | 0 | 0 |
| ramp-god@700 | Centauri | infantry | 88 | 0.93 | 1.867 | 0 | 0 | 0 | 0 |
| ramp-god@700 | Centauri | light | 78 | 1.00 | 2.349 | 0 | 0 | 0 | 0 |
| ramp-god@700 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@700 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@700 | Sol | heavy | 68 | 0.86 | 1.963 | 0 | 0 | 0 | 0 |
| ramp-god@700 | Sol | infantry | 88 | 0.72 | 1.702 | 0 | 0 | 0 | 0 |
| ramp-god@700 | Sol | light | 78 | 0.90 | 3.047 | 0 | 0 | 0 | 0 |
| ramp-god@700 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@700 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@800 | Alien | creature | 245 | 0.78 | 0.104 | 52954 | 0 | 0 | 2 |
| ramp-god@800 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@800 | Centauri | heavy | 79 | 1.00 | 3.201 | 0 | 0 | 0 | 0 |
| ramp-god@800 | Centauri | infantry | 101 | 0.93 | 1.544 | 0 | 0 | 0 | 0 |
| ramp-god@800 | Centauri | light | 87 | 1.00 | 2.184 | 0 | 0 | 0 | 0 |
| ramp-god@800 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@800 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@800 | Sol | heavy | 79 | 0.89 | 1.953 | 0 | 0 | 0 | 0 |
| ramp-god@800 | Sol | infantry | 101 | 0.77 | 1.663 | 0 | 0 | 0 | 0 |
| ramp-god@800 | Sol | light | 87 | 0.95 | 2.791 | 0 | 0 | 0 | 0 |
| ramp-god@800 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@800 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Alien | creature | 304 | 0.77 | 0.092 | 50116 | 0 | 0 | 4 |
| ramp-god@1000 | Alien | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Centauri | heavy | 100 | 0.99 | 2.446 | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Centauri | infantry | 122 | 0.90 | 1.156 | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Centauri | light | 111 | 1.00 | 1.568 | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Centauri | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Sol | heavy | 100 | 0.86 | 1.639 | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Sol | infantry | 123 | 0.78 | 1.385 | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Sol | light | 111 | 0.95 | 2.231 | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-god@1000 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |

