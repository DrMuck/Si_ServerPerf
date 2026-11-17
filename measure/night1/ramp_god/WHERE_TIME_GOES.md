# Where the time goes: `ramp_god` (PerfProbe sections, ms per frame; arms off, def; intervals inside one block, >= 5 s after the flip)

## step ramp-god@800

intervals / frames / fps / fixed steps per frame: off 4 / 495 / 12.3 / 1.97; def 3 / 546 / 18.1 / 1.36

(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| phase | FixedUpdate | 42.150 | 29.721 | -29 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 27.531 | 20.219 | -27 % |
| subsystem | Update.ScriptRunBehaviourUpdate | 26.311 | 17.742 | -33 % |
| phase | Update.ScriptRunBehaviourUpdate | 26.311 | 17.741 | -33 % |
| section | Ordered Update Manager - update (all) | 19.960 | 15.167 | -24 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 14.601 | 9.488 | -35 % |
| section | Ordered Update Manager - fixed (all) | 14.581 | 9.461 | -35 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 11.114 | 7.750 | -30 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 11.114 | 7.750 | -30 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 8.156 | 5.324 | -35 % |
| section | Ordered Update Manager - updateLate (all) | 6.898 | 4.650 | -33 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 4.549 | 3.756 | -17 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 3.252 | 2.164 | -33 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 2.907 | 1.939 | -33 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 2.903 | 1.852 | -36 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 2.879 | 1.796 | -38 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 2.172 | 0.339 | -84 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 1.958 | 1.479 | -24 % |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 1.933 | 1.273 | -34 % |
| section | Ordered Update Manager - fixed: TurretAimEnemy.OrderedFixedUpdate | 1.774 | 1.214 | -32 % |
| phase | PreLateUpdate | 1.738 | 0.030 | -98 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 1.694 | 0.008 | -100 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 1.033 | 0.641 | -38 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo | 0.887 | 0.589 | -34 % |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 0.864 | 0.758 | -12 % |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 0.747 | 0.308 | -59 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.743 | 0.665 | -10 % |
| section | GameManager.Update: NetworkComponent.TickNetworkObjects | 0.674 | 0.001 | -100 % |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 0.600 | 0.466 | -22 % |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 0.557 | 0.373 | -33 % |

## step ramp-god@1000

intervals / frames / fps / fixed steps per frame: off 3 / 235 / 7.8 / 2.49; def 3 / 313 / 10.4 / 1.99

(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| phase | FixedUpdate | 67.535 | 53.865 | -20 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 43.590 | 35.491 | -19 % |
| subsystem | Update.ScriptRunBehaviourUpdate | 41.815 | 29.512 | -29 % |
| phase | Update.ScriptRunBehaviourUpdate | 41.815 | 29.512 | -29 % |
| section | Ordered Update Manager - update (all) | 32.045 | 25.215 | -21 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 23.917 | 18.352 | -23 % |
| section | Ordered Update Manager - fixed (all) | 23.888 | 18.300 | -23 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 16.765 | 12.906 | -23 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 16.765 | 12.906 | -23 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 15.471 | 11.366 | -27 % |
| section | Ordered Update Manager - updateLate (all) | 10.614 | 7.691 | -28 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 6.299 | 4.962 | -21 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 4.917 | 3.716 | -24 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 4.694 | 3.717 | -21 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 4.679 | 3.765 | -20 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 4.640 | 2.868 | -38 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 3.354 | 0.548 | -84 % |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 3.051 | 2.350 | -23 % |
| section | Ordered Update Manager - fixed: TurretAimEnemy.OrderedFixedUpdate | 2.748 | 2.095 | -24 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 2.666 | 2.123 | -20 % |
| phase | PreLateUpdate | 2.196 | 0.038 | -98 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 2.142 | 0.010 | -100 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 1.777 | 1.390 | -22 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo | 1.600 | 1.264 | -21 % |
| section | GameManager.Update: NetworkComponent.TickNetworkObjects | 1.280 | 0.001 | -100 % |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 1.279 | 0.586 | -54 % |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 1.270 | 1.152 | -9 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.967 | 0.880 | -9 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart3 | 0.888 | 0.682 | -23 % |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 0.864 | 0.646 | -25 % |

