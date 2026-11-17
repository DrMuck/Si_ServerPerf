# Where the time goes: `b13_dead` (PerfProbe sections, ms per frame; arms A, B, C; intervals inside one block, >= 20 s after the flip)

## step melee-dead@450

intervals / frames / fps / fixed steps per frame: A 6 / 3232 / 53.8 / 0.47; B 7 / 3713 / 53.0 / 0.47; C 8 / 5391 / 67.3 / 0.37

(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)

| kind | name | A | B | C | change |
|---|---|---:|---:|---:|---:|
| subsystem | Update.ScriptRunBehaviourUpdate | 8.233 | 8.512 | 6.962 | -15 % |
| phase | Update.ScriptRunBehaviourUpdate | 8.233 | 8.512 | 6.962 | -15 % |
| section | Ordered Update Manager - update (all) | 7.238 | 7.297 | 6.124 | -15 % |
| phase | FixedUpdate | 6.960 | 6.840 | 4.836 | -31 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 5.205 | 4.786 | 3.470 | -33 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 3.021 | 3.074 | 2.462 | -19 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 3.021 | 3.074 | 2.461 | -19 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 2.959 | 3.029 | 2.167 | -27 % |
| section | Ordered Update Manager - updateLate (all) | 1.917 | 1.916 | 1.629 | -15 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 1.751 | 2.050 | 1.363 | -22 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 1.747 | 1.758 | 1.743 | -0 % |
| section | Ordered Update Manager - fixed (all) | 1.744 | 1.765 | 1.292 | -26 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 1.159 | 1.175 | 0.949 | -18 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.608 | 0.590 | 0.550 | -10 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.421 | 0.416 | 0.363 | -14 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 0.416 | 0.402 | 0.368 | -12 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 0.402 | 0.389 | 0.284 | -29 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 0.362 | 0.391 | 0.288 | -20 % |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 0.327 | 0.332 | 0.301 | -8 % |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 0.293 | 0.286 | 0.216 | -26 % |
| section | Ordered Update Manager - update: CreatureDecapod.OrderedUpdate | 0.275 | 0.278 | 0.224 | -18 % |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 0.236 | 0.232 | 0.221 | -6 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.OrderedLateUpdate | 0.219 | 0.216 | 0.213 | -3 % |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 0.209 | 0.300 | 0.448 | +115 % |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 0.146 | 0.149 | 0.114 | -22 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 0.134 | 0.138 | 0.103 | -23 % |
| section | GameManager.Update: Team.TickTeams | 0.106 | 0.104 | 0.103 | -3 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart3 | 0.100 | 0.105 | 0.089 | -11 % |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 0.099 | 0.097 | 0.078 | -21 % |
| section | Ordered Update Manager - update: UnitAimAt.OrderedUpdate | 0.082 | 0.079 | 0.075 | -8 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartThree | 0.075 | 0.076 | 0.057 | -24 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 0.073 | 0.075 | 0.056 | -23 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartTwo | 0.071 | 0.074 | 0.055 | -22 % |
| section | Ordered Update Manager - fixed: TurretAimEnemy.OrderedFixedUpdate | 0.070 | 0.067 | 0.050 | -28 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo | 0.059 | 0.057 | 0.044 | -26 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.UpdateAim | 0.053 | 0.051 | 0.047 | -10 % |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 0.049 | 0.048 | 0.032 | -33 % |
| section | GameManager.Update: AIManager.TickAI | 0.047 | 0.052 | 0.036 | -23 % |
| section | AIManager.TickAI: AIPathfinding.UpdateQueuedFindPaths | 0.044 | 0.050 | 0.034 | -23 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateFinish | 0.042 | 0.045 | 0.030 | -29 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart2 | 0.037 | 0.037 | 0.029 | -20 % |
| section | GameManager.Update: Unit.CompleteUpdateConcealmentAll | 0.035 | 0.038 | 0.014 | -59 % |
| section | Ordered Update Manager - fixed: VehicleAntiFlip.OrderedFixedUpdate | 0.035 | 0.034 | 0.025 | -27 % |
| section | Ordered Update Manager - update: VehicleHovered.OrderedUpdate | 0.027 | 0.024 | 0.022 | -18 % |
| section | Ordered Update Manager - update: UnitToggleLights.OrderedUpdate | 0.026 | 0.025 | 0.022 | -16 % |
| subsystem | EarlyUpdate.RendererNotifyInvisible | 0.024 | 0.024 | 0.024 | -2 % |
| subsystem | PostLateUpdate.UpdateRectTransform | 0.022 | 0.022 | 0.022 | -3 % |
| section | Ordered Update Manager - fixed: CollisionDamageManager.OrderedFixedUpdate | 0.022 | 0.020 | 0.015 | -33 % |
| subsystem | EarlyUpdate.UpdateAsyncInstantiate | 0.022 | 0.018 | 0.016 | -24 % |
| section | GameManager.Update: BaseGameObject.UpdateDestroyedDisposal | 0.020 | 0.019 | 0.014 | -30 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart2 | 0.019 | 0.021 | 0.015 | -21 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdate | 0.019 | 0.019 | 0.014 | -25 % |
| section | Ordered Update Manager - update: TurretWeapon.OrderedUpdate | 0.018 | 0.016 | 0.014 | -21 % |
| section | Ordered Update Manager - fixed: VehicleKinematicDrive.OrderedFixedUpdate | 0.017 | 0.026 | 0.012 | -29 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart3 | 0.013 | 0.013 | 0.010 | -22 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdate | 0.012 | 0.014 | 0.010 | -18 % |
| subsystem | PreLateUpdate.ParticleSystemBeginUpdateAll | 0.011 | 0.011 | 0.011 | -3 % |
| section | Ordered Update Manager - update: SimpleRotator.OrderedUpdate | 0.011 | 0.011 | 0.011 | -3 % |
| section | GameManager.Update: Unit.PrepareUpdateConcealmentAndNavmeshCutAll | 0.010 | 0.011 | 0.009 | -18 % |
| section | Ordered Update Manager - update: TurretRotator.OrderedUpdate | 0.010 | 0.009 | 0.009 | -7 % |

## step melee-dead@550

intervals / frames / fps / fixed steps per frame: A 9 / 1585 / 17.5 / 1.42; B 6 / 954 / 15.8 / 1.56; C 6 / 1624 / 27.0 / 0.93

(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)

| kind | name | A | B | C | change |
|---|---|---:|---:|---:|---:|
| phase | FixedUpdate | 25.182 | 28.593 | 15.835 | -37 % |
| subsystem | Update.ScriptRunBehaviourUpdate | 23.449 | 25.303 | 15.665 | -33 % |
| phase | Update.ScriptRunBehaviourUpdate | 23.448 | 25.303 | 15.665 | -33 % |
| section | Ordered Update Manager - update (all) | 20.135 | 21.711 | 13.619 | -32 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 18.096 | 19.257 | 11.323 | -37 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 12.611 | 13.641 | 7.573 | -40 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 7.917 | 8.684 | 5.196 | -34 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 7.917 | 8.684 | 5.196 | -34 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 7.075 | 9.323 | 4.504 | -36 % |
| section | Ordered Update Manager - fixed (all) | 7.049 | 8.252 | 4.254 | -40 % |
| section | Ordered Update Manager - updateLate (all) | 5.313 | 5.704 | 3.563 | -33 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 3.716 | 4.039 | 2.376 | -36 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 2.756 | 2.986 | 2.383 | -14 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 1.531 | 1.670 | 0.932 | -39 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 1.521 | 2.069 | 0.990 | -35 % |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 1.091 | 1.220 | 0.623 | -43 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.913 | 1.012 | 0.752 | -18 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 0.889 | 0.941 | 0.674 | -24 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.772 | 0.809 | 0.618 | -20 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 0.634 | 0.783 | 0.356 | -44 % |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 0.599 | 0.637 | 0.486 | -19 % |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 0.474 | 0.509 | 0.294 | -38 % |
| phase | Update (rest) | 0.400 | 0.472 | 0.285 | -29 % |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 0.380 | 0.387 | 0.334 | -12 % |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 0.374 | 0.442 | 0.264 | -29 % |
| section | Ordered Update Manager - update: CreatureDecapod.OrderedUpdate | 0.368 | 0.372 | 0.291 | -21 % |
| section | Ordered Update Manager - fixed: TurretAimEnemy.OrderedFixedUpdate | 0.334 | 0.342 | 0.197 | -41 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart3 | 0.331 | 0.382 | 0.236 | -29 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartTwo | 0.290 | 0.317 | 0.180 | -38 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartThree | 0.290 | 0.320 | 0.183 | -37 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.OrderedLateUpdate | 0.290 | 0.295 | 0.270 | -7 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateFinish | 0.279 | 0.292 | 0.134 | -52 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 0.242 | 0.269 | 0.176 | -27 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo | 0.223 | 0.241 | 0.125 | -44 % |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 0.188 | 0.209 | 0.110 | -42 % |
| section | GameManager.Update: Unit.CompleteUpdateConcealmentAll | 0.164 | 0.168 | 0.072 | -56 % |
| section | GameManager.Update: AIManager.TickAI | 0.158 | 0.202 | 0.125 | -21 % |
| section | AIManager.TickAI: AIPathfinding.UpdateQueuedFindPaths | 0.154 | 0.197 | 0.121 | -21 % |
| section | Ordered Update Manager - fixed: VehicleAntiFlip.OrderedFixedUpdate | 0.146 | 0.158 | 0.090 | -39 % |
| section | GameManager.Update: Team.TickTeams | 0.144 | 0.145 | 0.135 | -6 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart2 | 0.139 | 0.150 | 0.089 | -36 % |
| section | Ordered Update Manager - update: UnitAimAt.OrderedUpdate | 0.125 | 0.126 | 0.107 | -14 % |
| subsystem | EarlyUpdate.UpdateAsyncInstantiate | 0.088 | 0.076 | 0.051 | -42 % |
| section | Ordered Update Manager - fixed: CollisionDamageManager.OrderedFixedUpdate | 0.085 | 0.097 | 0.051 | -40 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart2 | 0.085 | 0.104 | 0.054 | -36 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.UpdateAim | 0.081 | 0.079 | 0.070 | -14 % |
| section | Ordered Update Manager - fixed: VehicleKinematicDrive.OrderedFixedUpdate | 0.079 | 0.111 | 0.024 | -70 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdate | 0.078 | 0.087 | 0.051 | -34 % |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 0.075 | 0.164 | 0.002 | -97 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdate | 0.061 | 0.071 | 0.032 | -48 % |
| section | Ordered Update Manager - update: VehicleHovered.OrderedUpdate | 0.057 | 0.062 | 0.042 | -27 % |
| section | Ordered Update Manager - update: UnitToggleLights.OrderedUpdate | 0.054 | 0.058 | 0.038 | -30 % |
| section | GameManager.Update: BaseGameObject.UpdateDestroyedDisposal | 0.053 | 0.111 | 0.037 | -30 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart3 | 0.051 | 0.057 | 0.033 | -35 % |
| section | Ordered Update Manager - update: TurretWeapon.OrderedUpdate | 0.035 | 0.036 | 0.017 | -51 % |
| section | GameManager.Update: Unit.PrepareUpdateConcealmentAndNavmeshCutAll | 0.029 | 0.031 | 0.018 | -37 % |
| subsystem | EarlyUpdate.RendererNotifyInvisible | 0.027 | 0.027 | 0.026 | -2 % |
| section | Ordered Update Manager - update: SimpleRotator.OrderedUpdate | 0.026 | 0.027 | 0.019 | -27 % |
| subsystem | PostLateUpdate.UpdateRectTransform | 0.025 | 0.025 | 0.024 | -5 % |
| section | Ordered Update Manager - update: VehicleSweepSensor.OrderedUpdate | 0.021 | 0.023 | 0.016 | -26 % |

