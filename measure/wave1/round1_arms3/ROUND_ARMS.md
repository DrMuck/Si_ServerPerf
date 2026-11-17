Round `round1_arms3` split into arms by flips.csv (54 flips; intervals fully inside a block and starting >= 3 s after its flip). Arms: A = 26 intervals, B = 26 intervals, C = 28 intervals

### Per 50-unit bin: normalised load ms/s (median; per-frame cost x 120 + per-step x 25), fps (median), frame p95 (median); delta vs A

| units | n A | n B | n C | load A | load B | load C | B vs A % | C vs A % | fps A | fps B | fps C | p95 A | p95 B | p95 C |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 5 | 6 | 4 | 213 | 183 | 197 | -14.1 | -7.5 | 117.7 | 118.2 | 118.4 | 10.4 | 10.6 | 10.7 |
| 50 | 4 | 4 | 6 | 247 | 229 | 227 | -7.2 | -7.9 | 116.7 | 117.0 | 117.0 | 10.8 | 10.7 | 10.8 |
| 100 | 2 | 2 | 2 | 366 | 350 | 303 | -4.4 | -17.1 | 116.3 | 116.3 | 119.3 | 11.2 | 11.5 | 11.2 |
| 150 | 1 | 1 | 2 | 432 | 367 | 389 | -15.1 | -10.1 | 116.9 | 119.4 | 116.6 | 11.7 | 11.7 | 11.6 |
| 200 | 2 | 2 | 2 | 507 | 426 | 442 | -15.9 | -12.7 | 116.1 | 116.9 | 117.0 | 12.3 | 12.2 | 12.5 |
| 250 | 10 | 9 | 8 | 632 | 524 | 515 | -17.0 | -18.5 | 114.8 | 115.8 | 117.0 | 12.8 | 12.5 | 12.4 |
| 300 | 2 | 2 | 4 | 736 | 594 | 589 | -19.3 | -20.0 | 109.8 | 115.9 | 115.7 | 13.8 | 13.0 | 13.0 |

### Fit of the normalised load (per-frame cost x 120 + per-step cost x 25, ms/s; REPORT.md 2.1) = a + b*U + c*U^2 per arm (all clean intervals, storms excluded)

| arm | n | a | b | c | R2 | at 300 u | at 500 u | at 700 u | vs A at 300 / 500 / 700 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| A | 24 | 136.4 | 1.501 | 0.00105 | 0.977 | 681 | 1148 | 1699 |  |
| B | 21 | 118.7 | 1.657 | -0.00099 | 0.963 | 526 | 699 | 792 | -22.7 % / -39.1 % / -53.4 % |
| C | 25 | 154.7 | 1.061 | 0.00088 | 0.958 | 552 | 904 | 1327 | -18.9 % / -21.2 % / -21.9 % |
(max units seen: A 314, B 294, C 306; values beyond that are extrapolated)

### ~300 units (+-15 %, storms excluded): ms per wall-second per arm; A: n=10, units 284, fps 114.3; B: n=6, units 279, fps 115.8; C: n=9, units 287, fps 116.3

| kind | name | A | B | C |
|---|---|---:|---:|---:|
| phase | TimeUpdate | 381.0 | 508.2 | 481.2 |
| phase | Initialization | 0.9 | 0.8 | 0.8 |
| phase | EarlyUpdate | 5.0 | 5.4 | 5.2 |
| phase | FixedUpdate | 110.4 | 97.9 | 102.4 |
| phase | PreUpdate | 0.6 | 0.6 | 0.6 |
| phase | Update | 0.1 | 0.2 | 0.1 |
| phase | Update.ScriptRunBehaviourUpdate | 368.3 | 288.1 | 301.0 |
| phase | Update (rest) | 1.1 | 0.9 | 1.0 |
| phase | PreLateUpdate | 33.4 | 3.5 | 3.6 |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 100.3 | 95.3 | 104.9 |
| phase | PreLateUpdate (rest) | 0.1 | 0.1 | 0.1 |
| phase | PostLateUpdate | 2.8 | 2.9 | 3.0 |
| phase | PostLateUpdate.FinishFrameRendering | 0.0 | 0.0 | 0.0 |
| phase | PostLateUpdate (rest) | 0.7 | 0.7 | 0.7 |
| phase | EndOfFrame (present, wait) | 0.1 | 0.1 | 0.1 |
| derived | main-thread busy (1000 - frame-cap wait) | 619.0 | 491.8 | 518.8 |
| derived | main-thread CPU (OS thread time) | 625.9 | 523.2 | 556.7 |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 380.6 | 507.8 | 480.8 |
| subsystem | Update.ScriptRunBehaviourUpdate | 368.3 | 288.1 | 301.1 |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 100.3 | 95.3 | 104.9 |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 72.0 | 64.7 | 67.5 |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 38.3 | 33.1 | 34.7 |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 30.8 | 1.4 | 1.4 |
| subsystem | EarlyUpdate.RendererNotifyInvisible | 3.1 | 3.3 | 3.3 |
| subsystem | PostLateUpdate.UpdateRectTransform | 1.9 | 2.0 | 2.1 |
| subsystem | PreLateUpdate.ParticleSystemBeginUpdateAll | 1.7 | 1.6 | 1.7 |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 0.6 | 0.5 | 0.6 |
| subsystem | EarlyUpdate.UpdateCanvasRectTransform | 0.6 | 0.6 | 0.6 |
| subsystem | PreLateUpdate.DirectorUpdateAnimationEnd | 0.4 | 0.1 | 0.1 |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 57.1 | 55.8 | 57.9 |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 36.0 | 35.5 | 36.6 |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 33.8 | 35.3 | 33.9 |
| section | Ordered Update Manager - update: CreatureDecapod.OrderedUpdate | 31.9 | 31.7 | 33.3 |
| section | GameManager.Update: FogOfWar.OnUpdate | 30.4 | 5.2 | 5.2 |
| section | Ordered Update Manager - update: ConstructionPlacement.UpdateAllActivePlacementsEnqueueGroundSlopeCheck | 27.1 | 27.1 | 24.7 |
| section | GameManager.Update: AIManager.TickAI | 20.3 | 17.2 | 13.3 |
| section | Ordered Update Manager - updateLate: CreatureTurret.OrderedLateUpdate | 17.7 | 16.3 | 19.9 |
| section | AIManager.TickAI: AI commanders Tick | 17.6 | 14.4 | 10.5 |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 16.0 | 15.8 | 16.4 |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 9.8 | 5.5 | 8.8 |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 9.1 | 8.0 | 8.1 |
| section | GameManager.Update: Team.TickTeams | 7.0 | 8.2 | 8.5 |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 7.0 | 6.6 | 5.4 |
| section | Ordered Update Manager - update: AlienStructure.OrderedUpdate | 6.6 | 1.9 | 2.0 |
| section | GameManager.Update: NetworkComponent.TickNetworkObjects | 6.2 | 0.1 | 0.1 |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 6.0 | 5.3 | 5.4 |
| section | Ordered Update Manager - updateLate: CreatureTurret.UpdateAim | 5.9 | 5.9 | 6.8 |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 5.0 | 1.8 | 1.7 |
| section | Ordered Update Manager - update: MiningClaws.OrderedUpdate | 4.8 | 4.3 | 4.6 |
| section | Ordered Update Manager - update: TurretWeapon.OrderedUpdate | 4.7 | 1.7 | 4.2 |
| section | Ordered Update Manager - update: AlienOrganicObject.OrderedUpdate | 4.7 | 3.9 | 4.2 |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 4.5 | 4.5 | 3.9 |
| section | Ordered Update Manager - update: HumanStructure.OrderedUpdate | 4.1 | 0.8 | 1.4 |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 3.4 | 2.9 | 3.2 |

