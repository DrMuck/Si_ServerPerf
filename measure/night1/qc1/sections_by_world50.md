# Where the time goes: `qc1` (PerfProbe sections, ms per frame; arms off, def; intervals inside one block, >= 5 s after the flip; trickle binned by world units)

## step 200-249 world units

intervals / frames / fps: off 1 / 1182 / 118.2; def 1 / 1191 / 119.0

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| phase | TimeUpdate | 3.722 | 5.033 | +35 % |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 3.719 | 5.030 | +35 % |
| subsystem | Update.ScriptRunBehaviourUpdate | 2.607 | 1.749 | -33 % |
| phase | Update.ScriptRunBehaviourUpdate | 2.607 | 1.749 | -33 % |
| section | Ordered Update Manager - update (all) | 1.773 | 1.374 | -23 % |
| phase | FixedUpdate | 1.064 | 0.954 | -10 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 0.773 | 0.714 | -8 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 0.750 | 0.528 | -30 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 0.750 | 0.528 | -30 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 0.495 | 0.348 | -30 % |
| section | Ordered Update Manager - updateLate (all) | 0.483 | 0.337 | -30 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 0.353 | 0.262 | -26 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 0.290 | 0.239 | -18 % |
| section | Ordered Update Manager - fixed (all) | 0.289 | 0.236 | -18 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 0.235 | 0.143 | -39 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 0.217 | 0.003 | -99 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.204 | 0.152 | -26 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.202 | 0.201 | -0 % |

## step 250-299 world units

intervals / frames / fps: off 1 / 1173 / 117.3; def 2 / 2332 / 116.5

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| phase | TimeUpdate | 3.615 | 3.789 | +5 % |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 3.611 | 3.786 | +5 % |
| subsystem | Update.ScriptRunBehaviourUpdate | 2.632 | 2.524 | -4 % |
| phase | Update.ScriptRunBehaviourUpdate | 2.631 | 2.524 | -4 % |
| section | Ordered Update Manager - update (all) | 1.937 | 2.084 | +8 % |
| phase | FixedUpdate | 1.137 | 1.264 | +11 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 0.829 | 0.929 | +12 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 0.812 | 0.901 | +11 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 0.812 | 0.901 | +11 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 0.520 | 0.594 | +14 % |
| section | Ordered Update Manager - updateLate (all) | 0.496 | 0.548 | +10 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 0.386 | 0.451 | +17 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 0.306 | 0.334 | +9 % |
| section | Ordered Update Manager - fixed (all) | 0.304 | 0.331 | +9 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.276 | 0.247 | -10 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 0.228 | 0.003 | -99 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 0.227 | 0.267 | +17 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.207 | 0.229 | +10 % |

## step 500-549 world units

intervals / frames / fps: off 1 / 661 / 66.1; def 1 / 703 / 70.2

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| subsystem | Update.ScriptRunBehaviourUpdate | 6.749 | 6.561 | -3 % |
| phase | Update.ScriptRunBehaviourUpdate | 6.749 | 6.561 | -3 % |
| section | Ordered Update Manager - update (all) | 5.326 | 5.713 | +7 % |
| phase | FixedUpdate | 4.898 | 4.510 | -8 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 3.733 | 3.431 | -8 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 2.373 | 2.259 | -5 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 2.373 | 2.258 | -5 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 1.711 | 2.034 | +19 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 1.446 | 1.494 | +3 % |
| section | Ordered Update Manager - updateLate (all) | 1.409 | 1.385 | -2 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 1.162 | 1.075 | -7 % |
| section | Ordered Update Manager - fixed (all) | 1.158 | 1.069 | -8 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 0.808 | 0.787 | -3 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.607 | 0.598 | -2 % |
| phase | PreLateUpdate | 0.567 | 0.017 | -97 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 0.543 | 0.003 | -99 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.369 | 0.374 | +1 % |
| phase | TimeUpdate | 0.335 | 0.723 | +115 % |

## step 600-649 world units

intervals / frames / fps: off 1 / 425 / 42.4; def 1 / 445 / 44.5

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| subsystem | Update.ScriptRunBehaviourUpdate | 10.400 | 10.028 | -4 % |
| phase | Update.ScriptRunBehaviourUpdate | 10.400 | 10.028 | -4 % |
| phase | FixedUpdate | 8.864 | 8.687 | -2 % |
| section | Ordered Update Manager - update (all) | 8.239 | 8.623 | +5 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 6.705 | 6.553 | -2 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 3.780 | 3.733 | -1 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 3.299 | 3.453 | +5 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 3.299 | 3.453 | +5 % |
| section | Ordered Update Manager - updateLate (all) | 2.226 | 2.168 | -3 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 2.154 | 2.129 | -1 % |
| section | Ordered Update Manager - fixed (all) | 2.149 | 2.119 | -1 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 1.737 | 2.027 | +17 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 1.415 | 1.325 | -6 % |
| phase | PreLateUpdate | 0.684 | 0.019 | -97 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.667 | 0.749 | +12 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 0.657 | 0.004 | -99 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 0.608 | 0.576 | -5 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.491 | 0.492 | +0 % |

## step 700-749 world units

intervals / frames / fps: off 1 / 103 / 10.3; def 1 / 222 / 22.2

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| phase | FixedUpdate | 41.625 | 19.218 | -54 % |
| subsystem | Update.ScriptRunBehaviourUpdate | 41.094 | 18.762 | -54 % |
| phase | Update.ScriptRunBehaviourUpdate | 41.093 | 18.762 | -54 % |
| section | Ordered Update Manager - update (all) | 33.294 | 16.039 | -52 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 29.532 | 14.337 | -51 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 23.902 | 9.542 | -60 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 12.073 | 4.872 | -60 % |
| section | Ordered Update Manager - fixed (all) | 12.046 | 4.850 | -60 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 11.377 | 6.698 | -41 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 11.377 | 6.697 | -41 % |
| section | Ordered Update Manager - updateLate (all) | 7.945 | 4.327 | -46 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 5.343 | 3.024 | -43 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 3.063 | 2.458 | -20 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 3.003 | 1.243 | -59 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 2.568 | 0.983 | -62 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 1.647 | 0.749 | -55 % |
| phase | Update (rest) | 1.579 | 0.376 | -76 % |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 1.455 | 0.352 | -76 % |

## step 800-849 world units

intervals / frames / fps: off 1 / 84 / 8.3; def 1 / 119 / 11.9

| kind | name | off | def | change |
|---|---|---:|---:|---:|
| subsystem | Update.ScriptRunBehaviourUpdate | 52.056 | 34.531 | -34 % |
| phase | Update.ScriptRunBehaviourUpdate | 52.055 | 34.530 | -34 % |
| phase | FixedUpdate | 51.655 | 38.015 | -26 % |
| section | Ordered Update Manager - update (all) | 43.094 | 29.949 | -31 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 36.559 | 27.657 | -24 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 31.645 | 20.584 | -35 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 15.072 | 10.341 | -31 % |
| section | Ordered Update Manager - fixed (all) | 15.041 | 10.296 | -32 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 12.746 | 10.819 | -15 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 12.746 | 10.819 | -15 % |
| section | Ordered Update Manager - updateLate (all) | 8.067 | 7.149 | -11 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 5.168 | 5.177 | +0 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 3.742 | 3.385 | -10 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 3.679 | 2.584 | -30 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 3.595 | 2.287 | -36 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 1.964 | 1.182 | -40 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 1.740 | 0.331 | -81 % |
| phase | Update (rest) | 1.715 | 0.822 | -52 % |

