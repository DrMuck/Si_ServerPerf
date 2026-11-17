# Steps x arms: `b13_dead` (Si_PerfLab 1 Hz Measure samples, 20 s after each flip dropped)

| step | A fps | A work ms | A main ms/s | A n s | B fps | B work ms | B main ms/s | B n s | C fps | C work ms | C main ms/s | C n s | B vs A work | C vs A work |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| melee-dead@450 | 54.8 | 18.28 | 1001 | 78 | 53.7 | 18.61 | 999 | 93 | 66.6 | 14.76 | 983 | 100 | +2 % | -19 % |
| melee-dead@550 | 18.2 | 56.85 | 1032 | 113 | 16.2 | 63.37 | 1030 | 76 | 27.4 | 37.32 | 1023 | 76 | +11 % | -34 % |

Paired (each block minus its neighbouring `A` blocks in the same step; mean, s.e., n blocks):

| step | B - A ms | % | s.e. | n | C - A ms | % | s.e. | n |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| melee-dead@450 | +0.15 | +0.8 | 0.10 | 3 | -3.53 | -19.3 | 0.27 | 3 |
| melee-dead@550 | +4.88 | +8.6 | 0.24 | 2 | -21.16 | -37.2 | 0.40 | 2 |
