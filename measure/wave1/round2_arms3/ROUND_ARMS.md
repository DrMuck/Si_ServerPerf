Round `round2_arms3` split into arms by flips.csv (65 flips; intervals fully inside a block and starting >= 3 s after its flip). Arms: A = 34 intervals, B = 36 intervals, C = 33 intervals

### Per 50-unit bin: normalised load ms/s (median; per-frame cost x 120 + per-step x 25), fps (median), frame p95 (median); delta vs A

| units | n A | n B | n C | load A | load B | load C | B vs A % | C vs A % | fps A | fps B | fps C | p95 A | p95 B | p95 C |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 3 | 4 | 4 | 184 | 156 | 157 | -15.0 | -14.4 | 119.3 | 119.9 | 119.5 | 9.7 | 9.7 | 9.9 |
| 50 | 4 | 4 | 4 | 278 | 248 | 236 | -10.8 | -15.0 | 119.5 | 120.0 | 119.9 | 10.8 | 10.9 | 11.1 |
| 100 | 15 | 14 | 13 | 362 | 325 | 298 | -10.3 | -17.6 | 119.8 | 119.9 | 119.8 | 12.2 | 12.4 | 12.6 |
| 150 | 12 | 14 | 12 | 408 | 359 | 337 | -12.1 | -17.4 | 119.7 | 119.7 | 119.8 | 14.4 | 14.2 | 14.5 |

### Fit of the normalised load (per-frame cost x 120 + per-step cost x 25, ms/s; REPORT.md 2.1) = a + b*U + c*U^2 per arm (all clean intervals, storms excluded)

| arm | n | a | b | c | R2 | at 300 u | at 500 u | at 700 u | vs A at 300 / 500 / 700 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| A | 34 | 80.7 | 3.232 | -0.00767 | 0.935 | 360 | -221 | -1416 |  |
| B | 36 | 70.5 | 2.471 | -0.00414 | 0.952 | 439 | 271 | -229 | 22.0 % / -222.3 % / -83.8 % |
| C | 33 | 100.1 | 1.517 | -0.00060 | 0.930 | 501 | 709 | 868 | 39.3 % / -420.2 % / -161.3 % |
(max units seen: A 176, B 179, C 179; values beyond that are extrapolated)

