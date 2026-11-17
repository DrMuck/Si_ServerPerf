# Where the time goes: `b13_snap` (PerfProbe sections, ms per frame; arms A, S1, S2, S3, L; intervals inside one block, >= 5 s after the flip)

## step melee-snap@450

intervals / frames / fps / fixed steps per frame: A 3 / 1813 / 60.4 / 0.42; S1 3 / 1876 / 62.5 / 0.40; S2 2 / 1270 / 63.5 / 0.40; S3 3 / 1884 / 62.7 / 0.40; L 3 / 1968 / 65.5 / 0.38

(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)

| kind | name | A | S1 | S2 | S3 | L | change |
|---|---|---:|---:|---:|---:|---:|---:|
| subsystem | Update.ScriptRunBehaviourUpdate | 7.607 | 7.233 | 7.121 | 7.437 | 6.992 | -8 % |
| phase | Update.ScriptRunBehaviourUpdate | 7.607 | 7.233 | 7.121 | 7.437 | 6.992 | -8 % |
| section | Ordered Update Manager - update (all) | 6.686 | 6.361 | 6.269 | 6.087 | 6.074 | -9 % |
| phase | FixedUpdate | 5.779 | 5.632 | 5.607 | 5.612 | 5.197 | -10 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 4.319 | 4.224 | 4.197 | 4.145 | 3.819 | -12 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 2.823 | 2.516 | 2.135 | 2.077 | 2.560 | -9 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 2.576 | 2.436 | 2.413 | 2.398 | 2.253 | -13 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 2.576 | 2.436 | 2.413 | 2.398 | 2.253 | -13 % |
| section | Ordered Update Manager - updateLate (all) | 1.621 | 1.555 | 1.550 | 1.542 | 1.486 | -8 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 1.456 | 1.405 | 1.406 | 1.463 | 1.375 | -6 % |
| section | Ordered Update Manager - fixed (all) | 1.450 | 1.399 | 1.400 | 1.457 | 1.370 | -6 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 1.429 | 1.452 | 1.667 | 1.516 | 1.289 | -10 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 0.950 | 0.894 | 0.876 | 0.882 | 0.876 | -8 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.587 | 0.570 | 0.629 | 0.643 | 0.518 | -12 % |
| phase | TimeUpdate | 0.470 | 0.563 | 0.489 | 0.383 | 0.708 | +51 % |
| subsystem | TimeUpdate.WaitForLastPresentationAndUpdateTime | 0.468 | 0.560 | 0.487 | 0.380 | 0.706 | +51 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.388 | 0.380 | 0.380 | 0.379 | 0.369 | -5 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 0.342 | 0.330 | 0.347 | 0.331 | 0.299 | -13 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 0.339 | 0.334 | 0.327 | 0.327 | 0.311 | -8 % |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 0.306 | 0.306 | 0.306 | 0.318 | 0.296 | -3 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 0.280 | 0.272 | 0.286 | 0.329 | 0.283 | +1 % |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 0.263 | 0.259 | 0.259 | 0.260 | 0.257 | -2 % |
| section | Ordered Update Manager - update: CreatureDecapod.OrderedUpdate | 0.253 | 0.258 | 0.262 | 0.260 | 0.250 | -1 % |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 0.221 | 0.224 | 0.228 | 0.223 | 0.216 | -2 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.OrderedLateUpdate | 0.220 | 0.222 | 0.223 | 0.223 | 0.219 | -0 % |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 0.154 | 0.150 | 0.143 | 0.145 | 0.065 | -58 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart3 | 0.140 | 0.132 | 0.134 | 0.150 | 0.151 | +8 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 0.113 | 0.112 | 0.112 | 0.119 | 0.111 | -2 % |
| section | GameManager.Update: Team.TickTeams | 0.093 | 0.093 | 0.093 | 0.091 | 0.090 | -4 % |
| section | Ordered Update Manager - update: UnitAimAt.OrderedUpdate | 0.068 | 0.070 | 0.072 | 0.069 | 0.062 | -8 % |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 0.065 | 0.065 | 0.063 | 0.051 | 0.053 | -19 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 0.065 | 0.062 | 0.060 | 0.064 | 0.059 | -9 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartThree | 0.063 | 0.058 | 0.059 | 0.058 | 0.060 | -5 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartTwo | 0.059 | 0.056 | 0.056 | 0.057 | 0.055 | -8 % |
| section | GameManager.Update: AIManager.TickAI | 0.047 | 0.048 | 0.045 | 0.045 | 0.044 | -6 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo | 0.047 | 0.048 | 0.049 | 0.048 | 0.044 | -6 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.UpdateAim | 0.046 | 0.041 | 0.046 | 0.043 | 0.041 | -11 % |
| section | AIManager.TickAI: AIPathfinding.UpdateQueuedFindPaths | 0.045 | 0.046 | 0.043 | 0.043 | 0.042 | -6 % |
| section | Ordered Update Manager - fixed: TurretAimEnemy.OrderedFixedUpdate | 0.044 | 0.027 | 0.026 | 0.025 | 0.038 | -14 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart2 | 0.043 | 0.038 | 0.041 | 0.044 | 0.040 | -8 % |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 0.041 | 0.040 | 0.041 | 0.041 | 0.037 | -11 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateFinish | 0.038 | 0.042 | 0.032 | 0.039 | 0.027 | -27 % |
| section | Ordered Update Manager - fixed: VehicleAntiFlip.OrderedFixedUpdate | 0.030 | 0.029 | 0.029 | 0.029 | 0.026 | -13 % |
| section | Ordered Update Manager - update: UnitToggleLights.OrderedUpdate | 0.025 | 0.025 | 0.025 | 0.025 | 0.023 | -7 % |
| section | Ordered Update Manager - update: VehicleHovered.OrderedUpdate | 0.023 | 0.024 | 0.024 | 0.023 | 0.022 | -7 % |
| subsystem | EarlyUpdate.RendererNotifyInvisible | 0.023 | 0.024 | 0.023 | 0.023 | 0.024 | +1 % |
| section | GameManager.Update: Unit.CompleteUpdateConcealmentAll | 0.021 | 0.019 | 0.022 | 0.032 | 0.019 | -7 % |
| subsystem | PostLateUpdate.UpdateRectTransform | 0.020 | 0.021 | 0.021 | 0.021 | 0.020 | -1 % |
| subsystem | EarlyUpdate.UpdateAsyncInstantiate | 0.020 | 0.018 | 0.015 | 0.015 | 0.014 | -32 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart2 | 0.020 | 0.017 | 0.016 | 0.018 | 0.018 | -10 % |
| section | Ordered Update Manager - fixed: CollisionDamageManager.OrderedFixedUpdate | 0.018 | 0.017 | 0.017 | 0.017 | 0.015 | -13 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdate | 0.016 | 0.016 | 0.016 | 0.016 | 0.015 | -9 % |
| section | GameManager.Update: BaseGameObject.UpdateDestroyedDisposal | 0.015 | 0.015 | 0.015 | 0.013 | 0.012 | -18 % |
| section | Ordered Update Manager - fixed: VehicleKinematicDrive.OrderedFixedUpdate | 0.013 | 0.013 | 0.004 | 0.016 | 0.011 | -16 % |
| section | Ordered Update Manager - update: SimpleRotator.OrderedUpdate | 0.012 | 0.012 | 0.012 | 0.011 | 0.011 | -3 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart3 | 0.012 | 0.011 | 0.012 | 0.011 | 0.011 | -9 % |
| subsystem | PreLateUpdate.ParticleSystemBeginUpdateAll | 0.011 | 0.010 | 0.011 | 0.010 | 0.010 | -3 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdate | 0.010 | 0.010 | 0.010 | 0.009 | 0.010 | -3 % |
| section | GameManager.Update: Unit.PrepareUpdateConcealmentAndNavmeshCutAll | 0.009 | 0.009 | 0.009 | 0.010 | 0.009 | +3 % |
| section | Ordered Update Manager - update: VehicleSweepSensor.OrderedUpdate | 0.009 | 0.009 | 0.009 | 0.009 | 0.008 | -3 % |
| section | Ordered Update Manager - update: TurretRotator.OrderedUpdate | 0.007 | 0.007 | 0.007 | 0.007 | 0.008 | +13 % |
| section | Ordered Update Manager - update: TurretWeapon.OrderedUpdate | 0.006 | 0.006 | 0.006 | 0.006 | 0.006 | -0 % |
| section | Ordered Update Manager - update: AutoHeal.OrderedUpdate | 0.006 | 0.005 | 0.005 | 0.005 | 0.005 | -12 % |
| section | Ordered Update Manager - update: PurchasePointUI.OrderedUpdate | 0.005 | 0.005 | 0.005 | 0.005 | 0.005 | -3 % |
| section | Ordered Update Manager - updateLate: LineOfSightManager.SwapAndSchedule | 0.004 | 0.004 | 0.004 | 0.005 | 0.004 | -13 % |
| subsystem | EarlyUpdate.UpdateCanvasRectTransform | 0.004 | 0.004 | 0.004 | 0.004 | 0.004 | +2 % |
| subsystem | Update.DirectorUpdate | 0.004 | 0.004 | 0.004 | 0.003 | 0.003 | -22 % |
| section | Ordered Update Manager - update: MiningArmsBase.OrderedUpdate | 0.004 | 0.003 | 0.003 | 0.003 | 0.003 | -15 % |
| section | GameManager.Update: Unit.UpdateFallThroughGround | 0.004 | 0.004 | 0.004 | 0.005 | 0.003 | -30 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 0.004 | 0.004 | 0.004 | 0.004 | 0.004 | -1 % |
| section | CollisionObstacleManager.ApplyChanges: navmesh cut ForceUpdate + FlushWorkItems | 0.004 | 0.002 | 0.000 | 0.012 | 0.006 | +56 % |
| section | Ordered Update Manager - update: CreatureAttackGlowTongue.OrderedUpdate | 0.003 | 0.004 | 0.003 | 0.003 | 0.004 | +7 % |
| section | GameManager.Update: Target.TickTargetsWithEvent | 0.003 | 0.003 | 0.003 | 0.003 | 0.002 | -14 % |
| section | Ordered Update Manager - update: HumanStructure.OrderedUpdate | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | -6 % |
| section | Ordered Update Manager - fixed: MiningArmsBase.OrderedFixedUpdate | 0.002 | 0.001 | 0.002 | 0.000 | 0.001 | -40 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedUpdateBatchedRaycasts | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | -7 % |
| section | Ordered Update Manager - updateLate: Piston.OrderedLateUpdate | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | -8 % |
| section | Ordered Update Manager - updateLate: TeleportUI.OrderedLateUpdate | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | -2 % |
| subsystem | Initialization.ProfilerStartFrame | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | +3 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedUpdateBatchedRaycasts | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | -0 % |

## step melee-snap@550

intervals / frames / fps / fixed steps per frame: A 2 / 451 / 22.5 / 1.12; S1 2 / 407 / 20.3 / 1.23; S2 2 / 505 / 25.2 / 1.00; S3 2 / 529 / 26.3 / 0.95; L 2 / 478 / 23.9 / 1.05

(FixedUpdate rows are per frame and scale with fixed steps per frame; divide by that column for ms per fixed step.)

| kind | name | A | S1 | S2 | S3 | L | change |
|---|---|---:|---:|---:|---:|---:|---:|
| subsystem | Update.ScriptRunBehaviourUpdate | 19.299 | 20.314 | 15.945 | 15.197 | 17.799 | -8 % |
| phase | Update.ScriptRunBehaviourUpdate | 19.298 | 20.314 | 15.945 | 15.197 | 17.799 | -8 % |
| phase | FixedUpdate | 19.119 | 21.811 | 17.983 | 17.103 | 18.446 | -4 % |
| section | Ordered Update Manager - update (all) | 17.073 | 17.477 | 13.842 | 13.187 | 15.721 | -8 % |
| subsystem | FixedUpdate.PhysicsFixedUpdate | 13.905 | 15.759 | 13.215 | 12.504 | 13.323 | -4 % |
| section | Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents | 10.674 | 10.493 | 7.607 | 6.878 | 9.768 | -8 % |
| subsystem | PreLateUpdate.ScriptRunBehaviourLateUpdate | 5.860 | 6.480 | 5.555 | 5.470 | 5.393 | -8 % |
| phase | PreLateUpdate.ScriptRunBehaviourLateUpdate | 5.860 | 6.480 | 5.555 | 5.470 | 5.393 | -8 % |
| subsystem | FixedUpdate.ScriptRunBehaviourFixedUpdate | 5.204 | 6.042 | 4.760 | 4.591 | 5.114 | -2 % |
| section | Ordered Update Manager - fixed (all) | 5.184 | 6.020 | 4.741 | 4.575 | 5.096 | -2 % |
| section | Ordered Update Manager - updateLate (all) | 3.911 | 4.166 | 3.557 | 3.548 | 3.794 | -3 % |
| section | Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate | 2.733 | 2.940 | 2.402 | 2.373 | 2.705 | -1 % |
| section | Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets | 1.989 | 2.159 | 1.985 | 2.089 | 1.850 | -7 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate | 1.196 | 1.295 | 1.083 | 1.006 | 1.122 | -6 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate | 1.126 | 1.495 | 1.067 | 1.075 | 1.212 | +8 % |
| section | Ordered Update Manager - fixed: CreatureDecapod.OrderedFixedUpdate | 0.851 | 1.013 | 0.805 | 0.772 | 0.826 | -3 % |
| section | Ordered Update Manager - update: Soldier.OrderedUpdate | 0.844 | 0.929 | 0.751 | 0.845 | 0.861 | +2 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart1 | 0.641 | 0.703 | 0.621 | 0.623 | 0.633 | -1 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateAll | 0.595 | 0.658 | 0.578 | 0.616 | 0.580 | -3 % |
| section | Ordered Update Manager - update: VehicleWheeled.OrderedUpdate | 0.537 | 0.582 | 0.522 | 0.532 | 0.552 | +3 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart3 | 0.485 | 0.567 | 0.491 | 0.403 | 0.484 | -0 % |
| section | Ordered Update Manager - update: LineOfSightManager.PublishResults | 0.476 | 0.514 | 0.443 | 0.408 | 0.169 | -64 % |
| section | Ordered Update Manager - fixed: Silica.AI.CollisionAvoidanceManager.PrepareFixedUpdate | 0.447 | 0.514 | 0.388 | 0.376 | 0.422 | -5 % |
| section | Ordered Update Manager - update: Silica.AI.AIOrderProcessor.OrderedUpdate | 0.348 | 0.359 | 0.342 | 0.345 | 0.340 | -2 % |
| section | Ordered Update Manager - update: CreatureDecapod.OrderedUpdate | 0.327 | 0.337 | 0.334 | 0.337 | 0.323 | -1 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.OrderedLateUpdate | 0.283 | 0.286 | 0.287 | 0.293 | 0.285 | +1 % |
| subsystem | Update.ScriptRunDelayedDynamicFrameRate | 0.223 | 0.263 | 0.198 | 0.200 | 0.211 | -5 % |
| section | GameManager.Update: FogOfWar.OnUpdate | 0.210 | 0.213 | 0.192 | 0.187 | 0.183 | -12 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartTwo | 0.196 | 0.221 | 0.181 | 0.178 | 0.190 | -3 % |
| section | Ordered Update Manager - updateLate: Soldier.OrderedLateUpdateFinish | 0.190 | 0.163 | 0.187 | 0.153 | 0.122 | -36 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdatePartThree | 0.187 | 0.215 | 0.181 | 0.177 | 0.197 | +5 % |
| section | Ordered Update Manager - fixed: TurretAimEnemy.OrderedFixedUpdate | 0.174 | 0.114 | 0.092 | 0.089 | 0.153 | -12 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdatePartTwo | 0.172 | 0.177 | 0.147 | 0.132 | 0.140 | -19 % |
| section | GameManager.Update: AIManager.TickAI | 0.170 | 0.183 | 0.150 | 0.139 | 0.152 | -11 % |
| section | AIManager.TickAI: AIPathfinding.UpdateQueuedFindPaths | 0.165 | 0.179 | 0.146 | 0.135 | 0.148 | -11 % |
| section | Ordered Update Manager - fixed: NetworkTransformComponent.OrderedFixedUpdate | 0.145 | 0.164 | 0.135 | 0.133 | 0.140 | -3 % |
| section | GameManager.Update: Team.TickTeams | 0.121 | 0.134 | 0.127 | 0.126 | 0.120 | -0 % |
| section | Ordered Update Manager - fixed: VehicleAntiFlip.OrderedFixedUpdate | 0.111 | 0.122 | 0.102 | 0.111 | 0.112 | +0 % |
| section | Ordered Update Manager - update: GameManager.SensorTickPart2 | 0.106 | 0.116 | 0.107 | 0.094 | 0.107 | +1 % |
| section | Ordered Update Manager - update: UnitAimAt.OrderedUpdate | 0.106 | 0.112 | 0.102 | 0.104 | 0.099 | -6 % |
| section | GameManager.Update: Unit.CompleteUpdateConcealmentAll | 0.098 | 0.119 | 0.086 | 0.083 | 0.088 | -10 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart2 | 0.066 | 0.077 | 0.059 | 0.059 | 0.062 | -6 % |
| section | Ordered Update Manager - updateLate: CreatureTurret.UpdateAim | 0.065 | 0.066 | 0.057 | 0.061 | 0.058 | -10 % |
| section | Ordered Update Manager - fixed: CollisionDamageManager.OrderedFixedUpdate | 0.063 | 0.070 | 0.060 | 0.057 | 0.063 | -1 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdate | 0.058 | 0.068 | 0.057 | 0.052 | 0.057 | -1 % |
| subsystem | EarlyUpdate.UpdateAsyncInstantiate | 0.050 | 0.056 | 0.054 | 0.041 | 0.048 | -3 % |
| section | Ordered Update Manager - update: UnitToggleLights.OrderedUpdate | 0.048 | 0.055 | 0.047 | 0.046 | 0.047 | -2 % |
| section | Ordered Update Manager - update: VehicleHovered.OrderedUpdate | 0.048 | 0.052 | 0.048 | 0.044 | 0.048 | +1 % |
| section | Ordered Update Manager - fixed: VehicleKinematicDrive.OrderedFixedUpdate | 0.041 | 0.076 | 0.050 | 0.029 | 0.052 | +28 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedFixedUpdatePart3 | 0.039 | 0.043 | 0.035 | 0.034 | 0.038 | -4 % |
| section | GameManager.Update: BaseGameObject.UpdateDestroyedDisposal | 0.036 | 0.052 | 0.035 | 0.033 | 0.038 | +3 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedFixedUpdate | 0.036 | 0.041 | 0.034 | 0.035 | 0.032 | -11 % |
| subsystem | EarlyUpdate.RendererNotifyInvisible | 0.027 | 0.026 | 0.026 | 0.027 | 0.026 | -2 % |
| subsystem | PostLateUpdate.UpdateRectTransform | 0.026 | 0.025 | 0.024 | 0.024 | 0.024 | -5 % |
| section | Ordered Update Manager - update: SimpleRotator.OrderedUpdate | 0.022 | 0.026 | 0.022 | 0.021 | 0.021 | -6 % |
| section | GameManager.Update: Unit.PrepareUpdateConcealmentAndNavmeshCutAll | 0.021 | 0.023 | 0.019 | 0.019 | 0.020 | -7 % |
| section | Ordered Update Manager - update: VehicleSweepSensor.OrderedUpdate | 0.018 | 0.020 | 0.017 | 0.017 | 0.017 | -4 % |
| subsystem | Update.DirectorUpdate | 0.015 | 0.016 | 0.011 | 0.013 | 0.012 | -24 % |
| section | Ordered Update Manager - update: PurchasePointUI.OrderedUpdate | 0.014 | 0.016 | 0.013 | 0.013 | 0.013 | -3 % |
| subsystem | PreLateUpdate.ParticleSystemBeginUpdateAll | 0.013 | 0.014 | 0.013 | 0.013 | 0.014 | +6 % |
| section | Ordered Update Manager - update: MiningArmsBase.OrderedUpdate | 0.009 | 0.010 | 0.011 | 0.010 | 0.011 | +15 % |
| section | Ordered Update Manager - update: TurretWeapon.OrderedUpdate | 0.009 | 0.010 | 0.009 | 0.009 | 0.009 | -5 % |
| section | Ordered Update Manager - update: TurretRotator.OrderedUpdate | 0.009 | 0.010 | 0.009 | 0.009 | 0.009 | -2 % |
| section | Ordered Update Manager - update: AutoHeal.OrderedUpdate | 0.008 | 0.010 | 0.009 | 0.010 | 0.008 | -6 % |
| subsystem | PreLateUpdate.DirectorUpdateAnimationBegin | 0.008 | 0.008 | 0.008 | 0.007 | 0.007 | -1 % |
| section | Ordered Update Manager - update: CreatureAttackGlowTongue.OrderedUpdate | 0.007 | 0.008 | 0.007 | 0.007 | 0.006 | -10 % |
| subsystem | EarlyUpdate.UpdateCanvasRectTransform | 0.007 | 0.008 | 0.007 | 0.007 | 0.007 | -2 % |
| section | Ordered Update Manager - fixed: VehicleHovered.OrderedUpdateBatchedRaycasts | 0.005 | 0.006 | 0.005 | 0.005 | 0.005 | -1 % |
| section | Ordered Update Manager - updateLate: LineOfSightManager.SwapAndSchedule | 0.005 | 0.005 | 0.004 | 0.010 | 0.004 | -11 % |
| section | Ordered Update Manager - fixed: VehicleWheeled.OrderedUpdateBatchedRaycasts | 0.005 | 0.005 | 0.004 | 0.004 | 0.004 | -7 % |
| section | GameManager.Update: Unit.UpdateFallThroughGround | 0.005 | 0.006 | 0.005 | 0.008 | 0.004 | -5 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedUpdateBatchedSpherecasts2 | 0.004 | 0.005 | 0.004 | 0.004 | 0.004 | -2 % |
| section | Ordered Update Manager - update: HumanStructure.OrderedUpdate | 0.004 | 0.004 | 0.004 | 0.004 | 0.004 | -1 % |
| section | Ordered Update Manager - update: Drone.OrderedUpdate | 0.004 | 0.004 | 0.003 | 0.004 | 0.003 | -17 % |
| section | GameManager.Update: Target.TickTargetsWithEvent | 0.004 | 0.005 | 0.004 | 0.004 | 0.004 | -7 % |
| section | Ordered Update Manager - update: VehicleDispenser.OrderedUpdate | 0.004 | 0.004 | 0.004 | 0.003 | 0.004 | -4 % |
| section | Ordered Update Manager - updateLate: Piston.OrderedLateUpdate | 0.004 | 0.004 | 0.004 | 0.004 | 0.004 | -1 % |
| section | Ordered Update Manager - updateLate: TeleportUI.OrderedLateUpdate | 0.004 | 0.004 | 0.003 | 0.003 | 0.003 | -5 % |
| subsystem | PostLateUpdate.DirectorLateUpdate | 0.004 | 0.004 | 0.003 | 0.004 | 0.003 | -6 % |
| section | Ordered Update Manager - fixed: Soldier.OrderedUpdateBatchedSpherecasts | 0.003 | 0.004 | 0.003 | 0.003 | 0.003 | -8 % |

