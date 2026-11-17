# Steps x arms: `b13_snap` (Si_PerfLab 1 Hz Measure samples, 5 s after each flip dropped)

| step | A fps | A work ms | A main ms/s | A n s | S1 fps | S1 work ms | S1 main ms/s | S1 n s | S2 fps | S2 work ms | S2 main ms/s | S2 n s | S3 fps | S3 work ms | S3 main ms/s | S3 n s | L fps | L work ms | L main ms/s | L n s | S1 vs A work | S2 vs A work | S3 vs A work | L vs A work |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| melee-snap@450 | 60.2 | 16.35 | 984 | 48 | 62.5 | 15.60 | 974 | 47 | 64.1 | 15.19 | 974 | 47 | 63.9 | 15.35 | 981 | 63 | 63.7 | 15.23 | 970 | 57 | -5 % | -7 % | -6 % | -7 % |
| melee-snap@550 | 22.1 | 46.79 | 1036 | 60 | 20.6 | 49.63 | 1024 | 56 | 25.7 | 39.72 | 1022 | 46 | 29.6 | 34.66 | 1027 | 47 | 24.2 | 42.32 | 1024 | 46 | +6 % | -15 % | -26 % | -10 % |

Paired (each block minus its neighbouring `A` blocks in the same step; mean, s.e., n blocks):

| step | S1 - A ms | % | s.e. | n | S2 - A ms | % | s.e. | n | S3 - A ms | % | s.e. | n | L - A ms | % | s.e. | n |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| melee-snap@450 | -1.34 | -8.2 | 0.57 | 2 | -1.74 | -10.6 | 0.24 | 2 | -1.13 | -6.9 | 0.49 | 3 | -1.01 | -6.2 | 0.26 | 3 |
| melee-snap@550 | +1.24 | +2.7 | 2.81 | 3 | -6.00 | -12.8 | 0.04 | 2 | -10.75 | -23.0 | 1.33 | 2 | -3.20 | -6.8 | 1.43 | 2 |
