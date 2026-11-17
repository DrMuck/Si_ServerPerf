#!/usr/bin/env python3
"""Writes scenarios_battle.json: sustained three-way battles (Sol / Centauri / Alien) on GreatErg at several total
unit counts. Four battle clusters around the map centre (one three-way, three two-way) plus units in transit and
~30 invulnerable structures per team. Every unit and structure is invulnerable (health refilled 4x/s) so the count
holds; units attack-move back and forth toward the map centre (60 m legs, re-ordered every 20 s), so they stay engaged
in their cluster instead of converging into one blob.

GreatErg HQs (1.0.17): Sol (758,-642), Centauri (-590,-642), Alien Nest (-999,261); map centre = mean (-277,-341)."""
import json
import os
import sys

HQ = {"sol": (758, -642), "cent": (-590, -642), "alien": (-999, 261)}
MC = (-277, -341)
# cluster centres (x, z) and the teams fighting there
CLUSTERS = [
    ("K1", (-277, -341), ["sol", "cent", "alien"]),   # three-way at the map centre
    ("K2", (-40, -620), ["sol", "cent"]),              # south
    ("K3", (-650, -120), ["cent", "alien"]),           # west
    ("K4", (120, -60), ["sol", "alien"]),              # north-east
]
COMP = {
    "sol": [("ObjectInfo_Sol_Soldier_Rifleman", .35), ("ObjectInfo_Sol_Soldier_Heavy", .10), ("ObjectInfo_Sol_Light_LightStriker", .25),
            ("ObjectInfo_Sol_Light_HeavyStriker", .15), ("ObjectInfo_Sol_Heavy_PulseTruck", .15)],
    "cent": [("ObjectInfo_Cent_Soldier_Trooper", .35), ("ObjectInfo_Cent_Soldier_Juggernaut", .10), ("ObjectInfo_Cent_Light_AssaultCar", .25),
             ("ObjectInfo_Cent_Light_StrikeTank", .15), ("ObjectInfo_Cent_Heavy_CombatTank", .15)],
    "alien": [("ObjectInfo_Alien_Crab", .40), ("ObjectInfo_Alien_CrabHorned", .20), ("ObjectInfo_Alien_Hunter", .15),
              ("ObjectInfo_Alien_Scorpion", .15), ("ObjectInfo_Alien_Squid", .10)],
}
STRUCT = {"sol": [("ObjectInfo_Sol_Silo", 15), ("ObjectInfo_Sol_Turret_Light", 10), ("ObjectInfo_Sol_Barracks", 3)],
          "cent": [("ObjectInfo_Cent_Silo", 15), ("ObjectInfo_Cent_Turret_Light", 10), ("ObjectInfo_Cent_Barracks", 3)],
          "alien": [("ObjectInfo_Alien_Node", 15), ("ObjectInfo_Alien_Turret_ThornSpire", 10), ("ObjectInfo_Alien_BioCache", 3)]}
TRANSIT_SHARE = 0.10


def unit(v):
    n = (v[0] ** 2 + v[1] ** 2) ** 0.5 or 1.0
    return (v[0] / n, v[1] / n)


def groups_for(total):
    per_team = total // 3
    out = []
    for team in ("sol", "cent", "alien"):
        clusters = [c for c in CLUSTERS if team in c[2]]
        fight_units = round(per_team * (1 - TRANSIT_SHARE))
        per_cluster = fight_units // len(clusters)
        for name, (cx, cz), teams in clusters:
            # place this team's group 65 m from the cluster centre on the side of its own HQ
            d = unit((HQ[team][0] - cx, HQ[team][1] - cz))
            gx, gz = cx + d[0] * 65, cz + d[1] * 65
            left = per_cluster
            for i, (prefab, share) in enumerate(COMP[team]):
                n = round(per_cluster * share) if i < len(COMP[team]) - 1 else left
                left -= n
                if n <= 0:
                    continue
                # spread kinds a little so they do not all try the same spots
                ox, oz = (i % 3 - 1) * 25, (i // 3) * 25
                out.append({"type": "unit", "prefab": prefab, "team": team, "count": n, "center": [round(gx + ox), round(gz + oz)],
                            "behaviour": "attack_move", "invulnerable": True, "_cluster": name})
        # units in transit between the team's HQ side and the map centre
        tr = per_team - per_cluster * len(clusters)
        d = unit((HQ[team][0] - MC[0], HQ[team][1] - MC[1]))
        tx, tz = MC[0] + d[0] * 350, MC[1] + d[1] * 350
        prefab = COMP[team][2][0] if team != "alien" else COMP[team][0][0]
        out.append({"type": "unit", "prefab": prefab, "team": team, "count": tr, "center": [round(tx), round(tz)],
                    "behaviour": "move_patrol", "invulnerable": True, "_cluster": "transit"})
        for prefab, n in STRUCT[team]:
            out.append({"type": "structure", "prefab": prefab, "team": team, "count": n, "center": "team_hq", "offset_m": 260,
                        "invulnerable": True})
    return out


def main():
    sizes = [int(x) for x in (sys.argv[1] if len(sys.argv) > 1 else "300,450,600,800").split(",")]
    measure = int(sys.argv[2]) if len(sys.argv) > 2 else 390
    sc = {
        "_readme": "Sustained three-way battles for Si_ServerPerf (make_battle_scenarios.py). Invulnerable units in 4 clusters + transit, "
                   "~28 invulnerable structures per team. Flip arms every 30 s during the measure window.",
        "auto_run": True, "start_after_round_start_s": 45, "between_scenarios_s": 15, "max_extra_cleanup_wait_s": 30,
        "stop_server_when_done": True, "max_spawned": 1500, "spawn_per_frame": 40, "refuse_with_humans": True,
        "fake_player_names": ["RTSA_HeadlessBot"], "disable_ai_commanders": True, "suppress_wildlife": True,
        "perfprobe_run_tag": "lab", "fast_dispose_after_s": 2,
        "defaults": {"settle_s": 45, "measure_s": measure, "team_a": "sol", "team_b": "cent", "patrol_dist_m": 60,
                     "patrol_interval_s": 20, "fight_gap_m": 200},
        "scenarios": [{"name": f"battle3-{n}", "behaviour": "attack_move", "spawns": groups_for(n)} for n in sizes],
    }
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "scenarios_battle.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(sc, f, indent=1)
    for s in sc["scenarios"]:
        units = sum(g["count"] for g in s["spawns"] if g["type"] == "unit")
        st = sum(g["count"] for g in s["spawns"] if g["type"] == "structure")
        print(s["name"], "units", units, "structures", st, "groups", len(s["spawns"]))


if __name__ == "__main__":
    main()
