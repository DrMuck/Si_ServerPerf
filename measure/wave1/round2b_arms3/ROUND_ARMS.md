Round `round2b_arms3` split into arms by flips.csv (5 flips; intervals fully inside a block and starting >= 3 s after its flip). Arms: A = 1 intervals, B = 2 intervals, C = 2 intervals

### Per 50-unit bin: normalised load ms/s (median; per-frame cost x 120 + per-step x 25), fps (median), frame p95 (median); delta vs A

| units | n A | n B | n C | load A | load B | load C | B vs A % | C vs A % | fps A | fps B | fps C | p95 A | p95 B | p95 C |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 1 | 2 | 2 | 141 | 138 | 138 | -2.1 | -2.3 | 113.2 | 119.6 | 120.0 | 8.7 | 9.5 | 9.6 |

### Fit of the normalised load (per-frame cost x 120 + per-step cost x 25, ms/s; REPORT.md 2.1) = a + b*U + c*U^2 per arm (all clean intervals, storms excluded)

| arm | n | a | b | c | R2 | at 300 u | at 500 u | at 700 u | vs A at 300 / 500 / 700 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
(max units seen: ; values beyond that are extrapolated)

