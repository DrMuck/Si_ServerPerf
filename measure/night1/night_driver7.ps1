# rig 2 driver 7 (08:20): waits for night_driver6 (PID 17160; chain_plan_b20c = dispose_paired, parked_spaced, best2) to exit,
# then runs chain_plan_b20d.json (b20_dispose_paired2, latest_start 09:10). chain.ps1 enforces 09:30 / 10:00.
$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"; $log = "$n1\chain_progress.log"
function P($x) { Add-Content -Path $log -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') night_driver7: $x" -Encoding utf8 }
$old = 17160
P "started pid=$PID, waiting for driver6 ($old) to exit"
try { Wait-Process -Id $old -Timeout 7200 -ErrorAction Stop } catch { }
if (Get-Process -Id $old -ErrorAction SilentlyContinue) { P "driver6 still alive, giving up"; exit 1 }
Start-Sleep 5
if (Test-Path "$n1\STOP_CHAIN") { P "STOP_CHAIN present, not starting chain_plan_b20d.json"; exit 0 }
$pn = "chain_plan_b20d.json"
P "starting chain.ps1 -Plan $pn"
$c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $pn -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$pn.out" -RedirectStandardError "$n1\chain_$pn.err" -PassThru -WindowStyle Hidden
$c.Id | Set-Content "$n1\chain.pid"
$c.WaitForExit()
P "all plans done"
