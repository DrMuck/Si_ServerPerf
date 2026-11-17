## Round length

- **round1_mod**: last [CASH] sample at t = 1516 s (25.3 min); final units/structures Sol 15/8, Cent 22/10, Alien 283/245
- **control_nomod**: last [CASH] sample at t = 1306 s (21.8 min); final units/structures Sol 4/0, Cent 159/19, Alien 49/53

## Units / structures / cash per team at 5-minute marks

| min | round1_mod Sol | round1_mod Cent | round1_mod Alien | control_nomod Sol | control_nomod Cent | control_nomod Alien |
|---:|---|---|---|---|---|---|
| 0 | 0u 0s $0 | 0u 0s $0 | 0u 0s $0 | 0u 0s $0 | 0u 0s $0 | 0u 0s $0 |
| 5 | 17u 7s $15010 | 14u 9s $36298 | 25u 7s $30600 | 15u 7s $17381 | 32u 11s $34550 | 62u 49s $11690 |
| 10 | 41u 11s $5000 | 25u 11s $34342 | 88u 76s $9430 | 43u 10s $2910 | 52u 13s $29124 | 78u 59s $6760 |
| 15 | 37u 11s $1374 | 29u 15s $1581 | 194u 151s $3120 | 14u 8s $4669 | 77u 15s $33665 | 52u 48s $4995 |
| 20 | 30u 10s $3254 | 14u 13s $955 | 232u 238s $17165 | 5u 0s $0 | 138u 19s $15685 | 56u 58s $6035 |
| 25 | 18u 8s $8324 | 23u 11s $652 | 279u 246s $18415 | - | - | - |

## Construct / queue requests ([CMD]) by result

| run | team | Success | other results |
|---|---|---:|---|
| round1_mod | Sol | 130 | TechTierInProduction 21 |
| round1_mod | Cent | 174 | AreaObstructed 4, InsufficientResource 1, TechTierInProduction 21 |
| round1_mod | Alien | 738 | - |
| control_nomod | Sol | 115 | InsufficientResource 4, TechTierInProduction 21 |
| control_nomod | Cent | 276 | AreaObstructed 4, TechTierInProduction 22 |
| control_nomod | Alien | 470 | - |

## Requests by kind (successful / refused) and spawns

| run | team | structure ok / refused | unit ok / refused | other kinds | SPAWN_S | SPAWN_U |
|---|---|---|---|---|---:|---:|
| round1_mod | Sol | 9 / 0 | 114 / 0 | tech 7/21 | 10 | 126 |
| round1_mod | Cent | 24 / 5 | 143 / 0 | tech 7/21 | 20 | 150 |
| round1_mod | Alien | 301 / 0 | 429 / 0 | tech 8/0 | 292 | 433 |
| control_nomod | Sol | 10 / 4 | 98 / 0 | tech 7/21 | 11 | 109 |
| control_nomod | Cent | 21 / 4 | 248 / 0 | tech 7/22 | 19 | 259 |
| control_nomod | Alien | 175 / 0 | 287 / 0 | tech 8/0 | 150 | 282 |
