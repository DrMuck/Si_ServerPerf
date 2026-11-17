flip run `smoke_mc_b5`, 25 flips, states ['A', 'B', 'C'], first 3 s after each flip dropped, blocks with < 3 samples dropped

| scenario | A ms (n) | B ms (n) | B-A paired | s.e. | % | C ms (n) | C-A paired | s.e. | % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| idle-soldiers-300 | 1.337 (1) | 1.346 (1) | 0.008 | - | 0.6 | - (0) | - | - | - |
| fight-soldiers-150 | 2.324 (1) | 1.363 (1) | -0.961 | - | -41.3 | - (0) | - | - | - |
| fight-vehicles-50 | 2.825 (1) | 2.164 (1) | -0.661 | - | -23.4 | - (0) | - | - | - |
| flip-fight-vehicles-150 | 11.167 (4) | 10.358 (3) | -1.263 | 0.536 | -11.3 | 11.095 (3) | 0.659 | 0.140 | 5.9 |
