# Live on/off comparison on the owner's server (2026-11-17, 14:09-14:13)

Build b21 (0.2.0 defaults) on the owner's own server with the full public mod set (49 other mods), one player
connected, host and client on the same PC. Player-made stress round on NarakaCity with instant build: 730-790 units,
2 200-2 440 dynamic physics bodies, server far beyond saturation in both arms.

Method: `runtime.json` switched all items off and back on in alternating blocks inside the running round (10 s settle,
30 s measured). fps = frame-counter difference / time between two `runtime_state.json` markers. `Physics.Census` and
`Headless.Diag_LogExceptions` stayed on in both arms; the startup-only item `Headless.Particles_Disable` cannot be
switched and stayed on too, so "off" is 3 items on, not 0.

| Block | Arm | Items on | Seconds | Frames | Server fps | Units / bodies at start | at end |
|---:|---|---:|---:|---:|---:|---|---|
| 1 | on  | 44 | 30.6 | 279 | 9.1 | 748 / 2329 | 732 / 2282 |
| 2 | off |  3 | 31.4 | 205 | 6.5 | 746 / 2325 | 779 / 2440 |
| 3 | on  | 44 | 30.6 | 243 | 7.9 | 780 / 2439 | 760 / 2344 |
| 4 | off |  3 | 31.4 | 187 | 6.0 | 789 / 2434 | 786 / 2385 |
| 5 | on  | 44 | 31.1 | 254 | 8.2 | 781 / 2304 | 758 / 2200 |

Mean on 8.4 fps, mean off 6.3 fps: about +34 % fps (frame time about 119 ms against 160 ms, -26 %). One short
comparison, five blocks, unit count drifting by a few percent between blocks; treat it as an indication, not a
measurement. 0 exceptions before, during and after; the original 44 items were restored and `runtime.json` removed.

Earlier in the same round (mod on): 110 fps at ~180 units, 70-95 at ~370, 15-38 at ~550, 10-11 at ~620.
