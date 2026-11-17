## Plateaus (ramp steps)

| plateau | status | target units | lab units avg (min-max) | count dev max % | structures | fps | work avg / p95 ms | deaths/s | respawns/s | shots | hits | kills | with target avg | stalls |
|---|---|---:|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| ramp-sustain@200 | ok | 200 | 186 (169-197) | 15.5 | 24 | 114.4 | 4.68 / 10.61 | 6.47 | 6.48 | 64755 | 36703 | 1552 | 111 | 0 |
| ramp-sustain@300 | ok | 300 | 280 (265-293) | 11.7 | 36 | 101.4 | 7.14 / 15.38 | 9.24 | 9.25 | 81565 | 48963 | 2298 | 163 | 0 |
| ramp-sustain@400 | ok | 400 | 374 (346-389) | 13.5 | 47 | 79.3 | 11.24 / 23.05 | 12.02 | 12.00 | 97764 | 62706 | 2935 | 214 | 0 |
| ramp-sustain@500 | ok | 500 | 467 (446-488) | 10.8 | 60 | 43.3 | 22.69 / 43.64 | 15.18 | 15.12 | 108551 | 73306 | 3684 | 270 | 0 |
| ramp-sustain@600 | ok | 600 | 563 (536-584) | 10.7 | 72 | 17.6 | 56.74 / 93.53 | 16.73 | 16.72 | 109684 | 79899 | 4028 | 320 | 0 |
| ramp-sustain@700 | ok | 700 | 665 (637-687) | 9.0 | 83 | 10.4 | 95.87 / 137.74 | 15.32 | 15.34 | 112081 | 75785 | 3716 | 376 | 0 |
| ramp-sustain@800 | ok | 800 | 767 (742-785) | 7.2 | 94 | 8.3 | 120.51 / 170.58 | 14.53 | 14.41 | 102356 | 72099 | 3488 | 444 | 0 |

Per plateau and arm (1 Hz Measure samples; paired = each X block minus the mean of its neighbouring A blocks):

| plateau | arm | s | lab units | fps median | work ms | X-A paired ms | s.e. | n pairs |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| ramp-sustain@200 | agg | 72 | 186 | 117.0 | 4.19 | - | - | 0 |
| ramp-sustain@200 | def | 72 | 187 | 117.0 | 4.51 | - | - | 0 |
| ramp-sustain@200 | off | 72 | 186 | 113.0 | 5.30 | - | - | 0 |
| ramp-sustain@300 | agg | 72 | 280 | 105.0 | 6.54 | - | - | 0 |
| ramp-sustain@300 | def | 72 | 282 | 104.0 | 6.81 | - | - | 0 |
| ramp-sustain@300 | off | 71 | 282 | 98.0 | 8.16 | - | - | 0 |
| ramp-sustain@400 | agg | 71 | 375 | 87.0 | 9.87 | - | - | 0 |
| ramp-sustain@400 | def | 72 | 374 | 85.0 | 10.29 | - | - | 0 |
| ramp-sustain@400 | off | 72 | 373 | 70.0 | 13.97 | - | - | 0 |
| ramp-sustain@500 | agg | 71 | 466 | 55.0 | 18.42 | - | - | 0 |
| ramp-sustain@500 | def | 71 | 468 | 50.0 | 20.05 | - | - | 0 |
| ramp-sustain@500 | off | 70 | 468 | 31.0 | 33.87 | - | - | 0 |
| ramp-sustain@600 | agg | 69 | 563 | 21.0 | 49.39 | - | - | 0 |
| ramp-sustain@600 | def | 70 | 564 | 20.0 | 50.84 | - | - | 0 |
| ramp-sustain@600 | off | 69 | 565 | 13.0 | 79.48 | - | - | 0 |
| ramp-sustain@700 | agg | 70 | 667 | 12.0 | 85.94 | - | - | 0 |
| ramp-sustain@700 | def | 69 | 665 | 12.0 | 90.49 | - | - | 0 |
| ramp-sustain@700 | off | 69 | 667 | 9.0 | 115.83 | - | - | 0 |
| ramp-sustain@800 | agg | 67 | 765 | 10.0 | 109.92 | - | - | 0 |
| ramp-sustain@800 | def | 68 | 769 | 10.0 | 110.66 | - | - | 0 |
| ramp-sustain@800 | off | 67 | 770 | 7.0 | 146.92 | - | - | 0 |

## Engagement per window (team, kind)

| window | team | kind | units avg | with target share | shots / unit-s | creature attacks | hits dealt | kills | deaths |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| ramp-sustain@200 | Alien | creature | 60 | 0.54 | 0.186 | 6716 | 5700 | 443 | 783 |
| ramp-sustain@200 | Alien | nonlab | 0 | - | - | 457 | 0 | 0 | 2 |
| ramp-sustain@200 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@200 | Alien | turret | 0 | - | - | 0 | 145 | 1 | 0 |
| ramp-sustain@200 | Centauri | heavy | 20 | 0.63 | 2.284 | 0 | 6916 | 194 | 171 |
| ramp-sustain@200 | Centauri | infantry | 18 | 0.51 | 1.036 | 0 | 2267 | 49 | 157 |
| ramp-sustain@200 | Centauri | light | 25 | 0.62 | 1.482 | 0 | 6356 | 162 | 221 |
| ramp-sustain@200 | Centauri | nonlab | 0 | - | - | 0 | 3 | 0 | 0 |
| ramp-sustain@200 | Centauri | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@200 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@200 | Sol | heavy | 20 | 0.78 | 2.079 | 0 | 5004 | 493 | 99 |
| ramp-sustain@200 | Sol | infantry | 18 | 0.50 | 1.383 | 0 | 2816 | 41 | 140 |
| ramp-sustain@200 | Sol | light | 26 | 0.65 | 1.998 | 0 | 7299 | 169 | 176 |
| ramp-sustain@200 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@200 | Sol | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@300 | Alien | creature | 91 | 0.56 | 0.193 | 9884 | 7905 | 657 | 1115 |
| ramp-sustain@300 | Alien | nonlab | 0 | - | - | 478 | 0 | 0 | 1 |
| ramp-sustain@300 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@300 | Alien | turret | 0 | - | - | 0 | 118 | 1 | 2 |
| ramp-sustain@300 | Centauri | heavy | 27 | 0.58 | 1.991 | 0 | 7440 | 277 | 198 |
| ramp-sustain@300 | Centauri | infantry | 35 | 0.55 | 1.122 | 0 | 5539 | 106 | 340 |
| ramp-sustain@300 | Centauri | light | 31 | 0.59 | 1.495 | 0 | 7849 | 178 | 253 |
| ramp-sustain@300 | Centauri | nonlab | 0 | - | - | 0 | 20 | 1 | 0 |
| ramp-sustain@300 | Centauri | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@300 | Sol | heavy | 28 | 0.76 | 1.593 | 0 | 7008 | 832 | 121 |
| ramp-sustain@300 | Sol | infantry | 36 | 0.49 | 0.974 | 0 | 4431 | 92 | 274 |
| ramp-sustain@300 | Sol | light | 31 | 0.63 | 1.824 | 0 | 8330 | 153 | 191 |
| ramp-sustain@300 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@300 | Sol | structure | 0 | - | - | 0 | 125 | 1 | 1 |
| ramp-sustain@400 | Alien | creature | 121 | 0.54 | 0.159 | 12040 | 9918 | 792 | 1567 |
| ramp-sustain@400 | Alien | nonlab | 0 | - | - | 160 | 0 | 0 | 1 |
| ramp-sustain@400 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 1 |
| ramp-sustain@400 | Alien | turret | 0 | - | - | 0 | 94 | 0 | 3 |
| ramp-sustain@400 | Centauri | heavy | 38 | 0.61 | 1.863 | 0 | 10533 | 360 | 288 |
| ramp-sustain@400 | Centauri | infantry | 48 | 0.52 | 0.874 | 0 | 6539 | 101 | 388 |
| ramp-sustain@400 | Centauri | light | 39 | 0.59 | 1.219 | 0 | 8988 | 192 | 319 |
| ramp-sustain@400 | Centauri | nonlab | 0 | - | - | 0 | 2 | 0 | 0 |
| ramp-sustain@400 | Centauri | structure | 0 | - | - | 0 | 125 | 1 | 1 |
| ramp-sustain@400 | Sol | heavy | 40 | 0.75 | 1.725 | 0 | 12177 | 1197 | 150 |
| ramp-sustain@400 | Sol | infantry | 49 | 0.47 | 0.694 | 0 | 4530 | 98 | 307 |
| ramp-sustain@400 | Sol | light | 40 | 0.61 | 1.555 | 0 | 9354 | 192 | 224 |
| ramp-sustain@400 | Sol | nonlab | 0 | - | - | 0 | 1 | 0 | 0 |
| ramp-sustain@400 | Sol | structure | 0 | - | - | 0 | 125 | 1 | 1 |
| ramp-sustain@400 | Sol | turret | 0 | - | - | 0 | 5 | 1 | 0 |
| ramp-sustain@500 | Alien | creature | 151 | 0.54 | 0.125 | 13236 | 11461 | 1058 | 1886 |
| ramp-sustain@500 | Alien | nonlab | 0 | - | - | 212 | 0 | 0 | 2 |
| ramp-sustain@500 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@500 | Alien | turret | 0 | - | - | 0 | 411 | 1 | 0 |
| ramp-sustain@500 | Centauri | heavy | 46 | 0.59 | 1.711 | 0 | 12559 | 385 | 347 |
| ramp-sustain@500 | Centauri | infantry | 56 | 0.52 | 0.744 | 0 | 6027 | 112 | 517 |
| ramp-sustain@500 | Centauri | light | 54 | 0.61 | 1.102 | 0 | 11827 | 255 | 478 |
| ramp-sustain@500 | Centauri | structure | 0 | - | - | 0 | 114 | 1 | 1 |
| ramp-sustain@500 | Sol | heavy | 48 | 0.75 | 1.547 | 0 | 13957 | 1607 | 171 |
| ramp-sustain@500 | Sol | infantry | 57 | 0.48 | 0.669 | 0 | 4869 | 70 | 365 |
| ramp-sustain@500 | Sol | light | 55 | 0.65 | 1.452 | 0 | 11547 | 194 | 326 |
| ramp-sustain@500 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@500 | Sol | structure | 0 | - | - | 0 | 124 | 1 | 1 |
| ramp-sustain@500 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@600 | Alien | creature | 182 | 0.53 | 0.106 | 13578 | 12546 | 1017 | 2241 |
| ramp-sustain@600 | Alien | nonlab | 0 | - | - | 80 | 0 | 0 | 1 |
| ramp-sustain@600 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@600 | Alien | turret | 0 | - | - | 0 | 457 | 1 | 5 |
| ramp-sustain@600 | Centauri | heavy | 59 | 0.63 | 1.493 | 0 | 14737 | 429 | 427 |
| ramp-sustain@600 | Centauri | infantry | 68 | 0.49 | 0.557 | 0 | 6359 | 131 | 504 |
| ramp-sustain@600 | Centauri | light | 61 | 0.60 | 0.857 | 0 | 11203 | 237 | 452 |
| ramp-sustain@600 | Centauri | nonlab | 0 | - | - | 0 | 145 | 2 | 0 |
| ramp-sustain@600 | Centauri | structure | 0 | - | - | 0 | 123 | 1 | 1 |
| ramp-sustain@600 | Centauri | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@600 | Sol | heavy | 61 | 0.75 | 1.356 | 0 | 16925 | 1940 | 202 |
| ramp-sustain@600 | Sol | infantry | 69 | 0.43 | 0.498 | 0 | 4582 | 80 | 359 |
| ramp-sustain@600 | Sol | light | 62 | 0.64 | 1.260 | 0 | 12204 | 189 | 330 |
| ramp-sustain@600 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@600 | Sol | structure | 0 | - | - | 0 | 123 | 1 | 1 |
| ramp-sustain@700 | Alien | creature | 218 | 0.53 | 0.088 | 13660 | 12661 | 1199 | 1820 |
| ramp-sustain@700 | Alien | nonlab | 0 | - | - | 128 | 0 | 0 | 0 |
| ramp-sustain@700 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 1 |
| ramp-sustain@700 | Alien | turret | 0 | - | - | 0 | 673 | 7 | 4 |
| ramp-sustain@700 | Centauri | heavy | 64 | 0.60 | 1.368 | 0 | 13216 | 287 | 411 |
| ramp-sustain@700 | Centauri | infantry | 83 | 0.53 | 0.472 | 0 | 6011 | 77 | 544 |
| ramp-sustain@700 | Centauri | light | 74 | 0.60 | 0.755 | 0 | 10746 | 191 | 510 |
| ramp-sustain@700 | Centauri | nonlab | 0 | - | - | 0 | 29 | 0 | 0 |
| ramp-sustain@700 | Centauri | structure | 0 | - | - | 0 | 105 | 1 | 1 |
| ramp-sustain@700 | Other | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@700 | Sol | heavy | 66 | 0.74 | 1.171 | 0 | 15947 | 1710 | 187 |
| ramp-sustain@700 | Sol | infantry | 86 | 0.43 | 0.431 | 0 | 4251 | 72 | 294 |
| ramp-sustain@700 | Sol | light | 75 | 0.65 | 1.097 | 0 | 11618 | 171 | 364 |
| ramp-sustain@700 | Sol | nonlab | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@700 | Sol | structure | 0 | - | - | 0 | 107 | 1 | 1 |
| ramp-sustain@700 | Sol | turret | 0 | - | - | 0 | 0 | 0 | 0 |
| ramp-sustain@800 | Alien | creature | 251 | 0.55 | 0.071 | 12992 | 12014 | 1127 | 1821 |
| ramp-sustain@800 | Alien | nonlab | 0 | - | - | 80 | 0 | 0 | 1 |
| ramp-sustain@800 | Alien | structure | 0 | - | - | 0 | 0 | 0 | 2 |
| ramp-sustain@800 | Alien | turret | 0 | - | - | 0 | 620 | 4 | 5 |
| ramp-sustain@800 | Centauri | heavy | 76 | 0.62 | 0.990 | 0 | 11832 | 322 | 370 |
| ramp-sustain@800 | Centauri | infantry | 97 | 0.53 | 0.309 | 0 | 4977 | 73 | 467 |
| ramp-sustain@800 | Centauri | light | 83 | 0.60 | 0.714 | 0 | 11213 | 141 | 450 |
| ramp-sustain@800 | Centauri | nonlab | 0 | - | - | 0 | 196 | 0 | 0 |
| ramp-sustain@800 | Centauri | structure | 0 | - | - | 0 | 88 | 1 | 2 |
| ramp-sustain@800 | Centauri | turret | 0 | - | - | 0 | 25 | 4 | 0 |
| ramp-sustain@800 | Sol | heavy | 78 | 0.76 | 0.972 | 0 | 15286 | 1593 | 164 |
| ramp-sustain@800 | Sol | infantry | 98 | 0.45 | 0.331 | 0 | 3636 | 66 | 315 |
| ramp-sustain@800 | Sol | light | 84 | 0.66 | 0.924 | 0 | 11683 | 156 | 324 |
| ramp-sustain@800 | Sol | nonlab | 0 | - | - | 0 | 1 | 0 | 0 |
| ramp-sustain@800 | Sol | structure | 0 | - | - | 0 | 86 | 1 | 2 |
| ramp-sustain@800 | Sol | turret | 0 | - | - | 0 | 7 | 0 | 0 |

