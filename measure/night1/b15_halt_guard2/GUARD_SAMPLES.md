# Behaviour guard per flip block (1 Hz samples, >= 15 s after each flip, blocks >= 20 s): `b15_halt_guard2`

| plateau | arm | blocks | deaths/s | kills/s | shots/unit-s | hits/s | with-target | fps |
|---|---|---:|---|---|---|---|---|---|
| halt-guard@450 | A | 5 | 11.6 ± 0.23 | 10.8 ± 0.24 | 0.917 ± 0.039 | 186 ± 4 | 0.857 ± 0.0025 | 46.6 ± 1.5 |
| halt-guard@450 | D | 4 | 11.9 ± 0.082 | 11.1 ± 0.082 | 0.853 ± 0.0091 | 177 ± 3.5 | 0.849 ± 0.0062 | 52.1 ± 1.2 |
| halt-guard@450 | D vs A | | +3.0 % (t +1.4) | +2.6 % (t +1.1) | -7.0 % (t -1.6) | -4.7 % (t -1.7) | -0.9 % (t -1.2) | +11.9 % (t +2.9) |
