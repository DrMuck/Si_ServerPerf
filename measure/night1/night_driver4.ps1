$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"
function P($x) { Add-Content -Path "$n1\chain_progress.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $x" -Encoding utf8 }
function RunPlan($plan) {
    if (Test-Path "$n1\STOP_CHAIN") { P "night_driver4: STOP_CHAIN present, not starting $plan"; return $false }
    P "night_driver4: starting chain.ps1 -Plan $plan"
    $c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $plan -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$plan.out" -RedirectStandardError "$n1\chain_$plan.err" -PassThru -WindowStyle Hidden
    $c.Id | Set-Content "$n1\chain.pid"; $c.WaitForExit(); return $true
}
try { Wait-Process -Id 25192 -Timeout 7200 -ErrorAction Stop } catch { }
if (Get-Process -Id 25192 -ErrorAction SilentlyContinue) { P "night_driver4: night_driver3 still alive, giving up"; exit 1 }
Start-Sleep 5
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
foreach ($p in "chain_plan_deep.json", "chain_plan3.json") { if (-not (RunPlan $p)) { exit 0 } }
P "night_driver4: all plans done"
