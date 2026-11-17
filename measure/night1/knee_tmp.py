import sys, os
sys.path.insert(0, ".")
import ab_report as R
run = r"night1\qc1"
G = R.round_load(run); flips = R.read_flips(run)
arms = R.split_arms(G, flips, 5)
want = [("subsystem","FixedUpdate.PhysicsFixedUpdate"),("phase","FixedUpdate"),("section","Ordered Update Manager - update: Silica.AI.AIBaseAgent.OrderedUpdateAllAgents"),
("section","Ordered Update Manager - update: VehicleTurret.OrderedUpdateAllTurrets"),("section","Ordered Update Manager - updateLate: CreatureDecapod.OrderedLateUpdate"),
("section","Ordered Update Manager - fixed: Silica.AI.CollisionAvoidance.OrderedFixedUpdate"),("section","Ordered Update Manager - fixed: VehicleWheeled.OrderedFixedUpdate"),
("section","GameManager.SensorTickPart1"),("subsystem","PreLateUpdate.ScriptRunBehaviourLateUpdate")]
names = ["PhysX","FixedPh","Agents","Turrets","CrabLate","Avoid","Wheeled","Sensor1","LateScr"]
for arm in ("off","def"):
  print("arm", arm)
  print("t world fps fixed/fr proj rb " + " ".join(names))
  for x in sorted(arms[arm]["frames"], key=lambda x: x["timestamp"]):
    u = R.fnum(x.get("units"))
    if u < 520: continue
    fr = R.fnum(x.get("frames")); fs = R.fnum(x.get("fixed_steps"))
    s = G["secs"].get(x["timestamp"], {})
    vals = [s.get(k, 0.0)/fr for k in want]
    print(x["timestamp"][11:], int(u), round(fr/x["_i"],1), round(fs/fr,2), x.get("projectiles"), x.get("rigidbodies"), " ".join(f"{v:.1f}" for v in vals))
