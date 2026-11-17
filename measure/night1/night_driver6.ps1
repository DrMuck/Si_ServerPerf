# rig 2 driver 6 (07:35): waits for night_driver5 (PID 3228; its last plan is chain_plan_rest.json = b15_eveh, b15_snap_check4)
# to exit, then runs chain_plan_b20c.json (b20_dispose_paired, b20_parked_spaced, b20_best2). chain.ps1 enforces 09:30 / 10:00.
$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"; $log = "$n1\chain_progress.log"
function P($x) { Add-Content -Path $log -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') night_driver6: $x" -Encoding utf8 }
$old = 3228
P "started pid=$PID, waiting for driver5 ($old) to exit"
try { Wait-Process -Id $old -Timeout 9000 -ErrorAction Stop } catch { }
if (Get-Process -Id $old -ErrorAction SilentlyContinue) { P "driver5 still alive, giving up"; exit 1 }
Start-Sleep 5
if (Test-Path "$n1\STOP_CHAIN") { P "STOP_CHAIN present, not starting chain_plan_b20c.json"; exit 0 }
$pn = "chain_plan_b20c.json"
P "starting chain.ps1 -Plan $pn"
$c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $pn -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$pn.out" -RedirectStandardError "$n1\chain_$pn.err" -PassThru -WindowStyle Hidden
$c.Id | Set-Content "$n1\chain.pid"
$c.WaitForExit()
P "all plans done"
