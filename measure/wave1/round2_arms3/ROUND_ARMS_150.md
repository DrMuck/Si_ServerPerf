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

### ~150 units (+-15 %, storms excluded): ms per wall-second per arm; A: n=21, units 151, fps 119.6; B: n=21, units 150, fps 119.8; C: n=20, units 148, fps 119.8

| kind | name | A | B | C |
|---|---|---:|---:|---:|
| phase | TimeUpdate | 608.8 | 656.3 | 689.5 |
| phase | Initialization | 0.9 | 0.9 | 0.9 |
| phase | EarlyUpdate | 6.2 | 6.2 | 6.4 |
| phase | FixedUpdate | 122.2 | 118.0 | 119.5 |
| phase | PreUpdate | 0.7 | 0.7 | 0.7 |
| phase | Update | 0.2 | 0.2 | 0.2 |
| phase | Update.ScriptRunBehaviourUpdate | 220.0 | 183.7 | 152.1 |
| phase | Update (rest) | 0.9 | 0.9 | 1.0 |
| phase | PreLateUpdate | 10.7 | 2.8 | 1.9 |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 29.3 | 30.2 | 29.5 |
| phase | PreLateUpdate (rest) | 0.1 | 0.1 | 0.1 |
| phase | PostLateUpdate | 3.5 | 3.6 | 1.6 |
| phase | PostLateUpdate.FinishFrameRendering | 0.0 | 0.0 | 0.0 |
| phase | PostLateUpdate (rest) | 0.8 | 0.8 | 0.8 |
| phase | EndOfFrame (present, wait) | 0.1 | 0.1 | 0.1 |
| derived | main-thread busy (1000 - frame-cap wait) | 391.2 | 343.7 | 310.5 |
| derived | main-thread CPU (OS thread time) | 409.4 | 371.7 | 341.2 |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 608.3 | 655.8 | 689.1 |
| subsystem | Update.ScriptRunBehaviourUpdate | 220.1 | 183.7 | 152.1 |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 87.2 | 84.4 | 85.9 |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 34.9 | 33.4 | 33.4 |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 29.3 | 30.2 | 29.5 |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 8.2 | 1.0 | 0.9 |
| subsystem | EarlyUpdate.RendererNotifyInvisible | 3.7 | 3.8 | 3.8 |
| subsystem | PostLateUpdate.UpdateRectTransform | 2.5 | 2.6 | 0.7 |
| subsystem | PreLateUpdate.ParticleSystemBeginUpdateAll | 1.9 | 1.3 | 0.5 |
| subsystem | EarlyUpdate.UpdateCanvasRectTransform | 0.8 | 0.8 | 0.8 |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 0.5 | 0.5 | 0.6 |
| subsystem | EarlyUpdate.UpdateAsyncInstantiate | 0.3 | 0.2 | 0.4 |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 24.3 | 27.3 | 5.3 |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 21.7 | 18.2 | 13.2 |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 20.8 | 17.9 | 15.5 |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 19.3 | 20.0 | 23.3 |
| section | GameManager.Update: FogOfWar.OnUpdate | 14.9 | 3.2 | 3.3 |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 14.5 | 14.0 | 14.3 |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 11.2 | 11.6 | 10.8 |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 8.5 | 8.3 | 8.3 |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 6.8 | 5.9 | 6.1 |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 6.4 | 6.0 | 6.0 |
| section | Ordered Update Manager - update: HumanStructure.OrderedUpdate | 5.7 | 1.7 | 1.6 |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 4.1 | 3.9 | 3.9 |
| section | GameManager.Update: NetworkComponent.TickNetworkObjects | 3.7 | 0.2 | 0.2 |
| section | GameManager.Update: AIManager.TickAI | 3.7 | 3.8 | 3.9 |
| section | GameManager.Update: Team.TickTeams | 3.6 | 4.3 | 4.4 |
| section | Ordered Update Manager - update: PurchasePointUI.OrderedUpdate | 3.1 | 3.0 | 3.2 |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartThree | 2.9 | 2.9 | 3.0 |
| section | Ordered Update Manager - update: SimpleRotator.OrderedUpdate | 2.6 | 2.7 | 2.6 |
| section | Ordered Update Manager - update: UnitAimAt.OrderedUpdate | 2.4 | 2.4 | 2.4 |
| section | GameManager.Update: BaseGameObject.UpdateDestroyedDisposal | 2.0 | 0.7 | 0.7 |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartTwo | 2.0 | 2.0 | 2.1 |
| section | Ordered Update Manager - update: TurretWeapon.OrderedUpdate | 2.0 | 2.8 | 1.9 |
| section | Ordered Update Manager - update: GameManager.SensorTickPart3 | 1.9 | 1.9 | 0.5 |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 1.8 | 0.8 | 0.7 |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 1.7 | 1.7 | 1.5 |

