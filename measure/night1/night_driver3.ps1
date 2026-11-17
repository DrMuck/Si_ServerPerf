$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"
function P($x) { Add-Content -Path "$n1\chain_progress.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $x" -Encoding utf8 }
function RunPlan($plan) {
    if (Test-Path "$n1\STOP_CHAIN") { P "night_driver3: STOP_CHAIN present, not starting $plan"; return $false }
    P "night_driver3: starting chain.ps1 -Plan $plan"
    $c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $plan -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$plan.out" -RedirectStandardError "$n1\chain_$plan.err" -PassThru -WindowStyle Hidden
    $c.Id | Set-Content "$n1\chain.pid"; $c.WaitForExit(); return $true
}
try { Wait-Process -Id 13880 -Timeout 7200 -ErrorAction Stop } catch { }
if (Get-Process -Id 13880 -ErrorAction SilentlyContinue) { P "night_driver3: night_driver2 still alive, giving up"; exit 1 }
Start-Sleep 5
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
if (-not (RunPlan "chain_plan_b16a.json")) { exit 0 }
$deadline = (Get-Date).AddMinutes(30)
while (-not (Test-Path "$n1\chain_plan_b16b.json") -and (Get-Date) -lt $deadline) { Start-Sleep 20 }
if (Test-Path "$n1\chain_plan_b16b.json") { Start-Sleep 5; if (-not (RunPlan "chain_plan_b16b.json")) { exit 0 } } else { P "night_driver3: no chain_plan_b16b.json within 30 min -> skipping b16 runs B/C" }
foreach ($p in "chain_plan.json", "chain_plan2.json") { if (-not (RunPlan $p)) { exit 0 } }
P "night_driver3: all plans done"
