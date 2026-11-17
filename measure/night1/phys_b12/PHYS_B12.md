# Physics slot on b12 (ParkedWheelHold), 2026-11-17 00:21-01:06

Build `release\b12` (md5 36E5918197BDBFE5A25D52C915A1CEF3). Startup config `night1\b12_cfg\cfg_serverperf_physics_b12.json`
(IdleForceSleep, AvoidanceSkipOntoResting, AvoidanceNoIdleDetour, ParkedWheelHold, FixedWakeTrace, IdleSleepDiag patched;
WheeledSleepDiag false). Arms by runtime flips (files `night1\b12_cfg\runtime_p12_*.json`):
A = defaults; F = IdleForceSleep + AvoidanceSkipOntoResting + AvoidanceNoIdleDetour; H = F + ParkedWheelHold; diagnostics
(FixedWakeTrace, IdleSleepDiag) OFF in A/F/H. FixedWakeTrace is patched in every arm (one bool check per ordered callback),
so absolute numbers are not comparable with phys_b10.

## phys_b12 (scenarios_physics_b10.json, cycle A,H,A,F,H,F, 60 s blocks, first 30 s after each flip dropped)

| scenario | arm | work ms | vs A (paired, s.e.) | wheeled asleep | PhysX ms/s | FixedUpdate phase ms/s | ordered fixed ms/s | CollisionAvoidance ms/s |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| idle LightStriker 300 (tight grid) | A | 3.39 | | 7 % | 133 | 232 | 99 | 26.9 |
| | F | 2.45 | -0.94 ms (-27.8 %, s.e. 0.35) | 90 % | 62 | 127 | 49 | 11.2 |
| | **H** | **2.12** | **-1.06 ms (-31.3 %, s.e. 0.01)** | **98 %** | **30** | **86** | **40** | 10.0 |
| parked mixed 300 (4 prefab grids) | A | 3.88 | | 0 % | 165 | 270 | 105 | 26.6 |
| | F | 2.91 | -0.80 ms (-20.5 %, s.e. 0.48) | 80 % | 89 | 165 | 61 | 14.3 |
| | **H** | **2.55** | **-1.47 ms (-37.9 %, 1 pair)** | **83 %** | **56** | **124** | 55 | 15.2 |
| move-vehicles 300 (guard) | A | 5.27 | | 0 % | 225 | 340 | 115 | 40.2 |
| | F | 5.25 | -1.6 % (1 block) | 0 % | 226 | 340 | 113 | 40.1 |
| | H | 5.20 | -1.6 % (1 pair) | 1 % | 221 | 336 | 111 | 38.9 |

* F's s.e. is large because F blocks follow H blocks in this cycle (H,F) and inherit H's sleeping wheels for a while;
  F in phys_b10 (A,C,A,F order) was -18.7 % / -15.6 %. H vs F within this run: about -0.3 ms (idle) / -0.4 ms (mixed).
* Census reSleep per 10 s (median): F 13 140 (idle) / 17 000 (mixed), **H 0 / 5**. sleepBlockedByForce ~1255 / 252 in both.
* `[Physics] wheelhold` in H timing blocks (median per 10 s): idle heldVehicles 0, wheelsSlept 0, leftAwakeFast 0,
  leftAwakeForce 0, syncs 250 (one SyncTransforms per fixed step); mixed 6 / 18 / 0 / 0 / 5. In the HD run the wheels
  were put to sleep once after settling (72 vehicles / 281 wheels in the first window after force-sleep started, 3 / 6
  before) and then 0 per window: **no other waker between steps**.
* Move guard: patrolling vehicles never held (asleep 0-1 %, heldVehicles 8.5 per window = vehicles stopping at patrol
  ends), 6869 orders ok / 0 failed, 300/300 alive, work equal across arms.
* Exceptions 0 in every arm (10 windows each); nothing FAILED / NOT APPLIED.

## phys_b12_HD (idle LightStriker 300, H + FixedWakeTrace + IdleSleepDiag from startup, no flips; diagnostic only)

Steady state per 10 s window:
* `fixedwake brackets=2653 watchedAvg=306 wakes=0 | preList=0 unattributed=0` (b11 arm F: wakes 49 000-55 000, all from
  VehicleWheeled.OrderedFixedUpdate). One window right after force-sleep began had wakes=3 (CollisionAvoidance).
* `idlesleep forcedNow=321 forcedSleep=0 reSleep=0 blocked=0 released=0 | rewake n=0 ... scripts=0 | bodies all=0 |
  slope=0 | drift xzMax=0.044 xzMean=0.001 dyMax=0.002 n=321` (b10 FD: scripts 56 051, slope 34 000).
* census `wheeled u=306 dyn=2730 sl=2730` = **100 % of wheeled bodies asleep**; whole world dyn 2765 / asleep 2764.
* Creep of held vehicles: max 4.4 cm, mean 1 mm since their first forced sleep (b10 arm C: mean 0.4-0.6 m, max ~3 m).
* 0 exceptions.

## probe_H_b12 (order probe, `night1\scenarios_n1_order_probe.json`, arm H fixed)

Wheeled asleep just before the 4 orders: 98 / 100 / 90 / 94 % (b10 A 95/62/22/18, F 90/83/72/74). 0 order failures,
0 never started, 0 never reached 2 m. Vehicles, p50 of the cycle p50s (range over 4 cycles), seconds:

| kind | first move A (b10) | first move H (b12) | reach 2 m A (b10) | reach 2 m F (b10) | reach 2 m H (b12) |
|---|---|---|---|---|---|
| Sol light (60) | 0.68-0.84 | 0.80-0.88 | 1.44-1.56 | 1.31-1.60 | 1.55-1.64 |
| Sol heavy (40) | 0.71-0.84 | 0.68-0.80 | 1.44-1.60 | 1.44-1.60 | 1.48-1.56 |
| Cent light (40) | 1.15-1.24 | 1.08-1.16 | 1.91-2.08 | 1.84-2.04 | 1.92-2.00 |
| Cent heavy (20) | 1.32-1.56 | 1.24-1.48 | 2.48-2.96 | 2.36-2.84 | 2.32-2.92 |

No added latency: reach-2 m within the cycle-to-cycle spread of A (Sol light at most +0.1 s, the others equal or
faster). H shows no wake "jolt" (first move like A, not F's 0.1 s). One Sol light reach max 5.3 s in cycle 1 (single
vehicle; p95 1.84 s).
