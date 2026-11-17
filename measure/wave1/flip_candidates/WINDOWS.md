`flip_candidates`: 54 status windows parsed

Items FAILED / NOT APPLIED in any window: none

| arm | item | windows | metric medians (windows where the item was ON) |
|---|---|---:|---|
| A | Services.CommanderAI_ThinkStats | 26 | round_thinks 0.00, avg_ms 0.00, max_ms 0.00 |
| A | Targeting.AlarmProximity_Grid | 26 | q_s 0.00 |
| A | Targeting.CollectEnemies_Grid | 26 | q_s 668.50, grid 0.00, vanilla 0.00, visited_q 0.00, D 144.50 |
| A | Targeting.DetectedGrid | 26 | builds_s 0.00, desync 0.00, entries_build 148.00 |
| A | Targeting.FriendlyFire_Grid | 26 | q_s 0.00, grid 0.00 |
| A | Targeting.NearestTarget_Grid | 26 | q_s 0.00 |
| A | Targeting.Rank_Sort | 26 | q_s 0.00 |
| A | Targeting.UrgentClose_Grid | 26 | q_s 0.00 |
| A | UpdateTiers.Tier_IdleSoldiers | 26 | run_s 483.00, early_s 72.00, skipped_pct 71.00 |
| A | UpdateTiers.Tier_OrderProcessors | 26 | skipped_pct 58.00 |
| A | UpdateTiers.Tier_Structures | 26 | skipped_pct 75.00 |
| B | Services.CommanderAI_ThinkStats | 28 | round_thinks 0.00, avg_ms 0.00, max_ms 0.00 |
| B | Targeting.AlarmProximity_Grid | 28 | q_s 0.00 |
| B | Targeting.CollectEnemies_Grid | 28 | q_s 655.00, grid 0.00, vanilla 0.00, visited_q 0.80, D 146.30 |
| B | Targeting.DetectedGrid | 28 | builds_s 0.00, desync 0.00, entries_build 148.00 |
| B | Targeting.FriendlyFire_Grid | 28 | q_s 0.00, grid 0.00 |
| B | Targeting.NearestTarget_Grid | 28 | q_s 0.00 |
| B | Targeting.Rank_Sort | 28 | q_s 0.00 |
| B | Targeting.UrgentClose_Grid | 28 | q_s 0.00 |
| B | UpdateTiers.Tier_IdleSoldiers | 28 | run_s 504.00, early_s 80.50, skipped_pct 71.00 |
| B | UpdateTiers.Tier_OrderProcessors | 28 | skipped_pct 58.00 |
| B | UpdateTiers.Tier_Structures | 28 | skipped_pct 75.00 |

### Exceptions / warnings counted by Headless.Diag_LogExceptions, per arm (delta of the running count per window; windows where counting was off are listed separately)

| arm | windows counting | exceptions+warnings | per window | windows not counting |
|---|---:|---:|---:|---:|
| A | 26 | 4 | 0.15 | 0 |
| B | 28 | 0 | 0.00 | 0 |

Detail lines printed by Diag (first few per round only): time, arm active, s since that arm's flip, message, first game frame

- 07:18:59.434 arm A (+15 s): NullReferenceException: Object reference not set to an instance of an object | at TurretWeapon.OrderedUpdate (System.Single dt) (at <b462779a69c44df78777fff6c32d2791>:0)

