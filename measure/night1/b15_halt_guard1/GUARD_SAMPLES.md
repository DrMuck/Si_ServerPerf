# Behaviour guard per flip block (1 Hz samples, >= 15 s after each flip, blocks >= 20 s): `b15_halt_guard1`

| plateau | arm | blocks | deaths/s | kills/s | shots/unit-s | hits/s | with-target | fps |
|---|---|---:|---|---|---|---|---|---|
| halt-guard@450 | A | 5 | 11.9 ± 0.28 | 11 ± 0.28 | 0.863 ± 0.024 | 198 ± 3.1 | 0.894 ± 0.004 | 58.2 ± 0.78 |
| halt-guard@450 | D | 6 | 11.6 ± 0.075 | 10.8 ± 0.089 | 0.869 ± 0.019 | 200 ± 3.7 | 0.893 ± 0.0018 | 65.6 ± 0.36 |
| halt-guard@450 | D vs A | | -2.1 % (t -0.9) | -1.9 % (t -0.7) | +0.6 % (t +0.2) | +0.8 % (t +0.3) | -0.1 % (t -0.2) | +12.6 % (t +8.6) |
