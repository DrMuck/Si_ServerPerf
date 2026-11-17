$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"
function P($x) { Add-Content -Path "$n1\chain_progress.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $x" -Encoding utf8 }
function RunPlan($plan) {
    if (Test-Path "$n1\STOP_CHAIN") { P "night_driver: STOP_CHAIN present, not starting $plan"; return $false }
    P "night_driver: starting chain.ps1 -Plan $plan"
    $c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $plan -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$plan.out" -RedirectStandardError "$n1\chain_$plan.err" -PassThru -WindowStyle Hidden
    $c.Id | Set-Content "$n1\chain.pid"; $c.WaitForExit(); return $true
}
$cp = [int](Get-Content "$n1\chain.pid")
try { Wait-Process -Id $cp -Timeout 7200 -ErrorAction Stop } catch { }
if (Get-Process -Id $cp -ErrorAction SilentlyContinue) { P "night_driver: chain still running after 2 h, giving up"; exit 1 }
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
if (-not (RunPlan "chain_plan_b12.json")) { exit 0 }
if (-not (RunPlan "chain_plan_b13a.json")) { exit 0 }
# gate: the rig agent writes chain_plan_b13b.json after checking b13_snap_check; wait up to 30 min for it
$deadline = (Get-Date).AddMinutes(30)
while (-not (Test-Path "$n1\chain_plan_b13b.json") -and (Get-Date) -lt $deadline) { Start-Sleep 20 }
if (Test-Path "$n1\chain_plan_b13b.json") { Start-Sleep 5; if (-not (RunPlan "chain_plan_b13b.json")) { exit 0 } } else { P "night_driver: no chain_plan_b13b.json within 30 min -> skipping b13 runs 3/4" }
foreach ($p in "chain_plan.json", "chain_plan2.json") { if (-not (RunPlan $p)) { exit 0 } }
P "night_driver: all plans done"
